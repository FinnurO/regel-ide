using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="AnsvarligDepartementBackfillTjeneste"/> mot ekte embedded Postgres — samme delte
/// DataTestCollection-database som resten av seed-testene i denne mappen.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AnsvarligDepartementBackfillTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AnsvarligDepartementBackfillTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static RettskildeEntitet NyRettskilde(string tittel, string? aknXml, string? ansvarligDepartement = null) => new()
    {
        Id = Guid.NewGuid(),
        Doctype = "act",
        Kildetype = "Lov",
        Tittel = tittel,
        AknXml = aknXml,
        Status = "Gjeldende",
        AnsvarligDepartement = ansvarligDepartement,
        OpprettetAv = "test",
        OpprettetTidspunkt = DateTimeOffset.UtcNow,
    };

    private const string AknXmlMedDepartement =
        "<akomaNtoso xmlns=\"http://docs.oasis-open.org/legaldocml/ns/akn/3.0\" xmlns:regelIde=\"https://regel-ide.no/ns/akn-utvidelse/1.0\">" +
        "<act name=\"lov\"><meta><proprietary source=\"#regel-ide\">" +
        "<regelIde:eli>https://lovdata.no/eli/lov/2026/01/01/1/nor</regelIde:eli>" +
        "<regelIde:ansvarligDepartement>Klima- og miljødepartementet</regelIde:ansvarligDepartement>" +
        "</proprietary></meta></act></akomaNtoso>";

    private const string AknXmlUtenDepartementElement =
        "<akomaNtoso xmlns=\"http://docs.oasis-open.org/legaldocml/ns/akn/3.0\" xmlns:regelIde=\"https://regel-ide.no/ns/akn-utvidelse/1.0\">" +
        "<act name=\"lov\"><meta><proprietary source=\"#regel-ide\">" +
        "<regelIde:eli>https://lovdata.no/eli/lov/2026/01/01/2/nor</regelIde:eli>" +
        "</proprietary></meta></act></akomaNtoso>"; // gammel XML skrevet FØR AknXmlSkriver fikk elementet.

    [Fact]
    public async Task Tilbakefyller_fra_ansvarligDepartement_element_i_lagret_AknXml()
    {
        await using var db = _fixture.NyDbContext();
        var rettskilde = NyRettskilde("En lov med departement i AKN-XML", AknXmlMedDepartement);
        db.Rettskilder.Add(rettskilde);
        await db.SaveChangesAsync();

        var antall = await AnsvarligDepartementBackfillTjeneste.KjorAsync(db);

        Assert.Equal(1, antall);
        var lagret = await db.Rettskilder.SingleAsync(r => r.Id == rettskilde.Id);
        Assert.Equal("Klima- og miljødepartementet", lagret.AnsvarligDepartement);
    }

    [Fact]
    public async Task Rad_uten_elementet_i_AknXml_forblir_null_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var rettskilde = NyRettskilde("En lov uten departement-element i AKN-XML", AknXmlUtenDepartementElement);
        db.Rettskilder.Add(rettskilde);
        await db.SaveChangesAsync();

        var antall = await AnsvarligDepartementBackfillTjeneste.KjorAsync(db);

        Assert.Equal(0, antall);
        var lagret = await db.Rettskilder.SingleAsync(r => r.Id == rettskilde.Id);
        Assert.Null(lagret.AnsvarligDepartement);
    }

    [Fact]
    public async Task Referanse_stubb_uten_AknXml_i_det_hele_tatt_forblir_null()
    {
        await using var db = _fixture.NyDbContext();
        var stubb = NyRettskilde("Referanse-stubb uten egen AKN-XML", aknXml: null);
        stubb.Importrolle = "referanse"; // ck_rettskilder_akn_xml krever dette når AknXml er NULL.
        db.Rettskilder.Add(stubb);
        await db.SaveChangesAsync();

        var antall = await AnsvarligDepartementBackfillTjeneste.KjorAsync(db);

        Assert.Equal(0, antall);
        var lagret = await db.Rettskilder.SingleAsync(r => r.Id == stubb.Id);
        Assert.Null(lagret.AnsvarligDepartement);
    }

    [Fact]
    public async Task Rorer_ikke_rad_som_allerede_har_verdien_satt()
    {
        await using var db = _fixture.NyDbContext();
        // AKN-XML-en her sier noe ANNET enn den allerede satte kolonneverdien — beviser at raden ikke
        // engang blir sett på (WHERE AnsvarligDepartement IS NULL), ikke bare at verdien tilfeldigvis
        // stemmer overens.
        var alleredeSatt = NyRettskilde(
            "Allerede tilbakefylt/importert rad", AknXmlMedDepartement, ansvarligDepartement: "Et helt annet departement");
        db.Rettskilder.Add(alleredeSatt);
        await db.SaveChangesAsync();

        var antall = await AnsvarligDepartementBackfillTjeneste.KjorAsync(db);

        Assert.Equal(0, antall);
        var lagret = await db.Rettskilder.SingleAsync(r => r.Id == alleredeSatt.Id);
        Assert.Equal("Et helt annet departement", lagret.AnsvarligDepartement);
    }

    [Fact]
    public async Task Kjoring_to_ganger_pa_rad_endrer_ikke_allerede_tilbakefylte_rader()
    {
        await using var db = _fixture.NyDbContext();
        var rettskilde = NyRettskilde("En lov med departement i AKN-XML", AknXmlMedDepartement);
        db.Rettskilder.Add(rettskilde);
        await db.SaveChangesAsync();

        var forsteKjoring = await AnsvarligDepartementBackfillTjeneste.KjorAsync(db);
        var andreKjoring = await AnsvarligDepartementBackfillTjeneste.KjorAsync(db);

        Assert.Equal(1, forsteKjoring);
        Assert.Equal(0, andreKjoring); // raden er allerede fylt ut, matcher ikke lenger WHERE-filteret.
        var lagret = await db.Rettskilder.SingleAsync(r => r.Id == rettskilde.Id);
        Assert.Equal("Klima- og miljødepartementet", lagret.AnsvarligDepartement);
    }
}
