using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

// ------------------------------------------------------------------------------------------------ Svarformene

/// <summary>[Ny, issue #441] Hvor hjemmelen for aktøren er funnet, i regelens rekkefølge (<see cref="AktorsideTjeneste.HjemmelAsync"/>).</summary>
public static class Hjemmelkilder
{
    /// <summary>R <c>oppretter</c> til aktøren (livsløpet, docs/33 §5.9): vedtaket som oppretter den.</summary>
    public const string Oppretterkant = "oppretter_kant";
    /// <summary>En navneform med <see cref="BegrepEntitet.HjemmelArt"/> (oppretting eller navnevedtak).</summary>
    public const string HjemmelArt = "hjemmel_art";
    /// <summary>Den gjeldende navneformen har hjemmel i en kilde med disposisjonstype oppretting, navneendring eller omorganisering (#395).</summary>
    public const string Disposisjonstype = "disposisjonstype";

    /// <summary>[LÅST, #441] Disposisjonstypene som gjør navneformens hjemmel til hjemmel for aktøren (regel 3).</summary>
    public static readonly IReadOnlySet<string> Organiseringstyper = new HashSet<string>
        { Disposisjonstyper.Oppretting, Disposisjonstyper.Navneendring, Disposisjonstyper.Omorganisering };
}

/// <summary>[Ny, issue #441] Navneformen i hjemmelkortet, med hjemmelen og statusen.</summary>
public sealed record AktorHjemmelNavneform(
    Guid Id, string Term, string? Navneformgrunn, string? Kunnskapsstatus, string? Godkjenning, string? OppdagelsesKilde, string? HjemmelArt,
    Guid? RettskildeId, string? RettskildeTittel, string? Eid);

/// <summary>[Ny, issue #441] «Mest omtalt i (ikke hjemmel)»: en rettskilde med flest direkte utsagn og taggede forekomster. <see cref="Eid"/> og
/// <see cref="Sitat"/> er den første bestemmelsen i dokumentrekkefølge der aktøren står (visning, ikke en påstand).</summary>
public sealed record MestOmtalt(
    Guid RettskildeId, string Tittel, string? Kortnavn, int Utsagn, int TaggedeForekomster, IReadOnlyList<string> Bestemmelser, string? Eid, string? Sitat);

/// <summary>[Ny, issue #441] «Det som finnes» under hullet: <see cref="Art"/> = <c>klasse</c> (utledet medlemskap, #384) eller <c>register</c>
/// (registerdata og kanter med kilde utenfor korpus av typen register). Ingen av dem er hjemmel for at aktøren finnes.</summary>
public sealed record DetSomFinnes(
    string Art, string Tekst, Guid? NodeId, Guid? HjemmelRettskildeId, string? HjemmelTittel, string? HjemmelEid,
    string? KlassensKunnskapsstatus, string? Kunnskapsstatus, bool Forslag);

/// <summary>
/// [Ny, issue #441] Svaret på <c>GET /api/virksomheter/{id}/hjemmel</c>. <see cref="Status"/> = <c>hjemlet</c> | <c>mangler</c>.
/// Når hjemmelen er funnet: kilden (<see cref="Hjemmelkilder"/>), rettskilden og bestemmelsen, sitatet, navneformen og R oppretter med status,
/// og alle utsagnene om aktøren i samme rettskilde (<see cref="UtsagnIKilden"/>, de gjentas ikke i rettskildelista). Når den mangler: hullmerknaden
/// fra aktørkøen, «mest omtalt i» og «det som finnes».
/// </summary>
/// <param name="Navnevedtak">Navneformen med <c>hjemmel_art</c> når den står i en annen rettskilde enn oppretterkanten («Navnet: …»).</param>
/// <param name="HjemmelArtForslag">Det «Godkjenn hjemmelen» setter når navneformen ikke har <c>hjemmel_art</c>. Null med
/// <see cref="MaVelgeHjemmelArt"/> = regelen kan ikke avgjøre det (bare omorganisering, eller både oppretting og navneendring): et menneske velger.</param>
/// <param name="Venter">Navneformen eller oppretterkanten er et forslag: det finnes noe å godkjenne.</param>
public sealed record AktorHjemmel(
    Guid VirksomhetId, string Status, string? Kilde,
    Guid? RettskildeId, string? RettskildeTittel, string? Kortnavn, string? DokumentId, string? Eid, string? Sitat, DateOnly? GjelderFra,
    IReadOnlyList<string> Disposisjonstyper, AktorHjemmelNavneform? Navneform, StrukturkantVisning? Oppretterkant,
    AktorHjemmelNavneform? Navnevedtak, string? HjemmelArtForslag, bool MaVelgeHjemmelArt, bool Venter,
    IReadOnlyList<StrukturkantVisning> UtsagnIKilden,
    string? Hullmerknad, IReadOnlyList<MestOmtalt> MestOmtalt, IReadOnlyList<DetSomFinnes> DetSomFinnes);

/// <summary>[Ny, issue #441] Kvitteringen for «Godkjenn hjemmelen».</summary>
public sealed record HjemmelGodkjent(Guid? NavneformId, bool NavneformGodkjent, Guid? OppretterkantId, bool OppretterkantGodkjent, string? HjemmelArt);

/// <summary>
/// [Ny, issue #441] Én rad i «Andre rettskilder aktøren står i». <see cref="Gruppe"/> = <c>direkte</c> | <c>via_klasse</c>; samme rettskilde kan stå
/// i begge gruppene, med hver sine tall. <see cref="RettskildeId"/> er null for kanter med kilde utenfor korpus (én rad per kildetype).
/// <para>Tellerne: <see cref="Kompetanse"/> (K), <see cref="Plikt"/> (P), <see cref="Oppgave"/> (F) og <see cref="Annet"/> (resten) teller utsagn
/// der aktøren er part (direkte) eller treff via klassen; hvert utsagn telles i én rad. <see cref="Venter"/> er forslag som ikke er godkjent: utsagn,
/// navnetreff (virksomhetskandidater) og navneformer med hjemmel i rettskilden. Via klasse er det utsagnene på klassen som er forslag.</para>
/// </summary>
public sealed record AktorRettskilderad(
    string Gruppe, Guid? RettskildeId, string Tittel, string? Kortnavn, string? Kildetype, string? DokumentId, string? KildeUtenforKorpusType,
    bool AnsvarligDepartement, bool FastsattAv, int NavnetTagget, int NavnetreffVenter, int NavneformerVenter,
    int Kompetanse, int Plikt, int Oppgave, int Annet, int Venter,
    IReadOnlyList<StrukturkantVisning> Utsagn, IReadOnlyList<KompetanseViaKlasseTreff> ViaKlasse);

/// <summary>[Ny, issue #441] Tallene øverst på lista (filterknappene) og kontrollsummen for AK 3.</summary>
/// <param name="Kanter">Alle strukturkanter der aktøren er part (som fanen «Etter kategori» viser).</param>
/// <param name="KanterIRadene">Summen av K+P+F+Annet i de direkte radene.</param>
/// <param name="KanterIHjemmelen">Utsagnene i hjemmelkortet (rettskilden som ikke gjentas).</param>
public sealed record AktorRettskildeTall(
    int Rettskilder, int MedUtsagn, int Venter, int AnsvarligDepartement, int FastsattAv, int NavnetTagget, int Direkte, int ViaKlasse,
    int Kanter, int KanterIRadene, int KanterIHjemmelen);

/// <summary>[Ny, issue #441] Svaret på <c>GET /api/virksomheter/{id}/rettskilder</c>. <see cref="Premisser"/> er kantene treffene via klasse
/// bygger på (én gang hver). <see cref="Hull"/> er det oppslaget ikke kan avgjøre.</summary>
public sealed record AktorRettskilder(
    Guid VirksomhetId, Guid? HjemmelRettskildeId, IReadOnlyList<AktorRettskilderad> Rader, IReadOnlyList<StrukturkantVisning> Premisser,
    IReadOnlyList<string> Hull, AktorRettskildeTall Tall);

// ------------------------------------------------------------------------------------------------ Tjenesten

/// <summary>
/// [Ny, issue #441, Johanns avgjørelser 2026-10-30 og 2026-11-01] Den samlede aktørsiden: «hjemmel for aktøren» øverst, og de andre rettskildene
/// som relasjoner, én rad per rettskilde. Erstatter de sju kallene aktørsiden gjorde for det samme (navneformer, strukturkanter, ventende
/// kandidater, ansvarlig for, fastsatt av, where-used og kompetanse via klasse).
/// <para>
/// <b>Hjemmelen (regelen, [LÅST #441]):</b> (1) R <c>oppretter</c> til aktøren; (2) en navneform med <c>hjemmel_art</c>; (3) den gjeldende
/// navneformen når hjemmelen dens står i en kilde med disposisjonstype oppretting, navneendring eller omorganisering. Ellers er det et hull, og
/// det gjettes ikke inn noe i stedet (CLAUDE.md §8). En hjemmel der navnet bare står, er ikke hjemmel for at aktøren finnes.
/// </para>
/// <para>
/// <b>«Mest omtalt i (ikke hjemmel)»</b> teller bare direkte utsagn (kanter der aktøren er part) og taggede forekomster av navneformene (#441:
/// aldri fritekst, aldri treff via klasse). «Karasjok» i politidistriktforskriften er et stedsnavn (CLAUDE.md §0), og der er det ingen tagg.
/// </para>
/// </summary>
public sealed class AktorsideTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste, AktoridentitetTjeneste identitet)
{
    /// <summary>Navneformer som er forslag, eller gjeldende uten godkjenningsakse (før #385), og ikke avvist/arkivert.</summary>
    private IQueryable<BegrepEntitet> Navneformer(Guid virksomhetId) =>
        db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId && b.Entitetsstatus == "gjeldende"
                               && b.Godkjenning != Strukturkanter.Avvist);

    // ---------------------------------------------------------------- Hjemmelen

    /// <returns>Null hvis virksomheten ikke finnes.</returns>
    public async Task<AktorHjemmel?> HjemmelAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        var v = await db.Virksomheter.FirstOrDefaultAsync(x => x.Id == virksomhetId, ct);
        if (v is null) return null;

        var navneformer = await Navneformer(virksomhetId).ToListAsync(ct);
        var gjeldende = navneformer.Where(n => n.Navneformgrunn == "gjeldende").ToList();
        var medArt = navneformer.Where(n => n.HjemmelArt is not null && n.LovkildeId is not null).ToList();

        // (1) R oppretter. Målt i kopien 2026-11-05: 30 kanter, aldri mer enn én per aktør. Skulle det komme flere, velges den nyeste etter
        // gyldighet — det er opprettelsen av identiteten aktøren har nå — og regelen står her, ikke i en gjetning.
        var oppretter = (await db.Strukturkanter
                .Where(k => k.Kategori == Strukturkanter.Relasjon && k.Typekode == "oppretter" && k.TilVirksomhetId == virksomhetId)
                .ToListAsync(ct))
            .OrderByDescending(k => k.GyldigFra ?? DateOnly.MinValue).ThenBy(k => k.Id).FirstOrDefault();

        string? kilde = null;
        Guid? rettskildeId = null;
        string? eid = null;
        BegrepEntitet? navneform = null;
        BegrepEntitet? navnevedtak = null;
        if (oppretter is not null)
        {
            kilde = Hjemmelkilder.Oppretterkant;
            rettskildeId = oppretter.HjemmelRettskildeId;
            eid = oppretter.HjemmelEid;
            // Navneformen som godkjennes sammen med kanten: den gjeldende med hjemmel i samme vedtak, ellers den med hjemmel_art (navnevedtaket).
            var iSamme = gjeldende.Where(n => n.LovkildeId is not null && n.LovkildeId == oppretter.HjemmelRettskildeId).ToList();
            navneform = iSamme.Count == 1 ? iSamme[0] : null;
            if (navneform is null)
            {
                var gjeldendeMedArt = medArt.Where(n => n.Navneformgrunn == "gjeldende").ToList();
                if (gjeldendeMedArt.Count == 1) navnevedtak = navneform = gjeldendeMedArt[0];
            }
        }
        else
        {
            // (2) Navneform med hjemmel_art. Flere: den gjeldende; ellers ingen (et menneske avgjør på navneformfanen).
            var kandidat = medArt.Count == 1 ? medArt[0] : medArt.Where(n => n.Navneformgrunn == "gjeldende").ToList() is { Count: 1 } g ? g[0] : null;
            if (kandidat is not null)
            {
                kilde = Hjemmelkilder.HjemmelArt;
                navneform = kandidat;
            }
            else
            {
                // (3) Gjeldende navneform med hjemmel i en organiseringskilde.
                var ider = gjeldende.Where(n => n.LovkildeId is not null).Select(n => n.LovkildeId!.Value).Distinct().ToList();
                var organisering = await db.RettskildeDisposisjonstyper
                    .Where(d => ider.Contains(d.RettskildeId) && Hjemmelkilder.Organiseringstyper.Contains(d.Disposisjonstype))
                    .Select(d => d.RettskildeId).Distinct().ToListAsync(ct);
                var treff = gjeldende.Where(n => n.LovkildeId is { } l && organisering.Contains(l) && n.LovreferanseEid is not null).ToList();
                if (treff.Count == 1)
                {
                    kilde = Hjemmelkilder.Disposisjonstype;
                    navneform = treff[0];
                }
            }
            if (navneform is not null)
            {
                rettskildeId = navneform.LovkildeId;
                eid = navneform.LovreferanseEid;
            }
        }

        if (kilde is null)
        {
            var (mest, omtaltFinnes) = await MestOmtaltAsync(virksomhetId, ct);
            return new AktorHjemmel(virksomhetId, "mangler", null, null, null, null, null, null, null, null, [], null, null, null, null, false, false, [],
                AktorkoTjeneste.Hullmerknad, omtaltFinnes ? mest : [], await DetSomFinnesAsync(v, ct));
        }

        var rettskilde = rettskildeId is { } rid ? await db.Rettskilder.Where(r => r.Id == rid)
            .Select(r => new { r.Id, r.Tittel, r.Kortnavn, r.DokumentId, r.Ikrafttredelse }).FirstOrDefaultAsync(ct) : null;
        var typer = rettskildeId is { } rid2
            ? await db.RettskildeDisposisjonstyper.Where(d => d.RettskildeId == rid2).Select(d => d.Disposisjonstype).Distinct().ToListAsync(ct)
            : [];
        var sitat = rettskildeId is { } rid3 && eid is not null ? await SitatAsync(rid3, eid, ct) : null;

        var utsagn = rettskildeId is { } rid4
            ? await kanttjeneste.ByggVisningerAsync(await db.Strukturkanter
                .Where(k => (k.FraVirksomhetId == virksomhetId || k.TilVirksomhetId == virksomhetId) && k.HjemmelRettskildeId == rid4)
                .ToListAsync(ct), Kantnode.Virksomhet(virksomhetId), ct)
            : [];
        StrukturkantVisning? oppretterVisning = oppretter is null ? null
            : utsagn.FirstOrDefault(u => u.Id == oppretter.Id) ?? (await kanttjeneste.ByggVisningerAsync([oppretter], Kantnode.Virksomhet(virksomhetId), ct)).Single();

        var tittler = await RettskildetitlerAsync([navneform?.LovkildeId], ct);
        AktorHjemmelNavneform? Vis(BegrepEntitet? b) => b is null ? null : new AktorHjemmelNavneform(b.Id, b.Term, b.Navneformgrunn, b.Kunnskapsstatus,
            b.Godkjenning, b.OppdagelsesKilde, b.HjemmelArt, b.LovkildeId, b.LovkildeId is { } l ? tittler.GetValueOrDefault(l) : null, b.LovreferanseEid);

        var (artForslag, maVelge) = HjemmelArtForslag(kilde, navneform, typer);
        var venter = (navneform is not null && navneform.Godkjenning is not null && Strukturkanter.Ventende.Contains(navneform.Godkjenning))
                     || (oppretter is not null && Strukturkanter.Ventende.Contains(oppretter.Status));
        return new AktorHjemmel(virksomhetId, "hjemlet", kilde, rettskilde?.Id, rettskilde?.Tittel, rettskilde?.Kortnavn, rettskilde?.DokumentId, eid, sitat,
            oppretter?.GyldigFra ?? rettskilde?.Ikrafttredelse, typer.Order(StringComparer.Ordinal).ToList(), Vis(navneform), oppretterVisning,
            Vis(navnevedtak), artForslag, maVelge, venter,
            utsagn.OrderBy(u => u.Id == oppretter?.Id ? 0 : 1).ThenBy(u => u.Kategori, StringComparer.Ordinal)
                .ThenBy(u => u.HjemmelEid, StringComparer.Ordinal).ThenBy(u => u.Visningstekst, StringComparer.Ordinal).ToList(),
            null, [], []);
    }

    /// <summary>
    /// Hva «Godkjenn hjemmelen» setter som <c>hjemmel_art</c> når navneformen ikke har den. Har den det, beholdes den (de 13 navnevedtakene fra
    /// livsløpet). Oppretterkanten: oppretting. Disposisjonstypen: oppretting eller navneendring når bare én av dem står på kilden; bare
    /// omorganisering, eller begge, avgjøres ikke av regelen (et menneske velger).
    /// </summary>
    public static (string? Forslag, bool MaVelge) HjemmelArtForslag(string? kilde, BegrepEntitet? navneform, IReadOnlyCollection<string> typer)
    {
        if (navneform is null || navneform.LovreferanseEid is null) return (null, false);
        if (navneform.HjemmelArt is not null) return (navneform.HjemmelArt, false);
        if (kilde == Hjemmelkilder.Oppretterkant) return navneform.Navneformgrunn == "gjeldende" ? ("oppretting", false) : (null, false);
        var opprett = typer.Contains(Disposisjonstyper.Oppretting);
        var navn = typer.Contains(Disposisjonstyper.Navneendring);
        if (opprett && !navn) return ("oppretting", false);
        if (navn && !opprett) return ("navnevedtak", false);
        return (null, true);
    }

    /// <summary>Den første teksten i bestemmelsen: nodens egen tekst, ellers de underordnede nodene i rekkefølge. Null når noden ikke finnes.</summary>
    private async Task<string?> SitatAsync(Guid rettskildeId, string eid, CancellationToken ct)
    {
        var node = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId && n.Eid == eid && n.Entitetsstatus == "gjeldende")
            .Select(n => new { n.Id, n.Tekst }).FirstOrDefaultAsync(ct);
        if (node is null) return null;
        if (!string.IsNullOrWhiteSpace(node.Tekst)) return node.Tekst;
        var barn = await db.RettskildeNoder.Where(n => n.ParentNodeId == node.Id && n.Entitetsstatus == "gjeldende" && n.Tekst != null)
            .OrderBy(n => n.Sorteringsrekkefolge).Select(n => n.Tekst!).Take(3).ToListAsync(ct);
        return barn.Count == 0 ? null : string.Join(" ", barn);
    }

    private async Task<Dictionary<Guid, string>> RettskildetitlerAsync(IEnumerable<Guid?> ider, CancellationToken ct)
    {
        var liste = ider.Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        return liste.Count == 0 ? [] : await db.Rettskilder.Where(r => liste.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Kortnavn ?? r.Tittel, ct);
    }

    /// <summary>
    /// [LÅST, #441] Rettskildene med flest direkte utsagn (kanter der aktøren er part, med hjemmel i korpus) og taggede forekomster av navneformene.
    /// Fritekst og treff via klasse teller aldri. Står flere rettskilder likt øverst, vises alle (ingen velges).
    /// </summary>
    private async Task<(IReadOnlyList<MestOmtalt> Mest, bool Finnes)> MestOmtaltAsync(Guid virksomhetId, CancellationToken ct)
    {
        var utsagn = await db.Strukturkanter
            .Where(k => (k.FraVirksomhetId == virksomhetId || k.TilVirksomhetId == virksomhetId) && k.HjemmelRettskildeId != null)
            .Select(k => new { RettskildeId = k.HjemmelRettskildeId!.Value, k.HjemmelEid }).ToListAsync(ct);
        var tagger = (await new VirksomhetWhereUsedTjeneste(db).HentAsync(virksomhetId, ct)).NavneformForekomster;
        var perKilde = utsagn.Select(u => (u.RettskildeId, Eid: u.HjemmelEid, Utsagn: 1, Tagg: 0))
            .Concat(tagger.Select(t => (t.RettskildeId, Eid: (string?)t.NodeEid, Utsagn: 0, Tagg: 1)))
            .GroupBy(x => x.RettskildeId)
            .Select(g => (Id: g.Key, Utsagn: g.Sum(x => x.Utsagn), Tagg: g.Sum(x => x.Tagg),
                Eider: g.Where(x => x.Eid is not null).Select(x => x.Eid!).Distinct().ToList()))
            .ToList();
        if (perKilde.Count == 0) return ([], false);
        var maks = perKilde.Max(p => p.Utsagn + p.Tagg);
        var topp = perKilde.Where(p => p.Utsagn + p.Tagg == maks).ToList();
        var ider = topp.Select(p => p.Id).ToList();
        var kilder = await db.Rettskilder.Where(r => ider.Contains(r.Id))
            .Select(r => new { r.Id, r.Tittel, r.Kortnavn }).ToDictionaryAsync(r => r.Id, ct);
        var svar = new List<MestOmtalt>();
        foreach (var p in topp)
        {
            // Bestemmelsene i dokumentrekkefølge; sitatet er fra den første (visning, ikke en påstand om hvilken som er viktigst).
            var rekkefolge = await db.RettskildeNoder.Where(n => n.RettskildeId == p.Id && p.Eider.Contains(n.Eid) && n.Entitetsstatus == "gjeldende")
                .Select(n => new { n.Eid, n.Sorteringsrekkefolge }).ToListAsync(ct);
            var eider = p.Eider.OrderBy(e => rekkefolge.FirstOrDefault(r => r.Eid == e)?.Sorteringsrekkefolge ?? int.MaxValue)
                .ThenBy(e => e, StringComparer.Ordinal).ToList();
            var forste = eider.FirstOrDefault();
            var r = kilder.GetValueOrDefault(p.Id);
            svar.Add(new MestOmtalt(p.Id, r?.Tittel ?? "(ukjent rettskilde)", r?.Kortnavn, p.Utsagn, p.Tagg, eider, forste,
                forste is null ? null : await SitatAsync(p.Id, forste, ct)));
        }
        return (svar.OrderBy(m => m.Tittel, StringComparer.Ordinal).ToList(), true);
    }

    /// <summary>«Det som finnes» under hullet: klassene aktøren er utledet medlem av (#384), og registeret (orgnr, organisasjonsform og kanter med
    /// kilde utenfor korpus av typen register).</summary>
    private async Task<IReadOnlyList<DetSomFinnes>> DetSomFinnesAsync(Virksomhet v, CancellationToken ct)
    {
        var svar = new List<DetSomFinnes>();
        var klassemedlemskap = new KlassemedlemskapTjeneste(db, kanttjeneste);
        foreach (var klasseId in (await klassemedlemskap.KlasserMedRegelAsync(ct)).Keys)
        {
            var utledet = await klassemedlemskap.UtledAsync(klasseId, v.Id, ct);
            var m = utledet?.Medlemmer.FirstOrDefault();
            if (utledet is null || m is null) continue;
            var klasse = await db.Begreper.Where(b => b.Id == klasseId).Select(b => new { b.LovkildeId, b.LovreferanseEid }).FirstAsync(ct);
            var tittel = klasse.LovkildeId is { } l ? (await RettskildetitlerAsync([l], ct)).GetValueOrDefault(l) : null;
            svar.Add(new DetSomFinnes("klasse", $"Medlem av klassen «{utledet.Term}»", klasseId, klasse.LovkildeId, tittel, klasse.LovreferanseEid,
                utledet.KlassensKunnskapsstatus, utledet.Kunnskapsstatus, !m.Validert));
        }
        var registerkanter = await db.Strukturkanter
            .Where(k => k.FraVirksomhetId == v.Id && k.HjemmelRettskildeId == null && k.KildeUtenforKorpusType == Strukturkanter.Register)
            .ToListAsync(ct);
        foreach (var k in await kanttjeneste.ByggVisningerAsync(registerkanter, Kantnode.Virksomhet(v.Id), ct))
        {
            svar.Add(new DetSomFinnes("register", k.Visningstekst + (k.KildeUtenforKorpusTekst is { } t ? $" ({t})" : ""), k.Til?.Id, null, null, null,
                null, k.Kunnskapsstatus, Strukturkanter.Ventende.Contains(k.Status)));
        }
        if (v.Organisasjonsnummer is not null)
        {
            svar.Add(new DetSomFinnes("register",
                $"Enhetsregisteret: organisasjonsnummer {v.Organisasjonsnummer}" + (v.OrganisasjonsformKode is { } f ? $", organisasjonsform {f}" : ""),
                null, null, null, null, null, null, false));
        }
        return svar;
    }

    // ---------------------------------------------------------------- Godkjenningen

    /// <summary>
    /// [#441, Johann 2026-10-30] «Godkjenn hjemmelen»: navneformen og R oppretter godkjennes i ÉN transaksjon, og <c>hjemmel_art</c> settes på
    /// navneformen når den mangler (<see cref="HjemmelArtForslag"/>, eller <paramref name="hjemmelArt"/> når regelen ikke avgjør det). Feiler et
    /// steg (for eksempel fordi aktøren alt har en annen godkjent gjeldende navneform), rulles alt tilbake.
    /// </summary>
    /// <returns>Null hvis virksomheten ikke finnes.</returns>
    /// <exception cref="ArgumentException">Hullet, ingenting å godkjenne, eller ugyldig/manglende <paramref name="hjemmelArt"/>.</exception>
    public async Task<HjemmelGodkjent?> GodkjennHjemmelAsync(Guid virksomhetId, string? hjemmelArt, string bruker, CancellationToken ct = default)
    {
        var h = await HjemmelAsync(virksomhetId, ct);
        if (h is null) return null;
        if (h.Status != "hjemlet") throw new ArgumentException("Hjemmel for aktøren mangler. Det er ingenting å godkjenne.");
        if (!h.Venter) throw new ArgumentException("Hjemmelen er alt godkjent.");
        if (hjemmelArt is not null && hjemmelArt is not ("oppretting" or "navnevedtak"))
        {
            throw new ArgumentException($"Ukjent hjemmel_art «{hjemmelArt}». Gyldige verdier: oppretting, navnevedtak.");
        }
        if (h.MaVelgeHjemmelArt && hjemmelArt is null)
        {
            throw new ArgumentException("Regelen avgjør ikke om hjemmelen er oppretting eller navnevedtak. Velg en av dem.");
        }

        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var navneformGodkjent = false;
        var kantGodkjent = false;
        string? art = null;
        if (h.Navneform is { } nf)
        {
            var b = await db.Begreper.FirstAsync(x => x.Id == nf.Id, ct);
            if (b.Godkjenning is not null && Strukturkanter.Ventende.Contains(b.Godkjenning))
            {
                await identitet.GodkjennNavneformAsync(b.Id, bruker, ct);
                navneformGodkjent = true;
            }
            if (b.HjemmelArt is null && b.LovreferanseEid is not null)
            {
                art = hjemmelArt ?? h.HjemmelArtForslag;
                if (art is not null)
                {
                    b.HjemmelArt = art;
                    b.SistEndretAv = bruker;
                    b.SistEndretTidspunkt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
            art = b.HjemmelArt;
        }
        if (h.Oppretterkant is { } k && Strukturkanter.Ventende.Contains(k.Status))
        {
            await kanttjeneste.GodkjennAsync(k.Id, bruker, ct);
            kantGodkjent = true;
        }
        if (tx is not null) await tx.CommitAsync(ct);
        return new HjemmelGodkjent(h.Navneform?.Id, navneformGodkjent, h.Oppretterkant?.Id, kantGodkjent, art);
    }

    /// <summary>[#441] «Godkjenn alle N» i én rad: de ventende utsagnene der aktøren er part og hjemmelen står i rettskilden — aldri andre aktørers
    /// forslag i samme rettskilde (det gjør <see cref="StrukturkantTjeneste.GodkjennAlleForHjemmelAsync"/>). Forslag med hjemmel i en merknad
    /// hoppes over (#419). Én transaksjon.</summary>
    public async Task<(int Godkjent, int HoppetOver)> GodkjennAlleIRettskildenAsync(Guid virksomhetId, Guid rettskildeId, string bruker, CancellationToken ct = default)
    {
        var aktuelle = db.Strukturkanter.Where(k => (k.FraVirksomhetId == virksomhetId || k.TilVirksomhetId == virksomhetId)
                                                    && k.HjemmelRettskildeId == rettskildeId && Strukturkanter.Ventende.Contains(k.Status));
        var ider = await aktuelle.Select(k => k.Id).ToListAsync(ct);
        var merknad = await kanttjeneste.MedMerknadshjemmel(aktuelle).Select(k => k.Id).ToListAsync(ct);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        foreach (var id in ider.Except(merknad)) await kanttjeneste.GodkjennAsync(id, bruker, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return (ider.Count - merknad.Count, merknad.Count);
    }

    // ---------------------------------------------------------------- Rettskildene

    /// <returns>Null hvis virksomheten ikke finnes.</returns>
    public async Task<AktorRettskilder?> RettskilderAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        var v = await db.Virksomheter.FirstOrDefaultAsync(x => x.Id == virksomhetId, ct);
        if (v is null) return null;
        var hjemmel = await HjemmelAsync(virksomhetId, ct);
        var hjemmelKilde = hjemmel?.Status == "hjemlet" ? hjemmel.RettskildeId : null;

        // Direkte utsagn: samme sett som fanen «Etter kategori» (alle kanter der aktøren er part, ikke avviste).
        var kanter = await db.Strukturkanter.Where(k => k.FraVirksomhetId == virksomhetId || k.TilVirksomhetId == virksomhetId).ToListAsync(ct);
        var visninger = await kanttjeneste.ByggVisningerAsync(kanter, Kantnode.Virksomhet(virksomhetId), ct);

        var ansvarlig = (await AktorRettskildeoppslag.AnsvarligForAsync(db, virksomhetId, ct)).ToDictionary(r => r.Id);
        var fastsatt = (await AktorRettskildeoppslag.FastsattAvAsync(db, virksomhetId, ct)).ToDictionary(r => r.Id);
        var tagger = (await new VirksomhetWhereUsedTjeneste(db).HentAsync(virksomhetId, ct)).NavneformForekomster
            .GroupBy(t => t.RettskildeId).ToDictionary(g => g.Key, g => g.Count());
        var navnetreff = await db.VirksomhetKandidater.Where(k => k.VirksomhetId == virksomhetId && k.Status == "Venter")
            .GroupBy(k => k.RettskildeId).Select(g => new { g.Key, Antall = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Antall, ct);
        var navneformerVenter = (await Navneformer(virksomhetId)
                .Where(b => b.LovkildeId != null && b.Godkjenning != null && Strukturkanter.Ventende.Contains(b.Godkjenning))
                .Select(b => b.LovkildeId!.Value).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        // Via klasse: kompetansen (#418) og, for en kommune, pliktene via registrert eller utledet medlemskap (#353/#384). Klasser uten avgjort
        // medlemskap gir ikke treff (de står i hullene).
        var viaK = await new KompetanseViaKlasseTjeneste(db, kanttjeneste).ForVirksomhetAsync(virksomhetId, ct);
        var via = viaK?.Treff.ToList() ?? [];
        var premisser = (viaK?.Premisser ?? []).ToDictionary(p => p.Id);
        var hull = (viaK?.Hull ?? []).ToList();
        if (v.Kommunenummer is { } knr && v.Aktortype == "rettssubjekt")
        {
            var plikter = await new OmradeOppslagTjeneste(db, kanttjeneste).PlikterForKommunenummerAsync(knr, null, ct);
            if (plikter is not null && plikter.Kommunevirksomhet?.Id == virksomhetId)
            {
                var pliktTreff = plikter.Plikter.Where(p => p.Grunnlag == "medlem_av").ToList();
                var premissIder = pliktTreff.SelectMany(p => p.Premisser).Distinct().Where(id => !premisser.ContainsKey(id)).ToList();
                var premissKanter = await kanttjeneste.GjeldendeInklAvviste.Where(k => premissIder.Contains(k.Id)).ToListAsync(ct);
                foreach (var p in await kanttjeneste.ByggVisningerAsync(premissKanter, null, ct)) premisser[p.Id] = p;
                foreach (var p in pliktTreff)
                {
                    var kjede = p.Premisser.Where(premisser.ContainsKey).Select(id => premisser[id]).ToList();
                    var svakest = SvakestePremiss.Finn(kjede.Select(k => (k.Id, k.Kunnskapsstatus)));
                    var klassenode = p.Retning == OmradeOppslagTjeneste.KommuneSkal ? p.Kant.Fra : p.Kant.Til!;
                    via.Add(new KompetanseViaKlasseTreff(p.Kant, p.Retning == OmradeOppslagTjeneste.KommuneSkal ? "har" : "overfor", [klassenode],
                        SvakestePremiss.SvarStatus(svakest?.Kunnskapsstatus), p.Premisser, svakest?.Id, svakest?.Kunnskapsstatus,
                        kjede.Any(k => k.Status != Strukturkanter.Godkjent)));
                }
                hull.AddRange(plikter.Hull);
            }
        }

        // Radene.
        var direkte = visninger.Where(u => u.HjemmelRettskildeId is null || u.HjemmelRettskildeId != hjemmelKilde).ToList();
        var iHjemmelen = visninger.Count - direkte.Count;
        var kildeIder = direkte.Where(u => u.HjemmelRettskildeId is not null).Select(u => u.HjemmelRettskildeId!.Value)
            .Concat(ansvarlig.Keys).Concat(fastsatt.Keys).Concat(tagger.Keys).Concat(navnetreff.Keys).Concat(navneformerVenter.Keys)
            .Where(id => id != hjemmelKilde).Distinct().ToList();
        var viaKildeIder = via.Where(t => t.Kant.HjemmelRettskildeId is not null).Select(t => t.Kant.HjemmelRettskildeId!.Value).Distinct().ToList();
        var alleIder = kildeIder.Concat(viaKildeIder).Distinct().ToList();
        var kilder = await db.Rettskilder.Where(r => alleIder.Contains(r.Id))
            .Select(r => new { r.Id, r.Tittel, r.Kortnavn, r.Kildetype, r.DokumentId }).ToDictionaryAsync(r => r.Id, ct);

        static (int K, int P, int F, int A) Tell(IEnumerable<string> kategorier)
        {
            var l = kategorier.ToList();
            return (l.Count(k => k == Strukturkanter.Kompetanse), l.Count(k => k == Strukturkanter.Plikt), l.Count(k => k == Strukturkanter.Oppgave),
                l.Count(k => k is not (Strukturkanter.Kompetanse or Strukturkanter.Plikt or Strukturkanter.Oppgave)));
        }
        static IReadOnlyList<StrukturkantVisning> Sorter(IEnumerable<StrukturkantVisning> u) =>
            u.OrderBy(x => Strukturkanter.Ventende.Contains(x.Status) ? 0 : 1).ThenBy(x => x.Kategori, StringComparer.Ordinal)
                .ThenBy(x => x.HjemmelEid, StringComparer.Ordinal).ThenBy(x => x.Visningstekst, StringComparer.Ordinal).ToList();

        var rader = new List<AktorRettskilderad>();
        foreach (var id in kildeIder)
        {
            var r = kilder.GetValueOrDefault(id);
            var u = direkte.Where(x => x.HjemmelRettskildeId == id).ToList();
            var (k, p, f, a) = Tell(u.Select(x => x.Kategori));
            var nt = navnetreff.GetValueOrDefault(id);
            var nv = navneformerVenter.GetValueOrDefault(id);
            rader.Add(new AktorRettskilderad("direkte", id, r?.Tittel ?? "(ukjent rettskilde)", r?.Kortnavn, r?.Kildetype, r?.DokumentId, null,
                ansvarlig.ContainsKey(id), fastsatt.ContainsKey(id), tagger.GetValueOrDefault(id), nt, nv, k, p, f, a,
                u.Count(x => Strukturkanter.Ventende.Contains(x.Status)) + nt + nv, Sorter(u), []));
        }
        foreach (var g in direkte.Where(u => u.HjemmelRettskildeId is null).GroupBy(u => u.KildeUtenforKorpusType ?? ""))
        {
            var (k, p, f, a) = Tell(g.Select(x => x.Kategori));
            rader.Add(new AktorRettskilderad("direkte", null, "Kilde utenfor korpus", null, null, null, g.Key == "" ? null : g.Key,
                false, false, 0, 0, 0, k, p, f, a, g.Count(x => Strukturkanter.Ventende.Contains(x.Status)), Sorter(g), []));
        }
        foreach (var g in via.GroupBy(t => t.Kant.HjemmelRettskildeId))
        {
            var r = g.Key is { } id ? kilder.GetValueOrDefault(id) : null;
            var (k, p, f, a) = Tell(g.Select(t => t.Kant.Kategori));
            rader.Add(new AktorRettskilderad("via_klasse", g.Key, r?.Tittel ?? (g.Key is null ? "Kilde utenfor korpus" : "(ukjent rettskilde)"), r?.Kortnavn,
                r?.Kildetype, r?.DokumentId, null, false, false, 0, 0, 0, k, p, f, a,
                g.Count(t => Strukturkanter.Ventende.Contains(t.Kant.Status)), [],
                g.OrderBy(t => t.Retning == "har" ? 0 : 1).ThenBy(t => t.Kant.Kategori, StringComparer.Ordinal)
                    .ThenBy(t => t.Kant.Visningstekst, StringComparer.Ordinal).ToList()));
        }

        // [LÅST, #441] Rader med utsagn først; så flest utsagn, flest som venter, og tittelen. Direkte før via klasse.
        int Utsagn(AktorRettskilderad r) => r.Kompetanse + r.Plikt + r.Oppgave + r.Annet;
        var sortert = rader
            .OrderBy(r => r.Gruppe == "direkte" ? 0 : 1)
            .ThenBy(r => Utsagn(r) > 0 ? 0 : 1)
            .ThenByDescending(Utsagn).ThenByDescending(r => r.Venter)
            .ThenBy(r => r.Tittel, StringComparer.Ordinal).ToList();

        var direkteRader = sortert.Where(r => r.Gruppe == "direkte").ToList();
        var tall = new AktorRettskildeTall(
            Rettskilder: sortert.Count,
            MedUtsagn: sortert.Count(r => Utsagn(r) > 0),
            Venter: sortert.Count(r => r.Venter > 0),
            AnsvarligDepartement: direkteRader.Count(r => r.AnsvarligDepartement),
            FastsattAv: direkteRader.Count(r => r.FastsattAv),
            NavnetTagget: direkteRader.Count(r => r.NavnetTagget > 0),
            Direkte: direkteRader.Count,
            ViaKlasse: sortert.Count(r => r.Gruppe == "via_klasse"),
            Kanter: visninger.Count,
            KanterIRadene: direkteRader.Sum(Utsagn),
            KanterIHjemmelen: iHjemmelen);
        return new AktorRettskilder(virksomhetId, hjemmelKilde, sortert,
            premisser.Values.OrderBy(p => p.Id).ToList(), hull.Distinct().ToList(), tall);
    }
}

/// <summary>
/// [Ny, issue #441, flyttet fra <c>RettskildeRepository</c> i Api] «Ansvarlig for» og «fastsatt av» for én aktør — ett sted for regelen, brukt av
/// de to gamle endepunktene og av aktørsidens rettskildeliste. Uendret logikk: eksakt (case-insensitivt) navnetreff, ingen fuzzy-matching.
/// </summary>
public static class AktorRettskildeoppslag
{
    /// <summary>Gjeldende rettskilder der Lovdatas «Ansvarlig departement» er registernavnet. Lovdatas felt er en sekundær kilde (#441): et merke,
    /// aldri et utsagn.</summary>
    public static async Task<List<RettskildeEntitet>> AnsvarligForAsync(RegelIdeDbContext db, Guid virksomhetId, CancellationToken ct = default)
    {
        var navn = await db.Virksomheter.Where(v => v.Id == virksomhetId).Select(v => v.Navn).FirstOrDefaultAsync(ct);
        if (navn is null) return [];
        var navnLower = navn.ToLower();
        return await db.Rettskilder
            .Where(r => r.Entitetsstatus == "gjeldende" && r.AnsvarligDepartement != null
                        && r.AnsvarligDepartement!.Any(d => d.ToLower() == navnLower))
            .ToListAsync(ct);
    }

    /// <summary>Gjeldende rettskilder der «Fastsatt av»-frasen peker på aktøren, matchet mot registernavnet og navneformene (issue #215).</summary>
    public static async Task<List<RettskildeEntitet>> FastsattAvAsync(RegelIdeDbContext db, Guid virksomhetId, CancellationToken ct = default)
    {
        var registernavn = await db.Virksomheter.Where(v => v.Id == virksomhetId).Select(v => v.Navn).FirstOrDefaultAsync(ct);
        if (registernavn is null) return [];
        var navneformer = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId == virksomhetId)
            .Select(b => b.Term).ToListAsync(ct);
        var navn = navneformer.Append(registernavn).Select(n => n.ToLower()).Distinct().ToList();
        return await db.Rettskilder
            .Where(r => r.Entitetsstatus == "gjeldende" && r.FastsattAvOrgannavn != null && navn.Contains(r.FastsattAvOrgannavn!.ToLower()))
            .ToListAsync(ct);
    }
}
