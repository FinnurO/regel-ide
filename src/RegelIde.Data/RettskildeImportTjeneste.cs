using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>Hva <see cref="RettskildeImportTjeneste.ImporterMedUtfallAsync"/> faktisk gjorde med dokumentet.</summary>
public enum RettskildeImportUtfall
{
    /// <summary>Helt ny rettskilde — fantes ikke fra før (verken som primær eller referanse-stub).</summary>
    Ny,

    /// <summary>En tidligere referanse-stub (§3.1 steg 6) ble forfremmet til en fullt importert primærkilde.</summary>
    ForfremmetStub,

    /// <summary>Innholdet var reelt endret siden forrige import — en ny, versjonert rad ble opprettet (§2.1).</summary>
    NyVersjon,

    /// <summary>Allerede importert med bit-identisk innhold (etter normalisering) — ingen ny rad opprettet.</summary>
    Uendret,
}

/// <summary>Resultatet av én <see cref="RettskildeImportTjeneste.ImporterMedUtfallAsync"/>-kjøring.</summary>
public sealed record RettskildeImportResultat(Guid RettskildeId, RettskildeImportUtfall Utfall);

/// <summary>
/// Persisterer et <see cref="KonverteringResultat"/> (fra RegelIde.Kildekonvertering) til databasen
/// (§2 i teknisk design). Idempotent på ELI: importerer aldri samme gjeldende rettskilde to ganger.
/// Løser/oppretter referanse-stubber for eksterne kryssreferanser (§3.1 steg 6) — dette krever
/// nettopp databasetilgang, og var derfor bevisst utenfor den rene konverteringspipelinens scope.
/// </summary>
public sealed class RettskildeImportTjeneste(RegelIdeDbContext db)
{
    // Placeholder inntil ekte autentisering/brukerkontekst finnes i systemet.
    private const string SystemBruker = "system-import";

    /// <param name="virksomhetId">
    /// NULL (default) = delt/nasjonal kilde (Lov/Forskrift fra Lovdata) — importeres én gang, delt av
    /// alle virksomheter. Satt = denne virksomhetens egen lokale kilde (lokal forskrift,
    /// virksomhetsdokument), kun synlig for/eid av virksomheten. Se docs/00-endringslogg-v0.3.md.
    /// </param>
    /// <param name="opprettetAv">
    /// Navnet som attribueres i `opprettet_av`/proveniens. NULL (default) = systemets egen
    /// oppstarts-seeding (se "system-import" under); API-endepunkter som importerer på vegne av en
    /// faktisk (test)bruker skal alltid sende bruker.Navn her — se GjeldendeBrukerTjeneste i RegelIde.Api.
    /// </param>
    public async Task<Guid> ImporterAsync(
        KonverteringResultat resultat, Guid? virksomhetId = null, string? opprettetAv = null, CancellationToken ct = default) =>
        (await ImporterMedUtfallAsync(resultat, virksomhetId, opprettetAv, ct)).RettskildeId;

    /// <summary>
    /// Som <see cref="ImporterAsync"/>, men returnerer også HVILKET utfall importen faktisk fikk —
    /// til bruk der noe (f.eks. <see cref="LovdataFullimportTjeneste"/>) trenger å telle/rapportere
    /// ny/oppdatert/uendret over et stort antall dokumenter, ikke bare den resulterende id-en.
    /// </summary>
    public async Task<RettskildeImportResultat> ImporterMedUtfallAsync(
        KonverteringResultat resultat, Guid? virksomhetId = null, string? opprettetAv = null, CancellationToken ct = default)
    {
        var attribuertTil = opprettetAv ?? SystemBruker;
        var m = resultat.Metadata;
        var eksisterende = await db.Rettskilder
            .FirstOrDefaultAsync(r => r.Eli == m.Eli && r.VirksomhetId == virksomhetId && r.Entitetsstatus == "gjeldende", ct);

        if (eksisterende is { Importrolle: "primaer" })
        {
            // Referansielt transparent (AknXmlSkriver.cs): samme (metadata, noder) gir bit-identisk
            // AKN-XML uansett importtidspunkt, bortsett fra ÉN linje (FRBRManifestation/@date,
            // name="regel-ide-import") — normaliseres bort før sammenligning, ellers ville ENHVER
            // reimport (selv av helt uendret kilde-HTML) feilaktig blitt tolket som en innholdsendring.
            var uendret = eksisterende.AknXml is not null &&
                NormaliserAknForSammenligning(eksisterende.AknXml) == NormaliserAknForSammenligning(resultat.AknXml);
            if (uendret)
            {
                // KRITISK (del B punkt 3, lovdata-raa-metadata-runden 2026-09-02): en tidlig return HER
                // uten å røre noen felt ville bety at en helt vanlig full-resynk ALDRI bakfyller
                // Url/Innhold/InnholdsHash/Hentet (eller del A sine nye rå metadatafelt) på allerede
                // importerte, uendrede rader — dette ER den eneste veien en helt vanlig resynk faktisk
                // bakfyller EKSISTERENDE rader, uten en egen engangs-backfill-tjeneste. Bevisst IKKE en
                // "reell endring": Versjon økes ikke, ingen ny Proveniens-rad opprettes — kun disse
                // konkrete kolonnene på den eksisterende, gjeldende raden. Billig og idempotent: EF Core
                // sitt change tracker-diff gjør SaveChangesAsync til en no-op når verdiene allerede
                // stemmer (f.eks. andre, tredje, ... gangs resynk av samme uendrede dokument).
                var (innholdUendret, innholdsHashUendret) = BeregnInnhold(resultat.RaaHtml);
                eksisterende.Url = m.Eli;
                eksisterende.Innhold = innholdUendret;
                eksisterende.InnholdsHash = innholdsHashUendret;
                eksisterende.Hentet = DateTimeOffset.UtcNow;
                eksisterende.IkrafttredelseRaa = m.IkrafttredelseRaa;
                eksisterende.KonsolidertDatoRaa = m.KonsolidertDatoRaa;
                eksisterende.SistEndretVed = m.SistEndretVed;
                // §152: (re)backfiller ALLTID fra den ferskt parsede metadataen, ikke bare når NULL fra
                // før — den tidligere sammenslåingsbugen kan ha skrevet en feil (sammenlimt) verdi inn
                // allerede, og en rad med feil verdi ville ellers aldri blitt rettet av en vanlig resynk.
                eksisterende.AnsvarligDepartement = m.AnsvarligDepartement.ToList();
                // [Ny, 2026-09-03, issue #127] — samme "backfill selv når uendret AKN"-begrunnelse som
                // feltene over: disse 10 feltene fantes ikke da de fleste rader sist gikk gjennom denne
                // metodens Ny/NyVersjon-gren, og AknXml (sammenligningsgrunnlaget for "uendret" rett
                // over) inneholder INGEN av dem — uten denne backfillen ville de aldri blitt fylt ut for
                // et allerede importert, tekstlig uendret dokument, uansett hvor mange ganger det resynkes.
                eksisterende.Kunngjort = m.Kunngjort;
                eksisterende.Rettsomrade = m.Rettsomrade;
                eksisterende.EuEosHenvisning = m.EuEosHenvisning;
                eksisterende.DokumentId = m.DokumentId;
                eksisterende.RefId = m.RefId;
                eksisterende.GjelderFor = m.GjelderFor;
                eksisterende.Etat = m.Etat;
                eksisterende.PublisertI = m.PublisertI;
                eksisterende.AnnetOmDokumentet = m.AnnetOmDokumentet;
                eksisterende.SisteRettelse = m.SisteRettelse;

                // [Ny, 2026-09-03, issue #159] Hjemmel/Endring (RettskildeHjemmelEntitet/
                // RettskildeEndringEntitet) settes KUN inn i SettInnNoderOgReferanserAsync, som denne
                // "uendret AKN"-grenen bevisst IKKE kaller (den grenen regenererer aldri noder — §2.1).
                // Men AknXml (sammenligningsgrunnlaget for "uendret" over) inneholder ALDRI hjemmel-/
                // endringsinformasjon (AknXmlSkriver skriver ingen av delene) — en rettskilde hvis
                // Hjemler/Endringer-parsing ble lagt til/rettet ETTER dokumentets siste Ny/NyVersjon-
                // import ville derfor ALDRI fått dem satt inn, uansett hvor mange ganger den resynkes
                // (bekreftet ekte, issue #159: 0 av 24 stikkprøvde "Lov om endringer"-dokumenter hadde
                // noen endringer-rad til tross for at Lovdatas egen HTML for et av dem bekreftet
                // inneholder et korrekt parsbart <dt class="changesToDocuments">). Idempotent og trygt å
                // gjøre ubetinget her: kun når raden IKKE allerede har noen (unngår duplikater ved
                // gjentatte resynker av en rad som allerede fikk sine hjemler/endringer satt inn via en
                // ordinær Ny/NyVersjon-import).
                await BackfillHjemlerOgEndringerAsync(eksisterende.Id, resultat, ct);
                await db.SaveChangesAsync(ct);

                // allerede importert, ingen reell endring — ikke dupliser (§2.1)
                return new RettskildeImportResultat(eksisterende.Id, RettskildeImportUtfall.Uendret);
            }

            // §2.1: en ny konsolidert versjon er en helt ny rettskilder-rad, aldri en inkrementell
            // oppdatering av den gamle. QuoteSelector-relokering av eksisterende tagger skjer i samme slag.
            var nyVersjonId = await OpprettNyVersjonAsync(eksisterende, resultat, virksomhetId, attribuertTil, ct);
            return new RettskildeImportResultat(nyVersjonId, RettskildeImportUtfall.NyVersjon);
        }

        Guid rettskildeId;
        var forfremmetStub = false;
        if (eksisterende is { Importrolle: "referanse" })
        {
            forfremmetStub = true;
            // Forfremmelse av en tidligere opprettet referanse-stub (§3.1 steg 6) til en fullt importert primærkilde.
            rettskildeId = eksisterende.Id;
            eksisterende.Importrolle = "primaer";
            eksisterende.Doctype = m.Doctype;
            eksisterende.Kildetype = m.Kildetype.ToString();
            eksisterende.Tittel = m.Tittel;
            eksisterende.Kortnavn = m.Kortnavn;
            eksisterende.AknXml = resultat.AknXml;
            eksisterende.Ikrafttredelse = m.Ikrafttredelse;
            eksisterende.IkrafttredelseRaa = m.IkrafttredelseRaa;
            eksisterende.KonsolidertDato = m.KonsolidertDato;
            eksisterende.KonsolidertDatoRaa = m.KonsolidertDatoRaa;
            eksisterende.SistEndretVed = m.SistEndretVed;
            eksisterende.Utgiver = m.Utgiver;
            eksisterende.AnsvarligDepartement = m.AnsvarligDepartement.ToList();
            eksisterende.Kunngjort = m.Kunngjort;
            eksisterende.Rettsomrade = m.Rettsomrade;
            eksisterende.EuEosHenvisning = m.EuEosHenvisning;
            eksisterende.DokumentId = m.DokumentId;
            eksisterende.RefId = m.RefId;
            eksisterende.GjelderFor = m.GjelderFor;
            eksisterende.Etat = m.Etat;
            eksisterende.PublisertI = m.PublisertI;
            eksisterende.AnnetOmDokumentet = m.AnnetOmDokumentet;
            eksisterende.SisteRettelse = m.SisteRettelse;
            eksisterende.Status = m.Status;
            // Del B (2026-09-02) — stubben har aldri hatt ekte HTML før nå (den ble opprettet av
            // FinnEllerOpprettReferanseStubAsync, uten Innhold), så dette er FØRSTE gang disse feltene
            // fylles ut for denne raden, akkurat som AknXml over.
            {
                var (innholdForfremmet, innholdsHashForfremmet) = BeregnInnhold(resultat.RaaHtml);
                eksisterende.Url = m.Eli;
                eksisterende.Innhold = innholdForfremmet;
                eksisterende.InnholdsHash = innholdsHashForfremmet;
                eksisterende.Hentet = DateTimeOffset.UtcNow;
            }
            eksisterende.SistEndretAv = attribuertTil;
            eksisterende.SistEndretTidspunkt = DateTimeOffset.UtcNow;
            eksisterende.Versjon++; // basemetadata §0: "heltall, økende" -- appens ansvar å øke ved faktisk endring
            db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde", rettskildeId, virksomhetId, "endret", attribuertTil));
        }
        else
        {
            rettskildeId = Guid.NewGuid();
            var (innholdNy, innholdsHashNy) = BeregnInnhold(resultat.RaaHtml);
            var naaNy = DateTimeOffset.UtcNow;
            db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = rettskildeId,
                VirksomhetId = virksomhetId,
                Doctype = m.Doctype,
                Kildetype = m.Kildetype.ToString(),
                Importrolle = "primaer",
                Tittel = m.Tittel,
                Kortnavn = m.Kortnavn,
                Eli = m.Eli,
                AknXml = resultat.AknXml,
                Ikrafttredelse = m.Ikrafttredelse,
                IkrafttredelseRaa = m.IkrafttredelseRaa,
                KonsolidertDato = m.KonsolidertDato,
                KonsolidertDatoRaa = m.KonsolidertDatoRaa,
                SistEndretVed = m.SistEndretVed,
                Utgiver = m.Utgiver,
                AnsvarligDepartement = m.AnsvarligDepartement.ToList(),
                Kunngjort = m.Kunngjort,
                Rettsomrade = m.Rettsomrade,
                EuEosHenvisning = m.EuEosHenvisning,
                DokumentId = m.DokumentId,
                RefId = m.RefId,
                GjelderFor = m.GjelderFor,
                Etat = m.Etat,
                PublisertI = m.PublisertI,
                AnnetOmDokumentet = m.AnnetOmDokumentet,
                SisteRettelse = m.SisteRettelse,
                Status = m.Status,
                Url = m.Eli,
                Innhold = innholdNy,
                InnholdsHash = innholdsHashNy,
                Hentet = naaNy,
                OpprettetAv = attribuertTil,
                OpprettetTidspunkt = naaNy,
            });
            db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde", rettskildeId, virksomhetId, "opprettet", attribuertTil));
        }

        await SettInnNoderOgReferanserAsync(rettskildeId, resultat, ct);
        await db.SaveChangesAsync(ct);
        return new RettskildeImportResultat(
            rettskildeId, forfremmetStub ? RettskildeImportUtfall.ForfremmetStub : RettskildeImportUtfall.Ny);
    }

    /// <summary>
    /// Setter inn hele node-/referanse-treet for en (ny eller nyopprettet-versjon-av) rettskilde.
    /// Utledet fra den opprinnelige innsettingsløkken i <see cref="ImporterAsync"/> — brukt både der
    /// og av <see cref="OpprettNyVersjonAsync"/>, som trenger nøyaktig samme logikk mot en ny rettskildeId.
    /// </summary>
    private async Task SettInnNoderOgReferanserAsync(Guid rettskildeId, KonverteringResultat resultat, CancellationToken ct)
    {
        var nodeIdVedEid = resultat.Noder.ToDictionary(n => n.Eid, _ => Guid.NewGuid());
        foreach (var n in resultat.Noder)
        {
            // TryGetValue, IKKE GetValueOrDefault: nodeIdVedEid er Dictionary<string, Guid> (Guid er en
            // value type), så GetValueOrDefault på et manglende nøkkel returnerer Guid.Empty — IKKE null
            // — og ville brutt FK-constrainten mot rettskilde_noder (bekreftet ekte, en paragraf- OG
            // kapittelfri lov hvis ledd har eId scopet til dokumentets ELI, men ParentEid=null siden ELI'en
            // ikke er noen ekte nodes eId — se ParseLedd i LovdataHtmlParser). Samme lærdom/mønster som
            // allerede etablert i HandbokImportTjeneste.cs.
            Guid? parentNodeId = null;
            if (n.ParentEid is not null && nodeIdVedEid.TryGetValue(n.ParentEid, out var funnetParentNodeId))
            {
                parentNodeId = funnetParentNodeId;
            }

            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = nodeIdVedEid[n.Eid],
                RettskildeId = rettskildeId,
                Eid = n.Eid,
                Kildesystem = n.Kildesystem,
                KildeId = n.KildeId,
                OffisiellEli = null,
                ParentNodeId = parentNodeId,
                NodeType = n.NodeType.TilDbVerdi(),
                Nummer = n.Nummer,
                Overskrift = n.Overskrift,
                Tekst = n.Tekst,
                TekstHash = n.TekstHash,
                Opphevet = n.Opphevet,
                OpphevetDato = n.OpphevetDato,
                Sorteringsrekkefolge = n.SorteringsRekkefolge,
            });
        }

        // UNIQUE(fra_node_id, til_rettskilde_id, til_eid) er der nettopp for å "forhindre duplikatimport
        // av samme referanse" (§2-kommentaren) — samme løpetekst kan lenke til samme mål flere ganger
        // innenfor ett og samme ledd (bekreftet reelt i alkoholloven), så vi dedupliserer her istedenfor
        // å la databasen kaste en constraint-feil på noe skjemaet selv sier skal tolereres.
        var alleredeLagtTil = new HashSet<(string FraEid, string TilEid)>();
        foreach (var r in resultat.Referanser)
        {
            if (!nodeIdVedEid.TryGetValue(r.FraNodeEid, out var fraNodeId)) continue;
            if (!alleredeLagtTil.Add((r.FraNodeEid, r.TilEid))) continue;

            var tilRettskildeId = r.ErInternReferanse
                ? rettskildeId
                : await FinnEllerOpprettReferanseStubAsync(r.TilEid, ct);

            db.RettskildeReferanser.Add(new RettskildeReferanseEntitet
            {
                Id = Guid.NewGuid(),
                FraNodeId = fraNodeId,
                TilRettskildeId = tilRettskildeId,
                TilEid = r.TilEid,
                Opprinnelse = "import",
                TekstStart = r.TekstStart,
                TekstLengde = r.TekstLengde,
            });
        }

        // Hjemmel/Endring — se de to dedikerte metodene under (utledet ut i egne metoder 2026-09-03,
        // issue #159, slik at RettskildeImportTjeneste.ImporterMedUtfallAsync sin "uendret AKN"-gren
        // også kan kalle dem, se BackfillHjemlerOgEndringerAsync).
        await SettInnHjemlerAsync(rettskildeId, resultat.Hjemler, ct);
        await SettInnEndringerAsync(rettskildeId, resultat.Endringer, ct);
    }

    /// <summary>
    /// Hjemmel (2026-08-30, docs-kommentar RettskildeHjemmelEntitet) — DOKUMENTNIVÅ, ikke per-node som
    /// løpetekst-referansene, derfor ingen FraNodeId-oppslag her. Samme stub-mekanisme som eksterne
    /// løpetekst-referanser: en lov som ikke (ennå) er importert får en referanse-stub opprettet (eller
    /// gjenbrukt), akkurat som FinnEllerOpprettReferanseStubAsync allerede gjør — ingen egen,
    /// andre-gjettet kobling (§3.3) noe sted i denne stien.
    /// </summary>
    private async Task SettInnHjemlerAsync(Guid rettskildeId, IReadOnlyList<RettskildeHjemmel> hjemler, CancellationToken ct)
    {
        // [Ny, 2026-09-04] Bekreftet ekte, live (ikke bare teoretisk): "Instruks om utstedelse av
        // sertifikater for levende dyr og animalske næringsmidler mv." parses til to Hjemmel-treff med
        // NØYAKTIG samme Eid — uten denne dedupliseringen kastet SaveChangesAsync en
        // PostgresException (23505, ux_rettskilde_hjemler_rettskilde_id_hjemmel_eid), som gjorde HELE
        // importen av dokumentet feile (ingen av dets øvrige felt — inkl. AnsvarligDepartement — ble
        // lagret heller, siden hele transaksjonen rulles tilbake). DISTINCT på Eid ALENE (ikke hele
        // RettskildeHjemmel-recorden) — (RettskildeId, HjemmelEid) ER unikhets-nøkkelen, samme par kan
        // aldri lovlig forekomme to ganger uansett hvilken Sorteringsrekkefolge det andre treffet hadde.
        foreach (var h in hjemler.DistinctBy(h => h.Eid))
        {
            var hjemmelRettskildeId = await FinnEllerOpprettReferanseStubAsync(h.Eid, ct);
            db.RettskildeHjemler.Add(new RettskildeHjemmelEntitet
            {
                Id = Guid.NewGuid(),
                RettskildeId = rettskildeId,
                HjemmelEid = h.Eid,
                HjemmelRettskildeId = hjemmelRettskildeId,
                Sorteringsrekkefolge = h.Sorteringsrekkefolge,
            });
        }
    }

    /// <summary>
    /// Endring (2026-09-02, docs-kommentar RettskildeEndringEntitet) — DOKUMENTNIVÅ, semantisk MOTSATT
    /// av Hjemmel over (denne rettskilden ENDRER target, ikke hjemlet i target), men NØYAKTIG samme
    /// stub-mekanisme.
    /// </summary>
    private async Task SettInnEndringerAsync(Guid rettskildeId, IReadOnlyList<RettskildeEndring> endringer, CancellationToken ct)
    {
        // Samme dedupliseringsbehov som SettInnHjemlerAsync over (ux_rettskilde_endringer_rettskilde_id_
        // endring_eid er samme slags (RettskildeId, Eid)-unikhet) — forebyggende her, ikke bekreftet
        // ekte truffet ennå for Endring-siden, men samme parsing-form (samme Eid kan i prinsippet
        // forekomme flere ganger i kildeteksten) gjør den like sårbar.
        foreach (var end in endringer.DistinctBy(end => end.Eid))
        {
            var endringRettskildeId = await FinnEllerOpprettReferanseStubAsync(end.Eid, ct);
            db.RettskildeEndringer.Add(new RettskildeEndringEntitet
            {
                Id = Guid.NewGuid(),
                RettskildeId = rettskildeId,
                EndringEid = end.Eid,
                EndringRettskildeId = endringRettskildeId,
                Sorteringsrekkefolge = end.Sorteringsrekkefolge,
            });
        }
    }

    /// <summary>
    /// [Ny, 2026-09-03, issue #159] Backfiller Hjemler/Endringer for en rettskilde hvis "uendret AKN"-
    /// gren i <see cref="ImporterMedUtfallAsync"/> tok — se kallstedets kommentar for hvorfor dette er
    /// den ENESTE veien disse noensinne blir satt inn for en rad som ble Ny/NyVersjon-importert FØR
    /// Hjemmel-/Endring-parsingen fantes eller hadde en bug som hindret et konkret treff. Betinget på
    /// "ingen eksisterende rader ennå" (ikke ubetinget alltid-slett-og-sett-inn-på-nytt): en rad som
    /// allerede FIKK sine hjemler/endringer satt inn via en ordinær import skal ikke få dem duplisert
    /// ved en senere, uendret resynk.
    /// </summary>
    private async Task BackfillHjemlerOgEndringerAsync(Guid rettskildeId, KonverteringResultat resultat, CancellationToken ct)
    {
        if (resultat.Hjemler.Count > 0 && !await db.RettskildeHjemler.AnyAsync(h => h.RettskildeId == rettskildeId, ct))
        {
            await SettInnHjemlerAsync(rettskildeId, resultat.Hjemler, ct);
        }
        if (resultat.Endringer.Count > 0 && !await db.RettskildeEndringer.AnyAsync(e => e.RettskildeId == rettskildeId, ct))
        {
            await SettInnEndringerAsync(rettskildeId, resultat.Endringer, ct);
        }
    }

    /// <summary>
    /// §2.1: en ny konsolidert versjon av en allerede-primær rettskilde. Ny <see cref="RettskildeEntitet"/>-
    /// rad (samme Eli, Versjon+1, ErstatterId til den gamle), gammel rad merkes 'erstattet', fullt nytt
    /// node-/referanse-tre, og eksisterende tagger relokeres via quoteSelector (05-arkitektur-og-nfk.md §3.1).
    /// </summary>
    private async Task<Guid> OpprettNyVersjonAsync(
        RettskildeEntitet gammel, KonverteringResultat resultat, Guid? virksomhetId, string attribuertTil, CancellationToken ct)
    {
        var m = resultat.Metadata;
        var nyRettskildeId = Guid.NewGuid();
        var (innholdNyVersjon, innholdsHashNyVersjon) = BeregnInnhold(resultat.RaaHtml);
        var naaNyVersjon = DateTimeOffset.UtcNow;
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = nyRettskildeId,
            VirksomhetId = virksomhetId,
            Doctype = m.Doctype,
            Kildetype = m.Kildetype.ToString(),
            Importrolle = "primaer",
            Tittel = m.Tittel,
            Kortnavn = m.Kortnavn,
            Eli = m.Eli,
            AknXml = resultat.AknXml,
            Ikrafttredelse = m.Ikrafttredelse,
            IkrafttredelseRaa = m.IkrafttredelseRaa,
            KonsolidertDato = m.KonsolidertDato,
            KonsolidertDatoRaa = m.KonsolidertDatoRaa,
            SistEndretVed = m.SistEndretVed,
            Utgiver = m.Utgiver,
            AnsvarligDepartement = m.AnsvarligDepartement.ToList(),
            Kunngjort = m.Kunngjort,
            Rettsomrade = m.Rettsomrade,
            EuEosHenvisning = m.EuEosHenvisning,
            DokumentId = m.DokumentId,
            RefId = m.RefId,
            GjelderFor = m.GjelderFor,
            Etat = m.Etat,
            PublisertI = m.PublisertI,
            AnnetOmDokumentet = m.AnnetOmDokumentet,
            SisteRettelse = m.SisteRettelse,
            Status = m.Status,
            Url = m.Eli,
            Innhold = innholdNyVersjon,
            InnholdsHash = innholdsHashNyVersjon,
            Hentet = naaNyVersjon,
            Versjon = gammel.Versjon + 1,
            ErstatterId = gammel.Id,
            OpprettetAv = attribuertTil,
            OpprettetTidspunkt = naaNyVersjon,
        });
        gammel.Entitetsstatus = "erstattet";
        db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde", nyRettskildeId, virksomhetId, "endret", attribuertTil));

        await SettInnNoderOgReferanserAsync(nyRettskildeId, resultat, ct);
        await RelokerTaggerAsync(gammel.Id, nyRettskildeId, resultat.Noder, ct);

        await db.SaveChangesAsync(ct);
        return nyRettskildeId;
    }

    /// <summary>
    /// QuoteSelector-relokering (05-arkitektur-og-nfk.md §3.1) av gjeldende tagger fra den gamle
    /// rettskilde-versjonen til den nye. Rekkefølge: (1) rask vei — uendret eid+tekst_hash, kun
    /// RettskildeId flyttes; (2) quoteSelector-søk — nøyaktig ett substring-treff for QuoteExact et
    /// sted i det nye node-settet, offset/eid/hash oppdateres til treffet; (3) verken eller, eller
    /// flertydig treff — flagg KreverGjennomgang, la RettskildeId fortsatt peke på den (nå erstattede)
    /// gamle raden slik at sitatkonteksten forblir inspiserbar.
    /// </summary>
    private async Task RelokerTaggerAsync(Guid gammelRettskildeId, Guid nyRettskildeId, IReadOnlyList<RettskildeNode> nyeNoder, CancellationToken ct)
    {
        var gjeldendeTagger = await db.TekstTagger
            .Where(t => t.RettskildeId == gammelRettskildeId && t.Entitetsstatus == "gjeldende")
            .ToListAsync(ct);
        if (gjeldendeTagger.Count == 0) return;

        var nyeNoderVedEid = nyeNoder.ToDictionary(n => n.Eid);
        var bladNoderMedTekst = nyeNoder.Where(n => n.Tekst is not null).ToList();

        foreach (var tagg in gjeldendeTagger)
        {
            if (nyeNoderVedEid.TryGetValue(tagg.NodeEid, out var sammeEid) && sammeEid.TekstHash == tagg.NodeTekstHash)
            {
                // Rask vei: nøyaktig samme node, ordrett uendret tekst — kun RettskildeId flyttes.
                tagg.RettskildeId = nyRettskildeId;
                continue;
            }

            var treff = bladNoderMedTekst
                .Select(n => (Node: n, Indeks: n.Tekst!.IndexOf(tagg.QuoteExact, StringComparison.Ordinal)))
                .Where(t => t.Indeks >= 0)
                .ToList();

            if (treff.Count == 1)
            {
                var (node, indeks) = treff[0];
                tagg.RettskildeId = nyRettskildeId;
                tagg.NodeEid = node.Eid;
                tagg.StartOffset = indeks;
                tagg.EndOffset = indeks + tagg.QuoteExact.Length;
                tagg.NodeTekstHash = node.TekstHash!;
            }
            else
            {
                // Ingen eller flertydig treff — flagg for manuell gjennomgang. RettskildeId røres IKKE:
                // taggen blir stående koblet til den (nå erstattede) gamle raden, slik at sitatkonteksten
                // (quotePrefix/quoteExact/quoteSuffix) fortsatt er inspiserbar for en jurist.
                tagg.KreverGjennomgang = true;
            }
        }
    }

    /// <summary>
    /// Fjerner den ene linjen som legitimt varierer med importtidspunkt (FRBRManifestation/@date,
    /// AknXmlSkriver.cs) før to AKN-XML-strenger sammenlignes for reell innholdsendring.
    /// </summary>
    private static readonly Regex ImportDatoLinje = new("""<FRBRdate date="[^"]*" name="regel-ide-import"/>""", RegexOptions.Compiled);

    private static string NormaliserAknForSammenligning(string aknXml) => ImportDatoLinje.Replace(aknXml, "");

    /// <summary>
    /// Del B (lovdata-raa-metadata-runden, 2026-09-02): <see cref="RettskildeEntitet.Innhold"/> (rå
    /// UTF-8-bytes, uendret original) + <see cref="RettskildeEntitet.InnholdsHash"/>. Hash-en gjenbruker
    /// BEVISST <see cref="LovdataIdentifikatorer.BeregnTekstHash"/> — samme SHA-256-hex-funksjon som
    /// allerede brukes for ALLE andre <c>InnholdsHash</c>-felt i kodebasen (EksternKildeEntitet via
    /// AltinnRessursHenter/KommuneTjenesteHenter/OppgaveregisterHenter/TjenestelisteImporter — samtlige
    /// hasher rå tekst direkte med denne, ikke en egen bytes-hash) — i stedet for å skrive en ny,
    /// parallell hash-funksjon for nøyaktig samme formål.
    /// </summary>
    private static (byte[] Innhold, string InnholdsHash) BeregnInnhold(string raaHtml) =>
        (Encoding.UTF8.GetBytes(raaHtml), LovdataIdentifikatorer.BeregnTekstHash(raaHtml));

    /// <summary>
    /// Finner en eksisterende rettskilde (primær eller stub) for en ekstern referansemål-ELI, eller
    /// oppretter en referanse-stub (importrolle='referanse', akn_xml=NULL) — §3.1 steg 6.
    /// </summary>
    private async Task<Guid> FinnEllerOpprettReferanseStubAsync(string tilEidEllerEli, CancellationToken ct)
    {
        var dokumentEli = DokumentEliFra(tilEidEllerEli);

        var eksisterende = await db.Rettskilder.FirstOrDefaultAsync(r => r.Eli == dokumentEli && r.Entitetsstatus == "gjeldende", ct);
        if (eksisterende is not null) return eksisterende.Id;

        // Kan allerede være lagt til (men ikke lagret) tidligere i samme import-batch.
        var sporet = db.ChangeTracker.Entries<RettskildeEntitet>()
            .Select(e => e.Entity)
            .FirstOrDefault(r => r.Eli == dokumentEli);
        if (sporet is not null) return sporet.Id;

        var (kildetype, doctype) = TolkKildetypeFraEli(dokumentEli);
        var stubId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = stubId,
            Doctype = doctype,
            Kildetype = kildetype,
            Importrolle = "referanse",
            Tittel = dokumentEli, // ingen ekte tittel tilgjengelig før stubben forfremmes ved faktisk import
            Eli = dokumentEli,
            AknXml = null,
            Status = "Utkast",
            OpprettetAv = SystemBruker,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        // Stubber (§3.1 steg 6) er en automatisk konsekvens av å parse en kryssreferanse, ikke noe
        // den kallende brukeren bevisst forfattet — attribueres derfor alltid til systemet, uansett
        // hvem som utløste importen som fant referansen.
        db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde", stubId, virksomhetId: null, "opprettet", SystemBruker));
        return stubId;
    }

    /// <summary>Trunkerer en eId (som kan ha et paragraf-/ledd-/punkt-suffiks) til dokumentets egen ELI, ved "/nor".</summary>
    private static string DokumentEliFra(string eidEllerEli)
    {
        var idx = eidEllerEli.IndexOf("/nor", StringComparison.Ordinal);
        return idx < 0 ? eidEllerEli : eidEllerEli[..(idx + 4)];
    }

    private static (string Kildetype, string Doctype) TolkKildetypeFraEli(string eli)
    {
        if (eli.Contains("/eli/lov/", StringComparison.Ordinal)) return ("Lov", "act");
        if (eli.Contains("/eli/forskrift/", StringComparison.Ordinal)) return ("Forskrift", "act");
        throw new NotSupportedException(
            $"Ukjent kildetype i ELI '{eli}' — verken lov eller forskrift. Ingen gjettet fallback (§3.3).");
    }
}
