using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #390 AK 4] Resultatet av etterfyllingen (eller angringen). <see cref="PerKantType"/> er «kategori typekode» → antall.</summary>
public sealed record RegisterkantEtterfylling(bool Skrevet, int Endret, IReadOnlyDictionary<string, int> PerKantType, IReadOnlyList<string> Kilder);

/// <summary>
/// [Ny, issue #390 AK 4, Johann 2026-10-16] <b>Registerkanter har kunnskapsstatus <c>eksternt_dokumentert</c>.</b> En kant med kilde utenfor
/// korpus av typen <c>register</c> (Kartverket kommuneinfo og SSR, Enhetsregisteret) er data fra et autoritativt register, ikke tolket lovtekst
/// (docs/33 §4.1, #312) — og «eksternt dokumentert» er nettopp metoden «følger av et dokument som ikke er lovtekst» (docs/profil/03 §5).
/// Det er en REGEL på kildetypen, ikke en vurdering av den enkelte kanten, og ingen gjetning.
/// <para>
/// <b>Avgrensning [LÅST, oppdraget for #390]:</b> bare kanter der kildetypen er <c>register</c> og kunnskapsstatus ikke er vurdert (NULL).
/// Vedtekter (helseregionene), kgl.res. (statsforvalterne), nettsider og kanter med hjemmel i korpus røres ikke, og en kant som alt har en
/// kunnskapsstatus, overskrives aldri. Godkjenningen endres ikke.
/// </para>
/// <para>
/// <b>Etterfylt med proveniens, og reversibelt.</b> Endringen gjøres på raden (ikke som revisjon: utsagnet er det samme, bare metoden er satt —
/// og en revisjon ville gitt 730 nye kant-id-er og brutt premisser og bekreftelser). Hver kant får en proveniensrad (<c>endret</c>, felt
/// <c>kunnskapsstatus</c>, fra NULL til <c>eksternt_dokumentert</c>, <see cref="Regel"/>). <see cref="AngreAsync"/> setter tilbake
/// akkurat de kantene der siste kunnskapsstatus-endring er denne regelen, med en ny proveniensrad. Nye registerkanter fra
/// <see cref="OmraderegisterSeed"/> får statusen ved opprettelsen.
/// </para>
/// </summary>
public sealed class RegisterkantKunnskapsstatusTjeneste(RegelIdeDbContext db)
{
    /// <summary>Regelens navn i proveniensen.</summary>
    public const string Regel = "register_eksternt_dokumentert";

    private const string Felt = "kunnskapsstatus";

    /// <summary>Kantene regelen gjelder for: kilde utenfor korpus av typen register, kunnskapsstatus ikke vurdert. Uten <paramref name="skriv"/>
    /// en ren telling.</summary>
    public async Task<RegisterkantEtterfylling> EtterfyllAsync(bool skriv, string endretAv, CancellationToken ct = default)
    {
        var kanter = await db.Strukturkanter
            .Where(k => k.KildeUtenforKorpusType == Strukturkanter.Register && k.Kunnskapsstatus == null
                        && k.Entitetsstatus == Strukturkanter.Gjeldende && k.Status != Strukturkanter.Avvist)
            .ToListAsync(ct);
        if (skriv)
        {
            foreach (var k in kanter)
            {
                k.Kunnskapsstatus = Strukturkanter.EksterntDokumentert;
                k.Profilversjon ??= Strukturkanter.Profilversjon;
                db.Proveniens.Add(Rad(k.Id, endretAv, fra: null, til: Strukturkanter.EksterntDokumentert, angret: false));
            }
            await db.SaveChangesAsync(ct);
        }
        return Resultat(skriv, kanter);
    }

    /// <summary>Setter tilbake (til NULL) de registerkantene der siste kunnskapsstatus-endring var <see cref="Regel"/>. En kant noen har
    /// vurdert etterpå, røres ikke.</summary>
    public async Task<RegisterkantEtterfylling> AngreAsync(bool skriv, string endretAv, CancellationToken ct = default)
    {
        var kanter = await db.Strukturkanter
            .Where(k => k.KildeUtenforKorpusType == Strukturkanter.Register && k.Kunnskapsstatus == Strukturkanter.EksterntDokumentert)
            .ToListAsync(ct);
        var ider = kanter.Select(k => k.Id).ToList();
        var rader = await db.Proveniens.Where(p => p.EntitetType == StrukturkantTjeneste.ProveniensType && ider.Contains(p.EntitetId)
                                                  && p.KildeReferanserJson != null)
            .Select(p => new { p.EntitetId, p.Dato, p.KildeReferanserJson }).ToListAsync(ct);
        var sisteRegel = rader
            .Select(r => (r.EntitetId, r.Dato, Json: Les(r.KildeReferanserJson!)))
            .Where(r => r.Json.Felt == Felt)
            .GroupBy(r => r.EntitetId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Dato).First().Json);
        var angres = kanter.Where(k => sisteRegel.TryGetValue(k.Id, out var j) && j.Regel == Regel && !j.Angret).ToList();
        if (skriv)
        {
            foreach (var k in angres)
            {
                k.Kunnskapsstatus = null;
                k.Profilversjon = null;
                db.Proveniens.Add(Rad(k.Id, endretAv, fra: Strukturkanter.EksterntDokumentert, til: null, angret: true));
            }
            await db.SaveChangesAsync(ct);
        }
        return Resultat(skriv, angres);
    }

    private static RegisterkantEtterfylling Resultat(bool skriv, List<StrukturkantEntitet> kanter) => new(skriv, kanter.Count,
        kanter.GroupBy(k => $"{k.Kategori} {k.Typekode}").OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
        kanter.Select(k => KildeUtenDato(k.KildeUtenforKorpusTekst)).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList());

    /// <summary>Kildeteksten forkortet til registeret (SSR-kantene har én tekst per tettsted).</summary>
    private static string KildeUtenDato(string? tekst) =>
        tekst is null ? "(ingen kildetekst)" : tekst.StartsWith("Kartverket SSR", StringComparison.Ordinal) ? "Kartverket SSR" : tekst;

    private static ProveniensEntitet Rad(Guid kantId, string endretAv, string? fra, string? til, bool angret)
    {
        var prov = ProveniensHjelper.NyRad(StrukturkantTjeneste.ProveniensType, kantId, virksomhetId: null, "endret", endretAv);
        prov.KildeReferanserJson = JsonSerializer.Serialize(new
        {
            felt = Felt, fra, til, regel = Regel, angret, issue = 390,
            merknad = angret
                ? "Etterfyllingen er angret: kunnskapsstatus satt tilbake til ikke vurdert."
                : "Kilde utenfor korpus av typen register (Kartverket, Enhetsregisteret): eksternt dokumentert etter regel, ikke vurdert enkeltvis. Godkjenningen er ikke endret.",
        });
        return prov;
    }

    private sealed record Proveniensfelt(string? Felt, string? Regel, bool Angret);

    private static Proveniensfelt Les(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            var r = d.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return new(null, null, false);
            string? S(string navn) => r.TryGetProperty(navn, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            return new(S("felt"), S("regel"), r.TryGetProperty("angret", out var a) && a.ValueKind == JsonValueKind.True);
        }
        catch (JsonException)
        {
            return new(null, null, false);
        }
    }
}
