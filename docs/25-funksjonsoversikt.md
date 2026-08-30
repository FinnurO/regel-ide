# 25. Funksjonsoversikt

Dette dokumentet er en **brukerrettet oversikt** over hva Regel-IDE faktisk kan gjøre i dag — for
noen som allerede jobber i systemet og trenger et overblikk over funksjonsområdene, ikke en
utviklerintroduksjon.

**Dette er IKKE**:
- en arkitekturbeskrivelse — se `docs/03-domenemodell.md`/`docs/05-arkitektur-og-nfk.md`,
- en backlog/plandokument — se `docs/13-backlog.md`,
- en endringslogg — se `docs/00-endringslogg-*.md`.

Organisert etter byggestein-inndelingen fra `docs/06-veikart.md` der den passer naturlig, siden det
er samme inndeling appens egen sidemeny og utviklingsrekkefølge følger.

---

## Byggestein 1 — Rettskildebibliotek

### Rettskilder (oversikt og detalj)

Katalog over alle åpne rettskilder — delte/nasjonale kilder (lover og forskrifter fra Lovdata) og
virksomheters egne, lokale kilder (forskrifter, rundskriv/håndbøker, brukerveiledninger/nettsider).
Kladder vises aldri i listen. Filtrerbar/sorterbar på tittel, kildetype og eier.

Detaljsiden er den store arbeidsflaten for én rettskilde: metadata (kortnavn, utgiver, vedtaksdato
osv. — «Fra Lovdata»-felt er skrivebeskyttet, «Lokalt forvaltet»-felt er redigerbare), en trevisning
av kapittel/paragraf/ledd-strukturen med fritekstsøk, tekst-tagging av utdrag (kobles til begrep,
tjeneste, vilkår eller regelnode — inkludert en snarvei «Opprett vilkår fra dette utdraget»),
kryssreferanser til andre rettskilder/paragrafer, og en knapp for å vise den underliggende AKN-XML-en.
For håndbøker/rundskriv kan man i tillegg opprette nye kapitler og redigere kommentarseksjoner direkte
i treet.

*Hvor:* «Rettskilder» i sidemenyen (`/rettskilder`, `/rettskilder/:id`).

### Importer rettskilder

To måter å hente inn nye rettskilder: søk i en lokal, automatisk fornyet katalog over Lovdatas
bulk-datasett (henter og konverterer direkte ved valg — gir alltid en delt/nasjonal kilde), eller last
opp en fil i Lovdatas «XML-kompatible HTML»-format (kan merkes som virksomhetens egen lokale
forskrift). Etter import vises en bekreftelsesside der kildeteksten vises side om side med den tolkede
strukturen, før man lagrer.

*Hvor:* «Importer rettskilder» (`/importer`).

*Kjent begrensning:* nettsidens HTML-format for lokale forskrifter (lovdata.no/dokument/LF/…)
støttes ikke ennå.

---

## Byggestein 2 — Tjenester, begrep og kodelister

### Begreper (register og KI-forslagskø)

Begrepsregister (SKOS-basert: term, definisjon, lovreferanse, begrepstype fakta-/handlingsbegrep),
med full status-pipeline (utkast → validert → publisert → arkivert). Kan opprettes manuelt, eller
foreslås av en KI-agent som sveiper valgte rettskilder («Identifiser begrep») — alle forslag havner i
en godkjenningskø (Avvis/Rediger/Godkjenn), ingenting blir gjeldende automatisk. Begrepsdetaljen viser
også «Brukt i rettskilder» — et ekte reverse-oppslag (ordgrense-avgrenset tekstsøk i importert
lovtekst etter begrepets Term, maks 50 treff), atskilt fra den manuelt satte lovreferansen.

*Hvor:* «Begreper» (`/begreper`, `/begreper/:id`), «KI-forslag begrep» (`/begreper/forslag`).

*Kjent begrensning:* KI-klienten er i dag en stub som returnerer ett fast eksempelforslag — ikke ekte
språkmodell-resonnering ennå.

### Kodelister

Verdiregister for kodelister (juridisk, teknisk, eller ekstern-referanse til en autoritativ ekstern
kilde — sistnevnte får ingen virksomhetseier og intet publiseringssteg). Legg til/fjern koder,
sett status.

*Hvor:* «Kodelister» (`/kodelister`, `/kodelister/:id`).

### Datasett

Feltdefinisjoner brukt som input til Vilkår, med verdiregistrering per felt: én rad er alltid
«Standardverdi» (nasjonal standard), øvrige er kommunale/virksomhetsspesifikke verdier med
kildehenvisning.

*Hvor:* «Datasett» (`/datasett`, `/datasett/:id`).

*Kjent begrensning:* listen er kun seedet i dag — ingen UI for å opprette et helt nytt datasett/felt.

---

## Byggestein 4 — Vilkårstre

Grafeditor for regelstrukturen (Vilkår/Regelnode/Unntak) bak én tjeneste, med to visningsmodus
(graf/tre) og et egenskapspanel for valgt node. Opprett vilkår eller regelnode, koble et barn til en
foreldre-regelnode (med klientside sykel-sjekk), opprett unntak. Viser også «løse noder» — opprettet,
men ikke koblet inn i noe tjenestes tre ennå.

*Hvor:* «Vilkårstre» (`/vilkarstre`, `/vilkarstre/:tjenesteId`).

*Kjent begrensning:* runde 2 (testmodul + full publiseringsmodell) er ikke startet — kun grafredigering
er bygget.

---

## Byggestein 5 — Tjenester, handlinger og KI-forslag

### Tjenester (liste og detalj)

Oversikt over alle tjenester (rettigheter) virksomheten forvalter. Detaljsiden er fanebasert:
Oversikt, Vilkårstre, Innhold (ni faste seksjoner: grunnleggende, frister, innsender/tilgang, vedlegg,
opplysninger, veiledning, innsending, kontakt, «hva rettigheten innebærer» — pluss frie
egendefinerte felt), Status, Regelverksreferanser, Hendelser, Handlinger, Avhengigheter. Fanerekkefølge
og synlighet er tilpassbar per bruker. Full JSON-modelleksport kan vises direkte i UI-et.

*Hvor:* «Tjenester» (`/tjenester`, `/tjenester/:id`).

### Veiledning

Viser vilkårstreet til en tjeneste som en lineær, lesbar fortelling i beslutningsrekkefølge (i stedet
for en teknisk graf) — hjemmel, skjønnsmomenter, parameterverdier (nasjonal standard eller valgt
virksomhets lokale verdi), veiledningskommentarer og unntak.

*Hvor:* lenke fra en tjenestes Vilkårstre-fane.

### Identifiser tjenester (KI-forslagskø)

Bygg opp et «kunnskapsbibliotek» (lenker/PDF/Word) og velg rettskilder, kjør et KI-forslag som
genererer tjeneste-utkast (evt. sammen med handlinger i ett kall). Forslag godkjennes/avvises/rediger­es
i en kø, akkurat som for begreper. En egen seksjon, **«Mine forslag til andre virksomheter»**, viser
tjenester DENNE virksomheten selv har foreslått til en annen virksomhet (typisk via
import-wizarden) og som fortsatt står ubehandlet der — med en «Slett»-knapp for å angre, f.eks. etter
en test-import.

*Hvor:* «KI-forslag tjenester» (`/tjenester/forslag`).

### Handlinger (liste og detalj)

Egen toppnivå-side som lister alle handlinger på tvers av alle tjenester. Detaljsiden lar deg
redigere navn/type/bruksområde/status, flytte handlingen til en annen tjeneste, koble en egen
vilkårstre-rotnode, og administrere kanaler, vedlegg, veiledningstekster, bortfallsårsaker, kostnad,
behandlingstid og resultat — hver med egen hjemmel der relevant.

*Hvor:* «Handlinger» (`/handlinger`, `/handlinger/:id`).

*Kjent begrensning:* en handlings regelverksreferanser er kun lesbare i dag — ingen UI for å koble
til/fjerne dem manuelt (kun automatisk satt av Oppgaveregister-seeden).

### Importer modelleksport-JSON (import-wizard)

Menneske-styrt import av en hel rettighetsmodell fra JSON (samme format som en tjenestes egen
JSON-eksport). Last opp fil eller lim inn, se en liste over gjenkjente rettigheter, og for hver: velg
mål-virksomhet (forhåndsgjettet, aldri auto-valgt), søk opp og koble til en allerede eksisterende
tjeneste i stedet for å opprette duplikat, koble regelverksreferanser til ekte rettskilde-paragrafer.
Støtter bulk-import/bulk-angring med fremdriftsindikator, og en in-memory graf-forhåndsvisning før
noe lagres. Avhengigheter mellom de importerte rettighetene (inkl. eksterne referanser) opprettes i et
eget steg etterpå. Velges en annen virksomhet enn din egen, lander rettigheten som forslag i
mottakerens forslagskø.

*Hvor:* «Importer rettighetsmodell» (`/importer/rettighetsmodell`).

### Tjenestereise (graf)

Velg en «sentrum»-tjeneste og se hvordan den henger sammen med andre tjenester (og valgfritt deres
handlinger) via en avhengighetsgraf — justerbar dybde (1–5 hopp), filtrerbar på livshendelse, noder
kan dras rundt. Krysser virksomhetsgrenser (ingen eierskapsfilter) og kan vise eksterne
plassholder-referanser til ikke-onboardede virksomheter.

*Hvor:* «Tjenestereise (graf)» (`/tjenestereise`).

### Håndbøker

Opprett en ny håndbok/rundskriv (virksomhetens egen forvaltningspraksis, forfattet direkte i
verktøyet) — gi tittel, velg hvilke rettskilder den omhandler. Selve kapittel-/kommentarredigeringen
skjer på rettskilde-detaljsiden (se byggestein 1).

*Hvor:* «Håndbøker» (`/handboker/ny`).

---

## Virksomhetskatalog og rollemodell (`docs/20`)

### Virksomheter

Katalog over virksomheter identifisert ved organisasjonsnummer — en virksomhet trenger ikke ha
brukere/tenant i systemet for å stå her (over 450 seedet fra Brreg). To måter å tette et hull i
katalogen: søk mot Brønnøysundregisterets Enhetsregister og opprett direkte (fyller
organisasjonsform/sektorkode automatisk), eller opprett en virksomhet med KUN navn (for aktører uten
egen Brreg-registrering, f.eks. Kystvakten som del av Forsvaret — kan knyttes til en overordnet
enhet).

*Hvor:* «Virksomheter» (`/virksomheter`, `/virksomheter/:id`).

### Virksomhetskandidater

Godkjenningskø for tekstsøk-treff: sveiper alle rettskilde-noder etter FOREKOMSTER av en virksomhets
allerede kjente navneformer (f.eks. finn flere steder «Statsforvalteren» nevnes, når virksomheten og
navneformen allerede er registrert). Filtrerbar, med massegodkjenning/-avvisning.

*Hvor:* «Virksomhetskandidater» (`/virksomhet-kandidater`).

### Navnekandidater

Komplementær oppdagelseskø: i stedet for å bekrefte forekomster av KJENTE navn, leter denne etter
HELT NYE, ukjente egennavn/juridiske aktører i rettskildeteksten via regex-mønstre (aldri KI) —
suffiksmønstre («-tilsynet», «-direktoratet» osv.) og en fast liste juridiske aktør-substantiv
(«Kongen», «departementet» osv.). Godkjenning av en «rolle»-kandidat oppretter et ekte rollebegrep
direkte; en «virksomhet»-kandidat krever et menneske til å koble den til en faktisk virksomhet (via
Brreg-søket eller «opprett med bare navn» over).

*Hvor:* «Navnekandidater» (`/navnekandidater`).

### Rollebegrep og myndighetstildeling

Et rollebegrep (f.eks. «forurensningsmyndighet») har identitet som (navn, lov) — samme rollestreng i
to ulike lover er to ulike begrep. En myndighetstildeling kobler ett rollebegrep til en konkret
virksomhet, hjemlet i en forskrift og avgrenset til et paragrafspenn. Gyldighet arves fra hjemmelens
egen status — ingen egne datoer.

*Hvor:* read-only tabell på en virksomhets detaljside. **Ingen frontend-skjema for å opprette disse
ennå** — kun via API/Swagger i dag (`docs/13-backlog.md` §8).

---

## Brukerhåndtering

Opprett testbrukere (navn + rolle: Fagansvarlig/Jurist/Systemforvalter/Saksbehandler + virksomhet),
rediger rolle/virksomhet for eksisterende brukere. En «identitetsbrikke» nederst i sidemenyen viser
gjeldende bruker og lar deg bytte testbruker (rent klientvalg — ikke ekte autentisering; under ekte
Altinn-innlogging vises kun brikken, ingen bytte-meny).

*Hvor:* «Brukere» (`/brukere`), identitetsbrikken i sidemenyen.

---

## Eksternt høstelag (rå datainnsamling)

Seks kilder høstes inn i en felles, rå lagringstabell (ikke koblet til domenemodellen ennå — bevisst,
venter på en avklaring av Rettighet/Samhandling-arkitekturen):

| Kilde | Innhold | Domenekobling |
|---|---|---|
| Oppgaveregisteret | ~900 skjemaer fra Brreg | **Ja** — eneste kilde som faktisk seeder `Handling`-rader |
| Altinn ressursregister | ~820 AltinnApp-ressurser | Nei |
| Altinn skjemaoversikt | ~800+ tjenestesider (HTML-krypet) | Nei |
| Statsforvalter-tjenester | Fil-basert, egen skraping | Nei |
| Fylkeskommune-dialogtjenester | Fil-basert, samme importør | Nei |
| kommune.no-tjenester | ~15 000 tjenester, 327 kommuner | Nei |

Alle seks trigges kun via `POST /api/eksterne-kilder/...`-endepunkt (ingen automatisk
bakgrunnsoppdatering). **Ingen av dem har en egen frontend-side** — resultatet er kun synlig indirekte,
via Oppgaveregister-koblingen på en handlings detaljside.

---

## Ikke startet / bevisst utenfor MVP

Per `docs/06-veikart.md`: Presedensregister (byggestein 3), full saksbehandling/forklaringslogg
(byggestein 7), Kunnskapsgraf/påvirkningsanalyse (byggestein 8) og Dashboard (byggestein 9) — de to
siste bevisst utenfor MVP, siden de er «strukturelt umulige å bevise noe med før byggestein 1–7 har
reelt innhold» (se også `docs/27-innsikt-sporsmal-vurdering.md` for en konkret vurdering av hva som
faktisk kan bygges som små rapporter allerede nå, uten å bygge et fullt dashboard).
