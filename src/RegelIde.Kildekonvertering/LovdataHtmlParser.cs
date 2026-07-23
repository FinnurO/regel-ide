using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RegelIde.Kildekonvertering;

public sealed record ParseResultat(RettskildeMetadata Metadata, IReadOnlyList<RettskildeNode> Noder, IReadOnlyList<RettskildeReferanse> Referanser);

/// <summary>
/// Steg 3-6 i konverteringspipelinen (docs/08-byggesteg1-teknisk-design.md §3.1): parse HTML til DOM,
/// ekstraher dokumentmetadata, vandre dokumentkroppen, samle kryssreferanser.
/// Forutsetter at input allerede er korrekt UTF-8 (steg 1-2 — henting/dekoding — er kallerens ansvar,
/// se LovdataKonverterer).
/// </summary>
public static partial class LovdataHtmlParser
{
    /// <summary>Konteksten en løpetekst-node trenger for å avgjøre om en kryssreferanse er intern og for å avlede eId på målet (§1.2/§3.1 steg 6).</summary>
    private sealed record ReferanseKontekst(string EgenDatokode, string EgenLovEli);

    public static ParseResultat Parse(string kildeHtml)
    {
        var doc = new HtmlDocument { OptionOutputAsXml = false };
        doc.LoadHtml(kildeHtml);

        var header = doc.DocumentNode.SelectSingleNode("//header[contains(@class,'documentHeader')]")
            ?? throw new FormatException("Fant ikke <header class=\"documentHeader\"> — ikke et gjenkjennelig Lovdata-dokument.");
        var metadata = ParseMetadata(header);
        var kontekst = new ReferanseKontekst(metadata.Datokode, metadata.Eli);

        var body = doc.DocumentNode.SelectSingleNode("//main[contains(@class,'documentBody')]")
            ?? throw new FormatException("Fant ikke <main class=\"documentBody\"> — ikke et gjenkjennelig Lovdata-dokument.");

        var noder = new List<RettskildeNode>();
        var referanser = new List<RettskildeReferanse>();
        var sortering = new SorteringsTeller();

        foreach (var child in body.ChildNodes)
        {
            if (child.NodeType != HtmlNodeType.Element) continue;
            if (child.Name == "section" && child.GetAttributeValue("class", "").Contains("section"))
            {
                ParseKapittel(child, kontekst, noder, referanser, sortering);
            }
            else if (child.Name == "h1")
            {
                // dokumenttittel, ikke en node
            }
            else if (child.Name == "article" && child.GetAttributeValue("class", "").Contains("defaultP"))
            {
                // Dokumentnivå-merknad (bekreftet i ekte data — forvaltningsloven har en varsel om at
                // hele loven oppheves fra en fremtidig dato). Samme behandling som changesToParent:
                // endringshistorikk/metainformasjon, ikke selve rettskildeteksten (§3.1 steg 5).
            }
            else
            {
                throw new NotSupportedException(
                    $"Uventet element direkte i documentBody: <{child.Name}>. " +
                    "Ingen gjettet fallback produseres (§3.3) — parseren må utvides bevisst.");
            }
        }

        return new ParseResultat(metadata, noder, referanser);
    }

    private sealed class SorteringsTeller
    {
        private int _neste;
        public int Neste() => _neste++;
    }

    // ---------- Metadata (steg 4) ----------

    private static RettskildeMetadata ParseMetadata(HtmlNode header)
    {
        // Eksakt klassematch, ikke contains(): Lovdatas "title" og "titleShort" er begge egne
        // dd-klasser der den ene er en delstreng av den andre — contains() ville plukket feil felt.
        string HentFelt(string cssClass) =>
            header.SelectSingleNode($".//dd[@class='{cssClass}']")?.InnerText.Trim()
                ?? throw new FormatException($"Påkrevd metadatafelt '{cssClass}' mangler i header. Ingen gjettet fallback (§3.3).");

        string? HentValgfritt(string cssClass) =>
            header.SelectSingleNode($".//dd[@class='{cssClass}']")?.InnerText.Trim();

        var datokode = HtmlEntity.DeEntitize(HentFelt("legacyID"));
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out var kildetype);
        var tittel = HtmlEntity.DeEntitize(HentFelt("title"));
        var kortnavn = HentValgfritt("titleShort") is { } kn ? HtmlEntity.DeEntitize(kn) : null;
        var departement = HtmlEntity.DeEntitize(HentFelt("ministry"));

        var ikrafttredelse = FørsteDato(HentValgfritt("dateInForce"));
        var konsolidertDato = FørsteDato(HentValgfritt("lastChangeInForce"));

        var (frbrAuthorHref, frbrAuthorShowAs) = kildetype == Kildetype.Lov
            ? ("stortinget", "Stortinget")
            : (Slugifiser(departement), departement);

        return new RettskildeMetadata
        {
            Kildetype = kildetype,
            Tittel = tittel,
            Kortnavn = kortnavn,
            Eli = eli,
            Datokode = datokode,
            Ikrafttredelse = ikrafttredelse,
            KonsolidertDato = konsolidertDato,
            AnsvarligDepartement = departement,
            FrbrAuthorHref = frbrAuthorHref,
            FrbrAuthorShowAs = frbrAuthorShowAs,
        };
    }

    private static DateOnly? FørsteDato(string? rått)
    {
        if (string.IsNullOrWhiteSpace(rått)) return null;
        var m = DatoMønster().Match(rått);
        return m.Success ? DateOnly.ParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex DatoMønster();

    private static string Slugifiser(string tekst)
    {
        var lower = tekst.Trim().ToLowerInvariant()
            .Replace("æ", "ae").Replace("ø", "o").Replace("å", "a");
        var slugget = IkkeSlugTegn().Replace(lower, "-");
        return slugget.Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex IkkeSlugTegn();

    /// <summary>
    /// Lovdatas kapittel-/underinndelingsoverskrifter er én tekstnode med nummeret innbakt
    /// (f.eks. "Kapittel 1. Alminnelige bestemmelser.", "I. Alminnelige bestemmelser") — AKN-eksempelet
    /// i §1.3 skiller &lt;num&gt; fra &lt;heading&gt;, så prefikset (allerede kjent fra data-name) fjernes her.
    /// </summary>
    private static string FjernNummerPrefiks(string heleOverskriften, string forventetPrefiks)
    {
        if (!heleOverskriften.StartsWith(forventetPrefiks, StringComparison.Ordinal))
        {
            throw new FormatException(
                $"Overskrift '{heleOverskriften}' starter ikke med forventet prefiks '{forventetPrefiks}'. " +
                "Ingen gjettet fallback (§3.3).");
        }
        return heleOverskriften[forventetPrefiks.Length..].TrimStart();
    }

    // ---------- Kapittel / underinndeling (steg 5) ----------

    private static void ParseKapittel(
        HtmlNode section, ReferanseKontekst kontekst, List<RettskildeNode> noder,
        List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var dataName = section.Attributes["data-name"]?.Value
            ?? throw new FormatException("Kapittel-<section> mangler data-name.");
        var kapittelNummer = dataName.StartsWith("kap", StringComparison.Ordinal) ? dataName[3..] : dataName;
        var eid = LovdataIdentifikatorer.KapittelEid(kapittelNummer);
        var kildeId = section.Attributes["id"]?.Value
            ?? throw new FormatException($"Kapittel {eid} mangler id-attributt.");
        var heleOverskriften = HtmlEntity.DeEntitize(section.SelectSingleNode("./h2|./h3|./h4")?.InnerText.Trim() ?? "");
        var overskrift = FjernNummerPrefiks(heleOverskriften, $"Kapittel {kapittelNummer}.");

        noder.Add(new RettskildeNode
        {
            Eid = eid,
            KildeId = kildeId,
            NodeType = NodeType.Kapittel,
            Nummer = kapittelNummer,
            Overskrift = overskrift,
            SorteringsRekkefolge = sortering.Neste(),
        });

        ParseKapittelInnhold(section, eid, kontekst, noder, referanser, sortering);
    }

    private static void ParseUnderinndeling(
        HtmlNode section, string parentKapittelEid, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var dataName = section.Attributes["data-name"]?.Value
            ?? throw new FormatException("Underinndelings-<section> mangler data-name.");
        var romertall = dataName.StartsWith("kap", StringComparison.Ordinal) ? dataName[3..] : dataName;
        var eid = LovdataIdentifikatorer.UnderinndelingEid(parentKapittelEid, romertall);
        var kildeId = section.Attributes["id"]?.Value
            ?? throw new FormatException($"Underinndeling {eid} mangler id-attributt.");
        var heleOverskriften = HtmlEntity.DeEntitize(section.SelectSingleNode("./h2|./h3|./h4")?.InnerText.Trim() ?? "");
        var overskrift = FjernNummerPrefiks(heleOverskriften, $"{romertall}.");

        noder.Add(new RettskildeNode
        {
            Eid = eid,
            ParentEid = parentKapittelEid,
            KildeId = kildeId,
            NodeType = NodeType.Underinndeling,
            Nummer = romertall,
            Overskrift = overskrift,
            SorteringsRekkefolge = sortering.Neste(),
        });

        ParseKapittelInnhold(section, eid, kontekst, noder, referanser, sortering);
    }

    /// <summary>Felles for kapittel og underinndeling: barn er enten nestet &lt;section&gt; (romertall) eller &lt;article class="legalArticle"&gt; (paragraf).</summary>
    private static void ParseKapittelInnhold(
        HtmlNode container, string containerEid, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        foreach (var child in container.ChildNodes)
        {
            if (child.NodeType != HtmlNodeType.Element) continue;
            var klasse = child.GetAttributeValue("class", "");
            if (child.Name == "section" && klasse.Contains("section"))
            {
                ParseUnderinndeling(child, containerEid, kontekst, noder, referanser, sortering);
            }
            else if (child.Name == "article" && klasse.Contains("legalArticle"))
            {
                ParseParagraf(child, containerEid, kontekst, noder, referanser, sortering);
            }
            else if (child.Name is "h2" or "h3" or "h4")
            {
                // overskrift, allerede lest i ParseKapittel/ParseUnderinndeling
            }
            else if (child.Name == "article" && klasse.Contains("changesToParent"))
            {
                // endringshistorikk for hele kapittelet/underinndelingen -> proveniens, ikke tekstinnhold
                // (samme begrunnelse som i ParseParagraf; utenfor scope uten proveniens-lager i denne byggeøkten).
            }
            else
            {
                throw new NotSupportedException(
                    $"Uventet element under kapittel/underinndeling {containerEid}: <{child.Name} class=\"{klasse}\">. " +
                    "Ingen gjettet fallback (§3.3).");
            }
        }
    }

    // ---------- Paragraf / ledd / punkt (steg 5) ----------

    private static void ParseParagraf(
        HtmlNode article, string parentEid, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var lovdataUrl = article.Attributes["data-lovdata-URL"]?.Value
            ?? throw new FormatException("Paragraf mangler data-lovdata-URL.");
        var paragrafnummer = lovdataUrl[(lovdataUrl.LastIndexOf('/') + 1)..];
        var eid = LovdataIdentifikatorer.ParagrafEid(kontekst.EgenLovEli, paragrafnummer);
        var kildeId = article.Attributes["id"]?.Value
            ?? throw new FormatException($"Paragraf {eid} mangler id-attributt.");

        var nummerVisning = HtmlEntity.DeEntitize(
            article.SelectSingleNode(".//span[contains(@class,'legalArticleValue')]")?.InnerText.Trim() ?? paragrafnummer);
        var overskrift = HtmlEntity.DeEntitize(
            article.SelectSingleNode(".//span[contains(@class,'legalArticleTitle')]")?.InnerText.Trim() ?? "");

        var opphevetDatoRaa = article.Attributes["data-repealeddate"]?.Value;
        var opphevet = opphevetDatoRaa is not null;
        var opphevetDato = opphevetDatoRaa is not null
            ? DateOnly.ParseExact(opphevetDatoRaa, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : (DateOnly?)null;

        var fotnoter = new List<Fotnote>();

        noder.Add(new RettskildeNode
        {
            Eid = eid,
            ParentEid = parentEid,
            KildeId = kildeId,
            NodeType = NodeType.Paragraf,
            Nummer = nummerVisning,
            Overskrift = overskrift,
            Opphevet = opphevet,
            OpphevetDato = opphevetDato,
            Fotnoter = fotnoter,
            SorteringsRekkefolge = sortering.Neste(),
        });

        var leddIndeks = 0;
        foreach (var child in article.ChildNodes)
        {
            if (child.NodeType != HtmlNodeType.Element) continue;
            var klasse = child.GetAttributeValue("class", "");
            if (child.Name is "h3" or "h4" && klasse.Contains("legalArticleHeader"))
            {
                // overskrift, allerede lest
            }
            else if (child.Name == "article" && klasse.Contains("legalP"))
            {
                leddIndeks++;
                ParseLedd(child, eid, leddIndeks, kontekst, noder, referanser, sortering);
            }
            else if (child.Name == "article" && klasse.Contains("changesToParent"))
            {
                // endringshistorikk -> proveniens, ikke tekstinnhold (§3.1 steg 5). Utenfor scope
                // for en pipeline uten proveniens-lager i denne byggeøkten.
            }
            else if (child.Name == "footer" && klasse.Contains("footnotes"))
            {
                fotnoter.AddRange(ParseFotnoter(child));
            }
            else
            {
                throw new NotSupportedException(
                    $"Uventet element under paragraf {eid}: <{child.Name} class=\"{klasse}\">. Ingen gjettet fallback (§3.3).");
            }
        }
    }

    private static IEnumerable<Fotnote> ParseFotnoter(HtmlNode footer)
    {
        foreach (var fn in footer.SelectNodes("./article[contains(@class,'footnote')]") ?? Enumerable.Empty<HtmlNode>())
        {
            var etikett = HtmlEntity.DeEntitize(fn.SelectSingleNode(".//span[contains(@class,'footnoteLabel')]")?.InnerText.Trim()
                ?? fn.GetAttributeValue("data-name", ""));
            // Fotnotetekst kan inneholde lenker (f.eks. til EØS-avtalen) som ikke matcher lov/forskrift-mønsteret;
            // de faller da tilbake til synlig tekst (§ HentSegmenter, tolket==null-grenen). Ingen kryssreferanse-
            // sporing for fotnoter i denne byggeøkten (utenfor §3.1 steg 6s scope, se README/kommentar der).
            var segmenter = HentSegmenter(fn, kontekst: null);
            var tekst = string.Concat(segmenter.Select(s => s.Tekst));
            yield return new Fotnote(etikett, tekst.Trim());
        }
    }

    private static void ParseLedd(
        HtmlNode legalP, string paragrafEid, int leddIndeks, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var eid = LovdataIdentifikatorer.LeddEid(paragrafEid, leddIndeks);
        LeggTilLeddEllerPunktNode(legalP, eid, parentEid: paragrafEid, NodeType.Ledd, kontekst, noder, referanser, sortering);
        ParseChildPunkter([legalP], eid, kontekst, noder, referanser, sortering);
    }

    /// <summary>
    /// Punkt-lister kan nøstes vilkårlig dypt (bekreftet i ekte data — alkoholforskriften § 6-2 har
    /// punkt-i-punkt for gebyrsatser). Både &lt;ul&gt; og &lt;ol&gt; forekommer med identisk struktur
    /// (samme "defaultList"-klasse), kun ulik nummereringsstil — behandles likt. <paramref name="containere"/>
    /// er gjerne flere enn ett element: et punkt kan selv ha flere direkte legalP-"ledd" (§14-3 punkt 14
    /// i alkoholforskriften: tekst+underliste, så en oppfølgende setning) — nummereringen av punktbarn
    /// løper da fortløpende på tvers av alle disse, i dokumentrekkefølge.
    /// </summary>
    private static void ParseChildPunkter(
        IEnumerable<HtmlNode> containere, string parentEid, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var punktIndeks = 0;
        foreach (var container in containere)
        {
            var lister = (container.SelectNodes("./ul") ?? Enumerable.Empty<HtmlNode>())
                .Concat(container.SelectNodes("./ol") ?? Enumerable.Empty<HtmlNode>());
            foreach (var liste in lister)
            {
                foreach (var li in liste.SelectNodes("./li") ?? Enumerable.Empty<HtmlNode>())
                {
                    var listArticle = li.SelectSingleNode("./article[contains(@class,'listArticle')]")
                        ?? throw new FormatException($"<li> under {parentEid} mangler <article class=\"listArticle\">.");
                    punktIndeks++;
                    ParsePunkt(listArticle, parentEid, punktIndeks, kontekst, noder, referanser, sortering);
                }
            }
        }
    }

    private static void ParsePunkt(
        HtmlNode listArticle, string parentEid, int punktIndeks, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var eid = LovdataIdentifikatorer.PunktEid(parentEid, punktIndeks);
        var kildeId = listArticle.Attributes["id"]?.Value
            ?? throw new FormatException($"Punkt {eid} mangler id-attributt.");

        var direkteLegalP = (listArticle.SelectNodes("./article[contains(@class,'legalP')]") ?? Enumerable.Empty<HtmlNode>()).ToList();
        if (direkteLegalP.Count == 0)
        {
            throw new FormatException($"Punkt {eid} har ingen nestet <article class=\"legalP\"> — uventet struktur, ingen gjettet fallback (§3.3).");
        }

        // Bladtekst = alle direkte legalP-barns egen tekst, konkatenert i dokumentrekkefølge
        // (vanligvis nøyaktig ett; §14-3 punkt 14 i alkoholforskriften har to — tekst+underliste,
        // så en oppfølgende setning). Schemaets 'tekst'-felt er definert som bladtekst for punkt-noder
        // (§2 i teknisk design) — det introduseres ikke et eget "ledd under punkt"-nivå for dette.
        var alleSegmenter = new List<TekstSegment>();
        foreach (var legalP in direkteLegalP)
        {
            // Mellomrom mellom flere direkte legalP-"ledd" i samme punkt (§14-3 punkt 14 i
            // alkoholforskriften) — samme begrunnelse som mellomrommet ved en hoppet-over liste over.
            if (alleSegmenter.Count > 0) alleSegmenter.Add(new TekstSegment(" ", null, false));
            alleSegmenter.AddRange(HentSegmenter(legalP, kontekst));
        }
        var plainTekst = KollapsDobleMellomrom(string.Concat(alleSegmenter.Select(s => s.Tekst)));
        var hash = LovdataIdentifikatorer.BeregnTekstHash(plainTekst);

        noder.Add(new RettskildeNode
        {
            Eid = eid,
            ParentEid = parentEid,
            KildeId = kildeId,
            NodeType = NodeType.Punkt,
            Tekst = plainTekst.Trim(),
            TekstHash = hash,
            Segmenter = alleSegmenter,
            SorteringsRekkefolge = sortering.Neste(),
        });
        LeggTilReferanser(referanser, eid, alleSegmenter);

        ParseChildPunkter(direkteLegalP, eid, kontekst, noder, referanser, sortering);
    }

    /// <summary>
    /// Felles for ledd og punkt: begge er "bladtekst-bærende" noder hvis egen Tekst/TekstHash kun
    /// dekker deres EGEN inline-tekst (HentSegmenter stopper ved nestet &lt;ul&gt;/&lt;ol&gt;) —
    /// undernoder (punkt/underpunkt) sin tekst telles ikke med, samme prinsipp som kapittel ikke
    /// inkluderer sine paragrafers tekst.
    /// </summary>
    private static void LeggTilLeddEllerPunktNode(
        HtmlNode legalP, string eid, string parentEid, NodeType nodeType, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var kildeId = legalP.Attributes["id"]?.Value
            ?? throw new FormatException($"{nodeType} {eid} mangler id-attributt.");
        var segmenter = HentSegmenter(legalP, kontekst);
        var plainTekst = KollapsDobleMellomrom(string.Concat(segmenter.Select(s => s.Tekst)));
        var hash = LovdataIdentifikatorer.BeregnTekstHash(plainTekst);

        noder.Add(new RettskildeNode
        {
            Eid = eid,
            ParentEid = parentEid,
            KildeId = kildeId,
            NodeType = nodeType,
            Tekst = plainTekst.Trim(),
            TekstHash = hash,
            Segmenter = segmenter,
            SorteringsRekkefolge = sortering.Neste(),
        });

        LeggTilReferanser(referanser, eid, segmenter);
    }

    private static void LeggTilReferanser(List<RettskildeReferanse> referanser, string fraEid, IReadOnlyList<TekstSegment> segmenter)
    {
        foreach (var s in segmenter.Where(s => s.ReferanseTilEid is not null))
        {
            referanser.Add(new RettskildeReferanse(fraEid, s.ReferanseTilEid!, s.ErInternReferanse, null, null));
        }
    }

    /// <summary>
    /// Rydder opp doble mellomrom som kan oppstå der HentSegmenter setter inn et skille-mellomrom ved en
    /// hoppet-over liste eller mellom flere direkte legalP-blokker (se kommentarer der) og kildeteksten
    /// allerede hadde whitespace på samme sted. Kun kosmetisk for visningsfeltet Tekst — tekst_hash (§3.4)
    /// har uansett sin egen fullstendige whitespace-normalisering og påvirkes ikke av dette.
    /// </summary>
    private static string KollapsDobleMellomrom(string tekst) => DobbeltMellomromMønster().Replace(tekst, " ");

    [GeneratedRegex(" {2,}")]
    private static partial Regex DobbeltMellomromMønster();

    // ---------- Inline tekst-/referanse-ekstraksjon ----------

    /// <summary>Kjente inline-elementer som bare gir videre ekstraksjon, ingen egen semantikk her.</summary>
    private static readonly HashSet<string> GjennomsiktigeInlineElementer = new(StringComparer.Ordinal)
        { "i", "b", "span", "sub", "em", "strong", "p" };

    private static List<TekstSegment> HentSegmenter(HtmlNode node, ReferanseKontekst? kontekst)
    {
        var segmenter = new List<TekstSegment>();
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                var tekst = HtmlEntity.DeEntitize(child.InnerText);
                if (tekst.Length > 0) segmenter.Add(new TekstSegment(tekst, null, false));
                continue;
            }

            if (child.NodeType != HtmlNodeType.Element) continue;
            var klasse = child.GetAttributeValue("class", "");

            if (child.Name == "a" && child.Attributes["href"]?.Value is string href)
            {
                segmenter.Add(TolkLenke(child, href, kontekst));
            }
            else if (child.Name == "sup" && klasse.Contains("footnotereference"))
            {
                // ekskludert fra hovedteksten (§3.2) — fotnoter er egne AKN <authorialNote>
            }
            else if (child.Name == "span" && klasse.Contains("footnoteLabel"))
            {
                // etiketten hentes separat til Fotnote.Etikett (ParseFotnoter) — skal ikke dupliseres i Tekst
            }
            else if (child.Name is "ul" or "ol")
            {
                // Selve listen håndteres separat av kalleren (punkt-utbrytning), men et mellomrom
                // settes inn her slik at tekst før og etter listen ikke smelter sammen uten skille
                // (f.eks. "herunder" + "Det skal …" → "herunderDet skal …" uten dette) — bekreftet reelt
                // problem i alkoholforskriften § 7-2 (<p class="leddfortsettelse"> rett etter </ul>).
                // Endelig Tekst trimmes og tekst_hash kollapser whitespace (§3.4), så et ekstra mellomrom
                // her er alltid trygt selv om det skulle bli overflødig i noen posisjoner.
                segmenter.Add(new TekstSegment(" ", null, false));
            }
            else if (child.Name == "footer")
            {
                // håndteres separat av kalleren (fotnoter) — footer er i praksis alltid søsken av
                // legalP under paragrafen, ikke et barn av selve legalP-en HentSegmenter kalles på
            }
            else if (child.Name == "article" && klasse.Contains("changesToParent"))
            {
                // endringshistorikk, ikke løpetekst
            }
            else if (GjennomsiktigeInlineElementer.Contains(child.Name))
            {
                segmenter.AddRange(HentSegmenter(child, kontekst));
            }
            else
            {
                throw new NotSupportedException(
                    $"Ukjent inline-element <{child.Name} class=\"{klasse}\"> i løpetekst. Ingen gjettet fallback (§3.3).");
            }
        }
        return segmenter;
    }

    private static TekstSegment TolkLenke(HtmlNode a, string href, ReferanseKontekst? kontekst)
    {
        var visning = HtmlEntity.DeEntitize(a.InnerText);
        var tolket = LovdataHrefTolker.TolkLøpetekstHref(href);
        if (tolket is null || kontekst is null)
        {
            // Enten et lenkemønster utenfor lov/forskrift-løpetekstreferanser (§3.1 steg 6,
            // Vedlegg A.7 — f.eks. EØS-avtalen inni en fotnote), eller kalleren har bevisst ingen
            // referansekontekst (fotnoter, se ParseFotnoter). Behandles som ren synlig tekst.
            return new TekstSegment(visning, null, false);
        }

        var erIntern = tolket.Datokode == kontekst.EgenDatokode;
        var tilLovEli = erIntern ? kontekst.EgenLovEli : LovdataIdentifikatorer.AvledEliFraDatokode(tolket.Datokode, out _);
        var tilEid = tolket.Paragrafnummer is not null
            ? LovdataIdentifikatorer.ParagrafEid(tilLovEli, tolket.Paragrafnummer)
            : tilLovEli;
        return new TekstSegment(visning, tilEid, erIntern);
    }
}
