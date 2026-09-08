namespace RegelIde.Data;

/// <summary>
/// [Ny, registernavn-runden, 2026-09-08] Eksplisitte, navngitte navneformer for de radene i
/// <c>Seed/organisasjoner-norge.json</c> der INGEN autoritativ ekstern kilde har en lesbar form av
/// navnet. Motstykket til de to automatiske veiene i
/// <see cref="VirksomhetRegisternavnSynkTjeneste"/>: SSR for kommunene (357 rader) og SNL for de
/// statlige/andre der leksikonet bekrefter navnet (45 rader).
///
/// <para>
/// <b>Hvorfor en håndskrevet tabell er riktig her, når #206 avviste en algoritme</b>: dette er ikke en
/// regel som skal gjelde ukjente framtidige navn — det er 45 KJENTE rader i en LUKKET kildefil, hver
/// med et navn Johann har gått gjennom og godkjent én for én (2026-09-08). Nøyaktig samme resonnement
/// som <see cref="DepartementSeed"/> sin navnekommentar allerede fører for sine 13: «bare 13 navn, alle
/// kjent på forhånd, så et manuelt, korrekt navn er billigere og tryggere enn en algoritme som
/// garantert ville bommet på minst én av dem». En per-ord-algoritme ville her produsert
/// «Nærings- Og Fiskeridepartementet», og den gamle <c>FormaterNavnEnkelt</c>-veien produserte faktisk
/// «(nve)»/«(dsb)» med små bokstaver og «Statsforvalteren i innlandet».
/// </para>
///
/// <para>
/// <b>Dette er navneFORMER, ikke navn.</b> <see cref="Virksomhet.Navn"/> settes ALLTID til Brregs egen
/// form (issue #158) — også for radene her. Tabellen fyller bare navneformene
/// (<see cref="BegrepEntitet"/> med <c>Begrepskategori='virksomhet'</c>) som visningen og taggingen
/// bruker.
/// </para>
///
/// <para>
/// <b>De 14 fylkeskommunene får ingen samisk navneform</b>, selv om Finnmark og Troms fylkeskommune
/// har offisielle samiske navn: SSR bærer den samiske formen for FYLKET («Romssa fylka»), ikke for
/// fylkes-KOMMUNEN, og «Romssa fylka» blir ikke «Romssa fylkkasuohkan» av en ordbytting. Ingen gjettet
/// fallback — de kan legges til manuelt når en autoritativ skrivemåte finnes.
/// </para>
/// </summary>
public static class VirksomhetNavneformOverstyringer
{
    /// <summary>Én navneform: termen slik den skal VISES, og grunnen til at den peker på virksomheten
    /// (samme lukkede vokabular som <see cref="VirksomhetsbegrepTjeneste.Navneformgrunner"/>).</summary>
    public sealed record Navneform(string Term, string Grunn);

    /// <summary>
    /// Nøklet på organisasjonsnummer, ikke navn — navnet er nettopp det som er feil i disse radene, og
    /// et navneoppslag ville dessuten brutt i samme øyeblikk Brreg-synken endret strengen.
    /// <para>
    /// FØRSTE navneform med grunn <c>'gjeldende'</c> er den visningen bruker (se
    /// <see cref="VirksomhetVisningsnavnTjeneste"/>). Rekkefølgen i listene under er derfor
    /// meningsbærende.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<Navneform>> PerOrganisasjonsnummer =
        new Dictionary<string, IReadOnlyList<Navneform>>(StringComparer.Ordinal)
        {
            // ---- 14 fylkeskommuner. Ren skrivemåte-retting av registernavnet; mønsteret
            // «<Fylke> fylkeskommune» er uniformt for alle 14. SSR bekrefter fylkesnavnet
            // («Agder fylke»), men har ikke fylkeskommune-formen — se klassekommentaren.
            ["921707134"] = [new("Agder fylkeskommune", "gjeldende")],
            ["930580783"] = [new("Akershus fylkeskommune", "gjeldende")],
            ["930580260"] = [new("Buskerud fylkeskommune", "gjeldende")],
            ["830090282"] = [new("Finnmark fylkeskommune", "gjeldende")],
            ["920717152"] = [new("Innlandet fylkeskommune", "gjeldende")],
            ["944183779"] = [new("Møre og Romsdal fylkeskommune", "gjeldende")],
            ["964982953"] = [new("Nordland fylkeskommune", "gjeldende")],
            ["971045698"] = [new("Rogaland fylkeskommune", "gjeldende")],
            ["929882989"] = [new("Telemark fylkeskommune", "gjeldende")],
            ["930068128"] = [new("Troms fylkeskommune", "gjeldende")],
            ["817920632"] = [new("Trøndelag fylkeskommune", "gjeldende")],
            ["929882385"] = [new("Vestfold fylkeskommune", "gjeldende")],
            ["821311632"] = [new("Vestland fylkeskommune", "gjeldende")],
            ["930580694"] = [new("Østfold fylkeskommune", "gjeldende")],

            // ---- 10 statsforvaltere. Formen under står ALLEREDE korrekt i eksportfila, mens Brreg
            // har dem i VERSALER (verifisert live 2026-09-08: 974761645 = "STATSFORVALTEREN I
            // INNLANDET"). Tre av dem er legitimt på NYNORSK («Statsforvaltaren») — det er embetenes
            // egen målform, ikke en skrivefeil, og skal ikke normaliseres bort.
            ["974761319"] = [new("Statsforvalteren i Østfold, Buskerud, Oslo og Akershus", "gjeldende")],
            ["974761645"] = [new("Statsforvalteren i Innlandet", "gjeldende")],
            ["974762501"] = [new("Statsforvalteren i Vestfold og Telemark", "gjeldende")],
            ["974762994"] = [new("Statsforvalteren i Agder", "gjeldende")],
            ["974763230"] = [new("Statsforvaltaren i Rogaland", "gjeldende")],
            ["974760665"] = [new("Statsforvaltaren i Vestland", "gjeldende")],
            ["974764067"] = [new("Statsforvaltaren i Møre og Romsdal", "gjeldende")],
            ["974764350"] = [new("Statsforvalteren i Trøndelag", "gjeldende")],
            ["974764687"] = [new("Statsforvalteren i Nordland", "gjeldende")],
            ["967311014"] = [new("Statsforvalteren i Troms og Finnmark", "gjeldende")],

            // ---- 21 øvrige statlige/andre. Kortformene er Johanns eksplisitte bestilling
            // (2026-09-08): dagligformen skal være søkbar og taggbar ved siden av det fulle navnet,
            // men det er det FULLE navnet som vises.
            ["889640782"] = [new("Arbeids- og velferdsetaten", "gjeldende"), new("Nav", "kortform")],

            // MÅ være eksakt denne strengen: RettskildeEntitet.AnsvarligDepartement kommer fra
            // Lovdatas "ministry"-felt, som skriver nøyaktig denne formen (se DepartementSeed).
            // Oppslaget er case-insensitivt (VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnAsync),
            // så koblingen tåler at Navn er VERSAL — men navneformen skal likevel stemme.
            ["912660680"] = [new("Nærings- og fiskeridepartementet", "gjeldende")],

            ["914459265"] = [new("Advokattilsynet", "gjeldende")],
            ["940415683"] = [new("Maritim pensjonskasse", "gjeldende")],

            // Parentes-forkortelsen Brreg har limt på navnet skilles UT som egen kortform (Johanns
            // instruks «skill ut»), i stedet for å stå inne i visningsnavnet.
            ["970205039"] = [new("Norges vassdrags- og energidirektorat", "gjeldende"), new("NVE", "kortform")],
            ["974760983"] = [new("Direktoratet for samfunnssikkerhet og beredskap", "gjeldende"), new("DSB", "kortform")],
            ["985165262"] = [new("Nasjonal sikkerhetsmyndighet", "gjeldende"), new("NSM", "kortform")],

            ["971040238"] = [new("Statens kartverk", "gjeldende"), new("Kartverket", "kortform")],
            ["971526068"] = [new("Bildende Kunstneres Hjelpefond", "gjeldende")],
            ["974446871"] = [new("Nasjonal kommunikasjonsmyndighet", "gjeldende"), new("Nkom", "kortform")],
            ["974760673"] = [new("Registerenheten i Brønnøysund", "gjeldende"), new("Brønnøysundregistrene", "kortform")],
            ["974761122"] = [new("Direktoratet for medisinske produkter", "gjeldende")],

            // «STI» i registernavnet er ORGANISASJONSFORMEN (stiftelse) limt inn i navnestrengen, ikke
            // en del av navnet — SNL fører artikkelen som «Reisegarantifondet».
            ["975421333"] = [new("Reisegarantifondet", "gjeldende")],

            // Brreg har DOBBELT mellomrom i denne («LOTTERI-  OG STIFTELSESTILSYNET», verifisert live
            // 2026-09-08). Registernavnet beholder det; navneformen retter det.
            ["982391490"] = [new("Lotteri- og stiftelsestilsynet", "gjeldende")],

            // SF/AS/HF er del av det JURIDISKE navnet og beholdes i gjeldende form; dagligformen
            // legges til som kortform.
            ["983609155"] = [new("Enova SF", "gjeldende"), new("Enova", "kortform")],
            ["985198292"] = [new("Avinor AS", "gjeldende"), new("Avinor", "kortform")],
            ["997005562"] = [new("Helse Møre og Romsdal HF", "gjeldende")],
            ["983974791"] = [new("Helse Nord-Trøndelag HF", "gjeldende")],

            // «Helseøkonomiforvaltningen» er IKKE et utgått navn, selv om det ligner: Brregs egne
            // historiskeNavn for 986965610 er "NASJONAL OPPGJØRSENHET" og "NAV HELSETJENESTEFORVALTNING"
            // (verifisert live 2026-09-08) — Helseøkonomiforvaltningen står ikke der. SNL fører den
            // under «også kjent som», altså utleggingen av forkortelsen HELFO. Den er derfor
            // 'parallellnavn', og de to Brreg faktisk oppgir som historiske er 'utgatt' — det er DE som
            // kan stå i eldre lovtekst.
            ["986965610"] =
            [
                new("Helfo", "gjeldende"),
                new("Helseøkonomiforvaltningen", "parallellnavn"),
                new("Nasjonal oppgjørsenhet", "utgatt"),
                new("Nav Helsetjenesteforvaltning", "utgatt"),
            ],

            ["926721380"] = [new("Norges Høyesterett", "gjeldende")],

            // SNLs oppslagsord er «gjenopptakelseskommisjonen» med liten g — leksikonkonvensjon for
            // oppslagsord. Artikkelteksten selv skriver «Gjenopptakelseskommisjonen», og en navneform
            // med liten forbokstav ville sett ut som en skrivefeil i tagg-listen.
            ["985847215"] =
            [
                new("Kommisjonen for gjenopptakelse av straffesaker", "gjeldende"),
                new("Gjenopptakelseskommisjonen", "kortform"),
            ],
        };

    /// <summary>
    /// De fire radene som er FJERNET fra <c>Seed/organisasjoner-norge.json</c> i denne runden (Johanns
    /// instruks 2026-09-08): tre NTL-fagforeningsledd og ett bedriftsidrettslag. De gjør ingen
    /// offentlige oppgaver og har ingen plass i en virksomhetskatalog for regelverksarbeid.
    ///
    /// <para>
    /// <b>Dette omgjør docs/20 §4 sin <c>[LÅST]</c>-avgrensning</b> («ALLE 451 rader seedes uendret,
    /// ingen navnemønster- eller orgForm-basert filtrering») for nøyaktig disse fire, og §4 er oppdatert
    /// tilsvarende. Avgrensningen står ellers uendret: dette er FIRE NAVNGITTE organisasjonsnumre, ikke
    /// en regel. En regel på <c>orgForm=FLI</c> ville tatt en femte rad med seg — DEBIO (971475471),
    /// som SNL bekrefter som en reell institusjon og som skal beholdes.
    /// </para>
    ///
    /// <para>
    /// Listen beholdes i koden etter at radene er fjernet fra kildefila, fordi en base som ALLEREDE er
    /// seedet fortsatt har dem: <see cref="VirksomhetRegisternavnSynkTjeneste"/> sletter dem eksplisitt
    /// og logger hver rad den fjerner (issue #157 «ingen stille destruksjon» — en sletting skal alltid
    /// være etterprøvbar i ettertid).
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> SlettedeOrganisasjonsnumre =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "892518572", // NTL BRØNNØYSUNDREGISTRENE
            "915411770", // NTL DOMSTOLENE
            "919483741", // NTL DIREKTORATET FOR E-HELSE
            "896349422", // KARTVERKET BEDRIFTSIDRETTSLAG (BIL)
        };
}
