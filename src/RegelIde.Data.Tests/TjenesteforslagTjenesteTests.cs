using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>«Identifiser tjenester» (byggesteg 5 runde 1, docs/06-veikart.md), mot ekte embedded Postgres og den ekte <see cref="KiAgentKlientStub"/>.</summary>
[Collection(DataTestCollection.Navn)]
public class TjenesteforslagTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TjenesteforslagTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Kjorer_forslag_oppretter_tjeneste_med_status_foreslatt_av_ai_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        await new KunnskapsbibliotekTjeneste(db).LeggTilLenkeAsync(
            virksomhet, "https://testkommunen.no/tjenester", "Om tjenestetilbudet", "Kari Jurist");

        var forslagstjeneste = new TjenesteforslagTjeneste(
            db, new KiAgentKlientStub(), new TjenesteregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db), new ConfigurationBuilder().Build(),
            new RettskildeEmbeddingTjeneste(db, new EmbeddingKlientStub(), new ConfigurationBuilder().Build()), new EmbeddingKlientStub(), new HandlingregisterTjeneste(db));
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Single(resultat.Opprettede);
        Assert.Equal("foreslatt_av_ai", resultat.Opprettede[0].Status);
        Assert.Null(resultat.Melding);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == resultat.Opprettede[0].Id);
        Assert.Equal("foreslatt_av_ai", proveniens.Handling);
        Assert.NotNull(proveniens.AiForslagVersjon);
        Assert.Contains(rettskildeId.ToString(), proveniens.KildeReferanserJson);
    }

    [Fact]
    public async Task Fungerer_uten_registrerte_kunnskapsbibliotek_lenker()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var forslagstjeneste = new TjenesteforslagTjeneste(
            db, new KiAgentKlientStub(), new TjenesteregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db), new ConfigurationBuilder().Build(),
            new RettskildeEmbeddingTjeneste(db, new EmbeddingKlientStub(), new ConfigurationBuilder().Build()), new EmbeddingKlientStub(), new HandlingregisterTjeneste(db));
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Single(resultat.Opprettede);
    }

    [Fact]
    public async Task Ukjent_rettskilde_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var forslagstjeneste = new TjenesteforslagTjeneste(
            db, new KiAgentKlientStub(), new TjenesteregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db), new ConfigurationBuilder().Build(),
            new RettskildeEmbeddingTjeneste(db, new EmbeddingKlientStub(), new ConfigurationBuilder().Build()), new EmbeddingKlientStub(), new HandlingregisterTjeneste(db));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(virksomhet, [Guid.NewGuid()], "system-ki"));
    }

    /// <summary>Fanger opp kontekst-strengen agenten faktisk fikk, og returnerer et fast, fullt utfylt svar (byggesteg 5 runde 3).</summary>
    private sealed class FangendeKlient : IKiAgentKlient
    {
        public string? SisteKontekst { get; private set; }

        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default)
        {
            SisteKontekst = kontekst;
            return Task.FromResult(new KiSvar("""
                [{"Tittel": "Testtjeneste", "KortBeskrivelse": "d", "KompetentMyndighet": "Testkommunen",
                  "Output": "Et vedtak", "Tjenestetype": "Bevilling", "Malgruppe": "Virksomheter",
                  "Kanaler": ["digitalt", "fysisk"], "Kostnad": "Gratis", "Behandlingstid": "4 uker",
                  "Kontaktpunkt": "postmottak@testkommunen.no", "KonsekvensVedBrudd": "Inndragning",
                  "Sprak": ["norsk", "engelsk"]}]
                """, InputTokens: 321, OutputTokens: 65));
        }
    }

    [Fact]
    public async Task Kontekst_inkluderer_eid_per_node_og_alle_cpsv_felt_lagres()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var klient = new FangendeKlient();
        var forslagstjeneste = new TjenesteforslagTjeneste(
            db, klient, new TjenesteregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db), new ConfigurationBuilder().Build(),
            new RettskildeEmbeddingTjeneste(db, new EmbeddingKlientStub(), new ConfigurationBuilder().Build()), new EmbeddingKlientStub(), new HandlingregisterTjeneste(db));
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Contains("[", klient.SisteKontekst); // eId-tag foran nodetekst, se RettskildeKontekstHjelper

        Assert.Single(resultat.Opprettede);
        Assert.Equal(321, resultat.InputTokens);
        Assert.Equal(65, resultat.OutputTokens);
        var tjeneste = resultat.Opprettede[0];
        Assert.Equal("Testkommunen", tjeneste.KompetentMyndighet);
        Assert.Equal("Et vedtak", tjeneste.Output);
        Assert.Equal("Bevilling", tjeneste.Tjenestetype);
        Assert.Equal(["Virksomheter"], tjeneste.Malgruppe);
        Assert.Equal(["digitalt", "fysisk"], tjeneste.Kanaler);
        Assert.Equal("Gratis", tjeneste.Kostnad);
        Assert.Equal("4 uker", tjeneste.Behandlingstid);
        Assert.Equal("postmottak@testkommunen.no", tjeneste.Kontaktpunkt);
        Assert.Equal("Inndragning", tjeneste.KonsekvensVedBrudd);
        Assert.Equal(["norsk", "engelsk"], tjeneste.Sprak);
    }

    /// <summary>To forslag i samme batch (byggesteg 5 runde 4): "Tjeneste A" refererer til en
    /// EKSISTERENDE tjeneste (E1) og har én gyldig + én hallusinert regelverksreferanse-eId; "Tjeneste
    /// B" refererer til "Tjeneste A" via T1 (har_del) og til E99, som ikke finnes (droppes stille) —
    /// samme "hallusinert referanse dropper kun seg selv"-mønster som <c>DelvisUgyldigEidKlient</c> i
    /// BegrepsforslagTjenesteTests.</summary>
    private sealed class RelasjonsKlient(string gyldigEid) : IKiAgentKlient
    {
        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default) =>
            Task.FromResult(new KiSvar((
                """
                [{"Tittel": "Tjeneste A", "RegelverksreferanserEid": ["GYLDIG_EID", "§ikke-en-faktisk-node"],
                  "RelatertTil": [{"Referanse": "E1", "Rel": "avhengig_av"}]},
                 {"Tittel": "Tjeneste B", "RelatertTil": [{"Referanse": "T1", "Rel": "har_del"}, {"Referanse": "E99", "Rel": "avhengig_av"}]}]
                """).Replace("GYLDIG_EID", gyldigEid), InputTokens: null, OutputTokens: null));
    }

    [Fact]
    public async Task Relaterte_tjenester_og_regelverksreferanser_kobles_uopploselige_droppes_stille()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        var gyldigEid = (await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == rettskildeId)).Eid;

        var tjenesteregister = new TjenesteregisterTjeneste(db);
        var eksisterende = await tjenesteregister.OpprettAsync(
            virksomhet, "Eksisterende tjeneste", null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist");

        var tjenesteavhengighetregister = new TjenesteavhengighetregisterTjeneste(db);
        var forslagstjeneste = new TjenesteforslagTjeneste(
            db, new RelasjonsKlient(gyldigEid), tjenesteregister, tjenesteavhengighetregister, new ConfigurationBuilder().Build(),
            new RettskildeEmbeddingTjeneste(db, new EmbeddingKlientStub(), new ConfigurationBuilder().Build()), new EmbeddingKlientStub(), new HandlingregisterTjeneste(db));
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Equal(2, resultat.Opprettede.Count);
        var tjenesteA = resultat.Opprettede.Single(t => t.Tittel == "Tjeneste A");
        var tjenesteB = resultat.Opprettede.Single(t => t.Tittel == "Tjeneste B");

        var referanserA = await tjenesteregister.RegelverksreferanserForAsync(tjenesteA.Id);
        Assert.Equal(gyldigEid, Assert.Single(referanserA).TilEid);

        // Tjeneste A er involvert i to kanter: sin egen (avhengig_av → Eksisterende) OG "til"-siden av
        // Tjeneste B sin (har_del) — HentForTjenesteAsync ser begge retninger.
        var avhengigheterA = await tjenesteavhengighetregister.HentForTjenesteAsync(tjenesteA.Id);
        Assert.Equal(2, avhengigheterA.Count);
        var avhengigAvEksisterende = Assert.Single(avhengigheterA, v => v.Rel == "avhengig_av");
        Assert.Equal("fra", avhengigAvEksisterende.Retning);
        Assert.Equal(eksisterende.Id, avhengigAvEksisterende.MotpartTjenesteId);
        var harDelFraB = Assert.Single(avhengigheterA, v => v.Rel == "har_del");
        Assert.Equal("til", harDelFraB.Retning);
        Assert.Equal(tjenesteB.Id, harDelFraB.MotpartTjenesteId);

        // E99 finnes ikke (kun E1) — droppes stille, Tjeneste B skal kun ha koblingen til Tjeneste A.
        var avhengigheterB = await tjenesteavhengighetregister.HentForTjenesteAsync(tjenesteB.Id);
        var avhengighetB = Assert.Single(avhengigheterB);
        Assert.Equal("har_del", avhengighetB.Rel);
        Assert.Equal("fra", avhengighetB.Retning);
        Assert.Equal(tjenesteA.Id, avhengighetB.MotpartTjenesteId);
    }

    /// <summary>Returnerer en fast vektor for eksakt tekst-match (satt av testen), og en "standard"-
    /// vektor for alt annet — gir full kontroll over hvilke noder som rangeres høyest i
    /// RagKontekstHjelper uten å måtte late som en ekte embeddings-modell (byggesteg 5 runde 4).</summary>
    private sealed class OppslagsEmbeddingKlient(Dictionary<string, double[]> oppslag, double[] standard) : IEmbeddingKlient
    {
        public Task<IReadOnlyList<double[]>> EmbedAsync(IReadOnlyList<string> tekster, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<double[]>>(tekster.Select(t => oppslag.TryGetValue(t, out var v) ? v : standard).ToList());
    }

    /// <summary>
    /// Seeder direkte (ikke via LovdataKonverterer/Testdata) med GARANTERT unik tekst per node —
    /// i motsetning til de andre testene i denne filen, som deler samme, deterministisk gjenbrukte
    /// "alkoholloven"-fixture (RettskildeImportTjeneste gjenbruker eksisterende rettskilde/noder ved
    /// uendret reimport). RAG-testen under bryr seg om NØYAKTIG hvilke noder som embeddes/rangeres,
    /// og ville false positive/negative på embeddings delt med andre tester av samme innhold.
    /// </summary>
    private static async Task<(Guid RettskildeId, List<RettskildeNodeEntitet> Noder)> NyRettskildeMedNoderAsync(RegelIdeDbContext db, int antall)
    {
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            // Importrolle "referanse" — slipper unna ck_rettskilder_akn_xml-sjekken (krever ellers
            // ekte AknXml-innhold, som denne testen ikke bryr seg om).
            Id = rettskildeId, Doctype = "act", Kildetype = "Lov", Importrolle = "referanse",
            Tittel = $"Testlov {rettskildeId}", Status = "Gjeldende", OpprettetAv = "system-test",
        });
        var noder = new List<RettskildeNodeEntitet>();
        for (var i = 0; i < antall; i++)
        {
            var node = new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = $"test/{Guid.NewGuid()}",
                KildeId = $"k-{Guid.NewGuid()}", NodeType = "ledd", Tekst = $"Unik tekst {Guid.NewGuid()}",
                Sorteringsrekkefolge = i,
            };
            noder.Add(node);
            db.RettskildeNoder.Add(node);
        }
        await db.SaveChangesAsync();
        return (rettskildeId, noder);
    }

    [Fact]
    public async Task KjorForslagMedRagAsync_uten_kunnskapsbibliotek_kaster_ingen_retrieval_anker()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var embeddingKlient = new OppslagsEmbeddingKlient([], standard: [0]);
        var forslagstjeneste = new TjenesteforslagTjeneste(
            db, new KiAgentKlientStub(), new TjenesteregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db),
            new ConfigurationBuilder().Build(), new RettskildeEmbeddingTjeneste(db, embeddingKlient, new ConfigurationBuilder().Build()), embeddingKlient, new HandlingregisterTjeneste(db));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagMedRagAsync(virksomhet, [rettskildeId], antallNoder: 3, "system-ki"));
        Assert.Contains("retrieval-anker", ex.Message);
    }

    [Fact]
    public async Task KjorForslagMedRagAsync_bruker_kun_de_k_mest_like_nodene_som_kontekst()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var (rettskildeId, alleNoder) = await NyRettskildeMedNoderAsync(db, antall: 3);
        await new KunnskapsbibliotekTjeneste(db).LeggTilLenkeAsync(
            virksomhet, "https://testkommunen.no/tjenester", "Om tjenestetilbudet", "Kari Jurist");

        var topNode = alleNoder[0];
        var andreNode = alleNoder[1];

        // ByggSporsmalTekstAsync produserer "{Url} — {Beskrivelse}" + AppendLine sin linjeskift, siden
        // det kun er registrert én lenke og ingen filer.
        var sporsmalTekst = $"https://testkommunen.no/tjenester — Om tjenestetilbudet{Environment.NewLine}";
        var oppslag = new Dictionary<string, double[]>
        {
            [sporsmalTekst] = [1, 0],
            [topNode.Tekst!] = [1, 0], // identisk med spørsmålet — kosinuslikhet 1
        };
        var embeddingKlient = new OppslagsEmbeddingKlient(oppslag, standard: [0, 1]); // alt annet er ortogonalt — likhet 0
        var rettskildeEmbeddingTjeneste = new RettskildeEmbeddingTjeneste(db, embeddingKlient, new ConfigurationBuilder().Build());

        var klient = new FangendeKlient();
        var forslagstjeneste = new TjenesteforslagTjeneste(
            db, klient, new TjenesteregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db),
            new ConfigurationBuilder().Build(), rettskildeEmbeddingTjeneste, embeddingKlient, new HandlingregisterTjeneste(db));

        var resultat = await forslagstjeneste.KjorForslagMedRagAsync(virksomhet, [rettskildeId], antallNoder: 1, "system-ki");

        Assert.Contains($"[{topNode.Eid}]", klient.SisteKontekst);
        Assert.DoesNotContain($"[{andreNode.Eid}]", klient.SisteKontekst);
        Assert.Single(resultat.Opprettede);

        // Sikring skal ha embeddet ALLE noder med tekst i rettskilden (lazy, men for hele kilden på én gang).
        var nodeIderMedTekst = alleNoder.Select(n => n.Id).ToList();
        var embeddinger = await db.RettskildeNodeEmbeddinger.Where(e => nodeIderMedTekst.Contains(e.NodeId)).ToListAsync();
        Assert.Equal(alleNoder.Count, embeddinger.Count);
    }

    // ---------- Omfang "full" (handlingsforslag-ki-omfang-runden) — Tjeneste + Handlinger i ett kall ----------

    /// <summary>Fast svar med ÉN tjeneste og to handlinger under den — beviser at KjorFullForslagAsync
    /// oppretter Tjenesten FØRST (for å få en id) og deretter Handlingene UNDER den.</summary>
    private sealed class FullForslagKlient : IKiAgentKlient
    {
        public string? SisteSystemInstruks { get; private set; }

        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default)
        {
            SisteSystemInstruks = systemInstruks;
            return Task.FromResult(new KiSvar("""
                [{"Tjeneste": {"Tittel": "Skjenkebevilling (full)", "KortBeskrivelse": "d", "KompetentMyndighet": "Testkommunen"},
                  "Handlinger": [
                    {"Navn": "Søke om skjenkebevilling", "Handlingstype": "soke", "UtfortAv": "soker"},
                    {"Navn": "Klage på avslag", "Handlingstype": "klage", "UtfortAv": "soker"}
                  ]}]
                """, InputTokens: 999, OutputTokens: 321));
        }
    }

    [Fact]
    public async Task KjorFullForslagAsync_oppretter_tjeneste_forst_og_handlinger_under_den()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var klient = new FullForslagKlient();
        var forslagstjeneste = new TjenesteforslagTjeneste(
            db, klient, new TjenesteregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db), new ConfigurationBuilder().Build(),
            new RettskildeEmbeddingTjeneste(db, new EmbeddingKlientStub(), new ConfigurationBuilder().Build()), new EmbeddingKlientStub(),
            new HandlingregisterTjeneste(db));

        var resultat = await forslagstjeneste.KjorFullForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Single(resultat.Opprettede);
        var element = resultat.Opprettede[0];
        Assert.Equal("Skjenkebevilling (full)", element.Tjeneste.Tittel);
        Assert.Equal("foreslatt_av_ai", element.Tjeneste.Status);
        Assert.Equal(2, element.Handlinger.Count);
        Assert.All(element.Handlinger, h =>
        {
            Assert.Equal(element.Tjeneste.Id, h.TjenesteId);
            Assert.Equal("foreslatt_av_ai", h.Status);
        });
        Assert.Contains(element.Handlinger, h => h.Navn == "Søke om skjenkebevilling" && h.Handlingstype == "soke");
        Assert.Contains(element.Handlinger, h => h.Navn == "Klage på avslag" && h.Handlingstype == "klage");
        Assert.Equal(999, resultat.InputTokens);
        Assert.Equal(321, resultat.OutputTokens);

        // Handlingenes proveniens skal også være markert som KI-forslag, med samme AiForslagVersjon
        // som tjenestens egen (samme kjøring, samme leverandør/modell).
        var tjenesteProveniens = await db.Proveniens.SingleAsync(p => p.EntitetType == "tjeneste" && p.EntitetId == element.Tjeneste.Id);
        foreach (var handling in element.Handlinger)
        {
            var handlingProveniens = await db.Proveniens.SingleAsync(p => p.EntitetType == "handling" && p.EntitetId == handling.Id);
            Assert.Equal("foreslatt_av_ai", handlingProveniens.Handling);
            Assert.Equal(tjenesteProveniens.AiForslagVersjon, handlingProveniens.AiForslagVersjon);
        }

        Assert.Contains("Handlingstype", klient.SisteSystemInstruks);
        Assert.Contains("Tittel", klient.SisteSystemInstruks);
    }

    [Fact]
    public async Task KjorFullForslagAsync_tomt_svar_gir_forklarende_melding()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var forslagstjeneste = new TjenesteforslagTjeneste(
            db, new TjenesteforslagKjorerTomtSvarKlient(), new TjenesteregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db),
            new ConfigurationBuilder().Build(), new RettskildeEmbeddingTjeneste(db, new EmbeddingKlientStub(), new ConfigurationBuilder().Build()),
            new EmbeddingKlientStub(), new HandlingregisterTjeneste(db));

        var resultat = await forslagstjeneste.KjorFullForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Empty(resultat.Opprettede);
        Assert.NotNull(resultat.Melding);
    }

    private sealed class TjenesteforslagKjorerTomtSvarKlient : IKiAgentKlient
    {
        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default) =>
            Task.FromResult(new KiSvar("[]", InputTokens: 10, OutputTokens: 1));
    }
}
