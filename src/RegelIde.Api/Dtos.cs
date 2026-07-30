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

/// <summary>Full rettskilde: metadata + kanonisk AKN-XML (§1 i teknisk design).</summary>
public sealed record RettskildeDetalj(
    Guid Id, Guid? VirksomhetId, string Doctype, string Kildetype, string Tittel, string? Kortnavn, string? Eli,
    DateOnly? Ikrafttredelse, DateOnly? KonsolidertDato, string? Utgiver, string Status, string? AknXml)
{
    public static RettskildeDetalj FraEntitet(RettskildeEntitet r) => new(
        r.Id, r.VirksomhetId, r.Doctype, r.Kildetype, r.Tittel, r.Kortnavn, r.Eli,
        r.Ikrafttredelse, r.KonsolidertDato, r.Utgiver, r.Status, r.AknXml);
}

/// <summary>Forespørsel for POST /api/rettskilder/lovdata.</summary>
public sealed record LovdataImportRequest(string Datokode);

/// <summary>Forespørsel for PATCH /api/rettskilder/{id}/metadata — AK-3.3.6, kun Kortnavn/Utgiver.</summary>
public sealed record OppdaterRettskildeMetadataRequest(string? Kortnavn, string? Utgiver);

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

/// <summary>Felles statusendrings-forespørsel for Tjeneste/Begrep/Kodeliste (§3.1 i domenemodellen).</summary>
public sealed record SettStatusRequest(string Status);

// ---------- Tjeneste (CPSV-AP-NO, docs/03-domenemodell.md §1.5) — byggesteg 2 ----------

/// <summary>Tjeneste. <see cref="Kanaler"/>/<see cref="Sprak"/> er postgres text[]; hendelser/tjenesteavhengigheter (jsonb) er ikke eksponert i v1 (ingen forfatter-UI ennå).</summary>
public sealed record TjenesteDto(
    Guid Id, Guid VirksomhetId, string Tittel, string? Beskrivelse, string? KompetentMyndighet, string? Output,
    string? Tjenestetype, string? Malgruppe, IReadOnlyList<string> Kanaler, string? Kostnad, string? Behandlingstid,
    string? Kontaktpunkt, string? KonsekvensVedBrudd, IReadOnlyList<string> Sprak, string Status, int Versjon,
    Guid? RotnodeId)
{
    public static TjenesteDto FraEntitet(TjenesteEntitet t) => new(
        t.Id, t.VirksomhetId, t.Tittel, t.Beskrivelse, t.KompetentMyndighet, t.Output, t.Tjenestetype, t.Malgruppe,
        t.Kanaler, t.Kostnad, t.Behandlingstid, t.Kontaktpunkt, t.KonsekvensVedBrudd, t.Sprak, t.Status, t.Versjon,
        t.RotnodeId);
}

/// <summary>Forespørsel for POST/PUT /api/tjenester.</summary>
public sealed record TjenesteRequest(
    string Tittel, string? Beskrivelse, string? KompetentMyndighet, string? Output, string? Tjenestetype,
    string? Malgruppe, IReadOnlyList<string>? Kanaler, string? Kostnad, string? Behandlingstid,
    string? Kontaktpunkt, string? KonsekvensVedBrudd, IReadOnlyList<string>? Sprak);

public sealed record TjenesteRegelverksreferanseDto(Guid Id, Guid TjenesteId, Guid TilRettskildeId, string TilEid)
{
    public static TjenesteRegelverksreferanseDto FraEntitet(TjenesteRegelverksreferanseEntitet r) =>
        new(r.Id, r.TjenesteId, r.TilRettskildeId, r.TilEid);
}

/// <summary>Forespørsel for POST /api/tjenester/{id}/regelverksreferanser.</summary>
public sealed record KobleRegelverksreferanseRequest(Guid TilRettskildeId, string TilEid);

/// <summary>
/// Motsatt retning av <see cref="TjenesteRegelverksreferanseDto"/> — brukt av
/// GET /api/rettskilder/{id}/referert-av-tjenester (byggesteg 4, 2026-07-30).
/// </summary>
public sealed record TjenesteReferanseDto(Guid TjenesteId, string TjenesteTittel, string TilEid);

// ---------- Begrep (SKOS, docs/03-domenemodell.md §1.3) — byggesteg 2 ----------

public sealed record BegrepDto(
    Guid Id, Guid VirksomhetId, string Term, string Definisjon, string? LovreferanseEid,
    IReadOnlyList<string> GjelderFor, Guid? KodelisteReferanseId, string? SkosUrl, string Begrepstype,
    string Status, int Versjon)
{
    public static BegrepDto FraEntitet(BegrepEntitet b) => new(
        b.Id, b.VirksomhetId, b.Term, b.Definisjon, b.LovreferanseEid, b.GjelderFor, b.KodelisteReferanseId,
        b.SkosUrl, b.Begrepstype, b.Status, b.Versjon);
}

/// <summary>Forespørsel for POST/PUT /api/begreper.</summary>
public sealed record BegrepRequest(
    string Term, string Definisjon, string? LovreferanseEid, IReadOnlyList<string>? GjelderFor,
    Guid? KodelisteReferanseId, string? SkosUrl, string Begrepstype);

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
    Guid Id, Guid VirksomhetId, string Tittel, string? Beskrivelse, string? GeneriskMal, string Vilkarstype,
    string? GjelderRolle, IReadOnlyList<JuridiskGrunnlagInput> JuridiskGrunnlag, Guid? BegrepId, string Vurderingstype,
    string ParametreJson, Guid? SkjonnsgrunnlagBegrepId, IReadOnlyList<SkjonnsmomentInput> Skjonnsmomenter,
    bool KreverDokumentasjon, string? Eskaleringsrolle, string? VeiledningTilBruker, string? VeiledningTilSaksbehandler,
    bool ErFormel, string? FormelBeskrivelse, string Status, int Versjon)
{
    public static VilkarDto FraEntitet(VilkarEntitet v) => new(
        v.Id, v.VirksomhetId, v.Tittel, v.Beskrivelse, v.GeneriskMal, v.Vilkarstype, v.GjelderRolle,
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
    string? Eskaleringsrolle, string? VeiledningTilBruker, string? VeiledningTilSaksbehandler, bool ErFormel, string? FormelBeskrivelse);

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
