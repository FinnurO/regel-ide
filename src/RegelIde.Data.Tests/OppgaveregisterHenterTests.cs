using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// docs/13-backlog.md — Oppgaveregisteret-høsteren (rått høstelag, se <see cref="EksternKildeEntitet"/>).
/// Samme stub-<see cref="HttpMessageHandler"/>-prinsipp som <see cref="KiAgentKlientOpenAiKompatibelTests"/>
/// — IKKE ekte nettverkskall som Lovdata-testenes kultur ellers i prosjektet, siden denne runden
/// eksplisitt krever at test-suiten ikke skal avhenge av nettverkstilgang. Fixturedataene er tre EKTE
/// skjemaer hentet fra det offentlige Oppgaveregister-API-et (data.brreg.no), valgt for å dekke reelle
/// kantsaker: null <c>nummer</c>, null <c>henvisning</c>, et ikke-norsk skjema, og ulike
/// <c>bruksomraader</c>-former.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class OppgaveregisterHenterTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public OppgaveregisterHenterTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class StubHandler(HttpStatusCode status, string responsBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(responsBody, Encoding.UTF8, "application/json") });
    }

    private static OppgaveregisterHenter LagHenter(RegelIdeDbContext db, string responsJson) =>
        new(new HttpClient(new StubHandler(HttpStatusCode.OK, responsJson)), db);

    /// <summary>Tre ekte skjemaer, verifisert mot live API-et rett før denne oppgaven ble laget (guid-ene "2BD"/"DP"/"AH").</summary>
    private const string TreSkjemaJson = """
    [
      {
        "navn": "Egenerklæring med revisoruttalelse",
        "eier": {
          "organisasjonsnummer": 914459265,
          "etatsnavn": "ADVOKATTILSYNET",
          "links": [{ "rel": "etat", "href": "https://data.brreg.no/enhetsregisteret/api/enheter/914459265" }]
        },
        "nummer": "1",
        "guid": "2BD",
        "statustype": "PUBLISERT",
        "godkjenningsdato": "23.10.2025 08:29:50",
        "formaal": { "fritekst": "Kontroll av (særlig) advokaters klientmiddelbehandling og regnskap ", "kategorier": [{ "kode": "KONTROLL_TILSYN", "verdi": "Kontroll og tilsyn" }] },
        "vedleggskrav": { "fritekst": null, "kategorier": [] },
        "lovhjemler": [{ "tittel": "Lov om advokater og andre som yter rettslig bistand", "henvisning": "§ 42", "dato": "LOV-2022-05-12-28", "forskrifter": [] }],
        "eoesTilpasset": { "kode": "NEI", "verdi": "Skjemaet er ikke tilpasset EØS-reglement." },
        "maalgruppe": { "naeringsgrupper": [{ "organisasjonsformer": [], "naeringskoder": [{ "verdi": "69.1", "navn": "Juridisk tjenesteyting", "type": "Naeringskode" }] }], "gjelderKunVedAnsatte": false, "begrensetMaalGruppe": false, "tilleggsopplysninger": "", "antall": 4619 },
        "spraakMaalformer": [{ "kode": "NORSK_BOKMAAL", "verdi": "Norsk-bokmål" }, { "kode": "NORSK_NYNORSK", "verdi": "Norsk-nynorsk" }],
        "nettadresser": ["https://www.altinn.no/skjemaoversikt/tilsynsradet-for-advokatvirksomhet/egenerklaring-med-revisoruttalelse/"],
        "rapporteringsformer": [{ "kode": "NAERING_STAT", "verdi": "næring" }],
        "tidsbruk": { "elektronisk": 10, "papir": null, "antallPrAar": 2300, "prosentandelPapir": 0 },
        "skjemainnhold": [{ "kode": "ANNET", "verdi": "Andre opplysninger" }, { "kode": "JURIDISK", "verdi": "Juridiske forhold" }, { "kode": "KONTAKTINFO", "verdi": "Kontaktinformasjon" }, { "kode": "PERSONAL", "verdi": "Personalopplysninger" }, { "kode": "PRODUKTER", "verdi": "Produkter (varer og tjenester)" }],
        "skjemainnholdAndreOpplysninger": "Klientmidler, opplysninger om revisor, eiendomsmeglingsoppdrag, inkassovirksomhet, antihvitvaskingsarbeid, organisering og drift, revisors uttalelse",
        "datakilder": [],
        "bruksomraader": [{ "navn": "Periodisk rapportering", "kommentar": null, "tidsfrister": [{ "date": "30", "month": "04" }], "antallPerioderPrAar": 1 }],
        "medium": { "kode": "ELEKTRONISK", "verdi": "Elektronisk" },
        "links": [{ "rel": "self", "href": "https://data.brreg.no/oppgaveregisteret/api/skjema/2BD" }]
      },
      {
        "navn": "Krav om svangerskapspenger til selvstendig næringsdrivende og frilansere",
        "eier": {
          "organisasjonsnummer": 889640782,
          "etatsnavn": "ARBEIDS- OG VELFERDSETATEN",
          "links": [{ "rel": "etat", "href": "https://data.brreg.no/enhetsregisteret/api/enheter/889640782" }]
        },
        "nummer": "NAV 14-04.10",
        "guid": "DP",
        "statustype": "PUBLISERT",
        "godkjenningsdato": "22.12.2025 15:19:52",
        "formaal": { "fritekst": "Skjemaet brukes ved krav om svangerskapspenger til selvstendig næringsdrivende og frilanser.", "kategorier": [{ "kode": "ANNET", "verdi": "Annet" }] },
        "vedleggskrav": { "fritekst": null, "kategorier": [{ "kode": "NAERINGSOPPGAVE", "verdi": "Næringsoppgave" }] },
        "lovhjemler": [{ "tittel": "Lov om folketrygd", "henvisning": "Kap 14 Del I Svangerskapspenger §14-4, annet ledd. Del II §14-11 - §14-16", "dato": "LOV-1997-02-28-19", "forskrifter": [] }],
        "eoesTilpasset": { "kode": "NEI", "verdi": "Skjemaet er ikke tilpasset EØS-reglement." },
        "maalgruppe": { "naeringsgrupper": [{ "organisasjonsformer": [{ "verdi": "ANS", "navn": "Ansvarlig selskap med solidarisk ansvar", "type": "Organisasjonsform" }, { "verdi": "AS", "navn": "Aksjeselskap", "type": "Organisasjonsform" }, { "verdi": "ASA", "navn": "Allmennaksjeselskap", "type": "Organisasjonsform" }, { "verdi": "DA", "navn": "Ansvarlig selskap med delt ansvar", "type": "Organisasjonsform" }, { "verdi": "ENK", "navn": "Enkeltpersonforetak", "type": "Organisasjonsform" }, { "verdi": "KS", "navn": "Kommandittselskap", "type": "Organisasjonsform" }, { "verdi": "PRE", "navn": "Partrederi", "type": "Organisasjonsform" }], "naeringskoder": [] }], "gjelderKunVedAnsatte": false, "begrensetMaalGruppe": true, "tilleggsopplysninger": "Kvinnelige gravide selvstendige næringsdrivende som ikke kan utføre sine vanlige arbeidsoppgaver pga fare for fosteret.", "antall": 500 },
        "spraakMaalformer": [{ "kode": "NORSK_BOKMAAL", "verdi": "Norsk-bokmål" }, { "kode": "NORSK_NYNORSK", "verdi": "Norsk-nynorsk" }],
        "nettadresser": ["https://www.nav.no/start/soknad-svangerskapspenger"],
        "rapporteringsformer": [{ "kode": "NAERING_STAT", "verdi": "næring" }],
        "tidsbruk": { "elektronisk": 15, "papir": 30, "antallPrAar": 500, "prosentandelPapir": 10 },
        "skjemainnhold": [{ "kode": "HMS", "verdi": "HMS og internkontroll" }, { "kode": "JURIDISK", "verdi": "Juridiske forhold" }, { "kode": "KONTAKTINFO", "verdi": "Kontaktinformasjon" }, { "kode": "PERSONAL", "verdi": "Personalopplysninger" }, { "kode": "SKATT", "verdi": "Skattemessige forhold" }],
        "skjemainnholdAndreOpplysninger": "",
        "datakilder": [],
        "bruksomraader": [{ "navn": "Søknad / registrering", "kommentar": null, "tidsfrister": [], "soknadstype": { "kode": "", "verdi": "" }, "rettighet": null, "rapporteringsplikt": "Krav om svangerskapspenger til selvstendig næringsdrivende\nUtsettelse eller gradert uttak av foreldrepenger" }],
        "medium": { "kode": "BEGGEDELER", "verdi": "Både elektronisk og på papir" },
        "links": [{ "rel": "self", "href": "https://data.brreg.no/oppgaveregisteret/api/skjema/DP" }]
      },
      {
        "navn": "Application for renewal of marketing authorisations",
        "eier": {
          "organisasjonsnummer": 974761122,
          "etatsnavn": "DIREKTORATET FOR MEDISINSKE PRODUKTER",
          "links": [{ "rel": "etat", "href": "https://data.brreg.no/enhetsregisteret/api/enheter/974761122" }]
        },
        "nummer": null,
        "guid": "AH",
        "statustype": "PUBLISERT",
        "godkjenningsdato": "30.10.2020 09:40:29",
        "formaal": { "fritekst": "Fornyelse av markedsføringstillatelse for legemidler ", "kategorier": [{ "kode": "KONTROLL_TILSYN", "verdi": "Kontroll og tilsyn" }] },
        "vedleggskrav": { "fritekst": null, "kategorier": [] },
        "lovhjemler": [{ "tittel": "Legemiddelloven", "henvisning": null, "dato": "LOV-1992-12-04-132", "forskrifter": [{ "tittel": "Forskrift om legemidler", "henvisning": null, "dato": "FOR-2009-12-18-1839" }] }],
        "eoesTilpasset": { "kode": "DELVIS", "verdi": "Skjemaet er endret spesielt med hensyn til EØS-tilpasning." },
        "maalgruppe": { "naeringsgrupper": [{ "organisasjonsformer": [], "naeringskoder": [{ "verdi": "21", "navn": "Produksjon av farmasøytiske råvarer og preparater", "type": "Naeringskode" }, { "verdi": "32.5", "navn": "Produksjon av medisinske og tanntekniske instrumenter og utstyr", "type": "Naeringskode" }, { "verdi": "46.46", "navn": "Engroshandel med apotekvarer og medisinske varer", "type": "Naeringskode" }] }], "gjelderKunVedAnsatte": false, "begrensetMaalGruppe": false, "tilleggsopplysninger": null, "antall": 400 },
        "spraakMaalformer": [{ "kode": "ENGELSK", "verdi": "Engelsk" }],
        "nettadresser": ["http://esubmission.ema.europa.eu/eaf/index.html"],
        "rapporteringsformer": [{ "kode": "NAERING_STAT", "verdi": "næring" }],
        "tidsbruk": { "elektronisk": 30, "papir": null, "antallPrAar": 250, "prosentandelPapir": 0 },
        "skjemainnhold": [{ "kode": "EKSTERNE", "verdi": "Eksterne aktører" }, { "kode": "KONTAKTINFO", "verdi": "Kontaktinformasjon" }, { "kode": "PRODUKTER", "verdi": "Produkter (varer og tjenester)" }],
        "skjemainnholdAndreOpplysninger": null,
        "datakilder": [],
        "bruksomraader": [{ "navn": "Søknad / registrering", "kommentar": null, "tidsfrister": [], "soknadstype": { "kode": "", "verdi": "" }, "rettighet": null, "rapporteringsplikt": null }],
        "medium": { "kode": "ELEKTRONISK", "verdi": "Elektronisk" },
        "links": [{ "rel": "self", "href": "https://data.brreg.no/oppgaveregisteret/api/skjema/AH" }]
      }
    ]
    """;

    /// <summary>Identisk med <see cref="TreSkjemaJson"/> bortsett fra "2BD"-skjemaets <c>godkjenningsdato</c>
    /// (23.10 → 24.10) — brukes til å teste at re-høsting KUN oppdaterer den ene endrede raden.</summary>
    private static readonly string TreSkjemaEndretJson = TreSkjemaJson.Replace("23.10.2025 08:29:50", "24.10.2025 08:29:50");

    [Fact]
    public async Task Forste_hosting_oppretter_tre_rader()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await LagHenter(db, TreSkjemaJson).HentAlleSkjemaAsync();

        Assert.Equal(3, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);

        var rader = await db.EksterneKilder.Where(k => k.Kildetype == OppgaveregisterHenter.Kildetype).ToListAsync();
        Assert.Equal(3, rader.Count);
        Assert.Contains(rader, r => r.EksternId == "2BD");
        Assert.Contains(rader, r => r.EksternId == "DP");
        Assert.Contains(rader, r => r.EksternId == "AH");
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.RaaJson)));
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.InnholdsHash)));
    }

    [Fact]
    public async Task Uendret_gjenhosting_er_en_no_op()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await LagHenter(db, TreSkjemaJson).HentAlleSkjemaAsync();
        var forHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == OppgaveregisterHenter.Kildetype)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);

        var resultat = await LagHenter(db, TreSkjemaJson).HentAlleSkjemaAsync();

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(3, resultat.Uendret);

        var antall = await db.EksterneKilder.CountAsync(k => k.Kildetype == OppgaveregisterHenter.Kildetype);
        Assert.Equal(3, antall); // ingen duplikater ble opprettet ved re-høsting

        var etterHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == OppgaveregisterHenter.Kildetype)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);
        Assert.Equal(forHentetTidspunkter, etterHentetTidspunkter); // uendret hash ⇒ HentetTidspunkt IKKE bumpet
    }

    [Fact]
    public async Task Endret_felt_pa_ett_skjema_oppdaterer_kun_den_raden()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await LagHenter(db, TreSkjemaJson).HentAlleSkjemaAsync();
        // AsNoTracking er nødvendig her: uten den ville "før"-øyeblikksbildet delt IDENTISK objektreferanse
        // med raden HentAlleSkjemaAsync senere muterer (samme DbContext ⇒ samme identity map), og da ville
        // "før"-verdiene stille blitt overskrevet av "etter"-mutasjonen før noen assert i det hele tatt kjørte.
        var for2bd = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == OppgaveregisterHenter.Kildetype && k.EksternId == "2BD");
        var forDp = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == OppgaveregisterHenter.Kildetype && k.EksternId == "DP");
        var forAh = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == OppgaveregisterHenter.Kildetype && k.EksternId == "AH");

        var resultat = await LagHenter(db, TreSkjemaEndretJson).HentAlleSkjemaAsync();

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(1, resultat.Oppdaterte);
        Assert.Equal(2, resultat.Uendret);

        // AsNoTracking igjen her: Postgres' timestamptz har mikrosekund-presisjon, .NET DateTimeOffset
        // har 100ns-tikk — en tracked instans som ALDRI ble skrevet på nytt ville fortsatt holdt sin
        // opprinnelige, ikke-avrundede in-memory-verdi fra første høsting, i stedet for den avrundede
        // verdien som faktisk står lagret i databasen. Uten AsNoTracking her ville DP/AH-sammenligningen
        // vært flaky på siste tikk-sifferet.
        var etter2bd = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == OppgaveregisterHenter.Kildetype && k.EksternId == "2BD");
        Assert.Contains("24.10.2025 08:29:50", etter2bd.RaaJson);
        Assert.NotEqual(for2bd.InnholdsHash, etter2bd.InnholdsHash);
        Assert.True(etter2bd.HentetTidspunkt > for2bd.HentetTidspunkt);

        var etterDp = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == OppgaveregisterHenter.Kildetype && k.EksternId == "DP");
        Assert.Equal(forDp.InnholdsHash, etterDp.InnholdsHash);
        Assert.Equal(forDp.HentetTidspunkt, etterDp.HentetTidspunkt);

        var etterAh = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == OppgaveregisterHenter.Kildetype && k.EksternId == "AH");
        Assert.Equal(forAh.InnholdsHash, etterAh.InnholdsHash);
        Assert.Equal(forAh.HentetTidspunkt, etterAh.HentetTidspunkt);
    }

    [Fact]
    public async Task Unik_indeks_hindrer_duplikat_pa_kildetype_og_ekstern_id()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = OppgaveregisterHenter.Kildetype, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "a", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = OppgaveregisterHenter.Kildetype, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "b", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
