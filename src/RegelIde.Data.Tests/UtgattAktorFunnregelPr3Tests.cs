using Microsoft.EntityFrameworkCore;
using Oppsett = RegelIde.Data.Tests.UtgattAktorFunnregelTests.Oppsett;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #429 PR 3, Johanns avgjørelser 2026-11-02 etter måling 2] Justeringen av maskinregelen for utgåtte aktører. Formulert som
/// spørsmålene modellen skal kunne besvare (CLAUDE.md §0):
/// <list type="bullet">
/// <item>Holder R2b, R5, R6, R7 og rettingene i R3 tilbake det juristen fant, og bare det (én positiv og én negativ per regel)?</item>
/// <item>R8: går en kilde som er opphevet av en annen gjeldende kilde, til kildefeilkøen uten funn — og en delvis oppheving ikke?</item>
/// <item>R9: får et funn der hjemmelsloven er opphevet, en synlig premissmangel som juristen kan lukke?</item>
/// <item>Etterfølgerkjeden: hvem har oppgaven nå ifølge modellen, når etterfølgeren selv er nedlagt?</item>
/// <item>Blir lastede funn som holdes tilbake, forklart bort av maskinregelen med regel og bevis — og gjenåpner en ny kjøring dem?</item>
/// </list>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class UtgattAktorFunnregelPr3Tests(EmbeddedPostgresFixture fixture)
{
    private const string Fd = "Fiskeridepartementet";
    private static readonly string[] Nfd = ["Nærings- og fiskeridepartementet"];

    // ---- Tekstreglene, rene ----------------------------------------------------------------------------------------------------

    [Theory]
    // R2b: datert vedtaksattribusjon, termen én gang (N19).
    [InlineData("Fiskeridepartementet har 23. mars 1988 bestemt at sjømannspresten i Houston skal ha vigselsrett.", null, "R2b")]
    // R5: endringsingress der termen står i tittelen på målvedtaket (N01–N03).
    [InlineData("I vedtak 8. desember 2003 nr. 1479 om overføring av myndighet til Fiskeridepartementet gjøres følgende endringer:", null, "R5")]
    // R3, retting 1 (N20): «av Xs» er genitiv, ikke agent.
    [InlineData("Fra samme tid oppheves vedtak 31. august 2001 nr. 1009 om delegering av Fiskeridepartementets myndighet.", null, "R3")]
    // R3, retting 2 (N20): setningen deles ikke ved «2. august».
    [InlineData("Fiskeridepartementets delegeringsfullmakt gitt 2. august 2000 oppheves.", null, "R3")]
    public void De_nye_reglene_slar_til_paa_juristens_former(string tekst, string? forelder, string regel) =>
        Assert.Contains(regel, UtgattAktorRegler.Treff(Fd, tekst, forelder));

    [Theory]
    // [LÅST, N19] R2b bare når termen står én gang: står den flere ganger, er navnet mer enn forfatteren av vedtaket.
    [InlineData("Fiskeridepartementet har 23. mars 1988 bestemt at Fiskeridepartementet skal føre tilsyn.", "R2b")]
    // R5 krever at hele noden er ingressen, med termen foran «gjøres følgende endring».
    [InlineData("I vedtak 8. desember 2003 nr. 1479 gjøres følgende endring: Fiskeridepartementet skal ha melding.", "R5")]
    // R3: «oppheves av X» er fortsatt agenten (grensen fra PR 2 står).
    [InlineData("Vedtaket kan oppheves av Fiskeridepartementet.", "R3")]
    // R3: den rettede delingen deler fortsatt ved et ekte setningsskille (stor bokstav).
    [InlineData("Forskrift om tilskudd oppheves 2. august 2000. Fiskeridepartementet fører tilsyn.", "R3")]
    public void De_nye_reglene_slar_ikke_til_utenfor_avgrensningen(string tekst, string regel) =>
        Assert.DoesNotContain(regel, UtgattAktorRegler.Treff(Fd, tekst, null));

    [Fact]
    public void R6_krever_termen_en_etterfolger_og_et_overforingsverb_i_samme_setning()
    {
        // N07: «alle klagesaker for X behandles av Y» er selve overføringen.
        Assert.Contains("R6", UtgattAktorRegler.Treff(Fd, "Saker som er til behandling i Fiskeridepartementet, overtas av Nærings- og fiskeridepartementet.", null, Nfd));
        // Ingen etterfølger i setningen: en gjeldende omtale, ikke en overføring.
        Assert.DoesNotContain("R6", UtgattAktorRegler.Treff(Fd, "Fiskeridepartementet overtar saken. Nærings- og fiskeridepartementet orienteres.", null, Nfd));
        // Etterfølgeren i setningen, men uten overføringsverb.
        Assert.DoesNotContain("R6", UtgattAktorRegler.Treff(Fd, "Fiskeridepartementet og Nærings- og fiskeridepartementet samarbeider.", null, Nfd));
    }

    [Theory]
    [InlineData("Fra samme tid oppheves vedtak 15. november 2005 nr. 1286 om delegering av myndighet til Rikstrygdeverket.", true)]
    [InlineData("Samtidig oppheves delegeringsvedtak 15. november 2005 nr. 1286 og delegeringsvedtak 12. desember 2000 nr. 1700.", true)]
    [InlineData("Kongelig resolusjon 15. november 2005 nr. 1286 om delegering av myndighet etter lov 15. august 2014 nr. 61 oppheves.", true)]
    [InlineData("Vedtaket erstatter delegeringsvedtak 15. november 2005 nr. 1286.", true)]
    // [LÅST, Johann 2026-11-02] Delvise opphevinger holdes utenfor.
    [InlineData("I forskrift 15. november 2005 nr. 1286 om ordningen av påtalemyndigheten oppheves kapittel 6.", false)]
    [InlineData("Fra samme dato oppheves kapitel VII i forskrift 15. november 2005 nr. 1286 om oppnevnelse av stedfortredere.", false)]
    [InlineData("I av kgl.res. 15. november 2005 nr. 1286 om delegering av Kongens myndighet, oppheves.", false)]
    [InlineData("Fra samme tidspunkt oppheves plan- og bygningsloven av 15. november 2005 nr. 1286 kapittel I til VII-a.", false)]
    // Et annet nummer samme dag er ikke målet.
    [InlineData("Fra samme tid oppheves vedtak 15. november 2005 nr. 1285 om delegering.", false)]
    public void Opphevingen_leses_av_teksten_og_delvise_holdes_utenfor(string tekst, bool hel) =>
        Assert.Equal(hel, Opphevingskontroll.FullOpphevingssetning(tekst, new DateOnly(2005, 11, 15), "1286") is not null);

    [Fact]
    public void Siterte_lover_blir_dokument_eli()
    {
        Assert.Equal(["https://lovdata.no/eli/lov/1988/06/24/64/nor"],
            Opphevingskontroll.SiterteLover("Myndighet etter lov 24. juni 1988 nr. 64 om utlendingers adgang til riket (utlendingsloven) § 37d.").ToArray());
        Assert.Empty(Opphevingskontroll.SiterteLover("Forskrift 24. juni 1988 nr. 64 gjelder."));
    }

    // ---- Med database -------------------------------------------------------------------------------------------------------

    private static string Dato(int nr) => $"1. januar 2099 nr. {nr}";
    private static int Nr(RettskildeEntitet rk) => int.Parse(rk.Eli!.Split('/')[^2]);

    /// <summary>Gjør testforskriften til en LTI-kilde: R8 gjelder ikke sentrale forskrifter (SF), fordi Lovdata vedlikeholder statusen.</summary>
    private static void Lti(RettskildeEntitet r) => r.DokumentId = "LTI/" + r.DokumentId!["SF/".Length..];

    private static async Task<RettskildeEntitet> KildeSomEndrerAsync(RegelIdeDbContext db, Oppsett o, RettskildeEntitet mal, string tekst)
    {
        var (s, _) = await o.ForskriftAsync(tekst);
        db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = s.Id, EndringEid = mal.Eli!, EndringRettskildeId = mal.Id });
        await db.SaveChangesAsync();
        return s;
    }

    [Fact]
    public async Task R8_En_kilde_som_er_opphevet_av_en_annen_gaar_til_kildefeilkoen_og_gir_ikke_funn()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Rtv"), nedlagt: true);
        var (t, _) = await o.ForskriftAsync(Lti, $"{a.Navn} kan videredelegere myndigheten.");
        var (delvis, _) = await o.ForskriftAsync(Lti, $"Klage går til {a.Navn}.");
        var s = await KildeSomEndrerAsync(db, o, t, $"Fra samme tid oppheves vedtak {Dato(Nr(t))} om delegering av myndighet.");
        await KildeSomEndrerAsync(db, o, delvis, $"I vedtak {Dato(Nr(delvis))} om klage oppheves kapittel 2.");

        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
        var torr = await regel.KjorAsync(skriv: false, "Test", [t.Id, delvis.Id], medRader: true);
        Assert.Equal(1, torr.KildefeilRettskilder);
        Assert.Equal(1, torr.PerRegel[UtgattAktorRegler.R8].Par);
        var rad = torr.Rader!.Single(r => r.DokumentId == t.DokumentId);
        Assert.Equal(UtgattAktorFunnregel.UtfallKildefeil, rad.Utfall);
        Assert.Contains(s.Eli! + "/§1/ledd-1", rad.Bevis);
        Assert.Equal(UtgattAktorFunnregel.UtfallForeslatt, torr.Rader!.Single(r => r.DokumentId == delvis.DokumentId).Utfall);
        Assert.False(await db.Kildefeil.AnyAsync(k => k.RettskildeId == t.Id), "Tørrkjøringen skal ikke skrive kildefeil.");

        var skrevet = await regel.KjorAsync(skriv: true, "Test", [t.Id, delvis.Id]);
        Assert.Equal(1, skrevet.KildefeilNye);
        var kf = await db.Kildefeil.SingleAsync(k => k.RettskildeId == t.Id);
        Assert.Equal(UtgattAktorFunnregel.KildefeiltypeOpphevetAvAnnenKilde, kf.Type);
        Assert.Equal(UtgattAktorFunnregel.OppdagelsesKilde, kf.FunnetAvMekanisme);
        Assert.Contains("oppheves vedtak", kf.Beskrivelse);
        var funn = new FunnTjeneste(db);
        Assert.Empty(await funn.ListeAsync(rettskildeId: t.Id));
        Assert.Single(await funn.ListeAsync(rettskildeId: delvis.Id));
        Assert.False(await db.Kildefeil.AnyAsync(k => k.RettskildeId == delvis.Id));

        // Idempotent i kildefeilkøen også.
        Assert.Equal(0, (await regel.KjorAsync(skriv: true, "Test", [t.Id, delvis.Id])).KildefeilNye);
        Assert.Equal(1, await db.Kildefeil.CountAsync(k => k.RettskildeId == t.Id));
    }

    [Fact]
    public async Task R7_Et_endringsvedtak_som_star_ordrett_i_malvedtaket_med_eget_funn_foreslas_ikke()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Dupl"), nedlagt: true);
        var tekst = $"Klage over vedtak etter forskriften går til {a.Navn}.";
        // Målvedtaket er konsolidert med endringsvedtaket (sist_endret_ved), og har samme ledd ordrett.
        var (endrer, _) = await o.ForskriftAsync(Lti, tekst);
        var (mal, _) = await o.ForskriftAsync(r => r.SistEndretVed = UtgattAktorFunnregel.KortDokumentId(endrer.DokumentId!), "Første ledd.", tekst);
        db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = endrer.Id, EndringEid = mal.Eli!, EndringRettskildeId = mal.Id });
        // Negativ: samme oppsett, men målvedtaket er ikke konsolidert med endringsvedtaket (2019-1491 i replikken).
        var (endrer2, _) = await o.ForskriftAsync(Lti, tekst);
        var (mal2, _) = await o.ForskriftAsync(r => r.SistEndretVed = "forskrift/1999-01-01-1", tekst);
        db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = endrer2.Id, EndringEid = mal2.Eli!, EndringRettskildeId = mal2.Id });
        await db.SaveChangesAsync();

        var r = await new UtgattAktorFunnregel(db, new FunnTjeneste(db))
            .KjorAsync(skriv: false, "Test", [endrer.Id, mal.Id, endrer2.Id, mal2.Id], medRader: true);
        var duplikat = r.Rader!.Single(x => x.DokumentId == endrer.DokumentId);
        Assert.False(duplikat.Foreslatt);
        Assert.Contains(UtgattAktorRegler.R7, duplikat.Forekomster.Single().Regler);
        Assert.Contains("ordrett i målvedtaket", duplikat.Bevis);
        Assert.True(r.Rader!.Single(x => x.DokumentId == mal.DokumentId).Foreslatt);
        Assert.True(r.Rader!.Single(x => x.DokumentId == endrer2.DokumentId).Foreslatt);
        Assert.Equal(1, r.PerRegel[UtgattAktorRegler.R7].Par);
    }

    [Fact]
    public async Task R9_Hjemmelsloven_opphevet_gir_synlig_premissmangel_som_juristen_kan_lukke()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (levende, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Justis"), nedlagt: false);
        var gammelt = UtgattAktorFunnregelTests.Departement("Justispoliti");
        db.Begreper.Add(Oppsett.NyNavneform(levende.Id, gammelt, "utgatt", Strukturkanter.FagligVurdert));

        // Den gamle loven er ikke i korpus (bare en referanse), og en gjeldende lov opphever den.
        var nr = Random.Shared.Next(100, 999);
        var gammelLov = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Gammel lov",
            Eli = $"https://lovdata.no/eli/lov/2098/06/24/{nr}/nor", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        var nyLov = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "primaer", Tittel = "Ny lov",
            Eli = $"https://lovdata.no/eli/lov/2099/05/15/{nr}/nor", DokumentId = $"NL/lov/2099-05-15-{nr}", AknXml = "<akomaNtoso/>",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Rettskilder.AddRange(gammelLov, nyLov);
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = nyLov.Id, Eid = $"{nyLov.Eli}/§140/ledd-1", KildeId = "§140/ledd-1", NodeType = "ledd", TekstHash = "h",
            Tekst = $"Loven gjelder fra den tid Kongen bestemmer. Fra den tiden loven trer i kraft oppheves lov 24. juni 2098 nr. {nr} om utlendinger.",
        });
        db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = nyLov.Id, EndringEid = gammelLov.Eli!, EndringRettskildeId = gammelLov.Id });
        await db.SaveChangesAsync();

        var (rk, _) = await o.ForskriftAsync($"Myndighet etter lov 24. juni 2098 nr. {nr} om utlendinger § 37d delegeres til {gammelt}.");
        // Negativ: samme navn, men loven som siteres, er ikke opphevet.
        var (uten, _) = await o.ForskriftAsync($"Myndighet etter lov 24. juni 2098 nr. {nr + 1} delegeres til {gammelt}.");

        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
        var r = await regel.KjorAsync(skriv: true, "Test", [rk.Id, uten.Id], medRader: true);
        Assert.Equal(1, r.Premissmangel);
        Assert.Equal(2, r.Foreslatt); // R9 undertrykker ikke
        var t = new FunnTjeneste(db);
        var f = Assert.Single(await t.ListeAsync(rettskildeId: rk.Id));
        Assert.Equal(Funn.Oppdaget, f.Status);
        var mangel = Assert.Single(await t.PremissmanglerAsync(f), m => m.StartsWith("Hjemmelsloven kan være opphevet"));
        Assert.Contains($"lov 24. juni 2098 nr. {nr}", mangel);
        Assert.Contains($"LOV-2099-05-15-{nr} § 140 første ledd", mangel);
        var fUten = Assert.Single(await t.ListeAsync(rettskildeId: uten.Id));
        Assert.DoesNotContain(await t.PremissmanglerAsync(fUten), m => m.StartsWith("Hjemmelsloven"));

        // Juristen lukker mangelen med vurderingspunktet «Hjemmel» (og «Etterfølger», som premissporten krever i gruppe A).
        await t.JuristvurderAsync(f.Id, "Delegeringen har ingen gjenstand etter at loven er opphevet.", Funn.KiAgent, "agent",
        [
            new NyttVurderingspunkt(Funn.BortforklaringProvd, "Overgangsregel: ingen videreføring av delegeringer."),
            new NyttVurderingspunkt(Funn.Vurdering, "Ikke videreført.", Funn.HjemmelOverskrift),
            new NyttVurderingspunkt(Funn.Vurdering, $"{levende.Navn}.", Funn.EtterfolgerOverskrift),
        ]);
        Assert.Empty(await t.PremissmanglerAsync((await t.HentAsync(f.Id))!));
    }

    [Fact]
    public async Task Etterfolgerkjeden_folges_til_levende_aktor_og_sier_fra_naar_den_ender_i_nedlagt_aktor()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        // Sosialdepartementet → ASD (selv nedlagt) → AID (levende). Og X → W (nedlagt, ingen etterfølger).
        var (sos, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Sosial"), nedlagt: true);
        var (asd, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Arbsos"), nedlagt: true);
        var (aid, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Arbinkl"), nedlagt: false);
        var kant1 = await o.EtterfolgerAsync(asd.Id, sos.Id, "monster:livslop-etterfolger");
        var kant2 = await o.EtterfolgerAsync(aid.Id, asd.Id, "monster:livslop-etterfolger");
        var (x, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Ex"), nedlagt: true);
        var (w, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Weh"), nedlagt: true);
        await o.EtterfolgerAsync(w.Id, x.Id, "monster:livslop-etterfolger");
        // Livsstatus NULL med en avvikler-kant: ikke levende uten videre.
        var (nemnd, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Nemnd"), nedlagt: false);
        var kongen = new Virksomhet { Id = Guid.NewGuid(), Navn = "Kongen i statsråd test" };
        db.Virksomheter.Add(kongen);
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = Strukturkanter.Relasjon, Typekode = "avvikler", FraVirksomhetId = kongen.Id, TilVirksomhetId = nemnd.Id,
            Status = Strukturkanter.MaskineltForeslatt, OppdagelsesKilde = "test", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            KildeUtenforKorpusTekst = "Testvedtak", KildeUtenforKorpusType = "kgl_res", KildeUtenforKorpusDokumentasjon = "primaer",
        });
        await db.SaveChangesAsync();

        var (rk, _) = await o.ForskriftAsync(r => r.AnsvarligDepartement = [sos.Navn, "Helse- og omsorgsdepartementet"],
            $"{sos.Navn} kan gi forskrift.", $"{x.Navn} avgjør klagen.", $"{nemnd.Navn}s vedtak er endelig.");
        await new UtgattAktorFunnregel(db, new FunnTjeneste(db)).KjorAsync(skriv: true, "Test", [rk.Id]);
        var t = new FunnTjeneste(db);
        var funn = await t.ListeAsync(rettskildeId: rk.Id);
        FunnEntitet For(Guid vid) => funn.Single(f => f.Koblinger.Any(k => k.Rolle == Funn.BerortAktor && k.VirksomhetId == vid));

        var fSos = For(sos.Id);
        Assert.Contains($"Modellen har {asd.Navn} som etterfølger.", fSos.Beskrivelse);
        Assert.Contains($"{asd.Navn} er selv nedlagt (siste dag 2013-12-31).", fSos.Beskrivelse);
        Assert.Contains($"fører videre til {aid.Navn}.", fSos.Beskrivelse);
        // Begge kantene er premisser: premissporten krever hver av dem vurdert (svakeste premiss).
        Assert.Equal(new[] { kant1.Id, kant2.Id }.Order(), fSos.Koblinger.Where(k => k.Rolle == Funn.Etterfolger).Select(k => k.StrukturkantId!.Value).Order());
        Assert.Contains(fSos.Koblinger, k => k.Rolle == Funn.Etterfolger && k.Etikett == $"{aid.Navn} (etter {asd.Navn})");
        Assert.Equal(2, (await t.PremissmanglerAsync(fSos)).Count(m => m.StartsWith("Etterfølger ikke vurdert")));
        // Lovdatas ansvarlige departement står ved siden av hintet, som sekundær kilde, med det utgåtte navnet merket.
        Assert.Contains($"Lovdata oppgir {sos.Navn} og Helse- og omsorgsdepartementet som ansvarlig departement for rettskilden (metadata, sekundær kilde). "
            + $"{sos.Navn} er selv et utgått navn i modellen.", fSos.Beskrivelse);

        Assert.Contains($"Kjeden ender i nedlagt aktør uten etterfølger: {w.Navn}.", For(x.Id).Beskrivelse);

        var fNemnd = For(nemnd.Id);
        Assert.Equal("nedlagt_aktor", fNemnd.Undertype);
        Assert.Contains("avvikler-kant", fNemnd.Beskrivelse);
        Assert.DoesNotContain("finnes fortsatt", fNemnd.Beskrivelse);
    }

    [Fact]
    public async Task Lastede_funn_som_holdes_tilbake_forklares_bort_av_maskinregelen_og_gjenapnes_ikke()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Last"), nedlagt: true);
        var (tekstregel, ledd) = await o.ForskriftAsync($"{a.Navn} avgjør.");
        var (opphevet, _) = await o.ForskriftAsync(Lti, $"{a.Navn} kan delegere.");
        var (vurdert, ledd3) = await o.ForskriftAsync($"Klage går til {a.Navn}.");
        var ider = new[] { tekstregel.Id, opphevet.Id, vurdert.Id };
        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
        Assert.Equal(3, (await regel.KjorAsync(skriv: true, "Test", ider)).Nye);
        var t = new FunnTjeneste(db);
        var fVurdert = (await t.ListeAsync(rettskildeId: vurdert.Id)).Single();
        await t.JuristvurderAsync(fVurdert.Id, "Gjeldende omtale.", Funn.Menneske, "Jurist", [new NyttVurderingspunkt(Funn.BortforklaringProvd, "ingen")]);

        // Etter lastingen: noden blir en endringsingress (R5), kilden blir opphevet av en annen (R8), og det juristvurderte funnet treffes
        // også av R5.
        ledd[0].Tekst = $"I vedtak 1. januar 2000 nr. 1 om {a.Navn} gjøres følgende endringer:";
        ledd3[0].Tekst = $"I vedtak 1. januar 2000 nr. 2 om {a.Navn} gjøres følgende endringer:";
        await db.SaveChangesAsync();
        var s = await KildeSomEndrerAsync(db, o, opphevet, $"Vedtak {Dato(Nr(opphevet))} om delegering oppheves.");

        var torr = await regel.KjorAsync(skriv: false, "Test", ider);
        Assert.Equal(2, torr.ForklaresBort);
        Assert.Equal(1, torr.HoldtTilbakeMenAlleredeVurdert);
        Assert.Equal(0, torr.ForklartBortSatt);
        Assert.Equal(1, torr.PerRegel[UtgattAktorRegler.R5].ForklaresBort);
        Assert.Equal(1, torr.PerRegel[UtgattAktorRegler.R8].ForklaresBort);
        Assert.Equal(Funn.Oppdaget, (await db.Funn.AsNoTracking().SingleAsync(f => f.Koblinger.Any(k => k.RettskildeId == tekstregel.Id))).Status);

        var skrevet = await regel.KjorAsync(skriv: true, "Kjører Regel", ider);
        Assert.Equal(2, skrevet.ForklartBortSatt);
        var fTekst = await db.Funn.AsNoTracking().SingleAsync(f => f.Koblinger.Any(k => k.RettskildeId == tekstregel.Id));
        Assert.Equal(Funn.ForklartBort, fTekst.Status);
        Assert.Equal(Funn.Maskinregel, fTekst.VurdertAvType);
        Assert.Equal(UtgattAktorFunnregel.OppdagelsesKilde, fTekst.ForklartBortAv);
        Assert.Contains("R5", fTekst.ForklartBortBegrunnelse);
        Assert.Contains(ledd[0].Eid, fTekst.ForklartBortBegrunnelse);
        var fOpphevet = await db.Funn.AsNoTracking().SingleAsync(f => f.Koblinger.Any(k => k.RettskildeId == opphevet.Id));
        var kf = await db.Kildefeil.SingleAsync(k => k.RettskildeId == opphevet.Id);
        Assert.Equal(Funn.ForklartBort, fOpphevet.Status);
        Assert.Contains("R8", fOpphevet.ForklartBortBegrunnelse);
        Assert.Contains(s.DokumentId!.Split('/')[^1], fOpphevet.ForklartBortBegrunnelse);
        Assert.Contains(kf.Id.ToString(), fOpphevet.ForklartBortBegrunnelse);
        // Hvem som kjørte regelen, står i proveniensen — ikke som den som forklarte bort.
        Assert.True(await db.Proveniens.AnyAsync(p => p.EntitetId == fTekst.Id && p.Handling == "forklart_bort" && p.EndretAv == "Kjører Regel"));
        // Et juristvurdert funn overstyres ikke.
        Assert.Equal(Funn.Juristvurdert, (await db.Funn.AsNoTracking().SingleAsync(f => f.Id == fVurdert.Id)).Status);

        // En ny kjøring gjenåpner ingenting, lager ingen nye og sletter ingenting.
        var igjen = await regel.KjorAsync(skriv: true, "Test", ider);
        Assert.Equal(0, igjen.Nye);
        Assert.Equal(0, igjen.ForklartBortSatt);
        Assert.Equal(0, igjen.ForklaresBort);
        Assert.Equal(3, await db.Funn.CountAsync(f => f.Koblinger.Any(k => k.Rolle == Funn.BerortAktor && k.VirksomhetId == a.Id)));
        Assert.Equal(Funn.ForklartBort, (await db.Funn.AsNoTracking().SingleAsync(f => f.Id == fTekst.Id)).Status);
        // Og en jurist kan ikke juristvurdere det som er forklart bort (samme regel som før).
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.JuristvurderAsync(fTekst.Id, "x", Funn.Menneske, "J",
            [new NyttVurderingspunkt(Funn.BortforklaringProvd, "x")]));
    }

    [Fact]
    public async Task Oppfrisk_skriver_paastanden_paa_nytt_bare_for_urort_oppdaget_funn()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Frisk"), nedlagt: true);
        var (rk1, _) = await o.ForskriftAsync($"{a.Navn} avgjør.");
        var (rk2, _) = await o.ForskriftAsync($"{a.Navn} fører tilsyn.");
        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
        await regel.KjorAsync(skriv: true, "Test", [rk1.Id, rk2.Id]);
        var t = new FunnTjeneste(db);
        var f2 = (await t.ListeAsync(rettskildeId: rk2.Id)).Single();
        await t.OppdaterAsync(f2.Id, new FunnEndring(f2.Tittel, f2.Beskrivelse + "\n- Merknad fra et menneske.", f2.Undertype, null, null), "Jurist");
        // Etter lastingen får aktøren en etterfølger: påstanden fra PR 2 er utdatert.
        var (ny, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Arving"), nedlagt: false);
        await o.EtterfolgerAsync(ny.Id, a.Id, "monster:livslop-etterfolger");

        var torr = await regel.KjorAsync(skriv: true, "Test", [rk1.Id, rk2.Id]);
        Assert.Equal(1, torr.Oppfriskes);
        Assert.Equal(0, torr.Oppfrisket); // uten ?oppfrisk=true røres ingenting
        Assert.Contains("ingen etterfølger", (await t.ListeAsync(rettskildeId: rk1.Id)).Single().Beskrivelse);

        var r = await regel.KjorAsync(skriv: true, "Test", [rk1.Id, rk2.Id], oppfrisk: true);
        Assert.Equal(1, r.Oppfrisket);
        var f1 = (await t.ListeAsync(rettskildeId: rk1.Id)).Single();
        Assert.Contains($"Modellen har {ny.Navn} som etterfølger", f1.Beskrivelse);
        Assert.Equal(Funn.Oppdaget, f1.Status);
        Assert.Equal(UtgattAktorFunnregel.OppdagelsesKilde, f1.SistEndretAv);
        Assert.Contains("Merknad fra et menneske", (await t.ListeAsync(rettskildeId: rk2.Id)).Single().Beskrivelse);
    }
}
