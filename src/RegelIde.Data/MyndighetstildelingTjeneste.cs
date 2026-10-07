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
    /// <summary>Lukket vokabular for <see cref="MyndighetstildelingEntitet.Status"/> — se dens kommentar.</summary>
    private static readonly string[] GyldigeStatuser = ["foreslatt_av_ai", "validert"];

    /// <param name="status">
    /// [Ny, issue #285 AC5, KI-oppdagelse-runden] <c>'validert'</c> (default) — UENDRET oppførsel for
    /// alle eksisterende kallere (menneske-drevet flyt fra PR #284/issue #164/#283). Kun
    /// <see cref="VirksomhetOgGruppeKiOppdagelseTjeneste"/> sender <c>'foreslatt_av_ai'</c>, og sender da
    /// ALLTID <paramref name="aiForslagVersjon"/> også — se <see cref="ProveniensHjelper.NyForslagRad"/>.
    /// </param>
    public async Task<MyndighetstildelingEntitet> OpprettAsync(
        Guid gruppeBegrepId, Guid virksomhetId, Guid hjemmelRettskildeId, IReadOnlyList<ParagrafspennPar> paragrafspenn,
        string? vilkaar, string opprettetAv, DateOnly? gyldigFra = null, DateOnly? gyldigTil = null, CancellationToken ct = default,
        string status = "validert", string? aiForslagVersjon = null)
    {
        if (!GyldigeStatuser.Contains(status))
        {
            throw new ArgumentException($"Ugyldig status '{status}'. Gyldige verdier: {string.Join(", ", GyldigeStatuser)}. Ingen gjettet fallback.");
        }
        if (status == "foreslatt_av_ai" && aiForslagVersjon is null)
        {
            throw new ArgumentException("aiForslagVersjon må oppgis når status er 'foreslatt_av_ai'. Ingen gjettet fallback.");
        }
        // [ENDRET, issue #310] Målet kan være ethvert begrep med gruppefunksjon (klasse/rolle/omrade/organ,
        // og gjenværende 'gruppe') — Nodetyper.MedGruppefunksjon. Konsolideres til én kanttabell i #311.
        var gruppeBegrep = await db.Begreper.FirstOrDefaultAsync(
            b => b.Id == gruppeBegrepId && Nodetyper.MedGruppefunksjon.Contains(b.Begrepskategori!)
                 && b.Entitetsstatus == "gjeldende", ct);
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
            Status = status,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Myndighetstildelinger.Add(tildeling);
        // Attribuert til den opprettende brukerens EGEN virksomhet (RBAC-prinsippet, docs/20 §0 pkt. 3)
        // — men KUN for Proveniens-sporing, ikke lagret på selve raden (myndighetstildelinger er delt,
        // nasjonal referansedata, samme som gruppebegrepet den peker på).
        db.Proveniens.Add(status == "foreslatt_av_ai"
            ? ProveniensHjelper.NyForslagRad("myndighetstildeling", tildeling.Id, virksomhetId: null, opprettetAv, aiForslagVersjon!)
            : ProveniensHjelper.NyRad("myndighetstildeling", tildeling.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return tildeling;
    }

    /// <summary>
    /// [Ny, issue #285 AC6, KI-oppdagelse-runden] Et menneske bekrefter en KI-foreslått tildeling —
    /// samme "sett status + GodkjentAv i Proveniens"-mønster som <see cref="BegrepsregisterTjeneste.SettStatusAsync"/>.
    /// Kun rader med <c>Status == "foreslatt_av_ai"</c> kan godkjennes her (en allerede validert rad har
    /// ingenting å godkjenne — ingen gjettet fallback).
    /// </summary>
    public async Task<MyndighetstildelingEntitet?> GodkjennAsync(Guid id, string godkjentAv, CancellationToken ct = default)
    {
        var tildeling = await db.Myndighetstildelinger.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (tildeling is null) return null;
        if (tildeling.Status != "foreslatt_av_ai")
        {
            throw new ArgumentException(
                $"Tildelingen har status '{tildeling.Status}' — kun 'foreslatt_av_ai'-rader kan godkjennes her.");
        }
        tildeling.Status = "validert";
        tildeling.SistEndretAv = godkjentAv;
        tildeling.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var proveniens = ProveniensHjelper.NyRad("myndighetstildeling", tildeling.Id, virksomhetId: null, "validert", godkjentAv);
        proveniens.GodkjentAv = godkjentAv;
        db.Proveniens.Add(proveniens);
        await db.SaveChangesAsync(ct);
        return tildeling;
    }

    /// <summary>
    /// [Ny, issue #285 AC6, KI-oppdagelse-runden] «Avvis» for en KI-foreslått tildeling — ekte
    /// <c>Remove</c>, samme presedens som <see cref="VirksomhetRelasjonregisterTjeneste.SlettAsync"/>.
    /// Bevisst BEGRENSET til <c>Status == "foreslatt_av_ai"</c>: en allerede validert tildeling (uansett
    /// om den opprinnelig kom fra et menneske eller en tidligere godkjent KI-rad) har ingen slette-vei i
    /// det hele tatt i dag (ingen eksisterende endepunkt) — denne runden utvider bevisst IKKE det, kun
    /// avvisning av egne, ennå-ubekreftede KI-forslag.
    /// </summary>
    public async Task<bool> AvvisAsync(Guid id, CancellationToken ct = default)
    {
        var tildeling = await db.Myndighetstildelinger.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (tildeling is null) return false;
        if (tildeling.Status != "foreslatt_av_ai")
        {
            throw new ArgumentException(
                $"Tildelingen har status '{tildeling.Status}' — kun 'foreslatt_av_ai'-rader kan avvises/slettes her.");
        }
        db.Myndighetstildelinger.Remove(tildeling);
        await db.SaveChangesAsync(ct);
        return true;
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
