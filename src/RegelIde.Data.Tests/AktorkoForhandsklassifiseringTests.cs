using RegelIde.Data;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #440, 2026-11-05] Forhåndsklassifiseringen i aktørkøen, uten database: leses den innebygde fila, gir handlingene de samme tallene
/// som <c>anbefalinger.md</c> (med avviket for navnene som venter på modellvalg), merkes et endret navn som utdatert, og listes det som ikke kan
/// skrives gjennom køens endepunkter som eget steg?
/// </summary>
public class AktorkoForhandsklassifiseringTests
{
    private static Kogruppe Gruppe(ForhandPost p, int? forekomster = null, params string[] skrivemater)
    {
        var varianter = p.Varianter.Select((v, i) => new Kovariant(v.Normalisert, v.Regel, null, null, v.Forekomster,
            [new Skrivemate(i == 0 && skrivemater.Length > 0 ? skrivemater[0] : v.Normalisert, v.Forekomster)])).ToList();
        var antall = forekomster ?? p.Forekomster;
        var rk = Guid.NewGuid();
        var f = Enumerable.Range(0, antall)
            .Select(i => new KoForekomst(Guid.NewGuid(), p.Varianter[0].Normalisert, rk, $"eid{i}", 0, 1, "virksomhet", null, null)).ToList();
        return new Kogruppe(p.Nokkel, p.Navn, varianter, f);
    }

    private static ForhandPost Post(string nokkel) => Forhandsklassifisering.Alle.Single(p => p.Nokkel == nokkel);

    private static readonly ForhandOppslag TomtOppslag = new(new Dictionary<Guid, (string, string, string?)>(),
        new Dictionary<string, (Guid, string, string?)>());

    [Fact]
    public void Fila_er_innebygd_med_193_navn_og_unike_nokler()
    {
        Assert.Equal(193, Forhandsklassifisering.Alle.Count);
        Assert.Equal(193, Forhandsklassifisering.Alle.Select(p => p.Nokkel).Distinct().Count());
        Assert.Equal("2026-11-05", Forhandsklassifisering.Generert);
    }

    [Fact]
    public void Handlingene_gir_tallene_i_anbefalingene_og_navnene_som_venter_far_ingen_knapp()
    {
        var tall = Forhandsklassifisering.Alle.GroupBy(Forhandsklassifisering.Handling).ToDictionary(g => g.Key, g => g.Count());
        // anbefalinger.md: opprett aktør 13 + klasse 23 + rolle 22 + område 6 = 64, men Finland og Sverige venter på modellvalg (#463).
        Assert.Equal(62, tall[Forhandshandling.Godkjenn]);
        Assert.Equal(10, tall[Forhandshandling.Venter]);
        Assert.Equal(39, tall[Forhandshandling.IkkeAktor]);
        Assert.Equal(73, tall[Forhandshandling.GeneriskOmtale]);
        Assert.Equal(7, tall[Forhandshandling.Koble]);
        Assert.Equal(2, tall[Forhandshandling.TaMed]);
        Assert.Equal(Forhandstype.Venter, Forhandsklassifisering.Type(Post("finland")));
        Assert.Null(Forhandsklassifisering.Sikkerhet(Post("fylkeskommunen")));
    }

    [Fact]
    public void Koblingen_tar_grunnen_bare_fra_relasjonen()
    {
        Assert.Equal("kortform", Forhandsklassifisering.Navneformgrunn(Forhandsklassifisering.Koblingsrelasjon(Post("kd"))!));
        Assert.Equal("utgatt", Forhandsklassifisering.Navneformgrunn(Forhandsklassifisering.Koblingsrelasjon(Post("fylkesnemnda"))!));
        // «Høyesterett» er samme som Norges Høyesterett, men relasjonen sier ikke hvilken grunn: den gjettes ikke.
        Assert.Null(Forhandsklassifisering.Navneformgrunn(Forhandsklassifisering.Koblingsrelasjon(Post("høyesterett"))!));
    }

    [Fact]
    public void Et_navn_med_andre_forekomster_er_utdatert_og_et_likt_er_ikke()
    {
        var p = Post("utdanningsdirektoratet");
        Assert.Null(Forhandsklassifisering.Utdatert(p, Gruppe(p)));
        Assert.Equal("9 forekomster da, 10 nå", Forhandsklassifisering.Utdatert(p, Gruppe(p, 10)));
        Assert.NotNull(Forhandsklassifisering.Kort(Gruppe(p, 10))!.Utdatert);
    }

    [Fact]
    public void Navnet_finnes_via_en_annen_variant_nar_nokkelen_har_skiftet()
    {
        var p = Post("barnehagen");
        var g = Gruppe(p) with { Nokkel = "barnehagene" };
        Assert.Same(p, Forhandsklassifisering.Finn(g));
        Assert.Contains("nøkkelen var «barnehagen»", Forhandsklassifisering.Utdatert(p, g));
    }

    [Fact]
    public void Bostedskommune_hjemmelen_er_ikke_regelens_forslag_og_star_som_eget_steg()
    {
        var p = Post("bostedskommune");
        var v = Forhandsklassifisering.Visning(p, Gruppe(p, null, "bostedskommune"), [], TomtOppslag);
        Assert.Equal(Forhandshandling.Godkjenn, v.Handling);
        Assert.Equal(Forhandstype.Rolle, v.Type);
        Assert.False(v.Hjemmel!.ErRegelensForslag);
        Assert.Equal("klassifiseringen", v.Hjemmel.SitatKilde);
        Assert.Contains(v.EgneSteg, s => s.StartsWith("Hjemmelen ("));
        // Navnet i forslaget står i teksten: ingen merknad om skrivemåten.
        Assert.DoesNotContain(v.EgneSteg, s => s.StartsWith("Navnet i forslaget"));
        // Kommunen er ikke slått opp i det tomme oppslaget: relasjonen vises uten lenke og står som eget steg.
        Assert.False(v.Relasjoner.Single().Til!.Finnes);
        Assert.False(v.Relasjoner.Single().SkrivesAvHandlingen);
    }

    [Fact]
    public void Med_regelens_forslag_kan_hjemmelen_lagres()
    {
        var p = Post("foreldrerådet");
        var h = p.Hjemmel!;
        var v = Forhandsklassifisering.Visning(p, Gruppe(p), [(h.RettskildeId!.Value, h.Eid)], TomtOppslag);
        Assert.True(v.Hjemmel!.ErRegelensForslag);
        Assert.DoesNotContain(v.EgneSteg, s => s.StartsWith("Hjemmelen ("));
        // «foreldreråd» (grunnformen) står ikke i teksten.
        Assert.Contains(v.EgneSteg, s => s.StartsWith("Navnet i forslaget, «foreldreråd»"));
    }

    [Fact]
    public void Koblingen_skriver_relasjonen_og_den_er_ikke_eget_steg()
    {
        var p = Post("kd");
        var id = Guid.Parse("61b9a4f8-b938-4a97-903e-b993b0134fc8");
        var oppslag = new ForhandOppslag(new Dictionary<Guid, (string, string, string?)> { [id] = ("virksomhet", "Kunnskapsdepartementet", "virksomhet") },
            new Dictionary<string, (Guid, string, string?)>());
        var v = Forhandsklassifisering.Visning(p, Gruppe(p), [], oppslag);
        Assert.Equal(Forhandshandling.Koble, v.Handling);
        Assert.Equal((id, "kortform", true), (v.Kobling!.VirksomhetId, v.Kobling.Navneformgrunn, v.Kobling.Finnes));
        Assert.True(v.Relasjoner.Single().SkrivesAvHandlingen);
        Assert.Empty(v.EgneSteg);
    }

    [Fact]
    public void Ta_med_peker_pa_nokkelen_til_det_andre_navnet()
    {
        var p = Post("det bestemmende organ");
        var v = Forhandsklassifisering.Visning(p, Gruppe(p), [], TomtOppslag);
        Assert.Equal((Forhandshandling.TaMed, "bestemmende organ"), (v.Handling, v.TaMedINokkel));
    }

    [Fact]
    public void Utdanningsdirektoratet_har_hjemmel_mangler_og_aktortypen_som_eget_steg()
    {
        var p = Post("utdanningsdirektoratet");
        var v = Forhandsklassifisering.Visning(p, Gruppe(p, null, "Utdanningsdirektoratet"), [], TomtOppslag);
        Assert.Null(v.Hjemmel);
        Assert.Equal("mangler", v.HjemmelStatus);
        Assert.Equal(["Aktørtypen (organ) settes på aktørsiden; godkjenningen oppretter aktøren uten type."], v.EgneSteg);
    }
}
