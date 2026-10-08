using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RegelIde.Data.Strukturkonvertering;

/// <summary>
/// [Ny, #308 strukturmodell-KI, 2026-10-07] KI-laget i den automatiske konverteringen (docs/33 §5.2
/// punkt 2): nodetekst inn, fasit-formatet (FORMAT.md) ut, via <see cref="IKiAgentKlient"/> — samme
/// kontrakt som <see cref="MonsterStrukturkonverterer"/>, slik at de to lagene måles likt mot fasiten.
/// <para>
/// <b>Svarskjemaet er FORMAT.md.</b> Systeminstruksen er samme mønster som
/// <c>VirksomhetOgGruppeKiOppdagelseTjeneste</c> (#285): én konstant instruks som beskriver JSON-svaret
/// eksakt, med kategoriene og typene hentet fra <see cref="Strukturkontrakt"/> — samme liste som
/// valideringen bruker.
/// </para>
/// <para>
/// <b>Oppdeling (som #295):</b> nodene deles grådig i kall à maks
/// <see cref="KiStrukturkonverteringsvalg.MaksTegnPerKall"/> tegn, aldri midt i en node, og aldri på
/// tvers av en ledsagende kilde. Er svaret ugyldig JSON — i praksis oftest et avkuttet svar fordi
/// utdata ble for langt — deles kallet i to og prøves på nytt, inntil
/// <see cref="KiStrukturkonverteringsvalg.MaksDelingsdybde"/>. Det er en ny forespørsel med mindre
/// inndata, ikke en reparasjon av svaret; hvert ugyldig svar telles.
/// </para>
/// <para>
/// <b>Nodereferanser:</b> hver node med tekst får en kort tagg (<c>[n17]</c>) i konteksten, og svaret
/// oppgir taggen i <c>eid</c>. Taggen slås opp til nodens ekte eId før noe skrives videre. Grunnen er
/// målt, ikke antatt: #285 observerte at modellen forkorter lange eId-er («§1/ledd-1», «§1-ledd-1»)
/// og trengte et suffiks-oppslag for å finne noden igjen. Med tagg trengs ingen slik fallback — en
/// tagg som ikke står i DETTE kallets inndata er ganske enkelt ukjent.
/// </para>
/// <para>
/// <b>Hard validering, ingen reparasjon [LÅST, #308]:</b> en rad kastes (og telles per årsak, se
/// <see cref="KastetArsak"/>) når sitatet ikke er en eksakt (ordinal) delstreng av nodens tekst, når
/// taggen ikke finnes i kallets inndata, når kategori eller type ikke står i FORMAT.md-lista (eller er
/// <c>annet:&lt;x&gt;</c>), når fra/til peker på en aktør-id som ikke finnes eller på en aktør som selv
/// er ugyldig, eller når et påkrevd felt mangler/har feil verdi. En aktør er ugyldig når tekstformen
/// eller en variant ikke står i kallets tekst (uten skille på store/små bokstaver), eller når
/// entitetstype/oppløsning ikke er i lista. Ingenting av dette «rettes» — ingen nærmeste treff, ingen
/// standardverdi for et manglende felt (CLAUDE.md §8).
/// </para>
/// <para>
/// Ingen database og ingen skriving (#308 akseptansekriterium 4). Lagring som forslag er #313.
/// </para>
/// </summary>
public sealed class KiStrukturkonverterer(
    IKiAgentKlient kiKlient,
    IConfiguration config,
    ILogger<KiStrukturkonverterer>? logger = null,
    KiStrukturkonverteringsvalg? valg = null) : IStrukturkonverterer
{
    private readonly ILogger<KiStrukturkonverterer> _logger = logger ?? NullLogger<KiStrukturkonverterer>.Instance;
    private readonly KiStrukturkonverteringsvalg _valg = valg ?? new KiStrukturkonverteringsvalg();

    private const int MaksEidEksempler = 5;

    /// <summary>
    /// Modellnavnet i proveniensen: samme regel som <c>AiForslagVersjon</c> i KI-agentene (docs/14) —
    /// den konfigurerte modellen når en ekte leverandør er aktiv, ellers <c>stub-v1</c>. En aktiv
    /// leverandør uten modell er en konfigfeil, ikke noe å gjette rundt.
    /// </summary>
    public string Modell
    {
        get
        {
            if (config["RegelIde:KiAgent:Leverandor"] != "OpenAiKompatibel") return "stub-v1";
            var modell = config["RegelIde:KiAgent:Modell"];
            return string.IsNullOrWhiteSpace(modell)
                ? throw new InvalidOperationException("RegelIde:KiAgent:Modell er ikke satt. Ingen gjettet fallback.")
                : modell;
        }
    }

    /// <summary><c>ki:&lt;modell&gt;</c> — verdien i <see cref="StrukturUtsagn.Oppdagelseskilde"/>.</summary>
    public string Oppdagelseskilde => "ki:" + Modell;

    /// <summary>
    /// Synkron variant for <see cref="IStrukturkonverterer"/>. Grensesnittet fra #307 er bevisst
    /// synkront (mønsterlaget har ingen I/O); KI-laget blokkerer her på
    /// <see cref="KonverterMedRapportAsync"/>. Kall den asynkrone direkte der det finnes en async-kontekst
    /// — den gir også tokens og kasserte rader, som dette grensesnittet ikke har plass til.
    /// </summary>
    public Strukturdokument Konverter(Strukturkonverteringsgrunnlag grunnlag) =>
        KonverterMedRapportAsync(grunnlag).GetAwaiter().GetResult().Dokument;

    /// <summary>Konverterer grunnlaget og returnerer dokumentet sammen med kall-, token- og kassasjonstall.</summary>
    public async Task<KiStrukturkonverteringsresultat> KonverterMedRapportAsync(
        Strukturkonverteringsgrunnlag grunnlag, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(grunnlag);
        var modell = Modell;
        var deler = DelOpp(grunnlag, _valg.MaksTegnPerKall);

        using var sperre = new SemaphoreSlim(Math.Max(1, _valg.MaksParallelleKall));
        var oppgaver = deler.Select(async del =>
        {
            await sperre.WaitAsync(ct);
            try
            {
                return await KjorDelAsync(del, 0, ct);
            }
            finally
            {
                sperre.Release();
            }
        }).ToList();
        var delresultater = await Task.WhenAll(oppgaver);

        // Sammenstilling i dokumentrekkefølge (uavhengig av hvilken del som svarte først), så samme
        // KI-svar alltid gir samme utdata.
        var aktorer = new AktorBygger();
        var utsagn = new List<StrukturUtsagn>();
        var kastet = new List<KastetRad>();
        var sett = new HashSet<string>(StringComparer.Ordinal);
        var kallfeil = new List<string>();
        int antallKall = 0, ugyldigJson = 0, feiledeKall = 0, taptNoder = 0;
        int? inn = null, ut = null;
        var kastedeAktorer = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var d in delresultater)
        {
            antallKall += d.AntallKall;
            ugyldigJson += d.UgyldigJson;
            feiledeKall += d.FeiledeKall;
            taptNoder += d.TaptNoder;
            kallfeil.AddRange(d.Kallfeil);
            if (d.InputTokens is not null) inn = (inn ?? 0) + d.InputTokens;
            if (d.OutputTokens is not null) ut = (ut ?? 0) + d.OutputTokens;
            foreach (var (arsak, n) in d.KastedeAktorer) kastedeAktorer[arsak] = kastedeAktorer.GetValueOrDefault(arsak) + n;
            kastet.AddRange(d.Kastet);

            foreach (var r in d.Rader)
            {
                var nokkel = string.Join('\u001f', r.Eid, r.Kategori, r.Type,
                    r.Fra?.Tekstform.ToLowerInvariant(), r.Til?.Tekstform.ToLowerInvariant(), r.Sitat);
                if (!sett.Add(nokkel))
                {
                    kastet.Add(new KastetRad(KastetArsak.Duplikat, r.Eid, r.Sitat, r.Kategori, r.Type,
                        "Samme eId, kategori, type, fra, til og sitat som en tidligere rad."));
                    continue;
                }
                var fraId = r.Fra is null ? null : aktorer.Registrer(r.Fra, r.Eid);
                var tilId = r.Til is null ? null : aktorer.Registrer(r.Til, r.Eid);
                utsagn.Add(new StrukturUtsagn(
                    Id: "u" + (utsagn.Count + 1),
                    Eid: r.Eid,
                    Sitat: r.Sitat,
                    Kategori: r.Kategori,
                    Type: r.Type,
                    Fra: fraId,
                    Til: tilId,
                    Objekt: r.Objekt,
                    Polaritet: r.Polaritet,
                    Avgrensning: r.Avgrensning,
                    Betinget: r.Betinget,
                    KildeUtenforKorpus: r.KildeUtenforKorpus,
                    Sikkerhet: r.Sikkerhet,
                    Kommentar: r.Kommentar)
                {
                    Oppdagelseskilde = "ki:" + modell,
                    Normform = r.Normform,
                    Undertype = r.Undertype,
                    Grunnlag = r.Grunnlag,
                    Delegerbar = r.Delegerbar,
                });
            }
        }

        var ledsagende = grunnlag.Noder
            .Where(n => !string.Equals(n.Eli, grunnlag.Eli, StringComparison.Ordinal))
            .Select(n => new StrukturLedsagendeKilde(n.Rettskilde, n.Eli))
            .Distinct()
            .ToList();

        var dokument = new Strukturdokument(
            Rettskilde: grunnlag.Rettskilde,
            Eli: grunnlag.Eli,
            Ledsagende: ledsagende,
            AnnotertAv: $"KiStrukturkonverterer (ki:{modell}, #308) — maskinelt, ikke verifisert",
            NoderLest: grunnlag.Noder.Count,
            Aktorer: aktorer.Bygg(),
            Utsagn: utsagn);

        return new KiStrukturkonverteringsresultat(
            dokument, modell, deler.Count, antallKall, ugyldigJson, feiledeKall, taptNoder, inn, ut,
            kastet, kastedeAktorer, kallfeil);
    }

    // ---------------------------------------------------------------------------------------------
    // Systeminstruks
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Systeminstruksen. Bygget én gang fra <see cref="Strukturkontrakt"/>, så lista KI-en ser er den
    /// samme som valideringen godtar. Innholdet er FORMAT.md sine prinsipper og felt, ikke fasitens
    /// konvensjoner — instruksen er IKKE iterert mot fasiten (#308: én måling, ikke prompt-tilpasning).
    /// </summary>
    public static string SystemInstruks { get; } = ByggSystemInstruks();

    /// <summary>[Ny, issue #341, Johanns beslutning 3] Leksikonets uttrykk, ett per linje — det KI-en IKKE skal foreslå for.</summary>
    private static string Leksikonliste() => string.Join("\n", Kompetanseleksikon.Regler
        .Select(r => $"  - {string.Join(" / ", r.Uttrykk.Select(u => $"«{u}»"))} → {r.Kategori}/{r.Type}{(r.Normform is null ? "" : $" ({r.Normform})")}"));

    /// <summary>[Ny, issue #352] Undertypene per kompetansetype, med verbene fra leksikonet: «bare på oppnevningskompetanse —
    /// valg («velger»), …».</summary>
    private static string Undertypeliste() => string.Join("; ", Kompetanseleksikon.Undertyper
        .GroupBy(u => u.Type)
        .Select(g => $"bare på {g.Key} — {string.Join(", ", g.Select(u => $"\"{u.Undertype}\" («{u.Verb}»)"))}"));

    private static string ByggSystemInstruks()
    {
        var typer = string.Join("\n", Strukturkontrakt.TyperPerKategori.Select(kv => $"  - {kv.Key}: {string.Join(", ", kv.Value)}"));
        return $$"""
            Du er en juridisk annotatør som leser norsk lovtekst og skriver ned hva teksten FAKTISK sier om
            forvaltningens STRUKTUR: hvilke aktører som finnes, hvordan de er organisert, hvilke relasjoner og
            hvilken myndighet (kompetanse) de har, og hvilke områder de virker i. Annoter det teksten sier — ikke
            det du vet om norsk forvaltning fra før.

            Ikke annoter plikter/rettigheter for private, saksbehandlingsregler, frister eller materielle vilkår.
            MEN kompetanse («X kan gi forskrift om …», «X treffer vedtak i saker etter …», «klage over vedtak etter
            § 5 går til Y», «X fører tilsyn med at …») ER struktur og SKAL annoteres.

            INNDATA: lovtekst der hver node med tekst står på én linje med en tagg foran, f.eks.
            "[n17] Departementet kan gi forskrift om …". Linjer som begynner med # er overskrifter (ikke siterbare).

            SVAR: KUN ett JSON-objekt, ingen markdown-kodeblokk (```), ingen tekst før eller etter:
            {"aktorer": [ ... ], "utsagn": [ ... ]}

            "aktorer" — ett innslag per DISTINKT omtaleform av en aktør (ikke per forekomst):
            - "id": "a1", "a2", … (unik i svaret)
            - "tekstform": aktøren EKSAKT slik den står i teksten (samme stavemåte)
            - "varianter": andre skrivemåter av SAMME omtaleform som står ordrett i teksten (ellers utelat)
            - "entitetstype": en av {{string.Join(", ", Strukturkontrakt.Entitetstyper)}}, eller "annet:<kort_navn>"
              (rettssubjekt = stat/kommune/fylkeskommune/RHF/HF/stiftelse/AS; organ = Stortinget, Kongen,
              departement, direktorat, nemnd, styre, domstol; organisatorisk_enhet = intern enhet uten egen
              myndighet; rolle = funksjon i en sammenheng; klasse = kategori av subjekter; omrade = geografisk/
              jurisdiksjonelt område)
            - "navngitt": true hvis den peker på én bestemt (Sametinget, NVE), false hvis generisk (kommunen,
              departementet)
            - "referent": konkret navn KUN hvis teksten ALENE avgjør det, ellers utelat
            - "oppløsning": hvordan referenten må løses: {{string.Join(", ", Strukturkontrakt.Opplosninger)}}
            - "distributiv": true hvis regelen gjelder hvert medlem for seg («kommunen skal …»)

            "utsagn" — ett innslag per strukturelt utsagn (en setning kan gi flere):
            - "eid": taggen til noden utsagnet står i, uten hakeparentes (f.eks. "n17")
            - "sitat": EKSAKT delstreng av den nodens tekst, tegn for tegn (kopier, ikke parafraser), maks ~30 ord
            - "kategori" og "type" — bruk disse når de passer:
            {{typer}}
              Passer ingen: "annet:<kort_navn>" (små bokstaver og _) og forklar i "kommentar".
              relasjon = STRUKTUR uten myndighet (eierskap, ledelse, sekretariat, rapportering, etterfølger, representasjon,
                og en GJENNOMFØRT delegering i et delegeringsvedtak = har_delegert_til);
              kompetanse = MYNDIGHET: «A har kompetanse av typen X, eventuelt overfor B, når det gjelder Y». Klageinstans,
                instruksjon, omgjøring, oppnevning, tilsyn med en aktør, avsetting, sanksjon og samtykke er kompetanse —
                "til" = motparten (den det gjelder), ellers null med "objekt" satt. «X kan delegere» = delegeringskompetanse.
                Å velge, ansette, utpeke eller oppnevne noen er oppnevningskompetanse med "undertype"; ankeinstans er
                overprovingskompetanse med "undertype": "anke".
                Forskrift er normgivningskompetanse med "normform": "forskrift". vedtakskompetanse betyr enkeltvedtak.
                Kan du ikke avgjøre typen for et kompetanseuttrykk, bruk "{{Strukturkontrakt.Ukjent}}" — ikke gjett;
              medlemskap = aktør/klasse→klasse; sammensetning_omrade = område→område; ansvarsomrade = aktør→område;
              konstituerende = oppretter/avvikler/skal_finnes («Hver kommune skal ha …»);
              organsammensetning = har_medlemmer (organets FASTE medlemmer: antall, hvem oppnevner) / har_organ (rettssubjekt→organ)
                / settes_med (sammensetningen i den ENKELTE SAK: antall i "objekt", sakstypen i "avgrensning").
            - "fra", "til": aktør-id fra "aktorer" i DETTE svaret, eller null når teksten ikke avgjør aktøren
            - "objekt": for kompetanse: bestemmelsen/sakstypen/regelverket (f.eks. "vedtak etter § 3-1")
            - "normform": bare på normgivningskompetanse — en av {{string.Join(", ", Strukturkontrakt.Normformer)}}, når teksten sier det
            - "undertype": {{Undertypeliste()}} — når teksten sier det, ellers utelat
            - "grunnlag": bare på kompetanse — "privatrettslig" når kompetansen følger av eierskap/selskapsrett, ellers utelat
            - "delegerbar": bare på kompetanse — false for «Kongen i statsråd …» og «X selv …», true for «Kongen …», ellers utelat
            - "polaritet": "positiv" eller "negativ" — ALLTID med («kan ikke instruere» = negativ)
            - "avgrensning": paragraf/sakstype/vilkår som begrenser utsagnet
            - "betinget": true/false
            - "kilde_utenfor_korpus": true hvis utsagnet viser til noe som fastsettes utenfor teksten (kgl.res.,
              vedtekter, «Kongen bestemmer»)
            - "sikkerhet": "hoy", "middels" eller "lav"
            - "kommentar": kort, bare når det trengs

            LEKSIKONET (versjon {{Kompetanseleksikon.Versjon}}): utsagn som uttrykkes med formuleringene under, finner
            et deterministisk mønsterlag — IKKE annoter dem. Foreslå bare for uttrykk som IKKE står her:
            {{Leksikonliste()}}

            REGLER:
            - Ingen gjetting. Kan aktøren ikke avgjøres fra teksten, sett fra/til til null. Dikt ikke opp aktører,
              noder eller sitater som ikke står i inndataene.
            - Usikkerhet er data: bruk "sikkerhet": "lav" heller enn å utelate utsagnet.
            - Utelat felt som er null. Returner {"aktorer": [], "utsagn": []} hvis teksten ikke sier noe om struktur.
            """;
    }

    // ---------------------------------------------------------------------------------------------
    // Oppdeling i kall
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Ett element i en del: en overskriftslinje (<see cref="Node"/> null — ikke siterbar) eller en node
    /// med tekst og tagg.
    /// </summary>
    internal sealed record Element(string Linje, Strukturnode? Node, string? Tagg);

    /// <summary>Én del = ett KI-kall: kildens tittel, en ev. overført overskrift, og elementene.</summary>
    internal sealed record Del(string Rettskilde, string? Innledning, IReadOnlyList<Element> Elementer)
    {
        public IEnumerable<Element> Taggede => Elementer.Where(e => e.Tagg is not null);

        /// <summary>Konteksten som sendes til KI-en.</summary>
        public string Kontekst()
        {
            var sb = new StringBuilder("# ").AppendLine(Rettskilde);
            if (Innledning is not null) sb.AppendLine(Innledning);
            foreach (var e in Elementer) sb.AppendLine(e.Linje);
            return sb.ToString();
        }

        /// <summary>Deler i to ved den midterste noden med tekst. Andre halvdel arver siste overskrift før delingen.</summary>
        public (Del Forste, Del Andre)? Halver()
        {
            var taggIndekser = Elementer.Select((e, i) => (e, i)).Where(x => x.e.Tagg is not null).Select(x => x.i).ToList();
            if (taggIndekser.Count < 2) return null;
            var delepunkt = taggIndekser[taggIndekser.Count / 2];
            // Overskrifter rett foran delepunktet hører til andre halvdel.
            while (delepunkt > 0 && Elementer[delepunkt - 1].Node is null) delepunkt--;
            var forste = Elementer.Take(delepunkt).ToList();
            var andre = Elementer.Skip(delepunkt).ToList();
            var arvet = forste.LastOrDefault(e => e.Node is null)?.Linje ?? Innledning;
            return (this with { Elementer = forste }, new Del(Rettskilde, arvet, andre));
        }
    }

    /// <summary>
    /// Deler grunnlaget i kall: grådig node for node i dokumentrekkefølge, aldri midt i en node, ny del
    /// når neste node ville sprengt budsjettet eller når en ledsagende kilde begynner. Samme prinsipp som
    /// <c>RettskildeKontekstHjelper.ByggKontekstChunketAsync</c> (#295), her over nodene i minnet i stedet
    /// for databasen. En enkelt node over budsjettet får egen del uten å kuttes (myk grense).
    /// </summary>
    internal static IReadOnlyList<Del> DelOpp(Strukturkonverteringsgrunnlag grunnlag, int maksTegn)
    {
        var deler = new List<Del>();
        var elementer = new List<Element>();
        string? rettskilde = null, eli = null, innledning = null, sisteOverskrift = null;
        var tegn = 0;
        var nr = 0;

        void Lukk()
        {
            if (elementer.Any(e => e.Tagg is not null)) deler.Add(new Del(rettskilde!, innledning, elementer.ToList()));
            elementer.Clear();
            tegn = 0;
            innledning = sisteOverskrift;
        }

        foreach (var node in grunnlag.Noder)
        {
            if (eli is not null && !string.Equals(node.Eli, eli, StringComparison.Ordinal))
            {
                Lukk();
                innledning = null;
                sisteOverskrift = null;
            }
            rettskilde = node.Rettskilde;
            eli = node.Eli;

            var harTekst = !string.IsNullOrWhiteSpace(node.Tekst);
            var overskrift = Overskriftslinje(node);
            var linje = harTekst ? $"[n{nr + 1}] {node.Tekst}" : null;
            var lengde = (overskrift?.Length ?? 0) + (linje?.Length ?? 0);
            if (tegn > 0 && tegn + lengde > maksTegn && elementer.Any(e => e.Tagg is not null)) Lukk();

            if (overskrift is not null)
            {
                elementer.Add(new Element(overskrift, null, null));
                sisteOverskrift = overskrift;
            }
            if (linje is not null)
            {
                nr++;
                elementer.Add(new Element(linje, node, "n" + nr));
            }
            tegn += lengde;
        }
        Lukk();
        return deler;
    }

    /// <summary>
    /// Overskriftslinje for en node som har overskrift eller er en strukturnode uten tekst («## §1-1
    /// Lovens formål»). Den lokale delen av eId-en tas med, så paragrafnummeret er synlig for KI-en.
    /// </summary>
    private static string? Overskriftslinje(Strukturnode node)
    {
        var harTekst = !string.IsNullOrWhiteSpace(node.Tekst);
        if (harTekst && string.IsNullOrWhiteSpace(node.Overskrift)) return null;
        var lokal = node.Eid.StartsWith(node.Eli + "/", StringComparison.Ordinal) ? node.Eid[(node.Eli.Length + 1)..] : node.Eid;
        return string.IsNullOrWhiteSpace(node.Overskrift) ? $"## {lokal}" : $"## {lokal} {node.Overskrift}";
    }

    // ---------------------------------------------------------------------------------------------
    // Kall og validering
    // ---------------------------------------------------------------------------------------------

    private sealed record Delresultat(
        IReadOnlyList<KiRad> Rader, IReadOnlyList<KastetRad> Kastet, IReadOnlyDictionary<string, int> KastedeAktorer,
        int AntallKall, int UgyldigJson, int FeiledeKall, int TaptNoder, int? InputTokens, int? OutputTokens,
        IReadOnlyList<string> Kallfeil);

    private async Task<Delresultat> KjorDelAsync(Del del, int dybde, CancellationToken ct)
    {
        KiSvar svar;
        try
        {
            svar = await kiKlient.GenererAsync(SystemInstruks, del.Kontekst(), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            // Et feilet kall (HTTP-feil, timeout etter klientens eget retry) stopper ikke resten av
            // kilden, men telles og navngis — nodene i delen er tapt for denne kjøringen, synlig.
            var noder = del.Taggede.Count();
            _logger.LogWarning(ex, "KI-strukturkonvertering: kallet for {Noder} noder feilet.", noder);
            return new Delresultat([], [], new Dictionary<string, int>(), 1, 0, 1, noder, null, null,
                [$"Kall feilet ({noder} noder, første {del.Taggede.First().Node!.Eid}): {Kort(ex.Message)}"]);
        }

        var tolket = Tolk(svar.Innhold, del);
        if (tolket is null)
        {
            var noder = del.Taggede.Count();
            _logger.LogWarning("KI-strukturkonvertering: ugyldig JSON for {Noder} noder (dybde {Dybde}). Rå respons (start): {Svar}",
                noder, dybde, Kort(svar.Innhold));
            var feil = $"Ugyldig JSON ({noder} noder, dybde {dybde}, {svar.OutputTokens?.ToString() ?? "?"} tokens ut): {Kort(svar.Innhold)}";
            var halvdeler = dybde < _valg.MaksDelingsdybde ? del.Halver() : null;
            if (halvdeler is null)
            {
                return new Delresultat([], [], new Dictionary<string, int>(), 1, 1, 0, noder, svar.InputTokens, svar.OutputTokens, [feil]);
            }
            var a = await KjorDelAsync(halvdeler.Value.Forste, dybde + 1, ct);
            var b = await KjorDelAsync(halvdeler.Value.Andre, dybde + 1, ct);
            return new Delresultat(
                [.. a.Rader, .. b.Rader], [.. a.Kastet, .. b.Kastet], Slå(a.KastedeAktorer, b.KastedeAktorer),
                1 + a.AntallKall + b.AntallKall, 1 + a.UgyldigJson + b.UgyldigJson, a.FeiledeKall + b.FeiledeKall,
                a.TaptNoder + b.TaptNoder, Sum(svar.InputTokens, a.InputTokens, b.InputTokens),
                Sum(svar.OutputTokens, a.OutputTokens, b.OutputTokens), [feil, .. a.Kallfeil, .. b.Kallfeil]);
        }

        return new Delresultat(tolket.Value.Rader, tolket.Value.Kastet, tolket.Value.KastedeAktorer,
            1, 0, 0, 0, svar.InputTokens, svar.OutputTokens, []);
    }

    /// <summary>
    /// Tolker og validerer ett svar mot delen det svarer på. Null = svaret er ikke et JSON-objekt med en
    /// <c>utsagn</c>-liste (hele kallet er ugyldig). Ellers: gyldige rader + kastede rader med årsak.
    /// </summary>
    internal static (List<KiRad> Rader, List<KastetRad> Kastet, Dictionary<string, int> KastedeAktorer)? Tolk(string innhold, Del del)
    {
        JsonDocument dok;
        try
        {
            dok = JsonDocument.Parse(JsonSvarHjelper.StrimleKodeblokk(innhold).Trim());
        }
        catch (JsonException)
        {
            return null;
        }

        using (dok)
        {
            var rot = dok.RootElement;
            if (rot.ValueKind != JsonValueKind.Object
                || !rot.TryGetProperty("utsagn", out var utsagnListe) || utsagnListe.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var noder = del.Taggede.ToDictionary(e => e.Tagg!, e => e.Node!, StringComparer.Ordinal);
            var deltekst = string.Join("\n", del.Elementer.Select(e => e.Linje));
            if (del.Innledning is not null) deltekst = del.Innledning + "\n" + deltekst;

            var kastedeAktorer = new Dictionary<string, int>(StringComparer.Ordinal);
            var aktorer = new Dictionary<string, (KiAktor? Aktor, string? Feil)>(StringComparer.Ordinal);
            if (rot.TryGetProperty("aktorer", out var aktorListe) && aktorListe.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in aktorListe.EnumerateArray())
                {
                    var (id, aktor, feil) = TolkAktor(a, deltekst);
                    if (id is null)
                    {
                        Tell(kastedeAktorer, "aktør uten id");
                        continue;
                    }
                    if (aktorer.ContainsKey(id))
                    {
                        aktorer[id] = (null, "duplisert aktør-id");
                        Tell(kastedeAktorer, "duplisert aktør-id");
                        continue;
                    }
                    aktorer[id] = (aktor, feil);
                    if (feil is not null) Tell(kastedeAktorer, feil);
                }
            }

            var rader = new List<KiRad>();
            var kastet = new List<KastetRad>();
            foreach (var u in utsagnListe.EnumerateArray())
            {
                var (rad, kastetRad) = TolkUtsagn(u, noder, aktorer, del);
                if (rad is not null) rader.Add(rad);
                if (kastetRad is not null) kastet.Add(kastetRad);
            }
            return (rader, kastet, kastedeAktorer);
        }
    }

    private static (string? Id, KiAktor? Aktor, string? Feil) TolkAktor(JsonElement a, string deltekst)
    {
        if (a.ValueKind != JsonValueKind.Object) return (null, null, null);
        if (!Streng(a, "id", out var id) || string.IsNullOrWhiteSpace(id)) return (null, null, null);

        if (!Streng(a, "tekstform", out var tekstform) || string.IsNullOrWhiteSpace(tekstform))
            return (id, null, "tekstform mangler");
        if (!StrengListe(a, "varianter", out var varianter)) return (id, null, "ugyldig felt");
        if (!Streng(a, "entitetstype", out var entitetstype)
            || !Bool(a, "navngitt", out var navngitt)
            || !Streng(a, "referent", out var referent)
            || !Streng(a, "oppløsning", out var opplosning)
            || !Bool(a, "distributiv", out var distributiv)
            || !Streng(a, "kommentar", out var kommentar))
            return (id, null, "ugyldig felt");

        if (deltekst.IndexOf(tekstform, StringComparison.OrdinalIgnoreCase) < 0) return (id, null, "tekstform står ikke i teksten");
        if (varianter.Any(v => deltekst.IndexOf(v, StringComparison.OrdinalIgnoreCase) < 0)) return (id, null, "variant står ikke i teksten");
        if (!Strukturkontrakt.ErGyldigEntitetstype(entitetstype)) return (id, null, "ukjent entitetstype");
        if (opplosning is not null && !Strukturkontrakt.Opplosninger.Contains(opplosning)) return (id, null, "ukjent oppløsning");

        return (id, new KiAktor(tekstform, varianter, entitetstype, navngitt, referent, opplosning, distributiv, kommentar), null);
    }

    private static (KiRad? Rad, KastetRad? Kastet) TolkUtsagn(
        JsonElement u, IReadOnlyDictionary<string, Strukturnode> noder,
        IReadOnlyDictionary<string, (KiAktor? Aktor, string? Feil)> aktorer, Del del)
    {
        if (u.ValueKind != JsonValueKind.Object)
            return (null, new KastetRad(KastetArsak.UgyldigFelt, null, null, null, null, "Utsagnet er ikke et JSON-objekt."));

        var feltOk = Streng(u, "eid", out var tagg) & Streng(u, "sitat", out var sitat)
            & Streng(u, "kategori", out var kategori) & Streng(u, "type", out var type)
            & Streng(u, "fra", out var fra) & Streng(u, "til", out var til) & Streng(u, "objekt", out var objekt)
            & Streng(u, "polaritet", out var polaritet) & Streng(u, "avgrensning", out var avgrensning)
            & Bool(u, "betinget", out var betinget) & Bool(u, "kilde_utenfor_korpus", out var utenfor)
            & Streng(u, "sikkerhet", out var sikkerhet) & Streng(u, "kommentar", out var kommentar)
            & Streng(u, "normform", out var normform) & Streng(u, "grunnlag", out var grunnlag) & Bool(u, "delegerbar", out var delegerbar)
            & Streng(u, "undertype", out var undertype);

        KastetRad Kast(string arsak, string detalj) =>
            new(arsak, tagg is not null && noder.TryGetValue(tagg, out var n) ? n.Eid : tagg, sitat, kategori, type, detalj);

        if (!feltOk) return (null, Kast(KastetArsak.UgyldigFelt, "Et felt har feil JSON-type."));
        if (string.IsNullOrEmpty(tagg) || string.IsNullOrEmpty(sitat) || kategori is null || type is null)
            return (null, Kast(KastetArsak.UgyldigFelt, "eid, sitat, kategori eller type mangler."));
        if (!noder.TryGetValue(tagg, out var node))
            return (null, Kast(KastetArsak.UkjentEid, $"Taggen «{tagg}» står ikke i dette kallets inndata."));
        if (!node.Tekst!.Contains(sitat, StringComparison.Ordinal))
            return (null, Kast(KastetArsak.FalsktSitat, SitatDiagnose(sitat, node, del)));
        if (!Strukturkontrakt.ErGyldigKategori(kategori))
            return (null, Kast(KastetArsak.UkjentKategori, $"Kategorien «{kategori}» står ikke i FORMAT.md."));
        if (!Strukturkontrakt.ErGyldigType(kategori, type))
            return (null, Kast(KastetArsak.UkjentType, $"Typen «{type}» står ikke i FORMAT.md-lista for «{kategori}»."));

        KiAktor? fraAktor = null, tilAktor = null;
        foreach (var (rolle, id) in new[] { ("fra", fra), ("til", til) })
        {
            if (id is null) continue;
            if (!aktorer.TryGetValue(id, out var oppslag))
                return (null, Kast(KastetArsak.UkjentAktorreferanse, $"{rolle} = «{id}» finnes ikke i svarets aktorer."));
            if (oppslag.Aktor is null)
                return (null, Kast(KastetArsak.UgyldigAktor, $"{rolle} = «{id}»: {oppslag.Feil}."));
            if (rolle == "fra") fraAktor = oppslag.Aktor; else tilAktor = oppslag.Aktor;
        }

        if (polaritet is null || !Strukturkontrakt.Polariteter.Contains(polaritet))
            return (null, Kast(KastetArsak.UgyldigFelt, $"polaritet = «{polaritet ?? "null"}»."));
        if (sikkerhet is not null && !Strukturkontrakt.Sikkerheter.Contains(sikkerhet))
            return (null, Kast(KastetArsak.UgyldigFelt, $"sikkerhet = «{sikkerhet}»."));
        // [Ny, issue #341] Kompetansefeltene: lukkede lister, og bare der de hører hjemme (FORMAT.md).
        if (normform is not null && (type != "normgivningskompetanse" || !Strukturkontrakt.Normformer.Contains(normform)))
            return (null, Kast(KastetArsak.UgyldigFelt, $"normform = «{normform}» (bare på normgivningskompetanse)."));
        if (grunnlag is not null && (kategori != "kompetanse" || !Strukturkontrakt.Grunnlag.Contains(grunnlag)))
            return (null, Kast(KastetArsak.UgyldigFelt, $"grunnlag = «{grunnlag}» (bare på kompetanse)."));
        if (delegerbar is not null && kategori != "kompetanse")
            return (null, Kast(KastetArsak.UgyldigFelt, "delegerbar finnes bare på kompetanse."));
        // [Ny, issue #352] Undertypen: bare en undertype FORMAT.md-typen har (oppnevning: valg/ansettelse/utpeking/oppnevning;
        // overprøving: anke).
        if (undertype is not null && (kategori != "kompetanse" || !Strukturkontrakt.ErGyldigUndertype(type, undertype)))
            return (null, Kast(KastetArsak.UgyldigFelt, $"undertype = «{undertype}» (bare på oppnevnings-/overprøvingskompetanse, lukket liste)."));

        return (new KiRad(node.Eid, sitat, kategori, type, fraAktor, tilAktor, objekt, polaritet, avgrensning,
            betinget, utenfor, sikkerhet, kommentar, normform, grunnlag, delegerbar, undertype), null);
    }

    /// <summary>
    /// Hvorfor sitatet ikke er eksakt — bare til rapporten, aldri til å godta raden. Skiller «står i en
    /// annen node i samme kall» (feil tagg), «ville passet med normalisert mellomrom/store-små bokstaver»
    /// (nesten ordrett) og «står ikke i kallets tekst» (parafrase eller oppdiktet).
    /// </summary>
    private static string SitatDiagnose(string sitat, Strukturnode node, Del del)
    {
        if (del.Taggede.Any(e => !ReferenceEquals(e.Node, node) && e.Node!.Tekst!.Contains(sitat, StringComparison.Ordinal)))
            return SitatDiagnoser.AnnenNode;
        if (Normaliser(node.Tekst!).Contains(Normaliser(sitat), StringComparison.Ordinal))
            return SitatDiagnoser.NestenOrdrett;
        return SitatDiagnoser.IkkeITeksten;
    }

    private static readonly Regex Mellomrom = new(@"\s+", RegexOptions.CultureInvariant);

    private static string Normaliser(string s) => Mellomrom.Replace(s, " ").Trim().ToLowerInvariant();

    // --- JSON-hjelpere: «fraværende eller null» er gyldig (verdi null); feil JSON-type er ugyldig. ---

    private static bool Streng(JsonElement o, string navn, out string? verdi)
    {
        verdi = null;
        if (!o.TryGetProperty(navn, out var v) || v.ValueKind == JsonValueKind.Null) return true;
        if (v.ValueKind != JsonValueKind.String) return false;
        verdi = v.GetString();
        return true;
    }

    private static bool Bool(JsonElement o, string navn, out bool? verdi)
    {
        verdi = null;
        if (!o.TryGetProperty(navn, out var v) || v.ValueKind == JsonValueKind.Null) return true;
        if (v.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        verdi = v.GetBoolean();
        return true;
    }

    private static bool StrengListe(JsonElement o, string navn, out List<string> verdi)
    {
        verdi = [];
        if (!o.TryGetProperty(navn, out var v) || v.ValueKind == JsonValueKind.Null) return true;
        if (v.ValueKind != JsonValueKind.Array) return false;
        foreach (var e in v.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(e.GetString())) return false;
            verdi.Add(e.GetString()!);
        }
        return true;
    }

    private static void Tell(Dictionary<string, int> d, string nokkel) => d[nokkel] = d.GetValueOrDefault(nokkel) + 1;

    private static Dictionary<string, int> Slå(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b)
    {
        var r = new Dictionary<string, int>(a, StringComparer.Ordinal);
        foreach (var (k, v) in b) r[k] = r.GetValueOrDefault(k) + v;
        return r;
    }

    private static int? Sum(params int?[] verdier) =>
        verdier.All(v => v is null) ? null : verdier.Sum(v => v ?? 0);

    private static string Kort(string s)
    {
        var t = s.Replace("\r", " ").Replace("\n", " ");
        return t.Length > 300 ? t[..300] + " …" : t;
    }

    // ---------------------------------------------------------------------------------------------
    // Aktører på tvers av kall
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Samler aktørene fra alle kall: én per distinkt tekstform uten skille på store/små bokstaver (som
    /// målingens treffregel og <see cref="MonsterStrukturkonverterer"/>). Varianter slås sammen. Gir to
    /// kall ULIKE verdier for entitetstype/navngitt/referent/oppløsning/distributiv for samme tekstform,
    /// blir feltet null med en kommentar — å velge én av dem ville vært gjetting (CLAUDE.md §8).
    /// </summary>
    private sealed class AktorBygger
    {
        private readonly Dictionary<string, Oppforing> _perNokkel = new(StringComparer.Ordinal);
        private readonly List<Oppforing> _rekkefolge = [];

        public string Registrer(KiAktor aktor, string eid)
        {
            var nokkel = aktor.Tekstform.ToLowerInvariant();
            if (!_perNokkel.TryGetValue(nokkel, out var o))
            {
                o = new Oppforing("a" + (_rekkefolge.Count + 1), aktor);
                _perNokkel[nokkel] = o;
                _rekkefolge.Add(o);
            }
            else
            {
                o.SlåSammen(aktor);
            }
            o.Antall++;
            if (o.Eider.Count < MaksEidEksempler && !o.Eider.Contains(eid)) o.Eider.Add(eid);
            return o.Id;
        }

        public IReadOnlyList<StrukturAktor> Bygg() => _rekkefolge
            .Select(o => new StrukturAktor(
                Id: o.Id,
                Tekstform: o.Tekstform,
                Varianter: o.Varianter,
                EidEksempler: o.Eider,
                AntallForekomster: o.Antall,
                Entitetstype: o.Entitetstype.Verdi,
                Navngitt: o.Navngitt.Verdi,
                Referent: o.Referent.Verdi,
                Opplosning: o.Opplosning.Verdi,
                Distributiv: o.Distributiv.Verdi,
                Kommentar: o.Konflikter.Count == 0 && o.Kommentar is null
                    ? null
                    : string.Join(" ", new[] { o.Kommentar }.Concat(o.Konflikter.Select(k => $"Motstridende {k} fra ulike KI-kall — satt til null.")).Where(s => s is not null))))
            .ToList();

        private sealed class Felt<T>(T? forste)
        {
            private bool _konflikt;
            public T? Verdi => _konflikt ? default : forste;

            /// <summary>Returnerer true første gang to ikke-null-verdier er ulike.</summary>
            public bool SlåSammen(T? annen)
            {
                if (_konflikt || annen is null) return false;
                if (forste is null) { forste = annen; return false; }
                if (EqualityComparer<T>.Default.Equals(forste, annen)) return false;
                _konflikt = true;
                return true;
            }
        }

        private sealed class Oppforing
        {
            public Oppforing(string id, KiAktor a)
            {
                Id = id;
                Tekstform = a.Tekstform;
                Entitetstype = new Felt<string>(a.Entitetstype);
                Navngitt = new Felt<bool?>(a.Navngitt);
                Referent = new Felt<string>(a.Referent);
                Opplosning = new Felt<string>(a.Opplosning);
                Distributiv = new Felt<bool?>(a.Distributiv);
                Kommentar = a.Kommentar;
                foreach (var v in a.Varianter) LeggTilVariant(v);
            }

            public string Id { get; }
            public string Tekstform { get; }
            public List<string> Varianter { get; } = [];
            public List<string> Eider { get; } = [];
            public List<string> Konflikter { get; } = [];
            public int Antall { get; set; }
            public string? Kommentar { get; }
            public Felt<string> Entitetstype { get; }
            public Felt<bool?> Navngitt { get; }
            public Felt<string> Referent { get; }
            public Felt<string> Opplosning { get; }
            public Felt<bool?> Distributiv { get; }

            public void SlåSammen(KiAktor a)
            {
                if (!string.Equals(a.Tekstform, Tekstform, StringComparison.Ordinal)) LeggTilVariant(a.Tekstform);
                foreach (var v in a.Varianter) LeggTilVariant(v);
                if (Entitetstype.SlåSammen(a.Entitetstype)) Konflikter.Add("entitetstype");
                if (Navngitt.SlåSammen(a.Navngitt)) Konflikter.Add("navngitt");
                if (Referent.SlåSammen(a.Referent)) Konflikter.Add("referent");
                if (Opplosning.SlåSammen(a.Opplosning)) Konflikter.Add("oppløsning");
                if (Distributiv.SlåSammen(a.Distributiv)) Konflikter.Add("distributiv");
            }

            private void LeggTilVariant(string variant)
            {
                if (!string.Equals(variant, Tekstform, StringComparison.Ordinal) && !Varianter.Contains(variant)) Varianter.Add(variant);
            }
        }
    }
}

/// <summary>[Ny, #308] Innstillinger for <see cref="KiStrukturkonverterer"/>.</summary>
/// <param name="MaksTegnPerKall">
/// Myk grense for nodetekst per kall. 6000 er lavere enn de 15 000 i <c>VirksomhetOgGruppeKiOppdagelseTjeneste</c>
/// fordi SVARET her er langt (ett utsagn per relasjon, ofte flere per setning): en inndelingsforskrift
/// kan gi over hundre utsagn per 6000 tegn. Valgt for å holde utdata under en antatt standard
/// utdatagrense hos leverandøren — ikke målt; ugyldig (avkuttet) JSON halverer kallet uansett.
/// </param>
/// <param name="MaksParallelleKall">Samtidige kall. 1 som standard; live-målingen bruker flere.</param>
/// <param name="MaksDelingsdybde">Hvor mange ganger et kall med ugyldig JSON kan halveres og prøves på nytt.</param>
public sealed record KiStrukturkonverteringsvalg(int MaksTegnPerKall = 6000, int MaksParallelleKall = 1, int MaksDelingsdybde = 2);

/// <summary>[Ny, #308] Resultatet av én KI-konvertering, med det som trengs for å etterprøve den.</summary>
/// <param name="AntallDeler">Antall deler grunnlaget ble delt i (før ev. halvering).</param>
/// <param name="AntallKall">Faktiske KI-kall, inkludert halveringer.</param>
/// <param name="UgyldigJson">Kall der svaret ikke var et JSON-objekt med <c>utsagn</c>.</param>
/// <param name="FeiledeKall">Kall som feilet (HTTP/timeout) etter klientens eget retry.</param>
/// <param name="TaptNoder">Noder som aldri fikk et gyldig svar (feilet kall, eller ugyldig JSON på bunnivå).</param>
/// <param name="KastedeAktorer">Aktører i svarene som ble avvist, per årsak.</param>
public sealed record KiStrukturkonverteringsresultat(
    Strukturdokument Dokument,
    string Modell,
    int AntallDeler,
    int AntallKall,
    int UgyldigJson,
    int FeiledeKall,
    int TaptNoder,
    int? InputTokens,
    int? OutputTokens,
    IReadOnlyList<KastetRad> Kastet,
    IReadOnlyDictionary<string, int> KastedeAktorer,
    IReadOnlyList<string> Kallfeil);

/// <summary>[Ny, #308] Én rad fra KI-svaret som ble kastet i valideringen, med årsak.</summary>
/// <param name="Eid">Nodens ekte eId når taggen fantes, ellers taggen slik KI-en skrev den.</param>
/// <param name="Detalj">For <see cref="KastetArsak.FalsktSitat"/>: en av <see cref="SitatDiagnoser"/>.</param>
public sealed record KastetRad(string Arsak, string? Eid, string? Sitat, string? Kategori, string? Type, string Detalj);

/// <summary>[Ny, #308] Årsakskodene for kastede rader. Første regel som slår til, i denne rekkefølgen.</summary>
public static class KastetArsak
{
    public const string UgyldigFelt = "ugyldig_felt";
    public const string UkjentEid = "ukjent_eid";
    public const string FalsktSitat = "falskt_sitat";
    public const string UkjentKategori = "ukjent_kategori";
    public const string UkjentType = "ukjent_type";
    public const string UkjentAktorreferanse = "ukjent_aktorreferanse";
    public const string UgyldigAktor = "ugyldig_aktor";
    public const string Duplikat = "duplikat";

    public static IReadOnlyList<string> Rekkefolge { get; } =
        [UgyldigFelt, UkjentEid, FalsktSitat, UkjentKategori, UkjentType, UkjentAktorreferanse, UgyldigAktor, Duplikat];
}

/// <summary>[Ny, #308] Underinndeling av <see cref="KastetArsak.FalsktSitat"/> — bare diagnose, raden kastes uansett.</summary>
public static class SitatDiagnoser
{
    public const string AnnenNode = "står ordrett i en annen node i samme kall (feil tagg)";
    public const string NestenOrdrett = "ville passet med normalisert mellomrom/store-små bokstaver";
    public const string IkkeITeksten = "står ikke i noden (parafrase eller oppdiktet)";
}

/// <summary>Én validert rad før aktørene er samlet på tvers av kall.</summary>
internal sealed record KiRad(
    string Eid, string Sitat, string Kategori, string Type, KiAktor? Fra, KiAktor? Til, string? Objekt, string Polaritet,
    string? Avgrensning, bool? Betinget, bool? KildeUtenforKorpus, string? Sikkerhet, string? Kommentar,
    // [Ny, issue #341] Bare på kompetanse (normform bare på normgivning) — validert i TolkUtsagn.
    string? Normform = null, string? Grunnlag = null, bool? Delegerbar = null,
    // [Ny, issue #352] Bare på oppnevnings-/overprøvingskompetanse — validert i TolkUtsagn.
    string? Undertype = null);

/// <summary>Én validert aktør fra ett KI-svar.</summary>
internal sealed record KiAktor(
    string Tekstform, IReadOnlyList<string> Varianter, string? Entitetstype, bool? Navngitt, string? Referent,
    string? Opplosning, bool? Distributiv, string? Kommentar);
