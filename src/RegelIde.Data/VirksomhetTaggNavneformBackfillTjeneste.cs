using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Engangs-/gjentakbar flytting av <see cref="TekstTaggEntitet.RefId"/> for
/// <c>Kind='virksomhet'</c>-tagger: fra å peke DIREKTE på en <see cref="Virksomhet"/> (slik #204
/// innførte) til å peke på NAVNEFORMEN — <see cref="BegrepEntitet"/> med
/// <see cref="BegrepEntitet.Begrepskategori"/> = <c>'virksomhet'</c>. Se
/// <see cref="TekstTaggEntitet.RefId"/> sin kommentar for hvorfor kjeden skal gå via navneformen.
///
/// <para>
/// Uten denne ville de eksisterende radene (seedens 14 kommunenavn i forskrift 2005-06-17-657, pluss
/// alt veiviseren har koblet — bl.a. «Reindriftsstyret» og «Landbruksdirektoratet») blitt stående med
/// en <c>RefId</c> mot en <see cref="Virksomhet"/>, som etter endringen ikke lenger er et gyldig
/// referansemål. UI-et ville da ikke klart å resolve dem, og taggene hadde vist en rå GUID.
/// </para>
///
/// <para>
/// <b>Matchingen er EKSAKT, aldri gjettet</b> (§3.3): navneformen må ha <c>Term</c> lik taggens
/// <see cref="TekstTaggEntitet.QuoteExact"/> OG <see cref="BegrepEntitet.VirksomhetReferanseId"/> lik
/// taggens NÅVÆRENDE <c>RefId</c>. Begge betingelsene, ikke én av dem: <c>Term</c> alene er tvetydig
/// (to virksomheter kan ha samme kortform), og virksomheten alene er tvetydig ved SYNONYMER (flere
/// navneformer på samme virksomhet — nettopp det tilfellet hele denne runden handler om). Finnes ingen
/// navneform som oppfyller BEGGE, blir raden stående URØRT og RAPPORTERT — det opprettes ingen
/// navneform for å få migreringen til å se komplett ut, og det velges ikke en «nærmeste» navneform.
/// </para>
///
/// <para>
/// <b>Idempotent</b> — en tagg hvis <c>RefId</c> allerede peker på en navneform gjenkjennes og hoppes
/// over (ingen skriving, ikke rapportert som et problem). Andre kjøring flytter derfor 0 rader. Merk at
/// idempotensen bygger på at de to referansemålene er DISJUNKTE tabeller: en id kan ikke være både en
/// <see cref="Virksomhet"/> og et <see cref="BegrepEntitet"/>, så «er denne raden alt flyttet?» er et
/// entydig spørsmål, ikke en heuristikk.
/// </para>
///
/// <para>
/// <b>Respekterer <c>tekst_tagger_unik_tagg</c></b> — den unike indeksen dekker
/// (VirksomhetId, RettskildeId, NodeEid, StartOffset, EndOffset, Kind, RefId). Skulle det alt finnes en
/// tagg på nøyaktig samme posisjon som PEKER på navneformen vi vil flytte til, ville en blind
/// oppdatering veltet med en <see cref="DbUpdateException"/> og tatt hele oppstarten med seg. Den gamle
/// raden ARKIVERES i stedet (<c>Entitetsstatus='arkivert'</c>, samme soft-delete som
/// <see cref="TekstTaggTjeneste.SlettAsync"/>): posisjonen er allerede korrekt tagget, så den gamle
/// virksomhet-pekende raden er en FORELDET DUBLETT av en påstand som nå står riktig — ikke noe som
/// mangler. Å la den ligge som <c>'gjeldende'</c> ville gitt TO markeringer på samme ord i løpeteksten,
/// hvorav den ene ikke lar seg resolve.
/// </para>
///
/// <para>
/// <b>Kjøres FØR <see cref="SamiskSprakforvaltningSeed"/>, ikke etter</b> — rekkefølgen er ikke
/// vilkårlig, og feil rekkefølge er observert i praksis: seeden slår opp «finnes taggen alt?» på
/// (Kind, RefId), og med RefId nå = navneformens id kjenner den IKKE igjen en gammel rad som peker på
/// virksomheten. Kjørte migreringen etterpå, opprettet seeden derfor en ny tagg ved siden av hver
/// gamle (14 dubletter ved første oppstart etter denne runden). Migreres FØRST, finner seeden radene
/// som allerede riktige og gjør ingenting. På en tom database er migreringen en no-op, så
/// rekkefølgen koster ingenting der.
/// </para>
/// </summary>
public static class VirksomhetTaggNavneformBackfillTjeneste
{
    /// <summary>Én flyttet tagg — for oppstartslogging, slik at migreringen er etterprøvbar.</summary>
    public sealed record FlyttetTagg(Guid TaggId, string QuoteExact, Guid FraVirksomhetId, Guid TilNavneformId);

    /// <summary>
    /// Én rad som IKKE kunne flyttes, med grunnen. Disse er hele poenget med å returnere et resultat i
    /// stedet for bare et antall: en urørt rad er en rad UI-et ikke klarer å resolve, og den skal være
    /// synlig i oppstartsloggen — ikke stilltiende akseptert.
    /// </summary>
    public sealed record UflyttbarTagg(Guid TaggId, string QuoteExact, Guid? RefId, string Grunn);

    /// <summary>
    /// En FORELDET dublett som ble arkivert: posisjonen var allerede korrekt tagget mot navneformen,
    /// så den gamle virksomhet-pekende raden var en duplikat-påstand. Se arkiveringsgrenen under.
    /// </summary>
    public sealed record ArkivertDublett(Guid TaggId, string QuoteExact, Guid NavneformId);

    public sealed record Resultat(
        IReadOnlyList<FlyttetTagg> Flyttet,
        IReadOnlyList<UflyttbarTagg> Uflyttbare,
        IReadOnlyList<ArkivertDublett> ArkiverteDubletter,
        int AlleredeFlyttet);

    public static async Task<Resultat> KjorAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var tagger = await db.TekstTagger
            .Where(t => t.Kind == "virksomhet" && t.RefId != null && t.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        if (tagger.Count == 0) return new Resultat([], [], [], 0);

        var refIder = tagger.Select(t => t.RefId!.Value).Distinct().ToList();

        // Hvilke av de nåværende RefId-ene er navneformer (⇒ alt flyttet), og hvilke er virksomheter
        // (⇒ skal flyttes)? Slått opp i bulk framfor én spørring per tagg.
        var alleredeNavneformIder = await db.Begreper
            .Where(b => refIder.Contains(b.Id) && b.Begrepskategori == "virksomhet")
            .Select(b => b.Id)
            .ToListAsync(ct);
        var navneformIdSett = alleredeNavneformIder.ToHashSet();

        var virksomhetIder = await db.Virksomheter
            .Where(v => refIder.Contains(v.Id))
            .Select(v => v.Id)
            .ToListAsync(ct);
        var virksomhetIdSett = virksomhetIder.ToHashSet();

        // Kandidat-navneformene for de virksomhetene taggene faktisk peker på, nøklet på
        // (virksomhet, term) — presis det sammensatte oppslaget matchingen krever.
        var navneformer = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende"
                        && b.VirksomhetReferanseId != null
                        && virksomhetIdSett.Contains(b.VirksomhetReferanseId.Value))
            .Select(b => new { b.Id, b.Term, VirksomhetId = b.VirksomhetReferanseId!.Value })
            .ToListAsync(ct);
        var navneformPerVirksomhetOgTerm = navneformer
            .GroupBy(n => (n.VirksomhetId, n.Term), TupleSammenligner)
            .ToDictionary(g => g.Key, g => g.First().Id, TupleSammenligner);

        var flyttet = new List<FlyttetTagg>();
        var uflyttbare = new List<UflyttbarTagg>();
        var arkiverteDubletter = new List<ArkivertDublett>();
        var alleredeFlyttet = 0;

        foreach (var tagg in tagger)
        {
            var refId = tagg.RefId!.Value;

            if (navneformIdSett.Contains(refId))
            {
                alleredeFlyttet++; // peker alt på en navneform — se idempotens-avsnittet.
                continue;
            }

            if (!virksomhetIdSett.Contains(refId))
            {
                // Peker verken på en navneform eller en virksomhet — en foreldreløs referanse som
                // fantes FØR denne runden (det finnes ingen FK på RefId). Ikke vår å reparere her.
                uflyttbare.Add(new UflyttbarTagg(
                    tagg.Id, tagg.QuoteExact, refId,
                    "RefId peker verken på en virksomhet eller en navneform — foreldreløs referanse, urørt."));
                continue;
            }

            if (!navneformPerVirksomhetOgTerm.TryGetValue((refId, tagg.QuoteExact), out var navneformId))
            {
                uflyttbare.Add(new UflyttbarTagg(
                    tagg.Id, tagg.QuoteExact, refId,
                    $"Fant ingen gjeldende navneform med Term = «{tagg.QuoteExact}» for virksomheten — "
                    + "ingen gjettet fallback, raden står urørt."));
                continue;
            }

            // Kollisjonssjekk mot tekst_tagger_unik_tagg, se klassekommentaren.
            var duplikat = tagger.Any(annen =>
                annen.Id != tagg.Id && annen.VirksomhetId == tagg.VirksomhetId
                && annen.RettskildeId == tagg.RettskildeId && annen.NodeEid == tagg.NodeEid
                && annen.StartOffset == tagg.StartOffset && annen.EndOffset == tagg.EndOffset
                && annen.RefId == navneformId);
            if (duplikat)
            {
                // Posisjonen ER allerede riktig tagget — den gamle raden er en FORELDET DUBLETT av en
                // påstand som nå står korrekt et annet sted, ikke en rad som mangler noe. Å la den
                // ligge ville gitt to markeringer på samme ord i løpeteksten og en tagg UI-et ikke
                // klarer å resolve. Arkiveres derfor (samme soft-delete som TekstTaggTjeneste.SlettAsync
                // bruker — raden og dens proveniens bevares, den er bare ikke lenger 'gjeldende').
                tagg.Entitetsstatus = "arkivert";
                db.Proveniens.Add(ProveniensHjelper.NyRad(
                    "tekst_tagg", tagg.Id, tagg.VirksomhetId, "arkivert",
                    "system (navneform-kjede-migrering)"));
                arkiverteDubletter.Add(new ArkivertDublett(tagg.Id, tagg.QuoteExact, navneformId));
                continue;
            }

            tagg.RefId = navneformId;
            db.Proveniens.Add(ProveniensHjelper.NyRad(
                "tekst_tagg", tagg.Id, tagg.VirksomhetId, "endret", "system (navneform-kjede-migrering)"));
            flyttet.Add(new FlyttetTagg(tagg.Id, tagg.QuoteExact, refId, navneformId));
        }

        if (flyttet.Count > 0 || arkiverteDubletter.Count > 0) await db.SaveChangesAsync(ct);
        return new Resultat(flyttet, uflyttbare, arkiverteDubletter, alleredeFlyttet);
    }

    /// <summary>
    /// <c>Term</c> sammenlignes ORDINALT (tegn for tegn), samme som <c>QuoteExact</c>-valideringen i
    /// <see cref="TekstTaggTjeneste.OpprettAsync"/> og som alle andre Term-oppslag i kodebasen
    /// (<c>b.Term == kandidat.ForeslattTekst</c>). Et case-ufølsomt oppslag ville vært en gjetting: at
    /// teksten skriver «karasjok» der navneformen heter «Karasjok» er en reell forskjell en
    /// saksbehandler skal se, ikke noe migreringen skal glatte over.
    /// </summary>
    private static readonly TupleComparer TupleSammenligner = new();

    private sealed class TupleComparer : IEqualityComparer<(Guid VirksomhetId, string Term)>
    {
        public bool Equals((Guid VirksomhetId, string Term) a, (Guid VirksomhetId, string Term) b) =>
            a.VirksomhetId == b.VirksomhetId && string.Equals(a.Term, b.Term, StringComparison.Ordinal);

        public int GetHashCode((Guid VirksomhetId, string Term) x) =>
            HashCode.Combine(x.VirksomhetId, StringComparer.Ordinal.GetHashCode(x.Term));
    }
}
