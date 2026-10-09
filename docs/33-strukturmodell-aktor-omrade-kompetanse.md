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
| **K** kompetanse | aktør/rolle → **motpart** (valgfri) og bestemmelse/sakstype | [ENDRET, #341, #352, #355] beslutning; struktur: oppretting, avvikling, organisasjon; oppnevning: oppnevning (med undertype valg/ansettelse/utpeking/oppnevning/utnevning/konstitusjon), avsetting (med undertype avsetting/oppsigelse/avskjed); styring: instruksjon, samordning, delegering, godkjenning, samtykke, palegg; normgivning (med normform); kontroll: tilsyn, revisjon (~~forelegging~~ [FJERNET, #355]); klage og overprøving: klage, omgjoring, overproving (med undertype anke), stadfesting; vedtak (med undertype tilbakekall); sanksjon |
| **M** medlemskap | aktør/klasse/område → klasse | medlem_av |
| **O** områdesammensetning | område → område | bestar_av |
| **A** ansvarsområde | aktør → område | har_ansvarsomrade, har_jurisdiksjon, har_sete_i, valgkrets_for |
| **G** organtilhørighet | organ/enhet/rolle → rettssubjekt | har_organ, del_av ([#355] også fra en stilling: «Høyesteretts direktør» del_av Høyesterett), har_medlemmer (organets faste medlemmer), [Ny, #352] settes_med (sammensetningen i den enkelte sak — saksavhengig), [Ny, #353] tilhorer (ordning → rettssubjekt, bare når hjemlet) |
| **I** rolleinnehav | aktør → rolle | innehar (= dagens `Myndighetstildeling` når målet er en rolle) |
| **T** klassenivå | klasse → rolle/organ-type | skal_ha (distributivt: hvert medlem av klassen har …) |

Felles egenskaper på **alle** kanter:

- `HjemmelRettskildeId` + `HjemmelEid` — påkrevd, **eller** `KildeUtenforKorpus` (fritekst +
  lenke) når hjemmelen er kgl.res./vedtekter/instruks (funn 8).
- `AvgrensningParagrafspennJson` (samme strukturerte format som i dag, `docs/20` §7.1) og
  `AvgrensningTekst` (sakstype, «bare ugyldige vedtak») — funn 7.
- `Polaritet` (`positiv`|`negativ`) — funn 6. «Kommunestyret selv» = K med `delegerbar = false` ([Bygget, #341] — se under).
- `GyldigFra`/`GyldigTil`, `Status` (`foreslatt_av_ai`|`validert`, `docs/20` §2.7), `OppdagelsesKilde`
  (`manuell` | `monster:<id>` | `ki:<modell>`) — proveniens for automatisk konvertering (§5).
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
| «Kan kompetansen delegeres videre?» | Feltet `delegerbar` (bool, NULL = ikke angitt) på K — fra #335: «Kongen …» = true, «Kongen i statsråd …» og «X selv» = false. [ENDRET, #356] Også false når loven sier «kan ikke delegeres» (sameloven § 2-12 fjerde ledd) |

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
