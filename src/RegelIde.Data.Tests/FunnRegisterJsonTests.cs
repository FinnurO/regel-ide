using System.Text.Json;
using System.Text.Json.Nodes;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #429 AK3] Det som står i <c>nettside/data/funn/register.json</c>, publiseres når PR-en flettes (deploy-nettside.yml). Testen
/// sier: fila inneholder bare funn som er godkjent for publisering i seed-fila — i dag bare funn 1 — og hvert funn er NØYAKTIG det
/// <see cref="FunnTjeneste.ByggOffentligInnhold"/> gir for den godkjente teksten. Endres teksten i én av dem uten den andre, blir testen rød.
/// <para>
/// Uten database: funnet bygges i minnet fra seed-fila, med alle koblingene slik de slås opp i en base der aktørene finnes (den delte
/// basen). En eksport fra en base der en aktør mangler, gir færre aktører — det er riktig oppførsel, og da skal fila skrives på nytt
/// med eksportskriptet, ikke testen tilpasses.
/// </para>
/// </summary>
public class FunnRegisterJsonTests
{
    private static string FinnRepoRot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "nettside", "data", "README.md"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Fant ikke repo-roten (nettside/data).");
    }

    private static FunnEntitet IMinnet(FunnSeed.SeedFunn s)
    {
        var f = new FunnEntitet
        {
            Id = Guid.NewGuid(), EksternNokkel = s.Nokkel, Kategori = s.Kategori, Undertype = s.Undertype, Tittel = s.Tittel,
            Beskrivelse = s.Beskrivelse, Vurdering = s.Juristvurdering?.Vurdering, VurdertAvType = s.Juristvurdering?.VurdertAvType,
            OppdagelsesKilde = s.OppdagelsesKilde, OpprettetAv = FunnSeed.Av,
        };
        f.Koblinger = s.Koblinger.Select((k, i) => new FunnKoblingEntitet
        {
            Id = Guid.NewGuid(), FunnId = f.Id, Rolle = k.Rolle, Sortering = i, Etikett = k.Etikett, Merknad = k.Merknad,
            RettskildeEli = k.Eli, Eid = k.Eid, EksternLenke = k.Lenke,
            VirksomhetId = k.Organisasjonsnummer is not null || k.VirksomhetNavn is not null ? Guid.NewGuid() : null,
        }).ToList();
        f.Vurderingspunkter = s.Vurderingspunkter.Select((p, i) => new FunnVurderingspunktEntitet
        {
            Id = Guid.NewGuid(), FunnId = f.Id, Type = p.Type, Sortering = i, Overskrift = p.Overskrift, Tekst = p.Tekst, Sikkerhet = p.Sikkerhet,
        }).ToList();
        return f;
    }

    [Fact]
    public void Nettsiden_publiserer_bare_godkjente_funn_med_den_godkjente_teksten()
    {
        var fil = JsonNode.Parse(File.ReadAllText(Path.Combine(FinnRepoRot(), "nettside", "data", "funn", "register.json")))!.AsObject();
        var publisert = fil["funn"]!.AsArray();

        var godkjente = FunnSeed.LesFil().Funn.Where(f => f.GodkjentForPublisering is not null).ToList();
        Assert.Equal(["429-funn-1"], publisert.Select(f => f!["id"]!.GetValue<string>()));
        Assert.Equal(godkjente.Select(g => g.Nokkel), publisert.Select(f => f!["id"]!.GetValue<string>()));
        Assert.Equal(publisert.Count, fil["antall"]!.GetValue<int>());

        foreach (var g in godkjente)
        {
            var forventet = JsonNode.Parse(FunnTjeneste.Serialiser(FunnTjeneste.ByggOffentligInnhold(
                IMinnet(g), DateOnly.FromDateTime(g.GodkjentForPublisering!.Tidspunkt.UtcDateTime))));
            var faktisk = publisert.Single(f => f!["id"]!.GetValue<string>() == g.Nokkel);
            Assert.True(JsonNode.DeepEquals(forventet, faktisk),
                $"register.json avviker fra det godkjente innholdet for {g.Nokkel}. Forventet:\n{forventet!.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
        }
        // Ingen personnavn: verken den som godkjente eller den som juristvurderte.
        var tekst = fil.ToJsonString();
        Assert.DoesNotContain("Johann", tekst);
        Assert.DoesNotContain("jurist-utfordrer", tekst);
    }
}
