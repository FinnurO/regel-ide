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
    /// Gruppebegrep (docs/20 §2.4). <paramref name="lovkildeId"/> er nå NULLBAR (issue #298):
    /// <list type="bullet">
    /// <item>SATT — lovspesifikt gruppebegrep, uendret oppførsel fra før: <paramref name="term"/> +
    /// <paramref name="lovkildeId"/> utgjør SAMMEN identiteten (samme gruppenavn i to ulike lover er to
    /// ulike rader; samme gruppenavn i SAMME lov skal ikke kunne dupliseres).</item>
    /// <item><c>null</c> — fast, NASJONALT gruppebegrep, uten lovscoping i det hele tatt (Johanns
    /// «Kongen er et fast begrep, men koblingen Kongen til lov er egne relasjoner»-presisering,
    /// issue #298). Identiteten er da KUN <paramref name="term"/>, gjenbrukt på tvers av ALLE lover —
    /// nøyaktig samme "én rad, mange tagger"-mønster som en virksomhets navneform
    /// (<see cref="OpprettVirksomhetsbegrepAsync"/>) allerede er. Rettskilde-eksistens-sjekken hoppes
    /// da over (ingen lov å sjekke mot). Se den unike partielle indeksen i RegelIdeDbContext for
    /// DB-vernet (to separate indekser — én per gren — fordi Postgres' standard NULL-i-unik-indeks-
    /// oppførsel ikke ville dedupet flere "LovkildeId IS NULL, samme Term"-rader i ÉN delt indeks).</item>
    /// </list>
    /// [Rettet, issue #298] Duplikatsjekken var case-SENSITIV (<c>b.Term == term</c>) — til forskjell
    /// fra navneform-sjekken over, som eksplisitt bruker <c>StringComparison.OrdinalIgnoreCase</c>.
    /// Samme mønster brukt her nå (last kandidatene i minnet, sammenlign case-insensitivt): «Departementet»
    /// og «departementet» skal ikke bli to rader, uansett om begrepet er fast eller lovspesifikt.
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
        Guid? lovkildeId, string term, string opprettetAv, string? lovreferanseEid = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("Term kan ikke være tom. Ingen gjettet fallback.");
        }
        if (lovkildeId is not null
            && !await db.Rettskilder.AnyAsync(r => r.Id == lovkildeId.Value && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde med id '{lovkildeId}'. Ingen gjettet fallback.");
        }

        // Case-insensitiv duplikatsjekk, scopet til SAMME lovkildeId (inkl. "null" — fast/nasjonalt
        // er sin egen scope, se metodekommentaren) — samme mønster (last inn kandidatene, sammenlign
        // i minnet med OrdinalIgnoreCase) som navneform-sjekken i OpprettVirksomhetsbegrepAsync over.
        var finnesAlt = await db.Begreper
            .Where(b => b.Begrepskategori == "gruppe" && b.LovkildeId == lovkildeId && b.Entitetsstatus == "gjeldende")
            .Select(b => b.Term)
            .ToListAsync(ct);
        if (finnesAlt.Any(t => string.Equals(t, term.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(lovkildeId is null
                ? $"Det faste, nasjonale gruppebegrepet '{term.Trim()}' finnes allerede."
                : $"Gruppebegrepet '{term.Trim()}' finnes allerede for denne loven.");
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

    /// <summary>
    /// [Ny, issue #298 AC3] Get-or-create for et FAST, nasjonalt gruppebegrep (<see cref="OpprettGruppebegrepAsync"/>
    /// med <c>lovkildeId=null</c>) — gjenbruker en eksisterende gjeldende rad med samme <paramref name="term"/>
    /// (case-insensitiv) i stedet for å opprette en dublett, samme "gjenbruk fremfor dublett"-mønster som
    /// navneform-gjenbruken i <see cref="NavnekandidatOppdagelseTjeneste.LukkKjedenMotVirksomhetAsync"/>.
    /// Oppretter en NY rad (via <see cref="OpprettGruppebegrepAsync"/>) kun hvis <see cref="FinnFastGruppebegrepAsync"/>
    /// ikke finner noe — dette er selve "søk FØRST, opprett kun hvis ingen finnes"-kravet i issue #298 AC3.
    /// </summary>
    /// <returns>Begrepet (nytt eller gjenbrukt), og <c>true</c> hvis det ble opprettet NÅ (ikke gjenbrukt).</returns>
    public async Task<(BegrepEntitet Begrep, bool VarNyttBegrep)> OpprettEllerGjenbrukFastGruppebegrepAsync(
        string term, string opprettetAv, string? lovreferanseEid = null, CancellationToken ct = default)
    {
        var eksisterende = await FinnFastGruppebegrepAsync(term, ct);
        if (eksisterende is not null) return (eksisterende, false);

        var nytt = await OpprettGruppebegrepAsync(null, term, opprettetAv, lovreferanseEid, ct);
        return (nytt, true);
    }

    /// <summary>
    /// [Ny, issue #298 AC3] Finner et EKSISTERENDE fast, nasjonalt gruppebegrep (<c>LovkildeId == null</c>)
    /// med samme <paramref name="term"/> (case-insensitiv, samme sammenligning som duplikatsjekken i
    /// <see cref="OpprettGruppebegrepAsync"/>) — eller <c>null</c> hvis ingen finnes ennå.
    /// </summary>
    public async Task<BegrepEntitet?> FinnFastGruppebegrepAsync(string term, CancellationToken ct = default)
    {
        var kandidater = await db.Begreper
            .Where(b => b.Begrepskategori == "gruppe" && b.LovkildeId == null && b.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        return kandidater.FirstOrDefault(b => string.Equals(b.Term, term.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// [Ny, issue #299 AC3/AC4] Get-or-create for et LOVSPESIFIKT gruppebegrep — speil av
    /// <see cref="OpprettEllerGjenbrukFastGruppebegrepAsync"/>, men scopet til <paramref name="lovkildeId"/>
    /// i stedet for "ingen lov". Finnes til «Behandle gruppen»-flyten i
    /// <see cref="NavnekandidatOppdagelseTjeneste.GodkjennGruppeBatchAsync"/>: der skal N kandidater med
    /// SAMME <c>(Term, LovkildeId)</c> dele ÉN begrep-rad, og <see cref="OpprettGruppebegrepAsync"/> alene
    /// ville KASTET for kandidat 2..N (dens duplikatsjekk er riktig for et enkeltstående, utilsiktet
    /// duplikat, men feil når duplikatet er selve POENGET — gruppen er per definisjon samme tekst).
    /// </summary>
    /// <returns>Begrepet (nytt eller gjenbrukt), og <c>true</c> hvis det ble opprettet NÅ (ikke gjenbrukt).</returns>
    public async Task<(BegrepEntitet Begrep, bool VarNyttBegrep)> OpprettEllerGjenbrukGruppebegrepAsync(
        Guid lovkildeId, string term, string opprettetAv, string? lovreferanseEid = null, CancellationToken ct = default)
    {
        var eksisterende = await FinnGruppebegrepAsync(lovkildeId, term, ct);
        if (eksisterende is not null) return (eksisterende, false);

        var nytt = await OpprettGruppebegrepAsync(lovkildeId, term, opprettetAv, lovreferanseEid, ct);
        return (nytt, true);
    }

    /// <summary>
    /// [Ny, issue #299 AC3/AC4] Finner et EKSISTERENDE lovspesifikt gruppebegrep (<c>LovkildeId == lovkildeId</c>,
    /// ikke <c>null</c> — se <see cref="FinnFastGruppebegrepAsync"/> for den faste/nasjonale grenen) med
    /// samme <paramref name="term"/> (case-insensitiv, samme sammenligning som duplikatsjekken i
    /// <see cref="OpprettGruppebegrepAsync"/>) — eller <c>null</c> hvis ingen finnes ennå.
    /// </summary>
    public async Task<BegrepEntitet?> FinnGruppebegrepAsync(Guid lovkildeId, string term, CancellationToken ct = default)
    {
        var kandidater = await db.Begreper
            .Where(b => b.Begrepskategori == "gruppe" && b.LovkildeId == lovkildeId && b.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        return kandidater.FirstOrDefault(b => string.Equals(b.Term, term.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// [Ny, issue #203 pkt. 2] Administrativ inndeling (nasjon/fylke/kommune) — nøyaktig samme mønster
    /// som <see cref="OpprettGruppebegrepAsync"/> rett over (samme (Term, LovkildeId)-scoping, samme
    /// unike-partielle-indeks-vern, samme <paramref name="lovreferanseEid"/>-formål), men egen metode
    /// og egen <see cref="BegrepEntitet.Begrepskategori"/>-verdi: en administrativ inndeling er IKKE en
    /// juridisk-aktør-rolle («gruppe»), den er et geografisk/administrativt nivå SSR har bekreftet (se
    /// <see cref="NavnekandidatOppdagelseTjeneste.KlassifiserAsync"/>). Ikke slått sammen med
    /// <see cref="OpprettGruppebegrepAsync"/> til én parameterisert metode: de to har ulik
    /// KILDE-begrunnelse for scopingen (gruppe er en juridisk rolle definert AV loven; administrativ
    /// inndeling er et geografisk faktum SSR bekrefter, som loven bare NEVNER) selv om selve koden
    /// tilfeldigvis blir strukturelt lik i dag — samme "egen, parallell metode fremfor en generisk
    /// kategori-parameter"-linje som resten av denne klassen (jf. OpprettVirksomhetsbegrepAsync vs.
    /// OpprettGruppebegrepAsync).
    /// </summary>
    public async Task<BegrepEntitet> OpprettAdministrativInndelingAsync(
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
                b.Begrepskategori == "administrativ_inndeling" && b.LovkildeId == lovkildeId && b.Term == term
                && b.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Den administrative inndelingen '{term}' finnes allerede for denne loven.");
        }

        var begrep = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = null,
            Begrepskategori = "administrativ_inndeling",
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

    /// <summary>
    /// [Ny, issue #299 AC3/AC4] Get-or-create for en administrativ inndeling — speil av
    /// <see cref="OpprettEllerGjenbrukGruppebegrepAsync"/>, brukt av samme «Behandle gruppen»-flyt
    /// (<see cref="NavnekandidatOppdagelseTjeneste.GodkjennGruppeBatchAsync"/>) for kategorien
    /// <c>administrativ_inndeling</c>, som alltid er lovspesifikt (ingen fast/nasjonal gren finnes for
    /// den, se issue #298s "Ikke i denne saken"). <see cref="FinnAdministrativInndelingAsync"/> sin
    /// case-INSENSITIVE sammenligning er bevisst mer tolerant enn <see cref="OpprettAdministrativInndelingAsync"/>s
    /// egen (case-sensitive) duplikatsjekk under — det utvider ALDRI til et kast, kun til MER gjenbruk
    /// (Opprett kalles her kun når Finn ikke fant noe), og endrer ingenting ved enkeltrad-godkjenning.
    /// </summary>
    /// <returns>Begrepet (nytt eller gjenbrukt), og <c>true</c> hvis det ble opprettet NÅ (ikke gjenbrukt).</returns>
    public async Task<(BegrepEntitet Begrep, bool VarNyttBegrep)> OpprettEllerGjenbrukAdministrativInndelingAsync(
        Guid lovkildeId, string term, string opprettetAv, string? lovreferanseEid = null, CancellationToken ct = default)
    {
        var eksisterende = await FinnAdministrativInndelingAsync(lovkildeId, term, ct);
        if (eksisterende is not null) return (eksisterende, false);

        var nytt = await OpprettAdministrativInndelingAsync(lovkildeId, term, opprettetAv, lovreferanseEid, ct);
        return (nytt, true);
    }

    /// <summary>
    /// [Ny, issue #299 AC3/AC4] Finner en EKSISTERENDE administrativ inndeling med samme
    /// <paramref name="term"/> (case-insensitiv — se <see cref="OpprettEllerGjenbrukAdministrativInndelingAsync"/>
    /// sin kommentar for hvorfor det er trygt) i samme lov — eller <c>null</c> hvis ingen finnes ennå.
    /// </summary>
    public async Task<BegrepEntitet?> FinnAdministrativInndelingAsync(Guid lovkildeId, string term, CancellationToken ct = default)
    {
        var kandidater = await db.Begreper
            .Where(b => b.Begrepskategori == "administrativ_inndeling" && b.LovkildeId == lovkildeId
                        && b.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        return kandidater.FirstOrDefault(b => string.Equals(b.Term, term.Trim(), StringComparison.OrdinalIgnoreCase));
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
    // [ENDRET, issue #203 pkt. 2] 'administrativ_inndeling' lagt til i OR-en — ellers usynlig i
    // tagg-picker-en for nøyaktig samme grunn som gruppebegrep opprinnelig var det, se klassekommentaren.
    public Task<List<BegrepEntitet>> AlleAsync(CancellationToken ct = default) =>
        db.Begreper.Where(b => b.Begrepskategori == "virksomhet" || b.Begrepskategori == "gruppe"
            || b.Begrepskategori == "administrativ_inndeling")
            .Where(b => b.Entitetsstatus == "gjeldende")
            .OrderBy(b => b.Term)
            .ToListAsync(ct);
}
