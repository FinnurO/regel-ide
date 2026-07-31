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

Fasiten er nå `docs/kildegrunnlag/skjenkebevilling-rundskriv-fasit.md` — Johanns opplastede
**versjon 4** (2026-07-31, erstatter `skjenkebevilling-rundskriv_3.md` som primærkilde; v3 er ikke
slettet, men v4 er den gjeldende fasiten). Etter eksplisitt instruks er **alt som er DMN/DRD-spesifikt
i kildedokumentet ignorert** — regel-ide har ingen DMN-motor. Der kildedokumentet skriver "hentet fra
DMN-modellen" eller "beslutning i DRD-en", leses det her som "hentet fra vilkårstreet og
datasett-parametrene" — det er regel-ides eget svar på samme rolle (strukturert, maskinlesbar
beslutningslogikk), ikke en ny mekanisme å bygge.

**Endringer fra v3 til v4** (relevante for denne fasiten — ikke en fullstendig diff):

- **Nytt §3 "Habilitet"** (fvl. § 8), satt inn FØR Formalia, som saksgangens første punkt. Dette er en
  vurdering av **saksbehandleren selv**, ikke av søkeren/virksomheten — den passer ikke inn i
  Vilkår/Regelnode-ontologien (som alltid evaluerer eligibility for søkeren). Se ny merknad under
  dimensjon E.
- **§6 Vandelsvurdering kraftig utvidet**: krever identifikasjon av alle relevante personer/
  virksomheter med organisasjonsnummer/D-/F-nummer, og lister ni konkrete "Årsaker til avslag" — dette
  er **Fakta**, ikke prosa, og bør kunne registreres strukturert (se ny merknad om håndbok som
  fakta-kilde nedenfor).
- **§7 "større arrangement" er nå tallfestet** (>1000 gjester) — enda et konkret eksempel på en
  terskelverdi som bør være et Datasett-parameter, ikke en frase i løpetekst.
- **§9 utvidet med "Gyldighet"** (4 år, eller til 30. september året etter neste kommunevalg — en
  avledet/beregnet regel, ikke en ren betingelse) og **"Prikkbelastning"** (sanksjonssystem, hører
  sammen med funn #5/#3 i `06-veikart.md` om kontroll/tilsyn — ikke modellert, ikke del av denne
  runden).
- **Bekreftet, konkret illustrasjon av et reelt strukturelt problem**: v4 har **to seksjoner
  nummerert "## 11."** ("Sjekkliste for saksbehandler" og "Sjekkliste for søker") og en tredje
  seksjon skrevet `##12.` (uten mellomrom) — fordi nummereringen er tastet inn manuelt i
  kildedokumentet og ingen forfatter/verktøy fanget opp duplikatet. Dette er ikke en kritikk av
  kildedokumentet i seg selv (det er et Word/Markdown-dokument uten strukturvalidering) — det er
  **beviset** på at regel-ide selv aldri må gjøre samme feil mulig: se ny prinsipp-seksjon
  "Rekkefølge og nummerering: aldri en manuelt redigerbar literal" nedenfor.
- **Ny "## 12. Relevante tjenester"-seksjon**: en flat liste over ~14 navngitte, relaterte tjenester
  (Omsetningsoppgave, Serveringsbevilling, Skjenkebevilling for et enkelt arrangement, osv.) — navn
  som tekst, ikke lenker/ID-er. Speiler nøyaktig samme gap som dimensjon C/A: en ekte, strukturert
  kobling (mot `Tjeneste`-registeret) gjengitt som fritekst i kildedokumentet.
- **§8 forenklet**: viser nå bare én kommunes parametertabell inline i stedet for v3s
  side-ved-side-sammenligning av flere kommuner — ikke en regresjon i seg selv (samme datamodell-gap
  som før), men betyr at v4 alene ikke lenger demonstrerer multi-kommune-variasjon like tydelig som
  v3 gjorde. Begge dokumentene brukes derfor sammen som kildegrunnlag der det er relevant.
- **Ingen inline rettskilde-lenker** — bekreftet av Johann selv ("den ikke inneholder
  referanser/linker til rettskilder, men det må den gjøre"): kildedokumentet siterer paragrafer som
  ren tekst ("alkoholloven § 1-7b"), aldri som klikkbare lenker. Dette er **ikke** et krav til
  kildedokumentet — det er et krav til **regel-ides genererte output**. Se fiksen under dimensjon A.

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

## Prinsipp: rekkefølge og nummerering er alltid beregnet, aldri en redigerbar literal

v4-kildedokumentets duplikate "## 11." (se over) er ikke et kuriosum — det er en konkret advarsel
Johann pekte på direkte: **"Du foreslår manuell nummerering. Det vil ikke fungere."** Dette
korrigerer ordlyden i den opprinnelige runde-1-planen, der `VilkarstreKommentarEntitet.Rekkefolge`
ble beskrevet som å tilby "manuell overstyring av visningsorden" — en fremtidig UI-affordanse som
aldri ble bygget denne runden, men som var feil retning å beskrive den i.

**Prinsippet som gjelder videre, for `Rekkefolge` og enhver fremtidig seksjons-/leddnummerering i
håndbok/veiledning-output:**

- Et lagret sorteringsfelt (`Rekkefolge`, eller en fremtidig håndbok-avsnitts-rekkefølge) er en
  **intern sorteringsnøkkel** — aldri et felt en forfatter skriver en literal verdi inn i via et
  tekst-/tallfelt.
- Reordning skjer via **strukturelle handlinger** (dra-og-slipp, opp/ned-knapper, "sett inn før/
  etter") som omberegner nøkkelen bak kulissene — aldri ved at brukeren taster "11" eller "12" i et
  input-felt.
- All nummerering som **vises** til en leser (§-nummer, listepunkt-tall, sjekkliste-nummer) beregnes
  **fra listeposisjon ved rendering**, aldri fra en lagret streng en forfatter selv har skrevet. Da
  er en duplikat-nummerering som v4s strukturelt umulig å produsere.

Dette er ikke implementert som kode denne runden (ingen reorder-UI er bygget ennå for
`Rekkefolge` — den settes i dag kun av backend som "append til slutten") — det er en **stående
designbegrensning** for når en slik UI faktisk bygges, tilsvarende hvordan dimensjonene under er
mål-tilstander, ikke ferdige leveranser.

## Håndboken er en forfatterflate for vilkår/fakta/sjekklister — ikke bare en lesevisning

Johanns presisering (2026-07-31): **"Samtidig så er det her man lager de detaljerte vilkårene som
brukes i vilkårstreet og sjekklister. Her definerer man også fakta."** Dette korrigerer en implisitt
premiss i runde-1-designet: `VeiledningRepository`/`TjenesteVeiledning.tsx` ble bygget som en
LESEVISNING som genereres FRA et allerede eksisterende vilkårstre — men i praksis er
håndbok-/veiledningsarbeidet (minst delvis) der de underliggende Vilkår-detaljene,
Datasett/"Fakta" og sjekklistene faktisk **skrives første gang**, ikke bare der de gjengis.

v4s §6 (identifikasjonskrav, ni konkrete avslagsgrunner) og §7 (>1000-gjester-terskelen) er gode
eksempler: dette ER fakta/vilkårsdetaljer en jurist typisk formulerer FØRST i en håndbok-tekst, som
deretter bør kunne bli strukturerte `Vilkar.ParametreJson`/`Datasett`-verdier eller
`Skjonnsmomenter`-oppføringer — ikke omvendt. Dagens `Egenskapspanel`s "Veiledning"-fane lar en
forfatter feste en kommentar TIL en allerede eksisterende Vilkår/Regelnode-node, men gir ingen vei
til å OPPRETTE et nytt Vilkår, sette dets `ParametreJson`, eller registrere en `DatasettVerdi` uten å
forlate håndbok-konteksten og gå til en helt separat side (Vilkårstre/Datasett-registeret). Dette er
en reell arkitektonisk implikasjon for frontend-design — se vurderingen under.

## Håndbok-nivå rettskildeomfang

Johanns nye krav (2026-07-31): en håndbok må kunne **deklarere tidlig, ved opprettelse**, hvilke(n)
rettskilde(r) den omhandler — kan være flere. Konkret eksempel fra fasiten: alkoholloven (lov),
alkoholforskriften (forskrift), kommunens alkoholpolitiske retningslinje, OG forvaltningsloven (siden
Habilitet/Formalia siterer fvl. §§ 8/11/17).

Dette finnes **ikke** i dag — `HandbokForfatterTjeneste`/`HandbokKommentarMetadataEntitet` er
strengt paragraf-anchoret til ÉN rettskilde-node (`NodeId`) per kommentar, og
`VeiledningRepository` har ingen håndbok-nivå-entitet overhodet (den er en live-rendret projeksjon,
ikke en persistert "håndbok"-rad å feste et rettskildeomfang til). Et sett med relaterte rettskilder
kan i dag kun uttrykkes implisitt, spredt over enkelt-referanser på hver enkelt Vilkår/Regelnode-node
— det finnes intet sted som svarer på "hvilke rettskilder handler denne håndboken/veiledningen om,
totalt sett" som ett samlet svar. **Gap, ikke løst denne runden** — adressert i vurderingen under.

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

**Rettelse 2026-07-31**: Johann påpekte at fasitens kildedokument selv IKKE har klikkbare
rettskilde-lenker, men at regel-ides genererte veiledning MÅ ha det — bekreftet at
`TjenesteVeiledning.tsx`s `Hjemmel: …`-linje til da rendret `juridiskGrunnlag` som ren, sammenslått
tekst (`${kilde} ${eId}`), ikke som lenker, til tross for at samme data allerede rendres som ekte
lenker andre steder (`Egenskapspanel.tsx`, `TjenesteDetalj.tsx`, via `rettskildeLenke()`). Fikset
samme runde: hvert `juridiskGrunnlag`-element i veiledningen er nå en `<Link>` til
`/rettskilder/{id}?eid=…` når eId-en matcher en kjent rettskilde, ellers uendret tekst-fallback.
Verifisert med `npx tsc -b --noEmit` (ikke browser-verifisert i denne runden — se
Optional-Next-Step).

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

**Utvidet av v4**: det nye §3 "Habilitet" er et EGET, beslektet men distinkt gap — det er en
vurdering av **saksbehandleren selv** (fvl. § 8), ikke av søkeren. Det passer ikke i
Vilkår/Regelnode-ontologien i det hele tatt (som alltid evaluerer eligibility for SØKER), og er ikke
en variant av dimensjon E sitt vedtaksvilkår-gap — det er et tredje, foreløpig helt umodellert
konsept ("prosess-forutsetning for selve saksbehandlingen"). Sammenfaller med funn #4 i
`06-veikart.md` ("Saksbehandler-habilitet, fvl. § 6") — nå konkret illustrert med et reelt
eksempel, fortsatt uløst, fortsatt notert til en senere byggesteg.

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

### H. Håndbok-nivå rettskildeomfang (ny dimensjon, 2026-07-31)

Idealet lar en håndbok deklarere, tidlig og samlet, hvilke rettskilder den som HELHET omhandler —
for fasiten: alkoholloven, alkoholforskriften, kommunens alkoholpolitiske retningslinje og
forvaltningsloven (siden Habilitet/Formalia siterer fvl. §§ 8/11/17). Dette er distinkt fra
node-nivå-referanser (dimensjon A/F) — et samlet "denne håndboken handler om X, Y, Z"-svar, ikke en
sum av enkelt-avsnitts-koblinger.

Regel-ide har nå (denne runden) `HandbokRettskildeomfangEntitet` + `/api/handboker/{id}/rettskilder`
(GET/POST/DELETE), en avkrysningsliste ved håndbok-opprettelse (`HandbokOpprett.tsx`) og en
"Denne håndboken omhandler: …"-visning med legg-til/fjern-skjema på håndbok-siden
(`RettskildeDetalj.tsx`). **Gap som gjenstår:** ingen validering av at innholdet FAKTISK kun siterer
de deklarerte rettskildene (en forfatter kan fortsatt koble en Referanser-seksjon til en rettskilde
som ikke er i omfanget, uten varsel) — omfanget er informativt/navigerbart, ikke håndhevet.

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

**Status etter runde 3 (2026-07-31)** — Johanns oppfølging på rundskriv v4: reproduksjonstest,
ikke-manuell rekkefølge, håndbok-nivå rettskildeomfang, og ekte rettskilde-lenker i veiledningen.

| Dimensjon | Skår | Kort begrunnelse |
|---|---|---|
| A — Sporbarhet på tekstnivå | **75 %** *(uendret tall, men reell forbedring)* | `Hjemmel: …`-linjen i `TjenesteVeiledning.tsx` var ren tekst til nå — rendres nå som ekte, klikkbare lenker til rettskilden (samme `rettskildeLenke()`-mekanisme som resten av appen). Ikke hevet til 100 %: fortsatt ingen kontroll av at en "hjemmel"-kommentar faktisk siterer riktig lovtekst. |
| H — Håndbok-nivå rettskildeomfang | **75 %** | Ny dimensjon (se over) — full CRUD + UI (opprettelse og etterhåndsredigering), verifisert i browser. Ikke 100 %: omfanget håndheves ikke (ingen varsel ved koblinger utenfor deklarert omfang). |
| Rekkefølge/nummerering (prinsipp, ikke egen dimensjon) | — | `VilkarstreKommentarEntitet.Rekkefolge` kan nå flyttes via ▲/▼-knapper (`FlyttAsync`, swap med nabo) i stedet for kun append — verifisert i browser at rekkefølgen faktisk endres og består reload. Ingen UI lar noensinne en bruker skrive et tall direkte; prinsippet er også skrevet inn i selve doc-kommentaren på feltet (`Entiteter.cs`), ikke bare her. |

**Reproduksjonstest** (`RundskrivReproduksjonTests.cs`, `RegelIde.Api.Tests`) — svar på "er det mulig
å reprodusere rundskriv v4 via applikasjonen": **delvis, med en presist begrunnet grense**. Testen
seeder ikke ny data (gjenbruker det eksisterende Byggesteg4-treet + KommunaleParametreSeed) og
bekrefter, via det ekte `GET /api/tjenester/{id}/veiledning`-endepunktet (pluss to `POST
/api/vilkarstre-kommentarer`-kall som demonstrerer forfatter-mekanismen for §6/§11), følgende
dekningskart:

| Seksjon | Dekning |
|---|---|
| §2 Saksgang (oversikt) | Delvis (3 av 6 spørsmål strukturert: vandel, kvalifikasjon, kommunalt skjønn) |
| §3 Habilitet | Nei — passer ikke i Vilkår/Regelnode-ontologien (evaluerer saksbehandler, ikke søker) |
| §4 Formalia | Nei — ingen søknad-komplett-vilkår modellert |
| §5 Serveringsbevilling | Nei — ingen egen vilkår-node |
| §6 Vandelsvurdering | Delvis — vilkåret finnes strukturert, avslagsgrunnene krever manuell `VilkarstreKommentar` |
| §7 Kvalifikasjonskrav | Delvis — aldersgrense strukturert, >1000-gjester-terskel og kunnskapsprøve-unntak er ikke |
| §8 Kommunal skjønnsvurdering | Delvis — kun klokkeslett er `DatasettVerdi`, resten av tabellen er ikke |
| §9 Vilkår i vedtaket (Gyldighet/Prikkbelastning) | Nei — Vedtaksvirkning eies av `forklaringsmodell-api` |
| §11 Sjekkliste | Delvis — mekanismen (`ul`/`li`) virker ende-til-ende, konkrete punkter krever manuell kommentar |
| §12 Relevante tjenester | Nei — `Tjeneste` har ikke noe relatert-tjenester-felt |

Testen selv, og tabellen over, er bevisst **uendret** — den måler dekning fra den delte seed-baseline
(`Byggesteg4VilkarstreSeed`/`KommunaleParametreSeed`), ikke innholdet en enkelt bruker har lagt til i
en løpende instans. Runde 4 (under) demonstrerer likevel at "Nei"-radene ikke er en permanent grense
i domenemodellen — bare i seed-dataene.

## Runde 4 (2026-07-31): "Da må du opprette de" — fyller gapet via API-ene

Johanns tilbakemelding på runde 3: dekningen i fasit-dokumentet var lav fordi jeg rapporterte gap
uten å bruke applikasjonens egne API-er til faktisk å **opprette** det som manglet — poenget med
reproduksjonsøvelsen er nettopp å se om håndboken *lar seg bygge*, ikke bare å konstatere at den
ikke gjør det i dag. Johann bekreftet en konkret nedbrytning av rundskriv v4 til lovreferanser,
vilkår og tjenester, og ba om at dette ble bygget direkte mot API-ene (ikke GUI), med GUI som
etterfølgende kontroll.

**Opprettet, alt via ekte HTTP-kall mot de eksisterende endepunktene — ingen ny kode:**

- **Serveringsloven** (`LOV-1997-06-13-55`) importert fra Lovdatas offisielle bulk-arkiv via
  `POST /api/rettskilder/lovdata` — nøyaktig samme mekanisme som alkoholloven/forvaltningsloven ble
  importert med. Reell paragrafstruktur (§ 3 Bevilling, § 5 Etablererprøve, § 6 Krav til vandel),
  ikke en stub.
- **5 nye Vilkår** (`POST /api/vilkar`): Habilitet, Formalia, Serveringsbevillingsvilkår,
  Kunnskapsprøve, Kommunal skjønnsvurdering (skjønnsbasert, med et nytt Begrep som skjønnsgrunnlag)
  — koblet inn som barn av rotnoden (`POST /api/regelnoder/{rootId}/barn`), synlige i både
  Vilkårstre-grafen og den genererte veiledningen.
- **6 ekte tekst-tagger** (`POST /api/rettskilder/{id}/tagger` + `.../koble`) som knytter de nye
  Vilkårene til faktiske lovtekst-ledd (fvl § 8/§11/§17, serveringsloven § 3, alkoholloven §
  1-7a/§1-7c) — samme mekanisme som knyttet de fire opprinnelige Vilkårene til lovteksten (runde
  "byggesteg 4 kryssnavigasjon").
- **12 nye Tjenester** (`POST /api/tjenester`) — hele "Relevante tjenester"-listen fra § 12 i
  rundskrivet (Omsetningsoppgave, Etablererprøven, Kunnskapsprøvene, Kontroller osv.).
- **10 nye `VilkarstreKommentarer`** (`POST /api/vilkarstre-kommentarer`) for innhold som ikke passer
  som et testbart Vilkår (§9s faste vilkår/gyldighet/gebyr/avledet vilkår på rotnoden, §6s
  avslagsgrunn-sjekkliste og 10-årsgrense på Vandelsvilkåret, §8s skjønnsbaserte tilleggsvilkår) —
  demonstrerer at kommentar-mekanismen faktisk BÆRER dette innholdet når en forfatter skriver det inn,
  ikke bare i teorien.
- **Håndboken "Skjenkebevilling – testrunde 3"** (tom etter runde 3s browser-verifisering) fylt med
  **13 kapitler** (§§ 1–13, riktig nummerert — ingen duplikat, i motsetning til kildedokumentets to
  "§ 11"-er) og en kommentarseksjon per kapittel, med ekte lovreferanser koblet på seksjonene der
  rundskrivet har en konkret paragraf. Håndbokens rettskildeomfang (ny funksjon fra runde 3) satt til
  alle fem relevante kilder: alkoholloven, alkoholforskriften, kommunens retningslinjer,
  serveringsloven og forvaltningsloven.

**Verifisert i browser** (ikke bare via API-responser): håndboken viser nå reelt innhold per kapittel
med klikkbare lovreferanser i «Lovreferanser»-seksjonen; vilkårstreet viser de fem nye Vilkårene som
egne noder; veiledningen (`GET /api/tjenester/{id}/veiledning`) render alt sammenhengende — rotnodens
fem nye kommentarer, Vandelsvilkårets sjekkliste og 10-årsgrense-hjemmel, og de fem nye Vilkårene med
ekte, klikkbare hjemmel-lenker (inkludert til det nyimporterte serveringsloven).

**Konklusjon**: svaret på "lar håndboken seg bygge" er **ja** — samtlige "Nei"/"Delvis"-rader i
dekningstabellen over kunne fylles med ekte data via eksisterende API-er, uten kodeendringer. Det som
sto igjen som et reelt gap etter runde 3 (Habilitet, Formalia, Serveringsbevilling, kommunal
skjønnsvurdering som strukturert Vilkår, §12s relaterte tjenester) var altså et **innholdsgap**, ikke
et **modellgap** — domenemodellen tillot det hele tiden, ingen ny entitet eller migrasjon var
nødvendig. Det ene reelle unntaket: §9s Gyldighet/Prikkbelastning/gebyr er fortsatt representert som
fritekst-kommentarer, ikke strukturerte felt — fordi `Vedtaksvirkning` bevisst eies av
`forklaringsmodell-api` (se dimensjon E) — men selv DET la seg bygge, bare ikke som et testbart
Vilkår.

**Ikke gjort denne runden** (bevisst, ikke et overraskende gap): "Testkommunen 2017 Vurdering av
habilitet 2018" (nevnt i rundskriv v4 § 3) ble ikke opprettet som egen rettskilde — Johanns egen
breakdown karakteriserer den som et internt saksdokument/presedens, ikke en rettskilde i egentlig
forstand, og Presedensregisteret (byggesteg 3) er fortsatt ikke bygget.

Testen feiler bevisst hvis et fremtidig gap tettes uten at testen selv oppdateres — den er skrevet
som en levende kontrakt for hvor grensen går, ikke en engangsmåling.

## Skjermbilde-konformitet mot Claude Design

Egen, enklere sammenligningsakse — gjelder UI/visuell verifisering, ikke innhold. Når en browser-
verifisering tar skjermbilder av en ferdig skjerm, sammenlign mot den tilhørende mockupen i
`prototyper/*.dc.html` (der en finnes) og angi en grov %-vurdering (layout/komponentvalg/
informasjonstetthet — ikke pixel-for-pixel) i verifiseringsteksten. Ingen egen fasit-fil nødvendig
her — mockupen ER fasiten. Håndbok-relaterte skjermer har i dag ingen egen mockup i `prototyper/`
(kun `Byggesteg1-Rettskilder.dc.html` og den pre-QA `Regel-IDE.dc.html`) — dette bør bemerkes som
"ingen design-fasit å måle mot" heller enn å hoppes over stilltiende, hvis/når håndbok-skjermer
testes visuelt.
