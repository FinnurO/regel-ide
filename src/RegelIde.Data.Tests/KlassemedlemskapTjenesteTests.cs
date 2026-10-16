using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #384, Johanns beslutning 2026-10-16] Den felles klassen «kommune» med utledet medlemskap, mot ekte embedded Postgres. Spørsmålene:
/// finnes klassen etter migrasjonen, med hjemmel (kommuneloven § 2-1) og kunnskapsstatus? Hvem er medlem — og er svaret UTLEDET av
/// områderegisteret (ingen M-kanter lagret), med territoriekanten som premiss? Blir en tingrett med ansvarsområde til kommunen holdt utenfor,
/// og blir en kommune uten (eller med flere) rettssubjekt et synlig hull, ikke et gjettet medlem? Og bruker pliktoppslaget medlemskapet?
/// <para>Testbasen er delt: kommunenumrene er unike per test, og påstandene gjelder bare testens egne rader.</para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class KlassemedlemskapTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KlassemedlemskapTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Unik(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    private static async Task<Guid> KommuneomradeAsync(RegelIdeDbContext db, string kode)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Omrade, Omradetype = Omradetyper.Kommune, Omradekode = kode, Term = Unik("Testkommune"),
            Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        await db.SaveChangesAsync();
        return b.Id;
    }

    private static async Task<Guid> AktorAsync(RegelIdeDbContext db, string navn, string? kommunenummer)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik(navn), Aktortype = "rettssubjekt", Kommunenummer = kommunenummer };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    private static async Task<Guid> TerritorieAsync(StrukturkantTjeneste kanter, Guid aktor, Guid omrade) =>
        (await kanter.OpprettAsync(new NyStrukturkant(Strukturkanter.Ansvarsomrade, "har_ansvarsomrade", Kantnode.Virksomhet(aktor), Kantnode.Begrep(omrade),
            KildeUtenforKorpusTekst: "Enhetsregisteret (test)", KildeUtenforKorpusType: Strukturkanter.Register,
            KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer), "test")).Kant.Id;

    [Fact]
    public async Task Klassen_kommune_finnes_med_hjemmel_kunnskapsstatus_og_regel()
    {
        await using var db = _fixture.NyDbContext();
        var klasse = await new KlassemedlemskapTjeneste(db, new StrukturkantTjeneste(db)).KlasseForRegelAsync(Klasseregler.OmraderegisterKommune);
        Assert.NotNull(klasse);
        Assert.Equal(Klasseregler.KommuneKlasseId, klasse.Id);
        Assert.Equal(("kommune", Nodetyper.Klasse, (Guid?)null), (klasse.Term, klasse.Begrepskategori, klasse.LovkildeId));
        Assert.Equal(Klasseregler.KommunelovenParagraf21Eid, klasse.LovreferanseEid);
        Assert.Equal(Strukturkanter.Tekstfunn, klasse.Kunnskapsstatus);
        Assert.Equal(1, await db.Proveniens.CountAsync(p => p.EntitetId == klasse.Id && p.EndretAv == KlasseKommuneMigrering.EndretAv));

        // CHECK-ene: regelen bare på klasser, og tekstfunn krever hjemmel.
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Rolle, Term = Unik("rolle"), Medlemskapsregel = Klasseregler.OmraderegisterKommune,
            Status = "publisert", OpprettetAv = "test",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Klasse, Term = Unik("klasse"), Kunnskapsstatus = Strukturkanter.Tekstfunn,
            Status = "publisert", OpprettetAv = "test",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Medlemskapet_utledes_av_omraderegisteret_med_premiss_og_hull_uten_gjetting()
    {
        await using var db = _fixture.NyDbContext();
        var kanter = new StrukturkantTjeneste(db);
        var tjeneste = new KlassemedlemskapTjeneste(db, kanter);
        string Kode() => "T" + Guid.NewGuid().ToString("N")[..7];
        var (k1, k2, k3) = (Kode(), Kode(), Kode());

        // Kommune 1: rettssubjektet (samme kommunenummer) og en tingrett med ansvarsområde hit (annet kommunenummer).
        var omr1 = await KommuneomradeAsync(db, k1);
        var kommune1 = await AktorAsync(db, "Testkommune", k1);
        var territorie1 = await TerritorieAsync(kanter, kommune1, omr1);
        var tingrett = await AktorAsync(db, "Test tingrett", Kode());
        await TerritorieAsync(kanter, tingrett, omr1);
        // Kommune 2: ingen aktør med samme kommunenummer.
        var omr2 = await KommuneomradeAsync(db, k2);
        // Kommune 3: to aktører med samme kommunenummer og ansvarsområde hit.
        var omr3 = await KommuneomradeAsync(db, k3);
        await TerritorieAsync(kanter, await AktorAsync(db, "Dublett A", k3), omr3);
        await TerritorieAsync(kanter, await AktorAsync(db, "Dublett B", k3), omr3);

        var svar = await tjeneste.UtledAsync(Klasseregler.KommuneKlasseId);
        Assert.NotNull(svar);
        Assert.Equal((Strukturkanter.Avledet, Strukturkanter.Tekstfunn, Klasseregler.KommunelovenParagraf21Eid),
            (svar.Kunnskapsstatus, svar.KlassensKunnskapsstatus, svar.Hjemmel));
        var medlem = Assert.Single(svar.Medlemmer, m => m.Kommunenummer == k1);
        Assert.Equal((kommune1, omr1, territorie1, true), (medlem.VirksomhetId, medlem.OmradeId, medlem.PremissKantId, medlem.Validert));
        Assert.DoesNotContain(svar.Medlemmer, m => m.VirksomhetId == tingrett);
        Assert.DoesNotContain(svar.Medlemmer, m => m.Kommunenummer == k2 || m.Kommunenummer == k3);
        Assert.Contains(svar.Hull, h => h.StartsWith(k2) && h.Contains("ikke funnet"));
        Assert.Contains(svar.Hull, h => h.StartsWith(k3) && h.Contains("ikke entydig"));
        // Ingenting er lagret: ingen M-kant til klassen fra testens aktører.
        Assert.False(await db.Strukturkanter.AnyAsync(k => k.Kategori == Strukturkanter.Medlemskap && k.TilBegrepId == Klasseregler.KommuneKlasseId
                                                           && (k.FraVirksomhetId == kommune1 || k.FraVirksomhetId == tingrett)));

        // Én aktør: samme utregning, bare den aktørens medlemskap.
        Assert.Equal(kommune1, Assert.Single((await tjeneste.UtledAsync(Klasseregler.KommuneKlasseId, kommune1))!.Medlemmer).VirksomhetId);
        Assert.Empty((await tjeneste.UtledAsync(Klasseregler.KommuneKlasseId, tingrett))!.Medlemmer);
        // En klasse uten regel har registrert medlemskap — ikke utledet.
        var vanlig = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(Nodetyper.Klasse, null, Unik("klasse"), "test");
        Assert.Null(await tjeneste.UtledAsync(vanlig.Id));
    }

    [Fact]
    public async Task Pliktoppslaget_bruker_det_utledede_medlemskapet_med_territoriekanten_som_premiss()
    {
        await using var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var kanter = new StrukturkantTjeneste(db);
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var paragrafEid = (await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync()).Eid;
        var kode = "T" + Guid.NewGuid().ToString("N")[..7];
        var omr = await KommuneomradeAsync(db, kode);
        var kommune = await AktorAsync(db, "Testkommune", kode);
        var territorie = await TerritorieAsync(kanter, kommune, omr);
        // «Kommunen skal …» som én P-kant fra den felles klassen. Ingen M-kant fra kommunen.
        var plikt = await kanter.OpprettAsync(new NyStrukturkant(Strukturkanter.Plikt, "avtale", Kantnode.Begrep(Klasseregler.KommuneKlasseId),
            null, HjemmelRettskildeId: lovId, HjemmelEid: paragrafEid, Modalitet: "skal"), "test");

        var svar = await new OmradeOppslagTjeneste(db, kanter).PlikterForKommunenummerAsync(kode, "avtale");
        var treff = Assert.Single(svar!.Plikter, t => t.Kant.Id == plikt.Kant.Id);
        Assert.Equal(("medlem_av", (string?)null), (treff.Grunnlag, treff.GrunnlagHull));
        Assert.Equal(Strukturkanter.Avledet, treff.Kunnskapsstatus);
        Assert.Contains(territorie, treff.Premisser);
    }
}
