namespace RegelIde.Data;

/// <summary>
/// [Ny, #472 PR 1, Johanns avgjørelser 2026-11-18] ÉN kilde for vokabularene til <see cref="HjemmelsstatusEntitet"/> — speilet av CHECK-ene i
/// <see cref="RegelIdeDbContext"/> — og for den samlede statusen, som regnes ut og aldri lagres (#345, docs/33 §4.6).
/// <para>
/// <b>Hva det svarer på:</b> «Hvilke hjemler har forskriften, hvilke av dem er opphevet, av hvilken bestemmelse, og er forskriften videreført?»
/// (#472). Lovdata viser alle disse forskriftene som gjeldende. En opphevet hjemmel uten funnet videreføring er derfor alltid en lesning av
/// premissene, ikke en påstand om at forskriften er bortfalt (Lovteknikk og lovforberedelse pkt. 7.3.5: «i utgangspunktet» uten rettslig
/// virkning, men den kan gjelde ved tolkning). Funnet heter «Hjemmel opphevet, videreføring ikke funnet» (mulig bortfalt), Johanns avgjørelse
/// 2026-11-18 (2).
/// </para>
/// </summary>
public static class Hjemmelsstatus
{
    public const string Regel = "monster:hjemmelsstatus";

    /// <summary>Versjonen av regelen. Økes når reglene endres, så en lagret rad viser hvilken regel som satte den.
    /// [ENDRET, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1] Rollen for en forskrift som hjemmel (H15) og endringslovens
    /// egne deler (H18).</summary>
    public const string Regelversjon = "472-pr5-2026-11-25";

    // ---- Rolle (juristrunde 1 R1, godtatt i svaret; runde 2 R1: NULL = ikke vurdert) ----
    /// <summary>Rettsgrunnlaget for innholdet.</summary>
    public const string Materiell = "materiell";
    /// <summary>Viser hvem som hadde myndigheten («jf. kgl.res.», et delegeringsvedtak, Grl. § 12), ikke grunnlaget for innholdet. At et
    /// kompetanseledd oppheves, gjør ikke vedtaket ugyldig (juristrunde 1, svaret på S01).</summary>
    public const string Kompetanseledd = "kompetanseledd";
    /// <summary>En overgangsbestemmelse som bærer forskriften videre (kommuneloven 2018 § 31-5, fagskoleloven 2018 § 44).</summary>
    public const string Overgangsgrunnlag = "overgangsgrunnlag";
    /// <summary>Loven vedtaket handler om, ikke hjemmelen (S08: tolloven § 6-4 i et ressortvedtak etter Grl. § 12).</summary>
    public const string Gjenstand = "gjenstand";
    public static readonly string[] Roller = [Materiell, Kompetanseledd, Overgangsgrunnlag, Gjenstand];

    // ---- Status per hjemmel ----
    public const string Aktiv = "aktiv";
    /// <summary>Opphevet med tekstbevis, og ingen videreføring funnet. «Ikke funnet» betyr ikke at den ikke finnes.</summary>
    public const string Opphevet = "opphevet";
    /// <summary>Bestemmelsen er merket opphevet i en ellers gjeldende lov. Videreføringen står i endringsloven, som ikke er i korpus, og er
    /// ikke undersøkt (juristrunde 1 R5, godtatt).</summary>
    public const string OpphevetBestemmelse = "opphevet_bestemmelse";
    public const string Viderefort = "viderefort";
    /// <summary>Videreført med en frist som er passert (S20: «frem til 31. desember 2019»).</summary>
    public const string VidereforingUtlopt = "videreforing_utlopt";
    /// <summary>Et opphevet kompetanseledd der vedtaket som opphever det, selv tildeler ny kompetanse (S01: FOR-1993-11-12-1022 og
    /// LTI/forskrift/2004-04-16-631 kap. I). Kunnskapsstatus fortolkning.</summary>
    public const string Erstattet = "erstattet";
    /// <summary>[Johanns avgjørelse 2026-11-18 (5): eget merke] Gjenstanden for vedtaket er opphevet (S08).</summary>
    public const string Gjenstandslos = "gjenstandslos";
    public const string Ukjent = "ukjent";
    public static readonly string[] Statuser = [Aktiv, Opphevet, OpphevetBestemmelse, Viderefort, VidereforingUtlopt, Erstattet, Gjenstandslos, Ukjent];

    // ---- Bevistrinn for en oppheving lest av teksten (fase 1, m2) ----
    /// <summary>Hel opphevingssetning i en gjeldende primærkilde som har målet i Lovdatas «Endrer»-felt.</summary>
    public const string E1 = "E1";
    /// <summary>Som E1, men i utvidet form: passiv, «til» etter nummeret, eller hele noden som én setning.</summary>
    public const string E1u = "E1u";
    /// <summary>Hel opphevingssetning uten «Endrer»-kobling, med vakten mot et mål i tittelen til noe annet.</summary>
    public const string E2 = "E2";
    /// <summary>[Johanns avgjørelse 2026-11-18 (3)] «Endrer» finnes, men den konsoliderte teksten har mistet opphevingssetningen; setningen er
    /// lest i den kunngjorte teksten i Norsk Lovtidend avd. I.</summary>
    public const string EnLovtidend = "EN_LTI";
    public static readonly string[] Bevistrinn = [E1, E1u, E2, EnLovtidend];

    // ---- Hvorfor en hjemmel er ukjent ----
    public const string GrunnEn = "EN";
    public const string GrunnEnd = "END";
    public const string GrunnE3 = "E3";
    public const string GrunnIngen = "INGEN";
    public const string GrunnEidIkkeFunnet = "eid_ikke_funnet";
    public const string GrunnIkkeIKraft = "oppheving_ikke_i_kraft";
    public const string GrunnMangler = "mangler_i_korpus";
    public const string GrunnBestemmelseOpphevet = "bestemmelse_opphevet";

    // ---- Videreføringen ----
    public static readonly string[] Videreforingsformer = ["V1", "V2", "V2a", "V3", "V4"];
    public const string Dekket = "dekket";
    /// <summary>Setningen nevner ikke dokumenttypen (bare «forskrifter» om et delegeringsvedtak): fortolkning, ikke ukjent (R3).</summary>
    public const string DokumenttypeIkkeNevnt = "dokumenttype_ikke_nevnt";
    /// <summary>Setningen avgrenser til kapitler eller paragrafer, og hjemmelen er hele dokumentet: avgrensningen kan ikke sammenlignes.</summary>
    public const string AvgrensningIkkeKontrollert = "avgrensning_ikke_kontrollert";
    public static readonly string[] Dekninger = [Dekket, DokumenttypeIkkeNevnt, AvgrensningIkkeKontrollert];

    // ---- Samlet status (regnes ut, lagres ikke) ----
    public const string SamletAktiv = "aktiv";
    public const string SamletViderefort = "viderefort";
    public const string SamletVidereforingUtlopt = "videreforing_utlopt";
    /// <summary>«Hjemmel opphevet, videreføring ikke funnet» (mulig bortfalt): alle premissene er opphevet uten funnet videreføring.</summary>
    public const string SamletHjemmelOpphevet = "hjemmel_opphevet";
    /// <summary>Minst én premiss opphevet uten videreføring og minst én som bærer. Delen kan ikke pekes ut uten fortolkning (juristrunde 2 R6).</summary>
    public const string SamletBlandede = "blandede_hjemler";
    /// <summary>[Juristrunde 2 R1, modellereren ga juristen rett] Det eneste som bærer, er hjemler med rolle NULL (ikke vurdert).</summary>
    public const string SamletUavklart = "uavklart";
    public const string SamletUkjent = "ukjent";

    /// <summary>[Ny, #472 PR 4, H15] Rolleregelen for en hjemmel som selv er en forskrift eller et vedtak, når ingen annen regel gir rollen.</summary>
    public const string RolleregelForskriftSomHjemmel = "forskrift_som_hjemmel";

    /// <summary>Teller i den samlede statusen: materiell og NULL. Er det ingen slike, teller kompetanseledd og overgangsgrunnlag. Gjenstand teller
    /// aldri (den er ikke hjemmel).</summary>
    internal static bool ErMateriellPremiss(string? rolle) => rolle is null or Materiell;

    internal static bool Baerer(string status) => status is Aktiv or Viderefort or Erstattet;

    /// <summary>
    /// [Ny, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1, H14 og juristrunde 2 R1] Om en premiss som bærer, er vurdert. Den er
    /// ikke vurdert når rollen er NULL, eller når den bare bærer ved en videreføring med dekningen <see cref="DokumenttypeIkkeNevnt"/>: setningen
    /// viderefører «forskrifter», og om det dekker et delegeringsvedtak, er fortolkning (finansforetaksloven § 23-2 andre ledd om
    /// DEL/forskrift/2003-06-20-730 i måling 1). En slik premiss bærer ikke alene: står bare slike igjen når noe faller, er den samlede statusen
    /// uavklart, ikke blandede hjemler. Når ingenting faller, endrer den ingenting (FOR-1993-12-17-1314 er fortsatt videreført, fase 1 AK 2).
    /// [ENDRET, #501, prosjekteierens avgjørelse 2026-11-25] Den gjelder også når ingenting faller, se <see cref="BaererBareUvurdertVidereforing"/>.
    /// AK2 er fortsatt videreført, fordi en annen premiss bærer og er vurdert.
    /// </summary>
    internal static bool BaererVurdert(HjemmelPremiss p, string effektivStatus) =>
        p.Rolle is not null && !(effektivStatus == Viderefort && p.VidereforingDekning == DokumenttypeIkkeNevnt);
    internal static bool Faller(string status) => status is Opphevet or VidereforingUtlopt;

    /// <summary>
    /// [Ny, #501, prosjekteierens avgjørelse 2026-11-25 etter måling 3, alternativ (a)] Når ingenting faller: minst én premiss bærer bare ved en
    /// videreføring med dekningen <see cref="DokumenttypeIkkeNevnt"/>, og ingen premiss som bærer, er vurdert (<see cref="BaererVurdert"/>). Da er
    /// samlet status uavklart, ikke videreført. Viltressursloven § 61 andre ledd viderefører «forskrifter»; om det dekker et delegeringsvedtak etter
    /// viltloven § 39 (DEL/forskrift/2002-09-13-995), er fortolkning (måling 3, E09–E11 og E15). Uten en slik videreføring endrer regelen ingenting:
    /// en forskrift med bare aktive hjemler med rolle NULL er fortsatt aktiv. Står en vurdert premiss som bærer ved siden av, er den samlede
    /// statusen fortsatt videreført (AK2, FOR-1993-12-17-1314, og E08, DEL/forskrift/1996-10-25-1008).
    /// </summary>
    internal static bool BaererBareUvurdertVidereforing(IEnumerable<(HjemmelPremiss Rad, string Status)> baerer)
    {
        var liste = baerer.ToList();
        return liste.Any(p => p.Status == Viderefort && p.Rad.VidereforingDekning == DokumenttypeIkkeNevnt)
            && liste.All(p => !BaererVurdert(p.Rad, p.Status));
    }
}

/// <summary>
/// [Ny, #472 PR 1] Statusen til ÉN hjemmelsrad (<see cref="RettskildeHjemmelEntitet"/>), avledet av maskinregelen
/// (<see cref="HjemmelsstatusRegel"/>). Importtabellen røres ikke; denne tabellen skrives på nytt ved hver kjøring med <c>?skriv=true</c>.
/// Felt som er NULL, er ikke funnet — ikke oppfunnet (CLAUDE.md §8).
/// </summary>
public sealed class HjemmelsstatusEntitet
{
    public Guid Id { get; set; }
    public Guid RettskildeHjemmelId { get; set; }
    /// <summary>Forskriften hjemmelen står i.</summary>
    public Guid RettskildeId { get; set; }
    public required string HjemmelEid { get; set; }
    /// <summary>Dokument-ELI-en til hjemmelen.</summary>
    public string? HjemmelEli { get; set; }
    /// <summary>Den gjeldende primærkilden med ELI-en, når den finnes (også når hjemmelsraden peker på en eldre versjon).</summary>
    public Guid? MalRettskildeId { get; set; }

    public string? Rolle { get; set; }
    /// <summary>Regelen som satte rollen (normalisering), f.eks. «delegeringsvedtak», «jf_vedtak», «grl_12».</summary>
    public string? RolleRegel { get; set; }

    public required string Status { get; set; }
    public string? Bevistrinn { get; set; }
    public string? UkjentGrunn { get; set; }
    /// <summary>[R5] Hjemmelen er en endringslov som navngir én lov; statusen gjelder loven den endrer.</summary>
    public string? FulgtEli { get; set; }

    public Guid? OpphevetAvRettskildeId { get; set; }
    public Guid? OpphevetAvNodeId { get; set; }
    public string? OpphevetAvEid { get; set; }
    public string? OpphevetAvSitat { get; set; }
    /// <summary>Ikrafttredelsen til kilden som opphever (dokumentnivå, R2): korpuset har ikke ikrafttredelse per bestemmelse.</summary>
    public DateOnly? OpphevetDato { get; set; }
    /// <summary>Sitatet er lest i den kunngjorte teksten (Norsk Lovtidend avd. I), ikke i korpuset. Noden finnes da ikke i korpuset.</summary>
    public bool OpphevetFraKunngjort { get; set; }

    public Guid? VidereforingRettskildeId { get; set; }
    public Guid? VidereforingNodeId { get; set; }
    public string? VidereforingEid { get; set; }
    public string? VidereforingSitat { get; set; }
    public string? VidereforingForm { get; set; }
    public string? VidereforingForbehold { get; set; }
    public DateOnly? VidereforingTidsfrist { get; set; }
    public string? VidereforingDekning { get; set; }
    public bool VidereforingFraKunngjort { get; set; }

    public Guid? ErstattetAvRettskildeId { get; set; }
    public Guid? ErstattetAvNodeId { get; set; }
    public string? ErstattetAvEid { get; set; }

    /// <summary>En premiss maskinen ikke kunne kontrollere, i klartekst («ikraftsetting av opphevingsbestemmelsen ikke kontrollert»).</summary>
    public string? Premissmangel { get; set; }

    public required string Kunnskapsstatus { get; set; }
    public string Regel { get; set; } = Hjemmelsstatus.Regel;
    public string Regelversjon { get; set; } = Hjemmelsstatus.Regelversjon;
    public required string BeregnetAv { get; set; }
    public DateTimeOffset BeregnetTidspunkt { get; set; }
}

/// <summary>Én hjemmelsrad som premiss for den samlede statusen.</summary>
/// <param name="MalForskriftId">Hjemmelen er selv en gjeldende primær forskrift: statusen arves (rekursjon, juristrunde 1 R1).</param>
/// <param name="VidereforingDekning">[Ny, #472 PR 4, H14] Dekningen til videreføringen når hjemmelen er videreført (<see cref="Hjemmelsstatus.Dekninger"/>).</param>
public sealed record HjemmelPremiss(Guid HjemmelId, string HjemmelEid, string? Rolle, string Status, string Kunnskapsstatus, Guid? MalForskriftId,
    string? VidereforingDekning = null);

/// <summary>En premiss etter rekursjonen: <see cref="Status"/> er den effektive statusen, <see cref="Arvet"/> den samlede statusen til
/// hjemmelsforskriften når den er arvet.</summary>
public sealed record EffektivPremiss(HjemmelPremiss Rad, string Status, string Kunnskapsstatus, bool Teller, SamletHjemmelsstatus? Arvet, string? Merknad);

public sealed record SamletHjemmelsstatus(
    string Status, string Kunnskapsstatus, bool Gjenstandslos, IReadOnlyList<EffektivPremiss> Premisser, Guid? SvakestePremissId);

/// <summary>
/// [Ny, #472 PR 1] Den samlede statusen for én forskrift etter svakeste premiss (docs/33 §4.6), med rekursjon og vern mot sykler. Ren funksjon:
/// den lagres ikke (#345), og kalleren gir radene.
/// <para>
/// <b>Regelen</b> (fase 1 §B, med juristrundens rettinger):
/// <list type="number">
/// <item>Premissene er hjemlene med rolle materiell eller NULL. Er det ingen, teller kompetanseledd og overgangsgrunnlag. Gjenstand teller aldri.</item>
/// <item>En hjemmel som selv er en gjeldende forskrift, arver forskriftens samlede status (R1). En syklus gir ukjent.</item>
/// <item>Én premiss ukjent → <b>ukjent</b> (den strenge varianten).</item>
/// <item>Alle faller (opphevet uten videreføring, eller videreføringen utløpt) → <b>hjemmel opphevet, videreføring ikke funnet</b>, eller
/// <b>videreføring utløpt</b> når alle er utløpt.</item>
/// <item>Noen faller og noen bærer → <b>blandede hjemler</b>, men <b>uavklart</b> når alle som bærer har rolle NULL (runde 2 R1).
/// [ENDRET, #472 PR 4, H14] Også når de bare bærer ved en videreføring som ikke nevner vedtakstypen (<see cref="Hjemmelsstatus.BaererVurdert"/>).</item>
/// <item>Ellers <b>videreført</b> når minst én er videreført, og <b>aktiv</b>. [ENDRET, #501, prosjekteierens avgjørelse 2026-11-25] Men
/// <b>uavklart</b> når minst én bare bærer ved en videreføring som ikke nevner vedtakstypen, og ingen som bærer, er vurdert
/// (<see cref="Hjemmelsstatus.BaererBareUvurdertVidereforing"/>).</item>
/// </list>
/// Kunnskapsstatusen er svakeste premiss (<see cref="SvakestePremiss"/>), og aldri sterkere enn fortolkning når svaret sier at en hjemmel
/// faller: om forskriften har mistet virkning, er alltid tolkning (Lovteknikk pkt. 7.3.5).
/// </para>
/// </summary>
public static class HjemmelsstatusSamlet
{
    private const int MaksDybde = 20;

    public static SamletHjemmelsstatus Beregn(Guid forskriftId, Func<Guid, IReadOnlyList<HjemmelPremiss>?> rader,
        Dictionary<Guid, SamletHjemmelsstatus>? memo = null) =>
        Beregn(forskriftId, rader, memo ?? [], []);

    private static SamletHjemmelsstatus Beregn(Guid id, Func<Guid, IReadOnlyList<HjemmelPremiss>?> rader,
        Dictionary<Guid, SamletHjemmelsstatus> memo, List<Guid> stakk)
    {
        if (memo.TryGetValue(id, out var ferdig)) return ferdig;
        stakk.Add(id);
        var effektive = new List<EffektivPremiss>();
        foreach (var r in rader(id) ?? [])
        {
            string status = r.Status, ks = r.Kunnskapsstatus;
            SamletHjemmelsstatus? arvet = null;
            string? merknad = null;
            if (r.Status == Hjemmelsstatus.Aktiv && r.MalForskriftId is { } g && g != id)
            {
                if (stakk.Contains(g) || stakk.Count >= MaksDybde)
                {
                    status = Hjemmelsstatus.Ukjent;
                    ks = Strukturkanter.Uavklart;
                    merknad = "Hjemmelskjeden går i sirkel; statusen kan ikke arves.";
                }
                else if (rader(g) is { Count: > 0 })
                {
                    arvet = Beregn(g, rader, memo, stakk);
                    status = Arv(arvet.Status);
                    ks = SvakestePremiss.Rang(arvet.Kunnskapsstatus) > SvakestePremiss.Rang(ks) ? arvet.Kunnskapsstatus : ks;
                }
            }
            effektive.Add(new EffektivPremiss(r, status, ks, false, arvet, merknad));
        }
        stakk.RemoveAt(stakk.Count - 1);

        var materielle = effektive.Where(e => Hjemmelsstatus.ErMateriellPremiss(e.Rad.Rolle)).ToList();
        var premisser = materielle.Count > 0
            ? materielle
            : effektive.Where(e => e.Rad.Rolle is Hjemmelsstatus.Kompetanseledd or Hjemmelsstatus.Overgangsgrunnlag).ToList();
        var teller = premisser.Select(p => p.Rad.HjemmelId).ToHashSet();
        effektive = effektive.Select(e => e with { Teller = teller.Contains(e.Rad.HjemmelId) }).ToList();
        // [ENDRET, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1, H20] Gjenstandsløs bare når ALLE gjenstandene er opphevet. Før
        // var det nok at én var det (Any): DEL/forskrift/2000-12-15-1263 ble merket gjenstandsløs selv om odelslova lever og vedtaket også gjelder
        // etater. En gjenstand som er aktiv eller ukjent, gir ikke merket. Ingen «delvis gjenstandsløs»: hvilken del av vedtaket som står uten
        // gjenstand, kan ikke leses uten fortolkning (som R6 for blandede hjemler). Det er et åpent spørsmål til prosjekteieren.
        var gjenstander = effektive.Where(e => e.Rad.Rolle == Hjemmelsstatus.Gjenstand).ToList();
        var gjenstandslos = gjenstander.Count > 0 && gjenstander.All(e => e.Status == Hjemmelsstatus.Gjenstandslos);

        string samlet;
        if (premisser.Count == 0 || premisser.Any(p => !Hjemmelsstatus.Baerer(p.Status) && !Hjemmelsstatus.Faller(p.Status)))
        {
            samlet = Hjemmelsstatus.SamletUkjent;
        }
        else
        {
            var faller = premisser.Where(p => Hjemmelsstatus.Faller(p.Status)).ToList();
            var baerer = premisser.Where(p => Hjemmelsstatus.Baerer(p.Status)).ToList();
            if (faller.Count == 0)
            {
                // [ENDRET, #501, prosjekteierens avgjørelse 2026-11-25 etter måling 3, alternativ (a)] En videreføring med dekningen
                // «dokumenttype ikke nevnt» bærer ikke alene, heller ikke når ingenting faller. Er alle premissene som bærer, ikke vurdert (samme
                // vilkår som H14), er samlet status uavklart. AK2 (FOR-1993-12-17-1314) er fortsatt videreført: lov 1993 nr. 66 bærer og er vurdert.
                samlet = Hjemmelsstatus.BaererBareUvurdertVidereforing(baerer.Select(p => (p.Rad, p.Status)))
                    ? Hjemmelsstatus.SamletUavklart
                    : baerer.Any(p => p.Status == Hjemmelsstatus.Viderefort) ? Hjemmelsstatus.SamletViderefort : Hjemmelsstatus.SamletAktiv;
            }
            else if (baerer.Count == 0)
            {
                samlet = faller.All(p => p.Status == Hjemmelsstatus.VidereforingUtlopt)
                    ? Hjemmelsstatus.SamletVidereforingUtlopt : Hjemmelsstatus.SamletHjemmelOpphevet;
            }
            else
            {
                // [ENDRET, #472 PR 4, H14] «Ikke vurdert» er også en videreføring som ikke nevner vedtakstypen.
                samlet = baerer.All(p => !Hjemmelsstatus.BaererVurdert(p.Rad, p.Status)) ? Hjemmelsstatus.SamletUavklart : Hjemmelsstatus.SamletBlandede;
            }
        }

        var svakest = SvakestePremiss.Finn(premisser.Select(p => (p.Rad.HjemmelId, (string?)p.Kunnskapsstatus)));
        var ks2 = samlet is Hjemmelsstatus.SamletUkjent or Hjemmelsstatus.SamletUavklart
            ? Strukturkanter.Uavklart
            : SvakestePremiss.SvarStatus(svakest?.Kunnskapsstatus);
        if (samlet is Hjemmelsstatus.SamletHjemmelOpphevet or Hjemmelsstatus.SamletBlandede or Hjemmelsstatus.SamletVidereforingUtlopt
            && SvakestePremiss.Rang(ks2) < SvakestePremiss.Rang(Strukturkanter.Fortolkning))
        {
            ks2 = Strukturkanter.Fortolkning;
        }
        var resultat = new SamletHjemmelsstatus(samlet, ks2, gjenstandslos, effektive, svakest?.Id);
        memo[id] = resultat;
        return resultat;
    }

    /// <summary>Hva en arvet samlet status betyr for hjemmelsraden. «Blandede hjemler» og «uavklart» blir ukjent: forskriften som er hjemmel,
    /// står bare delvis, og hvilken del kan ikke leses (svakeste premiss).</summary>
    private static string Arv(string samlet) => samlet switch
    {
        Hjemmelsstatus.SamletAktiv => Hjemmelsstatus.Aktiv,
        Hjemmelsstatus.SamletViderefort => Hjemmelsstatus.Viderefort,
        Hjemmelsstatus.SamletVidereforingUtlopt => Hjemmelsstatus.VidereforingUtlopt,
        Hjemmelsstatus.SamletHjemmelOpphevet => Hjemmelsstatus.Opphevet,
        _ => Hjemmelsstatus.Ukjent,
    };
}
