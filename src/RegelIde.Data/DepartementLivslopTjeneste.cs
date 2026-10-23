using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>Hvor i korpuset et utsagn står: rettskilden, noden og teksten.</summary>
public sealed record LivslopHjemmel(Guid RettskildeId, string Tittel, string? Kildeform, string Eid, string Tekst);

/// <summary>Ett navn i en identitets historie. <see cref="GittVed"/> er vedtaket som ga navnet (opprettelse eller navnebytte), null når
/// navnet var i bruk før det eldste vedtaket i korpuset. <see cref="EndretVed"/> er vedtaket som endret det bort.</summary>
public sealed record LivslopNavn(string Navn, DateOnly? Fra, DateOnly? Til, LivslopHjemmel? GittVed, LivslopHjemmel? EndretVed);

/// <summary>Ett departement over tid, slik organiseringsvedtakene beskriver det. <see cref="Nr"/> er bare et løpenummer i rapporten.
/// <see cref="KatalogVirksomhetId"/> er satt bare for en identitet som fortsatt finnes (ikke nedlagt) og hvis nåværende navn treffer
/// nøyaktig én aktør i katalogen.</summary>
public sealed record LivslopIdentitet(
    int Nr, IReadOnlyList<LivslopNavn> Navn, DateOnly? Opprettet, LivslopHjemmel? OpprettetVed, DateOnly? Nedlagt, LivslopHjemmel? NedlagtVed,
    Guid? KatalogVirksomhetId, string? KatalogNavn, int Ressortlover);

/// <summary>Én lov ett departement fikk forvaltningsansvaret for, med vedtaket og gyldighetstiden.</summary>
public sealed record LivslopRessort(int IdentitetNr, string Departement, string Objekt, string? Eli, bool HeleLoven, DateOnly Fra, DateOnly? Til,
    LivslopHjemmel Hjemmel, string Kunnskapsstatus);

/// <summary>AC7: Lovdatas «Ansvarlig departement» mot ressortfordelingen for én lov.</summary>
public sealed record RessortAvvik(string Eli, string Lovtittel, IReadOnlyList<string> LovdataDepartement, string Ressortdepartement,
    string? RessortKatalognavn, DateOnly Fra, LivslopHjemmel Hjemmel, string Utfall);

/// <summary>Ett ledd mønstrene kjente igjen som organisering, men ikke kunne lese sikkert. Rapporteres, ingenting gjettes.</summary>
public sealed record LivslopUlest(LivslopHjemmel Hjemmel, string Grunn);

/// <summary>Opprettelser og nedleggelser i SAMME vedtak — kandidater til R etterfolger. Teksten sier det ikke selv; det er en
/// fortolkning, og skrives derfor ikke (se klassekommentaren).</summary>
public sealed record EtterfolgerKandidat(LivslopHjemmel Vedtak, IReadOnlyList<string> Opprettet, IReadOnlyList<string> Nedlagt);

/// <summary>Hva som ble (eller ville blitt) skrevet som forslag.</summary>
public sealed record LivslopSkriving(int OpprettetNye, int OpprettetFantes, int NavneformerNye, int NavneformerFantes, int RessortNye,
    int RessortFantes, int Avvist, IReadOnlyList<string> Feil);

public sealed record LivslopRapport(
    bool Skrevet, int Vedtak, IReadOnlyList<LivslopIdentitet> Identiteter, IReadOnlyList<LivslopRessort> Ressortfordeling,
    IReadOnlyList<RessortAvvik> Avvik, IReadOnlyDictionary<string, int> AvvikPerUtfall, IReadOnlyList<EtterfolgerKandidat> EtterfolgerKandidater,
    IReadOnlyList<LivslopUlest> Ulest, IReadOnlyList<string> Utsatt, LivslopSkriving Skriving);

/// <summary>
/// [Ny, issue #395 del 3, 2026-10-23] Departementenes livsløp (pilot), satt sammen av organiseringsvedtakene i korpuset — konsoliderte og
/// kunngjorte — med hjemmel i vedtaket for hvert ledd.
/// <para>
/// <b>Identitet over tid, ikke navnetreff.</b> Navn gjenbrukes: «Arbeids- og sosialdepartementet» ble opprettet 2004, nedlagt 2006, og
/// «Arbeidsdepartementet» (opprettet 2010) endret navn til det samme i 2014; «Arbeids- og inkluderingsdepartementet» fantes 2006–2009 og
/// igjen fra 2022 (FOR-2004-10-01-1304, FOR-2005-12-16-1479, FOR-2009-12-18-1582, FOR-2013-12-13-1455, FOR-2021-12-22-3828). Et rent
/// navneoppslag mot katalogen ville koblet et vedtak fra 2005 til dagens departement. Tjenesten bygger derfor identitetene i
/// tidsrekkefølge: opprettelse gir en ny identitet, navnebytte fører samme identitet videre under nytt navn, nedleggelse avslutter den.
/// Bare en identitet som fortsatt lever, kobles til katalogen, og bare på sitt NÅVÆRENDE navn (registernavn eller gjeldende navneform,
/// nøyaktig ett treff). Nedlagte departementer finnes ikke i katalogen; de står i rapporten med hele historien, og hvordan de skal
/// representeres er Johanns valg.
/// </para>
/// <para>
/// <b>Hva som skrives (bare som forslag, aldri godkjent — forslagskøen, #313):</b>
/// </para>
/// <list type="bullet">
/// <item><b>R oppretter</b> fra Kongen i statsråd til departementet, når vedtaket er fastsatt ved kgl.res. og departementet lever.
/// docs/33 §4.3 (#352 beslutning 4): R <c>oppretter</c> er den gjennomførte handlingen; K <c>oppretting</c> er kompetansen til å opprette
/// (her Grunnloven § 12, som vedtakene har som hjemmel) og skrives ikke — den står ikke i vedtaket.</item>
/// <item><b>Navneform «utgått»</b> for hvert tidligere navn, på den levende aktøren, med hjemmel i navnevedtaket og gyldig fra/til.
/// Et navnebytte er samme aktør med nytt navn («Landbruksdepartementets navn endres til …»), ikke en etterfølger.</item>
/// <item><b>F forvaltning</b> (ressortfordeling): én kant per lov i vedleggene og i enkeltoverføringene, fra departementet, med lovteksten
/// som objekt og lovens ELI i <see cref="StrukturkantEntitet.ObjektEli"/>, gyldig fra vedtaket og til neste vedtak som gir samme hele lov
/// til et annet departement.</item>
/// </list>
/// <para>
/// R <c>avvikler</c> skrives ikke: alle nedlagte departementer mangler i katalogen. R <c>etterfolger</c> skrives ikke: ingen av vedtakene
/// sier at et nytt departement etterfølger et nedlagt — det er en slutning fra at de opprettes og nedlegges i samme vedtak, og står i
/// rapporten som kandidater.
/// </para>
/// </summary>
public sealed class DepartementLivslopTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanter)
{
    public const string SystemBruker = "system-departement-livslop";
    public const string OppdagelsesKilde = "monster:livslop";
    public const string KongenIStatsrad = "Kongen i statsråd";

    private sealed class Identitet
    {
        public int Nr;
        public List<(string Navn, DateOnly? Fra, DateOnly? Til, LivslopHjemmel? Gitt, LivslopHjemmel? Endret, string? Raa)> Navn = [];
        public DateOnly? Opprettet; public LivslopHjemmel? OpprettetVed;
        public DateOnly? Nedlagt; public LivslopHjemmel? NedlagtVed;
        public string Gjeldende => Navn[^1].Navn;
    }

    private sealed record Tildeling(Identitet Id, string NavnDaVedtaket, string Objekt, string? Eli, bool Hele, DateOnly Fra, LivslopHjemmel Hjemmel, bool Funksjonsord);

    public async Task<LivslopRapport> KjorAsync(bool skriv, string bruker = SystemBruker, CancellationToken ct = default)
    {
        // ---- Vedtakene: organiseringskilder med en livsløpstype ----
        var livsTyper = Disposisjonstyper.Livslopstyper.ToList();
        var kildeIder = await db.RettskildeDisposisjonstyper.Where(d => livsTyper.Contains(d.Disposisjonstype))
            .Select(d => d.RettskildeId).Distinct().ToListAsync(ct);
        var kilder = await db.Rettskilder
            .Where(r => kildeIder.Contains(r.Id) && r.Importrolle == "primaer" && r.Entitetsstatus == "gjeldende")
            .Select(r => new { r.Id, r.Tittel, r.Kildeform, r.Ikrafttredelse, r.Kunngjort, r.AnnetOmDokumentet, r.Eli })
            .ToListAsync(ct);

        var ulest = new List<LivslopUlest>();
        var utsatt = new List<string>();
        var vedtak = new List<(DateOnly Dato, Guid Id, string Tittel, string? Kildeform, bool KglRes, List<(LivslopHjemmel H, Livslopshendelse E)> Hendelser,
            List<(LivslopHjemmel H, string Dept, string Raa, bool Funksjonsord)> Vedlegg)>();

        foreach (var k in kilder)
        {
            var dato = k.Ikrafttredelse ?? (DateOnly.TryParse(k.Kunngjort, CultureInfo.InvariantCulture, out var kd) ? kd : (DateOnly?)null);
            var noder = await db.RettskildeNoder
                .Where(n => n.RettskildeId == k.Id && n.Entitetsstatus == "gjeldende" && !n.Opphevet)
                .OrderBy(n => n.Sorteringsrekkefolge)
                .Select(n => new { n.Id, n.ParentNodeId, n.Eid, n.NodeType, n.Overskrift, n.Tekst })
                .ToListAsync(ct);
            var hendelser = new List<(LivslopHjemmel, Livslopshendelse)>();
            var vedlegg = new List<(LivslopHjemmel, string, string, bool)>();
            var vedleggskapitler = new Dictionary<Guid, (string Dept, string Raa, bool F)>();

            foreach (var n in noder)
            {
                if (n.Overskrift is { } o && DepartementLivslopMonstre.LesVedleggsoverskrift(o) is { } vd) vedleggskapitler[n.Id] = vd;
            }
            foreach (var n in noder)
            {
                if (string.IsNullOrWhiteSpace(n.Tekst)) continue;
                var h = new LivslopHjemmel(k.Id, k.Tittel, k.Kildeform, n.Eid, n.Tekst);
                // Et punkt i et vedlegg («Oversikt over lover X skal ha forvaltningsansvaret for»): nærmeste vedleggskapittel oppover.
                Guid? p = n.ParentNodeId;
                (string Dept, string Raa, bool F)? kap = null;
                while (p is { } pid)
                {
                    if (vedleggskapitler.TryGetValue(pid, out var v)) { kap = v; break; }
                    p = noder.FirstOrDefault(x => x.Id == pid)?.ParentNodeId;
                }
                if (kap is { } kv)
                {
                    if (DepartementLivslopMonstre.LovDatokode(n.Tekst) is not null) vedlegg.Add((h, kv.Dept, kv.Raa, kv.F));
                    continue;
                }
                var lest = DepartementLivslopMonstre.LesLedd(n.Tekst, out var grunn);
                foreach (var e in lest) hendelser.Add((h, e));
                if (lest.Count == 0 && grunn is not null) ulest.Add(new(h, grunn));
            }
            // Tittelformen «Overføring av forvaltningsansvaret for … fra A til B» når leddet selv ikke ble lest.
            if (DepartementLivslopMonstre.LesOverforingstittel(k.Tittel) is { } tittelOverforing
                && !hendelser.Any(x => x.Item2.Type == DepartementLivslopMonstre.Overforing))
            {
                var forste = noder.FirstOrDefault(n => n.Tekst is not null && n.Tekst.Contains("forvaltningsansvar", StringComparison.OrdinalIgnoreCase));
                if (forste is not null) hendelser.Add((new LivslopHjemmel(k.Id, k.Tittel, k.Kildeform, forste.Eid, forste.Tekst!), tittelOverforing));
                else ulest.Add(new(new LivslopHjemmel(k.Id, k.Tittel, k.Kildeform, "", k.Tittel), "overføring i tittelen, men intet ledd nevner forvaltningsansvaret"));
            }
            if (hendelser.Count == 0 && vedlegg.Count == 0) continue;
            if (dato is null)
            {
                ulest.Add(new(new LivslopHjemmel(k.Id, k.Tittel, k.Kildeform, "", k.Tittel), "vedtaket har verken ikrafttredelse eller kunngjøringsdato"));
                continue;
            }
            var kglRes = k.AnnetOmDokumentet is { } a && (a.Contains("kgl.res", StringComparison.OrdinalIgnoreCase) || a.Contains("kgl. res", StringComparison.OrdinalIgnoreCase));
            vedtak.Add((dato.Value, k.Id, k.Tittel, k.Kildeform, kglRes, hendelser, vedlegg));
        }

        // ---- Identitetene i tidsrekkefølge ----
        var identiteter = new List<Identitet>();
        var levende = new Dictionary<string, Identitet>(StringComparer.Ordinal);
        var tildelinger = new List<Tildeling>();
        var etterfolgere = new List<EtterfolgerKandidat>();
        var opprettetKanter = new List<(Identitet Id, DateOnly Dato, LivslopHjemmel H, bool Normalisert)>();

        Identitet Hent(string navn, LivslopHjemmel h)
        {
            if (levende.TryGetValue(navn, out var i)) return i;
            // Navnet var i bruk før det eldste vedtaket vi har: en identitet uten kjent start.
            i = new Identitet { Nr = identiteter.Count + 1 };
            i.Navn.Add((navn, null, null, null, null, null));
            identiteter.Add(i);
            levende[navn] = i;
            return i;
        }

        // Duplikat: samme vedtak finnes ikke to ganger (ELI-nøkkelen), men to ulike vedtak kan si det samme (FOR-2004-06-18-916 og
        // FOR-2004-10-01-1304 sier begge at Landbruksdepartementets navn endres). Det utsatte («iverksettes fra det tidspunkt …
        // bestemmer») brukes ikke som hendelse; det iverksettende gjør.
        foreach (var v in vedtak.OrderBy(v => v.Dato).ThenBy(v => v.Tittel, StringComparer.Ordinal))
        {
            var opprettetHer = new List<string>();
            var nedlagtHer = new List<string>();
            foreach (var (h, e) in v.Hendelser.Where(x => x.E.Type == DepartementLivslopMonstre.Nedleggelse))
            {
                foreach (var (navn, _) in e.Navn)
                {
                    var i = Hent(navn, h);
                    i.Nedlagt = v.Dato; i.NedlagtVed = h;
                    i.Navn[^1] = i.Navn[^1] with { Til = v.Dato.AddDays(-1) };
                    levende.Remove(navn);
                    nedlagtHer.Add(navn);
                }
            }
            foreach (var (h, e) in v.Hendelser.Where(x => x.E.Type == DepartementLivslopMonstre.Navnebytte))
            {
                var (fra, fraRaa) = e.Fra!.Value;
                var (til, tilRaa) = e.Til!.Value;
                if (e.Utsatt) { utsatt.Add($"{v.Tittel}: «{fraRaa}» → «{tilRaa}» (utsatt iverksetting, {h.Eid})"); continue; }
                if (levende.TryGetValue(til, out var alt) && alt.Gjeldende == til && alt.Navn[^1].Gitt is { } gitt && gitt.RettskildeId != h.RettskildeId)
                {
                    continue; // allerede gjennomført av et tidligere vedtak med samme navnebytte
                }
                var i = Hent(fra, h);
                i.Navn[^1] = i.Navn[^1] with { Til = v.Dato.AddDays(-1), Endret = h };
                i.Navn.Add((til, v.Dato, null, h, null, tilRaa));
                levende.Remove(fra);
                levende[til] = i;
            }
            foreach (var (h, e) in v.Hendelser.Where(x => x.E.Type == DepartementLivslopMonstre.Oppretting))
            {
                foreach (var (navn, raa) in e.Navn)
                {
                    if (levende.ContainsKey(navn))
                    {
                        ulest.Add(new(h, $"«{navn}» opprettes, men et departement med samme navn finnes fra før uten å være nedlagt"));
                        continue;
                    }
                    var i = new Identitet { Nr = identiteter.Count + 1, Opprettet = v.Dato, OpprettetVed = h };
                    i.Navn.Add((navn, v.Dato, null, h, null, raa));
                    identiteter.Add(i);
                    levende[navn] = i;
                    opprettetHer.Add(navn);
                    if (v.KglRes) opprettetKanter.Add((i, v.Dato, h, raa != navn));
                }
            }
            if (opprettetHer.Count > 0 && nedlagtHer.Count > 0)
            {
                etterfolgere.Add(new EtterfolgerKandidat(v.Hendelser[0].H with { Eid = "", Tekst = v.Tittel }, opprettetHer, nedlagtHer));
            }
            foreach (var (h, e) in v.Hendelser.Where(x => x.E.Type == DepartementLivslopMonstre.Overforing))
            {
                var til = Hent(e.Til!.Value.Navn, h);
                var datokode = DepartementLivslopMonstre.LovDatokode(e.Objekt!);
                tildelinger.Add(new Tildeling(til, e.Til.Value.Navn, e.Objekt!, datokode is null ? null : LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out _),
                    ErHeleLoven(e.Objekt!), v.Dato, h, Funksjonsord: true));
            }
            foreach (var (h, dept, _, funksjonsord) in v.Vedlegg)
            {
                var i = Hent(dept, h);
                var datokode = DepartementLivslopMonstre.LovDatokode(h.Tekst)!;
                tildelinger.Add(new Tildeling(i, dept, h.Tekst.Trim(), LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out _),
                    ErHeleLoven(h.Tekst), v.Dato, h, funksjonsord));
            }
        }

        // ---- Gyldighet for ressortfordelingen: en hel lov til ett departement gjelder til neste vedtak gir den samme hele loven til et annet ----
        var tilDato = new Dictionary<Tildeling, DateOnly?>();
        foreach (var gruppe in tildelinger.Where(t => t.Eli is not null && t.Hele).GroupBy(t => t.Eli!))
        {
            var sortert = gruppe.OrderBy(t => t.Fra).ToList();
            for (var x = 0; x < sortert.Count; x++)
            {
                var neste = sortert.Skip(x + 1).FirstOrDefault(t => t.Id != sortert[x].Id && t.Fra > sortert[x].Fra);
                tilDato[sortert[x]] = neste?.Fra.AddDays(-1);
            }
        }
        // En nedlagt identitet har ikke ansvaret etter nedleggelsen, selv om intet vedtak vi leser ga loven videre.
        DateOnly? Til(Tildeling t) => tilDato.GetValueOrDefault(t) ?? (t.Id.Nedlagt is { } nd && nd > t.Fra ? nd.AddDays(-1) : null);

        // ---- Katalogen: bare levende identiteter, på nåværende navn ----
        var katalog = new Dictionary<Identitet, (Guid Id, string Navn)>();
        foreach (var i in identiteter.Where(i => i.Nedlagt is null))
        {
            if (await FinnAktorAsync(i.Gjeldende, ct) is { } a) katalog[i] = a;
        }

        // ---- AC7: Lovdatas «Ansvarlig departement» mot siste ressortvedtak for hele loven ----
        var avvik = new List<RessortAvvik>();
        var siste = tildelinger.Where(t => t.Eli is not null && t.Hele).GroupBy(t => t.Eli!).Select(g => g.OrderBy(t => t.Fra).Last()).ToList();
        var lover = await db.Rettskilder.Where(r => r.Importrolle == "primaer" && r.Entitetsstatus == "gjeldende" && r.Eli != null
                && siste.Select(s => s.Eli).Contains(r.Eli))
            .Select(r => new { r.Eli, r.Tittel, r.AnsvarligDepartement }).ToListAsync(ct);
        foreach (var s in siste)
        {
            var lov = lover.FirstOrDefault(l => l.Eli == s.Eli);
            var ressortNavn = s.Id.Gjeldende;
            var hj = s.Hjemmel;
            if (lov is null)
            {
                avvik.Add(new(s.Eli!, s.Objekt, [], ressortNavn, null, s.Fra, hj, "lov_ikke_i_korpus"));
                continue;
            }
            var lovdata = lov.AnsvarligDepartement ?? [];
            if (s.Id.Nedlagt is not null)
            {
                avvik.Add(new(s.Eli!, lov.Tittel, lovdata, ressortNavn, null, s.Fra, hj, "siste_ressortvedtak_gjelder_nedlagt_departement"));
                continue;
            }
            if (!katalog.TryGetValue(s.Id, out var ressortAktor))
            {
                avvik.Add(new(s.Eli!, lov.Tittel, lovdata, ressortNavn, null, s.Fra, hj, "ressortdepartement_ikke_i_katalogen"));
                continue;
            }
            var lovdataAktorer = new List<Guid>();
            var ukjent = false;
            foreach (var d in lovdata)
            {
                if (await FinnAktorAsync(d, ct) is { } la) lovdataAktorer.Add(la.Id); else ukjent = true;
            }
            var utfall = lovdataAktorer.Contains(ressortAktor.Id) ? "samsvar"
                : ukjent && lovdataAktorer.Count == 0 ? "lovdata_departement_ikke_i_katalogen"
                : "avvik";
            avvik.Add(new(s.Eli!, lov.Tittel, lovdata, ressortNavn, ressortAktor.Navn, s.Fra, hj, utfall));
        }

        // ---- Skriving (bare forslag) ----
        var skriving = skriv ? await SkrivAsync(katalog, opprettetKanter, tildelinger, Til, bruker, ct) : TellUtenSkriving(katalog, opprettetKanter, tildelinger);

        var ressortRader = tildelinger.Select(t => new LivslopRessort(t.Id.Nr, t.NavnDaVedtaket, t.Objekt, t.Eli, t.Hele, t.Fra, Til(t), t.Hjemmel,
            t.Funksjonsord ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering)).ToList();
        var identitetRader = identiteter.Select(i => new LivslopIdentitet(i.Nr,
            i.Navn.Select(n => new LivslopNavn(n.Navn, n.Fra, n.Til, n.Gitt, n.Endret)).ToList(), i.Opprettet, i.OpprettetVed, i.Nedlagt, i.NedlagtVed,
            katalog.TryGetValue(i, out var kv) ? kv.Id : null, katalog.TryGetValue(i, out var kv2) ? kv2.Navn : null,
            tildelinger.Count(t => t.Id == i))).ToList();

        return new LivslopRapport(skriv, vedtak.Count, identitetRader, ressortRader, avvik,
            avvik.GroupBy(a => a.Utfall).ToDictionary(g => g.Key, g => g.Count()), etterfolgere, ulest, utsatt, skriving);
    }

    /// <summary>Hele loven, ikke enkeltparagrafer eller kapitler («Lov 17. juli 1925 nr. 11 om Svalbard, § 4» er en del).</summary>
    private static bool ErHeleLoven(string objekt) =>
        !objekt.Contains('§') && !objekt.Contains("kapittel", StringComparison.OrdinalIgnoreCase) && !objekt.Contains("kap.", StringComparison.OrdinalIgnoreCase);

    /// <summary>Nøyaktig én aktør med dette registernavnet (uten skille på store og små bokstaver), ellers nøyaktig én aktør med det som
    /// GJELDENDE navneform. Utgåtte navneformer telles ikke: de er nettopp de navnene som kan ha vært brukt om et annet departement.</summary>
    private async Task<(Guid Id, string Navn)?> FinnAktorAsync(string navn, CancellationToken ct)
    {
        var lower = navn.ToLower();
        var direkte = await db.Virksomheter.Where(v => v.Navn.ToLower() == lower).Select(v => new { v.Id, v.Navn }).Take(2).ToListAsync(ct);
        if (direkte.Count == 1) return (direkte[0].Id, direkte[0].Navn);
        if (direkte.Count > 1) return null;
        var via = await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                && b.Navneformgrunn == "gjeldende" && b.Term.ToLower() == lower)
            .Select(b => b.VirksomhetReferanseId!.Value).Distinct().Take(2).ToListAsync(ct);
        if (via.Count != 1) return null;
        var v = await db.Virksomheter.Where(x => x.Id == via[0]).Select(x => new { x.Id, x.Navn }).FirstAsync(ct);
        return (v.Id, v.Navn);
    }

    private static LivslopSkriving TellUtenSkriving(Dictionary<Identitet, (Guid Id, string Navn)> katalog,
        List<(Identitet Id, DateOnly Dato, LivslopHjemmel H, bool Normalisert)> opprettet, List<Tildeling> tildelinger) =>
        new(opprettet.Count(o => katalog.ContainsKey(o.Id)), 0,
            katalog.Keys.Sum(i => i.Navn.Count(n => !string.Equals(n.Navn, i.Gjeldende, StringComparison.OrdinalIgnoreCase) && n.Endret is not null)), 0,
            tildelinger.Count(t => katalog.ContainsKey(t.Id)), 0, 0, []);

    private async Task<LivslopSkriving> SkrivAsync(Dictionary<Identitet, (Guid Id, string Navn)> katalog,
        List<(Identitet Id, DateOnly Dato, LivslopHjemmel H, bool Normalisert)> opprettet, List<Tildeling> tildelinger,
        Func<Tildeling, DateOnly?> til, string bruker, CancellationToken ct)
    {
        int oNye = 0, oFantes = 0, nNye = 0, nFantes = 0, rNye = 0, rFantes = 0, avvist = 0;
        var feil = new List<string>();
        var kongen = await FinnAktorAsync(KongenIStatsrad, ct);

        async Task Opprett(NyStrukturkant ny, Action nye, Action fantes)
        {
            try
            {
                var r = await kanter.OpprettAsync(ny, bruker, ct);
                if (r.VarNy) nye();
                else if (r.Kant.Status == Strukturkanter.Avvist) avvist++;
                else fantes();
            }
            catch (ArgumentException ex)
            {
                db.ChangeTracker.Clear();
                feil.Add($"{ny.Kategori} {ny.Typekode} ({ny.HjemmelEid}): {ex.Message}");
            }
        }

        // R oppretter
        foreach (var (i, dato, h, normalisert) in opprettet.Where(o => katalog.ContainsKey(o.Id)))
        {
            if (kongen is null) { feil.Add($"R oppretter {i.Gjeldende}: fant ikke «{KongenIStatsrad}» i katalogen"); continue; }
            await Opprett(new NyStrukturkant(Strukturkanter.Relasjon, "oppretter", Kantnode.Virksomhet(kongen.Value.Id), Kantnode.Virksomhet(katalog[i].Id),
                HjemmelRettskildeId: h.RettskildeId, HjemmelEid: h.Eid, GyldigFra: dato,
                Kommentar: $"«{Avkort(h.Tekst)}»", Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: OppdagelsesKilde,
                // Kongen i statsråd følger av «Fastsatt ved kgl.res.», og ubestemt form («et Helse- og omsorgsdepartement») er normalisert.
                Kunnskapsstatus: Strukturkanter.Normalisering,
                Begrunnelse: "Regel: monster:livslop-opprettelse-av. Vedtaket er fastsatt ved kgl.res. (fra = Kongen i statsråd)"
                    + (normalisert ? "; navnet står i ubestemt form i vedtaket og er gjort bestemt." : ".")),
                () => oNye++, () => oFantes++);
        }

        // Navneform «utgått» for tidligere navn
        foreach (var (i, aktor) in katalog)
        {
            foreach (var n in i.Navn.Where(n => n.Endret is not null && !string.Equals(n.Navn, i.Gjeldende, StringComparison.OrdinalIgnoreCase)))
            {
                var finnes = await db.Begreper.IgnoreQueryFilters().AnyAsync(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == aktor.Id
                    && b.Term.ToLower() == n.Navn.ToLower(), ct);
                if (finnes) { nFantes++; continue; }
                var h = n.Endret!;
                var ordrett = h.Tekst.Contains(n.Navn, StringComparison.Ordinal);
                var b = new BegrepEntitet
                {
                    Id = Guid.NewGuid(),
                    Begrepskategori = "virksomhet",
                    VirksomhetReferanseId = aktor.Id,
                    Navneformgrunn = "utgatt",
                    Term = n.Navn,
                    Status = "publisert",
                    GyldigFra = n.Fra,
                    GyldigTil = n.Til,
                    LovkildeId = h.RettskildeId,
                    LovreferanseEid = h.Eid,
                    Kunnskapsstatus = ordrett ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering,
                    Begrunnelse = ordrett
                        ? $"Navnevedtaket: «{Avkort(h.Tekst)}» Navnet var i bruk til {n.Til:yyyy-MM-dd}."
                        : $"Navnevedtaket: «{Avkort(h.Tekst)}» Navnet står i genitiv i vedtaket og er gjort til grunnform. Navnet var i bruk til {n.Til:yyyy-MM-dd}.",
                    Godkjenning = Strukturkanter.MaskineltForeslatt,
                    OppdagelsesKilde = OppdagelsesKilde,
                    OpprettetAv = bruker,
                    OpprettetTidspunkt = DateTimeOffset.UtcNow,
                };
                db.Begreper.Add(b);
                db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", b.Id, virksomhetId: null, "foreslatt", bruker));
                await db.SaveChangesAsync(ct);
                nNye++;
            }
        }

        // F forvaltning (ressortfordeling)
        foreach (var t in tildelinger.Where(t => katalog.ContainsKey(t.Id)))
        {
            var gyldigTil = til(t);
            await Opprett(new NyStrukturkant(Strukturkanter.Oppgave, "forvaltning", Kantnode.Virksomhet(katalog[t.Id].Id), null,
                HjemmelRettskildeId: t.Hjemmel.RettskildeId, HjemmelEid: t.Hjemmel.Eid, Objekt: t.Objekt, ObjektEli: t.Eli,
                GyldigFra: t.Fra, GyldigTil: gyldigTil is { } g && g >= t.Fra ? g : null,
                Kommentar: $"«{Avkort(t.Hjemmel.Tekst)}» (departementet het {t.NavnDaVedtaket} i vedtaket)",
                Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: OppdagelsesKilde,
                Kunnskapsstatus: t.Funksjonsord ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering,
                Begrunnelse: t.Funksjonsord
                    ? "Regel: monster:livslop-ressortfordeling. Vedtaket bruker funksjonsordet «forvaltningsansvaret» (begrepsmanualen §F)."
                    : "Regel: monster:livslop-ressortfordeling. «Oversikt over lover X skal ha ansvaret for» er lest som forvaltningsansvar (begrepsmanualen §F)."),
                () => rNye++, () => rFantes++);
        }

        return new LivslopSkriving(oNye, oFantes, nNye, nFantes, rNye, rFantes, avvist, feil);
    }

    private static string Avkort(string s) => s.Length <= 240 ? s.Trim() : s[..240].Trim() + "…";
}
