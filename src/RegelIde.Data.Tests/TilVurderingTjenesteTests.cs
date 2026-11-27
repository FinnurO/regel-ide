using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #517 «Til vurdering», 2026-11-27] Tellingene til startsiden, formulert som spørsmålene i akseptansekriteriene: er tallet det
/// samme som køen viser (AK 1), står bare det som har en knapp i lista (AK 4), og forsvinner et element når det er vurdert?
/// Den delte testbasen har data fra andre tester, så testene måler endringer og egne rader, ikke totaler.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class TilVurderingTjenesteTests(EmbeddedPostgresFixture fixture)
{
    private static TilVurderingTjeneste Tjeneste(RegelIdeDbContext db) =>
        new(db, new AktorkoTjeneste(db), new AktorkoRelasjonTjeneste(db, new StrukturkantTjeneste(db)), new FunnTjeneste(db));

    private static string Unik(string s) => $"{s} {Guid.NewGuid():N}";

    /// <summary>En nedlagt aktør med en utgått navneform (forslag), en etterfølger og en etterfølgerkant (manuelt foreslått), og et funn som
    /// peker på begge.</summary>
    private static async Task<(Virksomhet Aktor, BegrepEntitet Navneform, StrukturkantEntitet Kant, FunnEntitet Funn)> UtgattAsync(
        RegelIdeDbContext db, string? godkjenning = Strukturkanter.MaskineltForeslatt)
    {
        var oppsett = new UtgattAktorFunnregelTests.Oppsett(db);
        var (gammel, _) = await oppsett.AktorAsync(UtgattAktorFunnregelTests.Departement("Gammel"), nedlagt: true);
        var (ny, _) = await oppsett.AktorAsync(UtgattAktorFunnregelTests.Departement("Ny"), nedlagt: false, godkjenning: Strukturkanter.Godkjent);
        var utgatt = UtgattAktorFunnregelTests.Oppsett.NyNavneform(ny.Id, UtgattAktorFunnregelTests.Departement("Utgatt"), "utgatt", godkjenning);
        db.Begreper.Add(utgatt);
        await db.SaveChangesAsync();
        var kant = await oppsett.EtterfolgerAsync(ny.Id, gammel.Id, "manuell");
        kant.Status = Strukturkanter.ManueltForeslatt;
        await db.SaveChangesAsync();
        var (funn, _) = await new FunnTjeneste(db).OpprettAsync(new NyttFunn(Funn.UtgattAktor, "utgatt_navn", Unik("Utgått navn"), "Test.", "test",
            Koblinger: [new NyKobling(Funn.Navneform, utgatt.Term, BegrepId: utgatt.Id), new NyKobling(Funn.Etterfolger, "etterfølger", StrukturkantId: kant.Id)]), "test");
        return (ny, utgatt, kant, funn);
    }

    [Fact]
    public async Task AK1_tallene_for_navneformer_og_etterfolgere_er_de_samme_som_premissporten_gir()
    {
        await using var db = fixture.NyDbContext();
        await UtgattAsync(db);
        await UtgattAsync(db, godkjenning: null);

        var tv = await Tjeneste(db).HentAsync();

        var oppdaget = await db.Funn.Include(f => f.Koblinger).Include(f => f.Vurderingspunkter)
            .Where(f => f.Kategori == Funn.UtgattAktor && f.Status == Funn.Oppdaget).ToListAsync();
        var mangler = await new FunnTjeneste(db).PremissmanglerForAsync(oppdaget);
        Assert.Equal(oppdaget.Count, tv.UtgattAktor.Oppdaget);
        Assert.Equal(oppdaget.Count(f => mangler[f.Id].Any(TilVurderingTjeneste.ErNavneformmangel)), tv.Departementer.FunnMedNavneformmangel);
        Assert.Equal(oppdaget.Count(f => mangler[f.Id].Any(TilVurderingTjeneste.ErEtterfolgermangel)), tv.Etterfolgere.FunnMedEtterfolgermangel);
        // Den kumulative lista teller hvert funn én gang, og ender på alle funnene som lista holder igjen.
        Assert.Equal(tv.Departementer.Aktorer.Count, tv.Departementer.FunnKumulativt.Count);
        Assert.True(tv.Departementer.FunnKumulativt.Zip(tv.Departementer.FunnKumulativt.Skip(1)).All(p => p.First <= p.Second));
        Assert.True(tv.Departementer.FunnKumulativt.Last() <= tv.Departementer.FunnMedNavneformmangel);
        Assert.Equal(tv.Departementer.Aktorer[0].Funn, tv.Departementer.FunnKumulativt[0]);
        Assert.Equal(await db.Strukturkanter.CountAsync(k => k.Typekode == "etterfolger"
            && (k.Status == Strukturkanter.MaskineltForeslatt || k.Status == Strukturkanter.ManueltForeslatt)), tv.Etterfolgere.Forslag);
    }

    [Fact]
    public async Task AK4_lista_har_bare_navneformer_med_knapp_og_aktoren_forsvinner_naar_navnet_er_vurdert()
    {
        await using var db = fixture.NyDbContext();
        var (aktor, navneform, kant, _) = await UtgattAsync(db);
        var (utenKnapp, _, _, _) = await UtgattAsync(db, godkjenning: null);

        var for_ = await Tjeneste(db).HentAsync();
        var rad = Assert.Single(for_.Departementer.Aktorer, a => a.VirksomhetId == aktor.Id);
        Assert.Equal(1, rad.Funn);
        Assert.Equal([navneform.Term], rad.Navneformer.Select(n => n.Term));
        Assert.Equal("utgatt", rad.Navneformer[0].Navneformgrunn);
        // En navneform uten godkjenning har ingen knapp på aktørsiden: den holder igjen funnet, men står ikke i lista.
        Assert.DoesNotContain(for_.Departementer.Aktorer, a => a.VirksomhetId == utenKnapp.Id);

        navneform.Godkjenning = Strukturkanter.FagligVurdert;
        kant.Status = Strukturkanter.Godkjent;
        await db.SaveChangesAsync();
        var etter = await Tjeneste(db).HentAsync();
        Assert.DoesNotContain(etter.Departementer.Aktorer, a => a.VirksomhetId == aktor.Id);
        Assert.Equal(for_.Departementer.FunnMedNavneformmangel - 1, etter.Departementer.FunnMedNavneformmangel);
        Assert.Equal(for_.Etterfolgere.FunnMedEtterfolgermangel - 1, etter.Etterfolgere.FunnMedEtterfolgermangel);
        Assert.Equal(for_.Etterfolgere.FunnManuelle - 1, etter.Etterfolgere.FunnManuelle);
        Assert.Equal(for_.Etterfolgere.Manuelle - 1, etter.Etterfolgere.Manuelle);
    }

    [Fact]
    public async Task Klasseforslag_kildefeil_og_forekomster_teller_bare_det_som_venter()
    {
        await using var db = fixture.NyDbContext();
        var for_ = await Tjeneste(db).HentAsync();

        var klasse = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "klasse", Term = Unik("testklasse"), Status = "utkast", OpprettetAv = "test",
            Godkjenning = Strukturkanter.ManueltForeslatt,
        };
        var godkjentKlasse = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "klasse", Term = Unik("testklasse"), Status = "utkast", OpprettetAv = "test",
            Godkjenning = Strukturkanter.Godkjent,
        };
        db.Begreper.AddRange(klasse, godkjentKlasse);
        var rettskilde = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskilde, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse", Tittel = Unik("Testforskrift"),
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.Kildefeil.Add(new KildefeilEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskilde, Type = "opphevet_av_annen_kilde", Beskrivelse = "test", FunnetAvMekanisme = "test",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.Kildefeil.Add(new KildefeilEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskilde, Type = "opphevet_av_annen_kilde", Beskrivelse = "test", FunnetAvMekanisme = "test",
            Status = "Avvist", StatusEndretAv = "test", StatusEndretTidspunkt = DateTimeOffset.UtcNow, Statusbegrunnelse = "test",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var etter = await Tjeneste(db).HentAsync();
        Assert.Equal(for_.Klasseforslag + 1, etter.Klasseforslag);
        Assert.Equal(for_.Kildefeil.Ny + 1, etter.Kildefeil.Ny);
        Assert.Equal(for_.Kildefeil.PerType.GetValueOrDefault("opphevet_av_annen_kilde") + 1, etter.Kildefeil.PerType["opphevet_av_annen_kilde"]);
        Assert.Equal(await db.Strukturkanter.CountAsync(k => Strukturkanter.Ventende.Contains(k.Status)), etter.ForslagskoTotalt);
    }

    [Theory]
    [InlineData("Navneformen «Miljøverndepartementet» er ikke faglig vurdert (maskinelt foreslått).", true, false)]
    [InlineData("Navneformen «X» finnes ikke lenger.", false, false)]
    [InlineData("Etterfølger ikke vurdert: «Klima- og miljødepartementet» (manuelt foreslått).", false, true)]
    [InlineData("Etterfølger ikke vurdert: «X» (kanten finnes ikke lenger).", false, false)]
    [InlineData("Etterfølger ikke vurdert: funnet peker ikke på noen etterfølgerkant, og vurderingen har ikke noe punkt «Etterfølger».", false, false)]
    public void Manglene_som_rettes_i_en_ko_skilles_fra_de_andre(string mangel, bool navneform, bool etterfolger)
    {
        Assert.Equal(navneform, TilVurderingTjeneste.ErNavneformmangel(mangel));
        Assert.Equal(etterfolger, TilVurderingTjeneste.ErEtterfolgermangel(mangel));
    }
}
