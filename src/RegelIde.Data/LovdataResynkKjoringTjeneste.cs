using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Bokføring av <see cref="LovdataResynkKjoringEntitet"/>-rader (administrasjon-Lovdata-resynk,
/// GitHub-issue #104) — brukt av ALLE tre triggerveier (<see cref="LovdataResynkUtlost"/>):
/// <c>LovdataFullimportBakgrunnstjeneste</c> (Oppstart), det manuelle trigger-endepunktet (Manuell) og
/// <see cref="LovdataResynkPlanleggerTjeneste"/> (Planlagt).
/// <para>
/// Selve <see cref="LovdataFullimportTjeneste.KjorAsync"/>-kallet er BEVISST IKKE en konstruktør-
/// avhengighet her, men et parameter (<c>Func&lt;CancellationToken, Task&lt;LovdataFullimportResultat&gt;&gt;</c>)
/// på <see cref="FullforKjoringAsync"/>/<see cref="KjorOgRegistrerAsync"/> — to grunner: (1) det lar
/// alle bokførings-metodene testes uten et ekte, tregt nettverkskall mot Lovdata (se
/// LovdataResynkKjoringTjenesteTests, som sender inn en enkel lambda), og (2) det manuelle
/// trigger-endepunktet trenger å opprette raden SYNKRONT i request-scopen (for å returnere id-en
/// umiddelbart) men kjøre selve arbeidet i en SEPARAT DI-scope (se Program.cs) — <see cref="StartKjoringAsync"/>
/// og <see cref="FullforKjoringAsync"/> er derfor delt i to separate metoder nettopp for å støtte det.
/// </para>
/// </summary>
public sealed class LovdataResynkKjoringTjeneste(RegelIdeDbContext db)
{
    /// <summary>
    /// Oppretter en ny <see cref="LovdataResynkStatus.Pagar"/>-rad og returnerer dens Id. Selve arbeidet
    /// skjer i en ETTERFØLGENDE <see cref="FullforKjoringAsync"/>-kall (typisk i en annen DI-scope) —
    /// se klassekommentaren.
    /// </summary>
    public async Task<Guid> StartKjoringAsync(string utlost, string? utlostAvBruker, CancellationToken ct = default)
    {
        var kjoring = new LovdataResynkKjoringEntitet
        {
            Id = Guid.NewGuid(),
            Utlost = utlost,
            UtlostAvBruker = utlostAvBruker,
            Status = LovdataResynkStatus.Pagar,
            StartetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.LovdataResynkKjoringer.Add(kjoring);
        await db.SaveChangesAsync(ct);
        return kjoring.Id;
    }

    /// <summary>Finnes det en kjøring som fortsatt pågår? Brukt til å avvise overlappende kjøringer —
    /// både det manuelle trigger-endepunktet (409 Conflict) og den planlagte sjekken (hopper stille
    /// over denne runden, prøver igjen neste sjekk) bruker denne.</summary>
    public Task<bool> ErKjoringPagaendeAsync(CancellationToken ct = default) =>
        db.LovdataResynkKjoringer.AnyAsync(k => k.Status == LovdataResynkStatus.Pagar, ct);

    /// <summary>Siste FERDIGE kjøring (Fullført eller Feilet — ALDRI en fortsatt pågående rad), nyeste
    /// først. Brukt av <see cref="LovdataResynkPlanleggerTjeneste"/> til å avgjøre om intervallet siden
    /// forrige kjøring er utløpt.</summary>
    public Task<LovdataResynkKjoringEntitet?> SisteFerdigeKjoringAsync(CancellationToken ct = default) =>
        db.LovdataResynkKjoringer
            .Where(k => k.Status != LovdataResynkStatus.Pagar)
            .OrderByDescending(k => k.StartetTidspunkt)
            .FirstOrDefaultAsync(ct);

    /// <summary>Kjører <paramref name="kjorAsync"/> og oppdaterer raden <paramref name="kjoringId"/> med
    /// utfallet (Fullført + tellerne fra resultatet, eller Feilet + feilmelding) — raden må allerede
    /// finnes, se <see cref="StartKjoringAsync"/>. Kaster videre (etter å ha registrert Feilet) ved
    /// unntak, slik at eksisterende kalleres egen try/catch-logging (LovdataFullimportBakgrunnstjeneste,
    /// BackgroundService-loopen) er uendret.</summary>
    public async Task<LovdataFullimportResultat> FullforKjoringAsync(
        Guid kjoringId, Func<CancellationToken, Task<LovdataFullimportResultat>> kjorAsync, CancellationToken ct = default)
    {
        try
        {
            var resultat = await kjorAsync(ct);
            await RegistrerFullfortAsync(kjoringId, resultat, ct);
            return resultat;
        }
        catch (Exception ex)
        {
            // Egen, ukansellerbar CT for selve skrivingen -- en kjøring avbrutt av app-shutdown skal
            // likevel FÅ registrert at den ble avbrutt, ikke stå evig igjen som "Pågår" i historikken.
            var feilmelding = ex is OperationCanceledException ? "Avbrutt (app-avslutning)." : ex.Message;
            await RegistrerFeiletAsync(kjoringId, feilmelding, CancellationToken.None);
            throw;
        }
    }

    /// <summary>Bekvemmelighet: <see cref="StartKjoringAsync"/> + <see cref="FullforKjoringAsync"/> i ett
    /// kall — riktig når ingen andre trenger id-en FØR arbeidet er ferdig (appoppstart, planlagt sjekk).
    /// Det manuelle trigger-endepunktet bruker IKKE denne (se klassekommentaren).</summary>
    public async Task<LovdataFullimportResultat> KjorOgRegistrerAsync(
        string utlost, string? utlostAvBruker, Func<CancellationToken, Task<LovdataFullimportResultat>> kjorAsync,
        CancellationToken ct = default)
    {
        var kjoringId = await StartKjoringAsync(utlost, utlostAvBruker, ct);
        return await FullforKjoringAsync(kjoringId, kjorAsync, ct);
    }

    private async Task RegistrerFullfortAsync(Guid kjoringId, LovdataFullimportResultat resultat, CancellationToken ct)
    {
        var kjoring = await db.LovdataResynkKjoringer.FindAsync([kjoringId], ct)
            ?? throw new InvalidOperationException($"Fant ingen lovdata_resynk_kjoringer-rad med id '{kjoringId}'.");

        kjoring.Status = LovdataResynkStatus.Fullfort;
        kjoring.FullfortTidspunkt = DateTimeOffset.UtcNow;
        kjoring.Nye = resultat.Nye;
        kjoring.NyeVersjoner = resultat.NyeVersjoner;
        kjoring.Uendret = resultat.Uendret;
        kjoring.Feilet = resultat.Feilet;
        kjoring.TotaltBehandlet = resultat.TotaltBehandlet;
        await db.SaveChangesAsync(ct);
    }

    private async Task RegistrerFeiletAsync(Guid kjoringId, string feilmelding, CancellationToken ct)
    {
        var kjoring = await db.LovdataResynkKjoringer.FindAsync([kjoringId], ct);
        if (kjoring is null) return; // raden burde alltid finnes (StartKjoringAsync), men skal aldri kaste HER oppå et allerede reelt unntak

        kjoring.Status = LovdataResynkStatus.Feilet;
        kjoring.FullfortTidspunkt = DateTimeOffset.UtcNow;
        kjoring.Feilmelding = feilmelding;
        await db.SaveChangesAsync(ct);
    }
}
