using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Ett element i Vilkår/Regelnode/Unntak sin <c>juridisk_grunnlag[]</c> (docs/03-domenemodell.md §1.8-1.10).</summary>
public sealed record JuridiskGrunnlagInput(string Kilde, string EId);

/// <summary>Ett element i Vilkår sin <c>skjonnsmomenter[]</c> — <c>Presedensreferanse</c> er ubrukelig til byggesteg 3 finnes.</summary>
public sealed record SkjonnsmomentInput(string Navn, string? Beskrivelse, string? Presedensreferanse);

/// <summary>
/// Vilkårregister (docs/03-domenemodell.md §1.8) — byggesteg 4 runde 1. Samme stil som
/// <see cref="TjenesteregisterTjeneste"/>: primary-constructor DI, <see cref="ArgumentException"/> for
/// domenevalidering, dual-write av domenerad + proveniensrad.
/// </summary>
public sealed class VilkarregisterTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeVilkarstyper = ["formell", "materiell"];
    private static readonly string[] GyldigeVurderingstyper = ["regelbasert", "skjonnsbasert", "hybrid"];
    private static readonly string[] GyldigeStatuser =
        ["utkast", "under_revisjon", "validert", "publisert", "tilbaketrukket", "arkivert"];

    public Task<List<VilkarEntitet>> ListerForAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.Vilkar.Where(v => v.VirksomhetId == virksomhetId && v.Entitetsstatus == "gjeldende")
            .OrderBy(v => v.Tittel).ToListAsync(ct);

    public Task<VilkarEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Vilkar.FirstOrDefaultAsync(v => v.Id == id && v.Entitetsstatus == "gjeldende", ct);

    public Task<List<DatasettEntitet>> InputForAsync(Guid vilkarId, CancellationToken ct = default) =>
        db.VilkarInputDatasett.Where(i => i.VilkarId == vilkarId)
            .Join(db.Datasett, i => i.DatasettId, d => d.Id, (_, d) => d)
            .ToListAsync(ct);

    public async Task<VilkarEntitet> OpprettAsync(
        Guid virksomhetId, string tittel, string? beskrivelse, string? generiskMal, string vilkarstype, string? gjelderRolle,
        IReadOnlyList<JuridiskGrunnlagInput>? juridiskGrunnlag, Guid? begrepId, string vurderingstype, string? parametreJson,
        Guid? skjonnsgrunnlagBegrepId, IReadOnlyList<SkjonnsmomentInput>? skjonnsmomenter, bool kreverDokumentasjon,
        string? eskaleringsrolle, string? veiledningTilBruker, string? veiledningTilSaksbehandler, bool erFormel,
        string? formelBeskrivelse, string opprettetAv, CancellationToken ct = default)
    {
        await ValiderAsync(tittel, vilkarstype, vurderingstype, begrepId, skjonnsgrunnlagBegrepId, ct);

        var vilkar = new VilkarEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Tittel = tittel,
            Beskrivelse = beskrivelse,
            GeneriskMal = generiskMal,
            Vilkarstype = vilkarstype,
            GjelderRolle = gjelderRolle,
            JuridiskGrunnlagJson = JsonSerializer.Serialize(juridiskGrunnlag ?? [], JsonSerialiseringHjelper.Innstillinger),
            BegrepId = begrepId,
            Vurderingstype = vurderingstype,
            ParametreJson = parametreJson ?? "{}",
            SkjonnsgrunnlagBegrepId = skjonnsgrunnlagBegrepId,
            SkjonnsmomenterJson = JsonSerializer.Serialize(skjonnsmomenter ?? [], JsonSerialiseringHjelper.Innstillinger),
            KreverDokumentasjon = kreverDokumentasjon,
            Eskaleringsrolle = eskaleringsrolle,
            VeiledningTilBruker = veiledningTilBruker,
            VeiledningTilSaksbehandler = veiledningTilSaksbehandler,
            ErFormel = erFormel,
            FormelBeskrivelse = formelBeskrivelse,
            Status = "utkast",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Vilkar.Add(vilkar);
        db.Proveniens.Add(ProveniensHjelper.NyRad("vilkar", vilkar.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return vilkar;
    }

    public async Task<VilkarEntitet?> OppdaterAsync(
        Guid id, string tittel, string? beskrivelse, string? generiskMal, string vilkarstype, string? gjelderRolle,
        IReadOnlyList<JuridiskGrunnlagInput>? juridiskGrunnlag, Guid? begrepId, string vurderingstype, string? parametreJson,
        Guid? skjonnsgrunnlagBegrepId, IReadOnlyList<SkjonnsmomentInput>? skjonnsmomenter, bool kreverDokumentasjon,
        string? eskaleringsrolle, string? veiledningTilBruker, string? veiledningTilSaksbehandler, bool erFormel,
        string? formelBeskrivelse, string endretAv, CancellationToken ct = default)
    {
        await ValiderAsync(tittel, vilkarstype, vurderingstype, begrepId, skjonnsgrunnlagBegrepId, ct);

        var vilkar = await db.Vilkar.FirstOrDefaultAsync(v => v.Id == id && v.Entitetsstatus == "gjeldende", ct);
        if (vilkar is null) return null;

        vilkar.Tittel = tittel;
        vilkar.Beskrivelse = beskrivelse;
        vilkar.GeneriskMal = generiskMal;
        vilkar.Vilkarstype = vilkarstype;
        vilkar.GjelderRolle = gjelderRolle;
        vilkar.JuridiskGrunnlagJson = JsonSerializer.Serialize(juridiskGrunnlag ?? [], JsonSerialiseringHjelper.Innstillinger);
        vilkar.BegrepId = begrepId;
        vilkar.Vurderingstype = vurderingstype;
        vilkar.ParametreJson = parametreJson ?? "{}";
        vilkar.SkjonnsgrunnlagBegrepId = skjonnsgrunnlagBegrepId;
        vilkar.SkjonnsmomenterJson = JsonSerializer.Serialize(skjonnsmomenter ?? [], JsonSerialiseringHjelper.Innstillinger);
        vilkar.KreverDokumentasjon = kreverDokumentasjon;
        vilkar.Eskaleringsrolle = eskaleringsrolle;
        vilkar.VeiledningTilBruker = veiledningTilBruker;
        vilkar.VeiledningTilSaksbehandler = veiledningTilSaksbehandler;
        vilkar.ErFormel = erFormel;
        vilkar.FormelBeskrivelse = formelBeskrivelse;
        vilkar.SistEndretAv = endretAv;
        vilkar.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        vilkar.Versjon++;
        db.Proveniens.Add(ProveniensHjelper.NyRad("vilkar", vilkar.Id, vilkar.VirksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return vilkar;
    }

    public async Task<VilkarEntitet?> SettStatusAsync(Guid id, string nyStatus, string endretAv, CancellationToken ct = default)
    {
        if (!GyldigeStatuser.Contains(nyStatus))
        {
            throw new ArgumentException($"Ukjent status '{nyStatus}'. Gyldige verdier: {string.Join(", ", GyldigeStatuser)}.");
        }

        var vilkar = await db.Vilkar.FirstOrDefaultAsync(v => v.Id == id && v.Entitetsstatus == "gjeldende", ct);
        if (vilkar is null) return null;

        vilkar.Status = nyStatus;
        vilkar.SistEndretAv = endretAv;
        vilkar.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("vilkar", vilkar.Id, vilkar.VirksomhetId, nyStatus == "publisert" ? "publisert" : "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return vilkar;
    }

    public async Task<DatasettEntitet> LeggTilInputAsync(Guid vilkarId, Guid datasettId, CancellationToken ct = default)
    {
        if (!await db.Vilkar.AnyAsync(v => v.Id == vilkarId && v.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Vilkår '{vilkarId}' finnes ikke.");
        }
        var datasett = await db.Datasett.FirstOrDefaultAsync(d => d.Id == datasettId, ct)
            ?? throw new ArgumentException($"Datasett '{datasettId}' finnes ikke.");
        if (await db.VilkarInputDatasett.AnyAsync(i => i.VilkarId == vilkarId && i.DatasettId == datasettId, ct))
        {
            throw new ArgumentException("Dette datasettet er allerede koblet som input.");
        }

        db.VilkarInputDatasett.Add(new VilkarInputDatasettEntitet { Id = Guid.NewGuid(), VilkarId = vilkarId, DatasettId = datasettId });
        await db.SaveChangesAsync(ct);
        return datasett;
    }

    public async Task<bool> FjernInputAsync(Guid vilkarId, Guid datasettId, CancellationToken ct = default)
    {
        var rad = await db.VilkarInputDatasett.FirstOrDefaultAsync(i => i.VilkarId == vilkarId && i.DatasettId == datasettId, ct);
        if (rad is null) return false;
        db.VilkarInputDatasett.Remove(rad);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ValiderAsync(
        string tittel, string vilkarstype, string vurderingstype, Guid? begrepId, Guid? skjonnsgrunnlagBegrepId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }
        if (!GyldigeVilkarstyper.Contains(vilkarstype))
        {
            throw new ArgumentException($"Ukjent vilkarstype '{vilkarstype}'. Gyldige verdier: {string.Join(", ", GyldigeVilkarstyper)}.");
        }
        if (!GyldigeVurderingstyper.Contains(vurderingstype))
        {
            throw new ArgumentException($"Ukjent vurderingstype '{vurderingstype}'. Gyldige verdier: {string.Join(", ", GyldigeVurderingstyper)}.");
        }
        if (vurderingstype is "skjonnsbasert" or "hybrid" && skjonnsgrunnlagBegrepId is null)
        {
            throw new ArgumentException($"Vurderingstype '{vurderingstype}' krever et skjønnsgrunnlag (Begrep).");
        }
        if (begrepId is not null && !await db.Begreper.AnyAsync(b => b.Id == begrepId && b.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen begrep med id '{begrepId}'.");
        }
        if (skjonnsgrunnlagBegrepId is not null && !await db.Begreper.AnyAsync(b => b.Id == skjonnsgrunnlagBegrepId && b.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen begrep med id '{skjonnsgrunnlagBegrepId}' (skjønnsgrunnlag).");
        }
    }
}
