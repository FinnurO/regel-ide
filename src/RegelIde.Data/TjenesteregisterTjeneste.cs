using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

// ---------- Verdiobjekter for Rettighetens "innhold"-felt (2026-08-20, Tjenestedetalj-runden) ----------
// Overfører serveringsbevilling-modell-forslag.json sin rettigheter[].innhold-seksjon til ekte felt.
// Samme JSONB-mønster som HandlingregisterTjeneste.cs sine Handling*Input-records.

public sealed record TjenesteInnsenderInput(IReadOnlyList<string> HvemKanSende, string? Innlogging);
public sealed record TjenesteInnsendingInput(string? Kanal, IReadOnlyList<string> EtterMottak, string? Merknad);
public sealed record TjenesteKontaktInput(string? Generelt, IReadOnlyList<string> KommunenKanVeiledeOm);
public sealed record TjenesteEndringerInput(string? Plikt, IReadOnlyList<string> Eksempler);

/// <summary>
/// Modellfilens "hva_bevillingen_innebarer"/"hva_ordningen_innebarer" — forent til ETT feltnavn
/// (modellfilen selv foreslo dette, §1.1.1-kommentaren). Et supersett av begge rettighetenes
/// underfelt (Serveringsbevilling: Innledning/Varighet/Plikter/EndringerIVirksomheten/
/// KontrollOgTilsyn/AvgrensningMerknad; Fettutskiller: KravTilDrift/TommeavtaleOgKontroll/
/// Rapportering) — hver rettighet fyller bare ut det som gjelder for den, resten forblir null.
/// "konsekvenser_ved_brudd_pa_regelverket"/"...manglende_etterlevelse" er BEVISST IKKE med her — det
/// er allerede det ekte, eksisterende <see cref="TjenesteEntitet.KonsekvensVedBrudd"/>-feltet, ikke
/// duplisert inn i denne JSON-blobben.
/// </summary>
public sealed record TjenesteHvaRettighetenInnebarerInput(
    string? Innledning, string? Varighet, IReadOnlyList<string> Plikter,
    TjenesteEndringerInput? EndringerIVirksomheten, string? KontrollOgTilsyn, string? AvgrensningMerknad,
    string? KravTilDrift = null, string? TommeavtaleOgKontroll = null, string? Rapportering = null);

public sealed record TjenesteInnholdInput(
    string? TidspunktOgFrister, TjenesteInnsenderInput? InnsenderOgTilgang,
    IReadOnlyList<string> Vedlegg, string? VedleggMerknad,
    IReadOnlyList<string> OpplysningerSomSkalSendesInn, string? OpplysningerMerknad,
    IReadOnlyList<string> VeiledningOgUtfylling, string? VeiledningMerknad,
    TjenesteInnsendingInput? InnsendingOgOppfolging, TjenesteKontaktInput? KontaktOgHjelp,
    TjenesteHvaRettighetenInnebarerInput? HvaRettighetenInnebarer);

/// <summary>
/// Ett fritt, egendefinert innholdselement ("+ Legg til eget innholdselement",
/// Tjenestedetalj-redesignrunden 2026-08-27) — se <see cref="TjenesteEntitet.EgneInnholdselementerJson"/>.
/// <paramref name="Id"/> genereres klientside (frontend bruker <c>crypto.randomUUID()</c>) og MÅ være
/// stabil over lagringer — den kan være mål for en felt-nivå regelverksreferanse
/// (<c>Felt = "egneInnholdselementer.{Id}"</c>, se feltnøkkel-konvensjonen under).
/// </summary>
public sealed record EgetInnholdselementInput(string Id, string Tittel, string? Tekst);

/// <summary>
/// Feltnøkkel-konvensjonen for <see cref="TjenesteRegelverksreferanseEntitet.Felt"/> (2026-08-27,
/// Tjenestedetalj-redesignrunden) — ÉN kilde til sannhet, samme nøkler frontend og backend bruker,
/// ALLTID de ekte DTO-feltnavnene (aldri en forkortelse/oversettelsestabell):
/// <list type="bullet">
/// <item>Grunnleggende-felt: <c>tittel</c>, <c>tjenestetype</c>, <c>beskrivelse</c>, <c>formal</c>,
/// <c>kompetentMyndighet</c>, <c>type</c>, <c>malgruppe</c>, <c>kanaler</c>, <c>kostnad</c>,
/// <c>behandlingstid</c>, <c>kontaktpunkt</c>, <c>konsekvensVedBrudd</c>, <c>sprak</c>,
/// <c>livshendelser</c>, <c>losKlassifisering</c>, <c>tjenesteomrade</c>, <c>output</c>.</item>
/// <item>Innhold-underfelt, punktum-adskilt fra roten <c>innhold</c>:
/// <c>innhold.tidspunktOgFrister</c>, <c>innhold.innsenderOgTilgang.hvemKanSende</c>,
/// <c>innhold.innsenderOgTilgang.innlogging</c>, <c>innhold.vedlegg</c>, <c>innhold.vedleggMerknad</c>,
/// <c>innhold.opplysningerSomSkalSendesInn</c>, <c>innhold.opplysningerMerknad</c>,
/// <c>innhold.veiledningOgUtfylling</c>, <c>innhold.veiledningMerknad</c>,
/// <c>innhold.innsendingOgOppfolging.kanal</c>, <c>innhold.innsendingOgOppfolging.etterMottak</c>,
/// <c>innhold.innsendingOgOppfolging.merknad</c>, <c>innhold.kontaktOgHjelp.generelt</c>,
/// <c>innhold.kontaktOgHjelp.kommunenKanVeiledeOm</c>,
/// <c>innhold.hvaRettighetenInnebarer.innledning</c>, <c>innhold.hvaRettighetenInnebarer.varighet</c>,
/// <c>innhold.hvaRettighetenInnebarer.plikter</c>,
/// <c>innhold.hvaRettighetenInnebarer.endringerIVirksomheten.plikt</c>,
/// <c>innhold.hvaRettighetenInnebarer.endringerIVirksomheten.eksempler</c>,
/// <c>innhold.hvaRettighetenInnebarer.kravTilDrift</c>,
/// <c>innhold.hvaRettighetenInnebarer.tommeavtaleOgKontroll</c>,
/// <c>innhold.hvaRettighetenInnebarer.rapportering</c>,
/// <c>innhold.hvaRettighetenInnebarer.kontrollOgTilsyn</c>,
/// <c>innhold.hvaRettighetenInnebarer.avgrensningMerknad</c>.</item>
/// <item>Frie innholdselementer: <c>egneInnholdselementer.{id}</c>.</item>
/// </list>
/// Ingen DB-CHECK/enum her — <c>Felt</c> er fri streng nettopp fordi custom-elementer har
/// dynamiske id-er en fast liste ikke kan romme. Frontend er den eneste stedet som faktisk
/// validerer/tilbyr disse nøklene i et grensesnitt (velgeren viser kun de feltene som faktisk
/// finnes på siden).
/// </summary>
internal static class TjenesteFeltnokler;

/// <summary>
/// Ett cross-tenant søketreff (2026-08-19, feature/tjenesteavhengighet-ekstern-referanse) — KUN
/// <c>Status="publisert"</c> tjenester fra ENHVER virksomhet er søkbare her, aldri utkast/andre statuser
/// fra en annen virksomhet enn kalleren (samme virksomhet-isolasjons-default som docs/02 §0.1 — draft-
/// arbeid er alltid privat). <see cref="VirksomhetNavn"/> er med for disambiguering i UI-et ("Registrer
/// matbedriften — Mattilsynet"-stilen), samme begrunnelse som andre steder i koden slår opp virksomhetens
/// navn ved siden av selve treffet.
/// </summary>
public sealed record TjenesteTverrTenantTreff(Guid Id, string Tittel, string? Beskrivelse, Guid VirksomhetId, string VirksomhetNavn);

/// <summary>
/// Tjenesteregister (CPSV-AP-NO, docs/03-domenemodell.md §1.5) — byggesteg 2. Navngitt "register",
/// ikke "Tjeneste", for å unngå kollisjon med domenebegrepet Tjeneste selv (jf. <see cref="TekstTaggTjeneste"/>/
/// <see cref="HandbokForfatterTjeneste"/>-suffikset). Samme stil som disse: primary-constructor DI,
/// <see cref="ArgumentException"/> for domenevalidering ("ingen gjettet fallback", §3.3), dual-write av
/// domenerad + proveniensrad i samme <c>SaveChangesAsync</c>.
/// </summary>
public sealed class TjenesteregisterTjeneste(RegelIdeDbContext db)
{
    // Samme 5-verdis statusløp som Rettskilde/Kodeliste (§3.1) — ikke full FSM-håndheving av lovlige
    // overganger i v1 (samme v1-forenkling som HandbokKommentarMetadataEntitet.Status, se §1.1.1).
    // internal (ikke private) siden TjenesteModellSkjema (docs/23) gjenbruker denne listen i det
    // genererte JSON Schema-et, i stedet for å duplisere verdiene og risikere drift.
    internal static readonly string[] GyldigeStatuser =
        ["utkast", "foreslatt_av_ai", "under_revisjon", "validert", "publisert", "tilbaketrukket", "arkivert"];

    /// <summary>Rettighetstype (2026-08-20, fra serveringsbevilling-modell-forslag.json sitt "type"-felt,
    /// KI-agentens nivå 2-forslag) — hardkodet, utvidbar liste, samme "ingen DB-CHECK"-holdning som
    /// <see cref="HandlingregisterTjeneste.GyldigeHandlingstyper"/>.</summary>
    internal static readonly string[] GyldigeRettighetstyper =
        ["myndighetsutovelse", "ytelse", "infrastruktur", "veiledning", "medvirkning"];

    private static string? Serialiser<T>(T? verdi) => verdi is null ? null : JsonSerializer.Serialize(verdi, JsonSerialiseringHjelper.Innstillinger);

    public Task<List<TjenesteEntitet>> ListerForAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.Tjenester
            .Where(t => t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende")
            .OrderBy(t => t.Tittel)
            .ToListAsync(ct);

    /// <summary>
    /// Lister ALLE tjenester tvers av ALLE virksomheter (2026-08-22, Johanns eksplisitte avklaring: "alle
    /// kan se alles tjenester, men kun pålogget virksomhet kan endre sine egne") — samme "åpne data for
    /// lesing, virksomhet-scopet for skriving"-holdning som <see cref="FinnAsync"/>/<see cref="OppdaterAsync"/>
    /// allerede har hatt for ENKELT-tjenesten, nå ført gjennom til LISTE-endepunktet også. <see cref="ListerForAsync"/>
    /// beholdes uendret (brukes fortsatt der "mine egne" faktisk er det man vil ha, om noe skulle trenge det).
    /// </summary>
    public Task<List<TjenesteEntitet>> ListerAlleAsync(CancellationToken ct = default) =>
        db.Tjenester
            .Where(t => t.Entitetsstatus == "gjeldende")
            .OrderBy(t => t.Tittel)
            .ToListAsync(ct);

    public Task<TjenesteEntitet?> FinnAsync(Guid id, CancellationToken ct = default) =>
        db.Tjenester.FirstOrDefaultAsync(t => t.Id == id && t.Entitetsstatus == "gjeldende", ct);

    /// <summary>
    /// Cross-tenant søk (2026-08-19) — for å finne en ANNEN virksomhets tjeneste som mål for en
    /// tjenesteavhengighet (gap 1 i docs/13-backlog.md-runden: det finnes i dag ingen måte å FINNE en
    /// annen tenants tjeneste å lenke til). Kun publiserte tjenester er synlige, fra ENHVER virksomhet —
    /// se <see cref="TjenesteTverrTenantTreff"/>s klassekommentar. Samme enkle
    /// ToLower().Contains()-søk som <see cref="LovdataKatalogTjeneste.SokAsync"/> — trenger ikke være
    /// mer sofistikert enn det.
    /// </summary>
    public async Task<List<TjenesteTverrTenantTreff>> SokTverrTenantAsync(string sokestreng, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sokestreng)) return [];

        // Projisert til en anonym type FØR OrderBy/Take — EF Core klarer ikke å oversette en
        // OrderBy over en egenkonstruert record (TjenesteTverrTenantTreff) rett etter en Join.
        // Selve record-en materialiseres derfor klient-side, etter ToListAsync.
        var lavSokestreng = sokestreng.ToLower();
        var rader = await db.Tjenester
            .Where(t => t.Entitetsstatus == "gjeldende" && t.Status == "publisert" &&
                (t.Tittel.ToLower().Contains(lavSokestreng) || (t.Beskrivelse != null && t.Beskrivelse.ToLower().Contains(lavSokestreng))))
            .Join(db.Virksomheter, t => t.VirksomhetId, v => v.Id,
                (t, v) => new { t.Id, t.Tittel, t.Beskrivelse, t.VirksomhetId, VirksomhetNavn = v.Navn })
            .OrderBy(x => x.Tittel)
            .Take(50)
            .ToListAsync(ct);
        return rader.Select(x => new TjenesteTverrTenantTreff(x.Id, x.Tittel, x.Beskrivelse, x.VirksomhetId, x.VirksomhetNavn)).ToList();
    }

    public Task<List<TjenesteRegelverksreferanseEntitet>> RegelverksreferanserForAsync(Guid tjenesteId, CancellationToken ct = default) =>
        db.TjenesteRegelverksreferanser.Where(r => r.TjenesteId == tjenesteId).ToListAsync(ct);

    public async Task<TjenesteEntitet> OpprettAsync(
        Guid virksomhetId, string tittel, string? beskrivelse, string? kompetentMyndighet, string? output,
        string? tjenestetype, IReadOnlyList<string>? malgruppe, IReadOnlyList<string>? kanaler, string? kostnad,
        string? behandlingstid, string? kontaktpunkt, string? konsekvensVedBrudd, IReadOnlyList<string>? sprak,
        string opprettetAv, CancellationToken ct = default,
        IReadOnlyList<string>? livshendelser = null, string? losKlassifisering = null, string? tjenesteomrade = null,
        string? type = null, string? formal = null, TjenesteInnholdInput? innhold = null,
        IReadOnlyList<EgetInnholdselementInput>? egneInnholdselementer = null)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }
        if (type is not null && !GyldigeRettighetstyper.Contains(type))
        {
            throw new ArgumentException($"Ukjent rettighetstype '{type}'. Gyldige verdier: {string.Join(", ", GyldigeRettighetstyper)}.");
        }

        var tjeneste = new TjenesteEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Tittel = tittel,
            Beskrivelse = beskrivelse,
            KompetentMyndighet = kompetentMyndighet,
            Output = output,
            Tjenestetype = tjenestetype,
            Malgruppe = malgruppe?.ToList() ?? [],
            Kanaler = kanaler?.ToList() ?? [],
            Kostnad = kostnad,
            Behandlingstid = behandlingstid,
            Kontaktpunkt = kontaktpunkt,
            KonsekvensVedBrudd = konsekvensVedBrudd,
            Sprak = sprak?.ToList() ?? [],
            Livshendelser = livshendelser?.ToList() ?? [],
            LosKlassifisering = losKlassifisering,
            Tjenesteomrade = tjenesteomrade,
            Type = type,
            Formal = formal,
            InnholdJson = Serialiser(innhold),
            EgneInnholdselementerJson = Serialiser(egneInnholdselementer?.ToList() ?? []) ?? "[]",
            Status = "utkast",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Tjenester.Add(tjeneste);
        db.Proveniens.Add(ProveniensHjelper.NyRad("tjeneste", tjeneste.Id, virksomhetId, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }

    /// <summary>
    /// Byggesteg 5 runde 1 («Identifiser tjenester») — landet med Status="foreslatt_av_ai" og en
    /// <see cref="ProveniensHjelper.NyForslagRad"/>-rad. Kun Tittel er obligatorisk fra agenten — de
    /// øvrige, allerede nullable CPSV-AP-NO-feltene settes hvis agenten faktisk fant belegg for dem
    /// (byggesteg 5 runde 3, utvidet fra kun Tittel/Beskrivelse), resten fylles ut av mennesket ved
    /// godkjenning, samme "generer minimum, menneske fullfører"-prinsipp som «opprett vilkår fra
    /// tagg». Bevisst ingen auto-opprettet <see cref="TjenesteRegelverksreferanseEntitet"/> — agenten
    /// vet hvilken rettskilde som inspirerte forslaget (se kildeReferanserJson), ikke hvilken
    /// spesifikk eId/paragraf.
    /// </summary>
    public async Task<TjenesteEntitet> OpprettForslagFraKiAsync(
        Guid virksomhetId, string tittel, string? beskrivelse, string? kompetentMyndighet, string? output,
        string? tjenestetype, IReadOnlyList<string>? malgruppe, IReadOnlyList<string>? kanaler, string? kostnad,
        string? behandlingstid, string? kontaktpunkt, string? konsekvensVedBrudd, IReadOnlyList<string>? sprak,
        string opprettetAv, string aiForslagVersjon, string? kildeReferanserJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }

        var tjeneste = new TjenesteEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Tittel = tittel,
            Beskrivelse = beskrivelse,
            KompetentMyndighet = kompetentMyndighet,
            Output = output,
            Tjenestetype = tjenestetype,
            Malgruppe = malgruppe?.ToList() ?? [],
            Kanaler = kanaler?.ToList() ?? [],
            Kostnad = kostnad,
            Behandlingstid = behandlingstid,
            Kontaktpunkt = kontaktpunkt,
            KonsekvensVedBrudd = konsekvensVedBrudd,
            Sprak = sprak?.ToList() ?? [],
            Status = "foreslatt_av_ai",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Tjenester.Add(tjeneste);
        db.Proveniens.Add(ProveniensHjelper.NyForslagRad("tjeneste", tjeneste.Id, virksomhetId, opprettetAv, aiForslagVersjon, kildeReferanserJson));
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }

    /// <summary>
    /// Sikkerhetsfiks 2026-08-20 (kjent hull, se docs/17-forvaltningsstruktur-master-tjeneste.md §2.2 og
    /// docs/18-vurdering-rettighet-samhandling-modell.md §D.7): denne metoden filtrerte tidligere kun på
    /// <c>Entitetsstatus</c> — enhver innlogget bruker kunne endre enhver annen virksomhets tjeneste hvis
    /// hun hadde id-en. Nå kreves at raden faktisk eies av <paramref name="virksomhetId"/>. Lesing
    /// (<see cref="FinnAsync"/>) er BEVISST urørt — samme "åpne data"-holdning som kodelistene, og hullet
    /// som ble flagget var skriving, ikke lesing.
    /// </summary>
    public async Task<TjenesteEntitet?> OppdaterAsync(
        Guid id, Guid virksomhetId, string tittel, string? beskrivelse, string? kompetentMyndighet, string? output,
        string? tjenestetype, IReadOnlyList<string>? malgruppe, IReadOnlyList<string>? kanaler, string? kostnad,
        string? behandlingstid, string? kontaktpunkt, string? konsekvensVedBrudd, IReadOnlyList<string>? sprak,
        string endretAv, CancellationToken ct = default,
        IReadOnlyList<string>? livshendelser = null, string? losKlassifisering = null, string? tjenesteomrade = null,
        string? type = null, string? formal = null, TjenesteInnholdInput? innhold = null,
        IReadOnlyList<EgetInnholdselementInput>? egneInnholdselementer = null)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }
        if (type is not null && !GyldigeRettighetstyper.Contains(type))
        {
            throw new ArgumentException($"Ukjent rettighetstype '{type}'. Gyldige verdier: {string.Join(", ", GyldigeRettighetstyper)}.");
        }

        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(
            t => t.Id == id && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null) return null;

        tjeneste.Tittel = tittel;
        tjeneste.Beskrivelse = beskrivelse;
        tjeneste.KompetentMyndighet = kompetentMyndighet;
        tjeneste.Output = output;
        tjeneste.Tjenestetype = tjenestetype;
        tjeneste.Malgruppe = malgruppe?.ToList() ?? [];
        tjeneste.Kanaler = kanaler?.ToList() ?? [];
        tjeneste.Kostnad = kostnad;
        tjeneste.Behandlingstid = behandlingstid;
        tjeneste.Kontaktpunkt = kontaktpunkt;
        tjeneste.KonsekvensVedBrudd = konsekvensVedBrudd;
        tjeneste.Sprak = sprak?.ToList() ?? [];
        tjeneste.Livshendelser = livshendelser?.ToList() ?? [];
        tjeneste.LosKlassifisering = losKlassifisering;
        tjeneste.Tjenesteomrade = tjenesteomrade;
        tjeneste.Type = type;
        tjeneste.Formal = formal;
        tjeneste.InnholdJson = Serialiser(innhold);
        tjeneste.EgneInnholdselementerJson = Serialiser(egneInnholdselementer?.ToList() ?? []) ?? "[]";
        tjeneste.SistEndretAv = endretAv;
        tjeneste.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        tjeneste.Versjon++;
        db.Proveniens.Add(ProveniensHjelper.NyRad("tjeneste", tjeneste.Id, tjeneste.VirksomhetId, "endret", endretAv));
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }

    public async Task<TjenesteRegelverksreferanseEntitet> KobleRegelverksreferanseAsync(
        Guid tjenesteId, Guid tilRettskildeId, string tilEid, CancellationToken ct = default, string? felt = null)
    {
        if (!await db.Tjenester.AnyAsync(t => t.Id == tjenesteId && t.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Tjeneste '{tjenesteId}' finnes ikke.");
        }
        if (!await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == tilRettskildeId && n.Eid == tilEid, ct))
        {
            throw new ArgumentException($"Målnoden '{tilEid}' finnes ikke i rettskilde '{tilRettskildeId}'.");
        }
        // Samme (rettskilde, eId) kan kobles til BÅDE den flate listen (felt=null) OG ett eller
        // flere enkeltfelt — duplikatsjekken må derfor inkludere Felt, se de to partial-indeksene
        // i RegelIdeDbContext.cs.
        if (await db.TjenesteRegelverksreferanser.AnyAsync(
                r => r.TjenesteId == tjenesteId && r.TilRettskildeId == tilRettskildeId && r.TilEid == tilEid && r.Felt == felt, ct))
        {
            throw new ArgumentException("Denne regelverksreferansen er allerede koblet.");
        }

        var referanse = new TjenesteRegelverksreferanseEntitet
        {
            Id = Guid.NewGuid(), TjenesteId = tjenesteId, TilRettskildeId = tilRettskildeId, TilEid = tilEid, Felt = felt,
        };
        db.TjenesteRegelverksreferanser.Add(referanse);
        await db.SaveChangesAsync(ct);
        return referanse;
    }

    public async Task<bool> FjernRegelverksreferanseAsync(Guid referanseId, CancellationToken ct = default)
    {
        var referanse = await db.TjenesteRegelverksreferanser.FirstOrDefaultAsync(r => r.Id == referanseId, ct);
        if (referanse is null) return false;
        db.TjenesteRegelverksreferanser.Remove(referanse);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TjenesteEntitet?> SettStatusAsync(
        Guid id, Guid virksomhetId, string nyStatus, string endretAv, CancellationToken ct = default, string? godkjentAv = null)
    {
        if (!GyldigeStatuser.Contains(nyStatus))
        {
            throw new ArgumentException($"Ukjent status '{nyStatus}'. Gyldige verdier: {string.Join(", ", GyldigeStatuser)}.");
        }

        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(
            t => t.Id == id && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null) return null;

        tjeneste.Status = nyStatus;
        tjeneste.SistEndretAv = endretAv;
        tjeneste.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var proveniens = ProveniensHjelper.NyRad(
            "tjeneste", tjeneste.Id, tjeneste.VirksomhetId, nyStatus is "publisert" or "validert" ? nyStatus : "endret", endretAv);
        proveniens.GodkjentAv = godkjentAv;
        db.Proveniens.Add(proveniens);
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }

    /// <summary>
    /// Byggesteg 4 — kobler tjenesten til rotnoden (alltid en Regelnode, INV-5) i vilkårstreet.
    /// Lukker gapet fra byggesteg 2 ("vilkårskobling ... kommer i byggesteg 4", docs/06-veikart.md).
    /// </summary>
    public async Task<TjenesteEntitet?> SettRotnodeAsync(Guid tjenesteId, Guid virksomhetId, Guid regelnodeId, CancellationToken ct = default)
    {
        if (!await db.Regelnoder.AnyAsync(r => r.Id == regelnodeId && r.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen regelnode med id '{regelnodeId}'.");
        }

        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(
            t => t.Id == tjenesteId && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null) return null;

        tjeneste.RotnodeId = regelnodeId;
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }

    /// <summary>
    /// Fjerner koblingen mellom tjenesten og dens rotnode (2026-07-31, fasit-runde 5) — selve
    /// regelnoden slettes ikke, kun referansen fra Tjenesten. Nødvendig fordi opprettelse av en
    /// rotnode i dag er en i praksis irreversibel handling uten dette — se
    /// docs/12-fasit-handbok-leveranse.md.
    /// </summary>
    public async Task<TjenesteEntitet?> FjernRotnodeAsync(Guid tjenesteId, Guid virksomhetId, CancellationToken ct = default)
    {
        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(
            t => t.Id == tjenesteId && t.VirksomhetId == virksomhetId && t.Entitetsstatus == "gjeldende", ct);
        if (tjeneste is null) return null;

        tjeneste.RotnodeId = null;
        await db.SaveChangesAsync(ct);
        return tjeneste;
    }
}
