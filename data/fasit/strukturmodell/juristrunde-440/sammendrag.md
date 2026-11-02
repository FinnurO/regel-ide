# Juristrunde 440: sammendrag

Runden gjelder hjemmelregelen i aktørkøen (docs/33 §5.16), før godkjenningen bygges i PR 2 av #440. Kortene er de 18 navnene som fikk
hjemmelforslag 2026-10-31 (h1–h18, 24 forslag) og selve regelen (R). Inndata: `inndata.json`, hentet med `GET /api/aktorko` og
`/api/aktorko/{nøkkel}` og SELECT mot delt `regelide`. Inndataene har også `hull_med_andre_uttrykk`: 35 av de 174 navnene uten forslag, der en
setning i samme rettskilde har navnet og et uttrykk utenfor lista (et grovt søk, ikke forslag). Ingenting er skrevet til basen.

Merk: regelen i koden setter grensen på seks ord mellom «skal» og «ha», ikke mellom uttrykket og navnet. Navnet kunne stå hvor som helst
etter «ha et/en/ei» i setningen, og ved «opprettes», «oppnevner» og «etableres» hvor som helst i setningen. Kort R beskriver regelen slik
koden gjør det.

**Runde 1** (`a/utfordringer.json`, `a/sammendrag.md`): 4 holder (h2 foreldrerådet, h5 samarbeidsutvalget, h14 førstelagmannen, h17
lagmennene, der treffet er tilfeldig), 14 innvendinger (11 høy, 1 middels, 2 lav) og 1 prinsipiell (R). Av de 24 forslagene holdt 6, og to
av dem traff tilfeldig.

| Uttrykk | Forslag | Holder |
|---|---:|---:|
| skal … ha et | 14 | 6 |
| oppnevner | 4 | 0 |
| opprettes | 3 | 0 |
| etableres | 3 | 0 |

**Svar** (`a/svar.json`): modellereren aksepterte 10 og svarte delvis på 5 (h3, h15, h16, h18, R). Fellesnevneren for de fem: juristen
foreslo riktige hjemler som regelen ikke kan finne uten fortolkning (bhl. § 24 via overskriften, dl. § 55 a, hol. § 5-5 via en annen
bøyning, sameloven § 1-2 i en annen lov). Modellereren mener de hører til saksbehandleren i PR 2, ikke til regelen. På R var
modellereren uenig i merknaden «referent ikke kontrollert», fordi regelen ikke kan vite om et ord har flere referenter.

**Runde 2** (`a/replikk.json`): juristen godtok h3, h16 og h18 og presiserte h15 og R. Uenigheten om selve regelen er løst. To punkter
står igjen. Begge gjelder hva skjermen i PR 2 viser.

## Den presiserte regelen (enige etter runde 2)

1. Navnet (samme ord eller en bøyningsform) må være **kjernen i nominalfrasen** uttrykket styrer. Frasen går fram til første preposisjon,
   «som», «at», «enn» eller komma, har høyst tre ord per ledd, og kan ha sideordning med «og/eller» og ny artikkel.
2. Uttrykkene er **«skal … ha et/en/ei/ett (eller flere)»** og **«det skal være et …» / «skal det … være et …»** (nytt). «oppnevner»,
   «opprettes» og «etableres» med navnet hvor som helst tas ut.
3. **Forleddssperre:** «felles forliksråd» og «felles samarbeidsutvalg» er andre organer og gir ikke forslag til navnet uten forleddet.
4. **Oppnevning, valg, utnevnelse og tilsetting** gir aldri hjemmelforslag (det er kompetanse og forutsetter at organet finnes).
5. **Kunnskapsstatus:** tekstfunn når navnet står ordrett som kjernen, normalisering ved bøyning. Det som bare forutsetter at aktøren
   finnes (oppgaver, sammensetning, oppnevning), gir ingen status fra regelen. Det er fortolkning, og den gjør et menneske.
6. **Ikke med før de er målt (KONTROLLER):** «det opprettes/etableres/nedsettes et X», «skal/kan opprette et X», «ledes av et X».
7. **Lovbestemt i en annen lov** (Sametinget i sameloven § 1-2, nasjonal klagenemnd i fagskoleloven § 7 a, skoler i opplæringslova) og
   **vedtekter** (utenfor korpus) gjettes ikke. De vises som eget hull, «ikke søkt i andre lover».

Testet og utelatt, fordi de gir falske forslag i dataene: navnet hvor som helst ved «oppnevner/opprettes/etableres» (0 av 10 holder),
navnet hvor som helst etter «skal … ha et», «X opprettes/etableres» og «X oppnevnes» med navnet som subjekt, «skal ha» uten artikkel
(4 falske for «medlemmer») og «X skal være et … organ».

Med de to kontrollerte uttrykkene gir regelen 8 forslag i stedet for 24. 5 er riktige (foreldrerådet, styrer, samarbeidsutvalget,
førstelagmannen, forliksrådet), ett stoppes av forleddssperren, og 2 er feil på grunn av grupperingen eller aktørstatus (direktøren,
Dommere § 3). Kommunelegen og lagmennene får hull. De traff bare tilfeldig før.

## Uenighet som står igjen (til Johann)

**1. h15 (Innstillingsrådet): hva hullet skal hete.** Begge er enige om at regelen gir hull, og at saksbehandleren registrerer dl. § 55 a
første ledd med status fortolkning.
- **Juristen:** «Hjemmel for aktøren mangler» er misvisende når rådet er opprettet i loven der navnet står. «Ikke funnet» er ikke
  «finnes ikke» (docs/profil/03 §5). Teksten bør si hva som er undersøkt, for eksempel «ikke funnet av regelen». Det gjelder også
  kommunelegen.
- **Modellereren:** teksten «Hjemmel for aktøren mangler» er Johanns avgjørelse i #441, og den står også på aktørsiden. Å endre den i
  køen alene gir to ulike tekster for samme hull. Et skille mellom «ikke søkt» og «søkt, ikke funnet» er likevel mulig uten å endre
  hovedteksten, som en `Metatekst` under den. Valget er Johanns, fordi det endrer en låst tekst.

**2. R: hvordan saksbehandleren ser at forslaget gjelder feil aktør.** Begge er enige om at merknaden «referent ikke kontrollert» går ut.
- **Juristen:** at ingenting er forhåndsvalgt (#440 AK5), hindrer ikke at et menneske godkjenner et forslag som ser riktig ut («Høyesterett
  skal ha en direktør» for DA-direktøren). Vis hjemmelen og forekomstene med paragraf side om side, og si rett ut når hjemmelen står i en
  annen paragraf enn alle forekomstene. Det kan regnes ut uten å gjette. Målt på de 8 forslagene: tre får markeringen, direktøren (feil),
  Dommere (feil) og førstelagmannen (riktig). Fire av de fem uten markering er riktige.
- **Modellereren:** opplysningen er sikker, men den skiller ikke riktig fra feil: en hjemmel står ofte i en annen paragraf enn bruken
  (førstelagmannen § 10 mot § 11 er riktig). Markeringen kan derfor lære saksbehandleren å avvise riktige forslag. Paragrafen står
  allerede ved hver forekomst og ved forslaget på siden i dag (docs/09 §50). Å vise avviket rett ut er et designvalg for PR 2, og målingen er
  bare 8 forslag.

## Funn utenfor regelen (egne saker, ikke rettet her)

- **Grupperingen slår sammen ulike referenter av samme ord:** «styret» (bhl. § 7 og dl. § 33 a), «virksomheten» (to forskrifter),
  «direktøren» (DA og Høyesterett), «Klagenemnda» (tre nemnder).
- **Aktørstatus før hjemmel:** barna, «medlemmer» og «virksomheten» er ikke aktører. Barnehagen, skolen, dommere, foreldrerådet og
  forliksrådet er klasser eller roller.
- **Bøyningen** kobler homografer («styre» er organet, «styrer» personen), men ikke «kommuneleger» og «kommunelegen».
- **Rutingen kjenner ikke registernavnet med skråstrek:** «Sametinget» havner i køen, selv om aktøren finnes som «SAMEDIGGI / SAMETINGET».
- **Setningsdelingen** deler etter «1.» i «1. januar» (kjent fra PR #444).

**Tid og tokens** (harnessens telling): runde 1 11 min 52 s og 256 428 tokens, runde 2 (samme agent, fortsatt) 1 min 56 s og om lag 17 000 tokens.
