using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Ett paragraf-/leddspenn (docs/20 §7.1, `[LÅST]`: strukturert, ikke fritekst). <c>TilEid</c>
/// null betyr et enkeltstående punkt, ikke et spenn. [Flyttet hit fra MyndighetstildelingTjeneste.cs, #311.]</summary>
public sealed record ParagrafspennPar(string FraEid, string? TilEid);

/// <summary>[Ny, issue #311] Én ende av en strukturkant — NØYAKTIG én av de to id-ene er satt.</summary>
public sealed record Kantnode(Guid? VirksomhetId, Guid? BegrepId)
{
    public static Kantnode Virksomhet(Guid id) => new(id, null);
    public static Kantnode Begrep(Guid id) => new(null, id);
    public bool ErGyldig => (VirksomhetId is null) != (BegrepId is null);
}

/// <summary>
/// [Ny, issue #311] Alt som trengs for å opprette én strukturkant. Felles egenskaper fra docs/33 §4.3:
/// hjemmel ELLER kilde utenfor korpus, avgrensning (paragrafspenn + tekst), polaritet, gyldighet, status og
/// oppdagelseskilde.
/// </summary>
/// <param name="Til">Null bare for K og T (<see cref="Strukturkanter.Noderegel.TilValgfri"/>).</param>
/// <param name="AiForslagVersjon">Påkrevd sammen med <c>Status = "maskinelt_foreslatt"</c> når
/// <paramref name="OppdagelsesKilde"/> ikke er oppgitt (samme regel som de gamle tjenestene hadde) — havner i
/// Proveniens og avleder <c>OppdagelsesKilde = "ki:&lt;versjon&gt;"</c>.</param>
public sealed record NyStrukturkant(
    string Kategori, string Typekode, Kantnode Fra, Kantnode? Til,
    Guid? HjemmelRettskildeId = null, string? HjemmelEid = null,
    string? KildeUtenforKorpusTekst = null, string? KildeUtenforKorpusLenke = null,
    IReadOnlyList<ParagrafspennPar>? Paragrafspenn = null, string? AvgrensningTekst = null,
    string? Objekt = null, string Polaritet = "positiv",
    DateOnly? GyldigFra = null, DateOnly? GyldigTil = null, string? Kommentar = null,
    string Status = Strukturkanter.Godkjent, string? AiForslagVersjon = null, string? OppdagelsesKilde = null,
    string? KildeUtenforKorpusType = null, string? KildeUtenforKorpusDokumentasjon = null,
    // [Ny, issue #341, 2026-10-08] Bare på K — se StrukturkantEntitet.Normform/Grunnlag. Null = ikke angitt.
    // [ENDRET, issue #367 / #335] delegerbar (bool) → delegeringsadgang + slutning (StrukturkantEntitet.Delegeringsadgang/DelegeringSlutning).
    string? Normform = null, string? Grunnlag = null, string? Delegeringsadgang = null, string? DelegeringSlutning = null,
    // [Ny, issue #352] Bare på K oppnevning/overproving — se StrukturkantEntitet.Undertype. Null = ikke angitt. [ENDRET, #355] + avsetting og vedtak.
    string? Undertype = null,
    // [Ny, issue #353] Bare på P: skal | kan | bor — se StrukturkantEntitet.Modalitet. Null = ikke angitt.
    string? Modalitet = null,
    // [Ny, issue #367] Utsagnsstatus: hvordan utsagnet er kommet fram, begrunnelsen, tolkningsargumentene og premissene (påkrevd ved
    // avledet). Null = ikke vurdert. Se StrukturkantEntitet.Kunnskapsstatus.
    string? Kunnskapsstatus = null, string? Begrunnelse = null, IReadOnlyList<string>? Tolkningsargumenter = null,
    IReadOnlyList<Guid>? Premisser = null,
    // [Ny, issue #395] ELI-en til loven når objektet er en lov (ressortfordelingen). Se StrukturkantEntitet.ObjektEli.
    string? ObjektEli = null);

/// <summary>Resultatet av <see cref="StrukturkantTjeneste.OpprettAsync"/> — <see cref="VarNy"/> = false betyr
/// at et identisk utsagn alt fantes og ble returnert uendret (idempotens, se metoden).</summary>
public sealed record StrukturkantOpprettet(StrukturkantEntitet Kant, bool VarNy);

/// <summary>[Ny, issue #311] En node slik den vises: hva den er, og det lesbare navnet.</summary>
/// <param name="Type"><c>'virksomhet'</c> | <c>'begrep'</c>.</param>
/// <param name="Nodetype">Aktørtypen for en virksomhet (<see cref="Virksomhet.Aktortype"/>, kan være NULL =
/// uavklart) eller begrepskategorien for et begrep (klasse/rolle/omrade/gruppe).</param>
public sealed record KantnodeVisning(string Type, Guid Id, string Navn, string? Nodetype);

/// <summary>
/// [Ny, issue #311] Én strukturkant med navn og ferdig beregnet visningstekst. Når den er hentet FOR en
/// node, sier <see cref="Retning"/> om noden er fra- eller til-siden, og <see cref="Visningstekst"/> er
/// beregnet fra den sidens mal (docs/29 §Del C — samme rad gir bevisst ulik tekst avhengig av hvor man spør
/// fra). Uten node (f.eks. per hjemmel) er <see cref="Retning"/> null og teksten «Fra + Fra-mal(Til)».
/// </summary>
public sealed record StrukturkantVisning(
    Guid Id, string Kategori, string Typekode, string? Retning, string Visningstekst,
    KantnodeVisning Fra, KantnodeVisning? Til, string? Objekt,
    IReadOnlyList<ParagrafspennPar> Paragrafspenn, string? AvgrensningTekst, string Polaritet,
    Guid? HjemmelRettskildeId, string? HjemmelRettskildeTittel, string? HjemmelEid,
    string? KildeUtenforKorpusTekst, string? KildeUtenforKorpusLenke, string? KildeUtenforKorpusType,
    string? KildeUtenforKorpusDokumentasjon,
    DateOnly? GyldigFra, DateOnly? GyldigTil, string Status, string OppdagelsesKilde, string? Kommentar,
    string OpprettetAv, DateTimeOffset OpprettetTidspunkt,
    // [Ny, issue #341] K-feltene. Selvregulering = normgivning der til = fra (avledet, Johanns beslutning P2).
    // [ENDRET, issue #367 / #335] delegerbar → delegeringsadgang (etter teksten) + slutning (alltid merket slutning).
    string? Normform = null, string? Grunnlag = null, string? Delegeringsadgang = null, string? DelegeringSlutning = null,
    bool Selvregulering = false,
    // [Ny, issue #341, Johanns hierarkibeslutning] Fra typekonfigurasjonen: familien og fvl-kategorien (for normgivning
    // avledet av normformen, Strukturkanter.FvlKategoriFor).
    string? Familie = null, string? FvlKategori = null,
    // [Ny, issue #352] Undertypen (valg/ansettelse/utpeking/oppnevning på oppnevning, anke på overprøving). Null = ikke angitt.
    string? Undertype = null,
    // [Ny, issue #353] Modaliteten på en plikt (skal/kan/bor). Null = ikke angitt.
    string? Modalitet = null,
    // [Ny, issue #367] Utsagnsstatus. Status over er GODKJENNING; Kunnskapsstatus er metoden (null = ikke vurdert).
    string? Kunnskapsstatus = null, string? Begrunnelse = null, IReadOnlyList<string>? Tolkningsargumenter = null,
    IReadOnlyList<Guid>? PremissIder = null, string Entitetsstatus = "gjeldende", Guid? ErstatterKantId = null,
    string? Profilversjon = null,
    // [Ny, issue #367 AC4] Den versjonerte hjemmelnoden, merknaden når den mangler («hjemmelversjon ukjent») og resynk-flagget.
    Guid? HjemmelNodeId = null, string? HjemmelVersjonMerknad = null, DateTimeOffset? HjemmelEndretTidspunkt = null)
{
    /// <summary>[Ny, issue #313 / #374] Hvem som sist endret godkjenningen (godkjente eller avviste), og når. Ved <c>avvist</c> er det
    /// alltid et menneske (CHECK <c>ck_strukturkanter_avvist_hvem</c>).</summary>
    public string? SistEndretAv { get; init; }
    public DateTimeOffset? SistEndretTidspunkt { get; init; }
    /// <summary>[Ny, issue #313 / #374] Den valgfrie begrunnelsen for en avvisning.</summary>
    public string? AvvisningBegrunnelse { get; init; }
    /// <summary>[Ny, issue #397, Johann 2026-10-24 på #134] Omfanget av en avledet R etterfolger: antall lovdeler som gikk over, talt fra
    /// premissene (forvaltningskantene FRA etterfølgeren). Ikke et felt for hoved- eller deletterfølger, og ikke lagret: det følger av
    /// premissene. Null for alle andre kanter.</summary>
    public int? Omfang { get; init; }
}

/// <summary>[Ny, issue #313] Forhåndsvisningen av ett forslag: slik det blir seende ut på fra-nodens side, på til-nodens side (null uten
/// til-node, eller når til = fra) og på hjemmelens rettskildeside — samme visningstekst som sidene selv beregner (<see cref="StrukturkantTjeneste.ByggVisningerAsync"/>).</summary>
public sealed record StrukturkantForhandsvisning(StrukturkantVisning FraSide, StrukturkantVisning? TilSide, StrukturkantVisning Rettskildeside);

/// <summary>
/// [Ny, issue #311 «Strukturmodell 6: én typestyrt kanttabell», 2026-10-07] Den ENESTE skriveveien til
/// <see cref="StrukturkantEntitet"/> — erstatter <c>VirksomhetRelasjonregisterTjeneste</c>,
/// <c>GruppeMedlemskapTjeneste</c> og <c>MyndighetstildelingTjeneste</c> (Johanns valg A: full
/// konsolidering, ingen parallell skrivevei). API-endepunktene, veiviseren
/// (<see cref="NavnekandidatOppdagelseTjeneste"/>), KI-oppdagelsen
/// (<see cref="VirksomhetOgGruppeKiOppdagelseTjeneste"/>) og <see cref="SamiskSprakforvaltningSeed"/> kaller
/// alle <see cref="OpprettAsync"/>/<see cref="OpprettTildelingAsync"/> her.
/// <para>
/// Valideringen er den samme «ingen gjettet fallback»-linjen de tre gamle tjenestene hadde, nå felles:
/// typekode for kategorien må finnes, nodene må finnes og ha lovlig type for kategorien
/// (<see cref="Strukturkanter.Noderegler"/>), hjemmel og alle eId-er i avgrensningen må finnes, og
/// M/O-kanter kan ikke lukke en sykel (bevart fra gruppe-av-gruppe, issue #164).
/// </para>
/// </summary>
public sealed partial class StrukturkantTjeneste(RegelIdeDbContext db)
{
    /// <summary>Entitetstypen i <see cref="ProveniensEntitet.EntitetType"/> for kanter. (De gamle radene fra
    /// før #311 har <c>myndighetstildeling</c>/<c>virksomhet_relasjon</c>/<c>gruppe_medlemskap</c> og SAMME
    /// <see cref="ProveniensEntitet.EntitetId"/> — kantene beholdt id-ene sine i migrasjonen.)</summary>
    public const string ProveniensType = "strukturkant";

    // [ENDRET, issue #387] + fasit:<fil>:<id> — utsagn lastet fra data/fasit/strukturmodell (faglig vurdert, ikke uavhengig kontrollert).
    [GeneratedRegex(@"^(manuell|monster:\S+|ki:\S+|fasit:\S+)$", RegexOptions.CultureInvariant)]
    private static partial Regex OppdagelsesKildeForm();

    /// <summary>[Ny, issue #313 / #374] De gjeldende kantene MED de avviste — global spørrefilter skjuler avviste (historikk, ikke slettet).
    /// Brukes av «vis avviste», av oppslaget på én id og av identitetssjekken for maskinelle forslag («tidligere avvist»).</summary>
    public IQueryable<StrukturkantEntitet> GjeldendeInklAvviste =>
        db.Strukturkanter.IgnoreQueryFilters().Where(k => k.Entitetsstatus == Strukturkanter.Gjeldende);

    /// <summary>
    /// Oppretter en kant — eller returnerer en EKSISTERENDE identisk kant (samme kategori, typekode, fra,
    /// til, objekt, polaritet, hjemmel og avgrensning) uendret. Idempotensen er det
    /// <see cref="SamiskSprakforvaltningSeed"/>, veiviseren og KI-oppdagelsen hviler på for å kunne kjøres
    /// om igjen. (Før #311 hadde hver gamle tabell sin egen dublettregel; gruppemedlemskap var unikt på
    /// PARET uansett hjemmel. Med avgrensning og polaritet på kanten er to kanter med ulik hjemmel/
    /// avgrensning to ulike utsagn — «klageinstans etter § 3» og «etter § 5» — og lagres hver for seg.)
    /// </summary>
    public Task<StrukturkantOpprettet> OpprettAsync(NyStrukturkant ny, string opprettetAv, CancellationToken ct = default) =>
        OpprettAsync(ny, opprettetAv, erstatter: null, ct);

    private async Task<StrukturkantOpprettet> OpprettAsync(NyStrukturkant ny, string opprettetAv, StrukturkantEntitet? erstatter, CancellationToken ct)
    {
        // ---- Lukkede vokabular ----
        if (!Strukturkanter.ErGyldigKategori(ny.Kategori))
        {
            throw new ArgumentException(
                $"Ukjent kategori '{ny.Kategori}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Kategorier)}. Ingen gjettet fallback.");
        }
        if (!Strukturkanter.Polariteter.Contains(ny.Polaritet))
        {
            throw new ArgumentException($"Ugyldig polaritet '{ny.Polaritet}'. Gyldige verdier: positiv, negativ. Ingen standardverdi gjettes.");
        }
        if (!Strukturkanter.Statuser.Contains(ny.Status))
        {
            throw new ArgumentException($"Ugyldig status '{ny.Status}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Statuser)}. Ingen gjettet fallback.");
        }
        var oppdagelsesKilde = ny.OppdagelsesKilde
            ?? (ny.Status == Strukturkanter.MaskineltForeslatt && ny.AiForslagVersjon is not null ? $"ki:{ny.AiForslagVersjon}" : null)
            ?? (ny.Status != Strukturkanter.MaskineltForeslatt ? "manuell" : null);
        if (oppdagelsesKilde is null)
        {
            throw new ArgumentException(
                "Et maskinelt forslag (status 'maskinelt_foreslatt') må ha aiForslagVersjon eller oppdagelsesKilde. Ingen gjettet fallback.");
        }
        if (!OppdagelsesKildeForm().IsMatch(oppdagelsesKilde))
        {
            throw new ArgumentException(
                $"Ugyldig oppdagelseskilde '{oppdagelsesKilde}' — må være 'manuell', 'monster:<id>', 'ki:<modell>' eller 'fasit:<fil>:<id>' (docs/33 §4.3).");
        }

        var typeFinnes = await db.RelasjonsTypeKonfigurasjoner.AnyAsync(
            k => k.Kategori == ny.Kategori && k.Kode == ny.Typekode && k.Aktiv, ct);
        if (!typeFinnes)
        {
            throw new ArgumentException(
                $"Ukjent typekode '{ny.Typekode}' for kategori {ny.Kategori} ({Strukturkanter.Visningsnavn(ny.Kategori)}). "
                + "En ny type er en rad i relasjonstype_konfigurasjon, ikke en gjettet verdi.");
        }

        // ---- Noder ----
        var regel = Strukturkanter.Noderegler[ny.Kategori];
        if (!ny.Fra.ErGyldig)
        {
            throw new ArgumentException("Fra-noden må være NØYAKTIG én av virksomhet eller begrep.");
        }
        if (ny.Til is not null && !ny.Til.ErGyldig)
        {
            throw new ArgumentException("Til-noden må være NØYAKTIG én av virksomhet eller begrep (eller utelates helt).");
        }
        if (ny.Til is null && !regel.TilValgfri)
        {
            throw new ArgumentException(
                $"Kategori {ny.Kategori} ({Strukturkanter.Visningsnavn(ny.Kategori)}) krever en til-node ({regel.TilBeskrivelse}).");
        }
        // [ENDRET, issue #341] Unntaket er selvregulering: normgivning der motparten er innehaveren selv (Johanns
        // beslutning P2 — «normgivning der B = A»). Samme unntak som CHECK ck_strukturkanter_ikke_selv.
        var erSelvregulering = ny.Kategori == Strukturkanter.Kompetanse && ny.Typekode == Strukturkanter.Normgivning;
        // [Ny, issue #353] Og en plikt mellom medlemmer av SAMME klasse/rolle (begrep-ende) — se ck_strukturkanter_ikke_selv.
        var erPliktInnadIKlasse = ny.Kategori == Strukturkanter.Plikt && ny.Fra.BegrepId is not null;
        if (ny.Til is not null && ny.Til == ny.Fra && !erSelvregulering && !erPliktInnadIKlasse)
        {
            throw new ArgumentException(
                "En kant kan ikke gå fra en node til seg selv (unntak: K normgivning = selvregulering, og P mellom medlemmer av "
                + "samme klasse/rolle). Ingen gjettet fallback.");
        }
        var fraNavn = await ValiderNodeAsync(ny.Fra, regel.FraVirksomhet, regel.FraBegrep, "fra", regel.FraBeskrivelse, ny.Kategori, ct);
        var tilNavn = ny.Til is null
            ? null
            : await ValiderNodeAsync(ny.Til, regel.TilVirksomhet, regel.TilBegrep, "til", regel.TilBeskrivelse, ny.Kategori, ct);
        await ValiderOrdningsreglerAsync(ny, ct); // [Ny, issue #353]

        var objekt = string.IsNullOrWhiteSpace(ny.Objekt) ? null : ny.Objekt.Trim();
        var paragrafspenn = ny.Paragrafspenn ?? [];
        if (ny.Kategori == Strukturkanter.Kompetanse && ny.Til is null && objekt is null
            && paragrafspenn.Count == 0 && ny.HjemmelEid is null)
        {
            throw new ArgumentException(
                "En kompetansekant uten til-node må si HVA kompetansen gjelder: en sakstype (objekt), et paragrafspenn "
                + "eller en hjemmel-eId (docs/33 §4.3: «aktør/rolle → bestemmelse eller sakstype»).");
        }
        // [Ny, issue #353] Samme krav for en plikt uten motpart («Folketrygden skal dekke behandlings- og forpleiningsutgifter
        // …» — objektet er utgiftene, mottakeren står ikke i teksten).
        if (ny.Kategori == Strukturkanter.Plikt && ny.Til is null && objekt is null
            && paragrafspenn.Count == 0 && ny.HjemmelEid is null)
        {
            throw new ArgumentException(
                "En pliktkant uten motpart må si HVA plikten gjelder: et objekt (f.eks. «behandlings- og forpleiningsutgifter»), "
                + "et paragrafspenn eller en hjemmel-eId (issue #353).");
        }
        // [Ny, issue #368] En oppgave ER objektet («beredskapsarbeid», «fortegnelse over valgte meddommere») — uten det sier kanten
        // bare at aktøren har «en oppgave». Påkrevd også med til-node (CHECK ck_strukturkanter_oppgave_objekt).
        if (ny.Kategori == Strukturkanter.Oppgave && objekt is null)
        {
            throw new ArgumentException(
                "En oppgavekant må si HVA oppgaven er (objekt, f.eks. «beredskapsarbeid»). Begrepsmanualen §F (issue #368).");
        }
        if (ny.Kategori == Strukturkanter.Klasseniva && ny.Til is null && objekt is null)
        {
            throw new ArgumentException("En klassenivå-kant uten til-node må ha et objekt (f.eks. «kommunestyre»).");
        }
        var (normform, grunnlag, undertype) = ValiderKompetansefelt(ny.Kategori, ny.Typekode, ny.Normform, ny.Grunnlag, ny.Undertype);
        var (delegeringsadgang, delegeringSlutning) = ValiderDelegering(ny.Kategori, ny.Delegeringsadgang, ny.DelegeringSlutning); // [Ny, #367]
        var modalitet = ValiderModalitet(ny.Kategori, ny.Modalitet); // [Ny, issue #353]

        // ---- Kilde ----
        var kildeTekst = string.IsNullOrWhiteSpace(ny.KildeUtenforKorpusTekst) ? null : ny.KildeUtenforKorpusTekst.Trim();
        var kildeLenke = string.IsNullOrWhiteSpace(ny.KildeUtenforKorpusLenke) ? null : ny.KildeUtenforKorpusLenke.Trim();
        if (ny.HjemmelRettskildeId is null && kildeTekst is null)
        {
            throw new ArgumentException(
                "Kanten må ha en hjemmel (rettskilde) ELLER en kilde utenfor korpus (docs/33 §4.3). Ingen gjettet fallback.");
        }
        if (kildeLenke is not null && kildeTekst is null)
        {
            throw new ArgumentException("En lenke til kilde utenfor korpus må ha en tekst som sier hva kilden er.");
        }
        // [Ny, Johanns beslutning 2026-10-07] Typen på kilden utenfor korpus: påkrevd uten hjemmel, NULL med.
        var kildeType = string.IsNullOrWhiteSpace(ny.KildeUtenforKorpusType) ? null : ny.KildeUtenforKorpusType.Trim();
        var kildeDok = string.IsNullOrWhiteSpace(ny.KildeUtenforKorpusDokumentasjon) ? null : ny.KildeUtenforKorpusDokumentasjon.Trim();
        if (ny.HjemmelRettskildeId is not null
            && (kildeTekst is not null || kildeLenke is not null || kildeType is not null || kildeDok is not null))
        {
            throw new ArgumentException(
                "Kanten har hjemmel i korpus OG en kilde utenfor korpus — oppgi én av dem (docs/33 §4.3). "
                + "En utfyllende merknad hører i kommentaren.");
        }
        if (ny.HjemmelRettskildeId is null)
        {
            if (kildeType is null)
            {
                throw new ArgumentException(
                    $"Kilde utenfor korpus må ha en type ({string.Join(", ", Strukturkanter.KildeUtenforKorpusTyper)}). "
                    + "Ingen gjettet fallback.");
            }
            if (!Strukturkanter.KildeUtenforKorpusTyper.Contains(kildeType))
            {
                throw new ArgumentException(
                    $"Ukjent kildetype '{kildeType}'. Gyldige verdier: {string.Join(", ", Strukturkanter.KildeUtenforKorpusTyper)}.");
            }
            // [Ny, Johanns beslutning 2026-10-07] Primær (selve kilden) eller sekundær (en tekst som refererer den).
            if (kildeDok is null || !Strukturkanter.KildeDokumentasjoner.Contains(kildeDok))
            {
                throw new ArgumentException(
                    $"Kilde utenfor korpus må si om dokumentasjonen er 'primaer' (selve kilden) eller 'sekundaer' "
                    + $"(en tekst som refererer den){(kildeDok is null ? "" : $" — '{kildeDok}' er ukjent")}. Ingen gjettet fallback.");
            }
        }
        if (ny.HjemmelEid is not null && ny.HjemmelRettskildeId is null)
        {
            throw new ArgumentException("HjemmelEid uten HjemmelRettskildeId — eId-en er bare unik innenfor sin rettskilde.");
        }
        if (ny.HjemmelRettskildeId is { } hjemmelId)
        {
            if (!await db.Rettskilder.AnyAsync(r => r.Id == hjemmelId, ct))
            {
                throw new ArgumentException($"Fant ingen rettskilde med id '{hjemmelId}'. Ingen gjettet fallback.");
            }
            if (ny.HjemmelEid is not null
                && !await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == hjemmelId && n.Eid == ny.HjemmelEid, ct))
            {
                throw new ArgumentException(
                    $"Fant ingen node med eId '{ny.HjemmelEid}' i hjemmelen. Ingen gjettet fallback.");
            }
        }
        // [Ny, issue #367 AC4] Den versjonerte noden — bare ved nøyaktig én gjeldende node med eId-en (samme regel som migrasjonen).
        Guid? hjemmelNodeId = null;
        if (ny.HjemmelRettskildeId is { } hId && ny.HjemmelEid is not null)
        {
            var noder = await db.RettskildeNoder
                .Where(n => n.RettskildeId == hId && n.Eid == ny.HjemmelEid && n.Entitetsstatus == "gjeldende")
                .Select(n => n.Id).Take(2).ToListAsync(ct);
            if (noder.Count == 1) hjemmelNodeId = noder[0];
        }

        // ---- [Ny, issue #367 AC1/AC3] Kunnskapsstatus og grunnlaget den krever (docs/profil/03 §5) ----
        var (kunnskapsstatus, begrunnelse, argumenter, premisser) = await ValiderKunnskapsstatusAsync(ny, kildeTekst, ct);

        // ---- Hjemmelssted og avgrensning ----
        // [ENDRET, issue #341] M/I med hjemmel i korpus krever HVOR det står (hjemmel-eId), ikke et avgrensningsspenn —
        // se Strukturkanter.KreverHjemmelsted for sammenblandingen #311 innførte.
        if (Strukturkanter.KreverHjemmelsted.Contains(ny.Kategori) && ny.HjemmelRettskildeId is not null && ny.HjemmelEid is null)
        {
            throw new ArgumentException(
                $"Kategori {ny.Kategori} ({Strukturkanter.Visningsnavn(ny.Kategori)}) med hjemmel i korpus krever hjemmel-eId — "
                + "noden der tildelingen står. Ingen gjettet fallback (issue #341).");
        }
        await ValiderParagrafspennAsync(paragrafspenn, ct);
        if (ny.GyldigFra is not null && ny.GyldigTil is not null && ny.GyldigFra.Value > ny.GyldigTil.Value)
        {
            throw new ArgumentException("GyldigFra kan ikke være etter GyldigTil. Ingen gjettet fallback.");
        }
        var spennJson = JsonSerializer.Serialize(paragrafspenn, JsonSerialiseringHjelper.Innstillinger);
        var avgrensningTekst = string.IsNullOrWhiteSpace(ny.AvgrensningTekst) ? null : ny.AvgrensningTekst.Trim();

        // ---- Idempotens ----
        var tilV = ny.Til?.VirksomhetId;
        var tilB = ny.Til?.BegrepId;
        // [ENDRET, issue #341] Hjemmelssted og normform er nå en del av utsagnets identitet («kan gi forskrift» og «kan gi
        // reglement» er to utsagn; samme tildeling hjemlet i § 1 og § 2 også). Delegerbar og grunnlag er det IKKE —
        // to rader som bare skiller seg der, er samme utsagn med motstridende opplysning, og det avvises under.
        // [ENDRET, issue #367 AC3] Gjelder bare gjeldende rader (global spørrefilter), og ikke for omstridte utsagn: to konkurrerende
        // tolkninger av samme tekst skal kunne stå side om side.
        // [ENDRET, issue #313 / #374] Et MASKINELT forslag som er identisk med et avvist, returnerer den avviste raden (VarNy = false):
        // det foreslås ikke på nytt i stillhet. Et menneske som registrerer det samme, får en ny rad — avvisningen gjaldt forslaget.
        var identitetsgrunnlag = ny.Status == Strukturkanter.MaskineltForeslatt ? GjeldendeInklAvviste : db.Strukturkanter;
        var eksisterende = kunnskapsstatus == Strukturkanter.Omstridt ? null : await identitetsgrunnlag.FirstOrDefaultAsync(k =>
            k.Kategori == ny.Kategori && k.Typekode == ny.Typekode
            && k.FraVirksomhetId == ny.Fra.VirksomhetId && k.FraBegrepId == ny.Fra.BegrepId
            && k.TilVirksomhetId == tilV && k.TilBegrepId == tilB
            && k.Objekt == objekt && k.Polaritet == ny.Polaritet
            && k.HjemmelRettskildeId == ny.HjemmelRettskildeId && k.HjemmelEid == ny.HjemmelEid
            && k.Normform == normform
            // [Ny, issue #352] Undertypen er også identitet: «velger» og «ansetter» samme motpart er to utsagn.
            && k.Undertype == undertype
            // [Ny, issue #353] Modaliteten også: «skal samarbeide» og «kan samarbeide» er to utsagn.
            && k.Modalitet == modalitet
            && k.AvgrensningParagrafspennJson == spennJson, ct);
        if (eksisterende is not null)
        {
            if ((delegeringsadgang is not null && eksisterende.Delegeringsadgang is not null && delegeringsadgang != eksisterende.Delegeringsadgang)
                || (delegeringSlutning is not null && eksisterende.DelegeringSlutning is not null && delegeringSlutning != eksisterende.DelegeringSlutning)
                || (grunnlag is not null && eksisterende.Grunnlag is not null && grunnlag != eksisterende.Grunnlag)
                || (kunnskapsstatus is not null && eksisterende.Kunnskapsstatus is not null && kunnskapsstatus != eksisterende.Kunnskapsstatus))
            {
                throw new ArgumentException(
                    "Det finnes alt en identisk kant med en annen verdi for delegeringsadgang/grunnlag/kunnskapsstatus. Revider den "
                    + "eksisterende kanten i stedet (en revisjon gir en ny rad) — ingen gjettet sammenslåing.");
            }
            return new StrukturkantOpprettet(eksisterende, false);
        }

        // ---- Sykel ----
        if (Strukturkanter.SykelfrieKategorier.Contains(ny.Kategori) && ny.Fra.BegrepId is { } fraB && tilB is { } tilBegrep)
        {
            await KastHvisSykelAsync(ny.Kategori, fraB, tilBegrep, fraNavn, tilNavn!, ct);
        }

        var kant = new StrukturkantEntitet
        {
            Id = Guid.NewGuid(),
            Kategori = ny.Kategori,
            Typekode = ny.Typekode,
            FraVirksomhetId = ny.Fra.VirksomhetId,
            FraBegrepId = ny.Fra.BegrepId,
            TilVirksomhetId = tilV,
            TilBegrepId = tilB,
            Objekt = objekt,
            ObjektEli = string.IsNullOrWhiteSpace(ny.ObjektEli) ? null : ny.ObjektEli.Trim(), // [Ny, issue #395]
            AvgrensningParagrafspennJson = spennJson,
            AvgrensningTekst = avgrensningTekst,
            Polaritet = ny.Polaritet,
            HjemmelRettskildeId = ny.HjemmelRettskildeId,
            HjemmelEid = ny.HjemmelEid,
            KildeUtenforKorpusTekst = kildeTekst,
            KildeUtenforKorpusLenke = kildeLenke,
            KildeUtenforKorpusType = kildeType,
            KildeUtenforKorpusDokumentasjon = kildeDok,
            Normform = normform,
            Undertype = undertype,
            Modalitet = modalitet,
            Grunnlag = grunnlag,
            Delegeringsadgang = delegeringsadgang,
            DelegeringSlutning = delegeringSlutning,
            Kunnskapsstatus = kunnskapsstatus,
            Begrunnelse = begrunnelse,
            Tolkningsargumenter = argumenter,
            Profilversjon = kunnskapsstatus is null ? null : Strukturkanter.Profilversjon,
            ErstatterKantId = erstatter?.Id,
            HjemmelNodeId = hjemmelNodeId,
            GyldigFra = ny.GyldigFra,
            GyldigTil = ny.GyldigTil,
            Status = ny.Status,
            OppdagelsesKilde = oppdagelsesKilde,
            Kommentar = string.IsNullOrWhiteSpace(ny.Kommentar) ? null : ny.Kommentar.Trim(),
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Strukturkanter.Add(kant);
        foreach (var premiss in premisser)
        {
            db.StrukturkantPremisser.Add(new StrukturkantPremissEntitet { KantId = kant.Id, PremissKantId = premiss });
        }
        // Delt, nasjonal referansedata — proveniensen er ikke attribuert til en virksomhet (samme som
        // myndighetstildeling før #311).
        db.Proveniens.Add(ny.Status == Strukturkanter.MaskineltForeslatt
            ? ProveniensHjelper.NyForslagRad(ProveniensType, kant.Id, virksomhetId: null, opprettetAv, ny.AiForslagVersjon ?? oppdagelsesKilde)
            : ProveniensHjelper.NyRad(ProveniensType, kant.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return new StrukturkantOpprettet(kant, true);
    }

    /// <summary>
    /// [Ny, issue #367 AC3, docs/34 §4] Reviderer en kant: <paramref name="ny"/> blir en NY rad med <c>erstatter_kant_id</c> = den gamle, og
    /// den gamle blir <c>foreldet</c> — den slettes ikke (PROV-O <c>wasRevisionOf</c>). Samme validering som <see cref="OpprettAsync"/>;
    /// feiler den, står den gamle raden uendret (én transaksjon). Bare en gjeldende kant kan revideres.
    /// </summary>
    /// <returns>Den nye raden, eller null hvis id-en ikke finnes blant de gjeldende kantene.</returns>
    public async Task<StrukturkantEntitet?> ReviderAsync(Guid id, NyStrukturkant ny, string revidertAv, CancellationToken ct = default)
    {
        var gammel = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (gammel is null) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            return await ReviderITransaksjonAsync(gammel, ny, revidertAv, ct, tx);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear(); // den gamle raden er merket foreldet i minnet — ingenting av det skal leve videre
            throw;
        }
    }

    private async Task<StrukturkantEntitet> ReviderITransaksjonAsync(StrukturkantEntitet gammel, NyStrukturkant ny, string revidertAv,
        CancellationToken ct, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx)
    {
        gammel.Entitetsstatus = Strukturkanter.Foreldet;
        gammel.SistEndretAv = revidertAv;
        gammel.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var prov = ProveniensHjelper.NyRad(ProveniensType, gammel.Id, virksomhetId: null, "endret", revidertAv);
        prov.KildeReferanserJson = JsonSerializer.Serialize(new { felt = "entitetsstatus", issue = 367, nyStatus = Strukturkanter.Foreldet });
        db.Proveniens.Add(prov);
        await db.SaveChangesAsync(ct);
        var ny2 = await OpprettAsync(ny, revidertAv, gammel, ct);
        if (!ny2.VarNy)
        {
            throw new ArgumentException("Revisjonen er identisk med en annen gjeldende kant. Ingenting er endret.");
        }
        await tx.CommitAsync(ct);
        return ny2.Kant;
    }

    /// <summary>
    /// «Tildeling»: en virksomhet knyttes til et begrep med gruppefunksjon. Kategorien UTLEDES av begrepets
    /// nodetype — samme regel som datamigreringen i #311: mål = rolle → <c>I innehar</c> (rolleinnehav arves
    /// ikke, docs/33 §4.2), ellers → <c>M medlem_av</c>. Brukt av veiviseren, KI-oppdagelsen og samisk-seeden,
    /// som alle før #311 skrev en myndighetstildeling.
    /// <para>
    /// [ENDRET, issue #341, 2026-10-08] <paramref name="hjemmelEid"/> er HVOR tildelingen står (noden i hjemmelen —
    /// for veiviseren og KI-oppdagelsen kandidatens egen node). Før #341 sendte alle kallerne den noden som
    /// avgrensningsspenn; <paramref name="avgrensning"/> er nå bare «hvilke paragrafer tildelingen gjelder for»
    /// (#314), og er tom med mindre kalleren faktisk vet det.
    /// </para>
    /// </summary>
    public async Task<StrukturkantOpprettet> OpprettTildelingAsync(
        Guid virksomhetId, Guid begrepId, Guid hjemmelRettskildeId, string hjemmelEid,
        IReadOnlyList<ParagrafspennPar>? avgrensning, string? avgrensningTekst, string opprettetAv,
        DateOnly? gyldigFra = null, DateOnly? gyldigTil = null,
        CancellationToken ct = default, string status = Strukturkanter.Godkjent, string? aiForslagVersjon = null)
    {
        var (kategori, typekode) = await TildelingskategoriAsync(begrepId, ct);
        return await OpprettAsync(new NyStrukturkant(
            kategori, typekode, Kantnode.Virksomhet(virksomhetId), Kantnode.Begrep(begrepId),
            HjemmelRettskildeId: hjemmelRettskildeId, HjemmelEid: hjemmelEid, Paragrafspenn: avgrensning,
            AvgrensningTekst: avgrensningTekst,
            GyldigFra: gyldigFra, GyldigTil: gyldigTil, Status: status, AiForslagVersjon: aiForslagVersjon), opprettetAv, ct);
    }

    /// <summary>Kategori/typekode for en tildeling til <paramref name="begrepId"/> — se <see cref="OpprettTildelingAsync"/>.</summary>
    public async Task<(string Kategori, string Typekode)> TildelingskategoriAsync(Guid begrepId, CancellationToken ct = default)
    {
        var kategori = await db.Begreper.Where(b => b.Id == begrepId && b.Entitetsstatus == "gjeldende")
            .Select(b => b.Begrepskategori).FirstOrDefaultAsync(ct);
        if (!Nodetyper.HarGruppefunksjon(kategori))
        {
            throw new ArgumentException($"Fant ingen gjeldende begrep med gruppefunksjon med id '{begrepId}'. Ingen gjettet fallback.");
        }
        return kategori == Nodetyper.Rolle
            ? (Strukturkanter.Rolleinnehav, Strukturkanter.Innehar)
            : (Strukturkanter.Medlemskap, Strukturkanter.MedlemAv);
    }

    /// <summary>
    /// [Ny, issue #330, 2026-10-08] Erstatter avgrensningen (paragrafspenn + tekst) på en EKSISTERENDE kant. Før
    /// #330 fantes ingen oppdateringsvei — en kant som var riktig i retning men manglet avgrensning
    /// (Energiklagenemnda-raden: departementet er klageinstans bare for «enkeltvedtak Energiklagenemnda treffer i
    /// første instans», forskrift om Energiklagenemnda § 1 annet ledd) kunne bare rettes med SQL eller ved å slette
    /// og registrere på nytt, som mister id og proveniens. Johann (2026-10-08): rettelsen gjøres via API-et, ikke
    /// ved å gjette i en migrasjon.
    /// <para>
    /// Samme validering som <see cref="OpprettAsync"/>: hver eId i spennet må finnes i korpus. [ENDRET, #341] M/I krever
    /// ikke lenger et spenn — kravet gjelder hjemmelsstedet (<see cref="Strukturkanter.KreverHjemmelsted"/>), som denne
    /// metoden ikke rører. Begge feltene ERSTATTES — tomt spenn og
    /// null/blank tekst fjerner dem; det er ingen «behold det som står»-verdi å gjette på. Finnes det fra før en
    /// ANNEN kant med samme identitet som kanten ville fått (idempotensnøkkelen i <see cref="OpprettAsync"/>, der
    /// spennet inngår), avvises endringen: to rader for samme utsagn er nettopp det idempotensen skal hindre.
    /// Gammel og ny verdi logges i Proveniens (<c>handling = 'endret'</c>).
    /// </para>
    /// </summary>
    /// <returns>Kanten, eller null hvis id-en ikke finnes.</returns>
    public async Task<StrukturkantEntitet?> OppdaterAvgrensningAsync(
        Guid id, IReadOnlyList<ParagrafspennPar>? paragrafspenn, string? avgrensningTekst, string endretAv,
        CancellationToken ct = default)
    {
        var kant = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kant is null) return null;

        var spenn = paragrafspenn ?? [];
        await ValiderParagrafspennAsync(spenn, ct);
        var spennJson = JsonSerializer.Serialize(spenn, JsonSerialiseringHjelper.Innstillinger);
        var tekst = string.IsNullOrWhiteSpace(avgrensningTekst) ? null : avgrensningTekst.Trim();

        if (spennJson == kant.AvgrensningParagrafspennJson && tekst == kant.AvgrensningTekst) return kant;

        var dublett = await db.Strukturkanter.AnyAsync(k =>
            k.Id != kant.Id
            && k.Kategori == kant.Kategori && k.Typekode == kant.Typekode
            && k.FraVirksomhetId == kant.FraVirksomhetId && k.FraBegrepId == kant.FraBegrepId
            && k.TilVirksomhetId == kant.TilVirksomhetId && k.TilBegrepId == kant.TilBegrepId
            && k.Objekt == kant.Objekt && k.Polaritet == kant.Polaritet
            && k.HjemmelRettskildeId == kant.HjemmelRettskildeId && k.HjemmelEid == kant.HjemmelEid
            && k.Normform == kant.Normform
            && k.Undertype == kant.Undertype
            && k.Modalitet == kant.Modalitet
            && k.AvgrensningParagrafspennJson == spennJson, ct);
        if (dublett)
        {
            throw new ArgumentException(
                "Det finnes alt en kant med samme type, ender, hjemmel og paragrafspenn — endringen ville gitt to rader "
                + "for samme utsagn. Ingen gjettet sammenslåing.");
        }

        var proveniens = ProveniensHjelper.NyRad(ProveniensType, kant.Id, virksomhetId: null, "endret", endretAv);
        proveniens.KildeReferanserJson = JsonSerializer.Serialize(new
        {
            felt = "avgrensning",
            forParagrafspenn = JsonSerializer.Deserialize<JsonElement>(kant.AvgrensningParagrafspennJson),
            forAvgrensningTekst = kant.AvgrensningTekst,
            nyttParagrafspenn = JsonSerializer.Deserialize<JsonElement>(spennJson),
            nyAvgrensningTekst = tekst,
        });
        kant.AvgrensningParagrafspennJson = spennJson;
        kant.AvgrensningTekst = tekst;
        kant.SistEndretAv = endretAv;
        kant.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(proveniens);
        await db.SaveChangesAsync(ct);
        return kant;
    }

    /// <summary>Et menneske bekrefter et forslag (et av <see cref="Strukturkanter.Ventende"/> → <c>godkjent</c>; før #367
    /// <c>foreslatt_av_ai</c> → <c>validert</c>) — samme mønster som de gamle tjenestene (issue #285 AC6). En godkjent eller avvist
    /// kant har ingenting å godkjenne.</summary>
    public async Task<StrukturkantEntitet?> GodkjennAsync(Guid id, string godkjentAv, CancellationToken ct = default)
    {
        var kant = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kant is null) return null;
        if (!Strukturkanter.Ventende.Contains(kant.Status))
        {
            throw new ArgumentException(
                $"Kanten har status '{kant.Status}' — bare forslag ({string.Join(", ", Strukturkanter.Ventende)}) kan godkjennes.");
        }
        kant.Status = Strukturkanter.Godkjent;
        kant.SistEndretAv = godkjentAv;
        kant.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var proveniens = ProveniensHjelper.NyRad(ProveniensType, kant.Id, virksomhetId: null, "validert", godkjentAv);
        proveniens.GodkjentAv = godkjentAv;
        db.Proveniens.Add(proveniens);
        await db.SaveChangesAsync(ct);
        return kant;
    }

    /// <summary>
    /// [Ny, issue #312, Johanns beslutning 2026-10-08] Godkjenner ALLE ventende forslag hjemlet i én rettskilde — én
    /// handling for f.eks. de ~800 domstolkantene <see cref="DomstolinndelingTolker"/> har lest ut av
    /// inndelingsforskriften, som et menneske har kontrollert mot forskriften. Hver kant går gjennom
    /// <see cref="GodkjennAsync"/>, så hver får sin egen proveniensrad (<c>validert</c>, <c>GodkjentAv</c>).
    /// <paramref name="oppdagelseskildePrefiks"/> avgrenser valgfritt til én mekanisme (f.eks. <c>monster:</c>), slik at
    /// KI-forslag med samme hjemmel ikke godkjennes i samme slengen.
    /// </summary>
    /// <returns>Antall kanter som ble godkjent.</returns>
    public async Task<int> GodkjennAlleForHjemmelAsync(
        Guid hjemmelRettskildeId, string? oppdagelseskildePrefiks, string godkjentAv, CancellationToken ct = default)
    {
        var ider = await db.Strukturkanter
            .Where(k => k.Status == Strukturkanter.MaskineltForeslatt && k.HjemmelRettskildeId == hjemmelRettskildeId
                        && (oppdagelseskildePrefiks == null || k.OppdagelsesKilde.StartsWith(oppdagelseskildePrefiks)))
            .Select(k => k.Id).ToListAsync(ct);
        foreach (var id in ider) await GodkjennAsync(id, godkjentAv, ct);
        return ider.Count;
    }

    /// <summary>[Ny, issue #312] Ventende forslag gruppert på hjemmel — det samlet godkjenning trenger å vise
    /// («Godkjenn alle 812 forslag hjemlet i forskrift om inndelingen av rettskretser og lagdømmer»).</summary>
    public async Task<List<(Guid RettskildeId, string Tittel, int Antall)>> ForslagPerHjemmelAsync(CancellationToken ct = default)
    {
        var grupper = await db.Strukturkanter
            .Where(k => Strukturkanter.Ventende.Contains(k.Status) && k.HjemmelRettskildeId != null)
            .GroupBy(k => k.HjemmelRettskildeId!.Value)
            .Select(g => new { Id = g.Key, Antall = g.Count() }).ToListAsync(ct);
        var ider = grupper.Select(g => g.Id).ToList();
        var titler = await db.Rettskilder.Where(r => ider.Contains(r.Id))
            .Select(r => new { r.Id, Tittel = r.Kortnavn ?? r.Tittel }).ToDictionaryAsync(r => r.Id, r => r.Tittel, ct);
        return grupper.Select(g => (g.Id, titler.GetValueOrDefault(g.Id) ?? "(ukjent rettskilde)", g.Antall))
            .OrderByDescending(g => g.Antall).ToList();
    }

    /// <summary>
    /// [Ny, issue #312, Johanns beslutning 2026-10-08] Gjør en VALIDERT kant om til et forslag — veien for å rette kanter
    /// en seed har lagret som validert, men som er maskinelt tolket lovtekst og skal godkjennes av et menneske
    /// (docs/33 §5.3). Brukt én gang av <see cref="OmraderegisterSeed"/> for domstolkantene fra før beslutningen.
    /// Statusendringen logges i Proveniens (<c>handling = 'endret'</c>, gammel og ny status i kildereferansene).
    /// </summary>
    /// <returns>Kanten, eller null hvis id-en ikke finnes.</returns>
    public async Task<StrukturkantEntitet?> GjorTilForslagAsync(Guid id, string oppdagelsesKilde, string endretAv, CancellationToken ct = default)
    {
        var kant = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kant is null) return null;
        if (kant.Status != Strukturkanter.Godkjent)
        {
            throw new ArgumentException($"Kanten har status '{kant.Status}' — bare en godkjent kant kan gjøres om til forslag.");
        }
        if (!OppdagelsesKildeForm().IsMatch(oppdagelsesKilde))
        {
            throw new ArgumentException($"Ugyldig oppdagelseskilde '{oppdagelsesKilde}'.");
        }
        var proveniens = ProveniensHjelper.NyRad(ProveniensType, kant.Id, virksomhetId: null, "endret", endretAv);
        proveniens.KildeReferanserJson = JsonSerializer.Serialize(new
        {
            felt = "status", forStatus = kant.Status, nyStatus = Strukturkanter.MaskineltForeslatt,
            forOppdagelsesKilde = kant.OppdagelsesKilde, nyOppdagelsesKilde = oppdagelsesKilde,
            grunn = "Maskinelt tolket lovtekst skal godkjennes av et menneske (Johann 2026-10-08, #312).",
        });
        kant.Status = Strukturkanter.MaskineltForeslatt;
        kant.OppdagelsesKilde = oppdagelsesKilde;
        kant.SistEndretAv = endretAv;
        kant.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(proveniens);
        await db.SaveChangesAsync(ct);
        return kant;
    }

    /// <summary>
    /// «Avvis» et forslag. [ENDRET, issue #313 / #374, Johanns beslutning 2026-10-15] Setter <c>status = avvist</c> med hvem, når og en
    /// valgfri begrunnelse — raden SLETTES IKKE (før: ekte <c>Remove</c>). Historikken bevares (docs/34 §4, docs/profil/00 §20.2): en ny
    /// kjøring av forslagslasteren ser at utsagnet er avvist og foreslår det ikke på nytt. Bare forslag
    /// (<see cref="Strukturkanter.Ventende"/>) kan avvises; en godkjent kant slettes eller revideres.
    /// </summary>
    /// <returns>Kanten, eller null hvis id-en ikke finnes blant de gjeldende, ikke avviste kantene.</returns>
    public async Task<StrukturkantEntitet?> AvvisAsync(Guid id, string avvistAv, string? begrunnelse = null, CancellationToken ct = default)
    {
        var kant = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kant is null) return null;
        if (!Strukturkanter.Ventende.Contains(kant.Status))
        {
            throw new ArgumentException($"Kanten har status '{kant.Status}' — bare forslag kan avvises. Bruk slett eller revisjon.");
        }
        var grunn = string.IsNullOrWhiteSpace(begrunnelse) ? null : begrunnelse.Trim();
        var proveniens = ProveniensHjelper.NyRad(ProveniensType, kant.Id, virksomhetId: null, "avvist", avvistAv);
        proveniens.KildeReferanserJson = JsonSerializer.Serialize(new { forStatus = kant.Status, begrunnelse = grunn });
        kant.Status = Strukturkanter.Avvist;
        kant.SistEndretAv = avvistAv;
        kant.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        kant.AvvisningBegrunnelse = grunn;
        db.Proveniens.Add(proveniens);
        await db.SaveChangesAsync(ct);
        return kant;
    }

    /// <summary>[Ny, issue #313] Avviser alle ventende MASKINELLE forslag hjemlet i én rettskilde — speilet av
    /// <see cref="GodkjennAlleForHjemmelAsync"/>, med samme valgfrie avgrensning til en oppdagelseskilde-prefiks. Hver kant går gjennom
    /// <see cref="AvvisAsync"/> (egen proveniensrad).</summary>
    /// <returns>Antall kanter som ble avvist.</returns>
    public async Task<int> AvvisAlleForHjemmelAsync(
        Guid hjemmelRettskildeId, string? oppdagelseskildePrefiks, string avvistAv, string? begrunnelse, CancellationToken ct = default)
    {
        var ider = await db.Strukturkanter
            .Where(k => k.Status == Strukturkanter.MaskineltForeslatt && k.HjemmelRettskildeId == hjemmelRettskildeId
                        && (oppdagelseskildePrefiks == null || k.OppdagelsesKilde.StartsWith(oppdagelseskildePrefiks)))
            .Select(k => k.Id).ToListAsync(ct);
        foreach (var id in ider) await AvvisAsync(id, avvistAv, begrunnelse, ct);
        return ider.Count;
    }

    /// <summary>Sletter en kant uansett status — samme presedens som <c>VirksomhetRelasjonregisterTjeneste.SlettAsync</c>
    /// (ekte <c>Remove</c>). Myndighetstildeling og gruppemedlemskap hadde ingen slettevei for validerte rader;
    /// den finnes nå for alle kategorier, fordi en feilregistrert kant ellers bare kan fjernes med SQL.
    /// Slettingen logges i Proveniens (raden selv er borte).</summary>
    public async Task<bool> SlettAsync(Guid id, string slettetAv, CancellationToken ct = default)
    {
        var kant = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kant is null) return false;
        db.Strukturkanter.Remove(kant);
        db.Proveniens.Add(ProveniensHjelper.NyRad(ProveniensType, kant.Id, virksomhetId: null, "slettet", slettetAv));
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------- Lesing ----------------

    /// <summary>Én kant — [ENDRET, issue #313 / #374] også en avvist (svaret på «avvis» viser hvem og hvorfor).</summary>
    public async Task<StrukturkantVisning?> HentAsync(Guid id, CancellationToken ct = default)
    {
        var kant = await GjeldendeInklAvviste.FirstOrDefaultAsync(k => k.Id == id, ct);
        return kant is null ? null : (await ByggVisningerAsync([kant], perspektiv: null, ct)).Single();
    }

    /// <summary>Kanter der noden er fra ELLER til, valgfritt avgrenset til én kategori og/eller til kanter som
    /// er gjeldende i dag (<see cref="ErGjeldende"/>).</summary>
    /// <param name="familie">[Ny, issue #341] Bare kompetansekanter i denne familien (<see cref="Strukturkanter.Familier"/>).
    /// <see cref="Strukturkanter.Beslutning"/> er toppen og står ikke i noen familie.</param>
    public async Task<List<StrukturkantVisning>> HentForNodeAsync(
        Kantnode node, string? kategori = null, bool kunGjeldende = false, CancellationToken ct = default, string? familie = null,
        bool visAvviste = false)
    {
        if (familie is not null && !Strukturkanter.Familier.Contains(familie))
        {
            throw new ArgumentException($"Ukjent familie '{familie}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Familier)}.");
        }
        if (!node.ErGyldig) throw new ArgumentException("Noden må være NØYAKTIG én av virksomhet eller begrep.");
        if (kategori is not null && !Strukturkanter.ErGyldigKategori(kategori))
        {
            throw new ArgumentException($"Ukjent kategori '{kategori}'.");
        }
        // [ENDRET, issue #313 / #374] visAvviste = «vis avviste»-filteret; ellers skjuler global spørrefilter dem.
        var kilde = visAvviste ? GjeldendeInklAvviste : db.Strukturkanter;
        var q = node.VirksomhetId is { } v
            ? kilde.Where(k => k.FraVirksomhetId == v || k.TilVirksomhetId == v)
            : kilde.Where(k => k.FraBegrepId == node.BegrepId || k.TilBegrepId == node.BegrepId);
        if (kategori is not null) q = q.Where(k => k.Kategori == kategori);
        if (familie is not null)
        {
            var koder = db.RelasjonsTypeKonfigurasjoner
                .Where(t => t.Kategori == Strukturkanter.Kompetanse && t.Familie == familie).Select(t => t.Kode);
            q = q.Where(k => k.Kategori == Strukturkanter.Kompetanse && koder.Contains(k.Typekode));
        }
        var kanter = await q.ToListAsync(ct);
        if (kunGjeldende) kanter = await FiltrerGjeldendeAsync(kanter, ct);
        return await ByggVisningerAsync(kanter, node, ct);
    }

    /// <summary>Kantene HJEMLET i én rettskilde — docs/32 §3 S1/S2 fra lovens side («hvilke strukturutsagn
    /// gir denne loven?»). Kanter med bare kilde utenfor korpus hører per definisjon ikke hit.</summary>
    public async Task<List<StrukturkantVisning>> HentForHjemmelRettskildeAsync(Guid rettskildeId, CancellationToken ct = default, bool visAvviste = false)
    {
        var kanter = await (visAvviste ? GjeldendeInklAvviste : db.Strukturkanter).Where(k => k.HjemmelRettskildeId == rettskildeId).ToListAsync(ct);
        return (await ByggVisningerAsync(kanter, perspektiv: null, ct))
            .OrderBy(v => v.Kategori, StringComparer.Ordinal).ThenBy(v => v.Visningstekst, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// [Ny, Johanns beslutning 2026-10-07] Kantene som BARE er dokumentert i en kilde av typen
    /// <paramref name="kildetype"/> utenfor korpus — med <see cref="Strukturkanter.NettsideAnnet"/> er det
    /// arbeidslista «forvaltningsstruktur som mangler forankring i en rettskilde». Null = alle kanter uten
    /// hjemmel i korpus, uansett type.
    /// </summary>
    public async Task<List<StrukturkantVisning>> HentUtenKorpusforankringAsync(string? kildetype, CancellationToken ct = default)
    {
        if (kildetype is not null && !Strukturkanter.KildeUtenforKorpusTyper.Contains(kildetype))
        {
            throw new ArgumentException(
                $"Ukjent kildetype '{kildetype}'. Gyldige verdier: {string.Join(", ", Strukturkanter.KildeUtenforKorpusTyper)}.");
        }
        var kanter = await db.Strukturkanter
            .Where(k => k.HjemmelRettskildeId == null && (kildetype == null || k.KildeUtenforKorpusType == kildetype))
            .ToListAsync(ct);
        return (await ByggVisningerAsync(kanter, perspektiv: null, ct))
            .OrderBy(v => v.Kategori, StringComparer.Ordinal).ThenBy(v => v.Visningstekst, StringComparer.Ordinal).ToList();
    }

    /// <summary>Alle ventende forslag (<see cref="Strukturkanter.Ventende"/>) — forslagskøen (issue #285 AC6, [ENDRET #367]).</summary>
    public async Task<List<StrukturkantVisning>> HentForslagAsync(CancellationToken ct = default)
    {
        var kanter = await db.Strukturkanter.Where(k => Strukturkanter.Ventende.Contains(k.Status)).ToListAsync(ct);
        return (await ByggVisningerAsync(kanter, perspektiv: null, ct)).OrderBy(v => v.OpprettetTidspunkt).ToList();
    }

    /// <summary>[Ny, issue #313 / #374] De avviste kantene («vis avviste» i forslagskøen), valgfritt for én hjemmel. Nyeste avvisning først.</summary>
    public async Task<List<StrukturkantVisning>> HentAvvisteAsync(Guid? hjemmelRettskildeId = null, CancellationToken ct = default)
    {
        var kanter = await GjeldendeInklAvviste
            .Where(k => k.Status == Strukturkanter.Avvist && (hjemmelRettskildeId == null || k.HjemmelRettskildeId == hjemmelRettskildeId))
            .ToListAsync(ct);
        return (await ByggVisningerAsync(kanter, perspektiv: null, ct)).OrderByDescending(v => v.SistEndretTidspunkt).ToList();
    }

    /// <summary>[Ny, issue #313 AC4] Forhåndsvisningen av én kant (også et forslag eller en avvist): visningsteksten fra fra-nodens side,
    /// fra til-nodens side og uten perspektiv (rettskildesiden). Samme <see cref="ByggVisningerAsync"/> som sidene bruker — ingen egen
    /// tekstlogikk.</summary>
    public async Task<StrukturkantForhandsvisning?> ForhandsvisAsync(Guid id, CancellationToken ct = default)
    {
        var kant = await GjeldendeInklAvviste.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kant is null) return null;
        var fra = new Kantnode(kant.FraVirksomhetId, kant.FraBegrepId);
        Kantnode? til = kant.TilVirksomhetId is null && kant.TilBegrepId is null ? null : new Kantnode(kant.TilVirksomhetId, kant.TilBegrepId);
        var fraSide = (await ByggVisningerAsync([kant], fra, ct)).Single();
        var tilSide = til is null || til == fra ? null : (await ByggVisningerAsync([kant], til, ct)).Single();
        var rettskildeside = (await ByggVisningerAsync([kant], perspektiv: null, ct)).Single();
        return new StrukturkantForhandsvisning(fraSide, tilSide, rettskildeside);
    }

    /// <summary>Deserialiserer <see cref="StrukturkantEntitet.AvgrensningParagrafspennJson"/> (docs/20 §7.1).</summary>
    public static IReadOnlyList<ParagrafspennPar> LesParagrafspenn(StrukturkantEntitet kant) =>
        JsonSerializer.Deserialize<List<ParagrafspennPar>>(kant.AvgrensningParagrafspennJson, JsonSerialiseringHjelper.Innstillinger) ?? [];

    /// <summary>
    /// Gyldighet er en KOMBINASJON (docs/29 §Del B, uendret fra myndighetstildeling): kantens egne
    /// <see cref="StrukturkantEntitet.GyldigFra"/>/<see cref="StrukturkantEntitet.GyldigTil"/> OG hjemmelens
    /// <c>Status</c>/<c>GyldigTil</c>. En kant uten korpus-hjemmel (kilde utenfor korpus) avgjøres bare av
    /// sine egne datoer — det finnes ingen hjemmel å arve fra. <paramref name="hjemmel"/> null med
    /// hjemmel-id satt = hjemmelen finnes ikke lenger → ikke gjeldende.
    /// </summary>
    public static bool ErGjeldende(StrukturkantEntitet kant, RettskildeEntitet? hjemmel, DateOnly dato)
    {
        if (kant.GyldigFra is not null && kant.GyldigFra.Value > dato) return false;
        if (kant.GyldigTil is not null && kant.GyldigTil.Value < dato) return false;
        if (kant.HjemmelRettskildeId is null) return true;
        if (hjemmel is null) return false;
        if (hjemmel.Status == "Opphevet") return false;
        return hjemmel.GyldigTil is null || hjemmel.GyldigTil.Value >= dato;
    }

    public async Task<List<StrukturkantEntitet>> FiltrerGjeldendeAsync(List<StrukturkantEntitet> kanter, CancellationToken ct = default)
    {
        var dato = DateOnly.FromDateTime(DateTime.UtcNow);
        var hjemmelIder = kanter.Where(k => k.HjemmelRettskildeId is not null).Select(k => k.HjemmelRettskildeId!.Value).Distinct().ToList();
        var hjemler = await db.Rettskilder.Where(r => hjemmelIder.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        return kanter.Where(k => ErGjeldende(
            k, k.HjemmelRettskildeId is { } h ? hjemler.GetValueOrDefault(h) : null, dato)).ToList();
    }

    /// <summary>
    /// Navn, visningsmaler og hjemmeltitler i tre samlede oppslag (ikke ett per kant). Virksomhetsnavnet er
    /// den lesbare navneformen (<see cref="VirksomhetVisningsnavnTjeneste.VisningsGrunn"/>) der den finnes,
    /// ellers registernavnet — samme regel som relasjonene hadde før #311 (registernavn-runden 2026-09-08).
    /// </summary>
    public async Task<List<StrukturkantVisning>> ByggVisningerAsync(
        IReadOnlyList<StrukturkantEntitet> kanter, Kantnode? perspektiv, CancellationToken ct = default)
    {
        if (kanter.Count == 0) return [];

        var virksomhetIder = kanter.SelectMany(k => new[] { k.FraVirksomhetId, k.TilVirksomhetId })
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        var virksomheter = await db.Virksomheter.Where(v => virksomhetIder.Contains(v.Id))
            .Select(v => new { v.Id, v.Navn, v.Aktortype }).ToDictionaryAsync(v => v.Id, ct);
        var visningsnavn = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet"
                        && b.Navneformgrunn == VirksomhetVisningsnavnTjeneste.VisningsGrunn
                        && b.Entitetsstatus == "gjeldende"
                        // [ENDRET, issue #385] Et forslag til navneform er ikke aktørens navn før det er godkjent.
                        && (b.Godkjenning == null || b.Godkjenning == Strukturkanter.Godkjent)
                        && b.VirksomhetReferanseId != null
                        && virksomhetIder.Contains(b.VirksomhetReferanseId.Value))
            .Select(b => new { VirksomhetId = b.VirksomhetReferanseId!.Value, b.Term })
            .ToListAsync(ct);
        var lesbartNavn = visningsnavn.GroupBy(x => x.VirksomhetId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Term).OrderBy(t => t, StringComparer.Ordinal).First());

        var begrepIder = kanter.SelectMany(k => new[] { k.FraBegrepId, k.TilBegrepId })
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        var begreper = await db.Begreper.Where(b => begrepIder.Contains(b.Id))
            .Select(b => new { b.Id, b.Term, b.Begrepskategori }).ToDictionaryAsync(b => b.Id, ct);

        var typer = (await db.RelasjonsTypeKonfigurasjoner.ToListAsync(ct))
            .ToDictionary(k => (k.Kategori, k.Kode));

        var hjemmelIder = kanter.Where(k => k.HjemmelRettskildeId is not null).Select(k => k.HjemmelRettskildeId!.Value).Distinct().ToList();
        var hjemmeltitler = await db.Rettskilder.Where(r => hjemmelIder.Contains(r.Id))
            .Select(r => new { r.Id, Tittel = r.Kortnavn ?? r.Tittel }).ToDictionaryAsync(r => r.Id, r => r.Tittel, ct);

        // [Ny, issue #367] Premissene for avledede kanter, i ett oppslag.
        var avledetIder = kanter.Where(k => k.Kunnskapsstatus == Strukturkanter.Avledet).Select(k => k.Id).ToList();
        var premisser = avledetIder.Count == 0 ? [] : (await db.StrukturkantPremisser.Where(p => avledetIder.Contains(p.KantId)).ToListAsync(ct))
            .GroupBy(p => p.KantId).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(p => p.PremissKantId).OrderBy(x => x).ToList());

        // [Ny, issue #397] Omfanget av avledede etterfølgere: premissene som er F forvaltning fra etterfølgeren.
        var etterfolgerPremisser = kanter.Where(k => k.Typekode == "etterfolger" && premisser.ContainsKey(k.Id))
            .SelectMany(k => premisser[k.Id]).Distinct().ToList();
        var forvaltningsPremisser = etterfolgerPremisser.Count == 0 ? [] : await db.Strukturkanter.IgnoreQueryFilters()
            .Where(p => etterfolgerPremisser.Contains(p.Id) && p.Kategori == Strukturkanter.Oppgave && p.Typekode == "forvaltning")
            .Select(p => new { p.Id, p.FraVirksomhetId }).ToDictionaryAsync(p => p.Id, p => p.FraVirksomhetId, ct);
        int? Omfang(StrukturkantEntitet k) =>
            k.Typekode == "etterfolger" && premisser.TryGetValue(k.Id, out var pr)
                ? pr.Count(id => forvaltningsPremisser.TryGetValue(id, out var fra) && fra == k.FraVirksomhetId)
                : null;

        KantnodeVisning Node(Guid? virksomhetId, Guid? begrepId)
        {
            if (virksomhetId is { } v)
            {
                var finnes = virksomheter.TryGetValue(v, out var rad);
                return new KantnodeVisning("virksomhet", v,
                    lesbartNavn.GetValueOrDefault(v) ?? (finnes ? rad!.Navn : "(ukjent virksomhet)"),
                    finnes ? rad!.Aktortype : null);
            }
            var b = begrepId!.Value;
            return begreper.TryGetValue(b, out var brad)
                ? new KantnodeVisning("begrep", b, brad.Term, brad.Begrepskategori)
                : new KantnodeVisning("begrep", b, "(ukjent begrep)", null);
        }

        return kanter.Select(k =>
        {
            var fra = Node(k.FraVirksomhetId, k.FraBegrepId);
            var til = k.TilVirksomhetId is null && k.TilBegrepId is null ? null : Node(k.TilVirksomhetId, k.TilBegrepId);
            var type = typer.GetValueOrDefault((k.Kategori, k.Typekode));
            string? retning = null;
            if (perspektiv is not null)
            {
                var erFra = perspektiv.VirksomhetId is { } pv ? k.FraVirksomhetId == pv : k.FraBegrepId == perspektiv.BegrepId;
                retning = erFra ? "fra" : "til";
            }
            var objektTekst = k.Objekt ?? (k.HjemmelEid is not null || k.AvgrensningParagrafspennJson != "[]" ? "etter hjemmelen" : "(ikke angitt)");
            var selvregulering = Strukturkanter.ErSelvregulering(k);
            string tekst;
            if (retning == "til" && k.Kategori == Strukturkanter.Oppgave)
            {
                // [Ny, issue #368] Fra siden oppgaven gjelder beholder den modaliteten og objektet, som plikten (#353-rettingen).
                tekst = $"{fra.Navn} {string.Format(type?.FraVisningsmal ?? "(ukjent type) {0}", Oppgavetekst(Modalitetsord(k.Modalitet), "denne", k.Objekt, objektTekst))}";
            }
            else if (retning == "til" && k.Kategori == Strukturkanter.Plikt)
            {
                // [Ny, issue #353, koordinatorens kaldtest 2026-10-09] Fra motpartens side beholder en plikt modaliteten og
                // objektet: «HELSE SØR-ØST RHF har avtaleplikt (skal) overfor denne — samarbeidsavtale». Til-malen («{0} har
                // avtaleplikt overfor denne») mistet begge, så Oslo kommunes side sa mindre enn pliktsubjektets.
                tekst = $"{fra.Navn} {string.Format(type?.FraVisningsmal ?? "(ukjent type) {0}", Kompetansetekst(Modalitetsord(k.Modalitet), "denne", false, k.Objekt, objektTekst))}";
            }
            else if (retning == "til" && k.Kategori == Strukturkanter.Kompetanse && k.Undertype is not null)
            {
                // [Ny, issue #355, kaldtesten 2026-10-09] Fra motpartens side beholder en kompetanse med undertype undertypen, som
                // plikten beholder modaliteten (#353-rettingen): «Hvem kan sette inn en fast dommer?» spørres fra dommerrollen, og
                // «Kongen i statsråd har oppnevningskompetanse overfor denne» skilte ikke utnevning fra konstitusjon.
                tekst = $"{fra.Navn} {string.Format(type?.FraVisningsmal ?? "(ukjent type) {0}", Kompetansetekst(k.Undertype, "denne", false, k.Objekt, objektTekst))}";
            }
            else if (retning == "til" && !selvregulering)
            {
                tekst = string.Format(type?.TilVisningsmal ?? "(ukjent type) {0}", fra.Navn);
            }
            else
            {
                // [ENDRET, issue #341] K: motparten med «overfor» (+ normform og objekt) — se Kompetansetekst.
                // [Ny, issue #353] P: samme form, med modaliteten i parentes («har samarbeidsplikt (skal) overfor B»).
                var motpart = k.Kategori == Strukturkanter.Kompetanse
                    ? Kompetansetekst(k.Normform ?? k.Undertype, til?.Navn, selvregulering, k.Objekt, objektTekst)
                    : k.Kategori == Strukturkanter.Plikt
                        ? Kompetansetekst(Modalitetsord(k.Modalitet), til?.Navn, false, k.Objekt, objektTekst)
                        : k.Kategori == Strukturkanter.Oppgave // [Ny, issue #368] «har samordningsoppgave (skal) beredskapsarbeid»
                            ? Oppgavetekst(Modalitetsord(k.Modalitet), til?.Navn, k.Objekt, objektTekst)
                            : til?.Navn ?? objektTekst;
                var fraTekst = string.Format(type?.FraVisningsmal ?? "(ukjent type) {0}", motpart);
                tekst = retning == "fra" ? fraTekst : $"{fra.Navn} {fraTekst}";
            }
            // [Ny, issue #352-tillegg] En saksavhengig type (G settes_med) har antallet i objektet — det er selve
            // opplysningen («settes i den enkelte sak med dommere (fem dommere)»), så det står i teksten fra begge sider.
            if (type?.Saksavhengig == true && k.Objekt is not null) tekst += $" ({k.Objekt})";
            return new StrukturkantVisning(
                k.Id, k.Kategori, k.Typekode, retning, tekst, fra, til, k.Objekt, LesParagrafspenn(k), k.AvgrensningTekst,
                k.Polaritet, k.HjemmelRettskildeId,
                k.HjemmelRettskildeId is { } h ? hjemmeltitler.GetValueOrDefault(h) : null, k.HjemmelEid,
                k.KildeUtenforKorpusTekst, k.KildeUtenforKorpusLenke, k.KildeUtenforKorpusType, k.KildeUtenforKorpusDokumentasjon, k.GyldigFra, k.GyldigTil, k.Status,
                k.OppdagelsesKilde, k.Kommentar, k.OpprettetAv, k.OpprettetTidspunkt,
                k.Normform, k.Grunnlag, k.Delegeringsadgang, k.DelegeringSlutning, selvregulering,
                k.Kategori == Strukturkanter.Kompetanse ? type?.Familie : null,
                k.Kategori == Strukturkanter.Kompetanse ? Strukturkanter.FvlKategoriFor(k.Typekode, k.Normform, k.Undertype, type?.FvlKategori) : null,
                k.Undertype, k.Modalitet,
                k.Kunnskapsstatus, k.Begrunnelse, k.Tolkningsargumenter, premisser.GetValueOrDefault(k.Id), k.Entitetsstatus, k.ErstatterKantId,
                k.Profilversjon, k.HjemmelNodeId,
                k.HjemmelEid is not null && k.HjemmelNodeId is null
                    ? "Hjemmelversjon ukjent: kanten ble registrert før hjemmelen fikk versjon (#367), og noden kunne ikke slås entydig opp."
                    : null,
                k.HjemmelEndretTidspunkt)
            {
                SistEndretAv = k.SistEndretAv, SistEndretTidspunkt = k.SistEndretTidspunkt, AvvisningBegrunnelse = k.AvvisningBegrunnelse,
                Omfang = Omfang(k),
            };
        }).ToList();
    }

    /// <summary>
    /// [Ny, issue #341, Johanns beslutning P1 2026-10-08] Motpartsteksten i en kompetansekant — det {0} står for i
    /// K-malene («har klagekompetanse {0}»): «A har kompetanse av typen X, eventuelt OVERFOR B, når det gjelder Y».
    /// Normformen står først i parentes («har normgivningskompetanse (forskrift) …»). Uten motpart og objekt:
    /// <paramref name="reserve"/> («etter hjemmelen» / «(ikke angitt)», som før).
    /// <para>
    /// [ENDRET, issue #352] Første parameter er presiseringen i parentes: normformen ELLER undertypen («har
    /// oppnevningskompetanse (valg) overfor forliksrådet»). De kan ikke stå på samme kant — normform bare på normgivning,
    /// undertype bare på oppnevning/overprøving (CHECK-ene) — så kalleren sender den som finnes.
    /// </para>
    /// </summary>
    public static string Kompetansetekst(string? presisering, string? motpart, bool selvregulering, string? objekt, string reserve)
    {
        var deler = new List<string>();
        if (presisering is not null) deler.Add($"({presisering})");
        if (selvregulering) deler.Add("overfor seg selv (selvregulering)");
        else if (motpart is not null) deler.Add($"overfor {motpart}");
        if (objekt is not null) deler.Add(motpart is not null || selvregulering ? $"— {objekt}" : objekt);
        if (motpart is null && !selvregulering && objekt is null) deler.Add(reserve);
        return string.Join(' ', deler);
    }

    /// <summary>
    /// [Ny, issue #368] Teksten i en oppgavekant — det {0} står for i F-malene («har samordningsoppgave {0}»): modaliteten i parentes,
    /// oppgaven (objektet), og hva den gjelder etter tankestrek: «(skal) sekretariat for forliksrådene — gjelder Forliksrådene». Ingen
    /// «overfor» som i K og P: en oppgave har ingen motpart (begrepsmanualen §F).
    /// </summary>
    public static string Oppgavetekst(string? modalitet, string? gjelder, string? objekt, string reserve)
    {
        var deler = new List<string>();
        if (modalitet is not null) deler.Add($"({modalitet})");
        deler.Add(objekt ?? reserve);
        if (gjelder is not null) deler.Add($"— gjelder {gjelder}");
        return string.Join(' ', deler);
    }

    /// <summary>[Ny, issue #353] Modaliteten slik den leses («bor» lagres uten ø, som de andre lukkede vokabularene).</summary>
    public static string? Modalitetsord(string? modalitet) => modalitet switch
    {
        null => null,
        "bor" => "bør",
        _ => modalitet,
    };

    // ---------------- Validering ----------------

    /// <summary>[Ny, issue #353] Modalitet finnes bare på P (CHECK ck_strukturkanter_modalitet), lukket liste, ingen standardverdi.
    /// [ENDRET, issue #368] og på F (oppgave/funksjon): «skal» er plikten til å utføre oppgaven.</summary>
    private static string? ValiderModalitet(string kategori, string? modalitet)
    {
        var m = string.IsNullOrWhiteSpace(modalitet) ? null : modalitet.Trim();
        if (m is null) return null;
        if (!Strukturkanter.KategorierMedModalitet.Contains(kategori))
        {
            throw new ArgumentException(
                $"Modalitet er en egenskap ved en PLIKT (P) eller en OPPGAVE (F) — ikke ved {Strukturkanter.Visningsnavn(kategori)} (issue #353, #368).");
        }
        if (!Strukturkanter.Modaliteter.Contains(m))
        {
            throw new ArgumentException(
                $"Ukjent modalitet '{m}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Modaliteter)}. Ingen gjettet fallback (issue #353).");
        }
        return m;
    }

    /// <summary>
    /// [Ny, issue #353] En ordning (<see cref="Nodetyper.Ordning"/>) er ikke en aktør: som fra-node bare i P, R forvaltes_av og
    /// G tilhorer; som til-node bare i P. R forvaltes_av og G tilhorer krever omvendt at fra ER en ordning, og at til ikke er en
    /// ordning. Se <see cref="Strukturkanter.OrdningLovSomFra"/>.
    /// </summary>
    private async Task ValiderOrdningsreglerAsync(NyStrukturkant ny, CancellationToken ct)
    {
        var fraType = await AktortypeAsync(ny.Fra, ct);
        var tilType = await AktortypeAsync(ny.Til, ct);
        var navn = $"{ny.Kategori} {ny.Typekode}";
        if (fraType == Nodetyper.Ordning && !Strukturkanter.OrdningLovSomFra(ny.Kategori, ny.Typekode))
        {
            throw new ArgumentException(
                $"En ordning kan ikke være fra-node i {navn}: den er ikke en aktør. Lov er P (plikt), R {Strukturkanter.ForvaltesAv} "
                + $"og G {Strukturkanter.Tilhorer} (issue #353).");
        }
        if (tilType == Nodetyper.Ordning && !Strukturkanter.OrdningLovSomTil(ny.Kategori))
        {
            throw new ArgumentException($"En ordning kan bare være til-node i P (plikt overfor motpart), ikke i {navn} (issue #353).");
        }
        if (!Strukturkanter.KreverOrdningSomFra(ny.Kategori, ny.Typekode)) return;
        if (fraType != Nodetyper.Ordning)
        {
            var hva = fraType is null ? "ikke en ordning (aktørtypen er uavklart, eller noden er et begrep)" : $"'{fraType}'";
            throw new ArgumentException(
                $"{navn} går fra en ORDNING (aktørtype 'ordning', f.eks. folketrygden) — fra-noden er {hva}. "
                + "Sett aktørtypen først; den gjettes ikke (issue #353).");
        }
        if (tilType == Nodetyper.Ordning)
        {
            throw new ArgumentException($"{navn} går til en aktør, ikke til en annen ordning (issue #353).");
        }
    }

    private async Task<string?> AktortypeAsync(Kantnode? node, CancellationToken ct) => node?.VirksomhetId is { } v
        ? await db.Virksomheter.Where(x => x.Id == v).Select(x => x.Aktortype).FirstOrDefaultAsync(ct)
        : null;

    /// <summary>
    /// [Ny, issue #341] Normform, grunnlag og delegerbar finnes bare på K (CHECK-ene i RegelIdeDbContext); normformen bare
    /// på normgivning. Lukkede lister, ingen standardverdi. Returnerer de trimmede verdiene.
    /// </summary>
    /// <summary>[Ny, issue #367 AC5 / #335] Delegeringsadgang og slutning: bare på K, lukkede lister, slutning bare når teksten ikke
    /// regulerer delegeringen (<c>ikke_regulert</c>) — en slutning skal aldri stå ved siden av et tekstfunn.</summary>
    private static (string? Adgang, string? Slutning) ValiderDelegering(string kategori, string? adgang, string? slutning)
    {
        var a = string.IsNullOrWhiteSpace(adgang) ? null : adgang.Trim();
        var s = string.IsNullOrWhiteSpace(slutning) ? null : slutning.Trim();
        if (kategori != Strukturkanter.Kompetanse && (a is not null || s is not null))
        {
            throw new ArgumentException(
                $"Delegeringsadgang er en egenskap ved en KOMPETANSE (kategori K) — ikke ved {Strukturkanter.Visningsnavn(kategori)} (#335).");
        }
        if (a is not null && !Strukturkanter.Delegeringsadganger.Contains(a))
        {
            throw new ArgumentException(
                $"Ukjent delegeringsadgang '{a}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Delegeringsadganger)}. Ingen gjettet fallback.");
        }
        if (s is not null && !Strukturkanter.Delegeringsslutninger.Contains(s))
        {
            throw new ArgumentException(
                $"Ukjent delegeringsslutning '{s}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Delegeringsslutninger)}.");
        }
        if (s is not null && a != Strukturkanter.IkkeRegulert)
        {
            throw new ArgumentException(
                "En slutning om delegering etter alminnelige regler gjelder bare når teksten ikke regulerer det (delegeringsadgang "
                + "'ikke_regulert'). Står delegeringen i teksten, er det et tekstfunn, ikke en slutning (#335).");
        }
        return (a, s);
    }

    /// <summary>
    /// [Ny, issue #367 AC1/AC3, docs/profil/03 §5] Kunnskapsstatusen og grunnlaget den krever — samme regler som CHECK-ene
    /// (<c>ck_strukturkanter_kunnskapsstatus_grunnlag</c>, <c>ck_strukturkanter_fortolkning</c>) og constraint-triggeren for premisser,
    /// her med en forklaring. <c>foreldet</c> settes ikke ved opprettelse: det er en revidert rad (<see cref="ReviderAsync"/>).
    /// </summary>
    private async Task<(string? Status, string? Begrunnelse, List<string>? Argumenter, List<Guid> Premisser)> ValiderKunnskapsstatusAsync(
        NyStrukturkant ny, string? kildeTekst, CancellationToken ct)
    {
        var status = string.IsNullOrWhiteSpace(ny.Kunnskapsstatus) ? null : ny.Kunnskapsstatus.Trim();
        var begrunnelse = string.IsNullOrWhiteSpace(ny.Begrunnelse) ? null : ny.Begrunnelse.Trim();
        var argumenter = ny.Tolkningsargumenter is { Count: > 0 } liste ? liste.Select(a => a.Trim()).Distinct().ToList() : null;
        var premisser = (ny.Premisser ?? []).Distinct().ToList();
        if (status is not null && !Strukturkanter.Kunnskapsstatuser.Contains(status))
        {
            throw new ArgumentException(
                $"Ukjent kunnskapsstatus '{status}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Kunnskapsstatuser)} (eller utelat: ikke vurdert).");
        }
        if (argumenter?.FirstOrDefault(a => !Strukturkanter.Tolkningsargumenter.Contains(a)) is { } ukjent)
        {
            throw new ArgumentException(
                $"Ukjent tolkningsargument '{ukjent}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Tolkningsargumenter)}.");
        }
        switch (status)
        {
            case Strukturkanter.Foreldet:
                throw new ArgumentException("Kunnskapsstatus 'foreldet' settes ikke ved opprettelse — en revisjon gjør den gamle raden foreldet.");
            case Strukturkanter.EksterntDokumentert when kildeTekst is null:
                throw new ArgumentException("Kunnskapsstatus 'eksternt_dokumentert' krever en kilde utenfor korpus (docs/profil/03 §5).");
            case Strukturkanter.Tekstfunn or Strukturkanter.Normalisering when ny.HjemmelRettskildeId is null || ny.HjemmelEid is null:
                throw new ArgumentException(
                    $"Kunnskapsstatus '{status}' krever hjemmel i korpus med eId — tekstfragmentet utsagnet står i (docs/profil/03 §5).");
            case Strukturkanter.Fortolkning when begrunnelse is null || argumenter is null:
                throw new ArgumentException(
                    "Kunnskapsstatus 'fortolkning' krever en begrunnelse og minst ett tolkningsargument "
                    + $"({string.Join(", ", Strukturkanter.Tolkningsargumenter)}) (docs/profil/03 §5–6).");
            case Strukturkanter.Avledet when premisser.Count == 0:
                throw new ArgumentException("Kunnskapsstatus 'avledet' krever minst én premiss (id-en til kantene slutningen bygger på).");
        }
        if (premisser.Count > 0 && status != Strukturkanter.Avledet)
        {
            throw new ArgumentException("Premisser gjelder bare et avledet utsagn (kunnskapsstatus 'avledet').");
        }
        if (premisser.Count > 0)
        {
            var finnes = await db.Strukturkanter.CountAsync(k => premisser.Contains(k.Id), ct);
            if (finnes != premisser.Count)
            {
                throw new ArgumentException("En eller flere premisser finnes ikke blant de gjeldende kantene. Ingen gjettet fallback.");
            }
        }
        if (status is null && (begrunnelse is not null || argumenter is not null))
        {
            throw new ArgumentException("Begrunnelse og tolkningsargumenter hører til en vurdering — oppgi kunnskapsstatus.");
        }
        return (status, begrunnelse, argumenter, premisser);
    }

    private static (string? Normform, string? Grunnlag, string? Undertype) ValiderKompetansefelt(
        string kategori, string typekode, string? normform, string? grunnlag, string? undertype)
    {
        var nf = string.IsNullOrWhiteSpace(normform) ? null : normform.Trim();
        var gr = string.IsNullOrWhiteSpace(grunnlag) ? null : grunnlag.Trim();
        if (kategori != Strukturkanter.Kompetanse && (nf is not null || gr is not null))
        {
            throw new ArgumentException(
                "Normform og grunnlag er egenskaper ved en KOMPETANSE (kategori K) — ikke ved "
                + $"{Strukturkanter.Visningsnavn(kategori)} (issue #341).");
        }
        if (nf is not null && typekode != Strukturkanter.Normgivning)
        {
            throw new ArgumentException($"Normform gjelder bare normgivningskompetanse, ikke «{typekode}» (issue #341).");
        }
        if (nf is not null && !Strukturkanter.Normformer.Contains(nf))
        {
            throw new ArgumentException(
                $"Ukjent normform '{nf}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Normformer)}. Ingen gjettet fallback.");
        }
        if (gr is not null && !Strukturkanter.Grunnlag.Contains(gr))
        {
            throw new ArgumentException(
                $"Ukjent grunnlag '{gr}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Grunnlag)}. Ingen gjettet fallback.");
        }
        // [Ny, issue #352] Undertype: bare på K, og bare en undertype typen har (Strukturkanter.Undertyper).
        var ut = string.IsNullOrWhiteSpace(undertype) ? null : undertype.Trim();
        if (ut is not null && kategori != Strukturkanter.Kompetanse)
        {
            throw new ArgumentException(
                $"Undertype er en egenskap ved en KOMPETANSE (kategori K) — ikke ved {Strukturkanter.Visningsnavn(kategori)} (issue #352).");
        }
        if (ut is not null && !Strukturkanter.ErGyldigUndertype(typekode, ut))
        {
            var lov = Strukturkanter.Undertyper.TryGetValue(typekode, out var u) ? string.Join(", ", u) : "ingen";
            throw new ArgumentException(
                $"Ukjent undertype '{ut}' for «{typekode}». Gyldige verdier: {lov}. Ingen gjettet fallback (issue #352).");
        }
        return (nf, gr, ut);
    }

    /// <summary>Hver eId i spennet må finnes som rettskilde-node — ingen gjettet fallback. [Skilt ut fra
    /// <see cref="OpprettAsync"/> i #330, slik at <see cref="OppdaterAvgrensningAsync"/> validerer likt.]</summary>
    private async Task ValiderParagrafspennAsync(IReadOnlyList<ParagrafspennPar> paragrafspenn, CancellationToken ct)
    {
        foreach (var par in paragrafspenn)
        {
            if (string.IsNullOrWhiteSpace(par.FraEid) || !await db.RettskildeNoder.AnyAsync(n => n.Eid == par.FraEid, ct))
            {
                throw new ArgumentException($"Fant ingen rettskilde-node med eId '{par.FraEid}'. Ingen gjettet fallback.");
            }
            if (par.TilEid is not null && !await db.RettskildeNoder.AnyAsync(n => n.Eid == par.TilEid, ct))
            {
                throw new ArgumentException($"Fant ingen rettskilde-node med eId '{par.TilEid}'. Ingen gjettet fallback.");
            }
        }
    }

    /// <summary>Sjekker at noden finnes og har lov til å stå i denne enden for denne kategorien. Returnerer
    /// navnet (for feilmeldinger).</summary>
    private async Task<string> ValiderNodeAsync(
        Kantnode node, bool virksomhetLov, string[] begrepstyper, string ende, string beskrivelse, string kategori,
        CancellationToken ct)
    {
        if (node.VirksomhetId is { } v)
        {
            if (!virksomhetLov)
            {
                throw new ArgumentException(
                    $"Kategori {kategori} ({Strukturkanter.Visningsnavn(kategori)}) kan ikke ha en virksomhet som {ende}-node — "
                    + $"{ende} skal være {beskrivelse}.");
            }
            var navn = await db.Virksomheter.Where(x => x.Id == v).Select(x => x.Navn).FirstOrDefaultAsync(ct);
            return navn ?? throw new ArgumentException($"Fant ingen virksomhet med id '{v}'. Ingen gjettet fallback.");
        }

        var b = node.BegrepId!.Value;
        var begrep = await db.Begreper.Where(x => x.Id == b && x.Entitetsstatus == "gjeldende")
            .Select(x => new { x.Term, x.Begrepskategori }).FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException($"Fant ingen gjeldende begrep med id '{b}'. Ingen gjettet fallback.");
        // 'gruppe' (uavklart type, #310) godtas der et begrep med gruppefunksjon godtas — se Noderegler.
        var lov = begrepstyper.Length > 0
                  && (begrepstyper.Contains(begrep.Begrepskategori) || begrep.Begrepskategori == Nodetyper.Gruppe);
        if (!lov)
        {
            throw new ArgumentException(
                $"«{begrep.Term}» er {Nodetyper.Visningsnavn(begrep.Begrepskategori)} og kan ikke være {ende}-node i kategori "
                + $"{kategori} ({Strukturkanter.Visningsnavn(kategori)}) — {ende} skal være {beskrivelse}.");
        }
        return begrep.Term;
    }

    /// <summary>
    /// Bevart fra <c>GruppeMedlemskapTjeneste.KastHvisSykelAsync</c> (issue #164, Johanns eksplisitte valg):
    /// traverserer de EKSISTERENDE kantene i samme kategori baklengs fra den nye kantens fra-node. Når den nye
    /// kanten er «fra → til», finnes en sykel hvis <paramref name="tilId"/> alt (transitivt) peker på
    /// <paramref name="fraId"/>. Feilmeldingen navngir hele kjeden, slik at saksbehandleren ser HVILKEN
    /// registrering som må rettes. Bredde-først med <c>besokt</c>: grafen er en DAG, ikke et tre.
    /// </summary>
    private async Task KastHvisSykelAsync(string kategori, Guid fraId, Guid tilId, string fraNavn, string tilNavn, CancellationToken ct)
    {
        var kanter = await db.Strukturkanter
            .Where(k => k.Kategori == kategori && k.FraBegrepId != null && k.TilBegrepId != null)
            .Select(k => new { Fra = k.FraBegrepId!.Value, Til = k.TilBegrepId!.Value })
            .ToListAsync(ct);
        // «hvem peker på X» — baklengs nabo-liste.
        var innkommende = kanter.GroupBy(k => k.Til).ToDictionary(g => g.Key, g => g.Select(k => k.Fra).ToList());

        var forgjenger = new Dictionary<Guid, Guid>();
        var besokt = new HashSet<Guid> { fraId };
        var ko = new Queue<Guid>();
        ko.Enqueue(fraId);
        while (ko.Count > 0)
        {
            var gjeldende = ko.Dequeue();
            if (!innkommende.TryGetValue(gjeldende, out var forrige)) continue;
            foreach (var n in forrige)
            {
                if (n == tilId)
                {
                    forgjenger[n] = gjeldende;
                    var kjede = await BeskrivKjedeAsync(fraId, n, forgjenger, ct);
                    throw new ArgumentException(
                        $"«{tilNavn}» peker allerede (via {kjede}) på «{fraNavn}» i kategori {kategori} — å registrere "
                        + $"«{fraNavn}» → «{tilNavn}» ville laget en sirkulær kjede. Ingen gjettet fallback.");
                }
                if (!besokt.Add(n)) continue;
                forgjenger[n] = gjeldende;
                ko.Enqueue(n);
            }
        }
    }

    private async Task<string> BeskrivKjedeAsync(Guid fraId, Guid tilId, Dictionary<Guid, Guid> forgjenger, CancellationToken ct)
    {
        var sti = new List<Guid> { tilId };
        var gjeldende = tilId;
        while (gjeldende != fraId && forgjenger.TryGetValue(gjeldende, out var forrige))
        {
            sti.Add(forrige);
            gjeldende = forrige;
        }
        var termer = await db.Begreper.Where(b => sti.Contains(b.Id)).Select(b => new { b.Id, b.Term }).ToListAsync(ct);
        var termPerId = termer.ToDictionary(t => t.Id, t => t.Term);
        return string.Join(" → ", sti.Select(id => termPerId.TryGetValue(id, out var term) ? $"«{term}»" : id.ToString()));
    }
}
