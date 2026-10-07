# Måling: mønsterkonvertering mot strukturfasiten

Generert av `MonsterStrukturkonvertererMalingTests` (`src/RegelIde.Data.Tests/Strukturfasit/`) — **ikke rediger for hånd**.
Kjør `dotnet test src/RegelIde.Data.Tests --filter "FullyQualifiedName~Strukturfasit"` for å regenerere. Sak: #307, designgrunnlag: `docs/33` §5.

**Forbehold:** fasiten er KI-annotert og ikke menneskelig verifisert (`docs/33` §2). Tallene er bare så gode som fasiten,
og tersklene i testen er regresjonsvern, ikke kvalitetskrav — de låses først etter gjennomgangen i #309.

**Treffregel:** samme eId + kategori + type, og for hvert endepunkt fasiten har (fra/til): samme tekstform uten skille på store/små
bokstaver, der «tekstform» er aktørens tekstform ∪ varianter (minst én felles skrivemåte). Én-til-én. «Uten endepunktkrav» =
bare eId + kategori + type — forskjellen mellom de to viser hvor mye av feilen som er feil/manglende aktør, ikke feil gjenkjenning.

## Totalt

1865 fasitutsagn, 901 predikerte, 845 treff → presisjon **93,8 %**, gjenfinning **45,3 %**
(uten endepunktkrav: presisjon 95,9 %, gjenfinning 46,3 %).

## Per kategori (kanttype, `docs/33` §4.3)

Bokstaven følger `STD`-tabellen i `designtest.py`. `annet:*`-typer står i egen rad; `bistar`/`samarbeider_med` er bevisst senere lag (§4.4).

| Kategori | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| R | 223 | 36 | 28 | 77,8 % | 12,6 % | 94,4 % | 15,2 % |
| K | 427 | 288 | 255 | 88,5 % | 59,7 % | 88,5 % | 59,7 % |
| M | 48 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| O | 578 | 509 | 496 | 97,4 % | 85,8 % | 99,6 % | 87,7 % |
| A | 120 | 62 | 62 | 100,0 % | 51,7 % | 100,0 % | 51,7 % |
| G | 43 | 6 | 4 | 66,7 % | 9,3 % | 100,0 % | 14,0 % |
| I | 0 | 0 | 0 | – | – | – | – |
| T | 21 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| annet | 366 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| senere lag | 39 | 0 | 0 | – | 0,0 % | – | 0,0 % |

## Per kilde

| Kilde | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| domstolloven | 832 | 541 | 518 | 95,7 % | 62,3 % | 96,7 % | 62,9 % |
| energiloven | 254 | 114 | 108 | 94,7 % | 42,5 % | 94,7 % | 42,5 % |
| helse-og-omsorgstjenesteloven | 192 | 49 | 45 | 91,8 % | 23,4 % | 95,9 % | 24,5 % |
| sameloven | 285 | 130 | 114 | 87,7 % | 40,0 % | 96,2 % | 43,9 % |
| spesialisthelsetjenesteloven | 302 | 67 | 60 | 89,6 % | 19,9 % | 91,0 % | 20,2 % |

## Per type mønsterlaget produserer

| Kategori / type | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| K kompetanse / forskriftskompetanse | 205 | 198 | 182 | 91,9 % | 88,8 % | 91,9 % | 88,8 % |
| K kompetanse / vedtakskompetanse | 158 | 85 | 68 | 80,0 % | 43,0 % | 80,0 % | 43,0 % |
| R relasjon / klageinstans_for | 17 | 9 | 5 | 55,6 % | 29,4 % | 100,0 % | 52,9 % |
| R relasjon / administrativt_underordnet | 5 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| G organsammensetning / har_medlemmer | 24 | 6 | 4 | 66,7 % | 16,7 % | 100,0 % | 25,0 % |
| R relasjon / oppnevner | 23 | 10 | 7 | 70,0 % | 30,4 % | 90,0 % | 39,1 % |
| R relasjon / instruksjon | 21 | 4 | 4 | 100,0 % | 19,0 % | 100,0 % | 19,0 % |
| R relasjon / delegerer_til | 36 | 13 | 12 | 92,3 % | 33,3 % | 92,3 % | 33,3 % |
| K kompetanse / tilsynskompetanse | 16 | 5 | 5 | 100,0 % | 31,3 % | 100,0 % | 31,3 % |
| A ansvarsomrade / har_sete_i | 65 | 62 | 62 | 100,0 % | 95,4 % | 100,0 % | 95,4 % |
| O sammensetning_omrade / del_av | 85 | 49 | 41 | 83,7 % | 48,2 % | 100,0 % | 57,6 % |
| O sammensetning_omrade / bestar_av | 493 | 460 | 455 | 98,9 % | 92,3 % | 99,6 % | 92,9 % |

### Per kilde, K forskriftskompetanse (terskel ≥ 0,9 presisjon)

| Kilde | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| domstolloven | 40 | 37 | 32 | 86,5 % | 80,0 % | 86,5 % | 80,0 % |
| energiloven | 64 | 61 | 58 | 95,1 % | 90,6 % | 95,1 % | 90,6 % |
| helse-og-omsorgstjenesteloven | 38 | 37 | 36 | 97,3 % | 94,7 % | 97,3 % | 94,7 % |
| sameloven | 16 | 16 | 12 | 75,0 % | 75,0 % | 75,0 % | 75,0 % |
| spesialisthelsetjenesteloven | 47 | 47 | 44 | 93,6 % | 93,6 % | 93,6 % | 93,6 % |

## Per mønster

«Gjenkjent» = andelen av mønsterets utsagn der fasiten har et utsagn med samme eId + kategori + type (uansett aktør, ikke én-til-én).
Stor avstand mellom presisjon og gjenkjent betyr at mønsteret finner riktig utsagn, men feil eller manglende aktør.

| Mønster | Type | Predikert | Treff | Presisjon | Gjenkjent | Korpusgrunnlag (`docs/33` §1) |
|---|---|---:|---:|---:|---:|---|
| `forskrift-gi` | forskriftskompetanse | 105 | 97 | 92,4 % | 95,2 % | docs/33 §1: «kan gi forskrift om» 5651 treff, 100 % presisjon. |
| `forskrift-i-ved` | forskriftskompetanse | 64 | 61 | 95,3 % | 95,3 % | docs/33 §1: variant av «kan gi forskrift om» (5651 treff, 100 %); formen er ikke målt separat. |
| `forskrift-naermere-regler` | forskriftskompetanse | 22 | 19 | 86,4 % | 86,4 % | Ikke målt i docs/33 §1. Med fordi lovteksten bruker «gi (nærmere) regler» om forskrift uten å si ordet; presisjonen måles mot fasiten. |
| `forskrift-gitt-av` | forskriftskompetanse | 7 | 5 | 71,4 % | 71,4 % | Ikke målt i docs/33 §1 (passiv form av forskriftskompetanse). Krever at aktøren står i setningen. |
| `vedtak-treffe` | vedtakskompetanse | 24 | 20 | 83,3 % | 83,3 % | docs/33 §1: «treffer vedtak / avgjøres av» 1465 treff, 73 % presisjon. |
| `vedtak-avgjores-av` | vedtakskompetanse | 21 | 12 | 57,1 % | 57,1 % | docs/33 §1: «treffer vedtak / avgjøres av» 1465 treff, 73 %. Krever at aktøren står i setningen — «av» uten aktør er ikke et kompetanseutsagn. |
| `vedtak-godkjennes-av` | vedtakskompetanse | 7 | 5 | 71,4 % | 85,7 % | Ikke målt i docs/33 §1. Godkjenning er et enkeltvedtak; mønsteret krever at godkjenneren står i setningen. |
| `vedtak-forvaltningsverb` | vedtakskompetanse | 26 | 26 | 100,0 % | 100,0 % | Ikke målt i docs/33 §1. Tatt med etter fasitens falske negative: energiloven uttrykker vedtakskompetanse nesten bare slik, ikke som «treffe vedtak». Verbet «pål … |
| `vedtak-avgjor` | vedtakskompetanse | 7 | 5 | 71,4 % | 71,4 % | Ikke målt separat i docs/33 §1 (del av «avgjøres av»-familien). |
| `klage-paklages-til` | klageinstans_for | 4 | 1 | 25,0 % | 100,0 % | docs/33 §1: «klageinstans for / påklages til» 1718 treff, 67 % presisjon. Førsteinstansen er ofte implisitt. |
| `klage-er-klageinstans` | klageinstans_for | 3 | 2 | 66,7 % | 100,0 % | docs/33 §1: «klageinstans for / påklages til» 1718 treff, 67 %. |
| `klage-ikke-paklages` | klageinstans_for | 2 | 2 | 100,0 % | 100,0 % | docs/33 §1: del av «klageinstans for / påklages til» (1718 treff, 67 %); negativ form ikke målt separat. |
| `administrativt-underordnet` | administrativt_underordnet | 0 | 0 | – | – | docs/33 §1: 775 treff på «underordnet», 13 % — signalet er KUN i frasen «administrativt underordnet», som er det eneste mønsteret tar. |
| `har-medlemmer` | har_medlemmer | 6 | 4 | 66,7 % | 100,0 % | docs/33 §1: «består av N medlemmer» 147 treff, 100 % presisjon. |
| `oppnevnt-av` | oppnevner | 5 | 2 | 40,0 % | 80,0 % | docs/33 §1: «oppnevner / utnevner» 1422 treff, 33 % presisjon. |
| `oppnevner-aktivt` | oppnevner | 5 | 5 | 100,0 % | 100,0 % | docs/33 §1: «oppnevner / utnevner» 1422 treff, 33 %. |
| `instruksjon-kan-ikke-instrueres` | instruksjon | 4 | 4 | 100,0 % | 100,0 % | docs/33 §1: «instruere» 908 treff, 27 % — og nesten bare NEGATIV («kan ikke instruere») = uavhengighet. Mønsteret tar bare den negative formen. |
| `instruksjon-kan-ikke-instruere` | instruksjon | 0 | 0 | – | – | docs/33 §1: «instruere» 908 treff, 27 %, nesten bare negativ. |
| `delegerer-kan-delegere` | delegerer_til | 9 | 9 | 100,0 % | 100,0 % | docs/33 §1: «delegerer til» 2875 treff, 47 % presisjon — nesten alltid avgrenset til paragraf (avgrensningen tolkes ikke her). |
| `delegerer-delegeres-til` | delegerer_til | 4 | 3 | 75,0 % | 75,0 % | docs/33 §1: «delegerer til» 2875 treff, 47 %. |
| `tilsyn-forer-tilsyn` | tilsynskompetanse | 5 | 5 | 100,0 % | 100,0 % | docs/33 §1: «fører tilsyn med at …» 2246 treff, ~33 % presisjon, og «tilsyn med aktør» ≈ 0 % som kompetanse. Første versjon tok alle objekter og fikk 5 av 9 mot … |
| `har-sete-i` | har_sete_i | 1 | 1 | 100,0 % | 100,0 % | Ikke målt i docs/33 §1. Fast lovformulering for sete; Y tas slik den står («rikets hovedstad»), uten oppslag. |
| `inndeling-kommune-i-fylke` | del_av | 47 | 39 | 83,0 % | 100,0 % | Ikke målt i docs/33 §1. Fylkestilhørigheten står eksplisitt i setningen. |
| `inndeling-har-rettskretsen` | del_av | 2 | 2 | 100,0 % | 100,0 % | Ikke målt i docs/33 §1. |
| `inndeling-rettskrets` | bestar_av | 357 | 357 | 100,0 % | 100,0 % | docs/33 §1 nevner strukturerte kommunelister i inndelingsforskrifter som høypresisjonskilde (ikke tallfestet). |
| `inndeling-rettssted` | har_sete_i | 61 | 61 | 100,0 % | 100,0 % | Samme kilde som inndeling-rettskrets. |
| `inndeling-kommuneliste` | bestar_av | 56 | 56 | 100,0 % | 100,0 % | Strukturerte kommunelister (docs/33 §1, ikke tallfestet). Retnings- og komplementdefinisjoner kan ikke avgjøres uten et områderegister (#312). |
| `inndeling-utgjor` | bestar_av | 15 | 10 | 66,7 % | 100,0 % | Strukturerte lister i inndelingsforskrifter (docs/33 §1, ikke tallfestet). |
| `inndeling-sogner` | bestar_av | 28 | 28 | 100,0 % | 100,0 % | Strukturerte lister i inndelingsforskrifter (docs/33 §1, ikke tallfestet). |
| `inndeling-bestar-av-liste` | bestar_av | 4 | 4 | 100,0 % | 100,0 % | Ikke målt i docs/33 §1. Generell listeform; kravet om egennavn holder organsammensetning ute. |

## Falske positive (56)

Gruppert på mønster og om eId+kategori+type fantes i fasiten (= feil/manglende aktør) eller ikke (= feil gjenkjenning).

| Mønster | Årsak | Antall |
|---|---|---:|
| `monster:vedtak-avgjores-av` | ikke i fasiten | 9 |
| `monster:inndeling-kommune-i-fylke` | feil/manglende aktør | 8 |
| `monster:forskrift-gi` | ikke i fasiten | 5 |
| `monster:inndeling-utgjor` | feil/manglende aktør | 5 |
| `monster:vedtak-treffe` | ikke i fasiten | 4 |
| `monster:forskrift-gi` | feil/manglende aktør | 3 |
| `monster:forskrift-i-ved` | ikke i fasiten | 3 |
| `monster:forskrift-naermere-regler` | ikke i fasiten | 3 |
| `monster:klage-paklages-til` | feil/manglende aktør | 3 |
| `monster:forskrift-gitt-av` | ikke i fasiten | 2 |
| `monster:har-medlemmer` | feil/manglende aktør | 2 |
| `monster:oppnevnt-av` | feil/manglende aktør | 2 |
| `monster:vedtak-avgjor` | ikke i fasiten | 2 |
| `monster:delegerer-delegeres-til` | ikke i fasiten | 1 |
| `monster:klage-er-klageinstans` | feil/manglende aktør | 1 |
| `monster:oppnevnt-av` | ikke i fasiten | 1 |
| `monster:vedtak-godkjennes-av` | feil/manglende aktør | 1 |
| `monster:vedtak-godkjennes-av` | ikke i fasiten | 1 |

De 20 vanligste (én fra hver gruppe i tur, vanligste gruppe først):

| # | Kilde | eId | Mønster | fra → til | Sitat |
|---|---|---|---|---|---|
| 1 | domstolloven | lov/1915/08/13/5/nor/§5/ledd-1 | `monster:vedtak-avgjores-av` | Høyesteretts ankeutvalg → null | I saker som etter lov skal avgjøres av Høyesteretts ankeutvalg, settes Høyesterett med tre dommere. |
| 2 | sameloven | lov/1987/06/12/56/nor/§2-4/ledd-1/punkt-6 | `monster:inndeling-kommune-i-fylke` | Surnadal → Møre og Romsdal fylke | Surnadal |
| 3 | energiloven | lov/1990/06/29/50/nor/§2-4/ledd-1 | `monster:forskrift-gi` | Kongen → null | Kongen kan gi forskrift om at nærmere bestemte vedtak etter § 3-1 skal fattes av Kongen i statsråd. |
| 4 | domstolloven | forskrift/2021/01/22/163/nor/§15/ledd-1 | `monster:inndeling-utgjor` | Borgarting lagdømme → Oslo | Oslo |
| 5 | domstolloven | lov/1915/08/13/5/nor/§33/ledd-3 | `monster:vedtak-treffe` | Kongen i statsråd → null | Kongen i statsråd kan treffe vedtak om domstoladministrasjonens virksomhet og administrasjonen av domstolene. |
| 6 | domstolloven | lov/1915/08/13/5/nor/§122/ledd-2 | `monster:forskrift-gi` | Kongen → null | Kongen kan gi forskrift om at opplysninger som nevnt skal gis ved oppslag ved rettens kontor. |
| 7 | domstolloven | lov/1915/08/13/5/nor/§86/ledd-1 | `monster:forskrift-i-ved` | domstoladministrasjonen → null | Domstoladministrasjonen kan ved forskrift dele lagsogn og domssogn i flere trekningskretser. |
| 8 | domstolloven | lov/1915/08/13/5/nor/§33c/ledd-2 | `monster:forskrift-naermere-regler` | domstoladministrasjonen → null | Domstoladministrasjonen gir nærmere bestemmelser om organiseringen av disse dommernes tjenester. |
| 9 | helse-og-omsorgstjenesteloven | lov/2011/06/24/30/nor/§9-11/ledd-1 | `monster:klage-paklages-til` | Statsforvalteren → null | Beslutning etter § 9-5 tredje ledd bokstav a kan påklages av brukeren eller pasienten, verge og pårørende til statsforvalteren. |
| 10 | domstolloven | lov/1915/08/13/5/nor/§105a/ledd-1 | `monster:forskrift-gitt-av` | Kongen → null | Godtgjørelsen til jordskiftemeddommer og meddommer fastsettes av rettens leder etter forskrifter gitt av Kongen. |
| 11 | domstolloven | lov/1915/08/13/5/nor/§27/ledd-2 | `monster:har-medlemmer` | Forliksrådet → medlemmer | Forliksrådet skal ha tre medlemmer og like mange varamedlemmer. |
| 12 | domstolloven | lov/1915/08/13/5/nor/§43/ledd-2 | `monster:oppnevnt-av` | Kongen → særskilt organ | En granskingskommisjon, et kontrollutvalg eller et annet særskilt organ som er oppnevnt av Kongen, Stortinget eller et departement eller en statsforvalter for å … |
| 13 | domstolloven | lov/1915/08/13/5/nor/§6/ledd-2 | `monster:vedtak-avgjor` | domstollederen → null | Domstollederen avgjør da om retten skal settes med 11 eller med alle Høyesteretts dommere. |
| 14 | sameloven | forskrift/2004/12/10/1607/nor/ledd-3 | `monster:delegerer-delegeres-til` | Kongen → Kommunal- og regionaldepartementet | Departementet foreslår at Kongens myndighet etter samelovens § 2-11 delegeres delvis til Kommunal- og regionaldepartementet. |
| 15 | sameloven | lov/1987/06/12/56/nor/§3-11/ledd-1 | `monster:klage-er-klageinstans` | Statsforvalteren → null | Statsforvalteren er klageinstans når klagen angår kommunale eller fylkeskommunale organ. |
| 16 | domstolloven | lov/1915/08/13/5/nor/§101/ledd-1 | `monster:oppnevnt-av` | retten → Rettsvitner | Rettsvitner oppnevnes av retten eller av den tjenestemann som skal styre forretningen. |
| 17 | domstolloven | lov/1915/08/13/5/nor/§63/ledd-2 | `monster:vedtak-godkjennes-av` | tingretten → null | Hjelpestevnevitner for hovedstevnevitnet må godkjennes av tingretten. |
| 18 | energiloven | kap-I/ledd-8 | `monster:vedtak-godkjennes-av` | Departementet → null | Norges vassdrags- og energidirektorat delegeres også myndighet til å behandle søknader om endringer i konsesjoner etter energiloven gitt av Kongen i statsråd ve … |
| 19 | domstolloven | lov/1915/08/13/5/nor/§5/ledd-4 | `monster:vedtak-avgjores-av` | Høyesterett → null | I saker etter første og annet ledd som er av særlig viktighet, kan det bestemmes at saken, eller rettsspørsmål i den, skal avgjøres av Høyesterett i storkammer, … |
| 20 | sameloven | lov/1987/06/12/56/nor/§2-4/ledd-1/punkt-6 | `monster:inndeling-kommune-i-fylke` | Sunndal → Møre og Romsdal fylke | Sunndal |

## Falske negative (1020)

Gruppert på kategori/type. Typer mønsterlaget ikke har mønster for, er forventet her — de er KI-lagets (#308) nevner.

| Kategori / type | Har mønster | Antall |
|---|---|---:|
| K kompetanse / vedtakskompetanse | ja | 90 |
| A ansvarsomrade / har_ansvarsomrade | nei | 52 |
| O sammensetning_omrade / del_av | ja | 44 |
| O sammensetning_omrade / bestar_av | ja | 38 |
| R relasjon / rapporterer_til | nei | 31 |
| M medlemskap / medlem_av | nei | 30 |
| R relasjon / delegerer_til | ja | 24 |
| senere lag relasjon / samarbeider_med | nei | 24 |
| annet sammensetning_omrade / annet:gruppert_under | nei | 24 |
| K kompetanse / forskriftskompetanse | ja | 23 |
| K kompetanse / utpekingskompetanse | nei | 21 |
| T konstituerende / skal_finnes | nei | 21 |
| G organsammensetning / har_medlemmer | ja | 20 |
| M medlemskap / inngar_i | nei | 18 |
| R relasjon / instruksjon | ja | 17 |
| R relasjon / velger | nei | 17 |
| R relasjon / oppnevner | ja | 16 |
| senere lag relasjon / bistar | nei | 15 |
| R konstituerende / oppretter | nei | 14 |
| K kompetanse / oppnevningskompetanse | nei | 13 |
| R relasjon / eies_av | nei | 13 |
| annet relasjon / annet:informasjonsdeling | nei | 12 |
| R relasjon / klageinstans_for | ja | 12 |
| R relasjon / ledes_av | nei | 12 |
| annet annet:finansieringsansvar / annet:dekker_utgifter_for | nei | 11 |
| K kompetanse / tilsynskompetanse | ja | 11 |
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
| … 176 typer til (alle `annet:*` eller ≤ 5 utsagn) | | 286 |

De 20 vanligste blant typene med mønster (én fra hver type i tur, vanligste type først):

| # | Kilde | eId | Type | fra → til (fasit) | Sitat |
|---|---|---|---|---|---|
| 1 | domstolloven | lov/1915/08/13/5/nor/§13/ledd-1 | vedtakskompetanse | førstelagmann → null | Avgjørelser og andre forføyninger, som ikke gjelder de enkelte rettssaker, treffer førstelagmannen alene |
| 2 | domstolloven | forskrift/2021/01/22/163/nor/§4/ledd-1 | del_av | Nordmøre og Romsdal tingrett → Møre og Romsdal fylke | Møre og Romsdal fylke har rettskretsene |
| 3 | domstolloven | lov/1915/08/13/5/nor/§16/ledd-1 | bestar_av | Rikets → lagdømmer | Rikets inddeling i lagdømmer |
| 4 | energiloven | lov/1990/06/29/50/nor/§4-11/ledd-1 | delegerer_til | Departementet → reguleringsmyndighet | Departementet kan gi forskrift om at reguleringsmyndigheten kan gi forskrift om metoder for beregning av nettselskapenes tillatte og faktiske inntekter |
| 5 | domstolloven | lov/1915/08/13/5/nor/§48/ledd-4 | forskriftskompetanse | Kongen → null | ved regler, som Kongen gir |
| 6 | domstolloven | lov/1915/08/13/5/nor/§3/ledd-1 | har_medlemmer | Høyesterett → dommere | Retten skal ha en justitiarius og nitten andre dommere. |
| 7 | domstolloven | lov/1915/08/13/5/nor/§33/ledd-2 | instruksjon | Stortinget → domstoladministrasjonen | Gjennom Stortingets behandling av budsjettproposisjonen gis årlige retningslinjer for domstoladministrasjonens virksomhet |
| 8 | domstolloven | lov/1915/08/13/5/nor/§58/ledd-1 | oppnevner | statsforvalteren → Forliksrådene | Finner han valget lovlig, utferdiger han oppnevnelse for de valgte |
| 9 | domstolloven | lov/1915/08/13/5/nor/§33b/ledd-1 | klageinstans_for | Kongen → styre | for styrets vedtak Kongen i statsråd |
| 10 | domstolloven | lov/1915/08/13/5/nor/§77/ledd-1 | tilsynskompetanse | domstoladministrasjonen → null | Domstoladministrasjonen hvert halvår skal kontrollere om medlemmene av utvalgene av meddommere og skjønnsmedlemmer er innført i fortegnelsen i strid med § 72 |
| 11 | energiloven | lov/1990/06/29/50/nor/§9-1/ledd-5 | administrativt_underordnet | kraftforsyningen → Kraftforsyningens beredskapsorganisasjon (KBO) | Beredskapsmyndigheten kan under beredskap og i krig underlegge kraftforsyningen KBO. |
| 12 | domstolloven | forskrift/2021/01/22/163/nor/§1/ledd-1 | har_sete_i | tingrettene → rettssteder | med ett eller flere rettssteder |
| 13 | domstolloven | lov/1915/08/13/5/nor/§66a/ledd-1 | vedtakskompetanse | domstolens leder → null | kan domstollederen bestemme at det skal velges flere medlemmer til utvalgene |
| 14 | domstolloven | forskrift/2021/01/22/163/nor/§4/ledd-1 | del_av | Sunnmøre tingrett → Møre og Romsdal fylke | Møre og Romsdal fylke har rettskretsene |
| 15 | domstolloven | lov/1915/08/13/5/nor/§16/ledd-1 | bestar_av | lagdømmer → lagsogn | lagdømmernes inddeling i retskredser (lagsogn) |
| 16 | energiloven | lov/1990/06/29/50/nor/§9-4/ledd-1 | delegerer_til | Beredskapsmyndigheten → enheter i KBO | Beredskapsmyndigheten kan delegere myndighet til å treffe vedtak i forbindelse med beredskap til KBO eller enheter som inngår i KBO. |
| 17 | domstolloven | lov/1915/08/13/5/nor/§60/ledd-1 | forskriftskompetanse | Kongen → null | Kongen fastsetter hvordan forsikringen skal lyde. |
| 18 | domstolloven | lov/1915/08/13/5/nor/§5/ledd-1 | har_medlemmer | Høyesteretts ankeutvalg → dommere | settes Høyesterett med tre dommere |
| 19 | domstolloven | lov/1915/08/13/5/nor/§33/ledd-3 | instruksjon | Kongen → domstoladministrasjonen | Kongen i statsråd kan treffe vedtak om domstoladministrasjonens virksomhet og administrasjonen av domstolene. |
| 20 | domstolloven | lov/1915/08/13/5/nor/§43/ledd-2 | oppnevner | null → En granskingskommisjon, et kontrollutvalg eller et annet særskilt organ | som er oppnevnt av Kongen, Stortinget eller et departement eller en statsforvalter |

## Forkastede mønstre

Ingen.
