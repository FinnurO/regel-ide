using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Diagnostikk fra <see cref="HandbokImportTjeneste.ImporterAsync"/> — antall noder faktisk skrevet,
/// hvor mange <c>kryssrefererer</c>-referanser som ble koblet til en reell node, og hvor mange
/// <c>hjemlet_i</c>-lovnavn som IKKE lot seg koble til nøyaktig én importert Lov/Forskrift (§ "no
/// gjettet fallback" — se <see cref="HandbokImportTjeneste"/>s klassekommentar).
/// </summary>
public sealed record HandbokImportResultat(
    Guid RettskildeId, int AntallNoder, int AntallKryssreferanserKoblet, int AntallHjemletILovnavnUlost,
    int AntallNoderMedUlostForelder);

/// <summary>
/// Persisterer et <see cref="HandbokParseResultat"/> (fra <see cref="HandbokTekstParser.Parse"/>) som en
/// ekte <see cref="RettskildeEntitet"/> + <see cref="RettskildeNodeEntitet"/>-tre. Sideordnet
/// <see cref="HandbokForfatterTjeneste"/> (som forfatter en håndbok NODE FOR NODE via API-kall), men for
/// et FERDIG PARSET dokument som skal skrives i én batch — nøyaktig samme "parser er DB-fri, en egen
/// tjeneste gjør selve DB-skrivingen" todeling som <see cref="RettskildeImportTjeneste"/> har for
/// Lovdata-HTML og <see cref="NettsideGrafKobler"/> har for nettsider.
///
/// <para>
/// **Bevisst EGEN, PARALLELL skrive-vei — ikke et kall til <see cref="HandbokForfatterTjeneste"/>s
/// metoder.** Tre grunner: (1) <see cref="HandbokForfatterTjeneste.OpprettBladNodeAsync"/> tillater kun
/// <c>NodeType</c> "ledd"/"punkt", mens <see cref="HandbokTekstParser"/> også produserer "kapittel"
/// (med egen tekst, se <see cref="HandbokNode.Tekst"/>s doc-kommentar) og "avsnitt"
/// (overskrifts-fallbacken) — ingen DB-CHECK-constraint hindrer disse verdiene (verifisert mot
/// RegelIdeDbContext), men den EKSISTERENDE C#-valideringen i HandbokForfatterTjeneste ville avvist
/// dem. (2) <c>OpprettKapittelNodeAsync</c>/<c>OpprettBladNodeAsync</c> regner <c>Eid</c> på nytt via sin
/// egen <c>LagEid</c>-konvensjon ("kap-{nummer}") — vi vil bevare parserens EGEN <c>HandbokNode.Eid</c>
/// uendret ("kap4", "kap4/pkt4.1"), som testene i <c>HandbokTekstParserTests</c> allerede verifiserer
/// eksplisitt (f.eks. "punkt 4.7 løses mot Eid"). (3) De regner <c>TekstHash</c> på nytt —
/// oppgaven ber oss gjenbruke parserens egen <see cref="HandbokNode.TekstHash"/> uendret.
/// </para>
///
/// <para>
/// **To-pass nodetre-bygging**: parseren emitterer i dokumentets egen lesevolg, og foreldre KOMMER i
/// praksis før barn i alle observerte fixtures (verifisert mot <c>HandbokTekstParserTests</c> — se
/// <c>Segmenterer_alle_ti_kapitler_i_dokumentrekkefolge</c>s "stigende sortering"-assert). Vi stoler
/// likevel IKKE på denne rekkefølgen: alle node-GUID-er genereres FØRST (ett dictionary <c>Eid → Guid</c>
/// for HELE resultatet), og hver <see cref="RettskildeNodeEntitet.ParentNodeId"/> slås opp i dette
/// dictionary-et uavhengig av hvilken rekkefølge selve entitetene deretter legges til <c>DbContext</c>-
/// en i — samme robusthet som om ordenen IKKE var garantert.
/// </para>
///
/// <para>
/// **Importrolle="primaer", MED en minimal AKN-plassholder — IKKE "referanse", et EKTE FUNN underveis.**
/// <c>NettsideDokumentgrafTests.Bundlingssiden_kobler_helt_frem…</c> (forrige runde) seedet denne
/// nøyaktige håndboken med <c>Importrolle="referanse"</c>/<c>AknXml=null</c>, kommentert som "hva et
/// fremtidig håndbok-import-endepunkt ville skrevet". Å bygge nettopp DET endepunktet her avdekket at
/// forrige rundes valg var FEIL, ikke bare en stilforskjell: <c>RettskildeRepository.AlleRettskilderAsync</c>
/// (den ENESTE kilden til rettskilder-LISTEN i UI-et) filtrerer eksplisitt på
/// <c>Importrolle == "primaer"</c> — <c>"referanse"</c> betyr OVERALT ELLERS i kodebasen en automatisk
/// opprettet SITAT-STUBB uten eget innhold (<see cref="RettskildeImportTjeneste.FinnEllerOpprettReferanseStubAsync"/>),
/// aldri "ekte innhold, bare ikke AKN-forfattet". En håndbok importert her HAR fullt nodetre og løpetekst
/// — nøyaktig den formen <c>"primaer"</c> beskriver — og ville vært usynlig i rettskilder-listen (kun
/// nåbar direkte på GUID, f.eks. via en løst nettside-lenke) med <c>"referanse"</c>. Bekreftet i praksis
/// under browser-verifisering av denne rundens arbeid, se sluttrapporten. <c>ck_rettskilder_akn_xml</c>
/// (<c>importrolle = 'referanse' OR akn_xml IS NOT NULL</c>) krever da en AknXml — <see cref="MinimalAknPlassholder"/>
/// under er en EGEN, minimal kopi av <see cref="HandbokForfatterTjeneste.MinimalAknPlassholder"/> (den er
/// <c>private</c> i sin klasse, ikke delt) med samme v1-forenkling: statisk plassholder, ikke en ekte
/// AKN-serialisering av nodetreet (§9.5s fulle rundtur er utenfor scope denne runden, se sluttrapporten).
/// </para>
///
/// <para>
/// **Idempotent på (VirksomhetId, Tittel, Kildetype)** — samme guard-stil som <c>TestkommuneInnholdSeed</c>
/// bruker FØR den kaller inn, men gjort til en garanti INNI selve tjenesten også: et gjentatt
/// <see cref="ImporterAsync"/>-kall for samme tittel/kildetype/virksomhet er en no-op (returnerer den
/// eksisterende rettskildens Id), ingen dupliserte rader.
/// </para>
///
/// <para>
/// **EKTE FUNN under bygging av denne tjenesten, IKKE fikset i parseren (bevisst utenfor scope her,
/// se sluttrapporten)**: <see cref="HandbokTekstParser"/>s <c>PunktMønster</c> (<c>^N(.NN){1,2}$</c>,
/// hele linjen) kolliderer med PDF-tekstlagets linjebrytning av klokkeslett i Bergens FORSKRIFT-fixture
/// — "… salgstiden til kl." ender linjen, og "18.00." står deretter ALENE på neste linje (ren
/// side-/linjebrytningstilfeldighet, samme klasse problem som den allerede dokumenterte "Side N av
/// M"-støyen, men IKKE fanget av <c>FiltrerSidebrytningsstoy</c>). Parseren tolker "18.00." som et
/// GYLDIG 2-segments punktnummer og åpner en ny node <c>"kap18/pkt18.00"</c> — hvis overordnede
/// "kap18" ALDRI finnes (ingen Kapittel 18 i dokumentet), OG som i tillegg feilaktig avslutter/kapper
/// den ekte kap1-teksten midt i en setning. Siden dette er et REELT, ikke-gjettet funn i ekte data
/// (klokkeslett-på-N.NN-form er strukturelt uunngåelig i alkohol-skjenketid-dokumenter), men en
/// regex-fiks i selve parseren er utenfor denne rundens oppgave (og risikerer regresjon i parserens
/// 60+ allerede grønne, låste tester uten en dedikert fixture-drevet runde) — denne tjenesten gjør seg
/// i stedet ROBUST mot funnet: en node hvis <c>ParentEid</c> ikke finnes i det ferdige nodetreet får
/// <c>ParentNodeId = null</c> (importeres som en rot-node i EGET tre, IKKE Guid.Empty/krasj, og IKKE en
/// gjettet erstatning) — se <see cref="HandbokImportResultat.AntallNoderMedUlostForelder"/> for
/// diagnostikk og <c>HandbokImportTjenesteTests</c> for regresjonsbeviset. En fremtidig runde bør rette
/// dette i <c>HandbokTekstParser</c> selv (f.eks. kreve en tittel/tekst på samme linje for et
/// 2-segments punktnummer, eller filtrere "N.NN" umiddelbart etter "kl."-forekomster).
/// </para>
///
/// <para>
/// **HjemletI — bevisst begrenset, ALDRI gjettet.** Vi kan IKKE deterministisk utlede en paragraf-eId
/// fra <see cref="HandbokReferanse.EksternParagraf"/> (f.eks. "§1-7 d") uten å gjette formatet en
/// importert lovs egne node-eId-er faktisk har — det ville vært nøyaktig den gjettingen §0.1 forbyr.
/// Vi gjør derfor KUN det trygge, dokumentnivå-oppslaget oppgaven ba om: finnes det EKSAKT én allerede
/// importert Lov/Forskrift-rettskilde hvis Tittel inneholder <see cref="HandbokReferanse.EksternLovnavn"/>
/// (case-insensitive)? Er det tilfellet, kobles håndboken til DEN rettskilden i sin HELHET via
/// <see cref="HandbokRettskildeomfangEntitet"/> (§ "Håndbok-nivå rettskildeomfang" — nøyaktig designet
/// for "denne håndboken omhandler denne rettskilden, uten paragraf-presisjon", i motsetning til
/// <see cref="RettskildeReferanseEntitet"/>, som ville krevd den upresise/gjettede paragraf-eId-en).
/// Null eller flere enn ett treff ⇒ UGJORT, ikke gjettet — se <see cref="HandbokImportResultat.AntallHjemletILovnavnUlost"/>.
/// </para>
/// </summary>
public sealed class HandbokImportTjeneste(RegelIdeDbContext db)
{
    private const string SystemBruker = "system-import";

    public async Task<HandbokImportResultat> ImporterAsync(
        HandbokParseResultat resultat,
        string tittel,
        Guid virksomhetId,
        string kildetype,
        string doctype,
        string? opprettetAv = null,
        string? url = null,
        string? interntDokNr = null,
        string? revisjonsnr = null,
        string? vedtattAv = null,
        DateOnly? vedtaksdato = null,
        DateOnly? gyldigTil = null,
        string? normativVirkning = null,
        DateTimeOffset? hentet = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tittel))
        {
            throw new ArgumentException("Tittel kan ikke være tom. Ingen gjettet fallback.");
        }

        var attribuertTil = opprettetAv ?? SystemBruker;

        // Idempotens (samme "importer aldri samme gjeldende rettskilde to ganger"-prinsipp som
        // RettskildeImportTjeneste bruker på Eli — håndbøker har ingen Eli, så Tittel+Kildetype+
        // VirksomhetId er den nærmeste naturlige nøkkelen).
        var eksisterende = await db.Rettskilder.FirstOrDefaultAsync(
            r => r.VirksomhetId == virksomhetId && r.Tittel == tittel && r.Kildetype == kildetype && r.Entitetsstatus == "gjeldende", ct);
        if (eksisterende is not null)
        {
            var antallEksisterendeNoder = await db.RettskildeNoder.CountAsync(n => n.RettskildeId == eksisterende.Id, ct);
            return new HandbokImportResultat(eksisterende.Id, antallEksisterendeNoder, 0, 0, 0);
        }

        var rettskilde = new RettskildeEntitet
        {
            Id = Guid.NewGuid(),
            VirksomhetId = virksomhetId,
            Doctype = doctype,
            Kildetype = kildetype,
            Importrolle = "primaer", // se klassekommentaren — EKTE innhold, ikke en sitat-stubb; må vises i rettskilder-listen.
            Tittel = tittel,
            AknXml = MinimalAknPlassholder(tittel, kildetype),
            Status = "Gjeldende",
            Url = url,
            InterntDokNr = interntDokNr,
            Revisjonsnr = revisjonsnr,
            VedtattAv = vedtattAv,
            Vedtaksdato = vedtaksdato,
            GyldigTil = gyldigTil,
            NormativVirkning = normativVirkning,
            Hentet = hentet,
            OpprettetAv = attribuertTil,
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Rettskilder.Add(rettskilde);
        db.Proveniens.Add(ProveniensHjelper.NyRad("rettskilde", rettskilde.Id, virksomhetId, "opprettet", attribuertTil));

        // ---------- To-pass nodetre — se klassekommentaren ----------
        var nodeIdVedEid = resultat.Noder.ToDictionary(n => n.Eid, _ => Guid.NewGuid(), StringComparer.Ordinal);
        var antallUlostForelder = 0;

        foreach (var n in resultat.Noder)
        {
            // TryGetValue, IKKE GetValueOrDefault: en manglende ParentEid skal bli null (rot-node),
            // ALDRI Guid.Empty (som ville brutt FK-constrainten mot rettskilde_noder — verifisert i
            // praksis, se klassekommentaren om det ekte PunktMønster-funnet i forskrift-fixturen).
            Guid? parentNodeId = null;
            if (n.ParentEid is not null)
            {
                if (nodeIdVedEid.TryGetValue(n.ParentEid, out var funnet)) parentNodeId = funnet;
                else antallUlostForelder++;
            }

            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = nodeIdVedEid[n.Eid],
                RettskildeId = rettskilde.Id,
                Eid = n.Eid,
                Kildesystem = "regel-ide",
                KildeId = n.Nummer ?? n.Eid,
                OffisiellEli = null,
                ParentNodeId = parentNodeId,
                NodeType = n.NodeType,
                Nummer = n.Nummer,
                Overskrift = n.Overskrift,
                Tekst = n.Tekst, // uendret — IKKE kjørt gjennom KommentarTekstSanering, se klassekommentaren.
                TekstHash = n.TekstHash, // gjenbrukt fra parseren, ikke regnet på nytt.
                Sorteringsrekkefolge = n.SorteringsRekkefolge,
            });
        }

        await db.SaveChangesAsync(ct);

        // ---------- Kryssrefererer → RettskildeReferanseEntitet (etter at hele treet er lagret) ----------
        var antallKryssKoblet = 0;
        foreach (var r in resultat.Referanser.Where(r => r.Type == HandbokReferansetype.Kryssrefererer))
        {
            if (!nodeIdVedEid.TryGetValue(r.FraNodeEid, out var fraNodeId)) continue;
            if (r.TilEid is null || !nodeIdVedEid.ContainsKey(r.TilEid)) continue; // uløst — ingen gjettet fallback, dropp stille

            db.RettskildeReferanser.Add(new RettskildeReferanseEntitet
            {
                Id = Guid.NewGuid(),
                FraNodeId = fraNodeId,
                TilRettskildeId = rettskilde.Id, // intern referanse — håndbokens egen rettskilde-id, se HandbokTekstParser-kommentaren.
                TilEid = r.TilEid,
                Opprinnelse = "import",
            });
            antallKryssKoblet++;
        }

        // ---------- HjemletI → HandbokRettskildeomfangEntitet, KUN ved eksakt ett Tittel-treff ----------
        var antallHjemletUlost = 0;
        var eksterneLovnavn = resultat.Referanser
            .Where(r => r.Type == HandbokReferansetype.HjemletI && r.EksternLovnavn is not null)
            .Select(r => r.EksternLovnavn!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (eksterneLovnavn.Count > 0)
        {
            var lovOgForskrift = await db.Rettskilder
                .Where(r => (r.Kildetype == "Lov" || r.Kildetype == "Forskrift") && r.Entitetsstatus == "gjeldende")
                .Select(r => new { r.Id, r.Tittel })
                .ToListAsync(ct);

            foreach (var lovnavn in eksterneLovnavn)
            {
                var treff = lovOgForskrift.Where(r => r.Tittel.Contains(lovnavn, StringComparison.OrdinalIgnoreCase)).ToList();
                if (treff.Count != 1)
                {
                    // Null eller flertydig — ALDRI gjett. Diagnostikk til kalleren/testene, ikke en exception:
                    // en uløst hjemmel skal ikke stoppe resten av importen (§0.1).
                    antallHjemletUlost++;
                    continue;
                }

                var malId = treff[0].Id;
                var finnesAllerede = await db.HandbokRettskildeomfang
                    .AnyAsync(o => o.HandbokId == rettskilde.Id && o.TilRettskildeId == malId, ct);
                if (finnesAllerede) continue;

                db.HandbokRettskildeomfang.Add(new HandbokRettskildeomfangEntitet
                {
                    Id = Guid.NewGuid(),
                    HandbokId = rettskilde.Id,
                    TilRettskildeId = malId,
                    OpprettetAv = attribuertTil,
                    OpprettetTidspunkt = DateTimeOffset.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return new HandbokImportResultat(rettskilde.Id, resultat.Noder.Count, antallKryssKoblet, antallHjemletUlost, antallUlostForelder);
    }

    /// <summary>
    /// Egen, minimal kopi av <see cref="HandbokForfatterTjeneste.MinimalAknPlassholder"/> (den er
    /// <c>private</c> der, ikke delt) — samme v1-forenkling: tilfredsstiller KUN
    /// <c>ck_rettskilder_akn_xml</c> (non-null for <c>importrolle='primaer'</c>), ingen ekte
    /// AKN-serialisering av det importerte nodetreet. <c>rettskilde_noder</c> er og blir autoritativ
    /// kilde for navigasjon/lesing/tagging, akkurat som i HandbokForfatterTjeneste.
    /// </summary>
    private static string MinimalAknPlassholder(string tittel, string kildetype)
    {
        var tekst = System.Net.WebUtility.HtmlEncode(tittel);
        return $"""
            <akomaNtoso xmlns="http://docs.oasis-open.org/legaldocml/ns/akn/3.0">
              <doc name="{System.Net.WebUtility.HtmlEncode(kildetype.ToLowerInvariant())}">
                <meta>
                  <proprietary source="#regel-ide">
                    <regelIde:kildetype>{System.Net.WebUtility.HtmlEncode(kildetype)}</regelIde:kildetype>
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
