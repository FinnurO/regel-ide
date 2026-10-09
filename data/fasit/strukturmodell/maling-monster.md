# Måling: mønsterkonvertering mot strukturfasiten

Generert av `MonsterStrukturkonvertererMalingTests` (`src/RegelIde.Data.Tests/Strukturfasit/`) — **ikke rediger for hånd**.
Kjør `dotnet test src/RegelIde.Data.Tests --filter "FullyQualifiedName~Strukturfasit"` for å regenerere. Sak: #307, designgrunnlag: `docs/33` §5.

**Forbehold:** fasiten er KI-annotert og ikke menneskelig verifisert (`docs/33` §2). Tallene er bare så gode som fasiten,
og tersklene i testen er regresjonsvern, ikke kvalitetskrav — de låses først etter gjennomgangen i #309.

**Treffregel:** samme eId + kategori + type, og for hvert endepunkt fasiten har (fra/til): samme tekstform uten skille på store/små
bokstaver, der «tekstform» er aktørens tekstform ∪ varianter (minst én felles skrivemåte). Én-til-én. «Uten endepunktkrav» =
bare eId + kategori + type — forskjellen mellom de to viser hvor mye av feilen som er feil/manglende aktør, ikke feil gjenkjenning.

## Totalt

1798 fasitutsagn, 933 predikerte, 865 treff → presisjon **92,7 %**, gjenfinning **48,1 %**
(uten endepunktkrav: presisjon 95,0 %, gjenfinning 49,3 %).

## Per kategori (kanttype, `docs/33` §4.3)

Bokstaven følger `STD`-tabellen i `designtest.py`. `annet:*`-typer står i egen rad. P = plikt overfor motpart (#353; samarbeid og bistand var «senere lag» før).

| Kategori | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| R | 124 | 4 | 3 | 75,0 % | 2,4 % | 75,0 % | 2,4 % |
| K | 577 | 324 | 285 | 88,0 % | 49,4 % | 89,5 % | 50,3 % |
| P | 79 | 30 | 17 | 56,7 % | 21,5 % | 66,7 % | 25,3 % |
| M | 48 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| O | 177 | 122 | 109 | 89,3 % | 61,6 % | 98,4 % | 67,8 % |
| A | 449 | 419 | 419 | 100,0 % | 93,3 % | 100,0 % | 93,3 % |
| G | 43 | 6 | 4 | 66,7 % | 9,3 % | 100,0 % | 14,0 % |
| I | 0 | 0 | 0 | – | – | – | – |
| T | 21 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| annet | 280 | 28 | 28 | 100,0 % | 10,0 % | 100,0 % | 10,0 % |

## Per kilde

| Kilde | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| domstolloven | 764 | 540 | 517 | 95,7 % | 67,7 % | 96,9 % | 68,5 % |
| energiloven | 254 | 117 | 110 | 94,0 % | 43,3 % | 94,0 % | 43,3 % |
| helse-og-omsorgstjenesteloven | 192 | 58 | 51 | 87,9 % | 26,6 % | 91,4 % | 27,6 % |
| sameloven | 285 | 132 | 117 | 88,6 % | 41,1 % | 97,0 % | 44,9 % |
| spesialisthelsetjenesteloven | 303 | 86 | 70 | 81,4 % | 23,1 % | 83,7 % | 23,8 % |

## Per type mønsterlaget produserer

| Kategori / type | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| K kompetanse / normgivningskompetanse | 210 | 198 | 184 | 92,9 % | 87,6 % | 92,9 % | 87,6 % |
| K kompetanse / vedtakskompetanse | 153 | 78 | 63 | 80,8 % | 41,2 % | 80,8 % | 41,2 % |
| K kompetanse / godkjenningskompetanse | 8 | 7 | 6 | 85,7 % | 75,0 % | 85,7 % | 75,0 % |
| K kompetanse / klagekompetanse | 22 | 9 | 7 | 77,8 % | 31,8 % | 100,0 % | 40,9 % |
| R relasjon / administrativt_underordnet | 5 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| G organsammensetning / har_medlemmer | 21 | 6 | 4 | 66,7 % | 19,0 % | 100,0 % | 28,6 % |
| K kompetanse / oppnevningskompetanse | 79 | 10 | 7 | 70,0 % | 8,9 % | 100,0 % | 12,7 % |
| K kompetanse / instruksjonskompetanse | 27 | 4 | 4 | 100,0 % | 14,8 % | 100,0 % | 14,8 % |
| K kompetanse / delegeringskompetanse | 19 | 9 | 8 | 88,9 % | 42,1 % | 88,9 % | 42,1 % |
| R relasjon / har_delegert_til | 20 | 4 | 3 | 75,0 % | 15,0 % | 75,0 % | 15,0 % |
| K kompetanse / tilsynskompetanse | 26 | 5 | 5 | 100,0 % | 19,2 % | 100,0 % | 19,2 % |
| K kompetanse / beslutningskompetanse | 1 | 1 | 0 | 0,0 % | 0,0 % | 0,0 % | 0,0 % |
| K kompetanse / samordningskompetanse | 1 | 3 | 1 | 33,3 % | 100,0 % | 33,3 % | 100,0 % |
| P plikt / samarbeidsplikt | 21 | 9 | 7 | 77,8 % | 33,3 % | 77,8 % | 33,3 % |
| P plikt / avtaleplikt | 3 | 3 | 2 | 66,7 % | 66,7 % | 100,0 % | 100,0 % |
| P plikt / betalingsplikt | 19 | 18 | 8 | 44,4 % | 42,1 % | 55,6 % | 52,6 % |
| P plikt / informasjonsplikt | 13 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| P plikt / konsultasjonsplikt | 8 | 0 | 0 | – | 0,0 % | – | 0,0 % |
| A ansvarsomrade / har_sete_i | 65 | 62 | 62 | 100,0 % | 95,4 % | 100,0 % | 95,4 % |
| O sammensetning_omrade / del_av | 69 | 47 | 39 | 83,0 % | 56,5 % | 100,0 % | 68,1 % |
| A ansvarsomrade / har_ansvarsomrade | 381 | 357 | 357 | 100,0 % | 93,7 % | 100,0 % | 93,7 % |
| O sammensetning_omrade / bestar_av | 108 | 75 | 70 | 93,3 % | 64,8 % | 97,3 % | 67,6 % |
| annet ansvarsomrade / annet:sogner_til | 28 | 28 | 28 | 100,0 % | 100,0 % | 100,0 % | 100,0 % |

### Per kilde, K normgivningskompetanse — fasitens forskriftskompetanse før #341 (terskel ≥ 0,9 presisjon)

| Kilde | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| domstolloven | 40 | 37 | 32 | 86,5 % | 80,0 % | 86,5 % | 80,0 % |
| energiloven | 65 | 61 | 58 | 95,1 % | 89,2 % | 95,1 % | 89,2 % |
| helse-og-omsorgstjenesteloven | 39 | 37 | 36 | 97,3 % | 92,3 % | 97,3 % | 92,3 % |
| sameloven | 19 | 16 | 14 | 87,5 % | 73,7 % | 87,5 % | 73,7 % |
| spesialisthelsetjenesteloven | 47 | 47 | 44 | 93,6 % | 93,6 % | 93,6 % | 93,6 % |

## Per mønster

«Gjenkjent» = andelen av mønsterets utsagn der fasiten har et utsagn med samme eId + kategori + type (uansett aktør, ikke én-til-én).
Stor avstand mellom presisjon og gjenkjent betyr at mønsteret finner riktig utsagn, men feil eller manglende aktør.

| Mønster | Type | Predikert | Treff | Presisjon | Gjenkjent | Korpusgrunnlag (`docs/33` §1) |
|---|---|---:|---:|---:|---:|---|
| `forskrift-gi` | normgivningskompetanse | 105 | 97 | 92,4 % | 95,2 % | docs/33 §1: «kan gi forskrift om» 5651 treff, 100 % presisjon. |
| `forskrift-i-ved` | normgivningskompetanse | 64 | 61 | 95,3 % | 95,3 % | docs/33 §1: variant av «kan gi forskrift om» (5651 treff, 100 %); formen er ikke målt separat. |
| `forskrift-naermere-regler` | normgivningskompetanse | 22 | 21 | 95,5 % | 95,5 % | Ikke målt i docs/33 §1. Med fordi lovteksten bruker «gi (nærmere) regler» om forskrift uten å si ordet; presisjonen måles mot fasiten. |
| `forskrift-gitt-av` | normgivningskompetanse | 7 | 5 | 71,4 % | 71,4 % | Ikke målt i docs/33 §1 (passiv form av forskriftskompetanse). Krever at aktøren står i setningen. |
| `vedtak-treffe` | vedtakskompetanse | 24 | 20 | 83,3 % | 83,3 % | docs/33 §1: «treffer vedtak / avgjøres av» 1465 treff, 73 % presisjon. |
| `vedtak-avgjores-av` | vedtakskompetanse | 21 | 12 | 57,1 % | 57,1 % | docs/33 §1: «treffer vedtak / avgjøres av» 1465 treff, 73 %. Krever at aktøren står i setningen — «av» uten aktør er ikke et kompetanseutsagn. |
| `vedtak-godkjennes-av` | godkjenningskompetanse | 7 | 6 | 85,7 % | 85,7 % | Ikke målt i docs/33 §1. [ENDRET, #352] Gir godkjenningskompetanse (familien styring), ikke vedtakskompetanse (Johanns beslutning 3); mønsteret krever at godkjen … |
| `vedtak-forvaltningsverb` | vedtakskompetanse | 26 | 26 | 100,0 % | 100,0 % | Ikke målt i docs/33 §1. Tatt med etter fasitens falske negative: energiloven uttrykker vedtakskompetanse nesten bare slik, ikke som «treffe vedtak». Verbet «pål … |
| `vedtak-avgjor` | vedtakskompetanse | 7 | 5 | 71,4 % | 71,4 % | Ikke målt separat i docs/33 §1 (del av «avgjøres av»-familien). |
| `klage-paklages-til` | klagekompetanse | 4 | 3 | 75,0 % | 100,0 % | docs/33 §1: «klageinstans for / påklages til» 1718 treff, 67 % presisjon. Førsteinstansen er ofte implisitt. |
| `klage-er-klageinstans` | klagekompetanse | 3 | 2 | 66,7 % | 100,0 % | docs/33 §1: «klageinstans for / påklages til» 1718 treff, 67 %. |
| `klage-ikke-paklages` | klagekompetanse | 2 | 2 | 100,0 % | 100,0 % | docs/33 §1: del av «klageinstans for / påklages til» (1718 treff, 67 %); negativ form ikke målt separat. |
| `administrativt-underordnet` | administrativt_underordnet | 0 | 0 | – | – | docs/33 §1: 775 treff på «underordnet», 13 % — signalet er KUN i frasen «administrativt underordnet», som er det eneste mønsteret tar. |
| `har-medlemmer` | har_medlemmer | 6 | 4 | 66,7 % | 100,0 % | docs/33 §1: «består av N medlemmer» 147 treff, 100 % presisjon. |
| `oppnevnt-av` | oppnevningskompetanse | 5 | 2 | 40,0 % | 100,0 % | docs/33 §1: «oppnevner / utnevner» 1422 treff, 33 % presisjon. |
| `oppnevner-aktivt` | oppnevningskompetanse | 5 | 5 | 100,0 % | 100,0 % | docs/33 §1: «oppnevner / utnevner» 1422 treff, 33 %. |
| `instruksjon-kan-ikke-instrueres` | instruksjonskompetanse | 4 | 4 | 100,0 % | 100,0 % | docs/33 §1: «instruere» 908 treff, 27 % — og nesten bare NEGATIV («kan ikke instruere») = uavhengighet. Mønsteret tar bare den negative formen. |
| `instruksjon-kan-ikke-instruere` | instruksjonskompetanse | 0 | 0 | – | – | docs/33 §1: «instruere» 908 treff, 27 %, nesten bare negativ. |
| `delegerer-kan-delegere` | delegeringskompetanse | 9 | 8 | 88,9 % | 88,9 % | docs/33 §1: «delegerer til» 2875 treff, 47 % presisjon — nesten alltid avgrenset til paragraf (avgrensningen tolkes ikke her). |
| `delegerer-delegeres-til` | har_delegert_til | 4 | 3 | 75,0 % | 75,0 % | docs/33 §1: «delegerer til» 2875 treff, 47 %. |
| `tilsyn-forer-tilsyn` | tilsynskompetanse | 5 | 5 | 100,0 % | 100,0 % | docs/33 §1: «fører tilsyn med at …» 2246 treff, ~33 % presisjon, og «tilsyn med aktør» ≈ 0 % som kompetanse. Første versjon tok alle objekter og fikk 5 av 9 mot … |
| `beslutning-beslutningsmyndighet` | beslutningskompetanse | 1 | 0 | 0,0 % | 0,0 % | Ikke målt i docs/33 §1. Tatt med etter Johanns beslutning 3 på #341 («beslutningsmyndighet» → beslutning). |
| `samordning-samordne` | samordningskompetanse | 3 | 1 | 33,3 % | 33,3 % | Ikke målt i docs/33 §1. Tatt med etter Johanns beslutning 3 på #341 («samordne» → samordning). |
| `plikt-samarbeide` | samarbeidsplikt | 9 | 7 | 77,8 % | 88,9 % | docs/33 §1: «samarbeider med» 966 treff, 40 % presisjon (oppgavebundet, generisk motpart). Tatt inn etter Johanns godkjenning av #353 (energiloven:u27). |
| `plikt-inngaa-avtale` | avtaleplikt | 3 | 2 | 66,7 % | 100,0 % | Ikke målt i docs/33 §1. Tatt inn etter Johanns godkjenning av #353 (helse- og omsorgstjenesteloven § 6-1, a44): avtaleplikten er en P-kant; den inngåtte avtalen … |
| `plikt-dekkes-av` | betalingsplikt | 11 | 4 | 36,4 % | 63,6 % | Ikke målt i docs/33 §1 (finansiering sto utenfor strukturlaget til #353). Tatt inn etter Johanns godkjenning av #353 (spesialisthelsetjenesteloven:u269, a39). |
| `plikt-dekke-utgifter` | betalingsplikt | 7 | 4 | 57,1 % | 57,1 % | Ikke målt i docs/33 §1. Den aktive formen av plikt-dekkes-av, samme leksikonregel-familie (betalingsplikt). |
| `plikt-gi-opplysninger` | informasjonsplikt | 0 | 0 | – | – | Ikke målt i docs/33 §1 (informasjonsdeling sto utenfor strukturlaget til #353). Tatt inn etter Johanns godkjenning av #353 (energiloven:u201). |
| `plikt-innhente-uttalelse` | konsultasjonsplikt | 0 | 0 | – | – | Ikke målt i docs/33 §1. Tatt inn etter Johanns godkjenning av #353 (konsultasjonsplikt). |
| `har-sete-i` | har_sete_i | 1 | 1 | 100,0 % | 100,0 % | Ikke målt i docs/33 §1. Fast lovformulering for sete; Y tas slik den står («rikets hovedstad»), uten oppslag. |
| `inndeling-kommune-i-fylke` | del_av | 47 | 39 | 83,0 % | 100,0 % | Ikke målt i docs/33 §1. Fylkestilhørigheten står eksplisitt i setningen. |
| `inndeling-rettskrets` | har_ansvarsomrade | 357 | 357 | 100,0 % | 100,0 % | docs/33 §1 nevner strukturerte kommunelister i inndelingsforskrifter som høypresisjonskilde (ikke tallfestet). |
| `inndeling-rettssted` | har_sete_i | 61 | 61 | 100,0 % | 100,0 % | Samme kilde som inndeling-rettskrets. |
| `inndeling-kommuneliste` | bestar_av | 56 | 56 | 100,0 % | 100,0 % | Strukturerte kommunelister (docs/33 §1, ikke tallfestet). Retnings- og komplementdefinisjoner kan ikke avgjøres uten et områderegister (#312). |
| `inndeling-utgjor` | bestar_av | 15 | 10 | 66,7 % | 100,0 % | Strukturerte lister i inndelingsforskrifter (docs/33 §1, ikke tallfestet). |
| `inndeling-sogner` | annet:sogner_til | 28 | 28 | 100,0 % | 100,0 % | Strukturerte lister i inndelingsforskrifter (docs/33 §1, ikke tallfestet). |
| `inndeling-bestar-av-liste` | bestar_av | 4 | 4 | 100,0 % | 100,0 % | Ikke målt i docs/33 §1. Generell listeform; kravet om egennavn holder organsammensetning ute. |

## Falske positive (68)

Gruppert på mønster og om eId+kategori+type fantes i fasiten (= feil/manglende aktør) eller ikke (= feil gjenkjenning).

| Mønster | Årsak | Antall |
|---|---|---:|
| `monster:vedtak-avgjores-av` | ikke i fasiten | 9 |
| `monster:inndeling-kommune-i-fylke` | feil/manglende aktør | 8 |
| `monster:forskrift-gi` | ikke i fasiten | 5 |
| `monster:inndeling-utgjor` | feil/manglende aktør | 5 |
| `monster:plikt-dekkes-av` | ikke i fasiten | 4 |
| `monster:vedtak-treffe` | ikke i fasiten | 4 |
| `monster:forskrift-gi` | feil/manglende aktør | 3 |
| `monster:forskrift-i-ved` | ikke i fasiten | 3 |
| `monster:oppnevnt-av` | feil/manglende aktør | 3 |
| `monster:plikt-dekke-utgifter` | ikke i fasiten | 3 |
| `monster:plikt-dekkes-av` | feil/manglende aktør | 3 |
| `monster:forskrift-gitt-av` | ikke i fasiten | 2 |
| `monster:har-medlemmer` | feil/manglende aktør | 2 |
| `monster:samordning-samordne` | ikke i fasiten | 2 |
| `monster:vedtak-avgjor` | ikke i fasiten | 2 |
| `monster:beslutning-beslutningsmyndighet` | ikke i fasiten | 1 |
| `monster:delegerer-delegeres-til` | ikke i fasiten | 1 |
| `monster:delegerer-kan-delegere` | ikke i fasiten | 1 |
| `monster:forskrift-naermere-regler` | ikke i fasiten | 1 |
| `monster:klage-er-klageinstans` | feil/manglende aktør | 1 |
| `monster:klage-paklages-til` | feil/manglende aktør | 1 |
| `monster:plikt-inngaa-avtale` | feil/manglende aktør | 1 |
| `monster:plikt-samarbeide` | feil/manglende aktør | 1 |
| `monster:plikt-samarbeide` | ikke i fasiten | 1 |
| `monster:vedtak-godkjennes-av` | ikke i fasiten | 1 |

De 20 vanligste (én fra hver gruppe i tur, vanligste gruppe først):

| # | Kilde | eId | Mønster | fra → til | Sitat |
|---|---|---|---|---|---|
| 1 | domstolloven | lov/1915/08/13/5/nor/§5/ledd-1 | `monster:vedtak-avgjores-av` | Høyesteretts ankeutvalg → null | I saker som etter lov skal avgjøres av Høyesteretts ankeutvalg, settes Høyesterett med tre dommere. |
| 2 | sameloven | lov/1987/06/12/56/nor/§2-4/ledd-1/punkt-6 | `monster:inndeling-kommune-i-fylke` | Surnadal → Møre og Romsdal fylke | Surnadal |
| 3 | energiloven | lov/1990/06/29/50/nor/§2-4/ledd-1 | `monster:forskrift-gi` | Kongen → null | Kongen kan gi forskrift om at nærmere bestemte vedtak etter § 3-1 skal fattes av Kongen i statsråd. |
| 4 | domstolloven | forskrift/2021/01/22/163/nor/§15/ledd-1 | `monster:inndeling-utgjor` | Borgarting lagdømme → Oslo | Oslo |
| 5 | domstolloven | lov/1915/08/13/5/nor/§105a/ledd-2 | `monster:plikt-dekkes-av` | det offentlige → null | Utgiftene dekkes av det offentlige. |
| 6 | domstolloven | lov/1915/08/13/5/nor/§33/ledd-3 | `monster:vedtak-treffe` | Kongen i statsråd → null | Kongen i statsråd kan treffe vedtak om domstoladministrasjonens virksomhet og administrasjonen av domstolene. |
| 7 | domstolloven | lov/1915/08/13/5/nor/§122/ledd-2 | `monster:forskrift-gi` | Kongen → null | Kongen kan gi forskrift om at opplysninger som nevnt skal gis ved oppslag ved rettens kontor. |
| 8 | domstolloven | lov/1915/08/13/5/nor/§86/ledd-1 | `monster:forskrift-i-ved` | domstoladministrasjonen → null | Domstoladministrasjonen kan ved forskrift dele lagsogn og domssogn i flere trekningskretser. |
| 9 | domstolloven | lov/1915/08/13/5/nor/§43/ledd-2 | `monster:oppnevnt-av` | Kongen → særskilt organ | En granskingskommisjon, et kontrollutvalg eller et annet særskilt organ som er oppnevnt av Kongen, Stortinget eller et departement eller en statsforvalter for å … |
| 10 | helse-og-omsorgstjenesteloven | lov/2011/06/24/30/nor/§11-1/ledd-4 | `monster:plikt-dekke-utgifter` | Kommunen → null | Kommunen dekker reiseutgifter for behandlingspersonell som gir helsetjenester etter folketrygdloven kapittel 5. |
| 11 | helse-og-omsorgstjenesteloven | lov/2011/06/24/30/nor/§11-1/ledd-2 | `monster:plikt-dekkes-av` | den kommunen → null | Kostnadene ved de tjenester og tiltak som er nevnt i første ledd, skal dekkes av den kommunen som etter § 3-1 og § 10-8 er ansvarlig for å yte tjenesten eller s … |
| 12 | domstolloven | lov/1915/08/13/5/nor/§105a/ledd-1 | `monster:forskrift-gitt-av` | Kongen → null | Godtgjørelsen til jordskiftemeddommer og meddommer fastsettes av rettens leder etter forskrifter gitt av Kongen. |
| 13 | domstolloven | lov/1915/08/13/5/nor/§27/ledd-2 | `monster:har-medlemmer` | Forliksrådet → medlemmer | Forliksrådet skal ha tre medlemmer og like mange varamedlemmer. |
| 14 | helse-og-omsorgstjenesteloven | lov/2011/06/24/30/nor/§3-4/ledd-2 | `monster:samordning-samordne` | Kommunen → null | Kommunen skal samordne tjenestetilbudet etter første ledd. |
| 15 | domstolloven | lov/1915/08/13/5/nor/§6/ledd-2 | `monster:vedtak-avgjor` | domstollederen → null | Domstollederen avgjør da om retten skal settes med 11 eller med alle Høyesteretts dommere. |
| 16 | sameloven | lov/1987/06/12/56/nor/§2-1/ledd-4 | `monster:beslutning-beslutningsmyndighet` | Sametinget → null | Sametinget har beslutningsmyndighet når dette følger av andre bestemmelser i loven eller fastsatt på annen måte. |
| 17 | sameloven | forskrift/2004/12/10/1607/nor/ledd-3 | `monster:delegerer-delegeres-til` | Kongen → Kommunal- og regionaldepartementet | Departementet foreslår at Kongens myndighet etter samelovens § 2-11 delegeres delvis til Kommunal- og regionaldepartementet. |
| 18 | energiloven | kap-I/ledd-2 | `monster:delegerer-kan-delegere` | Departementet → Norges vassdrags- og energidirektorat | Departementet delegerer all myndighet etter lov 29. juni 1990 nr. 50 om produksjon, omforming, overføring, omsetning, fordeling og bruk av energi m.m. (energilo … |
| 19 | domstolloven | lov/1915/08/13/5/nor/§33c/ledd-2 | `monster:forskrift-naermere-regler` | domstoladministrasjonen → null | Domstoladministrasjonen gir nærmere bestemmelser om organiseringen av disse dommernes tjenester. |
| 20 | sameloven | lov/1987/06/12/56/nor/§3-11/ledd-1 | `monster:klage-er-klageinstans` | Statsforvalteren → null | Statsforvalteren er klageinstans når klagen angår kommunale eller fylkeskommunale organ. |

## Falske negative (933)

Gruppert på kategori/type. Typer mønsterlaget ikke har mønster for, er forventet her — de er KI-lagets (#308) nevner.

| Kategori / type | Har mønster | Antall |
|---|---|---:|
| K kompetanse / vedtakskompetanse | ja | 90 |
| K kompetanse / oppnevningskompetanse | ja | 72 |
| O sammensetning_omrade / bestar_av | ja | 38 |
| R relasjon / rapporterer_til | nei | 31 |
| M medlemskap / medlem_av | nei | 30 |
| O sammensetning_omrade / del_av | ja | 30 |
| K kompetanse / normgivningskompetanse | ja | 26 |
| A ansvarsomrade / har_ansvarsomrade | ja | 24 |
| K kompetanse / instruksjonskompetanse | ja | 23 |
| K kompetanse / tilsynskompetanse | ja | 21 |
| T konstituerende / skal_finnes | nei | 21 |
| M medlemskap / inngar_i | nei | 18 |
| G organsammensetning / har_medlemmer | ja | 17 |
| R relasjon / har_delegert_til | ja | 17 |
| K kompetanse / klagekompetanse | ja | 15 |
| P plikt / bistandsplikt | nei | 15 |
| R konstituerende / oppretter | nei | 14 |
| P plikt / samarbeidsplikt | ja | 14 |
| P plikt / informasjonsplikt | ja | 13 |
| R relasjon / eies_av | nei | 13 |
| R relasjon / ledes_av | nei | 12 |
| K kompetanse / delegeringskompetanse | ja | 11 |
| K kompetanse / overprovingskompetanse | nei | 11 |
| P plikt / betalingsplikt | ja | 11 |
| G organsammensetning / har_organ | nei | 10 |
| R relasjon / radgir | nei | 10 |
| annet kompetanse / annet:forkynningskompetanse | nei | 9 |
| annet medlemskap / annet:klasse_definert_ved_tjenestekrets | nei | 9 |
| G relasjon / del_av | nei | 9 |
| P plikt / konsultasjonsplikt | ja | 8 |
| annet annet:funksjonstildeling / annet:lovtildelt_oppgave | nei | 7 |
| annet medlemskap / annet:virkeomrade_utvidet_til | nei | 7 |
| annet organsammensetning / annet:valgkrets_for | nei | 7 |
| R relasjon / etterfolger | nei | 7 |
| annet kompetanse / annet:ikraftsettingskompetanse | nei | 6 |
| R relasjon / representerer | nei | 6 |
| annet ansvarsomrade / annet:lokalisert_i | nei | 5 |
| K kompetanse / avsettingskompetanse | nei | 5 |
| R relasjon / administrativt_underordnet | ja | 5 |
| K kompetanse / organisasjonskompetanse | nei | 4 |
| … 155 typer til (alle `annet:*` eller ≤ 4 utsagn) | | 232 |

De 20 vanligste blant typene med mønster (én fra hver type i tur, vanligste type først):

| # | Kilde | eId | Type | fra → til (fasit) | Sitat |
|---|---|---|---|---|---|
| 1 | domstolloven | lov/1915/08/13/5/nor/§13/ledd-1 | vedtakskompetanse | førstelagmann → null | Avgjørelser og andre forføyninger, som ikke gjelder de enkelte rettssaker, treffer førstelagmannen alene |
| 2 | domstolloven | lov/1915/08/13/5/nor/§20/ledd-1 | oppnevningskompetanse | domstoladministrasjonen → null | Domstoladministrasjonen kan oppnevne en særskilt dommer |
| 3 | domstolloven | lov/1915/08/13/5/nor/§16/ledd-1 | bestar_av | Rikets → lagdømmer | Rikets inddeling i lagdømmer |
| 4 | domstolloven | forskrift/2021/01/22/163/nor/§2/ledd-1/punkt-3 | del_av | Finnsnes → Senja | Finnsnes |
| 5 | domstolloven | lov/1915/08/13/5/nor/§48/ledd-4 | normgivningskompetanse | Kongen → null | ved regler, som Kongen gir |
| 6 | domstolloven | lov/1915/08/13/5/nor/§16/ledd-1 | har_ansvarsomrade | lagmannsrettene → lagdømmer | lagdømmer for hver lagmandsret |
| 7 | domstolloven | lov/1915/08/13/5/nor/§33/ledd-2 | instruksjonskompetanse | Stortinget → domstoladministrasjonen | Gjennom Stortingets behandling av budsjettproposisjonen gis årlige retningslinjer for domstoladministrasjonens virksomhet |
| 8 | domstolloven | lov/1915/08/13/5/nor/§33a/ledd-4 | tilsynskompetanse | Riksrevisjonen → domstoladministrasjonen | kritikk fra Riksrevisjonen |
| 9 | domstolloven | lov/1915/08/13/5/nor/§3/ledd-1 | har_medlemmer | Høyesterett → dommere | Retten skal ha en justitiarius og nitten andre dommere. |
| 10 | energiloven | kap-I/ledd-2 | har_delegert_til | Departementet → Norges vassdrags- og energidirektorat | Departementet delegerer all myndighet etter lov 29. juni 1990 nr. 50 |
| 11 | domstolloven | lov/1915/08/13/5/nor/§33b/ledd-1 | klagekompetanse | Kongen → styre | for styrets vedtak Kongen i statsråd |
| 12 | energiloven | kap-I/ledd-9 | samarbeidsplikt | Norges vassdrags- og energidirektorat → Departementet | Gjennomføring av rapportering etter energiloven § 10-1 tredje ledd annet punktum skal samordnes med departementet. |
| 13 | domstolloven | lov/1915/08/13/5/nor/§217a/ledd-1 | informasjonsplikt | Domstolene → Advokattilsynet | varsle Advokattilsynet om avgjørelsen |
| 14 | domstolloven | lov/1915/08/13/5/nor/§238/ledd-6 | delegeringskompetanse | Tilsynsutvalget for dommere → null | Tilsynsutvalget kan gi utvalgets leder eller et annet av utvalgets medlemmer myndighet til |
| 15 | helse-og-omsorgstjenesteloven | lov/2011/06/24/30/nor/§5-3/ledd-3 | betalingsplikt | den kommunen som har bistandsbehovet → andre kommuner | Den kommunen som mottar bistand etter andre ledd, skal yte kommunen som bidrar med hjelp, kompensasjon |
| 16 | sameloven | lov/1987/06/12/56/nor/§4-3/ledd-1/punkt-1 | konsultasjonsplikt | regjeringen → sameting | regjeringen, departementer, direktorater og andre underliggende virksomheter |
| 17 | energiloven | lov/1990/06/29/50/nor/§9-1/ledd-5 | administrativt_underordnet | kraftforsyningen → Kraftforsyningens beredskapsorganisasjon (KBO) | Beredskapsmyndigheten kan under beredskap og i krig underlegge kraftforsyningen KBO. |
| 18 | domstolloven | forskrift/2021/01/22/163/nor/§1/ledd-1 | har_sete_i | tingrettene → rettssteder | med ett eller flere rettssteder |
| 19 | helse-og-omsorgstjenesteloven | lov/2011/06/24/30/nor/§9-7/ledd-4 | godkjenningskompetanse | Statsforvalteren → null | Vedtaket kan ikke iverksettes før det er godkjent av statsforvalteren. |
| 20 | spesialisthelsetjenesteloven | lov/1999/07/02/61/nor/§4-4/ledd-1 | beslutningskompetanse | felles system for å beslutte hvilke metoder som kan tilbys i spesialisthelsetjenesten → null | felles system for å beslutte hvilke metoder som kan tilbys |

## Forkastede mønstre

- `vedtak-forvaltningsverb: verbet «pålegge»` — Prøvd sammen med «gi pålegg/dispensasjon, ilegge, trekke tilbake». Formen ga 11 utsagn, hvorav 8 falske positive: «En domstol kan pålegge en klager …», «Retten kan pålegge …», «Arbeidsgiver kan pålegge helsepersonell …», «Kommunen kan pålegge personell …» — prosessuelle pålegg og arbeidsgivers instruks, ikke enkeltvedtak. De 3 riktige: energiloven § 5-3 og § 10-1a («Departementet kan pålegge ethvert fjernvarmeanlegg …», «Konsesjonsmyndigheten kan … pålegge konsesjonæren …») og helse- og omsorgstjenesteloven § 6-6 («Departementet kan pålegge samarbeid mellom kommuner»). Uten verbet: 26 av 26.
- `tilsyn-forer-tilsyn: alle objekter etter «tilsyn med»` — 5 av 9. Tre av de fire feilene var tilsyn med en AKTØR («fører tilsyn med forliksrådets virksomhet», «med daglig leder», «med helseforetak»), den fjerde «Riksrevisjonen fører kontroll med forvaltningen av statens interesser» — samme skille som korpusmålingen i docs/33 §1 viste. Nå bare «tilsyn/kontroll med at …» og «med lovligheten/etterlevelsen/gjennomføringen/overholdelsen av …».
- `vedtak-godkjennes-av: «er godkjent av X»` — 6 av 12. Perfektum partisipp beskriver en tilstand eller et vilkår («Når avviklingsoppgjøret er godkjent av foretaksmøtet, skal …», «før det er godkjent av statsforvalteren»), ikke hvem som har kompetansen. Nå bare «skal/må/kan (være) godkjennes/godkjent av».
- `Navneliste: siste element med to «og» delt på det første` — Ga «Møre» + «Romsdal og Trööndelagen/Trøndelag» som lagsogn (domstolloven-inndelingen § 12). Tvetydig — teksten sier ikke hvilket «og» som er inne i et navn — så hele lista forkastes nå.
- `oppnevnt-av: ordet rett foran finitt passiv som den oppnevnte` — «Dommere til Høyesterett, …, tingrettene og jordskifterettene utnevnes … av Kongen» ga «jordskifterettene». For «oppnevnes/utnevnes» brukes nå setningens subjekt.
- `(ikke bygget) rapporterer_til` — Fasiten har 31, men uttrykt som «sende melding til», «varsle», «forelegges», «underrette» — formuleringer som like ofte er informasjonsplikter for private (docs/33 §1: «rapporterer til» 45 %). Ingen form med høy nok presisjon til mønsterlaget; overlatt til KI-laget (#308).
- `(ikke bygget) A har_ansvarsomrade tingrett → egen rettskrets` — Fasiten hadde 52 slike («Vestre Finnmark tingrett» har ansvarsområde «Vestre Finnmark tingrett»), men rettskretsens navn står ikke i teksten. [Løst i #312, 2026-10-08:] Johann forkastet rettskrets-aktørene i fasitkontrollen; fasiten er rettet til «tingrett har_ansvarsomrade kommune», som inndeling-rettskrets nå gir direkte.
- `inndeling-har-rettskretsen (fjernet i #312)` — «X fylke har rettskretsen N tingrett» → N del_av X fylke uttrykte tingretten som et område. Fjernet sammen med rettskrets-aktørene i fasiten (Johanns funn på #312).
