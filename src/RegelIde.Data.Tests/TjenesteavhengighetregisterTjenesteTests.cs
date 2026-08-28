using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Tjenesteavhengighetregister (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class TjenesteavhengighetregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TjenesteavhengighetregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db)
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        return virksomhet;
    }

    private static async Task<Guid> NyTjenesteAsync(RegelIdeDbContext db, Guid virksomhetId, string tittel)
    {
        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettAsync(
            virksomhetId, tittel, null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist");
        return tjeneste.Id;
    }

    [Fact]
    public async Task Oppretter_avhengighet_forutsetning_for_med_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var serveringsbevilling = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");
        var skjenkebevilling = await NyTjenesteAsync(db, virksomhet, "Alminnelig skjenkebevilling");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        var avhengighet = await register.OpprettAsync(
            virksomhet, serveringsbevilling, skjenkebevilling, "forutsetning_for", null, null, "Kari Jurist");

        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == avhengighet.Id);
        Assert.Equal("opprettet", proveniens.Handling);
    }

    [Fact]
    public async Task Kan_ikke_ha_avhengighet_til_seg_selv()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var tjeneste = await NyTjenesteAsync(db, virksomhet, "Alminnelig skjenkebevilling");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, tjeneste, tjeneste, "for", null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_rel_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        var b = await NyTjenesteAsync(db, virksomhet, "B");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, a, b, "ukjent_rel", null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task HendelseId_krever_rel_utlost_av()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        var b = await NyTjenesteAsync(db, virksomhet, "B");
        var hendelse = await new HendelseregisterTjeneste(db).OpprettAsync(null, "Eierskifte", "virksomhetshendelse", null, "Kari Jurist");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, a, b, "forutsetning_for", hendelse.Id, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Utlost_av_med_hendelse_gir_riktig_visningstekst_med_hendelsesnavn_pa_begge_sider()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var skjenkebevilling = await NyTjenesteAsync(db, virksomhet, "Alminnelig skjenkebevilling");
        var endringAvEiere = await NyTjenesteAsync(db, virksomhet, "Endring av eiere eller eierandeler");
        var hendelse = await new HendelseregisterTjeneste(db).OpprettAsync(null, "Endring av eierskap", "virksomhetshendelse", null, "Kari Jurist");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await register.OpprettAsync(virksomhet, skjenkebevilling, endringAvEiere, "utlost_av", hendelse.Id, null, "Kari Jurist");

        var fraSiden = await register.HentForTjenesteAsync(skjenkebevilling);
        var visning = Assert.Single(fraSiden);
        Assert.Equal("fra", visning.Retning);
        Assert.Equal("kan føre til Endring av eiere eller eierandeler (via Endring av eierskap)", visning.Visningstekst);

        var tilSiden = await register.HentForTjenesteAsync(endringAvEiere);
        var visningTil = Assert.Single(tilSiden);
        Assert.Equal("til", visningTil.Retning);
        Assert.Equal("kan utløses av Alminnelig skjenkebevilling (via Endring av eierskap)", visningTil.Visningstekst);
    }

    [Fact]
    public async Task Ett_rettet_kant_ikke_speilbilde_rad_lagres()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var serveringsbevilling = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");
        var skjenkebevilling = await NyTjenesteAsync(db, virksomhet, "Alminnelig skjenkebevilling");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await register.OpprettAsync(virksomhet, serveringsbevilling, skjenkebevilling, "forutsetning_for", null, null, "Kari Jurist");

        // Scoped til de to tjenestene i denne testen, ikke hele (delte) tabellen — andre tester i
        // samme testkjøring kan ha egne rader der.
        Assert.Single(await db.Tjenesteavhengigheter
            .Where(t => t.FraTjenesteId == serveringsbevilling && t.TilTjenesteId == skjenkebevilling)
            .ToListAsync());

        var fraSiden = await register.HentForTjenesteAsync(serveringsbevilling);
        Assert.Equal("er forutsetning for Alminnelig skjenkebevilling", Assert.Single(fraSiden).Visningstekst);

        var tilSiden = await register.HentForTjenesteAsync(skjenkebevilling);
        Assert.Equal("krever Serveringsbevilling", Assert.Single(tilSiden).Visningstekst);
    }

    [Fact]
    public async Task Ny_kobling_som_ville_lukket_en_sykel_kastes()
    {
        // Byggesteg 5 runde 4 — fantes ingen sykel-sjekk her tidligere (kun selvreferanse+duplikat).
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        var b = await NyTjenesteAsync(db, virksomhet, "B");
        var c = await NyTjenesteAsync(db, virksomhet, "C");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await register.OpprettAsync(virksomhet, a, b, "forutsetning_for", null, null, "Kari Jurist");
        await register.OpprettAsync(virksomhet, b, c, "forutsetning_for", null, null, "Kari Jurist");

        // c -> a ville lukket sykelen a -> b -> c -> a.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, c, a, "forutsetning_for", null, null, "Kari Jurist"));
        Assert.Contains("sykel", ex.Message);
    }

    [Fact]
    public async Task Har_del_gir_riktig_visningstekst_pa_begge_sider()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var helhet = await NyTjenesteAsync(db, virksomhet, "Skjenkebevilling");
        var del = await NyTjenesteAsync(db, virksomhet, "Kunnskapsprøve");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await register.OpprettAsync(virksomhet, helhet, del, "har_del", null, null, "Kari Jurist");

        var fraSiden = await register.HentForTjenesteAsync(helhet);
        Assert.Equal("har del Kunnskapsprøve", Assert.Single(fraSiden).Visningstekst);

        var tilSiden = await register.HentForTjenesteAsync(del);
        Assert.Equal("er del av Skjenkebevilling", Assert.Single(tilSiden).Visningstekst);
    }

    [Fact]
    public async Task Sletter_avhengighet()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        var b = await NyTjenesteAsync(db, virksomhet, "B");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        var avhengighet = await register.OpprettAsync(virksomhet, a, b, "for", null, null, "Kari Jurist");

        Assert.True(await register.SlettAsync(avhengighet.Id));
        Assert.Empty(await register.HentForTjenesteAsync(a));
    }

    // ---------- Ekstern tjenestereferanse (2026-08-19, feature/tjenesteavhengighet-ekstern-referanse) ----------

    [Fact]
    public async Task Oppretter_avhengighet_til_ekstern_referanse_og_leser_riktig_motpart()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var serveringsbevilling = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await register.OpprettAsync(
            virksomhet, serveringsbevilling, null, "avhengig_av", null, null, "Kari Jurist",
            tilOrganisasjonsnummer: "974761122", tilNavn: "Registrer matbedriften din hos Mattilsynet");

        var fraSiden = await register.HentForTjenesteAsync(serveringsbevilling);
        var visning = Assert.Single(fraSiden);
        Assert.Null(visning.MotpartTjenesteId);
        Assert.Equal("974761122", visning.MotpartOrganisasjonsnummer);
        Assert.Equal("Registrer matbedriften din hos Mattilsynet", visning.MotpartNavn);
        Assert.Equal("Registrer matbedriften din hos Mattilsynet er avhengig av denne", visning.Visningstekst);

        // Selve raden peker via TilEksternReferanseId, ikke TilTjenesteId.
        var rad = await db.Tjenesteavhengigheter.SingleAsync(t => t.FraTjenesteId == serveringsbevilling);
        Assert.Null(rad.TilTjenesteId);
        Assert.NotNull(rad.TilEksternReferanseId);
    }

    [Fact]
    public async Task Gjenbruker_eksisterende_ekstern_referanse_pa_orgnr_og_navn_ikke_duplikat()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var serveringsbevilling = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");
        var skjenkebevilling = await NyTjenesteAsync(db, virksomhet, "Alminnelig skjenkebevilling");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await register.OpprettAsync(
            virksomhet, serveringsbevilling, null, "avhengig_av", null, null, "Kari Jurist",
            tilOrganisasjonsnummer: "974761122", tilNavn: "Registrer matbedriften din hos Mattilsynet");
        await register.OpprettAsync(
            virksomhet, skjenkebevilling, null, "avhengig_av", null, null, "Kari Jurist",
            tilOrganisasjonsnummer: "974761122", tilNavn: "Registrer matbedriften din hos Mattilsynet");

        // To kanter refererer samme (orgnr, navn) — skal gjenbruke SAMME plassholder-rad, ikke opprette to.
        // Filtrert på orgnr (ikke hele tabellen) — DataTestCollection deler én Postgres på tvers av
        // ALLE tester i samlingen, så en usortert tabell-telling er skjørt mot andre testers egne rader.
        Assert.Single(await db.EksterneTjenestereferanser.Where(r => r.Organisasjonsnummer == "974761122").ToListAsync());
    }

    [Fact]
    public async Task Ulikt_navn_samme_orgnr_gir_to_distinkte_eksterne_referanser()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        var b = await NyTjenesteAsync(db, virksomhet, "B");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await register.OpprettAsync(
            virksomhet, a, null, "avhengig_av", null, null, "Kari Jurist",
            tilOrganisasjonsnummer: "974761122", tilNavn: "Registrer matbedriften din hos Mattilsynet");
        await register.OpprettAsync(
            virksomhet, b, null, "avhengig_av", null, null, "Kari Jurist",
            tilOrganisasjonsnummer: "974761122", tilNavn: "Vandelskontroll fra Mattilsynet");

        // Filtrert på orgnr (ikke hele tabellen) — se kommentar i testen over om delt Postgres-samling.
        Assert.Equal(2, await db.EksterneTjenestereferanser.CountAsync(r => r.Organisasjonsnummer == "974761122"));
    }

    [Fact]
    public async Task Verken_tiltjeneste_eller_ekstern_mal_gir_400()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, a, null, "avhengig_av", null, null, "Kari Jurist"));
        Assert.Contains("Mål for avhengigheten mangler", ex.Message);
    }

    [Fact]
    public async Task Bade_tiltjeneste_og_ekstern_mal_gir_400()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        var b = await NyTjenesteAsync(db, virksomhet, "B");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(
                virksomhet, a, b, "avhengig_av", null, null, "Kari Jurist",
                tilOrganisasjonsnummer: "974761122", tilNavn: "Registrer matbedriften din hos Mattilsynet"));
        Assert.Contains("ikke begge", ex.Message);
    }

    [Fact]
    public async Task Ekstern_referanse_uten_navn_gir_400()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(
                virksomhet, a, null, "avhengig_av", null, null, "Kari Jurist", tilOrganisasjonsnummer: "974761122"));
        // [ENDRET, 2026-08-28] Navn alene er nå det reelle ekstern-referanse-signalet (orgnummer er
        // blitt valgfritt, se EksternTjenestereferanseEntitet.Organisasjonsnummer) — et oppgitt
        // orgnummer UTEN navn gir fortsatt en tydelig feil, bare med ny ordlyd.
        Assert.Contains("organisasjonsnummer krever også et navn", ex.Message);
    }

    /// <summary>
    /// [Ny, 2026-08-28, bulk-import-runden] Reproduserer funnet fra vielsesreise-importtesten
    /// (data/eksempler/gifte-seg-reise.modelleksport.json): en konseptuell ekstern motpart («en
    /// utenlandsk vigselsmyndighet») har ingen ekte norsk orgnummer i det hele tatt, og skal likevel
    /// kunne opprettes — orgnummer er nå valgfritt, kun navn er påkrevd.
    /// </summary>
    [Fact]
    public async Task Ekstern_referanse_uten_organisasjonsnummer_opprettes_med_navn_alene()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var registrering = await NyTjenesteAsync(db, virksomhet, "Ekteskap inngått etter utenlandsk rett – registrering i Norge");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await register.OpprettAsync(
            virksomhet, registrering, null, "avhengig_av", null, null, "Kari Jurist",
            tilNavn: "Vigsel gjennomført av utenlandsk vigselsmyndighet");

        var fraSiden = await register.HentForTjenesteAsync(registrering);
        var visning = Assert.Single(fraSiden);
        Assert.Null(visning.MotpartTjenesteId);
        Assert.Null(visning.MotpartOrganisasjonsnummer);
        Assert.Equal("Vigsel gjennomført av utenlandsk vigselsmyndighet", visning.MotpartNavn);

        var referanse = await db.EksterneTjenestereferanser.SingleAsync(e => e.Navn == "Vigsel gjennomført av utenlandsk vigselsmyndighet");
        Assert.Null(referanse.Organisasjonsnummer);
    }

    [Fact]
    public async Task Ekte_og_ekstern_avhengighet_pa_samme_tjeneste_gir_riktig_motpart_for_hver()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var serveringsbevilling = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");
        var skjenkebevilling = await NyTjenesteAsync(db, virksomhet, "Alminnelig skjenkebevilling");

        var register = new TjenesteavhengighetregisterTjeneste(db);
        await register.OpprettAsync(virksomhet, serveringsbevilling, skjenkebevilling, "forutsetning_for", null, null, "Kari Jurist");
        await register.OpprettAsync(
            virksomhet, serveringsbevilling, null, "avhengig_av", null, null, "Kari Jurist",
            tilOrganisasjonsnummer: "974761122", tilNavn: "Vandelskontroll fra Politiet");

        var fraSiden = await register.HentForTjenesteAsync(serveringsbevilling);
        Assert.Equal(2, fraSiden.Count);

        var ekteMotpart = Assert.Single(fraSiden, v => v.MotpartTjenesteId is not null);
        Assert.Equal(skjenkebevilling, ekteMotpart.MotpartTjenesteId);
        Assert.Null(ekteMotpart.MotpartOrganisasjonsnummer);

        var eksternMotpart = Assert.Single(fraSiden, v => v.MotpartTjenesteId is null);
        Assert.Equal("974761122", eksternMotpart.MotpartOrganisasjonsnummer);
        Assert.Equal("Vandelskontroll fra Politiet", eksternMotpart.MotpartNavn);
    }
}
