namespace RegelIde.Kildekonvertering.Tests;

internal static class Testdata
{
    public static string LesAlkoholloven() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "alkoholloven-LOV-1989-06-02-27.html"));

    public static string LesAlkoholforskriften() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "alkoholforskriften-FOR-2005-06-08-538.html"));

    /// <summary>
    /// [Ny, hjemmel-presisjon-runden, 2026-09-10, issue #217] Ekte, ubearbeidet Lovdata-HTML — den
    /// forskriften Johann pekte på: hjemmelen er «§ 13-1 fjerde ledd», og dokumentet har presisjonen
    /// i ÉN href («miscInformation»: <c>lov/2024-03-08-9/§13-1/ledd/4</c>) men ikke i den andre
    /// («basedOn»: <c>lov/2024-03-08-9/§13-1</c>). Den ENESTE fixturen som viser de to feltene sprike,
    /// og derfor grunnlaget for hele runden.
    ///
    /// <para>Hentet fra vår egen base 2026-09-10 (rettskilde 5a1f2d39, rå kilde lagret ved import) —
    /// bitidentisk med det vi faktisk importerte, ikke en ny henting fra Lovdata.</para>
    /// </summary>
    public static string LesNtnuPhdForskriften() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "ntnu-phd-forskriften-FOR-2026-02-03-387.html"));

    public static string LesForvaltningsloven() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "forvaltningsloven-LOV-1967-02-10.html"));

    /// <summary>
    /// Ekte, ubearbeidet Lovdata-HTML — kapittelfri lov (paragrafer direkte i documentBody, ingen
    /// omsluttende &lt;section class="section"&gt;). Bekreftet ekte tilfelle funnet under full
    /// Lovdata-synkronisering 2026-08-20 (docs/13-backlog.md §6).
    /// </summary>
    public static string LesMotorferdselloven() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "motorferdselloven-LOV-1977-06-10-82.html"));

    /// <summary>
    /// Ekte, ubearbeidet Lovdata-HTML — "Kap. N."-forkortelsen (se FjernNummerPrefiks) OG en liste
    /// direkte under en paragraf uten noe omsluttende ledd (se ParseParagraf) — begge løst i samme
    /// runde som personopplysningsloven-fixturen, jf. https://api.lovdata.no/xmldocs-gjennomgangen.
    /// </summary>
    public static string LesTannhelsetjenesteloven() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "tannhelsetjenesteloven-LOV-1983-06-03-54.html"));

    /// <summary>
    /// Ekte, ubearbeidet Lovdata-HTML — innlemmer hele EUs GDPR-forordning som en egen "gdpr"-navngitt
    /// underinndeling, med flere strukturvarianter som ikke fantes i noen av de andre fixturene
    /// (KAPITTEL-ord i store bokstaver, kommentarprosa uten paragrafer, en tredje underinndelings-
    /// dybde ("Avsnitt N") uten data-name-attributt, sentrerte avslutningsavsnitt) — den fixturen som
    /// først avdekket at parseren var bygget empirisk og ikke mot Lovdatas offisielle formatdokumentasjon
    /// (https://api.lovdata.no/xmldocs), se docs/13-backlog.md.
    /// </summary>
    public static string LesPersonopplysningsloven() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "personopplysningsloven-LOV-2018-06-15-38.html"));

    /// <summary>
    /// Ekte tekst (ikke syntetisk) fra Bergen kommunes retningslinjer SD-24-113 — se
    /// data/kilder/raw-handbok/README.md for proveniens (hentet via WebFetch + PDF-tekstlag 2026-08-12).
    /// </summary>
    public static string LesBergenRetningslinjer() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "bergen-retningslinjer-SD-24-113.txt"));

    /// <summary>
    /// Ekte tekst (ikke syntetisk) fra Bergen kommunes FORSKRIFT om salgs-, skjenke- og
    /// åpningstider (Dok.nr SD-24-114) — se data/kilder/raw-handbok/README.md for proveniens og for
    /// det reelle strukturfunnet (bare tallpunktum-overskrifter, ikke "Kapittel N", på toppnivå).
    /// </summary>
    public static string LesBergenForskrift() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "bergen-forskrift-salgs-skjenke-apningstider.txt"));
}
