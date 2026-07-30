using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester: kjører hele API-et (inkl. migrasjon + førstegangs-seeding i Program.cs) mot
/// en ekte, embedded Postgres-instans og de ekte rettskilde-fixturene i data/kilder/raw-lovdata/.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class RettskilderEndepunktTests
{
    private readonly HttpClient _client;
    private const string AlkohollovenEli = "https://lovdata.no/eli/lov/1989/06/02/27/nor";

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly EmbeddedPostgresApiFixture _fixture;

    public RettskilderEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> HentAlkohollovenIdAsync()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        return sammendrag!.Single(r => r.Eli == AlkohollovenEli).Id;
    }

    [Fact]
    public async Task Liste_inneholder_alle_tre_kildedokumenter()
    {
        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);

        Assert.NotNull(sammendrag);
        // >= 3, ikke akkurat 3: denne testklassen deler databasen med ImportEndepunktTests
        // (samme Postgres-instans for hele assemblyen, se ApiTestCollection), som kan legge til
        // flere rettskilder (bl.a. en virksomhetseid kopi av forvaltningsloven).
        Assert.True(sammendrag!.Count >= 3);
        Assert.Contains(sammendrag, r => r.Eli == AlkohollovenEli);
        Assert.Contains(sammendrag, r => r.Eli == "https://lovdata.no/eli/forskrift/2005/06/08/538/nor");
        Assert.Contains(sammendrag, r => r.Eli == "https://lovdata.no/eli/lov/1967/02/10/nor");
    }

    [Fact]
    public async Task Henter_full_rettskilde_med_metadata_og_akn_xml()
    {
        var id = await HentAlkohollovenIdAsync();
        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);

        Assert.NotNull(detalj);
        Assert.Equal(AlkohollovenEli, detalj!.Eli);
        Assert.StartsWith("<akomaNtoso", detalj.AknXml);
    }

    [Fact]
    public async Task Ukjent_id_gir_404()
    {
        var svar = await _client.GetAsync($"/api/rettskilder/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Henter_nodetre_og_finner_paragraf_1_1()
    {
        var id = await HentAlkohollovenIdAsync();
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{id}/noder", JsonInnstillinger);

        Assert.NotNull(noder);
        Assert.Contains(noder!, n => n.Eid == $"{AlkohollovenEli}/§1-1");
    }

    [Fact]
    public async Task Nodetre_eksponerer_opphevet_flagg_og_dato_for_1_12()
    {
        var id = await HentAlkohollovenIdAsync();
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{id}/noder", JsonInnstillinger);

        var opphevetParagraf = noder!.Single(n => n.Eid == $"{AlkohollovenEli}/§1-12");
        Assert.True(opphevetParagraf.Opphevet);
        Assert.Equal(new DateOnly(2005, 7, 1), opphevetParagraf.OpphevetDato);

        var vanligParagraf = noder!.Single(n => n.Eid == $"{AlkohollovenEli}/§1-1");
        Assert.False(vanligParagraf.Opphevet);
    }

    [Fact]
    public async Task Henter_enkeltnode_ved_eId_med_skraastreker_og_skjema()
    {
        var id = await HentAlkohollovenIdAsync();
        var eid = $"{AlkohollovenEli}/§1-1/ledd-1";
        var node = await _client.GetFromJsonAsync<RettskildeNodeDto>(
            $"/api/rettskilder/{id}/noder/oppslag?eid={Uri.EscapeDataString(eid)}", JsonInnstillinger);

        Assert.NotNull(node);
        Assert.Equal(eid, node!.Eid);
        Assert.StartsWith("Reguleringen av innførsel", node.Tekst);
    }

    [Fact]
    public async Task Ukjent_eId_gir_404_selv_om_rettskilden_finnes()
    {
        var id = await HentAlkohollovenIdAsync();
        var svar = await _client.GetAsync($"/api/rettskilder/{id}/noder/oppslag?eid=finnes-ikke");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Henter_kryssreferanser_inkludert_intern_referanse_1_3_til_1_5()
    {
        var id = await HentAlkohollovenIdAsync();
        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{id}/noder", JsonInnstillinger);
        var fraNodeId = noder!.Single(n => n.Eid == $"{AlkohollovenEli}/§1-3/ledd-1").Id;

        var referanser = await _client.GetFromJsonAsync<List<RettskildeReferanseDto>>($"/api/rettskilder/{id}/referanser", JsonInnstillinger);

        Assert.NotNull(referanser);
        Assert.Contains(referanser!, r => r.FraNodeId == fraNodeId && r.TilEid == $"{AlkohollovenEli}/§1-5");
    }

    // ---------- Åpne data: statusfilter + valgfri virksomhet-parameter (2026-07-24) ----------

    [Fact]
    public async Task Utkast_rettskilde_er_skjult_fra_listen_og_gir_404_ved_direkte_oppslag()
    {
        Guid utkastId;
        await using (var db = _fixture.NyDbContext())
        {
            utkastId = Guid.NewGuid();
            db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = utkastId,
                Doctype = "internal",
                Kildetype = "Virksomhetsdokument",
                Tittel = "Ikke ferdig verifisert kilde",
                Status = "Utkast",
                AknXml = "<akomaNtoso/>",
                OpprettetAv = "test",
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var sammendrag = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        Assert.DoesNotContain(sammendrag!, r => r.Id == utkastId);

        var svar = await _client.GetAsync($"/api/rettskilder/{utkastId}");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task VirksomhetId_parameter_snevrer_inn_til_kun_den_virksomhetens_egne_kilder()
    {
        Guid virksomhetId, egenRettskildeId;
        await using (var db = _fixture.NyDbContext())
        {
            virksomhetId = Guid.NewGuid();
            db.Virksomheter.Add(new Virksomhet { Id = virksomhetId, Navn = "Vennesla kommune" });
            egenRettskildeId = Guid.NewGuid();
            db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = egenRettskildeId,
                VirksomhetId = virksomhetId,
                Doctype = "act",
                Kildetype = "Forskrift",
                Tittel = "Lokal forskrift om skjenketider, Vennesla kommune",
                Status = "Gjeldende",
                AknXml = "<akomaNtoso/>",
                OpprettetAv = "test",
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Uten parameter: ser alt (delte kilder + virksomhetens egen) -- åpne data, ikke en tilgangssperre.
        var alt = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        Assert.Contains(alt!, r => r.Id == egenRettskildeId);
        Assert.Contains(alt!, r => r.Eli == AlkohollovenEli);

        // Med ?virksomhetId=...: kun DENNE virksomhetens egne kilder, ikke de delte/nasjonale.
        var kunEgne = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>(
            $"/api/rettskilder?virksomhetId={virksomhetId}", JsonInnstillinger);
        Assert.Single(kunEgne!);
        Assert.Equal(egenRettskildeId, kunEgne!.Single().Id);
    }

    // ---------- Metadata-oppdatering (2026-07-29, AK-3.3.6 importbekreftelse) ----------

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    [Fact]
    public async Task Oppdater_metadata_uten_bruker_id_header_gir_400()
    {
        var id = await HentAlkohollovenIdAsync();
        var svar = await _client.PatchAsJsonAsync($"/api/rettskilder/{id}/metadata", new OppdaterRettskildeMetadataRequest("Nytt", "Ny utgiver"));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Oppdater_metadata_lagrer_kortnavn_og_utgiver()
    {
        var id = await HentAlkohollovenIdAsync();
        var bruker = await HentTestbrukerAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/rettskilder/{id}/metadata")
        {
            Content = JsonContent.Create(new OppdaterRettskildeMetadataRequest("Alkoholloven (kortnavn testet)", "Lovdata (redigert)")),
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString() } },
        };
        var svar = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var oppdatert = await svar.Content.ReadFromJsonAsync<RettskildeDetalj>(JsonInnstillinger);
        Assert.Equal("Alkoholloven (kortnavn testet)", oppdatert!.Kortnavn);
        Assert.Equal("Lovdata (redigert)", oppdatert.Utgiver);

        var hentetPaNytt = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{id}", JsonInnstillinger);
        Assert.Equal("Alkoholloven (kortnavn testet)", hentetPaNytt!.Kortnavn);
    }

    [Fact]
    public async Task Oppdater_metadata_pa_ukjent_rettskilde_gir_404()
    {
        var bruker = await HentTestbrukerAsync();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/rettskilder/{Guid.NewGuid()}/metadata")
        {
            Content = JsonContent.Create(new OppdaterRettskildeMetadataRequest("X", "Y")),
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, bruker.Id.ToString() } },
        };
        var svar = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Referert_av_tjenester_viser_alminnelig_skjenkebevilling()
    {
        // Byggesteg 4 (2026-07-30) — motsatt retning av tjenestens regelverksreferanser. Byggesteg 2-
        // seedingen kobler "Alminnelig skjenkebevilling" til alkoholloven kap. 4 (§§ 4-1 til 4-7).
        var id = await HentAlkohollovenIdAsync();

        var referanser = await _client.GetFromJsonAsync<List<TjenesteReferanseDto>>(
            $"/api/rettskilder/{id}/referert-av-tjenester", JsonInnstillinger);

        Assert.NotNull(referanser);
        Assert.Contains(referanser!, r => r.TjenesteTittel == "Alminnelig skjenkebevilling");
        Assert.True(referanser!.Count(r => r.TjenesteTittel == "Alminnelig skjenkebevilling") >= 7);
    }

    [Fact]
    public async Task Referert_av_tjenester_for_ukjent_rettskilde_gir_tom_liste()
    {
        var referanser = await _client.GetFromJsonAsync<List<TjenesteReferanseDto>>(
            $"/api/rettskilder/{Guid.NewGuid()}/referert-av-tjenester", JsonInnstillinger);

        Assert.NotNull(referanser);
        Assert.Empty(referanser!);
    }

    [Fact]
    public async Task Import_referanse_har_opprinnelse_import_og_kan_ikke_fjernes()
    {
        // 2026-07-30: kryssreferansen §1-3 → §1-5 fanges opp automatisk under import (§1-3 har en reell
        // inline Lovdata-lenke) — bekrefter at den er skrivebeskyttet i UI-flyten.
        var alkoholovenId = await HentAlkohollovenIdAsync();
        var referanser = await _client.GetFromJsonAsync<List<RettskildeReferanseDto>>(
            $"/api/rettskilder/{alkoholovenId}/referanser", JsonInnstillinger);
        var importReferanse = referanser!.First(r => r.Opprinnelse == "import");

        var fjernSvar = await _client.DeleteAsync($"/api/rettskilder/{alkoholovenId}/referanser/{importReferanse.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, fjernSvar.StatusCode);
    }

    [Fact]
    public async Task Manuell_referanse_kan_opprettes_pa_en_node_og_fjernes_igjen()
    {
        var alkoholovenId = await HentAlkohollovenIdAsync();
        var paragrafer = (await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{alkoholovenId}/noder", JsonInnstillinger))!
            .Where(n => n.NodeType == "paragraf").ToList();
        var fra = paragrafer[0];
        var til = paragrafer[1];

        var svar = await _client.PostAsJsonAsync(
            $"/api/rettskilder/{alkoholovenId}/noder/{fra.Id}/referanser",
            new KobleLovreferanseRequest(alkoholovenId, til.Eid), JsonInnstillinger);
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var referanse = await svar.Content.ReadFromJsonAsync<RettskildeReferanseDto>(JsonInnstillinger);
        Assert.Equal("manuell", referanse!.Opprinnelse);

        var fjernSvar = await _client.DeleteAsync($"/api/rettskilder/{alkoholovenId}/referanser/{referanse.Id}");
        Assert.Equal(HttpStatusCode.NoContent, fjernSvar.StatusCode);
    }
}
