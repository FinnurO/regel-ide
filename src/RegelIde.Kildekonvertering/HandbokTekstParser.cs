using System.Text.RegularExpressions;

namespace RegelIde.Kildekonvertering;

/// <summary>
/// Én node i en håndbok/retningslinje, produsert av <see cref="HandbokTekstParser"/>. Speiler
/// <c>RettskildeNodeEntitet</c> felt-for-felt (docs/15-handbok-dokumentgraf-notat.md §0.1/§2) — bevisst
/// IKKE <see cref="RettskildeNode"/> (Lovdata-modellen i Modeller.cs), fordi den har felt uten mening her
/// (KildeId/Kildesystem/Fotnoter/Segmenter er Lovdata-HTML-spesifikke) og mangler NodeType-verdien
/// "avsnitt" som overskriftsfallbacken (§2, Lag 2) krever. <see cref="RettskildeImportTjeneste"/>
/// (uendret denne runden) projiserer denne til <c>RettskildeNodeEntitet</c>-rader ved en fremtidig
/// import-kobling — se rapporten for hva som IKKE er bygget denne runden (intet import-endepunkt).
/// </summary>
public sealed record HandbokNode
{
    /// <summary>
    /// Dokumentets EGEN nummerering, ikke en syntetisk id (§2): <c>"kap4"</c> for et kapittel,
    /// <c>"kap4/pkt4.1"</c> for et punkt. Overskriftsfallbacken (<see cref="NodeType"/> = "avsnitt")
    /// bruker i stedet <c>"h2-3"</c>/<c>"h2-3/h3-1"</c>-stilen fra §2.
    /// </summary>
    public required string Eid { get; init; }

    public string? ParentEid { get; init; }

    /// <summary>"kapittel" | "punkt" | "avsnitt" — fri streng, ikke <c>NodeType</c>-enumen i Modeller.cs
    /// (den er Lovdata-spesifikk og har ingen "avsnitt"-verdi).</summary>
    public required string NodeType { get; init; }

    public string? Nummer { get; init; }
    public string? Overskrift { get; init; }

    /// <summary>Bladtekst — akkumulert løpetekst fram til neste struktur-markør. Null kun for kapittel-
    /// noder som ikke har egen tekst før første punkt (svært få — se f.eks. Kapittel 1/3/4/8 i Bergen,
    /// som ALLE har egen intro-tekst før første punkt, i motsetning til f.eks. Kapittel 6/7/9/10 som
    /// har HELE sin tekst direkte på kapittel-nivå, uten X.Y-punkt-numre i det hele tatt).</summary>
    public string? Tekst { get; init; }

    /// <summary>SHA-256 av normalisert tekst — samme funksjon som Lovdata-pipelinen
    /// (<see cref="LovdataIdentifikatorer.BeregnTekstHash"/>), gjenbrukt uendret (§8 Trinn 1 punkt 2:
    /// hash-basert reimport-versionering i RettskildeImportTjeneste forutsetter nettopp dette).</summary>
    public string? TekstHash { get; init; }

    public required int SorteringsRekkefolge { get; init; }
}

/// <summary>To kanttyper (§3.2) — begge deterministiske, begge kan bæres av EKSISTERENDE
/// <c>RettskildeReferanseEntitet</c> (se kommentaren på <see cref="HandbokTekstParser"/> for
/// begrunnelsen) — ingen ny kanttabell er bygget.</summary>
public enum HandbokReferansetype
{
    /// <summary>HandbokNode → RettskildeNode (ekstern lov/forskrift). Deterministisk UTTREKK av
    /// lovnavn+paragraf er gjort her; GUID-oppslag mot en faktisk importert rettskilde (f.eks.
    /// alkoholloven) krever databasetilgang og er bevisst IKKE gjort i denne rene, DB-frie parseren —
    /// samme arkitektur som Lovdata-pipelinens eksterne referanser (LovdataHtmlParser produserer
    /// <c>RettskildeReferanse.TilEid</c> som en best-effort-streng; <c>FinnEllerOpprettReferanseStubAsync</c>
    /// i RettskildeImportTjeneste gjør selve DB-oppslaget). Denne kanten er derfor IKKE ferdig koblet
    /// til en <c>RettskildeEntitet</c>-rad ennå — se rapporten.</summary>
    HjemletI,

    /// <summary>HandbokNode → HandbokNode, INTERN i samme dokument. Løses fullstendig deterministisk
    /// her, siden dokumentets egen Eid-nummerering allerede er kjent når parseren er ferdig (§2:
    /// "punkt 4.7" løses mot Eid). Uløste referanser (målet finnes ikke i dokumentet) droppes stille —
    /// samme "ingen gjettet fallback"-prinsipp som Lovdata-pipelinen.</summary>
    Kryssrefererer,
}

/// <summary>Én funnet referanse-kant, se <see cref="HandbokReferansetype"/>.</summary>
public sealed record HandbokReferanse(
    string FraNodeEid,
    HandbokReferansetype Type,
    string? TilEid,
    string? EksternLovnavn,
    string? EksternParagraf,
    string Utdrag
);

public sealed record HandbokParseResultat(IReadOnlyList<HandbokNode> Noder, IReadOnlyList<HandbokReferanse> Referanser);

/// <summary>
/// Sideordnet <see cref="LovdataHtmlParser"/> (docs/15-handbok-dokumentgraf-notat.md §2/§8 Trinn 1
/// punkt 1) — men for utvunnet PDF-/nettside-TEKST, ikke Lovdatas HTML. Ren regex/tekstsegmentering,
/// INGEN KI, INGEN HTML-parsing. Segmenterer på dokumentets EGEN nummerering
/// (<c>^Kapittel \d+</c>, <c>^\d+\.\d+</c>, evt. <c>^\d+\.\d+\.\d+</c>), filtrerer sidebrytnings-støy,
/// og faller tilbake til overskriftsbasert segmentering der nummerering mangler helt (Lag 2-fallback).
///
/// Skjemaspørsmålet fra oppgaven — kan <c>RettskildeReferanseEntitet</c> bære <c>hjemlet_i</c> og
/// <c>kryssrefererer</c> (§3.2) uten en ny tabell? — er JA: begge kanttyper er "fra en node, til en
/// rettskilde+eId", nøyaktig formen <c>RettskildeReferanseEntitet</c> allerede har
/// (<c>FraNodeId</c>/<c>TilRettskildeId</c>/<c>TilEid</c>/<c>Opprinnelse</c>). For
/// <c>kryssrefererer</c> er <c>TilRettskildeId</c> ganske enkelt håndbokens EGEN rettskilde-id (intern
/// referanse). For <c>hjemlet_i</c> er <c>TilRettskildeId</c> den eksterne lov/forskriftens
/// rettskilde-id — samme DB-oppslag som Lovdata-pipelinen allerede gjør
/// (<c>FinnEllerOpprettReferanseStubAsync</c>), ikke noe denne parseren selv gjør. Ingen ny tabell er
/// derfor bygget.
/// </summary>
public static partial class HandbokTekstParser
{
    public static HandbokParseResultat Parse(string raaTekst)
    {
        var linjer = FiltrerSidebrytningsstoy(raaTekst);
        var noder = SegmenterPaNummerering(linjer);

        if (noder.Count == 0)
        {
            noder = SegmenterPaOverskrift(linjer);
        }

        var referanser = TrekkUtReferanser(noder);
        return new HandbokParseResultat(noder, referanser);
    }

    // ---------- Steg 1: sidebrytnings-/kolofonfiltrering (§6.1, §2 Lag 2) ----------

    /// <summary>
    /// Eksempel fra notatet: "Dok.nr.: SD-24-113 Side 3 av 5". Fanger også en løsrevet
    /// "Side N av M"-linje uten Dok.nr-prefiks (samme mønster observert i andre kommunale maler).
    /// Linjen forkastes HELT — den skal virke som om den aldri stod der, ikke settes inn som et
    /// avsnittskille (teksten før/etter en sidebrytning i midten av et punkt, f.eks. Bergens 3.2, er
    /// fortsatt SAMME node).
    /// </summary>
    [GeneratedRegex(@"^Dok\.?nr\.?:?\s*\S+.*\bSide\s+\d+\s+av\s+\d+\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SidebrytningMedDoknrMønster();

    [GeneratedRegex(@"^Side\s+\d+\s+av\s+\d+\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SidebrytningAlenestaendeMønster();

    private static List<string> FiltrerSidebrytningsstoy(string raaTekst)
    {
        var resultat = new List<string>();
        foreach (var rad in raaTekst.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmet = rad.Trim();
            if (SidebrytningMedDoknrMønster().IsMatch(trimmet)) continue;
            if (SidebrytningAlenestaendeMønster().IsMatch(trimmet)) continue;
            resultat.Add(rad);
        }
        return resultat;
    }

    // ---------- Steg 2: primær segmentering på dokumentets nummerering (§2 Lag 2) ----------

    /// <summary>"Kapittel 4 - Skjenkebevillinger", "Kapittel 2 – Vilkår", "Kapittel 10 – Bevillingsperioden".
    /// Overskriften er valgfri (gruppe 2) — noen maler har bare "Kapittel 4" uten tittel på samme linje.</summary>
    [GeneratedRegex(@"^Kapittel\s+(\d{1,2})\.?(?:\s*[-–.]\s*(.+))?$")]
    private static partial Regex KapittelMønster();

    /// <summary>
    /// "4.1", "4.10", "9.1 – Kontroll med salgssteder", "1.2." (med trailing punktum). Bevisst
    /// <c>\d{1,2}</c> per segment (ikke <c>\d+</c>) — reduserer risiko for at en dato skrevet
    /// "DD.MM.ÅÅÅÅ" alene på en linje feiltolkes som et 3-nivås punktnummer (årstall har 4 sifre,
    /// forkastes av segment-lengdebegrensningen). Ingen observert i de to testfixturene, men en
    /// dokumentert føre-var-begrensning, ikke en antatt umulighet.
    /// </summary>
    [GeneratedRegex(@"^(\d{1,2}(?:\.\d{1,2}){1,2})\.?(?:\s+(.*))?$")]
    private static partial Regex PunktMønster();

    [GeneratedRegex(@"\s+")]
    private static partial Regex FlereMellomromMønster();

    private static List<HandbokNode> SegmenterPaNummerering(List<string> linjer)
    {
        var noder = new List<HandbokNode>();
        var eidTilNode = new Dictionary<string, HandbokNode>(StringComparer.Ordinal);
        var sortering = 0;

        // Åpen node under bygging: eid/parentEid/nodeType/nummer/overskrift + akkumulerte tekstlinjer.
        string? apenEid = null, apenParentEid = null, apenNodeType = null, apenNummer = null, apenOverskrift = null;
        var apenTekstlinjer = new List<string>();

        void FlushÅpenNode()
        {
            if (apenEid is null) return;
            var tekst = apenTekstlinjer.Count > 0 ? NormaliserTekst(string.Join(" ", apenTekstlinjer)) : null;
            var node = new HandbokNode
            {
                Eid = apenEid,
                ParentEid = apenParentEid,
                NodeType = apenNodeType!,
                Nummer = apenNummer,
                Overskrift = apenOverskrift,
                Tekst = tekst,
                TekstHash = tekst is not null ? LovdataIdentifikatorer.BeregnTekstHash(tekst) : null,
                SorteringsRekkefolge = sortering++,
            };
            noder.Add(node);
            eidTilNode[apenEid] = node;
            apenTekstlinjer = [];
        }

        foreach (var rad in linjer)
        {
            var trimmet = rad.Trim();
            if (trimmet.Length == 0) continue;

            var kapittelTreff = KapittelMønster().Match(trimmet);
            if (kapittelTreff.Success)
            {
                FlushÅpenNode();
                var kapittelNummer = kapittelTreff.Groups[1].Value;
                apenEid = $"kap{kapittelNummer}";
                apenParentEid = null;
                apenNodeType = "kapittel";
                apenNummer = kapittelNummer;
                apenOverskrift = kapittelTreff.Groups[2].Success ? kapittelTreff.Groups[2].Value.Trim() : null;
                continue;
            }

            var punktTreff = PunktMønster().Match(trimmet);
            if (punktTreff.Success)
            {
                FlushÅpenNode();
                var punktNummer = punktTreff.Groups[1].Value;
                apenEid = PunktEid(punktNummer, eidTilNode);
                apenParentEid = ForelderEid(punktNummer, eidTilNode);
                apenNodeType = "punkt";
                apenNummer = punktNummer;
                // Ingen egen overskrift-linje for punkt i kildedokumentet (§ HandbokNode.Overskrift-
                // kommentaren) — resten av linjen (om noe) er starten på bladteksten, ikke en tittel.
                apenOverskrift = null;
                if (punktTreff.Groups[2].Success)
                {
                    var rest = StripLedendeTankestrek(punktTreff.Groups[2].Value);
                    if (rest.Length > 0) apenTekstlinjer.Add(rest);
                }
                continue;
            }

            // Vanlig løpetekstlinje — hører til den ÅPNE noden (kapittel- eller punkt-nivå). Linjer før
            // første struktur-markør (dokumenttittel, "Fastsatt av Bystyret ...", kolofon) har ingen
            // åpen node og forkastes her — se rapporten for at dokumentnivå-metadata (Dok.nr/Vedtaksdato
            // osv.) IKKE trekkes ut av denne parseren denne runden.
            if (apenEid is not null) apenTekstlinjer.Add(trimmet);
        }
        FlushÅpenNode();

        return noder;
    }

    private static string StripLedendeTankestrek(string tekst)
    {
        var t = tekst.TrimStart();
        if (t.StartsWith('-') || t.StartsWith('–')) t = t[1..].TrimStart();
        return t;
    }

    /// <summary>"kap4/pkt4.1" for et 2-segments nummer (§2s eget eksempel). For et 3-segments nummer
    /// ("4.1.2") nøstes det under 2-segments-punktet HVIS det allerede finnes, ellers direkte under
    /// kapittelet — samme "ikke gjettet fallback, men heller ikke krasj" som resten av parseren.</summary>
    private static string PunktEid(string punktNummer, Dictionary<string, HandbokNode> eidTilNode)
    {
        var forelder = ForelderEid(punktNummer, eidTilNode);
        return $"{forelder}/pkt{punktNummer}";
    }

    private static string ForelderEid(string punktNummer, Dictionary<string, HandbokNode> eidTilNode)
    {
        var segmenter = punktNummer.Split('.');
        var kapittelEid = $"kap{segmenter[0]}";
        if (segmenter.Length == 2) return kapittelEid;

        // 3+ segmenter: prøv nærmeste forelder (ett nivå opp), fall tilbake til kapittelet.
        var foreldreNummer = string.Join('.', segmenter[..^1]);
        var foreldreKandidat = $"{kapittelEid}/pkt{foreldreNummer}";
        return eidTilNode.ContainsKey(foreldreKandidat) ? foreldreKandidat : kapittelEid;
    }

    private static string NormaliserTekst(string tekst) => FlereMellomromMønster().Replace(tekst, " ").Trim();

    // ---------- Steg 2b: overskriftsbasert fallback (§2 Lag 2, "der nummerering mangler") ----------

    /// <summary>
    /// Konvensjon (dokumentert, ikke antatt universell): Markdown-stil "## "/"### "-prefiks for
    /// h2/h3, siden løpende prosa uten NOEN maskinlesbar overskrift-markør per definisjon ikke kan
    /// segmenteres deterministisk uten en slik konvensjon å holde seg til (§0-prinsippet — struktur skal
    /// LESES, ikke gjettes — brytes hvis vi prøver å skjønnsmessig gjette overskrifter fra fontstørrelse
    /// e.l., som denne rene tekst-parseren uansett ikke har tilgang til). Har INGEN tekst noen
    /// overskrift-markør i det hele tatt, blir hele dokumentet én "avsnitt"-node (Eid "h2-1").
    /// </summary>
    [GeneratedRegex(@"^(#{2,3})\s+(.+)$")]
    private static partial Regex OverskriftMønster();

    private static List<HandbokNode> SegmenterPaOverskrift(List<string> linjer)
    {
        var noder = new List<HandbokNode>();
        var sortering = 0;
        var h2Teller = 0;
        var h3Teller = 0;

        string? apenEid = null;
        string? apenOverskrift = null;
        var apenTekstlinjer = new List<string>();

        void FlushÅpenNode(string? nyEid)
        {
            if (apenEid is not null)
            {
                var tekst = apenTekstlinjer.Count > 0 ? NormaliserTekst(string.Join(" ", apenTekstlinjer)) : null;
                noder.Add(new HandbokNode
                {
                    Eid = apenEid,
                    ParentEid = apenEid.Contains('/') ? apenEid[..apenEid.LastIndexOf('/')] : null,
                    NodeType = "avsnitt",
                    Overskrift = apenOverskrift,
                    Tekst = tekst,
                    TekstHash = tekst is not null ? LovdataIdentifikatorer.BeregnTekstHash(tekst) : null,
                    SorteringsRekkefolge = sortering++,
                });
            }
            apenEid = nyEid;
            apenOverskrift = null;
            apenTekstlinjer = [];
        }

        foreach (var rad in linjer)
        {
            var trimmet = rad.Trim();
            if (trimmet.Length == 0) continue;

            var treff = OverskriftMønster().Match(trimmet);
            if (treff.Success)
            {
                var nivå = treff.Groups[1].Value.Length; // 2 eller 3
                if (nivå == 2)
                {
                    h2Teller++;
                    h3Teller = 0;
                    FlushÅpenNode($"h2-{h2Teller}");
                }
                else
                {
                    h3Teller++;
                    FlushÅpenNode($"h2-{h2Teller}/h3-{h3Teller}");
                }
                apenOverskrift = treff.Groups[2].Value.Trim();
                continue;
            }

            if (apenEid is null)
            {
                // Ingen overskrift-markør observert ennå (evt. aldri) — én samlenode for hele
                // dokumentet i stedet for å forkaste teksten (§0.1: ikke miste informasjon).
                h2Teller = 1;
                apenEid = "h2-1";
            }
            apenTekstlinjer.Add(trimmet);
        }
        FlushÅpenNode(null);

        return noder;
    }

    // ---------- Steg 3: hjemlet_i / kryssrefererer (§3.2, Trinn 1 punkt 3) ----------

    /// <summary>
    /// "jf. Alkoholloven §1-7 d", "I medhold av alkohollovens § 4-5". <c>loven</c>s valgfrie trailing
    /// "s" (genitiv, "alkohollovens") fanges separat i stedet for i lovnavn-gruppen, slik at
    /// <see cref="HandbokReferanse.EksternLovnavn"/> alltid er den nominative lovformen.
    /// </summary>
    [GeneratedRegex(@"(jf\.|[Ii] medhold av)\s+([A-Za-zÆØÅæøå][A-Za-zÆØÅæøå\-\s]*?loven)s?\s*§\s*(\d+-\d+)\s*([a-z])?\b")]
    private static partial Regex HjemmelMønster();

    /// <summary>"det vises til retningslinjenes punkt 4.7". Løses mot dokumentets EGET eId-register
    /// (§2) — ingen ekstern oppslagstabell.</summary>
    [GeneratedRegex(@"punkt\s+(\d{1,2}(?:\.\d{1,2}){1,2})")]
    private static partial Regex KryssreferanseMønster();

    private static List<HandbokReferanse> TrekkUtReferanser(List<HandbokNode> noder)
    {
        var eidTilNode = noder.ToDictionary(n => n.Eid, n => n, StringComparer.Ordinal);
        var referanser = new List<HandbokReferanse>();

        foreach (var node in noder)
        {
            if (node.Tekst is null) continue;

            foreach (Match m in HjemmelMønster().Matches(node.Tekst))
            {
                referanser.Add(new HandbokReferanse(
                    FraNodeEid: node.Eid,
                    Type: HandbokReferansetype.HjemletI,
                    TilEid: null, // §-oppslag mot ekte rettskilde-GUID er DB-avhengig, se typekommentaren
                    EksternLovnavn: m.Groups[2].Value,
                    EksternParagraf: m.Groups[4].Success ? $"§{m.Groups[3].Value} {m.Groups[4].Value}" : $"§{m.Groups[3].Value}",
                    Utdrag: m.Value));
            }

            foreach (Match m in KryssreferanseMønster().Matches(node.Tekst))
            {
                var punktNummer = m.Groups[1].Value;
                var kandidatEid = PunktEid(punktNummer, eidTilNode);
                if (!eidTilNode.ContainsKey(kandidatEid)) continue; // uløst — ingen gjettet fallback, dropp stille

                referanser.Add(new HandbokReferanse(
                    FraNodeEid: node.Eid,
                    Type: HandbokReferansetype.Kryssrefererer,
                    TilEid: kandidatEid,
                    EksternLovnavn: null,
                    EksternParagraf: null,
                    Utdrag: m.Value));
            }
        }

        return referanser;
    }
}
