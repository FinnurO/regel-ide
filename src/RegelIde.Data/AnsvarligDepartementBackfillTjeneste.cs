using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Engangs-/gjentakbar tilbakefylling av <see cref="RettskildeEntitet.AnsvarligDepartement"/> for
/// rettskilder som ble importert FØR feltet fantes (kolonnen ble lagt til 2026-08-30, se feltets egen
/// klassekommentar i Entiteter.cs — bekreftet mot kjørende app: NULL for alle ~5900 allerede
/// importerte rettskilder). Verdien har likevel hele tiden ligget lagret i
/// <see cref="RettskildeEntitet.AknXml"/> som <c>&lt;regelIde:ansvarligDepartement&gt;</c>
/// (<see cref="RegelIdeNs"/>, skrevet av <c>AknXmlSkriver.SkrivMeta</c> i RegelIde.Kildekonvertering)
/// — kun den nye, spørrbare kolonnen manglet. En engangs, målrettet parsing av den allerede lagrede
/// XML-en er derfor både mulig og MYE billigere enn en full reimport mot Lovdata.
///
/// <para>
/// Idempotent — samme "kjør trygt flere ganger"-filosofi som <see cref="OrganisasjonsregisterSeed"/>/
/// <see cref="DepartementSeed"/>: henter KUN rader der <see cref="RettskildeEntitet.AnsvarligDepartement"/>
/// er NULL og <see cref="RettskildeEntitet.AknXml"/> finnes, og rører ALDRI en rad som allerede har
/// verdien satt (enten fra en tidligere kjøring av denne tjenesten, eller fra en ekte import via
/// <c>RettskildeImportTjeneste</c>). §3.3-prinsippet "ingen gjettet fallback" gjelder fullt ut: en rad
/// hvor AknXml mangler elementet helt (gammel XML skrevet FØR AknXmlSkriver fikk denne linjen) eller
/// hvor selve AknXml-feltet er NULL (f.eks. en referanse-stubb) forblir NULL uendret — departementet
/// utledes ALDRI fra tittel/kildetype e.l.
/// </para>
///
/// <para>
/// Kjøres to steder: som et engangs-oppstartssteg i Program.cs (tilbakefyller de ~5900 eksisterende
/// radene uten at Johann må trigge en full reimport manuelt), og etter hver fullimport i
/// LovdataFullimportBakgrunnstjeneste.cs (holder feltet oppdatert for alt fremtidige periodiske
/// reimporter rører — selv om selve importen normalt setter feltet direkte, dekker dette ev. rader
/// som av andre grunner endte opp NULL).
/// </para>
/// </summary>
public static class AnsvarligDepartementBackfillTjeneste
{
    /// <summary>Navnerommet AknXmlSkriver bruker for alle regel-ide-egne elementer/attributter (xmlns:regelIde).</summary>
    private static readonly XNamespace RegelIdeNs = "https://regel-ide.no/ns/akn-utvidelse/1.0";

    /// <summary>Returnerer antall rader som faktisk ble tilbakefylt (for oppstartslogging, se Program.cs).</summary>
    public static async Task<int> KjorAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var kandidater = await db.Rettskilder
            .Where(r => r.AnsvarligDepartement == null && r.AknXml != null)
            .ToListAsync(ct);

        var antallOppdatert = 0;
        foreach (var rettskilde in kandidater)
        {
            var departement = LesAnsvarligDepartementFraAknXml(rettskilde.AknXml!);
            if (string.IsNullOrEmpty(departement)) continue; // elementet manglet eller var tomt — ingen gjettet fallback, se klassekommentaren.

            rettskilde.AnsvarligDepartement = departement;
            antallOppdatert++;
        }

        if (antallOppdatert > 0) await db.SaveChangesAsync(ct);
        return antallOppdatert;
    }

    /// <summary>
    /// Parser <c>&lt;regelIde:ansvarligDepartement&gt;</c> ut av en allerede lagret AKN-XML-blob.
    /// Returnerer null (ikke en unntaksvelting av hele backfillen) både når elementet mangler og når
    /// selve XML-en er korrupt/ufullstendig — begge tilfeller skal behandles som "ingen data", akkurat
    /// som når AknXml-feltet er NULL.
    /// </summary>
    private static string? LesAnsvarligDepartementFraAknXml(string aknXml)
    {
        try
        {
            var dokument = XDocument.Parse(aknXml);
            return dokument.Descendants(RegelIdeNs + "ansvarligDepartement").FirstOrDefault()?.Value;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
