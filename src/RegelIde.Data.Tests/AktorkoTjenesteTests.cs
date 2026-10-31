using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #440 PR 1] Aktørkøen mot ekte embedded Postgres: tabellen rutingen leser (<see cref="AktorkoTjeneste.LastRutingstabellAsync"/>)
/// og detaljen for ett navn. Navnene er unike per test (Guid), fordi testsamlingen deler database; tallene for hele køen (192 av 1 211) er
/// målt mot den delte basen i PR-en.
/// <list type="bullet">
/// <item><b>AK3</b> Etter #423 havner 0 hos Agder: en avvist navneform er aldri med i tabellen, og et navn som bare har den, står i køen.</item>
/// <item><b>AK1/AK4</b> Detaljen samler forekomstene per rettskilde, merker forekomsten i veiledningen, og hjemmelforslaget kommer bare fra
/// normtekst i rettskildene der navnet står.</item>
/// <item>Skriver ingenting: antall rader i navnekandidater og begreper er det samme etter lesingen.</item>
/// </list>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AktorkoTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AktorkoTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Unikt navn med bare bokstaver: hjemmelregelen deler tekst i ord av bokstaver, så sifre ville delt navnet.</summary>
    private static string Unik(string s) => s + new string(Guid.NewGuid().ToString("N")[..12].Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))).ToArray());

    private static async Task<Guid> RettskildeAsync(RegelIdeDbContext db, params (string Eid, string Tekst, bool Merknad)[] noder)
    {
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testlov " + id,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var i = 0;
        foreach (var (eid, tekst, merknad) in noder)
        {
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = id, Eid = eid, KildeId = eid, NodeType = "ledd", Tekst = tekst, ErMerknad = merknad,
                Sorteringsrekkefolge = i++,
            });
        }
        await db.SaveChangesAsync();
        return id;
    }

    private static void Kandidat(RegelIdeDbContext db, Guid rettskildeId, string eid, string tekst, string navn)
    {
        var start = tekst.IndexOf(navn, StringComparison.Ordinal);
        db.Navnekandidater.Add(new NavnekandidatEntitet
        {
            Id = Guid.NewGuid(), ForeslattTekst = navn, Kategori = "gruppe", RettskildeId = rettskildeId, NodeEid = eid, StartOffset = start,
            EndOffset = start + navn.Length, Status = "Venter", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
    }

    [Fact]
    public async Task AK3_avvist_navneform_er_aldri_med_og_navnet_star_i_koen_ikke_hos_aktoren()
    {
        await using var db = _fixture.NyDbContext();
        var agder = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("AGDER TEST ") };
        var erstattetAv = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("Etterfølger ") };
        var duplikat = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("Duplikat "), ErstattetAvId = erstattetAv.Id };
        db.Virksomheter.AddRange(agder, erstattetAv, duplikat);
        var avvistTerm = Unik("Fylkesting");
        var gjeldendeTerm = Unik("Agdertinget");
        db.Begreper.AddRange(
            new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = agder.Id, Term = avvistTerm, Status = "publisert",
                Godkjenning = Strukturkanter.Avvist, AvvisningBegrunnelse = "#423-test", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            },
            new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = agder.Id, Term = gjeldendeTerm, Status = "publisert",
                OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        await db.SaveChangesAsync();

        var tabell = await AktorkoTjeneste.LastRutingstabellAsync(db);
        var rk = Guid.NewGuid();

        Assert.Equal(Aktorkoruter.Aktorko, Aktorkoregler.Ruter(tabell, avvistTerm, rk).Rute);
        Assert.Equal(Aktorkoruter.Aktorko, Aktorkoregler.Ruter(tabell, avvistTerm + "en", rk).Rute); // ingen bøyning av en avvist form
        var gjeldende = Aktorkoregler.Ruter(tabell, gjeldendeTerm.ToUpperInvariant(), rk);
        Assert.Equal((Aktorkoruter.KjentAktor, agder.Id), (gjeldende.Rute, gjeldende.Treff[0].VirksomhetId));
        Assert.Equal(Aktorkoruter.KjentAktor, Aktorkoregler.Ruter(tabell, agder.Navn, rk).Rute);          // registernavnet
        Assert.Equal(Aktorkoruter.Aktorko, Aktorkoregler.Ruter(tabell, duplikat.Navn, rk).Rute);          // erstattet (duplikat): ikke med
    }

    [Fact]
    public async Task Detaljen_samler_forekomstene_merker_veiledningen_og_gir_hjemmelforslag_bare_fra_normtekst()
    {
        await using var db = _fixture.NyDbContext();
        var navn = Unik("Testdirektoratet");
        var rad = Unik("Testrådet").ToLowerInvariant();
        var radBestemt = rad + "et";
        var lov = await RettskildeAsync(db,
            ("https://test/lov/§4/ledd-1", $"For å sikre samarbeidet skal hver barnehage ha et {rad} og et utvalg.", false),
            ("https://test/lov/§13/ledd-2", $"{navn} skal veilede kommuner og barnehager.", false),
            ("https://test/lov/§53/ledd-4", $"{char.ToUpperInvariant(radBestemt[0])}{radBestemt[1..]} skal få uttale seg.", false));
        var forskrift = await RettskildeAsync(db,
            ("kap-veiledning-til-forskriften/rom-til-5/ledd-2", $"Det opprettes en frist som {navn} bestemmer.", true),
            ("https://test/forskrift/§3/ledd-1", $"Barnehager skal rapportere til {navn}.", false));
        Kandidat(db, lov, "https://test/lov/§13/ledd-2", $"{navn} skal veilede kommuner og barnehager.", navn);
        Kandidat(db, forskrift, "kap-veiledning-til-forskriften/rom-til-5/ledd-2", $"Det opprettes en frist som {navn} bestemmer.", navn);
        Kandidat(db, forskrift, "https://test/forskrift/§3/ledd-1", $"Barnehager skal rapportere til {navn}.", navn);
        Kandidat(db, lov, "https://test/lov/§53/ledd-4", $"{char.ToUpperInvariant(radBestemt[0])}{radBestemt[1..]} skal få uttale seg.",
            $"{char.ToUpperInvariant(radBestemt[0])}{radBestemt[1..]}");
        await db.SaveChangesAsync();
        var forKandidater = await db.Navnekandidater.CountAsync();
        var forBegreper = await db.Begreper.CountAsync();

        var tjeneste = new AktorkoTjeneste(db);
        var detalj = await tjeneste.DetaljAsync(navn.ToUpperInvariant());

        Assert.NotNull(detalj);
        Assert.Equal((3, 2), (detalj.Forekomster, detalj.PerRettskilde.Count));
        var veiledning = detalj.PerRettskilde.SelectMany(r => r.Forekomster).Single(f => f.ErMerknad);
        Assert.StartsWith("kap-veiledning", veiledning.Eid);
        Assert.Equal(navn, veiledning.Utdrag!.Substring(veiledning.UtdragStart, navn.Length));
        // Veiledningen har både et opprettelsesuttrykk og navnet, men er merknad (#419): hullet vises.
        Assert.Empty(detalj.Hjemmelforslag);

        var raadet = await tjeneste.DetaljAsync(radBestemt);
        var forslag = Assert.Single(raadet!.Hjemmelforslag);
        Assert.Equal(("https://test/lov/§4/ledd-1", "§ 4 første ledd", "skal … ha et", Strukturkanter.Normalisering),
            (forslag.Eid, forslag.Bestemmelse, forslag.Uttrykk, forslag.Kunnskapsstatus));

        Assert.Null(await tjeneste.DetaljAsync(Unik("Finnes ikke")));
        Assert.Equal((forKandidater, forBegreper), (await db.Navnekandidater.CountAsync(), await db.Begreper.CountAsync()));
    }
}
