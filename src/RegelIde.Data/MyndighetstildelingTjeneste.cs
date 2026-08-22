using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Ett paragraf-/leddspenn (docs/20 §7.1, `[LÅST]`: strukturert, ikke fritekst). <c>TilEid</c>
/// null betyr et enkeltstående punkt, ikke et spenn.</summary>
public sealed record ParagrafspennPar(string FraEid, string? TilEid);

/// <summary>
/// Register for <see cref="MyndighetstildelingEntitet"/> (docs/20 §2.5) — kobler et rollebegrep til en
/// konkret virksomhet, hjemlet i en forskrift/et delegeringsvedtak. Gyldighet er IKKE et eget felt her:
/// den arves fra <see cref="MyndighetstildelingEntitet.HjemmelRettskildeId"/>s
/// <see cref="RettskildeEntitet.Status"/>/<see cref="RettskildeEntitet.GyldigTil"/> — se
/// <see cref="ErGjeldendeAsync"/>.
/// </summary>
public sealed class MyndighetstildelingTjeneste(RegelIdeDbContext db)
{
    public async Task<MyndighetstildelingEntitet> OpprettAsync(
        Guid rolleBegrepId, Guid virksomhetId, Guid hjemmelRettskildeId, IReadOnlyList<ParagrafspennPar> paragrafspenn,
        string? vilkaar, string opprettetAv, CancellationToken ct = default)
    {
        var rolleBegrep = await db.Begreper.FirstOrDefaultAsync(
            b => b.Id == rolleBegrepId && b.Begrepskategori == "rolle" && b.Entitetsstatus == "gjeldende", ct);
        if (rolleBegrep is null)
        {
            throw new ArgumentException($"Fant ingen rollebegrep med id '{rolleBegrepId}'. Ingen gjettet fallback.");
        }
        if (!await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{virksomhetId}'. Ingen gjettet fallback.");
        }
        if (!await db.Rettskilder.AnyAsync(r => r.Id == hjemmelRettskildeId, ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde med id '{hjemmelRettskildeId}'. Ingen gjettet fallback.");
        }
        if (paragrafspenn.Count == 0)
        {
            throw new ArgumentException("Paragrafspenn kan ikke være tomt — ingen gjettet fallback (docs/20 §7.1).");
        }
        foreach (var par in paragrafspenn)
        {
            if (!await db.RettskildeNoder.AnyAsync(n => n.Eid == par.FraEid, ct))
            {
                throw new ArgumentException($"Fant ingen rettskilde-node med eId '{par.FraEid}'. Ingen gjettet fallback.");
            }
            if (par.TilEid is not null && !await db.RettskildeNoder.AnyAsync(n => n.Eid == par.TilEid, ct))
            {
                throw new ArgumentException($"Fant ingen rettskilde-node med eId '{par.TilEid}'. Ingen gjettet fallback.");
            }
        }

        var tildeling = new MyndighetstildelingEntitet
        {
            Id = Guid.NewGuid(),
            RolleBegrepId = rolleBegrepId,
            VirksomhetId = virksomhetId,
            HjemmelRettskildeId = hjemmelRettskildeId,
            ParagrafspennJson = JsonSerializer.Serialize(paragrafspenn, JsonSerialiseringHjelper.Innstillinger),
            Vilkaar = vilkaar,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Myndighetstildelinger.Add(tildeling);
        // Attribuert til den opprettende brukerens EGEN virksomhet (RBAC-prinsippet, docs/20 §0 pkt. 3)
        // — men KUN for Proveniens-sporing, ikke lagret på selve raden (myndighetstildelinger er delt,
        // nasjonal referansedata, samme som rollebegrepet den peker på).
        db.Proveniens.Add(ProveniensHjelper.NyRad("myndighetstildeling", tildeling.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return tildeling;
    }

    public Task<List<MyndighetstildelingEntitet>> AlleForRolleBegrepAsync(Guid rolleBegrepId, CancellationToken ct = default) =>
        db.Myndighetstildelinger.Where(m => m.RolleBegrepId == rolleBegrepId).ToListAsync(ct);

    public Task<List<MyndighetstildelingEntitet>> AlleForVirksomhetAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.Myndighetstildelinger.Where(m => m.VirksomhetId == virksomhetId).ToListAsync(ct);

    /// <summary>Deserialiserer <see cref="MyndighetstildelingEntitet.ParagrafspennJson"/> til den
    /// strukturerte formen (docs/20 §7.1).</summary>
    public static IReadOnlyList<ParagrafspennPar> LesParagrafspenn(MyndighetstildelingEntitet tildeling) =>
        JsonSerializer.Deserialize<List<ParagrafspennPar>>(tildeling.ParagrafspennJson, JsonSerialiseringHjelper.Innstillinger) ?? [];

    /// <summary>
    /// Gyldighet ARVES fra hjemmelen (docs/20 §2.5) — ingen egne datoer på tildelingen selv. En
    /// tildeling er gjeldende når hjemmelen er det: <c>Status != 'Opphevet'</c> og (ingen
    /// <c>GyldigTil</c>-dato, eller den ligger i fremtiden relativt til <paramref name="somDato"/>).
    /// </summary>
    public async Task<bool> ErGjeldendeAsync(MyndighetstildelingEntitet tildeling, DateOnly? somDato = null, CancellationToken ct = default)
    {
        var hjemmel = await db.Rettskilder.FirstOrDefaultAsync(r => r.Id == tildeling.HjemmelRettskildeId, ct);
        if (hjemmel is null) return false; // hjemmelen finnes ikke lenger — ingen gjettet fallback, bare ikke gjeldende.
        if (hjemmel.Status == "Opphevet") return false;
        var dato = somDato ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return hjemmel.GyldigTil is null || hjemmel.GyldigTil.Value >= dato;
    }
}
