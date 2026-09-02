using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Én <see cref="VirksomhetRelasjonEntitet"/> sett fra én bestemt virksomhets ståsted —
/// <see cref="Retning"/> forteller om denne virksomheten er Fra eller Til, og
/// <see cref="Visningstekst"/> er forhåndsberegnet ut fra <see cref="RelasjonsTypeKonfigurasjonEntitet"/>
/// sine Fra-/Til-visningsmaler (docs/29 §Del C — SAMME rad gir bevisst ULIK tekst avhengig av hvilken
/// side man spør fra, den konkrete lærdommen fra <c>Virksomhet.OverordnetEnhetId</c>-bug-en i docs/28
/// som motiverte hele denne mekanismen).
/// </summary>
public sealed record VirksomhetRelasjonVisning(
    Guid Id, string RelasjonsType, string Retning, string Visningstekst,
    Guid MotpartVirksomhetId, string MotpartNavn,
    Guid? HjemmelRettskildeId, string? HjemmelEid, string? Kommentar);

/// <summary>
/// Register for <see cref="VirksomhetRelasjonEntitet"/> (docs/28/docs/29 §Del C) — navngitte relasjoner
/// mellom to BESTEMTE, konkrete virksomheter (til forskjell fra gruppe-mekanismen i docs/29 §Del A, som
/// dekker en generisk term realisert av mange virksomheter). Modellert på
/// <see cref="TjenesteavhengighetregisterTjeneste"/>s "ett lagret rad, to beregnede visningstekster"-
/// mønster, men med visningstekst-MALENE hentet fra en spørrbar/redigerbar databasetabell
/// (<see cref="RelasjonsTypeKonfigurasjonEntitet"/>) i stedet for en kompilert C#-Dictionary — se
/// <see cref="HentForVirksomhetAsync"/>.
/// </summary>
public sealed class VirksomhetRelasjonregisterTjeneste(RegelIdeDbContext db)
{
    public async Task<List<VirksomhetRelasjonVisning>> HentForVirksomhetAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        var rader = await db.VirksomhetRelasjoner
            .Where(r => r.Entitetsstatus == "gjeldende" && (r.FraVirksomhetId == virksomhetId || r.TilVirksomhetId == virksomhetId))
            .ToListAsync(ct);
        if (rader.Count == 0) return [];

        var motpartIder = rader.SelectMany(r => new[] { r.FraVirksomhetId, r.TilVirksomhetId }).Distinct().ToList();
        var navn = await db.Virksomheter.Where(v => motpartIder.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Navn, ct);

        var typeKoder = rader.Select(r => r.RelasjonsType).Distinct().ToList();
        var typer = await db.RelasjonsTypeKonfigurasjoner
            .Where(k => typeKoder.Contains(k.Kode))
            .ToDictionaryAsync(k => k.Kode, k => k, ct);

        return rader.Select(r =>
        {
            var erFra = r.FraVirksomhetId == virksomhetId;
            var motpartId = erFra ? r.TilVirksomhetId : r.FraVirksomhetId;
            var motpartNavn = navn.GetValueOrDefault(motpartId, "(ukjent virksomhet)");
            var mal = typer.TryGetValue(r.RelasjonsType, out var type)
                ? (erFra ? type.FraVisningsmal : type.TilVisningsmal)
                : "(ukjent relasjonstype) {0}";
            var visningstekst = string.Format(mal, motpartNavn);
            return new VirksomhetRelasjonVisning(
                r.Id, r.RelasjonsType, erFra ? "fra" : "til", visningstekst,
                motpartId, motpartNavn, r.HjemmelRettskildeId, r.HjemmelEid, r.Kommentar);
        }).ToList();
    }

    /// <summary>
    /// Oppretter en rettet relasjon FRA <paramref name="fraVirksomhetId"/> TIL
    /// <paramref name="tilVirksomhetId"/>. INGEN sykel-sjekk (BFS) her — i motsetning til
    /// Tjenesteavhengighet er det ikke opplagt at en sykel i VirksomhetRelasjon er meningsløs (f.eks.
    /// kan A være "underlagt" B og B samtidig "enhet_i" A i en annen betydning), se docs/29 §C.3.
    /// </summary>
    public async Task<VirksomhetRelasjonEntitet> OpprettAsync(
        Guid fraVirksomhetId, Guid tilVirksomhetId, string relasjonsType,
        Guid? hjemmelRettskildeId, string? hjemmelEid, string? kommentar,
        string opprettetAv, CancellationToken ct = default)
    {
        if (fraVirksomhetId == tilVirksomhetId)
        {
            throw new ArgumentException("En virksomhet kan ikke ha en relasjon til seg selv.");
        }
        var relasjonsTypeFinnes = await db.RelasjonsTypeKonfigurasjoner.AnyAsync(k => k.Kode == relasjonsType && k.Aktiv, ct);
        if (!relasjonsTypeFinnes)
        {
            throw new ArgumentException($"Ukjent relasjonstype '{relasjonsType}'. Ingen gjettet fallback.");
        }
        if (!await db.Virksomheter.AnyAsync(v => v.Id == fraVirksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{fraVirksomhetId}'. Ingen gjettet fallback.");
        }
        if (!await db.Virksomheter.AnyAsync(v => v.Id == tilVirksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{tilVirksomhetId}'. Ingen gjettet fallback.");
        }
        if (hjemmelRettskildeId is not null && !await db.Rettskilder.AnyAsync(r => r.Id == hjemmelRettskildeId, ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde med id '{hjemmelRettskildeId}'. Ingen gjettet fallback.");
        }
        var duplikatFinnes = await db.VirksomhetRelasjoner.AnyAsync(
            r => r.Entitetsstatus == "gjeldende" && r.FraVirksomhetId == fraVirksomhetId
                && r.TilVirksomhetId == tilVirksomhetId && r.RelasjonsType == relasjonsType, ct);
        if (duplikatFinnes)
        {
            throw new ArgumentException("Denne relasjonen (samme fra/til/type) finnes allerede.");
        }

        var relasjon = new VirksomhetRelasjonEntitet
        {
            Id = Guid.NewGuid(),
            FraVirksomhetId = fraVirksomhetId,
            TilVirksomhetId = tilVirksomhetId,
            RelasjonsType = relasjonsType,
            HjemmelRettskildeId = hjemmelRettskildeId,
            HjemmelEid = hjemmelEid,
            Kommentar = kommentar,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.VirksomhetRelasjoner.Add(relasjon);
        db.Proveniens.Add(ProveniensHjelper.NyRad("virksomhet_relasjon", relasjon.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return relasjon;
    }

    /// <summary>Ekte <c>Remove</c>, samme presedens som <see cref="TjenesteavhengighetregisterTjeneste.SlettAsync"/>
    /// (se <see cref="VirksomhetRelasjonEntitet"/>s klassekommentar om <c>Entitetsstatus</c>).</summary>
    public async Task<bool> SlettAsync(Guid id, CancellationToken ct = default)
    {
        var relasjon = await db.VirksomhetRelasjoner.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (relasjon is null) return false;
        db.VirksomhetRelasjoner.Remove(relasjon);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
