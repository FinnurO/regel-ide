using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Én navneform omgjøringen oppretter. <c>ArverTaggene</c>: taggene og kantene som pekte på det gamle begrepet, flyttes hit
/// (standard: den gjeldende).</summary>
public sealed record OmgjortNavneform(string Term, string Grunn, Utsagnsgrunnlag Grunnlag, bool ArverTaggene = false);

/// <summary>Hva et rolle-, klasse- eller gruppebegrep skal bli: en ny aktør (navnet) eller en som finnes, navneformene og aktørtypen.</summary>
public sealed record OmgjoringTilAktor(
    string Aktornavn, Guid? EksisterendeVirksomhetId, IReadOnlyList<OmgjortNavneform> Navneformer,
    string? Aktortype = null, Utsagnsgrunnlag? AktortypeGrunnlag = null);

public sealed record OmgjoringResultat(
    Guid BegrepId, Virksomhet Aktor, bool AktorVarNy, IReadOnlyList<BegrepEntitet> Navneformer, int TaggerFlyttet, int TaggerUtvidet,
    int KanterFlyttet, Treffidentitet Navnetreff, AktortypeUtsagnEntitet? Aktortype);

public sealed record AngreOmgjoringResultat(Guid BegrepId, int TaggerTilbake, int KanterTilbake, int NavneformerAvvist, int AktortyperAvvist);

/// <summary>
/// [Ny, issue #386] Omgjøring av et organ som er registrert som rolle-, klasse- eller gruppebegrep (en rest fra den eldre begrepstaggingen)
/// til en aktør med navneformer. Skjer bare etter en juristrunde (CLAUDE.md §23); tjenesten kalles av et menneske, aldri automatisk.
/// <list type="bullet">
/// <item><b>Ingenting slettes.</b> Begrepet settes som <c>erstattet</c>, og navneformen som arver taggene har <c>erstatter_id</c> = begrepet.</item>
/// <item><b>Forslag der strukturen har det:</b> navneformene og aktørtypen lagres med grunnlaget og statusen som oppgis (normalt
/// <c>manuelt_foreslatt</c>). Aktøren selv har ingen godkjenning og opprettes direkte.</item>
/// <item><b>Taggene følger med</b> til navneformen som arver dem. Står navneformen i teksten fra taggens start, utvides spennet til hele
/// navnet («Tilsynsutvalget» → «Tilsynsutvalget for dommere», juristrunden K1). <b>Kantene flyttes</b> fra begrepet til aktøren. Hver
/// flytting får en proveniensrad (<c>flyttet</c>) med de gamle verdiene, så <see cref="AngreAsync"/> kan sette alt tilbake.</item>
/// <item><b>Navnetreff:</b> navneformene tagges der teksten er nøyaktig lik (samme regel som #385, <see cref="AktoridentitetTjeneste.SikreTreffAsync"/>)
/// — i rettskilden der en navneform er hjemlet, og der det gjeldende navnet står i andre rettskilder. En kortform tagges ikke i andre
/// rettskilder alene: «Tilsynsutvalget» i politiarrestforskriften er et annet organ.</item>
/// </list>
/// </summary>
public sealed class OmgjoringTjeneste(RegelIdeDbContext db, AktoridentitetTjeneste identitet)
{
    public const string Oppdagelseskilde = "omgjoring:nodebegrep-til-aktor";
    public const string HandlingFlyttet = "flyttet";
    public static readonly string[] Nodebegrepkategorier = ["rolle", "klasse", "gruppe"];
    private const int Kontekstlengde = 30;

    private sealed record TaggFoer(Guid OmgjortFraBegrep, string Kind, Guid? RefId, int Start, int Slutt, string Prefix, string Exact, string Suffix);
    private sealed record KantFoer(Guid OmgjortFraBegrep, Guid? FraBegrepId, Guid? FraVirksomhetId, Guid? TilBegrepId, Guid? TilVirksomhetId);

    public async Task<OmgjoringResultat> OmgjorTilAktorAsync(Guid begrepId, OmgjoringTilAktor spec, string bruker, CancellationToken ct = default)
    {
        var begrep = await db.Begreper.FirstOrDefaultAsync(b => b.Id == begrepId, ct)
            ?? throw new ArgumentException($"Fant ingen begrep med id '{begrepId}'.");
        if (begrep.Entitetsstatus != "gjeldende" || !Nodebegrepkategorier.Contains(begrep.Begrepskategori))
        {
            throw new ArgumentException($"«{begrep.Term}» er ikke et gjeldende rolle-, klasse- eller gruppebegrep (kategori '{begrep.Begrepskategori}', "
                + $"{begrep.Entitetsstatus}).");
        }
        if (spec.Navneformer.Count == 0 || spec.Navneformer.Count(n => n.Grunn == "gjeldende") != 1)
        {
            throw new ArgumentException("Omgjøringen krever nøyaktig én gjeldende navneform (hjemlet i kilden som etablerer eller navngir aktøren, #395).");
        }
        if (spec.Navneformer.Count(n => n.ArverTaggene) > 1) throw new ArgumentException("Bare én navneform kan arve taggene.");
        foreach (var n in spec.Navneformer)
        {
            if (!VirksomhetsbegrepTjeneste.ErGyldigNavneformgrunn(n.Grunn) || n.Grunn == "generisk")
            {
                throw new ArgumentException($"Ugyldig navneformgrunn '{n.Grunn}' for «{n.Term}».");
            }
            await identitet.ValiderAsync(n.Grunnlag, ct);
        }
        if (spec.Navneformer.Select(n => n.Term.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != spec.Navneformer.Count)
        {
            throw new ArgumentException("To navneformer med samme term.");
        }
        if (spec.Aktortype is not null && spec.AktortypeGrunnlag is null)
        {
            throw new ArgumentException("En aktørtype må ha et grunnlag (CLAUDE.md §8: ingen type uten kilde).");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Virksomhet aktor;
        var aktorVarNy = spec.EksisterendeVirksomhetId is null;
        if (spec.EksisterendeVirksomhetId is { } vid)
        {
            aktor = await db.Virksomheter.FirstOrDefaultAsync(v => v.Id == vid && v.ErstattetAvId == null, ct)
                ?? throw new ArgumentException($"Fant ingen aktør med id '{vid}' (eller den er erstattet).");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(spec.Aktornavn)) throw new ArgumentException("Aktørens navn mangler.");
            aktor = new Virksomhet { Id = Guid.NewGuid(), Navn = spec.Aktornavn.Trim(), OpprettetTidspunkt = DateTimeOffset.UtcNow, Aktiv = true };
            db.Virksomheter.Add(aktor);
            var pv = ProveniensHjelper.NyRad("virksomhet", aktor.Id, virksomhetId: null, "opprettet", bruker);
            pv.KildeReferanserJson = JsonSerializer.Serialize(new { omgjortFraBegrep = begrepId });
            db.Proveniens.Add(pv);
        }
        var finnes = await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == aktor.Id
                && b.Entitetsstatus == "gjeldende" && b.Godkjenning != Strukturkanter.Avvist)
            .Select(b => new { b.Term, b.Navneformgrunn }).ToListAsync(ct);
        if (spec.Navneformer.FirstOrDefault(n => finnes.Any(f => string.Equals(f.Term, n.Term.Trim(), StringComparison.OrdinalIgnoreCase))) is { } dublett)
        {
            throw new ArgumentException($"Aktøren har alt navneformen «{dublett.Term}».");
        }
        if (finnes.Any(f => f.Navneformgrunn == "gjeldende"))
        {
            throw new ArgumentException("Aktøren har alt en gjeldende navneform. Endre grunnen på den først, så omgjør.");
        }

        var arving = spec.Navneformer.FirstOrDefault(n => n.ArverTaggene) ?? spec.Navneformer.Single(n => n.Grunn == "gjeldende");
        var navneformer = new List<BegrepEntitet>();
        BegrepEntitet? arvingRad = null;
        foreach (var n in spec.Navneformer)
        {
            var b = new BegrepEntitet
            {
                Id = Guid.NewGuid(), VirksomhetId = null, Begrepskategori = "virksomhet", VirksomhetReferanseId = aktor.Id, Term = n.Term.Trim(),
                Navneformgrunn = n.Grunn, Status = "publisert", OpprettetAv = bruker, OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            n.Grunnlag.SkrivTil(b, Oppdagelseskilde);
            if (ReferenceEquals(n, arving))
            {
                b.ErstatterId = begrepId;
                arvingRad = b;
            }
            db.Begreper.Add(b);
            db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", b.Id, virksomhetId: null, "opprettet", bruker));
            navneformer.Add(b);
        }

        // Taggene følger med.
        var tagger = await db.TekstTagger.Where(t => t.RefId == begrepId && t.Entitetsstatus == "gjeldende").ToListAsync(ct);
        var (flyttet, utvidet) = (0, 0);
        foreach (var t in tagger)
        {
            var foer = new TaggFoer(begrepId, t.Kind, t.RefId, t.StartOffset, t.EndOffset, t.QuotePrefix, t.QuoteExact, t.QuoteSuffix);
            var tekst = await db.RettskildeNoder.Where(n => n.RettskildeId == t.RettskildeId && n.Eid == t.NodeEid && n.Entitetsstatus == "gjeldende")
                .Select(n => n.Tekst).FirstOrDefaultAsync(ct);
            var term = arvingRad!.Term;
            if (tekst is not null && t.EndOffset - t.StartOffset != term.Length && t.StartOffset + term.Length <= tekst.Length
                && string.CompareOrdinal(tekst, t.StartOffset, term, 0, term.Length) == 0
                && !Regex.IsMatch(tekst[(t.StartOffset + term.Length)..Math.Min(tekst.Length, t.StartOffset + term.Length + 1)], @"^[\p{L}\p{N}]"))
            {
                t.EndOffset = t.StartOffset + term.Length;
                t.QuoteExact = term;
                t.QuoteSuffix = tekst[t.EndOffset..Math.Min(tekst.Length, t.EndOffset + Kontekstlengde)];
                utvidet++;
            }
            t.Kind = "virksomhet";
            t.RefId = arvingRad.Id;
            var p = ProveniensHjelper.NyRad("tekst_tagg", t.Id, t.VirksomhetId, HandlingFlyttet, bruker);
            p.KildeReferanserJson = JsonSerializer.Serialize(foer);
            db.Proveniens.Add(p);
            flyttet++;
        }

        // Kantene flyttes fra begrepet til aktøren.
        var kanter = await db.Strukturkanter.Where(k => k.FraBegrepId == begrepId || k.TilBegrepId == begrepId).ToListAsync(ct);
        foreach (var k in kanter)
        {
            var foer = new KantFoer(begrepId, k.FraBegrepId, k.FraVirksomhetId, k.TilBegrepId, k.TilVirksomhetId);
            if (k.FraBegrepId == begrepId) (k.FraBegrepId, k.FraVirksomhetId) = (null, aktor.Id);
            if (k.TilBegrepId == begrepId) (k.TilBegrepId, k.TilVirksomhetId) = (null, aktor.Id);
            k.SistEndretAv = bruker;
            k.SistEndretTidspunkt = DateTimeOffset.UtcNow;
            var p = ProveniensHjelper.NyRad(StrukturkantTjeneste.ProveniensType, k.Id, virksomhetId: null, HandlingFlyttet, bruker);
            p.KildeReferanserJson = JsonSerializer.Serialize(foer);
            db.Proveniens.Add(p);
        }

        begrep.Entitetsstatus = "erstattet";
        begrep.SistEndretAv = bruker;
        begrep.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        var pb = ProveniensHjelper.NyRad("begrep", begrep.Id, virksomhetId: null, "erstattet", bruker);
        pb.KildeReferanserJson = JsonSerializer.Serialize(new { aktor = aktor.Id, navneform = arvingRad!.Id });
        db.Proveniens.Add(pb);
        await db.SaveChangesAsync(ct);

        AktortypeUtsagnEntitet? typeUtsagn = null;
        if (spec.Aktortype is not null)
        {
            typeUtsagn = await identitet.RegistrerAktortypeAsync(aktor.Id, spec.Aktortype, null, spec.AktortypeGrunnlag, Oppdagelseskilde, bruker, ct);
        }
        await tx.CommitAsync(ct);

        var treff = await TaggNavnetreffAsync(aktor, navneformer, bruker, ct);
        return new OmgjoringResultat(begrepId, aktor, aktorVarNy, navneformer, flyttet, utvidet, kanter.Count, treff, typeUtsagn);
    }

    /// <summary>Navnetreffet etter omgjøringen: alle navneformer i rettskildene der de er hjemlet, og den gjeldende i andre rettskilder.</summary>
    private async Task<Treffidentitet> TaggNavnetreffAsync(Virksomhet aktor, List<BegrepEntitet> navneformer, string bruker, CancellationToken ct)
    {
        var gjeldende = navneformer.Single(n => n.Navneformgrunn == "gjeldende").Term;
        var hjemmelkilder = navneformer.Where(n => n.LovkildeId is not null).Select(n => n.LovkildeId!.Value).Distinct().ToList();
        var termer = navneformer.Select(n => n.Term).ToList();
        var noder = await db.RettskildeNoder
            .Where(n => n.Entitetsstatus == "gjeldende" && !n.Opphevet && n.Tekst != null
                && (hjemmelkilder.Contains(n.RettskildeId) || n.Tekst.Contains(gjeldende)))
            .Join(db.Rettskilder.Where(r => r.Entitetsstatus == "gjeldende"), n => n.RettskildeId, r => r.Id, (n, r) => new { n.RettskildeId, n.Eid, n.Tekst })
            .ToListAsync(ct);
        var helt = termer.ToDictionary(t => t, t => new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(t)}(?![\p{{L}}\p{{N}}])", RegexOptions.CultureInvariant));
        var tall = new Treffidentitet();
        foreach (var g in noder.Where(n => hjemmelkilder.Contains(n.RettskildeId) ? termer.Any(t => helt[t].IsMatch(n.Tekst!)) : helt[gjeldende].IsMatch(n.Tekst!))
                     .GroupBy(n => n.RettskildeId))
        {
            tall += await identitet.SikreTreffAsync(aktor.Id, g.Key, g.Select(n => n.Eid), null, Oppdagelseskilde, bruker, ct);
        }
        return tall;
    }

    /// <summary>
    /// Setter omgjøringen tilbake: begrepet blir gjeldende igjen, taggene og kantene får verdiene fra proveniensradene, og navneformene og
    /// aktørtypen omgjøringen foreslo, avvises (ikke slettet). Aktøren står igjen (den kan ha fått andre opplysninger siden); er den ny og
    /// uten andre navneformer, viser aktørsiden «Navneform mangler». En navneform eller aktørtype som alt er godkjent, avvises ikke: da
    /// stopper angringen med en melding, fordi et menneske har tatt stilling.
    /// </summary>
    public async Task<AngreOmgjoringResultat> AngreAsync(Guid begrepId, string? begrunnelse, string bruker, CancellationToken ct = default)
    {
        var begrep = await db.Begreper.FirstOrDefaultAsync(b => b.Id == begrepId, ct)
            ?? throw new ArgumentException($"Fant ingen begrep med id '{begrepId}'.");
        var arving = await db.Begreper.FirstOrDefaultAsync(b => b.ErstatterId == begrepId && b.OppdagelsesKilde == Oppdagelseskilde
            && b.Entitetsstatus == "gjeldende", ct);
        if (begrep.Entitetsstatus != "erstattet" || arving?.VirksomhetReferanseId is not { } aktorId)
        {
            throw new ArgumentException($"«{begrep.Term}» er ikke omgjort til en aktør (eller omgjøringen er alt angret).");
        }
        var foreslatte = await db.Begreper.Where(b => b.VirksomhetReferanseId == aktorId && b.OppdagelsesKilde == Oppdagelseskilde
            && b.Entitetsstatus == "gjeldende").ToListAsync(ct);
        var typer = await db.AktortypeUtsagn.Where(a => a.VirksomhetId == aktorId && a.OppdagelsesKilde == Oppdagelseskilde
            && a.Status != Strukturkanter.Avvist).ToListAsync(ct);
        if (foreslatte.FirstOrDefault(b => b.Godkjenning == Strukturkanter.Godkjent) is { } g)
        {
            throw new ArgumentException($"Navneformen «{g.Term}» er godkjent. Omgjøringen angres ikke over hodet på den som godkjente.");
        }
        if (typer.Any(a => a.Status == Strukturkanter.Godkjent)) throw new ArgumentException("Aktørtypen fra omgjøringen er godkjent.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var flyttinger = await db.Proveniens.Where(p => p.Handling == HandlingFlyttet && p.KildeReferanserJson != null).ToListAsync(ct);
        var (tagger, kanter) = (0, 0);
        foreach (var p in flyttinger.Where(p => p.EntitetType == "tekst_tagg"))
        {
            var f = JsonSerializer.Deserialize<TaggFoer>(p.KildeReferanserJson!);
            if (f is null || f.OmgjortFraBegrep != begrepId) continue;
            var t = await db.TekstTagger.FirstOrDefaultAsync(x => x.Id == p.EntitetId, ct);
            if (t is null) continue;
            (t.Kind, t.RefId, t.StartOffset, t.EndOffset, t.QuotePrefix, t.QuoteExact, t.QuoteSuffix) =
                (f.Kind, f.RefId, f.Start, f.Slutt, f.Prefix, f.Exact, f.Suffix);
            db.Proveniens.Add(ProveniensHjelper.NyRad("tekst_tagg", t.Id, t.VirksomhetId, "endret", bruker));
            tagger++;
        }
        foreach (var p in flyttinger.Where(p => p.EntitetType == StrukturkantTjeneste.ProveniensType))
        {
            var f = JsonSerializer.Deserialize<KantFoer>(p.KildeReferanserJson!);
            if (f is null || f.OmgjortFraBegrep != begrepId) continue;
            var k = await db.Strukturkanter.FirstOrDefaultAsync(x => x.Id == p.EntitetId, ct);
            if (k is null) continue;
            (k.FraBegrepId, k.FraVirksomhetId, k.TilBegrepId, k.TilVirksomhetId) = (f.FraBegrepId, f.FraVirksomhetId, f.TilBegrepId, f.TilVirksomhetId);
            k.SistEndretAv = bruker;
            k.SistEndretTidspunkt = DateTimeOffset.UtcNow;
            db.Proveniens.Add(ProveniensHjelper.NyRad(StrukturkantTjeneste.ProveniensType, k.Id, virksomhetId: null, "endret", bruker));
            kanter++;
        }
        begrep.Entitetsstatus = "gjeldende";
        begrep.SistEndretAv = bruker;
        begrep.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", begrep.Id, virksomhetId: null, "gjenopprettet", bruker));
        await db.SaveChangesAsync(ct);

        var tekst = string.IsNullOrWhiteSpace(begrunnelse) ? $"Omgjøringen av «{begrep.Term}» er angret." : begrunnelse.Trim();
        foreach (var b in foreslatte) await identitet.AvvisNavneformAsync(b.Id, tekst, bruker, ct);
        foreach (var a in typer) await identitet.AvvisAktortypeAsync(a.Id, tekst, bruker, ct);
        await tx.CommitAsync(ct);
        return new AngreOmgjoringResultat(begrepId, tagger, kanter, foreslatte.Count, typer.Count);
    }
}
