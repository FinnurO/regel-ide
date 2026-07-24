# Endringslogg v0.3 — flervirksomhet (opptil ~1000 offentlige virksomheter), Ansattporten

Denne runden er utløst av en driftsøkonomisk realitet Johann pekte på under byggesteg 1-kodingen: regel-IDE skal potensielt brukes av **opptil 1000 virksomheter i offentlig sektor**. Det opprinnelige designet (`02-produktkrav.md` v0.3, før denne endringen) sa eksplisitt at applikasjonen var *"et enkeltbruker-arbeidsverktøy for én virksomhet — ikke et flervirksomhets-fellesverktøy"*, med én driftsatt instans (egen database) per virksomhet. I den skalaen ville egne ressurser per virksomhet vært uforholdsmessig kostbart å drifte og vedlikeholde — ikke minst fordi delte, nasjonale rettskilder (alkoholloven, forvaltningsloven osv.) da måtte vedlikeholdes N ganger separat ved hver lovendring, i stedet for én gang.

## Adressert

| Endring | Adressert |
|---|---|
| Databasen må skille virksomheters data fra hverandre | Ny `virksomheter`-tabell + `virksomhet_id` lagt til på virksomhetseide entiteter (`RegelIde.Data`, `Entiteter.cs`/`RegelIdeDbContext.cs`). Nullable på `Rettskilde` (`NULL` = delt/nasjonal kilde, satt = virksomhetens egen), påkrevd på `Tekst-tag` (alltid et virksomhets eget arbeidsprodukt, selv på en delt rettskilde-node), nullable på `Proveniens` (arver status fra entiteten hendelsen gjelder). Se `03-domenemodell.md` §0.1 for den fullstendige regelen per entitetstype. |
| Delte nasjonale rettskilder må fortsatt kun finnes én gang globalt | Den opprinnelige globale unike indeksen på `rettskilder.eli WHERE entitetsstatus='gjeldende'` er delt i to partial-indekser: én global (kun `virksomhet_id IS NULL`) og én per-virksomhet (`virksomhet_id IS NOT NULL`). Bekreftet med tester mot ekte Postgres: to virksomheter kan ha hver sin lokale kilde med samme ELI-form uten kollisjon, mens to delte kilder med samme ELI fortsatt kolliderer som før. |
| `RettskildeImportTjeneste` må vite hvilken virksomhet (om noen) en import gjelder | Ny valgfri `virksomhetId`-parameter (default `null` = delt/nasjonal import — dagens tre fixture-dokumenter (alkoholloven, alkoholforskriften, forvaltningsloven) importeres uendret som delte kilder). Referanse-stubber (§3.1 steg 6 i teknisk design) opprettes alltid som delte (`virksomhet_id=NULL`), siden de representerer ikke-importerte nasjonale rettskilder. |
| Terminologivalg: "organisasjon" vs. "virksomhet" | Brukte **`virksomhet_id`**, ikke "organisasjon_id" som opprinnelig foreslått i samtalen — prosjektets egen dokumentasjon bruker konsekvent "virksomhet", og Schartums prinsipp (sitert i `03-domenemodell.md` §1.3: "ett begrepsuttrykk skal ha ett begrepsinnhold — unngå synonymer") tilsier at vi ikke bør innføre en ny synonym term for samme begrep. |
| Identitetsløsning for innlogging | **Ansattporten**, låst i `05-arkitektur-og-nfk.md` §1 og markert i systemkontekstdiagrammet (§0). |
| Produktkrav måtte oppdateres til å reflektere flervirksomhet | `02-produktkrav.md` kap. 2 skrevet om — fra "enkeltbruker-arbeidsverktøy for én virksomhet" til eksplisitt flervirksomhets-applikasjon med begrunnelse. |
| NFR for samtidig redigering (optimistic concurrency) var spesifisert, men ikke faktisk håndhevet i koden | Oppdaget i samme runde (ikke en del av selve flervirksomhet-refaktoreringen, men rettet her siden det ble avdekket ved gjennomgang): `versjon`-feltet var en vanlig kolonne, ikke konfigurert som EF Core concurrency token. Rettet, og bekreftet med en test mot ekte Postgres at en foreldet skriving nå faktisk avvises (`DbUpdateConcurrencyException`), ikke overskriver stille. Se `05-arkitektur-og-nfk.md` §2. |

## Bevisst avvist (og hvorfor)

- **Rad-nivå sikkerhet (Postgres RLS-policyer) er ikke satt opp ennå.** Databaseskjemaet støtter virksomhetsisolasjon (unike indekser, `virksomhet_id`-kolonner), men selve håndhevingen (at en spørring fysisk ikke KAN returnere en annen virksomhets rader, uavhengig av applikasjonskode) er utsatt til autentisering faktisk finnes — å bygge RLS-policyer mot en "brukerens virksomhet"-kontekst som ennå ikke settes noe sted (ingen innlogging er bygget) ville vært å gjette på et grensesnitt som ikke finnes ennå.

## Ikke bekreftet ennå

- **API-lags tilgangskontroll.** `RegelIde.Api` har ingen autentisering og ingen filtrering på virksomhet ennå — enhver kaller ser i dag alle rader uansett virksomhet. Dette *må* løses før noe reelt flervirksomhets-scenario kan driftsettes, men er ikke gjort i denne runden (den var spesifikt scopet til databaserefaktoreringen + dokumentasjon).
- **Ansattporten-integrasjonen selv** (OIDC-oppsett, hvilket claim gir `virksomhet_id`, tokenvalidering) er ikke bygget — kun besluttet og notert i arkitekturdokumentet.
- **Onboarding av en ny virksomhet** (opprette `virksomheter`-raden, knytte dens første brukere/roller) har ingen spesifisert flyt eller UI ennå.
- **RBAC-håndheving på tvers av virksomheter** (`03-domenemodell.md` §2) er presisert som prinsipp ("en rolle gjelder alltid innenfor egen virksomhet"), men ikke implementert som faktisk tilgangskontroll-kode.
- **Provisjonering/kapasitetsplanlegging for ~1000 virksomheter** (hvor mange rader/hvor stor datamengde realistisk, indekseringsstrategi ut over det som er satt opp for pilotskala) er ikke vurdert i denne runden — dagens NFR-er i `05-arkitektur-og-nfk.md` §2 er fortsatt skrevet for pilotskala (Testkommunen, ett testcase), ikke ~1000 virksomheter.
