using Microsoft.EntityFrameworkCore;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08, docs/33 §4.1/§5.6] Bygger områderegisteret fra
/// øyeblikksbildene i <c>Seed/</c> (<see cref="OmraderegisterKilder"/>) og inndelingsforskriften i korpus:
/// <list type="number">
/// <item><b>Fylker og kommuner</b> fra Kartverket som områder med kode, og <c>O bestar_av</c> fylke → kommune.</item>
/// <item><b>Kommunen som rettssubjekt → territoriet</b>: <c>A har_ansvarsomrade</c> fra <see cref="Virksomhet"/> til
/// kommuneområdet (docs/33 §5.6 «A har_ansvarsomrade kommune→eget territorium»). Koblingen går via
/// organisasjonsnummeret (stabil nøkkel) og Enhetsregisterets kommunenummer for organisasjonsform KOMM, og
/// <see cref="Virksomhet.Kommunenummer"/> fylles der den er NULL.</item>
/// <item><b>Domstolene</b> fra forskrift om inndelingen av rettskretser og lagdømmer (<see cref="DomstolinndelingTolker"/>),
/// med hjemmel = den ekte eId-en per kant.</item>
/// <item><b>Statsforvalterne</b> → fylker og <b>RHF-ene</b> → helseregion → fylker, fra de kuraterte filene.</item>
/// </list>
/// <para>
/// <b>Vakter på stabil nøkkel</b> (CLAUDE.md §4): et område finnes hvis det finnes en gjeldende rad med samme
/// (<see cref="BegrepEntitet.Omradetype"/>, <see cref="BegrepEntitet.Omradekode"/>) — eller (type, term) for de uten
/// kode. En virksomhet finnes på organisasjonsnummer. En kant finnes hvis samme (kategori, type, fra, til, hjemmel)
/// finnes. ALDRI på navn: Herøy og Våler er to kommuner hver. Navnet på en eksisterende rad overskrives aldri — et
/// avvik mellom fila og basen RAPPORTERES (§3).
/// </para>
/// <para>
/// <b>Skrivevei:</b> alle kanter går gjennom <see cref="StrukturkantTjeneste.OpprettAsync"/> (den eneste skriveveien,
/// #311). For at oppstarten ikke skal betale ~10 spørringer per kant hver gang, hentes de eksisterende kantene først,
/// og bare manglende kanter sendes til tjenesten.
/// </para>
/// <para>
/// Gated bak <c>RegelIde:Omraderegister:SeedVedOppstart</c> (CLAUDE.md §4: skriving ved oppstart). Testfixturen
/// setter den <c>false</c>; testene som trenger registeret kaller <see cref="SeedAsync"/> selv.
/// </para>
/// </summary>
public static class OmraderegisterSeed
{
    public const string OpprettetAv = "seed:omraderegister";
    public const string Konfignokkel = "RegelIde:Omraderegister:SeedVedOppstart";

    /// <summary>[Ny, issue #345] Typekoden for «Til lagsognet X sogner A tingrett» (A, tingrett → lagsogn).</summary>
    public const string SognerTil = "sogner_til";

    /// <summary>[Ny, issue #345] Oppdagelseskilden for tingrett → lagsogn (mønsteret inndeling-sogner, #307).</summary>
    public const string SognerTilOppdagelseskilde = "monster:inndeling-sogner-til";

    public sealed record Resultat(
        int NyeOmrader,
        int NyeKanter,
        int NyeVirksomheter,
        int KommunenummerFylt,
        IReadOnlyList<string> Hoppet,
        IReadOnlyList<string> Navneavvik,
        IReadOnlyList<string> KommunerUtenRettssubjekt,
        IReadOnlyList<string> RettssubjekterUtenKommune,
        IReadOnlyList<string> UlosteDomstoler,
        IReadOnlyDictionary<string, IReadOnlyList<string>> DelteKommuner,
        IReadOnlyList<string> Konverteringsavvik,
        int GjortTilForslag = 0,
        int SlettedeKanter = 0,
        int SlettedeUtflatedeKanter = 0);

    private sealed class Tilstand(RegelIdeDbContext db, StrukturkantTjeneste kanter, VirksomhetsbegrepTjeneste navneformer)
    {
        public RegelIdeDbContext Db { get; } = db;
        public StrukturkantTjeneste Kanter { get; } = kanter;
        public VirksomhetsbegrepTjeneste Navneformer { get; } = navneformer;
        public Dictionary<(string Type, string Kode), BegrepEntitet> MedKode { get; } = new();
        public Dictionary<(string Type, string Term), BegrepEntitet> UtenKode { get; } = new();
        public HashSet<(string Kat, string Type, Guid? FraV, Guid? FraB, Guid? TilV, Guid? TilB, Guid? Hjemmel)> Kantnokler { get; } = new();
        public int NyeOmrader;
        public int NyeKanter;
        public int NyeVirksomheter;
        public int KommunenummerFylt;
        public int GjortTilForslag;
        public int SlettedeKanter;
        public int SlettedeUtflatedeKanter;
        public List<string> Hoppet { get; } = [];
        public List<string> Navneavvik { get; } = [];
    }

    public static async Task<Resultat> SeedAsync(
        RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste, VirksomhetsbegrepTjeneste navneformtjeneste,
        string? seedmappe = null, CancellationToken ct = default)
    {
        var kilder = OmraderegisterKilder.Les(seedmappe ?? OmraderegisterKilder.StandardMappe);
        var t = new Tilstand(db, kanttjeneste, navneformtjeneste);
        if (kilder is null)
        {
            t.Hoppet.Add($"Øyeblikksbildene mangler i {seedmappe ?? OmraderegisterKilder.StandardMappe} — ingenting seedet.");
            return Tomt(t);
        }

        foreach (var b in await db.Begreper.Where(b => b.Begrepskategori == Nodetyper.Omrade && b.Omradetype != null
                                                       && b.Entitetsstatus == "gjeldende" && b.GyldigTil == null).ToListAsync(ct))
        {
            if (b.Omradekode is not null) t.MedKode[(b.Omradetype!, b.Omradekode)] = b;
            else t.UtenKode[(b.Omradetype!, b.Term)] = b;
        }
        foreach (var k in await db.Strukturkanter
                     .Select(k => new { k.Kategori, k.Typekode, k.FraVirksomhetId, k.FraBegrepId, k.TilVirksomhetId, k.TilBegrepId, k.HjemmelRettskildeId })
                     .ToListAsync(ct))
        {
            t.Kantnokler.Add((k.Kategori, k.Typekode, k.FraVirksomhetId, k.FraBegrepId, k.TilVirksomhetId, k.TilBegrepId, k.HjemmelRettskildeId));
        }

        // ---- 1. Fylker og kommuner (Kartverket) ----
        var kv = kilder.Kartverket;
        var kartverketKilde = $"Kartverket kommuneinfo, fylker med kommuner (hentet {kv.Hentet})";
        var fylkePerNavn = new Dictionary<string, BegrepEntitet>(StringComparer.Ordinal);
        var fylkePerNummer = new Dictionary<string, BegrepEntitet>(StringComparer.Ordinal);
        var kommunePerNummer = new Dictionary<string, BegrepEntitet>(StringComparer.Ordinal);
        foreach (var f in kv.Fylker)
        {
            var fylke = await OmradeAsync(t, Omradetyper.Fylke, f.Fylkesnummer, f.Fylkesnavn,
                $"Fylke {f.Fylkesnummer} i Kartverkets inndeling ({kartverketKilde}).", ct);
            fylkePerNavn[f.Fylkesnavn] = fylke;
            fylkePerNummer[f.Fylkesnummer] = fylke;
            foreach (var k in f.Kommuner)
            {
                var kommune = await OmradeAsync(t, Omradetyper.Kommune, k.Kommunenummer, k.KommunenavnNorsk,
                    $"Kommune {k.Kommunenummer} i {f.Fylkesnavn}, Kartverkets inndeling ({kartverketKilde}). Territoriet — "
                    + "kommunen som rettssubjekt er en virksomhet med ansvarsområde hit.", ct);
                kommunePerNummer[k.Kommunenummer] = kommune;
                await KantAsync(t, new NyStrukturkant(Strukturkanter.Omradesammensetning, "bestar_av",
                    Kantnode.Begrep(fylke.Id), Kantnode.Begrep(kommune.Id),
                    KildeUtenforKorpusTekst: kartverketKilde, KildeUtenforKorpusLenke: kv.Url,
                    KildeUtenforKorpusType: Strukturkanter.Register, KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer), ct);
            }
        }
        var registrerte = t.MedKode.Where(x => x.Key.Type is Omradetyper.Fylke or Omradetyper.Kommune)
            .Where(x => x.Key.Type == Omradetyper.Fylke ? !fylkePerNummer.ContainsKey(x.Key.Kode) : !kommunePerNummer.ContainsKey(x.Key.Kode))
            .Select(x => $"{x.Key.Type} {x.Key.Kode} {x.Value.Term} finnes i basen, men ikke i øyeblikksbildet — ikke avsluttet automatisk.");
        t.Hoppet.AddRange(registrerte);

        // ---- 2. Kommunen som rettssubjekt → territoriet ----
        var (utenRettssubjekt, utenKommune) = await KommuneTerritorierAsync(t, kilder, kommunePerNummer, ct);

        // ---- 3. Domstolene ----
        var domstol = await DomstolerAsync(t, kilder, kommunePerNummer, ct);

        // ---- 4. Statsforvaltere ----
        await StatsforvaltereAsync(t, kilder, fylkePerNavn, ct);

        // ---- 5. Helseregioner ----
        await HelseregionerAsync(t, kilder, fylkePerNavn, fylkePerNummer, ct);

        return new Resultat(t.NyeOmrader, t.NyeKanter, t.NyeVirksomheter, t.KommunenummerFylt, t.Hoppet, t.Navneavvik,
            utenRettssubjekt, utenKommune,
            domstol?.Uloste ?? [], domstol?.DelteKommuner ?? new Dictionary<string, IReadOnlyList<string>>(),
            domstol?.Konverteringsavvik ?? [], t.GjortTilForslag, t.SlettedeKanter, t.SlettedeUtflatedeKanter);
    }

    private static Resultat Tomt(Tilstand t) =>
        new(0, 0, 0, 0, t.Hoppet, [], [], [], [], new Dictionary<string, IReadOnlyList<string>>(), []);

    // ------------------------------------------------------------------------------------------------

    private static async Task<(List<string> UtenRettssubjekt, List<string> UtenKommune)> KommuneTerritorierAsync(
        Tilstand t, OmraderegisterKilder.Kilder kilder, Dictionary<string, BegrepEntitet> kommunePerNummer, CancellationToken ct)
    {
        var brreg = kilder.BrregKommuner;
        var brregKilde = $"Enhetsregisteret, organisasjonsform KOMM: forretningsadressens kommunenummer er kommunens eget (hentet {brreg.Hentet})";
        // Entydighet først: et kommunenummer som to KOMM-enheter deler, eller som Kartverket ikke kjenner, brukes ikke.
        var perNummer = brreg.Enheter.Where(e => e.Kommunenummer is not null).GroupBy(e => e.Kommunenummer!).ToDictionary(g => g.Key, g => g.ToList());
        var gyldige = new Dictionary<string, string>(StringComparer.Ordinal); // orgnr → kommunenummer
        foreach (var e in brreg.Enheter)
        {
            if (e.Kommunenummer is null) { t.Hoppet.Add($"Brreg {e.Organisasjonsnummer} {e.Navn}: mangler kommunenummer."); continue; }
            if (perNummer[e.Kommunenummer].Count > 1) { t.Hoppet.Add($"Brreg {e.Organisasjonsnummer} {e.Navn}: kommunenummer {e.Kommunenummer} deles av flere KOMM-enheter."); continue; }
            if (!kommunePerNummer.ContainsKey(e.Kommunenummer)) { t.Hoppet.Add($"Brreg {e.Organisasjonsnummer} {e.Navn}: kommunenummer {e.Kommunenummer} finnes ikke hos Kartverket."); continue; }
            gyldige[e.Organisasjonsnummer] = e.Kommunenummer;
        }

        var orgnr = gyldige.Keys.ToList();
        var virksomheter = await t.Db.Virksomheter.Where(v => v.Organisasjonsnummer != null && orgnr.Contains(v.Organisasjonsnummer)).ToListAsync(ct);
        var dekket = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in virksomheter)
        {
            var nummer = gyldige[v.Organisasjonsnummer!];
            if (v.Kommunenummer is null)
            {
                v.Kommunenummer = nummer;
                t.KommunenummerFylt++;
            }
            else if (v.Kommunenummer != nummer)
            {
                // Overskrives ikke: noen har satt et annet nummer, og det er ikke seedens sak å avgjøre hvem som har rett.
                t.Hoppet.Add($"{v.Navn} ({v.Organisasjonsnummer}) har kommunenummer {v.Kommunenummer} i basen, Brreg sier {nummer} — ingen territoriekant.");
                continue;
            }
            dekket.Add(nummer);
            await KantAsync(t, new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade",
                Kantnode.Virksomhet(v.Id), Kantnode.Begrep(kommunePerNummer[nummer].Id),
                KildeUtenforKorpusTekst: $"{brregKilde}; territoriet fra Kartverket kommuneinfo",
                KildeUtenforKorpusLenke: $"https://data.brreg.no/enhetsregisteret/api/enheter/{v.Organisasjonsnummer}",
                KildeUtenforKorpusType: Strukturkanter.Register, KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer,
                Kommentar: "Kommunens eget territorium (docs/33 §5.6)."), ct);
        }
        await t.Db.SaveChangesAsync(ct);

        var utenRettssubjekt = kommunePerNummer.Where(k => !dekket.Contains(k.Key))
            .Select(k => $"{k.Key} {k.Value.Term}").OrderBy(s => s, StringComparer.Ordinal).ToList();
        // Virksomheter seeden selv merket som kommune, men som ikke fikk en territoriekant.
        var kommunerIKatalogen = await t.Db.Virksomheter.Where(v => v.Forvaltningsniva == "kommune").ToListAsync(ct);
        var utenKommune = kommunerIKatalogen.Where(v => v.Organisasjonsnummer is null || !gyldige.ContainsKey(v.Organisasjonsnummer)
                                                        || (v.Kommunenummer is not null && v.Kommunenummer != gyldige[v.Organisasjonsnummer]))
            .Select(v => $"{v.Navn} ({v.Organisasjonsnummer ?? "uten orgnr"})").OrderBy(s => s, StringComparer.Ordinal).ToList();
        return (utenRettssubjekt, utenKommune);
    }

    private static async Task<DomstolinndelingTolker.Resultat?> DomstolerAsync(
        Tilstand t, OmraderegisterKilder.Kilder kilder, Dictionary<string, BegrepEntitet> kommunePerNummer, CancellationToken ct)
    {
        var forskrift = await t.Db.Rettskilder
            .Where(r => r.Eli == DomstolinndelingTolker.ForskriftEli && r.Entitetsstatus == "gjeldende" && r.Status != "Opphevet")
            .Select(r => new { r.Id, r.Tittel }).ToListAsync(ct);
        if (forskrift.Count != 1)
        {
            t.Hoppet.Add($"Inndelingsforskriften ({DomstolinndelingTolker.ForskriftEli}) finnes {forskrift.Count} ganger som gjeldende rettskilde — "
                         + "domstolstrukturen er ikke seedet.");
            return null;
        }
        var forskriftId = forskrift[0].Id;
        var noder = await t.Db.RettskildeNoder
            .Where(n => n.RettskildeId == forskriftId && n.Entitetsstatus == "gjeldende")
            .OrderBy(n => n.Sorteringsrekkefolge)
            .Select(n => new { n.Eid, n.NodeType, n.Overskrift, n.Tekst })
            .ToListAsync(ct);
        var r = DomstolinndelingTolker.Tolk(
            noder.Select(n => new Strukturnode(forskrift[0].Tittel, DomstolinndelingTolker.ForskriftEli, n.Eid, n.NodeType ?? "", n.Overskrift, n.Tekst)).ToList(),
            kilder.Kartverket, kilder.Ssr);

        // ---- Domstolene som virksomheter (Brreg-øyeblikksbildet, nøkkel = orgnr) ----
        var domstoler = kilder.BrregDomstoler.Enheter;
        var tingrettVirksomhet = new Dictionary<string, Virksomhet>(StringComparer.OrdinalIgnoreCase);
        foreach (var tr in r.Tingretter)
        {
            var former = DomstolinndelingTolker.Former(tr.Tekstform);
            var treff = domstoler.Where(d => d.Navn.Split('/').Select(p => p.Trim()).Any(p => former.Contains(p, StringComparer.OrdinalIgnoreCase))).ToList();
            if (treff.Count != 1)
            {
                t.Hoppet.Add($"{tr.Tekstform}: {treff.Count} treff i Brreg-øyeblikksbildet (eksakt navn) — ingen virksomhet, ingen kanter.");
                continue;
            }
            tingrettVirksomhet[tr.Tekstform] = await DomstolAsync(t, treff[0], tr.Tekstform, kilder.BrregDomstoler.Hentet, ct);
        }

        // ---- Tingrett → kommuner, rettssted ----
        foreach (var tr in r.Tingretter)
        {
            if (!tingrettVirksomhet.TryGetValue(tr.Tekstform, out var v)) continue;
            foreach (var k in tr.Kommuner)
            {
                await KantAsync(t, new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade",
                    Kantnode.Virksomhet(v.Id), Kantnode.Begrep(kommunePerNummer[k.Kommunenummer].Id),
                    HjemmelRettskildeId: forskriftId, HjemmelEid: tr.Eid, Kommentar: k.Avgjort), ct, "monster:inndeling-rettskrets");
            }
            foreach (var s in tr.Rettssteder)
            {
                BegrepEntitet sted;
                if (s.Kommunenummer is not null)
                {
                    sted = kommunePerNummer[s.Kommunenummer];
                }
                else
                {
                    var tt = s.Tettsted!;
                    sted = await OmradeAsync(t, Omradetyper.Tettsted, tt.Stedsnummer.ToString(System.Globalization.CultureInfo.InvariantCulture), tt.Navn,
                        $"{tt.Navneobjekttype} i SSR (stedsnummer {tt.Stedsnummer}), rettssted for {tr.Tekstform}.", ct);
                    await KantAsync(t, new NyStrukturkant(Strukturkanter.Omradesammensetning, "bestar_av",
                        Kantnode.Begrep(kommunePerNummer[tt.Kommunenummer].Id), Kantnode.Begrep(sted.Id),
                        KildeUtenforKorpusTekst: $"Kartverket SSR: {tt.Navn} ({tt.Navneobjekttype}, stedsnummer {tt.Stedsnummer}) ligger i kommune {tt.Kommunenummer} (hentet {kilder.Ssr.Hentet})",
                        KildeUtenforKorpusLenke: $"https://ws.geonorge.no/stedsnavn/v1/sted?stedsnummer={tt.Stedsnummer}",
                        KildeUtenforKorpusType: Strukturkanter.Register, KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer), ct);
                }
                await KantAsync(t, new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_sete_i",
                    Kantnode.Virksomhet(v.Id), Kantnode.Begrep(sted.Id),
                    HjemmelRettskildeId: forskriftId, HjemmelEid: tr.Eid), ct, "monster:inndeling-rettssted");
            }
        }

        // ---- Lagsogn og tingrett → lagsogn (§§ 11–16, «Til lagsognet X sogner A tingrett og B tingrett») ----
        // [ENDRET, issue #345, Johann 2026-10-08] Hele kjeden står i forskriften, og bare den lagres:
        //   §§ 2–9      tingrett «dekker kommunene …»  → A tingrett har_ansvarsomrade kommune (over)
        //   §§ 11–16    «Til lagsognet X sogner …»     → A tingrett sogner_til lagsogn (her)
        //   §§ 11–16    «… utgjør Z lagdømme»          → O lagdømme bestar_av lagsogn (under)
        // Kommunens lagsogn og lagdømme AVLEDES i oppslaget (OmradeOppslagTjeneste): kommune ← tingrett → lagsogn →
        // lagdømme.
        //
        // Typekoden er A «sogner_til» (aktør → område), samme som fasiten og mønsteret inndeling-sogner
        // («annet:sogner_til», kategori ansvarsomrade). O er utelukket fordi fra-noden er tingretten, ikke et område:
        // #312 lager ingen rettskrets-node (bare områder en kilde navngir får node). G del_av er organ → rettssubjekt.
        // Lest slik: tingrettens ansvarsområde (domssognet) inngår i lagsognet.
        //
        // [FJERNET, issue #345, Johann 2026-10-08] Her lå 357 kanter «O lagsogn bestar_av kommune», avledet via
        // tingrettens kommuneliste (kommentar «Via X tingrett, som sogner til lagsognet …»). Johann: det er utflatet
        // data som teksten ikke sier, og «alt skal kunne utledes fra forskriften ned på kommunenivå». Kantene slettes i
        // oppryddingen under, gjennom tjenesten (logget i Proveniens), og lages ikke lenger.
        var lagsognOmrade = new Dictionary<string, BegrepEntitet>(StringComparer.Ordinal);
        foreach (var l in r.Lagsogn)
        {
            var omr = await OmradeAsync(t, Omradetyper.Lagsogn, null, $"lagsogn {l.Navn}",
                $"Lagsognet {l.Navn} i forskrift om inndelingen av rettskretser og lagdømmer.", ct);
            lagsognOmrade[l.Navn] = omr;
            foreach (var trNavn in l.Tingretter)
            {
                var tr = r.Tingretter.Single(x => string.Equals(x.Tekstform, trNavn, StringComparison.OrdinalIgnoreCase));
                if (!tingrettVirksomhet.TryGetValue(tr.Tekstform, out var v)) continue; // listet i Hoppet over
                await KantAsync(t, new NyStrukturkant(Strukturkanter.Ansvarsomrade, SognerTil,
                    Kantnode.Virksomhet(v.Id), Kantnode.Begrep(omr.Id),
                    HjemmelRettskildeId: forskriftId, HjemmelEid: l.Eid), ct, SognerTilOppdagelseskilde);
            }
        }
        var lagdommeOmrade = new Dictionary<string, BegrepEntitet>(StringComparer.Ordinal);
        foreach (var ld in r.Lagdommer)
        {
            var omr = await OmradeAsync(t, Omradetyper.Lagdomme, null, ld.Navn,
                $"{ld.Navn} i forskrift om inndelingen av rettskretser og lagdømmer.", ct);
            lagdommeOmrade[ld.Navn] = omr;
            foreach (var ls in ld.Lagsogn)
            {
                await KantAsync(t, new NyStrukturkant(Strukturkanter.Omradesammensetning, "bestar_av",
                    Kantnode.Begrep(omr.Id), Kantnode.Begrep(lagsognOmrade[ls].Id),
                    HjemmelRettskildeId: forskriftId, HjemmelEid: ld.Eid), ct, "monster:inndeling-sogner");
            }

            // [ENDRET, issue #345, Johann 2026-10-08] Kanten lagmannsrett → lagdømme er lagt inn igjen, som FORSLAG
            // (blokken etter oppryddingen under). Navneregel: lagmannsretten for lagdømmet «X lagdømme» er «X
            // lagmannsrett» (FOR-2021-01-22-163 § 10 første ledd: «Hvert lagdømme har en lagmannsrett»). Bekreftet
            // som regel av Johann 2026-10-08.
            //
            // Historikk: i #342 ble kanten først lagret som validert, koblet via navnet. Etter Johanns «ikke via navn»
            // ble den fjernet ([FJERNET]). Begrunnelsen var at ingen tekst i korpus parer lagmannsrett og lagdømme.
            // Den runden leste feilaktig domstolloven § 10 som kandidat for hjemmel; den paragrafen handler om
            // lagmannsrettenes dommere. Johann pekte på forskriften § 10 første ledd og bekreftet navneregelen.
        }

        // [Ny, issue #312, Johanns beslutning 2026-10-08] Opprydding av kanter seeden lagret FØR beslutningen, gjennom
        // tjenesten (ikke SQL). Bare seedens egne, urørte kanter (OpprettetAv = seed, SistEndretAv NULL): en kant et
        // menneske har godkjent etterpå har SistEndretAv satt og røres aldri.
        var lagsognIder = lagsognOmrade.Values.Select(b => b.Id).ToHashSet();
        var kommuneIder = kommunePerNummer.Values.Select(b => b.Id).ToHashSet();
        bool ErLagsognKommune(StrukturkantEntitet k) =>
            k.Kategori == Strukturkanter.Omradesammensetning && k.FraBegrepId is { } fra && lagsognIder.Contains(fra)
            && k.TilBegrepId is { } til && kommuneIder.Contains(til);

        // [Ny, issue #345] De 357 utflatede lagsogn → kommune-kantene (se [FJERNET] over), uansett status: Johanns
        // beslutning gjelder selve utsagnet, ikke om det er godkjent. Bare seedens egne kanter med forskriften som hjemmel.
        var utflatede = (await t.Db.Strukturkanter
                .Where(k => k.HjemmelRettskildeId == forskriftId && k.OpprettetAv == OpprettetAv
                            && k.Kategori == Strukturkanter.Omradesammensetning)
                .ToListAsync(ct))
            .Where(ErLagsognKommune).ToList();
        foreach (var k in utflatede)
        {
            await t.Kanter.SlettAsync(k.Id, OpprettetAv, ct);
            t.Kantnokler.Remove((k.Kategori, k.Typekode, k.FraVirksomhetId, k.FraBegrepId, k.TilVirksomhetId, k.TilBegrepId, k.HjemmelRettskildeId));
            t.SlettedeUtflatedeKanter++;
        }

        var gamle = await t.Db.Strukturkanter
            .Where(k => k.HjemmelRettskildeId == forskriftId && k.OpprettetAv == OpprettetAv && k.SistEndretAv == null
                        && k.Status == "validert")
            .ToListAsync(ct);
        var lagdommeIder = lagdommeOmrade.Values.Select(b => b.Id).ToHashSet();
        foreach (var k in gamle)
        {
            if (k.Kategori == Strukturkanter.Ansvarsomrade && k.FraVirksomhetId is not null && k.TilBegrepId is { } til
                && lagdommeIder.Contains(til) && k.OppdagelsesKilde != DomstolinndelingTolker.LagmannsrettOppdagelseskilde)
            {
                // Den VALIDERTE navnekanten fra før beslutningen (oppdagelseskilde «manuell»). [ENDRET, #345] Den
                // erstattes av forslaget under, med samme nøkkel; derfor kjører oppryddingen FØR forslaget lages.
                await t.Kanter.SlettAsync(k.Id, OpprettetAv, ct);
                t.Kantnokler.Remove((k.Kategori, k.Typekode, k.FraVirksomhetId, k.FraBegrepId, k.TilVirksomhetId, k.TilBegrepId, k.HjemmelRettskildeId));
                t.SlettedeKanter++;
            }
            else if (k.OppdagelsesKilde.StartsWith("monster:", StringComparison.Ordinal))
            {
                await t.Kanter.GjorTilForslagAsync(k.Id, k.OppdagelsesKilde, OpprettetAv, ct);
                t.GjortTilForslag++;
            }
        }

        // ---- [Ny, issue #345, Johann 2026-10-08] Lagmannsrett → lagdømme, som forslag ----
        // Hjemmel: forskriften § 10 første ledd (r.LagmannsrettEid). Navneregelen (DomstolinndelingTolker.
        // ParLagmannsretter) gir lagmannsretten. Finnes ikke «X lagmannsrett» eksakt, lages ingen kant, og lagdømmet
        // listes i Hoppet.
        if (r.LagmannsrettEid is not null)
        {
            var (par, uparet) = DomstolinndelingTolker.ParLagmannsretter(r.Lagdommer, domstoler);
            t.Hoppet.AddRange(uparet);
            foreach (var p in par)
            {
                var lv = await DomstolAsync(t, p.Lagmannsrett, navneform: null, kilder.BrregDomstoler.Hentet, ct);
                await KantAsync(t, new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade",
                    Kantnode.Virksomhet(lv.Id), Kantnode.Begrep(lagdommeOmrade[p.Lagdomme.Navn].Id),
                    HjemmelRettskildeId: forskriftId, HjemmelEid: r.LagmannsrettEid,
                    Kommentar: DomstolinndelingTolker.LagmannsrettKommentar(p.Lagdomme.Navn, p.Lagdomme.Navn[..^" lagdømme".Length] + " lagmannsrett")),
                    ct, DomstolinndelingTolker.LagmannsrettOppdagelseskilde);
            }
        }
        return r;
    }

    private static async Task<Virksomhet> DomstolAsync(
        Tilstand t, OmraderegisterKilder.BrregDomstol d, string? navneform, string hentet, CancellationToken ct)
    {
        var v = await t.Db.Virksomheter.FirstOrDefaultAsync(x => x.Organisasjonsnummer == d.Organisasjonsnummer, ct);
        if (v is null)
        {
            // Opprettes fra Brreg-øyeblikksbildet — samme mønster som Stortinget i #311 (fil, ikke nettverk). Navnet er
            // registerets. Forvaltningsnivå og aktørtype står NULL (docs/20 §7.2 [LÅST]: ikke gjett fra navn/orgform).
            // Aktiv=false: til stede i registeret, ikke valgbart for nytt arbeid — samme som OrganisasjonsregisterSeed.
            v = new Virksomhet
            {
                Id = Guid.NewGuid(), Navn = d.Navn, Organisasjonsnummer = d.Organisasjonsnummer,
                OrganisasjonsformKode = d.Organisasjonsform, Aktiv = false, OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            t.Db.Virksomheter.Add(v);
            var p = ProveniensHjelper.NyRad("virksomhet", v.Id, virksomhetId: null, "opprettet", OpprettetAv);
            p.KildeReferanserJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                kilde = "Enhetsregisteret (Seed/brreg-domstoler.json)", hentet, issue = 312,
            });
            t.Db.Proveniens.Add(p);
            await t.Db.SaveChangesAsync(ct);
            t.NyeVirksomheter++;
            // Navneformen er forskriftens tekstform (CLAUDE.md §0: «det teksten sier, er navneformen»).
            if (navneform is not null)
            {
                await t.Navneformer.OpprettVirksomhetsbegrepAsync(v.Id, navneform, OpprettetAv,
                    navneformgrunn: VirksomhetVisningsnavnTjeneste.VisningsGrunn, ct: ct);
            }
        }
        return v;
    }

    private static async Task StatsforvaltereAsync(
        Tilstand t, OmraderegisterKilder.Kilder kilder, Dictionary<string, BegrepEntitet> fylkePerNavn, CancellationToken ct)
    {
        var kilde = kilder.Statsforvaltere.Sekundaerkilde;
        foreach (var e in kilder.Statsforvaltere.Embeter)
        {
            var v = await t.Db.Virksomheter.FirstOrDefaultAsync(x => x.Organisasjonsnummer == e.Organisasjonsnummer, ct);
            if (v is null) { t.Hoppet.Add($"{e.Navn} ({e.Organisasjonsnummer}) finnes ikke i virksomhetskatalogen — ingen embetsområde-kanter."); continue; }
            foreach (var fylkesnavn in e.Fylker)
            {
                if (!fylkePerNavn.TryGetValue(fylkesnavn, out var fylke)) { t.Hoppet.Add($"{e.Navn}: fylket «{fylkesnavn}» finnes ikke hos Kartverket."); continue; }
                await KantAsync(t, new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade",
                    Kantnode.Virksomhet(v.Id), Kantnode.Begrep(fylke.Id),
                    KildeUtenforKorpusTekst: kilde.Tekst, KildeUtenforKorpusLenke: kilde.Lenke,
                    KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: Strukturkanter.Sekundaer,
                    Kommentar: "Embetsinndelingen er satt ved kgl.res. 10.03.2017 (resolusjonsteksten ikke funnet); sammensetningen "
                               + "leses av embetets navn, som er fastsatt ved kgl.res. (for Østfold, Buskerud, Oslo og Akershus: "
                               + "FOR-2024-03-15-452)."), ct);
            }
        }
    }

    private static async Task HelseregionerAsync(
        Tilstand t, OmraderegisterKilder.Kilder kilder, Dictionary<string, BegrepEntitet> fylkePerNavn,
        Dictionary<string, BegrepEntitet> fylkePerNummer, CancellationToken ct)
    {
        // Gamle fylkesnavn (før 1.1.2024) → dagens fylkesnumre, fra SSB KLASS 104. Navnet før « - » (samisk/kvensk form
        // etter) er det vedtektene bruker.
        var ssb = kilder.Ssb.Svar.Endringer.GroupBy(e => e.GammeltNavn.Split(" - ")[0].Trim())
            .ToDictionary(g => g.Key, g => g.Select(e => e.NyKode).Distinct().ToList(), StringComparer.Ordinal);

        foreach (var rhf in kilder.Helseregioner.Rhf)
        {
            var kildetekst = $"Vedtekter for {rhf.Navn} § 3 (sist endret {rhf.SistEndret}): «{rhf.OmfatterOrdrett}»";
            var region = await OmradeAsync(t, Omradetyper.Helseregion, null, rhf.Helseregion,
                $"{rhf.Helseregion} — ansvarsområdet for {rhf.Navn} etter vedtektene § 3.", ct);
            foreach (var navn in rhf.Omfatter)
            {
                var deler = new List<(BegrepEntitet Omrade, string? Kommentar)>();
                if (fylkePerNavn.TryGetValue(navn, out var fylke))
                {
                    deler.Add((fylke, null));
                }
                else if (ssb.TryGetValue(navn, out var nyeKoder) && nyeKoder.All(fylkePerNummer.ContainsKey))
                {
                    deler.AddRange(nyeKoder.Select(k => (fylkePerNummer[k], (string?)
                        $"Vedtektene nevner «{navn}», som ble delt 1.1.2024; SSB KLASS 104 gir {string.Join(", ", nyeKoder.Select(x => $"{x} {fylkePerNummer[x].Term}"))}.")));
                }
                else if (navn == "Svalbard")
                {
                    var svalbard = await OmradeAsync(t, Omradetyper.Annet, null, "Svalbard",
                        "Svalbard — ikke et fylke eller en kommune i Kartverkets inndeling; navngitt i vedtektene for Helse Nord RHF § 3.", ct);
                    deler.Add((svalbard, null));
                }
                else
                {
                    t.Hoppet.Add($"{rhf.Helseregion}: «{navn}» er verken et fylke hos Kartverket eller et gammelt fylke i SSB-endringslista.");
                    continue;
                }
                foreach (var (omrade, kommentar) in deler)
                {
                    await KantAsync(t, new NyStrukturkant(Strukturkanter.Omradesammensetning, "bestar_av",
                        Kantnode.Begrep(region.Id), Kantnode.Begrep(omrade.Id),
                        KildeUtenforKorpusTekst: kildetekst, KildeUtenforKorpusLenke: rhf.Lenke,
                        KildeUtenforKorpusType: "vedtekter", KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer,
                        Kommentar: kommentar), ct);
                }
            }

            var v = await t.Db.Virksomheter.FirstOrDefaultAsync(x => x.Organisasjonsnummer == rhf.Organisasjonsnummer, ct);
            if (v is null) { t.Hoppet.Add($"{rhf.Navn} ({rhf.Organisasjonsnummer}) finnes ikke i virksomhetskatalogen — ingen ansvarsområde-kant."); continue; }
            await KantAsync(t, new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade",
                Kantnode.Virksomhet(v.Id), Kantnode.Begrep(region.Id),
                KildeUtenforKorpusTekst: kildetekst, KildeUtenforKorpusLenke: rhf.Lenke,
                KildeUtenforKorpusType: "vedtekter", KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer), ct);
        }
    }

    // ------------------------------------------------------------------------------------------------

    /// <summary>Finn-eller-opprett på stabil nøkkel: (type, kode) eller, uten kode, (type, term).</summary>
    private static async Task<BegrepEntitet> OmradeAsync(Tilstand t, string type, string? kode, string term, string definisjon, CancellationToken ct)
    {
        BegrepEntitet? funnet = kode is not null ? t.MedKode.GetValueOrDefault((type, kode)) : t.UtenKode.GetValueOrDefault((type, term));
        if (funnet is not null)
        {
            if (kode is not null && funnet.Term != term)
            {
                t.Navneavvik.Add($"{type} {kode}: basen har «{funnet.Term}», kilden har «{term}» — navnet er ikke overskrevet.");
            }
            return funnet;
        }
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = null, Begrepskategori = Nodetyper.Omrade, Omradetype = type, Omradekode = kode,
            Term = term, Definisjon = definisjon, Status = "publisert", OpprettetAv = OpprettetAv, OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        t.Db.Begreper.Add(b);
        t.Db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", b.Id, virksomhetId: null, "opprettet", OpprettetAv));
        await t.Db.SaveChangesAsync(ct);
        if (kode is not null) t.MedKode[(type, kode)] = b; else t.UtenKode[(type, term)] = b;
        t.NyeOmrader++;
        return b;
    }

    /// <summary>Oppretter kanten via <see cref="StrukturkantTjeneste"/> hvis (kategori, type, fra, til, hjemmel) ikke alt finnes.</summary>
    /// <param name="oppdagelseskilde"><c>monster:&lt;id&gt;</c> for kanter konverteringen (#307) leste ut av
    /// forskriftsteksten, ellers <c>manuell</c> — registerkanter og de kuraterte filene er ikke maskinelt tolket fra
    /// tekst, og kilden står i kildefeltet. (Vokabularet har ingen egen verdi for registerimport.)</param>
    private static async Task KantAsync(Tilstand t, NyStrukturkant ny, CancellationToken ct, string oppdagelseskilde = "manuell")
    {
        var nokkel = (ny.Kategori, ny.Typekode, ny.Fra.VirksomhetId, ny.Fra.BegrepId, ny.Til?.VirksomhetId, ny.Til?.BegrepId, ny.HjemmelRettskildeId);
        if (t.Kantnokler.Contains(nokkel)) return;
        // [ENDRET, issue #312, Johanns beslutning 2026-10-08] Kanter konverteringen leste ut av lovteksten (monster:<id>)
        // lagres som FORSLAG og godkjennes samlet av et menneske (StrukturkantTjeneste.GodkjennAlleForHjemmelAsync,
        // docs/33 §5.3). Registerkanter (kildetype register: Kartverket, Enhetsregisteret, SSR) og de kuraterte filene
        // (lest av et menneske, kilden oppgitt) lagres som validert: de er data fra en kilde, ikke tolket lovtekst.
        var status = oppdagelseskilde.StartsWith("monster:", StringComparison.Ordinal) ? "foreslatt_av_ai" : "validert";
        var opprettet = await t.Kanter.OpprettAsync(ny with { OppdagelsesKilde = oppdagelseskilde, Status = status }, OpprettetAv, ct);
        t.Kantnokler.Add(nokkel);
        if (opprettet.VarNy) t.NyeKanter++;
    }
}
