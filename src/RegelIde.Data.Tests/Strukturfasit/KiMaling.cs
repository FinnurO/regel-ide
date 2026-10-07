using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #308 strukturmodell-KI, 2026-10-07] Lagring av én live KI-kjøring under
/// <c>data/fasit/strukturmodell/ki-utdata/</c>: KI-dokumentet per kilde (fasit-formatet) og
/// <c>kjoring.json</c> med modell, kall, tokens og alle kastede rader.
/// <para>
/// <b>Hvorfor lagre utdataene:</b> en KI-kjøring er ikke reproduserbar (samme kontekst gir ulike svar,
/// docs/14 §8.4) og den koster. Med utdataene i repoet kan rapporten regenereres uten nettverk når
/// mønsterlaget eller fasiten endrer seg (<see cref="KiMalingRapportTests"/>), og hver KI-rad kan
/// etterprøves mot fasiten — uten å kjøre KI-en på nytt og uten å iterere prompten mot fasiten (#308).
/// </para>
/// </summary>
internal static class KiMaling
{
    public static string UtdataMappe => Path.Combine(StrukturfasitLeser.FasitMappe, "ki-utdata");
    private static string KjoringFil => Path.Combine(UtdataMappe, "kjoring.json");

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Kort, stabilt avtrykk av systeminstruksen — viser i rapporten hvilken instruks tallene gjelder.</summary>
    public static string Instruksavtrykk(string instruks) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instruks)))[..12].ToLowerInvariant();

    public static KiKjoring? LesKjoring() =>
        File.Exists(KjoringFil) ? JsonSerializer.Deserialize<KiKjoring>(File.ReadAllText(KjoringFil), Json) : null;

    public static Strukturdokument LesDokument(string kilde) =>
        JsonSerializer.Deserialize<Strukturdokument>(File.ReadAllText(Path.Combine(UtdataMappe, kilde + ".json")), Json)
        ?? throw new InvalidOperationException($"ki-utdata/{kilde}.json er tom.");

    public static void LagreDokument(string kilde, Strukturdokument dokument)
    {
        Directory.CreateDirectory(UtdataMappe);
        File.WriteAllText(Path.Combine(UtdataMappe, kilde + ".json"), JsonSerializer.Serialize(dokument, Json));
    }

    public static void LagreKjoring(KiKjoring kjoring)
    {
        Directory.CreateDirectory(UtdataMappe);
        File.WriteAllText(KjoringFil, JsonSerializer.Serialize(kjoring, Json));
    }

    /// <summary>
    /// Mønster ∪ KI som ett dokument: alle mønsterutsagn, pluss KI-utsagn som ikke har samme eId +
    /// kategori + type + fra/til-tekstform (uten skille på store/små) som et mønsterutsagn. Det er det
    /// en lagring av begge lagene ville gitt (#313), og det måles med samme treffregel som lagene hver for
    /// seg. Aktør-id-ene prefikses (<c>m</c>/<c>k</c>) så de to dokumentenes <c>a1</c> ikke kolliderer.
    /// </summary>
    public static Strukturdokument Union(Strukturdokument monster, Strukturdokument ki)
    {
        static string Nokkel(StrukturUtsagn u, Dictionary<string, StrukturAktor> a) => string.Join('\u001f',
            u.Eid, u.Kategori, u.Type,
            u.Fra is null ? null : a[u.Fra].Tekstform.ToLowerInvariant(),
            u.Til is null ? null : a[u.Til].Tekstform.ToLowerInvariant());

        var mA = monster.Aktorer.ToDictionary(a => a.Id);
        var kA = ki.Aktorer.ToDictionary(a => a.Id);
        var sett = monster.Utsagn.Select(u => Nokkel(u, mA)).ToHashSet(StringComparer.Ordinal);

        static StrukturUtsagn Prefiks(StrukturUtsagn u, string p) =>
            u with { Fra = u.Fra is null ? null : p + u.Fra, Til = u.Til is null ? null : p + u.Til };

        return monster with
        {
            AnnotertAv = "mønster ∪ KI (#308-måling)",
            Aktorer = [.. monster.Aktorer.Select(a => a with { Id = "m" + a.Id }), .. ki.Aktorer.Select(a => a with { Id = "k" + a.Id })],
            Utsagn = [.. monster.Utsagn.Select(u => Prefiks(u, "m")), .. ki.Utsagn.Where(u => !sett.Contains(Nokkel(u, kA))).Select(u => Prefiks(u, "k"))],
        };
    }
}

/// <summary>Én live-kjøring: modell, instruks, innstillinger og tall per kilde.</summary>
/// <param name="Kjoringsnummer">Teller hvor mange ganger live-målingen er kjørt (forrige kjoring.json + 1), så rapporten kan si det.</param>
internal sealed record KiKjoring(
    string Modell,
    string Tidspunkt,
    int Kjoringsnummer,
    string Instruksavtrykk,
    KiStrukturkonverteringsvalg Valg,
    IReadOnlyList<KiKjoringKilde> Kilder);

internal sealed record KiKjoringKilde(
    string Kilde,
    int AntallDeler,
    int AntallKall,
    int UgyldigJson,
    int FeiledeKall,
    int TaptNoder,
    int? InputTokens,
    int? OutputTokens,
    double Sekunder,
    IReadOnlyList<KastetRad> Kastet,
    IReadOnlyDictionary<string, int> KastedeAktorer,
    IReadOnlyList<string> Kallfeil);

/// <summary>
/// [Ny, #308] Konfig for live-målingen: de SAMME user-secrets som API-et bruker, uten å legge
/// <c>Microsoft.Extensions.Configuration.UserSecrets</c> til testprosjektet (delt fil, utenfor #308).
/// <para>
/// User-secrets er en JSON-fil per <c>UserSecretsId</c>; id-en leses fra <c>src/RegelIde.Api/RegelIde.Api.csproj</c>
/// og fila fra standardplasseringen (<c>%APPDATA%\Microsoft\UserSecrets\&lt;id&gt;\secrets.json</c>,
/// <c>~/.microsoft/usersecrets/&lt;id&gt;/secrets.json</c> utenfor Windows). Miljøvariabler
/// <c>RegelIde__KiAgent__*</c> (samme form som i EmbeddedPostgresApiFixture) overstyrer fila — det er veien i
/// CI eller på en maskin uten user-secrets. Verdiene (særlig <c>ApiKey</c>) skrives aldri ut.
/// </para>
/// </summary>
internal static class LiveKiKonfig
{
    public static IConfiguration Les()
    {
        var verdier = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var fil = UserSecretsFil();
        if (fil is not null && File.Exists(fil))
        {
            using var dok = JsonDocument.Parse(File.ReadAllText(fil));
            Flat(dok.RootElement, null, verdier);
        }
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
        {
            var navn = (string)e.Key;
            if (navn.StartsWith("RegelIde__", StringComparison.OrdinalIgnoreCase))
            {
                verdier[navn.Replace("__", ":", StringComparison.Ordinal)] = (string?)e.Value;
            }
        }
        return new ConfigurationBuilder().AddInMemoryCollection(verdier).Build();
    }

    private static string? UserSecretsFil()
    {
        var csproj = Path.GetFullPath(Path.Combine(StrukturfasitLeser.FasitMappe, "..", "..", "..", "src", "RegelIde.Api", "RegelIde.Api.csproj"));
        if (!File.Exists(csproj)) return null;
        var id = Regex.Match(File.ReadAllText(csproj), "<UserSecretsId>([^<]+)</UserSecretsId>").Groups[1].Value;
        if (id.Length == 0) return null;
        return OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", id, "secrets.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".microsoft", "usersecrets", id, "secrets.json");
    }

    private static void Flat(JsonElement e, string? prefiks, Dictionary<string, string?> ut)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject()) Flat(p.Value, prefiks is null ? p.Name : prefiks + ":" + p.Name, ut);
        }
        else if (prefiks is not null)
        {
            ut[prefiks] = e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText();
        }
    }
}
