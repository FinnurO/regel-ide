using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetNavnKasusBackfillTjeneste"/> + selve kasus-regelen i
/// <see cref="OrganisasjonsregisterSeed"/> mot ekte embedded Postgres — samme delte
/// DataTestCollection-database som resten av seed-testene i denne mappen.
///
/// <para>
/// MERK navnevalget på testradene: alle bruker en «Kasustest»-prefiks og et eget, syntetisk
/// organisasjonsnummer. Databasen er DELT mellom testklassene i denne collection-en, og
/// <see cref="OrganisasjonsregisterSeed"/> kjører i flere av dem — en testrad som het f.eks.
/// «Karasjoga gielda / karasjok kommune» kunne blitt matchet og mutert av seeden fra en annen
/// testklasse, eller omvendt forstyrret dens forventninger. De syntetiske navnene finnes ikke i
/// <c>organisasjoner-norge.json</c> og kan derfor ikke kollidere.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetNavnKasusBackfillTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetNavnKasusBackfillTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static Virksomhet NyVirksomhet(string navn, DateOnly? sistBrregSynkronisert = null) => new()
    {
        Id = Guid.NewGuid(),
        Navn = navn,
        SistBrregSynkronisert = sistBrregSynkronisert,
        Aktiv = false,
        OpprettetTidspunkt = DateTimeOffset.UtcNow,
    };

    // ── Selve regelen (ren funksjon, ingen database) ─────────────────────────────────────────────

    /// <summary>
    /// Kjernen i Del 2: begge ledd i et samisk dobbeltnavn skal få stor forbokstav. Dette er den
    /// eksakte kildestrengen fra <c>organisasjoner-norge.json</c>, ikke en omskrevet variant.
    /// </summary>
    [Fact]
    public void FormaterNavnEnkelt_gir_stor_forbokstav_i_BEGGE_ledd_av_et_dobbeltnavn()
    {
        Assert.Equal(
            "Karasjoga gielda / Karasjok kommune",
            OrganisasjonsregisterSeed.FormaterNavnEnkelt("KARASJOGA GIELDA / KARASJOK KOMMUNE"));
    }

    /// <summary>
    /// Regresjonsvernet mot en over-ivrig «tittelkasing»: en «og»-konnektor midt i et departementsnavn
    /// skal IKKE få stor bokstav. Dette navnet må stemme EKSAKT med Lovdatas "ministry"-felt for at
    /// departement-koblingen skal treffe (se <see cref="DepartementSeed"/>), så en regel som ga
    /// «Nærings- Og Fiskeridepartementet» ville brukket den koblingen — ikke bare sett stygg ut.
    /// </summary>
    [Theory]
    [InlineData("NÆRINGS- OG FISKERIDEPARTEMENTET", "Nærings- og fiskeridepartementet")]
    [InlineData("KLIMA- OG MILJØDEPARTEMENTET", "Klima- og miljødepartementet")]
    [InlineData("BARNE-, UNGDOMS- OG FAMILIEDIREKTORATET", "Barne-, ungdoms- og familiedirektoratet")]
    [InlineData("OSLO KOMMUNE", "Oslo kommune")]
    [InlineData("MATTILSYNET", "Mattilsynet")]
    public void FormaterNavnEnkelt_over_kapitaliserer_ikke_navn_uten_ledd_skille(string kilde, string forventet)
    {
        Assert.Equal(forventet, OrganisasjonsregisterSeed.FormaterNavnEnkelt(kilde));
    }

    /// <summary>
    /// Alle 7 dobbeltnavnene i kildefilen, slik at ingen av dem stille blir feil igjen. Verdiene er
    /// hentet ordrett fra <c>organisasjoner-norge.json</c>.
    /// </summary>
    [Theory]
    [InlineData("DEANU GIELDA / TANA KOMMUNE", "Deanu gielda / Tana kommune")]
    [InlineData("EVENES KOMMUNE / EVENÁSSI SUOHKAN", "Evenes kommune / Evenássi suohkan")]
    [InlineData("GUOVDAGEAINNU SUOHKAN / KAUTOKEINO KOMMUNE", "Guovdageainnu suohkan / Kautokeino kommune")]
    [InlineData("HARSTAD KOMMUNE / HÁRSTTÁID SUOHKAN", "Harstad kommune / Hársttáid suohkan")]
    [InlineData("KARASJOGA GIELDA / KARASJOK KOMMUNE", "Karasjoga gielda / Karasjok kommune")]
    [InlineData("SORTLAND KOMMUNE / SUORTTÁ SOUHKAN", "Sortland kommune / Suorttá souhkan")]
    [InlineData("UNJARGGA GIELDA / NESSEBY KOMMUNE", "Unjargga gielda / Nesseby kommune")]
    public void FormaterNavnEnkelt_dekker_alle_dobbeltnavnene_i_kildefilen(string kilde, string forventet)
    {
        Assert.Equal(forventet, OrganisasjonsregisterSeed.FormaterNavnEnkelt(kilde));
    }

    /// <summary>
    /// En bar <c>'/'</c> UTEN mellomrom er ikke et ledd-skille i denne kilden — regelen skal ikke
    /// finne på å kase noe der. (Ingen slik rad finnes i kildefilen i dag; testen låser avgrensningen.)
    /// </summary>
    [Fact]
    public void Bar_skrastrek_uten_mellomrom_er_ikke_et_ledd_skille()
    {
        Assert.Equal("A/b test", OrganisasjonsregisterSeed.FormaterNavnEnkelt("A/B TEST"));
    }

    [Fact]
    public void Tom_streng_gir_tom_streng()
    {
        Assert.Equal(string.Empty, OrganisasjonsregisterSeed.FormaterNavnEnkelt(string.Empty));
    }

    /// <summary>
    /// <see cref="OrganisasjonsregisterSeed.StorForbokstavPerLedd"/> skal ALDRI lowercase noe — den
    /// jobber på allerede lagrede navn, ikke på VERSAL-kildedata, og må derfor kunne kjøres på en rad
    /// noen har rettet manuelt uten å ødelegge rettelsen.
    /// </summary>
    [Fact]
    public void StorForbokstavPerLedd_lowercaser_ingenting()
    {
        Assert.Equal(
            "Karasjoga gielda / Karasjok kommune",
            OrganisasjonsregisterSeed.StorForbokstavPerLedd("Karasjoga gielda / karasjok kommune"));
        Assert.Equal(
            "SAMEDIGGI / SAMETINGET",
            OrganisasjonsregisterSeed.StorForbokstavPerLedd("SAMEDIGGI / SAMETINGET"));
    }

    // ── Tilbakefyllingen (mot database) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Retter_andre_ledd_pa_en_seedet_rad()
    {
        await using var db = _fixture.NyDbContext();
        var rad = NyVirksomhet("Kasustest gielda / kasustest kommune");
        db.Virksomheter.Add(rad);
        await db.SaveChangesAsync();

        var rettet = await VirksomhetNavnKasusBackfillTjeneste.KjorAsync(db);

        var min = Assert.Single(rettet, r => r.VirksomhetId == rad.Id);
        Assert.Equal("Kasustest gielda / kasustest kommune", min.Fra);
        Assert.Equal("Kasustest gielda / Kasustest kommune", min.Til);
        var lagret = await db.Virksomheter.SingleAsync(v => v.Id == rad.Id);
        Assert.Equal("Kasustest gielda / Kasustest kommune", lagret.Navn);
    }

    /// <summary>Kriterium 5: en Brreg-synkronisert rad skal aldri røres, jf. issue #158.</summary>
    [Fact]
    public async Task Rorer_ikke_en_Brreg_synkronisert_rad()
    {
        await using var db = _fixture.NyDbContext();
        // Samme FORM som den reelle «SAMEDIGGI / SAMETINGET»-raden, men med små bokstaver i andre ledd,
        // slik at raden ville vært en opplagt kandidat om det ikke var for Brreg-filteret. Uten den
        // detaljen ville testen bestått selv med filteret fjernet (kasus-regelen alene hadde latt en
        // VERSAL-rad stå), og dermed ikke bevist noe.
        var brregRad = NyVirksomhet("Kasustest brreg / kasustest sameting", new DateOnly(2026, 8, 29));
        db.Virksomheter.Add(brregRad);
        await db.SaveChangesAsync();

        var rettet = await VirksomhetNavnKasusBackfillTjeneste.KjorAsync(db);

        Assert.DoesNotContain(rettet, r => r.VirksomhetId == brregRad.Id);
        var lagret = await db.Virksomheter.SingleAsync(v => v.Id == brregRad.Id);
        Assert.Equal("Kasustest brreg / kasustest sameting", lagret.Navn); // ordrett uendret.
    }

    [Fact]
    public async Task Rorer_ikke_en_rad_uten_ledd_skille()
    {
        await using var db = _fixture.NyDbContext();
        // Et departementsnavn er den farlige varianten: blir det over-kapitalisert, brekker
        // departement-koblingen (se FormaterNavnEnkelt-testene over).
        var rad = NyVirksomhet("Kasustest nærings- og fiskeridepartementet");
        db.Virksomheter.Add(rad);
        await db.SaveChangesAsync();

        var rettet = await VirksomhetNavnKasusBackfillTjeneste.KjorAsync(db);

        Assert.DoesNotContain(rettet, r => r.VirksomhetId == rad.Id);
        var lagret = await db.Virksomheter.SingleAsync(v => v.Id == rad.Id);
        Assert.Equal("Kasustest nærings- og fiskeridepartementet", lagret.Navn);
    }

    [Fact]
    public async Task Er_idempotent_andre_kjoring_finner_ingenting_a_gjore()
    {
        await using var db = _fixture.NyDbContext();
        var rad = NyVirksomhet("Kasustest idempotens / kasustest ledd");
        db.Virksomheter.Add(rad);
        await db.SaveChangesAsync();

        var forsteKjoring = await VirksomhetNavnKasusBackfillTjeneste.KjorAsync(db);
        var andreKjoring = await VirksomhetNavnKasusBackfillTjeneste.KjorAsync(db);

        Assert.Contains(forsteKjoring, r => r.VirksomhetId == rad.Id);
        Assert.DoesNotContain(andreKjoring, r => r.VirksomhetId == rad.Id);
        var lagret = await db.Virksomheter.SingleAsync(v => v.Id == rad.Id);
        Assert.Equal("Kasustest idempotens / Kasustest ledd", lagret.Navn);
    }

    /// <summary>
    /// En rad som allerede er riktig kaset skal ikke telles som «rettet» — ellers ville
    /// oppstartsloggen påstått en endring som ikke skjedde, hver eneste oppstart.
    /// </summary>
    [Fact]
    public async Task Rorer_ikke_en_rad_som_allerede_har_riktig_kasus()
    {
        await using var db = _fixture.NyDbContext();
        var rad = NyVirksomhet("Kasustest riktig / Kasustest allerede");
        db.Virksomheter.Add(rad);
        await db.SaveChangesAsync();

        var rettet = await VirksomhetNavnKasusBackfillTjeneste.KjorAsync(db);

        Assert.DoesNotContain(rettet, r => r.VirksomhetId == rad.Id);
    }
}
