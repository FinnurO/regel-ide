using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #469, Johanns klarsignal 2026-11-17] Forhåndsklassifiseringens relasjoner som strukturkanter. Spørsmålene: får hver relasjonskode
/// i fila én kategori og én type, eller en grunn til at den ikke skrives? Skrives en relasjon som forslag med filas hjemmel og kunnskapsstatus,
/// bare når begge nodene finnes? Og skrives den ikke på nytt når den finnes eller er avvist?
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AktorkoRelasjonerTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AktorkoRelasjonerTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static ForhandPost Post(int nr) => Forhandsklassifisering.Alle.Single(p => p.Nr == nr);

    // ------------------------------------------------------------------------------------------------ Mappingen (ren funksjon)

    [Fact]
    public void Hver_relasjonskode_i_fila_har_en_kategori_eller_en_grunn_i_tabellen()
    {
        // Ingen kode skal havne i standardgrenen «ingen kategori for relasjonen»: da er tabellen i docs/33 §5.16.3 ufullstendig.
        var koder = Forhandsklassifisering.Alle.SelectMany(p => p.Relasjoner).Select(r => r.Relasjon).Distinct().ToList();
        foreach (var kode in koder)
        {
            var m = ForhandRelasjonsmapping.Kartlegg(kode, null, null, 0);
            Assert.False(m.Grunn?.StartsWith("ingen kategori") == true, $"«{kode}» står ikke i tabellen.");
        }
        Assert.Equal(28, koder.Count);
    }

    [Theory]
    [InlineData("rolle_innehas_av", Nodetyper.Rolle, Nodetyper.Klasse, 0, "I", "innehar", false)] // bostedskommune: kommune innehar rollen
    [InlineData("rolle_innehas_av", Nodetyper.Rolle, Nodeart.Aktor, 0, "I", "innehar", false)] // tilsynsmyndighet: Statens helsetilsyn
    [InlineData("rolle_for", Nodetyper.Rolle, Nodetyper.Klasse, 1, "T", "skal_ha", false)] // kommunelege: hver kommune skal ha
    [InlineData("rolle_for", Nodetyper.Rolle, Nodeart.Aktor, 1, "G", "del_av", true)] // leder (av nemnd) del av NOKUTs klagenemnd
    [InlineData("organ_i", Nodetyper.Klasse, Nodetyper.Klasse, 0, "T", "skal_ha", false)] // kommunestyre: hver kommune skal ha
    [InlineData("organ_i", Nodeart.Aktor, Nodeart.Aktor, 0, "G", "har_organ", true)] // Høyesteretts ankeutvalg er organ for Høyesterett
    [InlineData("underklasse_av", Nodetyper.Klasse, Nodetyper.Klasse, 0, "M", "medlem_av", true)]
    [InlineData("medlem", Nodetyper.Klasse, Nodeart.Aktor, 0, "M", "medlem_av", false)]
    [InlineData("bestar_av", Nodetyper.Omrade, Nodetyper.Omrade, 0, "O", "bestar_av", true)]
    [InlineData("inngar_i", Nodetyper.Omrade, Nodetyper.Omrade, 0, "O", "bestar_av", false)]
    [InlineData("har_ansvarsomrade", Nodeart.Aktor, Nodetyper.Omrade, 0, "A", "har_ansvarsomrade", true)]
    [InlineData("klageinstans_for", Nodeart.Aktor, Nodeart.Aktor, 0, "K", "klage", true)]
    [InlineData("forvaltes_av", Nodeart.Aktor, Nodeart.Aktor, 0, "R", "forvaltes_av", true)]
    [InlineData("opprettet_av", Nodeart.Aktor, Nodeart.Aktor, 0, "R", "oppretter", false)]
    public void Kategori_type_og_retning(string relasjon, string navnet, string motparten, int rolleFor, string kategori, string type, bool navnetErFra)
    {
        var m = ForhandRelasjonsmapping.Kartlegg(relasjon, navnet, motparten, rolleFor);
        Assert.Equal((kategori, type, navnetErFra, (string?)null), (m.Kategori, m.Typekode, m.NavnetErFra, m.Grunn));
        // Kategorien og typen finnes i startsettet: skriveveien godtar dem.
        Assert.Contains(Strukturkanter.Startsett, s => s.Kategori == kategori && s.Kode == type);
    }

    [Fact]
    public void En_rolle_hos_flere_organer_er_tvetydig_og_far_ingen_G()
    {
        // «Dommere» er rolle for Høyesterett og fire klasser. G del_av ville gjort dommeren til en stilling i Høyesterett.
        var p = Post(86);
        var m = ForhandRelasjonsmapping.Kartlegg("rolle_for", Nodetyper.Rolle, Nodeart.Aktor, p.Relasjoner.Count(r => r.Relasjon == "rolle_for"));
        Assert.Null(m.Kategori);
        Assert.StartsWith("tvetydig", m.Grunn);
    }

    [Theory]
    [InlineData("omtale_av")]
    [InlineData("medlemsregel")]
    [InlineData("samme_som")]
    [InlineData("kan_bestemmes_av")]
    [InlineData("ansvarlig")]
    [InlineData("organ_for")]
    [InlineData("underlagt")]
    [InlineData("personkrets_i")]
    public void Koder_uten_kategori_eller_tvetydige_far_ingen_knapp(string relasjon)
    {
        var m = ForhandRelasjonsmapping.Kartlegg(relasjon, Nodetyper.Klasse, Nodetyper.Klasse, 0);
        Assert.Null(m.Kategori);
        Assert.False(string.IsNullOrWhiteSpace(m.Grunn));
    }

    [Fact]
    public void Feil_nodeart_gir_grunn_ikke_en_gjettet_kategori()
    {
        // Forliksrådet (klasse) har ansvarsområde kommune (klasse): A krever aktør → område.
        var m = ForhandRelasjonsmapping.Kartlegg("har_ansvarsomrade", Nodetyper.Klasse, Nodetyper.Klasse, 0);
        Assert.Null(m.Kategori);
        Assert.Contains("fra en aktør til et område", m.Grunn);
    }

    [Fact]
    public void Typen_til_en_ny_node_kommer_fra_posten_eller_tilleggsnodene()
    {
        Assert.Equal(Nodetyper.Klasse, ForhandRelasjonsmapping.ArtForNyNode("barnehage")); // n2
        Assert.Equal(Nodetyper.Klasse, ForhandRelasjonsmapping.ArtForNyNode("lagmannsrett")); // tilleggsnode
        Assert.Equal(Nodeart.Aktor, ForhandRelasjonsmapping.ArtForNyNode("NOKUTs klagenemnd")); // n33
        Assert.Null(ForhandRelasjonsmapping.ArtForNyNode("jordskifterett")); // står ikke: ingen gjettet type
    }

    [Fact]
    public void En_relasjon_merket_slutning_skrives_ikke_med_navnets_hjemmel()
    {
        Assert.True(ForhandRelasjonsmapping.ErSlutning(Post(10).Relasjoner[0])); // RHF-medlemmene: «[slutning] fra registeret»
        Assert.False(ForhandRelasjonsmapping.ErSlutning(Post(24).Relasjoner[0])); // bostedskommune
    }

    // ------------------------------------------------------------------------------------------------ Mot basen

    /// <summary>En rettskilde med noden filas hjemmel for bostedskommune (n24) står i, og én forekomst av navnet.</summary>
    private static async Task<Guid> OppsettAsync(RegelIdeDbContext db)
    {
        await Strukturkanter.SeedStartsettAsync(db);
        var p = Post(24);
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testforskrift " + id,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = id, Eid = p.Hjemmel!.Eid, KildeId = "h", NodeType = "punkt",
            Tekst = "bostedskommune: kommunen der barnet er folkeregistrert", Sorteringsrekkefolge = 0,
        });
        db.Navnekandidater.Add(new NavnekandidatEntitet
        {
            Id = Guid.NewGuid(), ForeslattTekst = "bostedskommune", Kategori = "gruppe", RettskildeId = id, NodeEid = p.Hjemmel.Eid, StartOffset = 0,
            EndOffset = 14, Status = "Godkjent", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Bostedskommune_knappen_er_av_til_rollen_finnes_skriver_forslaget_og_skriver_ikke_pa_nytt()
    {
        await using var db = _fixture.NyDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var lov = await OppsettAsync(db);
        var tjeneste = new AktorkoRelasjonTjeneste(db, new StrukturkantTjeneste(db));
        Assert.True(await db.Begreper.AnyAsync(b => b.Id == Klasseregler.KommuneKlasseId), "Klassen kommune kommer fra migrasjonen.");

        // 1. Rollen er ikke opprettet: knappen er av.
        var for1 = (await tjeneste.ForNrAsync(24))!.Relasjoner.Single();
        if (for1.Status == Relasjonsstatus.OpprettNavnetForst)
        {
            Assert.Equal(("I", "innehar"), (for1.Kategori, for1.Typekode));
            Assert.Equal("opprett navnet først (godkjenn det i køen)", for1.Grunn);
        }

        // 2. Rollen opprettes (som godkjenningen i køen gjør: lovspesifikk i hjemmelens rettskilde).
        var rolle = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Term = "bostedskommune", Begrepskategori = Nodetyper.Rolle, LovkildeId = lov, Status = "utkast",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(rolle);
        await db.SaveChangesAsync();

        var f = (await tjeneste.ForNrAsync(24))!.Relasjoner.Single();
        Assert.Equal(Relasjonsstatus.KanSkrives, f.Status);
        Assert.Equal((Klasseregler.KommuneKlasseId, rolle.Id), (f.Fra!.Id!.Value, f.Til!.Id!.Value));
        Assert.Equal((lov, Post(24).Hjemmel!.Eid, Strukturkanter.Tekstfunn), (f.Hjemmel!.RettskildeId, f.Hjemmel.Eid, f.Kunnskapsstatus));
        Assert.Equal(Strukturkanter.ManueltForeslatt, f.SkrivesSom);
        Assert.Equal("kommune innehar rollen bostedskommune", f.Utsagn);

        // 3. Nodene brukeren så må være de samme.
        await Assert.ThrowsAsync<AktorkoKonfliktException>(() => tjeneste.SkrivAsync(24, 0, new ForhandRelasjonSkriv(Guid.NewGuid(), rolle.Id), "test"));

        // 4. Skrivingen: forslag, filas hjemmel og kunnskapsstatus, opphavet i begrunnelsen.
        var skrevet = await tjeneste.SkrivAsync(24, 0, new ForhandRelasjonSkriv(f.Fra.Id, f.Til.Id), "test");
        Assert.Equal(Relasjonsstatus.Finnes, skrevet!.Status);
        var kant = await db.Strukturkanter.SingleAsync(k => k.Id == skrevet.KantId);
        Assert.Equal(("I", "innehar", Klasseregler.KommuneKlasseId, rolle.Id), (kant.Kategori, kant.Typekode, kant.FraBegrepId!.Value, kant.TilBegrepId!.Value));
        Assert.Equal((Strukturkanter.ManueltForeslatt, "manuell", Strukturkanter.Tekstfunn), (kant.Status, kant.OppdagelsesKilde, kant.Kunnskapsstatus));
        Assert.Equal((lov, Post(24).Hjemmel!.Eid), (kant.HjemmelRettskildeId!.Value, kant.HjemmelEid));
        Assert.Contains("klassifisering.json, n24, relasjon 1", kant.Begrunnelse);

        // 5. Idempotent: vises som «finnes», og skrives ikke på nytt.
        Assert.Equal(Relasjonsstatus.Finnes, (await tjeneste.ForNrAsync(24))!.Relasjoner.Single().Status);
        await Assert.ThrowsAsync<AktorkoKonfliktException>(() => tjeneste.SkrivAsync(24, 0, new ForhandRelasjonSkriv(f.Fra.Id, f.Til.Id), "test"));

        // 6. Avvist av et menneske: vises som avvist, og skrives ikke på nytt.
        await new StrukturkantTjeneste(db).AvvisAsync(kant.Id, "Johann", "ikke riktig");
        var avvist = (await tjeneste.ForNrAsync(24))!.Relasjoner.Single();
        Assert.Equal((Relasjonsstatus.Avvist, "ikke riktig"), (avvist.Status, avvist.KantAvvisningBegrunnelse));
        await Assert.ThrowsAsync<AktorkoKonfliktException>(() => tjeneste.SkrivAsync(24, 0, new ForhandRelasjonSkriv(f.Fra.Id, f.Til.Id), "test"));
        Assert.Equal(1, await db.Strukturkanter.IgnoreQueryFilters().CountAsync(k => k.TilBegrepId == rolle.Id));
    }

    [Fact]
    public async Task En_relasjon_uten_kategori_far_ingen_knapp_og_skrives_ikke()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new AktorkoRelasjonTjeneste(db, new StrukturkantTjeneste(db));
        // n2 «barnehagen»: medlemsregelen er tekst, ikke en strukturkant.
        var f = (await tjeneste.ForNrAsync(2))!.Relasjoner.Single();
        Assert.Equal((Relasjonsstatus.KanIkkeSkrives, (string?)null), (f.Status, f.Kategori));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.SkrivAsync(2, 0, new ForhandRelasjonSkriv(null, null), "test"));
        Assert.Null(await tjeneste.SkrivAsync(2, 5, new ForhandRelasjonSkriv(null, null), "test"));
    }
}
