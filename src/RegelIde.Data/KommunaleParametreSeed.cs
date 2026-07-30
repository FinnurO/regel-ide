using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Testcase-data for dimensjon C i docs/12-fasit-handbok-leveranse.md ("kommunale variasjoner som
/// strukturert data, ikke fritekst") — uten faktiske DatasettVerdi-rader ville
/// veiledningsvisningens kommune-tabell aldri hatt noe reelt å vise, kun plumbing. Kjøres etter
/// <see cref="Byggesteg4VilkarstreSeed"/> (samme idempotens-mønster — global guard).
///
/// Skjenketidene er hentet fra det samme kildematerialet Johann la ved 2026-07-30
/// (skjenkebevilling-rundskriv_3.md §8.1/§8.2) — samme presedens som allerede finnes i
/// <c>data/kilder/referanser/</c> for å bruke reelle, navngitte kommuner i seed-data.
/// </summary>
public static class KommunaleParametreSeed
{
    private const string SeedBruker = "Kari Jurist";

    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        if (await db.Virksomheter.AnyAsync(v => v.Navn == "Tønsberg kommune", ct)) return; // global guard

        var klokkeslett = await db.Datasett.FirstOrDefaultAsync(d => d.Prop == "klokkeslett.tidspunkt", ct);
        if (klokkeslett is null) return; // Byggesteg4VilkarstreSeed må ha kjørt først

        var register = new DatasettregisterTjeneste(db);

        var tonsberg = new Virksomhet { Id = Guid.NewGuid(), Navn = "Tønsberg kommune", OpprettetTidspunkt = DateTimeOffset.UtcNow };
        var barum = new Virksomhet { Id = Guid.NewGuid(), Navn = "Bærum kommune", OpprettetTidspunkt = DateTimeOffset.UtcNow };
        db.Virksomheter.AddRange(tonsberg, barum);
        await db.SaveChangesAsync(ct);

        await register.SettVerdiAsync(
            klokkeslett.Id, tonsberg.Id, JsonSerializer.Serialize("08:00–02:00 (1.9.–14.5.), 08:00–03:00 (15.5.–31.8.)"),
            "Retningslinjer for behandling av salgs- og skjenkesaker 2024–2028, vedtatt av kommunestyret 12.06.2024.", SeedBruker, ct);
        await register.SettVerdiAsync(
            klokkeslett.Id, barum.Id, JsonSerializer.Serialize("07:00–03:00"),
            "Bevillingspolitiske retningslinjer 2024–2028.", SeedBruker, ct);
        // Standardregel (§8.4-mønsteret) — nasjonal norm for kommuner uten eget registrert regelsett,
        // VirksomhetId=null. Samme lovhjemmel som allerede står som juridisk grunnlag på Klokkeslettsvilkåret.
        await register.SettVerdiAsync(
            klokkeslett.Id, null, JsonSerializer.Serialize("08:00–01:00"),
            "Nasjonal norm, alkoholloven § 4-4 første ledd.", SeedBruker, ct);
    }
}
