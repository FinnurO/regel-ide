using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Oppdagelsesmekanismen (docs/13-backlog.md §9) — ren tekstanalyse (regex), ALDRI KI/LLM (eksplisitt
/// instruks fra Johann, ikke bare en foretrukket løsning). Komplementær til
/// <see cref="VirksomhetKandidatSveipTjeneste"/>, som er en BEKREFTELSES-mekanisme (krever en allerede
/// kjent navneform-<see cref="BegrepEntitet"/>-rad og leter etter FLERE forekomster av DEN kjente
/// strengen). Denne klassen foreslår derimot HELT NYE kandidatnavn — mønstre, ikke kjente strenger —
/// til <see cref="NavnekandidatEntitet"/>-køen (se den klassens kommentar for hvorfor en egen tabell).
/// <para>
/// Én klasse dekker både selve sveipet og selve køen (motsatt av virksomhet-kandidat-parets to klasser)
/// — denne køen har ingen separat gjenbruker av bare "opprett/lister"-delen (VirksomhetKandidatTjeneste
/// brukes bl.a. direkte av godkjennings-flyten i TekstTaggTjeneste-sammenheng), så en splitt ga ikke
/// samme gevinst her.
/// </para>
/// <para>
/// <b>[Restrukturert, 2026-09-03 — Johanns eksplisitte instruks etter tre patch-PR-er (#188/#189) på
/// den gamle arkitekturen]</b> Den gamle arkitekturen hadde TRE separate mønstre (suffiksmønster +
/// flerords-mønster + faste-gruppe-mønster), der suffiksmønsteret var en hånd-kuratert, stadig voksende
/// ordliste (<c>Suffikser</c>) som produserte gjentatte runder med falske positiver/negativer
/// ("regelverket" slapp gjennom, "beliggenheten"/"byggverket" ble nye falske positiver fra et suffiks
/// nettopp lagt til, "Nasjonalarkivet"/"merkenemnd" ble oversett til et nytt suffiks ble lagt til) — et
/// reelt whack-a-mole-mønster. Johanns instruks, verbatim: «Jeg vil at du tar alle ting som starter med
/// stor bokstav, unntatt som første ord, samler de opp og sjekker mot SSR og SNL. hvis treff i en tab
/// og hvis hva som ble avvist i en annen tab» — og videre, presisert: «kommer i tillegg til faste
/// mønstre/roller. men dropp suffiks, men behold flerords logikken, vi må sende over hele navnet.»
/// Konkret betyr dette:
/// <list type="number">
/// <item><see cref="FasteGruppeMønster"/>/<see cref="FasteRollesubstantiv"/> — UENDRET. Lukket,
/// hånd-kuratert liste (Kongen, Stortinget, bøyningsformer av kommune/fylkeskommune/departement/
/// statsforvalter) → ALLTID <c>"gruppe"</c>, aldri sendt til SNL/SSR.</item>
/// <item>Flerords-mønsteret (<see cref="InstitusjonsordMønster"/>/<see cref="FinnEgennavnForanInstitusjonsord"/>/
/// <see cref="ErFlerordsKontekstTillatt"/>) — UENDRET utløser-/fangelogikk (fanger allerede HELE navnet,
/// f.eks. "Statens vegvesen", "Møre og Romsdal fylkeskommune"). Det som ER nytt: <c>"virksomhet"</c>-
/// treffet herfra går nå gjennom SAMME samle-så-klassifiser-pipeline (se punkt 4-5 under) som resten av
/// <c>"virksomhet"</c>-treff, i stedet for å klassifiseres ETT ETT per posisjon slik det gamle
/// <c>SveipAsync</c> gjorde.</item>
/// <item><b>Suffiksmønsteret ER SLETTET</b> — <c>SuffiksMønster</c>, <c>Suffikser</c>,
/// <c>VerketDenyliste</c>, <c>ErSuffiksAvledetGruppe</c> finnes ikke lenger noe sted i denne klassen.
/// En STOR forbokstav midt i en setning (uansett suffiks) blir i stedet fanget av det brede
/// "stor bokstav"-mønsteret under (punkt 4) og strukturelt validert mot SNL/SSR i stedet for en
/// hånd-vedlikeholdt, evig ufullstendig ordliste — samme strukturelle løsning erstatter behovet for
/// <c>VerketDenyliste</c> også: "regelverket"/"lovverket" m.fl. (LITEN forbokstav) produserer nå INGEN
/// kandidat i det hele tatt (verken suffiksmønsteret som fanget dem, eller det brede mønsteret, som
/// KUN trigges på stor forbokstav) — strukturelt umulig å reprodusere, ikke lenger avhengig av en
/// denyliste å holde oppdatert.</item>
/// <item>Det tidligere separate, sjeldent-kjørte <see cref="SveipStorBokstavAsync"/>-endepunktet
/// (docs/31, opprinnelig scopet forsiktig til et lite delsett av korpuset — se historisk merknad ved
/// <see cref="SveipAsync"/>) er nå SLÅTT SAMMEN inn i <see cref="SveipAsync"/>, som primær-, standard-
/// og eneste sveipemetode. Ett sveip, én metode, som dekker faste mønstre/roller + flerords-mønsteret +
/// det brede "stor bokstav"-mønsteret sammen. docs/31 §6s opprinnelige forsiktighet (kjør først mot et
/// avgrenset delsett, siden ingen av de eksterne API-ene har dokumentert ratelimit) er ERSTATTET, ikke
/// fjernet stille: kostnaden er nå håndtert strukturelt via samle-så-klassifiser (punkt 5 under) —
/// ANTALL unike eksterne oppslag per sveip er nå <c>antall unike navn</c>, ikke <c>antall forekomster</c>.</item>
/// <item><b>Samle-så-klassifiser, ikke klassifiser-per-posisjon</b> — den egentlige arkitekturendringen.
/// <see cref="SveipAsync"/> er nå strukturert i tre faser: (a) en REN, rask fase uten nettverkskall som
/// skanner alle sveipbare noder og samler ALLE rå kandidatforekomster (både faste-gruppe-, flerords- og
/// stor-bokstav-mønster-treff), og anvender den EKSISTERENDE dedup-/idempotens-logikken (posisjonsbasert
/// per <c>(RettskildeId, NodeEid, StartOffset)</c>, termbasert for <c>"gruppe"</c>, allerede-dekket-av-
/// Begrep-filtrering) FØR noe sendes til klassifisering; (b) en fase som grupperer alt som trenger
/// klassifisering (<c>"virksomhet"</c>-treff, ALDRI <c>"gruppe"</c>) på NORMALISERT (case-insensitiv)
/// tekst og kaller <see cref="EksternNavneoppslagTjeneste"/> NØYAKTIG ÉN gang per unikt navn i DENNE
/// kjøringen (et internt <c>Dictionary&lt;string, bool&gt;</c> — i tillegg til den eksisterende
/// databasecachen, som forhindrer duplikate HTTP-kall på TVERS av kjøringer, forhindrer dette duplikate
/// CACHE-oppslag INNENFOR samme kjøring for et navn nevnt mange ganger); (c) en fase som materialiserer
/// én <see cref="NavnekandidatEntitet"/>-rad per samlet forekomst, med status avgjort av navnets
/// klassifiseringsresultat fra fase (b). Se selve <see cref="SveipAsync"/>s metodekommentar for detaljene.</item>
/// <item><b>To-utfalls klassifisering, ikke tre</b> — en reell, bevisst endring i selve
/// klassifiseringen (<see cref="KlassifiserAsync"/>, tidligere <c>BeholdSomKandidatAsync</c>). Den
/// gamle kjeden hadde en "ingen gjettet fallback"-standard der et navn UKJENT i BÅDE SNL og SSR ble
/// BEHOLDT som en lav-tillit <c>"Venter"</c>-kandidat — Johanns instruks beskriver eksplisitt KUN to
/// utfall («hvis treff i en tab og hvis hva som ble avvist i en annen tab»), ikke tre. SNL-bekreftet
/// institusjon ELLER SSR-bekreftet stedsnavn MED institusjonsord rett etter → <c>"Venter"</c> (en ekte
/// kandidat, klar for saksbehandler-vurdering, akkurat som før). ALT ANNET — SSR-bekreftet stedsnavn
/// UTEN institusjonsord rett etter, ELLER ukjent i begge kildene → <c>"Avvist"</c> DIREKTE ved
/// opprettelse. Avgjørende: raden opprettes FORTSATT (synlig, revisjonsbar, filtrerbar i UI-et via
/// eksisterende status-filter) — den forkastes ikke stille lenger slik den gamle SSR-uten-institusjonsord-
/// grenen gjorde (som ikke opprettet noen rad i det hele tatt).</item>
/// </list>
/// </para>
/// <para>
/// <b>[Ny, kodegjennomgang 2026-08-30] Normalisering før lagring — KUN <c>"gruppe"</c>:</b> for
/// <c>"gruppe"</c>-treff er selve store/små bokstaver-formen IKKE del av identiteten (en gruppe er per
/// definisjon ikke et egennavn — "statsforvalteren" og "Statsforvalteren" er samme gruppe, kun ulik
/// forbokstav fordi den ene tilfeldigvis sto ved en setningsstart). Bekreftet i live data: 68
/// forekomster av "statsforvalteren" og 45 av "Statsforvalteren" ga tidligere separate kandidater, ren
/// posisjonell idempotens fanget aldri opp at det var samme term. Løsning: <see cref="SveipAsync"/>
/// folder <c>"gruppe"</c>-treffets tekst til små bokstaver (<see cref="string.ToLowerInvariant"/>) FØR
/// den brukes som dedup-nøkkel og FØR den lagres som <see cref="NavnekandidatEntitet.ForeslattTekst"/>.
/// <c>"virksomhet"</c>-treff (inkl. flerords- og stor-bokstav-mønsteret) er IKKE del av denne
/// normaliseringen — der ER store/små bokstaver et reelt signal (et egennavn skal beholde sin faktiske
/// stavemåte), så disse beholder rå tekst uendret.
/// </para>
/// <para>
/// <b>[Ny, kodegjennomgang 2026-08-30] Term-basert dedup, i tillegg til posisjonell (KUN <c>"gruppe"</c>):</b>
/// idempotens var tidligere REN posisjon (<c>RettskildeId</c>, <c>NodeEid</c>, <c>StartOffset</c>) — to
/// ulike posisjoner med (etter normalisering) SAMME tekst i SAMME rettskilde ga tidligere to separate
/// <see cref="NavnekandidatEntitet"/>-rader (nettopp "statsforvalteren"/"Statsforvalteren"-caset over).
/// Dette er en reell arkitekturendring, ikke bare en bugfiks: <see cref="SveipAsync"/> sjekker nå, FØR
/// <see cref="OpprettEllerFinnAsync"/> kalles, om det ALLEREDE finnes en <c>"gruppe"</c>-kandidatrad
/// (uansett status — Venter/Godkjent/Avvist — og uansett tekstposisjon) med samme normaliserte tekst
/// for samme <c>RettskildeId</c> — samme prinsipp som den eksisterende "alleredeDekket mot godkjent
/// Begrep"-filtreringen under, nå utvidet til Å OGSÅ dekke ikke-godkjente kandidater. Uten dette ville
/// normaliseringen over kun forhindret NYE duplikater fra ETT sveip (samme treff, samme kjøring), ikke
/// duplikater på TVERS av sveip/posisjoner — som var selve det bekreftede problemet. Kun <c>"gruppe"</c>,
/// av samme grunn som normaliseringen over er scopet dit (<c>"virksomhet"</c> har ingen normalisert
/// term å slå opp mot for BEGREP-dekning — case er signal, ikke støy — der gjelder fortsatt ren
/// posisjonell idempotens for selve RADENE, selv om selve KLASSIFISERINGEN nå er termbasert-memoisert
/// innenfor ett sveip, se punkt 5 over).
/// </para>
/// <para>
/// <b>Kjøres KUN mot allerede importerte rettskilde-noder</b> — samme datakilde som
/// <see cref="VirksomhetKandidatSveipTjeneste"/> (<c>Entitetsstatus == "gjeldende" &amp;&amp; !Opphevet</c>),
/// IKKE en ny, live skraping av Lovdata. Dekningen er derfor begrenset til det som faktisk ER importert
/// — en reell begrensning, ikke noe denne klassen later som er komplett.
/// </para>
/// <para>
/// <b>[Rettet, kodegjennomgang 2026-08-30]</b> Sveipet er scopet til KUN delt/nasjonal, gjeldende
/// rettskilde (<c>Rettskilde.VirksomhetId == null &amp;&amp; Entitetsstatus == "gjeldende"</c>) — en
/// tidligere versjon søkte uskjermet gjennom ALLE virksomheters rettskilder (inkl. private/lokale) og
/// eksponerte tekstutdrag derfra til enhver bruker, samme klasse kryssvirksomhet-lekkasje som allerede
/// ble funnet og fikset én gang i <see cref="VirksomhetKandidatSveipTjeneste"/> (Agder/Bergen,
/// 2026-08-22). Dette er ikke en per-virksomhet-scoping (ingen <c>virksomhetId</c>-parameter finnes her,
/// med vilje — «oppdag nye navn i lovkorpuset» er et generelt, ikke brukerspesifikt søk), men en
/// synlighetsgrense: kun det som uansett er delt/nasjonalt innhold sveipes. <paramref name="rettskildeId"/>
/// i <see cref="SveipAsync"/> er en valgfri innsnevring til ÉN slik delt rettskilde — å oppgi en
/// virksomhets private rettskilde der kaster nå en tydelig feil i stedet for å sveipe den.
/// </para>
/// <para>
/// <b>Filtrering av allerede DEKKEDE treff</b> (docs/13-backlog.md §9): et treff som samsvarer
/// case-insensitivt med <see cref="BegrepEntitet.Term"/> til en eksisterende, gjeldende
/// <see cref="BegrepEntitet"/>-rad skal IKKE gi en ny kandidat — poenget er å oppdage NYE navn, ikke
/// duplisere det <see cref="VirksomhetKandidatSveipTjeneste"/> allerede finner/kan finne. Scopet ulikt
/// per kategori, siden identiteten er ulik (docs/20 §2.3 vs. §2.4): et <c>"virksomhet"</c>-treff sjekkes
/// mot ALLE eksisterende virksomhet-navneformer (globalt delt, uansett rettskilde) — uansett hvilket av
/// de to mønstrene (flerords eller stor-bokstav) som fant det — et <c>"gruppe"</c>-treff sjekkes kun mot
/// gruppebegrep for NØYAKTIG DENNE rettskilden (gruppebegrepets identitet er <c>(Term, LovkildeId)</c>
/// sammen, samme gruppenavn i en annen lov er en annen rad og dekker ikke dette treffet).
/// </para>
/// </summary>
public sealed class NavnekandidatOppdagelseTjeneste(
    RegelIdeDbContext db, VirksomhetsbegrepTjeneste virksomhetsbegrep,
    TekstTaggTjeneste tekstTaggTjeneste, VirksomhetOppslagTjeneste virksomhetOppslag,
    EksternNavneoppslagTjeneste eksternOppslag)
{
    /// <summary>Diskriminatorverdien skrevet til <see cref="NavnekandidatEntitet.OppdagelsesKilde"/> for
    /// alle kandidater produsert av det brede "stor bokstav"-mønsteret (<see cref="FinnStorBokstavKandidaterITekst"/>,
    /// docs/31) — se den entitetsfeltets kommentar. <c>null</c> for kandidater fra de eldre, presise
    /// mønstrene (faste gruppe-/rollesubstantiv, flerords-institusjonsord). Public (ikke internal): brukt
    /// av RegelIde.Api ved berikelse-oppslag på lesetidspunktet (se NavnekandidatDto/Program.cs).
    /// <para>
    /// [Restrukturert, 2026-09-03] Feltet levde tidligere ved siden av et eget <see cref="SveipStorBokstavAsync"/>-
    /// endepunkt — det endepunktet finnes ikke lenger (slått sammen inn i <see cref="SveipAsync"/>, se
    /// klassekommentaren), men selve diskriminatorverdien er fortsatt meningsfull og uendret: den skiller
    /// FORTSATT "hvilket mønster oppdaget denne raden" for UI-visning, uavhengig av at begge mønstrene nå
    /// kjøres av samme metode/kall.
    /// </para></summary>
    public const string StorBokstavOppdagelsesKilde = "stor-bokstav-snl-ssr";

    /// <summary>Faste juridisk-aktør-substantiv UTEN suffiks (docs/13-backlog.md §9) — ALLTID
    /// <c>"gruppe"</c>-kandidater, uansett store/små bokstaver. Lengst-først i alternasjonen, slik at
    /// "Kongen i statsråd" foretrekkes framfor et delvis treff på bare "Kongen".
    /// <para>
    /// <b>[Ny, kodegjennomgang 2026-08-30]</b> "statsforvalter"/"kommune"/"fylkeskommune"/"departement"
    /// er utvidet fra KUN bestemt entall (den opprinnelige, eneste dekkede formen) til ALLE fire
    /// bøyningsformer via <see cref="Bøyningsformer"/> — ubestemt entall, bestemt entall, ubestemt
    /// flertall, bestemt flertall. Bekreftet i live data: "kommuneloven" har 71 forekomster av
    /// "kommuner" og 71 av "fylkeskommuner" (ubestemt flertall) som IKKE ble fanget i det hele tatt før
    /// denne utvidelsen. Bevisst en LUKKET liste over kjente stammer + et lite, begrenset sett
    /// bøyningsendelser — IKKE en generell lemmatizer/språkmodell (eksplisitt forbudt, ren regex).
    /// </para>
    /// <para>
    /// [Restrukturert, 2026-09-03] Denne listen/mønsteret er UENDRET av dagens restrukturering (se
    /// klassekommentaren) — den er allerede en lukket, hånd-kuratert liste per design, ALDRI sendt til
    /// SNL/SSR, og var derfor aldri en del av «whack-a-mole»-problemet suffiksmønsteret ble slettet for.
    /// </para></summary>
    private static readonly string[] FasteRollesubstantiv =
    [
        "Kongen i statsråd", "Kongen", "Stortinget", "Regjeringen",
        .. Bøyningsformer("statsforvalter", "en", "e", "ne"), // statsforvalter/-en/-e/-ne
        .. Bøyningsformer("kommune", "n", "r", "ne"), // kommune/-n/-r/-ne
        .. Bøyningsformer("fylkeskommune", "n", "r", "ne"), // fylkeskommune/-n/-r/-ne
        .. Bøyningsformer("departement", "et", "er", "ene"), // departement/-et/-er/-ene
    ];

    /// <summary>Bygger de fire bøyningsformene (ubestemt entall = stammen selv, + de tre oppgitte
    /// endelsene i rekkefølgen bestemt entall/ubestemt flertall/bestemt flertall) av én kjent stamme —
    /// se <see cref="FasteRollesubstantiv"/>s kommentar for hvorfor dette er en lukket liste, ikke en
    /// generell bøyningsregel.</summary>
    private static string[] Bøyningsformer(string stamme, string bestemtEntallEndelse, string ubestemtFlertallEndelse, string bestemtFlertallEndelse) =>
        [stamme, stamme + bestemtEntallEndelse, stamme + ubestemtFlertallEndelse, stamme + bestemtFlertallEndelse];

    private static readonly Regex FasteGruppeMønster = new(
        @"\b(?:" + string.Join('|', FasteRollesubstantiv.OrderByDescending(s => s.Length).Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// Kjente institusjonsord i UBESTEMT FORM (docs/13-backlog.md §9, Johanns liste — ikke uttømmende)
    /// brukt av flerords-mønsteret (<see cref="FinnEgennavnForanInstitusjonsord"/>) — MÅ stå som eget,
    /// mellomromsdelt ord etter et egennavn (til forskjell fra det tidligere, nå slettede suffiksmønsteret,
    /// som var smeltet sammen med stammen — se klassekommentaren). "vegvesen" lagt til utover Johanns
    /// opprinnelige liste — "Statens vegvesen" skrives (til forskjell fra f.eks. "tilsyn"-institusjoner,
    /// som alltid er ETT sammensatt ord som "Datatilsynet") faktisk som to ord i virkelig bruk, og ordet
    /// har lav tvetydighetsrisiko alene (nesten utelukkende brukt om denne ene, spesifikke etaten).
    /// <para>
    /// <b>[Ny, kodegjennomgang 2026-08-30] Skole-relaterte ord</b> — bekreftet i live data (korpusomfattende
    /// sveip + direkte tekstsøk mot den kjørende dev-databasen, se PR-beskrivelsen) at korpuset inneholder
    /// MANGE navngitte fagskoler av nettopp formen "[Egennavn] fagskole" i selve rettskilde-TEKSTEN, ikke
    /// bare i titler — f.eks. "Nortrain fagskole", "Nordland fagskole", "Noroff fagskole", "TISIP fagskole".
    /// Lagt til: "fagskole", "høyskole", "høgskole", "høgskule" (BEGGE bokmål/nynorsk-stavemåter forekommer
    /// i ekte navn, f.eks. "Høgskulen på Vestlandet" — "høgskule" er derfor tatt med selv om den ikke sto i
    /// Johanns opprinnelige liste), "universitet", "barnehage".
    /// </para>
    /// <para>
    /// <b>Bevisst UTELATT: "skole" alene</b> (uten fag-/høy-/høg-prefiks). Et korpusomfattende testsveip
    /// med "skole" i lista ga et FLOM av falske positiver av formen "[Adjektiv/Stedsnavn] skole" der
    /// "skole" er en helt generisk fellesbetegnelse, ikke del av et spesifikt egennavn — f.eks. "Denne
    /// skole", "Norsk skole", "Samisk videregående skole". "skole" inngår produktivt i sammensetninger med
    /// ETHVERT stedsnavn/skoletype-adjektiv ("kunstskole", "sykepleierskole", "sommerskole", osv.), til
    /// forskjell fra "fagskole"/"høyskole"/"barnehage"/"universitet", som er LUKKEDE, spesifikke
    /// institusjonstyper. Løsning: utelatt helt, presisjon foran recall — de sammensatte institusjonsordene
    /// ("fagskole" m.fl.) dekker likevel det STORE flertallet av de bekreftede, navngitte skolene i korpuset.
    /// </para>
    /// <para>
    /// [Ny, issue #150 del 1] "utvalg"/"enhet" lagt til — Johann forventet vesentlig flere forslag i
    /// navnekandidat-køen enn den gamle arkitekturen ga, bl.a. eksemplifisert med "EOS-utvalget"/
    /// "PNR-enheten". Fanger et flerords egennavn+institusjonsord-par av nøyaktig samme form som "Statens
    /// vegvesen"/"Møre og Romsdal fylkeskommune", f.eks. et tenkt "Klageutvalget for X" ELLER "[Egennavn]
    /// utvalg"/"[Egennavn] enhet" der institusjonsordet står som eget, mellomromsdelt ord. Fanger IKKE
    /// bindestrek-forkortelsen "EOS-utvalget"/"PNR-enheten" selv (issue #150 del 2, ikke bygget her).
    /// </para>
    /// <para>
    /// [Ny, 2026-09-03] "arkiv" lagt til — samme klasse hull som "utvalg"/"enhet" over, bekreftet ved at
    /// "Nasjonalarkivet" (LOV-2025-06-20-96 § 4) manglet fra oppdagelsen. Under den GAMLE arkitekturen ble
    /// dette løst ved å legge "arkivet" til det (nå slettede) suffiksmønsteret — under DENNE arkitekturen
    /// fanges "Nasjonalarkivet" i stedet strukturelt av det brede "stor bokstav"-mønsteret (se
    /// <see cref="FinnStorBokstavKandidaterITekst"/>), og "arkiv" legges her KUN for å dekke det
    /// FLERORDS-formede tilfellet ("[Egennavn] arkiv" som to ord), samme begrunnelse som "utvalg"/"enhet".
    /// </para>
    /// </summary>
    private static readonly string[] Institusjonsord =
    [
        "fylkeskommune", "kommune", "direktorat", "tilsyn", "departement", "fylkesmannsembete", "vegvesen",
        "fagskole", "høyskole", "høgskole", "høgskule", "universitet", "barnehage", "utvalg", "enhet", "arkiv",
    ];

    private static readonly Regex InstitusjonsordMønster = new(
        @"\b(?:" + string.Join('|', Institusjonsord.OrderByDescending(s => s.Length).Select(Regex.Escape)) + @")\b");

    /// <summary>
    /// Det ENESTE bindeordet flerords-mønsteret tillater MELLOM to store-forbokstav-ord (f.eks. "Møre
    /// og Romsdal", "Troms og Finnmark", "Sogn og Fjordane" — alle bekreftet ekte, tidligere/nåværende
    /// fylkesnavn i live-data-sveipet under).
    /// <para>
    /// <b>[Ny, kodegjennomgang 2026-08-30]</b> Opprinnelig vurdert å også inkludere "i" (Johanns
    /// pseudo-eksempel "Sør i Nordland"), men et korpusomfattende testsveip mot den kjørende
    /// dev-databasen avdekket et konkret falskt positiv: "Inntaksnemnda i Finnmark fylkeskommune" —
    /// her er "i" en ekte PREPOSISJON ("Inntaksnemnda i [fylket] Finnmark fylkeskommune"), ikke et
    /// navneinternt bindeord, og fanget dermed feilaktig med et helt urelatert substantiv foran. "og"
    /// viste INGEN tilsvarende feil i samme sveip (kun ekte sammensatte fylkesnavn). Fjernet "i" på
    /// bakgrunn av dette funnet — presisjon foran Johanns opprinnelige eksempel, som viste seg ikke å
    /// holde mål mot ekte data.
    /// </para></summary>
    private static readonly HashSet<string> TillatteBindeord = new(StringComparer.Ordinal) { "og" };

    /// <summary>
    /// [Ny, kodegjennomgang 2026-08-30] Lukket liste over norske determinativer/kvantorer/pronomen som
    /// ALDRI skal telle som et egennavn-ord i flerords-mønsteret, SELV OM de er stor forbokstav (de er
    /// det typisk KUN fordi de tilfeldigvis åpner en node/setning — nøyaktig samme tvetydighet som
    /// begrunner <see cref="ErSetningsstart"/> for det brede stor-bokstav-mønsteret). Avdekket av samme
    /// korpusomfattende testsveip som begrunner <see cref="TillatteBindeord"/>-innstrammingen: uten denne
    /// lista ga mønsteret falske positiver som "Enhver fylkeskommune", "En kommune", "Hver kommune",
    /// "Det departement" — generiske funksjonsord, ikke navn. En LUKKET liste (determinativer/pronomen
    /// er en grammatisk lukket ordklasse i norsk, til forskjell fra f.eks. adjektiv) — IKKE et forsøk på
    /// å luke ut ALLE tenkelige falske positiver (et adjektiv som "Statlig tilsyn" ville fortsatt sluppet
    /// gjennom — se <see cref="ErFlerordsKontekstTillatt"/>s kommentar om denne gjenværende, dokumenterte
    /// begrensningen).
    /// </summary>
    private static readonly HashSet<string> AldriEgennavnOrd = new(StringComparer.Ordinal)
    {
        "En", "Et", "Ei", "Den", "Det", "Denne", "Dette", "Disse",
        "Enhver", "Ethvert", "Enkelte", "Hver", "Hvert", "Alle", "Ingen",
        "Flere", "Mange", "Noen", "Andre", "Annen", "Annet",
        "Hvilken", "Hvilket", "Hvilke", "Slik", "Slike", "Samme",
    };

    /// <summary>
    /// [Restrukturert, 2026-09-03 — nå den PRIMÆRE utløseren for <c>"virksomhet"</c>-treff som ikke
    /// allerede fanges av flerords-mønsteret, se klassekommentaren] Det brede "stor bokstav midt i en
    /// setning"-mønsteret (docs/31 §5 punkt 2) — se <see cref="FinnStorBokstavKandidaterITekst"/> for
    /// selve utløseren og <see cref="KlassifiserAsync"/> for klassifiseringskjeden (docs/31 §2) som
    /// skiller ekte institusjonsnavn fra stedsnavn/personnavn/utenlandske ord via SNL/SSR.
    /// <para>
    /// <b>KUN Title-case ord</b> (stor forbokstav + ETT ELLER FLERE små bokstaver, ALDRI en ordform der
    /// bokstav nummer to også er stor) — utelukker BEVISST forkortelser i store bokstaver ("NKR", "SFO",
    /// "NVE") fra selve rå-utløseren. Disse er en KJENT, allerede dokumentert falsk-positiv-kilde i
    /// denne klassen (se <see cref="FinnEgennavnForanInstitusjonsord"/>s "NKR og fagskole"/"SFO og
    /// skole"-funn) — ingen av dem er reelle egennavn i seg selv, og SNL/SSR-oppslag mot en bar
    /// forkortelse ville uansett aldri gitt et meningsfullt treff. Ren tegnbasert avgrensning, ikke en
    /// forkortelsesordliste.
    /// </para>
    /// </summary>
    private static readonly Regex StorBokstavOrdMønster = new(@"\b\p{Lu}\p{Ll}+\b");

    /// <summary>Flate, case-insensitive oppslagsmengder av <see cref="FasteRollesubstantiv"/>/
    /// <see cref="Institusjonsord"/> — brukt KUN av <see cref="FinnStorBokstavKandidaterITekst"/> til å
    /// UNNGÅ å sende en term de EKSISTERENDE, presise mønstrene allerede dekker med høyere presisjon
    /// videre til et kostbart eksternt SNL/SSR-oppslag (rent en ytelses-/høflighets-optimalisering mot
    /// de eksterne API-ene). <see cref="SveipAsync"/> anvender i tillegg sin egen, in-memory
    /// posisjonssjekk PER NODE (se den metodens kommentar) som en ekstra, komplementær beskyttelse mot
    /// at samme START-posisjon behandles av BEGGE mønstrene innenfor ETT sveip (f.eks. det første ordet
    /// i et flerords-treff, som også isolert er et Title-case-ord) — de to mekanismene dekker altså to
    /// litt ulike ting: denne sjekker TERM-identitet mot kjente, presise ordlister, posisjonssjekken i
    /// <see cref="SveipAsync"/> sjekker ren POSISJONS-identitet mot det andre mønsterets EGET treff.</summary>
    private static readonly HashSet<string> FasteRollesubstantivOrdSet = new(FasteRollesubstantiv, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> InstitusjonsordSet = new(Institusjonsord, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ren, testbar funksjon uten DB/HTTP-avhengighet — selve rå-utløseren for det brede
    /// stor-bokstav-mønsteret, separert fra <see cref="SveipAsync"/>s async DB-/SNL/SSR-orkestrering av
    /// samme grunn som <see cref="FinnKandidaterITekst"/> er separert fra <see cref="SveipAsync"/>.
    /// <para>
    /// Ekskluderer (i tillegg til <see cref="ErSetningsstart"/>, samme "midt i en setning"-vern som
    /// resten av klassen): <see cref="AldriEgennavnOrd"/> (determinativer/pronomen — grammatisk lukket
    /// klasse, aldri egennavn) og <see cref="FasteRollesubstantivOrdSet"/>/<see cref="InstitusjonsordSet"/>
    /// (allerede dekket av presise, eksisterende mønstre — se feltkommentaren).
    /// </para>
    /// <para>
    /// [Restrukturert, 2026-09-03] Ekskluderte TIDLIGERE også ord som endte på et kjent suffiks
    /// (<c>Suffikser</c>), for å unngå å sende en term det gamle, presise suffiksmønsteret allerede
    /// klassifiserte med høyere presisjon videre til et eksternt SNL-oppslag. Suffiksmønsteret (og
    /// dermed HELE denne sjekken) er slettet i denne restruktureringen — se klassekommentaren. Et ord som
    /// "Miljødirektoratet" fanges nå UTELUKKENDE av DETTE mønsteret og valideres strukturelt mot SNL/SSR
    /// i stedet for mot en hånd-vedlikeholdt suffiksliste.
    /// </para>
    /// </summary>
    internal static List<(int Start, int Lengde, string RaaTekst)> FinnStorBokstavKandidaterITekst(string tekst)
    {
        var funnet = new List<(int, int, string)>();
        foreach (Match m in StorBokstavOrdMønster.Matches(tekst))
        {
            if (ErSetningsstart(tekst, m.Index)) continue;
            var ord = m.Value;
            if (AldriEgennavnOrd.Contains(ord)) continue;
            if (FasteRollesubstantivOrdSet.Contains(ord)) continue;
            if (InstitusjonsordSet.Contains(ord)) continue;
            funnet.Add((m.Index, m.Length, ord));
        }
        return funnet;
    }

    /// <summary>Det første hele ordet (bokstaver) som starter etter evt. mellomrom/tab fra
    /// <paramref name="index"/> — brukt av <see cref="KlassifiserAsync"/> til å sjekke om et
    /// <see cref="Institusjonsord"/> står RETT ETTER et SSR-bekreftet stedsnavn (docs/31 §2 punkt 2).
    /// Samme "kun mellomrom/tab, stopp på ALT annet"-restriktivitet som
    /// <see cref="FinnEgennavnForanInstitusjonsord"/>s bakoverskanning (linjeskift/skilletegn teller
    /// IKKE som "rett etter").</summary>
    private static string? NesteOrdEtter(string tekst, int index)
    {
        var i = index;
        while (i < tekst.Length && (tekst[i] == ' ' || tekst[i] == '\t')) i++;
        if (i >= tekst.Length || !char.IsLetter(tekst[i])) return null;
        var start = i;
        while (i < tekst.Length && char.IsLetter(tekst[i])) i++;
        return tekst[start..i];
    }

    /// <summary>Projeksjon brukt av <see cref="SveipAsync"/> (se <see cref="HentSveipbareNoderAsync"/>) —
    /// <see cref="Tekst"/> er ALLTID non-null her (filtrert i spørringen), til forskjell fra
    /// <see cref="RettskildeNodeEntitet.Tekst"/> selv.</summary>
    private sealed record SveipbarNode(Guid RettskildeId, string Eid, string Tekst);

    /// <summary>
    /// Én kandidatforekomst av kategori <c>"virksomhet"</c> samlet i FASE 1 av <see cref="SveipAsync"/>
    /// (samle-så-klassifiser, se klassekommentaren) — venter på klassifisering i FASE 2, materialiseres
    /// i FASE 3. <see cref="NormalisertTerm"/> er nøkkelen fase 2 grupperer/memoiserer klassifiseringen
    /// på (case-insensitiv — <see cref="RaaTekst"/> beholder derimot den FAKTISKE stavemåten, brukt som
    /// selve <see cref="NavnekandidatEntitet.ForeslattTekst"/>-verdien ved opprettelse).
    /// </summary>
    private sealed record VentendeVirksomhetTreff(
        SveipbarNode Node, int Start, int Lengde, string RaaTekst, string NormalisertTerm, int MatchSlutt, string? OppdagelsesKilde);

    /// <summary>
    /// Node-spørring for <see cref="SveipAsync"/> — se de utfyllende kommentarene som fantes her FØR
    /// denne ble faktorert ut (git-historikk, kodegjennomgang 2026-08-30): (1) KUN <c>VirksomhetId == null</c>
    /// rettskilder — unngår kryssvirksomhet-lekkasje (samme reelt fikset bug som
    /// <see cref="VirksomhetKandidatSveipTjeneste"/> hadde, Agder/Bergen 2026-08-22), (2) KUN
    /// <c>Entitetsstatus == "gjeldende"</c> på BÅDE noden og selve <see cref="RettskildeEntitet"/> (en
    /// reimportert lovs gamle rettskilde-rad blir 'erstattet', men dens noder forblir for alltid
    /// 'gjeldende' på nodenivå — uten dette dobbeltfilteret ble kandidater fra utdaterte rettskilder
    /// tidligere opprettet, men kunne ALDRI godkjennes).
    /// <para>
    /// [Restrukturert, 2026-09-03] Het tidligere "delt for BEGGE sveipmetodene" (<c>SveipAsync</c> OG
    /// <c>SveipStorBokstavAsync</c>) — nå kun én caller, <see cref="SveipAsync"/>, etter at de to
    /// metodene ble slått sammen (se klassekommentaren). Selve spørringen/scopingen er UENDRET.
    /// </para>
    /// </summary>
    private async Task<List<SveipbarNode>> HentSveipbareNoderAsync(Guid? rettskildeId, CancellationToken ct)
    {
        // Krever delt/nasjonal OG gjeldende, se spørringen under — samme to vilkår, kontrollert her på
        // forhånd for en presis feilmelding når EN bestemt rettskilde etterspørres eksplisitt (i stedet
        // for at den bare gir 0 treff stille).
        if (rettskildeId is not null && !await db.Rettskilder.AnyAsync(
                r => r.Id == rettskildeId && r.VirksomhetId == null && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException(
                $"Fant ingen gjeldende, delt/nasjonal rettskilde med id '{rettskildeId}'. Ingen gjettet fallback.");
        }

        return await db.RettskildeNoder
            .Join(db.Rettskilder, n => n.RettskildeId, r => r.Id, (n, r) => new { Node = n, r.VirksomhetId, RettskildeStatus = r.Entitetsstatus })
            .Where(x => x.Node.Tekst != null && !x.Node.Opphevet && x.Node.Entitetsstatus == "gjeldende"
                        && x.VirksomhetId == null && x.RettskildeStatus == "gjeldende"
                        && (rettskildeId == null || x.Node.RettskildeId == rettskildeId))
            .Select(x => new SveipbarNode(x.Node.RettskildeId, x.Node.Eid, x.Node.Tekst!))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Kjører oppdagelsessveipet — enten mot ÉN rettskilde (<paramref name="rettskildeId"/> satt) eller
    /// mot HELE det importerte korpuset (<paramref name="rettskildeId"/> = <c>null</c>). Den ENESTE
    /// sveipemetoden i klassen — dekker faste gruppe-/rollesubstantiv, flerords-institusjonsord OG det
    /// brede "stor bokstav"-mønsteret sammen, se klassekommentarens restruktureringsavsnitt for
    /// bakgrunnen (tidligere to separate metoder/endepunkt).
    /// <para>
    /// <b>Tre faser — samle, klassifiser, materialiser (samle-så-klassifiser, ikke klassifiser-per-posisjon):</b>
    /// <list type="number">
    /// <item><b>Fase 1 (ren, rask, DB-dedup, INGEN nettverkskall):</b> for hver sveipbar node, kjør
    /// <see cref="FinnKandidaterITekst"/> (faste-gruppe- + flerords-mønster) og
    /// <see cref="FinnStorBokstavKandidaterITekst"/> (det brede mønsteret), og anvend umiddelbart:
    /// <c>"gruppe"</c>-treff opprettes DIREKTE her (ingen klassifisering trengs, se punkt 2 under) etter
    /// samme "allerede dekket av Begrep/eksisterende kandidat"-filtrering som før.
    /// <c>"virksomhet"</c>-treff (fra BEGGE de to gjenværende mønstrene) filtreres mot eksisterende
    /// <see cref="BegrepEntitet"/>-navneformer og mot allerede eksisterende kandidatposisjoner
    /// (<c>(RettskildeId, NodeEid, StartOffset)</c>), og samles i en <see cref="VentendeVirksomhetTreff"/>-
    /// liste for fase 2 — INGEN <see cref="EksternNavneoppslagTjeneste"/>-kall skjer i denne fasen.
    /// Én ekstra, NY beskyttelse her (mulig først nå som begge mønstrene kjøres i SAMME metode/kall): et
    /// <c>HashSet&lt;int&gt;</c> per node av allerede behandlede START-posisjoner hindrer at samme
    /// posisjon behandles av BEGGE mønstrene (f.eks. det første ordet i "Møre og Romsdal fylkeskommune" —
    /// "Møre" — som isolert også er et gyldig Title-case-ord for det brede mønsteret; flerords-mønsteret
    /// kjøres FØRST og "vinner" posisjonen, konsistent med at det er det mer PRESISE av de to).</item>
    /// <item><b>Fase 2 (klassifiser HVERT UNIKE navn nøyaktig ÉN gang):</b> grupperer ALT samlet i fase 1
    /// på normalisert (case-insensitiv) tekst, og kaller <see cref="KlassifiserAsync"/> (docs/31 §2) KUN
    /// for hvert UNIKE navn i DENNE kjøringen — resultatet memoiseres i et lokalt
    /// <c>Dictionary&lt;string, bool&gt;</c>. Den eksisterende <see cref="EksternNavneoppslagTjeneste"/>-
    /// cachen (databasetabell) forhindrer allerede duplikate HTTP-kall PÅ TVERS av sveip/kjøringer for
    /// samme term — dette dictionary-et forhindrer i tillegg duplikate CACHE-OPPSLAG INNENFOR samme
    /// kjøring for et navn nevnt mange ganger (f.eks. et institusjonsnavn nevnt 5 ganger i samme lov gir
    /// nå ÉN klassifisering, ikke 5, selv om alle 5 uansett ville truffet samme cache-rad).</item>
    /// <item><b>Fase 3 (materialiser):</b> for hver <see cref="VentendeVirksomhetTreff"/> fra fase 1,
    /// slå opp navnets klassifiseringsresultat fra fase 2 og opprett raden med riktig INITIAL status —
    /// se <see cref="KlassifiserAsync"/>s kommentar for selve to-utfalls-avgjørelsen (SNL-/kvalifisert-
    /// SSR-bekreftelse → <c>"Venter"</c>, alt annet → <c>"Avvist"</c> direkte, men ALLTID opprettet).</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Scopet til KUN <c>"virksomhet"</c> for selve klassifiseringen — bevisst, ikke en stilltiende
    /// innsnevring:</b> <c>"gruppe"</c>-treff (<see cref="FasteGruppeMønster"/>/<see cref="FasteRollesubstantiv"/>)
    /// sendes ALDRI til SNL/SSR. Begrunnelse: <see cref="FasteRollesubstantiv"/>s klassekommentar sier
    /// eksplisitt at disse er en LUKKET liste over generiske, juridiske rollesubstantiv ("Kongen",
    /// "Stortinget", bøyningsformer av "kommune"/"departement" osv.) — ALLTID korrekte som "gruppe" per
    /// design, uansett kontekst, ikke kandidater for et institusjons-EGENNAVN-oppslag i utgangspunktet
    /// (SNL/SSR svarer på "er DETTE en kjent institusjon/et kjent stedsnavn", et spørsmål som ikke gir
    /// mening for et rent rollesubstantiv).
    /// </para>
    /// <para>
    /// <b>Nettverkskall unngås når mulig</b>: klassifiseringen kalles KUN for et navn hvis MINST ÉN av
    /// forekomstene har en posisjon som IKKE allerede har en eksisterende <see cref="NavnekandidatEntitet"/>-
    /// rad — et gjentatt sveip over samme tekst re-klassifiserer ikke allerede oppdagede kandidater.
    /// </para>
    /// </summary>
    public async Task<NavnekandidatSveipResultat> SveipAsync(Guid? rettskildeId, string opprettetAv, CancellationToken ct = default)
    {
        var noder = await HentSveipbareNoderAsync(rettskildeId, ct);

        // Eksisterende Begrep-termer, forhåndslastet ÉN gang for hele sveipet (ikke ett spørring per
        // treff) — samme "unngå N+1" -hensyn som ellers i kodebasen. To separate mengder, se
        // klassekommentaren for HVORFOR scopingen er ulik per kategori.
        var virksomhetTermer = new HashSet<string>(
            await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende")
                .Select(b => b.Term).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);
        var gruppeTermerPerLovkilde = (await db.Begreper
                .Where(b => b.Begrepskategori == "gruppe" && b.Entitetsstatus == "gjeldende" && b.LovkildeId != null)
                .Select(b => new { b.Term, b.LovkildeId }).ToListAsync(ct))
            .GroupBy(b => b.LovkildeId!.Value)
            .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.Term), StringComparer.OrdinalIgnoreCase));

        // [Ny, kodegjennomgang 2026-08-30] Forhåndslastet, ÉN gang for hele sveipet, samme "unngå N+1"
        // -hensyn som mengdene over — normaliserte (små bokstaver) "gruppe"-termer PER RettskildeId, fra
        // EKSISTERENDE Navnekandidat-rader, uansett status. Brukes til å utvide "alleredeDekket"-sjekket
        // under til Å OGSÅ dekke ikke-godkjente kandidater, ikke bare godkjente Begrep-rader — se
        // klassekommentarens "Term-basert dedup"-avsnitt for hvorfor. Oppdateres fortløpende i løkken
        // under (samme sveip kan treffe samme normaliserte term flere ganger på ulike posisjoner).
        var gruppeKandidatTermerPerRettskilde = (await db.Navnekandidater
                .Where(k => k.Kategori == "gruppe")
                .Select(k => new { k.RettskildeId, k.ForeslattTekst }).ToListAsync(ct))
            .GroupBy(k => k.RettskildeId)
            .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.ForeslattTekst.ToLowerInvariant()), StringComparer.Ordinal));

        var antallTreff = 0;
        var antallNyeKandidater = 0;

        // ---------- Fase 1: samle, rent + DB-dedup, ingen nettverkskall ----------
        var trengerKlassifisering = new List<VentendeVirksomhetTreff>();

        foreach (var node in noder)
        {
            // Posisjoner allerede behandlet i DENNE noden i DETTE sveipet — hindrer at det brede
            // stor-bokstav-mønsteret dobbeltbehandler en posisjon flerords-mønsteret allerede fanget
            // (se metodekommentarens fase 1-avsnitt).
            var behandledeStartposisjoner = new HashSet<int>();

            foreach (var (start, lengde, kategori) in FinnKandidaterITekst(node.Tekst!))
            {
                behandledeStartposisjoner.Add(start);
                var raaTekst = node.Tekst![start..(start + lengde)];
                // [Ny, kodegjennomgang 2026-08-30] Normaliser KUN "gruppe" til små bokstaver — se
                // klassekommentarens "Normalisering før lagring"-avsnitt. "virksomhet" beholder rå tekst
                // (case er signal, ikke støy, for et egennavn).
                var tekst = kategori == "gruppe" ? raaTekst.ToLowerInvariant() : raaTekst;

                var alleredeDekketAvBegrep = kategori == "virksomhet"
                    ? virksomhetTermer.Contains(tekst)
                    : gruppeTermerPerLovkilde.TryGetValue(node.RettskildeId, out var gruppeTermer) && gruppeTermer.Contains(tekst);
                var alleredeDekketAvEksisterendeKandidat = kategori == "gruppe"
                    && gruppeKandidatTermerPerRettskilde.TryGetValue(node.RettskildeId, out var eksisterendeTermer)
                    && eksisterendeTermer.Contains(tekst);
                if (alleredeDekketAvBegrep || alleredeDekketAvEksisterendeKandidat) continue;

                antallTreff++;
                var forAntall = await db.Navnekandidater.CountAsync(
                    k => k.RettskildeId == node.RettskildeId && k.NodeEid == node.Eid && k.StartOffset == start, ct);

                if (kategori == "gruppe")
                {
                    // Faste rollesubstantiv — en lukket, hånd-kuratert liste, ALLTID korrekt som
                    // "gruppe" per design (se klassekommentaren). Ingen klassifisering, opprett direkte.
                    await OpprettEllerFinnAsync(tekst, "gruppe", node.RettskildeId, node.Eid, start, start + lengde, opprettetAv, ct);
                    if (forAntall == 0)
                    {
                        antallNyeKandidater++;
                        // Registrer umiddelbart, slik at en SENERE posisjon i samme sveip (samme
                        // rettskilde, samme normaliserte term) også blir korrekt gjenkjent som dekket.
                        if (!gruppeKandidatTermerPerRettskilde.TryGetValue(node.RettskildeId, out var settForRettskilde))
                        {
                            settForRettskilde = new HashSet<string>(StringComparer.Ordinal);
                            gruppeKandidatTermerPerRettskilde[node.RettskildeId] = settForRettskilde;
                        }
                        settForRettskilde.Add(tekst);
                    }
                }
                else
                {
                    // "virksomhet" fra flerords-mønsteret — HELE det fangede navnet sendes til
                    // klassifisering i fase 2, se metodekommentaren.
                    if (forAntall > 0) continue; // allerede en kandidat her — trenger ikke reklassifiseres.
                    trengerKlassifisering.Add(new VentendeVirksomhetTreff(
                        node, start, lengde, raaTekst, raaTekst.ToLowerInvariant(), start + lengde, OppdagelsesKilde: null));
                }
            }

            foreach (var (start, lengde, raaTekst) in FinnStorBokstavKandidaterITekst(node.Tekst!))
            {
                if (!behandledeStartposisjoner.Add(start)) continue; // samme posisjon allerede dekket over.

                if (virksomhetTermer.Contains(raaTekst)) continue; // allerede dekket av eksisterende Begrep.

                antallTreff++;
                var forAntall = await db.Navnekandidater.CountAsync(
                    k => k.RettskildeId == node.RettskildeId && k.NodeEid == node.Eid && k.StartOffset == start, ct);
                if (forAntall > 0) continue;

                trengerKlassifisering.Add(new VentendeVirksomhetTreff(
                    node, start, lengde, raaTekst, raaTekst.ToLowerInvariant(), start + lengde, StorBokstavOppdagelsesKilde));
            }
        }

        // ---------- Fase 2: klassifiser hvert unike navn nøyaktig én gang ----------
        var klassifiseringPerTerm = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var treff in trengerKlassifisering)
        {
            if (klassifiseringPerTerm.ContainsKey(treff.NormalisertTerm)) continue;
            klassifiseringPerTerm[treff.NormalisertTerm] =
                await KlassifiserAsync(treff.RaaTekst, treff.Node.Tekst, treff.MatchSlutt, ct);
        }

        // ---------- Fase 3: materialiser ----------
        foreach (var treff in trengerKlassifisering)
        {
            var erTreff = klassifiseringPerTerm[treff.NormalisertTerm];
            await OpprettEllerFinnAsync(
                treff.RaaTekst, "virksomhet", treff.Node.RettskildeId, treff.Node.Eid, treff.Start, treff.Start + treff.Lengde,
                opprettetAv, ct, treff.OppdagelsesKilde, initialStatus: erTreff ? "Venter" : "Avvist");
            antallNyeKandidater++; // forAntall==0 ble allerede bekreftet for denne posisjonen i fase 1.
        }

        return new NavnekandidatSveipResultat(antallTreff, antallNyeKandidater);
    }

    /// <summary>
    /// docs/31 §2 — selve klassifiseringskjeden for ETT unikt kandidatnavn (kalt fra fase 2 i
    /// <see cref="SveipAsync"/>, én gang per unikt navn i sveipet, se metodekommentaren der). Het
    /// tidligere <c>BeholdSomKandidatAsync</c> og returnerte <c>bool Behold</c> ("skal raden i det hele
    /// tatt opprettes"); heter nå <c>KlassifiserAsync</c> og returnerer <c>bool ErTreff</c> ("skal raden
    /// opprettes som <c>Venter</c> (<c>true</c>) eller <c>Avvist</c> (<c>false</c>) — raden opprettes NÅ
    /// ALLTID, se <see cref="SveipAsync"/>s fase 3).
    /// <para>
    /// <b>[Restrukturert, 2026-09-03] To-utfalls klassifisering, ikke tre</b> — Johanns instruks,
    /// verbatim: «sjekker mot SSR og SNL. hvis treff i en tab og hvis hva som ble avvist i en annen
    /// tab». Den gamle kjeden hadde en tredje, "ingen gjettet fallback"-gren (ukjent i BEGGE kildene →
    /// behold som lav-tillit <c>"Venter"</c>-kandidat) — denne grenen er fjernet. Selve MATCH-LOGIKKEN
    /// er ellers UENDRET fra før (se punktlisten under) — kun HVA hvert utfall BETYR er endret:
    /// </para>
    /// <list type="number">
    /// <item>SNL-bekreftet institusjon (<see cref="EksternNavneoppslagTjeneste.SlaOppSnlAsync"/> gir
    /// treff) → <c>true</c> ("Venter") — høy starttillit, positiv institusjonskandidat.</item>
    /// <item>Ingen SNL-treff, MEN SSR-bekreftet stedsnavn (<see cref="EksternNavneoppslagTjeneste.SlaOppSsrAsync"/>)
    /// MED et <see cref="Institusjonsord"/> RETT ETTER i løpeteksten (<see cref="NesteOrdEtter"/>) →
    /// <c>true</c> ("Venter") — samme positive bekreftelsesrolle som når <see cref="InstitusjonsordMønster"/>
    /// allerede finner "X kommune" et annet sted i klassen; SSR gir en EKSTRA bekreftelse på at "X" er et
    /// reelt stedsnavn. SSR-bekreftet stedsnavn UTEN institusjonsord rett etter → <c>false</c> ("Avvist")
    /// — en ren geografisk løpetekst-referanse, ikke et institusjonsnavn. [Restrukturert, 2026-09-03]
    /// Denne grenen opprettet TIDLIGERE ingen rad i det hele tatt (stille forkastet) — den oppretter nå
    /// en <c>"Avvist"</c>-rad, se klassekommentaren.</item>
    /// <item>Ukjent i BEGGE (ingen SNL-treff, ingen SSR-treff) → <c>false</c> ("Avvist").
    /// [Restrukturert, 2026-09-03] Ga TIDLIGERE <c>true</c> ("ingen gjettet fallback" — behold som
    /// lav-tillit) — gir nå <c>false</c>, se klassekommentaren for begrunnelsen (Johanns to-tabs-instruks
    /// tillater ikke en tredje, ubestemt bøtte).</item>
    /// </list>
    /// </summary>
    private async Task<bool> KlassifiserAsync(string raaTekst, string tekst, int matchSlutt, CancellationToken ct)
    {
        var snl = await eksternOppslag.SlaOppSnlAsync(raaTekst, ct);
        if (snl.Treff) return true;

        var ssr = await eksternOppslag.SlaOppSsrAsync(raaTekst, ct);
        if (ssr.Treff)
        {
            var nesteOrd = NesteOrdEtter(tekst, matchSlutt);
            return nesteOrd is not null && InstitusjonsordMønster.IsMatch(nesteOrd);
        }

        return false; // ukjent i begge — se metodekommentaren, gir nå Avvist (ikke lenger "behold").
    }

    /// <summary>
    /// Ren, testbar funksjon uten DB-avhengighet — selve mønstergjenkjenningen for de TO gjenværende,
    /// presise mønstrene (docs/13-backlog.md §9: faste juridisk-aktør-substantiv + flerords-institusjonsord),
    /// separert fra sveipets DB-orkestrering slik at klassifiseringslogikken kan enhetstestes direkte
    /// mot en tekststreng, uten en hel rettskilde-node/embedded Postgres.
    /// <para>
    /// [Restrukturert, 2026-09-03] Kjørte TIDLIGERE ETT TREDJE mønster her også — et hånd-kuratert
    /// suffiksmønster (STOR forbokstav + kjent suffiks → <c>"virksomhet"</c>; LITEN forbokstav + kjent
    /// suffiks → <c>"gruppe"</c>) — se klassekommentarens restruktureringsavsnitt for HVORFOR dette er
    /// slettet (whack-a-mole-mønster fra en stadig voksende, aldri uttømmende ordliste). Konsekvensen:
    /// et STOR-forbokstav-navn som "Miljødirektoratet" fanges IKKE lenger her — det fanges i stedet av
    /// det brede stor-bokstav-mønsteret i <see cref="SveipAsync"/> (<see cref="FinnStorBokstavKandidaterITekst"/>)
    /// og valideres strukturelt mot SNL/SSR. Et LITEN-forbokstav-navn som tidligere fikk suffiksmønsterets
    /// "gruppe"-gren (f.eks. "havnetilsynet", "regelverket") gir nå <b>INGEN kandidat i det hele tatt</b>
    /// — verken denne funksjonen (som aldri hadde noe treff for det) eller det brede mønsteret (som KUN
    /// trigges på stor forbokstav) fanger det. Dette er en bevisst, dokumentert recall-reduksjon,
    /// eksplisitt akseptert som del av restruktureringen (samme "presisjon foran uttømmende recall"-linje
    /// klassen allerede fulgte for f.eks. "skole" alene) — IKKE noe som skal "fikses" ved å legge
    /// suffiksmønsteret til igjen.
    /// </para>
    /// <para>
    /// <b>"Midt i en setning"</b> (<see cref="ErSetningsstart"/>, videreført av <see cref="ErFlerordsKontekstTillatt"/>
    /// for flerords-mønsteret): se de respektive metodekommentarene for presisjonsbegrunnelsen.
    /// </para>
    /// </summary>
    internal static List<(int Start, int Lengde, string Kategori)> FinnKandidaterITekst(string tekst)
    {
        var funnet = new List<(int, int, string)>();

        foreach (Match m in FasteGruppeMønster.Matches(tekst))
        {
            funnet.Add((m.Index, m.Length, "gruppe"));
        }

        // [Ny, kodegjennomgang 2026-08-30] Flerords-mønster (klassekommentarens punkt 4) — se
        // FinnEgennavnForanInstitusjonsord/ErFlerordsKontekstTillatt for presisjonsvernet.
        foreach (Match m in InstitusjonsordMønster.Matches(tekst))
        {
            var egennavn = FinnEgennavnForanInstitusjonsord(tekst, m.Index);
            if (egennavn is null) continue; // ingen store-forbokstav-ord rett før — generisk forekomst
                                             // (f.eks. "en fylkeskommune", "et statlig tilsyn"), ikke et navn.
            var (navnStart, _) = egennavn.Value;
            if (!ErFlerordsKontekstTillatt(tekst, navnStart)) continue;

            funnet.Add((navnStart, m.Index + m.Length - navnStart, "virksomhet"));
        }

        return funnet;
    }

    /// <summary>
    /// Skanner BAKOVER fra <paramref name="institusjonsordStart"/> og samler inntil 3 STOR-forbokstav-ord,
    /// ev. med ETT av <see cref="TillatteBindeord"/> mellom to av dem (f.eks. "Møre og Romsdal" foran
    /// "fylkeskommune"). Stopper ved skilletegn (<c>. , : ; ( )</c>), linjeskift, tekststart, eller et
    /// ord som verken er stor forbokstav eller et tillatt bindeord. Returnerer <c>null</c> hvis INGEN
    /// store-forbokstav-ord ble funnet rett før (den generiske "en fylkeskommune"/"et statlig
    /// tilsyn"-casen — presisjonsvernet docs/13-backlog.md §9 krever) — et dinglende bindeord i hver
    /// ende av det innsamlede spennet fjernes (bindeordet skal kun stå MELLOM to store-forbokstav-ord,
    /// aldri innlede eller avslutte selve navnet), og et for langt spenn (mer enn 3 store-forbokstav-ord
    /// eller mer enn ett bindeord — bør ikke kunne skje gitt filtreringen under, men sjekket eksplisitt
    /// for lesbarhet/defensivt) forkastes HELT i stedet for å bli kappet vilkårlig — ingen gjettet
    /// fallback for hvor navnet "egentlig" starter.
    /// <para>
    /// [Restrukturert, 2026-09-03] Hadde TIDLIGERE i tillegg et snevert genitiv-vern her («Finanstilsynets
    /// tilsyn», «Oljedirektoratets tilsyn» — genitivsform AV en allerede navngitt institusjon, ikke et
    /// navn på en NY institusjon, forkastet HELE treffet når ordet rett før institusjonsordet var [et
    /// KJENT SUFFIKS-NAVN] + genitiv-"s"). Dette vernet avhang av det nå SLETTEDE suffiksmønsterets
    /// ordliste (<c>Suffikser</c>) for å avgjøre hva som var "et kjent institusjonsnavn" — uten den
    /// ordlisten finnes det ikke lenger noe strukturelt signal HER til å skille "Finanstilsynets" (ekte
    /// genitiv av en institusjon) fra "Statens"/"Akershus" (ekte navn som tilfeldigvis ender på "s") uten
    /// å gjeninnføre nøyaktig den typen hånd-kuraterte ordliste restruktureringen fjerner (se
    /// klassekommentaren). Løsning: vernet er fjernet HERFRA — en genitivfrase som "Finanstilsynets
    /// tilsyn" fanges nå som et <c>"virksomhet"</c>-treff av flerords-mønsteret, men blir i praksis
    /// <c>"Avvist"</c> nedstrøms av <see cref="KlassifiserAsync"/> (SNL har ingen artikkel med
    /// headword/alias "Finanstilsynets tilsyn") — presisjonen er FLYTTET til klassifiseringskjeden, ikke
    /// tapt, og raden er nå synlig/revisjonsbar i stedet for stille forkastet FØR den i det hele tatt ble
    /// et forslag.
    /// </para>
    /// </summary>
    private static (int Start, int Lengde)? FinnEgennavnForanInstitusjonsord(string tekst, int institusjonsordStart)
    {
        var tokens = new List<(int Start, int Lengde, bool StorForbokstav)>();
        var posisjon = institusjonsordStart;
        while (tokens.Count < 4) // maks 3 store-forbokstav-ord + ett bindeord
        {
            var j = posisjon - 1;
            while (j >= 0 && (tekst[j] == ' ' || tekst[j] == '\t')) j--;
            if (j < 0) break; // tekststart
            if (tekst[j] is '.' or ',' or ':' or ';' or '(' or ')' or '\n' or '\r') break; // skilletegn/linjeskift
            if (!char.IsLetter(tekst[j])) break; // ukjent tegn rett før (f.eks. et siffer) — stopp konservativt

            var ordSlutt = j + 1;
            var ordStart = j;
            while (ordStart > 0 && char.IsLetter(tekst[ordStart - 1])) ordStart--;
            var ord = tekst[ordStart..ordSlutt];

            var erBindeord = TillatteBindeord.Contains(ord);
            // [Ny, kodegjennomgang 2026-08-30] "AldriEgennavnOrd" — se den listens kommentar. Et
            // determinativ/pronomen (f.eks. "Enhver", "Det") teller IKKE som et egennavn-ord selv om
            // det er stor forbokstav (typisk kun fordi det tilfeldigvis åpner en node/setning).
            var erStorForbokstav = char.IsUpper(ord[0]) && !AldriEgennavnOrd.Contains(ord);
            if (!erStorForbokstav && !erBindeord) break; // hverken egennavn-ord eller tillatt bindeord

            tokens.Add((ordStart, ord.Length, erStorForbokstav));
            posisjon = ordStart;
        }

        tokens.Reverse(); // nå i lese-rekkefølge (venstre-til-høyre, nærmest institusjonsordet sist)

        // Fjern et evt. dinglende bindeord i STARTEN — bindeordet skal kun stå MELLOM to
        // store-forbokstav-ord, aldri innlede selve det fangede navnet. Trygt å bare fjerne og gå
        // videre her: forkastet Start blir ganske enkelt neste (ekte) token, uten noen konsekvens for
        // selve tekstspennet som caller (FinnKandidaterITekst) fanger, siden det spennet uansett
        // begynner nøyaktig ved <see cref="forste"/>.Start.
        while (tokens.Count > 0 && !tokens[0].StorForbokstav) tokens.RemoveAt(0);

        // [Rettet, kodegjennomgang 2026-08-30] Et dinglende bindeord UMIDDELBART FØR institusjonsordet
        // (dvs. det SISTE innsamlede tokenet, nærmest institusjonsordet) kan IKKE bare fjernes på samme
        // måte som i starten — det må forkaste HELE treffet. Årsak: til forskjell fra starten, strekker
        // callerens fangede tekstspenn (FinnKandidaterITekst) seg alltid fra <see cref="forste"/>.Start
        // og HELT TIL institusjonsordets slutt (ikke til denne metodens returnerte Lengde) — å bare
        // fjerne det dinglende bindeordet her og falle tilbake til et tidligere token ville derfor
        // uansett re-inkludert bindeordet (og mellomrommet) i den fangede teksten, siden det ligger
        // FYSISK MELLOM det gjenværende tokenet og institusjonsordet. Bekreftet i live data (dette
        // sveipets skole-relaterte testing): "nivå 5 i NKR og fagskole 1" ga tidligere den falske
        // kandidaten "NKR og fagskole" (der "NKR" er en forkortelse — nivåbetegnelse i det nasjonale
        // kvalifikasjonsrammeverket — og "og" her er en ekte, urelatert setningskonjunksjon, ikke et
        // navneinternt bindeord), og "SFO og skole" (samme mønster, "SFO" og "skole" er to separate
        // generiske substantiv koordinert med "og", ikke ett institusjonsnavn). Denne bug'en fantes
        // allerede før dagens skole-utvidelse (allerede i "Møre og Romsdal fylkeskommune"-mekanismen fra
        // forrige runde), men ble aldri utløst da — ingen tidligere institusjonsord hadde et vanlig
        // forkortelse+"og"-mønster rett foran seg i korpuset.
        if (tokens.Count > 0 && !tokens[^1].StorForbokstav) return null;

        if (tokens.Count == 0) return null;
        if (tokens.Count(t => t.StorForbokstav) > 3 || tokens.Count(t => !t.StorForbokstav) > 1) return null;

        var forste = tokens[0];
        var siste = tokens[^1];
        var sisteOrdSlutt = siste.Start + siste.Lengde;

        return (forste.Start, sisteOrdSlutt - forste.Start);
    }

    /// <summary>
    /// Presisjonsvernet for flerords-mønsteret (analogt <see cref="ErSetningsstart"/> for det brede
    /// stor-bokstav-mønsteret, men IKKE identisk — se hvorfor under). Tillatt kontekst:
    /// <list type="bullet">
    /// <item>Midt i en setning (samme sjekk som <see cref="ErSetningsstart"/> — ikke rett etter et
    /// setningsavsluttende tegn) — alltid tillatt.</item>
    /// <item>Rett etter et setningsavsluttende tegn, MEN kun hvis det tegnet faktisk er en
    /// liste-linje-markør (bokstav/tall + punktum, f.eks. "a." eller "1.") ved starten av GJELDENDE
    /// linje, ikke en ekte setningsslutt — se <see cref="ErListePrefiksVedLinjestart"/>. Uten dette
    /// unntaket ville f.eks. "… se listen. a. Østfold fylkeskommune: …" blitt avvist som "tvetydig
    /// setningsstart".</item>
    /// </list>
    /// <para>
    /// <b>[Rettet, issue #149] ABSOLUTT start av teksten er IKKE lenger tillatt her.</b> Tidligere (fra
    /// kodegjennomgang 2026-08-30 og fram til denne rettelsen) var <paramref name="tekst"/> som begynner
    /// selv med det fangede egennavnet BEVISST tillatt — begrunnet med at AKN-listepunkter importeres
    /// HVER som sin EGEN <c>RettskildeNode</c> uten noen tekstlig liste-markør i selve <c>Tekst</c>
    /// (bokstav-/tallmarkøren er strukturell metadata), slik at "Østfold fylkeskommune: …" står
    /// bokstavelig på posisjon 0 av SIN node (FOR-2019-09-30-1310 §2). Johann bekreftet (issue #149,
    /// full korpus-resveip, ETTER SNL/SSR-berikelsen i PR #97) at nøyaktig DENNE tillatelsen slapp
    /// gjennom et helt annet, uønsket mønster: et vanlig, ikke-egennavn ord som TILFELDIGVIS er første
    /// ord i SIN EGEN node/setning, RETT FØR et institusjonsord — "For tilsyn med at reglene
    /// overholdes, skal …" og "Konkret tilsyn kan gjennomføres når …" ga begge falske
    /// "virksomhet"-kandidater ("For tilsyn", "Konkret tilsyn"), av nøyaktig samme grunn som
    /// <see cref="ErSetningsstart"/>-vernet eksisterer i utgangspunktet: et institusjonsord
    /// rett etter er et sterkt signal på at INSTITUSJONSORDET er ekte, men sier INGENTING om hvorvidt
    /// ORDET FORAN det er et egennavn eller bare tilfeldigvis stor forbokstav ved en setningsåpning —
    /// nøyaktig den ambiguiteten <see cref="ErSetningsstart"/> allerede finnes for å luke ut andre steder
    /// i klassen. Ordet er her, til forskjell fra <see cref="AldriEgennavnOrd"/>s determinativer/pronomen
    /// (lukket ordklasse) OG docs/28s enumererte, men ufullstendige eksempelliste ("For tilsyn", "Ved
    /// tilsyn", "Konkret tilsyn" m.fl. — «Stedlig tilsyn», «Statlig tilsyn» er ÅPEN ordklasse, adjektiv),
    /// IKKE begrenset til en lukket, uttømmelig ordliste — se den forrige (nå fjernede) "GJENVÆRENDE
    /// begrensning"-kommentaren her, som eksplisitt flagget nettopp DENNE svakheten til Johann uten å
    /// fikse den. En ren posisjonsbasert sjekk (som her) løser BEGGE de konkrete, rapporterte eksemplene
    /// uten en ordliste i det hele tatt.
    /// </para>
    /// <para>
    /// <b>Dokumentert, akseptert recall-tap som følge av dette</b>: et EKTE flerords-institusjonsnavn
    /// som (som i FOR-2019-09-30-1310 §2) står bokstavelig som det ALLERFØRSTE i sin egen
    /// <c>RettskildeNode</c> — f.eks. "Østfold fylkeskommune: …" helt uten noen tekstlig liste-markør
    /// foran — fanges IKKE lenger av DETTE mønsteret (samme presisjon-foran-recall-avveining som resten
    /// av klassen). Bevisst, ikke stilltiende: flagget eksplisitt til Johann i PR-en for issue #149 — et
    /// slikt navn kan fortsatt fanges via en AKN-import som beholder selve liste-markøren i teksten (se
    /// <see cref="ErListePrefiksVedLinjestart"/>-grenen over, uendret), via det brede stor-bokstav-
    /// mønsteret hvis navnet også forekommer et annet, IKKE-setningsinnledende sted i korpuset, eller ved
    /// manuell gjennomgang.
    /// </para>
    /// </summary>
    private static bool ErFlerordsKontekstTillatt(string tekst, int index)
    {
        var i = index - 1;
        while (i >= 0 && char.IsWhiteSpace(tekst[i])) i--;
        if (i < 0) return false; // [Rettet, issue #149] absolutt tekststart — se metodekommentaren.
        if (tekst[i] is '.' or '!' or '?') return ErListePrefiksVedLinjestart(tekst, index);
        return true; // midt i en setning (ikke rett etter et setningsavsluttende tegn).
    }

    /// <summary>Er <paramref name="index"/> — etter at en evt. liste-markør er trukket fra — starten av
    /// SIN linje? Ser på teksten fra siste linjeskift (eller tekststart) og fram til <paramref name="index"/>,
    /// og krever at det ENTEN er tomt (rent linjeskift rett før) ELLER består av nøyaktig én kort
    /// bokstav-/tallmarkør + punktum + mellomrom (f.eks. "a. ", "12. ") — IKKE en vilkårlig lengre
    /// tekst som tilfeldigvis ender på et punktum (det ville vært en ekte setningsslutt, ikke en
    /// liste-markør).</summary>
    private static bool ErListePrefiksVedLinjestart(string tekst, int index)
    {
        var linjeStart = index;
        while (linjeStart > 0 && tekst[linjeStart - 1] != '\n') linjeStart--;
        var prefiks = tekst[linjeStart..index];
        return ListePrefiksMønster.IsMatch(prefiks);
    }

    private static readonly Regex ListePrefiksMønster = new(@"^\s*[\p{L}0-9]{1,3}\.\s+$");

    /// <summary>
    /// Skanner bakover fra <paramref name="index"/>, hopper over whitespace, og ser på det første
    /// ikke-whitespace-tegnet før det. Start av teksten ELLER et setningsavsluttende tegn (<c>. ! ?</c>)
    /// rett før → setningsstart. Ren tegnbasert heuristikk (ingen ekte språklig setningsparsing) —
    /// dokumentert begrensning: et paragraf-/leddnummer som "(1) " rett før treffet regnes IKKE som en
    /// setningsavslutning (parentesen er ikke i tegnlisten over), så "(1) Advokattilsynet utsteder …"
    /// telles som MIDT i en setning (ikke setningsstart) — et bevisst enkelt valg konsistent med at hele
    /// mekanismen er "ren tekstanalyse (regex)", ikke ekte NLP-setningsgrensededeksjon.
    /// </summary>
    private static bool ErSetningsstart(string tekst, int index)
    {
        var i = index - 1;
        while (i >= 0 && char.IsWhiteSpace(tekst[i])) i--;
        return i < 0 || tekst[i] is '.' or '!' or '?';
    }

    /// <summary>Idempotent — samme (rettskilde, node, START-posisjon) gir samme rad tilbake i stedet
    /// for et duplikat, uansett status (samme mønster som <see cref="VirksomhetKandidatTjeneste.OpprettEllerFinnAsync"/>).
    /// <para>
    /// [Ny, 2026-09-03] <paramref name="initialStatus"/> — default <c>"Venter"</c>, samme implisitte
    /// standard som FØR denne parameteren fantes (alle eksisterende kallere er derfor uendret). Lagt til
    /// for <see cref="SveipAsync"/>s to-utfalls klassifisering (se <see cref="KlassifiserAsync"/>):
    /// et <c>"virksomhet"</c>-treff SNL/SSR ikke bekrefter skal opprettes DIREKTE med
    /// <c>Status = "Avvist"</c> — synlig/revisjonsbart i køen, ikke stille forkastet FØR raden i det
    /// hele tatt eksisterte. Validert mot samme lukkede statusmengde som resten av klassen bruker.
    /// </para></summary>
    public async Task<NavnekandidatEntitet> OpprettEllerFinnAsync(
        string foreslattTekst, string kategori, Guid rettskildeId, string nodeEid, int startOffset, int endOffset,
        string opprettetAv, CancellationToken ct = default, string? oppdagelsesKilde = null, string initialStatus = "Venter")
    {
        var eksisterende = await db.Navnekandidater.FirstOrDefaultAsync(
            k => k.RettskildeId == rettskildeId && k.NodeEid == nodeEid && k.StartOffset == startOffset, ct);
        if (eksisterende is not null) return eksisterende;

        if (kategori is not ("virksomhet" or "gruppe"))
        {
            throw new ArgumentException($"Ukjent kategori '{kategori}'. Gyldige verdier: 'virksomhet', 'gruppe'.");
        }
        if (initialStatus is not ("Venter" or "Avvist"))
        {
            throw new ArgumentException($"Ugyldig initialStatus '{initialStatus}'. Gyldige verdier: 'Venter', 'Avvist'. Ingen gjettet fallback.");
        }
        var node = await db.RettskildeNoder.FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == nodeEid, ct);
        if (node is null)
        {
            throw new ArgumentException($"Fant ingen rettskilde-node med eId '{nodeEid}' i rettskilde '{rettskildeId}'. Ingen gjettet fallback.");
        }
        if (endOffset <= startOffset || startOffset < 0 || endOffset > (node.Tekst?.Length ?? 0))
        {
            throw new ArgumentException(
                $"Ugyldig tegnintervall [{startOffset}, {endOffset}) for node '{nodeEid}' (tekstlengde {node.Tekst?.Length ?? 0}).");
        }

        var kandidat = new NavnekandidatEntitet
        {
            Id = Guid.NewGuid(),
            ForeslattTekst = foreslattTekst,
            Kategori = kategori,
            RettskildeId = rettskildeId,
            NodeEid = nodeEid,
            StartOffset = startOffset,
            EndOffset = endOffset,
            Status = initialStatus,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
            OppdagelsesKilde = oppdagelsesKilde,
        };
        db.Navnekandidater.Add(kandidat);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // [Rettet, kodegjennomgang 2026-08-30] Sjekk-så-sett mot en unik DB-indeks
            // (ux_navnekandidater_rettskilde_node_start) uten låsing — to overlappende sveip kan begge
            // passere FirstOrDefaultAsync-sjekken over før noen av dem committer. I stedet for en
            // ufanget 500 til klienten: den andre skriveren har allerede vunnet, hent OG returner DEN
            // raden — samme idempotente utfall som om sjekken over hadde funnet den først.
            db.Entry(kandidat).State = EntityState.Detached;
            var vantLopet = await db.Navnekandidater.FirstOrDefaultAsync(
                k => k.RettskildeId == rettskildeId && k.NodeEid == nodeEid && k.StartOffset == startOffset, ct);
            if (vantLopet is not null) return vantLopet;
            throw; // reelt uventet — verken vår rad eller en konkurrerende rad finnes, kast videre
        }
        return kandidat;
    }

    /// <summary>Full liste, valgfritt filtrert på status og/eller kategori. <paramref name="status"/> =
    /// <c>null</c> betyr ALLE statuser (samme eksplisitte "ingen stille standard"-mønster som
    /// <see cref="VirksomhetKandidatTjeneste.ListerAsync"/>).</summary>
    public Task<List<NavnekandidatEntitet>> ListerAsync(
        string? status = null, string? kategori = null, Guid? rettskildeId = null, CancellationToken ct = default)
    {
        var spørring = db.Navnekandidater.AsQueryable();
        if (status is not null) spørring = spørring.Where(k => k.Status == status);
        if (kategori is not null) spørring = spørring.Where(k => k.Kategori == kategori);
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        return spørring.OrderBy(k => k.RettskildeId).ThenBy(k => k.NodeEid).ThenBy(k => k.StartOffset).ToListAsync(ct);
    }

    /// <summary>
    /// Godkjenner kandidaten. Oppførsel avhenger av <see cref="NavnekandidatEntitet.Kategori"/> (se
    /// klassekommentaren på <see cref="NavnekandidatEntitet"/> for HVORFOR):
    /// <list type="bullet">
    /// <item><c>"gruppe"</c> — oppretter et EKTE gruppebegrep direkte
    /// (<see cref="VirksomhetsbegrepTjeneste.OpprettGruppebegrepAsync"/>, <c>Term</c>=<see cref="NavnekandidatEntitet.ForeslattTekst"/>,
    /// <c>LovkildeId</c>=kandidatens <see cref="NavnekandidatEntitet.RettskildeId"/>) — alt godkjenningen
    /// trenger er allerede kjent fra selve kandidaten. [Rettet, 2026-08-30] Sender også med
    /// <see cref="NavnekandidatEntitet.NodeEid"/> som gruppebegrepets <c>LovreferanseEid</c> — uten
    /// dette var det umulig å se, fra selve paragrafen, at gruppebegrepet stammer derfra (Johann
    /// observerte at «Statsforvalteren» ikke viste seg tagget i vergemålsforskriften § 19 ledd 1,
    /// der den faktisk ble funnet).</item>
    /// <item><c>"virksomhet"</c> — oppretter INGEN <see cref="BegrepEntitet"/>. Godkjenning her betyr
    /// kun "reelt navn, verdt å følge opp" — selve koblingen til en konkret <see cref="Virksomhet"/>
    /// (ny eller eksisterende) krever et menneske og skjer via den eksisterende
    /// navneform-tilleggsflyten i <c>VirksomhetDetalj.tsx</c>/<c>VirksomhetsbegrepTjeneste.OpprettVirksomhetsbegrepAsync</c>.</item>
    /// </list>
    /// Hvis gruppebegrep-opprettelsen kaster (f.eks. en rad med samme (Term, LovkildeId) allerede finnes
    /// — <see cref="VirksomhetsbegrepTjeneste.OpprettGruppebegrepAsync"/> sitt eget "ingen gjettet
    /// fallback"-vern), forblir kandidatens status <c>"Venter"</c> og feilen forplantes uendret —
    /// samme "ikke sett status før den faktiske handlingen lyktes"-prinsipp som
    /// <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/>.
    /// <para>
    /// [Ny, 2026-09-03] Kun rader med <c>Status == "Venter"</c> kan godkjennes (uendret vern, se under)
    /// — en rad <see cref="SveipAsync"/> opprettet DIREKTE som <c>"Avvist"</c> (SNL/SSR bekreftet den
    /// ikke, se <see cref="KlassifiserAsync"/>) kan derfor IKKE godkjennes her uten videre. Dette er en
    /// reell, dokumentert begrensning (ingen "gjenåpne"-endepunkt finnes i denne runden) — en
    /// saksbehandler som er UENIG i en automatisk avvisning kan slette raden (<see cref="SlettAsync"/>,
    /// fungerer uansett status) og eventuelt legge navnet til manuelt via den eksisterende
    /// navneform-tilleggsflyten. Ikke løst her — utenfor denne restruktureringens scope.
    /// </para>
    /// <para>
    /// <b>[Ny, tekst-tagg-departement-eierskap, 2026-08-31] Ekte <see cref="TekstTaggEntitet"/> for
    /// BEGGE kategorier:</b> Johanns eksplisitte designvalg — et gruppebegrep/navneform funnet her er
    /// delt/nasjonalt (ingen eiende virksomhet på selve <see cref="BegrepEntitet"/>), men
    /// <see cref="TekstTaggEntitet.VirksomhetId"/> er ikke-nullbar ("en tagg er alltid en virksomhets
    /// eget arbeidsprodukt"). Løsningen: opprett taggen med <see cref="TekstTaggEntitet.VirksomhetId"/>
    /// = virksomheten til rettskildens <see cref="RettskildeEntitet.AnsvarligDepartement"/> ("det eies
    /// av ansvarlig departement [...] men det skal jo være mulig å se taggene allikevel — opprett
    /// disse med virksomheten til departementet"). <see cref="TekstTaggEntitet.RefId"/> settes til det
    /// NYE gruppebegrepets id for <c>"gruppe"</c> (samme <c>Kind="begrep"</c>-mønster som
    /// <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/> allerede bruker for navneform-treff), og
    /// forblir <c>null</c> for <c>"virksomhet"</c> (INGEN Begrep-rad opprettes for den kategorien i det
    /// hele tatt her — "ingen gjettet fallback": ingen fabrikert id å peke på). Se
    /// <see cref="OpprettDepartementTaggHvisMuligAsync"/> for selve implementasjonen, inkl. når INGEN
    /// tagg opprettes (ukjent/uoppløsbart departement — en reell, dokumentert begrensning, ikke noe å
    /// arbeide rundt). Denne siden-effekten kan ALDRI hindre selve godkjenningen: en stale
    /// node/tekstposisjon (rettskilden endret siden sveipet) degraderer til "ingen tagg", ikke en kastet
    /// feil — til forskjell fra <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/>, der taggen ER
    /// selve hovedformålet med godkjenningen.
    /// </para>
    /// </summary>
    public async Task<NavnekandidatEntitet?> GodkjennAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.Navnekandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        if (kandidat.Status != "Venter")
        {
            throw new ArgumentException(
                $"Kandidaten har status '{kandidat.Status}' — kan kun godkjenne kandidater med status 'Venter'.");
        }

        Guid? refIdForTagg = null;
        if (kandidat.Kategori == "gruppe")
        {
            var gruppebegrep = await virksomhetsbegrep.OpprettGruppebegrepAsync(
                kandidat.RettskildeId, kandidat.ForeslattTekst, behandletAv, kandidat.NodeEid, ct);
            refIdForTagg = gruppebegrep.Id;
        }
        // "virksomhet": ingen Begrep-entitet opprettes her — se metodekommentaren.

        await OpprettDepartementTaggHvisMuligAsync(kandidat, refIdForTagg, behandletAv, ct);

        kandidat.Status = "Godkjent";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>
    /// Oppretter den faktiske <see cref="TekstTaggEntitet"/>-forekomsten for en godkjent kandidat, eid
    /// av virksomheten til rettskildens <see cref="RettskildeEntitet.AnsvarligDepartement"/> — se
    /// <see cref="GodkjennAsync"/> sin metodekommentar for designvalget. Oppretter INGEN tagg (returnerer
    /// stille) hvis:
    /// <list type="bullet">
    /// <item>rettskilden ikke har noe kjent <see cref="RettskildeEntitet.AnsvarligDepartement"/>, ELLER</item>
    /// <item>departementstrengen ikke løser til noen ekte <see cref="Virksomhet"/>-rad
    /// (<see cref="VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnAsync"/> — gjenbrukt, ikke duplisert,
    /// samme mekanisme som <c>RettskildeRepository</c> bruker for "Ansvarlig for"-visningen), ELLER</item>
    /// <item>noden/tekstintervallet ikke lenger er gyldig (rettskilden endret siden sveipet) — degraderer
    /// til "ingen tagg" i stedet for å kaste, se <see cref="GodkjennAsync"/>s kommentar for hvorfor.</item>
    /// </list>
    /// <paramref name="refId"/> kobles inn via <see cref="TekstTaggTjeneste.KobleTilEntitetAsync"/> KUN
    /// når den er satt (kun for <c>"gruppe"</c> — se <see cref="GodkjennAsync"/>); for <c>"virksomhet"</c>
    /// opprettes taggen med <c>RefId=null</c>, samme "ubundet inntil videre"-tilstand som
    /// <see cref="TekstTaggTjeneste.OpprettAsync"/> selv dokumenterer.
    /// </summary>
    private async Task OpprettDepartementTaggHvisMuligAsync(
        NavnekandidatEntitet kandidat, Guid? refId, string behandletAv, CancellationToken ct)
    {
        var ansvarligDepartement = await db.Rettskilder
            .Where(r => r.Id == kandidat.RettskildeId)
            .Select(r => r.AnsvarligDepartement)
            .FirstOrDefaultAsync(ct);
        if (ansvarligDepartement is null) return; // ukjent departement — ingen gjettet fallback.

        var departementVirksomhetId = await virksomhetOppslag.FinnVirksomhetIdForNavnAsync(ansvarligDepartement);
        if (departementVirksomhetId is null) return; // uoppløsbart departement — ingen gjettet fallback.

        var node = await db.RettskildeNoder.FirstOrDefaultAsync(
            n => n.RettskildeId == kandidat.RettskildeId && n.Eid == kandidat.NodeEid, ct);
        var tekst = node?.Tekst;
        if (tekst is null
            || kandidat.StartOffset < 0 || kandidat.EndOffset > tekst.Length || kandidat.EndOffset <= kandidat.StartOffset)
        {
            // Noden finnes ikke lenger, eller intervallet er ikke lenger gyldig (rettskilden er
            // reimportert/endret siden sveipet) — degraderer til "ingen tagg" i stedet for å kaste, se
            // GodkjennAsync sin metodekommentar for hvorfor dette IKKE skal hindre selve godkjenningen.
            return;
        }

        // Samme 30-tegns kontekstvindu som VirksomhetKandidatTjeneste.GodkjennAsync og klienten
        // (RettskildeDetalj.tsx sin manuelle tagging) bruker for QuotePrefix/QuoteSuffix.
        const int kontekstLengde = 30;
        var quoteExact = tekst[kandidat.StartOffset..kandidat.EndOffset];
        var quotePrefix = tekst[Math.Max(0, kandidat.StartOffset - kontekstLengde)..kandidat.StartOffset];
        var quoteSuffix = tekst[kandidat.EndOffset..Math.Min(tekst.Length, kandidat.EndOffset + kontekstLengde)];

        var tagg = await tekstTaggTjeneste.OpprettAsync(
            kandidat.RettskildeId, departementVirksomhetId.Value, behandletAv, kandidat.NodeEid,
            kandidat.StartOffset, kandidat.EndOffset, quotePrefix, quoteExact, quoteSuffix, "begrep", ct);
        if (tagg is null) return; // racy sletting av node mellom sjekkene over — samme "degrader" som over.

        if (refId is not null)
        {
            await tekstTaggTjeneste.KobleTilEntitetAsync(tagg.Id, refId.Value, behandletAv, ct);
        }
    }

    public async Task<NavnekandidatEntitet?> AvvisAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.Navnekandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        if (kandidat.Status != "Venter")
        {
            throw new ArgumentException(
                $"Kandidaten har status '{kandidat.Status}' — kan kun avvise kandidater med status 'Venter'.");
        }
        kandidat.Status = "Avvist";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>
    /// [Ny, 2026-08-30] Ekte sletting (<c>Remove</c>, IKKE soft-delete) av ÉN kandidatrad, uansett
    /// status. Til forskjell fra <see cref="VirksomhetKandidatTjeneste.HardslettAvvistAsync"/> (som KUN
    /// tillater hardsletting av <c>'Avvist'</c>-rader, docs/20 §2.6) er det HER ingen slik begrensning:
    /// <see cref="NavnekandidatEntitet"/> har ingen Entitetsstatus/proveniens-kobling (se klassekommentaren
    /// der) — den er en ren oppdagelseskø, ikke et revisjonsspor. Formålet med sletting (Johann, ytelsestest
    /// av sortering/filtrering-UI-en + de nye flerords-mønsterreglene) krever nettopp å kunne tømme
    /// KORPUSET, inkludert allerede godkjente/avviste rader — den posisjonsbaserte idempotensen i
    /// <see cref="OpprettEllerFinnAsync"/> (<c>RettskildeId</c>, <c>NodeEid</c>, <c>StartOffset</c>) gir
    /// ellers ALDRI en ny rad på en posisjon som allerede har en (selv avvist) kandidat, så et nytt sveip
    /// kan aldri re-evaluere allerede sveipet tekst mot de nye reglene uten ekte sletting her.
    /// </summary>
    public async Task<bool> SlettAsync(Guid id, CancellationToken ct = default)
    {
        var kandidat = await db.Navnekandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return false;
        db.Navnekandidater.Remove(kandidat);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// [Ny, 2026-08-30] Massesletting — samme valgfrie filter-signatur som <see cref="ListerAsync"/>
    /// (status/kategori/rettskildeId, hver <c>null</c> betyr "ingen filtrering på DEN dimensjonen", samme
    /// eksplisitte "ingen stille standard"-mønster). Lar Johann slette f.eks. kun kandidatene for ÉN
    /// rettskilde (for et avgrenset ytelsestest-sveip på nytt), i stedet for kun "alt eller ingenting".
    /// Ekte sletting, samme begrunnelse som <see cref="SlettAsync"/>. Returnerer antall slettede rader,
    /// slik at klienten kan bekrefte at det faktiske antallet stemte med det som ble varslet før kallet.
    /// </summary>
    public async Task<int> SlettAlleAsync(
        string? status = null, string? kategori = null, Guid? rettskildeId = null, CancellationToken ct = default)
    {
        var spørring = db.Navnekandidater.AsQueryable();
        if (status is not null) spørring = spørring.Where(k => k.Status == status);
        if (kategori is not null) spørring = spørring.Where(k => k.Kategori == kategori);
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        var rader = await spørring.ToListAsync(ct);
        db.Navnekandidater.RemoveRange(rader);
        await db.SaveChangesAsync(ct);
        return rader.Count;
    }
}

/// <summary>Oppsummering av ett sveip — <see cref="AntallTreffFunnet"/> teller ALLE mønstertreff (også
/// de som allerede fantes som kandidat fra et tidligere sveip, eller som ble filtrert bort fordi de
/// allerede er dekket av et eksisterende Begrep — se <see cref="NavnekandidatOppdagelseTjeneste.SveipAsync"/>
/// for at "dekket"-filtreringen skjer FØR denne telles opp, altså telles et dekket treff IKKE med her),
/// <see cref="AntallNyeKandidater"/> kun de som faktisk ble en NY rad i køen denne kjøringen — uavhengig
/// av om den nye raden ble opprettet som <c>"Venter"</c> eller <c>"Avvist"</c> (se
/// <see cref="NavnekandidatOppdagelseTjeneste.KlassifiserAsync"/>).</summary>
public sealed record NavnekandidatSveipResultat(int AntallTreffFunnet, int AntallNyeKandidater);
