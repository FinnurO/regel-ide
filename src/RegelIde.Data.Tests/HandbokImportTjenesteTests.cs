using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="HandbokImportTjeneste"/> mot BÅDE ekte håndbok-fixtures (data/kilder/raw-handbok/,
/// se README der) — Bergens retningslinjer (kapittel/punkt-tre, flere kryssreferanser) og Bergens
/// forskrift (den ANDRE dokumentstrukturen, tallpunktum-seksjoner, ingen hjemlet_i-treff). Samme
/// SQLite-profil-mønster som <see cref="NettsideDokumentgrafTests"/>.
/// </summary>
public sealed class HandbokImportTjenesteTests : IAsyncLifetime
{
    private string _filsti = "";
    private Guid _virksomhetId;

    public Task InitializeAsync()
    {
        _filsti = Path.Combine(Path.GetTempPath(), $"regelide-handboktest-{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_filsti)) File.Delete(_filsti);
        return Task.CompletedTask;
    }

    private async Task<RegelIdeDbContext> NyBaseAsync()
    {
        var db = new RegelIdeDbContext(new DbContextOptionsBuilder<RegelIdeDbContext>().UseSqlite($"Data Source={_filsti}").Options);
        await Databaseoppsett.SorgForSkjemaAsync(db);
        _virksomhetId = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = _virksomhetId, Navn = "Testvirksomhet", OpprettetTidspunkt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Importerer_bergen_retningslinjer_med_bevart_nodetre_og_tekstHash()
    {
        await using var db = await NyBaseAsync();
        var tjeneste = new HandbokImportTjeneste(db);
        var parset = HandbokTekstParser.Parse(Testdata.LesBergenRetningslinjer());

        var resultat = await tjeneste.ImporterAsync(
            parset, "Retningslinjer for tildeling av salgs- og skjenkebevillinger i Bergen kommune for perioden 2024-2028",
            _virksomhetId, kildetype: "Virksomhetsdokument", doctype: "doc", opprettetAv: "Kari Jurist",
            url: "https://www.bergen.kommune.no/api/rest/filer/V51903878", interntDokNr: "SD-24-113", revisjonsnr: "01",
            vedtattAv: "Bystyret", vedtaksdato: new DateOnly(2024, 6, 19), gyldigTil: new DateOnly(2028, 7, 1),
            normativVirkning: "bindende_forvaltning");

        Assert.Equal(parset.Noder.Count, resultat.AntallNoder);

        var lagredeNoder = await db.RettskildeNoder.Where(n => n.RettskildeId == resultat.RettskildeId).ToListAsync();
        Assert.Equal(parset.Noder.Count, lagredeNoder.Count);

        // Alle 10 kapitler + punkt-noder er der, med UENDRET Eid (parserens egen konvensjon, ikke
        // HandbokForfatterTjenestes LagEid) og bevart TekstHash (ikke regnet på nytt).
        foreach (var kildeNode in parset.Noder)
        {
            var lagret = lagredeNoder.Single(n => n.Eid == kildeNode.Eid);
            Assert.Equal(kildeNode.NodeType, lagret.NodeType);
            Assert.Equal(kildeNode.Nummer, lagret.Nummer);
            Assert.Equal(kildeNode.Overskrift, lagret.Overskrift);
            Assert.Equal(kildeNode.Tekst, lagret.Tekst);
            Assert.Equal(kildeNode.TekstHash, lagret.TekstHash);
            Assert.Equal(kildeNode.SorteringsRekkefolge, lagret.Sorteringsrekkefolge);
        }

        // ParentNodeId faktisk koblet, ikke bare Eid-strengen — kap4/pkt4.1 sin forelder er den EKTE kap4-raden.
        var punkt41 = lagredeNoder.Single(n => n.Eid == "kap4/pkt4.1");
        var kap4 = lagredeNoder.Single(n => n.Eid == "kap4");
        Assert.Equal(kap4.Id, punkt41.ParentNodeId);

        // Minst én kryssrefererer-referanse (4.8 → 4.7) faktisk koblet til en REELL NodeId — ikke bare en streng.
        var punkt48 = lagredeNoder.Single(n => n.Eid == "kap4/pkt4.8");
        var punkt47 = lagredeNoder.Single(n => n.Eid == "kap4/pkt4.7");
        Assert.True(resultat.AntallKryssreferanserKoblet >= 1);
        var referanser = await db.RettskildeReferanser.Where(r => r.FraNodeId == punkt48.Id).ToListAsync();
        var kryssref = Assert.Single(referanser, r => r.TilEid == "kap4/pkt4.7");
        Assert.Equal(resultat.RettskildeId, kryssref.TilRettskildeId);
        Assert.Equal("import", kryssref.Opprinnelse);
        // TilEid er en REELL node i det importerte treet — ikke en uløst streng.
        Assert.Contains(lagredeNoder, n => n.Eid == kryssref.TilEid && n.Id == punkt47.Id);

        var rettskilde = await db.Rettskilder.SingleAsync(r => r.Id == resultat.RettskildeId);
        // Importrolle="primaer" (IKKE "referanse", et ekte funn — se HandbokImportTjeneste-klassekommentaren):
        // ellers ville denne håndboken vært usynlig i RettskildeRepository.AlleRettskilderAsync, som
        // filtrerer eksplisitt på Importrolle=="primaer".
        Assert.Equal("primaer", rettskilde.Importrolle);
        Assert.NotNull(rettskilde.AknXml);
        Assert.Equal("SD-24-113", rettskilde.InterntDokNr);
        Assert.Equal("Bystyret", rettskilde.VedtattAv);

        // Proveniens-raden er skrevet (dual-write-mønsteret).
        Assert.True(await db.Proveniens.AnyAsync(p => p.EntitetType == "rettskilde" && p.EntitetId == resultat.RettskildeId && p.Handling == "opprettet"));

        // Hjemlet_i mot "alkoholloven"/"Alkoholloven" finnes i teksten (se HandbokTekstParserTests), men
        // INGEN lov er importert i denne testen — derfor forventet ULØST, ikke gjettet.
        Assert.True(resultat.AntallHjemletILovnavnUlost >= 1);
        Assert.False(await db.HandbokRettskildeomfang.AnyAsync(o => o.HandbokId == resultat.RettskildeId));
    }

    [Fact]
    public async Task Importerer_bergen_forskrift_med_bevart_nodetre()
    {
        await using var db = await NyBaseAsync();
        var tjeneste = new HandbokImportTjeneste(db);
        var parset = HandbokTekstParser.Parse(Testdata.LesBergenForskrift());

        var resultat = await tjeneste.ImporterAsync(
            parset, "Forskrift om salgs-, skjenke- og åpningstider i Bergen kommune for perioden 2024-2028",
            _virksomhetId, kildetype: "Forskrift", doctype: "act", opprettetAv: "Kari Jurist",
            url: "https://www.bergen.kommune.no/api/rest/filer/V51903879", interntDokNr: "SD-24-114", revisjonsnr: "01",
            vedtattAv: "Bystyret", vedtaksdato: new DateOnly(2024, 6, 19), gyldigTil: new DateOnly(2028, 7, 1));

        Assert.Equal(parset.Noder.Count, resultat.AntallNoder);
        Assert.True(resultat.AntallNoder > 0);

        var lagredeNoder = await db.RettskildeNoder.Where(n => n.RettskildeId == resultat.RettskildeId).ToListAsync();
        Assert.Equal(parset.Noder.Count, lagredeNoder.Count);

        // Toppnivå-seksjonene ("1. SALGSTID …") er lagret som ekte "kapittel"-noder (§ README-funnet:
        // TallpunktumSeksjonMønster, behandlet identisk med Kapittel) — ikke tapt/sammensmeltet.
        Assert.Contains(lagredeNoder, n => n.NodeType == "kapittel");

        // Ingen hjemlet_i-kandidater i denne fixturen (README: forskriften siterer ikke loven i egen
        // brødtekst) — 0 uløste er derfor korrekt her, i motsetning til retningslinjene.
        Assert.Equal(0, resultat.AntallHjemletILovnavnUlost);

        // EKTE FUNN (se HandbokImportTjeneste-klassekommentaren): PDF-linjebrytningen legger "18.00."
        // alene på en linje midt i kap1s løpetekst, som HandbokTekstParser.PunktMønster feiltolker som
        // et gyldig punktnummer med en ALDRI-eksisterende "kap18"-forelder. Regresjonsbevis: importen
        // krasjer IKKE (ingen FK-brudd/Guid.Empty), noden importeres i stedet som en rot-node.
        Assert.Equal(1, resultat.AntallNoderMedUlostForelder);
        var foreldreloosNode = lagredeNoder.Single(n => n.Eid == "kap18/pkt18.00");
        Assert.Null(foreldreloosNode.ParentNodeId);
    }

    [Fact]
    public async Task Reimport_av_samme_tittel_og_kildetype_er_idempotent()
    {
        await using var db = await NyBaseAsync();
        var tjeneste = new HandbokImportTjeneste(db);
        var parset = HandbokTekstParser.Parse(Testdata.LesBergenForskrift());

        var forste = await tjeneste.ImporterAsync(parset, "Samme forskrift", _virksomhetId, "Forskrift", "act");
        var andre = await tjeneste.ImporterAsync(parset, "Samme forskrift", _virksomhetId, "Forskrift", "act");

        Assert.Equal(forste.RettskildeId, andre.RettskildeId);
        Assert.Equal(1, await db.Rettskilder.CountAsync(r => r.Tittel == "Samme forskrift"));
    }
}
