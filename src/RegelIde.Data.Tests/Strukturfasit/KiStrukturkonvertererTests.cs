using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #308 strukturmodell-KI, 2026-10-07] Enhetstester for <see cref="KiStrukturkonverterer"/> uten
/// nettverk: en stub-klient gir kanned svar, og testene verifiserer den harde valideringen (gyldig rad,
/// falskt sitat, ukjent eId, ukjent kategori/type, ødelagt JSON, aktørreferanser), oppdelingen i kall
/// og at taggene slås opp til ekte eId-er. Live-målingen mot leverandøren ligger i
/// <see cref="KiStrukturkonvertererLiveMalingTests"/>.
/// </summary>
public class KiStrukturkonvertererTests
{
    private const string Eli = "https://lovdata.no/eli/lov/2000/01/01/1/nor";

    /// <summary>Svarer med <paramref name="svar"/>(kontekst) og husker hver kontekst den fikk.</summary>
    private sealed class KannetKlient(Func<string, string> svar, int? inn = 100, int? ut = 10) : IKiAgentKlient
    {
        public ConcurrentQueue<string> Kontekster { get; } = new();

        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default)
        {
            Kontekster.Enqueue(kontekst);
            return Task.FromResult(new KiSvar(svar(kontekst), inn, ut));
        }
    }

    private static IConfiguration Konfig(string? modell = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(modell is null
            ? []
            : new Dictionary<string, string?> { ["RegelIde:KiAgent:Leverandor"] = "OpenAiKompatibel", ["RegelIde:KiAgent:Modell"] = modell })
        .Build();

    private static Strukturkonverteringsgrunnlag Grunnlag(params string[] tekster) => new(
        "Testloven", Eli,
        tekster.Select((t, i) => new Strukturnode("Testloven", Eli, $"{Eli}/§{i + 1}/ledd-1", "ledd", null, t)).ToList());

    private static KiStrukturkonverteringsresultat Kjor(string svar, params string[] tekster) =>
        new KiStrukturkonverterer(new KannetKlient(_ => svar), Konfig("test-modell"))
            .KonverterMedRapportAsync(Grunnlag(tekster)).GetAwaiter().GetResult();

    private const string Tekst = "Departementet kan gi forskrift om leveringsplikten. Klage går til klagenemnda.";

    private const string AktorDepartementet =
        """{"id": "a1", "tekstform": "Departementet", "varianter": ["departementet"], "entitetstype": "organ", "navngitt": false, "oppløsning": "lovens_departement"}""";

    private static string Svar(string utsagn, string? aktorer = null) =>
        $$"""{"aktorer": [{{aktorer ?? AktorDepartementet}}], "utsagn": [{{utsagn}}]}""";

    [Fact]
    public void Gyldig_rad_slipper_gjennom_med_ekte_eid_aktor_og_proveniens()
    {
        var r = Kjor(Svar("""{"eid": "n1", "sitat": "Departementet kan gi forskrift om leveringsplikten", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a1", "objekt": "forskrift om leveringsplikten", "polaritet": "positiv", "sikkerhet": "hoy"}"""), Tekst);

        var u = Assert.Single(r.Dokument.Utsagn);
        Assert.Equal($"{Eli}/§1/ledd-1", u.Eid);
        Assert.Equal(("kompetanse", "forskriftskompetanse"), (u.Kategori, u.Type));
        Assert.Equal("ki:test-modell", u.Oppdagelseskilde);
        Assert.Equal("forskrift om leveringsplikten", u.Objekt);
        Assert.Null(u.Til);
        var a = Assert.Single(r.Dokument.Aktorer);
        Assert.Equal(u.Fra, a.Id);
        Assert.Equal("Departementet", a.Tekstform);
        Assert.Equal(["departementet"], a.Varianter);
        Assert.Equal("organ", a.Entitetstype);
        Assert.Equal("lovens_departement", a.Opplosning);
        Assert.Empty(r.Kastet);
        Assert.Equal((100, 10), (r.InputTokens, r.OutputTokens));
        Assert.Equal("test-modell", r.Modell);
    }

    [Fact]
    public void Falskt_sitat_kastes_og_telles_ogsa_naar_det_nesten_er_ordrett()
    {
        var r = Kjor(Svar("""
            {"eid": "n1", "sitat": "Departementet kan gi forskrifter om leveringsplikten", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a1", "polaritet": "positiv"},
            {"eid": "n1", "sitat": "departementet  kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a1", "polaritet": "positiv"},
            {"eid": "n1", "sitat": "Klagenemnda avgjør klager", "kategori": "kompetanse", "type": "klagekompetanse", "polaritet": "positiv"}
            """), Tekst, "Klagenemnda avgjør klager etter loven.");

        Assert.Empty(r.Dokument.Utsagn);
        Assert.All(r.Kastet, k => Assert.Equal(KastetArsak.FalsktSitat, k.Arsak));
        Assert.Equal(
            [SitatDiagnoser.IkkeITeksten, SitatDiagnoser.NestenOrdrett, SitatDiagnoser.AnnenNode],
            r.Kastet.Select(k => k.Detalj));
    }

    [Fact]
    public void Ukjent_eid_kastes_ogsaa_naar_det_er_den_lange_eid_en_eller_en_tagg_utenfor_kallet()
    {
        var r = Kjor(Svar($$"""
            {"eid": "n7", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "polaritet": "positiv"},
            {"eid": "{{Eli}}/§1/ledd-1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "polaritet": "positiv"},
            {"eid": "[n1]", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "polaritet": "positiv"}
            """), Tekst);

        Assert.Empty(r.Dokument.Utsagn);
        Assert.Equal(3, r.Kastet.Count(k => k.Arsak == KastetArsak.UkjentEid));
    }

    [Fact]
    public void Ukjent_kategori_og_type_kastes_men_annet_x_godtas()
    {
        var r = Kjor(Svar("""
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "myndighet", "type": "forskriftskompetanse", "polaritet": "positiv"},
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftsmyndighet", "polaritet": "positiv"},
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "bestar_av", "polaritet": "positiv"},
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "annet:Forskrift Myndighet", "polaritet": "positiv"},
            {"eid": "n1", "sitat": "Klage går til klagenemnda", "kategori": "kompetanse", "type": "annet:klageordning", "polaritet": "positiv", "kommentar": "uten førsteinstans"},
            {"eid": "n1", "sitat": "Klage går til klagenemnda", "kategori": "annet:klage", "type": "annet:klageordning", "polaritet": "positiv"}
            """), Tekst);

        Assert.Equal([KastetArsak.UkjentKategori, KastetArsak.UkjentType, KastetArsak.UkjentType, KastetArsak.UkjentType],
            r.Kastet.Select(k => k.Arsak));
        Assert.Equal(["annet:klageordning", "annet:klageordning"], r.Dokument.Utsagn.Select(u => u.Type));
    }

    [Fact]
    public void Odelagt_json_teller_kallet_som_ugyldig_og_gir_ingen_rader()
    {
        var r = Kjor("""{"aktorer": [], "utsagn": [{"eid": "n1", "sitat": "Departementet""", Tekst);

        Assert.Empty(r.Dokument.Utsagn);
        Assert.Equal(1, r.UgyldigJson);
        Assert.Equal(1, r.TaptNoder);
        Assert.Single(r.Kallfeil);
    }

    [Theory]
    [InlineData("""[{"eid": "n1"}]""")]
    [InlineData("""{"aktorer": []}""")]
    [InlineData("Her er utsagnene: {}")]
    public void Svar_som_ikke_er_objekt_med_utsagnliste_er_ugyldig_json(string svar)
    {
        Assert.Equal(1, Kjor(svar, Tekst).UgyldigJson);
    }

    [Fact]
    public void Markdown_kodeblokk_rundt_svaret_strimles_som_i_de_andre_ki_agentene()
    {
        var r = Kjor("```json\n" + Svar("""{"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a1", "polaritet": "positiv"}""") + "\n```", Tekst);

        Assert.Single(r.Dokument.Utsagn);
        Assert.Equal(0, r.UgyldigJson);
    }

    [Fact]
    public void Ugyldig_json_for_flere_noder_halverer_kallet_og_proever_igjen()
    {
        var klient = new KannetKlient(k => Regex.Matches(k, @"^\[n\d+\]", RegexOptions.Multiline).Count > 1
            ? "{\"utsagn\": [ AVKUTTET"
            : k.Contains("[n1]")
                ? Svar("""{"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a1", "polaritet": "positiv"}""")
                : """{"aktorer": [], "utsagn": []}""");

        var r = new KiStrukturkonverterer(klient, Konfig("m"))
            .KonverterMedRapportAsync(Grunnlag(Tekst, "Annen tekst.")).GetAwaiter().GetResult();

        Assert.Single(r.Dokument.Utsagn);
        Assert.Equal(3, r.AntallKall);
        Assert.Equal(1, r.UgyldigJson);
        Assert.Equal(0, r.TaptNoder);
    }

    [Fact]
    public void Aktorreferanser_maa_finnes_og_aktoren_maa_staa_i_teksten()
    {
        var r = Kjor(Svar("""
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a9", "polaritet": "positiv"},
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a2", "polaritet": "positiv"},
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a3", "polaritet": "positiv"},
            {"eid": "n1", "sitat": "Klage går til klagenemnda", "kategori": "relasjon", "type": "klageinstans_for", "til": "a4", "polaritet": "positiv"}
            """,
            """
            {"id": "a2", "tekstform": "Nærings- og fiskeridepartementet"},
            {"id": "a3", "tekstform": "departementet", "varianter": ["Olje- og energidepartementet"]},
            {"id": "a4", "tekstform": "klagenemnda", "entitetstype": "nemnd"}
            """), Tekst);

        Assert.Empty(r.Dokument.Utsagn);
        Assert.Equal(
            [KastetArsak.UkjentAktorreferanse, KastetArsak.UgyldigAktor, KastetArsak.UgyldigAktor, KastetArsak.UgyldigAktor],
            r.Kastet.Select(k => k.Arsak));
        Assert.Equal(1, r.KastedeAktorer["tekstform står ikke i teksten"]);
        Assert.Equal(1, r.KastedeAktorer["variant står ikke i teksten"]);
        Assert.Equal(1, r.KastedeAktorer["ukjent entitetstype"]);
    }

    [Fact]
    public void Manglende_polaritet_eller_feil_jsontype_kastes_som_ugyldig_felt_uten_standardverdi()
    {
        var r = Kjor(Svar("""
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse"},
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "polaritet": "positiv", "betinget": "nei"},
            {"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "polaritet": "positiv", "sikkerhet": "høy"}
            """), Tekst);

        Assert.Empty(r.Dokument.Utsagn);
        Assert.All(r.Kastet, k => Assert.Equal(KastetArsak.UgyldigFelt, k.Arsak));
        Assert.Equal(3, r.Kastet.Count);
    }

    [Fact]
    public void Duplikate_rader_telles_en_gang()
    {
        const string rad = """{"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a1", "polaritet": "positiv"}""";
        var r = Kjor(Svar(rad + "," + rad), Tekst);

        Assert.Single(r.Dokument.Utsagn);
        Assert.Equal(KastetArsak.Duplikat, Assert.Single(r.Kastet).Arsak);
    }

    [Fact]
    public void Store_kilder_deles_i_flere_kall_uten_aa_kutte_noder_og_tokens_summeres()
    {
        var tekster = Enumerable.Range(1, 10).Select(i => $"Ledd {i}: " + new string('x', 90)).ToArray();
        var klient = new KannetKlient(_ => """{"aktorer": [], "utsagn": []}""", inn: 50, ut: 5);
        var r = new KiStrukturkonverterer(klient, Konfig("m"), valg: new KiStrukturkonverteringsvalg(MaksTegnPerKall: 300))
            .KonverterMedRapportAsync(Grunnlag(tekster)).GetAwaiter().GetResult();

        Assert.True(r.AntallDeler > 1);
        Assert.Equal(r.AntallDeler, klient.Kontekster.Count);
        // Hver node står i nøyaktig ett kall, hel.
        var alle = string.Join("\n", klient.Kontekster);
        for (var i = 1; i <= 10; i++) Assert.Single(Regex.Matches(alle, $@"^\[n{i}\] Ledd {i}: x{{90}}\r?$", RegexOptions.Multiline));
        Assert.Equal((50 * r.AntallKall, 5 * r.AntallKall), (r.InputTokens, r.OutputTokens));
    }

    [Fact]
    public void Ledsagende_kilde_faar_egne_kall_og_overskrifter_er_kontekst_uten_tagg()
    {
        const string forskriftEli = "https://lovdata.no/eli/forskrift/2001/01/01/1/nor";
        var noder = new List<Strukturnode>
        {
            new("Testloven", Eli, $"{Eli}/§1", "paragraf", "Myndighet", null),
            new("Testloven", Eli, $"{Eli}/§1/ledd-1", "ledd", null, "Departementet kan gi forskrift."),
            new("Testforskriften", forskriftEli, $"{forskriftEli}/§1/ledd-1", "ledd", null, "Direktoratet fatter vedtak."),
        };
        var klient = new KannetKlient(_ => """{"aktorer": [], "utsagn": []}""");
        var r = new KiStrukturkonverterer(klient, Konfig("m"))
            .KonverterMedRapportAsync(new Strukturkonverteringsgrunnlag("Testloven", Eli, noder)).GetAwaiter().GetResult();

        Assert.Equal(2, r.AntallDeler);
        var kontekster = klient.Kontekster.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Contains("## §1 Myndighet\n", kontekster.Single(k => k.StartsWith("# Testloven")).Replace("\r", ""));
        Assert.Contains("[n2] Direktoratet fatter vedtak.", kontekster.Single(k => k.StartsWith("# Testforskriften")));
        Assert.Equal("Testforskriften", Assert.Single(r.Dokument.Ledsagende).Tittel);
    }

    [Fact]
    public void Uten_ekte_leverandor_er_proveniensen_stub_og_aktiv_leverandor_uten_modell_er_konfigfeil()
    {
        Assert.Equal("ki:stub-v1", new KiStrukturkonverterer(new KannetKlient(_ => ""), Konfig()).Oppdagelseskilde);

        var utenModell = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RegelIde:KiAgent:Leverandor"] = "OpenAiKompatibel" }).Build();
        Assert.Throws<InvalidOperationException>(() => new KiStrukturkonverterer(new KannetKlient(_ => ""), utenModell).Modell);
    }

    [Fact]
    public void Samme_tekstform_fra_to_kall_blir_en_aktor_og_motstridende_entitetstype_blir_null()
    {
        var klient = new KannetKlient(k => Svar(
            k.Contains("[n1]")
                ? """{"eid": "n1", "sitat": "Departementet kan gi forskrift", "kategori": "kompetanse", "type": "forskriftskompetanse", "fra": "a1", "polaritet": "positiv"}"""
                : """{"eid": "n2", "sitat": "departementet treffer vedtak", "kategori": "kompetanse", "type": "vedtakskompetanse", "fra": "a1", "polaritet": "positiv"}""",
            k.Contains("[n1]")
                ? """{"id": "a1", "tekstform": "Departementet", "entitetstype": "organ"}"""
                : """{"id": "a1", "tekstform": "departementet", "entitetstype": "rolle"}"""));
        var r = new KiStrukturkonverterer(klient, Konfig("m"), valg: new KiStrukturkonverteringsvalg(MaksTegnPerKall: 10))
            .KonverterMedRapportAsync(Grunnlag(Tekst, "Etter klage treffer departementet treffer vedtak.")).GetAwaiter().GetResult();

        Assert.Equal(2, r.Dokument.Utsagn.Count);
        var a = Assert.Single(r.Dokument.Aktorer);
        Assert.Null(a.Entitetstype);
        Assert.Contains("entitetstype", a.Kommentar);
        Assert.Equal(2, a.AntallForekomster);
    }
}
