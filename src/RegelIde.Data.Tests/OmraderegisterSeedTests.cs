using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08] <see cref="OmraderegisterSeed"/> og
/// <see cref="OmradeOppslagTjeneste"/> mot ekte embedded Postgres, med de committede øyeblikksbildene og
/// inndelingsforskriftens 81 noder fra <c>data/fasit/strukturmodell/noder/domstolloven.json</c>.
/// <para>
/// Seeden skriver ~1 500 kanter, så den kjøres ÉN gang mot en egen, fersk database (<see cref="SeededAsync"/>) som
/// testene deler. Testene her endrer ikke det seedede innholdet, bortsett fra de som eksplisitt bruker sin egen
/// database (<see cref="Delt_kommune_gir_ikke_entydig_og_ingen_valgt_tingrett"/>).
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class OmraderegisterSeedTests(EmbeddedPostgresFixture fixture)
{
    /// <summary>Kommunene testene ser på, med orgnr fra brreg-kommuner.json. De andre 350 kommunene finnes som
    /// områder, men ikke som virksomheter i testbasen — det er nettopp «kommuner som ikke matcher» (AC2).</summary>
    private static readonly (string Orgnr, string Navn)[] Kommuner =
    [
        ("963376030", "KARASJOGA GIELDA / KARASJOK KOMMUNE"), // 5610
        ("964978840", "HERØY KOMMUNE"), // 1515 Møre og Romsdal
        ("872417982", "HERØY KOMMUNE"), // 1818 Nordland
        ("959272581", "VÅLER KOMMUNE"), // 3114 Østfold
        ("871034222", "VÅLER KOMMUNE"), // 3419 Innlandet
        ("958935420", "OSLO KOMMUNE"), // 0301
        ("820852982", "KRISTIANSAND KOMMUNE"), // 4204
    ];

    private static readonly SemaphoreSlim Las = new(1, 1);
    private static (string ConnString, OmraderegisterSeed.Resultat Forste, OmraderegisterSeed.Resultat Andre, int Kanter1, int Kanter2, int Begrep1, int Begrep2)? _seedet;

    private async Task<(string ConnString, OmraderegisterSeed.Resultat Forste, OmraderegisterSeed.Resultat Andre, int Kanter1, int Kanter2, int Begrep1, int Begrep2)> SeededAsync()
    {
        await Las.WaitAsync();
        try
        {
            if (_seedet is not null) return _seedet.Value;
            var conn = await NyTomDatabaseAsync("omrader");
            await using var db = Ny(conn);
            await GrunnlagAsync(db);
            var forste = await KjorAsync(db);
            var kanter1 = await db.Strukturkanter.CountAsync();
            var begrep1 = await db.Begreper.CountAsync();
            await using var db2 = Ny(conn); // ny kontekst: idempotensen skal hvile på basen, ikke på change trackeren
            var andre = await KjorAsync(db2);
            _seedet = (conn, forste, andre, kanter1, await db2.Strukturkanter.CountAsync(), begrep1, await db2.Begreper.CountAsync());
            return _seedet.Value;
        }
        finally
        {
            Las.Release();
        }
    }

    [Fact]
    public async Task Gir_15_fylker_357_kommuner_med_kode_og_357_O_kanter_fylke_til_kommune()
    {
        var s = await SeededAsync();
        await using var db = Ny(s.ConnString);
        var omrader = await db.Begreper.Where(b => b.Omradetype != null).ToListAsync();
        Assert.Equal(15, omrader.Count(b => b.Omradetype == Omradetyper.Fylke));
        Assert.Equal(357, omrader.Count(b => b.Omradetype == Omradetyper.Kommune));
        Assert.All(omrader.Where(b => b.Omradetype is Omradetyper.Fylke or Omradetyper.Kommune), b => Assert.NotNull(b.Omradekode));
        Assert.All(omrader, b => Assert.Null(b.GyldigTil)); // gjeldende inndeling

        var fylkeIder = omrader.Where(b => b.Omradetype == Omradetyper.Fylke).Select(b => b.Id).ToList();
        var kommuneIder = omrader.Where(b => b.Omradetype == Omradetyper.Kommune).Select(b => b.Id).ToList();
        var fylkeKommune = await db.Strukturkanter.Where(k => k.Kategori == "O" && fylkeIder.Contains(k.FraBegrepId!.Value)
                                                              && kommuneIder.Contains(k.TilBegrepId!.Value)).ToListAsync();
        Assert.Equal(357, fylkeKommune.Count);
        Assert.Equal(357, fylkeKommune.Select(k => k.TilBegrepId).Distinct().Count()); // hver kommune i nøyaktig ett fylke
        Assert.All(fylkeKommune, k => Assert.Equal(Strukturkanter.Register, k.KildeUtenforKorpusType));
    }

    [Fact]
    public async Task Andre_kjoring_skriver_ingenting_vakten_er_pa_stabil_nokkel()
    {
        var s = await SeededAsync();
        Assert.True(s.Forste.NyeKanter > 1000, $"Første kjøring: {s.Forste.NyeKanter} kanter.");
        Assert.Equal(0, s.Andre.NyeOmrader);
        Assert.Equal(0, s.Andre.NyeKanter);
        Assert.Equal(0, s.Andre.NyeVirksomheter);
        Assert.Equal(0, s.Andre.KommunenummerFylt);
        Assert.Equal(s.Kanter1, s.Kanter2);
        Assert.Equal(s.Begrep1, s.Begrep2);
    }

    [Fact]
    public async Task Heroy_og_Valer_er_to_omrader_hver_med_hver_sin_kode_og_navnet_overskrives_aldri()
    {
        var s = await SeededAsync();
        await using var db = Ny(s.ConnString);
        var heroy = await db.Begreper.Where(b => b.Omradetype == Omradetyper.Kommune && b.Term == "Herøy").Select(b => b.Omradekode).ToListAsync();
        var valer = await db.Begreper.Where(b => b.Omradetype == Omradetyper.Kommune && b.Term == "Våler").Select(b => b.Omradekode).ToListAsync();
        Assert.Equal(["1515", "1818"], heroy.Order());
        Assert.Equal(["3114", "3419"], valer.Order());
        Assert.Empty(s.Forste.Navneavvik);

        // Et navn endret i basen (f.eks. av et menneske) overskrives ikke — avviket rapporteres (CLAUDE.md §3).
        var conn = await NyTomDatabaseAsync("omrader_navn");
        await using var egen = Ny(conn);
        await Strukturkanter.SeedStartsettAsync(egen);
        await KjorAsync(egen);
        var tana = await egen.Begreper.SingleAsync(b => b.Omradetype == Omradetyper.Kommune && b.Omradekode == "5636");
        tana.Term = "Deatnu/Tana";
        await egen.SaveChangesAsync();
        await using var egen2 = Ny(conn);
        var igjen = await KjorAsync(egen2);
        Assert.Contains(igjen.Navneavvik, a => a.Contains("5636") && a.Contains("Deatnu/Tana"));
        Assert.Equal("Deatnu/Tana", (await egen2.Begreper.SingleAsync(b => b.Id == tana.Id)).Term);
    }

    [Fact]
    public async Task Kommunen_som_rettssubjekt_far_A_kant_til_territoriet_og_umatchede_listes()
    {
        var s = await SeededAsync();
        await using var db = Ny(s.ConnString);
        Assert.Equal(Kommuner.Length, s.Forste.KommunenummerFylt);
        Assert.Equal(357 - Kommuner.Length, s.Forste.KommunerUtenRettssubjekt.Count);
        Assert.Contains(s.Forste.KommunerUtenRettssubjekt, k => k.StartsWith("4601 ", StringComparison.Ordinal)); // Bergen finnes ikke i testbasen

        foreach (var (orgnr, _) in Kommuner)
        {
            var v = await db.Virksomheter.SingleAsync(x => x.Organisasjonsnummer == orgnr);
            Assert.NotNull(v.Kommunenummer);
            var territorium = await db.Strukturkanter
                .Where(k => k.Kategori == "A" && k.Typekode == "har_ansvarsomrade" && k.FraVirksomhetId == v.Id)
                .Join(db.Begreper, k => k.TilBegrepId, b => b.Id, (k, b) => b).SingleAsync();
            Assert.Equal(Omradetyper.Kommune, territorium.Omradetype);
            Assert.Equal(v.Kommunenummer, territorium.Omradekode);
        }
    }

    [Fact]
    public async Task Domstolene_opprettes_fra_Brreg_med_hjemmel_per_kant_og_rettssted_er_samme_node_som_kommunen()
    {
        var s = await SeededAsync();
        await using var db = Ny(s.ConnString);
        Assert.Empty(s.Forste.UlosteDomstoler);
        Assert.Empty(s.Forste.DelteKommuner);
        Assert.Equal(34, s.Forste.NyeVirksomheter); // 28 tingretter + 6 lagmannsretter

        var forskriftId = await db.Rettskilder.Where(r => r.Eli == DomstolinndelingTolker.ForskriftEli).Select(r => r.Id).SingleAsync();
        var domstolkanter = await db.Strukturkanter.Where(k => k.HjemmelRettskildeId == forskriftId).ToListAsync();
        Assert.All(domstolkanter, k => Assert.NotNull(k.HjemmelEid));
        Assert.Equal(357, domstolkanter.Count(k => k.Typekode == "har_ansvarsomrade" && k.TilBegrepId != null
            && db.Begreper.Any(b => b.Id == k.TilBegrepId && b.Omradetype == Omradetyper.Kommune)));

        // Sunnmøre tingrett: Volda er både rettssted og del av ansvarsområdet — to kanter til SAMME node.
        var sunnmore = await db.Virksomheter.SingleAsync(v => v.Navn == "SUNNMØRE TINGRETT");
        var volda = await db.Begreper.SingleAsync(b => b.Omradetype == Omradetyper.Kommune && b.Term == "Volda");
        var tilVolda = domstolkanter.Where(k => k.FraVirksomhetId == sunnmore.Id && k.TilBegrepId == volda.Id).Select(k => k.Typekode).Order().ToList();
        Assert.Equal(["har_ansvarsomrade", "har_sete_i"], tilVolda);
        Assert.Equal(0, await db.Begreper.CountAsync(b => b.Omradetype == Omradetyper.Tettsted && b.Term == "Volda"));

        // Navneformen er forskriftens tekstform.
        Assert.True(await db.Begreper.AnyAsync(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == sunnmore.Id
                                                     && b.Term == "Sunnmøre tingrett" && b.Navneformgrunn == "gjeldende"));

        // 16 tettsteder, hvert i sin kommune.
        Assert.Equal(16, await db.Begreper.CountAsync(b => b.Omradetype == Omradetyper.Tettsted));
        Assert.Equal(15, await db.Begreper.CountAsync(b => b.Omradetype == Omradetyper.Lagsogn));
        Assert.Equal(6, await db.Begreper.CountAsync(b => b.Omradetype == Omradetyper.Lagdomme));
    }

    [Theory]
    [InlineData("5610", "Finnmark", "Sis- ja Nuorta-Finnmárkku diggegoddi/Indre og Østre Finnmark tingrett", "lagsogn Finnmárku/Finnmark", "Hålogaland lagdømme", "HÅLOGALAND LAGMANNSRETT", "Statsforvalteren i Troms og Finnmark", "Helseregion Nord", "HELSE NORD RHF")]
    [InlineData("1515", "Møre og Romsdal", "Sunnmøre tingrett", "lagsogn Møre og Romsdal", "Frostating lagdømme", "FROSTATING LAGMANNSRETT", "Statsforvaltaren i Møre og Romsdal", "Helseregion Midt-Norge", "HELSE MIDT-NORGE RHF")]
    [InlineData("1818", "Nordland", "Helgeland tingrett", "lagsogn Nordland", "Hålogaland lagdømme", "HÅLOGALAND LAGMANNSRETT", "Statsforvalteren i Nordland", "Helseregion Nord", "HELSE NORD RHF")]
    [InlineData("0301", "Oslo", "Oslo tingrett", "lagsogn Oslo, Asker og Bærum", "Borgarting lagdømme", "BORGARTING LAGMANNSRETT", "Statsforvalteren i Østfold, Buskerud, Oslo og Akershus", "Helseregion Sør-Øst", "HELSE SØR-ØST RHF")]
    [InlineData("4204", "Agder", "Agder tingrett", "lagsogn Agder", "Agder lagdømme", "AGDER LAGMANNSRETT", "Statsforvalteren i Agder", "Helseregion Sør-Øst", "HELSE SØR-ØST RHF")]
    [InlineData("3114", "Østfold", "Søndre Østfold tingrett", "lagsogn Søndre Østfold", "Borgarting lagdømme", "BORGARTING LAGMANNSRETT", "Statsforvalteren i Østfold, Buskerud, Oslo og Akershus", "Helseregion Sør-Øst", "HELSE SØR-ØST RHF")]
    [InlineData("3419", "Innlandet", "Hedmarken og Østerdal tingrett", "lagsogn Innlandet", "Eidsivating lagdømme", "EIDSIVATING LAGMANNSRETT", "Statsforvalteren i Innlandet", "Helseregion Sør-Øst", "HELSE SØR-ØST RHF")]
    public async Task Oppslag_gitt_kommune_gir_entydig_fylke_tingrett_lagdomme_statsforvalter_og_RHF(
        string kommunenummer, string fylke, string tingrett, string lagsogn, string lagdomme, string lagmannsrett,
        string statsforvalter, string helseregion, string rhf)
    {
        var s = await SeededAsync();
        await using var db = Ny(s.ConnString);
        var svar = await new OmradeOppslagTjeneste(db, new StrukturkantTjeneste(db)).ForKommunenummerAsync(kommunenummer);
        Assert.NotNull(svar);
        var r = svar!.Rubrikker.ToDictionary(x => x.Rubrikk);
        void Entydig(string rubrikk, string forventet)
        {
            Assert.True(r[rubrikk].Entydig, $"{rubrikk}: {r[rubrikk].Status} [{string.Join(" | ", r[rubrikk].Kandidater)}]");
            Assert.Equal(forventet, r[rubrikk].Kandidater.Single());
        }
        Assert.True(r["kommune"].Entydig);
        Entydig("fylke", fylke);
        Entydig("tingrett", tingrett);
        Entydig("lagsogn", lagsogn);
        Entydig("lagdømme", lagdomme);
        Entydig("lagmannsrett", lagmannsrett);
        Entydig("statsforvalter", statsforvalter);
        Entydig("helseregion", helseregion);
        Entydig("RHF", rhf);
    }

    [Fact]
    public async Task Delt_kommune_gir_ikke_entydig_og_ingen_valgt_tingrett()
    {
        // Egen database: kanten under ville ellers endret svaret i oppslagstestene.
        var conn = await NyTomDatabaseAsync("omrader_delt");
        await using var db = Ny(conn);
        await GrunnlagAsync(db);
        await KjorAsync(db);
        var tjeneste = new StrukturkantTjeneste(db);
        var vestreFinnmark = await db.Virksomheter.SingleAsync(v => v.Navn == "VESTRE FINNMARK TINGRETT");
        var karasjok = await db.Begreper.SingleAsync(b => b.Omradetype == Omradetyper.Kommune && b.Omradekode == "5610");
        await tjeneste.OpprettAsync(new NyStrukturkant("A", "har_ansvarsomrade", Kantnode.Virksomhet(vestreFinnmark.Id), Kantnode.Begrep(karasjok.Id),
            KildeUtenforKorpusTekst: "Syntetisk testkant: en kommune delt mellom to domssogn (domstolloven § 66 annet ledd)",
            KildeUtenforKorpusType: "nettside_annet", KildeUtenforKorpusDokumentasjon: "sekundaer"), "test");

        var svar = await new OmradeOppslagTjeneste(db, tjeneste).ForKommunenummerAsync("5610");
        var tingrett = svar!.Rubrikker.Single(x => x.Rubrikk == "tingrett");
        Assert.Equal("ikke_entydig", tingrett.Status);
        Assert.Equal(2, tingrett.Kandidater.Count);
    }

    [Fact]
    public async Task Nytt_omradebegrep_med_samme_navn_som_et_registrert_omrade_avvises()
    {
        var s = await SeededAsync();
        await using var db = Ny(s.ConnString);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(Nodetyper.Omrade, null, "Herøy", "test"));
        Assert.Contains("1515", ex.Message);
        Assert.Contains("1818", ex.Message);
    }

    // ---------------- oppsett ----------------

    private static async Task<OmraderegisterSeed.Resultat> KjorAsync(RegelIdeDbContext db) =>
        await OmraderegisterSeed.SeedAsync(db, new StrukturkantTjeneste(db), new VirksomhetsbegrepTjeneste(db));

    /// <summary>Typekonfigurasjonen, inndelingsforskriften med sine 81 noder, et utvalg kommuner, de ti statsforvalterne
    /// og de fire RHF-ene — det seeden forventer å finne i et ekte miljø.</summary>
    private static async Task GrunnlagAsync(RegelIdeDbContext db)
    {
        await Strukturkanter.SeedStartsettAsync(db);
        var forskrift = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Forskrift",
            Tittel = "Forskrift om inndelingen av rettskretser og lagdømmer", Eli = DomstolinndelingTolker.ForskriftEli,
            AknXml = $"<akomaNtoso><act><meta><identification><FRBRWork><FRBRuri value=\"{DomstolinndelingTolker.ForskriftEli}\"/>"
                     + "</FRBRWork></identification></meta></act></akomaNtoso>",
            Status = "Gjeldende", OpprettetAv = "Testoppsett", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Rettskilder.Add(forskrift);
        var i = 0;
        foreach (var n in DomstolinndelingTolkerTests.ForskriftNoder())
        {
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = forskrift.Id, Eid = n.Eid, KildeId = n.Eid, NodeType = n.NodeType,
                Overskrift = n.Overskrift, Tekst = n.Tekst, Sorteringsrekkefolge = i++,
            });
        }
        var kilder = DomstolinndelingTolkerTests.Kilder();
        var virksomheter = Kommuner.Select(k => (k.Orgnr, k.Navn, (string?)"kommune"))
            .Concat(kilder.Statsforvaltere.Embeter.Select(e => (e.Organisasjonsnummer, e.Navn, (string?)null)))
            .Concat(kilder.Helseregioner.Rhf.Select(r => (r.Organisasjonsnummer, r.Navn.ToUpperInvariant(), (string?)null)));
        foreach (var (orgnr, navn, niva) in virksomheter)
        {
            db.Virksomheter.Add(new Virksomhet
            {
                Id = Guid.NewGuid(), Navn = navn, Organisasjonsnummer = orgnr, Forvaltningsniva = niva,
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task<string> NyTomDatabaseAsync(string prefiks)
    {
        var navn = $"regelide_{prefiks}_{Guid.NewGuid():N}";
        await using (var master = Ny(ByttDatabase("postgres")))
        {
#pragma warning disable EF1003 // generert identifikator, ingen ytre inndata (samme som SamiskSprakforvaltningSeedTests)
            await master.Database.ExecuteSqlRawAsync("CREATE DATABASE " + navn + ";");
#pragma warning restore EF1003
        }
        var conn = ByttDatabase(navn);
        await using (var ny = Ny(conn)) await ny.Database.MigrateAsync();
        return conn;
    }

    private string ByttDatabase(string databasenavn) =>
        fixture.ConnectionString.Replace("Database=regelide_test", $"Database={databasenavn}");

    private static RegelIdeDbContext Ny(string conn) =>
        new(new DbContextOptionsBuilder<RegelIdeDbContext>().UseNpgsql(conn).Options);
}
