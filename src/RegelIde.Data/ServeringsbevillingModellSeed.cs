using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Seed for Rettighet/Handling-modellrunden (2026-08-20) — overfører innholdet fra den hånd-skrevne
/// modellutforskningen (<c>serveringsbevilling-modell-forslag.json</c>, en ren JSON-øvelse uten kode)
/// til ekte rader, nå som selve modellen (<see cref="HandlingEntitet"/>, Rettighet-utvidelsene på
/// <see cref="TjenesteEntitet"/>) faktisk er bygget.
///
/// Kjøres etter <see cref="BergenKorpusSeed"/> (krever Bergen kommune-virksomheten) og
/// <see cref="HendelseTjenesteavhengighetSeed"/> (krever Serveringsbevilling-raden fra
/// <see cref="FasitRunde4Seed"/>).
///
/// BEVISST IKKE bygget her (utenfor godkjent plan denne runden, se plan-filen): en strukturert
/// "krav"/"betingelse"-kolonne på <see cref="TjenesteavhengighetEntitet"/> — nyansen "krever" vs.
/// "kan kreve" fra JSON-modellen skrives derfor som fritekst i <c>Beskrivelse</c>, samme form som
/// avhengigheter allerede har hatt. En egen "mal_type: rettskilde" for Bruksendring av næringslokaler
/// er heller ikke bygget — den fanges i stedet som en veiledningstekst-rad med hjemmel på selve
/// søknadshandlingen, se under.
/// </summary>
public static class ServeringsbevillingModellSeed
{
    private const string SeedBruker = "Kari Jurist";
    private const string MarkorHandling = "Melding om endringer ved serveringsstedet"; // global guard

    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        // OBS: markørsjekken (guard) for handlinger/avhengigheter er FLYTTET NED, til rett før
        // handlinger-opprettelsen (§3) — se kommentaren der. Rettighet-feltene (§1/§2) skal fylles
        // ut/oppdateres HVER gang SeedAsync kjører, selv etter markøren er satt (idempotent —
        // OppdaterAsync/find-or-create skriver bare de samme verdiene på nytt), slik at en
        // etterfølgende utvidelse av innholdet (Type/Formal/Innhold, 2026-08-20-runde 2) faktisk
        // slår gjennom mot en database der handlingene allerede ble seedet i en TIDLIGERE kjøring.

        // Finn Serveringsbevilling FØRST, via KompetentMyndighet-markøren FasitRunde4Seed setter på
        // nettopp denne raden (RelevanteTjenester-løkken der) — og AVLED Testkommunen fra dens eget
        // VirksomhetId, i stedet for et uskopet navneoppslag på "Testkommunen" i seg selv. Et rent
        // navneoppslag er ikke unikt: flere uavhengige virksomheter kan hete "Testkommunen" (f.eks. i
        // delte testdatabaser der andre testklasser oppretter egne rader med samme navn) og hver av
        // dem kan ha sin EGEN "Serveringsbevilling"-tjeneste — et navneoppslag alene risikerer da å
        // treffe en helt annen virksomhet enn den FasitRunde4Seed faktisk opprettet i. KompetentMyndighet
        // == "Testkommunen" er derimot unikt for akkurat denne raden.
        var serveringsbevilling = await db.Tjenester.FirstOrDefaultAsync(
            t => t.Tittel == "Serveringsbevilling" && t.KompetentMyndighet == "Testkommunen" && t.Entitetsstatus == "gjeldende", ct);
        if (serveringsbevilling is null) return; // FasitRunde4Seed må ha kjørt først
        var testkommunen = await db.Virksomheter.SingleAsync(v => v.Id == serveringsbevilling.VirksomhetId, ct);

        var bergenKommune = await db.Virksomheter.FirstOrDefaultAsync(v => v.Navn == "Bergen kommune", ct);
        if (bergenKommune is null) return; // BergenKorpusSeed må ha kjørt først

        var tjenesteregister = new TjenesteregisterTjeneste(db);
        var handlingregister = new HandlingregisterTjeneste(db);
        var avhengighetregister = new TjenesteavhengighetregisterTjeneste(db);

        // ---------- 1. Serveringsbevilling — fyller ut de nye Rettighet-feltene ----------
        // OppdaterAsync skriver ALLE felt fra forespørselen (dokumentert atferd, docs/17 §2.2/§5.1) —
        // eksisterende verdier må derfor leses og gis tilbake uendret for feltene vi ikke endrer her.
        await tjenesteregister.OppdaterAsync(
            serveringsbevilling.Id, testkommunen.Id, serveringsbevilling.Tittel, serveringsbevilling.Beskrivelse,
            serveringsbevilling.KompetentMyndighet, serveringsbevilling.Output, serveringsbevilling.Tjenestetype,
            malgruppe: [
                "Virksomheter som skal etablere et nytt serveringssted",
                "Virksomheter som skal overta et eksisterende serveringssted",
                "Restaurant, kafé, gatekjøkken, kiosk eller lignende virksomhet med servering",
                "Mobile serveringssteder (foodtruck, matvogn) dersom virksomheten omfattes av serveringsloven",
            ],
            serveringsbevilling.Kanaler, serveringsbevilling.Kostnad, serveringsbevilling.Behandlingstid,
            serveringsbevilling.Kontaktpunkt,
            konsekvensVedBrudd: "Dersom virksomheten ikke oppfyller kravene i serveringsloven eller andre relevante " +
                "regler, kan kommunen fatte vedtak om suspensjon eller tilbakekall av serveringsbevillingen. " +
                "Alvorlige eller gjentatte brudd kan føre til at virksomheten ikke lenger kan drive serveringsstedet.",
            serveringsbevilling.Sprak, SeedBruker, ct,
            livshendelser: ["Starte og drive en bedrift"], losKlassifisering: null, tjenesteomrade: "Næring, salg og servering",
            type: "myndighetsutovelse",
            formal: "Serveringsbevilling gir tillatelse til å etablere og drive et serveringssted hvor det serveres " +
                "mat og/eller drikke, og hvor forholdene ligger til rette for fortæring på stedet. Formålet med " +
                "ordningen er å sikre at serveringssteder drives i samsvar med gjeldende regelverk og at " +
                "virksomheten utøves på en forsvarlig måte.",
            innhold: ServeringsbevillingInnhold());

        // ---------- 2. Fettutskiller — ny, ekte Rettighet, eid av Bergen kommune ----------
        // Bergen kommune, IKKE Testkommunen — det er den faktiske eieren av den allerede
        // eksisterende "Krav om fettutskiller"-rettskilden (Brukerveiledning-doctype), bekreftet
        // 2026-08-20 mot /api/rettskilder. Dette gjør avhengigheten under til en reell,
        // kryss-tenant-forankret kobling, ikke en plassholder.
        var fettutskillerFunnetFra = await db.Tjenester.FirstOrDefaultAsync(
            t => t.Tittel == "Krav om fettutskiller" && t.VirksomhetId == bergenKommune.Id && t.Entitetsstatus == "gjeldende", ct);
        var fettutskiller = fettutskillerFunnetFra ?? await tjenesteregister.OpprettAsync(
            bergenKommune.Id, "Krav om fettutskiller",
            beskrivelse: "Ordningen skal bidra til å beskytte det kommunale avløpsnettet og renseanlegg mot " +
                "problemer som oppstår når fett slippes ut i avløpssystemet. Fettutskilleren skiller ut fett fra " +
                "avløpsvannet før vannet ledes videre til det offentlige avløpsnettet.",
            kompetentMyndighet: "Bergen kommune", output: null, tjenestetype: "Enkeltvedtak", malgruppe: null,
            kanaler: null, kostnad: null, behandlingstid: null, kontaktpunkt: null, konsekvensVedBrudd: null,
            sprak: ["nb"], opprettetAv: SeedBruker, ct: ct);

        // OppdaterAsync kalles ALLTID (ikke bare på førstegangs-opprettelse via `??` over) — se
        // kommentaren ved SeedAsync-toppen for hvorfor: uten dette ville Type/Formal/Innhold aldri
        // slått gjennom mot en Fettutskiller-rad som allerede ble opprettet i en TIDLIGERE kjøring
        // av denne seeden (før denne runden la til disse feltene).
        await tjenesteregister.OppdaterAsync(
            fettutskiller.Id, bergenKommune.Id, fettutskiller.Tittel, fettutskiller.Beskrivelse,
            fettutskiller.KompetentMyndighet, fettutskiller.Output, fettutskiller.Tjenestetype,
            malgruppe: [
                "Restauranter", "Kafeer og konditorier", "Gatekjøkken", "Kantiner", "Cateringvirksomheter",
                "Bakerier", "Matbutikker med steke- eller grillavdeling", "Næringsmiddelindustri og matproduksjon",
            ],
            fettutskiller.Kanaler, fettutskiller.Kostnad, fettutskiller.Behandlingstid, fettutskiller.Kontaktpunkt,
            konsekvensVedBrudd: "Dersom virksomheten ikke oppfyller kravene til installasjon, drift eller " +
                "rapportering, kan kommunen gi pålegg om utbedring og følge opp saken etter gjeldende regelverk.",
            fettutskiller.Sprak, SeedBruker, ct,
            livshendelser: ["Starte og drive en bedrift"], losKlassifisering: null,
            tjenesteomrade: "Avløp, renovasjon og forurensning",
            type: "myndighetsutovelse",
            formal: "Ordningen skal bidra til å beskytte det kommunale avløpsnettet og renseanlegg mot " +
                "problemer som oppstår når fett slippes ut i avløpssystemet. Fettutskilleren skiller ut fett " +
                "fra avløpsvannet før vannet ledes videre til det offentlige avløpsnettet. Dette reduserer " +
                "risikoen for tette rør, driftsforstyrrelser, oversvømmelser og forurensning.",
            innhold: FettutskillerInnhold());

        // ---------- 6. Kodelister (KL-HANDLINGSTYPE/KL-UTFORT-AV/KL-BRUKSOMRAADE/KL-KANAL) ----------
        // Kjøres HER, FØR markørsjekken under — den er idempotent per kodeliste selv (se
        // SeedEnKodelisteAsync), og skal fylles inn selv når handlinger/avhengigheter allerede ble
        // seedet i en TIDLIGERE kjøring (samme grunn som Rettighet-feltene over kjører ubetinget) —
        // en plassering ETTER markørsjekken (der den lå først) betydde den ALDRI kjørte på en
        // database der markøren allerede var satt (bekreftet empirisk 2026-08-20).
        await SeedKodelisterAsync(db, testkommunen.Id, ct);

        // ---------- Markørsjekk (guard) for §3/§4/§5 — handlinger og avhengigheter ----------
        // Se kommentaren ved SeedAsync-toppen: §1/§2 (Rettighet-feltene) kjører alltid, uavhengig av
        // denne markøren — kun handlinger/avhengigheter-opprettelsen under er engangs.
        if (await db.Handlinger.AnyAsync(h => h.Navn == MarkorHandling, ct)) return;

        // ---------- 3. Handlinger under Serveringsbevilling ----------
        await OpprettHandlingHvisNyAsync(handlingregister, testkommunen.Id, serveringsbevilling.Id,
            "Søknad om serveringsbevilling", "soke", "soknad_registrering", "soker",
            kanaler: [new HandlingKanalInput("elektronisk", null)],
            behandlingstid: new HandlingBehandlingstidInput(
                "Senest 60 dager, regnet fra komplett dokumentasjon. Foreløpig svar med frist og klagerett skal gis så raskt som mulig.",
                new HandlingHjemmelInput("serveringsloven", "§ 10")),
            kostnad: new HandlingKostnadInput("Ingen søknadsgebyr. Kommunen kan kreve inntil kr 400 for etablererprøven.",
                [new HandlingHjemmelInput("serveringsloven", "§ 5"), new HandlingHjemmelInput("forskrift om etablererprøve for daglig leder av serveringssted", "§ 7")]),
            vedlegg: [
                new HandlingVedleggInput("Dokumentasjon på bestått etablererprøve", null, new HandlingHjemmelInput("serveringsloven", "§ 8")),
                new HandlingVedleggInput("Skatteattest for den serveringsstedet drives for regning av", "skatteattest", new HandlingHjemmelInput("serveringsloven", "§ 8")),
                new HandlingVedleggInput("Leiekontrakt (hvis kommunen krever det)", null, new HandlingHjemmelInput("serveringsloven", "§ 8")),
                new HandlingVedleggInput("Finansieringsplan (hvis kommunen krever det)", null, new HandlingHjemmelInput("serveringsloven", "§ 8")),
                new HandlingVedleggInput("Driftsbudsjett (hvis kommunen krever det)", null, new HandlingHjemmelInput("serveringsloven", "§ 8")),
                new HandlingVedleggInput("Likviditetsbudsjett (hvis kommunen krever det)", null, new HandlingHjemmelInput("serveringsloven", "§ 8")),
            ],
            veiledningstekst: [
                new HandlingVeiledningstekstInput("Når skal skjemaet brukes?",
                    "Skjemaet brukes når du skal etablere eller overta et serveringssted med mat og/eller drikke. Søknad sendes kommunen der serveringsstedet skal drives, og må være innvilget før virksomheten starter.", null),
                new HandlingVeiledningstekstInput("Hvem skal bruke skjemaet?",
                    "Alle som skal etablere og drive serveringssted med mat og/eller drikke. Krav om serveringsbevilling kan blant annet også gjelde for mobile serveringssteder som foodtruck og matvogn. Det er kommunen der virksomheten drives som vurderer om bevilling kreves i det enkelte tilfellet.", null),
                new HandlingVeiledningstekstInput("Hvorfor skal skjemaet brukes?",
                    "Den som vil gjøre seg næring av å drive serveringssted må ha serveringsbevilling gitt av kommunen.", new HandlingHjemmelInput("serveringsloven", "§ 3")),
                new HandlingVeiledningstekstInput("Mer om skjemaet",
                    "Bygningen må være godkjent av de kommunale bygningsmyndighetene — det må for eksempel foreligge godkjent bruksendring hvis bygningen er regulert til andre formål. Krav til parkeringsplasser må også være oppfylt.",
                    new HandlingHjemmelInput("plan- og bygningsloven", null)),
            ],
            arsaker: null, resultat: new HandlingResultatInput(
                "Serveringsbevillingen registreres i kommunens fagsystem/tjenestekatalog.",
                [new HandlingBevisKanalInput("Bekreftelse fra kommunen"), new HandlingBevisKanalInput("Virksomhetslommebok")]),
            merknad: "I dag Serveringsbevilling selv — fremstår både som Rettigheten og som sin egen 'starte'-handling.", ct);

        await OpprettHandlingHvisNyAsync(handlingregister, testkommunen.Id, serveringsbevilling.Id,
            MarkorHandling, "melde", null, "soker",
            kanaler: [new HandlingKanalInput("annet", null)],
            behandlingstid: new HandlingBehandlingstidInput("Uten ugrunnet opphold.", new HandlingHjemmelInput("serveringsloven", "§ 14")),
            kostnad: null,
            vedlegg: [new HandlingVedleggInput("Dokumentasjon som kreves etter § 8", null, new HandlingHjemmelInput("serveringsloven", "§ 14, jf. § 8"))],
            veiledningstekst: [new HandlingVeiledningstekstInput("Hva utløser meldeplikt?",
                "Skifte av daglig leder, andre endringer i personkretsen nevnt i § 6 jf. § 7, eller innstilt drift av serveringsstedet.",
                new HandlingHjemmelInput("serveringsloven", "§ 14"))],
            arsaker: null, resultat: null,
            merknad: "Unnlatt melding kan straffes med bøter (§ 21 bokstav e).", ct);

        await OpprettHandlingHvisNyAsync(handlingregister, testkommunen.Id, serveringsbevilling.Id,
            "Endring av eiere eller eierandeler", "melde", null, "soker",
            null, null, null, null, null, null, null,
            merknad: "Samme § 14-meldeplikt som 'Melding om endringer ved serveringsstedet' — kandidat for sammenslåing, ikke gjort her.", ct);

        await OpprettHandlingHvisNyAsync(handlingregister, testkommunen.Id, serveringsbevilling.Id,
            "Eierskifte og drift i overgangsperioden på tidligere eiers bevilling", "melde", null, "soker",
            kanaler: null,
            behandlingstid: new HandlingBehandlingstidInput(
                "Søknad om ny bevilling innen 30 dager etter avtale om overdragelse. Driften kan fortsette inntil søknaden er avgjort.",
                new HandlingHjemmelInput("serveringsloven", "§ 22")),
            kostnad: null, vedlegg: null, veiledningstekst: null, arsaker: null, resultat: null, merknad: null, ct: ct);

        await OpprettHandlingHvisNyAsync(handlingregister, testkommunen.Id, serveringsbevilling.Id,
            "Oppsigelse av bevilling", "avslutte", null, "soker",
            kanaler: null,
            behandlingstid: new HandlingBehandlingstidInput(
                "Meldeplikt ved innstilt drift (§ 14 c). Bevillingen faller uansett bort ved mer enn ett års driftsstans.",
                new HandlingHjemmelInput("serveringsloven", "§ 14, § 25")),
            kostnad: null, vedlegg: null, veiledningstekst: null, arsaker: null, resultat: null, merknad: null, ct: ct);

        await OpprettHandlingHvisNyAsync(handlingregister, testkommunen.Id, serveringsbevilling.Id,
            "Kontroller av salgs- og skjenkesteder", "kontrolleres", null, "forvaltning",
            null, null, null, null, null, null, null,
            merknad: "Forvaltningens kontroll, ikke søkers handling. Konkret tilsynshjemmel ikke bekreftet — ikke gjettet.", ct);

        await OpprettHandlingHvisNyAsync(handlingregister, testkommunen.Id, serveringsbevilling.Id,
            "Tilbaketrekking eller bortfall av bevilling", "annet", null, "forvaltning",
            null, null, null, null, null,
            arsaker: [
                new HandlingArsakInput("Brudd på regelverket", new HandlingHjemmelInput("serveringsloven", "§ 21")),
                new HandlingArsakInput("Konkurs — bevillingshaver går konkurs og konkursboet velger ikke å fortsette driften", new HandlingHjemmelInput("serveringsloven", "§ 23")),
                new HandlingArsakInput("Manglende ny søknad ved overdragelse innen 30 dager", new HandlingHjemmelInput("serveringsloven", "§ 22")),
                new HandlingArsakInput("Dødsfall — bevillingshaver dør og dødsboet velger ikke å fortsette driften", new HandlingHjemmelInput("serveringsloven", "§ 24")),
                new HandlingArsakInput("Innstilt eller manglende drift i mer enn ett år", new HandlingHjemmelInput("serveringsloven", "§ 25")),
            ],
            resultat: null,
            merknad: "Utvidet fra den opprinnelige 'Konsekvenser ved brudd på regelverket' — loven har flere årsaker til bortfall enn bare regelbrudd.", ct);

        await OpprettHandlingHvisNyAsync(handlingregister, testkommunen.Id, serveringsbevilling.Id,
            "Melding fra konkursbo/dødsbo om videre drift", "melde", null, "tredjepart",
            kanaler: null,
            behandlingstid: new HandlingBehandlingstidInput(
                "Uten ugrunnet opphold, hvis boet velger å fortsette driften.",
                new HandlingHjemmelInput("serveringsloven", "§ 23 (konkurs), § 24 (dødsfall)")),
            kostnad: null, vedlegg: null, veiledningstekst: null, arsaker: null, resultat: null,
            merknad: "§§ 5 og 6 gjelder ikke i perioden konkursboet driver stedet videre (§ 23); § 5 og deler av § 6 gjelder heller ikke for dødsbo (§ 24).", ct: ct);

        await OpprettHandlingHvisNyAsync(handlingregister, testkommunen.Id, serveringsbevilling.Id,
            "Klage på vedtak", "klage", null, "soker",
            kanaler: null,
            behandlingstid: new HandlingBehandlingstidInput(null, new HandlingHjemmelInput("serveringsloven", "§ 27")),
            kostnad: null, vedlegg: null,
            veiledningstekst: [new HandlingVeiledningstekstInput("Hvem behandler klagen?", "Kommunens vedtak kan påklages til statsforvalteren.",
                new HandlingHjemmelInput("serveringsloven", "§ 27"))],
            arsaker: null, resultat: null, merknad: null, ct: ct);

        // ---------- 4. Handlinger under Fettutskiller ----------
        await OpprettHandlingHvisNyAsync(handlingregister, bergenKommune.Id, fettutskiller.Id,
            "Registrering/søknad om fettutskiller", "registrere", null, "soker",
            null, null, null, null, null, null, null,
            merknad: "Sendes normalt før virksomheten starter opp eller før påslipp begynner.", ct);

        await OpprettHandlingHvisNyAsync(handlingregister, bergenKommune.Id, fettutskiller.Id,
            "Innsending av ferdigmelding/sluttdokumentasjon", "ettersende_dokumentasjon", null, "soker",
            null, null, null, null, null, null, null,
            merknad: "Dokumentasjon på dimensjonering, tømming og kontroll etter installasjon.", ct);

        await OpprettHandlingHvisNyAsync(handlingregister, bergenKommune.Id, fettutskiller.Id,
            "Årlig rapportering av tømming, vedlikehold og kontroll", "rapportere", null, "soker",
            kanaler: null,
            behandlingstid: new HandlingBehandlingstidInput("Mange kommuner krever dette innen 1. mars hvert år.", null),
            kostnad: null, vedlegg: null, veiledningstekst: null, arsaker: null,
            resultat: new HandlingResultatInput("Rapportering registreres hos kommunen som dokumentasjon på at fettutskilleren er fulgt opp.",
                [new HandlingBevisKanalInput("Bekreftelse fra kommunen")]),
            merknad: null, ct: ct);

        await OpprettHandlingHvisNyAsync(handlingregister, bergenKommune.Id, fettutskiller.Id,
            "Tilsyn og kontroll av fettutskilleranlegget", "kontrolleres", null, "forvaltning",
            null, null, null, null, null, null, null,
            merknad: "Forvaltningens handling, ikke virksomhetens egen.", ct);

        await OpprettHandlingHvisNyAsync(handlingregister, bergenKommune.Id, fettutskiller.Id,
            "Pålegg om utbedring ved manglende etterlevelse", "annet", null, "forvaltning",
            null, null, null, null, null, null, null,
            merknad: "Egentlig en konsekvens forvaltningen utløser, ikke en handling virksomheten selv utfører.", ct);

        // ---------- 5. Nye avhengigheter på Serveringsbevilling ----------
        try
        {
            await avhengighetregister.OpprettAsync(
                testkommunen.Id, serveringsbevilling.Id, fettutskiller.Id, "avhengig_av", null,
                "Kan kreves — gjelder virksomheter som produserer, tilbereder eller behandler mat og har påslipp av " +
                "fettholdig avløpsvann til kommunalt avløpsnett (f.eks. restauranter, kafeer, gatekjøkken, kantiner, " +
                "cateringvirksomheter, bakerier, matbutikker med steke- eller grillavdeling, næringsmiddelindustri). " +
                "Gjelder normalt IKKE et serveringssted uten egen mathåndtering.",
                SeedBruker, ct: ct);
        }
        catch (ArgumentException) { /* allerede opprettet ved gjentatt oppstart */ }

        try
        {
            await avhengighetregister.OpprettAsync(
                testkommunen.Id, serveringsbevilling.Id, null, "kan_miste", null,
                "Gjelder bare hvis bevillingshaver går konkurs — se handlinger 'Tilbaketrekking eller bortfall av " +
                "bevilling' og 'Melding fra konkursbo/dødsbo om videre drift' (serveringsloven § 23).",
                SeedBruker, tilOrganisasjonsnummer: "974760673",
                tilNavn: "Kunngjøring av konkurs (Konkursregisteret, Brønnøysundregistrene)", ct: ct);
        }
        catch (ArgumentException) { /* allerede opprettet ved gjentatt oppstart */ }
    }

    private static async Task OpprettHandlingHvisNyAsync(
        HandlingregisterTjeneste register, Guid virksomhetId, Guid tjenesteId, string navn, string handlingstype,
        string? bruksomraade, string? utfortAv, IReadOnlyList<HandlingKanalInput>? kanaler,
        HandlingBehandlingstidInput? behandlingstid, HandlingKostnadInput? kostnad, IReadOnlyList<HandlingVedleggInput>? vedlegg,
        IReadOnlyList<HandlingVeiledningstekstInput>? veiledningstekst, IReadOnlyList<HandlingArsakInput>? arsaker,
        HandlingResultatInput? resultat, string? merknad, CancellationToken ct)
    {
        await register.OpprettAsync(
            virksomhetId, tjenesteId, navn, handlingstype, bruksomraade, utfortAv,
            kanaler, behandlingstid, kostnad, vedlegg, veiledningstekst, arsaker, resultat, merknad, SeedBruker, ct);
    }

    /// <summary>Ordrett fra serveringsbevilling-modell-forslag.json sin rettigheter[0].innhold — kun
    /// 【N-hash】-citation-markører fjernet (samme rensing modellfilen selv beskriver).</summary>
    private static TjenesteInnholdInput ServeringsbevillingInnhold() => new(
        TidspunktOgFrister: "Serveringsbevilling må være innvilget før serveringsvirksomheten starter. Ved " +
            "overdragelse av et eksisterende serveringssted må ny eier søke om egen serveringsbevilling innen " +
            "30 dager etter at overdragelsesavtalen ble inngått. Kommunen behandler søknaden etter at nødvendig " +
            "dokumentasjon er mottatt. Saksbehandlingstiden kan variere mellom kommuner.",
        InnsenderOgTilgang: new TjenesteInnsenderInput(
            ["Innehaver av enkeltpersonforetak", "Styreleder eller daglig leder i virksomheten",
                "Person med nødvendig fullmakt", "Person med relevant rolle i Altinn"],
            "Normalt elektronisk ID."),
        Vedlegg: [
            "Dokumentasjon på bestått etablererprøve for daglig leder", "Firmaattest", "Dokumentasjon på eierforhold",
            "Eventuelle leie- eller adkomstdokumenter", "Overdragelsesavtale ved eierskifte",
            "Skatteattest for virksomheten og for hver person med vesentlig innflytelse over virksomheten",
        ],
        VedleggMerknad: "Kommunen kan ved behov be om ytterligere dokumentasjon, som finansieringsplan, " +
            "driftsbudsjett eller andre opplysninger som er nødvendige for å behandle søknaden.",
        OpplysningerSomSkalSendesInn: [
            "Virksomhetens navn og organisasjonsnummer", "Forretningsadresse og serveringsstedets beliggenhet",
            "Daglig leder", "Eiere og personer med vesentlig innflytelse over virksomheten",
            "Type serveringsvirksomhet", "Eventuell overdragelse av eksisterende virksomhet",
        ],
        OpplysningerMerknad: null,
        VeiledningOgUtfylling: [
            "Avklare at lokalene kan brukes til serveringsformål.", "Registrere virksomheten i relevante offentlige registre.",
            "Sørge for at daglig leder har bestått etablererprøven.", "Samle nødvendig dokumentasjon og vedlegg.",
        ],
        VeiledningMerknad: "Alle opplysninger må være korrekte og fullstendige. Mangelfulle søknader kan føre til lengre behandlingstid.",
        InnsendingOgOppfolging: new TjenesteInnsendingInput(
            "Søknaden sendes elektronisk via kommunens digitale løsning eller Altinn.",
            ["Kommunen kontrollerer dokumentasjonen", "Relevante myndigheter kan bli bedt om uttalelser",
                "Kommunen vurderer om vilkårene for bevilling er oppfylt", "Vedtak sendes til søker"],
            null),
        KontaktOgHjelp: new TjenesteKontaktInput(
            "Har du spørsmål om søknaden eller regelverket, kan du kontakte kommunen.",
            ["Krav til serveringsbevilling", "Dokumentasjon og vedlegg", "Etablererprøven", "Saksbehandling og status i saken"]),
        HvaRettighetenInnebarer: new TjenesteHvaRettighetenInnebarerInput(
            Innledning: "En serveringsbevilling gir virksomheten rett til å etablere og drive et serveringssted i " +
                "samsvar med serveringsloven. Bevillingen gjelder for den virksomheten og det serveringsstedet som " +
                "er oppgitt i søknaden.",
            Varighet: "Serveringsbevillingen gjelder inntil videre, så lenge virksomheten oppfyller kravene i " +
                "regelverket og forholdene som bevillingen bygger på ikke endres vesentlig. Ved overdragelse av " +
                "virksomheten må ny eier søke om egen serveringsbevilling.",
            Plikter: [
                "Sørge for at serveringsstedet drives i samsvar med gjeldende regelverk",
                "Ha en daglig leder som oppfyller lovens krav",
                "Melde fra til kommunen om relevante endringer i virksomheten",
                "Opprettholde kravene til vandel for bevillingshaver, daglig leder og personer med vesentlig innflytelse over virksomheten",
                "Gi nødvendige opplysninger til kommunen ved forespørsel eller kontroll",
            ],
            EndringerIVirksomheten: new TjenesteEndringerInput(
                "Virksomheten har plikt til å melde fra til kommunen om forhold som kan ha betydning for bevillingen.",
                ["Skifte av daglig leder", "Endringer i eierskap eller selskapsstruktur",
                    "Endringer i personkretsen med vesentlig innflytelse over virksomheten", "Andre vesentlige endringer i driften"]),
            KontrollOgTilsyn: "Kommunen og andre offentlige myndigheter kan føre kontroll med at vilkårene for " +
                "bevillingen overholdes. Virksomheten plikter å medvirke til kontroll og gi nødvendige opplysninger når dette kreves.",
            AvgrensningMerknad: "Serveringsbevilling gir rett til å servere mat og alkoholfri drikke. Dersom " +
                "virksomheten ønsker å servere alkohol, må det i tillegg søkes om egen skjenkebevilling etter alkoholloven."));

    /// <summary>Ordrett fra serveringsbevilling-modell-forslag.json sin rettigheter[1].innhold (§3-§11).</summary>
    private static TjenesteInnholdInput FettutskillerInnhold() => new(
        TidspunktOgFrister: "Fettutskiller skal normalt være installert og registrert før virksomheten starter " +
            "opp eller før påslipp til offentlig avløpsnett begynner. Virksomheten kan også ha plikt til å sende " +
            "inn rapportering og dokumentasjon innen frister fastsatt av kommunen. Mange kommuner krever årlig " +
            "rapportering innen 1. mars.",
        InnsenderOgTilgang: new TjenesteInnsenderInput(
            ["Virksomhetens eier eller ansvarlige representant", "Ansvarlig søker eller rørlegger ved installasjon",
                "Annen person med nødvendig fullmakt"],
            "Normalt elektronisk ID gjennom kommunens digitale løsninger."),
        Vedlegg: [
            "Situasjonsplan eller ledningskart", "Tegninger av avløpsanlegg og fettutskiller",
            "Tekniske spesifikasjoner for fettutskilleren", "Dokumentasjon på dimensjonering",
            "Ferdigmelding eller sluttdokumentasjon", "Dokumentasjon på tømming og kontroll",
        ],
        VedleggMerknad: "Kravene kan variere mellom kommuner.",
        OpplysningerSomSkalSendesInn: [
            "Virksomhetens navn og organisasjonsnummer", "Adresse og eiendom", "Type virksomhet",
            "Forventet påslipp av fettholdig avløpsvann", "Opplysninger om fettutskilleren", "Kontaktperson",
        ],
        OpplysningerMerknad: "Kommunen bruker opplysningene til å vurdere om kravene til påslipp og avløpsanlegg er oppfylt.",
        VeiledningOgUtfylling: [
            "Avklare om virksomheten omfattes av krav om fettutskiller.", "Sørge for korrekt dimensjonering av anlegget.",
            "Innhente nødvendig teknisk dokumentasjon.", "Avklare eventuelle lokale krav med kommunen.",
        ],
        VeiledningMerknad: "Mangelfull dokumentasjon kan føre til lengre saksbehandlingstid.",
        InnsendingOgOppfolging: new TjenesteInnsendingInput(
            null,
            ["Dokumentasjonen kontrolleres", "Kommunen vurderer om kravene er oppfylt",
                "Virksomheten kan få påslippskrav eller andre vilkår", "Kommunen kan gjennomføre tilsyn og kontroll"],
            "Virksomheten kan bli bedt om å sende inn tilleggsopplysninger ved behov."),
        KontaktOgHjelp: new TjenesteKontaktInput(
            "Dersom du har spørsmål om krav til fettutskiller, registrering, drift eller rapportering, kan du kontakte kommunen.",
            ["Krav til fettutskiller", "Tekniske løsninger", "Rapportering", "Kontroll og tilsyn", "Påslippskrav"]),
        HvaRettighetenInnebarer: new TjenesteHvaRettighetenInnebarerInput(
            Innledning: null, Varighet: null, Plikter: [], EndringerIVirksomheten: null, KontrollOgTilsyn: null, AvgrensningMerknad: null,
            KravTilDrift: "Virksomheten er ansvarlig for at fettutskilleren fungerer som forutsatt og vedlikeholdes " +
                "i henhold til kommunens krav. Fettutskilleren skal tømmes og rengjøres regelmessig slik at den " +
                "opprettholder ønsket renseeffekt.",
            TommeavtaleOgKontroll: "Det skal normalt foreligge en gyldig avtale om tømming av fettutskilleren. " +
                "Kommunen kan stille krav til tømmefrekvens, tilstandskontroll og dokumentasjon på utført vedlikehold.",
            Rapportering: "Virksomheten kan ha plikt til å sende inn rapporter om tømming, vedlikehold og kontroll " +
                "av fettutskilleren. Kommunen kan be om dokumentasjon som viser at kravene overholdes."));

    /// <summary>
    /// KL-HANDLINGSTYPE/KL-UTFORT-AV/KL-BRUKSOMRAADE/KL-KANAL som ekte, redigerbare KodelisteEntitet-
    /// rader (Type="teknisk") — se §6-kommentaren ved kallstedet. Idempotent per kodeliste (sjekker
    /// om koden allerede finnes før den opprettes), IKKE gatet av MarkorHandling-sjekken.
    /// </summary>
    private static async Task SeedKodelisterAsync(RegelIdeDbContext db, Guid virksomhetId, CancellationToken ct)
    {
        var kodelisteregister = new KodelisteregisterTjeneste(db);

        await SeedEnKodelisteAsync(db, kodelisteregister, virksomhetId, "KL-HANDLINGSTYPE", "Handlingstype",
            HandlingregisterTjeneste.GyldigeHandlingstyper.Select(k => (k, k)), ct);
        await SeedEnKodelisteAsync(db, kodelisteregister, virksomhetId, "KL-UTFORT-AV", "Utført av",
            HandlingregisterTjeneste.GyldigeUtfortAv.Select(k => (k, k)), ct);
        await SeedEnKodelisteAsync(db, kodelisteregister, virksomhetId, "KL-BRUKSOMRAADE", "Bruksområde",
            [("soknad_registrering", "Søknad / registrering"), ("periodisk_rapportering", "Periodisk rapportering"),
                ("hendelsesrapportering", "Hendelsesrapportering")], ct);
        await SeedEnKodelisteAsync(db, kodelisteregister, virksomhetId, "KL-KANAL", "Kanal",
            [("elektronisk", "Elektronisk"), ("papir", "Papir"), ("begge", "Både elektronisk og på papir"),
                ("annet", "Annet (f.eks. meldeplikt uten skjema)")], ct);
    }

    private static async Task SeedEnKodelisteAsync(
        RegelIdeDbContext db, KodelisteregisterTjeneste register, Guid virksomhetId, string kode, string navn,
        IEnumerable<(string Kode, string Term)> koder, CancellationToken ct)
    {
        if (await db.Kodelister.AnyAsync(k => k.Kode == kode, ct)) return;

        var kodeliste = await register.OpprettAsync(
            virksomhetId, kode, navn, "teknisk", juridiskGrunnlagEid: null, eksternKildeUri: null,
            eksternKildeVersjon: null, SeedBruker, ct);
        foreach (var (verdiKode, term) in koder)
        {
            await register.LeggTilKodeAsync(kodeliste.Id, verdiKode, term, definisjon: null, gyldigFra: null, gyldigTil: null, ct);
        }
    }
}
