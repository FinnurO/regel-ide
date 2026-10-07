# Måling: mønsterkonvertering mot strukturfasiten

Generert av `MonsterStrukturkonvertererMalingTests` (`src/RegelIde.Data.Tests/Strukturfasit/`) — **ikke rediger for hånd**.
Kjør `dotnet test src/RegelIde.Data.Tests --filter "FullyQualifiedName~Strukturfasit"` for å regenerere. Sak: #307, designgrunnlag: `docs/33` §5.

**Forbehold:** fasiten er KI-annotert og ikke menneskelig verifisert (`docs/33` §2). Tallene er bare så gode som fasiten,
og tersklene i testen er regresjonsvern, ikke kvalitetskrav — de låses først etter gjennomgangen i #309.

**Treffregel:** samme eId + kategori + type, og for hvert endepunkt fasiten har (fra/til): samme tekstform uten skille på store/små
bokstaver, der «tekstform» er aktørens tekstform ∪ varianter (minst én felles skrivemåte). Én-til-én. «Uten endepunktkrav» =
bare eId + kategori + type — forskjellen mellom de to viser hvor mye av feilen som er feil/manglende aktør, ikke feil gjenkjenning.

## Totalt

1865 fasitutsagn, 814 predikerte, 758 treff → presisjon **93,1 %**, gjenfinning **40,6 %**
(uten endepunktkrav: presisjon 95,6 %, gjenfinning 41,7 %).

## Per kategori (kanttype, `docs/33` §4.3)

Bokstaven følger `STD`-tabellen i `designtest.py`. `annet:*`-typer står i egen rad; `bistar`/`samarbeider_med` er bevisst senere lag (§4.4).

| Kategori | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| R | 223 | 36 | 23 | 63,9 % | 10,3 % | 94,4 % | 15,2 % |
| K | 427 | 249 | 215 | 86,3 % | 50,4 % | 87,1 % | 50,8 % |
| M | 48 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| O | 578 | 462 | 455 | 98,5 % | 78,7 % | 99,6 % | 79,6 % |
| A | 120 | 61 | 61 | 100,0 % | 50,8 % | 100,0 % | 50,8 % |
| G | 43 | 6 | 4 | 66,7 % | 9,3 % | 100,0 % | 14,0 % |
| I | 0 | 0 | 0 | – | – | – | – |
| T | 21 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| annet | 366 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| senere lag | 39 | 0 | 0 | – | 0,0 % | – | 0,0 % |

## Per kilde

| Kilde | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| domstolloven | 832 | 539 | 509 | 94,4 % | 61,2 % | 96,3 % | 62,4 % |
| energiloven | 254 | 89 | 83 | 93,3 % | 32,7 % | 95,5 % | 33,5 % |
| helse-og-omsorgstjenesteloven | 192 | 45 | 39 | 86,7 % | 20,3 % | 93,3 % | 21,9 % |
| sameloven | 285 | 80 | 73 | 91,3 % | 25,6 % | 96,3 % | 27,0 % |
| spesialisthelsetjenesteloven | 302 | 61 | 54 | 88,5 % | 17,9 % | 90,2 % | 18,2 % |

## Per type mønsterlaget produserer

| Kategori / type | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| K kompetanse / forskriftskompetanse | 205 | 192 | 178 | 92,7 % | 86,8 % | 93,2 % | 87,3 % |
| K kompetanse / vedtakskompetanse | 158 | 57 | 37 | 64,9 % | 23,4 % | 66,7 % | 24,1 % |
| R relasjon / klageinstans_for | 17 | 9 | 5 | 55,6 % | 29,4 % | 100,0 % | 52,9 % |
| R relasjon / administrativt_underordnet | 5 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| G organsammensetning / har_medlemmer | 24 | 6 | 4 | 66,7 % | 16,7 % | 100,0 % | 25,0 % |
| R relasjon / oppnevner | 23 | 10 | 6 | 60,0 % | 26,1 % | 90,0 % | 39,1 % |
| R relasjon / instruksjon | 21 | 4 | 4 | 100,0 % | 19,0 % | 100,0 % | 19,0 % |
| R relasjon / delegerer_til | 36 | 13 | 8 | 61,5 % | 22,2 % | 92,3 % | 33,3 % |
| O sammensetning_omrade / bestar_av | 493 | 462 | 455 | 98,5 % | 92,3 % | 99,6 % | 93,3 % |
| A ansvarsomrade / har_sete_i | 65 | 61 | 61 | 100,0 % | 93,8 % | 100,0 % | 93,8 % |

### Per kilde, K forskriftskompetanse (terskel ≥ 0,9 presisjon)

| Kilde | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| domstolloven | 40 | 36 | 31 | 86,1 % | 77,5 % | 86,1 % | 77,5 % |
| energiloven | 64 | 60 | 57 | 95,0 % | 89,1 % | 96,7 % | 90,6 % |
| helse-og-omsorgstjenesteloven | 38 | 36 | 35 | 97,2 % | 92,1 % | 97,2 % | 92,1 % |
| sameloven | 16 | 14 | 12 | 85,7 % | 75,0 % | 85,7 % | 75,0 % |
| spesialisthelsetjenesteloven | 47 | 46 | 43 | 93,5 % | 91,5 % | 93,5 % | 91,5 % |

## Per mønster

| Mønster | Type | Predikert | Treff | Presisjon | Korpusgrunnlag (`docs/33` §1) |
|---|---|---:|---:|---:|---|
| `forskrift-gi` | forskriftskompetanse | 104 | 96 | 92,3 % | docs/33 §1: «kan gi forskrift om» 5651 treff, 100 % presisjon. |
| `forskrift-i-ved` | forskriftskompetanse | 64 | 61 | 95,3 % | docs/33 §1: variant av «kan gi forskrift om» (5651 treff, 100 %); formen er ikke målt separat. |
| `forskrift-naermere-regler` | forskriftskompetanse | 17 | 16 | 94,1 % | Ikke målt i docs/33 §1. Med fordi lovteksten bruker «gi nærmere regler» om forskrift uten å si ordet; presisjonen måles mot fasiten. |
| `forskrift-gitt-av` | forskriftskompetanse | 7 | 5 | 71,4 % | Ikke målt i docs/33 §1 (passiv form av forskriftskompetanse). Krever at aktøren står i setningen. |
| `vedtak-treffe` | vedtakskompetanse | 24 | 20 | 83,3 % | docs/33 §1: «treffer vedtak / avgjøres av» 1465 treff, 73 % presisjon. |
| `vedtak-avgjores-av` | vedtakskompetanse | 26 | 12 | 46,2 % | docs/33 §1: «treffer vedtak / avgjøres av» 1465 treff, 73 %. Krever at aktøren står i setningen — «av» uten aktør er ikke et kompetanseutsagn. |
| `vedtak-avgjor` | vedtakskompetanse | 7 | 5 | 71,4 % | Ikke målt separat i docs/33 §1 (del av «avgjøres av»-familien). |
| `klage-paklages-til` | klageinstans_for | 4 | 1 | 25,0 % | docs/33 §1: «klageinstans for / påklages til» 1718 treff, 67 % presisjon. Førsteinstansen er ofte implisitt. |
| `klage-er-klageinstans` | klageinstans_for | 3 | 2 | 66,7 % | docs/33 §1: «klageinstans for / påklages til» 1718 treff, 67 %. |
| `klage-ikke-paklages` | klageinstans_for | 2 | 2 | 100,0 % | docs/33 §1: del av «klageinstans for / påklages til» (1718 treff, 67 %); negativ form ikke målt separat. |
| `administrativt-underordnet` | administrativt_underordnet | 0 | 0 | – | docs/33 §1: 775 treff på «underordnet», 13 % — signalet er KUN i frasen «administrativt underordnet», som er det eneste mønsteret tar. |
| `har-medlemmer` | har_medlemmer | 6 | 4 | 66,7 % | docs/33 §1: «består av N medlemmer» 147 treff, 100 % presisjon. |
| `oppnevnt-av` | oppnevner | 5 | 1 | 20,0 % | docs/33 §1: «oppnevner / utnevner» 1422 treff, 33 % presisjon. |
| `oppnevner-aktivt` | oppnevner | 5 | 5 | 100,0 % | docs/33 §1: «oppnevner / utnevner» 1422 treff, 33 %. |
| `instruksjon-kan-ikke-instrueres` | instruksjon | 4 | 4 | 100,0 % | docs/33 §1: «instruere» 908 treff, 27 % — og nesten bare NEGATIV («kan ikke instruere») = uavhengighet. Mønsteret tar bare den negative formen. |
| `instruksjon-kan-ikke-instruere` | instruksjon | 0 | 0 | – | docs/33 §1: «instruere» 908 treff, 27 %, nesten bare negativ. |
| `delegerer-kan-delegere` | delegerer_til | 9 | 8 | 88,9 % | docs/33 §1: «delegerer til» 2875 treff, 47 % presisjon — nesten alltid avgrenset til paragraf (avgrensningen tolkes ikke her). |
| `delegerer-delegeres-til` | delegerer_til | 4 | 0 | 0,0 % | docs/33 §1: «delegerer til» 2875 treff, 47 %. |
| `inndeling-rettskrets` | bestar_av | 357 | 357 | 100,0 % | docs/33 §1 nevner strukturerte kommunelister i inndelingsforskrifter som høypresisjonskilde (ikke tallfestet). |
| `inndeling-rettssted` | har_sete_i | 61 | 61 | 100,0 % | Samme kilde som inndeling-rettskrets. |
| `inndeling-kommuneliste` | bestar_av | 56 | 56 | 100,0 % | Strukturerte kommunelister (docs/33 §1, ikke tallfestet). Retnings- og komplementdefinisjoner kan ikke avgjøres uten et områderegister (#312). |
| `inndeling-utgjor` | bestar_av | 17 | 10 | 58,8 % | Strukturerte lister i inndelingsforskrifter (docs/33 §1, ikke tallfestet). |
| `inndeling-sogner` | bestar_av | 28 | 28 | 100,0 % | Strukturerte lister i inndelingsforskrifter (docs/33 §1, ikke tallfestet). |
| `inndeling-bestar-av-liste` | bestar_av | 4 | 4 | 100,0 % | Ikke målt i docs/33 §1. Generell listeform; kravet om egennavn holder organsammensetning ute. |

## Falske positive (56)

Gruppert på mønster og om eId+kategori+type fantes i fasiten (= feil/manglende aktør) eller ikke (= feil gjenkjenning).

| Mønster | Årsak | Antall |
|---|---|---:|
| `monster:vedtak-avgjores-av` | ikke i fasiten | 13 |
| `monster:inndeling-utgjor` | feil/manglende aktør | 7 |
| `monster:forskrift-gi` | ikke i fasiten | 5 |
| `monster:vedtak-treffe` | ikke i fasiten | 4 |
| `monster:delegerer-delegeres-til` | feil/manglende aktør | 3 |
| `monster:forskrift-gi` | feil/manglende aktør | 3 |
| `monster:forskrift-i-ved` | ikke i fasiten | 3 |
| `monster:klage-paklages-til` | feil/manglende aktør | 3 |
| `monster:oppnevnt-av` | feil/manglende aktør | 3 |
| `monster:forskrift-gitt-av` | ikke i fasiten | 2 |
| `monster:har-medlemmer` | feil/manglende aktør | 2 |
| `monster:vedtak-avgjor` | ikke i fasiten | 2 |
| `monster:delegerer-delegeres-til` | ikke i fasiten | 1 |
| `monster:delegerer-kan-delegere` | feil/manglende aktør | 1 |
| `monster:forskrift-naermere-regler` | ikke i fasiten | 1 |
| `monster:klage-er-klageinstans` | feil/manglende aktør | 1 |
| `monster:oppnevnt-av` | ikke i fasiten | 1 |
| `monster:vedtak-avgjores-av` | feil/manglende aktør | 1 |

De 20 vanligste (én fra hver gruppe i tur, vanligste gruppe først):

| # | Kilde | eId | Mønster | fra → til | Sitat |
|---|---|---|---|---|---|
| 1 | domstolloven | lov/1915/08/13/5/nor/§4/ledd-1 | `monster:vedtak-avgjores-av` | fem → null | Når saksmengden gjør det påkrevd, kan Høyesterett, for saker som skal avgjøres av fem dommere, deles i flere avdelinger etter bestemmelse av høyesterettsjustiti … |
| 2 | domstolloven | forskrift/2021/01/22/163/nor/§12/ledd-1 | `monster:inndeling-utgjor` | Frostating lagdømme → Møre | Møre |
| 3 | energiloven | lov/1990/06/29/50/nor/§2-4/ledd-1 | `monster:forskrift-gi` | Kongen → null | Kongen kan gi forskrift om at nærmere bestemte vedtak etter § 3-1 skal fattes av Kongen i statsråd. |
| 4 | domstolloven | lov/1915/08/13/5/nor/§33/ledd-3 | `monster:vedtak-treffe` | Kongen i statsråd → null | Kongen i statsråd kan treffe vedtak om domstoladministrasjonens virksomhet og administrasjonen av domstolene. |
| 5 | domstolloven | forskrift/2012/02/03/119/nor/punkt-1 | `monster:delegerer-delegeres-til` | null → Justis- og beredskapsdepartementet | Kongens myndighet til å fastsette forskrifter etter lov 13. august 1915 nr. 5 om domstolene § 163a delegeres til Justis- og beredskapsdepartementet. |
| 6 | domstolloven | lov/1915/08/13/5/nor/§122/ledd-2 | `monster:forskrift-gi` | Kongen → null | Kongen kan gi forskrift om at opplysninger som nevnt skal gis ved oppslag ved rettens kontor. |
| 7 | domstolloven | lov/1915/08/13/5/nor/§86/ledd-1 | `monster:forskrift-i-ved` | Domstoladministrasjonen → null | Domstoladministrasjonen kan ved forskrift dele lagsogn og domssogn i flere trekningskretser. |
| 8 | helse-og-omsorgstjenesteloven | lov/2011/06/24/30/nor/§9-11/ledd-1 | `monster:klage-paklages-til` | statsforvalteren → null | Beslutning etter § 9-5 tredje ledd bokstav a kan påklages av brukeren eller pasienten, verge og pårørende til statsforvalteren. |
| 9 | domstolloven | lov/1915/08/13/5/nor/§43/ledd-2 | `monster:oppnevnt-av` | Kongen → særskilt organ | En granskingskommisjon, et kontrollutvalg eller et annet særskilt organ som er oppnevnt av Kongen, Stortinget eller et departement eller en statsforvalter for å … |
| 10 | domstolloven | lov/1915/08/13/5/nor/§105a/ledd-1 | `monster:forskrift-gitt-av` | Kongen → null | Godtgjørelsen til jordskiftemeddommer og meddommer fastsettes av rettens leder etter forskrifter gitt av Kongen. |
| 11 | domstolloven | lov/1915/08/13/5/nor/§27/ledd-2 | `monster:har-medlemmer` | Forliksrådet → medlemmer | Forliksrådet skal ha tre medlemmer og like mange varamedlemmer. |
| 12 | domstolloven | lov/1915/08/13/5/nor/§6/ledd-2 | `monster:vedtak-avgjor` | domstollederen → null | Domstollederen avgjør da om retten skal settes med 11 eller med alle Høyesteretts dommere. |
| 13 | sameloven | forskrift/2004/12/10/1607/nor/ledd-3 | `monster:delegerer-delegeres-til` | Kongen → Kommunal- og regionaldepartementet | Departementet foreslår at Kongens myndighet etter samelovens § 2-11 delegeres delvis til Kommunal- og regionaldepartementet. |
| 14 | energiloven | kap-I/ledd-2 | `monster:delegerer-kan-delegere` | Departementet → null | Departementet delegerer all myndighet etter lov 29. juni 1990 nr. 50 om produksjon, omforming, overføring, omsetning, fordeling og bruk av energi m.m. (energilo … |
| 15 | domstolloven | lov/1915/08/13/5/nor/§33c/ledd-2 | `monster:forskrift-naermere-regler` | Domstoladministrasjonen → null | Domstoladministrasjonen gir nærmere bestemmelser om organiseringen av disse dommernes tjenester. |
| 16 | sameloven | lov/1987/06/12/56/nor/§3-11/ledd-1 | `monster:klage-er-klageinstans` | Statsforvalteren → null | Statsforvalteren er klageinstans når klagen angår kommunale eller fylkeskommunale organ. |
| 17 | domstolloven | lov/1915/08/13/5/nor/§101/ledd-1 | `monster:oppnevnt-av` | retten → Rettsvitner | Rettsvitner oppnevnes av retten eller av den tjenestemann som skal styre forretningen. |
| 18 | helse-og-omsorgstjenesteloven | lov/2011/06/24/30/nor/§9-13/ledd-2 | `monster:vedtak-avgjores-av` | tvang → null | Som ledd i spesialisthelsetjenestens utførelse av oppgaver etter §§ 9-7 og 9-9, kan det treffes vedtak om bruk av tvang og makt i medhold av reglene i dette kap … |
| 19 | domstolloven | lov/1915/08/13/5/nor/§4/ledd-1 | `monster:vedtak-avgjores-av` | tre → null | For saker som skal avgjøres av tre dommere, kan Høyesterett nedsette ett eller flere utvalg, som betegnes som Høyesteretts ankeutvalg. |
| 20 | domstolloven | forskrift/2021/01/22/163/nor/§12/ledd-1 | `monster:inndeling-utgjor` | Frostating lagdømme → Romsdal og Trööndelagen/Trøndelag | Romsdal og Trööndelagen/Trøndelag |

## Falske negative (1107)

Gruppert på kategori/type. Typer mønsterlaget ikke har mønster for, er forventet her — de er KI-lagets (#308) nevner.

| Kategori / type | Har mønster | Antall |
|---|---|---:|
| K kompetanse / vedtakskompetanse | ja | 121 |
| O sammensetning_omrade / del_av | nei | 85 |
| A ansvarsomrade / har_ansvarsomrade | nei | 52 |
| O sammensetning_omrade / bestar_av | ja | 38 |
| R relasjon / rapporterer_til | nei | 31 |
| M medlemskap / medlem_av | nei | 30 |
| R relasjon / delegerer_til | ja | 28 |
| K kompetanse / forskriftskompetanse | ja | 27 |
| senere lag relasjon / samarbeider_med | nei | 24 |
| annet sammensetning_omrade / annet:gruppert_under | nei | 24 |
| K kompetanse / utpekingskompetanse | nei | 21 |
| T konstituerende / skal_finnes | nei | 21 |
| G organsammensetning / har_medlemmer | ja | 20 |
| M medlemskap / inngar_i | nei | 18 |
| R relasjon / instruksjon | ja | 17 |
| R relasjon / oppnevner | ja | 17 |
| R relasjon / velger | nei | 17 |
| K kompetanse / tilsynskompetanse | nei | 16 |
| senere lag relasjon / bistar | nei | 15 |
| R konstituerende / oppretter | nei | 14 |
| K kompetanse / oppnevningskompetanse | nei | 13 |
| R relasjon / eies_av | nei | 13 |
| annet relasjon / annet:informasjonsdeling | nei | 12 |
| R relasjon / klageinstans_for | ja | 12 |
| R relasjon / ledes_av | nei | 12 |
| annet annet:finansieringsansvar / annet:dekker_utgifter_for | nei | 11 |
| G organsammensetning / har_organ | nei | 10 |
| R relasjon / radgir | nei | 10 |
| R relasjon / tilsyn_med_aktor | nei | 10 |
| annet kompetanse / annet:forkynningskompetanse | nei | 9 |
| annet medlemskap / annet:klasse_definert_ved_tjenestekrets | nei | 9 |
| G relasjon / del_av | nei | 9 |
| annet relasjon / annet:konsultasjonsplikt_overfor | nei | 8 |
| annet annet:funksjonstildeling / annet:lovtildelt_oppgave | nei | 7 |
| annet medlemskap / annet:virkeomrade_utvidet_til | nei | 7 |
| annet organsammensetning / annet:valgkrets_for | nei | 7 |
| R relasjon / etterfolger | nei | 7 |
| annet kompetanse / annet:ikraftsettingskompetanse | nei | 6 |
| K kompetanse / instruksjonskompetanse | nei | 6 |
| annet relasjon / annet:representerer | nei | 6 |
| … 176 typer til (alle `annet:*` eller ≤ 5 utsagn) | | 287 |

De 20 vanligste blant typene med mønster (én fra hver type i tur, vanligste type først):

| # | Kilde | eId | Type | fra → til (fasit) | Sitat |
|---|---|---|---|---|---|
| 1 | domstolloven | lov/1915/08/13/5/nor/§13/ledd-1 | vedtakskompetanse | førstelagmann → null | Avgjørelser og andre forføyninger, som ikke gjelder de enkelte rettssaker, treffer førstelagmannen alene |
| 2 | domstolloven | lov/1915/08/13/5/nor/§16/ledd-1 | bestar_av | Rikets → lagdømmer | Rikets inddeling i lagdømmer |
| 3 | domstolloven | forskrift/2012/02/03/119/nor/punkt-1 | delegerer_til | Kongen → Justisdepartementet | Kongens myndighet til å fastsette forskrifter etter lov 13. august 1915 nr. 5 om domstolene § 163a delegeres til Justis- og beredskapsdepartementet. |
| 4 | domstolloven | lov/1915/08/13/5/nor/§48/ledd-4 | forskriftskompetanse | Kongen → null | ved regler, som Kongen gir |
| 5 | domstolloven | lov/1915/08/13/5/nor/§3/ledd-1 | har_medlemmer | Høyesterett → dommere | Retten skal ha en justitiarius og nitten andre dommere. |
| 6 | domstolloven | lov/1915/08/13/5/nor/§33/ledd-2 | instruksjon | Stortinget → domstoladministrasjonen | Gjennom Stortingets behandling av budsjettproposisjonen gis årlige retningslinjer for domstoladministrasjonens virksomhet |
| 7 | domstolloven | lov/1915/08/13/5/nor/§55/ledd-1 | oppnevner | Kongen → dommere | Dommere til Høyesterett, lagmannsrettene, tingrettene og jordskifterettene utnevnes som embetsmenn av Kongen etter Grunnloven § 21. |
| 8 | domstolloven | lov/1915/08/13/5/nor/§33b/ledd-1 | klageinstans_for | Kongen → styre | for styrets vedtak Kongen i statsråd |
| 9 | energiloven | lov/1990/06/29/50/nor/§9-1/ledd-5 | administrativt_underordnet | kraftforsyningen → Kraftforsyningens beredskapsorganisasjon (KBO) | Beredskapsmyndigheten kan under beredskap og i krig underlegge kraftforsyningen KBO. |
| 10 | domstolloven | lov/1915/08/13/5/nor/§3/ledd-1 | har_sete_i | Høyesterett → rikets hovedstad | Høyesterett har sitt sete i rikets hovedstad |
| 11 | domstolloven | lov/1915/08/13/5/nor/§25/ledd-3 | vedtakskompetanse | domstoladministrasjonen → null | Rettslokalene må være godkjent av domstoladministrasjonen. |
| 12 | domstolloven | lov/1915/08/13/5/nor/§16/ledd-1 | bestar_av | lagdømmer → lagsogn | lagdømmernes inddeling i retskredser (lagsogn) |
| 13 | domstolloven | forskrift/2012/02/03/119/nor/punkt-2 | delegerer_til | Kongen → Justisdepartementet | Kongens myndighet til å fastsette forskrifter etter lov 13. august 1915 nr. 5 om domstolene § 197a delegeres til Justis- og beredskapsdepartementet. |
| 14 | domstolloven | lov/1915/08/13/5/nor/§49/ledd-1 | forskriftskompetanse | Kongen → null | kan Kongen gi de nødvendige bestemmelser |
| 15 | domstolloven | lov/1915/08/13/5/nor/§5/ledd-1 | har_medlemmer | Høyesteretts ankeutvalg → dommere | settes Høyesterett med tre dommere |
| 16 | domstolloven | lov/1915/08/13/5/nor/§33/ledd-3 | instruksjon | Kongen → domstoladministrasjonen | Kongen i statsråd kan treffe vedtak om domstoladministrasjonens virksomhet og administrasjonen av domstolene. |
| 17 | domstolloven | lov/1915/08/13/5/nor/§58/ledd-1 | oppnevner | statsforvalteren → Forliksrådene | Finner han valget lovlig, utferdiger han oppnevnelse for de valgte |
| 18 | domstolloven | lov/1915/08/13/5/nor/§76/ledd-3 | klageinstans_for | Domstolene → kommune | kan påklage avgjørelsen til den domstolen som fortegnelsen gjelder |
| 19 | helse-og-omsorgstjenesteloven | lov/2011/06/24/30/nor/§12-5/ledd-1 | administrativt_underordnet | Helsedirektoratet → Departementet | Departementet kan bestemme på hvilke områder direktoratet skal utarbeide slike retningslinjer og veiledere. |
| 20 | domstolloven | forskrift/2021/01/22/163/nor/§1/ledd-1 | har_sete_i | tingrettene → rettssteder | med ett eller flere rettssteder |

## Forkastede mønstre

Ingen.
