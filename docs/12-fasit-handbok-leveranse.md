# 12. Fasit: Håndbok/rundskriv-leveranse

## Hensikt

Dette er det første av flere planlagte "fasit"-dokumenter — en konkret beskrivelse av hva regel-ide
faktisk skal **levere** på et gitt område, ikke bare hvilke skjermer/entiteter som finnes. Johann
sin observasjon (2026-07-30): vi har bygget mye funksjonalitet, men har ikke definert et sluttresultat
å måle den opp mot, og uten det er det ikke sikkert vi faktisk gir verdi. Håndbok/rundskriv er
førstevalget fordi det er *der definisjonen av behandlingen og dermed vilkårene skjer* — får vi ikke
dette til, er resten (vilkårstre, begrep, tjeneste) bygget på et svakt fundament.

Dokumentet brukes fremover til to ting:

1. **Skriveregel/målbilde** når håndbok-funksjonalitet videreutvikles — hva "bra" ser ut som.
2. **Skåringsgrunnlag for tester**: når det skrives tester/verifiseringer av håndbok-output, skal
   det faktiske resultatet sammenlignes mot dimensjonene under og gis en %-sats per dimensjon (se
   "Skåringsmodell"). Samme prinsipp gjelder skjermbilder mot Claude Design-mockupene i
   `prototyper/` — en egen, enklere sammenligning, se eget avsnitt til slutt.

## Kildegrunnlag

Fasiten er destillert fra `skjenkebevilling-rundskriv_3.md` (Johanns opplastede eksempel, 2026-07-30)
— en reell, ferdig veiledning til behandling av søknad om skjenkebevilling. Etter eksplisitt
instruks er **alt som er DMN/DRD-spesifikt i kildedokumentet ignorert** — regel-ide har ingen
DMN-motor. Der kildedokumentet skriver "hentet fra DMN-modellen" eller "beslutning i DRD-en", leses
det her som "hentet fra vilkårstreet og datasett-parametrene" — det er regel-ides eget svar på
samme rolle (strukturert, maskinlesbar beslutningslogikk), ikke en ny mekanisme å bygge.

## Hovedfunn: en god håndbok forteller VILKÅRSTREET, ikke bare paragrafene

Dette er det viktigste enkeltfunnet, og det har reelle arkitektoniske konsekvenser — ikke bare en
stilforbedring.

Referansedokumentets kapittelstruktur (§3–§7) følger **ikke** lovens paragrafrekkefølge. Den følger
**saksbehandlingens faktiske beslutningssekvens**: formalia → serveringsbevilling → vandel →
kvalifikasjon → kommunalt skjønn — nøyaktig rekkefølgen en saksbehandler faktisk går gjennom
spørsmålene i, uavhengig av hvor i loven hvert vilkår står. §2 åpner med en kort, nummerert oversikt
over nettopp disse fem spørsmålene, hver koblet til navnet på vilkåret/vurderingen den svarer til.

Dette **er** en lineær rendering av et vilkårstre i beslutningsorden (AK-3.4-treet: rotnode
"Vedtak om skjenkebevilling", barn V-ALDER/V-VANDEL/R-SKJENKETID osv.) — med kommunale
datasett-parametre (§8) og skjønnsveiledning (§7, §9) vevd inn på riktig sted i sekvensen, ikke
tilføyd som et vedheng.

**Regel-ide i dag bygger noe strukturelt annerledes.** `HandbokKommentarMetadataEntitet`
(`src/RegelIde.Data/Entiteter.cs:117-150`) knytter én kommentar til én rettskilde-node
(`NodeId`, linje 119) — en håndbok er organisert etter *lovens/forskriftens* kapittel-/
paragrafstruktur, én kommentar per bestemmelse. Det finnes ingen mekanisme som ordner innhold etter
en tjenestes vilkårstre-traversering, og ingen som vever inn kommunale parameterverdier automatisk.
En jurist som skriver kommentarer i dagens UI kan **ikke** produsere noe som ligner § 2 eller § 8 i
referansedokumentet uten å gjøre det manuelt, paragraf for paragraf, uten hjelp fra applikasjonen.

**Dette er ikke nødvendigvis en feil å rette umiddelbart** — det er et reelt veivalg som bør tas
bevisst, ikke stilltiende:

- **Alternativ A — behold paragraf-anchoring, gjør hver kommentar rikere.** Håndboken forblir
  organisert etter lovteksten; §2/§8-lignende innhold må skrives som egne, frittstående
  kommentarseksjoner (f.eks. festet til § 1-1 eller et eget "oversikts"-kapittel forfatteren selv
  lager). Billigst å bygge videre på, men tvinger forfatteren til å gjøre koblingen til vilkårstreet
  manuelt hver gang.
- **Alternativ B — bygg en ny, tjenestesentrert visning** som *genererer* saksgangs-oversikten og
  kommune-tabellene fra vilkårstreet + datasett-verdiene automatisk, og lar håndbok-kommentarer feste
  seg til **vilkårstre-noder** i tillegg til rettskilde-noder (`FraNodeId`-mønsteret fra Referanser,
  generalisert). Dette er i praksis en ny leveranse — ikke en utvidelse av eksisterende
  `HandbokForfatterTjeneste` — og forutsetter at kommunale parameterVERDIER faktisk lagres et sted
  (se dimensjon D under: `DatasettEntitet` har i dag ingen verdi-kolonne, kun feltdefinisjon).

**Avgjort 2026-07-30: Alternativ B.** Johann valgte den tjenestesentrerte visningen. Bygget som
`VeiledningRepository` (`src/RegelIde.Api/VeiledningRepository.cs`) + `GET /api/tjenester/{id}/
veiledning?virksomhetId=`, en ny `VilkarstreKommentarEntitet` (kommentarer festet til vilkårstre-noder,
ikke rettskilde-noder) og en ny `DatasettVerdiEntitet` for de faktiske kommunale/nasjonale
parameterverdiene — se skåringstabellen under for hva som faktisk ble oppnådd og hva som fortsatt
mangler. `HandbokForfatterTjeneste`/`HandbokKommentarMetadataEntitet` er urørt — Alternativ B ble
bygget som en ny, parallell leveranse, ikke en ombygging av den eksisterende paragraf-anchorede
håndboken (som fortsatt er riktig verktøy for ren lovkommentar, uavhengig av tjenestens vilkårstre).

Dimensjonene under gjelder uavhengig av hvilket alternativ som velges; de beskriver kvaliteten på selve INNHOLDET, ikke
hvor det bor.

## Kvalitetsdimensjoner

Hver dimensjon har: hva idealet faktisk gjør, hvor regel-ide står i dag, og det konkrete gapet.

### A. Sporbarhet på tre nivåer — ikke bare kryssreferanser

Idealet merker **hver enkelt påstand** som én av tre ting: hentet direkte fra regelverket (med
`§`-hjemmel oppgitt), hentet fra en strukturert kilde (DMN/kommunale parametre — for oss:
vilkårstre/Datasett), eller forfatterens eget skjønn ("**Praktisk råd (forfatterens vurdering, ikke
hentet fra kilde)**" — gjentatt eksplisitt hver gang, § 7 og § 8.3 i eksempelet).

Regel-ide har i dag `Opprinnelse` ('import'/'manuell', `Entiteter.cs:159-164`) — men **kun på
kryssreferanser**, ikke på selve kommentarteksten. En håndbok-kommentar har ingen måte å markere at
"dette avsnittet er forfatterens eget praktiske råd, ikke en gjengivelse av loven" — sanueringslisten
(`KommentarTekstSanering.cs:31-35`) tillater `p/h3/h4/b/strong/i/em/u/a`, ingen egen semantisk
markør for proveniens på avsnittsnivå.

**Gap:** en `Kommentartype`/`Sikkerhetsgrad`-lignende markør per avsnitt eller per kommentarseksjon
(f.eks. `hjemmel`/`kilde`/`praktisk-rad`), rendret visuelt distinkt (som i eksempelet), er ikke bygget.

### B. Presisjon på tall og frister — aldri "en del år"

Idealet gjengir alltid konkrete tall: 10 år vs. 5 år (§5), 20 år (§6), klokkeslettvinduer med
sesongvariasjon (§8.1). Der noe ikke er kjent, sier det **eksplisitt** "Ikke registrert i
kildematerialet" eller "Ikke modellert ennå" (§8.2, §8.3) — i stedet for å utelate det stilltiende.

Regel-ide har ingen sperre mot vage formuleringer siden `MinimalEditor.tsx` er fritekst — dette er
ikke et strukturelt gap, men en forfatterdisiplin-norm som bør inn i en fremtidig
skrive-veiledning/lint (f.eks. varsle hvis en kommentar mangler et `§`-anker der `Dokumenttype` er
`retningslinje`/`instruks`).

### C. Kommunale variasjoner som strukturert data, ikke fritekst

Idealet gir hver kommune med egne parametre en egen tabell (§8.1–8.3), én eksplisitt
**standardregel-rad** for kommuner uten registrert regelsett (§8.4, med en advarsel om at det er en
teknisk standardverdi, ikke en juridisk norm), og et delvis kjent tilfelle (Vennesla) løses med
"ikke kjent"-felter pluss en praktisk instruks — ikke ved å late som dataene er komplette.

Regel-ide har `DatasettEntitet` (`Entiteter.cs:375-394`) — men det er en **felt-definisjon**
(`Felt/Prop/Dtype/Type/Kilde`), ikke en verdi. Det finnes **ingen entitet** som lagrer at "Tønsberg
sin skjenketid gruppe 1/2 er 08:00–02:00" — bekreftet ved gjennomsøk av `RegelIde.Data`/`RegelIde.Api`.

**Gap:** dette er det tyngste, mest konkrete gapet i hele dokumentet. Uten en
`DatasettVerdi`-entitet (eller lignende) knyttet til `Virksomhet`, kan regel-ide aldri produsere noe
som ligner § 8 automatisk — det må skrives som fritekst i en håndbok-kommentar per kommune, uten
noen garanti for at verdien faktisk stemmer med det som er registrert et annet sted i systemet.

### D. Skjønn som egen sjanger, ikke skjult i teksten

Idealet skiller alltid loven **selv** sine skjønnsmomenter (§7, listet fra alkoholloven § 1-7a) fra
forfatterens egne praktiske råd om hvordan momentene bør vektes (samme §7, tydelig merket). Skjønn
er ikke gjemt inni en vanlig avsnitt — det er en egen, gjenkjennelig sjanger i dokumentet.

Regel-ide har `Vilkar.Vurderingstype` (`regelbasert/skjonnsbasert/hybrid`) og
`SkjonnsgrunnlagBegrepId`/`SkjonnsmomenterJson` på Vilkår-entiteten — momentene KAN registreres
strukturert. Men ingenting kobler dette til håndbok-teksten, og "praktisk råd"-sjangeren fra
dimensjon A gjelder like mye her.

### E. Taksonomi på vilkår i selve vedtaket — atskilt fra eligibility-vilkår

Idealet (§9) skiller **seks** distinkte typer innhold i selve vedtaket: faste vilkår, skjenke-/
åpningstider (regel + kommunal justering innenfor en ytre grense), et vilkår **avledet av en annen
bevilling** (serveringsstedets åpningstid, avledet av skjenketiden), gebyr, sakspesifikke
OPPLYSNINGER som **ikke er vilkår i egentlig forstand** (fakta gjengitt fra søknaden), og
skjønnsbaserte tilleggsvilkår kommunen selv kan sette.

Dette begrepet — "vilkår/opplysninger i selve vedtaket, gyldig etter innvilgelse" — finnes **ikke**
i regel-ides egen domenemodell. `docs/01-referansemodell.md:117-119` og `docs/03-domenemodell.md:
212-220` er eksplisitte: Vedtak/Vedtaksgrunnlag/**Vedtaksvirkning** er driftsdata regel-ide bevisst
IKKE eier — det eies av `forklaringsmodell-api`. Regel-ides eget `Vilkar` er alltid et
ELIGIBILITY-vilkår (evaluert FØR vedtaket), aldri et løpende vilkår PÅ vedtaket. Dette er nøyaktig
funn #6 i `06-veikart.md` ("individuelle vilkår satt ved selve bevillingsvedtaket, § 4-3") — notert
til byggesteg 4/7, ikke løst.

**Gap:** en håndbok kan i dag ikke systematisk gjengi § 9-innhold, fordi regel-ide ikke har noe
sted å registrere det strukturert — bare vilkårstreets pre-vedtak-vilkår.

### F. Dokumentgraf, ikke monolitt

Idealet er ett dokument av flere som henger sammen — det viser aktivt til et søsterdokument
(`skjenkebevilling-datakilder-og-vilkarsvarighet.md`) for kildedetaljer per inputvariabel og
vilkårsvarighet, i stedet for å pakke alt inn i én tekst. Dette matcher regel-ides eksisterende
"håndbok er sin egen rettskilde med kapitler"-modell fint — poenget er at en enkelt håndbok bør
kunne **lenke** til en annen håndbok/rettskilde for detaljer, ikke at alt duplikat-skrives.

Regel-ide har kryssreferanse-mekanismen (`RettskildeReferanseEntitet`, generalisert denne uken til
å gjelde alle noder) — teknisk mulig i dag, ikke et gap. Nevnes for kompletthet.

### G. Struktur som handlingsverktøy: sjekkliste + tydelig avslagsbegrunnelse

Idealet avsluttes med en avkrysningsbar sjekkliste (§11) og et eksplisitt krav om at et avslag skal
si **hvilket** punkt (3–7) som var utslagsgivende (§10) — dokumentet er skrevet for å BRUKES i en
konkret sak, ikke bare leses som bakgrunnsstoff.

`KommentarTekstSanering.cs` tillater ikke `<ul>/<li>`/checkbox-elementer i det hele tatt (bekreftet
— kun `p/h3/h4/b/strong/i/em/u/a`) — en sjekkliste kan ikke skrives inn i dagens editor uten å falle
tilbake til nummererte `<p>`-avsnitt uten avkrysning.

**Gap:** dette er et rent editor-/sanerings-gap, ikke et datamodell-gap — enklest å tette av alle
punktene i dette dokumentet.

## Skåringsmodell

For fremtidige tester/verifiseringer av håndbok-output: skår hver dimensjon A–G på denne skalaen,
vektet likt (dimensjon E teller likevel tyngst i praksis siden den ofte er blokkerende, se merknad):

| Skår | Betydning |
|---|---|
| 0 % | Ingen støtte — verken datamodell eller UI kan produsere dette i dag. |
| 25 % | Teoretisk mulig med dagens felter, men ingen UI-vei til det (må gjøres via API/database). |
| 50 % | UI-vei finnes, men krever manuelt, disiplinert forfatterarbeid uten noen hjelp/validering fra applikasjonen. |
| 75 % | Applikasjonen hjelper aktivt (validering, strukturerte felt, delvis generering), men produserer ikke sluttresultatet automatisk. |
| 100 % | Applikasjonen genererer eller strukturerer dette automatisk fra allerede lagret data. |

**Status før runde 2 (2026-07-30, se over):**

| Dimensjon | Skår | Kort begrunnelse |
|---|---|---|
| A — Sporbarhet på tekstnivå | 25 % | `Opprinnelse` finnes, men bare på referanser, ikke kommentartekst. |
| B — Presisjon på tall | 50 % | Mulig i fritekst, ingen validering/norm. |
| C — Kommunale variasjoner som data | 0 % | Ingen verdi-lagring finnes (kun felt-definisjon). |
| D — Skjønn som egen sjanger | 25 % | Skjønnsmomenter kan lagres strukturert på Vilkår, men ikke koblet til håndbok-tekst. |
| E — Vilkår-i-vedtak-taksonomi | 0 % | Begrepet finnes ikke i domenemodellen — bevisst utenfor regel-ides eierskap i dag. |
| F — Dokumentgraf | 75 % | Kryssreferanse-mekanismen dekker det teknisk; ingen forfatterveiledning for NÅR man bør lenke vs. skrive. |
| G — Sjekkliste/handlingsstruktur | 0 % | Sanueringslisten tillater ikke liste-/checkbox-elementer. |
| Hovedfunn (saksgang i beslutningsorden) | 0–50 %* | 0 % automatisk generert; 50 % hvis Alternativ A velges og en jurist skriver det manuelt som egne kommentarseksjoner. |

*Hovedfunnet skåres ikke som en åttende dimensjon i snittet — det er et arkitekturvalg (A/B), ikke
en gradert kvalitet, og bør besvares før resten av tabellen brukes til å prioritere byggerekkefølge.

**Status etter runde 2 (2026-07-30) — Johann valgte Alternativ B, tjenestesentrert visning fra
vilkårstreet. Verifisert live i browser mot ekte seed-data (Tønsberg/Bærum-skjenketider), ikke bare
enhetstester.**

| Dimensjon | Skår | Kort begrunnelse |
|---|---|---|
| Hovedfunn (saksgang i beslutningsorden) | **75 %** | `GET /api/tjenester/{id}/veiledning` genererer nå faktisk saksgangen automatisk fra vilkårstreet i `Rekkefolge`-orden, med unntak vevd inn rett etter sin `gjelderRegel`s barn — verifisert visuelt at treet gjengis riktig. Ikke 100 %: ingen egen dokumentversjonering/publisering av selve veiledningen (bevisst utsatt, se Context) — den er en live-rendret visning, ikke en fastfrosset "utgave" en jurist kan godkjenne og datostemple. |
| C — Kommunale variasjoner som data | **75 %** | `DatasettVerdiEntitet` lagrer nå faktiske verdier per (Datasett, Virksomhet), med en egen standardverdi-rad og korrekt fallback-logikk (verifisert: byttet virksomhet fra "(standard)" → Tønsberg → Bærum i veiledningen, klokkeslett-verdien og kildeangivelsen endret seg riktig hver gang). Ikke 100 %: `DatasettDetalj.tsx` er en ren admin-side, ingen validering av at en verdi faktisk matcher `Dtype` (en boolsk-felt kan få en fritekst-streng registrert uten varsel). |
| A — Sporbarhet på tekstnivå | **75 %** | `VilkarstreKommentarEntitet.Dokumenttype` (`hjemmel`/`praktisk-rad`/`kommentar`/`sjekkliste`) er nå en reell, lagret proveniens-merking per avsnitt, rendret visuelt distinkt (fargede Tag-er) i både Egenskapspanelet og veiledningen — verifisert live. Ikke 100 %: merkingen er valgfri og manuell (forfatteren MÅ velge riktig type selv, ingen kontroll av at en "hjemmel"-kommentar faktisk siterer en lovtekst). |
| G — Sjekkliste/handlingsstruktur | **50 %** | Sanitizeren tillater nå `ul/ol/li` (verifisert med enhetstest at markup overlever saneringen). Nedjustert fra en optimistisk 75 %: **`MinimalEditor`s formateringsverktøylinje har ingen knapp for å sette inn en liste** — bekreftet ved å lese verktøylinjen live (kun Avsnitt/Overskrift/B/I/U/Lenke/Referanse). En forfatter kan i praksis ikke produsere en sjekkliste uten å skrive/lime inn rå HTML. Selve blokkeringen (server) er borte, men UI-veien finnes ikke — nøyaktig 50 %-definisjonen i tabellen over. |

**Ikke re-skåret denne runden** (bevisst utenfor scope, se Context): B (forfatterdisiplin, ikke kode),
D (skjønnsmomenter kan nå festes en veiledningskommentar via samme mekanisme som A, men selve
koblingen skjønnsmoment↔kommentartekst er ikke bygget strukturert), E, F (uendret).

## Skjermbilde-konformitet mot Claude Design

Egen, enklere sammenligningsakse — gjelder UI/visuell verifisering, ikke innhold. Når en browser-
verifisering tar skjermbilder av en ferdig skjerm, sammenlign mot den tilhørende mockupen i
`prototyper/*.dc.html` (der en finnes) og angi en grov %-vurdering (layout/komponentvalg/
informasjonstetthet — ikke pixel-for-pixel) i verifiseringsteksten. Ingen egen fasit-fil nødvendig
her — mockupen ER fasiten. Håndbok-relaterte skjermer har i dag ingen egen mockup i `prototyper/`
(kun `Byggesteg1-Rettskilder.dc.html` og den pre-QA `Regel-IDE.dc.html`) — dette bør bemerkes som
"ingen design-fasit å måle mot" heller enn å hoppes over stilltiende, hvis/når håndbok-skjermer
testes visuelt.
