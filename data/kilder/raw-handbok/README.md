# Rådata — testfixture for `HandbokTekstParser` (byggesteg-1-utvidelse, Trinn 1)

Ikke Lovdatas eksportformat (se `../raw-lovdata/README.md`) — dette er tekstlaget fra et **hentet
PDF-dokument** (kommunal retningslinje), rådata for den nye, ikke-Lovdata-spesifikke
segmenteringsparseren beskrevet i `../../docs/15-handbok-dokumentgraf-notat.md` §2/§8 (Trinn 1).

## Fil og proveniens

| Fil | Kilde | Hentet | Metode |
|---|---|---|---|
| `bergen-retningslinjer-SD-24-113.txt` | Bergen kommune, *Retningslinjer for tildeling av salgs- og skjenkebevillinger i Bergen kommune for perioden 2024–2028* (Dok.nr. SD-24-113, Rev.nr. 01, fastsatt av Bystyret 19.06.2024, gyldig 01.07.2024–01.07.2028). Offentlig, `https://www.bergen.kommune.no/api/rest/filer/V51903878` | 2026-08-12 | **Ekte dokument, ikke syntetisk.** Hentet via WebFetch, PDF-tekstlaget lest side for side (5 sider) via Claude Codes innebygde PDF-lesing. Sidene er konkatenert i lesevolgen med sidebrytnings-støylinjene (`Dok.nr.: SD-24-113 Side N av 5`) bevart i teksten, nettopp fordi disse skal filtreres bort AV parseren som testes — å fjerne dem her ville gjort filtreringslogikken utestet. |
| `bergen-forskrift-salgs-skjenke-apningstider.txt` | Bergen kommune, *Forskrift om salgs-, skjenke- og åpningstider i Bergen kommune for perioden 2024–2028* (Dok.nr. SD-24-114, Rev.nr. 01, fastsatt av Bystyret 19.06.2024, gyldig 01.07.2024–01.07.2028 — SAMME dokumentbunt/bystyrevedtak som retningslinjene over, men et eget dokumentnummer). Offentlig, `https://www.bergen.kommune.no/api/rest/filer/V51903879` | 2026-08-13 | **Ekte dokument, ikke syntetisk.** Hentet via WebFetch, PDF-tekstlaget lest side for side (2 sider) via Claude Codes innebygde PDF-lesing — samme metode som retningslinjene. Sidebrytnings-støylinjen (`Dok.nr.: SD-24-114 Side 2 av 2`) er bevart i teksten av samme grunn som over. |

Teksten er PDF-tekstlagets naturlige linjeoppdeling (ikke reflowet/redigert), inkludert der en
overskrift (f.eks. «3.2») står alene på slutten av en side og selve brødteksten fortsetter først
etter neste sides støylinje — en reell paginering-splitter-node-på-tvers-av-side-situasjon, bevisst
IKKE rettet opp manuelt, fordi det er nøyaktig den typen støy en ekte PDF-tekstutvinning (PdfPig)
ville produsert.

Ingen endringer i sak-/personinnhold — dokumentene inneholder ingen navngitte saksbehandlere eller
direktenumre.

## Forskriften — en ANNEN dokumentstruktur enn retningslinjene (reelt funn, se Del A i oppgaven)

Forskriften bruker IKKE retningslinjenes `Kapittel N`/`N.N`-mønster. Toppnivå-seksjonene er bare
`1. SALGSTID FOR ...`, `2. SKJENKETID`, `3. ÅPNINGSTID` — ETT tallsegment fulgt av punktum, mellomrom
og STORE BOKSTAVER, ingen literal `"Kapittel"`. Underpunktene («2.1 Skjenkestart», «2.1.1 Skjenketiden
...») følger derimot nøyaktig samme `N.N`/`N.N.N`-mønster som retningslinjenes punkter.

Konkret konsekvens av dette funnet, FØR fiksen beskrevet under: `KapittelMønster` (krever literalen
"Kapittel") matcher ALDRI disse toppnivå-linjene, og `PunktMønster` (krever minst ett `.`-segment)
matcher dem heller ikke (ett enkelt tall+punktum uten videre siffer). Siden `PunktMønster` derimot
FAKTISK matcher underpunktene (`2.1`, `2.1.1` osv.), blir `SegmenterPaNummerering`s nodeantall > 0,
og overskrifts-fallbacken (§2 Lag 2) trigges DERFOR ALDRI — i motsetning til det oppgaven antok som
det ene alternativet («faller parseren tilbake til fallbacken»). Det som i stedet skjer er verre:
toppnivå-teksten («1. SALGSTID ...» og hele dens brødtekst, samt selve «2. SKJENKETID»-linjen) blir
stille FORKASTET (ingen åpen node når den treffes), og «3. ÅPNINGSTID» + dens brødtekst blir i stedet
feilaktig SMELTET SAMMEN med den foregående punkt-noden (2.3 Fortæringstid), fordi ingen av
regexene fanger opp linjen og den derfor behandles som løpetekst på den sist åpne noden.

**Dette er rettet** i `HandbokTekstParser.cs` (ny `TallpunktumSeksjonMønster`, behandlet identisk
med et Kapittel-nivå — samme eId-form `kap{N}` — se kodekommentaren der) fordi dette er nøyaktig
"parseren i dag ikke fanger et reelt mønster i ekte data"-tilfellet oppgaven ga eksplisitt lov til å
fikse, IKKE en "gjett en fallback"-situasjon. Uten fiksen ville teksten fra §1 og §3 gått tapt/blitt
feilkoblet stille — det ville brutt §0.1s "ingen informasjon skal forkastes stille"-prinsipp langt
mer alvorlig enn en ren fallback ville gjort. Se `BergenForskriftParserTests.cs` for regresjonstesten
og sluttrapporten for full begrunnelse.

**Et annet reelt funn**: forskriftens brødtekst (de to sidene som faktisk ble hentet) inneholder
INGEN explisitte "jf. alkoholloven §..."-hjemmelssitater i løpetekst (i motsetning til retningslinjene,
som har flere). Forskriften er selv en hjemlet norm (kunngjort ved bystyrevedtak i medhold av
alkoholloven), ikke en tekst som SITERER loven inni sin egen brødtekst — `hjemlet_i`-uttrekket gir
derfor 0 treff for dette dokumentet, korrekt og forventet, ikke en parserfeil.
