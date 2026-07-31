using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Tjenesteregister (CPSV-AP-NO, docs/03-domenemodell.md §1.5) — byggesteg 2. Navngitt "register",
/// ikke "Tjeneste", for å unngå kollisjon med domenebegrepet Tjeneste selv (jf. <see cref="TekstTaggTjeneste"/>/
/// <see cref="HandbokForfatterTjeneste"/>-suffikset). Samme stil som disse: primary-constructor DI,
/// <see cref="ArgumentException"/> for domenevalidering ("ingen gjettet fallback", §3.3), dual-write av
/// domenerad + proveniensrad i samme <c>SaveChangesAsync</c>.
/// </summary>
public sealed class TjenesteregisterTjeneste(RegelIdeDbContext db)
{
    // Samme 5-verdis statusløp som Rettskilde/Kodeliste (§3.1) — ikke full FSM-håndheving av lovlige
    // overganger i v1 (samme v1-forenkling som HandbokKommentarMetadataEntitet.Status, se §1.1.1).
    private static readonly string[] GyldigeStatuser =
        ["utkast", "under_revisjon", "validert", "publisert", "tilbaketrukket", "arkivert"];

    public Task<List<TjenesteEntitet>> ListerForAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.Tjenester
            .Where(t => t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende")
            .OrderBy(t => t.Tittel)
            .ToListAsync(ct);

    public Task<TjenesteEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Tjenester.FirstOrDefaultAsync(t => t.Id == id && t.Entitetsstatus == "gjeldende", ct);

    public Task<List<TjenesteRegelverksreferanseEntitet>> RegelverksreferanserForAsync(Guid tjenesteId, CancellationToken ct = default) =>
        db.TjenesteRegelverksreferanser.Where(r => r.TjenesteId == tjenesteId).ToListAsync(ct);

    public async Task<TjenesteEntitet> OpprettAsync(
        Guid virksomhetId, string tittel, string? beskrivelse, string? kompetentMyndighet, string? output,
        string? tjenestetype, string? malgruppe, IReadOnlyList<string>? kanaler, string? kostnad,
        string? behandlingstid, string? kontaktpunkt, string? konsekvensVedBrudd, IReadOnlyList<string>? sprak,
        string opprettetAv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }

        var tjeneste = new TjenesteEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Tittel = tittel,
            Beskrivelse = beskrivelse,
            KompetentMyndighet = kompetentMyndighet,
            Output = output,
            Tjenestetype = tjenestetype,
            Malgruppe = malgruppe,
            Kanaler = kanaler?.ToList() ?? [],
            Kostnad = kostnad,
            Behandlingstid = behandlingstid,
            Kontaktpunkt = kontaktpunkt,
            KonsekvensVedBrudd = konsekvensVedBrudd,
            Sprak = sprak?.ToList() ?? [],
            Status = "utkast",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Tjenester.Add(tjeneste);
        db.Proveniens.Add(ProveniensHjelper.NyRad("tjeneste", tjeneste.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }

    public async Task<TjenesteEntitet?> OppdaterAsync(
        Guid id, string tittel, string? beskrivelse, string? kompetentMyndighet, string? output,
        string? tjenestetype, string? malgruppe, IReadOnlyList<string>? kanaler, string? kostnad,
        string? behandlingstid, string? kontaktpunkt, string? konsekvensVedBrudd, IReadOnlyList<string>? sprak,
        string endretAv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }

        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(t => t.Id == id && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null) return null;

        tjeneste.Tittel = tittel;
        tjeneste.Beskrivelse = beskrivelse;
        tjeneste.KompetentMyndighet = kompetentMyndighet;
        tjeneste.Output = output;
        tjeneste.Tjenestetype = tjenestetype;
        tjeneste.Malgruppe = malgruppe;
        tjeneste.Kanaler = kanaler?.ToList() ?? [];
        tjeneste.Kostnad = kostnad;
        tjeneste.Behandlingstid = behandlingstid;
        tjeneste.Kontaktpunkt = kontaktpunkt;
        tjeneste.KonsekvensVedBrudd = konsekvensVedBrudd;
        tjeneste.Sprak = sprak?.ToList() ?? [];
        tjeneste.SistEndretAv = endretAv;
        tjeneste.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        tjeneste.Versjon++;
        db.Proveniens.Add(ProveniensHjelper.NyRad("tjeneste", tjeneste.Id, tjeneste.VirksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }

    public async Task<TjenesteRegelverksreferanseEntitet> KobleRegelverksreferanseAsync(
        Guid tjenesteId, Guid tilRettskildeId, string tilEid, CancellationToken ct = default)
    {
        if (!await db.Tjenester.AnyAsync(t => t.Id == tjenesteId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Tjeneste '{tjenesteId}' finnes ikke.");
        }
        if (!await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == tilRettskildeId && n.Eid == tilEid, ct))
        {
            throw new ArgumentException($"Målnoden '{tilEid}' finnes ikke i rettskilde '{tilRettskildeId}'.");
        }
        if (await db.TjenesteRegelverksreferanser.AnyAsync(
                r => r.TjenesteId == tjenesteId && r.TilRettskildeId == tilRettskildeId && r.TilEid == tilEid, ct))
        {
            throw new ArgumentException("Denne regelverksreferansen er allerede koblet.");
        }

        var referanse = new TjenesteRegelverksreferanseEntitet
        {
            Id = Guid.NewGuid(), TjenesteId = tjenesteId, TilRettskildeId = tilRettskildeId, TilEid = tilEid,
        };
        db.TjenesteRegelverksreferanser.Add(referanse);
        await db.SaveChangesAsync(ct);
        return referanse;
    }

    public async Task<bool> FjernRegelverksreferanseAsync(Guid referanseId, CancellationToken ct = default)
    {
        var referanse = await db.TjenesteRegelverksreferanser.FirstOrDefaultAsync(r => r.Id == referanseId, ct);
        if (referanse is null) return false;
        db.TjenesteRegelverksreferanser.Remove(referanse);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TjenesteEntitet?> SettStatusAsync(Guid id, string nyStatus, string endretAv, CancellationToken ct = default)
    {
        if (!GyldigeStatuser.Contains(nyStatus))
        {
            throw new ArgumentException($"Ukjent status '{nyStatus}'. Gyldige verdier: {string.Join(", ", GyldigeStatuser)}.");
        }

        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(t => t.Id == id && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null) return null;

        tjeneste.Status = nyStatus;
        tjeneste.SistEndretAv = endretAv;
        tjeneste.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("tjeneste", tjeneste.Id, tjeneste.VirksomhetId, nyStatus == "publisert" ? "publisert" : "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }

    /// <summary>
    /// Byggesteg 4 — kobler tjenesten til rotnoden (alltid en Regelnode, INV-5) i vilkårstreet.
    /// Lukker gapet fra byggesteg 2 ("vilkårskobling ... kommer i byggesteg 4", docs/06-veikart.md).
    /// </summary>
    public async Task<TjenesteEntitet?> SettRotnodeAsync(Guid tjenesteId, Guid regelnodeId, CancellationToken ct = default)
    {
        if (!await db.Regelnoder.AnyAsync(r => r.Id == regelnodeId && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen regelnode med id '{regelnodeId}'.");
        }

        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(t => t.Id == tjenesteId && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null) return null;

        tjeneste.RotnodeId = regelnodeId;
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }

    /// <summary>
    /// Fjerner koblingen mellom tjenesten og dens rotnode (2026-07-31, fasit-runde 5) — selve
    /// regelnoden slettes ikke, kun referansen fra Tjenesten. Nødvendig fordi opprettelse av en
    /// rotnode i dag er en i praksis irreversibel handling uten dette — se
    /// docs/12-fasit-handbok-leveranse.md.
    /// </summary>
    public async Task<TjenesteEntitet?> FjernRotnodeAsync(Guid tjenesteId, CancellationToken ct = default)
    {
        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(t => t.Id == tjenesteId && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null) return null;

        tjeneste.RotnodeId = null;
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }
}
