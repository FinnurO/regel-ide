# Forslag til justeringer i `forklaringsmodell-api`

*Dette repoet endrer ikke `forklaringsmodell-api` direkte — det er et annet repos ansvar. Dette dokumentet samler konkrete, begrunnede forslag som bør tas inn dit (som issues/PR-er der) for at begrepsbruken skal henge sammen. Ingen av punktene under er kritiske blokkere for `forklaringsmodell-api` slik det står i dag; de blir relevante når regel-IDE begynner å produsere data den skal konsumere (byggesteg 7, `06-veikart.md`).*

## 1. `KL-VILKARSUTFALL` og `UtfallType` er ikke samme liste — og bør ikke være det

`forklaringsmodell-api`s `UtfallType` (`Oppfylt`, `IkkeOppfylt`, `Uaktuelt`, `IkkeVurdert`, `Uavklart`) er **frosne, endelige** utfall skrevet på en `Vurdering`-rad i et vedtak. Regel-IDEs `KL-VILKARSUTFALL` (`03-domenemodell.md` §1.4) har seks verdier, og to av dem (`krever_dokumentasjon`, `ma_vurderes_av_jurist`) er ikke sluttresultater — de er **rutingstatuser i en pågående sak** ("hva må skje videre"), slik kap. 3.11 i produktkrav faktisk bruker dem (vilkårstabell med "handling: vis/vurder/be om dokumentasjon"). Å bruke `KL-VILKARSUTFALL` direkte som `UtfallType` ville blande et levende saksbehandlingsstatus-vokabular inn i et frosset forklaringsvokabular — nøyaktig den typen sammenblanding `digital-rettsstat/docs/06-regellaget.md` advarer mot ("regel betyr tre ulike ting").

**Forslag:** behold begge listene, men dokumenter en eksplisitt mapping som brukes når en sak fryses til vedtak:

| `KL-VILKARSUTFALL` (regel-IDE, levende sak) | `UtfallType` (forklaringsmodell-api, frosset) |
|---|---|
| `oppfylt` | `Oppfylt` |
| `ikke_oppfylt` | `IkkeOppfylt` |
| `ukjent` | `Uavklart` |
| `krever_dokumentasjon` (uløst ved frysing) | `IkkeVurdert` |
| `ma_vurderes_av_jurist` (uløst ved frysing) | `IkkeVurdert` |
| `krever_skjonn` (løst før frysing → et av de fire over) | — (skal ikke forekomme på en frosset `Vurdering`) |

**Konkret forslag til `forklaringsmodell-api`:** legg denne tabellen inn i `docs/api-spesifikasjon-forklaringsmodell.md` som en kommentar til punkt 3.14 (append-only-regelen for `Vurdering.Utfall`), slik at fremtidige klienter (regel-IDE inkludert) ikke gjetter seg til mappingen.

## 2. Sporbarhet fra `Vilkar`/`Regel`-katalograder tilbake til regel-IDE

`forklaringsmodell-api`s `Vilkar`-entitet har allerede feltene `Kode` og `Kodeverk` ("f.eks. `FP_VK_41`, strukturert kode fra kildesystemets kodeverk"). Disse feltene er generelle nok til å dekke behovet uten skjemaendring:

**Forslag (konvensjon, ikke kodeendring):** når regel-IDE eksporterer et publisert vilkår, sett `Vilkar.Kode = <regel-IDE vilkar_id>` (f.eks. `V-101`) og `Vilkar.Kodeverk = "REGEL-IDE"`. Tilsvarende for `Regel`: bruk `Regel.RegeldefinisjonReferanse` (allerede en ekstern URI-peker) til å peke til regel-IDEs eksporterte artefakt-URL for den publiserte versjonen (kap. 3.14/4.3 i produktkrav). Dette gir full sporbarhet uten at noen av skjemaene må endres — kun en dokumentert navnekonvensjon, som bør legges i `forklaringsmodell-api/README.md` sin entitetstabell.

## 3. `RettskildeType` mangler en verdi for virksomhetsinterne dokumenter

Regel-IDEs Rettskildebibliotek (`03-domenemodell.md` §1.1, `doctype: internal`) inkluderer bevisst "virksomhetens egne dokumenter" — men dette er, presist sett iht. `01-referansemodell.md` §3–4, en **regelkilde**, ikke nødvendigvis en **rettskilde** i streng juridisk-metode-forstand. `forklaringsmodell-api`s `RettskildeType`-enum (`Lov, Forskrift, Rundskriv, Forarbeider, Rettspraksis, InternasjonalRett, Forvaltningspraksis`) har ingen ren match for dette — `Forvaltningspraksis` er nærmest, men betegner noe annet (etablert praksis, ikke et internt instruksdokument).

**Forslag:** enten (a) legg til en ny `RettskildeType`-verdi (`VirksomhetsinterntDokument` e.l.) i `forklaringsmodell-api`, eller (b) presiser i regel-IDEs egen dokumentasjon at kun `doctype ∈ {act, doc, judgment}`-poster eksporteres til `forklaringsmodell-api`s `Rettskilde`-tabell, mens `doctype: internal`-poster forblir en regel-IDE-intern referanse (brukt f.eks. som kildegrunnlag for `InternPraksis`-baserte vilkår, jf. `Vilkar.Grunnlagstype` i `forklaringsmodell-api`, som allerede har en `InternPraksis`-verdi *uten* krav om `RettskildeIder`). Alternativ (b) krever ingen endring i `forklaringsmodell-api` og anbefales derfor som førstevalg — men bør skrives ned eksplisitt et sted begge team leser.

## 4. Eksportformat-navn bør verifiseres på tvers før de låses

Kravspesifikasjonen (og dermed `01-referansemodell.md` og `02-produktkrav.md`) bruker "eFLINT" og "RuleML"; `digital-rettsstat/docs/03-kunnskapsgrunnlag.md` og `06-regellaget.md` bruker "FLINT" og "NRML". `forklaringsmodell-api`s `Regel.Teknologi` er fritekst uten enum-begrensning, så det er ikke et skjemaproblem — men det er en driftrisiko: uten en avtalt kanonisk liste vil `Teknologi`-feltet over tid samle inkonsekvente strengverdier for "samme" format. `digital-rettsstat` selv markerer NRML som *"venter på primærkilde — bør verifiseres"*.

**Forslag:** før regel-IDEs eksportmotor (byggesteg 6) bygges, avklar om eFLINT (kjørbar/formell variant) og FLINT (TNO sitt mer generelle rammeverk) faktisk er samme format i denne sammenhengen, og om RuleML og NRML er det. Lås deretter én kanonisk verdi per format og bruk den konsekvent i `Regel.Teknologi`-verdiene som skrives fra regel-IDE.

## 5. Grensesnittet: eksport-fil eller direkte API-kall?

Ikke et avvik, men et åpent spørsmål som må avgjøres før byggesteg 7 (`06-veikart.md`): når regel-IDE publiserer et vilkår, skal det (a) generere en eksportfil som noen registrerer manuelt via `forklaringsmodell-api`s `POST /api/vilkar`/`POST /api/regler`, eller (b) kalle disse endepunktene direkte som del av publiseringstransaksjonen (`03-domenemodell.md` §4)? Alternativ (b) gir sanntids sporbarhet, men kobler de to systemenes driftstilgjengelighet sammen (publisering i regel-IDE ville feile hvis `forklaringsmodell-api` er nede). Anbefaling: start med (a) i byggesteg 7s tynne demo-slice, vurder (b) når/hvis regel-IDE går i reell drift.
