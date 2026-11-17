using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #470] Den generiske navneformen «fylkeskommunen» i <see cref="FylkeskommunenOppsettRapport"/>. <see cref="Utfall"/>:
/// <c>opprettet</c> | <c>finnes</c> (en gjeldende generisk navneform har termen) | <c>avvist</c> (et menneske har avvist den; den foreslås ikke på
/// nytt) | <c>konflikt</c> (termen er alt navneform for en aktør; ingenting skrives).</summary>
public sealed record FylkeskommunenNavneform(string Utfall, Guid? NavneformId, string? Godkjenning, string? Merknad);

/// <summary>[Ny, issue #470] Ett bindingsforslag der samme paragraf navngir nøyaktig én fylkeskommune og bruker «fylkeskommunen».
/// <see cref="Utfall"/>: <c>ny</c> | <c>finnes</c> | <c>avvist</c> (et menneske har avvist samme forslag; det foreslås ikke på nytt).</summary>
public sealed record FylkeskommunenBindingsforslag(Guid RettskildeId, string Tittel, string Paragraf, string HjemmelEid, string Navn,
    Guid VirksomhetId, string Fylkeskommune, string Utfall, Guid? BindingId);

/// <summary>[Ny, issue #470] En rettskilde der regelen gir hull fordi kilden navngir en bestemt fylkeskommune. <see cref="Navn"/> er navnene
/// slik de står; <see cref="Forekomster"/> er nodene med «fylkeskommunen». Det er her et menneske binder ordet.</summary>
public sealed record FylkeskommunenBindManuelt(Guid RettskildeId, string Tittel, IReadOnlyList<string> Navn, int Noder, int Bindingsforslag);

/// <summary>[Ny, issue #470] Svaret på <c>POST /api/navneformer/generiske/fylkeskommunen[?skriv=true]</c>. Tallene er de skrivingen gir, også
/// i tørrkjøringen (samme arbeid i en transaksjon som rulles tilbake).</summary>
public sealed record FylkeskommunenOppsettRapport(
    bool Skrevet, Guid KlasseId, string? KlasseGodkjenning, FylkeskommunenNavneform Navneform,
    IReadOnlyList<FylkeskommunenBindingsforslag> Bindingsforslag, IReadOnlyList<FylkeskommunenBindManuelt> BindManuelt,
    IReadOnlyList<string> Uparet);

/// <summary>
/// [Ny, issue #470, Johanns ja 2026-11-17] Skriver den generiske navneformen «fylkeskommunen» (grunn <c>generisk</c>, ingen aktør) som forslag,
/// og foreslår bindinger der teksten peker ut én bestemt fylkeskommune. Standardregelen selv er kode (<see cref="GeneriskStandardregelTjeneste"/>,
/// regelkoden <c>klasse_fylkeskommune</c>) og skrives ikke. Samme mønster som <see cref="KlasseoppsettTjeneste"/> (#442): tørrkjøring er
/// standard, alt nytt er forslag, operasjonen er idempotent, og et avvist forslag foreslås ikke på nytt.
/// <list type="bullet">
/// <item><b>Forutsetninger</b> (ellers skrives ingenting): klassen fylkeskommune finnes (skrevet av #442), og kommuneloven står i korpuset med
/// ordlyden regelens hjemler siterer (<see cref="GeneriskStandardregel.Hjemler"/>).</item>
/// <item><b>Navneformen</b>: <c>manuelt_foreslatt</c> (Johanns avgjørelse i #470), oppdagelseskilde <c>manuell</c>. Som klassen fra #442 virker
/// den i rutingen og oppslaget med en gang; svaret sier at klassen er et forslag.</item>
/// <item><b>Bindingsforslagene</b>: der én paragraf navngir nøyaktig én fylkeskommune (entall, ikke «X og Y fylkeskommuner») og også bruker
/// «fylkeskommunen», og navnet etter navneregelen i kommuneloven § 3-1 fjerde ledd («X fylkeskommune») treffer nøyaktig én levende aktør med
/// forvaltningsnivå fylkeskommune. Spennet er paragrafen, hjemmelen er leddet som navngir den, kunnskapsstatusen er <c>fortolkning</c> (at
/// ordet viser tilbake til den navngitte, er en tolkning), og status <c>maskinelt_foreslatt</c>. Et forslag brukes ikke i oppslaget før det er
/// vurdert. Hele kilden bindes ikke: i opplæringsforskrifta navngir § 4-10 Finnmark fylkeskommune, mens «fylkeskommunen» i de 170 andre
/// nodene er klassen (målt 2026-11-17). Det avgjør et menneske, og kildene står i <see cref="FylkeskommunenOppsettRapport.BindManuelt"/>.</item>
/// </list>
/// </summary>
public sealed class FylkeskommunenOppsettTjeneste(RegelIdeDbContext db)
{
    public const string Term = "fylkeskommunen";
    public const string BindingOppdagelseskilde = "monster:fylkeskommunen-navngitt";

    public const string NavneformBegrunnelse =
        "Generisk navneform for «fylkeskommunen» med standardregelen «Klassen fylkeskommune» (issue #470, Johanns ja 2026-11-17). Regelen bygger på "
        + "kommuneloven § 2-1 første ledd og § 3-1 første ledd andre punktum og fjerde ledd. Hvilken fylkeskommune det er i en sak, avgjøres av saken; "
        + "navngir kilden én, bindes ordet.";

    private static readonly Regex Ordet = new(@"(?<![\p{L}])fylkeskommunen(?![\p{L}])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<FylkeskommunenOppsettRapport> KjorAsync(bool skriv, string bruker, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var rapport = await GjorAsync(bruker, ct) with { Skrevet = skriv };
            if (skriv && tx is not null) await tx.CommitAsync(ct);
            else if (tx is not null) await tx.RollbackAsync(ct);
            if (!skriv) db.ChangeTracker.Clear();
            return rapport;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<FylkeskommunenOppsettRapport> GjorAsync(string bruker, CancellationToken ct)
    {
        await KontrollerLovteksten(ct);
        var klasse = await db.Begreper.Where(b => b.Medlemskapsregel == Klasseregler.OmraderegisterFylkeskommune && b.Entitetsstatus == "gjeldende")
            .Select(b => new { b.Id, b.Godkjenning }).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Klassen fylkeskommune finnes ikke. Kjør POST /api/klasser/fylkeskommune-departement?skriv=true "
                + "først (#442). Ingenting skrives.");

        var navneform = await NavneformAsync(bruker, ct);
        if (navneform.NavneformId is not { } nfId || navneform.Utfall is "konflikt" or "avvist")
        {
            return new(false, klasse.Id, klasse.Godkjenning, navneform, [], [], []);
        }
        var (forslag, manuelt, uparet) = await BindingerAsync(nfId, bruker, ct);
        return new(false, klasse.Id, klasse.Godkjenning, navneform, forslag, manuelt, uparet);
    }

    /// <summary>Kommuneloven skal stå i korpuset med ordlyden regelens hjemler siterer, én gang. Ellers skrives ingenting (CLAUDE.md §8).</summary>
    private async Task KontrollerLovteksten(CancellationToken ct)
    {
        foreach (var h in GeneriskStandardregelTjeneste.For(Term)!.Hjemler)
        {
            var n = await db.RettskildeNoder.Where(x => x.Eid == h.Eid && x.Entitetsstatus == "gjeldende")
                .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende" && r.Status == "Gjeldende"), x => x.RettskildeId, r => r.Id, (x, r) => x.Tekst)
                .ToListAsync(ct);
            if (n.Count != 1 || n[0] is null || !n[0]!.Contains(h.Sitat, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Fant ikke nøyaktig én gjeldende node {h.Eid} med «{h.Sitat}» ({n.Count} treff). Ingenting skrives.");
            }
        }
    }

    private async Task<FylkeskommunenNavneform> NavneformAsync(string bruker, CancellationToken ct)
    {
        var finnes = await new GeneriskNavneformTjeneste(db).FinnAsync(Term, ct);
        if (finnes is not null)
        {
            return new(finnes.Godkjenning == Strukturkanter.Avvist ? "avvist" : "finnes", finnes.Id, finnes.Godkjenning,
                finnes.Godkjenning == Strukturkanter.Avvist ? finnes.AvvisningBegrunnelse : null);
        }
        // Samme kontroll som GeneriskNavneformTjeneste.OpprettEllerFinnAsync: er termen navneform for en aktør, ville oppslaget blitt flertydig.
        // [#423] En avvist navneform teller ikke («Fylkeskommune» på Agder fylkeskommune ble avvist 2026-10-31).
        var bundne = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                        && b.Godkjenning != Strukturkanter.Avvist && b.Term.ToLower() == Term)
            .Join(db.Virksomheter, b => b.VirksomhetReferanseId, v => v.Id, (b, v) => v.Navn).Take(3).ToListAsync(ct);
        if (bundne.Count > 0)
        {
            return new("konflikt", null, null, $"«{Term}» er alt en navneform for {string.Join(", ", bundne)}. Et menneske retter den først; ingenting skrives.");
        }
        var nf = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = null,
            Begrepskategori = "virksomhet",
            VirksomhetReferanseId = null,
            Navneformgrunn = VirksomhetsbegrepTjeneste.Generisk,
            Term = Term,
            Begrunnelse = NavneformBegrunnelse,
            Godkjenning = Strukturkanter.ManueltForeslatt,
            OppdagelsesKilde = "manuell",
            Status = "publisert",
            OpprettetAv = bruker,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(nf);
        var p = ProveniensHjelper.NyRad("begrep", nf.Id, virksomhetId: null, "foreslatt", bruker);
        p.KildeReferanserJson = JsonSerializer.Serialize(new
        {
            issue = 470, beslutning = "Johann 2026-11-17", standardregel = GeneriskStandardregelTjeneste.KlasseFylkeskommune,
            hjemler = GeneriskStandardregelTjeneste.For(Term)!.Hjemler.Select(h => h.Eid),
        });
        db.Proveniens.Add(p);
        await db.SaveChangesAsync(ct);
        return new("opprettet", nf.Id, nf.Godkjenning, null);
    }

    private async Task<(List<FylkeskommunenBindingsforslag>, List<FylkeskommunenBindManuelt>, List<string>)> BindingerAsync(Guid navneformId,
        string bruker, CancellationToken ct)
    {
        // Samme utvalg som målingen (GeneriskStandardregelTjeneste.DekningAsync): gjeldende primærrettskilder der ordet står som eget ord.
        var ider = await db.Database.SqlQueryRaw<Guid>(
            """
            SELECT DISTINCT r."Id" AS "Value" FROM rettskilder r JOIN rettskilde_noder n ON n.rettskilde_id = r."Id"
            WHERE r.entitetsstatus = 'gjeldende' AND r.importrolle = 'primaer' AND n.entitetsstatus = 'gjeldende' AND n.tekst ~* {0}
            """, @"\mfylkeskommunen\M").ToListAsync(ct);
        var regler = new GeneriskStandardregelTjeneste(db);
        var generiske = new GeneriskNavneformTjeneste(db);
        var aktorer = await FylkeskommuneNavnAsync(ct);
        var forslag = new List<FylkeskommunenBindingsforslag>();
        var manuelt = new List<FylkeskommunenBindManuelt>();
        var uparet = new List<string>();

        foreach (var id in ider)
        {
            var svar = await regler.LosAsync(Term, id, null, ct);
            if (svar is null || !svar.Forklaring.StartsWith("Kilden navngir en bestemt fylkeskommune", StringComparison.Ordinal)) continue;
            var tittel = await db.Rettskilder.Where(r => r.Id == id).Select(r => r.Kortnavn ?? r.Tittel).FirstAsync(ct);
            var navngitte = await regler.NavngitteFylkeskommunerAsync(id, ct);
            var noder = await db.RettskildeNoder.Where(n => n.RettskildeId == id && n.Entitetsstatus == "gjeldende")
                .Select(n => new { n.Eid, n.Tekst }).ToListAsync(ct);
            var medOrdet = noder.Where(n => n.Tekst is not null && Ordet.IsMatch(n.Tekst)).Select(n => n.Eid).ToList();
            var eider = noder.Select(n => n.Eid).ToHashSet(StringComparer.Ordinal);
            var antallForslag = 0;

            foreach (var par in navngitte.GroupBy(f => Paragraf(f.Eid)).Where(g => g.Key is not null))
            {
                var parEid = par.Key!;
                if (!eider.Contains(parEid) || !medOrdet.Any(e => Paragraf(e) == parEid)) continue;
                var ulike = par.Select(f => f.Ledd).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (ulike.Count != 1 || par.Any(f => f.Flertall)) continue;
                var ledd = ulike[0];
                var treff = aktorer.Where(a => string.Equals(a.Navn.Trim(), $"{ledd} fylkeskommune", StringComparison.OrdinalIgnoreCase))
                    .Select(a => a.Id).Distinct().ToList();
                if (treff.Count != 1)
                {
                    uparet.Add($"{tittel} {parEid}: «{ledd} fylkeskommune» gir {treff.Count} treff blant de levende aktørene med forvaltningsnivå "
                        + "fylkeskommune. Ingen binding foreslått.");
                    continue;
                }
                var f0 = par.First();
                forslag.Add(await BindingsforslagAsync(navneformId, id, tittel, parEid, f0.Eid, f0.Navn, treff[0], generiske, bruker, ct));
                antallForslag++;
            }
            manuelt.Add(new(id, tittel, navngitte.Select(f => f.Navn).Distinct().ToList(), medOrdet.Count, antallForslag));
        }
        return (forslag, manuelt.OrderBy(m => m.Tittel, StringComparer.Ordinal).ToList(), uparet);
    }

    /// <summary>Paragrafen en node hører til (eId-en til og med «§…»-leddet), eller null når eId-en ikke har en paragraf.</summary>
    internal static string? Paragraf(string eid)
    {
        var m = Regex.Match(eid, @"^(.*?/§[^/]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Navnene til de levende aktørene med forvaltningsnivå fylkeskommune: registernavn og navneformer som ikke er avvist (som
    /// navneregelen i <see cref="KlasseoppsettTjeneste"/>).</summary>
    private async Task<List<(Guid Id, string Navn)>> FylkeskommuneNavnAsync(CancellationToken ct)
    {
        var kandidater = await db.Virksomheter
            .Where(v => v.Forvaltningsniva == "fylkeskommune" && v.Livsstatus == null && v.ErstattetAvId == null)
            .Select(v => new { v.Id, v.Navn }).ToListAsync(ct);
        var ider = kandidater.Select(v => v.Id).ToList();
        var navneformer = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.VirksomhetReferanseId != null
                        && b.Godkjenning != Strukturkanter.Avvist && ider.Contains(b.VirksomhetReferanseId.Value))
            .Select(b => new { Id = b.VirksomhetReferanseId!.Value, b.Term }).ToListAsync(ct);
        return kandidater.Select(v => (v.Id, v.Navn)).Concat(navneformer.Select(n => (n.Id, Navn: n.Term))).ToList();
    }

    private async Task<FylkeskommunenBindingsforslag> BindingsforslagAsync(Guid navneformId, Guid rettskildeId, string tittel, string parEid,
        string hjemmelEid, string navn, Guid vid, GeneriskNavneformTjeneste generiske, string bruker, CancellationToken ct)
    {
        var visning = await new VirksomhetVisningsnavnTjeneste(db).ForAsync(vid, ct)
            ?? await db.Virksomheter.Where(v => v.Id == vid).Select(v => v.Navn).FirstAsync(ct);
        List<ParagrafspennPar> spenn = [new(parEid, null)];
        var spennJson = JsonSerializer.Serialize(spenn, JsonSerialiseringHjelper.Innstillinger);
        // Avvist eller ikke: en binding av ordet til samme spenn i verket er samme forslag. Et avvist forslag foreslås ikke på nytt.
        var versjoner = await generiske.VersjonerAvVerketAsync(rettskildeId, ct);
        var tidligere = await db.NavneformBindinger
            .Where(b => b.NavneformId == navneformId && versjoner.Contains(b.RettskildeId) && b.ParagrafspennJson == spennJson
                        && b.Entitetsstatus == "gjeldende")
            .OrderBy(b => b.Status == Strukturkanter.Avvist ? 1 : 0).FirstOrDefaultAsync(ct);
        if (tidligere is not null)
        {
            return new(rettskildeId, tittel, parEid, hjemmelEid, navn, vid, visning, tidligere.Status == Strukturkanter.Avvist ? "avvist" : "finnes",
                tidligere.Id);
        }
        var sitat = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId && n.Eid == hjemmelEid && n.Entitetsstatus == "gjeldende")
            .Select(n => n.Tekst).FirstOrDefaultAsync(ct);
        var b = await generiske.OpprettBindingAsync(new NyNavneformBinding(navneformId, rettskildeId, Kantnode.Virksomhet(vid), spenn,
            HjemmelRettskildeId: rettskildeId, HjemmelEid: hjemmelEid,
            Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd", "kontekst_system"],
            Begrunnelse: $"Paragrafen navngir én fylkeskommune ({navn}{(sitat is null ? "" : $": «{Avkort(sitat)}»")}), og «fylkeskommunen» i "
                         + "samme paragraf viser etter sammenhengen tilbake til den. Standardregelen gir hull i kilden fordi den navngir en bestemt "
                         + "fylkeskommune (#470).",
            Status: Strukturkanter.MaskineltForeslatt, OppdagelsesKilde: BindingOppdagelseskilde,
            Kommentar: "Forslag fra navneregelen (kommuneloven § 3-1 fjerde ledd: «Fylkeskommunen skal ha samme navn som fylket»). Et menneske "
                       + "avgjør om bindingen skal gjelde paragrafen, mer av kilden eller ingenting."), bruker, ct);
        return new(rettskildeId, tittel, parEid, hjemmelEid, navn, vid, visning, "ny", b.Id);
    }

    private static string Avkort(string s) => s.Length <= 200 ? s : s[..200] + " …";
}
