using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RegelIde.Api.Autentisering;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Dekker sømmen mellom autentisering og resten av API-et: profilvalg, dagens header-oppførsel,
/// og oversettelsen fra Altinn-claims til en Bruker-rad. Kjører mot SQLite i minnet — det som
/// testes her er kartleggingslogikk, ikke SQL.
/// </summary>
public sealed class AutentiseringsprofilTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _tilkobling;
    private readonly RegelIdeDbContext _db;

    public AutentiseringsprofilTests()
    {
        _tilkobling = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        _tilkobling.Open();
        _db = new RegelIdeDbContext(new DbContextOptionsBuilder<RegelIdeDbContext>()
            .UseSqlite(_tilkobling).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _tilkobling.Dispose();
    }

    private static IConfiguration Konfig(params (string Nokkel, string Verdi)[] verdier) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(verdier.Select(v => new KeyValuePair<string, string?>(v.Nokkel, v.Verdi)))
            .Build();

    // ---------- Profilvalg ----------

    [Fact]
    public void Standardprofilen_er_testbruker()
        => Assert.Equal(Autentiseringsprofil.Testbruker, Autentiseringsoppsett.LesProfil(Konfig()));

    [Theory]
    [InlineData("altinn", Autentiseringsprofil.Altinn)]
    [InlineData("Altinn", Autentiseringsprofil.Altinn)]
    [InlineData("  ALTINN  ", Autentiseringsprofil.Altinn)]
    [InlineData("testbruker", Autentiseringsprofil.Testbruker)]
    public void Profil_leses_uavhengig_av_store_og_sma_bokstaver(string verdi, Autentiseringsprofil forventet)
        => Assert.Equal(forventet, Autentiseringsoppsett.LesProfil(
            Konfig((Autentiseringsoppsett.Konfigurasjonsnokkel, verdi))));

    [Fact]
    public void Ukjent_profil_feiler_ved_oppstart_i_stedet_for_a_falle_tilbake()
    {
        var feil = Assert.Throws<InvalidOperationException>(() => Autentiseringsoppsett.LesProfil(
            Konfig((Autentiseringsoppsett.Konfigurasjonsnokkel, "ansattporten"))));
        Assert.Contains("testbruker | altinn", feil.Message);
    }

    // ---------- Testbruker-profilen: uendret oppførsel ----------

    private async Task<Bruker> LeggTilBrukerAsync(string rolle = "Jurist", string? altinnBrukerId = null)
    {
        var virksomhet = await _db.Virksomheter.FirstOrDefaultAsync();
        if (virksomhet is null)
        {
            virksomhet = new Virksomhet
            {
                Id = Guid.NewGuid(), Navn = "Testkommunen", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            _db.Virksomheter.Add(virksomhet);
        }

        var bruker = new Bruker
        {
            Id = Guid.NewGuid(), Navn = "Kari Jurist", VirksomhetId = virksomhet.Id,
            Rolle = rolle, AltinnBrukerId = altinnBrukerId,
        };
        _db.Brukere.Add(bruker);
        await _db.SaveChangesAsync();
        return bruker;
    }

    [Fact]
    public async Task Testbruker_finner_bruker_fra_header()
    {
        var bruker = await LeggTilBrukerAsync();
        var kontekst = new DefaultHttpContext();
        kontekst.Request.Headers[TestbrukerKontekst.HeaderNavn] = bruker.Id.ToString();

        var funnet = await new TestbrukerKontekst(_db).FinnAsync(kontekst);

        Assert.Equal(bruker.Id, funnet?.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ikke-en-guid")]
    public async Task Testbruker_uten_gyldig_header_gir_null(string? headerverdi)
    {
        await LeggTilBrukerAsync();
        var kontekst = new DefaultHttpContext();
        if (headerverdi is not null) kontekst.Request.Headers[TestbrukerKontekst.HeaderNavn] = headerverdi;

        Assert.Null(await new TestbrukerKontekst(_db).FinnAsync(kontekst));
    }

    [Fact]
    public async Task Testbruker_med_ukjent_id_gir_null()
    {
        await LeggTilBrukerAsync();
        var kontekst = new DefaultHttpContext();
        kontekst.Request.Headers[TestbrukerKontekst.HeaderNavn] = Guid.NewGuid().ToString();

        Assert.Null(await new TestbrukerKontekst(_db).FinnAsync(kontekst));
    }

    // ---------- Altinn-profilen ----------

    private static readonly Altinninnstillinger Innstillinger = new()
    {
        Plattform = "https://platform.tt02.altinn.no",
        Cookienavn = "AltinnStudioRuntime",
        Virksomhet = "Testkommunen",
        DaglIdentifikatorer = ["1001"],
    };

    private AltinnBrukerkontekst Altinn(params string[] daglIdentifikatorer) =>
        new(_db, Innstillinger, new KonfigurertRolleoppslag(daglIdentifikatorer));

    private static DefaultHttpContext Innlogget(string altinnBrukerId, string? navn = null, string? partyId = null)
    {
        var claims = new List<Claim> { new(AltinnBrukerkontekst.BrukerIdClaim, altinnBrukerId) };
        if (navn is not null) claims.Add(new Claim(AltinnBrukerkontekst.BrukernavnClaim, navn));
        if (partyId is not null) claims.Add(new Claim(AltinnBrukerkontekst.PartyIdClaim, partyId));

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")),
        };
    }

    [Fact]
    public async Task Uinnlogget_gir_null_selv_om_claims_finnes()
    {
        // Identitet uten authenticationType er per definisjon ikke autentisert. Dette er
        // forskjellen på "tokenet ble validert" og "noen sendte oss noen claims".
        var kontekst = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(AltinnBrukerkontekst.BrukerIdClaim, "1001")])),
        };

        Assert.Null(await Altinn("1001").FinnAsync(kontekst));
    }

    [Fact]
    public async Task Innlogget_uten_bruker_id_claim_gir_null()
    {
        var kontekst = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "Test")),
        };

        Assert.Null(await Altinn().FinnAsync(kontekst));
    }

    [Fact]
    public async Task Dagl_blir_jurist()
    {
        var bruker = await Altinn("1001").FinnAsync(Innlogget("1001", navn: "Kari Kommunedirektør"));

        Assert.NotNull(bruker);
        Assert.Equal("Jurist", bruker.Rolle);
        Assert.Equal("Kari Kommunedirektør", bruker.Navn);
    }

    [Fact]
    public async Task Ikke_dagl_blir_saksbehandler()
    {
        var bruker = await Altinn("1001").FinnAsync(Innlogget("2002", navn: "Per Ansatt"));

        Assert.NotNull(bruker);
        Assert.Equal("Saksbehandler", bruker.Rolle);
    }

    [Fact]
    public async Task Uten_konfigurert_rolleoppslag_faar_alle_minst_privilegerte_rolle()
    {
        // Viktig at standardtilstanden er minst tilgang: glemmer noen å konfigurere
        // DaglIdentifikatorer, skal ingen få Jurist ved et uhell.
        var bruker = await Altinn().FinnAsync(Innlogget("1001"));

        Assert.Equal("Saksbehandler", bruker?.Rolle);
    }

    [Fact]
    public async Task Andre_innlogging_gjenbruker_samme_rad()
    {
        var forste = await Altinn("1001").FinnAsync(Innlogget("1001"));
        var andre = await Altinn("1001").FinnAsync(Innlogget("1001"));

        Assert.Equal(forste!.Id, andre!.Id);
        Assert.Equal(1, await _db.Brukere.CountAsync(b => b.AltinnBrukerId == "1001"));
    }

    [Fact]
    public async Task Rollen_endres_ikke_av_at_konfigurasjonen_endres_etter_forste_innlogging()
    {
        // Dokumenterer en bevisst begrensning: rollen settes ved provisjonering og leses ikke på
        // nytt. Blir dette et problem, er det rolleoppslaget som må kalles per forespørsel.
        await Altinn().FinnAsync(Innlogget("1001"));
        var etterpa = await Altinn("1001").FinnAsync(Innlogget("1001"));

        Assert.Equal("Saksbehandler", etterpa?.Rolle);
    }

    [Fact]
    public async Task Ulike_altinn_brukere_gir_ulike_rader_i_samme_virksomhet()
    {
        var en = await Altinn("1001").FinnAsync(Innlogget("1001"));
        var to = await Altinn("1001").FinnAsync(Innlogget("2002"));

        Assert.NotEqual(en!.Id, to!.Id);
        Assert.Equal(en.VirksomhetId, to.VirksomhetId);
        Assert.Equal(1, await _db.Virksomheter.CountAsync());
    }

    [Fact]
    public async Task Innlogget_bruker_havner_i_den_konfigurerte_virksomheten()
    {
        var bruker = await Altinn().FinnAsync(Innlogget("3003"));
        var virksomhet = await _db.Virksomheter.FirstAsync(v => v.Id == bruker!.VirksomhetId);

        Assert.Equal("Testkommunen", virksomhet.Navn);
    }

    [Fact]
    public async Task Eksisterende_virksomhet_gjenbrukes_i_stedet_for_a_duplisere()
    {
        await LeggTilBrukerAsync();
        var antallFor = await _db.Virksomheter.CountAsync();

        await Altinn().FinnAsync(Innlogget("4004"));

        Assert.Equal(antallFor, await _db.Virksomheter.CountAsync());
    }

    [Fact]
    public async Task Uten_navn_claim_faar_brukeren_et_navn_likevel()
    {
        var bruker = await Altinn().FinnAsync(Innlogget("5005"));

        Assert.NotNull(bruker);
        Assert.Contains("5005", bruker.Navn);
    }

    // ---------- Hvilke identifikatorer som teller ----------

    [Fact]
    public async Task Party_id_gir_ikke_dagl_selv_om_den_staar_i_konfigurasjonen()
    {
        // Party-id-en i tokenet er avgiveren som er valgt, ikke personen. Representerer brukeren
        // organisasjonen, står organisasjonens party der — og hadde vi matchet på den, ville
        // *alle* som representerer den samme organisasjonen fått DAGL. Dette er den mest
        // sannsynlige feilkonfigurasjonen, siden Tenor oppgir party-id side om side med userid.
        var bruker = await Altinn("9999999").FinnAsync(Innlogget("2002", partyId: "9999999"));

        Assert.Equal("Saksbehandler", bruker?.Rolle);
    }

    [Fact]
    public async Task Fodselsnummer_i_konfigurasjonen_gir_dagl_hvis_tokenet_har_claimet()
    {
        // Tolerant match: vi har ikke verifisert claim-settet i tt02, så konfigurasjonen skal
        // virke enten det er userid eller fødselsnummer som er lagt inn.
        var kontekst = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(AltinnBrukerkontekst.BrukerIdClaim, "2002"),
                new Claim(AltinnBrukerkontekst.FodselsnummerClaim, "01019012345"),
            ], authenticationType: "Test")),
        };

        var bruker = await Altinn("01019012345").FinnAsync(kontekst);

        Assert.Equal("Jurist", bruker?.Rolle);
    }

    [Fact]
    public async Task Virksomheten_faar_organisasjonsnummer_naar_det_er_konfigurert()
    {
        var innstillinger = new Altinninnstillinger
        {
            Plattform = "https://platform.tt02.altinn.no",
            Cookienavn = "AltinnStudioRuntime",
            Virksomhet = "Tigerkommunen",
            Organisasjonsnummer = "999888777",
            DaglIdentifikatorer = [],
        };

        var kontekst = new AltinnBrukerkontekst(_db, innstillinger, new KonfigurertRolleoppslag([]));
        var bruker = await kontekst.FinnAsync(Innlogget("7007"));
        var virksomhet = await _db.Virksomheter.FirstAsync(v => v.Id == bruker!.VirksomhetId);

        Assert.Equal("999888777", virksomhet.Organisasjonsnummer);
    }

    [Fact]
    public void Ingen_dagl_identifikatorer_ligger_i_kildekoden()
    {
        // Verdiene er miljøspesifikke og flyktige, og settes i appsettings.Local.json eller som
        // miljøvariabel. Står de i appsettings.json, er de på vei til GitHub. Testen leser den
        // committede filen direkte — ikke testprosjektets egen konfigurasjon.
        var sti = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "RegelIde.Api", "appsettings.json");
        Assert.True(File.Exists(sti), $"Fant ikke {Path.GetFullPath(sti)}");

        var innstillinger = Altinninnstillinger.Les(
            new ConfigurationBuilder().AddJsonFile(sti, optional: false).Build());

        Assert.Empty(innstillinger.DaglIdentifikatorer);
        Assert.Null(innstillinger.Organisasjonsnummer);
    }

    [Fact]
    public void Vis_claims_er_av_som_standard()
        => Assert.False(Altinninnstillinger.Les(Konfig()).VisClaims);

    [Fact]
    public void Velkjent_endepunkt_peker_paa_plattformens_openid_konfigurasjon()
        => Assert.Equal(
            "https://platform.tt02.altinn.no/authentication/api/v1/openid/.well-known/openid-configuration",
            Innstillinger.VelkjentEndepunkt);

    [Fact]
    public void Standardinnstillingene_peker_paa_tt02()
    {
        var innstillinger = Altinninnstillinger.Les(Konfig());

        Assert.Equal("https://platform.tt02.altinn.no", innstillinger.Plattform);
        Assert.Equal("AltinnStudioRuntime", innstillinger.Cookienavn);
        Assert.Empty(innstillinger.DaglIdentifikatorer);
    }

    [Fact]
    public void Dagl_identifikatorer_leses_fra_konfigurasjon()
    {
        var innstillinger = Altinninnstillinger.Les(Konfig(
            ("RegelIde:Altinn:DaglIdentifikatorer:0", "1001"),
            ("RegelIde:Altinn:DaglIdentifikatorer:1", "1002")));

        Assert.Equal(["1001", "1002"], innstillinger.DaglIdentifikatorer);
    }
}
