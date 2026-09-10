using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, hjemmel-validering-runden, 2026-09-10, issue #233] Kontrollen av at hjemmelrelasjonene peker
/// på bestemmelser som FINNES, og rettelsen av de eId-ene som har fått et setningstegn med fra
/// kilden.
///
/// <para>
/// Det som må testes er at de fire utfallene faktisk skilles fra hverandre. Et enkelt
/// «gyldig/ugyldig» ville blandet ekte parsefeil med to legitime tilstander — en delegeringsforskrift
/// hjemlet i et HELT dokument, og en lov som er referert men ikke importert — og gjort tallet
/// ubrukelig som kvalitetsmål. Testene under holder de fire fra hverandre hver for seg.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class HjemmelValideringTjenesteTests(EmbeddedPostgresFixture fixture)
{
    private static int _lopenummer = 9000;

    /// <summary>En lov med ÉN paragrafnode (§1), og en forskrift som er hjemlet i den ved
    /// <paramref name="hjemmelSuffiks"/>. Loven får ingen noder når <paramref name="medNoder"/> er
    /// false — det er hvordan en referanse-stub ser ut.</summary>
    private async Task<(Guid ForskriftId, Guid LovId, string LovEli)> OpprettAsync(
        RegelIdeDbContext db, string hjemmelSuffiks, bool medNoder = true)
    {
        var nr = Interlocked.Increment(ref _lopenummer);
        var lovEli = $"https://lovdata.no/eli/lov/1976/12/17/{nr}/nor";
        var lovId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = lovId, Doctype = "act", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = $"Testlov {nr}", Eli = lovEli,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        if (medNoder)
        {
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = lovId, Eid = $"{lovEli}/§1", KildeId = "§1",
                NodeType = "paragraf", Nummer = "§ 1", Overskrift = "Virkeområde",
            });
        }

        var forskriftId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = forskriftId, Doctype = "act", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = $"Testforskrift {nr}", Eli = $"https://lovdata.no/eli/forskrift/1980/05/23/{nr}/nor",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeHjemler.Add(new RettskildeHjemmelEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = forskriftId,
            HjemmelEid = lovEli + hjemmelSuffiks, HjemmelRettskildeId = lovId, Sorteringsrekkefolge = 0,
        });
        await db.SaveChangesAsync();
        return (forskriftId, lovId, lovEli);
    }

    private static async Task<HjemmelValideringTjeneste.Utfall> UtfallForAsync(
        RegelIdeDbContext db, Guid forskriftId)
    {
        var eid = await db.RettskildeHjemler.Where(h => h.RettskildeId == forskriftId).Select(h => h.HjemmelEid).SingleAsync();
        var resultat = await new HjemmelValideringTjeneste(db).ValiderAsync(maksFeilrader: 5000);
        // Feilradene bærer utfallet; de tre andre kategoriene må utledes av at raden IKKE er en feil.
        var feil = resultat.Feilrader.FirstOrDefault(r => r.RettskildeId == forskriftId && r.HjemmelEid == eid);
        return feil?.Utfall ?? HjemmelValideringTjeneste.Utfall.Gyldig;
    }

    [Fact]
    public async Task Eid_som_treffer_en_node_er_gyldig()
    {
        await using var db = fixture.NyDbContext();
        var (forskriftId, _, _) = await OpprettAsync(db, "/§1");

        Assert.Equal(HjemmelValideringTjeneste.Utfall.Gyldig, await UtfallForAsync(db, forskriftId));
    }

    [Fact]
    public async Task Eid_med_setningstegn_rapporteres_som_manglende_node()
    {
        // Det ekte tilfellet: Jan Mayen-forskriften har «…/§1.» fordi Lovdatas href gjentar den viste
        // «Hjemmel:»-strengen med punktum. Noden heter «§1», så referansen kan ikke følges.
        await using var db = fixture.NyDbContext();
        var (forskriftId, _, _) = await OpprettAsync(db, "/§1.");

        Assert.Equal(HjemmelValideringTjeneste.Utfall.NodeFinnesIkke, await UtfallForAsync(db, forskriftId));
    }

    [Fact]
    public async Task Dokumentniva_er_legitimt_ikke_en_feil()
    {
        // En delegeringsforskrift er hjemlet i et HELT dokument, ikke i én bestemmelse (målt: 1711
        // dokumenter). Skal ALDRI havne blant feilene.
        await using var db = fixture.NyDbContext();
        var (forskriftId, _, _) = await OpprettAsync(db, "");

        var resultat = await new HjemmelValideringTjeneste(db).ValiderAsync(maksFeilrader: 5000);

        Assert.DoesNotContain(resultat.Feilrader, r => r.RettskildeId == forskriftId);
        Assert.True(resultat.Dokumentniva >= 1);
    }

    [Fact]
    public async Task Lov_uten_noder_er_ikke_importert_ikke_en_feil()
    {
        // En referanse-stub har ingen noder. Referansen kan ikke verifiseres internt, men den er ikke
        // dermed gal — og skal derfor ikke telles som feil. Det er nettopp DETTE tallet som avgjør om
        // en ekstern sjekk mot Lovdata er verdt å bygge.
        await using var db = fixture.NyDbContext();
        var (forskriftId, _, _) = await OpprettAsync(db, "/§1", medNoder: false);

        var resultat = await new HjemmelValideringTjeneste(db).ValiderAsync(maksFeilrader: 5000);

        Assert.DoesNotContain(resultat.Feilrader, r => r.RettskildeId == forskriftId);
        Assert.True(resultat.MaaletIkkeImportert >= 1);
    }

    [Fact]
    public async Task Retter_setningstegn_og_referansen_loser_seg()
    {
        await using var db = fixture.NyDbContext();
        var (forskriftId, _, lovEli) = await OpprettAsync(db, "/§1.");
        var tjeneste = new HjemmelValideringTjeneste(db);

        var rettelse = await tjeneste.RettSetningstegnAsync();

        Assert.True(rettelse.Rettet >= 1);
        Assert.True(rettelse.RettetOgLoserNa >= 1);
        var eid = await db.RettskildeHjemler.Where(h => h.RettskildeId == forskriftId).Select(h => h.HjemmelEid).SingleAsync();
        Assert.Equal($"{lovEli}/§1", eid);
        Assert.Equal(HjemmelValideringTjeneste.Utfall.Gyldig, await UtfallForAsync(db, forskriftId));
    }

    [Fact]
    public async Task Rettelsen_er_idempotent()
    {
        await using var db = fixture.NyDbContext();
        await OpprettAsync(db, "/§1.");
        var tjeneste = new HjemmelValideringTjeneste(db);

        await tjeneste.RettSetningstegnAsync();
        var andre = await tjeneste.RettSetningstegnAsync();

        Assert.Equal(0, andre.Rettet);
        Assert.Equal(0, andre.SlettetSomDuplikat);
    }

    [Fact]
    public async Task Duplikat_slettes_i_stedet_for_a_kollidere_med_unikhetsindeksen()
    {
        // Har ett dokument BÅDE «§1» og «§1.» — samme hjemmel skrevet to ganger, én gang med
        // setningstegn — ville trimmingen brutt ux_rettskilde_hjemler_rettskilde_id_hjemmel_eid.
        // Riktig utfall er å slette den trimmede duplikaten: de betegnet alltid samme bestemmelse.
        await using var db = fixture.NyDbContext();
        var (forskriftId, lovId, lovEli) = await OpprettAsync(db, "/§1");
        db.RettskildeHjemler.Add(new RettskildeHjemmelEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = forskriftId,
            HjemmelEid = $"{lovEli}/§1.", HjemmelRettskildeId = lovId, Sorteringsrekkefolge = 1,
        });
        await db.SaveChangesAsync();

        var rettelse = await new HjemmelValideringTjeneste(db).RettSetningstegnAsync();

        Assert.True(rettelse.SlettetSomDuplikat >= 1);
        var eider = await db.RettskildeHjemler.Where(h => h.RettskildeId == forskriftId).Select(h => h.HjemmelEid).ToListAsync();
        Assert.Equal([$"{lovEli}/§1"], eider);
    }
}
