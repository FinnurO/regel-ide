using System.Xml;
using System.Xml.Schema;

namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Faktisk skjemavalidering av <see cref="AknXmlSkriver"/>s output mot den offisielle AKN 3.0-skjemaen
/// (akomantoso30.xsd, OASIS LegalDocML v1.0, godkjent 2018-08-29 — vendoret i Testdata/Xsd/, se
/// RegelIde.Kildekonvertering.Tests.csproj). IKKE en håndkontrollert "ser riktig ut"-sjekk, og IKKE en
/// hjemmesnekret delmengde av skjemaet — <see cref="System.Xml.Schema.XmlSchemaSet"/> +
/// <see cref="XmlReaderSettings.ValidationType"/>=Schema kjører hele XSD-en, inkludert
/// innholdsmodeller (elementrekkefølge/-kardinalitet), attributt-krav og nøkkelbegrensninger
/// (f.eks. unik eId).
///
/// Bakgrunn: docs/13-backlog.md pkt. 9 og docs/15-handbok-dokumentgraf-notat.md §9.5/§14 dokumenterte
/// to konkrete brudd funnet ved manuell skjemavalidering (bar <c>kildeId</c>-attributt, manglende
/// <c>FRBRdate</c>). En fullstendig kjøring av EKTE skjemaet mot ekte Lovdata-fixtures under selve
/// rettingen av disse to bruddene avdekket ytterligere fire (duplikat/manglende-href
/// TLCOrganization, ugyldig "end"-attributt, feilplassert authorialNote, manglende "name" på
/// hcontainer) — alle rettet i samme omgang, se AknXmlSkriver.cs sine doc-kommentarer for hver.
///
/// Denne testen er derfor den egentlige regresjonsvakten for HELE settet, ikke bare de to opprinnelig
/// navngitte bruddene — en fremtidig endring som (re)introduserer NOEN av de seks skal feile her.
/// </summary>
public class AknXmlSkjemaValideringTests
{
    private static readonly XmlSchemaSet Skjema = LastAknSkjema();

    private static XmlSchemaSet LastAknSkjema()
    {
        var xsdMappe = Path.Combine(AppContext.BaseDirectory, "Testdata", "Xsd");
        var settings = new XmlReaderSettings();
        var skjemasett = new XmlSchemaSet();

        using (var xmlXsdReader = XmlReader.Create(Path.Combine(xsdMappe, "xml.xsd"), settings))
        {
            skjemasett.Add("http://www.w3.org/XML/1998/namespace", xmlXsdReader);
        }
        using (var aknXsdReader = XmlReader.Create(Path.Combine(xsdMappe, "akomantoso30.xsd"), settings))
        {
            skjemasett.Add(null, aknXsdReader);
        }

        skjemasett.Compile();
        return skjemasett;
    }

    /// <summary>
    /// Kjører <paramref name="aknXml"/> gjennom en ekte validerende <see cref="XmlReader"/> og returnerer
    /// alle skjemafeil/-advarsler (linje:posisjon + melding), i motsetning til å kaste på første feil —
    /// slik at et testfeilende assert viser HELE listen, ikke bare den første feilen.
    /// </summary>
    private List<string> Valider(string aknXml)
    {
        var feil = new List<string>();
        IXmlLineInfo? posisjon = null;

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = Skjema,
        };
        settings.ValidationEventHandler += (_, e) =>
        {
            var p = posisjon is null ? "?:?" : $"{posisjon.LineNumber}:{posisjon.LinePosition}";
            feil.Add($"L{p} [{e.Severity}] {e.Message}");
        };

        using var reader = XmlReader.Create(new StringReader(aknXml), settings);
        posisjon = (IXmlLineInfo)reader;
        while (reader.Read())
        {
        }

        return feil;
    }

    [Fact]
    public void AknXml_for_alkoholloven_validerer_mot_offisiell_akn30_skjema()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 12));
        var feil = Valider(resultat.AknXml);
        Assert.True(feil.Count == 0, $"{feil.Count} skjemafeil i AKN-XML for alkoholloven:\n{string.Join("\n", feil)}");
    }

    [Fact]
    public void AknXml_for_alkoholforskriften_validerer_mot_offisiell_akn30_skjema()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 12));
        var feil = Valider(resultat.AknXml);
        Assert.True(feil.Count == 0, $"{feil.Count} skjemafeil i AKN-XML for alkoholforskriften:\n{string.Join("\n", feil)}");
    }

    [Fact]
    public void AknXml_for_forvaltningsloven_validerer_mot_offisiell_akn30_skjema()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 8, 12));
        var feil = Valider(resultat.AknXml);
        Assert.True(feil.Count == 0, $"{feil.Count} skjemafeil i AKN-XML for forvaltningsloven:\n{string.Join("\n", feil)}");
    }

    [Fact]
    public void Opphevet_paragraf_validerer_ogsa_mot_skjemaet()
    {
        // Alkoholloven §1-12/§1-13 er opphevet med dato (se AlkohollovenKonverteringTests) — dette er
        // spesifikt banen som tidligere skrev det ugyldige "end"-attributtet og <proprietary>-barnet.
        // Egen test (i tillegg til den generelle over) for å gjøre regresjonsformålet eksplisitt.
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 12));
        var opphevetParagraf = resultat.Noder.First(n => n.NodeType == NodeType.Paragraf && n.Opphevet && n.OpphevetDato is not null);
        Assert.Contains($"eId=\"{opphevetParagraf.Eid}\"", resultat.AknXml);

        var feil = Valider(resultat.AknXml);
        Assert.True(feil.Count == 0, $"{feil.Count} skjemafeil:\n{string.Join("\n", feil)}");
    }
}
