using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Sveipefunksjonen (docs/24 §3/§4, byggerekkefølgens steg 3) — mønstergjenkjenning KUN over
/// allerede-importerte <see cref="RettskildeNodeEntitet"/>-rader, ALDRI rå HTML/PDF (docs/24, gjentatt
/// flere steder som et grunnprinsipp). Dekker de to mønstrene spesifikasjonen selv anbefaler å starte
/// med ("høyest konfidens, strukturelt enklest", docs/24 §3):
/// <list type="bullet">
/// <item><b>M1 — eksplisitt definisjonsliste:</b> en <c>ledd</c>-node som enten (a) selv inneholder
/// intro-frasen "... menes med" (typisk "I forskriften/loven her menes med:"), ELLER (b) er første ledd
/// under en <c>paragraf</c> hvis <c>Overskrift</c> inneholder "definisjon" (typisk "Definisjoner") —
/// etterfulgt av direkte <c>punkt</c>-barn av formen <c>"term: forklaring"</c>. Validert konkret mot
/// FOR-2015-06-25-793 (pasientreiseforskriften) § 1 — se <see cref="BegrepsoppdagelseSveipTjenesteTests"/>.</item>
/// <item><b>M11 — egen definisjonsparagraf uten punktliste:</b> en <c>paragraf</c>-node hvis
/// <c>Overskrift</c> ER selve termen, og hvis FØRSTE <c>ledd</c>-barns <c>Tekst</c> inneholder den
/// eksplisitte markøren "Med {term} menes/forstås/regnes" — samme "menes"-familie som M1s intro-frase,
/// bare entall/singel-term i stedet for en innledning til en liste. Validert mot folketrygdloven
/// §§ 1-8, 1-9, 1-10, 13-3 (bekreftet i den kjørende dev-databasen, 2026-09-02: "Med arbeidstaker menes
/// i denne loven ...", "Med frilanser menes ...", "Med selvstendig næringsdrivende menes ...", "Med
/// yrkesskade menes ...").</item>
/// </list>
/// <para>
/// <b>Hvorfor markør-KRAV, ikke bare "Overskrift er et kort substantiv":</b> uten et eksplisitt
/// "menes/forstås/regnes"-krav ville M11 truffet enhver kort paragraf-overskrift (f.eks. "Formål",
/// "Grunnbeløpet", "Sluttpoengtallet") uansett om selve paragrafen faktisk DEFINERER overskriften som
/// term eller bare beskriver noe med det navnet — copula-varianter ("X er ...", uten "menes") er
/// nettopp M13, som docs/24 §4 eksplisitt flagger som høyest falsk-positiv-risiko og derfor UTENFOR
/// scope denne runden. Markør-kravet er det som gjør M11 "strukturelt enkelt, høy konfidens" i praksis,
/// ikke bare i teorien.
/// </para>
/// <para>
/// <b>Scope (docs/24 §2.1) settes alltid til <c>'hele_dokumentet'</c> for begge mønstre denne runden:</b>
/// selve fraseringen begge mønstrene krever ("i forskriften HER", "i denne LOVEN") er allerede en
/// eksplisitt heldokument-erklæring — en finere paragraf-/kapittel-scopet gjenkjenning (for definisjoner
/// som eksplisitt sier "i dette kapitlet") er ikke bygget denne runden, dokumentert forenkling, ikke en
/// stille antakelse.
/// [Presisert, definisjonsmønster-runden, 2026-09-09: begrunnelsen over gjelder «menes»-markørklassene.
/// Verbklassen «beregnes/angir» (issue #214, se eget avsnitt lenger ned) bærer INGEN slik
/// heldokument-erklæring i ordlyden — der er <c>'hele_dokumentet'</c> et dokumentert valg, ikke en
/// tekstlig kjensgjerning.]
/// </para>
/// <para>
/// <b>[Ny, definisjonsmønster-runden, 2026-09-09] Omsluttende anførselstegn («fnutter») er ikke del av
/// termen</b> (issue #168). Norsk lovgivningskonvensjon introduserer ofte en definert term i hermetegn:
/// «Småfe»: sau og geit. (FOR-2022-04-07-636 § 3 første ledd punkt 1). Uttrekket brukte et rått
/// <c>.Trim()</c> og lagret derfor <c>«Småfe»</c> — MED hermetegn — som både <c>Begrep</c> og
/// <c>BegrepOriginal</c>, og siden ingen ledd i godkjenningskjeden sanerer termen
/// (<see cref="BegrepsforekomstTjeneste.GodkjennAsync"/> →
/// <see cref="BegrepsregisterTjeneste.OpprettFraForekomstAsync"/> setter <c>Term = term</c> direkte)
/// ville en godkjenning gitt registeret en rad med synlige hermetegn i termen.
/// <b>Målt live 2026-09-09</b> (<c>GET /api/begrepsforekomster?status=Alle</c>, 6017 rader): 526
/// forekomster inneholder et dobbelt-anførselstegn, 508 av dem som et rent omsluttende par, fordelt på
/// 60 rettskilder og 250 unike termer — ALLE M1, ALLE med status <c>'Venter'</c>. Ingen M11-forekomst er
/// rammet (se avsnittet under om hvorfor M11 aldri KAN produsere en hermetegn-term).
/// </para>
/// <para>
/// <b><see cref="TermSpenn"/> fjerner kun MATCHENDE, omsluttende par</b>, gjentatt, og returnerer et
/// SPENN (start + lengde) i inn-strengen — ikke en ny streng. Spennet er nødvendig, ikke pynt:
/// <see cref="BegrepsforekomstEntitet.StartOffset"/>/<see cref="BegrepsforekomstEntitet.EndOffset"/> må
/// peke på selve ordet, ellers feiler <c>GodkjennAsync</c> sin revalidering
/// (<c>tekst[StartOffset..EndOffset] != BegrepOriginal</c>) — issue #168 punkt 2, som eksplisitt sier at
/// strengen og posisjonene må endres SAMTIDIG.
/// </para>
/// <para>
/// <b>Enkle anførselstegn (<c>'</c>) er BEVISST utenfor tegnsettet</b>
/// (<see cref="AnførselstegnPar"/> har kun dobbelt-varianter: <c>« »</c>, <c>" "</c> og de krumme).
/// Målingen over viste at de to eneste <c>'</c>-forekomstene i korpuset er apostrofer INNE i ekte
/// termer — <c>investigator's brochure</c> (FOR-2009-10-30-1321 § 1-5) og
/// <c>EØS' harmoniseringsregelverk</c> — ikke skilletegn. Å strippe dem ville vært en gjetning som
/// skader ekte termer (CLAUDE.md §8). Av samme grunn står de 18 ASYMMETRISKE tilfellene urørt
/// («god landbrukspraksis» (GAP), «filial» av en finans- eller kredittinstitusjon, Skiller i klasse «B»,
/// …): der er hermetegnene ikke omsluttende, og hva termen «egentlig» er ville måtte gjettes. De er
/// rapportert som egne funn, ikke løst stille her.
/// </para>
/// <para>
/// <b>Issue #168s rotårsak-påstand for M11 stemmer ikke helt, og det som faktisk feiler er en FALSK
/// NEGATIV.</b> Issuet peker på <c>var term = paragraf.Overskrift!.Trim()</c> som samme feil som i M1.
/// Men den strengen brukes kun til å BYGGE søke-regexen; <c>BegrepOriginal</c> tas fra regexens
/// treffgruppe i selve leddteksten. En overskrift med hermetegn ga derfor aldri en skitten term — den ga
/// INGEN treff i det hele tatt, fordi <c>\bMed\s+«Arbeidstaker»\s+menes</c> ikke matcher «Med
/// arbeidstaker menes». Rettingen her er derfor to ting: overskriften renses før regexen bygges, OG
/// regexen tåler valgfrie hermetegn rundt termen i teksten (<see cref="ÅpneFnuttValgfri"/>/
/// <see cref="LukkeFnuttValgfri"/>) uten å ta dem med i treffgruppen. Defensivt: 0 målte M11-tilfeller
/// i korpuset i dag.
/// </para>
/// <para>
/// <b>Avvist alternativ:</b> issue #168 punkt 3 foreslår en «sekundær sikring» i
/// <see cref="BegrepsregisterTjeneste"/> som avviser en <c>Term</c> med hermetegn i endene. Ikke gjort.
/// <c>ValiderFelter</c> deles av <see cref="BegrepsregisterTjeneste.OpprettAsync"/> (manuell,
/// menneskeskrevet term), <see cref="BegrepsregisterTjeneste.OpprettForslagFraKiAsync"/> og
/// <see cref="BegrepsregisterTjeneste.OpprettFraForekomstAsync"/> — en ny avvisning der ville endret
/// oppførselen på tre kall-veier, inkludert den der et menneske med vilje kan ha skrevet hermetegn, for
/// å beskytte mot en feil hvis rotårsak ligger i uttrekket. Fikset på åstedet i stedet.
/// </para>
/// <para>
/// <b>[Ny, definisjonsmønster-runden, 2026-09-09] M11 dekker nå TO markørklasser, ikke én</b> (issue
/// #214): den opprinnelige «menes/forstås/regnes»-familien (<see cref="M11MarkørOrd"/>, konfidens
/// <c>'hoy'</c>) OG en verbklasse «beregnes/angir» (<see cref="M11VerbMarkørOrd"/>, konfidens
/// <c>'lav'</c>). Byggteknisk forskrift definerer BYA/%-BYA/BRA/%-BRA nøyaktig som M11 strukturelt —
/// paragrafoverskriften ER termen, første definerende ledd definerer den — men med et annet verb:
/// «Bebygd areal beregnes etter Norsk Standard NS 3940:2012 …», «Prosent bebygd areal angir forholdet
/// mellom …». Verifisert live mot den kjørende dev-databasen 2026-09-09
/// (<c>eb6e5cef-e062-4f52-9370-9be9c03c6518</c> §§ 5-2, 5-3, 5-4, 5-5).
/// </para>
/// <para>
/// <b>Hvorfor SAMME <c>MonsterId</c> ("M11") og ikke en egen id — issue #214 akseptansekriterium 5,
/// besluttet her:</b> mønsterkatalogen M1–M17 finnes IKKE i repoet (bekreftet: docs/24 navngir M1, M2,
/// M3, M4, M5, M6, M8, M9, M10, M11, M13, M14, M15, M16 og M17, men M7 og M12 er ikke omtalt noe sted
/// i <c>docs/</c> eller koden). CHECK-constrainten
/// <c>ck_begrepsforekomster_monster_id</c> er dessuten et LUKKET sett <c>'M1'…'M17'</c>, så en egen id
/// måtte enten vært M7 eller M12 — altså en GJETNING om hvilket mønster katalogen har tildelt de to
/// numrene (CLAUDE.md §8, og #214 sier selv «avgjøres mot katalogen, ikke gjettes») — eller en helt ny
/// id utenfor settet, som krever en migrasjon (eid av en annen agent denne bølgen). Verbklassen landes
/// derfor som M11 med <c>Kildetype='egen_paragraf'</c>, og den separate tuningen/telemetrien #214 ber om
/// kommer fra <see cref="BegrepsforekomstEntitet.Konfidens"/> i stedet: innenfor M11 er
/// <c>'hoy'</c> nå entydig «menes»-familien og <c>'lav'</c> entydig verbklassen, og
/// <c>GET /api/begrepsforekomster?konfidens=lav</c> filtrerer på det. Får vi katalogen og en id faktisk
/// er ledig, er splittingen én konstant her pluss (hvis utenfor M1–M17) en CHECK-migrasjon.
/// </para>
/// <para>
/// <b>Hvorfor verbklassen er <c>'lav'</c> og ikke <c>'hoy'</c>:</b> «menes» ER en erklæring om betydning;
/// «beregnes» er like ofte en pliktregel. Samme forskrift inneholder to reelle motbevis — § 14-1 tredje
/// ledd «U-verdier skal beregnes som gjennomsnitt …» og § 14-2 femte ledd «For yrkesbygning skal det
/// beregnes et energibudsjett …». Begge stoppes av den strukturelle vakten (overskriftene er «Generelle
/// krav» / «Krav til energieffektivitet», altså IKKE termen foran markøren), men de viser at vakten er
/// nødvendig, ikke tilstrekkelig — derfor lav konfidens, aldri automatisk opptak.
/// </para>
/// <para>
/// <b>Tre presiseringer verbklassen krever, som «menes»-grenen ikke gjør:</b>
/// <list type="number">
/// <item><b>Alle ledd, ikke bare det første.</b> § 5-4 «Bruksareal (BRA)» har definisjonen i ANDRE ledd
/// («(2) Bruksareal beregnes etter …»); FØRSTE ledd er «(1) Bruksareal for bebyggelse på en tomt skrives
/// m2-BRA og angis i hele tall.» Verbgrenen leter derfor gjennom alle direkte <c>ledd</c>-barn og tar det
/// FØRSTE som matcher. «Menes»-grenen er bevisst IKKE endret (den ser fortsatt kun på første ledd) — å
/// utvide den ville endret et allerede validert treffsett i en runde som handler om noe annet.</item>
/// <item><b>Termen må stå ved LEDDETS START, ev. etter et «(n) »-leddnummer</b> — det er den
/// operasjonelle formen av #214s «termen må stå som subjekt umiddelbart foran markøren». Alle fire ekte
/// treffene oppfyller det. Kravet er samtidig det som skiller definisjonen fra pliktregelen: «U-verdier
/// SKAL beregnes» bryter naboskapet mellom term og markør, og «Bruksareal for bebyggelse … og angis i
/// hele tall» gjør det samme.</item>
/// <item><b>Overskriftens hale-parentes fjernes før matching</b> (<see cref="ParentesHaleMønster"/>):
/// overskriften er «Bebygd areal (BYA)», teksten sier «Bebygd areal». <b>Kortformen «BYA»/«%-BYA»/«BRA»
/// lagres IKKE noe sted i denne runden</b> — det er en eksplisitt, dokumentert avgrensning (#214
/// akseptansekriterium 4, andre alternativ), ikke en stille forkasting: et kortform-/alias-felt finnes
/// ikke på <see cref="BegrepsforekomstEntitet"/>, og å legge det til krever en migrasjon (eid av en annen
/// agent denne bølgen). Kortform-som-egen-navneform er verdt en egen runde. Hale-parentesen fjernes
/// bevisst KUN i verbgrenen, av samme grunn som punkt 1.</item>
/// </list>
/// </para>
/// <para>
/// <b>En paragraf gir aldri både et «menes»-treff og et verbtreff</b> — traff «menes»-grenen først,
/// hopper verbgrenen over paragrafen. Et lavkonfidens-duplikat av en definisjon som allerede er fanget
/// med høy konfidens er ren støy i køen, ikke ny informasjon.
/// </para>
/// <para>
/// <b>Scope for verbklassen er <c>'hele_dokumentet'</c> — et dokumentert valg, ikke en tekstlig
/// kjensgjerning.</b> «Menes»-mønstrene bærer selve heldokument-erklæringen i ordlyden («i forskriften
/// her», «i denne loven»). Verbklassen gjør IKKE det: «Bebygd areal beregnes etter …» sier ingenting om
/// rekkevidde. Alternativet <c>'paragraf'</c> + <c>ScopeRefEid</c> ble vurdert og forkastet fordi det
/// ville påstått en avgrensning teksten heller ikke uttaler, og fordi BYA/BRA i praksis brukes gjennom
/// hele byggteknisk forskrift. Verdisettet har ingen «ukjent», så valget må tas — at det ER et valg er
/// en av grunnene til at konfidensen er lav. Åpent spørsmål rapportert til Johann.
/// </para>
/// <para>
/// <b>Delt/nasjonal + gjeldende scoping</b> — samme defensive filter som
/// <see cref="VirksomhetKandidatSveipTjeneste"/>/<see cref="NavnekandidatOppdagelseTjeneste"/> allerede
/// bruker (og som begge måtte rettes til EKSPLISITT etter reelle kryssvirksomhet-lekkasjer, Agder/Bergen
/// 2026-08-22 og gjentatt 2026-08-30): kun <c>Rettskilde.VirksomhetId == null &amp;&amp;
/// Entitetsstatus == "gjeldende"</c> sveipes. IKKE eksplisitt påkrevd av docs/24 selv — et bevisst,
/// forebyggende designvalg i denne runden, for å ikke gjenta samme feilklasse en tredje gang.
/// </para>
/// </summary>
public sealed class BegrepsoppdagelseSveipTjeneste(RegelIdeDbContext db, BegrepsforekomstTjeneste forekomstkø)
{
    /// <summary>Paragraf-overskrift-signalet for M1 (docs/24 §1.3: "typisk med overskrift
    /// 'Definisjoner'/tilsvarende"). Enkelt "inneholder"-sjekk, ikke eksakt likhet — dekker også f.eks.
    /// "Definisjoner og forkortelser".</summary>
    private static readonly Regex M1ParagrafOverskriftMønster = new("definisjon", RegexOptions.IgnoreCase);

    /// <summary>Ledd-tekst-signalet for M1 (docs/24 §1.3: "innledende tekst som 'I forskriften/loven her
    /// menes med:'"). Krever at frasen står HELT på slutten av leddets tekst (ev. med etterfølgende
    /// kolon) — akkurat der punktlisten begynner, ikke en tilfeldig forekomst av ordene midt i en lengre
    /// setning.</summary>
    private static readonly Regex M1LeddTekstMønster = new(@"\bmenes med\s*:?\s*$", RegexOptions.IgnoreCase);

    /// <summary>De eksplisitte definisjons-markørene M11 krever rett etter selve termen (samme
    /// "menes"-familie som <see cref="M1LeddTekstMønster"/>, men entall/singel-term). "forstås"/"regnes"
    /// er ekte, brukte varianter i norsk lovtekst ved siden av "menes" — IKKE copula ("X er ...", det er
    /// M13, se klassekommentaren).</summary>
    private const string M11MarkørOrd = "menes|forst[åa]s|regnes";

    /// <summary>[Ny, definisjonsmønster-runden, 2026-09-09, issue #214] Verbklassen som definerer uten å
    /// erklære betydning — «Bebygd areal BEREGNES etter …», «Prosent bebygd areal ANGIR forholdet mellom
    /// …» (byggteknisk forskrift §§ 5-2 til 5-5, verifisert live 2026-09-09). Holdt som et EGET sett fra
    /// <see cref="M11MarkørOrd"/> nettopp fordi de to klassene har ulik konfidens og ulik strukturell
    /// vakt — se klassekommentaren.
    /// <para>
    /// Bevisst kun disse to verbene, ikke en bredere «definerende verb»-klasse: de er de to som faktisk
    /// er MÅLT i korpuset (issue #214s fire treff). «angis»/«fastsettes»/«måles» er IKKE med — «angis»
    /// står i samme forskrift i ren pliktform («… og angis i hele tall», § 5-2 andre setning), og å ta
    /// dem inn på antakelse er nøyaktig den whack-a-mole-utvidelsen docs/31 §9 advarer mot.
    /// </para></summary>
    private const string M11VerbMarkørOrd = "beregnes|angir";

    /// <summary>[Ny, definisjonsmønster-runden, 2026-09-09, issue #168] Anførselstegn-PAR som regnes som
    /// omsluttende sitatmarkering rundt en term, ikke som del av termen. Kun dobbelt-varianter:
    /// <c>« »</c> (målt 527/526 ganger i korpuset 2026-09-09) og <c>" "</c> pluss de krumme
    /// typografiske variantene av samme tegnklasse. Enkelt anførselstegn/apostrof er BEVISST utelatt —
    /// se klassekommentaren for de to målte ekte termene som ville blitt ødelagt.</summary>
    private static readonly (char Åpne, char Lukke)[] AnførselstegnPar =
    [
        ('«', '»'), ('"', '"'), ('“', '”'), ('”', '”'), ('„', '“'),
    ];

    /// <summary>Valgfritt ÅPNENDE anførselstegn i en regex, UTENFOR treffgruppen — slik at
    /// «Med «arbeidstaker» menes …» matcher uten at hermetegnene havner i
    /// <see cref="BegrepsforekomstEntitet.BegrepOriginal"/> eller i tegn-intervallet (issue #168).</summary>
    private const string ÅpneFnuttValgfri = "[«“„\"]?";

    /// <summary>Valgfritt LUKKENDE anførselstegn, motstykket til <see cref="ÅpneFnuttValgfri"/>.</summary>
    private const string LukkeFnuttValgfri = "[»”“\"]?";

    /// <summary>[Ny, definisjonsmønster-runden, 2026-09-09, issue #214 akseptansekriterium 4] Hale-parentes
    /// i en paragrafoverskrift — «Bebygd areal (BYA)» → «Bebygd areal», siden lovteksten selv skriver
    /// termen uten forkortelsen. Kun ÉN hale-parentes uten nøsting (<c>[^()]*</c>), kun helt til slutt:
    /// en parentes MIDT i en overskrift er ikke en kortform og skal ikke røres. Brukes KUN av
    /// verbgrenen — se klassekommentaren.</summary>
    private static readonly Regex ParentesHaleMønster = new(@"\s*\([^()]*\)\s*$");

    /// <summary>Øvre lengdegrense for en M11-kandidat-Overskrift — en ren, enkelt-term-overskrift
    /// ("Arbeidstaker", "Yrkesskade") er alltid kort. Utelukker samtidig lange, sammensatte
    /// paragraf-titler som ikke er egnet som en enkelt term (defensivt, reduserer unødvendig regex-arbeid
    /// mer enn det faktisk endrer treffsettet — markør-kravet under er den reelle presisjons-vokteren).</summary>
    private const int M11MaksOverskriftLengde = 60;

    public async Task<BegrepsoppdagelseSveipResultat> SveipAsync(Guid? rettskildeId, string opprettetAv, CancellationToken ct = default)
    {
        if (rettskildeId is not null && !await db.Rettskilder.AnyAsync(
                r => r.Id == rettskildeId && r.VirksomhetId == null && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException(
                $"Fant ingen gjeldende, delt/nasjonal rettskilde med id '{rettskildeId}'. Ingen gjettet fallback.");
        }

        var rettskildeIder = rettskildeId is not null
            ? [rettskildeId.Value]
            : await db.Rettskilder.Where(r => r.VirksomhetId == null && r.Entitetsstatus == "gjeldende")
                .Select(r => r.Id).ToListAsync(ct);

        var antallTreff = 0;
        var antallNyeForekomster = 0;
        foreach (var enkeltRettskildeId in rettskildeIder)
        {
            var noder = await db.RettskildeNoder
                .Where(n => n.RettskildeId == enkeltRettskildeId && n.Entitetsstatus == "gjeldende")
                .Select(n => new NodeSnapshot(
                    n.Id, n.ParentNodeId, n.Eid, n.NodeType, n.Overskrift, n.Tekst, n.Sorteringsrekkefolge, n.Opphevet))
                .ToListAsync(ct);

            foreach (var funn in FinnForekomster(noder))
            {
                antallTreff++;
                var forAntall = await db.Begrepsforekomster.CountAsync(
                    k => k.RettskildeId == enkeltRettskildeId && k.NodeEid == funn.NodeEid && k.StartOffset == funn.StartOffset, ct);
                await forekomstkø.OpprettEllerFinnAsync(
                    enkeltRettskildeId, funn.NodeEid, funn.Begrep, funn.BegrepOriginal, funn.Definisjon,
                    funn.Kildetype, funn.MonsterId, funn.Konfidens, funn.Scope, funn.ScopeRefEid,
                    funn.StartOffset, funn.EndOffset, opprettetAv, ct);
                if (forAntall == 0) antallNyeForekomster++;
            }
        }

        return new BegrepsoppdagelseSveipResultat(antallTreff, antallNyeForekomster);
    }

    /// <summary>
    /// [Ny, definisjonsmønster-runden, 2026-09-09, issue #168] Finner SPENNET (start + lengde) til selve
    /// termen inne i <paramref name="raa"/>: ytre blanktegn fjernet, og deretter alle MATCHENDE,
    /// omsluttende anførselstegn-par (<see cref="AnførselstegnPar"/>) fjernet lag for lag, med ny
    /// blanktegn-trimming mellom hvert lag («&#160;«krav»&#160;» → <c>krav</c>).
    /// <para>
    /// <b>Returnerer et spenn, ikke en streng</b>, fordi kallstedene trenger BEGGE: den rensede termen
    /// OG dens posisjon i den opprinnelige teksten, slik at
    /// <see cref="BegrepsforekomstEntitet.StartOffset"/>/<see cref="BegrepsforekomstEntitet.EndOffset"/>
    /// peker på ordet og ikke på hermetegnene (issue #168 punkt 2 — streng og posisjon må endres
    /// samtidig, ellers feiler revalideringen i <c>GodkjennAsync</c>).
    /// </para>
    /// <para>
    /// <b>Krever at BEGGE ender matcher SAMME par</b> — en asymmetrisk streng
    /// («god landbrukspraksis» (GAP), Skiller i klasse «B») står helt urørt. Hva termen «egentlig» er i
    /// slike tilfeller kan bare gjettes, og det gjør vi ikke (CLAUDE.md §8).
    /// </para>
    /// <para>
    /// Lengde 0 er et lovlig svar (<c>«»</c>) — kallstedene skal hoppe over det, ikke finne på en term.
    /// </para>
    /// </summary>
    internal static (int Start, int Lengde) TermSpenn(string raa)
    {
        var start = 0;
        var slutt = raa.Length; // eksklusiv
        var endret = true;
        while (endret)
        {
            endret = false;
            while (start < slutt && char.IsWhiteSpace(raa[start])) start++;
            while (slutt > start && char.IsWhiteSpace(raa[slutt - 1])) slutt--;
            if (slutt - start < 2) break;
            foreach (var (åpne, lukke) in AnførselstegnPar)
            {
                if (raa[start] != åpne || raa[slutt - 1] != lukke) continue;
                start++;
                slutt--;
                endret = true;
                break;
            }
        }
        return (start, slutt - start);
    }

    /// <summary>
    /// Ren, testbar funksjon uten DB-avhengighet — selve M1/M11-mønstergjenkjenningen, separert fra
    /// sveipets DB-orkestrering (samme "internal static, ingen embedded Postgres nødvendig for å teste
    /// selve klassifiseringen" -mønster som <see cref="NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst"/>).
    /// Opererer på ÉN rettskildes fulle node-tre om gangen (<paramref name="noder"/>) — sveipet kaller
    /// denne én gang per rettskilde.
    /// </summary>
    internal static List<ForekomstFunn> FinnForekomster(IReadOnlyList<NodeSnapshot> noder)
    {
        var byId = noder.ToDictionary(n => n.Id);
        var barnAvForelder = noder
            .Where(n => n.ParentNodeId is not null)
            .GroupBy(n => n.ParentNodeId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Sorteringsrekkefolge).ToList());

        var funnet = new List<ForekomstFunn>();

        foreach (var ledd in noder.Where(n => n.NodeType == "ledd" && !n.Opphevet))
        {
            var forelder = ledd.ParentNodeId is not null && byId.TryGetValue(ledd.ParentNodeId.Value, out var p) ? p : null;
            var triggeretAvOverskrift = forelder is { NodeType: "paragraf", Overskrift: not null }
                                        && M1ParagrafOverskriftMønster.IsMatch(forelder.Overskrift);
            var triggeretAvTekst = ledd.Tekst is not null && M1LeddTekstMønster.IsMatch(ledd.Tekst);
            if (!triggeretAvOverskrift && !triggeretAvTekst) continue;

            if (!barnAvForelder.TryGetValue(ledd.Id, out var punktBarn)) continue;
            foreach (var punkt in punktBarn.Where(n => n.NodeType == "punkt" && !n.Opphevet))
            {
                if (punkt.Tekst is null) continue;
                var kolonIndeks = punkt.Tekst.IndexOf(':');
                if (kolonIndeks <= 0) continue; // ingen term funnet, eller teksten starter med kolon.

                var termRaa = punkt.Tekst[..kolonIndeks];
                // [ENDRET, definisjonsmønster-runden, 2026-09-09, issue #168] Var et rått termRaa.Trim()
                // pluss en start-offset regnet fra TrimStart alene — det lagret «Småfe» MED hermetegn
                // som term i 508 målte forekomster. TermSpenn gir både den rensede termen og dens
                // posisjon, slik at tegn-intervallet følger strengen (issue #168 punkt 2).
                var (start, termLengde) = TermSpenn(termRaa);
                if (termLengde == 0) continue;
                var term = termRaa.Substring(start, termLengde);
                var definisjon = punkt.Tekst[(kolonIndeks + 1)..].Trim();
                if (definisjon.Length == 0) continue; // defensivt — "term:" uten forklaring, ingen gjettet fallback.

                var slutt = start + termLengde;
                funnet.Add(new ForekomstFunn(
                    punkt.Eid, term.ToLowerInvariant(), term, definisjon,
                    "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, start, slutt));
            }
        }

        // [Ny, definisjonsmønster-runden, 2026-09-09, issue #214] Paragrafer som alt har gitt et
        // «menes»-treff (konfidens 'hoy') — verbgrenen under hopper over dem, se klassekommentaren.
        var paragrafMedMenesTreff = new HashSet<Guid>();

        var m11Kandidater = noder.Where(n =>
            n.NodeType == "paragraf" && !n.Opphevet && n.Overskrift is not null
            && n.Overskrift.Trim().Length is > 0 and <= M11MaksOverskriftLengde).ToList();

        foreach (var paragraf in m11Kandidater)
        {
            if (!barnAvForelder.TryGetValue(paragraf.Id, out var barn)) continue;
            var forsteLedd = barn.FirstOrDefault(n => n.NodeType == "ledd" && !n.Opphevet);
            if (forsteLedd?.Tekst is null) continue;

            // [ENDRET, definisjonsmønster-runden, 2026-09-09, issue #168] Var Overskrift!.Trim() rått.
            // En overskrift skrevet med hermetegn ga da INGEN treff (regexen lette etter hermetegnene i
            // leddteksten også) — en falsk negativ, ikke en skitten term. Se klassekommentaren.
            var overskriftRaa = paragraf.Overskrift!;
            var (overskriftStart, overskriftLengde) = TermSpenn(overskriftRaa);
            if (overskriftLengde == 0) continue;
            var term = overskriftRaa.Substring(overskriftStart, overskriftLengde);

            // Hermetegnene rundt termen i SELVE TEKSTEN er valgfrie og står utenfor treffgruppen, slik at
            // BegrepOriginal og tegn-intervallet dekker ordet alene.
            var mønster = new Regex(
                $@"\bMed\s+{ÅpneFnuttValgfri}(?<term>{Regex.Escape(term)}){LukkeFnuttValgfri}\s+(?:{M11MarkørOrd})\b",
                RegexOptions.IgnoreCase);
            var treff = mønster.Match(forsteLedd.Tekst);
            if (!treff.Success) continue;

            var termGruppe = treff.Groups["term"];
            var begrepOriginal = termGruppe.Value;
            paragrafMedMenesTreff.Add(paragraf.Id);
            funnet.Add(new ForekomstFunn(
                forsteLedd.Eid, begrepOriginal.ToLowerInvariant(), begrepOriginal, forsteLedd.Tekst,
                "egen_paragraf", "M11", "hoy", "hele_dokumentet", null, termGruppe.Index, termGruppe.Index + termGruppe.Length));
        }

        // [Ny, definisjonsmønster-runden, 2026-09-09, issue #214] M11s verbklasse: «{term}
        // beregnes/angir …» der paragrafoverskriften ER termen. Egen løkke, ikke en utvidelse av
        // «menes»-løkken over, fordi de tre tingene som skiller dem (alle ledd i stedet for bare det
        // første, krav om leddstart, hale-parentes fjernet) alle ville endret «menes»-grenens
        // allerede-validerte treffsett hvis de ble delt. Se klassekommentaren.
        foreach (var paragraf in m11Kandidater)
        {
            if (paragrafMedMenesTreff.Contains(paragraf.Id)) continue;
            if (!barnAvForelder.TryGetValue(paragraf.Id, out var barn)) continue;

            var utenParentes = ParentesHaleMønster.Replace(paragraf.Overskrift!, "");
            var (termStart, termLengde) = TermSpenn(utenParentes);
            if (termLengde == 0) continue;
            var term = utenParentes.Substring(termStart, termLengde);

            // ^ + valgfritt «(n) »-leddnummer: termen må stå som subjekt ved leddets start, umiddelbart
            // foran markøren. Dette ER presisjonsvakten — den er det som skiller «Bruksareal beregnes
            // etter …» (definisjon) fra «U-verdier skal beregnes som gjennomsnitt …» (pliktregel).
            var mønster = new Regex(
                $@"^\s*(?:\(\d+\)\s*)?{ÅpneFnuttValgfri}(?<term>{Regex.Escape(term)}){LukkeFnuttValgfri}\s+(?:{M11VerbMarkørOrd})\b",
                RegexOptions.IgnoreCase);

            foreach (var ledd in barn.Where(n => n.NodeType == "ledd" && !n.Opphevet && n.Tekst is not null))
            {
                var treff = mønster.Match(ledd.Tekst!);
                if (!treff.Success) continue;

                var termGruppe = treff.Groups["term"];
                var begrepOriginal = termGruppe.Value;
                funnet.Add(new ForekomstFunn(
                    ledd.Eid, begrepOriginal.ToLowerInvariant(), begrepOriginal, ledd.Tekst!,
                    "egen_paragraf", "M11", "lav", "hele_dokumentet", null,
                    termGruppe.Index, termGruppe.Index + termGruppe.Length));
                break; // kun det FØRSTE definerende leddet per paragraf — resten er utfyllende regler.
            }
        }

        return funnet;
    }
}

/// <summary>Minimal, flat projeksjon av én <see cref="RettskildeNodeEntitet"/>-rad — kun feltene
/// <see cref="BegrepsoppdagelseSveipTjeneste.FinnForekomster"/> faktisk trenger, slik at den rene
/// klassifiseringsfunksjonen kan testes uten en hel DB-entitet.</summary>
internal sealed record NodeSnapshot(
    Guid Id, Guid? ParentNodeId, string Eid, string NodeType, string? Overskrift, string? Tekst,
    int Sorteringsrekkefolge, bool Opphevet);

/// <summary>Ett M1/M11-treff, klar til å legges i <see cref="BegrepsforekomstTjeneste.OpprettEllerFinnAsync"/>.</summary>
internal sealed record ForekomstFunn(
    string NodeEid, string Begrep, string BegrepOriginal, string Definisjon, string Kildetype, string MonsterId,
    string Konfidens, string Scope, string? ScopeRefEid, int StartOffset, int EndOffset);

/// <summary>Oppsummering av ett sveip — samme "AntallTreffFunnet teller alt, AntallNyeForekomster kun
/// de faktisk nye" -skille som <see cref="VirksomhetKandidatSveipResultat"/>/<see cref="NavnekandidatSveipResultat"/>.</summary>
public sealed record BegrepsoppdagelseSveipResultat(int AntallTreffFunnet, int AntallNyeForekomster);
