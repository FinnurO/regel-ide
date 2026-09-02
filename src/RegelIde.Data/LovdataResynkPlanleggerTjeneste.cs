namespace RegelIde.Data;

/// <summary>
/// Orkestrerer den planlagte Lovdata-resynk-sjekken (administrasjon-Lovdata-resynk, GitHub-issue #104):
/// les lagret frekvensinnstilling + siste ferdige kjøring, avgjør via <see cref="LovdataResynkPlanlegging.SkalKjoreNaa"/>
/// om det er på tide, og starter+registrerer en ny <see cref="LovdataResynkUtlost.Planlagt"/>-kjøring
/// hvis så. ASP.NET-fri (samme begrunnelse som <see cref="LovdataFullimportTjeneste"/>) — selve
/// periodiske SJEKKE-loopen (hver time, <c>Task.Delay</c>) er <c>LovdataResynkPlanleggerBakgrunnstjeneste</c>
/// i RegelIde.Api sin jobb, ikke denne klassens.
/// <para>
/// Selve arbeidet (<paramref name="kjorAsync"/> på <see cref="KjorHvisPaaTideAsync"/>) er et parameter,
/// ikke en konstruktør-avhengighet av <see cref="LovdataFullimportTjeneste"/> — samme begrunnelse som
/// <see cref="LovdataResynkKjoringTjeneste"/>s tilsvarende valg: lar HELE denne klassen (inkl. selve
/// "er det på tide"-sjekken mot en ekte database) testes med en enkel, rask lambda i stedet for et ekte
/// nettverkskall mot Lovdata, se LovdataResynkPlanleggerTjenesteTests.
/// </para>
/// </summary>
public sealed class LovdataResynkPlanleggerTjeneste(
    LovdataResynkInnstillingTjeneste innstillingTjeneste, LovdataResynkKjoringTjeneste kjoringTjeneste, TimeProvider klokke)
{
    /// <summary>Kjører+registrerer en ny planlagt kjøring hvis intervallet er utløpt OG ingen annen
    /// kjøring allerede pågår. Returnerer true hvis en kjøring faktisk ble startet.</summary>
    public async Task<bool> KjorHvisPaaTideAsync(
        Func<CancellationToken, Task<LovdataFullimportResultat>> kjorAsync, CancellationToken ct = default)
    {
        // Sjekket FØR innstilling/siste-kjøring under: en kjøring som allerede pågår (manuelt trigget,
        // eller forrige planlagte runde som ennå ikke er ferdig) skal ALDRI overlappes av en ny.
        if (await kjoringTjeneste.ErKjoringPagaendeAsync(ct)) return false;

        var innstilling = await innstillingTjeneste.HentAsync(ct);
        var siste = await kjoringTjeneste.SisteFerdigeKjoringAsync(ct);
        if (!LovdataResynkPlanlegging.SkalKjoreNaa(klokke.GetUtcNow(), siste?.StartetTidspunkt, innstilling.IntervallTimer))
        {
            return false;
        }

        await kjoringTjeneste.KjorOgRegistrerAsync(LovdataResynkUtlost.Planlagt, utlostAvBruker: null, kjorAsync, ct);
        return true;
    }
}
