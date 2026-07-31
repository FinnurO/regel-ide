# Deploy i Altinns app-cluster (tt02)

Regel-IDE deployes som om den var en Altinn-app, men den er det ikke. Den kjører i stedet vårt
eget image, plassert inn i Altinns deploy-maskineri via app-repoet
`ttd/finnuro-poc-regel-editor` på altinn.studio.

## Hvorfor det ser ut som en Altinn-app

Altinn Studios CI/CD gjør bare to ting med app-repoet:

1. bygger `Dockerfile` i repoets rot
2. legger `deployment/values.yaml` på toppen av sitt eget Helm-chart

Det finnes ingen mulighet for sidecars, egne Kubernetes-manifester eller vilkårlige
`env`-blokker. Derfor blir app-repoets `Dockerfile` en tynn innpakning som bare gjør `FROM` på
vårt publiserte image og setter miljøvariablene — alt som normalt hadde ligget i en
Deployment-spec.

Det er samme mønster som `ttd/olebhansen-poc-custom-app1` bruker.

## De tre tingene som må stemme

### 1. Porten må være 5005

Chartet setter både `Service` og probene mot **5005**, som er Kestrel-standarden for Altinn-apper.
Vårt image lytter på 8080. App-repoets `Dockerfile` overstyrer derfor med
`ASPNETCORE_URLS=http://+:5005`.

`deployment.service.internalPort` finnes i values.yaml, men vi bruker den ikke: den dokumenterer
bare `Service`-porten, og det er uklart om probene følger med. Å flytte appen til den porten
chartet allerede forventer er ett sted å ta feil, ikke to.

### 2. Probe-stien er `/health`, ikke `/helse`

Chartet har hardkodet `/health`, og stien er ikke konfigurerbar i values.yaml.

Dette er verdt å forstå, for feilen er stille: uten et `/health`-endepunkt ville forespørselen
truffet SPA-fallbacken og fått **200 text/html** — altså ville probene rapportert «klar» også med
en død database. Appen ville sett frisk ut mens den var nede.

`Program.cs` mapper derfor `/helse` og `/health` til samme handler, dekket av
`HelsesjekkTests` som sjekker at svaret er JSON og ikke html.

### 3. Sti-prefikset må settes

Appen serveres på `/{org}/{app}/`, og ingressen stripper **ikke** prefikset. `RegelIde:Stiprefiks`
gjør to ting: `UsePathBase` fjerner prefikset før ruting, og `<base href>` i index.html settes til
det, slik at klientens relative asset- og API-URL-er peker riktig.

Prefikset kan ikke bakes inn i SPA-bygget — da ville imaget vært låst til én sti. Derfor er
Vite bygget med `base: './'` og prefikset settes ved kjøretid.

To feller er verdt å kjenne, begge oppdaget ved å teste og ikke ved å lese koden:

**`UsePathBase` alene er ikke nok.** `WebApplication` setter inn sitt eget `UseRouting` først i
pipelinen når man ikke kaller det selv, og da har rutingen allerede matchet på full sti.
Symptomet er lumsk: endepunktene svarer fortsatt på rot, mens alt under prefikset faller til
SPA-fallbacken og gir 200 med `text/html` — altså «alt svarer», men ingenting er riktig.
`Program.cs` kaller derfor `UseRouting()` eksplisitt rett etter `UsePathBase`.

**`<base href>` må skrives om ved kjøretid, ikke bare på forsiden.** Derfor serveres index.html
av `MapFallback` med omskrevet innhold, ikke av `UseDefaultFiles`/`MapFallbackToFile` som ville
sendt fila rått fra disk. Uten dette ville forsiden fungert mens en reload på
`/{org}/{app}/vilkarstre` løst `assets/...` mot `.../vilkarstre/` og gitt 404 — det man ikke
oppdager ved bare å laste forsiden.

Merk at appen fortsatt svarer på rot-stier i tillegg. Ingressen sender bare prefiksede
forespørsler, så det er ufarlig, og Docker-`HEALTHCHECK` bruker det.

### 4. Autentiseringsprofilen må settes eksplisitt

Imaget har `RegelIde__Autentisering=testbruker` som standard, slik at det er kjørbart lokalt.
**Deployet må sette `altinn`**, ellers står appen åpen med brukervelger. Også dette feiler stille —
appen virker, den spør bare ikke om hvem du er.

## Miljøspesifikke verdier

Tenor-brukere og syntetisk organisasjon hører til miljøet, ikke til Regel-IDE-koden, og settes som
`ENV` i app-repoets `Dockerfile`. Se [`autentisering.md`](autentisering.md) for hva de er og
hvorfor `urn:altinn:userid` — ikke party-id — er det som skal inn.

## Oppdatere til en ny versjon

1. bygg og push nytt image fra `master` i dette repoet
2. oppdater `FROM`-taggen i app-repoets `Dockerfile`
3. deploy app-repoet fra Altinn Studio

Bruk versjonstagg eller digest, aldri `latest` — `latest` flytter seg, og da endrer et redeploy
seg uten at noe i app-repoet ser annerledes ut.

## Databasen er efemer

SQLite-filen ligger i containerens filsystem. **Alt innhold forsvinner ved omstart** og bygges opp
igjen fra Lovdata-kildene og seedene. `replicaCount` må være 1 — to poder ville hatt hver sin
database og svart ulikt på samme spørsmål.
