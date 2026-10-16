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

    public static readonly string[] Alle = [OmraderegisterKommune];

    /// <summary>
    /// Id-en migrasjonen <c>KlasseKommuneOgUtledetMedlemskap</c> gir klassen «kommune». Den er fast bare for at migrasjonens <c>Down</c>
    /// skal treffe nøyaktig raden <c>Up</c> la inn; koden finner klassen på regelen (<see cref="KlassemedlemskapTjeneste.KlasseForRegelAsync"/>).
    /// </summary>
    public static readonly Guid KommuneKlasseId = Guid.Parse("eb25cd79-007e-404d-9cd5-c85800b65272");

    /// <summary>Hjemmelen for klassen «kommune»: kommuneloven § 2-1 (første ledd: «Norge er inndelt i kommuner …», annet ledd: «Hver
    /// kommune … er et eget rettssubjekt»). eId-en er verksnivå (ELI-en til loven + bestemmelsen), ikke en bestemt versjon.</summary>
    public const string KommunelovenParagraf21Eid = "https://lovdata.no/eli/lov/2018/06/22/83/nor/§2-1";
}

/// <summary>Ett utledet medlem: rettssubjektet, kommunen (området) det er rettssubjekt for, og territoriekanten som er premisset.</summary>
public sealed record UtledetMedlem(
    Guid VirksomhetId, string Navn, string Kommunenummer, Guid OmradeId, string Omradenavn, Guid PremissKantId, bool Validert);

/// <summary>
/// Svaret på «hvem er medlem av klassen?» for en klasse med <see cref="BegrepEntitet.Medlemskapsregel"/>. <see cref="Kunnskapsstatus"/> er
/// alltid <c>avledet</c> (medlemskapet er ikke lagret, det følger av premissene); <see cref="KlassensKunnskapsstatus"/> gjelder klassen selv.
/// <see cref="Hull"/> er kommunene som ikke kunne gjøres til medlem, i klartekst — de er IKKE med i <see cref="Medlemmer"/>.
/// </summary>
public sealed record UtledetMedlemskap(
    Guid KlasseId, string Term, string Regel, string? Hjemmel, string? KlassensKunnskapsstatus, string Kunnskapsstatus,
    int AntallOmrader, IReadOnlyList<UtledetMedlem> Medlemmer, IReadOnlyList<string> Hull);

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
        var aktorer = await db.Virksomheter.Where(v => aktorIder.Contains(v.Id))
            .Select(v => new { v.Id, v.Navn, v.Kommunenummer }).ToDictionaryAsync(v => v.Id, ct);
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
                medlemmer.Add(new UtledetMedlem(v.Id, v.Navn, k.Kode, k.Id, k.Term, kant.Id, kant.Status == Strukturkanter.Godkjent));
            }
            else
            {
                hull.Add(egne.Count == 0
                    ? $"{k.Kode} {k.Term}: ingen aktør med ansvarsområde til kommunen og samme kommunenummer — ikke medlem (ikke funnet ≠ oppfunnet)."
                    : $"{k.Kode} {k.Term}: {egne.Count} aktører med ansvarsområde til kommunen og samme kommunenummer — ikke entydig, ingen valgt.");
            }
        }
        return new UtledetMedlemskap(klasse.Id, klasse.Term, Klasseregler.OmraderegisterKommune, klasse.LovreferanseEid, klasse.Kunnskapsstatus,
            Strukturkanter.Avledet, kommuner.Count, medlemmer, hull);
    }
}
