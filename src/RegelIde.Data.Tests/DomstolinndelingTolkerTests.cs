using RegelIde.Data.Strukturkonvertering;
using RegelIde.Data.Tests.Strukturfasit;
using Xunit;
using Xunit.Abstractions;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #312 «områderegister», 2026-10-08] Tolkingen av inndelingsforskriften (FOR-2021-01-22-163) mot de
/// lagrede nodetekstene fra #306 (<c>data/fasit/strukturmodell/noder/domstolloven.json</c>, 81 noder fra
/// forskriften) og øyeblikksbildene i <c>Seed/</c>. Ingen database — dette er tallene seeden skriver.
/// </summary>
public sealed class DomstolinndelingTolkerTests(ITestOutputHelper utskrift)
{
    internal static IReadOnlyList<Strukturnode> ForskriftNoder() =>
        StrukturfasitLeser.LesAlle().Single(k => k.Navn == "domstolloven").Noder
            .Where(n => n.Eli == DomstolinndelingTolker.ForskriftEli).ToList();

    internal static OmraderegisterKilder.Kilder Kilder() =>
        OmraderegisterKilder.Les(OmraderegisterKilder.StandardMappe)
        ?? throw new InvalidOperationException("Øyeblikksbildene mangler i bin/Seed.");

    private static DomstolinndelingTolker.Resultat Tolk()
    {
        var k = Kilder();
        return DomstolinndelingTolker.Tolk(ForskriftNoder(), k.Kartverket, k.Ssr);
    }

    [Fact]
    public void Gir_28_tingretter_som_dekker_alle_357_kommunene_uten_uloste_navn()
    {
        var r = Tolk();
        foreach (var u in r.Uloste) utskrift.WriteLine("ULØST: " + u);
        foreach (var a in r.Konverteringsavvik) utskrift.WriteLine("AVVIK: " + a);
        utskrift.WriteLine($"Tingretter {r.Tingretter.Count}, kommunekanter {r.Tingretter.Sum(t => t.Kommuner.Count)}, "
                           + $"rettssteder {r.Tingretter.Sum(t => t.Rettssteder.Count)} (herav tettsteder "
                           + $"{r.Tingretter.Sum(t => t.Rettssteder.Count(s => s.Tettsted is not null))}), lagsogn {r.Lagsogn.Count}, "
                           + $"lagdømmer {r.Lagdommer.Count}, delte kommuner {r.DelteKommuner.Count}, uten tingrett {r.KommunerUtenTingrett.Count}");

        Assert.Equal(28, r.Tingretter.Count);
        Assert.Empty(r.Uloste);
        Assert.Empty(r.KommunerUtenTingrett);
        Assert.Equal(357, r.Tingretter.SelectMany(t => t.Kommuner).Select(k => k.Kommunenummer).Distinct().Count());
        Assert.NotNull(r.LagmannsrettEid);
    }

    [Fact]
    public void Lagsogn_og_lagdommer_tas_fra_sogner_leddene_og_avviket_i_konverteringen_listes()
    {
        var r = Tolk();
        Assert.Equal(15, r.Lagsogn.Count); // §§ 11–16: 3 + 2 + 2 + 3 + 3 + 2
        Assert.Equal(6, r.Lagdommer.Count);
        var borgarting = r.Lagdommer.Single(l => l.Navn == "Borgarting lagdømme");
        Assert.Equal(["Oslo, Asker og Bærum", "Buskerud", "Søndre Østfold"], borgarting.Lagsogn);
        var frostating = r.Lagdommer.Single(l => l.Navn == "Frostating lagdømme");
        Assert.Equal(["Møre og Romsdal", "Trööndelagen/Trøndelag"], frostating.Lagsogn);
        // Konverteringen (#307) deler tre av seks «utgjør»-lister annerledes enn sogner-leddene — målt, ikke skjult.
        Assert.Equal(3, r.Konverteringsavvik.Count);
        // Hver tingrett sogner til nøyaktig ett lagsogn (ellers sto det i Uloste).
        Assert.All(r.Tingretter, t => Assert.Single(r.Lagsogn, l => l.Tingretter.Contains(t.Tekstform)));
    }

    [Fact]
    public void Heroy_og_Valer_avgjores_av_fylket_i_paragrafens_forste_ledd()
    {
        var r = Tolk();
        var helgeland = r.Tingretter.Single(t => t.Tekstform == "Helgeland tingrett");
        var sunnmore = r.Tingretter.Single(t => t.Tekstform == "Sunnmøre tingrett");
        Assert.Equal("1818", helgeland.Kommuner.Single(k => k.Kommunenavn == "Herøy").Kommunenummer);
        Assert.Equal("1515", sunnmore.Kommuner.Single(k => k.Kommunenavn == "Herøy").Kommunenummer);
        var hedmarken = r.Tingretter.Single(t => t.Tekstform == "Hedmarken og Østerdal tingrett");
        var sondreOstfold = r.Tingretter.Single(t => t.Tekstform == "Søndre Østfold tingrett");
        Assert.Equal("3419", hedmarken.Kommuner.Single(k => k.Kommunenavn == "Våler").Kommunenummer);
        Assert.Equal("3114", sondreOstfold.Kommuner.Single(k => k.Kommunenavn == "Våler").Kommunenummer);
        Assert.All([helgeland, sunnmore, hedmarken, sondreOstfold],
            t => Assert.Contains("paragrafens første ledd", t.Kommuner.Single(k => k.Kommunenavn is "Herøy" or "Våler").Avgjort));
    }

    [Fact]
    public void Rettssted_som_er_kommune_blir_samme_kommune_og_ellers_tettsted_i_egen_kommune()
    {
        var r = Tolk();
        var sunnmore = r.Tingretter.Single(t => t.Tekstform == "Sunnmøre tingrett");
        var volda = sunnmore.Rettssteder.Single(s => s.Tekstform == "Volda");
        Assert.Equal(sunnmore.Kommuner.Single(k => k.Kommunenavn == "Volda").Kommunenummer, volda.Kommunenummer);
        Assert.Null(volda.Tettsted);

        var nordTroms = r.Tingretter.Single(t => t.Tekstform == "Nord-Troms og Senja tingrett");
        var finnsnes = nordTroms.Rettssteder.Single(s => s.Tekstform == "Finnsnes");
        Assert.Null(finnsnes.Kommunenummer);
        Assert.Equal("5530", finnsnes.Tettsted!.Kommunenummer); // Senja

        var follo = r.Tingretter.Single(t => t.Tekstform == "Follo og Nordre Østfold tingrett");
        Assert.Equal("By", follo.Rettssteder.Single(s => s.Tekstform == "Ski").Tettsted!.Navneobjekttype);
    }

    [Fact]
    public void Ingen_kommune_er_delt_mellom_domssogn_i_gjeldende_forskrift_men_en_delt_kommune_oppdages()
    {
        var r = Tolk();
        Assert.Empty(r.DelteKommuner);

        // Syntetisk: legg Karasjok inn i en annen tingretts liste også. Tolkeren skal liste begge, ikke velge.
        var noder = ForskriftNoder().Select(n => n.Eid.EndsWith("§2/ledd-1/punkt-1", StringComparison.Ordinal)
            ? n with { Tekst = n.Tekst!.Replace("Hasvik,", "Hasvik, Kárášjohka/Karasjok,", StringComparison.Ordinal) }
            : n).ToList();
        var k = Kilder();
        var delt = DomstolinndelingTolker.Tolk(noder, k.Kartverket, k.Ssr);
        var karasjok = Assert.Single(delt.DelteKommuner);
        Assert.Equal("5610", karasjok.Key);
        Assert.Equal(2, karasjok.Value.Count);
    }

    [Fact]
    public void Hvert_lagdomme_pares_med_lagmannsretten_med_samme_navn_og_uten_ett_treff_pares_det_ikke()
    {
        // [Ny, issue #345] Hjemmelen er § 10 første ledd; paret er navnets.
        var r = Tolk();
        Assert.EndsWith("/§10/ledd-1", r.LagmannsrettEid);
        var domstoler = Kilder().BrregDomstoler.Enheter;
        var (par, uparet) = DomstolinndelingTolker.ParLagmannsretter(r.Lagdommer, domstoler);
        Assert.Empty(uparet);
        Assert.Equal(
            ["AGDER LAGMANNSRETT", "BORGARTING LAGMANNSRETT", "EIDSIVATING LAGMANNSRETT", "FROSTATING LAGMANNSRETT", "GULATING LAGMANNSRETT", "HÅLOGALAND LAGMANNSRETT"],
            par.Select(p => p.Lagmannsrett.Navn).Order(StringComparer.Ordinal));
        Assert.Equal("GULATING LAGMANNSRETT", par.Single(p => p.Lagdomme.Navn == "Gulating lagdømme").Lagmannsrett.Navn);

        // Mangler én, eller er navnet tvetydig: ingen par, og lagdømmet listes.
        var utenAgder = domstoler.Where(d => d.Navn != "AGDER LAGMANNSRETT").ToList();
        var dobbel = domstoler.Append(new OmraderegisterKilder.BrregDomstol("999999999", "Gulating lagmannsrett", "ORGL")).ToList();
        var (par1, uparet1) = DomstolinndelingTolker.ParLagmannsretter(r.Lagdommer, utenAgder);
        var (par2, uparet2) = DomstolinndelingTolker.ParLagmannsretter(r.Lagdommer, dobbel);
        Assert.Equal(5, par1.Count);
        Assert.StartsWith("Agder lagdømme: 0 treff", Assert.Single(uparet1));
        Assert.Equal(5, par2.Count);
        Assert.StartsWith("Gulating lagdømme: 2 treff", Assert.Single(uparet2));
    }
}
