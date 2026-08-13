using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Persisterer et <see cref="NettsideParseResultat"/> (fra <see cref="NettsideTekstParser.Parse"/>,
/// UENDRET denne runden) som en ekte <see cref="RettskildeEntitet"/> — punkt 8 (avklaringsrunde
/// 2026-08-13): full konvergens, `NettsideDokumentEntitet` fjernet. Direkte sideordnet
/// <see cref="HandbokImportTjeneste"/> (samme filstruktur/stil, samme to-delte "parser er DB-fri, en
/// egen tjeneste gjør selve DB-skrivingen"-arkitektur som <see cref="RettskildeImportTjeneste"/> og
/// <see cref="NettsideGrafKobler"/> også følger).
///
/// <para>
/// **Skjema-mapping fra det gamle <c>NettsideDokumentEntitet</c>** (se <see cref="Entiteter"/>-filens
/// nå fjernede klassekommentar for opprinnelig begrunnelse, uendret i sak): <c>KanoniskUrl</c> →
/// <see cref="RettskildeEntitet.Url"/> (feltet fantes allerede, brukt av håndbok-URL-er — samme
/// gjenbrukte navnekonvensjon som Lag 1 i forrige runde); <c>Tittel</c>/<c>Hentet</c>/
/// <c>InnholdsHash</c> → SAMME navn på <see cref="RettskildeEntitet"/> (fantes allerede); <c>RaaTekst</c>
/// → teksten på ÉN ny <see cref="RettskildeNodeEntitet"/> med <c>NodeType="side"</c> (ingen
/// DB-CHECK-constraint hindrer nye NodeType-verdier — samme presedens som HandbokImportTjeneste
/// bruker for "kapittel"/"avsnitt"). Dette bevarer dagens granularitet ærlig: en nettside har ingen
/// intern struktur å høste (§3.1s "kun dokument-granularitet" gjelder fortsatt, nå for
/// Brukerveiledning-doctypen spesifikt, se docs/15-notatet) — vi later ikke som den har det ved å
/// finne opp kunstige underseksjoner.
/// </para>
///
/// <para>
/// **Importrolle="primaer", MED en minimal AKN-plassholder — samme fiks som nettopp gjort for
/// håndbok.** <see cref="RettskildeRepository.AlleRettskilderAsync"/> filtrerer eksplisitt på
/// <c>Importrolle == "primaer"</c> — en Brukerveiledning med "referanse" ville vært usynlig i
/// rettskilder-listen, kun nåbar direkte på GUID. <see cref="MinimalAknPlassholder"/> er en EGEN,
/// minimal kopi (samme v1-forenkling som <c>HandbokImportTjeneste</c>s egen kopi) — statisk
/// plassholder, ikke en ekte AKN-serialisering av sidens tekst.
/// </para>
///
/// <para>
/// **Idempotent på Url** — samme dedupliseringsintensjon som den gamle
/// <c>ux_nettside_dokumenter_kanonisk_url</c>-UNIQUE-indeksen hadde, nå håndhevet i applikasjonslaget
/// (samme stil som <c>HandbokImportTjeneste</c> dedupliserer på Tittel+Kildetype+VirksomhetId uten en
/// egen DB-constraint): et gjentatt <see cref="LagreDokumentAsync"/>-kall for samme
/// <see cref="RettskildeEntitet.Url"/> OPPDATERER innholdet i stedet for å duplisere raden, og
/// erstatter de gamle utgående <see cref="NettsideLenkeEntitet"/>-kandidatene (samme reimport-mønster
/// som den opprinnelige <c>NettsideGrafKobler.LagreDokumentAsync</c> hadde).
/// </para>
/// </summary>
public sealed class BrukerveiledningImportTjeneste(RegelIdeDbContext db)
{
    private const string SystemBruker = "system-import";

    /// <summary>Eid-en til sidens eneste node — konstant, siden det per definisjon kun finnes ÉN
    /// (§3.1: kun dokument-granularitet, ingen seksjonsnivå å adressere separat).</summary>
    private const string SideEid = "side";

    public async Task<Guid> LagreDokumentAsync(
        NettsideParseResultat resultat, Guid? virksomhetId = null, string? opprettetAv = null, CancellationToken ct = default)
    {
        var side = resultat.Side;
        var attribuertTil = opprettetAv ?? SystemBruker;

        var eksisterende = await db.Rettskilder
            .FirstOrDefaultAsync(r => r.Url == side.KanoniskUrl && r.Kildetype == "Brukerveiledning", ct);

        Guid rettskildeId;
        Guid sideNodeId;

        if (eksisterende is null)
        {
            var rettskilde = new RettskildeEntitet
            {
                Id = Guid.NewGuid(),
                VirksomhetId = virksomhetId,
                Doctype = "webside",
                Kildetype = "Brukerveiledning",
                Importrolle = "primaer", // se klassekommentaren — ekte innhold, ikke en sitat-stubb.
                Tittel = side.Tittel ?? side.KanoniskUrl,
                AknXml = MinimalAknPlassholder(side.Tittel ?? side.KanoniskUrl),
                Status = "Gjeldende",
                Url = side.KanoniskUrl,
                InnholdsHash = side.InnholdsHash,
                Hentet = DateTimeOffset.UtcNow,
                OpprettetAv = attribuertTil,
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            db.Rettskilder.Add(rettskilde);
            db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde", rettskilde.Id, virksomhetId, "opprettet", attribuertTil));

            var sideNode = new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(),
                RettskildeId = rettskilde.Id,
                Eid = SideEid,
                Kildesystem = "regel-ide",
                KildeId = SideEid,
                NodeType = "side",
                Tekst = side.RaaTekst,
                TekstHash = side.RaaTekst is not null ? LovdataIdentifikatorer.BeregnTekstHash(side.RaaTekst) : null,
                Sorteringsrekkefolge = 0,
            };
            db.RettskildeNoder.Add(sideNode);

            rettskildeId = rettskilde.Id;
            sideNodeId = sideNode.Id;
        }
        else
        {
            // Reimport av samme URL (§3.4-dedup) — oppdater innholdet i stedet for å duplisere raden.
            eksisterende.Tittel = side.Tittel ?? side.KanoniskUrl;
            eksisterende.InnholdsHash = side.InnholdsHash;
            eksisterende.Hentet = DateTimeOffset.UtcNow;
            rettskildeId = eksisterende.Id;

            var sideNode = await db.RettskildeNoder.SingleAsync(n => n.RettskildeId == rettskildeId && n.Eid == SideEid, ct);
            sideNode.Tekst = side.RaaTekst;
            sideNode.TekstHash = side.RaaTekst is not null ? LovdataIdentifikatorer.BeregnTekstHash(side.RaaTekst) : null;
            sideNodeId = sideNode.Id;

            // Rene lenke-rader fra forrige import fjernes og skrives på nytt — samme begrunnelse som
            // den opprinnelige NettsideGrafKobler.LagreDokumentAsync hadde: lenkene har ingen
            // nedstrøms FK-er andre rader avhenger av. Sti-rader rører vi IKKE her.
            var gamleLenker = await db.NettsideLenker.Where(l => l.FraNodeId == sideNodeId).ToListAsync(ct);
            db.NettsideLenker.RemoveRange(gamleLenker);
        }

        foreach (var kandidat in resultat.Lenker)
        {
            db.NettsideLenker.Add(new NettsideLenkeEntitet
            {
                Id = Guid.NewGuid(),
                FraNodeId = sideNodeId,
                Type = kandidat.Type == NettsideLenketype.Lovdatalenke ? "lovdatalenke" : "lenker_til",
                RaaHref = kandidat.RaaHref,
                AnkerTekst = kandidat.AnkerTekst,
                TilEidKandidat = kandidat.TilEidKandidat,
            });
        }

        await db.SaveChangesAsync(ct);
        return rettskildeId;
    }

    /// <summary>§3.4: lagre ALLE stier en nettside opptrer under, som separate rader — idempotent
    /// (finnes raden allerede, gjøres ingenting).</summary>
    public async Task LeggTilStiAsync(Guid rettskildeId, string sti, string stiType, CancellationToken ct = default)
    {
        var finnes = await db.NettsideStier.AnyAsync(
            s => s.RettskildeId == rettskildeId && s.StiType == stiType && s.Sti == sti, ct);
        if (finnes) return;

        db.NettsideStier.Add(new NettsideStiEntitet
        {
            Id = Guid.NewGuid(),
            RettskildeId = rettskildeId,
            Sti = sti,
            StiType = stiType,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Egen, minimal kopi av <see cref="HandbokForfatterTjeneste.MinimalAknPlassholder"/>/
    /// <c>HandbokImportTjeneste.MinimalAknPlassholder</c> (begge <c>private</c>, ikke delt) — samme
    /// v1-forenkling: tilfredsstiller KUN <c>ck_rettskilder_akn_xml</c>
    /// (non-null for <c>importrolle='primaer'</c>), ingen ekte AKN-serialisering av sidens tekst.
    /// <c>rettskilde_noder</c> er og blir autoritativ kilde for lesing, akkurat som i HandbokImportTjeneste.
    /// </summary>
    private static string MinimalAknPlassholder(string tittel)
    {
        var tekst = System.Net.WebUtility.HtmlEncode(tittel);
        return $"""
            <akomaNtoso xmlns="http://docs.oasis-open.org/legaldocml/ns/akn/3.0">
              <doc name="brukerveiledning">
                <meta>
                  <proprietary source="#regel-ide">
                    <regelIde:kildetype>Brukerveiledning</regelIde:kildetype>
                    <regelIde:status>Hentet</regelIde:status>
                  </proprietary>
                </meta>
                <preface><p>{tekst}</p></preface>
                <body/>
              </doc>
            </akomaNtoso>
            """;
    }
}
