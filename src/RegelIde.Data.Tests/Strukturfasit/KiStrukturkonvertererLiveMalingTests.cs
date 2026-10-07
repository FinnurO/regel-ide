using System.Diagnostics;
using RegelIde.Data.Strukturkonvertering;
using Xunit.Abstractions;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #308 strukturmodell-KI, 2026-10-07] LIVE-måling av <see cref="KiStrukturkonverterer"/> mot den
/// konfigurerte KI-leverandøren over alle fem fasitkildene (#308 akseptansekriterium 1).
/// <para>
/// <b>Ikke en del av vanlig testkjøring:</b> <c>[Trait("Category", "LiveIntegration")]</c>, samme
/// eksklusjon som <see cref="EksternNavneoppslagTjenesteLiveTests"/>/<see cref="LovdataBulkHenterTests"/>
/// (se RegelIde.Data.Tests.csproj), OG bak miljøvariabelen <c>REGELIDE_KI_LIVE_MALING=1</c> (se
/// <see cref="Gate"/> for hvorfor traiten alene ikke holder). Kjøres bevisst, og bare når en ny måling er
/// ønsket: <c>REGELIDE_KI_LIVE_MALING=1 dotnet test src/RegelIde.Data.Tests --filter "FullyQualifiedName~KiStrukturkonvertererLiveMalingTests"</c>.
/// Den koster tokens, tar flere minutter, og skal IKKE brukes til å iterere prompten mot fasiten (#308) —
/// rapporten teller hvor mange ganger den er kjørt.
/// </para>
/// <para>
/// <b>Konfig:</b> samme <c>RegelIde:KiAgent:*</c> som API-et, lest fra API-ets user-secrets eller
/// miljøvariabler <c>RegelIde__KiAgent__*</c> (<see cref="LiveKiKonfig"/>). Nøkler skrives aldri ut.
/// </para>
/// <para>
/// Ingen database (#308 akseptansekriterium 4). Skriver bare <c>ki-utdata/</c> og <c>maling-ki.md</c>.
/// </para>
/// </summary>
[Trait("Category", "LiveIntegration")]
public class KiStrukturkonvertererLiveMalingTests(ITestOutputHelper output)
{
    /// <summary>
    /// 4 samtidige kall: nok til at fem kilder (~360 000 tegn nodetekst, ~65 kall) går på minutter, ikke en
    /// time, og lavt nok til ikke å likne en last mot leverandøren. Ikke målt mot noen rate-grense.
    /// </summary>
    private static readonly KiStrukturkonverteringsvalg Valg = new(MaksParallelleKall: 4);

    /// <summary>
    /// [Ny, #308, 2026-10-07] Eksplisitt gate i tillegg til traiten. Målt i denne saken: en
    /// <c>dotnet test --filter "FullyQualifiedName~Strukturfasit"</c> ERSTATTER csproj-ens
    /// <c>VSTestTestCaseFilter</c> (<c>Category!=LiveIntegration</c>) i stedet for å kombineres med den —
    /// den første live-målingen ble startet nettopp slik, ved et uhell, av kommandoen som skulle kjøre
    /// enhetstestene. Uten gaten ville hver slik filterkjøring kostet en full KI-kjøring.
    /// </summary>
    private const string Gate = "REGELIDE_KI_LIVE_MALING";

    [Fact]
    public async Task Ki_maling_over_alle_fem_kilder_skriver_utdata_og_rapport()
    {
        if (Environment.GetEnvironmentVariable(Gate) != "1")
        {
            output.WriteLine($"Hoppet over: sett {Gate}=1 for å kjøre live-målingen (koster tokens, se klassekommentaren).");
            return;
        }

        var config = LiveKiKonfig.Les();
        // Et KI-svar på ~6000 tegn inn kan ta over ett minutt; HttpClient sin standard (100 s) er for kort
        // (docs/14: observert timeout mot HostYourAI).
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(6) };
        var konverterer = new KiStrukturkonverterer(new KiAgentKlientOpenAiKompatibel(http, config), config, valg: Valg);
        var modell = konverterer.Modell;
        Assert.NotEqual("stub-v1", modell); // RegelIde:KiAgent:Leverandor må være OpenAiKompatibel

        var kilder = StrukturfasitLeser.LesAlle();
        var forrige = KiMaling.LesKjoring();
        var logg = Path.Combine(Path.GetTempPath(), "regelide-ki-strukturmaling.log");
        File.WriteAllText(logg, $"Start {DateTimeOffset.Now:O}, modell {modell}\n");

        // Minste kilde først: en systematisk feil (f.eks. at alle svar er ugyldig JSON) synes i loggen
        // etter ett minutt, ikke etter domstolloven.
        var perKilde = new List<KiKjoringKilde>();
        foreach (var kilde in kilder.OrderBy(k => k.Noder.Sum(n => n.Tekst?.Length ?? 0)))
        {
            var sw = Stopwatch.StartNew();
            var r = await konverterer.KonverterMedRapportAsync(kilde.Grunnlag);
            sw.Stop();
            KiMaling.LagreDokument(kilde.Navn, r.Dokument);
            perKilde.Add(new KiKjoringKilde(kilde.Navn, r.AntallDeler, r.AntallKall, r.UgyldigJson, r.FeiledeKall, r.TaptNoder,
                r.InputTokens, r.OutputTokens, Math.Round(sw.Elapsed.TotalSeconds), r.Kastet, r.KastedeAktorer, r.Kallfeil));
            var linje = $"{kilde.Navn}: {r.Dokument.Utsagn.Count} utsagn, {r.Kastet.Count} kastet, {r.AntallKall} kall, " +
                        $"{r.UgyldigJson} ugyldig JSON, {r.FeiledeKall} feilet, tokens {r.InputTokens}/{r.OutputTokens}, {sw.Elapsed.TotalSeconds:0} s";
            File.AppendAllText(logg, linje + "\n");
            output.WriteLine(linje);
        }

        var kjoring = new KiKjoring(
            modell,
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz"),
            (forrige?.Kjoringsnummer ?? 0) + 1,
            KiMaling.Instruksavtrykk(KiStrukturkonverterer.SystemInstruks),
            Valg,
            perKilde.OrderBy(k => k.Kilde, StringComparer.Ordinal).ToList());
        KiMaling.LagreKjoring(kjoring);

        var rapport = KiMalerapport.Lag(KiMalerapport.Mal(kilder, kjoring), kjoring);
        File.WriteAllText(Path.Combine(StrukturfasitLeser.FasitMappe, "maling-ki.md"), rapport);
        output.WriteLine(rapport);

        // Ingen kvalitetsterskel — dette er den første målingen (#308). Kravet er bare at kjøringen
        // faktisk ga en måling: minst ett gyldig utsagn per kilde.
        Assert.All(perKilde, k => Assert.True(k.AntallKall > k.UgyldigJson + k.FeiledeKall, $"{k.Kilde}: ingen gyldige svar."));
    }
}
