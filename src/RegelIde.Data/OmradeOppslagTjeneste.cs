using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #312] Ett område slik oppslaget viser det.</summary>
public sealed record OmradeVisning(Guid Id, string Navn, string? Omradetype, string? Omradekode);

/// <summary>[Ny, issue #312] En aktør med ansvarsområde, og HVILKET område kanten lander på (kommunen selv, fylket,
/// lagdømmet …) — slik at «hvorfor er Statsforvalteren i Troms og Finnmark med?» kan besvares: via fylket Finnmark.</summary>
public sealed record AnsvarligAktor(Guid VirksomhetId, string Navn, string Typekode, OmradeVisning Via, Guid KantId, bool Forslag = false);

/// <summary>
/// [Ny, issue #312] Én rubrikk i svaret («tingrett»), med kandidatene. <see cref="Entydig"/> er true bare når det
/// er nøyaktig én — ved null eller flere velges ingenting (CLAUDE.md §8), og kalleren viser «mangler» eller
/// «ikke entydig» med kandidatene (samme regel #314 skal bruke).
/// </summary>
/// <param name="Forslag">[Ny, #312, Johanns beslutning 2026-10-08] Per kandidat: true når svaret hviler på minst én
/// kant som ennå er et FORSLAG (domstolkantene fra inndelingsforskriften, til de er godkjent). Vises, telles ikke bort.</param>
public sealed record Tilhorighetsrubrikk(string Rubrikk, IReadOnlyList<string> Kandidater, IReadOnlyList<Guid> Ider, IReadOnlyList<bool> Forslag)
{
    public bool Entydig => Ider.Count == 1;
    public string Status => Ider.Count switch { 0 => "mangler", 1 => "entydig", _ => "ikke_entydig" };
}

/// <summary>[Ny, issue #312, AC5] Svaret på «gitt kommune X → fylke, tingrett, lagsogn, lagdømme, lagmannsrett,
/// statsforvalter, RHF».</summary>
public sealed record KommuneTilhorighet(
    OmradeVisning Kommune,
    IReadOnlyList<Tilhorighetsrubrikk> Rubrikker,
    IReadOnlyList<OmradeVisning> Overordnede,
    IReadOnlyList<AnsvarligAktor> Ansvarlige);

/// <summary>[Ny, issue #353] Motparten til en plikt sett fra én kommune. <see cref="Status"/>: <c>konkret</c> (til er en
/// virksomhet) | <c>entydig</c> (klasse/rolle løst til ett medlem via område) | <c>ikke_entydig</c> | <c>mangler</c> |
/// <c>ikke_angitt</c> (teksten sier ikke hvem). <see cref="Hull"/> sier hvorfor når den ikke er løst.</summary>
public sealed record PliktMotpart(string Status, IReadOnlyList<string> Kandidater, IReadOnlyList<Guid> Ider, string? Hull);

/// <summary>[Ny, issue #353] Én plikt som gjelder (eller kan gjelde) kommunen. <see cref="Grunnlag"/>: <c>direkte</c> |
/// <c>medlem_av</c> | <c>klasse_uten_registrert_medlemskap</c> (da sier <see cref="GrunnlagHull"/> hvorfor det ikke er avgjort).</summary>
public sealed record PliktTreff(StrukturkantVisning Kant, string Grunnlag, string? GrunnlagHull, PliktMotpart Motpart)
{
    /// <summary>[Ny, issue #353] <c>kommunen_skal</c> (kommunen er pliktsubjektet) | <c>overfor_kommunen</c> (andre har plikten
    /// overfor kommunen). <see cref="Grunnlag"/> og <see cref="GrunnlagHull"/> gjelder kommunens ende; <see cref="Motpart"/> den andre.</summary>
    public string Retning { get; init; } = OmradeOppslagTjeneste.KommuneSkal;
}

/// <summary>[Ny, issue #355] Én K overprøving/anke-kant som gjelder domstolen: hvorfor den gjelder (<see cref="Grunnlag"/>:
/// <c>direkte</c> | <c>medlem_av</c> | <c>klasse_uten_registrert_medlemskap</c>, da med <see cref="GrunnlagHull"/>) og hvilken instans
/// den gir for nettopp denne domstolen (<see cref="Instans"/>, løst via område).</summary>
public sealed record AnkeinstansTreff(StrukturkantVisning Kant, string Grunnlag, string? GrunnlagHull, PliktMotpart Instans);

/// <summary>[Ny, issue #355 AC4] Svaret på «hvem er ankeinstans for X?». <see cref="Status"/> (entydig | ikke_entydig | mangler) og
/// <see cref="Kandidater"/> regnes bare av kanter der domstolen er SIKKERT på til-siden (direkte eller registrert medlemskap); kanter
/// til en klasse uten registrert medlemskap står i <see cref="Kanter"/> med hullet, men teller ikke.</summary>
public sealed record AnkeinstansSvar(
    KantnodeVisning Domstol, string Status, IReadOnlyList<string> Kandidater, IReadOnlyList<Guid> Ider,
    IReadOnlyList<AnkeinstansTreff> Kanter, IReadOnlyList<OmradeVisning> Omrader, IReadOnlyList<string> Hull);

/// <summary>[Ny, issue #353 AC5] Svaret på «hvem har kommune X (samarbeids)plikt med?».</summary>
public sealed record KommunePlikter(
    OmradeVisning Kommune, KantnodeVisning? Kommunevirksomhet, string? Typekode, IReadOnlyList<PliktTreff> Plikter, IReadOnlyList<string> Hull);

/// <summary>
/// [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08, AC5, docs/32 S6/S8] Oppslaget «gitt en kommune:
/// hvilket fylke, hvilken tingrett, hvilket lagsogn og lagdømme, hvilken lagmannsrett, statsforvalter og RHF?» —
/// beregnet fra strukturkantene, ikke lagret.
/// <para>
/// <b>Algoritmen.</b> (1) Alle områder kommunen inngår i: <c>O bestar_av</c>-kanter oppover, transitivt (fylke,
/// lagsogn → lagdømme, fylke → helseregion). [ENDRET, #345] Lagsognet nås gjennom tingretten: kommune ← tingrett
/// (<c>A har_ansvarsomrade</c>) → lagsogn (<c>A sogner_til</c>), se <see cref="AvledetGjennomSognerTilAsync"/>. (2) Alle aktører med <c>A har_ansvarsomrade</c> til kommunen eller et av
/// disse områdene. (3) Rubrikkene velges av OMRÅDETYPEN kanten lander på — ikke av navnet på aktøren:
/// </para>
/// <list type="bullet">
/// <item>fylke / lagsogn / lagdømme / helseregion = overordnede områder av den typen;</item>
/// <item>kommune (rettssubjekt) = aktøren med ansvarsområde til kommunen og samme <see cref="Virksomhet.Kommunenummer"/>;</item>
/// <item>tingrett = de ANDRE aktørene med ansvarsområde direkte til kommunen;</item>
/// <item>lagmannsrett = aktører med ansvarsområde til lagdømmet; statsforvalter = til fylket; RHF = til helseregionen.</item>
/// </list>
/// <para>
/// Bare gjeldende kanter teller (<see cref="StrukturkantTjeneste.FiltrerGjeldendeAsync"/>). [ENDRET, #312, Johanns
/// beslutning 2026-10-08] Forslag teller MED, men hver kandidat er merket <c>Forslag</c> når svaret hviler på en
/// ikke-godkjent kant — domstolkantene er forslag til de er godkjent, og et oppslag som skjulte dem ville sagt «mangler».
/// Rubrikkreglene forutsetter at registeret er det eneste som gir slike kanter i dag. Får en fylkeskommune senere
/// <c>har_ansvarsomrade</c> til fylket, havner den i statsforvalter-rubrikken — da må rubrikken avgrenses (f.eks.
/// mot klassen «statsforvalter», #298). Det er bevisst ikke gjort nå, fordi ingen slik kant finnes (målt 2026-10-08).
/// </para>
/// </summary>
public sealed class OmradeOppslagTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste)
{
    public async Task<KommuneTilhorighet?> ForKommunenummerAsync(string kommunenummer, CancellationToken ct = default)
    {
        var id = await db.Begreper
            .Where(b => b.Begrepskategori == Nodetyper.Omrade && b.Omradetype == Omradetyper.Kommune && b.Omradekode == kommunenummer
                        && b.Entitetsstatus == "gjeldende" && b.GyldigTil == null)
            .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        return id is null ? null : await ForKommuneAsync(id.Value, ct);
    }

    /// <returns>Null hvis id-en ikke er et gjeldende kommuneområde.</returns>
    public async Task<KommuneTilhorighet?> ForKommuneAsync(Guid kommuneOmradeId, CancellationToken ct = default)
    {
        var kommune = await db.Begreper.FirstOrDefaultAsync(b => b.Id == kommuneOmradeId && b.Begrepskategori == Nodetyper.Omrade
                                                                 && b.Omradetype == Omradetyper.Kommune && b.Entitetsstatus == "gjeldende", ct);
        if (kommune is null) return null;

        // (1) Overordnede områder, transitivt via O-kanter (til = barnet, fra = forelderen).
        var oKanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Omradesammensetning
                        && k.FraBegrepId != null && k.TilBegrepId != null)
            .ToListAsync(ct), ct);
        // [ENDRET, issue #345, Johann 2026-10-08] Foreldrekantene er O-kantene PLUSS kantene avledet gjennom en aktør
        // som «sogner til» et område: tingretten har ansvarsområde til kommunen (§§ 2–9) og sogner til lagsognet
        // (§§ 11–16), så kommunen inngår i lagsognet. Kjeden er kommune ← tingrett → lagsogn → lagdømme. Ingenting av
        // dette lagres som kant lagsogn → kommune; det er utledet her fra det forskriften faktisk sier.
        var foreldrekanter = oKanter.Select(k => (Barn: k.TilBegrepId!.Value, Forelder: k.FraBegrepId!.Value, Validert: k.Status == "validert"))
            .Concat(await AvledetGjennomSognerTilAsync(ct)).ToList();
        var foreldre = foreldrekanter.GroupBy(k => k.Barn).ToDictionary(g => g.Key, g => g.Select(k => k.Forelder).ToList());
        var overordnedeIder = new List<Guid>();
        var ko = new Queue<Guid>([kommune.Id]);
        var sett = new HashSet<Guid> { kommune.Id };
        while (ko.Count > 0)
        {
            foreach (var f in foreldre.GetValueOrDefault(ko.Dequeue()) ?? [])
            {
                if (!sett.Add(f)) continue;
                overordnedeIder.Add(f);
                ko.Enqueue(f);
            }
        }
        // [Ny, #312] Områder som bare nås via et forslag: samme traversering over bare validerte kanter.
        var valideredeForeldre = foreldrekanter.Where(k => k.Validert).GroupBy(k => k.Barn)
            .ToDictionary(g => g.Key, g => g.Select(k => k.Forelder).ToList());
        var validertSett = new HashSet<Guid> { kommune.Id };
        var vko = new Queue<Guid>([kommune.Id]);
        while (vko.Count > 0)
        {
            foreach (var f in valideredeForeldre.GetValueOrDefault(vko.Dequeue()) ?? [])
            {
                if (validertSett.Add(f)) vko.Enqueue(f);
            }
        }
        var omrader = await db.Begreper.Where(b => sett.Contains(b.Id))
            .Select(b => new OmradeVisning(b.Id, b.Term, b.Omradetype, b.Omradekode)).ToDictionaryAsync(o => o.Id, ct);
        var overordnede = overordnedeIder.Select(i => omrader[i]).ToList();

        // (2) Aktører med ansvarsområde til kommunen eller et overordnet område.
        var aKanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade"
                        && k.FraVirksomhetId != null && k.TilBegrepId != null && sett.Contains(k.TilBegrepId.Value))
            .ToListAsync(ct), ct);
        var visninger = (await kanttjeneste.ByggVisningerAsync(aKanter, perspektiv: null, ct)).ToDictionary(v => v.Id);
        var ansvarlige = aKanter
            .Select(k => new AnsvarligAktor(k.FraVirksomhetId!.Value, visninger[k.Id].Fra.Navn, k.Typekode, omrader[k.TilBegrepId!.Value], k.Id,
                k.Status != "validert" || !validertSett.Contains(k.TilBegrepId!.Value)))
            .OrderBy(a => a.Via.Omradetype == Omradetyper.Kommune ? 0 : 1).ThenBy(a => a.Navn, StringComparer.Ordinal)
            .ToList();

        // (3) Rubrikkene.
        var aktorIder = ansvarlige.Select(a => a.VirksomhetId).Distinct().ToList();
        var kommunenummer = await db.Virksomheter.Where(v => aktorIder.Contains(v.Id)).Select(v => new { v.Id, v.Kommunenummer })
            .ToDictionaryAsync(v => v.Id, v => v.Kommunenummer, ct);
        bool ErKommunensEgen(AnsvarligAktor a) =>
            a.Via.Id == kommune.Id && kommune.Omradekode is not null && kommunenummer.GetValueOrDefault(a.VirksomhetId) == kommune.Omradekode;

        Tilhorighetsrubrikk Omr(string rubrikk, string type)
        {
            var o = overordnede.Where(x => x.Omradetype == type).OrderBy(x => x.Navn, StringComparer.Ordinal).ToList();
            return new Tilhorighetsrubrikk(rubrikk, o.Select(x => x.Navn).ToList(), o.Select(x => x.Id).ToList(),
                o.Select(x => !validertSett.Contains(x.Id)).ToList());
        }
        Tilhorighetsrubrikk Akt(string rubrikk, Func<AnsvarligAktor, bool> filter)
        {
            var a = ansvarlige.Where(filter).GroupBy(x => x.VirksomhetId)
                .Select(g => g.OrderBy(x => x.Forslag).First()).OrderBy(x => x.Navn, StringComparer.Ordinal).ToList();
            return new Tilhorighetsrubrikk(rubrikk, a.Select(x => x.Navn).ToList(), a.Select(x => x.VirksomhetId).ToList(),
                a.Select(x => x.Forslag).ToList());
        }

        var rubrikker = new List<Tilhorighetsrubrikk>
        {
            Akt("kommune", ErKommunensEgen),
            Omr("fylke", Omradetyper.Fylke),
            Akt("tingrett", a => a.Via.Id == kommune.Id && !ErKommunensEgen(a)),
            Omr("lagsogn", Omradetyper.Lagsogn),
            Omr("lagdømme", Omradetyper.Lagdomme),
            Akt("lagmannsrett", a => a.Via.Omradetype == Omradetyper.Lagdomme),
            Akt("statsforvalter", a => a.Via.Omradetype == Omradetyper.Fylke),
            Omr("helseregion", Omradetyper.Helseregion),
            Akt("RHF", a => a.Via.Omradetype == Omradetyper.Helseregion),
        };
        return new KommuneTilhorighet(omrader[kommune.Id], rubrikker, overordnede, ansvarlige);
    }

    // ---------------- [Ny, issue #353] S6: «Hvem har kommune X samarbeidsplikt med?» ----------------

    /// <summary>
    /// [Ny, issue #353 AC5, docs/32 S6] Pliktene (P-kanter) som gjelder kommunen, med motparten løst til konkrete aktører der
    /// strukturen avgjør det — og et SYNLIG HULL der den ikke gjør det. Beregnet, ikke lagret (tilpasning punkt 5: «Kommunen skal
    /// samarbeide med det regionale helseforetaket i helseregionen» er ÉN kant mellom klasser; parene regnes ut via område, slik
    /// domstolkjeden gjør i #345).
    /// <para>
    /// <b>Hvilke plikter (fra-siden):</b> (1) <c>direkte</c> — fra = kommunens egen virksomhet (rubrikken «kommune» i
    /// <see cref="ForKommuneAsync"/>); (2) <c>medlem_av</c> — fra = en klasse kommunen er REGISTRERT medlem av (M, transitivt);
    /// (3) <c>klasse_uten_registrert_medlemskap</c> — fra = en klasse uten ett eneste registrert medlem («kommunen» i en lov er en
    /// intensjonal klasse: alle kommuner, men medlemskapet er ikke lastet). Den tredje kan gjelde kommunen, men avgjøres ikke her
    /// (CLAUDE.md §8) — den listes med hullet, ikke tas bort og ikke regnes som sikker.
    /// </para>
    /// <para>
    /// <b>Motparten (til-siden):</b> en virksomhet er <c>konkret</c>. En klasse/rolle løses som Statsforvalter-eksempelet i docs/33
    /// §4.2: medlemmene (M <c>medlem_av</c> / I <c>innehar</c>) hvis <c>A har_ansvarsomrade</c> dekker kommunen eller et område den
    /// ligger i. Nøyaktig ett → <c>entydig</c>; flere → <c>ikke_entydig</c> (ingen velges); ingen → <c>mangler</c> med hull: enten har
    /// klassen ingen registrerte medlemmer, eller ingen av dem har et område som dekker kommunen — helseregionenes inndeling står
    /// ikke i lov, den kommer fra ekstern kilde (vedtekter, #340). Ingen motpart i teksten → <c>ikke_angitt</c>.
    /// </para>
    /// </summary>
    /// <param name="typekode">P-typen (samarbeid, avtale, betaling …), null = alle.</param>
    /// <returns>Null hvis kommunenummeret ikke er et gjeldende kommuneområde.</returns>
    public async Task<KommunePlikter?> PlikterForKommunenummerAsync(string kommunenummer, string? typekode, CancellationToken ct = default)
    {
        if (typekode is not null && !Strukturkanter.Plikttyper.Any(t => t.Kode == typekode))
        {
            throw new ArgumentException(
                $"Ukjent plikttype '{typekode}'. Gyldige verdier: {string.Join(", ", Strukturkanter.Plikttyper.Select(t => t.Kode))}.");
        }
        var tilhorighet = await ForKommunenummerAsync(kommunenummer, ct);
        if (tilhorighet is null) return null;
        var hull = new List<string>();

        // Kommunens virksomhet: rubrikken «kommune» — bare når den er entydig (ellers ingen direkte plikter å slå opp).
        var kommuneRubrikk = tilhorighet.Rubrikker.First(r => r.Rubrikk == "kommune");
        Guid? kommuneVirksomhet = kommuneRubrikk.Entydig ? kommuneRubrikk.Ider[0] : null;
        if (kommuneVirksomhet is null)
        {
            hull.Add(kommuneRubrikk.Ider.Count == 0
                ? "Kommunen som rettssubjekt (virksomhet med ansvarsområde til kommunen) er ikke registrert — direkte plikter kan ikke slås opp."
                : "Kommunen som rettssubjekt er ikke entydig — direkte plikter slås ikke opp.");
        }

        // Klassene kommunen er REGISTRERT medlem av (M, transitivt over klasse → klasse), fra virksomheten og fra området.
        var mKanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Medlemskap && k.TilBegrepId != null).ToListAsync(ct), ct);
        var medlemAv = new HashSet<Guid>();
        var ko = new Queue<(Guid? V, Guid? B)>();
        if (kommuneVirksomhet is { } kv) ko.Enqueue((kv, null));
        ko.Enqueue((null, tilhorighet.Kommune.Id));
        while (ko.Count > 0)
        {
            var (v, b) = ko.Dequeue();
            foreach (var k in mKanter.Where(k => (v != null && k.FraVirksomhetId == v) || (b != null && k.FraBegrepId == b)))
            {
                if (medlemAv.Add(k.TilBegrepId!.Value)) ko.Enqueue((null, k.TilBegrepId));
            }
        }

        var pKanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Plikt && (typekode == null || k.Typekode == typekode)).ToListAsync(ct), ct);
        // Klasser som er en ende i en P-kant: hvilke har ingen registrerte medlemmer?
        var klasser = pKanter.SelectMany(k => new[] { k.FraBegrepId, k.TilBegrepId }).Where(b => b != null).Select(b => b!.Value).Distinct().ToList();
        var klasserMedMedlemmer = mKanter.Where(k => klasser.Contains(k.TilBegrepId!.Value)).Select(k => k.TilBegrepId!.Value).ToHashSet();
        var klassekategori = await db.Begreper.Where(b => klasser.Contains(b.Id))
            .Select(b => new { b.Id, b.Begrepskategori }).ToDictionaryAsync(b => b.Id, b => b.Begrepskategori, ct);

        // [ENDRET, issue #353, koordinatorens kaldtest 2026-10-09] BEGGE retninger: «hvem har ansvar overfor hvem» er både det
        // kommunen SKAL (kommunen er fra-siden) og det andre skal OVERFOR kommunen (kommunen er til-siden) — direkte, via en
        // klasse kommunen (virksomheten eller territoriet) er registrert medlem av, eller fra/til en klasse uten registrert
        // medlemskap (med hull). Før rettingen ga Oslo 0 treff selv om HELSE SØR-ØST RHF hadde avtaleplikt overfor Oslo kommune.
        string? Grunnlag(Guid? virksomhet, Guid? begrep) =>
            kommuneVirksomhet is not null && virksomhet == kommuneVirksomhet ? "direkte"
            : begrep is { } b && medlemAv.Contains(b) ? "medlem_av"
            : begrep is { } k && klassekategori.GetValueOrDefault(k) == Nodetyper.Klasse && !klasserMedMedlemmer.Contains(k)
                ? "klasse_uten_registrert_medlemskap"
            : null;
        var valgte = new List<(StrukturkantEntitet Kant, string Retning, string Grunnlag)>();
        foreach (var k in pKanter)
        {
            var fraGrunnlag = Grunnlag(k.FraVirksomhetId, k.FraBegrepId);
            var tilGrunnlag = Grunnlag(k.TilVirksomhetId, k.TilBegrepId);
            if (fraGrunnlag is not null) valgte.Add((k, KommuneSkal, fraGrunnlag));
            // En plikt innad i en klasse (fra = til) står bare én gang, som det kommunen skal.
            if (tilGrunnlag is not null && !(fraGrunnlag is not null && k.FraBegrepId is not null && k.FraBegrepId == k.TilBegrepId))
                valgte.Add((k, OverforKommunen, tilGrunnlag));
        }

        var visninger = (await kanttjeneste.ByggVisningerAsync(valgte.Select(v => v.Kant).Distinct().ToList(), perspektiv: null, ct)).ToDictionary(v => v.Id);
        var dekker = new HashSet<Guid>(tilhorighet.Overordnede.Select(o => o.Id)) { tilhorighet.Kommune.Id };
        var treff = new List<PliktTreff>();
        foreach (var (kant, retning, grunnlag) in valgte)
        {
            var visning = visninger[kant.Id];
            var kommunesiden = retning == KommuneSkal ? visning.Fra : visning.Til!;
            var grunnlagHull = grunnlag == "klasse_uten_registrert_medlemskap"
                ? $"Klassen «{kommunesiden.Navn}» har ingen registrerte medlemmer — om plikten gjelder kommunen, avgjøres ikke her (intensjonal klasse, regelevaluering)."
                : null;
            // Motparten er den ANDRE enden: til-siden når kommunen skal, pliktsubjektet (fra) når plikten er overfor kommunen.
            var motpart = retning == KommuneSkal
                ? await MotpartAsync(kant.TilVirksomhetId, kant.TilBegrepId, visning.Til?.Navn, dekker, ct)
                : await MotpartAsync(kant.FraVirksomhetId, kant.FraBegrepId, visning.Fra.Navn, dekker, ct);
            treff.Add(new PliktTreff(visning, grunnlag, grunnlagHull, motpart) { Retning = retning });
        }
        var kommunenode = kommuneVirksomhet is not { } kvId ? null
            : new KantnodeVisning("virksomhet", kvId, kommuneRubrikk.Kandidater[0],
                await db.Virksomheter.Where(v => v.Id == kvId).Select(v => v.Aktortype).FirstOrDefaultAsync(ct));
        return new KommunePlikter(tilhorighet.Kommune, kommunenode, typekode,
            treff.OrderBy(t => t.Retning == KommuneSkal ? 0 : 1).ThenBy(t => t.Grunnlag switch { "direkte" => 0, "medlem_av" => 1, _ => 2 })
                .ThenBy(t => t.Kant.Typekode, StringComparer.Ordinal)
                .ThenBy(t => t.Kant.Visningstekst, StringComparer.Ordinal).ToList(),
            hull);
    }

    // ---------------- [Ny, issue #355] S6: «Hvem er ankeinstans for X tingrett?» ----------------

    /// <summary>
    /// [Ny, issue #355 AC4, Johanns beslutning 4 2026-10-09, docs/32 S6] Ankeinstansen for en domstol, AVLEDET — ikke lagret per par.
    /// Inndelingsforskriften § 10 første ledd («Hvert lagdømme har en lagmannsrett som er ankeinstans for flere rettskretser») er
    /// K <c>overproving</c> med undertype <c>anke</c> fra lagmannsretten til tingretten (klassen), avgrenset til eget lagdømme. Paret
    /// lagmannsrett ↔ tingrett regnes ut via kjeden tingrett → lagsogn → lagdømme (#345) og lagres ikke dobbelt; rettskretsen er
    /// avgrensning, ikke motpart (L1).
    /// <para>
    /// <b>Algoritmen:</b> (1) Domstolens områder: målene for dens egne <c>A har_ansvarsomrade</c>/<c>A sogner_til</c>-kanter (tingrettens
    /// kommuner og lagsogn) og alle områder de inngår i via <c>O bestar_av</c>, transitivt (lagsognet → lagdømmet). (2) De gjeldende,
    /// positive K overprøving/anke-kantene der domstolen er til-siden: direkte, via en klasse den er REGISTRERT medlem av (M), eller
    /// fra en klasse uten registrert medlemskap — den siste listes med hull og teller ikke (CLAUDE.md §8, samme regel som pliktoppslaget
    /// i #353). (3) Instansen på fra-siden: en virksomhet som selv har <c>A har_ansvarsomrade</c> er instans bare når et av områdene
    /// dekker domstolens («eget lagdømme»); en uten registrert ansvarsområde er instans uten territoriell avgrensning; en rolle løses til
    /// innehaverne/medlemmene hvis område dekker domstolens (som motparten i <see cref="PlikterForKommunenummerAsync"/>). (4) Nøyaktig én
    /// instans over de sikre kantene → <c>entydig</c>; flere → <c>ikke_entydig</c> (ingen velges); ingen → <c>mangler</c>.
    /// </para>
    /// <para>
    /// Forslag teller med (domstolkantene fra inndelingsforskriften er forslag til de er godkjent, #312), men hver kant viser sin status.
    /// </para>
    /// </summary>
    /// <returns>Null hvis virksomheten ikke finnes.</returns>
    public async Task<AnkeinstansSvar?> AnkeinstansAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        var domstol = await db.Virksomheter.Where(v => v.Id == virksomhetId).Select(v => new { v.Id, v.Navn, v.Aktortype }).FirstOrDefaultAsync(ct);
        if (domstol is null) return null;
        var hull = new List<string>();

        // (1) Domstolens områder: egne A-mål og deres O-forfedre.
        var egneA = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.FraVirksomhetId == virksomhetId && k.TilBegrepId != null
                        && (k.Typekode == "har_ansvarsomrade" || k.Typekode == OmraderegisterSeed.SognerTil))
            .ToListAsync(ct), ct);
        var oKanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Omradesammensetning && k.FraBegrepId != null && k.TilBegrepId != null)
            .ToListAsync(ct), ct);
        var foreldre = oKanter.GroupBy(k => k.TilBegrepId!.Value).ToDictionary(g => g.Key, g => g.Select(k => k.FraBegrepId!.Value).ToList());
        var dekker = new HashSet<Guid>();
        var ko = new Queue<Guid>(egneA.Select(k => k.TilBegrepId!.Value));
        while (ko.Count > 0)
        {
            var o = ko.Dequeue();
            if (!dekker.Add(o)) continue;
            foreach (var f in foreldre.GetValueOrDefault(o) ?? []) ko.Enqueue(f);
        }
        if (dekker.Count == 0)
        {
            hull.Add($"«{domstol.Navn}» har ingen registrerte områder (ansvarsområde eller lagsogn) — en ankeinstans med ansvarsområde kan ikke pares.");
        }
        var omrader = await db.Begreper.Where(b => dekker.Contains(b.Id))
            .Select(b => new OmradeVisning(b.Id, b.Term, b.Omradetype, b.Omradekode)).ToListAsync(ct);

        // (2) Klassene domstolen er REGISTRERT medlem av (M, transitivt over klasse → klasse).
        var mKanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Medlemskap && k.TilBegrepId != null).ToListAsync(ct), ct);
        var medlemAv = new HashSet<Guid>();
        var mko = new Queue<(Guid? V, Guid? B)>([(virksomhetId, null)]);
        while (mko.Count > 0)
        {
            var (v, b) = mko.Dequeue();
            foreach (var k in mKanter.Where(k => (v != null && k.FraVirksomhetId == v) || (b != null && k.FraBegrepId == b)))
            {
                if (medlemAv.Add(k.TilBegrepId!.Value)) mko.Enqueue((null, k.TilBegrepId));
            }
        }

        var anker = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Kompetanse && k.Typekode == Strukturkanter.Overproving && k.Undertype == "anke"
                        && k.Polaritet == "positiv" && (k.TilVirksomhetId != null || k.TilBegrepId != null))
            .ToListAsync(ct), ct);
        var tilKlasser = anker.Where(k => k.TilBegrepId != null).Select(k => k.TilBegrepId!.Value).Distinct().ToList();
        var klasserMedMedlemmer = mKanter.Where(k => tilKlasser.Contains(k.TilBegrepId!.Value)).Select(k => k.TilBegrepId!.Value).ToHashSet();
        var klassekategori = await db.Begreper.Where(b => tilKlasser.Contains(b.Id))
            .Select(b => new { b.Id, b.Begrepskategori }).ToDictionaryAsync(b => b.Id, b => b.Begrepskategori, ct);

        var valgte = new List<(StrukturkantEntitet Kant, string Grunnlag)>();
        foreach (var k in anker)
        {
            string? grunnlag = k.TilVirksomhetId == virksomhetId ? "direkte"
                : k.TilBegrepId is { } b && medlemAv.Contains(b) ? "medlem_av"
                : k.TilBegrepId is { } c && klassekategori.GetValueOrDefault(c) == Nodetyper.Klasse && !klasserMedMedlemmer.Contains(c)
                    ? "klasse_uten_registrert_medlemskap"
                : null;
            if (grunnlag is not null) valgte.Add((k, grunnlag));
        }

        // (3) Instansen per kant, avgrenset til domstolens områder.
        var fraVirksomheter = valgte.Where(v => v.Kant.FraVirksomhetId != null).Select(v => v.Kant.FraVirksomhetId!.Value).Distinct().ToList();
        var fraAnsvar = (await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
                .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade"
                            && k.FraVirksomhetId != null && fraVirksomheter.Contains(k.FraVirksomhetId.Value) && k.TilBegrepId != null)
                .ToListAsync(ct), ct))
            .GroupBy(k => k.FraVirksomhetId!.Value).ToDictionary(g => g.Key, g => g.Select(k => k.TilBegrepId!.Value).ToList());
        var visninger = (await kanttjeneste.ByggVisningerAsync(valgte.Select(v => v.Kant).ToList(), perspektiv: null, ct)).ToDictionary(v => v.Id);
        var treff = new List<AnkeinstansTreff>();
        foreach (var (kant, grunnlag) in valgte)
        {
            var visning = visninger[kant.Id];
            PliktMotpart? instans;
            if (kant.FraVirksomhetId is { } fv)
            {
                var omr = fraAnsvar.GetValueOrDefault(fv);
                // Har instansen et registrert ansvarsområde, må det dekke domstolens («eget lagdømme»); ellers gjelder kanten
                // ikke denne domstolen. Uten registrert ansvarsområde: ingen territoriell avgrensning å prøve.
                instans = omr is null || omr.Any(dekker.Contains)
                    ? new PliktMotpart("konkret", [visning.Fra.Navn], [fv], null)
                    : null;
            }
            else
            {
                instans = await MotpartAsync(null, kant.FraBegrepId, visning.Fra.Navn, dekker, ct);
            }
            if (instans is null) continue;
            var grunnlagHull = grunnlag == "klasse_uten_registrert_medlemskap"
                ? $"Klassen «{visning.Til!.Navn}» har ingen registrerte medlemmer — om den omfatter «{domstol.Navn}», avgjøres ikke her."
                : null;
            treff.Add(new AnkeinstansTreff(visning, grunnlag, grunnlagHull, instans));
        }

        // (4) Svaret, bare fra de sikre kantene.
        var sikre = treff.Where(t => t.Grunnlag != "klasse_uten_registrert_medlemskap" && t.Instans.Status is "konkret" or "entydig").ToList();
        var navn = new Dictionary<Guid, string>();
        foreach (var t in sikre)
        {
            for (var i = 0; i < t.Instans.Ider.Count; i++) navn.TryAdd(t.Instans.Ider[i], t.Instans.Kandidater[i]);
        }
        var sortert = navn.Keys.OrderBy(i => navn[i], StringComparer.Ordinal).ToList();
        if (treff.Count == 0)
        {
            hull.Add("Ingen K overprøving/anke-kant gjelder domstolen (direkte, via registrert medlemskap eller fra en klasse uten registrerte medlemmer).");
        }
        var status = sortert.Count switch { 0 => "mangler", 1 => "entydig", _ => "ikke_entydig" };
        return new AnkeinstansSvar(new KantnodeVisning("virksomhet", domstol.Id, domstol.Navn, domstol.Aktortype), status,
            sortert.Select(i => navn[i]).ToList(), sortert,
            treff.OrderBy(t => t.Grunnlag switch { "direkte" => 0, "medlem_av" => 1, _ => 2 })
                .ThenBy(t => t.Kant.Visningstekst, StringComparer.Ordinal).ToList(),
            omrader.OrderBy(o => o.Navn, StringComparer.Ordinal).ToList(), hull);
    }

    /// <summary>[Ny, issue #353] <see cref="PliktTreff.Retning"/>: kommunen er pliktsubjektet (fra-siden).</summary>
    public const string KommuneSkal = "kommunen_skal";

    /// <summary>[Ny, issue #353] <see cref="PliktTreff.Retning"/>: plikten er overfor kommunen (kommunen er til-siden).</summary>
    public const string OverforKommunen = "overfor_kommunen";

    /// <summary>[Ny, issue #353] Motparten til én plikt sett fra kommunen — den ANDRE enden av kanten (virksomhet, klasse/rolle eller
    /// ingen) — se <see cref="PlikterForKommunenummerAsync"/>.</summary>
    private async Task<PliktMotpart> MotpartAsync(Guid? virksomhetId, Guid? begrepId, string? navnPaaNoden, HashSet<Guid> dekker, CancellationToken ct)
    {
        if (virksomhetId is { } tv) return new PliktMotpart("konkret", [navnPaaNoden!], [tv], null);
        if (begrepId is not { } tb) return new PliktMotpart("ikke_angitt", [], [], "Motparten står ikke i teksten.");

        // Medlemmene av klassen (M medlem_av) eller innehaverne av rollen (I innehar) — virksomheter.
        var medlemskanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => (k.Kategori == Strukturkanter.Medlemskap || k.Kategori == Strukturkanter.Rolleinnehav)
                        && k.TilBegrepId == tb && k.FraVirksomhetId != null).ToListAsync(ct), ct);
        var medlemmer = medlemskanter.Select(k => k.FraVirksomhetId!.Value).Distinct().ToList();
        if (medlemmer.Count == 0)
        {
            return new PliktMotpart("mangler", [], [],
                $"«{navnPaaNoden}» har ingen registrerte medlemmer — parene regnes ut via område når medlemskapet og områdeinndelingen "
                + "er lastet (helseregionene: ekstern kilde, #340).");
        }
        var ansvar = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade"
                        && k.FraVirksomhetId != null && medlemmer.Contains(k.FraVirksomhetId.Value)
                        && k.TilBegrepId != null && dekker.Contains(k.TilBegrepId.Value)).ToListAsync(ct), ct);
        var treffIder = ansvar.Select(k => k.FraVirksomhetId!.Value).Distinct().ToList();
        var navn = await db.Virksomheter.Where(v => treffIder.Contains(v.Id)).Select(v => new { v.Id, v.Navn }).ToDictionaryAsync(v => v.Id, v => v.Navn, ct);
        var sortert = treffIder.OrderBy(i => navn.GetValueOrDefault(i), StringComparer.Ordinal).ToList();
        return sortert.Count switch
        {
            1 => new PliktMotpart("entydig", [navn[sortert[0]]], sortert, null),
            0 => new PliktMotpart("mangler", [], [],
                $"Ingen av de {medlemmer.Count} registrerte medlemmene av «{navnPaaNoden}» har ansvarsområde som dekker kommunen — "
                + "områdeinndelingen er ikke lastet (helseregionene: ekstern kilde, #340)."),
            _ => new PliktMotpart("ikke_entydig", sortert.Select(i => navn[i]).ToList(), sortert,
                "Flere medlemmer har ansvarsområde som dekker kommunen — ingen velges."),
        };
    }

    /// <summary>
    /// [Ny, issue #345] Avledede foreldrekanter: aktøren X har <c>A sogner_til</c> område L og <c>A har_ansvarsomrade</c>
    /// område N ⇒ N inngår i L. Validert bare når BEGGE kantene er validert; ellers hviler svaret på et forslag.
    /// </summary>
    private async Task<List<(Guid Barn, Guid Forelder, bool Validert)>> AvledetGjennomSognerTilAsync(CancellationToken ct)
    {
        var sogner = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == OmraderegisterSeed.SognerTil
                        && k.FraVirksomhetId != null && k.TilBegrepId != null)
            .ToListAsync(ct), ct);
        if (sogner.Count == 0) return [];
        var aktorer = sogner.Select(k => k.FraVirksomhetId!.Value).Distinct().ToList();
        var ansvar = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade"
                        && k.FraVirksomhetId != null && aktorer.Contains(k.FraVirksomhetId.Value) && k.TilBegrepId != null)
            .ToListAsync(ct), ct);
        var ansvarPerAktor = ansvar.GroupBy(k => k.FraVirksomhetId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        return sogner.SelectMany(s => (ansvarPerAktor.GetValueOrDefault(s.FraVirksomhetId!.Value) ?? [])
                .Select(a => (Barn: a.TilBegrepId!.Value, Forelder: s.TilBegrepId!.Value, Validert: s.Status == "validert" && a.Status == "validert")))
            .ToList();
    }
}
