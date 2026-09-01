using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Arbeidskø for godkjenning av virksomhetsforekomster funnet ved tekstsøk (docs/20 §2.6). Denne
/// klassen er selve KØEN (opprett/liste/godkjenn/avvis/hardslett) — selve SVEIPEFUNKSJONEN (tekstsøket
/// gjennom alle rettskilder etter <see cref="BegrepEntitet"/>-navneformer, docs/20 §5/kravspek §4.2
/// pkt. 1) ligger i <see cref="VirksomhetKandidatSveipTjeneste"/>, en egen klasse — sveipet trenger kun
/// <see cref="OpprettEllerFinnAsync"/> herfra for å legge treff i køen.
/// <para>
/// <see cref="GodkjennAsync"/> oppretter nå (kandidatsøk-og-godkjenning-runden) den faktiske
/// <see cref="TekstTaggEntitet"/>-forekomsten (kravspek §4.2 pkt. 5) — se metodekommentaren for
/// designvalget om HVORFOR quoteSelector-en beregnes på nytt fra nodens DÅVÆRENDE tekst i stedet for å
/// lagres på kandidaten.
/// </para>
/// </summary>
public sealed class VirksomhetKandidatTjeneste(RegelIdeDbContext db, TekstTaggTjeneste tekstTaggTjeneste)
{
    /// <summary>Idempotent — samme (virksomhet, rettskilde, node, START-posisjon) gir samme rad tilbake
    /// i stedet for et duplikat, uansett status (docs/20 §2.6: en Avvist-rad skal IKKE dukke opp igjen
    /// ved neste sveip; se den unike indeksen i RegelIdeDbContext). StartOffset er del av nøkkelen —
    /// se <see cref="VirksomhetKandidatEntitet.StartOffset"/>s klassekommentar for hvorfor.</summary>
    public async Task<VirksomhetKandidatEntitet> OpprettEllerFinnAsync(
        Guid virksomhetId, Guid rettskildeId, string nodeEid, int startOffset, int endOffset,
        string opprettetAv, CancellationToken ct = default)
    {
        var eksisterende = await db.VirksomhetKandidater.FirstOrDefaultAsync(
            k => k.VirksomhetId == virksomhetId && k.RettskildeId == rettskildeId && k.NodeEid == nodeEid
                 && k.StartOffset == startOffset, ct);
        if (eksisterende is not null) return eksisterende;

        if (!await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{virksomhetId}'. Ingen gjettet fallback.");
        }
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

        var kandidat = new VirksomhetKandidatEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            RettskildeId = rettskildeId,
            NodeEid = nodeEid,
            StartOffset = startOffset,
            EndOffset = endOffset,
            Status = "Venter",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.VirksomhetKandidater.Add(kandidat);
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>Kun `'Venter'`-rader (docs/20 §2.6) — det andre statusene er nettopp for å IKKE vises
    /// igjen. Valgfritt filtrert til én virksomhet og/eller én rettskilde (kravspek §4.2 pkt. 3/4). Tynn
    /// innpakning av <see cref="ListerAsync"/> — bevart som eget navn siden dette er den ARBEIDSKØ-
    /// semantikken docs/20 §2.6 faktisk beskriver ("kun Venter vises i køen"), brukt av bl.a.
    /// VirksomhetDetalj.tsx sin godkjenn/avvis-widget.</summary>
    public Task<List<VirksomhetKandidatEntitet>> ListerVentendeAsync(
        Guid? virksomhetId = null, Guid? rettskildeId = null, CancellationToken ct = default) =>
        ListerAsync(virksomhetId, rettskildeId, "Venter", ct);

    /// <summary>
    /// Full liste, til kandidatliste-UI-et (kravspek §4.2 pkt. 3): sorterbar/filtrerbar på virksomhet,
    /// rettskilde og status. <paramref name="status"/> = <c>null</c> betyr ALLE statuser (i motsetning
    /// til <see cref="ListerVentendeAsync"/>, som alltid er Venter-only) — eksplisitt valgt av kalleren,
    /// ikke en stille "ingen filter"-standard forskjellig fra den andre metoden.
    /// </summary>
    public Task<List<VirksomhetKandidatEntitet>> ListerAsync(
        Guid? virksomhetId = null, Guid? rettskildeId = null, string? status = null, CancellationToken ct = default)
    {
        var spørring = db.VirksomhetKandidater.AsQueryable();
        if (status is not null) spørring = spørring.Where(k => k.Status == status);
        if (virksomhetId is not null) spørring = spørring.Where(k => k.VirksomhetId == virksomhetId);
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        return spørring.OrderBy(k => k.RettskildeId).ThenBy(k => k.NodeEid).ThenBy(k => k.StartOffset).ToListAsync(ct);
    }

    /// <summary>
    /// Godkjenner kandidaten OG oppretter den faktiske forekomst-taggingen (kravspek §4.2 pkt. 5).
    /// <para>
    /// <b>Designvalg — matching re-kjøres mot FRISK tekst, ikke lagret quoteSelector:</b> kandidaten
    /// bærer kun <see cref="VirksomhetKandidatEntitet.StartOffset"/>/<see cref="VirksomhetKandidatEntitet.EndOffset"/>
    /// fra sveip-tidspunktet. Her leses noden på nytt, tekstutdraget i intervallet slås opp mot
    /// virksomhetens navneform-<see cref="BegrepEntitet"/>-rader (samme "matcher ikke → kast"-vern som
    /// <see cref="TekstTaggTjeneste.OpprettAsync"/> selv har for stale klientmarkeringer) — hvis noden er
    /// reimportert/endret siden sveipet og intervallet ikke lenger er en gyldig navneform, kastes
    /// <see cref="ArgumentException"/> i stedet for å lagre en tagg som ikke faktisk stemmer.
    /// </para>
    /// <para>
    /// <b>Designvalg — hvem EIER taggen:</b> <see cref="TekstTaggEntitet.VirksomhetId"/> settes til
    /// <see cref="VirksomhetKandidatEntitet.VirksomhetId"/> (virksomheten TEKSTEN OMTALER, f.eks.
    /// Advokattilsynet), IKKE til godkjennerens egen virksomhet. VirksomhetKandidat-køen er delt/global
    /// (docs/20 §0 pkt. 3: "åpen skriving, sporet attribusjon", ingen per-virksomhet skrivesperre) —
    /// resultatet skal derfor også være et delt, globalt faktum om rettskilden, ikke noe scopet til
    /// hvilken bruker som klikket «godkjenn». Dette gjør også <c>TekstTaggTjeneste.ListerForAsync(rettskildeId,
    /// virksomhetId)</c> til nøyaktig riktig oppslag for docs/20 §3s "fra virksomhet til rettskilde"-visning
    /// (alle forekomster AV en virksomhet, ikke tagger LAGET AV en virksomhet). <paramref name="behandletAv"/>
    /// (personnavnet) bæres videre som taggens <c>OpprettetAv</c> for sporbarhet, samme mønster som
    /// docs/20 §0 pkt. 3 ellers bruker.
    /// </para>
    /// </summary>
    public async Task<VirksomhetKandidatEntitet?> GodkjennAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.VirksomhetKandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        if (kandidat.Status != "Venter")
        {
            throw new ArgumentException(
                $"Kandidaten har status '{kandidat.Status}' — kan kun godkjenne kandidater med status 'Venter'.");
        }

        var node = await db.RettskildeNoder.FirstOrDefaultAsync(
            n => n.RettskildeId == kandidat.RettskildeId && n.Eid == kandidat.NodeEid, ct);
        if (node is null)
        {
            throw new ArgumentException(
                $"Fant ikke noden '{kandidat.NodeEid}' i rettskilde '{kandidat.RettskildeId}' lenger. Ingen gjettet fallback.");
        }

        var tekst = node.Tekst ?? "";
        if (kandidat.StartOffset < 0 || kandidat.EndOffset > tekst.Length || kandidat.EndOffset <= kandidat.StartOffset)
        {
            throw new ArgumentException(
                $"Kandidatens tegn-intervall [{kandidat.StartOffset}, {kandidat.EndOffset}) er ikke lenger gyldig for " +
                $"noden (tekstlengde {tekst.Length}) — noden er trolig endret siden sveipet. Kjør sveipet på nytt.");
        }

        var faktiskUtdrag = tekst[kandidat.StartOffset..kandidat.EndOffset];
        var navneform = await db.Begreper.FirstOrDefaultAsync(b =>
            b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == kandidat.VirksomhetId
            && b.Entitetsstatus == "gjeldende" && b.Term == faktiskUtdrag, ct);
        if (navneform is null)
        {
            throw new ArgumentException(
                $"Fant ingen navneform-begrep for virksomheten som matcher teksten '{faktiskUtdrag}' i det lagrede " +
                "intervallet — noden er trolig endret siden sveipet, eller navneformen er fjernet. Ingen gjettet " +
                "fallback. Kjør sveipet på nytt.");
        }

        // Kontekstvindu for quoteSelector — samme størrelse (30 tegn) som klienten bruker ved manuell
        // tagging, se RettskildeDetalj.tsx (nodeTekst.slice(start - 30, start) / slice(end, end + 30)).
        const int kontekstLengde = 30;
        var quotePrefix = tekst[Math.Max(0, kandidat.StartOffset - kontekstLengde)..kandidat.StartOffset];
        var quoteSuffix = tekst[kandidat.EndOffset..Math.Min(tekst.Length, kandidat.EndOffset + kontekstLengde)];

        var tagg = await tekstTaggTjeneste.OpprettAsync(
            kandidat.RettskildeId, kandidat.VirksomhetId, behandletAv, kandidat.NodeEid,
            kandidat.StartOffset, kandidat.EndOffset, quotePrefix, faktiskUtdrag, quoteSuffix, "begrep", ct);
        if (tagg is null)
        {
            throw new ArgumentException(
                $"Fant ikke noden '{kandidat.NodeEid}' ved oppretting av tagg (racy sletting?). Ingen gjettet fallback.");
        }
        // [LÅST — kandidatsøk-og-godkjenning-runden] RefId skal ALLTID være navneform-Begrep-raden, ALDRI
        // en egen "virksomhet"-tag-kind som peker direkte på Virksomhet — det ble bygget og reversert i en
        // tidligere runde fordi det bypasser navneform-laget (som finnes for å håndtere synonymer, f.eks.
        // "Fylkesmann"/"Statsforvalter" mot samme virksomhet). Gjør ikke den feilen på nytt her.
        await tekstTaggTjeneste.KobleTilEntitetAsync(tagg.Id, navneform.Id, behandletAv, ct);

        kandidat.Status = "Godkjent";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    public async Task<VirksomhetKandidatEntitet?> AvvisAsync(Guid id, string behandletAv, CancellationToken ct = default)
    {
        var kandidat = await db.VirksomhetKandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return null;
        if (kandidat.Status != "Venter")
        {
            throw new ArgumentException(
                $"Kandidaten har status '{kandidat.Status}' — kan kun avvise kandidater med status 'Venter'.");
        }
        kandidat.Status = "Avvist";
        kandidat.BehandletAv = behandletAv;
        kandidat.BehandletTidspunkt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return kandidat;
    }

    /// <summary>Hardsletting — kun for `'Avvist'`-rader (docs/20 §2.6: "kan hardslettes manuelt", et
    /// eksplisitt unntak fra husstilens vanlige mykslette/Entitetsstatus-mønster, se klassekommentaren
    /// på <see cref="VirksomhetKandidatEntitet"/>). `'Venter'`/`'Godkjent'` skal IKKE kunne hardslettes
    /// herfra — en Venter-rad skal behandles (godkjennes/avvises), ikke bare forsvinne.</summary>
    public async Task<bool> HardslettAvvistAsync(Guid id, CancellationToken ct = default)
    {
        var kandidat = await db.VirksomhetKandidater.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kandidat is null) return false;
        if (kandidat.Status != "Avvist")
        {
            throw new ArgumentException("Kun avviste kandidater kan hardslettes. Godkjenn eller avvis først.");
        }
        db.VirksomhetKandidater.Remove(kandidat);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Massehardsletting — bulk-varianten av <see cref="HardslettAvvistAsync"/>, SAMME Avvist-only-
    /// restriksjon, ikke en parallell/løsere mekanisme. Filtrerbar på virksomhet/rettskilde, samme
    /// filterparametre som <see cref="ListerAsync"/>. <paramref name="status"/> er bevisst IKKE et fritt
    /// filter (til forskjell fra <see cref="NavnekandidatOppdagelseTjeneste.SlettAlleAsync"/>, som
    /// aksepterer ethvert statusfilter siden den entiteten ikke har noen sidevirkning å beskytte) —
    /// spørringen tvinger uansett <c>Status == "Avvist"</c>, men en eksplisitt forespørsel om noe ANNET
    /// enn 'Avvist' (eller utelatt) kastes som en tydelig feil i stedet for å stille returnere 0 slettet
    /// uten forklaring.
    /// <para>
    /// <b>Hvorfor 'Godkjent' er UTELUKKET fra hardsletting (til forskjell fra navnekandidater, der ALLE
    /// statuser kan hardslettes, se <see cref="NavnekandidatOppdagelseTjeneste.SlettAsync"/>):</b> en
    /// 'Godkjent'-rad her har en REELL sidevirkning — <see cref="GodkjennAsync"/> oppretter en ekte
    /// <see cref="TekstTaggEntitet"/> koblet til en navneform (<c>RefId</c> satt via
    /// <see cref="TekstTaggTjeneste.KobleTilEntitetAsync"/>). <see cref="TekstTaggTjeneste.SlettAsync"/>
    /// nekter EKSPLISITT å fjerne en tagg med <c>RefId</c> satt (returnerer <c>HarPublisertReferanse</c> —
    /// "kun tagger uten publiserte referanser kan fjernes"). En godkjent kandidats tagg kan derfor ALDRI
    /// fjernes gjennom noen eksisterende mekanisme i systemet i dag. Å likevel tillate sletting av
    /// KANDIDATRADEN ville etterlate en permanent, ikke-fjernbar tagg uten noen kandidatrad igjen som
    /// forklarer/sporer hvorfor den finnes — og et nytt sveip ville (posisjonen er ikke lenger "opptatt")
    /// kunne legge en FRISK 'Venter'-kandidat på nøyaktig samme tegnintervall, som ved en senere
    /// godkjenning ville opprette en ANDRE tagg for akkurat samme forekomst (<see cref="TekstTaggTjeneste.OpprettAsync"/>
    /// har ingen dedup-sjekk mot eksisterende tagger på samme intervall). Fremfor å bygge en egen
    /// "fjern kandidat OG tagg samtidig"-sti (som ville måtte omgå HarPublisertReferanse-vernet over —
    /// et vern som finnes av gode grunner andre steder i systemet), er den enkleste og tryggeste linjen å
    /// la 'Godkjent' rett og slett være utenfor hardsletting her, samme linje som docs/20 §2.6 allerede
    /// trekker for enkeltrad-varianten.
    /// </para>
    /// <para>
    /// <b>Hvorfor 'Venter' ER utelukket (samme som enkeltrad-varianten):</b> en rad som ikke er
    /// ferdigbehandlet skal behandles (godkjennes/avvises), ikke bare forsvinne — se
    /// <see cref="HardslettAvvistAsync"/> sin klassekommentar.
    /// </para>
    /// </summary>
    public async Task<int> HardslettAlleAvvisteAsync(
        Guid? virksomhetId = null, Guid? rettskildeId = null, string? status = null, CancellationToken ct = default)
    {
        if (status is not null && status != "Avvist")
        {
            throw new ArgumentException(
                $"Massehardsletting kan kun rettes mot 'Avvist'-rader (status='{status}' er ikke tillatt) — " +
                "'Venter' skal behandles, ikke forsvinne, og 'Godkjent' har en tagg som ikke kan fjernes i " +
                "etterkant. Se HardslettAlleAvvisteAsync sin klassekommentar for hele resonnementet.");
        }

        var spørring = db.VirksomhetKandidater.Where(k => k.Status == "Avvist");
        if (virksomhetId is not null) spørring = spørring.Where(k => k.VirksomhetId == virksomhetId);
        if (rettskildeId is not null) spørring = spørring.Where(k => k.RettskildeId == rettskildeId);
        var rader = await spørring.ToListAsync(ct);
        db.VirksomhetKandidater.RemoveRange(rader);
        await db.SaveChangesAsync(ct);
        return rader.Count;
    }
}
