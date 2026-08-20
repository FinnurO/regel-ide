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
        if (await db.Handlinger.AnyAsync(h => h.Navn == MarkorHandling, ct)) return;

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
            livshendelser: ["Starte og drive en bedrift"], losKlassifisering: null, tjenesteomrade: "Næring, salg og servering");

        // ---------- 2. Fettutskiller — ny, ekte Rettighet, eid av Bergen kommune ----------
        // Bergen kommune, IKKE Testkommunen — det er den faktiske eieren av den allerede
        // eksisterende "Krav om fettutskiller"-rettskilden (Brukerveiledning-doctype), bekreftet
        // 2026-08-20 mot /api/rettskilder. Dette gjør avhengigheten under til en reell,
        // kryss-tenant-forankret kobling, ikke en plassholder.
        var fettutskiller = await db.Tjenester.FirstOrDefaultAsync(
                t => t.Tittel == "Krav om fettutskiller" && t.VirksomhetId == bergenKommune.Id && t.Entitetsstatus == "gjeldende", ct)
            ?? await tjenesteregister.OpprettAsync(
                bergenKommune.Id, "Krav om fettutskiller",
                beskrivelse: "Ordningen skal bidra til å beskytte det kommunale avløpsnettet og renseanlegg mot " +
                    "problemer som oppstår når fett slippes ut i avløpssystemet. Fettutskilleren skiller ut fett fra " +
                    "avløpsvannet før vannet ledes videre til det offentlige avløpsnettet.",
                kompetentMyndighet: "Bergen kommune", output: null, tjenestetype: "Enkeltvedtak",
                malgruppe: [
                    "Restauranter", "Kafeer og konditorier", "Gatekjøkken", "Kantiner", "Cateringvirksomheter",
                    "Bakerier", "Matbutikker med steke- eller grillavdeling", "Næringsmiddelindustri og matproduksjon",
                ],
                kanaler: null, kostnad: null, behandlingstid: null, kontaktpunkt: null,
                konsekvensVedBrudd: "Dersom virksomheten ikke oppfyller kravene til installasjon, drift eller " +
                    "rapportering, kan kommunen gi pålegg om utbedring og følge opp saken etter gjeldende regelverk.",
                sprak: ["nb"], opprettetAv: SeedBruker, ct,
                livshendelser: ["Starte og drive en bedrift"], losKlassifisering: null,
                tjenesteomrade: "Avløp, renovasjon og forurensning");

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
}
