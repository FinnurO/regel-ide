using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Veiledningskommentarer på vilkårstre-noder (docs/12-fasit-handbok-leveranse.md "Hovedfunn" +
/// dimensjon A, 2026-07-30). Samme polymorfe valideringsmønster som
/// <see cref="TekstTaggTjeneste.KobleTilEntitetAsync"/> — <see cref="VilkarstreKommentarEntitet.MalType"/>
/// avgjør hvilken tabell <see cref="VilkarstreKommentarEntitet.MalId"/> må finnes i.
/// </summary>
public sealed class VilkarstreKommentarTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeDokumenttyper = ["kommentar", "hjemmel", "praktisk-rad", "sjekkliste"];

    public Task<List<VilkarstreKommentarEntitet>> HentForNodeAsync(string malType, Guid malId, CancellationToken ct = default) =>
        db.VilkarstreKommentarer.Where(k => k.MalType == malType && k.MalId == malId)
            .OrderBy(k => k.Rekkefolge).ThenBy(k => k.OpprettetTidspunkt).ToListAsync(ct);

    public async Task<VilkarstreKommentarEntitet> OpprettAsync(
        Guid virksomhetId, string malType, Guid malId, string dokumenttype, string tekstHtml, string opprettetAv, CancellationToken ct = default)
    {
        await ValiderAsync(malType, malId, dokumenttype, ct);

        var rekkefolge = await db.VilkarstreKommentarer.Where(k => k.MalType == malType && k.MalId == malId).CountAsync(ct);
        var kommentar = new VilkarstreKommentarEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            MalType = malType,
            MalId = malId,
            Dokumenttype = dokumenttype,
            TekstHtml = KommentarTekstSanering.Saner(tekstHtml),
            Rekkefolge = rekkefolge,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.VilkarstreKommentarer.Add(kommentar);
        db.Proveniens.Add(ProveniensHjelper.NyRad("vilkarstre_kommentar", kommentar.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return kommentar;
    }

    public async Task<VilkarstreKommentarEntitet?> OppdaterAsync(
        Guid id, string dokumenttype, string tekstHtml, string endretAv, CancellationToken ct = default)
    {
        if (!GyldigeDokumenttyper.Contains(dokumenttype))
        {
            throw new ArgumentException($"Ukjent dokumenttype '{dokumenttype}'. Gyldige verdier: {string.Join(", ", GyldigeDokumenttyper)}.");
        }

        var kommentar = await db.VilkarstreKommentarer.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kommentar is null) return null;

        kommentar.Dokumenttype = dokumenttype;
        kommentar.TekstHtml = KommentarTekstSanering.Saner(tekstHtml);
        kommentar.SistEndretAv = endretAv;
        kommentar.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("vilkarstre_kommentar", kommentar.Id, kommentar.VirksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return kommentar;
    }

    /// <summary>
    /// Flytter en kommentar én posisjon opp/ned blant søsknene sine ved å BYTTE <see
    /// cref="VilkarstreKommentarEntitet.Rekkefolge"/> med naboen — aldri ved å sette en fritt valgt
    /// verdi. Se docs/12-fasit-handbok-leveranse.md "Prinsipp: rekkefølge og nummerering er alltid
    /// beregnet, aldri en redigerbar literal" (2026-07-31).
    /// </summary>
    public async Task<VilkarstreKommentarEntitet> FlyttAsync(Guid id, string retning, string endretAv, CancellationToken ct = default)
    {
        if (retning is not ("opp" or "ned"))
        {
            throw new ArgumentException($"Ukjent retning '{retning}'. Gyldige verdier: opp, ned.");
        }

        var kommentar = await db.VilkarstreKommentarer.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new ArgumentException($"Fant ingen kommentar med id '{id}'.");

        var sosken = await db.VilkarstreKommentarer
            .Where(k => k.MalType == kommentar.MalType && k.MalId == kommentar.MalId)
            .OrderBy(k => k.Rekkefolge).ThenBy(k => k.OpprettetTidspunkt)
            .ToListAsync(ct);

        var indeks = sosken.FindIndex(k => k.Id == id);
        var naboIndeks = retning == "opp" ? indeks - 1 : indeks + 1;
        if (naboIndeks < 0 || naboIndeks >= sosken.Count)
        {
            throw new ArgumentException($"Kommentaren er allerede {(retning == "opp" ? "først" : "sist")} — kan ikke flyttes {retning}.");
        }

        var nabo = sosken[naboIndeks];
        (kommentar.Rekkefolge, nabo.Rekkefolge) = (nabo.Rekkefolge, kommentar.Rekkefolge);
        kommentar.SistEndretAv = endretAv;
        kommentar.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("vilkarstre_kommentar", kommentar.Id, kommentar.VirksomhetId, "flyttet", endretAv));
        await db.SaveChangesAsync(ct);
        return kommentar;
    }

    public async Task<bool> SlettAsync(Guid id, CancellationToken ct = default)
    {
        var kommentar = await db.VilkarstreKommentarer.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kommentar is null) return false;
        db.VilkarstreKommentarer.Remove(kommentar);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ValiderAsync(string malType, Guid malId, string dokumenttype, CancellationToken ct)
    {
        if (!GyldigeDokumenttyper.Contains(dokumenttype))
        {
            throw new ArgumentException($"Ukjent dokumenttype '{dokumenttype}'. Gyldige verdier: {string.Join(", ", GyldigeDokumenttyper)}.");
        }

        var finnesMatchende = malType switch
        {
            "vilkar" => await db.Vilkar.AnyAsync(v => v.Id == malId && v.Entitetsstatus == "gjeldende", ct),
            "regelnode" => await db.Regelnoder.AnyAsync(r => r.Id == malId && r.Entitetsstatus == "gjeldende", ct),
            "unntak" => await db.Unntak.AnyAsync(u => u.Id == malId && u.Entitetsstatus == "gjeldende", ct),
            _ => throw new ArgumentException($"Ukjent maltype '{malType}'. Gyldige verdier: vilkar, regelnode, unntak."),
        };
        if (!finnesMatchende)
        {
            throw new ArgumentException($"Fant ingen '{malType}' med id '{malId}'.");
        }
    }
}
