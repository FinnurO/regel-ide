using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #517 «Til vurdering», Johanns avgjørelser 2026-11-27] Tellingene til startsiden: hva som venter på en menneskelig avgjørelse,
/// per kø, og hva en vurdering låser opp. Bare lesing. Siden er en guide, ikke en ny kø: hvert tall skal være det samme som køsiden viser
/// (AK 1), og hvert tall regnes med samme regel som køen (premissporten i <see cref="FunnTjeneste.PremissmanglerForAsync"/>, aktørkøens
/// ruting i <see cref="AktorkoTjeneste.GrupperAsync"/>, forslagskøens <see cref="Strukturkanter.Ventende"/>), ikke med en egen kopi.
/// <para>
/// Ingen tall gjettes (CLAUDE.md §8). Et tall som ikke finnes, er ikke 0 i denne tjenesten; kalleren viser «ikke tilgjengelig» når kallet
/// feiler (AK 7).
/// </para>
/// </summary>
public sealed class TilVurderingTjeneste(RegelIdeDbContext db, AktorkoTjeneste aktorko, AktorkoRelasjonTjeneste relasjoner, FunnTjeneste funn)
{
    /// <summary>[ANTATT, #517] Pilotens kilder (#375, barnehageloven): rettskildene med «barnehage» i tittelen. Det finnes ikke noe pilotmerke
    /// på rettskildene. Regelen gir barnehageloven og forskriftene til den, og også «Forskrift om helse og miljø i barnehager, skoler og
    /// skolefritidsordninger». Den brukes både i aktørkøen og i forslagskøen, slik at de to tallene for piloten har samme utvalg.</summary>
    public const string PilotOrd = "barnehage";

    /// <summary>Forslag (ikke vurdert): det et menneske skal ta stilling til. <see cref="Strukturkanter.FagligVurdert"/> er alt vurdert og
    /// regnes som vurdert i premissporten (<see cref="FunnTjeneste"/>).</summary>
    private static readonly string[] Forslag = [Strukturkanter.MaskineltForeslatt, Strukturkanter.ManueltForeslatt];

    private static readonly string[] Vurdert = [Strukturkanter.FagligVurdert, Strukturkanter.Godkjent];

    public async Task<TilVurdering> HentAsync(CancellationToken ct = default)
    {
        var aktorkoTall = await AktorkoAsync(ct);
        var (departementer, utgatt, etterfolgere) = await UtgattAktorAsync(ct);
        var ho = await HjemmelOpphevetAsync(ct);
        return new TilVurdering(
            DateTimeOffset.UtcNow,
            departementer,
            aktorkoTall,
            etterfolgere,
            await PilotAsync(ct),
            ho,
            utgatt,
            await ForekomsterAsync(ct),
            await KlasseforslagAsync(ct),
            await db.Begreper.CountAsync(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende"
                                              && (b.Navneformgrunn == null || b.Navneformgrunn != "generisk")
                                              && b.Godkjenning != null && Forslag.Contains(b.Godkjenning), ct),
            await db.Strukturkanter.CountAsync(k => k.OppdagelsesKilde == "monster:livslop" && k.Typekode == "forvaltning"
                                                    && Forslag.Contains(k.Status), ct),
            await KildefeilAsync(ct),
            await db.Strukturkanter.CountAsync(k => Strukturkanter.Ventende.Contains(k.Status), ct),
            new TilVurderingEldre(
                await db.Begrepsforekomster.CountAsync(b => b.Status == "Venter", ct),
                await db.BegrepDefinisjonRelasjonKandidater.CountAsync(b => b.Status == "Venter", ct),
                await db.VirksomhetKandidater.CountAsync(v => v.Status == "Venter", ct)));
    }

    // ------------------------------------------------------------------ Aktørkøen

    private async Task<TilVurderingAktorko> AktorkoAsync(CancellationToken ct)
    {
        var grupper = await aktorko.GrupperAsync(ct);
        var pilotKilder = (await PilotRettskilderAsync(ct)).ToHashSet();
        var perHandling = new Dictionary<string, int>(StringComparer.Ordinal);
        int klare = 0, trengerSted = 0;
        foreach (var g in grupper)
        {
            var post = Forhandsklassifisering.Finn(g);
            var handling = post is null ? "uten_forhandsklassifisering" : Forhandsklassifisering.Handling(post);
            perHandling[handling] = perHandling.GetValueOrDefault(handling) + 1;
            if (handling != Forhandshandling.Godkjenn) continue;
            // Samme regel som skjemaet (kandidater/aktorkoGodkjenning.ts): en klasse, rolle eller et område uten hjemmel må få «Hvor gjelder
            // den?» før knappen virker. «Godta forslaget» fyller ikke ut det feltet, så navnet er ikke klart med tre klikk.
            if (Forhandsklassifisering.Type(post!) != Forhandstype.Aktor && post!.Hjemmel is null) trengerSted++;
            else klare++;
        }
        var rel = await relasjoner.AlleAsync(false, ct);
        return new TilVurderingAktorko(
            grupper.Count,
            grupper.Sum(g => g.Forekomster.Count),
            klare,
            trengerSted,
            perHandling,
            rel.SelectMany(r => r.Relasjoner).Count(r => r.Status == Relasjonsstatus.OpprettNavnetForst),
            grupper.Count(g => g.Forekomster.Any(f => pilotKilder.Contains(f.RettskildeId))));
    }

    // ------------------------------------------------------------------ Funn: utgått aktør (departementene og etterfølgerne)

    /// <summary>
    /// Navneformene og etterfølgerkantene som holder igjen funn om utgåtte aktører. Regelen er premissportens
    /// (<see cref="FunnTjeneste.PremissmanglerForAsync"/>): en navneform eller en etterfølgerkant som ikke er faglig vurdert eller godkjent.
    /// Den er skrevet ut her i stedet for å kalle porten, fordi porten også regner R9 (opphevet hjemmelslov), og det tok 2,1 s for de 592
    /// funnene mot en kopi av den delte basen (målt 2026-11-27). Hele svaret skal under 1 s (AK 8). At de to gir samme tall, er låst i
    /// <c>TilVurderingTjenesteTests</c>.
    /// </summary>
    private async Task<(TilVurderingDepartementer, TilVurderingUtgattAktor, TilVurderingEtterfolgere)> UtgattAktorAsync(CancellationToken ct)
    {
        var koblinger = await db.FunnKoblinger.AsNoTracking()
            .Where(k => (k.Rolle == Funn.Navneform && k.BegrepId != null) || (k.Rolle == Funn.Etterfolger && k.StrukturkantId != null))
            .Join(db.Funn.Where(f => f.Kategori == Funn.UtgattAktor && f.Status == Funn.Oppdaget), k => k.FunnId, f => f.Id,
                (k, f) => new { k.FunnId, k.BegrepId, k.StrukturkantId })
            .ToListAsync(ct);
        var antallFunn = await db.Funn.CountAsync(f => f.Kategori == Funn.UtgattAktor && f.Status == Funn.Oppdaget, ct);

        var begrepIder = koblinger.Where(k => k.BegrepId != null).Select(k => k.BegrepId!.Value).Distinct().ToList();
        var begreper = await db.Begreper.IgnoreQueryFilters().AsNoTracking().Where(b => begrepIder.Contains(b.Id))
            .Select(b => new { b.Id, b.Term, b.Navneformgrunn, b.Godkjenning, b.VirksomhetReferanseId }).ToDictionaryAsync(b => b.Id, ct);
        var kantIder = koblinger.Where(k => k.StrukturkantId != null).Select(k => k.StrukturkantId!.Value).Distinct().ToList();
        var kanter = await db.Strukturkanter.IgnoreQueryFilters().AsNoTracking().Where(k => kantIder.Contains(k.Id))
            .Select(k => new { k.Id, k.Status }).ToDictionaryAsync(k => k.Id, k => k.Status, ct);

        // Samme «ikke vurdert» som porten: godkjenning NULL teller som ikke vurdert. En navneform eller kant som ikke finnes lenger, er en annen
        // mangel («finnes ikke lenger») som ikke rettes i en kø.
        var navneformmangler = koblinger.Where(k => k.BegrepId is { } b && begreper.TryGetValue(b, out var n)
                                                    && (n.Godkjenning is null || !Vurdert.Contains(n.Godkjenning))).ToList();
        var etterfolgermangler = koblinger.Where(k => k.StrukturkantId is { } s && kanter.TryGetValue(s, out var st) && !Vurdert.Contains(st)).ToList();
        var medNavneform = navneformmangler.Select(k => k.FunnId).Distinct().Count();
        var medEtterfolger = etterfolgermangler.Select(k => k.FunnId).Distinct().Count();

        // Lista per aktør: bare navneformer som er forslag. En navneform uten godkjenning har ingen knapp på aktørsiden (AK 4); den telles i
        // FunnMedNavneformmangel, men står ikke i lista.
        var ventende = begreper.Values.Where(b => b.VirksomhetReferanseId != null && b.Godkjenning != null && Forslag.Contains(b.Godkjenning))
            .ToDictionary(b => b.Id, b => new { b.Id, b.Term, b.Navneformgrunn, b.Godkjenning, Aktor = b.VirksomhetReferanseId!.Value });
        var funnPerAktor = navneformmangler.Where(k => ventende.ContainsKey(k.BegrepId!.Value))
            .Select(k => (Aktor: ventende[k.BegrepId!.Value].Aktor, Funn: k.FunnId))
            .Distinct().ToLookup(x => x.Aktor, x => x.Funn);
        var iBruk = navneformmangler.Select(k => k.BegrepId!.Value).ToHashSet();
        var aktorIder = funnPerAktor.Select(g => g.Key).ToList();
        var visning = await new VirksomhetVisningsnavnTjeneste(db).AlleAsync(ct);
        var registernavn = await db.Virksomheter.AsNoTracking().Where(v => aktorIder.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Navn, ct);
        var rader = aktorIder
            .Select(id => new TilVurderingDepartement(
                id,
                visning.GetValueOrDefault(id) ?? registernavn.GetValueOrDefault(id) ?? id.ToString(),
                ventende.Values.Where(b => b.Aktor == id && iBruk.Contains(b.Id)).OrderBy(b => b.Term, StringComparer.Ordinal)
                    .Select(b => new TilVurderingNavneform(b.Id, b.Term, b.Navneformgrunn, b.Godkjenning!)).ToList(),
                funnPerAktor[id].Count()))
            .OrderByDescending(r => r.Funn).ThenBy(r => r.Navn, StringComparer.Ordinal).ToList();
        // Et funn kan vente på navneformer hos flere aktører. Summen av radene teller det da flere ganger; den kumulative lista gjør ikke det,
        // så «de sju første holder igjen N funn» er riktig (Start her, docs/09 §59).
        var sett = new HashSet<Guid>();
        var kumulativt = rader.Select(r =>
        {
            sett.UnionWith(funnPerAktor[r.VirksomhetId]);
            return sett.Count;
        }).ToList();

        var forslag = await db.Strukturkanter.AsNoTracking()
            .Where(k => k.Typekode == "etterfolger" && Forslag.Contains(k.Status)).Select(k => new { k.Id, k.Status }).ToListAsync(ct);
        var manuelle = forslag.Where(k => k.Status == Strukturkanter.ManueltForeslatt).Select(k => k.Id).ToHashSet();

        return (
            new TilVurderingDepartementer(rader, medNavneform, kumulativt),
            new TilVurderingUtgattAktor(antallFunn),
            new TilVurderingEtterfolgere(forslag.Count, manuelle.Count, medEtterfolger,
                etterfolgermangler.Where(k => manuelle.Contains(k.StrukturkantId!.Value)).Select(k => k.FunnId).Distinct().Count()));
    }

    /// <summary>Samme tekst som premissporten skriver (<see cref="FunnTjeneste"/>, «Navneformen «X» er ikke faglig vurdert (…)»).</summary>
    public static bool ErNavneformmangel(string mangel) =>
        mangel.StartsWith("Navneformen «", StringComparison.Ordinal) && mangel.Contains("» er ikke faglig vurdert", StringComparison.Ordinal);

    /// <summary>En etterfølgerkant som finnes og ikke er vurdert («Etterfølger ikke vurdert: «X» (…)»). Funn uten noen etterfølgerkant
    /// («funnet peker ikke på noen etterfølgerkant») rettes ikke i forslagskøen, men i juristvurderingen, og telles ikke her.</summary>
    public static bool ErEtterfolgermangel(string mangel) =>
        mangel.StartsWith("Etterfølger ikke vurdert: «", StringComparison.Ordinal) && !mangel.Contains("(kanten finnes ikke lenger)", StringComparison.Ordinal);

    // ------------------------------------------------------------------ Funn: hjemmel opphevet

    private async Task<TilVurderingHjemmelOpphevet> HjemmelOpphevetAsync(CancellationToken ct)
    {
        var oppdaget = await db.Funn.AsNoTracking().Include(f => f.Koblinger).Include(f => f.Vurderingspunkter)
            .Where(f => f.Kategori == Funn.HjemmelOpphevet && f.Status == Funn.Oppdaget).ToListAsync(ct);
        var mangler = await funn.PremissmanglerForAsync(oppdaget, ct);
        return new TilVurderingHjemmelOpphevet(
            oppdaget.Count,
            // «Mulig bortfalt» kan ikke vises før «Ny hjemmel» er vurdert: samme regel som merket (FunnTjeneste.MuligBortfaltVises).
            oppdaget.Count(f => FunnTjeneste.MuligBortfaltVises(f) == false),
            oppdaget.Count(f => mangler[f.Id].Any(m => m.StartsWith(FunnTjeneste.KunngjortMangelStart, StringComparison.Ordinal))),
            await db.Funn.CountAsync(f => f.Status == Funn.Oppdaget && f.Kategori != Funn.HjemmelOpphevet && f.Kategori != Funn.UtgattAktor, ct));
    }

    // ------------------------------------------------------------------ Piloten, forekomstene, klasser, kildefeil

    private async Task<List<Guid>> PilotRettskilderAsync(CancellationToken ct) =>
        await db.Rettskilder.AsNoTracking().Where(r => EF.Functions.ILike(r.Tittel, "%" + PilotOrd + "%")).Select(r => r.Id).ToListAsync(ct);

    private async Task<TilVurderingPilot> PilotAsync(CancellationToken ct)
    {
        var kilder = await PilotRettskilderAsync(ct);
        var forslag = await db.Strukturkanter.AsNoTracking()
            .Where(k => Strukturkanter.Ventende.Contains(k.Status) && k.HjemmelRettskildeId != null && kilder.Contains(k.HjemmelRettskildeId.Value))
            .Select(k => new { k.HjemmelRettskildeId, k.OppdagelsesKilde }).ToListAsync(ct);
        return new TilVurderingPilot(
            forslag.Count,
            forslag.Count(f => f.OppdagelsesKilde.StartsWith("ki:", StringComparison.Ordinal)),
            forslag.Count(f => f.OppdagelsesKilde.StartsWith("monster:", StringComparison.Ordinal)),
            forslag.Select(f => f.HjemmelRettskildeId!.Value).Distinct().OrderBy(x => x).ToList());
    }

    private async Task<List<TilVurderingForekomster>> ForekomsterAsync(CancellationToken ct)
    {
        // Tagg per forekomst (#486): en binding med lesning og tagg. Bindinger for hele rettskilden (uten lesning) er ikke med.
        var rader = await db.NavneformBindinger.AsNoTracking()
            .Where(b => b.Entitetsstatus == Strukturkanter.Gjeldende && b.TekstTaggId != null && b.Lesning != null && Forslag.Contains(b.Status))
            .Join(db.Begreper.IgnoreQueryFilters(), b => b.NavneformId, n => n.Id, (b, n) => new { n.Term, b.Lesning })
            .ToListAsync(ct);
        return rader.GroupBy(r => r.Term, StringComparer.Ordinal)
            .Select(g => new TilVurderingForekomster(g.Key, g.Count(), g.Count(r => r.Lesning == "slutning")))
            .OrderByDescending(r => r.Slutninger).ThenBy(r => r.Term, StringComparer.Ordinal).ToList();
    }

    private async Task<int> KlasseforslagAsync(CancellationToken ct) =>
        await db.Begreper.CountAsync(b => b.Entitetsstatus == "gjeldende" && b.Godkjenning != null && Forslag.Contains(b.Godkjenning)
                                          && (b.Navneformgrunn == "generisk"
                                              || b.Begrepskategori == "klasse" || b.Begrepskategori == "rolle"
                                              || b.Begrepskategori == "omrade" || b.Begrepskategori == "gruppe"), ct);

    private async Task<TilVurderingKildefeil> KildefeilAsync(CancellationToken ct)
    {
        var typer = await db.Kildefeil.AsNoTracking().Where(k => k.Status == "Ny").GroupBy(k => k.Type)
            .Select(g => new { g.Key, Antall = g.Count() }).ToListAsync(ct);
        return new TilVurderingKildefeil(typer.Sum(t => t.Antall), typer.ToDictionary(t => t.Key, t => t.Antall));
    }
}

/// <summary>[Ny, #517] Svaret fra <c>GET /api/til-vurdering</c>. Bare tall og id-er; tekstene står i frontenden (<c>tilVurdering.ts</c>).</summary>
public sealed record TilVurdering(
    DateTimeOffset Hentet,
    TilVurderingDepartementer Departementer,
    TilVurderingAktorko Aktorko,
    TilVurderingEtterfolgere Etterfolgere,
    TilVurderingPilot Pilot,
    TilVurderingHjemmelOpphevet HjemmelOpphevet,
    TilVurderingUtgattAktor UtgattAktor,
    IReadOnlyList<TilVurderingForekomster> Forekomster,
    int Klasseforslag,
    int Navneformforslag,
    int Ressortkanter,
    TilVurderingKildefeil Kildefeil,
    int ForslagskoTotalt,
    TilVurderingEldre Eldre);

/// <param name="Aktorer">Aktørene med navneformer som er forslag og holder igjen funn om utgåtte aktører, flest funn først.</param>
/// <param name="FunnMedNavneformmangel">Funn (oppdaget, utgått aktør) med minst én navneform som ikke er vurdert, også de uten knapp.</param>
/// <param name="FunnKumulativt">Element i: antall ulike funn de i+1 første aktørene i lista holder igjen.</param>
public sealed record TilVurderingDepartementer(IReadOnlyList<TilVurderingDepartement> Aktorer, int FunnMedNavneformmangel, IReadOnlyList<int> FunnKumulativt);

public sealed record TilVurderingDepartement(Guid VirksomhetId, string Navn, IReadOnlyList<TilVurderingNavneform> Navneformer, int Funn);

public sealed record TilVurderingNavneform(Guid BegrepId, string Term, string? Navneformgrunn, string Godkjenning);

/// <param name="Klare">Forhåndsklassifisert til godkjenning og klar med «Godta forslaget», nivå og «Godkjenn».</param>
/// <param name="TrengerSted">Til godkjenning, men en klasse, rolle eller et område uten hjemmel: «Hvor gjelder den?» må velges.</param>
/// <param name="PerHandling">Antall navn per handling i forhåndsklassifiseringen (<see cref="Forhandshandling"/>).</param>
/// <param name="RelasjonerVenterPaNavn">Relasjoner i forhåndsklassifiseringen som står som «Opprett navnet først».</param>
/// <param name="PilotNavn">Navn med minst én forekomst i pilotens kilder (<see cref="TilVurderingTjeneste.PilotOrd"/>).</param>
public sealed record TilVurderingAktorko(
    int AntallNavn, int Forekomster, int Klare, int TrengerSted, IReadOnlyDictionary<string, int> PerHandling, int RelasjonerVenterPaNavn, int PilotNavn);

/// <param name="Forslag">Etterfølgerkanter som er forslag (maskinelt eller manuelt).</param>
/// <param name="FunnMedEtterfolgermangel">Funn (oppdaget, utgått aktør) med en etterfølgerkant som ikke er vurdert.</param>
/// <param name="FunnManuelle">Av dem: funn som peker på en av de manuelt foreslåtte kantene.</param>
public sealed record TilVurderingEtterfolgere(int Forslag, int Manuelle, int FunnMedEtterfolgermangel, int FunnManuelle);

/// <param name="Forslag">Forslag i forslagskøen med hjemmel i pilotens kilder.</param>
/// <param name="Rettskilder">Hjemlene forslagene står under, til lenken inn i forslagskøen.</param>
public sealed record TilVurderingPilot(int Forslag, int Ki, int Monster, IReadOnlyList<Guid> Rettskilder);

/// <param name="HoldesAvNyHjemmel">Funn som ikke kan vises som «mulig bortfalt» før «Ny hjemmel» er vurdert.</param>
/// <param name="KunngjortVersjon">Funn med mangelen «Kunngjort versjon, gjeldende status ikke kontrollert».</param>
/// <param name="AndreFunnOppdaget">Oppdagede funn i de andre kategoriene (motstrid, uklarhet og hull).</param>
public sealed record TilVurderingHjemmelOpphevet(int Oppdaget, int HoldesAvNyHjemmel, int KunngjortVersjon, int AndreFunnOppdaget);

/// <summary>[#517] Tallet «står uten mangler når navneformene og etterfølgerne er vurdert» (325 i kartleggingen) er bevisst ikke med: det
/// krever hele premissporten med R9, og det tok 2,1 s (AK 8).</summary>
public sealed record TilVurderingUtgattAktor(int Oppdaget);

public sealed record TilVurderingForekomster(string Term, int Forslag, int Slutninger);

public sealed record TilVurderingKildefeil(int Ny, IReadOnlyDictionary<string, int> PerType);

public sealed record TilVurderingEldre(int Begrepskandidater, int RelaterteDefinisjoner, int Virksomhetskandidater);
