using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #395, Johanns presisering 2026-10-23] Hvilke rettskilder de korpusbrede sveipene hopper over som standard:
/// «Konverterings-, begreps-, regel- og tjenestesveipene hopper som standard over <c>ikrafttredelse</c> og rene endringsvedtak, men kan
/// velge å ta dem med.» Et ikrafttredelsesvedtak eller et rent endringsvedtak har ingen egne begreper eller regler; det setter i kraft
/// eller endrer en annen kilde, og treffene der er dubletter av treffene i kilden det gjelder.
/// <para>
/// Regelen er smal med vilje: en kilde hoppes over bare når ALLE disposisjonstypene den har er <c>ikrafttredelse</c>, eller når den er
/// kunngjort (Lovtidend) med tittelen til et rent endringsvedtak og ingen organiseringstype. «Ikrafttredelse av lov … Delegering av
/// myndighet» er også et delegeringsvedtak og blir med. En uklassifisert kilde blir alltid med — uklassifisert er ikke det samme som
/// ikrafttredelse (§8). Velger en kaller én bestemt rettskilde, gjelder ikke filteret: det eksplisitte valget vinner.
/// </para>
/// <para>
/// Aktørsveipet (per rettskilde) og livsløpet (<see cref="DepartementLivslopTjeneste"/>) bruker tvert imot organiseringstypene som
/// UTVALG, se <see cref="Disposisjonstyper.Organiseringstyper"/>.
/// </para>
/// </summary>
public static class Sveipeutvalg
{
    public static async Task<HashSet<Guid>> StandardUtelattAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var typer = await db.RettskildeDisposisjonstyper
            .Select(d => new { d.RettskildeId, d.Disposisjonstype })
            .ToListAsync(ct);
        var perKilde = typer.GroupBy(t => t.RettskildeId).ToDictionary(g => g.Key, g => g.Select(x => x.Disposisjonstype).ToHashSet());

        var utelatt = perKilde.Where(p => p.Value.All(t => t == Disposisjonstyper.Ikrafttredelse)).Select(p => p.Key).ToHashSet();

        var kunngjorte = await db.Rettskilder
            .Where(r => r.Kildeform == "kunngjort")
            .Select(r => new { r.Id, r.Tittel })
            .ToListAsync(ct);
        foreach (var k in kunngjorte)
        {
            var harOrganiseringstype = perKilde.TryGetValue(k.Id, out var t) && t.Overlaps(Disposisjonstyper.Organiseringstyper);
            if (!harOrganiseringstype && Disposisjonstyper.ErRentEndringsvedtak(k.Tittel)) utelatt.Add(k.Id);
        }
        return utelatt;
    }
}
