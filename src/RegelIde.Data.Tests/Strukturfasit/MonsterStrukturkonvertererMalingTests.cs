using RegelIde.Data.Strukturkonvertering;
using Xunit.Abstractions;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Måler <see cref="MonsterStrukturkonverterer"/> mot
/// strukturfasiten for de fem kildene (docs/33 §5.4) og skriver rapporten.
/// <para>
/// Bevisst UTEN <c>DataTestCollection</c>/embedded Postgres: målingen leser JSON fra
/// <c>data/fasit/strukturmodell/</c> og skal gå i en vanlig testkjøring uten database og nettverk
/// (#307 akseptansekriterium 1). Den kan derfor også kjøre parallelt med DB-testene.
/// </para>
/// </summary>
public class MonsterStrukturkonvertererMalingTests(ITestOutputHelper output)
{
    /// <summary>
    /// Mønstre som ble prøvd og tatt ut, med grunnen. Skrives inn i rapporten, så neste runde ikke prøver
    /// det samme igjen uten å vite hvorfor det ble forkastet (CLAUDE.md §7: avviste alternativer er
    /// dokumentasjon).
    /// </summary>
    internal static readonly IReadOnlyList<(string Id, string Grunn)> ForkastedeMonstre = [];

    [Fact]
    public void Maling_mot_fasiten_skriver_rapport_og_holder_tersklene()
    {
        var kilder = StrukturfasitLeser.LesAlle();
        var konverterer = new MonsterStrukturkonverterer();
        var malinger = kilder
            .Select(k => Strukturmaling.Mal(k.Navn, k.Fasit, konverterer.Konverter(k.Grunnlag)))
            .ToList();

        var rapport = Malerapport.Lag(malinger, ForkastedeMonstre);
        output.WriteLine(rapport);
        File.WriteAllText(Path.Combine(StrukturfasitLeser.FasitMappe, "maling-monster.md"), rapport);

        // [LÅST, #307 akseptansekriterium 3] K forskriftskompetanse: presisjon ≥ 0,9 mot fasiten.
        var forskrift = Tall.For(malinger, r => r.Utsagn.Type == "forskriftskompetanse");
        Assert.True(forskrift.Presisjon >= 0.9,
            $"Presisjon K forskriftskompetanse er {Malerapport.P(forskrift.Presisjon)}, krav ≥ 90 %.");
    }
}
