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
    public const string Medlemskap = "M";
    public const string Omradesammensetning = "O";
    public const string Ansvarsomrade = "A";
    public const string Organtilhorighet = "G";
    public const string Rolleinnehav = "I";
    public const string Klasseniva = "T";

    /// <summary>Alle åtte, i docs/33 §4.3-rekkefølge.</summary>
    public static readonly string[] Kategorier =
        [Relasjon, Kompetanse, Medlemskap, Omradesammensetning, Ansvarsomrade, Organtilhorighet, Rolleinnehav, Klasseniva];

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
        [Kompetanse] = new("aktør eller rolle", true, [Nodetyper.Rolle],
            "bestemmelse/sakstype (valgfri node)", true, [Nodetyper.Klasse, Nodetyper.Rolle, Nodetyper.Omrade], true),
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

    /// <summary>Kategoriene der en kant SKAL ha paragrafspenn når hjemmelen er i korpus — samme krav
    /// myndighetstildeling og gruppemedlemskap hadde før #311 (docs/20 §7.1), og docs/33 §3 funn 7
    /// («avgrensning til paragraf/ledd er påkrevd på all tildeling»).</summary>
    public static readonly string[] KreverParagrafspenn = [Medlemskap, Rolleinnehav];

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
    /// <b>K-kodene følger docs/33 §4.3</b> (<c>forskrift</c>, <c>vedtak</c> …), ikke FORMAT.md/
    /// <c>Strukturkontrakt</c> (<c>forskriftskompetanse</c>, <c>vedtakskompetanse</c> …). Avbildningen mellom
    /// de to hører til #313 (konverteringsresultat inn som forslag), der det er ett sted å gjøre den.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<(string Kategori, string Kode, string FraMal, string TilMal)> Startsett =
    [
        // ---- R relasjon (aktør → aktør) ----
        // [FJERNET, issue #330, 2026-10-08] underlagt, sekretariat, klageinstans, enhet_i og oppgaver_overfort_til
        // — se avsnittet over. Den rettslige ETTERFØLGELSEN fra etterfølgelse-runden (2026-09-09, issue #134,
        // advokatloven § 73: «Advokatbevillingsnemnden fikk oppgavene overført til Advokatnemnda») uttrykkes nå
        // som «Advokatnemnda etterfolger Advokatbevillingsnemnden».
        (Relasjon, "klageinstans_for", "er klageinstans for {0}", "har klageinstans hos {0}"),
        (Relasjon, "administrativt_underordnet", "er administrativt underordnet {0}", "er administrativt overordnet {0}"),
        (Relasjon, "instruksjon", "kan instruere {0}", "kan instrueres av {0}"),
        (Relasjon, "omgjoring", "kan omgjøre vedtak fra {0}", "kan få vedtak omgjort av {0}"),
        (Relasjon, "sekretariat_for", "er sekretariat for {0}", "har sekretariat hos {0}"),
        (Relasjon, "rapporterer_til", "rapporterer til {0}", "mottar rapporter fra {0}"),
        (Relasjon, "oppnevner", "oppnevner {0}", "oppnevnes av {0}"),
        (Relasjon, "velger", "velger {0}", "velges av {0}"),
        (Relasjon, "ledes_av", "ledes av {0}", "leder {0}"),
        (Relasjon, "eies_av", "eies av {0}", "eier {0}"),
        (Relasjon, "etterfolger", "etterfølger {0}", "etterfølges av {0}"),
        (Relasjon, "radgir", "gir råd til {0}", "får råd fra {0}"),
        (Relasjon, "delegerer_til", "delegerer myndighet til {0}", "har fått delegert myndighet fra {0}"),
        (Relasjon, "representerer", "representerer {0}", "representeres av {0}"),
        (Relasjon, "ankeinstans_for", "er ankeinstans for {0}", "har ankeinstans hos {0}"),
        (Relasjon, "oppretter", "oppretter {0}", "er opprettet av {0}"),
        (Relasjon, "avvikler", "avvikler {0}", "avvikles av {0}"),

        // ---- K kompetanse (aktør/rolle → bestemmelse eller sakstype) ----
        // {0} = til-noden hvis satt, ellers Objekt (sakstypen), ellers «etter hjemmelen» — se
        // StrukturkantTjeneste.Motpartstekst.
        (Kompetanse, "forskrift", "har forskriftskompetanse: {0}", "forskriftskompetanse ligger hos {0}"),
        (Kompetanse, "vedtak", "har vedtakskompetanse: {0}", "vedtakskompetanse ligger hos {0}"),
        (Kompetanse, "klage", "har klagekompetanse: {0}", "klagekompetanse ligger hos {0}"),
        (Kompetanse, "tilsyn", "har tilsynskompetanse: {0}", "tilsynskompetanse ligger hos {0}"),
        (Kompetanse, "delegering", "kan delegere: {0}", "delegeringsfullmakt ligger hos {0}"),
        (Kompetanse, "oppnevning", "har oppnevningskompetanse: {0}", "oppnevningskompetanse ligger hos {0}"),
        (Kompetanse, "instruksjon", "har instruksjonskompetanse: {0}", "instruksjonskompetanse ligger hos {0}"),
        (Kompetanse, "utpeking", "har utpekingskompetanse: {0}", "utpekingskompetanse ligger hos {0}"),
        (Kompetanse, "godkjenning", "har godkjenningskompetanse: {0}", "godkjenningskompetanse ligger hos {0}"),
        (Kompetanse, "iverksetting", "har iverksettingskompetanse: {0}", "iverksettingskompetanse ligger hos {0}"),
        (Kompetanse, "overproving", "har overprøvingskompetanse: {0}", "overprøvingskompetanse ligger hos {0}"),

        // ---- M medlemskap (aktør/klasse/område → klasse) ----
        (Medlemskap, "medlem_av", "er medlem av {0}", "har medlem {0}"),

        // ---- O områdesammensetning (område → område) ----
        (Omradesammensetning, "bestar_av", "består av {0}", "inngår i {0}"),

        // ---- A ansvarsområde (aktør → område) ----
        (Ansvarsomrade, "har_ansvarsomrade", "har ansvarsområde {0}", "er ansvarsområde for {0}"),
        (Ansvarsomrade, "har_jurisdiksjon", "har jurisdiksjon i {0}", "er jurisdiksjonsområde for {0}"),
        (Ansvarsomrade, "har_sete_i", "har sete i {0}", "er sete for {0}"),
        (Ansvarsomrade, "valgkrets_for", "er valgkrets for {0}", "har valgkrets {0}"),

        // ---- G organtilhørighet (organ/enhet/rolle → rettssubjekt) ----
        (Organtilhorighet, "har_organ", "er organ for {0}", "har organet {0}"),
        (Organtilhorighet, "del_av", "er del av {0}", "har som del {0}"),
        (Organtilhorighet, "har_medlemmer", "har medlemmer fra {0}", "har medlemmer i {0}"),

        // ---- I rolleinnehav (aktør → rolle) ----
        (Rolleinnehav, "innehar", "innehar rollen {0}", "innehas av {0}"),

        // ---- T klassenivå (klasse → rolle/organtype; distributivt) ----
        (Klasseniva, "skal_ha", "skal (hvert medlem) ha {0}", "skal finnes hos hvert medlem av {0}"),
    ];

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
            nye.Add(new RelasjonsTypeKonfigurasjonEntitet
            {
                Id = Guid.NewGuid(), Kategori = kategori, Kode = kode, FraVisningsmal = fraMal, TilVisningsmal = tilMal,
                Sorteringsrekkefolge = rekkefolge,
            });
        }
        if (nye.Count == 0) return;
        db.RelasjonsTypeKonfigurasjoner.AddRange(nye);
        await db.SaveChangesAsync(ct);
    }
}
