using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, #429 PR 2, juristens vilkår godtatt av Johann 2026-10-31] R1–R4: tekstformer der et utgått navn står i en gjeldende rettskilde
/// uten at det er en gjeldende omtale av aktøren. Et treff TELLES, men foreslås ikke: et par (rettskilde, aktør) foreslås ikke når
/// ALLE forekomstene i paret er truffet av minst én regel. Én forekomst uten treff er nok til at paret foreslås.
/// <para>
/// [LÅST, #429 presisjonsmålingen 2026-10-31] Reglene er portert ordrett fra juristmålingens <c>jurist429/regler.py</c>, med samme
/// avgrensninger. Særlig R3 og R4 er smale med vilje: R3 slår bare til når termen er det som oppheves (genitiv, eller etter «til»),
/// ikke når termen er den som opphever («oppheves av X»); R4 krever markøren «tidligere» rett foran termen. Simulert på de 773 parene i
/// fase 1 tok reglene bort 69 par, og ingen ekte funn var blant dem etter lesing. Forbehold: reglene er laget på samme utvalg som de er
/// målt på (presisjon 0,70 → 0,875, 95 % KI 0,72–0,95). Ny måling på om lag 20 par, også aktører som ikke er departementer, før
/// maskinfunn går videre fra «oppdaget» (Johann 2026-10-31).
/// </para>
/// <para>
/// Teksten normaliseres som i målingen: linjeskift og tabulator blir mellomrom (<see cref="Normaliser"/>). Regex-semantikken er den
/// samme som Pythons: <c>\b</c>, <c>\s</c> og <c>\S</c> er Unicode-bevisste i begge.
/// </para>
/// </summary>
public static class UtgattAktorRegler
{
    /// <summary>R1: endringsinstruks i et endringsvedtak — «endres X til Y», «X endres til», «… endres … til X.».</summary>
    public const string R1 = "R1";
    /// <summary>R2: identifikasjon av et datert dokument — «fastsatt/godkjent/vedtatt av X (…) 1. januar 2000», «datert X»,
    /// «Xs forskrift/vedtak/resolusjon/rundskriv/delegering(sbeslutning)/delegeringsbrev (av) 1. januar 2000».</summary>
    public const string R2 = "R2";
    /// <summary>R3: opphevingsformel — forelderen slutter med «oppheves:», «oppheves:» står foran termen, eller passiv «oppheves» i
    /// setningen med termen i genitiv eller etter «til» (inntil seks ord mellom), og ikke som agent («av X»).</summary>
    public const string R3 = "R3";
    /// <summary>R4: eksplisitt historisk markør rett foran termen — «(tidligere X», «(tidligere) X», «tidligere X».</summary>
    public const string R4 = "R4";
    public static readonly string[] Alle = [R1, R2, R3, R4];

    private const string Maaned = "(januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)";
    private const string Dato = @"(den\s+)?\d{1,2}\.\s*" + Maaned + @"\s+\d{4}";

    public static string Normaliser(string? tekst) => Regex.Replace(tekst ?? "", "[\n\t]", " ");

    private static string[] Setninger(string tekst) => Regex.Split(tekst, @"(?<=[.;])\s+");

    /// <summary>Reglene som slår til for <paramref name="term"/> i <paramref name="tekst"/> (allerede normalisert), med forelderens tekst
    /// for R3. Tom liste = ingen regel; forekomsten teller som gjeldende omtale.</summary>
    public static IReadOnlyList<string> Treff(string term, string tekst, string? forelder)
    {
        var t = Regex.Escape(term);
        var h = new List<string>();

        if (Regex.IsMatch(tekst, @"endres\s+" + t + @"\s+til\b") || Regex.IsMatch(tekst, t + @"\s+endres\s+til\b")
            || Setninger(tekst).Any(x => Regex.IsMatch(x, @"\bendres\b.*\btil\s+" + t + @"\s*(\.|$|i\s+både)")))
        {
            h.Add(R1);
        }

        if (Regex.IsMatch(tekst, @"(fastsatt|godkjent|vedtatt)\s+av\s+" + t + @"(\s+\([^)]*\))?\s+" + Dato)
            || Regex.IsMatch(tekst, @"datert\s+" + t)
            || Regex.IsMatch(tekst, t + @"s\s+(forskrift(er)?|vedtak|resolusjon|rundskriv|delegering(sbeslutning)?|delegeringsbrev)\s+(av\s+)?" + Dato))
        {
            h.Add(R2);
        }

        // Pythons tekst.split(term)[0]: teksten foran første forekomst (hele teksten når termen ikke står der).
        var i = tekst.IndexOf(term, StringComparison.Ordinal);
        var foran = i < 0 ? tekst : tekst[..i];
        if (Regex.IsMatch((forelder ?? "").Trim(), @"oppheves\s*:\s*$") || Regex.IsMatch(foran, @"oppheves\s*:"))
        {
            h.Add(R3);
        }
        else
        {
            foreach (var s in Setninger(tekst))
            {
                if (s.Contains(term, StringComparison.Ordinal) && Regex.IsMatch(s, @"\boppheves\b") && !Regex.IsMatch(s, @"\bav\s+" + t)
                    && (Regex.IsMatch(s, t + @"s\b") || Regex.IsMatch(s, @"\btil\s+(\S+\s+){0,6}?" + t) || Regex.IsMatch(s, @"\btil\s+" + t)))
                {
                    h.Add(R3);
                    break;
                }
            }
        }

        if (Regex.IsMatch(tekst, @"\(?\btidligere\)?\s+" + t)) h.Add(R4);
        return h;
    }
}

/// <summary>Én forekomst: noden og reglene som slo til (tom = gjeldende omtale).</summary>
public sealed record UtgattAktorForekomst(string Eid, string Term, string QuoteExact, IReadOnlyList<string> Regler);

/// <summary>Ett par (rettskilde, aktør) i rapporten.</summary>
public sealed record UtgattAktorRad(
    string? DokumentId, string Rettskilde, Guid VirksomhetId, string Aktor, string Gruppe, bool Foreslatt, string EksternNokkel,
    string? FinnesMedStatus, bool IEndringsvedtak, string? Malvedtak, IReadOnlyList<UtgattAktorForekomst> Forekomster);

/// <summary>
/// Rapporten fra én kjøring. Tallene er par (rettskilde, aktør) når ikke annet står. <c>Gruppe</c>: A navneendring (aktøren lever),
/// B nedlagt med hovedetterfølger dekket, C nedlagt med etterfølger men hovedetterfølger ukjent, D nedlagt uten etterfølger.
/// </summary>
public sealed record UtgattAktorRapport(
    bool Skrevet,
    int NavneformerSokt,
    int TermerSokt,
    IReadOnlyList<string> TvetydigeNavn,
    int Nodetreff,
    int NodetreffOrganiseringsvedtak,
    int NodetreffRentEndringsvedtak,
    int NodetreffTvetydigeNavn,
    int NodetreffUtenEli,
    int ParTvetydigeNavn,
    int ParRettskildeTerm,
    int Par,
    int IkkeForeslatt,
    IReadOnlyDictionary<string, int> IkkeForeslattPerRegel,
    IReadOnlyDictionary<string, int> NoderPerRegel,
    int Foreslatt,
    IReadOnlyDictionary<string, int> ForeslattPerGruppe,
    IReadOnlyDictionary<string, int> IkkeForeslattPerGruppe,
    int ForeslattIEndringsvedtak,
    int ForeslattMedMalvedtak,
    int Nye,
    int Finnes,
    int FinnesForklartBort,
    IReadOnlyList<UtgattAktorRad>? Rader);

/// <summary>
/// [Ny, #429 PR 2, Johanns avgjørelser 2026-10-30 og 2026-10-31] <b>Maskinregelen for funn om utgåtte aktører</b>
/// (<c>monster:utgatt-aktor</c>). Leser de gjeldende primære rettskildene og foreslår ett funn per (rettskilde, aktør) der teksten
/// fortsatt bruker et navn på en nedlagt aktør eller et utgått navn. Funnene lagres som <c>oppdaget</c> og går gjennom juristen og
/// premissporten som alle andre (<see cref="FunnTjeneste"/>).
/// <list type="number">
/// <item><b>Navnene:</b> navneformene (begrep, kategori virksomhet, gjeldende, ikke avvist) som hører til en aktør med
/// <c>livsstatus = 'nedlagt'</c> eller har grunnen <c>utgatt</c>. Minst åtte tegn, som i målingen.</item>
/// <item><b>Tekstforekomster:</b> hele ordet, med valgfri genitiv-s (Postgres <c>\m…s?\M</c> i målingen), skille mellom store og små
/// bokstaver. Bare noder som er gjeldende, ikke opphevet og ikke merknad (<c>er_merknad</c>, #419), og bare ledd, punkt og avslutning
/// (bladtekst).</item>
/// <item><b>Utelatt og telt:</b> organiseringsvedtak (disposisjonstype i <see cref="Disposisjonstyper.Livslopstyper"/>: de beskriver
/// endringene og får egen sak, #438), rene endringsvedtak (<c>rent_endringsvedtak = true</c>), og <b>tvetydige navn</b> — en term som er
/// navneform for mer enn én aktøridentitet (for eksempel «Kulturdepartementet»). De foreslås ikke (Johann 2026-10-30).</item>
/// <item><b>R1–R4</b> (<see cref="UtgattAktorRegler"/>) teller, men foreslår ikke.</item>
/// <item><b><c>rent_endringsvedtak</c> NULL er ukjent</b>, ikke «ikke rent» (Johann 2026-10-31 (2)). Et funn i en rettskilde som endrer
/// andre vedtak, foreslås, men påstanden sier at det er et endringsvedtak, og målvedtaket kobles på (<see cref="Funn.Malvedtak"/>) når
/// rettskilden endrer nøyaktig ett dokument. Endrer den flere, står det at målvedtaket ikke er entydig — ingen gjetting.</item>
/// <item><b>Forankring:</b> én <see cref="Funn.GjelderBestemmelse"/> per node, med ELI, eId og <see cref="FunnKoblingEntitet.QuoteExact"/>
/// — slik en tagg forankres. I tillegg berørt aktør, navneformene og etterfølgerkantene (<see cref="Funn.Etterfolger"/>).</item>
/// <item><b>Idempotent:</b> <see cref="FunnEntitet.EksternNokkel"/> er <c>utgatt-aktor:&lt;ELI uten prefiks&gt;:&lt;aktør-id&gt;</c>.
/// Finnes nøkkelen, røres funnet ikke — et funn som er forklart bort, gjenåpnes aldri, og en ny kjøring gir 0 nye.</item>
/// </list>
/// Tørrkjøring er standard (<c>skriv = false</c>): da skrives ingenting, og rapporten viser hva som ville blitt foreslått.
/// </summary>
public sealed class UtgattAktorFunnregel(RegelIdeDbContext db, FunnTjeneste funnTjeneste)
{
    public const string OppdagelsesKilde = "monster:utgatt-aktor";
    public const string NokkelPrefiks = "utgatt-aktor:";
    public const int MinsteTermlengde = 8;
    private static readonly string[] Bladnoder = ["ledd", "punkt", "avslutning"];

    public const string GruppeA = "A";
    public const string GruppeB = "B";
    public const string GruppeC = "C";
    public const string GruppeD = "D";
    public static readonly string[] Grupper = [GruppeA, GruppeB, GruppeC, GruppeD];

    private sealed record Nodetreff(Guid RettskildeId, Guid NodeId, string Eid, string Term, IReadOnlyList<Guid> BegrepIder, Guid VirksomhetId, string Quote,
        IReadOnlyList<string> Regler);

    /// <param name="rettskildeIder">Avgrensning til noen rettskilder (tester og feilsøking). Null = hele korpuset.</param>
    public async Task<UtgattAktorRapport> KjorAsync(
        bool skriv, string bruker, IReadOnlyCollection<Guid>? rettskildeIder = null, bool medRader = false, CancellationToken ct = default)
    {
        // ---- 1. Navnene ----
        var alleNavneformer = await db.Begreper.AsNoTracking()
            .Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId != null && b.Entitetsstatus == "gjeldende"
                        && (b.Godkjenning == null || b.Godkjenning != Strukturkanter.Avvist))
            .Select(b => new { b.Id, b.Term, b.Navneformgrunn, Vid = b.VirksomhetReferanseId!.Value })
            .ToListAsync(ct);
        var tvetydige = alleNavneformer.GroupBy(b => b.Term, StringComparer.Ordinal)
            .Where(g => g.Select(b => b.Vid).Distinct().Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var vidAlle = alleNavneformer.Select(b => b.Vid).Distinct().ToList();
        var virksomheter = await db.Virksomheter.AsNoTracking().Where(v => vidAlle.Contains(v.Id))
            .Select(v => new { v.Id, v.Navn, v.Livsstatus, v.GyldigTil }).ToDictionaryAsync(v => v.Id, ct);
        // Aktøren omtales med navneformen lovteksten bruker i dag (CLAUDE.md §0: registernavnet er avledet, og kan stå i versaler —
        // «JUSTIS- OG BEREDSKAPSDEPARTEMENTET»). Bare når det finnes nøyaktig én gjeldende navneform; ellers registernavnet.
        var visningsnavn = alleNavneformer.Where(b => b.Navneformgrunn == "gjeldende").GroupBy(b => b.Vid)
            .Where(g => g.Select(b => b.Term).Distinct(StringComparer.Ordinal).Count() == 1).ToDictionary(g => g.Key, g => g.First().Term);
        string Navn(Guid vid) => visningsnavn.GetValueOrDefault(vid) ?? virksomheter[vid].Navn;
        var sokes = alleNavneformer
            .Where(b => virksomheter.TryGetValue(b.Vid, out var v) && (v.Livsstatus == "nedlagt" || b.Navneformgrunn == "utgatt")
                        && b.Term.Length >= MinsteTermlengde)
            .ToList();

        // ---- 2. Rettskildene ----
        var rkQuery = db.Rettskilder.AsNoTracking().Where(r => r.Entitetsstatus == "gjeldende" && r.Importrolle == "primaer");
        if (rettskildeIder is not null) rkQuery = rkQuery.Where(r => rettskildeIder.Contains(r.Id));

        // ---- 3. Tekstforekomstene, én spørring per navn (50 i den delte basen 2026-10-31) ----
        // Én spørring per TERM, ikke per navneform: samme term kan være navneform flere ganger (for samme aktør, eller for to aktører når
        // den er tvetydig). Da telles forekomsten én gang, og funnet peker på alle navneformene med den termen.
        var treff = new List<(Guid Rk, Guid NodeId, string Eid, Guid? Forelder, string Tekst, string Term, IReadOnlyList<Guid> BegrepIder, Guid Vid, string Quote)>();
        foreach (var nf in sokes.GroupBy(b => b.Term, StringComparer.Ordinal))
        {
            var term = nf.Key;
            IReadOnlyList<Guid> begrepIder = nf.Select(b => b.Id).Distinct().ToList();
            var vid = nf.First().Vid; // entydig når termen ikke er tvetydig; tvetydige termer foreslås ikke
            var noder = await db.RettskildeNoder.AsNoTracking()
                .Where(n => n.Entitetsstatus == "gjeldende" && !n.Opphevet && !n.ErMerknad && Bladnoder.Contains(n.NodeType)
                            && n.Tekst != null && n.Tekst.Contains(term) && rkQuery.Any(r => r.Id == n.RettskildeId))
                .Select(n => new { n.Id, n.RettskildeId, n.Eid, n.ParentNodeId, n.Tekst })
                .ToListAsync(ct);
            var rx = Ordregex(term);
            foreach (var n in noder)
            {
                var m = rx.Match(n.Tekst!);
                if (m.Success) treff.Add((n.RettskildeId, n.Id, n.Eid, n.ParentNodeId, n.Tekst!, term, begrepIder, vid, m.Value));
            }
        }

        var rkIder = treff.Select(x => x.Rk).Distinct().ToList();
        var rettskilder = await db.Rettskilder.AsNoTracking().Where(r => rkIder.Contains(r.Id))
            .Select(r => new { r.Id, r.Eli, r.DokumentId, r.Tittel, r.Kortnavn, r.RentEndringsvedtak })
            .ToDictionaryAsync(r => r.Id, ct);
        var livslopstyper = Disposisjonstyper.Livslopstyper.ToList();
        var organisering = (await db.RettskildeDisposisjonstyper.AsNoTracking()
            .Where(d => rkIder.Contains(d.RettskildeId) && livslopstyper.Contains(d.Disposisjonstype))
            .Select(d => d.RettskildeId).Distinct().ToListAsync(ct)).ToHashSet();
        var endrer = (await db.RettskildeEndringer.AsNoTracking().Where(e => rkIder.Contains(e.RettskildeId))
                .Join(db.Rettskilder.IgnoreQueryFilters(), e => e.EndringRettskildeId, r => r.Id,
                    (e, r) => new { e.RettskildeId, e.EndringEid, r.Tittel })
                .ToListAsync(ct))
            .GroupBy(e => e.RettskildeId)
            .ToDictionary(g => g.Key, g => g.GroupBy(e => e.EndringEid, StringComparer.Ordinal).Select(x => x.First()).ToList());
        var forelderIder = treff.Where(x => x.Forelder != null).Select(x => x.Forelder!.Value).Distinct().ToList();
        var forelderTekst = new Dictionary<Guid, string?>();
        foreach (var bit in forelderIder.Chunk(2000))
        {
            foreach (var p in await db.RettskildeNoder.AsNoTracking().Where(n => bit.Contains(n.Id)).Select(n => new { n.Id, n.Tekst }).ToListAsync(ct))
            {
                forelderTekst[p.Id] = p.Tekst;
            }
        }

        // ---- 4. Klassifisering per forekomst ----
        int nOrg = 0, nRent = 0, nTvetydig = 0, nUtenEli = 0;
        var tvetydigePar = new HashSet<(Guid, string)>();
        var kandidater = new List<Nodetreff>();
        foreach (var x in treff)
        {
            var rk = rettskilder[x.Rk];
            if (organisering.Contains(x.Rk)) { nOrg++; continue; }
            if (rk.RentEndringsvedtak == true) { nRent++; continue; }
            if (tvetydige.Contains(x.Term)) { nTvetydig++; tvetydigePar.Add((x.Rk, x.Term)); continue; }
            if (rk.Eli is null) { nUtenEli++; continue; }
            var forelder = x.Forelder is { } fid ? forelderTekst.GetValueOrDefault(fid) : null;
            var regler = UtgattAktorRegler.Treff(x.Term, UtgattAktorRegler.Normaliser(x.Tekst), UtgattAktorRegler.Normaliser(forelder));
            kandidater.Add(new Nodetreff(x.Rk, x.NodeId, x.Eid, x.Term, x.BegrepIder, x.Vid, x.Quote, regler));
        }

        // ---- 5. Etterfølgerne og gruppene ----
        var vider = kandidater.Select(k => k.VirksomhetId).Distinct().ToList();
        var kanter = (await db.Strukturkanter.AsNoTracking()
                .Where(k => k.Kategori == Strukturkanter.Relasjon && k.Typekode == "etterfolger" && k.TilVirksomhetId != null
                            && vider.Contains(k.TilVirksomhetId.Value))
                .Select(k => new { k.Id, Til = k.TilVirksomhetId!.Value, k.FraVirksomhetId, k.Status })
                .ToListAsync(ct))
            .GroupBy(k => k.Til).ToDictionary(g => g.Key, g => g.ToList());
        var fraIder = kanter.Values.SelectMany(l => l).Where(k => k.FraVirksomhetId != null).Select(k => k.FraVirksomhetId!.Value).Distinct().ToList();
        var fraNavn = await db.Virksomheter.AsNoTracking().Where(v => fraIder.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Navn, ct);
        foreach (var id in fraIder.Where(visningsnavn.ContainsKey)) fraNavn[id] = visningsnavn[id];
        var nedlagte = vider.Where(v => virksomheter[v].Livsstatus == "nedlagt").ToList();
        var hull = await EtterfolgerHull.HovedetterfolgerUkjentAsync(db, nedlagte, ct);
        string Gruppe(Guid vid) =>
            virksomheter[vid].Livsstatus != "nedlagt" ? GruppeA
            : !kanter.ContainsKey(vid) ? GruppeD
            : hull.Contains(vid) ? GruppeC : GruppeB;

        // ---- 6. Par (rettskilde, aktør) ----
        var nokler = await db.Funn.AsNoTracking().Where(f => f.EksternNokkel != null && f.EksternNokkel.StartsWith(NokkelPrefiks))
            .Select(f => new { f.EksternNokkel, f.Status }).ToDictionaryAsync(f => f.EksternNokkel!, f => f.Status, ct);
        var noderPerRegel = UtgattAktorRegler.Alle.ToDictionary(r => r, r => kandidater.Count(k => k.Regler.Contains(r)));
        var ikkeForeslattPerRegel = UtgattAktorRegler.Alle.ToDictionary(r => r, _ => 0);
        var foreslattPerGruppe = Grupper.ToDictionary(g => g, _ => 0);
        var ikkeForeslattPerGruppe = Grupper.ToDictionary(g => g, _ => 0);
        int foreslatt = 0, ikkeForeslatt = 0, iEndring = 0, medMal = 0, nye = 0, finnes = 0, finnesForklartBort = 0;
        var rader = new List<UtgattAktorRad>();

        var par = kandidater.GroupBy(k => (k.RettskildeId, k.VirksomhetId))
            .OrderBy(g => rettskilder[g.Key.RettskildeId].DokumentId, StringComparer.Ordinal).ThenBy(g => Navn(g.Key.VirksomhetId), StringComparer.Ordinal);
        foreach (var g in par)
        {
            var rk = rettskilder[g.Key.RettskildeId];
            var v = virksomheter[g.Key.VirksomhetId];
            var gruppe = Gruppe(g.Key.VirksomhetId);
            var forekomster = g.OrderBy(k => k.Eid, StringComparer.Ordinal).ThenBy(k => k.Term, StringComparer.Ordinal).ToList();
            var undertrykt = forekomster.All(k => k.Regler.Count > 0);
            var nokkel = NokkelPrefiks + EliKort(rk.Eli!) + ":" + g.Key.VirksomhetId;
            var finnesStatus = nokler.GetValueOrDefault(nokkel);
            var endringer = endrer.GetValueOrDefault(g.Key.RettskildeId);
            var mal = endringer is { Count: 1 } ? endringer[0] : null;

            if (undertrykt)
            {
                ikkeForeslatt++;
                ikkeForeslattPerGruppe[gruppe]++;
                foreach (var r in forekomster.SelectMany(k => k.Regler).Distinct()) ikkeForeslattPerRegel[r]++;
            }
            else
            {
                foreslatt++;
                foreslattPerGruppe[gruppe]++;
                if (endringer is not null) iEndring++;
                if (mal is not null) medMal++;
                if (finnesStatus is not null)
                {
                    finnes++;
                    if (finnesStatus == Funn.ForklartBort) finnesForklartBort++;
                }
                else
                {
                    nye++;
                    if (skriv)
                    {
                        var etterfolgere = kanter.GetValueOrDefault(g.Key.VirksomhetId) ?? [];
                        var nytt = ByggFunn(nokkel, rk.Eli!, rk.DokumentId, rk.Kortnavn ?? rk.Tittel, rk.Tittel, Navn(g.Key.VirksomhetId), v.GyldigTil, g.Key.VirksomhetId,
                            gruppe, forekomster, etterfolgere.Select(k => (k.Id, k.FraVirksomhetId is { } f ? fraNavn.GetValueOrDefault(f) : null)).ToList(),
                            endringer?.Count ?? 0, mal is null ? null : (mal.EndringEid, mal.Tittel));
                        await funnTjeneste.OpprettAsync(nytt, bruker, ct);
                    }
                }
            }
            if (medRader)
            {
                rader.Add(new UtgattAktorRad(rk.DokumentId, rk.Kortnavn ?? rk.Tittel, g.Key.VirksomhetId, Navn(g.Key.VirksomhetId), gruppe, !undertrykt, nokkel,
                    finnesStatus, endringer is not null, mal?.EndringEid,
                    forekomster.Select(k => new UtgattAktorForekomst(k.Eid, k.Term, k.Quote, k.Regler)).ToList()));
            }
        }

        return new UtgattAktorRapport(
            skriv, sokes.Count, sokes.Select(b => b.Term).Distinct(StringComparer.Ordinal).Count(), tvetydige.Where(t => sokes.Any(s => s.Term == t)).Order(StringComparer.Ordinal).ToList(),
            treff.Count, nOrg, nRent, nTvetydig, nUtenEli, tvetydigePar.Count,
            kandidater.Select(k => (k.RettskildeId, k.Term)).Distinct().Count(),
            foreslatt + ikkeForeslatt, ikkeForeslatt, ikkeForeslattPerRegel, noderPerRegel, foreslatt, foreslattPerGruppe, ikkeForeslattPerGruppe,
            iEndring, medMal, nye, finnes, finnesForklartBort, medRader ? rader : null);
    }

    /// <summary>Hele ordet med valgfri genitiv-s, som Postgres <c>\m term s?\M</c> i målingen: ingen bokstav, siffer eller understrek rett
    /// foran eller etter. Skiller mellom store og små bokstaver.</summary>
    public static Regex Ordregex(string term)
    {
        var start = term.Length > 0 && (char.IsLetterOrDigit(term[0]) || term[0] == '_') ? @"(?<![\w])" : "";
        var slutt = term.Length > 0 && (char.IsLetterOrDigit(term[^1]) || term[^1] == '_') ? @"(?![\w])" : "";
        return new Regex(start + Regex.Escape(term) + "s?" + slutt, RegexOptions.CultureInvariant);
    }

    /// <summary>ELI-en uten <c>https://lovdata.no/eli/</c>: «forskrift/2009/06/22/842/nor».</summary>
    public static string EliKort(string eli) => eli.StartsWith("https://lovdata.no/eli/", StringComparison.Ordinal) ? eli["https://lovdata.no/eli/".Length..] : eli;

    /// <summary>Lovdatas korte referanse av dokument-id-en: «SF/forskrift/2009-06-22-842» → «FOR-2009-06-22-842», «NL/lov/…» → «LOV-…».
    /// Ukjent form → dokument-id-en som den er.</summary>
    public static string? Dokumentreferanse(string? dokumentId)
    {
        if (dokumentId is null) return null;
        var m = Regex.Match(dokumentId, @"^[A-Z]+/(forskrift|lov)/(\d{4}-\d{2}-\d{2}-\d+)$");
        return m.Success ? (m.Groups[1].Value == "lov" ? "LOV-" : "FOR-") + m.Groups[2].Value : dokumentId;
    }

    /// <summary>«§ 4 første ledd» der eId-en har en paragraf, ellers eId-en etter «/nor/» («kap-I/ledd-1»).</summary>
    public static string Bestemmelsesetikett(string eid)
    {
        if (AktorkoTjeneste.Bestemmelse(eid) is { } b) return b;
        var i = eid.IndexOf("/nor/", StringComparison.Ordinal);
        return i >= 0 ? eid[(i + 5)..] : eid;
    }

    private static NyttFunn ByggFunn(
        string nokkel, string eli, string? dokumentId, string kortnavn, string tittel, string aktor, DateOnly? gyldigTil, Guid vid, string gruppe,
        IReadOnlyList<Nodetreff> forekomster, IReadOnlyList<(Guid KantId, string? Fra)> etterfolgere, int antallEndret,
        (string Eli, string Tittel)? mal)
    {
        var referanse = Dokumentreferanse(dokumentId) ?? EliKort(eli);
        var termer = forekomster.Select(k => k.Term).Distinct(StringComparer.Ordinal).ToList();
        var termTekst = string.Join(" og ", termer.Select(t => $"«{t}»"));
        var bestemmelser = forekomster.Select(k => k.Eid).Distinct(StringComparer.Ordinal).ToList();
        var antall = bestemmelser.Count == 1 ? "én bestemmelse" : $"{bestemmelser.Count} bestemmelser";

        var linjer = new List<string>();
        if (gruppe == GruppeA)
        {
            linjer.Add($"«{tittel}» ({referanse}) er gjeldende og bruker {termTekst} i {antall}. Navnet er utgått. Aktøren finnes fortsatt, "
                + $"nå som {aktor}.");
            linjer.Add($"- Om oppgaven i bestemmelsen fortsatt ligger hos {aktor}, er ikke vurdert. Den kan være flyttet til en annen aktør.");
            // Ekom-tilfellet fra juristmålingen: en del av oppgavene er overført (DFD etterfølger KDD). Kantene kobles på, og
            // premissporten krever dem vurdert.
            if (etterfolgere.Count > 0)
            {
                linjer.Add($"- Etterfølger for en del av oppgavene i modellen: {string.Join(", ", etterfolgere.Select(e => e.Fra ?? "ukjent aktør").Distinct())}.");
            }
        }
        else
        {
            linjer.Add($"«{tittel}» ({referanse}) er gjeldende og nevner {termTekst} i {antall}. {aktor} er nedlagt"
                + (gyldigTil is { } d ? $" (siste dag {d:yyyy-MM-dd})." : "."));
            var navn = etterfolgere.Select(e => e.Fra ?? "ukjent aktør").Distinct().ToList();
            linjer.Add(gruppe switch
            {
                GruppeB => $"- Etterfølger i modellen: {string.Join(", ", navn)}. Ikke vurdert for denne bestemmelsen.",
                GruppeC => $"- Hovedetterfølger: ukjent. Etterfølger for en del av oppgavene i modellen: {string.Join(", ", navn)}.",
                _ => "- Etterfølger: ukjent. Modellen har ingen etterfølger for aktøren.",
            });
        }
        if (antallEndret > 0)
        {
            linjer.Add(mal is { } m
                ? $"- Rettskilden endrer «{m.Tittel}». Om den bare er et endringsvedtak, er ikke avklart."
                : $"- Rettskilden endrer {antallEndret} vedtak, så målvedtaket er ikke entydig. Om den bare er et endringsvedtak, er ikke avklart.");
        }

        var koblinger = new List<NyKobling>();
        foreach (var k in forekomster.GroupBy(k => k.Eid, StringComparer.Ordinal))
        {
            var forste = k.First();
            koblinger.Add(new NyKobling(Funn.GjelderBestemmelse, $"{referanse} {Bestemmelsesetikett(k.Key)}", Merknad: null,
                RettskildeEli: eli, Eid: k.Key, QuoteExact: forste.Quote));
        }
        koblinger.Add(new NyKobling(Funn.BerortAktor, aktor, VirksomhetId: vid));
        foreach (var nf in forekomster.SelectMany(k => k.BegrepIder.Select(b => (Begrep: b, k.Term))).Distinct()
                     .OrderBy(x => x.Term, StringComparer.Ordinal).ThenBy(x => x.Begrep))
        {
            koblinger.Add(new NyKobling(Funn.Navneform, nf.Term, BegrepId: nf.Begrep));
        }
        foreach (var e in etterfolgere) koblinger.Add(new NyKobling(Funn.Etterfolger, e.Fra ?? "ukjent aktør", StrukturkantId: e.KantId));
        if (mal is { } mv) koblinger.Add(new NyKobling(Funn.Malvedtak, mv.Tittel, Merknad: "endres av rettskilden", RettskildeEli: mv.Eli));

        var delvis = forekomster.Where(k => k.Regler.Count > 0).Select(k => $"{Bestemmelsesetikett(k.Eid)} ({string.Join(", ", k.Regler)})").ToList();
        var intern = $"Maskinfunn ({OppdagelsesKilde}, #429 PR 2), gruppe {gruppe}. "
            + (delvis.Count > 0 ? $"Forekomster truffet av R1–R4 (telt, men minst én forekomst er uten treff): {string.Join("; ", delvis)}. " : "")
            + "Presisjonen er målt på 40 par 2026-10-31: 0,70, og 0,875 med R1–R4 (reglene er laget på samme utvalg). Ny måling før "
            + "maskinfunn går videre fra «oppdaget».";

        return new NyttFunn(
            Funn.UtgattAktor, gruppe == GruppeA ? "utgatt_navn" : "nedlagt_aktor",
            gruppe == GruppeA ? $"Utgått navn i {kortnavn}: {termer[0]}" : $"Nedlagt aktør i {kortnavn}: {aktor}",
            string.Join("\n", linjer), OppdagelsesKilde, InternMerknad: intern, FunnetI: "#429", EksternNokkel: nokkel, Koblinger: koblinger);
    }
}
