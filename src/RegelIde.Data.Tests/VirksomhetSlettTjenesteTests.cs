using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #157] <see cref="VirksomhetSlettTjeneste"/> mot ekte embedded Postgres — verifiserer at
/// selve DB-skjemaets FK-kaskader (ikke antatt, live-verifisert mot <c>information_schema</c> under
/// byggingen, se klassekommentaren på tjenesten) faktisk fungerer sammen med de eksplisitte
/// rydde-stegene tjenesten selv gjør for de FÅ FK-ene som er <c>NO ACTION</c>/<c>RESTRICT</c>.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetSlettTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetSlettTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn, Guid? overordnetEnhetId = null)
    {
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"{navn}-{Guid.NewGuid():N}", OverordnetEnhetId = overordnetEnhetId };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        return virksomhet.Id;
    }

    [Fact]
    public async Task HentOversiktAsync_teller_riktig_pa_tvers_av_entitetstyper()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, "Testkommunen");
        var annenVirksomhetId = await NyVirksomhetAsync(db, "Nabokommunen");
        await NyVirksomhetAsync(db, "Datterselskap", overordnetEnhetId: virksomhetId);

        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhetId, Doctype = "doc", Kildetype = "Virksomhetsdokument",
            Tittel = "Testdokument", Status = "Gjeldende", OpprettetAv = "test",
            // ck_rettskilder_akn_xml krever enten AknXml satt ELLER Importrolle="referanse" — testen
            // trenger ikke ekte AKN-innhold, så "referanse" er riktig i stedet for defaultverdien "primaer".
            Importrolle = "referanse",
        });
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = virksomhetId,
            Term = "Testnavneform", Status = "publisert", OpprettetAv = "test",
        });
        db.Brukere.Add(new Bruker { Id = Guid.NewGuid(), Navn = "Test Testesen", VirksomhetId = virksomhetId, Rolle = "saksbehandler" });
        db.VirksomhetRelasjoner.Add(new VirksomhetRelasjonEntitet
        {
            Id = Guid.NewGuid(), FraVirksomhetId = annenVirksomhetId, TilVirksomhetId = virksomhetId,
            RelasjonsType = "underlagt", OpprettetAv = "test",
        });
        await db.SaveChangesAsync();

        var tjeneste = new VirksomhetSlettTjeneste(db);
        var oversikt = await tjeneste.HentOversiktAsync(virksomhetId);

        Assert.NotNull(oversikt);
        Assert.Equal(1, oversikt!.Rettskilder);
        Assert.Equal(1, oversikt.Navneformer);
        Assert.Equal(1, oversikt.Brukere);
        Assert.Equal(1, oversikt.VirksomhetRelasjoner);
        Assert.Equal(1, oversikt.UnderliggendeVirksomheter);
        Assert.Equal(0, oversikt.TekstTaggerMedPublisertReferanse);
        Assert.True(oversikt.KanSlettes);
    }

    [Fact]
    public async Task HentOversiktAsync_gir_null_for_ukjent_id()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new VirksomhetSlettTjeneste(db);

        Assert.Null(await tjeneste.HentOversiktAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SlettAsync_gir_FinnesIkke_for_ukjent_id()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new VirksomhetSlettTjeneste(db);

        var resultat = await tjeneste.SlettAsync(Guid.NewGuid());

        Assert.Equal(VirksomhetSlettUtfall.FinnesIkke, resultat.Utfall);
    }

    [Fact]
    public async Task SlettAsync_kaskadesletter_egne_rader_og_nullstiller_barnas_overordnetEnhetId()
    {
        Guid virksomhetId, annenVirksomhetId, datterselskapId, rettskildeId, begrepId, brukerId, relasjonId, proveniensId;
        await using (var db = _fixture.NyDbContext())
        {
            virksomhetId = await NyVirksomhetAsync(db, "Testkommunen");
            annenVirksomhetId = await NyVirksomhetAsync(db, "Nabokommunen");
            datterselskapId = await NyVirksomhetAsync(db, "Datterselskap", overordnetEnhetId: virksomhetId);

            var rettskilde = new RettskildeEntitet
            {
                Id = Guid.NewGuid(), VirksomhetId = virksomhetId, Doctype = "doc", Kildetype = "Virksomhetsdokument",
                Tittel = "Testdokument", Status = "Gjeldende", OpprettetAv = "test",
                Importrolle = "referanse", // ck_rettskilder_akn_xml — se kommentar lenger opp i filen.
            };
            db.Rettskilder.Add(rettskilde);
            rettskildeId = rettskilde.Id;

            var begrep = new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = virksomhetId,
                Term = "Testnavneform", Status = "publisert", OpprettetAv = "test",
            };
            db.Begreper.Add(begrep);
            begrepId = begrep.Id;

            var bruker = new Bruker { Id = Guid.NewGuid(), Navn = "Test Testesen", VirksomhetId = virksomhetId, Rolle = "saksbehandler" };
            db.Brukere.Add(bruker);
            brukerId = bruker.Id;

            // TilVirksomhetId == virksomhetId, RESTRICT i DB — nøyaktig FK-en tjenesten MÅ rydde eksplisitt.
            var relasjon = new VirksomhetRelasjonEntitet
            {
                Id = Guid.NewGuid(), FraVirksomhetId = annenVirksomhetId, TilVirksomhetId = virksomhetId,
                RelasjonsType = "underlagt", OpprettetAv = "test",
            };
            db.VirksomhetRelasjoner.Add(relasjon);
            relasjonId = relasjon.Id;

            var proveniens = new ProveniensEntitet
            {
                Id = Guid.NewGuid(), VirksomhetId = virksomhetId, EntitetType = "begrep", EntitetId = begrepId,
                EndretAv = "test", Handling = "opprettet",
            };
            db.Proveniens.Add(proveniens);
            proveniensId = proveniens.Id;

            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.NyDbContext())
        {
            var tjeneste = new VirksomhetSlettTjeneste(db);
            var resultat = await tjeneste.SlettAsync(virksomhetId);
            Assert.Equal(VirksomhetSlettUtfall.Slettet, resultat.Utfall);
        }

        await using (var db = _fixture.NyDbContext())
        {
            Assert.False(await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId));
            Assert.False(await db.Rettskilder.AnyAsync(r => r.Id == rettskildeId));
            Assert.False(await db.Begreper.AnyAsync(b => b.Id == begrepId));
            Assert.False(await db.Brukere.AnyAsync(b => b.Id == brukerId));
            Assert.False(await db.VirksomhetRelasjoner.AnyAsync(r => r.Id == relasjonId));

            // Andre virksomheten (relasjonens FRA-side) skal IKKE ha blitt rørt.
            Assert.True(await db.Virksomheter.AnyAsync(v => v.Id == annenVirksomhetId));

            // Datterselskapet skal fortsatt finnes — mister KUN foreldrekoblingen.
            var datterselskap = await db.Virksomheter.SingleAsync(v => v.Id == datterselskapId);
            Assert.Null(datterselskap.OverordnetEnhetId);

            // Proveniens er en logg — raden består, men FK-en til virksomheten er nullstilt.
            var proveniens = await db.Proveniens.SingleAsync(p => p.Id == proveniensId);
            Assert.Null(proveniens.VirksomhetId);
        }
    }

    [Fact]
    public async Task SlettAsync_blokkerer_og_sletter_ingenting_ved_publisert_tekst_tagg_referanse()
    {
        Guid virksomhetId, rettskildeId, taggId;
        await using (var db = _fixture.NyDbContext())
        {
            virksomhetId = await NyVirksomhetAsync(db, "Testkommunen");

            var rettskilde = new RettskildeEntitet
            {
                Id = Guid.NewGuid(), VirksomhetId = null, Doctype = "doc", Kildetype = "Lov",
                Tittel = "Testlov", Status = "Gjeldende", OpprettetAv = "test",
                Importrolle = "referanse", // ck_rettskilder_akn_xml — se kommentar lenger opp i filen.
            };
            db.Rettskilder.Add(rettskilde);
            rettskildeId = rettskilde.Id;

            var tagg = new TekstTaggEntitet
            {
                Id = Guid.NewGuid(), VirksomhetId = virksomhetId, RettskildeId = rettskildeId, NodeEid = "§1",
                StartOffset = 0, EndOffset = 4, QuotePrefix = "", QuoteExact = "test", QuoteSuffix = "",
                NodeTekstHash = "test-hash", Kind = "begrep", RefId = Guid.NewGuid(), OpprettetAv = "test",
            };
            db.TekstTagger.Add(tagg);
            taggId = tagg.Id;

            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.NyDbContext())
        {
            var tjeneste = new VirksomhetSlettTjeneste(db);
            var resultat = await tjeneste.SlettAsync(virksomhetId);

            Assert.Equal(VirksomhetSlettUtfall.BlokkertAvPublisertReferanse, resultat.Utfall);
            Assert.NotNull(resultat.Detalj);
        }

        await using (var db = _fixture.NyDbContext())
        {
            // Ingenting slettet — verken virksomheten eller taggen.
            Assert.True(await db.Virksomheter.AnyAsync(v => v.Id == virksomhetId));
            Assert.True(await db.TekstTagger.AnyAsync(t => t.Id == taggId));
        }
    }
}
