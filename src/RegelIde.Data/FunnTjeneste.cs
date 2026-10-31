using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #429] En kobling slik den sendes inn (fra skjemaet, en agent eller seed-fila). Nøyaktig ett mål.
/// [ENDRET, #429 PR 2] <see cref="QuoteExact"/>: ordet slik det står i noden, for bestemmelser maskinregelen fant.</summary>
public sealed record NyKobling(
    string Rolle, string Etikett, string? Merknad = null, string? RettskildeEli = null, string? Eid = null,
    Guid? VirksomhetId = null, Guid? BegrepId = null, Guid? StrukturkantId = null, Guid? KildefeilId = null, string? EksternLenke = null,
    string? QuoteExact = null);

/// <summary>[Ny, issue #429] Ett vurderingspunkt slik det sendes inn.</summary>
public sealed record NyttVurderingspunkt(string Type, string Tekst, string? Overskrift = null, string? Sikkerhet = null);

/// <summary>[Ny, issue #429] Et nytt funn. Får alltid status <c>oppdaget</c>.</summary>
public sealed record NyttFunn(
    string Kategori, string? Undertype, string Tittel, string Beskrivelse, string OppdagelsesKilde,
    string? InternMerknad = null, string? FunnetI = null, string? EksternNokkel = null,
    IReadOnlyList<NyKobling>? Koblinger = null, IReadOnlyList<NyttVurderingspunkt>? Vurderingspunkter = null);

/// <summary>[Ny, issue #429] En redigering. <c>null</c> i <see cref="Koblinger"/>/<see cref="Vurderingspunkter"/> = uendret; en liste
/// erstatter hele settet.</summary>
public sealed record FunnEndring(
    string Tittel, string Beskrivelse, string? Undertype, string? Vurdering, string? InternMerknad,
    IReadOnlyList<NyKobling>? Koblinger = null, IReadOnlyList<NyttVurderingspunkt>? Vurderingspunkter = null);

/// <summary>[Ny, issue #429] Det eksporten får: øyeblikksbildene som er godkjent og har riktig hash, og nøklene til dem som ble holdt
/// tilbake fordi hashen ikke stemte (skal aldri skje; da er raden endret utenom tjenesten).</summary>
public sealed record PubliserbareFunn(IReadOnlyList<JsonElement> Funn, IReadOnlyList<string> HoldtTilbake);

/// <summary>
/// [Ny, issue #429 «Funn til vurdering», Johanns avgjørelser 2026-10-29/30] Registeret: opprett, rediger, juristvurder, forklar bort,
/// godkjenn for publisering, trekk tilbake, og eksporten til nettsiden. Se <see cref="Funn"/> for statusflyten.
/// <para>
/// <b>Tilgang:</b> tjenesten sjekker IKKE hvem som kaller. Hvem som kan godkjenne for publisering, avgjøres i API-et mot
/// <see cref="Funn.KonfignokkelPubliseringsansvarlige"/>. Seed-fila (<see cref="FunnSeed"/>) kaller tjenesten direkte, fordi den
/// fører inn en avgjørelse som alt er tatt og står i #429.
/// </para>
/// <para>
/// <b>Ulovlige overganger</b> kaster <see cref="InvalidOperationException"/> (API: 409); ugyldige verdier kaster
/// <see cref="ArgumentException"/> (API: 400). Et funn som ikke finnes, gir <c>null</c>.
/// </para>
/// </summary>
public sealed class FunnTjeneste(RegelIdeDbContext db)
{
    private static readonly JsonSerializerOptions JsonUt = new(JsonSerialiseringHjelper.Innstillinger) { WriteIndented = false };

    public Task<FunnEntitet?> HentAsync(Guid id, CancellationToken ct = default) =>
        db.Funn.Include(f => f.Koblinger).Include(f => f.Vurderingspunkter).FirstOrDefaultAsync(f => f.Id == id, ct);

    /// <summary>Lista, med valgfrie filtre. <paramref name="rettskildeId"/> treffer også andre versjoner av samme verk via ELI-en, slik at
    /// et funn ikke forsvinner fra rettskildesiden når Lovdata-resynken lager en ny versjon.</summary>
    public async Task<List<FunnEntitet>> ListeAsync(
        string? kategori = null, string? status = null, Guid? rettskildeId = null, Guid? virksomhetId = null, CancellationToken ct = default)
    {
        var q = db.Funn.Include(f => f.Koblinger).Include(f => f.Vurderingspunkter).AsQueryable();
        if (!string.IsNullOrWhiteSpace(kategori)) q = q.Where(f => f.Kategori == kategori);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(f => f.Status == status);
        if (rettskildeId is { } rid)
        {
            var eli = await db.Rettskilder.Where(r => r.Id == rid).Select(r => r.Eli).FirstOrDefaultAsync(ct);
            q = q.Where(f => f.Koblinger.Any(k => k.RettskildeId == rid || (eli != null && k.RettskildeEli == eli)));
        }
        if (virksomhetId is { } vid) q = q.Where(f => f.Koblinger.Any(k => k.VirksomhetId == vid));
        return await q.OrderByDescending(f => f.OpprettetTidspunkt).ToListAsync(ct);
    }

    /// <summary>Oppretter et funn med status <c>oppdaget</c>. Idempotent på <see cref="NyttFunn.EksternNokkel"/>: finnes nøkkelen, returneres
    /// den eksisterende raden uendret (<c>Ny = false</c>) — en ny kjøring av en agent eller seed overskriver aldri en vurdering.</summary>
    public async Task<(FunnEntitet Funn, bool Ny)> OpprettAsync(NyttFunn n, string av, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(n.EksternNokkel))
        {
            var finnes = await db.Funn.Include(f => f.Koblinger).Include(f => f.Vurderingspunkter)
                .FirstOrDefaultAsync(f => f.EksternNokkel == n.EksternNokkel, ct);
            if (finnes is not null) return (finnes, false);
        }
        ValiderKategori(n.Kategori, n.Undertype);
        var tittel = Pakrevd(n.Tittel, "Tittel");
        var beskrivelse = Pakrevd(n.Beskrivelse, "Beskrivelse");
        var kilde = Pakrevd(n.OppdagelsesKilde, "Oppdagelseskilde");

        var funn = new FunnEntitet
        {
            Id = Guid.NewGuid(),
            EksternNokkel = string.IsNullOrWhiteSpace(n.EksternNokkel) ? null : n.EksternNokkel.Trim(),
            Kategori = n.Kategori,
            Undertype = string.IsNullOrWhiteSpace(n.Undertype) ? null : n.Undertype,
            Tittel = tittel,
            Beskrivelse = beskrivelse,
            InternMerknad = Tom(n.InternMerknad),
            OppdagelsesKilde = kilde,
            FunnetI = Tom(n.FunnetI),
            Status = Funn.Oppdaget,
            OpprettetAv = av,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        funn.Koblinger = await ByggKoblingerAsync(funn.Id, n.Koblinger ?? [], ct);
        funn.Vurderingspunkter = ByggVurderingspunkter(funn.Id, n.Vurderingspunkter ?? []);
        db.Funn.Add(funn);
        db.Proveniens.Add(ProveniensHjelper.NyRad(Funn.ProveniensType, funn.Id, virksomhetId: null, "opprettet", av));
        await db.SaveChangesAsync(ct);
        return (funn, true);
    }

    /// <summary>
    /// Redigerer et funn. [LÅST, #429 AK3] Er funnet godkjent for publisering og det OFFENTLIGE innholdet endres (alt unntatt den interne
    /// merknaden), går det tilbake til <c>juristvurdert</c> og øyeblikksbildet nulles: det som står på nettsiden, er alltid det Johann
    /// godkjente. En endring i bare den interne merknaden beholder godkjenningen.
    /// </summary>
    public async Task<FunnEntitet?> OppdaterAsync(Guid id, FunnEndring e, string av, CancellationToken ct = default)
    {
        var funn = await HentAsync(id, ct);
        if (funn is null) return null;
        ValiderKategori(funn.Kategori, e.Undertype);
        funn.Tittel = Pakrevd(e.Tittel, "Tittel");
        funn.Beskrivelse = Pakrevd(e.Beskrivelse, "Beskrivelse");
        funn.Undertype = string.IsNullOrWhiteSpace(e.Undertype) ? null : e.Undertype;
        funn.InternMerknad = Tom(e.InternMerknad);
        // En juristvurdert rad kan ikke miste vurderingen sin ved redigering (CHECK ck_funn_juristvurdert_hvem).
        funn.Vurdering = Tom(e.Vurdering) ?? (funn.Status is Funn.Oppdaget or Funn.ForklartBort ? null : funn.Vurdering);
        if (e.Koblinger is not null)
        {
            db.FunnKoblinger.RemoveRange(funn.Koblinger);
            funn.Koblinger = await ByggKoblingerAsync(funn.Id, e.Koblinger, ct);
            db.FunnKoblinger.AddRange(funn.Koblinger);
        }
        if (e.Vurderingspunkter is not null)
        {
            db.FunnVurderingspunkter.RemoveRange(funn.Vurderingspunkter);
            funn.Vurderingspunkter = ByggVurderingspunkter(funn.Id, e.Vurderingspunkter);
            db.FunnVurderingspunkter.AddRange(funn.Vurderingspunkter);
        }
        funn.SistEndretAv = av;
        funn.SistEndretTidspunkt = DateTimeOffset.UtcNow;

        if (funn.Status == Funn.GodkjentForPublisering)
        {
            var dato = DateOnly.FromDateTime(funn.GodkjentTidspunkt!.Value.UtcDateTime);
            if (Serialiser(ByggOffentligInnhold(funn, dato)) != funn.GodkjentInnholdJson)
            {
                NullstillGodkjenning(funn);
                funn.Status = Funn.Juristvurdert;
                db.Proveniens.Add(Proveniens(funn.Id, "publisering_trukket_ved_endring", av,
                    "Det offentlige innholdet er endret etter godkjenningen. Funnet er tilbake til juristvurdert og publiseres ikke før det godkjennes på nytt."));
            }
        }
        db.Proveniens.Add(ProveniensHjelper.NyRad(Funn.ProveniensType, funn.Id, virksomhetId: null, "endret", av));
        await db.SaveChangesAsync(ct);
        return funn;
    }

    /// <summary>
    /// Oppdaget (eller juristvurdert, for en ny vurdering) → juristvurdert. Krever vurderingen og minst én prøvd bortforklaring: Johanns
    /// terskel er at juristen først prøver å forklare funnet bort, og at hvert publisert funn viser hva som er prøvd (#429).
    /// Gitte <paramref name="punkter"/> erstatter de eksisterende. Nuller «kilde endret».
    /// </summary>
    public async Task<FunnEntitet?> JuristvurderAsync(
        Guid id, string vurdering, string vurdertAvType, string av, IReadOnlyList<NyttVurderingspunkt>? punkter = null,
        DateTimeOffset? tidspunkt = null, CancellationToken ct = default)
    {
        var funn = await HentAsync(id, ct);
        if (funn is null) return null;
        if (funn.Status is not (Funn.Oppdaget or Funn.Juristvurdert))
        {
            throw new InvalidOperationException(funn.Status == Funn.GodkjentForPublisering
                ? "Funnet er godkjent for publisering. Trekk det tilbake før det vurderes på nytt."
                : "Funnet er forklart bort og kan ikke juristvurderes.");
        }
        if (!Funn.VurdertAvTyper.Contains(vurdertAvType))
        {
            throw new ArgumentException($"Ukjent vurdert-av-type «{vurdertAvType}». Gyldige: {string.Join(", ", Funn.VurdertAvTyper)}.");
        }
        var tekst = Pakrevd(vurdering, "Vurdering");
        if (punkter is not null)
        {
            db.FunnVurderingspunkter.RemoveRange(funn.Vurderingspunkter);
            funn.Vurderingspunkter = ByggVurderingspunkter(funn.Id, punkter);
            db.FunnVurderingspunkter.AddRange(funn.Vurderingspunkter);
        }
        if (!funn.Vurderingspunkter.Any(p => p.Type == Funn.BortforklaringProvd))
        {
            throw new ArgumentException("En juristvurdering må vise minst én bortforklaring som er prøvd (#429).");
        }
        var naa = tidspunkt ?? DateTimeOffset.UtcNow;
        funn.Vurdering = tekst;
        funn.VurdertAvType = vurdertAvType;
        funn.JuristvurdertAv = av;
        funn.JuristvurdertTidspunkt = naa;
        funn.KildeEndretTidspunkt = null;
        funn.Status = Funn.Juristvurdert;
        funn.SistEndretAv = av;
        funn.SistEndretTidspunkt = naa;
        db.Proveniens.Add(ProveniensHjelper.NyRad(Funn.ProveniensType, funn.Id, virksomhetId: null, "juristvurdert", av));
        await db.SaveChangesAsync(ct);
        return funn;
    }

    /// <summary>Oppdaget/juristvurdert → forklart bort, med begrunnelse. Raden slettes ikke; den publiseres ikke (Johann 2026-10-30).</summary>
    public async Task<FunnEntitet?> ForklarBortAsync(
        Guid id, string begrunnelse, string av, DateTimeOffset? tidspunkt = null, CancellationToken ct = default)
    {
        var funn = await HentAsync(id, ct);
        if (funn is null) return null;
        if (funn.Status is not (Funn.Oppdaget or Funn.Juristvurdert))
        {
            throw new InvalidOperationException(funn.Status == Funn.GodkjentForPublisering
                ? "Funnet er godkjent for publisering. Trekk det tilbake før det forklares bort."
                : "Funnet er alt forklart bort.");
        }
        var naa = tidspunkt ?? DateTimeOffset.UtcNow;
        funn.ForklartBortBegrunnelse = Pakrevd(begrunnelse, "Begrunnelse");
        funn.ForklartBortAv = av;
        funn.ForklartBortTidspunkt = naa;
        funn.Status = Funn.ForklartBort;
        funn.SistEndretAv = av;
        funn.SistEndretTidspunkt = naa;
        db.Proveniens.Add(ProveniensHjelper.NyRad(Funn.ProveniensType, funn.Id, virksomhetId: null, "forklart_bort", av));
        await db.SaveChangesAsync(ct);
        return funn;
    }

    /// <summary>
    /// Juristvurdert → godkjent for publisering, og øyeblikksbildet fryses. Premissporten (<see cref="PremissmanglerAsync"/>) må være
    /// tom. Tilgangen sjekkes av kalleren (API-et).
    /// </summary>
    public async Task<FunnEntitet?> GodkjennForPubliseringAsync(
        Guid id, string av, DateTimeOffset? tidspunkt = null, CancellationToken ct = default)
    {
        var funn = await HentAsync(id, ct);
        if (funn is null) return null;
        if (funn.Status != Funn.Juristvurdert)
        {
            throw new InvalidOperationException(funn.Status switch
            {
                Funn.Oppdaget => "Funnet er ikke juristvurdert. Bare juristvurderte funn kan godkjennes for publisering.",
                Funn.GodkjentForPublisering => "Funnet er alt godkjent for publisering.",
                _ => "Funnet er forklart bort og publiseres ikke.",
            });
        }
        var mangler = await PremissmanglerAsync(funn, ct);
        if (mangler.Count > 0)
        {
            throw new InvalidOperationException("Premissporten stopper godkjenningen: " + string.Join(" ", mangler));
        }
        var naa = tidspunkt ?? DateTimeOffset.UtcNow;
        var json = Serialiser(ByggOffentligInnhold(funn, DateOnly.FromDateTime(naa.UtcDateTime)));
        funn.GodkjentInnholdJson = json;
        funn.GodkjentInnholdHash = Hash(json);
        funn.GodkjentAv = av;
        funn.GodkjentTidspunkt = naa;
        funn.Status = Funn.GodkjentForPublisering;
        funn.SistEndretAv = av;
        funn.SistEndretTidspunkt = naa;
        db.Proveniens.Add(ProveniensHjelper.NyRad(Funn.ProveniensType, funn.Id, virksomhetId: null, "godkjent_for_publisering", av));
        await db.SaveChangesAsync(ct);
        return funn;
    }

    /// <summary>Godkjent → juristvurdert. Funnet forsvinner fra neste eksport. Tilgangen sjekkes av kalleren.</summary>
    public async Task<FunnEntitet?> TrekkTilbakeAsync(Guid id, string av, CancellationToken ct = default)
    {
        var funn = await HentAsync(id, ct);
        if (funn is null) return null;
        if (funn.Status != Funn.GodkjentForPublisering)
        {
            throw new InvalidOperationException("Bare et funn som er godkjent for publisering, kan trekkes tilbake.");
        }
        NullstillGodkjenning(funn);
        funn.Status = Funn.Juristvurdert;
        funn.SistEndretAv = av;
        funn.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad(Funn.ProveniensType, funn.Id, virksomhetId: null, "publisering_trukket_tilbake", av));
        await db.SaveChangesAsync(ct);
        return funn;
    }

    /// <summary>
    /// [Ny, Johanns avgjørelse 2026-10-30, #429 anbefaling 5] <b>Premissporten.</b> Et funn om en utgått aktør kan bare godkjennes for
    /// publisering når navneformen og etterfølgerkanten er faglig vurdert (godkjenning <c>faglig_vurdert</c> eller <c>godkjent</c>).
    /// Hver mangel er én setning; tom liste = porten er åpen.
    /// <list type="bullet">
    /// <item>Funnet må peke på minst én navneform, og hver navneform må være faglig vurdert. En navneform uten godkjenning (NULL, registrert
    /// før #385) regnes ikke som vurdert — docs/33 §4.6: et premiss som ikke er vurdert, er ikke sterkere enn «uavklart».</item>
    /// <item>Hver etterfølgerkant funnet peker på, må være faglig vurdert. Ellers: «etterfølger ikke vurdert».</item>
    /// <item>[ENDRET, #429 PR 2, Johanns avgjørelse 2026-10-31 (3): «premissporten for etterfølger gjelder alle fire gruppene, også
    /// navneendring»] Et funn UTEN etterfølgerkant slapp før gjennom og ble publisert med «etterfølger ukjent». Juristmålingen viste at det
    /// er nettopp der hintet kan være galt: i gruppe A (aktøren lever under nytt navn) ser det ut som om oppgaven ligger hos aktøren i dag,
    /// men den kan være flyttet til et annet departement (ekom: KDD → DFD). Nå må juristen si det uttrykkelig: et vurderingspunkt med
    /// overskriften <see cref="Funn.EtterfolgerOverskrift"/> som sier hvem som har oppgaven nå, eller at det er ukjent. Ellers stopper
    /// porten med «Etterfølger ikke vurdert». Punktet vises som etterfølgerlinjen i forhåndsvisningen og på nettsiden.</item>
    /// </list>
    /// Andre kategorier har ingen premissport i denne runden.
    /// </summary>
    public async Task<List<string>> PremissmanglerAsync(FunnEntitet funn, CancellationToken ct = default) =>
        (await PremissmanglerForAsync([funn], ct))[funn.Id];

    /// <summary>[Ny, #429 PR 2] Premissmanglene for mange funn med to oppslag i stedet for to per kobling: lista får rundt 700 maskinfunn.
    /// Samme regel som <see cref="PremissmanglerAsync"/>.</summary>
    public async Task<Dictionary<Guid, List<string>>> PremissmanglerForAsync(IReadOnlyCollection<FunnEntitet> funn, CancellationToken ct = default)
    {
        var aktuelle = funn.Where(f => f.Kategori == Funn.UtgattAktor).ToList();
        var begrepIder = aktuelle.SelectMany(f => f.Koblinger).Where(k => k.Rolle == Funn.Navneform && k.BegrepId != null)
            .Select(k => k.BegrepId!.Value).Distinct().ToList();
        var kantIder = aktuelle.SelectMany(f => f.Koblinger).Where(k => k.Rolle == Funn.Etterfolger && k.StrukturkantId != null)
            .Select(k => k.StrukturkantId!.Value).Distinct().ToList();
        var begreper = begrepIder.Count == 0 ? new Dictionary<Guid, string?>()
            : await db.Begreper.IgnoreQueryFilters().Where(b => begrepIder.Contains(b.Id))
                .Select(b => new { b.Id, b.Godkjenning }).ToDictionaryAsync(b => b.Id, b => b.Godkjenning, ct);
        var kanter = kantIder.Count == 0 ? new Dictionary<Guid, string>()
            : await db.Strukturkanter.IgnoreQueryFilters().Where(s => kantIder.Contains(s.Id))
                .Select(s => new { s.Id, s.Status }).ToDictionaryAsync(s => s.Id, s => s.Status, ct);
        var vurdert = new[] { Strukturkanter.FagligVurdert, Strukturkanter.Godkjent };

        var ut = new Dictionary<Guid, List<string>>();
        foreach (var f in funn)
        {
            var mangler = new List<string>();
            ut[f.Id] = mangler;
            if (f.Kategori != Funn.UtgattAktor) continue;

            var navneformer = f.Koblinger.Where(k => k.Rolle == Funn.Navneform).OrderBy(k => k.Sortering).ToList();
            if (navneformer.Count == 0)
            {
                mangler.Add("Funnet peker ikke på noen navneform. Et funn om en utgått aktør må vise navnet teksten bruker.");
            }
            foreach (var k in navneformer)
            {
                if (k.BegrepId is not { } bid || !begreper.TryGetValue(bid, out var godkjenning))
                {
                    mangler.Add($"Navneformen «{k.Etikett}» finnes ikke lenger.");
                }
                else if (godkjenning is not { } g || !vurdert.Contains(g))
                {
                    mangler.Add($"Navneformen «{k.Etikett}» er ikke faglig vurdert ({GodkjenningTekst(godkjenning)}).");
                }
            }
            var etterfolgere = f.Koblinger.Where(k => k.Rolle == Funn.Etterfolger).OrderBy(k => k.Sortering).ToList();
            foreach (var k in etterfolgere)
            {
                string? status = k.StrukturkantId is { } kid && kanter.TryGetValue(kid, out var s) ? s : null;
                if (status is null || !vurdert.Contains(status))
                {
                    mangler.Add($"Etterfølger ikke vurdert: «{k.Etikett}» ({(status is null ? "kanten finnes ikke lenger" : GodkjenningTekst(status))}).");
                }
            }
            if (etterfolgere.Count == 0 && EtterfolgerVurdering(f) is null)
            {
                mangler.Add("Etterfølger ikke vurdert: funnet peker ikke på noen etterfølgerkant, og vurderingen har ikke noe punkt "
                    + $"«{Funn.EtterfolgerOverskrift}» som sier hvem som har oppgaven nå, eller at det er ukjent.");
            }
        }
        return ut;
    }

    /// <summary>[Ny, #429 PR 2] Juristens vurdering av etterfølgeren når funnet ikke peker på noen etterfølgerkant: vurderingspunktet med
    /// overskriften <see cref="Funn.EtterfolgerOverskrift"/>. Null når det ikke finnes.</summary>
    public static FunnVurderingspunktEntitet? EtterfolgerVurdering(FunnEntitet f) => f.Vurderingspunkter
        .Where(p => p.Type == Funn.Vurdering && string.Equals(p.Overskrift?.Trim(), Funn.EtterfolgerOverskrift, StringComparison.OrdinalIgnoreCase))
        .OrderBy(p => p.Sortering).FirstOrDefault();

    /// <summary>[ENDRET, kaldsjekken 2026-10-30] Godkjenningen i klartekst i premissmanglene, ikke den rå kodeverdien.</summary>
    private static string GodkjenningTekst(string? godkjenning) => godkjenning switch
    {
        null => "ikke vurdert",
        "maskinelt_foreslatt" => "maskinelt foreslått",
        "manuelt_foreslatt" => "manuelt foreslått",
        "faglig_vurdert" => "faglig vurdert",
        "avvist" => "avvist",
        _ => godkjenning,
    };

    /// <summary>
    /// Eksporten til nettsiden: bare funn med status <c>godkjent_for_publisering</c>, og bare det frosne øyeblikksbildet. Hashen
    /// kontrolleres; et øyeblikksbilde som ikke stemmer, holdes tilbake og nevnes (ingen gjettet reparasjon).
    /// </summary>
    public async Task<PubliserbareFunn> HentPubliserbareAsync(CancellationToken ct = default)
    {
        var rader = await db.Funn.AsNoTracking().Where(f => f.Status == Funn.GodkjentForPublisering)
            .OrderBy(f => f.GodkjentTidspunkt).ThenBy(f => f.EksternNokkel)
            .Select(f => new { f.Id, f.EksternNokkel, f.GodkjentInnholdJson, f.GodkjentInnholdHash }).ToListAsync(ct);
        var ut = new List<JsonElement>();
        var holdtTilbake = new List<string>();
        foreach (var r in rader)
        {
            if (r.GodkjentInnholdJson is null || Hash(r.GodkjentInnholdJson) != r.GodkjentInnholdHash)
            {
                holdtTilbake.Add(r.EksternNokkel ?? r.Id.ToString());
                continue;
            }
            using var dok = JsonDocument.Parse(r.GodkjentInnholdJson);
            ut.Add(dok.RootElement.Clone());
        }
        return new PubliserbareFunn(ut, holdtTilbake);
    }

    /// <summary>
    /// Den offentlige representasjonen — det eneste som eksporteres. Ingen personnavn (nettside/data/README.md, «Personvern»), ingen
    /// database-id-er (de er ulike i hvert miljø): funnet identifiseres med <see cref="FunnEntitet.EksternNokkel"/>, bestemmelser med
    /// ELI og eId, aktører med etiketten slik vurderingen skriver navnet. Rekkefølgen er fast, så samme innhold gir samme hash.
    /// </summary>
    public static object ByggOffentligInnhold(FunnEntitet f, DateOnly godkjentDato)
    {
        IEnumerable<FunnKoblingEntitet> Rolle(params string[] roller) =>
            f.Koblinger.Where(k => roller.Contains(k.Rolle)).OrderBy(k => k.Sortering).ThenBy(k => k.Etikett, StringComparer.Ordinal);
        IEnumerable<FunnVurderingspunktEntitet> Punkter(string type) =>
            f.Vurderingspunkter.Where(p => p.Type == type).OrderBy(p => p.Sortering).ThenBy(p => p.Tekst, StringComparer.Ordinal);
        return new
        {
            id = f.EksternNokkel ?? f.Id.ToString(),
            kategori = f.Kategori,
            undertype = f.Undertype,
            tittel = f.Tittel,
            beskrivelse = f.Beskrivelse,
            vurdering = f.Vurdering,
            bestemmelser = Rolle(Funn.MotstridendeBestemmelse, Funn.GjelderBestemmelse)
                .Select(k => new { rolle = k.Rolle, etikett = k.Etikett, merknad = k.Merknad, eli = k.RettskildeEli, eid = k.Eid }).ToList(),
            navneformer = Rolle(Funn.Navneform).Select(k => new { etikett = k.Etikett, merknad = k.Merknad }).ToList(),
            aktorer = Rolle(Funn.BerortAktor).Select(k => new { etikett = k.Etikett, merknad = k.Merknad }).ToList(),
            etterfolgere = Rolle(Funn.Etterfolger).Select(k => new { etikett = k.Etikett, merknad = k.Merknad }).ToList(),
            bortforklaringer = Punkter(Funn.BortforklaringProvd).Select(p => new { overskrift = p.Overskrift, tekst = p.Tekst }).ToList(),
            vurderingspunkter = Punkter(Funn.Vurdering).Select(p => new { overskrift = p.Overskrift, tekst = p.Tekst, sikkerhet = p.Sikkerhet }).ToList(),
            ikkeAvgjort = Punkter(Funn.IkkeAvgjort).Select(p => new { overskrift = p.Overskrift, tekst = p.Tekst }).ToList(),
            kilder = Rolle(Funn.Kilde).Select(k => new { etikett = k.Etikett, merknad = k.Merknad, lenke = k.EksternLenke, eli = k.RettskildeEli, eid = k.Eid }).ToList(),
            juristvurdertAvKi = f.VurdertAvType == Funn.KiAgent,
            godkjentDato = godkjentDato.ToString("yyyy-MM-dd"),
        };
    }

    public static string Serialiser(object innhold) => JsonSerializer.Serialize(innhold, JsonUt);

    public static string Hash(string json) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    // ---------------------------------------------------------------------------------------------------------------------------

    private static void NullstillGodkjenning(FunnEntitet funn)
    {
        funn.GodkjentAv = null;
        funn.GodkjentTidspunkt = null;
        funn.GodkjentInnholdJson = null;
        funn.GodkjentInnholdHash = null;
    }

    private static ProveniensEntitet Proveniens(Guid id, string handling, string av, string merknad)
    {
        var p = ProveniensHjelper.NyRad(Funn.ProveniensType, id, virksomhetId: null, handling, av);
        p.KildeReferanserJson = JsonSerializer.Serialize(new { issue = 429, merknad }, JsonSerialiseringHjelper.Innstillinger);
        return p;
    }

    private static void ValiderKategori(string kategori, string? undertype)
    {
        if (!Funn.Kategorier.Contains(kategori))
        {
            throw new ArgumentException($"Ukjent kategori «{kategori}». Gyldige: {string.Join(", ", Funn.Kategorier)}.");
        }
        if (!string.IsNullOrWhiteSpace(undertype) && !Funn.Undertyper[kategori].Contains(undertype))
        {
            throw new ArgumentException($"Undertypen «{undertype}» hører ikke til kategorien «{kategori}».");
        }
    }

    private static string Pakrevd(string? verdi, string felt) =>
        string.IsNullOrWhiteSpace(verdi) ? throw new ArgumentException($"{felt} er påkrevd.") : verdi.Trim();

    private static string? Tom(string? verdi) => string.IsNullOrWhiteSpace(verdi) ? null : verdi.Trim();

    private static List<FunnVurderingspunktEntitet> ByggVurderingspunkter(Guid funnId, IReadOnlyList<NyttVurderingspunkt> punkter)
    {
        var ut = new List<FunnVurderingspunktEntitet>();
        for (var i = 0; i < punkter.Count; i++)
        {
            var p = punkter[i];
            if (!Funn.VurderingspunktTyper.Contains(p.Type))
            {
                throw new ArgumentException($"Ukjent vurderingspunkt-type «{p.Type}». Gyldige: {string.Join(", ", Funn.VurderingspunktTyper)}.");
            }
            var sikkerhet = Tom(p.Sikkerhet);
            if (sikkerhet is not null && (p.Type != Funn.Vurdering || !Funn.Sikkerheter.Contains(sikkerhet)))
            {
                throw new ArgumentException($"Sikkerheten «{sikkerhet}» er bare lov på type «vurdering», med verdiene {string.Join(", ", Funn.Sikkerheter)}.");
            }
            ut.Add(new FunnVurderingspunktEntitet
            {
                Id = Guid.NewGuid(), FunnId = funnId, Type = p.Type, Sortering = i,
                Overskrift = Tom(p.Overskrift), Tekst = Pakrevd(p.Tekst, "Teksten i et vurderingspunkt"), Sikkerhet = sikkerhet,
            });
        }
        return ut;
    }

    /// <summary>
    /// Bygger koblingene. En bestemmelse slås opp på ELI-en mot den ENE gjeldende primærversjonen, og eId-en mot den ENE gjeldende noden i
    /// den versjonen. Gir oppslaget null eller flere treff, blir id-en NULL og koblingen står med ELI og eId — aldri nærmeste treff
    /// (CLAUDE.md §8). Aktører, navneformer, kanter og kildefeil må finnes.
    /// </summary>
    private async Task<List<FunnKoblingEntitet>> ByggKoblingerAsync(Guid funnId, IReadOnlyList<NyKobling> koblinger, CancellationToken ct)
    {
        var ut = new List<FunnKoblingEntitet>();
        for (var i = 0; i < koblinger.Count; i++)
        {
            var k = koblinger[i];
            if (!Funn.Koblingsroller.Contains(k.Rolle))
            {
                throw new ArgumentException($"Ukjent koblingsrolle «{k.Rolle}». Gyldige: {string.Join(", ", Funn.Koblingsroller)}.");
            }
            var eli = Tom(k.RettskildeEli);
            var lenke = Tom(k.EksternLenke);
            var mal = (eli is null ? 0 : 1) + (k.VirksomhetId is null ? 0 : 1) + (k.BegrepId is null ? 0 : 1)
                      + (k.StrukturkantId is null ? 0 : 1) + (k.KildefeilId is null ? 0 : 1) + (lenke is null ? 0 : 1);
            if (mal != 1)
            {
                throw new ArgumentException($"Koblingen «{k.Etikett}» må ha nøyaktig ett mål (rettskilde, aktør, navneform, kant, kildefeil eller lenke).");
            }
            var rolleOk = k.Rolle switch
            {
                Funn.GjelderBestemmelse or Funn.MotstridendeBestemmelse => eli is not null,
                Funn.BerortAktor => k.VirksomhetId is not null,
                Funn.Navneform => k.BegrepId is not null,
                Funn.Etterfolger => k.StrukturkantId is not null,
                Funn.Kilde => eli is not null || lenke is not null || k.KildefeilId is not null,
                Funn.Malvedtak => eli is not null,
                _ => false,
            };
            if (!rolleOk)
            {
                throw new ArgumentException($"Koblingen «{k.Etikett}» har et mål som ikke passer rollen «{k.Rolle}».");
            }
            if (k.Eid is not null && eli is null)
            {
                throw new ArgumentException($"Koblingen «{k.Etikett}» har eId uten ELI.");
            }
            var quote = Tom(k.QuoteExact);
            if (quote is not null && (Tom(k.Eid) is null || k.Rolle is not (Funn.GjelderBestemmelse or Funn.MotstridendeBestemmelse)))
            {
                throw new ArgumentException($"Koblingen «{k.Etikett}» har sitat uten å være en bestemmelse med eId.");
            }
            if (k.VirksomhetId is { } vid && !await db.Virksomheter.AnyAsync(v => v.Id == vid, ct))
                throw new ArgumentException($"Aktøren {vid} finnes ikke.");
            if (k.BegrepId is { } bid && !await db.Begreper.IgnoreQueryFilters().AnyAsync(b => b.Id == bid, ct))
                throw new ArgumentException($"Navneformen {bid} finnes ikke.");
            if (k.StrukturkantId is { } sid && !await db.Strukturkanter.IgnoreQueryFilters().AnyAsync(s => s.Id == sid, ct))
                throw new ArgumentException($"Kanten {sid} finnes ikke.");
            if (k.KildefeilId is { } fid && !await db.Kildefeil.AnyAsync(x => x.Id == fid, ct))
                throw new ArgumentException($"Kildefeilen {fid} finnes ikke.");

            Guid? rettskildeId = null, nodeId = null;
            var eid = Tom(k.Eid);
            if (eli is not null)
            {
                var kandidater = await db.Rettskilder
                    .Where(r => r.Eli == eli && r.Entitetsstatus == "gjeldende" && r.Importrolle == "primaer")
                    .Select(r => r.Id).Take(2).ToListAsync(ct);
                if (kandidater.Count == 1)
                {
                    rettskildeId = kandidater[0];
                    if (eid is not null)
                    {
                        var noder = await db.RettskildeNoder
                            .Where(n => n.RettskildeId == rettskildeId && n.Eid == eid && n.Entitetsstatus == "gjeldende")
                            .Select(n => n.Id).Take(2).ToListAsync(ct);
                        if (noder.Count == 1) nodeId = noder[0];
                    }
                }
            }
            ut.Add(new FunnKoblingEntitet
            {
                Id = Guid.NewGuid(), FunnId = funnId, Rolle = k.Rolle, Sortering = i,
                Etikett = Pakrevd(k.Etikett, "Etiketten på en kobling"), Merknad = Tom(k.Merknad),
                RettskildeEli = eli, RettskildeId = rettskildeId, Eid = eid, NodeId = nodeId,
                VirksomhetId = k.VirksomhetId, BegrepId = k.BegrepId, StrukturkantId = k.StrukturkantId, KildefeilId = k.KildefeilId,
                EksternLenke = lenke, QuoteExact = quote,
            });
        }
        return ut;
    }
}
