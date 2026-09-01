using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Seed-versjon av det som fasit-runde 4 (2026-07-31, docs/12-fasit-handbok-leveranse.md "Runde 4")
/// opprinnelig gjorde via ekte, live HTTP-kall mot en kjørende instans: 5 nye Vilkår (Habilitet,
/// Formalia, Serveringsbevillingsvilkår, Kunnskapsprøve, Kommunal skjønnsvurdering), 13 nye Tjenester
/// (rundskriv-fasitens § 12 "Relevante tjenester" — in extenso, ikke et utvalg), ekte tekst-tagger som
/// knytter de nye Vilkårene til lovtekst (fvl §8/§11/§17, serveringsloven §3, alkoholloven §1-7a/§1-7c),
/// og 10 VilkarstreKommentarer for innhold fra rundskriv-fasitens §6/§8/§9 som ikke passer som et
/// testbart Vilkår.
///
/// Hvorfor dette nå er en seed og ikke bare en engangs-API-øvelse: <see cref="RundskrivReproduksjonTests"/>
/// (RegelIde.Api.Tests) kjører mot en FRISK, embedded Postgres-instans per testkjøring, seedet kun via
/// klassene som kjøres fra Program.cs — det opprinnelige, manuelt kjørte API-innholdet fra runde 4
/// eksisterte bare i den ene, langvarige utviklings-databasen, aldri i test-databasen. Dekningstesten
/// målte derfor konstant den langt magrere seed-baselinen, uavhengig av hvor mye som faktisk var
/// bygget "for hånd" — se docs/13-backlog.md §1/§4 punkt 1. Denne filen lukker det gapet.
///
/// Kjøres etter <see cref="Byggesteg4VilkarstreSeed"/> (krever rotnoden + Vandelsvilkåret den bygger).
/// Kilder til alt innhold under: docs/kildegrunnlag/skjenkebevilling-rundskriv-fasit.md §3/§4/§5/§6/§7/
/// §8/§9/§12 — ordrett/nær-ordrett gjengitt, ikke oppspinn.
/// </summary>
public static class FasitRunde4Seed
{
    private const string SeedBruker = "Kari Jurist";
    private const string AlkohollovenEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";
    private const string ForvaltningslovenEli = "https://lovdata.no/eli/lov/1967/02/10/nor";
    private const string ServeringslovenEli = "https://lovdata.no/eli/lov/1997/06/13/55/nor";
    private const string RotnodeTittel = "Vedtak om skjenkebevilling";
    private const string MarkorVilkar = "Habilitet"; // global guard — finnes kun hvis denne seedingen allerede har kjørt

    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        if (await db.Vilkar.AnyAsync(v => v.Tittel == MarkorVilkar, ct)) return;

        var testkommunen = await db.Virksomheter.FirstOrDefaultAsync(v => v.Navn == "Testkommunen", ct);
        if (testkommunen is null) return;
        // Skopet på testkommunen.Id, ikke bare Tittel — samme begrunnelse som skopingen i
        // Byggesteg2InnholdSeed/Byggesteg4VilkarstreSeed: ellers kan et uskopet oppslag treffe en
        // "Alminnelig skjenkebevilling"-rad fra en helt annen, uavhengig test i denne delte databasen.
        var tjeneste = await db.Tjenester.FirstOrDefaultAsync(
            t => t.Tittel == "Alminnelig skjenkebevilling" && t.VirksomhetId == testkommunen.Id, ct);
        if (tjeneste?.RotnodeId is null) return; // byggesteg 2/4-seedingen må ha kjørt først

        var rRoot = await db.Regelnoder.FirstOrDefaultAsync(r => r.Id == tjeneste.RotnodeId && r.Tittel == RotnodeTittel, ct);
        var vandelsvilkar = await db.Vilkar.FirstOrDefaultAsync(v => v.Tittel == "Vandelsvilkår", ct);
        if (rRoot is null || vandelsvilkar is null) return;

        var virksomhetId = testkommunen.Id;

        var alkoholloven = await db.Rettskilder.FirstOrDefaultAsync(r => r.Eli == AlkohollovenEli && r.Entitetsstatus == "gjeldende", ct);
        var forvaltningsloven = await db.Rettskilder.FirstOrDefaultAsync(r => r.Eli == ForvaltningslovenEli && r.Entitetsstatus == "gjeldende", ct);
        var serveringsloven = await db.Rettskilder.FirstOrDefaultAsync(r => r.Eli == ServeringslovenEli && r.Entitetsstatus == "gjeldende", ct);

        var vilkarregister = new VilkarregisterTjeneste(db);
        var regelnoderegister = new RegelnoderegisterTjeneste(db);
        var begrepsregister = new BegrepsregisterTjeneste(db);
        var tjenesteregister = new TjenesteregisterTjeneste(db);
        var kommentarregister = new VilkarstreKommentarTjeneste(db);
        var tekstTaggTjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));

        // §8: "kommunens skjønnsutøvelse" — skjønnsgrunnlaget for "Kommunal skjønnsvurdering" under.
        var skjonnsutovelseBegrep = await begrepsregister.OpprettAsync(
            virksomhetId, "kommunens skjønnsutøvelse ved bevilling",
            "Kommunestyrets frie skjønn til å innvilge eller avslå en søknad ut fra en konkret vurdering av " +
            "stedets karakter, beliggenhet, målgruppe, trafikk- og ordensmessige forhold, næringspolitiske " +
            "hensyn og hensynet til lokalmiljøet, jf. alkoholloven § 1-7a.",
            $"{AlkohollovenEli}/§1-7a", gjelderFor: null, kodelisteReferanseId: null, skosUrl: null,
            "handlingsbegrep", SeedBruker, ct);

        var habilitet = await vilkarregister.OpprettAsync(
            virksomhetId, "Habilitet",
            "Saksbehandler skal selv ta stilling til sin habilitet i starten av hver sak, og straks varsle " +
            "nærmeste leder ved tvil om egen upartiskhet, jf. forvaltningsloven § 8.",
            null, "formell", "saksbehandler",
            [new JuridiskGrunnlagInput("forvaltningsloven", $"{ForvaltningslovenEli}/§8")],
            null, "regelbasert", null, null, null, false, null, null, null, false, null, tjeneste.Id, SeedBruker, ct);
        await regelnoderegister.KobleBarnAsync(rRoot.Id, "vilkar", habilitet.Id, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, forvaltningsloven, ForvaltningslovenEli, virksomhetId, "§8", habilitet.Id, ct);

        var formalia = await vilkarregister.OpprettAsync(
            virksomhetId, "Formalia",
            "Søknaden må være korrekt utfylt og nødvendig dokumentasjon foreligge, jf. forvaltningsloven § 17. " +
            "Mangler noe, følger kommunen sin alminnelige veiledningsplikt etter § 11.",
            null, "formell", null,
            [
                new JuridiskGrunnlagInput("forvaltningsloven", $"{ForvaltningslovenEli}/§17"),
                new JuridiskGrunnlagInput("forvaltningsloven", $"{ForvaltningslovenEli}/§11"),
            ],
            null, "regelbasert", null, null, null, false, null, null, null, false, null, tjeneste.Id, SeedBruker, ct);
        await regelnoderegister.KobleBarnAsync(rRoot.Id, "vilkar", formalia.Id, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, forvaltningsloven, ForvaltningslovenEli, virksomhetId, "§17", formalia.Id, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, forvaltningsloven, ForvaltningslovenEli, virksomhetId, "§11", formalia.Id, ct);

        var serveringsbevillingsvilkar = await vilkarregister.OpprettAsync(
            virksomhetId, "Serveringsbevillingsvilkår",
            "Gyldig serveringsbevilling må foreligge, eller søkes samtidig, jf. serveringsloven § 3. " +
            "Foreligger bevillingen allerede, er vilkåret oppfylt uten eget vedtak.",
            null, "formell", null,
            [new JuridiskGrunnlagInput("serveringsloven", $"{ServeringslovenEli}/§3")],
            null, "regelbasert", null, null, null, false, null, null, null, false, null, tjeneste.Id, SeedBruker, ct);
        await regelnoderegister.KobleBarnAsync(rRoot.Id, "vilkar", serveringsbevillingsvilkar.Id, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, serveringsloven, ServeringslovenEli, virksomhetId, "§3", serveringsbevillingsvilkar.Id, ct);

        var kunnskapsprove = await vilkarregister.OpprettAsync(
            virksomhetId, "Kunnskapsprøve",
            "Styrer og stedfortreder må begge ha bestått kunnskapsprøven i alkoholloven, med mindre bevillingen " +
            "gjelder en enkelt anledning eller er ambulerende, jf. alkoholloven § 1-7c.",
            null, "formell", "styrer/stedfortreder",
            [new JuridiskGrunnlagInput("alkoholloven", $"{AlkohollovenEli}/§1-7c")],
            null, "regelbasert", null, null, null, false, null, null, null, false, null, tjeneste.Id, SeedBruker, ct);
        await regelnoderegister.KobleBarnAsync(rRoot.Id, "vilkar", kunnskapsprove.Id, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, alkoholloven, AlkohollovenEli, virksomhetId, "§1-7c", kunnskapsprove.Id, ct);

        var kommunalSkjonnsvurdering = await vilkarregister.OpprettAsync(
            virksomhetId, "Kommunal skjønnsvurdering",
            "Kommunestyret står fritt til å innvilge eller avslå ut fra en konkret vurdering, jf. alkoholloven § 1-7a.",
            null, "materiell", null,
            [new JuridiskGrunnlagInput("alkoholloven", $"{AlkohollovenEli}/§1-7a")],
            null, "skjonnsbasert", null, skjonnsutovelseBegrep.Id,
            [
                new SkjonnsmomentInput("Stedets karakter, beliggenhet og målgruppe", null, null),
                new SkjonnsmomentInput("Trafikk- og ordensmessige forhold", null, null),
                new SkjonnsmomentInput("Næringspolitiske hensyn", null, null),
                new SkjonnsmomentInput("Hensynet til lokalmiljøet for øvrig", null, null),
            ],
            true, "Kommunestyret", null, null, false, null, tjeneste.Id, SeedBruker, ct);
        await regelnoderegister.KobleBarnAsync(rRoot.Id, "vilkar", kommunalSkjonnsvurdering.Id, ct);
        await SeedVilkarTaggAsync(db, tekstTaggTjeneste, alkoholloven, AlkohollovenEli, virksomhetId, "§1-7a", kommunalSkjonnsvurdering.Id, ct);

        // §9 "Vilkår i vedtaket" — faste vilkår/gyldighet/gebyr/avledede vilkår/sakspesifikke opplysninger
        // hører hjemme på rotnoden: de gjelder vedtaket som helhet, ikke ett enkelt Vilkår i treet.
        await kommentarregister.OpprettAsync(virksomhetId, "regelnode", rRoot.Id, "kommentar",
            "<p>Faste vilkår ved innvilget søknad: konsum av utskjenket alkohol må opphøre senest 30 minutter " +
            "etter skjenketidens utløp; meldeplikt ved endring av styrer, stedfortreder eller eiersammensetning; " +
            "innrapportering av omsetning; prikkbelastning ved brudd.</p>", SeedBruker, ct);
        await kommentarregister.OpprettAsync(virksomhetId, "regelnode", rRoot.Id, "kommentar",
            "<p>Gyldighet: bevilling gjelder for 4 år, eller til 30. september i året etter neste kommunevalg.</p>",
            SeedBruker, ct);
        await kommentarregister.OpprettAsync(virksomhetId, "regelnode", rRoot.Id, "kommentar",
            "<p>Har stedet også skjenkebevilling, følger serveringsstedets åpningstid normalt skjenketiden pluss " +
            "30 minutter — et vilkår avledet av en annen bevilling enn den vedtaket selv gjelder.</p>", SeedBruker, ct);
        await kommentarregister.OpprettAsync(virksomhetId, "regelnode", rRoot.Id, "kommentar",
            "<p>Bevillingsgebyr for enkelt anledning/ambulerende bevilling: 25 % av ordinært gebyr ved ≤50 " +
            "deltakere, 50 % ved 51–599, 100 % ved ≥600; egen redusert sats for seniorsentre, frivillige og " +
            "små kulturarrangement under 600 deltakere.</p>", SeedBruker, ct);
        await kommentarregister.OpprettAsync(virksomhetId, "regelnode", rRoot.Id, "praktisk-rad",
            "<p>Sakspesifikke opplysninger (navngitt styrer/stedfortreder, adresse, godkjent personantall inne/ute, " +
            "eiersammensetning) er ikke vilkår i egentlig forstand, men fakta fra søknaden som gjengis i vedtaket " +
            "— de skal stemme med det som faktisk er opplyst, ikke fylles ut skjønnsmessig.</p>", SeedBruker, ct);

        // §6 Vandelsvurdering — avslagsgrunner (sjekkliste) og 10-årsgrensen hører til det eksisterende
        // Vandelsvilkåret fra Byggesteg4VilkarstreSeed, ikke et nytt Vilkår.
        await kommentarregister.OpprettAsync(virksomhetId, "vilkar", vandelsvilkar.Id, "sjekkliste",
            "<ul>" +
            "<li>Manglende innlevering av mva-oppgaver og terminoppgaver for forskuddstrekk og arbeidsgiveravgift</li>" +
            "<li>Manglende innbetaling av merverdiavgift, arbeidsgiveravgift eller forskuddstrekk</li>" +
            "<li>Store restanser på restskatt</li>" +
            "<li>Manglende innlevering av selvangivelser</li>" +
            "<li>Regnskapsovertredelser som gjerne avdekkes gjennom bokettersyn</li>" +
            "<li>Manglende fortløpende registrering av kontantomsetning</li>" +
            "<li>Forsettlig overtredelse av alkoholloven § 1-10 (salg/skjenking uten gyldig bevilling)</li>" +
            "<li>Ordensmessige forhold ved stedet, for eksempel støy, uro og bråk</li>" +
            "<li>Andre brudd på alkoholloven, for eksempel overskjenking og skjenking til mindreårige</li>" +
            "</ul>", SeedBruker, ct);
        await kommentarregister.OpprettAsync(virksomhetId, "vilkar", vandelsvilkar.Id, "hjemmel",
            "<p>Det kan ikke legges vekt på forhold som er eldre enn 10 år for styrer/stedfortreder, jf. " +
            "alkoholloven § 1-7c femte ledd. Serveringslovens tilsvarende vandelsvurdering har en kortere " +
            "grense på 5 år.</p>", SeedBruker, ct);
        await kommentarregister.OpprettAsync(virksomhetId, "vilkar", vandelsvilkar.Id, "praktisk-rad",
            "<p>«Vesentlig innflytelse» er i seg selv en vurdering, ikke bare et spørsmål om formelt eierskap — " +
            "nære relasjoners eierandeler/stemmer (ektefelle, samboer, slektninger i rett opp- eller " +
            "nedstigende linje, søsken) regnes med.</p>", SeedBruker, ct);

        // §8 Kommunens skjønnsvurdering — kommunale tilleggsvilkår og beslutningsheuristikk.
        await kommentarregister.OpprettAsync(virksomhetId, "vilkar", kommunalSkjonnsvurdering.Id, "kommentar",
            "<p>Kommunalt tilleggsvilkår: alle som serverer alkohol skal ha gjennomført e-læringskurset " +
            "«Ansvarlig vertskap».</p>", SeedBruker, ct);
        await kommentarregister.OpprettAsync(virksomhetId, "vilkar", kommunalSkjonnsvurdering.Id, "praktisk-rad",
            "<p>En klar negativ uttalelse fra politiet eller sosialtjenesten, et nådd kommunalt bevillingstak, " +
            "eller et konsept kommunen uttrykkelig har lukket for, bør normalt lede til avslag. Har " +
            "høringsinstansene bare merknader — ikke en klar negativ konklusjon — bør det heller vurderes om " +
            "bevilling kan gis med vilkår som imøtekommer merknaden.</p>", SeedBruker, ct);

        // §12 "Relevante tjenester" — hele listen, ordrett fra kildedokumentet, ikke et utvalg.
        // Skopet på virksomhetId, ikke bare Tittel — flere titler her (f.eks. "Serveringsbevilling",
        // "Alminnelig skjenkebevilling") er generiske nok til at andre, helt uavhengige tester i denne
        // delte databasen oppretter sine egne rader med samme navn under egne virksomheter. Et uskopet
        // guard ville da hoppet over å opprette DENNE virksomhetens rad, og seedingen ville stille
        // no-opet for akkurat det elementet (bekreftet empirisk 2026-08-20, se ServeringsbevillingModell-
        // SeedTests-kommentaren for hele feilsøkingskjeden).
        foreach (var tittel in RelevanteTjenester)
        {
            if (await db.Tjenester.AnyAsync(t => t.Tittel == tittel && t.VirksomhetId == virksomhetId, ct)) continue;
            await tjenesteregister.OpprettAsync(
                virksomhetId, tittel, beskrivelse: $"{tittel} — relatert tjeneste (rundskriv-fasit § 12), " +
                "nevnt i forbindelse med «Alminnelig skjenkebevilling» men foreløpig uten en strukturert kobling " +
                "mellom tjenestene (se docs/13-backlog.md §2.1 Hendelse/Tjenesteavhengighet).",
                kompetentMyndighet: "Testkommunen", output: null, tjenestetype: "Enkeltvedtak", malgruppe: null,
                kanaler: null, kostnad: null, behandlingstid: null, kontaktpunkt: null, konsekvensVedBrudd: null,
                sprak: ["nb"], opprettetAv: SeedBruker, ct);
        }
    }

    private static readonly string[] RelevanteTjenester =
    [
        "Omsetningsoppgave og bevillingsgebyr",
        "Serveringsbevilling",
        "Skjenkebevilling for et arrangement",
        "Utvidelse av skjenkebevilling for en enkelt anledning",
        "Salgsbevilling",
        "Endringer i driften som får betydning for bevillingen",
        "Endring av eiere eller eierandeler",
        "Eierskifte og drift i overgangsperioden på tidligere eiers bevilling",
        "Oppsigelse av bevilling",
        "Etablererprøven",
        "Kunnskapsprøvene",
        "Konsekvenser ved brudd på regelverket",
        "Kontroller av salgs- og skjenkesteder",
    ];

    /// <summary>Samme mønster som <see cref="Byggesteg4VilkarstreSeed"/>s private helper, generalisert til vilkårlig rettskilde/ELI.</summary>
    private static async Task SeedVilkarTaggAsync(
        RegelIdeDbContext db, TekstTaggTjeneste tekstTaggTjeneste, RettskildeEntitet? rettskilde, string eli,
        Guid virksomhetId, string paragrafnummer, Guid vilkarId, CancellationToken ct)
    {
        if (rettskilde is null) return;

        var leddEid = $"{eli}/{paragrafnummer}/ledd-1";
        var node = await db.RettskildeNoder.FirstOrDefaultAsync(
            n => n.RettskildeId == rettskilde.Id && n.Eid == leddEid && n.Tekst != null, ct);
        if (node?.Tekst is null) return;

        var lengde = Math.Min(60, node.Tekst.Length);
        var tagg = await tekstTaggTjeneste.OpprettAsync(
            rettskilde.Id, virksomhetId, SeedBruker, node.Eid, 0, lengde,
            "", node.Tekst[..lengde], node.Tekst[lengde..], "vilkar", ct);
        if (tagg is not null)
        {
            await tekstTaggTjeneste.KobleTilEntitetAsync(tagg.Id, vilkarId, SeedBruker, ct);
        }
    }
}
