using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Utfallet for én aktør i forslagskjøringen for gjeldende navneform (<see cref="AktoridentitetTjeneste.ForeslaGjeldendeNavneformerAsync"/>).</summary>
public static class Navneformforslagutfall
{
    public const string Foreslatt = "foreslatt";
    public const string ForslagFinnes = "forslag_finnes";
    public const string TidligereAvvist = "tidligere_avvist";
    public const string NavneformFinnes = "navneform_finnes";
    public const string IngenTreff = "ingen_treff";
    /// <summary>Navnet står bare med små bokstaver eller bare i VERSALER — ingen skrivemåte å normalisere til.</summary>
    public const string IngenEgennavnform = "ingen_egennavnform";
    /// <summary>Navnet står med flere ulike skrivemåter med stor forbokstav; ingen velges.</summary>
    public const string FlereFormer = "flere_former";
}

/// <summary>Én aktør uten gjeldende navneform, og hva lovteksten sa.</summary>
public sealed record Navneformforslagrad(
    Guid VirksomhetId, string Registernavn, string? Organisasjonsnummer, string Utfall, string? Form, string? Kunnskapsstatus,
    Guid? RettskildeId, string? RettskildeTittel, string? Eid, int Forekomster, int Rettskilder, IReadOnlyList<string> Former, Guid? NavneformId);

public sealed record NavneformforslagRapport(bool Skrevet, int AktorerUtenGjeldendeNavneform, int Foreslatt, IReadOnlyList<Navneformforslagrad> Rader);

/// <summary>Tallene fra <see cref="AktoridentitetTjeneste.SikreTreffAsync"/>: hva et navnetreff ga av navneform, kilde, tagger og aktørtype.</summary>
public sealed record Treffidentitet(
    int NavneformForslag = 0, int KilderSatt = 0, int TaggerOpprettet = 0, int TaggerFantes = 0, int TaggerUtenEier = 0,
    int TaggerKolliderer = 0, int UlikSkrivemaate = 0, int AktortypeForslag = 0)
{
    public static Treffidentitet operator +(Treffidentitet a, Treffidentitet b) => new(
        a.NavneformForslag + b.NavneformForslag, a.KilderSatt + b.KilderSatt, a.TaggerOpprettet + b.TaggerOpprettet,
        a.TaggerFantes + b.TaggerFantes, a.TaggerUtenEier + b.TaggerUtenEier, a.TaggerKolliderer + b.TaggerKolliderer,
        a.UlikSkrivemaate + b.UlikSkrivemaate, a.AktortypeForslag + b.AktortypeForslag);
}

public sealed record EtterfyllingRapport(bool Skrevet, int KantTreff, int Aktorer, Treffidentitet Tall, IReadOnlyList<EtterfyllingRad> Rader);

/// <summary>Én aktør i én rettskilde i etterfyllingen: hva treffene ga (bare rader der noe skjedde eller noe ble hoppet over).</summary>
public sealed record EtterfyllingRad(Guid VirksomhetId, string Aktor, Guid RettskildeId, string RettskildeTittel, int Noder, Treffidentitet Tall);

/// <summary>
/// [Ny, issue #385 «aktøridentitet», Johann 2026-10-16] Aktørens identitet og primærnavn kommer fra rettskildene; orgnr og registernavn er
/// attributter. Tjenesten gjør tre ting, og ingen av dem setter noe som gjeldende uten et menneske:
/// <list type="number">
/// <item><b>Forslag til gjeldende navneform</b> der den mangler (AC1): registernavnet slås opp i lovteksten uten skille på store og små
/// bokstaver, og skrivemåten loven bruker foreslås. <c>tekstfunn</c> når skrivemåten er lik registernavnet, ellers <c>normalisering</c>
/// (regelen står i begrunnelsen). Bare én egennavnform (stor forbokstav, ikke bare versaler) gir forslag; ellers står aktøren på lista
/// med årsaken. Registernavnet normaliseres ALDRI uten en tekst som viser skrivemåten (CLAUDE.md §8: «Statsforvalteren i innlandet»).</item>
/// <item><b>Navnetreff fra aktørsveipet og lasteren</b> (AC3 og Johanns presisering 2026-10-16): navneformen registreres som forslag når
/// treffet gikk via registernavnet, navneformen får kilde (rettskilde + eId der navnet står), og tekstspennet tagges (tagg → navneform →
/// aktør) — bare der teksten er nøyaktig lik navneformen. Gjelder også treff som alt finnes (<see cref="EtterfyllKildeOgTaggerAsync"/>).</item>
/// <item><b>Aktørtype som modellutsagn</b> (AC4): forslag fra konverteringens entitetstype, fra veiviseren eller aktørsiden, med grunnlag;
/// <see cref="Virksomhet.Aktortype"/> settes først ved godkjenning.</item>
/// </list>
/// </summary>
public sealed class AktoridentitetTjeneste(RegelIdeDbContext db, TekstTaggTjeneste tekstTagger, VirksomhetOppslagTjeneste virksomhetOppslag)
{
    public const string ProveniensTypeAktortype = "aktortype_utsagn";
    public const string KildeLovtekstRegisternavn = "lovtekst:registernavn";
    public const string KildeEtterfylling = "etterfylling:kantens_hjemmel";
    private const int Kontekstlengde = 30;

    // ---------------------------------------------------------------- Navneform: forslag, godkjenning, avvisning

    /// <summary>AC1. Forslag til gjeldende navneform fra lovteksten for alle aktører som mangler én. <paramref name="skriv"/> = false er en
    /// ren tørrkjøring.</summary>
    /// <param name="bare">Avgrens til disse aktørene (tester og enkeltaktører); null = alle.</param>
    public async Task<NavneformforslagRapport> ForeslaGjeldendeNavneformerAsync(bool skriv, string bruker, CancellationToken ct = default,
        IReadOnlyCollection<Guid>? bare = null)
    {
        var navneformer = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId != null)
            .Select(b => new { Vid = b.VirksomhetReferanseId!.Value, b.Id, b.Term, b.Navneformgrunn, b.Godkjenning, b.Entitetsstatus })
            .ToListAsync(ct);
        // Bare en GODKJENT gjeldende navneform (eller en fra før #385) teller; et forslag som venter, rapporteres som «forslag finnes».
        var harGjeldende = navneformer.Where(n => n.Entitetsstatus == "gjeldende" && n.Navneformgrunn == "gjeldende"
            && (n.Godkjenning == null || n.Godkjenning == Strukturkanter.Godkjent)).Select(n => n.Vid).ToHashSet();
        var aktorer = await db.Virksomheter.Where(v => !harGjeldende.Contains(v.Id) && (bare == null || bare.Contains(v.Id))).OrderBy(v => v.Navn)
            .Select(v => new { v.Id, v.Navn, v.Organisasjonsnummer }).ToListAsync(ct);

        var rader = new List<Navneformforslagrad>();
        foreach (var v in aktorer)
        {
            if (navneformer.FirstOrDefault(n => n.Vid == v.Id && n.Entitetsstatus == "gjeldende" && n.Navneformgrunn == "gjeldende") is { } venter)
            {
                rader.Add(new Navneformforslagrad(v.Id, v.Navn, v.Organisasjonsnummer, Navneformforslagutfall.ForslagFinnes, venter.Term, null,
                    null, null, null, 0, 0, [], venter.Id));
                continue;
            }
            var forekomster = await ForekomsterAsync(v.Navn, ct);
            var former = forekomster.Select(f => f.Form).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal).ToList();
            var egennavn = former.Where(ErEgennavnform).ToList();
            Navneformforslagrad Rad(string utfall, string? form = null, string? ks = null, Forekomst? f = null, Guid? nid = null) => new(
                v.Id, v.Navn, v.Organisasjonsnummer, utfall, form, ks, f?.RettskildeId, f?.RettskildeTittel, f?.Eid,
                forekomster.Count(x => form is null || x.Form == form), forekomster.Where(x => form is null || x.Form == form)
                    .Select(x => x.RettskildeId).Distinct().Count(), former, nid);

            if (forekomster.Count == 0) { rader.Add(Rad(Navneformforslagutfall.IngenTreff)); continue; }
            if (egennavn.Count == 0) { rader.Add(Rad(Navneformforslagutfall.IngenEgennavnform)); continue; }
            if (egennavn.Count > 1) { rader.Add(Rad(Navneformforslagutfall.FlereFormer)); continue; }
            var form = egennavn[0];
            var finnes = navneformer.FirstOrDefault(n => n.Vid == v.Id && string.Equals(n.Term, form, StringComparison.OrdinalIgnoreCase));
            if (finnes is not null)
            {
                rader.Add(Rad(finnes.Godkjenning == Strukturkanter.Avvist ? Navneformforslagutfall.TidligereAvvist
                    : Navneformforslagutfall.NavneformFinnes, form, null, null, finnes.Id));
                continue;
            }
            var valgt = VelgForekomst(forekomster.Where(x => x.Form == form));
            var ks = form == v.Navn ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering;
            Guid? id = null;
            if (skriv)
            {
                var b = await OpprettNavneformforslagAsync(v.Id, form, "gjeldende", valgt, ks,
                    BegrunnelseLovtekst(v.Navn, form, forekomster.Count(x => x.Form == form)), KildeLovtekstRegisternavn, bruker, ct);
                id = b.Id;
            }
            rader.Add(Rad(Navneformforslagutfall.Foreslatt, form, ks, valgt, id));
        }
        return new NavneformforslagRapport(skriv, aktorer.Count, rader.Count(r => r.Utfall == Navneformforslagutfall.Foreslatt), rader);
    }

    private static string BegrunnelseLovtekst(string registernavn, string form, int antall) => form == registernavn
        ? $"Navnet står ordrett slik i lovteksten ({antall} forekomster)."
        : $"Registernavnet «{registernavn}» står i lovteksten som «{form}» ({antall} forekomster). Regel: samme ord uten skille på store og "
          + "små bokstaver, hele ord; skrivemåten med stor forbokstav (egennavn) er den eneste loven bruker.";

    /// <summary>En egennavnform: stor forbokstav, og ikke bare versaler (registerets VERSALFORM er ingen skrivemåte å normalisere til).</summary>
    public static bool ErEgennavnform(string s) =>
        s.Length > 0 && char.IsUpper(s[0]) && s.Any(char.IsLower);

    public sealed record Forekomst(Guid RettskildeId, string RettskildeTittel, string? Eli, string Eid, int Rekkefolge, int Start, string Form);

    /// <summary>Alle hele-ord-forekomster av <paramref name="navn"/> (uten skille på store og små bokstaver) i gjeldende noder i gjeldende
    /// rettskilder. Formen er teksten slik den står.</summary>
    private async Task<List<Forekomst>> ForekomsterAsync(string navn, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(navn) || navn.Length < 3) return [];
        var lower = navn.ToLower();
        var noder = await db.RettskildeNoder
            .Where(n => n.Entitetsstatus == "gjeldende" && !n.Opphevet && n.Tekst != null && n.Tekst.ToLower().Contains(lower))
            .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende"), n => n.RettskildeId, r => r.Id,
                (n, r) => new { n.RettskildeId, Tittel = r.Kortnavn ?? r.Tittel, r.Eli, n.Eid, n.Sorteringsrekkefolge, n.Tekst })
            .ToListAsync(ct);
        var regex = HeleOrd(navn, RegexOptions.IgnoreCase);
        return noder.SelectMany(n => regex.Matches(n.Tekst!).Select(m =>
                new Forekomst(n.RettskildeId, n.Tittel, n.Eli, n.Eid, n.Sorteringsrekkefolge, m.Index, m.Value)))
            .ToList();
    }

    private static Regex HeleOrd(string s, RegexOptions ekstra = RegexOptions.None) =>
        new($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(s)}(?![\p{{L}}\p{{N}}])", RegexOptions.CultureInvariant | ekstra);

    /// <summary>Hjemmelen for forslaget: rettskilden med flest forekomster (en lov foran en forskrift ved likt antall), første forekomst.</summary>
    private static Forekomst VelgForekomst(IEnumerable<Forekomst> f) => f
        .GroupBy(x => x.RettskildeId)
        .OrderByDescending(g => g.Count())
        .ThenBy(g => g.First().Eli?.Contains("/lov/") == true ? 0 : 1)
        .ThenBy(g => g.First().RettskildeTittel, StringComparer.Ordinal)
        .First().OrderBy(x => x.Rekkefolge).ThenBy(x => x.Start).First();

    private async Task<BegrepEntitet> OpprettNavneformforslagAsync(
        Guid virksomhetId, string term, string? grunn, Forekomst hjemmel, string kunnskapsstatus, string begrunnelse, string kilde,
        string bruker, CancellationToken ct)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            Begrepskategori = "virksomhet",
            VirksomhetReferanseId = virksomhetId,
            Navneformgrunn = grunn,
            Term = term,
            Status = "publisert",
            LovkildeId = hjemmel.RettskildeId,
            LovreferanseEid = hjemmel.Eid,
            Kunnskapsstatus = kunnskapsstatus,
            Begrunnelse = begrunnelse,
            Godkjenning = Strukturkanter.MaskineltForeslatt,
            OppdagelsesKilde = kilde,
            OpprettetAv = bruker,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", b.Id, virksomhetId: null, "foreslatt", bruker));
        await db.SaveChangesAsync(ct);
        return b;
    }

    /// <summary>Godkjenner en foreslått navneform. En ny gjeldende navneform kan ikke godkjennes mens aktøren har en annen godkjent
    /// gjeldende — det er én gjeldende per aktør, og hvilken av dem som er utgått, avgjør et menneske (ingen stille omklassifisering).</summary>
    public async Task<BegrepEntitet?> GodkjennNavneformAsync(Guid id, string bruker, CancellationToken ct = default)
    {
        var b = await db.Begreper.FirstOrDefaultAsync(x => x.Id == id && x.Begrepskategori == "virksomhet" && x.Entitetsstatus == "gjeldende", ct);
        if (b is null) return null;
        if (b.Godkjenning is null || !Strukturkanter.Ventende.Contains(b.Godkjenning))
        {
            throw new ArgumentException($"Navneformen «{b.Term}» er ikke et forslag (godkjenning '{b.Godkjenning ?? "registrert før #385"}').");
        }
        if (b.Navneformgrunn == "gjeldende" && b.VirksomhetReferanseId is { } vid)
        {
            var annen = await db.Begreper.Where(x => x.Id != id && x.Begrepskategori == "virksomhet" && x.VirksomhetReferanseId == vid
                    && x.Entitetsstatus == "gjeldende" && x.Navneformgrunn == "gjeldende"
                    && (x.Godkjenning == null || x.Godkjenning == Strukturkanter.Godkjent))
                .Select(x => x.Term).FirstOrDefaultAsync(ct);
            if (annen is not null)
            {
                throw new ArgumentException(
                    $"Aktøren har alt den gjeldende navneformen «{annen}». Endre grunnen på den først (f.eks. til utgått), så godkjenn denne.");
            }
        }
        b.Godkjenning = Strukturkanter.Godkjent;
        b.SistEndretAv = bruker;
        b.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var p = ProveniensHjelper.NyRad("begrep", b.Id, virksomhetId: null, "validert", bruker);
        p.GodkjentAv = bruker;
        db.Proveniens.Add(p);
        await db.SaveChangesAsync(ct);
        return b;
    }

    /// <summary>Avviser en foreslått navneform. Raden slettes ikke: den blir arkivert med hvem og hvorfor (#374-prinsippet), og taggene
    /// som pekte på den, slettes — en tagg kan ikke stå igjen og påstå en kjede som er avvist. Antallet returneres.</summary>
    public async Task<(BegrepEntitet Navneform, int TaggerSlettet)?> AvvisNavneformAsync(Guid id, string? begrunnelse, string bruker, CancellationToken ct = default)
    {
        var b = await db.Begreper.FirstOrDefaultAsync(x => x.Id == id && x.Begrepskategori == "virksomhet" && x.Entitetsstatus == "gjeldende", ct);
        if (b is null) return null;
        if (b.Godkjenning is null || !Strukturkanter.Ventende.Contains(b.Godkjenning))
        {
            throw new ArgumentException($"Navneformen «{b.Term}» er ikke et forslag. En godkjent navneform fjernes med «Fjern».");
        }
        var tagger = await db.TekstTagger.Where(t => t.RefId == id).ToListAsync(ct);
        db.TekstTagger.RemoveRange(tagger);
        b.Godkjenning = Strukturkanter.Avvist;
        b.Entitetsstatus = "arkivert";
        b.Status = "arkivert";
        b.AvvisningBegrunnelse = string.IsNullOrWhiteSpace(begrunnelse) ? null : begrunnelse.Trim();
        b.SistEndretAv = bruker;
        b.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", b.Id, virksomhetId: null, "avvist", bruker));
        await db.SaveChangesAsync(ct);
        return (b, tagger.Count);
    }

    /// <summary>Avviste navneforslag for en aktør (vises med «vis avviste»).</summary>
    public Task<List<BegrepEntitet>> AvvisteNavneformerAsync(Guid virksomhetId, CancellationToken ct = default) =>
        db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId && b.Godkjenning == Strukturkanter.Avvist)
            .ToListAsync(ct);

    /// <summary>Validerer grunnlaget og at hjemmelnoden finnes. Brukes av alle skriveveiene som tar et <see cref="Utsagnsgrunnlag"/>.</summary>
    public async Task ValiderAsync(Utsagnsgrunnlag g, CancellationToken ct)
    {
        g.Valider();
        if (g.Grunnlag == Utsagnsgrunnlag.Hjemmel
            && !await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == g.HjemmelRettskildeId && n.Eid == g.HjemmelEid!.Trim() && n.Entitetsstatus == "gjeldende", ct))
        {
            throw new ArgumentException($"Fant ingen node med eId '{g.HjemmelEid}' i rettskilden. Ingen gjettet fallback.");
        }
    }

    /// <summary>Skriver grunnlag og status på en navneform veiviseren har opprettet, eller på et forslag som et menneske nå har vurdert.
    /// En navneform registrert før #385 (godkjenning NULL) eller alt godkjent røres ikke — et tidligere menneskelig valg overskrives ikke
    /// stille. Returnerer om noe ble skrevet.</summary>
    public async Task<bool> SkrivGrunnlagPaaNavneformAsync(BegrepEntitet navneform, Utsagnsgrunnlag g, bool varNy, string bruker, CancellationToken ct)
    {
        if (!varNy && (navneform.Godkjenning is null || !Strukturkanter.Ventende.Contains(navneform.Godkjenning))) return false;
        await ValiderAsync(g, ct);
        g.SkrivTil(navneform, varNy ? "veiviser" : navneform.OppdagelsesKilde ?? "veiviser");
        navneform.SistEndretAv = bruker;
        navneform.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", navneform.Id, virksomhetId: null, "endret", bruker));
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------------------------------------------------------- Aktørtype som modellutsagn

    /// <summary>Registrerer (eller oppdaterer) utsagnet «aktøren er <paramref name="aktortype"/>». Idempotent per (aktør, type): finnes
    /// et ikke-avvist utsagn, oppdateres det bare når det er et forslag og et grunnlag sendes med. Godkjent → <see cref="Virksomhet.Aktortype"/>
    /// settes. Et avvist utsagn foreslås ikke på nytt av maskinen.</summary>
    public async Task<AktortypeUtsagnEntitet?> RegistrerAktortypeAsync(
        Guid virksomhetId, string aktortype, string? ordningstype, Utsagnsgrunnlag? grunnlag, string oppdagelsesKilde, string bruker,
        CancellationToken ct = default, string? maskinellBegrunnelse = null, Guid? maskinellHjemmelRettskildeId = null, string? maskinellHjemmelEid = null)
    {
        if (!Nodetyper.Aktortyper.Contains(aktortype))
        {
            throw new ArgumentException($"Ukjent aktørtype '{aktortype}'. Gyldige: {string.Join(", ", Nodetyper.Aktortyper)}.");
        }
        if (!Nodetyper.ErGyldigOrdningstype(aktortype, ordningstype))
        {
            throw new ArgumentException($"Ordningstype '{ordningstype}' er bare gyldig på en ordning.");
        }
        var virksomhet = await db.Virksomheter.FirstOrDefaultAsync(v => v.Id == virksomhetId, ct)
            ?? throw new ArgumentException($"Fant ingen aktør med id '{virksomhetId}'. Ingen gjettet fallback.");
        if (grunnlag is not null) await ValiderAsync(grunnlag, ct);

        var finnes = await db.AktortypeUtsagn.Where(a => a.VirksomhetId == virksomhetId && a.Aktortype == aktortype)
            .OrderByDescending(a => a.OpprettetTidspunkt).FirstOrDefaultAsync(ct);
        if (finnes is not null && (finnes.Status == Strukturkanter.Avvist && grunnlag is null)) return null; // avvist: maskinen gjentar ikke
        if (finnes is not null && finnes.Status != Strukturkanter.Avvist)
        {
            if (grunnlag is null || !Strukturkanter.Ventende.Contains(finnes.Status)) return finnes;
            grunnlag.SkrivTil(finnes);
            finnes.Ordningstype = ordningstype;
            finnes.SistEndretAv = bruker;
            finnes.SistEndretTidspunkt = DateTimeOffset.UtcNow;
            db.Proveniens.Add(ProveniensHjelper.NyRad(ProveniensTypeAktortype, finnes.Id, virksomhetId: null, "endret", bruker));
            if (finnes.Status == Strukturkanter.Godkjent) SettAktortype(virksomhet, finnes, bruker);
            await db.SaveChangesAsync(ct);
            return finnes;
        }

        var u = new AktortypeUtsagnEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Aktortype = aktortype,
            Ordningstype = ordningstype,
            OppdagelsesKilde = oppdagelsesKilde,
            OpprettetAv = bruker,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        if (grunnlag is not null) grunnlag.SkrivTil(u);
        else
        {
            // Maskinelt forslag: hvor aktøren står, ingen kunnskapsstatus (ikke vurdert) — et menneske vurderer.
            u.Status = Strukturkanter.MaskineltForeslatt;
            u.HjemmelRettskildeId = maskinellHjemmelRettskildeId;
            u.HjemmelEid = maskinellHjemmelRettskildeId is null ? null : maskinellHjemmelEid;
            u.Begrunnelse = maskinellBegrunnelse;
        }
        db.AktortypeUtsagn.Add(u);
        db.Proveniens.Add(ProveniensHjelper.NyRad(ProveniensTypeAktortype, u.Id, virksomhetId: null, "opprettet", bruker));
        if (u.Status == Strukturkanter.Godkjent) SettAktortype(virksomhet, u, bruker);
        await db.SaveChangesAsync(ct);
        return u;
    }

    private void SettAktortype(Virksomhet v, AktortypeUtsagnEntitet u, string bruker)
    {
        if (v.Aktortype == u.Aktortype && v.Ordningstype == u.Ordningstype) return;
        v.Aktortype = u.Aktortype;
        v.Ordningstype = u.Ordningstype;
        db.Proveniens.Add(ProveniensHjelper.NyRad("virksomhet", v.Id, virksomhetId: null, "endret", bruker));
    }

    public async Task<AktortypeUtsagnEntitet?> GodkjennAktortypeAsync(Guid id, string bruker, CancellationToken ct = default)
    {
        var u = await db.AktortypeUtsagn.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (u is null) return null;
        if (!Strukturkanter.Ventende.Contains(u.Status)) throw new ArgumentException($"Utsagnet har status '{u.Status}' — bare forslag kan godkjennes.");
        var v = await db.Virksomheter.FirstAsync(x => x.Id == u.VirksomhetId, ct);
        u.Status = Strukturkanter.Godkjent;
        u.SistEndretAv = bruker;
        u.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var p = ProveniensHjelper.NyRad(ProveniensTypeAktortype, u.Id, virksomhetId: null, "validert", bruker);
        p.GodkjentAv = bruker;
        db.Proveniens.Add(p);
        SettAktortype(v, u, bruker);
        await db.SaveChangesAsync(ct);
        return u;
    }

    public async Task<AktortypeUtsagnEntitet?> AvvisAktortypeAsync(Guid id, string? begrunnelse, string bruker, CancellationToken ct = default)
    {
        var u = await db.AktortypeUtsagn.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (u is null) return null;
        if (!Strukturkanter.Ventende.Contains(u.Status)) throw new ArgumentException($"Utsagnet har status '{u.Status}' — bare forslag kan avvises.");
        u.Status = Strukturkanter.Avvist;
        u.AvvisningBegrunnelse = string.IsNullOrWhiteSpace(begrunnelse) ? null : begrunnelse.Trim();
        u.SistEndretAv = bruker;
        u.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad(ProveniensTypeAktortype, u.Id, virksomhetId: null, "avvist", bruker));
        await db.SaveChangesAsync(ct);
        return u;
    }

    public Task<List<AktortypeUtsagnEntitet>> AktortypeUtsagnForAsync(Guid virksomhetId, bool visAvviste, CancellationToken ct = default) =>
        db.AktortypeUtsagn.Where(a => a.VirksomhetId == virksomhetId && (visAvviste || a.Status != Strukturkanter.Avvist))
            .OrderByDescending(a => a.OpprettetTidspunkt).ToListAsync(ct);

    // ---------------------------------------------------------------- Navnetreff: navneform, kilde, tagger, aktørtype

    /// <summary>
    /// AC3 + Johanns presisering 2026-10-16. Aktøren <paramref name="virksomhetId"/> er funnet ved navn i nodene <paramref name="eids"/> i
    /// <paramref name="rettskildeId"/>. For hver hele-ord-forekomst av en av aktørens navneformer eller registernavnet i nodeteksten:
    /// <list type="bullet">
    /// <item>teksten er NØYAKTIG lik en navneform → spennet tagges mot den (tagg → navneform → aktør);</item>
    /// <item>bare lik uten hensyn til store og små bokstaver → ingen tagg (telles som <c>UlikSkrivemaate</c>);</item>
    /// <item>treffet gikk via registernavnet og aktøren har ingen navneform med den skrivemåten → navneformen registreres som forslag
    /// (<c>maskinelt_foreslatt</c>, grunn <c>gjeldende</c> når aktøren mangler en gjeldende, ellers uspesifisert), med kilde, og tagges —
    /// men bare for en egennavnform (<see cref="ErEgennavnform"/>).</item>
    /// </list>
    /// En navneform uten kilde får kilden der den først står (<c>tekstfunn</c>: teksten står ordrett der). Med <paramref name="entitetstype"/>
    /// (konverteringens klassifisering) og ingen aktørtype på aktøren foreslås aktørtypen. Ingen spenn tagges der en annen koblet tagg
    /// overlapper; ingen tagg uten et ansvarlig departement som eier (samme degradering som veiviseren).
    /// </summary>
    public async Task<Treffidentitet> SikreTreffAsync(
        Guid virksomhetId, Guid rettskildeId, IEnumerable<string> eids, string? entitetstype, string oppdagelsesKilde, string bruker,
        CancellationToken ct = default)
    {
        var virksomhet = await db.Virksomheter.FirstOrDefaultAsync(v => v.Id == virksomhetId, ct);
        if (virksomhet is null) return new();
        var eidListe = eids.Distinct(StringComparer.Ordinal).ToList();
        var noder = await db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && eidListe.Contains(n.Eid) && n.Entitetsstatus == "gjeldende" && !n.Opphevet && n.Tekst != null)
            .Select(n => new { n.Eid, n.Tekst, n.Sorteringsrekkefolge }).ToListAsync(ct);
        var navneformer = await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId
            && b.Entitetsstatus == "gjeldende").ToListAsync(ct);
        var avvisteTermer = (await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId
            && b.Godkjenning == Strukturkanter.Avvist).Select(b => b.Term).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tall = new Treffidentitet();
        // Lengste form først, og et spenn som overlapper et allerede valgt spenn i samme node hoppes over («Tilsynsutvalget» inni
        // «Tilsynsutvalget for dommere»).
        var kandidatformer = navneformer.Select(n => n.Term).Append(virksomhet.Navn)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(s => s.Length).ToList();
        string? forsteKildeEid = null;
        foreach (var node in noder.OrderBy(n => n.Sorteringsrekkefolge))
        {
            var valgt = new List<(int Start, int Slutt)>();
            foreach (var kform in kandidatformer)
            {
                foreach (Match m in HeleOrd(kform, RegexOptions.IgnoreCase).Matches(node.Tekst!))
                {
                    var (start, slutt) = (m.Index, m.Index + m.Length);
                    if (valgt.Any(v => start < v.Slutt && v.Start < slutt)) continue;
                    valgt.Add((start, slutt));
                    var nf = navneformer.FirstOrDefault(n => n.Term == m.Value);
                    if (nf is null)
                    {
                        if (navneformer.Any(n => string.Equals(n.Term, m.Value, StringComparison.OrdinalIgnoreCase)))
                        {
                            tall += new Treffidentitet(UlikSkrivemaate: 1);
                            continue;
                        }
                        // Treffet gikk via registernavnet: registrer navneformen som forslag (AC3) — bare en egennavnform, og ikke en
                        // skrivemåte et menneske alt har avvist.
                        if (!ErEgennavnform(m.Value) || avvisteTermer.Contains(m.Value))
                        {
                            tall += new Treffidentitet(UlikSkrivemaate: 1);
                            continue;
                        }
                        var harGjeldende = navneformer.Any(n => n.Navneformgrunn == "gjeldende");
                        var tittel = await db.Rettskilder.Where(r => r.Id == rettskildeId).Select(r => r.Kortnavn ?? r.Tittel).FirstAsync(ct);
                        nf = await OpprettNavneformforslagAsync(virksomhetId, m.Value, harGjeldende ? null : "gjeldende",
                            new Forekomst(rettskildeId, tittel, null, node.Eid, node.Sorteringsrekkefolge, start, m.Value),
                            m.Value == virksomhet.Navn ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering,
                            m.Value == virksomhet.Navn
                                ? $"Aktøren ble funnet ved navn i {tittel}; navnet står ordrett slik."
                                : $"Aktøren ble funnet ved navn i {tittel}: registernavnet «{virksomhet.Navn}» står som «{m.Value}». Regel: samme ord "
                                  + "uten skille på store og små bokstaver, hele ord.",
                            oppdagelsesKilde, bruker, ct);
                        navneformer.Add(nf);
                        tall += new Treffidentitet(NavneformForslag: 1);
                    }
                    else if (nf.LovreferanseEid is null && nf.KildeUtenforKorpusTekst is null && nf.SkosUrl is null)
                    {
                        // Navneformen har ingen kilde: den får stedet der den står ordrett.
                        nf.LovkildeId = rettskildeId;
                        nf.LovreferanseEid = node.Eid;
                        nf.Kunnskapsstatus ??= Strukturkanter.Tekstfunn;
                        nf.SistEndretAv = bruker;
                        nf.SistEndretTidspunkt = DateTimeOffset.UtcNow;
                        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", nf.Id, virksomhetId: null, "endret", bruker));
                        await db.SaveChangesAsync(ct);
                        tall += new Treffidentitet(KilderSatt: 1);
                    }
                    forsteKildeEid ??= node.Eid;
                    tall += await TaggSpennAsync(rettskildeId, node.Eid, node.Tekst!, start, slutt, nf.Id, bruker, ct);
                }
            }
        }

        if (entitetstype is not null && virksomhet.Aktortype is null && Nodetyper.Aktortyper.Contains(entitetstype)
            && !await db.AktortypeUtsagn.AnyAsync(a => a.VirksomhetId == virksomhetId && a.Aktortype == entitetstype, ct))
        {
            var eid = forsteKildeEid ?? eidListe.FirstOrDefault();
            await RegistrerAktortypeAsync(virksomhetId, entitetstype, null, null, oppdagelsesKilde, bruker, ct,
                $"Konverteringen ({oppdagelsesKilde}) klassifiserte aktøren som {entitetstype.Replace('_', ' ')}. Ikke vurdert av et menneske.",
                eid is null ? null : rettskildeId, eid);
            tall += new Treffidentitet(AktortypeForslag: 1);
        }
        return tall;
    }

    /// <summary>Tagger nøyaktig spennet [start, slutt) mot navneformen, eller kobler en ubundet tagg på nøyaktig samme sted. Ingen tagg der
    /// en annen koblet tagg overlapper (dobbel markering), og ingen uten et ansvarlig departement som kan eie den.</summary>
    private async Task<Treffidentitet> TaggSpennAsync(
        Guid rettskildeId, string eid, string tekst, int start, int slutt, Guid navneformId, string bruker, CancellationToken ct)
    {
        var iNoden = await db.TekstTagger.Where(t => t.RettskildeId == rettskildeId && t.NodeEid == eid && t.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        if (iNoden.Any(t => t.StartOffset == start && t.EndOffset == slutt && t.RefId == navneformId)) return new(TaggerFantes: 1);
        var ubundet = iNoden.FirstOrDefault(t => t.StartOffset == start && t.EndOffset == slutt && t.RefId == null);
        if (iNoden.Any(t => t.RefId != null && start < t.EndOffset && t.StartOffset < slutt)) return new(TaggerKolliderer: 1);
        if (ubundet is not null)
        {
            ubundet.Kind = "virksomhet";
            ubundet.RefId = navneformId;
            db.Proveniens.Add(ProveniensHjelper.NyRad("tekst_tagg", ubundet.Id, ubundet.VirksomhetId, "endret", bruker));
            await db.SaveChangesAsync(ct);
            return new(TaggerOpprettet: 1);
        }
        var eier = await EierAsync(rettskildeId, ct);
        if (eier is null) return new(TaggerUtenEier: 1);
        var tagg = await tekstTagger.OpprettAsync(rettskildeId, eier.Value, bruker, eid, start, slutt,
            tekst[Math.Max(0, start - Kontekstlengde)..start], tekst[start..slutt], tekst[slutt..Math.Min(tekst.Length, slutt + Kontekstlengde)],
            "virksomhet", ct);
        if (tagg is null) return new(TaggerUtenEier: 1);
        await tekstTagger.KobleTilEntitetAsync(tagg.Id, navneformId, bruker, ct);
        return new(TaggerOpprettet: 1);
    }

    private readonly Dictionary<Guid, Guid?> _eiere = [];

    /// <summary>Taggen eies av rettskildens ansvarlige departement (første som finnes i katalogen) — samme regel som veiviseren.</summary>
    private async Task<Guid?> EierAsync(Guid rettskildeId, CancellationToken ct)
    {
        if (_eiere.TryGetValue(rettskildeId, out var e)) return e;
        Guid? eier = null;
        var dep = await db.Rettskilder.Where(r => r.Id == rettskildeId).Select(r => r.AnsvarligDepartement).FirstOrDefaultAsync(ct);
        foreach (var d in dep ?? [])
        {
            eier = await virksomhetOppslag.FinnVirksomhetIdForNavnAsync(d);
            if (eier is not null) break;
        }
        _eiere[rettskildeId] = eier;
        return eier;
    }

    /// <summary>
    /// Etterfylling for treff som alt finnes (Johann 2026-10-16): et «treff» er en strukturkant (gjeldende, ikke avvist) med hjemmel-eId der
    /// en aktør står i fra- eller til-enden — det er der lasteren og områdetolkeren fant aktøren ved navn. For hvert (aktør, rettskilde,
    /// eId) kjøres <see cref="SikreTreffAsync"/>. Med <paramref name="skriv"/> = false rulles alt tilbake (tørrkjøring med de samme tallene).
    /// </summary>
    public async Task<EtterfyllingRapport> EtterfyllKildeOgTaggerAsync(bool skriv, string bruker, CancellationToken ct = default)
    {
        var kanter = await db.Strukturkanter
            .Where(k => k.HjemmelRettskildeId != null && k.HjemmelEid != null && (k.FraVirksomhetId != null || k.TilVirksomhetId != null))
            .Select(k => new { k.HjemmelRettskildeId, k.HjemmelEid, k.FraVirksomhetId, k.TilVirksomhetId }).ToListAsync(ct);
        var treff = kanter.SelectMany(k => new[] { k.FraVirksomhetId, k.TilVirksomhetId }.OfType<Guid>()
                .Select(v => (Virksomhet: v, Rettskilde: k.HjemmelRettskildeId!.Value, Eid: k.HjemmelEid!)))
            .Distinct().ToList();

        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var tall = new Treffidentitet();
        var rader = new List<EtterfyllingRad>();
        var navn = await new VirksomhetVisningsnavnTjeneste(db).AlleAsync(ct);
        foreach (var g in treff.GroupBy(t => (t.Virksomhet, t.Rettskilde)))
        {
            var t = await SikreTreffAsync(g.Key.Virksomhet, g.Key.Rettskilde, g.Select(x => x.Eid), null, KildeEtterfylling, bruker, ct);
            tall += t;
            if (t == new Treffidentitet(TaggerFantes: t.TaggerFantes)) continue; // ingenting nytt og ingenting hoppet over
            var aktor = navn.GetValueOrDefault(g.Key.Virksomhet)
                ?? await db.Virksomheter.Where(v => v.Id == g.Key.Virksomhet).Select(v => v.Navn).FirstAsync(ct);
            var tittel = await db.Rettskilder.Where(r => r.Id == g.Key.Rettskilde).Select(r => r.Kortnavn ?? r.Tittel).FirstAsync(ct);
            rader.Add(new EtterfyllingRad(g.Key.Virksomhet, aktor, g.Key.Rettskilde, tittel, g.Count(), t));
        }
        if (tx is not null)
        {
            if (skriv) await tx.CommitAsync(ct);
            else
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
        }
        return new EtterfyllingRapport(skriv, treff.Count, treff.Select(t => t.Virksomhet).Distinct().Count(), tall,
            rader.OrderBy(r => r.Aktor, StringComparer.Ordinal).ThenBy(r => r.RettskildeTittel, StringComparer.Ordinal).ToList());
    }
}
