namespace RegelIde.Data;

/// <summary>
/// Selve "er det på tide å kjøre igjen"-avgjørelsen for planlagt Lovdata-resynk (administrasjon-
/// Lovdata-resynk, GitHub-issue #104) — en ren, statisk funksjon UTEN databasetilgang eller ekte klokke,
/// bevisst trukket ut slik at den er trivielt testbar med rene <see cref="DateTimeOffset"/>-verdier
/// (ingen <c>Task.Delay</c>/klokke-mocking nødvendig i testene, se LovdataResynkPlanleggingTests).
/// Selve orkestreringen (les innstilling+siste kjøring fra database, kall denne, evt. kjør) ligger i
/// <see cref="LovdataResynkPlanleggerTjeneste"/>.
/// </summary>
public static class LovdataResynkPlanlegging
{
    /// <summary>
    /// Bevisst enkel modell (brukerens eget ønske: "IKKE et fullverdig cron-bibliotek, hold det enkelt")
    /// — <paramref name="intervallTimer"/> null eller ≤0 betyr "aldri automatisk" (kun oppstart/manuell).
    /// Har aldri kjørt før (<paramref name="sisteKjoringStartet"/> er null) betyr "kjør nå" — ellers kjør
    /// når minst <paramref name="intervallTimer"/> timer har gått siden forrige kjørings START-tidspunkt.
    /// </summary>
    public static bool SkalKjoreNaa(DateTimeOffset naa, DateTimeOffset? sisteKjoringStartet, int? intervallTimer)
    {
        if (intervallTimer is not { } timer || timer <= 0) return false;
        if (sisteKjoringStartet is null) return true;
        return naa - sisteKjoringStartet.Value >= TimeSpan.FromHours(timer);
    }
}
