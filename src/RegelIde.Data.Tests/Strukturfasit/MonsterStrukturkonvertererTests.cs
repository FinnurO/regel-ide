using System.Text.Json;
using System.Text.Json.Nodes;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Enhetstester for <see cref="MonsterStrukturkonverterer"/>:
/// at mønstrene oppfører seg slik de er beskrevet (særlig at et endepunkt som ikke kan avgjøres blir
/// null, ikke gjettet), og at utdata holder kontrakten i FORMAT.md. Ingen database.
/// </summary>
public class MonsterStrukturkonvertererTests
{
    private const string Eli = "https://lovdata.no/eli/lov/2000/01/01/1/nor";

    private static Strukturdokument Konverter(params string[] tekster) =>
        new MonsterStrukturkonverterer().Konverter(new Strukturkonverteringsgrunnlag(
            "Testloven", Eli,
            tekster.Select((t, i) => new Strukturnode("Testloven", Eli, $"{Eli}/§{i + 1}/ledd-1", "ledd", null, t)).ToList()));

    private static string? Tekstform(Strukturdokument d, string? id) => id is null ? null : d.Aktorer.Single(a => a.Id == id).Tekstform;

    [Fact]
    public void Forskriftskompetanse_direkte_ordstilling_gir_aktoren_som_fra_og_objektet_fra_teksten()
    {
        var d = Konverter("Departementet kan gi forskrift om leveringsplikten.");

        var u = Assert.Single(d.Utsagn);
        Assert.Equal(("kompetanse", "forskriftskompetanse"), (u.Kategori, u.Type));
        Assert.Equal("Departementet", Tekstform(d, u.Fra));
        Assert.Null(u.Til);
        Assert.Equal("forskrift om leveringsplikten", u.Objekt);
        Assert.Equal("monster:forskrift-gi", u.Oppdagelseskilde);
    }

    [Fact]
    public void Forskriftskompetanse_omvendt_ordstilling_finner_subjektet_etter_modalverbet()
    {
        var d = Konverter("For konsesjoner etter § 5-1 kan departementet gi nærmere forskrifter og fastsette vilkår.");

        var u = Assert.Single(d.Utsagn, x => x.Type == "forskriftskompetanse");
        Assert.Equal("departementet", Tekstform(d, u.Fra));
    }

    [Fact]
    public void Forskriftskompetanse_i_bisetning_gir_to_utsagn()
    {
        var d = Konverter("Departementet kan gi forskrift om at reguleringsmyndigheten kan gi forskrift om metoder for beregning.");

        var fra = d.Utsagn.Where(u => u.Type == "forskriftskompetanse").Select(u => Tekstform(d, u.Fra)).ToList();
        Assert.Equal(["Departementet", "reguleringsmyndigheten"], fra);
    }

    [Theory]
    [InlineData("Det kan gis forskrift om gebyr.")]
    [InlineData("Den kan gi forskrift om gebyr.")]
    [InlineData("Etter søknad kan ikke gi forskrift om gebyr.")]
    public void Pronomen_eller_ikke_aktor_som_subjekt_gir_ingen_forskriftskompetanse(string tekst)
    {
        Assert.DoesNotContain(Konverter(tekst).Utsagn, u => u.Type == "forskriftskompetanse");
    }

    [Fact]
    public void Sideordnede_aktorer_gir_ett_utsagn_hver_og_negativ_instruksjon_uten_gjettet_instruerende()
    {
        var d = Konverter("Reguleringsmyndigheten og klagenemnda kan ikke instrueres i disponeringen av tildelte budsjettmidler.");

        var utsagn = d.Utsagn.Where(u => u.Type == "instruksjon").ToList();
        Assert.Equal(2, utsagn.Count);
        Assert.All(utsagn, u => Assert.Equal("negativ", u.Polaritet));
        Assert.All(utsagn, u => Assert.Null(u.Fra));
        Assert.Equal(["Reguleringsmyndigheten", "klagenemnda"], utsagn.Select(u => Tekstform(d, u.Til)));
    }

    [Fact]
    public void Bindestrek_foran_og_holder_sammensatt_navn_samlet()
    {
        var d = Konverter("Kongens myndighet etter lov 12. juni 1987 nr. 56 § 2-11 delegeres til Kommunal- og regionaldepartementet.");

        var u = Assert.Single(d.Utsagn, x => x.Type == "delegerer_til");
        Assert.Equal("Kongen", Tekstform(d, u.Fra));
        Assert.Equal("Kommunal- og regionaldepartementet", Tekstform(d, u.Til));
    }

    [Fact]
    public void Endepunkt_som_ikke_kan_avgjores_fra_setningen_blir_null()
    {
        // «disse» peker tilbake i teksten — mønsterlaget følger ikke pronomen (CLAUDE.md §8).
        var d = Konverter("Styret kan delegere myndighet til disse.");

        var u = Assert.Single(d.Utsagn, x => x.Type == "delegerer_til");
        Assert.Equal("Styret", Tekstform(d, u.Fra));
        Assert.Null(u.Til);
    }

    [Fact]
    public void Klage_uten_oppgitt_forsteinstans_har_null_som_til()
    {
        var d = Konverter("Vedtaket kan påklages til klagenemnda.");

        var u = Assert.Single(d.Utsagn, x => x.Type == "klageinstans_for");
        Assert.Equal("klagenemnda", Tekstform(d, u.Fra));
        Assert.Null(u.Til);
    }

    [Fact]
    public void Rettskretsliste_holder_navn_med_og_inne_i_lista_samlet()
    {
        var d = Konverter("Agder tingrett, med rettssteder i Arendal og Kristiansand, som dekker kommunene Arendal, Evje og Hornnes, Froland og Åseral.");

        // [ENDRET, issue #312] Kanttypen er A har_ansvarsomrade (tingrett → kommune), ikke O bestar_av (rettskrets → kommune).
        var kommuner = d.Utsagn.Where(u => u.Type == "har_ansvarsomrade").Select(u => Tekstform(d, u.Til)).ToList();
        Assert.Equal(["Arendal", "Evje og Hornnes", "Froland", "Åseral"], kommuner);
        Assert.All(d.Utsagn.Where(u => u.Type == "har_ansvarsomrade"), u => Assert.Equal("Agder tingrett", Tekstform(d, u.Fra)));

        var seter = d.Utsagn.Where(u => u.Type == "har_sete_i").Select(u => Tekstform(d, u.Til)).ToList();
        Assert.Equal(["Arendal", "Kristiansand"], seter);
    }

    [Fact]
    public void Tvetydig_liste_forkastes_i_stedet_for_a_velge_en_deling()
    {
        // To «og» i siste element: «Møre og Romsdal» + «Trøndelag», eller «Møre» + «Romsdal og Trøndelag»?
        var d = Konverter("Lagsognene Møre og Romsdal og Trøndelag utgjør Frostating lagdømme.");

        Assert.DoesNotContain(d.Utsagn, u => u.Type == "bestar_av");
    }

    [Fact]
    public void Kommuneliste_definert_ved_retning_tas_ikke()
    {
        var d = Konverter("Sørsamisk valgkrets: kommunene fra og med Rana og Rødøy og sørover i Nordland fylke");

        Assert.Empty(d.Utsagn);
    }

    [Fact]
    public void Skrastreknavn_far_sprakformene_som_varianter()
    {
        var d = Konverter("Østre valgkrets/Nuortaguovllu válgabiire: kommunene Sør-Varanger, Nesseby og Vadsø i Finnmark fylke");

        var krets = d.Aktorer.Single(a => a.Tekstform.StartsWith("Østre valgkrets", StringComparison.Ordinal));
        Assert.Equal(["Østre valgkrets", "Nuortaguovllu válgabiire"], krets.Varianter);
        Assert.Equal(3, d.Utsagn.Count(u => u.Type == "bestar_av"));
        Assert.Equal(3, d.Utsagn.Count(u => u.Type == "del_av"));
    }

    [Fact]
    public void Aktorfelt_som_krever_skjonn_eller_oppslag_star_null()
    {
        var d = Konverter("Departementet kan gi forskrift om leveringsplikten.");

        var a = Assert.Single(d.Aktorer);
        Assert.Null(a.Entitetstype);
        Assert.Null(a.Navngitt);
        Assert.Null(a.Referent);
        Assert.Null(a.Opplosning);
    }

    /// <summary>
    /// Kontrakten over hele det ekte grunnlaget (de fem fasitkildene): sitat er eksakt delstreng av
    /// nodeteksten (FORMAT.md «Ingen gjetting»), eId finnes, fra/til peker på aktører i dokumentet,
    /// og id-ene er unike.
    /// </summary>
    [Fact]
    public void Utdata_for_fasitkildene_holder_kontrakten_i_FORMAT()
    {
        foreach (var kilde in StrukturfasitLeser.LesAlle())
        {
            var d = new MonsterStrukturkonverterer().Konverter(kilde.Grunnlag);
            var tekstPerEid = kilde.Noder.Where(n => n.Tekst is not null).GroupBy(n => n.Eid).ToDictionary(g => g.Key, g => g.First().Tekst!);
            var aktorIder = d.Aktorer.Select(a => a.Id).ToHashSet();

            Assert.Equal(d.Aktorer.Count, aktorIder.Count);
            Assert.Equal(d.Utsagn.Count, d.Utsagn.Select(u => u.Id).Distinct().Count());
            foreach (var u in d.Utsagn)
            {
                Assert.True(tekstPerEid.TryGetValue(u.Eid, out var tekst), $"{kilde.Navn}: ukjent eId {u.Eid}");
                Assert.Contains(u.Sitat, tekst);
                if (u.Fra is not null) Assert.Contains(u.Fra, aktorIder);
                if (u.Til is not null) Assert.Contains(u.Til, aktorIder);
                Assert.StartsWith("monster:", u.Oppdagelseskilde);
                Assert.Contains(u.Polaritet, new[] { "positiv", "negativ" });
            }
        }
    }

    [Fact]
    public void Konverteringen_er_deterministisk()
    {
        var kilde = StrukturfasitLeser.LesAlle()[0];
        var a = JsonSerializer.Serialize(new MonsterStrukturkonverterer().Konverter(kilde.Grunnlag));
        var b = JsonSerializer.Serialize(new MonsterStrukturkonverterer().Konverter(kilde.Grunnlag));
        Assert.Equal(a, b);
    }

    /// <summary>
    /// Recordene speiler FORMAT.md: hvert felt fasiten bruker på aktører og utsagn finnes i
    /// serialiseringen av recordene, med samme navn — ellers ville konverteringens utdata ikke vært
    /// sammenlignbar med fasiten (docs/33 §5.1). Toppnivåfelt som bare finnes i én fasitfil
    /// («noder_lest_merknad») er fritekst-merknader, ikke en del av kontrakten.
    /// </summary>
    [Fact]
    public void Recordene_har_alle_feltnavnene_fasiten_bruker()
    {
        var aktorfelt = Feltnavn(JsonSerializer.SerializeToNode(new StrukturAktor("a1", "x", [], [], 1, null, null, null, null, null, null) { VerifisertAv = "x" })!); // [ENDRET, #312] + verifisert_av
        var utsagnfelt = Feltnavn(JsonSerializer.SerializeToNode(new StrukturUtsagn("u1", "e", "s", "k", "t", null, null, null, "positiv", null, null, null, null, null) { VerifisertAv = "x" })!);

        foreach (var kilde in StrukturfasitLeser.LesAlle())
        {
            var rot = JsonNode.Parse(File.ReadAllText(Path.Combine(StrukturfasitLeser.FasitMappe, kilde.Navn + ".json")))!;
            foreach (var a in rot["aktorer"]!.AsArray()) Assert.Subset(aktorfelt, Feltnavn(a!));
            foreach (var u in rot["utsagn"]!.AsArray()) Assert.Subset(utsagnfelt, Feltnavn(u!));
        }
    }

    private static HashSet<string> Feltnavn(JsonNode node) => node.AsObject().Select(p => p.Key).ToHashSet();
}
