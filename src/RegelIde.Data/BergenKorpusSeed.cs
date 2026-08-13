using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Seeder Bergen kommunes fulle dokumentgraf-korpus (docs/15-handbok-dokumentgraf-notat.md, byggesteg-1-
/// utvidelsen) — de to Lovdata-lovene korpuset refererer, begge håndbok-fixturene og alle 23 nettside-
/// fixturene, koblet sammen via <see cref="NettsideGrafKobler.LoosLenkerAsync"/>. Kjøres idempotent ved
/// oppstart (RegelIde.Api/Program.cs), samme mønster som <see cref="TestkommuneInnholdSeed"/>/
/// <see cref="AgderFylkeskommuneSeed"/> — global guard på om "Bergen kommune"-virksomheten allerede finnes.
///
/// <para>
/// **Kildetype-valget for forskriften, dokumentert (§ "ingen gjettet fallback")**: Bergens forskrift om
/// salgs-, skjenke- og åpningstider ER en reell, kunngjort lokal forskrift (vedtatt av Bystyret,
/// data/kilder/raw-handbok/README.md) — men fixturen vi har er PDF-tekstlag hentet via WebFetch, IKKE
/// Lovdatas "XML-kompatible HTML"-eksportformat <c>LovdataKonverterer</c> krever, og README-en nevner
/// INGEN Lovdata-URL/ELI for den (kun den direkte PDF-URL-en <c>.../api/rest/filer/V51903879</c>). Å
/// konstruere en Lovdata-ELI for den ville vært nøyaktig den gjettingen prinsippet forbyr. Den importeres
/// derfor via <see cref="HandbokImportTjeneste"/> (samme tekst-import-pipeline som retningslinjene), men
/// med <c>Kildetype="Forskrift"</c>/<c>Doctype="act"</c> — feltene som beskriver HVA dokumentet ER,
/// uavhengig av HVILKEN pipeline som importerte det.
/// </para>
///
/// <para>
/// **Alkoholloven/alkoholforskriften importeres DELT (VirksomhetId=null), ikke Bergen-scopet** — et
/// bevisst avvik fra en literal lesning av oppgavens "knyttet til Bergens virksomhetId" for disse to,
/// fordi <see cref="RettskildeEntitet.VirksomhetId"/>s egen klassekommentar er eksplisitt låst på at
/// nasjonale Lov/Forskrift ALDRI skal dupliseres per virksomhet (opptil 1000 virksomheter ville ellers
/// fått hver sin kopi av samme lov, kostbart og feilutsatt ved lovendring) — nøyaktig samme mønster
/// <c>NettsideDokumentgrafTests.Bundlingssiden_kobler_helt_frem…</c> allerede bruker for disse to lovene.
/// Bergens EGNE lokale kilder (håndbok-fixturene, nettsidene) knyttes derimot korrekt til
/// <see cref="Virksomhet.Id"/>, siden de FAKTISK er virksomhetsspesifikke.
/// </para>
/// </summary>
public static class BergenKorpusSeed
{
    private const string SeedBruker = "Kari Jurist";

    private static readonly string[] AlleUnderliggendeNettsideFiler =
    [
        "bevillingsgebyr-salgsog-skjenkebevillinger-20252026-frist-er-17februar-2026.txt",
        "etablererproven-og-kunnskapsproven.txt",
        "godkjenning-av-ny-styrer-stedfortreder-og-daglig-leder-i-bevillinger.txt",
        "kontrollvirksomhet-av-skjenking-og-salg-av-alkohol.txt",
        "krav-om-fettutskiller.txt",
        "kurs-i-ansvarlig-alkoholhandtering-2026.txt",
        "lukket-selskap-ambulerende-skjenkebevilling.txt",
        "melde-inn-og-ut-av-tobakkssalgsregisteret.txt",
        "retningslinjer-for-tildeling-av-salgsog-skjenkebevillinger-og-forskrift-om-salgsskjenkeog-apningstider.txt",
        "salgsbevilling-for-alkohol.txt",
        "skjenketid-ved-overgang-til-sommertid-og-vintertid.txt",
        "skjenketider-i-forbindelse-med-fotball-vm-2026-perioden-11-juni-til-19-juli-2026.txt",
        "soknad-om-serveringsbevilling.txt",
        "soknad-om-skjenkebevilling-for-alkohol-og-endringer-i-eksisterende-bevilling-f-eks-soknad-om-uteservering.txt",
        "soknad-om-skjenkebevilling-for-alkoholholdig-drikk-gruppe-3.txt",
        "soknad-om-skjenkebevilling-pa-uteareal.txt",
        "soknad-om-utvidet-skjenkeareal-for-en-enkelt-anledning.txt",
        "soknad-om-utvidet-skjenkeareal-pa-eksisterende-skjenkebevilling.txt",
        "tilsyn-av-internkontroll-ved-virksomheter-med-salgsog-skjenkebevilling.txt",
        "utvidet-skjenkeog-apningstid-for-en-enkelt-anledning.txt",
        "apent-arrangement-skjenkebevilling-for-n-enkelt-anledning.txt",
    ];

    private static readonly string[] IndeksNettsideFiler = ["bevilling-og-tillatelser.txt", "kontor-for-skjenkesaker-innbyggerhjelp.txt"];

    /// <param name="dataKilderRotmappe">Absolutt sti til <c>data/kilder</c> (mor-mappen til <c>raw-lovdata</c>/
    /// <c>raw-handbok</c>/<c>raw-nettside</c>) — samme konfigurerbare-sti-mønster som <c>RegelIde:Kildemappe</c>
    /// i Program.cs, siden containeren ikke har repoets mappestruktur rundt seg.</param>
    public static async Task SeedAsync(RegelIdeDbContext db, string dataKilderRotmappe, CancellationToken ct = default)
    {
        if (await db.Virksomheter.AnyAsync(v => v.Navn == "Bergen kommune", ct)) return;
        if (!Directory.Exists(dataKilderRotmappe)) return; // container uten repoets kildefiler — samme skip som Program.cs' egen raw-lovdata-import.

        var bergen = new Virksomhet
        {
            Id = Guid.NewGuid(), Navn = "Bergen kommune", Organisasjonsnummer = null,
            Kommunenummer = "4601", Forvaltningsniva = "kommune", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Virksomheter.Add(bergen);
        await db.SaveChangesAsync(ct);

        await SeedLovdataAsync(db, dataKilderRotmappe, ct);
        await SeedHandbokAsync(db, dataKilderRotmappe, bergen.Id, ct);
        await SeedNettsiderAsync(db, dataKilderRotmappe, bergen.Id, ct);
    }

    /// <summary>Alkoholloven + alkoholforskriften — delt/nasjonalt, se klassekommentaren.</summary>
    private static async Task SeedLovdataAsync(RegelIdeDbContext db, string dataKilderRotmappe, CancellationToken ct)
    {
        var mappe = Path.Combine(dataKilderRotmappe, "raw-lovdata");
        var importer = new RettskildeImportTjeneste(db);

        foreach (var filnavn in new[] { "alkoholloven-LOV-1989-06-02-27.html", "alkoholforskriften-FOR-2005-06-08-538.html" })
        {
            var full = Path.Combine(mappe, filnavn);
            if (!File.Exists(full)) continue; // ingen gjettet fallback — hopp over stille, dokumentert i sluttrapporten hvis det skjer.

            var resultat = LovdataKonverterer.Konverter(await File.ReadAllTextAsync(full, ct));
            await importer.ImporterAsync(resultat, virksomhetId: null, opprettetAv: SeedBruker, ct);
        }
    }

    /// <summary>Begge håndbok-fixturene, metadata fra data/kilder/raw-handbok/README.md (SD-24-113/-114).</summary>
    private static async Task SeedHandbokAsync(RegelIdeDbContext db, string dataKilderRotmappe, Guid bergenId, CancellationToken ct)
    {
        var mappe = Path.Combine(dataKilderRotmappe, "raw-handbok");
        var tjeneste = new HandbokImportTjeneste(db);

        var retningslinjerFil = Path.Combine(mappe, "bergen-retningslinjer-SD-24-113.txt");
        if (File.Exists(retningslinjerFil))
        {
            var parset = HandbokTekstParser.Parse(await File.ReadAllTextAsync(retningslinjerFil, ct));
            await tjeneste.ImporterAsync(
                parset,
                "Retningslinjer for tildeling av salgs- og skjenkebevillinger i Bergen kommune for perioden 2024-2028",
                bergenId, kildetype: "Virksomhetsdokument", doctype: "doc", opprettetAv: SeedBruker,
                url: "https://www.bergen.kommune.no/api/rest/filer/V51903878", interntDokNr: "SD-24-113", revisjonsnr: "01",
                vedtattAv: "Bystyret", vedtaksdato: new DateOnly(2024, 6, 19), gyldigTil: new DateOnly(2028, 7, 1),
                normativVirkning: "bindende_forvaltning", ct: ct);
        }

        var forskriftFil = Path.Combine(mappe, "bergen-forskrift-salgs-skjenke-apningstider.txt");
        if (File.Exists(forskriftFil))
        {
            var parset = HandbokTekstParser.Parse(await File.ReadAllTextAsync(forskriftFil, ct));
            await tjeneste.ImporterAsync(
                parset,
                "Forskrift om salgs-, skjenke- og åpningstider i Bergen kommune for perioden 2024 – 2028",
                bergenId, kildetype: "Forskrift", doctype: "act", opprettetAv: SeedBruker,
                url: "https://www.bergen.kommune.no/api/rest/filer/V51903879", interntDokNr: "SD-24-114", revisjonsnr: "01",
                vedtattAv: "Bystyret", vedtaksdato: new DateOnly(2024, 6, 19), gyldigTil: new DateOnly(2028, 7, 1),
                // En kommunal forskrift binder borgerne direkte (i motsetning til retningslinjen, som
                // primært styrer forvaltningens eget skjønn) — en dokumentert klassifisering, ikke en
                // gjettet faktapåstand om selve dokumentet.
                normativVirkning: "bindende_borger", ct: ct);
        }
    }

    /// <summary>
    /// Alle 23 nettside-fixturene importeres som ekte <see cref="NettsideDokumentEntitet"/>-rader —
    /// INKLUDERT de to indekssidene selv (et lite, bevisst avvik fra <c>NettsideDokumentgrafTests
    /// .ByggKorpusAsync</c>, som kun PARSER indekssidene for lenkelisten uten å lagre dem: her lagres
    /// de OGSÅ, siden de er ekte, siterbare sider med egen KanoniskUrl/Tittel/RaaTekst, og
    /// oppgavens ordlyd ber om at "alle 23" importeres via <see cref="NettsideGrafKobler.LagreDokumentAsync"/>).
    /// Stier utledes fra de samme to indekssidenes egne lenkelister EKSAKT som <c>ByggKorpusAsync</c> gjør
    /// (samme absolutt-URL-normalisering for rot-relative href-er) — deretter ett
    /// <see cref="NettsideGrafKobler.LoosLenkerAsync"/>-kall til slutt.
    /// </summary>
    private static async Task SeedNettsiderAsync(RegelIdeDbContext db, string dataKilderRotmappe, Guid bergenId, CancellationToken ct)
    {
        var mappe = Path.Combine(dataKilderRotmappe, "raw-nettside");
        if (!Directory.Exists(mappe)) return;

        var kobler = new NettsideGrafKobler(db);
        var urlTilId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var indeksResultater = new List<(string? Sti, string? StiType, NettsideParseResultat Resultat)>();

        foreach (var filnavn in AlleUnderliggendeNettsideFiler.Concat(IndeksNettsideFiler))
        {
            var full = Path.Combine(mappe, filnavn);
            if (!File.Exists(full)) continue;

            var fixtur = LesFixtureFil(await File.ReadAllTextAsync(full, ct));
            var resultat = NettsideTekstParser.Parse(fixtur.KanoniskUrl, fixtur.Tittel, fixtur.RaaTekst);
            var id = await kobler.LagreDokumentAsync(resultat, bergenId, ct);
            urlTilId[fixtur.KanoniskUrl] = id;

            if (IndeksNettsideFiler.Contains(filnavn)) indeksResultater.Add((fixtur.Sti, fixtur.StiType, resultat));
        }

        foreach (var (sti, stiType, indeksResultat) in indeksResultater)
        {
            if (sti is null || stiType is null) continue;

            foreach (var lenke in indeksResultat.Lenker.Where(l => l.Type == NettsideLenketype.LenkerTil))
            {
                var mal = TilAbsoluttUrl(lenke.RaaHref);
                if (urlTilId.TryGetValue(mal, out var dokumentId))
                {
                    await kobler.LeggTilStiAsync(dokumentId, sti, stiType, ct);
                }
            }
        }

        await kobler.LoosLenkerAsync(ct);
    }

    private static string TilAbsoluttUrl(string href) =>
        href.StartsWith('/') ? $"https://www.bergen.kommune.no{href}" : href;

    /// <summary>
    /// Leser SAMME header-format som <c>NettsideFixtureLeser</c> i de to test-prosjektene (bevisst en
    /// TREDJE, produksjonskode-egen kopi — samme "ikke delt via en shared lib for ~20 linjer"-begrunnelse
    /// den klassens kommentar allerede gir, nå gjort til produksjonskode siden dette IKKE er et test-only
    /// fixture-format, men selve konvensjonen filene i data/kilder/raw-nettside/ faktisk er skrevet i).
    /// </summary>
    private static (string KanoniskUrl, string? Tittel, string? StiType, string? Sti, string RaaTekst) LesFixtureFil(string innhold)
    {
        var linjer = innhold.Replace("\r\n", "\n").Split('\n');
        string? kanoniskUrl = null, tittel = null, stiType = null, sti = null;
        var kroppStart = linjer.Length;

        for (var i = 0; i < linjer.Length; i++)
        {
            var linje = linjer[i];
            if (linje.Length == 0) { kroppStart = i + 1; break; }
            if (linje.StartsWith("KanoniskUrl:")) kanoniskUrl = linje["KanoniskUrl:".Length..].Trim();
            else if (linje.StartsWith("Tittel:")) tittel = linje["Tittel:".Length..].Trim();
            else if (linje.StartsWith("StiType:")) stiType = linje["StiType:".Length..].Trim();
            else if (linje.StartsWith("Sti:")) sti = linje["Sti:".Length..].Trim();
        }

        if (kanoniskUrl is null)
        {
            throw new FormatException("Fixture mangler 'KanoniskUrl:'-header. Ingen gjettet fallback.");
        }

        var raaTekst = string.Join('\n', linjer[kroppStart..]).Trim();
        return (kanoniskUrl, tittel, stiType, sti, raaTekst);
    }
}
