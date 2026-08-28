using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

// ---------- Verdiobjekter for Handlingens jsonb-felt (2026-08-20) ----------
// Samme mønster som JuridiskGrunnlagInput/SkjonnsmomentInput i VilkarregisterTjeneste.cs — definert
// her i RegelIde.Data, gjenbrukt (ikke duplisert) av RegelIde.Api sine DTO-er.

/// <summary>Et lovsitat i kortform — <c>Lov</c> er et kortnavn (f.eks. "serveringsloven"), ikke en full
/// tittel eller Eli. Full tittel/Eli slås opp mot regelverksreferanser/rettskilder ved behov.</summary>
public sealed record HandlingHjemmelInput(string Lov, string? Henvisning);

public sealed record HandlingKanalInput(string Kanal, string? Adresse);
public sealed record HandlingBehandlingstidInput(string? Frist, HandlingHjemmelInput? Hjemmel);
public sealed record HandlingKostnadInput(string? Belop, IReadOnlyList<HandlingHjemmelInput> Hjemmel);
public sealed record HandlingVedleggInput(string Navn, string? Kategori, HandlingHjemmelInput? Hjemmel);
public sealed record HandlingVeiledningstekstInput(string Overskrift, string? Innhold, HandlingHjemmelInput? Hjemmel);
public sealed record HandlingArsakInput(string Arsak, HandlingHjemmelInput Hjemmel);
public sealed record HandlingBevisKanalInput(string Kanal);
public sealed record HandlingResultatInput(string? Hva, IReadOnlyList<HandlingBevisKanalInput> BevisKanaler);

/// <summary>Én rad fra <see cref="HandlingregisterTjeneste.ListerAlleAsync"/> — Handlingen selv pluss
/// nettopp den delen av eiende Tjeneste toppnivå-lista <c>/handlinger</c> trenger (tittel for lenke,
/// virksomhetId for <c>useVirksomheter().visEier</c>), uten å hente hele TjenesteEntitet-raden.</summary>
public sealed record HandlingMedTjeneste(HandlingEntitet Handling, string TjenesteTittel, Guid VirksomhetId);

/// <summary>
/// Handlingsregister (2026-08-20) — sideordnet <see cref="TjenesteregisterTjeneste"/>. Samme stil:
/// primary-constructor DI, hardkodede, UTVIDBARE verdilister (ikke DB-CHECK, ikke KodelisteEntitet i
/// v1 — se docs/13-backlog.md §7/Johanns "åpne, ikke fasit"-instruks), <see cref="ArgumentException"/>
/// for domenevalidering, dual-write av domenerad + proveniensrad.
///
/// ALLE metoder som leser/skriver én rad krever <c>virksomhetId</c> og verifiserer at handlingens
/// EIENDE Tjeneste faktisk tilhører det virksomhetId'et — bygget inn fra dag én, ikke lagt til
/// etterpå (se sikkerhetsfiksen på <see cref="TjenesteregisterTjeneste"/> samme runde).
/// </summary>
public sealed class HandlingregisterTjeneste(RegelIdeDbContext db)
{
    internal static readonly string[] GyldigeHandlingstyper =
        ["soke", "endre", "si_opp", "melde", "registrere", "rapportere", "ettersende_dokumentasjon",
         "klage", "gi_samtykke", "trekke_samtykke", "be_om_innsyn", "bestille", "kontrolleres", "avslutte", "annet"];

    internal static readonly string[] GyldigeUtfortAv = ["soker", "forvaltning", "tredjepart"];

    // "foreslatt_av_ai" lagt til (handlingsforslag-ki-omfang-runden) — samme mønster som
    // TjenesteregisterTjeneste/BegrepsregisterTjeneste sine egne GyldigeStatuser-lister: statusen ER
    // en gyldig verdi å SETTE (ikke bare noe proveniensraden alene forteller), slik at f.eks. en
    // fremtidig "avvis forslag" (tilbake til "utkast") kan skje via samme SettStatusAsync som alt annet.
    private static readonly string[] GyldigeStatuser =
        ["utkast", "foreslatt_av_ai", "foreslatt_av_annen_virksomhet", "under_revisjon", "validert", "publisert", "tilbaketrukket", "arkivert"];

    public Task<List<HandlingEntitet>> ListerForTjenesteAsync(Guid tjenesteId, CancellationToken ct = default) =>
        db.Handlinger
            .Where(h => h.TjenesteId == tjenesteId && h.Entitetsstatus == "gjeldende")
            .OrderBy(h => h.Navn)
            .ToListAsync(ct);

    /// <summary>
    /// Lister ALLE handlinger TVERS AV ALLE tjenester (2026-08-22, toppnivå-siden <c>/handlinger</c>) —
    /// samme "åpne data for lesing"-holdning som <see cref="FinnAsync"/>/<see cref="ListerForTjenesteAsync"/>,
    /// ikke virksomhet-scopet. Joiner inn eiende tjenestes <see cref="TjenesteEntitet.Tittel"/> og
    /// <see cref="TjenesteEntitet.VirksomhetId"/> i SAMME spørring — klienten skal IKKE måtte gjøre N
    /// kall (ett per tjeneste) for å bygge denne listen.
    /// </summary>
    public Task<List<HandlingMedTjeneste>> ListerAlleAsync(CancellationToken ct = default) =>
        db.Handlinger
            .Where(h => h.Entitetsstatus == "gjeldende")
            // Sorterer FØR Join+projeksjon til HandlingMedTjeneste — EF Core kan ikke oversette en
            // OrderBy som leser en egenskap PÅ et allerede klient-konstruert record-objekt (§3.3-aktig
            // lærdom, samme "kaster tydelig i stedet for å gjette" fant vi ved live-verifisering:
            // System.InvalidOperationException "could not be translated"). Rekkefølgen fra denne
            // OrderBy-en bevares gjennom Join-en av EF/Postgres uten videre reordering-operasjoner.
            .OrderBy(h => h.Navn)
            .Join(
                db.Tjenester.Where(t => t.Entitetsstatus == "gjeldende"),
                h => h.TjenesteId, t => t.Id,
                (h, t) => new HandlingMedTjeneste(h, t.Tittel, t.VirksomhetId))
            .ToListAsync(ct);

    /// <summary>Lesing er bevisst IKKE virksomhet-scopet her — samme "åpne data for lesing"-holdning
    /// som resten av modellen (kodelister, GET /api/tjenester/{id}). Skriving er det, se under.</summary>
    public Task<HandlingEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Handlinger.FirstOrDefaultAsync(h => h.Id == id && h.Entitetsstatus == "gjeldende", ct);

    public async Task<HandlingEntitet> OpprettAsync(
        Guid virksomhetId, Guid tjenesteId, string navn, string handlingstype, string? bruksomraade, string? utfortAv,
        IReadOnlyList<HandlingKanalInput>? kanaler, HandlingBehandlingstidInput? behandlingstid,
        HandlingKostnadInput? kostnad, IReadOnlyList<HandlingVedleggInput>? vedlegg,
        IReadOnlyList<HandlingVeiledningstekstInput>? veiledningstekst, IReadOnlyList<HandlingArsakInput>? arsaker,
        HandlingResultatInput? resultat, string? merknad, string opprettetAv, CancellationToken ct = default)
    {
        Valider(navn, handlingstype, utfortAv);

        if (!await db.Tjenester.AnyAsync(t => t.Id == tjenesteId && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{tjenesteId}' for denne virksomheten.");
        }

        var handling = new HandlingEntitet
        {
            Id = Guid.NewGuid(),
            TjenesteId = tjenesteId,
            Navn = navn,
            Handlingstype = handlingstype,
            Bruksomraade = bruksomraade,
            UtfortAv = utfortAv,
            KanalerJson = Serialiser(kanaler ?? []),
            BehandlingstidJson = Serialiser(behandlingstid ?? new HandlingBehandlingstidInput(null, null)),
            KostnadJson = Serialiser(kostnad ?? new HandlingKostnadInput(null, [])),
            VedleggJson = Serialiser(vedlegg ?? []),
            VeiledningstekstJson = Serialiser(veiledningstekst ?? []),
            ArsakerJson = Serialiser(arsaker ?? []),
            ResultatJson = Serialiser(resultat ?? new HandlingResultatInput(null, [])),
            Merknad = merknad,
            Status = "utkast",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Handlinger.Add(handling);
        db.Proveniens.Add(ProveniensHjelper.NyRad("handling", handling.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return handling;
    }

    /// <summary>
    /// «Foreslå handlinger»/«full»-omfang (handlingsforslag-ki-omfang-runden, se
    /// <see cref="HandlingsforslagTjeneste"/> og <see cref="TjenesteforslagTjeneste.KjorFullForslagAsync"/>)
    /// — kopi av <see cref="OpprettAsync"/>, men <c>Status = "foreslatt_av_ai"</c> og en
    /// <see cref="ProveniensHjelper.NyForslagRad"/>-rad i stedet for <c>NyRad(..., "opprettet", ...)</c>,
    /// EKSAKT samme mønster som <see cref="TjenesteregisterTjeneste.OpprettForslagFraKiAsync"/> og
    /// <see cref="BegrepsregisterTjeneste.OpprettForslagFraKiAsync"/> (docs/14-byggesteg5-teknisk-design.md
    /// §4). Validerer FORTSATT at <paramref name="tjenesteId"/> hører til <paramref name="virksomhetId"/> —
    /// samme sjekk som <see cref="OpprettAsync"/> — en KI-agent er ikke unntatt sikkerhetsscopingen
    /// klassekommentaren beskriver, selv om den (i motsetning til Tjeneste-forslaget) skriver UNDER en
    /// allerede eksisterende rad i stedet for å opprette en helt ny toppnivå-entitet.
    /// </summary>
    public async Task<HandlingEntitet> OpprettForslagFraKiAsync(
        Guid virksomhetId, Guid tjenesteId, string navn, string handlingstype, string? bruksomraade, string? utfortAv,
        IReadOnlyList<HandlingKanalInput>? kanaler, HandlingBehandlingstidInput? behandlingstid,
        HandlingKostnadInput? kostnad, IReadOnlyList<HandlingVedleggInput>? vedlegg,
        IReadOnlyList<HandlingVeiledningstekstInput>? veiledningstekst, IReadOnlyList<HandlingArsakInput>? arsaker,
        HandlingResultatInput? resultat, string? merknad, string opprettetAv, string aiForslagVersjon,
        string? kildeReferanserJson, CancellationToken ct = default)
    {
        Valider(navn, handlingstype, utfortAv);

        if (!await db.Tjenester.AnyAsync(t => t.Id == tjenesteId && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{tjenesteId}' for denne virksomheten. Ingen gjettet fallback.");
        }

        var handling = new HandlingEntitet
        {
            Id = Guid.NewGuid(),
            TjenesteId = tjenesteId,
            Navn = navn,
            Handlingstype = handlingstype,
            Bruksomraade = bruksomraade,
            UtfortAv = utfortAv,
            KanalerJson = Serialiser(kanaler ?? []),
            BehandlingstidJson = Serialiser(behandlingstid ?? new HandlingBehandlingstidInput(null, null)),
            KostnadJson = Serialiser(kostnad ?? new HandlingKostnadInput(null, [])),
            VedleggJson = Serialiser(vedlegg ?? []),
            VeiledningstekstJson = Serialiser(veiledningstekst ?? []),
            ArsakerJson = Serialiser(arsaker ?? []),
            ResultatJson = Serialiser(resultat ?? new HandlingResultatInput(null, [])),
            Merknad = merknad,
            Status = "foreslatt_av_ai",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Handlinger.Add(handling);
        db.Proveniens.Add(ProveniensHjelper.NyForslagRad("handling", handling.Id, virksomhetId, opprettetAv, aiForslagVersjon, kildeReferanserJson));
        await db.SaveChangesAsync(ct);
        return handling;
    }

    /// <summary>
    /// [Ny, 2026-08-28, import-wizard-runden] Samme mønster som <see cref="OpprettForslagFraKiAsync"/>,
    /// men kilden er en ANNEN virksomhets import (<see cref="TjenesteregisterTjeneste
    /// .OpprettForslagFraAnnenVirksomhetAsync"/>), ikke KI. Kalles KUN rett etter at den eiende
    /// tjenesten selv nettopp ble opprettet under <paramref name="virksomhetId"/> i samme import-kall —
    /// ownership-sjekken under holder derfor uansett hvilken virksomhet som faktisk KJØRTE importen.
    /// </summary>
    public async Task<HandlingEntitet> OpprettForslagFraAnnenVirksomhetAsync(
        Guid virksomhetId, Guid tjenesteId, string navn, string handlingstype, string? bruksomraade, string? utfortAv,
        IReadOnlyList<HandlingKanalInput>? kanaler, HandlingBehandlingstidInput? behandlingstid,
        HandlingKostnadInput? kostnad, IReadOnlyList<HandlingVedleggInput>? vedlegg,
        IReadOnlyList<HandlingVeiledningstekstInput>? veiledningstekst, IReadOnlyList<HandlingArsakInput>? arsaker,
        HandlingResultatInput? resultat, string? merknad, string opprettetAv, Guid forslagFraVirksomhetId,
        CancellationToken ct = default)
    {
        Valider(navn, handlingstype, utfortAv);

        if (!await db.Tjenester.AnyAsync(t => t.Id == tjenesteId && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{tjenesteId}' for denne virksomheten. Ingen gjettet fallback.");
        }

        var handling = new HandlingEntitet
        {
            Id = Guid.NewGuid(),
            TjenesteId = tjenesteId,
            Navn = navn,
            Handlingstype = handlingstype,
            Bruksomraade = bruksomraade,
            UtfortAv = utfortAv,
            KanalerJson = Serialiser(kanaler ?? []),
            BehandlingstidJson = Serialiser(behandlingstid ?? new HandlingBehandlingstidInput(null, null)),
            KostnadJson = Serialiser(kostnad ?? new HandlingKostnadInput(null, [])),
            VedleggJson = Serialiser(vedlegg ?? []),
            VeiledningstekstJson = Serialiser(veiledningstekst ?? []),
            ArsakerJson = Serialiser(arsaker ?? []),
            ResultatJson = Serialiser(resultat ?? new HandlingResultatInput(null, [])),
            Merknad = merknad,
            Status = "foreslatt_av_annen_virksomhet",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Handlinger.Add(handling);
        db.Proveniens.Add(ProveniensHjelper.NyTverrVirksomhetForslagRad("handling", handling.Id, virksomhetId, opprettetAv, forslagFraVirksomhetId));
        await db.SaveChangesAsync(ct);
        return handling;
    }

    /// <summary>Sikkerhetsscopet fra dag én — se klassekommentaren.</summary>
    public async Task<HandlingEntitet?> OppdaterAsync(
        Guid id, Guid virksomhetId, string navn, string handlingstype, string? bruksomraade, string? utfortAv,
        IReadOnlyList<HandlingKanalInput>? kanaler, HandlingBehandlingstidInput? behandlingstid,
        HandlingKostnadInput? kostnad, IReadOnlyList<HandlingVedleggInput>? vedlegg,
        IReadOnlyList<HandlingVeiledningstekstInput>? veiledningstekst, IReadOnlyList<HandlingArsakInput>? arsaker,
        HandlingResultatInput? resultat, string? merknad, string endretAv, CancellationToken ct = default)
    {
        Valider(navn, handlingstype, utfortAv);

        var handling = await db.Handlinger
            .Join(db.Tjenester, h => h.TjenesteId, t => t.Id, (h, t) => new { Handling = h, t.VirksomhetId })
            .Where(x => x.Handling.Id == id && x.VirksomhetId == virksomhetId && x.Handling.Entitetsstatus == "gjeldende")
            .Select(x => x.Handling)
            .FirstOrDefaultAsync(ct);
        if (handling is null) return null;

        handling.Navn = navn;
        handling.Handlingstype = handlingstype;
        handling.Bruksomraade = bruksomraade;
        handling.UtfortAv = utfortAv;
        handling.KanalerJson = Serialiser(kanaler ?? []);
        handling.BehandlingstidJson = Serialiser(behandlingstid ?? new HandlingBehandlingstidInput(null, null));
        handling.KostnadJson = Serialiser(kostnad ?? new HandlingKostnadInput(null, []));
        handling.VedleggJson = Serialiser(vedlegg ?? []);
        handling.VeiledningstekstJson = Serialiser(veiledningstekst ?? []);
        handling.ArsakerJson = Serialiser(arsaker ?? []);
        handling.ResultatJson = Serialiser(resultat ?? new HandlingResultatInput(null, []));
        handling.Merknad = merknad;
        handling.SistEndretAv = endretAv;
        handling.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        handling.Versjon++;
        db.Proveniens.Add(ProveniensHjelper.NyRad("handling", handling.Id, virksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return handling;
    }

    public async Task<bool> SlettAsync(Guid id, Guid virksomhetId, CancellationToken ct = default)
    {
        var handling = await db.Handlinger
            .Join(db.Tjenester, h => h.TjenesteId, t => t.Id, (h, t) => new { Handling = h, t.VirksomhetId })
            .Where(x => x.Handling.Id == id && x.VirksomhetId == virksomhetId && x.Handling.Entitetsstatus == "gjeldende")
            .Select(x => x.Handling)
            .FirstOrDefaultAsync(ct);
        if (handling is null) return false;

        db.Handlinger.Remove(handling);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Byggesteg 4-mønsteret gjenbrukt per handling, se HandlingEntitet.RotnodeId sin
    /// klassekommentar for tolkningen (override av Tjenestens eget vilkårstre).</summary>
    public async Task<HandlingEntitet?> SettRotnodeAsync(Guid handlingId, Guid virksomhetId, Guid regelnodeId, CancellationToken ct = default)
    {
        if (!await db.Regelnoder.AnyAsync(r => r.Id == regelnodeId && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen regelnode med id '{regelnodeId}'.");
        }

        var handling = await db.Handlinger
            .Join(db.Tjenester, h => h.TjenesteId, t => t.Id, (h, t) => new { Handling = h, t.VirksomhetId })
            .Where(x => x.Handling.Id == handlingId && x.VirksomhetId == virksomhetId && x.Handling.Entitetsstatus == "gjeldende")
            .Select(x => x.Handling)
            .FirstOrDefaultAsync(ct);
        if (handling is null) return null;

        handling.RotnodeId = regelnodeId;
        await db.SaveChangesAsync(ct);
        return handling;
    }

    /// <summary>
    /// Flytter en handling til en ANNEN tjeneste hos SAMME virksomhet (2026-08-22, Johanns
    /// tilbakemelding: samletjenestene Oppgaveregister-seeden lager ("Oppgaveregisteret — X") er
    /// bevisst grove plassholdere — man må lett kunne flytte hver handling til sin egentlige
    /// tjeneste når en fagperson har vurdert den, se OppgaveregisterHandlingSeed punkt (b)). Sikkerhets-
    /// scopet på SAMME måte som OppdaterAsync/SettStatusAsync: <paramref name="virksomhetId"/> må eie
    /// BÅDE handlingens nåværende tjeneste OG måltjenesten — flytting TVERS AV virksomheter er ikke en
    /// "flytt"-operasjon (det ville endre hvem som EIER handlingen), og støttes derfor ikke her.
    /// </summary>
    public async Task<HandlingEntitet?> FlyttTilTjenesteAsync(
        Guid handlingId, Guid virksomhetId, Guid nyTjenesteId, string endretAv, CancellationToken ct = default)
    {
        var handling = await db.Handlinger
            .Join(db.Tjenester, h => h.TjenesteId, t => t.Id, (h, t) => new { Handling = h, t.VirksomhetId })
            .Where(x => x.Handling.Id == handlingId && x.VirksomhetId == virksomhetId && x.Handling.Entitetsstatus == "gjeldende")
            .Select(x => x.Handling)
            .FirstOrDefaultAsync(ct);
        if (handling is null) return null;

        if (!await db.Tjenester.AnyAsync(t => t.Id == nyTjenesteId && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen tjeneste med id '{nyTjenesteId}' for denne virksomheten. Ingen gjettet fallback.");
        }

        handling.TjenesteId = nyTjenesteId;
        handling.SistEndretAv = endretAv;
        handling.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        handling.Versjon++;
        db.Proveniens.Add(ProveniensHjelper.NyRad("handling", handling.Id, virksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return handling;
    }

    public async Task<HandlingEntitet?> SettStatusAsync(Guid id, Guid virksomhetId, string nyStatus, string endretAv, CancellationToken ct = default)
    {
        if (!GyldigeStatuser.Contains(nyStatus))
        {
            throw new ArgumentException($"Ukjent status '{nyStatus}'. Gyldige verdier: {string.Join(", ", GyldigeStatuser)}.");
        }

        var handling = await db.Handlinger
            .Join(db.Tjenester, h => h.TjenesteId, t => t.Id, (h, t) => new { Handling = h, t.VirksomhetId })
            .Where(x => x.Handling.Id == id && x.VirksomhetId == virksomhetId && x.Handling.Entitetsstatus == "gjeldende")
            .Select(x => x.Handling)
            .FirstOrDefaultAsync(ct);
        if (handling is null) return null;

        handling.Status = nyStatus;
        handling.SistEndretAv = endretAv;
        handling.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("handling", handling.Id, virksomhetId, nyStatus is "publisert" or "validert" ? nyStatus : "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return handling;
    }

    private static void Valider(string navn, string handlingstype, string? utfortAv)
    {
        if (string.IsNullOrWhiteSpace(navn))
        {
            throw new ArgumentException("Navn kan ikke være tomt. Ingen gjettet fallback.");
        }
        if (!GyldigeHandlingstyper.Contains(handlingstype))
        {
            throw new ArgumentException($"Ukjent handlingstype '{handlingstype}'. Gyldige verdier: {string.Join(", ", GyldigeHandlingstyper)}.");
        }
        if (utfortAv is not null && !GyldigeUtfortAv.Contains(utfortAv))
        {
            throw new ArgumentException($"Ukjent utfort_av '{utfortAv}'. Gyldige verdier: {string.Join(", ", GyldigeUtfortAv)}.");
        }
    }

    private static string Serialiser<T>(T verdi) => JsonSerializer.Serialize(verdi, JsonSerialiseringHjelper.Innstillinger);
}
