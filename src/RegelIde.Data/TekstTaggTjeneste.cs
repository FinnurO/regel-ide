using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Tekstmerking → tagging (§1.2 i domenemodellen, AK-3.3.1–3.3.4). En tagg er alltid virksomhetens
/// eget arbeidsprodukt (§0.1) — to virksomheter kan tagge samme delte rettskilde-node helt ulikt, så
/// alt her er scopet til den kallende brukerens virksomhet, aldri på tvers.
/// </summary>
public sealed class TekstTaggTjeneste(RegelIdeDbContext db)
{
    private static readonly string[] GyldigeKinds = ["begrep", "tjeneste", "vilkar", "regel"];

    public Task<List<TekstTaggEntitet>> ListerForAsync(Guid rettskildeId, Guid virksomhetId, CancellationToken ct = default) =>
        db.TekstTagger
            .Where(t => t.RettskildeId == rettskildeId && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende")
            .OrderBy(t => t.NodeEid).ThenBy(t => t.StartOffset)
            .ToListAsync(ct);

    /// <summary>
    /// Oppretter en ny tagg. Returnerer null hvis <paramref name="nodeEid"/> ikke finnes på rettskilden
    /// (kalleren mapper det til 404). Kaster <see cref="ArgumentException"/> ved ugyldig kind, offset
    /// utenfor teksten, eller hvis <paramref name="quoteExact"/> ikke matcher nodens faktiske tekst i
    /// det oppgitte intervallet — det siste fanger opp en stale klientmarkering fremfor å lagre en
    /// tagg som ikke faktisk peker på det den sier den gjør (§3.3 "ingen gjettet fallback").
    /// </summary>
    public async Task<TekstTaggEntitet?> OpprettAsync(
        Guid rettskildeId, Guid virksomhetId, string opprettetAv, string nodeEid,
        int startOffset, int endOffset, string quotePrefix, string quoteExact, string quoteSuffix, string kind,
        CancellationToken ct = default)
    {
        if (!GyldigeKinds.Contains(kind))
        {
            throw new ArgumentException(
                $"Ukjent tag-type '{kind}'. Gyldige verdier: {string.Join(", ", GyldigeKinds)}. Ingen gjettet fallback.");
        }

        var node = await db.RettskildeNoder.FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == nodeEid, ct);
        if (node is null) return null;

        var tekst = node.Tekst ?? "";
        if (startOffset < 0 || endOffset <= startOffset || endOffset > tekst.Length)
        {
            throw new ArgumentException(
                $"Ugyldig tegnintervall [{startOffset}, {endOffset}) for node '{nodeEid}' (tekstlengde {tekst.Length}).");
        }

        var faktiskUtdrag = tekst[startOffset..endOffset];
        if (faktiskUtdrag != quoteExact)
        {
            throw new ArgumentException(
                $"quoteExact ('{quoteExact}') matcher ikke nodens faktiske tekst i intervallet ('{faktiskUtdrag}') — " +
                "markeringen er trolig utdatert, hent noden på nytt.");
        }

        var tagg = new TekstTaggEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            RettskildeId = rettskildeId,
            NodeEid = nodeEid,
            StartOffset = startOffset,
            EndOffset = endOffset,
            QuotePrefix = quotePrefix,
            QuoteExact = quoteExact,
            QuoteSuffix = quoteSuffix,
            NodeTekstHash = LovdataIdentifikatorer.BeregnTekstHash(tekst),
            Kind = kind,
            RefId = null, // nullable inntil byggesteg 2/4 gir taggen noe å peke på — se docs/06-veikart.md
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.TekstTagger.Add(tagg);
        db.Proveniens.Add(new ProveniensEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            EntitetType = "tekst_tagg",
            EntitetId = tagg.Id,
            EndretAv = opprettetAv,
            Dato = DateTimeOffset.UtcNow,
            Handling = "opprettet",
        });
        await db.SaveChangesAsync(ct);
        return tagg;
    }

    /// <summary>
    /// Fjerner (arkiverer) en tagg — AK-3.3.4. Returnerer <see cref="SlettResultat.IkkeFunnet"/> hvis
    /// taggen ikke finnes, <see cref="SlettResultat.TilhorerAnnenVirksomhet"/> hvis den tilhører en
    /// annen virksomhet (kalleren mapper til 403), og <see cref="SlettResultat.HarPublisertReferanse"/>
    /// hvis <c>ref_id</c> er satt (kalleren mapper til 409) — "kun tagger uten publiserte referanser
    /// kan fjernes". Arkiverer i stedet for å slette raden, for å bevare proveniens/sporbarhet
    /// (05-arkitektur-og-nfk.md §2), i tråd med entitetsstatus-mønsteret brukt ellers i skjemaet.
    /// </summary>
    public async Task<SlettResultat> SlettAsync(Guid rettskildeId, Guid taggId, Guid virksomhetId, string endretAv, CancellationToken ct = default)
    {
        var tagg = await db.TekstTagger.FirstOrDefaultAsync(
            t => t.Id == taggId && t.RettskildeId == rettskildeId && t.Entitetsstatus == "gjeldende", ct);
        if (tagg is null) return SlettResultat.IkkeFunnet;
        if (tagg.VirksomhetId != virksomhetId) return SlettResultat.TilhorerAnnenVirksomhet;
        if (tagg.RefId is not null) return SlettResultat.HarPublisertReferanse;

        tagg.Entitetsstatus = "arkivert";
        db.Proveniens.Add(new ProveniensEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            EntitetType = "tekst_tagg",
            EntitetId = tagg.Id,
            EndretAv = endretAv,
            Dato = DateTimeOffset.UtcNow,
            Handling = "arkivert",
        });
        await db.SaveChangesAsync(ct);
        return SlettResultat.Ok;
    }
}

public enum SlettResultat { Ok, IkkeFunnet, TilhorerAnnenVirksomhet, HarPublisertReferanse }
