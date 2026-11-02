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

    // ---- [Ny, #429 PR 3, Johanns avgjørelser 2026-11-02 etter måling 2] Reglene fra juristens jurist429b/regler_b.py, portert ordrett ----
    /// <summary>R2b: datert vedtaksattribusjon i fortellende form — «X har 23. mars 1988 bestemt/vedtatt/fastsatt/besluttet». [LÅST, #429
    /// N19, Johann 2026-11-02: juristen har rett] Bare når termen står ÉN gang i noden: står den flere ganger, er det mer enn forfatteren av
    /// vedtaket.</summary>
    public const string R2b = "R2b";
    /// <summary>R5: endringsingress — noden er «I vedtak/forskrift … gjøres (det) følgende endring(er):», og termen står i det som siteres
    /// (tittelen på målvedtaket slik den lød da). 13 maskinfunn i måling 2.</summary>
    public const string R5 = "R5";
    /// <summary>R6: overføringsbestemmelse (overgangsregel) — termen, en etterfølger fra modellen (for gruppe A: aktøren selv) og et
    /// overføringsverb («overtas», «behandles av», «overføres til» …) i samme setning. Navnet skal stå; det er selve overføringen (N07).</summary>
    public const string R6 = "R6";
    /// <summary>R7: duplikat — noden står ordrett i målvedtaket, målvedtaket er konsolidert med rettskilden (<c>sist_endret_ved</c> peker
    /// på den) og har eget funn for samme aktør (N14). Krever data utenfor noden og settes derfor av <see cref="UtgattAktorFunnregel"/>.</summary>
    public const string R7 = "R7";
    /// <summary>R8: rettskilden er opphevet av en annen gjeldende primærkilde i korpus (<see cref="Opphevingskontroll"/>). Gjelder hele
    /// rettskilden: den går til kildefeilkøen og gir ingen funn.</summary>
    public const string R8 = "R8";
    /// <summary>R9: hjemmelsloven er opphevet av en gjeldende lov i korpus. Undertrykker IKKE: funnet får en synlig premissmangel
    /// (<see cref="FunnTjeneste.PremissmanglerForAsync"/>).</summary>
    public const string R9 = "R9";

    /// <summary>Reglene som holder et par tilbake (teller, foreslår ikke). R9 er ikke med: den holder ingenting tilbake.</summary>
    public static readonly string[] Alle = [R1, R2, R2b, R3, R4, R5, R6, R7, R8];

    private const string Maaned = "(januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember)";
    private const string Dato = @"(den\s+)?\d{1,2}\.\s*" + Maaned + @"\s+\d{4}";

    public static string Normaliser(string? tekst) => Regex.Replace(tekst ?? "", "[\n\t]", " ");

    private static string[] Setninger(string tekst) => Regex.Split(tekst, @"(?<=[.;])\s+");

    /// <summary>[Ny, #429 PR 3] Juristens rettede setningsdeling (<c>setn</c> i regler_b.py), brukt av R3 og R6. Portert ordrett.
    /// Merk: lookbehindene står ved posisjonen ETTER skilletegnet, så det er lookaheaden (stor bokstav etter mellomrommet) som gjør at
    /// «2. august» og «nr. 1286» ikke deles. Det er verifisert i testene, ikke antatt. R1 bruker fortsatt den gamle delingen: rettingen gjaldt
    /// R3 (N20), og R1 er låst fra måling 1.</summary>
    private static string[] SetningerRettet(string tekst) =>
        Regex.Split(tekst, @"(?<![\d])(?<!\bnr)(?<!\bjf)(?<!\bjfr)(?<!\bpkt)(?<!\bm\.v)(?<!\bbl\.a)(?<=[.;])\s+(?=[A-ZÆØÅ])");

    private const string Overforing = @"\b(overtas|overtar|behandles\s+av|overføres\s+til|går\s+over\s+til|reises\s+mot)\b";

    /// <summary>Reglene som slår til for <paramref name="term"/> i <paramref name="tekst"/> (allerede normalisert), med forelderens tekst
    /// for R3 og etterfølgernavnene for R6. Tom liste = ingen regel; forekomsten teller som gjeldende omtale. R7 og R8 settes av regelen.</summary>
    public static IReadOnlyList<string> Treff(string term, string tekst, string? forelder, IReadOnlyCollection<string>? etterfolgere = null)
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
            // [ENDRET, #429 PR 3, N20] To rettinger fra juristen: (1) «av Xs» er genitiv, ikke agent — «oppheves vedtak … om delegering av
            // Sosial- og helsedepartementets myndighet» er en opphevingsformel for X. Agentunntaket er derfor «av X» som IKKE følges av
            // genitiv-s. (2) Setningsdelingen deler ikke ved «2. august» (SetningerRettet). Ellers uendret, også «oppheves av X»-grensen.
            foreach (var s in SetningerRettet(tekst))
            {
                if (s.Contains(term, StringComparison.Ordinal) && Regex.IsMatch(s, @"\boppheves\b") && !Regex.IsMatch(s, @"\bav\s+" + t + @"(?!s\b)")
                    && (Regex.IsMatch(s, t + @"s\b") || Regex.IsMatch(s, @"\btil\s+(\S+\s+){0,6}?" + t)))
                {
                    h.Add(R3);
                    break;
                }
            }
        }

        if (Regex.IsMatch(tekst, @"\(?\btidligere\)?\s+" + t)) h.Add(R4);

        // R2b — én forekomst (Pythons len(re.findall(term)) == 1: også «Xs» teller som en forekomst).
        if (Regex.Matches(tekst, t).Count == 1
            && Regex.IsMatch(tekst, t + @"\s+har\s+" + Dato + @"\s+(bestemt|vedtatt|fastsatt|besluttet)"))
        {
            h.Add(R2b);
        }

        // R5 — hele noden er ingressen, og termen står i det som siteres foran «gjøres følgende endring(er)».
        var ingress = Regex.Match(tekst, @"^\s*I\s+(.*)\bgjøres\s+(det\s+)?følgende\s+endring(er)?\s*:?\s*$");
        if (ingress.Success && ingress.Groups[1].Value.Contains(term, StringComparison.Ordinal)) h.Add(R5);

        // R6 — termen, en etterfølger og et overføringsverb i samme setning. Navnene sammenlignes uten hensyn til store og små bokstaver,
        // som i regler_b.py (registernavn kan stå i versaler).
        if (etterfolgere is { Count: > 0 } && SetningerRettet(tekst).Any(s => s.Contains(term, StringComparison.Ordinal)
                && Regex.IsMatch(s, Overforing)
                && etterfolgere.Any(a => s.Contains(a, StringComparison.OrdinalIgnoreCase))))
        {
            h.Add(R6);
        }
        return h;
    }

    /// <summary>[Ny, #429 PR 3, R7] Tekst normalisert som i regler_b.py før sammenligningen med målvedtaket: alt mellomrom blir ett.</summary>
    public static string NormaliserMellomrom(string? tekst) => Regex.Replace(tekst ?? "", @"\s+", " ").Trim();
}

/// <summary>Én forekomst: noden og reglene som slo til (tom = gjeldende omtale).</summary>
public sealed record UtgattAktorForekomst(string Eid, string Term, string QuoteExact, IReadOnlyList<string> Regler);

/// <summary>Ett par (rettskilde, aktør) i rapporten. [ENDRET, #429 PR 3] <c>Utfall</c>: <c>foreslatt</c> (gjenværende), <c>holdt_tilbake</c>
/// (alle forekomstene treffes av en tekstregel) eller <c>kildefeil</c> (R8). <c>Bevis</c> er det som står i begrunnelsen når et lastet funn
/// forklares bort. <c>Premissmangler</c> er R9.</summary>
public sealed record UtgattAktorRad(
    string? DokumentId, string Rettskilde, Guid VirksomhetId, string Aktor, string Gruppe, bool Foreslatt, string EksternNokkel,
    string? FinnesMedStatus, bool IEndringsvedtak, string? Malvedtak, IReadOnlyList<UtgattAktorForekomst> Forekomster,
    string Utfall = UtgattAktorFunnregel.UtfallForeslatt, string? Bevis = null, bool ForklaresBort = false, IReadOnlyList<string>? Premissmangler = null);

/// <summary>[Ny, #429 PR 3] Tellingen for én gruppe (A–D), i par (rettskilde, aktør). <c>Gjenvaerende</c> foreslås (også de med
/// premissmangel); <c>HoldtTilbake</c> treffes i alle forekomstene av R1–R7; <c>TilKildefeil</c> står i en kilde som er opphevet av en annen
/// (R8); <c>ForklaresBort</c> er lastede funn (status oppdaget) blant de to siste.</summary>
public sealed record UtgattAktorGruppetelling(int Gjenvaerende, int MedPremissmangel, int HoldtTilbake, int TilKildefeil, int ForklaresBort);

/// <summary>[Ny, #429 PR 3] Tellingen for én regel. <c>Noder</c>: forekomster regelen treffer. <c>Par</c>: par regelen er med på å holde
/// tilbake (R8: alle parene i kilden; R9: gjenværende par med premissmangel). <c>ForklaresBort</c>: lastede funn blant dem. Et par kan treffes
/// av flere regler, så summen er større enn antallet par.</summary>
public sealed record UtgattAktorRegeltelling(int Noder, int Par, int ForklaresBort);

/// <summary>
/// Rapporten fra én kjøring. Tallene er par (rettskilde, aktør) når ikke annet står. <c>Gruppe</c>: A navneendring (aktøren lever),
/// B nedlagt med hovedetterfølger dekket, C nedlagt med etterfølger men hovedetterfølger ukjent, D nedlagt uten etterfølger.
/// <para>[ENDRET, #429 PR 3] <c>PerGruppe</c> og <c>PerRegel</c> skiller det som blir forklart bort, det som går til kildefeilkøen og det som
/// får premissmangel (Johann 2026-11-02 (6)). De gamle feltene står for sammenligning med PR 2.</para>
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
    IReadOnlyList<UtgattAktorRad>? Rader,
    IReadOnlyDictionary<string, UtgattAktorGruppetelling> PerGruppe,
    IReadOnlyDictionary<string, UtgattAktorRegeltelling> PerRegel,
    int KildefeilRettskilder,
    int KildefeilNye,
    int ForklaresBort,
    int ForklartBortSatt,
    int HoldtTilbakeMenAlleredeVurdert,
    int Premissmangel,
    int ForeslattMalvedtakOpphevet,
    int Oppfriskes,
    int Oppfrisket);

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
/// <item><b>R1–R7</b> (<see cref="UtgattAktorRegler"/>) teller, men foreslår ikke. [ENDRET, #429 PR 3] R2b, R5, R6, R7 og rettingene i R3.</item>
/// <item>[Ny, #429 PR 3] <b>R8:</b> en kilde som er opphevet av en annen gjeldende primærkilde i korpus (<see cref="Opphevingskontroll"/>),
/// går til kildefeilkøen (<see cref="KildefeilTjeneste"/>, type <see cref="KildefeiltypeOpphevetAvAnnenKilde"/>) og gir ingen funn.</item>
/// <item>[Ny, #429 PR 3] <b>R9:</b> hjemmelsloven er opphevet av en gjeldende lov i korpus. Funnet foreslås med en synlig premissmangel
/// (<see cref="FunnTjeneste.PremissmanglerForAsync"/>). Valgt framfor kildefeilkøen etter juristens begrunnelse i måling 2 (N10, N12, N16,
/// N18): om et vedtak overlever at hjemmelsloven oppheves, avgjøres av overgangsregler i den nye loven (havne- og farvannsloven § 53 første
/// ledd, yrkestransportlova § 42, arbeidsmiljøloven § 20-2 første ledd). Det er et rettslig spørsmål for juristen, ikke en teknisk feil i
/// Lovdata-kopien, og observasjonen om navnet kan være riktig selv om hjemmelen er byttet.</item>
/// <item>[Ny, #429 PR 3] <b>Lastede funn som holdes tilbake</b> (R1–R8) settes maskinelt til <c>forklart_bort</c>, med regel og bevis i
/// begrunnelsen og vurdert-av-type <see cref="Funn.Maskinregel"/> (<see cref="FunnTjeneste.ForklarBortMaskineltAsync"/>). Bare status
/// <c>oppdaget</c>: et vurdert funn overstyres ikke, men telles.</item>
/// <item>[Ny, #429 PR 3] <b>Etterfølgerkjeden:</b> hintet følger etterfølgerkantene til levende aktører, sier fra når en etterfølger selv er
/// nedlagt, og når kjeden ender i en nedlagt aktør uten etterfølger. En aktør med livsstatus NULL og en avvikler-kant regnes ikke som
/// levende. Alle kantene i kjeden kobles på funnet, så premissporten krever hver av dem vurdert (svakeste premiss, docs/33 §4.6). Forslag
/// (maskinelt eller manuelt foreslått) leses som før. Lovdatas <c>ansvarlig_departement</c> står ved siden av hintet som sekundær kilde.</item>
/// <item><b><c>rent_endringsvedtak</c> NULL er ukjent</b>, ikke «ikke rent» (Johann 2026-10-31 (2)). Et funn i en rettskilde som endrer
/// andre vedtak, foreslås, men påstanden sier at det er et endringsvedtak, og målvedtaket kobles på (<see cref="Funn.Malvedtak"/>) når
/// rettskilden endrer nøyaktig ett dokument. Endrer den flere, står det at målvedtaket ikke er entydig — ingen gjetting. [ENDRET, #429 PR 3]
/// Om rettskilden endrer eller opphever målvedtaket, leses av teksten (<see cref="Opphevingskontroll.OpphevesAvKildenAsync"/>).</item>
/// <item><b>Forankring:</b> én <see cref="Funn.GjelderBestemmelse"/> per node, med ELI, eId og <see cref="FunnKoblingEntitet.QuoteExact"/>
/// — slik en tagg forankres. I tillegg berørt aktør, navneformene og etterfølgerkantene (<see cref="Funn.Etterfolger"/>).</item>
/// <item><b>Idempotent:</b> <see cref="FunnEntitet.EksternNokkel"/> er <c>utgatt-aktor:&lt;ELI uten prefiks&gt;:&lt;aktør-id&gt;</c>.
/// Finnes nøkkelen, opprettes ikke noe nytt — et funn som er forklart bort, gjenåpnes aldri, og en ny kjøring gir 0 nye.</item>
/// </list>
/// Tørrkjøring er standard (<c>skriv = false</c>): da skrives ingenting, og rapporten viser hva som ville blitt gjort.
/// </summary>
public sealed class UtgattAktorFunnregel(RegelIdeDbContext db, FunnTjeneste funnTjeneste)
{
    public const string OppdagelsesKilde = "monster:utgatt-aktor";
    public const string NokkelPrefiks = "utgatt-aktor:";
    public const int MinsteTermlengde = 8;
    private static readonly string[] Bladnoder = ["ledd", "punkt", "avslutning"];

    /// <summary>[Ny, #429 PR 3, R8] Kildefeiltypen for en kilde som står som gjeldende, men er opphevet av en annen gjeldende kilde i
    /// korpus (LTI-status ikke vedlikeholdt). Fri tekst i <see cref="KildefeilEntitet.Type"/>, som for de andre mekanismene.</summary>
    public const string KildefeiltypeOpphevetAvAnnenKilde = "opphevet_av_annen_kilde";

    public const string UtfallForeslatt = "foreslatt";
    public const string UtfallHoldtTilbake = "holdt_tilbake";
    public const string UtfallKildefeil = "kildefeil";

    public const string GruppeA = "A";
    public const string GruppeB = "B";
    public const string GruppeC = "C";
    public const string GruppeD = "D";
    public static readonly string[] Grupper = [GruppeA, GruppeB, GruppeC, GruppeD];

    private sealed class Nodetreff(Guid rettskildeId, Guid nodeId, string eid, int rekkefolge, string term, IReadOnlyList<Guid> begrepIder,
        Guid virksomhetId, string quote, string tekst, List<string> regler)
    {
        public Guid RettskildeId { get; } = rettskildeId;
        public Guid NodeId { get; } = nodeId;
        public string Eid { get; } = eid;
        public int Rekkefolge { get; } = rekkefolge;
        public string Term { get; } = term;
        public IReadOnlyList<Guid> BegrepIder { get; } = begrepIder;
        public Guid VirksomhetId { get; } = virksomhetId;
        public string Quote { get; } = quote;
        public string Tekst { get; } = tekst;
        public List<string> Regler { get; } = regler;
    }

    /// <summary>En etterfølgerkant: <c>Fra</c> er etterfølgeren, <c>Til</c> aktøren den etterfølger.</summary>
    private sealed record Kant(Guid Id, Guid Til, Guid? Fra);

    /// <summary>[Ny, #429 PR 3] Etterfølgerkjeden fra en aktør: alle kantene (direkte først), de levende aktørene kjeden når fram til, de
    /// nedlagte etterfølgerne den går gjennom, og de nedlagte som ikke har noen etterfølger (blindveier).</summary>
    private sealed record Kjede(IReadOnlyList<(Kant Kant, bool Direkte)> Kanter, IReadOnlyList<Guid> Levende, IReadOnlyList<Guid> Nedlagte,
        IReadOnlyList<Guid> Blindveier);

    private sealed record Malvedtak(string Eli, string Tittel, Oppheving? OpphevesAv);

    /// <param name="rettskildeIder">Avgrensning til noen rettskilder (tester og feilsøking). Null = hele korpuset.</param>
    /// <param name="oppfrisk">[Ny, #429 PR 3] Skriv påstanden og koblingene på nytt for lastede funn som fortsatt foreslås, er
    /// <c>oppdaget</c> og ikke er rørt av et menneske (sist endret av ingen eller av regelen selv). Uten den beholder lastede funn hintet fra
    /// PR 2. Bare sammen med <paramref name="skriv"/>.</param>
    public async Task<UtgattAktorRapport> KjorAsync(
        bool skriv, string bruker, IReadOnlyCollection<Guid>? rettskildeIder = null, bool medRader = false, CancellationToken ct = default,
        bool oppfrisk = false)
    {
        // ---- 1. Navnene ----
        var alleNavneformer = await db.Begreper.AsNoTracking()
            .Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId != null && b.Entitetsstatus == "gjeldende"
                        && (b.Godkjenning == null || b.Godkjenning != Strukturkanter.Avvist))
            .Select(b => new { b.Id, b.Term, b.Navneformgrunn, Vid = b.VirksomhetReferanseId!.Value })
            .ToListAsync(ct);
        var tvetydige = alleNavneformer.GroupBy(b => b.Term, StringComparer.Ordinal)
            .Where(g => g.Select(b => b.Vid).Distinct().Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);

        // [ENDRET, #429 PR 3] Etterfølger- og avviklerkantene leses før navnene: en aktør med livsstatus NULL og en avvikler-kant er ikke
        // levende uten videre (Johann 2026-11-02 (4)). Query-filteret holder avviste og foreldede kanter ute; forslag er med.
        var alleKanter = await db.Strukturkanter.AsNoTracking()
            .Where(k => k.Kategori == Strukturkanter.Relasjon && k.Typekode == "etterfolger" && k.TilVirksomhetId != null)
            .Select(k => new Kant(k.Id, k.TilVirksomhetId!.Value, k.FraVirksomhetId))
            .ToListAsync(ct);
        var kanter = alleKanter.GroupBy(k => k.Til).ToDictionary(g => g.Key, g => g.OrderBy(k => k.Id).ToList());
        var avviklet = (await db.Strukturkanter.AsNoTracking()
            .Where(k => k.Kategori == Strukturkanter.Relasjon && k.Typekode == "avvikler" && k.TilVirksomhetId != null)
            .Select(k => k.TilVirksomhetId!.Value).ToListAsync(ct)).ToHashSet();

        var vidAlle = alleNavneformer.Select(b => b.Vid).Concat(alleKanter.Select(k => k.Til))
            .Concat(alleKanter.Where(k => k.Fra != null).Select(k => k.Fra!.Value)).Distinct().ToList();
        var virksomheter = await db.Virksomheter.AsNoTracking().Where(v => vidAlle.Contains(v.Id))
            .Select(v => new { v.Id, v.Navn, v.Livsstatus, v.GyldigTil }).ToDictionaryAsync(v => v.Id, ct);
        bool Levende(Guid vid) => virksomheter.TryGetValue(vid, out var v) && v.Livsstatus != "nedlagt" && !(v.Livsstatus == null && avviklet.Contains(vid));
        bool LivsstatusMangler(Guid vid) => virksomheter.TryGetValue(vid, out var v) && v.Livsstatus == null && avviklet.Contains(vid);

        // Aktøren omtales med navneformen lovteksten bruker i dag (CLAUDE.md §0: registernavnet er avledet, og kan stå i versaler —
        // «JUSTIS- OG BEREDSKAPSDEPARTEMENTET»). Bare når det finnes nøyaktig én gjeldende navneform; ellers registernavnet.
        var visningsnavn = alleNavneformer.Where(b => b.Navneformgrunn == "gjeldende").GroupBy(b => b.Vid)
            .Where(g => g.Select(b => b.Term).Distinct(StringComparer.Ordinal).Count() == 1).ToDictionary(g => g.Key, g => g.First().Term);
        string Navn(Guid vid) => visningsnavn.GetValueOrDefault(vid) ?? (virksomheter.TryGetValue(vid, out var v) ? v.Navn : "ukjent aktør");
        string KantNavn(Kant k) => k.Fra is { } f ? Navn(f) : "ukjent aktør";
        var sokes = alleNavneformer
            .Where(b => virksomheter.ContainsKey(b.Vid) && (!Levende(b.Vid) || b.Navneformgrunn == "utgatt") && b.Term.Length >= MinsteTermlengde)
            .ToList();
        var utgatteTermer = sokes.Select(b => b.Term).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ---- 2. Rettskildene ----
        var rkQuery = db.Rettskilder.AsNoTracking().Where(r => r.Entitetsstatus == "gjeldende" && r.Importrolle == "primaer");
        if (rettskildeIder is not null) rkQuery = rkQuery.Where(r => rettskildeIder.Contains(r.Id));

        // ---- 3. Tekstforekomstene, én spørring per navn (50 i den delte basen 2026-10-31) ----
        // Én spørring per TERM, ikke per navneform: samme term kan være navneform flere ganger (for samme aktør, eller for to aktører når
        // den er tvetydig). Da telles forekomsten én gang, og funnet peker på alle navneformene med den termen.
        var treff = new List<(Guid Rk, Guid NodeId, string Eid, int Rekkefolge, Guid? Forelder, string Tekst, string Term, IReadOnlyList<Guid> BegrepIder, Guid Vid, string Quote)>();
        foreach (var nf in sokes.GroupBy(b => b.Term, StringComparer.Ordinal))
        {
            var term = nf.Key;
            IReadOnlyList<Guid> begrepIder = nf.Select(b => b.Id).Distinct().ToList();
            var vid = nf.First().Vid; // entydig når termen ikke er tvetydig; tvetydige termer foreslås ikke
            var noder = await db.RettskildeNoder.AsNoTracking()
                .Where(n => n.Entitetsstatus == "gjeldende" && !n.Opphevet && !n.ErMerknad && Bladnoder.Contains(n.NodeType)
                            && n.Tekst != null && n.Tekst.Contains(term) && rkQuery.Any(r => r.Id == n.RettskildeId))
                .Select(n => new { n.Id, n.RettskildeId, n.Eid, n.Sorteringsrekkefolge, n.ParentNodeId, n.Tekst })
                .ToListAsync(ct);
            var rx = Ordregex(term);
            foreach (var n in noder)
            {
                var m = rx.Match(n.Tekst!);
                if (m.Success) treff.Add((n.RettskildeId, n.Id, n.Eid, n.Sorteringsrekkefolge, n.ParentNodeId, n.Tekst!, term, begrepIder, vid, m.Value));
            }
        }

        var rkIder = treff.Select(x => x.Rk).Distinct().ToList();
        var rettskilder = await db.Rettskilder.AsNoTracking().Where(r => rkIder.Contains(r.Id))
            .Select(r => new { r.Id, r.Eli, r.DokumentId, r.Tittel, r.Kortnavn, r.RentEndringsvedtak, r.AnsvarligDepartement })
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

        // ---- 4. Etterfølgerne og gruppene ----
        Kjede KjedeFor(Guid vid)
        {
            var brukt = new List<(Kant, bool)>();
            var levende = new List<Guid>();
            var nedlagte = new List<Guid>();
            var blindveier = new List<Guid>();
            var sett = new HashSet<Guid> { vid };
            var ko = new Queue<(Kant Kant, bool Direkte)>((kanter.GetValueOrDefault(vid) ?? []).Select(k => (k, true)));
            while (ko.Count > 0)
            {
                var (k, direkte) = ko.Dequeue();
                brukt.Add((k, direkte));
                if (k.Fra is not { } f || !sett.Add(f)) continue; // ukjent aktør, eller en sirkel
                if (Levende(f)) { levende.Add(f); continue; }
                nedlagte.Add(f);
                var videre = kanter.GetValueOrDefault(f) ?? [];
                if (videre.Count == 0) blindveier.Add(f);
                foreach (var n in videre) ko.Enqueue((n, false));
            }
            return new Kjede(brukt, levende, nedlagte, blindveier);
        }
        var vider = treff.Select(x => x.Vid).Distinct().ToList();
        var kjeder = vider.ToDictionary(v => v, KjedeFor);
        var hull = await EtterfolgerHull.HovedetterfolgerUkjentAsync(db, vider.Where(v => !Levende(v)).ToList(), ct);
        string Gruppe(Guid vid) =>
            Levende(vid) ? GruppeA
            : !kanter.ContainsKey(vid) ? GruppeD
            : hull.Contains(vid) ? GruppeC : GruppeB;
        // R6: etterfølgerne fra modellen (de direkte kantene, som i regler_b.py), og for gruppe A aktøren selv.
        IReadOnlyCollection<string> R6Navn(Guid vid) => (kanter.GetValueOrDefault(vid) ?? []).Select(KantNavn)
            .Concat(Gruppe(vid) == GruppeA ? [Navn(vid)] : []).Where(n => n != "ukjent aktør").Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // ---- 5. Klassifisering per forekomst ----
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
            var regler = UtgattAktorRegler.Treff(x.Term, UtgattAktorRegler.Normaliser(x.Tekst), UtgattAktorRegler.Normaliser(forelder), R6Navn(x.Vid));
            kandidater.Add(new Nodetreff(x.Rk, x.NodeId, x.Eid, x.Rekkefolge, x.Term, x.BegrepIder, x.Vid, x.Quote, x.Tekst, regler.ToList()));
        }
        var kandidatRk = kandidater.Select(k => k.RettskildeId).Distinct().ToList();

        // ---- 6. R8: kilder som er opphevet av en annen gjeldende primærkilde ----
        var opphevetAvAnnen = await Opphevingskontroll.OpphevetAvAnnenKildeAsync(db, kandidatRk.Select(id => (id, rettskilder[id].Eli!)).ToList(), ct);

        // ---- 7. Målvedtakene: entydige, og om rettskilden opphever dem (teksten) eller bare har dem i «Endrer» ----
        var entydigMal = kandidatRk.Where(id => endrer.GetValueOrDefault(id) is { Count: 1 }).ToDictionary(id => id, id => endrer[id][0]);
        var opphevesAvKilden = await Opphevingskontroll.OpphevesAvKildenAsync(db, entydigMal.Select(p => (p.Key, p.Value.EndringEid)).ToList(), ct);
        var malvedtak = entydigMal.ToDictionary(p => p.Key,
            p => new Malvedtak(p.Value.EndringEid, p.Value.Tittel, opphevesAvKilden.GetValueOrDefault((p.Key, p.Value.EndringEid))));

        // R7: målvedtaket i korpus, konsolidert med rettskilden (sist_endret_ved), og nodetekstene i det.
        var malEli = entydigMal.Values.Select(e => e.EndringEid).Distinct(StringComparer.Ordinal).ToList();
        var malKilder = await db.Rettskilder.AsNoTracking()
            .Where(r => r.Eli != null && malEli.Contains(r.Eli) && r.Entitetsstatus == "gjeldende" && r.Importrolle == "primaer")
            .Select(r => new { r.Id, Eli = r.Eli!, r.SistEndretVed, r.DokumentId }).ToListAsync(ct);
        var malPerEli = malKilder.GroupBy(m => m.Eli, StringComparer.Ordinal).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
        var r7Mal = entydigMal.Where(p => malPerEli.TryGetValue(p.Value.EndringEid, out var m) && m.SistEndretVed is { } sev
                && rettskilder[p.Key].DokumentId is { } dok && string.Equals(sev.Trim(), KortDokumentId(dok), StringComparison.Ordinal))
            .ToDictionary(p => p.Key, p => malPerEli[p.Value.EndringEid]);
        var r7MalIder = r7Mal.Values.Select(m => m.Id).Distinct().ToList();
        var malTekster = (await db.RettskildeNoder.AsNoTracking()
                .Where(n => r7MalIder.Contains(n.RettskildeId) && n.Entitetsstatus == "gjeldende" && n.Tekst != null)
                .Select(n => new { n.RettskildeId, n.Tekst }).ToListAsync(ct))
            .GroupBy(n => n.RettskildeId).ToDictionary(g => g.Key, g => g.Select(n => UtgattAktorRegler.NormaliserMellomrom(n.Tekst)).ToHashSet(StringComparer.Ordinal));

        // ---- 8. Parene, i to omganger: R7 krever at målvedtaket har eget funn for samme aktør ----
        var nokler = await db.Funn.AsNoTracking().Where(f => f.EksternNokkel != null && f.EksternNokkel.StartsWith(NokkelPrefiks))
            .Select(f => new { f.Id, f.EksternNokkel, f.Status, f.SistEndretAv, f.Tittel, f.Beskrivelse })
            .ToDictionaryAsync(f => f.EksternNokkel!, ct);
        var par = kandidater.GroupBy(k => (k.RettskildeId, k.VirksomhetId))
            .OrderBy(g => rettskilder[g.Key.RettskildeId].DokumentId, StringComparer.Ordinal).ThenBy(g => Navn(g.Key.VirksomhetId), StringComparer.Ordinal)
            .Select(g => (Key: g.Key, Forekomster: g.OrderBy(k => k.Rekkefolge).ThenBy(k => k.Eid, StringComparer.Ordinal).ThenBy(k => k.Term, StringComparer.Ordinal).ToList()))
            .ToList();
        string Nokkel(string eli, Guid vid) => NokkelPrefiks + EliKort(eli) + ":" + vid;
        var foreslattForsteOmgang = par.Where(p => !opphevetAvAnnen.ContainsKey(p.Key.RettskildeId) && !p.Forekomster.All(k => k.Regler.Count > 0))
            .Select(p => p.Key).ToHashSet();
        foreach (var (key, forekomster) in par)
        {
            if (!r7Mal.TryGetValue(key.RettskildeId, out var m) || !malTekster.TryGetValue(m.Id, out var tekster)) continue;
            var malHarFunn = foreslattForsteOmgang.Contains((m.Id, key.VirksomhetId))
                || (nokler.TryGetValue(Nokkel(m.Eli, key.VirksomhetId), out var mf) && mf.Status != Funn.ForklartBort);
            if (!malHarFunn) continue;
            foreach (var k in forekomster.Where(k => tekster.Contains(UtgattAktorRegler.NormaliserMellomrom(k.Tekst)))) k.Regler.Add(UtgattAktorRegler.R7);
        }

        // ---- 9. R9 for parene som foreslås ----
        var nodetekster = kandidater.Where(k => !opphevetAvAnnen.ContainsKey(k.RettskildeId)).GroupBy(k => k.RettskildeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<(string Eid, string? Tekst)>)g.Select(k => (k.Eid, (string?)k.Tekst)).DistinctBy(x => x.Eid).ToList());
        var hjemmelOpphevet = await Opphevingskontroll.HjemmelOpphevetAsync(db, nodetekster.Keys.ToList(), nodetekster, ct);

        // ---- 10. Utfall, telling og skriving ----
        var noderPerRegel = UtgattAktorRegler.Alle.ToDictionary(r => r, r => kandidater.Count(k => k.Regler.Contains(r)));
        noderPerRegel[UtgattAktorRegler.R8] = kandidater.Count(k => opphevetAvAnnen.ContainsKey(k.RettskildeId));
        var ikkeForeslattPerRegel = UtgattAktorRegler.Alle.ToDictionary(r => r, _ => 0);
        var regelForklaresBort = UtgattAktorRegler.Alle.Append(UtgattAktorRegler.R9).ToDictionary(r => r, _ => 0);
        var foreslattPerGruppe = Grupper.ToDictionary(g => g, _ => 0);
        var ikkeForeslattPerGruppe = Grupper.ToDictionary(g => g, _ => 0);
        var gHoldt = Grupper.ToDictionary(g => g, _ => 0);
        var gKildefeil = Grupper.ToDictionary(g => g, _ => 0);
        var gBort = Grupper.ToDictionary(g => g, _ => 0);
        var gPremiss = Grupper.ToDictionary(g => g, _ => 0);
        int foreslatt = 0, ikkeForeslatt = 0, iEndring = 0, medMal = 0, malOpphevet = 0, nye = 0, finnes = 0, finnesForklartBort = 0;
        int forklaresBort = 0, forklartBortSatt = 0, vurdertIkkeRort = 0, premissmangel = 0, oppfriskes = 0, oppfrisket = 0, kildefeilNye = 0;
        var kildefeilIder = new Dictionary<Guid, Guid>();
        var rader = new List<UtgattAktorRad>();

        foreach (var (key, forekomster) in par)
        {
            var rk = rettskilder[key.RettskildeId];
            var v = virksomheter[key.VirksomhetId];
            var gruppe = Gruppe(key.VirksomhetId);
            var nokkel = Nokkel(rk.Eli!, key.VirksomhetId);
            var finnesFunn = nokler.GetValueOrDefault(nokkel);
            var endringer = endrer.GetValueOrDefault(key.RettskildeId);
            var mal = malvedtak.GetValueOrDefault(key.RettskildeId);
            var r8 = opphevetAvAnnen.GetValueOrDefault(key.RettskildeId);
            var undertrykt = r8 is null && forekomster.All(k => k.Regler.Count > 0);
            var utfall = r8 is not null ? UtfallKildefeil : undertrykt ? UtfallHoldtTilbake : UtfallForeslatt;
            var eider = forekomster.Select(k => k.Eid).Distinct(StringComparer.Ordinal).ToList();
            var r9 = utfall == UtfallForeslatt
                ? (hjemmelOpphevet.GetValueOrDefault(key.RettskildeId) ?? []).Where(h => h.GjelderNoder(eider)).ToList()
                : [];
            string? bevis = null;
            var forklares = false;

            if (utfall == UtfallForeslatt)
            {
                foreslatt++;
                foreslattPerGruppe[gruppe]++;
                if (endringer is not null) iEndring++;
                if (mal is not null) medMal++;
                if (mal?.OpphevesAv is not null) malOpphevet++;
                if (r9.Count > 0) { premissmangel++; gPremiss[gruppe]++; }
                if (finnesFunn is not null)
                {
                    finnes++;
                    if (finnesFunn.Status == Funn.ForklartBort) finnesForklartBort++;
                }
                else
                {
                    nye++;
                }

                var kjede = kjeder[key.VirksomhetId];
                NyttFunn Bygg() => ByggFunn(nokkel, rk.Eli!, rk.DokumentId, rk.Kortnavn ?? rk.Tittel, rk.Tittel, Navn(key.VirksomhetId), v.GyldigTil,
                    key.VirksomhetId, gruppe, LivsstatusMangler(key.VirksomhetId), forekomster, Kjedelinjer(kjede, KantNavn, Navn, id => virksomheter.GetValueOrDefault(id)?.GyldigTil),
                    kjede.Kanter.Select(e => (e.Kant.Id, e.Direkte ? KantNavn(e.Kant) : $"{KantNavn(e.Kant)} (etter {Navn(e.Kant.Til)})", e.Direkte)).ToList(),
                    Ansvarlige(rk.AnsvarligDepartement, utgatteTermer), endringer?.Count ?? 0, mal);
                if (finnesFunn is null)
                {
                    if (skriv) await funnTjeneste.OpprettAsync(Bygg(), bruker, ct);
                }
                else if (finnesFunn.Status == Funn.Oppdaget && (finnesFunn.SistEndretAv is null || finnesFunn.SistEndretAv == OppdagelsesKilde))
                {
                    var nytt = Bygg();
                    if (nytt.Beskrivelse != finnesFunn.Beskrivelse || nytt.Tittel != finnesFunn.Tittel)
                    {
                        oppfriskes++;
                        if (skriv && oppfrisk)
                        {
                            await funnTjeneste.OppdaterAsync(finnesFunn.Id, new FunnEndring(nytt.Tittel, nytt.Beskrivelse, nytt.Undertype, null,
                                nytt.InternMerknad, nytt.Koblinger, null), OppdagelsesKilde, ct);
                            oppfrisket++;
                        }
                    }
                }
            }
            else
            {
                ikkeForeslatt++;
                ikkeForeslattPerGruppe[gruppe]++;
                var regler = utfall == UtfallKildefeil ? [UtgattAktorRegler.R8] : forekomster.SelectMany(k => k.Regler).Distinct().ToList();
                if (utfall == UtfallKildefeil) gKildefeil[gruppe]++; else gHoldt[gruppe]++;
                foreach (var r in regler) ikkeForeslattPerRegel[r]++;
                bevis = utfall == UtfallKildefeil
                    ? $"R8: rettskilden er opphevet av {r8!.Etikett} (eId {r8.Eid}): «{Opphevingskontroll.Kort(r8.Sitat)}»."
                    : "Alle forekomstene treffes av regler som teller, men ikke foreslår: "
                      + string.Join("; ", forekomster.Select(k => $"{Bestemmelsesetikett(k.Eid)} (eId {k.Eid}): {string.Join(", ", k.Regler.Distinct())}"
                          + (k.Regler.Contains(UtgattAktorRegler.R7) && r7Mal.TryGetValue(key.RettskildeId, out var m7)
                              ? $" (ordrett i målvedtaket {Dokumentreferanse(m7.DokumentId) ?? EliKort(m7.Eli)}, konsolidert med rettskilden, med eget funn for samme aktør)"
                              : ""))) + ".";
                if (finnesFunn is not null)
                {
                    if (finnesFunn.Status == Funn.Oppdaget)
                    {
                        forklares = true;
                        forklaresBort++;
                        gBort[gruppe]++;
                        foreach (var r in regler) regelForklaresBort[r]++;
                    }
                    else if (finnesFunn.Status != Funn.ForklartBort)
                    {
                        vurdertIkkeRort++;
                    }
                }
                if (skriv && utfall == UtfallKildefeil && !kildefeilIder.ContainsKey(key.RettskildeId))
                {
                    var foer = await db.Kildefeil.CountAsync(k => k.RettskildeId == key.RettskildeId && k.Type == KildefeiltypeOpphevetAvAnnenKilde
                        && k.FunnetAvMekanisme == OppdagelsesKilde, ct);
                    var kf = await new KildefeilTjeneste(db).OpprettEllerFinnAsync(key.RettskildeId, null, KildefeiltypeOpphevetAvAnnenKilde,
                        $"«{rk.Tittel}» ({Dokumentreferanse(rk.DokumentId) ?? EliKort(rk.Eli!)}) står som gjeldende, men {r8!.Etikett} opphever den: "
                        + $"«{Opphevingskontroll.Kort(r8.Sitat)}». Lovdata-statusen er ikke vedlikeholdt for denne kilden (#429 måling 2, R8). "
                        + "Maskinregelen for utgåtte aktører lager ingen funn i kilden.",
                        OppdagelsesKilde, bruker, ct);
                    if (foer == 0) kildefeilNye++;
                    kildefeilIder[key.RettskildeId] = kf.Id;
                }
                if (utfall == UtfallKildefeil && kildefeilIder.TryGetValue(key.RettskildeId, out var kid)) bevis += $" Sendt til kildefeilkøen (kildefeil {kid}).";
                if (skriv && forklares)
                {
                    var begrunnelse = $"Forklart bort av maskinregelen {OppdagelsesKilde} (#429 PR 3, Johanns avgjørelse 2026-11-02). {bevis}";
                    if (await funnTjeneste.ForklarBortMaskineltAsync(finnesFunn!.Id, begrunnelse, OppdagelsesKilde, bruker, ct)) forklartBortSatt++;
                }
            }

            if (medRader)
            {
                rader.Add(new UtgattAktorRad(rk.DokumentId, rk.Kortnavn ?? rk.Tittel, key.VirksomhetId, Navn(key.VirksomhetId), gruppe,
                    utfall == UtfallForeslatt, nokkel, finnesFunn?.Status, endringer is not null, mal?.Eli,
                    forekomster.Select(k => new UtgattAktorForekomst(k.Eid, k.Term, k.Quote, k.Regler.Distinct().ToList())).ToList(),
                    utfall, bevis, forklares, r9.Select(Opphevingskontroll.Premissmangeltekst).ToList()));
            }
        }

        var perGruppe = Grupper.ToDictionary(g => g, g => new UtgattAktorGruppetelling(foreslattPerGruppe[g], gPremiss[g], gHoldt[g], gKildefeil[g], gBort[g]));
        var perRegel = UtgattAktorRegler.Alle.ToDictionary(r => r, r => new UtgattAktorRegeltelling(noderPerRegel[r], ikkeForeslattPerRegel[r], regelForklaresBort[r]));
        perRegel[UtgattAktorRegler.R9] = new UtgattAktorRegeltelling(0, premissmangel, 0);

        return new UtgattAktorRapport(
            skriv, sokes.Count, sokes.Select(b => b.Term).Distinct(StringComparer.Ordinal).Count(), tvetydige.Where(t => sokes.Any(s => s.Term == t)).Order(StringComparer.Ordinal).ToList(),
            treff.Count, nOrg, nRent, nTvetydig, nUtenEli, tvetydigePar.Count,
            kandidater.Select(k => (k.RettskildeId, k.Term)).Distinct().Count(),
            foreslatt + ikkeForeslatt, ikkeForeslatt, ikkeForeslattPerRegel, noderPerRegel, foreslatt, foreslattPerGruppe, ikkeForeslattPerGruppe,
            iEndring, medMal, nye, finnes, finnesForklartBort, medRader ? rader : null,
            perGruppe, perRegel, opphevetAvAnnen.Count, kildefeilNye, forklaresBort, forklartBortSatt, vurdertIkkeRort, premissmangel, malOpphevet,
            oppfriskes, oppfrisket);
    }

    /// <summary>«LTI/forskrift/2002-03-13-266» → «forskrift/2002-03-13-266», formen Lovdata bruker i <c>sist_endret_ved</c>.</summary>
    public static string KortDokumentId(string dokumentId)
    {
        var i = dokumentId.IndexOf('/');
        return i >= 0 ? dokumentId[(i + 1)..] : dokumentId;
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

    /// <summary>[Ny, #429 PR 3] Setningene om kjeden etter de direkte etterfølgerne: hvilke av dem som selv er nedlagt, hvilke levende aktører
    /// kjeden når fram til, og hvor den ender i en nedlagt aktør uten etterfølger. Tom når alle de direkte etterfølgerne lever.</summary>
    private static string Kjedelinjer(Kjede kjede, Func<Kant, string> kantNavn, Func<Guid, string> navn, Func<Guid, DateOnly?> gyldigTil)
    {
        if (kjede.Nedlagte.Count == 0) return "";
        var direkteNedlagte = kjede.Kanter.Where(k => k.Direkte && k.Kant.Fra is { } f && kjede.Nedlagte.Contains(f)).Select(k => k.Kant.Fra!.Value).Distinct().ToList();
        var deler = new List<string>();
        foreach (var d in direkteNedlagte)
        {
            deler.Add($"{navn(d)} er selv nedlagt" + (gyldigTil(d) is { } dato ? $" (siste dag {dato:yyyy-MM-dd})." : "."));
        }
        var direkteLevende = kjede.Kanter.Where(k => k.Direkte && k.Kant.Fra is { } f && kjede.Levende.Contains(f)).Select(k => k.Kant.Fra!.Value).ToHashSet();
        var videre = kjede.Levende.Where(l => !direkteLevende.Contains(l)).Select(navn).Distinct(StringComparer.Ordinal).ToList();
        if (videre.Count > 0) deler.Add($"Kjeden av etterfølgere i modellen fører videre til {string.Join(", ", videre)}.");
        if (kjede.Blindveier.Count > 0)
        {
            deler.Add($"Kjeden ender i nedlagt aktør uten etterfølger: {string.Join(", ", kjede.Blindveier.Select(navn).Distinct(StringComparer.Ordinal))}.");
        }
        return " " + string.Join(" ", deler);
    }

    /// <summary>[Ny, #429 PR 3] Lovdatas ansvarlige departement for rettskilden, med de navnene som selv er utgått i modellen (feltet er
    /// upålitelig for eldre LTI: «Sosialdepartementet», «Fiskeridepartementet» i måling 2).</summary>
    private static (IReadOnlyList<string> Navn, IReadOnlyList<string> Utgatte) Ansvarlige(List<string>? ansvarlig, IReadOnlySet<string> utgatteTermer)
    {
        var navn = (ansvarlig ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return (navn, navn.Where(utgatteTermer.Contains).ToList());
    }

    private static string Liste(IReadOnlyList<string> navn) =>
        navn.Count <= 1 ? string.Join("", navn) : string.Join(", ", navn.Take(navn.Count - 1)) + " og " + navn[^1];

    private static NyttFunn ByggFunn(
        string nokkel, string eli, string? dokumentId, string kortnavn, string tittel, string aktor, DateOnly? gyldigTil, Guid vid, string gruppe,
        bool livsstatusMangler, IReadOnlyList<Nodetreff> forekomster, string kjedelinjer, IReadOnlyList<(Guid KantId, string Etikett, bool Direkte)> etterfolgere,
        (IReadOnlyList<string> Navn, IReadOnlyList<string> Utgatte) ansvarlige, int antallEndret, Malvedtak? mal)
    {
        var referanse = Dokumentreferanse(dokumentId) ?? EliKort(eli);
        var termer = forekomster.Select(k => k.Term).Distinct(StringComparer.Ordinal).ToList();
        var termTekst = string.Join(" og ", termer.Select(t => $"«{t}»"));
        var bestemmelser = forekomster.Select(k => k.Eid).Distinct(StringComparer.Ordinal).ToList();
        var antall = bestemmelser.Count == 1 ? "én bestemmelse" : $"{bestemmelser.Count} bestemmelser";
        var direkte = etterfolgere.Where(e => e.Direkte).Select(e => e.Etikett).Distinct().ToList();

        var linjer = new List<string>();
        if (gruppe == GruppeA)
        {
            // [ENDRET, #429 PR 3, N07] «Aktøren finnes fortsatt, nå som X» der X er det utgåtte navnet selv er selvmotsigende: det skjer når
            // aktøren ikke har noen gjeldende navneform og registernavnet faller tilbake på termen.
            linjer.Add(termer.Contains(aktor, StringComparer.Ordinal)
                ? $"«{tittel}» ({referanse}) er gjeldende og bruker {termTekst} i {antall}. Navnet er registrert som utgått, men modellen har "
                  + "ingen gjeldende navneform for aktøren og sier ikke om den er nedlagt."
                : $"«{tittel}» ({referanse}) er gjeldende og bruker {termTekst} i {antall}. Navnet er utgått. Aktøren finnes fortsatt, nå som {aktor}.");
            linjer.Add($"- Om oppgaven i bestemmelsen fortsatt ligger hos {aktor}, er ikke vurdert. Den kan være flyttet til en annen aktør.");
            // Ekom-tilfellet fra juristmålingen: en del av oppgavene er overført (DFD etterfølger KDD). Kantene kobles på, og
            // premissporten krever dem vurdert.
            if (direkte.Count > 0) linjer.Add($"- {string.Join(", ", direkte)} tok over en del av oppgavene ifølge modellen.{kjedelinjer}");
        }
        else
        {
            linjer.Add(livsstatusMangler
                ? $"«{tittel}» ({referanse}) er gjeldende og nevner {termTekst} i {antall}. Modellen har en avvikler-kant for {aktor}, men "
                  + "livsstatus er ikke satt. Aktøren regnes ikke som levende."
                : $"«{tittel}» ({referanse}) er gjeldende og nevner {termTekst} i {antall}. {aktor} er nedlagt"
                  + (gyldigTil is { } d ? $" (siste dag {d:yyyy-MM-dd})." : "."));
            // [ENDRET, kaldsjekken 2026-10-31] Ingen «Etterfølger:» her: etterfølgerlinjen under påstanden viser kantene eller juristens
            // punkt «Etterfølger», og to linjer med samme ledetekst kunne si to ulike ting.
            linjer.Add(gruppe switch
            {
                GruppeB => $"- Modellen har {string.Join(", ", direkte)} som etterfølger.{kjedelinjer} Det er ikke vurdert om oppgaven i bestemmelsen gikk dit.",
                GruppeC => $"- Hovedetterfølgeren er ukjent. {string.Join(", ", direkte)} tok over en del av oppgavene ifølge modellen.{kjedelinjer}",
                _ => "- Modellen har ingen etterfølger for aktøren.",
            });
        }
        // [Ny, #429 PR 3, N08] Lovdatas metadata ved siden av hintet, som sekundær kilde — ikke som svar på hvem som har oppgaven.
        if (ansvarlige.Navn.Count > 0)
        {
            linjer.Add($"- Lovdata oppgir {Liste(ansvarlige.Navn)} som ansvarlig departement for rettskilden (metadata, sekundær kilde)."
                + (ansvarlige.Utgatte.Count > 0 ? $" {Liste(ansvarlige.Utgatte)} er selv et utgått navn i modellen." : ""));
        }
        if (antallEndret > 0)
        {
            // [ENDRET, #429 PR 3] Lovdatas «Endrer»-felt tar med opphevinger. Teksten avgjør om det er en oppheving; ellers står begge.
            linjer.Add(mal switch
            {
                { OpphevesAv: { } o } => $"- Rettskilden opphever «{mal.Tittel}» ({Bestemmelsesetikett(o.Eid)}: «{Opphevingskontroll.Kort(o.Sitat)}»).",
                { } m => $"- Rettskilden endrer eller opphever «{m.Tittel}» ifølge Lovdatas «Endrer»-felt. Om den bare er et endringsvedtak, er ikke avklart.",
                _ => $"- Rettskilden endrer eller opphever {antallEndret} vedtak, så målvedtaket er ikke entydig. Om den bare er et endringsvedtak, er ikke avklart.",
            });
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
        foreach (var e in etterfolgere.DistinctBy(e => e.KantId)) koblinger.Add(new NyKobling(Funn.Etterfolger, e.Etikett, StrukturkantId: e.KantId));
        if (mal is { } mv)
        {
            koblinger.Add(new NyKobling(Funn.Malvedtak, mv.Tittel,
                Merknad: mv.OpphevesAv is null ? "står i rettskildens «Endrer»-felt (endring eller oppheving)" : "oppheves av rettskilden",
                RettskildeEli: mv.Eli));
        }

        var delvis = forekomster.Where(k => k.Regler.Count > 0).Select(k => $"{Bestemmelsesetikett(k.Eid)} ({string.Join(", ", k.Regler.Distinct())})").ToList();
        var intern = $"Maskinfunn ({OppdagelsesKilde}, #429 PR 2/3), gruppe {gruppe}. "
            + (delvis.Count > 0 ? $"Forekomster truffet av R1–R7 (telt, men minst én forekomst er uten treff): {string.Join("; ", delvis)}. " : "")
            + "Presisjonen er målt to ganger: 0,70 på 40 par 2026-10-31, og 0,25 (0,11–0,47) på 20 lastede funn 2026-11-02. R2b, R5–R9 og "
            + "etterfølgerkjeden kom i PR 3. Ny måling før maskinfunn går videre fra «oppdaget».";

        return new NyttFunn(
            Funn.UtgattAktor, gruppe == GruppeA ? "utgatt_navn" : "nedlagt_aktor",
            gruppe == GruppeA ? $"Utgått navn i {kortnavn}: {termer[0]}" : $"Nedlagt aktør i {kortnavn}: {aktor}",
            string.Join("\n", linjer), OppdagelsesKilde, InternMerknad: intern, FunnetI: "#429", EksternNokkel: nokkel, Koblinger: koblinger);
    }
}
