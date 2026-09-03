using System.Security;
using System.Text;

namespace RegelIde.Kildekonvertering;

/// <summary>
/// Steg 7 i konverteringspipelinen: genererer kanonisk AKN-XML fra den allerede parsede noden-treet
/// og FRBR-metadataen, per struktur/eksempel i docs/08-byggesteg1-teknisk-design.md §1.1/§1.3.
/// Referansielt transparent (§3): samme (metadata, noder) gir alltid bit-identisk XML, uavhengig av
/// <paramref name="importDato"/>-parameteren (som kun påvirker FRBRManifestation/@date, §1.1).
///
/// Output er validert mot den offisielle AKN 3.0-skjemaen (akomantoso30.xsd, OASIS LegalDocML v1.0) —
/// se <c>AknXmlSkjemaValideringTests.cs</c>. Følgende er derfor bevisste valg, ikke tilfeldigheter:
/// - Egendefinerte data (kildeId, opphevet-markering) skrives som attributter i <c>regelIde:</c>-
///   navnerommet, ALDRI som uprefikserte attributter eller som &lt;proprietary&gt;-barn på hierarkiske
///   elementer (&lt;article&gt;/&lt;paragraph&gt;/&lt;point&gt;) — skjemaets eneste utvidelsesmekanisme
///   for attributter er <c>xsd:anyAttribute namespace="##other"</c> (attributeGroup "core"), og
///   &lt;proprietary&gt; er kun gyldig som barn av &lt;meta&gt;, ikke av hierarkiske elementer
///   (docs/13-backlog.md pkt. 9).
/// - Fotnoter (&lt;authorialNote&gt;) skrives inline i SISTE ledds &lt;p&gt;, ikke som egne siblings av
///   &lt;article&gt;s barn — authorialNote er et subFlow-element, kun lovlig i tekstflyt (mixed content).
/// - FRBRWork/FRBRExpression har alltid minst ett &lt;FRBRdate&gt; (skjemaet krever det, coreProperties),
///   plassert i riktig sekvens FØR FRBRauthor — se <see cref="SkrivFrbrDato"/>.
/// </summary>
public static class AknXmlSkriver
{
    public static string Skriv(RettskildeMetadata m, IReadOnlyList<RettskildeNode> noder, DateOnly importDato)
    {
        var sb = new StringBuilder();
        var rotnavn = m.Kildetype == Kildetype.Lov ? "lov" : "forskrift";

        sb.Append("<akomaNtoso xmlns=\"http://docs.oasis-open.org/legaldocml/ns/akn/3.0\" xmlns:regelIde=\"https://regel-ide.no/ns/akn-utvidelse/1.0\">");
        sb.Append($"<act name=\"{rotnavn}\">");
        SkrivMeta(sb, m, importDato);
        sb.Append("<preface>").Append($"<p>{Escape(m.Tittel)}</p>").Append("</preface>");
        sb.Append("<body>");
        SkrivNoder(sb, noder, parentEid: null);
        sb.Append("</body>");
        sb.Append("</act>");
        sb.Append("</akomaNtoso>");
        return sb.ToString();
    }

    private static void SkrivMeta(StringBuilder sb, RettskildeMetadata m, DateOnly importDato)
    {
        var workUri = m.Eli.EndsWith("/nor", StringComparison.Ordinal) ? m.Eli[..^4] : m.Eli;
        var expressionUri = m.Eli;
        var manifestationUri = $"{m.Eli}.xml";

        sb.Append("<meta>");
        sb.Append("<identification source=\"#regel-ide\">");

        // coreProperties (skjemaet) krever rekkefølgen FRBRthis, FRBRuri, [FRBRalias]*, FRBRdate+,
        // FRBRauthor+ — deretter FRBRWork/FRBRExpression-spesifikke felt. FRBRauthor FØR FRBRdate
        // (som i den gamle koden) er derfor like ugyldig som å utelate FRBRdate helt.
        sb.Append("<FRBRWork>");
        sb.Append($"<FRBRthis value=\"{Escape(workUri)}\"/>");
        sb.Append($"<FRBRuri value=\"{Escape(workUri)}\"/>");
        // Vedtakelsesdato (datoen Stortinget faktisk vedtok loven) er ikke pålitelig tilgjengelig som
        // eget maskinlesbart header-felt i rådataen (kun i fritekst-referanser). §3.3-prinsippet "ingen
        // gjettet fallback" gjelder fortsatt for DEN datoen spesifikt — men skjemaet krever minst ett
        // FRBRdate-element uansett. Løsning (avklart 2026-08-12): bruk Ikrafttredelse — en reell,
        // allerede innhentet dato, bare merket ærlig som noe ANNET enn vedtakelse — og kun når heller
        // ikke den finnes, en eksplisitt "ukjent"-sentinel (IKKE en gjettet dato). Se
        // <see cref="SkrivFrbrDato"/> og docs/13-backlog.md pkt. 9.
        SkrivFrbrDato(sb, foretrukket: null, m.Ikrafttredelse);
        sb.Append($"<FRBRauthor href=\"#{m.FrbrAuthorHref}\"/>");
        sb.Append("<FRBRcountry value=\"no\"/>");
        sb.Append("</FRBRWork>");

        sb.Append("<FRBRExpression>");
        sb.Append($"<FRBRthis value=\"{Escape(expressionUri)}\"/>");
        sb.Append($"<FRBRuri value=\"{Escape(expressionUri)}\"/>");
        SkrivFrbrDato(sb, m.KonsolidertDato is { } konsolidert ? (konsolidert, "konsolidering") : null, m.Ikrafttredelse);
        sb.Append("<FRBRauthor href=\"#lovdata\"/>");
        sb.Append("<FRBRlanguage language=\"nor\"/>");
        sb.Append("</FRBRExpression>");

        sb.Append("<FRBRManifestation>");
        sb.Append($"<FRBRthis value=\"{Escape(manifestationUri)}\"/>");
        sb.Append($"<FRBRuri value=\"{Escape(manifestationUri)}\"/>");
        sb.Append($"<FRBRdate date=\"{importDato:yyyy-MM-dd}\" name=\"regel-ide-import\"/>");
        sb.Append("<FRBRauthor href=\"#regel-ide\"/>");
        sb.Append("</FRBRManifestation>");

        sb.Append("</identification>");

        sb.Append("<references source=\"#regel-ide\">");
        // TLCOrganization krever href (attributeGroup "link", ##other-attributeGroup "core" gjelder
        // ikke her — href er en ordinær, PÅKREVD AKN-attributt). For Lov er FrbrAuthorHref alltid
        // "stortinget" (se Modeller.cs), altså identisk med organisasjonen under — å skrive begge ga
        // en duplikat-eId (skjemaets eId-nøkkelbegrensning på <act>) OG en TLCOrganization uten href.
        // Løsning: bygg listen, fjern duplikater på eId (§14/docs/13 pkt. 9).
        var organisasjoner = new List<(string EId, string Href, string ShowAs)>();
        if (m.Kildetype == Kildetype.Lov)
        {
            organisasjoner.Add(("stortinget", "/ontology/organization/no/stortinget", "Stortinget"));
        }
        organisasjoner.Add((m.FrbrAuthorHref, $"/ontology/organization/no/{m.FrbrAuthorHref}", m.FrbrAuthorShowAs));
        organisasjoner.Add(("lovdata", "/ontology/organization/no/lovdata", "Lovdata"));

        foreach (var org in organisasjoner.DistinctBy(o => o.EId))
        {
            sb.Append($"<TLCOrganization eId=\"{Escape(org.EId)}\" href=\"{Escape(org.Href)}\" showAs=\"{Escape(org.ShowAs)}\"/>");
        }
        sb.Append("</references>");

        sb.Append("<proprietary source=\"#regel-ide\">");
        sb.Append($"<regelIde:eli>{Escape(m.Eli)}</regelIde:eli>");
        sb.Append($"<regelIde:kildetype>{Escape(m.Kildetype.ToString())}</regelIde:kildetype>");
        sb.Append($"<regelIde:status>{Escape(m.Status)}</regelIde:status>");
        // [ENDRET, fler-verdi-departement, 2026-09-04] Ett <regelIde:ansvarligDepartement>-element PER
        // departement — Lovdata kan oppgi flere ved delt ansvar (RettskildeMetadata.AnsvarligDepartement
        // er nå en liste, ikke en kommaseparert streng). Skjemaet setter ingen maks-forekomst-grense på
        // regelIde:-elementer (anyAttribute/##other-mekanismen gjelder attributter, ikke selve
        // proprietary-barnelisten, som allerede tillater vilkårlig mange elementer). Se
        // AnsvarligDepartementBackfillTjeneste for tilsvarende lese-siden.
        foreach (var departement in m.AnsvarligDepartement)
        {
            sb.Append($"<regelIde:ansvarligDepartement>{Escape(departement)}</regelIde:ansvarligDepartement>");
        }
        sb.Append("</proprietary>");

        sb.Append("</meta>");
    }

    /// <summary>
    /// Skriver et &lt;FRBRdate&gt;-element. Skjemaet (complexType "coreProperties") krever minst ett
    /// FRBRdate per FRBR-nivå (Work/Expression) — aldri null. Prioritet: <paramref name="foretrukket"/>
    /// (en dato/navn-kombinasjon som er meningsfull for nettopp DETTE FRBR-nivået, f.eks. konsolidering
    /// for Expression) → <paramref name="ikrafttredelse"/> (reell, allerede innhentet dato — merket
    /// ærlig med <c>name="ikrafttredelse"</c>, ALDRI som vedtakelsesdato siden det ville vært en
    /// påstand vi ikke kan bekrefte) → en eksplisitt "ukjent"-sentinel. Sentinelen (9999-01-01) er en
    /// tydelig markør, IKKE en gjettet dato — §3.3-prinsippet om ingen gjettet fallback er dermed
    /// bevart for selve vedtakelsesdatoen, samtidig som skjemaets krav om minst ett FRBRdate oppfylles.
    /// </summary>
    private static void SkrivFrbrDato(StringBuilder sb, (DateOnly Dato, string Navn)? foretrukket, DateOnly? ikrafttredelse)
    {
        var (dato, navn) = foretrukket
            ?? (ikrafttredelse is { } i ? (i, "ikrafttredelse") : (new DateOnly(9999, 1, 1), "ukjent"));
        sb.Append($"<FRBRdate date=\"{dato:yyyy-MM-dd}\" name=\"{navn}\"/>");
    }

    /// <summary>Skriver noder rekursivt i sorteringsrekkefølge, gruppert per forelder via ParentEid.</summary>
    private static void SkrivNoder(StringBuilder sb, IReadOnlyList<RettskildeNode> alleNoder, string? parentEid)
    {
        var barn = alleNoder.Where(n => n.ParentEid == parentEid).OrderBy(n => n.SorteringsRekkefolge);
        foreach (var node in barn)
        {
            switch (node.NodeType)
            {
                case NodeType.Kapittel:
                    sb.Append($"<chapter eId=\"{Escape(node.Eid)}\">");
                    sb.Append($"<num>Kapittel {Escape(node.Nummer ?? "")}.</num>");
                    if (!string.IsNullOrEmpty(node.Overskrift)) sb.Append($"<heading>{Escape(node.Overskrift)}</heading>");
                    SkrivNoder(sb, alleNoder, node.Eid);
                    sb.Append("</chapter>");
                    break;

                case NodeType.Underinndeling:
                    // <hcontainer> er AKNs generiske hierarkiske element (§ kommentar i skjemaet:
                    // "The attribute name is required and gives a name to the element") — i motsetning
                    // til <chapter>/<article> har den INGEN egen betydning uten en "name"-attributt.
                    sb.Append($"<hcontainer eId=\"{Escape(node.Eid)}\" name=\"romertallgruppe\">");
                    sb.Append($"<num>{Escape(node.Nummer ?? "")}.</num>");
                    if (!string.IsNullOrEmpty(node.Overskrift)) sb.Append($"<heading>{Escape(node.Overskrift)}</heading>");
                    SkrivNoder(sb, alleNoder, node.Eid);
                    sb.Append("</hcontainer>");
                    break;

                case NodeType.Paragraf:
                    sb.Append($"<article eId=\"{Escape(node.Eid)}\" regelIde:kildeId=\"{Escape(node.KildeId)}\"");
                    if (node.Opphevet)
                    {
                        // §3.2: opphevet paragraf skal alltid produsere en node. AKNs offisielle
                        // temporal-mekanisme (attributtet "period" pekende til en <temporalGroup> i
                        // <lifecycle>) er ikke implementert her — det tidligere "end"-attributtet
                        // fantes ikke i noe attributeGroup skjemaet definerer for hierarkiske elementer
                        // og var derfor rett og slett ugyldig (bekreftet ved skjemavalidering, ikke
                        // bare "uavklart" som den gamle kommentaren sa). regelIde:-attributter er
                        // derimot skjemalovlige (anyAttribute namespace="##other") og brukes i stedet.
                        sb.Append(" regelIde:opphevet=\"true\"");
                        if (node.OpphevetDato is { } opphevetDato)
                        {
                            sb.Append($" regelIde:opphevetDato=\"{opphevetDato:yyyy-MM-dd}\"");
                        }
                    }
                    sb.Append(">");
                    sb.Append($"<num>{Escape(node.Nummer ?? "")}</num>");
                    if (!string.IsNullOrEmpty(node.Overskrift)) sb.Append($"<heading>{Escape(node.Overskrift)}</heading>");

                    // Fotnoter kan IKKE skrives som egne siblings av leddene (som før) — <authorialNote>
                    // er et subFlow-element og er kun lovlig inline i tekstflyt (f.eks. i <p>), ikke som
                    // block-nivå-barn av <article>. De legges derfor inn i SISTE ledds <p> i stedet.
                    var leddBarn = alleNoder.Where(n => n.ParentEid == node.Eid).OrderBy(n => n.SorteringsRekkefolge).ToList();
                    var fotnoteMarkup = SkrivFotnoterInline(node.Fotnoter);
                    for (var i = 0; i < leddBarn.Count; i++)
                    {
                        var erSisteLedd = i == leddBarn.Count - 1;
                        SkrivLedd(sb, alleNoder, leddBarn[i], erSisteLedd ? fotnoteMarkup : null);
                    }
                    // Ekstremt sjeldent tilfelle: paragraf har fotnote(r) men ingen ledd å feste dem i
                    // (ingen løpetekst overhodet). Det finnes ingen skjemalovlig plassering av
                    // <authorialNote> direkte på <article> — fotnoten utelates heller enn å skrive
                    // ugyldig AKN. Ikke observert i faktiske Lovdata-kilder (§14); flagg i
                    // docs/13-backlog.md dersom dette faktisk inntreffer.
                    sb.Append("</article>");
                    break;

                case NodeType.Ledd:
                    // Nås i praksis aldri via denne rekursjonen — Ledd sitt ParentEid er alltid en
                    // Paragraf sin Eid (LovdataHtmlParser), og Paragraf-casen over skriver derfor sine
                    // ledd-barn direkte (for å kunne plassere fotnoter i siste ledd). Beholdt som
                    // forsvarsverk mot fremtidige/andre nodetre-produsenter.
                    SkrivLedd(sb, alleNoder, node, ekstraInnhold: null);
                    break;

                case NodeType.Punkt:
                    // Skrevet som del av <list> i SkrivLedd, sammen med sitt ledd — ingen egen håndtering her.
                    break;
            }
        }
    }

    /// <summary>
    /// Skriver ett ledd (&lt;paragraph&gt;) og dets punkt-barn (&lt;list&gt;/&lt;point&gt;).
    /// <paramref name="ekstraInnhold"/> er allerede ferdigbygget AKN-markup (typisk &lt;authorialNote&gt;
    /// fra paragrafens fotnoter) som skal limes inn i SLUTTEN av dette leddets &lt;p&gt; — se
    /// begrunnelsen i Paragraf-casen i <see cref="SkrivNoder"/>.
    /// </summary>
    private static void SkrivLedd(StringBuilder sb, IReadOnlyList<RettskildeNode> alleNoder, RettskildeNode node, string? ekstraInnhold)
    {
        sb.Append($"<paragraph eId=\"{Escape(node.Eid)}\" regelIde:kildeId=\"{Escape(node.KildeId)}\">");
        sb.Append($"<num>{Escape(node.Nummer ?? "")}</num>");
        sb.Append("<content>").Append("<p>").Append(SkrivSegmenter(node.Segmenter));
        if (ekstraInnhold is not null)
        {
            sb.Append(ekstraInnhold);
        }
        sb.Append("</p>").Append("</content>");
        sb.Append("</paragraph>");

        // Punkt-barn (samme ParentEid = dette leddets eId) skrives ikke rekursivt via SkrivNoder her
        // fordi <paragraph> i AKN ikke har et eget "barn-steg" i vårt skjema — de skrives som søsken-
        // <point>-elementer rett etter, som søsken av <paragraph> i den omsluttende <article>.
        var punktBarn = alleNoder.Where(n => n.ParentEid == node.Eid).OrderBy(n => n.SorteringsRekkefolge).ToList();
        if (punktBarn.Count > 0)
        {
            sb.Append("<list>");
            foreach (var punkt in punktBarn)
            {
                sb.Append($"<point eId=\"{Escape(punkt.Eid)}\" regelIde:kildeId=\"{Escape(punkt.KildeId)}\">");
                sb.Append($"<num>{Escape(punkt.Nummer ?? "")}</num>");
                sb.Append("<content>").Append("<p>").Append(SkrivSegmenter(punkt.Segmenter)).Append("</p>").Append("</content>");
                sb.Append("</point>");
            }
            sb.Append("</list>");
        }
    }

    /// <summary>
    /// Bygger &lt;authorialNote&gt;-markup for en paragrafs fotnoter, ment å limes inn i slutten av
    /// et &lt;p&gt; (authorialNote er et subFlow-element, kun lovlig inline i tekstflyt). Returnerer
    /// null når det ikke er noen fotnoter (skiller "ingen fotnote" fra "tom streng å legge til").
    /// </summary>
    private static string? SkrivFotnoterInline(IReadOnlyList<Fotnote> fotnoter)
    {
        if (fotnoter.Count == 0) return null;
        var sb = new StringBuilder();
        foreach (var fotnote in fotnoter)
        {
            sb.Append($"<authorialNote marker=\"{Escape(fotnote.Etikett)}\"><p>{Escape(fotnote.Tekst)}</p></authorialNote>");
        }
        return sb.ToString();
    }

    private static string SkrivSegmenter(IReadOnlyList<TekstSegment>? segmenter)
    {
        if (segmenter is null) return "";
        var sb = new StringBuilder();
        foreach (var s in segmenter)
        {
            if (s.ReferanseTilEid is null)
            {
                sb.Append(Escape(s.Tekst));
            }
            else
            {
                sb.Append($"<ref href=\"#{Escape(s.ReferanseTilEid)}\">{Escape(s.Tekst)}</ref>");
            }
        }
        return sb.ToString();
    }

    private static string Escape(string s) => SecurityElement.Escape(s) ?? "";
}
