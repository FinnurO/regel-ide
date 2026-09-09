using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetRelasjonregisterTjeneste"/> (docs/28, docs/29 §Del C) mot ekte embedded Postgres.
/// Merk: <see cref="RelasjonsTypeKonfigurasjonEntitet"/> seedes normalt ved API-oppstart (Program.cs) —
/// denne fixturen kjører KUN migrasjoner, ingen seed, så hver test som trenger en gyldig relasjonstype
/// setter den selv opp via <see cref="NyRelasjonsTypeAsync"/>.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetRelasjonregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetRelasjonregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn = "Testkommunen")
    {
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"{navn}-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        return virksomhet.Id;
    }

    /// <summary>Samme som <see cref="NyVirksomhetAsync"/>, men returnerer også det faktiske (unike, Guid-
    /// suffikserte) navnet — nødvendig når testen selv skal assertere på visningstekst som inneholder navnet.</summary>
    private static async Task<(Guid Id, string Navn)> NyVirksomhetMedNavnAsync(RegelIdeDbContext db, string navn)
    {
        var unikNavn = $"{navn}-{Guid.NewGuid():N}";
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = unikNavn };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        return (virksomhet.Id, unikNavn);
    }

    private static async Task<string> NyRelasjonsTypeAsync(
        RegelIdeDbContext db, string? kode = null, string fraMal = "er underlagt {0}", string tilMal = "er eier/overordnet for {0}", bool aktiv = true)
    {
        kode ??= $"type-{Guid.NewGuid():N}";
        db.RelasjonsTypeKonfigurasjoner.Add(new RelasjonsTypeKonfigurasjonEntitet
        {
            Id = Guid.NewGuid(), Kode = kode, FraVisningsmal = fraMal, TilVisningsmal = tilMal, Aktiv = aktiv,
        });
        await db.SaveChangesAsync();
        return kode;
    }

    [Fact]
    public async Task Oppretter_relasjon_med_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db, "Lokal merkenemnd");
        var til = await NyVirksomhetAsync(db, "Statsforvalteren");
        var type = await NyRelasjonsTypeAsync(db, "underlagt-provtest");

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        var relasjon = await register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist");

        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == relasjon.Id);
        Assert.Equal("opprettet", proveniens.Handling);
        Assert.Equal("gjeldende", relasjon.Entitetsstatus);
    }

    [Fact]
    public async Task Kan_ikke_ha_relasjon_til_seg_selv()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var type = await NyRelasjonsTypeAsync(db);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, virksomhet, type, null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_relasjonstype_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db);
        var til = await NyVirksomhetAsync(db);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(fra, til, "ukjent_type", null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Inaktiv_relasjonstype_avvises_som_ukjent()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db);
        var til = await NyVirksomhetAsync(db);
        var type = await NyRelasjonsTypeAsync(db, aktiv: false);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Duplikat_samme_fra_til_type_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db);
        var til = await NyVirksomhetAsync(db);
        var type = await NyRelasjonsTypeAsync(db);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist"));
    }

    /// <summary>
    /// docs/29 §Del C, §Steg 7 punkt 3 — selve poenget hentet fra OverordnetEnhetId-bug-lærdommen i
    /// docs/28: SAMME lagrede rad skal gi ULIK visningstekst avhengig av hvilken virksomhet man spør
    /// fra, ikke bare antas riktig fordi mønsteret er kopiert fra Tjenesteavhengighet.
    /// </summary>
    [Fact]
    public async Task Samme_rad_gir_ulik_visningstekst_fra_og_til_siden()
    {
        await using var db = _fixture.NyDbContext();
        var (merkenemnd, merkenemndNavn) = await NyVirksomhetMedNavnAsync(db, "Lokal merkenemnd");
        var (statsforvalteren, statsforvalterenNavn) = await NyVirksomhetMedNavnAsync(db, "Statsforvalteren");
        var type = await NyRelasjonsTypeAsync(db, "sekretariat-visningtest", "har sekretariat hos {0}", "er sekretariat for {0}");

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await register.OpprettAsync(merkenemnd, statsforvalteren, type, null, null, null, "Kari Jurist");

        var fraSiden = await register.HentForVirksomhetAsync(merkenemnd);
        var visningFra = Assert.Single(fraSiden);
        Assert.Equal("fra", visningFra.Retning);
        Assert.Equal($"har sekretariat hos {statsforvalterenNavn}", visningFra.Visningstekst);
        Assert.Equal(statsforvalteren, visningFra.MotpartVirksomhetId);

        var tilSiden = await register.HentForVirksomhetAsync(statsforvalteren);
        var visningTil = Assert.Single(tilSiden);
        Assert.Equal("til", visningTil.Retning);
        Assert.Equal($"er sekretariat for {merkenemndNavn}", visningTil.Visningstekst);
        Assert.Equal(merkenemnd, visningTil.MotpartVirksomhetId);
    }

    private static async Task<Guid> NyRettskildeAsync(RegelIdeDbContext db, string tittel)
    {
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = $"{tittel}-{id:N}", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// [Ny, nemnd/sekretariat-runden, 2026-09-09] Lovens side av relasjonen: nemnd-/sekretariat-caset
    /// (konkurranseloven § 36 sjette ledd) skal kunne leses tilbake FRA bestemmelsen, ikke bare fra
    /// virksomheten — docs/32 §3 S1/S2.
    /// </summary>
    [Fact]
    public async Task Henter_relasjoner_hjemlet_i_en_rettskilde_med_begge_parter_navngitt()
    {
        await using var db = _fixture.NyDbContext();
        var (nemnd, nemndNavn) = await NyVirksomhetMedNavnAsync(db, "Konkurranseklagenemnda");
        var (kns, knsNavn) = await NyVirksomhetMedNavnAsync(db, "Klagenemndssekretariatet");
        var type = await NyRelasjonsTypeAsync(db, fraMal: "har sekretariat hos {0}", tilMal: "er sekretariat for {0}");
        var lov = await NyRettskildeAsync(db, "Konkurranseloven");
        var eid = "https://lovdata.no/eli/lov/2004/03/05/12/nor/§36/ledd-6";

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await register.OpprettAsync(nemnd, kns, type, lov, eid, null, "Kari Jurist");

        var hjemlet = await register.HentForHjemmelRettskildeAsync(lov);

        var rad = Assert.Single(hjemlet);
        // Fra-malen brukes ALLTID fra lovens ståsted — det finnes ingen «motpart» her å velge retning ut fra.
        Assert.Equal($"{nemndNavn} har sekretariat hos {knsNavn}", rad.Visningstekst);
        Assert.Equal(nemnd, rad.FraVirksomhetId);
        Assert.Equal(nemndNavn, rad.FraNavn);
        Assert.Equal(kns, rad.TilVirksomhetId);
        Assert.Equal(knsNavn, rad.TilNavn);
        Assert.Equal(eid, rad.HjemmelEid);
    }

    /// <summary>
    /// Kontrasten som gjør skillet verdt noe: KNS er sekretariat for Klagenemnda for godkjenning av
    /// utenlandsk utdanning, men INGEN bestemmelse sier det — bare organisasjonskartet. En slik relasjon
    /// hører per definisjon ikke til noen rettskilde, og skal ikke kunne dukke opp under en lov.
    /// </summary>
    [Fact]
    public async Task Relasjon_uten_hjemmel_dukker_ikke_opp_under_noen_rettskilde()
    {
        await using var db = _fixture.NyDbContext();
        var nemnd = await NyVirksomhetAsync(db, "Klagenemnda");
        var kns = await NyVirksomhetAsync(db, "Klagenemndssekretariatet");
        var type = await NyRelasjonsTypeAsync(db, fraMal: "har sekretariat hos {0}", tilMal: "er sekretariat for {0}");
        var lov = await NyRettskildeAsync(db, "Forskrift om enkelte klagenemnder");

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await register.OpprettAsync(nemnd, kns, type, null, null, "Bekreftet mot organisasjonskartet, ikke mot rettskilde.", "Kari Jurist");

        Assert.Empty(await register.HentForHjemmelRettskildeAsync(lov));
        // …men relasjonen finnes fortsatt, sett fra virksomheten.
        Assert.Single(await register.HentForVirksomhetAsync(nemnd));
    }

    /// <summary>
    /// Navneformen («Klagenemnd for godkjenning …», slik LOVEN skriver den) skal vinne over registerets
    /// VERSAL-form også fra lovens side — samme regel som visningen fra virksomhetssiden alt følger.
    /// Johann 2026-09-08: «Virksomhet, org.nummer og brreg er strengt tatt bare attributter på det som
    /// er definert av lov.»
    /// </summary>
    [Fact]
    public async Task Bruker_navneformen_ikke_registernavnet_i_visningsteksten()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db, "KONKURRANSEKLAGENEMNDA");
        var (til, _) = await NyVirksomhetMedNavnAsync(db, "KLAGENEMNDSSEKRETARIATET (KNS)");
        var navneform = $"Klagenemndssekretariatet-{Guid.NewGuid():N}";
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Term = navneform, Begrepskategori = "virksomhet", Status = "gjeldende",
            VirksomhetReferanseId = til, Navneformgrunn = VirksomhetVisningsnavnTjeneste.VisningsGrunn,
            Entitetsstatus = "gjeldende", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        var type = await NyRelasjonsTypeAsync(db, fraMal: "har sekretariat hos {0}", tilMal: "er sekretariat for {0}");
        var lov = await NyRettskildeAsync(db, "Konkurranseloven");

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await register.OpprettAsync(fra, til, type, lov, "https://lovdata.no/eli/lov/2004/03/05/12/nor/§36/ledd-6", null, "Kari Jurist");

        var rad = Assert.Single(await register.HentForHjemmelRettskildeAsync(lov));
        Assert.Equal(navneform, rad.TilNavn);
        Assert.DoesNotContain("KLAGENEMNDSSEKRETARIATET", rad.Visningstekst);
    }

    [Fact]
    public async Task Sletter_relasjon()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db);
        var til = await NyVirksomhetAsync(db);
        var type = await NyRelasjonsTypeAsync(db);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        var relasjon = await register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist");

        Assert.True(await register.SlettAsync(relasjon.Id));
        Assert.False(await db.VirksomhetRelasjoner.AnyAsync(r => r.Id == relasjon.Id));
        Assert.False(await register.SlettAsync(relasjon.Id));
    }
}
