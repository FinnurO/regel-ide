# Navnekandidat-presisjon — Johanns innspill (2026-09-02)

**Status: RÅTT INNSPILL, IKKE BESLUTTET** — MED ÉTT UNNTAK: se «Beslutning: datamodell for gruppe
og relasjon» rett under, som ER besluttet (2026-09-02, etter en lengre samtale), men fortsatt IKKE
bygget. Resten av dokumentet er fortsatt ren nedtegnelse av observasjoner Johann gjorde etter å ha
slettet hele navnekandidat-køen og kjørt et nytt korpusomfattende sveip (5881 kandidater, opp fra
~3990 før flerords-mønster/normalisering ble lagt til). Formålet er å ikke miste innspillene —
prioritering og faktisk bygging av resten er en egen, senere samtale.

Relatert: `docs/13-backlog.md` §9 (selve oppdagelsesmekanismen), `docs/20-*.md` (Begrep/Rollebegrep/
Myndighetstildeling-modellen), `docs/24-begrepsoppdagelse-plan.md`.

## Beslutning: datamodell for gruppe, relasjon og myndighetstildeling (2026-09-02)

**Besluttet, IKKE bygget.** Oppstod fra §7 sine åpne modellspørsmål, presisert gjennom et konkret
eksempel Johann selv fant (KNS/klagenemnder, se under) og videre skjerpet av Johanns egen
motforestilling mot at «rolle» var feil navn. To mekanismer, ikke tre — se begrunnelse under hver.

### Mekanisme 1 — «gruppe» (tidligere «rolle»): generisk term realisert av N virksomheter

**Navneendring, ikke ny mekanisme.** `BegrepEntitet.Begrepskategori`-verdien `"rolle"` omdøpes til
`"gruppe"`. Selve mekanismen (`Rollebegrep`/`Myndighetstildeling`, identitet `(Term, LovkildeId)`)
er UENDRET.

**Hvorfor navneendringen**: «rolle» er semantisk feil for mange av tilfellene den skal dekke — en
rolle er en FUNKSJON/kapasitet noen utøver ("i sin rolle som klageinstans"), mens f.eks. et
regionalt helseforetak ikke er en funksjon noen utøver, det ER organisasjonen. Å kalle begge for
"rolle" er en kategorifeil, selv om det tekniske oppslaget (generisk term → N konkrete
virksomheter) er identisk for begge. «Gruppe» er nøytralt — sier ikke noe om HVA slags ting det
er, bare AT det er én term med flere konkrete realiseringer.

**Verifisert, ikke gjettet**: «gruppe» er allerede etablert praksis for nøyaktig dette fenomenet i
Forvaltningsdatabasen (Sikt) sin egen dokumentasjon — de bruker «gruppe-enheter» for å slå sammen
flere parallelle enheter med samme funksjon til én representasjon (deres eksempel: ~90
utenriksstasjoner telles som én gruppe), uavhengig av om det underliggende er en funksjon eller en
organisasjonstype. Alternativet «samlebetegnelse»/«fellesbetegnelse» er et reelt, men mer generisk
ordboksuttrykk uten samme etablerte forvaltningsbruk (bekreftet ved websøk 2026-09-02).

**Ny utvidelse — tidsavgrenset medlemskap**: `MyndighetstildelingEntitet` får to nye, nullbare felt,
`GyldigFra`/`GyldigTil` (`DateOnly?`). De aller fleste tildelinger setter ALDRI `GyldigTil`
(permanent — Statsforvalter, regionale helseforetak, Riksvalgstyret/Fylkesvalgstyre). Situasjons-
betinget medlemskap (Johanns «Vertskommune»-eksempel: en kommune er vertskommune KUN fordi den
tilfeldigvis har et fengsel/mottak, og kan slutte å være det) setter `GyldigTil` den dagen
forholdet opphører. **Ingen ny Gruppe-/Medlem-tabell** — dette var opprinnelig foreslått som en
egen entitet, men Johanns eget spørsmål ("hvorfor ikke bare konsekvent bruke gruppefunksjonen?")
avdekket at hver `Myndighetstildeling`-rad ALLEREDE ER et medlemskap i gruppen (rollebegrepet) —
et tidsvindu var alt som manglet.

**Eksempler som nå går i denne mekanismen, likt**: Statsforvalter, Vertskommune (for fengsel/mottak/
vannkraftkonsesjon — situasjonsbetinget), Regionalt helseforetak (permanent, faste, navngitte),
Riksvalgstyret/Distriktsvalgstyre/Fylkesvalgstyre/Valgstyre (permanent, én per nivå/fylke).

### Mekanisme 2 — `VirksomhetRelasjon` (ny entitet): navngitt relasjon mellom to BESTEMTE virksomheter

For relasjoner der begge parter er KONKRETE, navngitte virksomheter (ikke en generisk term realisert
av mange) — f.eks. Advokatnemnda↔Advokatforeningen (sekretariat), en klagenemnd↔et departement
(underlagt), PNR-enheten↔Politiet (enhet i). Konkret utløst av et reelt funn: Johann hadde koblet 6
av 7 opprettede klagenemnder til KLAGENEMNDSSEKRETARIATET via «Del av virksomhet»
(`OverordnetEnhetId`) — men skjemaet nullstiller det feltet etter hver opprettelse (samme linje som
nullstiller navnefeltet), så kun den FØRSTE nemnda faktisk fikk koblingen lagret. Bekreftet mot det
offentlige org-kartet (klagenemndssekretariatet.no/om-oss/organisasjon) at alle 6 faktisk skulle
vært koblet — ikke rettet ennå, se §"Konkret, ikke rettet ennå" under.

```
VirksomhetRelasjonEntitet
├── Id
├── FraVirksomhetId       Guid   — f.eks. Advokatnemnda, PNR-enheten
├── TilVirksomhetId       Guid   — f.eks. Advokatforeningen, Politiet
├── RelasjonsType         string — konfigurasjonsstyrt liste (samme mønster som
│                                  TaggKindKonfigurasjonEntitet — admin-redigerbar, ikke hardkodet
│                                  enum), IKKE bare de fire under — ny type kan legges til av
│                                  Johann selv uten kodeendring
├── HjemmelRettskildeId?  Guid?  — nullbar: NÅR relasjonen er lovhjemlet
├── HjemmelEid            string?
├── Kommentar             string? — fritekst + kildehenvisning (f.eks. en lenke til et org-kart)
│                                   når det IKKE finnes en formell hjemmel
└── OpprettetAv / OpprettetTidspunkt / Entitetsstatus (samme provenance-mønster som ellers)
```

**Retningsvisning — lærdom fra `OverordnetEnhetId`-bugen**: `OverordnetEnhetId` vises i dag KUN fra
barnets side («jeg er del av X») — det finnes ingen reversert liste på foreldrens side («disse N er
del av meg»), noe som er nøyaktig hvorfor Johanns feilkoblede nemnder ikke ble oppdaget tidligere.
`VirksomhetRelasjon` skal IKKE gjenta dette — hver `RelasjonsType` defineres med ETT lagret rad, men
TO beregnede visningstekster (Fra-side/Til-side), samme mønster som `Tjenesteavhengighet` allerede
bruker og har løst dette for (`Visningstekster`-ordboken i `TjenesteavhengighetregisterTjeneste.cs`).
Kjente typer så langt (liste, ikke uttømmende — se over):

| RelasjonsType | Fra-siden ser | Til-siden ser |
|---|---|---|
| `underlagt` | «er underlagt {0}» | «er eier/overordnet for {0}» |
| `sekretariat` | «har sekretariat hos {0}» | «er sekretariat for {0}» |
| `klageinstans` | «har klageinstans hos {0}» | «er klageinstans for {0}» |
| `enhet_i` | «er enhet i {0}» | «har enhet {0}» |

**`OverordnetEnhetId` beholdes uendret** — det er Brregs egen, automatiske hierarkidata, uten
hjemmel å vise til. `VirksomhetRelasjon` dekker de manuelt kuraterte, ofte lovhjemlede koblingene.
De to skal IKKE slås sammen — ulik kilde (automatisk vs. manuelt dokumentert), ulik pålitelighet.

### Beslutningsregel — hvilken mekanisme for et nytt tilfelle?

> «Realiserer dette en generisk term for mange virksomheter?» → mekanisme 1 (gruppe).
> «Er dette en navngitt relasjon mellom to BESTEMTE virksomheter?» → mekanisme 2 (VirksomhetRelasjon).

### Fullstendig eksempel (merkenemnd-hierarkiet, Johanns eget)

```
Lokal merkenemnd     --sekretariat-->    Statsforvalteren (den aktuelle)         [VirksomhetRelasjon]
Lokal merkenemnd     --klageinstans-->   Sentral klagenemnd for merkesaker       [VirksomhetRelasjon]
Sentral klagenemnd   --sekretariat-->    Landbruksdirektoratet                   [VirksomhetRelasjon]
Sentral klagenemnd   --underlagt-->      Landbruks- og matdepartementet          [VirksomhetRelasjon]
```
(Om de lokale merkenemndene selv er «underlagt» departementet direkte eller Statsforvalteren, er
ikke avklart — reelt åpent spørsmål som krever oppslag per nemnd, ikke noe modellen kan utlede.)

### Konkret, ikke rettet ennå

De 6 feilkoblede klagenemndene (Konkurranseklagenemnda, Medieklagenemnda, Stiftelsesklagenemnda,
Energiklagenemnda, Finanstilsynsklagenemnda, Klagenemnda for godkjenning av utenlandsk utdanning)
mangler fortsatt sin KNS-kobling i databasen, og 2 nemnder fra org-kartet
(Frivillighetsregisternemnda, Lotterinemnda) finnes ikke i katalogen i det hele tatt. Selve
UI-bugen (nullstiller «Del av virksomhet» etter hver opprettelse) er heller ikke rettet. Alt dette
venter på eksplisitt "gjør det nå" fra Johann, samme som resten av dette dokumentet.

## Observert utgangspunkt

Et faktisk, korpusomfattende sveip etter dagens flerords-mønster + normalisering ga 5881 kandidater
(2487 rolle, 3394 virksomhet), derav 393 ekte flerords-treff (mellomromsdelte egennavn — «Oslo
kommune», «Møre og Romsdal fylkeskommune», «Norsk fagskole» m.fl.) som var null før. Samtidig
avdekket samme sveip nye falske positiver (se §1).

## 1) Avvisningsregler (unntak fra gjenkjenning)

Faste fraser/mønstre som IKKE skal tolkes som virksomhet eller rolle:

- **Faste uttrykk foran «tilsyn»** (sjekk om de står i setningsstart): «For tilsyn», «Ved tilsyn»,
  «Stedlig tilsyn», «Beskytta tilsyn», «Føre tilsyn», «Statlig tilsyn», «Støtta tilsyn», «Dersom
  tilsyn», «Vurderinger og tilsyn», «gjennomført», «gjennomførte», «kommunens tilsyn», «konkret
  tilsyn», «når tilsyn», «periodisk tilsyn», «spesielt tilsyn», «stedlig tilsyn».
- **«Dersom»** — eget avvisningstilfelle (bekreftet falsk positiv i det ferske sveipet: «Dersom
  tilsyn»).
- **Generisk/eksempelbruk**: «Ein skole», «Ein kommune», «I Oslo kommune», «KS og Oslo kommune» —
  ikke en konkret virksomhet.
- **Sammensatte navn med «- og»-regel**: sjekk om teksten foran er «- og» pluss et ord som i seg
  selv starter med stor bokstav → indikerer ETT samlet egennavn, ikke to separate ord. Eksempler:
  - «Toll- og avgiftsdirektoratet» — ikke bare «avgiftsdirektoratet»
  - «Post- og teletilsynet»
  - «toll- og avgiftsetaten» — ikke «avgiftsetaten» (unntak fra stor-forbokstav-regelen)
  - «Likestillings- og diskrimineringsombudet» og «Likestillings- og diskrimineringsnemnda»

Bekreftet i det ferske sveipet (uavhengig verifikasjon, samme funn som Johann selv fant):
«Vedkommende departement» (5×), «Vedkomande departement» (nynorsk, 3×), «Ved tilsyn» (4×), «For
tilsyn» (3×).

## 2) Generelle begrep (ikke virksomhet/rolle)

Ord som ser ut som egennavn på virksomhet, men er generelle begrep:

- «regelverket» (f.eks. anskaffelsesregelverket)
- «rammeverket» (f.eks. kvalifikasjonsrammeverket)
- «planverket» (f.eks. Læreplanverket)
- «byggverket»
- «nettverket»
- «gårds- og grendeverket»
- «forskningsbiobank»
- «kunnskapsbanken»
- «trassatbanken»
- «filmverket»

**Åpent spørsmål (viktig metodisk poeng)**: hva er egentlig «verket»-mønsteret — sammenlign med
reelle treff som «Rikstrygdeverket», «Televerket», «Arkivverket», «Myntverket» (disse ER
virksomheter, i motsetning til lista over). Dagens løsning (`VerketDenyliste`) er en LUKKET
denyliste over kjente FALSKE positiver — Johanns liste over viser at den ikke er uttømmende. Verdt
å vurdere om et **allowlist**-mønster (kun kjente, reelle «-verket»-institusjoner gir treff — trolig
et lite, endelig sett) er mer robust enn en denyliste her, altså motsatt strategi av det som ble
valgt for «skole» (der en denyliste ble vurdert IKKE mulig å gjøre uttømmende, se
`NavnekandidatOppdagelseTjeneste.cs` sin kommentar).

## 3) Synonymer / navneform-sammenslåing

Varianter som skal slås sammen til samme begrep ved oppslag:

- statsforvalter, statsforvalterne, statsforvaltere, statsforvaltaren (nynorsk)
- kongen / Kongen, kongen i statsråd / Kongen i statsråd / Kongen i Statsråd / kongen i Statsråd —
  bør også lagres som lowercase for case-insensitivt søk
- Høgskolen i Molde – Vitenskapelig høgskole i logistikk (samme institusjon, to navn)
- innkvarteringsnemndene vs. nemnda
- fagnemnda = fagnemnda for helsesaker

**Type referanse: geografiske tildelinger.** Noen ganger er treffet en geografisk tildeling snarere
enn en virksomhet, f.eks. hummerfiske eller lokasjon for generalforsamling (jf. Norsk Tipping).
Ønsket mulighet: avvise, men med forslag til nytt navn — jf. kommuner som begynner på Ø, geografiske
koordinater.

## 4) Kjente presiseringer (lett forvekslede/tvetydige entiteter)

Konkrete tilfeller der kortform må mappes til fullt/riktig navn, eventuelt med nytt søk ved
usikkerhet:

- «Ankenemnda» → ofte «Ankenemnda for sykepenger» — usikkert, bør trigge nytt søk (jf. også
  «Dersom Samisk høgskole» som bør trigge nytt søk)
- investeringsbanken = Den nordiske investeringsbanken
- NVEs tilsyn = navneform for NVE selv
- sentralnemda = Sentralnemnda for rekvisisjonssaker
- Politiet og Statens vegvesen = **to** separate virksomheter (ikke slå sammen)
- familieetaten = Barne-, ungdoms- og familieetaten (Bufetat)
- familiedirektoratet = Barne-, ungdoms- og familiedirektoratet
- patentverket = Det europeiske patentverket
- Brukerklagenemnda = Brukerklagenemnda for elektronisk kommunikasjon (samme som «Klagenemnda for
  elektronisk kommunikasjon»? — se https://lovdata.no/dokument/NL/lov/2024-12-13-76 — hvordan
  representere kortform «brukerklagenemnda» som navneform i en rettskilde er uavklart)
- Fylkesnemnda — mange regionale varianter. Eksempel på navneendring: «Fylkesnemnda for barnevern
  og sosiale saker i Oppland og Hedmark» → «... i Innlandet»
- merkenemda / Klagenemnda for merkesaker — statsforvalterne har sekretariatfunksjonen
- Advokatnemnda — uavhengig forvaltningsorgan; Advokatforeningen har sekretariatfunksjon. Alias:
  «Disiplinærnemnden for advokater» (klagesaker), «Advokatbevillingsnemnden» (andre saker),
  «Tilsynsrådet for advokatvirksomhet» (egne saker). Åpent problem: navneform «Advokat» er
  godkjent, men ansvarsområdet vises ikke tydelig — se §7.
- Riksrevisjonen = Stortingets revisjons- og kontrollorgan
- Etterretningstjenesten = Norges nasjonale utenlandsetterretningstjeneste
- Sekretariatet for Forbrukarklageutvalet ligger organisatorisk i Forbrukartilsynet
- Samisk arkiv og Norsk helsearkiv er deler av Nasjonalarkivet
- regionale helseforetak / «Dei regionale helseføretak» — spørsmål om rekkefølge, spesifikke vs.
  generelt nivå
- Longyearbyen lokalstyre, Skatteklagenemnda for Svalbard — Svalbard-spesifikke varianter av ellers
  kjente virksomhetstyper
- Klagenemnda for disiplinærsaker i Forsvaret — eksempel på lenkestruktur/type hos regjeringen.no
  (styrer, råd og utvalg under departement)

## 5) Manglende virksomheter/roller (kandidater å legge til)

Stor liste, **uprioritert** — trenger enkeltvurdering og kategorisering. Gruppert grovt etter
domene under (Johanns egen forenkling, bør sjekkes på nytt):

- **Justis/tilsyn/klage generelt**: nemnd (generisk), Klageorgan, Klageinstansen, tilsynsnemnd,
  Tilsynsorganet, Karantenenemnda, Skiftemyndigheten, Konfliktråd (gruppe), forliksråd,
  lagmannsrett, Jordskifterettane, Arbeidsretten, Riksmekler, Påtalemakta, Høyesterett, Oslo
  tingrett
- **Arbeid/trygd**: Rikslønnsnemnda, Tariffnemnda, Tvisteløsningsnemnda, tvistenemnd i
  utdanningspermisjonssaker, ankenemnda i tidskontosaker, Beordringsmyndigheten, Klagenemnda for
  krav om erstatning og kompensasjon for psykiske belastningsskader (sekretariat SRF),
  Personvernnemnda (sekretariat SRF)
- **Utdanning**: Folkehøgskolen, Kulturskole, yrkesopplæringsnemnd, Klagenemnd for fag- og
  sveineprøver (nynorsk-eksempel + klagenemnd generelt), Oppfølgingstenesta (nynorsk),
  Foreldreutvalet for grunnopplæringa (FUG), fagskole, lokal klagenemnd og nasjonalt klageorgan for
  fagskoleutdanning, Direktoratet for høyere utdanning og kompetanse, Nasjonalt organ for kvalitet
  i utdanningen (NOKUT), NKR, Fagskole 1, Fagskolen Tinius (?)
- **Helse/barnevern**: reindriftsstyre(t) (usikker plassering), barnevernet, barnevernsinstitusjon,
  helseinstitusjon, legevakt, fylkesnemnda, Statens helsetilsyn, Statens undersøkelseskommisjon for
  helse- og omsorgstjenesten (Ukom/undersøkelseskommisjonen), Adopsjonsmyndigheten,
  Sjukehuset/sykehuset, Norsk helsenett SF
- **Forskning/kultur**: Nasjonalt utvalg for gransking av uredelighet i forskning
  (Granskingsutvalget), Kulturrådet, Norsk kulturfond, Norsk filminstitutt (generell regel), Den
  norske kirke, Kontoret for voldsoffererstatning
- **Forbruker/marked**: Forbrukertilsynet, Markedsrådet, Reisegarantifondet, Bankenes sikringsfond
- **Skatt/avgift/registre**: Skattekontoret, Skattemyndighetene, Oljeskattekontoret, Klagenemnda for
  petroleumsskatt, Skatteklagenemnda for Svalbard, Skattedirektoratet, Innkrevingsmyndigheten,
  Folkeregisteret, Folkeregistermyndighet, registerføreren, registeransvarlig, registerenheten (jf.
  lov 2019-03-01-2), Verdipapirsentral (ekstern, Euronext m.fl. — finnes det flere?)
- **Energi/sjø/samferdsel**: Reguleringsmyndigheten for energi, Nettselskap, Kraftleverandør,
  Fjernvarmeselskap, Statens kartverk, offisiell sjøkartmyndighet/nasjonal koordinator for
  navigasjonsvarsler, Sjøtrafikksentral, Lostjenesten, statsloser (del av Kystverket, rolle),
  havner/havneterminaler/havneanlegg, kommunal havnevirksomhet, Sokkeldirektoratet,
  Sjøfartsdirektoratet, Fiskeridirektoratet, Havforskningsinstituttet, Kystverket,
  Forsvarsdepartementet, Den internasjonale havbunnsmyndigheten, fiskesalslag
- **Kommunikasjon/media**: Nasjonal kommunikasjonsmyndighet, EFTAs overvåkingsorgan (ESA), Body of
  European Regulators for Electronic Communications (BEREC), Medietilsynet, Språkrådet, NRK
- **Forsvar/sikkerhet**: EOS-utvalget, domstolene, Etterretningstjenesten, Utvalget for evaluering
  av etterretningstjenesteloven, Stortingets kontrollutvalg for etterretnings-, overvåkings- og
  sikkerhetstjeneste, militær politimyndighet/militærpolitiet, Sikkerhetsmyndigheten (jf. lov
  2018-06-01-24 — konkrete ledd/paragrafer bør ha egen navneform, ellers uklart),
  Klareringsmyndigheten, Sivilforsvaret, Direktoratet for samfunnssikkerhet og beredskap,
  PNR-enheten (del av politiet?), Klagenemnda for disiplinærsaker i Forsvaret
- **Konkurranse/finans**: Konkurransetilsynet, Finanstilsynsklagenemnda, Etikkrådet for Statens
  pensjonsfond utland, Representantskapet (med sekretariat, sentralbankloven), Statens
  pensjonskasse, Husbanken
- **Valg/politisk**: Riksvalgstyret, Distriktsvalgstyre, Fylkesvalgstyre, Valgstyre,
  Kommunestyret (ansvar/oppgave/plikt), Fylkestinget, Stortinget
- **Statistikk/forvaltning generelt**: Statistisk sentralbyrå, Rådet for Statistisk sentralbyrå,
  Økokrim, Justervesenet, Sysselmesteren, sentral vergemålsmyndighet, felles organ (jf. lov
  2010-02-19-5, Fellesordningen for AFP), Politiets sikkerhetstjeneste, Integrerings- og
  mangfoldsdirektoratet, Landbruksdirektoratet, Klagenemnda for naturskadesaker, Klagenemnda for
  industrielle rettar, Datatilsynet, Assistansesenter, Den ansvarlige myndigheten/De ansvarlige
  myndighetene, Sertifiseringsmyndighet, Tollmyndighet, Lotteritilsynet, Norsk Tipping,
  forvaltningsmyndighet for et verneområde, Tilsynsmyndighet, utlendingsinternat,
  Helsedirektoratet, nasjonal geodatakoordinator
- **Andre/tredjeparter**: brannvesen, forsvaret (som virksomhet, ikke domene), Norges
  Juristforbund, Samfunnsviterne, KS, Namsfogden (viser rolle med tilknytning, men også ansvar —
  egen modelleringsutfordring), ombud (generisk), Norec, Enova SF (generell regel),
  Utenriksdepartementer/spesifikke departement, norske utenriksstasjoner (gruppe), utenriksstasjon
  (enkelt), Longyearbyen lokalstyre

## 6) Tredjeparter, utenlandske og private aktører (egen klassifiseringsutfordring)

- Norges idrettsforbund og olympiske og paralympiske komité + organisasjonsledd (idrettskretser,
  særforbund, særkretser og regioner, idrettsråd, idrettslag)
- Riksbanken og Nationalbanken — utenlandske aktører (klassifiseringsspørsmål: skal disse i det
  hele tatt inn i den norske virksomhetskatalogen?)
- Private aktører, f.eks. fastleger — hvordan klassifiseres disse?
- Skille på når en virksomhet bare NEVNES i en rettskilde vs. når den faktisk HAR ANSVAR (jf.
  https://lovdata.no/dokument/NL/lov/2024-12-13-76 — kommune, statlige virksomheter,
  fylkeskommuner)

## 7) Modellerings- og datamodellspørsmål (åpne)

- Myndighetstildelinger fungerer ikke helt på høyt nivå — kjent begrensning i dagens modell.
- Kommunestyret er «organer», dvs. rettssubjekter — trenger dette en egen kategori i modellen?
- Eierskap vs. sekretariatsfunksjon må modelleres separat, f.eks. for kommisjoner, nemnder, utvalg
  (eksempler: Vigg Kristiansen/Baneheia, Koronautvalget, Ekstremismekommisjonen, Riksrevisjonens
  undersøkelser om lønninger). Se også oversikt over klagenemnders organisering:
  https://www.klagenemndssekretariatet.no/om-oss/organisasjon — samme struktur som klageinstanser?
  jf. https://lovdata.no/lov/2022-03-18-12/§29
- «Arving av navneform» — uavklart om en fylkeskommune-type skal ha ÉN felles navneform, eller om
  hver enkelt fylkeskommune skal ha sin egen (risiko for eksplosjon i antall koblinger — anslått
  372×12).
- Vertskommune — bør modelleres som en «kan»-relasjon for virksomheter som har det, f.eks. fengsel,
  folkehøgskole.
- Advokatnemnda-eksempelet (jf. §4): ønsker å vise referanser via navneform, men mangler visning av
  hva virksomheten faktisk er ansvarlig for og hvorfor — modellen godkjenner navneformen, men gir
  ikke nok kontekst.

## 8) Datakvalitet i rettskilde-korpuset

- Kortnavn vs. fullt navn — bør kunne søkes på begge.
- Hvordan fjerne rene retningslinjer fra korpuset? Uklart hvilke dokumenttyper Lovdata faktisk
  leverer.
- Ikrafttredelsesfelt vs. konsolidert dato — usikkert om dette feltet finnes i data. Eksempler å
  sjekke: https://lovdata.no/dokument/NL/lov/2025-06-20-103 (kunngjort?),
  https://lovdata.no/dokument/NL/lov/2025-06-20-71
- Endringslover — når blir de inaktive? Eksempel: https://lovdata.no/dokument/NL/lov/2026-06-19-46
- Fordeling av rettskilder basert på år.

## 9) Relasjon til Forvaltningsdatabasen (Sikt)

https://forvaltningsdatabasen.sikt.no/dokumentasjon/enheter som datamodell-referanse. Usikkert om
Forvaltningsdatabasen tar med alt: mangler den hjemler? Tar den med eksterne/utenlandske
virksomheter?

## 10) Formål / bruksområder for verktøyet

- Finne utdaterte lover (eks. Televerket).
- Liste over rollevirksomheter som ikke er tilordnet en reell virksomhet, f.eks. fordi virksomheten
  er nedlagt → identifisere irrelevante/«sovende» lover og telle tilhørende sidetall. Mange
  forskrifter er overgangsregler — hvor lenge bør de regnes som aktive?
- Identifisere kandidater for modernisering, f.eks. «Lov um prisereglar og prisedomstolar».
- Klassifisering av lovverk etter type: delegering/myndighet (internt), tilsynslover,
  overgangsregler, rundskriv, engelske lover, endringslover.
- Vise vekst over tid og hva en virksomhet faktisk må forholde seg til av regelverk.
- Vise myndighetstildelinger på lovnivå med tilhørende forskrifter.
- Valgdistrikter som administrative inndelinger — egen datakilde?
- Lov om opphevelse av lov som egen dokumenttype: https://lovdata.no/dokument/NL/lov/2019-12-13-83

## Vedlikeholdsoppgaver

- Slette utdaterte tilordninger (f.eks. NTL), endre navn/tilordninger og passe på at koblinger
  følger med.

## Strukturelt poeng på tvers av alt over

Mye av dette (§1 avvisningsregler, §2 denyliste, §3 synonymer, §4 kjente presiseringer) peker på
samme underliggende behov: **disse mønster-katalogene bør bli synlige og redigerbare i systemet**
(Johanns eget punkt under §1: "deny-listen bør være synlig i systemet, og eventuelt mulig å
redigere") — i stedet for hardkodede C#-arrays (`Institusjonsord`, `VerketDenyliste`,
`FasteRollesubstantiv`, `AldriEgennavnOrd` m.fl. i `NavnekandidatOppdagelseTjeneste.cs`) som krever
en kodeendring + redeploy for hver justering. Å flytte disse til data (en admin-redigerbar tabell)
er trolig den enkeltendringen som gir mest videre gevinst, siden den gjør ALLE de andre punktene
(§1–§4) mulig å justere iterativt uten min involvering hver gang.

**Ikke besluttet**: om dette skal bygges først, eller om noe annet i lista skal prioriteres. Egen
samtale.
