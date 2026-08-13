using System.Text.RegularExpressions;

namespace RegelIde.Kildekonvertering;

/// <summary>Speiler <c>NettsideDokumentEntitet</c> (RegelIde.Data) felt-for-felt — se
/// den klassens kommentar for AVVIKET fra §3.1s dokument+seksjon-par (kun dokument-granularitet
/// denne runden).</summary>
public sealed record NettsideSide
{
    public required string KanoniskUrl { get; init; }
    public string? Tittel { get; init; }
    public string? RaaTekst { get; init; }
    public string? InnholdsHash { get; init; }
}

/// <summary>To deterministiske kanttyper fra en nettside (docs/15-handbok-dokumentgraf-notat.md
/// §3.2) — se <see cref="NettsideTekstParser"/>s klassekommentar for hvorfor disse IKKE gjenbruker
/// <c>RettskildeReferanseEntitet</c>.</summary>
public enum NettsideLenketype
{
    /// <summary>Enhver annen lenke — intern Bergen-side eller ekstern URL.</summary>
    LenkerTil,

    /// <summary>Spesifikt en lovdata.no-URL i det moderne <c>/dokument/</c>-formatet, jf.
    /// <see cref="LovdataUrlTolker"/>.</summary>
    Lovdatalenke,
}

/// <summary>
/// Én funnet lenke-kandidat, FØR databasekobling. <see cref="TilEidKandidat"/> er satt for
/// <see cref="NettsideLenketype.Lovdatalenke"/> når <see cref="LovdataUrlTolker.TolkTilEliKandidat"/>
/// klarte å tolke URL-en — <c>null</c> betyr enten en ordinær ekstern lenke, eller en lovdata.no-URL
/// i et av de eldre, ikke-håndterte formatene (se data/kilder/raw-nettside/README.md). Selve
/// DB-oppslaget (finnes det en <c>RettskildeEntitet</c> med denne <c>Eli</c>? finnes det en
/// <c>NettsideDokumentEntitet</c> med denne <c>KanoniskUrl</c>?) gjøres av
/// <c>RegelIde.Data.NettsideGrafKobler</c>, ikke her — denne parseren er, som
/// <c>HandbokTekstParser</c>, bevisst DB-fri.
/// </summary>
public sealed record NettsideLenkeKandidat(
    NettsideLenketype Type,
    string RaaHref,
    string? AnkerTekst,
    string? TilEidKandidat);

public sealed record NettsideParseResultat(NettsideSide Side, IReadOnlyList<NettsideLenkeKandidat> Lenker);

/// <summary>
/// Sideordnet <see cref="HandbokTekstParser"/> (docs/15-handbok-dokumentgraf-notat.md §3.1/§8 Trinn
/// 4 punkt 12, fremskyndet til denne runden) — men for en kommunal NETTSIDE, ikke en PDF-håndbok.
/// Ren tekstbehandling, INGEN KI, INGEN HTML-parsing (henting og HTML→tekstlag-konvertering skjer
/// FØR denne parseren kalles — se data/kilder/raw-nettside/README.md for metoden brukt denne
/// runden, og §14 i notatet "Ikke i scope" for at ingen <c>NettsideHenterTjeneste</c> er bygget).
///
/// <para>
/// Lenker leses fra <see cref="NettsideSide.RaaTekst"/> selv, som Markdown-lenker
/// <c>[ankertekst](href)</c> — se <c>NettsideDokumentEntitet.RaaTekst</c>s doc-kommentar for
/// hvorfor: feltet er per definisjon tekstlaget, ikke rå HTML, så det kan ikke bære
/// <c>&lt;a href&gt;</c>-attributter direkte. Dette ER den deterministiske "enhver
/// <c>&lt;a href&gt;</c>-lenke"-uttrekkingen §3.2 ber om, bare uttrykt i tekstlagets egen notasjon
/// i stedet for DOM-en.
/// </para>
///
/// <para>
/// Skjemaspørsmålet fra oppgaven — kan <c>RettskildeReferanseEntitet</c> bære
/// <c>lenker_til</c>/<c>lovdatalenke</c> uten en ny tabell? — er NEI, i motsetning til forrige
/// rundes svar for <c>hjemlet_i</c>/<c>kryssrefererer</c>: <c>RettskildeReferanseEntitet.FraNodeId</c>
/// er en FK mot <c>RettskildeNodeEntitet</c>, en annen entitet enn <c>NettsideDokumentEntitet</c> —
/// kilden til kanten er en annen RADTYPE, ikke bare en annen ROLLE på samme radtype (det ville vært
/// det skillet §10.2 selv trakk mellom "én tabell med diskriminator" og "faktisk to forskjellige
/// former"). Ny <c>NettsideLenkeEntitet</c> er derfor riktigere — se den klassens kommentar i
/// Entiteter.cs for full begrunnelse.
/// </para>
/// </summary>
public static partial class NettsideTekstParser
{
    [GeneratedRegex(@"\[([^\]]*)\]\((\S+?)\)")]
    private static partial Regex MarkdownLenkeMønster();

    /// <param name="kanoniskUrl">Absolutt URL — deduplisieringsnøkkelen (§3.4).</param>
    /// <param name="tittel">Sidens tittel, lest utenfor denne parseren (kjent fra hentingen).</param>
    /// <param name="raaTekst">Hovedinnholdets tekstlag, med Markdown-lenker bevart.</param>
    public static NettsideParseResultat Parse(string kanoniskUrl, string? tittel, string? raaTekst)
    {
        var side = new NettsideSide
        {
            KanoniskUrl = kanoniskUrl,
            Tittel = tittel,
            RaaTekst = raaTekst,
            InnholdsHash = raaTekst is not null ? LovdataIdentifikatorer.BeregnTekstHash(raaTekst) : null,
        };

        var lenker = new List<NettsideLenkeKandidat>();
        if (raaTekst is not null)
        {
            foreach (Match m in MarkdownLenkeMønster().Matches(raaTekst))
            {
                var ankerTekst = m.Groups[1].Value;
                var href = m.Groups[2].Value.Trim();
                var eidKandidat = LovdataUrlTolker.TolkTilEliKandidat(href);
                var type = eidKandidat is not null ? NettsideLenketype.Lovdatalenke : NettsideLenketype.LenkerTil;
                lenker.Add(new NettsideLenkeKandidat(type, href, ankerTekst.Length > 0 ? ankerTekst : null, eidKandidat));
            }
        }

        return new NettsideParseResultat(side, lenker);
    }
}
