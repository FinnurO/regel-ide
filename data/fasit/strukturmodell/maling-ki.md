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
| Mønster | 1796 | 934 | 865 | 92,6 % | 48,2 % | 94,8 % | 49,3 % |
| KI | 1796 | 1042 | 385 | 36,9 % | 21,4 % | 44,4 % | 25,8 % |
| Mønster ∪ KI | 1796 | 1721 | 1004 | 58,3 % | 55,9 % | 62,8 % | 60,2 % |

Av 1796 fasitutsagn traff bare mønsterlaget 622, bare KI-laget 142.

## Per kategori (kanttype, `docs/33` §4.3)

P = presisjon, G = gjenfinning (med endepunktkrav).

| Kategori | Fasit | Mønster pred. | Mønster P | Mønster G | KI pred. | KI P | KI G | Union P | Union G | Bare mønster | Bare KI |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| R | 104 | 4 | 75,0 % | 2,9 % | 81 | 33,3 % | 26,0 % | 32,5 % | 26,0 % | 0 | 24 |
| K | 592 | 324 | 87,7 % | 48,0 % | 723 | 46,3 % | 56,6 % | 48,1 % | 64,9 % | 52 | 103 |
| P | 79 | 31 | 58,1 % | 22,8 % | 43 | 32,6 % | 17,7 % | 39,1 % | 34,2 % | 13 | 9 |
| M | 47 | 0 | – | 0,0 % | 22 | 4,5 % | 2,1 % | 4,5 % | 2,1 % | 0 | 1 |
| O | 177 | 122 | 89,3 % | 61,6 % | 11 | 0,0 % | 0,0 % | 82,0 % | 61,6 % | 109 | 0 |
| A | 449 | 419 | 100,0 % | 93,3 % | 51 | 0,0 % | 0,0 % | 89,1 % | 93,3 % | 419 | 0 |
| G | 45 | 6 | 66,7 % | 8,9 % | 41 | 12,2 % | 11,1 % | 12,8 % | 13,3 % | 1 | 2 |
| I | 0 | 0 | – | – | 0 | – | – | – | – | 0 | 0 |
| T | 19 | 0 | – | 0,0 % | 14 | 14,3 % | 10,5 % | 14,3 % | 10,5 % | 0 | 2 |
| annet | 284 | 28 | 100,0 % | 9,9 % | 56 | 1,8 % | 0,4 % | 34,5 % | 10,2 % | 28 | 1 |
| **Alle** | 1796 | 934 | 92,6 % | 48,2 % | 1042 | 36,9 % | 21,4 % | 58,3 % | 55,9 % | 622 | 142 |

### KI uten endepunktkrav (bare eId + kategori + type)

Avstanden til tabellen over er utsagn KI-en gjenkjente, men med feil eller manglende aktør.

| Kategori | KI P u/endepunkt | KI G u/endepunkt | Mønster P u/endepunkt | Mønster G u/endepunkt |
|---|---:|---:|---:|---:|
| R | 46,9 % | 36,5 % | 75,0 % | 2,9 % |
| K | 51,7 % | 63,2 % | 89,2 % | 48,8 % |
| P | 46,5 % | 25,3 % | 64,5 % | 25,3 % |
| M | 13,6 % | 6,4 % | – | 0,0 % |
| O | 81,8 % | 5,1 % | 98,4 % | 67,8 % |
| A | 7,8 % | 0,9 % | 100,0 % | 93,3 % |
| G | 22,0 % | 20,0 % | 100,0 % | 13,3 % |
| I | – | – | – | – |
| T | 35,7 % | 26,3 % | – | 0,0 % |
| annet | 1,8 % | 0,4 % | 100,0 % | 9,9 % |

## Per kilde

| Kilde | Fasit | Mønster pred. | Mønster P | Mønster G | KI pred. | KI P | KI G | Union P | Union G | Bare mønster | Bare KI |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| domstolloven | 775 | 540 | 95,6 % | 66,6 % | 344 | 18,0 % | 8,0 % | 63,9 % | 69,3 % | 475 | 21 |
| energiloven | 241 | 117 | 94,0 % | 45,6 % | 211 | 58,8 % | 51,5 % | 62,0 % | 62,2 % | 27 | 41 |
| helse-og-omsorgstjenesteloven | 191 | 58 | 87,9 % | 26,7 % | 133 | 51,1 % | 35,6 % | 49,6 % | 36,1 % | 3 | 20 |
| sameloven | 286 | 133 | 89,5 % | 41,6 % | 70 | 32,9 % | 8,0 % | 69,7 % | 45,8 % | 108 | 12 |
| spesialisthelsetjenesteloven | 303 | 86 | 80,2 % | 22,8 % | 284 | 38,0 % | 35,6 % | 37,6 % | 38,6 % | 9 | 48 |

## Per type

Alle typer fra FORMAT.md-listene som står i fasiten eller som et av lagene predikerte. `annet:*` er samlet i kategoritabellen.

| Kategori / type | Fasit | Mønster pred. | Mønster P | Mønster G | KI pred. | KI P | KI G | Union P | Union G | Bare mønster | Bare KI |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| R relasjon / rapporterer_til | 31 | 0 | – | 0,0 % | 32 | 31,3 % | 32,3 % | 31,3 % | 32,3 % | 0 | 10 |
| R relasjon / eies_av | 13 | 0 | – | 0,0 % | 10 | 10,0 % | 7,7 % | 10,0 % | 7,7 % | 0 | 1 |
| R relasjon / ledes_av | 12 | 0 | – | 0,0 % | 5 | 40,0 % | 16,7 % | 40,0 % | 16,7 % | 0 | 2 |
| R relasjon / radgir | 10 | 0 | – | 0,0 % | 8 | 25,0 % | 20,0 % | 25,0 % | 20,0 % | 0 | 2 |
| R relasjon / har_delegert_til | 9 | 4 | 75,0 % | 33,3 % | 7 | 85,7 % | 66,7 % | 66,7 % | 66,7 % | 0 | 3 |
| R relasjon / etterfolger | 7 | 0 | – | 0,0 % | 5 | 60,0 % | 42,9 % | 60,0 % | 42,9 % | 0 | 3 |
| R relasjon / representerer | 7 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| R relasjon / administrativt_underordnet | 5 | 0 | – | 0,0 % | 5 | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0 | 0 |
| R konstituerende / oppretter | 5 | 0 | – | 0,0 % | 3 | 66,7 % | 40,0 % | 66,7 % | 40,0 % | 0 | 2 |
| R relasjon / forvaltes_av | 2 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| R konstituerende / avvikler | 1 | 0 | – | 0,0 % | 4 | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0 | 0 |
| R organsammensetning / ledes_av | 1 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| R relasjon / sekretariat_for | 1 | 0 | – | 0,0 % | 2 | 50,0 % | 100,0 % | 50,0 % | 100,0 % | 0 | 1 |
| K kompetanse / normgivningskompetanse | 208 | 198 | 92,9 % | 88,5 % | 196 | 90,8 % | 85,6 % | 90,2 % | 92,8 % | 16 | 10 |
| K kompetanse / vedtakskompetanse | 146 | 78 | 78,2 % | 41,8 % | 349 | 26,9 % | 64,4 % | 28,9 % | 74,7 % | 16 | 49 |
| K kompetanse / oppnevningskompetanse | 82 | 10 | 70,0 % | 8,5 % | 56 | 32,1 % | 22,0 % | 34,8 % | 28,0 % | 5 | 16 |
| K kompetanse / instruksjonskompetanse | 26 | 4 | 100,0 % | 15,4 % | 33 | 30,3 % | 38,5 % | 37,8 % | 53,8 % | 4 | 10 |
| K kompetanse / tilsynskompetanse | 25 | 5 | 100,0 % | 20,0 % | 24 | 54,2 % | 52,0 % | 54,5 % | 48,0 % | 0 | 8 |
| K kompetanse / klagekompetanse | 22 | 9 | 77,8 % | 31,8 % | 28 | 21,4 % | 27,3 % | 29,4 % | 45,5 % | 4 | 3 |
| K kompetanse / delegeringskompetanse | 18 | 9 | 88,9 % | 44,4 % | 22 | 31,8 % | 38,9 % | 39,3 % | 61,1 % | 4 | 3 |
| K kompetanse / overprovingskompetanse | 13 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| K kompetanse / avsettingskompetanse | 9 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| K kompetanse / opprettingskompetanse | 9 | 0 | – | 0,0 % | 3 | 66,7 % | 22,2 % | 66,7 % | 22,2 % | 0 | 2 |
| K kompetanse / godkjenningskompetanse | 8 | 7 | 85,7 % | 75,0 % | 6 | 83,3 % | 62,5 % | 75,0 % | 75,0 % | 1 | 0 |
| K kompetanse / beslutningskompetanse | 6 | 1 | 100,0 % | 16,7 % | 0 | – | 0,0 % | 100,0 % | 16,7 % | 1 | 0 |
| K kompetanse / organisasjonskompetanse | 4 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| K kompetanse / sanksjonskompetanse | 4 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| K kompetanse / omgjoringskompetanse | 3 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| K kompetanse / paleggskompetanse | 3 | 0 | – | 0,0 % | 6 | 33,3 % | 66,7 % | 33,3 % | 66,7 % | 0 | 2 |
| K kompetanse / samtykkekompetanse | 3 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| K kompetanse / avviklingskompetanse | 1 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| K kompetanse / revisjonskompetanse | 1 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| K kompetanse / samordningskompetanse | 1 | 3 | 33,3 % | 100,0 % | 0 | – | 0,0 % | 33,3 % | 100,0 % | 1 | 0 |
| P plikt / samarbeidsplikt | 21 | 9 | 77,8 % | 33,3 % | 16 | 56,3 % | 42,9 % | 56,5 % | 61,9 % | 4 | 6 |
| P plikt / betalingsplikt | 18 | 19 | 47,4 % | 50,0 % | 1 | 0,0 % | 0,0 % | 47,4 % | 50,0 % | 9 | 0 |
| P plikt / bistandsplikt | 14 | 0 | – | 0,0 % | 21 | 14,3 % | 21,4 % | 14,3 % | 21,4 % | 0 | 3 |
| P plikt / informasjonsplikt | 13 | 0 | – | 0,0 % | 2 | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0 | 0 |
| P plikt / konsultasjonsplikt | 10 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| P plikt / avtaleplikt | 3 | 3 | 66,7 % | 66,7 % | 3 | 66,7 % | 66,7 % | 50,0 % | 66,7 % | 0 | 0 |
| M medlemskap / medlem_av | 29 | 0 | – | 0,0 % | 22 | 4,5 % | 3,4 % | 4,5 % | 3,4 % | 0 | 1 |
| M medlemskap / inngar_i | 18 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| O sammensetning_omrade / bestar_av | 108 | 75 | 93,3 % | 64,8 % | 11 | 0,0 % | 0,0 % | 81,4 % | 64,8 % | 70 | 0 |
| O sammensetning_omrade / del_av | 69 | 47 | 83,0 % | 56,5 % | 0 | – | 0,0 % | 83,0 % | 56,5 % | 39 | 0 |
| A ansvarsomrade / har_ansvarsomrade | 381 | 357 | 100,0 % | 93,7 % | 35 | 0,0 % | 0,0 % | 91,1 % | 93,7 % | 357 | 0 |
| A ansvarsomrade / har_sete_i | 65 | 62 | 100,0 % | 95,4 % | 0 | – | 0,0 % | 100,0 % | 95,4 % | 62 | 0 |
| A ansvarsomrade / har_jurisdiksjon | 3 | 0 | – | 0,0 % | 16 | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0 | 0 |
| G organsammensetning / har_medlemmer | 20 | 6 | 66,7 % | 20,0 % | 16 | 18,8 % | 15,0 % | 18,2 % | 20,0 % | 1 | 0 |
| G relasjon / del_av | 12 | 0 | – | 0,0 % | 19 | 0,0 % | 0,0 % | 0,0 % | 0,0 % | 0 | 0 |
| G organsammensetning / har_organ | 10 | 0 | – | 0,0 % | 6 | 33,3 % | 20,0 % | 33,3 % | 20,0 % | 0 | 2 |
| G organsammensetning / settes_med | 3 | 0 | – | 0,0 % | 0 | – | 0,0 % | – | 0,0 % | 0 | 0 |
| T konstituerende / skal_finnes | 19 | 0 | – | 0,0 % | 14 | 14,3 % | 10,5 % | 14,3 % | 10,5 % | 0 | 2 |

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

## Kastede rader i valideringen (272 av 1314 rader KI-en svarte med)

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

## KI: falske positive (657)

| Kategori | Feil/manglende aktør | Ikke i fasiten |
|---|---:|---:|
| R | 12 | 42 |
| K | 55 | 333 |
| P | 8 | 21 |
| M | 2 | 19 |
| O | 9 | 2 |
| A | 4 | 47 |
| G | 5 | 31 |
| T | 3 | 9 |
| annet | 0 | 55 |

Eksempler (én fra hver kategori i tur, inntil 20):

| # | Kilde | eId | Type | Årsak | fra → til | Sitat |
|---|---|---|---|---|---|---|
| 1 | domstolloven | lov/1915/08/13/5/nor/§22/ledd-1 | rapporterer_til | ikke i fasiten | Kongen → Stortinget | Endringer i rettskretsene skal forelegges for Stortinget. |
| 2 | domstolloven | lov/1915/08/13/5/nor/§21/ledd-2 | vedtakskompetanse | ikke i fasiten | domstolens leder → null | I vidløftige saker kan domstolens leder bestemme at en varadommer skal følge forhandlingen og tre inn dersom dommeren får forfall. |
| 3 | domstolloven | lov/1915/08/13/5/nor/§33b/ledd-2 | bistandsplikt | ikke i fasiten | direktøren → styret | For lederstillinger utenom stillingen som domstoladministrasjonens direktør, avgir direktøren forslag. |
| 4 | domstolloven | lov/1915/08/13/5/nor/§55a/ledd-4 | medlem_av | ikke i fasiten | Direktøren for domstoladministrasjonen → Innstillingsrådet | Direktøren for domstoladministrasjonen eller den direktøren bemyndiger, har møterett i Innstillingsrådet. |
| 5 | domstolloven | forskrift/2021/01/22/163/nor/§10/ledd-1 | bestar_av | feil/manglende aktør | null → Lagsogn | Landet deles inn i lagdømmer som består av flere lagsogn. Hvert lagdømme har en lagmannsrett som er ankeinstans for flere rettskretser. |
| 6 | domstolloven | lov/1915/08/13/5/nor/§67/ledd-1 | har_ansvarsomrade | ikke i fasiten | kommunen → null | Kommunen skal oppfordre allmennheten til å foreslå kandidater til valget. |
| 7 | domstolloven | lov/1915/08/13/5/nor/§3/ledd-1 | har_medlemmer | feil/manglende aktør | Høyesterett → justitiarius | Retten skal ha en justitiarius og nitten andre dommere. |
| 8 | domstolloven | lov/1915/08/13/5/nor/§23/ledd-1 | skal_finnes | ikke i fasiten | domstoladministrasjonen → null | I de domssogn, hvor domstoladministrasjonen finner det påkrevet, skal dommerfullmektiger ansettes. |
| 9 | domstolloven | lov/1915/08/13/5/nor/§1/ledd-2 | annet:har_begrenset_domsmyndighet | ikke i fasiten | Forliksrådene → null | Forliksrådene er meklingsinstitusjoner med begrenset domsmyndighet |
| 10 | domstolloven | lov/1915/08/13/5/nor/§25/ledd-1 | rapporterer_til | ikke i fasiten | Kongen → Stortinget | Endringer i de faste rettsstedene skal forelegges for Stortinget. |
| 11 | domstolloven | lov/1915/08/13/5/nor/§21/ledd-2 | vedtakskompetanse | ikke i fasiten | domstolen → null | Når retten settes med en dommer og en varadommer og det bare er en fast dommer ved domstolen, tilkaller domstolen en dommer etter reglene i domstolloven § 19 an … |
| 12 | domstolloven | lov/1915/08/13/5/nor/§33b/ledd-2 | bistandsplikt | ikke i fasiten | innstillingsråd → styret | For andre stillinger avgis innstillingen fra et innstillingsråd etter tjenestemannsloven. |
| 13 | domstolloven | lov/1915/08/13/5/nor/§66/ledd-1 | medlem_av | ikke i fasiten | kommunestyret → utvalgene av meddommere | Medlemmene til utvalgene av meddommere velges av kommunestyret selv hvert fjerde år. |
| 14 | domstolloven | forskrift/2021/01/22/163/nor/§11/ledd-1 | bestar_av | feil/manglende aktør | Hålogaland lagdømme → null | Lagsognene Nordland, Romsa/Troms og Finnmárku/Finnmark utgjør Hålogaland lagdømme. |
| 15 | domstolloven | lov/1915/08/13/5/nor/§69/ledd-1 | har_ansvarsomrade | ikke i fasiten | kommunen → null | Fortegnelse over de valgte meddommere føres av kommunen. |
| 16 | domstolloven | lov/1915/08/13/5/nor/§27/ledd-2 | har_medlemmer | feil/manglende aktør | forliksråd → null | Forliksrådet skal ha tre medlemmer og like mange varamedlemmer. |
| 17 | domstolloven | lov/1915/08/13/5/nor/§27/ledd-1 | skal_finnes | feil/manglende aktør | kommune → null | I hver kommune skal det være et forliksråd. |
| 18 | domstolloven | lov/1915/08/13/5/nor/§2/ledd-2 | annet:gjelder_lov | ikke i fasiten | jordskifterettene → null | For de domstoler som er nevnt under nr. 1-4, gjelder denne lov |
| 19 | domstolloven | lov/1915/08/13/5/nor/§33c/ledd-1 | rapporterer_til | ikke i fasiten | domstoladministrasjonen → departementet | Domstoladministrasjonen fremmer forslag til budsjett for domstolene for departementet. |
| 20 | domstolloven | lov/1915/08/13/5/nor/§22/ledd-1 | vedtakskompetanse | ikke i fasiten | Kongen → null | Rikets inndeling i domssogn for tingrettene (rettskretser) bestemmes av Kongen. |

## Falske negative per type (fasitutsagn ingen / bare ett lag fant)

| Kategori / type | Fasit | Ikke funnet av KI | Ikke funnet av mønster | Ikke funnet av noen |
|---|---:|---:|---:|---:|
| A ansvarsomrade / har_ansvarsomrade | 381 | 381 | 24 | 24 |
| K kompetanse / normgivningskompetanse | 208 | 30 | 24 | 15 |
| K kompetanse / vedtakskompetanse | 146 | 52 | 85 | 37 |
| O sammensetning_omrade / bestar_av | 108 | 108 | 38 | 38 |
| K kompetanse / oppnevningskompetanse | 82 | 64 | 75 | 59 |
| O sammensetning_omrade / del_av | 69 | 69 | 30 | 30 |
| A ansvarsomrade / har_sete_i | 65 | 65 | 3 | 3 |
| R relasjon / rapporterer_til | 31 | 21 | 31 | 21 |
| M medlemskap / medlem_av | 29 | 28 | 29 | 28 |
| annet ansvarsomrade / annet:sogner_til | 28 | 28 | 0 | 0 |
| K kompetanse / instruksjonskompetanse | 26 | 16 | 22 | 12 |
| K kompetanse / tilsynskompetanse | 25 | 12 | 20 | 13 |
| K kompetanse / klagekompetanse | 22 | 16 | 15 | 12 |
| P plikt / samarbeidsplikt | 21 | 12 | 14 | 8 |
| G organsammensetning / har_medlemmer | 20 | 17 | 16 | 16 |
| T konstituerende / skal_finnes | 19 | 17 | 19 | 17 |
| P plikt / betalingsplikt | 18 | 18 | 9 | 9 |
| K kompetanse / delegeringskompetanse | 18 | 11 | 10 | 7 |
| M medlemskap / inngar_i | 18 | 18 | 18 | 18 |
| P plikt / bistandsplikt | 14 | 11 | 14 | 11 |
| R relasjon / eies_av | 13 | 12 | 13 | 12 |
| P plikt / informasjonsplikt | 13 | 13 | 13 | 13 |
| K kompetanse / overprovingskompetanse | 13 | 13 | 13 | 13 |
| G relasjon / del_av | 12 | 12 | 12 | 12 |
| R relasjon / ledes_av | 12 | 10 | 12 | 10 |
| G organsammensetning / har_organ | 10 | 8 | 10 | 8 |
| P plikt / konsultasjonsplikt | 10 | 10 | 10 | 10 |
| R relasjon / radgir | 10 | 8 | 10 | 8 |
| annet kompetanse / annet:forkynningskompetanse | 9 | 9 | 9 | 9 |
| annet medlemskap / annet:klasse_definert_ved_tjenestekrets | 9 | 9 | 9 | 9 |
| … 173 typer til | 337 | | | |

