# API-kontrakter

*Ikke en full OpenAPI-spesifikasjon ennå — det kommer når fase 1-entitetene (se `06-veikart.md`) er stabile nok til å låse skjema. Dette er operasjonslisten kravspesifikasjonen manglet, skrevet i samme stil som `forklaringsmodell-api/docs/api-spesifikasjon-forklaringsmodell.md` §5 for gjenkjennelighet på tvers av repoene.*

## 1. Rettskilder

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/rettskilder` | List / opprett rettskilde (import fra Lovdata-søk eller filopplasting, AK-3.3.5) |
| GET | `/api/rettskilder/{id}` | Les rettskilde inkl. AKN-tre |
| PUT | `/api/rettskilder/{id}/metadata` | Oppdater metadata (tittel, ELI, kildetype, ikrafttredelse …) — avvises (409) hvis kilden er `publisert`/`gjeldende` og har referanser fra publiserte vilkår; oppretter ny versjon i så fall |
| POST | `/api/rettskilder/{id}/tagger` | Opprett tekst-tag (AK-3.3.1–3.3.2) |
| DELETE | `/api/rettskilder/{id}/tagger/{tagId}` | Fjern egendefinert tag (AK-3.3.4) — kun tagger uten publiserte referanser kan fjernes |
| GET | `/api/rettskilder/{id}/koblinger` | Begrep/vilkår denne bestemmelsen er koblet til |
| GET | `/api/rettskilder/{id}/historikk` | Proveniens |

## 2. Begreper

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/begreper` | List / opprett begrep |
| GET/PUT | `/api/begreper/{id}` | Les / oppdater — `PUT` på publisert begrep oppretter ny versjon, ikke in-place-endring |
| POST | `/api/begreper/{id}/publiser` | Publiser til data.norge.no (skriver `skosUrl`, trigger `ConceptChanged`) |
| GET | `/api/begreper/{id}/brukt-i` | Vilkår som refererer begrepet |

## 3. Kodelister

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/kodelister` | List / opprett kodeliste |
| GET/PUT | `/api/kodelister/{id}` | Les / oppdater — juridiske kodelister krever jurist-rolle (RBAC, `03-domenemodell.md` §2) |
| POST | `/api/kodelister/{id}/koder` | Legg til kode |
| PUT | `/api/kodelister/{id}/koder/{kode}` | Oppdater kode (setter `gyldig_til`/`erstattes_av` — koder er append-only på samme måte som andre publiserte entiteter) |

## 4. Tjenester

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/tjenester` | List / opprett tjeneste (CPSV-AP-NO) |
| GET/PUT | `/api/tjenester/{id}` | Les / oppdater grunndata |
| POST | `/api/tjenester/{id}/hendelser` | Legg til hendelse (§1.5) |
| POST | `/api/tjenester/{id}/avhengigheter` | Legg til tjenesteavhengighet (intern eller data.norge.no-referanse) |
| POST | `/api/tjenester/{id}/publiser` | Publiser tjeneste — atomisk over alle `validert`-vilkår, se publiseringsmodell `03-domenemodell.md` §4 |

## 5. Datasett

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/datasett` | List / opprett datapunkt |
| GET/PUT | `/api/datasett/{id}` | Les / oppdater |
| GET | `/api/datasett/informasjonsmodell` | Generert JSON Schema (AK-3.6.1) |
| POST | `/api/datasett/informasjonsmodell/publiser` | Publiser informasjonsmodell |

## 6. Presedens

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/presedens` | List / opprett presedens |
| GET/PUT | `/api/presedens/{id}` | Les / oppdater |
| GET | `/api/presedens?vilkarId={id}` | Presedens relevant for et gitt vilkår |

## 7. Vilkår / regeltre

*Tre distinkte ressurser, jf. den låste ontologien i `01-referansemodell.md` §5. Regel-IDEs komposisjonsnode kalles bevisst **regelnode**, ikke "regel" — `forklaringsmodell-api` har allerede en `Regel`-ressurs som betyr noe annet (det eksporterte, operasjonaliserte artefaktet), se `01-referansemodell.md` §5.6 og `07-forklaringsmodell-api-avvik.md`.*

### 7.1 Vilkår (bladnode)

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/vilkar` | List / opprett vilkår |
| GET/PUT | `/api/vilkar/{id}` | Les / oppdater — `PUT` på `publisert`-node avvises (409); bruk `/ny-versjon` |
| POST | `/api/vilkar/{id}/ny-versjon` | Opprett ny versjon av en publisert node |
| POST | `/api/vilkar/{id}/valider` | Sett status `validert` (kun jurist) |
| POST | `/api/vilkar/{id}/publiser` | Publiser (kun jurist) — kjører testcaser først (§4 i domenemodell) |
| POST | `/api/vilkar/{id}/tilbaketrekk` | Sett status `tilbaketrukket` |
| GET | `/api/vilkar/{id}/historikk` | Proveniens |
| GET | `/api/vilkar/{id}/diff?mot={versjon}` | Sammenlign to versjoner (kap. 3.16 i produktkrav — skjermen er spesifisert, ikke algoritmen) |

`POST`/`PUT` avvises (422) med feilkode `VILKAR_HAR_IKKE_BARN` dersom body inneholder et `barn`-felt — INV-1 (`01-referansemodell.md` §5.4) håndheves her, ikke bare i UI.

### 7.2 Regelnoder (komposisjonsnode)

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/regelnoder` | List / opprett regelnode |
| GET/PUT | `/api/regelnoder/{id}` | Les / oppdater — samme 409/`ny-versjon`-mønster som vilkår |
| POST | `/api/regelnoder/{id}/ny-versjon` | Opprett ny versjon |
| POST | `/api/regelnoder/{id}/barn` | Koble til barn (Vilkår- eller Regelnode-id) — validerer DAG på tvers av `barn`- og `unntak`-kanter samlet (avviser sykel, 422, jf. AK-3.4.6) |
| PUT | `/api/regelnoder/{id}/operator` | Endre `barn_operator` (OG/ELLER/IKKE) |
| POST | `/api/regelnoder/{id}/valider` | Sett status `validert` (kun jurist) |
| POST | `/api/regelnoder/{id}/publiser` | Publiser (kun jurist) — krever alle `barn[]` publisert/ikke-påkrevd (§4 i domenemodell) |
| POST | `/api/regelnoder/{id}/tilbaketrekk` | Sett status `tilbaketrukket` |
| GET | `/api/regelnoder/{id}/historikk` | Proveniens |
| GET | `/api/regelnoder/{id}/diff?mot={versjon}` | Sammenlign to versjoner |

### 7.3 Unntak

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/unntak` | List / opprett — body krever `gjelderRegelId` og `betingelse` (422 uten begge, INV-3/INV-4) |
| GET/PUT | `/api/unntak/{id}` | Les / oppdater |
| POST | `/api/unntak/{id}/valider` | Sett status `validert` (kun jurist) |
| POST | `/api/unntak/{id}/publiser` | Publiser — avvises (409) hvis `gjelderRegelId` ikke er `publisert` (§4 i domenemodell) |
| GET | `/api/unntak/{id}/historikk` | Proveniens |

## 8. AI-forslag

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET | `/api/ai-forslag` | List forslag i kø, med konfidens |
| GET | `/api/ai-forslag/{id}` | Les forslagsdetaljer (begrunnelse, kildesitering, presedensgrunnlag) |
| POST | `/api/ai-forslag/{id}/avvis` | Avvis |
| POST | `/api/ai-forslag/{id}/godkjenn` | Godkjenn — oppretter/oppdaterer vilkår med status `validert`, logger `ai_forslag_versjon` + `godkjent_av` (AK-3.10.2) |

Ingen `PUT`/publiseringsendepunkt for AI-forslag — AI kan aldri publisere (RBAC).

## 9. Testcaser

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET/POST | `/api/testcaser` | List / opprett |
| POST | `/api/testcaser/{id}/kjor` | Kjør mot gjeldende (eller angitt versjon av) vilkårstre |
| GET | `/api/testcaser?nodeType=vilkar\|regelnode\|unntak&nodeId={id}` | Testcaser tilknyttet en node — brukes av publiseringssjekken |

## 10. Kunnskapsgraf og påvirkningsanalyse

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET | `/api/graf/node/{type}/{id}` | Node med naboer, filtrerbart per relasjonstype |
| POST | `/api/graf/pavirkningsanalyse` | Body: `{nodeType, nodeId}` → alle nedstrøms berørte noder gruppert per type (AK-3.13.1), + hvilke testcaser bør kjøres |

## 11. Eksport

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET | `/api/regelnoder/{id}/eksport?format=eflint\|dmn\|openfisca\|ruleml` | Generert kode i lesevisning — kun regelnoder har `utdata`/Output-fane, se `02-produktkrav.md` kap. 3.4 |

## 12. Domenehendelser

| Metode | Sti | Beskrivelse |
|---|---|---|
| GET | `/api/hendelser?type=&fra=&til=` | Hendelseslogg (§5 i domenemodell) — grunnlag for dashbord-aktivitetsgraf |

## Generelle regler (gjelder alle ressurser over)

- Ingen `DELETE` på entiteter med `entitetsstatus != utkast` eller som er referert av en `ForklaringsloggOppforing` i `forklaringsmodell-api` (samme append-only-prinsipp som der).
- `PUT` på en `publisert`-entitet skal avvises med 409 og et forslag om å bruke `ny-versjon`-endepunktet i stedet.
- Alle skrive-endepunkt skal validere RBAC-rollen i forespørselens kontekst (`03-domenemodell.md` §2) og returnere 403 ved rollebrudd — ikke bare skjule handlingen i UI.
