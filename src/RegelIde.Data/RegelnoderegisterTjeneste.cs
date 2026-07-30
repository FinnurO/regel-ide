using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Regelnoderegister (docs/03-domenemodell.md §1.9) — byggesteg 4 runde 1. Kalt "regelnode" for å
/// unngå navnekollisjon med domenebegrepet "Regel" selv (samme "register"-mønster som
/// <see cref="TjenesteregisterTjeneste"/>/<see cref="BegrepsregisterTjeneste"/>).
/// </summary>
public sealed class RegelnoderegisterTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeOperatorer = ["OG", "ELLER", "IKKE"];
    private static readonly string[] GyldigeBarnTyper = ["vilkar", "regelnode"];
    private static readonly string[] GyldigeStatuser =
        ["utkast", "under_revisjon", "validert", "publisert", "tilbaketrukket", "arkivert"];

    public Task<List<RegelnodeEntitet>> ListerForAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.Regelnoder.Where(r => r.VirksomhetId == virksomhetId && r.Entitetsstatus == "gjeldende")
            .OrderBy(r => r.Tittel).ToListAsync(ct);

    public Task<RegelnodeEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Regelnoder.FirstOrDefaultAsync(r => r.Id == id && r.Entitetsstatus == "gjeldende", ct);

    public Task<List<RegelnodeBarnEntitet>> BarnForAsync(Guid regelnodeId, CancellationToken ct = default) =>
        db.RegelnodeBarn.Where(b => b.RegelnodeId == regelnodeId).ToListAsync(ct);

    public async Task<RegelnodeEntitet> OpprettAsync(
        Guid virksomhetId, string tittel, string? beskrivelse, string? generiskMal, string barnOperator,
        string utdataNavn, string utdataType, bool erRotnode, IReadOnlyList<JuridiskGrunnlagInput>? juridiskGrunnlag,
        string? innvilgelseTekst, string? avslagTekst, string opprettetAv, CancellationToken ct = default)
    {
        Valider(tittel, barnOperator, utdataNavn, utdataType);

        var regelnode = new RegelnodeEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Tittel = tittel,
            Beskrivelse = beskrivelse,
            GeneriskMal = generiskMal,
            BarnOperator = barnOperator,
            UtdataNavn = utdataNavn,
            UtdataType = utdataType,
            ErRotnode = erRotnode,
            JuridiskGrunnlagJson = JsonSerializer.Serialize(juridiskGrunnlag ?? [], JsonSerialiseringHjelper.Innstillinger),
            InnvilgelseTekst = innvilgelseTekst,
            AvslagTekst = avslagTekst,
            Status = "utkast",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Regelnoder.Add(regelnode);
        db.Proveniens.Add(ProveniensHjelper.NyRad("regelnode", regelnode.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return regelnode;
    }

    public async Task<RegelnodeEntitet?> OppdaterAsync(
        Guid id, string tittel, string? beskrivelse, string? generiskMal, string utdataNavn, string utdataType,
        IReadOnlyList<JuridiskGrunnlagInput>? juridiskGrunnlag, string? innvilgelseTekst, string? avslagTekst,
        string endretAv, CancellationToken ct = default)
    {
        var regelnode = await db.Regelnoder.FirstOrDefaultAsync(r => r.Id == id && r.Entitetsstatus == "gjeldende", ct);
        if (regelnode is null) return null;

        Valider(tittel, regelnode.BarnOperator, utdataNavn, utdataType);

        regelnode.Tittel = tittel;
        regelnode.Beskrivelse = beskrivelse;
        regelnode.GeneriskMal = generiskMal;
        regelnode.UtdataNavn = utdataNavn;
        regelnode.UtdataType = utdataType;
        regelnode.JuridiskGrunnlagJson = JsonSerializer.Serialize(juridiskGrunnlag ?? [], JsonSerialiseringHjelper.Innstillinger);
        regelnode.InnvilgelseTekst = innvilgelseTekst;
        regelnode.AvslagTekst = avslagTekst;
        regelnode.SistEndretAv = endretAv;
        regelnode.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        regelnode.Versjon++;
        db.Proveniens.Add(ProveniensHjelper.NyRad("regelnode", regelnode.Id, regelnode.VirksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return regelnode;
    }

    public async Task<RegelnodeEntitet?> SettOperatorAsync(Guid id, string barnOperator, string endretAv, CancellationToken ct = default)
    {
        if (!GyldigeOperatorer.Contains(barnOperator))
        {
            throw new ArgumentException($"Ukjent barn_operator '{barnOperator}'. Gyldige verdier: {string.Join(", ", GyldigeOperatorer)} (INV-6).");
        }
        var regelnode = await db.Regelnoder.FirstOrDefaultAsync(r => r.Id == id && r.Entitetsstatus == "gjeldende", ct);
        if (regelnode is null) return null;

        regelnode.BarnOperator = barnOperator;
        regelnode.SistEndretAv = endretAv;
        regelnode.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return regelnode;
    }

    public async Task<RegelnodeEntitet?> SettStatusAsync(Guid id, string nyStatus, string endretAv, CancellationToken ct = default)
    {
        if (!GyldigeStatuser.Contains(nyStatus))
        {
            throw new ArgumentException($"Ukjent status '{nyStatus}'. Gyldige verdier: {string.Join(", ", GyldigeStatuser)}.");
        }
        var regelnode = await db.Regelnoder.FirstOrDefaultAsync(r => r.Id == id && r.Entitetsstatus == "gjeldende", ct);
        if (regelnode is null) return null;

        regelnode.Status = nyStatus;
        regelnode.SistEndretAv = endretAv;
        regelnode.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("regelnode", regelnode.Id, regelnode.VirksomhetId, nyStatus == "publisert" ? "publisert" : "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return regelnode;
    }

    /// <summary>
    /// Kobler et barn (Vilkår eller Regelnode, INV-2) til en Regelnode. Avviser (ArgumentException) hvis
    /// koblingen ville skapt en sykel (INV-7, AK-3.4.6) — meldingen viser stien DAG-sjekken fant.
    /// </summary>
    public async Task<RegelnodeBarnEntitet> KobleBarnAsync(Guid regelnodeId, string barnType, Guid barnId, CancellationToken ct = default)
    {
        if (!GyldigeBarnTyper.Contains(barnType))
        {
            throw new ArgumentException($"Ukjent barn-type '{barnType}'. Gyldige verdier: {string.Join(", ", GyldigeBarnTyper)}.");
        }
        if (!await db.Regelnoder.AnyAsync(r => r.Id == regelnodeId && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Regelnode '{regelnodeId}' finnes ikke.");
        }
        if (!await FinnesAsync(barnType, barnId, ct))
        {
            throw new ArgumentException($"Fant ingen '{barnType}' med id '{barnId}'.");
        }
        if (await db.RegelnodeBarn.AnyAsync(b => b.RegelnodeId == regelnodeId && b.BarnType == barnType && b.BarnId == barnId, ct))
        {
            throw new ArgumentException("Dette barnet er allerede koblet.");
        }

        var sti = await VilkarstreGrafHjelper.FinnStiAsync(db, barnType, barnId, "regelnode", regelnodeId, ct);
        if (sti is not null)
        {
            throw new ArgumentException(
                $"Kan ikke koble '{barnType}:{barnId}' som barn av 'regelnode:{regelnodeId}' — ville skapt en sykel " +
                $"(INV-7): {VilkarstreGrafHjelper.FormaterSti(sti)}.");
        }

        // Append til slutten (2026-07-30) — Rekkefolge fantes ikke før veiledningsvisningen trengte en
        // stabil beslutnings-ordnet traversering; ingen eksisterende barn hadde noen rekkefølge å bevare.
        var rekkefolge = await db.RegelnodeBarn.Where(b => b.RegelnodeId == regelnodeId).CountAsync(ct);
        var rad = new RegelnodeBarnEntitet { Id = Guid.NewGuid(), RegelnodeId = regelnodeId, BarnType = barnType, BarnId = barnId, Rekkefolge = rekkefolge };
        db.RegelnodeBarn.Add(rad);
        await db.SaveChangesAsync(ct);
        return rad;
    }

    public async Task<bool> FjernBarnAsync(Guid regelnodeId, string barnType, Guid barnId, CancellationToken ct = default)
    {
        var rad = await db.RegelnodeBarn.FirstOrDefaultAsync(
            b => b.RegelnodeId == regelnodeId && b.BarnType == barnType && b.BarnId == barnId, ct);
        if (rad is null) return false;
        db.RegelnodeBarn.Remove(rad);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private Task<bool> FinnesAsync(string type, Guid id, CancellationToken ct) => type switch
    {
        "vilkar" => db.Vilkar.AnyAsync(v => v.Id == id && v.Entitetsstatus == "gjeldende", ct),
        "regelnode" => db.Regelnoder.AnyAsync(r => r.Id == id && r.Entitetsstatus == "gjeldende", ct),
        _ => Task.FromResult(false),
    };

    private static void Valider(string tittel, string barnOperator, string utdataNavn, string utdataType)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }
        if (!GyldigeOperatorer.Contains(barnOperator))
        {
            throw new ArgumentException($"Ukjent barn_operator '{barnOperator}'. Gyldige verdier: {string.Join(", ", GyldigeOperatorer)}.");
        }
        if (string.IsNullOrWhiteSpace(utdataNavn) || string.IsNullOrWhiteSpace(utdataType))
        {
            throw new ArgumentException("Utdata navn/type kan ikke være tomme.");
        }
    }
}
