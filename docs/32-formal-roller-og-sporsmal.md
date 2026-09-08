# 32. Formål, roller og spørsmålene modellen skal besvare

**Status:** utkast til gjennomgang med Johann (2026-09-08) · **Gjelder:** all utvikling i Regel-IDE

Dette dokumentet finnes fordi utviklingen begynte å drive: funksjoner ble bygget riktig etter
instruks, men uten å tjene formålet, og måtte korrigeres gang på gang. Formålet står spredt i
`01-referansemodell.md` (begrepsapparatet), `02-produktkrav.md` (skjermer og krav) og
`10-rules-as-code-landskap.md` (landskapet) — men det står ingen steder kort nok til å bæres med inn
i hver enkelt byggerunde. Det er det denne teksten skal gjøre.

Den erstatter ingenting. `01-referansemodell.md` er fortsatt begrepsapparatet, og skal leses først
ved uklarhet om hva et Begrep, Vilkår eller en Rettskilde ER. Dette dokumentet sier hva vi driver med
og hvordan man vet om en leveranse faktisk bidro.

---

## 1. Grunnkonseptet

**Loven er ustrukturert og ikke maskinlesbar. Hele formålet er å gjøre den maskinlesbar.**

Det følger én bindende konsekvens av det: **strukturering skal skje uten gjetting.** En
maskinlesbar lov som er feil er verre enn en som mangler — en feil kobling ser like autoritativ ut
som en riktig, og forplanter seg til alt som bygger på den. Dette er hvorfor «ingen gjettet
fallback» (`03-domenemodell.md` §3.3) går igjen overalt i koden: det er ikke en stilkonvensjon, det
er kjernen i konseptet. Der vi ikke vet, skal vi vise at vi ikke vet, ikke fylle inn noe plausibelt.

Praktisk betyr det:

- Et menneske bekrefter koblinger som ikke er entydige. Maskinen foreslår, mennesket avgjør.
- Alt som er utledet skal kunne spores tilbake til stedet i teksten det er utledet fra.
- Vi skiller alltid *hva teksten sier* fra *hva vi har tolket den til*, og fra *hva systemet gjorde
  automatisk*. Sammenblanding av disse tre er den vanligste alvorlige feilen.

## 2. Problemet dette løser

Forvaltningsapparatet er i praksis **skjult**. Hvem som forvalter en lov, hvem som fatter vedtak,
hvem som er klageinstans, hvem som fører tilsyn, hvem loven gjelder for — alt dette står i lovtekst
og forskrifter, men ingen kan lese hele korpuset. Derfor lever kunnskapen i stedet på nettsider, i
rundskriv, i hodene til folk, og den er ikke etterprøvbar mot kilden.

Regel-IDE gjør dette apparatet synlig ved å strukturere det som allerede står i teksten. Navneformer,
grupperinger og myndighetstildelinger er ikke datamodellering for sin egen del — de er mekanismen som
gjør et usynlig apparat spørrbart.

## 3. Spørsmålene modellen skal kunne besvare

Dette er den operative delen av dokumentet. Hver leveranse skal kunne peke på hvilket spørsmål den
flytter framover.

| # | Spørsmål | Hva som må være strukturert for å svare |
|---|---|---|
| S1 | **Hvem forvalter loven, og hvordan?** | Ansvarlig departement; organer som utøver myndighet etter bestemte paragrafer; i hvilken egenskap (vedtak, klage, tilsyn, forskrift) |
| S2 | **Hvem har ansvaret?** | Rollen et organ har under en gitt bestemmelse — ikke bare at det er nevnt |
| S3 | **Hvem blir berørt?** | Plikt- og rettighetssubjekter: hvem loven gjelder for, direkte eller via gruppemedlemskap |
| S4 | **Hvilke tjenester og oppgaver gir loven?** | Tjenester og handlinger loven hjemler, koblet til bestemmelsen som hjemler dem |
| S5 | **Hva er de faktiske navnene og betegnelsene?** | Alle navneformer et organ opptrer under i lovtekst — gjeldende, utgåtte, kortformer, feilskrivinger |
| S6 | **Hva gjelder for denne konkrete aktøren?** | Oppslag fra en virksomhet til bestemmelsene som treffer den, inkludert indirekte via gruppe |
| S7 | **Hva skjer hvis denne bestemmelsen endres?** | Påvirkningsanalyse: hvilke aktører, tjenester og regler henger i den |

Listen er ikke låst, men den skal utvides bevisst, ikke skli.

## 4. Roller og inngangsvinkler

Samme modell, ulike innganger. Rekkefølgen under er prioritert (Johann, 2026-09-08: «alle tre, men
modelløren først»).

**Modelløren** — jurist eller fagansvarlig som strukturerer regelverket. Førstebruker. Trenger å
kunne uttrykke en regel med de tingene modellen inneholder: at et vilkår gjelder for
«språkutviklingskommuner», at et vedtak fattes av kommunen og påklages til Statsforvalteren. Det er
her verdien realiseres — et normativt lag ingen regel konsumerer er ikke verdt noe ennå.

**Saksbehandleren i en virksomhet** — kommer fra sin egen virksomhet og spør S6: hva gjelder for oss?
Trenger oppslaget fra virksomhet til plikter, inkludert de indirekte.

**Analytikeren / den som forvalter regelverket** — spør S1, S2, S7. Trenger oversikten over
apparatet, og konsekvensanalyse ved endring.

**Utvikleren** — trenger den maskinlesbare modellen som utdata, med sporbarhet tilbake til kilden
intakt hele veien.

## 5. Hva en tagg er

Dette er skrevet ut eksplisitt fordi det har vært den gjentatte feilkilden.

En `TekstTagg` er **sporbarhetsleddet mellom normativ tekst og semantisk modell** — en påstand om at
denne strengen, på denne posisjonen, i denne bestemmelsen, betegner denne modellerte tingen. Den er
ikke en markering for at brukeren skal se noe fint. Markeringen i teksten er *konsekvensen* av
påstanden, ikke formålet.

Det følger to ting av det:

1. **Kriteriet for om noe er verdt å tagge**: bidrar taggen til å besvare et av spørsmålene i §3?
   «Karasjok» i forskrift om forvaltningsområdet for samiske språk besvarer S3 og S6 — den binder
   kommunen til en gruppe med plikter. «Karasjok» i en fredningsforskrift besvarer ingenting; der er
   det et stedsnavn. Kriteriet er ikke «er dette et egennavn».
2. **Fullstendig fangst av hver setning er IKKE målet** (Johann, 2026-09-08: «det tror jeg blir for
   ambisiøst gitt alle kompliserte setninger og paragrafer og referanser»). Vi fanger det som gjør
   spørsmålene besvarbare, ikke alt en setning påstår.

## 6. Hvordan dette skal brukes i utvikling

Før en byggerunde starter:

- **Navngi spørsmålet.** Hvilket av S1–S7 flytter denne leveransen, og for hvilken rolle i §4? Kan
  det ikke besvares, er ikke oppgaven forstått ennå.
- **Si hvor gjettingen kan snike seg inn**, og hva som gjøres i stedet. Enhver utledet kobling
  trenger et svar på «hva om vi tar feil».
- **Akseptansekriterier formuleres som spørsmål modellen skal kunne besvare etterpå**, ikke som
  «feltet finnes» eller «siden viser X».

Ved verifisering:

- **Åpne skjermen kald**, slik en bruker gjør. Å klikke seg til den tilstanden man forventer, og så
  bekrefte at den er der, er ingen verifisering — det er en selvoppfyllende sjekk. Dette er en reell
  feil som ble gjort 2026-09-08: taggene var «verifisert synlige» fordi verifiseringen først byttet
  til riktig lag; en bruker som åpnet siden så umarkert tekst.
- **Skill systemtilstand fra påstand om verden.** «Avvist» må vise om det var maskinen eller et
  menneske. «Sovende» må ikke leses som at en kommune er nedlagt.

---

## 7. Åpne spørsmål

- **Rolle-aksen** (S1/S2): `MyndighetstildelingEntitet` har ingen rolle — modellen kan si at et organ
  er tildelt en gruppe, men ikke i hvilken egenskap (vedtak/klage/tilsyn/forskrift).
  `RettskildeEntitet.FunksjonellRolle` finnes med verdien `kompetansenorm`, men er aldri populert og
  ligger på dokumentnivå. Uten dette leddet kan ikke modelløren uttrykke en vedtaksregel. Ikke avklart
  om dette er neste steg.
- **Vilkår som konsumerer gruppebegrep** (§4, modelløren): ingen regel refererer i dag et
  gruppebegrep som subjekt. Ikke avklart om dette er neste leveranse.
- **S4 (tjenester og oppgaver loven gir)** er den svakest dekkede av spørsmålene — Tjeneste/Handling
  finnes som entiteter, men koblingen «loven hjemler denne tjenesten» er ikke systematisk strukturert
  fra lovteksten slik aktørsiden nå er.
