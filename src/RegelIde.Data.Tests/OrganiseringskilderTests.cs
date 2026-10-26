using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #395] Hele kjeden mot ekte Postgres: et kunngjort Lovtidend-vedtak (FOR-2004-10-01-1304, fixture) importeres gjennom samme
/// parser og import som gjeldende-uttrekket, klassifiseres, og livsløpet legger forslag i forslagskøen — uten å godkjenne noe.
/// <para>
/// Én test, fordi trinnene bygger på hverandre og ELI-en i fixturen er fast (to tester ville delt den samme raden i samlingens database).
/// Katalogaktørene opprettes bare når de mangler: seedingen kan alt ha dem, og to aktører med samme navn ville gjort oppslaget flertydig.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class OrganiseringskilderTests(EmbeddedPostgresFixture fixture)
{
    private static async IAsyncEnumerable<(string Datokode, string Type, string Html)> Dokumenter(params (string, string, string)[] d)
    {
        foreach (var x in d) yield return x;
        await Task.CompletedTask;
    }

    private static async Task<Guid> SikreAktorAsync(RegelIdeDbContext db, string navn)
    {
        var lower = navn.ToLower();
        var finnes = await db.Virksomheter.Where(v => v.Navn.ToLower() == lower).Select(v => (Guid?)v.Id).FirstOrDefaultAsync();
        if (finnes is { } id) return id;
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = navn };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    [Fact]
    public async Task Lovtidend_vedtak_importeres_klassifiseres_og_gir_livslopsforslag()
    {
        var html = Testdata.LesOmorganisering2004();
        await using var db = fixture.NyDbContext();
        var klassifiserer = new DisposisjonstypeTjeneste(db);
        var import = new LovtidendImportTjeneste(new RettskildeImportTjeneste(db), klassifiserer, db);
        var ikraft = html.Replace("FOR-2004-10-01-1304", "FOR-2004-10-02-9999").Replace("2004-10-01-1304", "2004-10-02-9999")
            .Replace("<dd class=\"title\">Omorganisering av departementsstrukturen fra 1. oktober 2004.</dd>",
                "<dd class=\"title\">Ikrafttredelse av lov om noe.</dd>");

        // Tørrkjøring: ingenting skrives, tallene er de samme.
        var tørr = await import.KjorAsync(Dokumenter(("FOR-2004-10-01-1304", "forskrift", html), ("FOR-2004-10-02-9999", "forskrift", ikraft)), skriv: false);
        Assert.Equal(2, tørr.Dokumenter);
        Assert.Equal(1, tørr.Importert);
        Assert.Equal(1, tørr.HoppetOverIkrafttredelse);
        Assert.False(await db.Rettskilder.AnyAsync(r => r.Eli == "https://lovdata.no/eli/forskrift/2004/10/01/1304/nor" && r.Importrolle == "primaer"));

        var rapport = await import.KjorAsync(Dokumenter(("FOR-2004-10-01-1304", "forskrift", html)), skriv: true);
        Assert.Equal(1, rapport.Importert);
        Assert.Empty(rapport.Feil);
        Assert.Equal(1, rapport.PerType[Disposisjonstyper.Omorganisering].Importert);
        var id = Assert.Single(rapport.NyeRettskildeIder);
        var rettskilde = await db.Rettskilder.SingleAsync(r => r.Id == id);
        Assert.Equal("kunngjort", rettskilde.Kildeform);
        Assert.Equal("LTI/forskrift/2004-10-01-1304", rettskilde.DokumentId);
        Assert.True(await db.RettskildeHjemler.AnyAsync(h => h.RettskildeId == id)); // basedOn → hjemmelslov
        Assert.True(await db.RettskildeEndringer.AnyAsync(e => e.RettskildeId == id)); // changesToDocuments → endringsrelasjon

        // Klassifisert med tittel (omorganisering) og innhold (oppretting, nedleggelse, navneendring …), alt normalisering.
        var typer = await db.RettskildeDisposisjonstyper.Where(d => d.RettskildeId == id).ToListAsync();
        Assert.Contains(typer, t => t.Disposisjonstype == "omorganisering" && t.Kilde == "tittel");
        Assert.Contains(typer, t => t.Disposisjonstype == "navneendring" && t.Kilde == "monster" && t.NodeEid!.EndsWith("punkt-8"));
        Assert.All(typer, t => Assert.Equal("normalisering", t.Kunnskapsstatus));

        // En ny kjøring importerer ikke på nytt: ELI-en finnes som primærkilde.
        var igjen = await import.KjorAsync(Dokumenter(("FOR-2004-10-01-1304", "forskrift", html)), skriv: true);
        Assert.Equal(0, igjen.Importert);
        Assert.Equal(1, igjen.AlleredeIKorpus);

        // ---- Livsløpet ----
        await Strukturkanter.SeedStartsettAsync(db);
        var kongen = await SikreAktorAsync(db, DepartementLivslopTjeneste.KongenIStatsrad);
        var lmd = await SikreAktorAsync(db, "Landbruks- og matdepartementet");
        var hod = await SikreAktorAsync(db, "Helse- og omsorgsdepartementet");
        var livslop = new DepartementLivslopTjeneste(db, new StrukturkantTjeneste(db));

        var r1 = await livslop.KjorAsync(skriv: true);
        Assert.True(r1.Skriving.Feil.Count == 0, string.Join(" || ", r1.Skriving.Feil));
        var landbruk = r1.Identiteter.Single(i => i.Navn.Any(n => n.Navn == "Landbruksdepartementet"));
        Assert.Equal(lmd, landbruk.KatalogVirksomhetId);
        Assert.Equal(new DateOnly(2004, 9, 30), landbruk.Navn[0].Til);
        Assert.Equal("Landbruks- og matdepartementet", landbruk.Navn[^1].Navn);
        // [ENDRET, Johanns avgjørelse M5] Det nedlagte Sosialdepartementet blir en aktør med status nedlagt, uten registerkobling, og får
        // R avvikler fra Kongen i statsråd med hjemmel i nedleggelsen. Oppslaget «for i dag» treffer det aldri.
        var sosial = r1.Identiteter.Single(i => i.Navn[0].Navn == "Sosialdepartementet");
        Assert.Equal(new DateOnly(2004, 10, 1), sosial.Nedlagt);
        var sosialAktor = await db.Virksomheter.SingleAsync(v => v.Id == sosial.KatalogVirksomhetId);
        Assert.Equal("nedlagt", sosialAktor.Livsstatus);
        Assert.Null(sosialAktor.Organisasjonsnummer);
        Assert.False(sosialAktor.Aktiv);
        Assert.Equal(new DateOnly(2004, 9, 30), sosialAktor.GyldigTil);
        Assert.Null(await new VirksomhetOppslagTjeneste(db).FinnVirksomhetIdForNavnEllerNavneformAsync("Sosialdepartementet"));
        var avvikler = await db.Strukturkanter.SingleAsync(k => k.Typekode == "avvikler" && k.TilVirksomhetId == sosialAktor.Id);
        Assert.Equal(kongen, avvikler.FraVirksomhetId);
        Assert.Equal(Strukturkanter.MaskineltForeslatt, avvikler.Status);
        Assert.Equal(new DateOnly(2004, 10, 1), avvikler.GyldigFra);
        var sosialNavn = await db.Begreper.SingleAsync(b => b.VirksomhetReferanseId == sosialAktor.Id && b.Term == "Sosialdepartementet");
        Assert.Equal("gjeldende", sosialNavn.Navneformgrunn);
        Assert.Equal(Strukturkanter.MaskineltForeslatt, sosialNavn.Godkjenning);

        var utgatt = await db.Begreper.SingleAsync(b => b.VirksomhetReferanseId == lmd && b.Term == "Landbruksdepartementet");
        Assert.Equal("utgatt", utgatt.Navneformgrunn);
        Assert.Equal(id, utgatt.LovkildeId);
        Assert.EndsWith("punkt-8", utgatt.LovreferanseEid);
        Assert.Equal(Strukturkanter.MaskineltForeslatt, utgatt.Godkjenning);
        Assert.Equal(new DateOnly(2004, 9, 30), utgatt.GyldigTil);
        Assert.Equal("normalisering", utgatt.Kunnskapsstatus); // genitiv i vedtaket → grunnform

        var oppretter = await db.Strukturkanter.SingleAsync(k => k.Typekode == "oppretter" && k.TilVirksomhetId == hod && k.HjemmelRettskildeId == id);
        Assert.Equal(kongen, oppretter.FraVirksomhetId);
        Assert.Equal(Strukturkanter.MaskineltForeslatt, oppretter.Status);
        Assert.Equal(new DateOnly(2004, 10, 1), oppretter.GyldigFra);
        Assert.NotNull(oppretter.HjemmelNodeId);

        var ressort = await db.Strukturkanter.Where(k => k.Kategori == "F" && k.Typekode == "forvaltning" && k.FraVirksomhetId == hod
            && k.HjemmelRettskildeId == id).ToListAsync();
        Assert.NotEmpty(ressort);
        Assert.All(ressort, k => Assert.Equal(Strukturkanter.MaskineltForeslatt, k.Status));
        Assert.All(ressort, k => Assert.Equal("tekstfunn", k.Kunnskapsstatus)); // vedlegget sier «forvaltningsansvaret»
        Assert.Contains(ressort, k => k.ObjektEli is not null && k.ObjektEli.StartsWith("https://lovdata.no/eli/lov/"));
        Assert.Equal(r1.Skriving.RessortNye, ressort.Count);

        // Idempotent: andre kjøring legger ikke til noe.
        var r2 = await livslop.KjorAsync(skriv: true);
        Assert.Equal(0, r2.Skriving.OpprettetNye + r2.Skriving.NavneformerNye + r2.Skriving.RessortNye + r2.Skriving.AvviklerNye);
        Assert.Equal(0, r2.Skriving.HistoriskeAktorerNye);
        Assert.Empty(r2.Skriving.Feil);

        // ---- Etterfølger (Johanns avgjørelse 2): et konstruert vedtak 2006 nedlegger Arbeids- og sosialdepartementet og gir lovene det
        // fikk i 2004 (vedlegg 1) til et nytt Arbeids- og inkluderingsdepartement. Da etterfølger det nye det nedlagte, avledet av lovene.
        var v2006 = html.Replace("FOR-2004-10-01-1304", "FOR-2005-12-16-9998").Replace("2004-10-01-1304", "2005-12-16-9998")
            .Replace("<dd class=\"dateInForce\">2004-10-01</dd>", "<dd class=\"dateInForce\">2006-01-01</dd>")
            .Replace("Opprettelse av et Arbeids- og sosialdepartement, et Helse- og omsorgsdepartement og et Moderniseringsdepartement.",
                "Opprettelse av et Arbeids- og inkluderingsdepartement.")
            .Replace("Nedleggelse av Sosialdepartementet, Helsedepartementet og Arbeids- og administrasjonsdepartementet.",
                "Nedleggelse av Arbeids- og sosialdepartementet.")
            .Replace("Oversikt over lover Arbeids- og sosialdepartementet skal ha", "Oversikt over lover Arbeids- og inkluderingsdepartementet skal ha");
        Assert.Equal(1, (await import.KjorAsync(Dokumenter(("FOR-2005-12-16-9998", "forskrift", v2006)), skriv: true)).Importert);
        var aid = await SikreAktorAsync(db, "Arbeids- og inkluderingsdepartementet");
        var r3 = await livslop.KjorAsync(skriv: true);
        Assert.True(r3.Skriving.Feil.Count == 0, string.Join(" || ", r3.Skriving.Feil));
        var asdAktor = await db.Virksomheter.SingleAsync(v => v.Livsstatus == "nedlagt" && v.Navn == "Arbeids- og sosialdepartementet");
        Assert.Equal(new DateOnly(2004, 10, 1), asdAktor.GyldigFra);
        Assert.Equal(new DateOnly(2005, 12, 31), asdAktor.GyldigTil);
        var etterfolger = await db.Strukturkanter.SingleAsync(k => k.Typekode == "etterfolger" && k.FraVirksomhetId == aid && k.TilVirksomhetId == asdAktor.Id);
        Assert.Equal(Strukturkanter.Avledet, etterfolger.Kunnskapsstatus);
        Assert.Equal(Strukturkanter.MaskineltForeslatt, etterfolger.Status);
        Assert.True(await db.StrukturkantPremisser.AnyAsync(p => p.KantId == etterfolger.Id));
        // Et rent navnebytte gir aldri etterfølger: Landbruks- og matdepartementet har ingen.
        Assert.False(await db.Strukturkanter.AnyAsync(k => k.Typekode == "etterfolger" && (k.FraVirksomhetId == lmd || k.TilVirksomhetId == lmd)));
        // HOD finnes før og etter 2006 (ikke nedlagt), så HOD får ingen etterfølger selv om vedlegg 2 gjentas.
        Assert.False(await db.Strukturkanter.AnyAsync(k => k.Typekode == "etterfolger" && k.TilVirksomhetId == hod));

        // [Ny, issue #397, Johann 2026-10-24 på #134] Omfanget er antall lovdeler som gikk over: i kommentaren og i visningen (fra premissene).
        var visning = Assert.Single(await new StrukturkantTjeneste(db).ByggVisningerAsync([etterfolger], perspektiv: null));
        var premissFraEtterfolger = await db.Strukturkanter.CountAsync(k => k.Kategori == "F" && k.FraVirksomhetId == aid
            && db.StrukturkantPremisser.Any(p => p.KantId == etterfolger.Id && p.PremissKantId == k.Id));
        Assert.True(premissFraEtterfolger > 0);
        Assert.Equal(premissFraEtterfolger, visning.Omfang);
        Assert.StartsWith($"Omfang: {premissFraEtterfolger} ", etterfolger.Kommentar);
        Assert.DoesNotContain("hoved- eller delvis", etterfolger.Begrunnelse);

        // [Ny, issue #397] En ny versjon av vedtaket (resynk eller ny konvertering) gir ikke forslagene på nytt: samme ELI og eId er samme utsagn.
        var nyVersjonHtml = html.Replace("<dd class=\"title\">Omorganisering av departementsstrukturen fra 1. oktober 2004.</dd>",
            "<dd class=\"title\">Omorganisering av departementsstrukturen fra 1. oktober 2004 (ny versjon).</dd>");
        var nyVersjon = await new RettskildeImportTjeneste(db).ImporterMedUtfallAsync(RegelIde.Kildekonvertering.LovdataKonverterer.Konverter(nyVersjonHtml));
        Assert.Equal(RettskildeImportUtfall.NyVersjon, nyVersjon.Utfall);
        await klassifiserer.KlassifiserAsync(skriv: true, rettskildeId: nyVersjon.RettskildeId);
        var r4 = await livslop.KjorAsync(skriv: true);
        Assert.True(r4.Skriving.Feil.Count == 0, string.Join(" || ", r4.Skriving.Feil));
        Assert.Equal(0, r4.Skriving.OpprettetNye + r4.Skriving.RessortNye + r4.Skriving.AvviklerNye + r4.Skriving.EtterfolgerNye);
        Assert.False(await db.Strukturkanter.AnyAsync(k => k.HjemmelRettskildeId == nyVersjon.RettskildeId));

        // ---- Markøren for rene endringsvedtak (Johanns avgjørelse 6): hodet sier at kilden endrer et annet dokument, OG tittelen har
        // endringsform, OG ingen organiseringstype. Sveipene hopper over den uten å lese tittelen.
        var endring = html.Replace("FOR-2004-10-01-1304", "FOR-2004-10-03-9997").Replace("2004-10-01-1304", "2004-10-03-9997")
            .Replace("<dd class=\"title\">Omorganisering av departementsstrukturen fra 1. oktober 2004.</dd>",
                "<dd class=\"title\">Forskrift om endring i forskrift om noe.</dd>");
        var endringId = await new RettskildeImportTjeneste(db).ImporterAsync(RegelIde.Kildekonvertering.LovdataKonverterer.Konverter(endring));
        var rapportEndring = await klassifiserer.KlassifiserAsync(skriv: true, rettskildeId: endringId);
        Assert.Equal(1, rapportEndring.ReneEndringsvedtak);
        var endringKilde = await db.Rettskilder.AsNoTracking().SingleAsync(r => r.Id == endringId);
        Assert.True(endringKilde.RentEndringsvedtak);
        Assert.Contains("changesToDocuments", endringKilde.RentEndringsvedtakGrunnlag);
        Assert.Contains(endringId, await Sveipeutvalg.StandardUtelattAsync(db));
        Assert.DoesNotContain(id, await Sveipeutvalg.StandardUtelattAsync(db));
    }
}
