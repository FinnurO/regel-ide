using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Begrepsregister (SKOS, docs/03-domenemodell.md §1.3) — byggesteg 2. Samme stil som
/// <see cref="TjenesteregisterTjeneste"/>/<see cref="HandbokForfatterTjeneste"/>.
/// </summary>
public sealed class BegrepsregisterTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeBegrepstyper = ["faktabegrep", "handlingsbegrep"];
    private static readonly string[] GyldigeStatuser =
        ["utkast", "foreslatt_av_ai", "under_revisjon", "validert", "publisert", "tilbaketrukket", "arkivert"];

    public Task<List<BegrepEntitet>> ListerForAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.Begreper
            .Where(b => b.VirksomhetId == virksomhetId && b.Entitetsstatus == "gjeldende")
            .OrderBy(b => b.Term)
            .ToListAsync(ct);

    public Task<BegrepEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Begreper.FirstOrDefaultAsync(b => b.Id == id && b.Entitetsstatus == "gjeldende", ct);

    public async Task<BegrepEntitet> OpprettAsync(
        Guid virksomhetId, string term, string definisjon, string? lovreferanseEid, IReadOnlyList<string>? gjelderFor,
        Guid? kodelisteReferanseId, string? skosUrl, string begrepstype, string opprettetAv, CancellationToken ct = default)
    {
        ValiderFelter(term, definisjon, begrepstype);
        await ValiderLovreferanseAsync(lovreferanseEid, ct);
        await ValiderKodelisteReferanseAsync(kodelisteReferanseId, ct);

        var begrep = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Term = term,
            Definisjon = definisjon,
            LovreferanseEid = lovreferanseEid,
            GjelderFor = gjelderFor?.ToList() ?? [],
            KodelisteReferanseId = kodelisteReferanseId,
            SkosUrl = skosUrl,
            Begrepstype = begrepstype,
            Status = "utkast",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(begrep);
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", begrep.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return begrep;
    }

    /// <summary>
    /// Byggesteg 5 runde 1 («Identifiser begrep») — kopi av <see cref="OpprettAsync"/>, men landet med
    /// Status="foreslatt_av_ai" og en <see cref="ProveniensHjelper.NyForslagRad"/>-rad i stedet for
    /// "opprettet" — aldri publisert av en agent, jf. digital-rettsstat prinsipp 4.
    /// </summary>
    public async Task<BegrepEntitet> OpprettForslagFraKiAsync(
        Guid virksomhetId, string term, string definisjon, string? lovreferanseEid, IReadOnlyList<string>? gjelderFor,
        Guid? kodelisteReferanseId, string? skosUrl, string begrepstype, string opprettetAv, string aiForslagVersjon,
        string? kildeReferanserJson, CancellationToken ct = default)
    {
        ValiderFelter(term, definisjon, begrepstype);
        await ValiderLovreferanseAsync(lovreferanseEid, ct);
        await ValiderKodelisteReferanseAsync(kodelisteReferanseId, ct);

        var begrep = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Term = term,
            Definisjon = definisjon,
            LovreferanseEid = lovreferanseEid,
            GjelderFor = gjelderFor?.ToList() ?? [],
            KodelisteReferanseId = kodelisteReferanseId,
            SkosUrl = skosUrl,
            Begrepstype = begrepstype,
            Status = "foreslatt_av_ai",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(begrep);
        db.Proveniens.Add(ProveniensHjelper.NyForslagRad("begrep", begrep.Id, virksomhetId, opprettetAv, aiForslagVersjon, kildeReferanserJson));
        await db.SaveChangesAsync(ct);
        return begrep;
    }

    /// <summary>
    /// [Rettet, 2026-08-30] Denne metoden ble skrevet FØR <see cref="BegrepEntitet.Begrepskategori"/>
    /// fantes (virksomhetskatalog-runden, 2026-08-22) og validerte/overskrev alltid
    /// <see cref="BegrepEntitet.Definisjon"/>/<see cref="BegrepEntitet.Begrepstype"/> ubetinget — disse
    /// feltene er derimot dokumentert NULL for <c>Begrepskategori IN ('virksomhet','rolle')</c> (se
    /// klassekommentaren på <see cref="BegrepEntitet"/>). Uten en kategori-bevisst sjekk her kunne PUT
    /// /api/begreper/{id} stille forurense en virksomhet-/rolle-navneform med en oppfunnet
    /// "faktabegrep"/tom definisjon — reelt observert av Johann 2026-08-30 (frontend-krasjfiksen for
    /// null-felter satte ellers en fallback-verdi i redigeringsskjemaet som ville blitt lagret som ekte
    /// data ved første "Lagre"-klikk). Løsning: hent raden FØRST, og for
    /// <c>Begrepskategori IN ('virksomhet','rolle')</c> rører vi ALDRI Definisjon/Begrepstype uansett
    /// hva som sendes inn — kun Term/LovreferanseEid/GjelderFor/KodelisteReferanseId/SkosUrl er
    /// meningsfulle å endre for disse radene.
    /// </summary>
    public async Task<BegrepEntitet?> OppdaterAsync(
        Guid id, string term, string definisjon, string? lovreferanseEid, IReadOnlyList<string>? gjelderFor,
        Guid? kodelisteReferanseId, string? skosUrl, string begrepstype, string endretAv, CancellationToken ct = default)
    {
        var begrep = await db.Begreper.FirstOrDefaultAsync(b => b.Id == id && b.Entitetsstatus == "gjeldende", ct);
        if (begrep is null) return null;

        var erVirksomhetEllerRolle = begrep.Begrepskategori is "virksomhet" or "rolle";
        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("Term kan ikke være tom. Ingen gjettet fallback.");
        }
        if (!erVirksomhetEllerRolle)
        {
            ValiderFelter(term, definisjon, begrepstype);
        }
        await ValiderLovreferanseAsync(lovreferanseEid, ct);
        await ValiderKodelisteReferanseAsync(kodelisteReferanseId, ct);

        begrep.Term = term;
        if (!erVirksomhetEllerRolle)
        {
            begrep.Definisjon = definisjon;
            begrep.Begrepstype = begrepstype;
        }
        begrep.LovreferanseEid = lovreferanseEid;
        begrep.GjelderFor = gjelderFor?.ToList() ?? [];
        begrep.KodelisteReferanseId = kodelisteReferanseId;
        begrep.SkosUrl = skosUrl;
        begrep.SistEndretAv = endretAv;
        begrep.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        begrep.Versjon++;
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", begrep.Id, begrep.VirksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return begrep;
    }

    public async Task<BegrepEntitet?> SettStatusAsync(
        Guid id, string nyStatus, string endretAv, CancellationToken ct = default, string? godkjentAv = null)
    {
        if (!GyldigeStatuser.Contains(nyStatus))
        {
            throw new ArgumentException($"Ukjent status '{nyStatus}'. Gyldige verdier: {string.Join(", ", GyldigeStatuser)}.");
        }

        var begrep = await db.Begreper.FirstOrDefaultAsync(b => b.Id == id && b.Entitetsstatus == "gjeldende", ct);
        if (begrep is null) return null;

        begrep.Status = nyStatus;
        begrep.SistEndretAv = endretAv;
        begrep.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var proveniens = ProveniensHjelper.NyRad(
            "begrep", begrep.Id, begrep.VirksomhetId, nyStatus is "publisert" or "validert" ? nyStatus : "endret", endretAv);
        proveniens.GodkjentAv = godkjentAv;
        db.Proveniens.Add(proveniens);
        await db.SaveChangesAsync(ct);
        return begrep;
    }

    private static void ValiderFelter(string term, string definisjon, string begrepstype)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("Term kan ikke være tom. Ingen gjettet fallback.");
        }
        if (string.IsNullOrWhiteSpace(definisjon))
        {
            throw new ArgumentException("Definisjon kan ikke være tom. Ingen gjettet fallback.");
        }
        if (!GyldigeBegrepstyper.Contains(begrepstype))
        {
            throw new ArgumentException($"Ukjent begrepstype '{begrepstype}'. Gyldige verdier: {string.Join(", ", GyldigeBegrepstyper)}.");
        }
    }

    private async Task ValiderLovreferanseAsync(string? lovreferanseEid, CancellationToken ct)
    {
        if (lovreferanseEid is null) return;
        if (!await db.RettskildeNoder.AnyAsync(n => n.Eid == lovreferanseEid, ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde-node med eId '{lovreferanseEid}'. Ingen gjettet fallback.");
        }
    }

    private async Task ValiderKodelisteReferanseAsync(Guid? kodelisteReferanseId, CancellationToken ct)
    {
        if (kodelisteReferanseId is null) return;
        if (!await db.Kodelister.AnyAsync(k => k.Id == kodelisteReferanseId && k.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen kodeliste med id '{kodelisteReferanseId}'.");
        }
    }
}
