using System.Text;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>Hva en ny konvertering av den lagrede kilde-HTML-en gir: eId-er som kommer til, forsvinner eller får ny tekst.</summary>
public sealed record KonverterPaaNyttRapport(
    bool Skrevet, Guid RettskildeId, string? Eli, string Utfall, Guid? NyRettskildeId,
    IReadOnlyList<string> NyeEider, IReadOnlyList<string> BorteEider, IReadOnlyList<string> EndretTekstEider);

/// <summary>
/// [Ny, issue #397, 2026-10-26] Konverterer den LAGREDE kilde-HTML-en til én rettskilde på nytt med dagens parser, og importerer den
/// som vanlig (<see cref="RettskildeImportTjeneste.ImporterMedUtfallAsync"/>): uendret AKN gir <c>Uendret</c>, ellers en ny versjon
/// (§2.1, samme som en resynk). Bruk: en parserforbedring skal gjelde for én kilde nå, uten å vente på en full resynk og uten å hente fra
/// Lovdata (samme HTML, bare ny tolkning). Den nye versjonen klassifiseres med disposisjonstyper med en gang, fordi livsløpet
/// (<see cref="DepartementLivslopTjeneste"/>) finner vedtakene gjennom dem.
/// <para>Tørrkjøring (<c>skriv=false</c>) viser bare forskjellen i noder. Ingen kanter flyttes: de står på versjonen de ble foreslått
/// mot (#367), og «hjemmel endret» flagges av importen som ved en resynk.</para>
/// </summary>
public sealed class RettskildeKonverterPaaNyttTjeneste(RegelIdeDbContext db, RettskildeImportTjeneste importer, DisposisjonstypeTjeneste klassifiserer)
{
    public async Task<KonverterPaaNyttRapport?> KjorAsync(Guid rettskildeId, bool skriv, string bruker, CancellationToken ct = default)
    {
        var kilde = await db.Rettskilder.Where(r => r.Id == rettskildeId && r.Entitetsstatus == "gjeldende" && r.Importrolle == "primaer")
            .Select(r => new { r.Id, r.Eli, r.Innhold, r.VirksomhetId }).FirstOrDefaultAsync(ct);
        if (kilde?.Innhold is null) return null;
        var html = Encoding.UTF8.GetString(kilde.Innhold);
        var resultat = LovdataKonverterer.Konverter(html);

        var gamle = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende")
            .Select(n => new { n.Eid, n.TekstHash }).ToListAsync(ct);
        var gamleVedEid = gamle.ToDictionary(n => n.Eid, n => n.TekstHash);
        var nyeVedEid = resultat.Noder.ToDictionary(n => n.Eid, n => n.TekstHash);
        var nye = nyeVedEid.Keys.Where(e => !gamleVedEid.ContainsKey(e)).Order(StringComparer.Ordinal).ToList();
        var borte = gamleVedEid.Keys.Where(e => !nyeVedEid.ContainsKey(e)).Order(StringComparer.Ordinal).ToList();
        var endret = nyeVedEid.Where(kv => gamleVedEid.TryGetValue(kv.Key, out var h) && h != kv.Value).Select(kv => kv.Key)
            .Order(StringComparer.Ordinal).ToList();

        if (!skriv) return new(false, rettskildeId, kilde.Eli, "torrkjoring", null, nye, borte, endret);

        var import = await importer.ImporterMedUtfallAsync(resultat, kilde.VirksomhetId, bruker, ct);
        if (import.Utfall == RettskildeImportUtfall.NyVersjon)
        {
            await klassifiserer.KlassifiserAsync(skriv: true, rettskildeId: import.RettskildeId, bruker: bruker, ct: ct);
        }
        return new(true, rettskildeId, kilde.Eli, import.Utfall.ToString(), import.RettskildeId, nye, borte, endret);
    }
}
