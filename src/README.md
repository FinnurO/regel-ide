# src/ — Byggesteg 1: konverteringspipelinen

.NET 8-løsning. Scope for denne byggeøkten: **kun** konverteringspipelinen
(Lovdata-HTML → AKN-XML + nodetre), jf. `docs/06-veikart.md` byggesteg 1 og
`docs/08-byggesteg1-teknisk-design.md`. Ingen database, ingen HTTP-API, ingen
frontend ennå — se prosjektene:

- `RegelIde.Kildekonvertering` — selve pipelinen (`LovdataKonverterer.Konverter`).
- `RegelIde.Kildekonvertering.Tests` — xUnit-tester mot de ekte, fullstendige
  dokumentene i `data/kilder/raw-lovdata/` (ikke syntetiske utdrag).

```bash
dotnet test src/RegelIde.Kildekonvertering.Tests
```

## Datakvalitetsfunn: fixture-filene var dobbelt feilkodet

`data/kilder/raw-lovdata/*.html` var på disk kodet med en mojibake-feil (UTF-8-tekst
der norske tegn var kodet feil én gang for mye — sannsynligvis en cp1252→UTF-8-
konvertering kjørt på tekst som allerede var UTF-8). Dette er rettet i denne
byggeøkten ved å skrive filene tilbake med korrekt UTF-8 (samme innhold, kun
tegnkoding endret — se git-historikk). `LovdataKonverterer.Konverter` forutsetter
nå korrekt UTF-8 inn; `DekodCp1252TilUtf8` dekker det *ekte* scenarioet (rå bytes
rett fra Lovdatas bulk-datasett), ikke denne engangsrettingen.

## Bevisste utvidelser utover det låste designet (§6 i teknisk design dekker kun to åpne punkter — disse er nye, oppdaget under implementasjon)

Låst gjennom tre QA-runder før koding, men noen strukturelle detaljer i den ekte
HTML-en var ikke dekket av spec'en og krevde et implementasjonsvalg. Alle er
lavrisiko/reversible, men bør nevnes for jurist/fagansvarlig ved side-ved-side-
verifiseringen (AK-3.3.6) og ev. tas opp i en fjerde QA-runde:

1. **Underinndeling (romertall) sin eId** — ikke i §1.2s tabell. Valgt:
   `{kapittel-eId}/rom-{romertall}` (f.eks. `kap-3/rom-I`).
2. **Punkt-i-punkt (flernivå punktlister)** — bekreftet i ekte data
   (alkoholforskriften § 6-2, § 14-3). Håndteres rekursivt: et punkt kan ha egne
   underpunkt med samme `PunktEid`-mønster (`{punkt-eId}/punkt-N}`).
3. **Punkt med flere direkte legalP-"ledd"** (§ 14-3 punkt 14) — bladteksten
   konkatenerer alle direkte legalP-barns tekst i dokumentrekkefølge, i stedet for
   å kreve nøyaktig ett.
4. **Datokode uten løpenummer** — eldre lover/forskrifter identifisert av Lovdata
   med bare dato (f.eks. `LOV-1927-04-05`, ingen `-NN`) forekommer i kryssreferanser.
   ELI-en utelater da løpenummer-segmentet.
5. **`regelIde:`-XML-navnerom** — design-eksemplene i §1.1/§1.3 bruker prefikset
   `regelIde:` uten å vise en `xmlns:regelIde`-deklarasjon. Denne byggeøkten
   deklarerer `xmlns:regelIde="https://regel-ide.no/ns/akn-utvidelse/1.0"` — en
   plassholder-URI, ikke bekreftet/registrert noe sted.
6. **Kapitteloverskrift kan selv ha `data-repealeddate`** (alkoholloven kapittel 4,
   på `<h2>`-nivå, ikke bare på `<article>` som §3.2 dekker) — **ikke fanget opp**
   i denne byggeøkten. Selve paragrafene i kapittelet er ikke opphevet (kun
   utvalgte enkeltparagrafer, som håndteres korrekt), så dette påvirker ikke
   testcaset, men et fremtidig kapittel-nivå opphevet-flagg er ikke implementert.
7. **Romertall kan være selve hovedkapittelnummereringen**, ikke bare en
   underinndeling inni et arabisk-nummerert kapittel (§3.2 sitt eksempel er fra
   alkoholloven kapittel 3). Forvaltningsloven nummererer sine toppnivå-kapitler
   "Kapittel I", "Kapittel II" osv. — håndteres uten kodeendring, siden
   kapittelnummeret uansett behandles som en opak streng.
8. **Dokumentnivå-merknad direkte i `documentBody`** (`<article class="defaultP">`)
   — forvaltningsloven har et varsel om at hele loven oppheves fra en fremtidig
   dato, plassert før første kapittel. Behandlet som endringshistorikk/metadata,
   ikke tekstinnhold — samme prinsipp som `changesToParent`, ingen node produseres.

## Ekstern code review (Copilot, 2026-07-24) — funn og oppfølging

Kjørt mot diff'en for koden over. Triagert punkt for punkt mot faktisk kode/ekte data
(ikke blindt implementert) — se commit-historikk for de faktiske rettingene.

**Reelle bugs funnet og rettet:**
- Fotnotetekst duplikerte etiketten (f.eks. "1 Jf. EØS-avtalen …" i stedet for
  "Jf. EØS-avtalen …") — `<span class="footnoteLabel">` ble telt med i løpeteksten
  i tillegg til at `Fotnote.Etikett` hentet den separat.
- Tekst før og etter en hoppet-over liste smeltet sammen uten mellomrom
  (f.eks. "herunderDet skal …") — bekreftet reelt i alkoholforskriften § 7-2 sin
  `<p class="leddfortsettelse">`. Samme feil fantes ved skjøten mellom flere
  direkte legalP-"ledd" i samme punkt (§ 14-3 punkt 14). Begge rettet ved å sette
  inn et mellomrom-segment ved skillet; ev. doble mellomrom ryddes bort med
  `KollapsDobleMellomrom` (kun kosmetisk — påvirker ikke tekst_hash, som uansett
  normaliserer all whitespace, §3.4).

**Testdekning lagt til** (verifiserte påstander om manglende dekning):
lettermerkede paragrafer (§ 1-4a), kapittel med bokstav (3A) sin eId, kryssreferanse
til lettermerket paragraf i et ANNET dokument (§ 9-4 → markedsføringsloven § 43a —
ekte data), datokode uten løpenummer (`LovdataIdentifikatorerTests`, direkte
enhetstester uten HTML), flere fotnoter på samme paragraf (syntetisk — forekommer
ikke i de ekte dokumentene), tom/ufullstendig HTML gir kontrollert `FormatException`
i stedet for stille feil, norske bokstaver (æøå) i slugifisering.

**Sjekket, men bekreftet IKKE et problem:** slugifisering av "Ø/Æ/Å" — Copilot fryktet
at bokstaven forsvinner (f.eks. "Økonomiske" → "konomiske"). Verifisert med en
syntetisk test: `ToLowerInvariant()` kjører før de eksplisitte æ/ø/å→ae/o/a-erstatningene,
så dette fungerer korrekt. Testen beholdes som regresjonssikring.

**Bevisst IKKE endret (uenig med anbefalingen, eller lav verdi gitt prosjektets
prinsipper):**
- *"Parseren bør tåle ekstra wrapper-elementer rundt kjente strukturer."* Dette
  strider mot det låste prinsippet i §3.3 om at ingen gjettet fallback skal
  produseres — en uventet HTML-struktur skal feile høylytt med kontekst, ikke
  gjette seg forbi. Verdt en bevisst diskusjon med Johann hvis det blir aktuelt,
  ikke noe jeg endrer stille.
- URL-enkodet paragraftegn i href — ingen av de 498 hrefene i de to ekte dokumentene
  bruker URL-enkoding (alle bruker bokstavelig `§`). Uendret href faller uansett
  trygt tilbake til synlig tekst (ingen kryssreferanse spores, ingen krasj) hvis et
  ukjent mønster skulle dukke opp — akseptert restrisiko, ikke fikset spekulativt.
- `regelIde:`-navnerom-URI-en er allerede notert over som en plassholder som bør
  fryses før tverrsystem-validering — ingen ny handling utover det.

## Bevisst utenfor scope (jf. `06-veikart.md` byggesteg 1 og `08-byggesteg1-teknisk-design.md` §5)

Databaselag, HTTP-API, tre-navigasjon-UI, tekstmerking/tagging-UI, referanse-stub-
opprettelse i et faktisk bibliotek (§3.1 steg 6 — krever databasetilgang for å vite
om målet allerede er importert), proveniens-skriving for `changesToParent`,
Lovdata API-nøkkel-integrasjon (leser fortsatt fra lokale filer).
