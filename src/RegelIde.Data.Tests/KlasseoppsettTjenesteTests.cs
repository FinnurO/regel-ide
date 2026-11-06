using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #442, Johanns avgjørelse 2026-11-05] Klassene fylkeskommune og departement etter modell B, mot ekte embedded Postgres. Spørsmålene
/// modellen skal kunne besvare etterpå (S1, S6):
/// <list type="number">
/// <item>Hvilke aktører er fylkeskommuner? De 14 med territoriekant etter kommuneloven § 3-1 første ledd, ikke statsforvalterne (som også har
/// kant til fylkene), og ikke Oslo (unntatt i loven).</item>
/// <item>Hvilke aktører er departementer? De 16 levende: 9 med livsløpets R oppretter, 4 med navnevedtaket og 3 med kgl.res. utenfor korpus som
/// premiss (fortolkning). Ikke et nedlagt departement, ikke en manuell R oppretter, ikke en lovspesifikk M-kant med avgrensning.</item>
/// <item>Hva er grunnlaget for klassen? Minst hjemmel eller medlemsregel, og begge er lov; hullet bare når begge mangler — i klassevisningen og
/// i aktørsidens hjemmelkort.</item>
/// <item>Er klassen «kommunene» arkivert med begrunnelse og proveniens, og taggen og M-kanten med den?</item>
/// <item>Er skrivingen forslag, idempotent, og gjør tørrkjøringen ingenting?</item>
/// </list>
/// Testbasen er delt: navn og koder er unike per test, og påstandene gjelder bare testens egne rader. Klassene og kommuneloven finnes én gang i
/// basen (regelen er unik blant gjeldende), så skrivingen kjøres i hver test og er idempotent.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class KlasseoppsettTjenesteTests(EmbeddedPostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Etter hver test arkiveres klassene skrivingen opprettet (og testens egne klasser uten grunnlag). Testbasen er delt med resten av samlingen,
    /// og en gjeldende klasse «fylkeskommune» eller «departement» endrer rutingen der: «Fylkeskommunen» blir en bøyningsform av en kjent klasse og
    /// ikke en ny navnekandidat (målt i CI: 11 tester i NavnekandidatOppdagelseTjenesteTests feilet før denne ryddingen). Arkiveres, ikke slettes,
    /// fordi kantene peker på dem.
    /// </summary>
    public async Task DisposeAsync()
    {
        await using var db = fixture.NyDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE begreper SET entitetsstatus = 'arkivert' WHERE entitetsstatus = 'gjeldende' AND begrepskategori = 'klasse' "
            + "AND (medlemskapsregel IN ('omraderegister_fylkeskommune', 'organiseringsvedtak_departement') OR term LIKE 'tomklasse %')");
    }

    private static readonly string[] Fylkesnavn =
        ["Oslo", "Rogaland", "Møre og Romsdal", "Nordland", "Østfold", "Akershus", "Buskerud", "Innlandet", "Vestfold", "Telemark", "Agder",
         "Vestland", "Trøndelag", "Troms", "Finnmark"];

    private static string Kort() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Kommuneloven med de tre leddene skrivingen kontrollerer. Én gang i basen: noden må være entydig.</summary>
    private static async Task<Guid> KommunelovenAsync(RegelIdeDbContext db)
    {
        var finnes = await db.RettskildeNoder.Where(n => n.Eid == Klasseregler.KommunelovenFylkeskommuneEid && n.Entitetsstatus == "gjeldende")
            .Select(n => (Guid?)n.RettskildeId).FirstOrDefaultAsync();
        if (finnes is { } id) return id;
        var r = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Tittel = "Lov om kommuner og fylkeskommuner (kommuneloven)", Kortnavn = "Kommuneloven",
            Status = "Gjeldende", OpprettetAv = "test", AknXml = "<akomaNtoso/>",
        };
        db.Rettskilder.Add(r);
        var i = 0;
        RettskildeNodeEntitet Ledd(string eid, string tekst) => new()
        {
            Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = eid, KildeId = $"l{++i}", NodeType = "ledd", Nummer = $"{i}", Tekst = tekst, Sorteringsrekkefolge = i,
        };
        db.RettskildeNoder.AddRange(
            Ledd(Klasseregler.KommunelovenInndelingEid, "Norge er inndelt i kommuner og fylkeskommuner med en egen folkevalgt ledelse."),
            Ledd(Klasseregler.KommunelovenFylkeskommuneEid, "Hver kommune hører til et fylke. Hvert fylke unntatt Oslo utgjør en fylkeskommune."),
            Ledd(Klasseregler.KommunelovenNavneregelEid, "Fylkeskommunen skal ha samme navn som fylket."));
        await db.SaveChangesAsync();
        return r.Id;
    }

    /// <summary>En liten rettskilde med ett ledd (navnevedtak, reindriftsloven-ersatz).</summary>
    private static async Task<(Guid Id, string Eid)> KildeAsync(RegelIdeDbContext db, string tekst)
    {
        var r = new RettskildeEntitet { Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Forskrift", Tittel = $"Test {Kort()}", Status = "Gjeldende", OpprettetAv = "test", AknXml = "<akomaNtoso/>" };
        db.Rettskilder.Add(r);
        var eid = $"https://lovdata.no/eli/forskrift/test/{r.Id:N}/nor/punkt-1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet { Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = eid, KildeId = "p1", NodeType = "punkt", Nummer = "1", Tekst = tekst, Sorteringsrekkefolge = 1 });
        await db.SaveChangesAsync();
        return (r.Id, eid);
    }

    private static async Task<Virksomhet> AktorAsync(RegelIdeDbContext db, string navn, string? niva = null, string? aktortype = null,
        string? orgnr = null, string? livsstatus = null)
    {
        if (orgnr is not null && await db.Virksomheter.FirstOrDefaultAsync(v => v.Organisasjonsnummer == orgnr) is { } finnes) return finnes;
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = navn, Forvaltningsniva = niva, Aktortype = aktortype, Organisasjonsnummer = orgnr, Livsstatus = livsstatus };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    private static async Task<Guid> FylkeAsync(RegelIdeDbContext db, string term, string kode)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Omrade, Omradetype = Omradetyper.Fylke, Omradekode = kode, Term = term,
            Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        await db.SaveChangesAsync();
        return b.Id;
    }

    private sealed record Fylkesverden(Dictionary<string, Guid> Fylker, Dictionary<string, Guid> Fylkeskommuner, Guid Statsforvalter, Guid OsloKommune);

    /// <summary>15 fylker med lovens navn (Oslo heter «Oslo», de andre har en unik hale), 14 fylkeskommuner med navneregelens navn (registernavn i
    /// store bokstaver, som Brreg), en statsforvalter med registerkant til to fylker, og en Oslo kommune som ikke er fylkeskommune.</summary>
    private static async Task<Fylkesverden> FylkerAsync(RegelIdeDbContext db, StrukturkantTjeneste kanter)
    {
        var hale = Kort();
        var fylker = new Dictionary<string, Guid>();
        var fylkeskommuner = new Dictionary<string, Guid>();
        foreach (var navn in Fylkesnavn)
        {
            var term = navn == "Oslo" ? "Oslo" : $"{navn} {hale}";
            fylker[navn] = await FylkeAsync(db, term, $"F{Kort()}");
            if (navn == "Oslo") continue;
            fylkeskommuner[navn] = (await AktorAsync(db, $"{term} FYLKESKOMMUNE".ToUpperInvariant(), "fylkeskommune", "rettssubjekt")).Id;
        }
        var statsforvalter = await AktorAsync(db, $"STATSFORVALTEREN I AGDER {hale}", "statsforvalter");
        foreach (var f in new[] { "Agder", "Rogaland" })
        {
            await kanter.OpprettAsync(new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(statsforvalter.Id),
                Kantnode.Begrep(fylker[f]), KildeUtenforKorpusTekst: "Statsforvalterens embetsområde (test)", KildeUtenforKorpusType: Strukturkanter.Register,
                KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer), "test");
        }
        var oslo = await AktorAsync(db, "OSLO KOMMUNE " + hale, "kommune", "rettssubjekt");
        return new Fylkesverden(fylker, fylkeskommuner, statsforvalter.Id, oslo.Id);
    }

    private sealed record Departementsverden(List<Guid> Med9, List<Guid> Med4, List<Guid> Med3, Guid Nedlagt, Guid ManuellOppretter, Guid Lovspesifikk);

    /// <summary>16 levende departementer (9 med livsløpets R oppretter, 4 med navnevedtaket, 3 med orgnr fra kgl.res.-fila) og tre som ikke skal
    /// bli medlem: et nedlagt departement med R oppretter, en aktør med en manuell R oppretter og en aktør med en lovspesifikk M-kant.</summary>
    private static async Task<Departementsverden> DepartementerAsync(RegelIdeDbContext db, StrukturkantTjeneste kanter)
    {
        var kongen = await AktorAsync(db, $"Kongen i statsråd {Kort()}", aktortype: "organ");
        var vedtak = await KildeAsync(db, "Det opprettes et Testdepartement med virkning fra 1. januar 2024.");
        async Task Oppretter(Guid til, string kilde) => await kanter.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "oppretter",
            Kantnode.Virksomhet(kongen.Id), Kantnode.Virksomhet(til), HjemmelRettskildeId: vedtak.Id, HjemmelEid: vedtak.Eid,
            Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: kilde, Kunnskapsstatus: Strukturkanter.Normalisering), "test");

        var med9 = new List<Guid>();
        for (var i = 0; i < 9; i++)
        {
            var d = await AktorAsync(db, $"Opprettet {i} departement {Kort()}");
            await Oppretter(d.Id, "monster:livslop");
            med9.Add(d.Id);
        }
        var med4 = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var d = await AktorAsync(db, $"Navngitt{i}departementet {Kort()}");
            var navnevedtak = await KildeAsync(db, $"Departementet skal hete {d.Navn}.");
            db.Begreper.Add(new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = d.Id, Navneformgrunn = "gjeldende", Term = d.Navn,
                LovkildeId = navnevedtak.Id, LovreferanseEid = navnevedtak.Eid, HjemmelArt = "navnevedtak", Kunnskapsstatus = Strukturkanter.Tekstfunn,
                Godkjenning = Strukturkanter.MaskineltForeslatt, OppdagelsesKilde = DepartementNavneformHjemmelTjeneste.OppdagelsesKilde,
                Status = "publisert", OpprettetAv = "test",
            });
            med4.Add(d.Id);
        }
        await db.SaveChangesAsync();
        var med3 = new List<Guid>();
        foreach (var k in KlasseoppsettTjeneste.LesKglRes().Departementer)
        {
            med3.Add((await AktorAsync(db, k.Navn, orgnr: k.Organisasjonsnummer)).Id);
        }
        var nedlagt = await AktorAsync(db, $"Nedlagtdepartementet {Kort()}", livsstatus: "nedlagt");
        await Oppretter(nedlagt.Id, "monster:livslop");
        var manuell = await AktorAsync(db, $"Testutvalget {Kort()}");
        await Oppretter(manuell.Id, "manuell");
        var lovspesifikk = await AktorAsync(db, $"Statsministerens kontor {Kort()}");
        return new Departementsverden(med9, med4, med3, nedlagt.Id, manuell.Id, lovspesifikk.Id);
    }

    private static KlasseoppsettTjeneste Skriving(RegelIdeDbContext db) => new(db, new StrukturkantTjeneste(db));

    [Fact]
    public async Task Medlemsregelen_gir_de_14_fylkeskommunene_ingen_statsforvaltere_og_ikke_Oslo()
    {
        await using var db = fixture.NyDbContext();
        var kanter = new StrukturkantTjeneste(db);
        await KommunelovenAsync(db);
        var verden = await FylkerAsync(db, kanter);

        var rapport = await Skriving(db).KjorAsync(skriv: true, "test");

        // Territoriekantene: 14, med hjemmel i § 3-1 første ledd, tekstfunn, forslag. Ingen til Oslo.
        var egne = rapport.Territoriekanter.Where(t => verden.Fylkeskommuner.ContainsValue(t.VirksomhetId)).ToList();
        Assert.Equal(14, egne.Count);
        Assert.DoesNotContain(rapport.Territoriekanter, t => t.Fylke == "Oslo");
        var lagret = await db.Strukturkanter.Where(k => egne.Select(e => e.KantId).Contains(k.Id)).ToListAsync();
        Assert.Equal(14, lagret.Count);
        Assert.All(lagret, k =>
        {
            Assert.Equal((Strukturkanter.MaskineltForeslatt, Strukturkanter.Tekstfunn), (k.Status, k.Kunnskapsstatus));
            Assert.Equal(Klasseregler.KommunelovenFylkeskommuneEid, k.HjemmelEid);
            Assert.Null(k.KildeUtenforKorpusType); // registeret er ikke kilden for kanten (N9)
            Assert.Equal(KlasseoppsettTjeneste.NavneregelOppdagelseskilde, k.OppdagelsesKilde);
        });

        var klasse = await db.Begreper.SingleAsync(b => b.Medlemskapsregel == Klasseregler.OmraderegisterFylkeskommune && b.Entitetsstatus == "gjeldende");
        Assert.Equal((Strukturkanter.ManueltForeslatt, Klasseregler.KommunelovenInndelingEid, Strukturkanter.Tekstfunn),
            (klasse.Godkjenning, klasse.LovreferanseEid, klasse.Kunnskapsstatus));
        Assert.Contains("§ 49 andre ledd", klasse.Begrunnelse);

        var svar = (await new KlassemedlemskapTjeneste(db, kanter).UtledAsync(klasse.Id))!;
        var mine = svar.Medlemmer.Where(m => verden.Fylker.ContainsValue(m.OmradeId!.Value)).ToList();
        Assert.Equal(14, mine.Count);
        Assert.Equal(verden.Fylkeskommuner.Values.Order(), mine.Select(m => m.VirksomhetId).Order());
        Assert.All(mine, m => Assert.Equal((Premissart.Territoriekant, Strukturkanter.Avledet, false), (m.Premissart, m.Kunnskapsstatus, m.Validert)));
        Assert.DoesNotContain(svar.Medlemmer, m => m.VirksomhetId == verden.Statsforvalter || m.VirksomhetId == verden.OsloKommune);
        Assert.DoesNotContain(svar.Medlemmer, m => m.OmradeId == verden.Fylker["Oslo"]);
        Assert.Contains(svar.Unntatt, u => u.Contains("unntatt Oslo"));
        Assert.DoesNotContain(svar.Hull, h => h.Contains(" Oslo:"));
    }

    [Fact]
    public async Task Medlemsregelen_gir_de_16_levende_departementene_med_premiss_og_svakeste_status()
    {
        await using var db = fixture.NyDbContext();
        var kanter = new StrukturkantTjeneste(db);
        await KommunelovenAsync(db);
        var verden = await DepartementerAsync(db, kanter);

        var rapport = await Skriving(db).KjorAsync(skriv: true, "test");
        var klasseId = rapport.Klasser.Single(k => k.Regel == Klasseregler.OrganiseringsvedtakDepartement).KlasseId!.Value;

        // En lovspesifikk M-kant (avgrensning) er ikke alminnelig medlemskap (sak 3).
        await kanter.OpprettAsync(new NyStrukturkant(Strukturkanter.Medlemskap, "medlem_av", Kantnode.Virksomhet(verden.Lovspesifikk), Kantnode.Begrep(klasseId),
            KildeUtenforKorpusTekst: "Karanteneloven § 1 første ledd (test)", KildeUtenforKorpusType: "nettside_annet",
            KildeUtenforKorpusDokumentasjon: Strukturkanter.Sekundaer, AvgrensningTekst: "bare etter karanteneloven",
            Status: Strukturkanter.ManueltForeslatt), "test");

        // Medlemskantene for de sju uten oppretterkant: 4 med navnevedtaket i korpus, 3 med kgl.res. utenfor korpus. Begge fortolkning.
        var mine = rapport.Medlemskanter.Where(m => verden.Med4.Contains(m.VirksomhetId) || verden.Med3.Contains(m.VirksomhetId)).ToList();
        Assert.Equal(4, mine.Count(m => m.Premiss == "navnevedtak"));
        Assert.Equal(3, mine.Count(m => m.Premiss == "kgl_res"));
        var mKanter = await db.Strukturkanter.Where(k => mine.Select(m => m.KantId).Contains(k.Id)).ToListAsync();
        Assert.All(mKanter, k => Assert.Equal(Strukturkanter.Fortolkning, k.Kunnskapsstatus));
        Assert.All(mKanter, k => Assert.Contains(k.Status, new[] { Strukturkanter.MaskineltForeslatt, Strukturkanter.ManueltForeslatt }));
        var kglRes = mKanter.Where(k => k.HjemmelRettskildeId is null).ToList();
        Assert.Equal(3, kglRes.Count);
        Assert.All(kglRes, k =>
        {
            Assert.Equal(("kgl_res", Strukturkanter.Sekundaer), (k.KildeUtenforKorpusType, k.KildeUtenforKorpusDokumentasjon));
            Assert.StartsWith("https://www.regjeringen.no/", k.KildeUtenforKorpusLenke);
            Assert.Contains("«", k.KildeUtenforKorpusTekst); // sitatet står i kilden
        });

        var svar = (await new KlassemedlemskapTjeneste(db, kanter).UtledAsync(klasseId))!;
        var alle16 = verden.Med9.Concat(verden.Med4).Concat(verden.Med3).ToHashSet();
        Assert.Equal(16, svar.Medlemmer.Count(m => alle16.Contains(m.VirksomhetId)));
        Assert.All(svar.Medlemmer.Where(m => verden.Med9.Contains(m.VirksomhetId)),
            m => Assert.Equal((Premissart.Oppretterkant, Strukturkanter.Avledet), (m.Premissart, m.Kunnskapsstatus)));
        Assert.All(svar.Medlemmer.Where(m => verden.Med4.Contains(m.VirksomhetId) || verden.Med3.Contains(m.VirksomhetId)),
            m => Assert.Equal((Premissart.Medlemskant, Strukturkanter.Fortolkning), (m.Premissart, m.Kunnskapsstatus)));
        Assert.DoesNotContain(svar.Medlemmer, m => m.VirksomhetId == verden.Nedlagt || m.VirksomhetId == verden.ManuellOppretter
                                                    || m.VirksomhetId == verden.Lovspesifikk);
        Assert.Null(svar.AntallOmrader);
        Assert.Equal(Strukturkanter.Fortolkning, svar.Kunnskapsstatus); // det svakeste premisset (§4.6)
    }

    [Fact]
    public async Task Grunnlaget_for_klassen_er_hjemmel_medlemsregel_eller_begge_og_hullet_bare_naar_begge_mangler()
    {
        // Regelen alene.
        Assert.Equal(Klassegrunnlag.Begge, Klassegrunnlag.Vurder(true, true));
        Assert.Equal(Klassegrunnlag.BareHjemmel, Klassegrunnlag.Vurder(true, false));
        Assert.Equal(Klassegrunnlag.BareMedlemsregel, Klassegrunnlag.Vurder(false, true));
        Assert.Equal(Klassegrunnlag.Mangler, Klassegrunnlag.Vurder(false, false));

        await using var db = fixture.NyDbContext();
        var kanter = new StrukturkantTjeneste(db);
        var kommunelov = await KommunelovenAsync(db);
        var verden = await DepartementerAsync(db, kanter);
        var rapport = await Skriving(db).KjorAsync(skriv: true, "test");
        var medlemskap = new KlassemedlemskapTjeneste(db, kanter);

        // Klassevisningen: fylkeskommune har begge, departement bare medlemsregel, en klasse uten noe er hullet.
        var fylkeskommune = (await medlemskap.GrunnlagAsync(rapport.Klasser.Single(k => k.Term == "fylkeskommune").KlasseId!.Value))!;
        Assert.Equal(Klassegrunnlag.Begge, fylkeskommune.Status);
        Assert.Equal(kommunelov, fylkeskommune.Hjemmel!.RettskildeId);
        Assert.Equal(new[] { Klasseregler.KommunelovenFylkeskommuneEid, Klasseregler.KommunelovenNavneregelEid }, fylkeskommune.RegelKilder.Select(k => k.Eid));
        var departement = (await medlemskap.GrunnlagAsync(rapport.Klasser.Single(k => k.Term == "departement").KlasseId!.Value))!;
        Assert.Equal((Klassegrunnlag.BareMedlemsregel, (KlassegrunnlagKilde?)null), (departement.Status, departement.Hjemmel));
        Assert.Equal(Klasseregler.OrganiseringsvedtakDepartement, departement.Medlemsregel!.Regel);
        var tom = new BegrepEntitet { Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Klasse, Term = $"tomklasse {Kort()}", Status = "publisert", OpprettetAv = "test" };
        db.Begreper.Add(tom);
        await db.SaveChangesAsync();
        Assert.Equal(Klassegrunnlag.Mangler, (await medlemskap.GrunnlagAsync(tom.Id))!.Status);

        // Hjemmelkortet på aktørsiden: et departement uten hjemmel for aktøren (kgl.res.-premisset) viser klassen med medlemsregelen, ikke hullet.
        var ud = verden.Med3[0];
        var aktorside = new AktorsideTjeneste(db, kanter,
            new AktoridentitetTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new VirksomhetOppslagTjeneste(db)));
        var hjemmel = (await aktorside.HjemmelAsync(ud))!;
        Assert.Equal("mangler", hjemmel.Status);
        var klasserad = Assert.Single(hjemmel.DetSomFinnes, d => d.Art == "klasse" && d.Tekst.Contains("«departement»"));
        Assert.Equal(Klassegrunnlag.BareMedlemsregel, klasserad.Grunnlag);
        Assert.NotNull(klasserad.Medlemsregel);
        Assert.Equal(Strukturkanter.Fortolkning, klasserad.Kunnskapsstatus);
        Assert.True(klasserad.Forslag);
    }

    [Fact]
    public async Task Klassen_kommunene_arkiveres_med_begrunnelse_og_proveniens_og_taggen_og_kanten_med_den()
    {
        await using var db = fixture.NyDbContext();
        var kanter = new StrukturkantTjeneste(db);
        await KommunelovenAsync(db);
        var reindrift = await KildeAsync(db, "… lov 21. desember 1984 nr. 101 om reindrift i kommunene Meldal, Midtre Gauldal …");
        // Klassen finnes bare én gang i en ekte base; en eldre testkjøring kan ha arkivert den. Da opprettes en ny gjeldende.
        var kommunene = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Klasse, Term = "kommunene", LovkildeId = reindrift.Id,
            LovreferanseEid = KlasseoppsettTjeneste.KommuneneEid, Status = "publisert", OpprettetAv = "test",
        };
        db.Begreper.Add(kommunene);
        var eier = await AktorAsync(db, $"Taggeier {Kort()}");
        var tagg = new TekstTaggEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = eier.Id, RettskildeId = reindrift.Id, NodeEid = reindrift.Eid, StartOffset = 39, EndOffset = 48,
            QuotePrefix = "", QuoteExact = "kommunene", QuoteSuffix = "", NodeTekstHash = "x", Kind = "begrep", RefId = kommunene.Id, OpprettetAv = "test",
        };
        db.TekstTagger.Add(tagg);
        await db.SaveChangesAsync();
        var kant = (await kanter.OpprettAsync(new NyStrukturkant(Strukturkanter.Medlemskap, "medlem_av", Kantnode.Begrep(kommunene.Id),
            Kantnode.Begrep(Klasseregler.KommuneKlasseId), HjemmelRettskildeId: reindrift.Id, HjemmelEid: reindrift.Eid), "test")).Kant;

        var rapport = await Skriving(db).KjorAsync(skriv: true, "Kari Jurist");
        Assert.Equal(("arkivert", 1, 1), (rapport.Kommunene.Utfall, rapport.Kommunene.Tagger, rapport.Kommunene.Kanter));

        await using var les = fixture.NyDbContext();
        var b = await les.Begreper.SingleAsync(x => x.Id == kommunene.Id);
        Assert.Equal(("arkivert", "arkivert", Strukturkanter.Avvist, "Kari Jurist"), (b.Entitetsstatus, b.Status, b.Godkjenning, b.SistEndretAv));
        Assert.Contains("tittelen på lov 21. desember 1984 nr. 101", b.AvvisningBegrunnelse);
        var prov = await les.Proveniens.SingleAsync(p => p.EntitetId == kommunene.Id && p.Handling == "arkivert");
        Assert.Contains("N18", prov.KildeReferanserJson);
        Assert.Equal("arkivert", (await les.TekstTagger.SingleAsync(t => t.Id == tagg.Id)).Entitetsstatus);
        Assert.Equal(1, await les.Proveniens.CountAsync(p => p.EntitetId == tagg.Id && p.Handling == "arkivert"));
        Assert.Equal(Strukturkanter.Foreldet, (await les.Strukturkanter.IgnoreQueryFilters().SingleAsync(k => k.Id == kant.Id)).Entitetsstatus);
        Assert.Equal(1, await les.Proveniens.CountAsync(p => p.EntitetId == kant.Id && p.Handling == "arkivert"));

        // En ny kjøring arkiverer ikke på nytt.
        await using var igjen = fixture.NyDbContext();
        Assert.Equal("alt_arkivert", (await Skriving(igjen).KjorAsync(skriv: true, "test")).Kommunene.Utfall);
    }

    [Fact]
    public async Task Skrivingen_er_idempotent_og_torrkjoringen_skriver_ingenting()
    {
        await using var db = fixture.NyDbContext();
        var kanter = new StrukturkantTjeneste(db);
        await KommunelovenAsync(db);
        var fylker = await FylkerAsync(db, kanter);
        var departementer = await DepartementerAsync(db, kanter);

        async Task<(int Begreper, int Kanter, int Proveniens)> TellAsync()
        {
            await using var t = fixture.NyDbContext();
            return (await t.Begreper.IgnoreQueryFilters().CountAsync(), await t.Strukturkanter.IgnoreQueryFilters().CountAsync(), await t.Proveniens.CountAsync());
        }

        // Tørrkjøring: samme tall som skrivingen, ingenting lagret.
        var for1 = await TellAsync();
        var torr = await Skriving(db).KjorAsync(skriv: false, "test");
        Assert.False(torr.Skrevet);
        Assert.Equal(for1, await TellAsync());
        Assert.Equal(14, torr.Territoriekanter.Count(t => fylker.Fylkeskommuner.ContainsValue(t.VirksomhetId) && t.Utfall == "ny"));
        Assert.Equal(7, torr.Medlemskanter.Count(m => (departementer.Med4.Contains(m.VirksomhetId) || departementer.Med3.Contains(m.VirksomhetId))
                                                       && m.Utfall is "ny" or "finnes"));

        // Første skriving, så en ny: den andre skriver ingenting og sier «finnes».
        await using var db1 = fixture.NyDbContext();
        var forste = await Skriving(db1).KjorAsync(skriv: true, "test");
        Assert.True(forste.Skrevet);
        var etterForste = await TellAsync();
        await using var db2 = fixture.NyDbContext();
        var andre = await Skriving(db2).KjorAsync(skriv: true, "test");
        Assert.Equal(etterForste, await TellAsync());
        Assert.All(andre.Klasser, k => Assert.Equal("finnes", k.Utfall));
        Assert.All(andre.Territoriekanter, t => Assert.Equal("finnes", t.Utfall));
        Assert.All(andre.Medlemskanter, m => Assert.Equal("finnes", m.Utfall));

        // Et avvist forslag foreslås ikke på nytt.
        var enKant = forste.Territoriekanter.First(t => fylker.Fylkeskommuner.ContainsValue(t.VirksomhetId)).KantId!.Value;
        await new StrukturkantTjeneste(db2).AvvisAsync(enKant, "Kari Jurist", "test");
        await using var db3 = fixture.NyDbContext();
        var tredje = await Skriving(db3).KjorAsync(skriv: true, "test");
        Assert.Equal("avvist", tredje.Territoriekanter.Single(t => t.KantId == enKant).Utfall);
        Assert.Equal(etterForste.Kanter, (await TellAsync()).Kanter);
    }

    [Fact]
    public async Task Check_tillater_de_nye_reglene_bare_paa_klasser()
    {
        await using var db = fixture.NyDbContext();
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Rolle, Term = $"rolle {Kort()}", Medlemskapsregel = Klasseregler.OrganiseringsvedtakDepartement,
            Status = "publisert", OpprettetAv = "test",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Klasse, Term = $"klasse {Kort()}", Medlemskapsregel = "ukjent_regel",
            Status = "publisert", OpprettetAv = "test",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
