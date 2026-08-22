using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="OppgaveregisterHandlingSeed"/> — kobler allerede høstede Oppgaveregister-skjemaer
/// (<see cref="EksternKildeEntitet"/>) inn i domenemodellen.
/// <para>
/// **Isolasjon** — samme delte, ikke-transaksjonelle embedded Postgres-instans som resten av
/// RegelIde.Data.Tests (se <see cref="EmbeddedPostgresFixture"/>/<see cref="DataTestCollection"/>,
/// ÉN instans for HELE assemblyen, tester i samme collection kjører sekvensielt, ALDRI parallelt).
/// Samme "ingen wipe av delte tabeller, bruk ferske, unike verdier per test"-mønster som
/// <see cref="HandlingregisterTjenesteTests"/> for <c>Virksomheter</c>/<c>Rettskilder</c>/
/// <c>Tjenester</c>/<c>Handlinger</c> (disse tabellene bærer data fra MANGE andre testklasser gjennom
/// hele kjøringen — et blindt <c>ExecuteDeleteAsync()</c> på dem ville vært destruktivt utenfor denne
/// klassens eget ansvar). Eneste tabell som WIPES (kildetype-scopet, samme prinsipp som
/// <see cref="OppgaveregisterHenterTests"/> selv bruker for hele tabellen) er
/// <see cref="EksternKildeEntitet"/> — nødvendig fordi <see cref="OppgaveregisterHandlingSeed.SeedAsync"/>
/// leser HELE <see cref="OppgaveregisterHenter.Kildetype"/>-scopet, og resultatets tellere
/// (<c>SkjemaTotalt</c> m.fl.) derfor må ha et kjent utgangspunkt per test.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class OppgaveregisterHandlingSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public OppgaveregisterHandlingSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // Ferske, unike verdier per test (ikke gjenbrukte konstanter) — se klassekommentaren.
    private static int _teller;
    private static string NyOrgnr() => (910_000_000 + Interlocked.Increment(ref _teller)).ToString();
    private static string NyLovDatokode() => $"LOV-2031-01-01-{Interlocked.Increment(ref _teller)}";

    private static string SkjemaJson(
        string guid, string navn, string orgnr, string etatsnavn, string bruksomraadeNavn, string? lovDatokode,
        string? ekstraBruksomraadeNavn = null) =>
        $$"""
        {
          "guid": "{{guid}}",
          "navn": "{{navn}}",
          "eier": { "organisasjonsnummer": {{long.Parse(orgnr)}}, "etatsnavn": "{{etatsnavn}}" },
          "formaal": { "fritekst": "Formål for {{navn}}." },
          "bruksomraader": [
            { "navn": "{{bruksomraadeNavn}}" }
            {{(ekstraBruksomraadeNavn is null ? "" : $$""", { "navn": "{{ekstraBruksomraadeNavn}}" }""")}}
          ],
          "lovhjemler": {{(lovDatokode is null ? "[]" : $$"""[{ "dato": "{{lovDatokode}}", "henvisning": "§ 1", "forskrifter": [] }]""")}}
        }
        """;

    private static async Task<Virksomhet> LeggTilVirksomhetAsync(RegelIdeDbContext db, string navn, string orgnr)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = navn, Organisasjonsnummer = orgnr, OpprettetTidspunkt = DateTimeOffset.UtcNow };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    private static async Task<RettskildeEntitet> LeggTilRettskildeAsync(RegelIdeDbContext db, string eli)
    {
        var r = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Importrolle = "referanse",
            Tittel = "Testlov " + eli, Eli = eli, Status = "Gjeldende", OpprettetAv = "test",
        };
        db.Rettskilder.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    /// <summary>Wiper KUN sitt eget kildetype-scope, ikke hele tabellen — se klassekommentaren.
    /// Kalles FØR hver test setter opp sine egne <see cref="EksternKildeEntitet"/>-rader, slik at
    /// <see cref="OppgaveregisterHandlingSeed.SeedAsync"/>s tellere blir deterministiske per test.</summary>
    private static async Task<EksternKildeEntitet> NyKildeAsync(RegelIdeDbContext db, string eksternId, string raaJson)
    {
        await db.EksterneKilder.Where(k => k.Kildetype == OppgaveregisterHenter.Kildetype && k.EksternId == eksternId).ExecuteDeleteAsync();
        var k = new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = OppgaveregisterHenter.Kildetype, EksternId = eksternId,
            RaaJson = raaJson, InnholdsHash = "irrelevant-for-denne-testen", HentetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.EksterneKilder.Add(k);
        await db.SaveChangesAsync();
        return k;
    }

    /// <summary>Kjører seeden scopet til KUN de eksterne kildene testen selv nettopp opprettet (via
    /// <see cref="NyKildeAsync"/>) — kildetypen kan i prinsippet inneholde rader fra andre, tidligere
    /// kjørte tester i samme delte database (ingen wipe av hele tabellen, se klassekommentaren), så
    /// <see cref="OppgaveregisterHandlingSeed.SeedAsync"/> selv kjøres mot en midlertidig, isolert
    /// kopi: enkleste presise løsning er å faktisk fjerne EVENTUELLE andre rader av denne kildetypen
    /// først (de er alltid ferdig behandlet av en tidligere, allerede avsluttet test i samme sekvensielle
    /// kjøring — se klassekommentaren om at collection-tester ikke kjører parallelt).</summary>
    private static async Task<OppgaveregisterHandlingSeedResultat> KjorSeedIsolertAsync(RegelIdeDbContext db, params string[] behold)
    {
        await db.EksterneKilder
            .Where(k => k.Kildetype == OppgaveregisterHenter.Kildetype && !behold.Contains(k.EksternId))
            .ExecuteDeleteAsync();
        return await OppgaveregisterHandlingSeed.SeedAsync(db);
    }

    [Fact]
    public async Task Kjent_virksomhet_og_rettskilde_gir_full_kobling()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        var lovDatokode = NyLovDatokode();
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(lovDatokode, out _);
        var virksomhet = await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        var rettskilde = await LeggTilRettskildeAsync(db, eli);
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson("T-" + orgnr, "Testskjema en", orgnr, "TESTETATEN", "Periodisk rapportering", lovDatokode));

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(1, resultat.SkjemaTotalt);
        Assert.Equal(1, resultat.NyeHandlinger);
        Assert.Equal(0, resultat.OppdaterteHandlinger);
        Assert.Equal(0, resultat.UendretHandlinger);
        Assert.Equal(0, resultat.HoppetOverUsikkerVirksomhet);
        Assert.Equal(1, resultat.NyeTjenester);
        Assert.Equal(1, resultat.LovhjemlerTotalt);
        Assert.Equal(1, resultat.RettskildematcherFunnet);
        Assert.Equal(0, resultat.RettskildematcherIkkeFunnet);

        var tjeneste = await db.Tjenester.SingleAsync(t => t.VirksomhetId == virksomhet.Id);
        Assert.Equal("Oppgaveregisteret — " + virksomhet.Navn, tjeneste.Tittel);
        Assert.Equal("utkast", tjeneste.Status);

        var handling = await db.Handlinger.SingleAsync(h => h.TjenesteId == tjeneste.Id);
        Assert.Equal("Testskjema en", handling.Navn);
        Assert.Equal("rapportere", handling.Handlingstype); // Periodisk rapportering -> rapportere
        Assert.Equal("periodisk_rapportering", handling.Bruksomraade);
        Assert.Equal("soker", handling.UtfortAv);
        Assert.Equal(kilde.Id, handling.EksternKildeId);
        Assert.Equal("Formål for Testskjema en.", handling.Merknad);

        var referanse = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == handling.Id);
        Assert.Equal(rettskilde.Id, referanse.TilRettskildeId);
        Assert.Equal(eli, referanse.TilEid);
    }

    [Fact]
    public async Task Skjema_med_ukjent_virksomhet_hoppes_helt_over()
    {
        await using var db = _fixture.NyDbContext();

        var ukjentOrgnr = NyOrgnr(); // med vilje IKKE lagt til noen Virksomhet for denne.
        var kilde = await NyKildeAsync(db, "T-" + ukjentOrgnr, SkjemaJson("T-" + ukjentOrgnr, "Testskjema to", ukjentOrgnr, "UKJENT ETAT", "Hendelsesrapportering", null));

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(1, resultat.SkjemaTotalt);
        Assert.Equal(0, resultat.NyeHandlinger);
        Assert.Equal(1, resultat.HoppetOverUsikkerVirksomhet);
        Assert.Equal(0, resultat.NyeTjenester);
        Assert.False(await db.Handlinger.AnyAsync(h => h.EksternKildeId == kilde.Id));
    }

    [Fact]
    public async Task Kjent_virksomhet_men_ukjent_rettskilde_lager_handling_uten_kobling()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        var lovDatokode = NyLovDatokode(); // ALDRI koblet til noen Rettskilde-rad under.
        await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson("T-" + orgnr, "Testskjema tre", orgnr, "TESTETATEN", "Søknad / registrering", lovDatokode));

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(1, resultat.NyeHandlinger);
        Assert.Equal(1, resultat.LovhjemlerTotalt);
        Assert.Equal(0, resultat.RettskildematcherFunnet);
        Assert.Equal(1, resultat.RettskildematcherIkkeFunnet);

        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        Assert.Equal("registrere", handling.Handlingstype); // Søknad / registrering -> registrere (dokumentert forenkling)
        Assert.Equal("soknad_registrering", handling.Bruksomraade);
        Assert.False(await db.HandlingRegelverksreferanser.AnyAsync(r => r.HandlingId == handling.Id));
    }

    [Fact]
    public async Task To_virksomheter_deler_ikke_tjeneste_men_gjenbruker_egen_paa_nytt_skjema()
    {
        await using var db = _fixture.NyDbContext();

        var orgnrA = NyOrgnr();
        var orgnrB = NyOrgnr();
        var virksomhetA = await LeggTilVirksomhetAsync(db, "Testetaten " + orgnrA, orgnrA);
        var virksomhetB = await LeggTilVirksomhetAsync(db, "Andre Etaten " + orgnrB, orgnrB);

        var kildeA1 = await NyKildeAsync(db, "T-" + orgnrA + "-1", SkjemaJson("T-" + orgnrA + "-1", "Skjema A", orgnrA, "TESTETATEN", "Hendelsesrapportering", null));
        var kildeA2 = await NyKildeAsync(db, "T-" + orgnrA + "-2", SkjemaJson("T-" + orgnrA + "-2", "Skjema B", orgnrA, "TESTETATEN", "Hendelsesrapportering", null));
        var kildeB = await NyKildeAsync(db, "T-" + orgnrB, SkjemaJson("T-" + orgnrB, "Skjema C", orgnrB, "ANDRE ETATEN", "Hendelsesrapportering", null));

        var resultat = await KjorSeedIsolertAsync(db, kildeA1.EksternId, kildeA2.EksternId, kildeB.EksternId);

        Assert.Equal(3, resultat.NyeHandlinger);
        Assert.Equal(2, resultat.NyeTjenester); // én per virksomhet, ikke én per skjema

        var tjenesteA = await db.Tjenester.SingleAsync(t => t.VirksomhetId == virksomhetA.Id);
        var tjenesteB = await db.Tjenester.SingleAsync(t => t.VirksomhetId == virksomhetB.Id);
        Assert.NotEqual(tjenesteA.Id, tjenesteB.Id);
        Assert.Equal(2, await db.Handlinger.CountAsync(h => h.TjenesteId == tjenesteA.Id));
        Assert.Equal(1, await db.Handlinger.CountAsync(h => h.TjenesteId == tjenesteB.Id));
    }

    [Fact]
    public async Task Rekjoring_med_uendrede_data_er_en_no_op()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        var lovDatokode = NyLovDatokode();
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(lovDatokode, out _);
        var virksomhet = await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        await LeggTilRettskildeAsync(db, eli);
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson("T-" + orgnr, "Testskjema syv", orgnr, "TESTETATEN", "Periodisk rapportering", lovDatokode));

        var forsteResultat = await KjorSeedIsolertAsync(db, kilde.EksternId);
        Assert.Equal(1, forsteResultat.NyeHandlinger);

        var andreResultat = await KjorSeedIsolertAsync(db, kilde.EksternId);
        Assert.Equal(0, andreResultat.NyeHandlinger);
        Assert.Equal(0, andreResultat.OppdaterteHandlinger);
        Assert.Equal(1, andreResultat.UendretHandlinger);
        Assert.Equal(0, andreResultat.NyeTjenester); // fant den allerede opprettede aggregerte tjenesten, ikke en ny

        var tjeneste = await db.Tjenester.SingleAsync(t => t.VirksomhetId == virksomhet.Id); // ingen duplikat-tjeneste
        var handling = await db.Handlinger.SingleAsync(h => h.TjenesteId == tjeneste.Id); // ingen duplikat-handling
        Assert.Equal(1, await db.HandlingRegelverksreferanser.CountAsync(r => r.HandlingId == handling.Id)); // ingen duplikat-referanse
    }

    [Fact]
    public async Task Endret_navn_pa_kilden_oppdaterer_eksisterende_handling()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson("T-" + orgnr, "Gammelt navn", orgnr, "TESTETATEN", "Hendelsesrapportering", null));

        await KjorSeedIsolertAsync(db, kilde.EksternId);

        // Simulerer at OppgaveregisterHenter har hentet en endret versjon av SAMME skjema (samme guid/EksternId).
        kilde.RaaJson = SkjemaJson("T-" + orgnr, "Nytt navn", orgnr, "TESTETATEN", "Hendelsesrapportering", null);
        await db.SaveChangesAsync();

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(0, resultat.NyeHandlinger);
        Assert.Equal(1, resultat.OppdaterteHandlinger);
        Assert.Equal(0, resultat.UendretHandlinger);

        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        Assert.Equal("Nytt navn", handling.Navn);
        Assert.Equal(2, handling.Versjon);
    }

    [Fact]
    public async Task Kun_forste_bruksomraade_brukes_naar_skjema_har_to()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson(
            "T-" + orgnr, "Skjema med to bruksomraader", orgnr, "TESTETATEN", "Hendelsesrapportering", null,
            ekstraBruksomraadeNavn: "Søknad / registrering"));

        await KjorSeedIsolertAsync(db, kilde.EksternId);

        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        Assert.Equal("hendelsesrapportering", handling.Bruksomraade);
        Assert.Equal("rapportere", handling.Handlingstype);
    }
}
