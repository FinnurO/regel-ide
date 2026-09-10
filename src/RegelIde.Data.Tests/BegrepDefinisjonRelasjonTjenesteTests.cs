using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Ren funksjonstest av <see cref="BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon"/> —
/// ingen DB nødvendig. Dekker Johanns deteksjonsregel (issue #212, kommentar 2026-09-10) direkte mot de
/// konkrete eksemplene issuet selv lister («komma»/«ph.d.-»/«annet ord»/«(er)») pluss «Sidefunn»-tilfellet
/// (ledende leddnummer).</summary>
public class BegrepDefinisjonRelasjonNormaliseringTests
{
    [Fact]
    public void Komma_normaliseres_bort_slik_at_ellers_identiske_tekster_matcher()
    {
        // Issuets eget eksempel: samme definisjon, ett har komma foran "der" og det andre ikke.
        var a = "Med fellesgrader menes et samarbeid mellom flere institusjoner, der alle i fellesskap …";
        var b = "Med fellesgrader menes et samarbeid mellom flere institusjoner der alle i fellesskap …";
        Assert.Equal(BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon(a), BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon(b));
    }

    [Fact]
    public void StoreSmaBokstaver_og_mellomrom_normaliseres_bort()
    {
        Assert.Equal(
            BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon("Med   Fellesgrader   menes X"),
            BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon("med fellesgrader menes x"));
    }

    [Fact]
    public void Bindestrek_og_parentes_droppes_helt_ikke_erstattet_med_mellomrom()
    {
        // "(er)" og bindestreken skal forsvinne uten å etterlate et mellomrom — «kandidat(er)» blir
        // "kandidater", ikke "kandidat er".
        Assert.Equal("cotutelleavtaler", BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon("cotutelle-avtaler"));
        Assert.Equal("phdkandidater", BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon("ph.d.-kandidat(er)"));
    }

    [Fact]
    public void Reelt_ordavvik_gir_ULIK_normalisert_tekst_ikke_relatert_automatisk()
    {
        // Issuets «annet ord»-eksempel — doktorgradsstudenter vs. ph.d.-kandidat(er) er et EKTE avvik,
        // ikke støy, og skal derfor IKKE regnes som samme definisjon (Johanns eksplisitte regel).
        var a = "Med cotutelle-avtaler menes felles veiledning av doktorgradsstudenter og …";
        var b = "Med cotutelle-avtaler menes felles veiledning av ph.d.-kandidat(er) og …";
        Assert.NotEqual(BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon(a), BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon(b));
    }

    [Fact]
    public void Ledende_leddnummer_strippes_sidefunnet_i_issue_212()
    {
        // "(1) Med fellesgrad menes X" skal sammenlignes likt med "Med fellesgrad menes X" — se
        // issuets «Sidefunn»-seksjon (leddnummer lekket inn i Tekst ved import for tre dokumenter).
        Assert.Equal(
            BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon("(1) Med fellesgrad menes X"),
            BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon("Med fellesgrad menes X"));
    }

    [Fact]
    public void Leddnummer_midt_i_teksten_er_IKKE_et_leddnummer_prefiks_beholdes()
    {
        // Regelen er BEVISST forankret til STARTEN av strengen (^) — et tall i parentes et annet sted i
        // teksten er ikke et leddmerke og skal ikke fjernes.
        Assert.Equal("med x menes y 1 og z", BegrepDefinisjonRelasjonTjeneste.NormaliserDefinisjon("Med X menes Y (1) og Z"));
    }
}

/// <summary>
/// <see cref="BegrepDefinisjonRelasjonTjeneste"/> mot ekte embedded Postgres — deteksjon (SveipAsync),
/// kø (Lister/Avvis) og bekreftelse (GodkjennAsync, inkl. kravet om at begge forekomster allerede må
/// være godkjent til et Begrep — se klassekommentaren for hvorfor).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class BegrepDefinisjonRelasjonTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BegrepDefinisjonRelasjonTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<(Guid RettskildeId, RettskildeNodeEntitet Node)> OpprettSyntetiskRettskildeAsync(
        RegelIdeDbContext db, string term, string definisjonstekst)
    {
        var rettskildeId = Guid.NewGuid();
        var nodeEid = $"https://test/{rettskildeId:N}/§1/ledd-1/punkt-1";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testforskrift " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var node = new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid, KildeId = "punkt-1",
            NodeType = "punkt", Tekst = $"{term}: {definisjonstekst}",
        };
        db.RettskildeNoder.Add(node);
        await db.SaveChangesAsync();
        return (rettskildeId, node);
    }

    private static async Task<BegrepsforekomstEntitet> OpprettForekomstAsync(
        RegelIdeDbContext db, Guid rettskildeId, RettskildeNodeEntitet node, string term, string definisjon)
    {
        var forekomstTjeneste = new BegrepsforekomstTjeneste(
            db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new BegrepsregisterTjeneste(db));
        return await forekomstTjeneste.OpprettEllerFinnAsync(
            rettskildeId, node.Eid, term, term, definisjon,
            "egen_paragraf", "M11", "hoy", "hele_dokumentet", null, 0, term.Length, "sveip");
    }

    [Fact]
    public async Task Sveip_foreslar_par_pa_tvers_av_rettskilder_med_eksakt_lik_definisjon_etter_normalisering()
    {
        await using var db = _fixture.NyDbContext();
        // To ulike forskrifter, samme term, definisjon som kun skiller seg i tegnsetting/case — skal matche.
        var (r1, n1) = await OpprettSyntetiskRettskildeAsync(db, "fellesgrader", "et samarbeid, der alle deltar");
        var (r2, n2) = await OpprettSyntetiskRettskildeAsync(db, "fellesgrader", "ET SAMARBEID der alle deltar");
        var f1 = await OpprettForekomstAsync(db, r1, n1, "fellesgrader", "Med fellesgrader menes et samarbeid, der alle deltar");
        var f2 = await OpprettForekomstAsync(db, r2, n2, "fellesgrader", "Med fellesgrader menes ET SAMARBEID der alle deltar");

        var tjeneste = new BegrepDefinisjonRelasjonTjeneste(db);
        // Sveipet er BEVISST scopet til de to syntetiske rettskildene denne testen selv opprettet —
        // embedded Postgres er DELT på tvers av hele testkolleksjonen (EmbeddedPostgresFixture-
        // kommentaren), så et uscopet sveip ville også plukket opp forekomster fra andre tester.
        var resultat = await tjeneste.SveipAsync("sveip", [r1, r2]);

        Assert.Equal(1, resultat.AntallGrupperFunnet);
        Assert.Equal(1, resultat.AntallNyeKandidater);

        var ventende = await tjeneste.ListerVentendeAsync();
        var kandidat = Assert.Single(ventende, k => (k.FraForekomstId == f1.Id || k.FraForekomstId == f2.Id)
                                                            && (k.TilForekomstId == f1.Id || k.TilForekomstId == f2.Id));
        Assert.Equal(new[] { f1.Id, f2.Id }.OrderBy(x => x), new[] { kandidat.FraForekomstId, kandidat.TilForekomstId }.OrderBy(x => x));
    }

    [Fact]
    public async Task Sveip_foreslar_IKKE_par_med_reelt_ordavvik()
    {
        await using var db = _fixture.NyDbContext();
        var (r1, n1) = await OpprettSyntetiskRettskildeAsync(db, "cotutelle-avtaler", "veiledning av doktorgradsstudenter");
        var (r2, n2) = await OpprettSyntetiskRettskildeAsync(db, "cotutelle-avtaler", "veiledning av ph.d.-kandidater");
        var f1 = await OpprettForekomstAsync(db, r1, n1, "cotutelle-avtaler", "Med cotutelle-avtaler menes felles veiledning av doktorgradsstudenter");
        var f2 = await OpprettForekomstAsync(db, r2, n2, "cotutelle-avtaler", "Med cotutelle-avtaler menes felles veiledning av ph.d.-kandidater");

        var tjeneste = new BegrepDefinisjonRelasjonTjeneste(db);
        var resultat = await tjeneste.SveipAsync("sveip", [r1, r2]);

        Assert.Equal(0, resultat.AntallGrupperFunnet);
        var ventende = await tjeneste.ListerVentendeAsync();
        Assert.DoesNotContain(ventende, k => k.FraForekomstId == f1.Id || k.TilForekomstId == f1.Id
                                              || k.FraForekomstId == f2.Id || k.TilForekomstId == f2.Id);
    }

    [Fact]
    public async Task Sveip_foreslar_IKKE_par_innenfor_samme_rettskilde()
    {
        // Samme forskrift, definisjonen gjentatt to steder — det er duplikatimport, ikke en
        // tverr-forskrift-kobling, og skal ikke gi et kandidatpar.
        await using var db = _fixture.NyDbContext();
        var (r1, n1) = await OpprettSyntetiskRettskildeAsync(db, "x", "en definisjon");
        var node2 = new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = r1, Eid = n1.Eid + "-2", KildeId = "punkt-2",
            NodeType = "punkt", Tekst = "y: en definisjon",
        };
        db.RettskildeNoder.Add(node2);
        await db.SaveChangesAsync();
        await OpprettForekomstAsync(db, r1, n1, "x", "Med x menes en definisjon");
        await OpprettForekomstAsync(db, r1, node2, "y", "Med x menes en definisjon");

        var tjeneste = new BegrepDefinisjonRelasjonTjeneste(db);
        var resultat = await tjeneste.SveipAsync("sveip", [r1]);

        Assert.Equal(0, resultat.AntallNyeKandidater);
    }

    [Fact]
    public async Task Gjentatt_sveip_lager_ingen_dubletter()
    {
        await using var db = _fixture.NyDbContext();
        var (r1, n1) = await OpprettSyntetiskRettskildeAsync(db, "x", "en definisjon");
        var (r2, n2) = await OpprettSyntetiskRettskildeAsync(db, "x", "en definisjon");
        var f1 = await OpprettForekomstAsync(db, r1, n1, "x", "Med x menes en definisjon");
        var f2 = await OpprettForekomstAsync(db, r2, n2, "x", "Med x menes en definisjon");

        var tjeneste = new BegrepDefinisjonRelasjonTjeneste(db);
        await tjeneste.SveipAsync("sveip", [r1, r2]);
        var andreSveip = await tjeneste.SveipAsync("sveip", [r1, r2]);

        Assert.Equal(0, andreSveip.AntallNyeKandidater);
        var ventende = await tjeneste.ListerVentendeAsync();
        Assert.Single(ventende, k => (k.FraForekomstId == f1.Id || k.FraForekomstId == f2.Id)
                                            && (k.TilForekomstId == f1.Id || k.TilForekomstId == f2.Id));
    }

    [Fact]
    public async Task Godkjenn_kaster_hvis_forekomstene_ikke_er_godkjent_til_begrep_ennaa()
    {
        await using var db = _fixture.NyDbContext();
        var (r1, n1) = await OpprettSyntetiskRettskildeAsync(db, "x", "en definisjon");
        var (r2, n2) = await OpprettSyntetiskRettskildeAsync(db, "x", "en definisjon");
        var f1 = await OpprettForekomstAsync(db, r1, n1, "x", "Med x menes en definisjon");
        var f2 = await OpprettForekomstAsync(db, r2, n2, "x", "Med x menes en definisjon");

        var tjeneste = new BegrepDefinisjonRelasjonTjeneste(db);
        await tjeneste.SveipAsync("sveip", [r1, r2]);
        var ventende = await tjeneste.ListerVentendeAsync();
        var kandidat = Assert.Single(ventende, k => (k.FraForekomstId == f1.Id || k.FraForekomstId == f2.Id)
                                                           && (k.TilForekomstId == f1.Id || k.TilForekomstId == f2.Id));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist"));
        Assert.Contains("godkjent til et Begrep", ex.Message);
    }

    [Fact]
    public async Task Godkjenn_oppretter_bekreftet_relasjon_for_begge_retninger_etter_at_begge_er_godkjent_til_begrep()
    {
        await using var db = _fixture.NyDbContext();
        var (r1, n1) = await OpprettSyntetiskRettskildeAsync(db, "x", "en definisjon");
        var (r2, n2) = await OpprettSyntetiskRettskildeAsync(db, "x", "en definisjon");
        var f1 = await OpprettForekomstAsync(db, r1, n1, "x", "Med x menes en definisjon");
        var f2 = await OpprettForekomstAsync(db, r2, n2, "x", "Med x menes en definisjon");

        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var forekomstTjeneste = new BegrepsforekomstTjeneste(
            db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new BegrepsregisterTjeneste(db));
        var g1 = await forekomstTjeneste.GodkjennAsync(f1.Id, virksomhet.Id, "Kari Jurist");
        var g2 = await forekomstTjeneste.GodkjennAsync(f2.Id, virksomhet.Id, "Kari Jurist");
        Assert.NotNull(g1?.BegrepId);
        Assert.NotNull(g2?.BegrepId);

        var relasjonTjeneste = new BegrepDefinisjonRelasjonTjeneste(db);
        await relasjonTjeneste.SveipAsync("sveip", [r1, r2]);
        var ventendeKandidater = await relasjonTjeneste.ListerVentendeAsync();
        var kandidat = Assert.Single(ventendeKandidater, k => (k.FraForekomstId == f1.Id || k.FraForekomstId == f2.Id)
                                                                     && (k.TilForekomstId == f1.Id || k.TilForekomstId == f2.Id));

        var godkjent = await relasjonTjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist");
        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.Equal("Kari Jurist", godkjent.BehandletAv);

        var fraA = await relasjonTjeneste.ListerRelasjonerForBegrepAsync(g1!.BegrepId!.Value);
        var fraB = await relasjonTjeneste.ListerRelasjonerForBegrepAsync(g2!.BegrepId!.Value);
        Assert.Single(fraA);
        Assert.Single(fraB);
        Assert.Equal(g2.BegrepId, fraA[0].TilBegrepId);
        Assert.Equal(g1.BegrepId, fraB[0].TilBegrepId);
        Assert.Equal("sveip", fraA[0].Kilde);
    }

    [Fact]
    public async Task Avvist_kandidat_kan_ikke_godkjennes_eller_avvises_pa_nytt()
    {
        await using var db = _fixture.NyDbContext();
        var (r1, n1) = await OpprettSyntetiskRettskildeAsync(db, "x", "en definisjon");
        var (r2, n2) = await OpprettSyntetiskRettskildeAsync(db, "x", "en definisjon");
        var f1 = await OpprettForekomstAsync(db, r1, n1, "x", "Med x menes en definisjon");
        var f2 = await OpprettForekomstAsync(db, r2, n2, "x", "Med x menes en definisjon");

        var tjeneste = new BegrepDefinisjonRelasjonTjeneste(db);
        await tjeneste.SveipAsync("sveip", [r1, r2]);
        var ventende = await tjeneste.ListerVentendeAsync();
        var kandidat = Assert.Single(ventende, k => (k.FraForekomstId == f1.Id || k.FraForekomstId == f2.Id)
                                                           && (k.TilForekomstId == f1.Id || k.TilForekomstId == f2.Id));

        var avvist = await tjeneste.AvvisAsync(kandidat.Id, "Kari Jurist");
        Assert.Equal("Avvist", avvist!.Status);
        Assert.DoesNotContain(await tjeneste.ListerVentendeAsync(), k => k.Id == kandidat.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.AvvisAsync(kandidat.Id, "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.GodkjennAsync(kandidat.Id, "Kari Jurist"));
    }
}
