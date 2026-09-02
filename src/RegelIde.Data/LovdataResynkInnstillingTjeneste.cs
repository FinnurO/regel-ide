namespace RegelIde.Data;

/// <summary>
/// Lese/skrive-tilgang til den ENE singleton-raden i <c>lovdata_resynk_innstilling</c> (se
/// <see cref="LovdataResynkInnstillingEntitet"/> for hele resonnementet bak modellen) — administrasjon-
/// Lovdata-resynk, GitHub-issue #104.
/// </summary>
public sealed class LovdataResynkInnstillingTjeneste(RegelIdeDbContext db)
{
    /// <summary>Alltid <c>1</c> — se <see cref="LovdataResynkInnstillingEntitet.Id"/>.</summary>
    public const int SingletonId = 1;

    /// <summary>Henter innstillingen, og oppretter den lazily med standardverdier (IntervallTimer=null,
    /// altså "aldri automatisk") ved aller første kall — ingen egen seeding/migrasjons-datarad nødvendig.</summary>
    public async Task<LovdataResynkInnstillingEntitet> HentAsync(CancellationToken ct = default)
    {
        var rad = await db.LovdataResynkInnstillinger.FindAsync([SingletonId], ct);
        if (rad is not null) return rad;

        rad = new LovdataResynkInnstillingEntitet
        {
            Id = SingletonId,
            IntervallTimer = null,
            SistEndretTidspunkt = DateTimeOffset.UtcNow,
            SistEndretAv = null,
        };
        db.LovdataResynkInnstillinger.Add(rad);
        await db.SaveChangesAsync(ct);
        return rad;
    }

    /// <summary>Oppdaterer intervallet (null/0 = aldri automatisk). Kaster <see cref="ArgumentException"/>
    /// ved negativ verdi — samme "§3.3 ingen gjettet fallback"-holdning som resten av appen
    /// (se BrukerregisterTjeneste), endepunktet i Program.cs oversetter til 400.</summary>
    public async Task<LovdataResynkInnstillingEntitet> OppdaterAsync(int? intervallTimer, string? bruker, CancellationToken ct = default)
    {
        if (intervallTimer is < 0)
        {
            throw new ArgumentException("Intervall (timer) kan ikke være negativt.", nameof(intervallTimer));
        }

        var rad = await HentAsync(ct);
        rad.IntervallTimer = intervallTimer;
        rad.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        rad.SistEndretAv = bruker;
        await db.SaveChangesAsync(ct);
        return rad;
    }
}
