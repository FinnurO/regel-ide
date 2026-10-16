using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Alt som trengs for å binde en generisk navneform i én rettskilde (issue #378 B5).</summary>
/// <param name="Referent">Nøyaktig én av virksomhet og begrep (klasse eller rolle).</param>
/// <param name="Paragrafspenn">Tom = hele rettskilden.</param>
public sealed record NyNavneformBinding(
    Guid NavneformId, Guid RettskildeId, Kantnode Referent,
    IReadOnlyList<ParagrafspennPar>? Paragrafspenn = null,
    Guid? HjemmelRettskildeId = null, string? HjemmelEid = null,
    string? KildeUtenforKorpusTekst = null, string? KildeUtenforKorpusLenke = null,
    string? KildeUtenforKorpusType = null, string? KildeUtenforKorpusDokumentasjon = null,
    string? Kunnskapsstatus = null, string? Begrunnelse = null, IReadOnlyList<string>? Tolkningsargumenter = null,
    string Status = Strukturkanter.ManueltForeslatt, string OppdagelsesKilde = "manuell", string? Kommentar = null,
    // [Ny, issue #378, Johanns beslutning 2026-10-16] Bindingens egen gyldighet. Null = åpen ende.
    DateOnly? GyldigFra = null, DateOnly? GyldigTil = null);

/// <summary>Utfallet av å slå opp en generisk omtale i én node.</summary>
public static class GeneriskOppslagstatus
{
    /// <summary>En godkjent binding dekker noden — referenten er kjent.</summary>
    public const string Bundet = "bundet";
    /// <summary>Ingen godkjent binding dekker noden. Omtalen står som generisk, uten kobling.</summary>
    public const string Ubundet = "ubundet";
    /// <summary>To like smale godkjente bindinger dekker noden med ulik referent — ingen valgt.</summary>
    public const string Flertydig = "flertydig";
}

/// <summary>Resultatet av et oppslag: status, bindingen som vant (ved <c>bundet</c>) og referenten.</summary>
public sealed record GeneriskOppslag(string Status, NavneformBindingEntitet? Binding, Kantnode? Referent, int AntallForeslatteBindinger);

/// <summary>Én binding slik den vises: referentens navn og type, rettskildens tittel.</summary>
public sealed record NavneformBindingVisning(
    Guid Id, Guid NavneformId, string Term, Guid RettskildeId, string RettskildeTittel,
    IReadOnlyList<ParagrafspennPar> Paragrafspenn, KantnodeVisning Referent,
    Guid? HjemmelRettskildeId, string? HjemmelEid,
    string? KildeUtenforKorpusTekst, string? KildeUtenforKorpusLenke, string? KildeUtenforKorpusType, string? KildeUtenforKorpusDokumentasjon,
    string? Kunnskapsstatus, string? Begrunnelse, IReadOnlyList<string>? Tolkningsargumenter,
    string Status, string OppdagelsesKilde, string? Kommentar, string? AvvisningBegrunnelse,
    string OpprettetAv, DateTimeOffset OpprettetTidspunkt, string? SistEndretAv, DateTimeOffset? SistEndretTidspunkt,
    DateOnly? GyldigFra, DateOnly? GyldigTil);

/// <summary>AC7: én rettskilde der en generisk navneform er tagget eller bundet.</summary>
public sealed record GeneriskBrukIRettskilde(Guid RettskildeId, string RettskildeTittel, int AntallTagger, IReadOnlyList<NavneformBindingVisning> Bindinger);

/// <summary>AC7: «hvor brukes ‹styret›, og hva betyr det i hver rettskilde?»</summary>
public sealed record GeneriskNavneformOversikt(Guid NavneformId, string Term, int AntallTagger, IReadOnlyList<GeneriskBrukIRettskilde> Rettskilder);

/// <summary>Oppløsningen av én tagg mot en generisk navneform (AC6 — taggen bruker bindingen).</summary>
public sealed record GeneriskTaggOppslag(Guid TaggId, Guid NavneformId, string Status, Guid? BindingId, KantnodeVisning? Referent);

/// <summary>
/// [Ny, issue #378 B4–B7, Johanns godkjenning 2026-10-15] Generiske navneformer og bindingen av dem per rettskilde
/// (<see cref="NavneformBindingEntitet"/>). Den ENESTE skriveveien til <c>navneform_bindinger</c>, og den ene stedet oppløsningen
/// generisk omtale → referent skjer — lasteren (<see cref="Strukturkonvertering.StrukturforslagTjeneste"/>), taggvisningen og
/// virksomhetens «brukt i»-liste kaller hit.
/// <para>
/// <b>Termen lagres med små bokstaver.</b> En generisk omtale er et fellesnavn; stor forbokstav i «Styret skal …» er rettskrivning
/// (setningsstart), ikke en del av navnet. Én rad per term for hele korpuset (unik indeks <c>ux_begreper_generisk_term</c>).
/// </para>
/// </summary>
public sealed class GeneriskNavneformTjeneste(RegelIdeDbContext db)
{
    private const string ProveniensType = "navneform_binding";

    /// <summary>Begrepstypene en binding kan peke på (B5: «aktør, klasse eller rolle»). Område er ikke en referent for en
    /// aktøromtale; et uavklart <c>gruppe</c>-begrep må først få type (#310).</summary>
    public static readonly string[] ReferentBegrepstyper = [Nodetyper.Klasse, Nodetyper.Rolle];

    public static string NormaliserTerm(string term) => term.Trim().ToLowerInvariant();

    /// <summary>Den generiske navneformen for termen, eller null.</summary>
    public Task<BegrepEntitet?> FinnAsync(string term, CancellationToken ct = default)
    {
        var t = NormaliserTerm(term);
        return db.Begreper.FirstOrDefaultAsync(b => b.Navneformgrunn == VirksomhetsbegrepTjeneste.Generisk
            && b.Entitetsstatus == "gjeldende" && b.Term == t, ct);
    }

    /// <summary>
    /// Finner eller oppretter den generiske navneformen for <paramref name="term"/> (B4). Avvises når termen ALT er en navneform for en
    /// bestemt virksomhet: da er den ikke generisk, og en ny rad ved siden av ville gjort oppslaget flertydig. Den eksisterende navneformen
    /// må da fjernes eller rettes først — det avgjøres ikke her.
    /// </summary>
    public async Task<(BegrepEntitet Navneform, bool VarNy)> OpprettEllerFinnAsync(string term, string opprettetAv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) throw new ArgumentException("Termen kan ikke være tom. Ingen gjettet fallback.");
        var t = NormaliserTerm(term);
        var finnes = await FinnAsync(t, ct);
        if (finnes is not null) return (finnes, false);
        var bundne = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                        && b.Term.ToLower() == t)
            .Join(db.Virksomheter, b => b.VirksomhetReferanseId, v => v.Id, (b, v) => v.Navn).Take(3).ToListAsync(ct);
        if (bundne.Count > 0)
        {
            throw new ArgumentException(
                $"«{t}» er alt en navneform for {string.Join(", ", bundne)}. En generisk navneform med samme term ville gjort oppslaget "
                + "flertydig — fjern eller rett den eksisterende navneformen først. Ingen gjettet fallback.");
        }
        var navneform = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = null,
            Begrepskategori = "virksomhet",
            VirksomhetReferanseId = null,
            Navneformgrunn = VirksomhetsbegrepTjeneste.Generisk,
            Term = t,
            Status = "publisert",
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(navneform);
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", navneform.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return (navneform, true);
    }

    /// <summary>
    /// Oppretter en binding (B5). Valideringen er strukturkantenes (#311/#367): referenten finnes og har lov type, paragrafspennet og
    /// hjemmelen finnes, hjemmel ELLER kilde utenfor korpus (med type og dokumentasjon), og kunnskapsstatusen har grunnlaget den krever
    /// (docs/profil/03 §5). Samme virkeområde (navneform, rettskilde, spenn) to ganger er en motsigelse og avvises — en revisjon gjøres
    /// ved å avvise den gamle først.
    /// </summary>
    public async Task<NavneformBindingEntitet> OpprettBindingAsync(NyNavneformBinding ny, string opprettetAv, CancellationToken ct = default)
    {
        var navneform = await db.Begreper.FirstOrDefaultAsync(b => b.Id == ny.NavneformId && b.Entitetsstatus == "gjeldende", ct)
            ?? throw new ArgumentException($"Fant ingen navneform med id '{ny.NavneformId}'. Ingen gjettet fallback.");
        if (navneform.Navneformgrunn != VirksomhetsbegrepTjeneste.Generisk)
        {
            throw new ArgumentException($"«{navneform.Term}» er ikke en generisk navneform — bare generiske navneformer bindes per rettskilde.");
        }
        if (!await db.Rettskilder.AnyAsync(r => r.Id == ny.RettskildeId, ct))
        {
            throw new ArgumentException($"Fant ingen rettskilde med id '{ny.RettskildeId}'. Ingen gjettet fallback.");
        }
        if (!ny.Referent.ErGyldig) throw new ArgumentException("Referenten må være nøyaktig én av virksomhet og begrep.");
        if (ny.Referent.VirksomhetId is { } vid && !await db.Virksomheter.AnyAsync(v => v.Id == vid, ct))
        {
            throw new ArgumentException($"Fant ingen virksomhet med id '{vid}'. Ingen gjettet fallback.");
        }
        if (ny.Referent.BegrepId is { } bid)
        {
            var kategori = await db.Begreper.Where(b => b.Id == bid && b.Entitetsstatus == "gjeldende").Select(b => b.Begrepskategori)
                .FirstOrDefaultAsync(ct);
            if (kategori is null || !ReferentBegrepstyper.Contains(kategori))
            {
                throw new ArgumentException(
                    $"Referenten må være en virksomhet eller et begrep av typen klasse eller rolle (fant '{kategori ?? "ingen"}').");
            }
        }

        var spenn = (ny.Paragrafspenn ?? []).Where(p => !string.IsNullOrWhiteSpace(p.FraEid))
            .Select(p => new ParagrafspennPar(p.FraEid.Trim(), string.IsNullOrWhiteSpace(p.TilEid) ? null : p.TilEid.Trim())).ToList();
        foreach (var par in spenn)
        {
            foreach (var eid in new[] { par.FraEid, par.TilEid }.OfType<string>())
            {
                if (!await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == ny.RettskildeId && n.Eid == eid && n.Entitetsstatus == "gjeldende", ct))
                {
                    throw new ArgumentException($"Fant ingen node med eId '{eid}' i rettskilden. Ingen gjettet fallback.");
                }
            }
        }
        var spennJson = JsonSerializer.Serialize(spenn, JsonSerialiseringHjelper.Innstillinger);

        // Hjemmel ELLER kilde utenfor korpus — streng, som ck_navneform_bindinger_kilde.
        var kildeTekst = string.IsNullOrWhiteSpace(ny.KildeUtenforKorpusTekst) ? null : ny.KildeUtenforKorpusTekst.Trim();
        var kildeLenke = string.IsNullOrWhiteSpace(ny.KildeUtenforKorpusLenke) ? null : ny.KildeUtenforKorpusLenke.Trim();
        Guid? hjemmelNodeId = null;
        if (ny.HjemmelRettskildeId is { } hId)
        {
            if (kildeTekst is not null || kildeLenke is not null || ny.KildeUtenforKorpusType is not null || ny.KildeUtenforKorpusDokumentasjon is not null)
            {
                throw new ArgumentException("Oppgi ENTEN hjemmel i korpus ELLER kilde utenfor korpus, ikke begge.");
            }
            if (string.IsNullOrWhiteSpace(ny.HjemmelEid)) throw new ArgumentException("Hjemmel i korpus krever hjemmel-eId (bestemmelsen).");
            var noder = await db.RettskildeNoder
                .Where(n => n.RettskildeId == hId && n.Eid == ny.HjemmelEid && n.Entitetsstatus == "gjeldende")
                .Select(n => n.Id).Take(2).ToListAsync(ct);
            if (noder.Count == 0) throw new ArgumentException($"Fant ingen node med eId '{ny.HjemmelEid}' i hjemmelen. Ingen gjettet fallback.");
            if (noder.Count == 1) hjemmelNodeId = noder[0];
        }
        else
        {
            if (ny.HjemmelEid is not null) throw new ArgumentException("Hjemmel-eId uten hjemmel-rettskilde.");
            if (kildeTekst is null || ny.KildeUtenforKorpusType is null || ny.KildeUtenforKorpusDokumentasjon is null)
            {
                throw new ArgumentException(
                    "Uten hjemmel i korpus må bindingen si hvor betydningen kommer fra: kilde utenfor korpus med tekst, type og "
                    + "dokumentasjon (primær/sekundær). Ingen gjettet fallback.");
            }
            if (!Strukturkanter.KildeUtenforKorpusTyper.Contains(ny.KildeUtenforKorpusType))
            {
                throw new ArgumentException($"Ukjent kildetype '{ny.KildeUtenforKorpusType}'. Gyldige: {string.Join(", ", Strukturkanter.KildeUtenforKorpusTyper)}.");
            }
            if (!Strukturkanter.KildeDokumentasjoner.Contains(ny.KildeUtenforKorpusDokumentasjon))
            {
                throw new ArgumentException($"Ukjent dokumentasjon '{ny.KildeUtenforKorpusDokumentasjon}'. Gyldige: primaer, sekundaer.");
            }
        }

        // Kunnskapsstatus (docs/profil/03 §5). Avledet krever premisser, og en binding har ingen premisstabell — den avvises heller enn
        // å lagres uten grunnlag. Foreldet settes bare av en revisjon.
        var status = ny.Kunnskapsstatus;
        var begrunnelse = string.IsNullOrWhiteSpace(ny.Begrunnelse) ? null : ny.Begrunnelse.Trim();
        var argumenter = ny.Tolkningsargumenter is { Count: > 0 } a ? a.Distinct().ToList() : null;
        if (status is not null)
        {
            if (!Strukturkanter.Kunnskapsstatuser.Contains(status) || status is Strukturkanter.Avledet or Strukturkanter.Foreldet)
            {
                throw new ArgumentException(
                    $"Kunnskapsstatus '{status}' kan ikke brukes på en binding. Gyldige: tekstfunn, normalisering, fortolkning, "
                    + "eksternt_dokumentert, uavklart, omstridt.");
            }
            if (status is Strukturkanter.Tekstfunn or Strukturkanter.Normalisering && ny.HjemmelRettskildeId is null)
            {
                throw new ArgumentException($"{status} krever hjemmel i korpus med eId (docs/profil/03 §5).");
            }
            if (status == Strukturkanter.EksterntDokumentert && kildeTekst is null)
            {
                throw new ArgumentException("eksternt_dokumentert krever kilde utenfor korpus (docs/profil/03 §5).");
            }
            if (status == Strukturkanter.Fortolkning && (begrunnelse is null || argumenter is null))
            {
                throw new ArgumentException("fortolkning krever begrunnelse og minst ett tolkningsargument (docs/profil/03 §6).");
            }
        }
        if (argumenter?.FirstOrDefault(x => !Strukturkanter.Tolkningsargumenter.Contains(x)) is { } ukjent)
        {
            throw new ArgumentException($"Ukjent tolkningsargument '{ukjent}'. Gyldige: {string.Join(", ", Strukturkanter.Tolkningsargumenter)}.");
        }
        if (ny.GyldigFra is { } gf && ny.GyldigTil is { } gt && gf > gt)
        {
            throw new ArgumentException("Gyldig fra kan ikke være etter gyldig til. Ingen gjettet fallback.");
        }
        if (!Strukturkanter.Statuser.Contains(ny.Status) || ny.Status == Strukturkanter.Avvist)
        {
            throw new ArgumentException($"Ugyldig godkjenning '{ny.Status}' ved opprettelse.");
        }

        // Samme virkeområde med OVERLAPPENDE gyldighet er en motsigelse. Etter hverandre i tid er det ikke (ressortendring, B1).
        var nyFra = ny.GyldigFra ?? DateOnly.MinValue;
        var nyTil = ny.GyldigTil ?? DateOnly.MaxValue;
        var konflikt = await db.NavneformBindinger.FirstOrDefaultAsync(b => b.NavneformId == ny.NavneformId && b.RettskildeId == ny.RettskildeId
            && b.ParagrafspennJson == spennJson && b.Entitetsstatus == "gjeldende" && b.Status != Strukturkanter.Avvist
            && (b.GyldigFra == null || b.GyldigFra <= nyTil) && (b.GyldigTil == null || b.GyldigTil >= nyFra), ct);
        if (konflikt is not null)
        {
            throw new ArgumentException(
                $"«{navneform.Term}» er alt bundet med samme virkeområde og overlappende gyldighet i denne rettskilden (binding {konflikt.Id}, "
                + $"status {konflikt.Status}). Avvis den, eller avgrens gyldigheten, om betydningen skal endres — to slike bindinger er en motsigelse.");
        }

        var binding = new NavneformBindingEntitet
        {
            Id = Guid.NewGuid(),
            NavneformId = ny.NavneformId,
            RettskildeId = ny.RettskildeId,
            ParagrafspennJson = spennJson,
            TilVirksomhetId = ny.Referent.VirksomhetId,
            TilBegrepId = ny.Referent.BegrepId,
            HjemmelRettskildeId = ny.HjemmelRettskildeId,
            HjemmelEid = ny.HjemmelRettskildeId is null ? null : ny.HjemmelEid,
            HjemmelNodeId = hjemmelNodeId,
            KildeUtenforKorpusTekst = ny.HjemmelRettskildeId is null ? kildeTekst : null,
            KildeUtenforKorpusLenke = ny.HjemmelRettskildeId is null ? kildeLenke : null,
            KildeUtenforKorpusType = ny.HjemmelRettskildeId is null ? ny.KildeUtenforKorpusType : null,
            KildeUtenforKorpusDokumentasjon = ny.HjemmelRettskildeId is null ? ny.KildeUtenforKorpusDokumentasjon : null,
            Kunnskapsstatus = status,
            Begrunnelse = begrunnelse,
            Tolkningsargumenter = argumenter,
            Profilversjon = status is null ? null : Strukturkanter.Profilversjon,
            GyldigFra = ny.GyldigFra,
            GyldigTil = ny.GyldigTil,
            Status = ny.Status,
            OppdagelsesKilde = string.IsNullOrWhiteSpace(ny.OppdagelsesKilde) ? "manuell" : ny.OppdagelsesKilde,
            Kommentar = string.IsNullOrWhiteSpace(ny.Kommentar) ? null : ny.Kommentar.Trim(),
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.NavneformBindinger.Add(binding);
        db.Proveniens.Add(ProveniensHjelper.NyRad(ProveniensType, binding.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return binding;
    }

    /// <summary>Godkjenner et forslag til binding. Bare forslag (<see cref="Strukturkanter.Ventende"/>) kan godkjennes.</summary>
    public async Task<NavneformBindingEntitet?> GodkjennAsync(Guid id, string godkjentAv, CancellationToken ct = default)
    {
        var b = await db.NavneformBindinger.FirstOrDefaultAsync(x => x.Id == id && x.Entitetsstatus == "gjeldende", ct);
        if (b is null) return null;
        if (!Strukturkanter.Ventende.Contains(b.Status)) throw new ArgumentException($"Bindingen har status '{b.Status}' — bare forslag kan godkjennes.");
        b.Status = Strukturkanter.Godkjent;
        b.SistEndretAv = godkjentAv;
        b.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var p = ProveniensHjelper.NyRad(ProveniensType, b.Id, virksomhetId: null, "validert", godkjentAv);
        p.GodkjentAv = godkjentAv;
        db.Proveniens.Add(p);
        await db.SaveChangesAsync(ct);
        return b;
    }

    /// <summary>Avviser en binding — også en godkjent (betydningen var feil). Raden SLETTES IKKE (#374): den står som historikk med hvem,
    /// når og hvorfor, og et nytt forslag med samme virkeområde kan da registreres.</summary>
    public async Task<NavneformBindingEntitet?> AvvisAsync(Guid id, string avvistAv, string? begrunnelse, CancellationToken ct = default)
    {
        var b = await db.NavneformBindinger.FirstOrDefaultAsync(x => x.Id == id && x.Entitetsstatus == "gjeldende", ct);
        if (b is null) return null;
        if (b.Status == Strukturkanter.Avvist) throw new ArgumentException("Bindingen er alt avvist.");
        var grunn = string.IsNullOrWhiteSpace(begrunnelse) ? null : begrunnelse.Trim();
        var p = ProveniensHjelper.NyRad(ProveniensType, b.Id, virksomhetId: null, "avvist", avvistAv);
        p.KildeReferanserJson = JsonSerializer.Serialize(new { forStatus = b.Status, begrunnelse = grunn });
        b.Status = Strukturkanter.Avvist;
        b.SistEndretAv = avvistAv;
        b.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        b.AvvisningBegrunnelse = grunn;
        db.Proveniens.Add(p);
        await db.SaveChangesAsync(ct);
        return b;
    }

    // ------------------------------------------------------------------ Oppløsning

    private sealed record Nodetre(Dictionary<string, (Guid Id, Guid? Forelder, int Sortering)> PerEid, Dictionary<Guid, Guid?> Forelder);

    private readonly Dictionary<Guid, Nodetre> _nodetrer = [];

    private async Task<Nodetre> NodetreAsync(Guid rettskildeId, CancellationToken ct)
    {
        if (_nodetrer.TryGetValue(rettskildeId, out var t)) return t;
        var noder = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende")
            .Select(n => new { n.Id, n.Eid, n.ParentNodeId, n.Sorteringsrekkefolge }).ToListAsync(ct);
        var perEid = new Dictionary<string, (Guid, Guid?, int)>(StringComparer.Ordinal);
        foreach (var n in noder) perEid.TryAdd(n.Eid, (n.Id, n.ParentNodeId, n.Sorteringsrekkefolge));
        t = new Nodetre(perEid, noder.ToDictionary(n => n.Id, n => n.ParentNodeId));
        _nodetrer[rettskildeId] = t;
        return t;
    }

    /// <summary>
    /// Bredden av spennet målt i noder, eller null når noden <paramref name="eid"/> ikke er dekket. Et par (Fra, Til) dekker nodene fra Fra
    /// til og med Til i dokumentrekkefølge, og alt under dem; et par uten Til dekker Fra og alt under den. Tomt spenn = hele rettskilden.
    /// </summary>
    private static int? Dekning(Nodetre tre, IReadOnlyList<ParagrafspennPar> spenn, string eid)
    {
        if (spenn.Count == 0) return int.MaxValue;
        if (!tre.PerEid.TryGetValue(eid, out var node)) return null;
        bool UnderEllerLik(Guid nodeId, HashSet<Guid> mål)
        {
            for (Guid? cur = nodeId; cur is { } c; cur = tre.Forelder.GetValueOrDefault(c)) if (mål.Contains(c)) return true;
            return false;
        }
        var bredde = 0;
        var dekket = false;
        foreach (var par in spenn)
        {
            if (!tre.PerEid.TryGetValue(par.FraEid, out var fra)) continue;
            var til = par.TilEid is not null && tre.PerEid.TryGetValue(par.TilEid, out var t) ? t : fra;
            var (lo, hi) = (Math.Min(fra.Sortering, til.Sortering), Math.Max(fra.Sortering, til.Sortering));
            var røtter = tre.PerEid.Values.Where(v => v.Sortering >= lo && v.Sortering <= hi).Select(v => v.Id).ToHashSet();
            var iSpennet = tre.PerEid.Values.Where(v => UnderEllerLik(v.Id, røtter)).ToList();
            bredde += iSpennet.Count;
            if (UnderEllerLik(node.Id, røtter)) dekket = true;
        }
        return dekket ? bredde : null;
    }

    /// <summary>
    /// Slår opp hva generisk navneform <paramref name="navneformId"/> betyr i noden <paramref name="eid"/> i <paramref name="rettskildeId"/>
    /// (B6). Bare GODKJENTE bindinger teller; det smaleste spennet som dekker noden vinner. Like smale med ulik referent → flertydig.
    /// [ENDRET, issue #378, Johanns beslutning 2026-10-16] Bare bindinger som gjelder på <paramref name="dato"/> (standard: i dag) teller —
    /// også i tellingen av foreslåtte.
    /// </summary>
    public async Task<GeneriskOppslag> LosOppAsync(
        Guid navneformId, Guid rettskildeId, string? eid, CancellationToken ct = default, DateOnly? dato = null)
    {
        var d = dato ?? DateOnly.FromDateTime(DateTime.Today);
        var bindinger = await db.NavneformBindinger
            .Where(b => b.NavneformId == navneformId && b.RettskildeId == rettskildeId && b.Entitetsstatus == "gjeldende"
                        && b.Status != Strukturkanter.Avvist
                        && (b.GyldigFra == null || b.GyldigFra <= d) && (b.GyldigTil == null || b.GyldigTil >= d))
            .ToListAsync(ct);
        var foreslatte = bindinger.Count(b => b.Status != Strukturkanter.Godkjent);
        var godkjente = bindinger.Where(b => b.Status == Strukturkanter.Godkjent).ToList();
        if (godkjente.Count == 0) return new GeneriskOppslag(GeneriskOppslagstatus.Ubundet, null, null, foreslatte);
        var tre = await NodetreAsync(rettskildeId, ct);
        var dekkende = godkjente
            .Select(b => (B: b, Bredde: Dekning(tre, LesSpenn(b.ParagrafspennJson), eid ?? "")))
            .Where(x => x.Bredde is not null).ToList();
        if (dekkende.Count == 0) return new GeneriskOppslag(GeneriskOppslagstatus.Ubundet, null, null, foreslatte);
        var smalest = dekkende.Min(x => x.Bredde!.Value);
        var vinnere = dekkende.Where(x => x.Bredde == smalest).ToList();
        var referenter = vinnere.Select(x => (x.B.TilVirksomhetId, x.B.TilBegrepId)).Distinct().ToList();
        if (referenter.Count > 1) return new GeneriskOppslag(GeneriskOppslagstatus.Flertydig, null, null, foreslatte);
        var vinner = vinnere[0].B;
        return new GeneriskOppslag(GeneriskOppslagstatus.Bundet, vinner, new Kantnode(vinner.TilVirksomhetId, vinner.TilBegrepId), foreslatte);
    }

    public static IReadOnlyList<ParagrafspennPar> LesSpenn(string json) =>
        JsonSerializer.Deserialize<List<ParagrafspennPar>>(json, JsonSerialiseringHjelper.Innstillinger) ?? [];

    /// <summary>
    /// AC6: oppløsningen av alle gjeldende virksomhet-tagger i <paramref name="rettskildeId"/> som peker på en generisk navneform. Brukes av
    /// rettskildesiden, så markeringen «styret» lenker til det styret bindingen sier — eller står ukoblet når den ikke er bundet.
    /// </summary>
    public async Task<IReadOnlyList<GeneriskTaggOppslag>> LosOppTaggerAsync(Guid rettskildeId, CancellationToken ct = default)
    {
        var tagger = await db.TekstTagger
            .Where(t => t.RettskildeId == rettskildeId && t.Kind == "virksomhet" && t.RefId != null && t.Entitetsstatus == "gjeldende")
            .Join(db.Begreper.Where(b => b.Navneformgrunn == VirksomhetsbegrepTjeneste.Generisk), t => t.RefId, b => b.Id,
                (t, b) => new { t.Id, t.NodeEid, NavneformId = b.Id })
            .ToListAsync(ct);
        var ut = new List<GeneriskTaggOppslag>();
        var navn = new Dictionary<(Guid?, Guid?), KantnodeVisning?>();
        foreach (var t in tagger)
        {
            var o = await LosOppAsync(t.NavneformId, rettskildeId, t.NodeEid, ct);
            KantnodeVisning? referent = null;
            if (o.Referent is { } r)
            {
                var nokkel = (r.VirksomhetId, r.BegrepId);
                if (!navn.TryGetValue(nokkel, out referent)) navn[nokkel] = referent = await VisningAsync(r, ct);
            }
            ut.Add(new GeneriskTaggOppslag(t.Id, t.NavneformId, o.Status, o.Binding?.Id, referent));
        }
        return ut;
    }

    private async Task<KantnodeVisning?> VisningAsync(Kantnode node, CancellationToken ct)
    {
        if (node.VirksomhetId is { } v)
        {
            return await db.Virksomheter.Where(x => x.Id == v).Select(x => new KantnodeVisning("virksomhet", x.Id, x.Navn, x.Aktortype))
                .FirstOrDefaultAsync(ct);
        }
        if (node.BegrepId is { } b)
        {
            return await db.Begreper.Where(x => x.Id == b).Select(x => new KantnodeVisning("begrep", x.Id, x.Term, x.Begrepskategori))
                .FirstOrDefaultAsync(ct);
        }
        return null;
    }

    /// <summary>Bindingene, med navn — for én navneform, én rettskilde, eller alle. Avviste bare med <paramref name="visAvviste"/>.</summary>
    public async Task<List<NavneformBindingVisning>> HentBindingerAsync(
        Guid? navneformId = null, Guid? rettskildeId = null, bool visAvviste = false, CancellationToken ct = default)
    {
        var rader = await db.NavneformBindinger
            .Where(b => b.Entitetsstatus == "gjeldende" && (visAvviste || b.Status != Strukturkanter.Avvist)
                        && (navneformId == null || b.NavneformId == navneformId) && (rettskildeId == null || b.RettskildeId == rettskildeId))
            .Join(db.Begreper, b => b.NavneformId, n => n.Id, (b, n) => new { B = b, n.Term })
            .Join(db.Rettskilder, x => x.B.RettskildeId, r => r.Id, (x, r) => new { x.B, x.Term, Tittel = r.Kortnavn ?? r.Tittel })
            .OrderBy(x => x.Term).ThenBy(x => x.Tittel).ToListAsync(ct);
        var ut = new List<NavneformBindingVisning>();
        foreach (var x in rader)
        {
            var b = x.B;
            var referent = await VisningAsync(new Kantnode(b.TilVirksomhetId, b.TilBegrepId), ct)
                ?? new KantnodeVisning(b.TilVirksomhetId is null ? "begrep" : "virksomhet", b.TilVirksomhetId ?? b.TilBegrepId!.Value, "(ukjent)", null);
            ut.Add(new NavneformBindingVisning(b.Id, b.NavneformId, x.Term, b.RettskildeId, x.Tittel, LesSpenn(b.ParagrafspennJson), referent,
                b.HjemmelRettskildeId, b.HjemmelEid, b.KildeUtenforKorpusTekst, b.KildeUtenforKorpusLenke, b.KildeUtenforKorpusType,
                b.KildeUtenforKorpusDokumentasjon, b.Kunnskapsstatus, b.Begrunnelse, b.Tolkningsargumenter, b.Status, b.OppdagelsesKilde,
                b.Kommentar, b.AvvisningBegrunnelse, b.OpprettetAv, b.OpprettetTidspunkt, b.SistEndretAv, b.SistEndretTidspunkt,
                b.GyldigFra, b.GyldigTil));
        }
        return ut;
    }

    /// <summary>
    /// AC7: «hvor brukes ‹styret›, og hva betyr det i hver rettskilde?» — for hver generisk navneform (eller én, med
    /// <paramref name="term"/>): rettskildene der den er tagget eller bundet, antall tagger og bindingene der.
    /// </summary>
    public async Task<List<GeneriskNavneformOversikt>> OversiktAsync(string? term = null, CancellationToken ct = default)
    {
        var t = term is null ? null : NormaliserTerm(term);
        var navneformer = await db.Begreper
            .Where(b => b.Navneformgrunn == VirksomhetsbegrepTjeneste.Generisk && b.Entitetsstatus == "gjeldende" && (t == null || b.Term == t))
            .OrderBy(b => b.Term).Select(b => new { b.Id, b.Term }).ToListAsync(ct);
        var ider = navneformer.Select(n => n.Id).ToList();
        var tagger = await db.TekstTagger
            .Where(x => x.Kind == "virksomhet" && x.Entitetsstatus == "gjeldende" && x.RefId != null && ider.Contains(x.RefId.Value))
            .GroupBy(x => new { NavneformId = x.RefId!.Value, x.RettskildeId })
            .Select(g => new { g.Key.NavneformId, g.Key.RettskildeId, Antall = g.Count() }).ToListAsync(ct);
        var bindinger = await HentBindingerAsync(ct: ct);
        var rettskildeIder = tagger.Select(x => x.RettskildeId).Concat(bindinger.Select(b => b.RettskildeId)).Distinct().ToList();
        var titler = await db.Rettskilder.Where(r => rettskildeIder.Contains(r.Id))
            .Select(r => new { r.Id, Tittel = r.Kortnavn ?? r.Tittel }).ToDictionaryAsync(r => r.Id, r => r.Tittel, ct);
        return navneformer.Select(n =>
        {
            var perRettskilde = tagger.Where(x => x.NavneformId == n.Id).Select(x => x.RettskildeId)
                .Concat(bindinger.Where(b => b.NavneformId == n.Id).Select(b => b.RettskildeId)).Distinct()
                .Select(r => new GeneriskBrukIRettskilde(r, titler.GetValueOrDefault(r) ?? "(ukjent)",
                    tagger.Where(x => x.NavneformId == n.Id && x.RettskildeId == r).Sum(x => x.Antall),
                    bindinger.Where(b => b.NavneformId == n.Id && b.RettskildeId == r).ToList()))
                .OrderBy(r => r.RettskildeTittel, StringComparer.Ordinal).ToList();
            return new GeneriskNavneformOversikt(n.Id, n.Term, perRettskilde.Sum(r => r.AntallTagger), perRettskilde);
        }).ToList();
    }
}
