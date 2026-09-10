namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// [Ny, fastsatt-av-runden, 2026-09-10, issue #215] <see cref="FastsattAvTolker"/> — organet som
/// fastsatte en forskrift.
///
/// <para>
/// Alle strengene under er EKTE hjemmelslinjer fra korpuset (målt 2026-09-10 over 500 forskrifter),
/// ikke oppdiktede varianter. Det er poenget med testene: tolkeren skal treffe de formene Lovdata
/// faktisk bruker, og la resten stå i fred.
/// </para>
/// </summary>
public class FastsattAvTolkerTests
{
    [Fact]
    public void Leser_organet_og_stopper_for_hjemmelshalvdelen()
    {
        // NTNUs ph.d.-forskrift, den saken tok utgangspunkt i.
        var linje = "Hjemmel: Fastsatt av styret ved Norges teknisk-naturvitenskapelige universitet "
                    + "(NTNU) 3. februar 2026 med hjemmel i lov 8. mars 2024 nr. 9 om universiteter "
                    + "og høyskoler (universitets- og høyskoleloven) § 13-1 fjerde ledd.";

        var fastsatt = FastsattAvTolker.Tolk(linje);

        Assert.NotNull(fastsatt);
        // «styret ved» BEVARES i teksten — det er organet innad, og en opplysning kilden gir.
        Assert.Equal("styret ved Norges teknisk-naturvitenskapelige universitet (NTNU)", fastsatt.Tekst);
        // …men oppslaget skal gå på institusjonen, som er den som finnes i katalogen.
        Assert.Equal("Norges teknisk-naturvitenskapelige universitet (NTNU)", fastsatt.Organnavn);
        Assert.False(fastsatt.ErKongenIStatsrad);
    }

    [Theory]
    // Tre ekte former for hvor frasen slutter: dato, «med hjemmel», «i medhold av».
    [InlineData("Hjemmel: Fastsatt av Mattilsynet 12. mars 2019 med hjemmel i lov …", "Mattilsynet")]
    [InlineData("Hjemmel: Fastsatt av Samferdselsdepartementet med hjemmel i vegtrafikkloven § 4", "Samferdselsdepartementet")]
    [InlineData("Hjemmel: Fastsatt av Sjøfartsdirektoratet i medhold av skipssikkerhetsloven § 6", "Sjøfartsdirektoratet")]
    [InlineData("Hjemmel: Fastsatt av Statens jernbanetilsyn 1. januar 2017, jf. jernbaneloven", "Statens jernbanetilsyn")]
    public void Stopper_ved_hver_av_de_malte_terminatorene(string linje, string forventet)
    {
        var fastsatt = FastsattAvTolker.Tolk(linje);

        Assert.Equal(forventet, fastsatt!.Tekst);
        Assert.Equal(forventet, fastsatt.Organnavn);
    }

    [Fact]
    public void Kgl_res_er_en_annen_preposisjon_og_far_ikke_et_organnavn()
    {
        // 125 av 500 forskrifter har denne formen — «Fastsatt VED kgl.res», ikke «av». Kongen i
        // statsråd er et GRUPPEBEGREP, ikke en virksomhet, så det finnes ikke noe navn å slå opp mot
        // katalogen. Flagget bæres videre slik at en senere runde kan koble dem til gruppebegrepet.
        var fastsatt = FastsattAvTolker.Tolk(
            "Hjemmel: Fastsatt ved kgl.res. 7. desember 2012 med hjemmel i lov 26. mars 1999 nr. 15 …");

        Assert.NotNull(fastsatt);
        Assert.True(fastsatt.ErKongenIStatsrad);
        Assert.Null(fastsatt.Organnavn);
        Assert.Equal("kgl.res.", fastsatt.Tekst);
    }

    [Theory]
    // Alle fire formene er ekte. De to utskrevne ble funnet ved å lete etter rader der frasen FANTES
    // men ikke ble tolket — 3 av 120 stikkprøvde forskrifter. Alle fire er Kongen i statsråd.
    [InlineData("Hjemmel: Fastsatt ved Kronprinsreg.res. 4. mai 2001 …")]
    [InlineData("Fastsatt ved Kronprinsregentens res. av 9. november 1956, med endring ved kgl. res. av 7. februar 1975.")]
    [InlineData("Hjemmel: Fastsatt ved Regjeringens res. 5. november 1999 med hjemmel i lov av 13. juni 1997 nr. 42 …")]
    [InlineData("Hjemmel: Fastsatt ved kgl. res. 12. februar 2010 med hjemmel i lov 17. juni 2005 nr. 62 …")]
    public void Alle_resolusjonsformene_behandles_som_kongen_i_statsrad(string linje)
    {
        var fastsatt = FastsattAvTolker.Tolk(linje);

        Assert.True(fastsatt!.ErKongenIStatsrad);
        Assert.Null(fastsatt.Organnavn);
    }

    [Fact]
    public void Styret_for_er_samme_form_som_styret_ved()
    {
        // Begge preposisjonene forekommer i korpuset («styret ved Høgskolen i Østfold», «styret for
        // Norges Informasjonsteknologiske Høgskole»).
        var fastsatt = FastsattAvTolker.Tolk(
            "Hjemmel: Fastsatt av styret for Norges Informasjonsteknologiske Høgskole 5. juni 2015 …");

        Assert.Equal("styret for Norges Informasjonsteknologiske Høgskole", fastsatt!.Tekst);
        Assert.Equal("Norges Informasjonsteknologiske Høgskole", fastsatt.Organnavn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // Ekte linje uten fastsettelsesfrase — 33 av 500 forskrifter ser slik ut.
    [InlineData("Kunngjøring fra Finans- og tolldepartementet av stortingsvedtak 17. juni 1993.")]
    public void Linje_uten_frase_gir_null_ikke_en_gjettet_fastsetter(string? linje)
    {
        Assert.Null(FastsattAvTolker.Tolk(linje));
    }

    [Fact]
    public void Drar_ikke_hjemmelshalvdelen_inn_i_organnavnet()
    {
        // Vakten mot den feilen som ville vært lett å gjøre: uten terminator ville organnavnet blitt
        // «Mattilsynet 12. mars 2019 med hjemmel i lov 19. desember 2003 nr. 124 om matproduksjon …».
        var fastsatt = FastsattAvTolker.Tolk(
            "Hjemmel: Fastsatt av Mattilsynet 12. mars 2019 med hjemmel i lov 19. desember 2003 nr. 124 "
            + "om matproduksjon og mattrygghet mv. (matloven) § 9 første ledd, jf. delegeringsvedtak …");

        Assert.Equal("Mattilsynet", fastsatt!.Organnavn);
    }
}
