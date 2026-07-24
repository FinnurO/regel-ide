using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Api;
using RegelIde.Data;
using RegelIde.Kildekonvertering;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var connString = builder.Configuration.GetConnectionString("RegelIdeDb")
    ?? "Host=localhost;Port=5432;Database=regelide;Username=postgres;Password=postgres";
builder.Services.AddDbContext<RegelIdeDbContext>(o => o.UseNpgsql(connString));
builder.Services.AddScoped<RettskildeRepository>();
builder.Services.AddScoped<RettskildeImportTjeneste>();
builder.Services.AddHttpClient<LovdataBulkHenter>();

const string VitePolicy = "ViteDevServer";
builder.Services.AddCors(o => o.AddPolicy(VitePolicy, p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyMethod()
    .AllowAnyHeader()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(VitePolicy);
app.UseHttpsRedirection();

// Migrer og førstegangs-sås de kjente fixture-dokumentene hvis basen er tom — kun en utviklings-
// bekvemmelighet ("virker rett ut av boksen"), ikke en generell import-mekanisme. Ekte import skjer
// via egne endepunkter/verktøy når byggesteg 1s importfunksjon (kap. 3.3 i produktkrav) bygges videre.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RegelIdeDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.Rettskilder.AnyAsync())
    {
        var kildemappe = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "data", "kilder", "raw-lovdata"));
        if (Directory.Exists(kildemappe))
        {
            var importer = scope.ServiceProvider.GetRequiredService<RettskildeImportTjeneste>();
            foreach (var fil in Directory.EnumerateFiles(kildemappe, "*.html").OrderBy(f => f))
            {
                var resultat = LovdataKonverterer.Konverter(File.ReadAllText(fil));
                await importer.ImporterAsync(resultat);
            }
        }
    }

    // Enkel testbruker-seeding (2026-07-24) — IKKE ekte autentisering. Erstattes av Ansattporten-
    // innlogging senere; se Bruker-kommentaren i RegelIde.Data/Entiteter.cs.
    if (!await db.Brukere.AnyAsync())
    {
        var testkommunen = new Virksomhet { Id = Guid.NewGuid(), Navn = "Testkommunen" };
        db.Virksomheter.Add(testkommunen);
        db.Brukere.AddRange(
            new Bruker { Id = Guid.NewGuid(), Navn = "Kari Jurist", VirksomhetId = testkommunen.Id, Rolle = "Jurist" },
            new Bruker { Id = Guid.NewGuid(), Navn = "Ola Fagansvarlig", VirksomhetId = testkommunen.Id, Rolle = "Fagansvarlig" },
            new Bruker { Id = Guid.NewGuid(), Navn = "Per Saksbehandler", VirksomhetId = testkommunen.Id, Rolle = "Saksbehandler" },
            new Bruker { Id = Guid.NewGuid(), Navn = "Anne Systemforvalter", VirksomhetId = testkommunen.Id, Rolle = "Systemforvalter" });
        await db.SaveChangesAsync();
    }
}

app.MapGet("/api/brukere", async (RegelIdeDbContext db) =>
    {
        var brukere = await db.Brukere.Join(db.Virksomheter, b => b.VirksomhetId, v => v.Id,
                (b, v) => new BrukerDto(b.Id, b.Navn, v.Id, v.Navn, b.Rolle))
            .ToListAsync();
        return Results.Ok(brukere);
    })
    .WithOpenApi()
    .WithName("HentBrukere")
    .WithSummary("Lister testbrukere (IKKE ekte autentisering, se GjeldendeBrukerTjeneste) for GUI-ets brukervelger.");

app.MapGet("/api/virksomheter", async (RegelIdeDbContext db) =>
        (await db.Virksomheter.ToListAsync()).Select(v => new VirksomhetDto(v.Id, v.Navn, v.Organisasjonsnummer)))
    .WithOpenApi()
    .WithName("HentVirksomheter")
    .WithSummary("Lister virksomheter.");

var rettskilder = app.MapGroup("/api/rettskilder").WithOpenApi();

rettskilder.MapGet("/", async (Guid? virksomhetId, RettskildeRepository repo) =>
        (await repo.AlleRettskilderAsync(virksomhetId)).Select(RettskildeSammendrag.FraEntitet))
    .WithName("HentAlleRettskilder")
    .WithSummary("Lister rettskilder (åpne data — kun Status != 'Utkast'). " +
        "?virksomhetId snevrer inn til én virksomhets bidrag; utelatt viser alt (delt + alle virksomheter).");

rettskilder.MapGet("/{id:guid}", async (Guid id, RettskildeRepository repo) =>
    {
        var r = await repo.FinnAsync(id);
        return r is null
            ? Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." })
            : Results.Ok(RettskildeDetalj.FraEntitet(r));
    })
    .WithName("HentRettskilde")
    .WithSummary("Henter full metadata + kanonisk AKN-XML for én rettskilde.");

rettskilder.MapGet("/{id:guid}/noder", async (Guid id, RettskildeRepository repo) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." });
        var noder = await repo.NoderForAsync(id);
        return Results.Ok(noder.Select(RettskildeNodeDto.FraEntitet));
    })
    .WithName("HentRettskildeNoder")
    .WithSummary("Henter hele nodetreet (flat liste, eId+parentNodeId) for tre-navigasjon.");

// eId gis som query-parameter, ikke rutesegment — en eId er en full ELI-URI ("https://…/§1-1/ledd-1")
// med både "://" og flere skråstreker, som er upraktisk/tvetydig i selve URL-stien.
rettskilder.MapGet("/{id:guid}/noder/oppslag", async (Guid id, string eid, RettskildeRepository repo) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." });
        var node = await repo.FinnNodeAsync(id, eid);
        return node is null
            ? Results.NotFound(new { feil = $"Ingen node med eId '{eid}' i rettskilde '{id}'." })
            : Results.Ok(RettskildeNodeDto.FraEntitet(node));
    })
    .WithName("HentRettskildeNode")
    .WithSummary("Henter én node (kapittel/underinndeling/paragraf/ledd/punkt) ved eId.");

rettskilder.MapGet("/{id:guid}/referanser", async (Guid id, RettskildeRepository repo) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." });
        var referanser = await repo.ReferanserForAsync(id);
        return Results.Ok(referanser.Select(RettskildeReferanseDto.FraEntitet));
    })
    .WithName("HentRettskildeReferanser")
    .WithSummary("Henter kryssreferansene funnet i løpeteksten (interne og eksterne).");

// ---------- Import (2026-07-24) — krever X-Bruker-Id for attribusjon, se GjeldendeBrukerTjeneste ----------

rettskilder.MapPost("/fil", async (HttpRequest request, IFormFile fil, Guid? virksomhetId,
        RettskildeImportTjeneste importer, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }

        using var leser = new StreamReader(fil.OpenReadStream(), System.Text.Encoding.UTF8);
        var html = await leser.ReadToEndAsync(ct);

        KonverteringResultat resultat;
        try
        {
            resultat = LovdataKonverterer.Konverter(html);
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException)
        {
            // §3.3: importen skal feile tydelig, ikke gjette — inkl. filer i et format parseren ikke
            // kjenner igjen (f.eks. Lovdatas nettside-HTML for lokale forskrifter, se src/README.md).
            return Results.BadRequest(new { feil = $"Kunne ikke tolke filen som Lovdata-HTML: {ex.Message}" });
        }

        var rettskildeId = await importer.ImporterAsync(resultat, virksomhetId, bruker.Navn, ct);
        return Results.Created($"/api/rettskilder/{rettskildeId}", new { id = rettskildeId });
    })
    .DisableAntiforgery()
    .WithName("ImporterFraFil")
    .WithSummary("Importerer en rettskilde fra en opplastet HTML-fil (Lovdatas \"XML-kompatible HTML\"-format). " +
        "?virksomhetId angir at dette er virksomhetens egen lokale kilde; utelatt = delt/nasjonal kilde.");

rettskilder.MapPost("/lovdata", async (HttpRequest request, LovdataImportRequest body,
        LovdataBulkHenter henter, RettskildeImportTjeneste importer, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }

        string html;
        try
        {
            html = await henter.HentRaaHtmlAsync(body.Datokode, ct);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }

        KonverteringResultat resultat;
        try
        {
            resultat = LovdataKonverterer.Konverter(html);
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException)
        {
            return Results.UnprocessableEntity(new { feil = $"Hentet fra Lovdata, men kunne ikke tolke innholdet: {ex.Message}" });
        }

        // Alltid delt/nasjonalt (virksomhetId=null) -- dette endepunktet henter kun fra Lovdatas
        // offisielle bulk-datasett, som per definisjon kun inneholder nasjonale Lov/Forskrift.
        var rettskildeId = await importer.ImporterAsync(resultat, virksomhetId: null, bruker.Navn, ct);
        return Results.Created($"/api/rettskilder/{rettskildeId}", new { id = rettskildeId });
    })
    .WithName("ImporterFraLovdata")
    .WithSummary("Henter og importerer en rettskilde fra Lovdatas offisielle bulk-datasett via datokode " +
        "(f.eks. \"LOV-1989-06-02-27\"). Alltid en delt/nasjonal kilde.");

app.Run();

public partial class Program; // synlig for WebApplicationFactory<Program> i integrasjonstester
