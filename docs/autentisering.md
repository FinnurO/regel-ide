# Autentisering

Hvor "hvem er dette" kommer fra styres av **én** konfigurasjonsnøkkel, etter samme mønster som
databaseprofilen i `docker/README.md`:

| `RegelIde:Autentisering` | Kilde til identitet | Brukes til |
|---|---|---|
| `testbruker` *(standard)* | `X-Bruker-Id`-header + brukervelger i GUI-et | Lokal utvikling, tester |
| `altinn` | `AltinnStudioRuntime`-cookien, validert mot Altinn-plattformens JWKS | Deploy i Altinns app-cluster (tt02) |

Ukjent verdi feiler ved oppstart i stedet for å falle tilbake til noe.

`testbruker` er **ikke** autentisering — hvem som helst kan sende hvilken som helst bruker-id.
Det er grunnen til at profilen må settes bevisst ved deploy.

## Hvorfor Altinn-cookien og ikke en egen ID-porten-klient

Skall-appen (`ttd/finnuro-poc-regel-editor`) gjør ikke ID-porten selv. Den er en uendret Altinn
Studio-malapp: innloggingen skjer i Altinn-plattformen, og appen validerer bare runtime-cookien
mot plattformens JWKS (`OpenIdWellKnownEndpoint` i dens `appsettings.json`). Vi gjør det samme.

En egen ID-porten-klient ville krevd registrering i Samarbeidsportalen, client_id og secret, og
redirect-URIer — og gitt oss mindre, ikke mer:

> **ID-porten-tokenet inneholder ingen organisasjonstilknytning.** Det gir `pid`
> (fødselsnummer) og ingenting om virksomhet eller rolle.

DAGL kan altså ikke leses ut av et ID-porten-token uansett. Rollen ligger i Altinns rolleregister.
Egen klient ville betydd full registreringsjobb og *deretter* nøyaktig samme oppslag mot Altinn.
Det er bare riktig vei hvis appen må nås utenfra `altinn.no`.

Forutsetningen for cookie-varianten er at vi kjører på et subdomene av `altinn.no` — cookien er
scopet dit, og følger ikke med noe annet sted.

## Rollemapping

| Altinn | Regel-IDE |
|---|---|
| DAGL (daglig leder) | Jurist |
| alt annet | Saksbehandler |

Saksbehandler er den minst privilegerte rollen i RBAC-matrisen (`03-domenemodell.md` §2). Det er
med vilje: et rolleoppslag som ikke er konfigurert, eller som feiler, gir **minst** tilgang.

### Hvor DAGL kommer fra — og hva som er midlertidig

`IAltinnRolleoppslag` er egen abstraksjon fordi kilden til svaret er det eneste som skiller PoC
fra ferdig løsning. PoC-en bruker `KonfigurertRolleoppslag`, som leser en liste med identifikatorer
fra `RegelIde:Altinn:DaglIdentifikatorer`.

Grunnen til at den ikke spør Altinns rolle-API: det API-et krever en abonnementsnøkkel
(`Ocp-Apim-Subscription-Key`) og et plattform-token som Altinn-apper får montert inn som secret —
se `accesstoken`-volumet i skall-appens `values.yaml`. Vi er ikke en Altinn-app og har ikke de
secretene. Så lenge PoC-en bare skal vise at rollen styrer tilgangen, gir konfigurasjonslista
samme observerbare oppførsel til en brøkdel av koblingen.

Bytte til ekte oppslag = én ny implementasjon av `IAltinnRolleoppslag`.

## Brukerrader

En innlogget bruker får en rad i `brukere` ved første innlogging, nøklet på `altinn_bruker_id`
(claim `urn:altinn:userid`). Kolonnen er unik der den er satt; de seedede testbrukerne har NULL
og lever side om side med ekte brukere.

**Kjent begrensning:** rollen settes ved provisjonering og leses ikke på nytt. Endrer du
`DaglIdentifikatorer` etter at noen har logget inn, beholder de rollen de fikk. Det er dekket av en
test som dokumenterer oppførselen. Skal dette endres, må rolleoppslaget kalles per forespørsel.

Alle innloggede brukere havner i virksomheten `RegelIde:Altinn:Virksomhet` (standard
`Testkommunen`), som får `Organisasjonsnummer` fra konfigurasjonen første gang det er tomt — den
seedede raden representerer da Tenor-organisasjonen, og innloggede brukere ser samme innhold som
testbrukerne.

I PoC-en representerer én organisasjon én kommune. Vi slår ikke opp organisasjonsnummer via
register-API-et, fordi det ville krevd abonnementsnøkkel for å gi oss noe vi ikke bruker.

## Testdata (Tenor)

**Ingen Tenor-verdier ligger i repoet.** De er miljøspesifikke og flyktige — syntetiske brukere
og organisasjoner byttes ut, og de betyr ingenting i et annet miljø. En egen test feiler hvis
noen legger dem i `appsettings.json`.

De hører hjemme ett av to steder:

**Lokalt:** kopier `src/RegelIde.Api/appsettings.Local.example.json` til `appsettings.Local.json`
i samme mappe og fyll inn. Filen er gitignorert.

**I drift:** som miljøvariabler i deployment, sammen med resten av den miljøspesifikke
konfigurasjonen for det aktuelle clusteret:

```yaml
env:
  - name: RegelIde__Autentisering
    value: altinn
  - name: RegelIde__Altinn__Organisasjonsnummer
    value: "<organisasjonsnummer>"
  - name: RegelIde__Altinn__DaglIdentifikatorer__0
    value: "<urn:altinn:userid for daglig leder>"
```

Rekkefølgen er `appsettings.json` < `appsettings.Local.json` < miljøvariabler < kommandolinje, så
deploy overstyrer alltid en fil som måtte ligge igjen i imaget.

### Hva du trenger fra Tenor

To syntetiske personer knyttet til samme syntetiske organisasjon: én registrert som **daglig
leder**, og én med en annen rolle (for eksempel styremedlem). Rollen må faktisk finnes i
test-Enhetsregisteret — den kan ikke defineres av oss.

Bare daglig leders identifikator legges i `DaglIdentifikatorer`. Den andre skal *ikke* stå der;
det er nettopp fraværet som gir Saksbehandler, og det er hele testen.

### Party-id er ikke userid

`DaglIdentifikatorer` skal inneholde **`urn:altinn:userid`**. Tenor oppgir også *party-id* for de
samme personene — det er et annet nummer, og ikke det som skal inn her. Party-id identifiserer en
enhet i registeret, userid en innlogget bruker.

`urn:altinn:partyid` sammenlignes derfor **ikke**, og det er med vilje: den claimen peker på
avgiveren som er valgt, ikke på personen. Representerer brukeren organisasjonen, står
organisasjonens party-id der — og hadde vi matchet på den, ville alle som representerer
organisasjonen fått DAGL. Dette er den mest sannsynlige feilkonfigurasjonen, siden Tenor viser de
to numrene side om side, og den er dekket av en egen test.

Oppslaget sammenligner mot `urn:altinn:userid` og `urn:altinn:ssn`, slik at konfigurasjonen
virker uansett hvilket av de to som er lagt inn.

### Hvis noe ikke stemmer i tt02

Claim-settet er ikke verifisert mot et ekte token ennå. Sett `RegelIde:Altinn:VisClaims=true`,
logg inn, og hent `/api/meg/claims` — da ser du nøyaktig hva tokenet inneholder, inkludert
brukerens userid. Endepunktet viser kun innsenderens egne claims, finnes bare under
Altinn-profilen, og er av som standard.

## Konfigurasjon

| Nøkkel | Standard |
|---|---|
| `RegelIde:Autentisering` | `testbruker` |
| `RegelIde:Altinn:Plattform` | `https://platform.tt02.altinn.no` |
| `RegelIde:Altinn:Cookienavn` | `AltinnStudioRuntime` |
| `RegelIde:Altinn:Virksomhet` | `Testkommunen` |
| `RegelIde:Altinn:Organisasjonsnummer` | tom (settes per miljø) |
| `RegelIde:Altinn:DaglIdentifikatorer` | tom (settes per miljø) |
| `RegelIde:Altinn:VisClaims` | `false` |

## Klienten

GUI-et spør `/api/oppsett` om profilen. Under `testbruker` vises brukervelgeren som før; under
`altinn` hentes brukeren fra `/api/meg` og velgeren erstattes av ren tekst. `/api/brukere` svarer
404 under Altinn-profilen — å liste brukere der ville invitert til å velge en annen enn den man er
logget inn som.

## Gjenstår

**CSRF.** Dagens `X-Bruker-Id`-header gir utilsiktet beskyttelse, fordi en custom header tvinger
preflight. Med cookie-basert innlogging forsvinner den, og antiforgery må legges inn bevisst før
Altinn-profilen tas i bruk med ekte data.

**Autorisasjon.** Rollen er fortsatt bare attribusjon — den nekter ingenting ennå. Virksomhets-
sjekkene (IDOR) fra sikkerhetsgjennomgangen står også igjen, og er uavhengige av hvor identiteten
kommer fra.
