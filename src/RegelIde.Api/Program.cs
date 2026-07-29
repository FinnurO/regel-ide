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
builder.Services.AddScoped<TekstTaggTjeneste>();
builder.Services.AddScoped<HandbokForfatterTjeneste>();
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

    // Tag-kind-konfigurasjon (2026-07-25) — global, ikke virksomhets-scopet. Erstatter en tidligere
    // hardkodet liste; se TaggKindKonfigurasjonEntitet-kommentaren i RegelIde.Data/Entiteter.cs.
    if (!await db.TaggKindKonfigurasjoner.AnyAsync())
    {
        db.TaggKindKonfigurasjoner.AddRange(
            new TaggKindKonfigurasjonEntitet { Id = Guid.NewGuid(), Kode = "begrep", Navn = "Begrep", Farge = "accent", Sorteringsrekkefolge = 0 },
            new TaggKindKonfigurasjonEntitet { Id = Guid.NewGuid(), Kode = "tjeneste", Navn = "Tjeneste", Farge = "info", Sorteringsrekkefolge = 1 },
            new TaggKindKonfigurasjonEntitet { Id = Guid.NewGuid(), Kode = "vilkar", Navn = "Vilkår", Farge = "warning", Sorteringsrekkefolge = 2 },
            new TaggKindKonfigurasjonEntitet { Id = Guid.NewGuid(), Kode = "regel", Navn = "Regel", Farge = "success", Sorteringsrekkefolge = 3 });
        await db.SaveChangesAsync();
    }

    // Testkommunens egne lokale rettskilder (2026-07-29, docs/06-veikart.md) — idempotent, guardet
    // internt per rettskilde (ikke "!AnyAsync" på hele tabellen, siden dette kjører etter at
    // Lov/Forskrift kan være importert fra før). Se TestkommuneInnholdSeed.cs for proveniens.
    await TestkommuneInnholdSeed.SeedAsync(db);
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

app.MapGet("/api/konfigurasjon/tagg-kinds", async (RegelIdeDbContext db) =>
        (await db.TaggKindKonfigurasjoner.Where(k => k.Aktiv).OrderBy(k => k.Sorteringsrekkefolge).ToListAsync())
            .Select(TaggKindKonfigurasjonDto.FraEntitet))
    .WithOpenApi()
    .WithName("HentTaggKindKonfigurasjon")
    .WithSummary("Lister aktive tag-kinds (2026-07-25, erstatter en tidligere hardkodet liste i frontend/backend).");

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

rettskilder.MapPatch("/{id:guid}/metadata", async (Guid id, HttpRequest request, OppdaterRettskildeMetadataRequest body,
        RettskildeRepository repo, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        var oppdatert = await repo.OppdaterMetadataAsync(id, body.Kortnavn, body.Utgiver, bruker.Navn);
        return oppdatert is null
            ? Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." })
            : Results.Ok(RettskildeDetalj.FraEntitet(oppdatert));
    })
    .WithName("OppdaterRettskildeMetadata")
    .WithSummary("Oppdaterer Kortnavn/Utgiver etter import — AK-3.3.6, bekreftelsessteget i Importer.tsx.");

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

// ---------- Tekst-tagging (2026-07-24, AK-3.3.1–3.3.4) — krever X-Bruker-Id, tagger er alltid ----------
// ---------- virksomhetens eget arbeidsprodukt (§0.1 i domenemodellen), aldri delt på tvers.     ----------

rettskilder.MapGet("/{id:guid}/tagger", async (Guid id, HttpRequest request, RettskildeRepository repo,
        TekstTaggTjeneste taggTjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." });

        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }

        var egneTagger = await taggTjeneste.ListerForAsync(id, bruker.VirksomhetId, ct);
        return Results.Ok(egneTagger.Select(TekstTaggDto.FraEntitet));
    })
    .WithName("HentTekstTagger")
    .WithSummary("Lister virksomhetens egne tagger for denne rettskilden (ikke delt på tvers av virksomheter).");

rettskilder.MapPost("/{id:guid}/tagger", async (Guid id, HttpRequest request, OpprettTekstTaggRequest body,
        RettskildeRepository repo, TekstTaggTjeneste taggTjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." });

        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }

        TekstTaggEntitet? opprettet;
        try
        {
            opprettet = await taggTjeneste.OpprettAsync(id, bruker.VirksomhetId, bruker.Navn, body.NodeEid,
                body.StartOffset, body.EndOffset, body.QuotePrefix, body.QuoteExact, body.QuoteSuffix, body.Kind, ct);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }

        if (opprettet is null)
        {
            return Results.NotFound(new { feil = $"Ingen node med eId '{body.NodeEid}' i rettskilde '{id}'." });
        }

        return Results.Created($"/api/rettskilder/{id}/tagger/{opprettet.Id}", TekstTaggDto.FraEntitet(opprettet));
    })
    .WithName("OpprettTekstTagg")
    .WithSummary("Oppretter en ny tekst-tag (AK-3.3.1–3.3.2). ref_id er alltid null i byggesteg 1 (se docs/06-veikart.md).");

rettskilder.MapDelete("/{id:guid}/tagger/{taggId:guid}", async (Guid id, Guid taggId, HttpRequest request,
        TekstTaggTjeneste taggTjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }

        var resultat = await taggTjeneste.SlettAsync(id, taggId, bruker.VirksomhetId, bruker.Navn, ct);
        return resultat switch
        {
            SlettResultat.Ok => Results.NoContent(),
            SlettResultat.IkkeFunnet => Results.NotFound(new { feil = $"Ingen tagg med id '{taggId}' på rettskilde '{id}'." }),
            SlettResultat.TilhorerAnnenVirksomhet => Results.Json(new { feil = "Taggen tilhører en annen virksomhet." }, statusCode: 403),
            SlettResultat.HarPublisertReferanse => Results.Conflict(new { feil = "Taggen har en publisert referanse og kan ikke fjernes (AK-3.3.4)." }),
            _ => throw new InvalidOperationException($"Ukjent SlettResultat '{resultat}'."),
        };
    })
    .WithName("SlettTekstTagg")
    .WithSummary("Fjerner (arkiverer) en egendefinert tagg — AK-3.3.4.");

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

// ---------- Håndbok/rundskriv-forfatterflyt (2026-07-26, docs/03-domenemodell.md §1.1.1) ----------
// ---------- krever X-Bruker-Id for attribusjon, samme mønster som import/tagging over.       ----------

var handboker = app.MapGroup("/api/handboker").WithOpenApi();

handboker.MapPost("/", async (HttpRequest request, OpprettHandbokRequest body,
        HandbokForfatterTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            var handbok = await tjeneste.OpprettHandbokAsync(body.Tittel, bruker.VirksomhetId, bruker.Navn, ct: ct);
            return Results.Created($"/api/rettskilder/{handbok.Id}", new { id = handbok.Id });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettHandbok")
    .WithSummary("Oppretter en ny håndbok/rundskriv (AK-3.3.8) — kildetype='Rundskriv', ingen importpipeline.");

handboker.MapPost("/{id:guid}/kapitler", async (Guid id, HttpRequest request, OpprettKapittelNodeRequest body,
        HandbokForfatterTjeneste tjeneste, RettskildeRepository repo, RegelIdeDbContext db, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen håndbok med id '{id}'." });
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            var node = await tjeneste.OpprettKapittelNodeAsync(id, body.ParentNodeId, body.Nummer, body.Overskrift, bruker.Navn, ct);
            return Results.Created($"/api/rettskilder/{id}/noder/oppslag?eid={Uri.EscapeDataString(node.Eid)}", RettskildeNodeDto.FraEntitet(node));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettHandbokKapittel")
    .WithSummary("Oppretter en kapittel-/underinndelingsnode i håndbokens eget tre.");

handboker.MapPost("/{id:guid}/kommentarer", async (Guid id, HttpRequest request, OpprettKommentarNodeRequest body,
        HandbokForfatterTjeneste tjeneste, RettskildeRepository repo, RegelIdeDbContext db, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen håndbok med id '{id}'." });
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            var resultat = await tjeneste.OpprettKommentarNodeAsync(id, body.ParentNodeId, body.Nummer, body.Overskrift,
                body.TekstHtml, body.Dokumenttype, body.FesteNiva, body.Marginord, bruker.Navn, ct);
            var dto = RettskildeNodeDto.FraEntitet(resultat.Node) with { HandbokMetadata = HandbokKommentarMetadataDto.FraEntitet(resultat.Metadata) };
            return Results.Created($"/api/rettskilder/{id}/noder/oppslag?eid={Uri.EscapeDataString(resultat.Node.Eid)}", dto);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettHandbokKommentar")
    .WithSummary("Oppretter en kommentarseksjon, versjon 1 (AK-3.3.8/3.3.11). Tekst saneres server-side.");

handboker.MapPut("/{id:guid}/kommentarer/{nodeId:guid}", async (Guid id, Guid nodeId, HttpRequest request, RedigerKommentarNodeRequest body,
        HandbokForfatterTjeneste tjeneste, RettskildeRepository repo, RegelIdeDbContext db, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen håndbok med id '{id}'." });
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            var resultat = await tjeneste.RedigerKommentarNodeAsync(nodeId, body.TekstHtml, body.Overskrift,
                body.Dokumenttype, body.FesteNiva, body.Marginord, bruker.Navn, ct);
            var dto = RettskildeNodeDto.FraEntitet(resultat.Node) with { HandbokMetadata = HandbokKommentarMetadataDto.FraEntitet(resultat.Metadata) };
            return Results.Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("RedigerHandbokKommentar")
    .WithSummary("Redigerer en kommentarseksjon — oppretter alltid en ny node-versjon (AK-3.3.10), aldri in-place.");

// eid som query-parameter, ikke rutesegment — samme begrunnelse som /{id}/noder/oppslag over.
handboker.MapGet("/{id:guid}/kommentarer/versjoner", async (Guid id, string eid,
        HandbokForfatterTjeneste tjeneste, RettskildeRepository repo, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen håndbok med id '{id}'." });
        var versjoner = await tjeneste.HentVersjonshistorikkAsync(id, eid, ct);
        return Results.Ok(versjoner.Select(RettskildeNodeDto.FraEntitet));
    })
    .WithName("HentHandbokKommentarVersjoner")
    .WithSummary("Lister alle versjoner av en kommentarseksjon, nyeste først — AK-3.3.10 \"Se tidligere versjoner\".");

handboker.MapPost("/{id:guid}/kommentarer/{nodeId:guid}/lovreferanser", async (Guid id, Guid nodeId, KobleLovreferanseRequest body,
        HandbokForfatterTjeneste tjeneste, RettskildeRepository repo, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen håndbok med id '{id}'." });
        try
        {
            var referanse = await tjeneste.KobleLovreferanseAsync(nodeId, body.TilRettskildeId, body.TilEid, ct);
            return Results.Created($"/api/rettskilder/{id}/referanser", RettskildeReferanseDto.FraEntitet(referanse));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("KobleHandbokLovreferanse")
    .WithSummary("Kobler en kommentarseksjon til én paragraf i en Lov/Forskrift (AK-3.3.9) — samme mekanisme som interne kryssreferanser.");

handboker.MapDelete("/{id:guid}/kommentarer/{nodeId:guid}/lovreferanser/{referanseId:guid}", async (Guid referanseId,
        HandbokForfatterTjeneste tjeneste, CancellationToken ct) =>
        await tjeneste.FjernLovreferanseAsync(referanseId, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen lovreferanse med id '{referanseId}'." }))
    .WithName("FjernHandbokLovreferanse")
    .WithSummary("Fjerner en lovreferanse-kobling fra en kommentarseksjon.");

handboker.MapPost("/{id:guid}/kommentarer/{nodeId:guid}/revisjonsmerke", async (Guid id, Guid nodeId, HttpRequest request, SettRevisjonsmerkeRequest body,
        HandbokForfatterTjeneste tjeneste, RettskildeRepository repo, RegelIdeDbContext db, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen håndbok med id '{id}'." });
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            await tjeneste.SettRevisjonsmerkeAsync(nodeId, body.Revisjonsgrunn, bruker.Navn, ct);
            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettHandbokRevisjonsmerke")
    .WithSummary("Manuell «Må revideres»-merking (AK-3.3.12, v1-forenkling — ikke automatisk påvirkningsanalyse).");

handboker.MapPost("/{id:guid}/kommentarer/{nodeId:guid}/publiser", async (Guid id, Guid nodeId, HttpRequest request, PubliserKommentarRequest body,
        HandbokForfatterTjeneste tjeneste, RettskildeRepository repo, RegelIdeDbContext db, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen håndbok med id '{id}'." });
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            await tjeneste.PubliserKommentarAsync(nodeId, body.GodkjentAv, bruker.Navn, ct);
            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("PubliserHandbokKommentar")
    .WithSummary("Publiserer en kommentarseksjon. Bindende seksjoner krever registrert godkjenner (AK-3.3.11).");

app.Run();

public partial class Program; // synlig for WebApplicationFactory<Program> i integrasjonstester
