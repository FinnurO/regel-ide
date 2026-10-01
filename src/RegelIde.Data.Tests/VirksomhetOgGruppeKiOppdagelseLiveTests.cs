using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RegelIde.Kildekonvertering;
using Xunit.Abstractions;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #285 AC1/AC7] Ekte nettverkskall — MOT Lovdatas offisielle bulk-API (samme mekanisme som
/// <see cref="LovdataBulkHenterTests"/>) OG mot den ekte, konfigurerte KI-leverandøren (HostYourAI/
/// DeepSeek, <c>RegelIde:KiAgent:*</c> user-secrets), IKKE <see cref="KiAgentKlientStub"/>. Bevisst, ikke
/// mocket — <c>[Trait("Category", "LiveIntegration")]</c>, samme eksklusjon fra vanlig <c>dotnet test</c>
/// som resten av «live»-testene (se RegelIde.Data.Tests.csproj).
/// <para>
/// <b>Formål (issue #285 AC1/AC7, «mål omfanget FØRST»):</b> kjør KI-agenten mot minst issue #263s
/// testcase (Energidepartementet/Energiklagenemnda, forskrift <c>bd0a5f8d-…</c> — ELI
/// <c>https://lovdata.no/eli/forskrift/2019/10/24/1420/nor</c>, «Forskrift om Energiklagenemnda»,
/// hentet HER direkte fra Lovdata, ikke rekonstruert) og sammenlign mot det deterministiske
/// <see cref="NavnekandidatOppdagelseTjeneste.SveipAsync"/> — dekker KI-en noe REELT nytt (organrelasjoner,
/// roller) regex-mønsteret går glipp av? Funnene er dokumentert i PR-beskrivelsen for issue #285 (ikke
/// bare her) — denne testklassen ER selve verifiseringen, kjørbar på nytt av hvem som helst med
/// gyldige <c>RegelIde:KiAgent:*</c>-secrets.
/// </para>
/// <para>
/// <b>Ingen secrets i kildekoden</b> — leser dem fra samme sted <c>dotnet user-secrets</c> selv lagrer
/// dem (<c>%APPDATA%/Microsoft/UserSecrets/{RegelIde.Api sin UserSecretsId}/secrets.json</c>), akkurat
/// som en utvikler som har kjørt <c>dotnet user-secrets set</c> for RegelIde.Api ville hatt tilgjengelig.
/// Mangler filen (et miljø uten konfigurerte secrets), hopper hver test over selv med en tydelig
/// <see cref="ITestOutputHelper"/>-melding i stedet for å feile — «ekte KI-verifisering ikke mulig i
/// dette miljøet» er en gyldig, dokumentert utfall (issue #285s egen instruks).
/// </para>
/// </summary>
[Trait("Category", "LiveIntegration")]
[Collection(DataTestCollection.Navn)]
public class VirksomhetOgGruppeKiOppdagelseLiveTests(EmbeddedPostgresFixture fixture, ITestOutputHelper output)
{
    // Samme UserSecretsId som src/RegelIde.Api/RegelIde.Api.csproj — se den filen.
    private const string ApiUserSecretsId = "3fd1c918-27b2-48d3-99c3-db1e1de95c28";

    /// <summary>Leser den FLATE (ett nivå, "A:B:C" → verdi — nøyaktig formen <c>dotnet user-secrets</c>
    /// selv lagrer) JSON-fila manuelt via <see cref="JsonDocument"/> i stedet for
    /// <c>AddJsonFile</c>/<c>AddUserSecrets</c> — unngår en ny NuGet-pakkeavhengighet
    /// (Microsoft.Extensions.Configuration.Json/UserSecrets) i et testprosjekt bare for ÉN diagnostisk,
    /// aldri-i-CI live-test.</summary>
    private static IConfiguration LastEkteKiConfig()
    {
        var sti = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "UserSecrets", ApiUserSecretsId, "secrets.json");
        var verdier = new Dictionary<string, string?>();
        if (File.Exists(sti))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(sti));
            foreach (var felt in doc.RootElement.EnumerateObject())
            {
                verdier[felt.Name] = felt.Value.GetString();
            }
        }
        return new ConfigurationBuilder().AddInMemoryCollection(verdier).Build();
    }

    private static NavnekandidatOppdagelseTjeneste NyNavnekandidatOppdagelse(RegelIdeDbContext db, HttpClient http) => new(
        db, new VirksomhetsbegrepTjeneste(db), new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)),
        new VirksomhetOppslagTjeneste(db), new EksternNavneoppslagTjeneste(http, db),
        new MyndighetstildelingTjeneste(db), new GruppeMedlemskapTjeneste(db), new VirksomhetRelasjonregisterTjeneste(db));

    [Fact]
    public async Task AC1_AC7_Energiklagenemnda_forskrift_ekte_ki_mot_deterministisk_sveip()
    {
        var config = LastEkteKiConfig();
        if (config["RegelIde:KiAgent:BaseUrl"] is null)
        {
            output.WriteLine("INGEN RegelIde:KiAgent:*-secrets funnet i dette miljøet — ekte KI-verifisering " +
                              "ikke mulig her (issue #285 AC1s dokumenterte fallback). Testen gjør ingenting.");
            return;
        }

        using var http = new HttpClient();
        var henter = new LovdataBulkHenter(http);
        // Ekte, live henting — samme ELI som nettside/data/rettskilder/katalog.json sin
        // "bd0a5f8d-deaf-4bd9-9b7a-893ffba29393"-rad («Forskrift om Energiklagenemnda»,
        // https://lovdata.no/eli/forskrift/2019/10/24/1420/nor) peker på, IKKE en rekonstruert tekst.
        var raaHtml = await henter.HentRaaHtmlAsync("FOR-2019-10-24-1420");
        var konvertert = LovdataKonverterer.Konverter(raaHtml, new DateOnly(2026, 9, 30));
        output.WriteLine($"Hentet: {konvertert.Metadata.Tittel} ({konvertert.Metadata.Eli})");

        await using var db = fixture.NyDbContext();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(konvertert);

        // Energidepartementet finnes normalt i registeret via DepartementSeed/OrganisasjonsregisterSeed
        // (orgnr 977161630, se DepartementSeedTests) — seedes IKKE automatisk av EmbeddedPostgresFixture
        // (kun migrasjoner), så testen oppretter den selv for at relasjons-/rolleoppløsningen
        // (VirksomhetOppslagTjeneste) i det hele tatt skal ha noe å finne.
        var energidepartementet = new Virksomhet { Id = Guid.NewGuid(), Navn = "ENERGIDEPARTEMENTET", Organisasjonsnummer = "977161630" };
        db.Virksomheter.Add(energidepartementet);
        await db.SaveChangesAsync();
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(energidepartementet.Id, "Energidepartementet", "live-test");

        // --------- Deterministisk baseline: RENE mønsterfunksjoner, INGEN SNL/SSR-nettverkskall ---------
        // [Rettet] SveipAsync (ekte KLASSIFISERING) gjør ETT SNL- og/eller SSR-oppslag per UNIKT
        // "virksomhet"-treff i teksten — for en hel, ekte forskrift kan det være mange, og disse to
        // eksterne, ratelimit-udokumenterte tjenestene (docs/31) er utenfor vår kontroll. For selve
        // AC1-SAMMENLIGNINGEN (dekker KI noe regex-MØNSTERET i seg selv går glipp av) holder det å
        // kjøre de rene, DB-/nettverksfrie mønsterfunksjonene direkte på teksten — se
        // NavnekandidatOppdagelseTjenesteTests "Del A" for samme prinsipp.
        var alleNoder = konvertert.Noder;
        var regexTreff = new List<(string Type, string Navn)>();
        foreach (var node in alleNoder)
        {
            if (node.Tekst is null) continue;
            foreach (var (start, lengde, kategori) in NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst(node.Tekst))
            {
                regexTreff.Add((kategori, node.Tekst.Substring(start, lengde)));
            }
            foreach (var (start, lengde, raaTekst) in NavnekandidatOppdagelseTjeneste.FinnStorBokstavKandidaterITekst(node.Tekst))
            {
                regexTreff.Add(("virksomhet (stor bokstav, uklassifisert)", raaTekst));
            }
        }
        output.WriteLine("");
        output.WriteLine($"=== Deterministisk mønstergjenkjenning (ingen SNL/SSR-klassifisering): {regexTreff.Count} rå treff ===");
        foreach (var t in regexTreff.DistinctBy(t => (t.Type, t.Navn)).OrderBy(t => t.Navn))
        {
            output.WriteLine($"  [{t.Type}] '{t.Navn}'");
        }

        // --------- KI-basert oppdagelse (ekte leverandør) ---------
        var navnekandidatOppdagelse = NyNavnekandidatOppdagelse(db, http);
        var kiKlient = new KiAgentKlientOpenAiKompatibel(http, config);
        var kiTjeneste = new VirksomhetOgGruppeKiOppdagelseTjeneste(
            db, kiKlient, config, navnekandidatOppdagelse, new VirksomhetOppslagTjeneste(db),
            new MyndighetstildelingTjeneste(db), new VirksomhetRelasjonregisterTjeneste(db), new GruppeMedlemskapTjeneste(db));
        var kiResultat = await kiTjeneste.KjorOppdagelseAsync(rettskildeId, "live-test-ki");

        output.WriteLine("");
        output.WriteLine($"=== KI-oppdagelse (ekte {config["RegelIde:KiAgent:Leverandor"]}/{config["RegelIde:KiAgent:Modell"]}): " +
                          $"{kiResultat.Kandidater.Count} forslag, {kiResultat.InputTokens} input-/{kiResultat.OutputTokens} output-tokens ===");
        foreach (var k in kiResultat.Kandidater)
        {
            output.WriteLine($"  [{k.Type}] '{k.Navn}' — navnekandidat={(k.NavnekandidatId is not null ? "opprettet" : $"NEI ({k.NavnekandidatFeil})")}, " +
                              $"rolle={(k.MyndighetstildelingId is not null ? "OPPRETTET" : $"nei ({k.RolleIkkeOpprettetGrunn})")}, " +
                              $"relasjon={(k.VirksomhetRelasjonId is not null ? "OPPRETTET" : $"nei ({k.RelasjonIkkeOpprettetGrunn})")}, " +
                              $"gruppeAvGruppe={(k.GruppeMedlemskapId is not null ? "OPPRETTET" : $"nei ({k.GruppeAvGruppeIkkeOpprettetGrunn})")}");
        }
        if (kiResultat.Melding is not null) output.WriteLine($"  Melding: {kiResultat.Melding}");

        // --------- Sammenligning (AC1) ---------
        var deterministiskeNavn = new HashSet<string>(regexTreff.Select(t => t.Navn), StringComparer.OrdinalIgnoreCase);
        var kunHosKi = kiResultat.Kandidater.Where(k => !deterministiskeNavn.Contains(k.Navn)).ToList();
        output.WriteLine("");
        output.WriteLine($"=== KI fant {kunHosKi.Count} navn regex-sveipet IKKE fant: {string.Join(", ", kunHosKi.Select(k => k.Navn))} ===");
        var kiFantRolleEllerRelasjon = kiResultat.Kandidater.Any(k => k.MyndighetstildelingId is not null || k.VirksomhetRelasjonId is not null);
        output.WriteLine($"=== KI foreslo minst én rolle/relasjon regex-sveipet PRINSIPIELT ALDRI kan foreslå: {kiFantRolleEllerRelasjon} ===");

        // Eneste harde assert: agenten svarte i det hele tatt (rørledningen virker) — selve DEKNINGS-
        // sammenligningen over er dokumentert i output/PR-beskrivelsen, ikke en hard assert her, siden
        // et ekte modellsvar kan variere fra kjøring til kjøring (samme "ikke et deterministisk orakel"-
        // forbehold som resten av byggesteg 5).
        Assert.True(kiResultat.Kandidater.Count > 0 || kiResultat.Melding is not null);
    }
}
