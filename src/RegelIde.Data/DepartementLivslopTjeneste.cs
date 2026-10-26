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
    Guid? KatalogVirksomhetId, string? KatalogNavn, int Ressortlover, string? GjeldendeNavneformHjemmel = null);

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
    int RessortFantes, int Avvist, IReadOnlyList<string> Feil)
{
    /// <summary>[Ny, issue #395 M5] Nedlagte departementer opprettet som aktører (eller som ville blitt det i en tørrkjøring), og de som fantes.</summary>
    public int HistoriskeAktorerNye { get; init; }
    public int HistoriskeAktorerFantes { get; init; }
    public int AvviklerNye { get; init; }
    public int AvviklerFantes { get; init; }
    public int EtterfolgerNye { get; init; }
    public int EtterfolgerFantes { get; init; }
}

/// <summary>[Ny, issue #395, Johanns avgjørelse 2] Et avledet R etterfolger: <see cref="Etterfolger"/> overtar ansvaret for lover
/// <see cref="Nedlagt"/> hadde, i samme vedtak som nedlegger det. <see cref="Lover"/> er lovene (ELI) slutningen bygger på.</summary>
public sealed record AvledetEtterfolger(string Etterfolger, string Nedlagt, DateOnly Dato, LivslopHjemmel Vedtak, IReadOnlyList<string> Lover);

public sealed record LivslopRapport(
    bool Skrevet, int Vedtak, IReadOnlyList<LivslopIdentitet> Identiteter, IReadOnlyList<LivslopRessort> Ressortfordeling,
    IReadOnlyList<RessortAvvik> Avvik, IReadOnlyDictionary<string, int> AvvikPerUtfall, IReadOnlyList<EtterfolgerKandidat> EtterfolgerKandidater,
    IReadOnlyList<LivslopUlest> Ulest, IReadOnlyList<string> Utsatt, IReadOnlyList<string> Merknader, LivslopSkriving Skriving)
{
    /// <summary>[Ny, issue #395] De avledede etterfølgerne (skrevet som forslag når <see cref="Skrevet"/>).</summary>
    public IReadOnlyList<AvledetEtterfolger> AvlededeEtterfolgere { get; init; } = [];
}

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
        public DateOnly? Nedlagt; public LivslopHjemmel? NedlagtVed; public bool NedlagtKglRes;
        public string Gjeldende => Navn[^1].Navn;
    }

    // [ENDRET, issue #397] + Innledning: leddet over punktet i vedlegget («Statsråd Monica Mæland», «Oversikt over lover statsråd … har
    // forvaltningsansvar for etter endringene:»), når det finnes. Står i kommentaren.
    private sealed record Tildeling(Identitet Id, string NavnDaVedtaket, string Objekt, string? Eli, bool Hele, DateOnly Fra, LivslopHjemmel Hjemmel, bool Funksjonsord,
        string? Innledning = null);

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
            List<(LivslopHjemmel H, string Dept, string Raa, bool Funksjonsord, bool HarUnderpunkter, string? Innledning)> Vedlegg)>();

        foreach (var k in kilder)
        {
            var dato = k.Ikrafttredelse ?? (DateOnly.TryParse(k.Kunngjort, CultureInfo.InvariantCulture, out var kd) ? kd : (DateOnly?)null);
            var noder = await db.RettskildeNoder
                .Where(n => n.RettskildeId == k.Id && n.Entitetsstatus == "gjeldende" && !n.Opphevet)
                .OrderBy(n => n.Sorteringsrekkefolge)
                .Select(n => new { n.Id, n.ParentNodeId, n.Eid, n.NodeType, n.Overskrift, n.Tekst })
                .ToListAsync(ct);
            var hendelser = new List<(LivslopHjemmel, Livslopshendelse)>();
            var vedlegg = new List<(LivslopHjemmel, string, string, bool, bool, string?)>();
            // Et lovpunkt med underpunkter («Lov … om vern mot forurensninger» → «§ 44») gir bare ansvaret for underpunktene: en del, ikke hele loven.
            var harBarn = noder.Where(x => x.ParentNodeId is not null).Select(x => x.ParentNodeId!.Value).ToHashSet();
            var vedleggskapitler = new Dictionary<Guid, (string Dept, string Raa, bool F)>();

            var nyttKapitler = new Dictionary<Guid, string>();
            var nodeVedId = noder.ToDictionary(x => x.Id);
            bool UnderVedlegg(Guid? id)
            {
                for (var q = id; q is { } qid && nodeVedId.TryGetValue(qid, out var qn); q = qn.ParentNodeId)
                {
                    if (qn.Overskrift?.TrimStart().StartsWith("Vedlegg", StringComparison.OrdinalIgnoreCase) == true) return true;
                }
                return false;
            }
            foreach (var n in noder)
            {
                if (n.Overskrift is { } o && DepartementLivslopMonstre.LesVedleggsoverskrift(o) is { } vd) vedleggskapitler[n.Id] = vd;
                // [Ny, issue #397] Et kapittel som bare heter et departement, under «Vedlegg» (FOR-2018-04-27-638, FOR-2019-05-03-560: per
                // departement, og der to statsråder deler det, per statsråd). Funksjonsordet står i innledningen under (se løkka nedenfor).
                else if (n.Overskrift is { } ob && DepartementLivslopMonstre.LesBareDepartement(ob) is { } bd && UnderVedlegg(n.ParentNodeId))
                {
                    vedleggskapitler[n.Id] = (bd.Navn, bd.Raa, false);
                }
                // [Ny, issue #397] Overskriften står som et ledd over lista (FOR-2023-12-20-2166: «Oversikt over lover som Digitaliserings- og
                // forvaltningsdepartementet skal ha ansvaret for (med virkning fra 1. januar 2024):» med punktene under).
                else if (n.Tekst is { } lt && harBarn.Contains(n.Id) && DepartementLivslopMonstre.LesVedleggsoverskrift(lt) is { } vl)
                {
                    vedleggskapitler[n.Id] = vl;
                }
                if (n.Overskrift is { } on && DepartementLivslopMonstre.LesNyttDepartement(on) is { } nd) nyttKapitler[n.Id] = nd.Navn;
            }
            foreach (var n in noder)
            {
                if (string.IsNullOrWhiteSpace(n.Tekst)) continue;
                var h = new LivslopHjemmel(k.Id, k.Tittel, k.Kildeform, n.Eid, n.Tekst);
                // Et punkt i et vedlegg («Oversikt over lover X skal ha forvaltningsansvaret for»): nærmeste vedleggskapittel oppover.
                Guid? p = n.ParentNodeId;
                (string Dept, string Raa, bool F)? kap = null;
                string? innledning = null; // [Ny, issue #397] nærmeste tekst over punktet, under vedleggskapitlet
                while (p is { } pid)
                {
                    if (vedleggskapitler.TryGetValue(pid, out var v)) { kap = v; break; }
                    var pn = nodeVedId.GetValueOrDefault(pid);
                    if (innledning is null && !string.IsNullOrWhiteSpace(pn?.Tekst)) innledning = pn.Tekst.Trim();
                    p = pn?.ParentNodeId;
                }
                if (kap is { } kv)
                {
                    if (DepartementLivslopMonstre.LovDatokode(n.Tekst) is not null)
                    {
                        var funksjonsord = kv.F || (innledning?.Contains("forvaltningsansvar", StringComparison.OrdinalIgnoreCase) ?? false);
                        vedlegg.Add((h, kv.Dept, kv.Raa, funksjonsord, harBarn.Contains(n.Id), innledning));
                    }
                    continue;
                }
                var lest = DepartementLivslopMonstre.LesLedd(n.Tekst, out var grunn);
                foreach (var e in lest) hendelser.Add((h, e));
                // [Ny, issue #397] «X organiseres ved opprettelsen …» i et kapittel med overskriften «Nytt X (FORK)» (1997, 1998).
                if (DepartementLivslopMonstre.LesOrganiseresVedOpprettelsen(n.Tekst) is { } ov)
                {
                    for (var q = n.ParentNodeId; q is { } qid && nodeVedId.TryGetValue(qid, out var qn); q = qn.ParentNodeId)
                    {
                        if (nyttKapitler.TryGetValue(qid, out var nyttNavn) && nyttNavn == ov.Navn)
                        {
                            hendelser.Add((h, new Livslopshendelse(DepartementLivslopMonstre.Oppretting, [ov], Regel: "monster:livslop-nytt-departement")));
                            break;
                        }
                    }
                }
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
        var lopenr = 0; // løpenummer i rapporten; ikke Count + 1, fordi sammenslåtte identiteter fjernes
        var levende = new Dictionary<string, Identitet>(StringComparer.Ordinal);
        var tildelinger = new List<Tildeling>();
        var etterfolgere = new List<EtterfolgerKandidat>();
        var merknader = new List<string>();
        var opprettetKanter = new List<(Identitet Id, DateOnly Dato, LivslopHjemmel H, bool Normalisert)>();

        Identitet Hent(string navn, LivslopHjemmel h)
        {
            if (levende.TryGetValue(navn, out var i)) return i;
            // Navnet var i bruk før det eldste vedtaket vi har: en identitet uten kjent start.
            i = new Identitet { Nr = ++lopenr };
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
                    // [Ny, issue #397] Samme nedleggelse to ganger i samme vedtak («Nedleggelse av X» og «X nedlegges») er én.
                    if (!levende.ContainsKey(navn) && nedlagtHer.Contains(navn)) continue;
                    var i = Hent(navn, h);
                    i.Nedlagt = v.Dato; i.NedlagtVed = h; i.NedlagtKglRes = v.KglRes;
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
                // Et KONSOLIDERT vedtak fra før navnebyttet kan bruke det nye navnet: Lovdata oppdaterer departementsnavn i konsoliderte
                // tekster (FOR-2017-03-31-417 sier «til Energidepartementet» om et vedtak fra 2017; navnet kom 2024). Finnes det nye navnet
                // alt som en identitet uten kjent start, opprettelse eller nedleggelse, er det samme departement — det slås sammen, og det
                // står i merknadene. Et navn som er GJENBRUKT (Arbeids- og sosialdepartementet 2004 og 2014) har opprettelse/nedleggelse og
                // slås aldri sammen her.
                if (levende.TryGetValue(til, out var forut) && forut != i && forut.Opprettet is null && forut.Nedlagt is null && forut.Navn.Count == 1)
                {
                    for (var x = 0; x < tildelinger.Count; x++)
                    {
                        if (tildelinger[x].Id == forut) tildelinger[x] = tildelinger[x] with { Id = i };
                    }
                    identiteter.Remove(forut);
                    levende.Remove(til);
                    merknader.Add($"«{til}» er brukt i vedtak fra før navnebyttet «{fraRaa}» → «{tilRaa}» ({v.Tittel}, {v.Dato:yyyy-MM-dd}). " +
                        "Lest som samme departement: konsoliderte tekster bruker gjeldende navn.");
                }
                i.Navn[^1] = i.Navn[^1] with { Til = v.Dato.AddDays(-1), Endret = h };
                i.Navn.Add((til, v.Dato, null, h, null, tilRaa));
                levende.Remove(fra);
                levende[til] = i;
            }
            foreach (var (h, e) in v.Hendelser.Where(x => x.E.Type == DepartementLivslopMonstre.Oppretting))
            {
                foreach (var (navn, raa) in e.Navn)
                {
                    if (opprettetHer.Contains(navn)) continue; // [Ny, issue #397] samme opprettelse to ganger i samme vedtak
                    if (levende.ContainsKey(navn))
                    {
                        ulest.Add(new(h, $"«{navn}» opprettes, men et departement med samme navn finnes fra før uten å være nedlagt"));
                        continue;
                    }
                    var i = new Identitet { Nr = ++lopenr, Opprettet = v.Dato, OpprettetVed = h };
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
            foreach (var (h, dept, _, funksjonsord, harUnderpunkter, innledning) in v.Vedlegg)
            {
                var i = Hent(dept, h);
                var datokode = DepartementLivslopMonstre.LovDatokode(h.Tekst)!;
                tildelinger.Add(new Tildeling(i, dept, h.Tekst.Trim(), LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out _),
                    ErHeleLoven(h.Tekst) && !harUnderpunkter, v.Dato, h, funksjonsord, innledning));
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

        // ---- [Ny, issue #395, Johanns avgjørelse M5] Nedlagte departementer som aktører: status nedlagt, ingen registerkobling, gyldig fra
        // opprettelsen (når den er kjent) til dagen før nedleggelsen. Nøkkelen er (nedlagt, navn, gyldig til): et gjenbrukt navn (Barne- og
        // familiedepartementet til 2006) er en annen aktør enn dagens. I en tørrkjøring opprettes ingenting; de telles.
        var historiske = new Dictionary<Identitet, (Guid Id, string Navn)>();
        int historiskeNye = 0, historiskeFantes = 0;
        foreach (var i in identiteter.Where(i => i.Nedlagt is not null))
        {
            var sisteDag = i.Nedlagt!.Value.AddDays(-1);
            var navn = i.Gjeldende;
            var finnes = await db.Virksomheter.Where(v => v.Livsstatus == "nedlagt" && v.Navn == navn && v.GyldigTil == sisteDag)
                .Select(v => new { v.Id, v.Navn }).FirstOrDefaultAsync(ct);
            if (finnes is not null) { historiske[i] = (finnes.Id, finnes.Navn); historiskeFantes++; continue; }
            historiskeNye++;
            if (!skriv) continue;
            var aktor = new Virksomhet
            {
                Id = Guid.NewGuid(),
                Navn = navn,
                Livsstatus = "nedlagt",
                GyldigFra = i.Opprettet ?? i.Navn[0].Fra,
                GyldigTil = sisteDag,
                Aktiv = false,
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            db.Virksomheter.Add(aktor);
            db.Proveniens.Add(ProveniensHjelper.NyRad("virksomhet", aktor.Id, virksomhetId: null, "opprettet", bruker));
            await db.SaveChangesAsync(ct);
            historiske[i] = (aktor.Id, aktor.Navn);
        }

        // ---- [Ny etter juristrunden 2026-10-23] Hjemmelen den gjeldende navneformen har i dag, ved siden av navnevedtaket (Navn[^1].GittVed).
        // Del 4 (AC9) skal la navneformen få hjemmel i navnevedtaket; her står bare begge, slik at forskjellen synes (LMD har i dag
        // «Forskrift om avlsfremmende tiltak på bier» § 3 som hjemmel for navnet sitt).
        var navneformHjemmel = new Dictionary<Identitet, string>();
        foreach (var (i, a) in katalog)
        {
            var lower = i.Gjeldende.ToLower();
            var nf = await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == a.Id && b.Entitetsstatus == "gjeldende"
                    && b.Term.ToLower() == lower)
                .Select(b => new { b.LovkildeId, b.LovreferanseEid }).FirstOrDefaultAsync(ct);
            if (nf is null) { navneformHjemmel[i] = "(ingen navneform med dette navnet)"; continue; }
            var tittel = nf.LovkildeId is null ? null : await db.Rettskilder.Where(r => r.Id == nf.LovkildeId).Select(r => r.Tittel).FirstOrDefaultAsync(ct);
            navneformHjemmel[i] = tittel is null ? "(uten hjemmel)" : $"{tittel} ({nf.LovreferanseEid})";
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
        var alle = katalog.Concat(historiske).ToDictionary(x => x.Key, x => x.Value);
        var avledede = AvledEtterfolgere(identiteter, tildelinger);
        var skriving = skriv
            ? await SkrivAsync(alle, historiske.Keys.ToHashSet(), opprettetKanter, tildelinger, avledede, Til, bruker, ct)
            : TellUtenSkriving(katalog, opprettetKanter, tildelinger);
        skriving = skriving with { HistoriskeAktorerNye = historiskeNye, HistoriskeAktorerFantes = historiskeFantes };

        var ressortRader = tildelinger.Select(t => new LivslopRessort(t.Id.Nr, t.NavnDaVedtaket, t.Objekt, t.Eli, t.Hele, t.Fra, Til(t), t.Hjemmel,
            t.Funksjonsord ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering)).ToList();
        var identitetRader = identiteter.Select(i => new LivslopIdentitet(i.Nr,
            i.Navn.Select(n => new LivslopNavn(n.Navn, n.Fra, n.Til, n.Gitt, n.Endret)).ToList(), i.Opprettet, i.OpprettetVed, i.Nedlagt, i.NedlagtVed,
            alle.TryGetValue(i, out var kv) ? kv.Id : null, alle.TryGetValue(i, out var kv2) ? kv2.Navn : null,
            tildelinger.Count(t => t.Id == i), navneformHjemmel.GetValueOrDefault(i))).ToList();

        return new LivslopRapport(skriv, vedtak.Count, identitetRader, ressortRader, avvik,
            avvik.GroupBy(a => a.Utfall).ToDictionary(g => g.Key, g => g.Count()), etterfolgere, ulest, utsatt, merknader, skriving)
        {
            AvlededeEtterfolgere = avledede.Select(a => new AvledetEtterfolger(a.Y.Gjeldende, a.X.Gjeldende, a.X.Nedlagt!.Value, a.X.NedlagtVed!,
                a.Par.Select(p => p.Ny.Eli!).Distinct().ToList())).ToList(),
        };
    }

    /// <summary>
    /// [Ny, issue #395, Johanns avgjørelse 2 2026-10-23] R etterfolger brukes bare når ANSVAR går over til et annet organ (sammenslåing eller
    /// oppdeling), aldri ved et rent navnebytte. Avledet slik: X nedlegges i vedtaket V, og V gir lover X hadde forvaltningsansvaret for (siste
    /// tildeling før nedleggelsen) til Y. Da etterfølger Y X for de lovene. Flere Y = oppdeling, flere X til samme Y = sammenslåing. Lovparene
    /// er premissene (kunnskapsstatus avledet). Opprettelser og nedleggelser i samme vedtak UTEN lover som går fra X til Y gir ingenting —
    /// kryssproduktet juristrunden advarte mot (FAKD → NFD i 2014) oppstår ikke.
    /// </summary>
    private static List<(Identitet X, Identitet Y, List<(Tildeling Gammel, Tildeling Ny)> Par)> AvledEtterfolgere(
        List<Identitet> identiteter, List<Tildeling> tildelinger)
    {
        var svar = new List<(Identitet, Identitet, List<(Tildeling, Tildeling)>)>();
        foreach (var x in identiteter.Where(i => i.Nedlagt is not null && i.NedlagtVed is not null))
        {
            var d = x.Nedlagt!.Value;
            var vedtak = x.NedlagtVed!.RettskildeId;
            var hadde = tildelinger.Where(t => t.Id == x && t.Eli is not null && t.Fra < d)
                .GroupBy(t => t.Eli!).ToDictionary(g => g.Key, g => g.OrderBy(t => t.Fra).Last());
            if (hadde.Count == 0) continue;
            foreach (var gruppe in tildelinger.Where(t => t.Hjemmel.RettskildeId == vedtak && t.Id != x && t.Eli is not null && hadde.ContainsKey(t.Eli))
                         .GroupBy(t => t.Id))
            {
                // [ENDRET etter juristrunden M5 2026-10-23, E19/E22/E30/E32/E34] Samme DEL av loven må gå over: enten hele loven begge
                // ganger, eller samme punkttekst. Et par på ELI alene ga falske etterfølgere (KKD hadde §§ 1 og 3 i brudvigjingslova, BLD
                // §§ 2, 6, 7 og 11 — ulike deler av samme lov).
                var par = gruppe.Select(t => (Gammel: hadde[t.Eli!], Ny: t))
                    .Where(p => (p.Gammel.Hele && p.Ny.Hele) || Delnokkel(p.Gammel.Objekt) == Delnokkel(p.Ny.Objekt)).ToList();
                if (par.Count > 0) svar.Add((x, gruppe.Key, par));
            }
        }
        return svar;
    }

    /// <summary>Hele loven, ikke enkeltparagrafer eller kapitler («Lov 17. juli 1925 nr. 11 om Svalbard, § 4» er en del).</summary>
    // [ENDRET etter måling 2026-10-23] + «bestemmelser»/«følgende»: «følgende bestemmelser i lov 22. mars 2019 nr. 7 …» er en del.
    public static bool ErHeleLoven(string objekt) =>
        !objekt.Contains('§') && !objekt.Contains("kapittel", StringComparison.OrdinalIgnoreCase) && !objekt.Contains("kap.", StringComparison.OrdinalIgnoreCase)
        // [ENDRET etter juristrunden 2026-10-23, L16/L19/L21] + avgrensninger i punktteksten («Gjelder for petroleumsvirksomhet på
        // landanleggene …», «Anden Bog», «for så vidt gjelder», «med unntak av»). «bestemmelse» alene er for vidt: «… indeholdende visse
        // Bestemmelser» er en lovtittel.
        && !System.Text.RegularExpressions.Regex.IsMatch(objekt,
            @"\b(følgende|enkelte|visse av)\s+bestemmelse|\bbestemmelsene i\b|\bfølgende\b|\bdeler av\b|\bgjelder\b|\bbog\b|\bbok\b|\bfor så vidt\b|\bunntak\b|\bunntatt\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>Nøyaktig én aktør med dette registernavnet (uten skille på store og små bokstaver), ellers nøyaktig én aktør med det som
    /// GJELDENDE navneform. Utgåtte navneformer telles ikke: de er nettopp de navnene som kan ha vært brukt om et annet departement.</summary>
    private async Task<(Guid Id, string Navn)?> FinnAktorAsync(string navn, CancellationToken ct)
    {
        var lower = navn.ToLower();
        // [ENDRET, issue #395 M5] Nedlagte aktører er aldri svaret på «hvem er dette i dag».
        var direkte = await db.Virksomheter.Where(v => v.Navn.ToLower() == lower && v.Livsstatus == null).Select(v => new { v.Id, v.Navn }).Take(2).ToListAsync(ct);
        if (direkte.Count == 1) return (direkte[0].Id, direkte[0].Navn);
        if (direkte.Count > 1) return null;
        var via = await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                && b.Navneformgrunn == "gjeldende" && b.Term.ToLower() == lower
                && db.Virksomheter.Any(v => v.Id == b.VirksomhetReferanseId && v.Livsstatus == null))
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

    private async Task<LivslopSkriving> SkrivAsync(Dictionary<Identitet, (Guid Id, string Navn)> katalog, HashSet<Identitet> historiske,
        List<(Identitet Id, DateOnly Dato, LivslopHjemmel H, bool Normalisert)> opprettet, List<Tildeling> tildelinger,
        List<(Identitet X, Identitet Y, List<(Tildeling Gammel, Tildeling Ny)> Par)> avledede,
        Func<Tildeling, DateOnly?> til, string bruker, CancellationToken ct)
    {
        int oNye = 0, oFantes = 0, nNye = 0, nFantes = 0, rNye = 0, rFantes = 0, avvist = 0, aNye = 0, aFantes = 0, eNye = 0, eFantes = 0;
        var kantFor = new Dictionary<Tildeling, Guid>();
        var feil = new List<string>();
        var kongen = await FinnAktorAsync(KongenIStatsrad, ct);

        async Task<Guid?> Opprett(NyStrukturkant ny, Action nye, Action fantes)
        {
            try
            {
                // [Ny, issue #397] Samme utsagn med hjemmel i en ANNEN versjon av samme vedtak (samme ELI og eId) er ikke nytt: en ny
                // versjon av vedtaket (resynk eller ny konvertering, se /api/rettskilder/{id}/konverter-paa-nytt) skal ikke gi
                // forslagene på nytt. Kanten står på versjonen den ble foreslått mot (#367).
                if (await FinnIAnnenVersjonAsync(ny, ct) is { } annen)
                {
                    if (annen.Status == Strukturkanter.Avvist) avvist++; else fantes();
                    return annen.Id;
                }
                var r = await kanter.OpprettAsync(ny, bruker, ct);
                if (r.VarNy) nye();
                else if (r.Kant.Status == Strukturkanter.Avvist) avvist++;
                else fantes();
                return r.Kant.Id;
            }
            catch (ArgumentException ex)
            {
                db.ChangeTracker.Clear();
                feil.Add($"{ny.Kategori} {ny.Typekode} ({ny.HjemmelEid}): {ex.Message}");
                return null;
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
                // [ENDRET etter juristrunden 2026-10-23, L12/L13/L15] Navnekjeden står i begrunnelsen: kanten går til dagens aktør, men vedtaket
                // oppretter departementet under det navnet det hadde da («Opprettelse av Arbeidsdepartementet», 2010).
                Begrunnelse: "Regel: monster:livslop-opprettelse-av. Vedtaket er fastsatt ved kgl.res. (fra = Kongen i statsråd)"
                    + (normalisert ? "; navnet står i ubestemt form i vedtaket og er gjort bestemt" : "")
                    + (i.Navn.Count > 1
                        ? $". Opprettet som «{i.Navn[0].Navn}»; dagens navn etter navnebytte: " + string.Join(" → ",
                            i.Navn.Skip(1).Select(n => $"«{n.Navn}» fra {n.Fra:yyyy-MM-dd} ({n.Gitt?.Tittel})")) + "."
                        : ".")),
                () => oNye++, () => oFantes++);
        }

        // Navneform «utgått» for tidligere navn
        foreach (var (i, aktor) in katalog)
        {
            // [ENDRET, issue #395 M5] En nedlagt aktør får ALLE navnene sine: det siste som «gjeldende» (gyldig til nedleggelsen), de
            // tidligere som «utgått». Hjemmelen er vedtaket som ga navnet, ellers — for et navn som var i bruk før det eldste vedtaket —
            // vedtaket som endret det eller nedla departementet (der navnet står).
            var erHistorisk = historiske.Contains(i);
            var sisteNavn = i.Navn[^1];
            foreach (var n in i.Navn.Where(n => erHistorisk || (n.Endret is not null && !string.Equals(n.Navn, i.Gjeldende, StringComparison.OrdinalIgnoreCase))))
            {
                var finnes = await db.Begreper.IgnoreQueryFilters().AnyAsync(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == aktor.Id
                    && b.Term.ToLower() == n.Navn.ToLower(), ct);
                if (finnes) { nFantes++; continue; }
                var erSiste = n == sisteNavn;
                var h = (erHistorisk ? n.Gitt ?? n.Endret ?? i.NedlagtVed : n.Endret)!;
                // Hele ord: «Landbruksdepartementets» (genitiv) inneholder «Landbruksdepartementet», men er ikke ordrett.
                var ordrett = System.Text.RegularExpressions.Regex.IsMatch(h.Tekst,
                    $@"(?<!\p{{L}}){System.Text.RegularExpressions.Regex.Escape(n.Navn)}(?!\p{{L}})");
                var b = new BegrepEntitet
                {
                    Id = Guid.NewGuid(),
                    Begrepskategori = "virksomhet",
                    VirksomhetReferanseId = aktor.Id,
                    Navneformgrunn = erHistorisk && erSiste ? "gjeldende" : "utgatt",
                    Term = n.Navn,
                    Status = "publisert",
                    GyldigFra = n.Fra,
                    GyldigTil = n.Til,
                    LovkildeId = h.RettskildeId,
                    LovreferanseEid = h.Eid,
                    Kunnskapsstatus = ordrett ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering,
                    Begrunnelse = (ordrett
                        ? $"Vedtaket: «{Avkort(h.Tekst)}» Navnet var i bruk til {n.Til:yyyy-MM-dd}."
                        : $"Vedtaket: «{Avkort(h.Tekst)}» Navnet står i genitiv eller ubestemt form i vedtaket og er gjort til bestemt grunnform. "
                          + $"Navnet var i bruk til {n.Til:yyyy-MM-dd}.")
                        + (erHistorisk ? " Departementet er nedlagt (aktør med status nedlagt, issue #395 M5)." : ""),
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
            var fId = await Opprett(new NyStrukturkant(Strukturkanter.Oppgave, "forvaltning", Kantnode.Virksomhet(katalog[t.Id].Id), null,
                HjemmelRettskildeId: t.Hjemmel.RettskildeId, HjemmelEid: t.Hjemmel.Eid, Objekt: t.Objekt, ObjektEli: t.Eli,
                GyldigFra: t.Fra, GyldigTil: gyldigTil is { } g && g >= t.Fra ? g : null,
                // [ENDRET etter juristrunden 2026-10-23, M4] Kommentaren sier hvor gyldig-til kommer fra: neste vedtak som gir samme hele lov til
                // et annet departement, eller nedleggelsen av departementet.
                Kommentar: $"«{Avkort(t.Hjemmel.Tekst)}» (departementet het {t.NavnDaVedtaket} i vedtaket)"
                    + (t.Innledning is null ? "" : $" Innledningen i vedlegget: «{Avkort(t.Innledning)}».")
                    + (gyldigTil is null ? "" : TilGrunn(t, gyldigTil.Value)),
                Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: OppdagelsesKilde,
                Kunnskapsstatus: t.Funksjonsord ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering,
                // Modalitet null: en ressortfordeling er en TILDELING av ansvar, ikke en plikt til å utføre noe («skal ha» er tildelingens form).
                Begrunnelse: (t.Funksjonsord
                    ? "Regel: monster:livslop-ressortfordeling. Vedtaket bruker funksjonsordet «forvaltningsansvaret» (begrepsmanualen §F)."
                    : "Regel: monster:livslop-ressortfordeling. «… skal ha ansvaret for» lover er lest som forvaltningsansvar fordi objektet er et "
                      + "regelverk og vedtaket overfører ansvaret for lover (begrepsmanualen §F).")
                    + " Modalitet er ikke satt: vedtaket tildeler ansvaret, det pålegger ingen plikt."),
                () => rNye++, () => rFantes++);
            if (fId is { } fid) kantFor[t] = fid;
        }

        // [Ny, issue #395 M5] R avvikler fra Kongen i statsråd til det nedlagte departementet, når nedleggelsen er fastsatt ved kgl.res.
        foreach (var i in historiske.Where(i => i.NedlagtKglRes && i.NedlagtVed is not null))
        {
            if (kongen is null) { feil.Add($"R avvikler {i.Gjeldende}: fant ikke «{KongenIStatsrad}» i katalogen"); continue; }
            var h = i.NedlagtVed!;
            var ordrett = h.Tekst.Contains(i.Gjeldende + " ", StringComparison.Ordinal) || h.Tekst.Contains(i.Gjeldende + ",", StringComparison.Ordinal)
                || h.Tekst.Contains(i.Gjeldende + ".", StringComparison.Ordinal);
            await Opprett(new NyStrukturkant(Strukturkanter.Relasjon, "avvikler", Kantnode.Virksomhet(kongen.Value.Id), Kantnode.Virksomhet(katalog[i].Id),
                HjemmelRettskildeId: h.RettskildeId, HjemmelEid: h.Eid, GyldigFra: i.Nedlagt,
                Kommentar: $"«{Avkort(h.Tekst)}»", Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: OppdagelsesKilde,
                Kunnskapsstatus: Strukturkanter.Normalisering,
                Begrunnelse: "Regel: monster:livslop-nedleggelse-av. Vedtaket er fastsatt ved kgl.res. (fra = Kongen i statsråd)"
                    + (ordrett ? "." : "; navnet er gjort til bestemt grunnform.")),
                () => aNye++, () => aFantes++);
        }

        // [Ny, issue #395, avgjørelse 2] R etterfolger, avledet av ressortfordelingen (se AvledEtterfolgere): fra = etterfølgeren, til = det nedlagte.
        foreach (var (x, y, par) in avledede.Where(a => katalog.ContainsKey(a.X) && katalog.ContainsKey(a.Y)))
        {
            var premisser = par.SelectMany(p => new[] { kantFor.GetValueOrDefault(p.Gammel), kantFor.GetValueOrDefault(p.Ny) })
                .Where(g => g != Guid.Empty).Distinct().ToList();
            if (premisser.Count == 0) { feil.Add($"R etterfolger {y.Gjeldende} → {x.Gjeldende}: ingen av premisskantene ble skrevet"); continue; }
            var lover = par.Select(p => p.Ny.Objekt).Distinct().ToList();
            var ny = new NyStrukturkant(Strukturkanter.Relasjon, "etterfolger", Kantnode.Virksomhet(katalog[y].Id), Kantnode.Virksomhet(katalog[x].Id),
                HjemmelRettskildeId: x.NedlagtVed!.RettskildeId, HjemmelEid: x.NedlagtVed.Eid, GyldigFra: x.Nedlagt,
                // [ENDRET, issue #397, Johann 2026-10-24 på #134] Omfanget er antall lovdeler som gikk over; det er ikke et felt for hoved- eller
                // deletterfølger. API-et teller det samme fra premissene (StrukturkantVisning.Omfang).
                Kommentar: $"Omfang: {lover.Count} {(lover.Count == 1 ? "lovdel" : "lovdeler")}. Lover som går fra {NavnPaa(x, x.Nedlagt!.Value.AddDays(-1))} til {NavnPaa(y, x.Nedlagt.Value)} i samme vedtak: " + string.Join("; ", lover.Take(5).Select(o => Avkort(o)))
                    + (lover.Count > 5 ? $" … ({lover.Count} i alt)" : ""),
                Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: "monster:livslop-etterfolger",
                Kunnskapsstatus: Strukturkanter.Avledet, Premisser: premisser,
                // [ENDRET etter juristrunden M5, E27] Navnene som gjaldt på datoen, ikke dagens.
                Begrunnelse: $"Avledet (issue #395, Johanns avgjørelse 2): {NavnPaa(x, x.Nedlagt!.Value.AddDays(-1))} nedlegges ved «{x.NedlagtVed.Tittel}», og "
                    + $"{lover.Count} lov(er) eller samme del av lov det hadde forvaltningsansvaret for, gis i samme vedtak til {NavnPaa(y, x.Nedlagt.Value)}. "
                    + "Ansvaret går over til et annet organ. Premissene er forvaltningskantene før og etter. Omfanget er antall lovdeler som gikk "
                    + $"over ({lover.Count}); det er ikke et felt for hoved- eller deletterfølger (Johann 2026-10-24, #134).");
            var eId = await Opprett(ny, () => eNye++, () => eFantes++);
            // Et maskinelt forslag som alt fantes, får oppdatert tekst (begrunnelse og kommentar er ikke en del av utsagnets identitet, og
            // ingen har vurdert det ennå). Et vurdert eller avvist utsagn røres ikke.
            if (eId is { } eid && await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == eid, ct) is { Status: Strukturkanter.MaskineltForeslatt } eksisterende
                && (eksisterende.Begrunnelse != ny.Begrunnelse || eksisterende.Kommentar != ny.Kommentar))
            {
                eksisterende.Begrunnelse = ny.Begrunnelse;
                eksisterende.Kommentar = ny.Kommentar;
                await db.SaveChangesAsync(ct);
            }
        }

        return new LivslopSkriving(oNye, oFantes, nNye, nFantes, rNye, rFantes, avvist, feil)
        {
            AvviklerNye = aNye, AvviklerFantes = aFantes, EtterfolgerNye = eNye, EtterfolgerFantes = eFantes,
        };
    }

    /// <summary>[Ny, issue #397] En kant med samme kategori, type, fra, til, objekt og hjemmel-eId, der hjemmelen er en ANNEN versjon av
    /// samme rettskilde (samme ELI). Avviste telles med, som i <see cref="StrukturkantTjeneste.OpprettAsync"/>.</summary>
    private async Task<StrukturkantEntitet?> FinnIAnnenVersjonAsync(NyStrukturkant ny, CancellationToken ct)
    {
        if (ny.HjemmelRettskildeId is not { } hid || ny.HjemmelEid is not { } eid) return null;
        var eli = await db.Rettskilder.IgnoreQueryFilters().Where(r => r.Id == hid).Select(r => r.Eli).FirstOrDefaultAsync(ct);
        if (eli is null) return null;
        var tilV = ny.Til?.VirksomhetId;
        var objekt = string.IsNullOrWhiteSpace(ny.Objekt) ? null : ny.Objekt.Trim();
        return await kanter.GjeldendeInklAvviste.FirstOrDefaultAsync(k => k.Kategori == ny.Kategori && k.Typekode == ny.Typekode
            && k.FraVirksomhetId == ny.Fra.VirksomhetId && k.TilVirksomhetId == tilV && k.Objekt == objekt && k.HjemmelEid == eid
            && k.HjemmelRettskildeId != hid
            && db.Rettskilder.IgnoreQueryFilters().Any(r => r.Id == k.HjemmelRettskildeId && r.Eli == eli), ct);
    }

    /// <summary>Punktteksten uten mellomrom og skilletegn, for å kjenne igjen samme del av en lov i to vedlegg.</summary>
    private static string Delnokkel(string objekt) =>
        new string(objekt.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>Navnet identiteten hadde på en dato (navnet som gjaldt da), ellers det siste.</summary>
    private static string NavnPaa(Identitet i, DateOnly dato) =>
        i.Navn.LastOrDefault(n => (n.Fra is null || n.Fra <= dato) && (n.Til is null || n.Til >= dato)).Navn ?? i.Gjeldende;

    private static string TilGrunn(Tildeling t, DateOnly til) =>
        t.Id.Nedlagt is { } nd && til == nd.AddDays(-1)
            ? $" Gyldig til {til:yyyy-MM-dd}: departementet ble nedlagt ({t.Id.NedlagtVed?.Tittel})."
            : $" Gyldig til {til:yyyy-MM-dd}: et senere vedtak gir hele loven til et annet departement.";

    private static string Avkort(string s) => s.Length <= 240 ? s.Trim() : s[..240].Trim() + "…";
}
