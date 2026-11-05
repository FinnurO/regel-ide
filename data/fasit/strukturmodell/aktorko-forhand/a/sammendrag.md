# Aktørkøen, forhåndsklassifisering, del A: juristens runde 1

Inndata: `../inndata-a.json` (92 kort, n2–n96). Utdata: `utfordringer.json`, ett objekt per kort i samme rekkefølge. Bare lesing: SELECT mot
den delte basen (virksomheter, begreper, strukturkanter, rettskilder, rettskilde_noder), fasitfilene for domstolloven,
helse- og omsorgstjenesteloven og spesialisthelsetjenesteloven, og issue #440 og #309. Ingenting er skrevet til basen.

## Tall

| Dom | Antall |
|---|---:|
| holder | 63 |
| innvending | 28 |
| kontroller | 1 |
| prinsipiell | 0 |
| **Sum** | **92** |

| Alvor (innvendinger) | Antall |
|---|---:|
| høy | 1 |
| middels | 9 |
| lav | 18 |

Pilotkortene (11): 8 holder (n2, n5, n14, n22, n33, n52, n76, n84), 1 middels (n13 medlemmer), 2 lav (n24 bostedskommune, n36
omsorgskommune, begge bare relasjonsretningen).

Ingen ikke-aktør er gjort til rettssubjekt. De fire forslagene om ny aktør (Utdanningsdirektoratet, NOKUTs klagenemnd, Longyearbyen
lokalstyre, Barne-, ungdoms- og familieetaten) er riktige som aktør, og ingen av dem finnes i basen under et annet navn. «Regjeringen» er
satt til kontroller.

## De viktigste innvendingene

1. **n35 Longyearbyen lokalstyre (høy).** Aktøren og hjemmelen holder (svalbardloven § 30 andre ledd), men «har_ansvarsomrade Svalbard» gir
   motsatt resultat for Svalbard utenfor Longyearbyen. Korpuset legger kommunemyndigheten til lokalstyret «for Longyearbyen
   arealplanområde» og til Sysselmesteren utenfor (FOR-2015-06-22-747 § 9 femte og sjette ledd). Kortets egen forekomst sier «innenfor
   Longyearbyen arealplanområde». Området er navngitt og skal ha egen node.
2. **n86 Dommere (middels).** «Mangler» er feil. Domstolloven § 52 første ledd definerer «dommere» i loven (tekstfunn, avgrenset til
   loven, #309 L7). Relasjonene mangler jordskifterett (§ 55 første ledd) og forliksråd (§ 52 annet punktum).
3. **n31 institusjonen og n47 Virksomheter (middels).** Ikke_aktør er feil for forekomstene i helse- og omsorgstjenesteloven. Institusjonen
   i § 10-4 har tvangskompetanse (fasiten a48). «Virksomheter» i § 12-3 a er et avkortet treff av klassen «Virksomheter som er omfattet av
   loven her» (fasiten a60), som er pliktsubjekt overfor statsforvalteren. Begge grupperinger slår sammen ulike referenter.
4. **n7 spesialisthelsetjenesten (middels).** Rollen «innehas av RHF» bygger på sørge-for-ansvaret i shl. § 2-1 a. Det er en umerket
   slutning, og den gjør RHF til eneste pliktsubjekt. Fasiten har tjenestesektoren (RHF, HF og private, shl. § 1-2 første ledd).
5. **n10 regionalt helseforetak (middels).** De fire «har_ansvarsomrade»-kantene er lagt på klassen, men de hører til hvert medlem. De
   finnes alt som godkjente kanter RHF → helseregion fra vedtektene. Medlemskapet står ikke i hfl. § 4 (slutning), og hjemmelen er
   tekstfunn («skal det være ett regionalt helseforetak»), ikke normalisering.
6. **n3 barnehageeier (middels).** Bare kommunen er innehaver. For private barnehager er det rettssubjektet etter bhl. § 7 andre ledd og
   § 7a første ledd, og hjemmelen bør være hele § 7. Ellers knyttes forekomstene i tilskuddsforskriftene til kommunen.
7. **n91 eieren (middels).** «Eieren» i bhl. § 56 sjuende ledd er den som eier barnehagen (aksjonær, morselskap; § 22 første ledd bokstav
   a), ikke barnehageeieren etter § 7. Det er en privat part, som «eierens nærstående» ved siden av.
8. **n13 medlemmer (middels, pilot).** Én rolle «medlem (av organ)» for forliksrådet og NOKUTs klagenemnd. Rollene bør skilles per organ,
   og hjemmelen finnes («Forliksrådet skal ha tre medlemmer», dl. § 27 andre ledd).
9. **n38 private familiebarnehager (middels).** Familiebarnehage er ikke en underklasse av privat barnehage; bhl. § 15 andre ledd har
   «kommunale og private familiebarnehager».

## Tverrgående mønstre

- **«Mangler» brukt der det finnes hjemmel eller medlemsregel med kilde** (n13, n17, n42, n57, n86). Johanns regel 2026-11-05 sier at
  hullet bare vises når begge mangler. Bestemmelser som definerer ordet (dl. § 52), sier at verv skal finnes («skal ha tre medlemmer»,
  «kan det utnevnes en dommer som leder»), eller gir et kriterium (rammeplanens vedtektskrav for samiske barnehager) er hjemmel eller
  medlemsregel med fortolkning, selv om køregelen ikke foreslår dem.
- **Relasjonsretning for roller.** n3 bruker «rolle_for» for det rollen gjelder og «rolle_innehas_av» for innehaveren. I n24, n26 og n36
  står kommunen som «rolle_for», selv om den er innehaveren. Det bør rettes samlet.
- **Grupperingen slår sammen referenter** også der det ikke er flagget: n31 (barnevernsinstitusjon og institusjon utpekt av RHF), n28
  (Finland som territorium og som traktatpart), n70/n81 (samme tekststed som to navn, «bestemmende organ» og «det bestemmende organ»,
  som gir dobbel tagging). Der det er flagget (n13, n15, n16, n44, n47), er løsningen ofte å skille ut forekomstene, ikke å lage en
  felles node.
- **Forslagene avviker fra fasiten uten begrunnelse.** Seks innvendinger (n7, n31, n47, n58, n86, n88) gjelder ord som aktørlistene i
  fasitfilene for domstolloven og helse- og omsorgstjenesteloven alt har klassifisert. Forslagene sier ikke hvorfor de avviker fra
  fasiten.
- **Slutning uten merke** i relasjoner: n7 (spesialisthelsetjenesten → RHF), n10 (medlemmene), n70/n81/n44 (organet ≠ rettssubjektet).
  Motsatt er n29 merket som slutning, men folkeregisterloven § 1-3 første ledd sier det.
- **Venter:** «det offentlige» (n82) hører riktig til notatet om staten. Sverige (n45) og Finland (n28) hører ikke til notatet om Norge og
  forvaltningsnivåene. De territoriale forekomstene kan klassifiseres som område nå. Bare Finland som traktatpart (reindriftsloven § 81)
  er et spørsmål om stater som rettssubjekt.

## Kontroller (til Johann)

- **n40 regjeringen.** Grunnloven bruker ordet om både utøvende makt og organet (§§ 76, 82). Kollegiet er «statsrådet» (§ 12). Spørsmålet
  er om «regjeringen» skal være et eget organ (statsrådet), med organsammensetning mot «Kongen i statsråd», eller navneform for «Kongen
  i statsråd», slik «Kongen» ble i #335. Det kan også høre hjemme i notatet om staten og departementene. Relasjonen
  «kontroller_forhold_til» bør ikke opprettes.

## Prinsipielle innvendinger

Ingen.
