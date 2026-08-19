using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// docs/13-backlog.md — <see cref="KommuneTjenesteHenter"/> (sjette kilde i det rå høstelaget, se
/// <see cref="EksternKildeEntitet"/>). FIL-basert som <see cref="TjenestelisteImporter"/>, men strukturelt
/// ulikt nok (array av KOMMUNE-objekter, hver med egen <c>records[]</c>) til å ha fått en egen klasse.
/// Fixturen (ni ekte rader/tre ekte kommuner, <see cref="Testdata.LesKommuneTjenesteHosting"/>) inneholder
/// bevisst den ekte url-kollisjonen mellom to distinkte kommuner som begge heter "Herøy" — se
/// <see cref="KommuneTjenesteHenter"/>s klassekommentar punkt (a)/(b).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class KommuneTjenesteHenterTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KommuneTjenesteHenterTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly string NiRecorderTreKommunerJson = Testdata.LesKommuneTjenesteHosting();

    private const string AltaOrgnr = "944588132";
    private const string HeroyNordlandOrgnr = "872417982";
    private const string HeroyMoreOgRomsdalOrgnr = "964978840";

    /// <summary>Den ekte kolliderende url-en — delt av begge Herøy-kommunene i fixturen, se klassekommentaren.</summary>
    private const string HeroyKollisjonUrl = "https://skjema.heroy.kommune.no/skjema/HER152/Kontakt_oss";

    private const string BoligtomterUrl = "https://skjema.no/alta/5403_18";

    /// <summary>
    /// Identisk med <see cref="NiRecorderTreKommunerJson"/> bortsett fra Alta-kommunens
    /// "Boligtomter kommunale - Søknad"-records <c>beskrivelse</c> — brukes til å teste at re-import KUN
    /// oppdaterer den ene endrede raden. Målrettet på selve url-linjen (unik i hele fixturen) for å unngå
    /// å treffe andre records med samme "Bygg og eiendom"-kategori eller tom beskrivelse ved et uhell.
    /// </summary>
    private static readonly string NiRecorderEnEndretJson = NiRecorderTreKommunerJson.Replace(
        "\"url\": \"https://skjema.no/alta/5403_18\",\r\n        \"kategori\": \"Bygg og eiendom\",\r\n        \"beskrivelse\": \"\",",
        "\"url\": \"https://skjema.no/alta/5403_18\",\r\n        \"kategori\": \"Bygg og eiendom\",\r\n        \"beskrivelse\": \"ENDRET testbeskrivelse.\",")
        .Replace(
        "\"url\": \"https://skjema.no/alta/5403_18\",\n        \"kategori\": \"Bygg og eiendom\",\n        \"beskrivelse\": \"\",",
        "\"url\": \"https://skjema.no/alta/5403_18\",\n        \"kategori\": \"Bygg og eiendom\",\n        \"beskrivelse\": \"ENDRET testbeskrivelse.\",");

    [Fact]
    public void Fixture_har_faktisk_en_endret_rad()
    {
        // Sikrer at String.Replace over faktisk traff — hvis fixturens whitespace/linjeskift-stil endres
        // senere uten at denne strengen oppdateres, skal testen som bruker NiRecorderEnEndretJson feile
        // tydelig her, ikke stille bli en no-op-test.
        Assert.NotEqual(NiRecorderTreKommunerJson, NiRecorderEnEndretJson);
        Assert.Contains("ENDRET testbeskrivelse.", NiRecorderEnEndretJson);
    }

    // ---------- Sammensatt identitetsnøkkel (fokusert enhetstest) ----------

    [Fact]
    public void BeregnEksternId_kombinerer_organisasjonsnummer_og_url_med_skilletegn()
    {
        var eksternId = KommuneTjenesteHenter.BeregnEksternId(AltaOrgnr, BoligtomterUrl);

        Assert.Equal("944588132::https://skjema.no/alta/5403_18", eksternId);
    }

    /// <summary>
    /// Selve kjernen i hele oppgaven: to ulike organisasjonsnummer + SAMME url skal gi to ULIKE nøkler —
    /// beviser at den sammensatte nøkkelen faktisk løser den ekte Herøy-url-kollisjonen, ikke bare i teorien.
    /// </summary>
    [Fact]
    public void BeregnEksternId_med_samme_url_men_ulikt_organisasjonsnummer_gir_to_distinkte_nokler()
    {
        var heroyNordland = KommuneTjenesteHenter.BeregnEksternId(HeroyNordlandOrgnr, HeroyKollisjonUrl);
        var heroyMoreOgRomsdal = KommuneTjenesteHenter.BeregnEksternId(HeroyMoreOgRomsdalOrgnr, HeroyKollisjonUrl);

        Assert.NotEqual(heroyNordland, heroyMoreOgRomsdal);
        Assert.StartsWith(HeroyNordlandOrgnr, heroyNordland);
        Assert.StartsWith(HeroyMoreOgRomsdalOrgnr, heroyMoreOgRomsdal);
    }

    // ---------- Ende-til-ende mot fixturen ----------

    [Fact]
    public async Task Forste_import_oppretter_alle_ni_rader_og_heroy_kollisjonen_forblir_to_separate_rader()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await new KommuneTjenesteHenter(db).ImporterAsync(NiRecorderTreKommunerJson);

        Assert.Equal(9, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);
        Assert.Equal(0, resultat.RecordsMedManglendeOrganisasjonsnummer); // ekte fixturedata har ingen kjente manglende orgnummer

        var rader = await db.EksterneKilder.Where(k => k.Kildetype == KommuneTjenesteHenter.Kildetype).ToListAsync();
        Assert.Equal(9, rader.Count);
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.RaaJson)));
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.InnholdsHash)));

        // Den viktigste enkelt-påstanden i hele denne runden: de to Herøy-kommunenes rader med IDENTISK
        // url må forbli to SEPARATE rader, ikke kollapset til én.
        var heroyNordlandId = KommuneTjenesteHenter.BeregnEksternId(HeroyNordlandOrgnr, HeroyKollisjonUrl);
        var heroyMoreOgRomsdalId = KommuneTjenesteHenter.BeregnEksternId(HeroyMoreOgRomsdalOrgnr, HeroyKollisjonUrl);

        var heroyNordlandRad = await db.EksterneKilder.SingleAsync(k => k.Kildetype == KommuneTjenesteHenter.Kildetype && k.EksternId == heroyNordlandId);
        var heroyMoreOgRomsdalRad = await db.EksterneKilder.SingleAsync(k => k.Kildetype == KommuneTjenesteHenter.Kildetype && k.EksternId == heroyMoreOgRomsdalId);

        Assert.NotEqual(heroyNordlandRad.Id, heroyMoreOgRomsdalRad.Id);
        Assert.Contains("\"" + HeroyKollisjonUrl + "\"", heroyNordlandRad.RaaJson);
        Assert.Contains("\"" + HeroyKollisjonUrl + "\"", heroyMoreOgRomsdalRad.RaaJson);
        Assert.Contains(HeroyNordlandOrgnr, heroyNordlandRad.RaaJson);
        Assert.Contains(HeroyMoreOgRomsdalOrgnr, heroyMoreOgRomsdalRad.RaaJson);

        // De to distinkte SingleAsync-oppslagene over BEVISER allerede at nøyaktig to rader deler denne
        // url-en (SingleAsync ville kastet hvis 0 eller >1 traff samme EksternId) — ingen ekstra
        // RaaJson.Contains-sporring nødvendig (og en slik sporring ville uansett oversettes til en
        // ugyldig LIKE (~~) mot jsonb-kolonnen på Postgres-siden).
    }

    [Fact]
    public async Task Uendret_gjenimport_er_en_no_op()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new KommuneTjenesteHenter(db).ImporterAsync(NiRecorderTreKommunerJson);
        var forHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == KommuneTjenesteHenter.Kildetype)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);

        var resultat = await new KommuneTjenesteHenter(db).ImporterAsync(NiRecorderTreKommunerJson);

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(9, resultat.Uendret);

        var antall = await db.EksterneKilder.CountAsync(k => k.Kildetype == KommuneTjenesteHenter.Kildetype);
        Assert.Equal(9, antall); // ingen duplikater ved re-import

        var etterHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == KommuneTjenesteHenter.Kildetype)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);
        Assert.Equal(forHentetTidspunkter, etterHentetTidspunkter); // uendret hash ⇒ HentetTidspunkt IKKE bumpet
    }

    [Fact]
    public async Task Endret_felt_pa_en_tjeneste_oppdaterer_kun_den_ene_raden()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await new KommuneTjenesteHenter(db).ImporterAsync(NiRecorderTreKommunerJson);

        var boligtomterId = KommuneTjenesteHenter.BeregnEksternId(AltaOrgnr, BoligtomterUrl);
        var heroyNordlandId = KommuneTjenesteHenter.BeregnEksternId(HeroyNordlandOrgnr, HeroyKollisjonUrl);

        // AsNoTracking (se OppgaveregisterHenterTests for full begrunnelse): unngår at "før"-øyeblikksbildet
        // deler identisk objektreferanse med raden det andre ImporterAsync-kallet muterer.
        var forBoligtomter = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == KommuneTjenesteHenter.Kildetype && k.EksternId == boligtomterId);
        var forHeroyNordland = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == KommuneTjenesteHenter.Kildetype && k.EksternId == heroyNordlandId);

        var resultat = await new KommuneTjenesteHenter(db).ImporterAsync(NiRecorderEnEndretJson);

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(1, resultat.Oppdaterte);
        Assert.Equal(8, resultat.Uendret);

        var etterBoligtomter = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == KommuneTjenesteHenter.Kildetype && k.EksternId == boligtomterId);
        Assert.Contains("ENDRET", etterBoligtomter.RaaJson);
        Assert.NotEqual(forBoligtomter.InnholdsHash, etterBoligtomter.InnholdsHash);
        Assert.True(etterBoligtomter.HentetTidspunkt > forBoligtomter.HentetTidspunkt);

        var etterHeroyNordland = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == KommuneTjenesteHenter.Kildetype && k.EksternId == heroyNordlandId);
        Assert.Equal(forHeroyNordland.InnholdsHash, etterHeroyNordland.InnholdsHash);
        Assert.Equal(forHeroyNordland.HentetTidspunkt, etterHeroyNordland.HentetTidspunkt);
    }

    // ---------- Manglende organisasjonsnummer (defensiv telling, syntetisk — ekte data har null tilfeller) ----------

    private const string KommuneUtenOrganisasjonsnummerJson = """
    [
      {
        "kommune": "SYNTETISK TEST KOMMUNE",
        "slug": "syntetisk-test",
        "sources": [],
        "antall_tjenester": 1,
        "records": [
          {
            "tjenestenavn": "Syntetisk testtjeneste uten kommune-organisasjonsnummer",
            "url": "https://example.test/syntetisk-manglende-orgnummer",
            "kategori": "Test",
            "beskrivelse": "Syntetisk fixture, ikke ekte produksjonsdata — konstruert for å dekke det defensivt håndterte (men i praksis ikke observerte) tilfellet der en kommune mangler organisasjonsnummer.",
            "tilbys_av": [],
            "kilder": []
          }
        ]
      }
    ]
    """;

    [Fact]
    public async Task Kommune_uten_organisasjonsnummer_telles_og_hoppes_over_ikke_faller_tilbake_til_url_alene()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await new KommuneTjenesteHenter(db).ImporterAsync(KommuneUtenOrganisasjonsnummerJson);

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(1, resultat.RecordsMedManglendeOrganisasjonsnummer);

        // Raden importeres IKKE — url alene er nettopp den utrygge nøkkelen Herøy-kollisjonen (se
        // klassekommentaren) beviser er feil for denne kilden. Manglende orgnummer telles og synliggjøres,
        // men gjettes aldri rundt.
        Assert.Equal(0, await db.EksterneKilder.CountAsync(k => k.Kildetype == KommuneTjenesteHenter.Kildetype));
    }

    [Fact]
    public async Task Unik_indeks_hindrer_duplikat_pa_kildetype_og_ekstern_id()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = KommuneTjenesteHenter.Kildetype, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "a", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = KommuneTjenesteHenter.Kildetype, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "b", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
