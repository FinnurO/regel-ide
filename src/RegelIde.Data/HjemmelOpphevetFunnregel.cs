using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Én forskrift i rapporten (<c>?medRader=true</c>).</summary>
/// <param name="Undertype">Undertypen regelen gir, eller null når forskriften ikke gir funn (bare med når et lagret funn forklares bort).</param>
/// <param name="ForklaresBort">[Ny, #472 PR 4] Et lagret funn som står som oppdaget, og som regelen forklarer bort.</param>
/// <param name="Bevis">[Ny, #472 PR 4] Opphevingen av forskriften selv (R8), eller hvorfor funnet forklares bort.</param>
public sealed record HjemmelOpphevetRad(
    string? DokumentId, string Tittel, string EksternNokkel, string? Undertype, string SamletStatus, string Kunnskapsstatus, bool? RentEndringsvedtak,
    string Utfall, string? FinnesStatus, IReadOnlyList<string> Hjemler, string? Kildeform = null, bool ForklaresBort = false, string? Bevis = null,
    string? FinnesUndertype = null);

/// <summary>Hvor mange funn én tolkning av #446-regelen gir, per undertype og per prefiks i Lovdatas dokument-id (SF, DEL, LTI …).</summary>
public sealed record HjemmelOpphevetTolkning(int Funn, IReadOnlyDictionary<string, int> PerUndertype, IReadOnlyDictionary<string, int> PerPrefiks);

/// <summary>Rapporten fra <see cref="HjemmelOpphevetFunnregel"/>.</summary>
/// <param name="HjemmelsstatusBeregnet">Tabellen <c>hjemmelsstatus</c> har rader. Uten dem gir regelen ingenting: kjør
/// <c>POST /api/hjemmelsstatus/maskinregel?skriv=true</c> først.</param>
/// <param name="Kandidater">Forskrifter med en undertype før regelen om uttømt virkning.</param>
/// <param name="UtelattRentEndringsvedtak">#446-kolonnen er <c>true</c>: uttømt virkning, ingen funn.</param>
/// <param name="RentMenMalUtenforKorpus">[Ny, #446 PR 2, AK 7] #446-kolonnen er <c>true</c>, men et mål i «Endrer» er utenfor korpus: funnet står med
/// premissmangelen «Mål utenfor korpus».</param>
/// <param name="IkrafttredelseEnesteDisposisjonstype">Kandidater der ikrafttredelse er den eneste disposisjonstypen (fra tittelen). Telles, men
/// utelates IKKE: tittelregelen treffer også «Forskrift om ikrafttredelse av og overgangsregler til …» (DEL/forskrift/1999-06-25-711), som ikke har
/// uttømt virkningen. Om ikrafttredelsesvedtak har uttømt virkning, avgjøres av #446.</param>
/// <param name="IkkeKlassifisert">#446-kolonnen er NULL blant kandidatene som er igjen. Bryteren avgjør om de gir funn.</param>
/// <param name="Streng">Tolkning A, fase 1 ordrett: NULL i #446-kolonnen gir ikke funn.</param>
/// <param name="NullGirFunn">Tolkning B: bare <c>true</c> utelates; NULL gir funn med premissmangelen «Uttømt virkning ikke klassifisert».</param>
/// <param name="Foreslatt">Funn etter tolkningen bryteren står på.</param>
/// <param name="HoldtAvPremissporten">Funn med undertypen hjemmel opphevet eller videreføring utløpt som ikke vises som «mulig bortfalt», fordi
/// «Ny hjemmel» ikke er vurdert. Alle nye funn er det.</param>
/// <param name="UtenGrunnlag">Lagrede funn fra regelen der forskriften ikke lenger er i omfanget (ikke gjeldende primær forskrift med
/// hjemmelsstatus), eller der bare bryteren for #446 holder funnet tilbake. Telles, røres ikke. [ENDRET, #472 PR 4] Funn der regelen selv sier at
/// forskriften ikke gir funn, telles i <paramref name="ForklaresBort"/>.</param>
/// <param name="ForskriftenOpphevet">[Ny, #472 PR 4] Kandidater der forskriften selv er opphevet med tekstbevis i en annen gjeldende kilde i korpus
/// (R8 i #429): ingen funn.</param>
/// <param name="KunngjortIkkeKontrollert">[Ny, #472 PR 4] Foreslåtte funn der korpuset bare har forskriften som kunngjort versjon (LTI). De får
/// premissmangelen «kunngjort versjon, gjeldende status ikke kontrollert».</param>
/// <param name="ForklaresBort">[Ny, #472 PR 4] Lagrede funn (oppdaget, ikke gjenåpnet) der forskriften ikke lenger gir funn etter regelen.</param>
/// <param name="ForklaresBortPerGrunn">Per grunn: forskriften opphevet, samlet status (aktiv, videreført, ukjent), rent endringsvedtak.</param>
/// <param name="ForklartBortSatt">Satt til forklart bort i denne kjøringen (bare med <c>skriv</c>).</param>
/// <param name="VurdertIkkeRort">Lagrede funn der regelen ikke lenger gir funn, men som et menneske har vurdert eller gjenåpnet. Røres ikke.</param>
/// <param name="Oppfriskes">[Ny, #472 PR 4] Lagrede funn som ingen har rørt, der tittel, påstand, undertype eller koblinger er endret.</param>
/// <param name="Oppfrisket">Skrevet på nytt i denne kjøringen (bare med <c>skriv</c> og <c>oppfrisk</c>).</param>
public sealed record HjemmelOpphevetRapport(
    bool Skrevet, string Regelversjon, bool HjemmelsstatusBeregnet, string? HjemmelsstatusRegelversjon, bool IkkeKlassifisertGirFunn,
    int Forskrifter, IReadOnlyDictionary<string, int> PerSamletStatus, int Gjenstandslose,
    int Kandidater, IReadOnlyDictionary<string, int> KandidaterPerUndertype,
    int UtelattRentEndringsvedtak, int IkrafttredelseEnesteDisposisjonstype, int IkkeKlassifisert,
    HjemmelOpphevetTolkning Streng, HjemmelOpphevetTolkning NullGirFunn,
    int Foreslatt, IReadOnlyDictionary<string, int> ForeslattPerUndertype, int Nye, int Finnes, int FinnesForklartBort, int UndertypeEndret,
    int HoldtAvPremissporten, int MuligBortfaltVises, int UtenGrunnlag, IReadOnlyList<HjemmelOpphevetRad>? Rader,
    int ForskriftenOpphevet = 0, IReadOnlyDictionary<string, int>? ForskriftenOpphevetPerPrefiks = null, int KunngjortIkkeKontrollert = 0,
    int ForklaresBort = 0, IReadOnlyDictionary<string, int>? ForklaresBortPerGrunn = null, int ForklartBortSatt = 0, int VurdertIkkeRort = 0,
    int Oppfriskes = 0, int Oppfrisket = 0, IReadOnlyDictionary<string, int>? ForeslattPerPrefiks = null, int RentMenMalUtenforKorpus = 0);

/// <summary>
/// [Ny, #472 PR 2, Johanns avgjørelser 2026-11-18 og 2026-11-20] <b>Maskinregelen for «Hjemmel opphevet»</b> (<c>monster:hjemmel-opphevet</c>):
/// ett funn til vurdering (#429) per gjeldende primær forskrift der den samlede hjemmelsstatusen (<see cref="HjemmelsstatusSamlet"/>, regnet av de
/// lagrede radene i <c>hjemmelsstatus</c> fra PR 1) sier at en hjemmel som teller, er opphevet uten funnet videreføring.
/// <para>
/// <b>Undertypene</b> er de samme ordene som den samlede statusen: <c>hjemmel_opphevet</c> («Hjemmel opphevet, videreføring ikke funnet», mulig
/// bortfalt), <c>blandede_hjemler</c>, <c>uavklart</c> (de som bærer, har rolle som ikke er vurdert, juristrunde 2 R1), <c>videreforing_utlopt</c>
/// og <c>gjenstandslos</c> (eget merke, Johanns avgjørelse (5)). Ukjent gir aldri funn; en hjemmel som selv er en forskrift med blandede eller
/// uavklarte hjemler, arves som ukjent (Johann 2026-11-20).
/// </para>
/// <para>
/// <b>Uttømt virkning gir ikke funn</b> (#446): <c>rent_endringsvedtak = true</c>. [ENDRET, #446 PR 2] Kolonnen er «uttømt virkning» fra
/// <see cref="VedtaksformTjeneste"/>: ren vedtaksform (endring, ikrafttredelse eller oppheving) og i kraft. Er et mål i «Endrer» utenfor korpus, står
/// funnet med premissmangelen «Mål utenfor korpus» (<see cref="FunnTjeneste.MalUtenforKorpusMangelStart"/>).
/// <b>NULL i #446-kolonnen</b> er én bryter (<see cref="KonfignokkelIkkeKlassifisertGirFunn"/>), standard PÅ etter prosjekteierens avgjørelse
/// 2026-11-24 (tolkning B): NULL gir funn med premissmangelen «Uttømt virkning ikke klassifisert», så det ikke kan godkjennes før #446 er gjort.
/// [ENDRET, #446 PR 2] Kolonnen settes nå også til <c>false</c> (blandet, selvstendig, eller ren og sikkert ikke i kraft); NULL er ukjent form eller
/// ukjent ikraftstatus. Rapporten viser alltid begge tolkningene.
/// </para>
/// <para>
/// <b>Premissporten</b> (<see cref="FunnTjeneste.MuligBortfaltVises"/>): funnet vises ikke som «mulig bortfalt» før vurderingspunktet
/// «Ny hjemmel» er vurdert eller merket «ikke vurdert». Regelen lager aldri det punktet selv.
/// </para>
/// <para>
/// <b>Lagring og idempotens:</b> status <c>oppdaget</c>, nøkkel <c>hjemmel-opphevet:&lt;ELI uten prefiks&gt;</c>. En ny kjøring gir 0 nye, gjenåpner
/// ingenting og endrer ikke lagrede funn; et funn der forskriften ikke lenger gir funn, telles i <c>UtenGrunnlag</c>. Ingenting publiseres: funnene
/// står som oppdaget til #446 er klassifisert og juristen har målt presisjonen (Johanns avgjørelse 2026-11-18 (4)).
/// </para>
/// </summary>
public sealed class HjemmelOpphevetFunnregel(RegelIdeDbContext db, FunnTjeneste funnTjeneste)
{
    public const string OppdagelsesKilde = "monster:hjemmel-opphevet";
    public const string NokkelPrefiks = "hjemmel-opphevet:";
    /// <summary>[ENDRET, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1] Forskriften selv opphevet (R8), kunngjorte versjoner,
    /// forklart bort og oppfrisket.</summary>
    public const string Regelversjon = "472-pr4-2026-11-24";

    /// <summary>[Bryter] Om en forskrift der #446-kolonnen <c>rent_endringsvedtak</c> er NULL, gir funn. Standard <see cref="StandardIkkeKlassifisertGirFunn"/>.
    /// Se klassekommentaren.</summary>
    public const string KonfignokkelIkkeKlassifisertGirFunn = "RegelIde:Hjemmelsstatus:IkkeKlassifisertGirFunn";

    /// <summary>[LÅST, prosjekteierens avgjørelse 2026-11-24 i #472 (1)] Uklassifiserte forskrifter gir funn med premissmangelen for #446
    /// (tolkning B). Bryteren står, så den kan slås av med <c>false</c> i konfigurasjonen.</summary>
    public const bool StandardIkkeKlassifisertGirFunn = true;

    public const string UndertypeHjemmelOpphevet = Hjemmelsstatus.SamletHjemmelOpphevet;
    public const string UndertypeBlandede = Hjemmelsstatus.SamletBlandede;
    public const string UndertypeUavklart = Hjemmelsstatus.SamletUavklart;
    public const string UndertypeVidereforingUtlopt = Hjemmelsstatus.SamletVidereforingUtlopt;
    public const string UndertypeGjenstandslos = Hjemmelsstatus.Gjenstandslos;
    public static readonly string[] Undertyper =
        [UndertypeHjemmelOpphevet, UndertypeBlandede, UndertypeUavklart, UndertypeVidereforingUtlopt, UndertypeGjenstandslos];

    public const string UtfallForeslatt = "foreslatt";
    public const string UtfallRentEndringsvedtak = "utelatt_rent_endringsvedtak";
    public const string UtfallIkkeKlassifisert = "utelatt_ikke_klassifisert_446";
    /// <summary>[Ny, #472 PR 4] Forskriften er selv opphevet med tekstbevis i en annen gjeldende kilde i korpus (R8).</summary>
    public const string UtfallForskriftenOpphevet = "utelatt_forskriften_opphevet";
    /// <summary>[Ny, #472 PR 4] Forskriften har ingen undertype (aktiv, videreført eller ukjent), men et lagret funn står.</summary>
    public const string UtfallIngenUndertype = "ingen_undertype";

    /// <summary>[Ny, #472 PR 4] Kildeformen for vedtak slik de ble kunngjort i Norsk Lovtidend avd. I (<see cref="RettskildeEntitet.Kildeform"/>).</summary>
    public const string KildeformKunngjort = "kunngjort";

    private sealed record Kilde(Guid Id, string? Eli, string? DokumentId, string Tittel, string Kildetype, string Entitetsstatus, string Importrolle,
        bool? RentEndringsvedtak, string? Kildeform);

    /// <param name="oppfrisk">[Ny, #472 PR 4] Med <paramref name="skriv"/>: skriv tittel, påstand, undertype og koblinger på nytt for lagrede funn
    /// som står som oppdaget og som ingen har rørt (sist endret av ingen eller av regelen), som <c>?oppfrisk=true</c> i #429.</param>
    public async Task<HjemmelOpphevetRapport> KjorAsync(bool skriv, string bruker, bool ikkeKlassifisertGirFunn,
        IReadOnlyCollection<Guid>? forskriftIder = null, bool medRader = false, CancellationToken ct = default, bool oppfrisk = false)
    {
        var kilder = await db.Rettskilder.AsNoTracking()
            .Select(r => new Kilde(r.Id, r.Eli, r.DokumentId, r.Kortnavn ?? r.Tittel, r.Kildetype, r.Entitetsstatus, r.Importrolle, r.RentEndringsvedtak,
                r.Kildeform))
            .ToDictionaryAsync(r => r.Id, ct);
        var rader = await db.Hjemmelsstatuser.AsNoTracking().ToListAsync(ct);
        var perF = rader.GroupBy(r => r.RettskildeId).ToDictionary(g => g.Key, g => g.OrderBy(r => r.HjemmelEid, StringComparer.Ordinal).ToList());
        IReadOnlyList<HjemmelPremiss>? Premisser(Guid id) => perF.TryGetValue(id, out var l)
            ? l.Select(b => new HjemmelPremiss(b.RettskildeHjemmelId, b.HjemmelEid, b.Rolle, b.Status, b.Kunnskapsstatus,
                b.MalRettskildeId is { } m && kilder.GetValueOrDefault(m)?.Kildetype == "Forskrift" ? m : null, b.VidereforingDekning)).ToList()
            : null;

        var omfang = perF.Keys
            .Where(id => kilder.GetValueOrDefault(id) is { Kildetype: "Forskrift", Entitetsstatus: "gjeldende", Importrolle: "primaer", Eli: not null })
            .Where(id => forskriftIder is null || forskriftIder.Contains(id))
            .OrderBy(id => kilder[id].DokumentId, StringComparer.Ordinal).ToList();
        var memo = new Dictionary<Guid, SamletHjemmelsstatus>();
        var samlet = omfang.ToDictionary(id => id, id => HjemmelsstatusSamlet.Beregn(id, Premisser, memo));

        // Ikrafttredelse som eneste disposisjonstype: telles, utelates ikke (se rapporten).
        var kandidatIder = samlet.Where(s => Undertype(s.Value) is not null).Select(s => s.Key).ToList();
        var disposisjoner = new Dictionary<Guid, HashSet<string>>();
        foreach (var bit in kandidatIder.Chunk(2000))
        {
            foreach (var d in await db.RettskildeDisposisjonstyper.AsNoTracking().Where(d => bit.Contains(d.RettskildeId))
                         .Select(d => new { d.RettskildeId, d.Disposisjonstype }).ToListAsync(ct))
            {
                if (!disposisjoner.TryGetValue(d.RettskildeId, out var s)) disposisjoner[d.RettskildeId] = s = [];
                s.Add(d.Disposisjonstype);
            }
        }

        // [Ny, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1 (H08, H13, H16)] Er forskriften selv opphevet i sin helhet av en
        // annen gjeldende kilde i korpus, gir den ikke funn. Samme lesing som R8 i #429 (Opphevingskontroll.OpphevetAvAnnenKildeAsync): kilden
        // står med forskriften i «Endrer», hel opphevingssetning eller den utvidede formen gjennom vakten, og formelen i foreldrenoden. SF og NL
        // holdes utenfor som i R8, fordi Lovdata vedlikeholder statusen for dem. Fem av åtte feil i måling 1 var forskrifter som selv er
        // opphevet (alle LTI); for tre av dem står opphevingen i korpus.
        var opphevetSelv = await Opphevingskontroll.OpphevetAvAnnenKildeAsync(db,
            kandidatIder.Select(id => (id, kilder[id].Eli!)).ToList(), ct);

        // [Ny, #446 PR 2, AK 7] Et rent vedtak der et mål i «Endrer» ikke er i korpus, utelates ikke stille: funnet står med premissmangelen
        // «Mål utenfor korpus» (FunnTjeneste), fordi teksten i målet ikke kan kontrolleres.
        var rentIder = kandidatIder.Where(id => kilder[id].RentEndringsvedtak == true).ToList();
        var rentGrunnlag = await db.Rettskilder.AsNoTracking().Where(r => rentIder.Contains(r.Id))
            .Select(r => new { r.Id, r.RentEndringsvedtakGrunnlag }).ToDictionaryAsync(r => r.Id, r => r.RentEndringsvedtakGrunnlag, ct);
        var malUtenfor = await VedtaksformTjeneste.MalUtenforKorpusAsync(db,
            kandidatIder.Where(id => kilder[id].RentEndringsvedtak == true).ToList(), ct);

        var nokler = await db.Funn.AsNoTracking().Where(f => f.EksternNokkel != null && f.EksternNokkel.StartsWith(NokkelPrefiks))
            .Include(f => f.Vurderingspunkter)
            .ToDictionaryAsync(f => f.EksternNokkel!, ct);

        var tellerA = new List<(string Undertype, string Prefiks)>();
        var tellerB = new List<(string Undertype, string Prefiks)>();
        var kandidatUndertyper = new List<string>();
        var foreslattUndertyper = new List<string>();
        var foreslattPrefiks = new List<string>();
        var opphevetPrefiks = new List<string>();
        var bortGrunner = new List<string>();
        int rent = 0, rentMalUtenfor = 0, ikraft = 0, ikkeKlassifisert = 0, nye = 0, finnes = 0, finnesBort = 0, undertypeEndret = 0, holdt = 0, vises = 0;
        int kunngjort = 0, forklaresBort = 0, forklartBortSatt = 0, vurdertIkkeRort = 0, oppfriskes = 0, oppfrisket = 0;
        var behandledeNokler = new HashSet<string>(StringComparer.Ordinal);
        var radliste = new List<HjemmelOpphevetRad>();

        foreach (var id in omfang)
        {
            var s = samlet[id];
            var f = kilder[id];
            var nokkel = NokkelPrefiks + UtgattAktorFunnregel.EliKort(f.Eli!);
            var finnesFunn = nokler.GetValueOrDefault(nokkel);
            var prefiks = (f.DokumentId ?? "(ingen)").Split('/')[0];
            var undertype = Undertype(s);
            if (undertype is null && finnesFunn is null) continue;

            string utfall;
            string? bevis = null;
            if (undertype is null)
            {
                utfall = UtfallIngenUndertype;
                bevis = $"Den samlede hjemmelsstatusen er nå «{s.Status}» (hjemmelsstatus {string.Join(", ", perF[id].Select(r => r.Regelversjon).Distinct())}), "
                    + "og den gir ikke funn.";
            }
            else
            {
                kandidatUndertyper.Add(undertype);
                if (f.RentEndringsvedtak != true && disposisjoner.GetValueOrDefault(id) is { Count: 1 } d && d.Contains(Disposisjonstyper.Ikrafttredelse)) ikraft++;
                if (opphevetSelv.TryGetValue(id, out var o))
                {
                    utfall = UtfallForskriftenOpphevet;
                    opphevetPrefiks.Add(prefiks);
                    bevis = $"Forskriften er selv opphevet av {o.Etikett} (eId {o.Eid}): «{Opphevingskontroll.Kort(o.Sitat)}». Samme lesing som R8 i #429.";
                }
                else if (f.RentEndringsvedtak == true && malUtenfor.ContainsKey(id))
                {
                    // [Ny, #446 PR 2, AK 7] Ren og i kraft, men et mål er utenfor korpus: funnet står, med premissmangel.
                    rentMalUtenfor++;
                    tellerA.Add((undertype, prefiks));
                    tellerB.Add((undertype, prefiks));
                    utfall = UtfallForeslatt;
                }
                else if (f.RentEndringsvedtak == true)
                {
                    utfall = UtfallRentEndringsvedtak;
                    rent++;
                    // [ENDRET, #446 PR 2] «Uttømt virkning» er nå avledet av vedtaksformen og ikraftstatusen.
                    bevis = $"Forskriften har uttømt virkningen (#446): {rentGrunnlag.GetValueOrDefault(id) ?? "ren vedtaksform og i kraft"}";
                }
                else if (f.RentEndringsvedtak is null)
                {
                    ikkeKlassifisert++;
                    tellerB.Add((undertype, prefiks));
                    utfall = ikkeKlassifisertGirFunn ? UtfallForeslatt : UtfallIkkeKlassifisert;
                }
                else
                {
                    tellerA.Add((undertype, prefiks));
                    tellerB.Add((undertype, prefiks));
                    utfall = UtfallForeslatt;
                }
            }

            var forklares = false;
            if (utfall == UtfallForeslatt)
            {
                behandledeNokler.Add(nokkel);
                foreslattUndertyper.Add(undertype!);
                foreslattPrefiks.Add(prefiks);
                if (f.Kildeform == KildeformKunngjort) kunngjort++;
                var muligBortfalt = undertype is UndertypeHjemmelOpphevet or UndertypeVidereforingUtlopt;
                if (finnesFunn is null)
                {
                    nye++;
                    if (muligBortfalt) holdt++;
                    if (skriv) await funnTjeneste.OpprettAsync(ByggFunn(nokkel, f, s, undertype!, kilder, perF), bruker, ct);
                }
                else
                {
                    finnes++;
                    if (finnesFunn.Status == Funn.ForklartBort) finnesBort++;
                    if (finnesFunn.Undertype != undertype) undertypeEndret++;
                    if (muligBortfalt && finnesFunn.Status != Funn.ForklartBort)
                    {
                        if (FunnTjeneste.NyHjemmelVurdert(finnesFunn)) vises++; else holdt++;
                    }
                    // [Ny, #472 PR 4] Oppfrisking som i #429: bare et funn som står som oppdaget og som ingen har rørt.
                    if (finnesFunn.Status == Funn.Oppdaget && (finnesFunn.SistEndretAv is null || finnesFunn.SistEndretAv == OppdagelsesKilde))
                    {
                        var nytt = ByggFunn(nokkel, f, s, undertype!, kilder, perF);
                        if (nytt.Tittel != finnesFunn.Tittel || nytt.Beskrivelse != finnesFunn.Beskrivelse || nytt.Undertype != finnesFunn.Undertype)
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
            }
            else if (utfall == UtfallIkkeKlassifisert)
            {
                // Bryteren holder funnet tilbake; det er ikke regelen som sier at forskriften ikke gir funn. Lagrede funn røres ikke (telles i
                // utenGrunnlag, som før).
            }
            else if (finnesFunn is not null)
            {
                // [Ny, #472 PR 4] Regelen sier at forskriften ikke gir funn: et lagret funn som står som oppdaget, forklares bort maskinelt, som
                // i #429 (FunnTjeneste.ForklarBortMaskineltAsync). Funnet slettes aldri. Et funn et menneske har vurdert eller gjenåpnet, røres ikke.
                behandledeNokler.Add(nokkel);
                if (finnesFunn.Status == Funn.Oppdaget && finnesFunn.GjenapnetTidspunkt is null)
                {
                    forklares = true;
                    forklaresBort++;
                    bortGrunner.Add(utfall == UtfallIngenUndertype ? $"samlet_{s.Status}" : utfall);
                    if (skriv)
                    {
                        var begrunnelse = $"Forklart bort av maskinregelen {OppdagelsesKilde} (regelversjon {Regelversjon}, #472 PR 4, prosjekteierens "
                            + $"avgjørelse 2026-11-24 etter måling 1). {bevis}";
                        if (await funnTjeneste.ForklarBortMaskineltAsync(finnesFunn.Id, begrunnelse, OppdagelsesKilde, bruker, ct)) forklartBortSatt++;
                    }
                }
                else if (finnesFunn.Status != Funn.ForklartBort)
                {
                    vurdertIkkeRort++;
                }
            }

            if (medRader)
            {
                radliste.Add(new HjemmelOpphevetRad(f.DokumentId, f.Tittel, nokkel, undertype, s.Status, s.Kunnskapsstatus, f.RentEndringsvedtak, utfall,
                    finnesFunn?.Status, undertype is null ? [] : HjemmelLinjer(s, undertype, kilder, perF[id]), f.Kildeform, forklares, bevis,
                    finnesFunn?.Undertype));
            }
        }

        // Lagrede funn fra regelen som ikke er behandlet over: forskriften er ikke lenger i omfanget, eller bryteren for #446 holder dem tilbake.
        // Bare innenfor omfanget når det er avgrenset.
        var omfangEli = forskriftIder is null ? null : omfang.Select(id => NokkelPrefiks + UtgattAktorFunnregel.EliKort(kilder[id].Eli!)).ToHashSet(StringComparer.Ordinal);
        var utenGrunnlag = nokler.Keys.Count(k => !behandledeNokler.Contains(k) && (omfangEli is null || omfangEli.Contains(k)));

        static IReadOnlyDictionary<string, int> Tell(IEnumerable<string> v) =>
            v.GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());
        static HjemmelOpphevetTolkning Tolkning(List<(string Undertype, string Prefiks)> l) =>
            new(l.Count, Tell(l.Select(x => x.Undertype)), Tell(l.Select(x => x.Prefiks)));

        var hsVersjon = rader.Select(r => r.Regelversjon).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
        return new HjemmelOpphevetRapport(
            skriv, Regelversjon, rader.Count > 0, hsVersjon.Count == 0 ? null : string.Join(", ", hsVersjon), ikkeKlassifisertGirFunn,
            samlet.Count, Tell(samlet.Values.Select(s => s.Status)), samlet.Values.Count(s => s.Gjenstandslos),
            kandidatUndertyper.Count, Tell(kandidatUndertyper), rent, ikraft, ikkeKlassifisert, Tolkning(tellerA), Tolkning(tellerB),
            foreslattUndertyper.Count, Tell(foreslattUndertyper), nye, finnes, finnesBort, undertypeEndret, holdt, vises, utenGrunnlag,
            medRader ? radliste : null,
            opphevetPrefiks.Count, Tell(opphevetPrefiks), kunngjort, forklaresBort, Tell(bortGrunner), forklartBortSatt, vurdertIkkeRort,
            oppfriskes, oppfrisket, Tell(foreslattPrefiks), rentMalUtenfor);
    }

    /// <summary>Undertypen for en samlet status, eller null når forskriften ikke gir funn (aktiv, videreført, ukjent uten gjenstandsløs).</summary>
    public static string? Undertype(SamletHjemmelsstatus s) => s.Status switch
    {
        Hjemmelsstatus.SamletHjemmelOpphevet => UndertypeHjemmelOpphevet,
        Hjemmelsstatus.SamletBlandede => UndertypeBlandede,
        Hjemmelsstatus.SamletUavklart => UndertypeUavklart,
        Hjemmelsstatus.SamletVidereforingUtlopt => UndertypeVidereforingUtlopt,
        _ => s.Gjenstandslos ? UndertypeGjenstandslos : null,
    };

    /// <summary>«LOV-1993-06-11-65» av en dokument-ELI; ukjent form → ELI-en som den er.</summary>
    public static string? EliReferanse(string? eli) => Hjemmelstekstlesing.Mal(eli) is { } m
        ? $"{(m.Type == "lov" ? "LOV" : "FOR")}-{m.Aar:0000}-{m.Mnd:00}-{m.Dag:00}{(m.Nr.Length > 0 ? "-" + m.Nr : "")}"
        : eli;

    /// <summary>[Ny, #472 PR 4] Linjen i påstanden for en kunngjort versjon. Samme ordlyd som premissmangelen
    /// (<see cref="FunnTjeneste.KunngjortMangelStart"/>).</summary>
    public const string KunngjortMangelLinje = FunnTjeneste.KunngjortMangelStart + ": korpuset har bare vedtaket slik det ble kunngjort i "
        + "Norsk Lovtidend, og Lovdatas ajourførte status er ikke slått opp. Vedtaket kan selv være opphevet ved en kilde som ikke er i korpus.";

    private static string Referanse(Kilde k) => UtgattAktorFunnregel.Dokumentreferanse(k.DokumentId) ?? EliReferanse(k.Eli) ?? k.Tittel;

    private static string Bestemmelse(string? eid, string? dokumentEli) =>
        eid is null || eid == dokumentEli ? "" : " " + Hjemmelstekstlesing.Bestemmelsesetikett(eid);

    private static string Kildetekst(Guid? id, string? eid, Dictionary<Guid, Kilde> kilder) =>
        id is { } g && kilder.TryGetValue(g, out var k) ? Referanse(k) + Bestemmelse(eid, k.Eli) : eid ?? "(ukjent kilde)";

    /// <summary>Sitatet i anførselstegn, med punktum etter bare når sitatet ikke selv slutter med et skilletegn.</summary>
    private static string Sitat(string sitat)
    {
        var k = Kort(sitat).TrimEnd();
        return $"«{k}»" + (k.Length > 0 && ".!?:;…".Contains(k[^1]) ? "" : ".");
    }

    private static string Kort(string sitat) => sitat.Length <= 300 ? sitat : sitat[..297].TrimEnd() + " …";

    /// <summary>Linjene om hjemlene som bærer funnet: de som faller (og som teller), de som bærer i blandede hjemler, og gjenstanden som er
    /// opphevet. Samme linjer i påstanden og i rapporten.</summary>
    private static List<string> HjemmelLinjer(SamletHjemmelsstatus s, string undertype, Dictionary<Guid, Kilde> kilder,
        IReadOnlyList<HjemmelsstatusEntitet> egne)
    {
        var lagret = egne.ToDictionary(r => r.RettskildeHjemmelId);
        // (dokumentet, bestemmelsen, resten av setningen). Bestemmelser i samme dokument med samme resten slås sammen til én linje: tolloven
        // 2007 står som hjemmel med åtte paragrafer i FOR-2016-12-02-1426, og alle er opphevet av samme setning.
        var deler = new List<(string Dok, string Best, string Hale)>();
        foreach (var p in s.Premisser.Where(p => p.Teller || p.Rad.Rolle == Hjemmelsstatus.Gjenstand))
        {
            var r = lagret[p.Rad.HjemmelId];
            var dok = Dokumenttekst(r, kilder);
            var best = Bestemmelse(r.HjemmelEid, r.HjemmelEli).Trim();
            if (p.Rad.Rolle == Hjemmelsstatus.Gjenstand)
            {
                if (p.Status == Hjemmelsstatus.Gjenstandslos)
                    deler.Add(("Gjenstanden " + dok, best, $"er opphevet av {Kildetekst(r.OpphevetAvRettskildeId, r.OpphevetAvEid, kilder)}"
                        + (r.OpphevetAvSitat is null ? "." : $": {Sitat(r.OpphevetAvSitat)}")));
                continue;
            }
            if (p.Arvet is { } a && Hjemmelsstatus.Faller(p.Status))
            {
                deler.Add((dok, best, $"er selv en forskrift der {(a.Status == Hjemmelsstatus.SamletVidereforingUtlopt ? "videreføringen er utløpt" : "hjemmelen er opphevet uten funnet videreføring")}. Statusen arves (svakeste premiss)."));
                continue;
            }
            if (Hjemmelsstatus.Faller(p.Status))
            {
                var rest = $"er opphevet av {Kildetekst(r.OpphevetAvRettskildeId, r.OpphevetAvEid, kilder)}"
                    + (r.OpphevetFraKunngjort ? " (lest i den kunngjorte teksten i Norsk Lovtidend)" : "")
                    + (r.OpphevetAvSitat is null ? "." : $": {Sitat(r.OpphevetAvSitat)}");
                if (p.Status == Hjemmelsstatus.VidereforingUtlopt)
                {
                    rest += $" Videreført av {Kildetekst(r.VidereforingRettskildeId, r.VidereforingEid, kilder)}"
                        + (r.VidereforingTidsfrist is { } d ? $" fram til {d:dd.MM.yyyy}" : "") + ", og fristen er passert"
                        + (r.VidereforingSitat is null ? "." : $": {Sitat(r.VidereforingSitat)}");
                }
                deler.Add((dok, best, rest));
                if (r.Premissmangel is { } pm) deler.Add(("", "", $"Ikke kontrollert: {pm}"));
                continue;
            }
            if (undertype is UndertypeBlandede or UndertypeUavklart && Hjemmelsstatus.Baerer(p.Status))
            {
                var rolle = p.Rad.Rolle is null ? "rollen er ikke vurdert" : $"rolle {p.Rad.Rolle}";
                // [Ny, #472 PR 4, H14] Videreføringen nevner ikke vedtakstypen: den bærer ikke alene.
                if (p.Status == Hjemmelsstatus.Viderefort && r.VidereforingDekning == Hjemmelsstatus.DokumenttypeIkkeNevnt)
                    rolle += ", og videreføringen nevner ikke denne typen vedtak, så om den dekker forskriften, er ikke vurdert";
                deler.Add((dok, best, p.Status == Hjemmelsstatus.Viderefort
                    ? $"er opphevet, men videreført av {Kildetekst(r.VidereforingRettskildeId, r.VidereforingEid, kilder)} ({rolle})."
                    : $"er {(p.Status == Hjemmelsstatus.Erstattet ? "erstattet" : "aktiv")} ({rolle})."));
            }
        }
        return deler.GroupBy(d => (d.Dok, d.Hale)).Select(g =>
        {
            var best = g.Select(d => d.Best).Where(b => b.Length > 0).Distinct(StringComparer.Ordinal)
                .OrderBy(b => System.Text.RegularExpressions.Regex.Replace(b, @"\d+", m => m.Value.PadLeft(6, '0')), StringComparer.Ordinal).ToList();
            var hvem = g.Key.Dok.Length == 0 ? "" : (g.Key.Dok + (best.Count == 0 ? "" : " " + Liste(best)) + " ");
            return $"- {hvem}{g.Key.Hale}";
        }).ToList();
    }

    private static string Liste(List<string> l) => l.Count == 1 ? l[0] : string.Join(", ", l.Take(l.Count - 1)) + " og " + l[^1];

    /// <summary>Hjemmelsdokumentet uten bestemmelsen: «LOV-1993-06-11-65».</summary>
    private static string Dokumenttekst(HjemmelsstatusEntitet r, Dictionary<Guid, Kilde> kilder) =>
        r.MalRettskildeId is { } m && kilder.TryGetValue(m, out var mk) ? Referanse(mk) : EliReferanse(r.HjemmelEli) ?? r.HjemmelEid;

    private static NyttFunn ByggFunn(string nokkel, Kilde f, SamletHjemmelsstatus s, string undertype, Dictionary<Guid, Kilde> kilder,
        Dictionary<Guid, List<HjemmelsstatusEntitet>> perF)
    {
        var referanse = Referanse(f);
        var navn = undertype switch
        {
            UndertypeHjemmelOpphevet => "Hjemmel opphevet, videreføring ikke funnet",
            UndertypeBlandede => "Blandede hjemler, én opphevet uten funnet videreføring",
            UndertypeUavklart => "Hjemmel opphevet, de andre hjemlene ikke vurdert",
            UndertypeVidereforingUtlopt => "Videreføringen av hjemmelen er utløpt",
            _ => "Gjenstandsløs: loven vedtaket handler om, er opphevet",
        };
        var kjerne = undertype switch
        {
            UndertypeHjemmelOpphevet => "Alle hjemlene som teller, er opphevet, og maskinregelen har ikke funnet en overgangsregel som viderefører forskriften. "
                + "Ikke funnet betyr ikke at den ikke finnes.",
            UndertypeBlandede => "Minst én hjemmel som teller, er opphevet uten funnet videreføring, mens andre hjemler bærer. Hvilken del av forskriften "
                + "som står uten hjemmel, er ikke avgjort.",
            // [ENDRET, #472 PR 4, H15 og H14] «De andre hjemlene er gjeldende» var unøyaktig når en av dem er opphevet og videreført.
            UndertypeUavklart => "Minst én hjemmel som teller, er opphevet uten funnet videreføring. De andre hjemlene kan bære forskriften, men det er "
                + "ikke vurdert: rollen deres er ikke avklart, eller videreføringen nevner ikke denne typen vedtak.",
            UndertypeVidereforingUtlopt => "Hjemmelen er opphevet, og overgangsregelen viderefører forskriften bare fram til en dato som er passert.",
            _ => "Loven vedtaket handler om (gjenstanden), er opphevet. Hjemmelen for vedtaket er ikke berørt.",
        };
        // [ENDRET, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1] For en kunngjort versjon (LTI) viser ikke korpuset Lovdatas
        // ajourførte status: i måling 1 var fem slike vedtak opphevet i Lovdata (DELO), selv om korpuset har dem som «gjeldende».
        var kunngjort = f.Kildeform == KildeformKunngjort;
        var forste = kunngjort
            ? $"Korpuset har «{f.Tittel}» ({referanse}) bare slik det ble kunngjort i Norsk Lovtidend, med status gjeldende."
            : $"Lovdata viser «{f.Tittel}» ({referanse}) som gjeldende.";
        var linjer = new List<string>
        {
            $"{forste} {kjerne}"
                + (undertype == UndertypeGjenstandslos ? "" : " Om forskriften fortsatt har virkning, er en tolkning: uten en overgangsregel er den i "
                    + "utgangspunktet uten rettslig virkning, men den kan gjelde så langt den kunne vært gitt etter den nye loven (Lovteknikk og "
                    + "lovforberedelse pkt. 7.3.5)."),
        };
        linjer.AddRange(HjemmelLinjer(s, undertype, kilder, perF[f.Id]));
        if (kunngjort) linjer.Add($"- {KunngjortMangelLinje}");
        if (f.RentEndringsvedtak is null)
        {
            linjer.Add("- Om forskriften har uttømt virkningen (et rent endrings-, opphevings- eller ikrafttredelsesvedtak), er ikke klassifisert (#446).");
        }

        var koblinger = new List<NyKobling> { new(Funn.GjelderBestemmelse, $"{referanse} {f.Tittel}", Merknad: "forskriften", RettskildeEli: f.Eli) };
        var lagret = perF[f.Id].ToDictionary(r => r.RettskildeHjemmelId);
        var sett = new HashSet<(string, string?, string)>();
        foreach (var p in s.Premisser.Where(p => (p.Teller || p.Rad.Rolle == Hjemmelsstatus.Gjenstand) && p.Arvet is null))
        {
            var r = lagret[p.Rad.HjemmelId];
            var hjemmel = Dokumenttekst(r, kilder);
            var opphevet = Hjemmelsstatus.Faller(p.Status) || p.Status == Hjemmelsstatus.Gjenstandslos;
            if (opphevet && r.OpphevetAvRettskildeId is { } o && kilder.GetValueOrDefault(o) is { Eli: { } oEli } ok
                && sett.Add((Funn.OpphevendeBestemmelse, r.OpphevetAvEid, oEli)))
            {
                koblinger.Add(new NyKobling(Funn.OpphevendeBestemmelse, Referanse(ok) + Bestemmelse(r.OpphevetAvEid, oEli),
                    Merknad: $"opphever {hjemmel}" + (r.OpphevetFraKunngjort ? " (lest i Norsk Lovtidend)" : ""),
                    RettskildeEli: oEli, Eid: r.OpphevetAvEid));
            }
            if ((p.Status == Hjemmelsstatus.VidereforingUtlopt || (undertype is UndertypeBlandede or UndertypeUavklart && p.Status == Hjemmelsstatus.Viderefort))
                && r.VidereforingRettskildeId is { } v && kilder.GetValueOrDefault(v) is { Eli: { } vEli } vk
                && sett.Add((Funn.Videreforingsbestemmelse, r.VidereforingEid, vEli)))
            {
                koblinger.Add(new NyKobling(Funn.Videreforingsbestemmelse, Referanse(vk) + Bestemmelse(r.VidereforingEid, vEli),
                    Merknad: $"viderefører forskrifter gitt etter {hjemmel}" + (r.VidereforingTidsfrist is { } d ? $" fram til {d:dd.MM.yyyy}" : ""),
                    RettskildeEli: vEli, Eid: r.VidereforingEid));
            }
        }

        var intern = $"Maskinfunn ({OppdagelsesKilde}, regelversjon {Regelversjon}, #472 PR 2 og PR 4), lest av hjemmelsstatus "
            + $"{string.Join(", ", perF[f.Id].Select(r => r.Regelversjon).Distinct())}. Samlet status {s.Status}, kunnskapsstatus {s.Kunnskapsstatus}. "
            + "Står som oppdaget til #446 er klassifisert og juristen har målt presisjonen (Johanns avgjørelse 2026-11-18 (4)).";
        return new NyttFunn(Funn.HjemmelOpphevet, undertype, $"{navn}: {f.Tittel} ({referanse})", string.Join("\n", linjer), OppdagelsesKilde,
            InternMerknad: intern, FunnetI: "#472", EksternNokkel: nokkel, Koblinger: koblinger);
    }
}
