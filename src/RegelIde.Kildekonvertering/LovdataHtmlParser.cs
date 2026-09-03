using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RegelIde.Kildekonvertering;

public sealed record ParseResultat(
    RettskildeMetadata Metadata, IReadOnlyList<RettskildeNode> Noder, IReadOnlyList<RettskildeReferanse> Referanser,
    IReadOnlyList<RettskildeHjemmel> Hjemler, IReadOnlyList<RettskildeEndring> Endringer);

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
        var hjemler = HentHjemler(header);
        var endringer = HentEndringer(header);
        var kontekst = new ReferanseKontekst(metadata.Datokode, metadata.Eli);

        var body = doc.DocumentNode.SelectSingleNode("//main[contains(@class,'documentBody')]")
            ?? throw new FormatException("Fant ikke <main class=\"documentBody\"> — ikke et gjenkjennelig Lovdata-dokument.");

        var noder = new List<RettskildeNode>();
        var referanser = new List<RettskildeReferanse>();
        var sortering = new SorteringsTeller();

        var leddIndeksUtenKapittelEllerParagraf = 0;
        var punktIndeksUtenKapittelEllerParagraf = 0;

        // Lokal funksjon av samme grunn som HåndterBarn i ParseKapittelInnhold: div.indent-transparens
        // må dele leddIndeks/punktIndeks ved MUTASJON med hovedløkken, ellers risikeres eId-kollisjon.
        void HåndterDokumentBarn(HtmlNode child)
        {
            if (child.NodeType != HtmlNodeType.Element) return;
            var klasse = child.GetAttributeValue("class", "");
            if (ErIkkeGjeldendeInnhold(klasse))
            {
                // MÅ sjekkes FØR "section"-substrengsjekken under (samme "futuresection" inneholder
                // "section"-fellen som i ParseKapittelInnhold). Bulk-datasettet er konsolidert/
                // gjeldende tekst, så dette representeres bevisst ikke som rettskildeinnhold.
            }
            else if (child.Name == "section" && klasse.Contains("section"))
            {
                ParseKapittel(child, kontekst, noder, referanser, sortering);
            }
            else if (child.Name == "h1")
            {
                // dokumenttittel, ikke en node
            }
            else if (child.Name == "span" && klasse.Contains("errorMessage"))
            {
                // Bekreftet ekte: Lovdata rapporterer SELV at dokumentet ikke kan vises fullstendig
                // ("Vi klarer dessverre ikke vise hele dokumentet.") — hele documentBody er da bare
                // denne feilmeldingen, ingen reelt rettskildeinnhold å parse. Kastes med en EGEN, tydelig
                // merket feiltype (ikke NotSupportedException) slik at dette skiller seg fra "parseren
                // mangler et case" i importstatus-feilmeldingen — problemet ligger hos kilden, ikke her.
                throw new FormatException(
                    "Lovdata rapporterer selv at dokumentet ikke kan vises fullstendig " +
                    $"(\"{HtmlEntity.DeEntitize(child.InnerText.Trim())}\") — ingen reelt innhold å parse. Ikke en parser-mangel.");
            }
            else if (child.Name == "article" && ErAvsnittKlasse(klasse))
            {
                // Dokumentnivå-merknad (bekreftet i ekte data — forvaltningsloven har en varsel om at
                // hele loven oppheves fra en fremtidig dato). Samme behandling som changesToParent:
                // endringshistorikk/metainformasjon, ikke selve rettskildeteksten (§3.1 steg 5).
            }
            else if (child.Name == "article" && klasse.Contains("legalArticle"))
            {
                // Kapittelfri lov/forskrift (bekreftet ekte, funnet under full Lovdata-synkronisering
                // 2026-08-20, docs/13-backlog.md §6 — f.eks. LOV-1977-06-10-82): paragrafene ligger
                // direkte under documentBody, ikke omsluttet av et <section class="section">-kapittel.
                // Samme behandling som en paragraf inni et kapittel (ParseKapittelInnhold), bare uten
                // noe foreldrenivå — parentEid=null her er nøyaktig samme mønster som et Kapittel-nivå
                // selv (heller ikke det setter ParentEid).
                ParseParagraf(child, parentEid: null, kontekst, noder, referanser, sortering);
            }
            else if (child.Name == "article" && (ErLeddKlasse(klasse) || klasse.Contains("marginIdArticle")))
            {
                // Kapittel- OG paragraf-fri lov/forskrift (bekreftet ekte, flere korte eldre lover og
                // et par moderne endringslover/-forskrifter — full korpusgjennomgang 2026-08-21):
                // documentBody består av rene ledd (evt. marginIdArticle-punkt, samme begrunnelse som i
                // ParseKapittelInnhold) direkte, uten NOEN omsluttende struktur overhodet. Bruker
                // dokumentets eget ELI (kontekst.EgenLovEli) som eId-BASE — for global unikhet — men
                // ParentEid=null, siden ELI'en IKKE er noen ekte nodes eId i databasen (en FK-constraint
                // på rettskilde_noder.parent_node_id ville da feile ved lagring, se ParseLedds
                // doc-kommentar).
                if (klasse.Contains("marginIdArticle"))
                {
                    punktIndeksUtenKapittelEllerParagraf++;
                    ParsePunkt(child, kontekst.EgenLovEli, punktIndeksUtenKapittelEllerParagraf, kontekst, noder, referanser, sortering);
                }
                else
                {
                    leddIndeksUtenKapittelEllerParagraf++;
                    ParseLedd(child, eidBase: kontekst.EgenLovEli, parentEid: null, leddIndeksUtenKapittelEllerParagraf, kontekst, noder, referanser, sortering);
                }
            }
            else if (child.Name is "ul" or "ol" && klasse.Contains("defaultList"))
            {
                // Samme paragraf-/kapittelfrie mønster som over, bare for en punktliste i stedet for et
                // enkelt ledd — bekreftet ekte og OVERRASKENDE VANLIG i forskrift-korpuset (198 av 5123
                // sentrale forskrifter, full korpusgjennomgang 2026-08-21) — typisk en kort forskrift som
                // BARE er en punktvis liste (f.eks. en delegeringsforskrift), uten noe kapittel/paragraf
                // rundt i det hele tatt.
                ParseEnListe(child, kontekst.EgenLovEli, ref punktIndeksUtenKapittelEllerParagraf, kontekst, noder, referanser, sortering);
            }
            else if ((child.Name == "div" && klasse.Contains("indent")) || child.Name == "blockquote")
            {
                // Samme transparente innrykk-håndtering som i ParseKapittelInnhold — bekreftet ekte også
                // direkte i documentBody (siterte/innlemmede tekster i en kapittelfri forskrift).
                foreach (var grandchild in child.ChildNodes) HåndterDokumentBarn(grandchild);
            }
            else if (child.Name == "footer" && klasse.Contains("footnotes"))
            {
                // Fotnoter uten noe kapittel/paragraf å feste dem til (RettskildeNode.Fotnoter finnes
                // kun på Paragraf-noder) — bekreftet ekte, samme "metainformasjon, ikke tekstinnhold"-
                // begrunnelse som den allerede håndterte varianten i ParseKapittelInnhold.
            }
            else if (child.Name == "article" && klasse.Contains("changesToParent"))
            {
                // endringshistorikk på DOKUMENTNIVÅ — bekreftet ekte (samme rolle/begrunnelse som den
                // allerede håndterte varianten inni et kapittel/en paragraf, se ParseKapittelInnhold/
                // ParseParagraf).
            }
            else
            {
                throw new NotSupportedException(
                    $"Uventet element direkte i documentBody: <{child.Name} class=\"{klasse}\">. " +
                    "Ingen gjettet fallback produseres (§3.3) — parseren må utvides bevisst.");
            }
        }

        foreach (var child in body.ChildNodes) HåndterDokumentBarn(child);

        return new ParseResultat(metadata, noder, referanser, hjemler, endringer);
    }

    private sealed class SorteringsTeller
    {
        private int _neste;
        public int Neste() => _neste++;
    }

    /// <summary>
    /// Enkelte eldre forskrifter siterer et helt underdokument (f.eks. et skjema/en kunngjøring) med SIN
    /// EGEN, uavhengige nummerering — bekreftet ekte, "Kapittel I."/"§ 1" gjenbrukt en gang til senere i
    /// SAMME dokument (FOR-1972-08-25-3 m.fl., full korpus-resynkronisering 2026-08-21 av forskrifter —
    /// et helt annet korpus enn lover, ikke dekket av den første audit-runden). Et duplikat-eId ville
    /// krasjet senere (Dictionary-bygging i RettskildeImportTjeneste.SettInnNoderOgReferanserAsync) —
    /// løses her, I PARSEREN, ved å gjøre eId'en unik med et løpenummer-suffiks FØR den når databaselaget,
    /// i stedet for å la det krasje eller risikere å miste/overskrive den første noden. Ren no-op (samme
    /// eId returneres uendret) for alle dokumenter UTEN kollisjon — den store majoriteten.
    /// </summary>
    private static string GjørEidUnik(string kandidatEid, List<RettskildeNode> noder)
    {
        if (!noder.Exists(n => n.Eid == kandidatEid)) return kandidatEid;
        var forsok = 2;
        while (noder.Exists(n => n.Eid == $"{kandidatEid}-duplikat-{forsok}")) forsok++;
        return $"{kandidatEid}-duplikat-{forsok}";
    }

    // ---------- Metadata (steg 4) ----------

    private static RettskildeMetadata ParseMetadata(HtmlNode header)
    {
        // Eksakt klassematch, ikke contains(): Lovdatas "title" og "titleShort" er begge egne
        // dd-klasser der den ene er en delstreng av den andre — contains() ville plukket feil felt.
        //
        // BEGGE varianter (HentFelt/HentValgfritt) henter nå den SAMMENSATTE teksten via
        // HentSammensattTekst i stedet for rått .InnerText.Trim() (issue #152/#127, 2026-09-03) —
        // se den metodens doc-kommentar for hvorfor et rått InnerText-kall er en bekreftet
        // datakorrupsjonsbug for ethvert felt Lovdata strukturerer som en liste eller med
        // <br/>-linjeskift internt (103 av 5899 rettskilder hadde flere departementsnavn limt sammen
        // uten skilletegn, f.eks. "Landbruks- og matdepartementetNærings- og fiskeridepartementet").
        string HentFelt(string cssClass) =>
            header.SelectSingleNode($".//dd[@class='{cssClass}']") is { } dd
                ? HentSammensattTekst(dd)
                : throw new FormatException($"Påkrevd metadatafelt '{cssClass}' mangler i header. Ingen gjettet fallback (§3.3).");

        string? HentValgfritt(string cssClass) =>
            header.SelectSingleNode($".//dd[@class='{cssClass}']") is { } dd ? HentSammensattTekst(dd) : null;

        var datokode = HtmlEntity.DeEntitize(HentFelt("legacyID"));
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out var kildetype);
        var tittel = HtmlEntity.DeEntitize(HentFelt("title"));
        var kortnavn = HentValgfritt("titleShort");
        var departement = HentFelt("ministry");

        // Rå (utrunkert) verdi bevart ved siden av den trunkerte DateOnly? (§ IkrafttredelseRaa/
        // KonsolidertDatoRaa i Modeller.cs) — FørsteDato beholder kun FØRSTE dato-treff, som stille
        // trunkerer kompound-verdier som "01.06.2026, Kongen bestemmer". "UENDRET" (Del A punkt 1):
        // ingen ekstra normalisering/entity-decoding utover det HentValgfritt allerede gjør.
        var ikrafttredelseRaa = HentValgfritt("dateInForce");
        var ikrafttredelse = FørsteDato(ikrafttredelseRaa);
        var konsolidertDatoRaa = HentValgfritt("lastChangeInForce");
        var konsolidertDato = FørsteDato(konsolidertDatoRaa);

        // Nytt felt (2026-09-02) — "Sist endret ved", ikke fanget før nå. Rå tekst (typisk en lenkes
        // synlige tekst, f.eks. "lov/2024-06-21-46") — se SistEndretVed sin doc-kommentar.
        var sistEndretVed = HentValgfritt("lastChangedBy");

        // [Ny, 2026-09-03, issue #127] De resterende 10 av 15 bekreftede header-metadatafelt — se
        // RettskildeMetadata sin doc-kommentar for hva hver enkelt betyr/hvor de er bekreftet ekte
        // (full gjennomgang av data/kilder/raw-lovdata/ + tre live-hentede dokumenter 2026-09-03).
        var kunngjort = HentValgfritt("dateOfPublication");
        var rettsomrade = HentValgfritt("legalArea");
        var euEosHenvisning = HentValgfritt("eeaReferences");
        var dokumentId = HentValgfritt("dokid");
        var refId = HentValgfritt("refid");
        var gjelderFor = HentValgfritt("appliesTo");
        var etat = HentValgfritt("subunit");
        var publisertI = HentValgfritt("publishedIn");
        var annetOmDokumentet = HentValgfritt("miscInformation");
        var sisteRettelse = HentValgfritt("lastupdated");

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
            IkrafttredelseRaa = ikrafttredelseRaa,
            KonsolidertDato = konsolidertDato,
            KonsolidertDatoRaa = konsolidertDatoRaa,
            SistEndretVed = sistEndretVed,
            AnsvarligDepartement = departement,
            FrbrAuthorHref = frbrAuthorHref,
            FrbrAuthorShowAs = frbrAuthorShowAs,
            Kunngjort = kunngjort,
            Rettsomrade = rettsomrade,
            EuEosHenvisning = euEosHenvisning,
            DokumentId = dokumentId,
            RefId = refId,
            GjelderFor = gjelderFor,
            Etat = etat,
            PublisertI = publisertI,
            AnnetOmDokumentet = annetOmDokumentet,
            SisteRettelse = sisteRettelse,
        };
    }

    /// <summary>
    /// Henter teksten til et header-metadatafelts <c>&lt;dd&gt;</c>-node, korrekt for de TO bekreftede
    /// ekte "flere verdier i ett felt"-strukturene Lovdata bruker (§152, full HTML-gjennomgang
    /// 2026-09-03 av et bekreftet berørt dokument, cbe34f67-a029-4bb3-861e-c825e596a585) — i stedet for
    /// et rått <c>.InnerText.Trim()</c>-kall, som stille limer sammen flere verdier UTEN skilletegn
    /// (bekreftet ekte datakorrupsjon: 103 av 5899 rettskilder fikk
    /// "Landbruks- og matdepartementetNærings- og fiskeridepartementet" i stedet for to atskilte navn).
    /// <para>
    /// 1) <c>&lt;dd&gt;&lt;ul&gt;&lt;li&gt;verdi1&lt;/li&gt;&lt;li&gt;verdi2&lt;/li&gt;&lt;/ul&gt;&lt;/dd&gt;</c>
    /// — bekreftet ekte for "ministry"/"subunit"/"legalArea" (ALLTID en liste, selv med kun étt
    /// element — se fixture-korpuset i data/kilder/raw-lovdata/, ingen av de 8 har en bar streng her).
    /// Hvert &lt;li&gt; sin egen InnerText hentes og skilletegnes med ", ".
    /// </para>
    /// <para>
    /// 2) Et enkelt &lt;dd&gt; med &lt;br/&gt;-elementer som logiske linjeskift internt — bekreftet ekte
    /// for "miscInformation"/"eeaReferences" (f.eks. "…(i kraft 7 april 2020).&lt;br/&gt;&lt;strong&gt;
    /// Endret&lt;/strong&gt; ved …" — INGEN mellomrom mellom punktum og &lt;br/&gt;, samme rå-sammen-
    /// limings-bug som ministry-feltet ville gitt her også). &lt;br/&gt; skrives eksplisitt om til et
    /// linjeskift FØR tekstnodene rundt konkateneres, i stedet for InnerText (som ignorerer &lt;br/&gt;
    /// som om den ikke fantes).
    /// </para>
    /// <para>
    /// Et felt uten NOEN av disse to strukturene (det store flertallet — enkle ett-verdi-felt som
    /// "title"/"dokid"/"refid" osv.) faller trygt tilbake til (2)-løypa, som for et &lt;dd&gt; uten
    /// &lt;br/&gt;-barn i det hele tatt er bit-identisk med et rått InnerText-kall — ingen atferdsendring
    /// for disse.
    /// </para>
    /// </summary>
    private static string HentSammensattTekst(HtmlNode dd)
    {
        var ul = dd.SelectSingleNode("./ul");
        if (ul is not null)
        {
            var verdier = (ul.SelectNodes("./li") ?? Enumerable.Empty<HtmlNode>())
                .Select(li => HtmlEntity.DeEntitize(li.InnerText).Trim())
                .Where(v => v.Length > 0);
            return string.Join(", ", verdier);
        }

        var deler = new List<string>();
        var gjeldende = new StringBuilder();
        void FlushGjeldende()
        {
            var trimmet = gjeldende.ToString().Trim();
            if (trimmet.Length > 0) deler.Add(KollapsDobleMellomrom(trimmet));
            gjeldende.Clear();
        }
        foreach (var barn in dd.ChildNodes)
        {
            if (barn.Name == "br")
            {
                FlushGjeldende();
            }
            else
            {
                gjeldende.Append(HtmlEntity.DeEntitize(barn.InnerText));
            }
        }
        FlushGjeldende();
        return string.Join("\n", deler);
    }

    /// <summary>
    /// Header-metadatafeltet <c>&lt;dt class="basedOn"&gt;Hjemmel&lt;/dt&gt;</c> — hvilke paragraf(er) i
    /// hvilken lov dokumentet er hjemlet i (§ RettskildeHjemmel i Modeller.cs). Bekreftet ekte KUN på
    /// forskrifter under gjennomgang av samtlige fixturer i data/kilder/raw-lovdata/ 2026-08-30 (7
    /// lov-fixturer — advokatloven/alkoholloven/forvaltningsloven/motorferdselloven/personopplysnings-
    /// loven/serveringsloven/tannhelsetjenesteloven — har ALLE 0 forekomster av "basedOn", mens den
    /// ENESTE forskrift-fixturen, alkoholforskriften, har nøyaktig én, med 20 paragraf-referanser, ALLE
    /// til samme lov). Parses likevel UAVHENGIG av Kildetype (returnerer bare tom liste når feltet
    /// mangler — ikke en feil) i tilfelle Lovdata skulle vise seg å bruke feltet på en lov et sted i
    /// det virkelige (langt større) korpuset senere — ingen antagelse låst inn i selve parse-logikken.
    /// <para>
    /// Href-formatet i denne ENE bekreftede forekomsten (<c>lov/1989-06-02-27/§1-2</c>) er BIT-IDENTISK
    /// med løpetekst-kryssreferansenes eget mønster (§3.1 steg 6, se <see cref="LovdataHrefTolker"/> og
    /// <see cref="TolkLenke"/>) — <see cref="LovdataHrefTolker.TolkLøpetekstHref"/> gjenbrukes derfor
    /// direkte i stedet for en egen tolker. Alle 20 bekreftede referanser peker til SAMME lov, men hver
    /// &lt;a&gt; tolkes uavhengig av de andre — en Hjemmel til FLERE ulike lover samtidig (ikke bekreftet
    /// i fixture-korpuset, men strukturelt fullt mulig ut fra selve HTML-formen) håndteres derfor
    /// riktig helt uten videre arbeid, det er ingen antagelse om én-lov-per-dokument noe sted her.
    /// </para>
    /// <para>
    /// Kaster på ukjent href-prefiks i stedet for å gjette en betydning — «ingen gjettet fallback»
    /// (§3.3), samme filosofi som resten av parseren. En hjemmel-lenke UTEN paragrafnummer (f.eks. en
    /// hjemmel til en hel lov/forskrift, ikke én bestemt paragraf — href <c>"forskrift/1969-06-13-3"</c>)
    /// er derimot BEKREFTET ekte og OVERRASKENDE VANLIG, ikke et avvik: full korpusgjennomgang
    /// 2026-09-02 (5882 dokumenter, alle lover + sentrale forskrifter) fant 1711 dokumenter med nettopp
    /// dette mønsteret, dominerende blant delegeringsforskrifter (en kort forskrift som bare delegerer
    /// myndighet videre, hjemlet i en HEL annen forskrift/kongelig resolusjon, ikke én bestemt paragraf
    /// i den). Representeres som en DOKUMENT-nivå Hjemmel (<see cref="RettskildeHjemmel.Eid"/> er da
    /// bare dokument-ELI-en, samme form som <see cref="RettskildeEndring.Eid"/> alltid har) i stedet for
    /// å kaste — se <see cref="RettskildeHjemmel"/> sin klassekommentar for full begrunnelse.
    /// </para>
    /// </summary>
    private static IReadOnlyList<RettskildeHjemmel> HentHjemler(HtmlNode header)
    {
        var dd = header.SelectSingleNode(".//dd[@class='basedOn']");
        if (dd is null) return [];

        var hjemler = new List<RettskildeHjemmel>();
        var sortering = 0;
        foreach (var a in dd.SelectNodes(".//a") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = a.Attributes["href"]?.Value
                ?? throw new FormatException("Hjemmel-lenke mangler href-attributt. Ingen gjettet fallback (§3.3).");
            var tolket = LovdataHrefTolker.TolkLøpetekstHref(href)
                ?? throw new FormatException(
                    $"Hjemmel-lenke '{href}' matcher ikke kjent lov/forskrift-href-mønster. Ingen gjettet fallback (§3.3).");

            var dokumentEli = LovdataIdentifikatorer.AvledEliFraDatokode(tolket.Datokode, out _);
            var eid = tolket.Paragrafnummer is null
                ? dokumentEli
                : LovdataIdentifikatorer.ParagrafEid(dokumentEli, tolket.Paragrafnummer);
            hjemler.Add(new RettskildeHjemmel(eid, sortering++));
        }
        return hjemler;
    }

    /// <summary>
    /// Header-metadatafeltet <c>&lt;dt class="changesToDocuments"&gt;Endrer&lt;/dt&gt;</c> — hvilke(t)
    /// andre dokument(er) DENNE rettskilden ENDRER (§ RettskildeEndring i Modeller.cs, se den
    /// klassekommentaren for full begrunnelse). Strukturelt samme mønster som <see cref="HentHjemler"/>
    /// (samme href-tolkning via <see cref="LovdataHrefTolker.TolkLøpetekstHref"/>, samme «ingen gjettet
    /// fallback»-filosofi), men MOTSATT betingelse på paragrafnummeret: alle 5 bekreftede forekomster i
    /// fixture-korpuset (gjennomgang 2026-09-02: alkoholforskriften/alkoholloven/personopplysningsloven/
    /// serveringsloven/tannhelsetjenesteloven) er rene DOKUMENT-nivå-lenker uten paragrafnummer
    /// ("lov/1927-04-05", "forskrift/1997-12-11-1292") — en (ubekreftet) Endrer-lenke MED
    /// paragrafnummer kaster derfor her, i motsetning til Hjemmel-feltet der det motsatte gjelder.
    /// </summary>
    private static IReadOnlyList<RettskildeEndring> HentEndringer(HtmlNode header)
    {
        var dd = header.SelectSingleNode(".//dd[@class='changesToDocuments']");
        if (dd is null) return [];

        var endringer = new List<RettskildeEndring>();
        var sortering = 0;
        foreach (var a in dd.SelectNodes(".//a") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = a.Attributes["href"]?.Value
                ?? throw new FormatException("Endrer-lenke mangler href-attributt. Ingen gjettet fallback (§3.3).");
            var tolket = LovdataHrefTolker.TolkLøpetekstHref(href)
                ?? throw new FormatException(
                    $"Endrer-lenke '{href}' matcher ikke kjent lov/forskrift-href-mønster. Ingen gjettet fallback (§3.3).");
            if (tolket.Paragrafnummer is not null)
            {
                throw new FormatException(
                    $"Endrer-lenke '{href}' har et paragrafnummer — «Endrer»-feltet er bekreftet ekte KUN " +
                    "som en ren dokument-til-dokument-relasjon i fixture-korpuset. Ingen gjettet fallback (§3.3).");
            }

            var dokumentEli = LovdataIdentifikatorer.AvledEliFraDatokode(tolket.Datokode, out _);
            endringer.Add(new RettskildeEndring(dokumentEli, sortering++));
        }
        return endringer;
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

    /// <summary>Kjente, bekreftede ord brukt for "kapittel" i det virkelige korpuset — se <see cref="FjernNummerPrefiks"/>.
    /// "KAPITTEL" (helt store bokstaver) er en egen, bekreftet ekte variant (personopplysningsloven),
    /// ikke bare et kasse-avvik av "Kapittel" — ordinal StringComparison skiller dem. "Kapitel" (uten
    /// dobbel-t), "Avdeling", "Chapter"/"Section"/"Part" (engelsk — bekreftet ekte i innlemmet
    /// konvensjonstekst, samme mønster som GDPR-annekset) er alle bekreftet ekte under full
    /// korpusgjennomgang 2026-08-21. Ordinal-tallnavn FØR selve kapittel-ordet ("Første kapitel.",
    /// "Andre kapitel.") ble opprinnelig bevisst utelatt her (for få bekreftede forekomster på
    /// tidspunktet) — nå håndtert som en egen, dedikert grein i <see cref="FjernNummerPrefiks"/>
    /// (2026-08-22, se <see cref="NorskOrdinaltall"/>), siden strukturen der er omvendt (tallet kommer
    /// FØR ordet, ikke etter) og ikke passer i denne ord+nummer-løkken.</summary>
    private static readonly string[] KapittelOrdvarianter =
        ["Kapittel", "KAPITTEL", "Kap.", "Kap", "Kapitel", "Avdeling", "Chapter", "Section", "Part", "Avsnitt"];

    /// <summary>
    /// Fullstendig klasseliste hentet fra Lovdatas OFFISIELLE, publiserte formatdokumentasjon
    /// (https://api.lovdata.no/xmldocs, "Documentation for the content of the XML/HTML-format") —
    /// 2026-08-21, etter en runde der flere av disse ble funnet ETT ETT ved faktisk prøving mot
    /// korpuset (§3.3-stilen tåler det, men det er ineffektivt). "legalP" er grunnformen; suffiks-
    /// variantene (i en liste/et marginId-punkt/en fotnote) har samme innholdsrolle — se dokumentets
    /// egne definisjoner. Case-sensitiv (Ordinal): "KAPITTEL"-kasevarianten over beviser at Lovdata
    /// ikke er konsekvent på kasing, så en case-insensitiv sjekk her ville vært en for grov generalisering.
    /// </summary>
    private static readonly string[] LeddKlasser = ["legalP", "numberedLegalP", "listLegalP", "marginIdLegalP", "footnoteLegalP"];

    /// <summary>Alle bekreftede varianter av "vanlig avsnitt/ledetekst" (ikke et juridisk ledd) — samme kilde som <see cref="LeddKlasser"/>.
    /// "centeredP" (bekreftet ekte, personopplysningslovens GDPR-tekst artikkel 99 — EU-forordningens
    /// avsluttende utferdigelses-/signaturformular, "Utferdiget i Brussel, …") er et sentrert avsnitt,
    /// samme rolle som defaultP, bare med annen visningsstil — ingen egen semantikk utover det i den
    /// offisielle klassedokumentasjonen.</summary>
    private static readonly string[] AvsnittKlasser = ["defaultP", "listDefaultP", "marginIdDefaultP", "footnoteDefaultP", "centeredP"];

    /// <summary>
    /// Foreslått/fremtidig innhold (futureLegalArticle/futuresection — ennå ikke vedtatt) eller en
    /// diff-blokk for én bestemt endring (change/document-change/suggested-change/suggested-document-
    /// change). Bulk-datasettet regel-IDE henter fra ("gjeldende-lover"/"gjeldende-sentrale-forskrifter")
    /// er per definisjon KONSOLIDERT, gjeldende tekst — dette skal derfor aldri representeres som
    /// rettskildetekst, samme begrunnelse som den allerede etablerte "changesToParent"-håndteringen.
    /// </summary>
    private static readonly string[] IkkeGjeldendeInnholdKlasser =
        ["futureLegalArticle", "futuresection", "futuretitle", "futureLegalArticleHeader",
         "change", "document-change", "suggested-change", "suggested-document-change"];

    /// <summary>Klassetreff på tvers av flere mellomrom-separerte klassetokens, ikke ren substreng
    /// (unngår at f.eks. "listLegalP" ved et uhell matcher et søk etter en helt annen streng).</summary>
    private static bool HarKlasse(string klasseattributt, string klasse) =>
        klasseattributt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(klasse, StringComparer.Ordinal);

    private static bool ErLeddKlasse(string klasseattributt) => LeddKlasser.Any(k => HarKlasse(klasseattributt, k));
    private static bool ErAvsnittKlasse(string klasseattributt) => AvsnittKlasser.Any(k => HarKlasse(klasseattributt, k));
    private static bool ErIkkeGjeldendeInnhold(string klasseattributt) => IkkeGjeldendeInnholdKlasser.Any(k => HarKlasse(klasseattributt, k));

    /// <summary>Overskriftsnivå for en section/paragraf — h2-h6, ELLER (dypere enn 5 nivåer, bekreftet
    /// dokumentert) &lt;div role="heading" aria-level=N&gt;. XPath-fragment til gjenbruk der en
    /// overskriftsnode skal plukkes ut som DIREKTE barn av et gitt element.</summary>
    private const string OverskriftXPath = "./h2|./h3|./h4|./h5|./h6|./div[@role='heading']";

    private static bool ErOverskriftElement(HtmlNode node) =>
        node.Name is "h2" or "h3" or "h4" or "h5" or "h6"
        || (node.Name == "div" && node.GetAttributeValue("role", "") == "heading");

    /// <summary>
    /// Lovdatas kapittel-/underinndelingsoverskrifter er én tekstnode med nummeret innbakt
    /// (f.eks. "Kapittel 1. Alminnelige bestemmelser.", "I. Alminnelige bestemmelser") — AKN-eksempelet
    /// i §1.3 skiller &lt;num&gt; fra &lt;heading&gt;, så prefikset (allerede kjent fra data-name) fjernes her.
    /// <para>
    /// Det finnes IKKE én fast skrivemåte i det virkelige korpuset — bekreftet ekte under full
    /// Lovdata-synkronisering 2026-08-20 (docs/13-backlog.md §6): "Kapittel N." (vanligst), men også
    /// "Kap. N."/"Kap N." (forkortet ord, med/uten punktum etter selve forkortelsen),
    /// "Kapittel N"/"Kapittel N:" (annen/ingen avsluttende tegnsetting), og enkelte eldre
    /// kapitler/underinndelinger har INGEN egen tittel utover selve nummeret ("I", "1.").
    /// Prøver derfor en liste kjente, bekreftede varianter i stedet for ett hardkodet mønster — et
    /// helt nytt, ukjent mønster skal fortsatt kaste tydelig, ikke gjettes (§3.3). Bevisst IKKE
    /// håndtert her: ordinal-tallnavn ("Første del.", "Del I.") — for få bekreftede forekomster (6 av
    /// ~5900 dokumenter) til å bygge et tallnavn-oppslag uten reell risiko for å gjette feil.
    /// </para>
    /// </summary>
    private static string FjernNummerPrefiks(string heleOverskriften, string nummer)
    {
        // Ordvariantene prøves UAVHENGIG av om dette formelt er et Kapittel eller en Underinndeling —
        // bekreftet ekte at "KAPITTEL"-ordet også forekommer på underinndelings-nivå (personopplysnings-
        // lovens innlemmede GDPR-tekst: EU-forordningens EGNE kapitler ligger som nestede <section>
        // under "gdpr"-super-kapittelet, altså underinndelinger i denne parserens modell, men bruker
        // likevel ordet "KAPITTEL"). Ufarlig å prøve uansett nivå: en ekte bar-nummer-underinndeling som
        // IKKE starter med et av disse ordene treffer rett og slett ingen av dem og faller videre til
        // bar-nummer-sjekken under, akkurat som før.
        // "" (ingen tegnsetting/mellomrom overhodet mellom nummeret og tittelen — "Kapittel VIFelles
        // ordninger") er bekreftet ekte, men prøves ALLTID SIST i begge løkkene under: en ekte
        // tegnsetting-variant skal fortsatt foretrekkes når den finnes, "" er bare et sikkerhetsnett
        // for when kildeteksten ikke har NOE skille i det hele tatt.
        foreach (var ord in KapittelOrdvarianter)
        {
            foreach (var tegnsetting in new[] { ".", " ", ":", "" })
            {
                var prefiks = $"{ord} {nummer}{tegnsetting}";
                if (heleOverskriften.StartsWith(prefiks, StringComparison.Ordinal))
                {
                    return heleOverskriften[prefiks.Length..].TrimStart();
                }

                // "Kapittel. 1. Tittel"/"Kapittel. 1 Tittel" — punktum RETT ETTER selve ordet, IKKE
                // bare etter nummeret (bekreftet ekte, full korpusgjennomgang 2026-08-22, to uavhengige
                // dokumenter). Ufarlig å prøve for alle ord/tegnsettinger: en tekst uten dette ekstra
                // punktumet treffer rett og slett ikke, akkurat som før.
                var prefiksMedPunktumEtterOrd = $"{ord}. {nummer}{tegnsetting}";
                if (heleOverskriften.StartsWith(prefiksMedPunktumEtterOrd, StringComparison.Ordinal))
                {
                    return heleOverskriften[prefiksMedPunktumEtterOrd.Length..].TrimStart();
                }
            }
        }

        // Ordinal-tallnavn FØR selve kapittel-ordet ("Første kapitel.", "Andre kapitel.") — bekreftet
        // ekte, full korpusgjennomgang 2026-08-22 (fire uavhengige dokumenter; tidligere bevisst utelatt
        // pga. for få bekreftede forekomster på tidspunktet, se den historiske kommentaren på
        // KapittelOrdvarianter). Kun de ti første ordinaltallene — et norsk lovverk med FLERE enn ti
        // kapitler i nettopp DENNE gamle skrivemåten er ikke bekreftet i korpuset, og et nytt, ukjent
        // ordinaltall skal fortsatt kaste, ikke gjettes videre oppover (§3.3). Dette er et fast,
        // avgrenset norsk ordforråd (ikke en gjetning) — samme status som KapittelOrdvarianter selv.
        if (int.TryParse(nummer, out var arabiskNummer) && arabiskNummer is >= 1 and <= 10)
        {
            var ordinalOrd = NorskOrdinaltall[arabiskNummer - 1];
            foreach (var kapittelOrd in new[] { "kapitel", "kapittel" })
            {
                foreach (var tegnsetting in new[] { ".", " ", ":", "" })
                {
                    var prefiks = $"{ordinalOrd} {kapittelOrd}{tegnsetting}";
                    if (heleOverskriften.StartsWith(prefiks, StringComparison.Ordinal))
                    {
                        return heleOverskriften[prefiks.Length..].TrimStart();
                    }
                }
            }
        }

        // Bart nummer, uten noe ord foran — gjelder underinndelinger (aldri "Kapittel"-prefikset) og
        // enkelte kapitler som bruker bare romertallet/tallet selv, uten "Kapittel"/"Kap"-ord OG uten
        // tegnsetting mellom nummeret og tittelen ("I Omsetning" — bekreftet ekte, merverdiavgiftsloven;
        // "VIkrafttredelse." med HELT uten skille — bekreftet ekte, en annen lov samme korpusgjennomgang).
        foreach (var tegnsetting in new[] { ".", ":", " ", "" })
        {
            var prefiks = $"{nummer}{tegnsetting}";
            if (heleOverskriften.StartsWith(prefiks, StringComparison.Ordinal))
            {
                return heleOverskriften[prefiks.Length..].TrimStart();
            }
        }

        // Navngitte (ikke-numererte) kapitler/underinndelinger — bekreftet ekte, personopplysningsloven
        // sin innlemmede GDPR-tekst (data-name="gdpr", ingen tall-/romertall-nummerering i det hele
        // tatt). Disse følger ALDRI "N. Tittel"-konvensjonen — selve overskriften ER tittelen, det
        // finnes intet nummer-prefiks å fjerne. Skilt fra en reell, ukjent feil ved at "nummer" her
        // ikke er et tall eller romertall — et NUMMERERT kapittel med en overskrift ingen kjent
        // variant treffer, skal fortsatt kaste (§3.3), ikke stille godtas som "navngitt".
        if (!ErTallEllerRomertall(nummer))
        {
            return heleOverskriften;
        }

        // Bare nummeret, ingen egen tittel — bekreftet ekte (se typekommentaren), ikke en feil.
        var uttallUtenTittel = new List<string> { nummer, $"{nummer}." };
        uttallUtenTittel.AddRange(KapittelOrdvarianter.Select(ord => $"{ord} {nummer}"));
        if (uttallUtenTittel.Contains(heleOverskriften, StringComparer.Ordinal))
        {
            return "";
        }

        // Overskriften starter ikke med NOEN kjent kapittel-/underinndelingsordvariant i det hele
        // tatt — da har den aldri "påstått" å innkode et nummer i utgangspunktet, og det finnes
        // ingenting å validere (til forskjell fra en overskrift som FAKTISK starter med et kjent ord,
        // men med feil eller uklart nummer etterpå — den skal fortsatt kaste under). Bekreftet ekte og
        // OVERRASKENDE vanlig i full korpusgjennomgang 2026-08-22: regelverks-TITLER uten egen
        // nummerering ("Kommisjonsforordning (EU) 2024/3190 …"), kommentar-/merknadsoverskrifter som
        // refererer til et ANNET kapittel enn sitt eget ("Merknader til Kapittel 4 …", "Til kapittel
        // 5 …"), og en rekke andre nummererings-konvensjoner utenfor denne parserens kjente ordliste
        // (engelske "Rule"/"Category"/"PART"/"ANNEX", bokstavlister "a."/"B.", "AVSNITT"/"AVDELING" med
        // et nummer som ikke stemmer med den synlige teksten, osv.) — samme prinsipp som allerede gjaldt
        // for et navngitt (ikke-numerert) kapittel over, bare uavhengig av om selve nummeret
        // tilfeldigvis ER numerisk/romertall. Målt effekt: løser 44 av 48 tidligere importfeil av
        // nettopp denne typen (de resterende 4 starter FAKTISK med et kjent ord som "KAPITTEL", men med
        // et tall som ikke stemmer med den synlige romertallsteksten — en reell, uforklart uoverens-
        // stemmelse i kildedataene, som fortsatt bør kaste, ikke gjettes forbi).
        if (!KapittelOrdvarianter.Any(ord => heleOverskriften.StartsWith(ord, StringComparison.Ordinal)))
        {
            return heleOverskriften;
        }

        throw new FormatException(
            $"Overskrift '{heleOverskriften}' matcher ingen kjent prefiks-variant for nummer '{nummer}' " +
            "('Kapittel N.'/'Kap. N.'/'Kap N.'/bart tall, med eller uten egen tittel). Ingen gjettet fallback (§3.3).");
    }

    /// <summary>De ti første norske ordinaltallene, kun brukt for "Første kapitel."-varianten
    /// (se <see cref="FjernNummerPrefiks"/>) — et fast, avgrenset ordforråd, ikke en gjetning.</summary>
    private static readonly string[] NorskOrdinaltall =
        ["Første", "Andre", "Tredje", "Fjerde", "Femte", "Sjette", "Sjuende", "Åttende", "Niende", "Tiende"];

    /// <summary>Rent tall (§8-2) eller romertall (I-XXXIX, romslig nok margin for kapittelantall
    /// som faktisk forekommer) — skiller en NUMMERERT overskrift (skal ha "N. Tittel"-formen) fra
    /// en NAVNGITT en (f.eks. "gdpr"), se bruken i <see cref="FjernNummerPrefiks"/>.</summary>
    [GeneratedRegex(@"^\d+$|^X{0,3}(IX|IV|V?I{0,3})$")]
    private static partial Regex TallEllerRomertallMønster();

    private static bool ErTallEllerRomertall(string nummer) =>
        nummer.Length > 0 && TallEllerRomertallMønster().IsMatch(nummer);

    /// <summary>"Avsnitt N[.: ]Tittel" — se bruken i <see cref="AvledNummerOgTittelUtenDataName"/>: en
    /// bekreftet underinndelingsvariant uten noe data-name-attributt, nummeret må derfor parses ut av
    /// selve overskriftsteksten. Både arabiske tall OG romertall bekreftet ekte ("Avsnitt 1 Tittel" og
    /// "Avsnitt I. Tittel"), skilletegnet varierer også ("Avsnitt 1 Tittel"/"Avsnitt 1. Tittel").</summary>
    [GeneratedRegex(@"^Avsnitt (\d+|[IVXLCDM]+)[.:]?\s*")]
    private static partial Regex AvsnittUtenDataNameMønster();

    /// <summary>Bart romertall (stort ELLER lite — "i." bekreftet ekte, ellers store bokstaver) fulgt av
    /// et skilletegn, uten noe kapittel-ord foran — se <see cref="AvledNummerOgTittelUtenDataName"/>.</summary>
    [GeneratedRegex(@"^([IVXLCDM]+|[ivxlcdm]+)[.:\s]")]
    private static partial Regex BartRomertallUtenDataNameMønster();

    /// <summary>
    /// Deriverer nummer+tittel fra selve overskriftsteksten når data-name-attributtet mangler HELT —
    /// bekreftet ekte og overraskende vanlig for eldre lover under full korpusgjennomgang 2026-08-21
    /// (se ParseKapittel/ParseUnderinndeling). Prøves i rekkefølge: 1) "Avsnitt N …" (personopplysnings-
    /// lovens GDPR-tekst), 2) et bart romertall fulgt av skilletegn, 3) faller tilbake til å bruke HELE
    /// overskriften som en navngitt (ikke-nummerert) underinndeling — bekreftet ekte, en rekke eldre
    /// lover har navngitte kapitler/underinndelinger uten noe nummer overhodet ("Innledende
    /// bestemmelser", "Selskapsmøtet") — med en slugget versjon av overskriften som "nummer", altså
    /// samme rolle data-name normalt spiller, bare avledet fra teksten der attributtet ikke finnes.
    /// Ingen av disse tre er gjetning: alle er bekreftede, faktiske mønstre i korpuset, ikke antagelser.
    /// </summary>
    private static (string Nummer, string Tittel) AvledNummerOgTittelUtenDataName(string heleOverskriften)
    {
        var avsnitt = AvsnittUtenDataNameMønster().Match(heleOverskriften);
        if (avsnitt.Success)
        {
            return (avsnitt.Groups[1].Value, heleOverskriften[avsnitt.Value.Length..].TrimStart());
        }

        var romertall = BartRomertallUtenDataNameMønster().Match(heleOverskriften);
        if (romertall.Success)
        {
            var tallDel = romertall.Value[..^1]; // uten selve skilletegnet på slutten
            return (tallDel.ToUpperInvariant(), heleOverskriften[romertall.Value.Length..].TrimStart());
        }

        return (Slugifiser(heleOverskriften), heleOverskriften);
    }

    // ---------- Kapittel / underinndeling (steg 5) ----------

    private static void ParseKapittel(
        HtmlNode section, ReferanseKontekst kontekst, List<RettskildeNode> noder,
        List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var dataName = section.Attributes["data-name"]?.Value;
        var heleOverskriften = HtmlEntity.DeEntitize(section.SelectSingleNode(OverskriftXPath)?.InnerText.Trim() ?? "");
        string kapittelNummer;
        string overskrift;
        if (dataName is not null)
        {
            kapittelNummer = dataName.StartsWith("kap", StringComparison.Ordinal) ? dataName[3..] : dataName;
            overskrift = FjernNummerPrefiks(heleOverskriften, kapittelNummer);
        }
        else
        {
            // Bekreftet ekte, uten data-name-attributt overhodet — eldre lover under full
            // korpusgjennomgang 2026-08-21, se AvledNummerOgTittelUtenDataName.
            (kapittelNummer, overskrift) = AvledNummerOgTittelUtenDataName(heleOverskriften);
        }
        var eid = GjørEidUnik(LovdataIdentifikatorer.KapittelEid(kapittelNummer), noder);
        var kildeId = section.Attributes["id"]?.Value
            ?? throw new FormatException($"Kapittel {eid} mangler id-attributt.");

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
        var dataName = section.Attributes["data-name"]?.Value;
        var heleOverskriften = HtmlEntity.DeEntitize(section.SelectSingleNode(OverskriftXPath)?.InnerText.Trim() ?? "");
        string romertall;
        string overskrift;
        if (dataName is not null)
        {
            romertall = dataName.StartsWith("kap", StringComparison.Ordinal) ? dataName[3..] : dataName;
            overskrift = FjernNummerPrefiks(heleOverskriften, romertall);
        }
        else
        {
            // Bekreftet ekte, uten data-name-attributt overhodet — personopplysningslovens innlemmede
            // GDPR-tekst ("Avsnitt N …" som en tredje underinndelings-nivå INNI et allerede navngitt
            // kapittel) og en rekke eldre lover, se AvledNummerOgTittelUtenDataName.
            (romertall, overskrift) = AvledNummerOgTittelUtenDataName(heleOverskriften);
        }
        var eid = GjørEidUnik(LovdataIdentifikatorer.UnderinndelingEid(parentKapittelEid, romertall), noder);
        var kildeId = section.Attributes["id"]?.Value
            ?? throw new FormatException($"Underinndeling {eid} mangler id-attributt.");

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
        // "Paragraf-løse" kapitler/underinndelinger — bekreftet ekte og OVERRASKENDE VANLIG under full
        // korpusgjennomgang 2026-08-21 (dokumentasjonen på https://api.lovdata.no/xmldocs nevner det
        // ikke eksplisitt, men mønsteret er identisk med den allerede kjente "kapittelfri lov"-varianten
        // i Parse-metoden, bare ett nivå dypere): en rent administrativ ikrafttredelses-/overgangs-
        // bestemmelse ("Loven gjelder fra den tid Kongen bestemmer") ligger ofte som siste "kapittel" i
        // en endringslov, med ledd/lister direkte som kapittelinnhold — INGEN omsluttende <article
        // class="legalArticle">/paragraf. leddIndeks/punktIndeks er scopet til denne containeren, samme
        // prinsipp som i ParseParagraf/ParseChildPunkter.
        var leddIndeks = 0;
        var punktIndeks = 0;

        // Lokal funksjon (ikke en topp-nivå-metode) fordi den må dele leddIndeks/punktIndeks ved
        // MUTASJON med både hovedløkken og div.indent-rekursjonen under — to uavhengige tellere ville
        // gitt eId-KOLLISJONER (samme "ledd-1" produsert to ganger under samme containerEid) hvis
        // div.indent fikk sin egen, friske teller i stedet for å dele denne.
        void HåndterBarn(HtmlNode child)
        {
            if (child.NodeType != HtmlNodeType.Element) return;
            var klasse = child.GetAttributeValue("class", "");
            if (ErIkkeGjeldendeInnhold(klasse))
            {
                // MÅ sjekkes FØR "section"/"legalArticle"-substrengsjekkene under: "futuresection"
                // inneholder selv substrengen "section" og ville ellers blitt feilaktig behandlet som
                // en ordinær Underinndeling (bekreftet ekte — <span class="futuretitle"> som overskrift-
                // erstatning inni en slik seksjon, ingen h2-h6/div[role=heading] å lese, kastet lenger
                // ned i løkken). Se IkkeGjeldendeInnholdKlasser (samme begrunnelse som i Parse-metoden).
            }
            else if (child.Name == "section" && klasse.Contains("section"))
            {
                ParseUnderinndeling(child, containerEid, kontekst, noder, referanser, sortering);
            }
            else if (child.Name == "article" && klasse.Contains("legalArticle"))
            {
                ParseParagraf(child, containerEid, kontekst, noder, referanser, sortering);
            }
            else if (child.Name == "article" && (ErLeddKlasse(klasse) || klasse.Contains("marginIdArticle")))
            {
                // "marginIdArticle" (bekreftet ekte, EØS-tilpasningstekst — <span class="data-marginOriginalId">
                // med flere direkte legalP-ledd, https://api.lovdata.no/xmldocs) har NØYAKTIG samme form
                // som en listArticle/Punkt (ett eller flere direkte legalP-barn) — ParsePunkt gjenbrukes
                // derfor direkte i stedet for duplisert logikk, med samme punktIndeks-teller som en
                // ordinær liste på dette nivået ville brukt.
                if (klasse.Contains("marginIdArticle"))
                {
                    punktIndeks++;
                    ParsePunkt(child, containerEid, punktIndeks, kontekst, noder, referanser, sortering);
                }
                else
                {
                    leddIndeks++;
                    ParseLedd(child, eidBase: containerEid, parentEid: containerEid, leddIndeks, kontekst, noder, referanser, sortering);
                }
            }
            else if (child.Name is "ul" or "ol" && klasse.Contains("defaultList"))
            {
                ParseEnListe(child, containerEid, ref punktIndeks, kontekst, noder, referanser, sortering);
            }
            else if ((child.Name == "div" && klasse.Contains("indent")) || child.Name == "blockquote")
            {
                // Rent visuelt innrykk (bekreftet ekte — sitert/innlemmet EØS-forordningstekst) uten
                // egen strukturell betydning — transparent: barna behandles som om de lå direkte i
                // containeren, med SAMME leddIndeks/punktIndeks-teller (se kommentaren over).
                foreach (var grandchild in child.ChildNodes) HåndterBarn(grandchild);
            }
            else if (ErOverskriftElement(child))
            {
                // overskrift, allerede lest i ParseKapittel/ParseUnderinndeling
            }
            else if (child.Name == "article" && klasse.Contains("changesToParent"))
            {
                // endringshistorikk for hele kapittelet/underinndelingen -> proveniens, ikke tekstinnhold
                // (samme begrunnelse som i ParseParagraf; utenfor scope uten proveniens-lager i denne byggeøkten).
            }
            else if (child.Name == "article" && ErAvsnittKlasse(klasse))
            {
                // Merknad/kommentar direkte under et KAPITTEL (ikke inni en paragraf) — bekreftet ekte,
                // personopplysningslovens innlemmede GDPR-tekst (data-name="gdpr", et helt kapittel av
                // ren kommentarprosa uten egne paragrafer). Samme rolle/behandling som defaultP på
                // dokument- og paragraf-nivå (se Parse-metoden og ParseParagraf) — metainformasjon,
                // ikke selve rettskildeteksten/leddene.
            }
            else if (child.Name == "footer" && klasse.Contains("footnotes"))
            {
                // Fotnoter til den kommentarprosaen over (samme dokument/kapittel som bare har defaultP,
                // ingen egne paragrafer å feste fotnotene til — RettskildeNode.Fotnoter finnes kun på
                // Paragraf-noder). Samme "metainformasjon, ikke tekstinnhold"-begrunnelse som defaultP
                // rett over — hoppes bevisst over i stedet for å konstruere et nytt festepunkt for et
                // enkelttilfelle.
            }
            else
            {
                throw new NotSupportedException(
                    $"Uventet element under kapittel/underinndeling {containerEid}: <{child.Name} class=\"{klasse}\">. " +
                    "Ingen gjettet fallback (§3.3).");
            }
        }

        foreach (var child in container.ChildNodes) HåndterBarn(child);
    }

    // ---------- Paragraf / ledd / punkt (steg 5) ----------

    private static void ParseParagraf(
        HtmlNode article, string? parentEid, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var lovdataUrl = article.Attributes["data-lovdata-URL"]?.Value
            ?? throw new FormatException("Paragraf mangler data-lovdata-URL.");
        var paragrafnummer = lovdataUrl[(lovdataUrl.LastIndexOf('/') + 1)..];
        var eid = GjørEidUnik(LovdataIdentifikatorer.ParagrafEid(kontekst.EgenLovEli, paragrafnummer), noder);
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
        var punktIndeks = 0;

        void HåndterParagrafBarn(HtmlNode child)
        {
            if (child.NodeType != HtmlNodeType.Element) return;
            var klasse = child.GetAttributeValue("class", "");
            if (ErOverskriftElement(child) && klasse.Contains("legalArticleHeader"))
            {
                // overskrift, allerede lest. h2 (evt. div[role=heading] på store dybder) forekommer når
                // paragrafen selv er toppnivå-elementet (kapittelfri lov, se Parse-metodens documentBody-
                // løkke) — uten en omsluttende kapitteloverskrift flytter paragrafens EGEN overskrift opp
                // ett hakk, jf. den offisielle formatdokumentasjonens "alltid 1 høyere enn nærmeste <h\d>
                // under en <section>"-regel.
            }
            else if (child.Name is "ul" or "ol" && klasse.Contains("defaultList"))
            {
                // Liste DIREKTE under paragrafen, uten noe omsluttende ledd — bekreftet ekte (og
                // overraskende vanlig, full korpusgjennomgang 2026-08-21): typisk en "Definisjoner"-
                // paragraf der selve punktlisten ER paragrafens eneste innhold (§3 i en rekke moderne
                // lover, f.eks. åpenhetsloven). Samme mønster ett nivå opp som den paragraf-løse
                // kapittel-varianten i ParseKapittelInnhold. Egen punktIndeks-scope (ikke delt med et
                // eventuelt sideordnet ledd) — bekreftet at disse to aldri forekommer sammen i samme
                // paragraf i det virkelige korpuset.
                ParseEnListe(child, eid, ref punktIndeks, kontekst, noder, referanser, sortering);
            }
            else if (child.Name == "article" && (ErLeddKlasse(klasse) || klasse.Contains("marginIdArticle")))
            {
                // "numberedLegalP" (bekreftet ekte, merverdiavgiftsloven/kassasystemforskriften) er et
                // ledd med et eksplisitt data-numerator-attributt (moderne, nummerert (1)/(2)/…) —
                // samme innholdsrolle som "legalP". "listLegalP"/"marginIdLegalP"/"footnoteLegalP" er
                // øvrige dokumenterte varianter (https://api.lovdata.no/xmldocs) med samme rolle når de
                // skulle forekomme direkte under en paragraf (ikke bare inni en liste/fotnote) —
                // ErLeddKlasse samler alle i ett sted i stedet for spredte case-sensitive Contains()-kall
                // (§3.3-lærdom: en tidligere versjon fanget ikke "numberedLegalP" pga. nettopp dette).
                // "marginIdArticle" direkte under en paragraf (bekreftet ekte, samme rolle som i
                // ParseKapittelInnhold) rutes til ParsePunkt med samme punktIndeks-teller som en liste.
                if (klasse.Contains("marginIdArticle"))
                {
                    ParsePunkt(child, eid, ++punktIndeks, kontekst, noder, referanser, sortering);
                }
                else
                {
                    leddIndeks++;
                    ParseLedd(child, eidBase: eid, parentEid: eid, leddIndeks, kontekst, noder, referanser, sortering);
                }
            }
            else if ((child.Name == "div" && klasse.Contains("indent")) || child.Name == "blockquote")
            {
                // Samme transparente innrykk-håndtering som i ParseKapittelInnhold/Parse — bekreftet
                // ekte og vanlig også direkte under en paragraf (siterte/innlemmede EU-/EØS-tekster,
                // full korpusgjennomgang 2026-08-21).
                foreach (var grandchild in child.ChildNodes) HåndterParagrafBarn(grandchild);
            }
            else if (child.Name == "article" && klasse.Contains("changesToParent"))
            {
                // endringshistorikk -> proveniens, ikke tekstinnhold (§3.1 steg 5). Utenfor scope
                // for en pipeline uten proveniens-lager i denne byggeøkten.
            }
            else if (ErIkkeGjeldendeInnhold(klasse))
            {
                // Foreslått/fremtidig paragraftekst eller en enkelt-endrings diff-blokk — se
                // IkkeGjeldendeInnholdKlasser (samme begrunnelse som i Parse-metoden).
            }
            else if (child.Name == "article" && ErAvsnittKlasse(klasse))
            {
                // To ULIKE, bekreftede ekte former for et "defaultP" (§ AvsnittKlasser: per Lovdatas
                // egen offisielle klassedokumentasjon formelt "ikke et juridisk ledd") som DIREKTE
                // barn av en paragraf:
                //
                // 1) Et elidert-innhold-plassholder — bekreftet ekte, personopplysningsloven § 34
                //    "Endringer i andre lover": ledd-1 er en ordinær legalP, etterfulgt av
                //    <article class="defaultP">– – –</article> (kun tankestreker). Den konsoliderte
                //    ("gjeldende") teksten viser IKKE selve endringslisten (allerede utført/historisk),
                //    bare denne plassholderen — ren metainformasjon, ikke rettskildetekst. Fortsatt
                //    korrekt å hoppe over, som før denne endringen.
                //
                // 2) Et FAKTISK, substansielt ledd Lovdata av ukjent årsak IKKE har tagget som "legalP"
                //    — bekreftet ekte, FOR-2001-03-09-439 (forskrift om skipsmedisin) § 4
                //    "Fartøygrupper": ledd-1 ("Med fartøygrupper menes:") er en ordinær legalP MED
                //    id-attributt, men de tre påfølgende definisjonsavsnittene ("Fartøygruppe A:"/"B:"/
                //    "C:" …) er defaultP UTEN NOE id-attributt overhodet. Innholdsmessig er de like
                //    fullt ledd 2/3/4 (bekreftet ordrett mot lovdata.no/forskrift/2001-03-09-439/§4) —
                //    å hoppe over dem som "metainformasjon" mistet reelt rettskildeinnhold (bekreftet
                //    manuelt av Johann via /begrepskandidater-siden).
                //
                // Skillet mellom de to: er avsnittets tekst UTELUKKENDE tankestrek(er)/mellomrom (case 1,
                // ErEliderPlassholderTekst) eller ikke (case 2, reelt innhold). Verifisert på ALLE 8
                // fixturene i data/kilder/raw-lovdata: dette er de ENESTE to bekreftede paragraf-nivå
                // defaultP-mønstrene i korpuset så langt — et helt ukjent TREDJE mønster ville likevel
                // IKKE kastet en tydelig "ukjent struktur"-feil lenger etter denne endringen (det havner
                // stille i case 2), men det er en bevisst avveining: siden case 1 er presist
                // gjenkjennbart (kun tankestreker), er "har avsnittet noe ANNET enn det" en presis nok
                // beslutningsregel til å slå fast at det er reelt innhold — ikke en løs gjetning slik
                // §3.3 advarer mot.
                var avsnittSegmenter = HentSegmenter(child, kontekst);
                var avsnittTekst = KollapsDobleMellomrom(string.Concat(avsnittSegmenter.Select(s => s.Tekst))).Trim();
                if (!ErEliderPlassholderTekst(avsnittTekst))
                {
                    leddIndeks++;
                    var leddEid = GjørEidUnik(LovdataIdentifikatorer.LeddEid(eid, leddIndeks), noder);
                    // child mangler (bekreftet) id-attributt i case 2 — se LeggTilLeddEllerPunktNodes
                    // kildeIdNårIdMangler-parameter. Syntetisert fra paragrafens EGEN kildeId (alltid
                    // til stede, sjekket over) + løpenummer, deterministisk og globalt unikt innenfor
                    // paragrafen på samme måte som eId'en over.
                    var syntetiskKildeId = $"{kildeId}-avsnitt-{leddIndeks}";
                    LeggTilLeddEllerPunktNode(
                        child, leddEid, eid, NodeType.Ledd, kontekst, noder, referanser, sortering,
                        leddIndeks.ToString(), kildeIdNårIdMangler: syntetiskKildeId);
                    ParseChildPunkter([child], leddEid, kontekst, noder, referanser, sortering);
                }
            }
            else if (child.Name == "footer" && klasse.Contains("footnotes"))
            {
                fotnoter.AddRange(ParseFotnoter(child));
            }
            else if (child.Name == "p" && klasse.Contains("leddfortsettelse"))
            {
                // Fortsettelsestekst for et FORUTGÅENDE ledd/punkt DIREKTE under paragrafen, typisk rett
                // etter en liste som selv lå direkte under paragrafen uten noe omsluttende ledd
                // (bekreftet ekte, alkoholforskriften § 7-2 og flere andre — full korpusgjennomgang
                // 2026-08-22). Ikke sitt eget ledd — appender til Tekst/Segmenter på den SISTE
                // ledd-/punkt-noden som allerede er lagt til under denne paragrafen (samme "ledd"-nivå
                // som en liste-fortsettelse ville tilhørt), i stedet for å opprette en ny, løsrevet node.
                var forrigeIndeks = noder.FindLastIndex(n => n.ParentEid == eid);
                if (forrigeIndeks < 0)
                {
                    throw new NotSupportedException(
                        $"<p class=\"leddfortsettelse\"> under paragraf {eid} har ingen forutgående ledd/punkt å fortsette. Ingen gjettet fallback (§3.3).");
                }
                var forrige = noder[forrigeIndeks];
                var forrigeTekstLengde = forrige.Tekst?.Length ?? 0;
                var nyeSegmenter = new List<TekstSegment>(forrige.Segmenter ?? []) { new(" ", null, false) };
                nyeSegmenter.AddRange(HentSegmenter(child, kontekst));
                var nyTekst = KollapsDobleMellomrom(string.Concat(nyeSegmenter.Select(s => s.Tekst))).Trim();
                noder[forrigeIndeks] = forrige with
                {
                    Tekst = nyTekst,
                    TekstHash = LovdataIdentifikatorer.BeregnTekstHash(nyTekst),
                    Segmenter = nyeSegmenter,
                };
                LeggTilReferanser(referanser, forrige.Eid, nyeSegmenter, nyTekst, startCursor: forrigeTekstLengde);
            }
            else
            {
                throw new NotSupportedException(
                    $"Uventet element under paragraf {eid}: <{child.Name} class=\"{klasse}\">. Ingen gjettet fallback (§3.3).");
            }
        }

        foreach (var child in article.ChildNodes) HåndterParagrafBarn(child);
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

    /// <summary>
    /// <paramref name="eidBase"/> brukes KUN til å bygge selve eId'en (må alltid være en ekte streng —
    /// et globalt unikt eId krever en base uansett om leddet reelt har en overordnet node eller ikke).
    /// <paramref name="parentEid"/> er derimot hva som faktisk skrives til RettskildeNode.ParentEid, og
    /// er bevisst en SEPARAT, nullbar parameter: et ledd direkte i documentBody (kapittel- OG paragraf-
    /// fri lov, se Parse-metoden) må ha eId'en scopet til dokumentets ELI for global unikhet, men
    /// ParentEid=null siden det ikke finnes noen reell overordnet NODE i databasen å peke på — å sette
    /// ParentEid til en streng som ikke er noen ekte nodes eId gir en FK-CONSTRAINT-FEIL ved lagring
    /// (bekreftet ekte, FOR-1905-11-15-1 og en rekke andre — funnet under full korpus-resynkronisering
    /// 2026-08-21 rett etter denne parser-runden).
    /// </summary>
    private static void ParseLedd(
        HtmlNode legalP, string eidBase, string? parentEid, int leddIndeks, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var eid = GjørEidUnik(LovdataIdentifikatorer.LeddEid(eidBase, leddIndeks), noder);
        LeggTilLeddEllerPunktNode(legalP, eid, parentEid, NodeType.Ledd, kontekst, noder, referanser, sortering, leddIndeks.ToString());
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
                ParseEnListe(liste, parentEid, ref punktIndeks, kontekst, noder, referanser, sortering);
            }
        }
    }

    /// <summary>
    /// Selve punkt-utbrytningen for ÉN &lt;ul&gt;/&lt;ol&gt;-liste — uttrukket fra <see cref="ParseChildPunkter"/>
    /// slik at <see cref="ParseKapittelInnhold"/> kan gjenbruke nøyaktig samme logikk for lister som ligger
    /// DIREKTE under et kapittel/en underinndeling (paragraf-løs ikrafttredelsesbestemmelse, se der).
    /// <paramref name="punktIndeks"/> er 'ref' slik at nummereringen løper fortløpende over FLERE lister i
    /// samme nivå, akkurat som når ParseChildPunkter kalles med flere containere i ett kall.
    /// </summary>
    private static void ParseEnListe(
        HtmlNode liste, string parentEid, ref int punktIndeks, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        foreach (var li in liste.SelectNodes("./li") ?? Enumerable.Empty<HtmlNode>())
        {
            var listArticle = li.SelectSingleNode("./article[contains(@class,'listArticle')]")
                ?? throw new FormatException($"<li> under {parentEid} mangler <article class=\"listArticle\">.");
            punktIndeks++;
            ParsePunkt(listArticle, parentEid, punktIndeks, kontekst, noder, referanser, sortering);
        }
    }

    private static void ParsePunkt(
        HtmlNode listArticle, string parentEid, int punktIndeks, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering)
    {
        var eid = GjørEidUnik(LovdataIdentifikatorer.PunktEid(parentEid, punktIndeks), noder);
        var kildeId = listArticle.Attributes["id"]?.Value
            ?? throw new FormatException($"Punkt {eid} mangler id-attributt.");

        // Filtreres i C# (ErLeddKlasse), ikke via XPath contains(@class,'legalP') — det XPath-uttrykket
        // er case-sensitivt og ville IKKE truffet "listLegalP" (stor "L" i "Legal" — samme kasse-felle
        // som allerede funnet og fikset for "numberedLegalP" i ParseParagraf). Straffelovens § 5 i den
        // offisielle formatdokumentasjonen (https://api.lovdata.no/xmldocs) viser nettopp
        // <article class="listLegalP"> som barn av en listArticle, altså en bekreftet ekte klassenavn
        // her — ikke bare et hypotetisk edge-case.
        var direkteLegalP = (listArticle.SelectNodes("./article") ?? Enumerable.Empty<HtmlNode>())
            .Where(a => ErLeddKlasse(a.GetAttributeValue("class", "")))
            .ToList();

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

        if (direkteLegalP.Count == 0)
        {
            // Et rent underoverskrift-punkt uten egen bladtekst — bekreftet ekte og OVERRASKENDE VANLIG
            // (den klart største enkeltårsaken til gjenstående feil ved full korpusgjennomgang
            // 2026-08-21): et marginIdArticle/listArticle kan være en "miscHeadline" (§ span.miscHeadline
            // i https://api.lovdata.no/xmldocs) i stedet for et reelt punkt med tekst — f.eks. "1.2.
            // Transport" som ren mellomtittel for de faktiske punktene 1.2.1/1.2.2 som følger etter.
            // miscHeadline-teksten brukes da som selve punktets Tekst (fortsatt riktig, søkbart innhold)
            // i stedet for at strukturen kastes som en uventet feil. Fortsatt en ekte, uforklart anomali
            // (verken legalP ELLER miscHeadline) skal kaste, ikke stille godtas (§3.3).
            var misc = listArticle.SelectSingleNode("./span[contains(@class,'miscHeadline')]")
                ?? throw new FormatException($"Punkt {eid} har ingen nestet <article class=\"legalP\"/\"listLegalP\"/…> og ingen <span class=\"miscHeadline\"> — uventet struktur, ingen gjettet fallback (§3.3).");
            alleSegmenter.AddRange(HentSegmenter(misc, kontekst));
        }
        var plainTekst = KollapsDobleMellomrom(string.Concat(alleSegmenter.Select(s => s.Tekst))).Trim();
        var hash = LovdataIdentifikatorer.BeregnTekstHash(plainTekst);

        noder.Add(new RettskildeNode
        {
            Eid = eid,
            ParentEid = parentEid,
            KildeId = kildeId,
            NodeType = NodeType.Punkt,
            Nummer = punktIndeks.ToString(),
            Tekst = plainTekst,
            TekstHash = hash,
            Segmenter = alleSegmenter,
            SorteringsRekkefolge = sortering.Neste(),
        });
        LeggTilReferanser(referanser, eid, alleSegmenter, plainTekst);

        ParseChildPunkter(direkteLegalP, eid, kontekst, noder, referanser, sortering);
    }

    /// <summary>
    /// Felles for ledd og punkt: begge er "bladtekst-bærende" noder hvis egen Tekst/TekstHash kun
    /// dekker deres EGEN inline-tekst (HentSegmenter stopper ved nestet &lt;ul&gt;/&lt;ol&gt;) —
    /// undernoder (punkt/underpunkt) sin tekst telles ikke med, samme prinsipp som kapittel ikke
    /// inkluderer sine paragrafers tekst.
    /// </summary>
    private static void LeggTilLeddEllerPunktNode(
        HtmlNode legalP, string eid, string? parentEid, NodeType nodeType, ReferanseKontekst kontekst,
        List<RettskildeNode> noder, List<RettskildeReferanse> referanser, SorteringsTeller sortering,
        string? nummer = null, string? kildeIdNårIdMangler = null)
    {
        // kildeIdNårIdMangler brukes KUN av det bekreftede "defaultP-som-ledd"-tilfellet i
        // HåndterParagrafBarn (se der) — et ekte, substansielt ledd Lovdata av ukjent årsak ikke har
        // gitt noe id-attributt i det hele tatt (FOR-2001-03-09-439 § 4, "Fartøygruppe A/B/C"). Alle
        // ANDRE kallesteder lar denne stå null, slik at et manglende id-attributt fortsatt kaster
        // akkurat som før (§3.3) — ingen ny stille fallback for legalP-ledd/punkt generelt.
        var kildeId = legalP.Attributes["id"]?.Value
            ?? kildeIdNårIdMangler
            ?? throw new FormatException($"{nodeType} {eid} mangler id-attributt.");
        var segmenter = HentSegmenter(legalP, kontekst);
        var plainTekst = KollapsDobleMellomrom(string.Concat(segmenter.Select(s => s.Tekst))).Trim();
        var hash = LovdataIdentifikatorer.BeregnTekstHash(plainTekst);

        noder.Add(new RettskildeNode
        {
            Eid = eid,
            ParentEid = parentEid,
            KildeId = kildeId,
            NodeType = nodeType,
            Nummer = nummer,
            Tekst = plainTekst,
            TekstHash = hash,
            Segmenter = segmenter,
            SorteringsRekkefolge = sortering.Neste(),
        });

        LeggTilReferanser(referanser, eid, segmenter, plainTekst);
    }

    /// <summary>
    /// <paramref name="tekst"/> er den ENDELIGE, lagrede nodeteksten (etter kollaps av doble
    /// mellomrom og Trim) — posisjonen til hver referanse slås opp med <see cref="string.IndexOf(string, int)"/>
    /// fra en løpende cursor i stedet for å summere rå segment-lengder, slik at mellomrom-kollapsing/
    /// trimming ikke gir feil offset. Finnes ikke et treff (bør ikke skje i praksis), forblir
    /// TekstStart/TekstLengde null — referansen vises da fortsatt i "Referanser"-lista i UI-et, bare
    /// ikke som en klikkbar lenke inni selve løpeteksten.
    /// <paramref name="startCursor"/> (2026-08-22): brukes når <paramref name="segmenter"/> kun er en
    /// TILLEGG-del av en node som allerede har fått sine opprinnelige referanser lagt til én gang
    /// (se "leddfortsettelse"-håndteringen i HåndterParagrafBarn) — søket starter da etter den
    /// allerede prosesserte delen av <paramref name="tekst"/> i stedet for fra 0, slik at et kort
    /// segment i fortsettelsen ikke feilaktig matcher en tidligere forekomst av samme tekst.
    /// </summary>
    private static void LeggTilReferanser(
        List<RettskildeReferanse> referanser, string fraEid, IReadOnlyList<TekstSegment> segmenter, string tekst, int startCursor = 0)
    {
        var cursor = startCursor;
        foreach (var s in segmenter)
        {
            if (s.Tekst.Length == 0) continue;
            var funnet = tekst.IndexOf(s.Tekst, cursor, StringComparison.Ordinal);
            if (s.ReferanseTilEid is not null)
            {
                if (funnet >= 0)
                {
                    referanser.Add(new RettskildeReferanse(fraEid, s.ReferanseTilEid, s.ErInternReferanse, null, null, funnet, s.Tekst.Length));
                    cursor = funnet + s.Tekst.Length;
                }
                else
                {
                    referanser.Add(new RettskildeReferanse(fraEid, s.ReferanseTilEid, s.ErInternReferanse, null, null));
                }
            }
            else if (funnet >= 0)
            {
                cursor = funnet + s.Tekst.Length;
            }
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

    /// <summary>Lovdatas konvensjon for et "elidert" ledd-plassholder-avsnitt (et defaultP direkte i en
    /// paragraf, i stedet for et vanlig ledd) — bekreftet ekte, personopplysningsloven § 34 "Endringer i
    /// andre lover": teksten er UTELUKKENDE tankestrek(er) ("– – –", U+2013 gjentatt, mellomromsseparert)
    /// og bærer ikke noe reelt rettskildeinnhold. Tomt avsnitt (ingen tekst i det hele tatt) regnes også
    /// som plassholder — ikke bekreftet ekte, men det finnes uansett intet reelt innhold å tape ved å
    /// hoppe over. Se bruken i HåndterParagrafBarn (ParseParagraf) for hvordan dette skiller en ekte
    /// "defaultP-som-ledd" (FOR-2001-03-09-439 § 4) fra denne plassholder-varianten.</summary>
    [GeneratedRegex(@"^[\s\-‐‑‒–—―]*$")]
    private static partial Regex EliderPlassholderMønster();

    private static bool ErEliderPlassholderTekst(string tekst) => EliderPlassholderMønster().IsMatch(tekst);

    // ---------- Inline tekst-/referanse-ekstraksjon ----------

    /// <summary>Kjente inline-elementer som bare gir videre ekstraksjon, ingen egen semantikk her.
    /// "sup" UTEN "footnotereference"-klasse (bekreftet ekte — "8 m&lt;sup&gt;2&lt;/sup&gt; gulvflate",
    /// altså vanlig typografisk hevet skrift/"m²", ikke en fotnotereferanse) faller hit fordi den
    /// footnotereference-spesifikke grenen over allerede har fanget den ANDRE varianten av "sup".
    /// "s" (gjennomstreking, bekreftet ekte i en oppheving/rettelse) — rent visuell stil, samme
    /// begrunnelse som "b"/"i"/"em"/"strong".</summary>
    private static readonly HashSet<string> GjennomsiktigeInlineElementer = new(StringComparer.Ordinal)
        { "i", "b", "span", "sub", "sup", "em", "strong", "p", "s" };

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
            else if (child.Name == "a")
            {
                // <a> uten href-attributt overhodet (bekreftet ekte — et bokmerke-/ankermål uten egen
                // lenkedestinasjon, ikke en referanse) — samme transparente behandling som de vanlige
                // GjennomsiktigeInlineElementer under, bare et eget case siden "a" ellers alltid
                // forsøkes tolket som lenke over.
                segmenter.AddRange(HentSegmenter(child, kontekst));
            }
            else if ((child.Name == "div" && klasse.Contains("indent")) || child.Name == "blockquote")
            {
                // Samme transparente innrykk-håndtering som i ParseKapittelInnhold/Parse/
                // HåndterParagrafBarn — bekreftet ekte også INNI løpetekst (siterte/innlemmede EU-/
                // EØS-tekster nøstet dypere enn ledd-/punkt-nivå, full korpusgjennomgang 2026-08-22).
                segmenter.AddRange(HentSegmenter(child, kontekst));
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
            else if (child.Name == "br")
            {
                // Rent visuelt linjeskift innad i løpeteksten (bekreftet ekte, EØS-henvisninger og
                // ikrafttredelse-fotnoter med flere klausuler) — et selvlukkende element uten barn å
                // rekursere inn i. Samme mellomrom-skille-prinsipp som ved en hoppet-over liste, slik at
                // tekst før og etter <br/> ikke smelter sammen.
                segmenter.Add(new TekstSegment(" ", null, false));
            }
            else if (child.Name == "table")
            {
                if (segmenter.Count > 0) segmenter.Add(new TekstSegment(" ", null, false));
                segmenter.Add(TolkTabellSomFlatTekst(child, kontekst));
            }
            else if (child.Name == "table")
            {
                // Bekreftet ekte og OVERRASKENDE VANLIG i forskrift-korpuset (442 av 5882 dokumenter i
                // full korpusgjennomgang 2026-08-21 — gebyr-/pensjonssatser, tekniske spesifikasjoner)
                // — selve tabellinnholdet ER en del av den gjeldende normen, derfor flates den ut til
                // lesbar tekst i stedet for å hoppes over (se HentTabellSegmenter).
                if (segmenter.Count > 0) segmenter.Add(new TekstSegment(" ", null, false));
                segmenter.AddRange(HentTabellSegmenter(child, kontekst));
            }
            else if (child.Name == "img")
            {
                // Bekreftet ekte (32 av 5882 dokumenter) — img har konsekvent en REELT beskrivende
                // alt-tekst (f.eks. "Illustrasjon som viser hvordan målene X og L ... skal måles"), ikke
                // en tom/dekorativ streng. Selve bildet kan ikke representeres i en flat tekstmodell,
                // men alt-teksten er reelt normativt innhold (illustrerer en måleregel) — tas derfor med
                // som synlig tekst i stedet for å hoppes over eller kaste.
                var alt = child.GetAttributeValue("alt", "");
                if (alt.Length > 0) segmenter.Add(new TekstSegment($"[bilde: {HtmlEntity.DeEntitize(alt)}]", null, false));
            }
            else if (child.Name == "div" && klasse.Contains("latexBlock"))
            {
                // Bekreftet ekte (37 av 5882 dokumenter — matematiske formler, f.eks. justeringsfaktorer
                // i en referanseindeks-forskrift). Selve LaTeX-KILDETEKSTEN ("$$F_i = Min(...)$$") er
                // fortsatt lesbar som tekst for en fagperson, selv urendret — transparent gjennomgang i
                // stedet for å kaste, samme filosofi som resten av modellen (flat, søkbar tekst > ingenting).
                segmenter.AddRange(HentSegmenter(child, kontekst));
            }
            else if (child.Name == "article" && klasse.Contains("changesToParent"))
            {
                // endringshistorikk, ikke løpetekst
            }
            else if (child.Name == "article" && (ErLeddKlasse(klasse) || ErAvsnittKlasse(klasse)))
            {
                // En fotnote (ParseFotnoter kaller HentSegmenter direkte på <article class="footnote">)
                // kan selv bestå av FLERE strukturerte "ledd"/avsnitt-barn (footnoteLegalP/footnoteDefaultP)
                // i stedet for ren inline-tekst — bekreftet ekte, personopplysningsloven (kastet tidligere
                // "Ukjent inline-element <article class=\"legalP\"> i løpetekst"). Transparent rekursjon,
                // med samme mellomrom-skille som ved en hoppet-over liste, slik at to etterfølgende
                // "ledd" i samme fotnote ikke smelter sammen uten mellomrom.
                if (segmenter.Count > 0) segmenter.Add(new TekstSegment(" ", null, false));
                segmenter.AddRange(HentSegmenter(child, kontekst));
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

    /// <summary>
    /// Flater ut en &lt;table&gt; til lesbar løpetekst: hver rad blir en "linje" (skilt med mellomrom —
    /// Tekst-feltet er ren løpetekst uten et eget linjeskift-konsept, jf. §3.4), celler innad i en rad
    /// skilt med " | ". &lt;caption&gt; blir en egen innledende "linje" om den finnes. Ingen forsøk på å
    /// bevare colspan/rowspan visuelt eller kolonnejustering — bare selve INNHOLDET, i dokumentrekke-
    /// følge, som er det som faktisk trengs for søk/lesing av normen (samme flate tekstfilosofi som
    /// resten av modellen bygger på). En celle kan selv inneholde vanlige inline-elementer (lenker,
    /// &lt;br&gt; osv.) — HentSegmenter kalles rekursivt per celle, inkludert en evt. nestet &lt;table&gt;.
    /// </summary>
    private static List<TekstSegment> HentTabellSegmenter(HtmlNode table, ReferanseKontekst? kontekst)
    {
        var segmenter = new List<TekstSegment>();

        var caption = table.SelectSingleNode("./caption");
        if (caption is not null)
        {
            segmenter.AddRange(HentSegmenter(caption, kontekst));
        }

        foreach (var rad in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            if (segmenter.Count > 0) segmenter.Add(new TekstSegment(" ", null, false));
            var forsteCelle = true;
            foreach (var celle in rad.SelectNodes("./th|./td") ?? Enumerable.Empty<HtmlNode>())
            {
                if (!forsteCelle) segmenter.Add(new TekstSegment(" | ", null, false));
                forsteCelle = false;
                segmenter.AddRange(HentSegmenter(celle, kontekst));
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

    /// <summary>
    /// Tabeller (avgiftssatser, lønnstrinn, tekniske spesifikasjoner) er OVERRASKENDE VANLIG i eldre og
    /// tekniske forskrifter — bekreftet ekte, 442 av 5882 dokumenter i full korpusgjennomgang 2026-08-21
    /// (langt den enkeltstørste gjenværende parse-feilen). Modellen har ikke noe eget "tabell"-konsept
    /// (RettskildeNode.Tekst er en flat streng, §2 i teknisk design) — å kaste her ville gjort HELE
    /// dokumentet uimporterbart bare fordi ÉN tabell forekommer et sted i det. Flates derfor ut til
    /// lesbar tekst i stedet: "[Bildetekst: ]kolonne1 | kolonne2 …\nrad1kolonne1 | rad1kolonne2 …\n…".
    /// Bevisst forenklet, dokumentert avveining — IKKE en perfekt gjengivelse av opprinnelig
    /// tabellstruktur (kryssreferanser INNI en tabellcelle spores fortsatt korrekt via den vanlige
    /// HentSegmenter-rekursjonen på hver celle, men returneres her som ETT sammenslått segment, så en
    /// slik referanse ville ikke fått egen klikkbar tekstposisjon i UI-et — ingen bekreftet ekte
    /// forekomst av dette i korpuset, alle sett så langt er rene tall-/tekst-tabeller uten lenker).
    /// </summary>
    private static TekstSegment TolkTabellSomFlatTekst(HtmlNode table, ReferanseKontekst? kontekst)
    {
        var rader = new List<string>();

        var caption = table.SelectSingleNode("./caption");
        if (caption is not null)
        {
            var capTekst = string.Concat(HentSegmenter(caption, kontekst).Select(s => s.Tekst)).Trim();
            if (capTekst.Length > 0) rader.Add(capTekst + ":");
        }

        foreach (var rad in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var celler = (rad.SelectNodes("./th|./td") ?? Enumerable.Empty<HtmlNode>())
                .Select(c => string.Concat(HentSegmenter(c, kontekst).Select(s => s.Tekst)).Trim())
                .Where(t => t.Length > 0);
            var radTekst = string.Join(" | ", celler);
            if (radTekst.Length > 0) rader.Add(radTekst);
        }

        return new TekstSegment(string.Join("\n", rader), null, false);
    }
}
