using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RegelIde.Data;

/// <summary>
/// docs/31-navneform-berikelse-snl-ssr-spesifikasjon.md §3 — live per-term-oppslag MOT to eksterne,
/// levende API-er (Store norske leksikon og Kartverkets Sentralt stadnamnregister), MED lokal cache
/// (<see cref="EksternNavneoppslagCacheEntitet"/>). Brukt av
/// <see cref="NavnekandidatOppdagelseTjeneste.SveipStorBokstavAsync"/> til å klassifisere "stor
/// bokstav midt i en setning"-treff som institusjonsnavn (SNL), geografisk stedsnavn (SSR), eller
/// ukjent (ingen av dem) — se selve klassifiseringskjeden der (docs/31 §2).
/// <para>
/// <b>Bevisst IKKE bulk-nedlasting/skraping</b> (docs/31 §3) — begge metodene her slår opp NØYAKTIG
/// ÉN term per kall, cache-oppslag FØRST, live HTTP-kall KUN ved cache-miss.
/// </para>
/// <para>
/// <b>Nettverksfeil skal ALDRI stoppe et sveip</b> (docs/31 §3): en <see cref="HttpRequestException"/>,
/// en timeout (<see cref="TaskCanceledException"/> — men KUN når den IKKE stammer fra selve
/// <see cref="CancellationToken"/> calleren ga oss, se <c>when</c>-vernet på catch-blokkene under) eller
/// et uventet/ugyldig JSON-svar (<see cref="JsonException"/>) fanges, logges som en advarsel, og
/// behandles som <see cref="EksternOppslagResultat.IngenTreff"/> — semantisk identisk med et EKTE "API-et
/// fant ingenting", nettopp fordi docs/31 §3 sier "behandles som «ukjent» (samme som «ingen treff»)".
/// </para>
/// <para>
/// <b>Et nettverksfeil-resultat skrives ALDRI til cachen</b> — se <see cref="EksternNavneoppslagCacheEntitet"/>s
/// klassekommentar for hvorfor: cachen har ingen TTL i denne runden, og å cache en FORBIGÅENDE feil som
/// permanent "ingen treff" ville forgiftet den for alltid. Kun et ekte API-svar (uansett om det er et
/// treff eller et bekreftet fravær av treff) skrives til <see cref="EksternNavneoppslagCacheEntitet"/>.
/// </para>
/// </summary>
public sealed class EksternNavneoppslagTjeneste(
    HttpClient http, RegelIdeDbContext db, ILogger<EksternNavneoppslagTjeneste>? logger = null)
{
    private readonly ILogger<EksternNavneoppslagTjeneste> _logger = logger ?? NullLogger<EksternNavneoppslagTjeneste>.Instance;

    /// <summary>
    /// Ingen dokumentert API-nøkkel eller ratelimit (docs/31 §1.1, verifisert live). Hardkodet konstant,
    /// samme "ingen konfig nødvendig for et nøkkelfritt, offentlig API"-linje som
    /// <see cref="LovdataBulkHenter"/>/<see cref="BrregKlient"/> allerede følger i denne kodebasen.
    /// </summary>
    private const string SnlBaseUrl = "https://snl.no";

    /// <summary>
    /// Kartverkets indekserte stedsnavn-søk (docs/31 §1.2) — funnet/verifisert live under denne
    /// byggerunden (spesifikasjonen selv dokumenterte KUN at et slikt REST-søk finnes, ikke eksakt
    /// URL). Dokumentasjon: https://www.kartverket.no/api-og-data/stedsnavndata/brukarrettleiing-stadnamn-api
    /// (Swagger/OpenAPI på selve base-URL-en). Ingen API-nøkkel/registrering, ingen dokumentert ratelimit.
    /// </summary>
    private const string SsrBaseUrl = "https://ws.geonorge.no/stedsnavn/v1";

    /// <summary>
    /// SNLs <c>article_type_id</c> for en organisasjons-/institusjonsartikkel — verifisert LIVE (ikke
    /// antatt) mot flere kjente institusjoner: "Den Norske Advokatforening", "Miljødirektoratet",
    /// "Datatilsynet", "Høyre" (politisk parti) ga ALLE <c>article_type_id: 16</c>, mens personer
    /// ("Erna Solberg") ga 2/43 og steder ("Bergen") ga 1/10/11. Brukt i stedet for spesifikasjonens
    /// opprinnelige forslag om å sjekke MOT en kuratert liste av taksonomi-id-er (f.eks. ".taxonomy/3103"
    /// "Myndigheter i Norge") — det forslaget ville IKKE fanget f.eks. Advokatforeningen (verifisert
    /// live: <c>taxonomy_id 744</c>, "Arbeidslivsorganisasjoner", en privat interesseorganisasjon, ikke
    /// et offentlig myndighetsorgan), som er nøyaktig det Johann selv ba om å teste klassifiseringen
    /// mot. <c>article_type_id</c> er dessuten en LUKKET, liten kodeliste (artikkel-TYPE), mens
    /// taksonomi-kategorier er et åpent, stort og voksende tre — samme "lukket signal fremfor et
    /// uttømmende, sprekkfylt utvalg"-begrunnelse som resten av denne kodebasen bruker på lignende valg
    /// (jf. <c>Institusjonsord</c>/<c>VerketDenyliste</c> i <see cref="NavnekandidatOppdagelseTjeneste"/>).
    /// </summary>
    private const int SnlOrganisasjonsArtikkeltype = 16;

    /// <summary>
    /// docs/31 §2 punkt 1 — SNL-søk. Cache-oppslag først (<see cref="Kilde"/> = <c>"snl"</c>).
    /// <para>
    /// <b>Matchelogikk (docs/31 §6 — bygget bevisst, ikke gjettet):</b> søk-API-et er FULLTEKST-søk, ikke
    /// et rent titteloppslag — et strengt "headword == term"-krav ville FEILET på selve Johanns eget
    /// testeksempel: søk på "Advokatforeningen" gir IKKE noe treff med headword nøyaktig
    /// "Advokatforeningen" (den offisielle artikkelen heter "Den Norske Advokatforening"; "Advokatforeningen"
    /// er derimot artikkelens eget "også kjent som"-alias). Løsning: for HVERT søketreff med
    /// <see cref="SnlOrganisasjonsArtikkeltype"/>, hent selve artikkelens faktaboks (ett ekstra kall, KUN
    /// for organisasjonstype-treff — ikke for hvert søketreff) og godkjenn som ekte match hvis
    /// <paramref name="term"/> case-insensitivt tilsvarer artikkelens <c>headword</c>,
    /// <c>organization_name</c>, ELLER et av de utpakkede alias-navnene fra <c>alternative_form</c>.
    /// </para>
    /// </summary>
    public Task<EksternOppslagResultat> SlaOppSnlAsync(string term, CancellationToken ct = default) =>
        SlaOppAsync(term, "snl", () => SlaOppSnlLiveAsync(term.Trim(), ct), ct);

    /// <summary>
    /// docs/31 §2 punkt 2 — SSR-oppslag. Til forskjell fra SNL er dette et rent stedsnavn-REGISTER
    /// (ikke fulltekst-søk over løpende artikkeltekst), så et EKSAKT (case-insensitivt) treff på
    /// <c>skrivemåte</c> er riktig presisjonsnivå her — ingen tilsvarende alias-oppslag nødvendig.
    /// Selve "er det et institusjonsord RETT ETTER i løpeteksten"-sjekken (docs/31 §2 punkt 2, andre
    /// halvdel) skjer IKKE her — denne metoden svarer kun "er dette et bekreftet stedsnavn", selve
    /// beslutningen om å forkaste/beholde tas av kalleren (<see cref="NavnekandidatOppdagelseTjeneste"/>),
    /// som allerede har løpeteksten og <c>Institusjonsord</c>-listen.
    /// </summary>
    public Task<EksternOppslagResultat> SlaOppSsrAsync(string term, CancellationToken ct = default) =>
        SlaOppAsync(term, "ssr", () => SlaOppSsrLiveAsync(term.Trim(), ct), ct);

    /// <summary>Delt cache-først/live-ved-miss/aldri-krasj-orkestrering for begge kildene — se
    /// klassekommentaren.</summary>
    private async Task<EksternOppslagResultat> SlaOppAsync(
        string term, string kilde, Func<Task<EksternOppslagResultat>> slaOppLive, CancellationToken ct)
    {
        var normalisert = term.Trim().ToLowerInvariant();
        var cached = await LesFraCacheAsync(normalisert, kilde, ct);
        if (cached is not null) return cached;

        EksternOppslagResultat resultat;
        try
        {
            resultat = await slaOppLive();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                    && !ct.IsCancellationRequested)
        {
            // Nettverksfeil/uventet svar — se klassekommentaren. IKKE cachet (returnerer direkte, uten
            // å nå SkrivTilCacheAsync under).
            _logger.LogWarning(ex, "Ekstern navneoppslag ({Kilde}) feilet for '{Term}' — behandlet som ukjent, ikke cachet.", kilde, term);
            return EksternOppslagResultat.IngenTreff;
        }

        await SkrivTilCacheAsync(normalisert, kilde, resultat, ct);
        return resultat;
    }

    private async Task<EksternOppslagResultat?> LesFraCacheAsync(string normalisertTerm, string kilde, CancellationToken ct)
    {
        var rad = await db.EksternNavneoppslagCache.FirstOrDefaultAsync(
            c => c.Term == normalisertTerm && c.Kilde == kilde, ct);
        if (rad is null) return null;
        var alias = rad.AliasJson is null ? null : JsonSerializer.Deserialize<List<string>>(rad.AliasJson);
        return new EksternOppslagResultat(rad.Treff, rad.TaksonomiKategori, rad.EksternUrl, alias, rad.OrganisasjonsnummerFunnet, rad.BekreftetNavn);
    }

    private async Task SkrivTilCacheAsync(string normalisertTerm, string kilde, EksternOppslagResultat resultat, CancellationToken ct)
    {
        var rad = new EksternNavneoppslagCacheEntitet
        {
            Id = Guid.NewGuid(),
            Term = normalisertTerm,
            Kilde = kilde,
            Treff = resultat.Treff,
            TaksonomiKategori = resultat.TaksonomiKategori,
            AliasJson = resultat.Alias is null ? null : JsonSerializer.Serialize(resultat.Alias),
            OrganisasjonsnummerFunnet = resultat.Organisasjonsnummer,
            EksternUrl = resultat.EksternUrl,
            BekreftetNavn = resultat.BekreftetNavn,
            SlaOppTidspunkt = DateTimeOffset.UtcNow,
        };
        db.EksternNavneoppslagCache.Add(rad);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Racy sjekk-så-sett uten låsing (samme mønster som
            // NavnekandidatOppdagelseTjeneste.OpprettEllerFinnAsync) — to overlappende sveip kan begge
            // ha passert cache-miss-sjekken før noen av dem committer. Den andre skriveren har allerede
            // vunnet og skrevet samme (Term, Kilde) — trygt å bare forkaste vårt eget skriveforsøk.
            db.Entry(rad).State = EntityState.Detached;
        }
    }

    private async Task<EksternOppslagResultat> SlaOppSnlLiveAsync(string term, CancellationToken ct)
    {
        var url = $"{SnlBaseUrl}/api/v1/search?query={Uri.EscapeDataString(term)}&limit=3";
        var treffliste = await http.GetFromJsonAsync<List<SnlSokeTreff>>(url, ct) ?? [];

        foreach (var treff in treffliste.Where(t => t.ArticleTypeId == SnlOrganisasjonsArtikkeltype))
        {
            if (string.IsNullOrEmpty(treff.ArticleUrlJson)) continue;

            var artikkel = await http.GetFromJsonAsync<SnlArtikkel>(treff.ArticleUrlJson, ct);
            var metadata = artikkel?.Metadata;
            if (metadata is null) continue;

            var alias = ParseAlias(metadata.AlternativeForm);
            var kandidatnavn = new[] { artikkel!.Headword, metadata.OrganizationName }
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Concat(alias);
            if (!kandidatnavn.Any(n => string.Equals(n, term, StringComparison.OrdinalIgnoreCase))) continue;

            // BekreftetNavn: SNLs egen, normalt skrevne form av navnet (til forskjell fra `term`,
            // som er den rå, evt. VERSALE strengen kalleren slo opp) — brukt av #158 til å
            // foreslå en navneform ved Brreg-import. Headword foretrekkes (artikkelens tittel);
            // organization_name er fallback for artikler uten et rent institusjonsnavn som headword.
            var bekreftetNavn = !string.IsNullOrWhiteSpace(artikkel!.Headword) ? artikkel.Headword : metadata.OrganizationName;

            return new EksternOppslagResultat(
                Treff: true,
                TaksonomiKategori: treff.TaxonomyTitle,
                EksternUrl: artikkel.Url ?? treff.ArticleUrl,
                Alias: alias.Count == 0 ? null : alias,
                Organisasjonsnummer: metadata.OrganizationNumber,
                BekreftetNavn: bekreftetNavn);
        }

        return EksternOppslagResultat.IngenTreff;
    }

    private async Task<EksternOppslagResultat> SlaOppSsrLiveAsync(string term, CancellationToken ct)
    {
        var url = $"{SsrBaseUrl}/navn?sok={Uri.EscapeDataString(term)}&treffPerSide=5";
        var svar = await http.GetFromJsonAsync<SsrSokeSvar>(url, ct);
        var treff = svar?.Navn?.FirstOrDefault(n => string.Equals(n.Skrivemate, term, StringComparison.OrdinalIgnoreCase));
        if (treff is null) return EksternOppslagResultat.IngenTreff;

        // Ingen ekstern URL herfra — SSR har ikke noe tilsvarende artikkel-lenke-felt som SNL
        // (kun et internt stedsnummer), og ingen offentlig, stabil per-stedsnavn-URL er verifisert i
        // denne byggerunden. Ingen gjettet fallback.
        return new EksternOppslagResultat(true, treff.Navneobjekttype, null, null, null);
    }

    /// <summary>
    /// Pakker ut SNL-faktaboksens <c>alternative_form</c> ("også kjent som") — HTML-fragment, f.eks.
    /// <c>"&lt;p&gt;Advokatforeningen&lt;/p&gt;"</c> — til en flat liste med rene navn. Fjerner
    /// HTML-tagger, HTML-dekoder entiteter, og splitter deretter på komma/semikolon (heuristikk: feltet
    /// er ikke dokumentert som ALLTID nøyaktig ett navn — verifisert kun mot ETT eksempel, se docs/31
    /// §1.1). Tomme/whitespace-only fragmenter forkastes.
    /// </summary>
    private static List<string> ParseAlias(string? alternativeFormHtml)
    {
        if (string.IsNullOrWhiteSpace(alternativeFormHtml)) return [];
        var utenTagger = HtmlTaggMønster.Replace(alternativeFormHtml, " ");
        var dekodet = System.Net.WebUtility.HtmlDecode(utenTagger);
        return dekodet.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static readonly Regex HtmlTaggMønster = new(@"<[^>]+>");

    // ---- SNL JSON-modeller (kun feltene vi faktisk bruker — verifisert live mot snl.no/api/v1/search
    // og snl.no/<artikkel>.json under denne byggerunden) ----

    private sealed class SnlSokeTreff
    {
        [JsonPropertyName("article_type_id")] public int ArticleTypeId { get; set; }
        [JsonPropertyName("taxonomy_title")] public string? TaxonomyTitle { get; set; }
        [JsonPropertyName("article_url")] public string? ArticleUrl { get; set; }
        [JsonPropertyName("article_url_json")] public string? ArticleUrlJson { get; set; }
    }

    private sealed class SnlArtikkel
    {
        [JsonPropertyName("headword")] public string? Headword { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("metadata")] public SnlArtikkelMetadata? Metadata { get; set; }
    }

    private sealed class SnlArtikkelMetadata
    {
        [JsonPropertyName("organization_name")] public string? OrganizationName { get; set; }
        [JsonPropertyName("organization_number")] public string? OrganizationNumber { get; set; }
        // "også kjent som" — fritt lisensiert metadata-/faktaboksfelt (docs/31 §1.1), IKKE løpetekst.
        [JsonPropertyName("alternative_form")] public string? AlternativeForm { get; set; }
    }

    // ---- SSR JSON-modeller (kun feltene vi faktisk bruker — verifisert live mot
    // ws.geonorge.no/stedsnavn/v1/navn under denne byggerunden) ----

    private sealed class SsrSokeSvar
    {
        [JsonPropertyName("navn")] public List<SsrNavn>? Navn { get; set; }
    }

    private sealed class SsrNavn
    {
        [JsonPropertyName("skrivemåte")] public string? Skrivemate { get; set; }
        [JsonPropertyName("navneobjekttype")] public string? Navneobjekttype { get; set; }
    }
}

/// <summary>
/// Resultat av ETT eksternt navneoppslag (SNL eller SSR) — se <see cref="EksternNavneoppslagTjeneste"/>.
/// <see cref="IngenTreff"/> brukes BÅDE for et ekte "API-et fant ingenting"-svar OG for "nettverksfeil,
/// behandlet som ukjent" — docs/31 §3 sier disse to skal være semantisk identiske for KLASSIFISERINGEN
/// (kallerens beslutning). Kun selve CACHE-SKRIVINGEN (i <see cref="EksternNavneoppslagTjeneste"/>,
/// ikke her) skiller dem, ved at et nettverksfeil-resultat rett og slett aldri når frem til
/// cache-skrivingen.
/// </summary>
/// <param name="BekreftetNavn">
/// Kun <c>"snl"</c>, kun ved et bekreftet treff (#158): artikkelens egen, normalt skrevne form av
/// navnet (headword, evt. organization_name-fallback) — TIL FORSKJELL FRA søketermen som ble slått
/// opp, som kan ha vært en rå/VERSAL Brreg-streng. Brukt til å foreslå en navneform ved
/// Brreg-import, ALDRI til å overskrive den autoritative, rå Brreg-formen i selve Virksomhet.Navn.
/// </param>
public sealed record EksternOppslagResultat(
    bool Treff, string? TaksonomiKategori, string? EksternUrl, IReadOnlyList<string>? Alias, string? Organisasjonsnummer,
    string? BekreftetNavn = null)
{
    public static readonly EksternOppslagResultat IngenTreff = new(false, null, null, null, null, null);
}
