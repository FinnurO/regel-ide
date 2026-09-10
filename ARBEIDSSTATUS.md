# Arbeidsstatus — pågående arbeid

**Sist oppdatert: 2026-09-09.** Denne fila beskriver arbeid som er I GANG, ikke ferdig arbeid.
Den er bevisst midlertidig: er alt under landet, **slett fila**. Varige arbeidsregler hører i
`CLAUDE.md`, varige designbeslutninger i `docs/09-design-konvensjoner.md`.

Fila finnes fordi en økt kan bli avbrutt uten forvarsel (tokenkvote, sesjonsgrense), og neste økt
skal kunne fortsette uten å utlede alt på nytt. **Oppdater den etter hvert steg, ikke til slutt.**

---

## Rammer som ikke er valgfrie

- **Testene kan ikke kjøres parallelt.** `RegelIde.Data.Tests` bruker FAST port 55432 for embedded
  Postgres (`EmbeddedPostgresFixture`, issue #10). To samtidige kjøringer kolliderer — uansett om de
  står i ulike git-worktrees. Kjør ett prosjekt om gangen, i forgrunnen (CLAUDE.md §5).
- **Nettleserpanelet og dev-serverne er én delt ressurs.** Bare én aktør kan drive dem. API kjører
  på 5187, web på 5173, konfigurasjonene heter `regel-ide-api`/`regel-ide-web` i
  `.claude/launch.json`. Skal du verifisere en branch i nettleseren: sjekk den ut DETACHED i
  hovedtreet (`git checkout --detach <sha>`) — vite følger hovedtreet, ikke worktreene.
- **Parallelle agenter: erfart 2026-09-09.** Tre agenter i egne worktrees ble startet samtidig. Alle
  tre døde på SAMME sesjonsgrense, midt i arbeidet, uten å ha committet noe. Worktreene hindret
  filkonflikter, men tokenbudsjettet er delt — parallellitet flerdobler forbruket og gir ingen
  isolasjon mot at kvoten tar slutt. Arbeidet var likevel godt: alt de tre leverte ble landet etter
  gjennomgang og testkjøring. Lærdom: instruer agenter om å **committe og pushe underveis**, og regn
  med at orkestratoren må fullføre innkoblingen selv.
- **Én agent per EF-migrasjon.** To samtidige `dotnet ef migrations add` gir kolliderende
  ModelSnapshot.

---

## Branches i luften

Begge er **ferdige og verifiserte**, og venter kun på Johanns gjennomgang/merge. Merges de, skal de
merges med `gh pr merge <nr> --squash --delete-branch` (CLAUDE.md §13).

- **`kandidatsider-delt` → PR #226** — lukker #216 og #167. Fire commits: delt sortering/avkryssing,
  delt node-etikett (rettet feil paragraf på alle tre sidene), massehandling på begrepskandidatsiden
  med nye batch-endepunkter, og bunnjustering av raden. Api 294/294, Data 710/710, vitest 62/62.
- **`hjemmel-ledd-presisjon` → PR #227** — lukker #217. Hjemmelen peker nå på leddet kilden
  presiserer. 416 av 22 262 hjemmelrader oppgradert på ekte korpus. Kildekonvertering 115/115,
  Api 294/294, Data 715/715.
- **`fastsatt-av-organ` → PR #230** — lukker #215. Stablet PÅ #227 (PR-basen er satt til den
  grenen), fordi migrasjonen er generert oppå den andre. Merges #227 først, går denne automatisk mot
  master. 4701 av 5565 rettskilder fikk en fastsetter; `virksomhet_id` uendret for alle 5565.
  Kildekonvertering 131/131, Api 294/294, Data 721/721, vitest 62/62.

**Utført på Johanns base (begge idempotente, trygge å gjenta):**
- `POST /api/administrasjon/hjemmel-presisjon-etterfylling` — kjørt én gang 2026-09-10. Verdt å
  gjenta etter at flere lover er importert: en presisering som ikke kunne løses fordi loven manglet,
  løses da.
- `POST /api/administrasjon/fastsatt-av-etterfylling` — kjørt to ganger 2026-09-10 (andre kjøring
  fylte de 16 radene med de nye resolusjonsformene, og bekreftet idempotensen på ekte data). Verdt å
  gjenta når nye rettskilder er importert. Selve KOBLINGEN til virksomhet løses ved lesing, så nye
  virksomheter/navneformer kobler seg av seg selv uten ny kjøring.

**Gjort på Johanns base etter hans klarsignal (org.nr 974 767 880):** NTNU opprettet via
`POST /api/virksomheter/fra-brreg` — Kunnskapsdepartementet ble morenhet automatisk, og SNL bekreftet
navneformen `NTNU`. Lovdatas skrivemåte «Norges teknisk-naturvitenskapelige universitet (NTNU)» er
lagt inn som navneform, og koblingen løste seg umiddelbart uten ny etterfylling (lesetids-oppløsning).
NTNUs side viser nå seks forskrifter. #215 kriterium 6 er dermed oppfylt.

**To åpne spørsmål til Johann:**
1. NTNU har nå TO navneformer med grunn `gjeldende`: `NTNU` (auto fra SNL) og den fulle formen. Bare
   én kan være det gjeldende navnet — den fulle ER navnet, `NTNU` er en kortform. Ikke endret, fordi
   det er en modelleringsavgjørelse på hans katalog, og det styrer visningsnavnet (i dag `NTNU`).
   Kan være verdt en CHECK-constraint: én `gjeldende` per virksomhet.
2. #231 (nytt): navnekandidat-sveipet var den tenkte veien til NTNU-navneformen, men fanget «Norges»
   i stedet for hele navnet — bakoverskanningen stopper på tankestreken i
   «teknisk–naturvitenskapelig». Kodens egen kommentar bekrefter mekanismen. Samme sveip viste også
   at «nemnd» mangler i `Institusjonsord`, så «Felles klagenemnd» blir «Felles».

**Til Johann, blokkert for meg:** 56 fjerngrener med merget PR ligger igjen på origin.
`git push origin --delete …` blir konsekvent avslått av auto-modus-klassifiseringen. Lokalt er det
ryddet (21 → 3). Kommandoen med full grenliste står i økt-loggen.

---

## Issues: gjort 2026-09-09

Merget til master: **#208** (registernavn-synk gated + seed-vakter), **#209** (nemnd/sekretariat/RME,
konfidens erstatter auto-avvisning), **#210** (postcss), **#211** (CLAUDE.md §11), **#218** (slett
navneform), **#220** (eier-virksomhet + denne fila), **#221** (bindestrek-forkortelser), **#222**
(hermetegn + verbmarkør), **#223** (punktliste + departementslenke).

Lukket — se kommentaren i hvert issue for hva som faktisk ble målt: **#118**, **#120**, **#128**,
**#129**, **#133**, **#135**, **#138**, **#150**, **#154**, **#155**, **#161**, **#168**, **#194**,
**#213**, **#214**.

Nye issues, alle med målte tall og akseptansekriterier: **#212** (dupliserte definisjoner),
**#215** («Fastsatt av» → organ), **#216** (kandidatsidene spriker), **#217** (hjemmel mister
ledd-presisjon), **#219** (`sistEndretVed` som relasjon).

---

## Kø: neste, i denne rekkefølgen

1. **#134 Advokatbevillingsnemnden** — ferdig undersøkt, modellen er beskrevet i issue-kommentaren
   (advokatloven § 73 syvende/åttende ledd splitter organets saker mellom Advokatnemnda og
   Advokattilsynet, så dagens navneform på Advokattilsynet er en påstand loven motsier). Trenger en
   ny relasjonstype `oppgaver_overfort_til`: `RelasjonsTypeKonfigurasjonEntitet` er data-styrt, så
   det er SEED + en rad i eksisterende base, ikke en skjemaendring. Merk at oppstartsseeden bare
   kjører når tabellen er TOM.
2. **#216 kandidatsidene** — besluttet med Johann: trekk ut delt komponent for alle tre sidene
   samtidig. Skriver om tre store filer, så den MÅ kjøre alene. Tar #167 (konfidens-filter på
   begrepskandidater) med seg — backend-filteret kom i #222, UI-et mangler.
3. **#212 dupliserte definisjoner** — besluttet: ett begrep per forskrift, relatert via en ny
   relasjonstype mellom begreper, deteksjon på ord-for-ord-likhet. Trenger migrasjon.
4. **#215 «Fastsatt av»** — besluttet: parse frasen, koble til virksomhet, vis på rettskilden og som
   «Rettskilder fastsatt av denne virksomheten». Rør IKKE `Rettskilde.VirksomhetId`.
5. **#217 hjemmel-presisjon** og **#219 sistEndretVed** — begge lav prioritet, begge gjenbruker
   mekanismer som alt finnes og er testet.

Sporingssaker som IKKE er oppgaver og ikke skal «løses»: **#172, #177, #178, #179, #180** (hele
byggesteg som ikke er startet, flere eksplisitt utenfor MVP).

---

## Beslutninger Johann tok 2026-09-09 (bindende for arbeidet over)

- **Dupliserte definisjoner (#212):** ett begrep per forskrift, RELATERT — ikke ett delt begrep på
  tvers. NTNUs og MFs definisjon er to ulike tekster med ulik hjemmel, og skal ikke slås sammen.
- **Eierskap (#215):** vis fastsetteren koblet til virksomhet. IKKE en virksomhetsrelasjon av
  sekretariat-typen, og IKKE `Rettskilde.VirksomhetId` (det feltet betyr «virksomhetens eget,
  private dokument» — å sette det ville skjult forskriften for alle andre og for sveipene).
- **Kandidatsidene (#216):** trekk ut delt komponent nå, alle tre samtidig.
- **Konfidens framfor avvisning:** «vi kan ikke automatisk avvise disse p.g.a. manglende SNL/SSR.»
  Landet i #209 — se `docs/31` §9.
