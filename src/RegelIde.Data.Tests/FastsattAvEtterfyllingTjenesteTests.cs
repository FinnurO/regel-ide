using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, fastsatt-av-runden, 2026-09-10, issue #215] Etterfyllingen som gir eksisterende rettskilder
/// en fastsetter, og oppslaget som kobler navnet til en virksomhet.
///
/// <para>
/// Det viktigste som testes er kriterium 7: <see cref="RettskildeEntitet.VirksomhetId"/> skal være
/// UENDRET. Det feltet betyr «virksomhetens eget, private dokument» — å sette det på en nasjonal
/// forskrift ville skjult forskriften for alle andre virksomheter og for sveipene. Johann var
/// eksplisitt om at fastsetteren skal vises UTEN å røre eierskapet.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class FastsattAvEtterfyllingTjenesteTests(EmbeddedPostgresFixture fixture)
{
    private static int _lopenummer = 7000;

    private static async Task<Guid> OpprettForskriftAsync(RegelIdeDbContext db, string hjemmelslinje, Guid? virksomhetId = null)
    {
        var nr = Interlocked.Increment(ref _lopenummer);
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "act", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = $"Testforskrift {nr}", Eli = $"https://lovdata.no/eli/forskrift/2026/02/03/{nr}/nor",
            AnnetOmDokumentet = hjemmelslinje, VirksomhetId = virksomhetId,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Fyller_fastsetteren_og_skiller_visningstekst_fra_oppslagsnavn()
    {
        await using var db = fixture.NyDbContext();
        var id = await OpprettForskriftAsync(db,
            "Hjemmel: Fastsatt av styret ved Norges teknisk-naturvitenskapelige universitet (NTNU) "
            + "3. februar 2026 med hjemmel i lov 8. mars 2024 nr. 9 … § 13-1 fjerde ledd.");

        await new FastsattAvEtterfyllingTjeneste(db).KjorAsync();

        var r = await db.Rettskilder.SingleAsync(x => x.Id == id);
        Assert.Equal("styret ved Norges teknisk-naturvitenskapelige universitet (NTNU)", r.FastsattAv);
        Assert.Equal("Norges teknisk-naturvitenskapelige universitet (NTNU)", r.FastsattAvOrgannavn);
    }

    [Fact]
    public async Task Rorer_ikke_virksomhetId_verken_null_eller_satt()
    {
        // Kriterium 7, målt i stedet for antatt — begge retninger: en delt/nasjonal rad skal IKKE få
        // en eier, og en rad som ALT har en eier skal beholde nøyaktig den.
        await using var db = fixture.NyDbContext();
        var eier = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Eier-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(eier);
        await db.SaveChangesAsync();

        var delt = await OpprettForskriftAsync(db, "Hjemmel: Fastsatt av Mattilsynet 1. mai 2020 med hjemmel i …");
        var eid = await OpprettForskriftAsync(db, "Hjemmel: Fastsatt av Mattilsynet 1. mai 2020 med hjemmel i …", eier.Id);

        var resultat = await new FastsattAvEtterfyllingTjeneste(db).KjorAsync();

        Assert.Equal(resultat.AntallRettskilder, resultat.VirksomhetIdUendret);
        Assert.Null((await db.Rettskilder.SingleAsync(x => x.Id == delt)).VirksomhetId);
        Assert.Equal(eier.Id, (await db.Rettskilder.SingleAsync(x => x.Id == eid)).VirksomhetId);
    }

    [Fact]
    public async Task Kgl_res_far_tekst_men_bevisst_ingen_organnavn()
    {
        // Kongen i statsråd er et GRUPPEBEGREP, ikke en virksomhet — teksten skal stå, men det finnes
        // ikke noe navn å slå opp mot katalogen (issue #215 kriterium 3).
        await using var db = fixture.NyDbContext();
        var id = await OpprettForskriftAsync(db, "Hjemmel: Fastsatt ved kgl.res. 7. desember 2012 med hjemmel i …");

        var resultat = await new FastsattAvEtterfyllingTjeneste(db).KjorAsync();

        var r = await db.Rettskilder.SingleAsync(x => x.Id == id);
        Assert.Equal("kgl.res.", r.FastsattAv);
        Assert.Null(r.FastsattAvOrgannavn);
        Assert.True(resultat.KongenIStatsrad >= 1);
    }

    [Fact]
    public async Task Er_idempotent_andre_kjoring_skriver_ingenting()
    {
        await using var db = fixture.NyDbContext();
        await OpprettForskriftAsync(db, "Hjemmel: Fastsatt av Vegdirektoratet 3. mars 2003 med hjemmel i …");
        var tjeneste = new FastsattAvEtterfyllingTjeneste(db);

        await tjeneste.KjorAsync();
        var andre = await tjeneste.KjorAsync();

        Assert.True(andre.AlleredeSatt >= 1);
    }

    [Fact]
    public async Task Navneform_loser_navnet_registernavnet_ikke_finner()
    {
        // Kjernen i at koblingen i det hele tatt virker: Brreg-formen er «NORGES VASSDRAGS- OG
        // ENERGIDIREKTORAT (NVE)», Lovdata skriver «Norges vassdrags- og energidirektorat». Store
        // bokstaver løses av det case-ufølsomme oppslaget, men parentesen gjør at registernavnet
        // alene IKKE matcher — og det er nettopp derfor navneformene finnes.
        await using var db = fixture.NyDbContext();
        var suffiks = Guid.NewGuid().ToString("N")[..6];
        var registernavn = $"TESTDIREKTORATET {suffiks} (TD)";
        var lovdataform = $"Testdirektoratet {suffiks}";
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = registernavn };
        db.Virksomheter.Add(virksomhet);
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhet.Id, Term = lovdataform,
            Definisjon = "Navneform for testdirektoratet.",
            Begrepskategori = "virksomhet", Navneformgrunn = "gjeldende", Status = "publisert",
            VirksomhetReferanseId = virksomhet.Id,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var oppslag = new VirksomhetOppslagTjeneste(db);

        Assert.Null(await oppslag.FinnVirksomhetIdForNavnAsync(lovdataform));
        Assert.Equal(virksomhet.Id, await oppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(lovdataform));
        // Registernavnet skal fortsatt løses direkte.
        Assert.Equal(virksomhet.Id, await oppslag.FinnVirksomhetIdForNavnEllerNavneformAsync(registernavn));
    }

    [Fact]
    public async Task Flertydig_navneform_gir_ingen_kobling()
    {
        // «Ingen gjettet fallback»: deler to virksomheter samme navneform, er navnet ikke et svar.
        await using var db = fixture.NyDbContext();
        var term = $"Fellesnavn-{Guid.NewGuid():N}"[..24];
        foreach (var i in new[] { 1, 2 })
        {
            var v = new Virksomhet { Id = Guid.NewGuid(), Navn = $"FLERTYDIG {i} {Guid.NewGuid():N}" };
            db.Virksomheter.Add(v);
            db.Begreper.Add(new BegrepEntitet
            {
                Id = Guid.NewGuid(), VirksomhetId = v.Id, Term = term,
                Definisjon = "Navneform delt av to virksomheter — konstruert for denne testen.",
                Begrepskategori = "virksomhet", Navneformgrunn = "gjeldende", Status = "publisert", VirksomhetReferanseId = v.Id,
                OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        Assert.Null(await new VirksomhetOppslagTjeneste(db).FinnVirksomhetIdForNavnEllerNavneformAsync(term));
    }
}
