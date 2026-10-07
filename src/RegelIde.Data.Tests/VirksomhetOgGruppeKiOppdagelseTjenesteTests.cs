using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetOgGruppeKiOppdagelseTjeneste"/> (issue #285) mot ekte embedded Postgres og en
/// egen, deterministisk KI-stub (ikke <see cref="KiAgentKlientStub"/>, som returnerer et fast svar for
/// «Identifiser begrep» — denne agenten trenger et strukturert svar av en helt annen form). Samme
/// "aldri ekte nettverkskall i automatiserte tester"-prinsipp som resten av testsuiten — se docs/13
/// for AC1/AC7s manuelle, ekte KI-verifisering (dokumentert i PR-beskrivelsen, ikke her).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetOgGruppeKiOppdagelseTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetOgGruppeKiOppdagelseTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class StubKiKlient(string svar) : IKiAgentKlient
    {
        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default) =>
            Task.FromResult(new KiSvar(svar, InputTokens: 42, OutputTokens: 7));
    }

    /// <summary>Kaster hvis kalt — beviser at agenten ALDRI gjør et eksternt SNL/SSR-oppslag (den bruker
    /// kun <see cref="NavnekandidatOppdagelseTjeneste.OpprettEllerFinnAsync"/>, ikke <c>SveipAsync</c>/
    /// <c>KlassifiserAsync</c>, som er de eneste stedene <see cref="EksternNavneoppslagTjeneste"/> faktisk brukes).</summary>
    private sealed class KasterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Uventet eksternt HTTP-kall — VirksomhetOgGruppeKiOppdagelseTjeneste skal aldri utløse SNL/SSR-oppslag.");
    }

    private static VirksomhetOgGruppeKiOppdagelseTjeneste NyTjeneste(RegelIdeDbContext db, string kiSvar) => new(
        db, new StubKiKlient(kiSvar), new ConfigurationBuilder().Build(),
        new NavnekandidatOppdagelseTjeneste(
            db, new VirksomhetsbegrepTjeneste(db), new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)),
            new VirksomhetOppslagTjeneste(db), new EksternNavneoppslagTjeneste(new HttpClient(new KasterHandler()), db),
            new MyndighetstildelingTjeneste(db), new GruppeMedlemskapTjeneste(db), new VirksomhetRelasjonregisterTjeneste(db)),
        new VirksomhetOppslagTjeneste(db), new MyndighetstildelingTjeneste(db),
        new VirksomhetRelasjonregisterTjeneste(db), new GruppeMedlemskapTjeneste(db));

    // DB-en er DELT mellom alle tester i DataTestCollection (ICollectionFixture) — et organnavn brukt i
    // navneformen MÅ derfor være unikt på tvers av HELE denne testfilen, ellers blir
    // VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnEllerNavneformAsync's "krever ETT treff"-regel
    // tvetydig (to ulike virksomheter med samme navneform → null, «ingen gjettet fallback»). Samme
    // "unik streng per test"-begrunnelse som NyTerm() i MyndighetstildelingTjenesteTests.
    private static string NyOrgNavn(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    /// <summary>[Ny, issue #285] <see cref="RelasjonsTypeKonfigurasjonEntitet"/> seedes normalt ved
    /// API-oppstart (Program.cs), IKKE av denne fixturen (kun migrasjoner) — samme mønster som
    /// <c>VirksomhetRelasjonregisterTjenesteTests.NyRelasjonsTypeAsync</c>. Egen, unik kode per test:
    /// en fast kode som "underlagt" ville kollidert på tvers av delte DB-tester.</summary>
    private static async Task<string> NyRelasjonsTypeAsync(RegelIdeDbContext db)
    {
        var kode = $"underlagt-{Guid.NewGuid():N}";
        db.RelasjonsTypeKonfigurasjoner.Add(new RelasjonsTypeKonfigurasjonEntitet
        {
            Id = Guid.NewGuid(), Kode = kode, FraVisningsmal = "er underlagt {0}", TilVisningsmal = "er eier/overordnet for {0}", Aktiv = true,
        });
        await db.SaveChangesAsync();
        return kode;
    }

    private static async Task<(Guid RettskildeId, string NodeEid)> OpprettRettskildeMedNodeAsync(RegelIdeDbContext db, string tekst)
    {
        var rettskildeId = Guid.NewGuid();
        var nodeEid = $"https://test/{Guid.NewGuid():N}/§1/ledd-1";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid, KildeId = "ledd-1",
            NodeType = "ledd", Tekst = tekst,
        });
        await db.SaveChangesAsync();
        return (rettskildeId, nodeEid);
    }

    [Fact]
    public async Task Oppretter_navnekandidat_med_oppdagelseskilde_ki_fri_sveip()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettRettskildeMedNodeAsync(
            db, "Energiklagenemnda behandler klager over enkeltvedtak.");
        var svar = $$"""
            [{"Type":"virksomhet","Navn":"Energiklagenemnda","NodeEid":"{{nodeEid}}","Rolle":null,"Relasjon":null,"GruppeAvGruppe":null}]
            """;

        var tjeneste = NyTjeneste(db, svar);
        var resultat = await tjeneste.KjorOppdagelseAsync(rettskildeId, "system-ki");

        Assert.Single(resultat.Kandidater);
        var k = resultat.Kandidater[0];
        Assert.NotNull(k.NavnekandidatId);
        Assert.Null(k.NavnekandidatFeil);
        Assert.Null(k.MyndighetstildelingId);
        Assert.Null(k.VirksomhetRelasjonId);
        Assert.Null(k.GruppeMedlemskapId);

        var kandidat = await db.Navnekandidater.SingleAsync(n => n.Id == k.NavnekandidatId);
        Assert.Equal("ki-fri-sveip", kandidat.OppdagelsesKilde);
        Assert.Equal("Venter", kandidat.Status); // ALDRI godkjent automatisk — et menneske må inn.
        Assert.Equal("Energiklagenemnda", kandidat.ForeslattTekst);
        Assert.Equal(42, resultat.InputTokens);
        Assert.Equal(7, resultat.OutputTokens);
    }

    /// <summary>
    /// [Ny, live-verifisering issue #285 AC1] Ekte funn mot HostYourAI/DeepSeek-V4-Flash (se
    /// <see cref="VirksomhetOgGruppeKiOppdagelseLiveTests"/>) — over TO SEPARATE kjøringer mot NØYAKTIG
    /// samme tekst ekkoet modellen TO ULIKE kortformer av samme posisjons eId: "§1/ledd-1" én gang,
    /// "§1-ledd-1" (bindestrek der den ekte formen har skråstrek, se
    /// <c>LovdataIdentifikatorer.LeddEid</c>) den andre. Uten et suffiks-fall-tilbake som normaliserer
    /// BEGGE skilletegnene ble HVERT ENESTE forslag forkastet i BEGGE de ekte kjøringene.
    /// </summary>
    [Theory]
    [InlineData("/")] // "§1/ledd-1" — den ene observerte, ekte modellformen.
    [InlineData("-")] // "§1-ledd-1" — den ANDRE observerte, ekte modellformen (samme posisjon, annen kjøring).
    public async Task NodeEid_som_kortform_suffiks_lopes_opp_uansett_om_modellen_bruker_skrastrek_eller_bindestrek(string skilletegn)
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettRettskildeMedNodeAsync(
            db, "Energiklagenemnda behandler klager over enkeltvedtak.");
        // nodeEid har formen "https://test/{guid}/§1/ledd-1" (se OpprettRettskildeMedNodeAsync).
        var raaKortform = nodeEid[(nodeEid.LastIndexOf('/', nodeEid.LastIndexOf('/') - 1) + 1)..];
        var kortformEid = raaKortform.Replace('/', skilletegn[0]);
        var svar = $$$"""
            [{"Type":"virksomhet","Navn":"Energiklagenemnda","NodeEid":"{{{kortformEid}}}","Rolle":null,"Relasjon":null,"GruppeAvGruppe":null}]
            """;

        var tjeneste = NyTjeneste(db, svar);
        var resultat = await tjeneste.KjorOppdagelseAsync(rettskildeId, "system-ki");

        var k = Assert.Single(resultat.Kandidater);
        Assert.NotNull(k.NavnekandidatId);
        Assert.Null(k.NavnekandidatFeil);
        Assert.Equal(nodeEid, k.NodeEid); // Utfallet rapporterer den EKTE, lagrede formen — ikke kortformen.

        var kandidat = await db.Navnekandidater.SingleAsync(n => n.Id == k.NavnekandidatId);
        Assert.Equal(nodeEid, kandidat.NodeEid);
    }

    [Fact]
    public async Task Tekst_som_ikke_finnes_i_noden_gir_forkastet_forslag_ingen_kandidat()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettRettskildeMedNodeAsync(db, "Denne teksten nevner ingen organer.");
        var svar = $$"""
            [{"Type":"virksomhet","Navn":"Et Diktet Organ","NodeEid":"{{nodeEid}}","Rolle":null,"Relasjon":null,"GruppeAvGruppe":null}]
            """;

        var tjeneste = NyTjeneste(db, svar);
        var resultat = await tjeneste.KjorOppdagelseAsync(rettskildeId, "system-ki");

        Assert.Single(resultat.Kandidater);
        Assert.Null(resultat.Kandidater[0].NavnekandidatId);
        Assert.NotNull(resultat.Kandidater[0].NavnekandidatFeil);
        // Scopet til DENNE testens rettskilde — DB-en er DELT mellom alle tester i DataTestCollection
        // (samme presedens som NavnekandidatOppdagelseTjenesteTests), en ubetinget ToListAsync() ville
        // også truffet rader fra andre tester i denne filen.
        Assert.Empty(await db.Navnekandidater.Where(n => n.RettskildeId == rettskildeId).ToListAsync());
    }

    [Fact]
    public async Task Tomt_svar_gir_tom_kandidatliste_og_melding()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, _) = await OpprettRettskildeMedNodeAsync(db, "Ingen organer nevnt her.");

        var tjeneste = NyTjeneste(db, "[]");
        var resultat = await tjeneste.KjorOppdagelseAsync(rettskildeId, "system-ki");

        Assert.Empty(resultat.Kandidater);
        Assert.NotNull(resultat.Melding);
    }

    [Fact]
    public async Task Rolle_med_kjent_virksomhet_og_rollebegrep_gir_myndighetstildeling_foreslatt_av_ai()
    {
        await using var db = _fixture.NyDbContext();
        var navn = NyOrgNavn("Energiklagenemnda");
        var (rettskildeId, nodeEid) = await OpprettRettskildeMedNodeAsync(
            db, $"{navn} er klageinstans for enkeltvedtak etter denne loven.");

        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"register-{navn}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        // Navneformen MÅ matche EKSAKT teksten agenten siterer — se
        // VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnEllerNavneformAsync.
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(virksomhet.Id, navn, "Kari Jurist");

        var rolleTerm = NyOrgNavn("klageinstans");
        var rolleLov = await OpprettRettskildeMedNodeAsync(db, "Klageinstans er den myndighet som er tillagt dette.");
        var rollebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(Nodetyper.Klasse, rolleLov.RettskildeId, rolleTerm, "Kari Jurist");

        var svar = $$$"""
            [{"Type":"virksomhet","Navn":"{{{navn}}}","NodeEid":"{{{nodeEid}}}",
              "Rolle":{"RolleNavn":"{{{rolleTerm}}}","ParagrafEid":null},"Relasjon":null,"GruppeAvGruppe":null}]
            """;

        var tjeneste = NyTjeneste(db, svar);
        var resultat = await tjeneste.KjorOppdagelseAsync(rettskildeId, "system-ki");

        var k = Assert.Single(resultat.Kandidater);
        Assert.NotNull(k.NavnekandidatId);
        Assert.NotNull(k.MyndighetstildelingId);
        Assert.Null(k.RolleIkkeOpprettetGrunn);

        var tildeling = await db.Myndighetstildelinger.SingleAsync(m => m.Id == k.MyndighetstildelingId);
        Assert.Equal("foreslatt_av_ai", tildeling.Status);
        Assert.Equal(rollebegrep.Id, tildeling.GruppeBegrepId);
        Assert.Equal(virksomhet.Id, tildeling.VirksomhetId);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetType == "myndighetstildeling" && p.EntitetId == tildeling.Id);
        Assert.Equal("foreslatt_av_ai", proveniens.Handling);
        Assert.NotNull(proveniens.AiForslagVersjon);

        // Kandidatens EGEN navnekandidat-status er UENDRET "Venter" — rolletildelingen er et separat,
        // ubekreftet forslag, den lukker IKKE navnekandidat-kjeden slik et menneskes veiviser-valg gjør.
        var kandidat = await db.Navnekandidater.SingleAsync(n => n.Id == k.NavnekandidatId);
        Assert.Equal("Venter", kandidat.Status);
    }

    [Fact]
    public async Task Rolle_med_ukjent_virksomhet_gir_ingen_myndighetstildeling_men_navnekandidaten_opprettes_fortsatt()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettRettskildeMedNodeAsync(
            db, "Et Ukjent Organ er klageinstans for enkeltvedtak etter denne loven.");

        var svar = $$"""
            [{"Type":"virksomhet","Navn":"Et Ukjent Organ","NodeEid":"{{nodeEid}}",
              "Rolle":{"RolleNavn":"klageinstans","ParagrafEid":null},"Relasjon":null,"GruppeAvGruppe":null}]
            """;

        var tjeneste = NyTjeneste(db, svar);
        var resultat = await tjeneste.KjorOppdagelseAsync(rettskildeId, "system-ki");

        var k = Assert.Single(resultat.Kandidater);
        Assert.NotNull(k.NavnekandidatId); // navneform-forslaget lever fortsatt, uavhengig
        Assert.Null(k.MyndighetstildelingId);
        Assert.NotNull(k.RolleIkkeOpprettetGrunn);
        Assert.Empty(await db.Myndighetstildelinger.Where(m => m.HjemmelRettskildeId == rettskildeId).ToListAsync());
    }

    [Fact]
    public async Task Relasjon_med_to_kjente_virksomheter_gir_virksomhetrelasjon_foreslatt_av_ai_hjemlet_her()
    {
        await using var db = _fixture.NyDbContext();
        var fraNavn = NyOrgNavn("Energiklagenemnda");
        var tilNavn = NyOrgNavn("Energidepartementet");
        var (rettskildeId, nodeEid) = await OpprettRettskildeMedNodeAsync(
            db, $"{fraNavn} er underlagt {tilNavn} i denne sammenheng.");

        var fra = new Virksomhet { Id = Guid.NewGuid(), Navn = $"register-{fraNavn}" };
        var til = new Virksomhet { Id = Guid.NewGuid(), Navn = $"register-{tilNavn}" };
        db.Virksomheter.AddRange(fra, til);
        await db.SaveChangesAsync();
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(fra.Id, fraNavn, "Kari Jurist");
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(til.Id, tilNavn, "Kari Jurist");
        var relasjonsType = await NyRelasjonsTypeAsync(db);

        var svar = $$$"""
            [{"Type":"virksomhet","Navn":"{{{fraNavn}}}","NodeEid":"{{{nodeEid}}}","Rolle":null,
              "Relasjon":{"Type":"{{{relasjonsType}}}","MotpartNavn":"{{{tilNavn}}}","HjemletHer":true},"GruppeAvGruppe":null}]
            """;

        var tjeneste = NyTjeneste(db, svar);
        var resultat = await tjeneste.KjorOppdagelseAsync(rettskildeId, "system-ki");

        var k = Assert.Single(resultat.Kandidater);
        Assert.NotNull(k.VirksomhetRelasjonId);
        var relasjon = await db.VirksomhetRelasjoner.SingleAsync(r => r.Id == k.VirksomhetRelasjonId);
        Assert.Equal("foreslatt_av_ai", relasjon.Status);
        Assert.Equal(fra.Id, relasjon.FraVirksomhetId);
        Assert.Equal(til.Id, relasjon.TilVirksomhetId);
        Assert.Equal(relasjonsType, relasjon.RelasjonsType);
        Assert.Equal(rettskildeId, relasjon.HjemmelRettskildeId);
        Assert.Equal(nodeEid, relasjon.HjemmelEid);
        Assert.Null(relasjon.Kommentar);
    }

    [Fact]
    public async Task GruppeAvGruppe_krever_at_underordnet_gruppebegrep_allerede_finnes()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettRettskildeMedNodeAsync(
            db, "Vertskommuner inngår i ordningen for særskilt tilsyn.");

        var svar = $$$"""
            [{"Type":"gruppe","Navn":"vertskommuner","NodeEid":"{{{nodeEid}}}","Rolle":null,"Relasjon":null,
              "GruppeAvGruppe":{"OverordnetGruppeNavn":"ordningen for særskilt tilsyn"}}]
            """;

        var tjeneste = NyTjeneste(db, svar);
        var resultat = await tjeneste.KjorOppdagelseAsync(rettskildeId, "system-ki");

        var k = Assert.Single(resultat.Kandidater);
        Assert.NotNull(k.NavnekandidatId); // gruppe-navnekandidaten opprettes uansett
        Assert.Null(k.GruppeMedlemskapId);
        Assert.NotNull(k.GruppeAvGruppeIkkeOpprettetGrunn);
    }

    [Fact]
    public async Task GruppeAvGruppe_med_begge_gruppebegrep_allerede_godkjent_gir_gruppemedlemskap_foreslatt_av_ai()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettRettskildeMedNodeAsync(
            db, "Vertskommuner inngår i ordningen for særskilt tilsyn.");

        var underordnet = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(Nodetyper.Klasse, rettskildeId, "vertskommuner", "Kari Jurist");
        var overordnet = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(Nodetyper.Klasse, rettskildeId, "ordningen for særskilt tilsyn", "Kari Jurist");

        var svar = $$$"""
            [{"Type":"gruppe","Navn":"vertskommuner","NodeEid":"{{{nodeEid}}}","Rolle":null,"Relasjon":null,
              "GruppeAvGruppe":{"OverordnetGruppeNavn":"ordningen for særskilt tilsyn"}}]
            """;

        var tjeneste = NyTjeneste(db, svar);
        var resultat = await tjeneste.KjorOppdagelseAsync(rettskildeId, "system-ki");

        var k = Assert.Single(resultat.Kandidater);
        Assert.NotNull(k.GruppeMedlemskapId);
        var medlemskap = await db.GruppeMedlemskap.SingleAsync(m => m.Id == k.GruppeMedlemskapId);
        Assert.Equal("foreslatt_av_ai", medlemskap.Status);
        Assert.Equal(overordnet.Id, medlemskap.OverordnetGruppeBegrepId);
        Assert.Equal(underordnet.Id, medlemskap.UnderordnetGruppeBegrepId);
    }

    [Fact]
    public async Task Kjort_to_ganger_gir_ikke_duplikate_rader_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        var fraNavn = NyOrgNavn("Energiklagenemnda");
        var tilNavn = NyOrgNavn("Energidepartementet");
        var (rettskildeId, nodeEid) = await OpprettRettskildeMedNodeAsync(
            db, $"{fraNavn} er underlagt {tilNavn} i denne sammenheng.");
        var fra = new Virksomhet { Id = Guid.NewGuid(), Navn = $"register-{fraNavn}" };
        var til = new Virksomhet { Id = Guid.NewGuid(), Navn = $"register-{tilNavn}" };
        db.Virksomheter.AddRange(fra, til);
        await db.SaveChangesAsync();
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(fra.Id, fraNavn, "Kari Jurist");
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(til.Id, tilNavn, "Kari Jurist");
        var relasjonsType = await NyRelasjonsTypeAsync(db);

        var svar = $$$"""
            [{"Type":"virksomhet","Navn":"{{{fraNavn}}}","NodeEid":"{{{nodeEid}}}","Rolle":null,
              "Relasjon":{"Type":"{{{relasjonsType}}}","MotpartNavn":"{{{tilNavn}}}","HjemletHer":true},"GruppeAvGruppe":null}]
            """;

        await NyTjeneste(db, svar).KjorOppdagelseAsync(rettskildeId, "system-ki");
        await NyTjeneste(db, svar).KjorOppdagelseAsync(rettskildeId, "system-ki");

        Assert.Single(await db.Navnekandidater.Where(n => n.RettskildeId == rettskildeId).ToListAsync());
        Assert.Single(await db.VirksomhetRelasjoner.Where(r => r.FraVirksomhetId == fra.Id).ToListAsync());
    }

    [Fact]
    public async Task Ukjent_rettskilde_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = NyTjeneste(db, "[]");
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.KjorOppdagelseAsync(Guid.NewGuid(), "system-ki"));
    }
}
