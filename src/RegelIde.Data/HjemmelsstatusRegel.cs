using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>Én node i den kunngjorte teksten til et dokument (Norsk Lovtidend avd. I).</summary>
public sealed record KunngjortNode(string Eid, string? ForelderEid, string? Tekst, int Rekkefolge);

/// <summary>
/// [Ny, #472 PR 1, Johanns avgjørelse 2026-11-18 (3)] Den kunngjorte teksten til dokumenter i korpuset. Lovdata fjerner opphevingssetningen fra den
/// konsoliderte teksten når den er gjennomført (universitets- og høyskoleloven 2024 opphever uhl. 2005, som er hjemmel i 465 rader), men
/// setningen står i vedtaket slik det ble kunngjort. Grensesnitt fordi den ekte kilden laster ned arkivet (69 MB); testene bruker en falsk.
/// </summary>
public interface IKunngjortTekstkilde
{
    /// <summary>Nodene i den kunngjorte teksten for hver ELI som finnes i kilden. En ELI som mangler, er ikke med.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<KunngjortNode>>> HentAsync(IReadOnlyCollection<string> eliListe, CancellationToken ct);
}

/// <summary>Lovtidend avd. I fra Lovdatas åpne API, lest av samme arkivleser og parser som importen (<see cref="LovdataBulkHenter"/>,
/// <see cref="LovdataKonverterer"/>). Arkivet dekker 2001 og senere; eldre lover finnes ikke der.</summary>
public sealed class LovtidendKunngjortTekstkilde(LovdataBulkHenter henter) : IKunngjortTekstkilde
{
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<KunngjortNode>>> HentAsync(IReadOnlyCollection<string> eliListe, CancellationToken ct)
    {
        var onsket = eliListe.ToHashSet(StringComparer.Ordinal);
        var ut = new Dictionary<string, IReadOnlyList<KunngjortNode>>(StringComparer.Ordinal);
        if (onsket.Count == 0) return ut;
        await foreach (var (datokode, _, html) in henter.HentLovtidendDokumenterAsync(ct))
        {
            string eli;
            try { eli = LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out _); }
            catch (FormatException) { continue; }
            if (!onsket.Contains(eli) || ut.ContainsKey(eli)) continue;
            try
            {
                var k = LovdataKonverterer.Konverter(html);
                ut[eli] = k.Noder.Select(n => new KunngjortNode(n.Eid, n.ParentEid, n.Tekst, n.SorteringsRekkefolge))
                    .Concat(Endringsblokker(html, eli)).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Et dokument parseren ikke klarer, gir ingen kunngjort tekst: raden blir stående som EN (ingen gjettet fallback).
            }
        }
        return ut;
    }

    /// <summary>
    /// [Ny, #472 PR 1, målt mot Lovtidend-arkivet 2026-11-20] Leddene i endringsblokkene (<c>article.document-change</c> /
    /// <c>article.change</c>) i en kunngjort tekst. Parseren hopper bevisst over dem (<c>IkkeGjeldendeInnholdKlasser</c> i
    /// <see cref="LovdataHtmlParser"/>): i den konsoliderte teksten er de ikke gjeldende rettskildetekst. Men i en kunngjort lov er det nettopp
    /// der opphevingen står — uhl. 2024 § 17-2: «Samtidig oppheves lov 1. april 2005 nr. 15 om universiteter og høyskoler.» ligger i
    /// <c>&lt;article class="change" data-repeal-part="lov/2005-04-01-15"&gt;</c>. Uten dette fant regelen ingen av de 465 radene for uhl. 2005.
    /// Leddene festes til paragrafen de står i (<c>data-name</c> på nærmeste <c>legalArticle</c>), fordi endringsblokken ikke har en eId i
    /// korpusets form. Parseren endres ikke: importen skal fortsatt hoppe over dem.
    /// </summary>
    public static IEnumerable<KunngjortNode> Endringsblokker(string html, string eli)
    {
        var dok = new HtmlAgilityPack.HtmlDocument();
        dok.LoadHtml(html);
        var ledd = dok.DocumentNode.SelectNodes("//article[contains(concat(' ', normalize-space(@class), ' '), ' change ') "
            + "or contains(concat(' ', normalize-space(@class), ' '), ' document-change ')]//article[contains(@class, 'legalP')]");
        if (ledd is null) yield break;
        var sett = new HashSet<HtmlAgilityPack.HtmlNode>();
        var i = 0;
        foreach (var l in ledd)
        {
            if (!sett.Add(l)) continue;
            var paragraf = l.Ancestors("article").FirstOrDefault(a => (a.GetAttributeValue("class", "") ?? "").Split(' ').Contains("legalArticle"));
            var navn = paragraf?.GetAttributeValue("data-name", null);
            var tekst = HtmlAgilityPack.HtmlEntity.DeEntitize(l.InnerText).Trim();
            if (tekst.Length == 0) continue;
            yield return new KunngjortNode(navn is null ? eli : $"{eli}/{navn}", null, tekst, 1_000_000 + i++);
        }
    }
}

public sealed record HjemmelsstatusLovtidendTall(bool Lest, int Etterspurt, int Funnet, int EnDokumenter, int EnDokumenterLost, int EnRader, int EnRaderLost);

/// <summary>Én hjemmelsrad i rapporten (<c>?medRader=true</c>).</summary>
public sealed record HjemmelsstatusRapportRad(
    Guid RettskildeHjemmelId, string? ForskriftDokumentId, string HjemmelEid, string? Rolle, string Status, string? Bevistrinn, string? UkjentGrunn,
    string? OpphevetAv, string? OpphevetAvSitat, string? VidereforingForm, string? Videreforing, string? Kunnskapsstatus);

public sealed record HjemmelsstatusRapport(
    bool Skrevet, bool LovtidendLest, int Hjemmelsrader, int Skrevne, int EndretStatus,
    IReadOnlyDictionary<string, int> PerStatus, IReadOnlyDictionary<string, int> PerBevistrinn, IReadOnlyDictionary<string, int> PerUkjentGrunn,
    IReadOnlyDictionary<string, int> PerRolle, IReadOnlyDictionary<string, int> PerRolleregel, IReadOnlyDictionary<string, int> PerVidereforingsform,
    IReadOnlyDictionary<string, int> PerDekning, IReadOnlyDictionary<string, int> PerKunnskapsstatus,
    HjemmelsstatusReferanserader Referanserader, HjemmelsstatusLovtidendTall Lovtidend, HjemmelsstatusForskrifter Forskrifter,
    int VaktAvviste, IReadOnlyList<HjemmelsstatusRapportRad>? Rader,
    // [Ny, #501] Kortnavnet fra tittelen for lovene med en lest oppheving (distinkte lover): «entydig», eller grunnen til at det ikke gis.
    IReadOnlyDictionary<string, int>? KortnavnFraTittel = null,
    // [Ny, #501] Med rader: lesingen per lov (ELI, kortnavnet eller null, grunnen).
    IReadOnlyList<HjemmelsstatusKortnavn>? KortnavnPerLov = null);

/// <summary>[Ny, #501] Kortnavnet fra tittelen for én opphevet lov.</summary>
public sealed record HjemmelsstatusKortnavn(string Eli, string? Kortnavn, string Grunn);

/// <summary>AK 4: referanseradene (hjemler som bare finnes som referanse-stub) for gjeldende primære forskrifter.</summary>
public sealed record HjemmelsstatusReferanserader(
    int Rader, int MedEntitetsstatusGjeldende, int Aktive, int AktiveViaEndringslov, IReadOnlyDictionary<string, int> PerBevistrinn, int OpphevetMedTekstbevis,
    int DokumenterOpphevetMedTekstbevis);

/// <summary>AK 3: gjeldende primære forskrifter med minst én hjemmel, per samlet status. Rene endringsvedtak (#446-kolonnen
/// <c>rent_endringsvedtak = true</c>) og dokumenter der markøren mangler (NULL), telles for seg.</summary>
public sealed record HjemmelsstatusForskrifter(
    int MedHjemmel, IReadOnlyDictionary<string, int> PerSamletStatus, IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> PerRentEndringsvedtak,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> PerPrefiks, int MinstEnHjemmelOpphevetMedTekstbevis, int MinstEnHjemmelIkkeAktiv,
    int Gjenstandslose);

/// <summary>
/// [Ny, #472 PR 1, Johanns avgjørelser 2026-11-18] <b>Maskinregelen for hjemmelsstatus</b> (<c>monster:hjemmelsstatus</c>): én status per rad i
/// <c>rettskilde_hjemler</c>, lest av teksten, og den samlede statusen per forskrift (<see cref="HjemmelsstatusSamlet"/>), som ikke lagres.
/// <para>
/// <b>Status per hjemmel.</b> Er hjemmelsdokumentet en gjeldende primærkilde, er hjemmelen aktiv når bestemmelsen finnes og ikke er merket
/// opphevet. Ellers (bare en referanse-stub, som alltid står med <c>entitetsstatus = gjeldende</c>; AK 4) leses opphevingen av teksten i andre
/// gjeldende primærkilder (bevistrinn E1, E1u, E2, og EN med Lovtidend), og videreføringen av overgangsreglene (V1–V4). En stub uten bevis er
/// ukjent, aldri aktiv.
/// </para>
/// <para>
/// <b>Rollen</b> settes bare der en navngitt regel gir den (normalisering, juristrunde 1 R1 og runde 2 R1); ellers NULL (ikke vurdert).
/// </para>
/// <para>
/// <b>Ikke her</b> [LÅST, oppdraget for #472 PR 1]: funnundertypen og premissporten (PR 2), og rettingene i #429-koden (PR 3). Tekstformene
/// PR 3 trenger, er delt i <see cref="Hjemmelstekstlesing"/>. [LØST, #472 PR 3] Lesingen av én tekst, formelen i foreldrenoden og
/// videreføringssøket er flyttet dit og brukes av begge regler.
/// </para>
/// </summary>
public sealed class HjemmelsstatusRegel(RegelIdeDbContext db, IKunngjortTekstkilde? kunngjort = null)
{
    private const string Grunnloven = "https://lovdata.no/eli/lov/1814/05/17/nor";

    private sealed record Kilde(Guid Id, string? Eli, string? DokumentId, string Tittel, string Kildetype, string Importrolle, string Entitetsstatus,
        DateOnly? Ikrafttredelse, string? AnnetOmDokumentet, bool? RentEndringsvedtak);

    private sealed record Node(Guid Id, Guid RettskildeId, string Eid, string? Tekst, int Rekkefolge, bool Opphevet, DateOnly? OpphevetDato, string? Overskrift);

    /// <summary>En tekst å lese: nodens egen tekst, eller barnets tekst med opphevingsformelen i forelderen foran (#429 PR 4 (1c)).</summary>
    private sealed record Lesning(Guid KildeId, Guid? NodeId, string Eid, string Tekst, int Rekkefolge, bool FraKunngjort);

    private sealed record Bevis(string Trinn, Guid KildeId, Guid? NodeId, string Eid, string Sitat, bool FraKunngjort);

    private sealed record Videreforingskandidat(string Form, Guid KildeId, Guid? NodeId, string Eid, string Sitat, bool FraKunngjort);

    /// <summary>Utfallet for ett hjemmelsdokument som ikke er gjeldende primærkilde.</summary>
    private sealed record Dokumentutfall(Bevis? Bevis, string? UkjentGrunn, IReadOnlyList<Videreforingskandidat> Videreforing, string? Premissmangel,
        bool IkkeIKraft, (Guid KildeId, Guid NodeId, string Eid)? Tildeling);

    private sealed class Data
    {
        public required Dictionary<Guid, Kilde> Kilder;
        public required Dictionary<string, Kilde> GjeldendePrimaer;
        public required Dictionary<string, HashSet<Guid>> Endrer;
        public required List<Lesning> Kandidater;
        public required Dictionary<Guid, List<Node>> NoderPerKilde; // alle noder i kilder som opphever
        public required List<Lesning> Videreforingsnoder; // lovnoder og overgangsforskrifter med hjemmelsuttrykk
        public required Dictionary<string, Node> MalNoder; // eId → node i en gjeldende primærkilde (hjemmelsbestemmelser og paragrafene over)
        public IReadOnlyDictionary<string, IReadOnlyList<KunngjortNode>> Kunngjort = new Dictionary<string, IReadOnlyList<KunngjortNode>>();
        /// <summary>[Ny, #501] Kortnavnet fra tittelen til en opphevet lov, når det er entydig (V4).</summary>
        public Kortnavnregister Kortnavn = Kortnavnregister.Tomt;
        public int VaktAvviste;
        public int LovtidendEtterspurt;
        public readonly Dictionary<string, Dokumentutfall> Utfall = new(StringComparer.Ordinal);
    }

    private sealed record Hjemmelsrad(Guid Id, Guid RettskildeId, string HjemmelEid, Guid HjemmelRettskildeId);

    public async Task<HjemmelsstatusRapport> KjorAsync(bool skriv, string bruker, bool lesLovtidend = true, bool medRader = false,
        IReadOnlyCollection<Guid>? forskriftIder = null, CancellationToken ct = default)
    {
        var naa = DateTimeOffset.UtcNow;
        var idag = DateOnly.FromDateTime(naa.UtcDateTime);
        var kilder = await db.Rettskilder.AsNoTracking()
            .Select(r => new Kilde(r.Id, r.Eli, r.DokumentId, r.Kortnavn ?? r.Tittel, r.Kildetype, r.Importrolle, r.Entitetsstatus, r.Ikrafttredelse,
                r.AnnetOmDokumentet, r.RentEndringsvedtak))
            .ToDictionaryAsync(r => r.Id, ct);
        var gjeldendePrimaer = kilder.Values.Where(k => k.Entitetsstatus == "gjeldende" && k.Importrolle == "primaer" && k.Eli is not null)
            .GroupBy(k => k.Eli!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Radene i omfanget. Med forskriftIder: de forskriftene, og forskriftene de bygger på (rekursjonen trenger dem).
        var alleRader = await db.RettskildeHjemler.AsNoTracking()
            .Select(h => new Hjemmelsrad(h.Id, h.RettskildeId, h.HjemmelEid, h.HjemmelRettskildeId)).ToListAsync(ct);
        var rader = alleRader;
        if (forskriftIder is not null)
        {
            var perF = alleRader.GroupBy(h => h.RettskildeId).ToDictionary(g => g.Key, g => g.ToList());
            var omfang = new HashSet<Guid>(forskriftIder);
            var ko = new Queue<Guid>(forskriftIder);
            while (ko.Count > 0)
            {
                foreach (var h in perF.GetValueOrDefault(ko.Dequeue()) ?? [])
                {
                    if (DokumentEli(h, kilder) is { } e && gjeldendePrimaer.TryGetValue(e, out var g) && g.Kildetype == "Forskrift" && omfang.Add(g.Id))
                        ko.Enqueue(g.Id);
                }
            }
            rader = alleRader.Where(h => omfang.Contains(h.RettskildeId)).ToList();
        }

        var data = await LastAsync(rader, kilder, gjeldendePrimaer, lesLovtidend, ct);
        var beregnet = rader.Select(h => BeregnRad(h, rader, data, idag, bruker, naa)).ToList();

        var lovtidend = LovtidendTall(rader, beregnet, data, lesLovtidend);
        int skrevne = 0, endret = 0;
        if (skriv)
        {
            var ider = beregnet.Select(b => b.RettskildeHjemmelId).ToList();
            var gamle = new Dictionary<Guid, string>();
            foreach (var bit in ider.Chunk(5000))
            {
                foreach (var g in await db.Hjemmelsstatuser.AsNoTracking().Where(x => bit.Contains(x.RettskildeHjemmelId))
                             .Select(x => new { x.RettskildeHjemmelId, x.Status }).ToListAsync(ct))
                    gamle[g.RettskildeHjemmelId] = g.Status;
            }
            endret = beregnet.Count(b => gamle.TryGetValue(b.RettskildeHjemmelId, out var s) && s != b.Status);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            foreach (var bit in ider.Chunk(5000))
                await db.Hjemmelsstatuser.Where(x => bit.Contains(x.RettskildeHjemmelId)).ExecuteDeleteAsync(ct);
            foreach (var bit in beregnet.Chunk(2000))
            {
                db.Hjemmelsstatuser.AddRange(bit);
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
            await tx.CommitAsync(ct);
            skrevne = beregnet.Count;
        }

        return Rapport(skriv, lesLovtidend, rader, beregnet, data, lovtidend, skrevne, endret, medRader);
    }

    // ---- Lasting -----------------------------------------------------------------------------------------------------------------------

    private static string? DokumentEli(Hjemmelsrad h, Dictionary<Guid, Kilde> kilder) =>
        kilder.GetValueOrDefault(h.HjemmelRettskildeId)?.Eli ?? (h.HjemmelEid.Contains("/eli/") ? Hjemmelstekstlesing.DokumentEli(h.HjemmelEid) : null);

    private async Task<Data> LastAsync(List<Hjemmelsrad> rader, Dictionary<Guid, Kilde> kilder, Dictionary<string, Kilde> gjeldendePrimaer,
        bool lesLovtidend, CancellationToken ct)
    {
        var dokEli = rader.Select(h => DokumentEli(h, kilder)).Where(e => e is not null).Select(e => e!).ToHashSet(StringComparer.Ordinal);
        // Endringslover som hjemmelstekstene navngir (R5), slik at de kan følges.
        foreach (var h in rader)
        {
            if (DokumentEli(h, kilder) is { } e && !gjeldendePrimaer.ContainsKey(e) && Hjemmelstekstlesing.Mal(e) is { } m
                && Hjemmelstekstlesing.EndringslovTil(kilder.GetValueOrDefault(h.RettskildeId)?.AnnetOmDokumentet, m) is { } fulgt)
                dokEli.Add(fulgt);
        }
        var mal = dokEli.Where(e => !gjeldendePrimaer.ContainsKey(e)).ToList();

        // «Endrer»: hvilke kilder har målet i Lovdatas felt.
        var endrer = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);
        foreach (var bit in mal.Chunk(2000))
        {
            foreach (var e in await db.RettskildeEndringer.AsNoTracking().Where(x => bit.Contains(x.EndringEid))
                         .Select(x => new { x.EndringEid, x.RettskildeId }).ToListAsync(ct))
            {
                if (!endrer.TryGetValue(e.EndringEid, out var s)) endrer[e.EndringEid] = s = [];
                s.Add(e.RettskildeId);
            }
        }

        // Kandidatnodene for opphevingen: noder i gjeldende primærkilder med et oppheve-ord, og barna til en opphevingsformel.
        var gjeldendeIder = gjeldendePrimaer.Values.Select(k => k.Id).ToHashSet();
        var gjeldendeListe = gjeldendeIder.ToList();
        var oppheveNoder = await db.RettskildeNoder.AsNoTracking()
            .Where(n => gjeldendeListe.Contains(n.RettskildeId) && n.Entitetsstatus == "gjeldende" && !n.Opphevet && !n.ErMerknad && n.Tekst != null
                        && (n.Tekst.Contains("pphev") || n.Tekst.Contains("avløs") || n.Tekst.Contains("erstatt") || n.Tekst.Contains("opphør")
                            || n.Tekst.Contains("oppheva")))
            .Select(n => new { n.Id, n.RettskildeId, n.Eid, n.Tekst, n.Sorteringsrekkefolge })
            .ToListAsync(ct);
        var formler = oppheveNoder.Where(n => UtgattAktorRegler.Opphevingsformel(Hjemmelstekstlesing.Normaliser(n.Tekst))).ToDictionary(n => n.Id, n => n.Tekst!);
        var kandidater = oppheveNoder.Select(n => new Lesning(n.RettskildeId, n.Id, n.Eid, n.Tekst!, n.Sorteringsrekkefolge, false)).ToList();
        if (formler.Count > 0)
        {
            var fIder = formler.Keys.ToList();
            foreach (var bit in fIder.Chunk(2000))
            {
                var barn = await db.RettskildeNoder.AsNoTracking()
                    .Where(n => n.ParentNodeId != null && bit.Contains(n.ParentNodeId.Value) && n.Entitetsstatus == "gjeldende" && !n.Opphevet
                                && !n.ErMerknad && n.Tekst != null)
                    .Select(n => new { n.Id, n.RettskildeId, n.Eid, n.Tekst, n.Sorteringsrekkefolge, Forelder = n.ParentNodeId!.Value })
                    .ToListAsync(ct);
                kandidater.AddRange(barn.Select(b => new Lesning(b.RettskildeId, b.Id, b.Eid, Hjemmelstekstlesing.MedForelderformel(formler[b.Forelder], b.Tekst)!,
                    b.Sorteringsrekkefolge, false)));
            }
        }

        // Nodene i hjemmelsbestemmelsene til gjeldende primærkilder, og paragrafene over dem (overskriften gir overgangsgrunnlag).
        var malEider = new HashSet<string>(StringComparer.Ordinal);
        foreach (var h in rader)
        {
            if (DokumentEli(h, kilder) is not { } e || !gjeldendePrimaer.ContainsKey(e) || h.HjemmelEid == e) continue;
            malEider.Add(h.HjemmelEid);
            if (Regex.Match(h.HjemmelEid, @"^(.*/§[^/]+)") is { Success: true } p) malEider.Add(p.Groups[1].Value);
        }
        var malNoder = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var bit in malEider.Chunk(2000))
        {
            foreach (var n in await db.RettskildeNoder.AsNoTracking().Where(n => bit.Contains(n.Eid) && n.Entitetsstatus == "gjeldende")
                         .Select(n => new Node(n.Id, n.RettskildeId, n.Eid, null, n.Sorteringsrekkefolge, n.Opphevet, n.OpphevetDato, n.Overskrift))
                         .ToListAsync(ct))
            {
                if (gjeldendeIder.Contains(n.RettskildeId)) malNoder[n.Eid] = n;
            }
        }

        // Videreføringssøket i andre kilder (V2a): gjeldende lover, og overgangsforskrifter, med et hjemmelsuttrykk.
        var lovIder = gjeldendePrimaer.Values.Where(k => k.Kildetype == "Lov").Select(k => k.Id).ToHashSet();
        var overgangsforskrifter = gjeldendePrimaer.Values
            .Where(k => k.Kildetype == "Forskrift" && Regex.IsMatch(k.Tittel, @"overgang", RegexOptions.IgnoreCase)).Select(k => k.Id).ToHashSet();
        var vfIder = lovIder.Concat(overgangsforskrifter).ToList();
        var vfNoder = await db.RettskildeNoder.AsNoTracking()
            .Where(n => vfIder.Contains(n.RettskildeId) && n.Entitetsstatus == "gjeldende" && !n.Opphevet && !n.ErMerknad && n.Tekst != null
                        && (n.Tekst.Contains("medh") || n.Tekst.Contains("hjemmel i") || n.Tekst.Contains("heimel i") || n.Tekst.Contains("gitt etter")
                            || n.Tekst.Contains("fastsatt etter") || n.Tekst.Contains("truffet etter") || n.Tekst.Contains("gjeve etter")))
            .Select(n => new { n.Id, n.RettskildeId, n.Eid, n.Tekst, n.Sorteringsrekkefolge })
            .ToListAsync(ct);
        var videreforingsnoder = vfNoder
            .Select(n => new Lesning(n.RettskildeId, n.Id, n.Eid, n.Tekst!, n.Sorteringsrekkefolge, false)).ToList();

        var data = new Data
        {
            Kilder = kilder, GjeldendePrimaer = gjeldendePrimaer, Endrer = endrer, Kandidater = kandidater, NoderPerKilde = [],
            Videreforingsnoder = videreforingsnoder, MalNoder = malNoder, Kortnavn = await Kortnavnregister.LastAsync(db, ct),
        };

        // Bevistrinnene i korpuset først; så de opphevende kildenes noder (videreføring, ikraftsetting, tildeling); så Lovtidend for EN.
        var bevis = mal.ToDictionary(e => e, e => BevisIKorpus(e, data), StringComparer.Ordinal);
        var enKilder = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (e, b) in bevis)
        {
            if (b.Bevis is null && b.Grunn == Hjemmelsstatus.GrunnEn)
            {
                foreach (var k in EndrerKilder(e, data)) if (k.Eli is not null) enKilder.Add(k.Eli);
            }
        }
        data.LovtidendEtterspurt = enKilder.Count;
        if (lesLovtidend && kunngjort is not null && enKilder.Count > 0) data.Kunngjort = await kunngjort.HentAsync(enKilder, ct);
        foreach (var e in bevis.Keys.ToList())
        {
            if (bevis[e].Bevis is null && bevis[e].Grunn == Hjemmelsstatus.GrunnEn && BevisILovtidend(e, data) is { } lti) bevis[e] = (lti, null);
        }

        var opphevende = bevis.Values.Where(b => b.Bevis is not null).Select(b => b.Bevis!.KildeId)
            .Concat(bevis.Where(b => b.Value.Bevis is null && b.Value.Grunn == Hjemmelsstatus.GrunnEn).SelectMany(b => EndrerKilder(b.Key, data)).Select(k => k.Id))
            .ToHashSet();
        foreach (var bit in opphevende.ToList().Chunk(500))
        {
            foreach (var n in await db.RettskildeNoder.AsNoTracking()
                         .Where(n => bit.Contains(n.RettskildeId) && n.Entitetsstatus == "gjeldende" && !n.Opphevet && !n.ErMerknad && n.Tekst != null)
                         .Select(n => new Node(n.Id, n.RettskildeId, n.Eid, n.Tekst, n.Sorteringsrekkefolge, false, null, n.Overskrift)).ToListAsync(ct))
            {
                if (!data.NoderPerKilde.TryGetValue(n.RettskildeId, out var l)) data.NoderPerKilde[n.RettskildeId] = l = [];
                l.Add(n);
            }
        }
        foreach (var l in data.NoderPerKilde.Values) l.Sort((a, b) => a.Rekkefolge != b.Rekkefolge ? a.Rekkefolge.CompareTo(b.Rekkefolge) : string.CompareOrdinal(a.Eid, b.Eid));

        foreach (var (e, b) in bevis) data.Utfall[e] = Utfall(e, b.Bevis, b.Grunn, data);
        return data;
    }

    private static IEnumerable<Kilde> EndrerKilder(string eli, Data data) =>
        (data.Endrer.GetValueOrDefault(eli) ?? []).Select(id => data.Kilder.GetValueOrDefault(id))
            .Where(k => k is { Entitetsstatus: "gjeldende", Importrolle: "primaer" } && k.Eli != eli).Select(k => k!)
            .OrderBy(k => k.DokumentId, StringComparer.Ordinal);

    // ---- Bevistrinnene (fase 1, m2) ----------------------------------------------------------------------------------------------------

    private static readonly Regex Aarstall = new(@"\b(1[6-9]\d\d|20\d\d)\b", RegexOptions.Compiled);

    private Dictionary<string, List<Lesning>>? _kandidaterPerAar;

    private (Bevis? Bevis, string? Grunn) BevisIKorpus(string eli, Data data)
    {
        if (Hjemmelstekstlesing.Mal(eli) is not { } mal) return (null, Hjemmelsstatus.GrunnIngen);
        _kandidaterPerAar ??= IndekserTreff(data);
        Bevis? e1 = null, e1u = null, e2 = null;
        var e3 = false;
        var rx = new Regex(Hjemmelstekstlesing.MalRegex(mal));
        var endrer = data.Endrer.GetValueOrDefault(eli) ?? [];
        foreach (var n in (_kandidaterPerAar.GetValueOrDefault(mal.Aar.ToString()) ?? [])
                     .OrderBy(n => data.Kilder.GetValueOrDefault(n.KildeId)?.DokumentId ?? "", StringComparer.Ordinal).ThenBy(n => n.Rekkefolge))
        {
            if (data.Kilder.GetValueOrDefault(n.KildeId)?.Eli == eli) continue;
            var tn = Hjemmelstekstlesing.Normaliser(n.Tekst);
            if (!rx.IsMatch(tn)) continue;
            var (s, utvidet) = LesOppheving(n.Tekst, mal, data);
            if (s is not null)
            {
                if (endrer.Contains(n.KildeId))
                {
                    if (utvidet) e1u ??= new Bevis(Hjemmelsstatus.E1u, n.KildeId, n.NodeId, n.Eid, s, false);
                    else e1 ??= new Bevis(Hjemmelsstatus.E1, n.KildeId, n.NodeId, n.Eid, s, false);
                }
                else if (Hjemmelstekstlesing.E2Vilkar(s, mal))
                {
                    e2 ??= new Bevis(Hjemmelsstatus.E2, n.KildeId, n.NodeId, n.Eid, s, false);
                }
            }
            else if (Hjemmelstekstlesing.HarOpphevingsord(n.Tekst))
            {
                e3 = true;
            }
        }
        if ((e1 ?? e1u ?? e2) is { } b) return (b, null);
        if (EndrerKilder(eli, data).Any()) return (null, Hjemmelsstatus.GrunnEn);
        return (null, e3 ? Hjemmelsstatus.GrunnE3 : Hjemmelsstatus.GrunnIngen);
    }

    /// <summary>Hel opphevingssetning, ellers den utvidede formen; begge gjennom vakten. Returnerer sitatet og om det var den utvidede formen.
    /// [ENDRET, #472 PR 3] Lesingen er flyttet til <see cref="Hjemmelstekstlesing.LesOppheving"/>, som R8, R9 og målvedtaket i #429 også bruker.</summary>
    private static (string? Sitat, bool Utvidet) LesOppheving(string tekst, Dokumentmal mal, Data data)
    {
        var (l, avvist) = Hjemmelstekstlesing.LesOppheving(tekst, mal);
        if (avvist) data.VaktAvviste++;
        return (l?.Sitat, l?.Utvidet ?? false);
    }

    private static Dictionary<string, List<Lesning>> IndekserTreff(Data data)
    {
        var ut = new Dictionary<string, List<Lesning>>(StringComparer.Ordinal);
        foreach (var n in data.Kandidater)
        {
            foreach (var aar in Aarstall.Matches(n.Tekst).Select(m => m.Value).Distinct())
            {
                if (!ut.TryGetValue(aar, out var l)) ut[aar] = l = [];
                l.Add(n);
            }
        }
        return ut;
    }

    /// <summary>EN med Lovtidend: den kunngjorte teksten til kildene som har målet i «Endrer», med samme kontroll og vakt som i korpuset.</summary>
    private static Bevis? BevisILovtidend(string eli, Data data)
    {
        if (Hjemmelstekstlesing.Mal(eli) is not { } mal) return null;
        foreach (var k in EndrerKilder(eli, data))
        {
            if (k.Eli is null || !data.Kunngjort.TryGetValue(k.Eli, out var noder)) continue;
            foreach (var l in Kunngjortlesninger(k.Id, noder))
            {
                if (!Hjemmelstekstlesing.Nevner(l.Tekst, mal)) continue;
                var (s, _) = LesOppheving(l.Tekst, mal, data);
                if (s is not null) return new Bevis(Hjemmelsstatus.EnLovtidend, k.Id, null, l.Eid, s, true);
            }
        }
        return null;
    }

    private static IEnumerable<Lesning> Kunngjortlesninger(Guid kildeId, IReadOnlyList<KunngjortNode> noder)
    {
        var tekstPerEid = noder.Where(n => n.Tekst is not null).GroupBy(n => n.Eid).ToDictionary(g => g.Key, g => g.First().Tekst!, StringComparer.Ordinal);
        foreach (var n in noder.OrderBy(n => n.Rekkefolge))
        {
            if (n.Tekst is null) continue;
            yield return new Lesning(kildeId, null, n.Eid, n.Tekst, n.Rekkefolge, true);
            if (n.ForelderEid is { } f && Hjemmelstekstlesing.MedForelderformel(tekstPerEid.GetValueOrDefault(f), n.Tekst) is { } medFormel)
                yield return new Lesning(kildeId, null, n.Eid, medFormel, n.Rekkefolge, true);
        }
    }

    // ---- Utfallet for et dokument: videreføring, ikraftsetting, tildeling ----------------------------------------------------------------

    private static Dokumentutfall Utfall(string eli, Bevis? bevis, string? grunn, Data data)
    {
        if (Hjemmelstekstlesing.Mal(eli) is not { } mal) return new Dokumentutfall(null, grunn ?? Hjemmelsstatus.GrunnIngen, [], null, false, null);
        if (bevis is null)
        {
            // EN uten Lovtidend-treff: videreføringen i «Endrer»-kildene vises, men opphevingen er ikke lest (fase 1: «videreført, opphevingen
            // ikke lest»). Statusen forblir ukjent.
            var vf = grunn == Hjemmelsstatus.GrunnEn
                ? EndrerKilder(eli, data).Where(k => k.Kildetype == "Lov").SelectMany(k => Videreforinger(eli, mal, k.Id, null, data)).Take(1).ToList()
                : [];
            return new Dokumentutfall(null, grunn, vf, null, false, null);
        }
        var o = data.Kilder[bevis.KildeId];
        var idag = DateOnly.FromDateTime(DateTime.UtcNow);
        var noder = data.NoderPerKilde.GetValueOrDefault(o.Id) ?? [];
        var premissmangel = noder.Any(n => Hjemmelstekstlesing.UlikIkraftsetting(n.Tekst))
            ? $"Ikraftsettingen av opphevingsbestemmelsen er ikke kontrollert: {o.DokumentId ?? o.Eli} kan settes i kraft til ulik tid, og korpuset har ikrafttredelse bare per dokument."
            : null;
        var kunngjorte = o.Eli is not null && bevis.FraKunngjort ? data.Kunngjort.GetValueOrDefault(o.Eli) : null;
        var tildeling = noder.FirstOrDefault(n => n.Id != bevis.NodeId && n.Eid != bevis.Eid && Hjemmelstekstlesing.TildelerKompetanse(n.Tekst));
        return new Dokumentutfall(bevis, null, Videreforinger(eli, mal, o.Id, kunngjorte, data).ToList(), premissmangel,
            o.Ikrafttredelse is { } d && d > idag, tildeling is null ? null : (o.Id, tildeling.Id, tildeling.Eid));
    }

    /// <summary>Videreføringskandidatene i fase 1-rekkefølgen: V1, V2 og V4 i den opphevende loven, V2a i andre lover og overgangsforskrifter,
    /// V3 (generisk) i den opphevende loven. Den første av hver form.</summary>
    private static IEnumerable<Videreforingskandidat> Videreforinger(string eli, Dokumentmal mal, Guid opphevende,
        IReadOnlyList<KunngjortNode>? kunngjorte, Data data)
    {
        var egne = (data.NoderPerKilde.GetValueOrDefault(opphevende) ?? [])
            .Select(n => new Lesning(n.RettskildeId, n.Id, n.Eid, n.Tekst ?? "", n.Rekkefolge, false))
            .Concat(kunngjorte is null ? [] : kunngjorte.Where(n => n.Tekst is not null).OrderBy(n => n.Rekkefolge)
                .Select(n => new Lesning(opphevende, null, n.Eid, n.Tekst!, n.Rekkefolge, true)))
            .ToList();
        var rxLos = new Regex(Hjemmelstekstlesing.MalRegexLos(mal));
        var andre = data.Videreforingsnoder
            .Where(n => n.KildeId != opphevende && data.Kilder.GetValueOrDefault(n.KildeId)?.Eli != eli && rxLos.IsMatch(Hjemmelstekstlesing.Normaliser(n.Tekst)))
            .OrderBy(n => data.Kilder.GetValueOrDefault(n.KildeId)?.DokumentId ?? "", StringComparer.Ordinal).ThenBy(n => n.Rekkefolge);
        // [ENDRET, #472 PR 3] Formene og rekkefølgen er flyttet til Hjemmelstekstlesing.Videreforinger, som R9-hintet i #429 også bruker.
        // [ENDRET, #501 (#472 PR 5)] Kortnavnet fra tittelen til den opphevede loven godtas som mål i V4 når det er entydig.
        foreach (var (form, n, s) in Hjemmelstekstlesing.Videreforinger(egne, l => l.Tekst, mal, andre, data.Kortnavn.Entydig(eli)))
            yield return new Videreforingskandidat(form, n.KildeId, n.NodeId, n.Eid, s, n.FraKunngjort);
    }

    // ---- Én rad ------------------------------------------------------------------------------------------------------------------------

    private HjemmelsstatusEntitet BeregnRad(Hjemmelsrad h, List<Hjemmelsrad> alle, Data data, DateOnly idag, string bruker, DateTimeOffset naa)
    {
        var f = data.Kilder.GetValueOrDefault(h.RettskildeId);
        var eli = DokumentEli(h, data.Kilder);
        var r = new HjemmelsstatusEntitet
        {
            Id = Guid.NewGuid(), RettskildeHjemmelId = h.Id, RettskildeId = h.RettskildeId, HjemmelEid = h.HjemmelEid, HjemmelEli = eli,
            Status = Hjemmelsstatus.Ukjent, Kunnskapsstatus = Strukturkanter.Uavklart, BeregnetAv = bruker, BeregnetTidspunkt = naa,
        };
        var gj = eli is null ? null : data.GjeldendePrimaer.GetValueOrDefault(eli);
        r.MalRettskildeId = gj?.Id;
        (r.Rolle, r.RolleRegel) = Rolle(h, eli, gj, f, alle, data);

        if (eli is null)
        {
            r.UkjentGrunn = Hjemmelsstatus.GrunnMangler;
            return r;
        }
        if (gj is not null)
        {
            SettPrimaer(r, h.HjemmelEid, eli, data);
            return r;
        }
        if (!data.Utfall.TryGetValue(eli, out var u))
        {
            r.UkjentGrunn = Hjemmelsstatus.GrunnMangler;
            return r;
        }
        // R5: en endringslov som navngir nøyaktig én lov, følges til den loven når selve endringsloven ikke er lest som opphevet. Kan den ikke
        // følges («… og enkelte andre lover»), eller er loven den endrer også ukjent, er grunnen END.
        var endringslov = u.Bevis is null && Hjemmelstekstlesing.Mal(eli) is { } em && Hjemmelstekstlesing.ErEndringslov(f?.AnnetOmDokumentet, em);
        // [ENDRET, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1, H18] Viser hjemmelsteksten til en egen del av endringsloven
        // («… om fagskoleutdanning del II nr. 1»), er hjemmelen endringslovens egen bestemmelse (ikrafttredelse, overgang, eller endring i en annen
        // lov), ikke bestemmelsene den føyer inn i hovedloven. Den følges ikke: DEL/forskrift/2007-06-29-759 fikk ellers statusen til fagskoleloven
        // 2003, og funnet sa at endringsloven var opphevet ved en setning som opphever hovedloven. Endringsloven er ikke i korpus, så hva delen
        // inneholder, kan ikke leses. Statusen blir ukjent (END), ikke gjettet.
        if (endringslov && Hjemmelstekstlesing.EndringslovensEgenDel(f?.AnnetOmDokumentet, Hjemmelstekstlesing.Mal(eli)!) is not null)
        {
            SettUtfall(r, u, f, h.HjemmelEid, data, idag);
            if (r.Status == Hjemmelsstatus.Ukjent && r.Bevistrinn is null) r.UkjentGrunn = Hjemmelsstatus.GrunnEnd;
            return r;
        }
        if (endringslov && Hjemmelstekstlesing.EndringslovTil(f?.AnnetOmDokumentet, Hjemmelstekstlesing.Mal(eli)!) is { } fulgt)
        {
            r.FulgtEli = fulgt;
            if (data.GjeldendePrimaer.TryGetValue(fulgt, out var fg))
            {
                r.MalRettskildeId = fg.Id;
                SettPrimaer(r, fulgt, fulgt, data);
                return r;
            }
            if (data.Utfall.TryGetValue(fulgt, out var fu)) u = fu;
        }
        SettUtfall(r, u, f, h.HjemmelEid, data, idag);
        if (endringslov && r.Status == Hjemmelsstatus.Ukjent && r.Bevistrinn is null) r.UkjentGrunn = Hjemmelsstatus.GrunnEnd;
        return r;
    }

    private static void SettPrimaer(HjemmelsstatusEntitet r, string hjemmelEid, string eli, Data data)
    {
        r.Kunnskapsstatus = Strukturkanter.Normalisering;
        if (hjemmelEid == eli)
        {
            r.Status = Hjemmelsstatus.Aktiv;
            return;
        }
        if (!data.MalNoder.TryGetValue(hjemmelEid, out var n))
        {
            r.Status = Hjemmelsstatus.Ukjent;
            r.UkjentGrunn = Hjemmelsstatus.GrunnEidIkkeFunnet;
            r.Kunnskapsstatus = Strukturkanter.Uavklart;
            return;
        }
        if (n.Opphevet)
        {
            r.Status = Hjemmelsstatus.OpphevetBestemmelse;
            r.UkjentGrunn = Hjemmelsstatus.GrunnBestemmelseOpphevet;
            r.OpphevetDato = n.OpphevetDato;
            return;
        }
        r.Status = Hjemmelsstatus.Aktiv;
    }

    private static void SettUtfall(HjemmelsstatusEntitet r, Dokumentutfall u, Kilde? f, string hjemmelEid, Data data, DateOnly idag)
    {
        if (u.Bevis is not { } b)
        {
            r.UkjentGrunn = u.UkjentGrunn ?? Hjemmelsstatus.GrunnIngen;
            // EN uten Lovtidend-treff: videreføringen står for seg (opphevingen ikke lest), statusen er ukjent.
            if (u.Videreforing.FirstOrDefault() is { } vfEn) SettVidereforing(r, vfEn, Hjemmelstekstlesing.Mal(r.HjemmelEli!)!, f, hjemmelEid);
            return;
        }
        r.Bevistrinn = b.Trinn;
        r.OpphevetAvRettskildeId = b.KildeId;
        r.OpphevetAvNodeId = b.NodeId;
        r.OpphevetAvEid = b.Eid;
        r.OpphevetAvSitat = b.Sitat;
        r.OpphevetFraKunngjort = b.FraKunngjort;
        r.OpphevetDato = data.Kilder.GetValueOrDefault(b.KildeId)?.Ikrafttredelse;
        r.Premissmangel = u.Premissmangel;
        var bevisstatus = b.FraKunngjort ? Strukturkanter.EksterntDokumentert : Strukturkanter.Tekstfunn;
        if (u.IkkeIKraft)
        {
            r.Status = Hjemmelsstatus.Ukjent;
            r.UkjentGrunn = Hjemmelsstatus.GrunnIkkeIKraft;
            r.Kunnskapsstatus = Strukturkanter.Uavklart;
            return;
        }
        if (r.Rolle == Hjemmelsstatus.Gjenstand)
        {
            r.Status = Hjemmelsstatus.Gjenstandslos;
            r.Kunnskapsstatus = Strukturkanter.Normalisering;
            return;
        }
        if (r.Rolle == Hjemmelsstatus.Kompetanseledd && u.Tildeling is { } t)
        {
            r.Status = Hjemmelsstatus.Erstattet;
            r.ErstattetAvRettskildeId = t.KildeId;
            r.ErstattetAvNodeId = t.NodeId;
            r.ErstattetAvEid = t.Eid;
            r.Kunnskapsstatus = Strukturkanter.Fortolkning;
            return;
        }
        var mal = Hjemmelstekstlesing.Mal(r.HjemmelEli!)!;
        foreach (var v in u.Videreforing)
        {
            if (SettVidereforing(r, v, mal, f, hjemmelEid) is not { } ks) continue;
            r.Status = r.VidereforingTidsfrist is { } frist && frist < idag ? Hjemmelsstatus.VidereforingUtlopt : Hjemmelsstatus.Viderefort;
            r.Kunnskapsstatus = SvakestePremiss.Rang(ks) > SvakestePremiss.Rang(bevisstatus) ? ks : bevisstatus;
            return;
        }
        r.Status = Hjemmelsstatus.Opphevet;
        r.Kunnskapsstatus = bevisstatus;
    }

    /// <summary>Setter videreføringsfeltene når kandidaten dekker hjemmelen, og returnerer kunnskapsstatusen den gir. Null når avgrensningen
    /// i setningen ikke dekker hjemmelens paragraf (S12): da er beviset ikke gyldig for denne hjemmelen.</summary>
    private static string? SettVidereforing(HjemmelsstatusEntitet r, Videreforingskandidat v, Dokumentmal mal, Kilde? f, string hjemmelEid)
    {
        string? dekning = null;
        var (kap, par) = Hjemmelstekstlesing.Avgrensning(v.Sitat, mal);
        if (kap.Count + par.Count > 0)
        {
            if (Hjemmelstekstlesing.Paragraf(hjemmelEid) is not { } p) dekning = Hjemmelsstatus.AvgrensningIkkeKontrollert;
            else if (!par.Contains(p) && !(p.Contains('-') && kap.Contains(p[..p.IndexOf('-')]))) return null;
        }
        if (dekning is null)
        {
            var typer = Hjemmelstekstlesing.Dokumenttyper(v.Sitat);
            var dekket = Dokumenttype(f) switch
            {
                "delegering" => typer.Contains("delegering"),
                "instruks" => typer.Contains("instruks"),
                _ => typer.Contains("forskrift") || typer.Contains("reglement") || typer.Contains("vedtekt"),
            };
            dekning = dekket ? Hjemmelsstatus.Dekket : Hjemmelsstatus.DokumenttypeIkkeNevnt;
        }
        r.VidereforingRettskildeId = v.KildeId;
        r.VidereforingNodeId = v.NodeId;
        r.VidereforingEid = v.Eid;
        r.VidereforingSitat = v.Sitat;
        r.VidereforingForm = v.Form;
        r.VidereforingFraKunngjort = v.FraKunngjort;
        r.VidereforingForbehold = Hjemmelstekstlesing.Forbehold(v.Sitat);
        r.VidereforingTidsfrist = Hjemmelstekstlesing.Tidsfrist(v.Sitat);
        r.VidereforingDekning = dekning;
        var ks = v.Form is "V3" or "V4" ? Strukturkanter.Normalisering : v.FraKunngjort ? Strukturkanter.EksterntDokumentert : Strukturkanter.Tekstfunn;
        if (r.VidereforingForbehold is not null || dekning != Hjemmelsstatus.Dekket) ks = Strukturkanter.Fortolkning;
        return ks;
    }

    /// <summary>Hva slags vedtak forskriften er, for dekningen: delegering (DEL eller tittel «Delegering/Tildeling/Overføring …»), instruks,
    /// ellers forskrift.</summary>
    private static string Dokumenttype(Kilde? f)
    {
        if (f is null) return "forskrift";
        if ((f.DokumentId ?? "").StartsWith("DEL/", StringComparison.Ordinal) || ErDelegeringstittel(f.Tittel)) return "delegering";
        if ((f.DokumentId ?? "").StartsWith("INS/", StringComparison.Ordinal) || f.Tittel.StartsWith("Instruks", StringComparison.OrdinalIgnoreCase)) return "instruks";
        return "forskrift";
    }

    private static bool ErDelegeringstittel(string tittel) =>
        Regex.IsMatch(tittel, @"^(Delegering|Delegasjon|Tildeling|Overføring)\b.*\b(myndighet|kompetanse|forskriftskompetanse)", RegexOptions.IgnoreCase);

    // ---- Rollen (normalisering) -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Rollen etter navngitte regler, i rekkefølge; ellers NULL (ikke vurdert):
    /// <list type="number">
    /// <item><c>grl_12</c>: Grunnloven § 12 er ressortfordeling, ikke hjemmel for innholdet → kompetanseledd (juristrunde 1, svaret på R1).</item>
    /// <item><c>delegeringsvedtak</c>: hjemmelsdokumentet er selv et delegeringsvedtak (Lovdata-basen DEL, eller tittelen «Delegering/Tildeling/
    /// Overføring av myndighet») → kompetanseledd (runde 2 R1).</item>
    /// <item><c>overgangsparagraf</c>: bestemmelsen står i en paragraf med «overgang» i overskriften → overgangsgrunnlag.</item>
    /// <item><c>ressortvedtak_gjenstand</c>: forskriften har Grl. § 12 som hjemmel, og hjemmelsteksten nevner ikke denne loven → gjenstand (S08).</item>
    /// <item><c>jf_vedtak</c>: hjemmelsteksten nevner dokumentet etter «jf.» (eller «delegeringsvedtak»), og det er et vedtak, ikke en lov →
    /// kompetanseledd.</item>
    /// <item><c>med_hjemmel_i</c>: hjemmelsteksten nevner loven etter «med hjemmel i», før «jf.» → materiell.</item>
    /// <item>[Ny, #472 PR 4, H15] <c>forskrift_som_hjemmel</c>: hjemmelen er en forskrift eller et vedtak, og ingen regel over har gitt rollen →
    /// kompetanseledd.</item>
    /// </list>
    /// </summary>
    private static (string? Rolle, string? Regel) Rolle(Hjemmelsrad h, string? eli, Kilde? malKilde, Kilde? f, List<Hjemmelsrad> alle, Data data)
    {
        if (eli is null) return (null, null);
        if (eli == Grunnloven && Hjemmelstekstlesing.Paragraf(h.HjemmelEid) == "12") return (Hjemmelsstatus.Kompetanseledd, "grl_12");
        if (malKilde is not null && ((malKilde.DokumentId ?? "").StartsWith("DEL/", StringComparison.Ordinal) || ErDelegeringstittel(malKilde.Tittel)))
            return (Hjemmelsstatus.Kompetanseledd, "delegeringsvedtak");
        if (h.HjemmelEid != eli && Regex.Match(h.HjemmelEid, @"^(.*/§[^/]+)") is { Success: true } p
            && data.MalNoder.TryGetValue(p.Groups[1].Value, out var pn) && pn.Overskrift is { } o && Regex.IsMatch(o, "overgang", RegexOptions.IgnoreCase))
            return (Hjemmelsstatus.Overgangsgrunnlag, "overgangsparagraf");

        var (rolle, regel) = RolleFraHjemmelsteksten(h, eli, f, alle, data);
        // [Ny, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1, H15] En hjemmel som selv er en forskrift eller et vedtak, og som
        // ingen regel over har gitt rolle, er et kompetanseledd: en forskrift kan bare være hjemmel for en annen forskrift ved å delegere
        // kompetanse. FOR-1975-12-29-1 sto med rolle NULL i SF/forskrift/1976-08-03-9783 og ga «uavklart», selv om den materielle hjemmelen
        // (sivilforsvarsloven 1953 § 41) er videreført. Kompetanseleddet teller bare når det ikke finnes materielle hjemler eller hjemler med
        // rolle NULL (§5.20 punkt 1), og en gjeldende forskrift som hjemmel arver da sin egen samlede status (R1).
        if (rolle is null && eli.Contains("/eli/forskrift/", StringComparison.Ordinal))
            return (Hjemmelsstatus.Kompetanseledd, Hjemmelsstatus.RolleregelForskriftSomHjemmel);
        return (rolle, regel);
    }

    private static (string? Rolle, string? Regel) RolleFraHjemmelsteksten(Hjemmelsrad h, string eli, Kilde? f, List<Hjemmelsrad> alle, Data data)
    {
        var tekst = Hjemmelstekstlesing.Normaliser(f?.AnnetOmDokumentet);
        if (tekst.Length == 0 || Hjemmelstekstlesing.Mal(eli) is not { } mal) return (null, null);
        var treff = Regex.Match(tekst, Hjemmelstekstlesing.MalRegex(mal));
        var harGrl12 = alle.Any(a => a.RettskildeId == h.RettskildeId && DokumentEli(a, data.Kilder) == Grunnloven);
        if (!treff.Success)
        {
            return harGrl12 && eli.Contains("/eli/lov/", StringComparison.Ordinal) && eli != Grunnloven
                ? (Hjemmelsstatus.Gjenstand, "ressortvedtak_gjenstand") : (null, null);
        }
        var foran = tekst[..treff.Index];
        var hjemmel = Math.Max(foran.LastIndexOf("hjemmel", StringComparison.OrdinalIgnoreCase), foran.LastIndexOf("medhold", StringComparison.OrdinalIgnoreCase));
        var jf = Regex.Matches(foran, @"\bjfr?\.").LastOrDefault()?.Index ?? -1;
        var erVedtak = eli.Contains("/eli/forskrift/", StringComparison.Ordinal);
        if (erVedtak && (jf > hjemmel || Regex.IsMatch(foran, @"\bdelegeringsvedtak(et)?\s+(av\s+)?$", RegexOptions.IgnoreCase)))
            return (Hjemmelsstatus.Kompetanseledd, "jf_vedtak");
        if (!erVedtak && hjemmel >= 0 && jf < hjemmel) return (Hjemmelsstatus.Materiell, "med_hjemmel_i");
        return (null, null);
    }

    // ---- Rapporten --------------------------------------------------------------------------------------------------------------------

    private static HjemmelsstatusLovtidendTall LovtidendTall(List<Hjemmelsrad> rader, List<HjemmelsstatusEntitet> beregnet, Data data, bool lest)
    {
        var enDok = data.Utfall.Where(u => u.Value.UkjentGrunn == Hjemmelsstatus.GrunnEn || u.Value.Bevis?.Trinn == Hjemmelsstatus.EnLovtidend)
            .Select(u => u.Key).ToHashSet(StringComparer.Ordinal);
        var lost = data.Utfall.Where(u => u.Value.Bevis?.Trinn == Hjemmelsstatus.EnLovtidend).Select(u => u.Key).ToHashSet(StringComparer.Ordinal);
        var enRader = beregnet.Where(b => b.HjemmelEli is { } e && enDok.Contains(e) && b.MalRettskildeId is null && b.FulgtEli is null).ToList();
        return new HjemmelsstatusLovtidendTall(lest, data.LovtidendEtterspurt, data.Kunngjort.Count, enDok.Count, lost.Count, enRader.Count,
            enRader.Count(b => b.Bevistrinn == Hjemmelsstatus.EnLovtidend));
    }

    private static IReadOnlyDictionary<string, int> Tell(IEnumerable<string?> verdier) =>
        verdier.GroupBy(v => v ?? "(ingen)").OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count());

    private static HjemmelsstatusRapport Rapport(bool skriv, bool lovtidend, List<Hjemmelsrad> rader, List<HjemmelsstatusEntitet> beregnet, Data data,
        HjemmelsstatusLovtidendTall lt, int skrevne, int endret, bool medRader)
    {
        var gjeldendeForskrift = beregnet.Where(b => data.Kilder.GetValueOrDefault(b.RettskildeId) is { Entitetsstatus: "gjeldende", Importrolle: "primaer", Kildetype: "Forskrift" }).ToList();
        // Referanserader: hjemmelsdokumentet finnes ikke som gjeldende primærkilde (bare som referanse-stub).
        var referanse = gjeldendeForskrift.Where(b => b.HjemmelEli is null || !data.GjeldendePrimaer.ContainsKey(b.HjemmelEli)).ToList();
        var stubIder = rader.ToDictionary(h => h.Id, h => data.Kilder.GetValueOrDefault(h.HjemmelRettskildeId));
        var refRader = new HjemmelsstatusReferanserader(
            referanse.Count,
            referanse.Count(b => stubIder.GetValueOrDefault(b.RettskildeHjemmelId)?.Entitetsstatus == "gjeldende"),
            referanse.Count(b => b.Status == Hjemmelsstatus.Aktiv && b.FulgtEli is null),
            referanse.Count(b => b.Status == Hjemmelsstatus.Aktiv && b.FulgtEli is not null),
            Tell(referanse.Select(b => b.Bevistrinn ?? b.UkjentGrunn)),
            referanse.Count(b => b.Bevistrinn is not null),
            referanse.Where(b => b.Bevistrinn is not null).Select(b => b.HjemmelEli).Distinct().Count());

        // Samlet status for gjeldende primære forskrifter med minst én hjemmel.
        var perF = beregnet.GroupBy(b => b.RettskildeId).ToDictionary(g => g.Key, g => g.ToList());
        IReadOnlyList<HjemmelPremiss>? Premisser(Guid id) => perF.TryGetValue(id, out var l)
            ? l.Select(b => new HjemmelPremiss(b.RettskildeHjemmelId, b.HjemmelEid, b.Rolle, b.Status, b.Kunnskapsstatus,
                b.MalRettskildeId is { } m && data.Kilder.GetValueOrDefault(m)?.Kildetype == "Forskrift" ? m : null, b.VidereforingDekning)).ToList()
            : null;
        var memo = new Dictionary<Guid, SamletHjemmelsstatus>();
        var forskrifter = gjeldendeForskrift.Select(b => b.RettskildeId).Distinct().ToList();
        var samlet = forskrifter.ToDictionary(id => id, id => HjemmelsstatusSamlet.Beregn(id, Premisser, memo));
        string Endringsvedtak(Guid id) => data.Kilder[id].RentEndringsvedtak switch
        {
            true => "rent_endringsvedtak",
            _ => "ikke_klassifisert",
        };
        string Prefiks(Guid id) => (data.Kilder[id].DokumentId ?? "(ingen)").Split('/')[0];
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Del(Func<Guid, string> nokkel) =>
            samlet.GroupBy(s => nokkel(s.Key)).OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => Tell(g.Select(s => (string?)s.Value.Status)));
        var fTall = new HjemmelsstatusForskrifter(
            samlet.Count, Tell(samlet.Values.Select(s => (string?)s.Status)), Del(Endringsvedtak), Del(Prefiks),
            forskrifter.Count(id => perF[id].Any(b => b.Bevistrinn is not null)),
            forskrifter.Count(id => perF[id].Any(b => b.Status != Hjemmelsstatus.Aktiv)),
            samlet.Values.Count(s => s.Gjenstandslos));

        List<HjemmelsstatusRapportRad>? radliste = null;
        if (medRader)
        {
            string? Ref(Guid? id, string? eid) => id is { } k && data.Kilder.GetValueOrDefault(k) is { } x
                ? $"{UtgattAktorFunnregel.Dokumentreferanse(x.DokumentId) ?? x.Eli} {(eid is null ? "" : Hjemmelstekstlesing.Bestemmelsesetikett(eid))}".Trim()
                : null;
            radliste = beregnet.Select(b => new HjemmelsstatusRapportRad(b.RettskildeHjemmelId, data.Kilder.GetValueOrDefault(b.RettskildeId)?.DokumentId,
                b.HjemmelEid, b.Rolle, b.Status, b.Bevistrinn, b.UkjentGrunn, Ref(b.OpphevetAvRettskildeId, b.OpphevetAvEid), b.OpphevetAvSitat,
                b.VidereforingForm, Ref(b.VidereforingRettskildeId, b.VidereforingEid), b.Kunnskapsstatus)).ToList();
        }

        var opphevedeLover = beregnet
            .Where(b => b.Bevistrinn is not null && b.HjemmelEli is not null && b.HjemmelEli.Contains("/eli/lov/", StringComparison.Ordinal))
            .Select(b => b.HjemmelEli!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(e => data.Kortnavn.Les(e) is var (k, g) ? new HjemmelsstatusKortnavn(e, k, k is null ? g : "entydig") : null!).ToList();
        return new HjemmelsstatusRapport(skriv, lovtidend, beregnet.Count, skrevne, endret,
            Tell(beregnet.Select(b => (string?)b.Status)), Tell(beregnet.Select(b => b.Bevistrinn)), Tell(beregnet.Select(b => b.UkjentGrunn)),
            Tell(beregnet.Select(b => b.Rolle)), Tell(beregnet.Select(b => b.RolleRegel)), Tell(beregnet.Select(b => b.VidereforingForm)),
            Tell(beregnet.Select(b => b.VidereforingDekning)), Tell(beregnet.Select(b => (string?)b.Kunnskapsstatus)),
            refRader, lt, fTall, data.VaktAvviste, radliste,
            Tell(opphevedeLover.Select(e => e.Kortnavn is not null ? "entydig" : (string?)e.Grunn)), medRader ? opphevedeLover : null);
    }

    // ---- Lesing for rettskildesiden ---------------------------------------------------------------------------------------------------

    /// <summary>De lagrede radene for en forskrift og forskriftene den bygger på (rekursjonen), og den samlede statusen regnet ut av dem.
    /// Null når maskinregelen ikke er kjørt for forskriften.</summary>
    public async Task<(SamletHjemmelsstatus Samlet, IReadOnlyDictionary<Guid, HjemmelsstatusEntitet> Rader)?> LesAsync(Guid forskriftId, CancellationToken ct = default)
    {
        var perF = new Dictionary<Guid, List<HjemmelsstatusEntitet>>();
        var forskrift = new HashSet<Guid>();
        var ko = new Queue<Guid>([forskriftId]);
        var sett = new HashSet<Guid> { forskriftId };
        while (ko.Count > 0 && sett.Count < 200)
        {
            var id = ko.Dequeue();
            var l = await db.Hjemmelsstatuser.AsNoTracking().Where(x => x.RettskildeId == id).ToListAsync(ct);
            perF[id] = l;
            var mal = l.Where(x => x.MalRettskildeId is not null).Select(x => x.MalRettskildeId!.Value).Distinct().ToList();
            foreach (var m in await db.Rettskilder.AsNoTracking().Where(r => mal.Contains(r.Id) && r.Kildetype == "Forskrift").Select(r => r.Id).ToListAsync(ct))
            {
                forskrift.Add(m);
                if (sett.Add(m)) ko.Enqueue(m);
            }
        }
        if (perF[forskriftId].Count == 0) return null;
        IReadOnlyList<HjemmelPremiss>? Premisser(Guid id) => perF.TryGetValue(id, out var l)
            ? l.Select(b => new HjemmelPremiss(b.RettskildeHjemmelId, b.HjemmelEid, b.Rolle, b.Status, b.Kunnskapsstatus,
                b.MalRettskildeId is { } m && forskrift.Contains(m) ? m : null, b.VidereforingDekning)).ToList()
            : null;
        var samlet = HjemmelsstatusSamlet.Beregn(forskriftId, Premisser);
        return (samlet, perF.Values.SelectMany(l => l).ToDictionary(x => x.RettskildeHjemmelId));
    }
}
