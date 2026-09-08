using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RegelIde.Data;

/// <summary>
/// [Ny, registernavn-runden, 2026-09-08] Setter <see cref="Virksomhet.Navn"/> til Brregs egen form for
/// alle rader i <c>Seed/organisasjoner-norge.json</c>, og oppretter navneformene som visningen og
/// taggingen faktisk bruker — hentet fra en AUTORITATIV kilde per radtype.
///
/// <para>
/// <b>Hvorfor denne runden finnes.</b> Johann rapporterte tre kommunenavn med synlig feil kasus i
/// tagg-listen under forskrift 2005-06-17-657 § 1 («Gaivuona suohkan kåfjord kommune kaivuonon
/// komuuni»). #206 hadde løst de sju navnene med skilletegnet <c>" / "</c> ved å heve forbokstaven i
/// hvert ledd, men de tre gjenstående har ingen skilletegn å splitte på — Brreg lagrer navnet i opptil
/// tre navnelinjer, og en flat konkatenering limer dem sammen med mellomrom, så leddgrensen finnes
/// rett og slett ikke i strengen. Johanns beslutning var derfor å slutte å regne på navnet:
/// «navnet tas fra Brreg. Alternative skrivemåter legges til i navneformer.»
/// </para>
///
/// <para>
/// <b>Tre kilder, i denne rekkefølgen per rad</b> — den første som treffer, vinner:
/// <list type="number">
/// <item>
/// <see cref="VirksomhetNavneformOverstyringer.PerOrganisasjonsnummer"/> (45 rader) — eksplisitte,
/// godkjente navn for radene ingen ekstern kilde dekker (14 fylkeskommuner, 10 statsforvaltere, 21
/// øvrige). Sjekkes FØRST, slik at et godkjent navn aldri kan bli overkjørt av et leksikontreff.
/// </item>
/// <item>
/// <b>SSR</b> for <c>orgForm=KOMM</c> (357 rader) — Kartverkets stedsnavnregister, via
/// <see cref="EksternNavneoppslagTjeneste.SlaOppSsrStedAsync"/>. Gir ALLE språkmerkede skrivemåter for
/// kommunen med Kartverkets egen vedtaksstatus, og er den eneste kilden som har de diakritiske tegnene
/// Brreg mangler: «KARASJOGA GIELDA» i registeret, «Kárášjoga gielda» i SSR. Ingen algoritme kunne
/// produsert de tegnene fra Brreg-strengen — de finnes ikke i den.
/// </item>
/// <item>
/// <b>SNL</b> for alt annet (45 rader bekreftes) — samme mekanisme #158 alt bruker ved Brreg-import.
/// Kun ved et BEKREFTET institusjonstreff; ingen gjettet fallback.
/// </item>
/// </list>
/// Treffer ingen av de tre, står raden UTEN navneform og visningen faller tilbake på Brreg-navnet.
/// Det er en bevisst, synlig tilstand — ikke en feil å skjule med et gjettet navn.
/// </para>
///
/// <para>
/// <b>Navnet overskrives, navneformene gjør det ikke.</b> <see cref="Virksomhet.Navn"/> settes til
/// Brregs form på hver kjøring der den avviker — det er selve policyen (#158: registerets form er den
/// korrekte, autoritative registreringen, ikke en feil å normalisere), og den skal ikke kunne drifte
/// bort. Navneformer derimot OPPRETTES bare når de mangler: en navneform noen har rettet manuelt
/// beholder sin egen term og grunn, og en grunn som alt er satt overskrives aldri.
/// </para>
///
/// <para>
/// <b>Idempotent.</b> Andre kjøring gjør ingen skrivinger: navnene er alt like, navneformene finnes
/// alt, og SSR/SNL-svarene kommer fra <c>ekstern_navneoppslag_cache</c> uten et eneste nytt HTTP-kall.
/// Brreg-oppslaget hoppes over for rader som alt har <see cref="Virksomhet.SistBrregSynkronisert"/>
/// satt, med mindre <paramref name="tvingBrregOppslag"/> er satt.
/// </para>
///
/// <para>
/// <b>Kjøres som bakgrunnsjobb</b> (<c>VirksomhetRegisternavnSynkBakgrunnstjeneste</c>), ikke inline i
/// oppstartsblokken: første kjøring gjør inntil 447 Brreg-kall pluss inntil 402 SSR/SNL-kall, og de
/// ville lagt minutter på oppstarten. Samme begrunnelse som
/// <c>LovdataFullimportBakgrunnstjeneste</c> sin klassekommentar oppgir.
/// </para>
/// </summary>
public sealed class VirksomhetRegisternavnSynkTjeneste(
    RegelIdeDbContext db,
    BrregKlient brreg,
    EksternNavneoppslagTjeneste eksternOppslag,
    VirksomhetsbegrepTjeneste virksomhetsbegrep,
    VirksomhetSlettTjeneste slett,
    ILogger<VirksomhetRegisternavnSynkTjeneste>? logger = null)
{
    private const string SeedFilnavn = "Seed/organisasjoner-norge.json";

    /// <summary>Proveniens-bruker for navneformer denne tjenesten oppretter — samme mønster som de
    /// øvrige seedene, slik at det er sporbart HVEM som la dem inn.</summary>
    private const string SynkBruker = "registernavn-synk";

    private readonly ILogger _logger = logger ?? NullLogger<VirksomhetRegisternavnSynkTjeneste>.Instance;

    /// <summary>Ett rettet navn — for logging, slik at hver endring er etterprøvbar.</summary>
    public sealed record RettetNavn(Guid VirksomhetId, string Fra, string Til);

    /// <summary>Én opprettet navneform, med kilden den kom fra
    /// (<c>"overstyring"</c>/<c>"ssr"</c>/<c>"snl"</c>).</summary>
    public sealed record OpprettetNavneform(Guid VirksomhetId, string Term, string Grunn, string Kilde);

    /// <summary>Én rad som ikke fikk noen navneform, med grunnen — hele poenget med å rapportere:
    /// en rad uten navneform vises med Brregs VERSAL-form, og det skal være synlig hvorfor.</summary>
    public sealed record UtenNavneform(Guid VirksomhetId, string Navn, string Grunn);

    /// <summary>Én slettet (eller blokkert) rad fra
    /// <see cref="VirksomhetNavneformOverstyringer.SlettedeOrganisasjonsnumre"/>.</summary>
    public sealed record SlettetRad(string Organisasjonsnummer, string Navn, string Utfall);

    public sealed record Resultat(
        IReadOnlyList<RettetNavn> RettedeNavn,
        IReadOnlyList<OpprettetNavneform> OpprettedeNavneformer,
        IReadOnlyList<UtenNavneform> UtenNavneform,
        IReadOnlyList<SlettetRad> SlettedeRader,
        int BrregOppslag,
        IReadOnlyList<string> HoppetOver);

    private sealed record OrganisasjonJsonEntry(
        [property: JsonPropertyName("organisasjonsnummer")] string Organisasjonsnummer,
        [property: JsonPropertyName("navn")] string Navn,
        [property: JsonPropertyName("orgForm")] string OrgForm);

    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    /// <param name="tvingBrregOppslag">
    /// Slår opp mot Brreg også for rader som alt har <see cref="Virksomhet.SistBrregSynkronisert"/>
    /// satt. Normalt <c>false</c> — da er andre kjøring gratis. Settes <c>true</c> når registeret
    /// faktisk skal leses på nytt (f.eks. en navneendring i Brreg).
    /// </param>
    public async Task<Resultat> KjorAsync(bool tvingBrregOppslag = false, CancellationToken ct = default)
    {
        var rettedeNavn = new List<RettetNavn>();
        var opprettede = new List<OpprettetNavneform>();
        var utenNavneform = new List<UtenNavneform>();
        var hoppetOver = new List<string>();
        var brregOppslag = 0;

        var slettedeRader = await SlettFjernedeRaderAsync(ct);

        var filsti = Path.Combine(AppContext.BaseDirectory, SeedFilnavn);
        if (!File.Exists(filsti))
        {
            // Samme skip-mønster som OrganisasjonsregisterSeed ved manglende fil — ingen gjettet
            // fallback, og ingenting å synke uten kildelisten.
            hoppetOver.Add($"Fant ikke {SeedFilnavn} — ingen synk utført.");
            return new Resultat([], [], [], slettedeRader, 0, hoppetOver);
        }

        var alle = JsonSerializer.Deserialize<List<OrganisasjonJsonEntry>>(
            await File.ReadAllTextAsync(filsti, ct), JsonInnstillinger) ?? [];

        foreach (var entry in alle)
        {
            ct.ThrowIfCancellationRequested();

            var virksomhet = await db.Virksomheter
                .FirstOrDefaultAsync(v => v.Organisasjonsnummer == entry.Organisasjonsnummer, ct);
            if (virksomhet is null)
            {
                // Raden er ikke seedet i dette miljøet (OrganisasjonsregisterSeed har ikke kjørt, eller
                // noen har slettet den bevisst). Ingenting å synke — og INGEN ny rad opprettes her:
                // katalogseeding er seedens jobb, ikke synkens.
                continue;
            }

            // ---- 1) Navnet: Brregs egen form ----
            var kommunenummer = (string?)null;
            if (tvingBrregOppslag || virksomhet.SistBrregSynkronisert is null)
            {
                var enhet = await brreg.HentPaOrgnrAsync(entry.Organisasjonsnummer, ct);
                brregOppslag++;
                if (enhet is null)
                {
                    hoppetOver.Add(
                        $"{entry.Organisasjonsnummer} ({entry.Navn}): ikke funnet i Brreg — navn og "
                        + "SistBrregSynkronisert urørt.");
                }
                else
                {
                    kommunenummer = enhet.Forretningsadresse?.Kommunenummer;
                    if (!string.IsNullOrWhiteSpace(enhet.Navn) && enhet.Navn != virksomhet.Navn)
                    {
                        rettedeNavn.Add(new RettetNavn(virksomhet.Id, virksomhet.Navn, enhet.Navn));
                        virksomhet.Navn = enhet.Navn;
                    }
                    virksomhet.SistBrregSynkronisert = DateOnly.FromDateTime(DateTime.UtcNow);
                    await db.SaveChangesAsync(ct);
                }
            }

            // ---- 2) Navneformene ----
            var (navneformer, kilde) = await FinnNavneformerAsync(entry, kommunenummer, ct);
            if (navneformer.Count == 0)
            {
                utenNavneform.Add(new UtenNavneform(virksomhet.Id, virksomhet.Navn, kilde));
                continue;
            }

            foreach (var nf in navneformer)
            {
                var opprettet = await SorgForNavneformAsync(virksomhet.Id, nf.Term, nf.Grunn, ct);
                if (opprettet) opprettede.Add(new OpprettetNavneform(virksomhet.Id, nf.Term, nf.Grunn, kilde));
            }
        }

        return new Resultat(rettedeNavn, opprettede, utenNavneform, slettedeRader, brregOppslag, hoppetOver);
    }

    /// <summary>
    /// Sletter radene i <see cref="VirksomhetNavneformOverstyringer.SlettedeOrganisasjonsnumre"/> som
    /// fortsatt ligger i basen — de er fjernet fra kildefila, men en base som alt er seedet har dem.
    /// <para>
    /// Bruker <see cref="VirksomhetSlettTjeneste"/> og ikke en rå <c>DELETE</c>: den kjenner kaskaden
    /// og — viktigere — den BLOKKERER hvis en av radenes tekst-tagger har en publisert referanse
    /// (AK-3.3.4). Da rapporteres utfallet i stedet, og ingenting slettes. Hver rad logges av kalleren,
    /// slik at slettingen er etterprøvbar (issue #157 «ingen stille destruksjon»).
    /// </para>
    /// </summary>
    private async Task<List<SlettetRad>> SlettFjernedeRaderAsync(CancellationToken ct)
    {
        var resultat = new List<SlettetRad>();
        foreach (var orgnr in VirksomhetNavneformOverstyringer.SlettedeOrganisasjonsnumre)
        {
            var rad = await db.Virksomheter.FirstOrDefaultAsync(v => v.Organisasjonsnummer == orgnr, ct);
            if (rad is null) continue; // alt slettet, eller aldri seedet — idempotensen.

            var navn = rad.Navn;
            var utfall = await slett.SlettAsync(rad.Id, ct);
            resultat.Add(new SlettetRad(orgnr, navn, utfall.Utfall.ToString()
                + (utfall.Detalj is null ? "" : $": {utfall.Detalj}")));
        }
        return resultat;
    }

    /// <summary>Kildevalget — se klassekommentarens «tre kilder, i denne rekkefølgen».</summary>
    private async Task<(IReadOnlyList<VirksomhetNavneformOverstyringer.Navneform> Navneformer, string Kilde)>
        FinnNavneformerAsync(OrganisasjonJsonEntry entry, string? kommunenummer, CancellationToken ct)
    {
        if (VirksomhetNavneformOverstyringer.PerOrganisasjonsnummer.TryGetValue(
                entry.Organisasjonsnummer, out var overstyring))
        {
            return (overstyring, "overstyring");
        }

        if (entry.OrgForm == "KOMM")
        {
            if (string.IsNullOrWhiteSpace(kommunenummer))
            {
                // Uten kommunenummer fra Brreg finnes ingen deterministisk nøkkel inn i SSR, og et
                // navnesøk på den konkatenerte Brreg-strengen treffer ingenting (se
                // SlaOppSsrStedAsync). Ingen gjettet fallback.
                return ([], "ssr: mangler kommunenummer fra Brreg");
            }

            var ssr = await eksternOppslag.SlaOppSsrStedAsync(kommunenummer, "kommune", ct);
            if (!ssr.Treff || ssr.Skrivemater is null || ssr.Skrivemater.Count == 0)
            {
                return ([], $"ssr: ingen treff for kommunenummer {kommunenummer}");
            }
            return (FraSsr(ssr.Skrivemater), "ssr");
        }

        var snl = await eksternOppslag.SlaOppSnlAsync(entry.Navn, ct);
        if (snl.Treff && !string.IsNullOrWhiteSpace(snl.BekreftetNavn))
        {
            // Samme «SNL bekreftet = gjeldende, ikke gjettet»-resonnement som
            // POST /api/virksomheter/fra-brreg alt bruker (#158).
            return ([new(snl.BekreftetNavn, "gjeldende")], "snl");
        }

        return ([], "ingen autoritativ kilde (verken overstyring, SSR eller SNL)");
    }

    /// <summary>
    /// Avbilder SSRs språkmerkede skrivemåter til navneformgrunner. Grunnen er LEST fra kildens egne
    /// felt, ikke regnet ut fra strengen:
    /// <list type="bullet">
    /// <item>
    /// <c>'gjeldende'</c> — den NORSKE hovedformen med Kartverkets vedtaksstatus
    /// (<c>vedtatt</c>/<c>godkjent og prioritert</c>). Kun DEN FØRSTE som oppfyller alt: det er én
    /// gjeldende form per virksomhet, og visningen plukker nettopp den.
    /// </item>
    /// <item>
    /// <c>'parallellnavn'</c> — alt på et ANNET språk (Nordsamisk/Kvensk/Lulesamisk/Sørsamisk). Dette
    /// er likestilte offisielle navn, ikke oversettelser eller varianter, og derfor verken
    /// <c>'utgatt'</c> eller <c>'feilskriving'</c>.
    /// </item>
    /// <item>
    /// <c>'kortform'</c> — resten av de norske: <c>undernavn</c> (SSRs egen kortform, «Bergen» ved
    /// siden av «Bergen kommune») og hovednavn som bare er <c>godkjent</c> uten å være prioritert
    /// («Oslo» ved siden av «Oslo kommune»). Begge er kontekstavhengige kortformer, som er nøyaktig
    /// hva <c>'kortform'</c> betyr i vokabularet.
    /// </item>
    /// </list>
    /// Verifisert mot 5540 (Kåfjord, tre språk), 5512 (Tjeldsund), 5518 (Lavangen, med kortform),
    /// 5610 (Karasjok), 4601 (Bergen, med undernavn) og 0301 (Oslo, fem former) — 2026-09-08.
    /// </summary>
    internal static IReadOnlyList<VirksomhetNavneformOverstyringer.Navneform> FraSsr(
        IReadOnlyList<SsrSkrivemate> skrivemater)
    {
        var resultat = new List<VirksomhetNavneformOverstyringer.Navneform>();
        var harGjeldende = false;
        foreach (var sm in skrivemater)
        {
            resultat.Add(new(sm.Skrivemate, GrunnFor(sm, ref harGjeldende)));
        }
        return resultat;
    }

    /// <summary>Grunnen for ÉN skrivemåte — se <see cref="FraSsr"/> for reglene og hvorfor de er som de er.</summary>
    internal static string GrunnFor(SsrSkrivemate sm, ref bool harGjeldende)
    {
        var erNorsk = string.Equals(sm.Sprak, "Norsk", StringComparison.OrdinalIgnoreCase);
        if (!erNorsk) return "parallellnavn";

        var erHovednavn = string.Equals(sm.Navnestatus, "hovednavn", StringComparison.OrdinalIgnoreCase);
        var erVedtatt = sm.Skrivematestatus is "vedtatt" or "godkjent og prioritert";
        if (!harGjeldende && erHovednavn && erVedtatt)
        {
            harGjeldende = true;
            return "gjeldende";
        }
        return "kortform";
    }

    /// <summary>
    /// Oppretter navneformen bare hvis den mangler. Returnerer <c>true</c> når en rad faktisk ble
    /// opprettet (grunnlaget for idempotens-rapporteringen).
    /// <para>
    /// Samme «sjekk først»-mønster som <c>SamiskSprakforvaltningSeed.SorgForNavneformAsync</c> og
    /// <c>NavnekandidatOppdagelseTjeneste</c> bruker — nødvendig fordi
    /// <see cref="VirksomhetsbegrepTjeneste.OpprettVirksomhetsbegrepAsync"/> IKKE deduplikerer og
    /// ingen unik indeks dekker <c>(virksomhet_referanse_id, term)</c> for
    /// <c>begrepskategori='virksomhet'</c>. Uten sjekken ville hver kjøring lagt på en dublett.
    /// </para>
    /// <para>
    /// En eksisterende rad med grunn NULL får grunnen fylt inn (det er ny informasjon, ikke en
    /// overskriving); en rad som alt HAR en grunn røres ikke — den kan være manuelt satt.
    /// </para>
    /// </summary>
    private async Task<bool> SorgForNavneformAsync(Guid virksomhetId, string term, string grunn, CancellationToken ct)
    {
        var eksisterende = await db.Begreper.FirstOrDefaultAsync(
            b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == virksomhetId
                 && b.Term == term && b.Entitetsstatus == "gjeldende", ct);
        if (eksisterende is not null)
        {
            if (eksisterende.Navneformgrunn is null)
            {
                eksisterende.Navneformgrunn = grunn;
                eksisterende.SistEndretAv = SynkBruker;
                eksisterende.SistEndretTidspunkt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return false;
        }

        await virksomhetsbegrep.OpprettVirksomhetsbegrepAsync(
            virksomhetId, term, SynkBruker, skosUrl: null, navneformgrunn: grunn, ct);
        return true;
    }
}
