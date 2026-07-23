using System.Text.Json.Serialization;
using RegelIde.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<RettskildeRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var rettskilder = app.MapGroup("/api/rettskilder").WithOpenApi();

rettskilder.MapGet("/", (RettskildeRepository repo) =>
        repo.AlleRettskilder().Select(r => RettskildeSammendrag.FraMetadata(r.Metadata)))
    .WithName("HentAlleRettskilder")
    .WithSummary("Lister alle importerte rettskilder (sammendrag).");

rettskilder.MapGet("/{datokode}", (string datokode, RettskildeRepository repo) =>
    {
        var resultat = repo.FinnVedDatokode(datokode);
        return resultat is null
            ? Results.NotFound(new { feil = $"Ingen rettskilde med datokode '{datokode}'." })
            : Results.Ok(RettskildeDetalj.FraResultat(resultat));
    })
    .WithName("HentRettskilde")
    .WithSummary("Henter full metadata + kanonisk AKN-XML for én rettskilde.");

rettskilder.MapGet("/{datokode}/noder", (string datokode, RettskildeRepository repo) =>
    {
        var resultat = repo.FinnVedDatokode(datokode);
        return resultat is null
            ? Results.NotFound(new { feil = $"Ingen rettskilde med datokode '{datokode}'." })
            : Results.Ok(resultat.Noder);
    })
    .WithName("HentRettskildeNoder")
    .WithSummary("Henter hele nodetreet (flat liste, eId+parentEid) for tre-navigasjon.");

// eId gis som query-parameter, ikke rutesegment — en eId er en full ELI-URI ("https://…/§1-1/ledd-1")
// med både "://" og flere skråstreker, som er upraktisk/tvetydig i selve URL-stien.
rettskilder.MapGet("/{datokode}/noder/oppslag", (string datokode, string eid, RettskildeRepository repo) =>
    {
        var resultat = repo.FinnVedDatokode(datokode);
        if (resultat is null) return Results.NotFound(new { feil = $"Ingen rettskilde med datokode '{datokode}'." });

        var node = resultat.Noder.FirstOrDefault(n => n.Eid == eid);
        return node is null
            ? Results.NotFound(new { feil = $"Ingen node med eId '{eid}' i '{datokode}'." })
            : Results.Ok(node);
    })
    .WithName("HentRettskildeNode")
    .WithSummary("Henter én node (kapittel/underinndeling/paragraf/ledd/punkt) ved eId.");

rettskilder.MapGet("/{datokode}/referanser", (string datokode, RettskildeRepository repo) =>
    {
        var resultat = repo.FinnVedDatokode(datokode);
        return resultat is null
            ? Results.NotFound(new { feil = $"Ingen rettskilde med datokode '{datokode}'." })
            : Results.Ok(resultat.Referanser);
    })
    .WithName("HentRettskildeReferanser")
    .WithSummary("Henter kryssreferansene funnet i løpeteksten (interne og eksterne).");

app.Run();

public partial class Program; // synlig for WebApplicationFactory<Program> i integrasjonstester
