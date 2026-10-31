using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #429 PR 2] Maskinregelen for funn om utgåtte aktører. Testene er formulert som spørsmålene i akseptansekriteriene fra fase 1
/// (CLAUDE.md §0):
/// <list type="bullet">
/// <item>AK4: Hvilke gjeldende rettskilder viser til en nedlagt aktør eller bruker et utgått navn, og hvem er etterfølgeren, med kilde
/// eller «ukjent»?</item>
/// <item>AK5: Hvor mange gjeldende rettskilder viser fortsatt til en nedlagt aktør, og er hovedetterfølgeren kjent? (I den delte basen:
/// Fiskeri- og kystdepartementet, 94 og nei.)</item>
/// <item>AK12: Lager en ny kjøring duplikater, eller gjenåpner den funn som er forklart bort? Nei på begge.</item>
/// <item>R1–R4 teller, men foreslår ikke, med juristens avgrensninger.</item>
/// </list>
/// Den delte testbasen har data fra andre tester, så hver test avgrenser kjøringen til sine egne rettskilder.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class UtgattAktorFunnregelTests(EmbeddedPostgresFixture fixture)
{
    /// <summary>Et unikt ord som slutter på «departementet» (bare bokstaver, så ordgrensene er som for ekte navn).</summary>
    private static string Departement(string forstavelse) =>
        forstavelse + new string(Guid.NewGuid().ToString("N").Select(c => (char)('a' + c % 26)).Take(10).ToArray()) + "departementet";

    private sealed class Oppsett(RegelIdeDbContext db)
    {
        public async Task<(Virksomhet V, BegrepEntitet Navneform)> AktorAsync(string term, bool nedlagt, string grunn = "gjeldende",
            string? godkjenning = Strukturkanter.MaskineltForeslatt)
        {
            var v = new Virksomhet { Id = Guid.NewGuid(), Navn = term, Livsstatus = nedlagt ? "nedlagt" : null, Aktiv = !nedlagt,
                GyldigTil = nedlagt ? new DateOnly(2013, 12, 31) : null };
            db.Virksomheter.Add(v);
            var b = NyNavneform(v.Id, term, grunn, godkjenning);
            db.Begreper.Add(b);
            await db.SaveChangesAsync();
            return (v, b);
        }

        public static BegrepEntitet NyNavneform(Guid vid, string term, string grunn, string? godkjenning = Strukturkanter.MaskineltForeslatt) => new()
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = vid, Navneformgrunn = grunn, Term = term,
            Status = "publisert", OpprettetAv = "test", Godkjenning = godkjenning,
        };

        public async Task<StrukturkantEntitet> EtterfolgerAsync(Guid fra, Guid til, string kilde, string? begrunnelse = null)
        {
            var k = new StrukturkantEntitet
            {
                Id = Guid.NewGuid(), Kategori = Strukturkanter.Relasjon, Typekode = "etterfolger", FraVirksomhetId = fra, TilVirksomhetId = til,
                Status = Strukturkanter.MaskineltForeslatt, OppdagelsesKilde = kilde, Begrunnelse = begrunnelse, OpprettetAv = "test",
                OpprettetTidspunkt = DateTimeOffset.UtcNow, KildeUtenforKorpusTekst = "Testvedtak", KildeUtenforKorpusType = "kgl_res",
                KildeUtenforKorpusDokumentasjon = "primaer",
            };
            db.Strukturkanter.Add(k);
            await db.SaveChangesAsync();
            return k;
        }

        /// <summary>En gjeldende primær forskrift med ett ledd per tekst (§ 1 første ledd, § 1 andre ledd …). Leddene kan merkes.</summary>
        public async Task<(RettskildeEntitet Rk, List<RettskildeNodeEntitet> Ledd)> ForskriftAsync(
            params string[] tekster) => await ForskriftAsync(null, tekster);

        public async Task<(RettskildeEntitet Rk, List<RettskildeNodeEntitet> Ledd)> ForskriftAsync(Action<RettskildeEntitet>? juster, params string[] tekster)
        {
            var nr = Random.Shared.Next(1_000_000, 9_999_999);
            var eli = $"https://lovdata.no/eli/forskrift/2099/01/01/{nr}/nor";
            var rk = new RettskildeEntitet
            {
                Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "primaer",
                Tittel = $"Testforskrift {nr}", Eli = eli, DokumentId = $"SF/forskrift/2099-01-01-{nr}", AknXml = "<akomaNtoso/>",
                OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            juster?.Invoke(rk);
            db.Rettskilder.Add(rk);
            var paragraf = new RettskildeNodeEntitet { Id = Guid.NewGuid(), RettskildeId = rk.Id, Eid = $"{eli}/§1", KildeId = "§1", NodeType = "paragraf" };
            db.RettskildeNoder.Add(paragraf);
            var ledd = new List<RettskildeNodeEntitet>();
            for (var i = 0; i < tekster.Length; i++)
            {
                var n = new RettskildeNodeEntitet
                {
                    Id = Guid.NewGuid(), RettskildeId = rk.Id, Eid = $"{eli}/§1/ledd-{i + 1}", KildeId = $"§1/ledd-{i + 1}", NodeType = "ledd",
                    ParentNodeId = paragraf.Id, Tekst = tekster[i], TekstHash = $"h{i}", Sorteringsrekkefolge = i,
                };
                ledd.Add(n);
                db.RettskildeNoder.Add(n);
            }
            await db.SaveChangesAsync();
            return (rk, ledd);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task AK4_Hvilke_gjeldende_rettskilder_viser_til_en_utgatt_aktor_og_hvem_er_etterfolgeren()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        // A: aktøren lever, navnet er utgått. B: nedlagt, hovedetterfølger dekket (avledet etterfølger). C: nedlagt, bare delvis
        // etterfølger lest av teksten. D: nedlagt, ingen etterfølger.
        var (levende, _) = await o.AktorAsync(Departement("Nytt"), nedlagt: false);
        var gammeltNavn = Departement("Gammelt");
        var utgatt = Oppsett.NyNavneform(levende.Id, gammeltNavn, "utgatt");
        db.Begreper.Add(utgatt);
        await db.SaveChangesAsync();
        var (b, _) = await o.AktorAsync(Departement("Bee"), nedlagt: true);
        var (c, _) = await o.AktorAsync(Departement("Cee"), nedlagt: true);
        var (d, _) = await o.AktorAsync(Departement("Dee"), nedlagt: true);
        var (ny, _) = await o.AktorAsync(Departement("Etterfolger"), nedlagt: false);
        var kantB = await o.EtterfolgerAsync(ny.Id, b.Id, "monster:livslop-etterfolger");
        var kantC = await o.EtterfolgerAsync(ny.Id, c.Id, "monster:livslop-etterfolger-tekst", "overtar ansvaret for kystsaker");

        var (rk, ledd) = await o.ForskriftAsync(
            $"Søknaden sendes til {gammeltNavn}.",
            $"{b.Navn} avgjør klagen. {b.Navn}s vedtak er endelig.",
            $"Klage går til {c.Navn}.",
            $"{d.Navn} kan gi utfyllende forskrifter.");

        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
        var torr = await regel.KjorAsync(skriv: false, "Test", [rk.Id], medRader: true);

        Assert.False(torr.Skrevet);
        Assert.Equal(4, torr.Foreslatt);
        foreach (var g in UtgattAktorFunnregel.Grupper) Assert.Equal(1, torr.ForeslattPerGruppe[g]);
        Assert.Equal(0, await db.Funn.CountAsync(f => f.EksternNokkel!.Contains(UtgattAktorFunnregel.EliKort(rk.Eli!))));

        var skrevet = await regel.KjorAsync(skriv: true, "Test", [rk.Id]);
        Assert.Equal(4, skrevet.Nye);
        var funn = await db.Funn.Include(f => f.Koblinger).Where(f => f.OppdagelsesKilde == UtgattAktorFunnregel.OppdagelsesKilde
            && f.Koblinger.Any(k => k.RettskildeId == rk.Id)).ToListAsync();
        Assert.Equal(4, funn.Count);
        Assert.All(funn, f => Assert.Equal(Funn.Oppdaget, f.Status));

        FunnEntitet For(Guid vid) => funn.Single(f => f.Koblinger.Any(k => k.Rolle == Funn.BerortAktor && k.VirksomhetId == vid));
        var fa = For(levende.Id);
        Assert.Equal("utgatt_navn", fa.Undertype);
        Assert.Contains($"nå som {levende.Navn}", fa.Beskrivelse);
        Assert.Contains(fa.Koblinger, k => k.Rolle == Funn.Navneform && k.BegrepId == utgatt.Id && k.Etikett == gammeltNavn);

        // B: én kobling per node, forankret som en tagg — ELI, eId og ordrett sitat (her uten genitiv i første forekomst).
        var fb = For(b.Id);
        Assert.Equal("nedlagt_aktor", fb.Undertype);
        var bestemmelse = Assert.Single(fb.Koblinger, k => k.Rolle == Funn.GjelderBestemmelse);
        Assert.Equal(rk.Eli, bestemmelse.RettskildeEli);
        Assert.Equal(ledd[1].Eid, bestemmelse.Eid);
        Assert.Equal(ledd[1].Id, bestemmelse.NodeId);
        Assert.Equal(b.Navn, bestemmelse.QuoteExact);
        Assert.Equal($"FOR-{rk.DokumentId!.Split('/')[^1]} § 1 andre ledd", bestemmelse.Etikett);
        Assert.Contains(fb.Koblinger, k => k.Rolle == Funn.Etterfolger && k.StrukturkantId == kantB.Id);
        Assert.Contains($"Modellen har {ny.Navn} som etterfølger", fb.Beskrivelse);

        var fc = For(c.Id);
        Assert.Contains(fc.Koblinger, k => k.Rolle == Funn.Etterfolger && k.StrukturkantId == kantC.Id);
        Assert.Contains("Hovedetterfølgeren er ukjent", fc.Beskrivelse);

        var fd = For(d.Id);
        Assert.DoesNotContain(fd.Koblinger, k => k.Rolle == Funn.Etterfolger);
        Assert.Contains("Modellen har ingen etterfølger", fd.Beskrivelse);

        // Spørsmålet over registeret: hvilke funn gjelder aktør Y?
        var t = new FunnTjeneste(db);
        Assert.Single(await t.ListeAsync(virksomhetId: d.Id));
        Assert.Equal(4, (await t.ListeAsync(kategori: Funn.UtgattAktor, rettskildeId: rk.Id)).Count);
    }

    [Fact]
    public async Task AK5_Hvor_mange_gjeldende_rettskilder_viser_til_aktoren_og_er_hovedetterfolgeren_kjent()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (fkd, _) = await o.AktorAsync(Departement("Kyst"), nedlagt: true);
        var (nfd, _) = await o.AktorAsync(Departement("Naering"), nedlagt: false);
        await o.EtterfolgerAsync(nfd.Id, fkd.Id, "monster:livslop-etterfolger-tekst", "overtar ansvaret for kyst- og miljøsaker");

        var (rk1, _) = await o.ForskriftAsync($"{fkd.Navn} kan gi forskrift.");
        var (rk2, _) = await o.ForskriftAsync($"Søknad sendes {fkd.Navn}.", $"{fkd.Navn}s avgjørelse er endelig.");
        var (rk3, _) = await o.ForskriftAsync($"Klage går til {fkd.Navn}.");
        // Telles ikke: opphevet node, merknad (#419), en rettskilde som ikke er gjeldende, et ord der navnet bare er en del.
        var (rk4, l4) = await o.ForskriftAsync($"{fkd.Navn} fører tilsyn.");
        l4[0].Opphevet = true;
        var (rk5, l5) = await o.ForskriftAsync($"Merknad: {fkd.Navn} har uttalt seg.");
        l5[0].ErMerknad = true;
        var (rk6, _) = await o.ForskriftAsync(r => r.Entitetsstatus = "erstattet", $"{fkd.Navn} avgjør.");
        var (rk7, _) = await o.ForskriftAsync($"Ny{fkd.Navn.ToLowerInvariant()} og {fkd.Navn}et er ikke navnet.");
        await db.SaveChangesAsync();

        var r = await new UtgattAktorFunnregel(db, new FunnTjeneste(db))
            .KjorAsync(skriv: false, "Test", [rk1.Id, rk2.Id, rk3.Id, rk4.Id, rk5.Id, rk6.Id, rk7.Id], medRader: true);
        var rader = r.Rader!.Where(x => x.VirksomhetId == fkd.Id).ToList();
        Assert.Equal(3, rader.Count);
        Assert.Equal(new[] { rk1.DokumentId, rk2.DokumentId, rk3.DokumentId }.Order(), rader.Select(x => x.DokumentId).Order());
        // Hovedetterfølgeren er ikke kjent: den eneste etterfølgeren er delvis (gruppe C).
        Assert.All(rader, x => Assert.Equal(UtgattAktorFunnregel.GruppeC, x.Gruppe));
        // Begge forekomstene i rk2 er med, også genitiv-s, og sitatet er ordrett.
        var rad2 = rader.Single(x => x.DokumentId == rk2.DokumentId);
        Assert.Equal(new[] { fkd.Navn, fkd.Navn + "s" }, rad2.Forekomster.Select(f => f.QuoteExact).ToArray());
    }

    [Fact]
    public async Task Organiseringsvedtak_rene_endringsvedtak_og_tvetydige_navn_telles_men_foreslas_ikke()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (nedlagt, _) = await o.AktorAsync(Departement("Org"), nedlagt: true);
        // Et navn som er navneform for to identiteter (som «Kulturdepartementet»): må avgjøres etter dato, ikke av regelen.
        var tvetydig = Departement("Tvetydig");
        var (en, _) = await o.AktorAsync(tvetydig, nedlagt: true);
        var (annen, _) = await o.AktorAsync(Departement("Annen"), nedlagt: false);
        db.Begreper.Add(Oppsett.NyNavneform(annen.Id, tvetydig, "utgatt"));
        await db.SaveChangesAsync();

        var (org, _) = await o.ForskriftAsync($"Oppgavene til {nedlagt.Navn} overføres.");
        db.RettskildeDisposisjonstyper.Add(new RettskildeDisposisjonstypeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = org.Id, Disposisjonstype = Disposisjonstyper.OverforingRessortfordeling, Kilde = "tittel",
            Regel = "test", Kunnskapsstatus = Strukturkanter.Normalisering, OpprettetAv = "test",
        });
        var (delegering, _) = await o.ForskriftAsync($"Myndighet delegeres til {nedlagt.Navn}.");
        db.RettskildeDisposisjonstyper.Add(new RettskildeDisposisjonstypeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = delegering.Id, Disposisjonstype = Disposisjonstyper.Delegering, Kilde = "tittel",
            Regel = "test", Kunnskapsstatus = Strukturkanter.Normalisering, OpprettetAv = "test",
        });
        var (rent, _) = await o.ForskriftAsync(r => r.RentEndringsvedtak = true, $"{nedlagt.Navn} skal ha melding.");
        var (tvetydigRk, _) = await o.ForskriftAsync($"{tvetydig} avgjør.");
        await db.SaveChangesAsync();

        var r = await new UtgattAktorFunnregel(db, new FunnTjeneste(db))
            .KjorAsync(skriv: false, "Test", [org.Id, delegering.Id, rent.Id, tvetydigRk.Id], medRader: true);
        Assert.Equal(1, r.NodetreffOrganiseringsvedtak);
        Assert.Equal(1, r.NodetreffRentEndringsvedtak);
        Assert.Equal(1, r.NodetreffTvetydigeNavn);
        Assert.Equal(1, r.ParTvetydigeNavn);
        Assert.Contains(tvetydig, r.TvetydigeNavn);
        // Delegeringsvedtak er ikke organiseringsvedtak i denne regelen: der står funnene (#429 fase 1).
        var rad = Assert.Single(r.Rader!);
        Assert.Equal(delegering.DokumentId, rad.DokumentId);
        Assert.DoesNotContain(r.Rader!, x => x.VirksomhetId == en.Id);
    }

    [Fact]
    public async Task Endringsvedtak_med_ukjent_renhet_foreslas_og_merkes_med_malvedtaket_naar_det_er_entydig()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (nedlagt, _) = await o.AktorAsync(Departement("Endr"), nedlagt: true);
        RettskildeEntitet Stub(string tittel) => new()
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse", Tittel = tittel,
            Eli = $"https://lovdata.no/eli/forskrift/1990/01/01/{Random.Shared.Next(1_000_000, 9_999_999)}/nor", OpprettetAv = "test",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        var mal = Stub("Forskrift om fiske");
        var mal2 = Stub("Forskrift om fangst");
        db.Rettskilder.AddRange(mal, mal2);
        // rent_endringsvedtak er NULL (ukjent) på begge — som for 755 av 809 LTI i den delte basen.
        var (en, _) = await o.ForskriftAsync($"I forskrift om fiske skal {nedlagt.Navn} ha melding.");
        var (to, _) = await o.ForskriftAsync($"{nedlagt.Navn} avgjør.");
        db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = en.Id, EndringEid = mal.Eli!, EndringRettskildeId = mal.Id });
        db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = to.Id, EndringEid = mal.Eli!, EndringRettskildeId = mal.Id });
        db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = to.Id, EndringEid = mal2.Eli!, EndringRettskildeId = mal2.Id, Sorteringsrekkefolge = 1 });
        await db.SaveChangesAsync();

        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
        var r = await regel.KjorAsync(skriv: true, "Test", [en.Id, to.Id], medRader: true);
        Assert.Equal(2, r.Foreslatt);
        Assert.Equal(2, r.ForeslattIEndringsvedtak);
        Assert.Equal(1, r.ForeslattMedMalvedtak);

        var funn = await db.Funn.Include(f => f.Koblinger).Where(f => f.OppdagelsesKilde == UtgattAktorFunnregel.OppdagelsesKilde
            && f.Koblinger.Any(k => k.Rolle == Funn.BerortAktor && k.VirksomhetId == nedlagt.Id)).ToListAsync();
        var fEn = funn.Single(f => f.Koblinger.Any(k => k.RettskildeId == en.Id));
        var malKobling = Assert.Single(fEn.Koblinger, k => k.Rolle == Funn.Malvedtak);
        Assert.Equal(mal.Eli, malKobling.RettskildeEli);
        Assert.Contains("endrer «Forskrift om fiske»", fEn.Beskrivelse);
        var fTo = funn.Single(f => f.Koblinger.Any(k => k.RettskildeId == to.Id));
        Assert.DoesNotContain(fTo.Koblinger, k => k.Rolle == Funn.Malvedtak);
        Assert.Contains("målvedtaket er ikke entydig", fTo.Beskrivelse);
        // Målvedtaket er ikke «funn i rettskilde X».
        Assert.DoesNotContain(await new FunnTjeneste(db).ListeAsync(rettskildeId: mal.Id), f => f.Id == fEn.Id);
    }

    [Fact]
    public async Task AK12_En_ny_kjoring_lager_ingen_duplikater_og_gjenapner_ikke_funn_som_er_forklart_bort()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(Departement("Idem"), nedlagt: true);
        var (rk1, _) = await o.ForskriftAsync($"{a.Navn} avgjør.");
        var (rk2, _) = await o.ForskriftAsync($"Klage til {a.Navn}.");
        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));

        var forste = await regel.KjorAsync(skriv: true, "Test", [rk1.Id, rk2.Id]);
        Assert.Equal(2, forste.Nye);
        var t = new FunnTjeneste(db);
        var bort = (await t.ListeAsync(rettskildeId: rk1.Id)).Single();
        await t.ForklarBortAsync(bort.Id, "Forskriften er opphevet ved kgl.res. (test).", "Jurist");

        var andre = await regel.KjorAsync(skriv: true, "Test", [rk1.Id, rk2.Id]);
        Assert.Equal(0, andre.Nye);
        Assert.Equal(2, andre.Finnes);
        Assert.Equal(1, andre.FinnesForklartBort);
        Assert.Equal(2, await db.Funn.CountAsync(f => f.Koblinger.Any(k => k.Rolle == Funn.BerortAktor && k.VirksomhetId == a.Id)));
        Assert.Equal(Funn.ForklartBort, (await db.Funn.AsNoTracking().SingleAsync(f => f.Id == bort.Id)).Status);
    }

    [Fact]
    public async Task R1_til_R4_teller_men_foreslar_ikke_og_ett_treff_uten_regel_er_nok_til_at_paret_foreslas()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(Departement("Regel"), nedlagt: true);
        var (alleTruffet, _) = await o.ForskriftAsync(
            $"I § 2 endres {a.Navn} til Nærings- og fiskeridepartementet.",
            $"Forskriften er fastsatt av {a.Navn} 1. januar 2004.");
        var (blandet, _) = await o.ForskriftAsync(
            $"(tidligere {a.Navn})",
            $"{a.Navn} avgjør klagen.");
        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));

        var r = await regel.KjorAsync(skriv: false, "Test", [alleTruffet.Id, blandet.Id], medRader: true);
        Assert.Equal(1, r.IkkeForeslatt);
        Assert.Equal(1, r.IkkeForeslattPerRegel[UtgattAktorRegler.R1]);
        Assert.Equal(1, r.IkkeForeslattPerRegel[UtgattAktorRegler.R2]);
        Assert.Equal(0, r.IkkeForeslattPerRegel[UtgattAktorRegler.R4]);
        Assert.Equal(1, r.NoderPerRegel[UtgattAktorRegler.R4]);
        Assert.False(r.Rader!.Single(x => x.DokumentId == alleTruffet.DokumentId).Foreslatt);
        Assert.True(r.Rader!.Single(x => x.DokumentId == blandet.DokumentId).Foreslatt);

        await regel.KjorAsync(skriv: true, "Test", [alleTruffet.Id, blandet.Id]);
        var t = new FunnTjeneste(db);
        Assert.Empty(await t.ListeAsync(rettskildeId: alleTruffet.Id));
        var f = Assert.Single(await t.ListeAsync(rettskildeId: blandet.Id));
        Assert.Contains("(R4)", f.InternMerknad);
    }

    [Theory]
    // R1: endringsinstruks.
    [InlineData("I § 3 endres Fiskeridepartementet til Nærings- og fiskeridepartementet.", null, "R1")]
    [InlineData("Fiskeridepartementet endres til Nærings- og fiskeridepartementet i hele forskriften.", null, "R1")]
    [InlineData("I § 4 endres «departementet» til Fiskeridepartementet.", null, "R1")]
    // R2: identifikasjon av et datert dokument.
    [InlineData("Forskrift fastsatt av Fiskeridepartementet 12. mars 1990 oppheves ikke.", null, "R2")]
    [InlineData("Ordningen er godkjent av Fiskeridepartementet (nå NFD) den 3. mai 2001.", null, "R2")]
    [InlineData("Instruks datert Fiskeridepartementet 1. juni 1999.", null, "R2")]
    [InlineData("Fiskeridepartementets forskrift av 2. januar 1990 gjelder.", null, "R2")]
    // R3: opphevingsformel — termen er det som oppheves.
    [InlineData("Forskrift 1. januar 1990 nr. 1 om Fiskeridepartementets kontor.", "Følgende forskrifter oppheves:", "R3")]
    [InlineData("Fiskeridepartementets delegeringsvedtak oppheves.", null, "R3")]
    [InlineData("Samtidig oppheves forskrift om tilskudd til Fiskeridepartementet.", null, "R3")]
    // R4: historisk markør rett foran.
    [InlineData("Saken avgjøres av Nærings- og fiskeridepartementet (tidligere Fiskeridepartementet).", null, "R4")]
    [InlineData("Saken avgjøres av (tidligere) Fiskeridepartementet.", null, "R4")]
    public void Reglene_slar_til_paa_juristens_former(string tekst, string? forelder, string regel) =>
        Assert.Contains(regel, UtgattAktorRegler.Treff("Fiskeridepartementet", tekst, forelder));

    [Theory]
    // Gjeldende omtale: ingen regel.
    [InlineData("Fiskeridepartementet avgjør klagen.", null)]
    [InlineData("Fiskeridepartementet kan gi forskrift.", null)]
    // [LÅST, juristens avgrensning av R3] Termen som den som opphever («oppheves av X») er ikke en opphevingsformel for X.
    [InlineData("Vedtaket kan oppheves av Fiskeridepartementet.", null)]
    // R3 krever «oppheves» i SAMME setning; en annen setning teller ikke.
    [InlineData("Forskrift om tilskudd oppheves. Fiskeridepartementet fører tilsyn.", null)]
    // [LÅST, juristens avgrensning av R4] «tidligere» må stå rett foran termen.
    [InlineData("Fiskeridepartementet har tidligere avgjort slike saker.", null)]
    [InlineData("Som tidligere nevnt avgjør Fiskeridepartementet klagen.", null)]
    // R2 krever datoen; «fastsatt av X» alene er en gjeldende kompetanseangivelse.
    [InlineData("Forskrift fastsatt av Fiskeridepartementet.", null)]
    public void Reglene_slar_ikke_til_utenfor_avgrensningen(string tekst, string? forelder) =>
        Assert.Empty(UtgattAktorRegler.Treff("Fiskeridepartementet", tekst, forelder));

    [Fact]
    public async Task Premissporten_for_etterfolger_gjelder_ogsaa_navneendring_gruppe_A()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (levende, _) = await o.AktorAsync(Departement("Levende"), nedlagt: false);
        var gammelt = Departement("Gammelt");
        var utgatt = Oppsett.NyNavneform(levende.Id, gammelt, "utgatt", Strukturkanter.FagligVurdert);
        db.Begreper.Add(utgatt);
        await db.SaveChangesAsync();
        var (rk, _) = await o.ForskriftAsync($"{gammelt} gir tillatelse.");
        await new UtgattAktorFunnregel(db, new FunnTjeneste(db)).KjorAsync(skriv: true, "Test", [rk.Id]);
        var t = new FunnTjeneste(db);
        var f = Assert.Single(await t.ListeAsync(rettskildeId: rk.Id));
        Assert.Equal("utgatt_navn", f.Undertype);

        // Navneformen er faglig vurdert, men hintet «aktøren heter nå X» er ikke: porten stopper.
        await t.JuristvurderAsync(f.Id, "Navnet er utgått.", Funn.KiAgent, "agent",
            [new NyttVurderingspunkt(Funn.BortforklaringProvd, "ingen endring som retter navnet.")]);
        var mangler = await t.PremissmanglerAsync((await t.HentAsync(f.Id))!);
        Assert.Single(mangler);
        Assert.StartsWith("Etterfølger ikke vurdert", mangler[0]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.GodkjennForPubliseringAsync(f.Id, "X"));

        await t.JuristvurderAsync(f.Id, "Navnet er utgått.", Funn.KiAgent, "agent",
        [
            new NyttVurderingspunkt(Funn.BortforklaringProvd, "ingen endring som retter navnet."),
            new NyttVurderingspunkt(Funn.Vurdering, $"{levende.Navn}. Oppgaven er ikke flyttet.", Funn.EtterfolgerOverskrift),
        ]);
        Assert.Empty(await t.PremissmanglerAsync((await t.HentAsync(f.Id))!));
        Assert.Equal(Funn.GodkjentForPublisering, (await t.GodkjennForPubliseringAsync(f.Id, "X"))!.Status);
    }
}
