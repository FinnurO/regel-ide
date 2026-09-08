using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, navneform-kjede-runden, 2026-09-08] «Hvor er denne virksomheten koblet inn?» — ETT oppslag som
/// svarer på det Johann etterlyste på virksomhetssiden: «Jeg hadde også forventet at man ser "where
/// used", altså koblinger fra Virksomheten.»
///
/// <para>
/// <b>Bulk, ikke N+1.</b> Navneform-forekomstene kunne vært hentet med ett
/// <see cref="TekstTaggTjeneste.ListerForRefIdAsync"/>-kall PER navneform (slik
/// <c>GET /api/begreper/{id}/taggede-forekomster</c> gjør for ett begrep), men en virksomhet kan ha
/// mange navneformer/synonymer, og siden trenger dem alle samtidig. Samme avveining og samme løsning
/// som <c>GET /api/rettskilder/hjemmelrelasjoner</c> (issue #193) valgte for departement→lov→forskrift-
/// hierarkiet: én flat spørring for hele behovet i stedet for ett kall per rad.
/// </para>
///
/// <para>
/// <b>Hva som IKKE er her, bevisst:</b> virksomhetsrelasjoner. De vises allerede i sin helhet i
/// «Relasjoner til andre virksomheter» på <c>VirksomhetDetalj.tsx</c> (begge retninger, med hjemmel),
/// og å returnere dem her igjen ville gitt to kilder til samme tabell og en reell risiko for at de
/// kommer i utakt. Denne tjenesten dekker nøyaktig de to koblingene siden IKKE viste: hvilke
/// rettskildetekster navneformene faktisk er tagget i, og hvilket GRUPPEBEGREP hver
/// myndighetstildeling gjelder.
/// </para>
///
/// <para>
/// <b>Én rad per FOREKOMST, og derfor må offsetene med.</b> <c>tekst_tagger_unik_tagg</c> dekker
/// blant annet StartOffset/EndOffset, så den SAMME navneformen kan lovlig være tagget FLERE ganger i
/// samme node — står navnet to steder i ett ledd, er det to legitime markeringer (og
/// <c>BegrepsforekomstTjeneste.GodkjennAsync</c> oppretter nettopp slike automatisk for de øvrige
/// forekomstene av samme term). Radene er da IKKE unikt identifisert av (navneform, rettskilde,
/// node) alene, og en klient som nøkler på bare de tre får kolliderende nøkler for to reelle rader.
/// Derfor bærer <see cref="NavneformForekomst"/> også <c>StartOffset</c>/<c>EndOffset</c>: de er det
/// som faktisk skiller to forekomster i samme ledd. Selve LENKEN peker fortsatt på noden (det er
/// granulariteten rettskildevisningen fokuserer på), men identiteten til raden er forekomsten.
/// </para>
///
/// <para>
/// <b>Ingen virksomhetsscoping.</b> Samme valg som
/// <see cref="TekstTaggTjeneste.ListerForRefIdAsync"/> (den etablerte «where used»-spørringen bak
/// <c>/api/begreper/{id}/taggede-forekomster</c>) allerede gjør: et where-used-oppslag PÅ EN
/// REFERANSE spør «hvem peker hit», og filtrerer ikke på hvilken virksomhet som eier taggen. Merk at
/// dette avviker fra <see cref="TekstTaggTjeneste.ListerForAsync"/>, som lister taggene for ÉN
/// rettskilde og der scopet til egen virksomhet (+ ansvarlig departement) er selve poenget (§0.1 —
/// en tagg er virksomhetens eget arbeidsprodukt). Skillet er bevisst og følger presedensen.
/// </para>
/// </summary>
public sealed class VirksomhetWhereUsedTjeneste(RegelIdeDbContext db)
{
    /// <summary>
    /// Én forekomst av en navneform i en rettskildetekst — altså én <c>Kind='virksomhet'</c>-tagg som
    /// peker på navneformen. Flat form (navneformens felt gjentas per forekomst) etter samme
    /// minimalitetsprinsipp som <c>RettskildeHjemmelRelasjonDto</c>: klienten grupperer selv på
    /// <paramref name="NavneformId"/>, og trenger ingen nøstet struktur for det.
    /// </summary>
    public sealed record NavneformForekomst(
        Guid NavneformId, string Term, string? Navneformgrunn,
        Guid RettskildeId, string RettskildeTittel, string NodeEid, string QuoteExact,
        int StartOffset, int EndOffset);

    /// <summary>
    /// Én myndighetstildeling, med GRUPPEBEGREPET navngitt. Selve tildelingene vises allerede på
    /// siden, men UTEN gruppens navn — tabellen lovet «Gruppebegrep … tildelt denne virksomheten» i
    /// ingressen og viste så bare paragrafspenn/vilkår/gyldighet. Det er nøyaktig den manglende
    /// opplysningen Johann ba om for Karasjok («språkutviklingskommuner»).
    /// </summary>
    public sealed record Gruppetildeling(Guid TildelingId, Guid GruppeBegrepId, string GruppeTerm);

    public sealed record Resultat(
        IReadOnlyList<NavneformForekomst> NavneformForekomster,
        IReadOnlyList<Gruppetildeling> Gruppetildelinger);

    public async Task<Resultat> HentAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        // Navneformene for denne virksomheten JOINet mot taggene som peker på dem. Etter
        // navneform-kjede-runden ER en 'virksomhet'-taggs RefId navneformens id, så dette er en ren
        // join — før runden ville den samme spørringen gitt null treff (RefId var virksomhetens id).
        // Se TekstTaggEntitet.RefId.
        var forekomster = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId
                        && b.Entitetsstatus == "gjeldende")
            .Join(
                db.TekstTagger.Where(t => t.Kind == "virksomhet" && t.Entitetsstatus == "gjeldende"),
                b => b.Id, t => t.RefId, (b, t) => new { Navneform = b, Tagg = t })
            .Join(
                db.Rettskilder, x => x.Tagg.RettskildeId, r => r.Id,
                (x, r) => new
                {
                    x.Navneform,
                    x.Tagg,
                    RettskildeTittel = r.Kortnavn ?? r.Tittel,
                })
            // Sortert på de UNDERLIGGENDE kolonnene, ikke på den projiserte recordens egenskaper: EF
            // kan ikke oversette en OrderBy over et konstruert objekt (den ville da måtte materialisere
            // hele treffsettet og sortere klientside). Rekkefølgen er den samme.
            // StartOffset sist i sorteringen: to forekomster i SAMME node skiller seg bare på
            // posisjon, og uten den er rekkefølgen mellom dem udefinert (og dermed ustabil mellom to
            // kall — en liste som bytter rekkefølge av seg selv).
            .OrderBy(x => x.Navneform.Term).ThenBy(x => x.RettskildeTittel).ThenBy(x => x.Tagg.NodeEid)
            .ThenBy(x => x.Tagg.StartOffset)
            .Select(x => new NavneformForekomst(
                x.Navneform.Id, x.Navneform.Term, x.Navneform.Navneformgrunn,
                x.Tagg.RettskildeId, x.RettskildeTittel, x.Tagg.NodeEid, x.Tagg.QuoteExact,
                x.Tagg.StartOffset, x.Tagg.EndOffset))
            .ToListAsync(ct);

        var gruppetildelinger = await db.Myndighetstildelinger
            .Where(m => m.VirksomhetId == virksomhetId)
            .Join(
                db.Begreper.Where(b => b.Begrepskategori == "gruppe"),
                m => m.GruppeBegrepId, b => b.Id,
                (m, b) => new Gruppetildeling(m.Id, b.Id, b.Term))
            .ToListAsync(ct);

        return new Resultat(forekomster, gruppetildelinger);
    }
}
