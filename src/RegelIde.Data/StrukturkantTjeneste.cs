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
/// <param name="AiForslagVersjon">Påkrevd sammen med <c>Status = "foreslatt_av_ai"</c> når
/// <paramref name="OppdagelsesKilde"/> ikke er oppgitt (samme regel som de gamle tjenestene hadde) — havner i
/// Proveniens og avleder <c>OppdagelsesKilde = "ki:&lt;versjon&gt;"</c>.</param>
public sealed record NyStrukturkant(
    string Kategori, string Typekode, Kantnode Fra, Kantnode? Til,
    Guid? HjemmelRettskildeId = null, string? HjemmelEid = null,
    string? KildeUtenforKorpusTekst = null, string? KildeUtenforKorpusLenke = null,
    IReadOnlyList<ParagrafspennPar>? Paragrafspenn = null, string? AvgrensningTekst = null,
    string? Objekt = null, string Polaritet = "positiv",
    DateOnly? GyldigFra = null, DateOnly? GyldigTil = null, string? Kommentar = null,
    string Status = "validert", string? AiForslagVersjon = null, string? OppdagelsesKilde = null,
    string? KildeUtenforKorpusType = null, string? KildeUtenforKorpusDokumentasjon = null);

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
    string OpprettetAv, DateTimeOffset OpprettetTidspunkt);

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

    [GeneratedRegex(@"^(manuell|monster:\S+|ki:\S+)$", RegexOptions.CultureInvariant)]
    private static partial Regex OppdagelsesKildeForm();

    /// <summary>
    /// Oppretter en kant — eller returnerer en EKSISTERENDE identisk kant (samme kategori, typekode, fra,
    /// til, objekt, polaritet, hjemmel og avgrensning) uendret. Idempotensen er det
    /// <see cref="SamiskSprakforvaltningSeed"/>, veiviseren og KI-oppdagelsen hviler på for å kunne kjøres
    /// om igjen. (Før #311 hadde hver gamle tabell sin egen dublettregel; gruppemedlemskap var unikt på
    /// PARET uansett hjemmel. Med avgrensning og polaritet på kanten er to kanter med ulik hjemmel/
    /// avgrensning to ulike utsagn — «klageinstans etter § 3» og «etter § 5» — og lagres hver for seg.)
    /// </summary>
    public async Task<StrukturkantOpprettet> OpprettAsync(NyStrukturkant ny, string opprettetAv, CancellationToken ct = default)
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
            ?? (ny.Status == "foreslatt_av_ai" && ny.AiForslagVersjon is not null ? $"ki:{ny.AiForslagVersjon}" : null)
            ?? (ny.Status == "validert" ? "manuell" : null);
        if (oppdagelsesKilde is null)
        {
            throw new ArgumentException(
                "Et forslag (status 'foreslatt_av_ai') må ha aiForslagVersjon eller oppdagelsesKilde. Ingen gjettet fallback.");
        }
        if (!OppdagelsesKildeForm().IsMatch(oppdagelsesKilde))
        {
            throw new ArgumentException(
                $"Ugyldig oppdagelseskilde '{oppdagelsesKilde}' — må være 'manuell', 'monster:<id>' eller 'ki:<modell>' (docs/33 §4.3).");
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
        if (ny.Til is not null && ny.Til == ny.Fra)
        {
            throw new ArgumentException("En kant kan ikke gå fra en node til seg selv. Ingen gjettet fallback.");
        }
        var fraNavn = await ValiderNodeAsync(ny.Fra, regel.FraVirksomhet, regel.FraBegrep, "fra", regel.FraBeskrivelse, ny.Kategori, ct);
        var tilNavn = ny.Til is null
            ? null
            : await ValiderNodeAsync(ny.Til, regel.TilVirksomhet, regel.TilBegrep, "til", regel.TilBeskrivelse, ny.Kategori, ct);

        var objekt = string.IsNullOrWhiteSpace(ny.Objekt) ? null : ny.Objekt.Trim();
        var paragrafspenn = ny.Paragrafspenn ?? [];
        if (ny.Kategori == Strukturkanter.Kompetanse && ny.Til is null && objekt is null
            && paragrafspenn.Count == 0 && ny.HjemmelEid is null)
        {
            throw new ArgumentException(
                "En kompetansekant uten til-node må si HVA kompetansen gjelder: en sakstype (objekt), et paragrafspenn "
                + "eller en hjemmel-eId (docs/33 §4.3: «aktør/rolle → bestemmelse eller sakstype»).");
        }
        if (ny.Kategori == Strukturkanter.Klasseniva && ny.Til is null && objekt is null)
        {
            throw new ArgumentException("En klassenivå-kant uten til-node må ha et objekt (f.eks. «kommunestyre»).");
        }

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

        // ---- Avgrensning ----
        if (Strukturkanter.KreverParagrafspenn.Contains(ny.Kategori) && ny.HjemmelRettskildeId is not null && paragrafspenn.Count == 0)
        {
            throw new ArgumentException(
                $"Kategori {ny.Kategori} ({Strukturkanter.Visningsnavn(ny.Kategori)}) med hjemmel i korpus krever "
                + "paragrafspenn — ingen gjettet fallback (docs/20 §7.1, docs/33 §3 funn 7).");
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
        var eksisterende = await db.Strukturkanter.FirstOrDefaultAsync(k =>
            k.Kategori == ny.Kategori && k.Typekode == ny.Typekode
            && k.FraVirksomhetId == ny.Fra.VirksomhetId && k.FraBegrepId == ny.Fra.BegrepId
            && k.TilVirksomhetId == tilV && k.TilBegrepId == tilB
            && k.Objekt == objekt && k.Polaritet == ny.Polaritet
            && k.HjemmelRettskildeId == ny.HjemmelRettskildeId
            && k.AvgrensningParagrafspennJson == spennJson, ct);
        if (eksisterende is not null) return new StrukturkantOpprettet(eksisterende, false);

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
            AvgrensningParagrafspennJson = spennJson,
            AvgrensningTekst = avgrensningTekst,
            Polaritet = ny.Polaritet,
            HjemmelRettskildeId = ny.HjemmelRettskildeId,
            HjemmelEid = ny.HjemmelEid,
            KildeUtenforKorpusTekst = kildeTekst,
            KildeUtenforKorpusLenke = kildeLenke,
            KildeUtenforKorpusType = kildeType,
            KildeUtenforKorpusDokumentasjon = kildeDok,
            GyldigFra = ny.GyldigFra,
            GyldigTil = ny.GyldigTil,
            Status = ny.Status,
            OppdagelsesKilde = oppdagelsesKilde,
            Kommentar = string.IsNullOrWhiteSpace(ny.Kommentar) ? null : ny.Kommentar.Trim(),
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Strukturkanter.Add(kant);
        // Delt, nasjonal referansedata — proveniensen er ikke attribuert til en virksomhet (samme som
        // myndighetstildeling før #311).
        db.Proveniens.Add(ny.Status == "foreslatt_av_ai"
            ? ProveniensHjelper.NyForslagRad(ProveniensType, kant.Id, virksomhetId: null, opprettetAv, ny.AiForslagVersjon ?? oppdagelsesKilde)
            : ProveniensHjelper.NyRad(ProveniensType, kant.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return new StrukturkantOpprettet(kant, true);
    }

    /// <summary>
    /// «Tildeling»: en virksomhet knyttes til et begrep med gruppefunksjon. Kategorien UTLEDES av begrepets
    /// nodetype — samme regel som datamigreringen i #311: mål = rolle → <c>I innehar</c> (rolleinnehav arves
    /// ikke, docs/33 §4.2), ellers → <c>M medlem_av</c>. Brukt av veiviseren, KI-oppdagelsen, «Legg til
    /// tilhørighet»-skjemaet og samisk-seeden, som alle før #311 skrev en myndighetstildeling.
    /// </summary>
    public async Task<StrukturkantOpprettet> OpprettTildelingAsync(
        Guid virksomhetId, Guid begrepId, Guid hjemmelRettskildeId, IReadOnlyList<ParagrafspennPar> paragrafspenn,
        string? avgrensningTekst, string opprettetAv, DateOnly? gyldigFra = null, DateOnly? gyldigTil = null,
        CancellationToken ct = default, string status = "validert", string? aiForslagVersjon = null)
    {
        var (kategori, typekode) = await TildelingskategoriAsync(begrepId, ct);
        return await OpprettAsync(new NyStrukturkant(
            kategori, typekode, Kantnode.Virksomhet(virksomhetId), Kantnode.Begrep(begrepId),
            HjemmelRettskildeId: hjemmelRettskildeId, Paragrafspenn: paragrafspenn, AvgrensningTekst: avgrensningTekst,
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
    /// Samme validering som <see cref="OpprettAsync"/>: hver eId i spennet må finnes i korpus, og M/I med hjemmel
    /// krever et spenn (<see cref="Strukturkanter.KreverParagrafspenn"/>). Begge feltene ERSTATTES — tomt spenn og
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
        if (Strukturkanter.KreverParagrafspenn.Contains(kant.Kategori) && kant.HjemmelRettskildeId is not null && spenn.Count == 0)
        {
            throw new ArgumentException(
                $"Kategori {kant.Kategori} ({Strukturkanter.Visningsnavn(kant.Kategori)}) med hjemmel i korpus krever "
                + "paragrafspenn — ingen gjettet fallback (docs/20 §7.1, docs/33 §3 funn 7).");
        }
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
            && k.HjemmelRettskildeId == kant.HjemmelRettskildeId
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

    /// <summary>Et menneske bekrefter et forslag (<c>foreslatt_av_ai</c> → <c>validert</c>) — samme mønster
    /// som de gamle tjenestene (issue #285 AC6). En allerede validert kant har ingenting å godkjenne.</summary>
    public async Task<StrukturkantEntitet?> GodkjennAsync(Guid id, string godkjentAv, CancellationToken ct = default)
    {
        var kant = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kant is null) return null;
        if (kant.Status != "foreslatt_av_ai")
        {
            throw new ArgumentException($"Kanten har status '{kant.Status}' — kun 'foreslatt_av_ai'-rader kan godkjennes.");
        }
        kant.Status = "validert";
        kant.SistEndretAv = godkjentAv;
        kant.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var proveniens = ProveniensHjelper.NyRad(ProveniensType, kant.Id, virksomhetId: null, "validert", godkjentAv);
        proveniens.GodkjentAv = godkjentAv;
        db.Proveniens.Add(proveniens);
        await db.SaveChangesAsync(ct);
        return kant;
    }

    /// <summary>«Avvis» et forslag — ekte <c>Remove</c>, og BARE for <c>foreslatt_av_ai</c> (samme
    /// avgrensning som før #311: avvisning er for egne, ubekreftede forslag; sletting av en validert kant er
    /// <see cref="SlettAsync"/>).</summary>
    public async Task<bool> AvvisAsync(Guid id, CancellationToken ct = default)
    {
        var kant = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kant is null) return false;
        if (kant.Status != "foreslatt_av_ai")
        {
            throw new ArgumentException($"Kanten har status '{kant.Status}' — kun 'foreslatt_av_ai'-rader kan avvises. Bruk slett.");
        }
        db.Strukturkanter.Remove(kant);
        await db.SaveChangesAsync(ct);
        return true;
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

    public async Task<StrukturkantVisning?> HentAsync(Guid id, CancellationToken ct = default)
    {
        var kant = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == id, ct);
        return kant is null ? null : (await ByggVisningerAsync([kant], perspektiv: null, ct)).Single();
    }

    /// <summary>Kanter der noden er fra ELLER til, valgfritt avgrenset til én kategori og/eller til kanter som
    /// er gjeldende i dag (<see cref="ErGjeldende"/>).</summary>
    public async Task<List<StrukturkantVisning>> HentForNodeAsync(
        Kantnode node, string? kategori = null, bool kunGjeldende = false, CancellationToken ct = default)
    {
        if (!node.ErGyldig) throw new ArgumentException("Noden må være NØYAKTIG én av virksomhet eller begrep.");
        if (kategori is not null && !Strukturkanter.ErGyldigKategori(kategori))
        {
            throw new ArgumentException($"Ukjent kategori '{kategori}'.");
        }
        var q = node.VirksomhetId is { } v
            ? db.Strukturkanter.Where(k => k.FraVirksomhetId == v || k.TilVirksomhetId == v)
            : db.Strukturkanter.Where(k => k.FraBegrepId == node.BegrepId || k.TilBegrepId == node.BegrepId);
        if (kategori is not null) q = q.Where(k => k.Kategori == kategori);
        var kanter = await q.ToListAsync(ct);
        if (kunGjeldende) kanter = await FiltrerGjeldendeAsync(kanter, ct);
        return await ByggVisningerAsync(kanter, node, ct);
    }

    /// <summary>Kantene HJEMLET i én rettskilde — docs/32 §3 S1/S2 fra lovens side («hvilke strukturutsagn
    /// gir denne loven?»). Kanter med bare kilde utenfor korpus hører per definisjon ikke hit.</summary>
    public async Task<List<StrukturkantVisning>> HentForHjemmelRettskildeAsync(Guid rettskildeId, CancellationToken ct = default)
    {
        var kanter = await db.Strukturkanter.Where(k => k.HjemmelRettskildeId == rettskildeId).ToListAsync(ct);
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

    /// <summary>Alle ventende forslag (<c>foreslatt_av_ai</c>) — KI-forslag-køen (issue #285 AC6).</summary>
    public async Task<List<StrukturkantVisning>> HentForslagAsync(CancellationToken ct = default)
    {
        var kanter = await db.Strukturkanter.Where(k => k.Status == "foreslatt_av_ai").ToListAsync(ct);
        return (await ByggVisningerAsync(kanter, perspektiv: null, ct)).OrderBy(v => v.OpprettetTidspunkt).ToList();
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
            string tekst;
            if (retning == "til")
            {
                tekst = string.Format(type?.TilVisningsmal ?? "(ukjent type) {0}", fra.Navn);
            }
            else
            {
                var motpart = til?.Navn ?? objektTekst;
                var fraTekst = string.Format(type?.FraVisningsmal ?? "(ukjent type) {0}", motpart);
                tekst = retning == "fra" ? fraTekst : $"{fra.Navn} {fraTekst}";
            }
            return new StrukturkantVisning(
                k.Id, k.Kategori, k.Typekode, retning, tekst, fra, til, k.Objekt, LesParagrafspenn(k), k.AvgrensningTekst,
                k.Polaritet, k.HjemmelRettskildeId,
                k.HjemmelRettskildeId is { } h ? hjemmeltitler.GetValueOrDefault(h) : null, k.HjemmelEid,
                k.KildeUtenforKorpusTekst, k.KildeUtenforKorpusLenke, k.KildeUtenforKorpusType, k.KildeUtenforKorpusDokumentasjon, k.GyldigFra, k.GyldigTil, k.Status,
                k.OppdagelsesKilde, k.Kommentar, k.OpprettetAv, k.OpprettetTidspunkt);
        }).ToList();
    }

    // ---------------- Validering ----------------

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
