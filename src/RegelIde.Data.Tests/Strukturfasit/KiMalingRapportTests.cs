using Xunit.Abstractions;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #308 strukturmodell-KI, 2026-10-07] Regenererer <c>maling-ki.md</c> fra de LAGREDE KI-utdataene
/// (<c>ki-utdata/</c>) og en fersk kjøring av mønsterlaget — uten nettverk, i vanlig testkjøring. Endres
/// mønstrene eller fasiten, følger KI-sammenligningen og unionen med, uten at KI-en kjøres på nytt.
/// <para>
/// Finnes ingen lagret kjøring (før første live-måling), er det ingenting å regenerere, og testen gjør
/// ingenting — den sier det i utskriften. Det er ikke en stille suksess for en måling som mangler:
/// live-målingen er en egen test (<see cref="KiStrukturkonvertererLiveMalingTests"/>).
/// </para>
/// </summary>
public class KiMalingRapportTests(ITestOutputHelper output)
{
    [Fact]
    public void Rapporten_regenereres_fra_lagret_ki_utdata()
    {
        var kjoring = KiMaling.LesKjoring();
        if (kjoring is null)
        {
            output.WriteLine("Ingen ki-utdata/kjoring.json — live-målingen er ikke kjørt ennå. Ingenting å regenerere.");
            return;
        }

        var kilder = StrukturfasitLeser.LesAlle();
        Assert.Equal(kilder.Select(k => k.Navn).Order(), kjoring.Kilder.Select(k => k.Kilde).Order());

        var rapport = KiMalerapport.Lag(KiMalerapport.Mal(kilder, kjoring), kjoring);
        File.WriteAllText(Path.Combine(StrukturfasitLeser.FasitMappe, "maling-ki.md"), rapport);
        output.WriteLine(rapport);
    }
}
