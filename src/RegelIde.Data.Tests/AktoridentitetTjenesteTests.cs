using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #385 «aktøridentitet»] Mot ekte embedded Postgres. Spørsmålene modellen skal kunne besvare etterpå:
/// <list type="bullet">
/// <item>Hva heter aktøren i loven? Et forslag til gjeldende navneform kommer fra lovteksten (normalisering av registernavnet), men blir
/// visningsnavnet først når et menneske har godkjent det. Flere skrivemåter gir ingen gjetting.</item>
/// <item>Hvor står navnet? Et navnetreff gir navneformen en kilde og en tagg på nøyaktig spennet — ikke der skrivemåten avviker.</item>
/// <item>Hva slags aktør er det? En aktørtype foreslås, men settes ikke før den er godkjent; grunnlaget følger reglene i metodenotatet.</item>
/// <item>Er bindingens hjemmel endret? En ny versjon med endret tekst i hjemmelen eller spennet flagger bindingen.</item>
/// </list>
/// Navnene er unike per test (Guid), fordi testsamlingen deler database.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AktoridentitetTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AktoridentitetTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class KasterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Ingen eksterne oppslag her.");
    }

    private static AktoridentitetTjeneste Tjeneste(RegelIdeDbContext db) =>
        new(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new VirksomhetOppslagTjeneste(db));

    private static string Unik() => new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(8).ToArray()).PadRight(8, 'x');

    private static async Task<Virksomhet> NyVirksomhetAsync(RegelIdeDbContext db, string navn)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = navn };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    /// <summary>En referanse-rettskilde med én node per tekst, eid av <paramref name="departement"/> (ansvarlig departement).</summary>
    private static async Task<(Guid Id, List<string> Eider)> NyRettskildeAsync(RegelIdeDbContext db, string? departement, params string[] tekster)
    {
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testlov " + id,
            AnsvarligDepartement = departement is null ? null : [departement], OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var eider = new List<string>();
        for (var i = 0; i < tekster.Length; i++)
        {
            var eid = $"https://test/{id:N}/§{i + 1}/ledd-1";
            eider.Add(eid);
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = id, Eid = eid, KildeId = $"ledd-{i + 1}", NodeType = "ledd", Tekst = tekster[i],
                Sorteringsrekkefolge = i,
            });
        }
        await db.SaveChangesAsync();
        return (id, eider);
    }

    [Fact]
    public async Task Gjeldende_navneform_foreslas_fra_lovteksten_og_blir_visningsnavn_forst_ved_godkjenning()
    {
        await using var db = _fixture.NyDbContext();
        var u = Unik();
        var registernavn = $"SPESIALRÅDET{u.ToUpperInvariant()}";
        var form = $"Spesialrådet{u}";
        var aktor = await NyVirksomhetAsync(db, registernavn);
        var tvetydig = await NyVirksomhetAsync(db, $"NORGES TESTBANK{u.ToUpperInvariant()}");
        var bareSmaa = await NyVirksomhetAsync(db, $"TESTKONTORET{u.ToUpperInvariant()}");
        await NyRettskildeAsync(db, null,
            $"{form} skal avgjøre saken. Klage over {form}s vedtak går til departementet.",
            $"Det er {form} som fører tilsyn. Norges Testbank{u} og Norges testbank{u} er nevnt.",
            $"saken sendes testkontoret{u.ToLowerInvariant()}.");
        var tjeneste = Tjeneste(db);
        var bare = new[] { aktor.Id, tvetydig.Id, bareSmaa.Id };

        // Tørrkjøring: forslaget beregnes, ingenting skrives.
        var torr = await tjeneste.ForeslaGjeldendeNavneformerAsync(false, "Test", bare: bare);
        var rad = Assert.Single(torr.Rader, r => r.VirksomhetId == aktor.Id);
        Assert.Equal((Navneformforslagutfall.Foreslatt, form, Strukturkanter.Normalisering, 2), (rad.Utfall, rad.Form, rad.Kunnskapsstatus, rad.Forekomster));
        Assert.Equal(Navneformforslagutfall.FlereFormer, torr.Rader.Single(r => r.VirksomhetId == tvetydig.Id).Utfall);
        Assert.Equal(Navneformforslagutfall.IngenEgennavnform, torr.Rader.Single(r => r.VirksomhetId == bareSmaa.Id).Utfall);
        Assert.False(await db.Begreper.AnyAsync(b => b.VirksomhetReferanseId == aktor.Id));

        // Skriv: forslaget lagres, men visningsnavnet er fortsatt registernavnet.
        var skrevet = await tjeneste.ForeslaGjeldendeNavneformerAsync(true, "Test", bare: bare);
        var id = skrevet.Rader.Single(r => r.VirksomhetId == aktor.Id).NavneformId!.Value;
        var forslag = await db.Begreper.AsNoTracking().SingleAsync(b => b.Id == id);
        Assert.Equal((form, "gjeldende", Strukturkanter.MaskineltForeslatt, Strukturkanter.Normalisering),
            (forslag.Term, forslag.Navneformgrunn, forslag.Godkjenning, forslag.Kunnskapsstatus));
        Assert.NotNull(forslag.LovreferanseEid);
        Assert.Contains(registernavn, forslag.Begrunnelse);
        var visning = new VirksomhetVisningsnavnTjeneste(db);
        Assert.Null(await visning.ForAsync(aktor.Id));

        // En ny kjøring foreslår ikke på nytt: forslaget venter.
        Assert.Equal((Navneformforslagutfall.ForslagFinnes, id),
            (await tjeneste.ForeslaGjeldendeNavneformerAsync(false, "Test", bare: [aktor.Id])).Rader.Select(r => (r.Utfall, r.NavneformId!.Value)).Single());

        // Godkjent: lovens navn er visningsnavnet.
        await tjeneste.GodkjennNavneformAsync(id, "Jurist");
        Assert.Equal(form, await visning.ForAsync(aktor.Id));
    }

    [Fact]
    public async Task Avvist_navneforslag_arkiveres_med_begrunnelse_og_foreslas_ikke_igjen()
    {
        await using var db = _fixture.NyDbContext();
        var u = Unik();
        var aktor = await NyVirksomhetAsync(db, $"AVVISRÅDET{u.ToUpperInvariant()}");
        await NyRettskildeAsync(db, null, $"Avvisrådet{u} gjør vedtak.");
        var tjeneste = Tjeneste(db);
        var id = (await tjeneste.ForeslaGjeldendeNavneformerAsync(true, "Test", bare: [aktor.Id])).Rader.Single().NavneformId!.Value;

        var (navneform, tagger) = (await tjeneste.AvvisNavneformAsync(id, "Feil skrivemåte", "Jurist"))!.Value;
        Assert.Equal((Strukturkanter.Avvist, "arkivert", "Feil skrivemåte", 0), (navneform.Godkjenning, navneform.Entitetsstatus, navneform.AvvisningBegrunnelse, tagger));
        var igjen = (await tjeneste.ForeslaGjeldendeNavneformerAsync(false, "Test", bare: [aktor.Id])).Rader.Single();
        Assert.Equal(Navneformforslagutfall.TidligereAvvist, igjen.Utfall);
        // Et avvist forslag er arkivert og kan ikke godkjennes etterpå.
        Assert.Null(await tjeneste.GodkjennNavneformAsync(id, "Jurist"));
    }

    /// <summary>
    /// [Ny, issue #423] Hva betyr ordet etter avvisningen? En navneform registrert før #385 (godkjenning NULL) — her et klasseord som
    /// «Fylkeskommune» for én fylkeskommune — kan avvises med begrunnelse. Raden arkiveres (slettes ikke), avvisningen har proveniens,
    /// og navneoppslaget løser ikke lenger ordet til aktøren. En godkjent navneform kan fortsatt ikke avvises.
    /// </summary>
    [Fact]
    public async Task Navneform_registrert_for_385_kan_avvises_med_begrunnelse_men_ikke_en_godkjent()
    {
        await using var db = _fixture.NyDbContext();
        var u = Unik();
        var aktor = await NyVirksomhetAsync(db, $"TESTDAL{u.ToUpperInvariant()} FYLKESKOMMUNE");
        var klasseord = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = aktor.Id, Term = $"Fylkeskommune{u}",
            Status = "publisert", OpprettetAv = "seed", // Godkjenning og Navneformgrunn NULL: registrert før #385, uten grunn og kilde
        };
        var godkjent = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = aktor.Id, Term = $"Testdal{u} fylkeskommune",
            Navneformgrunn = "gjeldende", Godkjenning = Strukturkanter.Godkjent, Status = "publisert", OpprettetAv = "seed",
        };
        db.Begreper.AddRange(klasseord, godkjent);
        await db.SaveChangesAsync();
        var oppslag = new VirksomhetOppslagTjeneste(db);
        Assert.Equal(aktor.Id, await oppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(klasseord.Term.ToLowerInvariant()));
        var tjeneste = Tjeneste(db);

        var (navneform, tagger) = (await tjeneste.AvvisNavneformAsync(klasseord.Id, " Klasseord uten grunn og kilde (#423). ", "Jurist"))!.Value;

        Assert.Equal((Strukturkanter.Avvist, "arkivert", "arkivert", "Klasseord uten grunn og kilde (#423).", "Jurist", 0),
            (navneform.Godkjenning, navneform.Entitetsstatus, navneform.Status, navneform.AvvisningBegrunnelse, navneform.SistEndretAv, tagger));
        Assert.True(await db.Begreper.AsNoTracking().AnyAsync(b => b.Id == klasseord.Id)); // arkivert, ikke slettet
        Assert.Equal(1, await db.Proveniens.CountAsync(p => p.EntitetId == klasseord.Id && p.Handling == "avvist" && p.EndretAv == "Jurist"));
        Assert.Null(await oppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(klasseord.Term.ToLowerInvariant()));
        Assert.Contains(await tjeneste.AvvisteNavneformerAsync(aktor.Id), b => b.Id == klasseord.Id); // vises under «vis avviste»

        // En godkjent navneform har et menneskes vurdering bak seg: den avvises ikke, den fjernes med «Fjern».
        var feil = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.AvvisNavneformAsync(godkjent.Id, "nei", "Jurist"));
        Assert.Contains("ikke et forslag", feil.Message);
        var urort = await db.Begreper.AsNoTracking().SingleAsync(b => b.Id == godkjent.Id);
        Assert.Equal((Strukturkanter.Godkjent, "gjeldende", (string?)null), (urort.Godkjenning, urort.Entitetsstatus, urort.AvvisningBegrunnelse));
    }

    [Fact]
    public async Task Navnetreff_gir_kilde_og_tagg_bare_paa_noyaktig_spenn_og_forslag_via_registernavnet()
    {
        await using var db = _fixture.NyDbContext();
        var u = Unik();
        var departement = await NyVirksomhetAsync(db, $"Testdepartementet{u}");
        var tingrett = await NyVirksomhetAsync(db, $"VESTRE TESTMARK{u.ToUpperInvariant()} TINGRETT");
        var navn = $"Vestre Testmark{u} tingrett";
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = tingrett.Id, Navneformgrunn = "gjeldende",
            Term = navn, Status = "publisert", OpprettetAv = "seed",
        });
        var brreg = await NyVirksomhetAsync(db, $"NORDRE TESTDAL{u.ToUpperInvariant()} TINGRETT");
        await db.SaveChangesAsync();
        var (lov, eider) = await NyRettskildeAsync(db, departement.Navn,
            $"1. {navn}: Alta, Hammerfest. Nordre Testdal{u} tingrett: Lyngen.",
            $"Klage går til {navn.ToLowerInvariant()}.");
        var tjeneste = Tjeneste(db);

        var t1 = await tjeneste.SikreTreffAsync(tingrett.Id, lov, eider, null, "test", "Test");
        Assert.Equal((1, 1, 1), (t1.KilderSatt, t1.TaggerOpprettet, t1.UlikSkrivemaate)); // små bokstaver i § 2 tagges ikke
        var nf = await db.Begreper.AsNoTracking().SingleAsync(b => b.VirksomhetReferanseId == tingrett.Id);
        Assert.Equal((lov, eider[0], Strukturkanter.Tekstfunn), (nf.LovkildeId, nf.LovreferanseEid, nf.Kunnskapsstatus));
        var tagg = await db.TekstTagger.AsNoTracking().SingleAsync(t => t.RefId == nf.Id);
        Assert.Equal((navn, "virksomhet", eider[0], departement.Id), (tagg.QuoteExact, tagg.Kind, tagg.NodeEid, tagg.VirksomhetId));
        // Idempotent.
        Assert.Equal(1, (await tjeneste.SikreTreffAsync(tingrett.Id, lov, eider, null, "test", "Test")).TaggerFantes);

        // Treff via registernavnet: navneformen registreres som forslag (gjeldende, normalisering), med kilde og tagg, og aktørtypen foreslås.
        var t2 = await tjeneste.SikreTreffAsync(brreg.Id, lov, [eider[0]], "organ", "aktorsveip:monster", "Test");
        Assert.Equal((1, 1, 1), (t2.NavneformForslag, t2.TaggerOpprettet, t2.AktortypeForslag));
        var forslag = await db.Begreper.AsNoTracking().SingleAsync(b => b.VirksomhetReferanseId == brreg.Id);
        Assert.Equal(($"Nordre Testdal{u} tingrett", "gjeldende", Strukturkanter.MaskineltForeslatt, Strukturkanter.Normalisering, "aktorsveip:monster"),
            (forslag.Term, forslag.Navneformgrunn, forslag.Godkjenning, forslag.Kunnskapsstatus, forslag.OppdagelsesKilde));
        var aktortype = await db.AktortypeUtsagn.AsNoTracking().SingleAsync(a => a.VirksomhetId == brreg.Id);
        Assert.Equal(("organ", Strukturkanter.MaskineltForeslatt, (string?)null), (aktortype.Aktortype, aktortype.Status, aktortype.Kunnskapsstatus));
        Assert.Null((await db.Virksomheter.AsNoTracking().SingleAsync(v => v.Id == brreg.Id)).Aktortype); // et forslag setter ingenting

        // Avvist forslag: taggen som pekte på det, slettes.
        var (_, slettet) = (await tjeneste.AvvisNavneformAsync(forslag.Id, null, "Jurist"))!.Value;
        Assert.Equal(1, slettet);
    }

    [Fact]
    public async Task Aktortype_settes_forst_ved_godkjenning_og_grunnlaget_folger_metodenotatet()
    {
        await using var db = _fixture.NyDbContext();
        var u = Unik();
        var aktor = await NyVirksomhetAsync(db, $"Tilsynsutvalget for testdommere {u}");
        var tjeneste = Tjeneste(db);
        var kilde = new Utsagnsgrunnlag
        {
            Grunnlag = Utsagnsgrunnlag.Kilde, KildeTekst = "Utvalgets nettside", KildeType = "nettside_annet", KildeDokumentasjon = "sekundaer",
            KildeLenker = ["https://www.tilsynsutvalget.no/", "https://lovdata.no/"], Kunnskapsstatus = Strukturkanter.EksterntDokumentert,
            Godkjenning = Strukturkanter.FagligVurdert,
        };
        var forslag = (await tjeneste.RegistrerAktortypeAsync(aktor.Id, "organ", null, kilde, "aktorside", "Jurist"))!;
        Assert.Equal((Strukturkanter.FagligVurdert, 2), (forslag.Status, forslag.KildeUtenforKorpusLenker!.Count));
        Assert.Null((await db.Virksomheter.AsNoTracking().SingleAsync(v => v.Id == aktor.Id)).Aktortype);
        await tjeneste.GodkjennAktortypeAsync(forslag.Id, "Jurist");
        Assert.Equal("organ", (await db.Virksomheter.AsNoTracking().SingleAsync(v => v.Id == aktor.Id)).Aktortype);

        // Reglene: kilde krever lenke, «hjemmel mangler» krever uavklart med begrunnelse, tekstfunn krever hjemmel.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.ValiderAsync(kilde with { KildeLenker = [] }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.ValiderAsync(kilde with { KildeLenker = ["ikke en lenke"] }, default));
        var mangler = new Utsagnsgrunnlag { Grunnlag = Utsagnsgrunnlag.Mangler, Kunnskapsstatus = Strukturkanter.Uavklart, Godkjenning = Strukturkanter.FagligVurdert };
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.ValiderAsync(mangler, default));
        await tjeneste.ValiderAsync(mangler with { Begrunnelse = "Opprettelsesvedtaket er ikke funnet." }, default);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.ValiderAsync(kilde with { Kunnskapsstatus = Strukturkanter.Tekstfunn }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.ValiderAsync(kilde with { Godkjenning = Strukturkanter.Avvist }, default));
    }

    [Fact]
    public async Task Veiviseren_lagrer_grunnlag_og_status_paa_navneform_og_aktortype()
    {
        await using var db = _fixture.NyDbContext();
        var u = Unik();
        var departement = await NyVirksomhetAsync(db, $"Veiviserdepartementet{u}");
        var aktor = await NyVirksomhetAsync(db, $"Tilsynsutvalget for veiviserdommere {u}");
        var kortform = $"Tilsynsutvalget{u}";
        var (lov, eider) = await NyRettskildeAsync(db, departement.Navn, $"{kortform} behandler klager over dommere.");
        var vo = new VirksomhetOppslagTjeneste(db);
        var kandidater = new NavnekandidatOppdagelseTjeneste(db, new VirksomhetsbegrepTjeneste(db), new TekstTaggTjeneste(db, vo), vo,
            new EksternNavneoppslagTjeneste(new HttpClient(new KasterHandler()), db), new StrukturkantTjeneste(db));
        var kandidat = await kandidater.OpprettEllerFinnAsync(kortform, "virksomhet", lov, eider[0], 0, kortform.Length, "Test", default, "manuell");

        var grunnlag = new Utsagnsgrunnlag
        {
            Grunnlag = Utsagnsgrunnlag.Kilde, KildeTekst = "Om Tilsynsutvalget for dommere", KildeType = "nettside_annet",
            KildeDokumentasjon = "sekundaer", KildeLenker = ["https://www.tilsynsutvalget.no/"],
            Kunnskapsstatus = Strukturkanter.EksterntDokumentert, Godkjenning = Strukturkanter.FagligVurdert, Begrunnelse = "Kortform i loven.",
        };
        var r = (await kandidater.KoblTilVirksomhetAsync(kandidat.Id, aktor.Id, "kortform", "Jurist", default,
            new Veivisergrunnlag(grunnlag, "organ")))!;
        var nf = await db.Begreper.AsNoTracking().SingleAsync(b => b.Id == r.Navneform.Id);
        Assert.Equal((kortform, "kortform", Strukturkanter.FagligVurdert, Strukturkanter.EksterntDokumentert, "veiviser"),
            (nf.Term, nf.Navneformgrunn, nf.Godkjenning, nf.Kunnskapsstatus, nf.OppdagelsesKilde));
        Assert.Equal(["https://www.tilsynsutvalget.no/"], nf.KildeUtenforKorpusLenker!);
        Assert.NotNull(r.TaggId);
        var at = await db.AktortypeUtsagn.AsNoTracking().SingleAsync(a => a.VirksomhetId == aktor.Id);
        Assert.Equal(("organ", Strukturkanter.FagligVurdert), (at.Aktortype, at.Status));
        Assert.Null((await db.Virksomheter.AsNoTracking().SingleAsync(v => v.Id == aktor.Id)).Aktortype); // faglig vurdert er ikke godkjent

        // «Ikke en aktør»: avvist med begrunnelse, ikke slettet.
        var annen = await kandidater.OpprettEllerFinnAsync("behandler", "gruppe", lov, eider[0], kortform.Length + 1, kortform.Length + 10, "Test");
        await kandidater.AvvisAsync(annen.Id, "Jurist", default, "Et verb, ikke en aktør.");
        var avvist = await db.Navnekandidater.AsNoTracking().SingleAsync(k => k.Id == annen.Id);
        Assert.Equal(("Avvist", "Et verb, ikke en aktør."), (avvist.Status, avvist.AvvisningBegrunnelse));
    }

    private static string Isolert(string datokode, Func<string, string>? endring = null)
    {
        var html = Testdata.LesAlkoholloven().Replace("LOV-1989-06-02-27", datokode);
        return endring is null ? html : endring(html);
    }

    /// <summary>AC12: hjemmel endret flagger bindingen (som strukturkantene i #367); en uendret hjemmel og et uendret spenn gjør det ikke.
    /// Et tomt spenn er hele rettskilden og flagges ved enhver tekstendring. Godkjenning på nytt fjerner flagget.</summary>
    [Fact]
    public async Task Binding_flagges_naar_hjemmel_eller_spenn_er_endret_i_ny_versjon()
    {
        await using var db = _fixture.NyDbContext();
        var import = new RettskildeImportTjeneste(db);
        const string datokode = "LOV-2098-08-06-385";
        var v1 = (await import.ImporterMedUtfallAsync(LovdataKonverterer.Konverter(Isolert(datokode), new DateOnly(2026, 10, 1)))).RettskildeId;
        var endret = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == v1 && n.Overskrift != null && n.Overskrift.Contains("Lovens virkeområde"));
        var uendret = await db.RettskildeNoder.Where(n => n.RettskildeId == v1 && n.NodeType == "paragraf" && n.Id != endret.Id
                && !n.Eid.StartsWith(endret.Eid) && !endret.Eid.StartsWith(n.Eid))
            .OrderByDescending(n => n.Sorteringsrekkefolge).FirstAsync();
        var a = await NyVirksomhetAsync(db, $"Flagg A {Guid.NewGuid():N}");
        var generiske = new GeneriskNavneformTjeneste(db);
        var (navneform, _) = await generiske.OpprettEllerFinnAsync($"styret{Guid.NewGuid():N}", "Test");
        var iHjemmelen = await generiske.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, v1, Kantnode.Virksomhet(a.Id),
            Paragrafspenn: [new ParagrafspennPar(uendret.Eid, null)], HjemmelRettskildeId: v1, HjemmelEid: endret.Eid,
            Kunnskapsstatus: "tekstfunn", Status: "godkjent", GyldigFra: new DateOnly(2001, 1, 1)), "Test");
        var urort = await generiske.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, v1, Kantnode.Virksomhet(a.Id),
            Paragrafspenn: [new ParagrafspennPar(uendret.Eid, null)], HjemmelRettskildeId: v1, HjemmelEid: uendret.Eid,
            Kunnskapsstatus: "tekstfunn", Status: "godkjent", GyldigTil: new DateOnly(2000, 1, 1)), "Test");
        var hele = await generiske.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, v1, Kantnode.Virksomhet(a.Id),
            KildeUtenforKorpusTekst: "Kgl.res.", KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: "primaer",
            KildeUtenforKorpusLenker: ["https://www.regjeringen.no/", "https://lovdata.no/"], Status: "godkjent"), "Test");
        // «Hjemmel mangler»: uavklart med begrunnelse er lov, uten er det ikke.
        await Assert.ThrowsAsync<ArgumentException>(() => generiske.OpprettBindingAsync(new NyNavneformBinding(navneform.Id, v1,
            Kantnode.Virksomhet(a.Id), Paragrafspenn: [new ParagrafspennPar(endret.Eid, null)], Kunnskapsstatus: "uavklart"), "Test"));

        var resynk = await import.ImporterMedUtfallAsync(LovdataKonverterer.Konverter(
            Isolert(datokode, h => h.Replace("Lovens virkeområde.", "Lovens virkeområde og formål.")), new DateOnly(2026, 10, 16)));
        Assert.Equal(RettskildeImportUtfall.NyVersjon, resynk.Utfall);

        var etter = await db.NavneformBindinger.AsNoTracking().Where(b => b.NavneformId == navneform.Id).ToDictionaryAsync(b => b.Id);
        Assert.NotNull(etter[iHjemmelen.Id].HjemmelEndretTidspunkt);
        Assert.Null(etter[urort.Id].HjemmelEndretTidspunkt);
        // [ENDRET, issue #390, Johanns tillegg 2026-10-23] Hele rettskilden uten en tagget forekomst av navneformen flagges ikke lenger ved
        // enhver tekstendring (se testen under for tilfellene med tagger).
        Assert.Null(etter[hele.Id].HjemmelEndretTidspunkt);
        Assert.Equal(2, etter[hele.Id].KildeUtenforKorpusLenker!.Count);
        Assert.Equal("https://www.regjeringen.no/", etter[hele.Id].KildeUtenforKorpusLenke);
        Assert.Equal(Strukturkanter.Godkjent, etter[iHjemmelen.Id].Status); // ingenting annet endres
        Assert.Single(await generiske.HentBindingerAsync(navneformId: navneform.Id, bareHjemmelEndret: true));

        await generiske.GodkjennAsync(iHjemmelen.Id, "Jurist");
        Assert.Null((await db.NavneformBindinger.AsNoTracking().SingleAsync(b => b.Id == iHjemmelen.Id)).HjemmelEndretTidspunkt);
    }

    /// <summary>[Ny, issue #390, Johanns tillegg 2026-10-23 fra #385] En binding for hele rettskilden (uten paragrafspenn) får «hjemmel endret»
    /// bare når en node med en TAGGET forekomst av navneformen endres: en endring i et ledd uten forekomst gir ikke flagg, en endring i et
    /// ledd med forekomst gir flagg.</summary>
    [Fact]
    public async Task Binding_for_hele_rettskilden_flagges_bare_naar_en_tagget_forekomst_endres()
    {
        await using var db = _fixture.NyDbContext();
        var import = new RettskildeImportTjeneste(db);
        const string datokode = "LOV-2098-08-07-390";
        var v1 = (await import.ImporterMedUtfallAsync(LovdataKonverterer.Konverter(Isolert(datokode), new DateOnly(2026, 10, 1)))).RettskildeId;
        var endret = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == v1 && n.Overskrift != null && n.Overskrift.Contains("Lovens virkeområde"));
        var uendret = await db.RettskildeNoder.Where(n => n.RettskildeId == v1 && n.NodeType == "paragraf" && n.Id != endret.Id
                && !n.Eid.StartsWith(endret.Eid) && !endret.Eid.StartsWith(n.Eid))
            .OrderByDescending(n => n.Sorteringsrekkefolge).FirstAsync();
        var a = await NyVirksomhetAsync(db, $"Flagg B {Guid.NewGuid():N}");
        var generiske = new GeneriskNavneformTjeneste(db);
        var (utenForekomst, _) = await generiske.OpprettEllerFinnAsync($"styret{Guid.NewGuid():N}", "Test");
        var (medForekomst, _) = await generiske.OpprettEllerFinnAsync($"styret{Guid.NewGuid():N}", "Test");
        db.TekstTagger.AddRange(
            Tagg(v1, uendret.Eid, utenForekomst.Id, a.Id), // ordet står bare i et ledd som ikke endres
            Tagg(v1, endret.Eid, medForekomst.Id, a.Id));  // ordet står i leddet som endres
        await db.SaveChangesAsync();
        NyNavneformBinding HeleKilden(Guid navneformId) => new(navneformId, v1, Kantnode.Virksomhet(a.Id),
            KildeUtenforKorpusTekst: "Kgl.res.", KildeUtenforKorpusType: "kgl_res", KildeUtenforKorpusDokumentasjon: "primaer", Status: "godkjent");
        var ikkeFlagg = await generiske.OpprettBindingAsync(HeleKilden(utenForekomst.Id), "Test");
        var flagg = await generiske.OpprettBindingAsync(HeleKilden(medForekomst.Id), "Test");

        var resynk = await import.ImporterMedUtfallAsync(LovdataKonverterer.Konverter(
            Isolert(datokode, h => h.Replace("Lovens virkeområde.", "Lovens virkeområde og formål.")), new DateOnly(2026, 10, 16)));
        Assert.Equal(RettskildeImportUtfall.NyVersjon, resynk.Utfall);

        await using var les = _fixture.NyDbContext();
        Assert.Null((await les.NavneformBindinger.SingleAsync(b => b.Id == ikkeFlagg.Id)).HjemmelEndretTidspunkt);
        Assert.NotNull((await les.NavneformBindinger.SingleAsync(b => b.Id == flagg.Id)).HjemmelEndretTidspunkt);
    }

    private static TekstTaggEntitet Tagg(Guid rettskildeId, string eid, Guid navneformId, Guid virksomhetId) => new()
    {
        Id = Guid.NewGuid(), RettskildeId = rettskildeId, NodeEid = eid, StartOffset = 0, EndOffset = 5, QuotePrefix = "", QuoteExact = "styre",
        QuoteSuffix = "", NodeTekstHash = "x", Kind = "virksomhet", RefId = navneformId, OpprettetAv = "Test", VirksomhetId = virksomhetId,
    };

    // ---------------------------------------------------------------- [Ny, issue #386] Navnet som gjeldende navneform

    private static Utsagnsgrunnlag Hjemmel(Guid rettskildeId, string eid, string godkjenning = Strukturkanter.FagligVurdert) => new()
    {
        Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = rettskildeId, HjemmelEid = eid,
        Kunnskapsstatus = Strukturkanter.Tekstfunn, Godkjenning = godkjenning,
    };

    /// <summary>Hva heter en aktør som er opprettet med bare et navn? Navnet, med veiviserens grunnlag og status, hjemlet der det står.</summary>
    [Fact]
    public async Task Ny_aktor_med_bare_navn_far_navnet_som_gjeldende_navneform_med_grunnlaget()
    {
        await using var db = _fixture.NyDbContext();
        var u = Unik();
        var aktor = await NyVirksomhetAsync(db, $"Testutvalget{u} for dommere");
        var (rk, eider) = await NyRettskildeAsync(db, null, $"Testutvalget{u} for dommere behandler klager. Testutvalget{u} skal bestå av fem.");

        var r = await Tjeneste(db).RegistrerNavnetSomGjeldendeAsync(aktor.Id, Hjemmel(rk, eider[0]), "test", default);

        Assert.Equal(NavnetSomGjeldende.Opprettet, r.Utfall);
        Assert.Null(r.Merknad);
        var nf = await db.Begreper.SingleAsync(b => b.VirksomhetReferanseId == aktor.Id);
        Assert.Equal(($"Testutvalget{u} for dommere", "gjeldende", Strukturkanter.Tekstfunn, Strukturkanter.FagligVurdert, eider[0], (Guid?)rk),
            (nf.Term, nf.Navneformgrunn, nf.Kunnskapsstatus, nf.Godkjenning, nf.LovreferanseEid, nf.LovkildeId));

        // Andre gang: aktøren har alt en gjeldende navneform, ingenting nytt.
        var igjen = await Tjeneste(db).RegistrerNavnetSomGjeldendeAsync(aktor.Id, Hjemmel(rk, eider[0]), "test", default);
        Assert.Equal(NavnetSomGjeldende.Fantes, igjen.Utfall);
        Assert.Equal(1, await db.Begreper.CountAsync(b => b.VirksomhetReferanseId == aktor.Id));
    }

    /// <summary>Står navnet ikke i bestemmelsen som ble valgt, gjettes ingen annen: «hjemmel mangler» med begrunnelse (#395).</summary>
    [Fact]
    public async Task Navnet_som_ikke_star_i_hjemmelen_lagres_med_hjemmel_mangler()
    {
        await using var db = _fixture.NyDbContext();
        var u = Unik();
        var aktor = await NyVirksomhetAsync(db, $"Styret for testadministrasjonen{u}");
        var (rk, eider) = await NyRettskildeAsync(db, null, "Styret skal ha sju medlemmer.");

        var r = await Tjeneste(db).RegistrerNavnetSomGjeldendeAsync(aktor.Id, Hjemmel(rk, eider[0], Strukturkanter.Godkjent), "test", default);

        Assert.Equal(NavnetSomGjeldende.Opprettet, r.Utfall);
        Assert.Contains("står ikke i bestemmelsen", r.Merknad);
        var nf = r.Navneform!;
        Assert.Equal(("gjeldende", Strukturkanter.Uavklart, Strukturkanter.Godkjent, (string?)null), (nf.Navneformgrunn, nf.Kunnskapsstatus, nf.Godkjenning, nf.LovreferanseEid));
        Assert.Equal(r.Merknad, nf.Begrunnelse);
    }

    /// <summary>En navneform med samme navn uten grunn eller grunnlag (SNL ved opprettelsen) fylles ut, ingen dublett.</summary>
    [Fact]
    public async Task Eksisterende_navneform_med_samme_navn_uten_grunnlag_fylles_ut()
    {
        await using var db = _fixture.NyDbContext();
        var u = Unik();
        var aktor = await NyVirksomhetAsync(db, $"Testnemnda{u}");
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = aktor.Id, Term = $"Testnemnda{u}", Status = "publisert",
            OpprettetAv = "manuell-opprettelse", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        var (rk, eider) = await NyRettskildeAsync(db, null, $"Testnemnda{u} avgjør klager.");

        var r = await Tjeneste(db).RegistrerNavnetSomGjeldendeAsync(aktor.Id, Hjemmel(rk, eider[0]), "test", default);

        Assert.Equal(NavnetSomGjeldende.Utfylt, r.Utfall);
        var nf = await db.Begreper.SingleAsync(b => b.VirksomhetReferanseId == aktor.Id);
        Assert.Equal(("gjeldende", eider[0]), (nf.Navneformgrunn, nf.LovreferanseEid));
    }
}
