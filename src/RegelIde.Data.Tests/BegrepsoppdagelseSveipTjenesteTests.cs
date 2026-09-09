using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// Begrepsoppdagelse — M1 (eksplisitt definisjonsliste) og M11 (egen definisjonsparagraf), docs/24 §3.
/// To deler: rene enhetstester av selve mønstergjenkjenningen
/// (<see cref="BegrepsoppdagelseSveipTjeneste.FinnForekomster"/>, ingen DB — samme "internal static, rask
/// og presis" -mønster som <c>NavnekandidatOppdagelseTjenesteTests</c>) og integrasjonstester av selve
/// sveipet (<see cref="BegrepsoppdagelseSveipTjeneste.SveipAsync"/>) mot ekte embedded Postgres.
/// <para>
/// Del A sin M1-test bruker den EKSAKTE nodestrukturen/teksten fra FOR-2015-06-25-793
/// (pasientreiseforskriften) § 1, hentet direkte fra den kjørende dev-databasen 2026-09-02 (docs/24
/// §1.3 sin "nærmest perfekte M1-testcase") — konstruert som <see cref="NodeSnapshot"/>-rader her i
/// stedet for en full HTML-reimport, siden selve klassifiseringsfunksjonen er DB-uavhengig og dette gir
/// samme reelle valideringsverdi uten en embedded Postgres-avhengighet for denne delen. Del A sin
/// M11-test bruker tilsvarende den ekte teksten fra folketrygdloven §§ 1-8/1-9/1-10/13-3 (samme kilde,
/// samme dato).
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class BegrepsoppdagelseSveipTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BegrepsoppdagelseSveipTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------- Del A: ren mønstergjenkjenning, ingen DB ----------

    /// <summary>Ekte struktur/tekst fra FOR-2015-06-25-793 § 1 (pasientreiseforskriften), bekreftet mot
    /// den kjørende dev-databasen 2026-09-02 — paragraf "Definisjoner", ett ledd ("I forskriften her
    /// menes med"), fem punkt-barn av formen "term: forklaring".</summary>
    private static List<NodeSnapshot> PasientreiseforskriftenParagraf1()
    {
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        const string basis = "https://lovdata.no/eli/forskrift/2015/06/25/793/nor/§1";
        return
        [
            new NodeSnapshot(paragrafId, null, basis, "paragraf", "Definisjoner", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, $"{basis}/ledd-1", "ledd", null, "I forskriften her menes med", 2, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-1", "punkt", null,
                "reisestønad: stønad til dekning av nødvendige utgifter til reise", 3, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-2", "punkt", null,
                "bosted: pasientens folkeregistrerte adresse. Som bosted regnes også nødvendig midlertidig " +
                "oppholdssted på grunn av arbeid, studier, militærtjeneste og opphold i institusjon.", 4, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-3", "punkt", null,
                "bostedskommune: kommunen der pasienten har folkeregistrert adresse.", 5, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-4", "punkt", null,
                "bostedsregion: region som nevnt i spesialisthelsetjenesteloven § 5-1.", 6, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-5", "punkt", null,
                "nære pårørende: ektefelle, samboer, barn, barnebarn, foreldre, besteforeldre, svigerbarn, " +
                "svigerforeldre, søsken og personer som tilhører pasientens husstand.", 7, false),
        ];
    }

    [Fact]
    public void M1_pasientreiseforskriften_par1_gir_fem_forekomster_med_riktig_begrep_og_definisjon()
    {
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(PasientreiseforskriftenParagraf1());

        Assert.Equal(5, funn.Count);
        Assert.All(funn, f =>
        {
            Assert.Equal("M1", f.MonsterId);
            Assert.Equal("eksplisitt_liste", f.Kildetype);
            Assert.Equal("hoy", f.Konfidens);
            Assert.Equal("hele_dokumentet", f.Scope);
            Assert.Null(f.ScopeRefEid);
        });

        var reisestønad = funn.Single(f => f.Begrep == "reisestønad");
        Assert.Equal("reisestønad", reisestønad.BegrepOriginal);
        Assert.Equal("stønad til dekning av nødvendige utgifter til reise", reisestønad.Definisjon);
        Assert.Equal("https://lovdata.no/eli/forskrift/2015/06/25/793/nor/§1/ledd-1/punkt-1", reisestønad.NodeEid);

        var bostedsregion = funn.Single(f => f.Begrep == "bostedsregion");
        Assert.Equal("region som nevnt i spesialisthelsetjenesteloven § 5-1.", bostedsregion.Definisjon);

        // "nære pårørende" — begrepet selv inneholder mellomrom, første kolon skiller likevel korrekt.
        var nærePårørende = funn.Single(f => f.Begrep == "nære pårørende");
        Assert.StartsWith("ektefelle, samboer, barn", nærePårørende.Definisjon);
    }

    [Fact]
    public void M1_termens_tegnintervall_peker_eksakt_pa_selve_termen_ikke_kolon_eller_forklaring()
    {
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(PasientreiseforskriftenParagraf1());
        var reisestønad = funn.Single(f => f.Begrep == "reisestønad");
        const string punktTekst = "reisestønad: stønad til dekning av nødvendige utgifter til reise";

        Assert.Equal("reisestønad", punktTekst[reisestønad.StartOffset..reisestønad.EndOffset]);
    }

    [Fact]
    public void M1_trigges_ogsa_av_ledd_tekst_alene_uten_definisjon_i_paragrafoverskriften()
    {
        // Samme intro-frase ("... menes med"), men paragrafens EGEN overskrift sier ikke "definisjon" —
        // signal (a) og (b) i klassekommentaren er et OR, ikke et AND.
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§9", "paragraf", "Andre bestemmelser", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, "https://test/§9/ledd-1", "ledd", null, "I denne forskriften menes med", 2, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, "https://test/§9/ledd-1/punkt-1", "punkt", null, "x: y", 3, false),
        ];

        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder);

        Assert.Single(funn);
        Assert.Equal("M1", funn[0].MonsterId);
    }

    [Fact]
    public void M1_ignorerer_nostet_punkt_under_et_definisjonspunkt()
    {
        // docs/24 §1.3, "Mindre observasjon" — et listepunkt i en definisjonsliste kan selv inneholde en
        // nøstet liste (bekreftet i ekte data, alkoholforskriften § 6-2). Kun DIREKTE punkt-barn av selve
        // definisjons-leddet skal telle — et barnebarn av leddet (barn av et punkt) skal IKKE telle som
        // en egen forekomst.
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        var punktId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§1", "paragraf", "Definisjoner", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, "https://test/§1/ledd-1", "ledd", null, "I loven her menes med", 2, false),
            new NodeSnapshot(punktId, leddId, "https://test/§1/ledd-1/punkt-1", "punkt", null,
                "vurderingsmoment: et begrep med en nøstet liste av momenter", 3, false),
            new NodeSnapshot(Guid.NewGuid(), punktId, "https://test/§1/ledd-1/punkt-1/punkt-1", "punkt", null,
                "et moment som IKKE er en egen definisjon", 4, false),
        ];

        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder);

        Assert.Single(funn); // kun det ytterste punktet — det nøstede barnet telles ikke separat.
        Assert.Equal("vurderingsmoment", funn[0].Begrep);
    }

    /// <summary>Ekte tekst fra folketrygdloven §§ 1-8/1-9/1-10/13-3, bekreftet mot den kjørende
    /// dev-databasen 2026-09-02 — fire uavhengige, reelle M11-treff i samme lov.</summary>
    private static List<NodeSnapshot> FolketrygdlovenM11Paragrafer()
    {
        static (NodeSnapshot Paragraf, NodeSnapshot Ledd) Par(string eid, string overskrift, string leddTekst)
        {
            var paragrafId = Guid.NewGuid();
            var basis = $"https://lovdata.no/eli/lov/1997/02/28/19/nor/{eid}";
            return (
                new NodeSnapshot(paragrafId, null, basis, "paragraf", overskrift, null, 1, false),
                new NodeSnapshot(Guid.NewGuid(), paragrafId, $"{basis}/ledd-1", "ledd", null, leddTekst, 2, false));
        }

        var arbeidstaker = Par("§1-8", "Arbeidstaker",
            "Med arbeidstaker menes i denne loven enhver som arbeider i en annens tjeneste for lønn eller annen godtgjørelse.");
        var frilanser = Par("§1-9", "Frilanser",
            "Med frilanser menes i denne loven enhver som utfører arbeid eller oppdrag utenfor tjeneste for lønn " +
            "eller annen godtgjørelse, men uten å være selvstendig næringsdrivende, se § 1-10.");
        var selvstendig = Par("§1-10", "Selvstendig næringsdrivende",
            "Med selvstendig næringsdrivende menes i denne loven enhver som for egen regning og risiko driver en " +
            "vedvarende virksomhet som er egnet til å gi nettoinntekt.");
        var yrkesskade = Par("§13-3", "Yrkesskade",
            "Med yrkesskade menes en personskade, en sykdom eller et dødsfall som skyldes en arbeidsulykke som " +
            "skjer mens medlemmet er yrkesskadedekket, se §§ 13-6 til 13-13.");
        // Negativ kontroll: § 6-2 "Sykdom, skade eller lyte" — overskriften er IKKE selve definisjonen
        // ("Det er et vilkår ..." er en vilkårsbestemmelse, ikke "Med X menes ...").
        var sykdomSkadeLyte = Par("§6-2", "Sykdom, skade eller lyte",
            "Det er et vilkår for rett til stønad etter dette kapitlet at medlemmet etter hensiktsmessig " +
            "behandling fortsatt har varig sykdom, skade eller lyte.");

        return
        [
            arbeidstaker.Paragraf, arbeidstaker.Ledd,
            frilanser.Paragraf, frilanser.Ledd,
            selvstendig.Paragraf, selvstendig.Ledd,
            yrkesskade.Paragraf, yrkesskade.Ledd,
            sykdomSkadeLyte.Paragraf, sykdomSkadeLyte.Ledd,
        ];
    }

    [Fact]
    public void M11_folketrygdloven_gir_fire_reelle_treff_og_ingen_falskt_positiv_pa_ikke_definert_overskrift()
    {
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(FolketrygdlovenM11Paragrafer());

        Assert.Equal(4, funn.Count); // IKKE fem — "Sykdom, skade eller lyte" skal ikke gi et falskt treff.
        Assert.All(funn, f =>
        {
            Assert.Equal("M11", f.MonsterId);
            Assert.Equal("egen_paragraf", f.Kildetype);
            Assert.Equal("hoy", f.Konfidens);
            Assert.Equal("hele_dokumentet", f.Scope);
        });
        Assert.Contains(funn, f => f.Begrep == "arbeidstaker");
        Assert.Contains(funn, f => f.Begrep == "frilanser");
        Assert.Contains(funn, f => f.Begrep == "selvstendig næringsdrivende");
        Assert.Contains(funn, f => f.Begrep == "yrkesskade");
        Assert.DoesNotContain(funn, f => f.Begrep.Contains("sykdom") || f.Begrep.Contains("lyte"));

        var arbeidstaker = funn.Single(f => f.Begrep == "arbeidstaker");
        Assert.Equal(
            "Med arbeidstaker menes i denne loven enhver som arbeider i en annens tjeneste for lønn eller annen godtgjørelse.",
            arbeidstaker.Definisjon);
        Assert.Equal("arbeidstaker", arbeidstaker.Definisjon[arbeidstaker.StartOffset..arbeidstaker.EndOffset]);
    }

    [Fact]
    public void M11_krever_eksplisitt_menes_markor_ikke_bare_kort_overskrift()
    {
        // Presisjonsvernet klassekommentaren nevner: en kort paragraf-overskrift ALENE ("Formål",
        // "Grunnbeløpet") skal IKKE trigge M11 uten den eksplisitte "Med X menes/forstås/regnes"-markøren
        // — det er nettopp copula-varianten (M13), eksplisitt utenfor scope denne runden.
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§1-4", "paragraf", "Grunnbeløpet", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, "https://test/§1-4/ledd-1", "ledd", null,
                "Grunnbeløpet fastsettes av Kongen og reguleres årlig.", 2, false),
        ];

        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder);

        Assert.Empty(funn);
    }

    [Fact]
    public void Opphevet_paragraf_og_ledd_gir_ingen_forekomster()
    {
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§1", "paragraf", "Definisjoner", null, 1, true),
            new NodeSnapshot(leddId, paragrafId, "https://test/§1/ledd-1", "ledd", null, "I loven her menes med", 2, true),
            new NodeSnapshot(Guid.NewGuid(), leddId, "https://test/§1/ledd-1/punkt-1", "punkt", null, "x: y", 3, false),
        ];

        Assert.Empty(BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder));
    }

    // ---------- Del A2: [Ny, definisjonsmønster-runden, 2026-09-09] issue #168 — anførselstegn ----------

    /// <summary>Ekte tekst fra FOR-2022-04-07-636 § 3 (forskrift om livdyrsamarbeid for småfe), hentet
    /// live fra den kjørende dev-databasen 2026-09-09 — norsk lovgivningskonvensjon der den definerte
    /// termen introduseres i hermetegn. Dette er ÉN av 60 rettskilder der feilen i issue #168 er
    /// målt.</summary>
    private static List<NodeSnapshot> SmåfeforskriftenParagraf3()
    {
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        const string basis = "https://lovdata.no/eli/forskrift/2022/04/07/636/nor/§3";
        return
        [
            new NodeSnapshot(paragrafId, null, basis, "paragraf", "Definisjoner", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, $"{basis}/ledd-1", "ledd", null, "I denne forskriften menes med", 2, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-1", "punkt", null,
                "«Småfe»: sau og geit.", 3, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, $"{basis}/ledd-1/punkt-2", "punkt", null,
                "«Livdyrsamarbeid»: en avtale mellom driftsansvarlige fra inntil fire anlegg der småfe holdes, " +
                "som innebærer at småfe kan flyttes mellom anlegg eller holdes på fellesseter hvor småfe melkes.", 4, false),
        ];
    }

    [Fact]
    public void M1_omsluttende_anforselstegn_er_ikke_del_av_termen()
    {
        // Issue #168, målt live 2026-09-09: 508 av 6017 forekomster hadde «...» BAKT INN i termen, og
        // ingenting i godkjenningskjeden sanerer den — en godkjenning ville gitt registeret
        // Term = "«Småfe»" med synlige hermetegn.
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(SmåfeforskriftenParagraf3());

        Assert.Equal(2, funn.Count);
        var småfe = funn.Single(f => f.Begrep == "småfe");
        Assert.Equal("Småfe", småfe.BegrepOriginal); // IKKE "«Småfe»"
        Assert.Equal("sau og geit.", småfe.Definisjon);

        var livdyr = funn.Single(f => f.Begrep == "livdyrsamarbeid");
        Assert.Equal("Livdyrsamarbeid", livdyr.BegrepOriginal);
    }

    [Fact]
    public void M1_tegnintervallet_folger_den_rensede_termen_ikke_hermetegnene()
    {
        // Issue #168 punkt 2: strengen OG posisjonene må endres samtidig, ellers feiler revalideringen i
        // BegrepsforekomstTjeneste.GodkjennAsync (tekst[StartOffset..EndOffset] != BegrepOriginal).
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(SmåfeforskriftenParagraf3());
        var småfe = funn.Single(f => f.Begrep == "småfe");
        const string punktTekst = "«Småfe»: sau og geit.";

        Assert.Equal(1, småfe.StartOffset); // forbi det åpnende «
        Assert.Equal(småfe.BegrepOriginal, punktTekst[småfe.StartOffset..småfe.EndOffset]);
    }

    [Fact]
    public void M1_apostrofer_og_asymmetriske_hermetegn_star_urort()
    {
        // Alle tre tekstene er EKTE, målt i korpuset 2026-09-09. De to første er apostrofer INNE i termen
        // (FOR-2009-10-30-1321 § 1-5 og en EØS-forskrift) — enkelt anførselstegn er derfor bevisst
        // utenfor tegnsettet. Den tredje er asymmetrisk («...» + hale-parentes): hva termen "egentlig" er
        // kan bare gjettes, og det gjør vi ikke (CLAUDE.md §8).
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§1", "paragraf", "Definisjoner", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, "https://test/§1/ledd-1", "ledd", null, "I forskriften her menes med", 2, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, "https://test/§1/ledd-1/punkt-1", "punkt", null,
                "investigator's brochure: en samling kliniske og ikke-kliniske data", 3, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, "https://test/§1/ledd-1/punkt-2", "punkt", null,
                "EØS' harmoniseringsregelverk: regelverk som nevnt i vedlegg II", 4, false),
            new NodeSnapshot(Guid.NewGuid(), leddId, "https://test/§1/ledd-1/punkt-3", "punkt", null,
                "«god landbrukspraksis» (GAP): den anbefalte bruken av plantevernmidler", 5, false),
        ];

        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder);

        Assert.Equal(3, funn.Count);
        Assert.Contains(funn, f => f.BegrepOriginal == "investigator's brochure");
        Assert.Contains(funn, f => f.BegrepOriginal == "EØS' harmoniseringsregelverk");
        Assert.Contains(funn, f => f.BegrepOriginal == "«god landbrukspraksis» (GAP)");
    }

    [Theory]
    // Ingen hermetegn — uendret, og start-offset følger blanktegn-trimmingen slik det alltid har gjort.
    [InlineData("reisestønad", 0, "reisestønad")]
    [InlineData("  reisestønad  ", 2, "reisestønad")]
    // Ett omsluttende par, de tegnsettene som faktisk er målt i korpuset.
    [InlineData("«Småfe»", 1, "Småfe")]
    [InlineData("\"investigator\"", 1, "investigator")]
    // Blanktegn INNENFOR hermetegnene trimmes også — derfor er løkken i TermSpenn gjentakende.
    [InlineData(" « krav » ", 3, "krav")]
    // Asymmetrisk: begge ender må matche SAMME par, ellers står strengen urørt.
    [InlineData("«god landbrukspraksis» (GAP)", 0, "«god landbrukspraksis» (GAP)")]
    [InlineData("Skiller i klasse «B»", 0, "Skiller i klasse «B»")]
    // Apostrof er ikke et skilletegn — enkelt anførselstegn er bevisst utenfor tegnsettet.
    [InlineData("EØS' harmoniseringsregelverk", 0, "EØS' harmoniseringsregelverk")]
    [InlineData("'x'", 0, "'x'")]
    // Tomt innhold er et lovlig svar (lengde 0) — kallstedene hopper over, de finner ikke på en term.
    [InlineData("«»", 1, "")]
    [InlineData("   ", 3, "")]
    public void TermSpenn_fjerner_kun_matchende_omsluttende_par(string raa, int forventetStart, string forventetTerm)
    {
        var (start, lengde) = BegrepsoppdagelseSveipTjeneste.TermSpenn(raa);

        Assert.Equal(forventetStart, start);
        Assert.Equal(forventetTerm, raa.Substring(start, lengde));
    }

    [Fact]
    public void M11_hermetegn_i_overskrift_og_tekst_gir_treff_pa_den_rene_termen()
    {
        // Issue #168 sier M11 lagret en skitten term. Det stemmer IKKE — M11s BegrepOriginal kommer fra
        // regexens treffgruppe i leddteksten, ikke fra overskriften. Feilen var en FALSK NEGATIV: en
        // overskrift med hermetegn ga INGEN treff, fordi \bMed\s+«Arbeidstaker»\s+menes ikke matcher
        // "Med arbeidstaker menes". Nå treffer den, og hermetegnene står utenfor treffgruppen.
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        const string leddTekst = "Med «arbeidstaker» menes i denne loven enhver som arbeider i en annens tjeneste.";
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§1-8", "paragraf", "«Arbeidstaker»", null, 1, false),
            new NodeSnapshot(leddId, paragrafId, "https://test/§1-8/ledd-1", "ledd", null, leddTekst, 2, false),
        ];

        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder);

        var treff = Assert.Single(funn);
        Assert.Equal("M11", treff.MonsterId);
        Assert.Equal("hoy", treff.Konfidens);
        Assert.Equal("arbeidstaker", treff.BegrepOriginal); // uten hermetegn
        Assert.Equal("arbeidstaker", leddTekst[treff.StartOffset..treff.EndOffset]);
    }

    // ---------- Del A3: [Ny, definisjonsmønster-runden, 2026-09-09] issue #214 — verbklassen ----------

    /// <summary>
    /// Ekte struktur og tekst fra byggteknisk forskrift (FOR-2017-06-19-840,
    /// <c>eb6e5cef-e062-4f52-9370-9be9c03c6518</c>), hentet live via
    /// <c>GET /api/rettskilder/{id}/noder</c> 2026-09-09 — issue #214s fire ekte treff (§§ 5-2 til 5-5)
    /// OG issuets to ekte falske positiver fra SAMME forskrift (§§ 14-1 og 14-2), med de faktiske
    /// tekstene. Merk to detaljer som er nøyaktig som i kilden og som mønsteret må tåle:
    /// § 5-4s definisjon står i ANDRE ledd (ikke første), og §§ 5-3/5-5 har et hardt mellomrom
    /// (U+00A0) foran prosenttegnet.
    /// </summary>
    private static List<NodeSnapshot> ByggtekniskForskriftM11Verbparagrafer()
    {
        var noder = new List<NodeSnapshot>();

        void Paragraf(string nummer, string overskrift, params string[] leddTekster)
        {
            var paragrafId = Guid.NewGuid();
            var basis = $"https://lovdata.no/eli/forskrift/2017/06/19/840/nor/{nummer}";
            noder.Add(new NodeSnapshot(paragrafId, null, basis, "paragraf", overskrift, null, 1, false));
            for (var i = 0; i < leddTekster.Length; i++)
            {
                noder.Add(new NodeSnapshot(
                    Guid.NewGuid(), paragrafId, $"{basis}/ledd-{i + 1}", "ledd", null, leddTekster[i], i + 2, false));
            }
        }

        Paragraf("§5-2", "Bebygd areal (BYA)",
            "Bebygd areal beregnes etter Norsk Standard NS 3940:2012 Areal- og volumberegninger av bygninger, " +
            "men slik at parkeringsarealet inngår i beregningsgrunnlaget etter § 5-7. Bebygd areal på en tomt " +
            "skrives m2-BYA og angis i hele tall.");
        Paragraf("§5-3", "Prosent bebygd areal (%-BYA)",
            "Prosent bebygd areal angir forholdet mellom bebygd areal etter § 5-2 og tomtearealet. " +
            "Prosent bebygd areal skrives  %-BYA og angis i hele tall.");
        Paragraf("§5-4", "Bruksareal (BRA)",
            "(1) Bruksareal for bebyggelse på en tomt skrives m2-BRA og angis i hele tall.",
            "(2) Bruksareal beregnes etter Norsk Standard NS 3940:2012 Areal- og volumberegninger av bygninger, " +
            "men slik at parkeringsarealet inngår i beregningsgrunnlaget etter § 5-7. I tillegg gjelder følgende:");
        Paragraf("§5-5", "Prosent bruksareal (%-BRA)",
            "Prosent bruksareal angir forholdet mellom bruksareal etter § 5-4 og tomtearealet. " +
            "Prosent bruksareal skrives  %-BRA og angis i hele tall.");

        // Issue #214s to EKTE falske positiver — samme forskrift, samme verb, men overskriften er ikke
        // termen og subjektet står ikke umiddelbart foran markøren.
        Paragraf("§14-1", "Generelle krav",
            "(1) Bygninger skal prosjekteres og utføres slik at det tilrettelegges for forsvarlig energibruk.",
            "(2) Energikravene gjelder for bygningens oppvarmede bruksareal (BRA).",
            "(3) U-verdier skal beregnes som gjennomsnitt for de ulike bygningsdelene.",
            "(4) For bygning eller del av en bygning som skal holde lav innetemperatur, gjelder ikke " +
            "energikravene dersom energibehovet holdes på et forsvarlig nivå.",
            "(5) Dersom kravene i dette kapitlet ikke kan forenes med bevaring av kulturminner og " +
            "antikvariske verdier, gjelder kravene så langt de passer.");
        Paragraf("§14-2", "Krav til energieffektivitet",
            "(1) Det totale netto energibehovet for bygningen skal ikke overstige energirammene i tabellen " +
            "i bokstav a samtidig som kravene i § 14-3 oppfylles.",
            "(5) For yrkesbygning skal det beregnes et energibudsjett med reelle verdier for den konkrete " +
            "bygningen. Denne beregningen kommer i tillegg til kontrollberegningen med normerte verdier.");

        return noder;
    }

    [Fact]
    public void M11_verbmarkor_byggteknisk_forskrift_gir_de_fire_ekte_treffene_med_lav_konfidens()
    {
        // Issue #214 akseptansekriterium 1 og 3.
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(ByggtekniskForskriftM11Verbparagrafer());

        Assert.Equal(4, funn.Count);
        Assert.All(funn, f =>
        {
            Assert.Equal("M11", f.MonsterId);
            Assert.Equal("egen_paragraf", f.Kildetype);
            Assert.Equal("lav", f.Konfidens); // IKKE 'hoy' — "beregnes" er like ofte en pliktregel.
            Assert.Equal("hele_dokumentet", f.Scope);
            Assert.Null(f.ScopeRefEid);
        });

        var bya = funn.Single(f => f.Begrep == "bebygd areal");
        Assert.Equal("Bebygd areal", bya.BegrepOriginal);
        Assert.StartsWith("Bebygd areal beregnes etter Norsk Standard NS 3940:2012", bya.Definisjon);
        Assert.Equal("https://lovdata.no/eli/forskrift/2017/06/19/840/nor/§5-2/ledd-1", bya.NodeEid);
        Assert.Equal("Bebygd areal", bya.Definisjon[bya.StartOffset..bya.EndOffset]);

        var prosentBya = funn.Single(f => f.Begrep == "prosent bebygd areal");
        Assert.StartsWith("Prosent bebygd areal angir forholdet mellom", prosentBya.Definisjon);

        var prosentBra = funn.Single(f => f.Begrep == "prosent bruksareal");
        Assert.StartsWith("Prosent bruksareal angir forholdet mellom", prosentBra.Definisjon);
    }

    [Fact]
    public void M11_verbmarkor_finner_definisjonen_i_ANDRE_ledd_nar_den_star_der()
    {
        // § 5-4 "Bruksareal (BRA)": FØRSTE ledd er "(1) Bruksareal for bebyggelse ... og angis i hele
        // tall." (pliktform, ingen definisjon), definisjonen står i ANDRE ledd. Verbgrenen leter derfor
        // gjennom alle ledd — "menes"-grenen ser fortsatt bare på det første, bevisst.
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(ByggtekniskForskriftM11Verbparagrafer());

        var bra = funn.Single(f => f.Begrep == "bruksareal");
        Assert.Equal("https://lovdata.no/eli/forskrift/2017/06/19/840/nor/§5-4/ledd-2", bra.NodeEid);
        Assert.StartsWith("(2) Bruksareal beregnes etter", bra.Definisjon);
        // Tegn-intervallet peker forbi "(2) "-leddnummeret, rett på termen.
        Assert.Equal(4, bra.StartOffset);
        Assert.Equal("Bruksareal", bra.Definisjon[bra.StartOffset..bra.EndOffset]);
    }

    [Fact]
    public void M11_verbmarkor_gir_ingen_treff_pa_par_14_1_generelle_krav()
    {
        // Issue #214 akseptansekriterium 2, falsk positiv nr. 1, med den FAKTISKE teksten: verbet
        // "beregnes" står der, men overskriften er "Generelle krav" og subjektet er "U-verdier" — og
        // "skal" bryter dessuten naboskapet mellom term og markør.
        var alle = ByggtekniskForskriftM11Verbparagrafer();
        var paragrafId = alle.Single(n => n.NodeType == "paragraf" && n.Overskrift == "Generelle krav").Id;
        var kun141 = alle.Where(n => n.Id == paragrafId || n.ParentNodeId == paragrafId).ToList();

        Assert.Contains(kun141, n => n.Tekst is not null && n.Tekst.Contains("skal beregnes som gjennomsnitt"));
        Assert.Empty(BegrepsoppdagelseSveipTjeneste.FinnForekomster(kun141));
    }

    [Fact]
    public void M11_verbmarkor_gir_ingen_treff_pa_par_14_2_krav_til_energieffektivitet()
    {
        // Issue #214 akseptansekriterium 2, falsk positiv nr. 2, med den FAKTISKE teksten:
        // "(5) For yrkesbygning skal det beregnes et energibudsjett ..." — overskriften er "Krav til
        // energieffektivitet" og står ikke noe sted i leddet.
        var alle = ByggtekniskForskriftM11Verbparagrafer();
        var paragrafId = alle.Single(n => n.NodeType == "paragraf" && n.Overskrift == "Krav til energieffektivitet").Id;
        var kun142 = alle.Where(n => n.Id == paragrafId || n.ParentNodeId == paragrafId).ToList();

        Assert.Contains(kun142, n => n.Tekst is not null && n.Tekst.Contains("skal det beregnes et energibudsjett"));
        Assert.Empty(BegrepsoppdagelseSveipTjeneste.FinnForekomster(kun142));
    }

    [Fact]
    public void M11_verbmarkor_krever_at_termen_star_umiddelbart_foran_markoren()
    {
        // Den strukturelle vakten isolert: HER er overskriften faktisk termen, og verbet står i leddet —
        // men med "skal" mellom. Det er en pliktregel om noe som allerede er definert, ikke en
        // definisjon. Dette er den eneste vakten som skiller de to, og den er derfor testet alene.
        var paragrafId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§14-3", "paragraf", "Varmetapstall", null, 1, false),
            new NodeSnapshot(Guid.NewGuid(), paragrafId, "https://test/§14-3/ledd-1", "ledd", null,
                "(1) Varmetapstall skal beregnes etter Norsk Standard NS 3031:2014.", 2, false),
        ];

        Assert.Empty(BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder));
    }

    [Fact]
    public void M11_verbmarkor_krever_at_termen_star_ved_leddets_start()
    {
        // Samme term og samme markør, men termen står midt i leddet som objekt, ikke som subjekt ved
        // starten — "(3) Ved søknad om tillatelse skal bebygd areal beregnes på nytt." definerer ingenting.
        var paragrafId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§5-9", "paragraf", "Bebygd areal", null, 1, false),
            new NodeSnapshot(Guid.NewGuid(), paragrafId, "https://test/§5-9/ledd-1", "ledd", null,
                "(3) Ved søknad om tillatelse skal bebygd areal beregnes på nytt.", 2, false),
        ];

        Assert.Empty(BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder));
    }

    [Fact]
    public void M11_verbmarkor_matcher_term_selv_om_overskriften_barer_kortform_i_parentes()
    {
        // Issue #214 akseptansekriterium 4: overskriften er "Bebygd areal (BYA)", teksten sier "Bebygd
        // areal". Kortformen "BYA" LAGRES IKKE noe sted i denne runden — testen fester den avgrensningen
        // eksplisitt, slik at den er et dokumentert valg og ikke en stille forkasting: finnes det senere
        // et kortform-/alias-felt, skal denne assertionen endres bevisst.
        var funn = BegrepsoppdagelseSveipTjeneste.FinnForekomster(ByggtekniskForskriftM11Verbparagrafer());

        var bya = funn.Single(f => f.Begrep == "bebygd areal");
        Assert.Equal("Bebygd areal", bya.BegrepOriginal);
        Assert.DoesNotContain("BYA", bya.Begrep);
        Assert.DoesNotContain("(", bya.BegrepOriginal);
        // %-BYA/%-BRA: parentesinnholdet kan inneholde tegn som ville vært regex-metategn — dekket av at
        // hele hale-parentesen fjernes før termen escapes.
        Assert.Contains(funn, f => f.Begrep == "prosent bebygd areal");
    }

    [Fact]
    public void M11_verbmarkor_hopper_over_paragraf_som_alt_har_et_menes_treff()
    {
        // Ett paragraf-treff, ikke to: et lavkonfidens-duplikat av en definisjon som alt er fanget med
        // høy konfidens er støy i køen, ikke ny informasjon.
        var paragrafId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(paragrafId, null, "https://test/§5-2", "paragraf", "Bebygd areal", null, 1, false),
            new NodeSnapshot(Guid.NewGuid(), paragrafId, "https://test/§5-2/ledd-1", "ledd", null,
                "Med bebygd areal menes i forskriften her det arealet bygningen opptar av terrenget.", 2, false),
            new NodeSnapshot(Guid.NewGuid(), paragrafId, "https://test/§5-2/ledd-2", "ledd", null,
                "Bebygd areal beregnes etter Norsk Standard NS 3940:2012.", 3, false),
        ];

        var treff = Assert.Single(BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder));

        Assert.Equal("hoy", treff.Konfidens); // "menes"-treffet, ikke verbtreffet.
    }

    [Fact]
    public void M11_verbmarkor_gir_ingen_treff_pa_opphevet_paragraf_eller_ledd()
    {
        var opphevetParagrafId = Guid.NewGuid();
        var levendeParagrafId = Guid.NewGuid();
        List<NodeSnapshot> noder =
        [
            new NodeSnapshot(opphevetParagrafId, null, "https://test/§5-2", "paragraf", "Bebygd areal", null, 1, true),
            new NodeSnapshot(Guid.NewGuid(), opphevetParagrafId, "https://test/§5-2/ledd-1", "ledd", null,
                "Bebygd areal beregnes etter Norsk Standard NS 3940:2012.", 2, false),
            new NodeSnapshot(levendeParagrafId, null, "https://test/§5-3", "paragraf", "Bruksareal", null, 3, false),
            new NodeSnapshot(Guid.NewGuid(), levendeParagrafId, "https://test/§5-3/ledd-1", "ledd", null,
                "Bruksareal beregnes etter Norsk Standard NS 3940:2012.", 4, true),
        ];

        Assert.Empty(BegrepsoppdagelseSveipTjeneste.FinnForekomster(noder));
    }

    // ---------- Del B: sveip + kø mot ekte embedded Postgres ----------

    private static BegrepsoppdagelseSveipTjeneste NySveip(RegelIdeDbContext db) =>
        new(db, new BegrepsforekomstTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new BegrepsregisterTjeneste(db)));

    /// <summary>Fersk, syntetisk, DELT/nasjonal (VirksomhetId=null) rettskilde med en M1-definisjonsparagraf
    /// — samme "egen syntetisk rettskilde per test" -mønster som
    /// <c>VirksomhetKandidatTjenesteTests.OpprettSyntetiskRettskildeAsync</c>, av samme grunn (DB-en er
    /// DELT mellom alle tester i samlingen).</summary>
    private static async Task<(Guid RettskildeId, string PunktEid)> OpprettSyntetiskM1RettskildeAsync(RegelIdeDbContext db)
    {
        var rettskildeId = Guid.NewGuid();
        var basis = $"https://test/{rettskildeId:N}";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testforskrift " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var paragrafId = Guid.NewGuid();
        var leddId = Guid.NewGuid();
        var punktEid = $"{basis}/§1/ledd-1/punkt-1";
        db.RettskildeNoder.AddRange(
            new RettskildeNodeEntitet
            {
                Id = paragrafId, RettskildeId = rettskildeId, Eid = $"{basis}/§1", KildeId = "§1",
                NodeType = "paragraf", Overskrift = "Definisjoner", Sorteringsrekkefolge = 1,
            },
            new RettskildeNodeEntitet
            {
                Id = leddId, RettskildeId = rettskildeId, Eid = $"{basis}/§1/ledd-1", KildeId = "ledd-1",
                ParentNodeId = paragrafId, NodeType = "ledd", Tekst = "I forskriften her menes med", Sorteringsrekkefolge = 2,
            },
            new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = punktEid, KildeId = "punkt-1",
                ParentNodeId = leddId, NodeType = "punkt", Tekst = "testbegrep: en testdefinisjon", Sorteringsrekkefolge = 3,
            });
        await db.SaveChangesAsync();
        return (rettskildeId, punktEid);
    }

    [Fact]
    public async Task Sveip_oppretter_forekomst_med_riktig_felter()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, punktEid) = await OpprettSyntetiskM1RettskildeAsync(db);

        var resultat = await NySveip(db).SveipAsync(rettskildeId, "sveip");

        Assert.Equal(1, resultat.AntallTreffFunnet);
        Assert.Equal(1, resultat.AntallNyeForekomster);
        var forekomst = await db.Begrepsforekomster.SingleAsync(f => f.RettskildeId == rettskildeId);
        Assert.Equal(punktEid, forekomst.NodeEid);
        Assert.Equal("testbegrep", forekomst.Begrep);
        Assert.Equal("en testdefinisjon", forekomst.Definisjon);
        Assert.Equal("M1", forekomst.MonsterId);
        Assert.Equal("Venter", forekomst.Status);
    }

    [Fact]
    public async Task Gjentatt_sveip_gir_ingen_duplikate_forekomster()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, _) = await OpprettSyntetiskM1RettskildeAsync(db);
        var sveip = NySveip(db);

        var forste = await sveip.SveipAsync(rettskildeId, "sveip");
        var andre = await sveip.SveipAsync(rettskildeId, "sveip");

        Assert.Equal(1, forste.AntallNyeForekomster);
        Assert.Equal(0, andre.AntallNyeForekomster); // samme treff igjen, men ingen ny rad.
        Assert.Equal(1, await db.Begrepsforekomster.CountAsync(f => f.RettskildeId == rettskildeId));
    }

    [Fact]
    public async Task Sveip_mot_rettskilde_eid_av_en_virksomhet_kastes()
    {
        // Samme defensive delt/nasjonal-scoping som VirksomhetKandidatSveipTjeneste/
        // NavnekandidatOppdagelseTjeneste — se klassekommentaren.
        await using var db = _fixture.NyDbContext();
        var rettskildeId = Guid.NewGuid();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, VirksomhetId = virksomhet.Id, Doctype = "doc", Kildetype = "Forskrift",
            Status = "Gjeldende", Importrolle = "referanse", Tittel = "Lokal forskrift " + rettskildeId,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => NySveip(db).SveipAsync(rettskildeId, "sveip"));
    }

    /// <summary>[Ny, definisjonsmønster-runden, 2026-09-09] Fersk, syntetisk DELT/nasjonal rettskilde med
    /// byggteknisk forskrifts EKTE §-5-2-struktur (overskrift med kortform i parentes, definisjonen i
    /// første ledd) — samme "egen syntetisk rettskilde per test" -mønster som
    /// <see cref="OpprettSyntetiskM1RettskildeAsync"/>, av samme grunn (DB-en er DELT).</summary>
    private static async Task<(Guid RettskildeId, string LeddEid)> OpprettSyntetiskVerbmonsterRettskildeAsync(
        RegelIdeDbContext db)
    {
        var rettskildeId = Guid.NewGuid();
        var basis = $"https://test/{rettskildeId:N}";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testforskrift " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var paragrafId = Guid.NewGuid();
        var leddEid = $"{basis}/§5-2/ledd-1";
        db.RettskildeNoder.AddRange(
            new RettskildeNodeEntitet
            {
                Id = paragrafId, RettskildeId = rettskildeId, Eid = $"{basis}/§5-2", KildeId = "§5-2",
                NodeType = "paragraf", Overskrift = "Bebygd areal (BYA)", Sorteringsrekkefolge = 1,
            },
            new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = leddEid, KildeId = "ledd-1",
                ParentNodeId = paragrafId, NodeType = "ledd", Sorteringsrekkefolge = 2,
                Tekst = "Bebygd areal beregnes etter Norsk Standard NS 3940:2012 Areal- og volumberegninger " +
                        "av bygninger, men slik at parkeringsarealet inngår i beregningsgrunnlaget etter § 5-7.",
            });
        await db.SaveChangesAsync();
        return (rettskildeId, leddEid);
    }

    [Fact]
    public async Task Sveip_verbmonster_lander_i_koen_med_lav_konfidens()
    {
        // Issue #214 akseptansekriterium 3, ende-til-ende: dette er samtidig BEVISET for at ingen
        // migrasjon er nødvendig — CHECK-constraintene ck_begrepsforekomster_monster_id ('M1'…'M17'),
        // ck_begrepsforekomster_kildetype og ck_begrepsforekomster_konfidens ('hoy'|'middels'|'lav'|
        // 'krever_oppslag') godtar alle tre verdiene verbklassen bruker som de står i dag.
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, leddEid) = await OpprettSyntetiskVerbmonsterRettskildeAsync(db);

        var resultat = await NySveip(db).SveipAsync(rettskildeId, "sveip");

        Assert.Equal(1, resultat.AntallNyeForekomster);
        var forekomst = await db.Begrepsforekomster.SingleAsync(f => f.RettskildeId == rettskildeId);
        Assert.Equal(leddEid, forekomst.NodeEid);
        Assert.Equal("bebygd areal", forekomst.Begrep);
        Assert.Equal("Bebygd areal", forekomst.BegrepOriginal);
        Assert.Equal("M11", forekomst.MonsterId);
        Assert.Equal("egen_paragraf", forekomst.Kildetype);
        Assert.Equal("lav", forekomst.Konfidens);
        Assert.Equal("Venter", forekomst.Status); // lav konfidens er en ventende tilstand, ikke en avvisning.
    }

    [Fact]
    public async Task Lister_kan_filtreres_pa_konfidens()
    {
        // Issue #214 akseptansekriterium 3, andre setning ("Kandidatlisten kan filtreres på det") — samme
        // filter GET /api/begrepsforekomster?konfidens= nå eksponerer, og den eneste måten å skille M11s
        // to markørklasser fra hverandre, siden de deler MonsterId.
        await using var db = _fixture.NyDbContext();
        var (m1RettskildeId, _) = await OpprettSyntetiskM1RettskildeAsync(db);
        var (verbRettskildeId, _) = await OpprettSyntetiskVerbmonsterRettskildeAsync(db);
        var sveip = NySveip(db);
        await sveip.SveipAsync(m1RettskildeId, "sveip");
        await sveip.SveipAsync(verbRettskildeId, "sveip");
        var kø = new BegrepsforekomstTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new BegrepsregisterTjeneste(db));

        var lave = await kø.ListerAsync(verbRettskildeId, konfidens: "lav");
        var høye = await kø.ListerAsync(verbRettskildeId, konfidens: "hoy");
        var alle = await kø.ListerAsync(verbRettskildeId);

        Assert.Single(lave);
        Assert.Empty(høye);
        Assert.Single(alle); // konfidens utelatt = ingen filter, ikke en skjult standardverdi.
        Assert.Single(await kø.ListerAsync(m1RettskildeId, konfidens: "hoy"));
        Assert.Empty(await kø.ListerAsync(m1RettskildeId, konfidens: "lav"));
    }
}
