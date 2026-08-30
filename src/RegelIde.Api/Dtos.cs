using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Lett sammendrag for listeendepunktet. <see cref="Id"/> er databaseradens Guid — det låste
/// skjemaet (§2 i teknisk design) har ingen egen "datokode"-kolonne, kun (nullable) ELI, så Guid-en
/// er den naturlige, alltid-URL-sikre nøkkelen for enkeltoppslag.
/// </summary>
public sealed record RettskildeSammendrag(Guid Id, Guid? VirksomhetId, string? Eli, string Tittel, string? Kortnavn, string Kildetype)
{
    public static RettskildeSammendrag FraEntitet(RettskildeEntitet r) =>
        new(r.Id, r.VirksomhetId, r.Eli, r.Tittel, r.Kortnavn, r.Kildetype);
}

/// <summary>Full rettskilde: metadata + kanonisk AKN-XML (§1 i teknisk design). ELI er ALLTID skrivebeskyttet
/// (§3.3, avklaringsrunde 2026-08-13) — vist her, men aldri en del av <see cref="OppdaterRettskildeMetadataRequest"/>.
/// De seks feltene fra <see cref="InterntDokNr"/> til <see cref="GyldigTil"/> fantes allerede på
/// <see cref="RettskildeEntitet"/> (håndbok-metadata, §3.3) men var før nå ikke UI-eksponert i det hele tatt.</summary>
public sealed record RettskildeDetalj(
    Guid Id, Guid? VirksomhetId, string Doctype, string Kildetype, string Tittel, string? Kortnavn, string? Eli,
    DateOnly? Ikrafttredelse, DateOnly? KonsolidertDato, string? Utgiver, string Status, string? AknXml,
    string? InterntDokNr, string? Revisjonsnr, string? VedtattAv, DateOnly? Vedtaksdato, DateOnly? GyldigTil,
    string? Url)
{
    public static RettskildeDetalj FraEntitet(RettskildeEntitet r) => new(
        r.Id, r.VirksomhetId, r.Doctype, r.Kildetype, r.Tittel, r.Kortnavn, r.Eli,
        r.Ikrafttredelse, r.KonsolidertDato, r.Utgiver, r.Status, r.AknXml,
        r.InterntDokNr, r.Revisjonsnr, r.VedtattAv, r.Vedtaksdato, r.GyldigTil, r.Url);
}

/// <summary>Forespørsel for POST /api/rettskilder/lovdata.</summary>
public sealed record LovdataImportRequest(string Datokode);

/// <summary>
/// Forespørsel for PATCH /api/rettskilder/{id}/metadata — AK-3.3.6 (Kortnavn/Utgiver, opprinnelig
/// importbekreftelsen i Importer.tsx) UTVIDET 2026-08-13 (avklaringsrunde) med de håndbok-metadatafeltene
/// som allerede fantes på <see cref="RettskildeEntitet"/> men ikke var skrivbare noe sted:
/// InterntDokNr/Revisjonsnr/VedtattAv/Vedtaksdato/GyldigTil/KonsolidertDato. <see cref="RettskildeEntitet.Eli"/>
/// er BEVISST IKKE med her — permanent skrivebeskyttet, aldri brukerredigerbar eller gjettet.
/// </summary>
public sealed record OppdaterRettskildeMetadataRequest(
    string? Kortnavn, string? Utgiver, string? InterntDokNr, string? Revisjonsnr, string? VedtattAv,
    DateOnly? Vedtaksdato, DateOnly? GyldigTil, DateOnly? KonsolidertDato);

/// <summary>Én node i rettskildens tre (kapittel/underinndeling/paragraf/ledd/punkt), for tre-navigasjon.</summary>
public sealed record RettskildeNodeDto(
    Guid Id, string Eid, Guid? ParentNodeId, string NodeType, string? Nummer, string? Overskrift, string? Tekst,
    bool Opphevet, DateOnly? OpphevetDato, int Versjon, HandbokKommentarMetadataDto? HandbokMetadata)
{
    public static RettskildeNodeDto FraEntitet(RettskildeNodeEntitet n) => new(
        n.Id, n.Eid, n.ParentNodeId, n.NodeType, n.Nummer, n.Overskrift, n.Tekst, n.Opphevet, n.OpphevetDato,
        n.Versjon, n.HandbokMetadata is null ? null : HandbokKommentarMetadataDto.FraEntitet(n.HandbokMetadata));
}

/// <summary>Håndbok-kommentarseksjonens 1:1-metadata (docs/03-domenemodell.md §1.1.1). Kun satt for kommentar-noder.</summary>
public sealed record HandbokKommentarMetadataDto(
    string Dokumenttype, bool Bindende, string FesteNiva, string Status, string? Revisjonsgrunn,
    DateOnly? Publisert, DateOnly? SistFagligEndret, IReadOnlyList<string> Marginord)
{
    public static HandbokKommentarMetadataDto FraEntitet(HandbokKommentarMetadataEntitet m) => new(
        m.Dokumenttype, m.Bindende, m.FesteNiva, m.Status, m.Revisjonsgrunn, m.Publisert, m.SistFagligEndret, m.Marginord);
}

/// <summary>Forespørsel for POST /api/handboker.</summary>
public sealed record OpprettHandbokRequest(string Tittel);

/// <summary>Forespørsel for POST /api/handboker/{id}/kapitler.</summary>
public sealed record OpprettKapittelNodeRequest(Guid? ParentNodeId, string Nummer, string? Overskrift);

/// <summary>Forespørsel for POST /api/handboker/{id}/kommentarer.</summary>
public sealed record OpprettKommentarNodeRequest(
    Guid ParentNodeId, string Nummer, string? Overskrift, string TekstHtml,
    string Dokumenttype, string FesteNiva, IReadOnlyList<string>? Marginord);

/// <summary>Forespørsel for PUT /api/handboker/{id}/kommentarer/{nodeId} — oppretter alltid en ny versjon.</summary>
public sealed record RedigerKommentarNodeRequest(
    string TekstHtml, string? Overskrift, string Dokumenttype, string FesteNiva, IReadOnlyList<string>? Marginord);

/// <summary>Forespørsel for POST .../lovreferanser.</summary>
public sealed record KobleLovreferanseRequest(Guid TilRettskildeId, string TilEid);

/// <summary>Forespørsel for POST .../revisjonsmerke — AK-3.3.12.</summary>
public sealed record SettRevisjonsmerkeRequest(string Revisjonsgrunn);

/// <summary>Forespørsel for POST .../publiser — AK-3.3.11. GodkjentAv er påkrevd kun for bindende seksjoner.</summary>
public sealed record PubliserKommentarRequest(string? GodkjentAv);

/// <summary>Kryssreferanse funnet i løpeteksten (intern eller ekstern, §3.1 steg 6).</summary>
public sealed record RettskildeReferanseDto(Guid Id, Guid FraNodeId, Guid TilRettskildeId, string TilEid, string Opprinnelse, int? TekstStart, int? TekstLengde)
{
    public static RettskildeReferanseDto FraEntitet(RettskildeReferanseEntitet r) =>
        new(r.Id, r.FraNodeId, r.TilRettskildeId, r.TilEid, r.Opprinnelse, r.TekstStart, r.TekstLengde);
}

/// <summary>Tekst-tag (§1.2 i domenemodellen, AK-3.3.1–3.3.4). `RefId` er alltid null i byggesteg 1.</summary>
public sealed record TekstTaggDto(
    Guid Id, Guid RettskildeId, string NodeEid, int StartOffset, int EndOffset,
    string QuotePrefix, string QuoteExact, string QuoteSuffix, string Kind, Guid? RefId, string OpprettetAv,
    bool KreverGjennomgang)
{
    public static TekstTaggDto FraEntitet(TekstTaggEntitet t) => new(
        t.Id, t.RettskildeId, t.NodeEid, t.StartOffset, t.EndOffset,
        t.QuotePrefix, t.QuoteExact, t.QuoteSuffix, t.Kind, t.RefId, t.OpprettetAv, t.KreverGjennomgang);
}

/// <summary>Forespørsel for POST /api/rettskilder/{id}/tagger.</summary>
public sealed record OpprettTekstTaggRequest(
    string NodeEid, int StartOffset, int EndOffset, string QuotePrefix, string QuoteExact, string QuoteSuffix, string Kind);

/// <summary>Konfigurerbare tag-kinds (2026-07-25, erstatter en tidligere hardkodet liste).</summary>
public sealed record TaggKindKonfigurasjonDto(string Kode, string Navn, string Farge)
{
    public static TaggKindKonfigurasjonDto FraEntitet(TaggKindKonfigurasjonEntitet k) => new(k.Kode, k.Navn, k.Farge);
}

/// <summary>Forespørsel for POST .../koble — byggesteg 2, låser opp TekstTaggEntitet.RefId.</summary>
public sealed record KobleTaggTilEntitetRequest(Guid RefId);

/// <summary>
/// Felles statusendrings-forespørsel for Tjeneste/Begrep/Kodeliste (§3.1 i domenemodellen).
/// <see cref="GodkjentAv"/> er valgfri — brukt av byggesteg 5 runde 1 (AI-forslag, AK-3.10.2) når
/// status settes til "validert" etter et KI-forslag; øvrige kallere lar den stå null.
/// </summary>
public sealed record SettStatusRequest(string Status, string? GodkjentAv = null);

// ---------- Tjeneste (CPSV-AP-NO, docs/03-domenemodell.md §1.5) — byggesteg 2 ----------

/// <summary>Tjeneste ("Rettighet" i UI/modelltekst — samme rad, ikke en ny tabell, se
/// docs/18-vurdering-rettighet-samhandling-modell.md). <see cref="Kanaler"/>/<see cref="Sprak"/>/
/// <see cref="Malgruppe"/>/<see cref="Livshendelser"/> er postgres text[]; hendelser/tjenesteavhengigheter
/// (jsonb) er ikke eksponert i v1 (ingen forfatter-UI ennå).</summary>
public sealed record TjenesteDto(
    Guid Id, Guid VirksomhetId, string Tittel, string? Beskrivelse, string? KompetentMyndighet, string? Output,
    string? Tjenestetype, IReadOnlyList<string> Malgruppe, IReadOnlyList<string> Kanaler, string? Kostnad, string? Behandlingstid,
    string? Kontaktpunkt, string? KonsekvensVedBrudd, IReadOnlyList<string> Sprak, string Status, int Versjon,
    Guid? RotnodeId, IReadOnlyList<string> Livshendelser, string? LosKlassifisering, string? Tjenesteomrade,
    string? Type, string? Formal, TjenesteInnholdInput? Innhold, IReadOnlyList<EgetInnholdselementInput> EgneInnholdselementer)
{
    public static TjenesteDto FraEntitet(TjenesteEntitet t) => new(
        t.Id, t.VirksomhetId, t.Tittel, t.Beskrivelse, t.KompetentMyndighet, t.Output, t.Tjenestetype, t.Malgruppe,
        t.Kanaler, t.Kostnad, t.Behandlingstid, t.Kontaktpunkt, t.KonsekvensVedBrudd, t.Sprak, t.Status, t.Versjon,
        t.RotnodeId, t.Livshendelser, t.LosKlassifisering, t.Tjenesteomrade, t.Type, t.Formal,
        t.InnholdJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<TjenesteInnholdInput>(t.InnholdJson),
        System.Text.Json.JsonSerializer.Deserialize<List<EgetInnholdselementInput>>(t.EgneInnholdselementerJson) ?? []);
}

/// <summary>Forespørsel for POST/PUT /api/tjenester. De tre feltene fra 2026-08-20-runden(e) har
/// defaultverdi (null) slik at eksisterende positional-kall fortsatt kompilerer uendret.</summary>
public sealed record TjenesteRequest(
    string Tittel, string? Beskrivelse, string? KompetentMyndighet, string? Output, string? Tjenestetype,
    IReadOnlyList<string>? Malgruppe, IReadOnlyList<string>? Kanaler, string? Kostnad, string? Behandlingstid,
    string? Kontaktpunkt, string? KonsekvensVedBrudd, IReadOnlyList<string>? Sprak,
    IReadOnlyList<string>? Livshendelser = null, string? LosKlassifisering = null, string? Tjenesteomrade = null,
    string? Type = null, string? Formal = null, TjenesteInnholdInput? Innhold = null,
    IReadOnlyList<EgetInnholdselementInput>? EgneInnholdselementer = null);

// ---------- Handling (2026-08-20) — se HandlingEntitet i RegelIde.Data for begrunnelse ----------
// Underliggende JSON-verdiobjekter (HandlingKanalInput/HandlingHjemmelInput/osv.) er definert i
// RegelIde.Data/HandlingregisterTjeneste.cs, ikke duplisert her — samme mønster som
// JuridiskGrunnlagInput/SkjonnsmomentInput brukt av VilkarDto.

/// <summary>En konkret handling tilknyttet en Rettighet (Tjeneste) — søke, melde, klage, kontrolleres.
/// Se <see cref="RegelIde.Data.HandlingEntitet"/> for full begrunnelse.</summary>
public sealed record HandlingDto(
    Guid Id, Guid TjenesteId, string Navn, string Handlingstype, string? Bruksomraade, string? UtfortAv,
    Guid? RotnodeId, Guid? EksternKildeId, IReadOnlyList<HandlingKanalInput> Kanaler, HandlingBehandlingstidInput Behandlingstid,
    HandlingKostnadInput Kostnad, IReadOnlyList<HandlingVedleggInput> Vedlegg,
    IReadOnlyList<HandlingVeiledningstekstInput> Veiledningstekst, IReadOnlyList<HandlingArsakInput> Arsaker,
    HandlingResultatInput Resultat, string? Merknad, string Status, int Versjon)
{
    public static HandlingDto FraEntitet(HandlingEntitet h) => new(
        h.Id, h.TjenesteId, h.Navn, h.Handlingstype, h.Bruksomraade, h.UtfortAv, h.RotnodeId, h.EksternKildeId,
        System.Text.Json.JsonSerializer.Deserialize<List<HandlingKanalInput>>(h.KanalerJson) ?? [],
        System.Text.Json.JsonSerializer.Deserialize<HandlingBehandlingstidInput>(h.BehandlingstidJson)
            ?? new HandlingBehandlingstidInput(null, null),
        System.Text.Json.JsonSerializer.Deserialize<HandlingKostnadInput>(h.KostnadJson)
            ?? new HandlingKostnadInput(null, []),
        System.Text.Json.JsonSerializer.Deserialize<List<HandlingVedleggInput>>(h.VedleggJson) ?? [],
        System.Text.Json.JsonSerializer.Deserialize<List<HandlingVeiledningstekstInput>>(h.VeiledningstekstJson) ?? [],
        System.Text.Json.JsonSerializer.Deserialize<List<HandlingArsakInput>>(h.ArsakerJson) ?? [],
        System.Text.Json.JsonSerializer.Deserialize<HandlingResultatInput>(h.ResultatJson)
            ?? new HandlingResultatInput(null, []),
        h.Merknad, h.Status, h.Versjon);
}

/// <summary>Forespørsel for POST /api/tjenester/{id}/handlinger/koble (2026-08-27, Tjenestedetalj-
/// redesignrunden) — kobler en EKSISTERENDE handling (som virksomheten selv eier) som en sekundær
/// "også brukt av"-kobling. Se <see cref="RegelIde.Data.HandlingTjenesteEntitet"/>.</summary>
public sealed record KobleHandlingRequest(Guid HandlingId);

/// <summary>Én sekundær handlings-kobling — samme rolle for <see cref="RegelIde.Data.HandlingTjenesteEntitet"/>
/// som andre kobling-DTO-er i denne filen.</summary>
public sealed record HandlingTjenesteDto(Guid Id, Guid HandlingId, Guid TjenesteId)
{
    public static HandlingTjenesteDto FraEntitet(HandlingTjenesteEntitet k) => new(k.Id, k.HandlingId, k.TjenesteId);
}

/// <summary>Forespørsel for POST/PUT /api/tjenester/{id}/handlinger og .../handlinger/{handlingId}.</summary>
public sealed record HandlingRequest(
    string Navn, string Handlingstype, string? Bruksomraade, string? UtfortAv,
    IReadOnlyList<HandlingKanalInput>? Kanaler, HandlingBehandlingstidInput? Behandlingstid,
    HandlingKostnadInput? Kostnad, IReadOnlyList<HandlingVedleggInput>? Vedlegg,
    IReadOnlyList<HandlingVeiledningstekstInput>? Veiledningstekst, IReadOnlyList<HandlingArsakInput>? Arsaker,
    HandlingResultatInput? Resultat, string? Merknad);

/// <summary>Én rad for GET /api/handlinger (toppnivå-siden, 2026-08-22) — samme "innpakket base-DTO
/// pluss ekstra visningsfelt"-mønster som <see cref="TjenesteforslagDto"/>, ikke en duplisert/flatet
/// kopi av HandlingDto sine felt.</summary>
public sealed record HandlingMedTjenesteDto(HandlingDto Handling, string TjenesteTittel, Guid VirksomhetId)
{
    public static HandlingMedTjenesteDto FraRad(HandlingMedTjeneste rad) =>
        new(HandlingDto.FraEntitet(rad.Handling), rad.TjenesteTittel, rad.VirksomhetId);
}

public sealed record TjenesteRegelverksreferanseDto(Guid Id, Guid TjenesteId, Guid TilRettskildeId, string TilEid, string? Felt)
{
    public static TjenesteRegelverksreferanseDto FraEntitet(TjenesteRegelverksreferanseEntitet r) =>
        new(r.Id, r.TjenesteId, r.TilRettskildeId, r.TilEid, r.Felt);
}

/// <summary>Samme rolle for en Handling som <see cref="TjenesteRegelverksreferanseDto"/> har for en
/// Tjeneste (2026-08-22, <see cref="RegelIde.Data.OppgaveregisterHandlingSeed"/>).</summary>
public sealed record HandlingRegelverksreferanseDto(Guid Id, Guid HandlingId, Guid TilRettskildeId, string TilEid)
{
    public static HandlingRegelverksreferanseDto FraEntitet(HandlingRegelverksreferanseEntitet r) =>
        new(r.Id, r.HandlingId, r.TilRettskildeId, r.TilEid);
}

/// <summary>Sammendrag returnert av POST /api/eksterne-kilder/oppgaveregister/koble-til-handlinger —
/// se <see cref="RegelIde.Data.OppgaveregisterHandlingSeed"/>s klassekommentar for hva hvert felt teller
/// og hvorfor lave rettskilde-/virksomhet-treffrater er forventet.</summary>
public sealed record OppgaveregisterHandlingSeedResultatDto(
    int SkjemaTotalt, int NyeHandlinger, int OppdaterteHandlinger, int UendretHandlinger,
    int HoppetOverUsikkerVirksomhet, int NyeTjenester, int LovhjemlerTotalt,
    int RettskildematcherFunnet, int RettskildematcherIkkeFunnet)
{
    public static OppgaveregisterHandlingSeedResultatDto FraResultat(RegelIde.Data.OppgaveregisterHandlingSeedResultat r) =>
        new(r.SkjemaTotalt, r.NyeHandlinger, r.OppdaterteHandlinger, r.UendretHandlinger, r.HoppetOverUsikkerVirksomhet,
            r.NyeTjenester, r.LovhjemlerTotalt, r.RettskildematcherFunnet, r.RettskildematcherIkkeFunnet);
}

/// <summary>Håndbok-nivå rettskildeomfang (docs/12-fasit-handbok-leveranse.md, 2026-07-31).</summary>
public sealed record HandbokRettskildeomfangDto(Guid Id, Guid HandbokId, Guid TilRettskildeId)
{
    public static HandbokRettskildeomfangDto FraEntitet(HandbokRettskildeomfangEntitet o) =>
        new(o.Id, o.HandbokId, o.TilRettskildeId);
}

// ---------- Nettsider (docs/15-handbok-dokumentgraf-notat.md §3.1/§3.2/§3.4) ----------
// ---------- Punkt 8 (avklaringsrunde 2026-08-13): full konvergens — en nettside ER nå en    ----------
// ---------- ordinær RettskildeEntitet (Kildetype="Brukerveiledning"), vist via de vanlige   ----------
// ---------- /api/rettskilder-endepunktene. Disse to DTO-ene dekker det som IKKE allerede    ----------
// ---------- fanges av RettskildeDetalj/RettskildeNodeDto: §3.4s multi-sti og §3.2s lenker.  ----------

/// <summary>Én navigasjonssti (§3.4) — Sti+StiType, uten rettskilde-FK (allerede kjent fra konteksten).
/// Kun ikke-tom for Kildetype="Brukerveiledning" (se RettskildeEntitet.Stier).</summary>
public sealed record NettsideStiDto(string Sti, string StiType)
{
    public static NettsideStiDto FraEntitet(NettsideStiEntitet s) => new(s.Sti, s.StiType);
}

/// <summary>
/// Én utgående lenke (§3.2) fra en Brukerveilednings side-node, med oppløsningsstatus flatet ut som
/// nullbare felt (samme mønster som <see cref="RettskildeReferanseDto"/> ellers i denne filen — ingen
/// nestet "mål"-objekt). <see cref="TilRettskildeId"/>-familien er null når lenken er uløst (ekstern,
/// eller et av de eldre Lovdata-formatene <c>LovdataUrlTolker</c> bevisst ikke tolker). ÉN kolonne nå,
/// ikke to (se Entiteter.cs/NettsideLenkeEntitet-kommentaren) — punkt 8s konvergens gjorde "intern
/// nettside-lenke" og "PDF-omtale-lenke til håndbok" til nøyaktig samme oppløsning.
/// </summary>
public sealed record NettsideLenkeMedMalDto(
    Guid Id, string Type, string RaaHref, string? AnkerTekst,
    Guid? TilRettskildeId, string? TilRettskildeTittel, string? TilRettskildeEli)
{
    public static NettsideLenkeMedMalDto FraEntitet(NettsideLenkeEntitet l, RettskildeEntitet? tilRettskilde) => new(
        l.Id, l.Type, l.RaaHref, l.AnkerTekst, tilRettskilde?.Id, tilRettskilde?.Tittel, tilRettskilde?.Eli);
}

/// <summary>Hendelse (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1).</summary>
public sealed record HendelseDto(Guid Id, Guid? VirksomhetId, string Navn, string Type, string? Beskrivelse)
{
    public static HendelseDto FraEntitet(HendelseEntitet h) => new(h.Id, h.VirksomhetId, h.Navn, h.Type, h.Beskrivelse);
}

/// <summary>Forespørsel for POST /api/hendelser.</summary>
public sealed record HendelseRequest(string Navn, string Type, string? Beskrivelse);

/// <summary>Forespørsel for POST /api/tjenester/{id}/hendelser.</summary>
public sealed record KobleHendelseRequest(Guid HendelseId);

/// <summary>
/// Én tjenesteavhengighet sett fra den spurte tjenestens ståsted — se <see cref="TjenesteavhengighetVisning"/>.
/// (2026-08-19) Motparten er ENTEN en ekte tjeneste (<see cref="MotpartTjenesteId"/> satt,
/// <see cref="MotpartOrganisasjonsnummer"/> null) ELLER en ekstern plassholder (omvendt) —
/// <see cref="MotpartNavn"/> er alltid populert uansett, slik at klienten kan rendre begge tilfeller med
/// kun én null-sjekk (på <see cref="MotpartTjenesteId"/>, for å avgjøre om en <c>/tjenester/:id</c>-lenke
/// gir mening).
/// </summary>
public sealed record TjenesteavhengighetDto(
    Guid Id, string Rel, string Retning, string Visningstekst,
    Guid? MotpartTjenesteId, string? MotpartOrganisasjonsnummer, string MotpartNavn, string? MotpartUrl,
    Guid? HendelseId, string? HendelseNavn, string? Beskrivelse)
{
    public static TjenesteavhengighetDto FraVisning(TjenesteavhengighetVisning v) => new(
        v.Id, v.Rel, v.Retning, v.Visningstekst, v.MotpartTjenesteId, v.MotpartOrganisasjonsnummer, v.MotpartNavn, v.MotpartUrl,
        v.HendelseId, v.HendelseNavn, v.Beskrivelse);
}

/// <summary>
/// Forespørsel for POST /api/tjenester/{id}/avhengigheter — {id} blir alltid FraTjenesteId.
/// (2026-08-19) <see cref="TilTjenesteId"/> ble nullable — mål er ENTEN denne (en ekte tjeneste, typisk
/// funnet via GET /api/tjenester/sok-tverr-tenant) ELLER den eksterne trioen
/// <see cref="TilOrganisasjonsnummer"/>/<see cref="TilNavn"/> (+valgfri <see cref="TilUrl"/>) — nøyaktig
/// ett av de to må oppgis, håndhevet i <see cref="TjenesteavhengighetregisterTjeneste.OpprettAsync"/>.
/// </summary>
public sealed record TjenesteavhengighetRequest(
    Guid? TilTjenesteId, string Rel, Guid? HendelseId, string? Beskrivelse,
    string? TilOrganisasjonsnummer = null, string? TilNavn = null, string? TilUrl = null);

/// <summary>Ett cross-tenant søketreff for GET /api/tjenester/sok-tverr-tenant — se <see cref="TjenesteTverrTenantTreff"/>.</summary>
public sealed record TjenesteTverrTenantTreffDto(Guid Id, string Tittel, string? Beskrivelse, Guid VirksomhetId, string VirksomhetNavn)
{
    public static TjenesteTverrTenantTreffDto FraTreff(TjenesteTverrTenantTreff t) => new(t.Id, t.Tittel, t.Beskrivelse, t.VirksomhetId, t.VirksomhetNavn);
}

/// <summary>[Ny, 2026-08-28] Én node i en tjenestereise-graf (GET /api/tjenester/{id}/avhengighetsgraf) — se <see cref="TjenestereiseNode"/>.</summary>
public sealed record GrafNodeDto(
    Guid Id, string Navn, bool ErHandling, string? Type, string? KompetentMyndighet,
    IReadOnlyList<string> Livshendelser, string? Status)
{
    public static GrafNodeDto FraVisning(TjenestereiseNode n) => new(n.Id, n.Navn, n.ErHandling, n.Type, n.KompetentMyndighet, n.Livshendelser, n.Status);
}

/// <summary>Én kant — <see cref="ErHandlingTilhorighet"/> = ikke en ekte avhengighet, se <see cref="TjenestereiseKant"/>.</summary>
public sealed record GrafKantDto(Guid FraId, Guid TilId, string Rel, bool ErHandlingTilhorighet)
{
    public static GrafKantDto FraVisning(TjenestereiseKant k) => new(k.FraId, k.TilId, k.Rel, k.ErHandlingTilhorighet);
}

public sealed record AvhengighetsgrafDto(IReadOnlyList<GrafNodeDto> Noder, IReadOnlyList<GrafKantDto> Kanter)
{
    public static AvhengighetsgrafDto FraGraf(TjenestereiseGraf g) =>
        new(g.Noder.Select(GrafNodeDto.FraVisning).ToList(), g.Kanter.Select(GrafKantDto.FraVisning).ToList());
}

/// <summary>Forespørsel for POST /api/handboker/{id}/rettskilder.</summary>
public sealed record LeggTilRettskildeomfangRequest(Guid TilRettskildeId);

/// <summary>Forespørsel for POST /api/tjenester/{id}/regelverksreferanser. <see cref="Felt"/> valgfri —
/// se feltnøkkel-konvensjonen på <see cref="RegelIde.Data.TjenesteFeltnokler"/> (TjenesteregisterTjeneste.cs).</summary>
public sealed record KobleRegelverksreferanseRequest(Guid TilRettskildeId, string TilEid, string? Felt = null);

/// <summary>
/// [Ny, 2026-08-28, import-wizard-runden] Ett allerede menneske-bekreftet element fra en modelleksport-
/// JSON: <see cref="Tjeneste"/>/<see cref="Handlinger"/> gjenbruker EKSAKT samme request-DTO-er som
/// de vanlige skriveendepunktene (ingen duplisert felt-liste), <see cref="Regelverksreferanser"/>
/// peker allerede på ekte rettskilde-noder (wizarden har løst navn→FK FØR dette sendes). Avhengigheter
/// sendes IKKE her — de opprettes i et eget, senere kall (etter at ALLE rettigheter i importen har
/// fått ekte id-er), via det allerede eksisterende <c>POST /{id}/avhengigheter</c>.
/// </summary>
public sealed record ImportRettighetRequest(
    TjenesteRequest Tjeneste,
    IReadOnlyList<HandlingRequest> Handlinger,
    IReadOnlyList<KobleRegelverksreferanseRequest> Regelverksreferanser);

/// <summary>
/// Motsatt retning av <see cref="TjenesteRegelverksreferanseDto"/> — brukt av
/// GET /api/rettskilder/{id}/referert-av-tjenester (byggesteg 4, 2026-07-30).
/// </summary>
public sealed record TjenesteReferanseDto(Guid TjenesteId, string TjenesteTittel, string TilEid);

/// <summary>
/// Motsatt retning av <see cref="RettskildeReferanseDto"/> — brukt av
/// GET /api/rettskilder/{id}/referert-av-dokumenter (punkt 6/9, avklaringsrunde 2026-08-13).
/// Samme rolle som <see cref="TjenesteReferanseDto"/> har for tjenester, men for referanser som
/// ORIGINERER i en ANNEN RettskildeEntitet (typisk håndbok/rundskriv) sitt nodetre, ikke fra en
/// Tjeneste. <see cref="FraNodeEid"/>/<see cref="FraNodeOverskrift"/> identifiserer PRESIST hvilken
/// seksjon i det eiende dokumentet som refererer — nødvendig for å vise koblingen RETT VED SIDEN AV
/// den refererte noden (punkt 9), ikke bare som en global sluttliste (jf. <see cref="TjenesteReferanseDto"/>,
/// som ikke har et per-node-presisjonsbehov siden en Tjeneste ikke selv har et nodetre).
/// </summary>
public sealed record DokumentReferanseDto(
    Guid DokumentId, string DokumentTittel, string FraNodeEid, string? FraNodeOverskrift, string TilEid);

// ---------- Begrep (SKOS, docs/03-domenemodell.md §1.3) — byggesteg 2 ----------

public sealed record BegrepDto(
    Guid Id, Guid? VirksomhetId, string? Begrepskategori, Guid? VirksomhetReferanseId, Guid? LovkildeId,
    string Term, string? Definisjon, string? LovreferanseEid, IReadOnlyList<string> GjelderFor,
    Guid? KodelisteReferanseId, string? SkosUrl, string? Begrepstype, string Status, int Versjon)
{
    public static BegrepDto FraEntitet(BegrepEntitet b) => new(
        b.Id, b.VirksomhetId, b.Begrepskategori, b.VirksomhetReferanseId, b.LovkildeId, b.Term,
        b.Definisjon, b.LovreferanseEid, b.GjelderFor, b.KodelisteReferanseId, b.SkosUrl, b.Begrepstype,
        b.Status, b.Versjon);
}

/// <summary>Forespørsel for POST/PUT /api/begreper.</summary>
public sealed record BegrepRequest(
    string Term, string Definisjon, string? LovreferanseEid, IReadOnlyList<string>? GjelderFor,
    Guid? KodelisteReferanseId, string? SkosUrl, string Begrepstype);

// ---------- Virksomhetskatalog og rollemodell (docs/20) ----------

public sealed record SettForvaltningsnivaRequest(string? Forvaltningsniva);

/// <summary>
/// [Ny, 2026-08-29, docs/13-backlog.md §9] Ett Brreg-søketreff — nok til å vise i en velgerliste og
/// avgjøre om det allerede finnes i katalogen (frontend sjekker mot allerede lastet
/// <see cref="VirksomhetDto"/>-liste på <see cref="Organisasjonsnummer"/>).
/// </summary>
public sealed record BrregEnhetDto(
    string Organisasjonsnummer, string Navn, string? OrganisasjonsformKode, string? OrganisasjonsformBeskrivelse,
    string? Poststed, bool ErAktiv)
{
    public static BrregEnhetDto FraBrregEnhet(BrregEnhet e) => new(
        e.Organisasjonsnummer, e.Navn, e.Organisasjonsform?.Kode, e.Organisasjonsform?.Beskrivelse,
        e.Forretningsadresse?.Poststed, e.ErAktiv);
}

public sealed record OpprettVirksomhetFraBrregRequest(string Organisasjonsnummer);

/// <summary>
/// [Ny, 2026-08-30, brukertilbakemelding] Opprett en virksomhet med KUN navn — ingen org.nummer.
/// Dekker reelle juridiske aktører/underorganer som ikke har (eller ikke bør ha) sin egen Brreg-
/// registrering, f.eks. Kystvakten (del av Forsvaret) — <see cref="OverordnetEnhetId"/> lar en slik
/// virksomhet knyttes til en allerede eksisterende virksomhet i katalogen, samme felt som Brreg-
/// berikelse ellers fyller automatisk (docs/20 §2.1), her satt manuelt siden Brreg ikke har dataen.
/// </summary>
public sealed record OpprettVirksomhetRequest(string Navn, Guid? OverordnetEnhetId);

public sealed record VirksomhetsbegrepRequest(Guid VirksomhetId, string Term, string? SkosUrl);
public sealed record RollebegrepRequest(Guid LovkildeId, string Term);

public sealed record ParagrafspennParDto(string FraEid, string? TilEid);

public sealed record MyndighetstildelingRequest(
    Guid RolleBegrepId, Guid VirksomhetId, Guid HjemmelRettskildeId,
    IReadOnlyList<ParagrafspennParDto> Paragrafspenn, string? Vilkaar);

public sealed record MyndighetstildelingDto(
    Guid Id, Guid RolleBegrepId, Guid VirksomhetId, Guid HjemmelRettskildeId,
    IReadOnlyList<ParagrafspennParDto> Paragrafspenn, string? Vilkaar)
{
    public static MyndighetstildelingDto FraEntitet(MyndighetstildelingEntitet m) => new(
        m.Id, m.RolleBegrepId, m.VirksomhetId, m.HjemmelRettskildeId,
        MyndighetstildelingTjeneste.LesParagrafspenn(m).Select(p => new ParagrafspennParDto(p.FraEid, p.TilEid)).ToList(),
        m.Vilkaar);
}

public sealed record VirksomhetKandidatRequest(Guid VirksomhetId, Guid RettskildeId, string NodeEid, int StartOffset, int EndOffset);

public sealed record VirksomhetKandidatDto(
    Guid Id, Guid VirksomhetId, Guid RettskildeId, string NodeEid, int StartOffset, int EndOffset, string Status,
    string OpprettetAv, DateTimeOffset OpprettetTidspunkt, string? BehandletAv, DateTimeOffset? BehandletTidspunkt)
{
    public static VirksomhetKandidatDto FraEntitet(VirksomhetKandidatEntitet k) => new(
        k.Id, k.VirksomhetId, k.RettskildeId, k.NodeEid, k.StartOffset, k.EndOffset, k.Status,
        k.OpprettetAv, k.OpprettetTidspunkt, k.BehandletAv, k.BehandletTidspunkt);
}

/// <summary>Sveip-trigger (kravspek §4.2 pkt. 1) — søker gjennom alle rettskilder etter virksomhetens navneformer.</summary>
public sealed record SveipVirksomhetKandidaterRequest(Guid VirksomhetId);

public sealed record SveipVirksomhetKandidaterResultatDto(int AntallTreffFunnet, int AntallNyeKandidater);

/// <summary>Massegodkjenning/-avvisning (kravspek §4.2 pkt. 4) — server-side batch, ikke N separate kall.
/// Per-rad-feilhåndtering: én ugyldig id stopper ikke resten av batchen.</summary>
public sealed record VirksomhetKandidatBatchRequest(IReadOnlyList<Guid> Ider);

public sealed record VirksomhetKandidatBatchRadDto(Guid Id, bool Ok, string? Feil, VirksomhetKandidatDto? Resultat);

public sealed record VirksomhetKandidatBatchResultatDto(IReadOnlyList<VirksomhetKandidatBatchRadDto> Rader);

// ---------- Navnekandidater — oppdagelse av egennavn/juridiske aktører (docs/13-backlog.md §9) ----------

public sealed record NavnekandidatDto(
    Guid Id, string ForeslattTekst, string Kategori, Guid RettskildeId, string NodeEid, int StartOffset, int EndOffset,
    string Status, string OpprettetAv, DateTimeOffset OpprettetTidspunkt, string? BehandletAv, DateTimeOffset? BehandletTidspunkt)
{
    public static NavnekandidatDto FraEntitet(NavnekandidatEntitet k) => new(
        k.Id, k.ForeslattTekst, k.Kategori, k.RettskildeId, k.NodeEid, k.StartOffset, k.EndOffset, k.Status,
        k.OpprettetAv, k.OpprettetTidspunkt, k.BehandletAv, k.BehandletTidspunkt);
}

/// <summary>Sveip-trigger — <see cref="RettskildeId"/> = <c>null</c> sveiper HELE det importerte
/// korpuset, satt snevrer inn til én rettskilde.</summary>
public sealed record SveipNavnekandidaterRequest(Guid? RettskildeId);

public sealed record SveipNavnekandidaterResultatDto(int AntallTreffFunnet, int AntallNyeKandidater);

/// <summary>
/// Massegodkjenning/-avvisning (samme testing-i-store-mengder-begrunnelse som
/// <see cref="VirksomhetKandidatBatchRequest"/> over — sveip kan legge svært mange kandidater i køen
/// samtidig). Egen, IKKE delt/generisk DTO-familie fremfor å gjenbruke VirksomhetKandidatBatch*-typene
/// på tvers av de to køene: formen er identisk, men <see cref="NavnekandidatBatchRadDto.Resultat"/> må
/// likevel være <see cref="NavnekandidatDto"/> (ikke <see cref="VirksomhetKandidatDto"/>) for at
/// klienten skal få riktig type tilbake — samme "egen, parallell type" -linje som NavnekandidatDto selv
/// allerede følger ved siden av VirksomhetKandidatDto, i stedet for å innføre en generisk
/// BatchRadDto&lt;T&gt; kun for disse to. <see cref="NavnekandidatBatchRequest"/> derimot ER strukturelt
/// identisk med VirksomhetKandidatBatchRequest (bare en id-liste) — holdt som egen type likevel, for at
/// navnet skal si hvilken kø den hører til i signaturer/generert klient-kode.
/// </summary>
public sealed record NavnekandidatBatchRequest(IReadOnlyList<Guid> Ider);

public sealed record NavnekandidatBatchRadDto(Guid Id, bool Ok, string? Feil, NavnekandidatDto? Resultat);

public sealed record NavnekandidatBatchResultatDto(IReadOnlyList<NavnekandidatBatchRadDto> Rader);

// ---------- Kodeliste / verdidomene (docs/03-domenemodell.md §1.4) — byggesteg 2 ----------

public sealed record KodelisteKodeDto(
    Guid Id, string Kode, string Term, string? Definisjon, DateOnly? GyldigFra, DateOnly? GyldigTil, Guid? ErstattesAvKodeId)
{
    public static KodelisteKodeDto FraEntitet(KodelisteKodeEntitet k) =>
        new(k.Id, k.Kode, k.Term, k.Definisjon, k.GyldigFra, k.GyldigTil, k.ErstattesAvKodeId);
}

public sealed record KodelisteDto(
    Guid Id, Guid? VirksomhetId, string Kode, string Navn, string Type, string? JuridiskGrunnlagEid,
    string? EksternKildeUri, string? EksternKildeVersjon, string Status, int Versjon, IReadOnlyList<KodelisteKodeDto> Koder)
{
    public static KodelisteDto FraEntitet(KodelisteEntitet k) => new(
        k.Id, k.VirksomhetId, k.Kode, k.Navn, k.Type, k.JuridiskGrunnlagEid, k.EksternKildeUri, k.EksternKildeVersjon,
        k.Status, k.Versjon, k.Koder.Select(KodelisteKodeDto.FraEntitet).ToList());
}

/// <summary>Forespørsel for POST /api/kodelister. VirksomhetId påkrevd for juridisk/teknisk, må være null for ekstern-referanse (§0.1).</summary>
public sealed record KodelisteRequest(
    string Kode, string Navn, string Type, Guid? VirksomhetId, string? JuridiskGrunnlagEid,
    string? EksternKildeUri, string? EksternKildeVersjon);

/// <summary>Forespørsel for POST /api/kodelister/{id}/koder.</summary>
public sealed record LeggTilKodeRequest(string Kode, string Term, string? Definisjon, DateOnly? GyldigFra, DateOnly? GyldigTil);

// ---------- Vilkårstre (byggesteg 4 runde 1, docs/03-domenemodell.md §1.6/§1.8-1.10) ----------

/// <summary>Proveniens/endringslogg — brukt av .../historikk-endepunktene for Vilkår/Regelnode/Unntak.</summary>
public sealed record ProveniensDto(Guid Id, string EntitetType, Guid EntitetId, string EndretAv, DateTimeOffset Dato, string Handling, string? GodkjentAv)
{
    public static ProveniensDto FraEntitet(ProveniensEntitet p) => new(p.Id, p.EntitetType, p.EntitetId, p.EndretAv, p.Dato, p.Handling, p.GodkjentAv);
}

/// <summary>Datasett (§1.6), minimal — full skjerm er byggesteg 6. Kun lesing i denne runden, seedet.</summary>
public sealed record DatasettDto(
    Guid Id, Guid VirksomhetId, string Felt, string Prop, string Dtype, string Type, string? Kilde,
    Guid? KodelisteId, string? Grunnlag, string? Lagring, IReadOnlyList<string> Mottakere, string? Bruk)
{
    public static DatasettDto FraEntitet(DatasettEntitet d) => new(
        d.Id, d.VirksomhetId, d.Felt, d.Prop, d.Dtype, d.Type, d.Kilde, d.KodelisteId, d.Grunnlag, d.Lagring, d.Mottakere, d.Bruk);
}

/// <summary>Vilkår (§1.8) — bladnode i vilkårstreet. <c>ErFormel</c>/<c>FormelBeskrivelse</c>: se docs/10-rules-as-code-landskap.md.</summary>
public sealed record VilkarDto(
    Guid Id, Guid VirksomhetId, Guid? TjenesteId, string Tittel, string? Beskrivelse, string? GeneriskMal, string Vilkarstype,
    string? GjelderRolle, IReadOnlyList<JuridiskGrunnlagInput> JuridiskGrunnlag, Guid? BegrepId, string Vurderingstype,
    string ParametreJson, Guid? SkjonnsgrunnlagBegrepId, IReadOnlyList<SkjonnsmomentInput> Skjonnsmomenter,
    bool KreverDokumentasjon, string? Eskaleringsrolle, string? VeiledningTilBruker, string? VeiledningTilSaksbehandler,
    bool ErFormel, string? FormelBeskrivelse, string Status, int Versjon)
{
    public static VilkarDto FraEntitet(VilkarEntitet v) => new(
        v.Id, v.VirksomhetId, v.TjenesteId, v.Tittel, v.Beskrivelse, v.GeneriskMal, v.Vilkarstype, v.GjelderRolle,
        System.Text.Json.JsonSerializer.Deserialize<List<JuridiskGrunnlagInput>>(v.JuridiskGrunnlagJson) ?? [],
        v.BegrepId, v.Vurderingstype, v.ParametreJson, v.SkjonnsgrunnlagBegrepId,
        System.Text.Json.JsonSerializer.Deserialize<List<SkjonnsmomentInput>>(v.SkjonnsmomenterJson) ?? [],
        v.KreverDokumentasjon, v.Eskaleringsrolle, v.VeiledningTilBruker, v.VeiledningTilSaksbehandler,
        v.ErFormel, v.FormelBeskrivelse, v.Status, v.Versjon);
}

/// <summary>Forespørsel for POST/PUT /api/vilkar.</summary>
public sealed record VilkarRequest(
    string Tittel, string? Beskrivelse, string? GeneriskMal, string Vilkarstype, string? GjelderRolle,
    IReadOnlyList<JuridiskGrunnlagInput>? JuridiskGrunnlag, Guid? BegrepId, string Vurderingstype, string? ParametreJson,
    Guid? SkjonnsgrunnlagBegrepId, IReadOnlyList<SkjonnsmomentInput>? Skjonnsmomenter, bool KreverDokumentasjon,
    string? Eskaleringsrolle, string? VeiledningTilBruker, string? VeiledningTilSaksbehandler, bool ErFormel,
    string? FormelBeskrivelse, Guid? TjenesteId);

/// <summary>Forespørsel for POST /api/vilkar/{id}/input.</summary>
public sealed record LeggTilVilkarInputRequest(Guid DatasettId);

/// <summary>Regelnode (§1.9) — komposisjonsnode.</summary>
public sealed record RegelnodeDto(
    Guid Id, Guid VirksomhetId, string Tittel, string? Beskrivelse, string? GeneriskMal, string BarnOperator,
    string UtdataNavn, string UtdataType, bool ErRotnode, IReadOnlyList<JuridiskGrunnlagInput> JuridiskGrunnlag,
    string? InnvilgelseTekst, string? AvslagTekst, string Status, int Versjon)
{
    public static RegelnodeDto FraEntitet(RegelnodeEntitet r) => new(
        r.Id, r.VirksomhetId, r.Tittel, r.Beskrivelse, r.GeneriskMal, r.BarnOperator, r.UtdataNavn, r.UtdataType,
        r.ErRotnode, System.Text.Json.JsonSerializer.Deserialize<List<JuridiskGrunnlagInput>>(r.JuridiskGrunnlagJson) ?? [],
        r.InnvilgelseTekst, r.AvslagTekst, r.Status, r.Versjon);
}

/// <summary>Forespørsel for POST/PUT /api/regelnoder.</summary>
public sealed record RegelnodeRequest(
    string Tittel, string? Beskrivelse, string? GeneriskMal, string BarnOperator, string UtdataNavn, string UtdataType,
    bool ErRotnode, IReadOnlyList<JuridiskGrunnlagInput>? JuridiskGrunnlag, string? InnvilgelseTekst, string? AvslagTekst);

public sealed record RegelnodeBarnDto(Guid Id, Guid RegelnodeId, string BarnType, Guid BarnId)
{
    public static RegelnodeBarnDto FraEntitet(RegelnodeBarnEntitet b) => new(b.Id, b.RegelnodeId, b.BarnType, b.BarnId);
}

/// <summary>Forespørsel for POST /api/regelnoder/{id}/barn.</summary>
public sealed record KobleBarnRequest(string BarnType, Guid BarnId);

/// <summary>Forespørsel for PUT /api/regelnoder/{id}/operator.</summary>
public sealed record SettOperatorRequest(string BarnOperator);

/// <summary>Unntak (§1.10).</summary>
public sealed record UnntakDto(
    Guid Id, Guid VirksomhetId, string Tittel, string? Beskrivelse, Guid GjelderRegelId, string BetingelseType,
    Guid BetingelseId, IReadOnlyList<JuridiskGrunnlagInput> JuridiskGrunnlag, string Status, int Versjon)
{
    public static UnntakDto FraEntitet(UnntakEntitet u) => new(
        u.Id, u.VirksomhetId, u.Tittel, u.Beskrivelse, u.GjelderRegelId, u.BetingelseType, u.BetingelseId,
        System.Text.Json.JsonSerializer.Deserialize<List<JuridiskGrunnlagInput>>(u.JuridiskGrunnlagJson) ?? [], u.Status, u.Versjon);
}

/// <summary>Forespørsel for POST /api/unntak.</summary>
public sealed record OpprettUnntakRequest(
    string Tittel, string? Beskrivelse, Guid GjelderRegelId, string BetingelseType, Guid BetingelseId,
    IReadOnlyList<JuridiskGrunnlagInput>? JuridiskGrunnlag);

/// <summary>Forespørsel for PUT /api/unntak/{id}.</summary>
public sealed record OppdaterUnntakRequest(string Tittel, string? Beskrivelse, IReadOnlyList<JuridiskGrunnlagInput>? JuridiskGrunnlag);

/// <summary>Forespørsel for POST /api/tjenester/{id}/rotnode.</summary>
public sealed record SettRotnodeRequest(Guid RegelnodeId);

/// <summary>Flytt-en-handling-til-en-annen-tjeneste (2026-08-22) — se HandlingregisterTjeneste.FlyttTilTjenesteAsync.</summary>
public sealed record FlyttHandlingRequest(Guid TjenesteId);

/// <summary>
/// Kommunal/nasjonal parameterverdi for et Datasett-felt (docs/12-fasit-handbok-leveranse.md
/// dimensjon C, 2026-07-30). <c>VirksomhetId</c> null = nasjonal standardverdi.
/// </summary>
public sealed record DatasettVerdiDto(Guid Id, Guid DatasettId, Guid? VirksomhetId, string VerdiJson, string? Kilde)
{
    public static DatasettVerdiDto FraEntitet(DatasettVerdiEntitet v) => new(v.Id, v.DatasettId, v.VirksomhetId, v.VerdiJson, v.Kilde);
}

/// <summary>Forespørsel for POST /api/datasett/{id}/verdier.</summary>
public sealed record SettDatasettVerdiRequest(Guid? VirksomhetId, string VerdiJson, string? Kilde);

/// <summary>Veiledningskommentar på en vilkårstre-node (docs/12-fasit-handbok-leveranse.md "Hovedfunn" + dimensjon A).</summary>
public sealed record VilkarstreKommentarDto(Guid Id, string MalType, Guid MalId, string Dokumenttype, string TekstHtml, int Rekkefolge)
{
    public static VilkarstreKommentarDto FraEntitet(VilkarstreKommentarEntitet k) =>
        new(k.Id, k.MalType, k.MalId, k.Dokumenttype, k.TekstHtml, k.Rekkefolge);
}

/// <summary>Forespørsel for POST /api/vilkarstre-kommentarer.</summary>
public sealed record OpprettVilkarstreKommentarRequest(string MalType, Guid MalId, string Dokumenttype, string TekstHtml);

/// <summary>Forespørsel for PUT /api/vilkarstre-kommentarer/{id}.</summary>
public sealed record OppdaterVilkarstreKommentarRequest(string Dokumenttype, string TekstHtml);

/// <summary>Forespørsel for POST /api/vilkarstre-kommentarer/{id}/flytt. Retning: 'opp' | 'ned'.</summary>
public sealed record FlyttVilkarstreKommentarRequest(string Retning);

/// <summary>
/// Én datasett-verdi slik den gjelder for den spurte virksomheten i veiledningsvisningen — allerede
/// falt tilbake til standardverdien der ingen kommune-spesifikk verdi finnes (§8.4-mønsteret).
/// </summary>
public sealed record VeiledningDatasettVerdiDto(Guid DatasettId, string Felt, string Prop, string VerdiJson, string? Kilde, bool ErStandardverdi);

/// <summary>Ett Unntak inline i veiledningstraverseringen, rett etter sin GjelderRegel sine egne barn.</summary>
public sealed record VeiledningUnntakDto(
    Guid Id, string Tittel, string? Beskrivelse, string BetingelseType, Guid BetingelseId, string BetingelseTittel,
    IReadOnlyList<VilkarstreKommentarDto> Kommentarer);

/// <summary>
/// Én node i veiledningstreet (docs/12-fasit-handbok-leveranse.md "Hovedfunn") — rekursiv, i
/// beslutningsorden (Rekkefolge). <c>Type</c> ('vilkar'|'regelnode') avgjør hvilke av de
/// type-spesifikke feltene som er satt, samme diskriminator-mønster som ellers i byggesteg 4.
/// </summary>
public sealed record VeiledningNodeDto(
    Guid Id, string Type, string Tittel, string? Beskrivelse,
    string? Vilkarstype, string? Vurderingstype, IReadOnlyList<SkjonnsmomentInput> Skjonnsmomenter,
    string? BarnOperator,
    IReadOnlyList<JuridiskGrunnlagInput> JuridiskGrunnlag,
    IReadOnlyList<VeiledningDatasettVerdiDto> InputDatasettVerdier,
    IReadOnlyList<VilkarstreKommentarDto> Kommentarer,
    IReadOnlyList<VeiledningNodeDto> Barn,
    IReadOnlyList<VeiledningUnntakDto> Unntak);

/// <summary>Rotobjektet for GET /api/tjenester/{id}/veiledning.</summary>
public sealed record VeiledningDto(Guid TjenesteId, string TjenesteTittel, Guid? VirksomhetId, VeiledningNodeDto Rot);

// ---------- Byggesteg 5 runde 1 — Kunnskapsbibliotek + AI-forslag (docs/06-veikart.md) ----------

/// <summary>Kunnskapsbibliotek-lenke — kun brukt av «Identifiser tjenester»-agenten.</summary>
public sealed record KunnskapsbibliotekLenkeDto(Guid Id, Guid VirksomhetId, string Url, string? Beskrivelse, string OpprettetAv, DateTimeOffset OpprettetTidspunkt)
{
    public static KunnskapsbibliotekLenkeDto FraEntitet(KunnskapsbibliotekLenkeEntitet l) => new(
        l.Id, l.VirksomhetId, l.Url, l.Beskrivelse, l.OpprettetAv, l.OpprettetTidspunkt);
}

/// <summary>Forespørsel for POST /api/kunnskapsbibliotek/lenker.</summary>
public sealed record LeggTilLenkeRequest(string Url, string? Beskrivelse);

/// <summary>Ett søketreff i Lovdata-katalogen (byggesteg 5 runde 2) — kun metadata, ingen full tekst.</summary>
public sealed record LovdataKatalogTreffDto(string Datokode, string Tittel, string Type)
{
    public static LovdataKatalogTreffDto FraEntitet(LovdataKatalogOppforingEntitet o) => new(o.Datokode, o.Tittel, o.Type);
}

/// <summary>Siste kjente importforsøk for ETT Lovdata-dokument — se <see cref="LovdataImportstatusEntitet"/>.</summary>
public sealed record LovdataImportstatusDto(
    string Datokode, string Type, string? Tittel, string Eli, bool Importert, Guid? RettskildeId,
    string? Feilmelding, DateTimeOffset SistForsoktTidspunkt)
{
    public static LovdataImportstatusDto FraEntitet(LovdataImportstatusEntitet e) => new(
        e.Datokode, e.Type, e.Tittel, e.Eli, e.Importert, e.RettskildeId, e.Feilmelding, e.SistForsoktTidspunkt);
}

/// <summary>
/// Én høstet, rå kildepost (<see cref="EksternKildeEntitet"/>) — se den klassens kommentar for hvorfor
/// den bevisst IKKE er koblet til domenemodellen ennå. <see cref="RaaJson"/> er hele kildeobjektet,
/// verbatim.
/// </summary>
public sealed record EksternKildeDto(Guid Id, string Kildetype, string EksternId, string RaaJson, string InnholdsHash, DateTimeOffset HentetTidspunkt)
{
    public static EksternKildeDto FraEntitet(EksternKildeEntitet k) =>
        new(k.Id, k.Kildetype, k.EksternId, k.RaaJson, k.InnholdsHash, k.HentetTidspunkt);
}

/// <summary>Rotobjektet for GET /api/eksterne-kilder — paginert.</summary>
public sealed record EksternKildeListeDto(int Totalt, IReadOnlyList<EksternKildeDto> Kilder);

/// <summary>Sammendrag returnert av POST /api/eksterne-kilder/oppgaveregister/hent.</summary>
public sealed record EksternKildeHostingResultatDto(int Nye, int Oppdaterte, int Uendret);

/// <summary>
/// Sammendrag returnert av POST /api/eksterne-kilder/statsforvalter-tjenester/importer OG
/// POST /api/eksterne-kilder/fylkeskommune-tjenester/importer — de to fil-baserte kildene delt av
/// <see cref="RegelIde.Data.TjenestelisteImporter"/>. Utvider <see cref="EksternKildeHostingResultatDto"/>s
/// tre felt med <see cref="TilbydereMedManglendeOrgnummer"/>, siden begge kildene har en <c>tilbys_av</c>-
/// liste der et kjent oppstrøms-skjørhetstilfelle kan gi tom-streng-organisasjonsnummer — se
/// <see cref="RegelIde.Data.TjenestelisteImporter"/>s klassekommentar.
/// </summary>
public sealed record TjenestelisteHostingResultatDto(int Nye, int Oppdaterte, int Uendret, int TilbydereMedManglendeOrgnummer);

/// <summary>
/// Sammendrag returnert av POST /api/eksterne-kilder/kommune-tjenester/importer — den sjette,
/// fil-baserte kilden i høstelaget (<see cref="RegelIde.Data.KommuneTjenesteHenter"/>). Egen DTO, ikke
/// <see cref="TjenestelisteHostingResultatDto"/> gjenbrukt, siden det siste feltets betydning er reelt
/// forskjellig: her telles RECORDS hvis EIENDE KOMMUNE mangler <c>organisasjonsnummer</c> (og dermed ikke
/// kan få en trygg sammensatt identitetsnøkkel, se <see cref="RegelIde.Data.KommuneTjenesteHenter"/>s
/// klassekommentar), ikke enkeltoppføringer i en <c>tilbys_av</c>-liste.
/// </summary>
public sealed record KommuneTjenesteHostingResultatDto(int Nye, int Oppdaterte, int Uendret, int RecordsMedManglendeOrganisasjonsnummer);

/// <summary>Kunnskapsbibliotek-fil (byggesteg 5 runde 2) — inneholder aldri de rå bytene, kun utvunnet tekst.</summary>
public sealed record KunnskapsbibliotekFilDto(Guid Id, Guid VirksomhetId, string Filnavn, string? Tittel, string Filtype, string UtvunnetTekst, string OpprettetAv, DateTimeOffset OpprettetTidspunkt)
{
    public static KunnskapsbibliotekFilDto FraEntitet(KunnskapsbibliotekFilEntitet f) => new(
        f.Id, f.VirksomhetId, f.Filnavn, f.Tittel, f.Filtype, f.UtvunnetTekst, f.OpprettetAv, f.OpprettetTidspunkt);
}

/// <summary>
/// Forespørsel for POST /api/begreper/forslag/kjor og /api/tjenester/forslag/kjor.
/// <see cref="Omfang"/> (handlingsforslag-ki-omfang-runden) brukes KUN av /api/tjenester/forslag/kjor
/// — "tjeneste" (default, uendret oppførsel — ingen regresjon for eksisterende kallere, inkl.
/// /api/begreper/forslag/kjor som ikke bryr seg om feltet) eller "full" (Tjeneste + Handlinger i
/// samme kall, se <see cref="RegelIde.Data.TjenesteforslagTjeneste.KjorFullForslagAsync"/>). Omfang
/// "handling" hører IKKE hjemme her — det krever en EKSISTERENDE tjeneste og har derfor sitt eget
/// endepunkt, POST /api/tjenester/{id}/handlinger/forslag/kjor (<see cref="KjorHandlingsforslagRequest"/>).
/// </summary>
public sealed record KjorForslagRequest(IReadOnlyList<Guid> RettskildeIder, string Omfang = "tjeneste");

/// <summary>Forespørsel for POST /api/tjenester/{id}/handlinger/forslag/kjor (omfang "handling",
/// handlingsforslag-ki-omfang-runden) — {id} i ruten ER tjenesten handlingene skal foreslås for,
/// ingen egen TjenesteId-felt trengs i body.</summary>
public sealed record KjorHandlingsforslagRequest(IReadOnlyList<Guid> RettskildeIder);

/// <summary>Ett element i svaret fra omfang "full" — tjenesten pluss handlingene KI-en foreslo under den.</summary>
public sealed record TjenesteMedHandlingerDto(TjenesteDto Tjeneste, IReadOnlyList<HandlingDto> Handlinger)
{
    public static TjenesteMedHandlingerDto FraResultat(RegelIde.Data.TjenesteMedHandlingerResultat r) =>
        new(TjenesteDto.FraEntitet(r.Tjeneste), r.Handlinger.Select(HandlingDto.FraEntitet).ToList());
}

/// <summary>Forespørsel for POST /api/tjenester/forslag/kjor-rag (byggesteg 5 runde 4, RAG-spike) —
/// <see cref="AntallNoder"/> er K i "de K mest like nodene", se <see cref="RegelIde.Data.RagKontekstHjelper"/>.</summary>
public sealed record KjorForslagMedRagRequest(IReadOnlyList<Guid> RettskildeIder, int AntallNoder);

/// <summary>
/// Svar fra POST .../forslag/kjor (byggesteg 5 runde 3) — token-forbruk fra KI-kallet
/// (<see cref="RegelIde.Data.KiSvar"/>, null hvis leverandøren ikke rapporterer det) og en eksplisitt
/// <see cref="Melding"/> når agenten svarte, men <see cref="Forslag"/> er tom — se
/// <see cref="RegelIde.Data.KiForslagResultat{T}"/> for begrunnelsen.
/// </summary>
public sealed record KjorForslagResponsDto<T>(IReadOnlyList<T> Forslag, int? InputTokens, int? OutputTokens, string? Melding);

/// <summary>Kø-visning for «Identifiser begrep» — beriker BegrepDto med proveniens fra AI-forslaget.</summary>
public sealed record BegrepsforslagDto(BegrepDto Begrep, string? AiForslagVersjon, DateTimeOffset ForeslattTidspunkt, string? KildeReferanserJson);

/// <summary>Massegodkjenning/-avvisning av begrepsforslag (store test-sveip-/import-mengder gjør
/// enkeltrad-behandling upraktisk) — samme per-rad-feilhåndterings-mønster som
/// <see cref="VirksomhetKandidatBatchRequest"/>/<see cref="NavnekandidatBatchRequest"/>, egen
/// ikke-generisk DTO-familie av samme begrunnelse som der (se NavnekandidatBatchRequest-kommentaren).</summary>
public sealed record BegrepsforslagBatchRequest(IReadOnlyList<Guid> Ider);

public sealed record BegrepsforslagBatchRadDto(Guid Id, bool Ok, string? Feil, BegrepDto? Resultat);

public sealed record BegrepsforslagBatchResultatDto(IReadOnlyList<BegrepsforslagBatchRadDto> Rader);

/// <summary>
/// Kø-visning for «Identifiser tjenester» — beriker TjenesteDto med proveniens fra forslaget.
/// <see cref="AiForslagVersjon"/> satt = KI-forslag; <see cref="ForeslattAvVirksomhetNavn"/> satt =
/// tverr-virksomhet-import-forslag (2026-08-28, import-wizard-runden). Nøyaktig én av de to er satt.
/// </summary>
public sealed record TjenesteforslagDto(
    TjenesteDto Tjeneste, string? AiForslagVersjon, DateTimeOffset ForeslattTidspunkt, string? KildeReferanserJson,
    string? ForeslattAvVirksomhetNavn = null);

/// <summary>
/// [Ny, 2026-08-29] Motstykket til <see cref="TjenesteforslagDto"/>, sett fra IMPORTØRENS side — den
/// vanlige forslagskøen (`GET /api/tjenester/forslag`) viser kun det EGEN virksomhet EIER (mål-
/// virksomheten), så et tverr-virksomhet-import-forslag man selv har foreslått til en ANNEN
/// virksomhet er usynlig der. Oppdaget som et reelt hull under opprydding etter en test-import: uten
/// denne var det ingen UI-vei til å bruke <see cref="TjenesteregisterTjeneste.SlettForslagAsync"/>s
/// allerede eksisterende "opprinnelig foreslagsstiller"-tilgang — kun rå API-kall.
/// </summary>
public sealed record MittForslagDto(TjenesteDto Tjeneste, DateTimeOffset ForeslattTidspunkt, string MalVirksomhetNavn);

/// <summary>Massegodkjenning/-avvisning av tjenesteforslag — samme begrunnelse/mønster som
/// BegrepsforslagBatch*/VirksomhetKandidatBatch*/NavnekandidatBatch* (se NavnekandidatBatchRequest-
/// kommentaren for hvorfor hver kø har sin egen, ikke-generiske DTO-familie).</summary>
public sealed record TjenesteforslagBatchRequest(IReadOnlyList<Guid> Ider);

public sealed record TjenesteforslagBatchRadDto(Guid Id, bool Ok, string? Feil, TjenesteDto? Resultat);

public sealed record TjenesteforslagBatchResultatDto(IReadOnlyList<TjenesteforslagBatchRadDto> Rader);

/// <summary>Massesletting av UBEHANDLEDE tjenesteforslag — dekker BÅDE «Ventende forslag»-tabellens
/// enkeltrad-Slett-knapp OG «Mine forslag til andre virksomheter»-seksjonens Slett-knapp
/// (TjenesteforslagKo.tsx), siden begge til syvende og sist kaller samme
/// <see cref="TjenesteregisterTjeneste.SlettForslagAsync"/> (eier ELLER opprinnelig
/// foreslagsstiller — se den metodens egen tilgangskommentar). Ingen <c>Resultat</c>-felt her (til
/// forskjell fra Godkjenn/Avvis-radene over) — en sletting har ingenting igjen å returnere.</summary>
public sealed record TjenesteforslagSlettBatchRadDto(Guid Id, bool Ok, string? Feil);

public sealed record TjenesteforslagSlettBatchResultatDto(IReadOnlyList<TjenesteforslagSlettBatchRadDto> Rader);

/// <summary>
/// (2026-08-20) Full eksport av ÉN tjeneste og alt den er koblet til på KJERNEMODELL-nivået —
/// egenskaper, regelverksreferanser, hendelser og tjenesteavhengigheter (i BEGGE retninger, inkludert
/// eksterne plassholder-referanser, <c>feature/tjenesteavhengighet-ekstern-referanse</c>). BEVISST
/// UTEN vilkårstre — se <see cref="RegelIde.Data.TjenesteEksportTjeneste"/>s klassekommentar for
/// hvorfor det er en egen, senere avklaring, ikke en del av dette. Rent sammensatt LESEENDEPUNKT —
/// ingen egen lagret representasjon, alltid friskt beregnet fra de samme radene de øvrige
/// <c>/api/tjenester/{id}/...</c>-endepunktene allerede viser. Formål: ett JSON-dokument som er
/// tilstrekkelig til å forstå kjernemodellen for én tjeneste uten å måtte slå opp flere endepunkter
/// selv — først brukt til å gi et eksternt UX-designverktøy (Johann, 2026-08-20) et ekte
/// datagrunnlag for et skjermbilde-forslag på Serverings-/skjenkebevilling-domenet.
/// </summary>
public sealed record TjenesteEksportDto(
    TjenesteDto Tjeneste, string VirksomhetNavn,
    IReadOnlyList<TjenesteRegelverksreferanseDto> Regelverksreferanser, IReadOnlyList<HendelseDto> Hendelser,
    IReadOnlyList<TjenesteavhengighetDto> Avhengigheter, DateTimeOffset EksportertTidspunkt);
