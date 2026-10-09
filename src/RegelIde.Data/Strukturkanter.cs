using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #311 «Strukturmodell 6: én typestyrt kanttabell», 2026-10-07, docs/33 §4.3] ÉN kilde for de
/// lukkede vokabularene til <see cref="StrukturkantEntitet"/> — speilet av CHECK-constraintene
/// <c>ck_strukturkanter_kategori</c>/<c>_polaritet</c>/<c>_status</c> i <see cref="RegelIdeDbContext"/> —
/// og for HVILKE nodetyper som kan stå i hver ende av hver kategori.
/// <para>
/// <b>Kategoriene er lukket, typekodene er det ikke</b> (docs/33 §4.3: «samme drift som
/// <c>RelasjonsTypeKonfigurasjon</c> i dag — nye typer er rader, ikke kode»). Åtte kategorier bestemmer
/// hvordan en kant traverseres (arves den? er den geografisk?), og det er kode som leser dem. Typekoden
/// (<c>klageinstans_for</c>, <c>forskrift</c>, <c>medlem_av</c> …) er en rad i
/// <see cref="RelasjonsTypeKonfigurasjonEntitet"/> med <see cref="RelasjonsTypeKonfigurasjonEntitet.Kategori"/>
/// satt — <see cref="Startsett"/> under er bare det som seedes ved oppstart (per (kategori, kode), aldri
/// «er tabellen tom», CLAUDE.md §4).
/// </para>
/// </summary>
public static class Strukturkanter
{
    public const string Relasjon = "R";
    public const string Kompetanse = "K";

    /// <summary>
    /// [Ny, issue #353 «pliktrelasjoner mellom parter», Johanns godkjenning 2026-10-08, <c>[LÅST]</c>] P — PLIKT OVERFOR
    /// MOTPART: «A skal samarbeide med / inngå avtale med / dekke utgiftene / bistå / gi opplysninger til / konsultere B».
    /// Motstykket til <see cref="Kompetanse"/> (K = det A KAN, normativt; P = det A SKAL overfor B). Samme felter som K (fra,
    /// valgfri til, hjemmel, avgrensning, polaritet, status, oppdagelseskilde) pluss <see cref="StrukturkantEntitet.Modalitet"/>.
    /// <para>
    /// <b>P-kanten er den AKTØRNÆRE PROJEKSJONEN av en rettsfølge, ikke plikten selv.</b> Den rike plikt-noden (vilkår, objekt,
    /// unntak, tid, mottaker, begunstiget) er det referansemodellen kaller regel → vilkår → rettsfølge → unntak (docs/01 §7–§8)
    /// og bygges i regellaget (L16). Strukturlaget lagrer bare hvem som skal, overfor hvem, og hvor det står — koblet til
    /// regellaget via eId når det bygges. Ingen parallell plikt-node her (issue #353, tilpasning punkt 2).
    /// </para>
    /// <para>
    /// <b>Gjensidighet registreres som teksten sier den</b> (tilpasning punkt 6): én P-kant per pliktsubjekt (L13). Står begge
    /// parter som pliktsubjekt i teksten, blir det to kanter; et ensidig «A skal samarbeide med B» gir aldri en kant B → A.
    /// </para>
    /// </summary>
    public const string Plikt = "P";
    public const string Medlemskap = "M";
    public const string Omradesammensetning = "O";
    public const string Ansvarsomrade = "A";
    public const string Organtilhorighet = "G";
    public const string Rolleinnehav = "I";
    public const string Klasseniva = "T";

    /// <summary>Alle ni, i docs/33 §4.3-rekkefølge. [ENDRET, issue #353] P (plikt overfor motpart) står rett etter K.</summary>
    public static readonly string[] Kategorier =
        [Relasjon, Kompetanse, Plikt, Medlemskap, Omradesammensetning, Ansvarsomrade, Organtilhorighet, Rolleinnehav, Klasseniva];

    public static readonly string[] Polariteter = ["positiv", "negativ"];

    /// <summary>Samme to-verdis vokabular de tre gamle tabellene hadde (issue #285 AC5, docs/20 §2.7).</summary>
    public static readonly string[] Statuser = ["foreslatt_av_ai", "validert"];

    public static bool ErGyldigKategori(string? k) => k is not null && Kategorier.Contains(k);

    /// <summary>
    /// [Ny, issue #311, Johanns beslutning 2026-10-07] Typen kilde utenfor korpus — speilet av CHECK
    /// <c>ck_strukturkanter_kilde_type</c>. <see cref="NettsideAnnet"/> er «bare dokumentert på en nettside eller
    /// annet sted uten rettslig status» — arbeidslista over struktur som mangler forankring i en rettskilde.
    /// </summary>
    public static readonly string[] KildeUtenforKorpusTyper =
        ["kgl_res", "instruks", "tildelingsbrev", "vedtekter", "styrevedtak", "forarbeider", NettsideAnnet, Register];

    public const string NettsideAnnet = "nettside_annet";

    /// <summary>
    /// [Ny, issue #312 «områderegister», 2026-10-08] Et AUTORITATIVT REGISTER som ikke er en rettskilde —
    /// Kartverkets kommuneinfo (fylke → kommune), Enhetsregisteret (kommunens organisasjonsnummer → eget
    /// kommunenummer) og Kartverkets SSR (tettsted → kommune). Lagt til fordi ingen av de sju typene fra #311
    /// passet: <see cref="NettsideAnnet"/> betyr «uten rettslig status» og er ARBEIDSLISTA over struktur som
    /// mangler forankring — 1 000+ register-kanter der ville druknet de få som faktisk mangler kilde, og et
    /// nasjonalt register er ikke en nettside. Dokumentasjonen er <see cref="Primaer"/> når kanten er lest
    /// direkte av registeret. Johann bekrefter typen før merge (CLAUDE.md §19).
    /// </summary>
    public const string Register = "register";

    /// <summary>[Ny, Johanns beslutning 2026-10-07] Er kilden utenfor korpus dokumentert PRIMÆRT (lenken/teksten
    /// er selve kilden) eller SEKUNDÆRT (en tekst som refererer den)? CHECK <c>ck_strukturkanter_kilde_dokumentasjon</c>.</summary>
    public static readonly string[] KildeDokumentasjoner = [Primaer, Sekundaer];
    public const string Primaer = "primaer";
    public const string Sekundaer = "sekundaer";

    /// <summary>Visningsnavn — samme ord som UI-et (StrukturkantKategoriTag.tsx).</summary>
    public static string Visningsnavn(string kategori) => kategori switch
    {
        Relasjon => "relasjon",
        Kompetanse => "kompetanse",
        Plikt => "plikt overfor motpart", // [Ny, issue #353]
        Medlemskap => "medlemskap",
        Omradesammensetning => "områdesammensetning",
        Ansvarsomrade => "ansvarsområde",
        Organtilhorighet => "organtilhørighet",
        Rolleinnehav => "rolleinnehav",
        Klasseniva => "klassenivå",
        _ => kategori,
    };

    /// <summary>
    /// Hvilke noder kan stå i hver ende (docs/33 §4.3, kolonnen «Fra → Til»). <c>FraBegrep</c>/
    /// <c>TilBegrep</c> er tillatte <see cref="BegrepEntitet.Begrepskategori"/>-verdier; <c>'gruppe'</c>
    /// (uavklart type, gjenværende rader i andre miljøer etter #310) godtas overalt der et begrep med
    /// gruppefunksjon godtas — ellers ville en ikke-reklassifisert rad blitt umulig å koble.
    /// <c>TilValgfri</c> = kanten kan stå uten til-node (K: «kompetanse etter § X» — bestemmelsen er
    /// avgrensningen/hjemmelen, sakstypen er <see cref="StrukturkantEntitet.Objekt"/>; T: «hver kommune skal
    /// ha et kommunestyre» — organtypen er tekst).
    /// </summary>
    public sealed record Noderegel(
        string FraBeskrivelse, bool FraVirksomhet, string[] FraBegrep,
        string TilBeskrivelse, bool TilVirksomhet, string[] TilBegrep, bool TilValgfri);

    public static readonly IReadOnlyDictionary<string, Noderegel> Noderegler = new Dictionary<string, Noderegel>
    {
        [Relasjon] = new("aktør", true, [], "aktør", true, [], false),
        // [ENDRET, issue #341] Til-noden er MOTPARTEN («A har klagekompetanse overfor B»); uten motpart sier objekt/
        // hjemmel/avgrensning hva kompetansen gjelder. Til = fra er lov bare for normgivning (selvregulering).
        [Kompetanse] = new("aktør eller rolle", true, [Nodetyper.Rolle],
            "motpart (aktør, klasse, rolle eller område) — valgfri", true, [Nodetyper.Klasse, Nodetyper.Rolle, Nodetyper.Omrade], true),
        // [Ny, issue #353] Pliktsubjektet kan være en aktør (også en ordning: «Folketrygden skal dekke …»), en rolle
        // («Koordinatoren skal samarbeide med barnekoordinator») eller en KLASSE («Kommunen skal inngå samarbeidsavtale med det
        // regionale helseforetaket i helseregionen» — én kant mellom klasser, parene regnes ut via område, tilpasning punkt 5).
        // Motparten er valgfri: betalingsmottakeren er null når teksten ikke sier hvem som får pengene («utgiftene til X»).
        [Plikt] = new("aktør (også ordning), rolle eller klasse", true, [Nodetyper.Rolle, Nodetyper.Klasse],
            "motpart (aktør, klasse eller rolle) — valgfri", true, [Nodetyper.Klasse, Nodetyper.Rolle], true),
        // Mål = klasse ELLER område: «språkutviklingskommuner» (klasse) er medlem av «forvaltningsområdet for
        // samiske språk» (område) — gruppe-av-gruppe-dataene fra #164 har nettopp den formen (målt 2026-10-07).
        [Medlemskap] = new("aktør, klasse eller område", true, [Nodetyper.Klasse, Nodetyper.Omrade],
            "klasse eller område", false, [Nodetyper.Klasse, Nodetyper.Omrade], false),
        [Omradesammensetning] = new("område", false, [Nodetyper.Omrade], "område", false, [Nodetyper.Omrade], false),
        [Ansvarsomrade] = new("aktør", true, [], "område", false, [Nodetyper.Omrade], false),
        [Organtilhorighet] = new("organ, enhet eller rolle", true, [Nodetyper.Rolle], "rettssubjekt", true, [], false),
        [Rolleinnehav] = new("aktør", true, [], "rolle", false, [Nodetyper.Rolle], false),
        [Klasseniva] = new("klasse", false, [Nodetyper.Klasse], "rolle eller klasse (valgfri node)", false,
            [Nodetyper.Rolle, Nodetyper.Klasse], true),
    };

    /// <summary>
    /// [ENDRET, issue #341, 2026-10-08 — het <c>KreverParagrafspenn</c>] Kategoriene der en kant med hjemmel i korpus
    /// SKAL si HVOR i hjemmelen den står (<see cref="StrukturkantEntitet.HjemmelEid"/>) — ikke hvilke paragrafer den
    /// gjelder for.
    /// <para>
    /// Før #341 krevde regelen et AVGRENSNINGSSPENN på M/I, og alle skriveveiene (veiviseren, KI-oppdagelsen,
    /// samisk-seeden) fylte det med noden der tildelingen står — altså hjemmelsstedet. #311 flyttet så
    /// <c>Myndighetstildeling.ParagrafspennJson</c> inn i <c>avgrensning_paragrafspenn_json</c>, og 19 lokale rader
    /// (18 M + 1 I) fikk hjemmelsstedet som avgrensning og tom <c>hjemmel_eid</c> (Johanns funn på #341). Målt
    /// 2026-10-08: alle 19 er ETT punkt som er en node i kantens egen hjemmel. Migrasjonen
    /// <c>KompetanseMedMotpart</c> flyttet dem til <c>hjemmel_eid</c>; avgrensningen står nå bare for «hvilke
    /// paragrafer det gjelder for» (#314: rollen i § X avgjøres av innehavet som gjelder § X).
    /// </para>
    /// <para>
    /// Merk at docs/20 §2.5 beskrev det gamle feltet som «paragrafer i LOVEN denne tildelingen dekker» (en
    /// avgrensning), og «Legg til tilhørighet»-skjemaet bygget det fra gruppebegrepets lov. Begge betydningene har
    /// altså vært skrevet til samme felt; migrasjonen skiller dem på om noden ligger i hjemmelen, ikke på en gjetning.
    /// </para>
    /// </summary>
    public static readonly string[] KreverHjemmelsted = [Medlemskap, Rolleinnehav];

    // ---------------- Kompetanse (issue #341, Johanns beslutninger 2026-10-08) ----------------

    /// <summary>[Ny, issue #341] K-typen normgivning — den eneste som har en <see cref="Normformer">normform</see>.</summary>
    public const string Normgivning = "normgivning";

    /// <summary>[Ny, issue #341] R-typen for en GJENNOMFØRT delegering (Johanns beslutning 1).</summary>
    public const string HarDelegertTil = "har_delegert_til";

    /// <summary>
    /// [Ny, issue #341, Johanns beslutning P2 2026-10-08, <c>[LÅST]</c>] Normgivningens form — speilet av CHECK
    /// <c>ck_strukturkanter_normform</c>. «Selvregulering» er ikke en egen form eller type: den er normgivning der
    /// til = fra (<see cref="ErSelvregulering"/>).
    /// </summary>
    public static readonly string[] Normformer = ["forskrift", "reglement", "arbeidsordning", "vedtekter", "instruks"];

    /// <summary>
    /// [Ny, issue #341, Johanns beslutning 3 2026-10-08, <c>[LÅST]</c>] Grunnlaget for en kompetanse — speilet av CHECK
    /// <c>ck_strukturkanter_grunnlag</c>. <c>privatrettslig</c> = eierskap/selskapsrett (morselskapets instruksjon av et
    /// nettforetak); samme modell, men spørrbart.
    /// </summary>
    public static readonly string[] Grunnlag = ["offentligrettslig", "privatrettslig"];

    /// <summary>[Ny, issue #341] Selvregulering er AVLEDET (Johanns beslutning P2): normgivning der motparten er
    /// kompetanseinnehaveren selv. Den eneste kanten der fra = til er lov (CHECK <c>ck_strukturkanter_ikke_selv</c>).</summary>
    public static bool ErSelvregulering(StrukturkantEntitet k) =>
        k.Kategori == Kompetanse && k.Typekode == Normgivning
        && ((k.FraVirksomhetId is not null && k.FraVirksomhetId == k.TilVirksomhetId)
            || (k.FraBegrepId is not null && k.FraBegrepId == k.TilBegrepId));

    /// <summary>[Ny, issue #341, Johanns beslutning 2026-10-08 (hierarki), <c>[LÅST]</c>] Kompetansefamiliene — speilet av
    /// CHECK <c>ck_relasjonstype_konfigurasjon_familie</c>. <see cref="Beslutning"/> står over alle og har ingen familie.
    /// <para>
    /// [ENDRET, issue #352, Johanns beslutning 1 2026-10-08, <c>[LÅST]</c>] <c>personell</c> heter <see cref="Oppnevning"/>:
    /// «Oppnevningskompetanse … er den mest generelle kategorien. […] Fellesnevner: En aktør gis myndighet til å bestemme
    /// hvem som skal inneha en rolle, et verv eller en funksjon.» Migrasjonen <c>OppnevningsfamilienOgRester</c> byttet
    /// verdien i typekonfigurasjonen og i CHECK-en.
    /// </para></summary>
    public static readonly string[] Familier =
        ["struktur", Oppnevning, "styring", "normgivning", "kontroll", "klage_overproving", "vedtak", "sanksjon"];

    /// <summary>[Ny, issue #352] Familien OG K-typen oppnevning (samme ord: typen er familiens generelle form).</summary>
    public const string Oppnevning = "oppnevning";

    /// <summary>[Ny, issue #352] K-typen overprøving — anke er en undertype av den (<see cref="Undertyper"/>).</summary>
    public const string Overproving = "overproving";

    /// <summary>
    /// [Ny, issue #352, Johanns beslutning 1 2026-10-08] Undertypene til en K-type — speilet av CHECK
    /// <c>ck_strukturkanter_undertype</c>. Undertypen sier HVORDAN kompetansen utøves etter teksten, der typen er den
    /// generelle formen: «Kommunestyret velger forliksrådsmedlemmer» er oppnevningskompetanse med undertype <c>valg</c>.
    /// <para>
    /// <b>Hvorfor et felt på kanten og ikke bare et uttrykk i leksikonet</b> (Johann: «Verbet … beholdes som en undertype
    /// eller et uttrykk i leksikonet, så det kan spørres på»): leksikonet sier hvilke ORD som gir oppnevningskompetanse,
    /// men ikke hvilket av dem en bestemt kant kom fra. Uten feltet kunne «hvem velger styremedlemmene?» bare besvares ved
    /// å lese sitatet igjen, og de 26 fasitutsagnene som før #352 var <c>utpekingskompetanse</c> eller
    /// <c>ansettelseskompetanse</c> ville mistet opplysningen de hadde (sakens AC: «ingenting går tapt»). Den naturlige
    /// plassen er den <see cref="Normformer">normformen</see> alt har for normgivning: en lukket liste per type, bare på K,
    /// NULL = ikke angitt. Normformen er IKKE slått sammen med undertypen — den er <c>[LÅST]</c> fra #341 og bærer sin
    /// egen fvl-betydning.
    /// </para>
    /// <para>
    /// <b>Ordene er substantiv</b> (<c>valg</c>, ikke <c>velger</c>), som typekodene og normformene; VERBENE («velger»,
    /// «ansetter», «utpeker», «oppnevner») står i leksikonet (<c>kompetanseleksikon.json</c>, <c>undertyper</c>), sammen
    /// med ordstammene som avgjør undertypen ut fra et sitat.
    /// </para>
    /// <para>
    /// <b><c>anke</c> under overprøving</b> er hovedøktens tolkning (Johann bekrefter i PR-en for #352): Johann avgjorde at
    /// <c>ankeinstans_for</c> blir K overprøving; uten undertypen kan «hvem er ankeinstans for tingrettene?» ikke skilles fra
    /// annen overprøving (domstolsprøving av et forvaltningsvedtak), og det ville vært et tap.
    /// </para>
    /// <para>
    /// <b>Avsetting er ikke en undertype av oppnevning</b>, men en egen type i familien oppnevning (hovedøktens tolkning,
    /// Johann bekrefter: den er motsatsen). <c>utnevning</c> og <c>konstitusjon</c> står i fasitsitatene, men er ikke på
    /// Johanns liste — de er ikke lagt til (spørsmål i PR-en), og et sitat med dem får undertype NULL.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Undertyper = new Dictionary<string, string[]>
    {
        [Oppnevning] = ["valg", "ansettelse", "utpeking", "oppnevning"],
        [Overproving] = ["anke"],
    };

    /// <summary>[Ny, issue #352] Er <paramref name="undertype"/> en gyldig undertype for K-typen <paramref name="typekode"/>?</summary>
    public static bool ErGyldigUndertype(string typekode, string undertype) =>
        Undertyper.TryGetValue(typekode, out var lov) && lov.Contains(undertype);

    /// <summary>[Ny, issue #341, Johanns beslutning 2026-10-08, <c>[LÅST]</c>] Forvaltningslovens § 2-perspektiv — speilet av
    /// CHECK <c>ck_relasjonstype_konfigurasjon_fvl_kategori</c>.</summary>
    public static readonly string[] FvlKategorier = ["forskrift", "enkeltvedtak", "ikke_vedtak"];

    /// <summary>[Ny, issue #341] Øverst i hierarkiet: brukes når teksten bare sier «beslutningsmyndighet» (sameloven § 2-1
    /// fjerde ledd).</summary>
    public const string Beslutning = "beslutning";

    /// <summary>
    /// [Ny, issue #341, Johanns beslutninger 2026-10-08] ÉN tabell over kompetansetypene: koden i databasen, ordet i
    /// visningen («klagekompetanse»), typen i FORMAT.md/fasiten, familien og fvl-kategorien. Startsettet (K-radene),
    /// <see cref="KompetansetypeFraFasit"/> og seedingen av familie/fvl-kategori leses herfra, så de ikke kan drifte.
    /// <para>
    /// <b>Familiene</b> er Johanns liste (struktur: opprette, avvikle, organisere; personell: oppnevne, utpeke, ansette,
    /// avsette; styring: instruere, samordne, delegere, godkjenne, samtykke, pålegg; normgivning; kontroll: tilsyn,
    /// revisjon; klage og overprøving: klage, omgjøre, overprøve, stadfeste; vedtak; sanksjon). <c>forelegging</c>
    /// (beslutning 2 på #341, kom før hierarkiet) er ikke plassert i en familie av Johann — NULL til det er avgjort.
    /// </para>
    /// <para>
    /// [ENDRET, issue #352, Johanns beslutninger 2026-10-08] Familien <c>personell</c> heter <see cref="Oppnevning"/>, og
    /// <c>utpeking</c> og <c>ansettelse</c> er ikke lenger egne typer: de er oppnevningskompetanse med
    /// <see cref="Undertyper">undertype</see> <c>utpeking</c>/<c>ansettelse</c> (R <c>velger</c> ble undertype <c>valg</c>).
    /// <c>avsetting</c> står i familien oppnevning som motsatsen (hovedøktens tolkning, Johann bekrefter). <c>forelegging</c>
    /// har familien <c>kontroll</c> (Johanns beslutning 3).
    /// </para>
    /// <para>
    /// <b>Fvl-kategori</b> er satt bare der den følger av forvaltningsloven uten skjønn: vedtak og pålegg er
    /// enkeltvedtak (fvl. § 2 første ledd bokstav b), ansettelse er enkeltvedtak (§ 2 tredje ledd), instruksjon,
    /// samordning, tilsyn og revisjon er i seg selv ikke vedtak. Normgivning avgjøres av normformen (se
    /// <see cref="FvlKategoriFor"/>). Resten er NULL = ikke avklart (f.eks. er en avskjed et enkeltvedtak, men avsetting
    /// av et styre i et foretak er det ikke) — det gjettes ikke; lista står i PR-en for #341.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<(string Kode, string Substantiv, string FasitType, string? Familie, string? FvlKategori)> Kompetansetyper =
    [
        (Beslutning, "beslutningskompetanse", "beslutningskompetanse", null, null),
        ("oppretting", "opprettingskompetanse", "opprettingskompetanse", "struktur", null),
        ("avvikling", "avviklingskompetanse", "avviklingskompetanse", "struktur", null),
        ("organisasjon", "organisasjonskompetanse", "organisasjonskompetanse", "struktur", null),
        // [ENDRET, issue #352] Familien heter oppnevning. [FJERNET, #352] utpeking og ansettelse — nå undertyper av oppnevning
        // (migrasjonen OppnevningsfamilienOgRester flyttet kantene). Ansettelsens fvl-kategori følger undertypen (FvlKategoriFor).
        (Oppnevning, "oppnevningskompetanse", "oppnevningskompetanse", Oppnevning, null),
        ("avsetting", "avsettingskompetanse", "avsettingskompetanse", Oppnevning, null),
        ("instruksjon", "instruksjonskompetanse", "instruksjonskompetanse", "styring", "ikke_vedtak"),
        ("samordning", "samordningskompetanse", "samordningskompetanse", "styring", "ikke_vedtak"),
        ("delegering", "delegeringskompetanse", "delegeringskompetanse", "styring", null),
        ("godkjenning", "godkjenningskompetanse", "godkjenningskompetanse", "styring", null),
        ("samtykke", "samtykkekompetanse", "samtykkekompetanse", "styring", null),
        ("palegg", "påleggskompetanse", "paleggskompetanse", "styring", "enkeltvedtak"),
        (Normgivning, "normgivningskompetanse", "normgivningskompetanse", "normgivning", null),
        ("tilsyn", "tilsynskompetanse", "tilsynskompetanse", "kontroll", "ikke_vedtak"),
        ("revisjon", "revisjonskompetanse", "revisjonskompetanse", "kontroll", "ikke_vedtak"),
        ("klage", "klagekompetanse", "klagekompetanse", "klage_overproving", null),
        ("omgjoring", "omgjøringskompetanse", "omgjoringskompetanse", "klage_overproving", null),
        (Overproving, "overprøvingskompetanse", "overprovingskompetanse", "klage_overproving", null),
        ("stadfesting", "stadfestingskompetanse", "stadfestingskompetanse", "klage_overproving", null),
        ("vedtak", "vedtakskompetanse", "vedtakskompetanse", "vedtak", "enkeltvedtak"),
        ("sanksjon", "sanksjonskompetanse", "sanksjonskompetanse", "sanksjon", null),
        // [ENDRET, issue #352, Johanns beslutning 3 2026-10-08] forelegging er kontroll (var NULL = ikke plassert).
        ("forelegging", "foreleggingskompetanse", "foreleggingskompetanse", "kontroll", null),
    ];

    /// <summary>
    /// [Ny, issue #341] FORMAT.md-typen (fasit og konvertering, «…kompetanse») → K-typekoden i databasen. Avbildningen
    /// #313 trenger når konverteringsresultatet skal inn som forslag.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KompetansetypeFraFasit =
        Kompetansetyper.ToDictionary(t => t.FasitType, t => t.Kode, StringComparer.Ordinal);

    /// <summary>
    /// [Ny, issue #341, Johanns beslutning 2026-10-08] Fvl-kategorien for én KANT: typens (fra konfigurasjonen), unntatt
    /// normgivning, der den følger av normformen — normform <c>forskrift</c> gir <c>forskrift</c> («forskriftskompetanse
    /// er normgivning med normform forskrift og fvl-kategori forskrift»). Andre normformer (reglement, instruks …) og
    /// ukjent normform gir NULL: om et reglement er en forskrift etter fvl. § 2 c, avgjøres ikke av formen alene.
    /// <para>
    /// <b>Hvorfor på typen og ikke på kanten (Johanns spørsmål):</b> fvl-kategorien er en egenskap ved HVA slags
    /// kompetanse det er, ikke ved det enkelte utsagnet — alle vedtakskompetanser er enkeltvedtak. Det eneste unntaket
    /// er normgivning, og der er det normformen (som alt står på kanten) som avgjør. Et eget felt på kanten ville gitt
    /// to kilder til samme opplysning og en verdi som kan motsi typen.
    /// </para>
    /// <para>
    /// [ENDRET, issue #352] Samme slags unntak for oppnevning med undertype <c>ansettelse</c>: den er enkeltvedtak (fvl. § 2
    /// tredje ledd), slik typen <c>ansettelse</c> var før den ble en undertype — opplysningen skal ikke gå tapt i
    /// sammenslåingen. De andre undertypene (valg, utpeking, oppnevning) gir typens verdi (NULL = ikke avklart).
    /// </para>
    /// </summary>
    public static string? FvlKategoriFor(string typekode, string? normform, string? undertype, string? typensFvlKategori) =>
        typekode switch
        {
            Normgivning => normform == "forskrift" ? "forskrift" : null,
            Oppnevning when undertype == "ansettelse" => "enkeltvedtak",
            _ => typensFvlKategori,
        };

    // ---------------- Plikt overfor motpart og ordning (issue #353, Johanns godkjenning 2026-10-08) ----------------

    /// <summary>
    /// [Ny, issue #353, <c>[LÅST]</c>] ÉN tabell over plikttypene, som <see cref="Kompetansetyper"/>: koden i databasen, ordet i
    /// visningen og typen i FORMAT.md/fasiten («samarbeidsplikt»). Startsettet (P-radene) og <see cref="PlikttypeFraFasit"/>
    /// leses herfra.
    /// <para>
    /// <b>Typer, ikke undertyper.</b> Saken kaller de seks «undertyper» av P. De er lagt inn som TYPEKODER i kategorien P (rader i
    /// <c>relasjonstype_konfigurasjon</c>, som K-typene), ikke i feltet <see cref="StrukturkantEntitet.Undertype"/>: K har sine
    /// typer som koder og bruker undertype til en presisering av én type (oppnevning → valg). Å legge de seks i undertype-feltet
    /// ville gitt P én eneste typekode og gjort kategorien til en type. Hovedøktens tolkning, Johann bekrefter (PR-en for #353).
    /// </para>
    /// <para>
    /// <b>Retning:</b> fra = pliktsubjektet, til = motparten. Konsultasjon: fra = den som SKAL konsultere/innhente uttalelse, til =
    /// den som skal høres. Informasjon: fra = den som skal gi opplysningene, til = mottakeren. Betaling: fra = den som skal dekke
    /// utgiftene (også en <see cref="Nodetyper.Ordning">ordning</see>), til = betalingsmottakeren, NULL når teksten ikke sier det.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<(string Kode, string Substantiv, string FasitType)> Plikttyper =
    [
        ("samarbeid", "samarbeidsplikt", "samarbeidsplikt"),
        ("avtale", "avtaleplikt", "avtaleplikt"),
        ("betaling", "betalingsplikt", "betalingsplikt"),
        ("bistand", "bistandsplikt", "bistandsplikt"),
        ("informasjon", "informasjonsplikt", "informasjonsplikt"),
        ("konsultasjon", "konsultasjonsplikt", "konsultasjonsplikt"),
    ];

    /// <summary>[Ny, issue #353] FORMAT.md-typen («samarbeidsplikt») → P-typekoden i databasen («samarbeid»).</summary>
    public static readonly IReadOnlyDictionary<string, string> PlikttypeFraFasit =
        Plikttyper.ToDictionary(t => t.FasitType, t => t.Kode, StringComparer.Ordinal);

    /// <summary>
    /// [Ny, issue #353, jf. L14 «modalitet bevares», <c>[LÅST]</c>] Modaliteten i teksten — speilet av CHECK
    /// <c>ck_strukturkanter_modalitet</c>. <c>skal</c> (også «plikter», «har plikt til»), <c>kan</c>, <c>bor</c> («bør»). Bare
    /// på P; NULL = ikke angitt (teksten har ikke et modalverb som avgjør det, f.eks. presens «Staten dekker …» — det gjettes
    /// ikke at presens betyr «skal»).
    /// <para>
    /// <b>Hvorfor bare på P:</b> K er «kan» per definisjon (kompetanse er myndighet), og «kan ikke» er polaritet. På P sier
    /// modaliteten hvor sterk plikten er: «skal samarbeide» (energiloven § 2-5) og «bør … skje i samarbeid» (samisk
    /// opplæringsforskrift § 2) er ulike opplysninger (Johanns kommentar på energiloven:u27: «skal_samarbeide som blir mer
    /// forpliktende»).
    /// </para>
    /// </summary>
    public static readonly string[] Modaliteter = ["skal", "kan", "bor"];

    /// <summary>[Ny, issue #353] R-typen fra en ordning til organet som forvalter den («Helsedirektoratet skal forvalte kapittel 5»,
    /// folketrygdloven § 21-11 a første ledd). Hjemlet, avgrenset per kapittel/stønadsområde.</summary>
    public const string ForvaltesAv = "forvaltes_av";

    /// <summary>[Ny, issue #353] G-typen fra en ordning til rettssubjektet den tilhører (rettslig ansvarsbærer) — BARE når det er
    /// hjemlet. Analysen merket «folketrygden tilhører staten» selv som en slutning uten kilde; uten hjemmel finnes ingen kant,
    /// og hullet er synlig (CLAUDE.md §8).</summary>
    public const string Tilhorer = "tilhorer";

    /// <summary>
    /// [Ny, issue #353] Hvilke kanter en <see cref="Nodetyper.Ordning">ordning</see> kan stå i. En ordning er ikke en aktør
    /// (folketrygden fatter ingen vedtak, ansetter ingen og har ingen organer), men rettskilden gir den en funksjon:
    /// pliktsubjekt i P («Folketrygden skal dekke …»), forvaltet av et organ (R <see cref="ForvaltesAv"/>) og tilhørende et
    /// rettssubjekt (G <see cref="Tilhorer"/>). Som fra-node er bare disse lov; som til-node bare P (en betaling TIL en ordning
    /// eller et fond). R <see cref="ForvaltesAv"/> og G <see cref="Tilhorer"/> krever omvendt at fra-noden ER en ordning — en
    /// aktør «forvaltes ikke av» en annen i denne betydningen (det er eierskap/ledelse/sekretariat, R).
    /// </summary>
    public static bool OrdningLovSomFra(string kategori, string typekode) =>
        kategori == Plikt || (kategori, typekode) == (Relasjon, ForvaltesAv) || (kategori, typekode) == (Organtilhorighet, Tilhorer);

    /// <summary>[Ny, issue #353] Se <see cref="OrdningLovSomFra"/>.</summary>
    public static bool OrdningLovSomTil(string kategori) => kategori == Plikt;

    /// <summary>[Ny, issue #353] Typene som KREVER en ordning som fra-node.</summary>
    public static bool KreverOrdningSomFra(string kategori, string typekode) =>
        (kategori, typekode) == (Relasjon, ForvaltesAv) || (kategori, typekode) == (Organtilhorighet, Tilhorer);

    /// <summary>
    /// Kategoriene der en sykel er en registreringsfeil og avvises (Johanns valg i issue #164 for
    /// gruppe-av-gruppe, bevart her): medlemskap og områdesammensetning arves transitivt, så en ring ville
    /// velte enhver traversering. R har bevisst INGEN sykelsjekk (docs/29 §C.3: A kan være «underlagt» B og B
    /// samtidig «enhet_i» A i en annen betydning) — uendret fra <c>VirksomhetRelasjon</c>. [Merk, #330: <c>enhet_i</c>
    /// er siden blitt G <c>del_av</c>; eksempelet om to ulike betydninger mellom samme par gjelder fortsatt.]
    /// </summary>
    public static readonly string[] SykelfrieKategorier = [Medlemskap, Omradesammensetning];

    /// <summary>
    /// Startsettet av typekoder (docs/33 §4.3 «Typekoder (startsett, utvidbart)») med visningsmaler —
    /// <c>{0}</c> er motparten sett fra den siden man spør fra (samme Fra-/Til-maler som docs/29 §Del C).
    /// Seedes ved oppstart per (kategori, kode); en kode som alt finnes røres ikke (maler kan være endret
    /// bevisst i drift).
    /// <para>
    /// <b>[FJERNET, issue #330, 2026-10-08] De fem R-kodene fra før #311</b> (<c>underlagt</c>, <c>sekretariat</c>,
    /// <c>klageinstans</c>, <c>enhet_i</c>, <c>oppgaver_overfort_til</c>) sto først i lista fram til #330. #311
    /// migrerte <c>virksomhet_relasjoner</c> med SAMME typekode, og tre av dem leses MOTSATT vei av sin
    /// docs/33-tvilling («X har klageinstans hos Y» = «Y er klageinstans for X»), så «hvem er klageinstans for
    /// hvem?» ga to svar. Johann besluttet 2026-10-08 å harmonisere: migrasjonen <c>HarmoniserRelasjonskoder</c>
    /// konverterte radene (<see cref="RelasjonskodeHarmonisering"/> har mappingen: <c>klageinstans</c> →
    /// <c>klageinstans_for</c>, <c>sekretariat</c> → <c>sekretariat_for</c> og <c>oppgaver_overfort_til</c> →
    /// <c>etterfolger</c> med fra/til byttet; <c>enhet_i</c> → G <c>del_av</c> og <c>underlagt</c> →
    /// <c>administrativt_underordnet</c> i samme retning) og slettet de gamle kodene fra konfigurasjonen. De er
    /// fjernet HERFRA også — ellers ville seeden under lagt dem inn igjen ved neste oppstart, og veiviseren og
    /// «Legg til relasjon» (som lister konfigurasjonen) ville tilbudt dem på nytt.
    /// </para>
    /// <para>
    /// <b>K-kodene er korte</b> (<c>klage</c>, <c>normgivning</c> …), FORMAT.md/<c>Strukturkontrakt</c> bruker
    /// «…kompetanse» (<c>klagekompetanse</c>, <c>normgivningskompetanse</c> …). [ENDRET, #341] Avbildningen står nå i
    /// <see cref="KompetansetypeFraFasit"/>, så #313 ikke må finne den opp.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<(string Kategori, string Kode, string FraMal, string TilMal)> Startsett =
    [
        // ---- R relasjon (aktør → aktør) ----
        // [FJERNET, issue #330, 2026-10-08] underlagt, sekretariat, klageinstans, enhet_i og oppgaver_overfort_til
        // — se avsnittet over. Den rettslige ETTERFØLGELSEN fra etterfølgelse-runden (2026-09-09, issue #134,
        // advokatloven § 73: «Advokatbevillingsnemnden fikk oppgavene overført til Advokatnemnda») uttrykkes nå
        // som «Advokatnemnda etterfolger Advokatbevillingsnemnden».
        //
        // [ENDRET, issue #341, Johanns beslutning P1 2026-10-08] R er STRUKTUR UTEN MYNDIGHET: eierskap, ledelse,
        // sekretariat, rapportering, etterfølger og representasjon — pluss den gjennomførte delegeringen
        // (har_delegert_til). Myndighetsrelasjonene er flyttet til K med til = motparten:
        // [FJERNET, #341] klageinstans_for → K klage («A har klagekompetanse overfor B»; samme retning: fra =
        //   klageinstansen, til = den hvis vedtak påklages — migrasjonen KompetanseMedMotpart flyttet de 3 lokale radene),
        // [FJERNET, #341] instruksjon → K instruksjon, omgjoring → K omgjoring, oppnevner → K oppnevning,
        // [FJERNET, #341] delegerer_til → K delegering (kompetansen til å delegere, «X kan delegere til Y») ELLER
        //   R har_delegert_til (en GJENNOMFØRT delegering, Johanns beslutning 1 på #341). Hvilken av de to en gammel
        //   delegerer_til-rad var, kan ikke avgjøres uten å lese kilden — migrasjonen avbryter derfor hvis det finnes
        //   slike rader (0 lokalt 2026-10-08) i stedet for å gjette.
        (Relasjon, "eies_av", "eies av {0}", "eier {0}"),
        (Relasjon, "ledes_av", "ledes av {0}", "leder {0}"),
        (Relasjon, "sekretariat_for", "er sekretariat for {0}", "har sekretariat hos {0}"),
        (Relasjon, "rapporterer_til", "rapporterer til {0}", "mottar rapporter fra {0}"),
        (Relasjon, "etterfolger", "etterfølger {0}", "etterfølges av {0}"),
        (Relasjon, "representerer", "representerer {0}", "representeres av {0}"),
        // [Ny, issue #341, Johanns beslutning 1 2026-10-08] Gjennomført delegering: en strukturell kant fra den som
        // HAR delegert til mottakeren, avgrenset per paragraf. Unntakene i et delegeringsvedtak («omfatter ikke …»)
        // er avgrensning, ikke negativ kompetanse. Kompetansen til å delegere er K delegering.
        (Relasjon, HarDelegertTil, "har delegert myndighet til {0}", "har fått delegert myndighet fra {0}"),
        // [ENDRET, issue #352, Johanns beslutning 4 2026-10-08 — sto som UAVKLART fra #341] administrativt_underordnet,
        // radgir, oppretter og avvikler BLIR STÅENDE som R: «De er struktur eller hendelser, ikke myndighet.»
        // oppretter/avvikler er den GJENNOMFØRTE handlingen; kompetansen til å opprette eller avvikle er K oppretting/
        // avvikling i familien struktur.
        // [FJERNET, #352] velger → K oppnevning med undertype valg («Kommunestyret velger forliksrådsmedlemmer»; samme
        //   retning: fra = den som velger, til = den/det som velges), ankeinstans_for → K overprøving med undertype anke
        //   (fra = ankeinstansen, til = den hvis avgjørelser ankes). Migrasjonen OppnevningsfamilienOgRester flyttet
        //   kantene og slettet kodene fra konfigurasjonen; de er fjernet HERFRA også, ellers ville seeden lagt dem inn igjen.
        (Relasjon, "administrativt_underordnet", "er administrativt underordnet {0}", "er administrativt overordnet {0}"),
        (Relasjon, "radgir", "gir råd til {0}", "får råd fra {0}"),
        (Relasjon, "oppretter", "oppretter {0}", "er opprettet av {0}"),
        (Relasjon, "avvikler", "avvikler {0}", "avvikles av {0}"),
        // [Ny, issue #353] Fra en ordning (folketrygden) til organet som forvalter den — bare fra en ordning (OrdningLovSomFra).
        (Relasjon, ForvaltesAv, "forvaltes av {0}", "forvalter {0}"),

        // ---- K kompetanse (aktør/rolle → motpart, bestemmelse eller sakstype) ----
        // [ENDRET, issue #341, Johanns beslutning P2 2026-10-08] Typologien er Johanns liste: instruksjon, tilsyn, klage,
        // omgjøring, delegering, oppnevning, vedtak, utpeking, godkjenning, samtykke, pålegg, normgivning og organisasjon,
        // utvidet (beslutning 2) med avsetting, sanksjon, overprøving og forelegging, og (hierarkibeslutningen) med
        // beslutning, oppretting, avvikling, ansettelse, samordning, revisjon og stadfesting. «A har kompetanse av typen X,
        // eventuelt OVERFOR B (til-noden), når det gjelder Y (objekt/avgrensning)». {0} er motpartsteksten
        // StrukturkantTjeneste.Kompetansetekst bygger: «(normform) overfor B — objekt», eller objektet, eller «etter
        // hjemmelen». Til-malen leses fra motpartens side.
        // [FJERNET, #341] forskrift → normgivning med Normform = 'forskrift' (de 205 forskriftskompetansene i fasiten
        //   og eventuelle K forskrift-rader; migrasjonen konverterer dem), iverksetting (ikke i typologien; 0 rader).
        // [ENDRET, issue #341, Johanns beslutning 2026-10-08 (hierarki)] Radene genereres fra Kompetansetyper (under), i
        // familierekkefølge — beslutning øverst. Nye typer i den runden: beslutning, oppretting, avvikling, ansettelse,
        // samordning, revisjon og stadfesting.
        .. Kompetansetyper.Select(t => (Kompetanse, t.Kode, $"har {t.Substantiv} {{0}}", $"{{0}} har {t.Substantiv} overfor denne")),

        // ---- P plikt overfor motpart ([Ny, issue #353]) ----
        // {0} er motpartsteksten StrukturkantTjeneste.Plikttekst bygger: «(skal) overfor B — objekt», eller objektet, eller
        // «etter hjemmelen». Til-malen leses fra motpartens side.
        .. Plikttyper.Select(t => (Plikt, t.Kode, $"har {t.Substantiv} {{0}}", $"{{0}} har {t.Substantiv} overfor denne")),

        // ---- M medlemskap (aktør/klasse/område → klasse) ----
        (Medlemskap, "medlem_av", "er medlem av {0}", "har medlem {0}"),

        // ---- O områdesammensetning (område → område) ----
        (Omradesammensetning, "bestar_av", "består av {0}", "inngår i {0}"),

        // ---- A ansvarsområde (aktør → område) ----
        (Ansvarsomrade, "har_ansvarsomrade", "har ansvarsområde {0}", "er ansvarsområde for {0}"),
        (Ansvarsomrade, "har_jurisdiksjon", "har jurisdiksjon i {0}", "er jurisdiksjonsområde for {0}"),
        (Ansvarsomrade, "har_sete_i", "har sete i {0}", "er sete for {0}"),
        (Ansvarsomrade, "valgkrets_for", "er valgkrets for {0}", "har valgkrets {0}"),
        // [Ny, issue #345] «Til lagsognet X sogner A tingrett» (inndelingsforskriften §§ 11–16): tingrettens
        // ansvarsområde inngår i lagsognet. Samme kode som fasiten og mønsteret inndeling-sogner («annet:sogner_til»).
        (Ansvarsomrade, "sogner_til", "sogner til {0}", "har tilsognet {0}"),

        // ---- G organtilhørighet (organ/enhet/rolle → rettssubjekt) ----
        (Organtilhorighet, "har_organ", "er organ for {0}", "har organet {0}"),
        (Organtilhorighet, "del_av", "er del av {0}", "har som del {0}"),
        (Organtilhorighet, "har_medlemmer", "har medlemmer fra {0}", "har medlemmer i {0}"),
        // [Ny, issue #352-tillegg, Johanns beslutning 2026-10-08, fasitkontrollen domstolloven u17] SAMMENSETNINGEN I DEN
        // ENKELTE SAK: hvor mange og hvilke dommere (eller andre) som deltar i en sakstype — ikke organets faste medlemmer
        // (har_medlemmer). Fra = rollen som deltar (G-noderegelen: «organ, enhet eller rolle»), til = organet som settes;
        // antallet står i Objekt («fem dommere»), sakstypen i avgrensningen («andre saker enn etter første ledd første
        // punktum»). Typen er merket saksavhengig (SaksavhengigeTyper), og visningsteksten tar med objektet.
        (Organtilhorighet, SettesMed, "deltar i den enkelte sak i {0}", "settes i den enkelte sak med {0}"),
        // [Ny, issue #353] Fra en ordning til rettssubjektet den tilhører — bare fra en ordning, og bare når det er hjemlet.
        (Organtilhorighet, Tilhorer, "tilhører {0}", "har ordningen {0}"),

        // ---- I rolleinnehav (aktør → rolle) ----
        (Rolleinnehav, "innehar", "innehar rollen {0}", "innehas av {0}"),

        // ---- T klassenivå (klasse → rolle/organtype; distributivt) ----
        (Klasseniva, "skal_ha", "skal (hvert medlem) ha {0}", "skal finnes hos hvert medlem av {0}"),
    ];

    /// <summary>[Ny, issue #352-tillegg] G-typen for sammensetningen i den enkelte sak.</summary>
    public const string SettesMed = "settes_med";

    /// <summary>
    /// [Ny, issue #352-tillegg, Johanns beslutning 2026-10-08] Typene som gjelder den ENKELTE SAKEN, ikke organet fast —
    /// seedes som <see cref="RelasjonsTypeKonfigurasjonEntitet.Saksavhengig"/> = true.
    /// <para>
    /// <b>Antallet i <c>Objekt</c>, ikke i et eget tallfelt (valgt og begrunnet, #352):</b> lovteksten sier antallet i
    /// former et heltall ikke bærer uten tap — «fem dommere», «minst tre», «en dommer og en varadommer», «alle … som ikke er
    /// ugilde» (plenum) — og fasiten har alt antallet i objektet på de tilsvarende <c>har_medlemmer</c>-utsagnene («3
    /// dommere», «11 dommere»). Ingen spørsmål i docs/32 §3 krever å regne med antallet ennå; et tallfelt kan legges til den
    /// dagen et gjør det, uten å miste noe, fordi teksten står i objektet.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<(string Kategori, string Kode)> SaksavhengigeTyper = [(Organtilhorighet, SettesMed)];

    /// <summary>Typekoden migrerte myndighetstildelinger og gruppemedlemskap får (issue #311).</summary>
    public const string MedlemAv = "medlem_av";

    /// <summary>Typekoden migrerte myndighetstildelinger til en rolle får (issue #311).</summary>
    public const string Innehar = "innehar";

    /// <summary>
    /// Seeder <see cref="Startsett"/> per (kategori, kode). Brukes av oppstartsblokken i Program.cs og av
    /// testene (som ellers måtte kopiert lista). Rører aldri en rad som alt finnes.
    /// </summary>
    public static async Task SeedStartsettAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var finnes = (await db.RelasjonsTypeKonfigurasjoner.Select(k => new { k.Kategori, k.Kode }).ToListAsync(ct))
            .Select(k => (k.Kategori, k.Kode)).ToHashSet();
        var rekkefolge = 0;
        var nye = new List<RelasjonsTypeKonfigurasjonEntitet>();
        foreach (var (kategori, kode, fraMal, tilMal) in Startsett)
        {
            rekkefolge++;
            if (finnes.Contains((kategori, kode))) continue;
            // [Ny, issue #341] Familie og fvl-kategori for K-typene (Kompetansetyper).
            var kt = kategori == Kompetanse ? Kompetansetyper.FirstOrDefault(t => t.Kode == kode) : default;
            nye.Add(new RelasjonsTypeKonfigurasjonEntitet
            {
                Id = Guid.NewGuid(), Kategori = kategori, Kode = kode, FraVisningsmal = fraMal, TilVisningsmal = tilMal,
                Sorteringsrekkefolge = rekkefolge, Familie = kt.Familie, FvlKategori = kt.FvlKategori,
                Saksavhengig = SaksavhengigeTyper.Contains((kategori, kode)), // [Ny, #352-tillegg]
            });
        }
        if (nye.Count == 0) return;
        db.RelasjonsTypeKonfigurasjoner.AddRange(nye);
        await db.SaveChangesAsync(ct);
    }
}
