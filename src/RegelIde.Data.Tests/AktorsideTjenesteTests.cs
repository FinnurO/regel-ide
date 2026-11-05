using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #441] Den samlede aktørsiden. Akseptansekriteriene som spørsmål modellen skal kunne besvare:
/// <list type="number">
/// <item>«Hva er hjemmelen for at aktøren finnes?» — R oppretter først, med sitat, kunnskapsstatus og godkjenning; «Godkjenn hjemmelen» godkjenner
/// navneformen og R oppretter i én transaksjon og setter <c>hjemmel_art</c>.</item>
/// <item>Uten hjemmel: hullet, med «mest omtalt i» bare av direkte utsagn og taggede forekomster (ikke fritekst, ikke via klasse).</item>
/// <item>Én rad per rettskilde, tellerne summerer til alle kantene uten dobbelttelling, og hjemmelsvedtaket gjentas ikke.</item>
/// <item>«Godkjenn alle N» i en rad godkjenner bare aktørens egne utsagn.</item>
/// <item>Direkte utsagn og utsagn via klasse står hver for seg, med svakeste premiss.</item>
/// <item>«Ansvarlig departement» er aldri et utsagn.</item>
/// </list>
/// Testbasen er delt: navn er unike per test, og påstandene gjelder bare testens egne rader. KD- og Karasjok-tallene er målt i kopien av den
/// delte basen og står i PR-en; her er de bygget opp i det små.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AktorsideTjenesteTests(EmbeddedPostgresFixture fixture)
{
    private static string Unik(string prefiks) => $"{prefiks} {Guid.NewGuid():N}";

    private sealed record Kilde(Guid Id, string Eid1, string Eid2);

    /// <summary>En liten rettskilde med to ledd. Teksten i ledd 2 kan nevne aktøren som fritekst.</summary>
    private static async Task<Kilde> KildeAsync(RegelIdeDbContext db, string tittel, string tekst1, string tekst2, List<string>? ansvarlig = null)
    {
        var r = new RettskildeEntitet { Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Forskrift", Tittel = Unik(tittel), AnsvarligDepartement = ansvarlig, Status = "Gjeldende", OpprettetAv = "test", AknXml = "<akomaNtoso/>" };
        db.Rettskilder.Add(r);
        var eli = $"https://lovdata.no/eli/forskrift/test/{r.Id:N}/nor";
        db.RettskildeNoder.AddRange(
            new RettskildeNodeEntitet { Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = $"{eli}/punkt-1", KildeId = "p1", NodeType = "punkt", Nummer = "1", Tekst = tekst1, Sorteringsrekkefolge = 1 },
            new RettskildeNodeEntitet { Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = $"{eli}/punkt-2", KildeId = "p2", NodeType = "punkt", Nummer = "2", Tekst = tekst2, Sorteringsrekkefolge = 2 });
        await db.SaveChangesAsync();
        return new Kilde(r.Id, $"{eli}/punkt-1", $"{eli}/punkt-2");
    }

    private static async Task<Virksomhet> AktorAsync(RegelIdeDbContext db, string navn, string? aktortype = null)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik(navn), Aktortype = aktortype };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    private static async Task<BegrepEntitet> NavneformAsync(RegelIdeDbContext db, Virksomhet v, Kilde? k, string? godkjenning = Strukturkanter.MaskineltForeslatt,
        string grunn = "gjeldende", string? hjemmelArt = null)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = v.Id, Navneformgrunn = grunn, Term = v.Navn,
            LovkildeId = k?.Id, LovreferanseEid = k?.Eid1, Kunnskapsstatus = k is null ? null : Strukturkanter.Tekstfunn, Godkjenning = godkjenning,
            HjemmelArt = hjemmelArt, OppdagelsesKilde = "monster:test", Status = "publisert", OpprettetAv = "test",
        };
        db.Begreper.Add(b);
        await db.SaveChangesAsync();
        return b;
    }

    private static async Task<Guid> KantAsync(StrukturkantTjeneste t, string kat, string type, Kantnode fra, Kantnode? til, Kilde k, string eid,
        string status = Strukturkanter.MaskineltForeslatt, string? ks = Strukturkanter.Normalisering, string? objekt = null) =>
        (await t.OpprettAsync(new NyStrukturkant(kat, type, fra, til, HjemmelRettskildeId: k.Id, HjemmelEid: eid, Objekt: objekt, Status: status,
            Kunnskapsstatus: ks, OppdagelsesKilde: "monster:test"), "test")).Kant.Id;

    private AktorsideTjeneste Tjeneste(RegelIdeDbContext db) => new(db, new StrukturkantTjeneste(db),
        new AktoridentitetTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new VirksomhetOppslagTjeneste(db)));

    private async Task<(RegelIdeDbContext Db, Virksomhet Kongen)> OppsettAsync()
    {
        var db = fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        return (db, await AktorAsync(db, "Kongen i statsråd"));
    }

    [Fact]
    public async Task AK1_hjemmelen_er_oppretterkanten_med_sitat_og_status_og_godkjennes_i_en_transaksjon()
    {
        var (db, kongen) = await OppsettAsync();
        await using var _ = db;
        var t = new StrukturkantTjeneste(db);
        var vedtak = await KildeAsync(db, "Omorganisering av departementsstrukturen", "Opprettelse av Testdepartementet.", "Nedleggelse av Gammeldepartementet.");
        var dep = await AktorAsync(db, "Testdepartementet");
        var navneform = await NavneformAsync(db, dep, vedtak, hjemmelArt: null);
        var r = await KantAsync(t, Strukturkanter.Relasjon, "oppretter", Kantnode.Virksomhet(kongen.Id), Kantnode.Virksomhet(dep.Id), vedtak, vedtak.Eid1);

        var h = await Tjeneste(db).HjemmelAsync(dep.Id);
        Assert.NotNull(h);
        Assert.Equal(("hjemlet", Hjemmelkilder.Oppretterkant, vedtak.Id, vedtak.Eid1), (h.Status, h.Kilde, h.RettskildeId, h.Eid));
        Assert.Equal("Opprettelse av Testdepartementet.", h.Sitat);
        Assert.Equal((Strukturkanter.Tekstfunn, Strukturkanter.MaskineltForeslatt), (h.Navneform!.Kunnskapsstatus, h.Navneform.Godkjenning));
        Assert.Equal((r, Strukturkanter.Normalisering, Strukturkanter.MaskineltForeslatt), (h.Oppretterkant!.Id, h.Oppretterkant.Kunnskapsstatus, h.Oppretterkant.Status));
        Assert.Equal(("oppretting", false, true), (h.HjemmelArtForslag, h.MaVelgeHjemmelArt, h.Venter));

        var kvittering = await Tjeneste(db).GodkjennHjemmelAsync(dep.Id, null, "Kari Jurist");
        Assert.Equal(new HjemmelGodkjent(navneform.Id, true, r, true, "oppretting"), kvittering);
        await using var les = fixture.NyDbContext();
        var b = await les.Begreper.SingleAsync(x => x.Id == navneform.Id);
        Assert.Equal((Strukturkanter.Godkjent, "oppretting"), (b.Godkjenning, b.HjemmelArt));
        Assert.Equal(Strukturkanter.Godkjent, (await les.Strukturkanter.SingleAsync(k => k.Id == r)).Status);
        await Assert.ThrowsAsync<ArgumentException>(() => Tjeneste(les).GodkjennHjemmelAsync(dep.Id, null, "Kari Jurist"));
    }

    [Fact]
    public async Task AK1_feiler_et_steg_rulles_alt_tilbake()
    {
        var (db, kongen) = await OppsettAsync();
        await using var _ = db;
        var t = new StrukturkantTjeneste(db);
        var vedtak = await KildeAsync(db, "Opprettelsesvedtak", "Opprettelse av X.", "Merknad: X opprettes.");
        var dep = await AktorAsync(db, "Rullbakdepartementet");
        var forslag = await NavneformAsync(db, dep, vedtak);
        var r = await KantAsync(t, Strukturkanter.Relasjon, "oppretter", Kantnode.Virksomhet(kongen.Id), Kantnode.Virksomhet(dep.Id), vedtak, vedtak.Eid2);
        // Kanten står i en merknad (#419) og kan ikke godkjennes. Navneformen godkjennes først i transaksjonen; feilen etterpå skal rulle den tilbake.
        (await db.RettskildeNoder.SingleAsync(n => n.RettskildeId == vedtak.Id && n.Eid == vedtak.Eid2)).ErMerknad = true;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => Tjeneste(db).GodkjennHjemmelAsync(dep.Id, null, "Kari Jurist"));
        await using var les = fixture.NyDbContext();
        Assert.Equal(Strukturkanter.MaskineltForeslatt, (await les.Strukturkanter.SingleAsync(k => k.Id == r)).Status);
        var b = await les.Begreper.SingleAsync(x => x.Id == forslag.Id);
        Assert.Equal((Strukturkanter.MaskineltForeslatt, (string?)null), (b.Godkjenning, b.HjemmelArt));
    }

    [Fact]
    public async Task AK1_disposisjonstypen_avgjor_ikke_alltid_hjemmel_art_og_da_maa_et_menneske_velge()
    {
        var (db, _) = await OppsettAsync();
        await using var __ = db;
        var vedtak = await KildeAsync(db, "Omorganisering", "Fiskeridepartementets navn endres til Testkystdepartementet.", "-");
        db.RettskildeDisposisjonstyper.AddRange(
            new RettskildeDisposisjonstypeEntitet { Id = Guid.NewGuid(), RettskildeId = vedtak.Id, Disposisjonstype = Disposisjonstyper.Oppretting, Kilde = Disposisjonstyper.KildeTittel, Regel = "test", Treff = "x", Kunnskapsstatus = Strukturkanter.Normalisering, OpprettetAv = "test" },
            new RettskildeDisposisjonstypeEntitet { Id = Guid.NewGuid(), RettskildeId = vedtak.Id, Disposisjonstype = Disposisjonstyper.Navneendring, Kilde = Disposisjonstyper.KildeTittel, Regel = "test", Treff = "y", Kunnskapsstatus = Strukturkanter.Normalisering, OpprettetAv = "test" });
        await db.SaveChangesAsync();
        var dep = await AktorAsync(db, "Testkystdepartementet");
        var navneform = await NavneformAsync(db, dep, vedtak);

        var h = await Tjeneste(db).HjemmelAsync(dep.Id);
        Assert.Equal((Hjemmelkilder.Disposisjonstype, (string?)null, true), (h!.Kilde, h.HjemmelArtForslag, h.MaVelgeHjemmelArt));
        await Assert.ThrowsAsync<ArgumentException>(() => Tjeneste(db).GodkjennHjemmelAsync(dep.Id, null, "Kari Jurist"));
        var k = await Tjeneste(db).GodkjennHjemmelAsync(dep.Id, "navnevedtak", "Kari Jurist");
        Assert.Equal("navnevedtak", k!.HjemmelArt);
        await using var les = fixture.NyDbContext();
        Assert.Equal("navnevedtak", (await les.Begreper.SingleAsync(x => x.Id == navneform.Id)).HjemmelArt);
    }

    [Fact]
    public async Task AK2_uten_hjemmel_vises_hullet_og_mest_omtalt_regnes_bare_av_utsagn_og_tagger()
    {
        var (db, _) = await OppsettAsync();
        await using var __ = db;
        var t = new StrukturkantTjeneste(db);
        var kommune = await AktorAsync(db, "Testjok kommune", "rettssubjekt");
        var navneform = await NavneformAsync(db, kommune, null, godkjenning: Strukturkanter.Godkjent);
        var klasse = new BegrepEntitet { Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Klasse, Term = Unik("språkutviklingskommuner"), Status = "publisert", OpprettetAv = "test" };
        var stor = new BegrepEntitet { Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Klasse, Term = Unik("barnehagekommune"), Status = "publisert", OpprettetAv = "test" };
        db.Begreper.AddRange(klasse, stor);
        await db.SaveChangesAsync();

        // Samiske språk: ett direkte utsagn og én tagget forekomst.
        var samisk = await KildeAsync(db, "Forskrift om forvaltningsområdet for samiske språk", $"{kommune.Navn} er språkutviklingskommune.", "-");
        await KantAsync(t, Strukturkanter.Medlemskap, "medlem_av", Kantnode.Virksomhet(kommune.Id), Kantnode.Begrep(klasse.Id), samisk, samisk.Eid1,
            Strukturkanter.Godkjent, Strukturkanter.Tekstfunn);
        db.TekstTagger.Add(new TekstTaggEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = samisk.Id, NodeEid = samisk.Eid1, StartOffset = 0, EndOffset = kommune.Navn.Length, QuotePrefix = "",
            QuoteExact = kommune.Navn, QuoteSuffix = "", NodeTekstHash = "x", Kind = "virksomhet", RefId = navneform.Id, OpprettetAv = "test", VirksomhetId = kommune.Id,
        });
        // Politidistrikt: navnet står tre ganger som fritekst (et stedsnavn), uten tagg og uten utsagn.
        await KildeAsync(db, "Politidistriktforskriften", $"{kommune.Navn}, {kommune.Navn} og {kommune.Navn}.", $"{kommune.Navn}.");
        // Barnehageloven: tre kompetanser på en klasse kommunen er medlem av — via klasse, teller ikke.
        var bhl = await KildeAsync(db, "Barnehageloven", "Kommunen skal ...", "Kommunen kan ...");
        await KantAsync(t, Strukturkanter.Medlemskap, "medlem_av", Kantnode.Virksomhet(kommune.Id), Kantnode.Begrep(stor.Id), samisk, samisk.Eid2,
            Strukturkanter.Godkjent, Strukturkanter.Tekstfunn);
        foreach (var o in new[] { "a", "b", "c" })
        {
            await KantAsync(t, Strukturkanter.Kompetanse, "tilsyn", Kantnode.Begrep(stor.Id), null, bhl, bhl.Eid1, objekt: o);
        }

        var h = await Tjeneste(db).HjemmelAsync(kommune.Id);
        Assert.Equal(("mangler", AktorkoTjeneste.Hullmerknad), (h!.Status, h.Hullmerknad));
        var mest = Assert.Single(h.MestOmtalt);
        Assert.Equal((samisk.Id, 2, 1), (mest.RettskildeId, mest.Utsagn, mest.TaggedeForekomster));
        Assert.Equal(samisk.Eid1, mest.Eid);
        Assert.StartsWith(kommune.Navn, mest.Sitat);
        await Assert.ThrowsAsync<ArgumentException>(() => Tjeneste(db).GodkjennHjemmelAsync(kommune.Id, null, "Kari Jurist"));
    }

    [Fact]
    public async Task AK3_en_rad_per_rettskilde_tellerne_summerer_uten_dobbelttelling_og_hjemmelsvedtaket_gjentas_ikke()
    {
        var (db, kongen) = await OppsettAsync();
        await using var __ = db;
        var t = new StrukturkantTjeneste(db);
        var dep = await AktorAsync(db, "Telledepartementet");
        var vedtak = await KildeAsync(db, "Opprettelsesvedtak", "Opprettelse av Telledepartementet.", "Ressortfordeling.");
        var lov = await KildeAsync(db, "Lov om barnehager", "Departementet kan gi forskrift.", "Departementet fører tilsyn.", [dep.Navn]);
        var bare = await KildeAsync(db, "Forskrift med bare ansvarlig departement", "-", "-", [dep.Navn]);
        await NavneformAsync(db, dep, vedtak);
        await KantAsync(t, Strukturkanter.Relasjon, "oppretter", Kantnode.Virksomhet(kongen.Id), Kantnode.Virksomhet(dep.Id), vedtak, vedtak.Eid1);
        await KantAsync(t, Strukturkanter.Oppgave, "forvaltning", Kantnode.Virksomhet(dep.Id), null, vedtak, vedtak.Eid2, objekt: "barnehageloven");
        await KantAsync(t, Strukturkanter.Kompetanse, "tilsyn", Kantnode.Virksomhet(dep.Id), null, lov, lov.Eid1, objekt: "forskrift");
        await KantAsync(t, Strukturkanter.Kompetanse, "tilsyn", Kantnode.Virksomhet(dep.Id), null, lov, lov.Eid2, objekt: "barnehager");
        // Et utsagn der aktøren er motpart (til) teller også, i samme rad.
        await KantAsync(t, Strukturkanter.Relasjon, "har_delegert_til", Kantnode.Virksomhet(kongen.Id), Kantnode.Virksomhet(dep.Id), lov, lov.Eid1);

        var svar = await Tjeneste(db).RettskilderAsync(dep.Id);
        Assert.NotNull(svar);
        Assert.Equal(vedtak.Id, svar.HjemmelRettskildeId);
        Assert.DoesNotContain(svar.Rader, r => r.RettskildeId == vedtak.Id);
        Assert.Equal(svar.Rader.Count, svar.Rader.Select(r => (r.Gruppe, r.RettskildeId)).Distinct().Count());
        Assert.Equal([lov.Id, bare.Id], svar.Rader.Select(r => r.RettskildeId!.Value));
        var lovrad = svar.Rader[0];
        Assert.Equal((2, 0, 0, 1, 3), (lovrad.Kompetanse, lovrad.Plikt, lovrad.Oppgave, lovrad.Annet, lovrad.Venter));
        Assert.Equal((5, 3, 2), (svar.Tall.Kanter, svar.Tall.KanterIRadene, svar.Tall.KanterIHjemmelen));
        Assert.Equal(svar.Tall.Kanter, svar.Rader.Sum(r => r.Kompetanse + r.Plikt + r.Oppgave + r.Annet) + svar.Tall.KanterIHjemmelen);
        var h = await Tjeneste(db).HjemmelAsync(dep.Id);
        Assert.Equal(2, h!.UtsagnIKilden.Count);
    }

    [Fact]
    public async Task AK4_godkjenn_alle_i_raden_godkjenner_bare_aktorens_egne_utsagn()
    {
        var (db, _) = await OppsettAsync();
        await using var __ = db;
        var t = new StrukturkantTjeneste(db);
        var dep = await AktorAsync(db, "Godkjenndepartementet");
        var annen = await AktorAsync(db, "Annetdirektoratet");
        var lov = await KildeAsync(db, "Lov", "Departementet kan gi forskrift.", "Direktoratet fører tilsyn.");
        var a = await KantAsync(t, Strukturkanter.Kompetanse, "tilsyn", Kantnode.Virksomhet(dep.Id), null, lov, lov.Eid1, objekt: "forskrift");
        var b = await KantAsync(t, Strukturkanter.Kompetanse, "tilsyn", Kantnode.Virksomhet(dep.Id), null, lov, lov.Eid2, objekt: "x");
        var c = await KantAsync(t, Strukturkanter.Kompetanse, "tilsyn", Kantnode.Virksomhet(annen.Id), null, lov, lov.Eid2, objekt: "y");

        Assert.Equal((2, 0), await Tjeneste(db).GodkjennAlleIRettskildenAsync(dep.Id, lov.Id, "Kari Jurist"));
        await using var les = fixture.NyDbContext();
        var status = await les.Strukturkanter.Where(k => k.Id == a || k.Id == b || k.Id == c).ToDictionaryAsync(k => k.Id, k => k.Status);
        Assert.Equal((Strukturkanter.Godkjent, Strukturkanter.Godkjent, Strukturkanter.MaskineltForeslatt), (status[a], status[b], status[c]));
    }

    [Fact]
    public async Task AK5_direkte_utsagn_og_utsagn_via_klasse_staar_hver_for_seg_med_svakeste_premiss()
    {
        var (db, _) = await OppsettAsync();
        await using var __ = db;
        var t = new StrukturkantTjeneste(db);
        var kommune = await AktorAsync(db, "Klasseby kommune", "rettssubjekt");
        var klasse = new BegrepEntitet { Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Klasse, Term = Unik("barnehagekommune"), Status = "publisert", OpprettetAv = "test" };
        db.Begreper.Add(klasse);
        await db.SaveChangesAsync();
        var lov = await KildeAsync(db, "Barnehageloven", "Kommunen er medlem.", "Kommunen kan godkjenne.");
        var medlem = await KantAsync(t, Strukturkanter.Medlemskap, "medlem_av", Kantnode.Virksomhet(kommune.Id), Kantnode.Begrep(klasse.Id), lov, lov.Eid1,
            Strukturkanter.Godkjent, Strukturkanter.Tekstfunn);
        var k = await KantAsync(t, Strukturkanter.Kompetanse, "godkjenning", Kantnode.Begrep(klasse.Id), null, lov, lov.Eid2, ks: null, objekt: "barnehager");

        var svar = await Tjeneste(db).RettskilderAsync(kommune.Id);
        var direkte = Assert.Single(svar!.Rader, r => r.Gruppe == "direkte");
        var via = Assert.Single(svar.Rader, r => r.Gruppe == "via_klasse");
        Assert.Equal((lov.Id, lov.Id), (direkte.RettskildeId, via.RettskildeId));
        Assert.Equal((0, 1), (direkte.Kompetanse, direkte.Annet));
        Assert.Equal([medlem], direkte.Utsagn.Select(u => u.Id));
        var treff = Assert.Single(via.ViaKlasse);
        Assert.Equal((k, 1, 1), (treff.Kant.Id, via.Kompetanse, via.Venter));
        // K-kanten er ikke vurdert: svaret er uavklart, og den er det svakeste premisset.
        Assert.Equal((Strukturkanter.Uavklart, k, true), (treff.Kunnskapsstatus, treff.SvakestePremissId, treff.Forslag));
        Assert.Equal([klasse.Id], treff.Via.Select(n => n.Id));
        // Utsagnet via klassen er ikke et direkte utsagn: kontrollsummen gjelder bare de direkte.
        Assert.Equal((1, 1), (svar.Tall.Kanter, svar.Tall.KanterIRadene));
    }

    [Fact]
    public async Task AK6_ansvarlig_departement_er_et_merke_og_aldri_et_utsagn()
    {
        var (db, _) = await OppsettAsync();
        await using var __ = db;
        var dep = await AktorAsync(db, "Merkedepartementet");
        var kilde = await KildeAsync(db, "Forskrift", "-", "-", [dep.Navn]);

        var svar = await Tjeneste(db).RettskilderAsync(dep.Id);
        var rad = Assert.Single(svar!.Rader);
        Assert.Equal((kilde.Id, true), (rad.RettskildeId, rad.AnsvarligDepartement));
        Assert.Equal((0, 0, 0, 0, 0), (rad.Kompetanse, rad.Plikt, rad.Oppgave, rad.Annet, rad.Venter));
        Assert.Empty(rad.Utsagn);
        Assert.Equal((0, 0, 1), (svar.Tall.Kanter, svar.Tall.MedUtsagn, svar.Tall.AnsvarligDepartement));
        Assert.False(await db.Strukturkanter.AnyAsync(k => k.FraVirksomhetId == dep.Id || k.TilVirksomhetId == dep.Id));
    }
}
