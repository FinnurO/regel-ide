using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, registernavn-runden, 2026-09-08] <see cref="VirksomhetVisningsnavnTjeneste"/> mot ekte
/// embedded Postgres. Dette er laget som avgjør hva saksbehandleren SER der en virksomhet nevnes, så
/// de to viktige egenskapene er: den plukker 'gjeldende'-navneformen, og den svarer <c>null</c> (ikke
/// et gjettet navn) når ingen slik navneform finnes.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetVisningsnavnTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetVisningsnavnTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Kåfjord-tilfellet slik det ser ut etter synken: registernavnet urørt, tre navneformer.</summary>
    private static async Task<Virksomhet> OpprettKafjordAsync(RegelIdeDbContext db)
    {
        var virksomhet = new Virksomhet
        {
            Id = Guid.NewGuid(),
            // Brregs egen streng, ordrett — tre navneledd uten noe skilletegn i det hele tatt.
            Navn = $"GAIVUONA SUOHKAN KÅFJORD KOMMUNE KAIVUONON KOMUUNI {Guid.NewGuid():N}",
            Organisasjonsnummer = null,
        };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        return virksomhet;
    }

    private static async Task LeggTilNavneformAsync(
        RegelIdeDbContext db, Guid virksomhetId, string term, string? grunn)
    {
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhetId, term, "test", skosUrl: null, navneformgrunn: grunn);
    }

    [Fact]
    public async Task Plukker_den_gjeldende_navneformen_og_ikke_de_andre()
    {
        await using var db = _fixture.NyDbContext();
        var kafjord = await OpprettKafjordAsync(db);

        await LeggTilNavneformAsync(db, kafjord.Id, "Kåfjord kommune", "gjeldende");
        await LeggTilNavneformAsync(db, kafjord.Id, "Gáivuona suohkan", "parallellnavn");
        await LeggTilNavneformAsync(db, kafjord.Id, "Kaivuonon komuuni", "parallellnavn");
        await LeggTilNavneformAsync(db, kafjord.Id, "Kåfjord", "kortform");

        var tjeneste = new VirksomhetVisningsnavnTjeneste(db);

        Assert.Equal("Kåfjord kommune", await tjeneste.ForAsync(kafjord.Id));
        Assert.Equal("Kåfjord kommune", (await tjeneste.AlleAsync())[kafjord.Id]);
    }

    /// <summary>
    /// Ingen 'gjeldende'-navneform ⇒ <c>null</c>, slik at kalleren faller tilbake på
    /// <see cref="Virksomhet.Navn"/>. Dette er tilstanden for enhver rad ingen autoritativ kilde
    /// dekker — den skal vises med registerets form, ALDRI med et gjettet navn. Det var nettopp en
    /// slik gjetting (lowercase alt, hev første tegn) som skapte «Gaivuona suohkan kåfjord kommune
    /// kaivuonon komuuni» og som denne runden fjernet.
    /// </summary>
    [Fact]
    public async Task Gir_null_nar_ingen_gjeldende_navneform_finnes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await OpprettKafjordAsync(db);

        // Navneformer FINNES, men ingen av dem er den gjeldende formen.
        await LeggTilNavneformAsync(db, virksomhet.Id, "Gáivuona suohkan", "parallellnavn");
        await LeggTilNavneformAsync(db, virksomhet.Id, "Kåfjord", "kortform");
        await LeggTilNavneformAsync(db, virksomhet.Id, "Uspesifisert form", null);

        var tjeneste = new VirksomhetVisningsnavnTjeneste(db);

        Assert.Null(await tjeneste.ForAsync(virksomhet.Id));
        Assert.False((await tjeneste.AlleAsync()).ContainsKey(virksomhet.Id));
    }

    [Fact]
    public async Task Gir_null_for_en_virksomhet_helt_uten_navneformer()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await OpprettKafjordAsync(db);

        Assert.Null(await new VirksomhetVisningsnavnTjeneste(db).ForAsync(virksomhet.Id));
    }

    /// <summary>
    /// En ARKIVERT navneform teller ikke. Uten dette ville en navneform noen bevisst har fjernet
    /// fortsatt styrt hva hele appen viser — og den ville ikke vært synlig noe sted i UI-et, så feilen
    /// hadde vært svært vanskelig å forstå.
    /// </summary>
    [Fact]
    public async Task Arkivert_navneform_styrer_ikke_visningen()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await OpprettKafjordAsync(db);
        await LeggTilNavneformAsync(db, virksomhet.Id, "Gammelt visningsnavn", "gjeldende");

        var navneform = await db.Begreper.SingleAsync(
            b => b.VirksomhetReferanseId == virksomhet.Id && b.Term == "Gammelt visningsnavn");
        navneform.Entitetsstatus = "arkivert";
        await db.SaveChangesAsync();

        Assert.Null(await new VirksomhetVisningsnavnTjeneste(db).ForAsync(virksomhet.Id));
    }

    /// <summary>
    /// To 'gjeldende'-navneformer skal ikke forekomme (vokabularet håndhever det ikke), men skulle det
    /// skje, må valget være DETERMINISTISK — ellers ville etiketten hoppet mellom to navn fra kall til
    /// kall, avhengig av databasens radrekkefølge, og det ville vært nær umulig å feilsøke.
    /// </summary>
    [Fact]
    public async Task Velger_deterministisk_nar_to_gjeldende_navneformer_finnes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await OpprettKafjordAsync(db);
        await LeggTilNavneformAsync(db, virksomhet.Id, "Ø-navn", "gjeldende");
        await LeggTilNavneformAsync(db, virksomhet.Id, "A-navn", "gjeldende");

        var tjeneste = new VirksomhetVisningsnavnTjeneste(db);
        var forste = await tjeneste.ForAsync(virksomhet.Id);

        Assert.Equal("A-navn", forste);
        Assert.Equal(forste, await tjeneste.ForAsync(virksomhet.Id));
        Assert.Equal(forste, (await tjeneste.AlleAsync())[virksomhet.Id]);
    }

    /// <summary>
    /// 'parallellnavn' er lagringsbart hele veien ned i databasen — altså at CHECK-constrainten
    /// <c>ck_begreper_navneformgrunn</c> faktisk er utvidet av migrasjonen, og ikke bare i C#-settet.
    /// De to har drevet fra hverandre før (se <c>VirksomhetsbegrepTjeneste.Navneformgrunner</c> sin
    /// kommentar: «Endres den ene, må den andre endres i samme migrasjon»).
    /// </summary>
    [Fact]
    public async Task Parallellnavn_kan_lagres_i_databasen()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await OpprettKafjordAsync(db);

        await LeggTilNavneformAsync(db, virksomhet.Id, "Gáivuona suohkan", "parallellnavn");

        var lagret = await db.Begreper.SingleAsync(
            b => b.VirksomhetReferanseId == virksomhet.Id && b.Term == "Gáivuona suohkan");
        Assert.Equal("parallellnavn", lagret.Navneformgrunn);
    }
}
