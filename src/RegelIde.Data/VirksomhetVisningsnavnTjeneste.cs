using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, registernavn-runden, 2026-09-08] Løser en virksomhets VISNINGSNAVN: navneformen med grunn
/// <c>'gjeldende'</c> når den finnes, ellers <see cref="Virksomhet.Navn"/>.
///
/// <para>
/// <b>Hvorfor dette laget finnes.</b> Fra denne runden er <see cref="Virksomhet.Navn"/> registerets
/// egen form (issue #158) — som for Brreg betyr VERSALER, og for tospråklige kommuner en
/// konkatenering uten skilletegn: «GAIVUONA SUOHKAN KÅFJORD KOMMUNE KAIVUONON KOMUUNI». Den strengen
/// er korrekt som REGISTRERING, men uleselig som etikett i en tagg-liste. Johann valgte eksplisitt
/// (2026-09-08) at visningen skal bruke navneformen: «A: navneformen — «Kåfjord kommune»».
/// </para>
///
/// <para>
/// <b>Navnet skrives ALDRI om av dette.</b> Visningsnavnet er et avledet, beregnet felt — det lagres
/// ikke, og <see cref="Virksomhet.Navn"/> røres ikke. #158 sier at et navn hentet fra registeret skal
/// beholdes i registerets form, og det gjør det: begge formene finnes, og siden viser den ene og
/// oppgir den andre.
/// </para>
///
/// <para>
/// <b>Faller tilbake på <see cref="Virksomhet.Navn"/>, aldri på et gjettet navn.</b> 49 av
/// katalogradene hadde ingen autoritativ lesbar form da runden startet; 45 av dem fikk en godkjent
/// navneform (<see cref="VirksomhetNavneformOverstyringer"/>) og fire ble fjernet. En rad som likevel
/// mangler navneform vises med registerets form — synlig, og ikke maskert av en algoritmisk
/// omskriving. Det var nettopp en slik omskriving som skapte problemet denne runden løser.
/// </para>
///
/// <para>
/// <b>Flere <c>'gjeldende'</c>-navneformer skal ikke forekomme</b> (det er én gjeldende form per
/// virksomhet), men vokabularet håndhever det ikke — verken en unik indeks eller CHECK-constrainten
/// dekker «maks én gjeldende per virksomhet». Skulle det oppstå, velges den alfabetisk første, slik at
/// visningen er DETERMINISTISK mellom kall i stedet for å hoppe med databasens radrekkefølge.
/// </para>
/// </summary>
public sealed class VirksomhetVisningsnavnTjeneste(RegelIdeDbContext db)
{
    /// <summary>Grunnen som utpeker visningsformen — samme verdi
    /// <see cref="VirksomhetsbegrepTjeneste.Navneformgrunner"/> definerer.</summary>
    public const string VisningsGrunn = "gjeldende";

    /// <summary>
    /// Visningsnavn for ALLE virksomheter, nøklet på virksomhetens id. Ett spørsmål mot
    /// <c>begreper</c> for hele katalogen — <see cref="Virksomhet"/>-radene har kalleren allerede, og
    /// et oppslag per rad ville gitt 447 spørringer for én liste.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, string>> AlleAsync(CancellationToken ct = default)
    {
        var navneformer = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet"
                        && b.Navneformgrunn == VisningsGrunn
                        && b.Entitetsstatus == "gjeldende"
                        && b.VirksomhetReferanseId != null)
            .Select(b => new { VirksomhetId = b.VirksomhetReferanseId!.Value, b.Term })
            .ToListAsync(ct);

        return navneformer
            .GroupBy(n => n.VirksomhetId)
            .ToDictionary(g => g.Key, g => g.Select(n => n.Term).OrderBy(t => t, StringComparer.Ordinal).First());
    }

    /// <summary>Visningsnavn for ÉN virksomhet. <c>null</c> når den ikke har noen
    /// <c>'gjeldende'</c>-navneform — kalleren faller da tilbake på <see cref="Virksomhet.Navn"/>.</summary>
    public async Task<string?> ForAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        var termer = await db.Begreper
            .Where(b => b.Begrepskategori == "virksomhet"
                        && b.Navneformgrunn == VisningsGrunn
                        && b.Entitetsstatus == "gjeldende"
                        && b.VirksomhetReferanseId == virksomhetId)
            .Select(b => b.Term)
            .ToListAsync(ct);

        return termer.Count == 0 ? null : termer.OrderBy(t => t, StringComparer.Ordinal).First();
    }
}
