namespace RegelIde.Data.Strukturkonvertering;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Noder inn → dokument i fasit-formatet ut (docs/33 §5.1).
/// <para>
/// Kontrakten er bevisst smal: ingen database, ingen nettverk, ingen sideeffekter. Konverteringen
/// skal kunne kjøres i en vanlig testkjøring og måles mot <c>data/fasit/strukturmodell/</c>, og
/// lagring (som forslag, aldri som validert — docs/33 §5.3) er et eget, senere steg (#313).
/// </para>
/// <para>
/// Implementasjoner: <see cref="MonsterStrukturkonverterer"/> (deterministiske mønstre, denne saken).
/// KI-laget (#308) skal implementere samme grensesnitt, slik at de to måles likt.
/// </para>
/// </summary>
public interface IStrukturkonverterer
{
    /// <summary>Konverterer alle tekstnodene i grunnlaget til strukturutsagn og aktører.</summary>
    Strukturdokument Konverter(Strukturkonverteringsgrunnlag grunnlag);
}
