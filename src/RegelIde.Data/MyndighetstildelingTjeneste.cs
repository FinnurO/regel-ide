using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Ett paragraf-/leddspenn (docs/20 §7.1, `[LÅST]`: strukturert, ikke fritekst). <c>TilEid</c>
/// null betyr et enkeltstående punkt, ikke et spenn.</summary>
public sealed record ParagrafspennPar(string FraEid, string? TilEid);

/// <summary>
/// Register for <see cref="MyndighetstildelingEntitet"/> (docs/20 §2.5) — kobler et gruppebegrep til en
/// konkret virksomhet, hjemlet i en forskrift/et delegeringsvedtak. Gyldighet arves i utgangspunktet fra
/// <see cref="MyndighetstildelingEntitet.HjemmelRettskildeId"/>s
/// <see cref="RettskildeEntitet.Status"/>/<see cref="RettskildeEntitet.GyldigTil"/>, og kan i tillegg
/// avgrenses av tildelingens egne <see cref="MyndighetstildelingEntitet.GyldigFra"/>/
/// <see cref="MyndighetstildelingEntitet.GyldigTil"/> (docs/29 §Del B) — se <see cref="ErGjeldendeAsync"/>.
/// </summary>
public sealed class MyndighetstildelingTjeneste(RegelIdeDbContext db)
{
    public async Task<MyndighetstildelingEntitet> OpprettAsync(
        Guid gruppeBegrepId, Guid virksomhetId, Guid hjemmelRettskildeId, IReadOnlyList<ParagrafspennPar> paragrafspenn,
        string? vilkaar, string opprettetAv, DateOnly? gyldigFra = null, DateOnly? gyldigTil = null, CancellationToken ct = default)
    {
        var gruppeBegrep = await db.Begreper.FirstOrDefaultAsync(
            b => b.Id == gruppeBegrepId && b.Begrepskategori == "gruppe" && b.Entitetsstatus == "gjeldende", ct);
        if (gruppeBegrep is null)
        {
            throw new ArgumentException($"Fant ingen gruppebegrep med id '{gruppeBegrepId}'. Ingen gjettet fallback.");
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

        if (gyldigFra is not null && gyldigTil is not null && gyldigFra.Value > gyldigTil.Value)
        {
            throw new ArgumentException("GyldigFra kan ikke være etter GyldigTil. Ingen gjettet fallback.");
        }

        var tildeling = new MyndighetstildelingEntitet
        {
            Id = Guid.NewGuid(),
            GruppeBegrepId = gruppeBegrepId,
            VirksomhetId = virksomhetId,
            HjemmelRettskildeId = hjemmelRettskildeId,
            ParagrafspennJson = JsonSerializer.Serialize(paragrafspenn, JsonSerialiseringHjelper.Innstillinger),
            Vilkaar = vilkaar,
            GyldigFra = gyldigFra,
            GyldigTil = gyldigTil,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Myndighetstildelinger.Add(tildeling);
        // Attribuert til den opprettende brukerens EGEN virksomhet (RBAC-prinsippet, docs/20 §0 pkt. 3)
        // — men KUN for Proveniens-sporing, ikke lagret på selve raden (myndighetstildelinger er delt,
        // nasjonal referansedata, samme som gruppebegrepet den peker på).
        db.Proveniens.Add(ProveniensHjelper.NyRad("myndighetstildeling", tildeling.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return tildeling;
    }

    /// <summary>
    /// <paramref name="kunGjeldende"/> = <c>true</c> filtrerer bort tildelinger som ikke er gjeldende
    /// akkurat nå (docs/29 §Del B, punkt 2 — filteret må FAKTISK kobles inn, ikke bare finnes som et
    /// informativt felt) — se <see cref="ErGjeldendeAsync"/> for hva «gjeldende» betyr.
    /// </summary>
    public async Task<List<MyndighetstildelingEntitet>> AlleForGruppeBegrepAsync(Guid gruppeBegrepId, bool kunGjeldende = false, CancellationToken ct = default)
    {
        var alle = await db.Myndighetstildelinger.Where(m => m.GruppeBegrepId == gruppeBegrepId).ToListAsync(ct);
        if (!kunGjeldende) return alle;
        var resultat = new List<MyndighetstildelingEntitet>();
        foreach (var m in alle)
        {
            if (await ErGjeldendeAsync(m, ct: ct)) resultat.Add(m);
        }
        return resultat;
    }

    /// <summary>Se <see cref="AlleForGruppeBegrepAsync"/> for <paramref name="kunGjeldende"/>-semantikken.</summary>
    public async Task<List<MyndighetstildelingEntitet>> AlleForVirksomhetAsync(Guid virksomhetId, bool kunGjeldende = false, CancellationToken ct = default)
    {
        var alle = await db.Myndighetstildelinger.Where(m => m.VirksomhetId == virksomhetId).ToListAsync(ct);
        if (!kunGjeldende) return alle;
        var resultat = new List<MyndighetstildelingEntitet>();
        foreach (var m in alle)
        {
            if (await ErGjeldendeAsync(m, ct: ct)) resultat.Add(m);
        }
        return resultat;
    }

    /// <summary>Deserialiserer <see cref="MyndighetstildelingEntitet.ParagrafspennJson"/> til den
    /// strukturerte formen (docs/20 §7.1).</summary>
    public static IReadOnlyList<ParagrafspennPar> LesParagrafspenn(MyndighetstildelingEntitet tildeling) =>
        JsonSerializer.Deserialize<List<ParagrafspennPar>>(tildeling.ParagrafspennJson, JsonSerialiseringHjelper.Innstillinger) ?? [];

    /// <summary>
    /// Gyldighet er en KOMBINASJON (docs/29 §Del B) av tildelingens EGEN
    /// <see cref="MyndighetstildelingEntitet.GyldigFra"/>/<see cref="MyndighetstildelingEntitet.GyldigTil"/>
    /// (de aller fleste tildelinger setter ALDRI disse) OG hjemmelens
    /// <c>Status</c>/<c>GyldigTil</c> (docs/20 §2.5, uendret): en tildeling er gjeldende KUN når BEGGE
    /// sier ja — <c>Status != 'Opphevet'</c> på hjemmelen, og <paramref name="somDato"/> ligger innenfor
    /// BÅDE hjemmelens og tildelingens egne datoer (der satt).
    /// </summary>
    public async Task<bool> ErGjeldendeAsync(MyndighetstildelingEntitet tildeling, DateOnly? somDato = null, CancellationToken ct = default)
    {
        var dato = somDato ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (tildeling.GyldigFra is not null && tildeling.GyldigFra.Value > dato) return false;
        if (tildeling.GyldigTil is not null && tildeling.GyldigTil.Value < dato) return false;

        var hjemmel = await db.Rettskilder.FirstOrDefaultAsync(r => r.Id == tildeling.HjemmelRettskildeId, ct);
        if (hjemmel is null) return false; // hjemmelen finnes ikke lenger — ingen gjettet fallback, bare ikke gjeldende.
        if (hjemmel.Status == "Opphevet") return false;
        return hjemmel.GyldigTil is null || hjemmel.GyldigTil.Value >= dato;
    }
}
