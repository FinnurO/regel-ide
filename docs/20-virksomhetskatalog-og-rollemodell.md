# 20. Virksomhetskatalog og rollemodell

Teknisk plan for implementasjon, basert på Johanns kravspesifikasjon (2026-08-22) og hans svar på
åpne spørsmål samme dag. Erstatter `docs/arkiv/19-virksomhet-begrep-og-rettskildekobling-SUPERSEDED.md`
i sin helhet — den runden ble forkastet (null commits, skjema traff ikke de faktiske kravene), men to
lærdommer derfra tas videre inn hit: navngitt seeding fremfor bred harvest (§4), og fare for å blande
to uavhengige akser i ett felt (relevant for §2.2 under, som denne gangen er et BEVISST, instruert valg
om å slå dem sammen — ikke en feil å unngå).

## 0. Johanns bindende valg for denne runden

Svar på tre spørsmål jeg stilte etter gjennomgang av kravspesifikasjonen:

1. **Utvid eksisterende `Virksomhet`-entitet, ikke en ny tabell.** "En virksomhet trenger ikke å ha
   brukere eller en aktiv tenant i systemet" — bekrefter at katalograder uten tenant-tilknytning er en
   forutsett, normal tilstand, ikke et spesialtilfelle.
2. **Slå sammen `Forvaltningsniva` og `Organisasjonstype` til ett felt.** Verdisett: stat, kommune,
   fylkeskommune, statsforvalter, osv. — den FINE-grained taksonomien (det `docs/17` kalte
   `Organisasjonstype`), ikke den grove tre-verdis (`stat`\|`fylke`\|`kommune`). Dette opphever
   eksplisitt `docs/17` §3s `[LÅST]`-vedtak om å holde de to aksene separate — et bevisst, instruert
   omvalg, ikke en feil.
3. **RBAC via attribusjon, ikke per-virksomhet skrivesperre.** Skrivehandlinger på de nye delte
   tabellene (katalogen, myndighetstildeling, kandidatkøen) tilskrives brukerens EGEN virksomhet
   (samme `OpprettetAv`/Proveniens-mønster som kodelister) — ingen `WHERE virksomhet_id = @bruker`-filter
   på skriving. Åpen skriving, sporet attribusjon.

## 1. Formål

Uendret fra kravspesifikasjonen: en katalog over virksomheter (identifisert ved organisasjonsnummer),
kobling til begrep brukt om dem i rettskildetekst, håndtering av **rollebegrep** som tildeles konkrete
virksomheter gjennom forskrift/delegeringsvedtak (ofte ulikt fordelt per paragraf), en arbeidsflyt for
å godkjenne kandidatkoblinger, og aggregerte oversikter beregnet fra koblingene — ikke lagret som egen
fakta.

**Avgrenset bort:** en generisk aktør-modell (fysiske personer, "arbeidsgiver"/"innbygger"-roller,
internasjonale organisasjoner). Strengt virksomheter identifisert ved org.nummer denne runden.

## 2. Datamodell

### 2.1 `Virksomhet` (utvidet, ikke ny)

Eksisterende entitet (`RegelIde.Data/Entiteter.cs:9`) — allerede referert fra `Bruker`, `Rettskilde`,
`Tjeneste`, `Begrep`, `Vilkar`, `Kodeliste` og mer via `VirksomhetId`. `Guid Id` som PK beholdes
UENDRET — ingen migrasjon av eksisterende referanser. `Organisasjonsnummer` har allerede en unik
indeks (`ux_virksomheter_organisasjonsnummer`, tillater NULL for virksomheter uten org.nummer, som
`Testkommunen`) — dekker identitetsbehovet i kravspekens §2.1 uten en PK-endring.

**Nye felt:**

| Felt | Type | Kommentar |
|---|---|---|
| `OrganisasjonsformKode` | `string?` | Fra Brreg, ren referanseinformasjon (`KOMM`, `FYLK`, `ORGL`, `STAT`, …) |
| `Sektorkode` | `string?` | Fra Brreg (institusjonell sektorkode, SSB) |
| `OverordnetEnhetId` | `Guid?`, FK → `Virksomhet` | Fra Brreg, for hierarki. Nullbar, selvrefererende |
| `SistBrregSynkronisert` | `DateOnly?` | Tidspunkt for siste berikelses-oppslag mot Brreg |

`Hjemmeside` dekkes av `VirksomhetNettside` (§2.2) med `Type = Hovedside`, ikke et eget felt her —
unngår duplisert kilde til samme fakta.

**Endret felt:**

| Felt | Før | Nå |
|---|---|---|
| `Forvaltningsniva` | `string?`, verdisett `stat`\|`fylke`\|`kommune` (grov akse, `[LÅST]` i `docs/17` §3) | `string?`, verdisett `stat`\|`kommune`\|`fylkeskommune`\|`statsforvalter`\|`tingrett`\|`lagmannsrett`\|`jordskifterett` (fin akse). Feltnavnet beholdes — kun verdisettet utvides/erstattes, ingen kolonneomdøping |

**Blast radius, verifisert (ikke antatt) før dette skrives i kode:** ingen PRODUKSJONSLOGIKK grener på
verdien av `Forvaltningsniva` i dag — kun to steder leser den i det hele tatt: seed-mappingen i
`OrganisasjonsregisterSeed.cs:104` (`FYLK` → `"fylke"`, `KOMM` → `"kommune"`) og fem test-assertions
(`OrganisasjonsregisterSeedTests.cs`, `BergenKorpusSeedTests.cs`). Migrasjonen er derfor:
1. Skjemamigrasjon: ingen kolonneendring nødvendig (feltet er allerede fri streng, ingen DB-CHECK).
2. Datamigrasjon: `UPDATE virksomheter SET forvaltningsniva = 'fylkeskommune' WHERE forvaltningsniva = 'fylke'`
   (14 rader i dag, alle fylkeskommuner).
3. Kodeendring: `OrganisasjonsregisterSeed.cs:104` sin mapping utvides til å dekke ALLE `orgForm`-verdier
   i kildefilen (se §4), ikke bare `KOMM`/`FYLK`.
4. Testendring: `OrganisasjonsregisterSeedTests.cs:73`s assertion `"fylke"` → `"fylkeskommune"`.

### 2.2 `VirksomhetNettside`

| Felt | Type | Kommentar |
|---|---|---|
| `Id` | PK | |
| `VirksomhetId` | FK → `Virksomhet` | |
| `Url` | `string` | |
| `Type` | `string` | `Hovedside` \| `Ovrig` |
| `Merknad` | `string?` | |

Én `Hovedside`-rad auto-seedes fra Brregs `hjemmeside`-felt ved berikelse (§4.1); øvrige legges til
manuelt. Separat fra de eksisterende `NettsideSti`/`NettsideLenke`-tabellene (ulik hensikt — se
kravspek §2.2, uendret fra opprinnelig forslag).

### 2.3 `Begrep` — kategori `Virksomhet`

Gjenbruker eksisterende `Begrep`-tabell. Nytt felt `Begrepskategori` (`string`, verdisett `Virksomhet`\|
`Rolle` — se §2.4) styrer hvilken av de to formene en rad representerer.

| Felt (for `Begrepskategori = Virksomhet`) | Type | Kommentar |
|---|---|---|
| `Streng` | `string` | Navneformen brukt i rettskildetekst, f.eks. "Mattilsynet", "Statsforvalter", "Fylkesmann" |
| `VirksomhetId` | FK → `Virksomhet` | Direkte, entydig referanse |

Synonymi (f.eks. "Fylkesmann"/"Statsforvalter" etter 2021-omdøpingen) løses med flere `Begrep`-rader
mot samme `VirksomhetId` — ingen egen mekanisme.

### 2.4 `Begrep` — kategori `Rolle`

| Felt (for `Begrepskategori = Rolle`) | Type | Kommentar |
|---|---|---|
| `Streng` | `string` | Rollenavnet, f.eks. "forurensningsmyndighet" |
| `LovkildeId` | FK → `Rettskilde` | Sammen med `Streng`: **rollebegrepets identitet**, ikke bare metadata |

**Unik constraint**: `(Streng, LovkildeId, Begrepskategori)` — samme streng i to ulike lover er to
ulike rader (forurensningslovens "tilsynsmyndighet" ≠ plan- og bygningslovens). Uten denne constraint-en
kan duplikate rollebegrep for samme lov opprettes ved en inntastingsfeil, uten at noe fanger det.

### 2.5 `Myndighetstildeling`

| Felt | Type | Kommentar |
|---|---|---|
| `Id` | PK | |
| `RolleBegrepId` | FK → `Begrep` (kategori=`Rolle`) | Loven følger implisitt av begrepets `LovkildeId` |
| `VirksomhetId` | FK → `Virksomhet` | Hvem rollen er tildelt |
| `HjemmelRettskildeId` | FK → `Rettskilde` | Forskriften/delegeringsvedtaket som gjør tildelingen |
| `Paragrafspenn` | se §7.1 | Paragrafer i loven denne tildelingen dekker |
| `Vilkaar` | `string?` | Saksområde-/vilkårsavgrensning, f.eks. "kommunale avløpsanlegg" |

**Ingen `GyldigFra`/`GyldigTil` på denne tabellen** — gyldighet arves fra `HjemmelRettskildeId`.
**Verifisert, ikke antatt**: `Rettskilde` har allerede `Status` (`Gjeldende`\|`Opphevet`\|`Utkast`) og
`GyldigFra`/`GyldigTil` (`Entiteter.cs:89,93-94`) — forutsetningen i kravspekens §6 er ALLEREDE oppfylt,
ingen forarbeid nødvendig der. Når hjemmelen markeres opphevet, faller tilhørende
`Myndighetstildeling`-rader ut av "gjeldende rett"-visningen automatisk via et join mot `Rettskilde`.

**Flere innganger, én tabell**: både inline-tagging fra lovtekst-visningen og et samlet skjema for en
delegeringsforskrift som tildeler roller for flere paragrafer i én operasjon skriver til samme tabell.

### 2.6 `VirksomhetKandidat`

Arbeidskø for godkjenning av forekomster funnet ved tekstsøk.

| Felt | Type | Kommentar |
|---|---|---|
| `Id` | PK | |
| `VirksomhetId` | FK → `Virksomhet` | Kandidat-virksomheten (funnet via `Begrep`, kategori=`Virksomhet`) |
| `RettskildeId` | FK → `Rettskilde` | Rettskilden treffet er funnet i |
| `NodeEid` | `string` | Presis node-referanse (samme eId-mønster som resten av rettskilde-modellen — IKKE en løs paragrafreferanse-streng, se §7.1s resonnement om strukturert vs. fritekst) |
| `Status` | `string` | `Venter` \| `Godkjent` \| `Avvist` |

**Bevisst avvik fra husstilen**: ingen `Entitetsstatus`/`Versjon`/`OpprettetAv`-fullversjonering som
resten av rettskildeinnholdet — dette er en arbeidskø, ikke autoritativt rettskildeinnhold. `Avvist`
beholdes for å hindre gjenoppdukking ved neste sveip, men kan hardslettes manuelt.

**Statusregler**: kun `Venter` vises i køen og foreslås på nytt ved ny sveip. `Godkjent` → oppretter
den faktiske forekomst-taggingen (samme mekanisme som §2.3/§2.4s begrep-i-tekst-kobling).

### 2.6.1 Sveip og godkjenning — bygget (kandidatsøk-og-godkjenning-runden)

Bygget i en senere runde enn resten av dette dokumentet (§2.6 over beskrev fortsatt bare KØEN, ikke
sveipet, da den ble skrevet). Faktisk implementasjon:

**Ekstra felt på `VirksomhetKandidatEntitet`**: `StartOffset`/`EndOffset` (int) — presist tegn-intervall
i nodens `Tekst` på sveip-tidspunktet. Lagt til fordi punkt 5 (godkjenning → tagg) krever et eksakt
intervall, og fordi ETT sveip kan gi FLERE treff i samme node (f.eks. samme navneform nevnt to ganger i
samme ledd). Konsekvens: den unike indeksen er utvidet fra `(VirksomhetId, RettskildeId, NodeEid)` til
`(VirksomhetId, RettskildeId, NodeEid, StartOffset)` — to ulike treff i samme node er nå to uavhengige
kandidatrader, som kan godkjennes/avvises hver for seg. Migrasjon:
`20260822034839_LeggTilTegnintervallPaVirksomhetKandidat`.

**Sveipefunksjonen** (`VirksomhetKandidatSveipTjeneste`, egen klasse fra selve køen): for én virksomhet,
henter ALLE dens navneform-`Begrep`-rader (`Begrepskategori="virksomhet"`, gruppert på
`VirksomhetReferanseId` — ikke bare `Virksomhet.Navn`) og matcher hver navneform med ordgrense-regex
(`\bnavn\b`, case-sensitivt, lengste navneform først i alternasjonen) mot `Tekst` på alle
ikke-opphevede, gjeldende rettskilde-noder. Hvert treff sendes til
`VirksomhetKandidatTjeneste.OpprettEllerFinnAsync` (idempotent per den utvidede nøkkelen over).

**Godkjenning → ekte tagg** (`VirksomhetKandidatTjeneste.GodkjennAsync`): re-kjører matchingen mot nodens
DÅVÆRENDE tekst ved godkjenningstidspunktet i stedet for å lagre quoteSelector-en (prefiks/eksakt/
suffiks) på kandidaten — henter nodens `Tekst` på nytt, slår opp tegn-utdraget i det lagrede intervallet,
og krever at det EKSAKT matcher en fortsatt-gjeldende navneform-`Begrep`-rad for virksomheten. Matcher
det ikke (node reimportert/endret siden sveipet, eller navneformen fjernet) → `ArgumentException`, ingen
tagg opprettes og kandidatens status forblir `Venter`. Samme "ingen gjettet fallback"-vern som
`TekstTaggTjeneste.OpprettAsync` allerede har for staleness. Valgt fremfor å utvide kandidat-raden med
egne quoteSelector-felt — unngår duplisert lagring av noe som allerede kan avledes fra noden.

Taggen som opprettes: `kind="begrep"`, `RefId` = navneform-`Begrep`-radens id (ALDRI en egen
`"virksomhet"`-kind som peker direkte på `Virksomhet` — forsøkt og reversert i en tidligere runde, se
klassekommentaren på `VirksomhetKandidatTjeneste.GodkjennAsync`, fordi det bypasser navneform-laget som
finnes nettopp for synonymer). `TekstTaggEntitet.VirksomhetId` settes til virksomheten TEKSTEN OMTALER
(kandidatens `VirksomhetId`), ikke til den godkjennende brukerens egen virksomhet — et bevisst valg som
gjør `TekstTaggTjeneste.ListerForAsync(rettskildeId, virksomhetId)` til riktig oppslag for §3s "fra
virksomhet til rettskilde"-visning, konsistent med at kandidatkøen selv er delt/global (§0 pkt. 3), ikke
tenant-scopet.

**API**: `app.MapGroup("/api/virksomhet-kandidater")` — `GET /` (filtrerbar på virksomhet/rettskilde/
status, status utelatt = kun `Venter`, `status=Alle` = ingen statusfilter), `POST /sveip`, `POST /`
(manuell), `POST /{id}/godkjenn`, `POST /{id}/avvis`, `POST /godkjenn-batch`/`POST /avvis-batch`
(massehandling med per-rad-feilhåndtering), `DELETE /{id}` (hardslett, kun `Avvist`).

**UI**: `VirksomhetKandidaterListe.tsx` (egen rute `/virksomhet-kandidater`, sorterbar/filtrerbar tabell
+ avkrysningsbokser + massegodkjenn/avvis + sveip-trigger) og en "Kjør sveip"-knapp + lenke til fullisten
fra `VirksomhetDetalj.tsx`.

## 3. Aggregerte visninger (beregnet, ikke lagret)

Uendret fra kravspekens §3 — beregnes ved lesing, aldri lagret som egen fakta:

- **Fra rettskilde til virksomhet**: unionen av (a) direkte taggede `Begrep`-forekomster i teksten, og
  (b) virksomheter dekket av en `Myndighetstildeling` hvis `Paragrafspenn` treffer en forekomst av
  rollebegrepet — as-of gjeldende dato, gyldighet arvet fra hjemmelen.
- **Fra virksomhet til rettskilde**: lover virksomheten forekommer i (direkte eller via rolletildeling),
  antall treff per lov, gjenstående `Venter`-kandidater for virksomheten.

## 4. Seed- og berikelsesstrategi

**Ikke en ny bred BRREG-høsting.** `src/RegelIde.Data/Seed/organisasjoner-norge.json` (Johanns eksport,
2026-08-14, 451 rader) har allerede alt som trengs for en solid startkatalog:

| `orgForm` | Antall | Status i dag |
|---|---|---|
| `KOMM` | 357 | Seedet (Forvaltningsniva=`kommune`) |
| `FYLK` | 14 | Seedet (Forvaltningsniva=`fylke` → migreres til `fylkeskommune`, §2.1) |
| `STAT`/`ORGL`/`SF`/`AS`/`STI`/`FLI`/`ANNA`/`SÆR` | 80 | **Aldri seedet** — hoppet bevisst over i forrige runde ("kun kommuner/fylkeskommuner denne runden") |

De 80 useedede radene inneholder alle 10 statsforvalterembeter (både bokmåls- og nynorskform —
"Statsforvalteren i Innlandet" og "Statsforvaltaren i Vestland" begge til stede, ingen ny søk etter
nynorskformen nødvendig), Mattilsynet, Digitaliseringsdirektoratet, Skatteetaten, Statens vegvesen,
Forsvaret, Politidirektoratet, Norges Høyesterett, helseforetak (HF) m.fl. — reelle, verifiserte
organisasjonsnumre, ikke noe som må hentes på nytt.

**[LÅST — 2026-08-22] Ingen filtrering på seed.** Vurdert og eksplisitt avvist av Johann: "du kan ikke
vurdere hva som er reelle virksomheter [som] gjør offentlige oppgaver bare basert på navnet." Alle 80
radene seedes uendret — inkludert de som ved første augnekast ser ut som fagforeningslokaler eller
stiftelser (`NTL Brønnøysundregistrene`, `Kartverket Bedriftsidrettslag`, `Fond for utøvende kunstnere`,
osv.). Ingen navnemønster-/orgForm-heuristikk for å luke ut rader — det var mitt forslag, ikke en
instruks, og det er nettopp den typen navnebasert gjetning som er avvist her.

**[LÅST — 2026-08-22] `Forvaltningsniva`-tildeling ved seeding: start blankt, med ett unntak.**
`KOMM` → `kommune` og `FYLK` → `fylkeskommune` beholdes automatisk (uendret oppførsel, entydig
`orgForm`-mapping — "kommune[n] er helt klart definert med KOMM", og `FYLK` er samme kategori
presisjon). **Alle andre `orgForm`-verdier** (`STAT`, `ORGL`, `SF`, `AS`, `STI`, `FLI`, `ANNA`, `SÆR`)
seedes med `Forvaltningsniva = NULL` — INGEN forsøk på å gjette `statsforvalter` fra navnemønster
eller annet, selv der det ville vært et opplagt riktig gjett (statsforvalterne). Johann fyller inn
manuelt selv. Dette er den samme "ikke gjett fra navn"-linjen som filtreringen over, konsekvent
anvendt også på selve klassifiseringen, ikke bare på om raden skal finnes.

**Berikelse** (§4.1 i kravspeken, uendret): en funksjon som slår opp et gitt org.nummer mot Brreg og
fyller `OrganisasjonsformKode`/`Sektorkode`/`OverordnetEnhetId`/`VirksomhetNettside(Hovedside)` —
kjøres per organisasjonsnummer, ikke som bulk, og overskriver ALDRI `Forvaltningsniva` (manuelt
vedlikeholdt felt, se kravspekens §2.1-begrunnelse — statsforvalter-eksempelet der viser nettopp hvorfor
`ORGL` alene ikke er nok).

## 5. Arbeidsflyter

Uendret fra kravspekens §4 (import/berikelse, kandidatsøk/-godkjenning, rolletildeling) — ingen
endringer i logikken, bare i hvilken tabell dataene lander i (§2 over).

## 6. UI-krav

Uendret fra kravspekens §5 (virksomhetsvisning, kandidatliste, lovtekst-tagging) — samme krav, samme
avgrensning (rollebegrep-resolusjon vises i direkte tilknytning til paragrafen, ikke et separat panel;
"ingen tildeling" vises eksplisitt, ikke tomt felt).

## 7. Beslutninger — avklart [LÅST 2026-08-22]

### 7.1 `Paragrafspenn`-format — strukturert, bruker gjeldende eId-referanser

Bekreftet: strukturert fra dag 1, ikke fritekst. Struktur: en liste av `{ FraEid: string, TilEid: string? }`-par
(samme "start/slutt"-par-mønster som andre spenn i modellen), der `TilEid = null` betyr et enkeltstående
punkt. Matches mot faktiske paragraf-/ledd-noder via eksisterende node-eId-oppslag — ingen ny
infrastruktur, gjenbruker det samme presisjonsnivået som `TjenesteRegelverksreferanse.TilEid`/
`RettskildeReferanse` allerede har.

### 7.2 Default-forslag for `Forvaltningsniva` — start blankt, unntatt kommune/fylkeskommune

Bekreftet: ingen automatisk forslag basert på sektorkode ved berikelse. Feltet starter `NULL` for alt
utenom `KOMM`/`FYLK` (som allerede er entydig, se §4) — Johann fyller inn resten manuelt. Samme
prinsipp som §4s "ingen filtrering/gjetning fra navn", nå også anvendt på klassifiseringsfeltet.

## 8. Forutsetninger — status

- ~~`RettskildeEntitet` må ha gyldighetsperiode som førsteklasses felt~~ — **allerede oppfylt**, se §2.5.
- Endringsrammeverket kan allerede markere en forskrift som opphevet (`Status = 'Opphevet'`) — ingen
  nytt arbeid.
- Sveiparkitekturen må kunne kjøres på nytt uten å gjenskape allerede behandlede kandidater —
  `VirksomhetKandidat.Status`-filteret (§2.6) løser dette, samme mønster som kravspeken selv beskriver.

## 9. Utenfor scope denne runden

Uendret fra den forkastede rundens §8, fortsatt gyldig avgrensning: full bakoverfylling av forekomster
i alle importerte rettskilder, automatisk (regel- eller KI-basert) gjenkjenning av virksomhetsnavn i
løpetekst, gyldighetsperioder per synonym-term, relokering av forekomster ved reimport, master-tjeneste-
delen av `docs/17`, og en dedikert redigeringsflate utover det §6 spesifiserer.
