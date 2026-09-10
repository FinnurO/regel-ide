using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, hjemmel-presisjon-runden, 2026-09-10, issue #217] Etterfyllingen som peker eksisterende
/// hjemmelrader på den noden kilden faktisk presiserer.
///
/// <para>
/// En hjemmel ER en bestemmelse. «§ 13-1 fjerde ledd» lagret som «§ 13-1» tvinger en modelløren til
/// selv å finne ut hvilket ledd som ga myndigheten — nettopp den gjettingen applikasjonen finnes for å
/// fjerne. Testene her dekker de utgangene som betyr noe: presiseringen løses, presiseringen finnes
/// ikke i kilden (og paragrafnivå er da RIKTIG), og presiseringen finnes men noden gjør det ikke
/// (tapet skal bli SYNLIG, ikke stille).
/// </para>
///
/// <para>Selve parsingen av de to header-feltene er dekket i <c>HjemmelKonverteringTests</c> mot den
/// ekte NTNU-fixturen — her testes oppløsningen mot noder.</para>
///
/// <para>Hver test lager sin EGEN syntetiske lov med et unikt løpenummer i datokoden. Fikstursen
/// deles av hele testkollektivet, og <c>ux_rettskilder_eli_gjeldende_delt</c> tillater ikke to
/// gjeldende rettskilder med samme ELI — en fast ELI her ville gjort testene avhengige av
/// kjørerekkefølgen.</para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class HjemmelPresisjonEtterfyllingTjenesteTests(EmbeddedPostgresFixture fixture)
{
    /// <summary>Én isolert scene: unik lov-ELI, hjemmelrad på paragrafnivå, og rå HTML å parse.</summary>
    private sealed record Scene(Guid ForskriftId, string ParagrafEid, string LeddEid);

    private static int _lopenummer = 1000;

    /// <summary>Minimal, men strukturelt ekte hjemmelslinje: samme form som den ekte NTNU-fixturen har
    /// (verifisert i HjemmelKonverteringTests), uten resten av dokumentet.</summary>
    private static string RaaHtml(string href) =>
        "<dl class=\"documentInfo\"><dt class=\"miscInformation\">Annet om dokumentet</dt>" +
        "<dd class=\"miscInformation\"><strong>Hjemmel:</strong> Fastsatt med hjemmel i " +
        $"<a href=\"{href}\">lov 8. mars 2024 § 13-1 fjerde ledd</a>.</dd></dl>";

    private async Task<Scene> OpprettSceneAsync(
        RegelIdeDbContext db, bool medLeddnode, string presiseringIHref = "/ledd/4")
    {
        var nr = Interlocked.Increment(ref _lopenummer);
        var lovEli = $"https://lovdata.no/eli/lov/2024/03/08/{nr}/nor";
        var paragrafEid = $"{lovEli}/§13-1";
        var leddEid = $"{paragrafEid}/ledd-4";
        var href = $"lov/2024-03-08-{nr}/§13-1{presiseringIHref}";

        var lovId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = lovId, Doctype = "act", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = $"Testlov {nr}", Eli = lovEli,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = lovId, Eid = paragrafEid, KildeId = "§13-1",
            NodeType = "paragraf", Nummer = "§ 13-1", Overskrift = "Forskrifter om grader",
        });
        if (medLeddnode)
        {
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = lovId, Eid = leddEid, KildeId = "ledd-4",
                NodeType = "ledd", Nummer = "4", Tekst = "Departementet kan gi forskrift om grader.",
            });
        }

        var forskriftId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = forskriftId, Doctype = "act", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = $"Ph.d.-forskrift (test {nr})",
            Eli = $"https://lovdata.no/eli/forskrift/2026/02/03/{nr}/nor",
            Innhold = Encoding.UTF8.GetBytes(RaaHtml(href)),
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeHjemler.Add(new RettskildeHjemmelEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = forskriftId,
            HjemmelEid = paragrafEid, HjemmelRettskildeId = lovId, Sorteringsrekkefolge = 0,
        });
        await db.SaveChangesAsync();
        return new Scene(forskriftId, paragrafEid, leddEid);
    }

    [Fact]
    public async Task Oppgraderer_hjemmelen_til_leddnoden_kilden_presiserer()
    {
        await using var db = fixture.NyDbContext();
        var scene = await OpprettSceneAsync(db, medLeddnode: true);

        var resultat = await new HjemmelPresisjonEtterfyllingTjeneste(db).KjorAsync();

        var hjemmel = await db.RettskildeHjemler.SingleAsync(h => h.RettskildeId == scene.ForskriftId);
        Assert.Equal(scene.LeddEid, hjemmel.HjemmelEid);
        Assert.Null(hjemmel.UlostPresisering);
        Assert.True(resultat.Oppgradert >= 1);
    }

    [Fact]
    public async Task Presisering_uten_node_beholder_paragrafniva_men_gjor_tapet_synlig()
    {
        // Loven er referert men ikke importert dypt nok — en LEGITIM tilstand, ikke en feil. Poenget er
        // at «fjerde ledd» ikke forsvinner stille: paragrafnivået står, og presiseringen ligger igjen i
        // ulost_presisering slik at den kan løses ved en senere kjøring.
        await using var db = fixture.NyDbContext();
        var scene = await OpprettSceneAsync(db, medLeddnode: false);

        var resultat = await new HjemmelPresisjonEtterfyllingTjeneste(db).KjorAsync();

        var hjemmel = await db.RettskildeHjemler.SingleAsync(h => h.RettskildeId == scene.ForskriftId);
        Assert.Equal(scene.ParagrafEid, hjemmel.HjemmelEid);
        Assert.Equal("ledd-4", hjemmel.UlostPresisering);
        Assert.True(resultat.PresiseringKunneIkkeLoses >= 1);
    }

    [Fact]
    public async Task Kilde_uten_presisering_lar_paragrafnivaet_sta_urort()
    {
        // 82 % av hjemmelsreferansene har ingenting å presisere. Paragrafnivå er da svaret, og raden
        // skal ikke få et ulost_presisering-flagg som antyder et tap som ikke finnes.
        await using var db = fixture.NyDbContext();
        var scene = await OpprettSceneAsync(db, medLeddnode: true, presiseringIHref: "");

        await new HjemmelPresisjonEtterfyllingTjeneste(db).KjorAsync();

        var hjemmel = await db.RettskildeHjemler.SingleAsync(h => h.RettskildeId == scene.ForskriftId);
        Assert.Equal(scene.ParagrafEid, hjemmel.HjemmelEid);
        Assert.Null(hjemmel.UlostPresisering);
    }

    [Fact]
    public async Task Er_idempotent_en_allerede_oppgradert_rad_rores_ikke_igjen()
    {
        await using var db = fixture.NyDbContext();
        var scene = await OpprettSceneAsync(db, medLeddnode: true);
        var tjeneste = new HjemmelPresisjonEtterfyllingTjeneste(db);

        await tjeneste.KjorAsync();
        var andreKjoring = await tjeneste.KjorAsync();

        var hjemmel = await db.RettskildeHjemler.SingleAsync(h => h.RettskildeId == scene.ForskriftId);
        Assert.Equal(scene.LeddEid, hjemmel.HjemmelEid);
        // Raden teller nå som «allerede presis», og skal ikke telles som oppgradert på nytt.
        Assert.True(andreKjoring.AlleredePresise >= 1);
    }

    [Fact]
    public async Task Bokstavniva_faller_tilbake_til_leddet_som_finnes_og_resten_blir_synlig()
    {
        // Nodetreet har målt bare paragraf/ledd/punkt/kapittel — ingen bokstav-noder. «ledd/4/bokstav/a»
        // skal derfor løses til leddet, og «bokstav-a» bli stående som uløst. Dokumentert avgrensning
        // for denne runden (issue #217 kriterium 4), ikke en uavklart mangel.
        await using var db = fixture.NyDbContext();
        var scene = await OpprettSceneAsync(db, medLeddnode: true, presiseringIHref: "/ledd/4/bokstav/a");

        await new HjemmelPresisjonEtterfyllingTjeneste(db).KjorAsync();

        var hjemmel = await db.RettskildeHjemler.SingleAsync(h => h.RettskildeId == scene.ForskriftId);
        Assert.Equal(scene.LeddEid, hjemmel.HjemmelEid);
        Assert.Equal("bokstav-a", hjemmel.UlostPresisering);
    }
}
