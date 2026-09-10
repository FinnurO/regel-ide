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
    private static string NyForskriftDatokode() => $"FOR-2031-01-01-{Interlocked.Increment(ref _teller)}";

    private static string JsonStreng(string? verdi) => verdi is null ? "null" : $"\"{verdi}\"";

    private static string SkjemaJson(
        string guid, string navn, string orgnr, string etatsnavn, string bruksomraadeNavn, string? lovDatokode,
        string? ekstraBruksomraadeNavn = null, string? henvisning = "§ 1") =>
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
          "lovhjemler": {{(lovDatokode is null ? "[]" : $$"""[{ "dato": "{{lovDatokode}}", "henvisning": {{JsonStreng(henvisning)}}, "forskrifter": [] }]""")}}
        }
        """;

    /// <summary>Lav-nivå variant som tar den RÅ <c>lovhjemler</c>-JSON-arrayen direkte — brukt av tester
    /// som trenger nøstede <c>forskrifter[]</c> (med sin EGEN, uavhengige <c>henvisning</c>, se
    /// <see cref="OppgaveregisterHandlingSeed"/>s klassekommentar punkt (c)) eller flere lovhjemler-
    /// oppføringer, noe <see cref="SkjemaJson"/> over ikke uttrykker.</summary>
    private static string SkjemaJsonMedRaaLovhjemler(string guid, string navn, string orgnr, string etatsnavn, string raaLovhjemlerJson) =>
        $$"""
        {
          "guid": "{{guid}}",
          "navn": "{{navn}}",
          "eier": { "organisasjonsnummer": {{long.Parse(orgnr)}}, "etatsnavn": "{{etatsnavn}}" },
          "formaal": { "fritekst": "Formål for {{navn}}." },
          "bruksomraader": [ { "navn": "Hendelsesrapportering" } ],
          "lovhjemler": {{raaLovhjemlerJson}}
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

    /// <summary>Legger til en ekte paragraf-node med Eid konstruert AKKURAT slik
    /// <see cref="LovdataIdentifikatorer.ParagrafEid"/> (og dermed HTML-parseren selv) gjør — den
    /// bekreftelsen <see cref="OppgaveregisterHandlingSeed"/>s tiltak 2 krever før en regex-ekstrahert
    /// paragrafkandidat får lov til å styre <see cref="HandlingRegelverksreferanseEntitet.TilEid"/>.</summary>
    private static async Task<RettskildeNodeEntitet> LeggTilParagrafNodeAsync(RegelIdeDbContext db, Guid rettskildeId, string eli, string paragrafnummer)
    {
        var node = new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = LovdataIdentifikatorer.ParagrafEid(eli, paragrafnummer),
            KildeId = "test-kilde-" + Guid.NewGuid(), NodeType = "paragraf", Nummer = paragrafnummer,
        };
        db.RettskildeNoder.Add(node);
        await db.SaveChangesAsync();
        return node;
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

    // ---------- issue #147: tiltak 1 (KildeHenvisningFritekst) + tiltak 2 (paragraf-ekstraksjon) ----------
    // Henvisning-eksemplene under ("§ 42", "§§ 1 til 5", "§ 96-97") er ALLE bekreftet ekte, faktiske
    // former hentet fra den seedede dev-databasens EksternKildeEntitet.RaaJson 2026-09-10 (se PR-
    // beskrivelsen for målingene mot hele korpuset) — ikke oppdiktede eksempler, jf. CLAUDE.md §8.

    [Fact]
    public async Task Enkel_paragrafhenvisning_persisteres_og_loses_til_ekte_paragrafnode_naar_den_finnes()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        var lovDatokode = NyLovDatokode();
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(lovDatokode, out _);
        await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        var rettskilde = await LeggTilRettskildeAsync(db, eli);
        await LeggTilParagrafNodeAsync(db, rettskilde.Id, eli, "§42");
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson(
            "T-" + orgnr, "Testskjema paragraf", orgnr, "TESTETATEN", "Periodisk rapportering", lovDatokode,
            henvisning: "§ 42"));

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(1, resultat.HenvisningerFunnet);
        Assert.Equal(1, resultat.ParagrafmatcherFunnet);

        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        var referanse = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == handling.Id);
        Assert.Equal("§ 42", referanse.KildeHenvisningFritekst); // verbatim, ikke normalisert (tiltak 1)
        Assert.Equal($"{eli}/§42", referanse.TilEid); // paragraf-nivå, ikke dokument-nivå (tiltak 2)
        Assert.Equal(rettskilde.Id, referanse.TilRettskildeId);
    }

    [Fact]
    public async Task Enkel_paragrafhenvisning_persisteres_men_faller_tilbake_til_dokumentniva_uten_bekreftet_node()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        var lovDatokode = NyLovDatokode();
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(lovDatokode, out _);
        await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        await LeggTilRettskildeAsync(db, eli); // MERK: ingen RettskildeNodeEntitet lagt til for "§42".
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson(
            "T-" + orgnr, "Testskjema paragraf uten node", orgnr, "TESTETATEN", "Periodisk rapportering", lovDatokode,
            henvisning: "§ 42"));

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(1, resultat.HenvisningerFunnet); // fritekst ER funnet ...
        Assert.Equal(0, resultat.ParagrafmatcherFunnet); // ... men IKKE bekreftet mot noen ekte node.

        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        var referanse = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == handling.Id);
        Assert.Equal("§ 42", referanse.KildeHenvisningFritekst); // tiltak 1: bevart uansett.
        Assert.Equal(eli, referanse.TilEid); // tiltak 2 IKKE forsøkt gjettet — dagens dokumentnivå-oppførsel.
    }

    [Fact]
    public async Task Plural_paragraftegn_med_spenn_loses_aldri_til_enkelt_node_selv_om_en_av_dem_finnes()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        var lovDatokode = NyLovDatokode();
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(lovDatokode, out _);
        await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        var rettskilde = await LeggTilRettskildeAsync(db, eli);
        // §1 finnes faktisk som ekte node — beviser at "§§ 1 til 5" IKKE feilaktig delvis-matcher den.
        await LeggTilParagrafNodeAsync(db, rettskilde.Id, eli, "§1");
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson(
            "T-" + orgnr, "Testskjema spenn", orgnr, "TESTETATEN", "Periodisk rapportering", lovDatokode,
            henvisning: "§§ 1 til 5")); // bekreftet ekte formvariant i korpuset (jf. "§§ 1-3" m.fl.).

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(1, resultat.HenvisningerFunnet);
        Assert.Equal(0, resultat.ParagrafmatcherFunnet); // "§§" er alltid flertall — aldri forsøkt løst.

        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        var referanse = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == handling.Id);
        Assert.Equal("§§ 1 til 5", referanse.KildeHenvisningFritekst);
        Assert.Equal(eli, referanse.TilEid);
    }

    [Fact]
    public async Task Tvetydig_bindestrekform_bekreftes_mot_ekte_node_ikke_gjettet_som_spenn()
    {
        await using var db = _fixture.NyDbContext();

        // "§ 96-97" (bekreftet ekte i korpuset) er tvetydig: KAN være lovens egen sammensatte
        // paragrafnummerform (som "§ 4-1"), eller et spenn "§96 til §97" — se
        // OppgaveregisterHandlingSeed sin klassekommentar punkt (c). Her finnes en EKTE node med
        // akkurat denne Eid-en, så resolusjon SKAL skje (bekreftet, ikke gjettet).
        var orgnr = NyOrgnr();
        var lovDatokode = NyLovDatokode();
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(lovDatokode, out _);
        await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        var rettskilde = await LeggTilRettskildeAsync(db, eli);
        await LeggTilParagrafNodeAsync(db, rettskilde.Id, eli, "§96-97");
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson(
            "T-" + orgnr, "Testskjema bindestrek", orgnr, "TESTETATEN", "Periodisk rapportering", lovDatokode,
            henvisning: "§96-97"));

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(1, resultat.ParagrafmatcherFunnet);
        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        var referanse = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == handling.Id);
        Assert.Equal($"{eli}/§96-97", referanse.TilEid);
    }

    [Fact]
    public async Task Null_henvisning_gir_ingen_fritekst_og_telles_ikke_som_funnet()
    {
        await using var db = _fixture.NyDbContext();

        var orgnr = NyOrgnr();
        var lovDatokode = NyLovDatokode();
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(lovDatokode, out _);
        await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        await LeggTilRettskildeAsync(db, eli);
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJson(
            "T-" + orgnr, "Testskjema uten henvisning", orgnr, "TESTETATEN", "Periodisk rapportering", lovDatokode,
            henvisning: null));

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(0, resultat.HenvisningerFunnet);
        Assert.Equal(0, resultat.ParagrafmatcherFunnet);

        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        var referanse = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == handling.Id);
        Assert.Null(referanse.KildeHenvisningFritekst);
        Assert.Equal(eli, referanse.TilEid);
    }

    [Fact]
    public async Task Forskrift_har_egen_henvisning_uavhengig_av_lovhjemmelens_egen()
    {
        await using var db = _fixture.NyDbContext();

        // Bekreftet ekte struktur 2026-09-10: en nøstet forskrift kan ha en HELT ANNEN henvisning enn
        // loven den er hjemlet i — begge skal persisteres, hver med sin EGEN fritekst, ikke loven sin
        // gjenbrukt for forskriften (se SkjemaForskriftJson sin doc-kommentar).
        var orgnr = NyOrgnr();
        var lovDatokode = NyLovDatokode();
        var forskriftDatokode = NyForskriftDatokode();
        var lovEli = LovdataIdentifikatorer.AvledEliFraDatokode(lovDatokode, out _);
        var forskriftEli = LovdataIdentifikatorer.AvledEliFraDatokode(forskriftDatokode, out _);
        await LeggTilVirksomhetAsync(db, "Testetaten " + orgnr, orgnr);
        await LeggTilRettskildeAsync(db, lovEli);
        var forskrift = await LeggTilRettskildeAsync(db, forskriftEli);
        await LeggTilParagrafNodeAsync(db, forskrift.Id, forskriftEli, "§3");

        var raaLovhjemler = $$"""
        [
          {
            "dato": "{{lovDatokode}}",
            "henvisning": "Kapittel 5",
            "forskrifter": [ { "dato": "{{forskriftDatokode}}", "henvisning": "§ 3" } ]
          }
        ]
        """;
        var kilde = await NyKildeAsync(db, "T-" + orgnr, SkjemaJsonMedRaaLovhjemler(
            "T-" + orgnr, "Testskjema lov og forskrift", orgnr, "TESTETATEN", raaLovhjemler));

        var resultat = await KjorSeedIsolertAsync(db, kilde.EksternId);

        Assert.Equal(2, resultat.LovhjemlerTotalt); // loven selv + den nøstede forskriften.
        Assert.Equal(2, resultat.HenvisningerFunnet);
        Assert.Equal(1, resultat.ParagrafmatcherFunnet); // kun forskriftens "§ 3" er entydig løsbar.

        var handling = await db.Handlinger.SingleAsync(h => h.EksternKildeId == kilde.Id);
        var referanser = await db.HandlingRegelverksreferanser.Where(r => r.HandlingId == handling.Id).ToListAsync();
        Assert.Equal(2, referanser.Count);

        var lovReferanse = Assert.Single(referanser, r => r.TilEid == lovEli);
        Assert.Equal("Kapittel 5", lovReferanse.KildeHenvisningFritekst);

        var forskriftReferanse = Assert.Single(referanser, r => r.TilEid == $"{forskriftEli}/§3");
        Assert.Equal("§ 3", forskriftReferanse.KildeHenvisningFritekst);
    }
}
