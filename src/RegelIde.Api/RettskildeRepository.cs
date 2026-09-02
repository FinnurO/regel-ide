using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Databasebacket register over rettskilder (§2 i teknisk design). Scoped (per request), speiler
/// DbContext sin levetid.
///
/// Åpne data, ikke virksomhets-lukket (2026-07-24, jf. eksisterende publiseringsfilosofi i
/// 05-arkitektur-og-nfk.md §1.2 — "publisert" har alltid betydd "gjøres tilgjengelig i regel-IDEs
/// eget høstbare/lesbare endepunkt"): alle metoder viser kun rettskilder med
/// <c>Status != "Utkast"</c> — kladder (ikke menneskelig verifisert ennå, §3.1 steg 10 i teknisk
/// design) er aldri offentlig synlige, uansett virksomhet. <c>virksomhetId</c> er en valgfri
/// filtrering for å snevre inn til én virksomhets bidrag, ikke en tilgangssperre — utelates den,
/// vises alt som er synlig (delte/nasjonale kilder + alle virksomheters publiserte lokale kilder),
/// akkurat som en nasjonal åpne-data-katalog aggregerer på tvers av alle bidragsytere.
/// </summary>
public sealed class RettskildeRepository(RegelIdeDbContext db, VirksomhetOppslagTjeneste virksomhetOppslag)
{
    private const string UtkastStatus = "Utkast";

    /// <summary>
    /// <paramref name="inkluderIrrelevante"/> (2026-08-30, irrelevant-markering) — default <c>false</c>:
    /// rettskilder <see cref="RettskildeEntitet.ErIrrelevant"/>-merket ekskluderes stille fra
    /// standardvisningen (RettskilderListe.tsx), samme "ikke skjul stille, gi et eksplisitt valg"-
    /// prinsipp som <c>visIkkeImportert</c> der bruker for Lovdata-importstatus — eksplisitt
    /// <c>true</c> tar dem med igjen.
    /// </summary>
    public Task<List<RettskildeEntitet>> AlleRettskilderAsync(Guid? virksomhetId = null, bool inkluderIrrelevante = false) =>
        db.Rettskilder
            .Where(r => r.Importrolle == "primaer" && r.Entitetsstatus == "gjeldende" && r.Status != UtkastStatus)
            .Where(r => virksomhetId == null || r.VirksomhetId == virksomhetId)
            .Where(r => inkluderIrrelevante || !r.ErIrrelevant)
            .ToListAsync();

    public Task<RettskildeEntitet?> FinnAsync(Guid id) =>
        db.Rettskilder.FirstOrDefaultAsync(r => r.Id == id && r.Status != UtkastStatus);

    /// <summary>
    /// [Ny, departement-virksomhet-lenke, 2026-08-30] Løser en rå <see cref="RettskildeEntitet.AnsvarligDepartement"/>-
    /// streng (Lovdatas eget "ministry"-metadatafelt) til en EKTE <see cref="Virksomhet"/>-rad.
    /// [Flyttet, tekst-tagg-departement-eierskap, 2026-08-31] Selve oppslagslogikken bor nå i
    /// <see cref="VirksomhetOppslagTjeneste"/> (i <c>RegelIde.Data</c>) — <see cref="NavnekandidatOppdagelseTjeneste"/>
    /// og <see cref="TekstTaggTjeneste"/> trenger nøyaktig samme oppslag for å avgjøre hvilken
    /// virksomhet en departement-eid tekst-tagg skal tilhøre, men <c>RegelIde.Data</c> kan ikke
    /// referere <c>RegelIde.Api</c> (prosjektreferansen går kun én vei) — flyttet dit i stedet for
    /// duplisert, denne metoden delegerer uendret videre. Se <see cref="VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnAsync"/>
    /// for selve dokumentasjonen (case-insensitivitet, «ingen gjettet fallback» osv.).
    /// </summary>
    public Task<Guid?> FinnVirksomhetIdForNavnAsync(string navn) => virksomhetOppslag.FinnVirksomhetIdForNavnAsync(navn);

    /// <summary>
    /// Motsatt retning av <see cref="FinnVirksomhetIdForNavnAsync"/> — alle GJELDENDE rettskilder der
    /// <see cref="RettskildeEntitet.AnsvarligDepartement"/> eksakt (case-insensitivt) matcher DENNE
    /// virksomhetens eget navn. Brukt av "Ansvarlig for"-seksjonen på VirksomhetDetalj.tsx. Kun
    /// <c>Entitetsstatus == "gjeldende"</c> filtrert (ikke <c>Importrolle</c>/<c>Status</c> i tillegg) —
    /// referanse-stubber har uansett aldri <see cref="RettskildeEntitet.AnsvarligDepartement"/> satt
    /// (opprettes med <c>Tittel = dokumentEli</c>, ingen metadata), så de faller bort av seg selv.
    /// Returnerer tom liste (ikke feil) for en virksomhet uten navnetreff — samme "ingen gjettet
    /// fallback"-prinsipp som <see cref="FinnVirksomhetIdForNavnAsync"/>.
    /// </summary>
    public async Task<List<RettskildeEntitet>> RettskilderAnsvarligForAsync(Guid virksomhetId)
    {
        var navn = await db.Virksomheter.Where(v => v.Id == virksomhetId).Select(v => v.Navn).FirstOrDefaultAsync();
        if (navn is null) return [];

        var navnLower = navn.ToLower();
        return await db.Rettskilder
            .Where(r => r.Entitetsstatus == "gjeldende" && r.AnsvarligDepartement != null && r.AnsvarligDepartement.ToLower() == navnLower)
            .ToListAsync();
    }

    // Filter på Entitetsstatus="gjeldende" lagt til 2026-07-26 (node-nivå versjonering, håndbok/rundskriv,
    // docs/08-byggesteg1-teknisk-design.md §2.1) — virkningsløst for Lov/Forskrift-noder (alltid
    // "gjeldende"), men nødvendig for håndbok-noder: en redigert kommentarseksjon har flere rader med
    // samme eid (gammel="erstattet", ny="gjeldende") som ellers ville dukket opp som duplikater i treet.
    public Task<List<RettskildeNodeEntitet>> NoderForAsync(Guid rettskildeId) =>
        db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende")
            .Include(n => n.HandbokMetadata)
            .OrderBy(n => n.Sorteringsrekkefolge)
            .ToListAsync();

    public Task<RettskildeNodeEntitet?> FinnNodeAsync(Guid rettskildeId, string eid) =>
        db.RettskildeNoder
            .Include(n => n.HandbokMetadata)
            .FirstOrDefaultAsync(n => n.RettskildeId == rettskildeId && n.Eid == eid && n.Entitetsstatus == "gjeldende");

    public async Task<List<RettskildeReferanseEntitet>> ReferanserForAsync(Guid rettskildeId)
    {
        var nodeIder = await db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende")
            .Select(n => n.Id).ToListAsync();
        return await db.RettskildeReferanser.Where(r => nodeIder.Contains(r.FraNodeId)).ToListAsync();
    }

    /// <summary>
    /// Hjemmel-referansene FRA denne rettskilden (typisk en forskrift) — header-metadatafeltet
    /// Hjemmel (2026-08-30, se RettskildeHjemmelEntitet-kommentaren). DOKUMENTNIVÅ, derfor intet
    /// node-oppslag her (til forskjell fra <see cref="ReferanserForAsync"/>). Tom liste for enhver
    /// rettskilde uten feltet (bekreftet KUN på forskrifter i fixture-korpuset, ikke en feil for andre
    /// doctyper), i kildens egen rekkefølge.
    /// </summary>
    public Task<List<RettskildeHjemmelEntitet>> HjemlerForAsync(Guid rettskildeId) =>
        db.RettskildeHjemler
            .Where(h => h.RettskildeId == rettskildeId)
            .OrderBy(h => h.Sorteringsrekkefolge)
            .ToListAsync();

    /// <summary>
    /// Motsatt retning av <see cref="HjemlerForAsync"/> — hvilke ANDRE rettskilder (typisk forskrifter)
    /// som er hjemlet i DENNE rettskilden (typisk en lov). Samme "reverse lookup"-mønster som
    /// <see cref="ReferertAvAndreDokumenterAsync"/>, bare for Hjemmel i stedet for løpetekst-referanser.
    /// </summary>
    public Task<List<RettskildeHjemletForDto>> HjemletForAsync(Guid rettskildeId) =>
        db.RettskildeHjemler
            .Where(h => h.HjemmelRettskildeId == rettskildeId)
            .Join(db.Rettskilder, h => h.RettskildeId, r => r.Id, (h, r) => new { h, r })
            .OrderBy(x => x.r.Tittel).ThenBy(x => x.h.Sorteringsrekkefolge)
            .Select(x => new RettskildeHjemletForDto(x.r.Id, x.r.Tittel, x.h.HjemmelEid))
            .ToListAsync();

    /// <summary>
    /// Endring-referansene FRA denne rettskilden (rettskildedetalj-fikser, 2026-09-02, punkt 5) — header-
    /// metadatafeltet Endrer (<see cref="RettskildeEndringEntitet"/>): hvilke(t) andre dokument(er) DENNE
    /// rettskilden endrer. Samme "ingen join, DOKUMENTNIVÅ"-mønster som <see cref="HjemlerForAsync"/>, i
    /// kildens egen rekkefølge. Tom liste for enhver rettskilde uten feltet.
    /// </summary>
    public Task<List<RettskildeEndringEntitet>> EndringerForAsync(Guid rettskildeId) =>
        db.RettskildeEndringer
            .Where(e => e.RettskildeId == rettskildeId)
            .OrderBy(e => e.Sorteringsrekkefolge)
            .ToListAsync();

    /// <summary>
    /// Oppdaterer redigerbar metadata på en allerede importert rettskilde — opprinnelig AK-3.3.6
    /// "bekreft/rediger metadata" (Kortnavn/Utgiver, i importbekreftelsen `Importer.tsx`), utvidet
    /// 2026-08-13 (avklaringsrunde) med de håndbok-metadatafeltene som allerede fantes på entiteten
    /// men manglet en skrivevei: InterntDokNr/Revisjonsnr/VedtattAv/Vedtaksdato/GyldigTil/KonsolidertDato.
    /// <see cref="RettskildeEntitet.Eli"/> er BEVISST utenfor denne signaturen — permanent
    /// skrivebeskyttet (§3.3), aldri en del av redigeringen. Resten av metadataen (tittel, status osv.)
    /// er tolket direkte fra kilden og skal fortsatt ikke friredigeres her.
    /// Returnerer null hvis rettskilden ikke finnes (kalleren mapper til 404).
    /// </summary>
    public async Task<RettskildeEntitet?> OppdaterMetadataAsync(
        Guid id, string? kortnavn, string? utgiver, string? interntDokNr, string? revisjonsnr, string? vedtattAv,
        DateOnly? vedtaksdato, DateOnly? gyldigTil, DateOnly? konsolidertDato, string endretAv)
    {
        var rettskilde = await db.Rettskilder.FirstOrDefaultAsync(r => r.Id == id);
        if (rettskilde is null) return null;

        rettskilde.Kortnavn = kortnavn;
        rettskilde.Utgiver = utgiver;
        rettskilde.InterntDokNr = interntDokNr;
        rettskilde.Revisjonsnr = revisjonsnr;
        rettskilde.VedtattAv = vedtattAv;
        rettskilde.Vedtaksdato = vedtaksdato;
        rettskilde.GyldigTil = gyldigTil;
        rettskilde.KonsolidertDato = konsolidertDato;
        rettskilde.SistEndretAv = endretAv;
        rettskilde.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        rettskilde.Versjon++; // basemetadata §0: appens ansvar å øke ved faktisk endring
        await db.SaveChangesAsync();
        return rettskilde;
    }

    /// <summary>
    /// Setter/fjerner header-nivå irrelevant-markeringen (2026-08-30) — se
    /// <see cref="RettskildeEntitet.ErIrrelevant"/>. Setter BEGGE feltene samtidig, alltid (ikke bare
    /// flagget) — <paramref name="irrelevantKommentar"/> lagres uendret selv om <paramref name="erIrrelevant"/>
    /// er <c>false</c> (fjernes markeringen igjen, slettes IKKE kommentaren automatisk, se entitetens
    /// klassekommentar). Returnerer null hvis rettskilden ikke finnes (kalleren mapper til 404).
    /// </summary>
    public async Task<RettskildeEntitet?> OppdaterIrrelevantAsync(
        Guid id, bool erIrrelevant, string? irrelevantKommentar, string endretAv)
    {
        var rettskilde = await db.Rettskilder.FirstOrDefaultAsync(r => r.Id == id);
        if (rettskilde is null) return null;

        rettskilde.ErIrrelevant = erIrrelevant;
        rettskilde.IrrelevantKommentar = irrelevantKommentar;
        rettskilde.SistEndretAv = endretAv;
        rettskilde.SistEndretTidspunkt = DateTimeOffset.UtcNow;
        rettskilde.Versjon++;
        await db.SaveChangesAsync();
        return rettskilde;
    }

    /// <summary>
    /// Byggesteg 4 (2026-07-30) — motsatt retning av <see cref="TjenesteRegelverksreferanseEntitet"/>:
    /// hvilke tjenester som faktisk refererer denne rettskilden. Brukt til å vise "Brukt i tjenester"
    /// på rettskilde-siden, slik at koblingen kan navigeres begge veier, ikke bare fra Tjeneste-siden.
    ///
    /// Bugfiks 2026-09-02 (levende gjennomgang): <c>Distinct()</c> lagt til. Én tjeneste kan lovlig
    /// koble SAMME (rettskilde, eId) flere ganger — én gang på den flate listen (<c>Felt IS NULL</c>)
    /// og i tillegg på ett eller flere enkeltfelt (<c>Felt IS NOT NULL</c>), se de to partial-unike
    /// indeksene i <c>RegelIdeDbContext.cs</c> og kommentaren i
    /// <see cref="TjenesteregisterTjeneste.KobleRegelverksreferanseAsync"/>. <see cref="TjenesteReferanseDto"/>
    /// bærer bevisst ikke Felt (denne visningen svarer kun "bruker tjenesten X denne eId-en?", ikke
    /// hvilket felt) — uten Distinct ga det React-nøkkelkollisjon i KontekstPanel (samme
    /// tjeneste+eId flere ganger i "Brukt i tjenester") så snart én tjeneste hadde både en flat- og
    /// en feltnivå-referanse til samme paragraf. Ikke en datafeil (ingen duplikate rader i tabellen —
    /// radene er forskjellige på Felt), men en projeksjon som slapp gjennom en reell mangfoldighet
    /// denne visningen ikke bryr seg om.
    /// </summary>
    public Task<List<TjenesteReferanseDto>> ReferertAvTjenesterAsync(Guid rettskildeId) =>
        db.TjenesteRegelverksreferanser
            .Where(r => r.TilRettskildeId == rettskildeId)
            .Join(db.Tjenester, r => r.TjenesteId, t => t.Id, (r, t) => new TjenesteReferanseDto(t.Id, t.Tittel, r.TilEid))
            .Distinct()
            .ToListAsync();

    /// <summary>
    /// Punkt 6/9 (avklaringsrunde 2026-08-13) — sideordnet <see cref="ReferertAvTjenesterAsync"/>, men
    /// for referanser som ORIGINERER i et ANNET dokuments nodetre (håndbok/rundskriv), ikke fra en
    /// Tjeneste. Joiner <see cref="RettskildeReferanseEntitet.FraNodeId"/> → <see cref="RettskildeNodeEntitet"/>
    /// for å finne EIENDE dokument (dens <see cref="RettskildeNodeEntitet.RettskildeId"/>) — dette er
    /// nøyaktig den koblingen som før kun fantes fra tjeneste-siden: "Skjenkebevilling – testrunde 3"
    /// har 9 reelle håndbok-originerte referanser til forvaltningsloven/serveringsloven/alkoholloven i
    /// databasen, usynlige den andre veien før dette endepunktet fantes.
    ///
    /// Bugfiks 2026-08-13 (levende gjennomgang): filtrerer nå på <c>Opprinnelse == "manuell"</c> — det
    /// ER akkurat skillet mellom "en jurist koblet dette fra en håndbok-seksjon" (manuell,
    /// <see cref="HandbokForfatterTjeneste.KobleLovreferanseAsync"/>) og "dette er lovens/forskriftens
    /// EGEN interne struktur, funnet ved import" (import, <c>LovdataHtmlParser</c>/
    /// <c>RettskildeImportTjeneste</c>). Uten dette filteret ble en rettskildes egne interne
    /// kryssreferanser (§10-1 → §10-6 osv., skrevet med <c>Opprinnelse="import"</c> ved import) talt
    /// som om et ANNET dokument refererte den — de var aldri det, se <see cref="DokumentReferanseDto"/>s
    /// klassekommentar ("... i en ANNEN RettskildeEntitet ..."). Den eksplisitte
    /// <c>d.Id != rettskildeId</c>-sjekken under er et forsvarslag i tillegg (Opprinnelse-filteret over
    /// eliminerer sannsynligvis alle faktiske forekomster alene) — en selvreferanse er PER DEFINISJON
    /// ikke "et annet dokument", uansett hvilken Opprinnelse den skulle vært skrevet med.
    /// </summary>
    public Task<List<DokumentReferanseDto>> ReferertAvAndreDokumenterAsync(Guid rettskildeId) =>
        db.RettskildeReferanser
            .Where(r => r.TilRettskildeId == rettskildeId && r.Opprinnelse == "manuell")
            .Join(db.RettskildeNoder, r => r.FraNodeId, n => n.Id, (r, n) => new { r, n })
            .Join(db.Rettskilder, rn => rn.n.RettskildeId, d => d.Id, (rn, d) => new { rn.r, rn.n, d })
            .Where(x => x.d.Id != rettskildeId)
            .Select(x => new DokumentReferanseDto(x.d.Id, x.d.Tittel, x.n.Eid, x.n.Overskrift, x.r.TilEid))
            .ToListAsync();

    /// <summary>
    /// Punkt 8 — §3.4s multi-sti-egenskap for en Brukerveiledning (kun ikke-tom for den doctypen, se
    /// <see cref="RettskildeEntitet.Stier"/>). Tom liste for enhver annen rettskilde, ikke en feil.
    /// </summary>
    public Task<List<NettsideStiEntitet>> StierForAsync(Guid rettskildeId) =>
        db.NettsideStier.Where(s => s.RettskildeId == rettskildeId).ToListAsync();

    /// <summary>
    /// Punkt 8/9 — de utgående <see cref="NettsideLenkeEntitet"/>-radene for en Brukerveilednings
    /// side-node, med oppløsningsstatus. Egen liten join siden <see cref="NettsideLenkeEntitet"/>
    /// bevisst IKKE ble konvergert inn i <see cref="RettskildeReferanseEntitet"/> (se Entiteter.cs-
    /// kommentaren) — denne metoden er derfor det som lar RettskildeDetalj.tsx vise dem i SAMME
    /// "Referanser"-visning som alle andre doctyper, i stedet for en nettside-spesifikk duplikat-UI
    /// (punkt 10/9s krav, løst her på API-siden).
    /// </summary>
    public async Task<List<NettsideLenkeMedMalDto>> NettsideLenkerForAsync(Guid rettskildeId)
    {
        var nodeIder = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).Select(n => n.Id).ToListAsync();
        var lenker = await db.NettsideLenker.Where(l => nodeIder.Contains(l.FraNodeId)).ToListAsync();

        var malIder = lenker.Where(l => l.TilRettskildeId is not null).Select(l => l.TilRettskildeId!.Value).Distinct().ToList();
        var mal = await db.Rettskilder.Where(r => malIder.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

        return lenker
            .Select(l => NettsideLenkeMedMalDto.FraEntitet(l, l.TilRettskildeId is not null ? mal.GetValueOrDefault(l.TilRettskildeId.Value) : null))
            .ToList();
    }
}
