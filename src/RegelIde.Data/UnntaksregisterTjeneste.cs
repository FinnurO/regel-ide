using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Unntaksregister (docs/03-domenemodell.md §1.10) — byggesteg 4 runde 1. <see cref="OpprettAsync"/>
/// krever både <c>gjelderRegelId</c> og <c>betingelseId</c> (INV-3/INV-4, referansemodell §5.4) — et
/// Unntak kan ikke opprettes uten begge.
/// </summary>
public sealed class UnntaksregisterTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeBetingelseTyper = ["vilkar", "regelnode"];
    private static readonly string[] GyldigeStatuser =
        ["utkast", "under_revisjon", "validert", "publisert", "tilbaketrukket", "arkivert"];

    public Task<List<UnntakEntitet>> ListerForAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.Unntak.Where(u => u.VirksomhetId == virksomhetId && u.Entitetsstatus == "gjeldende")
            .OrderBy(u => u.Tittel).ToListAsync(ct);

    public Task<UnntakEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Unntak.FirstOrDefaultAsync(u => u.Id == id && u.Entitetsstatus == "gjeldende", ct);

    /// <summary>
    /// Oppretter et Unntak. Avviser (ArgumentException) hvis <paramref name="gjelderRegelId"/>/
    /// <paramref name="betingelseId"/> ikke finnes, eller hvis koblingen ville skapt en sykel
    /// (INV-7, AK-3.4.6) — meldingen viser stien.
    /// </summary>
    public async Task<UnntakEntitet> OpprettAsync(
        Guid virksomhetId, string tittel, string? beskrivelse, Guid gjelderRegelId, string betingelseType, Guid betingelseId,
        IReadOnlyList<JuridiskGrunnlagInput>? juridiskGrunnlag, string opprettetAv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }
        if (!GyldigeBetingelseTyper.Contains(betingelseType))
        {
            throw new ArgumentException($"Ukjent betingelse-type '{betingelseType}'. Gyldige verdier: {string.Join(", ", GyldigeBetingelseTyper)}.");
        }
        if (!await db.Regelnoder.AnyAsync(r => r.Id == gjelderRegelId && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen regelnode med id '{gjelderRegelId}' (gjelder_regel, INV-3).");
        }
        if (!await FinnesAsync(betingelseType, betingelseId, ct))
        {
            throw new ArgumentException($"Fant ingen '{betingelseType}' med id '{betingelseId}' (betingelse, INV-4).");
        }

        var sti = await VilkarstreGrafHjelper.FinnStiAsync(db, betingelseType, betingelseId, "regelnode", gjelderRegelId, ct);
        if (sti is not null)
        {
            throw new ArgumentException(
                $"Kan ikke sette '{betingelseType}:{betingelseId}' som betingelse for unntak fra 'regelnode:{gjelderRegelId}' " +
                $"— ville skapt en sykel (INV-7): {VilkarstreGrafHjelper.FormaterSti(sti)}.");
        }

        var unntak = new UnntakEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Tittel = tittel,
            Beskrivelse = beskrivelse,
            GjelderRegelId = gjelderRegelId,
            BetingelseType = betingelseType,
            BetingelseId = betingelseId,
            JuridiskGrunnlagJson = JsonSerializer.Serialize(juridiskGrunnlag ?? [], JsonSerialiseringHjelper.Innstillinger),
            Status = "utkast",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Unntak.Add(unntak);
        db.Proveniens.Add(ProveniensHjelper.NyRad("unntak", unntak.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return unntak;
    }

    public async Task<UnntakEntitet?> OppdaterAsync(
        Guid id, string tittel, string? beskrivelse, IReadOnlyList<JuridiskGrunnlagInput>? juridiskGrunnlag,
        string endretAv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }
        var unntak = await db.Unntak.FirstOrDefaultAsync(u => u.Id == id && u.Entitetsstatus == "gjeldende", ct);
        if (unntak is null) return null;

        unntak.Tittel = tittel;
        unntak.Beskrivelse = beskrivelse;
        unntak.JuridiskGrunnlagJson = JsonSerializer.Serialize(juridiskGrunnlag ?? [], JsonSerialiseringHjelper.Innstillinger);
        unntak.SistEndretAv = endretAv;
        unntak.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        unntak.Versjon++;
        db.Proveniens.Add(ProveniensHjelper.NyRad("unntak", unntak.Id, unntak.VirksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return unntak;
    }

    public async Task<UnntakEntitet?> SettStatusAsync(Guid id, string nyStatus, string endretAv, CancellationToken ct = default)
    {
        if (!GyldigeStatuser.Contains(nyStatus))
        {
            throw new ArgumentException($"Ukjent status '{nyStatus}'. Gyldige verdier: {string.Join(", ", GyldigeStatuser)}.");
        }
        var unntak = await db.Unntak.FirstOrDefaultAsync(u => u.Id == id && u.Entitetsstatus == "gjeldende", ct);
        if (unntak is null) return null;

        unntak.Status = nyStatus;
        unntak.SistEndretAv = endretAv;
        unntak.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("unntak", unntak.Id, unntak.VirksomhetId, nyStatus == "publisert" ? "publisert" : "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return unntak;
    }

    private Task<bool> FinnesAsync(string type, Guid id, CancellationToken ct) => type switch
    {
        "vilkar" => db.Vilkar.AnyAsync(v => v.Id == id && v.Entitetsstatus == "gjeldende", ct),
        "regelnode" => db.Regelnoder.AnyAsync(r => r.Id == id && r.Entitetsstatus == "gjeldende", ct),
        _ => Task.FromResult(false),
    };
}
