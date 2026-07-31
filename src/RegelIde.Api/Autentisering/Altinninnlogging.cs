using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http.Extensions;

namespace RegelIde.Api.Autentisering;

/// <summary>
/// Sender uautentiserte nettleser-navigasjoner videre til Altinns innlogging.
/// <para>
/// JwtBearer utfordrer ikke av seg selv: den validerer cookien hvis den er der, og går videre uten
/// identitet hvis den ikke er der. Uten denne middlewaren laster SPA-en derfor helt fint for en
/// utlogget bruker, og feiler først når den kaller API-et — resultatet er en tom side med en
/// teknisk feilmelding i stedet for en innlogging. Ingenting i pipelinen ba noen gang om å
/// logge inn.
/// </para>
/// <para>
/// Plattformen tar seg av resten: den sjekker om det finnes en ID-porten-sesjon, ber om innlogging
/// hvis ikke, lar brukeren velge avgiver, setter <c>AltinnStudioRuntime</c>-cookien og sender
/// brukeren tilbake til <c>goto</c>. Se docs/autentisering.md.
/// </para>
/// </summary>
public static class Altinninnlogging
{
    /// <summary>
    /// Kortlevd markør for at vi allerede har sendt brukeren gjennom innloggingen én gang.
    /// <para>
    /// Uten den ville en cookie vi ikke godtar gitt en evig løkke mellom oss og plattformen: vi
    /// redirecter til innlogging, plattformen logger inn og sender brukeren tilbake, vi godtar
    /// fortsatt ikke cookien, og vi redirecter igjen. Det er nøyaktig det som skjer når
    /// <see cref="Altinninnstillinger.Plattform"/> peker på et annet miljø enn appen kjører i,
    /// for da er tokenet signert med en nøkkel vi ikke finner i JWKS-en vi spør.
    /// </para>
    /// </summary>
    public const string ForsokCookie = "regelide-innlogging-forsokt";

    /// <summary>
    /// Levetiden på markøren. Skal dekke én tur til plattformen og tilbake, og ikke mer — en
    /// sesjon som løper ut om en time skal fortsatt gi en ny innlogging, ikke feilsiden.
    /// </summary>
    private static readonly TimeSpan ForsokLevetid = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Stier som aldri skal redirectes.
    /// <para>
    /// <c>/api</c> skal svare 401 slik at klienten kan reagere på det selv; en 302 til plattformen
    /// ville blitt fulgt av <c>fetch</c> og gitt et uforståelig CORS-brudd i stedet for en
    /// statuskode. Helsesjekkene spørres av klyngen uten cookie, og en redirect der ville gjort at
    /// probene aldri ble klare — appen ville sett død ut for Kubernetes selv om den var frisk.
    /// </para>
    /// </summary>
    private static readonly string[] UnntatteStier = ["/api", "/helse", "/health"];

    /// <summary>
    /// Om forespørselen er en nettleser som ber om et dokument, og altså noe det gir mening å
    /// redirecte. <c>fetch</c>/XHR og klyngens prober ber ikke om <c>text/html</c>.
    /// </summary>
    public static bool ErNettlesernavigasjon(HttpRequest forespørsel)
    {
        if (!HttpMethods.IsGet(forespørsel.Method)) return false;

        var sti = forespørsel.Path.Value ?? "/";
        if (UnntatteStier.Any(unntak =>
                sti.Equals(unntak, StringComparison.OrdinalIgnoreCase)
                || sti.StartsWith($"{unntak}/", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return forespørsel.Headers.Accept
            .Any(verdi => verdi?.Contains("text/html", StringComparison.OrdinalIgnoreCase) is true);
    }

    /// <summary>
    /// Bygger den absolutte URL-en plattformen skal sende brukeren tilbake til.
    /// <para>
    /// <paramref name="tvingHttps"/> er nødvendig fordi TLS termineres i ingressen: Kestrel ser
    /// ren HTTP, så <c>Request.Scheme</c> er «http». Sendte vi det som <c>goto</c> ville brukeren
    /// kommet tilbake over HTTP. PathBase er med — uten prefikset ville returen landet utenfor appen.
    /// </para>
    /// </summary>
    public static string ByggReturUrl(HttpRequest forespørsel, bool tvingHttps) =>
        UriHelper.BuildAbsolute(
            tvingHttps ? "https" : forespørsel.Scheme,
            forespørsel.Host,
            forespørsel.PathBase,
            forespørsel.Path,
            forespørsel.QueryString);

    /// <summary>
    /// Plattformens innloggingsendepunkt. Samme URL en vanlig Altinn-app sender brukeren til.
    /// </summary>
    public static string ByggInnloggingsUrl(string plattform, string returUrl) =>
        $"{plattform.TrimEnd('/')}/authentication/api/v1/authentication?goto={Uri.EscapeDataString(returUrl)}";

    /// <summary>
    /// Siden brukeren får når innloggingen gikk bra hos Altinn, men vi ikke godtar sesjonen.
    /// Nevner plattformen vi validerer mot, fordi det i praksis alltid er den som er feil.
    /// </summary>
    public static string Feilside(string plattform) =>
        $$"""
        <!doctype html>
        <html lang="nb">
        <head><meta charset="utf-8" /><title>Innlogget, men sesjonen ble ikke godtatt</title>
        <style>body{font-family:system-ui,sans-serif;margin:3rem auto;max-width:38rem;line-height:1.5;padding:0 1rem}
        code{background:#eee;padding:.1rem .3rem;border-radius:3px}</style></head>
        <body>
        <h1>Innlogget, men sesjonen ble ikke godtatt</h1>
        <p>Du ble logget inn hos Altinn, men Regel-IDE godtok ikke sesjonscookien. Vi sender deg ikke
        tilbake til innloggingen igjen, siden det bare ville gitt en evig runddans.</p>
        <p>Appen validerer runtime-cookien mot <code>{{WebUtility.HtmlEncode(plattform)}}</code>.
        Hvert Altinn-miljø signerer med sin egen nøkkel, så peker denne på et annet miljø enn appen
        kjører i, blir en gyldig cookie avvist. Sjekk <code>RegelIde__Altinn__Plattform</code>.</p>
        </body></html>
        """;

    /// <summary>
    /// Registrerer middlewaren. Må ligge etter <c>UseAuthentication</c>, ellers er
    /// <c>HttpContext.User</c> alltid tom og alt blir redirectet.
    /// </summary>
    public static IApplicationBuilder BrukAltinninnlogging(
        this IApplicationBuilder app, Altinninnstillinger innstillinger, bool bakEnTerminerendeProxy)
    {
        var cookievalg = new CookieOptions
        {
            HttpOnly = true,
            Secure = bakEnTerminerendeProxy,
            // Lax, ikke Strict: cookien må følge med når plattformen sender brukeren tilbake hit,
            // og det er en navigasjon fra et annet nettsted. Strict ville droppet den, og markøren
            // ville aldri virket.
            SameSite = SameSiteMode.Lax,
            MaxAge = ForsokLevetid,
        };

        return app.Use(async (kontekst, neste) =>
        {
            if (kontekst.User.Identity?.IsAuthenticated is true)
            {
                if (kontekst.Request.Cookies.ContainsKey(ForsokCookie))
                {
                    kontekst.Response.Cookies.Delete(ForsokCookie);
                }
                await neste();
                return;
            }

            if (!ErNettlesernavigasjon(kontekst.Request))
            {
                await neste();
                return;
            }

            if (kontekst.Request.Cookies.ContainsKey(ForsokCookie))
            {
                kontekst.Response.Cookies.Delete(ForsokCookie);
                kontekst.Response.StatusCode = StatusCodes.Status401Unauthorized;
                kontekst.Response.ContentType = "text/html; charset=utf-8";
                await kontekst.Response.WriteAsync(Feilside(innstillinger.Plattform), Encoding.UTF8);
                return;
            }

            kontekst.Response.Cookies.Append(ForsokCookie, "1", cookievalg);
            kontekst.Response.Redirect(ByggInnloggingsUrl(
                innstillinger.Plattform,
                ByggReturUrl(kontekst.Request, bakEnTerminerendeProxy)));
        });
    }
}
