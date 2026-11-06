using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #384, Johanns beslutning 2026-10-16] Det lukkede vokabularet for <see cref="BegrepEntitet.Medlemskapsregel"/> — speilet av
/// CHECK <c>ck_begreper_medlemskapsregel</c>. Endres det ene, må det andre endres i samme migrasjon (samme regel som <see cref="Nodetyper"/>).
/// </summary>
public static class Klasseregler
{
    /// <summary>Klassen «kommune»: kommunene i områderegisteret (Kartverkets inndeling) med sitt rettssubjekt.</summary>
    public const string OmraderegisterKommune = "omraderegister_kommune";

    /// <summary>[Ny, issue #442, Johanns avgjørelse 2026-11-05] Klassen «fylkeskommune»: hvert fylke unntatt Oslo utgjør en fylkeskommune
    /// (kommuneloven § 3-1 første ledd andre punktum). Medlem er aktøren med forvaltningsnivå fylkeskommune og aktørtype rettssubjekt som har
    /// territoriekanten med hjemmel i § 3-1 første ledd til fylket. Statsforvalteren har også ansvarsområde til fylkene, men verken nivået,
    /// aktørtypen eller hjemmelen (juristrunden N8). Kanten skrives av <see cref="KlasseoppsettTjeneste"/>, ikke av registeret.</summary>
    public const string OmraderegisterFylkeskommune = "omraderegister_fylkeskommune";

    /// <summary>[Ny, issue #442, Johanns avgjørelse 2026-11-05] Klassen «departement»: ingen lovdefinisjon (Grunnloven bruker ikke ordet, og
    /// ingen lov definerer det, juristrunden N12). Kilden er organiseringsvedtakene. Medlem er en levende aktør med R <c>oppretter</c> lest av
    /// livsløpet (docs/33 §5.9), ellers en M-kant til klassen uten avgrensning, med navnevedtaket eller en kgl.res. utenfor korpus som premiss
    /// (fortolkning: vedtaket forutsetter departementet, men oppretter det ikke).</summary>
    public const string OrganiseringsvedtakDepartement = "organiseringsvedtak_departement";

    public static readonly string[] Alle = [OmraderegisterKommune, OmraderegisterFylkeskommune, OrganiseringsvedtakDepartement];

    /// <summary>[Ny, issue #442] Kommuneloven § 3-1 første ledd: «Hver kommune hører til et fylke. Hvert fylke unntatt Oslo utgjør en
    /// fylkeskommune.» Hjemmelen for territoriekanten fylkeskommune → fylke og kilden for regelen.</summary>
    public const string KommunelovenFylkeskommuneEid = "https://lovdata.no/eli/lov/2018/06/22/83/nor/§3-1/ledd-1";

    /// <summary>[Ny, issue #442] Kommuneloven § 3-1 fjerde ledd: «Fylkeskommunen skal ha samme navn som fylket.» Navneregelen som parer
    /// fylket med fylkeskommunen (samme mønster som lagdømmet og lagmannsretten i #345).</summary>
    public const string KommunelovenNavneregelEid = "https://lovdata.no/eli/lov/2018/06/22/83/nor/§3-1/ledd-4";

    /// <summary>[Ny, issue #442] Fylket loven unntar: «Hvert fylke unntatt Oslo utgjør en fylkeskommune». Ordet er lovens, og
    /// <see cref="KlasseoppsettTjeneste"/> kontrollerer at det står i § 3-1 første ledd før noe skrives. Oslo kommune og fylkeskommunens
    /// oppgaver er sak 3 (#464).</summary>
    public const string UnntattFylke = "Oslo";

    /// <summary>[Ny, issue #442] Det oppdagelseskilden for livsløpets R oppretter begynner med (<c>monster:livslop</c>,
    /// <c>monster:livslop-nytt-departement</c>). Livsløpet skriver R oppretter bare fra Kongen i statsråd til et departement.</summary>
    public const string LivslopPrefiks = "monster:livslop";

    /// <summary>[Ny, issue #442] Hva regelen sier, og kilden, slik klassevisningen og aktørsiden viser den. <see cref="KildeEider"/> er
    /// bestemmelsene i korpus regelen bygger på (tom når kilden er et register eller vedtak som står som premisser).</summary>
    public sealed record Regelbeskrivelse(string Regel, string Tekst, string Kilde, IReadOnlyList<string> KildeEider);

    public static Regelbeskrivelse? Beskriv(string? regel) => regel switch
    {
        OmraderegisterKommune => new(regel,
            "Kommunene i områderegisteret, med virksomheten som har ansvarsområde til kommunen og samme kommunenummer.",
            "Områderegisteret (Kartverket) og Enhetsregisteret", []),
        OmraderegisterFylkeskommune => new(regel,
            "Hvert fylke unntatt Oslo utgjør en fylkeskommune: aktøren med forvaltningsnivå fylkeskommune og aktørtype rettssubjekt som har "
            + "territoriekanten etter kommuneloven § 3-1 første ledd til fylket.",
            "Kommuneloven § 3-1 første ledd andre punktum og fjerde ledd", [KommunelovenFylkeskommuneEid, KommunelovenNavneregelEid]),
        OrganiseringsvedtakDepartement => new(regel,
            "Levende aktører som organiseringsvedtakene oppretter (R oppretter fra Kongen i statsråd), eller som har en medlemskant med "
            + "navnevedtaket eller en kongelig resolusjon som premiss.",
            "Organiseringsvedtakene (kgl.res.)", []),
        _ => null,
    };

    /// <summary>
    /// Id-en migrasjonen <c>KlasseKommuneOgUtledetMedlemskap</c> gir klassen «kommune». Den er fast bare for at migrasjonens <c>Down</c>
    /// skal treffe nøyaktig raden <c>Up</c> la inn; koden finner klassen på regelen (<see cref="KlassemedlemskapTjeneste.KlasseForRegelAsync"/>).
    /// </summary>
    public static readonly Guid KommuneKlasseId = Guid.Parse("eb25cd79-007e-404d-9cd5-c85800b65272");

    /// <summary>[ENDRET, Johanns beslutning på PR #388 2026-10-16] Hjemmelen for klassen «kommune»: kommuneloven § 2-1 FØRSTE LEDD, leddet
    /// som deler landet inn i kommuner. Ordrett i korpus (lokal <c>regelide</c>, kommuneloven versjon 2, lest 2026-10-16): «Norge er inndelt i
    /// kommuner og fylkeskommuner med en egen folkevalgt ledelse.» Annet ledd («Hver kommune og fylkeskommune er et eget rettssubjekt …»)
    /// sier hva medlemmene er, ikke hvilke som finnes, og er derfor ikke hjemmelen for klassen. Før rettingen pekte hjemmelen på hele § 2-1.
    /// eId-en er verksnivå (lovens ELI + bestemmelsen), ikke en bestemt versjon.</summary>
    public const string KommunelovenInndelingEid = "https://lovdata.no/eli/lov/2018/06/22/83/nor/§2-1/ledd-1";
}

/// <summary>Ett utledet medlem: aktøren, området den er medlem for (kommune eller fylke; null for departementene) og premisset.
/// [ENDRET, issue #442] <c>Kommunenummer</c> heter <see cref="Omradekode"/> (fylkesnummer for en fylkeskommune), og område er valgfritt.
/// <see cref="Premissart"/>: <c>territoriekant</c> (A til området) | <c>oppretterkant</c> (R oppretter fra livsløpet) | <c>medlemskant</c>
/// (M til klassen, med navnevedtaket eller en kgl.res. som premiss). <see cref="Kunnskapsstatus"/> er svaret for dette medlemmet etter
/// svakeste premiss (docs/33 §4.6): et premiss som ikke er vurdert, gir <c>uavklart</c>, og en fortolkning gir <c>fortolkning</c>.</summary>
public sealed record UtledetMedlem(
    Guid VirksomhetId, string Navn, string? Omradekode, Guid? OmradeId, string? Omradenavn, Guid PremissKantId, bool Validert,
    string Premissart, string? PremissKunnskapsstatus, string Kunnskapsstatus);

/// <summary>[Ny, issue #442] Hva premisset for et utledet medlemskap er.</summary>
public static class Premissart
{
    public const string Territoriekant = "territoriekant";
    public const string Oppretterkant = "oppretterkant";
    public const string Medlemskant = "medlemskant";
}

/// <summary>
/// Svaret på «hvem er medlem av klassen?» for en klasse med <see cref="BegrepEntitet.Medlemskapsregel"/>. Medlemskapet er ikke lagret; det
/// følger av premissene. [ENDRET, issue #442] <see cref="Kunnskapsstatus"/> er svaret etter det svakeste premisset blant medlemmene (før: alltid
/// <c>avledet</c>, som fortsatt er svaret for kommunene); <see cref="KlassensKunnskapsstatus"/> gjelder klassen selv.
/// <see cref="Hull"/> er det som ikke kunne gjøres til medlem, i klartekst — det er IKKE med i <see cref="Medlemmer"/>.
/// <see cref="AntallOmrader"/> er antall områder regelen går gjennom (kommuner, fylker unntatt Oslo), null for departementene.
/// <see cref="Unntatt"/> er det loven selv unntar (Oslo for fylkeskommunen). Det er ikke et hull.
/// </summary>
public sealed record UtledetMedlemskap(
    Guid KlasseId, string Term, string Regel, string? Hjemmel, string? KlassensKunnskapsstatus, string Kunnskapsstatus,
    int? AntallOmrader, IReadOnlyList<UtledetMedlem> Medlemmer, IReadOnlyList<string> Hull)
{
    public string? Omradetype { get; init; }
    public IReadOnlyList<string> Unntatt { get; init; } = [];
    public Klasseregler.Regelbeskrivelse? Beskrivelse { get; init; }
}

/// <summary>[Ny, issue #442] En bestemmelse i korpus som klassens hjemmel eller medlemsregel bygger på, med rettskilden når den er funnet.</summary>
public sealed record KlassegrunnlagKilde(string Eid, Guid? RettskildeId, string? Tittel);

/// <summary>
/// [Ny, issue #442, Johanns avgjørelse 2026-11-05: «Klasser må ha minst hjemmel eller medlemsregel, og kan ha begge.»] Grunnlaget for en klasse,
/// slik klassevisningen og aktørsidens hjemmelkort viser det. <see cref="Status"/>: <c>begge</c> | <c>hjemmel</c> | <c>medlemsregel</c> | <c>mangler</c>.
/// Hullet vises bare ved <c>mangler</c> (<see cref="Vurder"/>). Hjemmel er en bestemmelse i korpus (<c>lovreferanse_eid</c>) eller en kilde utenfor
/// korpus på begrepet.
/// </summary>
public sealed record Klassegrunnlag(
    Guid KlasseId, string Term, string Status, KlassegrunnlagKilde? Hjemmel, string? KildeUtenforKorpus, string? Kunnskapsstatus, string? Godkjenning,
    string? Begrunnelse, Klasseregler.Regelbeskrivelse? Medlemsregel, IReadOnlyList<KlassegrunnlagKilde> RegelKilder)
{
    public const string Begge = "begge";
    public const string BareHjemmel = "hjemmel";
    public const string BareMedlemsregel = "medlemsregel";
    public const string Mangler = "mangler";

    /// <summary>Regelen: minst én av dem, og begge er lov. Ren funksjon, så den kan testes uten database.</summary>
    public static string Vurder(bool harHjemmel, bool harMedlemsregel) => (harHjemmel, harMedlemsregel) switch
    {
        (true, true) => Begge,
        (true, false) => BareHjemmel,
        (false, true) => BareMedlemsregel,
        _ => Mangler,
    };
}

/// <summary>
/// [Ny, issue #384, Johanns beslutning 2026-10-16, docs/33 §4.2] Medlemskap som UTLEDES, ikke registreres: «kommune» har 357 medlemmer, og
/// de står alt i områderegisteret (#312). Å legge inn 357 M-kanter i tillegg ville lagret det samme to ganger, og de ville blitt gale ved
/// neste kommunesammenslåing uten at noen merket det. Samme prinsipp som domstolkjeden i #345: bare det kilden sier, lagres; resten regnes ut.
/// <para>
/// <b>Regelen <c>omraderegister_kommune</c></b> er den samme som rubrikken «kommune» i <see cref="OmradeOppslagTjeneste.ForKommuneAsync"/>
/// (<c>ErKommunensEgen</c>): for hver gjeldende kommune i områderegisteret, aktøren med <c>A har_ansvarsomrade</c> til kommunen og samme
/// <see cref="Virksomhet.Kommunenummer"/> som områdekoden. Nøyaktig én → medlem, med territoriekanten som premiss. Ingen eller flere → ikke
/// medlem, og kommunen står i <see cref="UtledetMedlemskap.Hull"/> (CLAUDE.md §8, ingen gjetting). En tingrett har også ansvarsområde til
/// kommunen, men et annet kommunenummer — den er ikke medlem.
/// </para>
/// </summary>
public sealed class KlassemedlemskapTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste)
{
    /// <summary>Den gjeldende klassen med regelen, eller null (migrasjonen kan ha latt være å opprette den, se den).</summary>
    public Task<BegrepEntitet?> KlasseForRegelAsync(string regel, CancellationToken ct = default) =>
        db.Begreper.FirstOrDefaultAsync(b => b.Medlemskapsregel == regel && b.Entitetsstatus == "gjeldende", ct);

    /// <summary>[Ny, issue #442] Grunnlaget for klassen (<see cref="Klassegrunnlag"/>), eller null når id-en ikke er en gjeldende klasse.</summary>
    public async Task<Klassegrunnlag?> GrunnlagAsync(Guid klasseId, CancellationToken ct = default)
    {
        var b = await db.Begreper.FirstOrDefaultAsync(x => x.Id == klasseId && x.Entitetsstatus == "gjeldende" && x.Begrepskategori == Nodetyper.Klasse, ct);
        if (b is null) return null;
        var regel = Klasseregler.Beskriv(b.Medlemskapsregel);
        var hjemmel = b.LovreferanseEid is { } eid ? await KildeAsync(eid, b.LovkildeId, ct) : null;
        var regelKilder = new List<KlassegrunnlagKilde>();
        foreach (var e in regel?.KildeEider ?? []) regelKilder.Add(await KildeAsync(e, null, ct));
        var status = Klassegrunnlag.Vurder(hjemmel is not null || b.KildeUtenforKorpusTekst is not null, regel is not null);
        return new Klassegrunnlag(b.Id, b.Term, status, hjemmel, b.KildeUtenforKorpusTekst, b.Kunnskapsstatus, b.Godkjenning, b.Begrunnelse, regel, regelKilder);
    }

    /// <summary>Et fast begrep (LovkildeId NULL, #298) har bestemmelsen bare som eId: rettskilden er den gjeldende versjonen som har noden.</summary>
    private async Task<KlassegrunnlagKilde> KildeAsync(string eid, Guid? lovkildeId, CancellationToken ct)
    {
        var kilde = lovkildeId is { } l
            ? await db.Rettskilder.Where(r => r.Id == l).Select(r => new { r.Id, Tittel = r.Kortnavn ?? r.Tittel }).FirstOrDefaultAsync(ct)
            : await db.RettskildeNoder.Where(n => n.Eid == eid && n.Entitetsstatus == "gjeldende")
                .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende"), n => n.RettskildeId, r => r.Id, (n, r) => new { r.Id, Tittel = r.Kortnavn ?? r.Tittel })
                .FirstOrDefaultAsync(ct);
        return new KlassegrunnlagKilde(eid, kilde?.Id, kilde?.Tittel);
    }

    /// <summary>Klassene med utledet medlemskap, med regelen.</summary>
    public async Task<Dictionary<Guid, string>> KlasserMedRegelAsync(CancellationToken ct = default) =>
        await db.Begreper.Where(b => b.Medlemskapsregel != null && b.Entitetsstatus == "gjeldende")
            .ToDictionaryAsync(b => b.Id, b => b.Medlemskapsregel!, ct);

    /// <summary>
    /// Medlemmene av klassen <paramref name="klasseId"/> etter regelen, eller null når klassen ikke har noen regel (da er medlemskapet
    /// registrert med M-kanter, og spørres der). Med <paramref name="bareVirksomhet"/> returneres bare den aktørens medlemskap (og ingen hull).
    /// </summary>
    public async Task<UtledetMedlemskap?> UtledAsync(Guid klasseId, Guid? bareVirksomhet = null, CancellationToken ct = default)
    {
        if (!_utledet.TryGetValue(klasseId, out var alle))
        {
            var klasse = await db.Begreper.FirstOrDefaultAsync(b => b.Id == klasseId && b.Entitetsstatus == "gjeldende" && b.Medlemskapsregel != null, ct);
            alle = klasse is null ? null : klasse.Medlemskapsregel switch
            {
                Klasseregler.OmraderegisterKommune => await KommuneAsync(klasse, ct),
                Klasseregler.OmraderegisterFylkeskommune => await FylkeskommuneAsync(klasse, ct),
                Klasseregler.OrganiseringsvedtakDepartement => await DepartementAsync(klasse, ct),
                // CHECK-en slipper ikke inn andre verdier; står det likevel noe annet, er det en regel koden ikke kjenner — ingen gjetting.
                _ => throw new InvalidOperationException($"Ukjent medlemskapsregel '{klasse.Medlemskapsregel}' på «{klasse.Term}»."),
            };
            _utledet[klasseId] = alle;
        }
        // Hele registeret regnes alltid ut, også for én aktør: entydigheten avgjøres per kommune, ikke per aktør.
        return alle is null || bareVirksomhet is not { } bv ? alle
            : alle with { Medlemmer = alle.Medlemmer.Where(m => m.VirksomhetId == bv).ToList(), Hull = [] };
    }

    /// <summary>Utregningen per klasse, for levetiden til tjenesten (ett HTTP-kall): pliktoppslaget spør om samme klasse for hver kant.</summary>
    private readonly Dictionary<Guid, UtledetMedlemskap?> _utledet = [];

    private async Task<UtledetMedlemskap> KommuneAsync(BegrepEntitet klasse, CancellationToken ct)
    {
        var kommuner = await db.Begreper
            .Where(b => b.Begrepskategori == Nodetyper.Omrade && b.Omradetype == Omradetyper.Kommune && b.Omradekode != null
                        && b.Entitetsstatus == "gjeldende" && b.GyldigTil == null)
            .Select(b => new { b.Id, b.Term, Kode = b.Omradekode! }).ToListAsync(ct);
        var kommuneIder = kommuner.Select(k => k.Id).ToList();
        var kanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade" && k.FraVirksomhetId != null
                        && k.TilBegrepId != null && kommuneIder.Contains(k.TilBegrepId.Value))
            .ToListAsync(ct), ct);
        var aktorIder = kanter.Select(k => k.FraVirksomhetId!.Value).Distinct().ToList();
        var visning = await new VirksomhetVisningsnavnTjeneste(db).AlleAsync(ct); // [ENDRET, issue #385 AC2] visningsnavnet, ikke registernavnet
        var aktorer = (await db.Virksomheter.Where(v => aktorIder.Contains(v.Id))
            .Select(v => new { v.Id, v.Navn, v.Kommunenummer }).ToListAsync(ct))
            .Select(v => v with { Navn = visning.GetValueOrDefault(v.Id) ?? v.Navn }).ToDictionary(v => v.Id);
        var perKommune = kanter.GroupBy(k => k.TilBegrepId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var medlemmer = new List<UtledetMedlem>();
        var hull = new List<string>();
        foreach (var k in kommuner.OrderBy(k => k.Kode, StringComparer.Ordinal))
        {
            var egne = (perKommune.GetValueOrDefault(k.Id) ?? [])
                .Where(x => aktorer.TryGetValue(x.FraVirksomhetId!.Value, out var v) && v.Kommunenummer == k.Kode)
                .GroupBy(x => x.FraVirksomhetId!.Value).ToList();
            if (egne.Count == 1)
            {
                // Flere kanter fra samme aktør til samme kommune (f.eks. en godkjent og et forslag): den godkjente er premisset.
                var kant = egne[0].OrderBy(x => x.Status == Strukturkanter.Godkjent ? 0 : 1).ThenBy(x => x.Id).First();
                var v = aktorer[egne[0].Key];
                medlemmer.Add(new UtledetMedlem(v.Id, v.Navn, k.Kode, k.Id, k.Term, kant.Id, kant.Status == Strukturkanter.Godkjent,
                    Premissart.Territoriekant, kant.Kunnskapsstatus, SvakestePremiss.SvarStatus(kant.Kunnskapsstatus)));
            }
            else
            {
                hull.Add(egne.Count == 0
                    ? $"{k.Kode} {k.Term}: ingen aktør med ansvarsområde til kommunen og samme kommunenummer — ikke medlem (ikke funnet ≠ oppfunnet)."
                    : $"{k.Kode} {k.Term}: {egne.Count} aktører med ansvarsområde til kommunen og samme kommunenummer — ikke entydig, ingen valgt.");
            }
        }
        // Det samlede svaret for kommunene er uendret (avledet): å håndheve svakeste premiss også her er #411. Hvert medlem har sitt eget svar.
        return new UtledetMedlemskap(klasse.Id, klasse.Term, Klasseregler.OmraderegisterKommune, klasse.LovreferanseEid, klasse.Kunnskapsstatus,
            Strukturkanter.Avledet, kommuner.Count, medlemmer, hull)
        { Omradetype = Omradetyper.Kommune, Beskrivelse = Klasseregler.Beskriv(Klasseregler.OmraderegisterKommune) };
    }

    /// <summary>[Ny, issue #442] Det samlede svaret for klassen etter det svakeste premisset blant medlemmene (docs/33 §4.6). Uten medlemmer:
    /// <c>avledet</c> (det er ingenting å svekke; hullene sier hva som mangler).</summary>
    private static string Samlet(IReadOnlyList<UtledetMedlem> medlemmer) =>
        medlemmer.Count == 0 ? Strukturkanter.Avledet
            : SvakestePremiss.SvarStatus(SvakestePremiss.Finn(medlemmer.Select(m => (m.PremissKantId, m.PremissKunnskapsstatus)))!.Value.Kunnskapsstatus);

    /// <summary>
    /// [Ny, issue #442, Johanns avgjørelse 2026-11-05, juristrunden N8/N9] Regelen <c>omraderegister_fylkeskommune</c>: for hvert gjeldende fylke i
    /// områderegisteret unntatt Oslo, aktøren som (1) har forvaltningsnivå fylkeskommune, (2) har aktørtype rettssubjekt, (3) lever og ikke er
    /// erstattet, og (4) har <c>A har_ansvarsomrade</c> til fylket med hjemmel i kommuneloven § 3-1 første ledd. Nøyaktig én → medlem, med kanten som
    /// premiss. Ingen eller flere → hull. Statsforvalteren har også ansvarsområde til fylket, men oppfyller ingen av de tre første vilkårene og har
    /// ikke hjemmelen. Et vilkår alene ville vært for skjørt (N8). Registeret brukes ikke som kilde for kanten (N9); kanten skrives av
    /// <see cref="KlasseoppsettTjeneste"/> med navneregelen i § 3-1 fjerde ledd.
    /// </summary>
    private async Task<UtledetMedlemskap> FylkeskommuneAsync(BegrepEntitet klasse, CancellationToken ct)
    {
        var fylker = await db.Begreper
            .Where(b => b.Begrepskategori == Nodetyper.Omrade && b.Omradetype == Omradetyper.Fylke && b.Omradekode != null
                        && b.Entitetsstatus == "gjeldende" && b.GyldigTil == null)
            .Select(b => new { b.Id, b.Term, Kode = b.Omradekode! }).ToListAsync(ct);
        var unntatt = fylker.Where(f => f.Term == Klasseregler.UnntattFylke).OrderBy(f => f.Kode, StringComparer.Ordinal).ToList();
        var regelFylker = fylker.Where(f => f.Term != Klasseregler.UnntattFylke).OrderBy(f => f.Kode, StringComparer.Ordinal).ToList();
        var fylkeIder = regelFylker.Select(f => f.Id).ToList();
        var kanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade" && k.FraVirksomhetId != null
                        && k.TilBegrepId != null && fylkeIder.Contains(k.TilBegrepId.Value)
                        && k.HjemmelEid == Klasseregler.KommunelovenFylkeskommuneEid)
            .ToListAsync(ct), ct);
        var aktorIder = kanter.Select(k => k.FraVirksomhetId!.Value).Distinct().ToList();
        var visning = await new VirksomhetVisningsnavnTjeneste(db).AlleAsync(ct);
        var aktorer = (await db.Virksomheter
                .Where(v => aktorIder.Contains(v.Id) && v.Forvaltningsniva == "fylkeskommune" && v.Aktortype == "rettssubjekt"
                            && v.Livsstatus == null && v.ErstattetAvId == null)
                .Select(v => new { v.Id, v.Navn }).ToListAsync(ct))
            .ToDictionary(v => v.Id, v => visning.GetValueOrDefault(v.Id) ?? v.Navn);

        var medlemmer = new List<UtledetMedlem>();
        var hull = new List<string>();
        foreach (var f in regelFylker)
        {
            var egne = kanter.Where(k => k.TilBegrepId == f.Id && aktorer.ContainsKey(k.FraVirksomhetId!.Value))
                .GroupBy(k => k.FraVirksomhetId!.Value).ToList();
            if (egne.Count == 1)
            {
                var kant = egne[0].OrderBy(x => x.Status == Strukturkanter.Godkjent ? 0 : 1).ThenBy(x => x.Id).First();
                medlemmer.Add(new UtledetMedlem(egne[0].Key, aktorer[egne[0].Key], f.Kode, f.Id, f.Term, kant.Id, kant.Status == Strukturkanter.Godkjent,
                    Premissart.Territoriekant, kant.Kunnskapsstatus, SvakestePremiss.SvarStatus(kant.Kunnskapsstatus)));
            }
            else
            {
                hull.Add(egne.Count == 0
                    ? $"{f.Kode} {f.Term}: ingen fylkeskommune (rettssubjekt) med territoriekant etter kommuneloven § 3-1 første ledd. Ikke medlem (ikke funnet ≠ oppfunnet)."
                    : $"{f.Kode} {f.Term}: {egne.Count} fylkeskommuner med territoriekant til fylket. Ikke entydig, ingen valgt.");
            }
        }
        return new UtledetMedlemskap(klasse.Id, klasse.Term, Klasseregler.OmraderegisterFylkeskommune, klasse.LovreferanseEid, klasse.Kunnskapsstatus,
            Samlet(medlemmer), regelFylker.Count, medlemmer, hull)
        {
            Omradetype = Omradetyper.Fylke,
            Unntatt = unntatt.Select(f => $"{f.Kode} {f.Term}: «Hvert fylke unntatt Oslo utgjør en fylkeskommune» (kommuneloven § 3-1 første ledd). "
                                          + "Oslo kommune i fylkeskommunens sted er egen sak (#464).").ToList(),
            Beskrivelse = Klasseregler.Beskriv(Klasseregler.OmraderegisterFylkeskommune),
        };
    }

    /// <summary>
    /// [Ny, issue #442, Johanns avgjørelse 2026-11-05, juristrunden N12] Regelen <c>organiseringsvedtak_departement</c>. Kandidatene er levende
    /// aktører (ikke nedlagt, ikke erstattet). Premissene prøves sammen, og det sterkeste vinner (svakeste premiss, så godkjent, så id):
    /// <list type="number">
    /// <item><b>R oppretter</b> lest av livsløpet (<see cref="Klasseregler.LivslopPrefiks"/>, docs/33 §5.9) til aktøren. Livsløpet skriver bare
    /// R oppretter fra Kongen i statsråd til et departement; en manuell R oppretter (Kongen i statsråd oppretter også utvalg og råd) teller ikke.</item>
    /// <item><b>M medlem_av</b> fra aktøren til klassen, uten avgrensning. For de sju uten oppretterkant har den navnevedtaket (i korpus) eller en
    /// kgl.res. utenfor korpus som hjemmel, merket fortolkning. En M-kant med avgrensning er lovspesifikk (Statsministerens kontor etter
    /// karanteneloven § 1, sak 3) og er ikke alminnelig medlemskap.</item>
    /// </list>
    /// <b>Hull:</b> en levende aktør med en gjeldende navneform hjemlet i navnevedtaket (<see cref="DepartementNavneformHjemmelTjeneste.OppdagelsesKilde"/>)
    /// men uten noen av premissene. Det er livsløpets eget spor av at aktøren er et departement; da er skrivingen for #442 ikke kjørt.
    /// </summary>
    private async Task<UtledetMedlemskap> DepartementAsync(BegrepEntitet klasse, CancellationToken ct)
    {
        var levende = (await db.Virksomheter.Where(v => v.Livsstatus == null && v.ErstattetAvId == null).Select(v => v.Id).ToListAsync(ct)).ToHashSet();
        var oppretter = (await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
                .Where(k => k.Kategori == Strukturkanter.Relasjon && k.Typekode == "oppretter" && k.TilVirksomhetId != null
                            && k.OppdagelsesKilde.StartsWith(Klasseregler.LivslopPrefiks))
                .ToListAsync(ct), ct))
            .Where(k => levende.Contains(k.TilVirksomhetId!.Value)).Select(k => (Aktor: k.TilVirksomhetId!.Value, Kant: k, Art: Premissart.Oppretterkant));
        var mKanter = (await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
                .Where(k => k.Kategori == Strukturkanter.Medlemskap && k.TilBegrepId == klasse.Id && k.FraVirksomhetId != null
                            && (k.AvgrensningParagrafspennJson == "[]" || k.AvgrensningParagrafspennJson == "") && k.AvgrensningTekst == null)
                .ToListAsync(ct), ct))
            .Where(k => levende.Contains(k.FraVirksomhetId!.Value)).Select(k => (Aktor: k.FraVirksomhetId!.Value, Kant: k, Art: Premissart.Medlemskant));
        var premisser = oppretter.Concat(mKanter).GroupBy(p => p.Aktor).ToList();
        var visning = await new VirksomhetVisningsnavnTjeneste(db).AlleAsync(ct);
        var ider = premisser.Select(g => g.Key).ToList();
        var navn = await db.Virksomheter.Where(v => ider.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Navn, ct);
        var sortering = StringComparer.Create(new System.Globalization.CultureInfo("nb-NO"), ignoreCase: true);

        var medlemmer = premisser.Select(g =>
            {
                var p = g.OrderBy(x => SvakestePremiss.Rang(x.Kant.Kunnskapsstatus)).ThenBy(x => x.Kant.Status == Strukturkanter.Godkjent ? 0 : 1)
                    .ThenBy(x => x.Kant.Id).First();
                return new UtledetMedlem(g.Key, visning.GetValueOrDefault(g.Key) ?? navn[g.Key], null, null, null, p.Kant.Id,
                    p.Kant.Status == Strukturkanter.Godkjent, p.Art, p.Kant.Kunnskapsstatus, SvakestePremiss.SvarStatus(p.Kant.Kunnskapsstatus));
            })
            .OrderBy(m => m.Navn, sortering).ToList();

        var medlemSett = medlemmer.Select(m => m.VirksomhetId).ToHashSet();
        var medNavnevedtak = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.Navneformgrunn == "gjeldende" && b.Entitetsstatus == "gjeldende"
                        && b.HjemmelArt == "navnevedtak" && b.OppdagelsesKilde == DepartementNavneformHjemmelTjeneste.OppdagelsesKilde
                        && b.VirksomhetReferanseId != null)
            .Select(b => b.VirksomhetReferanseId!.Value).Distinct().ToListAsync(ct);
        var hull = medNavnevedtak.Where(id => levende.Contains(id) && !medlemSett.Contains(id))
            .Select(id => $"{visning.GetValueOrDefault(id) ?? id.ToString()}: navnevedtaket er hjemmel for navnet, men det finnes verken R oppretter "
                          + "eller en medlemskant. Ikke medlem (ikke funnet ≠ oppfunnet).")
            .Order(sortering).ToList();
        return new UtledetMedlemskap(klasse.Id, klasse.Term, Klasseregler.OrganiseringsvedtakDepartement, klasse.LovreferanseEid,
            klasse.Kunnskapsstatus, Samlet(medlemmer), null, medlemmer, hull)
        { Beskrivelse = Klasseregler.Beskriv(Klasseregler.OrganiseringsvedtakDepartement) };
    }
}
