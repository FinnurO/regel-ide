using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// Begrepsoppdagelse — M1 (eksplisitt definisjonsliste) og M11 (egen definisjonsparagraf), docs/24 §3.
/// To deler: rene enhetstester av selve mønstergjenkjenningen
/// (<see cref="BegrepsoppdagelseSveipTjeneste.FinnForekomster"/>, ingen DB — samme "internal static, rask
/// og presis" -mønster som <c>NavnekandidatOppdagelseTjenesteTests</c>) og integrasjonstester av selve
/// sveipet (<see cref="BegrepsoppdagelseSveipTjeneste.SveipAsync"/>) mot ekte embedded Postgres.
/// <para>
/// Del A sin M1-test bruker den EKSAKTE nodestrukturen/teksten fra FOR-2015-06-25-793
/// (pasientreiseforskriften) § 1, hentet direkte fra den kjørende dev-databasen 2026-09-02 (docs/24
/// §1.3 sin "nærmest perfekte M1-testcase") — konstruert som <see cref="NodeSnapshot"/>-rader her i
/// stedet for en full HTML-reimport, siden selve klassifiseringsfunksjonen er DB-uavhengig og dette gir
/// samme reelle valideringsverdi uten en embedded Postgres-avhengighet for denne delen. Del A sin
/// M11-test bruker tilsvarende den ekte teksten fra folketrygdloven §§ 1-8/1-9/1-10/13-3 (samme kilde,
/// samme dato).
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class BegrepsoppdagelseSveipTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BegrepsoppdagelseSveipTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------- Del A: ren mønstergjenkjenning, ingen DB ----------

    /// <summary>Ekte struktur/tekst fra FOR-2015-06-25-793 § 1 (pasientreiseforskriften), bekreftet mot
    /// den kjørende dev-databasen 2026-09-02 — paragraf "Definisjoner", ett ledd ("I forskriften her
    /// menes med"), fem punkt-barn av formen "term: forklaring".</summary>
    private static List<NodeSnapshot> PasientreiseforskriftenParagraf1()
    {
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        const string basis = "https://lovdata.no/eli/forskrift/2015/06/25/793/nor/§1";
        return
        [
            new NodeSnapshot(paragrafId, null, basis, "paragraf", "Definisjoner", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, $"{basis}/ledd-1", "ledd", null, "I forskriften her menes med", 2, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-1", "punkt", null,
                "reisestønad: stønad til dekning av nødvendige utgifter til reise", 3, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-2", "punkt", null,
                "bosted: pasientens folkeregistrerte adresse. Som bosted regnes også nødvendig midlertidig " +
                "oppholdssted på grunn av arbeid, studier, militærtjeneste og opphold i institusjon.", 4, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-3", "punkt", null,
                "bostedskommune: kommunen der pasienten har folkeregistrert adresse.", 5, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-4", "punkt", null,
                "bostedsregion: region som nevnt i spesialisthelsetjenesteloven § 5-1.", 6, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-5", "punkt", null,
                "nære pårørende: ektefelle, samboer, barn, barnebarn, foreldre, besteforeldre, svigerbarn, " +
                "svigerforeldre, søsken og personer som tilhører pasientens husstand.", 7, false),
        ];
    }

    [Fact]
    public void M1_pasientreiseforskriften_par1_gir_fem_forekomster_med_riktig_begrep_og_definisjon()
    {
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(PasientreiseforskriftenParagraf1());

        Assert.Equal(5, funn.Count);
        Assert.All(funn, f =>
        {
            Assert.Equal("M1", f.MonsterId);
            Assert.Equal("eksplisitt_liste", f.Kildetype);
            Assert.Equal("hoy", f.Konfidens);
            Assert.Equal("hele_dokumentet", f.Scope);
            Assert.Null(f.ScopeRefEid);
        });

        var reisestønad = funn.Single(f => f.Begrep == "reisestønad");
        Assert.Equal("reisestønad", reisestønad.BegrepOriginal);
        Assert.Equal("stønad til dekning av nødvendige utgifter til reise", reisestønad.Definisjon);
        Assert.Equal("https://lovdata.no/eli/forskrift/2015/06/25/793/nor/§1/ledd-1/punkt-1", reisestønad.NodeEid);

        var bostedsregion = funn.Single(f => f.Begrep == "bostedsregion");
        Assert.Equal("region som nevnt i spesialisthelsetjenesteloven § 5-1.", bostedsregion.Definisjon);

        // "nære pårørende" — begrepet selv inneholder mellomrom, første kolon skiller likevel korrekt.
        var nærePårørende = funn.Single(f => f.Begrep == "nære pårørende");
        Assert.StartsWith("ektefelle, samboer, barn", nærePårørende.Definisjon);
    }

    [Fact]
    public void M1_termens_tegnintervall_peker_eksakt_pa_selve_termen_ikke_kolon_eller_forklaring()
    {
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(PasientreiseforskriftenParagraf1());
        var reisestønad = funn.Single(f => f.Begrep == "reisestønad");
        const string punktTekst = "reisestønad: stønad til dekning av nødvendige utgifter til reise";

        Assert.Equal("reisestønad", punktTekst[reisestønad.StartOffset..reisestønad.EndOffset]);
    }

    [Fact]
    public void M1_trigges_ogsa_av_ledd_tekst_alene_uten_definisjon_i_paragrafoverskriften()
    {
        // Samme intro-frase ("... menes med"), men paragrafens EGEN overskrift sier ikke "definisjon" —
        // signal (a) og (b) i klassekommentaren er et OR, ikke et AND.
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§9", "paragraf", "Andre bestemmelser", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, "https://test/§9/ledd-1", "ledd", null, "I denne forskriften menes med", 2, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, "https://test/§9/ledd-1/punkt-1", "punkt", null, "x: y", 3, false),
        ];

        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder);

        Assert.Single(funn);
        Assert.Equal("M1", funn[0].MonsterId);
    }

    [Fact]
    public void M1_ignorerer_nostet_punkt_under_et_definisjonspunkt()
    {
        // docs/24 §1.3, "Mindre observasjon" — et listepunkt i en definisjonsliste kan selv inneholde en
        // nøstet liste (bekreftet i ekte data, alkoholforskriften § 6-2). Kun DIREKTE punkt-barn av selve
        // definisjons-leddet skal telle — et barnebarn av leddet (barn av et punkt) skal IKKE telle som
        // en egen forekomst.
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        var punktId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§1", "paragraf", "Definisjoner", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, "https://test/§1/ledd-1", "ledd", null, "I loven her menes med", 2, false),
            new NodeSnapshot(punktId, leddId, "https://test/§1/ledd-1/punkt-1", "punkt", null,
                "vurderingsmoment: et begrep med en nøstet liste av momenter", 3, false),
            new NodeSnapshot(Guid.NewGuid(), punktId, "https://test/§1/ledd-1/punkt-1/punkt-1", "punkt", null,
                "et moment som IKKE er en egen definisjon", 4, false),
        ];

        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder);

        Assert.Single(funn); // kun det ytterste punktet — det nøstede barnet telles ikke separat.
        Assert.Equal("vurderingsmoment", funn[0].Begrep);
    }

    /// <summary>Ekte tekst fra folketrygdloven §§ 1-8/1-9/1-10/13-3, bekreftet mot den kjørende
    /// dev-databasen 2026-09-02 — fire uavhengige, reelle M11-treff i samme lov.</summary>
    private static List<NodeSnapshot> FolketrygdlovenM11Paragrafer()
    {
        static (NodeSnapshot Paragraf, NodeSnapshot Ledd) Par(string eid, string overskrift, string leddTekst)
        {
            var paragrafId = Guid.NewGuid();
            var basis = $"https://lovdata.no/eli/lov/1997/02/28/19/nor/{eid}";
            return (
                new NodeSnapshot(paragrafId, null, basis, "paragraf", overskrift, null, 1, false),
                new NodeSnapshot(Guid.NewGuid(), paragrafId, $"{basis}/ledd-1", "ledd", null, leddTekst, 2, false));
        }

        var arbeidstaker = Par("§1-8", "Arbeidstaker",
            "Med arbeidstaker menes i denne loven enhver som arbeider i en annens tjeneste for lønn eller annen godtgjørelse.");
        var frilanser = Par("§1-9", "Frilanser",
            "Med frilanser menes i denne loven enhver som utfører arbeid eller oppdrag utenfor tjeneste for lønn " +
            "eller annen godtgjørelse, men uten å være selvstendig næringsdrivende, se § 1-10.");
        var selvstendig = Par("§1-10", "Selvstendig næringsdrivende",
            "Med selvstendig næringsdrivende menes i denne loven enhver som for egen regning og risiko driver en " +
            "vedvarende virksomhet som er egnet til å gi nettoinntekt.");
        var yrkesskade = Par("§13-3", "Yrkesskade",
            "Med yrkesskade menes en personskade, en sykdom eller et dødsfall som skyldes en arbeidsulykke som " +
            "skjer mens medlemmet er yrkesskadedekket, se §§ 13-6 til 13-13.");
        // Negativ kontroll: § 6-2 "Sykdom, skade eller lyte" — overskriften er IKKE selve definisjonen
        // ("Det er et vilkår ..." er en vilkårsbestemmelse, ikke "Med X menes ...").
        var sykdomSkadeLyte = Par("§6-2", "Sykdom, skade eller lyte",
            "Det er et vilkår for rett til stønad etter dette kapitlet at medlemmet etter hensiktsmessig " +
            "behandling fortsatt har varig sykdom, skade eller lyte.");

        return
        [
            arbeidstaker.Paragraf, arbeidstaker.Ledd,
            frilanser.Paragraf, frilanser.Ledd,
            selvstendig.Paragraf, selvstendig.Ledd,
            yrkesskade.Paragraf, yrkesskade.Ledd,
            sykdomSkadeLyte.Paragraf, sykdomSkadeLyte.Ledd,
        ];
    }

    [Fact]
    public void M11_folketrygdloven_gir_fire_reelle_treff_og_ingen_falskt_positiv_pa_ikke_definert_overskrift()
    {
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(FolketrygdlovenM11Paragrafer());

        Assert.Equal(4, funn.Count); // IKKE fem — "Sykdom, skade eller lyte" skal ikke gi et falskt treff.
        Assert.All(funn, f =>
        {
            Assert.Equal("M11", f.MonsterId);
            Assert.Equal("egen_paragraf", f.Kildetype);
            Assert.Equal("hoy", f.Konfidens);
            Assert.Equal("hele_dokumentet", f.Scope);
        });
        Assert.Contains(funn, f => f.Begrep == "arbeidstaker");
        Assert.Contains(funn, f => f.Begrep == "frilanser");
        Assert.Contains(funn, f => f.Begrep == "selvstendig næringsdrivende");
        Assert.Contains(funn, f => f.Begrep == "yrkesskade");
        Assert.DoesNotContain(funn, f => f.Begrep.Contains("sykdom") || f.Begrep.Contains("lyte"));

        var arbeidstaker = funn.Single(f => f.Begrep == "arbeidstaker");
        Assert.Equal(
            "Med arbeidstaker menes i denne loven enhver som arbeider i en annens tjeneste for lønn eller annen godtgjørelse.",
            arbeidstaker.Definisjon);
        Assert.Equal("arbeidstaker", arbeidstaker.Definisjon[arbeidstaker.StartOffset..arbeidstaker.EndOffset]);
    }

    [Fact]
    public void M11_krever_eksplisitt_menes_markor_ikke_bare_kort_overskrift()
    {
        // Presisjonsvernet klassekommentaren nevner: en kort paragraf-overskrift ALENE ("Formål",
        // "Grunnbeløpet") skal IKKE trigge M11 uten den eksplisitte "Med X menes/forstås/regnes"-markøren
        // — det er nettopp copula-varianten (M13), eksplisitt utenfor scope denne runden.
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§1-4", "paragraf", "Grunnbeløpet", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, "https://test/§1-4/ledd-1", "ledd", null,
                "Grunnbeløpet fastsettes av Kongen og reguleres årlig.", 2, false),
        ];

        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder);

        Assert.Empty(funn);
    }

    [Fact]
    public void Opphevet_paragraf_og_ledd_gir_ingen_forekomster()
    {
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§1", "paragraf", "Definisjoner", null, 1, true),
            new NodeSnapshot(leddId, paragrafId, "https://test/§1/ledd-1", "ledd", null, "I loven her menes med", 2, true),
            new NodeSnapshot(Guid.NewGuid(), leddId, "https://test/§1/ledd-1/punkt-1", "punkt", null, "x: y", 3, false),
        ];

        Assert.Empty(BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder));
    }

    // ---------- Del B: sveip + kø mot ekte embedded Postgres ----------

    private static BegrepsoppdagelseSveipTjeneste NySveip(RegelIdeDbContext db) =>
        new(db, new BegrepsforekomstTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new BegrepsregisterTjeneste(db)));

    /// <summary>Fersk, syntetisk, DELT/nasjonal (VirksomhetId=null) rettskilde med en M1-definisjonsparagraf
    /// — samme "egen syntetisk rettskilde per test" -mønster som
    /// <c>VirksomhetKandidatTjenesteTests.OpprettSyntetiskRettskildeAsync</c>, av samme grunn (DB-en er
    /// DELT mellom alle tester i samlingen).</summary>
    private static async Task<(Guid RettskildeId, string PunktEid)> OpprettSyntetiskM1RettskildeAsync(RegelIdeDbContext db)
    {
        var rettskildeId = Guid.NewGuid();
        var basis = $"https://test/{rettskildeId:N}";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testforskrift " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        var punktEid = $"{basis}/§1/ledd-1/punkt-1";
        db.RettskildeNoder.AddRange(
            new RettskildeNodeEntitet
            {
                Id = paragrafId, RettskildeId = rettskildeId, Eid = $"{basis}/§1", KildeId = "§1",
                NodeType = "paragraf", Overskrift = "Definisjoner", Sorteringsrekkefolge = 1,
            },
            new RettskildeNodeEntitet
            {
                Id = leddId, RettskildeId = rettskildeId, Eid = $"{basis}/§1/ledd-1", KildeId = "ledd-1",
                ParentNodeId = paragrafId, NodeType = "ledd", Tekst = "I forskriften her menes med", Sorteringsrekkefolge = 2,
            },
            new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = punktEid, KildeId = "punkt-1",
                ParentNodeId = leddId, NodeType = "punkt", Tekst = "testbegrep: en testdefinisjon", Sorteringsrekkefolge = 3,
            });
        await db.SaveChangesAsync();
        return (rettskildeId, punktEid);
    }

    [Fact]
    public async Task Sveip_oppretter_forekomst_med_riktig_felter()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, punktEid) = await OpprettSyntetiskM1RettskildeAsync(db);

        var resultat = await NySveip(db).SveipAsync(rettskildeId, "sveip");

        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(1, resultat.AntallNyeForekomster);
        var forekomst = await db.Begrepsforekomster.SingleAsync(f => f.RettskildeId == rettskildeId);
        Assert.Equal(punktEid, forekomst.NodeEid);
        Assert.Equal("testbegrep", forekomst.Begrep);
        Assert.Equal("en testdefinisjon", forekomst.Definisjon);
        Assert.Equal("M1", forekomst.MonsterId);
        Assert.Equal("Venter", forekomst.Status);
    }

    [Fact]
    public async Task Gjentatt_sveip_gir_ingen_duplikate_forekomster()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, _) = await OpprettSyntetiskM1RettskildeAsync(db);
        var sveip = NySveip(db);

        var forste = await sveip.SveipAsync(rettskildeId, "sveip");
        var andre = await sveip.SveipAsync(rettskildeId, "sveip");

        Assert.Equal(1, forste.AntallNyeForekomster);
        Assert.Equal(0, andre.AntallNyeForekomster); // samme treff igjen, men ingen ny rad.
        Assert.Equal(1, await db.Begrepsforekomster.CountAsync(f => f.RettskildeId == rettskildeId));
    }

    [Fact]
    public async Task Sveip_mot_rettskilde_eid_av_en_virksomhet_kastes()
    {
        // Samme defensive delt/nasjonal-scoping som VirksomhetKandidatSveipTjeneste/
        // NavnekandidatOppdagelseTjeneste — se klassekommentaren.
        await using var db = _fixture.NyDbContext();
        var rettskildeId = Guid.NewGuid();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, VirksomhetId = virksomhet.Id, Doctype = "doc", Kildetype = "Forskrift",
            Status = "Gjeldende", Importrolle = "referanse", Tittel = "Lokal forskrift " + rettskildeId,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => NySveip(db).SveipAsync(rettskildeId, "sveip"));
    }
}
