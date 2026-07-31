using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace RegelIde.Api.Autentisering;

/// <summary>Hvor identiteten kommer fra. Velges med konfigurasjonsnøkkelen <c>RegelIde:Autentisering</c>.</summary>
public enum Autentiseringsprofil
{
    /// <summary>
    /// Standard. <c>X-Bruker-Id</c>-header + brukervelgeren i GUI-et, altså dagens oppførsel.
    /// IKKE autentisering — hvem som helst kan sende hvilken som helst bruker-id. Finnes for
    /// lokal utvikling og tester, og er grunnen til at profilen må settes bevisst i drift.
    /// </summary>
    Testbruker,

    /// <summary>
    /// Altinn-innloggingen: <c>AltinnStudioRuntime</c>-cookien validert mot plattformens JWKS.
    /// Forutsetter at vi kjører på et subdomene av altinn.no (app-clusteret), ellers følger
    /// ikke cookien med. Se docs/autentisering.md.
    /// </summary>
    Altinn,
}

/// <summary>
/// Ett sted å velge hvor "hvem skriver" kommer fra, etter samme mønster som
/// <see cref="Data.Databaseoppsett"/>. Poenget er at resten av API-et ikke skal vite
/// forskjell: alle kallsteder spør <see cref="IBrukerkontekst"/> og får en
/// <see cref="Data.Bruker"/> tilbake, uansett profil.
/// </summary>
public static class Autentiseringsoppsett
{
    public const string Konfigurasjonsnokkel = "RegelIde:Autentisering";

    public static Autentiseringsprofil LesProfil(IConfiguration konfigurasjon) =>
        (konfigurasjon[Konfigurasjonsnokkel] ?? "testbruker").Trim().ToLowerInvariant() switch
        {
            "testbruker" => Autentiseringsprofil.Testbruker,
            "altinn" => Autentiseringsprofil.Altinn,
            var ukjent => throw new InvalidOperationException(
                $"Ukjent {Konfigurasjonsnokkel}='{ukjent}'. Gyldige verdier: testbruker | altinn."),
        };

    public static IServiceCollection LeggTilRegelIdeAutentisering(
        this IServiceCollection tjenester, IConfiguration konfigurasjon)
    {
        if (LesProfil(konfigurasjon) is Autentiseringsprofil.Testbruker)
        {
            return tjenester.AddScoped<IBrukerkontekst, TestbrukerKontekst>();
        }

        var innstillinger = Altinninnstillinger.Les(konfigurasjon);
        tjenester.AddSingleton(innstillinger);
        tjenester.AddScoped<IBrukerkontekst, AltinnBrukerkontekst>();
        tjenester.AddSingleton<IAltinnRolleoppslag>(new KonfigurertRolleoppslag(innstillinger.DaglIdentifikatorer));

        // Cookien er et vanlig JWT — den ligger bare ikke i Authorization-headeren. JwtBearer gjør
        // signatur-, utsteder- og utløpsvalidering mot plattformens JWKS, som er nøyaktig samme
        // kontroll en Altinn-app gjør (se OpenIdWellKnownEndpoint i skall-appens appsettings.json).
        tjenester
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MetadataAddress = innstillinger.VelkjentEndepunkt;
                o.TokenValidationParameters.ValidateIssuerSigningKey = true;
                o.TokenValidationParameters.ValidateLifetime = true;
                // Plattformtokenet har ingen audience vi kan feste oss til — det er utstedt til
                // Altinn selv, ikke til oss. Utsteder + signatur er kontrollen som gjelder.
                o.TokenValidationParameters.ValidateAudience = false;
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = kontekst =>
                    {
                        kontekst.Token = kontekst.Request.Cookies[innstillinger.Cookienavn];
                        return Task.CompletedTask;
                    },
                };
            });

        return tjenester.AddAuthorization();
    }
}
