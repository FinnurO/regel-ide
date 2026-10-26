using Microsoft.EntityFrameworkCore;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #390] Kommunens tilhørighet via territoriet, registerkanter som eksternt dokumentert, kortformen per kommune og bindingen av
/// den. Spørsmålene: hvilken tingrett, lagmannsrett og hvilket fylke hører kommunen (rettssubjektet) til, og hvilke premisser bygger svaret
/// på? Kan svaret vises som sterkere enn det svakeste premisset? Blir registerkantene eksternt dokumentert etter regel, med proveniens, og
/// kan det angres? Får hver kommune kortformen som forslag, og listes kollisjonene? Betyr «Alta» området der en binding sier det, og ellers
/// kommunen?
/// <para>Testbasen er delt: kodene er unike per test, og påstandene gjelder bare testens egne rader.</para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class KommuneTilhorighetTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KommuneTilhorighetTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Kanttypene (relasjonstype_konfigurasjon) må finnes; startsettet seedes idempotent.</summary>
    private async Task<RegelIdeDbContext> DbAsync()
    {
        var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        return db;
    }

    private static string Kode() => "T" + Guid.NewGuid().ToString("N")[..7];

    private static async Task<Guid> OmradeAsync(RegelIdeDbContext db, string type, string? kode, string term)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Omrade, Omradetype = type, Omradekode = kode, Term = term,
            Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        await db.SaveChangesAsync();
        return b.Id;
    }

    private static async Task<Guid> AktorAsync(RegelIdeDbContext db, string navn, string? kommunenummer = null, string? orgnr = null)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = navn, Aktortype = "rettssubjekt", Kommunenummer = kommunenummer, Organisasjonsnummer = orgnr };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    private static async Task<Guid> KantAsync(StrukturkantTjeneste kanter, string kategori, string type, Kantnode fra, Kantnode til,
        string kildetype = Strukturkanter.Register, string? kunnskapsstatus = null, string status = Strukturkanter.Godkjent) =>
        (await kanter.OpprettAsync(new NyStrukturkant(kategori, type, fra, til,
            KildeUtenforKorpusTekst: $"Testkilde ({kildetype})", KildeUtenforKorpusType: kildetype,
            KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer, Kunnskapsstatus: kunnskapsstatus, Status: status), "test")).Kant.Id;

    /// <summary>Et lite register: kommune K i fylke F, kommunens rettssubjekt V, tingrett D med sete i S og lagsogn L, lagdømme LD og
    /// lagmannsrett LM.</summary>
    private sealed record Register(Guid K, Guid F, Guid V, Guid D, Guid S, Guid L, Guid LD, Guid LM,
        Guid Territorie, Guid FylkeO, Guid TingrettA, Guid SeteA, Guid SognerA, Guid LagdommeO, Guid LagmannsrettA);

    private static async Task<Register> RegisterAsync(RegelIdeDbContext db, string kode, string? tingrettStatus = null)
    {
        var kanter = new StrukturkantTjeneste(db);
        var k = await OmradeAsync(db, Omradetyper.Kommune, kode, $"Testby{kode}");
        var f = await OmradeAsync(db, Omradetyper.Fylke, "F" + kode, $"Testfylke{kode}");
        var s = await OmradeAsync(db, Omradetyper.Tettsted, null, $"Testsete{kode}");
        var l = await OmradeAsync(db, Omradetyper.Lagsogn, null, $"Testlagsogn{kode}");
        var ld = await OmradeAsync(db, Omradetyper.Lagdomme, null, $"Testlagdømme{kode}");
        var v = await AktorAsync(db, $"Testby{kode} kommune", kode);
        var d = await AktorAsync(db, $"Testby{kode} tingrett");
        var lm = await AktorAsync(db, $"Testby{kode} lagmannsrett");
        var territorie = await KantAsync(kanter, Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(v), Kantnode.Begrep(k),
            kunnskapsstatus: Strukturkanter.EksterntDokumentert);
        var fylkeO = await KantAsync(kanter, Strukturkanter.Omradesammensetning, "bestar_av", Kantnode.Begrep(f), Kantnode.Begrep(k),
            kunnskapsstatus: Strukturkanter.EksterntDokumentert);
        // Domstolkantene har ingen vurdert kunnskapsstatus (som i den lokale basen 2026-10-26).
        var tingrettA = await KantAsync(kanter, Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(d), Kantnode.Begrep(k),
            "nettside_annet", tingrettStatus);
        var seteA = await KantAsync(kanter, Strukturkanter.Ansvarsomrade, "har_sete_i", Kantnode.Virksomhet(d), Kantnode.Begrep(s), "nettside_annet");
        var sognerA = await KantAsync(kanter, Strukturkanter.Ansvarsomrade, OmraderegisterSeed.SognerTil, Kantnode.Virksomhet(d), Kantnode.Begrep(l), "nettside_annet");
        var lagdommeO = await KantAsync(kanter, Strukturkanter.Omradesammensetning, "bestar_av", Kantnode.Begrep(ld), Kantnode.Begrep(l), "nettside_annet");
        var lagmannsrettA = await KantAsync(kanter, Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(lm), Kantnode.Begrep(ld), "nettside_annet");
        return new(k, f, v, d, s, l, ld, lm, territorie, fylkeO, tingrettA, seteA, sognerA, lagdommeO, lagmannsrettA);
    }

    /// <summary>AK 1/5: hvilken tingrett og lagmannsrett hører kommunen til — med kjeden, og aldri sterkere enn det svakeste premisset.</summary>
    [Fact]
    public async Task Tilhorighet_fra_kommunen_viser_kjeden_og_svakeste_premiss()
    {
        await using var db = await DbAsync();
        var r = await RegisterAsync(db, Kode());

        var svar = await new OmradeTilhorighetTjeneste(db, new StrukturkantTjeneste(db)).ForVirksomhetAsync(r.V);

        Assert.NotNull(svar);
        Assert.Equal(r.K, svar.Territorium!.Id);
        Assert.Equal(r.Territorie, svar.TerritoriekantId);
        TilhorighetSvar S(string rubrikk) => svar.Svar.Single(x => x.Rubrikk == rubrikk);

        // Fylket: territoriet + Kartverket, begge eksternt dokumentert → avledet.
        var fylke = Assert.Single(S("fylke").Kandidater);
        Assert.Equal((r.F, "entydig", Strukturkanter.Avledet), (fylke.Node.Id, S("fylke").Status, fylke.Kunnskapsstatus));
        Assert.Equal([r.Territorie, r.FylkeO], fylke.Premisser);

        // Tingretten: tingrettkanten er ikke vurdert → svaret er uavklart, og det svakeste premisset er den kanten.
        var tingrett = Assert.Single(S("tingrett").Kandidater);
        Assert.Equal((r.D, Strukturkanter.Uavklart, r.TingrettA, (string?)null),
            (tingrett.Node.Id, tingrett.Kunnskapsstatus, tingrett.SvakestePremissId, tingrett.SvakestePremissStatus));
        Assert.Equal([r.Territorie, r.TingrettA], tingrett.Premisser);

        // Rettssted, lagsogn, lagdømme og lagmannsrett: hele kjeden kommune ← tingrett → lagsogn → lagdømme ← lagmannsrett.
        Assert.Equal([r.Territorie, r.TingrettA, r.SeteA], Assert.Single(S("rettssted").Kandidater).Premisser);
        Assert.Equal([r.Territorie, r.TingrettA, r.SognerA], Assert.Single(S("lagsogn").Kandidater).Premisser);
        Assert.Equal([r.Territorie, r.TingrettA, r.SognerA, r.LagdommeO], Assert.Single(S("lagdømme").Kandidater).Premisser);
        var lagmannsrett = Assert.Single(S("lagmannsrett").Kandidater);
        Assert.Equal(r.LM, lagmannsrett.Node.Id);
        Assert.Equal([r.Territorie, r.TingrettA, r.SognerA, r.LagdommeO, r.LagmannsrettA], lagmannsrett.Premisser);

        // Valgkretsen er ikke registrert: mangler, med hull, ingen avledning fra fylket.
        Assert.Equal("mangler", S("valgkrets").Status);
        Assert.NotNull(S("valgkrets").Hull);
        // Klassen «kommune» (#384): utledet, med territoriekanten som premiss.
        var kommune = svar.Klasser.Single(k => k.Node.Id == Klasseregler.KommuneKlasseId);
        Assert.Equal([r.Territorie], kommune.Premisser);
        Assert.Equal(Strukturkanter.Avledet, kommune.Kunnskapsstatus);
        // Premissene er med, én gang hver.
        Assert.Equal(svar.Premisser.Select(p => p.Id).Distinct().Count(), svar.Premisser.Count);
        Assert.Contains(svar.Premisser, p => p.Id == r.LagmannsrettA);

        // Ingenting lagres (#345): oppslaget skrev ingen kanter.
        Assert.False(await db.Strukturkanter.AnyAsync(k => k.FraVirksomhetId == r.V && k.TilBegrepId != r.K));
    }

    /// <summary>Svakeste premiss: et fortolket premiss gir et svar som er fortolkning, ikke avledet; tingretten er ikke svakere enn det.</summary>
    [Fact]
    public async Task Svaret_far_aldri_sterkere_status_enn_det_svakeste_premisset()
    {
        Assert.Equal(Strukturkanter.Avledet, SvakestePremiss.SvarStatus(Strukturkanter.Normalisering));
        Assert.Equal(Strukturkanter.Avledet, SvakestePremiss.SvarStatus(Strukturkanter.EksterntDokumentert));
        Assert.Equal(Strukturkanter.Fortolkning, SvakestePremiss.SvarStatus(Strukturkanter.Fortolkning));
        Assert.Equal(Strukturkanter.Uavklart, SvakestePremiss.SvarStatus(null));
        Assert.Equal(Strukturkanter.Omstridt, SvakestePremiss.SvarStatus(Strukturkanter.Omstridt));
        Assert.True(SvakestePremiss.Rang(Strukturkanter.Tekstfunn) == SvakestePremiss.Rang(Strukturkanter.EksterntDokumentert));
        Assert.True(SvakestePremiss.Rang(null) == SvakestePremiss.Rang(Strukturkanter.Uavklart));

        await using var db = await DbAsync();
        var r = await RegisterAsync(db, Kode(), tingrettStatus: Strukturkanter.Uavklart);
        var svar = await new OmradeTilhorighetTjeneste(db, new StrukturkantTjeneste(db)).ForVirksomhetAsync(r.V);
        Assert.Equal(Strukturkanter.Uavklart, svar!.Svar.Single(x => x.Rubrikk == "tingrett").Kandidater.Single().Kunnskapsstatus);
    }

    /// <summary>Uten territorium: ingen svar, og hullet sies (ingen gjetting).</summary>
    [Fact]
    public async Task Aktor_uten_territorium_far_hull_og_ingen_svar()
    {
        await using var db = await DbAsync();
        var v = await AktorAsync(db, $"Uten territorium {Kode()}");
        var svar = await new OmradeTilhorighetTjeneste(db, new StrukturkantTjeneste(db)).ForVirksomhetAsync(v);
        Assert.Null(svar!.Territorium);
        Assert.NotNull(svar.Hull);
        Assert.Empty(svar.Svar);
    }

    /// <summary>AK 4: registerkantene (og bare de) blir eksternt dokumentert etter regel, med proveniens; angre setter dem tilbake.</summary>
    [Fact]
    public async Task Registerkanter_blir_eksternt_dokumentert_med_proveniens_og_kan_angres()
    {
        await using var db = await DbAsync();
        var kanter = new StrukturkantTjeneste(db);
        var kode = Kode();
        var k = await OmradeAsync(db, Omradetyper.Kommune, kode, $"Regby{kode}");
        var f = await OmradeAsync(db, Omradetyper.Fylke, "F" + kode, $"Regfylke{kode}");
        var v = await AktorAsync(db, $"Regby{kode} kommune", kode);
        var register = await KantAsync(kanter, Strukturkanter.Omradesammensetning, "bestar_av", Kantnode.Begrep(f), Kantnode.Begrep(k));
        var vedtekter = await KantAsync(kanter, Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(v), Kantnode.Begrep(f), "vedtekter");
        var vurdert = await KantAsync(kanter, Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(v), Kantnode.Begrep(k),
            kunnskapsstatus: Strukturkanter.Uavklart);
        var tjeneste = new RegisterkantKunnskapsstatusTjeneste(db);

        var torr = await tjeneste.EtterfyllAsync(skriv: false, "test");
        Assert.False(torr.Skrevet);
        Assert.Null((await db.Strukturkanter.AsNoTracking().SingleAsync(x => x.Id == register)).Kunnskapsstatus);

        var etter = await tjeneste.EtterfyllAsync(skriv: true, "test");
        Assert.True(etter.Endret >= 1);
        await using (var les = _fixture.NyDbContext())
        {
            var rader = await les.Strukturkanter.Where(x => x.Id == register || x.Id == vedtekter || x.Id == vurdert).ToDictionaryAsync(x => x.Id);
            Assert.Equal(Strukturkanter.EksterntDokumentert, rader[register].Kunnskapsstatus);
            Assert.Equal(Strukturkanter.Godkjent, rader[register].Status); // godkjenningen endres ikke
            Assert.Null(rader[vedtekter].Kunnskapsstatus);                 // ikke register: urørt
            Assert.Equal(Strukturkanter.Uavklart, rader[vurdert].Kunnskapsstatus); // alt vurdert: aldri overskrevet
            Assert.Contains(await les.Proveniens.Where(p => p.EntitetId == register).Select(p => p.KildeReferanserJson).ToListAsync(),
                j => j != null && j.Contains(RegisterkantKunnskapsstatusTjeneste.Regel));
        }
        Assert.Equal(0, (await tjeneste.EtterfyllAsync(skriv: true, "test")).Endret); // idempotent

        var angret = await tjeneste.AngreAsync(skriv: true, "test");
        Assert.Equal(etter.Endret, angret.Endret);
        await using var les2 = _fixture.NyDbContext();
        Assert.Null((await les2.Strukturkanter.SingleAsync(x => x.Id == register)).Kunnskapsstatus);
        Assert.Equal(Strukturkanter.Uavklart, (await les2.Strukturkanter.SingleAsync(x => x.Id == vurdert)).Kunnskapsstatus);
    }

    /// <summary>AK 2: kortformen som forslag med registeret som kilde; kollisjoner listes (to kommuner med samme navn), ingenting velges.</summary>
    [Fact]
    public async Task Kortform_foreslas_per_kommune_og_kollisjoner_listes()
    {
        await using var db = await DbAsync();
        var kanter = new StrukturkantTjeneste(db);
        var (a, b) = (Kode(), Kode());
        var navn = $"Herøy{a}";
        var ka = await OmradeAsync(db, Omradetyper.Kommune, a, navn);
        var kb = await OmradeAsync(db, Omradetyper.Kommune, b, navn); // samme navn, annen kommune (Herøy/Våler)
        var va = await AktorAsync(db, $"{navn} kommune", a, "9" + a[1..]);
        var vb = await AktorAsync(db, $"{navn} kommune", b);
        await KantAsync(kanter, Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(va), Kantnode.Begrep(ka));
        await KantAsync(kanter, Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(vb), Kantnode.Begrep(kb));
        var tjeneste = new KommuneKortformTjeneste(db);

        var torr = await tjeneste.ForeslaAsync(skriv: false, "test", [a, b]);
        Assert.Equal((2, 2, 0), (torr.Kommuner, torr.Nye, torr.Fantes));
        Assert.False(await db.Begreper.AnyAsync(x => x.VirksomhetReferanseId == va && x.Navneformgrunn == "kortform"));
        // Kollisjonene: den andre kommunen (området og kortformen), men ikke kommunens eget territorium.
        Assert.Contains(torr.Kollisjoner, x => x.Kommunenummer == a && x.NodeId == kb);
        Assert.Contains(torr.Kollisjoner, x => x.Kommunenummer == a && x.NodeId == vb);
        Assert.DoesNotContain(torr.Kollisjoner, x => x.Kommunenummer == a && x.NodeId == ka);

        var skrevet = await tjeneste.ForeslaAsync(skriv: true, "test", [a, b]);
        Assert.Equal(2, skrevet.Nye);
        var kortform = await db.Begreper.AsNoTracking().SingleAsync(x => x.VirksomhetReferanseId == va && x.Navneformgrunn == "kortform");
        Assert.Equal((navn, Strukturkanter.MaskineltForeslatt, Strukturkanter.EksterntDokumentert, Strukturkanter.Register),
            (kortform.Term, kortform.Godkjenning, kortform.Kunnskapsstatus, kortform.KildeUtenforKorpusType));
        Assert.Equal(KommuneKortformTjeneste.OppdagelsesKilde, kortform.OppdagelsesKilde);

        // Kjørt på nytt: ingenting nytt.
        Assert.Equal((0, 2), ((await tjeneste.ForeslaAsync(skriv: true, "test", [a, b])).Nye, (await tjeneste.ForeslaAsync(skriv: false, "test", [a, b])).Fantes));
    }

    /// <summary>AK 2, Alta: kortformen bindes til OMRÅDET i én rettskilde; der betyr ordet området, andre steder er det flertydig (området og
    /// kommunen har samme navn) til en binding eller konteksten avgjør. En generisk navneform kan ikke bindes til et område.</summary>
    [Fact]
    public async Task Kortform_bundet_til_omradet_i_en_rettskilde_betyr_omradet_der()
    {
        await using var db = await DbAsync();
        var kanter = new StrukturkantTjeneste(db);
        var kode = Kode();
        var term = $"Altaby{kode}";
        var omrade = await OmradeAsync(db, Omradetyper.Kommune, kode, term);
        var kommune = await AktorAsync(db, $"{term} kommune", kode);
        await KantAsync(kanter, Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(kommune), Kantnode.Begrep(omrade));
        await new KommuneKortformTjeneste(db).ForeslaAsync(skriv: true, "test", [kode]);
        var kortform = await db.Begreper.SingleAsync(x => x.VirksomhetReferanseId == kommune && x.Navneformgrunn == "kortform");
        var (rettskretser, _) = await RettskildeAsync(db, $"Tingretten dekker kommunene {term} og Nabo.");
        var (annen, _) = await RettskildeAsync(db, $"{term} skal gi tilskudd.");
        var generiske = new GeneriskNavneformTjeneste(db);

        var binding = await generiske.OpprettBindingAsync(new NyNavneformBinding(kortform.Id, rettskretser, Kantnode.Begrep(omrade),
            Kunnskapsstatus: Strukturkanter.Fortolkning, Begrunnelse: "Inndelingen gjelder territoriet.", Tolkningsargumenter: ["ordlyd", "kontekst_system"],
            KildeUtenforKorpusTekst: "Test", KildeUtenforKorpusType: "nettside_annet", KildeUtenforKorpusDokumentasjon: "primaer",
            Status: Strukturkanter.FagligVurdert), "test");
        var (generisk, _) = await generiske.OpprettEllerFinnAsync($"styret{Guid.NewGuid():N}", "test");
        await Assert.ThrowsAsync<ArgumentException>(() => generiske.OpprettBindingAsync(new NyNavneformBinding(generisk.Id, rettskretser,
            Kantnode.Begrep(omrade), KildeUtenforKorpusTekst: "Test", KildeUtenforKorpusType: "nettside_annet",
            KildeUtenforKorpusDokumentasjon: "primaer"), "test"));

        var aktor = new StrukturAktor("a1", term, [], [], 1, null, null, null, null, null, null);
        async Task<(string Status, Kantnode? Node)> KlassifiserAsync(Guid rettskildeId)
        {
            var kl = new Aktorklassifiserer(db, new VirksomhetOppslagTjeneste(db), new GeneriskNavneformTjeneste(db), rettskildeId, [rettskildeId]);
            var g = await kl.GrunnlagAsync(aktor, default);
            var eid = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).Select(n => n.Eid).FirstAsync();
            var (status, node, _, _) = await kl.ForekomstAsync(g, eid, default);
            return (status, node);
        }
        Assert.Equal((Aktorstatus.Kjent, Kantnode.Begrep(omrade)), await KlassifiserAsync(rettskretser));
        Assert.Equal(Aktorstatus.Flertydig, (await KlassifiserAsync(annen)).Status); // området og kommunen: ingen gjetting

        // En avvist binding brukes ikke: da er ordet flertydig også her.
        await generiske.AvvisAsync(binding.Id, "test", "test");
        Assert.Equal(Aktorstatus.Flertydig, (await KlassifiserAsync(rettskretser)).Status);
    }

    private static async Task<(Guid Id, string Eid)> RettskildeAsync(RegelIdeDbContext db, string tekst)
    {
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testforskrift " + id,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var eid = $"https://test/{id:N}/§1/ledd-1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = id, Eid = eid, KildeId = "ledd-1", NodeType = "ledd", Tekst = tekst, Sorteringsrekkefolge = 0,
        });
        await db.SaveChangesAsync();
        return (id, eid);
    }
}
