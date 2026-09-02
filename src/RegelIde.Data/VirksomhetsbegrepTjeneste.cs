using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Register for de to nye <see cref="BegrepEntitet.Begrepskategori"/>-verdiene, `'virksomhet'` og
/// `'gruppe'` (docs/20 §2.3/§2.4) — delt/nasjonal referansedata, samme "ingen eiende virksomhet"-mønster
/// som <see cref="KodelisteregisterTjeneste"/>s `Type='ekstern-referanse'`. Skilt fra
/// <see cref="BegrepsregisterTjeneste"/> (ordinære fakta-/handlingsbegrep, fortsatt virksomhetens eget
/// arbeidsprodukt, uendret) — de to har ulik eier-semantikk og bør ikke dele valideringslogikk.
/// </summary>
public sealed class VirksomhetsbegrepTjeneste(RegelIdeDbContext db)
{
    /// <summary>Navneform brukt om en virksomhet i rettskildetekst (docs/20 §2.3) — f.eks.
    /// "Mattilsynet", "Statsforvalter". Synonymi (f.eks. "Fylkesmann"/"Statsforvalter") løses med
    /// flere rader mot samme <paramref name="virksomhetId"/> — ingen egen mekanisme.</summary>
    public async Task<BegrepEntitet> OpprettVirksomhetsbegrepAsync(
        Guid virksomhetId, string term, string opprettetAv, string? skosUrl = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("Term kan ikke være tom. Ingen gjettet fallback.");
        }
        if (!await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{virksomhetId}'. Ingen gjettet fallback.");
        }

        var begrep = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = null, // delt/nasjonal referansedata (docs/20 §2.3) — ikke virksomhetens eget arbeidsprodukt.
            Begrepskategori = "virksomhet",
            VirksomhetReferanseId = virksomhetId,
            Term = term,
            SkosUrl = skosUrl,
            Status = "publisert", // samme "intet publiseringssteg, alltid gjeldende"-begrunnelse som Kodelistes ekstern-referanse.
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(begrep);
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", begrep.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return begrep;
    }

    /// <summary>
    /// Gruppebegrep (docs/20 §2.4) — <paramref name="term"/> + <paramref name="lovkildeId"/> utgjør
    /// SAMMEN identiteten (samme gruppenavn i to ulike lover er to ulike rader; samme gruppenavn i SAMME
    /// lov skal ikke kunne dupliseres — se den unike partielle indeksen i RegelIdeDbContext).
    /// </summary>
    /// <param name="lovreferanseEid">
    /// [Ny, 2026-08-30] Valgfri eId til NØYAKTIG den noden gruppebegrepet ble oppdaget i (typisk
    /// <see cref="NavnekandidatEntitet.NodeEid"/> fra godkjenningsflyten i
    /// <see cref="NavnekandidatOppdagelseTjeneste.GodkjennAsync"/>). Uten denne var det tidligere
    /// umulig å se, fra selve paragrafen, at et gruppebegrep var "tagget" der — Johann observerte at
    /// et godkjent "Statsforvalteren"-gruppebegrep verken viste seg som en tagg i
    /// vergemålsforskriften § 19 ledd 1 (der det faktisk ble funnet), og at en lenke fra
    /// begrepssiden til loven (uten node) landet på en tom side (ingen node valgt). Fortsatt
    /// valgfri (`null`) for manuelt opprettede gruppebegrep via <c>POST /api/gruppebegrep</c>, som
    /// ikke har noen enkelt "opprinnelsesnode".
    /// </param>
    public async Task<BegrepEntitet> OpprettGruppebegrepAsync(
        Guid lovkildeId, string term, string opprettetAv, string? lovreferanseEid = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("Term kan ikke være tom. Ingen gjettet fallback.");
        }
        if (!await db.Rettskilder.AnyAsync(r => r.Id == lovkildeId && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde med id '{lovkildeId}'. Ingen gjettet fallback.");
        }
        if (await db.Begreper.AnyAsync(b =>
                b.Begrepskategori == "gruppe" && b.LovkildeId == lovkildeId && b.Term == term
                && b.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Gruppebegrepet '{term}' finnes allerede for denne loven.");
        }

        var begrep = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = null,
            Begrepskategori = "gruppe",
            LovkildeId = lovkildeId,
            Term = term,
            LovreferanseEid = lovreferanseEid,
            Status = "publisert",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(begrep);
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", begrep.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return begrep;
    }

    public Task<List<BegrepEntitet>> AlleVirksomhetsbegrepForAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId
            && b.Entitetsstatus == "gjeldende").ToListAsync(ct);

    public Task<List<BegrepEntitet>> AlleGruppebegrepForLovAsync(Guid lovkildeId, CancellationToken ct = default) =>
        db.Begreper.Where(b => b.Begrepskategori == "gruppe" && b.LovkildeId == lovkildeId
            && b.Entitetsstatus == "gjeldende").ToListAsync(ct);

    /// <summary>
    /// ALLE gruppebegrep, uansett hvilken lov de hører til — til bruk i en søk/velg-picker for å
    /// OPPRETTE en <see cref="MyndighetstildelingEntitet"/> (docs/13-backlog.md §8.1 punkt 1: fantes
    /// ingen frontend-skjema for dette, kun en read-only tildelings-tabell). Til forskjell fra
    /// <see cref="AlleGruppebegrepForLovAsync"/> (scoped til ÉN kjent lov, brukt i lovtekst-visningen)
    /// vet ikke denne kalleren på forhånd hvilken lov — brukeren skal kunne søke på tvers av alle.
    /// </summary>
    public Task<List<BegrepEntitet>> AlleGruppebegrepAsync(CancellationToken ct = default) =>
        db.Begreper.Where(b => b.Begrepskategori == "gruppe" && b.Entitetsstatus == "gjeldende")
            .OrderBy(b => b.Term)
            .ToListAsync(ct);

    public Task<BegrepEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Begreper.FirstOrDefaultAsync(b => b.Id == id && b.Entitetsstatus == "gjeldende", ct);

    /// <summary>
    /// ALLE virksomhets-/gruppebegrep, uansett hvilken virksomhet/lov de tilhører — til bruk der en
    /// bruker skal kunne tagge en forekomst i løpetekst med et virksomhetsbegrep (samme "Koble til …"-
    /// flyt som allerede finnes for ordinære fakta-/handlingsbegrep i RettskildeDetalj.tsx). Uten denne
    /// er virksomhetsbegrep INVISIBLE i den eksisterende tagg-picker-en: den vanlige
    /// <see cref="BegrepsregisterTjeneste.ListerForAsync"/> filtrerer på brukerens EGEN
    /// VirksomhetId, og disse radene har bevisst VirksomhetId=NULL (delt, docs/20 §2.3/§2.4).
    /// </summary>
    public Task<List<BegrepEntitet>> AlleAsync(CancellationToken ct = default) =>
        db.Begreper.Where(b => b.Begrepskategori == "virksomhet" || b.Begrepskategori == "gruppe")
            .Where(b => b.Entitetsstatus == "gjeldende")
            .OrderBy(b => b.Term)
            .ToListAsync(ct);
}
