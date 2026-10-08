using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #312] Ett område slik oppslaget viser det.</summary>
public sealed record OmradeVisning(Guid Id, string Navn, string? Omradetype, string? Omradekode);

/// <summary>[Ny, issue #312] En aktør med ansvarsområde, og HVILKET område kanten lander på (kommunen selv, fylket,
/// lagdømmet …) — slik at «hvorfor er Statsforvalteren i Troms og Finnmark med?» kan besvares: via fylket Finnmark.</summary>
public sealed record AnsvarligAktor(Guid VirksomhetId, string Navn, string Typekode, OmradeVisning Via, Guid KantId);

/// <summary>
/// [Ny, issue #312] Én rubrikk i svaret («tingrett»), med kandidatene. <see cref="Entydig"/> er true bare når det
/// er nøyaktig én — ved null eller flere velges ingenting (CLAUDE.md §8), og kalleren viser «mangler» eller
/// «ikke entydig» med kandidatene (samme regel #314 skal bruke).
/// </summary>
public sealed record Tilhorighetsrubrikk(string Rubrikk, IReadOnlyList<string> Kandidater, IReadOnlyList<Guid> Ider)
{
    public bool Entydig => Ider.Count == 1;
    public string Status => Ider.Count switch { 0 => "mangler", 1 => "entydig", _ => "ikke_entydig" };
}

/// <summary>[Ny, issue #312, AC5] Svaret på «gitt kommune X → fylke, tingrett, lagsogn, lagdømme, lagmannsrett,
/// statsforvalter, RHF».</summary>
public sealed record KommuneTilhorighet(
    OmradeVisning Kommune,
    IReadOnlyList<Tilhorighetsrubrikk> Rubrikker,
    IReadOnlyList<OmradeVisning> Overordnede,
    IReadOnlyList<AnsvarligAktor> Ansvarlige);

/// <summary>
/// [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08, AC5, docs/32 S6/S8] Oppslaget «gitt en kommune:
/// hvilket fylke, hvilken tingrett, hvilket lagsogn og lagdømme, hvilken lagmannsrett, statsforvalter og RHF?» —
/// beregnet fra strukturkantene, ikke lagret.
/// <para>
/// <b>Algoritmen.</b> (1) Alle områder kommunen inngår i: <c>O bestar_av</c>-kanter oppover, transitivt (fylke,
/// lagsogn → lagdømme, fylke → helseregion). (2) Alle aktører med <c>A har_ansvarsomrade</c> til kommunen eller et av
/// disse områdene. (3) Rubrikkene velges av OMRÅDETYPEN kanten lander på — ikke av navnet på aktøren:
/// </para>
/// <list type="bullet">
/// <item>fylke / lagsogn / lagdømme / helseregion = overordnede områder av den typen;</item>
/// <item>kommune (rettssubjekt) = aktøren med ansvarsområde til kommunen og samme <see cref="Virksomhet.Kommunenummer"/>;</item>
/// <item>tingrett = de ANDRE aktørene med ansvarsområde direkte til kommunen;</item>
/// <item>lagmannsrett = aktører med ansvarsområde til lagdømmet; statsforvalter = til fylket; RHF = til helseregionen.</item>
/// </list>
/// <para>
/// Bare gjeldende kanter teller (<see cref="StrukturkantTjeneste.FiltrerGjeldendeAsync"/>), og bare validerte.
/// Rubrikkreglene forutsetter at registeret er det eneste som gir slike kanter i dag. Får en fylkeskommune senere
/// <c>har_ansvarsomrade</c> til fylket, havner den i statsforvalter-rubrikken — da må rubrikken avgrenses (f.eks.
/// mot klassen «statsforvalter», #298). Det er bevisst ikke gjort nå, fordi ingen slik kant finnes (målt 2026-10-08).
/// </para>
/// </summary>
public sealed class OmradeOppslagTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste)
{
    public async Task<KommuneTilhorighet?> ForKommunenummerAsync(string kommunenummer, CancellationToken ct = default)
    {
        var id = await db.Begreper
            .Where(b => b.Begrepskategori == Nodetyper.Omrade && b.Omradetype == Omradetyper.Kommune && b.Omradekode == kommunenummer
                        && b.Entitetsstatus == "gjeldende" && b.GyldigTil == null)
            .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        return id is null ? null : await ForKommuneAsync(id.Value, ct);
    }

    /// <returns>Null hvis id-en ikke er et gjeldende kommuneområde.</returns>
    public async Task<KommuneTilhorighet?> ForKommuneAsync(Guid kommuneOmradeId, CancellationToken ct = default)
    {
        var kommune = await db.Begreper.FirstOrDefaultAsync(b => b.Id == kommuneOmradeId && b.Begrepskategori == Nodetyper.Omrade
                                                                 && b.Omradetype == Omradetyper.Kommune && b.Entitetsstatus == "gjeldende", ct);
        if (kommune is null) return null;

        // (1) Overordnede områder, transitivt via O-kanter (til = barnet, fra = forelderen).
        var oKanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Omradesammensetning && k.Status == "validert"
                        && k.FraBegrepId != null && k.TilBegrepId != null)
            .ToListAsync(ct), ct);
        var foreldre = oKanter.GroupBy(k => k.TilBegrepId!.Value).ToDictionary(g => g.Key, g => g.Select(k => k.FraBegrepId!.Value).ToList());
        var overordnedeIder = new List<Guid>();
        var ko = new Queue<Guid>([kommune.Id]);
        var sett = new HashSet<Guid> { kommune.Id };
        while (ko.Count > 0)
        {
            foreach (var f in foreldre.GetValueOrDefault(ko.Dequeue()) ?? [])
            {
                if (!sett.Add(f)) continue;
                overordnedeIder.Add(f);
                ko.Enqueue(f);
            }
        }
        var omrader = await db.Begreper.Where(b => sett.Contains(b.Id))
            .Select(b => new OmradeVisning(b.Id, b.Term, b.Omradetype, b.Omradekode)).ToDictionaryAsync(o => o.Id, ct);
        var overordnede = overordnedeIder.Select(i => omrader[i]).ToList();

        // (2) Aktører med ansvarsområde til kommunen eller et overordnet område.
        var aKanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade" && k.Status == "validert"
                        && k.FraVirksomhetId != null && k.TilBegrepId != null && sett.Contains(k.TilBegrepId.Value))
            .ToListAsync(ct), ct);
        var visninger = (await kanttjeneste.ByggVisningerAsync(aKanter, perspektiv: null, ct)).ToDictionary(v => v.Id);
        var ansvarlige = aKanter
            .Select(k => new AnsvarligAktor(k.FraVirksomhetId!.Value, visninger[k.Id].Fra.Navn, k.Typekode, omrader[k.TilBegrepId!.Value], k.Id))
            .OrderBy(a => a.Via.Omradetype == Omradetyper.Kommune ? 0 : 1).ThenBy(a => a.Navn, StringComparer.Ordinal)
            .ToList();

        // (3) Rubrikkene.
        var aktorIder = ansvarlige.Select(a => a.VirksomhetId).Distinct().ToList();
        var kommunenummer = await db.Virksomheter.Where(v => aktorIder.Contains(v.Id)).Select(v => new { v.Id, v.Kommunenummer })
            .ToDictionaryAsync(v => v.Id, v => v.Kommunenummer, ct);
        bool ErKommunensEgen(AnsvarligAktor a) =>
            a.Via.Id == kommune.Id && kommune.Omradekode is not null && kommunenummer.GetValueOrDefault(a.VirksomhetId) == kommune.Omradekode;

        Tilhorighetsrubrikk Omr(string rubrikk, string type)
        {
            var o = overordnede.Where(x => x.Omradetype == type).OrderBy(x => x.Navn, StringComparer.Ordinal).ToList();
            return new Tilhorighetsrubrikk(rubrikk, o.Select(x => x.Navn).ToList(), o.Select(x => x.Id).ToList());
        }
        Tilhorighetsrubrikk Akt(string rubrikk, Func<AnsvarligAktor, bool> filter)
        {
            var a = ansvarlige.Where(filter).GroupBy(x => x.VirksomhetId).Select(g => g.First()).OrderBy(x => x.Navn, StringComparer.Ordinal).ToList();
            return new Tilhorighetsrubrikk(rubrikk, a.Select(x => x.Navn).ToList(), a.Select(x => x.VirksomhetId).ToList());
        }

        var rubrikker = new List<Tilhorighetsrubrikk>
        {
            Akt("kommune", ErKommunensEgen),
            Omr("fylke", Omradetyper.Fylke),
            Akt("tingrett", a => a.Via.Id == kommune.Id && !ErKommunensEgen(a)),
            Omr("lagsogn", Omradetyper.Lagsogn),
            Omr("lagdømme", Omradetyper.Lagdomme),
            Akt("lagmannsrett", a => a.Via.Omradetype == Omradetyper.Lagdomme),
            Akt("statsforvalter", a => a.Via.Omradetype == Omradetyper.Fylke),
            Omr("helseregion", Omradetyper.Helseregion),
            Akt("RHF", a => a.Via.Omradetype == Omradetyper.Helseregion),
        };
        return new KommuneTilhorighet(omrader[kommune.Id], rubrikker, overordnede, ansvarlige);
    }
}
