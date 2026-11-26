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
            // [ENDRET, #429 PR 2] Målvedtaket til et endringsvedtak er ikke «funn i rettskilde X»: funnet står i endringsvedtaket.
            // [ENDRET, #472 PR 2] Det samme gjelder loven som opphever en hjemmel, og overgangsregelen: funnet står i forskriften.
            q = q.Where(f => f.Koblinger.Any(k => k.Rolle != Funn.Malvedtak && k.Rolle != Funn.OpphevendeBestemmelse && k.Rolle != Funn.Videreforingsbestemmelse
                                                  && (k.RettskildeId == rid || (eli != null && k.RettskildeEli == eli))));
        }
        if (virksomhetId is { } vid) q = q.Where(f => f.Koblinger.Any(k => k.VirksomhetId == vid));
        // [ENDRET, #429 PR 4, Johanns avgjørelse 2026-11-04 (2)] Funn der navnet bare står i avgiverformelen, sorteres sist. De holdes ikke tilbake.
        return await q.OrderBy(f => f.Undertype == UtgattAktorFunnregel.UndertypeAvgiverformel ? 1 : 0)
            .ThenByDescending(f => f.OpprettetTidspunkt).ToListAsync(ct);
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
    /// [Ny, #429 PR 3, Johanns avgjørelse 2026-11-02 (2)] Oppdaget → forklart bort, satt av en MASKINREGEL: et lastet maskinfunn som en ny
    /// regel holder tilbake. <see cref="FunnEntitet.VurdertAvType"/> blir <see cref="Funn.Maskinregel"/> og
    /// <see cref="FunnEntitet.ForklartBortAv"/> regelens kilde («monster:utgatt-aktor»), så det aldri ser ut som et menneske har gjort det
    /// (CLAUDE.md §9). Begrunnelsen har regelen og beviset (eId, kilde-ID). Hvem som kjørte regelen, står i proveniensen.
    /// <para>
    /// [LÅST, Johann 2026-11-02] Funnet slettes aldri, og det gjenåpnes ikke av en ny kjøring (nøkkelen finnes). Bare <c>oppdaget</c>: et funn
    /// en jurist eller juristagenten har vurdert, overstyres ikke av en maskinregel. Det gir <c>false</c>.
    /// </para>
    /// </summary>
    public async Task<bool> ForklarBortMaskineltAsync(
        Guid id, string begrunnelse, string regelkilde, string kjortAv, CancellationToken ct = default)
    {
        var funn = await db.Funn.FirstOrDefaultAsync(f => f.Id == id, ct);
        // [ENDRET, #429 PR 4] Et funn som et menneske har gjenåpnet, regnes som menneskelig vurdert og forklares ikke bort av en maskinregel.
        if (funn is null || funn.Status != Funn.Oppdaget || funn.GjenapnetTidspunkt is not null) return false;
        var naa = DateTimeOffset.UtcNow;
        funn.ForklartBortBegrunnelse = Pakrevd(begrunnelse, "Begrunnelse");
        funn.ForklartBortAv = Pakrevd(regelkilde, "Regelkilde");
        funn.ForklartBortTidspunkt = naa;
        funn.VurdertAvType = Funn.Maskinregel;
        funn.Status = Funn.ForklartBort;
        funn.SistEndretAv = regelkilde;
        funn.SistEndretTidspunkt = naa;
        db.Proveniens.Add(Proveniens(funn.Id, "forklart_bort", kjortAv,
            $"Forklart bort maskinelt av {regelkilde}, kjørt av {kjortAv}. {begrunnelse}"));
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// [Ny, #429 PR 4, Johanns avgjørelse 2026-11-04 (4)] <b>Gjenåpning:</b> forklart bort → oppdaget, med begrunnelse. Bare fra
    /// <c>forklart_bort</c>, og bare til <c>oppdaget</c>. Tilgangen (jurist eller publiseringsansvarlig) sjekkes av kalleren (API-et).
    /// <para>
    /// Utløst av måling 3: funnet F06 (FOR-2020-01-10-33, KMD) ble forklart bort av maskinregelen fordi opphevingen i FOR-2022-10-21-1777 ble
    /// lest som hel («oppheves for så vidt gjelder …»). Begrunnelsen og hvem som forklarte bort, nulles på funnet, men står ordrett i proveniensen
    /// sammen med gjenåpningen. Er funnet forklart bort av en maskinregel, nulles også <see cref="FunnEntitet.VurdertAvType"/>
    /// (<see cref="Funn.Maskinregel"/>), så merket «Forklart bort av maskinregel» forsvinner. En tidligere juristvurdering blir stående.
    /// </para>
    /// <para>
    /// [LÅST, Johann 2026-11-04] Et gjenåpnet funn regnes som menneskelig vurdert: <see cref="FunnEntitet.GjenapnetTidspunkt"/> står, og
    /// maskinregelen forklarer det ikke bort igjen.
    /// </para>
    /// </summary>
    public async Task<FunnEntitet?> GjenapneAsync(Guid id, string begrunnelse, string av, DateTimeOffset? tidspunkt = null, CancellationToken ct = default)
    {
        var funn = await HentAsync(id, ct);
        if (funn is null) return null;
        if (funn.Status != Funn.ForklartBort)
        {
            throw new InvalidOperationException("Bare et funn som er forklart bort, kan gjenåpnes.");
        }
        var tekst = Pakrevd(begrunnelse, "Begrunnelse");
        var naa = tidspunkt ?? DateTimeOffset.UtcNow;
        var tidligere = $"Forklart bort av {funn.ForklartBortAv} {funn.ForklartBortTidspunkt:yyyy-MM-dd}"
            + (funn.VurdertAvType == Funn.Maskinregel ? " (maskinregel)" : "") + $": {funn.ForklartBortBegrunnelse}";
        if (funn.VurdertAvType == Funn.Maskinregel) funn.VurdertAvType = null;
        funn.ForklartBortBegrunnelse = null;
        funn.ForklartBortAv = null;
        funn.ForklartBortTidspunkt = null;
        funn.GjenapnetAv = av;
        funn.GjenapnetTidspunkt = naa;
        funn.GjenapnetBegrunnelse = tekst;
        funn.Status = Funn.Oppdaget;
        funn.SistEndretAv = av;
        funn.SistEndretTidspunkt = naa;
        db.Proveniens.Add(Proveniens(funn.Id, "gjenapnet", av, $"Gjenåpnet: {tekst} Tidligere: {tidligere}"));
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
    /// <item>[Ny, #429 PR 3, R9, Johanns avgjørelse 2026-11-02 (3)] Hjemmelsloven er opphevet av en gjeldende lov i korpus
    /// (<see cref="Opphevingskontroll.HjemmelOpphevetAsync"/>). Funnet undertrykkes ikke og går ikke til kildefeilkøen: om vedtaket fortsatt
    /// har virkning, er et rettslig spørsmål (overgangsregler som havne- og farvannsloven § 53 første ledd), ikke en feil i Lovdata-kopien.
    /// Mangelen står også på et funn som bare er oppdaget, og forsvinner når juristen har et vurderingspunkt
    /// «<see cref="Funn.HjemmelOverskrift"/>».</item>
    /// Andre kategorier har ingen premissport i denne runden.
    /// </summary>
    public async Task<List<string>> PremissmanglerAsync(FunnEntitet funn, CancellationToken ct = default) =>
        (await PremissmanglerForAsync([funn], ct))[funn.Id];

    /// <summary>[Ny, #429 PR 2] Premissmanglene for mange funn med to oppslag i stedet for to per kobling: lista får rundt 700 maskinfunn.
    /// Samme regel som <see cref="PremissmanglerAsync"/>.</summary>
    public async Task<Dictionary<Guid, List<string>>> PremissmanglerForAsync(IReadOnlyCollection<FunnEntitet> funn, CancellationToken ct = default)
    {
        var ut = await PremissmanglerUtgattAktorAsync(funn, ct);
        await LeggTilPremissmanglerHjemmelOpphevetAsync(funn, ut, ct);
        await LeggTilMalUtenforKorpusAsync(funn, ut, ct);
        return ut;
    }

    /// <summary>[Ny, #446 PR 2, AK 7] Premissmangelen når kilden har uttømt virkning, men et mål i «Endrer» ikke er i korpus. Starter alltid slik.</summary>
    public const string MalUtenforKorpusMangelStart = "Mål utenfor korpus";

    /// <summary>
    /// [Ny, #446 PR 2, AK 7, juristrunden D14] Funn fra #429 og #472 i en kilde som har uttømt virkning (<c>rent_endringsvedtak = true</c>), men der minst ett
    /// mål i Lovdatas «Endrer» ikke er en gjeldende primærkilde i korpus. Maskinreglene utelater ikke slike kilder stille: teksten i målet kan ikke
    /// kontrolleres, så funnet står med denne mangelen til juristen har vurdert målet.
    /// </summary>
    private async Task LeggTilMalUtenforKorpusAsync(IReadOnlyCollection<FunnEntitet> funn, Dictionary<Guid, List<string>> ut, CancellationToken ct)
    {
        var aktuelle = funn.Where(f => f.Kategori is Funn.UtgattAktor or Funn.HjemmelOpphevet).ToList();
        var kildeIder = aktuelle.SelectMany(f => f.Koblinger).Where(k => k.Rolle == Funn.GjelderBestemmelse && k.RettskildeId != null)
            .Select(k => k.RettskildeId!.Value).Distinct().ToList();
        if (kildeIder.Count == 0) return;
        var rene = await db.Rettskilder.AsNoTracking().Where(r => kildeIder.Contains(r.Id) && r.RentEndringsvedtak == true).Select(r => r.Id).ToListAsync(ct);
        var malUtenfor = await VedtaksformTjeneste.MalUtenforKorpusAsync(db, rene, ct);
        foreach (var f in aktuelle)
        {
            var kilde = f.Koblinger.Where(k => k.Rolle == Funn.GjelderBestemmelse && k.RettskildeId != null).Select(k => k.RettskildeId!.Value)
                .FirstOrDefault(malUtenfor.ContainsKey);
            if (kilde == Guid.Empty) continue;
            var mal = malUtenfor[kilde];
            ut[f.Id].Add($"{MalUtenforKorpusMangelStart} (#446): kilden har uttømt virkning (et rent endrings-, ikrafttredelses- eller opphevingsvedtak), men "
                + $"{(mal.Count == 1 ? "målet" : $"{mal.Count} mål")} i «Endrer» er ikke i korpus ({string.Join(", ", mal.Take(3))}{(mal.Count > 3 ? " …" : "")}). "
                + "Om teksten der er riktig, er ikke kontrollert. Juristen vurderer målet.");
        }
    }

    private async Task<Dictionary<Guid, List<string>>> PremissmanglerUtgattAktorAsync(IReadOnlyCollection<FunnEntitet> funn, CancellationToken ct)
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
        var hjemmelOpphevet = await HjemmelOpphevetForAsync(aktuelle.Where(f => f.Status != Funn.ForklartBort && HjemmelVurdering(f) is null).ToList(), ct);

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
            foreach (var h in hjemmelOpphevet.GetValueOrDefault(f.Id) ?? []) mangler.Add(Opphevingskontroll.Premissmangeltekst(h));
        }
        return ut;
    }

    /// <summary>[Ny, #472 PR 2] Premissmangelen når «Ny hjemmel» ikke er vurdert. Starter alltid slik (appen skiller merkene på det).</summary>
    public const string NyHjemmelMangelStart = "Ny hjemmel ikke vurdert";

    /// <summary>[Ny, #472 PR 2] Premissmangelen når #446 ikke har klassifisert forskriften. Starter alltid slik.</summary>
    public const string UttomtVirkningMangelStart = "Uttømt virkning ikke klassifisert";

    /// <summary>[Ny, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1] Premissmangelen når korpuset bare har forskriften som
    /// kunngjort versjon (LTI). Starter alltid slik.</summary>
    public const string KunngjortMangelStart = "Kunngjort versjon, gjeldende status ikke kontrollert";

    /// <summary>
    /// [Ny, #472 PR 2, Johanns avgjørelser 2026-11-18 og 2026-11-20] <b>Premissporten for «Hjemmel opphevet»</b> (svakeste premiss, docs/33 §4.6).
    /// Mangelen står også på et funn som bare er oppdaget, som R9, så den synes før juristen tar det:
    /// <list type="bullet">
    /// <item><b>«Ny hjemmel»</b> (alle undertyper unntatt gjenstandsløs): juristen sier om forskriften kan stå på en ny hjemmel som gir samme
    /// kompetanse, eller merker punktet «ikke vurdert». Til da vises funnet ikke som «mulig bortfalt» (<see cref="MuligBortfaltVises"/>). Et
    /// maskinsignal for ny tilsvarende hjemmel er avvist i juristrunde 2.</item>
    /// <item><b>«Hjemmel»</b> (som R9): om forskriften fortsatt har virkning — videreført, erstattet eller uten virkning.</item>
    /// <item><b>#446:</b> forskriften er ikke klassifisert som rent endrings-, opphevings- eller ikrafttredelsesvedtak (<c>rent_endringsvedtak</c> er
    /// NULL). Slike funn finnes bare når bryteren <see cref="HjemmelOpphevetFunnregel.KonfignokkelIkkeKlassifisertGirFunn"/> er på.</item>
    /// <item>[Ny, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1] <b>Kunngjort versjon:</b> korpuset har bare forskriften slik den
    /// ble kunngjort i Norsk Lovtidend (<c>kildeform = kunngjort</c>), så Lovdatas ajourførte status er ikke kontrollert. I måling 1 var fem slike
    /// vedtak opphevet i Lovdata (DELO); to av dem ved en lov som ikke er i korpus (militærnekterloven § 10, LOV-2012-04-20-19). Mangelen
    /// forsvinner når juristen har et vurderingspunkt «<see cref="Funn.GjeldendeStatusOverskrift"/>» som sier hva Lovdata viser.</item>
    /// </list>
    /// </summary>
    private async Task LeggTilPremissmanglerHjemmelOpphevetAsync(IReadOnlyCollection<FunnEntitet> funn, Dictionary<Guid, List<string>> ut, CancellationToken ct)
    {
        var aktuelle = funn.Where(f => f.Kategori == Funn.HjemmelOpphevet).ToList();
        if (aktuelle.Count == 0) return;
        var forskriftIder = aktuelle.SelectMany(f => f.Koblinger).Where(k => k.Rolle == Funn.GjelderBestemmelse && k.RettskildeId != null)
            .Select(k => k.RettskildeId!.Value).Distinct().ToList();
        var forskrifter = forskriftIder.Count == 0 ? []
            : await db.Rettskilder.AsNoTracking().Where(r => forskriftIder.Contains(r.Id))
                .Select(r => new { r.Id, r.RentEndringsvedtak, r.Kildeform }).ToDictionaryAsync(r => r.Id, ct);
        foreach (var f in aktuelle)
        {
            var mangler = ut[f.Id];
            if (f.Undertype != HjemmelOpphevetFunnregel.UndertypeGjenstandslos && !NyHjemmelVurdert(f))
            {
                mangler.Add($"{NyHjemmelMangelStart}: vurderingen har ikke noe punkt «{Funn.NyHjemmelOverskrift}» som sier om forskriften kan stå på en "
                    + "ny hjemmel som gir samme kompetanse. Funnet vises ikke som «mulig bortfalt» før punktet er vurdert eller merket «ikke vurdert».");
            }
            if (HjemmelVurdering(f) is null)
            {
                mangler.Add("Hjemmel ikke vurdert: vurderingen har ikke noe punkt «Hjemmel» som sier om forskriften fortsatt har virkning "
                    + "(videreført etter en overgangsregel, erstattet av en ny ordning, eller uten virkning).");
            }
            var forskrift = f.Koblinger.Where(k => k.Rolle == Funn.GjelderBestemmelse && k.RettskildeId != null).Select(k => k.RettskildeId!.Value)
                .FirstOrDefault();
            if (forskrift != Guid.Empty && forskrifter.TryGetValue(forskrift, out var fk) && fk.Kildeform == HjemmelOpphevetFunnregel.KildeformKunngjort
                && GjeldendeStatusVurdering(f) is null)
            {
                mangler.Add($"{KunngjortMangelStart}: korpuset har bare forskriften slik den ble kunngjort i Norsk Lovtidend, og Lovdatas ajourførte "
                    + $"status er ikke slått opp. Den kan selv være opphevet ved en kilde som ikke er i korpus. Juristen slår den opp og skriver et punkt "
                    + $"«{Funn.GjeldendeStatusOverskrift}».");
            }
            if (forskrift == Guid.Empty || !forskrifter.TryGetValue(forskrift, out var fr) || fr.RentEndringsvedtak is null)
            {
                // [ENDRET, #446 PR 2] NULL betyr nå at #446 ga ukjent form, eller ren form med ukjent ikraftstatus.
                mangler.Add($"{UttomtVirkningMangelStart} (#446): det er ikke avgjort om forskriften er et rent endrings-, opphevings- eller "
                    + "ikrafttredelsesvedtak som er i kraft (formen er ukjent, eller ikrafttredelsen er ikke lest). Et slikt vedtak har uttømt virkningen "
                    + "og gir ikke funn.");
            }
        }
    }

    /// <summary>[Ny, #472 PR 2] Juristens punkt «Ny hjemmel»: vurdert (type vurdering) eller merket «ikke vurdert» (type ikke_avgjort).</summary>
    public static bool NyHjemmelVurdert(FunnEntitet f) => f.Vurderingspunkter.Any(p => p.Type is Funn.Vurdering or Funn.IkkeAvgjort
        && string.Equals(p.Overskrift?.Trim(), Funn.NyHjemmelOverskrift, StringComparison.OrdinalIgnoreCase));

    /// <summary>[Ny, #472 PR 2, Johanns avgjørelse 2026-11-18] Om funnet kan vises som «mulig bortfalt»: bare undertypene der alle hjemlene som
    /// teller, faller (hjemmel opphevet, videreføringen utløpt), og bare når «Ny hjemmel» er vurdert eller merket «ikke vurdert» (premissporten).
    /// <c>null</c> for funn der merket ikke hører hjemme (andre kategorier, blandede hjemler, uavklart, gjenstandsløs).</summary>
    public static bool? MuligBortfaltVises(FunnEntitet f) =>
        f.Kategori == Funn.HjemmelOpphevet && f.Undertype is HjemmelOpphevetFunnregel.UndertypeHjemmelOpphevet or HjemmelOpphevetFunnregel.UndertypeVidereforingUtlopt
            ? NyHjemmelVurdert(f) : null;

    /// <summary>[Ny, #429 PR 3, R9] Hjemmelslovene som er opphevet, per funn, lest av rettskilden funnet står i og funnets egne noder. Tre
    /// til fem oppslag for hele lista.</summary>
    private async Task<Dictionary<Guid, List<HjemmelOpphevet>>> HjemmelOpphevetForAsync(IReadOnlyList<FunnEntitet> funn, CancellationToken ct)
    {
        var ut = new Dictionary<Guid, List<HjemmelOpphevet>>();
        var bestemmelser = funn.SelectMany(f => f.Koblinger.Where(k => k.Rolle == Funn.GjelderBestemmelse && k.RettskildeId != null)
            .Select(k => (Funn: f.Id, Rk: k.RettskildeId!.Value, k.Eid, k.NodeId))).ToList();
        if (bestemmelser.Count == 0) return ut;
        var nodeIder = bestemmelser.Where(b => b.NodeId != null).Select(b => b.NodeId!.Value).Distinct().ToList();
        var tekster = new Dictionary<Guid, string?>();
        foreach (var bit in nodeIder.Chunk(2000))
        {
            foreach (var n in await db.RettskildeNoder.AsNoTracking().Where(n => bit.Contains(n.Id)).Select(n => new { n.Id, n.Tekst }).ToListAsync(ct))
            {
                tekster[n.Id] = n.Tekst;
            }
        }
        var nodetekster = bestemmelser.Where(b => b.Eid != null && b.NodeId != null).GroupBy(b => b.Rk)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<(string Eid, string? Tekst)>)g.Select(b => (b.Eid!, tekster.GetValueOrDefault(b.NodeId!.Value)))
                .DistinctBy(x => x.Item1).ToList());
        var perRk = await Opphevingskontroll.HjemmelOpphevetAsync(db, bestemmelser.Select(b => b.Rk).Distinct().ToList(), nodetekster, ct);
        foreach (var g in bestemmelser.GroupBy(b => b.Funn))
        {
            var eider = g.Where(b => b.Eid != null).Select(b => b.Eid!).ToList();
            var liste = g.Select(b => b.Rk).Distinct().SelectMany(rk => perRk.GetValueOrDefault(rk) ?? []).Where(h => h.GjelderNoder(eider))
                .DistinctBy(h => h.HjemmelEli).ToList();
            if (liste.Count > 0) ut[g.Key] = liste;
        }
        return ut;
    }

    /// <summary>[Ny, #472 PR 4] Juristens punkt «Gjeldende status» (type vurdering): hva Lovdata viser for en kunngjort versjon.</summary>
    public static FunnVurderingspunktEntitet? GjeldendeStatusVurdering(FunnEntitet f) => f.Vurderingspunkter
        .Where(p => p.Type == Funn.Vurdering && string.Equals(p.Overskrift?.Trim(), Funn.GjeldendeStatusOverskrift, StringComparison.OrdinalIgnoreCase))
        .OrderBy(p => p.Sortering).FirstOrDefault();

    /// <summary>[Ny, #429 PR 3, R9] Juristens vurdering av om vedtaket fortsatt har virkning når hjemmelsloven er opphevet: vurderingspunktet
    /// med overskriften <see cref="Funn.HjemmelOverskrift"/>. Null når det ikke finnes.</summary>
    public static FunnVurderingspunktEntitet? HjemmelVurdering(FunnEntitet f) => f.Vurderingspunkter
        .Where(p => p.Type == Funn.Vurdering && string.Equals(p.Overskrift?.Trim(), Funn.HjemmelOverskrift, StringComparison.OrdinalIgnoreCase))
        .OrderBy(p => p.Sortering).FirstOrDefault();

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
            // [ENDRET, #472 PR 2] Den opphevende bestemmelsen og overgangsregelen står med rollen sin. Funn uten disse rollene får samme innhold
            // og samme hash som før.
            bestemmelser = Rolle(Funn.MotstridendeBestemmelse, Funn.GjelderBestemmelse, Funn.OpphevendeBestemmelse, Funn.Videreforingsbestemmelse)
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
                Funn.GjelderBestemmelse or Funn.MotstridendeBestemmelse or Funn.OpphevendeBestemmelse or Funn.Videreforingsbestemmelse => eli is not null,
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
