using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Strukturkonvertering;

/// <summary>Utfallet for ett utsagn i en lasting (<see cref="StrukturforslagTjeneste.LastAsync"/>).</summary>
public static class Forslagsutfall
{
    /// <summary>Ny rad med status <c>maskinelt_foreslatt</c>.</summary>
    public const string Ny = "ny";
    /// <summary>Samme nøkkel finnes alt som ventende forslag (en tidligere kjøring) — ingen ny rad.</summary>
    public const string FantesSomForslag = "fantes_som_forslag";
    /// <summary>Samme nøkkel finnes alt godkjent — ingen ny rad.</summary>
    public const string FantesGodkjent = "fantes_godkjent";
    /// <summary>Samme nøkkel er AVVIST tidligere — foreslås ikke på nytt (issue #374 AC4).</summary>
    public const string TidligereAvvist = "tidligere_avvist";
    /// <summary>Samme nøkkel to ganger i samme kjøring (to sitater i samme node) — den første vant.</summary>
    public const string DublettIKjoringen = "dublett_i_kjoringen";
    /// <summary>Ikke lastet — se årsaken.</summary>
    public const string IkkeLastet = "ikke_lastet";
}

/// <summary>Årsakene til at et utsagn ikke ble lastet. Lukket liste, så tallene kan sammenlignes mellom kjøringer.</summary>
public static class Forslagsarsak
{
    public const string TypeUtenTypekode = "type_uten_typekode";
    public const string KiMedlemskapSendesIkke = "ki_medlemskap_sendes_ikke";
    public const string FraIkkeITeksten = "fra_ikke_i_teksten";
    public const string AktorIkkeIKatalogen = "aktor_ikke_i_katalogen";
    public const string AktorFlertydig = "aktor_flertydig";
    /// <summary>[Ny, issue #378 B6] Generisk omtale uten godkjent binding for rettskilden — ingen kobling, ingen gjetting.</summary>
    public const string AktorGeneriskUtenBinding = "aktor_generisk_uten_binding";
    public const string AvvistAvValideringen = "avvist_av_valideringen";

    public static string Tekst(string arsak) => arsak switch
    {
        TypeUtenTypekode => "Typen har ingen typekode i databasen (annet:*, «ukjent», eller en fasittype uten databasetype)",
        KiMedlemskapSendesIkke => "KI-forslag om medlemskap sendes ikke (docs/33 §5.5: 4,5 % presisjon)",
        FraIkkeITeksten => "Fra-aktøren står ikke i setningen (konverteringen satte fra = null)",
        AktorIkkeIKatalogen => "Aktøren har ikke eksakt treff på navneform, område, klasse eller rolle — lagt i navnekandidatkøen",
        AktorFlertydig => "Aktørnavnet treffer flere noder eksakt — ingen valgt",
        AktorGeneriskUtenBinding => "Generisk omtale («styret», «departementet») uten godkjent binding for denne rettskilden — lagt i navnekandidatkøen",
        AvvistAvValideringen => "Den felles skriveveien (StrukturkantTjeneste) avviste raden",
        _ => arsak,
    };
}

/// <summary>
/// [Ny, issue #378 A1] Statusen til én aktør (tekstform på aktørplass) i aktørsveipet og lasterapporten. Lukket liste.
/// </summary>
public static class Aktorstatus
{
    /// <summary>Eksakt treff på én node (registernavn, navneform, eller klasse/rolle/område/gruppe for loven) — eller en generisk
    /// navneform med godkjent binding for alle forekomstene.</summary>
    public const string Kjent = "kjent";
    /// <summary>Ikke eksakt treff, men tekstformen står som hele ord inni en kjent aktørs registernavn eller navneform («Høyesterett» i
    /// «Norges Høyesterett»). En KANDIDAT, ikke en kobling — et menneske avgjør.</summary>
    public const string NyNavneformForKjentAktor = "ny_navneform_for_kjent_aktor";
    /// <summary>Ingen treff og ingen kandidat.</summary>
    public const string NyAktor = "ny_aktor";
    /// <summary>Generisk navneform finnes, men minst én forekomst er ikke dekket av en godkjent binding.</summary>
    public const string Generisk = "generisk";
    /// <summary>Flere noder treffer eksakt, eller to like smale bindinger peker ulikt.</summary>
    public const string Flertydig = "flertydig";

    public static readonly string[] Alle = [Kjent, NyNavneformForKjentAktor, NyAktor, Generisk, Flertydig];
}

/// <summary>[Ny, issue #378 A1] Én aktør i aktørsveipet.</summary>
/// <param name="Forekomster">Antall utsagn aktøren står i (fra eller til).</param>
/// <param name="LastbareForekomster">Av dem: utsagn med en databasetype — de som faktisk kan bli forslag i steg 2.</param>
/// <param name="BundneForekomster">For en generisk navneform: forekomster dekket av en godkjent binding.</param>
/// <param name="Treff">Eksakte treff («virksomhet:Navn», «rolle:term»).</param>
/// <param name="Kandidater">Ved <see cref="Aktorstatus.NyNavneformForKjentAktor"/>: kjente navn tekstformen står i (inntil fem).</param>
public sealed record AktorsveipRad(
    string AktorId, string Tekstform, IReadOnlyList<string> Varianter, string Status, int Forekomster, int LastbareForekomster,
    string EidEksempel, IReadOnlyList<string> Treff, IReadOnlyList<string> Kandidater, int AntallKandidater,
    Guid? GeneriskNavneformId, int BundneForekomster, int ForeslatteBindinger, KantnodeVisning? Referent, Guid? NavnekandidatId);

/// <summary>[Ny, issue #378 A1] Antall aktører per status.</summary>
public sealed record AktorstatusTall(string Status, int Antall);

/// <summary>[Ny, issue #378 A1] Rapporten fra aktørsveipet for én rettskilde. Ingen forslag er skrevet; navnekandidater bare når
/// <see cref="NavnekandidaterSkrevet"/>.</summary>
public sealed record AktorsveipRapport(
    Guid RettskildeId, string RettskildeTittel, string Lag, int NoderLest, int UtsagnTotalt, int AktorerTotalt,
    IReadOnlyList<AktorstatusTall> PerStatus, IReadOnlyList<AktorsveipRad> Aktorer, bool NavnekandidaterSkrevet);

/// <summary>Én rad i rapporten: hva som skjedde med ett utsagn.</summary>
public sealed record ForslagsutfallRad(
    string UtsagnId, string Eid, string Sitat, string FasitKategori, string FasitType, string? Kategori, string? Typekode,
    string Utfall, string? Arsak, string? Detalj, Guid? KantId, string Oppdagelseskilde);

/// <summary>Tallene for én kategori (databasens bokstav, eller fasitkategorien når typen ikke har en databasetype).</summary>
public sealed record ForslagKategoritall(
    string Kategori, int Utsagn, int Nye, int FantesSomForslag, int FantesGodkjent, int TidligereAvvist, int Dubletter, int IkkeLastet);

/// <summary>Én årsak med antall og inntil fem eksempler.</summary>
public sealed record ForslagArsaktall(string Arsak, string Tekst, int Antall, IReadOnlyList<ForslagsutfallRad> Eksempler);

/// <summary>En aktør som ikke kunne løses opp, og navnekandidaten den ble lagt i (null når tekstformen ikke ble funnet i noden).</summary>
public sealed record UloestAktor(string Tekstform, string Arsak, int AntallUtsagn, Guid? NavnekandidatId, IReadOnlyList<string> Treff)
{
    /// <summary>[Ny, issue #378 A3] Aktørstatusen (samme klassifisering som aktørsveipet).</summary>
    public string? Status { get; init; }
}

/// <summary>Rapporten fra én lasting av én rettskilde.</summary>
public sealed record StrukturforslagRapport(
    Guid RettskildeId, string RettskildeTittel, string Lag, string Oppdagelseskilde, int NoderLest, int UtsagnTotalt,
    IReadOnlyList<ForslagKategoritall> PerKategori, IReadOnlyList<ForslagArsaktall> IkkeLastetPerArsak,
    IReadOnlyList<UloestAktor> UloesteAktorer, IReadOnlyList<ForslagsutfallRad> Rader,
    int? KiInputTokens = null, int? KiOutputTokens = null, int? KiKastedeRader = null)
{
    /// <summary>[Ny, issue #378 A3] Aktørene i konverteringen per status, med samme klassifisering som aktørsveipet — slik at
    /// lasterapporten (steg 2) kan sammenlignes med sveipet før gjennomgangen (steg 1).</summary>
    public IReadOnlyList<AktorstatusTall> AktorerPerStatus { get; init; } = [];
}

/// <summary>
/// [Ny, issue #313 «Strukturmodell 8», Johanns godkjenning 2026-10-15] Broen fra konverteringen (#307 mønster, #308 KI) inn i databasen:
/// hvert utsagn i fasit-formatet blir et FORSLAG (<c>status = maskinelt_foreslatt</c>, <c>oppdagelses_kilde = monster:&lt;id&gt; | ki:&lt;modell&gt;</c>)
/// som et menneske godkjenner eller avviser i forslagskøen (docs/33 §5.3). Aldri <c>godkjent</c> av seg selv.
/// <para>
/// <b>Én skrivevei (#285-prinsippet):</b> hver rad skrives med <see cref="StrukturkantTjeneste.OpprettAsync"/>, samme metode som
/// <c>POST /api/strukturkanter</c>. All validering (nodetyper, typekoder, kunnskapsstatus, delegering) er dermed den samme; en rad
/// skriveveien avviser, telles med meldingen, og «repareres» ikke.
/// </para>
/// <para>
/// <b>Aktøroppløsning (CLAUDE.md §8):</b> bare EKSAKT treff (uten skille på store/små bokstaver) på tekstformen eller en variant:
/// virksomhetens registernavn eller navneform (<see cref="VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnEllerNavneformAsync"/>), eller
/// termen til et gjeldende klasse-, rolle-, område- eller gruppebegrep. Ingen treff → navnekandidat (kategori <c>gruppe</c> = uavklart,
/// oppdagelseskilde <see cref="NavnekandidatOppdagelseskilde"/>); flere treff → flertydig. Ingenting gjettes.
/// </para>
/// <para>
/// <b>Idempotens per rettskilde:</b> nøkkelen er hjemmel + eId + kategori + typekode + fra + til + undertype + normform (#313), pluss
/// polaritet og modalitet («kan ikke instruere» og «skal samarbeide» er andre utsagn enn «kan instruere» og «kan samarbeide»). Finnes
/// nøkkelen alt — som forslag, godkjent eller AVVIST — lages ingen ny rad. Et avvist utsagn foreslås ikke på nytt (#374 AC4).
/// </para>
/// </summary>
public sealed class StrukturforslagTjeneste(
    RegelIdeDbContext db, StrukturkantTjeneste kanter, VirksomhetOppslagTjeneste virksomhetOppslag, NavnekandidatOppdagelseTjeneste navnekandidater)
{
    private readonly GeneriskNavneformTjeneste generiske = new(db);

    /// <summary>Oppdagelseskilden på navnekandidater denne lasteren oppretter for aktører som ikke kunne løses opp.</summary>
    public const string NavnekandidatOppdagelseskilde = "strukturforslag";

    /// <summary>
    /// FORMAT.md-kategori og -type → databasens kategori og typekode. Null = ingen databasetype (annet:*, «ukjent», <c>inngar_i</c>,
    /// områdesammensetning <c>del_av</c>) — det gjettes ikke en nærliggende type. Avbildningene er de dokumenterte: kompetanse og plikt via
    /// <see cref="Strukturkanter.KompetansetypeFraFasit"/>/<see cref="Strukturkanter.PlikttypeFraFasit"/>, relasjon <c>del_av</c> er
    /// organtilhørighet G (FORMAT.md #355), konstituerende <c>oppretter</c>/<c>avvikler</c> er R og <c>skal_finnes</c> er T <c>skal_ha</c>,
    /// organsammensetning er G (designtest.py).
    /// </summary>
    public static (string Kategori, string Typekode)? KanttypeFor(string fasitKategori, string fasitType)
    {
        if (Strukturkontrakt.ErAnnet(fasitType) || Strukturkontrakt.ErAnnet(fasitKategori)) return null;
        return fasitKategori switch
        {
            "relasjon" when fasitType == "del_av" => (Strukturkanter.Organtilhorighet, "del_av"),
            "relasjon" when Strukturkontrakt.TyperPerKategori["relasjon"].Contains(fasitType) => (Strukturkanter.Relasjon, fasitType),
            "kompetanse" when Strukturkanter.KompetansetypeFraFasit.TryGetValue(fasitType, out var k) => (Strukturkanter.Kompetanse, k),
            "plikt" when Strukturkanter.PlikttypeFraFasit.TryGetValue(fasitType, out var p) => (Strukturkanter.Plikt, p),
            // [Ny, issue #368] Oppgave/funksjon via Strukturkanter.OppgavetypeFraFasit.
            "oppgave" when Strukturkanter.OppgavetypeFraFasit.TryGetValue(fasitType, out var f) => (Strukturkanter.Oppgave, f),
            "medlemskap" when fasitType == Strukturkanter.MedlemAv => (Strukturkanter.Medlemskap, Strukturkanter.MedlemAv),
            "sammensetning_omrade" when fasitType == "bestar_av" => (Strukturkanter.Omradesammensetning, "bestar_av"),
            "ansvarsomrade" when Strukturkontrakt.TyperPerKategori["ansvarsomrade"].Contains(fasitType) => (Strukturkanter.Ansvarsomrade, fasitType),
            "konstituerende" when fasitType is "oppretter" or "avvikler" => (Strukturkanter.Relasjon, fasitType),
            "konstituerende" when fasitType == "skal_finnes" => (Strukturkanter.Klasseniva, "skal_ha"),
            "organsammensetning" when Strukturkontrakt.TyperPerKategori["organsammensetning"].Contains(fasitType)
                => (Strukturkanter.Organtilhorighet, fasitType),
            _ => null,
        };
    }

    /// <summary>Inndataene til konverteringen: rettskildens gjeldende, ikke opphevede noder i dokumentrekkefølge (FORMAT.md «Lesing»).
    /// Bare rettskildens EGNE noder — et utsagn har bare en eId, og eId-en er unik bare innenfor sin rettskilde. En ledsagende forskrift
    /// lastes som egen rettskilde.</summary>
    public async Task<Strukturkonverteringsgrunnlag> ByggGrunnlagAsync(Guid rettskildeId, CancellationToken ct = default)
    {
        var rettskilde = await db.Rettskilder.FirstOrDefaultAsync(r => r.Id == rettskildeId, ct)
            ?? throw new ArgumentException($"Fant ingen rettskilde med id '{rettskildeId}'.");
        var tittel = rettskilde.Tittel;
        var eli = rettskilde.Eli ?? "";
        var noder = await db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende" && !n.Opphevet)
            .OrderBy(n => n.Sorteringsrekkefolge)
            .Select(n => new Strukturnode(tittel, eli, n.Eid, n.NodeType, n.Overskrift, n.Tekst))
            .ToListAsync(ct);
        return new Strukturkonverteringsgrunnlag(tittel, eli, noder);
    }

    private sealed record Oppslag(Kantnode? Node, string? Arsak, IReadOnlyList<string> Treff);

    private sealed record Nokkel(string Eid, string Kategori, string Typekode, Guid? FraV, Guid? FraB, Guid? TilV, Guid? TilB,
        string? Undertype, string? Normform, string Polaritet, string? Modalitet);

    private static Nokkel NokkelFor(StrukturkantEntitet k) => new(k.HjemmelEid ?? "", k.Kategori, k.Typekode, k.FraVirksomhetId, k.FraBegrepId,
        k.TilVirksomhetId, k.TilBegrepId, k.Undertype, k.Normform, k.Polaritet, k.Modalitet);

    /// <summary>
    /// Laster <paramref name="dokument"/> (konverteringen av <paramref name="rettskildeId"/>) som forslag. <paramref name="lag"/> er
    /// <c>monster</c> eller <c>ki</c>; utsagn uten egen oppdagelseskilde får <paramref name="standardOppdagelseskilde"/>.
    /// </summary>
    public async Task<StrukturforslagRapport> LastAsync(
        Guid rettskildeId, Strukturdokument dokument, string lag, string standardOppdagelseskilde, string opprettetAv, CancellationToken ct = default)
    {
        if (lag is not ("monster" or "ki")) throw new ArgumentException($"Ukjent lag '{lag}'. Gyldige verdier: monster, ki.");
        var rettskilde = await db.Rettskilder.FirstOrDefaultAsync(r => r.Id == rettskildeId, ct)
            ?? throw new ArgumentException($"Fant ingen rettskilde med id '{rettskildeId}'.");
        // [Ny, issue #378 C9] «kommune del_av fylke» lastes som «fylke bestar_av kommune» (Strukturkontrakt.NormaliserOmradesammensetning).
        dokument = dokument with { Utsagn = dokument.Utsagn.Select(Strukturkontrakt.NormaliserOmradesammensetning).ToList() };

        // Eksisterende kanter hjemlet i rettskilden, også de avviste — nøkkelen avgjør om et utsagn alt er lastet eller avvist.
        var eksisterende = new Dictionary<Nokkel, StrukturkantEntitet>();
        foreach (var k in await kanter.GjeldendeInklAvviste.Where(k => k.HjemmelRettskildeId == rettskildeId && k.HjemmelEid != null).ToListAsync(ct))
        {
            // Står samme nøkkel flere ganger (to avgrensninger, et omstridt par), teller den første — ingen av dem lages på nytt.
            eksisterende.TryAdd(NokkelFor(k), k);
        }

        var aktorer = dokument.Aktorer.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var klassifiserer = await NyKlassifisererAsync(rettskildeId, ct);
        // [ENDRET, issue #378 B6] Oppslaget går gjennom samme klassifisering som aktørsveipet: eksakt → generisk med binding for
        // rettskilden (per eId, smaleste spenn) → ellers ingen node. Grunnlaget er cachet per aktør; bindingen slås opp per eId.
        async Task<Oppslag> SlaOppAsync(string aktorId, string eid)
        {
            var k = await klassifiserer.GrunnlagAsync(aktorer[aktorId], ct);
            var (status, node, _, _) = await klassifiserer.ForekomstAsync(k, eid, ct);
            return status switch
            {
                Aktorstatus.Kjent => new Oppslag(node, null, k.Treff),
                Aktorstatus.Flertydig => new Oppslag(null, Forslagsarsak.AktorFlertydig, k.Treff),
                Aktorstatus.Generisk => new Oppslag(null, Forslagsarsak.AktorGeneriskUtenBinding, k.Treff),
                _ => new Oppslag(null, Forslagsarsak.AktorIkkeIKatalogen, k.Treff),
            };
        }

        var rader = new List<ForslagsutfallRad>();
        var uloest = new Dictionary<string, (string Arsak, int Antall, string Eid, IReadOnlyList<string> Treff)>(StringComparer.Ordinal);
        var settIKjoringen = new HashSet<Nokkel>();

        foreach (var u in dokument.Utsagn)
        {
            var kilde = u.Oppdagelseskilde ?? standardOppdagelseskilde;
            ForslagsutfallRad Rad(string? kat, string? kode, string utfall, string? arsak, string? detalj, Guid? kantId) =>
                new(u.Id, u.Eid, u.Sitat, u.Kategori, u.Type, kat, kode, utfall, arsak, detalj, kantId, kilde);

            if (KanttypeFor(u.Kategori, u.Type) is not { } kanttype)
            {
                rader.Add(Rad(null, null, Forslagsutfall.IkkeLastet, Forslagsarsak.TypeUtenTypekode, $"{u.Kategori}/{u.Type}", null));
                continue;
            }
            var (kat, kode) = kanttype;
            if (lag == "ki" && kat == Strukturkanter.Medlemskap)
            {
                rader.Add(Rad(kat, kode, Forslagsutfall.IkkeLastet, Forslagsarsak.KiMedlemskapSendesIkke, null, null));
                continue;
            }
            if (u.Fra is null)
            {
                rader.Add(Rad(kat, kode, Forslagsutfall.IkkeLastet, Forslagsarsak.FraIkkeITeksten, null, null));
                continue;
            }
            var fra = await SlaOppAsync(u.Fra, u.Eid);
            var til = u.Til is null ? null : await SlaOppAsync(u.Til, u.Eid);
            var mislyktes = new[] { (Id: u.Fra, O: fra), (Id: u.Til, O: til) }.Where(x => x.O is { Node: null }).ToList();
            if (mislyktes.Count > 0)
            {
                foreach (var (id, o) in mislyktes)
                {
                    uloest[id!] = uloest.TryGetValue(id!, out var f) ? f with { Antall = f.Antall + 1 } : (o!.Arsak!, 1, u.Eid, o!.Treff);
                }
                rader.Add(Rad(kat, kode, Forslagsutfall.IkkeLastet, mislyktes[0].O!.Arsak,
                    string.Join("; ", mislyktes.Select(m => $"«{aktorer[m.Id!].Tekstform}»")), null));
                continue;
            }

            var normalisering = u.Kunnskapsstatus == Strukturkanter.Normalisering;
            var ny = new NyStrukturkant(
                kat, kode, fra.Node!, til?.Node,
                HjemmelRettskildeId: rettskildeId, HjemmelEid: u.Eid,
                AvgrensningTekst: u.Avgrensning, Objekt: u.Objekt, Polaritet: u.Polaritet,
                Kommentar: $"«{u.Sitat}»" + (string.IsNullOrWhiteSpace(u.Kommentar) ? "" : $" — {u.Kommentar}"),
                Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: kilde,
                Normform: u.Normform, Grunnlag: u.Grunnlag, Delegeringsadgang: u.Delegeringsadgang, DelegeringSlutning: u.DelegeringSlutning,
                Undertype: u.Undertype, Modalitet: u.Modalitet,
                // Kunnskapsstatus bare der konverteringen setter den (mønsterlaget: normalisering, regelen = mønsteret; KI: ingen).
                Kunnskapsstatus: u.Kunnskapsstatus,
                Begrunnelse: u.Begrunnelse ?? (normalisering ? $"Regel: {kilde} (#307)" : null),
                // [Ny, issue #368] Juristrundens fortolkning bæres med (CHECK ck_strukturkanter_fortolkning krever argumentene).
                Tolkningsargumenter: u.Tolkningsargumenter);
            var nokkel = new Nokkel(u.Eid, kat, kode, ny.Fra.VirksomhetId, ny.Fra.BegrepId, ny.Til?.VirksomhetId, ny.Til?.BegrepId,
                ny.Undertype, ny.Normform, ny.Polaritet, ny.Modalitet);
            if (eksisterende.TryGetValue(nokkel, out var fantes))
            {
                var utfall = !settIKjoringen.Add(nokkel) ? Forslagsutfall.DublettIKjoringen
                    : fantes.Status == Strukturkanter.Avvist ? Forslagsutfall.TidligereAvvist
                    : Strukturkanter.Ventende.Contains(fantes.Status) ? Forslagsutfall.FantesSomForslag
                    : Forslagsutfall.FantesGodkjent;
                rader.Add(Rad(kat, kode, utfall, null, null, fantes.Id));
                continue;
            }
            try
            {
                var resultat = await kanter.OpprettAsync(ny, opprettetAv, ct);
                // OpprettAsync har sin egen (strengere) identitet; treffer den en eksisterende rad, er det samme utsagn.
                var utfall = resultat.VarNy ? Forslagsutfall.Ny
                    : resultat.Kant.Status == Strukturkanter.Avvist ? Forslagsutfall.TidligereAvvist
                    : Strukturkanter.Ventende.Contains(resultat.Kant.Status) ? Forslagsutfall.FantesSomForslag
                    : Forslagsutfall.FantesGodkjent;
                eksisterende[nokkel] = resultat.Kant;
                settIKjoringen.Add(nokkel);
                rader.Add(Rad(kat, kode, utfall, null, null, resultat.Kant.Id));
            }
            catch (ArgumentException ex)
            {
                rader.Add(Rad(kat, kode, Forslagsutfall.IkkeLastet, Forslagsarsak.AvvistAvValideringen, ex.Message, null));
            }
        }

        // Navnekandidater for aktørene som ikke fantes i katalogen (ikke for de flertydige: der finnes navnet, valget mangler).
        var uloesteAktorer = new List<UloestAktor>();
        foreach (var (aktorId, (arsak, antall, eid, treff)) in uloest)
        {
            var aktor = aktorer[aktorId];
            Guid? kandidatId = null;
            if (arsak is Forslagsarsak.AktorIkkeIKatalogen or Forslagsarsak.AktorGeneriskUtenBinding)
            {
                kandidatId = await OpprettNavnekandidatAsync(rettskildeId, eid, aktor.Tekstform, opprettetAv, ct);
            }
            var grunnlag = await klassifiserer.GrunnlagAsync(aktor, ct);
            uloesteAktorer.Add(new UloestAktor(aktor.Tekstform, arsak, antall, kandidatId, treff) { Status = grunnlag.Status });
        }

        var perKategori = rader
            .GroupBy(r => r.Kategori ?? r.FasitKategori)
            .Select(g => new ForslagKategoritall(g.Key, g.Count(),
                g.Count(r => r.Utfall == Forslagsutfall.Ny), g.Count(r => r.Utfall == Forslagsutfall.FantesSomForslag),
                g.Count(r => r.Utfall == Forslagsutfall.FantesGodkjent), g.Count(r => r.Utfall == Forslagsutfall.TidligereAvvist),
                g.Count(r => r.Utfall == Forslagsutfall.DublettIKjoringen), g.Count(r => r.Utfall == Forslagsutfall.IkkeLastet)))
            .OrderBy(t => Array.IndexOf(Strukturkanter.Kategorier, t.Kategori) is var i and >= 0 ? i : 99).ThenBy(t => t.Kategori, StringComparer.Ordinal)
            .ToList();
        var perArsak = rader.Where(r => r.Arsak is not null).GroupBy(r => r.Arsak!)
            .Select(g => new ForslagArsaktall(g.Key, Forslagsarsak.Tekst(g.Key), g.Count(), g.Take(5).ToList()))
            .OrderByDescending(a => a.Antall).ToList();

        var sveip = await KlassifiserAktorerAsync(dokument, klassifiserer, ct);
        return new StrukturforslagRapport(rettskildeId, rettskilde.Kortnavn ?? rettskilde.Tittel, lag, standardOppdagelseskilde,
            dokument.NoderLest, dokument.Utsagn.Count, perKategori, perArsak,
            uloesteAktorer.OrderByDescending(a => a.AntallUtsagn).ThenBy(a => a.Tekstform, StringComparer.Ordinal).ToList(), rader)
        {
            AktorerPerStatus = TellPerStatus(sveip),
        };
    }

    /// <summary>Oppretter (eller finner) en navnekandidat der tekstformen står i noden. Null når tekstformen ikke står der.</summary>
    private async Task<Guid?> OpprettNavnekandidatAsync(Guid rettskildeId, string eid, string tekstform, string opprettetAv, CancellationToken ct)
    {
        var tekst = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId && n.Eid == eid && n.Entitetsstatus == "gjeldende")
            .Select(n => n.Tekst).FirstOrDefaultAsync(ct) ?? "";
        var start = tekst.IndexOf(tekstform, StringComparison.Ordinal);
        if (start < 0) start = tekst.IndexOf(tekstform, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        var kandidat = await navnekandidater.OpprettEllerFinnAsync(
            tekst.Substring(start, tekstform.Length), Nodetyper.Gruppe, rettskildeId, eid, start, start + tekstform.Length,
            opprettetAv, ct, NavnekandidatOppdagelseskilde);
        return kandidat.Id;
    }

    private static List<AktorstatusTall> TellPerStatus(IEnumerable<AktorsveipRad> rader)
    {
        var liste = rader.ToList();
        return Aktorstatus.Alle.Select(st => new AktorstatusTall(st, liste.Count(r => r.Status == st))).ToList();
    }

    // ------------------------------------------------------------------ [Ny, issue #378 A] Aktørsveip

    /// <summary>
    /// [Ny, issue #378 A1, Johanns godkjenning 2026-10-15] Steg 1 før forslagene: samler ALLE tekstformer på aktørplass (fra/til) i
    /// konverteringen av én rettskilde og klassifiserer hver (<see cref="Aktorstatus"/>). Skriver INGEN forslag. Med
    /// <paramref name="skrivNavnekandidater"/> legges aktørene som ikke er kjent (ny aktør, ny navneform, generisk uten binding) i
    /// navnekandidatkøen, der de gjennomgås i den eksisterende veiviseren (A2); uten skrives ingenting (ren tørrkjøring). Flertydige får ingen
    /// navnekandidat (navnet finnes, valget mangler — samme regel som lasteren).
    /// </summary>
    public async Task<AktorsveipRapport> AktorsveipAsync(
        Guid rettskildeId, Strukturdokument dokument, string lag, bool skrivNavnekandidater, string opprettetAv, CancellationToken ct = default)
    {
        var rettskilde = await db.Rettskilder.FirstOrDefaultAsync(r => r.Id == rettskildeId, ct)
            ?? throw new ArgumentException($"Fant ingen rettskilde med id '{rettskildeId}'.");
        dokument = dokument with { Utsagn = dokument.Utsagn.Select(Strukturkontrakt.NormaliserOmradesammensetning).ToList() };
        var klassifiserer = await NyKlassifisererAsync(rettskildeId, ct);
        var rader = await KlassifiserAktorerAsync(dokument, klassifiserer, ct);
        if (skrivNavnekandidater)
        {
            for (var i = 0; i < rader.Count; i++)
            {
                var r = rader[i];
                if (r.Status is Aktorstatus.Kjent or Aktorstatus.Flertydig) continue;
                rader[i] = r with { NavnekandidatId = await OpprettNavnekandidatAsync(rettskildeId, r.EidEksempel, r.Tekstform, opprettetAv, ct) };
            }
        }
        return new AktorsveipRapport(rettskildeId, rettskilde.Kortnavn ?? rettskilde.Tittel, lag, dokument.NoderLest, dokument.Utsagn.Count,
            rader.Count, TellPerStatus(rader),
            rader.OrderBy(r => Array.IndexOf(Aktorstatus.Alle, r.Status)).ThenByDescending(r => r.Forekomster)
                .ThenBy(r => r.Tekstform, StringComparer.Ordinal).ToList(),
            skrivNavnekandidater);
    }

    private async Task<List<AktorsveipRad>> KlassifiserAktorerAsync(Strukturdokument dokument, Aktorklassifiserer klassifiserer, CancellationToken ct)
    {
        var forekomster = new Dictionary<string, List<(string Eid, bool Lastbar)>>(StringComparer.Ordinal);
        foreach (var u in dokument.Utsagn)
        {
            var lastbar = KanttypeFor(u.Kategori, u.Type) is not null;
            foreach (var id in new[] { u.Fra, u.Til }.OfType<string>().Distinct(StringComparer.Ordinal))
            {
                if (!forekomster.TryGetValue(id, out var l)) forekomster[id] = l = [];
                l.Add((u.Eid, lastbar));
            }
        }
        var rader = new List<AktorsveipRad>();
        foreach (var aktor in dokument.Aktorer)
        {
            if (!forekomster.TryGetValue(aktor.Id, out var f)) continue; // ikke på aktørplass i noe utsagn
            var g = await klassifiserer.GrunnlagAsync(aktor, ct);
            var status = g.Status;
            var bundne = 0;
            var foreslatte = 0;
            KantnodeVisning? referent = g.Referent;
            if (g.GeneriskNavneformId is not null && g.Status == Aktorstatus.Generisk)
            {
                var statuser = new List<string>();
                foreach (var eid in f.Select(x => x.Eid).Distinct(StringComparer.Ordinal))
                {
                    var (st, _, visning, forsl) = await klassifiserer.ForekomstAsync(g, eid, ct);
                    statuser.Add(st);
                    foreslatte = Math.Max(foreslatte, forsl);
                    if (st == Aktorstatus.Kjent) { bundne += f.Count(x => x.Eid == eid); referent ??= visning; }
                }
                status = statuser.Contains(Aktorstatus.Flertydig) ? Aktorstatus.Flertydig
                    : statuser.All(x => x == Aktorstatus.Kjent) ? Aktorstatus.Kjent : Aktorstatus.Generisk;
            }
            rader.Add(new AktorsveipRad(aktor.Id, aktor.Tekstform, aktor.Varianter, status, f.Count, f.Count(x => x.Lastbar), f[0].Eid,
                g.Treff, g.Kandidater.Take(5).ToList(), g.Kandidater.Count, g.GeneriskNavneformId, bundne, foreslatte, referent, null));
        }
        return rader;
    }

    private async Task<Aktorklassifiserer> NyKlassifisererAsync(Guid rettskildeId, CancellationToken ct)
    {
        // Lovspesifikke begrep (klasse/rolle/område med LovkildeId) gjelder rettskilden selv og lovene den har hjemmel i — ikke andre
        // lover. [ENDRET, issue #378] Før matchet lasteren termen uansett lov: «departementet» (rolle i helse- og omsorgstjenesteloven)
        // ble «kjent» i barnehageloven. Det var en gjetning.
        var lover = await db.RettskildeHjemler.Where(h => h.RettskildeId == rettskildeId).Select(h => h.HjemmelRettskildeId).Distinct().ToListAsync(ct);
        lover.Add(rettskildeId);
        return new Aktorklassifiserer(db, virksomhetOppslag, generiske, rettskildeId, lover);
    }
}

/// <summary>Grunnlaget for én aktør: eksakte treff, generisk navneform og kandidater — uavhengig av hvor i rettskilden den står.</summary>
internal sealed record Aktorgrunnlag(
    string Status, Kantnode? Node, KantnodeVisning? Referent, IReadOnlyList<string> Treff, IReadOnlyList<string> Kandidater, Guid? GeneriskNavneformId);

/// <summary>
/// [Ny, issue #378 A1/B6] Klassifiseringen av aktører i én rettskilde — felles for aktørsveipet og lasteren, så statusene i de to
/// rapportene er de samme. Rekkefølgen (CLAUDE.md §8): eksakt treff på én node → generisk navneform (bundet per eId) → flere eksakte treff
/// (flertydig) → kandidat (hele ord inni et kjent navn) → ny aktør. Ingenting gjettes; en kandidat er aldri en kobling.
/// </summary>
internal sealed class Aktorklassifiserer(
    RegelIdeDbContext db, VirksomhetOppslagTjeneste virksomhetOppslag, GeneriskNavneformTjeneste generiske, Guid rettskildeId, List<Guid> lover)
{
    private readonly Dictionary<string, Aktorgrunnlag> _cache = new(StringComparer.Ordinal);
    private List<(string Navn, string[] Ord)>? _kjenteNavn;

    private static string[] Ord(string s) =>
        s.ToLowerInvariant().Split([' ', '/', ',', '(', ')', '«', '»', '-', '.'], StringSplitOptions.RemoveEmptyEntries);

    public async Task<Aktorgrunnlag> GrunnlagAsync(StrukturAktor aktor, CancellationToken ct)
    {
        if (_cache.TryGetValue(aktor.Id, out var c)) return c;
        var treff = new HashSet<Kantnode>();
        var treffnavn = new List<string>();
        var former = new[] { aktor.Tekstform }.Concat(aktor.Varianter).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Guid? generisk = null;
        foreach (var form in former)
        {
            var lower = form.ToLower();
            if (await virksomhetOppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(form, ct) is { } vid)
            {
                if (treff.Add(Kantnode.Virksomhet(vid))) treffnavn.Add($"virksomhet:{form}");
            }
            else
            {
                // Oppslaget gir null både ved null og ved flere treff; skill dem, så årsaken blir riktig («flertydig»).
                var flere = await db.Begreper
                    .Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                                && b.Term.ToLower() == lower)
                    .Select(b => b.VirksomhetReferanseId!.Value).Distinct().Take(3).ToListAsync(ct);
                foreach (var id in flere.Count > 1 ? flere : [])
                {
                    if (treff.Add(Kantnode.Virksomhet(id))) treffnavn.Add($"virksomhet:{form}");
                }
            }
            var begreper = await db.Begreper
                .Where(b => b.Entitetsstatus == "gjeldende" && Nodetyper.MedGruppefunksjon.Contains(b.Begrepskategori!) && b.Term.ToLower() == lower
                            && (b.LovkildeId == null || lover.Contains(b.LovkildeId.Value)))
                .Select(b => new { b.Id, b.Begrepskategori }).Take(3).ToListAsync(ct);
            foreach (var b in begreper)
            {
                if (treff.Add(Kantnode.Begrep(b.Id))) treffnavn.Add($"{b.Begrepskategori}:{form}");
            }
            generisk ??= (await generiske.FinnAsync(form, ct))?.Id;
        }

        Aktorgrunnlag svar;
        if (treff.Count == 1) svar = new(Aktorstatus.Kjent, treff.Single(), null, treffnavn, [], null);
        else if (generisk is not null) svar = new(Aktorstatus.Generisk, null, null, treffnavn, [], generisk);
        else if (treff.Count > 1) svar = new(Aktorstatus.Flertydig, null, null, treffnavn, [], null);
        else
        {
            var kandidater = await KandidaterAsync(former, ct);
            svar = new(kandidater.Count > 0 ? Aktorstatus.NyNavneformForKjentAktor : Aktorstatus.NyAktor, null, null, treffnavn, kandidater, null);
        }
        _cache[aktor.Id] = svar;
        return svar;
    }

    /// <summary>Status for én forekomst (eId): for en generisk navneform avgjør bindingen; ellers grunnlagets status.</summary>
    public async Task<(string Status, Kantnode? Node, KantnodeVisning? Referent, int ForeslatteBindinger)> ForekomstAsync(
        Aktorgrunnlag g, string eid, CancellationToken ct)
    {
        if (g.GeneriskNavneformId is not { } nid || g.Status != Aktorstatus.Generisk) return (g.Status, g.Node, g.Referent, 0);
        var o = await generiske.LosOppAsync(nid, rettskildeId, eid, ct);
        if (o.Status == GeneriskOppslagstatus.Bundet)
        {
            var r = o.Referent!;
            KantnodeVisning? v = r.VirksomhetId is { } vid
                ? await db.Virksomheter.Where(x => x.Id == vid).Select(x => new KantnodeVisning("virksomhet", x.Id, x.Navn, x.Aktortype)).FirstOrDefaultAsync(ct)
                : await db.Begreper.Where(x => x.Id == r.BegrepId).Select(x => new KantnodeVisning("begrep", x.Id, x.Term, x.Begrepskategori)).FirstOrDefaultAsync(ct);
            return (Aktorstatus.Kjent, r, v, o.AntallForeslatteBindinger);
        }
        // [ENDRET, issue #384] spenn_endret (ingen gyldig binding dekker noden, og en godkjent binding kunne ikke flyttes til denne
        // versjonen) gir generisk: omtalen er ikke bundet her før bindingen er registrert på nytt.
        return (o.Status == GeneriskOppslagstatus.Flertydig ? Aktorstatus.Flertydig : Aktorstatus.Generisk, null, null, o.AntallForeslatteBindinger);
    }

    /// <summary>Kjente aktørnavn (registernavn og ikke-generiske navneformer) der tekstformen står som hele ord, i rekkefølge.</summary>
    private async Task<List<string>> KandidaterAsync(IReadOnlyList<string> former, CancellationToken ct)
    {
        _kjenteNavn ??= (await db.Virksomheter.Select(v => v.Navn).ToListAsync(ct))
            .Concat(await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null)
                .Select(b => b.Term).ToListAsync(ct))
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(n => (n, Ord(n))).ToList();
        var ut = new List<string>();
        foreach (var form in former)
        {
            var f = Ord(form);
            if (f.Length == 0) continue;
            foreach (var (navn, ord) in _kjenteNavn)
            {
                if (ord.Length <= f.Length) continue;
                for (var i = 0; i + f.Length <= ord.Length; i++)
                {
                    if (f.Select((w, j) => ord[i + j] == w).All(x => x)) { ut.Add(navn); break; }
                }
            }
        }
        return ut.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.Ordinal).ToList();
    }
}
