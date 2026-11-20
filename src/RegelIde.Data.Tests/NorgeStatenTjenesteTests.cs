using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #463, Johanns avgjørelse 2026-11-05 (modell B)] Norge, staten og departementene som organer for staten, mot ekte embedded Postgres.
/// Spørsmålene modellen skal kunne besvare etterpå (S1, S8):
/// <list type="number">
/// <item>Hva er Norge, og hvor er det hjemlet? Et område av typen rike, Grl. § 1 første ledd (tekstfunn), og fylkene og Svalbard er deler av det.</item>
/// <item>Hvem er staten? En aktør med navneformen «staten» og aktørtypen rettssubjekt som forslag (fortolkning, tvl. § 2-1).</item>
/// <item>Hvilke organer hører til staten? Hvert medlem av klassen departement, avledet med medlemskapets premiss.</item>
/// <item>Betyr «staten» i en bestemmelse den norske staten? Ikke når ordsperren slår til; da tagges det ikke.</item>
/// <item>Er skrivingen forslag, idempotent, og gjør tørrkjøringen ingenting?</item>
/// </list>
/// Testbasen er delt: Norge og staten har faste id-er og finnes én gang, så skrivingen kjøres i hver test og er idempotent. Påstandene gjelder
/// testens egne rader (fylket, departementet) og de faste id-ene.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class NorgeStatenTjenesteTests(EmbeddedPostgresFixture fixture) : IAsyncLifetime
{
    /// <summary>Typekodene (O bestar_av, G har_organ, M medlem_av) seedes ved oppstart i appen; testbasen har dem bare når en annen test har seedet.</summary>
    public async Task InitializeAsync()
    {
        await using var db = fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
    }

    /// <summary>Testens klasse «departement» arkiveres etter hver test (regelen er unik blant gjeldende klasser, og en gjeldende klasse endrer
    /// rutingen i andre tester, se KlasseoppsettTjenesteTests).</summary>
    public async Task DisposeAsync()
    {
        await using var db = fixture.NyDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE begreper SET entitetsstatus = 'arkivert' WHERE entitetsstatus = 'gjeldende' AND begrepskategori = 'klasse' "
            + "AND medlemskapsregel = 'organiseringsvedtak_departement'");
    }

    private static string Kort() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Bestemmelsene skrivingen kontrollerer, én gang i basen (noden må være entydig).</summary>
    private static async Task LovtekstAsync(RegelIdeDbContext db)
    {
        async Task Ledd(string tittel, string eid, string tekst)
        {
            if (await db.RettskildeNoder.AnyAsync(n => n.Eid == eid && n.Entitetsstatus == "gjeldende")) return;
            var r = new RettskildeEntitet
            {
                Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Tittel = tittel, Status = "Gjeldende", OpprettetAv = "test", AknXml = "<akomaNtoso/>",
            };
            db.Rettskilder.Add(r);
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = eid, KildeId = "l1", NodeType = "ledd", Nummer = "1", Tekst = tekst, Sorteringsrekkefolge = 1,
            });
            await db.SaveChangesAsync();
        }
        await Ledd("Kongeriket Norges Grunnlov", NorgeStatenTjeneste.GrunnlovenEid,
            "Kongeriket Norge er et fritt, selvstendig, udelelig og uavhendelig rike. Dets regjeringsform er innskrenket og arvelig monarkisk.");
        await Ledd("Kongeriket Norges Grunnlov § 16", NorgeStatenTjeneste.Grunnloven16Eid,
            "Alle innbyggere i riket har fri religionsutøvelse. Den norske kirke, en evangelisk-luthersk kirke, forblir Norges folkekirke og "
            + "understøttes som sådan av staten.");
        await Ledd("Lov om Svalbard", NorgeStatenTjeneste.SvalbardlovenEid, "Svalbard er en del av kongeriket Norge.");
        await Ledd("Kommuneloven", Klasseregler.KommunelovenInndelingEid, "Norge er inndelt i kommuner og fylkeskommuner med en egen folkevalgt ledelse.");
        await Ledd("Tvisteloven", NorgeStatenTjeneste.TvistelovenPartsevneEid, "stat, kommuner og fylkeskommuner,");
    }

    private static async Task<Guid> OmradeAsync(RegelIdeDbContext db, string type, string term)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Omrade, Omradetype = type, Omradekode = type == Omradetyper.Fylke ? $"F{Kort()}" : null,
            Term = term, Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        await db.SaveChangesAsync();
        return b.Id;
    }

    private static async Task<Guid> SvalbardAsync(RegelIdeDbContext db) =>
        await db.Begreper.Where(b => b.Begrepskategori == Nodetyper.Omrade && b.Omradetype == Omradetyper.Annet && b.Term == "Svalbard" && b.Entitetsstatus == "gjeldende")
            .Select(b => (Guid?)b.Id).FirstOrDefaultAsync() ?? await OmradeAsync(db, Omradetyper.Annet, "Svalbard");

    /// <summary>Klassen «departement» med ett medlem (M-kant uten avgrensning, kgl.res. utenfor korpus).</summary>
    private static async Task<(Guid Klasse, Guid Departement, Guid MKant)> DepartementAsync(RegelIdeDbContext db, StrukturkantTjeneste kanter)
    {
        var klasse = await db.Begreper.FirstOrDefaultAsync(b => b.Medlemskapsregel == Klasseregler.OrganiseringsvedtakDepartement && b.Entitetsstatus == "gjeldende");
        if (klasse is null)
        {
            klasse = new BegrepEntitet
            {
                Id = Guid.NewGuid(), Term = "departement", Begrepskategori = Nodetyper.Klasse, Medlemskapsregel = Klasseregler.OrganiseringsvedtakDepartement,
                Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            db.Begreper.Add(klasse);
        }
        var d = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Testdepartementet {Kort()}", Aktiv = true };
        db.Virksomheter.Add(d);
        await db.SaveChangesAsync();
        var m = await kanter.OpprettAsync(new NyStrukturkant(Strukturkanter.Medlemskap, "medlem_av", Kantnode.Virksomhet(d.Id), Kantnode.Begrep(klasse.Id),
            KildeUtenforKorpusTekst: "Kgl.res. (test)", KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: Strukturkanter.Sekundaer,
            Status: Strukturkanter.ManueltForeslatt, Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd"], Begrunnelse: "test"), "test");
        return (klasse.Id, d.Id, m.Kant.Id);
    }

    private static NorgeStatenTjeneste Tjeneste(RegelIdeDbContext db) =>
        new(db, new StrukturkantTjeneste(db), new AktoridentitetTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new VirksomhetOppslagTjeneste(db)));

    [Fact]
    public async Task Norge_fylkene_Svalbard_staten_og_departementet_skrives_som_forslag()
    {
        await using var db = fixture.NyDbContext();
        await LovtekstAsync(db);
        var fylke = await OmradeAsync(db, Omradetyper.Fylke, $"Testfylke {Kort()}");
        var svalbard = await SvalbardAsync(db);
        var (_, departement, mKant) = await DepartementAsync(db, new StrukturkantTjeneste(db));

        // Tørrkjøringen gir tallene, men skriver ingenting nytt (Norge og staten kan finnes fra en tidligere test).
        var norgeFor = await db.Begreper.AnyAsync(b => b.Id == NorgeStatenTjeneste.NorgeId);
        var torr = await Tjeneste(db).KjorAsync(skriv: false, "test");
        Assert.False(torr.Skrevet);
        Assert.Contains(torr.Omradekanter, k => k.Til.StartsWith("Testfylke") && k.Utfall == "ny");
        Assert.Contains(torr.Departementskanter, k => k.Utfall == "ny" && k.PremissKantId == mKant);
        await using (var les = fixture.NyDbContext())
        {
            Assert.Equal(norgeFor, await les.Begreper.AnyAsync(b => b.Id == NorgeStatenTjeneste.NorgeId));
            Assert.False(await les.Strukturkanter.AnyAsync(k => k.TilBegrepId == fylke && k.FraBegrepId == NorgeStatenTjeneste.NorgeId));
            Assert.False(await les.Strukturkanter.AnyAsync(k => k.FraVirksomhetId == departement && k.Kategori == Strukturkanter.Organtilhorighet));
        }

        var r = await Tjeneste(db).KjorAsync(skriv: true, "test");
        Assert.True(r.Skrevet);
        Assert.Contains(r.Norge.Utfall, new[] { "opprettet", "finnes" });
        Assert.Contains(r.Staten.Utfall, new[] { "opprettet", "finnes" });
        Assert.Equal(2, r.IkkeRegistrert.Count); // «Kongeriket Norge» og «riket»: navneformer for områder er #464

        await using var sjekk = fixture.NyDbContext();
        var norge = await sjekk.Begreper.SingleAsync(b => b.Id == NorgeStatenTjeneste.NorgeId);
        Assert.Equal(("Norge", Omradetyper.Rike, NorgeStatenTjeneste.GrunnlovenEid, Strukturkanter.Tekstfunn, Strukturkanter.ManueltForeslatt),
            (norge.Term, norge.Omradetype, norge.LovreferanseEid, norge.Kunnskapsstatus, norge.Godkjenning));

        var tilFylket = await sjekk.Strukturkanter.SingleAsync(k => k.FraBegrepId == NorgeStatenTjeneste.NorgeId && k.TilBegrepId == fylke);
        Assert.Equal((Strukturkanter.Omradesammensetning, "bestar_av", Klasseregler.KommunelovenInndelingEid, Strukturkanter.Fortolkning, Strukturkanter.MaskineltForeslatt),
            (tilFylket.Kategori, tilFylket.Typekode, tilFylket.HjemmelEid, tilFylket.Kunnskapsstatus, tilFylket.Status));
        var tilSvalbard = await sjekk.Strukturkanter.SingleAsync(k => k.FraBegrepId == NorgeStatenTjeneste.NorgeId && k.TilBegrepId == svalbard);
        Assert.Equal((NorgeStatenTjeneste.SvalbardlovenEid, Strukturkanter.Tekstfunn), (tilSvalbard.HjemmelEid, tilSvalbard.Kunnskapsstatus));

        var navneform = await sjekk.Begreper.SingleAsync(b => b.VirksomhetReferanseId == NorgeStatenTjeneste.StatenId && b.Navneformgrunn == "gjeldende");
        Assert.Equal(("staten", NorgeStatenTjeneste.Grunnloven16Eid, Strukturkanter.ManueltForeslatt), (navneform.Term, navneform.LovreferanseEid, navneform.Godkjenning));
        var aktortype = await sjekk.AktortypeUtsagn.SingleAsync(a => a.VirksomhetId == NorgeStatenTjeneste.StatenId);
        Assert.Equal(("rettssubjekt", Strukturkanter.Fortolkning, Strukturkanter.ManueltForeslatt), (aktortype.Aktortype, aktortype.Kunnskapsstatus, aktortype.Status));
        Assert.Null((await sjekk.Virksomheter.SingleAsync(v => v.Id == NorgeStatenTjeneste.StatenId)).Aktortype); // settes først ved godkjenning

        var organ = await sjekk.Strukturkanter.SingleAsync(k => k.FraVirksomhetId == departement && k.TilVirksomhetId == NorgeStatenTjeneste.StatenId);
        Assert.Equal((Strukturkanter.Organtilhorighet, "har_organ", Strukturkanter.Avledet, Strukturkanter.MaskineltForeslatt, "kgl_res"),
            (organ.Kategori, organ.Typekode, organ.Kunnskapsstatus, organ.Status, organ.KildeUtenforKorpusType));
        Assert.Equal([mKant], await sjekk.StrukturkantPremisser.Where(p => p.KantId == organ.Id).Select(p => p.PremissKantId).ToListAsync());

        // Idempotent: en ny kjøring skriver ingenting.
        var igjen = await Tjeneste(sjekk).KjorAsync(skriv: true, "test");
        Assert.Equal("finnes", igjen.Norge.Utfall);
        Assert.Equal("finnes", igjen.Staten.Utfall);
        Assert.Equal("finnes", igjen.Navneform.Utfall);
        Assert.Equal("finnes", igjen.Aktortype.Utfall);
        Assert.All(igjen.Omradekanter, k => Assert.NotEqual("ny", k.Utfall));
        Assert.All(igjen.Departementskanter, k => Assert.NotEqual("ny", k.Utfall));
    }

    [Fact]
    public async Task Ordsperren_stopper_taggingen_av_staten()
    {
        await using var db = fixture.NyDbContext();
        await LovtekstAsync(db);
        await DepartementAsync(db, new StrukturkantTjeneste(db));
        await Tjeneste(db).KjorAsync(skriv: true, "test");

        var r = new RettskildeEntitet { Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Tittel = $"Testlov {Kort()}", Status = "Gjeldende", OpprettetAv = "test", AknXml = "<akomaNtoso/>" };
        db.Rettskilder.Add(r);
        var vedlegg = new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = "vedlegg-1", KildeId = "v1", NodeType = "kapittel", Sorteringsrekkefolge = 3,
            Overskrift = "Vedlegg 1. Konvensjon av 25 oktober 1980 om de sivile sider ved internasjonal barnebortføring.",
        };
        db.RettskildeNoder.AddRange(
            new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = "ledd-1", KildeId = "l1", NodeType = "ledd", Sorteringsrekkefolge = 1,
                Tekst = "Søksmål mot staten rettes mot departementet. Myndigheten i den anmodede staten og i EØS-staten skal varsles.",
            },
            vedlegg,
            new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = "vedlegg-1-art-1", KildeId = "v2", NodeType = "ledd", Sorteringsrekkefolge = 4,
                ParentNodeId = vedlegg.Id, Tekst = "Hver kontraherende stat skal utpeke en sentralmyndighet, og staten skal gi den opplysninger.",
            });
        await db.SaveChangesAsync();

        var identitet = new AktoridentitetTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new VirksomhetOppslagTjeneste(db));
        var t = await identitet.SikreTreffAsync(NorgeStatenTjeneste.StatenId, r.Id, ["ledd-1", "vedlegg-1-art-1"], null, "test", "test");

        // Ett treff gjelder («mot staten»). «den anmodede staten» og konvensjonsteksten er sperret; «EØS-staten» er ikke et treff i det hele tatt
        // (bindestrek rett foran er ikke lenger ordgrense). Ingen eier i testkilden, så treffet som gjelder, telles som «uten eier».
        Assert.Equal(2, t.Ordsperre);
        Assert.Equal(1, t.TaggerOpprettet + t.TaggerUtenEier + t.TaggerFantes);
        var konvensjon = await Ordsperrer.KonvensjonsnoderAsync(db,
            await db.RettskildeNoder.Where(n => n.RettskildeId == r.Id && n.Tekst != null).Select(n => n.Id).ToListAsync(), CancellationToken.None);
        Assert.Single(konvensjon);
    }
}
