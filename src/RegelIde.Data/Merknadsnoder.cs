using System.Text.RegularExpressions;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #419, Johanns avgjørelser 2026-10-29 og 2026-10-30] <b>Merknader og veiledning som Lovdata har tatt inn i forskriftsteksten.</b>
/// Noen forskrifter har departementets merknader («Merknader til de enkelte bestemmelsene», «Veiledning til forskriften», «Kommentarer»)
/// som eget kapittel etter selve forskriften. Uten merking ble de lest som normtekst: i piloten på barnehageloven (#375) ble
/// Finansdepartementets anslag for kommunal deflator til vedtak og forskrift.
/// <para>
/// <b>Avgjørelsene (#419):</b> (1) nodene merkes ved import; de kan aldri være hjemmel for en kant, men kan siteres som tolkningsargument i
/// en begrunnelse. (2) Eksisterende forslag med slik hjemmel avvises maskinelt med begrunnelsen <see cref="AvvisningBegrunnelse"/>; godkjente
/// berøres ikke. (3) Regelen er målt over hele korpuset før bygging, og Johann valgte 2026-10-30 alternativ A: regel 1 pluss en eksplisitt
/// unntaksliste.
/// </para>
/// <para>
/// <b>[LÅST, #419] Regelen (regel 1):</b> et kapittel ØVERST i strukturen (ingen foreldernode) med eId som starter med
/// <c>kap-merknad</c>, <c>kap-veiledning</c> eller <c>kap-kommentar</c>, unntatt radene i <see cref="Unntak"/>. Hele undertreet merkes
/// (kapitlet, underinndelingene, leddene, punktene). EId-en bærer tittelen fordi et unummerert kapittel får eId av overskriften
/// (<c>LovdataHtmlParser.AvledNummerOgTittelUtenDataName</c>): «Merknader til de enkelte bestemmelsene» → <c>kap-merknader-til-de-enkelte-bestemmelsene</c>.
/// Store/små bokstaver skilles (slik regelen ble målt).
/// </para>
/// <para>
/// <b>Målt i den delte basen 2026-10-29 (bare SELECT, kommentaren på #419):</b> 100 forskrifter og instrukser, 6 991 noder; tildelings- og
/// finansieringsforskriften har til sammen 210 (juristens telling). Ett falskt treff i normtekst: FOR-2008-01-09-50, der Lovdata har kalt
/// vedlegget med standardreglene «Merknader» og § 11 gjør «standardbestemmelsene fastsatt i vedlegget til denne forskrift» gjeldende —
/// derav <see cref="Unntak"/>. Om lag 700 noder i 11 kapitler fanges ikke («Veiledende materiale» i BSL E, «gule sider» i BSL D 2-4,
/// nummererte kommentarkapitler i RPR og instrukser); de behandles fortsatt som normtekst (Johann 2026-10-30). Ingen kanter har hjemmel der.
/// </para>
/// <para>
/// <b>Forkastede regler (målt, #419):</b> regel 2, samme eId-prefiks også på underinndelinger lenger ned (<c>…/rom-merknader</c>), merker
/// bindende takstvilkår («Det er et vilkår for å utløse takstene …» i logopedforskriften). Regel 3, kapitteltittel som begynner med
/// Merknad/Veiledning/Kommentar, merker blant annet veiledning av doktorgradskandidater, «Veiledningsplikten etter forvaltningsloven § 11»
/// og bindende «Merknader» i vedlegg til EU-forordninger.
/// </para>
/// <para>
/// <b>Egenskap, ikke nodetype.</b> <c>node_type</c> er STRUKTUREN (kapittel, underinndeling, paragraf, ledd, punkt, avslutning): den styrer
/// treet i UI-et, AKN-skrivingen, setningsdelingen og kandidat-/taggesøkene, og et merknadskapittel har samme ledd- og punktstruktur som
/// resten av forskriften. Merkingen er ortogonal på strukturen — samme mønster som <see cref="RettskildeNodeEntitet.Opphevet"/>: en bool på
/// noden, satt ved import, som konverteringen filtrerer på («gjeldende, ikke opphevede noder»). <c>avslutning</c> (#361) ble en nodetype
/// fordi den ER en strukturposisjon (tekst etter en punktliste). Den 1:1-tabellen <see cref="HandbokKommentarMetadataEntitet"/> ble også
/// vurdert og forkastet: den er håndbokforfatterens arbeidsflyt (status, festenivå, publisert), felt som ikke finnes for en Lovdata-node.
/// </para>
/// <para>
/// <b>Overlever ny import:</b> alle Lovdata-veier (første import, ny versjon ved resynk, «konverter på nytt») går gjennom
/// <c>RettskildeImportTjeneste.SettInnNoderOgReferanserAsync</c>, som setter egenskapen fra <see cref="Merk"/>. Eksisterende noder får den
/// av migrasjonen (<see cref="MerknadsnodeMigrering"/>).
/// </para>
/// </summary>
public static partial class Merknadsnoder
{
    /// <summary>[LÅST, #419 avgjørelse 2] Begrunnelsen på maskinelt avviste forslag med slik hjemmel.</summary>
    public const string AvvisningBegrunnelse = "hjemmel er merknad/veiledning (#419)";

    /// <summary>Hvem den maskinelle avvisningen skrives som (<c>sist_endret_av</c> og proveniensen) — maskinen, ikke et menneske
    /// (CLAUDE.md §9: avvist av maskinen skal kunne skilles fra avvist av et menneske).</summary>
    public const string SystemBruker = "system-merknadsnoder";

    /// <summary>Feilmeldingen skriveveien gir når hjemmelen er en merknadsnode.</summary>
    public static string Hjemmelfeil(string eid) =>
        $"Hjemmelen '{eid}' er merknad/veiledning i forskriftsteksten, ikke normtekst (#419). Den kan ikke være hjemmel for en kant, men kan "
        + "siteres som tolkningsargument i begrunnelsen.";

    /// <summary>Regelen som regex (PostgreSQL-<c>~</c> og .NET gir samme svar på dette uttrykket). Migrasjonen har sin egen, frosne kopi.</summary>
    public const string EidMonster = "^kap-(merknad|veiledning|kommentar)";

    [GeneratedRegex(EidMonster, RegexOptions.CultureInvariant)]
    private static partial Regex EidRegex();

    /// <summary>
    /// [LÅST, #419, Johann 2026-10-30] Kapitler som treffer regelen, men er normtekst. Nøklet på rettskildens ELI (stabil på tvers av
    /// versjoner og miljøer) og kapitlets eId — aldri på Guid. En ny rad krever en måling i saken, og eksisterende noder trenger en ny
    /// migrasjon (den frosne lista i <see cref="MerknadsnodeMigrering"/> endres ikke).
    /// </summary>
    public static readonly IReadOnlyList<(string Eli, string Eid, string Grunn)> Unntak =
    [
        ("https://lovdata.no/eli/forskrift/2008/01/09/50/nor", "kap-merknader",
            "Lovdata har kalt vedlegget med standardreglene «Merknader»; § 11 gjør «standardbestemmelsene fastsatt i vedlegget til denne "
            + "forskrift» gjeldende. Bindende normtekst (målt 2026-10-29, #419). Lovdata-feilen er kandidat i #429."),
    ];

    /// <summary>Er noden ROTEN i et merknadskapittel? <paramref name="toppniva"/> = noden har ingen foreldernode (i databasen:
    /// <c>parent_node_id IS NULL</c>).</summary>
    public static bool ErRot(string? eli, string eid, string nodeType, bool toppniva) =>
        toppniva && nodeType == "kapittel" && EidRegex().IsMatch(eid)
        && !Unntak.Any(u => string.Equals(u.Eli, eli, StringComparison.Ordinal) && string.Equals(u.Eid, eid, StringComparison.Ordinal));

    /// <summary>
    /// EId-ene som skal merkes i én rettskilde: røttene (<see cref="ErRot"/>) og hele undertreet deres. En node regnes som toppnivå når
    /// den ikke har noen forelder BLANT NODENE — samme regel som importen bruker når den setter <c>parent_node_id</c> (en forelder-eId
    /// som ikke er en node, blir NULL).
    /// </summary>
    public static HashSet<string> Merk(string? eli, IReadOnlyCollection<(string Eid, string? ParentEid, string NodeType)> noder)
    {
        var eids = noder.Select(n => n.Eid).ToHashSet(StringComparer.Ordinal);
        var barn = noder.Where(n => n.ParentEid is not null && eids.Contains(n.ParentEid))
            .ToLookup(n => n.ParentEid!, n => n.Eid, StringComparer.Ordinal);
        var merket = new HashSet<string>(StringComparer.Ordinal);
        var ko = new Queue<string>(noder
            .Where(n => ErRot(eli, n.Eid, n.NodeType, toppniva: n.ParentEid is null || !eids.Contains(n.ParentEid)))
            .Select(n => n.Eid));
        while (ko.TryDequeue(out var eid))
        {
            if (!merket.Add(eid)) continue;
            foreach (var b in barn[eid]) ko.Enqueue(b);
        }
        return merket;
    }
}

/// <summary>
/// [Ny, issue #419] Data-delen av migrasjonen <c>MerknadsnoderMerkes</c>, skilt ut som konstant slik at <c>MerknadsnoderTests</c> kjører
/// nøyaktig den SQL-en migrasjonen kjører (samme teknikk som <see cref="AvslutningMigrering"/>).
/// <para>
/// <b>Tilbakefyllingen</b> merker eksisterende noder etter regel 1 og unntakslisten (<see cref="Merknadsnoder"/>), i alle versjoner av alle
/// rettskilder — en kant kan peke på en node i en eldre versjon. Den er idempotent: bare noder som ikke alt er merket, oppdateres, og en ny
/// kjøring endrer ingenting. Den fjerner aldri en merking.
/// </para>
/// <para>
/// <b>[LÅST] Frosset sammen med migrasjonen.</b> Unntakslisten står som literal her (en test sjekker at den er lik
/// <see cref="Merknadsnoder.Unntak"/> i dag). Trengs en ny unntaksrad: ny migrasjon, ikke rediger denne.
/// </para>
/// </summary>
public static class MerknadsnodeMigrering
{
    /// <summary>Unntakene i den frosne SQL-en (ELI, eId) — for testen som holder dem lik <see cref="Merknadsnoder.Unntak"/>.</summary>
    public static readonly IReadOnlyList<(string Eli, string Eid)> FrosneUnntak =
    [
        ("https://lovdata.no/eli/forskrift/2008/01/09/50/nor", "kap-merknader"),
    ];

    public const string TilbakefyllSql = """
        WITH RECURSIVE rot AS (
            SELECT n."Id"
            FROM rettskilde_noder n
            JOIN rettskilder r ON r."Id" = n.rettskilde_id
            WHERE n.parent_node_id IS NULL
              AND n.node_type = 'kapittel'
              AND n.eid ~ '^kap-(merknad|veiledning|kommentar)'
              AND NOT EXISTS (
                  SELECT 1 FROM (VALUES ('https://lovdata.no/eli/forskrift/2008/01/09/50/nor', 'kap-merknader')) AS u(eli, eid)
                  WHERE u.eli = r.eli AND u.eid = n.eid)
        ),
        undertre AS (
            SELECT "Id" FROM rot
            UNION
            SELECT c."Id" FROM rettskilde_noder c JOIN undertre t ON c.parent_node_id = t."Id"
        )
        UPDATE rettskilde_noder SET er_merknad = true
        WHERE "Id" IN (SELECT "Id" FROM undertre) AND NOT er_merknad;
        """;
}
