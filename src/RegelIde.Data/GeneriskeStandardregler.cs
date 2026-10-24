using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #394 AK 1, Johanns beslutning 2026-10-23] Én standardregel for en generisk navneform: hvordan referenten utledes når ingen
/// manuell binding dekker forekomsten. Reglene er kode, ikke data — det er tre, de er bestemt av Johann, og en ny regel er en ny
/// utledningsmåte (en ny kodevei), ikke en ny rad. Samme valg som <see cref="Klasseregler"/> for utledet medlemskap (#384).
/// </summary>
/// <param name="Term">Den generiske navneformens term (små bokstaver).</param>
/// <param name="Regelkode">Stabil kode for regelen; står i oppslagssvaret og i begrunnelser.</param>
/// <param name="Navn">Regelen i klart språk, slik den vises.</param>
/// <param name="Forklaring">Hva regelen gjør, med et eksempel — samme ord som profilsiden (avledet slutning, premiss, grunnlag).</param>
/// <param name="Grunnlag">Det rettslige grunnlaget regelen bygger på, i klart språk.</param>
/// <param name="Kunnskapsstatus">Kunnskapsstatusen et svar fra regelen får: alltid <c>avledet</c> (en slutning fra premissene).</param>
public sealed record GeneriskStandardregel(string Term, string Regelkode, string Navn, string Forklaring, string Grunnlag, string Kunnskapsstatus);

/// <summary>Hvordan et standardregelsvar ble funnet.</summary>
public static class StandardregelRute
{
    /// <summary>Ressortfordelingen fra organiseringsvedtakene (F forvaltning med lovens ELI, #395), vurdert av et menneske. Primær kilde.</summary>
    public const string Ressortfordeling = "ressortfordeling";
    /// <summary>Lovdatas felt «Ansvarlig departement». Sekundær kilde, brukt bare når ressortfordelingen ikke gir et vurdert svar.</summary>
    public const string Lovdata = "lovdata";
    /// <summary>En fast referent (en klasse): «kommunen» → klassen kommune.</summary>
    public const string FastReferent = "fast_referent";
}

/// <summary>Utfallet av en standardregel for én rettskilde.</summary>
public static class StandardregelUtfall
{
    public const string Lost = "lost";
    public const string IkkeLost = "ikke_lost";
    public const string Flertydig = "flertydig";
}

/// <summary>Ett premiss et standardregelsvar bygger på. <see cref="Type"/>: <c>ressortkant</c> (F forvaltning; <see cref="KantId"/> og
/// <see cref="Status"/> er kantens), <c>lovdata_felt</c> (Lovdatas «Ansvarlig departement»), <c>klasse</c> (den faste referenten, med
/// klassens hjemmel).</summary>
public sealed record StandardregelPremiss(
    string Type, string Tekst, Guid? KantId = null, string? Status = null, string? Kunnskapsstatus = null,
    Guid? HjemmelRettskildeId = null, string? HjemmelEid = null, string? Dokumentasjon = null);

/// <summary>
/// Svaret fra en standardregel for én rettskilde. <see cref="Dokumentasjon"/> er <c>primaer</c> når svaret bygger på vurderte
/// modellutsagn (ressortkant godkjent eller faglig vurdert, klassens hjemmel), <c>sekundaer</c> når det bygger på Lovdatas felt.
/// <see cref="Merknader"/> sier det en leser må vite: at et ressortforslag venter, at Lovdata avviker, at en del av loven er lagt til et
/// annet departement.
/// </summary>
public sealed record StandardregelSvar(
    string Term, string Regelkode, string Regelnavn, string Utfall, string? Rute, Kantnode? Referent, KantnodeVisning? ReferentVisning,
    string Kunnskapsstatus, string? Dokumentasjon, DateOnly Oppslagsdato, DateOnly? GyldigFra, DateOnly? GyldigTil, string Forklaring,
    IReadOnlyList<StandardregelPremiss> Premisser, IReadOnlyList<string> Merknader)
{
    /// <summary>[#395 M5] Satt når referenten er en nedlagt aktør (svaret gjelder en dato før nedleggelsen).</summary>
    public DateOnly? ReferentNedlagt { get; init; }
}

/// <summary>Andelen rettskilder der ordet står, per utfall og rute — målingen i PR-en og på oversikten.</summary>
public sealed record StandardregelDekning(
    string Term, int Rettskilder, int Ressortfordeling, int Lovdata, int FastReferent, int IkkeLost, int Flertydig,
    int RessortforslagVenter, IReadOnlyDictionary<string, int> IkkeLostGrunner, IReadOnlyList<StandardregelDekningRad> Eksempler);

public sealed record StandardregelDekningRad(Guid RettskildeId, string Tittel, string Utfall, string? Rute, string? Referent, string Forklaring);

/// <summary>
/// [Ny, issue #394 AK 1–3] Standardreglene for generiske navneformer og utledningen av referenten for én rettskilde.
/// <para>
/// <b>«departementet»</b> (<c>ansvarlig_departement</c>): det departementet som har forvaltningsansvaret for rettskilden på datoen.
/// [LÅST, Johanns beslutning 2026-10-23, kommentar på #394] Grunnlaget er <b>ressortfordelingen</b> fra #395 — F forvaltning-kantene med
/// lovens ELI (<see cref="StrukturkantEntitet.ObjektEli"/>) — og Lovdatas «Ansvarlig departement» er bare en fallback, merket sekundær
/// kilde. Rekkefølgen:
/// </para>
/// <list type="number">
/// <item>En ressortkant for HELE loven (<see cref="DepartementLivslopTjeneste.ErHeleLoven"/>) som gjelder på datoen og er <b>godkjent eller
/// faglig vurdert</b>. Én referent → svaret, primær kilde. Flere ulike → flertydig, ingen valgt.</item>
/// <item>Ellers Lovdatas felt, når det oppgir nøyaktig ett departement som finnes i katalogen i dag (<see cref="VirksomhetOppslagTjeneste"/>,
/// som aldri treffer et nedlagt departement). Sekundær kilde. Står det en ressortkant som FORSLAG (ikke vurdert), sier merknaden at den
/// venter og om den sier det samme — forslaget brukes ikke som svar (det er ikke vurdert av noen).</item>
/// <item>Ellers ikke løst, med grunnen (ingen kilde, flere departementer i Lovdata, navnet finnes ikke i katalogen).</item>
/// </list>
/// <para>
/// <b>Gyldighet over tid.</b> Datoen er oppslagsdatoen (standard: i dag) for den gjeldende versjonen, og versjonens egen dato
/// (konsolidert dato, ellers ikrafttredelse) for en eldre versjon — det departementet som hadde ansvaret da teksten gjaldt. En ressortkant
/// til et departement som senere er nedlagt, er svaret for datoer før nedleggelsen (kantens <c>gyldig_til</c> er nedleggelsen, #395), og
/// svaret sier at aktøren er nedlagt. Lovdatas felt sier bare hvem som har ansvaret i dag, og brukes for en eldre versjon med det i merknaden.
/// </para>
/// <para>
/// <b>Forskrifter.</b> Ressortvedtakene fordeler lover, ikke forskrifter, så for en forskrift finnes ingen ressortkant med dens egen ELI.
/// Departementet for hjemmelsloven brukes IKKE (det ville vært en gjetning: en forskrift kan ha flere hjemmelslover). Lovdatas felt er da
/// eneste kilde, merket sekundær.
/// </para>
/// <para>
/// <b>«statsforvalteren»</b> og <b>«kommunen»</b> har fast referent: klassen statsforvalter og klassen kommune (#384). Et svar herfra sier
/// hva slags aktør omtalen gjelder, ikke hvilken (hvilken kommune i den enkelte saken er #314). Sikkerhet og omfang er vurdert i
/// juristrunden på PR-en (CLAUDE.md §23).
/// </para>
/// <para>
/// Svarene lagres ikke (samme prinsipp som de avledede svarene i #345): de regnes ut ved oppslag, så en ressortkant som godkjennes i
/// forslagskøen, slår igjennom straks.
/// </para>
/// </summary>
public sealed class GeneriskStandardregelTjeneste(RegelIdeDbContext db)
{
    public const string AnsvarligDepartement = "ansvarlig_departement";
    public const string KlasseStatsforvalter = "klasse_statsforvalter";
    public const string KlasseKommune = "klasse_kommune";

    /// <summary>Klassen «statsforvalter» finnes på termen (fast term, unik indeks <c>ux_begreper_nodebegrep_fast_term</c>).</summary>
    public const string StatsforvalterKlasseTerm = "statsforvalter";

    public static readonly IReadOnlyList<GeneriskStandardregel> Alle =
    [
        new("departementet", AnsvarligDepartement, "Ansvarlig departement",
            "«departementet» i en lov betyr det departementet som har forvaltningsansvaret for loven. Eksempel: «departementet» i "
            + "reindriftsloven er Landbruks- og matdepartementet. Svaret er en avledet slutning fra ressortfordelingen (organiseringsvedtakene); "
            + "når den ikke er vurdert for loven, brukes Lovdatas felt «Ansvarlig departement», merket sekundær kilde.",
            "Ressortfordelingen ved kongelig resolusjon: Kongen i statsråd fordeler forvaltningsansvaret for lovene mellom departementene.",
            Strukturkanter.Avledet),
        new("statsforvalteren", KlasseStatsforvalter, "Klassen statsforvalter",
            "«statsforvalteren» betyr en statsforvalter — klassen, ikke én bestemt. Hvilken statsforvalter det er i en sak, avgjøres av "
            + "saken (stedlig tilhørighet), ikke av lovteksten. Eksempel: «statsforvalteren kan gi pålegg» gjelder den statsforvalteren saken hører under.",
            "Klassen statsforvalter, med hjemmelen klassen har.",
            Strukturkanter.Avledet),
        new("kommunen", KlasseKommune, "Klassen kommune",
            "«kommunen» betyr en kommune — klassen, ikke én bestemt. Hvilken kommune det er i en sak, avgjøres av saken. Eksempel: "
            + "«kommunen skal sørge for» gjelder hver kommune; medlemmene er utledet av områderegisteret.",
            "Klassen kommune (kommuneloven § 2-1 første ledd).",
            Strukturkanter.Avledet),
    ];

    public static GeneriskStandardregel? For(string term) =>
        Alle.FirstOrDefault(r => r.Term == GeneriskNavneformTjeneste.NormaliserTerm(term));

    private readonly Dictionary<(string, Guid, DateOnly?), StandardregelSvar?> _cache = [];

    /// <summary>
    /// Svaret fra standardregelen for <paramref name="term"/> i <paramref name="rettskildeId"/>, eller null når termen ikke har en regel.
    /// <paramref name="dato"/> null = oppslagsdatoen regnes ut fra versjonen (se klassekommentaren).
    /// </summary>
    public async Task<StandardregelSvar?> LosAsync(string term, Guid rettskildeId, DateOnly? dato = null, CancellationToken ct = default)
    {
        var regel = For(term);
        if (regel is null) return null;
        var nokkel = (regel.Regelkode, rettskildeId, dato);
        if (_cache.TryGetValue(nokkel, out var svar)) return svar;
        svar = regel.Regelkode switch
        {
            AnsvarligDepartement => await DepartementAsync(regel, rettskildeId, dato, ct),
            KlasseKommune => await FastKlasseAsync(regel, await db.Begreper.FirstOrDefaultAsync(
                b => b.Medlemskapsregel == Klasseregler.OmraderegisterKommune && b.Entitetsstatus == "gjeldende", ct), rettskildeId, dato, ct),
            KlasseStatsforvalter => await FastKlasseAsync(regel, await db.Begreper.FirstOrDefaultAsync(
                b => b.Begrepskategori == Nodetyper.Klasse && b.Term == StatsforvalterKlasseTerm && b.LovkildeId == null
                     && b.Omradetype == null && b.Entitetsstatus == "gjeldende", ct), rettskildeId, dato, ct),
            _ => null,
        };
        _cache[nokkel] = svar;
        return svar;
    }

    private static StandardregelSvar IkkeLost(GeneriskStandardregel r, DateOnly d, string forklaring, List<StandardregelPremiss> premisser,
        List<string> merknader, string utfall = StandardregelUtfall.IkkeLost) =>
        new(r.Term, r.Regelkode, r.Navn, utfall, null, null, null, r.Kunnskapsstatus, null, d, null, null, forklaring, premisser, merknader);

    /// <summary>
    /// [Ny, juristrunden på #394 (runde 1, 2026-10-24)] Når kilden selv sier hva ordet betyr, går kilden foran regelen: regelen gir da
    /// «ikke avklart» med grunnen, og en manuell binding avgjør. Fire sperrer, i rekkefølge:
    /// (a) kilden definerer ordet («Med «Statsforvalteren» forstås …», «med kommune forstås Longyearbyen lokalstyre»);
    /// (b) for «statsforvalteren»: kilden navngir ett embete («Statsforvalteren i Trøndelag avgjør …») — juristen målte 92 noder i 26 kilder;
    /// (c) kilden er et virksomhetsdokument (eid av én aktør, typisk en kommune): «kommunen» er da utstederen (Bergen kommunes retningslinjer);
    /// (d) kilden gjelder Svalbard, Jan Mayen eller Longyearbyen, som ikke er inndelt i kommuner og fylker (Longyearbyen lokalstyre er ikke en
    /// kommune; Jan Mayen ligger under Statsforvalteren i Nordland).
    /// </summary>
    private async Task<string?> SperreAsync(GeneriskStandardregel r, Guid rettskildeId, CancellationToken ct)
    {
        var rk = await db.Rettskilder.Where(x => x.Id == rettskildeId).Select(x => new { x.Tittel, x.Kortnavn, x.VirksomhetId }).FirstOrDefaultAsync(ct);
        if (rk is null) return "Fant ikke rettskilden.";
        var stamme = r.Regelkode == KlasseKommune ? "kommun" : "statsforvalt";
        var definisjon = $@"\m(med|ved)\s+«?{stamme}[a-zæøå]*»?\s+(menes|meinast|forstås)";
        if (await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende" && n.Tekst != null
                && Regex.IsMatch(n.Tekst, definisjon, RegexOptions.IgnoreCase), ct))
        {
            return $"Kilden definerer selv hva «{r.Term}» betyr. Definisjonen går foran standardregelen — bind manuelt etter den.";
        }
        if (r.Regelkode == KlasseStatsforvalter)
        {
            var navngitt = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende" && n.Tekst != null
                    && Regex.IsMatch(n.Tekst, @"Statsforvalteren i [A-ZÆØÅ]"))
                .Select(n => n.Tekst!).Take(20).ToListAsync(ct);
            if (navngitt.Count > 0)
            {
                var navn = navngitt.SelectMany(t => System.Text.RegularExpressions.Regex.Matches(t, @"Statsforvalteren i [A-ZÆØÅ][\p{L}]+(?:(?:,| og) [A-ZÆØÅ][\p{L}]+)*")
                    .Select(m => m.Value)).Distinct().Take(3);
                return $"Kilden navngir et bestemt embete ({string.Join(", ", navn)}). «{r.Term}» kan vise tilbake til det — bind manuelt.";
            }
        }
        if (rk.VirksomhetId is { } eier)
        {
            var navn = await db.Virksomheter.Where(v => v.Id == eier).Select(v => v.Navn).FirstOrDefaultAsync(ct);
            return $"Kilden er et virksomhetsdokument eid av {navn}. «{r.Term}» kan være utstederen selv — bind manuelt.";
        }
        var tittel = $"{rk.Tittel} {rk.Kortnavn}";
        if (System.Text.RegularExpressions.Regex.IsMatch(tittel, @"Svalbard|Jan Mayen|Longyearbyen", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return "Kilden gjelder Svalbard, Jan Mayen eller Longyearbyen, som ikke er inndelt i kommuner og fylker — bind manuelt.";
        }
        return null;
    }

    private async Task<StandardregelSvar> FastKlasseAsync(GeneriskStandardregel r, BegrepEntitet? klasse, Guid rettskildeId, DateOnly? dato,
        CancellationToken ct)
    {
        var d = dato ?? DateOnly.FromDateTime(DateTime.Today);
        if (await SperreAsync(r, rettskildeId, ct) is { } sperre) return IkkeLost(r, d, sperre, [], []);
        if (klasse is null)
        {
            return IkkeLost(r, d, $"Klassen regelen peker på, finnes ikke i modellen. Ingen gjettet fallback.", [], []);
        }
        var hjemmelId = klasse.LovkildeId;
        // Klassene har hjemmelen som verksnivå-eId (lovens ELI + bestemmelsen); rettskilden finnes da på ELI-en (gjeldende versjon).
        if (hjemmelId is null && klasse.LovreferanseEid is { } eid && eid.IndexOf("/nor/", StringComparison.Ordinal) is var i and > 0)
        {
            var eli = eid[..(i + 4)];
            hjemmelId = await db.Rettskilder.Where(x => x.Eli == eli && x.Entitetsstatus == "gjeldende").Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        }
        // [ENDRET etter juristrunden på #394, runde 2] Ingen gjettet status: en klasse uten godkjenning (registrert før #385) vises som
        // «ikke vurdert», ikke som godkjent (docs/33 §5.8), og hjemmelen til en klasse uten vurdert kunnskapsstatus vises som «hjemmel ikke
        // vurdert» — klassen statsforvalter har i dag reindriftsloven § 62 fjerde ledd, som ikke er grunnlaget for klassen.
        var vurdert = klasse.Kunnskapsstatus is not null;
        var premiss = new StandardregelPremiss("klasse",
            $"Klassen «{klasse.Term}»" + (klasse.LovreferanseEid is null ? " (uten hjemmel i korpus)" : vurdert ? "" : " (hjemmel ikke vurdert)"),
            Status: klasse.Godkjenning, Kunnskapsstatus: klasse.Kunnskapsstatus, HjemmelRettskildeId: vurdert ? hjemmelId : null,
            HjemmelEid: vurdert ? klasse.LovreferanseEid : null);
        return new StandardregelSvar(r.Term, r.Regelkode, r.Navn, StandardregelUtfall.Lost, StandardregelRute.FastReferent,
            Kantnode.Begrep(klasse.Id), new KantnodeVisning("begrep", klasse.Id, klasse.Term, klasse.Begrepskategori), r.Kunnskapsstatus,
            "primaer", d, null, null, $"«{r.Term}» betyr klassen {klasse.Term} (standardregel).", [premiss],
            // [Ny, juristrunden på #394] En slutning er ikke sterkere enn det svakeste premisset (profilsiden).
            klasse.Kunnskapsstatus is null
                ? [$"Klassen «{klasse.Term}» har ingen vurdert kunnskapsstatus" + (klasse.LovreferanseEid is null ? " og ingen hjemmel." : $" (hjemmel: {klasse.LovreferanseEid}).")
                   + " Svaret er ikke sterkere enn det."]
                : []);
    }

    private async Task<StandardregelSvar> DepartementAsync(GeneriskStandardregel r, Guid rettskildeId, DateOnly? dato, CancellationToken ct)
    {
        var rk = await db.Rettskilder.Where(x => x.Id == rettskildeId)
            .Select(x => new { x.Eli, x.Entitetsstatus, x.KonsolidertDato, x.Ikrafttredelse, x.AnsvarligDepartement, Tittel = x.Kortnavn ?? x.Tittel })
            .FirstOrDefaultAsync(ct) ?? throw new ArgumentException($"Fant ingen rettskilde med id '{rettskildeId}'. Ingen gjettet fallback.");
        var merknader = new List<string>();
        var premisser = new List<StandardregelPremiss>();
        var iDag = DateOnly.FromDateTime(DateTime.Today);
        var eldreVersjon = rk.Entitetsstatus != "gjeldende";
        var d = dato ?? (eldreVersjon ? rk.KonsolidertDato ?? rk.Ikrafttredelse ?? iDag : iDag);
        if (dato is null && eldreVersjon)
        {
            merknader.Add(rk.KonsolidertDato is not null || rk.Ikrafttredelse is not null
                ? $"Eldre versjon: oppslaget gjelder versjonens dato ({d:yyyy-MM-dd})."
                : "Eldre versjon uten dato: oppslaget gjelder i dag.");
        }

        // ---- 1. Ressortfordelingen (F forvaltning med lovens ELI) ----
        var kanter = rk.Eli is null ? [] : await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Oppgave && k.Typekode == "forvaltning" && k.ObjektEli == rk.Eli && k.FraVirksomhetId != null)
            .Select(k => new { k.Id, k.FraVirksomhetId, k.Objekt, k.Status, k.Kunnskapsstatus, k.GyldigFra, k.GyldigTil, k.HjemmelRettskildeId, k.HjemmelEid })
            .ToListAsync(ct);
        bool Gjelder(DateOnly? fra, DateOnly? til) => (fra is null || fra <= d) && (til is null || til >= d);
        var hele = kanter.Where(k => k.Objekt is not null && DepartementLivslopTjeneste.ErHeleLoven(k.Objekt)).ToList();
        var paDato = hele.Where(k => Gjelder(k.GyldigFra, k.GyldigTil)).ToList();
        var vurderte = paDato.Where(k => k.Status is Strukturkanter.Godkjent or Strukturkanter.FagligVurdert).ToList();
        var forslag = paDato.Where(k => k.Status is Strukturkanter.MaskineltForeslatt or Strukturkanter.ManueltForeslatt).ToList();
        var deler = kanter.Where(k => k.Objekt is not null && !DepartementLivslopTjeneste.ErHeleLoven(k.Objekt) && Gjelder(k.GyldigFra, k.GyldigTil)).ToList();

        var navn = new Dictionary<Guid, (string Navn, string? Livsstatus, DateOnly? GyldigTil)>();
        async Task<(string Navn, string? Livsstatus, DateOnly? GyldigTil)> NavnAsync(Guid id)
        {
            if (navn.TryGetValue(id, out var n)) return n;
            var v = await db.Virksomheter.Where(x => x.Id == id).Select(x => new { x.Navn, x.Livsstatus, x.GyldigTil }).FirstAsync(ct);
            n = (await new VirksomhetVisningsnavnTjeneste(db).ForAsync(id, ct) ?? v.Navn, v.Livsstatus, v.GyldigTil);
            navn[id] = n;
            return n;
        }
        StandardregelPremiss Kantpremiss(Guid id, string dept, string? objekt, string status, string? ks, Guid? hId, string? hEid) =>
            new("ressortkant", $"{dept} har forvaltningsansvaret for {objekt ?? "loven"}", id, status, ks, hId, hEid);

        foreach (var k in deler.Where(k => k.Status != Strukturkanter.Avvist))
        {
            merknader.Add($"En del av loven er lagt til {(await NavnAsync(k.FraVirksomhetId!.Value)).Navn} («{Avkort(k.Objekt!)}», "
                + $"{GodkjenningTekst(k.Status)}). «departementet» i den delen kan bety det departementet — bind den delen manuelt.");
        }

        var lovdata = (rk.AnsvarligDepartement ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList();
        var lovdataPremiss = lovdata.Count == 0 ? null : new StandardregelPremiss("lovdata_felt",
            $"Lovdata oppgir «Ansvarlig departement»: {string.Join(", ", lovdata)}", Dokumentasjon: "sekundaer");

        if (vurderte.Count > 0)
        {
            var referenter = vurderte.Select(k => k.FraVirksomhetId!.Value).Distinct().ToList();
            foreach (var k in vurderte)
            {
                premisser.Add(Kantpremiss(k.Id, (await NavnAsync(k.FraVirksomhetId!.Value)).Navn, k.Objekt, k.Status, k.Kunnskapsstatus,
                    k.HjemmelRettskildeId, k.HjemmelEid));
            }
            if (referenter.Count > 1)
            {
                return IkkeLost(r, d, "Ressortfordelingen gir flere departementer for hele loven på datoen. Ingen er valgt.", premisser, merknader,
                    StandardregelUtfall.Flertydig);
            }
            var vid = referenter[0];
            var dept = await NavnAsync(vid);
            var k0 = vurderte.First(k => k.FraVirksomhetId == vid);
            if (lovdata.Count > 0 && !lovdata.Contains(dept.Navn, StringComparer.OrdinalIgnoreCase) && !eldreVersjon)
            {
                merknader.Add($"Lovdata oppgir {string.Join(", ", lovdata)} (sekundær kilde) — avviker fra ressortfordelingen.");
            }
            return new StandardregelSvar(r.Term, r.Regelkode, r.Navn, StandardregelUtfall.Lost, StandardregelRute.Ressortfordeling,
                Kantnode.Virksomhet(vid), new KantnodeVisning("virksomhet", vid, dept.Navn, null), r.Kunnskapsstatus, "primaer", d,
                k0.GyldigFra, k0.GyldigTil,
                $"«departementet» betyr {dept.Navn}: ressortfordelingen gir departementet forvaltningsansvaret for loven "
                + $"({GodkjenningTekst(k0.Status)}).", premisser, merknader)
            { ReferentNedlagt = dept.Livsstatus == "nedlagt" ? dept.GyldigTil : null };
        }

        // Ingen vurdert ressortkant. Forslagene brukes ikke som svar, men sies.
        var forslagNavn = new List<string>();
        foreach (var k in forslag)
        {
            var n = (await NavnAsync(k.FraVirksomhetId!.Value)).Navn;
            forslagNavn.Add(n);
            premisser.Add(Kantpremiss(k.Id, n, k.Objekt, k.Status, k.Kunnskapsstatus, k.HjemmelRettskildeId, k.HjemmelEid) with
            {
                Tekst = $"Forslag (ikke vurdert): {n} har forvaltningsansvaret for {k.Objekt ?? "loven"}",
            });
        }
        if (forslagNavn.Count > 0)
        {
            merknader.Add($"Et ressortforslag venter på vurdering i forslagskøen: {string.Join(", ", forslagNavn.Distinct())}. Det brukes ikke før "
                + "det er faglig vurdert eller godkjent.");
        }
        else if (rk.Eli is not null && hele.Count > 0)
        {
            // Siste ressortvedtak for loven gjelder ikke på datoen — typisk et nedlagt departement uten lest etterfølger (#395).
            var siste = hele.Where(k => k.GyldigFra is null || k.GyldigFra <= d).OrderByDescending(k => k.GyldigFra).FirstOrDefault();
            if (siste is not null)
            {
                var n = await NavnAsync(siste.FraVirksomhetId!.Value);
                merknader.Add($"Siste ressortvedtak i korpuset for loven gjaldt {n.Navn}"
                    + (siste.GyldigTil is { } t ? $" til {t:yyyy-MM-dd}" : "")
                    + (n.Livsstatus == "nedlagt" ? " (nedlagt)" : "") + "; vedtaket etter det er ikke lest.");
            }
        }

        // ---- 2. Lovdatas felt, sekundær kilde ----
        if (lovdataPremiss is not null) premisser.Add(lovdataPremiss);
        if (lovdata.Count == 0)
        {
            return IkkeLost(r, d, "Ingen vurdert ressortfordeling for rettskilden, og Lovdata oppgir ikke ansvarlig departement.", premisser, merknader);
        }
        if (lovdata.Count > 1)
        {
            return IkkeLost(r, d, $"Ingen vurdert ressortfordeling, og Lovdata oppgir flere departementer ({string.Join(", ", lovdata)}). Ingen er valgt.",
                premisser, merknader, StandardregelUtfall.Flertydig);
        }
        var id = await new VirksomhetOppslagTjeneste(db).FinnVirksomhetIdForNavnAsync(lovdata[0]);
        if (id is null)
        {
            // Et gammelt navn: livsløpet (#395) kan ha registrert det som utgått navneform for et levende departement. Det sies, men brukes
            // ikke: et navn kobles aldri til en aktør på navnet alene (navn gjenbrukes, #395).
            var lower = lovdata[0].ToLower();
            var utgatt = await db.Begreper.Where(b => b.Navneformgrunn == "utgatt" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                    && b.Term.ToLower() == lower)
                .Join(db.Virksomheter, b => b.VirksomhetReferanseId, v => v.Id, (b, v) => new { v.Navn, v.Livsstatus, b.Godkjenning, b.GyldigTil })
                .ToListAsync(ct);
            foreach (var u in utgatt)
            {
                merknader.Add($"«{lovdata[0]}» er registrert som utgått navn for {u.Navn}"
                    + (u.GyldigTil is { } gt ? $" (til {gt:yyyy-MM-dd})" : "") + (u.Livsstatus == "nedlagt" ? ", som er nedlagt" : "")
                    + $" — {(u.Godkjenning is null or Strukturkanter.Godkjent ? "godkjent" : "forslag")}. Bind manuelt om det er riktig.");
            }
            return IkkeLost(r, d, $"Lovdata oppgir «{lovdata[0]}», men det navnet finnes ikke som et departement i katalogen i dag. Ingen gjettet fallback.",
                premisser, merknader);
        }
        var lv = await NavnAsync(id.Value);
        if (forslagNavn.Count > 0 && !forslagNavn.Contains(lv.Navn, StringComparer.OrdinalIgnoreCase))
        {
            merknader.Add($"Ressortforslaget ({string.Join(", ", forslagNavn.Distinct())}) avviker fra Lovdata.");
        }
        if (eldreVersjon) merknader.Add("Lovdatas felt sier hvem som har ansvaret i dag, ikke på versjonens dato.");
        return new StandardregelSvar(r.Term, r.Regelkode, r.Navn, StandardregelUtfall.Lost, StandardregelRute.Lovdata,
            Kantnode.Virksomhet(id.Value), new KantnodeVisning("virksomhet", id.Value, lv.Navn, null), r.Kunnskapsstatus, "sekundaer", d,
            null, null,
            $"«departementet» betyr {lv.Navn} etter Lovdatas felt «Ansvarlig departement» (sekundær kilde): "
            + (forslagNavn.Count > 0 ? "ressortforslaget for loven er ikke vurdert ennå."
                : rk.Eli is not null && rk.Eli.Contains("/lov/", StringComparison.Ordinal) ? "ressortvedtakene i korpuset gir ikke loven til noe departement på datoen."
                : "ressortvedtakene fordeler lover, ikke forskrifter og andre kilder."), premisser, merknader);
    }

    /// <summary>
    /// Målingen: for hver gjeldende primærrettskilde der <paramref name="term"/> står som eget ord (uten skille på store og små bokstaver),
    /// hva regelen gir. Rettskilder med en manuell binding regnes også med regelen alene — tallet sier hva regelen klarer.
    /// </summary>
    public async Task<StandardregelDekning> DekningAsync(string term, int eksempler = 15, CancellationToken ct = default)
    {
        var t = GeneriskNavneformTjeneste.NormaliserTerm(term);
        if (For(t) is null) throw new ArgumentException($"«{t}» har ingen standardregel. Reglene: {string.Join(", ", Alle.Select(x => x.Term))}.");
        var monster = $@"\m{System.Text.RegularExpressions.Regex.Escape(t)}\M";
        var ider = await db.Database.SqlQueryRaw<Guid>(
            """
            SELECT DISTINCT r."Id" AS "Value" FROM rettskilder r JOIN rettskilde_noder n ON n.rettskilde_id = r."Id"
            WHERE r.entitetsstatus = 'gjeldende' AND r.importrolle = 'primaer' AND n.entitetsstatus = 'gjeldende' AND n.tekst ~* {0}
            """, monster).ToListAsync(ct);
        int ressort = 0, lovdata = 0, fast = 0, ikke = 0, fler = 0, venter = 0;
        var grunner = new Dictionary<string, int>();
        var rader = new List<StandardregelDekningRad>();
        var titler = await db.Rettskilder.Where(r => ider.Contains(r.Id)).Select(r => new { r.Id, T = r.Kortnavn ?? r.Tittel }).ToDictionaryAsync(r => r.Id, r => r.T, ct);
        foreach (var id in ider)
        {
            var s = (await LosAsync(t, id, null, ct))!;
            if (s.Merknader.Any(m => m.StartsWith("Et ressortforslag venter", StringComparison.Ordinal))) venter++;
            switch (s.Utfall, s.Rute)
            {
                case (StandardregelUtfall.Lost, StandardregelRute.Ressortfordeling): ressort++; break;
                case (StandardregelUtfall.Lost, StandardregelRute.Lovdata): lovdata++; break;
                case (StandardregelUtfall.Lost, _): fast++; break;
                case (StandardregelUtfall.Flertydig, _): fler++; break;
                default: ikke++; break;
            }
            if (s.Utfall != StandardregelUtfall.Lost)
            {
                var g = s.Forklaring.StartsWith("Ingen vurdert ressortfordeling for rettskilden, og Lovdata oppgir ikke", StringComparison.Ordinal) ? "ingen_kilde"
                    : s.Forklaring.Contains("finnes ikke som et departement", StringComparison.Ordinal) ? "navnet_finnes_ikke_i_katalogen"
                    : s.Forklaring.Contains("flere departementer", StringComparison.Ordinal) ? "flere_departementer"
                    : s.Forklaring.StartsWith("Kilden definerer", StringComparison.Ordinal) ? "kilden_definerer_ordet"
                    : s.Forklaring.StartsWith("Kilden navngir", StringComparison.Ordinal) ? "kilden_navngir_embete"
                    : s.Forklaring.Contains("virksomhetsdokument", StringComparison.Ordinal) ? "virksomhetsdokument"
                    : s.Forklaring.Contains("Svalbard", StringComparison.Ordinal) ? "svalbard_jan_mayen_longyearbyen" : "annet";
                grunner[g] = grunner.GetValueOrDefault(g) + 1;
            }
            if (rader.Count < eksempler || s.Utfall != StandardregelUtfall.Lost && rader.Count < eksempler * 2)
            {
                rader.Add(new StandardregelDekningRad(id, titler.GetValueOrDefault(id) ?? "(ukjent)", s.Utfall, s.Rute, s.ReferentVisning?.Navn, s.Forklaring));
            }
        }
        return new StandardregelDekning(t, ider.Count, ressort, lovdata, fast, ikke, fler, venter, grunner, rader);
    }

    private static string GodkjenningTekst(string status) => status switch
    {
        Strukturkanter.Godkjent => "godkjent",
        Strukturkanter.FagligVurdert => "faglig vurdert",
        Strukturkanter.ManueltForeslatt => "manuelt foreslått",
        Strukturkanter.MaskineltForeslatt => "maskinelt foreslått",
        _ => status,
    };

    private static string Avkort(string s) => s.Length <= 80 ? s : s[..77] + "…";
}
