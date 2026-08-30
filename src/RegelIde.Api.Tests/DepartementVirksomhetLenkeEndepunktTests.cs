using System.Net.Http.Json;
using System.Text.Json;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Departement-virksomhet-lenke (2026-08-30) — verifiserer at
/// <see cref="RettskildeEntitet.AnsvarligDepartement"/> (Lovdatas eget "ministry"-metadatafelt, satt
/// ved import fra data/kilder/raw-lovdata) faktisk kobles til de riktige, nyseedede
/// <see cref="Virksomhet"/>-departement-radene (<see cref="DepartementSeed"/>), begge veier: fra
/// rettskilden (<c>GET /api/rettskilder/{id}</c>) og fra virksomheten
/// (<c>GET /api/virksomheter/{id}/rettskilder-ansvarlig-for</c>). Kjører mot samme fullt oppstartede/
/// seedede API som <see cref="VirksomhetEndepunktTests"/> — IKKE en isolert testdatabase.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class DepartementVirksomhetLenkeEndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web);

    private readonly EmbeddedPostgresApiFixture _fixture;

    public DepartementVirksomhetLenkeEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> HentVirksomhetIdAsync(string navn)
    {
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);
        return Assert.Single(virksomheter!, v => v.Navn == navn).Id;
    }

    [Fact]
    public async Task Rettskilde_med_kjent_departement_lenkes_til_ekte_virksomhet()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        var alkohollovenId = sammendrag!.Single(r => r.Eli == "https://lovdata.no/eli/lov/1989/06/02/27/nor").Id;

        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{alkohollovenId}", JsonInnstillinger);

        Assert.Equal("Helse- og omsorgsdepartementet", detalj!.AnsvarligDepartement);
        Assert.NotNull(detalj.AnsvarligDepartementVirksomhetId);

        var helseId = await HentVirksomhetIdAsync("Helse- og omsorgsdepartementet");
        Assert.Equal(helseId, detalj.AnsvarligDepartementVirksomhetId);
    }

    [Fact]
    public async Task Virksomhet_lister_alle_gjeldende_rettskilder_den_er_ansvarlig_for()
    {
        var helseId = await HentVirksomhetIdAsync("Helse- og omsorgsdepartementet");

        var ansvarligFor = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>(
            $"/api/virksomheter/{helseId}/rettskilder-ansvarlig-for", JsonInnstillinger);

        Assert.NotNull(ansvarligFor);
        // Alle tre kildefixturer fra Helse- og omsorgsdepartementet (data/kilder/raw-lovdata):
        // alkoholloven, alkoholforskriften, tannhelsetjenesteloven.
        Assert.Contains(ansvarligFor!, r => r.Eli == "https://lovdata.no/eli/lov/1989/06/02/27/nor");
        Assert.Contains(ansvarligFor!, r => r.Eli == "https://lovdata.no/eli/forskrift/2005/06/08/538/nor");
        Assert.Contains(ansvarligFor!, r => r.Kortnavn == "Tannhelsetjenesteloven" || r.Tittel.Contains("tannhelsetjeneste"));
    }

    [Fact]
    public async Task Klima_og_miljodepartementet_er_ansvarlig_for_nøyaktig_motorferdselloven()
    {
        var klimaId = await HentVirksomhetIdAsync("Klima- og miljødepartementet");

        var ansvarligFor = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>(
            $"/api/virksomheter/{klimaId}/rettskilder-ansvarlig-for", JsonInnstillinger);

        var enkeltrad = Assert.Single(ansvarligFor!);
        Assert.Contains("motorferdsel", enkeltrad.Tittel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Virksomhet_uten_navnetreff_gir_tom_liste_ingen_gjettet_fallback()
    {
        // Bergen kommune er en ekte, seedet virksomhet, men matcher ingen rettskildes
        // AnsvarligDepartement — skal gi tom liste, ikke feil eller et gjettet treff.
        var bergenId = await HentVirksomhetIdAsync("Bergen kommune");

        var ansvarligFor = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>(
            $"/api/virksomheter/{bergenId}/rettskilder-ansvarlig-for", JsonInnstillinger);

        Assert.NotNull(ansvarligFor);
        Assert.Empty(ansvarligFor!);
    }

    [Fact]
    public async Task Ukjent_virksomhet_id_gir_tom_liste()
    {
        var ansvarligFor = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>(
            $"/api/virksomheter/{Guid.NewGuid()}/rettskilder-ansvarlig-for", JsonInnstillinger);

        Assert.NotNull(ansvarligFor);
        Assert.Empty(ansvarligFor!);
    }

    /// <summary>
    /// En 'erstattet' rad (§2.1-versjonering — samme AnsvarligDepartement som den nye, gjeldende
    /// raden) skal IKKE telles med — kun Entitetsstatus == "gjeldende" er i listen (oppgavekravet).
    /// Satt opp direkte mot databasen (ikke via en reell reimport) for å isolere nøyaktig dette ene
    /// filteret.
    /// </summary>
    [Fact]
    public async Task Erstattet_rad_med_samme_departement_telles_ikke_med()
    {
        var klimaId = await HentVirksomhetIdAsync("Klima- og miljødepartementet");

        await using (var db = _fixture.NyDbContext())
        {
            db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = Guid.NewGuid(),
                Doctype = "act",
                Kildetype = "Lov",
                Tittel = "Gammel versjon (erstattet) av en klimarelatert lov",
                AknXml = "<akomaNtoso/>",
                Status = "Gjeldende",
                Entitetsstatus = "erstattet",
                AnsvarligDepartement = "Klima- og miljødepartementet",
                OpprettetAv = "test",
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var ansvarligFor = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>(
            $"/api/virksomheter/{klimaId}/rettskilder-ansvarlig-for", JsonInnstillinger);

        // Fortsatt kun motorferdselloven (den ENE gjeldende raden) — den erstattede raden over er ikke med.
        var enkeltrad = Assert.Single(ansvarligFor!);
        Assert.Contains("motorferdsel", enkeltrad.Tittel, StringComparison.OrdinalIgnoreCase);
    }
}
