using Ganss.Xss;

namespace RegelIde.Data;

/// <summary>
/// Saner rik tekst i håndbok-kommentarseksjoner til det bevisst begrensede markup-settet
/// (docs/03-domenemodell.md §1.1.1, "Redigeringsflate"): avsnitt, overskrift, fet/kursiv/understreking,
/// lenke, intern referanse — ingen tabeller, bilder eller egne farger/fonter. Bruker
/// <c>HtmlSanitizer</c> (Ganss.Xss, ny avhengighet 2026-07-26) fremfor å skrive egen HTML-parsing —
/// en allow-list-sanitizer er nøyaktig oppgaven biblioteket løser, og "ingen gjettet fallback"-prinsippet
/// (§3.3) tilsier å bruke et herdet, mye brukt bibliotek for noe sikkerhetskritisk som dette, ikke en
/// hjemmesnekret regex. Kalles alltid server-side før lagring — klienten er ikke tiltrodd, uansett hva
/// klientkoden selv gjør (samme presisering som i domenemodellen).
///
/// Interne referanser lagres som typede pekere, ikke URL-er: en <c>&lt;a data-ref-kind="..." data-ref-id="..."&gt;</c>
/// -markør (§1.1.1) i stedet for <c>href</c>, slik at en fremtidig påvirkningsanalyse kan følge dem.
/// </summary>
internal static class KommentarTekstSanering
{
    private static readonly HtmlSanitizer Sanitizer = OpprettSanitizer();

    public static string Saner(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return Sanitizer.Sanitize(html);
    }

    private static HtmlSanitizer OpprettSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "h3", "h4", "b", "strong", "i", "em", "u", "a" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("data-ref-kind");
        sanitizer.AllowedAttributes.Add("data-ref-id");

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("http");

        sanitizer.AllowedCssProperties.Clear();
        // KeepChildNodes=false (bibliotekets standard, bekreftet empirisk 2026-07-26): en fjernet tag
        // fjerner HELE undertreet sitt, inkludert tekstinnhold. Nødvendig for <script> (ellers lekker
        // JS-koden som synlig tekst i stedet for å bli helt fjernet) — <table>-innhold forsvinner også
        // helt fremfor å flates ut, som er greit siden tabeller uansett ikke er del av markup-settet.
        return sanitizer;
    }
}
