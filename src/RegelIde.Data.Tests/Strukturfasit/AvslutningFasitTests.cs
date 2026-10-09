namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, issue #355 «avslutning speiler innsetting», 2026-10-09] Spørsmålene saken skal flytte, stilt mot FASITEN (uten database):
/// sakens AC4 «hvem kan sette inn en fast dommer?» og at konverteringen (<c>konvertering-355-avslutning.py</c>) faktisk står der —
/// ingen foreleggingskompetanse igjen, kort nr. 58 delt i tre, u275 med tingrettene som motpart.
/// </summary>
public class AvslutningFasitTests
{
    private static Fasitkilde Kilde(string navn) => StrukturfasitLeser.LesAlle().Single(k => k.Navn == navn);

    /// <summary>Domstolloven § 55 første ledd (utnevning) mot §§ 55a, 55e, 55f (konstitusjon): uten undertypen svarer
    /// oppnevningskompetansen om dommere Kongen, Innstillingsrådet og domstollederen; med den bare Kongen.</summary>
    [Fact]
    public void Hvem_kan_sette_inn_en_fast_dommer_er_bare_Kongen()
    {
        var d = Kilde("domstolloven").Fasit;
        var navn = d.Aktorer.ToDictionary(a => a.Id, a => a.Tekstform);
        var omDommere = d.Utsagn.Where(u => u.Kategori == "kompetanse" && u.Type == "oppnevningskompetanse" && u.Fra is not null
                // Innsettingen i en dommerstilling: til = dommere, eller konstitusjon av en dommer (til står ikke i teksten).
                // Oppnevning av en særskilt dommer i én sak (u46) og ansettelse av dommerfullmektiger (u140) er andre spørsmål.
                && (u.Til == "dommer" || u.Undertype is "utnevning" or "konstitusjon"))
            .ToList();
        Assert.Equal(["Innstillingsrådet for dommere", "Kongen", "domstolens leder"],
            omDommere.Select(u => navn[u.Fra!]).Distinct().Order(StringComparer.Ordinal));

        var fast = omDommere.Where(u => u.Undertype == "utnevning").Select(u => navn[u.Fra!]).Distinct().ToList();
        Assert.Equal(["Kongen"], fast);
        Assert.All(omDommere.Where(u => navn[u.Fra!] != "Kongen"), u => Assert.Equal("konstitusjon", u.Undertype));
    }

    [Fact]
    public void Fasiten_er_konvertert_etter_beslutningene()
    {
        var alle = StrukturfasitLeser.LesAlle();
        Assert.DoesNotContain(alle.SelectMany(k => k.Fasit.Utsagn), u => u.Type == "foreleggingskompetanse");
        Assert.DoesNotContain(alle.SelectMany(k => k.Fasit.Utsagn), u => u.Modalitet is not null && u.Kategori != "plikt");

        var dl = Kilde("domstolloven").Fasit.Utsagn;
        // Kort nr. 58: § 55 femte ledd er tre regler.
        var p55 = dl.Where(u => u.Eid.EndsWith("/§55/ledd-5")).ToList();
        Assert.Equal(["annet:forflytning", "avsettingskompetanse/avskjed", "avsettingskompetanse/oppsigelse"],
            p55.Select(u => u.Undertype is null ? u.Type : $"{u.Type}/{u.Undertype}").Order(StringComparer.Ordinal));
        Assert.All(p55, u => Assert.Equal(("negativ", (string?)null, "dommer"), (u.Polaritet, u.Fra, u.Til)));
        // § 51 a: rådgivende → P konsultasjon (kan), forliksrådene negativt, og en negativ anke-rad.
        var p51a = dl.Where(u => u.Eid.Contains("/§51a/")).ToList();
        Assert.Equal(2, p51a.Count(u => u.Kategori == "plikt" && u.Type == "konsultasjonsplikt" && u.Modalitet == "kan"));
        Assert.Single(p51a, u => u.Type == "overprovingskompetanse" && u.Undertype == "anke" && u.Polaritet == "negativ");
        // Beslutning 4: ankeinstansen har organet som motpart.
        Assert.Equal("tr", dl.Single(u => u.Eid.EndsWith("163/nor/§10/ledd-1") && u.Type == "overprovingskompetanse").Til);
        // Beslutning 5: ankeadgang er partsposisjon; § 37 har R representerer.
        Assert.Equal(2, dl.Count(u => u.Type == "annet:ankeadgang" && u.Kategori == "annet:partsposisjon"));
        Assert.Single(dl, u => u.Eid.EndsWith("/§37/ledd-1") && u.Type == "representerer" && u.Til == "staten");
        // u22/u165: G del_av fra stillingen/enheten til organet.
        Assert.Single(dl, u => u.Type == "del_av" && u.Fra == "hr_dir" && u.Til == "hr");
        Assert.Single(Kilde("sameloven").Fasit.Utsagn, u => u.Type == "del_av" && u.Fra == "a132" && u.Til == "a1");
        // Tilbakekall er vedtak med undertype (beslutning 2).
        Assert.Contains(Kilde("helse-og-omsorgstjenesteloven").Fasit.Utsagn, u => u.Type == "vedtakskompetanse" && u.Undertype == "tilbakekall");
    }
}
