using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Del B/C/D — punkt 8 (avklaringsrunde 2026-08-13): <c>NettsideDokumentEntitet</c> er fjernet, en
/// nettside ER nå en ordinær <see cref="RettskildeEntitet"/> (Kildetype="Brukerveiledning") skrevet
/// av <see cref="BrukerveiledningImportTjeneste"/>, med <see cref="NettsideStiEntitet"/>/
/// <see cref="NettsideLenkeEntitet"/> repekt til den (§3.4/§3.2 er UENDRET i sak, kun FK-formen er
/// ny). SQLite-profil (samme mønster som <c>SqliteProfilTests</c>) — ingen av disse testene krever
/// Postgres-spesifikk oppførsel.
/// <para>
/// <see cref="Bundlingssiden_kobler_helt_frem_til_importerte_rettskilder_pa_eli_og_url"/> er
/// KJERNETESTEN oppgaven ba om — det faktiske "koble alle sammen"-beviset, se metodekommentaren der.
/// Dette beviset må holde i DEN NYE modellen akkurat som i den gamle — det er selve poenget med at
/// denne testen fortsatt eksisterer etter konvergensen.
/// </para>
/// </summary>
public sealed class NettsideDokumentgrafTests : IAsyncLifetime
{
    private string _filsti = "";

    public Task InitializeAsync()
    {
        _filsti = Path.Combine(Path.GetTempPath(), $"regelide-nettsidetest-{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_filsti)) File.Delete(_filsti);
        return Task.CompletedTask;
    }

    private async Task<RegelIdeDbContext> NyBaseAsync()
    {
        var db = new RegelIdeDbContext(new DbContextOptionsBuilder<RegelIdeDbContext>().UseSqlite($"Data Source={_filsti}").Options);
        await Databaseoppsett.SorgForSkjemaAsync(db);
        return db;
    }

    private static NettsideFixture LesFixture(string filnavn) => NettsideFixtureLeser.Les(Testdata.LesNettsideFixture(filnavn));

    private static string TilAbsoluttUrl(string href) =>
        href.StartsWith('/') ? $"https://www.bergen.kommune.no{href}" : href;

    /// <summary>Henter den EI ene "side"-noden for en importert Brukerveiledning — samme rolle som
    /// dokument-Id-en spilte i den gamle NettsideDokumentEntitet-modellen, nå ett nivå ned.</summary>
    private static Task<RettskildeNodeEntitet> SideNodeAsync(RegelIdeDbContext db, Guid rettskildeId) =>
        db.RettskildeNoder.SingleAsync(n => n.RettskildeId == rettskildeId && n.Eid == "side");

    /// <summary>Alle 21 URL-ene under "Bevilling og tillatelser" — bundlingssiden inkludert (samme
    /// slug er nevnt to ganger i oppgavebeskrivelsen, IKKE hentet/lagret to ganger her).</summary>
    private static readonly string[] AlleUnderliggendeFiler =
    [
        "bevillingsgebyr-salgsog-skjenkebevillinger-20252026-frist-er-17februar-2026.txt",
        "etablererproven-og-kunnskapsproven.txt",
        "godkjenning-av-ny-styrer-stedfortreder-og-daglig-leder-i-bevillinger.txt",
        "kontrollvirksomhet-av-skjenking-og-salg-av-alkohol.txt",
        "krav-om-fettutskiller.txt",
        "kurs-i-ansvarlig-alkoholhandtering-2026.txt",
        "lukket-selskap-ambulerende-skjenkebevilling.txt",
        "melde-inn-og-ut-av-tobakkssalgsregisteret.txt",
        "retningslinjer-for-tildeling-av-salgsog-skjenkebevillinger-og-forskrift-om-salgsskjenkeog-apningstider.txt",
        "salgsbevilling-for-alkohol.txt",
        "skjenketid-ved-overgang-til-sommertid-og-vintertid.txt",
        "skjenketider-i-forbindelse-med-fotball-vm-2026-perioden-11-juni-til-19-juli-2026.txt",
        "soknad-om-serveringsbevilling.txt",
        "soknad-om-skjenkebevilling-for-alkohol-og-endringer-i-eksisterende-bevilling-f-eks-soknad-om-uteservering.txt",
        "soknad-om-skjenkebevilling-for-alkoholholdig-drikk-gruppe-3.txt",
        "soknad-om-skjenkebevilling-pa-uteareal.txt",
        "soknad-om-utvidet-skjenkeareal-for-en-enkelt-anledning.txt",
        "soknad-om-utvidet-skjenkeareal-pa-eksisterende-skjenkebevilling.txt",
        "tilsyn-av-internkontroll-ved-virksomheter-med-salgsog-skjenkebevilling.txt",
        "utvidet-skjenkeog-apningstid-for-en-enkelt-anledning.txt",
        "apent-arrangement-skjenkebevilling-for-n-enkelt-anledning.txt",
    ];

    /// <summary>Importerer alle 21 undersider + de to indekssidene, og utleder <see cref="NettsideStiEntitet"/>-
    /// rader fra indekssidenes egne lenkelister (§3.4). Delt oppsett for stitestene under. Returnerer
    /// rettskilde-Id-er (ikke lenger dokument-Id-er), samme kart-rolle som før.</summary>
    private static async Task<(RegelIdeDbContext Db, BrukerveiledningImportTjeneste Tjeneste, Dictionary<string, Guid> UrlTilRettskildeId)> ByggKorpusAsync(RegelIdeDbContext db)
    {
        var tjeneste = new BrukerveiledningImportTjeneste(db);
        var urlTilId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var fil in AlleUnderliggendeFiler)
        {
            var f = LesFixture(fil);
            var resultat = NettsideTekstParser.Parse(f.KanoniskUrl, f.Tittel, f.RaaTekst);
            var id = await tjeneste.LagreDokumentAsync(resultat);
            urlTilId[f.KanoniskUrl] = id;
        }

        foreach (var indeksfil in new[] { "bevilling-og-tillatelser.txt", "kontor-for-skjenkesaker-innbyggerhjelp.txt" })
        {
            var indeks = LesFixture(indeksfil);
            var indeksResultat = NettsideTekstParser.Parse(indeks.KanoniskUrl, indeks.Tittel, indeks.RaaTekst);
            foreach (var lenke in indeksResultat.Lenker.Where(l => l.Type == NettsideLenketype.LenkerTil))
            {
                var mal = TilAbsoluttUrl(lenke.RaaHref);
                if (urlTilId.TryGetValue(mal, out var rettskildeId))
                {
                    await tjeneste.LeggTilStiAsync(rettskildeId, indeks.Sti!, indeks.StiType!);
                }
            }
        }

        return (db, tjeneste, urlTilId);
    }

    [Fact]
    public async Task Samme_kanoniske_url_dedupliseres_ikke_duplisert_ved_reimport()
    {
        await using var db = await NyBaseAsync();
        var tjeneste = new BrukerveiledningImportTjeneste(db);
        var f = LesFixture("krav-om-fettutskiller.txt");
        var resultat = NettsideTekstParser.Parse(f.KanoniskUrl, f.Tittel, f.RaaTekst);

        var forsteId = await tjeneste.LagreDokumentAsync(resultat);
        var andreId = await tjeneste.LagreDokumentAsync(resultat);

        Assert.Equal(forsteId, andreId);
        Assert.Equal(1, await db.Rettskilder.CountAsync(r => r.Url == f.KanoniskUrl && r.Kildetype == "Brukerveiledning"));
    }

    [Fact]
    public async Task S3_4_krav_om_fettutskiller_har_kun_EN_sti_presisering_ikke_full_bekreftelse()
    {
        // Se data/kilder/raw-nettside/README.md: den organisatoriske indekssiden mangler DENNE
        // lenken, i motsetning til de 20 andre — §3.4s påstand holder for 20/21, ikke 21/21.
        await using var db = await NyBaseAsync();
        var (ferdigDb, _, urlTilId) = await ByggKorpusAsync(db);

        var fettutskillerId = urlTilId["https://www.bergen.kommune.no/innbyggerhjelpen/naring-avgifter-og-anskaffelser/naring/bevilling-og-tillatelser/krav-om-fettutskiller"];
        var stier = await ferdigDb.NettsideStier.Where(s => s.RettskildeId == fettutskillerId).ToListAsync();

        Assert.Single(stier);
        Assert.Equal("tematisk", stier[0].StiType);
    }

    [Fact]
    public async Task S3_4_salgsbevilling_har_TO_stier_tematisk_og_organisatorisk_bekreftet()
    {
        await using var db = await NyBaseAsync();
        var (ferdigDb, _, urlTilId) = await ByggKorpusAsync(db);

        var salgsbevillingId = urlTilId["https://www.bergen.kommune.no/innbyggerhjelpen/naring-avgifter-og-anskaffelser/naring/bevilling-og-tillatelser/salgsbevilling-for-alkohol"];
        var stiTyper = await ferdigDb.NettsideStier.Where(s => s.RettskildeId == salgsbevillingId)
            .Select(s => s.StiType).OrderBy(t => t).ToListAsync();

        Assert.Equal(["organisatorisk", "tematisk"], stiTyper);
    }

    [Fact]
    public async Task Nitten_av_tjueen_underliggende_sider_har_begge_stier_krav_om_fettutskiller_er_unntaket()
    {
        await using var db = await NyBaseAsync();
        var (ferdigDb, _, urlTilId) = await ByggKorpusAsync(db);

        var dokumenterMedToStier = await ferdigDb.NettsideStier
            .GroupBy(s => s.RettskildeId)
            .Where(g => g.Count() == 2)
            .CountAsync();
        var dokumenterMedEnSti = await ferdigDb.NettsideStier
            .GroupBy(s => s.RettskildeId)
            .Where(g => g.Count() == 1)
            .CountAsync();

        Assert.Equal(20, dokumenterMedToStier);
        Assert.Equal(1, dokumenterMedEnSti);
    }

    /// <summary>
    /// KJERNETESTEN (Del D) — det faktiske "koble alle sammen"-beviset oppgaven ba om, VERIFISERT
    /// I DEN NYE, KONVERGERTE MODELLEN (punkt 8, 2026-08-13) — dette er nøyaktig kjernebeviset fra
    /// forrige runde, det er IKKE latt stille forsvinne i konvergensen. Strekker seg fra bundlingsidens
    /// side-NODE (tidligere: dens NettsideDokument), via to UAVHENGIGE deterministiske kanttyper, helt
    /// frem til EKTE, allerede-importerte RettskildeEntitet-rader:
    /// <list type="number">
    /// <item><c>lovdatalenke</c> → <see cref="RettskildeEntitet"/> for alkoholloven OG
    /// alkoholforskriften, matchet på <see cref="RettskildeEntitet.Eli"/> — begge FAKTISK importert
    /// her (samme <c>LovdataKonverterer</c>/<c>RettskildeImportTjeneste</c> som byggesteg 1), ikke
    /// antatt.</item>
    /// <item><c>lenker_til</c> → en <see cref="RettskildeEntitet"/> for Bergens retningslinjer,
    /// matchet på <see cref="RettskildeEntitet.Url"/> — SEEDET her for å representere hva et
    /// håndbok-import-endepunkt produserer (i praksis <c>HandbokImportTjeneste</c>, bygget forrige
    /// runde). Denne rettskildens underliggende <c>HandbokTekstParser.TrekkUtReferanser</c>-uttrekk
    /// viser <c>hjemlet_i</c>-referanser til "alkoholloven" i klartekst — den TEKSTLIGE, men ikke
    /// (ennå) GUID-koblede, fortsettelsen av kjeden inn i loven (uendret, ærlig grense fra forrige
    /// runde — se docs/13-backlog.md).</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Bundlingssiden_kobler_helt_frem_til_importerte_rettskilder_pa_eli_og_url()
    {
        await using var db = await NyBaseAsync();

        // --- 1) Ekte import av alkoholloven og alkoholforskriften (byggesteg 1s egen pipeline) ---
        var importTjeneste = new RettskildeImportTjeneste(db);
        var alkohollovenId = await importTjeneste.ImporterAsync(LovdataKonverterer.Konverter(Testdata.LesAlkoholloven()));
        var alkoholforskriftenId = await importTjeneste.ImporterAsync(LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften()));

        var alkoholloven = await db.Rettskilder.SingleAsync(r => r.Id == alkohollovenId);
        var alkoholforskriften = await db.Rettskilder.SingleAsync(r => r.Id == alkoholforskriftenId);
        Assert.Equal("https://lovdata.no/eli/lov/1989/06/02/27/nor", alkoholloven.Eli);
        Assert.Equal("https://lovdata.no/eli/forskrift/2005/06/08/538/nor", alkoholforskriften.Eli);

        // --- 2) Seedet RettskildeEntitet for Bergens retningslinjer (§2 Lag 1, Url-feltet) ---
        // Representerer hva HandbokImportTjeneste (bygget forrige runde) faktisk skriver.
        var retningslinjerId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = retningslinjerId,
            Doctype = "doc",
            Kildetype = "Virksomhetsdokument",
            Importrolle = "primaer",
            AknXml = "<akomaNtoso xmlns=\"http://docs.oasis-open.org/legaldocml/ns/akn/3.0\"><doc name=\"virksomhetsdokument\"><body/></doc></akomaNtoso>",
            Tittel = "Retningslinjer for tildeling av salgs- og skjenkebevillinger i Bergen kommune for perioden 2024-2028",
            Status = "Gjeldende",
            Url = "https://www.bergen.kommune.no/api/rest/filer/V51903878",
            NormativVirkning = "bindende_forvaltning",
            InterntDokNr = "SD-24-113",
            Revisjonsnr = "01",
            VedtattAv = "Bystyret",
            Vedtaksdato = new DateOnly(2024, 6, 19),
            GyldigTil = new DateOnly(2028, 7, 1),
            OpprettetAv = "Kari Jurist",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        // Den TEKSTLIGE hjemlet_i-fortsettelsen — uendret uttrekk, kun LEST her, ikke skrevet til en
        // ny RettskildeReferanseEntitet-rad (bevisst utenfor scope, se docs/13-backlog.md §4 punkt 8).
        var retningslinjerParset = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());
        Assert.Contains(retningslinjerParset.Referanser,
            r => r.Type == HandbokReferansetype.HjemletI &&
                 r.EksternLovnavn!.Equals("alkoholloven", StringComparison.OrdinalIgnoreCase));

        // --- 3) Bundlingssiden importert som en RettskildeEntitet (Kildetype="Brukerveiledning"), ---
        // --- med sine lenke-kandidater på siden-noden ---
        var brukerveiledningTjeneste = new BrukerveiledningImportTjeneste(db);
        var kobler = new NettsideGrafKobler(db);
        var bundling = LesFixture("retningslinjer-for-tildeling-av-salgsog-skjenkebevillinger-og-forskrift-om-salgsskjenkeog-apningstider.txt");
        var bundlingResultat = NettsideTekstParser.Parse(bundling.KanoniskUrl, bundling.Tittel, bundling.RaaTekst);
        var bundlingRettskildeId = await brukerveiledningTjeneste.LagreDokumentAsync(bundlingResultat);
        var bundlingSideNode = await SideNodeAsync(db, bundlingRettskildeId);

        // --- 4) DB-koblingen — selve beviset ---
        var ulost = await kobler.LoosLenkerAsync();

        var lenker = await db.NettsideLenker.Where(l => l.FraNodeId == bundlingSideNode.Id).ToListAsync();

        var lovdatalenkeAlkoholloven = lenker.Single(l => l.RaaHref == "https://lovdata.no/dokument/NL/lov/1989-06-02-27");
        Assert.Equal("lovdatalenke", lovdatalenkeAlkoholloven.Type);
        Assert.Equal(alkohollovenId, lovdatalenkeAlkoholloven.TilRettskildeId); // <-- ekte GUID-match

        var lovdatalenkeForskriften = lenker.Single(l => l.RaaHref == "https://lovdata.no/dokument/SF/forskrift/2005-06-08-538");
        Assert.Equal("lovdatalenke", lovdatalenkeForskriften.Type);
        Assert.Equal(alkoholforskriftenId, lovdatalenkeForskriften.TilRettskildeId); // <-- ekte GUID-match

        var pdfLenke = lenker.Single(l => l.RaaHref == "/api/rest/filer/V51903878");
        Assert.Equal("lenker_til", pdfLenke.Type);
        Assert.Equal(retningslinjerId, pdfLenke.TilRettskildeId); // <-- matchet på Url, ikke Eli

        // Interne Bergen-lenker (Kontor for skjenkesaker, veiledninger) er ordinær lenker_til uten
        // noen rettskilde å matche mot ennå (siden er ikke importert i denne testen) — 0 uløst er
        // altså IKKE forventet; disse to forblir naturlig uløst.
        Assert.True(ulost >= 2, $"Forventet minst de to interne Bergen-lenkene uløst, fikk {ulost}.");
    }
}
