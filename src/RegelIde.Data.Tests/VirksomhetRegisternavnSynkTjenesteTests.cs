using System.Text.Json;
using RegelIde.Data;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, registernavn-runden, 2026-09-08] Dekker de to delene av
/// <see cref="VirksomhetRegisternavnSynkTjeneste"/> som er ren logikk uten database eller nett:
/// avbildningen fra SSRs språkmerkede skrivemåter til navneformgrunn, og integriteten i
/// <see cref="VirksomhetNavneformOverstyringer"/>.
///
/// <para>
/// Testdataene under er EKTE svar fra <c>ws.geonorge.no/stedsnavn/v1/sted</c>, hentet live 2026-09-08
/// — ikke oppdiktede eksempler. Det er poenget: regelen skal holde mot registerets faktiske
/// verdikombinasjoner (både «vedtatt» og «godkjent og prioritert», både «hovednavn» og «undernavn»,
/// og fire ulike språk), ikke mot en forenklet modell av dem.
/// </para>
/// </summary>
public class VirksomhetRegisternavnSynkTjenesteTests
{
    private static string Grunn(string skrivemate, string sprak, string status, string navnestatus = "hovednavn")
    {
        var harGjeldende = false;
        return VirksomhetRegisternavnSynkTjeneste.GrunnFor(
            new SsrSkrivemate(skrivemate, sprak, status, navnestatus), ref harGjeldende);
    }

    [Theory]
    // Kåfjord (knr 5540) — tre likestilte offisielle navn, alle «vedtatt».
    [InlineData("Kåfjord kommune", "Norsk", "vedtatt", "gjeldende")]
    [InlineData("Gáivuona suohkan", "Nordsamisk", "vedtatt", "parallellnavn")]
    [InlineData("Kaivuonon komuuni", "Kvensk", "vedtatt", "parallellnavn")]
    // Oslo (knr 0301) — samisk finnes også på sørsamisk/lulesamisk.
    [InlineData("Osloven tjïelte", "Sørsamisk", "vedtatt", "parallellnavn")]
    [InlineData("Oslo suohkan", "Lulesamisk", "vedtatt", "parallellnavn")]
    // Lavangen (knr 5518) — norsk hovedform med den ANDRE gyldige statusverdien.
    [InlineData("Lavangen kommune", "Norsk", "godkjent og prioritert", "gjeldende")]
    public void Grunnen_leses_fra_SSRs_egne_felt(string skrivemate, string sprak, string status, string forventet)
    {
        Assert.Equal(forventet, Grunn(skrivemate, sprak, status));
    }

    /// <summary>
    /// Et ANNET språk er ALDRI 'utgatt' eller 'feilskriving', uansett status. «Gáivuona suohkan» er et
    /// likestilt offisielt navn etter stedsnavnloven, ikke en avløst eller feilskrevet variant — og
    /// forskjellen er synlig for saksbehandleren, siden 'utgatt' gir oransje varselfarge og
    /// 'feilskriving' rød (docs/09 §15).
    /// </summary>
    [Theory]
    [InlineData("vedtatt")]
    [InlineData("godkjent")]
    [InlineData("foreslått")]
    public void Annet_sprak_blir_alltid_parallellnavn(string status)
    {
        Assert.Equal("parallellnavn", Grunn("Romssa fylka", "Nordsamisk", status));
    }

    /// <summary>
    /// De norske formene som IKKE er den offisielle hovedformen blir 'kortform' — både SSRs egne
    /// <c>undernavn</c> («Bergen» ved siden av «Bergen kommune») og et hovednavn som bare er
    /// <c>godkjent</c> uten å være prioritert («Oslo» ved siden av «Oslo kommune»). Begge ER
    /// kontekstavhengige kortformer, som er nøyaktig hva 'kortform' betyr i vokabularet — og de er
    /// dessuten termene forskriftstekster faktisk bruker («Lavangen», «Karasjok»).
    /// </summary>
    [Theory]
    [InlineData("Bergen", "godkjent og prioritert", "undernavn")]
    [InlineData("Oslo", "godkjent", "hovednavn")]
    [InlineData("Lavangen", "godkjent", "hovednavn")]
    public void Norske_biformer_blir_kortform(string skrivemate, string status, string navnestatus)
    {
        Assert.Equal("kortform", Grunn(skrivemate, "Norsk", status, navnestatus));
    }

    /// <summary>
    /// KUN ÉN 'gjeldende' selv om flere norske former oppfyller kriteriet — det er én gjeldende form
    /// per virksomhet, og <see cref="VirksomhetVisningsnavnTjeneste"/> plukker nettopp den. Uten denne
    /// avgrensningen ville en kommune med to «vedtatt»-norske former fått to konkurrerende
    /// visningsnavn, og visningen ville hoppet mellom dem.
    /// </summary>
    [Fact]
    public void Bare_den_forste_norske_hovedformen_blir_gjeldende()
    {
        var resultat = VirksomhetRegisternavnSynkTjeneste.FraSsr(
        [
            new("Lavangen kommune", "Norsk", "godkjent og prioritert", "hovednavn"),
            new("Lavangen", "Norsk", "godkjent og prioritert", "hovednavn"),
            new("Loabága suohkan", "Nordsamisk", "vedtatt", "hovednavn"),
        ]);

        Assert.Equal("gjeldende", resultat.Single(r => r.Term == "Lavangen kommune").Grunn);
        Assert.Equal("kortform", resultat.Single(r => r.Term == "Lavangen").Grunn);
        Assert.Equal("parallellnavn", resultat.Single(r => r.Term == "Loabága suohkan").Grunn);
        Assert.Single(resultat, r => r.Grunn == "gjeldende");
    }

    /// <summary>
    /// Hele Kåfjord-tilfellet fra ende til ende — raden som utløste denne runden. Beviser at de tre
    /// leddene Brreg limte sammen uten skilletegn («GAIVUONA SUOHKAN KÅFJORD KOMMUNE KAIVUONON
    /// KOMUUNI») kommer ut som tre separate, korrekt skrevne navneformer, MED de diakritiske tegnene
    /// registeret mangler.
    /// </summary>
    [Fact]
    public void Kafjord_gir_tre_navneformer_med_diakritiske_tegn()
    {
        var resultat = VirksomhetRegisternavnSynkTjeneste.FraSsr(
        [
            new("Gáivuona suohkan", "Nordsamisk", "vedtatt", "hovednavn"),
            new("Kåfjord kommune", "Norsk", "vedtatt", "hovednavn"),
            new("Kaivuonon komuuni", "Kvensk", "vedtatt", "hovednavn"),
        ]);

        Assert.Equal(3, resultat.Count);
        Assert.Equal("Kåfjord kommune", resultat.Single(r => r.Grunn == "gjeldende").Term);
        Assert.Equal(
            new[] { "Gáivuona suohkan", "Kaivuonon komuuni" },
            resultat.Where(r => r.Grunn == "parallellnavn").Select(r => r.Term).OrderBy(t => t).ToArray());

        // «á» er beviset på at navnet er HENTET og ikke regnet ut: Brreg-strengen har ingen
        // diakritiske tegn i det hele tatt, så ingen omskriving av den kunne produsert dette.
        Assert.Contains("á", resultat.Single(r => r.Term.StartsWith("Gáivuona", StringComparison.Ordinal)).Term);
    }

    // ---- Overstyringstabellen ----

    /// <summary>
    /// Hver overstyring har NØYAKTIG én 'gjeldende'-navneform.
    /// <see cref="VirksomhetVisningsnavnTjeneste"/> plukker den ene, så to ville gjort visningen
    /// avhengig av sorteringsrekkefølge, og null ville betydd at raden faller tilbake på Brregs
    /// VERSAL-form — altså at overstyringen ikke virket i det hele tatt.
    /// </summary>
    [Fact]
    public void Hver_overstyring_har_noyaktig_en_gjeldende_navneform()
    {
        foreach (var (orgnr, navneformer) in VirksomhetNavneformOverstyringer.PerOrganisasjonsnummer)
        {
            Assert.Single(navneformer, n => n.Grunn == "gjeldende");
            Assert.All(navneformer, n => Assert.False(string.IsNullOrWhiteSpace(n.Term)));
            // Termene innenfor én virksomhet må være unike — SorgForNavneformAsync ville ellers
            // opprettet den første og stille hoppet over den andre.
            Assert.Equal(
                navneformer.Select(n => n.Term).Distinct(StringComparer.Ordinal).Count(),
                navneformer.Count);
            Assert.Matches("^[0-9]{9}$", orgnr);
        }
    }

    /// <summary>
    /// Alle grunnene i tabellen står i det lukkede vokabularet. Uten denne testen ville en skrivefeil
    /// («paralellnavn») først blitt oppdaget ved at CHECK-constrainten
    /// <c>ck_begreper_navneformgrunn</c> velter under bakgrunnssynken — altså i en logg ingen leser,
    /// lenge etter oppstart.
    /// </summary>
    [Fact]
    public void Alle_grunner_i_tabellen_er_i_det_lukkede_vokabularet()
    {
        var ugyldige = VirksomhetNavneformOverstyringer.PerOrganisasjonsnummer
            .SelectMany(p => p.Value.Select(n => new { p.Key, n.Term, n.Grunn }))
            .Where(x => !VirksomhetsbegrepTjeneste.ErGyldigNavneformgrunn(x.Grunn))
            .ToList();

        Assert.Empty(ugyldige);
    }

    /// <summary>
    /// Tabellen dekker nøyaktig de radene som faktisk finnes i kildefilen, og de fire slettede er
    /// FJERNET derfra. Beviset på at de to listene og kildedata ikke har drevet fra hverandre — en
    /// overstyring for et organisasjonsnummer som ikke finnes ville aldri blitt brukt, og en slettet
    /// rad som fortsatt sto i fila ville blitt seedet inn igjen ved neste oppstart.
    /// </summary>
    [Fact]
    public void Tabellen_og_slettelisten_stemmer_med_kildefilen()
    {
        var filsti = Path.Combine(AppContext.BaseDirectory, "Seed", "organisasjoner-norge.json");
        Assert.True(File.Exists(filsti), $"Fant ikke kildefilen: {filsti}");

        using var dok = JsonDocument.Parse(File.ReadAllText(filsti));
        var orgnrIFila = dok.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("organisasjonsnummer").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        // Hver overstyring peker på en rad som finnes.
        var manglende = VirksomhetNavneformOverstyringer.PerOrganisasjonsnummer.Keys
            .Where(nr => !orgnrIFila.Contains(nr)).ToList();
        Assert.Empty(manglende);

        // Ingen av de slettede står igjen i fila.
        var fortsattIFila = VirksomhetNavneformOverstyringer.SlettedeOrganisasjonsnumre
            .Where(orgnrIFila.Contains).ToList();
        Assert.Empty(fortsattIFila);

        // Og de to listene overlapper ikke: en slettet rad skal ikke ha en navneform-overstyring.
        Assert.DoesNotContain(
            VirksomhetNavneformOverstyringer.SlettedeOrganisasjonsnumre,
            VirksomhetNavneformOverstyringer.PerOrganisasjonsnummer.ContainsKey);
    }

    /// <summary>
    /// «Nærings- og fiskeridepartementet» MÅ stå eksakt slik i tabellen: koblingen fra rettskilde til
    /// ansvarlig departement går via et navnematch mot Lovdatas "ministry"-felt, som skriver nøyaktig
    /// denne formen (se <c>DepartementSeed</c>). Var det den ENESTE mekanismen som holdt koblingen,
    /// ville en skrivefeil her brutt den stille. Oppslaget er case-insensitivt, så testen slår ned på
    /// noe som faktisk betyr noe: bokstavene og bindestreken, ikke kasus.
    /// </summary>
    [Fact]
    public void Naerings_og_fiskeridepartementet_beholder_Lovdatas_egen_skrivemate()
    {
        var navneformer = VirksomhetNavneformOverstyringer.PerOrganisasjonsnummer["912660680"];
        var gjeldende = navneformer.Single(n => n.Grunn == "gjeldende");

        Assert.Equal("Nærings- og fiskeridepartementet", gjeldende.Term);
    }

    /// <summary>
    /// Johanns eksplisitt bestilte kortformer og utgåtte navn (2026-09-08) er faktisk i tabellen —
    /// ellers ville de stille falt ut ved en senere redigering.
    /// </summary>
    [Theory]
    [InlineData("889640782", "Nav", "kortform")]
    [InlineData("970205039", "NVE", "kortform")]
    [InlineData("971040238", "Kartverket", "kortform")]
    [InlineData("974446871", "Nkom", "kortform")]
    [InlineData("974760673", "Brønnøysundregistrene", "kortform")]
    [InlineData("974760983", "DSB", "kortform")]
    [InlineData("983609155", "Enova", "kortform")]
    [InlineData("985198292", "Avinor", "kortform")]
    [InlineData("985847215", "Gjenopptakelseskommisjonen", "kortform")]
    // Helfo: «Helseøkonomiforvaltningen» er utleggingen av forkortelsen (SNLs «også kjent som»), IKKE
    // et utgått navn — Brregs egne historiskeNavn er de to under. Verifisert live 2026-09-08.
    [InlineData("986965610", "Helseøkonomiforvaltningen", "parallellnavn")]
    [InlineData("986965610", "Nasjonal oppgjørsenhet", "utgatt")]
    [InlineData("986965610", "Nav Helsetjenesteforvaltning", "utgatt")]
    public void Bestilte_tilleggsnavneformer_er_med(string orgnr, string term, string grunn)
    {
        var navneformer = VirksomhetNavneformOverstyringer.PerOrganisasjonsnummer[orgnr];
        var funnet = Assert.Single(navneformer, n => n.Term == term);
        Assert.Equal(grunn, funnet.Grunn);
    }

    /// <summary>
    /// Parentes-forkortelsene Brreg limer på navnet skal være SKILT UT som egne kortformer, ikke stå
    /// inne i visningsnavnet (Johanns instruks «skill ut»). En visningsetikett som «Norges vassdrags-
    /// og energidirektorat (NVE)» i hver eier-kolonne er nettopp den støyen utskillingen fjerner.
    /// </summary>
    [Theory]
    [InlineData("970205039")]
    [InlineData("974760983")]
    [InlineData("985165262")]
    public void Gjeldende_navneform_inneholder_ingen_parentesforkortelse(string orgnr)
    {
        var gjeldende = VirksomhetNavneformOverstyringer.PerOrganisasjonsnummer[orgnr]
            .Single(n => n.Grunn == "gjeldende");

        Assert.DoesNotContain("(", gjeldende.Term);
    }
}
