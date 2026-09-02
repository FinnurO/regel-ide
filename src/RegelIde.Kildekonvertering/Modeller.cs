namespace RegelIde.Kildekonvertering;

/// <summary>
/// Node-typer per docs/08-byggesteg1-teknisk-design.md §2 (rettskilde_noder.node_type).
/// Enum-navnene (lowercased) matcher DB-verdiene direkte, se <see cref="NodeTypeExtensions.TilDbVerdi"/>.
/// </summary>
public enum NodeType
{
    Kapittel,
    Underinndeling,
    Paragraf,
    Ledd,
    Punkt,
}

public static class NodeTypeExtensions
{
    public static string TilDbVerdi(this NodeType type) => type.ToString().ToLowerInvariant();
}

public enum Kildetype
{
    Lov,
    Forskrift,
}

/// <summary>En fotnote knyttet til en paragraf, modellert som AKN &lt;authorialNote&gt; (§3.2 i teknisk design), atskilt fra hovedteksten.</summary>
public sealed record Fotnote(string Etikett, string Tekst);

/// <summary>
/// Én tekstflate innenfor et ledd/punkt: enten ren tekst, eller en intern/ekstern kryssreferanse
/// (fra &lt;a href="lov/…"&gt; i løpeteksten). Brukes til å bygge både ren søketekst (§ i tekst_hash)
/// og AKN-serialiseringens &lt;ref&gt;-elementer (§1.3) fra samme kilde, uten å hente ut teksten to ganger.
/// </summary>
public sealed record TekstSegment(string Tekst, string? ReferanseTilEid, bool ErInternReferanse);

/// <summary>
/// Rå kryssreferanse funnet i løpeteksten til en ledd/punkt-node (§3.1 steg 6).
/// <see cref="TilEid"/> er en best-effort-konstruksjon (§1.2s deterministiske utvidelse) —
/// hvorvidt målet allerede finnes i biblioteket eller må opprettes som referanse-stub er en
/// beslutning som krever databasetilgang, og er derfor bevisst utenfor denne rene pipelinens scope
/// (se docs/06-veikart.md byggesteg 1 og 08-byggesteg1-teknisk-design.md §3.1 steg 6).
/// </summary>
public sealed record RettskildeReferanse(
    string FraNodeEid,
    string TilEid,
    bool ErInternReferanse,
    Kildetype? TilKildetype,
    string? TilDatokode,
    int? TekstStart = null,
    int? TekstLengde = null
);

public sealed record RettskildeNode
{
    public required string Eid { get; init; }
    public string Kildesystem { get; init; } = "lovdata";
    public required string KildeId { get; init; }
    public string? ParentEid { get; init; }
    public required NodeType NodeType { get; init; }
    public string? Nummer { get; init; }
    public string? Overskrift { get; init; }

    /// <summary>Kun for ledd/punkt-noder (bladtekst) — ren tekst, tagger fjernet. Se §2 i teknisk design.</summary>
    public string? Tekst { get; init; }

    /// <summary>SHA-256 av normalisert tekst, §3.4. Null for noder uten Tekst.</summary>
    public string? TekstHash { get; init; }

    public required int SorteringsRekkefolge { get; init; }

    /// <summary>(Opphevet)-paragraf, §3.2 — noden produseres alltid, aldri hoppet over.</summary>
    public bool Opphevet { get; init; }
    public DateOnly? OpphevetDato { get; init; }

    /// <summary>Kun relevant for paragraf-noder — fotnoter tilhørende denne paragrafen.</summary>
    public IReadOnlyList<Fotnote> Fotnoter { get; init; } = [];

    /// <summary>
    /// Tekstsegmentene som Tekst/TekstHash er avledet fra. Bevares for AKN-serialisering
    /// slik at interne kryssreferanser kan gjenskapes som &lt;ref&gt; (§1.3) uten å re-parse HTML.
    /// Null for noder uten løpetekst (kapittel/underinndeling/opphevet paragraf).
    /// </summary>
    public IReadOnlyList<TekstSegment>? Segmenter { get; init; }
}

public sealed record RettskildeMetadata
{
    public required Kildetype Kildetype { get; init; }
    public string Doctype { get; init; } = "act";
    public required string Tittel { get; init; }
    public string? Kortnavn { get; init; }

    /// <summary>Verifisert, ekstern ELI-URI på lovnivå — kanonisk rot for eId, §1.2 (låst).</summary>
    public required string Eli { get; init; }

    public required string Datokode { get; init; }
    public DateOnly? Ikrafttredelse { get; init; }

    /// <summary>
    /// [Ny, 2026-09-02] Rå, UTRUNKERT verdi av header-feltet <c>&lt;dt class="dateInForce"&gt;</c>,
    /// ved siden av (ikke i stedet for) <see cref="Ikrafttredelse"/>. <see cref="Ikrafttredelse"/>
    /// beholder kun FØRSTE dato-treff (LovdataHtmlParser.FørsteDato) — kompound-verdier som
    /// "01.06.2026, Kongen bestemmer" eller "01.07.2026, 15.09.2026" trunkeres der stille til én dato.
    /// Dette feltet bevarer HELE den opprinnelige strengen uendret, slik at den tapte informasjonen
    /// (betingelse/andre datoer) ikke går tapt for godt.
    /// </summary>
    public string? IkrafttredelseRaa { get; init; }

    public DateOnly? KonsolidertDato { get; init; }

    /// <summary>[Ny, 2026-09-02] Rå, UTRUNKERT verdi av header-feltet <c>&lt;dt class="lastChangeInForce"&gt;</c> —
    /// samme begrunnelse som <see cref="IkrafttredelseRaa"/>, bare for <see cref="KonsolidertDato"/>.</summary>
    public string? KonsolidertDatoRaa { get; init; }

    /// <summary>
    /// [Ny, 2026-09-02] Header-feltet <c>&lt;dt class="lastChangedBy"&gt;Sist endret ved&lt;/dt&gt;</c> —
    /// hvilken lov/forskrift (og dato) som SIST endret DETTE dokumentet. Ikke fanget før nå. Rå tekst
    /// (typisk en lenkes synlige tekst, f.eks. "lov/2024-06-21-46") — ingen strukturert kobling til en
    /// annen rettskilde forsøkt her, i motsetning til <see cref="RettskildeEndring"/> (som ER
    /// strukturert, men dekker den MOTSATTE relasjonen — hva DETTE dokumentet endrer, ikke hva som sist
    /// endret det).
    /// </summary>
    public string? SistEndretVed { get; init; }

    public string Utgiver { get; init; } = "Lovdata";
    public required string AnsvarligDepartement { get; init; }

    /// <summary>FRBRauthor-href: 'stortinget' for Lov, avledet fra departement for Forskrift (Vedlegg A.1).</summary>
    public required string FrbrAuthorHref { get; init; }
    public required string FrbrAuthorShowAs { get; init; }

    public string Status { get; init; } = "Gjeldende";
}

/// <summary>
/// Én rad i header-metadatafeltet <c>&lt;dt class="basedOn"&gt;Hjemmel&lt;/dt&gt;</c> (bekreftet ekte,
/// data/kilder/raw-lovdata/alkoholforskriften-FOR-2005-06-08-538.html) — hvilken paragraf i hvilken
/// lov dokumentet (typisk en forskrift) er hjemlet i. DOKUMENTNIVÅ-metadata, bevisst atskilt fra
/// <see cref="RettskildeReferanse"/> (som er per-NODE løpetekst-kryssreferanser, §3.1 steg 6, en helt
/// annen kilde/mekanisme — se LovdataHtmlParser sin ParseMetadata-kommentar for full begrunnelse).
/// <para>
/// <see cref="Eid"/> er i NØYAKTIG samme paragraf-eId-format som <see cref="RettskildeNode.Eid"/> og
/// <see cref="RettskildeReferanse.TilEid"/> (<c>"{lov-eli}/§X-Y"</c>, se
/// <see cref="LovdataIdentifikatorer.ParagrafEid"/>) — bevisst gjenbrukt format, ikke oppfunnet på
/// nytt, slik at klientens allerede etablerte eId→lenke-oppslag (eidLenker.ts/rettskildeLenke)
/// fungerer uendret for hjemmel-referanser også.
/// </para>
/// </summary>
public sealed record RettskildeHjemmel(string Eid, int Sorteringsrekkefolge);

/// <summary>
/// Én rad i header-metadatafeltet <c>&lt;dt class="changesToDocuments"&gt;Endrer&lt;/dt&gt;</c> —
/// hvilke(t) andre dokument(er) DENNE rettskilden ENDRER (2026-09-02). Strukturelt identisk med
/// <see cref="RettskildeHjemmel"/> (samme href-tolkning, samme referanse-stub-mekanisme ved import),
/// men en semantisk MOTSATT relasjon («hjemlet i» vs. «endrer») — derfor en egen type/tabell, ikke
/// gjenbruk av <see cref="RettskildeHjemmel"/> selv.
/// <para>
/// <see cref="Eid"/> her er, TIL FORSKJELL FRA <see cref="RettskildeHjemmel.Eid"/>, ALLTID en
/// dokument-ELI (aldri en paragraf-eId): «Endrer»-feltet er bekreftet ekte i 5 av 8 fixturer
/// (alkoholforskriften/alkoholloven/personopplysningsloven/serveringsloven/tannhelsetjenesteloven,
/// gjennomgang 2026-09-02), og ALLE bekreftede forekomster peker på et HELT dokument
/// ("lov/1927-04-05", "forskrift/1997-12-11-1292"), aldri én bestemt paragraf i det — motsatt av
/// Hjemmel-feltets ene bekreftede forekomst, som alltid HAR et paragrafnummer. Se
/// LovdataHtmlParser.HentEndringer for hvorfor en (ubekreftet) Endrer-lenke MED paragrafnummer kaster
/// i stedet for å gjette betydningen («ingen gjettet fallback», §3.3).
/// </para>
/// </summary>
public sealed record RettskildeEndring(string Eid, int Sorteringsrekkefolge);

public sealed record KonverteringResultat
{
    public required RettskildeMetadata Metadata { get; init; }
    public required IReadOnlyList<RettskildeNode> Noder { get; init; }
    public required IReadOnlyList<RettskildeReferanse> Referanser { get; init; }
    public required IReadOnlyList<RettskildeHjemmel> Hjemler { get; init; }

    /// <summary>[Ny, 2026-09-02] Se <see cref="RettskildeEndring"/> — dokumenter DENNE rettskilden endrer.</summary>
    public required IReadOnlyList<RettskildeEndring> Endringer { get; init; }

    public required string AknXml { get; init; }
    public required DateOnly ImportDato { get; init; }

    /// <summary>
    /// [Ny, 2026-09-02, del B av lovdata-raa-metadata-runden] Den rå, UTF-8-dekodede kilde-HTML-en
    /// <see cref="LovdataKonverterer.Konverter"/> selv mottok (samme streng som
    /// <c>kildeHtmlUtf8</c>-parameteren), bevart uendret gjennom hele pipelinen. FØR denne runden ble
    /// den rå HTML-en kastet umiddelbart etter parsing — ingen bit-identisk original ble noensinne
    /// lagret for en Lovdata-importert rettskilde (til forskjell fra kommunal-nettside-sporet, se
    /// RettskildeEntitet.Innhold). Konsumeres av RettskildeImportTjeneste til å populere
    /// Url/Innhold/InnholdsHash/Hentet — se den klassens kommentarer for hvorfor disse ALLEREDE
    /// eksisterende, men til nå Lovdata-bevisst-NULL-holdte feltene nå også fylles ut her.
    /// </summary>
    public required string RaaHtml { get; init; }
}
