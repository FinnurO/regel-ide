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
    public const string AvvistAvValideringen = "avvist_av_valideringen";

    public static string Tekst(string arsak) => arsak switch
    {
        TypeUtenTypekode => "Typen har ingen typekode i databasen (annet:*, «ukjent», eller en fasittype uten databasetype)",
        KiMedlemskapSendesIkke => "KI-forslag om medlemskap sendes ikke (docs/33 §5.5: 4,5 % presisjon)",
        FraIkkeITeksten => "Fra-aktøren står ikke i setningen (konverteringen satte fra = null)",
        AktorIkkeIKatalogen => "Aktøren har ikke eksakt treff på navneform, område, klasse eller rolle — lagt i navnekandidatkøen",
        AktorFlertydig => "Aktørnavnet treffer flere noder eksakt — ingen valgt",
        AvvistAvValideringen => "Den felles skriveveien (StrukturkantTjeneste) avviste raden",
        _ => arsak,
    };
}

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
public sealed record UloestAktor(string Tekstform, string Arsak, int AntallUtsagn, Guid? NavnekandidatId, IReadOnlyList<string> Treff);

/// <summary>Rapporten fra én lasting av én rettskilde.</summary>
public sealed record StrukturforslagRapport(
    Guid RettskildeId, string RettskildeTittel, string Lag, string Oppdagelseskilde, int NoderLest, int UtsagnTotalt,
    IReadOnlyList<ForslagKategoritall> PerKategori, IReadOnlyList<ForslagArsaktall> IkkeLastetPerArsak,
    IReadOnlyList<UloestAktor> UloesteAktorer, IReadOnlyList<ForslagsutfallRad> Rader,
    int? KiInputTokens = null, int? KiOutputTokens = null, int? KiKastedeRader = null);

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

        // Eksisterende kanter hjemlet i rettskilden, også de avviste — nøkkelen avgjør om et utsagn alt er lastet eller avvist.
        var eksisterende = new Dictionary<Nokkel, StrukturkantEntitet>();
        foreach (var k in await kanter.GjeldendeInklAvviste.Where(k => k.HjemmelRettskildeId == rettskildeId && k.HjemmelEid != null).ToListAsync(ct))
        {
            // Står samme nøkkel flere ganger (to avgrensninger, et omstridt par), teller den første — ingen av dem lages på nytt.
            eksisterende.TryAdd(NokkelFor(k), k);
        }

        var aktorer = dokument.Aktorer.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var oppslagCache = new Dictionary<string, Oppslag>(StringComparer.Ordinal);
        async Task<Oppslag> SlaOppAsync(string aktorId)
        {
            if (oppslagCache.TryGetValue(aktorId, out var c)) return c;
            var aktor = aktorer[aktorId];
            var treff = new HashSet<Kantnode>();
            var treffnavn = new List<string>();
            foreach (var form in new[] { aktor.Tekstform }.Concat(aktor.Varianter).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (await virksomhetOppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(form, ct) is { } vid && treff.Add(Kantnode.Virksomhet(vid)))
                {
                    treffnavn.Add($"virksomhet:{form}");
                }
                var lower = form.ToLower();
                var begreper = await db.Begreper
                    .Where(b => b.Entitetsstatus == "gjeldende" && Nodetyper.MedGruppefunksjon.Contains(b.Begrepskategori!) && b.Term.ToLower() == lower)
                    .Select(b => new { b.Id, b.Begrepskategori }).Take(3).ToListAsync(ct);
                foreach (var b in begreper)
                {
                    if (treff.Add(Kantnode.Begrep(b.Id))) treffnavn.Add($"{b.Begrepskategori}:{form}");
                }
            }
            var svar = treff.Count switch
            {
                1 => new Oppslag(treff.Single(), null, treffnavn),
                0 => new Oppslag(null, Forslagsarsak.AktorIkkeIKatalogen, treffnavn),
                _ => new Oppslag(null, Forslagsarsak.AktorFlertydig, treffnavn),
            };
            oppslagCache[aktorId] = svar;
            return svar;
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
            var fra = await SlaOppAsync(u.Fra);
            var til = u.Til is null ? null : await SlaOppAsync(u.Til);
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
                Begrunnelse: u.Begrunnelse ?? (normalisering ? $"Regel: {kilde} (#307)" : null));
            var nokkel = new Nokkel(u.Eid, kat, kode, ny.Fra.VirksomhetId, ny.Fra.BegrepId, ny.Til?.VirksomhetId, ny.Til?.BegrepId,
                ny.Undertype, ny.Normform, ny.Polaritet, ny.Modalitet);
            if (eksisterende.TryGetValue(nokkel, out var fantes))
            {
                var utfall = settIKjoringen.Contains(nokkel) ? Forslagsutfall.DublettIKjoringen
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
            if (arsak == Forslagsarsak.AktorIkkeIKatalogen)
            {
                var tekst = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId && n.Eid == eid && n.Entitetsstatus == "gjeldende")
                    .Select(n => n.Tekst).FirstOrDefaultAsync(ct) ?? "";
                var start = tekst.IndexOf(aktor.Tekstform, StringComparison.Ordinal);
                if (start < 0) start = tekst.IndexOf(aktor.Tekstform, StringComparison.OrdinalIgnoreCase);
                if (start >= 0)
                {
                    var kandidat = await navnekandidater.OpprettEllerFinnAsync(
                        tekst.Substring(start, aktor.Tekstform.Length), Nodetyper.Gruppe, rettskildeId, eid, start, start + aktor.Tekstform.Length,
                        opprettetAv, ct, NavnekandidatOppdagelseskilde);
                    kandidatId = kandidat.Id;
                }
            }
            uloesteAktorer.Add(new UloestAktor(aktor.Tekstform, arsak, antall, kandidatId, treff));
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

        return new StrukturforslagRapport(rettskildeId, rettskilde.Kortnavn ?? rettskilde.Tittel, lag, standardOppdagelseskilde,
            dokument.NoderLest, dokument.Utsagn.Count, perKategori, perArsak,
            uloesteAktorer.OrderByDescending(a => a.AntallUtsagn).ThenBy(a => a.Tekstform, StringComparer.Ordinal).ToList(), rader);
    }
}
