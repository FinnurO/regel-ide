using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, gruppemedlemskap-runden, 2026-09-08, issue #164] Tjenestelaget for «gruppe av gruppe» —
/// <see cref="GruppeMedlemskapEntitet"/>. Speiler <see cref="MyndighetstildelingTjeneste"/> bevisst
/// nøyaktig i form (samme validering, samme «Ingen gjettet fallback»-feilmeldinger, samme
/// proveniensrad, samme <c>ParagrafspennPar</c>-serialisering): de to er de to nivåene i det samme
/// hierarkiet, og en leser som har forstått den ene skal ikke måtte lære en ny form for å lese den
/// andre.
/// <para>
/// Det ENESTE som skiller dem er sykelvalideringen under — den finnes ikke i
/// <see cref="MyndighetstildelingTjeneste"/>, fordi en kant til en <see cref="Virksomhet"/> aldri kan
/// være del av en sykel (virksomheter har ingen utgående gruppekanter i denne modellen).
/// </para>
/// </summary>
public sealed class GruppeMedlemskapTjeneste(RegelIdeDbContext db)
{
    /// <summary>
    /// Registrerer at <paramref name="underordnetGruppeBegrepId"/> er MEDLEM av
    /// <paramref name="overordnetGruppeBegrepId"/>, hjemlet i
    /// <paramref name="hjemmelRettskildeId"/>.
    /// <para>
    /// <b>Idempotent:</b> finnes kanten allerede (samme par, uansett hjemmel), returneres den
    /// EKSISTERENDE raden uendret i stedet for at en duplikat legges inn eller den unike indeksen
    /// <c>ux_gruppe_medlemskap_par</c> velter kallet med en ufanget <c>DbUpdateException</c>. Dette er
    /// det <see cref="SamiskSprakforvaltningSeed"/> hviler på for å kunne kjøres om igjen.
    /// </para>
    /// <para>
    /// <b>Sykler avvises</b> (Johanns eksplisitte valg, issue #164), i to trinn: selv-medlemskap
    /// direkte, og lengre sirkulære kjeder ved å traversere de EKSISTERENDE kantene fra den
    /// underordnede gruppen og nedover — finner traverseringen den overordnede gruppen, ville den nye
    /// kanten lukket en ring. Feilmeldingen navngir hele kjeden, slik at saksbehandleren ser HVILKEN
    /// registrering som må rettes, ikke bare at «noe» er sirkulært.
    /// </para>
    /// </summary>
    public async Task<GruppeMedlemskapEntitet> OpprettAsync(
        Guid overordnetGruppeBegrepId, Guid underordnetGruppeBegrepId, Guid hjemmelRettskildeId,
        IReadOnlyList<ParagrafspennPar> paragrafspenn, string opprettetAv,
        DateOnly? gyldigFra = null, DateOnly? gyldigTil = null, CancellationToken ct = default)
    {
        // Selv-medlemskap først: en tydeligere feilmelding enn den generelle sykelmeldingen, og
        // sjekken må uansett stå her fordi traverseringen under starter ETTER den underordnede noden.
        if (overordnetGruppeBegrepId == underordnetGruppeBegrepId)
        {
            throw new ArgumentException(
                "En gruppe kan ikke være medlem av seg selv. Ingen gjettet fallback.");
        }

        var overordnet = await FinnGruppebegrepAsync(overordnetGruppeBegrepId, ct);
        var underordnet = await FinnGruppebegrepAsync(underordnetGruppeBegrepId, ct);

        if (!await db.Rettskilder.AnyAsync(r => r.Id == hjemmelRettskildeId, ct))
        {
            throw new ArgumentException(
                $"Fant ingen rettskilde med id '{hjemmelRettskildeId}'. Ingen gjettet fallback.");
        }

        if (paragrafspenn.Count == 0)
        {
            throw new ArgumentException(
                "Paragrafspenn kan ikke være tomt — ingen gjettet fallback (docs/20 §7.1).");
        }
        foreach (var par in paragrafspenn)
        {
            if (!await db.RettskildeNoder.AnyAsync(n => n.Eid == par.FraEid, ct))
            {
                throw new ArgumentException(
                    $"Fant ingen rettskilde-node med eId '{par.FraEid}'. Ingen gjettet fallback.");
            }
            if (par.TilEid is not null && !await db.RettskildeNoder.AnyAsync(n => n.Eid == par.TilEid, ct))
            {
                throw new ArgumentException(
                    $"Fant ingen rettskilde-node med eId '{par.TilEid}'. Ingen gjettet fallback.");
            }
        }

        if (gyldigFra is not null && gyldigTil is not null && gyldigFra.Value > gyldigTil.Value)
        {
            throw new ArgumentException("GyldigFra kan ikke være etter GyldigTil. Ingen gjettet fallback.");
        }

        // Idempotens: kanten er ÉN opplysning uansett hvor mange ganger den registreres.
        var eksisterende = await db.GruppeMedlemskap.FirstOrDefaultAsync(
            m => m.OverordnetGruppeBegrepId == overordnetGruppeBegrepId
                 && m.UnderordnetGruppeBegrepId == underordnetGruppeBegrepId, ct);
        if (eksisterende is not null) return eksisterende;

        await KastHvisSykelAsync(overordnetGruppeBegrepId, underordnetGruppeBegrepId,
            overordnet.Term, underordnet.Term, ct);

        var medlemskap = new GruppeMedlemskapEntitet
        {
            Id = Guid.NewGuid(),
            OverordnetGruppeBegrepId = overordnetGruppeBegrepId,
            UnderordnetGruppeBegrepId = underordnetGruppeBegrepId,
            HjemmelRettskildeId = hjemmelRettskildeId,
            ParagrafspennJson = JsonSerializer.Serialize(paragrafspenn, JsonSerialiseringHjelper.Innstillinger),
            GyldigFra = gyldigFra,
            GyldigTil = gyldigTil,
            OpprettetAv = opprettetAv,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.GruppeMedlemskap.Add(medlemskap);
        db.Proveniens.Add(ProveniensHjelper.NyRad(
            "gruppe_medlemskap", medlemskap.Id, virksomhetId: null, "opprettet", opprettetAv));
        await db.SaveChangesAsync(ct);
        return medlemskap;
    }

    /// <summary>
    /// Traverserer NEDOVER fra <paramref name="startId"/> (den nye kantens underordnede gruppe) gjennom
    /// alle eksisterende medlemskapskanter. Treffer traverseringen
    /// <paramref name="målId"/> (den nye kantens overordnede gruppe), ville den nye kanten lukket en
    /// ring — og kallet avvises med den fulle kjeden i meldingen.
    /// <para>
    /// Bredde-først med en <c>besokt</c>-mengde: en gruppe kan lovlig være medlem av flere grupper
    /// (grafen er en DAG, ikke et tre), så samme node kan nås flere veier uten at det er en sykel.
    /// Uten <c>besokt</c> ville traverseringen vært eksponentiell i en dyp DAG.
    /// </para>
    /// </summary>
    private async Task KastHvisSykelAsync(
        Guid målId, Guid startId, string målTerm, string startTerm, CancellationToken ct)
    {
        var kanter = await db.GruppeMedlemskap
            .Select(m => new { m.OverordnetGruppeBegrepId, m.UnderordnetGruppeBegrepId })
            .ToListAsync(ct);
        var medlemmerPerGruppe = kanter
            .GroupBy(k => k.OverordnetGruppeBegrepId)
            .ToDictionary(g => g.Key, g => g.Select(k => k.UnderordnetGruppeBegrepId).ToList());

        // Sporer forgjengeren for hver besøkt node, slik at kjeden kan gjengis i feilmeldingen.
        var forgjenger = new Dictionary<Guid, Guid>();
        var besokt = new HashSet<Guid> { startId };
        var ko = new Queue<Guid>();
        ko.Enqueue(startId);

        while (ko.Count > 0)
        {
            var gjeldende = ko.Dequeue();
            if (!medlemmerPerGruppe.TryGetValue(gjeldende, out var medlemmer)) continue;
            foreach (var medlem in medlemmer)
            {
                if (medlem == målId)
                {
                    forgjenger[medlem] = gjeldende;
                    var kjede = await BeskrivKjedeAsync(startId, medlem, forgjenger, ct);
                    throw new ArgumentException(
                        $"«{målTerm}» er allerede medlem av «{startTerm}» (via {kjede}) — å registrere "
                        + $"«{startTerm}» som medlem av «{målTerm}» ville laget en sirkulær kjede. "
                        + "Ingen gjettet fallback.");
                }
                if (!besokt.Add(medlem)) continue;
                forgjenger[medlem] = gjeldende;
                ko.Enqueue(medlem);
            }
        }
    }

    /// <summary>Gjengir kjeden <paramref name="fraId"/> → … → <paramref name="tilId"/> med
    /// gruppebegrepenes TERMER, slik at feilmeldingen navngir de faktiske registreringene. Faller
    /// tilbake til rå id for et begrep som ikke lenger finnes — ingen oppfunnet term.</summary>
    private async Task<string> BeskrivKjedeAsync(
        Guid fraId, Guid tilId, Dictionary<Guid, Guid> forgjenger, CancellationToken ct)
    {
        var sti = new List<Guid> { tilId };
        var gjeldende = tilId;
        while (gjeldende != fraId && forgjenger.TryGetValue(gjeldende, out var forrige))
        {
            sti.Add(forrige);
            gjeldende = forrige;
        }
        sti.Reverse();

        var termer = await db.Begreper
            .Where(bb => sti.Contains(bb.Id))
            .Select(bb => new { bb.Id, bb.Term })
            .ToListAsync(ct);
        var termPerId = termer.ToDictionary(t => t.Id, t => t.Term);
        return string.Join(" → ", sti.Select(id => termPerId.TryGetValue(id, out var term) ? $"«{term}»" : id.ToString()));
    }

    private async Task<BegrepEntitet> FinnGruppebegrepAsync(Guid id, CancellationToken ct)
    {
        var begrep = await db.Begreper.FirstOrDefaultAsync(
            b => b.Id == id && b.Begrepskategori == "gruppe" && b.Entitetsstatus == "gjeldende", ct);
        return begrep ?? throw new ArgumentException(
            $"Fant ingen gruppebegrep med id '{id}'. Ingen gjettet fallback.");
    }

    /// <summary>Gruppene som er MEDLEM av <paramref name="overordnetGruppeBegrepId"/> — ett nivå ned,
    /// ikke transitivt. Drill-through-visningen på <c>BegrepDetalj</c> viser ett nivå om gangen, og en
    /// transitiv utfolding ville skjult hvilken hjemmel som navngir hvilket ledd.</summary>
    public Task<List<GruppeMedlemskapEntitet>> MedlemsgrupperForAsync(
        Guid overordnetGruppeBegrepId, CancellationToken ct = default) =>
        db.GruppeMedlemskap
            .Where(m => m.OverordnetGruppeBegrepId == overordnetGruppeBegrepId)
            .ToListAsync(ct);

    /// <summary>Gruppene <paramref name="underordnetGruppeBegrepId"/> selv er MEDLEM av — den motsatte
    /// retningen, slik at et gruppebegrep kan vise «medlem av» oppover og «medlemsgrupper» nedover.</summary>
    public Task<List<GruppeMedlemskapEntitet>> OverordnedeGrupperForAsync(
        Guid underordnetGruppeBegrepId, CancellationToken ct = default) =>
        db.GruppeMedlemskap
            .Where(m => m.UnderordnetGruppeBegrepId == underordnetGruppeBegrepId)
            .ToListAsync(ct);

    /// <summary>Samme form som <see cref="MyndighetstildelingTjeneste.LesParagrafspenn"/>.</summary>
    public static IReadOnlyList<ParagrafspennPar> LesParagrafspenn(GruppeMedlemskapEntitet medlemskap) =>
        JsonSerializer.Deserialize<List<ParagrafspennPar>>(
            medlemskap.ParagrafspennJson, JsonSerialiseringHjelper.Innstillinger) ?? [];
}
