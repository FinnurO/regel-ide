using RegelIde.Data.Strukturkonvertering;
using Xunit.Abstractions;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Måler <see cref="MonsterStrukturkonverterer"/> mot
/// strukturfasiten for de fem kildene (docs/33 §5.4) og skriver rapporten.
/// <para>
/// Bevisst UTEN <c>DataTestCollection</c>/embedded Postgres: målingen leser JSON fra
/// <c>data/fasit/strukturmodell/</c> og skal gå i en vanlig testkjøring uten database og nettverk
/// (#307 akseptansekriterium 1). Den kan derfor også kjøre parallelt med DB-testene.
/// </para>
/// </summary>
public class MonsterStrukturkonvertererMalingTests(ITestOutputHelper output)
{
    /// <summary>
    /// Mønstre som ble prøvd og tatt ut, med grunnen. Skrives inn i rapporten, så neste runde ikke prøver
    /// det samme igjen uten å vite hvorfor det ble forkastet (CLAUDE.md §7: avviste alternativer er
    /// dokumentasjon).
    /// </summary>
    internal static readonly IReadOnlyList<(string Id, string Grunn)> ForkastedeMonstre =
    [
        ("vedtak-forvaltningsverb: verbet «pålegge»",
            "Prøvd sammen med «gi pålegg/dispensasjon, ilegge, trekke tilbake». Formen ga 11 utsagn, hvorav 8 falske positive: «En domstol kan pålegge en klager …», «Retten kan pålegge …», «Arbeidsgiver kan pålegge helsepersonell …», «Kommunen kan pålegge personell …» — prosessuelle pålegg og arbeidsgivers instruks, ikke enkeltvedtak. De 3 riktige: energiloven § 5-3 og § 10-1a («Departementet kan pålegge ethvert fjernvarmeanlegg …», «Konsesjonsmyndigheten kan … pålegge konsesjonæren …») og helse- og omsorgstjenesteloven § 6-6 («Departementet kan pålegge samarbeid mellom kommuner»). Uten verbet: 26 av 26."),
        ("tilsyn-forer-tilsyn: alle objekter etter «tilsyn med»",
            "5 av 9. Tre av de fire feilene var tilsyn med en AKTØR («fører tilsyn med forliksrådets virksomhet», «med daglig leder», «med helseforetak»), den fjerde «Riksrevisjonen fører kontroll med forvaltningen av statens interesser» — samme skille som korpusmålingen i docs/33 §1 viste. Nå bare «tilsyn/kontroll med at …» og «med lovligheten/etterlevelsen/gjennomføringen/overholdelsen av …»."),
        ("vedtak-godkjennes-av: «er godkjent av X»",
            "6 av 12. Perfektum partisipp beskriver en tilstand eller et vilkår («Når avviklingsoppgjøret er godkjent av foretaksmøtet, skal …», «før det er godkjent av statsforvalteren»), ikke hvem som har kompetansen. Nå bare «skal/må/kan (være) godkjennes/godkjent av»."),
        ("Navneliste: siste element med to «og» delt på det første",
            "Ga «Møre» + «Romsdal og Trööndelagen/Trøndelag» som lagsogn (domstolloven-inndelingen § 12). Tvetydig — teksten sier ikke hvilket «og» som er inne i et navn — så hele lista forkastes nå."),
        ("oppnevnt-av: ordet rett foran finitt passiv som den oppnevnte",
            "«Dommere til Høyesterett, …, tingrettene og jordskifterettene utnevnes … av Kongen» ga «jordskifterettene». For «oppnevnes/utnevnes» brukes nå setningens subjekt."),
        ("(ikke bygget) rapporterer_til",
            "Fasiten har 31, men uttrykt som «sende melding til», «varsle», «forelegges», «underrette» — formuleringer som like ofte er informasjonsplikter for private (docs/33 §1: «rapporterer til» 45 %). Ingen form med høy nok presisjon til mønsterlaget; overlatt til KI-laget (#308)."),
        ("(ikke bygget) A har_ansvarsomrade tingrett → egen rettskrets",
            "Fasiten hadde 52 slike («Vestre Finnmark tingrett» har ansvarsområde «Vestre Finnmark tingrett»), men rettskretsens navn står ikke i teksten. [Løst i #312, 2026-10-08:] Johann forkastet rettskrets-aktørene i fasitkontrollen; fasiten er rettet til «tingrett har_ansvarsomrade kommune», som inndeling-rettskrets nå gir direkte."),
        ("inndeling-har-rettskretsen (fjernet i #312)",
            "«X fylke har rettskretsen N tingrett» → N del_av X fylke uttrykte tingretten som et område. Fjernet sammen med rettskrets-aktørene i fasiten (Johanns funn på #312)."),
    ];

    // Regresjonsvern per kanttype: [målt verdi 2026-10-07] − 5 prosentpoeng, for presisjon og gjenfinning
    // med endepunktkrav. [FORELØPIG — #307 akseptansekriterium 3] Tersklene låses først etter den
    // menneskelige gjennomgangen av fasiten i #309: før det er en «feil» like gjerne en fasitfeil, og et
    // vern som er strengere enn målingen ville bare ha låst fast fasitens nåværende tilfeldigheter.
    // Kanttyper uten predikerte utsagn (M, I, T) har ingen presisjon å verne; gjenfinningen er 0.
    private static readonly IReadOnlyDictionary<string, (double Presisjon, double Gjenfinning)> MaltPerKategori =
        new Dictionary<string, (double, double)>
        {
            // [ENDRET, issue #341, 2026-10-08] Målt på nytt etter at fasiten ble konvertert (konvertering-341-kompetanse.py):
            // myndighetsrelasjonene er flyttet fra R til K (kompetanse med motpart), og K har fått hele typologien. R er nå
            // bare struktur + har_delegert_til (139 fasitutsagn, mønsterlaget finner bare delegeringsvedtakets form) — derfor
            // den lave gjenfinningen; K-nevneren vokste fra 427 til 557. Før: R 0,778/0,126, K 0,885/0,597.
            ["R"] = (0.750, 0.022),
            ["K"] = (0.877, 0.510),
            // [Ny, issue #353, 2026-10-09] P (plikt overfor motpart) målt første gang etter konvertering-353-plikt.py: 79 i fasiten,
            // 30 predikert av de seks nye pliktmønstrene, 17 treff. 7 av de 13 falske positive er «ikke i fasiten» og ser ut som
            // fasitutelatelser («Utgiftene dekkes av det offentlige», «Kommunen dekker reiseutgifter …») — vurderes i #309.
            ["P"] = (0.567, 0.215),
            // [ENDRET, issue #312, 2026-10-08] O og A målt på nytt etter den systemiske rettelsen av domstollovens
            // inndelingsdel (rettelse-312-domstolinndeling.py): de 357 kommunelisteradene er flyttet fra O (rettskrets
            // består av kommune) til A (tingrett har ansvarsområde i kommune). Før: O 0,974/0,858, A 1,000/0,517.
            ["O"] = (0.893, 0.616),
            ["A"] = (1.000, 0.933),
            ["G"] = (0.667, 0.093),
        };

    private const double Slingringsmonn = 0.05;

    [Fact]
    public void Maling_mot_fasiten_skriver_rapport_og_holder_tersklene()
    {
        var kilder = StrukturfasitLeser.LesAlle();
        var konverterer = new MonsterStrukturkonverterer();
        var malinger = kilder
            .Select(k => Strukturmaling.Mal(k.Navn, k.Fasit, konverterer.Konverter(k.Grunnlag)))
            .ToList();

        var rapport = Malerapport.Lag(malinger, ForkastedeMonstre);
        output.WriteLine(rapport);
        File.WriteAllText(Path.Combine(StrukturfasitLeser.FasitMappe, "maling-monster.md"), rapport);

        // [LÅST, #307 akseptansekriterium 3] K forskriftskompetanse: presisjon ≥ 0,9 mot fasiten. [ENDRET, #341] Forskrifts-
        // kompetanse er nå normgivningskompetanse (normform forskrift) — alle 205 fasitradene ble konvertert, ingen andre.
        var forskrift = Tall.For(malinger, r => r.Utsagn.Type == "normgivningskompetanse");
        Assert.True(forskrift.Presisjon >= 0.9,
            $"Presisjon K normgivningskompetanse er {Malerapport.P(forskrift.Presisjon)}, krav ≥ 90 %.");

        var brudd = new List<string>();
        foreach (var (bokstav, malt) in MaltPerKategori)
        {
            var t = Tall.For(malinger, r => r.Bokstav == bokstav);
            if (!(t.Presisjon >= malt.Presisjon - Slingringsmonn))
                brudd.Add($"{bokstav} presisjon {Malerapport.P(t.Presisjon)} < {Malerapport.P(malt.Presisjon - Slingringsmonn)}");
            if (!(t.Gjenfinning >= malt.Gjenfinning - Slingringsmonn))
                brudd.Add($"{bokstav} gjenfinning {Malerapport.P(t.Gjenfinning)} < {Malerapport.P(malt.Gjenfinning - Slingringsmonn)}");
        }
        Assert.True(brudd.Count == 0,
            "Regresjon mot målt nivå (se data/fasit/strukturmodell/maling-monster.md): " + string.Join("; ", brudd));
    }
}
