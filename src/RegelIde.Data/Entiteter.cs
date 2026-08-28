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

    /// <summary>
    /// [ENDRET — virksomhetskatalog-runden, 2026-08-22, docs/20 §2.1] Var en grov, 3-verdis akse
    /// (`stat`|`fylke`|`kommune`); slått sammen med det som tidligere var en egen, planlagt
    /// `Organisasjonstype`-akse (docs/17 §3, `[LÅST]` der — bevisst OPPHEVET her, et instruert omvalg,
    /// ikke en feil). Verdisett nå: `stat`|`kommune`|`fylkeskommune`|`statsforvalter`|`tingrett`|
    /// `lagmannsrett`|`jordskifterett`. `kommune`/`fylkeskommune` settes automatisk ved seeding
    /// (entydig fra Brregs `orgForm`, se <see cref="OrganisasjonsregisterSeed"/>) — alt annet starter
    /// NULL og fylles inn manuelt (docs/20 §7.2, `[LÅST]`: "ikke gjett fra sektorkode/navn").
    /// </summary>
    public string? Forvaltningsniva { get; set; }

    /// <summary>[Ny, virksomhetskatalog-runden] Fra Brreg, ren referanseinformasjon — IKKE brukt for
    /// å utlede <see cref="Forvaltningsniva"/> automatisk utover den entydige kommune/fylkeskommune-
    /// seedingen (samme begrunnelse som statsforvalter-eksempelet i docs/20 §2.1: `ORGL` alene er for
    /// grovt, og selv der det ER entydig gjettbart skal det ikke gjettes, se §4).</summary>
    public string? OrganisasjonsformKode { get; set; }

    /// <summary>[Ny, virksomhetskatalog-runden] Fra Brreg (institusjonell sektorkode, SSB) — ren
    /// referanseinformasjon, samme "ingen automatisk avledning"-begrunnelse som
    /// <see cref="OrganisasjonsformKode"/>.</summary>
    public string? Sektorkode { get; set; }

    /// <summary>[Ny, virksomhetskatalog-runden] Fra Brreg, for organisatorisk hierarki. Selvrefererende,
    /// nullbar — de fleste virksomheter i katalogen har ingen kjent overordnet enhet.</summary>
    public Guid? OverordnetEnhetId { get; set; }

    /// <summary>[Ny, virksomhetskatalog-runden] Tidspunkt for siste berikelses-oppslag mot Brreg
    /// (docs/20 §4) — NULL for rader som bare er seedet, aldri beriket.</summary>
    public DateOnly? SistBrregSynkronisert { get; set; }

    /// <summary>
    /// (2026-08-14, organisasjonsregister-seeding) Gater om virksomheten er brukbar for REELT arbeid i
    /// dag — synlig i virksomhet-VELGERE (opprett/tilordne bruker, legge til kommunal datasett-verdi
    /// osv.) — versus seedet-men-sovende: til stede i registeret for fremtidig aktivering, men ikke
    /// noe man skal kunne velge for nytt arbeid ennå. Styrer KUN UI-velgere, ikke lesetilgang: allerede
    /// eksisterende innhold (rettskilder, tjenester osv.) eid av en inaktiv virksomhet forblir fullt
    /// synlig og søkbart (se <c>useVirksomheter.ts</c>' <c>visEier</c>, som bevisst IKKE filtrerer på
    /// dette feltet). Default <c>true</c> i databasen (migrasjonen) — sikker fallback for eksisterende
    /// rader ingen seed rører, slik at ingenting existing forsvinner stille fra en velger.
    /// </summary>
    public bool Aktiv { get; set; } = true;
}

/// <summary>
/// [Ny, virksomhetskatalog-runden, docs/20 §2.2] Nettsteder for en <see cref="Virksomhet"/> i
/// katalogen — distinkt fra de eksisterende <c>NettsideSti</c>/<c>NettsideLenke</c>-tabellene (ulik
/// hensikt: dette er virksomhetens EGNE nettsted, ikke en lenke fra et datasett/en tjeneste til en
/// ekstern side). Én <c>Hovedside</c>-rad kan auto-seedes ved Brreg-berikelse; øvrige legges til manuelt.
/// </summary>
public sealed class VirksomhetNettsideEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }
    public required string Url { get; set; }
    public required string Type { get; set; } // 'Hovedside' | 'Ovrig'
    public string? Merknad { get; set; }
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

/// <summary>
/// [Ny, 2026-08-27, Tjenestedetalj-redesignrunden] Rene UI-visningspreferanser for Tjeneste-siden —
/// EN rad per <see cref="Bruker"/>, aldri per <see cref="TjenesteEntitet"/> (samme fanerekkefølge og
/// samme accordion-åpen-tilstand følger brukeren fra tjeneste til tjeneste, bevisst valgt fremfor
/// «per tjeneste» — se plan-notatet for begrunnelsen). Dekker KUN de faste 7 fane-nøklene og de 9
/// faste accordion-nøklene i Innhold-fanen — egendefinerte innholdselementer ("+ Legg til eget
/// innholdselement") har sin egen, tjeneste-spesifikke rekkefølge/åpen-tilstand lagret sammen med
/// selve innholdet (<see cref="TjenesteEntitet.EgneInnholdselementerJson"/>), ikke her, siden en
/// custom-nøkkel bare gir mening for DEN ene tjenesten den ble skrevet på.
/// </summary>
public sealed class BrukerVisningsinnstillingEntitet
{
    public Guid Id { get; set; }
    public required Guid BrukerId { get; set; }

    /// <summary>Ordnet liste med de 7 faste fane-nøklene (vilkarstre/innhold/status/regelverk/
    /// hendelser/handlinger/avhengigheter) — "oversikt" er alltid først og aldri med her, se
    /// klassekommentaren. JSON-array av strenger.</summary>
    public string SeksjonsrekkefolgeJson { get; set; } = "[]";

    /// <summary>Delmengde av de samme 7 nøklene — skjulte faner. JSON-array av strenger.</summary>
    public string SkjulteSeksjonerJson { get; set; } = "[]";

    /// <summary>Ordnet liste med de 9 faste accordion-nøklene i Innhold-fanen. JSON-array av strenger.</summary>
    public string AccordionRekkefolgeJson { get; set; } = "[]";

    /// <summary>Åpen/lukket per fast accordion-nøkkel. JSON-objekt, streng-nøkkel til bool.</summary>
    public string AccordionApneJson { get; set; } = "{}";
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

    /// <summary>
    /// Kun ikke-tom for <c>Kildetype="Brukerveiledning"</c> (punkt 8, avklaringsrunde 2026-08-13) —
    /// §3.4s multi-sti-egenskap (en nettside kan opptre under FLERE navigasjonsstier samtidig, se
    /// <see cref="NettsideStiEntitet"/>s klassekommentar) er reell og nettside-spesifikk, ingen annen
    /// doctype har dette. Samme "egen tabell for en egenskap kun én doctype har"-mønster som
    /// <see cref="HandbokKommentarMetadataEntitet"/> og <see cref="RettskildeNodeEntitet.HandbokMetadata"/>.
    /// </summary>
    public List<NettsideStiEntitet> Stier { get; set; } = [];
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

    /// <summary>Endret fra <c>string?</c> 2026-08-20 (Rettighet/Handling-modellrunden) — samme
    /// listeform som <see cref="Kanaler"/>/<see cref="Sprak"/> på denne entiteten, for å dekke flere
    /// målgrupper uten å presse dem inn i én fri tekststreng.</summary>
    public List<string> Malgruppe { get; set; } = [];
    public List<string> Kanaler { get; set; } = [];
    public string? Kostnad { get; set; }
    public string? Behandlingstid { get; set; }
    public string? Kontaktpunkt { get; set; }
    public string? KonsekvensVedBrudd { get; set; }
    public List<string> Sprak { get; set; } = [];

    /// <summary>Nytt 2026-08-20 — livshendelse(r) tjenesten hører til (f.eks. "Starte og drive en
    /// bedrift"), atskilt fra <see cref="Tjenesteomrade"/> (fagområdet). Ikke koblet mot noe eksternt
    /// LOS-vokabular ennå — fri tekst.</summary>
    public List<string> Livshendelser { get; set; } = [];

    /// <summary>Nytt 2026-08-20 — Digdirs LOS-klassifisering (felles vokabular for klassifisering av
    /// offentlige tjenester og ressurser). Ikke koblet mot det faktiske LOS-vokabularet ennå (LOS 4 er
    /// varslet, ikke lansert) — fri tekst i mellomtiden.</summary>
    public string? LosKlassifisering { get; set; }

    /// <summary>Nytt 2026-08-20 — innbyggervennlig tema/kategori (f.eks. "Næring, salg og servering").
    /// Egen, egen akse fra <see cref="LosKlassifisering"/> — de to kan gi ulike svar for samme rad.</summary>
    public string? Tjenesteomrade { get; set; }

    /// <summary>Nytt 2026-08-20 (Tjenestedetalj-runden) — rettighetstype (myndighetsutøvelse/ytelse/...),
    /// valideres mot <c>TjenesteregisterTjeneste.GyldigeRettighetstyper</c>. Fra
    /// serveringsbevilling-modell-forslag.json sitt "type"-felt.</summary>
    public string? Type { get; set; }

    /// <summary>Nytt 2026-08-20 — formålsteksten (typisk lovens eget "§1 Formål"-avsnitt), atskilt fra
    /// <see cref="Beskrivelse"/> som allerede har et annet, kortere, teknisk-notat-aktig innhold.</summary>
    public string? Formal { get; set; }

    /// <summary>Nytt 2026-08-20 — rettighetens rike, forfattede innholdsseksjoner (tidspunkt/frister,
    /// innsender, vedlegg, veiledning, hva rettigheten innebærer, osv.), fra
    /// serveringsbevilling-modell-forslag.json sin rettigheter[].innhold. Se
    /// <see cref="TjenesteInnholdInput"/> i TjenesteregisterTjeneste.cs for skjemaet. Nullable (ikke
    /// "{}"-default) — de fleste tjenester vil ikke ha dette utfylt, og en NOT NULL-kolonne uten
    /// defaultValueSql feiler mot en tabell som allerede har rader (samme feil som ble rettet på
    /// Livshendelser-migrasjonen).</summary>
    public string? InnholdJson { get; set; }

    /// <summary>Nytt 2026-08-27 (Tjenestedetalj-redesignrunden) — frie, egendefinerte
    /// innholdsseksjoner utover de faste <see cref="InnholdJson"/>-feltene ("+ Legg til eget
    /// innholdselement"). Samme JSON-strengmønster som <see cref="HandlingEntitet.VedleggJson"/> —
    /// en liste av <c>{id, tittel, tekst}</c>, deserialisert til
    /// <c>List&lt;EgetInnholdselementInput&gt;</c> (TjenesteregisterTjeneste.cs) i API-laget.
    /// Rekkefølgen i JSON-arrayet ER visningsrekkefølgen — ingen egen sorteringskolonne.
    /// <c>Id</c> genereres klientside og er stabil over lagringer, siden den kan være mål for en
    /// felt-nivå regelverksreferanse (<see cref="TjenesteRegelverksreferanseEntitet.Felt"/> =
    /// <c>"egneInnholdselementer.{id}"</c>).</summary>
    public string EgneInnholdselementerJson { get; set; } = "[]";

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

/// <summary>
/// Nytt 2026-08-20 — en konkret handling tilknyttet en Rettighet (<see cref="TjenesteEntitet"/>):
/// søke, melde, klage, kontrolleres, osv. Vurdert mot <c>docs/18-vurdering-rettighet-samhandling-
/// modell.md</c> før bygging: en helt ny, parallell "Rettighet"-tabell ble avvist der (høy kostnad,
/// liten nytte mot bare 14 seedede Tjeneste-rader) — <see cref="TjenesteEntitet"/> ER Rettigheten,
/// utvidet. Handling er derimot genuint nytt, intet lignende fantes.
///
/// De rike underfeltene (kanaler/vedlegg/veiledningstekst/hjemmel/kostnad/behandlingstid/resultat/
/// arsaker) lagres som JSON-strenger, samme mønster som <see cref="VilkarEntitet.SkjonnsmomenterJson"/>
/// — verdiobjekter uten egen livssyklus, ingen "legg til én rad"-UI kreves i v1. Se
/// <see cref="JsonSerialiseringHjelper"/> for valideringen som allerede finnes for dette mønsteret.
///
/// <see cref="RotnodeId"/> er en EGEN, nullbar kobling til vilkårstreet — til forskjell fra
/// <see cref="TjenesteEntitet.RotnodeId"/> (som fortsatt gjelder Rettigheten som helhet). Tolkning:
/// en handling uten eget vilkårstre bruker Rettighetens; en handling MED eget vilkårstre overstyrer
/// det for sin egen saksbehandling (f.eks. en klage kan ha andre vilkår enn selve søknaden).
/// </summary>
public sealed class HandlingEntitet
{
    public Guid Id { get; set; }
    public required Guid TjenesteId { get; set; }
    public required string Navn { get; set; }

    /// <summary>Valideres mot <c>HandlingregisterTjeneste.GyldigeHandlingstyper</c> — ikke en
    /// DB-CHECK, samme "utvid med én kodelinje, ingen migrasjon"-holdning som
    /// <c>TjenesteavhengighetregisterTjeneste.GyldigeRel</c>.</summary>
    public required string Handlingstype { get; set; }

    /// <summary>Grov, ekte kategori hentet fra Oppgaveregisterets eget "bruksomraader[].navn"-felt
    /// (søknad_registrering/periodisk_rapportering/hendelsesrapportering) — Handlingstype er en finere
    /// underinndeling av denne. Valgfri: ikke alle handlinger er hentet fra en høstet kilde.</summary>
    public string? Bruksomraade { get; set; }

    /// <summary>'soker' | 'forvaltning' | 'tredjepart' — hvem som faktisk utfører handlingen.</summary>
    public string? UtfortAv { get; set; }

    /// <summary>Override av <see cref="TjenesteEntitet.RotnodeId"/> for denne ene handlingens
    /// saksbehandling — se klassekommentaren.</summary>
    public Guid? RotnodeId { get; set; }

    public string KanalerJson { get; set; } = "[]"; // [{kanal, adresse}]
    public string BehandlingstidJson { get; set; } = "{}"; // {frist, hjemmel}
    public string KostnadJson { get; set; } = "{}"; // {belop, hjemmel: [...]}
    public string VedleggJson { get; set; } = "[]"; // [{navn, kategori?, hjemmel}]
    public string VeiledningstekstJson { get; set; } = "[]"; // [{overskrift, innhold, hjemmel?}]
    public string ArsakerJson { get; set; } = "[]"; // [{arsak, hjemmel}] — kun for "bortfall"-type handlinger
    public string ResultatJson { get; set; } = "{}"; // {hva, bevisKanaler: [...]}

    public string? Merknad { get; set; }

    /// <summary>
    /// [Ny, 2026-08-22, <see cref="OppgaveregisterHandlingSeed"/>] Hvilken <see cref="EksternKildeEntitet"/>
    /// (Oppgaveregister-skjemaet) denne handlingen ble seedet fra — <c>null</c> for alle håndskrevne
    /// handlinger (de aller fleste i dag). Dette ER den idempotente re-kjørings-nøkkelen for seeden
    /// (matcher på denne, ikke på Navn — et skjemanavn kan endre seg mellom to høstinger av samme
    /// <see cref="EksternKildeEntitet.EksternId"/>), samme rolle som <see cref="EksternKildeEntitet.EksternId"/>
    /// spiller for høstelaget selv ett nivå ned.
    /// </summary>
    public Guid? EksternKildeId { get; set; }

    public required string Status { get; set; } // samme 7 verdier som TjenesteEntitet.Status (inkl. "foreslatt_av_ai")
    public int Versjon { get; set; } = 1;
    public string Entitetsstatus { get; set; } = "gjeldende";
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }
}

/// <summary>Regelverksreferanse fra en Tjeneste til en rettskilde-node — samme form som <see cref="RettskildeReferanseEntitet"/>, egen tabell siden kilden er en Tjeneste, ikke en rettskilde-node.</summary>
public sealed class TjenesteRegelverksreferanseEntitet
{
    public Guid Id { get; set; }
    public Guid TjenesteId { get; set; }
    public Guid TilRettskildeId { get; set; }
    public required string TilEid { get; set; }

    /// <summary>
    /// Nytt 2026-08-27 (Tjenestedetalj-redesignrunden) — <c>null</c> = gjelder hele tjenesten (den
    /// flate listen i "Regelverksreferanser"-fanen, dagens/opprinnelige oppførsel). Satt = knyttet
    /// til ETT bestemt felt i "Innhold"-fanen — verdien er det ekte DTO-feltnavnet
    /// (<see cref="TjenesteEntitet"/>/<c>TjenesteInnholdInput</c> sine egne property-navn, med punktum
    /// for nestede felt, f.eks. <c>"innhold.hvaRettighetenInnebarer.kontrollOgTilsyn"</c>), eller
    /// <c>"egneInnholdselementer.{id}"</c> for et fritt innholdselement. Se den fulle,
    /// dokumenterte feltnøkkel-konvensjonen i TjenesteregisterTjeneste.cs. Bevisst IKKE en validert
    /// enum-liste her — egendefinerte innholdselementer har dynamiske id-er en fast liste ikke kan
    /// romme.
    /// </summary>
    public string? Felt { get; set; }
}

/// <summary>
/// [Ny, 2026-08-22, <see cref="OppgaveregisterHandlingSeed"/>] Regelverksreferanse fra en Handling til
/// en rettskilde — EKSAKT samme form/rolle som <see cref="TjenesteRegelverksreferanseEntitet"/>, egen
/// tabell siden kilden her er en Handling, ikke en Tjeneste (en handling kan ha en annen, mer spesifikk
/// hjemmel enn den overordnede rettighetens egen). <see cref="TilEid"/> er her ALLTID rettskildens eget
/// <see cref="RettskildeEntitet.Eli"/> (dokument-nivå, ikke paragraf-nivå) — se
/// <see cref="OppgaveregisterHandlingSeed"/>s klassekommentar for hvorfor paragraf-nivå-oppløsning
/// bevisst ikke er forsøkt (Oppgaveregisterets <c>henvisning</c>-fritekst er for variert til å tolkes
/// uten å gjette).
/// </summary>
public sealed class HandlingRegelverksreferanseEntitet
{
    public Guid Id { get; set; }
    public Guid HandlingId { get; set; }
    public Guid TilRettskildeId { get; set; }
    public required string TilEid { get; set; }
}

/// <summary>
/// [Ny, 2026-08-27, Tjenestedetalj-redesignrunden] Sekundær "også brukt av"-kobling mellom en
/// <see cref="HandlingEntitet"/> og en ANNEN <see cref="TjenesteEntitet"/> enn den som eier den
/// ("Koble eksisterende handling" — søk blant ALLE tjenesters handlinger). IKKE eierskap:
/// <see cref="HandlingEntitet.TjenesteId"/> forblir uendret den ENE eier-referansen (forfatter,
/// sikkerhetsscoping i <c>HandlingregisterTjeneste</c>, tilbakelenken på <c>HandlingDetalj</c>-siden).
/// Denne tabellen er ren mange-til-mange, samme rolle for Handling som
/// <see cref="TjenesteHendelseEntitet"/> har for Hendelse — men her finnes ALLTID også en egen
/// eier utenfor koblingstabellen, siden en Handling (i motsetning til Hendelse) er forfattet
/// innhold, ikke et delt register fra dag én.
/// </summary>
public sealed class HandlingTjenesteEntitet
{
    public Guid Id { get; set; }
    public required Guid HandlingId { get; set; }
    public required Guid TjenesteId { get; set; }
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
/// <para>
/// (2026-08-19, `feature/tjenesteavhengighet-ekstern-referanse`) <see cref="FraTjenesteId"/> er ALLTID en
/// ekte tjeneste eid av kallerens egen virksomhet (kanten opprettes alltid FRA denne). Målet
/// («Til»-siden) er derimot ett av to: en ekte <see cref="TjenesteEntitet"/> (<see cref="TilTjenesteId"/>,
/// typisk en ANNEN virksomhets publiserte tjeneste — søkt opp via cross-tenant-søket), ELLER en
/// <see cref="EksternTjenestereferanseEntitet"/>-plassholder (<see cref="TilEksternReferanseId"/>) for en
/// tjeneste som ikke finnes som ekte rad i det hele tatt. NØYAKTIG ÉN av de to må være satt — håndhevet
/// BÅDE av <c>ck_tjenesteavhengigheter_ett_mal</c> (RegelIdeDbContext) OG defensivt i
/// <see cref="TjenesteavhengighetregisterTjeneste.OpprettAsync"/> (aldri stol på at CHECK-feilmeldingen
/// alene er lesbar for en bruker).
/// </para>
/// </summary>
public sealed class TjenesteavhengighetEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }

    public required Guid FraTjenesteId { get; set; }

    /// <summary>Nullable — nøyaktig én av denne og <see cref="TilEksternReferanseId"/> er satt, se klassekommentaren.</summary>
    public Guid? TilTjenesteId { get; set; }

    /// <summary>Nullable — nøyaktig én av denne og <see cref="TilTjenesteId"/> er satt, se klassekommentaren.</summary>
    public Guid? TilEksternReferanseId { get; set; }

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
/// Plassholder-referanse til en tjeneste som IKKE finnes som en ekte <see cref="TjenesteEntitet"/>-rad
/// (2026-08-19, `feature/tjenesteavhengighet-ekstern-referanse`) — enten fordi den eiende organisasjonen
/// ikke er onboardet til Regel-IDE, eller fordi den ikke har modellert nettopp denne tjenesten ennå
/// (f.eks. "Registrer matbedriften din hos Mattilsynet", "Vandelskontroll fra Politiet/Skatteetaten" —
/// reelle offentlig-offentlig-avhengigheter en kommunes Serveringsbevilling har I DAG, der motparten
/// mest sannsynlig ALDRI blir en Regel-IDE-tenant).
/// <para>
/// <see cref="Organisasjonsnummer"/> er BEVISST bindingsnøkkelen (Johanns eksplisitte instruks: "gjerne
/// med org.nummer som binding slik at man kan se avhengighetene") — IKKE en <c>VirksomhetId</c>-FK, siden
/// den refererte organisasjonen kanskje aldri onboardes som en ekte <see cref="Virksomhet"/>. Nøkkelen er
/// likevel et EKTE organisasjonsnummer — ingen gjettet fallback for identifikatoren selv — nettopp slik at
/// en FREMTIDIG reell onboarding av samme organisasjon i prinsippet kunne gjenkjennes/forsones mot denne
/// raden. Den forsoningen er IKKE bygget nå (se docs/13-backlog.md) — kun nøkkelvalget som gjør den mulig
/// senere, uten at noe er gjettet i dag.
/// </para>
/// <para>
/// IKKE <see cref="EksternKildeEntitet"/> — det er det rå, bulk-høstede harvest-laget (Oppgaveregisteret
/// m.fl., hele JSON-blober per høstet post). Denne entiteten er et lett, formålsbygd plassholder-objekt,
/// opprettet ETT OM ETT fra en tjenesteavhengighet-kobling (idempotent match på
/// <see cref="Organisasjonsnummer"/>+<see cref="Navn"/>, se <see cref="TjenesteavhengighetregisterTjeneste.OpprettAsync"/>),
/// ikke noe høstet i bulk.
/// </para>
/// </summary>
public sealed class EksternTjenestereferanseEntitet
{
    public Guid Id { get; set; }
    /// <summary>
    /// [ENDRET, 2026-08-28, bulk-import-runden] Gjort nullbar — reell import-testdata (vielsesreisen,
    /// se data/eksempler/) avdekket eksterne motparter som er KONSEPTUELLE, ikke identifiserbare norske
    /// organisasjoner i det hele tatt («en utenlandsk vigselsmyndighet», «et allerede inngått
    /// registrert partnerskap») — de kan aldri få et ekte orgnummer, uansett hvor lenge man venter.
    /// Dette er BEVISST atskilt fra <see cref="Virksomhet.Organisasjonsnummer"/>, som fortsatt er
    /// `[LÅST]` som en EKTE, stabil BRREG-nøkkel (docs/17 §11) — denne endringen rører IKKE den
    /// beslutningen. Når orgnummer FAKTISK finnes, brukes det fortsatt som bindingsnøkkel akkurat som
    /// før (se <see cref="TjenesteavhengighetregisterTjeneste.OpprettAsync"/>); kun når det ikke
    /// finnes noe orgnummer i det hele tatt faller deduplisering tilbake til <see cref="Navn"/> alene.
    /// </summary>
    public string? Organisasjonsnummer { get; set; }
    public required string Navn { get; set; }
    public string? Url { get; set; }
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
///
/// <para>
/// [ENDRET — virksomhetskatalog-runden, 2026-08-22, docs/20 §2.3/§2.4] <see cref="Begrepskategori"/>
/// er en ny, valgfri diskriminator: NULL betyr et ordinært fakta-/handlingsbegrep (opprinnelig, uendret
/// betydning — <see cref="VirksomhetId"/>/<see cref="Definisjon"/>/<see cref="Begrepstype"/> er da
/// fortsatt de facto påkrevd, validert i tjenestelaget, ikke en DB CHECK). `'virksomhet'` og `'rolle'`
/// er de to nye kategoriene — DELT/nasjonal referansedata uten én eiende virksomhet, samme mønster som
/// <see cref="KodelisteEntitet"/>s `Type='ekstern-referanse'`. Derfor er <see cref="VirksomhetId"/> nå
/// NULLBAR (var påkrevd) — NULL for `Begrepskategori IN ('virksomhet','rolle')`, satt for alt annet.
/// </para>
/// </summary>
public sealed class BegrepEntitet
{
    public Guid Id { get; set; }

    /// <summary>Påkrevd for ordinære fakta-/handlingsbegrep (§0.1 — et begrep er da virksomhetens eget
    /// arbeidsprodukt). NULL for <see cref="Begrepskategori"/> `'virksomhet'`/`'rolle'` — delt,
    /// nasjonal referansedata uten én eiende virksomhet (docs/20 §2.3/§2.4).</summary>
    public Guid? VirksomhetId { get; set; }

    /// <summary>NULL = ordinært fakta-/handlingsbegrep (opprinnelig betydning, uendret). `'virksomhet'`
    /// = navneform brukt om en virksomhet i rettskildetekst (<see cref="Term"/> = navnet,
    /// <see cref="VirksomhetReferanseId"/> = hvilken). `'rolle'` = et rollebegrep tildelt konkrete
    /// virksomheter gjennom forskrift (<see cref="Term"/> = rollenavnet, <see cref="LovkildeId"/> =
    /// hvilken lov — sammen utgjør de to rollebegrepets identitet, docs/20 §2.4).</summary>
    public string? Begrepskategori { get; set; }

    /// <summary>Kun for <see cref="Begrepskategori"/> = `'virksomhet'` — hvilken virksomhet
    /// <see cref="Term"/> er en navneform for.</summary>
    public Guid? VirksomhetReferanseId { get; set; }

    /// <summary>Kun for <see cref="Begrepskategori"/> = `'rolle'` — loven rollebegrepet hører til.
    /// Del av rollebegrepets IDENTITET sammen med <see cref="Term"/>, ikke bare metadata (docs/20 §2.4):
    /// samme rollenavn i to ulike lover er to ulike rader.</summary>
    public Guid? LovkildeId { get; set; }

    public required string Term { get; set; } // skos:prefLabel
    public string? Definisjon { get; set; } // skos:definition — påkrevd for Begrepskategori=NULL, se klassekommentaren
    public string? LovreferanseEid { get; set; } // dct:source — validert mot RettskildeNoder ved lagring
    public List<string> GjelderFor { get; set; } = [];
    public Guid? KodelisteReferanseId { get; set; } // peker til verdiområde (§1.4)
    public string? SkosUrl { get; set; } // publisert URI i Felles datakatalog (data.norge.no)
    public string? Begrepstype { get; set; } // 'faktabegrep' | 'handlingsbegrep' (Schartum 2025 7.3.3-7.3.4) — påkrevd for Begrepskategori=NULL, se klassekommentaren
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
/// [Ny, virksomhetskatalog-runden, docs/20 §2.5] Kobler et rollebegrep (<see cref="BegrepEntitet"/> med
/// <see cref="BegrepEntitet.Begrepskategori"/> = `'rolle'`) til en konkret virksomhet, hjemlet i en
/// forskrift/et delegeringsvedtak. INGEN egen <c>GyldigFra</c>/<c>GyldigTil</c> her — gyldighet arves
/// fra <see cref="HjemmelRettskildeId"/> (allerede har <c>Status</c>/<c>GyldigFra</c>/<c>GyldigTil</c>
/// som førsteklasses felt, docs/20 §2.5/§8 — ingen forutsetning måtte bygges for dette).
/// </summary>
public sealed class MyndighetstildelingEntitet
{
    public Guid Id { get; set; }
    public required Guid RolleBegrepId { get; set; }
    public required Guid VirksomhetId { get; set; }
    public required Guid HjemmelRettskildeId { get; set; }

    /// <summary>Strukturert (docs/20 §7.1, `[LÅST]`) — JSON-serialisert liste av
    /// <c>{ FraEid: string, TilEid: string? }</c>-par. <c>TilEid = null</c> betyr et enkeltstående
    /// punkt, ikke et spenn. Matches mot faktiske paragraf-/ledd-noder via eksisterende eId-oppslag.</summary>
    public string ParagrafspennJson { get; set; } = "[]";

    public string? Vilkaar { get; set; }
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? SistEndretAv { get; set; }
    public DateTimeOffset? SistEndretTidspunkt { get; set; }
}

/// <summary>
/// [Ny, virksomhetskatalog-runden, docs/20 §2.6] Arbeidskø for godkjenning av virksomhetsforekomster
/// funnet ved tekstsøk. Bevisst UTEN full <c>Entitetsstatus</c>/<c>Versjon</c>-versjonering som resten
/// av rettskildeinnholdet (docs/20 §2.6) — dette er en arbeidskø, ikke autoritativt rettskildeinnhold.
/// </summary>
public sealed class VirksomhetKandidatEntitet
{
    public Guid Id { get; set; }
    public required Guid VirksomhetId { get; set; }
    public required Guid RettskildeId { get; set; }

    /// <summary>Presis node-referanse (samme eId-mønster som resten av rettskilde-modellen).</summary>
    public required string NodeEid { get; set; }

    /// <summary>
    /// [Ny, kandidatsøk-og-godkjenning-runden] Tegn-intervall [<see cref="StartOffset"/>,
    /// <see cref="EndOffset"/>) for TREFFET i nodens <c>Tekst</c> på sveip-tidspunktet — IKKE lagret på
    /// noden selv, siden en node kan re-importeres/endre tekst mellom sveip og godkjenning.
    /// Designvalg: kandidat-nøkkelen (unik indeks, se RegelIdeDbContext) er derfor utvidet fra
    /// (VirksomhetId, RettskildeId, NodeEid) til også inkludere <see cref="StartOffset"/> — ETT sveip kan
    /// gi FLERE treff i samme node (f.eks. "Advokattilsynet" nevnt to ganger i samme ledd), og disse må
    /// kunne godkjennes/avvises UAVHENGIG av hverandre siden de blir til separate tagger. Ved godkjenning
    /// re-kjøres matchingen mot nodens DÅVÆRENDE tekst (samme "matcher ikke → kast"-vern som
    /// <see cref="TekstTaggTjeneste.OpprettAsync"/> allerede har) — quoteSelector-feltene (prefiks/eksakt/
    /// suffiks) beregnes da på nytt fra frisk tekst i stedet for å lagres her, og trenger derfor ikke
    /// dupliseres på denne raden.
    /// </summary>
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }

    public string Status { get; set; } = "Venter"; // 'Venter' | 'Godkjent' | 'Avvist'
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? BehandletAv { get; set; }
    public DateTimeOffset? BehandletTidspunkt { get; set; }
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
    public required string Handling { get; set; } // 'opprettet' | 'endret' | 'foreslatt_av_ai' | 'foreslatt_av_annen_virksomhet' | 'validert' | 'publisert' | 'arkivert'
    public string? KildeReferanserJson { get; set; } // jsonb
    public string? AiForslagVersjon { get; set; }
    public string? GodkjentAv { get; set; }

    /// <summary>
    /// [Ny, 2026-08-28, import-wizard-runden] Kun satt når <see cref="Handling"/> er
    /// <c>'foreslatt_av_annen_virksomhet'</c> — hvilken virksomhet som faktisk KJØRTE importen og
    /// dermed foreslo denne raden til <see cref="VirksomhetId"/> (mål-/eier-virksomheten). Samme
    /// additive mønster som <see cref="AiForslagVersjon"/> — ingen eksisterende kallere av
    /// <see cref="ProveniensEntitet"/> påvirkes.
    /// </summary>
    public Guid? ForeslattAvVirksomhetId { get; set; }
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

/// <summary>
/// Ett resultat per KJENT Lovdata-dokument (fra bulk-arkivet) etter siste
/// <see cref="LovdataFullimportTjeneste"/>-forsøk på full AKN-import — uansett om forsøket lyktes.
/// Formålet (2026-08-20, oppfølging av full Lovdata-synkronisering) er å kunne SE, i databasen,
/// nøyaktig hvilke dokumenter parseren i dag IKKE takler (<see cref="Importert"/> = false), med nok
/// metadata til å prioritere/utvide parseren case-by-case i stedet for å gjette en generell løsning
/// — se docs/13-backlog.md §6. <see cref="Eli"/> er alltid satt, UANSETT utfall: den er avledet rent
/// fra datokoden (<c>LovdataIdentifikatorer.AvledEliFraDatokode</c>), ikke fra den strukturelle
/// AKN-parsingen som nettopp er det som kan feile — derfor alltid tilgjengelig som en direkte lenke
/// til Lovdata-ressursen selv når importen mislykkes. <see cref="Datokode"/> er primærnøkkel (samme
/// mønster som <see cref="LovdataKatalogOppforingEntitet"/>) — én rad per kjent dokument, overskrevet
/// ved hver ny kjøring (ikke historikk over tid — kun SISTE kjente status er interessant her).
/// </summary>
public sealed class LovdataImportstatusEntitet
{
    public required string Datokode { get; set; }
    public required string Type { get; set; } // 'lov' | 'forskrift'

    /// <summary>Beste-forsøk tittel (se <c>LovdataBulkHenter.LesTittelBesteForsok</c>) — null hvis
    /// selv det enkle tittel-uttrekket feilet (svært sjeldent, uavhengig av hovedparseren).</summary>
    public string? Tittel { get; set; }

    /// <summary>Lovdatas offisielle ELI, dobler som direkte URL (<c>https://lovdata.no/eli/...</c>).</summary>
    public required string Eli { get; set; }

    /// <summary>Flagget brukeren ba om: false = dette dokumentet er IKKE importert (parseren avviste det).</summary>
    public required bool Importert { get; set; }

    /// <summary>Satt når <see cref="Importert"/> er true — hvilken <see cref="RettskildeEntitet"/> dette ble.</summary>
    public Guid? RettskildeId { get; set; }

    /// <summary>Satt når <see cref="Importert"/> er false — den faktiske unntaksmeldingen, til triage.</summary>
    public string? Feilmelding { get; set; }

    public DateTimeOffset SistForsoktTidspunkt { get; set; }
}

/// <summary>
/// <c>NettsideSti</c> (§3.1/§3.4) — én av potensielt FLERE navigasjonsstier en nettside opptrer
/// under. §3.4 er eksplisitt: "lagre ALLE stier en node opptrer under som separate rader. Å velge én
/// sti og kaste resten kaster informasjon." — derfor egen tabell (1:N), ikke et enkelt strengfelt.
/// <para>
/// **Punkt 8 (avklaringsrunde 2026-08-13) — full konvergens**: <c>NettsideDokumentEntitet</c> er
/// fjernet. En nettside ER nå en ordinær <see cref="RettskildeEntitet"/> med
/// <c>Kildetype="Brukerveiledning"</c> (se <see cref="RegelIde.Data.BrukerveiledningImportTjeneste"/>),
/// samme nodetre-maskineri som håndbok. <see cref="RettskildeId"/> peker derfor nå på
/// <c>rettskilder.Id</c>, ikke en egen <c>nettside_dokumenter</c>-tabell — formen (Id/FK/Sti/StiType)
/// er UENDRET, kun FK-målet er repekt (§3.4s multi-sti-egenskap ER reell og nettside-spesifikk, ingen
/// annen doctype trenger den, derfor fortsatt egen tabell — se <see cref="RettskildeEntitet.Stier"/>).
/// </para>
/// </summary>
public sealed class NettsideStiEntitet
{
    public Guid Id { get; set; }
    public Guid RettskildeId { get; set; }

    /// <summary>F.eks. "innbyggerhjelpen/naring-avgifter-og-anskaffelser/naring/bevilling-og-tillatelser"
    /// (tematisk) eller "omkommunen/avdelinger/kontor-for-skjenkesaker" (organisatorisk) — §3.4s egne
    /// eksempler, verifisert mot ekte data denne runden (se data/kilder/raw-nettside/README.md).</summary>
    public required string Sti { get; set; }

    /// <summary>'tematisk' | 'organisatorisk' (§3.4).</summary>
    public required string StiType { get; set; }
}

/// <summary>
/// Deterministiske kanter FRA en nettside (§3.2): <c>lenker_til</c> og <c>lovdatalenke</c>. EGEN
/// tabell — punkt 8s avklaring: vurdert MOT konvergens med <see cref="RettskildeReferanseEntitet"/>
/// (siden begge nå har en <see cref="RettskildeNodeEntitet"/>-FK som kilde etter denne rundens
/// endring), men IKKE konvergert, fordi feltene ikke passer uten friksjon:
/// <see cref="RettskildeReferanseEntitet.TilEid"/> er PÅKREVD der (en referanse peker alltid på en
/// presis paragraf), mens en nettside-lenke ofte IKKE har noe presist mål (ekstern lenke, eller en
/// hel rettskilde uten paragraf-presisjon); <see cref="RaaHref"/>/<see cref="AnkerTekst"/>/
/// <see cref="TilEidKandidat"/> har heller ingen mening for en Lovdata-kryssreferanse og ville blitt
/// alltid-NULL-kolonner der (samme antipattern <see cref="HandbokKommentarMetadataEntitet"/> og
/// <see cref="RettskildeNodeEmbeddingEntitet"/> allerede unngår ved å være egne tabeller). Ny, liten
/// tabell er derfor fortsatt riktigere enn gjenbruk — se <see cref="RegelIde.Kildekonvertering.NettsideTekstParser"/>
/// for den opprinnelige begrunnelsen (uendret i sak, kun FK-formen er ny).
/// </summary>
public sealed class NettsideLenkeEntitet
{
    public Guid Id { get; set; }

    /// <summary>
    /// Punkt 8: var <c>FraNettsideDokumentId</c> (FK mot den nå fjernede <c>NettsideDokumentEntitet</c>).
    /// Peker nå på SIDEN-NODEN (<see cref="RettskildeNodeEntitet"/> med <c>NodeType="side"</c>) en
    /// <see cref="RegelIde.Data.BrukerveiledningImportTjeneste"/>-import oppretter — samme FK-form som
    /// <see cref="RettskildeReferanseEntitet.FraNodeId"/> nå.
    /// </summary>
    public Guid FraNodeId { get; set; }

    /// <summary>'lenker_til' | 'lovdatalenke' (§3.2).</summary>
    public required string Type { get; set; }

    /// <summary>Den eksakte href-en/URL-en funnet i kildeteksten — ALDRI normalisert bort, selv
    /// etter at <see cref="TilRettskildeId"/> er løst, samme "vis kilden, ikke bare konklusjonen"-
    /// prinsipp som <see cref="RettskildeReferanseEntitet"/>.</summary>
    public required string RaaHref { get; set; }

    public string? AnkerTekst { get; set; }

    /// <summary>
    /// KUN for <see cref="Type"/> = 'lovdatalenke': ELI-formen parset deterministisk av
    /// <c>RegelIde.Kildekonvertering.LovdataUrlTolker</c> fra en moderne
    /// <c>lovdata.no/dokument/{NL|SF}/{lov|forskrift}/{dato}</c>-URL — f.eks.
    /// <c>"https://lovdata.no/eli/lov/1989/06/02/27/nor"</c>. Samme "ingen gjettet fallback, men
    /// heller ikke krasj"-prinsipp som <c>HjemmelMønster</c> i <c>HandbokTekstParser</c>: NULL når
    /// lenken er en lovdata.no-URL i et av de ELDRE, IKKE-håndterte formatene (se
    /// data/kilder/raw-nettside/README.md).
    /// </summary>
    public string? TilEidKandidat { get; set; }

    /// <summary>
    /// Punkt 8: kollapser den TIDLIGERE <c>TilNettsideDokumentId</c> (intern nettside-til-nettside-lenke)
    /// OG denne (lovdatalenke/PDF-omtale-lenke til en importert håndbok) til ÉN kolonne — siden ALLE
    /// lenkemål nå er <see cref="RettskildeEntitet"/>-rader (en nettside ER en rettskilde), finnes
    /// ikke lenger to ulike måltyper å skille mellom. Løst DB-avhengig av
    /// <see cref="RegelIde.Data.NettsideGrafKobler.LoosLenkerAsync"/>, ikke av parseren.
    /// </summary>
    public Guid? TilRettskildeId { get; set; }
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

/// <summary>
/// Rå, uforandret kopi av én oppføring fra en ekstern skjema-/tjenestekatalog — et frittstående
/// HØSTELAG, ikke en del av domenemodellen. Første og eneste kilde denne runden er Oppgaveregisteret
/// (Brønnøysundregistrene), se <see cref="RegelIde.Data.OppgaveregisterHenter"/>. Formålet er å samle
/// inn strukturert kildemateriale FØR det tolkes inn i appens domenemodell, ikke å erstatte den
/// tolkningen — "høst struktur, ikke generer den".
/// <para>
/// (a) **Bevisst INGEN FK** til <see cref="TjenesteEntitet"/>, <see cref="VilkarEntitet"/>, eller noen
/// fremtidig Rettighet/Samhandling-entitet. docs/17-forvaltningsstruktur-master-tjeneste.md og
/// docs/18-vurdering-rettighet-samhandling-modell.md diskuterer fortsatt om/hvordan Tjeneste-modellen
/// bør revideres (en mulig Rettighet/Samhandling-splitt) — uavklart på flere punkter per 2026-08-14.
/// Denne tabellen skal verken vente på det svaret eller forutsette hvilket svar som vinner: mapping
/// til domenemodellen er en egen, senere beslutning, tatt av en annen komponent enn denne.
/// </para>
/// <para>
/// (b) <see cref="RaaJson"/> ER sannheten — den fullstendige, ufortolkede kildeposten, lagret verbatim
/// (byte-for-byte fra kildens respons). De øvrige kolonnene
/// (<see cref="Kildetype"/>/<see cref="EksternId"/>/<see cref="InnholdsHash"/>/<see cref="HentetTidspunkt"/>)
/// finnes KUN for å identifisere/deduplisere rader ved re-høsting — vi vet ennå ikke hvilke felter en
/// fremtidig domenemapping trenger, så ingenting tolkes ut av JSON-en her på forhånd.
/// </para>
/// <para>
/// (c) <see cref="Kildetype"/> er en fri streng, samme mønster som <see cref="RettskildeEntitet.Kildetype"/>
/// (ingen CHECK-constraint) — flere kildetyper (Altinn skjemakatalog, Norge.no, FDK/data.norge.no m.fl.)
/// kan legges til senere UTEN skjemaendring. Navngivningen er bevisst nøytral med hensyn til retning:
/// verken et navn som "EksternKildeType" (som ville antatt vi alltid bare LESER) eller noe som forbereder
/// publisering tilbake til kilderegistrene — denne runden bygger verken den ene eller den andre retningen,
/// kun selve høstingen.
/// </para>
/// </summary>
public sealed class EksternKildeEntitet
{
    public Guid Id { get; set; }

    /// <summary>F.eks. "oppgaveregister_skjema". Fri streng — se klassekommentaren punkt (c).</summary>
    public required string Kildetype { get; set; }

    /// <summary>
    /// Kildens egen stabile identifikator (Oppgaveregisterets <c>guid</c>-felt, f.eks. "2BD") —
    /// SAMMEN med <see cref="Kildetype"/> den idempotente nøkkelen re-høsting matcher på (unik indeks).
    /// </summary>
    public required string EksternId { get; set; }

    /// <summary>Hele kildeobjektet, verbatim, som mottatt — se klassekommentaren punkt (b). Postgres <c>jsonb</c>, <c>text</c> på SQLite.</summary>
    public required string RaaJson { get; set; }

    /// <summary>
    /// SHA-256 over <see cref="RaaJson"/> (<see cref="RegelIde.Kildekonvertering.LovdataIdentifikatorer.BeregnTekstHash"/>)
    /// — samme endringsdeteksjonsmønster som <see cref="RettskildeEntitet.InnholdsHash"/>. Uendret hash
    /// ved re-høsting ⇒ raden røres ikke i det hele tatt (heller ikke <see cref="HentetTidspunkt"/>).
    /// </summary>
    public required string InnholdsHash { get; set; }

    /// <summary>Tidspunktet raden sist faktisk ble opprettet/endret av en høsting — IKKE tidspunktet for siste kjøring hvis den kjøringen ikke endret noe.</summary>
    public DateTimeOffset HentetTidspunkt { get; set; }
}
