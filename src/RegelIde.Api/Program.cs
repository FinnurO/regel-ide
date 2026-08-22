using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Api;
using RegelIde.Api.Autentisering;
using RegelIde.Data;
using RegelIde.Kildekonvertering;

var builder = WebApplication.CreateBuilder(args);

// Miljøspesifikke verdier — hvilke Altinn-brukere som er DAGL, hvilken syntetisk organisasjon
// vi kjører mot — hører ikke hjemme i git. De er flyktige og betyr ingenting i et annet miljø.
// Lokalt legges de i appsettings.Local.json (gitignorert, se appsettings.Local.example.json);
// i drift settes de som miljøvariabler. Se docs/autentisering.md.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Filen legges sist i kjeden og ville ellers overstyrt både miljøvariabler og kommandolinje.
// Vi legger dem inn på nytt, slik at den vanlige rekkefølgen står: fil < miljø < kommandolinje.
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Postgres som standard; SQLite kun når RegelIde:Database sier det (deploy-profilen, se
// docker/README.md). Se Databaseoppsett.cs for hva som faktisk skiller de to.
builder.Services.LeggTilRegelIdeDatabase(builder.Configuration);

// Testbruker-header som standard; Altinn-cookie kun når RegelIde:Autentisering sier det.
// Se Autentiseringsoppsett.cs og docs/autentisering.md.
builder.Services.LeggTilRegelIdeAutentisering(builder.Configuration);
builder.Services.AddScoped<RettskildeRepository>();
builder.Services.AddScoped<VeiledningRepository>();
builder.Services.AddScoped<RettskildeImportTjeneste>();
builder.Services.AddScoped<TekstTaggTjeneste>();
builder.Services.AddScoped<HandbokForfatterTjeneste>();
builder.Services.AddScoped<TjenesteregisterTjeneste>();
builder.Services.AddScoped<BegrepsregisterTjeneste>();
builder.Services.AddScoped<BrukerregisterTjeneste>();
builder.Services.AddScoped<KodelisteregisterTjeneste>();
builder.Services.AddScoped<VirksomhetsbegrepTjeneste>();
builder.Services.AddScoped<MyndighetstildelingTjeneste>();
builder.Services.AddScoped<VirksomhetKandidatTjeneste>();
builder.Services.AddScoped<VilkarregisterTjeneste>();
builder.Services.AddScoped<RegelnoderegisterTjeneste>();
builder.Services.AddScoped<UnntaksregisterTjeneste>();
builder.Services.AddScoped<DatasettregisterTjeneste>();
builder.Services.AddScoped<VilkarstreKommentarTjeneste>();
builder.Services.AddScoped<HendelseregisterTjeneste>();
builder.Services.AddScoped<TjenesteavhengighetregisterTjeneste>();
builder.Services.AddScoped<HandlingregisterTjeneste>();
builder.Services.AddScoped<TjenesteEksportTjeneste>();
builder.Services.AddScoped<RettighetModellEksportTjeneste>();
builder.Services.AddScoped<KunnskapsbibliotekTjeneste>();
// "Stub" (default) eller "OpenAiKompatibel" — se docs/14-byggesteg5-teknisk-design.md. Bytte krever
// restart av API-et; å gjøre dette velgbart fra en admin-side i appen er en senere, avgrenset
// utvidelse (flytt valget til en DB-lagret innstilling + en dispatcher-IKiAgentKlient).
// "OpenAiKompatibel" er ikke bundet til én leverandør — BaseUrl/Modell/ApiKey er alle konfig
// (RegelIde:KiAgent:*), så samme klasse fungerer mot HostYourAI, OpenRouter, eller noe helt annet.
if (builder.Configuration["RegelIde:KiAgent:Leverandor"] == "OpenAiKompatibel")
{
    builder.Services.AddHttpClient<IKiAgentKlient, KiAgentKlientOpenAiKompatibel>();
}
else
{
    builder.Services.AddScoped<IKiAgentKlient, KiAgentKlientStub>();
}
builder.Services.AddScoped<BegrepsforslagTjeneste>();
// Byggesteg 5 runde 4 (RAG-spike) — samme "Stub eller OpenAiKompatibel"-mønster som IKiAgentKlient
// over, men egen konfig (RegelIde:KiAgent:EmbeddingBaseUrl/EmbeddingModell) siden en leverandør
// typisk har separate URL-er/modellnavn for chat-completions og embeddings. Om HostYourAI faktisk
// tilbyr embeddings er ubekreftet (se docs/13-backlog.md) — Stub-fallback lar API-et starte og
// TjenesteforslagTjeneste.KjorForslagMedRagAsync kjøre uten en ekte leverandør konfigurert.
if (builder.Configuration["RegelIde:KiAgent:EmbeddingBaseUrl"] is not null)
{
    builder.Services.AddHttpClient<IEmbeddingKlient, EmbeddingKlientOpenAiKompatibel>();
}
else
{
    builder.Services.AddScoped<IEmbeddingKlient, EmbeddingKlientStub>();
}
builder.Services.AddScoped<RettskildeEmbeddingTjeneste>();
builder.Services.AddScoped<TjenesteforslagTjeneste>();
builder.Services.AddHttpClient<LovdataBulkHenter>();
builder.Services.AddScoped<LovdataKatalogTjeneste>();
builder.Services.AddScoped<LovdataFullimportTjeneste>();
builder.Services.AddScoped<LovdataImportstatusTjeneste>();
// Full Lovdata-synkronisering ved oppstart (docs/13-backlog.md §6) — se klassekommentaren for
// hvorfor dette er en BackgroundService og ikke et synkront steg i oppstartsblokken under.
builder.Services.AddHostedService<LovdataFullimportBakgrunnstjeneste>();
builder.Services.AddHttpClient<OppgaveregisterHenter>();
builder.Services.AddHttpClient<AltinnRessursHenter>();
// info.altinn.no returnerer 403 uten en nettleserlignende User-Agent (bekreftet ved live-verifisering
// av testfixturene i src/RegelIde.Data.Tests/Testdata/AltinnHosting/, samme header Johanns
// referanseskript setter) — data.brreg.no/tjenesteoversikten.no over krever ingen tilsvarende header.
builder.Services.AddHttpClient<AltinnSkjemaoversiktHenter>(c =>
    // EKTE FUNN 2026-08-14 (ved live-verifisering, ikke fanget av testsuiten siden den stubber
    // HttpMessageHandler og dermed hopper over ekte header-validering): "høster" inneholder "ø", en
    // ikke-ASCII-karakter. HttpClient krever ASCII-only header-verdier og kaster
    // HttpRequestException på HELT FØRSTE kall — hele høstingen feilet 100 % av tiden. "hoster" (uten
    // ø) løser det uten å endre meningen.
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; RegelIde-hoster/1.0; +https://github.com/FinnurO/regel-ide)"));
// Fil-basert, ikke URL-basert (se TjenestelisteImporter.cs klassekommentar) — ingen HttpClient å registrere.
// Delt av begge fil-baserte kildene (Statsforvalter-tjenester + fylkeskommune-dialogtjenester).
builder.Services.AddScoped<TjenestelisteImporter>();
// Sjette kilde i høstelaget — også fil-basert, men strukturelt ulik (nestet-per-kommune, ikke et bart
// tjeneste-array) nok til å ha fått sin egen klasse i stedet for en tredje TjenestelisteImporter-kildetype,
// se KommuneTjenesteHenter.cs klassekommentar.
builder.Services.AddScoped<KommuneTjenesteHenter>();

const string VitePolicy = "ViteDevServer";
builder.Services.AddCors(o => o.AddPolicy(VitePolicy, p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyMethod()
    .AllowAnyHeader()));

var app = builder.Build();

// Altinns app-cluster serverer appen under /{org}/{app}/, og ingressen stripper IKKE prefikset —
// appen forventes å håndtere det selv. UsePathBase fjerner det fra stien før ruting, slik at
// resten av pipelinen kan fortsette å tenke i «/api/...» og «/helse». Må ligge først; en
// middleware registrert før denne ser fortsatt hele stien.
// Tom verdi ⇒ ingen effekt, så lokal kjøring er uendret. Se docs/deploy-altinn-app-cluster.md.
var stiprefiks = Stiprefiks.Les(app.Configuration);
if (stiprefiks is not null)
{
    app.UsePathBase(stiprefiks);

    // Det eksplisitte UseRouting-kallet er nødvendig, ikke pynt. WebApplication setter inn sitt
    // eget UseRouting FØRST i pipelinen når man ikke kaller det selv — da har rutingen allerede
    // matchet på full sti, og UsePathBase over kommer for sent. Symptomet er lumsk: endepunktene
    // svarer fortsatt på rot, mens alt under prefikset faller til SPA-fallbacken og gir 200 med
    // text/html. Kaller vi UseRouting her, brukes denne posisjonen i stedet.
    app.UseRouting();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(VitePolicy);

// I containeren (docker/Dockerfile) serveres API og SPA fra samme origin over ren HTTP —
// TLS termineres av ingress foran oss. UseHttpsRedirection ville da bare logge en advarsel
// om at den ikke finner noen HTTPS-port. Utenfor container er oppførselen som før.
var bakEnTerminerendeProxy = app.Configuration.GetValue("RegelIde:BakEnTerminerendeProxy", false);
if (!bakEnTerminerendeProxy)
{
    app.UseHttpsRedirection();
}

// Serverer den ferdigbygde SPA-en hvis den er lagt inn ved siden av API-et (wwwroot).
// Tom mappe utenfor container ⇒ ingen effekt, og `vite dev` brukes som før.
//
// Merk at UseDefaultFiles er bevisst IKKE med: den ville sendt «/» til index.html på disk, som
// serveres rått av UseStaticFiles og dermed uten omskrevet <base href>. Forsiden ville altså
// fungert lokalt og vært knekt under et sti-prefiks. «/» håndteres i stedet av MapFallback nederst.
app.UseStaticFiles();

// No-op under testbruker-profilen (ingen skjemaer registrert), så rekkefølgen er den samme uansett.
var autentiseringsprofil = Autentiseringsoppsett.LesProfil(app.Configuration);
if (autentiseringsprofil is Autentiseringsprofil.Altinn)
{
    app.UseAuthentication();
    app.UseAuthorization();

    // Uten denne ber ingenting brukeren om å logge inn: JwtBearer validerer cookien hvis den er
    // der og går videre uten identitet hvis den ikke er der. SPA-en lastet derfor fint for en
    // utlogget bruker og feilet først på første API-kall. Må ligge etter UseAuthentication —
    // ellers er User alltid tom og alt blir redirectet. Se Altinninnlogging.cs.
    app.BrukAltinninnlogging(app.Services.GetRequiredService<Altinninnstillinger>(), bakEnTerminerendeProxy);
}

// Enkel liveness/readiness for klyngen: svarer 200 først når databasen faktisk svarer.
// Altinns app-Helm-chart har hardkodet probe-sti /health, og den er ikke konfigurerbar i
// values.yaml. Uten dette aliaset ville /health truffet SPA-fallbacken under og svart 200
// text/html uansett — probene ville altså rapportert klar også med død database, som er verre
// enn å feile. Begge stiene deler samme handler, så de kan ikke drive fra hverandre.
foreach (var sti in new[] { "/helse", "/health" })
{
    app.MapGet(sti, async (RegelIdeDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct)
                ? Results.Ok(new { status = "oppe" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
        .WithName($"Helsesjekk{sti.Replace("/", "_")}")
        .WithSummary("Svarer 200 når API-et er oppe og databasen svarer.")
        .ExcludeFromDescription();
}

// Migrer og førstegangs-sås de kjente fixture-dokumentene hvis basen er tom — kun en utviklings-
// bekvemmelighet ("virker rett ut av boksen"), ikke en generell import-mekanisme. Ekte import skjer
// via egne endepunkter/verktøy når byggesteg 1s importfunksjon (kap. 3.3 i produktkrav) bygges videre.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RegelIdeDbContext>();
    await Databaseoppsett.SorgForSkjemaAsync(db);

    if (!await db.Rettskilder.AnyAsync())
    {
        // Stien er konfigurerbar fordi containeren ikke har repoets mappestruktur rundt seg
        // (der ligger kildene på /kilder). Fallback er den samme relative stien som før.
        var kildemappe = app.Configuration["RegelIde:Kildemappe"]
            ?? Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "data", "kilder", "raw-lovdata"));
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
        // OpprettetTidspunkt settes eksplisitt: på Postgres ville now() dekket det, men den
        // databasestandarden finnes ikke på SQLite-profilen. Dette er eneste stedet i koden som
        // lente seg på den.
        var testkommunen = new Virksomhet
        {
            Id = Guid.NewGuid(), Navn = "Testkommunen", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
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
    // (2026-08-22: en femte "virksomhet"-kind ble kort lagt til her og reverdert samme runde —
    // en løpetekst-omtale av en virksomhet tagges som 'begrep' mot en navneform-rad
    // (Begrepskategori='virksomhet', docs/20 §2.3), IKKE direkte mot Virksomhet-katalogen. Se
    // VirksomhetsbegrepTjeneste/GET /api/begreper for hvordan navneformer allerede flettes inn i
    // 'begrep'-registeret.)
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

    // Byggesteg 5 runde 3 (docs/14-byggesteg5-teknisk-design.md) — testcase for ekte KI-agentkjøring
    // mot ekte data. Kun Virksomhet+Bruker seedet her; rettskilde/fil/lenke/agent-kjøring gjøres
    // live gjennom appen.
    await AgderFylkeskommuneSeed.SeedAsync(db);

    // Byggesteg 2-testcaseinnhold (2026-07-29, docs/06-veikart.md) — tjeneste/begreper/kodelister for
    // "Alminnelig skjenkebevilling". Kjøres etter alkoholloven-importen over (no-op hvis den mangler).
    await Byggesteg2InnholdSeed.SeedAsync(db);

    // Byggesteg 4 runde 1-testcaseinnhold (2026-07-30) — vilkårstreet fra docs/01-referansemodell.md
    // §5.5. Kjøres etter byggesteg 2 (no-op hvis tjenesten/begrepet den trenger ikke finnes ennå).
    await Byggesteg4VilkarstreSeed.SeedAsync(db);
    await KommunaleParametreSeed.SeedAsync(db);

    // Fasit-runde 4-innhold (2026-07-31, docs/12-fasit-handbok-leveranse.md) — det som opprinnelig ble
    // bygget via ekte, live API-kall mot en kjørende instans, nå som gjentakbar seed slik at
    // RundskrivReproduksjonTests.cs faktisk måler det samme innholdet i test-databasen. Kjøres etter
    // Byggesteg4VilkarstreSeed (krever rotnoden + Vandelsvilkåret den bygger).
    await FasitRunde4Seed.SeedAsync(db);

    // Hendelse + Tjenesteavhengighet (2026-07-31, docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1)
    // — kobler de 13 fasit-tjenestene faktisk sammen med "Alminnelig skjenkebevilling" i stedet for at
    // de forblir frittstående. Kjøres etter FasitRunde4Seed (krever tjenestene den oppretter).
    await HendelseTjenesteavhengighetSeed.SeedAsync(db);

    // Bergen kommunes nettside-/håndbok-dokumentgraf-korpus (2026-08-13, docs/15-handbok-dokumentgraf-
    // notat.md, byggesteg-1-utvidelsen — applikasjonslaget). Egen mor-mappe (ikke den samme
    // "raw-lovdata"-spesifikke `kildemappe`-variabelen over) siden BergenKorpusSeed selv trenger
    // BÅDE raw-lovdata, raw-handbok OG raw-nettside — samme konfigurerbare-sti-begrunnelse.
    var dataKilderRotmappe = app.Configuration["RegelIde:DataKilderRotmappe"]
        ?? Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "data", "kilder"));
    await BergenKorpusSeed.SeedAsync(db, dataKilderRotmappe);

    // Rettighet/Handling-modellrunden (2026-08-20) — overfører den hånd-skrevne modellutforskningen
    // (serveringsbevilling-modell-forslag.json) til ekte rader. Kjøres etter FasitRunde4Seed (krever
    // Serveringsbevilling) og BergenKorpusSeed (krever Bergen kommune-virksomheten, som Fettutskiller
    // opprettes under).
    await ServeringsbevillingModellSeed.SeedAsync(db);

    // Organisasjonsregister (2026-08-14) — norske kommuner/fylkeskommuner fra Johanns eksport, se
    // OrganisasjonsregisterSeed.cs. Kjøres SIST av virksomhet-seedene over, slik at matching mot
    // Testkommunen/Agder/Bærum+Tønsberg/Bergen faktisk finner de eksisterende radene i stedet for å
    // opprette duplikater.
    await OrganisasjonsregisterSeed.SeedAsync(db);
}

// GUI-et spør om profilen for å vite om brukervelgeren skal vises i det hele tatt.
app.MapGet("/api/oppsett", () => Results.Ok(new { autentisering = autentiseringsprofil.ToString().ToLowerInvariant() }))
    .WithOpenApi()
    .WithName("HentOppsett")
    .WithSummary("Forteller klienten hvilken autentiseringsprofil serveren kjører.");

// Hvem er jeg — den eneste kilden til gjeldende bruker under Altinn-profilen.
app.MapGet("/api/meg", async (HttpRequest request, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        // IkkeInnloggetSvar, ikke Results.Unauthorized(): den siste har ingen kropp, og klienten
        // viser meldingen herfra når den er innlogget men ikke fikk noen brukerkonto.
        if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);

        var virksomhet = await db.Virksomheter.FirstAsync(v => v.Id == bruker.VirksomhetId, ct);
        return Results.Ok(new BrukerDto(bruker.Id, bruker.Navn, virksomhet.Id, virksomhet.Navn, bruker.Rolle, bruker.AltinnBrukerId != null));
    })
    .WithOpenApi()
    .WithName("HentMeg")
    .WithSummary("Returnerer den innloggede brukeren, uavhengig av autentiseringsprofil.");

// Diagnostikk: viser claims i eget token, slik at første innlogging i tt02 avklarer hvilke
// identifikatorer runtime-tokenet faktisk inneholder (userid vs party-id vs fødselsnummer).
// Av som standard, og viser aldri andres claims enn innsenderens egne.
if (autentiseringsprofil is Autentiseringsprofil.Altinn
    && Altinninnstillinger.Les(app.Configuration).VisClaims)
{
    app.MapGet("/api/meg/claims", (HttpContext kontekst) =>
            kontekst.User.Identity?.IsAuthenticated is true
                ? Results.Ok(kontekst.User.Claims.Select(c => new { type = c.Type, verdi = c.Value }))
                : Results.Unauthorized())
        .WithName("HentMineClaims")
        .WithSummary("Diagnostikk for tt02-oppkobling. Krever RegelIde:Altinn:VisClaims=true.");
}

app.MapGet("/api/brukere", async (RegelIdeDbContext db) =>
    {
        // Lister ALLE brukere (testbrukere OG ekte Altinn-brukere, se ErAltinnBruker) — brukt av to
        // ulike GUI-flater: brukervelgeren i identitetsbrikken (kun under testbruker-profilen, og
        // klienten filtrerer der bort ErAltinnBruker-rader selv, se BrukerContext.tsx) og den nye
        // brukerhåndteringssiden (/brukere), som skal vise alt uansett profil.
        var brukere = await db.Brukere
            .Join(db.Virksomheter, b => b.VirksomhetId, v => v.Id, (b, v) => new { b, v })
            .OrderBy(x => x.b.Navn)
            .Select(x => new BrukerDto(x.b.Id, x.b.Navn, x.v.Id, x.v.Navn, x.b.Rolle, x.b.AltinnBrukerId != null))
            .ToListAsync();
        return Results.Ok(brukere);
    })
    .WithOpenApi()
    .WithName("HentBrukere")
    .WithSummary("Lister alle brukere (testbrukere og ekte Altinn-brukere) for GUI-ets brukervelger og brukerhåndteringssiden.");

app.MapPost("/api/brukere", async (OpprettBrukerRequest body, BrukerregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        try
        {
            var bruker = await register.OpprettAsync(body.Navn, body.Rolle, body.VirksomhetId, ct);
            var virksomhet = await db.Virksomheter.FirstAsync(v => v.Id == bruker.VirksomhetId, ct);
            return Results.Created(
                $"/api/brukere/{bruker.Id}",
                new BrukerDto(bruker.Id, bruker.Navn, virksomhet.Id, virksomhet.Navn, bruker.Rolle, false));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithOpenApi()
    .WithName("OpprettBruker")
    .WithSummary("Oppretter en ny testbruker og tilordner den til en virksomhet (brukerhåndteringssiden).");

app.MapPut("/api/brukere/{id:guid}", async (Guid id, OppdaterBrukerRequest body, BrukerregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        try
        {
            var bruker = await register.OppdaterAsync(id, body.Rolle, body.VirksomhetId, ct);
            if (bruker is null) return Results.NotFound(new { feil = $"Ingen bruker med id '{id}'." });

            var virksomhet = await db.Virksomheter.FirstAsync(v => v.Id == bruker.VirksomhetId, ct);
            return Results.Ok(new BrukerDto(bruker.Id, bruker.Navn, virksomhet.Id, virksomhet.Navn, bruker.Rolle, bruker.AltinnBrukerId != null));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithOpenApi()
    .WithName("OppdaterBruker")
    .WithSummary("Endrer rolle og virksomhetstilordning for en eksisterende bruker (test- eller Altinn-bruker).");

app.MapGet("/api/virksomheter", async (RegelIdeDbContext db) =>
        (await db.Virksomheter.ToListAsync()).Select(VirksomhetDto.FraEntitet))
    .WithOpenApi()
    .WithName("HentVirksomheter")
    .WithSummary("Lister virksomheter — hele virksomhetskatalogen (docs/20), ikke bare aktive tenanter.");

app.MapPut("/api/virksomheter/{id:guid}/forvaltningsniva", async (Guid id, SettForvaltningsnivaRequest body, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var virksomhet = await db.Virksomheter.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (virksomhet is null) return Results.NotFound(new { feil = $"Ingen virksomhet med id '{id}'." });

        // Validert mot KL-FORVALTNINGSNIVA (docs/20) i stedet for en hardkodet liste her — samme
        // "kodelisten ER sannheten, ikke en kopi av den i kode"-prinsipp som resten av appen. NULL
        // (tilbake til "ikke satt") er alltid gyldig — docs/20 §7.2: feltet skal kunne stå tomt.
        if (body.Forvaltningsniva is not null)
        {
            var kodelisteId = await db.Kodelister
                .Where(k => k.Kode == "KL-FORVALTNINGSNIVA")
                .Select(k => k.Id)
                .FirstOrDefaultAsync(ct);
            var gyldig = kodelisteId != Guid.Empty && await db.KodelisteKoder
                .AnyAsync(k => k.KodelisteId == kodelisteId && k.Kode == body.Forvaltningsniva, ct);
            if (!gyldig)
            {
                return Results.BadRequest(new { feil = $"'{body.Forvaltningsniva}' er ikke en gyldig kode i KL-FORVALTNINGSNIVA." });
            }
        }

        virksomhet.Forvaltningsniva = body.Forvaltningsniva;
        await db.SaveChangesAsync(ct);
        return Results.Ok(VirksomhetDto.FraEntitet(virksomhet));
    })
    .WithOpenApi()
    .WithName("SettVirksomhetForvaltningsniva")
    .WithSummary("Setter Forvaltningsnivå — validert mot KL-FORVALTNINGSNIVA-kodelisten (docs/20 §7.2: aldri gjettet automatisk).");

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
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        var oppdatert = await repo.OppdaterMetadataAsync(
            id, body.Kortnavn, body.Utgiver, body.InterntDokNr, body.Revisjonsnr, body.VedtattAv,
            body.Vedtaksdato, body.GyldigTil, body.KonsolidertDato, bruker.Navn);
        return oppdatert is null
            ? Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." })
            : Results.Ok(RettskildeDetalj.FraEntitet(oppdatert));
    })
    .WithName("OppdaterRettskildeMetadata")
    .WithSummary("Oppdaterer redigerbar metadata (Kortnavn/Utgiver/InterntDokNr/Revisjonsnr/VedtattAv/" +
        "Vedtaksdato/GyldigTil/KonsolidertDato). Eli er ALLTID skrivebeskyttet, aldri i denne requesten.");

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

rettskilder.MapGet("/{id:guid}/referert-av-tjenester", async (Guid id, RettskildeRepository repo) =>
        Results.Ok(await repo.ReferertAvTjenesterAsync(id)))
    .WithName("HentRettskildeReferertAvTjenester")
    .WithSummary("Byggesteg 4 — hvilke tjenester som refererer denne rettskilden (motsatt retning av tjenestens regelverksreferanser).");

rettskilder.MapGet("/{id:guid}/referert-av-dokumenter", async (Guid id, RettskildeRepository repo) =>
        Results.Ok(await repo.ReferertAvAndreDokumenterAsync(id)))
    .WithName("HentRettskildeReferertAvDokumenter")
    .WithSummary("Punkt 6/9 — hvilke ANDRE dokumenters (håndbok/rundskriv) noder som refererer denne rettskilden.");

// ---------- Punkt 8 (avklaringsrunde 2026-08-13) — §3.4s multi-sti og §3.2s lenker for en   ----------
// ---------- Brukerveiledning. Tomme lister for enhver annen doctype, ikke en feil.          ----------

rettskilder.MapGet("/{id:guid}/stier", async (Guid id, RettskildeRepository repo) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." });
        var stier = await repo.StierForAsync(id);
        return Results.Ok(stier.Select(NettsideStiDto.FraEntitet));
    })
    .WithName("HentRettskildeStier")
    .WithSummary("§3.4 — navigasjonsstiene en Brukerveiledning opptrer under. Tom liste for andre doctyper.");

rettskilder.MapGet("/{id:guid}/nettside-lenker", async (Guid id, RettskildeRepository repo) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." });
        return Results.Ok(await repo.NettsideLenkerForAsync(id));
    })
    .WithName("HentRettskildeNettsideLenker")
    .WithSummary("§3.2 — utgående lenker (lenker_til/lovdatalenke) fra en Brukerveilednings side-node, med oppløsningsstatus.");

// ---------- Referanser (2026-07-30) — generell variant av håndbokens lovreferanse-kobling,     ----------
// ---------- brukbar på en node i HVILKEN SOM HELST rettskilde, ikke bare håndbok-kommentarer.  ----------

rettskilder.MapPost("/{rettskildeId:guid}/noder/{nodeId:guid}/referanser", async (Guid rettskildeId, Guid nodeId, KobleLovreferanseRequest body,
        HandbokForfatterTjeneste tjeneste, RettskildeRepository repo, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(rettskildeId) is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{rettskildeId}'." });
        try
        {
            var referanse = await tjeneste.KobleLovreferanseAsync(nodeId, body.TilRettskildeId, body.TilEid, ct);
            return Results.Created($"/api/rettskilder/{rettskildeId}/referanser", RettskildeReferanseDto.FraEntitet(referanse));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("KobleRettskildeNodeReferanse")
    .WithSummary("Kobler en manuell referanse fra en node til en eId — samme mekanisme som håndbokens lovreferanser, men uten håndbok-binding.");

rettskilder.MapDelete("/{rettskildeId:guid}/referanser/{referanseId:guid}", async (Guid rettskildeId, Guid referanseId,
        HandbokForfatterTjeneste tjeneste, CancellationToken ct) =>
    {
        try
        {
            return await tjeneste.FjernLovreferanseAsync(referanseId, ct)
                ? Results.NoContent()
                : Results.NotFound(new { feil = $"Ingen referanse med id '{referanseId}'." });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("FjernRettskildeNodeReferanse")
    .WithSummary("Fjerner en manuelt lagt referanse. Kilde-referanser (Opprinnelse='import') kan ikke fjernes her.");

// ---------- Tekst-tagging (2026-07-24, AK-3.3.1–3.3.4) — krever X-Bruker-Id, tagger er alltid ----------
// ---------- virksomhetens eget arbeidsprodukt (§0.1 i domenemodellen), aldri delt på tvers.     ----------

rettskilder.MapGet("/{id:guid}/tagger", async (Guid id, HttpRequest request, RettskildeRepository repo,
        TekstTaggTjeneste taggTjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." });

        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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

rettskilder.MapPost("/{id:guid}/tagger/{taggId:guid}/koble", async (Guid taggId, HttpRequest request, KobleTaggTilEntitetRequest body,
        TekstTaggTjeneste taggTjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var oppdatert = await taggTjeneste.KobleTilEntitetAsync(taggId, body.RefId, bruker.Navn, ct);
            return oppdatert is null
                ? Results.NotFound(new { feil = $"Ingen tagg med id '{taggId}'." })
                : Results.Ok(TekstTaggDto.FraEntitet(oppdatert));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("KobleTekstTaggTilEntitet")
    .WithSummary("Kobler en eksisterende tagg til en Begrep/Tjeneste-rad (byggesteg 2) — låser opp TekstTaggEntitet.RefId.");

// ---------- Import (2026-07-24) — krever X-Bruker-Id for attribusjon, se GjeldendeBrukerTjeneste ----------

rettskilder.MapPost("/fil", async (HttpRequest request, IFormFile fil, Guid? virksomhetId,
        RettskildeImportTjeneste importer, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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
        LovdataBulkHenter henter, RettskildeImportTjeneste importer, LovdataImportstatusTjeneste importstatusTjeneste,
        RegelIdeDbContext db, ILogger<Program> logger, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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

        // Avledet fra datokoden ALENE (samme som LovdataFullimportTjeneste) — alltid tilgjengelig her,
        // siden HentRaaHtmlAsync over allerede har bekreftet at datokoden er velformet.
        var eli = LovdataIdentifikatorer.AvledEliFraDatokode(body.Datokode, out var kildetype);
        var type = kildetype == Kildetype.Lov ? "lov" : "forskrift";
        var tittel = LovdataBulkHenter.LesTittelBesteForsok(html);

        KonverteringResultat resultat;
        try
        {
            resultat = LovdataKonverterer.Konverter(html);
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException)
        {
            // Konsistens (2026-08-20, se LovdataImportstatusTjeneste): brukeren kan ha trigget denne
            // enkeltimporten nettopp FORDI dokumentet stod som importert=false i lovdata_importstatus
            // (fra forrige fullimport-runde) — hvis det fortsatt feiler, skal raden få den FERSKE
            // feilmeldingen, ikke stå igjen med en utdatert én til neste app-restart. Svelger egne feil
            // her: denne bekvemmeligheten skal aldri velte feilresponsen brukeren allerede skal få.
            try
            {
                await importstatusTjeneste.OppdaterAsync(body.Datokode, type, tittel, eli, importert: false, rettskildeId: null, ex.Message, ct);
            }
            catch (Exception statusEx) when (statusEx is not OperationCanceledException)
            {
                logger.LogWarning(statusEx, "Kunne ikke oppdatere lovdata_importstatus for {Datokode} etter mislykket enkeltimport.", body.Datokode);
            }

            return Results.UnprocessableEntity(new { feil = $"Hentet fra Lovdata, men kunne ikke tolke innholdet: {ex.Message}" });
        }

        // Alltid delt/nasjonalt (virksomhetId=null) -- dette endepunktet henter kun fra Lovdatas
        // offisielle bulk-datasett, som per definisjon kun inneholder nasjonale Lov/Forskrift.
        var rettskildeId = await importer.ImporterAsync(resultat, virksomhetId: null, bruker.Navn, ct);

        // Konsistens (2026-08-20, se LovdataImportstatusTjeneste): en vellykket enkeltimport her skal
        // ALLTID gjenspeiles i lovdata_importstatus også, ikke bare fullimport-rundens egen skriving —
        // ellers står raden fortsatt som importert=false med en utdatert feilmelding selv om dokumentet
        // nå faktisk er en ekte rettskilde.
        await importstatusTjeneste.OppdaterAsync(body.Datokode, type, tittel, eli, importert: true, rettskildeId, feilmelding: null, ct);

        return Results.Created($"/api/rettskilder/{rettskildeId}", new { id = rettskildeId });
    })
    .WithName("ImporterFraLovdata")
    .WithSummary("Henter og importerer en rettskilde fra Lovdatas offisielle bulk-datasett via datokode " +
        "(f.eks. \"LOV-1989-06-02-27\"). Alltid en delt/nasjonal kilde. Oppdaterer også lovdata_importstatus " +
        "for denne datokoden (konsistens med LovdataFullimportTjeneste).");

app.MapGet("/api/lovdata-katalog/sok", async (string q, LovdataKatalogTjeneste tjeneste, CancellationToken ct) =>
    {
        var treff = await tjeneste.SokAsync(q, ct);
        return Results.Ok(treff.Select(LovdataKatalogTreffDto.FraEntitet));
    })
    .WithOpenApi()
    .WithName("SokLovdataKatalog")
    .WithSummary("Søker i en lokal, søkbar katalog over Lovdatas bulk-datasett (byggesteg 5 runde 2) — " +
        "kun metadata, bygges/fornyes automatisk (24t foreldelsesgrense). Bruk treffets datokode mot " +
        "/api/rettskilder/lovdata for selve importen.");

app.MapGet("/api/lovdata-importstatus", async (bool? importert, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var sporsmal = db.LovdataImportstatuser.AsQueryable();
        if (importert is { } i) sporsmal = sporsmal.Where(s => s.Importert == i);

        var treff = await sporsmal.OrderBy(s => s.Datokode).ToListAsync(ct);
        return Results.Ok(treff.Select(LovdataImportstatusDto.FraEntitet));
    })
    .WithName("HentLovdataImportstatus")
    .WithSummary("Siste kjente importforsøk per KJENT Lovdata-dokument (fra bulk-arkivet), skrevet av " +
        "LovdataFullimportTjeneste — inkl. dokumenter som IKKE lot seg AKN-importere (importert=false), " +
        "med tittel/ELI/feilmelding til triage. ?importert=false viser kun det som trengs å prioriteres.");

// ---------- Eksterne kilder — rått høstelag for skjema-/tjenestekatalog (docs/13-backlog.md), ----------
// ---------- ENNÅ IKKE koblet til domenemodellen (docs/17/docs/18 fortsatt uavklart). Trigges på ----------
// ---------- forespørsel, ikke ved oppstart — samme begrunnelse som lovdata-katalogen over.       ----------

var eksterneKilder = app.MapGroup("/api/eksterne-kilder").WithOpenApi();

eksterneKilder.MapPost("/oppgaveregister/hent", async (OppgaveregisterHenter henter, CancellationToken ct) =>
    {
        var resultat = await henter.HentAlleSkjemaAsync(ct);
        return Results.Ok(new EksternKildeHostingResultatDto(resultat.Nye, resultat.Oppdaterte, resultat.Uendret));
    })
    .WithName("HentOppgaveregisterSkjema")
    .WithSummary("Høster alle skjemaer fra Oppgaveregisteret (Brønnøysundregistrene) inn som rå, uforandrede " +
        "kildeposter — idempotent upsert på (kildetype, ekstern_id). Ikke koblet til domenemodellen ennå.");

eksterneKilder.MapPost("/altinn-ressurser/hent", async (AltinnRessursHenter henter, CancellationToken ct) =>
    {
        var resultat = await henter.HentAlleRessurserAsync(ct);
        return Results.Ok(new EksternKildeHostingResultatDto(resultat.Nye, resultat.Oppdaterte, resultat.Uendret));
    })
    .WithName("HentAltinnRessurser")
    .WithSummary("Høster alle AltinnApp-ressurser fra Altinns ressursregister (tjenesteoversikten.no) inn som " +
        "rå, uforandrede kildeposter — idempotent upsert på (kildetype, ekstern_id). Ikke koblet til domenemodellen ennå.");

eksterneKilder.MapPost("/altinn-skjemaoversikt/hent", async (AltinnSkjemaoversiktHenter henter, CancellationToken ct) =>
    {
        var resultat = await henter.HentAltAsync(ct);
        return Results.Ok(new EksternKildeHostingResultatDto(resultat.Nye, resultat.Oppdaterte, resultat.Uendret));
    })
    .WithName("HentAltinnSkjemaoversikt")
    .WithSummary("Kryper hele Altinns skjemaoversikt (info.altinn.no/skjemaoversikt, ~800+ tjenestesider) og " +
        "høster hver tjenesteside inn som en strukturert kildepost — SYNKRONT, langvarig kall (trenger lang " +
        "klient-timeout), lagrer inkrementelt per etat. Idempotent, trygt å kjøre på nytt ved avbrudd.");

eksterneKilder.MapPost("/statsforvalter-tjenester/importer", async (HttpRequest request, TjenestelisteImporter importer, CancellationToken ct) =>
    {
        using var leser = new StreamReader(request.Body);
        var raaJson = await leser.ReadToEndAsync(ct);
        var resultat = await importer.ImporterAsync(raaJson, TjenestelisteImporter.Statsforvalter, ct);
        return Results.Ok(new TjenestelisteHostingResultatDto(resultat.Nye, resultat.Oppdaterte, resultat.Uendret, resultat.TilbydereMedManglendeOrgnummer));
    })
    .WithName("ImporterStatsforvalterTjenester")
    .WithSummary("Importerer Statsforvalternes 'skjema og tjenester'-oversikt fra en rå JSON-array-body — " +
        "FIL-basert, ikke URL-basert (Johanns egen eksterne Python-skrape leverer filen periodisk, ingen " +
        "stabil offentlig URL denne appen kan polle selv). Idempotent upsert på (kildetype, url). Ikke " +
        "koblet til domenemodellen ennå. Rapporterer også antall tilbys_av-oppføringer med manglende " +
        "organisasjonsnummer — et kjent oppstrøms-skjørhetstilfelle, aldri behandlet som en gyldig identifikator.");

eksterneKilder.MapPost("/fylkeskommune-tjenester/importer", async (HttpRequest request, TjenestelisteImporter importer, CancellationToken ct) =>
    {
        using var leser = new StreamReader(request.Body);
        var raaJson = await leser.ReadToEndAsync(ct);
        var resultat = await importer.ImporterAsync(raaJson, TjenestelisteImporter.FylkeskommuneDialog, ct);
        return Results.Ok(new TjenestelisteHostingResultatDto(resultat.Nye, resultat.Oppdaterte, resultat.Uendret, resultat.TilbydereMedManglendeOrgnummer));
    })
    .WithName("ImporterFylkeskommuneTjenester")
    .WithSummary("Importerer fylkeskommunenes 'dialog'-kontaktskjema-oversikt fra en rå JSON-array-body — " +
        "strukturelt identisk fil-basert kilde som Statsforvalter-tjenester (samme Johann-eksterne-skrape-" +
        "mønster, ingen stabil offentlig URL denne appen kan polle selv), delt implementasjon via " +
        "TjenestelisteImporter. Idempotent upsert på (kildetype, url). Ikke koblet til domenemodellen ennå. " +
        "Rapporterer også antall tilbys_av-oppføringer med manglende organisasjonsnummer, selv om empirisk " +
        "alle rader i denne kilden har nøyaktig én tilbyder.");

eksterneKilder.MapPost("/kommune-tjenester/importer", async (HttpRequest request, KommuneTjenesteHenter henter, CancellationToken ct) =>
    {
        using var leser = new StreamReader(request.Body);
        var raaJson = await leser.ReadToEndAsync(ct);
        var resultat = await henter.ImporterAsync(raaJson, ct);
        return Results.Ok(new KommuneTjenesteHostingResultatDto(resultat.Nye, resultat.Oppdaterte, resultat.Uendret, resultat.RecordsMedManglendeOrganisasjonsnummer));
    })
    .WithName("ImporterKommuneTjenester")
    .WithSummary("Importerer kommune.no-tjenester fra en rå JSON-body — array av KOMMUNE-objekter, hver med " +
        "egen records[]-liste (ikke et bart tjeneste-array som Statsforvalter/fylkeskommune-kildene). FIL-basert " +
        "(Johanns eget eksterne skrapeskript mot ~327 kommune.no-nettsteder, ingen stabil offentlig URL denne " +
        "appen kan polle selv). Idempotent upsert på (kildetype, organisasjonsnummer::url) — en sammensatt nøkkel, " +
        "IKKE url alene, fordi to reelt distinkte kommuner (begge \"Herøy\") deler samme url-mønster i " +
        "produksjonsdataene. Ikke koblet til domenemodellen ennå. Rapporterer også antall records hvis eiende " +
        "kommune mangler organisasjonsnummer (empirisk null i produksjon, men aldri stille antatt).");

eksterneKilder.MapGet("/", async (string? kildetype, RegelIdeDbContext db, CancellationToken ct, int start = 0, int antall = 50) =>
    {
        var faktiskStart = Math.Max(start, 0);
        var faktiskAntall = antall <= 0 ? 50 : Math.Min(antall, 200);

        var sporring = db.EksterneKilder.AsQueryable();
        if (!string.IsNullOrWhiteSpace(kildetype)) sporring = sporring.Where(k => k.Kildetype == kildetype);

        var totalt = await sporring.CountAsync(ct);
        var rader = await sporring
            .OrderBy(k => k.Kildetype).ThenBy(k => k.EksternId)
            .Skip(faktiskStart).Take(faktiskAntall)
            .ToListAsync(ct);

        return Results.Ok(new EksternKildeListeDto(totalt, rader.Select(EksternKildeDto.FraEntitet).ToList()));
    })
    .WithName("ListEksterneKilder")
    .WithSummary("Paginert liste over høstede rå kildeposter, valgfritt filtrert på kildetype (start/antall, default 50, maks 200).");

// ---------- Håndbok/rundskriv-forfatterflyt (2026-07-26, docs/03-domenemodell.md §1.1.1) ----------
// ---------- krever X-Bruker-Id for attribusjon, samme mønster som import/tagging over.       ----------

var handboker = app.MapGroup("/api/handboker").WithOpenApi();

handboker.MapPost("/", async (HttpRequest request, OpprettHandbokRequest body,
        HandbokForfatterTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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
    {
        try
        {
            return await tjeneste.FjernLovreferanseAsync(referanseId, ct)
                ? Results.NoContent()
                : Results.NotFound(new { feil = $"Ingen lovreferanse med id '{referanseId}'." });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("FjernHandbokLovreferanse")
    .WithSummary("Fjerner en lovreferanse-kobling fra en kommentarseksjon. Kilde-referanser (Opprinnelse='import') kan ikke fjernes.");

handboker.MapPost("/{id:guid}/kommentarer/{nodeId:guid}/revisjonsmerke", async (Guid id, Guid nodeId, HttpRequest request, SettRevisjonsmerkeRequest body,
        HandbokForfatterTjeneste tjeneste, RettskildeRepository repo, RegelIdeDbContext db, CancellationToken ct) =>
    {
        if (await repo.FinnAsync(id) is null) return Results.NotFound(new { feil = $"Ingen håndbok med id '{id}'." });
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
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

handboker.MapGet("/{id:guid}/rettskilder", async (Guid id, HandbokForfatterTjeneste tjeneste, CancellationToken ct) =>
        Results.Ok((await tjeneste.HentRettskildeomfangAsync(id, ct)).Select(HandbokRettskildeomfangDto.FraEntitet)))
    .WithName("HentHandbokRettskildeomfang")
    .WithSummary("Lister hvilke rettskilder en håndbok som helhet omhandler.");

handboker.MapPost("/{id:guid}/rettskilder", async (Guid id, HttpRequest request, LeggTilRettskildeomfangRequest body,
        HandbokForfatterTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var omfang = await tjeneste.LeggTilRettskildeomfangAsync(id, body.TilRettskildeId, bruker.Navn, ct);
            return Results.Created($"/api/handboker/{id}/rettskilder/{omfang.Id}", HandbokRettskildeomfangDto.FraEntitet(omfang));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("LeggTilHandbokRettskildeomfang")
    .WithSummary("Deklarerer at håndboken som helhet omhandler en gitt rettskilde.");

handboker.MapDelete("/{id:guid}/rettskilder/{omfangId:guid}", async (Guid omfangId, HandbokForfatterTjeneste tjeneste, CancellationToken ct) =>
        await tjeneste.FjernRettskildeomfangAsync(omfangId, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen rettskildeomfang med id '{omfangId}'." }))
    .WithName("FjernHandbokRettskildeomfang")
    .WithSummary("Fjerner en rettskilde fra håndbokens omfang.");

// ---------- Tjenesteregister (CPSV-AP-NO, docs/03-domenemodell.md §1.5) — byggesteg 2 ----------
// ---------- krever X-Bruker-Id — en tjeneste er alltid virksomhetens eget arbeidsprodukt (§0.1). ----------

var tjenester = app.MapGroup("/api/tjenester").WithOpenApi();

tjenester.MapGet("/", async (HttpRequest request, TjenesteregisterTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        var liste = await tjeneste.ListerForAsync(bruker.VirksomhetId, ct);
        return Results.Ok(liste.Select(TjenesteDto.FraEntitet));
    })
    .WithName("HentTjenester")
    .WithSummary("Lister virksomhetens egne tjenester (produktkrav kap. 3.2).");

tjenester.MapGet("/{id:guid}", async (Guid id, TjenesteregisterTjeneste tjeneste, CancellationToken ct) =>
    {
        var t = await tjeneste.FinnAsync(id, ct);
        return t is null ? Results.NotFound(new { feil = $"Ingen tjeneste med id '{id}'." }) : Results.Ok(TjenesteDto.FraEntitet(t));
    })
    .WithName("HentTjeneste")
    .WithSummary("Henter én tjeneste.");

tjenester.MapGet("/sok-tverr-tenant", async (string q, TjenesteregisterTjeneste tjeneste, CancellationToken ct) =>
    {
        var treff = await tjeneste.SokTverrTenantAsync(q, ct);
        return Results.Ok(treff.Select(TjenesteTverrTenantTreffDto.FraTreff));
    })
    .WithName("SokTjenesterTverrTenant")
    .WithSummary("Søker i PUBLISERTE tjenester fra ALLE virksomheter (ikke bare egen) — for å finne en annen " +
        "virksomhets tjeneste som mål for en tjenesteavhengighet. Utkast/andre statuser fra andre virksomheter " +
        "forblir usynlige, samme virksomhet-isolasjons-default som docs/02 §0.1.");

tjenester.MapPost("/", async (HttpRequest request, TjenesteRequest body, TjenesteregisterTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var t = await tjeneste.OpprettAsync(bruker.VirksomhetId, body.Tittel, body.Beskrivelse, body.KompetentMyndighet,
                body.Output, body.Tjenestetype, body.Malgruppe, body.Kanaler, body.Kostnad, body.Behandlingstid,
                body.Kontaktpunkt, body.KonsekvensVedBrudd, body.Sprak, bruker.Navn, ct,
                body.Livshendelser, body.LosKlassifisering, body.Tjenesteomrade,
                body.Type, body.Formal, body.Innhold);
            return Results.Created($"/api/tjenester/{t.Id}", TjenesteDto.FraEntitet(t));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettTjeneste")
    .WithSummary("Oppretter en ny tjeneste (utkast).");

tjenester.MapPut("/{id:guid}", async (Guid id, HttpRequest request, TjenesteRequest body, TjenesteregisterTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var t = await tjeneste.OppdaterAsync(id, bruker.VirksomhetId, body.Tittel, body.Beskrivelse, body.KompetentMyndighet, body.Output,
                body.Tjenestetype, body.Malgruppe, body.Kanaler, body.Kostnad, body.Behandlingstid, body.Kontaktpunkt,
                body.KonsekvensVedBrudd, body.Sprak, bruker.Navn, ct,
                body.Livshendelser, body.LosKlassifisering, body.Tjenesteomrade,
                body.Type, body.Formal, body.Innhold);
            return t is null ? Results.NotFound(new { feil = $"Ingen tjeneste med id '{id}'." }) : Results.Ok(TjenesteDto.FraEntitet(t));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OppdaterTjeneste")
    .WithSummary("Oppdaterer en tjeneste.");

tjenester.MapPost("/{id:guid}/status", async (Guid id, HttpRequest request, SettStatusRequest body, TjenesteregisterTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var t = await tjeneste.SettStatusAsync(id, bruker.VirksomhetId, body.Status, bruker.Navn, ct, body.GodkjentAv);
            return t is null ? Results.NotFound(new { feil = $"Ingen tjeneste med id '{id}'." }) : Results.Ok(TjenesteDto.FraEntitet(t));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettTjenesteStatus")
    .WithSummary("Endrer status (§3.1 i domenemodellen).");

tjenester.MapGet("/{id:guid}/regelverksreferanser", async (Guid id, TjenesteregisterTjeneste tjeneste, CancellationToken ct) =>
        Results.Ok((await tjeneste.RegelverksreferanserForAsync(id, ct)).Select(TjenesteRegelverksreferanseDto.FraEntitet)))
    .WithName("HentTjenesteRegelverksreferanser")
    .WithSummary("Lister tjenestens regelverksreferanser.");

tjenester.MapPost("/{id:guid}/regelverksreferanser", async (Guid id, KobleRegelverksreferanseRequest body, TjenesteregisterTjeneste tjeneste, CancellationToken ct) =>
    {
        try
        {
            var r = await tjeneste.KobleRegelverksreferanseAsync(id, body.TilRettskildeId, body.TilEid, ct);
            return Results.Created($"/api/tjenester/{id}/regelverksreferanser", TjenesteRegelverksreferanseDto.FraEntitet(r));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("KobleTjenesteRegelverksreferanse")
    .WithSummary("Kobler tjenesten til en paragraf i en Lov/Forskrift.");

tjenester.MapDelete("/regelverksreferanser/{referanseId:guid}", async (Guid referanseId, TjenesteregisterTjeneste tjeneste, CancellationToken ct) =>
        await tjeneste.FjernRegelverksreferanseAsync(referanseId, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen regelverksreferanse med id '{referanseId}'." }))
    .WithName("FjernTjenesteRegelverksreferanse")
    .WithSummary("Fjerner en regelverksreferanse-kobling.");

// ---------- Handlinger (2026-08-20) — konkrete handlinger tilknyttet en Rettighet ----------

tjenester.MapGet("/{id:guid}/handlinger", async (Guid id, HandlingregisterTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.ListerForTjenesteAsync(id, ct)).Select(HandlingDto.FraEntitet)))
    .WithName("HentHandlinger")
    .WithSummary("Lister handlingene tilknyttet en rettighet (tjeneste). Åpen lesing, samme holdning som GET /api/tjenester/{id}.");

tjenester.MapPost("/{id:guid}/handlinger", async (Guid id, HttpRequest request, HandlingRequest body, HandlingregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var h = await register.OpprettAsync(
                bruker.VirksomhetId, id, body.Navn, body.Handlingstype, body.Bruksomraade, body.UtfortAv,
                body.Kanaler, body.Behandlingstid, body.Kostnad, body.Vedlegg, body.Veiledningstekst, body.Arsaker,
                body.Resultat, body.Merknad, bruker.Navn, ct);
            return Results.Created($"/api/tjenester/handlinger/{h.Id}", HandlingDto.FraEntitet(h));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettHandling")
    .WithSummary("Oppretter en ny handling under en rettighet (tjeneste).");

tjenester.MapGet("/handlinger/{handlingId:guid}", async (Guid handlingId, HandlingregisterTjeneste register, CancellationToken ct) =>
    {
        var h = await register.FinnAsync(handlingId, ct);
        return h is null ? Results.NotFound(new { feil = $"Ingen handling med id '{handlingId}'." }) : Results.Ok(HandlingDto.FraEntitet(h));
    })
    .WithName("HentHandling")
    .WithSummary("Henter én handling.");

tjenester.MapPut("/handlinger/{handlingId:guid}", async (Guid handlingId, HttpRequest request, HandlingRequest body, HandlingregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var h = await register.OppdaterAsync(
                handlingId, bruker.VirksomhetId, body.Navn, body.Handlingstype, body.Bruksomraade, body.UtfortAv,
                body.Kanaler, body.Behandlingstid, body.Kostnad, body.Vedlegg, body.Veiledningstekst, body.Arsaker,
                body.Resultat, body.Merknad, bruker.Navn, ct);
            return h is null ? Results.NotFound(new { feil = $"Ingen handling med id '{handlingId}'." }) : Results.Ok(HandlingDto.FraEntitet(h));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OppdaterHandling")
    .WithSummary("Oppdaterer en handling.");

tjenester.MapDelete("/handlinger/{handlingId:guid}", async (Guid handlingId, HttpRequest request, HandlingregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        return await register.SlettAsync(handlingId, bruker.VirksomhetId, ct)
            ? Results.NoContent()
            : Results.NotFound(new { feil = $"Ingen handling med id '{handlingId}'." });
    })
    .WithName("SlettHandling")
    .WithSummary("Sletter en handling.");

tjenester.MapPost("/handlinger/{handlingId:guid}/status", async (Guid handlingId, HttpRequest request, SettStatusRequest body, HandlingregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var h = await register.SettStatusAsync(handlingId, bruker.VirksomhetId, body.Status, bruker.Navn, ct);
            return h is null ? Results.NotFound(new { feil = $"Ingen handling med id '{handlingId}'." }) : Results.Ok(HandlingDto.FraEntitet(h));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettHandlingStatus")
    .WithSummary("Endrer status på en handling.");

tjenester.MapPost("/handlinger/{handlingId:guid}/rotnode", async (Guid handlingId, HttpRequest request, SettRotnodeRequest body, HandlingregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var h = await register.SettRotnodeAsync(handlingId, bruker.VirksomhetId, body.RegelnodeId, ct);
            return h is null ? Results.NotFound(new { feil = $"Ingen handling med id '{handlingId}'." }) : Results.Ok(HandlingDto.FraEntitet(h));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettHandlingRotnode")
    .WithSummary("Kobler handlingen til en EGEN rotnode i vilkårstreet — overstyrer rettighetens for denne ene handlingens saksbehandling.");

// ---------- «Identifiser tjenester» (byggesteg 5 runde 1, docs/06-veikart.md) — stub-KI ----------

tjenester.MapGet("/forslag", async (HttpRequest request, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        var forslag = await db.Tjenester
            .Where(t => t.VirksomhetId == bruker.VirksomhetId && t.Entitetsstatus == "gjeldende" && t.Status == "foreslatt_av_ai")
            .OrderByDescending(t => t.OpprettetTidspunkt)
            .ToListAsync(ct);
        var resultat = new List<TjenesteforslagDto>();
        foreach (var t in forslag)
        {
            var proveniens = await db.Proveniens
                .Where(p => p.EntitetType == "tjeneste" && p.EntitetId == t.Id && p.Handling == "foreslatt_av_ai")
                .OrderByDescending(p => p.Dato)
                .FirstOrDefaultAsync(ct);
            resultat.Add(new TjenesteforslagDto(TjenesteDto.FraEntitet(t), proveniens?.AiForslagVersjon, proveniens?.Dato ?? t.OpprettetTidspunkt, proveniens?.KildeReferanserJson));
        }
        return Results.Ok(resultat);
    })
    .WithName("HentTjenesteforslagKo")
    .WithSummary("Lister ventende KI-forslag til nye tjenester (foreslatt_av_ai).");

tjenester.MapPost("/forslag/kjor", async (HttpRequest request, KjorForslagRequest body, TjenesteforslagTjeneste forslagstjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            var resultat = await forslagstjeneste.KjorForslagAsync(bruker.VirksomhetId, body.RettskildeIder, bruker.Navn, ct);
            return Results.Ok(new KjorForslagResponsDto<TjenesteDto>(
                resultat.Opprettede.Select(TjenesteDto.FraEntitet).ToList(), resultat.InputTokens, resultat.OutputTokens, resultat.Melding));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("KjorTjenesteforslag")
    .WithSummary("Kjører «Identifiser tjenester»-agenten mot valgte rettskilder + kunnskapsbibliotek (byggesteg 5 runde 1, stub-KI).");

// Byggesteg 5 runde 4 (RAG-spike) — RÅ SAMMENLIGNING mot endepunktet over, ikke en erstatning. Ingen
// frontend-kobling denne runden (se docs/13-backlog.md §2.2) — kun til manuell/skriptet sammenligning
// av tokens+forslag mellom dump-alt (over) og RAG (her) på samme testcase.
tjenester.MapPost("/forslag/kjor-rag", async (HttpRequest request, KjorForslagMedRagRequest body, TjenesteforslagTjeneste forslagstjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            var resultat = await forslagstjeneste.KjorForslagMedRagAsync(bruker.VirksomhetId, body.RettskildeIder, body.AntallNoder, bruker.Navn, ct);
            return Results.Ok(new KjorForslagResponsDto<TjenesteDto>(
                resultat.Opprettede.Select(TjenesteDto.FraEntitet).ToList(), resultat.InputTokens, resultat.OutputTokens, resultat.Melding));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("KjorTjenesteforslagMedRag")
    .WithSummary("RAG-spike (byggesteg 5 runde 4) — samme agent som /forslag/kjor, men henter kun de K mest like rettskilde-nodene i stedet for å dumpe alt. Rå sammenligning, ikke en erstatning.");

// ---------- Kunnskapsbibliotek (byggesteg 5 runde 1, docs/06-veikart.md) — krever X-Bruker-Id, ----------
// ---------- alltid virksomhetens eget arbeidsprodukt, kun brukt av «Identifiser tjenester». ----------

var kunnskapsbibliotek = app.MapGroup("/api/kunnskapsbibliotek").WithOpenApi();

kunnskapsbibliotek.MapGet("/lenker", async (HttpRequest request, KunnskapsbibliotekTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        var liste = await tjeneste.ListerForVirksomhetAsync(bruker.VirksomhetId, ct);
        return Results.Ok(liste.Select(KunnskapsbibliotekLenkeDto.FraEntitet));
    })
    .WithName("HentKunnskapsbibliotekLenker")
    .WithSummary("Lister virksomhetens kunnskapsbibliotek-lenker.");

kunnskapsbibliotek.MapPost("/lenker", async (HttpRequest request, LeggTilLenkeRequest body, KunnskapsbibliotekTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            var lenke = await tjeneste.LeggTilLenkeAsync(bruker.VirksomhetId, body.Url, body.Beskrivelse, bruker.Navn, ct);
            return Results.Created($"/api/kunnskapsbibliotek/lenker/{lenke.Id}", KunnskapsbibliotekLenkeDto.FraEntitet(lenke));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("LeggTilKunnskapsbibliotekLenke")
    .WithSummary("Legger til en lenke i virksomhetens kunnskapsbibliotek.");

kunnskapsbibliotek.MapDelete("/lenker/{id:guid}", async (Guid id, KunnskapsbibliotekTjeneste tjeneste, CancellationToken ct) =>
        await tjeneste.SlettAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen kunnskapsbibliotek-lenke med id '{id}'." }))
    .WithName("SlettKunnskapsbibliotekLenke")
    .WithSummary("Fjerner en kunnskapsbibliotek-lenke.");

kunnskapsbibliotek.MapGet("/filer", async (HttpRequest request, KunnskapsbibliotekTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        var liste = await tjeneste.ListerFilerForVirksomhetAsync(bruker.VirksomhetId, ct);
        return Results.Ok(liste.Select(KunnskapsbibliotekFilDto.FraEntitet));
    })
    .WithName("HentKunnskapsbibliotekFiler")
    .WithSummary("Lister virksomhetens kunnskapsbibliotek-filer (uten rå fil-bytes).");

kunnskapsbibliotek.MapPost("/filer", async (HttpRequest request, IFormFile fil, KunnskapsbibliotekTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }

        using var minne = new MemoryStream();
        await fil.OpenReadStream().CopyToAsync(minne, ct);
        string? tittel = request.Form.TryGetValue("tittel", out var tittelVerdi) ? tittelVerdi.ToString() : null;

        try
        {
            var lagretFil = await tjeneste.LeggTilFilAsync(bruker.VirksomhetId, fil.FileName, minne.ToArray(), bruker.Navn, tittel, ct);
            return Results.Created($"/api/kunnskapsbibliotek/filer/{lagretFil.Id}", KunnskapsbibliotekFilDto.FraEntitet(lagretFil));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("LastOppKunnskapsbibliotekFil")
    .WithSummary("Laster opp en PDF/Word-fil til virksomhetens kunnskapsbibliotek — avvises hvis filen mangler tekstlag.")
    .DisableAntiforgery();

kunnskapsbibliotek.MapDelete("/filer/{id:guid}", async (Guid id, KunnskapsbibliotekTjeneste tjeneste, CancellationToken ct) =>
        await tjeneste.SlettFilAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen kunnskapsbibliotek-fil med id '{id}'." }))
    .WithName("SlettKunnskapsbibliotekFil")
    .WithSummary("Fjerner en kunnskapsbibliotek-fil.");

// ---------- Begrepsregister (SKOS, docs/03-domenemodell.md §1.3) — byggesteg 2 ----------

var begreper = app.MapGroup("/api/begreper").WithOpenApi();

begreper.MapGet("/", async (HttpRequest request, BegrepsregisterTjeneste begrepsregister,
        VirksomhetsbegrepTjeneste virksomhetsbegrepregister, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        // Egne fakta-/handlingsbegrep + ALLE delte virksomhets-/rollebegrep (docs/20 §2.3/§2.4) —
        // uten det siste er de INVISIBLE i tagg-picker-en (se VirksomhetsbegrepTjeneste.AlleAsync).
        var egne = await begrepsregister.ListerForAsync(bruker.VirksomhetId, ct);
        var delte = await virksomhetsbegrepregister.AlleAsync(ct);
        return Results.Ok(egne.Concat(delte).Select(BegrepDto.FraEntitet));
    })
    .WithName("HentBegreper")
    .WithSummary("Lister virksomhetens egne begreper (produktkrav kap. 3.8) + alle delte virksomhets-/rollebegrep (docs/20).");

begreper.MapGet("/{id:guid}", async (Guid id, BegrepsregisterTjeneste begrepsregister, CancellationToken ct) =>
    {
        var b = await begrepsregister.FinnAsync(id, ct);
        return b is null ? Results.NotFound(new { feil = $"Ingen begrep med id '{id}'." }) : Results.Ok(BegrepDto.FraEntitet(b));
    })
    .WithName("HentBegrep")
    .WithSummary("Henter ett begrep.");

begreper.MapPost("/", async (HttpRequest request, BegrepRequest body, BegrepsregisterTjeneste begrepsregister, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var b = await begrepsregister.OpprettAsync(bruker.VirksomhetId, body.Term, body.Definisjon, body.LovreferanseEid,
                body.GjelderFor, body.KodelisteReferanseId, body.SkosUrl, body.Begrepstype, bruker.Navn, ct);
            return Results.Created($"/api/begreper/{b.Id}", BegrepDto.FraEntitet(b));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettBegrep")
    .WithSummary("Oppretter et nytt begrep (utkast).");

begreper.MapPut("/{id:guid}", async (Guid id, HttpRequest request, BegrepRequest body, BegrepsregisterTjeneste begrepsregister, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var b = await begrepsregister.OppdaterAsync(id, body.Term, body.Definisjon, body.LovreferanseEid,
                body.GjelderFor, body.KodelisteReferanseId, body.SkosUrl, body.Begrepstype, bruker.Navn, ct);
            return b is null ? Results.NotFound(new { feil = $"Ingen begrep med id '{id}'." }) : Results.Ok(BegrepDto.FraEntitet(b));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OppdaterBegrep")
    .WithSummary("Oppdaterer et begrep.");

begreper.MapPost("/{id:guid}/status", async (Guid id, HttpRequest request, SettStatusRequest body, BegrepsregisterTjeneste begrepsregister, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var b = await begrepsregister.SettStatusAsync(id, body.Status, bruker.Navn, ct, body.GodkjentAv);
            return b is null ? Results.NotFound(new { feil = $"Ingen begrep med id '{id}'." }) : Results.Ok(BegrepDto.FraEntitet(b));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettBegrepStatus")
    .WithSummary("Endrer status (§3.1 i domenemodellen).");

// ---------- «Identifiser begrep» (byggesteg 5 runde 1, docs/06-veikart.md) — stub-KI ----------

begreper.MapGet("/forslag", async (HttpRequest request, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        var forslag = await db.Begreper
            .Where(b => b.VirksomhetId == bruker.VirksomhetId && b.Entitetsstatus == "gjeldende" && b.Status == "foreslatt_av_ai")
            .OrderByDescending(b => b.OpprettetTidspunkt)
            .ToListAsync(ct);
        var resultat = new List<BegrepsforslagDto>();
        foreach (var b in forslag)
        {
            var proveniens = await db.Proveniens
                .Where(p => p.EntitetType == "begrep" && p.EntitetId == b.Id && p.Handling == "foreslatt_av_ai")
                .OrderByDescending(p => p.Dato)
                .FirstOrDefaultAsync(ct);
            resultat.Add(new BegrepsforslagDto(BegrepDto.FraEntitet(b), proveniens?.AiForslagVersjon, proveniens?.Dato ?? b.OpprettetTidspunkt, proveniens?.KildeReferanserJson));
        }
        return Results.Ok(resultat);
    })
    .WithName("HentBegrepsforslagKo")
    .WithSummary("Lister ventende KI-forslag til nye begrep (foreslatt_av_ai).");

begreper.MapPost("/forslag/kjor", async (HttpRequest request, KjorForslagRequest body, BegrepsforslagTjeneste forslagstjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return Results.BadRequest(new { feil = $"Mangler eller ukjent {GjeldendeBrukerTjeneste.HeaderNavn}-header." });
        }
        try
        {
            var resultat = await forslagstjeneste.KjorForslagAsync(bruker.VirksomhetId, body.RettskildeIder, bruker.Navn, ct);
            return Results.Ok(new KjorForslagResponsDto<BegrepDto>(
                resultat.Opprettede.Select(BegrepDto.FraEntitet).ToList(), resultat.InputTokens, resultat.OutputTokens, resultat.Melding));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("KjorBegrepsforslag")
    .WithSummary("Kjører «Identifiser begrep»-agenten mot valgte rettskilder (byggesteg 5 runde 1, stub-KI).");

// ---------- Kodelisteregister / verdidomene (docs/03-domenemodell.md §1.4) — byggesteg 2 ----------
// ---------- Åpne data (som Rettskildebiblioteket) — ekstern-referanse-kodelister er delt/uten     ----------
// ---------- virksomhet og må kunne leses uten X-Bruker-Id.                                        ----------

var kodelister = app.MapGroup("/api/kodelister").WithOpenApi();

kodelister.MapGet("/", async (KodelisteregisterTjeneste kodelisteregister, CancellationToken ct) =>
        Results.Ok((await kodelisteregister.AlleAsync(ct)).Select(KodelisteDto.FraEntitet)))
    .WithName("HentKodelister")
    .WithSummary("Lister alle kodelister (produktkrav kap. 3.7) — juridisk/teknisk/ekstern-referanse.");

kodelister.MapGet("/{id:guid}", async (Guid id, KodelisteregisterTjeneste kodelisteregister, CancellationToken ct) =>
    {
        var k = await kodelisteregister.FinnAsync(id, ct);
        return k is null ? Results.NotFound(new { feil = $"Ingen kodeliste med id '{id}'." }) : Results.Ok(KodelisteDto.FraEntitet(k));
    })
    .WithName("HentKodeliste")
    .WithSummary("Henter én kodeliste, inkl. koder.");

kodelister.MapPost("/", async (HttpRequest request, KodelisteRequest body, KodelisteregisterTjeneste kodelisteregister, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var k = await kodelisteregister.OpprettAsync(body.VirksomhetId, body.Kode, body.Navn, body.Type,
                body.JuridiskGrunnlagEid, body.EksternKildeUri, body.EksternKildeVersjon, bruker.Navn, ct);
            return Results.Created($"/api/kodelister/{k.Id}", KodelisteDto.FraEntitet(k));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettKodeliste")
    .WithSummary("Oppretter en ny kodeliste.");

kodelister.MapPost("/{id:guid}/koder", async (Guid id, LeggTilKodeRequest body, KodelisteregisterTjeneste kodelisteregister, CancellationToken ct) =>
    {
        try
        {
            var kode = await kodelisteregister.LeggTilKodeAsync(id, body.Kode, body.Term, body.Definisjon, body.GyldigFra, body.GyldigTil, ct);
            return kode is null
                ? Results.NotFound(new { feil = $"Ingen kodeliste med id '{id}'." })
                : Results.Created($"/api/kodelister/{id}", KodelisteKodeDto.FraEntitet(kode));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("LeggTilKodelisteKode")
    .WithSummary("Legger til en ny kode i en kodeliste (\"Ny kode\", produktkrav kap. 3.7).");

kodelister.MapDelete("/koder/{kodeId:guid}", async (Guid kodeId, KodelisteregisterTjeneste kodelisteregister, CancellationToken ct) =>
        await kodelisteregister.FjernKodeAsync(kodeId, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen kode med id '{kodeId}'." }))
    .WithName("FjernKodelisteKode")
    .WithSummary("Fjerner en kode fra en kodeliste.");

kodelister.MapPost("/{id:guid}/status", async (Guid id, HttpRequest request, SettStatusRequest body, KodelisteregisterTjeneste kodelisteregister, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var k = await kodelisteregister.SettStatusAsync(id, body.Status, bruker.Navn, ct);
            return k is null ? Results.NotFound(new { feil = $"Ingen kodeliste med id '{id}'." }) : Results.Ok(KodelisteDto.FraEntitet(k));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettKodelisteStatus")
    .WithSummary("Endrer status (§3.1 i domenemodellen) — avvises for ekstern-referanse.");

// ---------- Virksomhetskatalog og rollemodell (docs/20) ----------
// RBAC (docs/20 §0 pkt. 3): skrivehandlinger attribueres til den innloggede brukerens EGEN
// virksomhet/navn (bruker.Navn som opprettetAv/behandletAv) — ingen per-virksomhet skriveperre på
// disse delte, nasjonale tabellene.

app.MapPost("/api/virksomhetsbegrep", async (HttpRequest request, VirksomhetsbegrepRequest body,
        VirksomhetsbegrepTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        try
        {
            var opprettet = await register.OpprettVirksomhetsbegrepAsync(body.VirksomhetId, body.Term, bruker.Navn, body.SkosUrl, ct);
            return Results.Created($"/api/begreper/{opprettet.Id}", BegrepDto.FraEntitet(opprettet));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithOpenApi()
    .WithName("OpprettVirksomhetsbegrep")
    .WithSummary("Navneform brukt om en virksomhet i rettskildetekst (docs/20 §2.3) — f.eks. 'Mattilsynet'.");

app.MapGet("/api/virksomheter/{id:guid}/begrep", async (Guid id, VirksomhetsbegrepTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.AlleVirksomhetsbegrepForAsync(id, ct)).Select(BegrepDto.FraEntitet)))
    .WithOpenApi()
    .WithName("HentVirksomhetsbegrepForVirksomhet")
    .WithSummary("Lister navneformer (inkl. synonymer) brukt om denne virksomheten i rettskildetekst.");

app.MapPost("/api/rollebegrep", async (HttpRequest request, RollebegrepRequest body,
        VirksomhetsbegrepTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        try
        {
            var opprettet = await register.OpprettRollebegrepAsync(body.LovkildeId, body.Term, bruker.Navn, ct);
            return Results.Created($"/api/begreper/{opprettet.Id}", BegrepDto.FraEntitet(opprettet));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithOpenApi()
    .WithName("OpprettRollebegrep")
    .WithSummary("Rollebegrep (docs/20 §2.4) — Term+LovkildeId er sammen begrepets identitet, f.eks. 'forurensningsmyndighet' i forurensningsloven.");

app.MapGet("/api/rettskilder/{lovkildeId:guid}/rollebegrep", async (Guid lovkildeId, VirksomhetsbegrepTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.AlleRollebegrepForLovAsync(lovkildeId, ct)).Select(BegrepDto.FraEntitet)))
    .WithOpenApi()
    .WithName("HentRollebegrepForLov")
    .WithSummary("Lister rollebegrep definert for denne loven.");

app.MapPost("/api/myndighetstildelinger", async (HttpRequest request, MyndighetstildelingRequest body,
        MyndighetstildelingTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        try
        {
            var paragrafspenn = body.Paragrafspenn.Select(p => new ParagrafspennPar(p.FraEid, p.TilEid)).ToList();
            var opprettet = await register.OpprettAsync(
                body.RolleBegrepId, body.VirksomhetId, body.HjemmelRettskildeId, paragrafspenn, body.Vilkaar, bruker.Navn, ct);
            return Results.Created($"/api/myndighetstildelinger/{opprettet.Id}", MyndighetstildelingDto.FraEntitet(opprettet));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithOpenApi()
    .WithName("OpprettMyndighetstildeling")
    .WithSummary("Kobler et rollebegrep til en konkret virksomhet, hjemlet i en forskrift (docs/20 §2.5). Gyldighet arves fra hjemmelen, ingen egne datoer her.");

app.MapGet("/api/virksomheter/{id:guid}/myndighetstildelinger", async (Guid id, MyndighetstildelingTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.AlleForVirksomhetAsync(id, ct)).Select(MyndighetstildelingDto.FraEntitet)))
    .WithOpenApi()
    .WithName("HentMyndighetstildelingerForVirksomhet")
    .WithSummary("Lister myndighetstildelinger denne virksomheten har.");

app.MapGet("/api/rollebegrep/{id:guid}/tildelinger", async (Guid id, MyndighetstildelingTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.AlleForRolleBegrepAsync(id, ct)).Select(MyndighetstildelingDto.FraEntitet)))
    .WithOpenApi()
    .WithName("HentMyndighetstildelingerForRolleBegrep")
    .WithSummary("Lister hvilke virksomheter et rollebegrep er tildelt til, og under hvilke hjemler.");

var virksomhetKandidater = app.MapGroup("/api/virksomhet-kandidater").WithOpenApi();

virksomhetKandidater.MapGet("/", async (Guid? virksomhetId, Guid? rettskildeId, VirksomhetKandidatTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.ListerVentendeAsync(virksomhetId, rettskildeId, ct)).Select(VirksomhetKandidatDto.FraEntitet)))
    .WithName("HentVentendeVirksomhetKandidater")
    .WithSummary("Godkjenningskø (docs/20 §2.6) — kun 'Venter'-rader, valgfritt filtrert til virksomhet og/eller rettskilde.");

virksomhetKandidater.MapPost("/", async (HttpRequest request, VirksomhetKandidatRequest body,
        VirksomhetKandidatTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        try
        {
            var kandidat = await register.OpprettEllerFinnAsync(body.VirksomhetId, body.RettskildeId, body.NodeEid, bruker.Navn, ct);
            return Results.Created($"/api/virksomhet-kandidater/{kandidat.Id}", VirksomhetKandidatDto.FraEntitet(kandidat));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettVirksomhetKandidat")
    .WithSummary("Idempotent — samme (virksomhet, rettskilde, node) gir samme rad tilbake uansett status, ikke et duplikat.");

virksomhetKandidater.MapPost("/{id:guid}/godkjenn", async (Guid id, HttpRequest request,
        VirksomhetKandidatTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        var oppdatert = await register.GodkjennAsync(id, bruker.Navn, ct);
        return oppdatert is null ? Results.NotFound(new { feil = $"Ingen kandidat med id '{id}'." }) : Results.Ok(VirksomhetKandidatDto.FraEntitet(oppdatert));
    })
    .WithName("GodkjennVirksomhetKandidat");

virksomhetKandidater.MapPost("/{id:guid}/avvis", async (Guid id, HttpRequest request,
        VirksomhetKandidatTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        var oppdatert = await register.AvvisAsync(id, bruker.Navn, ct);
        return oppdatert is null ? Results.NotFound(new { feil = $"Ingen kandidat med id '{id}'." }) : Results.Ok(VirksomhetKandidatDto.FraEntitet(oppdatert));
    })
    .WithName("AvvisVirksomhetKandidat");

virksomhetKandidater.MapDelete("/{id:guid}", async (Guid id, VirksomhetKandidatTjeneste register, CancellationToken ct) =>
    {
        try
        {
            return await register.HardslettAvvistAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen kandidat med id '{id}'." });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("HardslettAvvistVirksomhetKandidat")
    .WithSummary("Kun 'Avvist'-rader kan hardslettes (docs/20 §2.6) — et eksplisitt unntak fra husstilens vanlige mykslette-mønster.");

// ---------- Datasett (docs/03-domenemodell.md §1.6) — byggesteg 4, minimal, kun lesing ----------

app.MapGet("/api/datasett", async (RegelIdeDbContext db) =>
        Results.Ok((await db.Datasett.OrderBy(d => d.Felt).ToListAsync()).Select(DatasettDto.FraEntitet)))
    .WithOpenApi()
    .WithName("HentDatasett")
    .WithSummary("Lister datasett (§1.6, minimal — full skjerm er byggesteg 6). Seedet, ingen opprett-UI ennå.");

app.MapGet("/api/datasett/{id:guid}/verdier", async (Guid id, DatasettregisterTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.HentVerdierAsync(id, ct)).Select(DatasettVerdiDto.FraEntitet)))
    .WithOpenApi()
    .WithName("HentDatasettVerdier")
    .WithSummary("Lister kommunale/nasjonale parameterverdier for et datasett-felt (docs/12-fasit-handbok-leveranse.md dimensjon C).");

app.MapPost("/api/datasett/{id:guid}/verdier", async (Guid id, HttpRequest request, SettDatasettVerdiRequest body,
        DatasettregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var v = await register.SettVerdiAsync(id, body.VirksomhetId, body.VerdiJson, body.Kilde, bruker.Navn, ct);
            return Results.Ok(DatasettVerdiDto.FraEntitet(v));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithOpenApi()
    .WithName("SettDatasettVerdi")
    .WithSummary("Setter (upsert) en kommunal eller nasjonal (VirksomhetId=null) parameterverdi.");

app.MapDelete("/api/datasett/verdier/{verdiId:guid}", async (Guid verdiId, DatasettregisterTjeneste register, CancellationToken ct) =>
        await register.FjernVerdiAsync(verdiId, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen verdi med id '{verdiId}'." }))
    .WithOpenApi()
    .WithName("FjernDatasettVerdi")
    .WithSummary("Fjerner en parameterverdi.");

// ---------- Vilkårstre-kommentarer (docs/12-fasit-handbok-leveranse.md "Hovedfunn" + dimensjon A) ----------

var vilkarstreKommentarer = app.MapGroup("/api/vilkarstre-kommentarer").WithOpenApi();

vilkarstreKommentarer.MapGet("/", async (string malType, Guid malId, VilkarstreKommentarTjeneste tjeneste, CancellationToken ct) =>
        Results.Ok((await tjeneste.HentForNodeAsync(malType, malId, ct)).Select(VilkarstreKommentarDto.FraEntitet)))
    .WithName("HentVilkarstreKommentarer")
    .WithSummary("Lister veiledningskommentarer for en vilkårstre-node (?malType=vilkar|regelnode|unntak&malId=).");

vilkarstreKommentarer.MapPost("/", async (HttpRequest request, OpprettVilkarstreKommentarRequest body,
        VilkarstreKommentarTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var k = await tjeneste.OpprettAsync(bruker.VirksomhetId, body.MalType, body.MalId, body.Dokumenttype, body.TekstHtml, bruker.Navn, ct);
            return Results.Created($"/api/vilkarstre-kommentarer/{k.Id}", VilkarstreKommentarDto.FraEntitet(k));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettVilkarstreKommentar")
    .WithSummary("Legger til en veiledningskommentar på en vilkårstre-node.");

vilkarstreKommentarer.MapPut("/{id:guid}", async (Guid id, HttpRequest request, OppdaterVilkarstreKommentarRequest body,
        VilkarstreKommentarTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var k = await tjeneste.OppdaterAsync(id, body.Dokumenttype, body.TekstHtml, bruker.Navn, ct);
            return k is null ? Results.NotFound(new { feil = $"Ingen kommentar med id '{id}'." }) : Results.Ok(VilkarstreKommentarDto.FraEntitet(k));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OppdaterVilkarstreKommentar")
    .WithSummary("Redigerer en veiledningskommentar.");

vilkarstreKommentarer.MapDelete("/{id:guid}", async (Guid id, VilkarstreKommentarTjeneste tjeneste, CancellationToken ct) =>
        await tjeneste.SlettAsync(id, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen kommentar med id '{id}'." }))
    .WithName("SlettVilkarstreKommentar")
    .WithSummary("Fjerner en veiledningskommentar.");

vilkarstreKommentarer.MapPost("/{id:guid}/flytt", async (Guid id, HttpRequest request, FlyttVilkarstreKommentarRequest body,
        VilkarstreKommentarTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var k = await tjeneste.FlyttAsync(id, body.Retning, bruker.Navn, ct);
            return Results.Ok(VilkarstreKommentarDto.FraEntitet(k));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("FlyttVilkarstreKommentar")
    .WithSummary("Flytter en kommentar én posisjon opp/ned blant søsknene sine (bytter Rekkefolge med naboen, aldri en fritt satt verdi).");

// ---------- Vilkårregister (docs/03-domenemodell.md §1.8) — byggesteg 4 runde 1 ----------

var vilkar = app.MapGroup("/api/vilkar").WithOpenApi();

vilkar.MapGet("/", async (HttpRequest request, Guid? tjenesteId, VilkarregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        return Results.Ok((await register.ListerForAsync(bruker.VirksomhetId, tjenesteId, ct)).Select(VilkarDto.FraEntitet));
    })
    .WithName("HentVilkar")
    .WithSummary("Lister virksomhetens egne vilkår (produktkrav kap. 3.4). ?tjenesteId= filtrerer på identifisert tjeneste.");

vilkar.MapGet("/{id:guid}", async (Guid id, VilkarregisterTjeneste register, CancellationToken ct) =>
    {
        var v = await register.FinnAsync(id, ct);
        return v is null ? Results.NotFound(new { feil = $"Ingen vilkår med id '{id}'." }) : Results.Ok(VilkarDto.FraEntitet(v));
    })
    .WithName("HentEttVilkar")
    .WithSummary("Henter ett vilkår.");

vilkar.MapPost("/", async (HttpRequest request, VilkarRequest body, VilkarregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var v = await register.OpprettAsync(bruker.VirksomhetId, body.Tittel, body.Beskrivelse, body.GeneriskMal,
                body.Vilkarstype, body.GjelderRolle, body.JuridiskGrunnlag, body.BegrepId, body.Vurderingstype,
                body.ParametreJson, body.SkjonnsgrunnlagBegrepId, body.Skjonnsmomenter, body.KreverDokumentasjon,
                body.Eskaleringsrolle, body.VeiledningTilBruker, body.VeiledningTilSaksbehandler, body.ErFormel,
                body.FormelBeskrivelse, body.TjenesteId, bruker.Navn, ct);
            return Results.Created($"/api/vilkar/{v.Id}", VilkarDto.FraEntitet(v));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettVilkar")
    .WithSummary("Oppretter et nytt vilkår (utkast).");

vilkar.MapPut("/{id:guid}", async (Guid id, HttpRequest request, VilkarRequest body, VilkarregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var v = await register.OppdaterAsync(id, body.Tittel, body.Beskrivelse, body.GeneriskMal, body.Vilkarstype,
                body.GjelderRolle, body.JuridiskGrunnlag, body.BegrepId, body.Vurderingstype, body.ParametreJson,
                body.SkjonnsgrunnlagBegrepId, body.Skjonnsmomenter, body.KreverDokumentasjon, body.Eskaleringsrolle,
                body.VeiledningTilBruker, body.VeiledningTilSaksbehandler, body.ErFormel, body.FormelBeskrivelse,
                body.TjenesteId, bruker.Navn, ct);
            return v is null ? Results.NotFound(new { feil = $"Ingen vilkår med id '{id}'." }) : Results.Ok(VilkarDto.FraEntitet(v));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OppdaterVilkar")
    .WithSummary("Oppdaterer et vilkår.");

vilkar.MapPost("/{id:guid}/status", async (Guid id, HttpRequest request, SettStatusRequest body, VilkarregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var v = await register.SettStatusAsync(id, body.Status, bruker.Navn, ct);
            return v is null ? Results.NotFound(new { feil = $"Ingen vilkår med id '{id}'." }) : Results.Ok(VilkarDto.FraEntitet(v));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettVilkarStatus")
    .WithSummary("Endrer status (§3.1 i domenemodellen).");

vilkar.MapGet("/{id:guid}/input", async (Guid id, VilkarregisterTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.InputForAsync(id, ct)).Select(DatasettDto.FraEntitet)))
    .WithName("HentVilkarInput")
    .WithSummary("Lister vilkårets input-datasett.");

vilkar.MapPost("/{id:guid}/input", async (Guid id, LeggTilVilkarInputRequest body, VilkarregisterTjeneste register, CancellationToken ct) =>
    {
        try
        {
            var d = await register.LeggTilInputAsync(id, body.DatasettId, ct);
            return Results.Created($"/api/vilkar/{id}/input", DatasettDto.FraEntitet(d));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("LeggTilVilkarInput")
    .WithSummary("Kobler et datasett som input til vilkåret.");

vilkar.MapDelete("/{id:guid}/input/{datasettId:guid}", async (Guid id, Guid datasettId, VilkarregisterTjeneste register, CancellationToken ct) =>
        await register.FjernInputAsync(id, datasettId, ct) ? Results.NoContent() : Results.NotFound(new { feil = "Fant ingen slik input-kobling." }))
    .WithName("FjernVilkarInput")
    .WithSummary("Fjerner en input-datasett-kobling fra vilkåret.");

vilkar.MapGet("/{id:guid}/historikk", async (Guid id, RegelIdeDbContext db) =>
        Results.Ok((await db.Proveniens.Where(p => p.EntitetType == "vilkar" && p.EntitetId == id)
            .OrderByDescending(p => p.Dato).ToListAsync()).Select(ProveniensDto.FraEntitet)))
    .WithName("HentVilkarHistorikk")
    .WithSummary("Proveniens for vilkåret.");

// ---------- Regelnoderegister (docs/03-domenemodell.md §1.9) — byggesteg 4 runde 1 ----------

var regelnoder = app.MapGroup("/api/regelnoder").WithOpenApi();

regelnoder.MapGet("/", async (HttpRequest request, RegelnoderegisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        return Results.Ok((await register.ListerForAsync(bruker.VirksomhetId, ct)).Select(RegelnodeDto.FraEntitet));
    })
    .WithName("HentRegelnoder")
    .WithSummary("Lister virksomhetens egne regelnoder.");

regelnoder.MapGet("/{id:guid}", async (Guid id, RegelnoderegisterTjeneste register, CancellationToken ct) =>
    {
        var r = await register.FinnAsync(id, ct);
        return r is null ? Results.NotFound(new { feil = $"Ingen regelnode med id '{id}'." }) : Results.Ok(RegelnodeDto.FraEntitet(r));
    })
    .WithName("HentEnRegelnode")
    .WithSummary("Henter én regelnode.");

regelnoder.MapPost("/", async (HttpRequest request, RegelnodeRequest body, RegelnoderegisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var r = await register.OpprettAsync(bruker.VirksomhetId, body.Tittel, body.Beskrivelse, body.GeneriskMal,
                body.BarnOperator, body.UtdataNavn, body.UtdataType, body.ErRotnode, body.JuridiskGrunnlag,
                body.InnvilgelseTekst, body.AvslagTekst, bruker.Navn, ct);
            return Results.Created($"/api/regelnoder/{r.Id}", RegelnodeDto.FraEntitet(r));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettRegelnode")
    .WithSummary("Oppretter en ny regelnode (utkast).");

regelnoder.MapPut("/{id:guid}", async (Guid id, HttpRequest request, RegelnodeRequest body, RegelnoderegisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var r = await register.OppdaterAsync(id, body.Tittel, body.Beskrivelse, body.GeneriskMal, body.UtdataNavn,
                body.UtdataType, body.JuridiskGrunnlag, body.InnvilgelseTekst, body.AvslagTekst, bruker.Navn, ct);
            return r is null ? Results.NotFound(new { feil = $"Ingen regelnode med id '{id}'." }) : Results.Ok(RegelnodeDto.FraEntitet(r));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OppdaterRegelnode")
    .WithSummary("Oppdaterer en regelnode.");

regelnoder.MapPut("/{id:guid}/operator", async (Guid id, HttpRequest request, SettOperatorRequest body, RegelnoderegisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var r = await register.SettOperatorAsync(id, body.BarnOperator, bruker.Navn, ct);
            return r is null ? Results.NotFound(new { feil = $"Ingen regelnode med id '{id}'." }) : Results.Ok(RegelnodeDto.FraEntitet(r));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettRegelnodeOperator")
    .WithSummary("Endrer barn_operator (OG/ELLER/IKKE, AK-3.4.2).");

regelnoder.MapPost("/{id:guid}/status", async (Guid id, HttpRequest request, SettStatusRequest body, RegelnoderegisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var r = await register.SettStatusAsync(id, body.Status, bruker.Navn, ct);
            return r is null ? Results.NotFound(new { feil = $"Ingen regelnode med id '{id}'." }) : Results.Ok(RegelnodeDto.FraEntitet(r));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettRegelnodeStatus")
    .WithSummary("Endrer status (§3.1 i domenemodellen).");

regelnoder.MapGet("/{id:guid}/barn", async (Guid id, RegelnoderegisterTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.BarnForAsync(id, ct)).Select(RegelnodeBarnDto.FraEntitet)))
    .WithName("HentRegelnodeBarn")
    .WithSummary("Lister regelnodens barn (Vilkår- eller Regelnode-referanser).");

regelnoder.MapPost("/{id:guid}/barn", async (Guid id, KobleBarnRequest body, RegelnoderegisterTjeneste register, CancellationToken ct) =>
    {
        try
        {
            var b = await register.KobleBarnAsync(id, body.BarnType, body.BarnId, ct);
            return Results.Created($"/api/regelnoder/{id}/barn", RegelnodeBarnDto.FraEntitet(b));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("KobleRegelnodeBarn")
    .WithSummary("Kobler et barn (Vilkår eller Regelnode) til regelnoden — validerer DAG (AK-3.4.6, INV-7).");

regelnoder.MapDelete("/{id:guid}/barn/{barnType}/{barnId:guid}", async (Guid id, string barnType, Guid barnId, RegelnoderegisterTjeneste register, CancellationToken ct) =>
        await register.FjernBarnAsync(id, barnType, barnId, ct) ? Results.NoContent() : Results.NotFound(new { feil = "Fant ingen slik barn-kobling." }))
    .WithName("FjernRegelnodeBarn")
    .WithSummary("Fjerner en barn-kobling fra regelnoden.");

regelnoder.MapGet("/{id:guid}/historikk", async (Guid id, RegelIdeDbContext db) =>
        Results.Ok((await db.Proveniens.Where(p => p.EntitetType == "regelnode" && p.EntitetId == id)
            .OrderByDescending(p => p.Dato).ToListAsync()).Select(ProveniensDto.FraEntitet)))
    .WithName("HentRegelnodeHistorikk")
    .WithSummary("Proveniens for regelnoden.");

// ---------- Unntaksregister (docs/03-domenemodell.md §1.10) — byggesteg 4 runde 1 ----------

var unntak = app.MapGroup("/api/unntak").WithOpenApi();

unntak.MapGet("/", async (HttpRequest request, UnntaksregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        return Results.Ok((await register.ListerForAsync(bruker.VirksomhetId, ct)).Select(UnntakDto.FraEntitet));
    })
    .WithName("HentUnntak")
    .WithSummary("Lister virksomhetens egne unntak.");

unntak.MapGet("/{id:guid}", async (Guid id, UnntaksregisterTjeneste register, CancellationToken ct) =>
    {
        var u = await register.FinnAsync(id, ct);
        return u is null ? Results.NotFound(new { feil = $"Ingen unntak med id '{id}'." }) : Results.Ok(UnntakDto.FraEntitet(u));
    })
    .WithName("HentEttUnntak")
    .WithSummary("Henter ett unntak.");

unntak.MapPost("/", async (HttpRequest request, OpprettUnntakRequest body, UnntaksregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var u = await register.OpprettAsync(bruker.VirksomhetId, body.Tittel, body.Beskrivelse, body.GjelderRegelId,
                body.BetingelseType, body.BetingelseId, body.JuridiskGrunnlag, bruker.Navn, ct);
            return Results.Created($"/api/unntak/{u.Id}", UnntakDto.FraEntitet(u));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettUnntak")
    .WithSummary("Oppretter et unntak — krever gjelderRegelId og betingelse (INV-3/INV-4).");

unntak.MapPut("/{id:guid}", async (Guid id, HttpRequest request, OppdaterUnntakRequest body, UnntaksregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var u = await register.OppdaterAsync(id, body.Tittel, body.Beskrivelse, body.JuridiskGrunnlag, bruker.Navn, ct);
            return u is null ? Results.NotFound(new { feil = $"Ingen unntak med id '{id}'." }) : Results.Ok(UnntakDto.FraEntitet(u));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OppdaterUnntak")
    .WithSummary("Oppdaterer et unntak (tittel/beskrivelse/juridisk grunnlag — ikke gjelder_regel/betingelse).");

unntak.MapPost("/{id:guid}/status", async (Guid id, HttpRequest request, SettStatusRequest body, UnntaksregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var u = await register.SettStatusAsync(id, body.Status, bruker.Navn, ct);
            return u is null ? Results.NotFound(new { feil = $"Ingen unntak med id '{id}'." }) : Results.Ok(UnntakDto.FraEntitet(u));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettUnntakStatus")
    .WithSummary("Endrer status (§3.1 i domenemodellen).");

unntak.MapGet("/{id:guid}/historikk", async (Guid id, RegelIdeDbContext db) =>
        Results.Ok((await db.Proveniens.Where(p => p.EntitetType == "unntak" && p.EntitetId == id)
            .OrderByDescending(p => p.Dato).ToListAsync()).Select(ProveniensDto.FraEntitet)))
    .WithName("HentUnntakHistorikk")
    .WithSummary("Proveniens for unntaket.");

// ---------- Vilkårstre-kobling på Tjeneste (byggesteg 4 — lukker gapet fra byggesteg 2) ----------

tjenester.MapPost("/{id:guid}/rotnode", async (Guid id, HttpRequest request, SettRotnodeRequest body, TjenesteregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var t = await register.SettRotnodeAsync(id, bruker.VirksomhetId, body.RegelnodeId, ct);
            return t is null ? Results.NotFound(new { feil = $"Ingen tjeneste med id '{id}'." }) : Results.Ok(TjenesteDto.FraEntitet(t));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("SettTjenesteRotnode")
    .WithSummary("Kobler tjenesten til rotnoden i sitt vilkårstre (byggesteg 4).");

tjenester.MapDelete("/{id:guid}/rotnode", async (Guid id, HttpRequest request, TjenesteregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        var t = await register.FjernRotnodeAsync(id, bruker.VirksomhetId, ct);
        return t is null ? Results.NotFound(new { feil = $"Ingen tjeneste med id '{id}'." }) : Results.Ok(TjenesteDto.FraEntitet(t));
    })
    .WithName("FjernTjenesteRotnode")
    .WithSummary("Fjerner koblingen til rotnoden (selve regelnoden slettes ikke) — gjør en feilaktig opprettelse reversibel.");

tjenester.MapGet("/{id:guid}/veiledning", async (Guid id, Guid? virksomhetId, VeiledningRepository repo, CancellationToken ct) =>
    {
        var veiledning = await repo.ByggAsync(id, virksomhetId, ct);
        return veiledning is null
            ? Results.NotFound(new { feil = $"Tjenesten '{id}' finnes ikke, eller har ingen rotnode i vilkårstreet." })
            : Results.Ok(veiledning);
    })
    .WithName("HentTjenesteVeiledning")
    .WithSummary(
        "Vilkårstreet rendret som en tjenestesentrert veiledning i beslutningsorden " +
        "(docs/12-fasit-handbok-leveranse.md \"Hovedfunn\"). ?virksomhetId= velger hvilken kommunes " +
        "datasett-verdier som vises — utelatt/ingen treff faller tilbake til den nasjonale standardverdien.");

// ---------- Hendelseregister (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1) ----------

var hendelser = app.MapGroup("/api/hendelser").WithOpenApi();

hendelser.MapGet("/", async (HttpRequest request, HendelseregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        var liste = await register.ListerAsync(bruker?.VirksomhetId, ct);
        return Results.Ok(liste.Select(HendelseDto.FraEntitet));
    })
    .WithName("HentHendelser")
    .WithSummary("Lister nasjonale/delte hendelser pluss (hvis en gyldig testbruker er valgt) virksomhetens egne lokale hendelser.");

hendelser.MapPost("/", async (HttpRequest request, HendelseRequest body, HendelseregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            var h = await register.OpprettAsync(bruker.VirksomhetId, body.Navn, body.Type, body.Beskrivelse, bruker.Navn, ct);
            return Results.Created($"/api/hendelser/{h.Id}", HendelseDto.FraEntitet(h));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettHendelse")
    .WithSummary("Oppretter en ny hendelse, virksomhetseid (satt VirksomhetId) — nasjonale/delte hendelser opprettes ikke via denne v1-flyten.");

tjenester.MapGet("/{id:guid}/hendelser", async (Guid id, HendelseregisterTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.ListerForTjenesteAsync(id, ct)).Select(HendelseDto.FraEntitet)))
    .WithName("HentTjenesteHendelser")
    .WithSummary("Lister hendelsene som klassifiserer denne tjenesten (cpsv:isClassifiedBy — symmetrisk, ingen retning).");

tjenester.MapPost("/{id:guid}/hendelser", async (Guid id, KobleHendelseRequest body, HendelseregisterTjeneste register, CancellationToken ct) =>
    {
        try
        {
            await register.KobleTilTjenesteAsync(id, body.HendelseId, ct);
            return Results.Ok((await register.ListerForTjenesteAsync(id, ct)).Select(HendelseDto.FraEntitet));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("KobleTjenesteHendelse")
    .WithSummary("Klassifiserer tjenesten ved en hendelse.");

tjenester.MapDelete("/{id:guid}/hendelser/{hendelseId:guid}", async (Guid id, Guid hendelseId, HendelseregisterTjeneste register, CancellationToken ct) =>
        await register.FjernFraTjenesteAsync(id, hendelseId, ct)
            ? Results.NoContent()
            : Results.NotFound(new { feil = "Fant ingen slik klassifisering." }))
    .WithName("FjernTjenesteHendelse")
    .WithSummary("Fjerner klassifiseringen (selve hendelsen slettes ikke).");

// ---------- Tjenesteavhengighetregister (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1) ----------

tjenester.MapGet("/{id:guid}/avhengigheter", async (Guid id, TjenesteavhengighetregisterTjeneste register, CancellationToken ct) =>
        Results.Ok((await register.HentForTjenesteAsync(id, ct)).Select(TjenesteavhengighetDto.FraVisning)))
    .WithName("HentTjenesteavhengigheter")
    .WithSummary(
        "Lister tjenestens avhengigheter i BEGGE retninger (der tjenesten er Fra, og der den er Til) " +
        "med ferdig beregnet visningstekst — ett rettet kant per relasjon, ingen duplisert lagring.");

// ---------- Tjenesteeksport (2026-08-20) — ett samlet JSON-dokument for én tjeneste ----------

tjenester.MapGet("/{id:guid}/eksport", async (Guid id, TjenesteEksportTjeneste eksport, CancellationToken ct) =>
    {
        var resultat = await eksport.EksporterAsync(id, ct);
        if (resultat is null) return Results.NotFound(new { feil = $"Ingen tjeneste med id '{id}'." });

        return Results.Ok(new TjenesteEksportDto(
            TjenesteDto.FraEntitet(resultat.Tjeneste), resultat.VirksomhetNavn,
            resultat.Regelverksreferanser.Select(TjenesteRegelverksreferanseDto.FraEntitet).ToList(),
            resultat.Hendelser.Select(HendelseDto.FraEntitet).ToList(),
            resultat.Avhengigheter.Select(TjenesteavhengighetDto.FraVisning).ToList(),
            resultat.EksportertTidspunkt));
    })
    .WithName("EksporterTjeneste")
    .WithSummary(
        "Ett samlet JSON-dokument for tjenestens KJERNEMODELL — egenskaper, regelverksreferanser, " +
        "hendelser og tjenesteavhengigheter (inkl. eksterne plassholder-referanser). BEVISST uten " +
        "vilkårstre (egen, senere avklaring). Rent sammensatt leseendepunkt, ingen egen lagret representasjon.");

tjenester.MapGet("/{id:guid}/modelleksport", async (Guid id, RettighetModellEksportTjeneste eksport, CancellationToken ct) =>
    {
        var resultat = await eksport.EksporterAsync(id, ct);
        return resultat is null
            ? Results.NotFound(new { feil = $"Ingen tjeneste med id '{id}'." })
            : Results.Text(resultat.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), "application/json");
    })
    .WithName("EksporterRettighetModell")
    .WithSummary(
        "Eksporterer én Rettighet (Tjeneste) formet EKSAKT som rettigheter[]-elementene i den " +
        "hånd-modellerte serveringsbevilling-modell-forslag.json — snake_case feltnavn, samme " +
        "nøsting (innhold/regelverksreferanser/handlinger/avhengigheter). Til bruk i modell-vs-app " +
        "verifisering, ikke et alternativ til /eksport (som er det flate CPSV-dokumentet).");

tjenester.MapPost("/{id:guid}/avhengigheter", async (Guid id, HttpRequest request, TjenesteavhengighetRequest body, TjenesteavhengighetregisterTjeneste register, RegelIdeDbContext db, CancellationToken ct) =>
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null)
        {
            return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        }
        try
        {
            await register.OpprettAsync(
                bruker.VirksomhetId, id, body.TilTjenesteId, body.Rel, body.HendelseId, body.Beskrivelse, bruker.Navn,
                body.TilOrganisasjonsnummer, body.TilNavn, body.TilUrl, ct);
            return Results.Ok((await register.HentForTjenesteAsync(id, ct)).Select(TjenesteavhengighetDto.FraVisning));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    })
    .WithName("OpprettTjenesteavhengighet")
    .WithSummary("Oppretter en rettet avhengighet FRA denne tjenesten TIL en annen.");

tjenester.MapDelete("/avhengigheter/{avhengighetId:guid}", async (Guid avhengighetId, TjenesteavhengighetregisterTjeneste register, CancellationToken ct) =>
        await register.SlettAsync(avhengighetId, ct) ? Results.NoContent() : Results.NotFound(new { feil = $"Ingen avhengighet med id '{avhengighetId}'." }))
    .WithName("SlettTjenesteavhengighet")
    .WithSummary("Sletter en tjenesteavhengighet.");

// SPA-ruting: /rettskilder/{id} o.l. er klientruter uten motstykke på serveren, så alt som
// ikke traff et API-endepunkt eller en fil sendes til index.html. Gjør ingenting når wwwroot
// er tom (utviklingsoppsettet, der Vite serverer klienten selv).
//
// Vi serverer innholdet selv i stedet for MapFallbackToFile, fordi <base href> må settes til
// sti-prefikset. Klientens asset- og API-URL-er er relative (vite base: './'), og løses mot
// denne. Uten omskrivingen ville en reload på /{org}/{app}/vilkarstre løst «assets/...» mot
// .../vilkarstre/ og gitt 404 — nettopp det man ikke oppdager ved bare å laste forsiden.
var indeksfil = Path.Combine(app.Environment.WebRootPath ?? "", "index.html");
if (File.Exists(indeksfil))
{
    var indeksinnhold = Stiprefiks.SettBaseHref(File.ReadAllText(indeksfil), stiprefiks);

    app.MapFallback(() => Results.Content(indeksinnhold, "text/html", Encoding.UTF8));
}

app.Run();

public partial class Program; // synlig for WebApplicationFactory<Program> i integrasjonstester
