using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Arbeidskø for godkjenning av begreps-FOREKOMSTER funnet ved deterministisk mønstersveip (docs/24
/// §1.2/§3 punkt 4) — selve KØEN (opprett/finn/liste/godkjenn/avvis/hardslett), nøyaktig samme
/// rolle-fordeling som <see cref="VirksomhetKandidatTjeneste"/> spiller for
/// <see cref="VirksomhetKandidatEntitet"/>. Selve SVEIPEFUNKSJONEN (M1/M11-mønstergjenkjenningen mot
/// <see cref="RettskildeNodeEntitet"/>-treet) ligger i <see cref="BegrepsoppdagelseSveipTjeneste"/>, en
/// egen klasse — sveipet trenger kun <see cref="OpprettEllerFinnAsync"/> herfra for å legge treff i
/// køen.
/// <para>
/// <b>Hvem eier den nye <see cref="BegrepEntitet"/>-raden ved godkjenning?</b>
/// <see cref="BegrepsforekomstEntitet"/> har (bevisst, docs/24 §2.1) INGEN <c>VirksomhetId</c> — en
/// forekomst er et objektivt, delt faktum om rettskildeteksten, ikke noens arbeidsprodukt ennå. Men
/// <see cref="BegrepEntitet"/> (for ordinære fakta-/handlingsbegrep, <c>Begrepskategori=null</c>) krever
/// en eiende virksomhet (§0.1), og <see cref="TekstTaggEntitet.VirksomhetId"/> er ikke-nullbar. Løsning,
/// konsistent med hvordan «Identifiser begrep» (<see cref="BegrepsforslagTjeneste.KjorForslagAsync"/>)
/// allerede løser NØYAKTIG samme spørsmål for KI-forslag mot delte rettskilder: den GODKJENNENDE
/// brukeren oppgir eksplisitt hvilken virksomhets register forekomsten skal landes i
/// (<paramref name="virksomhetId"/> på <see cref="GodkjennAsync"/>) — IKKE en automatisk utledning (som
/// <c>AnsvarligDepartement</c>-oppslaget <see cref="NavnekandidatOppdagelseTjeneste"/> bruker), siden
/// den mekanismen løser et annet spørsmål (hvem EIER en delt navneform) enn dette (hvilket register skal
/// et NYTT, ordinært fakta-/handlingsbegrep legges i). Dette er ikke eksplisitt besluttet av docs/24 selv
/// — et bevisst, dokumentert designvalg tatt i denne runden, ikke en stille antakelse.
/// </para>
/// </summary>
public sealed class BegrepsforekomstTjeneste(
    RegelIdeDbContext db, TekstTaggTjeneste tekstTaggTjeneste, BegrepsregisterTjeneste begrepsregister)
{
    /// <summary>Idempotent — samme (rettskilde, node, START-posisjon) gir samme rad tilbake i stedet for
    /// et duplikat, uansett status (docs/24 §4 siste punkt) — nøyaktig samme mønster som
    /// <see cref="VirksomhetKandidatTjeneste.OpprettEllerFinnAsync"/>/<see cref="NavnekandidatOppdagelseTjeneste.OpprettEllerFinnAsync"/>.</summary>
    public async Task<BegrepsforekomstEntitet> OpprettEllerFinnAsync(
        Guid rettskildeId, string nodeEid, string begrep, string begrepOriginal, string? definisjon,
        string kildetype, string monsterId, string konfidens, string scope, string? scopeRefEid,
        int startOffset, int endOffset, string opprettetAv, CancellationToken ct = default)
    {
        var eksisterende = await db.Begrepsforekomster.FirstOrDefaultAsync(
            k => k.RettskildeId == rettskildeId && k.NodeEid == nodeEid && k.StartOffset == startOffset, ct);
        if (eksisterende is not null) return eksisterende;

        var node = await db.RettskildeNoder.FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == nodeEid, ct);
        if (node is null)
        {
            throw new ArgumentException($"Fant ingen rettskilde-node med eId '{nodeEid}' i rettskilde '{rettskildeId}'. Ingen gjettet fallback.");
        }
        if (endOffset <= startOffset || startOffset < 0 || endOffset > (node.Tekst?.Length ?? 0))
        {
            throw new ArgumentException(
                $"Ugyldig tegnintervall [{startOffset}, {endOffset}) for node '{nodeEid}' (tekstlengde {node.Tekst?.Length ?? 0}).");
        }

        var forekomst = new BegrepsforekomstEntitet
        {
            Id = Guid.NewGuid(),
            RettskildeId = rettskildeId,
            NodeEid = nodeEid,
            StartOffset = startOffset,
            EndOffset = endOffset,
            Begrep = begrep,
            BegrepOriginal = begrepOriginal,
            Definisjon = definisjon,
            Kildetype = kildetype,
            MonsterId = monsterId,
            Konfidens = konfidens,
            Scope = scope,
            ScopeRefEid = scopeRefEid,
            Status = "Venter",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begrepsforekomster.Add(forekomst);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Samme racy-sveip-vern som NavnekandidatOppdagelseTjeneste.OpprettEllerFinnAsync — to
            // overlappende sveip kan begge passere FirstOrDefaultAsync-sjekken over før noen av dem
            // committer.
            db.Entry(forekomst).State = EntityState.Detached;
            var vantLopet = await db.Begrepsforekomster.FirstOrDefaultAsync(
                k => k.RettskildeId == rettskildeId && k.NodeEid == nodeEid && k.StartOffset == startOffset, ct);
            if (vantLopet is not null) return vantLopet;
            throw;
        }
        return forekomst;
    }

    /// <summary>Kun <c>'Venter'</c>-rader — se <see cref="ListerAsync"/> for full liste med status=<c>null</c>.</summary>
    public Task<List<BegrepsforekomstEntitet>> ListerVentendeAsync(
        Guid? rettskildeId = null, string? monsterId = null, CancellationToken ct = default) =>
        ListerAsync(rettskildeId, monsterId, "Venter", ct);

    /// <summary>Full liste, valgfritt filtrert på rettskilde/mønster/status. <paramref name="status"/> =
    /// <c>null</c> betyr ALLE statuser (samme eksplisitte "ingen stille standard" -mønster som
    /// <see cref="VirksomhetKandidatTjeneste.ListerAsync"/>).</summary>
    public Task<List<BegrepsforekomstEntitet>> ListerAsync(
        Guid? rettskildeId = null, string? monsterId = null, string? status = null, CancellationToken ct = default)
    {
        var spørring = db.Begrepsforekomster.AsQueryable();
        if (status is not null) spørring = spørring.Where(k => k.Status == status);
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        if (monsterId is not null) spørring = spørring.Where(k => k.MonsterId == monsterId);
        return spørring.OrderBy(k => k.RettskildeId).ThenBy(k => k.NodeEid).ThenBy(k => k.StartOffset).ToListAsync(ct);
    }

    /// <summary>
    /// Godkjenner forekomsten: revaliderer mot nodens DÅVÆRENDE tekst (samme "matcher ikke → kast"-vern
    /// som <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/> — kaster <see cref="ArgumentException"/>
    /// hvis rettskilden er endret siden sveipet i stedet for å stille lagre en tagg/et begrep som ikke
    /// faktisk stemmer med gjeldende tekst), oppretter en ekte <see cref="TekstTaggEntitet"/>
    /// (<c>Kind="begrep"</c>) OG en ny <see cref="BegrepEntitet"/>-rad i <paramref name="virksomhetId"/>
    /// sitt register via <see cref="BegrepsregisterTjeneste.OpprettFraForekomstAsync"/> — se klassekommentaren
    /// for hvorfor <paramref name="virksomhetId"/> er et eksplisitt parameter, ikke utledet.
    /// </summary>
    public async Task<BegrepsforekomstEntitet?> GodkjennAsync(
        Guid id, Guid virksomhetId, string behandletAv, CancellationToken ct = default)
    {
        var forekomst = await db.Begrepsforekomster.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (forekomst is null) return null;
        if (forekomst.Status != "Venter")
        {
            throw new ArgumentException(
                $"Forekomsten har status '{forekomst.Status}' — kan kun godkjenne forekomster med status 'Venter'.");
        }
        if (!await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{virksomhetId}'. Ingen gjettet fallback.");
        }
        if (string.IsNullOrWhiteSpace(forekomst.Definisjon))
        {
            throw new ArgumentException(
                "Forekomsten har ingen definisjonstekst — kan ikke opprette et begrep uten definisjon. Ingen gjettet fallback.");
        }

        var node = await db.RettskildeNoder.FirstOrDefaultAsync(
            n => n.RettskildeId == forekomst.RettskildeId && n.Eid == forekomst.NodeEid, ct);
        if (node is null)
        {
            throw new ArgumentException(
                $"Fant ikke noden '{forekomst.NodeEid}' i rettskilde '{forekomst.RettskildeId}' lenger. Ingen gjettet fallback.");
        }

        var tekst = node.Tekst ?? "";
        if (forekomst.StartOffset < 0 || forekomst.EndOffset > tekst.Length || forekomst.EndOffset <= forekomst.StartOffset)
        {
            throw new ArgumentException(
                $"Forekomstens tegn-intervall [{forekomst.StartOffset}, {forekomst.EndOffset}) er ikke lenger gyldig " +
                $"for noden (tekstlengde {tekst.Length}) — noden er trolig endret siden sveipet. Kjør sveipet på nytt.");
        }

        var faktiskUtdrag = tekst[forekomst.StartOffset..forekomst.EndOffset];
        if (faktiskUtdrag != forekomst.BegrepOriginal)
        {
            throw new ArgumentException(
                $"Teksten i det lagrede intervallet ('{faktiskUtdrag}') samsvarer ikke lenger med forekomstens " +
                $"opprinnelige ordlyd ('{forekomst.BegrepOriginal}') — noden er trolig endret siden sveipet. " +
                "Ingen gjettet fallback. Kjør sveipet på nytt.");
        }

        // Samme 30-tegns kontekstvindu som VirksomhetKandidatTjeneste.GodkjennAsync/klientens manuelle tagging.
        const int kontekstLengde = 30;
        var quotePrefix = tekst[Math.Max(0, forekomst.StartOffset - kontekstLengde)..forekomst.StartOffset];
        var quoteSuffix = tekst[forekomst.EndOffset..Math.Min(tekst.Length, forekomst.EndOffset + kontekstLengde)];

        var begrep = await begrepsregister.OpprettFraForekomstAsync(
            virksomhetId, forekomst.Begrep, forekomst.Definisjon, forekomst.NodeEid, behandletAv, ct);

        var tagg = await tekstTaggTjeneste.OpprettAsync(
            forekomst.RettskildeId, virksomhetId, behandletAv, forekomst.NodeEid,
            forekomst.StartOffset, forekomst.EndOffset, quotePrefix, faktiskUtdrag, quoteSuffix, "begrep", ct);
        if (tagg is null)
        {
            throw new ArgumentException(
                $"Fant ikke noden '{forekomst.NodeEid}' ved oppretting av tagg (racy sletting?). Ingen gjettet fallback.");
        }
        await tekstTaggTjeneste.KobleTilEntitetAsync(tagg.Id, begrep.Id, behandletAv, ct);

        forekomst.BegrepId = begrep.Id;
        forekomst.Status = "Godkjent";
        forekomst.BehandletAv = behandletAv;
        forekomst.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return forekomst;
    }

    public async Task<BegrepsforekomstEntitet?> AvvisAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var forekomst = await db.Begrepsforekomster.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (forekomst is null) return null;
        if (forekomst.Status != "Venter")
        {
            throw new ArgumentException(
                $"Forekomsten har status '{forekomst.Status}' — kan kun avvise forekomster med status 'Venter'.");
        }
        forekomst.Status = "Avvist";
        forekomst.BehandletAv = behandletAv;
        forekomst.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return forekomst;
    }

    /// <summary>Hardsletting — kun for <c>'Avvist'</c>-rader, samme begrunnelse som
    /// <see cref="VirksomhetKandidatTjeneste.HardslettAvvistAsync"/>: en <c>'Godkjent'</c>-rad har en
    /// ekte tagg (og et ekte begrep) som ikke kan fjernes i etterkant (<see cref="TekstTaggTjeneste.SlettAsync"/>
    /// nekter å fjerne en tagg med <c>RefId</c> satt), og en <c>'Venter'</c>-rad skal behandles, ikke
    /// bare forsvinne.</summary>
    public async Task<bool> HardslettAvvistAsync(Guid id, CancellationToken ct = default)
    {
        var forekomst = await db.Begrepsforekomster.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (forekomst is null) return false;
        if (forekomst.Status != "Avvist")
        {
            throw new ArgumentException("Kun avviste forekomster kan hardslettes. Godkjenn eller avvis først.");
        }
        db.Begrepsforekomster.Remove(forekomst);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Massehardsletting — bulk-varianten av <see cref="HardslettAvvistAsync"/>, samme
    /// Avvist-only-restriksjon (se den metodens kommentar).</summary>
    public async Task<int> HardslettAlleAvvisteAsync(
        Guid? rettskildeId = null, string? status = null, CancellationToken ct = default)
    {
        if (status is not null && status != "Avvist")
        {
            throw new ArgumentException(
                $"Massehardsletting kan kun rettes mot 'Avvist'-rader (status='{status}' er ikke tillatt).");
        }

        var spørring = db.Begrepsforekomster.Where(k => k.Status == "Avvist");
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        var rader = await spørring.ToListAsync(ct);
        db.Begrepsforekomster.RemoveRange(rader);
        await db.SaveChangesAsync(ct);
        return rader.Count;
    }
}
