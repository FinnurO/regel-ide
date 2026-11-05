using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #440 PR 2] Den presiserte hjemmelregelen (<see cref="Aktorkoregler.FinnHjemmeltreff"/>) mot de 24 forslagene juristrunde-440
/// angrep (<c>data/fasit/strukturmodell/juristrunde-440/inndata.json</c>, kortene h1–h18). Spørsmålet: «Hvilke av de 18 navnene får et
/// hjemmelforslag etter regelen juristen og modellereren ble enige om, og er de riktige?»
/// <para>
/// Nodene er setningene de 24 forslagene sto i (hele leddet), pluss de to leddene de nye uttrykkene treffer, lest fra korpuset 2026-11-04:
/// dl. § 27 første ledd («I hver kommune skal det være et forliksråd») og bhl. § 5 første ledd («det skal være felles samarbeidsutvalg»).
/// </para>
/// <para>
/// Forventet etter sammendraget: 8 treff. Forleddssperren stopper ett (bhl. § 5, «felles samarbeidsutvalg»), så 7 blir forslag. 5 er riktige
/// (foreldrerådet, styrer, samarbeidsutvalget, førstelagmannen, forliksrådet), 2 er feil på grunn av grupperingen eller aktørstatus
/// (direktøren dl. § 9, Dommere dl. § 3). Juristens «8 forslag» teller med treffet sperren stopper.
/// </para>
/// </summary>
public class AktorkoHjemmelregelJuristrundeTests
{
    private static string FinnRepoRot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "data", "fasit"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Fant ikke repo-roten (data/fasit).");
    }

    /// <summary>En stabil id per rettskildenavn (inndataene har bare navnet).</summary>
    private static Guid Id(string rettskilde) => new(MD5.HashData(Encoding.UTF8.GetBytes(rettskilde)));

    private const string Dl = "Domstolloven – dl";
    private const string Bhl = "Barnehageloven – bhl";

    private sealed record Kort(string Id, string Navn, string[] Varianter, List<HjemmelNode> Noder);

    private static List<Kort> LesKort()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(FinnRepoRot(), "data", "fasit", "strukturmodell", "juristrunde-440", "inndata.json")));
        var ut = new List<Kort>();
        foreach (var k in doc.RootElement.GetProperty("kort").EnumerateArray())
        {
            var id = k.GetProperty("id").GetString()!;
            if (id == "R") continue;
            var varianter = k.GetProperty("varianter").EnumerateArray().Select(v => v.GetProperty("form").GetString()!).ToArray();
            var noder = new List<HjemmelNode>();
            var i = 0;
            foreach (var f in k.GetProperty("foreslatt").GetProperty("hjemmelforslag").EnumerateArray())
            {
                var rk = f.TryGetProperty("kortnavn", out var kn) && kn.ValueKind == JsonValueKind.String ? kn.GetString()! : f.GetProperty("rettskilde").GetString()!;
                noder.Add(new HjemmelNode(Id(rk), f.GetProperty("eid").GetString()!, f.GetProperty("hele_leddet").GetString()!, false, i++));
            }
            ut.Add(new Kort(id, k.GetProperty("navn").GetString()!, varianter, noder));
        }
        // De to leddene de nye uttrykkene treffer (lest fra korpuset 2026-11-04). Begge står i en lov der navnet står.
        ut.Single(k => k.Id == "h13").Noder.Add(new HjemmelNode(Id(Dl), "https://lovdata.no/eli/lov/1915/08/13/5/nor/§27/ledd-1",
            "I hver kommune skal det være et forliksråd.", false, 100));
        ut.Single(k => k.Id == "h5").Noder.Add(new HjemmelNode(Id(Bhl), "https://lovdata.no/eli/lov/2005/06/17/64/nor/§5/ledd-1",
            "Kommunen kan bestemme at det skal være felles samarbeidsutvalg for kommunal barnehage og grunnskole. Der begge parter ønsker det, "
            + "kan det etableres tilsvarende ordning for privat barnehage og kommunal eller privat skole. Samarbeidsutvalget skal også i slike "
            + "tilfeller settes sammen etter § 4 tredje ledd, men slik at foreldre og ansatte fra både barnehagen og skolen skal være representert, "
            + "jf. opplæringslova § 10-4 første ledd første punktum.", false, 101));
        return ut;
    }

    /// <summary>Kortene der regelen gir forslag, med paragrafen og om juristrunden fant forslaget riktig.</summary>
    private static readonly Dictionary<string, (string Paragraf, bool Riktig)> Forventet = new()
    {
        ["h2"] = ("§4/ledd-1", true),   // foreldrerådet, bhl. § 4 første ledd
        ["h3"] = ("§4/ledd-1", true),   // styrer, forskrift om pedagogisk bemanning § 4 første ledd
        ["h5"] = ("§4/ledd-1", true),   // samarbeidsutvalget, bhl. § 4 første ledd
        ["h10"] = ("§9/ledd-1", false), // direktøren: Høyesteretts direktør, ikke DA-direktøren (grupperingen)
        ["h11"] = ("§3/ledd-1", false), // Dommere: «nitten andre dommere» er ikke oppretting av en aktør (aktørstatus)
        ["h13"] = ("§27/ledd-1", true), // forliksrådet, dl. § 27 første ledd (nytt uttrykk)
        ["h14"] = ("§10/ledd-1", true), // førstelagmannen, dl. § 10 første ledd
    };

    [Fact]
    public void De_24_forslagene_fra_juristrunden_blir_8_treff_7_forslag_og_5_riktige()
    {
        var kort = LesKort();
        Assert.Equal(18, kort.Count);
        Assert.Equal(24, kort.Sum(k => k.Noder.Count(n => n.Rekkefolge < 100)));

        var treff = kort.SelectMany(k => Aktorkoregler.FinnHjemmeltreff(k.Varianter, k.Noder).Select(t => (k.Id, t))).ToList();
        Assert.Equal(8, treff.Count);
        var sperret = Assert.Single(treff, t => t.t.Sperret is not null);
        Assert.Equal("h5", sperret.Id);
        Assert.EndsWith("§5/ledd-1", sperret.t.Forslag.Eid);
        Assert.Contains("felles samarbeidsutvalg", sperret.t.Sperret);

        var forslag = treff.Where(t => t.t.Sperret is null).ToList();
        Assert.Equal(7, forslag.Count);
        Assert.Equal(Forventet.Keys.Order(), forslag.Select(f => f.Id).Order());
        foreach (var (id, t) in forslag) Assert.EndsWith(Forventet[id].Paragraf, t.Forslag.Eid);
        Assert.Equal(5, forslag.Count(f => Forventet[f.Id].Riktig));
    }

    [Fact]
    public void Ingen_forslag_kommer_fra_oppnevner_opprettes_eller_etableres()
    {
        var treff = LesKort().SelectMany(k => Aktorkoregler.FinnHjemmeltreff(k.Varianter, k.Noder)).ToList();
        Assert.All(treff, t => Assert.Contains(t.Forslag.Uttrykk, new[] { "skal_ha", "skal_vaere" }));
    }

    [Theory]
    [InlineData("h2", "foreldreråd", Strukturkanter.Normalisering)]
    [InlineData("h3", "styrer", Strukturkanter.Tekstfunn)]
    [InlineData("h13", "forliksråd", Strukturkanter.Normalisering)]
    [InlineData("h14", "førstelagmann", Strukturkanter.Normalisering)]
    public void Kunnskapsstatusen_er_tekstfunn_bare_naar_navnet_star_ordrett(string id, string treff, string status)
    {
        var k = LesKort().Single(x => x.Id == id);
        var f = Assert.Single(Aktorkoregler.FinnHjemmelforslag(k.Varianter, k.Noder));
        Assert.Equal((treff, status), (f.Treff, f.Kunnskapsstatus));
    }
}
