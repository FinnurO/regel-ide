# Juristrunde #440 (hjemmelregelen i aktørkøen), del a: sammendrag av runde 1

Det er 19 kort. h1–h18 er de 18 navnene som får hjemmelforslag i dag, og R er selve regelen. Alle detaljene står i `utfordringer.json`.

Spørsmålet på hvert kort er om bestemmelsen er hjemmel for at aktøren **finnes** (oppretting eller navnevedtak, #441), og om navnet i det hele tatt er en aktør.

Slik er det kontrollert:
- **Basen:** lest med SELECT 2026-10-31.
- **Simulering:** regelen er kjørt på nytt mot alle 192 navn og 26 042 gjeldende noder (ikke merknad, ikke opphevet) i de 332 rettskildene med ventende navnekandidater. Simuleringen gir nøyaktig de 18 navnene og 24 forslagene i inndataene.
- **Fasit og Lovdata:** domstolloven er sjekket mot fasiten. Dl. §§ 9, 10, 27, 33, 33 a og 55 a, bhl. §§ 4, 5, 14 b og 24 og sameloven § 1-2 er kontrollert på Lovdata.

## Tall

| Dom | Antall |
|---|---:|
| holder | 4 |
| innvending | 14 |
| kontroller | 0 |
| prinsipiell | 1 (R) |

| Alvor | Antall | Kort |
|---|---:|---|
| høy | 11 | h1, h4, h6, h7, h8, h9, h10, h11, h12, h13, h18 |
| middels | 1 | h15 |
| lav | 2 | h3, h16 |

- **Holder:** h2 (foreldrerådet), h5 (samarbeidsutvalget), h14 (førstelagmannen) og h17 (lagmennene; treffet er tilfeldig).
- **Hullet er riktig svar** (`retting` = tom liste): h1, h4, h6, h7, h8, h9, h10, h11 og h12.
- **Ny hjemmel i stedet:**
  - h13: dl. § 27 første ledd, «I hver kommune skal det være et forliksråd».
  - h18: sameloven § 1-2 første ledd, og navnet kobles til den eksisterende aktøren SAMEDIGGI / SAMETINGET.
- **Riktig bestemmelse, men treff eller status må rettes:**
  - h3: legg til bhl. § 24 andre ledd, med paragrafoverskriften «Styrer».
  - h15: første punktum i § 55 a, med status fortolkning og ikke tekstfunn.
  - h16: treffet skal være «kommuneleger», med status normalisering.

## Regelen i tall (dagens forslag)

| Uttrykk | Forslag | Holder |
|---|---:|---:|
| skal … ha et | 14 | 6 |
| opprettes | 3 | 0 |
| oppnevner | 4 | 0 (h15 har riktig paragraf, men feil grunnlag) |
| etableres | 3 | 0 |
| **Sum** | **24** | **6** |

6 av 24 forslag holder, og to av dem bare tilfeldig. Regelen gir altså flere falske forslag enn riktige. Det bryter med prinsippet «heller hullet enn et feil forslag».

## De viktigste innvendingene

1. **Navnet står i setningen, men det er ikke navnet som opprettes (R, prinsipiell).**
   - «Leken skal ha en sentral plass i barnehagen», «Personalet skal ha et bevisst forhold til at barn …» og «Høyesterett skal ha en annen sammensetning … treffes av domstollederen» gir alle forslag.
   - Det samme gjelder «Det opprettes et reindriftsstyre … oppnevnes av Sametinget».
   - Retting: navnet må være kjernen i nominalfrasen uttrykket styrer. Frasen stopper ved preposisjon, «som», «at», «enn» eller komma, og kan ha sideordning med «og et».
2. **«oppnevner» gjelder kompetanse til å oppnevne og forutsetter at organet finnes (R, h4, h9, h11, h15).**
   - Ingen av de fire forslagene holder.
   - Uttrykket bør tas ut av lista over opprettelsesuttrykk.
3. **Feil aktør med riktig mønster (h10, h13, h18).**
   - Høyesteretts direktør er foreslått for DA-direktøren.
   - Felles forliksråd er foreslått for forliksrådet.
   - Reindriftsstyret er foreslått for Sametinget.
4. **Slutning vist som tekstfunn (h15, h16, h18).**
   - Statusen settes etter om ordet står ordrett, ikke etter om opprettelsen står der.
   - Det å oppnevne medlemmer eller gi oppgaver forutsetter at aktøren finnes. Det er fortolkning, aldri tekstfunn (docs/profil/03 §5).
5. **Ikke-aktører og klasser får hjemmel (h1, h4, h6, h7, h8, h11).**
   - Barna, medlemmer og «virksomheten» er ikke aktører.
   - Barnehagen, skolen og dommere er klasser eller roller.
   - I PR 2 må spørsmålet om aktørstatus avgjøres før hjemmelen. Hjemmelforslaget bør ikke vises før det er gjort.

## Tverrgående mønstre

- **Grupperingen slår sammen ulike referenter av samme appellativ:**
  - «styret»: privat barnehage (bhl. § 7) og Domstoladministrasjonen (dl. § 33 a)
  - «virksomheten»: helse og miljø i barnehager og skoler, og helse- og omsorgstjenester
  - «direktøren»: Domstoladministrasjonen og Høyesterett
  - «Klagenemnda»: tre nemnder i hvert sitt kapittel

  Ett hjemmelforslag per navn blir da feil for minst én av forekomstene. Regelen kan ikke skille referentene. Forslaget bør derfor merkes «referent ikke kontrollert».
- **Forledd som skiller organer:** «felles forliksråd» og «felles samarbeidsutvalg» er andre organer enn forliksrådet og samarbeidsutvalget. Det er målt i dataene, med to treff.
- **Bøyningsregelen** kobler ikke «kommuneleger» og «kommunelegen», men den kan koble homografer: «styre» er organet, mens «styrer» er personen.
- **Klasser og roller:** mange navn er organer eller roller som finnes én gang per barnehage, kommune eller lagmannsrett. Det gjelder foreldrerådet, samarbeidsutvalget, forliksrådet, styrer og førstelagmannen. Hjemmelen holder, men godkjenningen bør gi en klasse eller rolle og ikke én aktør.

## Det regelen mangler (spørsmål 2)

- **«det skal være et X»:** kontrollert. Gir forliksrådet riktig (fasit u63). Ellers i korpuset er kjernene organer som menighetsråd, fjellstyre og distriktsstyre.
- **Lovbestemte organer i en annen lov enn forekomsten:**
  - Sametinget (sameloven § 1-2)
  - nasjonal klagenemnd (fagskoleloven § 7 a, «Departementet kan opprette …»)
  - skoler (opplæringslova og privatskolelova, som er lastet)
  - Høyesterett (oppregningen i dl. § 1 og Grl.)

  Det er riktig at regelen ikke gjetter disse. Men hullet bør si at det ikke er søkt i andre lover, så «ikke funnet» ikke leses som «finnes ikke».
- **Leddhode og overskrift:** «daglig leder» under overskriften «Styrer» (bhl. § 24) blir ikke funnet.
- **Vedtekter:** kilde utenfor korpus. De kan bare gi `eksternt_dokumentert`, aldri tekstfunn.
- **«det skal velges» og «oppnevnes»** gjelder personer og ikke organer som finnes. Ikke ta dem med.

## Mønstre som bør utelates (spørsmål 3)

Alle er testet mot dataene:
- «oppnevner», «opprettes» og «etableres» med navnet hvor som helst i setningen: 0 av 10 holder.
- «skal … ha et» med navnet hvor som helst etter.
- «X opprettes/etableres» med navnet som subjekt: 2 treff, begge feil.
- «X oppnevnes»: Rettsvitner er feil, og klagenemnda er fortolkning.
- «skal ha» uten artikkel: 4 feil for «medlemmer».
- «X skal være et … organ»: en karakteristikk, ikke hjemmel.

Merket KONTROLLER, ikke målt i køen:
- «det opprettes/etableres/nedsettes et X»
- «skal/kan opprette et X»
- «ledes av et X»: 1 riktig, 1 falsk

Med de kontrollerte uttrykkene gir regelen 8 forslag, mot 24 i dag:
- 5 er riktige: foreldrerådet, styrer, samarbeidsutvalget, førstelagmannen og forliksrådet.
- 1 stoppes av forleddsperren: felles samarbeidsutvalg.
- 2 er feil fordi navnet er feil gruppert eller ikke er en aktør: direktøren og Dommere § 3.

Prisen er at kommunelegen og lagmennene får hull. I dag traff de bare tilfeldig.

## Prinsipiell innvending (R)

Regelen er feil formulert på tre punkter:
1. Den krever ikke at navnet er det som opprettes.
2. Den har med et kompetanseuttrykk («oppnevner») som opprettelsesuttrykk.
3. Den utleder kunnskapsstatus av ordet og ikke av opprettelsen.

Retteforslaget står i `retting` på kort R, med ny regeltekst, uttrykk med kontrollstatus og utelatte mønstre.
