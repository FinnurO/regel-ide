using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// docs/13-backlog.md — Altinn skjemaoversikt-krypehøsteren (rått høstelag, se <see cref="EksternKildeEntitet"/>).
/// To grupper tester, bevisst splittet (se oppgavebeskrivelsen for feature/altinn-hostere):
/// <para>
/// (1) RENE PARSE-FUNKSJONER (<see cref="AltinnSkjemaoversiktHenter.HentEtater"/>,
/// <see cref="AltinnSkjemaoversiktHenter.HentTjenesteStier"/>, <see cref="AltinnSkjemaoversiktHenter.ParseTjenesteside"/>)
/// testes mot de TO EKTE HTML-fixturene (<see cref="Testdata.LesSkjemaoversiktIndeksside"/>/
/// <see cref="Testdata.LesSkjemaoversiktAdvokatside"/>, verifisert live mot info.altinn.no) — ikke syntetisk
/// minimal HTML, siden det er nettopp EKTE markup-kvirker (se
/// <see cref="AltinnSkjemaoversiktHenter"/>s klassekommentar punkt (a)) en syntetisk fixture ville skjult.
/// MERK: den ekte <c>skjemaoversikt-advokat.html</c>-fixturen har faktisk SEKS
/// <c>&lt;details&gt;</c>-seksjoner, ikke fem som først antatt da fixturen ble hentet inn — testene under
/// verifiserer det faktiske, opptalte innholdet i fixturen.
/// </para>
/// <para>
/// (2) ORKESTRERINGEN (<see cref="AltinnSkjemaoversiktHenter.HentAltAsync"/>) testes med en STUBBET
/// <see cref="HttpMessageHandler"/> — én syntetisk minimal indeksside (1 etatlenke) og én syntetisk
/// minimal etatside (1 tjenestelenke), men den ENDELIGE tjenestesiden er den samme EKTE
/// advokat-fixturen — samme "ikke fake HELE ~200-etats-indeksen, kun de to dype nivåene trenger ikke
/// være ekte i orkestreringstesten" som oppgaven ber om.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AltinnSkjemaoversiktHenterTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AltinnSkjemaoversiktHenterTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------- Gruppe 1: rene parse-funksjoner mot ekte fixtures ----------

    [Fact]
    public void HentEtater_finner_ekte_etater_og_ekskluderer_kategori_kvirken()
    {
        var etater = AltinnSkjemaoversiktHenter.HentEtater(Testdata.LesSkjemaoversiktIndeksside());

        // Ekte, bekreftet antall 2-segments-lenker på indekssiden (85) minus den ene "kategori"-kvirken
        // (samme liste-markup som en ekte etat, men er en UI-filterlenke — se klassekommentaren punkt (a)).
        Assert.Equal(84, etater.Count);
        Assert.DoesNotContain(etater, e => e.Sti == "/skjemaoversikt/kategori/");

        Assert.Contains(etater, e => e.Sti == "/skjemaoversikt/advokattilsynet/" && e.Navn == "Advokattilsynet");
        Assert.Contains(etater, e => e.Sti == "/skjemaoversikt/a-ordningen/" && e.Navn == "A-ordningen");
        Assert.Contains(etater, e => e.Sti == "/skjemaoversikt/bronnoysundregistrene/");
        Assert.Contains(etater, e => e.Sti == "/skjemaoversikt/skatteetaten/");

        // Ingen duplikater — Sti er dedupliseringsnøkkelen.
        Assert.Equal(etater.Count, etater.Select(e => e.Sti).Distinct().Count());
    }

    [Fact]
    public void HentTjenesteStier_finner_kun_lenker_som_matcher_den_gitte_etatens_egen_slug()
    {
        var html = Testdata.LesSkjemaoversiktIndeksside();

        // Indekssidens egen "populære tjenester"-utvalg inneholder faktisk 3-segments-lenker for enkelte
        // etater — brukes her som ekte fixture-data for den generiske 3-segments-uttrekkingslogikken,
        // selv om denne funksjonen i produksjon kalles på en etats EGEN underside (se klassekommentaren).
        var bronnoysund = AltinnSkjemaoversiktHenter.HentTjenesteStier(html, "/skjemaoversikt/bronnoysundregistrene/");
        Assert.Equal(3, bronnoysund.Count);
        Assert.Contains("/skjemaoversikt/bronnoysundregistrene/arsregnskap/", bronnoysund);
        Assert.Contains("/skjemaoversikt/bronnoysundregistrene/arsregnskap-for-frivillig-virksomhet-foreninger/", bronnoysund);
        Assert.Contains("/skjemaoversikt/bronnoysundregistrene/samordnet-registermelding-registrering-av-nye-og-endring-av-eksisterende-foretak-og-enheter/", bronnoysund);

        var aOrdningen = AltinnSkjemaoversiktHenter.HentTjenesteStier(html, "/skjemaoversikt/a-ordningen/");
        Assert.Equal(["/skjemaoversikt/a-ordningen/a-melding-bestill-avstemmingsinformasjon/"], aOrdningen);

        // "kategori"s egne 3-segments-lenker (/skjemaoversikt/kategori/for-privatperson/ osv.) skal IKKE
        // dukke opp for en annen etats slug — beviser at filtreringen faktisk er PR ETAT, ikke "alle
        // 3-segments-lenker på siden".
        var advokattilsynet = AltinnSkjemaoversiktHenter.HentTjenesteStier(html, "/skjemaoversikt/advokattilsynet/");
        Assert.Empty(advokattilsynet);
    }

    [Fact]
    public void HentTjenesteStier_kaster_pa_ugyldig_etat_sti()
    {
        Assert.Throws<ArgumentException>(() =>
            AltinnSkjemaoversiktHenter.HentTjenesteStier(Testdata.LesSkjemaoversiktIndeksside(), "/skjemaoversikt/"));
    }

    [Fact]
    public void ParseTjenesteside_leser_tittel_seksjoner_og_eksterne_lenker_fra_ekte_advokatside()
    {
        var side = AltinnSkjemaoversiktHenter.ParseTjenesteside(
            Testdata.LesSkjemaoversiktAdvokatside(), "https://info.altinn.no/skjemaoversikt/advokattilsynet/advokat/");

        Assert.Equal("Advokat", side.Tjeneste);

        // Faktisk opptalt i den ekte fixturen: SEKS <details>-seksjoner (se klassekommentaren MERK-avsnittet).
        Assert.Equal(6, side.Seksjoner.Count);
        Assert.Equal(
            [
                "Hvem skal bruke skjemaet?",
                "Skal jeg søke om godkjenning ved etablering eller midlertidig tjenesteytelse?",
                "Hva skal jeg legge ved?",
                "Mer om skjemaet",
                "Lovhjemler for det aktuelle yrket",
                "Lovhjemler for yrkeskvalifikasjoner",
            ],
            side.Seksjoner.Select(s => s.Overskrift));
        // Innholdet skal IKKE lenger starte med overskriftsteksten (strippet fra fronten, Johanns metode).
        Assert.All(side.Seksjoner, s => Assert.False(s.Innhold.StartsWith(s.Overskrift, StringComparison.Ordinal)));
        // "Lovhjemler for det aktuelle yrket" har et FAKTISK TOMT <div class="rich-text"></div> i den ekte
        // fixturen (bekreftet ved inspeksjon — en reell kvirk/feil på selve Altinn-siden, ikke en parse-bug)
        // — innhold er derfor bevisst IKKE påkrevd ikke-tomt for alle seksjoner.
        Assert.Equal("", side.Seksjoner.Single(s => s.Overskrift == "Lovhjemler for det aktuelle yrket").Innhold);
        Assert.All(side.Seksjoner.Where(s => s.Overskrift != "Lovhjemler for det aktuelle yrket"),
            s => Assert.False(string.IsNullOrWhiteSpace(s.Innhold)));

        // af.altinn.no/am.ui.altinn.no er kjente Altinn-interne domener og skal ekskluderes —
        // de fem gjenværende eksterne lenkene er ekte innhold på siden.
        Assert.Equal(5, side.Lenker.Count);
        Assert.DoesNotContain(side.Lenker, l => l.Url.Contains("af.altinn.no"));
        Assert.DoesNotContain(side.Lenker, l => l.Url.Contains("am.ui.altinn.no"));
        Assert.Contains(side.Lenker, l => l.Url == "https://lovdata.no/dokument/NL/lov/1967-02-10" && l.Tekst.Contains("forvaltningsloven"));
        Assert.Contains(side.Lenker, l => l.Url == "https://tilsynet.no/utenlandsk-advokat-jurist/advokat-jurist-fra-utlandet");
    }

    [Fact]
    public void ParseTjenesteside_serialiserer_til_forventet_json_form()
    {
        var side = AltinnSkjemaoversiktHenter.ParseTjenesteside(
            Testdata.LesSkjemaoversiktAdvokatside(), "https://info.altinn.no/skjemaoversikt/advokattilsynet/advokat/");
        var json = JsonSerializer.Serialize(side);

        using var dokument = JsonDocument.Parse(json);
        var rot = dokument.RootElement;
        Assert.Equal("https://info.altinn.no/skjemaoversikt/advokattilsynet/advokat/", rot.GetProperty("url").GetString());
        Assert.Equal("Advokat", rot.GetProperty("tjeneste").GetString());
        Assert.Equal(JsonValueKind.Array, rot.GetProperty("lenker").ValueKind);
        Assert.Equal(JsonValueKind.Array, rot.GetProperty("seksjoner").ValueKind);
        Assert.Equal("Hvem skal bruke skjemaet?", rot.GetProperty("seksjoner")[0].GetProperty("overskrift").GetString());
    }

    // ---------- Gruppe 2: orkestrering med stubbet HttpMessageHandler ----------

    /// <summary>Returnerer ett svar per kall i rekkefølge (siste svar gjentas hvis flere kall enn oppgitt) — samme prinsipp som SekvensStubHandler andre steder i denne test-suiten.</summary>
    private sealed class SekvensStubHandler(IReadOnlyList<string> htmlSvar) : HttpMessageHandler
    {
        private int _kall;
        public int AntallKall => _kall;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = htmlSvar[Math.Min(_kall, htmlSvar.Count - 1)];
            _kall++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/html"),
            });
        }
    }

    private const string SyntetiskIndeksHtml = """
        <html><body><ul>
        <li><a href="/skjemaoversikt/advokattilsynet/">Advokattilsynet</a></li>
        </ul></body></html>
        """;

    private const string SyntetiskEtatsideHtml = """
        <html><body><ul>
        <li><a href="/skjemaoversikt/advokattilsynet/advokat/">Advokat</a></li>
        </ul></body></html>
        """;

    private AltinnSkjemaoversiktHenter LagHenter(RegelIdeDbContext db, SekvensStubHandler handler) =>
        new(new HttpClient(handler), db);

    [Fact]
    public async Task HentAltAsync_kryper_ett_syntetisk_niva_ned_til_en_ekte_tjenesteside_og_lagrer_den()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var handler = new SekvensStubHandler([SyntetiskIndeksHtml, SyntetiskEtatsideHtml, Testdata.LesSkjemaoversiktAdvokatside()]);
        var resultat = await LagHenter(db, handler).HentAltAsync();

        Assert.Equal(1, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);
        Assert.Equal(3, handler.AntallKall); // indeks + 1 etatside + 1 tjenesteside

        var rad = await db.EksterneKilder.SingleAsync(k => k.Kildetype == AltinnSkjemaoversiktHenter.Kildetype);
        Assert.Equal("/skjemaoversikt/advokattilsynet/advokat/", rad.EksternId);

        using var dokument = JsonDocument.Parse(rad.RaaJson);
        Assert.Equal("https://info.altinn.no/skjemaoversikt/advokattilsynet/advokat/", dokument.RootElement.GetProperty("url").GetString());
        Assert.Equal("Advokat", dokument.RootElement.GetProperty("tjeneste").GetString());
        Assert.Equal(6, dokument.RootElement.GetProperty("seksjoner").GetArrayLength());
    }

    [Fact]
    public async Task HentAltAsync_er_idempotent_ved_gjentatt_kjoring_uten_endring()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var forsteHandler = new SekvensStubHandler([SyntetiskIndeksHtml, SyntetiskEtatsideHtml, Testdata.LesSkjemaoversiktAdvokatside()]);
        await LagHenter(db, forsteHandler).HentAltAsync();
        var forHentetTidspunkt = (await db.EksterneKilder.SingleAsync(k => k.Kildetype == AltinnSkjemaoversiktHenter.Kildetype)).HentetTidspunkt;

        var andreHandler = new SekvensStubHandler([SyntetiskIndeksHtml, SyntetiskEtatsideHtml, Testdata.LesSkjemaoversiktAdvokatside()]);
        var resultat = await LagHenter(db, andreHandler).HentAltAsync();

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(1, resultat.Uendret);

        var antall = await db.EksterneKilder.CountAsync(k => k.Kildetype == AltinnSkjemaoversiktHenter.Kildetype);
        Assert.Equal(1, antall); // ingen duplikat ved re-kjøring

        var etterHentetTidspunkt = (await db.EksterneKilder.SingleAsync(k => k.Kildetype == AltinnSkjemaoversiktHenter.Kildetype)).HentetTidspunkt;
        Assert.Equal(forHentetTidspunkt, etterHentetTidspunkt); // uendret hash ⇒ HentetTidspunkt IKKE bumpet
    }

    [Fact]
    public async Task Unik_indeks_hindrer_duplikat_pa_kildetype_og_ekstern_id()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = AltinnSkjemaoversiktHenter.Kildetype, EksternId = "/skjemaoversikt/dup/test/",
            RaaJson = "{}", InnholdsHash = "a", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = AltinnSkjemaoversiktHenter.Kildetype, EksternId = "/skjemaoversikt/dup/test/",
            RaaJson = "{}", InnholdsHash = "b", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
