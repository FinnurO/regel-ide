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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
}

var rettskilder = app.MapGroup("/api/rettskilder").WithOpenApi();

rettskilder.MapGet("/", async (RettskildeRepository repo) =>
        (await repo.AlleRettskilderAsync()).Select(RettskildeSammendrag.FraEntitet))
    .WithName("HentAlleRettskilder")
    .WithSummary("Lister alle importerte primære rettskilder (sammendrag).");

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

app.Run();

public partial class Program; // synlig for WebApplicationFactory<Program> i integrasjonstester
