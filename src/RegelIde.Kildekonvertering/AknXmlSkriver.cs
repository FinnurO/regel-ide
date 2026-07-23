using System.Security;
using System.Text;

namespace RegelIde.Kildekonvertering;

/// <summary>
/// Steg 7 i konverteringspipelinen: genererer kanonisk AKN-XML fra den allerede parsede noden-treet
/// og FRBR-metadataen, per struktur/eksempel i docs/08-byggesteg1-teknisk-design.md §1.1/§1.3.
/// Referansielt transparent (§3): samme (metadata, noder) gir alltid bit-identisk XML, uavhengig av
/// <paramref name="importDato"/>-parameteren (som kun påvirker FRBRManifestation/@date, §1.1).
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
        var segment = m.Kildetype == Kildetype.Lov ? "lov" : "forskrift";
        // FRBRWork/FRBRthis/FRBRuri er ELI-en uten språk-/manifestasjonssuffiks — utledes fra m.Eli
        // ved å fjerne det avsluttende "/nor" (§1.1s eksempel skiller Work fra Expression nettopp slik).
        var workUri = m.Eli.EndsWith("/nor", StringComparison.Ordinal) ? m.Eli[..^4] : m.Eli;
        var expressionUri = m.Eli;
        var manifestationUri = $"{m.Eli}.xml";

        sb.Append("<meta>");
        sb.Append("<identification source=\"#regel-ide\">");

        sb.Append("<FRBRWork>");
        sb.Append($"<FRBRthis value=\"{Escape(workUri)}\"/>");
        sb.Append($"<FRBRuri value=\"{Escape(workUri)}\"/>");
        // Vedtakelsesdato er ikke pålitelig tilgjengelig som eget maskinlesbart header-felt i rådataen
        // (kun i fritekst-referanser) — utelates heller enn å gjettes (§3.3: ingen gjettet fallback).
        sb.Append($"<FRBRauthor href=\"#{m.FrbrAuthorHref}\"/>");
        sb.Append("<FRBRcountry value=\"no\"/>");
        sb.Append("</FRBRWork>");

        sb.Append("<FRBRExpression>");
        sb.Append($"<FRBRthis value=\"{Escape(expressionUri)}\"/>");
        sb.Append($"<FRBRuri value=\"{Escape(expressionUri)}\"/>");
        if (m.KonsolidertDato is { } konsolidert)
        {
            sb.Append($"<FRBRdate date=\"{konsolidert:yyyy-MM-dd}\" name=\"konsolidering\"/>");
        }
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
        if (m.Kildetype == Kildetype.Lov)
        {
            sb.Append("<TLCOrganization eId=\"stortinget\" href=\"/ontology/organization/no/stortinget\" showAs=\"Stortinget\"/>");
        }
        sb.Append($"<TLCOrganization eId=\"{Escape(m.FrbrAuthorHref)}\" showAs=\"{Escape(m.FrbrAuthorShowAs)}\"/>");
        sb.Append("<TLCOrganization eId=\"lovdata\" href=\"/ontology/organization/no/lovdata\" showAs=\"Lovdata\"/>");
        sb.Append("</references>");

        sb.Append("<proprietary source=\"#regel-ide\">");
        sb.Append($"<regelIde:eli>{Escape(m.Eli)}</regelIde:eli>");
        sb.Append($"<regelIde:kildetype>{Escape(m.Kildetype.ToString())}</regelIde:kildetype>");
        sb.Append($"<regelIde:status>{Escape(m.Status)}</regelIde:status>");
        sb.Append($"<regelIde:ansvarligDepartement>{Escape(m.AnsvarligDepartement)}</regelIde:ansvarligDepartement>");
        sb.Append("</proprietary>");

        sb.Append("</meta>");
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
                    sb.Append($"<hcontainer eId=\"{Escape(node.Eid)}\">");
                    sb.Append($"<num>{Escape(node.Nummer ?? "")}.</num>");
                    if (!string.IsNullOrEmpty(node.Overskrift)) sb.Append($"<heading>{Escape(node.Overskrift)}</heading>");
                    SkrivNoder(sb, alleNoder, node.Eid);
                    sb.Append("</hcontainer>");
                    break;

                case NodeType.Paragraf:
                    sb.Append($"<article eId=\"{Escape(node.Eid)}\" kildeId=\"{Escape(node.KildeId)}\"");
                    if (node.Opphevet && node.OpphevetDato is { } opphevetDato)
                    {
                        // §3.2: opphevet paragraf skal alltid produsere en node. AKNs temporal-gruppe
                        // (start/end) er ikke bekreftet i eksakt attributtkombinasjon (§6 punkt 2) —
                        // regel-IDEs egen <proprietary>-markering brukes derfor som reserveløsning,
                        // ikke som erstatning for end-attributtet når det kan settes trygt.
                        sb.Append($" end=\"{opphevetDato:yyyy-MM-dd}\"");
                    }
                    sb.Append(">");
                    sb.Append($"<num>{Escape(node.Nummer ?? "")}</num>");
                    if (!string.IsNullOrEmpty(node.Overskrift)) sb.Append($"<heading>{Escape(node.Overskrift)}</heading>");
                    if (node.Opphevet)
                    {
                        sb.Append($"<proprietary source=\"#regel-ide\"><regelIde:opphevet dato=\"{node.OpphevetDato:yyyy-MM-dd}\"/></proprietary>");
                    }
                    SkrivNoder(sb, alleNoder, node.Eid);
                    foreach (var fotnote in node.Fotnoter)
                    {
                        sb.Append($"<authorialNote marker=\"{Escape(fotnote.Etikett)}\"><p>{Escape(fotnote.Tekst)}</p></authorialNote>");
                    }
                    sb.Append("</article>");
                    break;

                case NodeType.Ledd:
                    sb.Append($"<paragraph eId=\"{Escape(node.Eid)}\" kildeId=\"{Escape(node.KildeId)}\">");
                    sb.Append("<content>").Append("<p>").Append(SkrivSegmenter(node.Segmenter)).Append("</p>").Append("</content>");
                    sb.Append("</paragraph>");
                    // Punkt-barn (samme ParentEid = dette leddets eId) skrives ikke rekursivt via
                    // SkrivNoder her fordi <paragraph> i AKN ikke har et eget "barn-steg" i vårt skjema —
                    // de skrives som søsken-<point>-elementer rett etter, se under.
                    var punktBarn = alleNoder.Where(n => n.ParentEid == node.Eid).OrderBy(n => n.SorteringsRekkefolge).ToList();
                    if (punktBarn.Count > 0)
                    {
                        sb.Append("<list>");
                        foreach (var punkt in punktBarn)
                        {
                            sb.Append($"<point eId=\"{Escape(punkt.Eid)}\" kildeId=\"{Escape(punkt.KildeId)}\">");
                            sb.Append("<content>").Append("<p>").Append(SkrivSegmenter(punkt.Segmenter)).Append("</p>").Append("</content>");
                            sb.Append("</point>");
                        }
                        sb.Append("</list>");
                    }
                    break;

                case NodeType.Punkt:
                    // Skrevet som del av <list> over, sammen med sitt ledd — ingen egen håndtering her.
                    break;
            }
        }
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
