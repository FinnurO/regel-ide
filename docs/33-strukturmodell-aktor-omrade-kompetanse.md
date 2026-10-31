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

En lettlest innføring i modellen for lesere uten forkunnskap ligger på nettsiden:
[finnuro.github.io/regel-ide/strukturmodell/](https://finnuro.github.io/regel-ide/strukturmodell/) (`nettside/strukturmodell/`, #325).

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
| `ordning` | Folketrygden (trygdeordning), Energifondet (fond), en tilskuddsordning | `Virksomhet` (aktørtype `ordning`, orgnr NULL) | [Ny, #353] Ikke-aktør som loven gir en funksjon — se §4.5 |
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

**[Bygget, issue #311, 2026-10-07] Organene er virksomheter.** Johanns beslutning på #311: migrasjonen
`InnforStrukturkanttabell` oppretter **Stortinget** (orgnr 971524960, aktørtype `organ`) fra et committet
Brreg-øyeblikksbilde (`src/RegelIde.Data/Seed/brreg-971524960-stortinget.json`, hentet 2026-10-07 — en
migrasjon kan ikke kalle Brreg; en test holder SQL-en og fila like) og **«Kongen i statsråd»** som organ uten
orgnr, med hjemmelen (Grunnloven) i proveniensraden for opprettelsen. De tre organ-begrepene («Kongen i
statsråd», den arkiverte «kongen», «stortinget») er navneformer for dem, taggene er flyttet til
virksomhet-laget, og `'organ'` er fjernet som begrepskategori (CHECK og nodebegrep-indeksene). Et organ-
begrep med ukjent term i et annet miljø blir `'gruppe'` (uavklart) — det gjettes ikke på en virksomhet.

**[Bygget, issue #312, 2026-10-08] Områderegisteret.** `Begrep.Omradetype` (fylke | kommune | tettsted | lagsogn |
lagdomme | helseregion | annet) og `Begrep.Omradekode`; identitet = (type, kode) blant gjeldende rader, eller (type,
term) uten kode — ikke navn (Herøy og Våler finnes to ganger, Oslo er fylke og kommune). Bare områder en kilde
NAVNGIR får node (Johanns funn på #312): ingen rettskrets-node — tingretten har `A har_ansvarsomrade` direkte til
kommunene og `A har_sete_i` til rettsstedet, som er SAMME kommunenode der rettsstedet er en kommune og et
tettsted-område i kommunen ellers; lagsogn og lagdømme er områder; statsforvalteren har kantene direkte til
fylkene; helseregionen er et område (vedtektene navngir den). Målt i lokal `regelide` 2026-10-08: 414 områder
(15 fylker, 357 kommuner, 16 tettsteder, 15 lagsogn, 6 lagdømmer, 4 helseregioner, Svalbard), 1 559 kanter (1 553 etter at de 6 navnebaserte
lagmannsrett-kantene ble slettet), 34 nye domstolvirksomheter. Oppslaget «gitt kommune X» er `GET /api/omrader/kommuner/{nr}/tilhorighet`
(`OmradeOppslagTjeneste`). Inndelingsforskriften tolkes av `DomstolinndelingTolker` via #307-mønstrene: 0 uløste
navn, 0 kommuner delt mellom domssogn (domstolloven § 66 annet ledd håndteres som «ikke entydig», ingen velges).
**[ENDRET, Johanns beslutninger 2026-10-08 før merge]** (1) Alle kanter tolkeren leser ut av forskriften (790: tingrett →
kommune 357, sete 61, lagsogn → kommune 357, lagdømme → lagsogn 15) er FORSLAG (`foreslatt_av_ai`, oppdagelseskilde
`monster:inndeling-*`) og godkjennes samlet per hjemmel (`POST /api/strukturkanter/godkjenn-alle?hjemmelRettskildeId=…`,
knapp på områdefanen, hver kant logges i Proveniens). Registerkanter og de kuraterte filene er validert — data fra en
kilde, ikke tolket lovtekst. (2) Lagmannsrett → lagdømme kobles IKKE via navn: ingen tekst i korpus parer dem (domstolloven
§ 10 første ledd gjelder lagmannsrettenes dommere; § 16 første ledd og forskriften § 10 sier bare at hvert lagdømme har én),
så de seks kantene er slettet og rubrikken er «mangler» til en kilde avgjør paret. (3) Oppslaget tar med forslag, merket.
**[ENDRET, issue #345, Johann 2026-10-08] Lagmannsrett → lagdømme.** Punkt (2) bygde på en feillesning. Hjemmelen er
inndelingsforskriften (FOR-2021-01-22-163) § 10 første ledd: «Hvert lagdømme har en lagmannsrett som er ankeinstans for
flere rettskretser.» Domstolloven § 10 var feil kandidat. De seks kantene er lagt inn igjen som FORSLAG:
- Hjemmel: eId `…/163/nor/§10/ledd-1`. Oppdagelseskilde: `monster:lagdomme-lagmannsrett-navneregel`.
- Navneregel: lagmannsretten for lagdømmet «X lagdømme» er «X lagmannsrett»
  (`DomstolinndelingTolker.ParLagmannsretter`, slått opp i Enhetsregisteret). Johann bekreftet den som regel
  2026-10-08, med hjemmel i § 10 første ledd.
- Kantene godkjennes i samme samlede godkjenning som de andre domstolkantene.
- Finnes ikke «X lagmannsrett» eksakt, lages ingen kant, og lagdømmet listes.

**[ENDRET, issue #345, Johann 2026-10-08] Hele kjeden fra forskriften, ned til kommunenivå.** «Alt skal kunne utledes
fra forskriften ned på kommunenivå.» Bare det teksten sier, lagres:
- §§ 2–9: A tingrett `har_ansvarsomrade` kommune.
- §§ 11–16, sogner-leddene: **A tingrett `sogner_til` lagsogn.** Kanten er ny, har oppdagelseskilde
  `monster:inndeling-sogner-til` og hjemmel = ledd-eId. Typekoden er den samme som i fasiten og i mønsteret
  `inndeling-sogner`. O er utelukket fordi fra-noden er en aktør, og det finnes ingen rettskrets-node.
- §§ 11–16, første ledd: O lagdømme `bestar_av` lagsogn.
- **[FJERNET]** De 357 kantene «O lagsogn `bestar_av` kommune» fra #342 er slettet gjennom tjenesten. De var utflatet
  data som teksten ikke sier.
- Kommunens lagsogn og lagdømme **avledes** i `OmradeOppslagTjeneste`: kommune ← tingrett → lagsogn → lagdømme.
  Svaret er validert bare når begge kantene i leddet er validert.

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
| **R** relasjon | aktør → aktør | [ENDRET, #341] Struktur UTEN myndighet: eies_av, ledes_av, sekretariat_for, rapporterer_til, etterfolger, representerer, har_delegert_til (gjennomført delegering). [ENDRET, #352] Avgjort som R (struktur eller hendelse, ikke myndighet): administrativt_underordnet, radgir, oppretter, avvikler. velger og ankeinstans_for er flyttet til K. [Ny, #353] forvaltes_av (ordning → organet som forvalter den) |
| **P** plikt overfor motpart ([Ny, #353]) | aktør (også ordning)/rolle/klasse → **motpart** (valgfri) | samarbeid, avtale, betaling, bistand, informasjon, konsultasjon — med `modalitet` (skal/kan/bør). Se §4.5 |
| **K** kompetanse | aktør/rolle/klasse ([ENDRET, #418]: klasse lest distributivt) → **motpart** (valgfri) og bestemmelse/sakstype | [ENDRET, #341, #352, #355] beslutning; struktur: oppretting, avvikling, organisasjon; oppnevning: oppnevning (med undertype valg/ansettelse/utpeking/oppnevning/utnevning/konstitusjon), avsetting (med undertype avsetting/oppsigelse/avskjed); styring: instruksjon, samordning, delegering, godkjenning, samtykke, palegg; normgivning (med normform); kontroll: tilsyn, revisjon (~~forelegging~~ [FJERNET, #355]); klage og overprøving: klage, omgjoring, overproving (med undertype anke), stadfesting; vedtak (med undertype tilbakekall); sanksjon |
| **F** oppgave/funksjon ([Ny, #368]) | aktør/rolle/klasse → område/klasse/rolle/aktør (valgfri), objekt PÅKREVD | forvaltning, samordning, sorge_for, oversikt, bistand, sekretariatsfunksjon, radgivning, veiledning, kontroll — med `modalitet`. Gir aldri K eller P i seg selv. Se §4.7 |
| **M** medlemskap | aktør/klasse/område → klasse | medlem_av |
| **O** områdesammensetning | område → område | bestar_av |
| **A** ansvarsområde | aktør → område | har_ansvarsomrade, har_jurisdiksjon, har_sete_i, valgkrets_for |
| **G** organtilhørighet | organ/enhet/rolle → rettssubjekt | har_organ, del_av ([#355] også fra en stilling: «Høyesteretts direktør» del_av Høyesterett), har_medlemmer (organets faste medlemmer), [Ny, #352] settes_med (sammensetningen i den enkelte sak — saksavhengig), [Ny, #353] tilhorer (ordning → rettssubjekt, bare når hjemlet) |
| **I** rolleinnehav | aktør/klasse ([ENDRET, #418]: klasse lest distributivt) → rolle | innehar (= dagens `Myndighetstildeling` når målet er en rolle) |
| **T** klassenivå | klasse → rolle/organ-type | skal_ha (distributivt: hvert medlem av klassen har …) |

Felles egenskaper på **alle** kanter:

- `HjemmelRettskildeId` + `HjemmelEid` — påkrevd, **eller** `KildeUtenforKorpus` (fritekst +
  lenke) når hjemmelen er kgl.res./vedtekter/instruks (funn 8).
- `AvgrensningParagrafspennJson` (samme strukturerte format som i dag, `docs/20` §7.1) og
  `AvgrensningTekst` (sakstype, «bare ugyldige vedtak») — funn 7.
- `Polaritet` (`positiv`|`negativ`) — funn 6. «Kommunestyret selv» = K med `delegerbar = false` ([Bygget, #341] — se under;
  [ENDRET, #367] nå `delegeringsadgang = uttrykkelig_forbudt`, §4.6).
- `GyldigFra`/`GyldigTil`, `Status` (`foreslatt_av_ai`|`validert`, `docs/20` §2.7), `OppdagelsesKilde`
  (`manuell` | `monster:<id>` | `ki:<modell>` | [Ny, #387] `fasit:<fil>:<id>`) — proveniens for automatisk konvertering (§5) og for
  utsagn lastet fra fasiten (§5.12).
- **[Ny, Johanns beslutninger 2026-10-07] `KildeUtenforKorpusType`** — kildens ART: `kgl_res` |
  `instruks` | `tildelingsbrev` | `vedtekter` | `styrevedtak` | `forarbeider` | `nettside_annet`.
  **`KildeUtenforKorpusDokumentasjon`** — HVOR den er dokumentert: `primaer` (lenken/teksten er selve
  kilden) | `sekundaer` (en tekst som refererer den). Begge er påkrevd når kanten har kilde utenfor
  korpus, og NULL når den har hjemmel i korpus — «ELLER» er eksklusivt (CHECK `ck_strukturkanter_kilde`).
  `nettside_annet` gir arbeidslista over forvaltningsstruktur som mangler forankring i en rettskilde
  (`GET /api/strukturkanter/uten-korpusforankring`). Skillet mellom art og dokumentasjon finnes fordi en
  sekundærkilde kan ta feil om kildens art: en debattartikkel påsto at Tilsynsutvalget for dommere ble
  *opprettet* ved kgl.res. 15. mai 2002, mens primærkilden viser at resolusjonen *oppnevnte* de første
  medlemmene. Rettet testtilfelle (#311): Domstoladministrasjonen `sekretariat_for` Tilsynsutvalget =
  `forarbeider` + `primaer` (Ot.prp. nr. 44 (2000–2001) kap. 11.5.12); Kongen i statsråd `oppnevner`
  Tilsynsutvalget hjemlet i domstolloven; ingen `oppretter`-kant.

**[Bygget, issue #311 «Strukturmodell 6», 2026-10-07]** Tabellen `strukturkanter` (`StrukturkantEntitet`,
`StrukturkantTjeneste`, `Strukturkanter.cs`) — Johanns valg **A: full konsolidering**. Den erstatter
`VirksomhetRelasjon`, `GruppeMedlemskap` og `Myndighetstildeling` (droppet; spor i `[FJERNET]`-kommentarer,
proveniensrad `migrert` per kant og migrasjonens `Down`).

- **Ender:** polymorfe — `FraVirksomhetId` | `FraBegrepId` (nøyaktig én, CHECK) og `TilVirksomhetId` |
  `TilBegrepId` (høyst én; begge NULL bare for K/T). Lovlige nodetyper per kategori står i
  `Strukturkanter.Noderegler` og håndheves i tjenesten (M: til = klasse/område; I: til = rolle; O: område →
  område; G: til = virksomhet …). `'gruppe'` (uavklart) godtas der et begrep med gruppefunksjon godtas.
- **K uten til-node** må si hva kompetansen gjelder: `Objekt` (sakstypen), paragrafspenn eller hjemmel-eId.
- **Typekoder** er rader i `relasjonstype_konfigurasjon`, som har fått `kategori` (identitet = (kategori,
  kode), siden `instruksjon` finnes både i R og K). Startsettet over seedes ved oppstart per (kategori, kode).
- **Sykel** avvises i M og O (bevart fra gruppe-av-gruppe, #164); R har bevisst ingen sykelsjekk.
- **Idempotens:** samme kategori/type/fra/til/objekt/polaritet/hjemmel/avgrensning ⇒ samme kant.
- **Åpne spørsmål (ikke avgjort i #311):** (1) ~~de fem R-kodene som fantes før (`underlagt`, `sekretariat`,
  `klageinstans`, `enhet_i`, `oppgaver_overfort_til`) er beholdt med samme kode~~ — **avgjort og bygget i
  #330**, se under. (2) K-kodene følger tabellen over (`forskrift`, `vedtak` …), ikke FORMAT.md/
  `Strukturkontrakt` (`forskriftskompetanse` …) — avbildningen hører til #313.

**[Bygget, issue #330 «harmoniser gamle og nye relasjonskoder», 2026-10-08]** Hvert forhold har nå ÉN kode og
én lagret retning. De fem R-kodene fra før #311 er konvertert og fjernet (migrasjonen `HarmoniserRelasjonskoder`,
`RelasjonskodeHarmonisering.cs`; fjernet også fra `Strukturkanter.Startsett`, så oppstartsseeden ikke legger dem
inn igjen):

| Gammel kode | Ny kode | Retning | Rader lokalt |
|---|---|---|---:|
| `klageinstans` («har klageinstans hos») | R `klageinstans_for` | fra/til **byttet** | 3 |
| `sekretariat` («har sekretariat hos») | R `sekretariat_for` | fra/til **byttet** | 4 |
| `oppgaver_overfort_til` («fikk oppgavene overført til») | R `etterfolger` | fra/til **byttet** | 2 |
| `enhet_i` | **G** `del_av` — en enhet i en annen virksomhet er organtilhørighet, ikke en relasjon mellom selvstendige aktører | samme | 1 |
| `underlagt` | R `administrativt_underordnet` | samme | 0 |

Migrasjonen teller før og etter og avbryter ved avvik (totalt antall, ingen gammel kode igjen, hver målkode =
før + konvertert, endene kontrollert mot proveniensraden). Målt i lokal `regelide` 2026-10-08: 29 → 29 kanter,
10 konvertert med samme id, 10 proveniensrader (`endret_av = 'migrasjon-330'`, gammel kode og opprinnelige ender
i `kilde_referanser`, som `Down` leser for å snu nøyaktig de radene). KI-oppdagelsen ber nå om
`klageinstans_for`/`sekretariat_for`/`administrativt_underordnet`/`del_av` med retningen spelt ut.

**Avgrensning kan oppdateres** (`PUT /api/strukturkanter/{id}/avgrensning`, `StrukturkantTjeneste.OppdaterAvgrensningAsync`)
— den første oppdateringsveien for en eksisterende kant. Brukt på Energiklagenemnda-raden (Johanns rettelse
2026-10-08, ikke i migrasjonen): «Energidepartementet er klageinstans for Energiklagenemnda» har fått
`AvgrensningTekst` «enkeltvedtak Energiklagenemnda treffer i første instans» og paragrafspenn
`https://lovdata.no/eli/forskrift/2019/10/24/1420/nor/§1/ledd-2` (forskrift om Energiklagenemnda § 1 annet ledd).

**[Bygget, issue #341 «kompetanse med motpart og typologi», Johanns beslutninger 2026-10-08] Grensen mellom K og R.**

*Prinsippet (P1):* A har kompetanse av typen X, eventuelt **overfor B**, når det gjelder Y (bestemmelse eller sakstype),
med avgrensning og hjemmel. Myndighet er **K**, og motparten er kantens til-node. **R** brukes bare om struktur uten
myndighet: eierskap, ledelse, sekretariat, rapportering, etterfølger og representasjon. Klageinstans, instruksjon,
omgjøring, oppnevning, tilsyn med en aktør, avsetting, sanksjon, samtykke, overprøving og forelegging er derfor K, ikke R. [ENDRET, #355] Forelegging er siden tatt ut som K-type og klassifiseres etter rettsvirkningen (se #355-avsnittet under).

| Spørsmål | Svar i modellen |
|---|---|
| «Hvem er klageinstans for B?» | K `klage` med til = B — samme retning som R `klageinstans_for` hadde (fra = klageinstansen) |
| «Kan X delegere?» | K `delegering` (kompetansen til å delegere, «X kan delegere til Y») |
| «Har X delegert til Y?» | R `har_delegert_til` — en GJENNOMFØRT delegering fra et delegeringsvedtak, avgrenset per paragraf. Unntakene i vedtaket («omfatter ikke …») er avgrensning av delegeringen, ikke negativ kompetanse (beslutning 1). [ENDRET, #356] De står i avgrensningen på den positive kanten (med hjemmelsstedet), ikke som egne negative kanter |
| «Kan X gi forskrift?» | K `normgivning` med `normform = forskrift`. Normformene: forskrift, reglement, arbeidsordning, vedtekter, instruks |
| Selvregulering | Ikke en egen type: normgivning der til = fra. Den eneste selvkanten CHECK `ck_strukturkanter_ikke_selv` tillater |
| Privatrettslig instruksjon (morselskap → nettforetak) | Samme modell, feltet `grunnlag = privatrettslig` (beslutning 3). `offentligrettslig`/NULL ellers |
| «Kan kompetansen delegeres videre?» | ~~Feltet `delegerbar` (bool)~~ [ENDRET, #367 / #335] `delegeringsadgang` etter TEKSTEN (`uttrykkelig_tillatt` «kan delegere» / `uttrykkelig_forbudt` «kan ikke delegeres», «X selv» / `ikke_regulert`) og `delegering_slutning` etter alminnelige regler, alltid merket slutning (`trolig_delegerbar` «Kongen» / `trolig_ikke_delegerbar` «Kongen i statsråd», «Grunnloven § 21»). Se §4.6 |

*Typologien og hierarkiet (P2 + Johanns hierarkibeslutning 2026-10-08):* hver K-type har en **familie** og en
**fvl-kategori** i typekonfigurasjonen (`relasjonstype_konfigurasjon.familie`/`fvl_kategori`, kilden er
`Strukturkanter.Kompetansetyper`). `beslutning` står over alle familiene og brukes når teksten bare sier
«beslutningsmyndighet» (sameloven § 2-1 fjerde ledd). Familiene: struktur (oppretting, avvikling, organisasjon), personell
— [ENDRET, #352] heter nå oppnevning, se under — (oppnevning, utpeking, ansettelse, avsetting), styring (instruksjon, samordning, delegering, godkjenning, samtykke, pålegg),
normgivning, kontroll (tilsyn, revisjon), klage og overprøving (klage, omgjøring, overprøving, stadfesting), vedtak og
sanksjon. `forelegging` (beslutning 2) er ikke plassert i en familie av Johann og har familie NULL. Kantene kan filtreres
på familie (`GET /api/strukturkanter?familie=…`), og VirksomhetDetalj grupperer kompetansen på familie.

**[Bygget, issue #352 «rester etter #341», Johanns beslutninger 2026-10-08]**

1. *Familien `personell` heter `oppnevning`.* Johann: «Oppnevningskompetanse … er den mest generelle kategorien. Eksempler:
   Kommunestyret velger forliksrådsmedlemmer. Stortinget velger sivilombud. Et styre ansetter direktør. Kongen i statsråd
   oppnevner et utvalg. Fellesnevner: En aktør gis myndighet til å bestemme hvem som skal inneha en rolle, et verv eller en
   funksjon.» R `velger`, K `utpeking` og K `ansettelse` er derfor K `oppnevning` med motpart, og verbet er bevart som
   **undertype** på kanten (`strukturkanter.undertype`: `valg` | `ansettelse` | `utpeking` | `oppnevning`). Verbene
   («velger», «ansetter», «utpeker», «oppnevner») og ordstammene som avgjør undertypen ut fra et sitat står i
   `kompetanseleksikon.json` («undertyper», versjon 2026-10-08.2). *Hvorfor et felt og ikke bare leksikonet:* leksikonet sier
   hvilke ord som gir oppnevning, ikke hvilket ord en bestemt kant kom fra; uten feltet ville de 26 fasitutsagnene som var
   utpeking/ansettelse mistet opplysningen. Undertypen er bygget som normformen (lukket liste per type, bare på K, NULL = ikke
   angitt, CHECK `ck_strukturkanter_undertype`), men er ikke slått sammen med den. Ansettelse er fortsatt enkeltvedtak: fvl-
   kategorien følger undertypen (`Strukturkanter.FvlKategoriFor`). `avsetting` er en egen type i familien oppnevning som
   motsatsen — **hovedøktens tolkning, Johann bekrefter**. `utnevning` og `konstitusjon` står i fasitsitatene, men er ikke på
   Johanns liste og er ikke lagt til (spørsmål i PR-en).
2. *`ankeinstans_for` er K `overproving`* (familien klage og overprøving) med motpart og undertype `anke` (fra =
   ankeinstansen, til = den hvis avgjørelser ankes). Undertypen `anke` er hovedøktens tolkning (Johann bekrefter): uten den
   kan «hvem er ankeinstans for tingrettene?» ikke skilles fra annen overprøving.
3. *`forelegging` har familien `kontroll`.* Leksikonregelen `vedtak-godkjennes-av` gir `godkjenningskompetanse` (styring),
   ikke vedtak; id-en er beholdt som mønsterets stabile id.
4. *R `administrativt_underordnet`, `radgir`, `oppretter` og `avvikler` blir stående:* de er struktur eller hendelser, ikke
   myndighet. Kompetansen til å opprette eller avvikle er K `oppretting`/`avvikling` (familien struktur).
5. *[Tillegg, Johann 2026-10-08, fasitkontrollen domstolloven u17] Sammensetningen i den enkelte sak er G `settes_med`*, ikke
   `har_medlemmer` (organets faste medlemmer): «I andre saker enn etter første ledd første punktum settes Høyesterett med fem
   dommere». Fra = rollen som deltar (dommer), til = organet; antallet står i `objekt` («fem dommere») og sakstypen i
   avgrensningen. Typen er merket **saksavhengig** i typekonfigurasjonen (`relasjonstype_konfigurasjon.saksavhengig`).
   *Antallet i `objekt`, ikke et tallfelt:* loven sier antallet i former et heltall ikke bærer («minst tre», «en dommer og en
   varadommer», plenum = «alle … som ikke er ugilde»), fasiten har det alt i objektet, og ingen av S1–S9 regner med det ennå.
   Bare u17 er konvertert; u16 og u18 har samme form og venter på Johann.

Migrasjonen `OppnevningsfamilienOgRester` gjør dette i basen (teller før/etter, proveniens, `Down` snur nøyaktig), og
`konvertering-352-oppnevning.py` i fasiten (1797 → 1797 utsagn).

**[Bygget, issue #355 «avslutning speiler innsetting», Johanns beslutninger 2026-10-09]**

Johann la fram to analyser (avslutning av en posisjon og ankedomenet) og godkjente tilpasningen 2026-10-09; samme dag kom
beslutningene om fasitkortene domstolloven:u22, sameloven:u165 og domstolloven:u118 og utfallet av juristdebatten (kort nr. 29 og 58).

1. *Avslutning speiler innsetting.* K `avsetting` (familien oppnevning) har undertypene `avsetting` (verv og styre — motsatsen til
   valg/oppnevning/utpeking: helseforetaksloven § 25 annet ledd, domstolloven § 33a fjerde ledd), `oppsigelse` og `avskjed` (motsatsen
   til ansettelse: helseforetaksloven § 36 første ledd). Oppnevning får `utnevning` (embete etter Grl. § 21: domstolloven § 55 første
   ledd; avskjed bare etter dom, femte ledd) og `konstitusjon` (midlertidig: §§ 55a, 55e, 55f; den opphører, varigheten står i
   avgrensningen). *Hvorfor:* utnevning er ikke ansettelse. Uten skillet svarer «hvem kan sette inn en fast dommer?» Kongen,
   Innstillingsrådet og domstollederen; med det bare Kongen (`GET /api/strukturkanter?begrepId=<dommere>&kategori=K&undertype=utnevning`).
2. *Tilbakekall er ikke oppnevning:* tilbakekall av en tillatelse eller autorisasjon er K `vedtak` med undertype `tilbakekall`.
   Tilbakekall av delegert myndighet hører til delegering.
3. *Sanksjon er en funksjon, ikke en disposisjonstype.* Domstolloven § 33a er en avsetting selv om den er en reaksjon; vilkåret står i
   avgrensningen. Familien sanksjon beholdes for disposisjoner som selv er reaksjoner. Ingen funksjonsfelt.
4. *Ankeinstansen er organet.* Inndelingsforskriften § 10 første ledd er K `overproving`/`anke` fra lagmannsretten til tingretten
   (klassen), avgrenset til eget lagdømme. Parene regnes ut via tingrett → lagsogn → lagdømme (#345) og lagres ikke dobbelt; rettskretsen
   er avgrensning, ikke motpart (L1). Oppslaget `GET /api/virksomheter/{id}/ankeinstans` (`OmradeOppslagTjeneste.AnkeinstansAsync`):
   domstolens områder (A har_ansvarsomrade/sogner_til + O-forfedre) → de positive anke-kantene der domstolen er til-siden (direkte, via
   registrert medlemskap, eller til en klasse uten registrert medlemskap — den siste med hull, teller ikke) → instansen hvis
   ansvarsområde dekker domstolens. Én → `entydig`, flere → `ikke_entydig` (ingen velges), ingen → `mangler`. Lokalt finnes ennå ingen
   anke-kanter og ingen klasse «tingrett» (målt 2026-10-09); de lastes ikke i denne saken (importen er #313).
5. *Ankeadgang er en partsposisjon i regellaget*, utenfor strukturlaget (som møteplikt, §4.4): domstolloven § 37 og § 46 annet ledd
   («departementet kan anke») og «Kommunen er part i saken» (hotl. § 10-7, kort nr. 29). Den strukturelle delen av § 37, «paa det
   offentliges vegne», er R `representerer` (departementet → staten).
6. *Ankehandlingen* (den konkrete anken) hører til sakslaget og er ikke i scope.
7. *«X skal ha en Y» for ett navngitt organ er G `del_av`*, ikke T (T er bare klassenivå): Sametingets administrasjon del_av Sametinget;
   STILLINGEN «Høyesteretts direktør» (en rolle knyttet til nettopp Høyesterett) del_av Høyesterett. G tillot alt rolle som fra; ingen ny
   type. En person kobles til stillingen med I `innehar`.
8. *Ingen modalitet på strukturkanter.* Struktur med lovhjemmel er lovpålagt; modalitet brukes bare på P (CHECK
   `ck_strukturkanter_modalitet` fra #353 håndhevet det alt).
9. *Forelegging er ikke lenger en K-type.* Den klassifiseres etter rettsvirkningen og formålet: bindende svar (godkjenning, samtykke,
   avgjørelse) er mottakerens kompetanse i familien den alt har, og selve foreleggelsen er saksgang (regellaget); rådgivende svar
   (uttalelse, tolkning, merknader) er P `konsultasjon` fra avsenderen med modalitet kan/skal og negativ polaritet for forbud; kontroll
   bare når mottakeren kan undersøke og følge opp. Domstolloven § 51 a første ledd: norsk domstol P konsultasjon (kan) overfor
   EFTA-domstolen, objekt «rådgivende tolkningsuttalelse», ODA artikkel 34 som kilde utenfor korpus; annet ledd: forliksrådene, samme kant,
   negativ; første ledd siste punktum («kan ikke angripes ved anke») er en egen negativ anke-rad. EFTA-domstolens egen kompetanse
   modelleres bare hvis ODA kommer inn i korpuset.

*Migrasjonen `AvslutningSpeilerInnsetting`* (`AvslutningMigrering.cs`): ny CHECK `ck_strukturkanter_undertype`; K `forelegging` ut av
typekonfigurasjonen med alle verdiene i proveniensen (`migrasjon-355`, `slettet`); avbryter og lister id-ene hvis det finnes K
forelegging-kanter (rettsvirkningen kan ikke avgjøres uten teksten); ingen eksisterende kant får undertype (basen har ikke sitatet).
`Down` legger inn nøyaktig den slettede raden (samme Id) og nekter når en undertype fra #355 finnes. **Målt mot lokal `regelide`
2026-10-09:** 1 261 → 1 261 kanter, typekonfigurasjonen 54 → 53 (−K forelegging), 0 K forelegging-kanter, 1 proveniensrad. `Down`
prøvd (53 → 54, proveniensraden borte, gammel CHECK) og kjørt opp igjen.

*Fasiten* (`konvertering-355-avslutning.py`, deterministisk, idempotent, også på `ki-utdata/`): 1 798 → 1 809 utsagn. Undertype fra
sitatet: konstitusjon 5, tilbakekall 5 (hotl. u186, sphl. u253, energiloven u205–u207), avsetting 2, utnevning 2 (domstolloven u120 og
spesialisthelsetjenesteloven u216 — rettet til oppnevning i juristrunden, se under). Kort nr. 58 (domstolloven § 55 femte ledd) er tre
rader, spesialisthelsetjenesteloven u118 («si opp eller avskjedige») to. Forelegging: de 2 K-radene (domstolloven u117, u118) → P
konsultasjon (kan); relasjon/annet:forelegges_for (u52, u56) og annet:intern_forelegging (hotl. u141, u142) står som saksgang. Nye rader:
§ 37 R representerer, § 51 a negativ anke. u275 til = tingrettene, u22/u165 → del_av, u102/u108 → annet:partsposisjon/annet:ankeadgang,
u145 → annet:partsposisjon (til = null). KI-utdata 1 043 → 1 043 (tilbakekall 6, utnevning 1).

*Juristrunden (CLAUDE.md §23, 2026-10-09):* agenten jurist-utfordrer vurderte de 29 radene konverteringen endret eller la til; 13
holdt og 16 fikk innvending (9 lav, 7 middels). Alle er akseptert og bygget inn som regel 10 i skriptet, så ingen uenighet gikk
videre til Johann: konstitusjonsradene fikk motparten dommere og vilkårene/varigheten i avgrensningen (u124, u133, u134, u136, u137);
Innstillingsrådets konstitusjon etter § 55 f annet ledd er en egen rad (L13); u120 har `delegerbar = false` (Grl. § 21: Kongen i
statsråd); u216 «kontaktpsykolog utnevnes» er oppnevning, ikke utnevning (ikke embete — verbet alene avgjør ikke); u94 er unntaket fra
avsettingen (undertype avsetting, fra = null som u93); vilkårene står i avgrensningen på tilbakekallene (u186, u205–u207) og § 51 a
(u117); nye rader for protokollsekretærer og utredere del_av Høyesterett (§ 9), Høyesteretts anke etter § 37 og § 55 h første ledd
(kort nr. 58 for midlertidige dommere).

| Måling (samme treffregel, samme utdata) | Før #355 | Etter #355 |
|---|---:|---:|
| Fasitutsagn | 1 798 | 1 809 |
| Mønster alle: P / G | 92,7 / 48,2 % | 92,7 / 47,9 % |
| Mønster K (fasit 577 → 582): P / G | 88,0 / 49,4 % | 88,0 / 49,0 % |
| KI alle: P / G | 36,5 / 21,2 % | 36,5 / 21,1 % |
| Union alle: P / G | 58,2 / 55,7 % | 58,2 / 55,4 % |
| designtest.py revidert ja / senere lag | 1 719 / 58 | 1 728 / 60 |

Prediksjonene er uendret (934 mønster, 1 043 KI); fallet i gjenfinning er bare at fasiten fikk elleve rader. Leksikonet (versjon
2026-10-09.3) har undertypene med verb og ordstammer; ingen nye mønster-regex er lagt til (ingen målt form med høy nok presisjon).

**[Bygget, issue #356 «konverteringsrester etter #341/#352», 2026-10-09]**

Fasitkontrollen (#309, gjennomgang v5) fant regler som var besluttet, men ikke slo til, og juristdebatten om fasitkortene ga 35 kort
(17 i #356, 18 i #309). Rettelsen er REGLER i `konvertering-356-fasitrester.py` (deterministisk, idempotent, også på `ki-utdata/`), ikke
håndrettede rader. Ingen modellendring og ingen migrasjon:

- *Oppretting og avvikling* (R1): konstituerende/oppretter eller avvikler med kompetanseordlyd («kan opprette/nedsette», «maa ikke
  nedsættes», «treffer vedtak om å opprette/oppløsning», «avgjør om … etablere») er K `oppretting`/`avvikling` (#352 beslutning 4).
  Hendelsen og plikten («skal opprette») står. 10 rader i fasiten, 3 i KI-utdataene (og 1 til oppnevning etter R2).
- *«Det kan oppnevnes»* (R2) er K `oppnevning`/`oppnevning` med fra = null, ikke G `har_medlemmer`.
- *«… kan ikke delegeres»* (R3): `delegerbar = false` på kompetansene i paragrafene loven nevner (sameloven § 2-12 fjerde ledd → §§ 2-9,
  2-10, 2-14). Tekstfunn, ikke slutning; skillet på feltet er #335. Den doble «X selv»-raden (hotl. u62) er slettet (R4).
- *«beslutningsmyndighet»* gir K `beslutning` (R5). Interne foretaksbeslutninger, fordeling mellom RHF-er, avtaleinngåelse mellom
  offentlige rettssubjekter og «øverste valgmyndighet» er også `beslutning` (kort og juristrunden). Leksikonregelen «X avgjør» er
  gjennomgått og står: 4 av 7 prediksjoner treffer et enkeltvedtak i fasiten (57 %), og verbet alene skiller ikke enkeltvedtak fra
  beslutning — en type som ikke kan leses av ordlyden, gjettes ikke av mønsterlaget.
- *Unntak i et delegeringsvedtak er avgrensning* (R6): de 13 negative `har_delegert_til`-radene i energilovens delegeringsvedtak er
  slettet, og unntakene står i avgrensningen på den positive kanten med hjemmelsstedet («FOR-2025-06-26-1340 kap. I femte ledd»). Uten
  en positiv kant blir raden stående og listes (0). I KI-utdataene: 1.
- *Gjennomført delegering* (R0, R7): `delegeringskompetanse` i et ledsagende delegeringsvedtak med «delegerer/delegeres» er R
  `har_delegert_til` (KI-utdataene: 7 rader). «Delegeringen omfatter myndigheten til å endre og oppheve forskrift …» er delegeringens
  rekkevidde (kort nr. 8), ikke departementets egen normgivning.
- *Negasjon som gjentar en positiv rad* føres som avgrensning (kort nr. 30, 50, 62): Tilsynsutvalget («kan ikke vurdere forhold som kan
  overprøves …») har avgrensningen på alle sine kanter; eierstyring i foretaksmøte (hfl § 16) er én kant med unntaket for vilkår ved
  bevilgning som egen kompetanse.
- `kilde_utenfor_korpus` gjelder hjemmelen (lærdom 1): 48 forskriftshjemler i spesialisthelsetjenesteloven-fasiten rettet.

*Juristrunden (CLAUDE.md §23):* to agenter vurderte 74 kort (33 regelendringer, 41 kortrettinger): 51 holdt, 23 fikk innvending (19 lav,
4 middels). 21 er akseptert og bygget inn som regel 10; to er delvis akseptert, og juristen godtok begge i runde 2, så ingen uenighet går
til Johann: oppløsningsverdien for en forskrift i korpuset mangler i formatet (lærdom 2 — egen sak; «ukjent» ville vært en ny feil), og
familien for eierstyring i foretaksmøtet (u51 beslutning mot u56 instruksjon) er et åpent valg for hovedøkta, merket på begge radene.

| Måling (samme treffregel, samme utdata) | Før #356 | Etter #356 |
|---|---:|---:|
| Fasitutsagn | 1 809 | 1 802 |
| KI-utsagn | 1 043 | 1 042 |
| Mønster alle: P / G | 92,7 / 47,9 % | 92,5 / 47,9 % |
| Mønster K (fasit 582 → 596): P / G | 88,0 / 49,0 % | 87,3 / 47,5 % |
| KI alle: P / G | 36,5 / 21,1 % | 36,8 / 21,3 % |
| Union alle: P / G | 58,2 / 55,4 % | 58,2 / 55,6 % |
| designtest.py revidert ja / senere lag | 1 728 / 60 | 1 720 / 61 |

Mønsterprediksjonene er uendret (934). Presisjonen faller fordi fasiten nå sier beslutning eller sanksjon der mønstrene `vedtak-treffe` og
`vedtak-avgjor` sier vedtak (domstolloven § 236, spesialisthelsetjenesteloven § 5-1). KI-laget vinner på R: `har_delegert_til` i
KI-utdataene treffer nå fasiten. Lokal `regelide` har ingen kanter som rettingene gjelder (målt 2026-10-09: 1 261 kanter, ingen K
oppretting/vedtak/delegering eller R `har_delegert_til`), så ingen dataoppdatering.

*Fvl-kategori — på typen, ikke på kanten:* `forskrift | enkeltvedtak | ikke_vedtak` (forvaltningsloven § 2: vedtak =
forskrift + enkeltvedtak). Det er en egenskap ved hva slags kompetanse det er (alle vedtakskompetanser er enkeltvedtak), så
den står på typen. Unntaket er normgivning, der normformen avgjør: `normform = forskrift` gir fvl-kategori `forskrift`
(`Strukturkanter.FvlKategoriFor`); andre normformer gir NULL, fordi om et reglement er en forskrift etter fvl. § 2 c ikke
følger av formen. Satt der det følger av loven uten skjønn: vedtak, pålegg og ansettelse = enkeltvedtak; instruksjon,
samordning, tilsyn og revisjon = ikke_vedtak. Resten er NULL = ikke avklart (f.eks. er en avskjed et enkeltvedtak, men
avsetting av et foretaksstyre er det ikke).

*Hjemmelssted og avgrensning (feilen fra #311):* kanten har to ulike opplysninger om paragrafer —
**hjemmelsstedet** (`hjemmel_eid`: HVOR det står, f.eks. naturgassforskriften § 1-4) og **avgrensningen**
(`avgrensning_paragrafspenn_json`: HVILKE paragrafer det gjelder for, f.eks. energiloven §§ 2-1, 2-2 for
konsesjonsmyndigheten). #311 la `Myndighetstildeling.ParagrafspennJson` i avgrensningen, men alle skriveveiene (veiviseren,
KI-oppdagelsen, samisk-seeden) hadde fylt det med noden der tildelingen står. Modellvalget er **den eksisterende
`hjemmel_eid`** for hvor, ikke et nytt `hjemmel_paragrafspenn_json`: fasiten og konverteringen bruker ett eId per utsagn,
R/K/A/O-kantene hadde alt `hjemmel_eid` med den betydningen, og to felt for «hvor» ville gitt to sannheter. Et spenn som
hvor-opplysning finnes ikke i data (0 av 19). M/I med hjemmel i korpus krever nå hjemmel-eId (før: et avgrensningsspenn).
Merk at docs/20 §2.5 beskrev det gamle feltet som «paragrafer i loven tildelingen dekker» (avgrensning), og «Legg til
tilhørighet»-skjemaet bygget det fra gruppebegrepets lov — migrasjonen skiller derfor på om punktet ligger i kantens EGEN
hjemmel, ikke på en antakelse.

*Migrasjonen `KompetanseMedMotpart`* (`KompetanseMigrering.cs`, teller før/etter og avbryter ved avvik). **Målt mot lokal
`regelide` 2026-10-08:**

| Måling | Før | Etter |
|---|---:|---:|
| Strukturkanter totalt | 1 261 | 1 261 |
| R `klageinstans_for` | 3 | 0 |
| K `klage` (samme fra/til, Energiklagenemnda-radens avgrensning beholdt) | 0 | 3 |
| M/I med avgrensningsspenn | 19 | 0 |
| M/I med hjemmel-eId (spennet var ett punkt i egen hjemmel i alle 19) | 0 | 19 |
| Proveniensrader `migrasjon-341` (gamle verdier, som `Down` leser) | — | 22 |
| K-typer i konfigurasjonen (med familie / med fvl-kategori) | 11 | 24 (22 / 7) |
| R-typer i konfigurasjonen | 17 | 13 |

Avbryter i stedet for å gjette ved R `delegerer_til` (kompetanse eller gjennomført?) og K `iverksetting` (ikke i
typologien) — 0 lokalt. `grunnlag` og `delegerbar` er ikke satt på noen rad.

**[Bygget, issue #418, Johanns avgjørelser 2026-10-29] Klasse som innehaver av kompetanse (K) og rolle (I).** Piloten på barnehageloven
(#375) viste at standardregelen fra #394 løser «kommunen» og «statsforvalteren» til klassene kommune og statsforvalter, mens `Noderegler`
bare tok aktør eller rolle som fra i K. 103 utsagn om kommunens og statsforvalterens myndighet falt derfor bort: godkjenning, dispensasjon,
lokal forskrift, tilsyn, pålegg, stenging, klage og tilskuddsvedtak.

- **(a) K tar klasse som fra**, lest **distributivt** som P (#353) og F (#368): «Kommunen kan gi lokal forskrift» i en lov som retter seg mot
  alle kommuner, gir hver kommune kompetansen. Det er ikke en slutning om lovens innhold; organet som utøver kompetansen, følger av
  kommuneloven. `Strukturkanter.Noderegler[K]`: «aktør, rolle eller klasse». Et område er fortsatt ikke en innehaver.
- **(b) I tar klasse som fra**, slik at en rolle loven selv navngir, kan registreres på klassen: «Kommunen er lokal barnehagemyndighet»
  (barnehageloven § 10 første ledd) er I `innehar` fra klassen kommune til rollen. Kompetanser kan stå både på klassen og på rollen; teksten
  avgjør hvilken. `Noderegler[I]`: «aktør eller klasse». (b) er et tillegg til (a), ikke et alternativ: mange kompetanser står på
  «kommunen» uten rollebetegnelse.
- **Svaret for ett medlem regnes ut og lagres ikke** (#345): `KompetanseViaKlasseTjeneste`,
  `GET /api/virksomheter/{id}/kompetanse-via-klasse`, seksjonen «Via klasse eller rolle (avledet)» under Kompetanse på aktørsiden
  (docs/09 §48). Veien er medlemskapet (utledet via territoriekanten for klassen kommune, #384, eller registrerte M-kanter, også klasse i
  klasse og område i klasse), eventuelt rolleinnehavet (I fra aktøren eller fra klassen), og K-kanten. Retning `har` er K fra klassen eller
  rollen; `overfor` er K med klassen eller rollen som motpart.
- **Svakeste premiss (§4.6)** håndheves i dette oppslaget: svaret er `avledet` bare når alle premissene er minst så sterke; en K-kant som
  ikke er vurdert, gir `uavklart`. Når samme kant nås langs flere veier, brukes den med sterkest svakeste premiss, så færrest forslag, så
  kortest. Kjent avgrensning: premissene er kanter. Klassens egen kunnskapsstatus og standardregelen som løste «kommunen» da K-kanten ble
  lastet, er ikke premisser, fordi kanten ikke bærer hvordan fra-noden ble løst. Det hører til #411.
- **Hull:** en klasse med K-kanter men uten registrert eller utledet medlemskap gir ingen treff. Den listes som hull («om aktøren er medlem,
  avgjøres ikke her»). Lokalt gjelder det klassen statsforvalter (ingen M-kanter, målt 2026-10-29).
- **Andre oppslag** som leser I-kanter, tar fortsatt bare aktør som fra: myndighetstildelingsfasadene (`/myndighetstildelinger`,
  `/gruppebegrep/{id}/tildelinger`) og motpartsoppløsningen i pliktoppslaget (en plikt overfor rollen «lokal barnehagemyndighet» løses ikke til
  kommunene via klassen). Ankeinstansoppslaget håndterte alt K fra et begrep.
- **Rollen som forslag:** `POST /api/gruppebegrep` tar valgfritt hjemmel-eId (`lovreferanseEid`, kontrollert mot korpus) og utsagnsstatus
  (kunnskapsstatus, begrunnelse, godkjenning bare som forslag, oppdagelseskilde). Rollen «lokal barnehagemyndighet» er lagt inn slik, se
  docs/profil/07 §8.

### 4.4 Bevisst ikke i strukturlaget (forslagets punkt 9)

~~11 % av fasit-utsagnene hører til et senere lag og lagres ikke som strukturkanter: samarbeid,
bistand, informasjonsdeling, konsultasjon, saksforberedelse, finansiering/betalingsansvar,
møteplikt. De står fortsatt i fasiten, så de kan måles når det laget bygges.~~

**[ENDRET, issue #353, Johanns godkjenning 2026-10-08] Snudd.** Fasitkontrollen (#309) viste det motsatte: samarbeid,
avtale, betaling, bistand, informasjon og konsultasjon handler om hvem som har ansvar overfor hvem — selve formålet med
strukturmodellen. De er nå kategorien **P** (§4.5). Det som **blir stående ute**, er møteplikt og saksforberedelse
(forelegging, oversending, forberedelse av sak, budsjettforslag), rettigheter som er motstykket til en plikt
(konsultasjonsrett, høringsrett, informasjonstilgang — å lage plikten av retten ville vært å slutte gjensidighet), plikter
for private («Enhver plikter …»), hefte/garanti, bevilgning og avtalens innhold. Målt etter konverteringen: 56 av 1 798
fasitutsagn (3 %) står igjen som «senere lag» (58 etter juristgjennomgangen) (før: 7–8 %, `designtest.py`).

### 4.5 Plikt overfor motpart (P) og nodetypen ordning (issue #353)

**[Bygget, issue #353, Johanns godkjenning 2026-10-08]** Johann la fram en analyse (struktur- vs pliktrelasjoner, plikt som egen
node, fem roller i en betalingsregel, folketrygden som ordning og ikke aktør) med beskjed om å tilpasse den til modellen vi
har. Tilpasningen:

1. **To normative kanttyper, ikke ett nytt lag.** R er beskrivende, K (kompetanse med motpart, #341) er normativ «kan». En plikt
   overfor en motpart er motstykket: **P**, i samme kanttabell, med samme felter som K (fra, valgfri til, hjemmel-eId,
   avgrensning, polaritet, status, oppdagelseskilde) og i tillegg **`modalitet`** (`skal` | `kan` | `bor`, NULL = ikke angitt;
   L14 «modalitet bevares»). Typene: `samarbeid`, `avtale`, `betaling`, `bistand`, `informasjon`, `konsultasjon` — lagt inn
   som **typekoder** i kategorien P (som K-typene), ikke i kantens `undertype`-felt (hovedøktens tolkning, Johann bekrefter).
2. **Den rike plikt-noden finnes i regellaget.** Vilkår, objekt, unntak, tid, mottaker og begunstiget er regel → vilkår →
   **rettsfølge** → unntak (docs/01 §7–§8, L16). P-kanten er den **aktørnære projeksjonen**: hvem som skal, overfor hvem, og
   hvor det står — koblet til regellaget via eId når det bygges.
3. **De fem rollene i en betalingsregel, uten nye felter:** normadressaten i ordlyden = taggens tekstform; ordningen =
   fra-noden; forvaltende organ = R `forvaltes_av` (hjemlet); rettslig ansvarsbærer = G `tilhorer` **bare når hjemlet**
   (ellers ingen kant, synlig hull); betalingsmottaker = `til`, **null når teksten ikke sier det** («utgiftene til X» sier ikke
   hvem som får pengene); begunstiget = avgrensning nå, regellaget senere.
4. **Nodetypen `ordning`** (ikke-aktør som rettskilden gir en funksjon) bor på `Virksomhet` som aktørtype, ved siden av
   rettssubjekt, organ og organisatorisk enhet, med `Virksomhet.Ordningstype` = `trygdeordning` | `fond` | `tilskuddsordning`
   (CHECK `ck_virksomheter_ordningstype`). Hvorfor `Virksomhet` og ikke en begrepskategori: P/R/G har virksomhet-ender, og et
   organ uten orgnr («Kongen i statsråd», #311) bor der alt (hovedøktens valg, Johann bekrefter). En ordning kan bare være
   fra-node i P, R `forvaltes_av` og G `tilhorer`, og til-node i P (`Strukturkanter.OrdningLovSomFra`); `forvaltes_av` og
   `tilhorer` krever en ordning som fra-node.
5. **Generelle parter løses opp til konkrete par, ikke lagret.** «Kommunen skal inngå samarbeidsavtale med det regionale
   helseforetaket i helseregionen» er én P-kant mellom klasser. S6 «hvem har kommune X samarbeidsplikt med?»
   (`GET /api/omrader/kommuner/{nr}/plikter?type=samarbeid`, `OmradeOppslagTjeneste.PlikterForKommunenummerAsync`):
   pliktene **direkte** (fra = kommunens virksomhet), **via registrert medlemskap** (M) og fra en **klasse uten registrert
   medlemskap** — den siste listes med hullet «om plikten gjelder kommunen, avgjøres ikke her» (intensjonal klasse,
   regelevaluering). Motparten løses som Statsforvalter-eksempelet i §4.2: medlemmene av til-klassen hvis
   `A har_ansvarsomrade` dekker kommunen eller et område den ligger i — `entydig` | `ikke_entydig` (ingen velges) | `mangler`
   med hull (helseregionenes inndeling står ikke i lov; den kommer fra vedtektene, ekstern kilde, #340) | `ikke_angitt`.
   Lokalt finnes helseregion → fylke-kantene fra vedtektene alt (#312), så paret kan regnes ut når RHF-klassens medlemmer er
   registrert. [ENDRET, #353-retting etter koordinatorens kaldtest 2026-10-09] Oppslaget går i **begge retninger**: det kommunen
   skal (`retning = kommunen_skal`) og det andre skal overfor kommunen (`overfor_kommunen`, kommunen er til-siden — direkte
   eller via registrert medlemskap); motparten er da pliktsubjektet, løst på samme måte. Klasser uten registrert medlemskap listes for ALLE kommuner (de kan gjelde dem) — det er støy til
   medlemskapet er lastet eller kan avgjøres, men den er merket, ikke skjult.
6. **Gjensidighet registreres som teksten sier den.** Én P-kant per pliktsubjekt (L13). Samarbeid mellom medlemmer av samme
   klasse er én kant fra klassen til seg selv (CHECK `ck_strukturkanter_ikke_selv` har unntaket for P med begrep-ende).
7. **Avtaleplikt, avtale og det avtalen etablerer er tre ting.** Plikten til å inngå avtale er P `avtale`. Den inngåtte
   avtalen er en ekstern kilde (kildetype `avtale`, #340 — ikke bygget). Det avtalen etablerer, er R/K-kanter hjemlet i den.

**Folketrygden — slått opp, ikke gjettet.** Folketrygdloven ligger i den lokale basen (ELI `lov/1997/02/28/19`, gjeldende).
§ 21-11 a første ledd: «Helsedirektoratet skal forvalte kapittel 5, sikre rett ytelse til den enkelte og ha ansvaret for å følge
opp og kontrollere tjenester, ytelser og utbetalinger» (eId `https://lovdata.no/eli/lov/1997/02/28/19/nor/§21-11a/ledd-1`).
Det gir R `forvaltes_av` Folketrygden → Helsedirektoratet, avgrenset til kapittel 5. For de andre kapitlene sier
folketrygdloven ikke «forvalte» om noen (§ 21-11 første ledd gir Arbeids- og velferdsdirektoratet **vedtakskompetanse**, ikke
forvaltning) — synlig hull. Ingen bestemmelse sier at folketrygden tilhører staten — ingen `tilhorer`-kant. Fasitkommentaren
«forvaltes av Nav (utenfor korpus)» var en antakelse uten kilde og er erstattet. Hvilket kapittel betalingsplikten i
spesialisthelsetjenesteloven § 5-3 annet ledd hører under, sier ikke loven.

**Migrasjonen `PliktOgOrdning`** (`PliktMigrering.cs`, teller før/etter, proveniens `migrasjon-353`, `Down` snur nøyaktig og
nekter når det finnes P-/forvaltes_av-/tilhorer-kanter, en modalitet eller en ordning). **Målt mot lokal `regelide`
2026-10-09:** 1 261 → 1 261 kanter, typekonfigurasjonen 46 → 54 (P ×6, R `forvaltes_av`, G `tilhorer`), 0 kanter flyttet (ingen
R `samarbeider_med`/`bistar` lokalt — kodene har aldri stått i startsettet), 0 proveniensrader. `Down` prøvd mot lokal base
(54 → 46, kolonnen `modalitet` borte) og kjørt opp igjen.

**[ENDRET, juristgjennomgangen 2026-10-09, godtatt av koordinatoren] Betalingsmottaker og modalitet.**
- *Betalingsmottakeren:* sier teksten HVEM utgiftene er sine — genitiv («Det regionale helseforetakets … utgifter») eller
  «utgifter som påføres X» — er `til` = X; det er en tekstlesning, ikke en slutning. Bare formål («utgifter til behandling …»,
  «utgiftene til kontrollkommisjonenes virksomhet») gir `til` = null. Spesialisthelsetjenesteloven § 5-2 første ledd første
  punktum: til = det behandlende RHF-et (at det må være et ANNET RHF, kodes ikke); annet punktum: null. Sameloven § 1-4 første
  ledd (u5/u6): til = fylkeskommunene og kommunene. § 5-3 første ledd siste punktum («… skal de dekkes av vedkommende
  helseinstitusjon») er tapsfordeling, ikke plikt overfor en motpart: `annet:tapsfordeling`.
- *Modalitet:* «Det samme gjelder …» arver modalverbet i setningen det viser til (§ 5-2 første ledd annet punktum → skal). Normativ
  presens uten modalverb («dekkes av staten», «Staten dekker», «Staten yter») = skal, notert som presens. Et punkt i en liste får
  innledningens modalverb («Plikten til å konsultere … gjelder for» → skal). «Departementet kan pålegge samarbeid mellom kommuner»
  (hotl. § 6-6): «kan» hører til departementets K `paleggskompetanse` (var vedtakskompetanse — samme regel gir også energiloven
  u119 og u204), kommunenes plikt er P samarbeid med modalitet skal, avgrensning «når pålegg er gitt», og det konkrete pålegget er
  en kilde utenfor korpus. Domstolloven § 19 annet ledd («Domstolens leder kan ellers be lagmannsretten om å foreta tilkalling …»,
  u43) er verken «A kan pålegge B» eller «B kan, etter samtykke fra A», men «A kan be B om»: en adgang til å be om bistand, ingen
  plikt for B → `relasjon/annet:anmodning_om_bistand`, ikke P.
- *Hjemmelen på punktumnivå:* § 5-2 første ledd har ingen punktum-noder i korpuset (bare ledd-eId), så punktumet står i
  kommentaren/avgrensningen, ikke i eId-en.

**Fasiten** (`konvertering-353-plikt.py`, deterministisk, idempotent, også på `ki-utdata/`): 1 797 → 1 798 utsagn. 77 til plikt
(samarbeid 21, betaling 18, bistand 14, informasjon 13, konsultasjon 8, avtale 3); modalitet fra sitatet 48, fra setningen 13,
fra innledningen til lista 7, normativ presens 6, «Det samme gjelder» 1, pålegg 1, null 1 (spesialisthelsetjenesteloven u190:
«skal … sørge for samarbeid …, slik at … kan ivareta» — to modalverb). Betalingsmottakeren beholdt i 3 utsagn (genitiv / «som
påføres»), satt til null i 3 (formål). Én betaling er `annet:tapsfordeling`, én bistand er `annet:anmodning_om_bistand`, og tre
vedtakskompetanser med «kan pålegge» er `paleggskompetanse`. Folketrygden (spesialisthelsetjenesteloven a39 og helse- og
omsorgstjenesteloven a56) og Energifondet er entitetstype `ordning`; «forvalteren av Energifondet» er snudd til `forvaltes_av`;
ett nytt `forvaltes_av` fra folketrygdloven (noden lagt i `noder/`, folketrygdloven som ledsagende kilde med bare den ene noden).
KI-utdata 1 043 → 1 043, 43 til plikt.

**Leksikonet** (versjon 2026-10-09.2): «skal samarbeide med» → samarbeidsplikt, «skal inngå (samarbeids)avtale med» →
avtaleplikt, «skal dekkes av» og den aktive «X skal dekke utgifter» → betalingsplikt (til = Y bare ved genitiv eller «som påføres
Y»; presens = skal), «skal gi opplysninger til» → informasjonsplikt, «skal innhente uttalelse fra» → konsultasjonsplikt (uten «fra»
er det saksforberedelse). Modaliteten tas fra modalverbet i treffet, ikke fra regelen.

| Måling (samme treffregel) | Før #353 | Etter #353 |
|---|---:|---:|
| Fasitutsagn | 1 797 | 1 798 |
| Mønster alle: P / G | 93,9 / 47,2 % | 92,7 / 48,2 % |
| Mønster P (fasit 77; før: «senere lag» 39): P / G | – / 0 % | 58,1 / 23,4 % |
| — samarbeidsplikt (21) / avtaleplikt (3) / betalingsplikt (18) | – | 77,8 / 33,3 · 66,7 / 66,7 · 47,4 / 50,0 % |
| — informasjonsplikt (13) / konsultasjonsplikt (8) | – | 0 treff (fasitens former er passiv «skal informasjon utleveres til» og lister) |
| KI alle (samme utdata, konvertert): P / G | 36,6 / 21,3 % | 36,5 / 21,2 % |
| KI P: P / G | (senere lag 35,9 / 35,9 %) | 32,6 / 18,2 % |
| Union alle: P / G | 58,3 / 55,1 % | 58,2 / 55,7 % |
| designtest.py revidert ja | 91 % (1 642) | 96 % (1 719) |

Flere av mønsterlagets falske positive i P er «ikke i fasiten» og ser ut som fasitutelatelser («Utgiftene dekkes av det
offentlige», domstolloven § 105 a; «Kommunen dekker reiseutgifter for behandlingspersonell …», hotl. § 11-1) — ikke målt som
riktige; de vurderes i #309. KI-tallene er de lagrede utdataene fra #308 konvertert med samme skript, ikke en ny kjøring med
den nye instruksen.

### 4.6 Utsagnsstatus: kunnskapsstatus, godkjenning, historikk og versjonert hjemmel (issue #367 + #335)

[Ny, issue #367, Johanns beslutning 2026-10-15: #367 og #335 i én migrasjon, `UtsagnsstatusOgDelegeringsadgang`] Bygget etter
docs/34 §3–5 og metodenotatet (docs/profil/03 §5 og §8). Hvert felt på `strukturkanter`:

| Felt | Verdier | Regel |
|---|---|---|
| `status` = **godkjenning** | `maskinelt_foreslatt` \| `manuelt_foreslatt` \| `faglig_vurdert` \| `godkjent` \| `avvist` | Gamle verdier: `foreslatt_av_ai` → `maskinelt_foreslatt`, `validert` → `godkjent`. Forslagskøen og «godkjenn» gjelder de tre første. [ENDRET, #313/#374, Johanns beslutning 2026-10-15] «Avvis» setter `avvist` med `sist_endret_av`/`sist_endret_tidspunkt` og valgfri `avvisning_begrunnelse` (CHECK-er i migrasjonen `AvvistSomStatus`); raden slettes ikke. Avviste skjules av den globale spørrefilteren (som foreldede) og vises med «vis avviste» |
| `kunnskapsstatus` = **metode** | `tekstfunn` \| `normalisering` \| `fortolkning` \| `avledet` \| `eksternt_dokumentert` \| `uavklart` \| `omstridt` \| `foreldet`; NULL = ikke vurdert | CHECK: eksternt dokumentert krever kilde utenfor korpus; tekstfunn og normalisering krever hjemmel i korpus med eId; fortolkning krever `begrunnelse` og minst ett av `tolkningsargumenter` (ordlyd, kontekst_system, formal, forarbeider, praksis, reelle_hensyn, antitetisk_analogisk); foreldet krever entitetsstatus foreldet. Avledet krever minst én rad i `strukturkant_premisser` (constraint-trigger, utsatt til commit). Ingen eksisterende rad fikk en verdi |
| `entitetsstatus`, `erstatter_kant_id` | `gjeldende` \| `foreldet` | En revisjon (`POST /api/strukturkanter/{id}/revisjon`) gir en ny rad; den gamle blir foreldet, ikke slettet. Alle oppslag ser bare gjeldende rader (global spørrefilter). Identitetsregelen gjelder ikke når kunnskapsstatus er `omstridt` |
| `profilversjon` | `arbeidsutkast-2026-10-09` | Settes på nye rader som får en kunnskapsstatus |
| `hjemmel_node_id`, `hjemmel_endret_tidspunkt` | FK til `rettskilde_noder` | Settes når kanten opprettes (nøyaktig én gjeldende node med eId-en). Migrerte rader fikk noden i sin egen hjemmel-rettskilde; NULL med eId vises «hjemmelversjon ukjent». Lovdata-resynken (`RettskildeImportTjeneste.OpprettNyVersjonAsync`) og håndbokrevisjonen flagger kanten når nodens deltre (noden og undernodene, også avslutningsnoder fra #361) har annen tekst i den nye versjonen — ellers ikke. Ingenting annet endres |
| `delegeringsadgang`, `delegering_slutning` | se tabellen i §4.3 | Bare på K; slutningen bare ved `ikke_regulert`. Delegeringskompetansen selv får ingen verdi (den ER delegeringsregelen) |

**Avledede svar** lagres ikke (#345): ankeinstans-oppslaget svarer med `kunnskapsstatus = avledet` og premissenes kant-id-er (anke-kanten,
områdekjeden, instansens ansvarsområde, medlemskapet); pliktoppslaget for en kommune merker et treff `avledet` når det bygger på mer enn
den ene lagrede plikten (medlemskap, motpartsoppløsning). [Ny, #418] Kompetanse via klasse eller rolle (§4.3) svarer med statusen etter
svakeste premiss (medlemskap, rolleinnehav, K-kanten), som tilhørigheten i §5.13.

**[Ny, Johanns beslutning 2026-10-27] Svakeste premiss — en generell regel.** Et avledet utsagn (en kant eller et svar som bygger på
andre utsagn) får **aldri** sterkere kunnskapsstatus enn det svakeste premisset. Regelen gjelder alle avledninger, lagrede og beregnede
(etterfølger, utledet medlemskap, standardregler, ankeinstans, pliktoppslag, F-kanter fra migrasjoner), ikke bare etterfølgeren i §5.9.3.
Den samme setningen står i metodenotatet (docs/profil/03 §5, «Statusen følger utsagnet»). Konkret:
- Et premiss som er `uavklart`, `omstridt` eller **ikke vurdert** (NULL) gir ikke et `avledet` svar som om det var avgjort: utsagnet
  blir `uavklart` (eller `omstridt`), og premisset nevnes.
- En lesning som selv er `tekstfunn` eller `normalisering`, men som bare gjelder fordi et annet utsagn er `fortolkning`, blir
  `fortolkning` (som etterfølgeren etter P2/S2 når opphøret er lest etter P1).
- Et premiss som er avvist eller foreldet, bærer ikke slutningen lenger.
- **Rangering** [Johanns beslutning 2026-10-27], sterkest først: `tekstfunn` = `eksternt_dokumentert` > `normalisering` > `avledet` >
  `fortolkning` > `uavklart` > `omstridt`. Et premiss uten vurdert kunnskapsstatus (NULL) regnes ikke som sterkere enn `uavklart`.
  `foreldet` bærer ingen slutning. Rangeringen gjelder bare når premisser sammenlignes i en avledning. Den gjør ikke vokabularet i
  docs/profil/03 §5 til en generell kvalitetsskala.
- **Målt 2026-10-27 (lokal base)** mot regelen: se §5.11 (#386). Bruddene er registrert i #411 og ikke rettet i #386.

**Kategorier bundet til en lov** (docs/profil/03 §8): typekonfigurasjonen har `fvl_kategori_hjemmel` («forvaltningsloven (LOV-1967-02-10)
§ 2»), `fvl_kategori_gjelder_fra` (1970-01-01, lovens ikrafttredelse slik korpuset har den) og `fvl_kategori_gjelder_til` (tom: den nye
forvaltningsloven er ikke i kraft).

**«Kongen» og «Kongen i statsråd»** (#335 AC1, Johanns beslutning 2026-10-08: samme aktør, forskjellen ligger på kompetansen): virksomheten
Kongen i statsråd har fått navneformen «Kongen», og taggene med teksten «Kongen» er flyttet dit (lokalt 7 av 12; 5 står på «Kongen i statsråd»).

**Fasiten** er konvertert med `konvertering-367-utsagnsstatus.py` (regler D0–D5 og K1, se skriptet og FORMAT.md).

### 4.7 Oppgave/funksjon (kategori F, issue #368)

**[Bygget, issue #368, Johanns beslutning 2026-10-15: F er et eget begrep, og ingenting flyttes automatisk før en juristrunde på
kandidatene]** Definisjon, typer, eksempler, moteksempler og grenser står i begrepsmanualen (docs/profil/02 §F); her står modellen,
juristrunden og målingen.

**Modellen.** Kategori `F` i `strukturkanter`: fra = aktør, rolle eller klasse (ikke ordning); `objekt` = oppgaven, PÅKREVD (CHECK
`ck_strukturkanter_oppgave_objekt`); `til` valgfri (område, klasse, rolle eller aktør oppgaven gjelder); `modalitet` lov på P og F
(`ck_strukturkanter_modalitet`). Ni typer (`Strukturkanter.Oppgavetyper`): forvaltning, samordning, sorge_for, oversikt, bistand,
sekretariatsfunksjon (ikke «sekretariat» — det var R-koden #330 fjernet), radgivning, veiledning og kontroll — de to siste er lagt til i #368 (barnehageloven-piloten «veilede og påse»; juristrunden).
Visningen har ingen «overfor»: «har samordningsoppgave (skal) beredskapsarbeid», «… — gjelder B». **F gir aldri K eller P i seg selv**:
ingen kode slutter en K- eller P-kant fra F, og kompetanse- og pliktoppslagene ser ikke F (`OppgaveFunksjonTests`).

**Juristrunden (CLAUDE.md §23).** Kandidater: K `samordningskompetanse`, R `sekretariat_for`, R `radgir`, P `bistandsplikt`, `annet:*`
med oppgaveordlyd og andre typer der sitatet har «skal sørge for/samordne/føre oversikt/veilede/være sekretariat/forvalte» — 189 i
fasit, KI-utdata og lokal base (pluss fire rader runde 1 viste at søket bommet på). Utfallet per kort står i
`data/fasit/strukturmodell/juristrunde-368-oppgave.json`.

| | Fasit | KI-utdata | Lokal base | Sum |
|---|---:|---:|---:|---:|
| F (raden erstattes) | 29 | 20 | 0 | 49 |
| begge (raden står, F legges til) | 9 | 3 | 1 | 13 |
| behold | 75 | 53 | 3 | 131 |

Runde 1: 141 holder, 48 innvendinger (21 middels, 27 lav, 0 høy, 0 prinsipielle). Modelløren aksepterte 48 og svarte delvis på 4;
juristen godtok alle i runde 2 (1 godtar, 9 presiseringer, 0 fastholder). Ingen uenighet går til Johann. Det runden endret:
sekretariat og rådgiver er R alene når teksten bare sier relasjonen (F bare med eget innhold — `radgivning` brukes derfor ikke i
fasiten); «sørge for at» avgjør ikke typen alene (P konsultasjon og P avtale i to kort); ny type `kontroll` for «påse at»; modalitet
null ved attributiv form; K oppnevning ved siden av F «overordnet ansvar» står som egen fortolkning (hotl. u91/u92, shl. u213). Funn
som ikke er F (R radgir som er P konsultasjon, P bistand som er informasjonsplikt, P samarbeid sluttet fra F i shl. u187/u188 m.fl.)
er meldt som egen sak (#380). [Johanns beslutning på PR #381, 2026-10-16] Ingen ny type for innsats («arbeide for», «fremme») eller for en
tjenestesektor som bærer; begge står som åpne spørsmål med eksempler i begrepsmanualen (§F) og avgjøres i barnehageloven-piloten (#375).

**Fasiten** (`konvertering-368-oppgave.py`, deterministisk, idempotent, også på `ki-utdata/`): 1 802 → 1 825 utsagn, 52 oppgaverader
(sorge_for 23, oversikt 8, veiledning 8, samordning 6, forvaltning 4, bistand 2, kontroll 1; fortolkning 50, tekstfunn 2). KI-utdata
1 042 → 1 051, 29 oppgaverader (ingen kunnskapsstatus, som i #367).

**Migrasjonen `OppgaveFunksjon`** (`OppgaveMigrering.cs`): CHECK-ene over, de ni F-typene i typekonfigurasjonen, og BARE juristrundens
utfall for de fire lokale kandidatene (R sekretariat_for fra KLAGENEMNDSSEKRETARIATET): tre behold (Energi- og Konkurranseklagenemnda:
«de oppgavene som lederen pålegger» er en henvisning, ingen F; Klagenemnda for utdanning: ingen hjemmeltekst), én begge
(Stiftelsesklagenemnda: «sekretariat som foretar saksforberedelse» → ny F sekretariatsfunksjon, kunnskapsstatus `avledet` med R-kanten som
premiss, status `maskinelt_foreslatt`, oppdagelseskilde `ki:juristrunde-368` — godkjennes i forslagskøen). Ingen kant flyttet. Teller
før/etter og avbryter ved avvik; `Down` sletter nøyaktig radene med `migrasjon-368`-proveniens og nekter når det finnes F-kanter
registrert etter migrasjonen. **Målt mot lokal `regelide` 2026-10-16 (kjørt ved API-oppstart):** 1 303 → 1 304 kanter, typekonfigurasjonen +9 (F), 1 ny F-kant (KNS sekretariatsfunksjon «saksforberedelse» → Stiftelsesklagenemnda, avledet, premiss = R-kanten 6d5b8668…), 1 proveniensrad, 0 kanter flyttet. `Down` er prøvd i `OppgaveMigreringTests` (ikke mot lokal base).

**Leksikonet** (2026-10-16.2) og mønsterlaget: `oppgave-fore-oversikt`, `oppgave-veilede`, `oppgave-pase`, `oppgave-forvalte`, og `samordning-samordne` (fra #341) gir nå samordningsOPPGAVE, ikke samordningskompetanse: Johanns beslutning om F
og juristrunden (energiloven u160 «Beredskapsmyndigheten skal samordne beredskapsarbeid»). K samordning står i typologien for tekst som
gir bindende virkning, men den kan ikke leses av ordet alene.

| Måling (samme treffregel) | Før #368 | Etter #368 |
|---|---:|---:|
| Fasitutsagn / KI-utsagn | 1 802 / 1 042 | 1 825 / 1 051 |
| Mønster alle: P / G | 92,5 / 47,9 % | 92,5 / 47,5 % |
| Mønster K (fasit 596 → 595): P / G | 87,3 / 47,5 % | 87,9 / 47,4 % |
| Mønster F (fasit 52): P / G | – | 66,7 / 7,7 % |
| — oppgave-fore-oversikt / samordning-samordne | – | 100 / 100 % · 66,7 / 100 % |
| KI alle: P / G | 36,8 / 21,3 % | 37,9 / 21,8 % |
| KI F: P / G | – | 51,7 / 28,8 % |
| Union alle: P / G | 58,2 / 55,6 % | 58,8 / 55,7 % |
| designtest.py revidert ja | 1 720 (95 %) | 1 745 (96 %), F 52 |

[ENDRET, Johanns beslutning på PR #381, 2026-10-16] **`oppgave-sorge-for` er fjernet fra mønsterlaget; typen `sorge_for` står.** Målt
med regelen: 36 treff, 5 riktige (13,9 % presisjon), mønsterlaget alle 89,6 / 47,8 %. 27 treff hadde ingen rad i fasiten, blant dem
saksbehandling («Retten skal sørge for at dom i straffesak blir forkynt»), og juristrunden viste at «sørge for at» også kan være P
konsultasjon eller P avtale. Den står under «Forkastede mønstre» i maling-monster.md. F-gjenfinningen i mønsterlaget er derfor lav (7,7 %);
sørge-for-ansvaret settes av juristrunder og KI-laget (KI F 51,7 / 28,8 %). KI-tallene er de lagrede utdataene fra #308 konvertert med samme skript, ikke en ny kjøring.

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

   **Bygget i #308 (2026-10-07):** `KiStrukturkonverterer` (samme `IStrukturkonverterer`) via
   `IKiAgentKlient`. Kategoriene/typene i instruksen og valideringen er samme liste
   (`Strukturkontrakt`, holdt lik FORMAT.md av en test). Kildene deles i kall à maks 6000 tegn
   nodetekst, aldri midt i en node og aldri på tvers av en ledsagende kilde (som #295); ugyldig JSON
   halverer kallet. Noder refereres med korte tagger (`[n17]`) som slås opp til ekte eId — #285 målte
   at modellen forkorter lange eId-er. Validering: sitat eksakt (ordinal) delstreng av noden, tagg i
   kallets egne inndata, kategori/type i lista for kategorien eller `annet:<x>`, fra/til må peke på en
   aktør i samme svar, aktørens tekstform og varianter må stå i kallets tekst, polaritet må være satt
   (ingen standardverdi). `oppdagelseskilde = "ki:<modell>"`. Live-målingen er gated
   (`LiveIntegration` + `REGELIDE_KI_LIVE_MALING=1`), og utdataene lagres i
   `data/fasit/strukturmodell/ki-utdata/`, så rapporten regenereres uten nettverk.

### 5.3 Til databasen bare som forslag

Uttrekket skrives aldri som `validert`. Det går inn som `foreslatt_av_ai` (eller `monster`-proveniens)
i forslagskøen for strukturkanter, og et menneske godkjenner — samme prinsipp som `docs/20` §2.7.

**Bygget i #313 (2026-10-15).** `StrukturforslagTjeneste` (`src/RegelIde.Data/Strukturkonvertering/`) og
`POST /api/rettskilder/{id}/strukturforslag?lag=monster|ki`:

- **Grunnlag:** rettskildens egne gjeldende, ikke opphevede noder. En ledsagende forskrift lastes som egen rettskilde, fordi et utsagn bare
  har en eId. [ENDRET, #419] Merknader og veiledning i forskriftsteksten er ikke med, og et utsagn med slik eId lastes ikke (årsak
  `hjemmel_er_merknad`). Rapporten teller dem (`merknadsnoderUtelatt`). Se §5.14.
- **Skrivevei:** hvert utsagn går gjennom `StrukturkantTjeneste.OpprettAsync` med `status = maskinelt_foreslatt` og
  `oppdagelses_kilde = monster:<id> | ki:<modell>`. Feltene følger med: objekt, polaritet, avgrensning, normform, undertype, modalitet,
  grunnlag, delegeringsadgang/-slutning og kunnskapsstatus (mønsterlaget: `normalisering`, begrunnelse «Regel: monster:<id>»; KI: ingen).
  `hjemmel_node_id` settes av skriveveien. Sitatet står i `kommentar` («…»). `betinget`, `sikkerhet` og `kilde_utenfor_korpus` har ingen
  kolonne og følger ikke med.
- **Typene:** kompetanse og plikt via `KompetansetypeFraFasit`/`PlikttypeFraFasit`; relasjon `del_av` → G; konstituerende `oppretter`/
  `avvikler` → R, `skal_finnes` → T `skal_ha`; organsammensetning → G. Uten databasetype (ikke lastet, årsak `type_uten_typekode`):
  `annet:*`, kompetanse `ukjent` og medlemskap `inngar_i`. [ENDRET, #378 C9] Områdesammensetning `del_av` snus til O `bestar_av` med fra og til
  byttet (`Strukturkontrakt.NormaliserOmradesammensetning`), og mønsteret `inndeling-kommune-i-fylke` gir nå selv «X fylke bestar_av kommunen».
  KI-forslag om medlemskap sendes ikke (§5.5).
- **Aktører:** bare eksakt treff (uten skille på store/små bokstaver) på tekstformen eller en variant: registernavn eller navneform
  (`VirksomhetOppslagTjeneste`), eller termen til et gjeldende klasse-, rolle-, område- eller gruppebegrep. [ENDRET, #378] Et lovspesifikt
  begrep treffer bare i sin egen lov og i forskrifter med hjemmel i den, og en generisk navneform løses via bindingen (§5.7). Ingen treff → navnekandidat
  (kategori `gruppe`, oppdagelseskilde `strukturforslag`) og årsak `aktor_ikke_i_katalogen`; flere treff → `aktor_flertydig`. Et utsagn
  uten fra → `fra_ikke_i_teksten`. Det skriveveien avviser, telles som `avvist_av_valideringen` med meldingen.
- **Idempotens:** nøkkelen er hjemmel + eId + kategori + typekode + fra + til + undertype + normform, pluss polaritet og modalitet. Finnes
  nøkkelen (forslag, godkjent eller avvist), lages ingen rad; utfallet blir `fantes_som_forslag`, `fantes_godkjent` eller
  `tidligere_avvist`. Et avvist utsagn foreslås ikke på nytt (#374). `OpprettAsync` returnerer også selv den avviste raden for et identisk
  maskinelt forslag; et menneske som registrerer det samme, får en ny rad.
  - **[ENDRET, #420, Johanns valg 2026-10-29] Setningen hører til nøkkelen, og paringen er en-til-en.** Nøkkelen over manglet hva utsagnet
    gjaldt. To utsagn med samme aktør og type i samme ledd ble derfor ett: pålegget og stengingen i bhl. § 53 andre ledd, og tilsvarende i
    § 55 andre ledd og to steder i finansieringsforskriften. I pilotlastingen 2026-10-29 ble 4 utsagn i mønsterlaget og om lag 10 i KI-laget tapt.
    Nå er nøkkelen den samme pluss **setningsnummeret** til sitatet: den setningen sitatet starter i, med mønsterlagets setningsdeling
    (`MonsterStrukturkonverterer.Setningsnummer`). Hvert utsagn bruker opp høyst én eksisterende rad med samme nøkkel, og hver rad brukes
    høyst én gang per kjøring. Rekkefølgen er:
    1. eksakt samme sitat og objekt (mønsterlaget er deterministisk, så en ny kjøring parer alt her);
    2. samme setning (KI omformulerer objekt og sitatspenn, men holder seg i setningen);
    3. en rad eller et utsagn der setningen er ukjent.

    Et utsagn uten ledig rad blir et nytt forslag. Ulike setninger er altså ulike utsagn, og flere utsagn i samme setning enn det finnes rader
    gir nye forslag (§ 23 a andre ledd). `dublett_i_kjoringen` betyr nå identisk innhold: skriveveiens identitet, med objektet, traff en rad
    som alt er laget eller brukt i samme kjøring.
  - **Sitatet** leses fra `kommentar` («sitat» eller «sitat» — kommentar) og har ingen egen kolonne (Q2 = a, ingen migrering). Setningen regnes i
    den gjeldende nodeteksten. Rader uten sitat på den formen (fasit, manuell, de fleste livsløps- og inndelingsrader), og rader der sitatet
    ikke lenger står i noden, har ukjent setning og blokkerer som før.
  - **Avviste alternativer:**
    - *Objekt i nøkkelen:* KI omformulerer objektet. 0 av 6 par fra KI og mønsterlaget i samme setning i piloten hadde likt objekt, så nesten
      alle KI-forslag ville kommet på nytt ved neste kjøring. I korpuset skiller objektet dessuten bare 547 av 566 falske dubletter i
      mønsterlaget, mot 565 for setningen.
    - *Tegnspenn eller sitat-hash:* KI-sitatet er hele setningen i bare 44 av 129 KI-rader i piloten.
    - *Setningsnummer alene:* mister § 23 a, der to utsagn står i samme setning.
  - **Kjent restrisiko:** siterer KI en annen setning for samme utsagn, blir det et nytt forslag. Det gjelder også et utsagn som er avvist i
    den første setningen. Følgen er en synlig dublett i køen, ikke et stille tap.
  - **Eksisterende data** (målt i den delte basen 2026-10-29, bare SELECT): 251 av 252 rader skrevet av lasteren har et sitat som står i noden.
    Ingen av dem kolliderer under den nye nøkkelen. En ny mønsterkjøring av piloten gjenskaper alle 81 mønsterrader eksakt (sitat og objekt).
    De tapte utsagnene lastes ved neste pilotlasting (#375); de fra KI-laget bare hvis KI gir dem igjen. Testene står i
    `StrukturforslagTjenesteTests` (#420-delen).
- **Kaldsjekk, domstolloven (mønsterlaget, lokal base 2026-10-15):** 954 noder, 77 utsagn. K 71: 42 nye forslag, 1 dublett i kjøringen,
  28 ikke lastet. P 1, A 1 og G 4: ikke lastet. Ikke lastet med årsak: 32 `aktor_ikke_i_katalogen` (23 aktører, alle lagt i
  navnekandidatkøen: blant annet «Høyesterett» (katalogen har «Norges Høyesterett»), «Innstillingsrådet», «domstollederen»,
  «Tilsynsutvalget», «styret»), 2 `fra_ikke_i_teksten`. To godkjent og to avvist (én med begrunnelse) via API-et, aktørsiden viste de
  godkjente og skjulte de avviste (2 med `visAvviste=true`). Ny kjøring: 0 nye, 38 fantes som forslag, 2 fantes godkjent, 2 tidligere
  avvist, ingen nye rader. Testgodkjenningene og -avvisningene er tilbakestilt; de 42 forslagene og 23 navnekandidatene står igjen lokalt
  som ordinære forslag. KI-laget er ikke kjørt i kaldsjekken.
- **Pilot, barnehageloven med 19 forskrifter (#375, lokal base 2026-10-28, begge lag):** 472 utsagn, 71 nye forslag (38 mønster, 33 KI). Ikke lastet: 240 aktør ikke i katalogen, 103 avvist fordi K ikke tar klasse som fra (#418), 17 uten fra-aktør, 10 uten databasetype. Detaljer og sammenligning med BU: docs/profil/07.
- **Ny lasting etter #418 (lokal base 2026-10-29, begge lag):** 468 utsagn, 139 nye forslag (43 mønster, 96 KI), hvorav 106 K med klasse som fra. Ingen avvist for klasse som fra. 13 tapt som falske dubletter fordi idempotensnøkkelen mangler objekt (#420, rettet: setningen er nå med i nøkkelen, se Idempotens over). KI-laget er kjørt på nytt og er ikke deterministisk. Detaljer: docs/profil/07 §8.

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

**Målt 2026-10-07 (#308, KI-laget)** — én live-kjøring med `deepseek-ai/DeepSeek-V4-Flash` via
HostYourAI, instruksen ikke iterert mot fasiten. Full rapport (per type, kastede rader med
eksempler, falske positive/negative): `data/fasit/strukturmodell/maling-ki.md`. Samme treffregel
som mønsterlaget. Union = alle mønsterutsagn + KI-utsagn som ikke dupliserer et mønsterutsagn.

| Kanttype | Fasit | Mønster P / G | KI P / G | Union P / G | Bare mønster | Bare KI |
|---|---:|---:|---:|---:|---:|---:|
| R relasjon | 223 | 77,8 / 12,6 % | 24,4 / 14,3 % | 34,5 / 25,6 % | 25 | 29 |
| K kompetanse | 427 | 88,5 / 59,7 % | 45,1 / 71,2 % | 46,3 / 78,0 % | 32 | 81 |
| — herav forskriftskompetanse | 205 | 91,9 / 88,8 % | 89,8 / 85,9 % | 89,3 / 93,2 % | 16 | 10 |
| — herav vedtakskompetanse | 158 | 80,0 / 43,0 % | 28,8 / 65,8 % | 30,4 / 75,3 % | 16 | 52 |
| — herav tilsynskompetanse | 16 | 100 / 31,3 % | 68,8 / 68,8 % | 71,4 / 62,5 % | 0 | 6 |
| M medlemskap | 48 | – / 0 % | 4,5 / 2,1 % | 4,5 / 2,1 % | 0 | 1 |
| O områdesammensetning | 578 | 97,4 / 85,8 % | 0 / 0 % | 95,4 / 85,8 % | 496 | 0 |
| A ansvarsområde | 120 | 100 / 51,7 % | 0 / 0 % | 54,9 / 51,7 % | 62 | 0 |
| G organtilhørighet | 43 | 66,7 / 9,3 % | 12,2 / 11,6 % | 12,8 / 14,0 % | 1 | 2 |
| T skal finnes | 21 | – / 0 % | 14,3 / 9,5 % | 14,3 / 9,5 % | 0 | 2 |
| `annet:*` | 366 | – / 0 % | 1,7 / 0,3 % | 1,7 / 0,3 % | 0 | 1 |
| senere lag | 39 | – / 0 % | 35,9 / 35,9 % | 35,9 / 35,9 % | 0 | 14 |
| **Alle** | **1865** | **93,8 / 45,3 %** | **34,4 / 19,2 %** | **57,2 / 52,1 %** | 616 | 130 |

| Kilde | Mønster P / G | KI P / G | Union P / G |
|---|---:|---:|---:|
| Domstolloven | 95,7 / 62,3 % | 16,3 / 6,7 % | 63,5 / 64,5 % |
| Energiloven | 94,7 / 42,5 % | 57,5 / 48,0 % | 60,8 / 57,5 % |
| Helse- og omsorgstjenesteloven | 91,8 / 23,4 % | 48,9 / 33,9 % | 48,1 / 33,3 % |
| Sameloven | 87,7 / 40,0 % | 27,1 / 6,7 % | 66,8 / 43,9 % |
| Spesialisthelsetjenesteloven + helseforetaksloven | 89,6 / 19,9 % | 34,2 / 32,1 % | 34,0 / 33,1 % |

Kostnad og drift: 79 kall, 259 316 tokens inn og 236 712 ut. Det gir ≈ 0,16 EUR for alle fem kildene
etter HostYourAIs listepris lest 2026-10-07 (0,29 €/M inn, 0,35 €/M ut, øvre grense fordi
hurtigbufrede tokens ikke rapporteres). Summert kalltid var 19 min, med 4 kall samtidig. 0 kall
returnerte ugyldig JSON. Ett kall feilet med 504 hos leverandøren, og da gikk 28 noder i en
domstolloven-forskrift (2021/01/22/163) tapt.

Valideringen kastet 272 av 1315 rader (21 %):

- falskt sitat: 91 (77 parafrase eller oppdiktet, 14 med feil tagg)
- ugyldig felt: 71 (f.eks. `sikkerhet: "høy"`)
- ugyldig aktør: 52. Aktørene som ble avvist, fordeler seg slik: ukjent oppløsning 17, tekstformen står
  ikke i teksten 14, ugyldig felt 9, varianten står ikke i teksten 2.
- ukjent type: 21
- ukjent eId: 17
- ukjent kategori: 14
- ukjent aktørreferanse: 4 (blant annet `"fra": "null"` som streng)
- duplikat: 2

Lesning:

- **KI er ikke uttømmende på lister.** Inndelingslistene i domstolloven og sameloven, O og A, blir
  oppsummert og ikke listet. Det gir 0 treff, og derfor er KI-gjenfinningen 6,7 % i begge kildene.
- **KI når der mønstrene ikke gjør det.** I de to helselovene og energiloven gir KI 21–40 treff hver
  som mønsterlaget ikke fant.
- **Rapporttallene i `annet:*` sier lite.** Treffregelen krever samme fritt valgte typenavn, og det
  blir nesten aldri likt.

**[ENDRET, issue #312, 2026-10-08] Fasitrettelse for domstollovens inndelingsdel** (Johanns funn fra fasitkontrollen,
`data/fasit/strukturmodell/rettelse-312-domstolinndeling.py`): rettskrets-aktørene er fjernet, de 357 kommunelisteradene
er «tingrett `har_ansvarsomrade` kommune», «lagsogn består av rettskrets» er «tingrett `annet:sogner_til` lagsogn»,
56 rader som bare beskrev rettskretsen er fjernet, 45 rettssteder er slått sammen med kommunen og 16 er tettsted +
`del_av`. Mønstrene fulgte: `inndeling-rettskrets` gir nå A, `inndeling-sogner` gir `annet:sogner_til`,
`inndeling-har-rettskretsen` er fjernet. Tallene i tabellene over er fra FØR rettelsen.

| Måling (mønsterlaget) | Før | Etter |
|---|---:|---:|
| Fasitutsagn (alle fem) | 1 865 | 1 797 |
| Alle: presisjon / gjenfinning | 93,8 / 45,3 % | 93,8 / 46,9 % |
| O: fasit, P / G | 578, 97,4 / 85,8 % | 177, 89,3 / 61,6 % |
| A: fasit, P / G | 120, 100 / 51,7 % | 449, 100 / 93,3 % |
| Domstolloven: fasit, P / G | 832, 95,7 / 62,3 % | 764, 95,7 / 67,5 % |
| KI alle P / G (samme utdata) | 34,4 / 19,2 % | 34,4 / 20,0 % |

Tersklene i `MonsterStrukturkonvertererMalingTests` er satt til de nye O/A-verdiene. O-gjenfinningen faller fordi de
357 lett-funne radene flyttet til A; det som står igjen i O er vanskeligere.

**[ENDRET, issue #341, 2026-10-08] Fasiten og konverteringen på kompetansemodellen.** Fasiten er konvertert deterministisk
(`konvertering-341-kompetanse.py`, idempotent, samme skript på `ki-utdata/`): 1 797 → 1 797 utsagn, relasjon 384 → 276,
kompetanse 522 → 630. 205 forskriftskompetanser → normgivningskompetanse/forskrift; 108 myndighetsrelasjoner → kompetanse
med motpart (oppnevning 23, instruksjon 21, klage 17, delegeringskompetanse 16, tilsyn 10, avsetting 5, overprøving 7,
omgjøring 3, forelegging 2, sanksjon 2, samtykke 1, revisjon 1); 20 `delegerer_til` i delegeringsvedtak →
`har_delegert_til`; `delegerbar` satt på 104 kompetanser etter #335-regelen på sitatet (89 true, 15 false). Ikke konvertert
(avventer Johann): velger 17, radgir 10, bistar 15, samarbeider_med 24, administrativt_underordnet 5, del_av 9,
annet:ankeinstans_for 3, annet:forelegges_for 2, annet:intern_forelegging 2, oppretter 14, avvikler 2.

**[ENDRET, issue #352, 2026-10-08] Restene konvertert** (`konvertering-352-oppnevning.py`, deterministisk, idempotent, samme
skript på `ki-utdata/`): fasit 1 797 → 1 797 utsagn, relasjon 276 → 256, kompetanse 630 → 650. velger 17 → oppnevning/valg,
utpekingskompetanse 21 → oppnevning/utpeking, ansettelseskompetanse 5 → oppnevning/ansettelse, annet:ankeinstans_for 3 →
overprøving/anke, 5 vedtak → godkjenning (leksikonregelens to uttrykk), 21 eksisterende oppnevningsutsagn fikk undertypen
fra sitatet (17 oppnevning, 2 ansettelse, 2 utpeking), domstolloven u17 har_medlemmer → settes_med. administrativt_underordnet,
radgir, oppretter og avvikler står (beslutning 4). Fortsatt ikke avgjort: bistar 15, samarbeider_med 24, del_av 9,
annet:forelegges_for 2, annet:intern_forelegging 2, annet:ankekompetanse 2 (en parts rett til å anke), annet:overordnet_domstol 2.
KI-utdata 1 043 → 1 043. Rapportene: mønster 93,8/47,1 % → 93,9/47,2 %; KI 36,1/21,0 % → 36,6/21,3 %; union 58,0/54,8 % →
58,3/55,1 %.

**Leksikonet** (`src/RegelIde.Data/Strukturkonvertering/kompetanseleksikon.json`, versjonert, innebygd) er Johanns
beslutning 3: det tilordner hvert lovuttrykk mønsterlaget kjenner til kategori, type, normform og familie; `Monsterkatalog`
henter betydningen derfra ved mønster-id (regex-en står i koden). Datafil framfor tabell fordi mønsterlaget måles uten
database og en regel bare gir mening sammen med regex-en sin. Nye regler: «beslutningsmyndighet» → beslutning, «samordne» →
samordning. KI-instruksen lister leksikonet og skal bare foreslå for uttrykk det ikke kjenner; et kompetanseuttrykk ingen
av dem kan typebestemme, blir `ukjent`. At et godkjent KI-forslag blir en ny leksikonregel, er ikke bygget (oppfølgingssak).

| Måling (samme treffregel) | Før #341 | Etter #341 |
|---|---:|---:|
| Mønster alle: P / G | 93,8 / 46,9 % | 93,8 / 47,1 % |
| Mønster R (fasit 223 → 139): P / G | 77,8 / 12,6 % | 75,0 / 2,2 % |
| Mønster K (fasit 427 → 557): P / G | 88,5 / 59,7 % | 87,7 / 51,0 % |
| Mønster forskrift → normgivning: P / G | 91,9 / 88,8 % | 92,9 / 87,6 % |
| Mønster klage (R klageinstans_for → K klagekompetanse): P / G | 55,6 / 29,4 % | 77,8 / 31,8 % |
| KI alle (samme utdata, konvertert): P / G | 34,4 / 20,0 % | 36,1 / 21,0 % |
| Union alle: P / G | 57,1 / 54,0 % | 58,0 / 54,8 % |
| designtest.py revidert ja | 91 % (1 640) | 91 % (1 642) |

R-gjenfinningen faller fordi R nå bare er struktur (mønsterlaget kjenner bare delegeringsvedtakets form); K-nevneren vokste
med de flyttede radene. De to nye mønstrene treffer 3 utsagn fasiten ikke har (Sametinget har beslutningsmyndighet;
kommunen/RHF skal samordne) — sannsynlige fasitutelatelser, ikke målt som riktige. KI-tallene er de lagrede utdataene fra
#308 konvertert med samme skript, ikke en ny kjøring med den nye instruksen.

### 5.5 Anbefalt fordeling mellom mønster og KI (#308)

Fordelingen bygger på tallene over: én kjøring, en fasit som ikke er verifisert (#309) og én modell.
Den gjelder hvilket lag som skal levere forslag til forslagskøen (#313), og med hvilken tillit. Det
betyr ikke at noe skal lagres som validert (§5.3).

| Kanttype | Lag | Tillit i forslagskøen (#313) | Begrunnelse |
|---|---|---|---|
| O områdesammensetning | **Mønster alene** | normal | Mønster 97,4 / 85,8 %. KI 0 treff (oppsummerer lister) og bidrar ikke med noe. |
| A `har_sete_i` | **Mønster alene** | normal | Mønster 100 / 95,4 %. KI predikerte ingen. |
| A `har_ansvarsomrade` / `har_jurisdiksjon` | **Ingen av dem nå** → områderegisteret (#312/#314) | — | 55 i fasiten. Mønster 0 og KI 0 av 51 riktige. Fasitens konvensjon («tingrett har ansvarsområde = egen rettskrets») står ikke i teksten. |
| K forskriftskompetanse | **Mønster først, KI som supplement** | mønster normal. KI-rader uten mønstertreff i samme node: normal, merket `ki:` | Lagene er like gode (91,9 / 88,8 % mot 89,8 / 85,9 %). Unionen hever gjenfinningen til 93,2 % med 89,3 % presisjon. |
| K vedtakskompetanse | **Mønster + KI** | KI: **lav tillit, egen merking** | KI gir 52 treff mønsteret ikke har (G 43 → 75 % i union), men bare 28,8 % presisjon. Rundt 7 av 10 KI-forslag er feil. |
| K tilsynskompetanse | **Mønster + KI** | KI: middels | KI 68,8 / 68,8 % mot mønster 100 / 31,3 %. Grunnlaget er lite (16 i fasiten). |
| K utpeking/oppnevning/instruksjon/klage/delegering | **KI alene** (ingen mønstre) | **lav tillit, egen merking** | Presisjon 3,7–26 %, gjenfinning 23–40 %. KI-forslagene er hovedsakelig støy, men de er det eneste laget som finner noe. |
| R relasjon, typer med mønster (`delegerer_til`, `instruksjon`, `klageinstans_for`, `oppnevner`) | **Mønster** | normal (`klageinstans_for`/`oppnevner`: lav, jf. #307) | Mønster 55–100 % presisjon. KI 0–33 % og tilfører 0–2 treff per type. |
| R relasjon, typer uten mønster (`rapporterer_til`, `velger`, `ledes_av`, `eies_av`, `etterfolger`, `radgir`, `oppretter`, …) | **KI alene** | **lav tillit, egen merking** | R samlet: KI 24,4 / 14,3 %. `rapporterer_til` 31 %, `oppretter` 57 %, `etterfolger` 60 %, men på få utsagn. KI gir 29 R-treff mønsteret ikke har. |
| M medlemskap | **Ingen automatikk nå** | KI-forslag sendes **ikke** | KI 4,5 / 2,1 % er støy. Medlemskap i fasiten er mest klasser og områdeutvidelser, og det hører til #310/#314. |
| G organtilhørighet, T skal finnes | **KI** | **lav tillit, egen merking** | KI 12–14 % presisjon, 2 treff hver som mønsteret ikke har. Mønster `har_medlemmer` 66,7 % (#307). |
| `annet:*` | Ikke målbart med dagens treffregel | **lav tillit**, som hull-signal | Fritt typenavn treffer nesten aldri eksakt (1,7 %). Verdien ligger i at de avslører hull i modellen (FORMAT.md), ikke i treff. |

**Konklusjon:** Mønsterlaget bærer O, A `har_sete_i` og K forskriftskompetanse. KI bidrar mest i
de kildene og typene der formuleringene varierer: de to helselovene, energiloven, K vedtak og R-typer
uten mønster. Presisjonen er for lav til normal tillit, med ett unntak (forskriftskompetanse).

Alle KI-forslag utenfor forskriftskompetanse bør derfor merkes **lav tillit** i #313 og vises
adskilt fra mønsterforslag. Valideringens kassasjoner bør ikke «repareres» for å øke tallene (21 %
kastet).

Før tersklene settes, trengs to ting:

- en ny KI-måling etter fasitgjennomgangen (#309)
- en beslutning om KI-svar skal låses med strukturert utdata eller JSON-skjema hos leverandøren.
  Begge deler kan redusere `ugyldig_felt`/`ugyldig_aktor`.

### 5.7 Aktørsveip og generiske navneformer (#378, bygget 2026-10-16)

Johanns spørsmål 2026-10-15: navneformene skal kartlegges FØR forslagene, ikke samtidig (på domstolloven falt 34 av 77 utsagn bort fordi
aktøren ikke var kjent), og generiske omtaler («styret», «retten», «departementet») skal bli navneformer for hele korpuset med betydningen
bundet per rettskilde.

**Steg 1 — aktørsveip** (`POST /api/rettskilder/{id}/aktorsveip?lag=monster|ki[&navnekandidater=true]`, knappen «1. Aktørsveip» i
forslagskøen): samme konvertering som lasteren, men bare aktørene klassifiseres. Ingen forslag skrives. Statusene (`Aktorstatus`):

| Status | Når |
|---|---|
| `kjent` | Eksakt treff på én node, eller en generisk navneform med godkjent binding for alle forekomstene |
| `ny_navneform_for_kjent_aktor` | Ikke eksakt treff, men tekstformen står som hele ord inni et kjent registernavn eller en navneform («Høyesterett» i «Norges Høyesterett»). En kandidat, ikke en kobling |
| `ny_aktor` | Verken treff eller kandidat |
| `generisk` | Generisk navneform finnes, men minst én forekomst er ikke dekket av en godkjent binding |
| `flertydig` | Flere eksakte treff, eller to like smale bindinger med ulik referent |

Rekkefølgen er eksakt → generisk med binding → flertydig → kandidat → ny aktør. Med `navnekandidater=true` legges alle som ikke er kjent
eller flertydige i navnekandidatkøen (oppdagelseskilde `strukturforslag`); uten er sveipet en ren tørrkjøring. Gjennomgangen skjer i den
eksisterende veiviseren (docs/09 §38). **Steg 2** er lasteren (§5.3), som bruker samme klassifisering; lasterapporten viser aktørene per
status, så den kan sammenlignes med sveipet før gjennomgangen. Ny årsak: `aktor_generisk_uten_binding`.
[ENDRET, #440] Bare aktører som aktørkøens ruting ikke kjenner, blir navnekandidater (§5.16); hver rad har `aktorkoRute`. [ENDRET, #440,
Johanns avgjørelse 2026-10-31] Unntak: et generisk ord uten binding (status `generisk`, rute `generisk`) blir fortsatt navnekandidat, så
«Behandle» åpner én-skjermen for generisk omtale (docs/09 §42, §50). Andre kjente ord holdes utenfor.

**Generisk navneform:** `navneformgrunn = 'generisk'`, kategori `virksomhet`, ingen `virksomhet_referanse_id`
(CHECK `ck_begreper_generisk_uten_referanse`), termen med små bokstaver, én per term for hele korpuset (`ux_begreper_generisk_term`). En
term som alt er navneform for én virksomhet, kan ikke gjøres generisk (oppslaget ville blitt flertydig).

**Binding** (`navneform_bindinger`, `NavneformBindingEntitet`): (navneform, rettskilde, valgfritt paragrafspenn) → referent (virksomhet,
eller begrep av typen klasse eller rolle). Et modellutsagn med samme egenskaper som strukturkantene etter #367: hjemmel i korpus ELLER kilde
utenfor korpus (type og dokumentasjon), kunnskapsstatus (docs/profil/03 §5: `tekstfunn` når loven definerer ordet, ellers `fortolkning` med
tolkningsargumenter, eller `eksternt_dokumentert`; `avledet` og `foreldet` avvises), godkjenning, avvisning med hvem/hvorfor (slettes ikke).
[Ny, #390] En kortform for en aktør kan også bindes, og da også til et område (§5.13). Bare godkjente bindinger oppløser ([ENDRET, #394, Johanns beslutning 2 2026-10-23] godkjente OG faglig vurderte; se §5.10); det smaleste spennet som dekker noden vinner (et spenn dekker nodene fra–til i dokumentrekkefølge og alt
under dem; tomt = hele rettskilden). [ENDRET før merge, Johanns beslutning 2026-10-16 etter juristrunden B1] Bindingen har egen gyldighet
(`gyldig_fra`/`gyldig_til`): «departementet» følger ressortfordelingen ved kgl.res., som kan endres uten at loven endres (docs/profil/03 §8).
Oppslaget bruker bare bindinger som gjelder på datoen (standard i dag). Samme virkeområde kan ha bindinger etter hverandre i tid;
overlappende gyldighet avvises i tjenesten (ingen unik indeks). Brukes av
lasteren, taggvisningen (`GET /api/rettskilder/{id}/generiske-tagger`) og virksomhetens «brukt i». Oversikt (AC7):
`GET /api/navneformer/generiske[?term=]` — hvor termen er tagget eller bundet, og hva den betyr i hver rettskilde. Hva en generisk omtale
betyr i den enkelte SAKEN (hvilken kommune), er #314 og ikke bindingen.

**Målt på barnehageloven og forskriftene (lokal base 2026-10-16, mønsterlaget, tørrkjøring — ingen forslag lastet):** se PR #383 og #378.

**[Ny, issue #384, Johanns beslutning 2026-10-16] Bindingen følger verket, ikke versjonen.** En ny konsolidert versjon er en ny
rettskilde-rad med ny id (§2.1), og før #384 falt bindingen da bort. Nå gjelder bindingen VERKET. Verket er (`virksomhet_id`, `eli`) på
rettskilden: det samme paret importen bruker for å finne forrige versjon, og som de unike indeksene `ux_rettskilder_eli_gjeldende_*`
håndhever. `rettskilde_id` på bindingen er versjonen den ble registrert og vurdert mot, så versjonen er registrert.
- **Ingen ny kolonne og ingen migrasjon for dette.** ELI-en er målt lik over alle 1 113 versjonsoverganger i lokal `regelide`
  (2026-10-16).
- **Uten ELI er verket rettskilden selv.** Det gjelder 28 lokale virksomhetsdokumenter, og ingen av dem er versjonert.
- **Alt som leser bindinger, ser hele verket:** oppslaget, konflikten «samme virkeområde», listen per rettskilde, oversikten (AC7) og
  virksomhetens «brukt i».
- **En binding for hele rettskilden** følger verket uten videre.
- **En binding med paragrafspenn** følger bare når spennet dekker nøyaktig de samme eId-ene i begge versjonene. Ellers brukes den ikke i den
  nye versjonen, og det gjettes ikke hvilke noder den nå skulle dekke (teksten kan være omnummerert, CLAUDE.md §8).
- **[ENDRET, Johanns beslutning på PR #388] En binding som ikke kan flyttes, blokkerer bare seg selv.** De andre gyldige bindingene for
  navneformen brukes som før (smaleste spenn vinner). Dekker ingen av dem noden, er oppslaget `spenn_endret` med den uflyttbare bindingen,
  ikke `ubundet`.
- **Bindinger som ikke kan flyttes, listes** (`GET /api/navneform-bindinger/ikke-flyttbare`). Et menneske registrerer dem på nytt: avvis
  den gamle, og opprett en ny mot den gjeldende versjonen.
- **Lokalt 2026-10-16:** 0 bindinger, altså 0 å flytte og 0 på listen.

**[Ny, issue #384] Den felles klassen «kommune»** er referenten for den generiske navneformen «kommunen». Klassen
(`Klasseregler.KommuneKlasseId`) er hjemlet i kommuneloven § 2-1 første ledd («Norge er inndelt i kommuner og fylkeskommuner …»; [ENDRET, Johanns beslutning på PR #388: leddet, ikke hele paragrafen]) med kunnskapsstatus `tekstfunn`, og migrasjonen
`KlasseKommuneOgUtledetMedlemskap` oppretter den.
- **Medlemskapet utledes og lagres ikke** (`begreper.medlemskapsregel = 'omraderegister_kommune'`, `KlassemedlemskapTjeneste`). Medlemmene
  er kommunene i områderegisteret med sitt rettssubjekt. Regelen er den samme som rubrikken «kommune» i tilhørighetsoppslaget (#312).
- **Hvert medlemskap er `avledet`**, med territoriekanten som premiss. Ingen eller flere kandidater for en kommune gir et hull, ikke et
  medlem.
- **Medlemmene vises i** `GET /api/gruppebegrep/{id}/utledet-medlemskap`.
- **Lokalt 2026-10-16:** 357 av 357 kommuner er medlem, med 0 hull. Alle 357 premisser er godkjent.
- **Pliktoppslaget (§4.5, S6) og ankeinstansoppslaget** bruker det utledede medlemskapet. En P-kant fra «kommune» gjelder kommune X med
  grunnlaget `medlem_av` (avledet), ikke `klasse_uten_registrert_medlemskap`.
- **Den lovspesifikke klassen «kommunene»** i reindriftsloven (§ 2 annet ledd) beholdes. Den henviser til den felles klassen med
  `M medlem_av` «kommune», lagt inn som forslag (`manuelt_foreslatt`, tekstfunn) av `OmraderegisterSeed` steg 6.

### 5.8 Aktøridentitet: lovens navn er primært, registeret er attributt (#385, bygget 2026-10-16/23)

Johann 2026-10-16: «Strengt tatt så er orgnummer og orgnavnet i Brreg kun attributter.» Aktørens identitet og primærnavn kommer fra
rettskildene. Migrasjonen `Aktoridentitet`:
- **Navneformen er et modellutsagn** (`begreper`): `godkjenning` (vokabularet fra §4.6; NULL = registrert før #385, regnes som gjeldende,
  ingen gjettet status), hjemmel = `lovkilde_id` + `lovreferanse_eid`, kilde utenfor korpus med `kilde_utenfor_korpus_lenker` (én eller
  flere), `begrunnelse`, `tolkningsargumenter`, `oppdagelses_kilde`, `avvisning_begrunnelse`. Avvist = `godkjenning avvist` +
  `entitetsstatus arkivert` (slettes ikke; taggene som pekte på forslaget, slettes). `ck_begreper_kunnskapsstatus` tar nå også
  fortolkning og eksternt dokumentert.
- **Visningsnavnet** er bare en GODKJENT gjeldende navneform (`VirksomhetVisningsnavnTjeneste`, `StrukturkantTjeneste`); ellers
  registernavnet, merket «Navneform mangler». Brukes også i S-oppslagene (ankeinstans, kommunens ansvarlige), utledet medlemskap,
  bindingens referent og aktørsveipets referent.
- **Aktørtypen er et modellutsagn** (`aktortype_utsagn`): forslag med grunnlag og status. `virksomheter.aktortype` settes først ved
  godkjenning (eller av KOMM/FYLK-regelen fra #310, eller direkte av et menneske på aktørsiden).
- **Forslag til gjeldende navneform** (`POST /api/aktorer/navneformforslag`, AC1): registernavnet slås opp i lovteksten (hele ord, uten
  skille på store/små bokstaver). Én egennavnform (stor forbokstav, ikke bare versaler) gir forslag: `tekstfunn` når den er lik
  registernavnet, ellers `normalisering` med regelen i begrunnelsen. Flere skrivemåter («Norges Bank»/«Norges bank») gir ingen.
  Registernavnet normaliseres ALDRI uten en tekst som viser skrivemåten.
- **Navnetreff** (AC3 og Johanns presisering 2026-10-16): når aktørsveipet (skrivemodus) eller lasteren finner en kjent aktør, registreres
  navneformen som forslag når treffet gikk via registernavnet, navneformen får kilden der navnet står, og spennet tagges der teksten er
  NØYAKTIG lik navneformen. Konverteringens entitetstype gir et aktørtypeforslag (`maskinelt_foreslatt`, kunnskapsstatus ikke vurdert).
  Treff som alt finnes (strukturkanter med hjemmel-eId og en aktør i en ende) etterfylles med `POST /api/aktorer/etterfyll-kilde-og-tagger`.
- **Bindingene** har «hjemmel endret» (AC12) etter samme regel som kantene: hjemmelen eller spennet har annen tekst i den nye versjonen av
  verket. Et tomt spenn (hele rettskilden) flagges [ENDRET, #390, Johanns tillegg 2026-10-23] bare når en node med en tagget forekomst av navneformen endres (§5.13). «Hjemmel mangler» er lov som uavklart med begrunnelse.
- **Målt på en kopi av lokal `regelide` 2026-10-23:** 55 aktører uten gjeldende navneform → 31 forslag, 11 har navneformen uten grunnen
  «gjeldende», 7 uten treff, 6 med flere skrivemåter. Etterfyllingen: 133 kanttreff for 61 aktører → 53 kilder, 103 nye tagger (27
  fantes), 3 ikke tagget (annen skrivemåte), 0 uten eier.

### 5.9 Organiseringskildene og departementenes livsløp (#395, bygget 2026-10-23)

**Disposisjonstype** (`rettskilde_disposisjonstyper`, `Disposisjonstyper.cs`): lukket liste `oppretting`, `navneendring`, `nedleggelse`,
`omorganisering`, `overforing_ressortfordeling`, `delegering`, `instruks`, `ikrafttredelse`, `regulering`. En kilde kan ha flere.
Presisjon på juristutvalget (40 kilder): 79 % (42/53) før juristrundens rettinger, 96 % (46/48, én type ikke vurdert) etter — målt på det
SAMME utvalget som rettingene bygger på, så tallet er optimistisk. Hver rad har
metode (`tittel` | `monster` | `ki` | `manuell`), regel, treff og kunnskapsstatus `normalisering`. Ingen rad = uklassifisert. Tittelregler
først; innholdsmønstrene kjøres når tittelen ikke ga noen type (bare for kilder med høyst 30 noder) eller bare organiseringstyper.
Anledningen i tittelen («som følge av oppretting av …», «i forbindelse med etableringen av …», parenteser) gir ikke type. KI-laget er ikke
bygget. Hjemmelen for en tittelrad er dokumentet selv (dokumentnivå); livsløpet bruker aldri en tittelrad som hjemmel, bare noden der
hendelsen står. `POST /api/rettskilder/disposisjonstyper/klassifiser[?skriv=true]` erstatter bare maskinens egne rader. Rettskildelista har
filter på kildeform og disposisjonstype (docs/09 §41).

**Sveipefilter** (`Sveipeutvalg`): begreps- og navnekandidatsveipet over hele korpuset hopper som standard over kilder der alle typene er
`ikrafttredelse`, og kilder markert som rent endringsvedtak; `?inkluderIkrafttredelseOgEndring=true` tar dem med.

**[Ny, Johanns avgjørelse 6] Markøren for rene endringsvedtak** er en egen egenskap på rettskilden (`rettskilder.rent_endringsvedtak`,
med regelen i `rent_endringsvedtak_grunnlag`), ikke en disposisjonstype: den lukkede listen er uendret. Den settes når hodet sier at kilden
endrer andre dokumenter (`changesToDocuments`), tittelen har endringsform, og ingen organiseringstype slo til — to signaler fra kilden, ikke
tittelen alene. Målt: 222 kilder.
Uklassifisert er ikke ikrafttredelse og blir alltid med.

**Livsløpet** (`DepartementLivslopTjeneste`, `POST /api/departementer/livslop[?skriv=true]`) bygger departementene som identiteter over tid
fra organiseringsvedtakene (konsoliderte og kunngjorte): opprettelse gir en ny identitet, navnebytte fører samme identitet videre,
nedleggelse avslutter den. Navn gjenbrukes (Arbeids- og sosialdepartementet 2004–2005 og 2014–2021), så et vedtak kobles aldri til katalogen
på navnet alene: bare en identitet som lever i dag, og bare på sitt nåværende navn. Konsoliderte tekster kan bruke dagens departementsnavn om
eldre vedtak (Lovdata oppdaterer dem); en slik identitet slås sammen ved navnebyttet og står i merknadene. Alt skrives som forslag
(`maskinelt_foreslatt`, `oppdagelses_kilde = monster:livslop`):
- **R oppretter** fra Kongen i statsråd (vedtaket er fastsatt ved kgl.res.) til departementet, med navnekjeden i begrunnelsen. K oppretting
  (kompetansen, Grl. § 12) skrives ikke: vedtaket sier den ikke.
- **Navneform `utgatt`** for hvert tidligere navn, med gyldig fra/til og hjemmel i navnevedtaket (genitiv → `normalisering`, ordrett →
  `tekstfunn`). [Avvik fra AK 5, juristrunden M1] Et navnebytte er samme aktør, ikke R etterfolger (det ville vært en kant til seg selv).
- **F forvaltning** (ressortfordeling) per lov i vedleggene («Oversikt over lover X skal ha forvaltningsansvaret for», åtte målte former) og
  i enkeltoverføringene, med `objekt_eli` (lovens ELI fra dato og nummer), gyldig fra vedtaket og til neste vedtak som gir samme HELE lov til
  et annet departement, eller til nedleggelsen. Modalitet er ikke satt: en ressortfordeling tildeler ansvar, den pålegger ingen plikt.
- **[ENDRET, Johanns avgjørelse M5 2026-10-23] Nedlagte departementer er aktører** (`virksomheter.livsstatus = 'nedlagt'`,
  `gyldig_fra`/`gyldig_til` fra vedtakene, ingen orgnr, `aktiv = false`). Nøkkelen er (nedlagt, navn, gyldig til), fordi navn gjenbrukes.
  Det siste navnet er navneform `gjeldende` (gyldig til nedleggelsen), tidligere navn `utgatt`, alle som forslag med hjemmel i vedtaket.
  Navneoppslagene «for i dag» (`VirksomhetOppslagTjeneste`, livsløpets katalogoppslag) treffer aldri en nedlagt aktør.
  Aktørsiden og aktørlisten viser merket «Nedlagt (til …)» (docs/09 §41).
- **R avvikler** fra Kongen i statsråd til det nedlagte departementet (kgl.res.), hjemmel i nedleggelsesleddet, `normalisering`.
- **[ENDRET, Johanns avgjørelse 2] R etterfolger bare når ansvar går over til et annet organ** (sammenslåing eller oppdeling), aldri ved et
  rent navnebytte. Avledet (`avledet`, premissene er forvaltningskantene før og etter): X nedlegges i vedtaket V, og V gir SAMME del av en
  lov X hadde forvaltningsansvaret for (hele loven begge ganger, eller samme punkttekst) til Y. Fra = etterfølgeren. [ENDRET, Johann
  2026-10-24 på #134, bygget i #397] Det er ikke et felt for hoved- eller deletterfølger: **omfanget** (antall lovdeler som gikk over) står
  først i kommentaren og telles i API-et fra premissene (`omfang` på kanten, forvaltningskantene fra etterfølgeren), og vises som merket
  «Omfang: N lovdeler» (docs/09 §43). Ingen tallterskel (juristrunden M5).
- **K oppretting og K avvikling** for Kongen i statsråd er registrert én gang, manuelt (`manuelt_foreslatt`), med Grl. § 12 annet ledd
  som hjemmel og kunnskapsstatus `fortolkning` (ordlyd, praksis): vedtakene oppgir selv Grl. § 12 og Instruks for Regjeringen
  (LOV-1909-03-23) som hjemmel.
- **Sekundær kontroll:** siste ressortvedtak for hele loven mot Lovdatas «Ansvarlig departement»; avvik listes, ingenting rettes.

**Målt 2026-10-23 (lokal base):** 45 vedtak, 34 identiteter (16 lever og er i katalogen, 16 nedlagt), 829 ressorttildelinger. Skrevet:
7 R oppretter, 11 navneformer `utgatt`, 503 F forvaltning. Mot Lovdata: 221 samsvar, 9 avvik, 4 der siste ressortvedtak gjelder et nedlagt
departement, 178 lover som ikke er i korpuset. 6 ledd ble ikke lest (bl.a. skrivefeilen «regionaldepartementel» i FOR-2013-12-13-1455).
13 av 16 levende departementer har i dag en gjeldende navneform uten hjemmel eller med en tilfeldig hjemmel (Landbruks- og
matdepartementet: «Forskrift om avlsfremmende tiltak på bier» § 3), mens navnevedtaket står i rapporten — grunnlaget for del 4 (AK 9), som
bygges i #394 (navneformen får hjemmel i navnevedtaket som forslag, og den gamle hjemmelen settes som erstattet).

**Målt etter M5 (2026-10-23, lokal base):** 16 nedlagte departementer som aktører, 18 navneformer, 9 R oppretter og 16 R avvikler til dem,
319 F forvaltning for dem (ressortkjeden), 15 avledede R etterfolger. Juristrunden på aktørene og kantene: R oppretter og R avvikler holder
alle; 5 etterfølgere bygde på ELI alene og koblet ulike deler av samme lov — regelen krever nå samme del (7 tidligere forslag avledes ikke
lenger og står i forslagskøen til avvisning). Kjente hull: vedtakene fra 1989–1997 i korpuset er ikke lest (Utdannings- og
forskningsdepartementet 1990, «nytt» Arbeids- og administrasjonsdepartement 1998), og vedleggene 2–4 fra 2001 og 3 fra 2013 gir ingen
forvaltningskanter, så hovedetterfølgeren mangler for blant andre Sosial- og helsedepartementet og Fiskeri- og kystdepartementet. **[Fulgt opp i #397, se §5.9.1.]**

### 5.9.1 Hullene i livsløpet: vedtakene 1990–1998 og vedleggene (#397, bygget 2026-10-26)

**Kilden for vedtakene før 2001 (kartlagt 2026-10-26).** Lovdatas åpne API (`/v1/publicData/list`) har bare seks datasett:
gjeldende lover, gjeldende sentrale forskrifter og Lovtidend avd. I og II fra 2001. **Kunngjorte vedtak fra før 2001 finnes ikke i åpne
data.** Vedtakene fra 1989–1998 finnes bare som konsoliderte `DEL/`-tekster i gjeldende-uttrekket, og de var alt i korpuset:
FOR-1989-12-22-1273 (1990), FOR-1990-12-21-1022 (1991), FOR-1992-12-18-1074 (1993), FOR-1996-12-20-1156/1157/1158 (1997),
FOR-1997-12-19-1316 og FOR-1998-01-01-1 (1998), FOR-1999-12-03-1203 (2000). Det som **ikke** kan hentes:
- FOR-1992-12-18-1074 punkt 3–5 er «– – –» i den konsoliderte teksten. Innholdet er ukjent (opprettelsen av Sosial- og
  helsedepartementet står trolig der, men det er ikke kontrollert, og det gjettes ikke).
- FOR-1989-12-22-1273 har kapittel I–IV og IX; V–VIII mangler i den konsoliderte teksten.
- Vedtakene fra 1997 og 1998 er gjengitt som foredragstekst. De sier ikke «nedlegges» om Administrasjonsdepartementet,
  Nærings- og energidepartementet, Planleggings- og samordningsdepartementet eller Kommunal- og arbeidsdepartementet (1993–). Disse
  identitetene har derfor ingen slutt, blir ikke aktører og får ingen R oppretter (juristrunden P1, til Johann).
- Lovtidend før 2001, eller Lovdata Pro, ville vært kilden for disse. Det er ikke bygget noen omvei.

**Nye mønstre** (`DepartementLivslopMonstre`, alt som forslag):
- «opprettes N nye departementer …: A, B samt C» og kommaformen (1972), «nedlegges A, B og C» (1990, 1993).
- «… nedlegges X og det opprettes et nytt departementet, Y» (1988).
- Navnebytte som liste: «endrer følgende departementer navn; A til B, C til D, og E til F» (1991).
- 1997/1998: overskriften «Nytt X (FORK)» sammen med leddet «X organiseres ved opprettelsen …» i samme kapittel gir opprettelse
  (`monster:livslop-nytt-departement`, hjemmel = leddet, `normalisering`). Overskriften alene er ikke nok.
- Lov med tittelen før datoen: «Lov om folkebibliotek av 20. desember 1985 nr. 108» (ikke når «endring» eller en annen lov står før datoen).
- Vedlegg: et kapittel som bare heter et departement under et «Vedlegg» (2018, 2019, 2020), og en vedleggsoverskrift som står som
  ledd over lista (2024). Innledningen («Statsråd Monica Mæland», «Oversikt over lover statsråd … har forvaltningsansvar for …») står i
  kommentaren. **Tekstfunn bare når samme tekst navngir departementet og har funksjonsordet** (overskriften, eller en innledning som også
  nevner departementet); en statsrådliste under en departementsoverskrift er `normalisering`.
- «slås … sammen» leses ikke, men rapporteres som ulest (Departementet for utviklingshjelp 1990, Handelsdepartementet 1988).

**Parseren** (docs/08 §3.1.3): lovlistene i vedleggene 2002, 2014, 2018, 2019, 2020 og 2024 sto i et `defaultP`-avsnitt med liste og
ble hoppet over. **Ny versjon av et vedtak:** `POST /api/rettskilder/{id}/konverter-paa-nytt[?skriv=true]` konverterer den lagrede HTML-en
på nytt og klassifiserer den nye versjonen. Livsløpet foreslår ikke samme utsagn på nytt når hjemmelen er en annen versjon av samme
vedtak (samme ELI og eId), og en gammel kant står på versjonen den ble foreslått mot (#367).

**Gyldighet (rettet etter juristrunden).** Gyldig til er det tidligste av neste vedtak som gir samme hele lov til et annet departement,
og nedleggelsen. Et eget, uvurdert F-forslag får ny gyldig til og kommentar når et senere vedtak er lest; en tom gyldig fra på en
nedlagt aktør eller en egen uvurdert navneform fylles når opprettelsen er lest.

**Målt 2026-10-26 (lokal base), før → etter:**

| | Før (#396) | Etter (#397) |
|---|---|---|
| Vedtak lest | 45 | 49 |
| Identiteter | 34 | 45 |
| Ressorttildelinger | 829 | 1 175 |
| Mot Lovdata: samsvar | 221 | 272 |
| Mot Lovdata: avvik | 9 | 5 |
| Mot Lovdata: siste vedtak gjelder et nedlagt departement | 4 | 4 |
| Mot Lovdata: lov ikke i korpuset | 178 | 205 |
| Avledede R etterfolger | 15 | 20 |

Navnehistorikken fra 1990 for tre aktører: Familie- og forbrukerdepartementet (1990) → Barne- og familiedepartementet (1991–2005),
Utdannings- og forskningsdepartementet (1990) → Kirke-, utdannings- og forskningsdepartementet (1991–2001), Kirke- og kulturdepartementet
(1990) → Kulturdepartementet (1991–2001). Arbeids- og administrasjonsdepartementet 1990–1992 (nedlagt 1993) er skilt fra det «nye»
1998–2004. Olje- og energidepartementet 1978–1992 er skilt fra det som ble opprettet 1997 og heter Energidepartementet i dag.
Nye etterfølgere: Nærings- og handelsdepartementet → Nærings- og fiskeridepartementet (18 lovdeler), Fornyings-, administrasjons- og
kirkedepartementet → Nærings- og fiskeridepartementet (6), Helsedepartementet → Helse- og omsorgsdepartementet (21), Sosialdepartementet →
Arbeids- og sosialdepartementet (24), Utdannings- og forskningsdepartementet → Kunnskapsdepartementet (4). **Ikke avledet:** Sosial- og
helsedepartementet og Fiskeri- og kystdepartementet. Korpuset viser ikke at de hadde forvaltningsansvaret for noen lov før nedleggelsen
(vedleggene 2002 sier ikke «fra»), og avgjørelse 2 krever forvaltningskanter før og etter. Om en etterfølger skal kunne leses der vedtaket
selv sier «overtar ansvaret … fra» (2014 punkt 6), er juristrundens P2, til Johann.

**Virkning på «departementet» (§5.10), målt 2026-10-26.** Ressortfordelingen gir fortsatt 0 svar, fordi ingen ressortkant er vurdert
(207 kilder har et ressortforslag som venter, før 167). Ruten «Lovdata, utgått navn» gikk fra 29 til 14 og «flertydig» fra 12 til 27:
navnehistorikken viser nå at «Olje- og energidepartementet» er brukt av to identiteter (1978–1992 og 1997–2023), og etter regelen fra PR
#398 gir et navn flere identiteter har hatt, ikke svar (15 kilder). Dette er regelen som virker, ikke en feil; om datoen skal kunne
skille dem, er Johanns valg.

### 5.9.2 Johanns avgjørelser etter #405 (2026-10-26, bygget i oppfølgeren)

- **Datoskille for utgåtte navn** (§5.10): når flere identiteter har hatt navnet i Lovdatas felt, er svaret for gjeldende versjon den som
  SIST hadde navnet (navneformenes gyldig til), og for en eldre versjon den som hadde det på versjonens dato. Merket «Sekundær kilde, utgått
  navn, valgt etter dato» (`valgtEtterDato`). Ingen entydig → flertydig som før.
- **U1:** står «med virkning fra <dato>» i leddet, gjelder den datoen for hendelsene i leddet (FOR-1987-12-18-971: 1. januar 1988). Bare
  der; ellers vedtakets dato. En nedlagt aktør og egne uvurderte R-forslag får datoen flyttet (ikke en ny aktør).
- **U2:** «Forvaltningen av følgende lover overføres fra X» under «Til Y overføres følgende» (FOR-1989-12-22-1273 vedlegg 4) gir F
  forvaltning til Y. Y må finnes på vedtaksdatoen; vedlegg 3 bruker 2002-navnet «Kultur- og kirkedepartementet» og står som ulest.
- **P1:** opphør uten «nedlegges» («overtar alle arbeidsoppgavene til [det nåværende] X», «overtar de resterende oppgavene til X»,
  «det tidligere X», også i en overskrift) gir R avvikler med kunnskapsstatus `fortolkning` (ordlyd, kontekst_system). Leses bare i et
  vedtak som også oppretter et departement, og bare for et departement som finnes i livsløpet.
- **P2:** «Y overtar … fra X» (og «Y overtar alle arbeidsoppgavene/de resterende oppgavene til X») gir R etterfolger når X opphører i
  samme vedtak, også uten forvaltningskanter (`monster:livslop-etterfolger-tekst`, `normalisering`, eller `fortolkning` når opphøret selv
  er lest etter P1). Kommentaren sier at omfanget er det teksten sier, ikke lovdeler. Et par som alt er avledet, skrives ikke på nytt.
- **P3:** statsrådlistene «etter endringene» (2018, 2019) er nye tildelinger, som bygget i #405. Begrunnelsen sier at departementet er
  tatt fra overskriften.
- **P4:** en navneform med hjemmel i konsolidert `DEL/`-tekst fra før 2001 får merket «Ikke kontrollert mot kunngjort tekst»
  (`KunngjortKontroll`, avledet av hjemmelen, ikke lagret; `ikkeKontrollertMotKunngjort` på `/api/virksomheter/{id}/begrep`). Merket
  hindrer ikke godkjenning. Målt: 19 navneformer.

**Målt etterpå (lokal base):** nye forslag 5 nedlagte aktører (Nærings- og energidepartementet, Administrasjonsdepartementet,
Planleggings- og samordningsdepartementet, Kommunal- og arbeidsdepartementet 1993–1997, og Industridepartementet på ny dato, se under),
4 R avvikler `fortolkning` + 1 `normalisering`, 4 R oppretter, 15 F forvaltning (1990 vedlegg 4), 1 avledet R etterfolger og 5 R
etterfolger lest av teksten: Planleggings- og samordningsdepartementet ← Administrasjonsdepartementet, Nærings- og handelsdepartementet ←
Nærings- og energidepartementet, Energidepartementet (OED 1997) ← Nærings- og energidepartementet, Arbeids- og administrasjonsdepartementet ←
Planleggings- og samordningsdepartementet (1998), Samferdselsdepartementet ← Fiskeri- og kystdepartementet (2014 punkt 6, kyst- og
miljøsaker). Kommunal- og regionaldepartementet ← Kommunal- og arbeidsdepartementet er lest, men ikke skrevet: KRD (1998–) er ikke en aktør
fordi navnebyttet til KMD 2014 har skrivefeilen «regionaldepartementel» (ulest). **Fortsatt ikke:** Sosial- og helsedepartementet får
ingen etterfølger (2002-teksten sier ikke «fra»), og Fiskeri- og kystdepartementet ingen hovedetterfølger (NFD; teksten sier ikke «fra
Fiskeri- og kystdepartementet»). Juristrunden stiller tre spørsmål til Johann (PR-en). **Kjent rest:** Industridepartementet finnes nå som
to nedlagte aktører (gyldig til 1987-12-17 fra #405, og 1987-12-31 etter U1); den gamle og dens forslag bør avvises.

**«departementet» (§5.10) etter datoskillet:** Lovdata, utgått navn 14 → 34, flertydig 27 → 7 (bare «flere departementer i Lovdata»).
Ressortfordelingen gir fortsatt 0, fordi ingen ressortkant er vurdert.

### 5.9.3 S1–S3 og duplikatet (Johanns avgjørelser 2026-10-27)

- **S1, bekreftelse:** en statsrådliste («etter endringene», innledningen nevner en statsråd) som gir SAMME identitet samme del av en lov
  som den alt har på datoen, gir ingen ny F-kant. Vedtaket legges til som grunnlag på den eksisterende kanten i den nye tabellen
  `strukturkant_bekreftelser` (kant, hjemmel, dato, kommentar). Egne, uvurderte duplikater fra #405 er avvist med begrunnelse
  (reversibelt). Målt: 58 bekreftelser på 56 kanter, 58 duplikater avvist.
- **S2:** også «… overføres/tilbakeføres fra X til Y», «Y får overført fra X» og «Y overtar … Xs oppgaver» (og overskriften «Lover som
  overføres fra X til Y») gir R etterfolger når X opphører i samme vedtak — samme vilkår og samme kunnskapsstatus som P2 (`normalisering`;
  `fortolkning` når opphøret selv er lest etter P1, slik at kanten ikke er sterkere enn premisset — den generelle regelen står i §4.6,
  «Svakeste premiss»). Et vedtak med bare vanlige
  overføringer er ikke et livsløpsvedtak. Nye: Arbeids- og administrasjonsdepartementet ← Kommunal- og arbeidsdepartementet,
  Nærings- og handelsdepartementet ← Planleggings- og samordningsdepartementet, Finansdepartementet ← Planleggings- og
  samordningsdepartementet (1998), Samferdselsdepartementet ← Fornyings-, administrasjons- og kirkedepartementet (2014, bredbånd).
- **S3, «Hovedetterfølger ukjent»:** regnet ut (`EtterfolgerHull`, ikke lagret) for en nedlagt aktør uten en avledet etterfølger, uten en
  som overtok alle (de resterende) oppgavene, og uten en faglig vurdert eller godkjent etterfølger. Vises som `warning` på aktørsiden og i
  aktørlisten (docs/09 §45). Ingen etterfølger gjettes; hullet fylles ved å registrere en etterfølger med faglig vurdering. Målt: 13 aktører,
  blant dem Fiskeri- og kystdepartementet, Sosial- og helsedepartementet og Nærings- og energidepartementet (delt mellom to).
- **Duplikatet Industridepartementet:** R avvikler `5d354af1` og navneformen `44d84785` er avvist, og aktøren `257389e5` er merket
  erstattet av `dc29e208` (`virksomheter.erstattet_av_id`, `POST /api/virksomheter/{id}/erstattet-av`, null angrer). Ingenting er
  slettet. En erstattet aktør vises ikke i aktørlisten og brukes ikke av livsløpet.

### 5.10 Standardregler for generiske omtaler og navnevedtaket som hjemmel (#394, bygget 2026-10-24)

Johann 2026-10-23: det var tungvint å binde «departementet» til lovens eierdepartement, bindingen han lagde ble ikke brukt (bare godkjente
oppløste), taggen viste en rå id, og veiviseren hadde for mange steg. Beslutningene står i issue #394; reglene er beskrevet for fagfolk i
begrepsmanualen (docs/profil/02, «Generiske omtaler og standardregler»).

**Oppslaget** (`GeneriskNavneformTjeneste.LosOppAsync`): vurderte bindinger (godkjent eller faglig vurdert) først, smaleste spenn vinner.
Dekker ingen av dem noden — og ingen er uflyttbar (`spenn_endret`) eller flertydig — gir **standardregelen** svaret, med ny status `regel`.
Regelens svar står ved siden av også når en binding vant («regelen alene ville gitt …»). Svarene lagres ikke (som #345).
`GET /api/navneformer/generiske/oppslag?rettskildeId=&term=&eid=&dato=&bareRegel=` viser status, kilde (binding/regel), bindingen og
regelens premisser; `bareRegel=true` ser bort fra bindingene. Taggoppslaget (`/api/rettskilder/{id}/generiske-tagger`) har fått term,
kilde, bindingens status og regelsvaret. Lasteren behandler `regel` som `kjent` (forslagene er uansett forslag). Virksomhetens «brukt i»
teller bare bindinger.

**Standardreglene** (`GeneriskStandardregelTjeneste`, kode, ikke data — tre regler, bestemt av Johann; liste:
`GET /api/navneformer/generiske/standardregler`). Kunnskapsstatus `avledet`, med premissene:
- **«departementet» → ansvarlig departement.** [LÅST, Johanns kommentar på #394 2026-10-23] Ressortfordelingen fra §5.9 er grunnlaget:
  F forvaltning med `objekt_eli` = rettskildens ELI, for HELE loven (`DepartementLivslopTjeneste.ErHeleLoven`), gyldig på datoen,
  **godkjent eller faglig vurdert**. Én referent → svar, primær kilde; flere → flertydig. Ellers Lovdatas «Ansvarlig departement» når
  det er nøyaktig ett navn som finnes i katalogen i dag — **sekundær kilde**. Et ressortforslag som ikke er vurdert, brukes ALDRI som svar;
  det står i merknadene («et ressortforslag venter», og om det avviker fra Lovdata). Del av loven til et annet departement gir en merknad.
  [ENDRET, Johanns beslutning på PR #398, 2026-10-26] Et gammelt navn i Lovdatas felt gir svar når navnehistorikken (navneformer
  «utgått» fra §5.9) fører det til NØYAKTIG ÉN levende aktør, og ingen annen identitet har hatt navnet: rute `lovdata_utgatt_navn`,
  merket «Sekundær kilde, utgått navn», med navneformen som premiss (og en merknad når den bare er et forslag). Har flere identiteter
  hatt navnet (navn gjenbrukes, §5.9: Barne- og likestillingsdepartementet, Arbeids- og sosialdepartementet, Kulturdepartementet), er
  svaret flertydig. Fører navnet bare til et nedlagt departement (Fiskeridepartementet, Sosialdepartementet), eller finnes det ikke i
  historikken, gir det ikke svar. Før: bare en merknad.
- **Gyldighet:** datoen er i dag for den gjeldende versjonen og versjonens dato (konsolidert dato, ellers ikrafttredelse) for en eldre.
  En ressortkant til et nedlagt departement er svaret før nedleggelsen (`referentNedlagt`); etter den er kanten utløpt (§5.9).
- **Forskrifter:** ingen ressortkant (vedtakene fordeler lover); Lovdatas felt er eneste kilde. Hjemmelslovens departement brukes ikke.
- **«statsforvalteren» → klassen statsforvalter, «kommunen» → klassen kommune** (#384). [Ny etter juristrunden på PR-en] Fire sperrer gir
  «ikke avklart» når kilden selv sier hva ordet betyr: (a) kilden definerer ordet («med kommune forstås Longyearbyen lokalstyre»),
  (b) for «statsforvalteren»: kilden navngir et embete («Statsforvalteren i ‹sted›»), (c) kilden er et virksomhetsdokument (eid av én
  aktør — «kommunen» er utstederen), (d) kilden gjelder Svalbard, Jan Mayen eller Longyearbyen. Premisset viser klassens egen status; en
  klasse uten vurdert kunnskapsstatus vises med «hjemmel ikke vurdert» (klassen statsforvalter har i dag reindriftsloven § 62 fjerde ledd,
  som ikke er grunnlaget for klassen).
- **Kjent pris og hull:** sperrene gjelder hele kilden (vergemålsforskriften sperres for «statsforvalteren» i alle 47 forekomster fordi
  § 35 navngir et embete). Regelen kan treffe der ordet betyr søkeren (klimatiltaksforskriften § 3 nr. 2 og 4) eller kommunen som område;
  der binder et menneske paragrafen. Sperre (a) fanger bare «med X menes/forstås». Ordformene «statsforvaltaren» (200 noder i 43 kilder),
  «fylkesmannen» (187 noder i 88 kilder, også eldre versjoner av samme verk) og «kommuna» har ingen regel — det er Johanns valg.

**Målt 2026-10-24, «departementet» på nytt 2026-10-26 etter regelen om utgått navn (lokal base, regelen alene, gjeldende
primærrettskilder der ordet står som eget ord; `GET /api/navneformer/generiske/standardregel-dekning?term=`):**

| Ord | Rettskilder | Ressortfordeling | Lovdata (sekundær) | Lovdata, utgått navn | Fast klasse | Ikke løst | Flertydig |
|---|---|---|---|---|---|---|---|
| departementet | 1 543 | 0 | 1 489 | 29 | – | 13 (11 navnet tilhører bare et nedlagt departement, 2 ikke i katalogen eller historikken) | 12 (7 flere departementer i Lovdata, 5 navnet brukt av flere identiteter) |
| statsforvalteren | 324 | – | – | – | 297 | 27 (sperret) | 0 |
| kommunen | 533 | – | – | – | 519 | 14 (sperret) | 0 |

Null ressortsvar fordi ingen av ressortkantene fra §5.9 er vurdert ennå; 167 av «departementet»-kildene har et ressortforslag som venter.

**Navnevedtaket som hjemmel (AK 9, Johanns avgjørelse 3 på #395)** (`DepartementNavneformHjemmelTjeneste`,
`POST /api/departementer/navneform-hjemmel?skriv=`): for hvert levende departement i katalogen der navnevedtaket (`navn[^1].gittVed` i
livsløpet) ikke alt er hjemmelen, en NY navneform med samme term, grunn «gjeldende», hjemmel i navnevedtaket (`tekstfunn` når navnet står
ordrett, ellers `normalisering`), `maskinelt_foreslatt`, `oppdagelses_kilde = monster:livslop-navnevedtak` og `erstatter_id` = den gamle.
Er den gamle bare et forslag, settes den som `erstattet` med en gang (to konkurrerende forslag skal ikke stå i køen) og taggene flyttes.
Er den gamle godkjent, settes den som `erstattet` — og taggene flyttes — først når det nye forslaget godkjennes
(`AktoridentitetTjeneste.GodkjennNavneformAsync`). Skrevet i lokal base 2026-10-24: 13 forslag av 16 levende (3 har navn eldre enn det
eldste vedtaket i korpuset: Utenriks-, Samferdsels- og Forsvarsdepartementet), 8 gamle forslag satt som erstattet, 3 godkjente
(Energi-, Justis- og beredskaps-, Nærings- og fiskeridepartementet) venter på at det nye godkjennes, 0 tagger flyttet. Forslagene står på
aktørsiden (fanen Navneformer) med Godkjenn/Avvis.

**Veiviseren** for generisk omtale er én skjerm (docs/09 §42), med forslag fra `GET /api/navnekandidater/{id}/generisk-kontekst`.

### 5.11 Organer registrert som rolle-, klasse- eller gruppebegrep blir aktører (#386, bygget 2026-10-27)

Restene fra den eldre begrepstaggingen («Tilsynsutvalget for dommere» som rolle) gjøres om til aktører. Ingenting skjer uten en
juristrunde (CLAUDE.md §23).
- **Kandidatene (AK 1)**, målt i lokal base 2026-10-26: begreper med kategori rolle, klasse eller gruppe der termen har et organord, eller
  som er tagget som aktør. 10 gjeldende rader (4 rolle, 6 klasse, 0 gruppe), 5 med organord, 0 tagget som aktør. Juristrunden
  (`data/fasit/strukturmodell/juristrunde-386-organer.json` og mappen `juristrunde-386/`, to runder, ingen uenighet): **aktør** Tilsynsutvalget
  for dommere; **rolle** reguleringsmyndighet (energiloven og naturgassloven); **klasse** statsforvalter; **generisk omtale** «departementet»
  (helse- og omsorgstjenesteloven), som gjøres om i en egen sak.
- **Omgjøringen** (`OmgjoringTjeneste`, `POST /api/begreper/{id}/omgjor-til-aktor`): aktøren opprettes (eller en eksisterende kobles), med
  navneformene og aktørtypen som forslag med grunnlag. Den gjeldende navneformen er hjemlet i bestemmelsen som oppretter eller navngir
  aktøren (#395); tjenesten nekter uten nøyaktig én gjeldende. Begrepet settes `erstattet`, og navneformen som arver taggene har
  `erstatter_id` = begrepet. Taggene og kantene flyttes; står navneformen i teksten fra taggens start, utvides spennet til hele navnet.
  Hver flytting har en proveniensrad (`flyttet`, de gamle verdiene i `kilde_referanser`). Etterpå tagges navnetreff med regelen fra §5.8
  (nøyaktig lik tekst, hele ord): alle navneformer i rettskilden der de er hjemlet, og den gjeldende også i andre rettskilder. En kortform
  tagges ikke alene i andre rettskilder («Tilsynsutvalget» i politiarrestforskriften er et annet organ). Bøyningsformer («Tilsynsutvalgets»)
  tagges ikke (#385-regelen for hele korpuset).
- **Angring** (`POST /api/begreper/{id}/angre-omgjoring`): begrepet, taggene og kantene settes tilbake fra proveniensen, og navneformene og
  aktørtypen fra omgjøringen avvises (ikke slettet). Er noe av det godkjent, angres det ikke. Å avvise den arvende navneformen direkte
  nektes, fordi det ville slettet de flyttede taggene.
- **Ny aktør med bare et navn** (Johanns tillegg 2026-10-23, `AktoridentitetTjeneste.RegistrerNavnetSomGjeldendeAsync`): navnet blir
  gjeldende navneform samtidig, med grunnlaget og statusen fra veiviseren (kobl-til-endepunktene med `nyAktorMedBareNavn`, og
  `POST /api/virksomheter` med `grunnlag`). Står navnet ikke som hele ord i bestemmelsen som er valgt, lagres «hjemmel mangler»
  (`uavklart`) med begrunnelsen; det gjettes ikke en annen bestemmelse. Har aktøren alt en gjeldende navneform, skjer ingenting; en
  navneform med samme navn uten grunnlag (SNL-bekreftelsen) fylles ut, ingen dublett. I generisk omtale (docs/09 §42) er hjemmelen noden
  der navnekandidaten står, med `tekstfunn` og bindingens godkjenning.

### 5.12 Fasiten i appen: verifiserte utsagn som faglig vurdert (#387, bygget 2026-10-27)

Johann 2026-10-16: det som er verifisert i fasiten (`data/fasit/strukturmodell`, 1 825 utsagn, kontrollert i #309 og juristrundene), fantes
ikke i appen. `POST /api/fasit/strukturmodell/import` (`StrukturforslagTjeneste.ImporterFasitAsync`, `Fasitimport.cs`) laster det inn gjennom
forslagskøens tjenestevei (§5.3): samme aktørklassifisering (§5.7, §5.8), samme typeavbildning (`KanttypeFor`) og samme skrivevei
(`StrukturkantTjeneste.OpprettAsync`/`ReviderAsync`).
- **Godkjenning** `faglig_vurdert`, aldri `godkjent`: fasiten er ikke uavhengig kontrollert (docs/profil/03 §10). **Opphav** i
  oppdagelseskilden: `fasit:<fil>:<id>`. **Kunnskapsstatus** er fasitens egen (NULL = ikke vurdert står som NULL). **Hjemmel** er eId-en;
  `kilde_utenfor_korpus: true` i fasiten (feltet gjelder hjemmelen, FORMAT.md #356) står i kommentaren, fordi en kant har hjemmel ELLER
  kilde utenfor korpus (`ck_strukturkanter_kilde`). Utsagn som ikke står i lovteksten, ligger i fasitens `utsagn_utenfor_korpus` med en
  strukturert kilde (FORMAT.md); første rad er Domstoladministrasjonen `sekretariat_for` Tilsynsutvalget for dommere (§4.3, #311).
- **Rettskilde og eId-drift:** rettskilden er ELI-en før «/nor» i eId-en (gjeldende versjon), eller for en eId uten ELI («kap-I/ledd-1»)
  den ene av fasitens kilder som har noden. eId-en må finnes som gjeldende node, og sitatet må stå i nodens tekst; ellers `eid_drift`,
  ingen ny avbildning gjettes.
- **Aktører (AK 2):** eksakt navn eller navneform, eller generisk omtale med vurdert binding eller standardregel (§5.7, §5.10). To
  tillegg, begge uten gjetting: et navn som treffer flere noder eksakt (Karasjok kommune og området Karasjok), skilles på fasitens
  `entitetstype` når nøyaktig ett treff har den arten; og fasitens `referent` (satt bare når teksten alene avgjør den) slås opp eksakt.
  Resten listes som uløst. Ingen aktør opprettes; navnekandidater bare med `?navnekandidater=true`.
- **Svakeste premiss (§4.6):** er en aktør løst via en binding eller standardregel, er det et premiss. Er premisset svakere enn fasitens
  kunnskapsstatus, får raden premissets nivå: fortolkning (med argumentene), avledet (med premisskantene), ellers uavklart eller omstridt,
  og begrunnelsen nevner premisset.
- **Kjøres på nytt (AK 3):** samme opphav og likt innhold → uendret; endret fasit → revisjon (ny rad, den gamle foreldet), men bare når raden
  fortsatt er faglig vurdert — er den godkjent etter importen, listes den som konflikt; avvist → respektert. Samme utsagn fra en annen
  kilde: avvist → respektert; godkjent eller faglig vurdert → uendret, eller **konflikt** når objekt, avgrensning, kunnskapsstatus,
  delegering eller grunnlag er ulikt (aldri overskrevet); ventende forslag → revidert til fasitraden. Nøkkelen er forslagskøens pluss objektet.
- **Synlig (AK 4):** merket «Fra fasiten, faglig vurdert, ikke uavhengig kontrollert» (docs/09 §46).
- **[Ny, Johanns avgjørelser 2026-10-26 på #387]** **Lav sikkerhet** (`sikkerhet: lav` i fasiten, annotatøren var usikker) lastes ikke som
  faglig vurdert, men som `manuelt_foreslatt` med kunnskapsstatus `uavklart` (begrunnelsen sier hvorfor) og vurderes i forslagskøen. De to
  radene som alt var lastet (energiloven u254, helse- og omsorgstjenesteloven u19), er revidert: ny rad, den gamle `foreldet` med
  `sist_endret_av`, ingenting slettet. **Bekreftelse:** finnes utsagnet alt som godkjent eller faglig vurdert med samme innhold, legges fasiten
  til som et ekstra grunnlag i `strukturkant_bekreftelser` (samme mekanisme som S1 i #408), med oppdagelseskilde `fasit:<fil>:<id>`. Kantens
  status, hjemmel og opphav endres ikke. Målt 2026-10-27: 401 bekreftelser (387 domstolinndelingen, 14 manuelle); ny kjøring gir 0 nye.
- **Målt 2026-10-27, lokal base** (første kjøring, så andre kjøring): 1 826 utsagn (1 825 + `x1`). 350 nye, 1 ventende forslag erstattet,
  401 fantes alt godkjent med samme innhold (387 domstolinndelingen fra mønsterlaget, 14 manuelle), 0 konflikter, 1 dublett, 1 073 ikke
  lastet: 733 aktør ikke i katalogen, 281 type uten databasetype (`annet:*`, `inngar_i`), 42 uten fra-aktør i teksten, 11 avvist av
  skriveveien (nodetype passer ikke kategorien), 6 flertydig aktør. 0 eId-drift, 0 rader markert omstridt eller uavklart i fasiten. 169
  rader løst via entitetstype, 0 via referent. 76 rader svekket normalisering → uavklart: «departementet» løst via standardregelen
  `ansvarlig_departement`, der ressortforslaget ikke er vurdert. 280 uløste aktører (260 ny aktør, 14 ny navneform for kjent aktør, 6
  flertydige). Andre kjøring: 351 uendret, 0 nye rader. Detaljene står i #387.

### 5.13 Kommunens tilhørighet via territoriet, kortform og registerkanter (#390, bygget 2026-10-27)

Johann 2026-10-16 (Alta-eksempelet): området «Alta» og rettssubjektet «Alta kommune» er bevisst skilt (§4.1). Tingretten har domsmyndighet over
territoriet, ikke over kommunen som juridisk person. Den som åpnet Alta kommune, så likevel ikke hvilken tingrett kommunen hører til.

**Område og tilhørighet sett fra aktøren (AK 1)** (`OmradeTilhorighetTjeneste`, `GET /api/virksomheter/{id}/omrade-tilhorighet`, fanen
«Område og tilhørighet» på et rettssubjekt, docs/09 §47):
- Territoriet er aktørens `A har_ansvarsomrade` til kommuneområdet med samme kommunenummer (regelen fra #384). Ingen eller flere → ingen svar,
  og hullet sies.
- Rubrikkene er fylke, tingrett, rettssted, lagsogn, lagdømme, lagmannsrett, statsforvalter, helseregion, RHF og valgkrets, pluss medlemskap i
  klasser. Kjedene er de samme som tilhørigheten på områdesiden, med territoriekanten først: kommune ← fylke (O), kommune ← tingrett →
  lagsogn (A + A sogner_til) → lagdømme (O) ← lagmannsrett (A), tingrett → rettssted (A har_sete_i).
- Ingenting lagres (#345). Hvert svar har premissene i kjedens rekkefølge.
- **Svakeste premiss (§4.6)** håndheves her: svaret er `avledet` bare når det svakeste premisset er minst like sterkt som `avledet`. Ellers
  får det premissets nivå, og et premiss som ikke er vurdert, gir `uavklart` (`SvakestePremiss`). Det svakeste premisset står i svaret.
  Å håndheve regelen i de andre avledningene er #411.
- Valgkretsen er ikke en områdetype i registeret og står som «mangler». Den utledes ikke av fylket.

**Registerkanter er eksternt dokumentert (AK 4)** (`RegisterkantKunnskapsstatusTjeneste`,
`POST /api/strukturkanter/registerkanter/eksternt-dokumentert[/angre]?skriv=`):
- Regel: kilde utenfor korpus av typen `register` og kunnskapsstatus ikke vurdert → `eksternt_dokumentert`. Godkjenningen endres ikke, og
  en kant med vurdert status overskrives aldri. Vedtekter, kgl.res. og kanter med hjemmel røres ikke.
- Endringen gjøres på raden, med én proveniensrad per kant (`regel = register_eksternt_dokumentert`). En revisjon ville gitt nye kant-id-er
  og brutt premisser og bekreftelser. `/angre` setter tilbake de kantene der siste kunnskapsstatus-endring var regelen.
- Nye registerkanter fra `OmraderegisterSeed` får statusen når de opprettes.

**Kortform per kommune (AK 2)** (`KommuneKortformTjeneste`, `POST /api/omrader/kommuner/kortformer?skriv=&kommunenummer=`):
- Kortformen er Kartverkets kommunenavn (`kommunenavnNorsk`). Den lages ikke ved å kutte «kommune» av registernavnet.
- Navneformen har grunn `kortform`, registeret som kilde og `maskinelt_foreslatt`. Den får ingen tagger (§5.11).
- [ENDRET, juristrunden k2] Kunnskapsstatus er `fortolkning` (`kontekst_system`), ikke eksternt dokumentert. Kartverket navngir
  kommuneområdet, og Enhetsregisteret gir orgnr og kommunenummer. At rettssubjektet bærer områdets navn, er en slutning
  (kommuneloven § 3-1).
- Kommuner som alt har kortformen, eller der et menneske har avvist den, hoppes over.
- [Ny, juristrunden k2] **Et kortformforslag som ikke er vurdert, brukes ikke i navneoppslaget** (`VirksomhetOppslagTjeneste`,
  aktørklassifisereren). Ellers ville forslagene gjort kommunenavn som før traff bare området, flertydige i hele korpuset.
- **Kollisjoner listes og stoppes ikke.** To kommuner med samme navn får hver sin kortform, og oppslaget blir flertydig til en binding
  eller konteksten avgjør. Kommunens eget territorium er ikke en kollisjon.
- Kjente hull (juristrunden k3/k4): sjekken ser bare dagens inndeling, ikke tidligere kommunenavn (Bø, Os, Nes, Sande før 2020). Den ser
  heller ikke kortformer som også er vanlige ord (Time, Sel, Ås, Fjord); oppslaget skiller ikke store og små bokstaver.
- [Ny, #390-oppfølging 2026-10-28] **Samiske og kvenske navn.** Kartverket-øyeblikksbildet er fornyet med `gyldigeNavn` (samme 357
  kommuner og norske navn som før). Hvert navn som ikke er norsk, foreslås som navneform med grunn `parallellnavn`, ikke `kortform`.
  Det er samme regel som registernavn-synken: et navn på et annet språk er likestilt, ikke en kortform. Ellers gjelder samme regel som
  for kortformen: registeret som kilde, `fortolkning`, forslag, og forslaget brukes ikke i navneoppslaget før det er vurdert. Språket og
  Kartverkets prioritet står i kildeteksten. Kartverket oppgir slike navn for 26 kommuner: 15 nordsamiske, 8 sørsamiske, 3 lulesamiske og
  4 kvenske, til sammen 30. Tallet 41 over teller kommuner som alt hadde et parallellnavn fra registernavn-synken (fulle navn som
  «Gáivuona suohkan»), ikke Kartverkets kortnavn.

**Binding av en kortform (AK 2)** (§5.7): en kortform for en aktør kan bindes per rettskilde, og da også til et **område**. En generisk
navneform kan fortsatt ikke bindes til et område. Aktørklassifisereren (§5.7) bruker en vurdert binding på en kortform når den dekker noden
(én referent → kjent), uten standardregel. «Alta» i forskrift om inndelingen av rettskretser og lagdømmer er bundet til området Alta. Hjemmelen er
§ 2 første ledd nr. 1, med kunnskapsstatus `fortolkning` (ordlyd, kontekst_system). Bindingen er et forslag, og juristrunden sa at den holder.
Utenfor den forskriften finnes ingen binding.

**«Hjemmel endret» for en binding uten spenn** (Johanns tillegg 2026-10-23, `HjemmelEndretFlagg`): flagget settes bare når en node med en
**tagget** forekomst av navneformen er endret eller borte i den nye versjonen. Før #390 ble flagget satt ved enhver tekstendring. Taggene
leses fra versjonen bindingen ble vurdert mot. En forekomst som ikke er tagget, gir ikke flagg.

**Relasjonstabellen på områdesiden (AK 3)** grupperes etter betydning: Er territoriet til, Ligger i ‹type›, Domstol (tingrett), Rettssted
for, Tingretter som sogner hit, Statsforvalter, Lagmannsrett, Regionalt helseforetak, Består av ‹typer›. Kilden står i egen kolonne
(docs/09 §47). `StrukturnodeDto` har fått `omradetype`.

**Målt og skrevet i lokal `regelide` 2026-10-26/27:**
- 730 registerkanter er satt til `eksternt_dokumentert`: 357 A territorium (Enhetsregisteret + Kartverket), 357 O fylke → kommune og
  16 O kommune → tettsted (Kartverket SSR). Det gir 730 proveniensrader.
- Av de 739 A-premissene i #411 er de 357 territoriekantene løftet. Det utledede medlemskapet i «kommune» har nå bare eksternt dokumenterte
  premisser, og det samme gjelder fylkessvaret på kommunesiden. Tingrett-, lagmannsrett-, statsforvalter- og RHF-kantene (382) er ikke vurdert.
  Svarene som bygger på dem, vises derfor som `uavklart`.
- 357 kommuner: 209 nye kortformforslag og 148 som hadde kortformen fra før. Kollisjoner: Herøy (1515/1818), Våler (3114/3419) og Oslo
  (kommune og fylke 03). 41 kommuner har samiske eller kvenske parallellnavn.
- Én binding: «Alta» → området Alta i forskrift om inndelingen av rettskretser og lagdømmer (forslag).
- Juristrunden ligger i `data/fasit/strukturmodell/juristrunde-390-kortform.json` og `juristrunde-390/`.

### 5.14 Merknader og veiledning i forskriftsteksten er aldri hjemmel (#419, bygget 2026-10-30)

Noen forskrifter har departementets merknader, veiledning eller kommentarer som eget kapittel etter selve forskriften. Konverteringen (begge
lag) og lasteren leste dem som normtekst. I piloten på barnehageloven (#375) ble Finansdepartementets anslag for kommunal deflator til vedtak
og forskrift, og satser som står i selve forskriften, ble til departementets enkeltvedtak.

**Johanns avgjørelser (#419, 2026-10-29 og 2026-10-30):**
1. Nodene merkes ved import. De kan aldri være hjemmel for en kant, men kan siteres som tolkningsargument i begrunnelsen.
2. Eksisterende forslag med slik hjemmel avvises maskinelt med begrunnelsen «hjemmel er merknad/veiledning (#419)». Godkjente kanter berøres
   ikke.
3. Regelen måles før bygging. Etter målingen valgte Johann alternativ A: regel 1 pluss en eksplisitt unntaksliste.

**Regelen** (`Merknadsnoder`, [LÅST]): et kapittel øverst i strukturen (ingen foreldernode) med eId som starter med `kap-merknad`,
`kap-veiledning` eller `kap-kommentar`, og hele undertreet. EId-en bærer tittelen fordi et unummerert kapittel får eId av overskriften.
Unntakslisten er nøklet på ELI og eId. Den har én rad: FOR-2008-01-09-50 `kap-merknader`. Der har Lovdata kalt det bindende vedlegget med
standardreglene «Merknader», og § 11 gjør «standardbestemmelsene fastsatt i vedlegget til denne forskrift» gjeldende. Lovdata-feilen er
kandidat i #429.

**Målt i den delte basen 2026-10-29 (bare SELECT, kommentaren på #419):**
- Regel 1 treffer 100 forskrifter og instrukser og 6 991 noder i gjeldende versjoner, 6 981 etter unntaket. Tildelings- og
  finansieringsforskriften har til sammen 210 (juristens telling).
- Bom: 11 kapitler, om lag 700 noder, behandles fortsatt som normtekst. Det gjelder «Veiledende materiale» i BSL E (én med eId
  `kap-veieledning-…`), «gule sider» i BSL D 2-4 og nummererte kommentarkapitler i RPR og instrukser. Ingen kanter har hjemmel der.
- Forkastet: regel 2 (samme prefiks på underinndelinger lenger ned) merker bindende takstvilkår i logopedforskriften. Regel 3
  (kapitteltittel) merker veiledning av doktorgradskandidater, «Veiledningsplikten etter forvaltningsloven § 11» og bindende «Merknader» i
  vedlegg til EU-forordninger.

**Egenskap, ikke nodetype.** `rettskilde_noder.er_merknad` (bool). `node_type` er strukturen (kapittel, ledd, punkt …) og styrer treet,
AKN-en og setningsdelingen. Et merknadskapittel har samme struktur som resten av forskriften. Merkingen følger samme mønster som `opphevet`:
en bool satt ved import, som konverteringen filtrerer på. Tabellen for håndbokkommentarer ble vurdert og forkastet, fordi den er
forfatterens arbeidsflyt (status, festenivå).

**Hvor den håndheves:**
- *Import:* `RettskildeImportTjeneste.SettInnNoderOgReferanserAsync` setter egenskapen. Første import, ny versjon ved resynk og «konverter på
  nytt» går alle den veien, så merkingen overlever en ny import.
- *Eksisterende noder:* migrasjonen `MerknadsnoderMerkes` fyller tilbake med samme regel og unntak, frosset i `MerknadsnodeMigrering`. Den er
  idempotent og gjelder alle versjoner. I den delte basen blir det 9 833 noder, hvorav 6 981 i gjeldende versjoner. Det er simulert med
  SELECT 2026-10-30.
- *Konvertering:* `ByggGrunnlagAsync` tar dem ikke med, så verken mønster- eller KI-laget ser dem. Antallet står i `merknadsnoderUtelatt`.
- *Laster:* et utsagn med eId i en merknadsnode lastes ikke (`hjemmel_er_merknad`). Det gjelder for eksempel en lagret konvertering fra før
  merkingen. Rapporten teller nodene.
- *Skrivevei:* `StrukturkantTjeneste.OpprettAsync` avviser hjemmel i en merknadsnode, også for en manuell kant. `GodkjennAsync` nekter å
  godkjenne et forslag med slik hjemmel, og «godkjenn alle» hopper over dem.

**Avvisning av eksisterende forslag** (`MerknadshjemmelAvvisningTjeneste`, `POST /api/strukturkanter/merknadshjemmel/avvis?skriv=`):
- Gjelder `maskinelt_foreslatt` og `manuelt_foreslatt` med hjemmel i en merknadsnode. Hjemmelen finnes via `hjemmel_node_id`, eller via
  rettskilde og eId for eldre rader.
- Hver kant avvises gjennom `AvvisAsync` med begrunnelsen over og `system-merknadsnoder` som avviser. Raden står igjen som avvist (#374) og
  foreslås ikke på nytt.
- Godkjente og faglig vurderte kanter røres ikke, men telles i `ikkeBerort`.
- Uten `skriv` er det en tørrkjøring. En ny kjøring finner 0.
- Forventet i den delte basen: 52 forslag, alle `maskinelt_foreslatt`. Det er tildelingsforskriften 34, finansieringsforskriften 11 og
  foreldrebetalingsforskriften 7. Ingen er godkjent. Tallet er simulert med SELECT 2026-10-30 (tilbakefyllingens utvalg pluss avvisningens).
- Operasjonen kjøres av hovedøkten etter fletting og migrering, ikke fra grenen.

**Tolkningsargument i begrunnelsen: hvor det hører hjemme (ikke bygget).** Avgjørelse 1 tillater at en merknad siteres som tolkningsargument.
I dag finnes bare fritekstfeltet `begrunnelse` og kodelista `tolkningsargumenter` (ordlyd, kontekst_system, formal, forarbeider, praksis,
reelle_hensyn, antitetisk_analogisk, §4.6). Ingen av dem kan peke på en node. Skriveveien hindrer ikke at en merknad siteres i
`begrunnelse`, men konverteringen ser ikke merknadene, så ingen av lagene siterer dem i dag. Den naturlige plassen er en henvisning fra
analysen til tolkningskilden: rettskilde, eId og node, ved siden av `tolkningsargumenter`, på samme måte som `hjemmel_node_id` peker på
hjemmelen.
- Først må Johann avgjøre hvilket tolkningsargument en departementsmerknad er: `forarbeider` eller `praksis`.
- KI-laget kan da få merknaden til den aktuelle paragrafen som kontekst, merket som ikke-hjemmel.
- Dette er ikke bygget.

**Testene:** `MerknadsnoderTests` (Data, med ekte Lovdata-HTML for foreldrebetalings- og fusjonsforskriften) og
`StrukturkantEndepunktTests.Merknadshjemmel_…` (API).

### 5.15 Funn til vurdering (#429, PR 1 bygget 2026-10-30)

[Ny, Johanns avgjørelser 2026-10-29 og 2026-10-30 i #429] Når lovteksten gjøres maskinlesbar, kommer det fram steder der rettskildene ikke
stemmer med seg selv eller med virkeligheten. Registeret samler dem som **funn til vurdering**: observasjoner med kilder, ikke påstander om
feil i loven. Kildefeil (#249) er noe annet: tekniske feil i Lovdata-kopien.

**Datamodellen** (`Funn.cs`, migrasjonen `FunnTilVurdering`):
- `funn`: kategori (`utgatt_aktor` | `mulig_motstrid` | `uklarhet_hull`) og undertype (`nedlagt_aktor`, `utgatt_navn`; `hull`,
  `upresis_henvisning`, `publiseringsfeil_kilde`), tittel, påstand (`beskrivelse`), vurdering, intern merknad (publiseres aldri),
  oppdagelseskilde, saken funnet kom fram i, og hvem og når for hver overgang. `ekstern_nokkel` er idempotensnøkkelen for seed og agenter.
- `funn_koblinger`: nøyaktig ett mål per rad — bestemmelse (ELI, eId, og oppslått rettskilde og node; NULL når den ikke er i korpuset,
  aldri nærmeste treff), aktør, navneform, etterfølgerkant, kildefeil eller ekstern lenke. Rollen bestemmer målet (CHECK).
- `funn_vurderingspunkter`: prøvd bortforklaring, delvurdering med sikkerhet (`hoy` … `lav`), eller ikke avgjort.

**Statusflyten:** `oppdaget` → `juristvurdert` → `godkjent_for_publisering`, eller `forklart_bort`.
- Juristvurdert krever vurderingen og minst én prøvd bortforklaring (Johanns terskel: juristen prøver først å forklare funnet bort).
  `vurdert_av_type` skiller menneske fra juristagenten. KI-vurderingen teller som juristvurdert og vises med eget merke (docs/09 §49).
- **Godkjent for publisering** kan bare settes av en publiseringsansvarlig: bruker-id-ene i `RegelIde:Funn:Publiseringsansvarlige`, tom
  som standard, slik at ingen kan publisere før Johann har satt sin egen id. Dette er kodebasens første tilgangssjekk. Testbrukermodus er
  ikke sikkerhet; den reelle sperren er at nettsiden bare endres når en PR flettes.
- Ved godkjenning fryses den offentlige representasjonen (`godkjent_innhold_json` med SHA-256). Eksporten leser bare øyeblikksbildet.
  Endres det offentlige innholdet etterpå, går funnet tilbake til juristvurdert. Funn som er forklart bort, publiseres ikke.
- **Premissporten** (§4.6, svakeste premiss): et funn om en utgått aktør kan bare godkjennes når navneformen og etterfølgerkanten er faglig
  vurdert (`faglig_vurdert` eller `godkjent`). En navneform uten godkjenning (NULL, fra før #385) regnes ikke som vurdert. Et funn uten
  etterfølgerkant stoppes ikke og publiseres med «etterfølger ukjent» — tolkningen er lagt fram for Johann i PR-en.
- **Kilde endret:** Lovdata-resynken flagger funn som peker på en bestemmelse med ny tekst (`HjemmelEndretFlagg`, samme deltre-regel som
  strukturkantene). Et flagg, ikke en statusendring; ny juristvurdering nuller det.

**Nettsiden:** `GET /api/funn/publiserbare` → `nettside/tools/eksporter-data.ps1` (steg 7) → `nettside/data/funn/register.json` →
`nettside/funn/index.html`. `FunnRegisterJsonTests` sjekker at fila bare inneholder godkjente funn, med nøyaktig den godkjente teksten.

**Seed:** de fem funnene som står i #429 (`Seed/funn-429.json`, gated av `RegelIde:Funn:SeedVedOppstart`): funn 1 om klageinstansen for
barnehage (godkjent for publisering av Johann 2026-10-29), kontrollen for lærere (forklart bort), og tre kandidater som oppdaget
(overgangsregelen, den upresise henvisningen, Lovdatas «Merknader» i FOR-2008-01-09-50).

**Ikke i PR 1:** maskinregelen for utgåtte aktører (PR 2, etter juristmålingen på om lag 40 par), statsrådsembeter og organiseringsvedtak
som har uttømt virkningen (#438).

### 5.16 Aktørkøen: ruting, gruppering og hjemmelforslag (#440 PR 1, bygget 2026-10-31)

**Hvorfor.** Johann 2026-10-30: «gjentagelsen skjer vel på forslagsidene … altså før aktøren er opprettet». De ventende navnekandidatene var
én rad per forekomst, og det samme navnet måtte godkjennes på fem steder. Aktørkøen viser hvert ukjent navn én gang. PR 1 er bare lesing.

**Rutingen** (`Aktorkoregler.Ruter`, ren funksjon) gir hver ventende kandidat nøyaktig én rute, prøvd i rekkefølge:
1. likt ord (trimmet, mellomrom slått sammen, uten forskjell på store og små bokstaver) som en **generisk navneform** (§5.7; standardregelen
   §5.10 står i svaret);
2. likt ord som en **aktørs navneform eller registernavn** (ikke avviste navneformer; ikke aktører som er erstattet som duplikat);
3. likt ord som en **kjent klasse, rolle, område eller gruppe** som gjelder i rettskilden (et lovspesifikt begrep bare i loven og i
   rettskilder med hjemmel i den, samme regel som aktørsveipet);
4. en **bøyningsform** av et ord i 1–3, i samme rekkefølge: ett suffiks fra den lukkede lista (-ene, -ane, -ens, -ets, -en, -et, -er, -ar,
   -ne, -a, -e, -n, -r, -t, -s), og det korteste ordet har minst fire tegn. **En bøyningsform arver aldri regelen eller bindingen** til ordet
   den ligner («et departement» er ikke «departementet»);
5. ellers **aktørkøen**.

Treffene regnes ut ved lesing. Ingenting flyttes (Johanns avgjørelse).

**Grupperingen** (`Aktorkoregler.Grupper`): samme ord (regel 1) og bøyning med like forledd (regel 2) slås sammen automatisk, og regelen står
på varianten. Genitivforledd (regel 3, «barnehagens foreldreråd» mot «foreldrerådet») er bare forslag: «domstolens leder» slås aldri
sammen med «leder». Resultatet avhenger ikke av rekkefølgen på inndataene.

**Hjemmelforslaget** (`Aktorkoregler.FinnHjemmelforslag`) bygger på opprettelsesuttrykk i rettskildene der navnet står: «skal … ha
et/en/ei» (høyst seks ord mellom, navnet etter uttrykket), «opprettes», «oppnevner», «etableres», med navnet (samme ord eller en
bøyningsform) i samme setning. Merknader og veiledning (§5.14) er aldri med. Tekstfunn når navnet står ordrett, ellers normalisering.
Finnes det ingen bestemmelse, vises hullet. **Regelen er et forslag**: juristrunden angriper den før godkjenningen bygges i PR 2.

**Sveipene** (navnekandidatsveipet, aktørsveipet og lasteren/fasitimporten via samme hjelper, KI-oppdagelsen) bruker samme ruting: et
treff som ikke havner i aktørkøen, blir ikke en ny navnekandidat. Tellingen per rute står i svaret. [ENDRET, Johanns avgjørelse 2026-10-31, valg 2 i PR #444]
Ett unntak, bare i aktørsveipet: et generisk ord uten binding blir fortsatt navnekandidat (`AktorsveipRad.BlirNavnekandidat`), så
lenken til én-skjermen for generisk omtale står. Kandidaten havner på ruten «generisk ord», ikke i aktørkøen.

**Målt mot den delte basen 2026-10-31** (etter #423): 1 211 ventende. Generisk ord 387, kjent aktør 233, kjent klasse/rolle/område 75,
bøyningsform 123, aktørkøen 393 forekomster i 80 rettskilder, som blir 192 navn (208 ord; 47 med mer enn én forekomst; 11
genitivforslag). 0 havner hos Agder fylkeskommune. Utdanningsdirektoratet står én gang, 9 forekomster i 5 rettskilder, med hullet.
Foreldrerådet får forslaget barnehageloven § 4 første ledd (normalisering). 18 navn har hjemmelforslag.

### 5.6 Konvertering av eksisterende data

| Fra | Til | Automatisk? |
|---|---|---|
| `VirksomhetRelasjon` (10 rader lokalt) | R-kanter, samme typekode | Ja — 1:1, ingen tap. **Bygget #311:** 10 → 10 (8 hjemlet; 2 uten hjemmel → kilde utenfor korpus `nettside_annet` + `sekundaer`). **Kodene harmonisert i #330:** 10 → 10, se §4.3 |
| `GruppeMedlemskap` (3) | M-kanter | Ja — 1:1. **Bygget #311:** 3 → 3 |
| `Myndighetstildeling` (16) | M eller I, avhengig av målets nye nodetype | **Bygget #311** (etter #310): 16 → 15 M (14 klasse + 1 område) + 1 I (rolle) |
| `Begrep(gruppe)` (13) | klasse / rolle / område / organ | **Nei** — må avgjøres av et menneske (liste i sak) |
| `Begrep(administrativ_inndeling)` (0) | område | Ja |
| Kommuner/fylker | område-noder fra Kartverket kommuneinfo (15 fylker, 357 kommuner) + `O bestar_av` + `A har_ansvarsomrade` kommune→eget territorium | Ja — entydig nøkkel (kommunenummer innen gyldig inndeling). **Bygget #312:** 357 O-kanter; 355 territoriekanter (Herøy 1515 og Våler 3114 mangler som virksomhet i katalogen — `OrganisasjonsregisterSeed` slår like navn sammen, egen sak) |

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
«ja» fordeler seg slik på kanttypene: O 578, K 514, R 266, A 176, M 65, G 61, T 21, I 14 (P 77 etter juristgjennomgangen).

[ENDRET, issue #312] Etter fasitrettelsen gir `designtest.py` 1 797 utsagn, revidert ja 91 % (1 640), delvis 1 %,
senere lag 8 %; domstolloven 739 strukturutsagn, revidert ja 100 %.

[ENDRET, issue #356] Etter konverteringen for #356 (med juristrunden): 1 802 utsagn, revidert ja 95 % (1 720), delvis 21, senere lag 61 (+ «avtalene kan ikke overdras», avtalens innhold).

[ENDRET, issue #355] Etter konverteringen for #355: 1 809 utsagn, revidert ja 96 % (1 728), senere lag 60 (+ ankeadgang u102/u108).

[ENDRET, issue #353] Etter plikt-konverteringen (§4.5): 1 798 utsagn, revidert ja 96 % (1 719), delvis 1 %, senere lag 3 % (56:
møteplikt, saksforberedelse, rettigheter, hefte, avtalens innhold). Revidert «ja» per element: K 637, A 508, O 177, R 159, P 77,
M 65, G 61, T 21, I 14.

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
[Ny, #355] 16. Hvem kan sette inn en fast dommer (utnevning, ikke konstitusjon)? 17. Hvem er ankeinstans for X tingrett (avledet)?
[Ny, #353] 14. Gitt en kommune: hvem har den plikt overfor (samarbeid, avtale, betaling …), og hvor står det (S6)?
15. Hvem forvalter en ordning (folketrygden), etter hvilket kapittel (S1)?

## 7. Ikke løst / åpne beslutninger

1. ~~**Konsolidering av kanttabellene**~~ — **avgjort og bygget (#311):** Johann valgte A, full
   konsolidering. Se §4.3.
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
