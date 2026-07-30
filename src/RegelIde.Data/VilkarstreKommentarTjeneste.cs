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
