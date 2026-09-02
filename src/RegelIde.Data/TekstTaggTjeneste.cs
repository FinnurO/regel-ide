using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Tekstmerking → tagging (§1.2 i domenemodellen, AK-3.3.1–3.3.4). En tagg er alltid virksomhetens
/// eget arbeidsprodukt (§0.1) — to virksomheter kan tagge samme delte rettskilde-node helt ulikt, så
/// alt her er scopet til den kallende brukerens virksomhet, aldri på tvers.
/// </summary>
public sealed class TekstTaggTjeneste(RegelIdeDbContext db, VirksomhetOppslagTjeneste virksomhetOppslag)
{
    /// <summary>
    /// [Utvidet, tekst-tagg-departement-eierskap, 2026-08-31] Egne tagger for <paramref name="virksomhetId"/>
    /// (uendret oppførsel — §0.1, aldri delt på tvers) PLUSS, hvis rettskilden faktisk har et kjent OG
    /// oppløsbart <see cref="RettskildeEntitet.AnsvarligDepartement"/>, departementets EGNE tagger (se
    /// <see cref="NavnekandidatOppdagelseTjeneste.GodkjennAsync"/>, som oppretter disse ved godkjenning
    /// av et gruppe-/virksomhet-navnetreff — Johanns eksplisitte designvalg: "det eies av ansvarlig
    /// departement [...] men det skal jo være mulig å se taggene allikevel"). Uten dette tillegget var
    /// GET-endepunktet (som alltid spør per KALLENDE brukers egen virksomhet) reelt begrenset til å vise
    /// departementets egne tagger KUN til departementets egne innloggede brukere — selv om selve
    /// rettskilden og navnetreffet er delt/nasjonalt innhold alle skal kunne se. Løses FERSK ved hvert
    /// kall (samme "aldri lagret FK, alltid navnematch ved lesing"-designvalg som
    /// <see cref="RettskildeEntitet.AnsvarligDepartement"/> selv), ikke en per-virksomhet skrivesperre —
    /// en departement-virksomhet som selv kaller dette endepunktet for SIN EGEN VirksomhetId får
    /// nøyaktig samme (allerede dekkede) rader fra begge leddene i OR-en, ingen dobling.
    /// </summary>
    public async Task<List<TekstTaggEntitet>> ListerForAsync(Guid rettskildeId, Guid virksomhetId, CancellationToken ct = default)
    {
        var departementVirksomhetId = await FinnDepartementVirksomhetIdAsync(rettskildeId, ct);
        return await db.TekstTagger
            .Where(t => t.RettskildeId == rettskildeId && t.Entitetsstatus == "gjeldende"
                        && (t.VirksomhetId == virksomhetId
                            || (departementVirksomhetId != null && t.VirksomhetId == departementVirksomhetId)))
            .OrderBy(t => t.NodeEid).ThenBy(t => t.StartOffset)
            .ToListAsync(ct);
    }

    /// <summary>Gjenbruker <see cref="VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnAsync"/> (samme
    /// oppslagsmekanisme som <c>RettskildeRepository</c> bruker for "Ansvarlig for"-visningen) — ALDRI
    /// en egen duplisert spørring. Returnerer null både når rettskilden ikke har noe kjent
    /// <see cref="RettskildeEntitet.AnsvarligDepartement"/>, og når strengen ikke matcher noen ekte
    /// <see cref="Virksomhet"/> — «ingen gjettet fallback» i begge tilfellene.</summary>
    private async Task<Guid?> FinnDepartementVirksomhetIdAsync(Guid rettskildeId, CancellationToken ct)
    {
        var ansvarligDepartement = await db.Rettskilder
            .Where(r => r.Id == rettskildeId)
            .Select(r => r.AnsvarligDepartement)
            .FirstOrDefaultAsync(ct);
        return ansvarligDepartement is null ? null : await virksomhetOppslag.FinnVirksomhetIdForNavnAsync(ansvarligDepartement);
    }

    /// <summary>
    /// Oppretter en ny tagg. Returnerer null hvis <paramref name="nodeEid"/> ikke finnes på rettskilden
    /// (kalleren mapper det til 404). Kaster <see cref="ArgumentException"/> ved ugyldig/inaktiv kind
    /// (2026-07-25: kind-listen er nå konfigurasjonsstyrt via <see cref="TaggKindKonfigurasjonEntitet"/>,
    /// ikke hardkodet), offset utenfor teksten, hvis <paramref name="quoteExact"/> ikke matcher nodens
    /// faktiske tekst i det oppgitte intervallet (fanger opp en stale klientmarkering fremfor å lagre en
    /// tagg som ikke faktisk peker på det den sier den gjør — §3.3 "ingen gjettet fallback"), eller hvis
    /// noden er opphevet (2026-07-24: ikke et poeng å tagge tekst som ikke lenger gjelder).
    /// </summary>
    public async Task<TekstTaggEntitet?> OpprettAsync(
        Guid rettskildeId, Guid virksomhetId, string opprettetAv, string nodeEid,
        int startOffset, int endOffset, string quotePrefix, string quoteExact, string quoteSuffix, string kind,
        CancellationToken ct = default)
    {
        if (!await db.TaggKindKonfigurasjoner.AnyAsync(k => k.Kode == kind && k.Aktiv, ct))
        {
            throw new ArgumentException($"Ukjent eller inaktiv tag-type '{kind}'. Ingen gjettet fallback.");
        }

        var node = await db.RettskildeNoder.FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == nodeEid, ct);
        if (node is null) return null;

        if (node.Opphevet)
        {
            throw new ArgumentException($"Node '{nodeEid}' er opphevet og kan ikke tagges.");
        }

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
    /// <summary>
    /// Kobler en eksisterende tagg til en Begrep/Tjeneste/Vilkår/Regelnode-rad (byggesteg 2 + 4,
    /// docs/06-veikart.md — låser opp <see cref="TekstTaggEntitet.RefId"/>, som var <c>null</c> "inntil
    /// byggesteg 2/4 gir taggen noe å peke på"). Gyldig kun når taggens <see cref="TekstTaggEntitet.Kind"/>
    /// faktisk har en matchende rad — en 'begrep'-tagg kan f.eks. ikke kobles til en Tjeneste-id.
    /// Returnerer null hvis taggen ikke finnes (kalleren mapper det til 404).
    /// </summary>
    public async Task<TekstTaggEntitet?> KobleTilEntitetAsync(Guid taggId, Guid refId, string endretAv, CancellationToken ct = default)
    {
        var tagg = await db.TekstTagger.FirstOrDefaultAsync(t => t.Id == taggId && t.Entitetsstatus == "gjeldende", ct);
        if (tagg is null) return null;

        var finnesMatchende = tagg.Kind switch
        {
            "begrep" => await db.Begreper.AnyAsync(b => b.Id == refId && b.Entitetsstatus == "gjeldende", ct),
            "tjeneste" => await db.Tjenester.AnyAsync(t => t.Id == refId && t.Entitetsstatus == "gjeldende", ct),
            // Byggesteg 4 (2026-07-30): 'vilkar'/'regel'-tagger kan nå faktisk kobles til Vilkår/Regelnode —
            // se docs/03-domenemodell.md §1.8/§1.9. "regel" er tag-kindens navn (byggesteg 1), koblet til
            // RegelnodeEntitet (samme "regelnode ikke regel"-navnekonvensjon som resten av byggesteg 4).
            "vilkar" => await db.Vilkar.AnyAsync(v => v.Id == refId && v.Entitetsstatus == "gjeldende", ct),
            "regel" => await db.Regelnoder.AnyAsync(r => r.Id == refId && r.Entitetsstatus == "gjeldende", ct),
            _ => throw new ArgumentException($"Tagger av type '{tagg.Kind}' kan ikke kobles til en entitet ennå."),
        };
        if (!finnesMatchende)
        {
            throw new ArgumentException($"Fant ingen '{tagg.Kind}' med id '{refId}' å koble taggen til.");
        }

        tagg.RefId = refId;
        db.Proveniens.Add(ProveniensHjelper.NyRad("tekst_tagg", tagg.Id, tagg.VirksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return tagg;
    }

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
