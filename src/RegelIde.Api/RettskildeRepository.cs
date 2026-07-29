using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Databasebacket register over rettskilder (§2 i teknisk design). Scoped (per request), speiler
/// DbContext sin levetid.
///
/// Åpne data, ikke virksomhets-lukket (2026-07-24, jf. eksisterende publiseringsfilosofi i
/// 05-arkitektur-og-nfk.md §1.2 — "publisert" har alltid betydd "gjøres tilgjengelig i regel-IDEs
/// eget høstbare/lesbare endepunkt"): alle metoder viser kun rettskilder med
/// <c>Status != "Utkast"</c> — kladder (ikke menneskelig verifisert ennå, §3.1 steg 10 i teknisk
/// design) er aldri offentlig synlige, uansett virksomhet. <c>virksomhetId</c> er en valgfri
/// filtrering for å snevre inn til én virksomhets bidrag, ikke en tilgangssperre — utelates den,
/// vises alt som er synlig (delte/nasjonale kilder + alle virksomheters publiserte lokale kilder),
/// akkurat som en nasjonal åpne-data-katalog aggregerer på tvers av alle bidragsytere.
/// </summary>
public sealed class RettskildeRepository(RegelIdeDbContext db)
{
    private const string UtkastStatus = "Utkast";

    public Task<List<RettskildeEntitet>> AlleRettskilderAsync(Guid? virksomhetId = null) =>
        db.Rettskilder
            .Where(r => r.Importrolle == "primaer" && r.Entitetsstatus == "gjeldende" && r.Status != UtkastStatus)
            .Where(r => virksomhetId == null || r.VirksomhetId == virksomhetId)
            .ToListAsync();

    public Task<RettskildeEntitet?> FinnAsync(Guid id) =>
        db.Rettskilder.FirstOrDefaultAsync(r => r.Id == id && r.Status != UtkastStatus);

    // Filter på Entitetsstatus="gjeldende" lagt til 2026-07-26 (node-nivå versjonering, håndbok/rundskriv,
    // docs/08-byggesteg1-teknisk-design.md §2.1) — virkningsløst for Lov/Forskrift-noder (alltid
    // "gjeldende"), men nødvendig for håndbok-noder: en redigert kommentarseksjon har flere rader med
    // samme eid (gammel="erstattet", ny="gjeldende") som ellers ville dukket opp som duplikater i treet.
    public Task<List<RettskildeNodeEntitet>> NoderForAsync(Guid rettskildeId) =>
        db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende")
            .Include(n => n.HandbokMetadata)
            .OrderBy(n => n.Sorteringsrekkefolge)
            .ToListAsync();

    public Task<RettskildeNodeEntitet?> FinnNodeAsync(Guid rettskildeId, string eid) =>
        db.RettskildeNoder
            .Include(n => n.HandbokMetadata)
            .FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == eid && n.Entitetsstatus == "gjeldende");

    public async Task<List<RettskildeReferanseEntitet>> ReferanserForAsync(Guid rettskildeId)
    {
        var nodeIder = await db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende")
            .Select(n => n.Id).ToListAsync();
        return await db.RettskildeReferanser.Where(r => nodeIder.Contains(r.FraNodeId)).ToListAsync();
    }

    /// <summary>
    /// Oppdaterer Kortnavn/Utgiver på en allerede importert rettskilde — AK-3.3.6 "bekreft/rediger
    /// metadata" i importbekreftelsen (`Importer.tsx`). Kun disse to feltene: resten av metadataen
    /// (tittel, ELI, status osv.) er tolket direkte fra kilden og skal ikke friredigeres her.
    /// Returnerer null hvis rettskilden ikke finnes (kalleren mapper til 404).
    /// </summary>
    public async Task<RettskildeEntitet?> OppdaterMetadataAsync(Guid id, string? kortnavn, string? utgiver, string endretAv)
    {
        var rettskilde = await db.Rettskilder.FirstOrDefaultAsync(r => r.Id == id);
        if (rettskilde is null) return null;

        rettskilde.Kortnavn = kortnavn;
        rettskilde.Utgiver = utgiver;
        rettskilde.SistEndretAv = endretAv;
        rettskilde.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        rettskilde.Versjon++; // basemetadata §0: appens ansvar å øke ved faktisk endring
        await db.SaveChangesAsync();
        return rettskilde;
    }
}
