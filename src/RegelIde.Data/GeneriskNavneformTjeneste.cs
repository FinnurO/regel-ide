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
    /// <summary>[Ny, issue #384] En godkjent binding med paragrafspenn ble registrert mot en annen versjon av verket, og spennet dekker ikke
    /// de samme nodene i denne versjonen (en eId er borte, eller nodene i spennet er andre). Bindingen kan ikke flyttes entydig, og hva
    /// den sa om de nodene, gjettes ikke. [ENDRET, Johanns beslutning på PR #388 2026-10-16] Den blokkerer bare seg selv: de andre gyldige
    /// bindingene for navneformen brukes som før, og statusen gis bare for en node ingen av dem dekker.
    /// <see cref="GeneriskOppslag.Binding"/> er bindingen som ikke kunne flyttes.</summary>
    public const string SpennEndret = "spenn_endret";
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
    DateOnly? GyldigFra, DateOnly? GyldigTil)
{
    /// <summary>[Ny, issue #384] Verket bindingen gjelder: ELI-en til rettskilden (null for en lokal kilde uten ELI — da er verket den ene
    /// rettskilden). Bindingen følger verket over nye versjoner.</summary>
    public string? VerkEli { get; init; }
    /// <summary>[Ny, issue #384] Versjonsnummeret til rettskilden bindingen ble registrert (og vurdert) mot — <see cref="RettskildeId"/>.</summary>
    public int RegistrertVersjon { get; init; }
    /// <summary>[Ny, issue #384] Den gjeldende versjonen av verket (lik <see cref="RettskildeId"/> når bindingen er registrert mot den).</summary>
    public Guid GjeldendeRettskildeId { get; init; }
}

/// <summary>[Ny, issue #384] En binding som ikke kan flyttes entydig til den gjeldende versjonen av verket, og hvorfor.</summary>
public sealed record IkkeFlyttbarBinding(NavneformBindingVisning Binding, Guid GjeldendeRettskildeId, int GjeldendeVersjon, string Grunn);

/// <summary>[Ny, issue #384] Verket en rettskilde-versjon hører til: (virksomhet, ELI) — samme par som importen bruker for å finne forrige
/// versjon (<see cref="RettskildeImportTjeneste"/>) og som de unike indeksene <c>ux_rettskilder_eli_gjeldende_*</c> håndhever. Uten ELI er
/// verket rettskilden selv.</summary>
public sealed record Rettskildeverk(Guid RettskildeId, Guid? VirksomhetId, string? Eli, int Versjon);

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
/// <para>
/// <b>[ENDRET, issue #384, Johanns beslutning 2026-10-16] Bindingen følger verket, ikke versjonen.</b> En ny konsolidert versjon er en ny
/// rettskilde-rad med ny id (<see cref="RettskildeImportTjeneste"/>, §2.1), og før #384 falt bindingen da bort. Nå er verket
/// (<see cref="Rettskildeverk"/>: virksomhet + ELI, det paret importen alt bruker for å finne forrige versjon) identiteten bindingen gjelder
/// for, og <see cref="NavneformBindingEntitet.RettskildeId"/> er versjonen den ble registrert og vurdert mot. Ingen ny kolonne: ELI-en står
/// på hver versjon, og er målt lik over alle 1 113 versjonsoverganger i den lokale basen (2026-10-16). En lokal kilde uten ELI har ikke
/// noe verk å følge; der er verket rettskilden selv (ingen slike er versjonert, målt samme dag).
/// </para>
/// <para>
/// <b>Ingen gjetting ved flytting:</b> en binding for hele rettskilden følger verket uten videre. En binding med paragrafspenn følger
/// verket bare når spennet dekker nøyaktig de samme eId-ene i den nye versjonen. Ellers brukes den ikke i den versjonen, og den listes av
/// <see cref="IkkeFlyttbareAsync"/>. [ENDRET, Johanns beslutning på PR #388] Den blokkerer bare seg selv: de andre gyldige bindingene brukes,
/// og <see cref="GeneriskOppslagstatus.SpennEndret"/> gis bare for en node ingen av dem dekker.
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
        // [ENDRET, issue #384] Virkeområdet er VERKET: en binding registrert mot forrige versjon gjelder også her.
        var verk = await VerkAsync(ny.RettskildeId, ct);
        var konflikt = await IVerket(verk).FirstOrDefaultAsync(b => b.NavneformId == ny.NavneformId
            && b.ParagrafspennJson == spennJson && b.Entitetsstatus == "gjeldende" && b.Status != Strukturkanter.Avvist
            && (b.GyldigFra == null || b.GyldigFra <= nyTil) && (b.GyldigTil == null || b.GyldigTil >= nyFra), ct);
        if (konflikt is not null)
        {
            throw new ArgumentException(
                $"«{navneform.Term}» er alt bundet med samme virkeområde og overlappende gyldighet i denne rettskilden, eller en annen versjon av den (binding {konflikt.Id}, "
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

    /// <summary>[Ny, issue #384] Nodene (eId-er) spennet dekker i treet, eller null når et endepunkt ikke finnes der. Samme dekning som
    /// <see cref="Dekning"/>; brukes for å avgjøre om et spenn betyr det samme i to versjoner.</summary>
    private static HashSet<string>? Dekningssett(Nodetre tre, IReadOnlyList<ParagrafspennPar> spenn)
    {
        var sett = new HashSet<string>(StringComparer.Ordinal);
        var eidPerId = tre.PerEid.ToDictionary(x => x.Value.Id, x => x.Key);
        foreach (var par in spenn)
        {
            if (!tre.PerEid.TryGetValue(par.FraEid, out var fra)) return null;
            var til = fra;
            if (par.TilEid is not null && !tre.PerEid.TryGetValue(par.TilEid, out til)) return null;
            var (lo, hi) = (Math.Min(fra.Sortering, til.Sortering), Math.Max(fra.Sortering, til.Sortering));
            var røtter = tre.PerEid.Values.Where(v => v.Sortering >= lo && v.Sortering <= hi).Select(v => v.Id).ToHashSet();
            foreach (var id in eidPerId.Keys)
            {
                for (Guid? cur = id; cur is { } c; cur = tre.Forelder.GetValueOrDefault(c))
                {
                    if (røtter.Contains(c)) { sett.Add(eidPerId[id]); break; }
                }
            }
        }
        return sett;
    }

    /// <summary>[Ny, issue #384] Verket rettskilde-versjonen hører til (se <see cref="Rettskildeverk"/>).</summary>
    public async Task<Rettskildeverk> VerkAsync(Guid rettskildeId, CancellationToken ct = default) =>
        await db.Rettskilder.Where(r => r.Id == rettskildeId).Select(r => new Rettskildeverk(r.Id, r.VirksomhetId, r.Eli, r.Versjon))
            .FirstOrDefaultAsync(ct)
        ?? throw new ArgumentException($"Fant ingen rettskilde med id '{rettskildeId}'. Ingen gjettet fallback.");

    /// <summary>[Ny, issue #384] Bindingene som gjelder verket: registrert mot en hvilken som helst versjon med samme (virksomhet, ELI), eller
    /// mot rettskilden selv når den ikke har ELI.</summary>
    private IQueryable<NavneformBindingEntitet> IVerket(Rettskildeverk verk) => verk.Eli is null
        ? db.NavneformBindinger.Where(b => b.RettskildeId == verk.RettskildeId)
        : db.NavneformBindinger.Where(b => db.Rettskilder.Any(r => r.Id == b.RettskildeId && r.Eli == verk.Eli && r.VirksomhetId == verk.VirksomhetId));

    /// <summary>[Ny, issue #384] Alle versjonene av verket (id-er), eldste først — for oppslag som må se tagger i alle versjoner.</summary>
    public async Task<List<Guid>> VersjonerAvVerketAsync(Guid rettskildeId, CancellationToken ct = default)
    {
        var verk = await VerkAsync(rettskildeId, ct);
        if (verk.Eli is null) return [rettskildeId];
        return await db.Rettskilder.Where(r => r.Eli == verk.Eli && r.VirksomhetId == verk.VirksomhetId)
            .OrderBy(r => r.Versjon).Select(r => r.Id).ToListAsync(ct);
    }

    /// <summary>[Ny, issue #384] Den gjeldende versjonen av verket, eller rettskilden selv når den ikke har ELI eller ikke nøyaktig én
    /// versjon er gjeldende.</summary>
    private async Task<(Guid Id, int Versjon)> GjeldendeVersjonAsync(Rettskildeverk verk, CancellationToken ct)
    {
        if (verk.Eli is null) return (verk.RettskildeId, verk.Versjon);
        var g = await db.Rettskilder.Where(r => r.Eli == verk.Eli && r.VirksomhetId == verk.VirksomhetId && r.Entitetsstatus == "gjeldende")
            .Select(r => new { r.Id, r.Versjon }).ToListAsync(ct);
        return g.Count == 1 ? (g[0].Id, g[0].Versjon) : (verk.RettskildeId, verk.Versjon);
    }

    /// <summary>
    /// [Ny, issue #384] Null når bindingen kan brukes i versjonen <paramref name="iVersjon"/>; ellers hvorfor ikke. En binding uten spenn
    /// gjelder hele verket. Med spenn må spennet dekke nøyaktig de samme eId-ene i versjonen den ble registrert mot og i denne.
    /// </summary>
    private async Task<string?> IkkeFlyttbarGrunnAsync(NavneformBindingEntitet b, Guid iVersjon, CancellationToken ct)
    {
        if (b.RettskildeId == iVersjon) return null;
        var spenn = LesSpenn(b.ParagrafspennJson);
        if (spenn.Count == 0) return null;
        var registrert = Dekningssett(await NodetreAsync(b.RettskildeId, ct), spenn);
        var her = Dekningssett(await NodetreAsync(iVersjon, ct), spenn);
        if (her is null)
        {
            return "Et endepunkt i paragrafspennet (" + string.Join(", ", spenn.Select(p => p.TilEid is null ? p.FraEid : $"{p.FraEid}–{p.TilEid}"))
                + ") finnes ikke i denne versjonen.";
        }
        if (registrert is null || !registrert.SetEquals(her))
        {
            return $"Paragrafspennet dekker andre noder i denne versjonen ({her.Count} noder) enn i versjonen bindingen ble registrert mot "
                + $"({registrert?.Count.ToString() ?? "ukjent antall"} noder).";
        }
        return null;
    }

    /// <summary>
    /// Slår opp hva generisk navneform <paramref name="navneformId"/> betyr i noden <paramref name="eid"/> i <paramref name="rettskildeId"/>
    /// (B6). Bare GODKJENTE bindinger teller; det smaleste spennet som dekker noden vinner. Like smale med ulik referent → flertydig.
    /// [ENDRET, issue #378, Johanns beslutning 2026-10-16] Bare bindinger som gjelder på <paramref name="dato"/> (standard: i dag) teller —
    /// også i tellingen av foreslåtte.
    /// [ENDRET, issue #384] Bindingene for VERKET teller, uansett hvilken versjon de ble registrert mot. En godkjent binding som ikke kan
    /// flyttes entydig til denne versjonen, brukes ikke her. [ENDRET, Johanns beslutning på PR #388 2026-10-16] Den blokkerer bare seg selv:
    /// de andre gyldige bindingene oppløser som før (smaleste spenn vinner). Dekker ingen av dem noden, er svaret
    /// <see cref="GeneriskOppslagstatus.SpennEndret"/> med den uflyttbare bindingen — ikke <c>ubundet</c>, for da ville det sett ut som om
    /// ingen hadde bundet navneformen her. Før rettingen blokkerte én slik binding navneformen i hele versjonen.
    /// </summary>
    public async Task<GeneriskOppslag> LosOppAsync(
        Guid navneformId, Guid rettskildeId, string? eid, CancellationToken ct = default, DateOnly? dato = null)
    {
        var d = dato ?? DateOnly.FromDateTime(DateTime.Today);
        var verk = await VerkAsync(rettskildeId, ct);
        var bindinger = await IVerket(verk)
            .Where(b => b.NavneformId == navneformId && b.Entitetsstatus == "gjeldende"
                        && b.Status != Strukturkanter.Avvist
                        && (b.GyldigFra == null || b.GyldigFra <= d) && (b.GyldigTil == null || b.GyldigTil >= d))
            .ToListAsync(ct);
        var foreslatte = bindinger.Count(b => b.Status != Strukturkanter.Godkjent);
        var godkjente = bindinger.Where(b => b.Status == Strukturkanter.Godkjent).ToList();
        if (godkjente.Count == 0) return new GeneriskOppslag(GeneriskOppslagstatus.Ubundet, null, null, foreslatte);
        var gyldige = new List<NavneformBindingEntitet>();
        NavneformBindingEntitet? ikkeFlyttet = null;
        foreach (var b in godkjente.OrderBy(b => b.Id))
        {
            if (await IkkeFlyttbarGrunnAsync(b, rettskildeId, ct) is null) gyldige.Add(b);
            else ikkeFlyttet ??= b;
        }
        var tre = await NodetreAsync(rettskildeId, ct);
        var dekkende = gyldige
            .Select(b => (B: b, Bredde: Dekning(tre, LesSpenn(b.ParagrafspennJson), eid ?? "")))
            .Where(x => x.Bredde is not null).ToList();
        if (dekkende.Count == 0)
        {
            return ikkeFlyttet is not null
                ? new GeneriskOppslag(GeneriskOppslagstatus.SpennEndret, ikkeFlyttet, null, foreslatte)
                : new GeneriskOppslag(GeneriskOppslagstatus.Ubundet, null, null, foreslatte);
        }
        var smalest = dekkende.Min(x => x.Bredde!.Value);
        var vinnere = dekkende.Where(x => x.Bredde == smalest).ToList();
        var referenter = vinnere.Select(x => (x.B.TilVirksomhetId, x.B.TilBegrepId)).Distinct().ToList();
        if (referenter.Count > 1) return new GeneriskOppslag(GeneriskOppslagstatus.Flertydig, null, null, foreslatte);
        var vinner = vinnere[0].B;
        return new GeneriskOppslag(GeneriskOppslagstatus.Bundet, vinner, new Kantnode(vinner.TilVirksomhetId, vinner.TilBegrepId), foreslatte);
    }

    /// <summary>
    /// [Ny, issue #384, AC «bindinger som ikke kan flyttes entydig, listes»] De gjeldende, ikke avviste bindingene som er registrert mot en
    /// annen versjon enn den gjeldende av verket, og som IKKE kan flyttes entydig dit (<see cref="IkkeFlyttbarGrunnAsync"/>). Det som står
    /// her, må registreres på nytt mot den gjeldende versjonen av et menneske (avvis den gamle, opprett en ny).
    /// </summary>
    public async Task<List<IkkeFlyttbarBinding>> IkkeFlyttbareAsync(CancellationToken ct = default)
    {
        var kandidater = await db.NavneformBindinger
            .Where(b => b.Entitetsstatus == "gjeldende" && b.Status != Strukturkanter.Avvist && b.ParagrafspennJson != "[]")
            .ToListAsync(ct);
        var ut = new List<IkkeFlyttbarBinding>();
        foreach (var b in kandidater)
        {
            var (gjeldende, versjon) = await GjeldendeVersjonAsync(await VerkAsync(b.RettskildeId, ct), ct);
            if (gjeldende == b.RettskildeId) continue;
            if (await IkkeFlyttbarGrunnAsync(b, gjeldende, ct) is not { } grunn) continue;
            var visning = (await HentBindingerAsync(ider: [b.Id], visAvviste: true, ct: ct)).Single();
            ut.Add(new IkkeFlyttbarBinding(visning, gjeldende, versjon, grunn));
        }
        return ut;
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

    /// <summary>Bindingene, med navn — for én navneform, én rettskilde, eller alle. Avviste bare med <paramref name="visAvviste"/>.
    /// [ENDRET, issue #384] <paramref name="rettskildeId"/> betyr VERKET den versjonen hører til: bindinger registrert mot en tidligere versjon
    /// er med. <paramref name="ider"/> avgrenser til bestemte bindinger.</summary>
    public async Task<List<NavneformBindingVisning>> HentBindingerAsync(
        Guid? navneformId = null, Guid? rettskildeId = null, bool visAvviste = false, CancellationToken ct = default,
        IReadOnlyCollection<Guid>? ider = null)
    {
        var kilde = rettskildeId is { } rid ? IVerket(await VerkAsync(rid, ct)) : db.NavneformBindinger;
        if (ider is not null) kilde = kilde.Where(b => ider.Contains(b.Id));
        var rader = await kilde
            .Where(b => b.Entitetsstatus == "gjeldende" && (visAvviste || b.Status != Strukturkanter.Avvist)
                        && (navneformId == null || b.NavneformId == navneformId))
            .Join(db.Begreper, b => b.NavneformId, n => n.Id, (b, n) => new { B = b, n.Term })
            .Join(db.Rettskilder, x => x.B.RettskildeId, r => r.Id,
                (x, r) => new { x.B, x.Term, Tittel = r.Kortnavn ?? r.Tittel, r.Eli, r.VirksomhetId, r.Versjon })
            .OrderBy(x => x.Term).ThenBy(x => x.Tittel).ToListAsync(ct);
        var ut = new List<NavneformBindingVisning>();
        foreach (var x in rader)
        {
            var b = x.B;
            var referent = await VisningAsync(new Kantnode(b.TilVirksomhetId, b.TilBegrepId), ct)
                ?? new KantnodeVisning(b.TilVirksomhetId is null ? "begrep" : "virksomhet", b.TilVirksomhetId ?? b.TilBegrepId!.Value, "(ukjent)", null);
            var (gjeldende, _) = await GjeldendeVersjonAsync(new Rettskildeverk(b.RettskildeId, x.VirksomhetId, x.Eli, x.Versjon), ct);
            ut.Add(new NavneformBindingVisning(b.Id, b.NavneformId, x.Term, b.RettskildeId, x.Tittel, LesSpenn(b.ParagrafspennJson), referent,
                b.HjemmelRettskildeId, b.HjemmelEid, b.KildeUtenforKorpusTekst, b.KildeUtenforKorpusLenke, b.KildeUtenforKorpusType,
                b.KildeUtenforKorpusDokumentasjon, b.Kunnskapsstatus, b.Begrunnelse, b.Tolkningsargumenter, b.Status, b.OppdagelsesKilde,
                b.Kommentar, b.AvvisningBegrunnelse, b.OpprettetAv, b.OpprettetTidspunkt, b.SistEndretAv, b.SistEndretTidspunkt,
                b.GyldigFra, b.GyldigTil)
            {
                VerkEli = x.Eli,
                RegistrertVersjon = x.Versjon,
                GjeldendeRettskildeId = gjeldende,
            });
        }
        return ut;
    }

    /// <summary>
    /// AC7: «hvor brukes ‹styret›, og hva betyr det i hver rettskilde?» — for hver generisk navneform (eller én, med
    /// <paramref name="term"/>): rettskildene der den er tagget eller bundet, antall tagger og bindingene der.
    /// [ENDRET, issue #384] En binding står under den GJELDENDE versjonen av verket sitt (der taggene er flyttet ved resynk), ikke under
    /// versjonen den ble registrert mot — ellers ville samme lov stått to ganger.
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
        var rettskildeIder = tagger.Select(x => x.RettskildeId).Concat(bindinger.Select(b => b.GjeldendeRettskildeId)).Distinct().ToList();
        var titler = await db.Rettskilder.Where(r => rettskildeIder.Contains(r.Id))
            .Select(r => new { r.Id, Tittel = r.Kortnavn ?? r.Tittel }).ToDictionaryAsync(r => r.Id, r => r.Tittel, ct);
        return navneformer.Select(n =>
        {
            var perRettskilde = tagger.Where(x => x.NavneformId == n.Id).Select(x => x.RettskildeId)
                .Concat(bindinger.Where(b => b.NavneformId == n.Id).Select(b => b.GjeldendeRettskildeId)).Distinct()
                .Select(r => new GeneriskBrukIRettskilde(r, titler.GetValueOrDefault(r) ?? "(ukjent)",
                    tagger.Where(x => x.NavneformId == n.Id && x.RettskildeId == r).Sum(x => x.Antall),
                    bindinger.Where(b => b.NavneformId == n.Id && b.GjeldendeRettskildeId == r).ToList()))
                .OrderBy(r => r.RettskildeTittel, StringComparer.Ordinal).ToList();
            return new GeneriskNavneformOversikt(n.Id, n.Term, perRettskilde.Sum(r => r.AntallTagger), perRettskilde);
        }).ToList();
    }
}
