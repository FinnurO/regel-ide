using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #384] Migrasjonen <c>KlasseKommuneOgUtledetMedlemskap</c>, kjørt for ekte: en fersk database migreres til #378
/// (<see cref="HistoriskSkjema.GeneriskNavneformOgBinding"/>), så til siste versjon — og tilbake. Spørsmålene: legger Up inn nøyaktig én
/// klasse og én proveniensrad, og ellers ingenting? Snur Down nøyaktig (raden, proveniensen, kolonnene og CHECK-ene borte)? Nekter Down når
/// noe er registrert mot klassen? Og lar Up være å opprette klassen når et fast nodebegrep «kommune» alt finnes (ingen gjetting)?
/// </summary>
[Collection(DataTestCollection.Navn)]
public class KlasseKommuneMigreringTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KlasseKommuneMigreringTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static Task<int> Tell(RegelIdeDbContext db, FormattableString sql) => HistoriskSkjema.TellAsync(db, sql);

    [Fact]
    public async Task Up_legger_inn_en_klasse_og_Down_snur_noyaktig()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_kl384", HistoriskSkjema.GeneriskNavneformOgBinding, leggTilSenereKolonner: false);
        int begreperFor, proveniensFor;
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            begreperFor = await Tell(db, $"SELECT count(*)::int AS \"Value\" FROM begreper");
            proveniensFor = await Tell(db, $"SELECT count(*)::int AS \"Value\" FROM proveniens");
        }

        await HistoriskSkjema.MigrerAsync(conn, null);
        await using (var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(begreperFor + 1, await etter.Begreper.CountAsync());
            Assert.Equal(proveniensFor + 1, await etter.Proveniens.CountAsync());
            var klasse = await etter.Begreper.SingleAsync(b => b.Medlemskapsregel != null);
            Assert.Equal((Klasseregler.KommuneKlasseId, "kommune", Nodetyper.Klasse, Strukturkanter.Tekstfunn, Klasseregler.KommunelovenInndelingEid),
                (klasse.Id, klasse.Term, klasse.Begrepskategori, klasse.Kunnskapsstatus, klasse.LovreferanseEid));
            Assert.Null(klasse.LovkildeId);
        }

        await HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.GeneriskNavneformOgBinding, leggTilSenereKolonner: false);
        await using (var tilbake = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            Assert.Equal(begreperFor, await Tell(tilbake, $"SELECT count(*)::int AS \"Value\" FROM begreper"));
            Assert.Equal(proveniensFor, await Tell(tilbake, $"SELECT count(*)::int AS \"Value\" FROM proveniens"));
            Assert.Equal(0, await Tell(tilbake, $"""
                SELECT count(*)::int AS "Value" FROM information_schema.columns
                WHERE table_name = 'begreper' AND column_name IN ('medlemskapsregel', 'kunnskapsstatus')
                """));
            Assert.Equal(0, await Tell(tilbake, $"""
                SELECT count(*)::int AS "Value" FROM pg_constraint WHERE conname IN ('ck_begreper_medlemskapsregel', 'ck_begreper_kunnskapsstatus')
                """));
        }
    }

    [Fact]
    public async Task Down_nekter_naar_noe_er_registrert_mot_klassen()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_kl384b", null);
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            var annen = await HistoriskSkjema.VirksomhetAsync(db, "Testkommune");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, til_begrep_id, kilde_utenfor_korpus_tekst,
                    kilde_utenfor_korpus_type, kilde_utenfor_korpus_dokumentasjon, status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
                VALUES ({Guid.NewGuid()}, 'M', 'medlem_av', {annen}, {Klasseregler.KommuneKlasseId}, 'test', 'register', 'primaer',
                    'godkjent', 'manuell', 'test', now())
                """);
        }
        var feil = await Assert.ThrowsAnyAsync<Exception>(() =>
            HistoriskSkjema.MigrerAsync(conn, HistoriskSkjema.GeneriskNavneformOgBinding, leggTilSenereKolonner: false));
        Assert.Contains("Issue #384 Down", feil.Message);
    }

    [Fact]
    public async Task Up_oppretter_ingenting_naar_et_fast_nodebegrep_kommune_alt_finnes()
    {
        var conn = await HistoriskSkjema.NyDatabaseAsync(_fixture, "regelide_kl384c", HistoriskSkjema.GeneriskNavneformOgBinding, leggTilSenereKolonner: false);
        var eksisterende = Guid.NewGuid();
        await using (var db = new RegelIdeDbContext(HistoriskSkjema.Options(conn)))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO begreper ("Id", term, gjelder_for, status, opprettet_av, begrepskategori)
                VALUES ({eksisterende}, 'kommune', {Array.Empty<string>()}, 'publisert', 'test', 'gruppe')
                """);
        }
        await HistoriskSkjema.MigrerAsync(conn, null);
        await using var etter = new RegelIdeDbContext(HistoriskSkjema.Options(conn));
        Assert.False(await etter.Begreper.AnyAsync(b => b.Medlemskapsregel != null));
        Assert.Equal(1, await etter.Begreper.CountAsync(b => b.Term == "kommune"));
        Assert.Null((await etter.Begreper.SingleAsync(b => b.Id == eksisterende)).Medlemskapsregel);
    }
}
