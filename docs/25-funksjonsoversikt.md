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
Kladder vises aldri i listen. Filtrerbar/sorterbar på tittel, kildetype, ansvarlig departement og
eier. [ENDRET, fler-verdi-departement, 2026-09-04] Ansvarlig departement er et EKTE fler-verdi-felt —
Lovdata har rettskilder med flere ansvarlige departementer (delt ansvar), lagret som en liste, ikke en
kommaseparert streng, slik at filtrering/gruppering matcher presist på ett enkelt departement. Den
flate lista har ingen egen departement-nedtrekksliste (fjernet — hierarki-fanen er navigasjons-
mekanismen på departement-nivå), men beholder sortering på departement (alfabetisk laveste av en rads
eventuelt flere departementer). En tredje fane («Departement → lov → forskrift», ved siden av den
flate listen) grupperer lover under HVERT sitt ansvarlige departement (en lov med delt ansvar vises
under alle sine departementer) og viser hvilke forskrifter som er hjemlet i hver lov — samme hjemmel-
relasjon som allerede vises på detaljsiden, kun i en aggregert, klikkbar trevisning.

Detaljsiden er den store arbeidsflaten for én rettskilde: metadata (kortnavn, utgiver, vedtaksdato
osv. — «Fra Lovdata»-felt er skrivebeskyttet, «Lokalt forvaltet»-felt er redigerbare), en trevisning
av kapittel/paragraf/ledd-strukturen med fritekstsøk, tekst-tagging av utdrag (kobles til begrep,
tjeneste, vilkår eller regelnode — inkludert en snarvei «Opprett vilkår fra dette utdraget»),
kryssreferanser til andre rettskilder/paragrafer, og en knapp for å vise den underliggende AKN-XML-en.
For håndbøker/rundskriv kan man i tillegg opprette nye kapitler og redigere kommentarseksjoner direkte
i treet.

*Hvor:* «Rettskilder» i sidemenyen (`/rettskilder`, `/rettskilder/:id`).

[NYTT, 2026-10-01] `GET /api/rettskilder/{id}/statistikk` gir presist telte nøkkeltall for én
rettskilde — antall distinkte virksomheter koblet (direkte navneform-tagg ELLER myndighetstildeling),
antall distinkte begrep tagget, og antall tjenester koblet (direkte `TjenesteRegelverksreferanse`
ELLER via en `Handling` sin regelverksreferanse, siden Oppgaveregisteret kobler på Handling-nivå, ikke
Tjeneste-nivå) — brukt av `nettside/tools/eksporter-data.ps1` for den offentlige nettsideeksporten
(ELI-basert oppslag, ikke hardkodet rettskilde-id).

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
til/fjerne dem manuelt (kun automatisk satt av Oppgaveregister-seeden) — se de to KI-forslagskøene
under for den ene unntatte skrivbare veien som faktisk finnes.

### Regelverksreferanser — KI-forslag for tjenester og handlinger (KI-forslagskø) [NYTT, 2026-10-01]

To separate køer foreslår koblinger til rettskilde-paragrafer der de mangler i dag, etter samme
godkjenn/avvis-mønster som «Identifiser begrep»/«Identifiser tjenester»:

- **Tjenester uten regelverksreferanser** — embedding-innsnevrede kandidatparagrafer + KI-vurdering
  per tjeneste, for eksisterende, gjeldende tjenester som i dag har null koblinger. Godkjenning
  oppretter en ekte `TjenesteRegelverksreferanse`; forslagsraden selv er bare arbeidskøen, aldri
  selve koblingen. *Hvor:* «KI-forslag regelverksreferanser (tjenester)»
  (`/tjenester/regelverksreferanse-forslag`).
- **Handlinger matchet på dokumentnivå, uten bekreftet paragrafnivå** — dekker de ~900
  Oppgaveregister-importerte skjemaene spesifikt (den ekte bulk-importen til `Handling`, i motsetning
  til tjeneste-katalogen): deterministisk regex+bekreftelse-kandidatinnsnevring (ikke embedding),
  siden kildens egen henvisningsfritekst allerede nevner paragrafnumrene. Kjøres kun for rader der
  rettskilden selv ER matchet, men fritekst-henvisningen var for kompleks til å tolkes automatisk ved
  import. Godkjenning OPPGRADERER den eksisterende dokumentnivå-raden til paragrafnivå — oppretter
  aldri en søsterrad. *Hvor:* «KI-forslag regelverksreferanser (handlinger)»
  (`/tjenester/handlinger/regelverksreferanse-forslag`).

Begge viser KI-ens begrunnelse per forslag (hvorfor akkurat denne paragrafen), samme
«ingen ugjennomsiktig score alene»-prinsipp som resten av KI-forslagskøene.

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

Begge opprettelsesveiene slår automatisk opp navnet mot Store norske leksikon (synkront, del av
samme kall) og oppretter en bekreftet navneform ved treff — ingen gjettet/algoritmisk fallback hvis
SNL ikke bekrefter, virksomheten opprettes uansett, bare uten navneform. En bekreftet navneform vises
med en «SNL ↗»-lenke til selve artikkelen (i opprettelsesbekreftelsen for «kun navn»-panelet, og i
navneform-tabellen på virksomhetens detaljside) — saksbehandler kan alltid åpne og selv verifisere
SNL-teksten (issue #194, samme mekanisme som Brreg-opprettelsen fikk i #158).

[NYTT, navneformgrunn, 2026-09-07] Hver navneform kan bære en **grunn** til at den peker på nettopp
denne virksomheten: «gjeldende navn» (det offisielle navnet, normaltilfellet), «utgått navn» (et
historisk navn som fortsatt STÅR i lovteksten, f.eks. «Arkivverket», nå Nasjonalarkivet),
«kortform» (kontekstavhengig kortform — «Suldal» betyr Suldal kommune her), eller «feilskriving»
(skrivefeil i kildeteksten, f.eks. «Matilsynet» med bare én t). Grunnen er **valgfri**: alle
navneformer som fantes før denne runden står som uspesifisert, og ingen verdi blir gjettet for dem.
I navneform-tabellen vises grunnen som en farget merkelapp med tre tydelig ulike roller — grønn for
«gjeldende», oransje for «utgått», rød for «feilskriving» — nettopp fordi et utgått eller feilskrevet
navn aldri skal kunne forveksles med det offisielle. Uspesifisert grunn gir ingen merkelapp (tom
celle), for ikke å fylle tabellen med støy.

Merk skillet: grunnen forklarer en LEGITIM streng som faktisk står i lovteksten. Er derimot selve
treffet et regex-artefakt («Ø Suldal kommune», der Ø er limt inn fra en koordinat rett foran), er det
teksten som skal rettes — se «Navnekandidater» under.

[NYTT, navneform-kjede, 2026-09-08] **Navneformen er mellomleddet mellom lovteksten og
virksomheten — og kjeden er nå synlig hele veien.** En `virksomhet`-tagg i lovteksten peker på
NAVNEFORMEN, ikke direkte på virksomheten. Det er navneformen som bærer navneformgrunnen, så det er
den som kan forklare HVORFOR nettopp denne strengen betyr denne virksomheten — og ved synonymer
(«Fylkesmann»/«Statsforvalter») er det bare navneformen som forteller hvilket navn teksten faktisk
brukte. I tagg-listen under lovteksten vises derfor hele kjeden i stedet for bare endepunktet:

> «Karasjok» → [Kortform] → «Karasjok kommune»

Samme kjede ligger i tooltipet på selve markeringen i løpeteksten. Lenken går til virksomheten (det
er dit saksbehandleren skal), mens merkelappen i midten gjør det synlig AT «Karasjok» bare er en
kortform og ikke virksomhetens offisielle navn.

[ENDRET, tagg-synlig, 2026-09-08] **Siste ledd er den gjeldende NAVNEFORMEN, ikke registernavnet.**
Kjeden endte tidligere i virksomhetens tospråklige registernavn («Karasjoga gielda / Karasjok
kommune»), altså i det Enhetsregisteret kaller virksomheten, og ikke i en navneform. Nå slås den
`gjeldende` navneformen for samme virksomhet opp og brukes som hovedledd — begge navneformene peker
på samme virksomhet, og mellomleddet finnes derfor uten noen navneform→navneform-kobling i
datamodellen (et bevisst valg: ingen migrasjon, ingen ny syklusrisiko). Registernavnet er ikke
borte, men degradert til hover: det står i `title` på lenken («Registernavn: …»), fordi det er en
sann og nyttig opplysning som bare ikke skal være HOVEDleddet. Finnes ingen gjeldende navneform,
faller hovedleddet tilbake til registernavnet — det som faktisk finnes; ingenting utledes av
kortformen.

[FIKSET, tagg-synlig, 2026-09-08] **Teksten er faktisk markert når siden åpnes.** Aktivt tagg-lag
ble forhåndsvalgt til det første konfigurerte laget («Begrep») uten å se på nodens innhold. Åpnet
man forskrift 2005-06-17-657 § 1 ledd-1, som kun har `virksomhet`-tagger, listet tagg-tabellen 14
rader mens teksten sto helt umarkert — det ser ut som taggingen ikke virker. Aktivt lag defaulter nå
til et lag som FAKTISK har tagger på noden som vises (begge veier: en node med bare
`begrep`-tagger åpner i Begrep-laget), mens brukerens eget lagvalg alltid overstyrer defaulten og
blir stående. Samtidig er tagg-listen gjort koherent med markeringen: den lister fortsatt alle lag
— å skjule at noden har arbeid i et annet lag ville vært en dårligere feil — men rader utenfor
aktivt lag er dempet og forklarer seg selv, og et klikk på en rad aktiverer radens eget lag og
ruller markeringen inn i synsfeltet.

[NYTT, navneform-kjede, 2026-09-08] **«Where used» på virksomhetens detaljside.** Navneform-tabellen
har fått en «Brukt i»-kolonne som viser hvor navneformen faktisk er tagget i en rettskildetekst, med
lenke til NØYAKTIG paragraf/ledd (ikke bare til dokumentet — en navneform kan være tagget i flere
paragrafer). Er navneformen ikke tagget noe sted, står det uttrykkelig «Ikke tagget i noen
rettskildetekst»; mens data lastes vises en spinner, aldri en påstand om at koblingen mangler.
Myndighetstildelings-tabellen har samtidig fått en **Gruppe**-kolonne: ingressen lovet «gruppebegrep
tildelt denne virksomheten», men selve gruppens navn sto ingensteds — for Karasjok vises nå
«språkutviklingskommuner», med lenke til gruppebegrepet. Alt dette hentes i ETT kall
(`GET /api/virksomheter/{id}/where-used`) som dekker alle virksomhetens navneformer, framfor ett kall
per navneform.

Virksomhetsrelasjoner er bevisst IKKE del av dette oppslaget — de vises allerede i sin helhet i
«Relasjoner til andre virksomheter» på samme side, og to kilder til samme tabell ville kunne komme i
utakt.

[FIKSET, 2026-09-08] Seedede virksomhetsnavn med to likestilte navneledd skilt med « / » fikk stor
forbokstav på bare det FØRSTE leddet: «Karasjoga gielda / karasjok kommune». Nå får hvert ledd stor
forbokstav. Dette er ikke generell norsk tittelkasing — bare ledd-splitting — nettopp for ikke å
ødelegge navn som «Nærings- og fiskeridepartementet» (som må matche Lovdatas departementsnavn
eksakt). Navn hentet fra Brønnøysundregisteret røres ikke: de beholdes i registerets egen form
(VERSALER, f.eks. «SAMEDIGGI / SAMETINGET»), slik issue #158 låser.

[FIKSET, 2026-09-08] Seksjonen «Forekomster i \<definerende rettskilde\>» på et begreps detaljside
sto tom for ALLE begreper: endepunktet bak den svarte 500, og siden svelget feilen og viste
«ingen forekomster funnet» — et tomt svar var ikke til å skille fra en feil. Den viser nå de ekte,
taggkoblede forekomstene, også for en navneform (der den tidligere var tom i tillegg fordi taggen
ikke pekte på navneformen).

*Hvor:* «Virksomheter» (`/virksomheter`, `/virksomheter/:id`).

### Virksomhetskandidater

Godkjenningskø for tekstsøk-treff: sveiper alle rettskilde-noder etter FOREKOMSTER av en virksomhets
allerede kjente navneformer (f.eks. finn flere steder «Statsforvalteren» nevnes, når virksomheten og
navneformen allerede er registrert). Filtrerbar, med massegodkjenning/-avvisning.

*Hvor:* «Virksomhetskandidater» (`/virksomhet-kandidater`).

### Navnekandidater

Komplementær oppdagelseskø: i stedet for å bekrefte forekomster av KJENTE navn, leter denne etter
HELT NYE, ukjente egennavn/juridiske aktører i rettskildeteksten via regex-mønstre (aldri KI). Ett
samlet sveip dekker en fast liste juridiske aktør-substantiv («Kongen», «Stortinget», bøyningsformer
av kommune/fylkeskommune/departement/statsforvalter), et flerords-mønster som fanger hele navn
(«Statens vegvesen», «Møre og Romsdal fylkeskommune»), og et bredt «stor forbokstav midt i
setningen»-mønster der hvert unike navn valideres strukturelt mot Store norske leksikon og
Sentralt stedsnavnregister i stedet for mot en hånd-vedlikeholdt ordliste. Køen har fem faner
(Venter / Godkjent / Avvist automatisk / Avvist manuelt / Alle).

[NYTT, 2026-10-01, issue #283] **To veier inn i køen.** I tillegg til det automatiske sveipet (under)
kan en kandidat nå også opprettes manuelt: «Behandle som organ/gruppe →» i tagg-linja under et
`begrep`-merket utdrag (`TagTekst`, lenket fra `RettskildeDetalj`) oppretter — eller gjenbruker,
GET-or-create på samme nøkkel som sveipet — en rad med `OppdagelsesKilde='manuell'`, `Konfidens=null`,
og sender saksbehandleren rett inn i den samme veiviseren under.

**Veiviser for å behandle én kandidat** [NYTT, 2026-09-07]. «Behandle …» på en kandidatrad åpner en
egen, dypt lenkbar side som tar saksbehandleren gjennom hele kjeden i fem steg, og som er
tilgjengelig **uansett status** — også for rader som alt er godkjent eller avvist:

1. **Kontekst** — rettskilden, noden, ansvarlig departement, SNL/SSR-berikelsen, og hele setningen
   fra lovteksten med treffet uthevet, slik at man kan lese den og selv vurdere avgrensningen.
2. **Er teksten riktig?** — retting av regex-artefakter («Ø Suldal kommune»). Er raden **avvist**,
   settes den tilbake til «Venter» når en rettet tekst lagres, slik at den kan behandles på nytt —
   den eneste veien tilbake fra «Avvist». En godkjent rad beholder bevisst sin status. Er navnet
   derimot legitimt slik det står, skal teksten stå, og forklares med en grunn i steg 4 i stedet.
3. **Hva slags ting er dette?** — konkret virksomhet; **konkret virksomhet navngitt som medlem av en
   gruppe** [NYTT, 2026-09-08]; gruppe som defineres her (samme resultat som den gamle
   Godkjenn-knappen: gruppebegrep hjemlet i loven + koblet tagg); eller ikke relevant (raden avvises).
   **[ENDRET, issue #310, 2026-10-07]** «Gruppe som defineres her» heter nå «Begrep loven definerer»,
   og saksbehandleren MÅ velge type — **klasse, rolle eller område** — før begrepet opprettes (typen
   forhåndsvelges bare når KI-en/SSR alt har foreslått den). Det samme gjelder hurtig-«Godkjenn» i
   listen (velg typen med «Rediger») og «Behandle gruppen». Begrepssiden viser typen som tag og lar den
   endres; virksomhetssiden viser og setter **aktørtype** (rettssubjekt/organ/organisatorisk enhet).
4. **Hvilken virksomhet?** — velg fra katalogen, eller opprett underveis fra Brreg eller med bare
   navn. Her velges også **grunnen** til at navneformen peker dit (se «Virksomheter» over). På
   gruppemedlem-veien velges i tillegg **hvilken gruppe** teksten navngir virksomheten som medlem av;
   gruppen må finnes som gruppebegrep fra før, siden den er definert i en LOV mens denne rettskilden
   bare navngir medlemmene.
5. **Utover navneform?** [NYTT, 2026-10-01, issue #283 AC5/AC6/AC7] — valgfritt steg for BÅDE
   virksomhet- og gruppemedlem-sporet, kun relevant når denne KONKRETE forekomsten sier noe mer enn
   bare en navneform: «Rolle tildelt her» oppretter en generell myndighetstildeling (rollebegrep +
   paragraf/eId-avgrensning), uavhengig av et eventuelt gruppemedlemskap samtidig — generaliserer det
   som før kun fantes via gruppemedlem-veien. «Relasjon til annen virksomhet» oppretter en
   `VirksomhetRelasjon` (klageinstans, underlagt, sekretariat …; [ENDRET, #311/#330] nå en R-kant med
   docs/33-kodene — klageinstans for, administrativt underordnet, sekretariat for …) når teksten beskriver et
   organisatorisk forhold til en annen, navngitt virksomhet — løser issue #263 AC2/AC3 sitt
   "Minimalt"-nivå. Gruppe-sporet («gruppe som defineres her») får i tillegg et eget, valgfritt
   gruppe-av-gruppe-tillegg som oppretter gruppebegrepet OG medlemskapet atomisk i samme operasjon.
6. **Bekreft** — en oppsummering av hva som blir opprettet eller endret, før man fullfører.

Fullføring **lukker kjeden** for en virksomhet-kandidat: navneformen opprettes (eller gjenbrukes) med
sin grunn, kandidaten settes til «Godkjent», og tekst-taggen for forekomsten peker nå på
**navneformen** — som i sin tur peker på virksomheten — og er synlig i rettskildeteksten under et
eget, valgbart lag «Virksomhet» i tagg-velgeren, der hele kjeden vises
(«Karasjok» → [Kortform] → virksomhetsnavnet, se «Virksomheter» over).
[ENDRET, navneform-kjede, 2026-09-08: taggen pekte tidligere direkte på virksomheten, slik at
mellomleddet — og dermed navneformgrunnen — ikke var gjenfinnbart fra taggen.]
Fantes det alt en ubundet tagg på samme sted (etterlatt av en tidligere godkjenning),
er det DEN som kobles — ingen ny, overlappende tagg. Kan ingen tagg opprettes fordi rettskilden
mangler et ansvarlig departement som finnes i virksomhetskatalogen (en tagg må eies av noen), eller
fordi tegnposisjonene ikke lenger stemmer etter en reimport, **sies det eksplisitt** i
bekreftelsen — navneformkoblingen lykkes uansett.

Gruppemedlem-veien gjør alt det over, og legger til **én ting**: en myndighetstildeling som gjør
virksomheten medlem av gruppen, hjemlet i **kandidatens egen rettskilde** — det er der navnet står,
og hjemmelen er derfor ikke et valg saksbehandleren kan sette til noe annet. Bekreftelsen lenker
videre til gruppens side, der medlemslisten nå inneholder virksomheten. Veien er idempotent: å kjøre
den to ganger for samme par gir ikke to tildelinger.

*Kjent begrensning / bevisst utenfor denne runden:* «administrativ inndeling» er ikke et eget utfall
ennå — velg «Ikke relevant» og ta det opp separat.

[RETTET, 2026-10-01] Gruppe-av-gruppe **fra veiviseren** er ikke lenger en begrensning — se steg 5
over. Beholdes også som egen registrering utenfor veiviseren, se «Gruppe av gruppe» under.

*Hvor:* «Navnekandidater» (`/navnekandidater`, `/navnekandidater/:id/behandle`).

### Strukturkanter — relasjoner, tilhørighet, kompetanse, områder [NYTT, 2026-10-07, issue #311]

Alle strukturutsagn — «X er klageinstans for Y», «Karasjok er medlem av språkutviklingskommunene», «NVE
innehar rollen konsesjonsmyndighet etter § 3-1», «departementet har forskriftskompetanse etter § 5», «Agder
lagdømme består av …» — lagres som én type, **strukturkant**, med kategori (R relasjon, K kompetanse, M
medlemskap, O områdesammensetning, A ansvarsområde, G organtilhørighet, I rolleinnehav, T klassenivå) og
en konfigurerbar typekode. Alle har de samme egenskapene: hjemmel i en rettskilde ELLER en kilde utenfor
korpus (med type — kgl.res., instruks, tildelingsbrev, vedtekter, styrevedtak, forarbeider, nettside/annet
— og om dokumentasjonen er primær eller sekundær), avgrensning (paragrafspenn og tekst), polaritet
(«kan IKKE instruere» er et eget utsagn), gyldighet, status (forslag/validert) og hvilken mekanisme som
fant den. Myndighetstildelinger, gruppemedlemskap og virksomhetsrelasjoner ble flyttet inn uten tap.

[NYTT, 2026-10-08, issue #330] Hvert forhold har ÉN kode og én lagret retning: de gamle relasjonskodene
(«har klageinstans hos», «har sekretariat hos», «fikk oppgavene overført til», «er enhet i», «er underlagt»)
er konvertert til «er klageinstans for», «er sekretariat for», «etterfølger», «er del av» (organtilhørighet)
og «er administrativt underordnet», så «hvem er klageinstans for hvem?» gir ett svar. Avgrensningen på en
eksisterende kant kan rettes over API-et (`PUT /api/strukturkanter/{id}/avgrensning`).

Organene **Stortinget** og **Kongen i statsråd** er nå virksomheter (aktørtype organ), ikke begreper.

*Hvor:* «Myndighet & relasjoner»-fanen på en virksomhet (tre seksjoner: relasjoner, medlemskap og roller,
øvrige utsagn), «Relasjoner»-fanen på et begrep med gruppefunksjon, «Strukturutsagn hjemlet her» på en
rettskilde, KI-forslagskøen. API: `/api/strukturkanter` (se `docs/04` §14), og arbeidslista over struktur
som bare er dokumentert på nettside: `GET /api/strukturkanter/uten-korpusforankring`.

### Områderegister — fylker, kommuner, domstoler, embetsområder, helseregioner [NYTT, 2026-10-08, issue #312]

Alle 15 fylker og 357 kommuner (Kartverket), 28 tingretter med kommunene de dekker og rettsstedet sitt, 15 lagsogn
og 6 lagdømmer (forskrift om inndelingen av rettskretser og lagdømmer, hjemmel per kant), de 10 statsforvalterne →
fylker og de 4 RHF-ene → helseregion → fylker. Spørsmålet «gitt kommune X: hvilket fylke, hvilken tingrett,
lagdømme, lagmannsrett, statsforvalter og RHF?» besvares av oppslaget, som sier «ikke entydig» i stedet for å velge
når det er flere kandidater.

*Hvor:* «Område»-fanen på et områdebegrep (tilhørighet for en kommune, hvem har ansvar her, består av, inngår i),
«Ansvarsområder» i «Myndighet & relasjoner» på en virksomhet. API: `GET /api/omrader/kommuner/{nr}/tilhorighet`,
`GET /api/omrader/{id}/tilhorighet`. Fornyelse: `src/RegelIde.Data/Seed/fornye-omraderegister.py` (docs/21 §7).

### Rollebegrep og myndighetstildeling

> [ENDRET, issue #311] En tildeling er nå en strukturkant (M eller I) — se seksjonen over. Teksten under
> beskriver det saksbehandleren ser, som er uendret i form.

Et rollebegrep (f.eks. «forurensningsmyndighet») har identitet som (navn, lov) — samme rollestreng i
to ulike lover er to ulike begrep. En myndighetstildeling kobler ett rollebegrep til en konkret
virksomhet, hjemlet i en forskrift og avgrenset til et paragrafspenn. Gyldighet arves fra hjemmelens
egen status — ingen egne datoer.

*Hvor:* read-only tabell på en virksomhets detaljside, og — [NYTT, 2026-09-08] — den motsatte veien:
hele medlemslisten på gruppebegrepets egen side, se «Gruppe av gruppe» under. **Ingen generelt
frontend-skjema for å opprette en tildeling fra bunnen ennå** — men navnekandidat-veiviseren oppretter
dem nå fra saksbehandlerflyten, både via gruppemedlem-veien og — [NYTT, 2026-10-01, issue #283] — som
en generell tildeling uavhengig av gruppemedlemskap, se steg 5 under «Navnekandidater» over
(`docs/13-backlog.md` §8).

[NYTT, 2026-10-01, issue #285] En tildeling kan nå også komme fra KI-oppdagelse, med en egen
`Status` (`foreslatt_av_ai`/`validert`) som skiller et ubekreftet forslag fra en bekreftet
opplysning — se «KI-oppdagelse av virksomheter, grupper og roller» under.

### Gruppe av gruppe, og drill-through til medlemmene [NYTT, 2026-09-08]

Et gruppebegrep kan selv være **medlem av** et annet gruppebegrep. Medlemskapet er en egen entitet
(`GruppeMedlemskapEntitet`) og ikke en kolonne på begrepet, fordi det er en egen påstand med sin egen
**hjemmel**: det er en forskrift som sier at «språkutviklingskommuner» inngår i
«forvaltningsområdet for samiske språk», mens selve gruppebegrepene er hjemlet i loven. Samme
feltsett som en myndighetstildeling — de to er de to nivåene i det samme hierarkiet, ikke to
konkurrerende mekanismer. Et medlemskap er **idempotent på paret** (én opplysning uansett hvor mange
hjemler som gjentar den), og **sirkulære kjeder avvises** med en feilmelding som navngir hele kjeden,
slik at man ser hvilken registrering som må rettes. En gruppe kan lovlig være medlem av flere grupper
— grafen er en DAG, ikke et tre.

**Gruppebegrepets detaljside** viser nå tre lister, som er tre ulike påstander og derfor ikke slått
sammen til én:

- **Medlemsgrupper** — grupper som selv er medlem av denne, ett nivå ned (ikke transitivt).
- **Virksomheter i gruppen** — de konkrete, navngitte organene (myndighetstildelingene).
- **Medlem av** — motsatt retning, slik at man kan navigere opp igjen etter å ha gått ned.

Hjemmelen står **per rad**, ikke per liste, og lenker til nøyaktig paragrafen medlemskapet står i —
to medlemmer kan komme fra to ulike forskrifter, og en felles «hjemlet i …»-setning over tabellen
ville da vært en påstand som ikke stemmer.

**Tagger i lovteksten er navigerbare** [NYTT, 2026-09-08]. En markering i løpeteksten var tidligere
bare en farget `<mark>` med et tooltip som viste en rå GUID; eneste vei videre gikk via tagg-listen
under teksten. Er taggen koblet, er markeringen nå selv en lenke: «språkutviklingskommuner» i
sameloven går til gruppebegrepet og dermed til medlemslisten, og «Karasjok» i forskriften går til
virksomheten «Karasjoga gielda / karasjok kommune» — altså til *virksomheten*, ikke til strengen.
Fargen er uendret (den bærer allerede betydning: hvilket tagg-lag), og seleksjon for å opprette nye
tagger virker fortsatt over og rundt taggede ord.

**Ekte eksempeldata.** Forvaltningsområdet for samiske språk seedes ved oppstart
(`SamiskSprakforvaltningSeed`): de fire gruppebegrepene hjemlet i sameloven § 3-1, de tre
kommunekategoriene som medlemsgrupper av forvaltningsområdet, og kommunene forskriften § 1 navngir
som konkrete medlemmer — med «Karasjok» som en `kortform`-navneform for «Karasjoga gielda / karasjok
kommune». [UTVIDET, tagg-synlig, 2026-09-08] Hver kommune får nå **to** navneformer: kortformen som
står i forskriftsteksten («Karasjok», grunn `kortform`, den taggen peker på) og den alminnelige
norske navneformen («Karasjok kommune», grunn `gjeldende`), som er mellomleddet visningen resolver
til. De gjeldende navnene står som eksplisitt innsjekket data i seeden framfor å utledes av
registernavnet ved strengmanipulasjon — de tospråklige registernavnene har ulik form, og et navn en
slik utledning tok feil av ville blitt seedet som «gjeldende» og dermed sett offisielt ut.
Seeden går gjennom de **generelle tjenestene**, samme kodevei en saksbehandler utløser fra
veiviseren, slik at den også er en verifikasjon av at mekanismen virker. Den er idempotent, og
**oppfinner ingenting**: mangler rettskildene, nodene eller kommunene i miljøet, hoppes det som
mangler over og rapporteres i oppstartsloggen i stedet for at en rettskilde eller virksomhet
opprettes for å få eksempelet til å se komplett ut.

*Hvor:* gruppebegrepets detaljside (`/begreper/:id` for et begrep med gruppefunksjon), som nå også viser
«Andre strukturutsagn»; [ENDRET, #311] `GET/POST /api/strukturkanter` (M-kanter begrep → begrep). De gamle
`GET /api/gruppebegrep/{id}/medlemsgrupper|overordnede-grupper|tildelinger` står som lesefasader for
nettside-eksporten; `POST /api/gruppemedlemskap` er fjernet.

### KI-oppdagelse av virksomheter, grupper og roller [NYTT, 2026-10-01, issue #285]

En KI-agent (`VirksomhetOgGruppeKiOppdagelseTjeneste`) leser valgte rettskilders tekst FRITT — ikke
regex/tekstsøk som sveipene over, men samme forslagsmønster som «Identifiser begrep»/«Identifiser
tjenester» — og foreslår virksomhet-/gruppekandidater, pluss rolle/relasjon/gruppe-av-gruppe der
teksten eksplisitt sier det. Skriver utelukkende via de samme tjenestemetodene de ordinære
HTTP-endepunktene selv bruker (ingen egen, parallell skrivevei). Hver opprettet rad
(`Myndighetstildeling`/`VirksomhetRelasjon`/`GruppeMedlemskap`) får `Status='foreslatt_av_ai'` i
stedet for det vanlige `'validert'`, og vises i en egen kø adskilt fra menneske-opprettede rader — et
menneske må eksplisitt godkjenne (sletter forslaget og setter `'validert'`) eller avvise (sletter
raden) før den regnes som gjeldende. Rader opprettet av et menneske er fortsatt `'validert'` med én
gang, uendret oppførsel.

*Hvor:* «KI-oppdagelse» (`/ki-oppdagelse`); `POST /api/ki-oppdagelse/kjor` (kjør for valgte
rettskilder), `GET /api/ki-oppdagelse/ko` (samlet kø).

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

## Offentlig nettside (`nettside/`)

Separat fra appen (`src/RegelIde.Web`) beskrevet over: et statisk mikronettsted som viser utvalgte
deler av korpuset offentlig, bygget fra data eksportert med `nettside/tools/eksporter-data.ps1`. [NYTT,
2026-10-01, issue #287] Siden «Kjeden» (`nettside/kjeden/index.html`) viser hele kjeden rettskilde →
begrep → virksomhet → tjeneste → vilkår → vedtak som én sammenhengende fortelling for en leser uten
forkunnskap, med Karasjok kommune/sameloven som gjennomgående eksempel — de tre første leddene hentet
live fra samme datasett som koblinger-siden bruker, de tre siste tydelig merket som illustrasjon siden
DigRett ennå ikke har eksportert tjeneste-/vilkår-/vedtaksdata. Lenket fra forsidens hero, kortgrid og
hovednavigasjon. Denne oversikten dekker ikke nettsidens øvrige sider i detalj — se `nettside/`
selv — kun at «Kjeden» nå finnes.

## Ikke startet / bevisst utenfor MVP

Per `docs/06-veikart.md`: Presedensregister (byggestein 3), full saksbehandling/forklaringslogg
(byggestein 7), Kunnskapsgraf/påvirkningsanalyse (byggestein 8) og Dashboard (byggestein 9) — de to
siste bevisst utenfor MVP, siden de er «strukturelt umulige å bevise noe med før byggestein 1–7 har
reelt innhold» (se også `docs/27-innsikt-sporsmal-vurdering.md` for en konkret vurdering av hva som
faktisk kan bygges som små rapporter allerede nå, uten å bygge et fullt dashboard).
