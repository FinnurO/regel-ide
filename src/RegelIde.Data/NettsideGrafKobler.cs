using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// DB-avhengig lenke-LØSER for <see cref="NettsideLenkeEntitet"/>-rader skrevet av
/// <see cref="BrukerveiledningImportTjeneste"/> (samme arkitektur-todeling som
/// <see cref="RettskildeImportTjeneste.FinnEllerOpprettReferanseStubAsync"/> løser <c>hjemlet_i</c>
/// for håndbøker: en tjeneste skriver kandidat-rader, denne klassen gjør selve DB-oppslaget i en
/// EGEN, senere sending — en lenke kan sikte på et dokument importert i en senere runde).
///
/// <para>
/// **Punkt 8 (avklaringsrunde 2026-08-13)**: opprinnelig het denne klassen også
/// <c>LagreDokumentAsync</c>/<c>LeggTilStiAsync</c> (skrev <c>NettsideDokumentEntitet</c>-rader).
/// Den jobben er nå <see cref="BrukerveiledningImportTjeneste"/>s (samme mønster som
/// <c>HandbokImportTjeneste</c>) — denne klassen er redusert til KUN lenke-løsingen, som fortsatt er
/// en distinkt, batch-aktig operasjon kjørt ETTER at alle dokumenter i korpuset er skrevet.
/// </para>
/// </summary>
public sealed class NettsideGrafKobler(RegelIdeDbContext db)
{
    /// <summary>
    /// Løser <see cref="NettsideLenkeEntitet.TilRettskildeId"/> — ETT felles oppslag nå (punkt 8s
    /// konvergens kollapset den tidligere "intern nettside-til-nettside via KanoniskUrl"-sjekken og
    /// "PDF-omtale via RettskildeEntitet.Url"-sjekken til NØYAKTIG samme operasjon, siden en nettside
    /// selv ER en <see cref="RettskildeEntitet"/> nå — begge var alltid "matcher RaaHref mot en kjent
    /// RettskildeEntitet.Url"): først URL-match mot <see cref="RettskildeEntitet.Url"/> (dekker BÅDE
    /// nettside-til-nettside OG nettside-til-håndbok), deretter — kun for <c>lovdatalenke</c> uten
    /// URL-treff — ELI-match mot <see cref="RettskildeEntitet.Eli"/>. Kjøres som EGET steg etter at
    /// alle dokumenter/rettskilder er lagret, bevisst, samme "løs det du kan, dropp stille resten"-
    /// prinsipp som <c>HandbokTekstParser.TrekkUtReferanser</c> bruker for uløste kryssreferanser.
    /// </summary>
    public async Task<int> LoosLenkerAsync(CancellationToken ct = default)
    {
        var ulostAntall = 0;

        var lenker = await db.NettsideLenker.Where(l => l.TilRettskildeId == null).ToListAsync(ct);
        var rettskilderMedUrl = await db.Rettskilder.Where(r => r.Url != null).Select(r => new { r.Id, r.Url }).ToListAsync(ct);
        var rettskilderMedEli = await db.Rettskilder.Where(r => r.Eli != null).Select(r => new { r.Id, r.Eli }).ToListAsync(ct);

        foreach (var lenke in lenker)
        {
            var urlMatch = rettskilderMedUrl.FirstOrDefault(r => ErSammeUrl(lenke.RaaHref, r.Url!));
            if (urlMatch is not null)
            {
                lenke.TilRettskildeId = urlMatch.Id;
                continue;
            }

            if (lenke.Type == "lovdatalenke" && lenke.TilEidKandidat is not null)
            {
                var eliMatch = rettskilderMedEli.FirstOrDefault(r => r.Eli == lenke.TilEidKandidat);
                if (eliMatch is not null)
                {
                    lenke.TilRettskildeId = eliMatch.Id;
                    continue;
                }
            }

            ulostAntall++;
        }

        await db.SaveChangesAsync(ct);
        return ulostAntall;
    }

    /// <summary>
    /// Sammenligner en (potensielt relativ) href mot en kjent absolutt URL — tolerant på skjema
    /// (http/https) og trailing slash, IKKE på vertsnavn/sti forøvrig (ingen gjettet normalisering
    /// utover dette).
    /// </summary>
    private static bool ErSammeUrl(string href, string kjentUrl)
    {
        var a = NormaliserSti(href);
        var b = NormaliserSti(kjentUrl);
        return a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormaliserSti(string url)
    {
        var u = url.Trim();
        if (u.StartsWith("https://www.bergen.kommune.no", StringComparison.OrdinalIgnoreCase))
            u = u["https://www.bergen.kommune.no".Length..];
        else if (u.StartsWith("http://www.bergen.kommune.no", StringComparison.OrdinalIgnoreCase))
            u = u["http://www.bergen.kommune.no".Length..];
        return u.TrimEnd('/');
    }
}
