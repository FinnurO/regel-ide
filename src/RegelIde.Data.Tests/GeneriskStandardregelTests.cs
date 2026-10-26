using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #394] Standardreglene for generiske navneformer. Spørsmålene modellen skal kunne besvare etterpå:
/// hvilket departement er «departementet» i lov X på dato D — og bygger svaret på en vurdert ressortfordeling (primær) eller Lovdatas felt
/// (sekundær)? Brukes et ressortforslag som ikke er vurdert? Går en manuell (også faglig vurdert) binding foran regelen? Sier regelen
/// «ikke avklart» når kilden selv sier hva ordet betyr? Og blir den gamle navneformen erstattet når et forslag med ny hjemmel godkjennes (AK 9)?
/// <para>Rettskildene lages med unik ELI per test, fordi testsamlingen deler database.</para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class GeneriskStandardregelTests(EmbeddedPostgresFixture fixture)
{
    private static async Task<Virksomhet> NyVirksomhetAsync(RegelIdeDbContext db, string navn, string? livsstatus = null, DateOnly? gyldigTil = null)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = navn, Livsstatus = livsstatus, GyldigTil = gyldigTil };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    private static async Task<RettskildeEntitet> NyLovAsync(RegelIdeDbContext db, List<string>? lovdata, Guid? eier = null, string? tekst = null,
        string tittel = "Testlov")
    {
        var n = Guid.NewGuid().ToString("N")[..8];
        var r = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Tittel = $"{tittel} {n}", VirksomhetId = eier,
            Eli = $"https://lovdata.no/eli/lov/2099/01/01/{n}/nor", AnsvarligDepartement = lovdata, OpprettetAv = "Test", Status = "Gjeldende", AknXml = "<akomaNtoso/>",
        };
        db.Rettskilder.Add(r);
        if (tekst is not null)
        {
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = $"{r.Eli}/§1/ledd-1", KildeId = "x", NodeType = "ledd", Tekst = tekst,
            });
        }
        await db.SaveChangesAsync();
        return r;
    }

    private static async Task<StrukturkantEntitet> NyRessortkantAsync(RegelIdeDbContext db, Guid dept, string eli, string status, string objekt = "Lov om test",
        DateOnly? fra = null, DateOnly? til = null)
    {
        var k = new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = Strukturkanter.Oppgave, Typekode = "forvaltning", FraVirksomhetId = dept, Objekt = objekt, ObjektEli = eli,
            KildeUtenforKorpusTekst = "Testvedtak", KildeUtenforKorpusType = "kgl_res", KildeUtenforKorpusDokumentasjon = "primaer",
            GyldigFra = fra, GyldigTil = til, Status = status, OppdagelsesKilde = "monster:livslop", OpprettetAv = "Test",
        };
        db.Strukturkanter.Add(k);
        await db.SaveChangesAsync();
        return k;
    }

    [Fact]
    public async Task Departementet_ressortfordeling_vurdert_foran_Lovdata_og_forslag_brukes_ikke()
    {
        await using var db = fixture.NyDbContext();
        var lovdataDept = await NyVirksomhetAsync(db, $"Lovdatadepartementet {Guid.NewGuid():N}");
        var ressortDept = await NyVirksomhetAsync(db, $"Ressortdepartementet {Guid.NewGuid():N}");
        var lov = await NyLovAsync(db, [lovdataDept.Navn]);

        // Ingen ressortkant: Lovdatas felt, sekundær kilde.
        var s1 = (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", lov.Id))!;
        Assert.Equal((StandardregelUtfall.Lost, StandardregelRute.Lovdata, "sekundaer", lovdataDept.Id),
            (s1.Utfall, s1.Rute, s1.Dokumentasjon, s1.Referent!.VirksomhetId!.Value));
        Assert.Equal(Strukturkanter.Avledet, s1.Kunnskapsstatus);

        // Et ressortFORSLAG (ikke vurdert) brukes ikke — svaret er fortsatt Lovdata, men merknaden sier at forslaget venter og avviker.
        var kant = await NyRessortkantAsync(db, ressortDept.Id, lov.Eli!, Strukturkanter.MaskineltForeslatt, fra: new DateOnly(2001, 1, 1));
        var s2 = (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", lov.Id))!;
        Assert.Equal((StandardregelRute.Lovdata, lovdataDept.Id), (s2.Rute, s2.Referent!.VirksomhetId!.Value));
        Assert.Contains(s2.Merknader, m => m.StartsWith("Et ressortforslag venter", StringComparison.Ordinal));
        Assert.Contains(s2.Premisser, p => p.KantId == kant.Id && p.Status == Strukturkanter.MaskineltForeslatt);

        // Faglig vurdert: ressortfordelingen er svaret (primær), og Lovdatas avvik står i merknadene.
        kant.Status = Strukturkanter.FagligVurdert;
        await db.SaveChangesAsync();
        var s3 = (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", lov.Id))!;
        Assert.Equal((StandardregelRute.Ressortfordeling, "primaer", ressortDept.Id), (s3.Rute, s3.Dokumentasjon, s3.Referent!.VirksomhetId!.Value));
        Assert.Contains(s3.Merknader, m => m.Contains("avviker fra ressortfordelingen", StringComparison.Ordinal));

        // Gyldighet over tid: på en dato før kanten gjaldt, er det Lovdata igjen.
        var s4 = (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", lov.Id, new DateOnly(2000, 6, 1)))!;
        Assert.Equal(StandardregelRute.Lovdata, s4.Rute);

        // En DEL av loven til et annet departement gir en merknad, ikke et annet svar.
        await NyRessortkantAsync(db, lovdataDept.Id, lov.Eli!, Strukturkanter.Godkjent, objekt: "Lov om test, § 4", fra: new DateOnly(2001, 1, 1));
        var s5 = (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", lov.Id))!;
        Assert.Equal(ressortDept.Id, s5.Referent!.VirksomhetId);
        Assert.Contains(s5.Merknader, m => m.StartsWith("En del av loven", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Departementet_ikke_lost_uten_kilde_flertydig_Lovdata_og_nedlagt()
    {
        await using var db = fixture.NyDbContext();
        var a = await NyVirksomhetAsync(db, $"A-departementet {Guid.NewGuid():N}");
        var b = await NyVirksomhetAsync(db, $"B-departementet {Guid.NewGuid():N}");
        var regler = new GeneriskStandardregelTjeneste(db);

        Assert.Equal(StandardregelUtfall.IkkeLost, (await regler.LosAsync("departementet", (await NyLovAsync(db, null)).Id))!.Utfall);
        Assert.Equal(StandardregelUtfall.Flertydig, (await regler.LosAsync("departementet", (await NyLovAsync(db, [a.Navn, b.Navn])).Id))!.Utfall);
        // Et navn som ikke finnes i katalogen i dag, gjettes ikke.
        Assert.Equal(StandardregelUtfall.IkkeLost, (await regler.LosAsync("departementet", (await NyLovAsync(db, ["Finnes ikke-departementet"])).Id))!.Utfall);

        // Et nedlagt departement er svaret på en dato før nedleggelsen, og svaret sier at det er nedlagt.
        var nedlagt = await NyVirksomhetAsync(db, $"Nedlagtdepartementet {Guid.NewGuid():N}", "nedlagt", new DateOnly(2010, 1, 1));
        var lov = await NyLovAsync(db, [a.Navn]);
        await NyRessortkantAsync(db, nedlagt.Id, lov.Eli!, Strukturkanter.Godkjent, fra: new DateOnly(2001, 1, 1), til: new DateOnly(2010, 1, 1));
        var før = (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", lov.Id, new DateOnly(2005, 1, 1)))!;
        Assert.Equal((nedlagt.Id, new DateOnly(2010, 1, 1)), (før.Referent!.VirksomhetId!.Value, før.ReferentNedlagt!.Value));
        var iDag = (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", lov.Id))!;
        Assert.Equal((StandardregelRute.Lovdata, a.Id), (iDag.Rute, iDag.Referent!.VirksomhetId!.Value));
        Assert.Contains(iDag.Merknader, m => m.Contains("(nedlagt)", StringComparison.Ordinal));
    }

    private static async Task NyNavneformAsync(RegelIdeDbContext db, Guid vid, string term, string grunn)
    {
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = vid, Navneformgrunn = grunn, Term = term, Status = "publisert",
            Godkjenning = Strukturkanter.MaskineltForeslatt, OpprettetAv = "Test",
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Departementet_utgatt_navn_i_Lovdata_gir_svar_bare_naar_historikken_er_entydig()
    {
        await using var db = fixture.NyDbContext();
        var n = Guid.NewGuid().ToString("N")[..8];
        var levende = await NyVirksomhetAsync(db, $"Nytt departement {n}");
        await NyNavneformAsync(db, levende.Id, $"Gammelt departement {n}", "utgatt");
        var regler = new GeneriskStandardregelTjeneste(db);

        // Entydig: det utgåtte navnet fører til én levende aktør — svar, sekundær kilde, utgått navn.
        var s1 = (await regler.LosAsync("departementet", (await NyLovAsync(db, [$"Gammelt departement {n}"])).Id))!;
        Assert.Equal((StandardregelUtfall.Lost, StandardregelRute.LovdataUtgattNavn, "sekundaer", levende.Id),
            (s1.Utfall, s1.Rute, s1.Dokumentasjon, s1.Referent!.VirksomhetId!.Value));
        Assert.Contains(s1.Premisser, p => p.Type == "navnehistorikk");

        // Navnet gjenbrukt av et nedlagt departement: flertydig, ingen valgt.
        var nedlagt = await NyVirksomhetAsync(db, $"Gjenbrukt {n}", "nedlagt", new DateOnly(2010, 1, 1));
        await NyNavneformAsync(db, nedlagt.Id, $"Gjenbrukt navn {n}", "gjeldende");
        await NyNavneformAsync(db, levende.Id, $"Gjenbrukt navn {n}", "utgatt");
        Assert.Equal(StandardregelUtfall.Flertydig,
            (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", (await NyLovAsync(db, [$"Gjenbrukt navn {n}"])).Id))!.Utfall);

        // Bare et nedlagt departement har hatt navnet: ikke løst.
        await NyNavneformAsync(db, nedlagt.Id, $"Bare nedlagt {n}", "utgatt");
        Assert.Equal(StandardregelUtfall.IkkeLost,
            (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", (await NyLovAsync(db, [$"Bare nedlagt {n}"])).Id))!.Utfall);
    }

    [Fact]
    public async Task Departementet_utgatt_navn_brukt_av_flere_velges_etter_dato()
    {
        // [Ny, Johanns avgjørelse på #397 2026-10-26] Olje- og energidepartementet-mønsteret: et nedlagt departement hadde navnet til 1992,
        // dagens departement hadde det 1997–2023. Gjeldende versjon → den som sist hadde navnet; eldre versjon → den som hadde det på datoen.
        await using var db = fixture.NyDbContext();
        var n = Guid.NewGuid().ToString("N")[..8];
        var navn = $"Gjenbrukt dato {n}";
        var levende = await NyVirksomhetAsync(db, $"Dagens departement {n}");
        var nedlagt = await NyVirksomhetAsync(db, $"Nedlagt departement {n}", "nedlagt", new DateOnly(1992, 12, 31));
        foreach (var (vid, grunn, fra, til) in new[] { (nedlagt.Id, "gjeldende", (DateOnly?)null, new DateOnly(1992, 12, 31)),
                     (levende.Id, "utgatt", (DateOnly?)new DateOnly(1997, 1, 1), new DateOnly(2023, 12, 31)) })
        {
            db.Begreper.Add(new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = vid, Navneformgrunn = grunn, Term = navn, Status = "publisert",
                GyldigFra = fra, GyldigTil = til, Godkjenning = Strukturkanter.MaskineltForeslatt, OpprettetAv = "Test",
            });
        }
        await db.SaveChangesAsync();

        var gjeldende = (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", (await NyLovAsync(db, [navn])).Id))!;
        Assert.Equal((StandardregelUtfall.Lost, StandardregelRute.LovdataUtgattNavn, levende.Id, true),
            (gjeldende.Utfall, gjeldende.Rute, gjeldende.Referent!.VirksomhetId!.Value, gjeldende.ValgtEtterDato));

        var eldre = await NyLovAsync(db, [navn]);
        eldre.Entitetsstatus = "erstattet";
        eldre.KonsolidertDato = new DateOnly(1990, 6, 1);
        eldre.Ikrafttredelse = new DateOnly(1990, 6, 1);
        await db.SaveChangesAsync();
        var s = (await new GeneriskStandardregelTjeneste(db).LosAsync("departementet", eldre.Id))!;
        Assert.Equal((StandardregelUtfall.Lost, nedlagt.Id, true), (s.Utfall, s.Referent!.VirksomhetId!.Value, s.ValgtEtterDato));
        Assert.NotNull(s.ReferentNedlagt);
    }

    [Fact]
    public async Task Binding_faglig_vurdert_gaar_foran_regelen_og_regelen_alene_kan_slaas_opp()
    {
        await using var db = fixture.NyDbContext();
        var lovdataDept = await NyVirksomhetAsync(db, $"Regeldepartementet {Guid.NewGuid():N}");
        var bundet = await NyVirksomhetAsync(db, $"Bindingsdepartementet {Guid.NewGuid():N}");
        var lov = await NyLovAsync(db, [lovdataDept.Navn], tekst: "Departementet kan gi forskrift.");
        var tjeneste = new GeneriskNavneformTjeneste(db);
        // «departementet» er termen regelen gjelder, og lastertestene forventer at den IKKE er generisk i den delte basen: navneformen
        // arkiveres igjen til slutt når testen opprettet den.
        var (nf, varNy) = await tjeneste.OpprettEllerFinnAsync("departementet", "Test");
        try
        {

        var o1 = await tjeneste.LosOppAsync(nf.Id, lov.Id, null);
        Assert.Equal((GeneriskOppslagstatus.Regel, "regel", lovdataDept.Id), (o1.Status, o1.Kilde, o1.Referent!.VirksomhetId!.Value));

        // Et manuelt forslag (ikke vurdert) brukes ikke; faglig vurdert gjør (Johanns beslutning 2).
        var b = await tjeneste.OpprettBindingAsync(new NyNavneformBinding(nf.Id, lov.Id, Kantnode.Virksomhet(bundet.Id),
            Kunnskapsstatus: Strukturkanter.Uavklart, Begrunnelse: "Test", Status: Strukturkanter.ManueltForeslatt), "Test");
        Assert.Equal((GeneriskOppslagstatus.Regel, 1), ((await tjeneste.LosOppAsync(nf.Id, lov.Id, null)).Status,
            (await tjeneste.LosOppAsync(nf.Id, lov.Id, null)).AntallForeslatteBindinger));
        (await db.NavneformBindinger.SingleAsync(x => x.Id == b.Id)).Status = Strukturkanter.FagligVurdert;
        await db.SaveChangesAsync();
        var o2 = await new GeneriskNavneformTjeneste(db).LosOppAsync(nf.Id, lov.Id, null);
        Assert.Equal((GeneriskOppslagstatus.Bundet, "binding", bundet.Id), (o2.Status, o2.Kilde, o2.Referent!.VirksomhetId!.Value));
        Assert.Equal(lovdataDept.Id, o2.Regel!.Referent!.VirksomhetId); // regelen står ved siden av, som opplysning

        // Regelen alene (kaldsjekken): ser bort fra bindingen.
        var o3 = await new GeneriskNavneformTjeneste(db).LosOppAsync(nf.Id, lov.Id, null, brukBindinger: false);
        Assert.Equal((GeneriskOppslagstatus.Regel, lovdataDept.Id), (o3.Status, o3.Referent!.VirksomhetId!.Value));
        }
        finally
        {
            if (varNy)
            {
                await using var rydd = fixture.NyDbContext();
                var rad = await rydd.Begreper.SingleAsync(x => x.Id == nf.Id);
                rad.Entitetsstatus = "arkivert";
                await rydd.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Kommunen_og_statsforvalteren_fast_klasse_med_sperrer_naar_kilden_sier_noe_annet()
    {
        await using var db = fixture.NyDbContext();
        var regler = new GeneriskStandardregelTjeneste(db);
        var kommuneKlasse = await db.Begreper.FirstOrDefaultAsync(b => b.Medlemskapsregel == Klasseregler.OmraderegisterKommune && b.Entitetsstatus == "gjeldende");
        var vanlig = await NyLovAsync(db, null, tekst: "Kommunen skal sørge for tilbud.");
        var s = (await regler.LosAsync("kommunen", vanlig.Id))!;
        if (kommuneKlasse is null)
        {
            // Testbasen har ikke klassen (migrasjonen kan være tom her): ingen gjettet referent.
            Assert.Equal(StandardregelUtfall.IkkeLost, s.Utfall);
            return;
        }
        Assert.Equal((StandardregelUtfall.Lost, StandardregelRute.FastReferent, kommuneKlasse.Id), (s.Utfall, s.Rute, s.Referent!.BegrepId!.Value));

        // (a) kilden definerer ordet, (c) virksomhetsdokument, (d) Svalbard: «ikke avklart», med grunnen.
        var definerer = await NyLovAsync(db, null, tekst: "I denne forskriften forstås med kommunen Longyearbyen lokalstyre. Med kommunen menes Longyearbyen lokalstyre.");
        Assert.StartsWith("Kilden definerer", (await regler.LosAsync("kommunen", definerer.Id))!.Forklaring);
        var eier = await NyVirksomhetAsync(db, $"Testkommune {Guid.NewGuid():N}");
        var eid = await NyLovAsync(db, null, eier.Id, "Kommunen kan legge vekt på");
        Assert.Contains("virksomhetsdokument", (await regler.LosAsync("kommunen", eid.Id))!.Forklaring);
        var svalbard = await NyLovAsync(db, null, tekst: "Kommunen skal", tittel: "Byggeforskrift for Longyearbyen");
        Assert.Equal(StandardregelUtfall.IkkeLost, (await regler.LosAsync("kommunen", svalbard.Id))!.Utfall);

        // (b) for «statsforvalteren»: kilden navngir et embete.
        var navngir = await NyLovAsync(db, null, tekst: "Statsforvalteren i Nordland avgjør saken. Statsforvalteren står fritt.");
        Assert.StartsWith("Kilden navngir", (await regler.LosAsync("statsforvalteren", navngir.Id))!.Forklaring);
    }

    [Fact]
    public async Task Godkjent_forslag_med_ny_hjemmel_erstatter_den_gamle_navneformen_og_flytter_taggene()
    {
        await using var db = fixture.NyDbContext();
        var v = await NyVirksomhetAsync(db, $"Navnedepartementet {Guid.NewGuid():N}");
        var lov = await NyLovAsync(db, null, tekst: "Navnedepartementet er opprettet.");
        var node = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == lov.Id);
        var gammel = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = v.Id, Navneformgrunn = "gjeldende", Term = v.Navn,
            Status = "publisert", Godkjenning = Strukturkanter.Godkjent, OpprettetAv = "Test",
        };
        var ny = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = v.Id, Navneformgrunn = "gjeldende", Term = v.Navn,
            Status = "publisert", Godkjenning = Strukturkanter.MaskineltForeslatt, LovkildeId = lov.Id, LovreferanseEid = node.Eid,
            Kunnskapsstatus = Strukturkanter.Tekstfunn, ErstatterId = gammel.Id, OppdagelsesKilde = DepartementNavneformHjemmelTjeneste.OppdagelsesKilde,
            OpprettetAv = "Test",
        };
        var uten = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = v.Id, Navneformgrunn = "gjeldende", Term = v.Navn + " x",
            Status = "publisert", Godkjenning = Strukturkanter.MaskineltForeslatt, OpprettetAv = "Test",
        };
        db.Begreper.AddRange(gammel, ny, uten);
        db.TekstTagger.Add(new TekstTaggEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = lov.Id, NodeEid = node.Eid, StartOffset = 0, EndOffset = 5, QuotePrefix = "", QuoteExact = "Navne",
            QuoteSuffix = "", NodeTekstHash = "x", Kind = "virksomhet", RefId = gammel.Id, OpprettetAv = "Test", VirksomhetId = v.Id,
        });
        await db.SaveChangesAsync();
        var identitet = new AktoridentitetTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new VirksomhetOppslagTjeneste(db));

        // Et forslag som IKKE sier at det erstatter den gjeldende, kan ikke godkjennes (én gjeldende per aktør).
        await Assert.ThrowsAsync<ArgumentException>(() => identitet.GodkjennNavneformAsync(uten.Id, "Kari Jurist"));

        await identitet.GodkjennNavneformAsync(ny.Id, "Kari Jurist");
        await using var les = fixture.NyDbContext();
        Assert.Equal("erstattet", (await les.Begreper.SingleAsync(b => b.Id == gammel.Id)).Entitetsstatus);
        Assert.Equal(Strukturkanter.Godkjent, (await les.Begreper.SingleAsync(b => b.Id == ny.Id)).Godkjenning);
        Assert.True(await les.TekstTagger.AnyAsync(t => t.RefId == ny.Id));
        Assert.False(await les.TekstTagger.AnyAsync(t => t.RefId == gammel.Id));
    }
}
