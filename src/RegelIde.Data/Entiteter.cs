namespace RegelIde.Data;

/// <summary>
/// EF Core-entiteter som speiler docs/08-byggesteg1-teknisk-design.md §2 — feltnavn, typer og
/// constraints er låst der etter tre QA-runder. Avvik markert eksplisitt: multi-virksomhet-
/// refaktoreringen (docs/00-endringslogg-v0.3.md, 2026-07-24) la til <see cref="Virksomhet"/> og
/// virksomhet_id-feltene under, som ikke var del av det opprinnelige låste skjemaet.
/// </summary>
public sealed class Virksomhet
{
    public Guid Id { get; set; }
    public required string Navn { get; set; }
    public string? Organisasjonsnummer { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }

    /// <summary>
    /// [LÅST — avklaringsrunde 1, 2026-08-12, docs/15-handbok-dokumentgraf-notat.md §3.3/§11] Nullbart
    /// geografisk/statistisk attributt — statlige/regionale virksomheter (Digdir, et direktorat, en
    /// statsforvalter) har ingen. ALDRI i en AKN/ELI-URI: kommunenummer er ikke stabilt over tid
    /// (Bergen var 1201 før 2020, 4601 etter — samme organ, samme bystyre, nytt nummer).
    /// <see cref="Organisasjonsnummer"/> (allerede stabilt) bærer URI-nøkkelen i stedet.
    /// </summary>
    public string? Kommunenummer { get; set; }

    /// <summary>'stat' | 'fylke' | 'kommune' — styrer hvilket organ som er vedtaksmyndighet
    /// (bystyre/kommunestyre/fylkesting), se §3.3.</summary>
    public string? Forvaltningsniva { get; set; }
}

/// <summary>
/// Lagt til 2026-07-24 (samme runde som GUI-arbeidet) — en enkel testbruker-modell, IKKE ekte
/// autentisering. Erstattes av Ansattporten-innlogging senere uten at denne tabellen forsvinner:
/// en ekte innlogget bruker vil fortsatt trenge en rad her (navn, virksomhet, rolle), bare med
/// identiteten hentet fra et Ansattporten-claim i stedet for en GUI-nedtrekksliste.
/// </summary>
public sealed class Bruker
{
    public Guid Id { get; set; }
    public required string Navn { get; set; }
    public Guid VirksomhetId { get; set; }

    /// <summary>Se RBAC-matrisen i docs/03-domenemodell.md §2: 'Fagansvarlig' | 'Jurist' | 'Systemforvalter' | 'Saksbehandler'.</summary>
    public required string Rolle { get; set; }

    /// <summary>
    /// Altinn-bruker-id (claim <c>urn:altinn:userid</c>) for rader som er opprettet ved
    /// innlogging. NULL for de seedede testbrukerne, som ikke svarer til noen ekte identitet.
    /// Unik der den er satt, slik at gjentatte innlogginger treffer samme rad.
    /// </summary>
    public string? AltinnBrukerId { get; set; }
}

public sealed class RettskildeEntitet
{
    public Guid Id { get; set; }

    /// <summary>
    /// NULL = delt/nasjonal rettskilde (Lov/Forskrift fra Lovdata — importeres og vises likt for
    /// alle virksomheter, aldri duplisert per virksomhet). Satt = virksomhetens egen lokale kilde
    /// (lokal forskrift, virksomhetsdokument) — kun synlig for og eid av denne virksomheten.
    /// Se docs/00-endringslogg-v0.3.md for begrunnelsen (opptil 1000 offentlige virksomheter —
    /// duplisering av delte nasjonale kilder per virksomhet ville vært både kostbart og feilutsatt
    /// ved lovendringer, som da måtte vedlikeholdes N ganger i stedet for én).
    /// </summary>
    public Guid? VirksomhetId { get; set; }

    public required string Doctype { get; set; } // 'act' | 'doc' | 'judgment' | 'internal'
    public required string Kildetype { get; set; } // 'Lov' | 'Forskrift' | 'Rundskriv' | 'Presedens' | 'Virksomhetsdokument'
    public string Importrolle { get; set; } = "primaer"; // 'primaer' | 'referanse'
    public required string Tittel { get; set; }
    public string? Kortnavn { get; set; }
    public string? Eli { get; set; }
    public string? AknXml { get; set; } // NULL for referanse-stubber
    public DateOnly? Ikrafttredelse { get; set; }
    public DateOnly? KonsolidertDato { get; set; }
    public string? Utgiver { get; set; }
    public required string Status { get; set; } // 'Gjeldende' | 'Opphevet' | 'Utkast'
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public Guid? ErstatterId { get; set; }
    public DateOnly? GyldigFra { get; set; }
    public DateOnly? GyldigTil { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }

    // ---------- Lag 1 (docs/15-handbok-dokumentgraf-notat.md §2/§8 Trinn 1) — hentet, bitidentisk
    // original + endringsdeteksjon for kilder som KUN finnes på et kommunalt nettsted. Alle nullable:
    // irrelevante for delt/nasjonal Lovdata-import (Url/Innhold forblir NULL der, samme mønster som
    // AknXml er NULL for referanse-stubber).

    /// <summary>Eksakt URL kilden ble hentet fra — finnes ikke for Lovdata-import (annen henteflyt).</summary>
    public string? Url { get; set; }

    /// <summary>Bytea — uendret original (typisk PDF) for et hentet dokument. Distinkt fra
    /// <see cref="AknXml"/>, som er en AVLEDET serialisering, ikke originalen selv.</summary>
    public byte[]? Innhold { get; set; }

    /// <summary>SHA-256 over <see cref="Innhold"/> — den ENESTE versjoneringsmekanismen som finnes for
    /// et dokument som bare ligger på kommunens nettside (§2 Lag 1).</summary>
    public string? InnholdsHash { get; set; }

    public DateTimeOffset? Hentet { get; set; }
    public string? HttpEtag { get; set; }
    public string? HttpLastModified { get; set; }

    // ---------- RettsligStatus, splittet i to ortogonale akser (§3.3, [LÅST] avklaringsrunde 1
    // 2026-08-12) — ett felt kan ikke bære både normativ kraft OG funksjonell rolle.

    /// <summary>AKSE A — populeres denne runden: 'bindende_borger' | 'bindende_forvaltning' |
    /// 'vektbaerende' | 'faktisk_praksis'. [PÅ AVKLARING, §13] om 'bindende_forvaltning' er riktig
    /// snitt for retningslinjer/innbyggerveiledere generelt (Schartum-spørsmålet) — IKKE avgjort,
    /// derfor bevisst ikke gjort obligatorisk i skjemaet ennå, selv om §3.3 sier feltet "må være
    /// obligatorisk" når taksonomien er ferdig avklart.</summary>
    public string? NormativVirkning { get; set; }

    /// <summary>AKSE B — feltet finnes, forblir nullable til delegasjonsreglement-arbeidet starter (§3.3):
    /// 'materiell_norm' | 'kompetansenorm' | 'prosessnorm' | 'gebyr_okonomi' | 'tolkning'. IKKE
    /// populert av <see cref="HandbokTekstParser"/> denne runden (bevisst utsatt, se §13).</summary>
    public string? FunksjonellRolle { get; set; }

    /// <summary>"SD-24-113" — les fra dokumentet når det finnes.</summary>
    public string? InterntDokNr { get; set; }

    /// <summary>"01".</summary>
    public string? Revisjonsnr { get; set; }

    /// <summary>"Bystyret".</summary>
    public string? VedtattAv { get; set; }

    public DateOnly? Vedtaksdato { get; set; }

    /// <summary>Bystyresak, når den finnes.</summary>
    public string? Saksnummer { get; set; }

    /// <summary>"alkoholloven/§1-7d".</summary>
    public string? HjemmelEid { get; set; }

    // §3.3s "GyldigTil" (2028-07-01) er BEVISST IKKE en ny kolonne — <see cref="GyldigTil"/> lenger
    // opp i denne klassen fantes allerede (nasjonale rettskilder) og gjenbrukes uendret, nøyaktig som
    // §3.3 selv ber om å sjekke ("Ikrafttredelse/KonsolidertDato finnes allerede ... sjekk om de kan
    // gjenbrukes/utvides"). Ingen duplikatkolonne.

    public List<RettskildeNodeEntitet> Noder { get; set; } = [];
}

public sealed class RettskildeNodeEntitet
{
    public Guid Id { get; set; }
    public Guid RettskildeId { get; set; }
    public required string Eid { get; set; } // canonical_id — endres aldri
    public string Kildesystem { get; set; } = "lovdata";
    public required string KildeId { get; set; } // source_id
    public string? OffisiellEli { get; set; } // nullable — §1.2, fylles ut hvis Lovdata publiserer seksjons-ELI
    public Guid? ParentNodeId { get; set; }
    public required string NodeType { get; set; } // 'kapittel' | 'underinndeling' | 'paragraf' | 'ledd' | 'punkt'
    public string? Nummer { get; set; }
    public string? Overskrift { get; set; }
    public string? Tekst { get; set; } // kun ledd/punkt (bladtekst)
    public string? TekstHash { get; set; }

    /// <summary>
    /// Flyttet gjennom fra Lovdatas data-repealeddate (§3.2 i teknisk design) — fantes fra før i
    /// RegelIde.Kildekonvertering (Modeller.cs) og brukes til AKN-XML-en, men ble aldri lagret på
    /// noden selv før nå (2026-07-24). En opphevet paragraf produseres alltid som en node (aldri
    /// hoppet over), men skal ikke tagges (TekstTaggTjeneste avviser det) og kan etter hvert skjules.
    /// </summary>
    public bool Opphevet { get; set; }
    public DateOnly? OpphevetDato { get; set; }

    public int Sorteringsrekkefolge { get; set; }

    /// <summary>
    /// Node-nivå versjonering (2026-07-26, se docs/03-domenemodell.md §1.1.1 og
    /// docs/08-byggesteg1-teknisk-design.md §2.1) — kun i bruk for håndbok/rundskriv-noder.
    /// Lov/Forskrift-noder regenereres fortsatt synkront ved reimport (Vedlegg A.5/A.6) og forblir
    /// alltid Versjon=1/Entitetsstatus="gjeldende"/ErstatterNodeId=null.
    /// </summary>
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public Guid? ErstatterNodeId { get; set; }

    /// <summary>Navigasjonsegenskap til 1:1-metadataraden — kun satt (via Include) for håndbok-kommentarseksjoner.</summary>
    public HandbokKommentarMetadataEntitet? HandbokMetadata { get; set; }
}

/// <summary>
/// 1:1-utvidelse av <see cref="RettskildeNodeEntitet"/> for håndbok/rundskriv-kommentarseksjoner
/// (docs/03-domenemodell.md §1.1.1, "Presisert 2026-07-26"). Egen tabell fordi dokumenttype/bindende
/// er en egenskap PER KOMMENTAR-NODE, ikke per håndbok — og fordi Lov/Forskrift-noder ellers ville fått
/// en rekke alltid-NULL håndbok-spesifikke kolonner på den delte rettskilde_noder-tabellen.
/// </summary>
public sealed class HandbokKommentarMetadataEntitet
{
    public Guid NodeId { get; set; }

    /// <summary>'kommentar' | 'retningslinje' | 'instruks' | 'handbok'.</summary>
    public required string Dokumenttype { get; set; }

    /// <summary>
    /// Utledet av <see cref="Dokumenttype"/> (kommentar=false, de tre andre=true) — settes av
    /// HandbokForfatterTjeneste, aldri fritt av klienten (AK-3.3.11).
    /// </summary>
    public bool Bindende { get; set; }

    /// <summary>'kapittel' | 'bestemmelse' | 'ledd' | 'bokstav' — detaljnivå kommentaren er festet på.</summary>
    public required string FesteNiva { get; set; }

    /// <summary>'under_arbeid' | 'til_godkjenning' | 'publisert' | 'ma_revideres'.</summary>
    public required string Status { get; set; }

    /// <summary>Utfylt når Status='ma_revideres' — v1-forenkling: manuell merking, ikke automatisk påvirkningsanalyse (byggesteg 8).</summary>
    public string? Revisjonsgrunn { get; set; }

    public DateOnly? Publisert { get; set; }
    public DateOnly? SistFagligEndret { get; set; }

    /// <summary>Lokal innholdsliste for lange kommentarer. Tom i v1 (ikke i forfatter-UI-et ennå).</summary>
    public string UnderoverskrifterJson { get; set; } = "[]";

    /// <summary>Stikkordindeks (Skatteetaten-mønster).</summary>
    public List<string> Marginord { get; set; } = [];

    /// <summary>Presedensreferanser med rettskildevekt. Alltid tom — forutsetter Presedensregisteret (byggesteg 3), ikke bygget ennå.</summary>
    public string PraksisJson { get; set; } = "[]";
}

/// <summary>
/// 1:1-utvidelse av <see cref="RettskildeNodeEntitet"/> med et embedding-vektor for RAG-spiken
/// (byggesteg 5 runde 4, docs/14-byggesteg5-teknisk-design.md) — samme "egen tabell, ikke en alltid-
/// NULL-kolonne på den delte rettskilde_noder-tabellen"-begrunnelse som
/// <see cref="HandbokKommentarMetadataEntitet"/>, siden embeddings kun beregnes lazy for noder som
/// faktisk er brukt i en RAG-kontekstbygging. Bevisst en vanlig <c>double precision[]</c>-kolonne, IKKE
/// pgvector — se docs/14 §RAG-spike for begrunnelsen (ingen ny NuGet-avhengighet, embedded-test-
/// Postgres-en har ikke extension-en forhåndskompilert). Kosinelikhet beregnes i C# ved henting
/// (<see cref="RagKontekstHjelper"/>), ikke i databasen.
/// </summary>
public sealed class RettskildeNodeEmbeddingEntitet
{
    public Guid NodeId { get; set; }
    public required List<double> Embedding { get; set; }

    /// <summary>Hvilken embeddings-modell vektoren stammer fra — en fremtidig modellbytte gjør gamle
    /// rader ikke-sammenlignbare med nye, men v1 av spiken har ingen automatisk re-embedding/
    /// invalidering (se docs/13-backlog.md), kun dette feltet til manuell diagnose.</summary>
    public required string Modell { get; set; }

    public DateTimeOffset OpprettetTidspunkt { get; set; }
}

public sealed class RettskildeReferanseEntitet
{
    public Guid Id { get; set; }
    public Guid FraNodeId { get; set; }
    public Guid TilRettskildeId { get; set; }
    public required string TilEid { get; set; }

    /// <summary>
    /// 'import' (auto-fanget fra Lovdatas egne kryssreferanse-lenker under import) eller 'manuell'
    /// (lagt til av en bruker via referanser-UI-et, 2026-07-30). Kilde-referanser er skrivebeskyttet —
    /// se <see cref="HandbokForfatterTjeneste.FjernLovreferanseAsync"/>.
    /// </summary>
    public string Opprinnelse { get; set; } = "import";

    /// <summary>
    /// Posisjon (tegn-offset/lengde) for referansens synlige tekst i FraNode sin <c>Tekst</c> —
    /// gjør referansen klikkbar INNI selve løpeteksten (2026-07-30), ikke bare i den separate
    /// referanse-lista. Null for manuelt lagte referanser (peker ikke på noe bestemt tekstutdrag) og
    /// for de fåtallige import-referansene der parseren ikke fant et entydig treff.
    /// </summary>
    public int? TekstStart { get; set; }
    public int? TekstLengde { get; set; }
}

/// <summary>
/// Global (ikke virksomhets-scopet) konfigurasjon av hvilke tag-kinds som finnes — erstatter en
/// tidligere hardkodet liste (2026-07-25). Bevisst ikke en generisk nøkkel/verdi-Settings-ramme ennå,
/// kun denne ene konkrete tabellen — utvides til noe bredere når flere konfigurerbare ting faktisk
/// dukker opp.
/// </summary>
public sealed class TaggKindKonfigurasjonEntitet
{
    public Guid Id { get; set; }
    public required string Kode { get; set; } // 'begrep' | 'tjeneste' | 'vilkar' | 'regel' | ... (utvidbart)
    public required string Navn { get; set; } // "Begrep"
    public required string Farge { get; set; } // Designsystemet-fargerolle: accent/info/warning/success/...
    public int Sorteringsrekkefolge { get; set; }
    public bool Aktiv { get; set; } = true;
}

public sealed class TekstTaggEntitet
{
    public Guid Id { get; set; }

    /// <summary>
    /// Ikke nullable, i motsetning til RettskildeEntitet.VirksomhetId — en tagg er alltid en
    /// virksomhets eget arbeidsprodukt, selv når den peker på en delt/nasjonal rettskilde. To
    /// virksomheter kan tagge samme lovparagraf ulikt (forskjellige vilkår/begreper), så taggen
    /// arver ikke synlighet fra RettskildeId.
    /// </summary>
    public required Guid VirksomhetId { get; set; }

    public Guid RettskildeId { get; set; }
    public required string NodeEid { get; set; }
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public required string QuotePrefix { get; set; }
    public required string QuoteExact { get; set; }
    public required string QuoteSuffix { get; set; }
    public required string NodeTekstHash { get; set; }
    public required string Kind { get; set; } // 'begrep' | 'tjeneste' | 'vilkar' | 'regel'
    public Guid? RefId { get; set; } // nullable inntil byggesteg 2/4
    public string Entitetsstatus { get; set; } = "gjeldende";
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }

    /// <summary>
    /// quoteSelector-relokering ved reimport (2026-07-29, docs/05-arkitektur-og-nfk.md §3.1) — satt til
    /// true når <see cref="RettskildeImportTjeneste"/> ikke klarte å finne et entydig treff for
    /// <see cref="QuoteExact"/> i en ny rettskilde-versjon (verken samme eid+uendret tekst_hash, eller
    /// nøyaktig ett substring-treff). Taggen forblir da koblet til den nå 'erstattede' gamle raden —
    /// sitatkonteksten er fortsatt inspiserbar, men peker ikke lenger på gjeldende tekst.
    /// </summary>
    public bool KreverGjennomgang { get; set; }
}

/// <summary>
/// Tjeneste (CPSV-AP-NO, docs/03-domenemodell.md §1.5) — byggesteg 2. Samme basemetadata-mønster som
/// <see cref="RettskildeEntitet"/> og samme 5-verdis statusløp (§3.1: utkast → under_revisjon →
/// validert → publisert → tilbaketrukket/arkivert), IKKE Rettskildes enklere 3-verdis status.
/// <see cref="Hendelser"/>/<see cref="Tjenesteavhengigheter"/> lagres som jsonb-lister (samme mønster
/// som <see cref="HandbokKommentarMetadataEntitet.UnderoverskrifterJson"/>) — verdiobjekter uten egen
/// queryable rad, siden ingen skjerm trenger inkrementell CRUD på enkeltelementer (i motsetning til
/// <see cref="KodelisteKodeEntitet"/>, der "Ny kode"-knappen krever nettopp det).
/// </summary>
public sealed class TjenesteEntitet
{
    public Guid Id { get; set; }

    /// <summary>Påkrevd (§0.1) — en tjeneste er alltid virksomhetens eget arbeidsprodukt, aldri delt.</summary>
    public required Guid VirksomhetId { get; set; }

    public required string Tittel { get; set; }
    public string? Beskrivelse { get; set; }
    public string? KompetentMyndighet { get; set; }
    public string? Output { get; set; }
    public string? Tjenestetype { get; set; }
    public string? Malgruppe { get; set; }
    public List<string> Kanaler { get; set; } = [];
    public string? Kostnad { get; set; }
    public string? Behandlingstid { get; set; }
    public string? Kontaktpunkt { get; set; }
    public string? KonsekvensVedBrudd { get; set; }
    public List<string> Sprak { get; set; } = [];

    public required string Status { get; set; } // 'utkast' | 'under_revisjon' | 'validert' | 'publisert' | 'tilbaketrukket' | 'arkivert'
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public Guid? ErstatterId { get; set; }
    public DateOnly? GyldigFra { get; set; }
    public DateOnly? GyldigTil { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }

    /// <summary>
    /// Byggesteg 4 — lukker gapet fra byggesteg 2 ("vilkårskobling ... kommer i byggesteg 4",
    /// docs/06-veikart.md). Peker til rotnoden (alltid en Regelnode, INV-5) i tjenestens vilkårstre.
    /// </summary>
    public Guid? RotnodeId { get; set; }
}

/// <summary>Regelverksreferanse fra en Tjeneste til en rettskilde-node — samme form som <see cref="RettskildeReferanseEntitet"/>, egen tabell siden kilden er en Tjeneste, ikke en rettskilde-node.</summary>
public sealed class TjenesteRegelverksreferanseEntitet
{
    public Guid Id { get; set; }
    public Guid TjenesteId { get; set; }
    public Guid TilRettskildeId { get; set; }
    public required string TilEid { get; set; }
}

/// <summary>
/// Hendelse (CPSV <c>cv:Event</c>/<c>cv:LifeEvent</c>/<c>cv:BusinessEvent</c>) — docs/03-domenemodell.md
/// §1.5, korrigert/avklart 2026-07-31 (docs/13-backlog.md §2.1). Et eget, DELT register — samme
/// nasjonal/lokal-mønster som <see cref="RettskildeEntitet.VirksomhetId"/>: <c>null</c> = nasjonal/delt
/// hendelse (f.eks. «Eierskifte»), satt = virksomhetens egen lokale hendelse. En Hendelse er alltid et
/// EKTE, eksternt fenomen som skjer MED en virksomhet (eierskifte, kontroll/tilsyn, brudd, avvikling)
/// — aldri en tjenestes eget resultat/utfall (f.eks. "Bestått Etablererprøve" hører IKKE hjemme her).
/// </summary>
public sealed class HendelseEntitet
{
    public Guid Id { get; set; }
    public Guid? VirksomhetId { get; set; }

    public required string Navn { get; set; }
    public required string Type { get; set; } // 'generell' (cv:Event) | 'livshendelse' (cv:LifeEvent) | 'virksomhetshendelse' (cv:BusinessEvent)
    public string? Beskrivelse { get; set; }

    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }
}

/// <summary>
/// Klassifisering av en Tjeneste ved en Hendelse — ren, symmetrisk mange-til-mange-kobling
/// (<c>cpsv:isClassifiedBy</c>), INGEN lagret retning: to tjenester som deler samme Hendelse blir
/// dermed relaterte uten at én "forårsaker" den andre. Rettede, årsaksforklarte koblinger er
/// <see cref="TjenesteavhengighetEntitet"/>, et annet konsept.
/// </summary>
public sealed class TjenesteHendelseEntitet
{
    public Guid Id { get; set; }
    public required Guid TjenesteId { get; set; }
    public required Guid HendelseId { get; set; }
}

/// <summary>
/// Tjenesteavhengighet (docs/03-domenemodell.md §1.5) — én RETTET kant <c>FraTjenesteId → TilTjenesteId</c>
/// per relasjon (aldri to speilbilde-rader, se domenemodellens presisering 2026-07-31 tredje runde).
/// Bevisst løsere lagdeling enn regelgrafen — INGEN FK inn i Vilkårstreet (Vilkår/RegelnodeBarn/Datasett),
/// kun tjeneste-til-tjeneste. <see cref="HendelseId"/> er kun relevant (og valgfri der den er) når
/// <see cref="Rel"/> er <c>"utlost_av"</c> — den eneste rel-verdien som kobles til en konkret Hendelse.
/// </summary>
public sealed class TjenesteavhengighetEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }

    public required Guid FraTjenesteId { get; set; }
    public required Guid TilTjenesteId { get; set; }

    /// <summary>'forutsetning_for' | 'gir_mulighet_til' | 'utlost_av' | 'for' | 'avhengig_av' | 'input_til'.</summary>
    public required string Rel { get; set; }

    /// <summary>Kun satt (og kun meningsfullt) når <see cref="Rel"/> == "utlost_av".</summary>
    public Guid? HendelseId { get; set; }

    /// <summary>Fritekst-nyanse for kjente unntak/forbehold — ikke ment for egen betingelseslogikk (den hører hjemme i Til-tjenestens eget vilkårstre).</summary>
    public string? Beskrivelse { get; set; }

    public string Entitetsstatus { get; set; } = "gjeldende";
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}

/// <summary>
/// Håndbok-nivå rettskildeomfang (2026-07-31, docs/12-fasit-handbok-leveranse.md "Håndbok-nivå
/// rettskildeomfang") — hvilke rettskilder en håndbok som helhet omhandler (f.eks. alkoholloven +
/// alkoholforskriften + kommunens alkoholpolitiske retningslinje + forvaltningsloven), deklarert på
/// håndboken selv (<see cref="HandbokId"/> peker på håndbokens EGEN <see cref="RettskildeEntitet.Id"/>,
/// siden en håndbok IKKE har en egen tabell — den ER en RettskildeEntitet med
/// <c>Kildetype="Rundskriv"</c>, se <see cref="HandbokForfatterTjeneste.OpprettHandbokAsync"/>).
/// Distinkt fra <see cref="RettskildeReferanseEntitet"/>, som knytter et bestemt TEKSTUTDRAG
/// (<see cref="RettskildeReferanseEntitet.FraNodeId"/>) til en bestemt <c>eId</c> — dette feltet
/// deklarerer en hel rettskilde som relevant for håndboken, uten noen bestemt paragraf-presisjon.
/// </summary>
public sealed class HandbokRettskildeomfangEntitet
{
    public Guid Id { get; set; }
    public Guid HandbokId { get; set; }
    public Guid TilRettskildeId { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}

/// <summary>
/// Begrep (SKOS, docs/03-domenemodell.md §1.3) — byggesteg 2. Samme basemetadata/statusløp-mønster
/// som <see cref="TjenesteEntitet"/>.
/// </summary>
public sealed class BegrepEntitet
{
    public Guid Id { get; set; }

    /// <summary>Påkrevd (§0.1) — et begrep er alltid virksomhetens eget arbeidsprodukt.</summary>
    public required Guid VirksomhetId { get; set; }

    public required string Term { get; set; } // skos:prefLabel
    public required string Definisjon { get; set; } // skos:definition
    public string? LovreferanseEid { get; set; } // dct:source — validert mot RettskildeNoder ved lagring
    public List<string> GjelderFor { get; set; } = [];
    public Guid? KodelisteReferanseId { get; set; } // peker til verdiområde (§1.4)
    public string? SkosUrl { get; set; } // publisert URI i Felles datakatalog (data.norge.no)
    public required string Begrepstype { get; set; } // 'faktabegrep' | 'handlingsbegrep' (Schartum 2025 7.3.3-7.3.4)
    public required string Status { get; set; } // 'utkast' | 'under_revisjon' | 'validert' | 'publisert' | 'tilbaketrukket' | 'arkivert'
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public Guid? ErstatterId { get; set; }
    public DateOnly? GyldigFra { get; set; }
    public DateOnly? GyldigTil { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }
}

/// <summary>
/// Kodeliste / verdidomene (docs/03-domenemodell.md §1.4) — byggesteg 2. Tre typer (§0.1): juridisk og
/// teknisk krever <see cref="VirksomhetId"/> (virksomhetens eget arbeidsprodukt); ekstern-referanse er
/// delt/uten virksomhet (refererer en autoritativ kilde, dupliserer ikke). Ekstern-referanse har heller
/// ikke noe 'publisert'-steg (§3.1) — alltid 'gjeldende' så lenge kilden den refererer til er det.
/// </summary>
public sealed class KodelisteEntitet
{
    public Guid Id { get; set; }

    /// <summary>NULL kun for Type='ekstern-referanse' (§0.1) — juridisk/teknisk krever den (virksomhetens eget arbeidsprodukt).</summary>
    public Guid? VirksomhetId { get; set; }

    public required string Kode { get; set; } // f.eks. "KL-VANDELSOMRADE-ALKOHOLLOV"
    public required string Navn { get; set; }
    public required string Type { get; set; } // 'juridisk' | 'teknisk' | 'ekstern-referanse'
    public string? JuridiskGrunnlagEid { get; set; } // kun Type='juridisk'
    public string? EksternKildeUri { get; set; } // kun Type='ekstern-referanse'
    public string? EksternKildeVersjon { get; set; } // kun Type='ekstern-referanse'
    public required string Status { get; set; }
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public Guid? ErstatterId { get; set; }
    public DateOnly? GyldigFra { get; set; }
    public DateOnly? GyldigTil { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }

    public List<KodelisteKodeEntitet> Koder { get; set; } = [];
}

/// <summary>
/// Én kode i en <see cref="KodelisteEntitet"/> — egen tabell (ikke jsonb), siden "Ny kode"
/// (produktkrav kap. 3.7) er en egen, inkrementell brukerhandling per kode.
/// </summary>
public sealed class KodelisteKodeEntitet
{
    public Guid Id { get; set; }
    public Guid KodelisteId { get; set; }
    public required string Kode { get; set; }
    public required string Term { get; set; }
    public string? Definisjon { get; set; }
    public DateOnly? GyldigFra { get; set; }
    public DateOnly? GyldigTil { get; set; }
    public Guid? ErstattesAvKodeId { get; set; }
}

/// <summary>
/// Datasett (docs/03-domenemodell.md §1.6) — byggesteg 4, minimal. Full Datasett-skjerm (produktkrav
/// kap. 3.5, filtrerbar tabell, "Nytt datapunkt") er byggesteg 6 — denne tabellen finnes nå kun fordi
/// Vilkår.input (§5.4 i referansemodellen: kardinalitet 1..N) trenger noe å peke på. Ingen egen
/// statuslivssyklus — §3 i domenemodellen spesifiserer ikke en for denne typen.
/// </summary>
public sealed class DatasettEntitet
{
    public Guid Id { get; set; }

    /// <summary>Påkrevd — ikke eksplisitt kategorisert i §0.1, behandlet som virksomhetens eget arbeidsprodukt (samme begrunnelse som Vilkår/Begrep).</summary>
    public required Guid VirksomhetId { get; set; }

    public required string Felt { get; set; } // visningsnavn
    public required string Prop { get; set; } // maskinnavn, f.eks. "styrer.fodselsdato"
    public required string Dtype { get; set; } // 'string' | 'integer' | 'boolean' | 'date' | 'object'
    public required string Type { get; set; } // 'oppslagbart' | 'brukeroppgitt' | 'utledet'
    public string? Kilde { get; set; }
    public Guid? KodelisteId { get; set; }
    public string? Grunnlag { get; set; } // rettslig grunnlag for behandling/oppslag
    public string? Lagring { get; set; } // lagringstid
    public List<string> Mottakere { get; set; } = [];
    public string? Bruk { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}

/// <summary>
/// Faktisk parameterverdi for et Datasett-felt (2026-07-30, docs/12-fasit-handbok-leveranse.md
/// dimensjon C — "kommunale variasjoner som strukturert data, ikke fritekst"). <see cref="DatasettEntitet"/>
/// er kun en felt-DEFINISJON (Felt/Prop/Dtype) uten verdi — denne raden er selve verdien, én per
/// (Datasett, Virksomhet). <see cref="VirksomhetId"/> null betyr den nasjonale standardverdien
/// (speiler rundskriv-eksempelets §8.4-standardregel-rad for kommuner uten eget registrert regelsett)
/// — håndheves som maks én rad per Datasett via en egen filtrert unik-indeks, se RegelIdeDbContext.
/// </summary>
public sealed class DatasettVerdiEntitet
{
    public Guid Id { get; set; }
    public Guid DatasettId { get; set; }
    public Guid? VirksomhetId { get; set; }
    public required string VerdiJson { get; set; } // validert med JsonSerialiseringHjelper.ValiderJsonObjekt
    public string? Kilde { get; set; } // fritekst, f.eks. "Retningslinjer 2024–2028, vedtatt 12.06.2024"
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}

/// <summary>
/// Vilkår (docs/03-domenemodell.md §1.8) — bladnode i vilkårstreet (INV-1, referansemodell §5.4:
/// <c>Vilkår.barn = ∅</c>, aldri barn). Samme basemetadata/statusløp-mønster som <see cref="TjenesteEntitet"/>.
/// </summary>
public sealed class VilkarEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }

    /// <summary>
    /// Hvilken tjeneste dette vilkåret er identifisert for (2026-07-31, fasit-runde 5) — bevisst
    /// atskilt fra om vilkåret faktisk er koblet inn i tjenestens vilkårstre
    /// (<see cref="RegelnodeBarnEntitet"/>). Å identifisere et vilkår fra lovteksten er et lettere,
    /// tidligere steg enn å sette opp selve regelgrafen — se docs/12-fasit-handbok-leveranse.md.
    /// Nullable for eksisterende/generiske vilkår opprettet før dette feltet fantes.
    /// </summary>
    public Guid? TjenesteId { get; set; }

    public required string Tittel { get; set; }
    public string? Beskrivelse { get; set; }
    public string? GeneriskMal { get; set; } // fritekst-kode, f.eks. "GM-VANDEL-PERSON" — ingen egen registertabell i v1
    public required string Vilkarstype { get; set; } // 'formell' | 'materiell'
    public string? GjelderRolle { get; set; }
    public string JuridiskGrunnlagJson { get; set; } = "[]"; // liste {kilde, eId}
    public Guid? BegrepId { get; set; }
    public required string Vurderingstype { get; set; } // 'regelbasert' | 'skjonnsbasert' | 'hybrid'
    public string ParametreJson { get; set; } = "{}";

    /// <summary>Kun relevante når <see cref="Vurderingstype"/> ∈ {skjonnsbasert, hybrid} — håndhevet i VilkarregisterTjeneste, ikke DB-constraint.</summary>
    public Guid? SkjonnsgrunnlagBegrepId { get; set; }

    /// <summary>Liste {navn, beskrivelse, presedensreferanse?} — presedensreferanse er ubrukelig til byggesteg 3 finnes, samme utsettelsesmønster som HandbokKommentarMetadataEntitet.PraksisJson.</summary>
    public string SkjonnsmomenterJson { get; set; } = "[]";

    public bool KreverDokumentasjon { get; set; }
    public string? Eskaleringsrolle { get; set; }
    public string? VeiledningTilBruker { get; set; }
    public string? VeiledningTilSaksbehandler { get; set; }

    /// <summary>
    /// Lett annotering (2026-07-30, se docs/10-rules-as-code-landskap.md Trinn 1b) — markerer at denne
    /// "vilkår"-noden egentlig er en beregnet verdi/aggregert faktum (f.eks. bevillingsgebyr etter
    /// alkoholforskriften § 6-2), ikke en ekte testbar betingelse. Rent annoterende i v1 — ingen egen
    /// Formel-nodetype eller beregningsmotor; se referansemodellen §5.8 for hvorfor dette IKKE er en
    /// full ontologiendring.
    /// </summary>
    public bool ErFormel { get; set; }
    public string? FormelBeskrivelse { get; set; }

    public required string Status { get; set; }
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public Guid? ErstatterId { get; set; }
    public DateOnly? GyldigFra { get; set; }
    public DateOnly? GyldigTil { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }
}

/// <summary>Join-tabell for Vilkår.input (§5.4: kardinalitet 1..N mot Datasett). Ingen minimum håndheves ved opprettelse — bygges inkrementelt, en villet v1-forenkling.</summary>
public sealed class VilkarInputDatasettEntitet
{
    public Guid Id { get; set; }
    public Guid VilkarId { get; set; }
    public Guid DatasettId { get; set; }
}

/// <summary>
/// Regelnode (docs/03-domenemodell.md §1.9) — komposisjonsnode. Kalt "regelnode" i kode/API for å
/// unngå navnekollisjon med forklaringsmodell-apis "Regel" (referansemodell §5.6), som betyr noe helt
/// annet (det eksporterte, operasjonaliserte artefaktet).
/// </summary>
public sealed class RegelnodeEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }

    public required string Tittel { get; set; }
    public string? Beskrivelse { get; set; }
    public string? GeneriskMal { get; set; }
    public required string BarnOperator { get; set; } // 'OG' | 'ELLER' | 'IKKE'
    public required string UtdataNavn { get; set; }
    public required string UtdataType { get; set; }

    /// <summary>Kun rotnoden i et vilkårstre kjennetegnes slik (INV-5) — rotnodens utdata er selve vedtaksforslaget.</summary>
    public bool ErRotnode { get; set; }

    public string JuridiskGrunnlagJson { get; set; } = "[]";
    public string? InnvilgelseTekst { get; set; }
    public string? AvslagTekst { get; set; }

    public required string Status { get; set; }
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public Guid? ErstatterId { get; set; }
    public DateOnly? GyldigFra { get; set; }
    public DateOnly? GyldigTil { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }
}

/// <summary>Polymorf join-tabell for Regelnode.barn[] (1..N, INV-2) — et barn er enten et Vilkår eller en annen Regelnode (rekursivt).</summary>
public sealed class RegelnodeBarnEntitet
{
    public Guid Id { get; set; }
    public Guid RegelnodeId { get; set; }
    public required string BarnType { get; set; } // 'vilkar' | 'regelnode'
    public Guid BarnId { get; set; }

    /// <summary>
    /// Rekkefølge blant søsknene til samme RegelnodeId (2026-07-30) — settes av
    /// RegelnoderegisterTjeneste.KobleBarnAsync som "append til slutten". Fantes ikke før;
    /// nødvendig for en stabil beslutnings-ordnet traversering (veiledningsvisningen).
    /// </summary>
    public int Rekkefolge { get; set; }
}

/// <summary>
/// Unntak (docs/03-domenemodell.md §1.10). <see cref="GjelderRegelId"/> og <see cref="BetingelseId"/>
/// er begge påkrevd (INV-3/INV-4, referansemodell §5.4) — et Unntak kan ikke opprettes uten begge.
/// </summary>
public sealed class UnntakEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }

    public required string Tittel { get; set; }
    public string? Beskrivelse { get; set; }

    /// <summary>INV-3 — peker alltid på en Regelnode, aldri et Vilkår direkte.</summary>
    public required Guid GjelderRegelId { get; set; }

    /// <summary>INV-4 — selve "med mindre …"-testen. Vilkår eller Regelnode, samme rekursjon som Regelnode.barn.</summary>
    public required string BetingelseType { get; set; } // 'vilkar' | 'regelnode'
    public required Guid BetingelseId { get; set; }

    public string JuridiskGrunnlagJson { get; set; } = "[]";
    public required string Status { get; set; }
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public Guid? ErstatterId { get; set; }
    public DateOnly? GyldigFra { get; set; }
    public DateOnly? GyldigTil { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }
}

/// <summary>
/// Veiledningskommentar på en vilkårstre-node (2026-07-30, docs/12-fasit-handbok-leveranse.md
/// "Hovedfunn" + dimensjon A). En lettere, polymorf parallell til håndbok-kommentarer — IKKE en
/// generalisering av <see cref="HandbokKommentarMetadataEntitet"/> (den er hard delt-primærnøkkel mot
/// RettskildeNodeEntitet, se RegelIdeDbContext). Samme polymorfe mønster som
/// <see cref="TekstTaggEntitet.Kind"/>/<c>RefId</c> og <see cref="RegelnodeBarnEntitet.BarnType"/>/
/// <c>BarnId</c> — <see cref="MalType"/>/<see cref="MalId"/> peker på et Vilkår, en Regelnode eller
/// et Unntak.
/// </summary>
public sealed class VilkarstreKommentarEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }
    public required string MalType { get; set; } // 'vilkar' | 'regelnode' | 'unntak'
    public required Guid MalId { get; set; }

    /// <summary>
    /// Proveniens-merking på selve avsnittet — fasit-dimensjon A: "hvor sikker/kildebasert er DETTE
    /// avsnittet", ikke "hvilken dokumenttype er dette" (derfor en egen enum, ikke en gjenbruk av
    /// HandbokForfatterTjeneste sin dokumenttype-liste).
    /// </summary>
    public required string Dokumenttype { get; set; } // 'kommentar' | 'hjemmel' | 'praktisk-rad' | 'sjekkliste'

    public required string TekstHtml { get; set; } // sanert med KommentarTekstSanering, samme allow-list som håndbok

    /// <summary>
    /// Intern sorteringsnøkkel blant flere kommentarer PÅ SAMME node — node-til-node-rekkefølgen
    /// dekkes av vilkårstre-traverseringen selv. Settes KUN av <see
    /// cref="VilkarstreKommentarTjeneste.OpprettAsync"/> (append) og <see
    /// cref="VilkarstreKommentarTjeneste.FlyttAsync"/> (swap med nabo) — aldri av en klient som en
    /// fritt valgt literal verdi (2026-07-31, docs/12-fasit-handbok-leveranse.md "Prinsipp: rekkefølge
    /// og nummerering er alltid beregnet, aldri en redigerbar literal"). Visningsnummerering (hvis
    /// noe fremtidig UI trenger å vise "1.", "2." osv.) skal alltid beregnes fra listeposisjon ved
    /// rendering, aldri leses direkte fra dette feltet som en streng.
    /// </summary>
    public int Rekkefolge { get; set; }

    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }
}

public sealed class ProveniensEntitet
{
    public Guid Id { get; set; }

    /// <summary>NULL når den underliggende hendelsen gjaldt en delt/nasjonal entitet (§ RettskildeEntitet.VirksomhetId).</summary>
    public Guid? VirksomhetId { get; set; }

    public required string EntitetType { get; set; } // 'rettskilde' | 'begrep' | 'vilkar' | 'regelnode' | 'unntak' | …
    public Guid EntitetId { get; set; }
    public required string EndretAv { get; set; }
    public DateTimeOffset Dato { get; set; }
    public required string Handling { get; set; } // 'opprettet' | 'endret' | 'foreslatt_av_ai' | 'validert' | 'publisert' | 'arkivert'
    public string? KildeReferanserJson { get; set; } // jsonb
    public string? AiForslagVersjon { get; set; }
    public string? GodkjentAv { get; set; }
}

/// <summary>
/// Kunnskapsbibliotek (byggesteg 5 runde 1, docs/06-veikart.md "Byggesteg 5 — AI-forslag") — en lenke
/// til virksomhetens nettside/andre kilder som beskriver hva den leverer. Brukes kun av
/// «Identifiser tjenester»-agenten (<see cref="TjenesteregisterTjeneste"/>) som ekstra kontekst utover
/// valgte rettskilder. Alltid virksomhetens eget arbeidsprodukt, aldri delt (samme begrunnelse som
/// <see cref="TjenesteEntitet.VirksomhetId"/>). Rått kildemateriale, ikke et forfattet arbeidsprodukt —
/// ingen Status/Versjon/Entitetsstatus, sletting er hard delete.
/// </summary>
public sealed class KunnskapsbibliotekLenkeEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }
    public required string Url { get; set; }
    public string? Beskrivelse { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}

/// <summary>
/// Kunnskapsbibliotek — opplastet fil (byggesteg 5 runde 2), samme rolle som
/// <see cref="KunnskapsbibliotekLenkeEntitet"/> (rått kildemateriale til «Identifiser tjenester»,
/// ingen Status/Versjon, hard delete), men for faktiske dokumenter (PDF/Word) i stedet for lenker.
/// Egen tabell fremfor en <c>Type</c>-diskriminator på lenke-entiteten, siden formen er helt ulik
/// (binært innhold + utvunnet tekst vs. en ren URL) — samme begrunnelse som andre steder i kodebasen
/// der ulike kilde-typer får separate entitetsklasser. <see cref="Innhold"/> lagres som bytea i
/// Postgres (ikke ekstern blob-lagring — se docs/14-byggesteg5-teknisk-design.md). Tekstlaget i
/// <see cref="UtvunnetTekst"/> er allerede validert ikke-tomt av
/// <see cref="KunnskapsbibliotekTekstUtvinner"/> før raden opprettes — rene skann uten tekstlag
/// avvises der og når aldri hit.
/// </summary>
/// <summary>
/// Søkbar katalograd over Lovdatas bulk-datasett (byggesteg 5 runde 2) — KUN metadata (tittel/type),
/// aldri full strukturert tekst. <see cref="Datokode"/> er primærnøkkel og brukes direkte som input til
/// eksisterende <c>POST /api/rettskilder/lovdata</c> (uendret) når brukeren velger et treff. Hele
/// katalogen slettes og bygges på nytt ved hver <see cref="LovdataKatalogTjeneste.SikreOppdatertKatalogAsync"/>
/// (foreldet etter 24t, matcher Lovdatas nattlige oppdateringssyklus) — <see cref="SistOppdatert"/> er
/// derfor identisk på alle rader fra samme bygging.
/// </summary>
public sealed class LovdataKatalogOppforingEntitet
{
    public required string Datokode { get; set; }
    public required string Tittel { get; set; }
    public required string Type { get; set; } // 'lov' | 'forskrift'
    public DateTimeOffset SistOppdatert { get; set; }
}

public sealed class KunnskapsbibliotekFilEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }
    public required string Filnavn { get; set; }
    /// <summary>Valgfri, menneskelesbar tittel (byggesteg 5 runde 3) — samme rolle som Lenke.Beskrivelse. Vises i UI i stedet for Filnavn når satt.</summary>
    public string? Tittel { get; set; }
    public required string Filtype { get; set; } // 'pdf' | 'docx'
    public required byte[] Innhold { get; set; }
    public required string UtvunnetTekst { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}
