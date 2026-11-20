# Juristrunde om «regjeringen» (#463), runde 1: sammendrag

Inndata: `inndata.json`, med kortene Q, R og r1–r50 (52 kort). Dommene står i `utfordringer.json`.

## Tall

| Dom | Antall | Kort |
|---|---|---|
| holder | 34 | |
| innvending | 14 | 10 lav, 2 middels, 2 høy |
| kontroller | 3 | r1, r2, r38 |
| prinsipiell | 1 | Q |
| **Sum** | **52** | |

Innvendinger etter alvor:
- **Høy:** R og r13.
- **Middels:** r14 og r42.
- **Lav:** r4, r7, r8, r10, r18, r20, r22, r23, r26 og r35.

Forekomstene (r1–r50) etter modellererens lesning:

| Lesning | Kort | Holder | Innvending | Kontroller |
|---|---|---|---|---|
| A (statsrådet) | 17 | 12 | 4 | 1 |
| A_medlem | 8 | 7 | 1 | 0 |
| B (Kongen i statsråd) | 6 | 1 | 3 | 2 |
| B_historisk | 3 | 3 | 0 | 0 |
| fremmed | 10 | 6 | 4 | 0 |
| fremmed_og_B | 1 | 0 | 1 | 0 |
| ikke_aktor | 3 | 3 | 0 | 0 |
| funksjon | 1 | 1 | 0 | 0 |
| uavklart | 1 | 1 | 0 | 0 |

Bare én B-lesning holder uten videre: r37, der myndighet etter en traktat er delegert ved kgl.res. Alle de andre B-lesningene er slutninger fra fremgangsmåten, ikke fra teksten.

## Anbefaling: kontekstavhengig, med en regel som ikke gjetter

Verken A eller B holder som standardlesning.

**Grunnloven bruker selv ordet i minst tre betydninger:**
- styring av riket eller regentskap (§§ 9, 11, 13, 36, 40, 41, 43 og 44);
- statsrådskollegiet (§§ 76 og 82);
- rollen, i «medlemmer av regjeringen» (§ 72 tredje ledd). Det er samme krets som «Statsrådets medlemmer» i § 62 annet ledd.

I tillegg kommer § 25 tredje ledd, der ordet betyr den utøvende makt, og § 32, der det betyr statsrådet i kongens fravær.

Grunnlovsspråkutvalget sier uttrykkelig at ordet er flertydig, og at det ble beholdt uendret av den grunn (Dok. 19 (2011–2012) pkt. 3.4.4).

Begge organene finnes og bør finnes i modellen:
- **Statsrådet:** Grl. § 12 første ledd, §§ 13, 15 og 39–41.
- **Kongen i statsråd:** § 3, § 28 og § 30, og Håndboka del I pkt. 3.8.

Ordet «regjeringen» skal ikke være navneform for noen av dem.

### Forslag til regel

Bindingen gjelder én forekomst (node og posisjon), ikke rettskilden.

**Regel 1–6 er ordlyd eller Grunnloven. Ingen gjetting:**

1. **Fremmed** (prøves først). Gjelder:
   - «regjeringen i <stat>», unntatt Norge og Kongeriket Norge;
   - «den <adj.> regjeringen»;
   - «<stat>s regjering»;
   - sanksjonsforskrifter, avgrenset ved tittel.
2. **Generisk konvensjonsstat.** Maler og definisjoner i konvensjonstekst gjelder alle statene, også Norge. De gir ingen kant, og de er ikke «fremmed».
3. **Ikke aktør.** Gjelder:
   - «regjeringen.no»;
   - en lukket liste over navn;
   - avtaletitler («Avtale mellom regjeringen i …»).

   Regelen er ikke et prefiks på «Regjeringens …».
4. **Rolle.** «medlem(mer) av [X, Y og] regjeringen» og «regjeringens medlemmer» gir rollen medlem av statsrådet. Regelen gjelder også i sideordnede oppregninger. Den gjelder ikke «… i regjeringen» etter statssekretærer eller politiske rådgivere.
5. **Funksjon.** «føre», «forestå», «tiltre», «ivareta», «bestyre» eller «anordne» regjeringen betyr styring av riket. Det gir ingen aktør.
6. **«Regjeringens resolusjon».** Dette er en vedtaksform:
   - Kompetansen er Kongen i statsråds (Grl. § 32: beslutningene «utferdiges i kongens navn»).
   - Statsrådet handler i kongens fravær (§§ 13 og 41).
   - Formen behandles som i `FastsattAvTolker.cs`.
   - Den er ikke historisk: 14 gjeldende forskrifter i korpuset er fastsatt slik.

**Regel 7 gir bare forslag.** Resten får ingen standardlesning. Signalord gir et forslag merket [slutning], og et menneske bekrefter per forekomst:
- **B:** «legge fram for» eller «foreslå for Stortinget», budsjettproposisjon og traktatmyndighet.
- **A:** «fremme sak for», «rapportere til», «rådgi», «anmodning fra» og «regjeringens politikk».
- **Utøvende makt:** «avskåret fra forbindelse med regjeringen».

Lesningslisten bør få fire nye verdier: «kontekstavhengig», «utøvende_makt», «generisk_konvensjonsstat» og «politisk_ledelse».

## De viktigste innvendingene

1. **Q, prinsipiell. A eller B er et falskt valg, og «per rettskilde» er for grovt.**
   - Grunnloven, klimaloven (§ 7 første og annet ledd), miljøvernkonvensjonen art. 11 (r26) og trygdeavtaleforskriften (r42) har ulike lesninger i samme kilde.
   - r26 og r42 har to ulike referenter i samme node.
   - Dok. 19 pkt. 3.4.4 slår fast at ordet er flertydig i Grunnloven.
2. **R, høy. Regel 4 treffer en russisk resolusjon, og regel 4 tar ikke med det som allerede er bygget.**
   - Regel 4 fyrer på r43, «endret ved regjeringens resolusjon nr. 380 av 27. mars 2024». Sanksjonsforskrifter fanges ikke av konvensjonssperren, som går etter overskrift.
   - Regel 4 krever et menneske, men Grl. § 32 og `FastsattAvTolker.cs` («Alle fire er Kongen i statsråd») avgjør alt kompetansen.
   - Regel 2 mangler unntak for «regjeringen i Kongeriket Norge».
   - Prefikset i regel 3 treffer «Regjeringens resolusjon», «Regjeringens departementer» og «Regjeringens/Samferdselsdepartementets».
   - Regel 1 mangler sideordning.
   - Funksjonsregelen mangler.
3. **r13, høy. Fvl. § 85 støtter ikke A.** Unntaket gjelder kollegiale organer «som fatter enkeltvedtak», og i regjeringen er det Kongen i statsråd. NOU 2019: 5 pkt. 5.1.1 skiller ikke mellom A og B: regjeringen er «et kollegialt organ» som treffer vedtak «ved Kongen i statsråd». Med A alene blir Kongen i statsråd omfattet av nemndreglene i §§ 86–91. Q bruker dette kortet som støtte for A.
4. **r1, r2 og r10, kontroller. Plikten til å legge fram noe for Stortinget er et modellvalg.**
   - Dok. 19 pkt. 3.4.4 leser «regjeringen» i Grl. §§ 76 og 82 som statsrådskollegiet (A). Proposisjoner og meldinger godkjennes likevel formelt i statsråd (B).
   - Det må avgjøres én gang om P-kanten skal gå fra kollegiet eller fra beslutningsformen.
   - I r10 er formen dessuten åpen («på egnet vis», i motsetning til «I budsjettproposisjonen» i første ledd).
5. **r14 og r42, middels. To kort er lest feil.**
   - **r14:** «statsråder, statssekretærer eller politiske rådgivere i regjeringen» er ikke «medlemmer av regjeringen». Grl. § 72 tredje ledd holder de to gruppene fra hverandre.
   - **r42:** Ordlyden er en avtaletittel og dermed ikke en aktør. At «avtaleparten er Kongen i statsråd», er en slutning som ikke er merket. Folkerettslig er parten staten.

## Tverrgående mønstre

- **«Fremmed» er brukt for bredt (r7, r18, r26 og r35).** Maler og definisjoner i konvensjonstekst gjelder alle statene, også Norge. Det gir riktig utfall (ingen kant), men feil kategori. Regelen bryter sammen så snart en norsk gjennomføringsbestemmelse knytter seg til teksten.
- **Sideordning er behandlet ulikt.** «medlemmer av Stortinget og regjeringen» er satt til A_medlem (r15, r16). «medlemmer av Kongehuset, Stortinget, regjeringen, Høyesterett» er satt til A (r22, r23).
- **B begrunnes med fremgangsmåte og ikke med tekst** (r4, r8 og r10). Alt som formelt går gjennom statsrådet, er blitt B, også der teksten bare sier hvem noe skal sendes til.
- **Genitiv er ikke struktur** (r20). «Regjeringens departementer» skal ikke gi en G-kant fra departementene til statsrådet, når #480 legger departementene inn som del av staten.
- **Utenfor kortene** (r31): forskriften om eiendomsskatt nevner «Kongen eller Kongehuset, Stortinget, domstolene, Regjeringen eller departementene». Der er «Kongen» monarken. Det kolliderer med navneformen «Kongen» → Kongen i statsråd (#335).

## Runde 2

Modellereren godtok 17 kort. Hun svarte «delvis» bare på Q: standardenheten for bindingen skal være noden (binding med spenn på én eId), og en forekomst som avviker, tagges for seg. Hun vil ikke bygge en ny bindingstype per tegnposisjon.

Replikken min på Q er **presiserer** (`replikk.json`). Jeg godtar noden som standardenhet på tre vilkår:

1. En tagg for én forekomst har samme status som en binding: godkjent og faglig vurdert, med gyldighet, og den følger verket (#384). Den går også foran nodens binding.
2. En nodebinding dekker ikke stille forekomster som ikke er tagget. Er noen av flere forekomster i noden ikke tagget, gir oppslaget «flertydig».
3. Ordlydsreglene virker per forekomst som tagger.

Premisset om at bare r26 og r42 har to forekomster i samme node, og at ingen av dem gir kant, gjelder bare utvalget. I korpuset har 25 gjeldende noder minst to forekomster:
- **Helsingforsavtalen art. 48 første ledd** har tre forekomster av «utpekt av regjeringen»: om Danmark, om Finland og om «hvert av de øvrige land», som omfatter Norge. Den siste gir den norske regjeringen kompetanse til å utpeke representanter til Nordisk råd, og det er en kant.
- **Reglement for avgradering av dokumenter utstedt i Stortinget § 2 annet ledd** har både organet («oversendt fra regjeringen») og rollen («medlem av regjeringen») i samme ledd.

Vilkåret modellereren selv satte for å bygge noe nytt, er dermed oppfylt. Forekomsttagger løser det likevel hvis vilkår 1–3 holder.

## Kilder

- Grunnloven i korpuset (eli `https://lovdata.no/eli/lov/1814/05/17/nor`, gjeldende versjon), §§ 1, 3, 9, 11–15, 22, 25–32, 36, 39–44, 62, 72, 76 og 82.
- Dokument 19 (2011–2012), Grunnlovsspråkutvalgets rapport:
  - pkt. 3.4.4: https://stortinget.no/no/Saker-og-publikasjoner/Publikasjoner/Dokumentserien/2011-2012/dok19-201112/3/
  - kap. 4, merknader til §§ 9, 11, 36, 40, 41 og 43: https://stortinget.no/no/Saker-og-publikasjoner/Publikasjoner/Dokumentserien/2011-2012/dok19-201112/4/
- Dokument 16 (2011–2012) er Menneskerettighetsutvalgets rapport og behandler ikke ordet. Kilden er derfor ikke brukt: https://www.stortinget.no/global/pdf/dokumentserien/2011-2012/dok16-201112.pdf
- NOU 2019: 5 Ny forvaltningslov, pkt. 5.1.1 (PDF-s. 93) og pkt. 36.3.11 (PDF-s. 564): https://www.regjeringen.no/contentassets/8d681c58c7a94929954ca9538a2476a6/no/pdfs/nou201920190005000dddpdfs.pdf
- Statsministerens kontor, Håndbok for politisk ledelse (mai 2026), del I pkt. 2.3, 3.5, 3.7 og 3.8: https://www.regjeringen.no/contentassets/bf7a48eac08a42a5a8758969c5ec64f8/no/pdfs/handbok-for-politisk-ledelse-200526.pdf
- Innst. 465 S (2025–2026), om Grl. § 41 (Dokument 12:16 (2023–2024)): https://stortinget.no/no/Saker-og-publikasjoner/Publikasjoner/Innstillinger/Stortinget/2025-2026/inns-202526-465s/?all=true
- Store norske leksikon:
  - «regjering»: https://snl.no/regjering
  - «statsråd»: https://snl.no/statsråd
  - «kongelig resolusjon»: https://snl.no/kongelig_resolusjon (nevner ikke «regjeringens resolusjon»)
- Wikipedia, «Kongelig resolusjon» (sekundærkilde for når «regjeringens resolusjon» brukes): https://no.wikipedia.org/wiki/Kongelig_resolusjon
- **Ikke funnet:** SMK-veiledningen «Om Kongen i statsråd» og «Om r-konferanser» på regjeringen.no. PDF-lenkene fra søk ga 404 den 2026-11-18. Innholdet om regjeringskonferanser er hentet fra Håndboka.
- Repoet: `src/RegelIde.Kildekonvertering/FastsattAvTolker.cs`, og korpusets `fastsatt_av` (14 gjeldende rettskilder med «Regjeringens res.»).
