# Måling: KI-konvertering mot strukturfasiten, side om side med mønsterlaget

Generert av `KiStrukturkonvertererLiveMalingTests` (live) og `KiMalingRapportTests` (regenererer fra lagret utdata)
i `src/RegelIde.Data.Tests/Strukturfasit/` — **ikke rediger for hånd**. Sak: #308, designgrunnlag: `docs/33` §5.
KI-utdataene fra kjøringen ligger i `ki-utdata/` (fasit-formatet per kilde + `kjoring.json`).

- **Modell:** `deepseek-ai/DeepSeek-V4-Flash` (`oppdagelseskilde = "ki:deepseek-ai/DeepSeek-V4-Flash"`)
- **Kjørt:** 2026-10-07 10:16 +02:00. **Antall live-kjøringer av målingen hittil:** 1.
- **Systeminstruks:** `KiStrukturkonverterer.SystemInstruks`, avtrykk `64071f24989f` (SHA-256, 12 tegn). Ikke iterert mot fasiten.
- **Oppdeling:** maks 6000 tegn nodetekst per kall, 4 kall samtidig, halvering ved ugyldig JSON inntil 2 ganger.

**Forbehold:** fasiten er KI-annotert (Claude) og ikke menneskelig verifisert (`docs/33` §2, #309). En KI som annoterer
som fasit-annotatøren får fordel av det; tallene er bare så gode som fasiten. Én kjøring — KI-svar varierer mellom kjøringer.

**Treffregel** (som `maling-monster.md`): samme eId + kategori + type, og for hvert endepunkt fasiten har: minst én felles
skrivemåte (tekstform ∪ varianter, uten skille på store/små). Én-til-én. **Union** = alle mønsterutsagn + KI-utsagn som ikke har
samme eId + kategori + type + fra/til-tekstform som et mønsterutsagn. **Bare mønster / bare KI** = fasitutsagn bare det ene laget traff.

## Totalt

| Lag | Fasit | Predikert | Treff | Presisjon | Gjenfinning | Presisjon u/endepunkt | Gjenfinning u/endepunkt |
|---|---:|---:|---:|---:|---:|---:|---:|
| Mønster | 1797 | 899 | 843 | 93,8 % | 46,9 % | 95,9 % | 48,0 % |
| KI | 1797 | 1043 | 359 | 34,4 % | 20,0 % | 39,8 % | 23,1 % |
| Mønster ∪ KI | 1797 | 1698 | 970 | 57,1 % | 54,0 % | 60,8 % | 57,4 % |

Av 1797 fasitutsagn traff bare mønsterlaget 614, bare KI-laget 130.

## Per kategori (kanttype, `docs/33` §4.3)

P = presisjon, G = gjenfinning (med endepunktkrav).

| Kategori | Fasit | Mønster pred. | Mønster P | Mønster G | KI pred. | KI P | KI G | Union P | Union G | Bare mønster | Bare KI |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| R | 223 | 36 | 77,8 % | 12,6 % | 131 | 24,4 % | 14,3 % | 34,5 % | 25,6 % | 25 | 29 |
| K | 427 | 288 | 88,5 % | 59,7 % | 674 | 45,1 % | 71,2 % | 46,3 % | 78,0 % | 32 | 81 |
| M | 48 | 0 | – | 0,0 % | 22 | 4,5 % | 2,1 % | 4,5 % | 2,1 % | 0 | 1 |
| O | 177 | 122 | 89,3 % | 61,6 % | 11 | 0,0 % | 0,0 % | 82,0 % | 61,6 % | 109 | 0 |
| A | 449 | 419 | 100,0 % | 93,3 % | 51 | 0,0 % | 0,0 % | 89,1 % | 93,3 % | 419 | 0 |
| G | 43 | 6 | 66,7 % | 9,3 % | 41 | 12,2 % | 11,6 % | 12,8 % | 14,0 % | 1 | 2 |
| I | 0 | 0 | – | – | 0 | – | – | – | – | 0 | 0 |
| T | 21 | 0 | – | 0,0 % | 14 | 14,3 % | 9,5 % | 14,3 % | 9,5 % | 0 | 2 |
| annet | 370 | 28 | 100,0 % | 7,6 % | 60 | 1,7 % | 0,3 % | 33,0 % | 7,8 % | 28 | 1 |
| senere lag | 39 | 0 | – | 0,0 % | 39 | 35,9 % | 35,9 % | 35,9 % | 35,9 % | 0 | 14 |
| **Alle** | 1797 | 899 | 93,8 % | 46,9 % | 1043 | 34,4 % | 20,0 % | 57,1 % | 54,0 % | 614 | 130 |

### KI uten endepunktkrav (bare eId + kategori + type)

Avstanden til tabellen over er utsagn KI-en gjenkjente, men med feil eller manglende aktør.

| Kategori | KI P u/endepunkt | KI G u/endepunkt | Mønster P u/endepunkt | Mønster G u/endepunkt |
|---|---:|---:|---:|---:|
| R | 41,2 % | 24,2 % | 94,4 % | 15,2 % |
| K | 46,0 % | 72,6 % | 88,5 % | 59,7 % |
| M | 13,6 % | 6,3 % | – | 0,0 % |
| O | 81,8 % | 5,1 % | 98,4 % | 67,8 % |
| A | 7,8 % | 0,9 % | 100,0 % | 93,3 % |
| G | 22,0 % | 20,9 % | 100,0 % | 14,0 % |
| I | – | – | – | – |
| T | 42,9 % | 28,6 % | – | 0,0 % |
| annet | 1,7 % | 0,3 % | 100,0 % | 7,6 % |
| senere lag | 48,7 % | 48,7 % | – | 0,0 % |

## Per kilde

| Kilde | Fasit | Mønster pred. | Mønster P | Mønster G | KI pred. | KI P | KI G | Union P | Union G | Bare mønster | Bare KI |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| domstolloven | 764 | 539 | 95,7 % | 67,5 % | 344 | 16,3 % | 7,3 % | 63,4 % | 70,0 % | 479 | 19 |
| energiloven | 254 | 114 | 94,7 % | 42,5 % | 212 | 57,5 % | 48,0 % | 60,8 % | 57,5 % | 25 | 39 |
| helse-og-omsorgstjenesteloven | 192 | 49 | 91,8 % | 23,4 % | 133 | 48,9 % | 33,9 % | 48,1 % | 33,3 % | 1 | 21 |
| sameloven | 285 | 130 | 87,7 % | 40,0 % | 70 | 27,1 % | 6,7 % | 66,8 % | 43,9 % | 106 | 11 |
| spesialisthelsetjenesteloven | 302 | 67 | 89,6 % | 19,9 % | 284 | 34,2 % | 32,1 % | 34,0 % | 33,1 % | 3 | 40 |

## Per type

Alle typer fra FORMAT.md-listene som står i fasiten eller som et av lagene predikerte. `annet:*` er samlet i kategoritabellen.

| Kategori / type | Fasit | Mønster pred. | Mønster P | Mønster G | KI pred. | KI P | KI G | Union P | Union G | Bare mønster | Bare KI |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| R relasjon / delegerer_til | 36 | 13 | 92,3 % | 33,3 % | 3 | 33,3 % | 2,8 % | 75,0 % | 33,3 % | 11 | 0 |
| R relasjon / rapporterer_til | 31 | 0 | – | 0,0 % | 32 | 31,3 % | 32,3 % | 31,3 % | 32,3 % | 0 | 10 |
| R relasjon / oppnevner | 23 | 10 | 70,0 % | 30,4 % | 5 | 0,0 % | 0,0 % | 46,7 % | 30,4 % | 7 | 0 |
| R relasjon / instruksjon | 21 | 4 | 100,0 % | 19,0 % | 16 | 6,3 % | 4,8 % | 25,0 % | 23,8 % | 4 | 1 |
| R relasjon / klageinstans_for | 17 | 9 | 55,6 % | 29,4 % | 14 | 28,6 % | 23,5 % | 33,3 % | 41,2 % | 3 | 2 |
| R relasjon / velger | 17 | 0 | – | 0,0 % | 7 | 28,6 % | 11,8 % | 28,6 % | 11,8 % | 0 | 2 |
| R konstituerende / oppretter | 14 | 0 | – | 0,0 % | 7 | 57,1 % | 28,6 % | 57,1 % | 28,6 % | 0 | 4 |
| R relasjon / eies_av | 13 | 0 | – | 0,0 % | 10 | 10,0 % | 7,7 % | 10,0 % | 7,7 % | 0 | 1 |
| R relasjon / ledes_av | 12 | 0 | – | 0,0 % | 5 | 40,0 % | 16,7 % | 40,0 % | 16,7 % | 0 | 2 |
| R relasjon / radgir | 10 | 0 | – | 0,0 % | 8 | 25,0 % | 20,0 % | 25,0 % | 20,0 % | 0 | 2 |
| R relasjon / tilsyn_med_aktor | 10 | 0 | – | 0,0 % | 8 | 12,5 % | 10,0 % | 12,5 % | 10,0 % | 0 | 1 |
| R relasjon / etterfolger | 7 | 0 | – | 0,0 % | 5 | 60,0 % | 42,9 % | 60,0 % | 42,9 % | 0 | 3 |
| R relasjon / administrativt_underordnet | 5 | 0 | – | 0,0 % | 5 | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0 | 0 |
| R relasjon / omgjoring | 3 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| R konstituerende / avvikler | 2 | 0 | – | 0,0 % | 4 | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0 | 0 |
| R organsammensetning / ledes_av | 1 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| R relasjon / sekretariat_for | 1 | 0 | – | 0,0 % | 2 | 50,0 % | 100,0 % | 50,0 % | 100,0 % | 0 | 1 |
| K kompetanse / forskriftskompetanse | 205 | 198 | 91,9 % | 88,8 % | 196 | 89,8 % | 85,9 % | 89,3 % | 93,2 % | 16 | 10 |
| K kompetanse / vedtakskompetanse | 158 | 85 | 80,0 % | 43,0 % | 361 | 28,8 % | 65,8 % | 30,4 % | 75,3 % | 16 | 52 |
| K kompetanse / utpekingskompetanse | 21 | 0 | – | 0,0 % | 19 | 26,3 % | 23,8 % | 26,3 % | 23,8 % | 0 | 5 |
| K kompetanse / tilsynskompetanse | 16 | 5 | 100,0 % | 31,3 % | 16 | 68,8 % | 68,8 % | 71,4 % | 62,5 % | 0 | 6 |
| K kompetanse / oppnevningskompetanse | 13 | 0 | – | 0,0 % | 24 | 12,5 % | 23,1 % | 12,5 % | 23,1 % | 0 | 3 |
| K kompetanse / instruksjonskompetanse | 6 | 0 | – | 0,0 % | 17 | 11,8 % | 33,3 % | 11,8 % | 33,3 % | 0 | 2 |
| K kompetanse / klagekompetanse | 5 | 0 | – | 0,0 % | 14 | 14,3 % | 40,0 % | 14,3 % | 40,0 % | 0 | 2 |
| K kompetanse / delegeringsfullmakt | 3 | 0 | – | 0,0 % | 27 | 3,7 % | 33,3 % | 3,7 % | 33,3 % | 0 | 1 |
| M medlemskap / medlem_av | 30 | 0 | – | 0,0 % | 22 | 4,5 % | 3,3 % | 4,5 % | 3,3 % | 0 | 1 |
| M medlemskap / inngar_i | 18 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| O sammensetning_omrade / bestar_av | 108 | 75 | 93,3 % | 64,8 % | 11 | 0,0 % | 0,0 % | 81,4 % | 64,8 % | 70 | 0 |
| O sammensetning_omrade / del_av | 69 | 47 | 83,0 % | 56,5 % | 0 | – | 0,0 % | 83,0 % | 56,5 % | 39 | 0 |
| A ansvarsomrade / har_ansvarsomrade | 381 | 357 | 100,0 % | 93,7 % | 35 | 0,0 % | 0,0 % | 91,1 % | 93,7 % | 357 | 0 |
| A ansvarsomrade / har_sete_i | 65 | 62 | 100,0 % | 95,4 % | 0 | – | 0,0 % | 100,0 % | 95,4 % | 62 | 0 |
| A ansvarsomrade / har_jurisdiksjon | 3 | 0 | – | 0,0 % | 16 | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0 | 0 |
| G organsammensetning / har_medlemmer | 24 | 6 | 66,7 % | 16,7 % | 16 | 18,8 % | 12,5 % | 18,2 % | 16,7 % | 1 | 0 |
| G organsammensetning / har_organ | 10 | 0 | – | 0,0 % | 6 | 33,3 % | 20,0 % | 33,3 % | 20,0 % | 0 | 2 |
| G relasjon / del_av | 9 | 0 | – | 0,0 % | 19 | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0 | 0 |
| T konstituerende / skal_finnes | 21 | 0 | – | 0,0 % | 14 | 14,3 % | 9,5 % | 14,3 % | 9,5 % | 0 | 2 |
| senere lag relasjon / samarbeider_med | 24 | 0 | – | 0,0 % | 19 | 57,9 % | 45,8 % | 57,9 % | 45,8 % | 0 | 11 |
| senere lag relasjon / bistar | 15 | 0 | – | 0,0 % | 20 | 15,0 % | 20,0 % | 15,0 % | 20,0 % | 0 | 3 |

## Kall, tokens og kostnad

| Kilde | Deler | Kall | Ugyldig JSON | Feilede kall | Tapte noder | Tokens inn | Tokens ut | Sekunder |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| domstolloven | 29 | 29 | 0 | 1 | 28 | 96 466 | 91 210 | 556 |
| energiloven | 15 | 15 | 0 | 0 | 0 | 49 422 | 47 586 | 208 |
| helse-og-omsorgstjenesteloven | 11 | 11 | 0 | 0 | 0 | 38 676 | 25 273 | 132 |
| sameloven | 9 | 9 | 0 | 0 | 0 | 24 307 | 18 425 | 56 |
| spesialisthelsetjenesteloven | 15 | 15 | 0 | 0 | 0 | 50 445 | 54 218 | 173 |
| **Alle** | 79 | 79 | 0 | 1 | 28 | 259 316 | 236 712 | 1125 |

**Kostnad:** ≈ 0.16 EUR for hele kjøringen (0.29 EUR per million tokens inn, 0.35 ut; hostyourai.com/pricing, lest 2026-10-07). Øvre grense: hurtigbufrede inndata-tokens (0,07 €/M) rapporteres ikke separat og er regnet til full pris.

Kall med ugyldig JSON eller feil (1, første 300 tegn av svaret):

- domstolloven: Kall feilet (28 noder, første https://lovdata.no/eli/forskrift/2021/01/22/163/nor/§1/ledd-1): KI-kallet mot 'https://hostyourai.com/api/v1/chat/completions' fei …

## Kastede rader i valideringen (272 av 1315 rader KI-en svarte med)

Hard validering, ingen reparasjon (#308). Hver rad telles på første regel som slår til, i rekkefølgen i tabellen.
Kall med ugyldig JSON telles i tabellen over (radene i dem er ukjente), ikke her.

| Årsak | domstolloven | energiloven | helse-og-omsorgstjenesteloven | sameloven | spesialisthelsetjenesteloven | Alle |
|---|---:|---:|---:|---:|---:|---:|
| `ugyldig_felt` | 45 | 22 | 0 | 0 | 4 | 71 |
| `ukjent_eid` | 17 | 0 | 0 | 0 | 0 | 17 |
| `falskt_sitat` | 61 | 11 | 12 | 4 | 3 | 91 |
| `ukjent_kategori` | 3 | 1 | 0 | 1 | 9 | 14 |
| `ukjent_type` | 4 | 2 | 0 | 4 | 11 | 21 |
| `ukjent_aktorreferanse` | 2 | 0 | 0 | 0 | 2 | 4 |
| `ugyldig_aktor` | 22 | 3 | 7 | 19 | 1 | 52 |
| `duplikat` | 0 | 0 | 0 | 0 | 2 | 2 |

`falskt_sitat` fordelt på diagnose (bare til forklaring — radene er kastet uansett):

- står ikke i noden (parafrase eller oppdiktet): 77
- står ordrett i en annen node i samme kall (feil tagg): 14

Avviste aktører (en rad som peker på en av dem, telles som `ugyldig_aktor`):

- ukjent oppløsning: 17
- tekstform står ikke i teksten: 14
- ugyldig felt: 9
- variant står ikke i teksten: 2

Eksempler (én fra hver årsak i tur, inntil 20):

| # | Kilde | Årsak | eId | Kategori / type | Sitat | Detalj |
|---|---|---|---|---|---|---|
| 1 | domstolloven | `ugyldig_felt` | lov/1915/08/13/5/nor/§11/ledd-3 | kompetanse / delegeringsfullmakt | Førstelagmannen kan bemyndige lagmennene til å utføre de forretningene som etter andre lovbestemmelser er tillagt denne. | sikkerhet = «høy». |
| 2 | domstolloven | `ukjent_eid` | n107b | kompetanse / vedtakskompetanse | Videre kan et forvaltningsorgan som ledd i forberedelsen av eller kontrollen med enkeltvedtak kreve slikt bevisopptak. | Taggen «n107b» står ikke i dette kallets inndata. |
| 3 | domstolloven | `falskt_sitat` | lov/1915/08/13/5/nor/§1/ledd-1 | sammensetning_omrade / bestar_av | De alminnelige domstoler er: Høyesterett, lagmannsrettene, tingrettene | står ikke i noden (parafrase eller oppdiktet) |
| 4 | domstolloven | `ukjent_kategori` | lov/1915/08/13/5/nor/§11/ledd-4 | organisasjonsstruktur / bestar_av | Hvor saksmengden gjør det påkrevet, kan lagmannsretten etter bestemmelse av domstoladministrasjonen deles i avdelinger. | Kategorien «organisasjonsstruktur» står ikke i FORMAT.md. |
| 5 | domstolloven | `ukjent_type` | lov/1915/08/13/5/nor/§11/ledd-4 | medlemskap / har_medlemmer | I så fall kan det utnevnes en lagmann som leder for hver avdeling. | Typen «har_medlemmer» står ikke i FORMAT.md-lista for «medlemskap». |
| 6 | domstolloven | `ukjent_aktorreferanse` | lov/1915/08/13/5/nor/§92/ledd-1 | kompetanse / vedtakskompetanse | Dersom en uttrukket meddommer ikke kan gjøre tjeneste eller ikke møter, skal et varamedlem av samme kjønn innkalles. | fra = «null» finnes ikke i svarets aktorer. |
| 7 | domstolloven | `ugyldig_aktor` | lov/1915/08/13/5/nor/§48/ledd-1 | relasjon / instruksjon | eller følger av overenskomst med fremmed stat | fra = «a5»: tekstform står ikke i teksten. |
| 8 | spesialisthelsetjenesteloven | `duplikat` | lov/1999/07/02/61/nor/§2-4/ledd-1 | kompetanse / forskriftskompetanse | Departementet kan gi forskrifter om | Samme eId, kategori, type, fra, til og sitat som en tidligere rad. |
| 9 | domstolloven | `ugyldig_felt` | lov/1915/08/13/5/nor/§11/ledd-4 | kompetanse / vedtakskompetanse | Førstelagmannen fordeler sakene mellom avdelingene og treffer bestemmelse om dommernes tjenestegjøring. | sikkerhet = «høy». |
| 10 | domstolloven | `ukjent_eid` | n107c | relasjon / oppnevner | En granskingskommisjon, et kontrollutvalg eller et annet særskilt organ som er oppnevnt av Kongen, Stortinget eller et departement eller en statsforvalter | Taggen «n107c» står ikke i dette kallets inndata. |
| 11 | domstolloven | `falskt_sitat` | lov/1915/08/13/5/nor/§1/ledd-1 | sammensetning_omrade / bestar_av | De alminnelige domstoler er: Høyesterett, lagmannsrettene, tingrettene | står ikke i noden (parafrase eller oppdiktet) |
| 12 | domstolloven | `ukjent_kategori` | lov/1915/08/13/5/nor/§19/ledd-4 | organisasjonsstruktur / bestar_av | kan tingrettene etter bestemmelse av domstoladministrasjonen deles i avdelinger. | Kategorien «organisasjonsstruktur» står ikke i FORMAT.md. |
| 13 | domstolloven | `ukjent_type` | lov/1915/08/13/5/nor/§19/ledd-4 | medlemskap / har_medlemmer | I så fall kan det utnevnes en dommer som leder for hver avdeling. | Typen «har_medlemmer» står ikke i FORMAT.md-lista for «medlemskap». |
| 14 | domstolloven | `ukjent_aktorreferanse` | lov/1915/08/13/5/nor/§241/ledd-1 | kompetanse / vedtakskompetanse | kan vedkommende styresmakt i særlige tilfelle godta tilsvarende juridisk eksamen i utlandet | fra = «null» finnes ikke i svarets aktorer. |
| 15 | domstolloven | `ugyldig_aktor` | lov/1915/08/13/5/nor/§58/ledd-1 | kompetanse / vedtakskompetanse | Valget innberettes til statsforvalteren. Finner han valget lovlig, utferdiger han oppnevnelse for de valgte | fra = «a2»: ukjent oppløsning. |
| 16 | spesialisthelsetjenesteloven | `duplikat` | lov/1999/07/02/61/nor/§2-4/ledd-1 | kompetanse / forskriftskompetanse | Departementet kan gi forskrifter om | Samme eId, kategori, type, fra, til og sitat som en tidligere rad. |
| 17 | domstolloven | `ugyldig_felt` | lov/1915/08/13/5/nor/§11/ledd-4 | kompetanse / vedtakskompetanse | Lagmannen fordeler sakene mellom dommerne i avdelingen. | sikkerhet = «høy». |
| 18 | domstolloven | `ukjent_eid` | n107d | relasjon / oppnevner | En granskingskommisjon, et kontrollutvalg eller et annet særskilt organ som er oppnevnt av Kongen, Stortinget eller et departement eller en statsforvalter | Taggen «n107d» står ikke i dette kallets inndata. |
| 19 | domstolloven | `falskt_sitat` | lov/1915/08/13/5/nor/§2/ledd-1 | sammensetning_omrade / bestar_av | Særdomstoler er: jordskifterettene; de overordentlige domstoler, som nedsettes etter § 29; konsulrettene i utlandet; Riksretten. | står ikke i noden (parafrase eller oppdiktet) |
| 20 | domstolloven | `ukjent_kategori` | lov/1915/08/13/5/nor/§62/ledd-1 | annet / annet:inkorporasjon | For tjenestemenn ved domstolene gjelder lov 4. mars 1983 nr. 3 om statens tjenestemenn m.m. | Kategorien «annet» står ikke i FORMAT.md. |

## KI: falske positive (684)

| Kategori | Feil/manglende aktør | Ikke i fasiten |
|---|---:|---:|
| R | 24 | 75 |
| K | 22 | 348 |
| M | 2 | 19 |
| O | 9 | 2 |
| A | 4 | 47 |
| G | 5 | 31 |
| T | 4 | 8 |
| annet | 0 | 59 |
| senere lag | 7 | 18 |

Eksempler (én fra hver kategori i tur, inntil 20):

| # | Kilde | eId | Type | Årsak | fra → til | Sitat |
|---|---|---|---|---|---|---|
| 1 | domstolloven | lov/1915/08/13/5/nor/§22/ledd-1 | rapporterer_til | ikke i fasiten | Kongen → Stortinget | Endringer i rettskretsene skal forelegges for Stortinget. |
| 2 | domstolloven | lov/1915/08/13/5/nor/§21/ledd-2 | vedtakskompetanse | ikke i fasiten | domstolens leder → null | I vidløftige saker kan domstolens leder bestemme at en varadommer skal følge forhandlingen og tre inn dersom dommeren får forfall. |
| 3 | domstolloven | lov/1915/08/13/5/nor/§55a/ledd-4 | medlem_av | ikke i fasiten | Direktøren for domstoladministrasjonen → Innstillingsrådet | Direktøren for domstoladministrasjonen eller den direktøren bemyndiger, har møterett i Innstillingsrådet. |
| 4 | domstolloven | forskrift/2021/01/22/163/nor/§10/ledd-1 | bestar_av | feil/manglende aktør | null → Lagsogn | Landet deles inn i lagdømmer som består av flere lagsogn. Hvert lagdømme har en lagmannsrett som er ankeinstans for flere rettskretser. |
| 5 | domstolloven | lov/1915/08/13/5/nor/§67/ledd-1 | har_ansvarsomrade | ikke i fasiten | kommunen → null | Kommunen skal oppfordre allmennheten til å foreslå kandidater til valget. |
| 6 | domstolloven | lov/1915/08/13/5/nor/§3/ledd-1 | har_medlemmer | feil/manglende aktør | Høyesterett → justitiarius | Retten skal ha en justitiarius og nitten andre dommere. |
| 7 | domstolloven | lov/1915/08/13/5/nor/§23/ledd-1 | skal_finnes | ikke i fasiten | domstoladministrasjonen → null | I de domssogn, hvor domstoladministrasjonen finner det påkrevet, skal dommerfullmektiger ansettes. |
| 8 | domstolloven | lov/1915/08/13/5/nor/§1/ledd-2 | annet:har_begrenset_domsmyndighet | ikke i fasiten | Forliksrådene → null | Forliksrådene er meklingsinstitusjoner med begrenset domsmyndighet |
| 9 | domstolloven | lov/1915/08/13/5/nor/§33b/ledd-2 | bistar | ikke i fasiten | direktøren → styret | For lederstillinger utenom stillingen som domstoladministrasjonens direktør, avgir direktøren forslag. |
| 10 | domstolloven | lov/1915/08/13/5/nor/§25/ledd-1 | rapporterer_til | ikke i fasiten | Kongen → Stortinget | Endringer i de faste rettsstedene skal forelegges for Stortinget. |
| 11 | domstolloven | lov/1915/08/13/5/nor/§21/ledd-2 | vedtakskompetanse | ikke i fasiten | domstolen → null | Når retten settes med en dommer og en varadommer og det bare er en fast dommer ved domstolen, tilkaller domstolen en dommer etter reglene i domstolloven § 19 an … |
| 12 | domstolloven | lov/1915/08/13/5/nor/§66/ledd-1 | medlem_av | ikke i fasiten | kommunestyret → utvalgene av meddommere | Medlemmene til utvalgene av meddommere velges av kommunestyret selv hvert fjerde år. |
| 13 | domstolloven | forskrift/2021/01/22/163/nor/§11/ledd-1 | bestar_av | feil/manglende aktør | Hålogaland lagdømme → null | Lagsognene Nordland, Romsa/Troms og Finnmárku/Finnmark utgjør Hålogaland lagdømme. |
| 14 | domstolloven | lov/1915/08/13/5/nor/§69/ledd-1 | har_ansvarsomrade | ikke i fasiten | kommunen → null | Fortegnelse over de valgte meddommere føres av kommunen. |
| 15 | domstolloven | lov/1915/08/13/5/nor/§27/ledd-2 | har_medlemmer | feil/manglende aktør | forliksråd → null | Forliksrådet skal ha tre medlemmer og like mange varamedlemmer. |
| 16 | domstolloven | lov/1915/08/13/5/nor/§27/ledd-1 | skal_finnes | feil/manglende aktør | kommune → null | I hver kommune skal det være et forliksråd. |
| 17 | domstolloven | lov/1915/08/13/5/nor/§2/ledd-2 | annet:gjelder_lov | ikke i fasiten | jordskifterettene → null | For de domstoler som er nevnt under nr. 1-4, gjelder denne lov |
| 18 | domstolloven | lov/1915/08/13/5/nor/§33b/ledd-2 | bistar | ikke i fasiten | innstillingsråd → styret | For andre stillinger avgis innstillingen fra et innstillingsråd etter tjenestemannsloven. |
| 19 | domstolloven | lov/1915/08/13/5/nor/§27/ledd-4 | velger | ikke i fasiten | forliksråd → leder for domstolen | Den ene formannen velges som leder for domstolen. |
| 20 | domstolloven | lov/1915/08/13/5/nor/§22/ledd-1 | vedtakskompetanse | ikke i fasiten | Kongen → null | Rikets inndeling i domssogn for tingrettene (rettskretser) bestemmes av Kongen. |

## Falske negative per type (fasitutsagn ingen / bare ett lag fant)

| Kategori / type | Fasit | Ikke funnet av KI | Ikke funnet av mønster | Ikke funnet av noen |
|---|---:|---:|---:|---:|
| A ansvarsomrade / har_ansvarsomrade | 381 | 381 | 24 | 24 |
| K kompetanse / forskriftskompetanse | 205 | 29 | 23 | 14 |
| K kompetanse / vedtakskompetanse | 158 | 54 | 90 | 39 |
| O sammensetning_omrade / bestar_av | 108 | 108 | 38 | 38 |
| O sammensetning_omrade / del_av | 69 | 69 | 30 | 30 |
| A ansvarsomrade / har_sete_i | 65 | 65 | 3 | 3 |
| R relasjon / delegerer_til | 36 | 35 | 24 | 24 |
| R relasjon / rapporterer_til | 31 | 21 | 31 | 21 |
| M medlemskap / medlem_av | 30 | 29 | 30 | 29 |
| annet ansvarsomrade / annet:sogner_til | 28 | 28 | 0 | 0 |
| G organsammensetning / har_medlemmer | 24 | 21 | 20 | 20 |
| senere lag relasjon / samarbeider_med | 24 | 13 | 24 | 13 |
| R relasjon / oppnevner | 23 | 23 | 16 | 16 |
| R relasjon / instruksjon | 21 | 20 | 17 | 16 |
| T konstituerende / skal_finnes | 21 | 19 | 21 | 19 |
| K kompetanse / utpekingskompetanse | 21 | 16 | 21 | 16 |
| M medlemskap / inngar_i | 18 | 18 | 18 | 18 |
| R relasjon / klageinstans_for | 17 | 13 | 12 | 10 |
| R relasjon / velger | 17 | 15 | 17 | 15 |
| K kompetanse / tilsynskompetanse | 16 | 5 | 11 | 6 |
| senere lag relasjon / bistar | 15 | 12 | 15 | 12 |
| R konstituerende / oppretter | 14 | 10 | 14 | 10 |
| R relasjon / eies_av | 13 | 12 | 13 | 12 |
| K kompetanse / oppnevningskompetanse | 13 | 10 | 13 | 10 |
| annet relasjon / annet:informasjonsdeling | 12 | 12 | 12 | 12 |
| R relasjon / ledes_av | 12 | 10 | 12 | 10 |
| annet annet:finansieringsansvar / annet:dekker_utgifter_for | 11 | 11 | 11 | 11 |
| G organsammensetning / har_organ | 10 | 8 | 10 | 8 |
| R relasjon / radgir | 10 | 8 | 10 | 8 |
| R relasjon / tilsyn_med_aktor | 10 | 9 | 10 | 9 |
| … 186 typer til | 364 | | | |

