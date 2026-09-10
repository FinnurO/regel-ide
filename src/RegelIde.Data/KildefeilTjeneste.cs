using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #249, 2026-09-10] Skrive-/lesesiden for <see cref="KildefeilEntitet"/> — det VARIGE,
/// samlede registeret over feil funnet i selve kilden (typisk Lovdata). Se den entitetens
/// klassekommentar for hele resonnementet (Alternativ B, besluttet med Johann 2026-09-10).
/// <para>
/// <b>Generisk, gjenbrukbar skrivevei</b> (akseptansekriterium 3 — "kan legges til samme liste uten at
/// hele mekanismen bygges om"): denne klassen vet INGENTING om hjemmel-validering spesifikt. ETHVERT
/// fremtidig sveip/enhver fremtidig validering (issue #249 nevner selv #147/#153 som fremtidige
/// kandidater) kaller <see cref="OpprettEllerFinnAsync"/> med sin egen <c>type</c>/
/// <c>funnetAvMekanisme</c>-streng — ingen migrasjon, ingen ny tjeneste, ingen endring HER. Selve
/// KOBLINGEN mot hjemmel-valideringen (issue #233s <see cref="HjemmelValideringTjeneste"/>) er derfor
/// bevisst IKKE i denne filen, men i <see cref="HjemmelValideringTjeneste.RegistrerKildefeilAsync"/> —
/// den klassen som faktisk VET hva et "node finnes ikke"-funn betyr, kaller inn HIT, ikke omvendt.
/// </para>
/// </summary>
public sealed class KildefeilTjeneste(RegelIdeDbContext db)
{
    /// <summary>
    /// Idempotent — samme (<paramref name="rettskildeId"/>, <paramref name="rettskildeEid"/>,
    /// <paramref name="type"/>, <paramref name="funnetAvMekanisme"/>) gir samme rad tilbake i stedet
    /// for et duplikat ved gjentatt sveip/validering (samme "opprett-eller-finn"-mønster som
    /// <see cref="VirksomhetKandidatTjeneste.OpprettEllerFinnAsync"/>/
    /// <see cref="NavnekandidatOppdagelseTjeneste.OpprettEllerFinnAsync"/>). Status/beskrivelse
    /// RØRES IKKE på en allerede eksisterende rad — en triagert rad ("Kjent"/"Rettet-hos-oss") skal
    /// ikke bli tilbakestilt til "Ny" bare fordi det samme sveipet finner det samme funnet igjen.
    /// </summary>
    public async Task<KildefeilEntitet> OpprettEllerFinnAsync(
        Guid rettskildeId, string? rettskildeEid, string type, string beskrivelse,
        string funnetAvMekanisme, string opprettetAv, CancellationToken ct = default)
    {
        var eksisterende = await db.Kildefeil.FirstOrDefaultAsync(
            k => k.RettskildeId == rettskildeId && k.RettskildeEid == rettskildeEid
                 && k.Type == type && k.FunnetAvMekanisme == funnetAvMekanisme, ct);
        if (eksisterende is not null) return eksisterende;

        if (!await db.Rettskilder.AnyAsync(r => r.Id == rettskildeId, ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde med id '{rettskildeId}'. Ingen gjettet fallback.");
        }

        var rad = new KildefeilEntitet
        {
            Id = Guid.NewGuid(),
            RettskildeId = rettskildeId,
            RettskildeEid = rettskildeEid,
            Type = type,
            Beskrivelse = beskrivelse,
            FunnetAvMekanisme = funnetAvMekanisme,
            Status = "Ny",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Kildefeil.Add(rad);
        await db.SaveChangesAsync(ct);
        return rad;
    }

    /// <summary>Full liste til søke-/listesiden i UI-et, valgfritt filtrert på status og/eller
    /// rettskilde. <paramref name="status"/> = <c>null</c> betyr ALLE statuser — eksplisitt valgt av
    /// kalleren, samme "utelatt = ingen filter"-form som <see cref="VirksomhetKandidatTjeneste.ListerAsync"/>.
    /// Nyeste funn først.</summary>
    public Task<List<KildefeilEntitet>> ListerAsync(
        string? status = null, Guid? rettskildeId = null, CancellationToken ct = default)
    {
        var sporsmal = db.Kildefeil.AsQueryable();
        if (status is not null) sporsmal = sporsmal.Where(k => k.Status == status);
        if (rettskildeId is { } id) sporsmal = sporsmal.Where(k => k.RettskildeId == id);
        return sporsmal.OrderByDescending(k => k.OpprettetTidspunkt).ToListAsync(ct);
    }

    /// <summary>Antall registrerte rader, valgfritt filtrert på <paramref name="funnetAvMekanisme"/> —
    /// brukt av <see cref="HjemmelValideringTjeneste.RegistrerKildefeilAsync"/> til å måle hvor mange
    /// NYE rader en kjøring faktisk la til (§16 — mål det, ikke anta det).</summary>
    public Task<int> AntallAsync(string? funnetAvMekanisme = null, CancellationToken ct = default)
    {
        var sporsmal = db.Kildefeil.AsQueryable();
        if (funnetAvMekanisme is not null) sporsmal = sporsmal.Where(k => k.FunnetAvMekanisme == funnetAvMekanisme);
        return sporsmal.CountAsync(ct);
    }
}
