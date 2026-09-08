using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="GruppeMedlemskapTjeneste"/> (issue #164, «gruppe av gruppe») mot ekte embedded Postgres —
/// kobler ett gruppebegrep til et annet som MEDLEM, hjemlet i en forskrift. Speiler bevisst
/// <see cref="MyndighetstildelingTjenesteTests"/> i form, fordi tjenestene er de to nivåene i samme
/// hierarki; det som IKKE finnes i den analogen er sykelvalideringen, og den er derfor
/// tyngdepunktet her.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class GruppeMedlemskapTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public GruppeMedlemskapTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // Alkoholloven importeres idempotent (samme ELI) og deler derfor SAMME rad på tvers av alle tester
    // i denne delte DataTestCollection-databasen — hvert gruppebegrep må derfor ha en term som er unik
    // per test, ellers velter ux_begreper_gruppebegrep_term_lovkilde. Samme begrunnelse og samme
    // hjelper som NyTerm() i MyndighetstildelingTjenesteTests.
    private static string NyTerm(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    /// <summary>Loven gruppebegrepene hjemles i, forskriften medlemskapene hjemles i, og to ekte
    /// paragraf-eId-er fra loven å bygge paragrafspenn av — samme oppsett hver test starter fra.</summary>
    private static async Task<Oppsett> NyttOppsettAsync(RegelIdeDbContext db)
    {
        var importer = new RettskildeImportTjeneste(db);
        var lovId = await importer.ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var forskriftId = await importer.ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 22)));
        var paragrafer = await db.RettskildeNoder
            .Where(n => n.RettskildeId == lovId && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge)
            .Take(2)
            .ToListAsync();
        return new Oppsett(lovId, forskriftId, paragrafer[0].Eid, paragrafer[1].Eid);
    }

    private sealed record Oppsett(Guid LovId, Guid ForskriftId, string FraEid, string TilEid);

    private static Task<BegrepEntitet> NyGruppeAsync(RegelIdeDbContext db, Guid lovId, string prefiks) =>
        new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(lovId, NyTerm(prefiks), "Kari Jurist");

    [Fact]
    public async Task Oppretter_gruppemedlemskap_med_strukturert_paragrafspenn()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var overordnet = await NyGruppeAsync(db, oppsett.LovId, "forvaltningsomradet");
        var underordnet = await NyGruppeAsync(db, oppsett.LovId, "sprakutviklingskommuner");

        var register = new GruppeMedlemskapTjeneste(db);
        var medlemskap = await register.OpprettAsync(
            overordnet.Id, underordnet.Id, oppsett.ForskriftId,
            [new ParagrafspennPar(oppsett.FraEid, oppsett.TilEid)], "Kari Jurist");

        var lest = GruppeMedlemskapTjeneste.LesParagrafspenn(medlemskap);
        Assert.Single(lest);
        Assert.Equal(oppsett.FraEid, lest[0].FraEid);
        Assert.Equal(oppsett.TilEid, lest[0].TilEid);
        Assert.Equal(oppsett.ForskriftId, medlemskap.HjemmelRettskildeId);

        // Retningen er hele poenget: den underordnede gruppen skal dukke opp som MEDLEMSGRUPPE av den
        // overordnede, og den overordnede som «medlem av» sett fra den underordnede — aldri motsatt.
        var medlemsgrupper = await register.MedlemsgrupperForAsync(overordnet.Id);
        Assert.Single(medlemsgrupper);
        Assert.Equal(underordnet.Id, medlemsgrupper[0].UnderordnetGruppeBegrepId);

        var overordnede = await register.OverordnedeGrupperForAsync(underordnet.Id);
        Assert.Single(overordnede);
        Assert.Equal(overordnet.Id, overordnede[0].OverordnetGruppeBegrepId);

        Assert.Empty(await register.MedlemsgrupperForAsync(underordnet.Id));
        Assert.Empty(await register.OverordnedeGrupperForAsync(overordnet.Id));
    }

    [Fact]
    public async Task Selv_medlemskap_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var gruppe = await NyGruppeAsync(db, oppsett.LovId, "gruppe-selv");

        var register = new GruppeMedlemskapTjeneste(db);
        var feil = await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            gruppe.Id, gruppe.Id, oppsett.ForskriftId,
            [new ParagrafspennPar(oppsett.FraEid, null)], "Kari Jurist"));
        Assert.Contains("kan ikke være medlem av seg selv", feil.Message);
    }

    /// <summary>
    /// Hovedkravet i issue #164: A→B og B→C finnes, og C→A skal avvises. Testen holder IKKE med at det
    /// kastes — feilmeldingen må navngi den faktiske kjeden med gruppenes TERMER, for det er den
    /// eneste opplysningen som gjør at en saksbehandler ser HVILKEN registrering som må rettes.
    /// </summary>
    [Fact]
    public async Task Sirkulaer_kjede_kastes_og_feilmeldingen_navngir_kjeden()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var a = await NyGruppeAsync(db, oppsett.LovId, "sykel-a");
        var b = await NyGruppeAsync(db, oppsett.LovId, "sykel-b");
        var c = await NyGruppeAsync(db, oppsett.LovId, "sykel-c");
        ParagrafspennPar[] spenn = [new ParagrafspennPar(oppsett.FraEid, null)];

        var register = new GruppeMedlemskapTjeneste(db);
        await register.OpprettAsync(a.Id, b.Id, oppsett.ForskriftId, spenn, "Kari Jurist");
        await register.OpprettAsync(b.Id, c.Id, oppsett.ForskriftId, spenn, "Kari Jurist");

        var feil = await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            c.Id, a.Id, oppsett.ForskriftId, spenn, "Kari Jurist"));

        Assert.Contains($"«{a.Term}» → «{b.Term}» → «{c.Term}»", feil.Message);
        Assert.Contains("sirkulær kjede", feil.Message);

        // Ingen halvskrevet rad: avvisningen skjer FØR noe legges inn.
        Assert.Empty(await register.MedlemsgrupperForAsync(c.Id));
    }

    /// <summary>Den korteste sykelen — A→B finnes, B→A avvises. Egen test fordi traverseringen da
    /// treffer målet på FØRSTE steg (ingen mellomledd), en annen kodesti enn den lange kjeden over.</summary>
    [Fact]
    public async Task Gjensidig_medlemskap_mellom_to_grupper_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var a = await NyGruppeAsync(db, oppsett.LovId, "toveis-a");
        var b = await NyGruppeAsync(db, oppsett.LovId, "toveis-b");
        ParagrafspennPar[] spenn = [new ParagrafspennPar(oppsett.FraEid, null)];

        var register = new GruppeMedlemskapTjeneste(db);
        await register.OpprettAsync(a.Id, b.Id, oppsett.ForskriftId, spenn, "Kari Jurist");

        var feil = await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            b.Id, a.Id, oppsett.ForskriftId, spenn, "Kari Jurist"));

        Assert.Contains($"«{a.Term}» → «{b.Term}»", feil.Message);
    }

    /// <summary>
    /// Regresjonsvern for <c>besokt</c>-mengden i traverseringen: grafen er en DAG, ikke et tre — en
    /// gruppe kan lovlig være medlem av FLERE grupper. Diamanten A→B, A→C, B→D, C→D når D langs to
    /// veier, og en traversering som tolket «samme node nådd to ganger» som en sykel ville avvist en
    /// helt legitim registrering. E→A tvinger traverseringen gjennom hele diamanten.
    /// </summary>
    [Fact]
    public async Task Diamant_er_ingen_sykel_en_gruppe_kan_vaere_medlem_av_flere_grupper()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var a = await NyGruppeAsync(db, oppsett.LovId, "diamant-a");
        var b = await NyGruppeAsync(db, oppsett.LovId, "diamant-b");
        var c = await NyGruppeAsync(db, oppsett.LovId, "diamant-c");
        var d = await NyGruppeAsync(db, oppsett.LovId, "diamant-d");
        var e = await NyGruppeAsync(db, oppsett.LovId, "diamant-e");
        ParagrafspennPar[] spenn = [new ParagrafspennPar(oppsett.FraEid, null)];

        var register = new GruppeMedlemskapTjeneste(db);
        await register.OpprettAsync(a.Id, b.Id, oppsett.ForskriftId, spenn, "Kari Jurist");
        await register.OpprettAsync(a.Id, c.Id, oppsett.ForskriftId, spenn, "Kari Jurist");
        await register.OpprettAsync(b.Id, d.Id, oppsett.ForskriftId, spenn, "Kari Jurist");
        await register.OpprettAsync(c.Id, d.Id, oppsett.ForskriftId, spenn, "Kari Jurist");

        // Traverserer nedover fra A og møter D via både B og C — skal ikke avvises.
        await register.OpprettAsync(e.Id, a.Id, oppsett.ForskriftId, spenn, "Kari Jurist");

        Assert.Equal(2, (await register.MedlemsgrupperForAsync(a.Id)).Count);
        Assert.Equal(2, (await register.OverordnedeGrupperForAsync(d.Id)).Count);
        Assert.Single(await register.OverordnedeGrupperForAsync(a.Id));
    }

    /// <summary>
    /// Medlemskapet mellom to grupper er ÉN opplysning, uansett hvor mange hjemler som gjentar den —
    /// derfor er den unike indeksen lagt på PARET, ikke på paret + hjemmel, og gjentatt kall returnerer
    /// den eksisterende raden i stedet for å velte på en ufanget <c>DbUpdateException</c>. Dette er det
    /// <see cref="SamiskSprakforvaltningSeed"/> hviler på for å kunne kjøres om igjen.
    /// </summary>
    [Fact]
    public async Task Idempotent_ved_gjentatt_kall_selv_med_annen_hjemmel()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var overordnet = await NyGruppeAsync(db, oppsett.LovId, "idempotent-over");
        var underordnet = await NyGruppeAsync(db, oppsett.LovId, "idempotent-under");

        var register = new GruppeMedlemskapTjeneste(db);
        var forste = await register.OpprettAsync(
            overordnet.Id, underordnet.Id, oppsett.ForskriftId,
            [new ParagrafspennPar(oppsett.FraEid, null)], "Kari Jurist");

        // Samme par, men LOVEN som hjemmel i stedet for forskriften: fortsatt samme opplysning.
        var andre = await register.OpprettAsync(
            overordnet.Id, underordnet.Id, oppsett.LovId,
            [new ParagrafspennPar(oppsett.TilEid, null)], "Ola Saksbehandler");

        Assert.Equal(forste.Id, andre.Id);
        Assert.Equal(oppsett.ForskriftId, andre.HjemmelRettskildeId); // den første registreringen står urørt.
        Assert.Equal("Kari Jurist", andre.OpprettetAv);
        Assert.Single(await register.MedlemsgrupperForAsync(overordnet.Id));
        Assert.Equal(1, await db.GruppeMedlemskap.CountAsync(
            m => m.OverordnetGruppeBegrepId == overordnet.Id && m.UnderordnetGruppeBegrepId == underordnet.Id));
    }

    [Fact]
    public async Task Tomt_paragrafspenn_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var overordnet = await NyGruppeAsync(db, oppsett.LovId, "tomt-over");
        var underordnet = await NyGruppeAsync(db, oppsett.LovId, "tomt-under");

        var register = new GruppeMedlemskapTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            overordnet.Id, underordnet.Id, oppsett.ForskriftId, [], "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_eid_i_paragrafspenn_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var overordnet = await NyGruppeAsync(db, oppsett.LovId, "ukjent-eid-over");
        var underordnet = await NyGruppeAsync(db, oppsett.LovId, "ukjent-eid-under");

        var register = new GruppeMedlemskapTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            overordnet.Id, underordnet.Id, oppsett.ForskriftId,
            [new ParagrafspennPar("https://ukjent/eid", null)], "Kari Jurist"));

        // Også TilEid-siden av paret valideres, ikke bare FraEid.
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            overordnet.Id, underordnet.Id, oppsett.ForskriftId,
            [new ParagrafspennPar(oppsett.FraEid, "https://ukjent/eid")], "Kari Jurist"));
    }

    /// <summary>Et begrep som ikke finnes, og et som finnes men IKKE er et gruppebegrep (en navneform
    /// for en virksomhet), skal behandles likt — begge er «ingen gruppe med den id-en», ingen gjettet
    /// fallback til å behandle navneformen som en gruppe.</summary>
    [Fact]
    public async Task Ukjent_eller_ikke_gruppe_begrep_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var gruppe = await NyGruppeAsync(db, oppsett.LovId, "ikke-gruppe-over");
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhet.Id, NyTerm("navneform"), "Kari Jurist");
        ParagrafspennPar[] spenn = [new ParagrafspennPar(oppsett.FraEid, null)];

        var register = new GruppeMedlemskapTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            Guid.NewGuid(), gruppe.Id, oppsett.ForskriftId, spenn, "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            gruppe.Id, Guid.NewGuid(), oppsett.ForskriftId, spenn, "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            navneform.Id, gruppe.Id, oppsett.ForskriftId, spenn, "Kari Jurist"));
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            gruppe.Id, navneform.Id, oppsett.ForskriftId, spenn, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_hjemmel_rettskilde_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var overordnet = await NyGruppeAsync(db, oppsett.LovId, "ukjent-hjemmel-over");
        var underordnet = await NyGruppeAsync(db, oppsett.LovId, "ukjent-hjemmel-under");

        var register = new GruppeMedlemskapTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            overordnet.Id, underordnet.Id, Guid.NewGuid(),
            [new ParagrafspennPar(oppsett.FraEid, null)], "Kari Jurist"));
    }

    [Fact]
    public async Task GyldigFra_etter_gyldigTil_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var overordnet = await NyGruppeAsync(db, oppsett.LovId, "gyldighet-over");
        var underordnet = await NyGruppeAsync(db, oppsett.LovId, "gyldighet-under");

        var register = new GruppeMedlemskapTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            overordnet.Id, underordnet.Id, oppsett.ForskriftId,
            [new ParagrafspennPar(oppsett.FraEid, null)], "Kari Jurist",
            gyldigFra: new DateOnly(2026, 1, 1), gyldigTil: new DateOnly(2025, 12, 31)));
    }

    /// <summary>Proveniens er ikke pynt: en registrering som ikke etterlater sporet «hvem registrerte
    /// dette, og når» er ikke etterprøvbar. Samme <c>EntitetType</c>-streng som tabellnavnet, slik
    /// resten av kodebasen gjør det.</summary>
    [Fact]
    public async Task Skriver_proveniensrad_ved_opprettelse()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await NyttOppsettAsync(db);
        var overordnet = await NyGruppeAsync(db, oppsett.LovId, "proveniens-over");
        var underordnet = await NyGruppeAsync(db, oppsett.LovId, "proveniens-under");

        var medlemskap = await new GruppeMedlemskapTjeneste(db).OpprettAsync(
            overordnet.Id, underordnet.Id, oppsett.ForskriftId,
            [new ParagrafspennPar(oppsett.FraEid, null)], "Kari Jurist");

        var rad = await db.Proveniens.SingleAsync(
            p => p.EntitetType == "gruppe_medlemskap" && p.EntitetId == medlemskap.Id);
        Assert.Equal("opprettet", rad.Handling);
        Assert.Equal("Kari Jurist", rad.EndretAv);
        Assert.Null(rad.VirksomhetId); // delt/nasjonal opplysning, ikke én virksomhets arbeidsprodukt.
    }
}
