using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetsbegrepTjeneste"/> (docs/20 §2.3/§2.4) mot ekte embedded Postgres — de to nye
/// <see cref="BegrepEntitet.Begrepskategori"/>-verdiene, delt/nasjonal referansedata uten eiende
/// virksomhet (til forskjell fra <see cref="BegrepsregisterTjeneste"/>s ordinære fakta-/handlingsbegrep).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetsbegrepTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetsbegrepTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> OpprettAlkohollovenAsync(RegelIdeDbContext db)
    {
        var resultat = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        return resultat;
    }

    /// <summary>
    /// [Ny, nemnd/sekretariat-runden, 2026-09-09] Dublett-navneformer fantes i basen og ga dobbelt
    /// markering av samme organnavn i samme setning (observert for «Energiklagenemnda» og
    /// «Konkurransetilsynet»). Case-insensitivt fordi sveipet er det.
    /// </summary>
    [Fact]
    public async Task Nekter_samme_navneform_to_ganger_mot_samme_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-nemnd-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettVirksomhetsbegrepAsync(virksomhet.Id, "Energiklagenemnda", "Kari Jurist");

        var feil = await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettVirksomhetsbegrepAsync(virksomhet.Id, "energiklagenemnda", "Kari Jurist"));
        Assert.Contains("allerede navneformen", feil.Message);

        // …men samme term mot en ANNEN virksomhet er en helt annen opplysning og skal gå gjennom.
        var annen = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-annen-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(annen);
        await db.SaveChangesAsync();
        Assert.NotNull(await register.OpprettVirksomhetsbegrepAsync(annen.Id, "Energiklagenemnda", "Kari Jurist"));
    }

    /// <summary>
    /// [Ny, 2026-09-09] Grunnen kunne før bare settes VED opprettelse — en navneform fra
    /// katalogimporten kunne derfor ikke merkes i etterkant uten å lage en dublett.
    /// </summary>
    [Fact]
    public async Task Setter_navneformgrunn_pa_eksisterende_navneform()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-nemnd-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var register = new VirksomhetsbegrepTjeneste(db);
        var navneform = await register.OpprettVirksomhetsbegrepAsync(virksomhet.Id, "Nemnda", "Kari Jurist");
        Assert.Null(navneform.Navneformgrunn);

        Assert.True(await register.SettNavneformgrunnAsync(navneform.Id, "gjeldende", "Kari Jurist"));
        Assert.Equal("gjeldende", (await db.Begreper.SingleAsync(b => b.Id == navneform.Id)).Navneformgrunn);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.SettNavneformgrunnAsync(navneform.Id, "tullball", "Kari Jurist"));
        Assert.False(await register.SettNavneformgrunnAsync(Guid.NewGuid(), "gjeldende", "Kari Jurist"));
    }

    /// <summary>
    /// [Ny, 2026-09-09] Taggene MÅ følge med: en tagg som peker på en navneform som ikke finnes lenger
    /// er en markering i lovteksten som ikke kan følges noe sted.
    /// </summary>
    [Fact]
    public async Task Sletter_navneform_og_taggene_som_peker_pa_den()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettAlkohollovenAsync(db);
        var node = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == rettskildeId && n.Tekst != null);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-nemnd-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var register = new VirksomhetsbegrepTjeneste(db);
        var navneform = await register.OpprettVirksomhetsbegrepAsync(virksomhet.Id, node.Tekst![..5], "Kari Jurist");
        db.TekstTagger.Add(new TekstTaggEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhet.Id, RettskildeId = rettskildeId, NodeEid = node.Eid,
            StartOffset = 0, EndOffset = 5, QuotePrefix = "", QuoteExact = node.Tekst![..5], QuoteSuffix = "",
            NodeTekstHash = "hash", Kind = "virksomhet", RefId = navneform.Id,
            OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, await register.SlettVirksomhetsbegrepAsync(navneform.Id, "Kari Jurist"));
        Assert.False(await db.Begreper.AnyAsync(b => b.Id == navneform.Id));
        Assert.False(await db.TekstTagger.AnyAsync(t => t.RefId == navneform.Id));
        Assert.Null(await register.SlettVirksomhetsbegrepAsync(navneform.Id, "Kari Jurist"));
    }

    [Fact]
    public async Task Oppretter_virksomhetsbegrep_uten_eiende_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var mattilsynet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-Mattilsynet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(mattilsynet);
        await db.SaveChangesAsync();

        var register = new VirksomhetsbegrepTjeneste(db);
        var begrep = await register.OpprettVirksomhetsbegrepAsync(mattilsynet.Id, "Mattilsynet", "Kari Jurist");

        Assert.Equal("virksomhet", begrep.Begrepskategori);
        Assert.Equal(mattilsynet.Id, begrep.VirksomhetReferanseId);
        Assert.Null(begrep.VirksomhetId); // delt/nasjonal referansedata — ingen eiende virksomhet (docs/20 §2.3).

        var alle = await register.AlleVirksomhetsbegrepForAsync(mattilsynet.Id);
        Assert.Single(alle);
    }

    /// <summary>
    /// [Ny, navneformgrunn-runden, 2026-09-07] Det lukkede vokabularet for
    /// <see cref="BegrepEntitet.Navneformgrunn"/>, håndhevet i TJENESTELAGET (CHECK-constrainten
    /// `ck_begreper_navneformgrunn` er andre forsvarslinje). NULL er bevisst gyldig — uspesifisert.
    /// Johanns tre konkrete eksempler er dekket eksplisitt: Arkivverket (`utgatt`), «Suldal» som
    /// kortform for Suldal kommune (`kortform`), «Matilsynet» med én t (`feilskriving`).
    /// </summary>
    [Theory]
    [InlineData("gjeldende")]
    [InlineData("utgatt")]
    [InlineData("kortform")]
    [InlineData("feilskriving")]
    [InlineData(null)]
    public async Task Godtar_hele_navneformgrunn_vokabularet_og_null(string? grunn)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-Navneformgrunn-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var begrep = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhet.Id, NyTerm("Navneform"), "Kari Jurist", navneformgrunn: grunn);

        Assert.Equal(grunn, begrep.Navneformgrunn);

        // Faktisk PERSISTERT (ikke bare satt på det returnerte objektet) — CHECK-constrainten ville
        // ellers kunne avvise verdien ved lagring uten at testen merket det.
        await using var friskDb = _fixture.NyDbContext();
        Assert.Equal(grunn, (await friskDb.Begreper.SingleAsync(b => b.Id == begrep.Id)).Navneformgrunn);
    }

    [Theory]
    [InlineData("utdatert")]      // nær 'utgatt', men ikke i vokabularet.
    [InlineData("Gjeldende")]     // feil kasus — vokabularet er ordinalt, ikke case-insensitivt.
    [InlineData("")]              // tom streng normaliseres BEVISST ikke stille til NULL.
    [InlineData("kort form")]
    public async Task Avviser_navneformgrunn_utenfor_vokabularet(string grunn)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-Ugyldiggrunn-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = new VirksomhetsbegrepTjeneste(db);
        var feil = await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettVirksomhetsbegrepAsync(
            virksomhet.Id, NyTerm("Navneform"), "Kari Jurist", navneformgrunn: grunn));
        Assert.Contains("navneformgrunn", feil.Message, StringComparison.OrdinalIgnoreCase);

        // Ingen halvveis opprettet rad etter en avvist verdi.
        Assert.Empty(await register.AlleVirksomhetsbegrepForAsync(virksomhet.Id));
    }

    /// <summary>Grunnen er kun meningsfull for navneformer — et GRUPPEbegrep har ingen, og skal
    /// fortsatt kunne opprettes (feltet er nullbart, ikke påkrevd noe sted).</summary>
    [Fact]
    public async Task Gruppebegrep_har_ingen_navneformgrunn()
    {
        await using var db = _fixture.NyDbContext();
        var lovId = await OpprettAlkohollovenAsync(db);
        var gruppe = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(
            Nodetyper.Klasse, lovId, NyTerm("kontrollmyndighet"), "Kari Jurist");
        Assert.Null(gruppe.Navneformgrunn);
    }

    [Fact]
    public async Task Synonymer_er_bare_flere_rader_mot_samme_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var statsforvalteren = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-Statsforvalteren-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(statsforvalteren);
        await db.SaveChangesAsync();

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettVirksomhetsbegrepAsync(statsforvalteren.Id, "Statsforvalter", "Kari Jurist");
        await register.OpprettVirksomhetsbegrepAsync(statsforvalteren.Id, "Fylkesmann", "Kari Jurist");

        var alle = await register.AlleVirksomhetsbegrepForAsync(statsforvalteren.Id);
        Assert.Equal(2, alle.Count);
        Assert.Contains(alle, b => b.Term == "Statsforvalter");
        Assert.Contains(alle, b => b.Term == "Fylkesmann");
    }

    // Alkoholloven/forvaltningsloven importeres idempotent (samme ELI) og deler derfor SAMME rad på
    // tvers av alle tester i denne delte DataTestCollection-databasen — samme "unik streng per test"-
    // begrunnelse som NyKode() i KodelisteregisterTjenesteTests.
    private static string NyTerm(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    [Fact]
    public async Task Gruppebegrep_samme_term_i_samme_lov_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("kontrollmyndighet");

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, lovkildeId, term, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettGruppebegrepAsync(Nodetyper.Klasse, lovkildeId, term, "Kari Jurist"));
    }

    [Fact]
    public async Task Gruppebegrep_samme_term_i_ulik_lov_er_to_ulike_rader()
    {
        await using var db = _fixture.NyDbContext();
        var alkoholloven = await OpprettAlkohollovenAsync(db);
        var forvaltningsloven = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 8, 22)));
        var term = NyTerm("tilsynsmyndighet");

        var register = new VirksomhetsbegrepTjeneste(db);
        var forsteRad = await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, alkoholloven, term, "Kari Jurist");
        var andreRad = await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, forvaltningsloven, term, "Kari Jurist");

        Assert.NotEqual(forsteRad.Id, andreRad.Id);
        Assert.Equal(alkoholloven, forsteRad.LovkildeId);
        Assert.Equal(forvaltningsloven, andreRad.LovkildeId);
    }

    // ---------- [Ny, issue #298] Fast, nasjonalt gruppebegrep (lovkildeId=null) + case-insensitiv
    // duplikatsjekk for BEGGE grener (fast og lovspesifikt). ----------

    [Fact]
    public async Task Gruppebegrep_samme_term_ulik_case_i_samme_lov_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("Departementet");

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, lovkildeId, term, "Kari Jurist");

        // «Departementet» og «departementet» skal IKKE bli to rader — issue #298 pkt. 3.
        var feil = await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettGruppebegrepAsync(Nodetyper.Klasse, lovkildeId, term.ToLowerInvariant(), "Kari Jurist"));
        Assert.Contains("finnes allerede", feil.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fast_gruppebegrep_opprettes_uten_lovkilde()
    {
        await using var db = _fixture.NyDbContext();
        var term = NyTerm("Kongen");

        var register = new VirksomhetsbegrepTjeneste(db);
        var fast = await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, null, term, "Kari Jurist");

        Assert.Null(fast.LovkildeId);
        Assert.Equal("klasse", fast.Begrepskategori);
        Assert.Equal(term, fast.Term);
    }

    [Fact]
    public async Task Fast_gruppebegrep_samme_term_ulik_case_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var term = NyTerm("Kongen");

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, null, term, "Kari Jurist");

        var feil = await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettGruppebegrepAsync(Nodetyper.Klasse, null, term.ToUpperInvariant(), "Kari Jurist"));
        Assert.Contains("finnes allerede", feil.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Et fast (lovkildeId=null) og et lovspesifikt gruppebegrep med SAMME Term er to ulike
    /// rader — "null" er sin egen scope, ikke en tredje lov som tilfeldigvis matcher alle andre.</summary>
    [Fact]
    public async Task Fast_og_lovspesifikt_gruppebegrep_med_samme_term_er_to_ulike_rader()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("tilsynsorganet");

        var register = new VirksomhetsbegrepTjeneste(db);
        var fast = await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, null, term, "Kari Jurist");
        var lovspesifikt = await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, lovkildeId, term, "Kari Jurist");

        Assert.NotEqual(fast.Id, lovspesifikt.Id);
        Assert.Null(fast.LovkildeId);
        Assert.Equal(lovkildeId, lovspesifikt.LovkildeId);
    }

    /// <summary>To ulike, FASTE gruppebegrep (begge lovkildeId=null) med samme term — uten en egen
    /// delvis indeks for null-grenen ville Postgres' standard NULL != NULL-oppførsel i en unik indeks
    /// IKKE dedupet disse (issue #298 AC4) — denne testen dekker nettopp det DB-vernet, ikke bare
    /// applikasjonssjekken over.</summary>
    [Fact]
    public async Task To_faste_gruppebegrep_med_samme_term_kastes_pa_db_niva()
    {
        await using var db = _fixture.NyDbContext();
        var term = NyTerm("Stortinget");
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            Begrepskategori = "gruppe",
            LovkildeId = null,
            Term = term,
            Status = "publisert",
            OpprettetAv = "Kari Jurist",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        // Omgår applikasjonssjekken i OpprettGruppebegrepAsync ved å skrive rett til basen — dette
        // isolerer DEN UNIKE INDEKSENS egen evne til å hindre to "LovkildeId IS NULL"-rader med
        // samme (case-sensitivt) Term, uavhengig av C#-koden rundt.
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            Begrepskategori = "gruppe",
            LovkildeId = null,
            Term = term,
            Status = "publisert",
            OpprettetAv = "Kari Jurist",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task OpprettEllerGjenbrukFastGruppebegrep_gjenbruker_eksisterende_rad()
    {
        await using var db = _fixture.NyDbContext();
        var term = NyTerm("Kongen i statsrad");

        var register = new VirksomhetsbegrepTjeneste(db);
        var (forste, forsteVarNy) = await register.OpprettEllerGjenbrukFastGruppebegrepAsync(Nodetyper.Klasse, term, "Kari Jurist");
        Assert.True(forsteVarNy);

        // Ulik case OG ledende/avsluttende whitespace — samme toleranse som den øvrige dedupen i denne klassen.
        var (andre, andreVarNy) = await register.OpprettEllerGjenbrukFastGruppebegrepAsync(
            Nodetyper.Klasse, $"  {term.ToUpperInvariant()}  ", "Ola Saksbehandler");
        Assert.False(andreVarNy);
        Assert.Equal(forste.Id, andre.Id);

        var alle = await register.AlleGruppebegrepAsync();
        Assert.Single(alle, b => string.Equals(b.Term, term, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FinnFastGruppebegrep_finner_ikke_lovspesifikt_begrep_med_samme_term()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("kontrollorganet");

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, lovkildeId, term, "Kari Jurist");

        Assert.Null(await register.FinnFastGruppebegrepAsync(term));
    }

    // ---------- [Ny, issue #299 AC3/AC4] Get-or-create for LOVSPESIFIKT gruppebegrep og administrativ
    // inndeling — brukt av NavnekandidatOppdagelseTjeneste.GodkjennGruppeBatchAsync («Behandle
    // gruppen»), speil av OpprettEllerGjenbrukFastGruppebegrepAsync over. ----------

    [Fact]
    public async Task OpprettEllerGjenbrukGruppebegrep_gjenbruker_eksisterende_lovspesifikt_rad()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("tilsynsorganet");

        var register = new VirksomhetsbegrepTjeneste(db);
        var (forste, forsteVarNy) = await register.OpprettEllerGjenbrukGruppebegrepAsync(Nodetyper.Klasse, lovkildeId, term, "Kari Jurist");
        Assert.True(forsteVarNy);
        Assert.Equal(lovkildeId, forste.LovkildeId);

        // Ulik case OG whitespace — samme toleranse som den faste varianten.
        var (andre, andreVarNy) = await register.OpprettEllerGjenbrukGruppebegrepAsync(
            Nodetyper.Klasse, lovkildeId, $"  {term.ToUpperInvariant()}  ", "Ola Saksbehandler");
        Assert.False(andreVarNy);
        Assert.Equal(forste.Id, andre.Id);
    }

    [Fact]
    public async Task OpprettEllerGjenbrukGruppebegrep_ulik_lov_gir_to_ulike_rader()
    {
        await using var db = _fixture.NyDbContext();
        var alkoholloven = await OpprettAlkohollovenAsync(db);
        var forvaltningsloven = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 8, 22)));
        var term = NyTerm("kontrollmyndigheten");

        var register = new VirksomhetsbegrepTjeneste(db);
        var (forste, _) = await register.OpprettEllerGjenbrukGruppebegrepAsync(Nodetyper.Klasse, alkoholloven, term, "Kari Jurist");
        var (andre, andreVarNy) = await register.OpprettEllerGjenbrukGruppebegrepAsync(Nodetyper.Klasse, forvaltningsloven, term, "Kari Jurist");

        Assert.True(andreVarNy);
        Assert.NotEqual(forste.Id, andre.Id);
    }

    [Fact]
    public async Task FinnGruppebegrep_finner_ikke_fast_begrep_med_samme_term()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("Kongen");

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettGruppebegrepAsync(Nodetyper.Klasse, null, term, "Kari Jurist");

        Assert.Null(await register.FinnGruppebegrepAsync(lovkildeId, term));
    }

    // ---------- [ENDRET, issue #310 «nodetype-akse», 2026-10-07] Testene for administrativ inndeling
    // (issue #203 pkt. 2) er erstattet: kategorien er slått inn i 'omrade', og OpprettAdministrativ-
    // InndelingAsync er fjernet. Det de testet — (Term, LovkildeId)-scoping og gjenbruk — dekkes nå av
    // nodetype-testene under, som i tillegg låser at typene deler ÉN identitet. ----------

    [Theory]
    [InlineData("klasse")]
    [InlineData("rolle")]
    [InlineData("omrade")]
    // [FJERNET, issue #311] [InlineData("organ")] — organ er ikke lenger en begrepskategori, se testen under.
    public async Task Nytt_begrep_far_valgt_nodetype_og_ingen_navneformgrunn(string nodetype)
    {
        await using var db = _fixture.NyDbContext();
        var lovId = await OpprettAlkohollovenAsync(db);
        var begrep = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(
            nodetype, lovId, NyTerm("Suldal kommune"), "Kari Jurist");

        Assert.Equal(nodetype, begrep.Begrepskategori);
        Assert.Equal(lovId, begrep.LovkildeId);
        Assert.Null(begrep.VirksomhetId);
        Assert.Null(begrep.Navneformgrunn);
        Assert.Equal("publisert", begrep.Status);
    }

    /// <summary>Ingen nye 'gruppe'-begrep (utfases), og ingen ukjent verdi — ingen gjettet fallback.</summary>
    [Theory]
    [InlineData("gruppe")]
    [InlineData("administrativ_inndeling")]
    [InlineData("organ")] // [Ny, issue #311] organ er en Virksomhet, ikke en begrepskategori.
    [InlineData("Klasse")]
    [InlineData("")]
    public async Task Ugyldig_eller_utfaset_nodetype_kastes(string nodetype)
    {
        await using var db = _fixture.NyDbContext();
        var feil = await Assert.ThrowsAsync<ArgumentException>(
            () => new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(nodetype, null, NyTerm("x"), "Kari Jurist"));
        Assert.Contains("nodetype", feil.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>[ENDRET, issue #310] Var «samme term og lov er OK for gruppe og administrativ inndeling
    /// samtidig» — de to hadde hver sin unike indeks. Nå deler alle typene med gruppefunksjon ÉN
    /// identitet (ux_begreper_nodebegrep_term_lovkilde): «reguleringsmyndighet» i energiloven kan ikke
    /// finnes både som rolle og som klasse — det ville vært samme begrep registrert to ganger.</summary>
    [Fact]
    public async Task Samme_term_og_lov_med_ulik_nodetype_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("reguleringsmyndighet");

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettGruppebegrepAsync(Nodetyper.Rolle, lovkildeId, term, "Kari Jurist");

        var feil = await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettGruppebegrepAsync(Nodetyper.Klasse, lovkildeId, term, "Kari Jurist"));
        Assert.Contains("som rolle", feil.Message);
    }

    /// <summary>DB-vernet bak testen over — skrevet rett til basen, forbi applikasjonssjekken.</summary>
    [Fact]
    public async Task Samme_term_og_lov_med_ulik_nodetype_kastes_pa_db_niva()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("kommunene");
        foreach (var kategori in new[] { "klasse", "omrade" })
        {
            db.Begreper.Add(new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = kategori, LovkildeId = lovkildeId, Term = term,
                Status = "publisert", OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>Get-or-create gjenbruker KUN samme type — en klasse-forekomst kobles aldri stille til et
    /// område med samme navn (gjettet kobling). Gjelder både lovspesifikk og fast gren.</summary>
    [Fact]
    public async Task OpprettEllerGjenbruk_med_annen_nodetype_enn_eksisterende_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("Suldal kommune");

        var register = new VirksomhetsbegrepTjeneste(db);
        var (omrade, varNy) = await register.OpprettEllerGjenbrukGruppebegrepAsync(Nodetyper.Omrade, lovkildeId, term, "Kari Jurist");
        Assert.True(varNy);
        var (igjen, igjenVarNy) = await register.OpprettEllerGjenbrukGruppebegrepAsync(
            Nodetyper.Omrade, lovkildeId, $"  {term.ToUpperInvariant()}  ", "Ola Saksbehandler");
        Assert.False(igjenVarNy);
        Assert.Equal(omrade.Id, igjen.Id);
        await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettEllerGjenbrukGruppebegrepAsync(Nodetyper.Klasse, lovkildeId, term, "Kari Jurist"));

        var fastTerm = NyTerm("Kongen i statsrad");
        await register.OpprettEllerGjenbrukFastGruppebegrepAsync(Nodetyper.Klasse, fastTerm, "Kari Jurist");
        await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettEllerGjenbrukFastGruppebegrepAsync(Nodetyper.Rolle, fastTerm, "Kari Jurist"));
    }

    /// <summary>En gjenværende 'gruppe'-rad (andre miljøer) gjenbrukes ikke stille som en type — feilen
    /// sier at nodetypen må settes på den først.</summary>
    [Fact]
    public async Task OpprettEllerGjenbruk_mot_gjenvaerende_gruppe_rad_ber_om_reklassifisering()
    {
        await using var db = _fixture.NyDbContext();
        var term = NyTerm("Statsforvalteren");
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "gruppe", LovkildeId = null, Term = term,
            Status = "publisert", OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var feil = await Assert.ThrowsAsync<ArgumentException>(
            () => new VirksomhetsbegrepTjeneste(db).OpprettEllerGjenbrukFastGruppebegrepAsync(Nodetyper.Klasse, term, "Kari Jurist"));
        Assert.Contains("'gruppe'", feil.Message);
    }

    /// <summary>SettNodetypeAsync — veien for å reklassifisere gjenværende 'gruppe'-rader for hånd.
    /// Tildelinger peker på begrepets id og følger uendret med.</summary>
    [Fact]
    public async Task SettNodetype_reklassifiserer_gruppe_rad_og_beholder_tildelinger()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var gruppe = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "gruppe", LovkildeId = lovkildeId, Term = NyTerm("vertskommuner"),
            Status = "publisert", OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Vertskommune-{Guid.NewGuid():N}" };
        db.Begreper.Add(gruppe);
        db.Virksomheter.Add(virksomhet);
        // [ENDRET, issue #311] Tildelingen er en M-kant.
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = Strukturkanter.Medlemskap, Typekode = Strukturkanter.MedlemAv,
            FraVirksomhetId = virksomhet.Id, TilBegrepId = gruppe.Id, HjemmelRettskildeId = lovkildeId,
            OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var register = new VirksomhetsbegrepTjeneste(db);
        var oppdatert = await register.SettNodetypeAsync(gruppe.Id, Nodetyper.Klasse, "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal("klasse", oppdatert!.Begrepskategori);
        Assert.Equal(1, await db.Strukturkanter.CountAsync(m => m.TilBegrepId == gruppe.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => register.SettNodetypeAsync(gruppe.Id, "gruppe", "Kari Jurist"));
        Assert.Null(await register.SettNodetypeAsync(Guid.NewGuid(), Nodetyper.Rolle, "Kari Jurist"));
    }

    /// <summary>Listene som picker-ene bygger på tar med ALLE typene med gruppefunksjon.</summary>
    [Fact]
    public async Task AlleGruppebegrep_inkluderer_alle_nodetyper()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var register = new VirksomhetsbegrepTjeneste(db);
        var ider = new List<Guid>();
        foreach (var type in Nodetyper.Settbare)
        {
            ider.Add((await register.OpprettGruppebegrepAsync(type, lovkildeId, NyTerm($"t-{type}"), "Kari Jurist")).Id);
        }

        var alle = (await register.AlleGruppebegrepAsync()).Select(b => b.Id).ToHashSet();
        var forLov = (await register.AlleGruppebegrepForLovAsync(lovkildeId)).Select(b => b.Id).ToHashSet();
        var alleAsync = (await register.AlleAsync()).Select(b => b.Id).ToHashSet();
        Assert.All(ider, id => Assert.Contains(id, alle));
        Assert.All(ider, id => Assert.Contains(id, forLov));
        Assert.All(ider, id => Assert.Contains(id, alleAsync));
    }
}
