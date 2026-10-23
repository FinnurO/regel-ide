using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>Telling for én disposisjonstype i <see cref="LovtidendImportRapport"/>. En kilde med flere typer telles under hver av dem.</summary>
public sealed record LovtidendTypetelling(int Importert, int AlleredeIKorpus, int Feilet);

/// <summary>Ett dokument importen IKKE fikk inn (parser- eller lagringsfeil), med feilmeldingen.</summary>
public sealed record LovtidendFeil(string Datokode, string? Tittel, string Feilmelding);

/// <summary>Rapporten fra <see cref="LovtidendImportTjeneste.KjorAsync"/>. <see cref="HoppetOverIkkeOrganisering"/> er dokumentene uten
/// organiseringstype i tittelen (ikrafttredelser, reguleringer, endringsvedtak, uklassifiserte); <see cref="HoppetOverIkrafttredelse"/>
/// er den delen av dem som er rene ikrafttredelser. <see cref="ForfremmetStub"/> er dokumenter som bare fantes som referanse-stub.</summary>
public sealed record LovtidendImportRapport(
    bool Skrevet, int Dokumenter, int Organisering, int Importert, int ForfremmetStub, int AlleredeIKorpus,
    int HoppetOverIkkeOrganisering, int HoppetOverIkrafttredelse, int IkrafttredelseMedOrganisering,
    IReadOnlyDictionary<string, LovtidendTypetelling> PerType, IReadOnlyList<LovtidendFeil> Feil, IReadOnlyList<Guid> NyeRettskildeIder);

/// <summary>
/// [Ny, issue #395, 2026-10-23] Importerer KUNNGJORTE organiseringsvedtak fra Norsk Lovtidend avd. I som ikke finnes i korpuset.
/// <para>
/// <b>Samme vei som gjeldende-importen, ikke en parallell rørledning</b> (Johanns presisering 2026-10-23): dokumentene leses av
/// <see cref="LovdataBulkHenter.HentLovtidendDokumenterAsync"/> (samme arkivleser), konverteres av <see cref="LovdataKonverterer"/>
/// (samme parser, samme noder, eId og AKN, samme hodemetadata: <c>basedOn</c> → <see cref="RettskildeHjemmelEntitet"/>,
/// <c>changesToDocuments</c> → <see cref="RettskildeEndringEntitet"/>, departement, datoer og <c>miscInformation</c>) og lagres av
/// <see cref="RettskildeImportTjeneste"/>. Kildeformen blir <c>kunngjort</c> av seg selv, fordi parseren leser den av <c>dokid</c>-prefikset
/// <c>LTI/</c>. Det eneste nye her er utvalget.
/// </para>
/// <para>
/// <b>Utvalget [LÅST, #395 presisering 2026-10-23].</b> (1) Tittelen må gi minst én organiseringstype
/// (<see cref="Disposisjonstyper.Organiseringstyper"/>); rene ikrafttredelser holdes utenfor inntil videre. (2) Dokumentet må mangle i
/// korpuset, med nøkkel dokumenttype + dato + nummer — det er nøyaktig det ELI-en er avledet av (<see cref="LovdataIdentifikatorer"/>).
/// Finnes ELI-en som primærkilde (gjeldende eller eldre versjon), importeres det ikke: den konsoliderte teksten går foran, og en
/// kunngjort tekst skal aldri bli en «ny versjon» av en konsolidert. Finnes den bare som referanse-stub, forfremmes stubben (samme
/// regel som gjeldende-importen).
/// </para>
/// <para>
/// Etter importen klassifiseres hvert nye dokument med <see cref="DisposisjonstypeTjeneste"/> (tittel og innhold), slik at
/// livsløpet og sveipefilteret ser det med en gang.
/// </para>
/// </summary>
public sealed class LovtidendImportTjeneste(
    RettskildeImportTjeneste importer, DisposisjonstypeTjeneste klassifiserer, RegelIdeDbContext db,
    ILogger<LovtidendImportTjeneste>? logger = null)
{
    public const string SystemBruker = "system-lovtidend-import";
    private readonly ILogger<LovtidendImportTjeneste> _logger = logger ?? NullLogger<LovtidendImportTjeneste>.Instance;

    public async Task<LovtidendImportRapport> KjorAsync(
        IAsyncEnumerable<(string Datokode, string Type, string Html)> dokumenter, bool skriv, CancellationToken ct = default)
    {
        // Korpusets ELI-er som primærkilde, uansett versjon — nøkkelen for «finnes i korpuset». Referanse-stubber teller ikke.
        var primaerEli = (await db.Rettskilder.Where(r => r.Importrolle == "primaer" && r.Eli != null).Select(r => r.Eli!).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var stubEli = (await db.Rettskilder.Where(r => r.Importrolle == "referanse" && r.Entitetsstatus == "gjeldende" && r.Eli != null)
            .Select(r => r.Eli!).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var perType = Disposisjonstyper.Organiseringstyper.ToDictionary(t => t, _ => (Importert: 0, Alt: 0, Feilet: 0));
        var feil = new List<LovtidendFeil>();
        var nye = new List<Guid>();
        int antall = 0, organisering = 0, importert = 0, forfremmet = 0, alt = 0, ikkeOrg = 0, rentIkraft = 0, ikraftMedOrg = 0;

        await foreach (var (datokode, _, html) in dokumenter.WithCancellation(ct))
        {
            antall++;
            var tittel = LovdataBulkHenter.LesTittelBesteForsok(html);
            var typer = tittel is null ? [] : Disposisjonstyper.KlassifiserTittel(tittel).Select(t => t.Disposisjonstype).ToHashSet();
            var orgTyper = typer.Where(Disposisjonstyper.Organiseringstyper.Contains).ToList();
            if (orgTyper.Count == 0)
            {
                ikkeOrg++;
                if (typer.Count > 0 && typer.All(t => t == Disposisjonstyper.Ikrafttredelse)) rentIkraft++;
                continue;
            }
            organisering++;
            if (typer.Contains(Disposisjonstyper.Ikrafttredelse)) ikraftMedOrg++;

            var eli = LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out _);
            if (primaerEli.Contains(eli))
            {
                alt++;
                foreach (var t in orgTyper) perType[t] = perType[t] with { Alt = perType[t].Alt + 1 };
                continue;
            }

            if (!skriv)
            {
                importert++;
                if (stubEli.Contains(eli)) forfremmet++;
                foreach (var t in orgTyper) perType[t] = perType[t] with { Importert = perType[t].Importert + 1 };
                continue;
            }

            try
            {
                var konvertert = LovdataKonverterer.Konverter(html);
                var resultat = await importer.ImporterMedUtfallAsync(konvertert, virksomhetId: null, SystemBruker, ct);
                switch (resultat.Utfall)
                {
                    case RettskildeImportUtfall.Ny:
                        nye.Add(resultat.RettskildeId);
                        break;
                    case RettskildeImportUtfall.ForfremmetStub:
                        forfremmet++;
                        nye.Add(resultat.RettskildeId);
                        break;
                    default:
                        // Kan ikke skje etter primaerEli-sjekken over, med mindre to dokumenter i arkivet har samme ELI.
                        throw new InvalidOperationException($"Uventet utfall {resultat.Utfall} for {datokode} — ELI-en fantes alt som primærkilde.");
                }
                importert++;
                primaerEli.Add(eli);
                foreach (var t in orgTyper) perType[t] = perType[t] with { Importert = perType[t].Importert + 1 };
                await klassifiserer.KlassifiserAsync(skriv: true, rettskildeId: resultat.RettskildeId, bruker: SystemBruker, ct: ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Samme brede fangst og opprydding som LovdataFullimportTjeneste: ett uventet dokument stopper ikke resten.
                db.ChangeTracker.Clear();
                feil.Add(new LovtidendFeil(datokode, tittel, BeskrivFeilKjede(ex)));
                foreach (var t in orgTyper) perType[t] = perType[t] with { Feilet = perType[t].Feilet + 1 };
                _logger.LogWarning(ex, "Lovtidend-import: kunne ikke importere {Datokode}, hoppet over.", datokode);
            }
        }

        return new LovtidendImportRapport(
            skriv, antall, organisering, importert, forfremmet, alt, ikkeOrg, rentIkraft, ikraftMedOrg,
            perType.ToDictionary(p => p.Key, p => new LovtidendTypetelling(p.Value.Importert, p.Value.Alt, p.Value.Feilet)),
            feil, nye);
    }

    private static string BeskrivFeilKjede(Exception ex)
    {
        var deler = new List<string>();
        for (var e = (Exception?)ex; e is not null; e = e.InnerException) deler.Add(e.Message);
        return string.Join(" → ", deler);
    }
}
