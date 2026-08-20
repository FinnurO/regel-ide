using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Testcase-innhold for byggesteg 2 (Tjeneste/Begrep/Kodeliste, docs/06-veikart.md) — tjenesten
/// "Alminnelig skjenkebevilling", begrepene «uklanderlig vandel»/«styrer og stedfortreder»/«skjenketid»,
/// og kodelistene KL-VANDELSOMRADE-ALKOHOLLOV/KL-RETTSKILDEVEKT. Alt under Testkommunen, samme mønster
/// som <see cref="TestkommuneInnholdSeed"/> — idempotent, kjørt fra Program.cs, guardet per innholdsstykke
/// (ikke bare "er databasen tom"), siden dette kjører etter Lov/Forskrift-import og øvrig seeding.
///
/// Forutsetter at alkoholloven (LOV-1989-06-02-27) allerede er importert — no-op (ikke feil) hvis den
/// ikke finnes ennå, samme "ingenting å referere til"-toleranse som resten av oppstartsseedingen.
/// Regelverksreferansene/lovreferansene peker på paragraf-nivå eId-er (<c>{alkohollovenEli}/§X-Y</c>),
/// avledet direkte fra rettskildens ELI — samme konvensjon som <c>LovdataIdentifikatorer.ParagrafEid</c>
/// (docs/08-byggesteg1-teknisk-design.md §1.2), verifisert mot faktisk importert innhold.
/// </summary>
public static class Byggesteg2InnholdSeed
{
    private const string SeedBruker = "Kari Jurist";
    private const string AlkohollovenEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";

    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var testkommunen = await db.Virksomheter.FirstOrDefaultAsync(v => v.Navn == "Testkommunen", ct);
        if (testkommunen is null) return; // Brukere-seedingen (som oppretter Testkommunen) kjører før denne — se Program.cs

        var alkoholloven = await db.Rettskilder.FirstOrDefaultAsync(r => r.Eli == AlkohollovenEli && r.Entitetsstatus == "gjeldende", ct);
        if (alkoholloven is null) return; // alkoholloven er ikke importert ennå — ingenting å referere til

        var tjenesteregister = new TjenesteregisterTjeneste(db);
        var begrepsregister = new BegrepsregisterTjeneste(db);
        var kodelisteregister = new KodelisteregisterTjeneste(db);

        var vandelsomrade = await SeedVandelsomradeKodelisteAsync(db, kodelisteregister, testkommunen.Id, ct);
        await SeedRettskildevektKodelisteAsync(db, kodelisteregister, testkommunen.Id, ct);
        await SeedBegreperAsync(db, begrepsregister, testkommunen.Id, vandelsomrade?.Id, ct);
        await SeedTjenesteAsync(db, tjenesteregister, testkommunen.Id, alkoholloven.Id, ct);
    }

    /// <summary>KL-VANDELSOMRADE-ALKOHOLLOV — de fire vandelsområdene alkoholloven § 1-7b viser til.</summary>
    private static async Task<KodelisteEntitet?> SeedVandelsomradeKodelisteAsync(
        RegelIdeDbContext db, KodelisteregisterTjeneste kodelisteregister, Guid virksomhetId, CancellationToken ct)
    {
        const string kode = "KL-VANDELSOMRADE-ALKOHOLLOV";
        var eksisterende = await db.Kodelister.FirstOrDefaultAsync(k => k.Kode == kode, ct);
        if (eksisterende is not null) return eksisterende;

        var kodeliste = await kodelisteregister.OpprettAsync(
            virksomhetId, kode, "Vandelsområder (alkoholloven § 1-7b)", "juridisk",
            juridiskGrunnlagEid: $"{AlkohollovenEli}/§1-7b", eksternKildeUri: null, eksternKildeVersjon: null,
            SeedBruker, ct);

        await kodelisteregister.LeggTilKodeAsync(kodeliste.Id, "skatt-og-avgift", "Skatte- og avgiftslovgivningen",
            "Bevillingshavers overholdelse av skatte- og avgiftslovgivningen.", null, null, ct);
        await kodelisteregister.LeggTilKodeAsync(kodeliste.Id, "regnskap-og-bokforing", "Regnskaps- og bokføringslovgivningen",
            "Bevillingshavers overholdelse av regnskaps- og bokføringslovgivningen.", null, null, ct);
        await kodelisteregister.LeggTilKodeAsync(kodeliste.Id, "annen-naeringslovgivning", "Annen næringslovgivning",
            "Lovgivning knyttet til virksomhetsutøvelsen i bransjen for øvrig.", null, null, ct);
        await kodelisteregister.LeggTilKodeAsync(kodeliste.Id, "straffelovgivningen", "Straffelovgivningen",
            "Relevante forhold etter straffelovgivningen.", null, null, ct);
        return kodeliste;
    }

    /// <summary>KL-RETTSKILDEVEKT — generell rettskildelære, ikke knyttet til én enkelt lovhjemmel.</summary>
    private static async Task SeedRettskildevektKodelisteAsync(
        RegelIdeDbContext db, KodelisteregisterTjeneste kodelisteregister, Guid virksomhetId, CancellationToken ct)
    {
        const string kode = "KL-RETTSKILDEVEKT";
        if (await db.Kodelister.AnyAsync(k => k.Kode == kode, ct)) return;

        var kodeliste = await kodelisteregister.OpprettAsync(
            virksomhetId, kode, "Rettskildevekt", "juridisk",
            juridiskGrunnlagEid: null, eksternKildeUri: null, eksternKildeVersjon: null, SeedBruker, ct);

        await kodelisteregister.LeggTilKodeAsync(kodeliste.Id, "lovtekst", "Lovtekst", "Selve lov-/forskriftsteksten.", null, null, ct);
        await kodelisteregister.LeggTilKodeAsync(kodeliste.Id, "forarbeider", "Forarbeider", "Proposisjoner, innstillinger mv.", null, null, ct);
        await kodelisteregister.LeggTilKodeAsync(kodeliste.Id, "rettspraksis", "Rettspraksis", "Høyesteretts- og underrettsdommer.", null, null, ct);
        await kodelisteregister.LeggTilKodeAsync(kodeliste.Id, "forvaltningspraksis", "Forvaltningspraksis", "Statsforvalterens og klagenemndas avgjørelser.", null, null, ct);
        await kodelisteregister.LeggTilKodeAsync(kodeliste.Id, "juridisk-teori", "Juridisk teori", "Faglitteratur og kommentarutgaver.", null, null, ct);
    }

    private static async Task SeedBegreperAsync(
        RegelIdeDbContext db, BegrepsregisterTjeneste begrepsregister, Guid virksomhetId, Guid? vandelsomradeId, CancellationToken ct)
    {
        await SeedBegrepAsync(db, begrepsregister, virksomhetId, "uklanderlig vandel",
            "Et krav om at bevillingshaver og personer med vesentlig innflytelse på virksomheten ikke har " +
            "utvist forhold som gir grunn til å anta at bevillingen vil bli misbrukt, jf. alkoholloven § 1-7b.",
            $"{AlkohollovenEli}/§1-7b", vandelsomradeId, "handlingsbegrep", ct);

        await SeedBegrepAsync(db, begrepsregister, virksomhetId, "styrer og stedfortreder",
            "Personene som reelt innehar ansvaret for den daglige driften av salgs- eller skjenkestedet, jf. alkoholloven § 1-7c.",
            $"{AlkohollovenEli}/§1-7c", null, "faktabegrep", ct);

        await SeedBegrepAsync(db, begrepsregister, virksomhetId, "skjenketid",
            "Tidsrommet på døgnet det er tillatt å skjenke alkoholholdig drikk, jf. alkoholloven § 4-4.",
            $"{AlkohollovenEli}/§4-4", null, "faktabegrep", ct);
    }

    private static async Task SeedBegrepAsync(
        RegelIdeDbContext db, BegrepsregisterTjeneste begrepsregister, Guid virksomhetId,
        string term, string definisjon, string lovreferanseEid, Guid? kodelisteReferanseId, string begrepstype, CancellationToken ct)
    {
        // Global guard (samme idempotens-mønster som TestkommuneInnholdSeed) — ikke virksomhets-scopet,
        // siden det i praksis kun finnes én Testkommunen-rad denne kjører mot.
        if (await db.Begreper.AnyAsync(b => b.Term == term, ct)) return;
        await begrepsregister.OpprettAsync(virksomhetId, term, definisjon, lovreferanseEid, gjelderFor: null,
            kodelisteReferanseId, skosUrl: null, begrepstype, SeedBruker, ct);
    }

    private static async Task SeedTjenesteAsync(
        RegelIdeDbContext db, TjenesteregisterTjeneste tjenesteregister, Guid virksomhetId, Guid alkohollovenId, CancellationToken ct)
    {
        const string tittel = "Alminnelig skjenkebevilling";
        // Skopet på virksomhetId, ikke bare Tittel (avvik fra SeedBegrepAsync sin "kun én Testkommunen-
        // rad i praksis"-antakelse) — denne tittelen er generisk nok til at helt uavhengige tester i en
        // delt testdatabase oppretter egne rader med samme navn under andre virksomheter, noe som ville
        // fått dette guardet til å hoppe over å opprette DENNE virksomhetens rad (bekreftet empirisk
        // 2026-08-20, se ServeringsbevillingModellSeedTests).
        if (await db.Tjenester.AnyAsync(t => t.Tittel == tittel && t.VirksomhetId == virksomhetId, ct)) return;

        var tjeneste = await tjenesteregister.OpprettAsync(
            virksomhetId, tittel,
            beskrivelse: "Bevilling til å skjenke alkoholholdig drikk til allmennheten eller en sluttet krets, jf. alkoholloven kapittel 4.",
            kompetentMyndighet: "Testkommunen",
            output: "Vedtak om skjenkebevilling",
            tjenestetype: "Enkeltvedtak",
            malgruppe: ["Virksomheter som ønsker å skjenke alkoholholdig drikk"],
            kanaler: ["Digitalt søknadsskjema"],
            kostnad: "Bevillingsgebyr fastsatt av kommunestyret, jf. alkoholforskriften kapittel 6",
            behandlingstid: "Inntil 3 måneder",
            kontaktpunkt: "Testkommunens skjenkekontor",
            konsekvensVedBrudd: "Advarsel, inndragning av bevilling eller prikktildeling, jf. alkoholforskriften kapittel 10",
            sprak: ["nb"],
            opprettetAv: SeedBruker, ct);

        foreach (var paragraf in new[] { "§4-1", "§4-2", "§4-3", "§4-4", "§4-5", "§4-6", "§4-7" })
        {
            await tjenesteregister.KobleRegelverksreferanseAsync(tjeneste.Id, alkohollovenId, $"{AlkohollovenEli}/{paragraf}", ct);
        }
    }
}
