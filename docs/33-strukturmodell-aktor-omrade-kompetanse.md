# 33. Strukturmodell for forvaltningen: aktør, område, kompetanse

**Status:** SPESIFIKASJON (2026-10-07) · **Gjelder:** virksomhetskatalogen, gruppe/rolle, relasjoner,
og automatisk konvertering av rettskildetekst til strukturdata · **Bygger på:** `docs/20`, `docs/29`,
`docs/32` · **Måledata:** `data/fasit/strukturmodell/` + `src/RegelIde.Data.Tests/Strukturfasit/`

## 0. Hvorfor dette dokumentet finnes

Johann, 2026-10-07: «hvis man ikke har kontroll på hvem som gjør hva og hvem som har ansvaret så gir
det ingen mening å gå videre på å identifisere tjenester, handlinger eller regler. […] prioriteten er å
bevise at modellene er robuste nok til å modellere et utvalg av rettskilder.»

Utgangspunktet var et forslag til konseptuell modell for forvaltning og rettslige subjekter (juridiske
subjekter, organer, organisatoriske enheter, roller, personer, geografiske områder, virkeområder,
seks relasjonskategorier, og skillet struktur/normativitet). Dette dokumentet

1. **måler** hvilke strukturutsagn rettskildene faktisk inneholder (§1–§2),
2. **tester** dagens modell og en revidert modell mot en fasit for fem rettskilder (§3, §6),
3. **spesifiserer** den reviderte modellen (§4) og den automatiske konverteringen (§5),
4. og deler arbeidet i byggbare saker (§8).

Prinsippet fra `docs/32` §1 gjelder uendret: **strukturering skal skje uten gjetting.** Alt i §4–§5 er
utformet slik at det maskinen ikke vet, forblir synlig ukjent.

## 1. Korpusmåling — hvilke relasjoner står faktisk i teksten?

Målt 2026-10-07 mot den lokale basen: 757 gjeldende lover + 5110 gjeldende forskrifter (kun
`rettskilder.entitetsstatus = 'gjeldende'` — se §7.5 om erstattede versjoner), 407 516 tekstnoder.
For hver relasjonstype: regex-treff × presisjon målt ved manuell lesing av 15 tilfeldige treff
(10 lov + 5 forskrift). Tallene er størrelsesordener — utvalgene er små, og formuleringer mønsteret
ikke fanger er ikke telt.

| Relasjon (forslagets liste) | Treff (noder) | Presisjon | ≈ reelle | Merknad |
|---|---|---|---|---|
| klageinstans for | 1718 | 67 % | 1150 | Sterkest. Mottaker ofte navngitt, førsteinstans implisitt |
| delegerer til | 2875 | 47 % | 1350 | Nesten alltid avgrenset til paragraf |
| instruere | 908 | 27 % | 250 | Nesten bare **negativ** («kan ikke instruere») = uavhengighet |
| omgjøre | 454 | 38 % | 170 | Ofte negativ |
| administrativt underordnet | 775 | 13 % | 100 | Signal kun i frasen «administrativt underordnet» |
| styrer / ledes av | 649 | 13 % | 85 | |
| sekretariat for | 375 | 20–40 % | 75–150 | |
| eier | 631 | 7 % | 45 | «eies av staten» |
| oppretter (organ) | 81 | 36 % | 30 | De fleste organer opprettes ved kgl.res. — utenfor korpus |
| avvikler | 305 | 7 % | 20 | |
| samarbeider med / bistår | 966 / 642 | 40 / 13 % | 390 / 85 | Oppgavebundet, generisk motpart → handlingslag |
| tilsyn med *aktør* | 2246 | ~0 % | ≈ 0 | Tilsyn er nesten alltid «med at regelverket følges» |
| inngår i (organ) | 2105 | 0 % | ≈ 0 | |
| leverer tjenester til | 176 | 0 % | 0 | |
| virkeområde (ordet) | 560 | 7 % | 40 | Betyr nesten alltid *lovens* anvendelsesområde |

**Ikke i forslagets liste, men dominerende:**

| Utsagn | Treff | Presisjon | ≈ reelle | Form |
|---|---|---|---|---|
| kan gi forskrift om … | 5651 | 100 % | 5650 | aktør → tema/bestemmelse |
| treffer vedtak / avgjøres av | 1465 | 73 % | 1070 | aktør → sakstype |
| fører tilsyn med at … | 2246 | ~33 % | 740 | aktør → regelverk |
| oppnevner / utnevner | 1422 | 33 % | 470 | aktør → aktør |
| består av N medlemmer | 147 | 100 % | 150 | organsammensetning |
| rapporterer til | 296 | 45 % | 130 | aktør → aktør |
| gir råd til | 193 | 33 % | 65 | aktør → aktør |

**Konklusjon §1:** lovteksten uttrykker struktur i hovedsak som **kompetanse knyttet til en
bestemmelse** (hvem kan gi forskrift / treffe vedtak / behandle klage / føre tilsyn etter hvilken
paragraf), ikke som kanter mellom aktører. Kanter mellom aktører finnes, men er få, og nesten alle er
**avgrenset** (gjelder visse vedtak/paragrafer) — 85–90 % i utvalget.

## 2. Fasit — fem rettskilder annotert fullt

Fem rettskilder med ulik form, hver med de forskriftene som definerer områdene deres. Alle noder lest
(ikke stikkprøver), hvert utsagn med eksakt sitat og eId, sitatene maskinelt verifisert som
delstrenger av nodeteksten. Formatet: `data/fasit/strukturmodell/FORMAT.md`.

| Kilde (+ ledsagende) | Tekstnoder | Aktører | Utsagn | Tester særlig |
|---|---|---|---|---|
| Sameloven (+ forvaltningsområde-, språkregel-, delegerings-, overføringsforskrift) | 144 | 162 | 285 | områder, klasser av kommuner, valgkretser |
| Energiloven (+ vedtaksmyndighet-, delegerings-, overføringsvedtak) | 339 | 52 | 254 | kompetanse, delegering med unntak, uavhengighet |
| Helse- og omsorgstjenesteloven | 285 | 63 | 192 | organ/funksjon inne i kommunen (#300), statsforvalter |
| Spesialisthelsetjenesteloven + helseforetaksloven (+ refusjonsforskrift) | 378 | 70 | 302 | eierskap, foretaksorganer, helseregion utenfor korpus |
| Domstolloven (+ inndelingsforskrift, to delegeringer) | 726 | 613 | 832 | domssogn/lagdømme, uavhengighet, anke |

**Fasiten er KI-annotert og ikke menneskelig verifisert.** Den er laget for å måle, og målingene er
bare så gode som fasiten. Før terskler låses (§5.4) skal et stratifisert utvalg gjennomgås av et
menneske (sak i §8).

## 3. Funn fra fasiten — hva modellen MÅ kunne uttrykke

1. **Kompetanse er den største kategorien utenom områdelister** (514 av 1865 utsagn; 39 % i de fire
   kildene uten domstollovens kommunelister), og typene er flere enn
   forskrift/vedtak/klage/tilsyn: godkjenning, iverksetting, ikraftsetting, overprøving, utpeking,
   oppnevning, begjæring, samtykke, normering. → Kompetansetype må være **konfigurerbar**, ikke lukket.
2. **Generiske omtaler er vanlige.** Av 960 aktører har 701 en referent teksten selv avgjør (de fleste
   er navngitte kommuner/områder i inndelingslister). Resten avgjøres av **saksforhold** (160,
   «pasientens bostedsregion»), **område** (41, «Statsforvalteren … i sitt område»), **forskrift utenfor
   korpus** (28), lovens departement (5), foregående ledd (5), eller er ukjente (20). Utenom
   domstollovens inndelingslister er under halvparten av aktørene entydige fra teksten alene.
3. **Gruppe er ikke én ting.** Det som i dag heter `gruppe` er i fasiten fordelt på klasse (72),
   rolle (54), område (85) og organ (68). Sameloven viser at forvaltningsområdet og språkkategoriene
   er **både** klasse og område. Domstolloven viser det motsatte problemet: område og organ har samme
   navn («Agder» = fylke, lagsogn, tingrett og lagdømme), og rettskretsen har ikke eget navn — de brukes om kommunen som rettssubjekt og om kommunen som territorium
   i samme lov.
4. **Utsagn på klassenivå.** «Hver kommune skal ha …», «kommunen skal …» gjelder hvert medlem av en
   klasse for seg (distributivt). Modellen må kunne si «klassen K har organ/rolle R» og la det slå ut
   på hvert medlem — uten å liste 357 rader.
5. **Organer og funksjoner inne i ett rettssubjekt** (kommunestyret, koordinerende enhet,
   foretaksmøtet, styret) — bekrefter #300. «Kommunestyret selv» = kompetanse som ikke kan delegeres.
6. **Polaritet og unntak.** 57 negative utsagn (uavhengighet, ikke-delegerbar, «kan ikke omgjøre»).
   Energiloven har delegering med unntak *og* unntak fra unntakene.
7. **Rollenavn har ulik innehaver per paragraf** («beredskapsmyndigheten» = NVE, unntatt § 9-1 siste
   ledd; «departementet» i § 4-4 annet ledd = et annet departement). Avgrensning til paragraf/ledd er
   påkrevd på all tildeling — i dag finnes den på `Myndighetstildeling`, ikke på `VirksomhetRelasjon`.
8. **Kilder utenfor korpus.** 167 utsagn viser til noe som fastsettes utenfor teksten: helseregionene
   (vedtekter), statsforvalternes embetsområder (kgl.res.), RME/Energiklagenemnda (forskrift ikke i
   utvalget). Modellen må kunne ha en hjemmel som ikke er en Lovdata-node.
9. **Områder har intern struktur** som ikke er en flat liste: valgkretser definert ved komplement
   («kommunene som ikke tilhører valgkrets 6»), retning («fra og med Saltdal og nordover»),
   tre nivåer (rettskrets → lagsogn → lagdømme), en kommune delt mellom flere domssogn (domstolloven
   § 66), samme kommunenavn to ganger (Herøy, Våler) og flerspråklige navn i ulik rekkefølge —
   kommuner trenger kanonisk identitet (kommunenummer innen inndeling), ikke navn. Og
   inndelinger som endres over tid (kommunenummer er ikke stabilt, `docs/15` §3.3).

## 4. Revidert modell

### 4.1 Noder — de ekte begrepene som typer

| Nodetype | Eksempel | Bor i | Merknad |
|---|---|---|---|
| `rettssubjekt` | Staten, Oslo kommune, Helse Nord RHF | `Virksomhet` | |
| `organ` | Stortinget, Kongen i statsråd, departement, kommunestyret i Oslo, Energiklagenemnda | `Virksomhet` (orgnr valgfritt) | Organ med `del_av` → rettssubjekt |
| `organisatorisk_enhet` | RME (enhet i NVE), avdeling, koordinerende enhet | `Virksomhet` | |
| `rolle` | reguleringsmyndighet, kommunelege, beredskapsmyndigheten | `Begrep` | Innehas av en aktør, alltid avgrenset |
| `klasse` | kommunene, språkutviklingskommuner, forvaltningsorgan | `Begrep` | Ekstensjonal (listet med hjemmel) eller intensjonal (kriterium) |
| `omrade` | Troms, Karasjok (territorium), Agder tingretts rettskrets, forvaltningsområdet for samiske språk | `Begrep` | Gyldighetsperiode; kode (kommune-/fylkesnr) som attributt, aldri identitet |
| `person` | — | — | Utsatt: ingen av de fem kildene navngir personer |

Aktørtypen (`rettssubjekt`/`organ`/`organisatorisk_enhet`) legges som felt på `Virksomhet`, **NULL =
uavklart** — settes automatisk bare der det er entydig (Brreg `KOMM`/`FYLK` → rettssubjekt, samme
regel som `docs/20` §7.2 for forvaltningsnivå), ellers av et menneske. `Virksomhet` blir dermed «aktør»
i ordets juridiske forstand; registeret beskriver den, loven definerer den (CLAUDE.md §0).

**[Bygget, issue #310, 2026-10-07]** `Virksomhet.Aktortype` og `Begrepskategori` ∈ `klasse`/`rolle`/
`omrade` finnes nå (migrasjonen `InnforNodetypeakse`), og de 13 gruppebegrepene er reklassifisert etter
Johanns godkjente liste. Ett avvik fra tabellen over: tre godkjente «organ»-rader (Kongen i statsråd,
med «kongen» slått inn; «stortinget» i reindriftsloven) har ingen `Virksomhet`-rad å bo i — Stortinget
(orgnr 971524960) finnes ikke i katalogen lokalt, og en rad med gjettede data opprettes ikke. De ligger
derfor som `Begrepskategori = 'organ'` til noen oppretter virksomheten (migrasjonen gjør dem til
navneform automatisk der Stortinget-virksomheten alt finnes). Statsforvalter-radene er slått sammen til
én fast, nasjonal klasse.

### 4.2 Gruppe er en evne, ikke en type

Svar på Johanns spørsmål (2026-10-07): «er det bedre å ha grupper med en attributt som skiller ulike
grupper, eller modellere med de faktiske begrepene og legge til gruppefunksjonalitet på alle?»

**De faktiske begrepene som typer, og gruppefunksjonaliteten bygget ÉN gang som en felles kant.**

- Ikke «gruppe med type-attributt» — det er dagens tilstand, og fasiten viser hva det gir: én kasse der
  område, klasse, rolle og organ ligger om hverandre og typen er tapt.
- Ikke egen gruppemekanikk per type — `Myndighetstildeling` og `GruppeMedlemskap` har allerede
  identiske felt (koden sier selv at de er «to nivåer i det samme hierarkiet»).

Tre slags tilhørighet ser like ut, men traverseres ulikt — derfor er kanttypen bærende:

| Tilhørighet | Kant | Arves? | Eksempel |
|---|---|---|---|
| medlem av klasse | `M medlem_av` | Ja — det som gjelder klassen gjelder medlemmet (S6) | Karasjok kommune ∈ språkforvaltningskommuner |
| områdesammensetning | `O bestar_av` | Ja, geografisk | Agder tingretts rettskrets = {kommuner} |
| rolle/kompetanse | `I innehar` / `K` | **Nei** — gjelder bare avgrensningen | NVE innehar «konsesjonsmyndigheten» etter § 3-1 |

Statsforvalter-eksempelet (avklart med Johann 2026-10-07): gruppen «statsforvalter» defineres én gang
som fast, nasjonalt begrep (#298) med ti medlemmer; hver omtale i teksten tagges mot gruppen. For
omtaler som er **distributive** («kommunen skal …») er det nok. For omtaler som **parer to grupper**
(«Statsforvalteren er klageinstans for kommunens vedtak») velger området paret: medlemskapet får en
`A har_ansvarsomrade`-kant (Statsforvalteren i Troms og Finnmark → Troms + Finnmark), og oppslaget blir
tagg → gruppe → medlemmer → det medlemmet hvis område dekker saken.

### 4.3 Kanter — én tabell, typestyrt

Alle strukturutsagn lagres som én kanttype med kategori + konfigurerbar typekode (samme drift som
`RelasjonsTypeKonfigurasjon` i dag — nye typer er rader, ikke kode):

| Kat. | Fra → Til | Typekoder (startsett, utvidbart) |
|---|---|---|
| **R** relasjon | aktør → aktør | klageinstans_for, administrativt_underordnet, instruksjon, omgjoring, sekretariat_for, rapporterer_til, oppnevner, velger, ledes_av, eies_av, etterfolger, radgir, delegerer_til, representerer, ankeinstans_for, oppretter, avvikler |
| **K** kompetanse | aktør/rolle → bestemmelse eller sakstype | forskrift, vedtak, klage, tilsyn, delegering, oppnevning, instruksjon, utpeking, godkjenning, iverksetting, overproving, … |
| **M** medlemskap | aktør/klasse/område → klasse | medlem_av |
| **O** områdesammensetning | område → område | bestar_av |
| **A** ansvarsområde | aktør → område | har_ansvarsomrade, har_jurisdiksjon, har_sete_i, valgkrets_for |
| **G** organtilhørighet | organ/enhet/rolle → rettssubjekt | har_organ, del_av, har_medlemmer |
| **I** rolleinnehav | aktør → rolle | innehar (= dagens `Myndighetstildeling` når målet er en rolle) |
| **T** klassenivå | klasse → rolle/organ-type | skal_ha (distributivt: hvert medlem av klassen har …) |

Felles egenskaper på **alle** kanter:

- `HjemmelRettskildeId` + `HjemmelEid` — påkrevd, **eller** `KildeUtenforKorpus` (fritekst +
  lenke) når hjemmelen er kgl.res./vedtekter/instruks (funn 8).
- `AvgrensningParagrafspennJson` (samme strukturerte format som i dag, `docs/20` §7.1) og
  `AvgrensningTekst` (sakstype, «bare ugyldige vedtak») — funn 7.
- `Polaritet` (`positiv`|`negativ`) — funn 6. «Kommunestyret selv» = K med `delegerbar = false`.
- `GyldigFra`/`GyldigTil`, `Status` (`foreslatt_av_ai`|`validert`, `docs/20` §2.7), `OppdagelsesKilde`
  (`manuell` | `monster:<id>` | `ki:<modell>`) — proveniens for automatisk konvertering (§5).

### 4.4 Bevisst ikke i strukturlaget (forslagets punkt 9)

11 % av fasit-utsagnene hører til et senere lag og lagres ikke som strukturkanter: samarbeid,
bistand, informasjonsdeling, konsultasjon, saksforberedelse, finansiering/betalingsansvar,
møteplikt. De står fortsatt i fasiten, så de kan måles når det laget bygges.

## 5. Automatisk konvertering

### 5.1 Kontrakten er fasit-formatet

Konverteringen — deterministisk eller KI — produserer **nøyaktig samme JSON-format som fasiten**
(`data/fasit/strukturmodell/FORMAT.md`). Det gir tre ting gratis: (1) konverteringen kan måles mot
fasiten uten database, (2) mønster- og KI-laget kan sammenlignes på like vilkår, (3) lagring er et
eget, senere steg som ikke bestemmer hvordan uttrekket virker.

### 5.2 To lag

1. **Deterministisk mønsterlag** — bare mønstre med målt høy presisjon (§1): «kan gi forskrift(er)
   om», «treffer vedtak / avgjøres av», «er klageinstans for / påklages til», «administrativt
   underordnet», «består av N medlemmer», «oppnevnes av», «kan ikke instruere», og strukturerte
   lister av kommuner i inndelingsforskrifter. Navn løses kun ved eksakt treff mot navneform/område
   (`VirksomhetOppslagTjeneste`, ingen fuzzy) — ellers `referent = null`.

   **Bygget i #307 (2026-10-07):** `src/RegelIde.Data/Strukturkonvertering/` —
   `IStrukturkonverterer` (noder inn → fasit-formatet ut) og `MonsterStrukturkonverterer` med 30
   navngitte mønstre (`monster:<id>`, stabil id = senere `OppdagelsesKilde`). I tillegg til listen
   over: `vedtak-forvaltningsverb` (gi pålegg/dispensasjon, ilegge, trekke tilbake, fastsette
   vilkår), `vedtak-godkjennes-av`, `tilsyn-forer-tilsyn` (bare tilsyn med at *regelverk* følges,
   jf. §1), `har-sete-i`, og fra inndelingslistene også `O del_av` (kommune → fylke) og `A
   har_sete_i` (tingrett → rettssted). Aktører identifiseres i dette laget **bare ved tekstform** —
   oppslaget mot katalog/område i avsnittet over hører til #313/#314. Endepunkter setningen ikke
   avgjør (førsteinstansen i «… påklages til klagenemnda», «til disse») blir `null`; aktørfelt som
   krever skjønn (entitetstype, navngitt, referent, oppløsning) står `null`. Utsagnene har feltet
   `oppdagelseskilde` (tillegg til FORMAT.md, utelatt i fasiten). Prøvde og forkastede former står
   med begrunnelse nederst i målerapporten.
2. **KI-lag** — samme systeminstruks-mønster som `VirksomhetOgGruppeKiOppdagelseTjeneste` (#285),
   med fasit-formatet som svarskjema. Svar valideres hardt: sitat må være eksakt delstreng av noden,
   eId må finnes, ukjente typekoder avvises. Ugyldige rader kastes og telles — aldri «repareres».

### 5.3 Til databasen bare som forslag

Uttrekket skrives aldri som `validert`. Det går inn som `foreslatt_av_ai` (eller `monster`-proveniens)
i forslagskøen for strukturkanter, og et menneske godkjenner — samme prinsipp som `docs/20` §2.7.

### 5.4 Måling

`src/RegelIde.Data.Tests/Strukturfasit/` kjører konverteringen over de lagrede nodetekstene for de
fem kildene og regner presisjon/gjenfinning per kategori mot fasiten. Treff = samme eId + kategori +
type (+ samme fra/til-tekstform når de finnes). Mønsterlaget kjøres i vanlig testkjøring (ingen
nettverk); KI-laget er gated på samme måte som andre live-tester. Terskler låses først etter
menneskelig gjennomgang av fasiten.

**Målt 2026-10-07 (#307, mønsterlaget)** — full rapport med per-mønster-tall, de 20 vanligste
falske positive/negative og forkastede mønstre: `data/fasit/strukturmodell/maling-monster.md`
(regenereres av testen). «Samme tekstform» er tolket som at aktørenes former (tekstform ∪
varianter) har minst én felles skrivemåte, uten skille på store/små bokstaver; én-til-én-treff.

| Kanttype | Fasit | Predikert | Presisjon | Gjenfinning | Presisjon uten endepunktkrav |
|---|---:|---:|---:|---:|---:|
| R relasjon | 223 | 36 | 77,8 % | 12,6 % | 94,4 % |
| K kompetanse | 427 | 288 | 88,5 % | 59,7 % | 88,5 % |
| — herav forskriftskompetanse | 205 | 198 | **91,9 %** | 88,8 % | 91,9 % |
| — herav vedtakskompetanse | 158 | 85 | 80,0 % | 43,0 % | 80,0 % |
| M medlemskap | 48 | 0 | – | 0 % | – |
| O områdesammensetning | 578 | 509 | 97,4 % | 85,8 % | 99,6 % |
| A ansvarsområde | 120 | 62 | 100 % | 51,7 % | 100 % |
| G organtilhørighet | 43 | 6 | 66,7 % | 9,3 % | 100 % |
| I / T | 0 / 21 | 0 | – | 0 % | – |
| `annet:*` + senere lag | 405 | 0 | – | 0 % | – |
| **Alle** | **1865** | **901** | **93,8 %** | **45,3 %** | 95,9 % |

| Kilde | Presisjon | Gjenfinning |
|---|---:|---:|
| Domstolloven | 95,7 % | 62,3 % |
| Energiloven | 94,7 % | 42,5 % |
| Helse- og omsorgstjenesteloven | 91,8 % | 23,4 % |
| Sameloven | 87,7 % | 40,0 % |
| Spesialisthelsetjenesteloven + helseforetaksloven | 89,6 % | 19,9 % |

Lesning: mønsterlaget er presist der lovteksten har fast form — inndelingslistene (O/A) og
forskriftskompetanse — og finner lite av R/M/T, der formuleringene varierer og endepunktet ofte står
i en annen setning. Forskjellen mellom presisjon med og uten endepunktkrav (R 78 → 94 %, G 67 →
100 %) er aktører mønsteret ikke kan avgjøre fra setningen alene — nettopp det KI-laget (#308) og
oppløsningen (#313/#314) skal måles på. Gjenfinningen er lavest i de to helselovene, som uttrykker
struktur gjennom plikter og saksforhold heller enn faste kompetanseformler.

Terskler i testen: K forskriftskompetanse presisjon ≥ 0,9 (#307); øvrige kanttyper målt verdi − 5
prosentpoeng som regresjonsvern. **Foreløpige** — låses etter fasitgjennomgangen i #309, fordi en
del av de falske positive er sannsynlige fasitutelatelser (f.eks. kommune → fylke i samelovens
§ 2-4 punkt 6, «Kongen kan gi forskrift om at …» klassifisert som annen kompetansetype).

### 5.5 Konvertering av eksisterende data

| Fra | Til | Automatisk? |
|---|---|---|
| `VirksomhetRelasjon` (10 rader lokalt) | R-kanter, samme typekode | Ja — 1:1, ingen tap |
| `GruppeMedlemskap` (3) | M-kanter | Ja — 1:1 |
| `Myndighetstildeling` (16) | M eller I, avhengig av målets nye nodetype | **Nei** før de 13 gruppebegrepene er reklassifisert |
| `Begrep(gruppe)` (13) | klasse / rolle / område / organ | **Nei** — må avgjøres av et menneske (liste i sak) |
| `Begrep(administrativ_inndeling)` (0) | område | Ja |
| Kommuner/fylker | område-noder fra Kartverket kommuneinfo (15 fylker, 357 kommuner) + `O bestar_av` + `A har_ansvarsomrade` kommune→eget territorium | Ja — entydig nøkkel (kommunenummer innen gyldig inndeling) |

## 6. Designtest — kan modellen uttrykke fasiten?

`designtest.py` (lagt ved under `data/fasit/strukturmodell/`) klassifiserer hvert utsagn: kan dagens
modell / den reviderte modellen lagre det? «Delvis» for revidert betyr at utsagnet kan lagres, men
ikke avgjøres uten regelevaluering (intensjonale klasser, bostedsregion, komplement).

| Kilde | Strukturutsagn | Dagens: ja | Dagens: delvis | Revidert: ja |
|---|---|---|---|---|
| Sameloven | 269 | 10 % | 19 % | 93 % |
| Energiloven | 229 | 1 % | 81 % | 100 % |
| Helse- og omsorgstjenesteloven | 156 | 6 % | 63 % | 100 % |
| Spesialisthelsetjenesteloven + helseforetaksloven | 268 | 4 % | 38 % | 99 % |
| Domstolloven | 807 | 2 % | 15 % | 100 % |
| **Alle 1865 utsagn** | — | **3 %** | **30 %** | **92 %** (+ 1 % delvis, 7 % senere lag) |

Dagens «delvis» er nesten bare kompetanse, som kan presses inn i `Myndighetstildeling` via et
gruppebegrep («forskriftsmyndighet etter § X»), men uten at kompetansetypen blir spørrbar. Revidert
«ja» fordeler seg slik på kanttypene: O 578, K 514, R 266, A 176, M 65, G 61, T 21, I 14.

**Forbehold:** «ja» for revidert modell betyr at utsagnet har en plass med de egenskapene det trenger
— ikke at modellen er bevist. Beviset er at strukturen for de fem kildene faktisk lastes inn og at
spørsmålene i §6.1 besvares fra data. Det er akseptansekriteriet for epic-saken.

### 6.1 Spørsmålene beviset måles mot

Tommelfingerreglene fra forslaget, som spørringer mot de fem kildene:

1. Hvilke rettssubjekter finnes? 2. Hvilke organer tilhører et rettssubjekt? 3. Hvilke
organisatoriske enheter finnes? 4. Hvilke roller finnes? 5. Hvilke geografiske/jurisdiksjonelle
områder finnes? 6. Hvilke relasjoner finnes mellom disse? 7. Hvem er underlagt hvem? 8. Hvem fører
tilsyn med hvem (og med hvilket regelverk)? 9. Hvem er klageinstans for hvem? 10. Hvilke aktører har
ansvarsområde i hvilke områder? 11. Hvilke aktører har myndighet innenfor hvilke områder?

Pluss to som følger av funnene: 12. Hvem har hvilken kompetanse etter hvilken paragraf (S1)?
13. Gitt en kommune: hvilken statsforvalter, tingrett og helseregion gjelder for den (S6)?

## 7. Ikke løst / åpne beslutninger

1. **Konsolidering av kanttabellene** — én ny tabell som erstatter `VirksomhetRelasjon`,
   `GruppeMedlemskap` og `Myndighetstildeling` (berører KI-oppdagelsen, veiviseren, VirksomhetDetalj,
   BegrepDetalj, nettside-eksporten), eller ny tabell for de nye kategoriene og konsolidering senere.
   Prissatt i sak; Johanns valg.
2. **Reklassifisering av de 13 gruppebegrepene** — liste i sak; Johanns valg per rad.
3. **Sametinget** passer ikke rent i én nodetype (folkevalgt organ uten oppgitt rettssubjekt som
   likevel ansetter og trer inn i rettigheter). Fasiten bruker `organ` med kommentar.
4. **Intensjonale og geometriske klasser** («offentlig organ» = tjenestekrets overlapper
   forvaltningsområdet) lagres som kriterium-tekst; å *avgjøre* medlemskap krever regelevaluering
   (senere lag).
5. **Erstattede rettskildeversjoner** har noder med `entitetsstatus = 'gjeldende'` — alle korpus-
   tellinger må filtrere på rettskildens status. Egen sak.
6. **Fasiten er ikke menneskelig verifisert** (§2).

## 8. Byggerekkefølge

Se epic-saken #318 (delsaker #306–#317). Kort: (1) fasit + måleoppsett + dette notatet → (2) deterministisk
konvertering målt mot fasit → (3) KI-konvertering, samme kontrakt → (4) områderegister fra Kartverket
→ (5) nodetype-akse + reklassifisering → (6) kanttabell (etter Johanns konsolideringsvalg) →
(7) import av konverteringsresultat til forslagskø → (8) oppløsning av generiske omtaler →
(9) beviset: de fem kildene lastet, §6.1 besvart.
