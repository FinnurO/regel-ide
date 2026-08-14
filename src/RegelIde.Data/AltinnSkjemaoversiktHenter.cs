using System.Text.Json;
using System.Text.Json.Serialization;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>Én ekstern lenke funnet på en tjenesteside — <see cref="Tekst"/> er ankerteksten, <see cref="Url"/> hele href-verdien.</summary>
public sealed record AltinnTjenesteLenke(
    [property: JsonPropertyName("tekst")] string Tekst,
    [property: JsonPropertyName("url")] string Url);

/// <summary>Én <c>&lt;details&gt;</c>/<c>&lt;summary&gt;</c>-akkordionseksjon på en tjenesteside.</summary>
public sealed record AltinnTjenesteSeksjon(
    [property: JsonPropertyName("overskrift")] string Overskrift,
    [property: JsonPropertyName("innhold")] string Innhold);

/// <summary>
/// Den harvestede STRUKTUREN for én tjenesteside — dette, ikke rå HTML, er "kilden til sannhet" for en
/// skrapet nettside (se <see cref="AltinnSkjemaoversiktHenter"/>s klassekommentar punkt (b) for
/// begrunnelsen, samme som <see cref="BrukerveiledningImportTjeneste"/> bruker for Bergen-korpuset).
/// Serialisert til <see cref="EksternKildeEntitet.RaaJson"/> med nøyaktig disse fire feltnavnene.
/// </summary>
public sealed record AltinnTjenesteSide(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("tjeneste")] string? Tjeneste,
    [property: JsonPropertyName("lenker")] IReadOnlyList<AltinnTjenesteLenke> Lenker,
    [property: JsonPropertyName("seksjoner")] IReadOnlyList<AltinnTjenesteSeksjon> Seksjoner);

/// <summary>
/// Repeterbar, idempotent to-nivås HTML-krypende høstejobb mot Altinns offentlige skjemaoversikt
/// (<c>https://info.altinn.no/skjemaoversikt</c>) — ingen JSON-API finnes for denne kilden, i motsetning
/// til <see cref="OppgaveregisterHenter"/>/<see cref="AltinnRessursHenter"/>. Sideordnet disse to i
/// høstelaget for øvrig (samme <see cref="EksternKildeEntitet"/>, samme "ingen FK til domenemodellen
/// ennå"-begrunnelse).
/// <para>
/// **Krypeflyt**: (1) indekssiden lister ~200+ etater som lenker <c>/skjemaoversikt/{slug}/</c> (NØYAKTIG
/// 2 stisegmenter); (2) hver etatside lister sine tjenester som lenker <c>/skjemaoversikt/{slug}/{slug}/</c>
/// (NØYAKTIG 3 stisegmenter, filtrert til denne ETATENS EGEN slug — se <see cref="HentTjenesteStier"/>);
/// (3) hver tjenesteside gir <c>&lt;h1&gt;</c>-tittel, eksterne lenker og
/// <c>&lt;details&gt;</c>/<c>&lt;summary&gt;</c>-seksjoner, se <see cref="ParseTjenesteside"/>.
/// </para>
/// <para>
/// **(a) Reell markup-kvirk, bekreftet i den ekte indekssidefixturen**: <c>/skjemaoversikt/kategori/</c>
/// opptrer i EKSAKT samme liste-markup (<c>schema-overview__provider-item</c>) som de ekte etatene —
/// det er en UI-kategorifilterlenke, ikke en etat, og MÅ ekskluderes eksplisitt (<see cref="IkkeEtatSlugs"/>)
/// siden den ellers ville blitt krypet som en falsk "etat" med falske "tjenester"
/// (<c>/skjemaoversikt/kategori/for-privatperson/</c> osv. har også nøyaktig 3 segmenter).
/// </para>
/// <para>
/// **(b) RaaJson er den harvestede STRUKTUREN, ikke rå HTML** — for en JSON-kilde (Oppgaveregisteret,
/// Altinn ressursregister) ER kildens eget objekt "sannheten" og lagres byte-for-byte. For en skrapet
/// nettside finnes ingen tilsvarende "originalobjekt" å bevare uforandret — HTML-en er presentasjon, ikke
/// data. Samme resonnement som <see cref="BrukerveiledningImportTjeneste"/> bruker for Bergen-korpuset
/// (lagrer utvunnet tekst, ikke rå HTML/PDF, som sannhetslaget): <see cref="AltinnTjenesteSide"/>
/// (tittel/lenker/seksjoner) ER den analoge "sannheten" her.
/// </para>
/// <para>
/// **(c) Kjent v1-begrensning — ingen bakgrunnsjobb-infrastruktur denne runden** (avgrenset, ikke
/// big-bang, jf. docs/13-backlog.md). En full kryping er ~800+ tjenestesider × 0.5s høflighetsforsinkelse
/// ≈ 7+ minutter — <c>POST /api/eksterne-kilder/altinn-skjemaoversikt/hent</c> er derfor et SYNKRONT,
/// langvarig kall (kalleren trenger en lang klient-timeout). For å ikke tape alt arbeidet ved en
/// avbrutt/timet-ut kjøring, lagres INKREMENTELT — én <c>SaveChangesAsync</c> PER ETAT (ikke én batch for
/// hele kjøringen) — slik at delvis fremgang er varig og synlig via
/// <c>GET /api/eksterne-kilder?kildetype=altinn_skjemaoversikt</c> selv midt i eller etter en avbrutt
/// kjøring. Idempotent upsert (samme (Kildetype, EksternId)-mønster som resten av høstelaget) gjør et
/// helt nytt kall trygt å kjøre på nytt — allerede hostede, uendrede sider blir raske no-op-er.
/// </para>
/// <para>
/// **(d) Ingen per-side feilsvelging** — en enkelt mislykket HTTP-forespørsel (404/timeout) kaster og
/// stopper hele kjøringen, samme "ingen gjettet fallback"-filosofi som resten av kodebasen. Mitigering er
/// nettopp re-kjøring (punkt c), ikke stille hopp-over-og-fortsett.
/// </para>
/// </summary>
public sealed class AltinnSkjemaoversiktHenter(HttpClient http, RegelIdeDbContext db)
{
    /// <summary><see cref="EksternKildeEntitet.Kildetype"/>-verdien denne høsteren skriver.</summary>
    public const string Kildetype = "altinn_skjemaoversikt";

    private const string BaseUrl = "https://info.altinn.no";
    private const string IndeksSti = "/skjemaoversikt/";

    /// <summary>Johanns 0.5s høflighetsforsinkelse mellom påfølgende TJENESTESIDE-kall (ikke etat-/indekssidekall — se klassekommentaren punkt (c)).</summary>
    private static readonly TimeSpan Tjenesteforsinkelse = TimeSpan.FromMilliseconds(500);

    /// <summary>Ekte, bekreftet kvirk i indekssiden — se klassekommentaren punkt (a).</summary>
    private static readonly HashSet<string> IkkeEtatSlugs = new(StringComparer.Ordinal) { "kategori" };

    private static readonly string[] EkskluderteDomener = ["info.altinn.no", "af.altinn.no", "am.ui.altinn.no"];
    private static readonly string[] EkskluderteFilendelser =
        [".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".css", ".js", ".ico", ".json", ".xml"];

    /// <summary>Én etat funnet på indekssiden.</summary>
    public sealed record EtatLenke(string Sti, string Navn);

    /// <summary>
    /// Tolker en href til stisegmentene under <c>/skjemaoversikt/</c>, eller <c>null</c> hvis href-en
    /// ikke er en <c>/skjemaoversikt/...</c>-lenke (verken relativ eller absolutt med
    /// <see cref="BaseUrl"/>s vertsnavn). <c>["advokattilsynet"]</c> for <c>/skjemaoversikt/advokattilsynet/</c>
    /// (segmentet "skjemaoversikt" selv er ikke med i det returnerte arrayet).
    /// </summary>
    private static string[]? TolkSkjemaoversiktSegmenter(string href)
    {
        var sti = href;
        if (sti.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sti.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(sti, UriKind.Absolute, out var uri)) return null;
            sti = uri.AbsolutePath;
        }
        if (!sti.StartsWith("/skjemaoversikt/", StringComparison.Ordinal)) return null;

        var segmenter = sti.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segmenter.Length >= 2 ? segmenter[1..] : [];
    }

    /// <summary>
    /// Steg 1 — indekssiden. Nøyaktig 2 stisegmenter (<c>/skjemaoversikt/{slug}/</c>), deduplisert på
    /// slug, ekskludert <see cref="IkkeEtatSlugs"/> (se klassekommentaren punkt (a)).
    /// </summary>
    public static IReadOnlyList<EtatLenke> HentEtater(string indekssideHtml)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(indekssideHtml);

        var resultat = new List<EtatLenke>();
        var sett = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var segmenter = TolkSkjemaoversiktSegmenter(a.GetAttributeValue("href", ""));
            if (segmenter is not [var slug]) continue; // nøyaktig 2 stisegmenter totalt (skjemaoversikt + 1)
            if (IkkeEtatSlugs.Contains(slug)) continue;
            if (!sett.Add(slug)) continue; // samme etat kan opptre i flere lenker på siden (f.eks. både en "populære tjenester"-seksjon og selve A-Å-listen)

            var navn = HtmlEntity.DeEntitize(HentTekstUtenAvatarInitial(a)).Trim();
            resultat.Add(new EtatLenke($"/skjemaoversikt/{slug}/", navn));
        }
        return resultat;
    }

    /// <summary>
    /// Reell markup-kvirk, bekreftet i den ekte indekssidefixturen: en etat UTEN logo-SVG
    /// (<c>data-image="false"</c>) får en fallback-avatar med etatnavnets FØRSTE BOKSTAV som egen
    /// synlig tekst (<c>&lt;span class="_label_caglx_35"&gt;A&lt;/span&gt;</c>) INNI samme
    /// <c>&lt;a&gt;</c> som selve navnelenken — <c>a.InnerText</c> alene ville dermed gitt "AA-ordningen"
    /// i stedet for "A-ordningen". Ekskluderer derfor all tekst under enhver node hvis <c>class</c>
    /// inneholder "avatar", uansett hvilken genererte CSS-modul-hash resten av klassenavnet har.
    /// </summary>
    private static string HentTekstUtenAvatarInitial(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Element && node.GetAttributeValue("class", "").Contains("avatar", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }
        if (node.NodeType == HtmlNodeType.Text)
        {
            return node.InnerText;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var barn in node.ChildNodes) sb.Append(HentTekstUtenAvatarInitial(barn));
        return sb.ToString();
    }

    /// <summary>
    /// Steg 2 — én etats egen side. Nøyaktig 3 stisegmenter (<c>/skjemaoversikt/{etatSlug}/{tjenesteSlug}/</c>)
    /// der det MIDTERSTE segmentet må matche <paramref name="etatSti"/>s egen slug — en tjenesteside
    /// lenket fra en ANNEN etats side (kryssreferanse) skal krypes derfra, ikke herfra.
    /// </summary>
    public static IReadOnlyList<string> HentTjenesteStier(string etatsideHtml, string etatSti)
    {
        var etatSegmenter = etatSti.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (etatSegmenter is not [_, var etatSlug])
        {
            throw new ArgumentException($"'{etatSti}' er ikke en gyldig etat-sti (forventet nøyaktig /skjemaoversikt/{{slug}}/).", nameof(etatSti));
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(etatsideHtml);

        var resultat = new List<string>();
        var sett = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var segmenter = TolkSkjemaoversiktSegmenter(a.GetAttributeValue("href", ""));
            if (segmenter is not [var provider, var tjeneste] || provider != etatSlug) continue;

            var sti = $"/skjemaoversikt/{provider}/{tjeneste}/";
            if (sett.Add(sti)) resultat.Add(sti);
        }
        return resultat;
    }

    /// <summary>
    /// Steg 3 — selve tjenestesiden. <c>&lt;h1&gt;</c> → tittel; eksterne lenker (<c>href</c> som starter
    /// med <c>https://</c>, ekskludert kjente Altinn-interne domener og statiske filendelser, deduplisert
    /// på href); <c>&lt;details&gt;</c>-seksjoner med heading=<c>&lt;summary&gt;</c>-tekst og
    /// innhold=hele detaljteksten MED summary-teksten strippet fra fronten (Johanns eksakte metode).
    /// </summary>
    public static AltinnTjenesteSide ParseTjenesteside(string html, string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tittel = doc.DocumentNode.SelectSingleNode("//h1") is { } h1
            ? HtmlEntity.DeEntitize(h1.InnerText).Trim()
            : null;

        var lenker = new List<AltinnTjenesteLenke>();
        var sett = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = a.GetAttributeValue("href", "");
            if (!href.StartsWith("https://", StringComparison.Ordinal)) continue;
            if (EkskluderteDomener.Any(d => href.Contains(d, StringComparison.OrdinalIgnoreCase))) continue;
            if (EkskluderteFilendelser.Any(ext => href.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;
            if (!sett.Add(href)) continue;

            var tekst = HtmlEntity.DeEntitize(a.InnerText).Trim();
            lenker.Add(new AltinnTjenesteLenke(tekst, href));
        }

        var seksjoner = new List<AltinnTjenesteSeksjon>();
        foreach (var details in doc.DocumentNode.SelectNodes("//details") ?? Enumerable.Empty<HtmlNode>())
        {
            var summary = details.SelectSingleNode(".//summary");
            if (summary is null) continue; // ingen overskrift å hente ut — ikke observert i ekte data, men ingen gjettet fallback.

            var raaOverskrift = summary.InnerText;
            var raaHeltekst = details.InnerText;
            var raaInnhold = raaHeltekst.StartsWith(raaOverskrift, StringComparison.Ordinal)
                ? raaHeltekst[raaOverskrift.Length..]
                : raaHeltekst; // uventet struktur (summary ikke først) — behold hele teksten fremfor å gjette

            seksjoner.Add(new AltinnTjenesteSeksjon(
                HtmlEntity.DeEntitize(raaOverskrift).Trim(),
                HtmlEntity.DeEntitize(raaInnhold).Trim()));
        }

        return new AltinnTjenesteSide(url, tittel, lenker, seksjoner);
    }

    /// <summary>Full kryping — se klassekommentaren for flyt/inkrementell lagring/feilhåndtering.</summary>
    public async Task<AltinnSkjemaoversiktHostingResultat> HentAltAsync(CancellationToken ct = default)
    {
        var indeksHtml = await http.GetStringAsync(BaseUrl + IndeksSti, ct);
        var etater = HentEtater(indeksHtml);

        var eksisterende = await db.EksterneKilder
            .Where(k => k.Kildetype == Kildetype)
            .ToDictionaryAsync(k => k.EksternId, StringComparer.Ordinal, ct);

        var nye = 0;
        var oppdaterte = 0;
        var uendret = 0;
        var forsteTjenesteHentetAlt = false;

        foreach (var etat in etater)
        {
            ct.ThrowIfCancellationRequested();
            var etatHtml = await http.GetStringAsync(BaseUrl + etat.Sti, ct);
            var tjenesteStier = HentTjenesteStier(etatHtml, etat.Sti);

            foreach (var tjenesteSti in tjenesteStier)
            {
                ct.ThrowIfCancellationRequested();
                if (forsteTjenesteHentetAlt) await Task.Delay(Tjenesteforsinkelse, ct);
                forsteTjenesteHentetAlt = true;

                var fullUrl = BaseUrl + tjenesteSti;
                var tjenesteHtml = await http.GetStringAsync(fullUrl, ct);
                var side = ParseTjenesteside(tjenesteHtml, fullUrl);
                var raaTekst = JsonSerializer.Serialize(side);
                var hash = LovdataIdentifikatorer.BeregnTekstHash(raaTekst);

                if (eksisterende.TryGetValue(tjenesteSti, out var rad))
                {
                    if (rad.InnholdsHash == hash)
                    {
                        uendret++;
                        continue;
                    }

                    rad.RaaJson = raaTekst;
                    rad.InnholdsHash = hash;
                    rad.HentetTidspunkt = DateTimeOffset.UtcNow;
                    oppdaterte++;
                }
                else
                {
                    var nyRad = new EksternKildeEntitet
                    {
                        Id = Guid.NewGuid(),
                        Kildetype = Kildetype,
                        EksternId = tjenesteSti,
                        RaaJson = raaTekst,
                        InnholdsHash = hash,
                        HentetTidspunkt = DateTimeOffset.UtcNow,
                    };
                    db.EksterneKilder.Add(nyRad);
                    eksisterende[tjenesteSti] = nyRad;
                    nye++;
                }
            }

            // Inkrementell lagring PER ETAT — se klassekommentaren punkt (c).
            await db.SaveChangesAsync(ct);
        }

        return new AltinnSkjemaoversiktHostingResultat(nye, oppdaterte, uendret);
    }
}

/// <summary>Sammendrag av én <see cref="AltinnSkjemaoversiktHenter.HentAltAsync"/>-kjøring.</summary>
public sealed record AltinnSkjemaoversiktHostingResultat(int Nye, int Oppdaterte, int Uendret);
