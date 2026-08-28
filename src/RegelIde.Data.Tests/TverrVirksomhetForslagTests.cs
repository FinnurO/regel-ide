using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, 2026-08-28, import-wizard-runden] Dekker `OpprettForslagFraAnnenVirksomhetAsync` (både
/// Tjeneste- og Handling-varianten) — kjernen i "tverr-virksomhet import-forslag"-mekanismen.
/// End-til-ende-verifisering av selve import-wizard-flyten er gjort manuelt mot en kjørende server
/// (se sesjonsnotatet); disse testene dekker service-laget isolert.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class TverrVirksomhetForslagTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TverrVirksomhetForslagTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn)
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = navn });
        await db.SaveChangesAsync();
        return virksomhet;
    }

    [Fact]
    public async Task OpprettForslagFraAnnenVirksomhetAsync_setter_riktig_eierskap_status_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");

        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", "Skatteetaten", ["Personer som skal gifte seg"],
            null, "Kari Jurist");

        Assert.Equal(mal, tjeneste.VirksomhetId);
        Assert.Equal("foreslatt_av_annen_virksomhet", tjeneste.Status);

        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetType == "tjeneste" && p.EntitetId == tjeneste.Id);
        Assert.Equal("foreslatt_av_annen_virksomhet", proveniens.Handling);
        Assert.Equal(importor, proveniens.ForeslattAvVirksomhetId);
        Assert.Equal(mal, proveniens.VirksomhetId);
    }

    /// <summary>
    /// [Ny, 2026-08-29] Retter funnet fra kodegjennomgangen: `OpprettForslagFraAnnenVirksomhetAsync`s
    /// doc-kommentar påsto (feilaktig) at metoden speilet `OpprettAsync` feltmessig — den manglet i
    /// realiteten åtte felt (Beskrivelse/Output/Tjenestetype/Kanaler/Kostnad/Behandlingstid/
    /// Kontaktpunkt/Sprak), som ble stille forkastet for en tverr-virksomhet-import selv om NØYAKTIG
    /// samme JSON importert til egen virksomhet beholdt dem.
    /// </summary>
    [Fact]
    public async Task OpprettForslagFraAnnenVirksomhetAsync_persisterer_samme_fulle_feltsett_som_OpprettAsync()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");

        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", "Skatteetaten", ["Personer som skal gifte seg"],
            null, "Kari Jurist", beskrivelse: "En attest som bekrefter ekteskapsvilkårene", output: "Prøvingsattest (PDF)",
            tjenestetype: "myndighetsutovelse", kanaler: ["Skatteetaten.no"], kostnad: "Gratis",
            behandlingstid: "2 uker", kontaktpunkt: "Skatteetatens kundesenter", sprak: ["nb"]);

        Assert.Equal("En attest som bekrefter ekteskapsvilkårene", tjeneste.Beskrivelse);
        Assert.Equal("Prøvingsattest (PDF)", tjeneste.Output);
        Assert.Equal("myndighetsutovelse", tjeneste.Tjenestetype);
        Assert.Equal(["Skatteetaten.no"], tjeneste.Kanaler);
        Assert.Equal("Gratis", tjeneste.Kostnad);
        Assert.Equal("2 uker", tjeneste.Behandlingstid);
        Assert.Equal("Skatteetatens kundesenter", tjeneste.Kontaktpunkt);
        Assert.Equal(["nb"], tjeneste.Sprak);
    }

    [Fact]
    public async Task Handling_OpprettForslagFraAnnenVirksomhetAsync_setter_riktig_status_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        var handling = await new HandlingregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, tjeneste.Id, "Søke om prøvingsattest", "soke", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist", importor);

        Assert.Equal("foreslatt_av_annen_virksomhet", handling.Status);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetType == "handling" && p.EntitetId == handling.Id);
        Assert.Equal(importor, proveniens.ForeslattAvVirksomhetId);
    }

    [Fact]
    public async Task Forslagskoen_filtrerer_pa_riktig_malvirksomhet_ikke_importorens_egen()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        await new TjenesteregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        var iMal = await db.Tjenester.Where(t => t.VirksomhetId == mal && t.Status == "foreslatt_av_annen_virksomhet").ToListAsync();
        var iImportor = await db.Tjenester.Where(t => t.VirksomhetId == importor && t.Status == "foreslatt_av_annen_virksomhet").ToListAsync();

        Assert.Single(iMal);
        Assert.Empty(iImportor);
    }

    /// <summary>
    /// [Ny, 2026-08-28] Selve gapet som utløste `SlettForslagAsync`: en importerende virksomhet kunne
    /// ikke rydde opp sine egne tverr-virksomhet-testforslag, siden `SettStatusAsync`s eierskapssjekk
    /// alene (VirksomhetId == bruker.VirksomhetId) blokkerer alt annet enn MÅL-virksomheten selv —
    /// oppdaget live under opprydding etter vielsesreise-importtesten.
    /// </summary>
    [Fact]
    public async Task SlettForslagAsync_lar_importoren_slette_sitt_eget_tverr_virksomhet_forslag()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        var slettet = await register.SlettForslagAsync(tjeneste.Id, importor);

        Assert.True(slettet);
        Assert.False(await db.Tjenester.AnyAsync(t => t.Id == tjeneste.Id));
    }

    [Fact]
    public async Task SlettForslagAsync_lar_ogsa_maal_virksomheten_selv_slette()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        var slettet = await register.SlettForslagAsync(tjeneste.Id, mal);

        Assert.True(slettet);
    }

    [Fact]
    public async Task SlettForslagAsync_nekter_en_tredje_virksomhet_uten_tilknytning()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var utenforstaende = await NyVirksomhetAsync(db, "UDI");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => register.SlettForslagAsync(tjeneste.Id, utenforstaende));
        Assert.True(await db.Tjenester.AnyAsync(t => t.Id == tjeneste.Id));
    }

    [Fact]
    public async Task SlettForslagAsync_nekter_en_tjeneste_som_ikke_lenger_er_et_ubehandlet_forslag()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");
        // Mål-virksomheten har godkjent forslaget — det er ikke lenger "ubehandlet".
        await register.SettStatusAsync(tjeneste.Id, mal, "validert", "Skatteetaten-saksbehandler");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => register.SlettForslagAsync(tjeneste.Id, importor));
        Assert.Contains("ubehandlet forslag", ex.Message);
        Assert.True(await db.Tjenester.AnyAsync(t => t.Id == tjeneste.Id));
    }

    [Fact]
    public async Task SlettForslagAsync_sletter_avhengigheter_og_handlinger_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var tjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");
        var handling = await new HandlingregisterTjeneste(db).OpprettForslagFraAnnenVirksomhetAsync(
            mal, tjeneste.Id, "Søke om prøvingsattest", "soke", null, "soker",
            null, null, null, null, null, null, null, null, "Kari Jurist", importor);
        var annenTjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Ekteskap (vigsel)", null, null, null, "Kari Jurist");
        await new TjenesteavhengighetregisterTjeneste(db).OpprettAsync(
            mal, tjeneste.Id, annenTjeneste.Id, "forutsetning_for", null, null, "Kari Jurist");

        var slettet = await register.SlettForslagAsync(tjeneste.Id, importor);

        Assert.True(slettet);
        Assert.False(await db.Handlinger.AnyAsync(h => h.Id == handling.Id));
        Assert.False(await db.Tjenesteavhengigheter.AnyAsync(a => a.FraTjenesteId == tjeneste.Id || a.TilTjenesteId == tjeneste.Id));
        Assert.False(await db.Proveniens.AnyAsync(p => p.EntitetType == "tjeneste" && p.EntitetId == tjeneste.Id));
        Assert.False(await db.Proveniens.AnyAsync(p => p.EntitetType == "handling" && p.EntitetId == handling.Id));
        // Motparten i avhengigheten selv skal IKKE bli slettet — kun tjenesten vi ba om å slette.
        Assert.True(await db.Tjenester.AnyAsync(t => t.Id == annenTjeneste.Id));
    }

    /// <summary>
    /// [Ny, 2026-08-29] Retter funnet fra kodegjennomgangen (PR #55): den opprinnelige
    /// `SlettForslagAsync` slettet tjenesteavhengigheter i BEGGE retninger uten å sjekke den andre
    /// siden — en allerede validert/publisert tjenestes ekte avhengighet kunne dermed forsvinne
    /// stille bare fordi den pekte mot et forslag noen ryddet bort. Motsatt av testen over
    /// (`..._sletter_avhengigheter_...`, der motparten OGSÅ er et forslag og slettingen derfor er
    /// trygg) — her er motparten en ekte, validert tjeneste, og slettingen skal nektes.
    /// </summary>
    [Fact]
    public async Task SlettForslagAsync_nekter_sletting_nar_en_validert_tjeneste_har_avhengighet_hit()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var forslag = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");
        var validertTjeneste = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Ekteskap (vigsel)", null, null, null, "Kari Jurist");
        await register.SettStatusAsync(validertTjeneste.Id, mal, "validert", "Skatteetaten-saksbehandler");
        await new TjenesteavhengighetregisterTjeneste(db).OpprettAsync(
            mal, validertTjeneste.Id, forslag.Id, "forutsetning_for", null, null, "Skatteetaten-saksbehandler");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => register.SlettForslagAsync(forslag.Id, importor));

        Assert.Contains("allerede behandlet tjeneste", ex.Message);
        // Verken forslaget eller den ekte avhengigheten skal ha blitt rørt.
        Assert.True(await db.Tjenester.AnyAsync(t => t.Id == forslag.Id));
        Assert.True(await db.Tjenesteavhengigheter.AnyAsync(a => a.FraTjenesteId == validertTjeneste.Id && a.TilTjenesteId == forslag.Id));
    }

    /// <summary>
    /// [Ny, 2026-08-29] Retter funnet fra kodegjennomgangen: `Vilkar.TjenesteId` og
    /// `Tjeneste.ErstatterId` manglet eksplisitt `OnDelete`-oppførsel (Postgres NO ACTION/Restrict som
    /// standard) — en hard-sletting av et forslag som fortsatt hadde et koblet vilkår, eller som en
    /// annen tjeneste tilfeldigvis "erstattet", kastet en ufanget FK-brudd-exception i stedet for å
    /// fullføre. Begge er nå `SetNull` (RegelIdeDbContext.cs) — verifiser at slettingen faktisk
    /// fullfører og at koblingene bare nulles ut, ikke at vilkåret/den andre tjenesten forsvinner.
    /// </summary>
    [Fact]
    public async Task SlettForslagAsync_fullforer_og_setter_null_pa_tilknyttet_vilkar_og_erstatter_referanse()
    {
        await using var db = _fixture.NyDbContext();
        var importor = await NyVirksomhetAsync(db, "Testkommunen");
        var mal = await NyVirksomhetAsync(db, "Skatteetaten");
        var register = new TjenesteregisterTjeneste(db);
        var forslag = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Prøvingsattest for ekteskap", null, null, null, "Kari Jurist");

        var vilkar = new VilkarEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = mal, TjenesteId = forslag.Id, Tittel = "Gyldig prøvingsattest",
            Vilkarstype = "formell", Vurderingstype = "regelbasert", Status = "utkast", OpprettetAv = "Kari Jurist",
        };
        db.Vilkar.Add(vilkar);
        var erstatterForslag = await register.OpprettForslagFraAnnenVirksomhetAsync(
            mal, importor, "Ekteskap (vigsel)", null, null, null, "Kari Jurist");
        var erstatterTjeneste = await db.Tjenester.SingleAsync(t => t.Id == erstatterForslag.Id);
        erstatterTjeneste.ErstatterId = forslag.Id;
        await db.SaveChangesAsync();

        var slettet = await register.SlettForslagAsync(forslag.Id, importor);

        Assert.True(slettet);
        // AsNoTracking — DB-nivåets ON DELETE SET NULL skjedde utenom denne DbContext-instansens
        // endringssporing (samme klasse årsak som ExecuteDeleteAsync-kommentaren i selve metoden); uten
        // dette leser SingleAsync den allerede sporede (og nå utdaterte) in-memory-kopien av radene i
        // stedet for den faktiske, nettopp nullede databaseverdien.
        // Vilkåret selv består — kun koblingen til den slettede tjenesten er nullet ut.
        var vilkarEtterpa = await db.Vilkar.AsNoTracking().SingleAsync(v => v.Id == vilkar.Id);
        Assert.Null(vilkarEtterpa.TjenesteId);
        var erstatterEtterpa = await db.Tjenester.AsNoTracking().SingleAsync(t => t.Id == erstatterForslag.Id);
        Assert.Null(erstatterEtterpa.ErstatterId);
    }
}
