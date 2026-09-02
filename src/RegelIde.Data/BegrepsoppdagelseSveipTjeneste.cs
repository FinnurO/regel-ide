using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Sveipefunksjonen (docs/24 §3/§4, byggerekkefølgens steg 3) — mønstergjenkjenning KUN over
/// allerede-importerte <see cref="RettskildeNodeEntitet"/>-rader, ALDRI rå HTML/PDF (docs/24, gjentatt
/// flere steder som et grunnprinsipp). Dekker de to mønstrene spesifikasjonen selv anbefaler å starte
/// med ("høyest konfidens, strukturelt enklest", docs/24 §3):
/// <list type="bullet">
/// <item><b>M1 — eksplisitt definisjonsliste:</b> en <c>ledd</c>-node som enten (a) selv inneholder
/// intro-frasen "... menes med" (typisk "I forskriften/loven her menes med:"), ELLER (b) er første ledd
/// under en <c>paragraf</c> hvis <c>Overskrift</c> inneholder "definisjon" (typisk "Definisjoner") —
/// etterfulgt av direkte <c>punkt</c>-barn av formen <c>"term: forklaring"</c>. Validert konkret mot
/// FOR-2015-06-25-793 (pasientreiseforskriften) § 1 — se <see cref="BegrepsoppdagelseSveipTjenesteTests"/>.</item>
/// <item><b>M11 — egen definisjonsparagraf uten punktliste:</b> en <c>paragraf</c>-node hvis
/// <c>Overskrift</c> ER selve termen, og hvis FØRSTE <c>ledd</c>-barns <c>Tekst</c> inneholder den
/// eksplisitte markøren "Med {term} menes/forstås/regnes" — samme "menes"-familie som M1s intro-frase,
/// bare entall/singel-term i stedet for en innledning til en liste. Validert mot folketrygdloven
/// §§ 1-8, 1-9, 1-10, 13-3 (bekreftet i den kjørende dev-databasen, 2026-09-02: "Med arbeidstaker menes
/// i denne loven ...", "Med frilanser menes ...", "Med selvstendig næringsdrivende menes ...", "Med
/// yrkesskade menes ...").</item>
/// </list>
/// <para>
/// <b>Hvorfor markør-KRAV, ikke bare "Overskrift er et kort substantiv":</b> uten et eksplisitt
/// "menes/forstås/regnes"-krav ville M11 truffet enhver kort paragraf-overskrift (f.eks. "Formål",
/// "Grunnbeløpet", "Sluttpoengtallet") uansett om selve paragrafen faktisk DEFINERER overskriften som
/// term eller bare beskriver noe med det navnet — copula-varianter ("X er ...", uten "menes") er
/// nettopp M13, som docs/24 §4 eksplisitt flagger som høyest falsk-positiv-risiko og derfor UTENFOR
/// scope denne runden. Markør-kravet er det som gjør M11 "strukturelt enkelt, høy konfidens" i praksis,
/// ikke bare i teorien.
/// </para>
/// <para>
/// <b>Scope (docs/24 §2.1) settes alltid til <c>'hele_dokumentet'</c> for begge mønstre denne runden:</b>
/// selve fraseringen begge mønstrene krever ("i forskriften HER", "i denne LOVEN") er allerede en
/// eksplisitt heldokument-erklæring — en finere paragraf-/kapittel-scopet gjenkjenning (for definisjoner
/// som eksplisitt sier "i dette kapitlet") er ikke bygget denne runden, dokumentert forenkling, ikke en
/// stille antakelse.
/// </para>
/// <para>
/// <b>Delt/nasjonal + gjeldende scoping</b> — samme defensive filter som
/// <see cref="VirksomhetKandidatSveipTjeneste"/>/<see cref="NavnekandidatOppdagelseTjeneste"/> allerede
/// bruker (og som begge måtte rettes til EKSPLISITT etter reelle kryssvirksomhet-lekkasjer, Agder/Bergen
/// 2026-08-22 og gjentatt 2026-08-30): kun <c>Rettskilde.VirksomhetId == null &amp;&amp;
/// Entitetsstatus == "gjeldende"</c> sveipes. IKKE eksplisitt påkrevd av docs/24 selv — et bevisst,
/// forebyggende designvalg i denne runden, for å ikke gjenta samme feilklasse en tredje gang.
/// </para>
/// </summary>
public sealed class BegrepsoppdagelseSveipTjeneste(RegelIdeDbContext db, BegrepsforekomstTjeneste forekomstkø)
{
    /// <summary>Paragraf-overskrift-signalet for M1 (docs/24 §1.3: "typisk med overskrift
    /// 'Definisjoner'/tilsvarende"). Enkelt "inneholder"-sjekk, ikke eksakt likhet — dekker også f.eks.
    /// "Definisjoner og forkortelser".</summary>
    private static readonly Regex M1ParagrafOverskriftMønster = new("definisjon", RegexOptions.IgnoreCase);

    /// <summary>Ledd-tekst-signalet for M1 (docs/24 §1.3: "innledende tekst som 'I forskriften/loven her
    /// menes med:'"). Krever at frasen står HELT på slutten av leddets tekst (ev. med etterfølgende
    /// kolon) — akkurat der punktlisten begynner, ikke en tilfeldig forekomst av ordene midt i en lengre
    /// setning.</summary>
    private static readonly Regex M1LeddTekstMønster = new(@"\bmenes med\s*:?\s*$", RegexOptions.IgnoreCase);

    /// <summary>De eksplisitte definisjons-markørene M11 krever rett etter selve termen (samme
    /// "menes"-familie som <see cref="M1LeddTekstMønster"/>, men entall/singel-term). "forstås"/"regnes"
    /// er ekte, brukte varianter i norsk lovtekst ved siden av "menes" — IKKE copula ("X er ...", det er
    /// M13, se klassekommentaren).</summary>
    private const string M11MarkørOrd = "menes|forst[åa]s|regnes";

    /// <summary>Øvre lengdegrense for en M11-kandidat-Overskrift — en ren, enkelt-term-overskrift
    /// ("Arbeidstaker", "Yrkesskade") er alltid kort. Utelukker samtidig lange, sammensatte
    /// paragraf-titler som ikke er egnet som en enkelt term (defensivt, reduserer unødvendig regex-arbeid
    /// mer enn det faktisk endrer treffsettet — markør-kravet under er den reelle presisjons-vokteren).</summary>
    private const int M11MaksOverskriftLengde = 60;

    public async Task<BegrepsoppdagelseSveipResultat> SveipAsync(Guid? rettskildeId, string opprettetAv, CancellationToken ct = default)
    {
        if (rettskildeId is not null && !await db.Rettskilder.AnyAsync(
                r => r.Id == rettskildeId && r.VirksomhetId == null && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException(
                $"Fant ingen gjeldende, delt/nasjonal rettskilde med id '{rettskildeId}'. Ingen gjettet fallback.");
        }

        var rettskildeIder = rettskildeId is not null
            ? [rettskildeId.Value]
            : await db.Rettskilder.Where(r => r.VirksomhetId == null && r.Entitetsstatus == "gjeldende")
                .Select(r => r.Id).ToListAsync(ct);

        var antallTreff = 0;
        var antallNyeForekomster = 0;
        foreach (var enkeltRettskildeId in rettskildeIder)
        {
            var noder = await db.RettskildeNoder
                .Where(n => n.RettskildeId == enkeltRettskildeId && n.Entitetsstatus == "gjeldende")
                .Select(n => new NodeSnapshot(
                    n.Id, n.ParentNodeId, n.Eid, n.NodeType, n.Overskrift, n.Tekst, n.Sorteringsrekkefolge, n.Opphevet))
                .ToListAsync(ct);

            foreach (var funn in FinnForekomster(noder))
            {
                antallTreff++;
                var forAntall = await db.Begrepsforekomster.CountAsync(
                    k => k.RettskildeId == enkeltRettskildeId && k.NodeEid == funn.NodeEid && k.StartOffset == funn.StartOffset, ct);
                await forekomstkø.OpprettEllerFinnAsync(
                    enkeltRettskildeId, funn.NodeEid, funn.Begrep, funn.BegrepOriginal, funn.Definisjon,
                    funn.Kildetype, funn.MonsterId, funn.Konfidens, funn.Scope, funn.ScopeRefEid,
                    funn.StartOffset, funn.EndOffset, opprettetAv, ct);
                if (forAntall == 0) antallNyeForekomster++;
            }
        }

        return new BegrepsoppdagelseSveipResultat(antallTreff, antallNyeForekomster);
    }

    /// <summary>
    /// Ren, testbar funksjon uten DB-avhengighet — selve M1/M11-mønstergjenkjenningen, separert fra
    /// sveipets DB-orkestrering (samme "internal static, ingen embedded Postgres nødvendig for å teste
    /// selve klassifiseringen" -mønster som <see cref="NavnekandidatOppdagelseTjeneste.FinnKandidaterITekst"/>).
    /// Opererer på ÉN rettskildes fulle node-tre om gangen (<paramref name="noder"/>) — sveipet kaller
    /// denne én gang per rettskilde.
    /// </summary>
    internal static List<ForekomstFunn> FinnForekomster(IReadOnlyList<NodeSnapshot> noder)
    {
        var byId = noder.ToDictionary(n => n.Id);
        var barnAvForelder = noder
            .Where(n => n.ParentNodeId is not null)
            .GroupBy(n => n.ParentNodeId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Sorteringsrekkefolge).ToList());

        var funnet = new List<ForekomstFunn>();

        foreach (var ledd in noder.Where(n => n.NodeType == "ledd" && !n.Opphevet))
        {
            var forelder = ledd.ParentNodeId is not null && byId.TryGetValue(ledd.ParentNodeId.Value, out var p) ? p : null;
            var triggeretAvOverskrift = forelder is { NodeType: "paragraf", Overskrift: not null }
                                        && M1ParagrafOverskriftMønster.IsMatch(forelder.Overskrift);
            var triggeretAvTekst = ledd.Tekst is not null && M1LeddTekstMønster.IsMatch(ledd.Tekst);
            if (!triggeretAvOverskrift && !triggeretAvTekst) continue;

            if (!barnAvForelder.TryGetValue(ledd.Id, out var punktBarn)) continue;
            foreach (var punkt in punktBarn.Where(n => n.NodeType == "punkt" && !n.Opphevet))
            {
                if (punkt.Tekst is null) continue;
                var kolonIndeks = punkt.Tekst.IndexOf(':');
                if (kolonIndeks <= 0) continue; // ingen term funnet, eller teksten starter med kolon.

                var termRaa = punkt.Tekst[..kolonIndeks];
                var term = termRaa.Trim();
                if (term.Length == 0) continue;
                var definisjon = punkt.Tekst[(kolonIndeks + 1)..].Trim();
                if (definisjon.Length == 0) continue; // defensivt — "term:" uten forklaring, ingen gjettet fallback.

                var start = termRaa.Length - termRaa.TrimStart().Length;
                var slutt = start + term.Length;
                funnet.Add(new ForekomstFunn(
                    punkt.Eid, term.ToLowerInvariant(), term, definisjon,
                    "eksplisitt_liste", "M1", "hoy", "hele_dokumentet", null, start, slutt));
            }
        }

        foreach (var paragraf in noder.Where(n =>
                     n.NodeType == "paragraf" && !n.Opphevet && n.Overskrift is not null
                     && n.Overskrift.Trim().Length is > 0 and <= M11MaksOverskriftLengde))
        {
            if (!barnAvForelder.TryGetValue(paragraf.Id, out var barn)) continue;
            var forsteLedd = barn.FirstOrDefault(n => n.NodeType == "ledd" && !n.Opphevet);
            if (forsteLedd?.Tekst is null) continue;

            var term = paragraf.Overskrift!.Trim();
            var mønster = new Regex(
                $@"\bMed\s+(?<term>{Regex.Escape(term)})\s+(?:{M11MarkørOrd})\b", RegexOptions.IgnoreCase);
            var treff = mønster.Match(forsteLedd.Tekst);
            if (!treff.Success) continue;

            var termGruppe = treff.Groups["term"];
            var begrepOriginal = termGruppe.Value;
            funnet.Add(new ForekomstFunn(
                forsteLedd.Eid, begrepOriginal.ToLowerInvariant(), begrepOriginal, forsteLedd.Tekst,
                "egen_paragraf", "M11", "hoy", "hele_dokumentet", null, termGruppe.Index, termGruppe.Index + termGruppe.Length));
        }

        return funnet;
    }
}

/// <summary>Minimal, flat projeksjon av én <see cref="RettskildeNodeEntitet"/>-rad — kun feltene
/// <see cref="BegrepsoppdagelseSveipTjeneste.FinnForekomster"/> faktisk trenger, slik at den rene
/// klassifiseringsfunksjonen kan testes uten en hel DB-entitet.</summary>
internal sealed record NodeSnapshot(
    Guid Id, Guid? ParentNodeId, string Eid, string NodeType, string? Overskrift, string? Tekst,
    int Sorteringsrekkefolge, bool Opphevet);

/// <summary>Ett M1/M11-treff, klar til å legges i <see cref="BegrepsforekomstTjeneste.OpprettEllerFinnAsync"/>.</summary>
internal sealed record ForekomstFunn(
    string NodeEid, string Begrep, string BegrepOriginal, string Definisjon, string Kildetype, string MonsterId,
    string Konfidens, string Scope, string? ScopeRefEid, int StartOffset, int EndOffset);

/// <summary>Oppsummering av ett sveip — samme "AntallTreffFunnet teller alt, AntallNyeForekomster kun
/// de faktisk nye" -skille som <see cref="VirksomhetKandidatSveipResultat"/>/<see cref="NavnekandidatSveipResultat"/>.</summary>
public sealed record BegrepsoppdagelseSveipResultat(int AntallTreffFunnet, int AntallNyeForekomster);
