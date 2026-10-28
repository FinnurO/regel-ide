# Juristrunde #390, runde 1: kortform og bindingen av «Alta»

Fire kort er vurdert. Kildene er docs/33 §4.1, §4.6, §5.7, §5.8, §5.10 og §5.11, issue #390, forskriftsteksten i lokal base, kommuneloven § 3-1 og stadnamnlova §§ 2 og 11 (begge kontrollert på Lovdata), forskrift om stadnamn § 7 og Kartverkets kommuneinfo-API. FORMAT.md har ingen regler om navneformer eller kortformer.

## Tall

| Dom | Antall | Alvor |
|---|---|---|
| holder | 1 (k1) | |
| innvending | 3 (k2, k3, k4) | middels 1 (k2), lav 2 (k3, k4) |
| kontroller | 0 | |
| prinsipiell | 0 | |

## Viktigste innvendinger

1. **k2 (middels): kortformen er dokumentert for OMRÅDET, ikke for rettssubjektet.** Et kommunenavn er et stadnamn for et administrativt område (stadnamnlova § 2 bokstav a) og vedtas av Kongen (kommuneloven § 3-1 tredje ledd). Kartverket dokumenterer områdets navn, og Enhetsregisteret dokumenterer orgnr og kommunenummer. At rettssubjektet heter «Alta», er en slutning fra disse to premissene. Statusen `eksternt_dokumentert` viser altså en slutning som om den var et registerfaktum. Etter regelen om svakeste premiss er det en avledning, men CHECK-en på begreper tar ikke `avledet`, og det er en mangel i formatet. I korpuset betyr «Alta» alene sted eller område i 15 av 16 rettskilder. Påstanden i #390 om at ordet betyr «kommunen i andre rettskilder», stemmer ikke med målingen. I tillegg teller klassifisereren også ikke-vurderte navneformer som treff. Da gir de 209 forslagene «flertydig» for kommunenavn som før traff bare området.
2. **k4 (lav): registeret gir de samiske og kvenske navnene.** Kartverket har `gyldigeNavn` (for eksempel Porsáŋgu, Porsanki og Hámmerfeasta). De mangler bare fordi øyeblikksbildet ble hentet med et filter. Etter stadnamnlova § 11 andre ledd skal navnene brukes «i register saman med» det norske. Begrensningen er et hull i dataene, ikke en regel.
3. **k3 (lav): kollisjonssjekken ser bare dagens inndeling.** Bindinger følger verket gjennom eldre versjoner. Navn som før 2020 tilhørte en annen kommune (Bø, Os, Nes og Sande), og kortformer som også er vanlige ord (Time, Sel, Ås og Fjord), er ikke listet. Matchingen skiller ikke mellom store og små bokstaver.

## Tverrgående mønstre

- **Området er den vanlige betydningen av et kommunenavn som står alene.** Det gjelder både etter loven (stadnamnlova § 2) og i korpuset. Modellen bør behandle det slik og ikke bare som én av to likeverdige referenter. Det går ikke på bindingen i k1, som er riktig, men på hvilken referent kortformen har når ingen binding finnes, og på kunnskapsstatusen i k2.
- **Et forslag virker i oppslaget før det er vurdert.** For bindinger brukes bare vurderte. For navneformer teller også `maskinelt_foreslatt` som treff. Det bryter med prinsippet i kortet om at bare vurderte bindinger brukes, slik det er formulert i docs/33 §5.7 og §5.10.
- **Hull i dataene presenteres som regler.** Det gjelder filteret i øyeblikksbildet (k4) og inndelingen bare på én dato (k3).

## Prinsipielle innvendinger

Ingen. Skillet mellom område og rettssubjekt og bindingen per rettskilde holder. Innvendingene gjelder statusen, dataene og at forslag virker i oppslaget, ikke selve modellregelen.

## Merknad til k1 (holder)

docs/33 §5.7 sier fortsatt at en bindings referent er «virksomhet, eller begrep av typen klasse eller rolle». At en kortform kan bindes til et område (#390, commit c98b735), står ikke der. Begrunnelsen for bindingen blir sterkere med forskriften § 1 første ledd («dekker én eller flere kommuner») og stadnamnlova § 2 bokstav a.
