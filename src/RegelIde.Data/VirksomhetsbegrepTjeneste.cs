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
    /// <summary>
    /// [Ny, navneformgrunn-runden, 2026-09-07] Det lukkede vokabularet for
    /// <see cref="BegrepEntitet.Navneformgrunn"/> — ÉN kilde, speilet av CHECK-constrainten
    /// `ck_begreper_navneformgrunn` i <see cref="RegelIdeDbContext"/>. Endres den ene, må den andre
    /// endres i samme migrasjon. NULL (uspesifisert) er gyldig og står bevisst IKKE i dette settet —
    /// se <see cref="ErGyldigNavneformgrunn"/>.
    /// </summary>
    public static readonly IReadOnlySet<string> Navneformgrunner =
        new HashSet<string>(StringComparer.Ordinal) { "gjeldende", "utgatt", "kortform", "feilskriving", "parallellnavn" };

    /// <summary>NULL er gyldig (uspesifisert — normaltilfellet for historiske rader); enhver annen
    /// verdi må stå i <see cref="Navneformgrunner"/>. Tom/blank streng er IKKE stille normalisert til
    /// NULL — en kaller som sender "" har en feil, og skal få vite det ("ingen gjettet fallback").</summary>
    public static bool ErGyldigNavneformgrunn(string? navneformgrunn) =>
        navneformgrunn is null || Navneformgrunner.Contains(navneformgrunn);

    /// <summary>Navneform brukt om en virksomhet i rettskildetekst (docs/20 §2.3) — f.eks.
    /// "Mattilsynet", "Statsforvalter". Synonymi (f.eks. "Fylkesmann"/"Statsforvalter") løses med
    /// flere rader mot samme <paramref name="virksomhetId"/> — ingen egen mekanisme.</summary>
    /// <param name="navneformgrunn">
    /// [Ny, navneformgrunn-runden, 2026-09-07] Hvorfor navneformen peker på denne virksomheten —
    /// `'gjeldende'`/`'utgatt'`/`'kortform'`/`'feilskriving'`, eller NULL for uspesifisert. Se
    /// <see cref="BegrepEntitet.Navneformgrunn"/>. Bevisst IKKE påkrevd og med NULL som default:
    /// alle eksisterende kallsteder (Brreg-import, SNL-oppslag, manuell "Legg til navneform") skal
    /// fortsette å virke uendret uten å måtte gjette en verdi.
    /// </param>
    public async Task<BegrepEntitet> OpprettVirksomhetsbegrepAsync(
        Guid virksomhetId, string term, string opprettetAv, string? skosUrl = null,
        string? navneformgrunn = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("Term kan ikke være tom. Ingen gjettet fallback.");
        }
        if (!ErGyldigNavneformgrunn(navneformgrunn))
        {
            throw new ArgumentException(
                $"Ugyldig navneformgrunn '{navneformgrunn}'. Gyldige verdier: {string.Join(", ", Navneformgrunner)} "
                + "(eller utelat feltet for uspesifisert). Ingen gjettet fallback.");
        }
        if (!await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{virksomhetId}'. Ingen gjettet fallback.");
        }
        // [Ny, nemnd/sekretariat-runden, 2026-09-09] Samme navneform TO ganger mot samme virksomhet er
        // ikke en ny opplysning — det er en dublett, og den forplanter seg: hver av de to radene kan bli
        // pekt på av sine egne tagger, og lovteksten viser da samme organnavn markert to ganger i samme
        // setning (observert konkret for «Energiklagenemnda» og «Konkurransetilsynet» 2026-09-09).
        // Gruppebegrep har alt en unik partiell indeks for nettopp dette (se
        // OpprettGruppebegrepAsync); navneformer hadde ingen tilsvarende vakt. Case-insensitivt fordi
        // sveipet også er det (VirksomhetKandidatSveipTjeneste) — «Fylkeskommune» og «fylkeskommune»
        // mot samme virksomhet er samme navneform, ikke to.
        // <para>
        // Merk at NavnekandidatOppdagelseTjeneste sin kjedelukking GJENBRUKER en eksisterende rad i
        // stedet for å kalle hit; denne vakten dekker de direkte kallene (POST /api/virksomhetsbegrep,
        // Brreg-opprettelsen) som ikke gikk gjennom noen slik sjekk.
        // </para>
        var finnesAlt = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId
                        && b.Entitetsstatus == "gjeldende")
            .Select(b => b.Term)
            .ToListAsync(ct);
        if (finnesAlt.Any(t => string.Equals(t, term.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"Virksomheten har allerede navneformen '{term.Trim()}'. Bruk den eksisterende raden "
                + "(eller endre grunnen på den) i stedet for å opprette en dublett. Ingen gjettet fallback.");
        }

        var begrep = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = null, // delt/nasjonal referansedata (docs/20 §2.3) — ikke virksomhetens eget arbeidsprodukt.
            Begrepskategori = "virksomhet",
            VirksomhetReferanseId = virksomhetId,
            Navneformgrunn = navneformgrunn,
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
    /// [Ny, nemnd/sekretariat-runden, 2026-09-09] Setter (eller fjerner) grunnen på en eksisterende
    /// navneform. Fantes ikke før: grunnen kunne bare settes VED opprettelse, eller av veiviseren når
    /// den var NULL. En navneform opprettet av Brreg-/katalogimporten (uten grunn) kunne derfor ikke
    /// merkes som gjeldende/utgått i etterkant uten å lage en dublett — som er nøyaktig dubletten
    /// vakten i <see cref="OpprettVirksomhetsbegrepAsync"/> nå nekter.
    /// </summary>
    /// <returns><c>false</c> hvis iden ikke finnes eller ikke er en navneform.</returns>
    public async Task<bool> SettNavneformgrunnAsync(
        Guid id, string? navneformgrunn, string endretAv, CancellationToken ct = default)
    {
        if (!ErGyldigNavneformgrunn(navneformgrunn))
        {
            throw new ArgumentException(
                $"Ugyldig navneformgrunn '{navneformgrunn}'. Gyldige verdier: {string.Join(", ", Navneformgrunner)} "
                + "(eller null for uspesifisert). Ingen gjettet fallback.");
        }
        var navneform = await db.Begreper.FirstOrDefaultAsync(
            b => b.Id == id && b.Begrepskategori == "virksomhet", ct);
        if (navneform is null) return false;
        navneform.Navneformgrunn = navneformgrunn;
        navneform.SistEndretAv = endretAv;
        navneform.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", id, virksomhetId: null, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// [Ny, nemnd/sekretariat-runden, 2026-09-09] Sletter én navneform, sammen med tekst-taggene som
    /// peker på den. EKTE sletting (<c>Remove</c>), ikke en status — samme prinsipp Johann låste for
    /// virksomhetsrelasjoner («det skal være mulig å slette, ikke sette status 'slettes'»).
    /// <para>
    /// Taggene MÅ med: en tagg med <c>RefId</c> mot en navneform som ikke finnes lenger er en
    /// markering i lovteksten som ikke kan følges noe sted — verre enn ingen markering, fordi den ser
    /// ut som en lukket kjede. De slettes derfor i samme operasjon, og antallet returneres slik at
    /// kalleren kan si hva som faktisk forsvant.
    /// </para>
    /// <para>
    /// Bakgrunn: dublett-navneformer (samme term to ganger mot samme virksomhet) fantes i basen og
    /// ga dobbelt markering av samme organnavn i samme setning. Vakten i
    /// <see cref="OpprettVirksomhetsbegrepAsync"/> hindrer NYE; denne finnes for å kunne rydde de
    /// gamle, siden det ikke fantes noen vei til å fjerne en navneform i det hele tatt.
    /// </para>
    /// </summary>
    /// <returns><c>null</c> hvis iden ikke finnes eller ikke er en navneform; ellers antall tagger som ble slettet med.</returns>
    public async Task<int?> SlettVirksomhetsbegrepAsync(Guid id, string slettetAv, CancellationToken ct = default)
    {
        var navneform = await db.Begreper.FirstOrDefaultAsync(
            b => b.Id == id && b.Begrepskategori == "virksomhet", ct);
        if (navneform is null) return null;

        var tagger = await db.TekstTagger.Where(t => t.RefId == id).ToListAsync(ct);
        db.TekstTagger.RemoveRange(tagger);
        db.Begreper.Remove(navneform);
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", id, virksomhetId: null, "slettet", slettetAv));
        await db.SaveChangesAsync(ct);
        return tagger.Count;
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
