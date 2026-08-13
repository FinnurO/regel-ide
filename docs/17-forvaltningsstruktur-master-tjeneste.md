# 17. Notat: Forvaltningsstruktur (organisasjonstyper) og master-tjeneste

*Status: forslag til beslutning, ikke besluttet. Egen avklaringsrunde, samme form som ontologilåsen og
håndbok-/dokumentgraf-notatet (`15-handbok-dokumentgraf-notat.md`) fikk. Ingen kode, ingen migrasjon og
ingen entitet er skrevet mot dette notatet — det er bevisst: Johann har varslet at han vil levere en
seed-liste over organisasjoner senere, og skjemaet bør ikke låses før den listen og spørsmålene i §9 er
avklart. Mottatt som oppdrag fra Johann 2026-08-13/14.*

Verifisert mot `master` @ `a246f5e` (2026-08-14). Alle linjenumre er fra det commit-et. Tre andre
branchar hadde kodeendringer under arbeid samtidig; ingen av dem berører feltene dette notatet
diskuterer, men linjenumre kan ha forskjøvet seg når notatet leses senere.

---

## 0. Markører

| Markør | Betyr |
|---|---|
| **[LÅST]** | Besluttet i en tidligere runde, gjengitt her fordi det binder dette designet. Skal ikke reåpnes i denne runden |
| **[ANBEFALT]** | Mitt forslag, med begrunnelse. Trenger Johanns ja før koding |
| **[PÅ AVKLARING]** | Ikke avgjort, og jeg kan ikke avgjøre det alene. Samlet i §9 |
| **[UTENFOR SCOPE]** | Bevisst utsatt til en egen, senere runde. Samlet i §8 |
| **[VERIFISERT]** | Påstand om dagens kode, sjekket mot fil og linje |

### 0.1 Bærende prinsipp for hele notatet

To prinsipper fra tidligere runder styrer hver anbefaling under, og er grunnen til at flere av svarene
er «ikke bygg noe»:

- **Høst struktur, ikke generer den.** Der strukturen finnes i virkeligheten (Enhetsregisteret,
  loven), skal den leses inn — ikke modelleres på nytt. `docs/15` §3.4: «strukturen finnes, den skal
  bare leses.»
- **Ingen gjettet fallback.** Manglende data skal føre til avvisning eller `NULL`, aldri til en
  plausibel utfylling. Håndhevet i koden i dag, f.eks.
  `TjenesteregisterTjeneste.cs:37-40` («Tittel kan ikke være tom. Ingen gjettet fallback.») og
  `BergenKorpusSeed.cs:102`.

Konsekvensen for dette notatet: det billigste riktige svaret på «hvordan modellerer vi 357 kommuner»
er sannsynligvis *ekte data i en tabell som allerede finnes*, ikke en ny tabell. Det er nettopp hva §4
konkluderer med.

---

## 1. Behovet, i Johanns egne ord

> «Vi må få til en struktur som reflekterer strukturene i offentlig forvaltning. F.eks nivået
> 'Kommune' som kan knyttes til 357 kommuner. Hver kommune har navn, orgnummer. Samme med
> 'Fylkeskommuner', 'Statsforvaltere', 'Tingretter', 'Lagmannsretter', 'Jordskifteretter'. Det skal
> altså være mulig å definere de organisasjonene som er like. Så kan vi jobbe på samlenivået men de
> reelle tjenestene ligger på org.nivå. Så vi må ha et konsept om 'master' tjeneste som kan
> arves/knyttes til de underliggende tjenestene. Dette gjør vi for å kunne sammenligne på tvers og
> etterhvert få en konsolidering.»

To krav, som henger sammen men kan bygges uavhengig:

1. **Organisasjonstype-taksonomi** — «Bergen kommune» er én instans av typen «Kommune»; typen har
   søsken (Fylkeskommune, Statsforvalter, Tingrett, Lagmannsrett, Jordskifterett); hver type har sitt
   eget sett navngitte instanser med navn + organisasjonsnummer.
2. **Master-tjeneste** — en tjeneste definert på type-/samlenivå («Skjenkebevilling» som felles
   konsept for alle kommuner) som de konkrete per-kommune-radene knyttes til, for sammenligning på
   tvers og senere konsolidering.

Det er verdt å merke at behovet ikke er nytt i prosjektets dokumentasjon — det er *skjemaet* som
mangler, ikke erkjennelsen. `docs/15` §4 beskriver allerede den samme arbeidsdelingen som en
firefaset prosess (15:364-374):

> **Fase 2 — Kanonisk tjenesteliste og generell beskrivelse (én gang, 3–5 kommuner).** […] Forfatt den
> generelle tjenestebeskrivelsen **én gang** — Finlands modell, der DVVs redaksjon skriver og eksperter
> faktasjekker. […]
>
> **Fase 3 — Lokal utfylling (alle 357).** Målrettet uttrekk mot **fast** feltliste og fast
> rettskildeliste […]
>
> **Fase 4 — Kryssammenligning som kvalitetskontroll (kontinuerlig).** Dette er premien; se §6.5.

Master-tjenesten er skjemaet til fase 2. Instansene er fase 3. Konsolideringen Johann nevner er fase
4. Notatet her gir de tre fasene en datamodell, og stopper der — se §6.

Og gevinsten er formulert i `docs/15` §7 (15:571-573):

> Regel-IDEs leveranse er derfor ikke bare en katalog, men **normalisering**: samme tjeneste, samme
> feltnavn, lokale verdier side om side, med sitat og hjemmel per verdi.

---

## 2. Hva som finnes i dag — verifisert mot koden

Ingen av punktene under er gjettet; hvert har fil og linje.

### 2.1 `Virksomhet` er nesten riktig form allerede

`Entiteter.cs:9-28`:

```csharp
public sealed class Virksomhet
{
    public Guid Id { get; set; }
    public required string Navn { get; set; }
    public string? Organisasjonsnummer { get; set; }   // :13
    public DateTimeOffset OpprettetTidspunkt { get; set; }
    public string? Kommunenummer { get; set; }          // :23
    public string? Forvaltningsniva { get; set; }       // :27 — "stat" | "fylke" | "kommune"
}
```

- **[LÅST — avklaringsrunde 1, 2026-08-12, `docs/15` §3.3]** `Organisasjonsnummer` er den stabile
  nøkkelen; `Kommunenummer` er et sekundært geografisk/statistisk attributt og **aldri** en
  URI-/oppslagsnøkkel (Bergen: 1201 før 2020, 4601 etter — samme organ, nytt nummer). Gjengitt i
  koden på `Entiteter.cs:16-23` og `BergenKorpusSeed.cs:77-81`. Dette binder dette notatet direkte:
  hver types instanser identifiseres på nøyaktig samme måte, og seed-listen i §7 er derfor bygget rundt
  organisasjonsnummer.
- **[VERIFISERT]** Det finnes allerede en filtrert unik indeks på organisasjonsnummer:
  `RegelIdeDbContext.cs:102-103` (`ux_virksomheter_organisasjonsnummer`, filter
  `organisasjonsnummer IS NOT NULL`). En 357-raders seed får altså duplikatvern gratis — og
  `NULL`-radene (Testkommunen) kan eksistere side om side.
- **[VERIFISERT]** Det finnes **ingen** `CHECK`-constraint på `forvaltningsniva`. Feltet er en fri
  streng (`RegelIdeDbContext.cs:101`).
- **[VERIFISERT]** **Ingen kode leser `Forvaltningsniva` eller `Kommunenummer` for logikk.** Eneste
  skriving er én seed (`BergenKorpusSeed.cs:83`), eneste lesing er en testassert på samme seed
  (`BergenKorpusSeedTests.cs:46-47`). Feltene er ikke i `VirksomhetDto`
  (`GjeldendeBrukerTjeneste.cs:43` — kun `Id, Navn, Organisasjonsnummer`) og forekommer ikke ett sted
  i `src/RegelIde.Web`. Den dokumenterte hensikten («styrer hvilket organ som er vedtaksmyndighet»,
  `Entiteter.cs:25-26`) er uimplementert.
- **[VERIFISERT]** Alle andre `Virksomhet`-rader enn Bergen mangler både organisasjonsnummer og
  forvaltningsnivå: `AgderFylkeskommuneSeed.cs:22` og `Program.cs:194-197` (Testkommunen),
  `KommunaleParametreSeed.cs:29-30` (Tønsberg, Bærum). Et oppslag på `Forvaltningsniva = "kommune"`
  ville i dag truffet **én** rad.
- **[VERIFISERT]** Det finnes **ingen** API eller UI for å opprette en virksomhet — bare seed. Sagt
  eksplisitt i `AgderFylkeskommuneSeed.cs:10-14`; bekreftet ved at `/api/virksomheter` kun har GET
  (`Program.cs:309-313`). Seed er altså den eneste veien inn for 357 rader i dag, og det er greit.

### 2.2 `TjenesteEntitet` er moden, og har én antakelse master-konseptet bryter

`Entiteter.cs:346-382` — full CPSV-AP-NO-modell med statusløp, versjonering, proveniens og
regelverksreferanser. Den ene setningen som betyr noe her er `Entiteter.cs:350-351`:

```csharp
/// <summary>Påkrevd (§0.1) — en tjeneste er alltid virksomhetens eget arbeidsprodukt, aldri delt.</summary>
public required Guid VirksomhetId { get; set; }
```

Tilsvarende i `docs/03-domenemodell.md` §0.1 (03:25): Tjeneste står i kategorien «Alltid satt
(påkrevd)». **En master-tjeneste er per definisjon delt og ikke én virksomhets arbeidsprodukt**, så
denne ene antakelsen må endres bevisst — se §5.3.

Andre verifiserte forhold som styrer designet:

- **[VERIFISERT]** Kun listeendepunktet er virksomhet-scopet:
  `TjenesteregisterTjeneste.cs:19-23` (`Where(t => t.VirksomhetId == virksomhetId && …)`), kalt fra
  `Program.cs:865`. `FinnAsync` (`:25-26`), `OppdaterAsync` (`:126`), status, rotnode og
  regelverksreferanser filtrerer **kun** på `Entitetsstatus` — enhver innlogget bruker kan i dag lese
  **og skrive** enhver tjeneste hvis hun har id-en. Det er en eksisterende svakhet, ikke en jeg
  innfører, men den blir mer alvorlig med delte master-rader (§5.6).
- **[VERIFISERT]** `OppdaterAsync` (`TjenesteregisterTjeneste.cs:129-140`) skriver **alle** felt
  ubetinget fra forespørselen, og `TjenesteRequest` (`Dtos.cs:143-146`) sender hele feltsettet. Dette
  er det konkrete tekniske argumentet mot ekte feltarv i første runde — se §5.1.
- **[VERIFISERT]** `TjenesterListe.tsx:122-156` har allerede en **Eier**-kolonne, som slår id → navn
  via `useVirksomheter.ts:44-48` (`visEier`). En rad uten eier trenger en eksplisitt etikett der.
- **[VERIFISERT]** CPSV-feltsettet redigeres på ett sted i frontend: `TjenesteDetalj.tsx:405-429`.
- **[VERIFISERT]** 25 migrasjoner i `src/RegelIde.Data/Migrasjoner`. Postgres kjører `MigrateAsync`,
  SQLite kjører `EnsureCreatedAsync` og får aldri migrasjoner (`Databaseoppsett.cs:75-89`). Et nytt
  felt trenger altså **én** Npgsql-migrasjon; SQLite-profilen får det gratis fra modellen, men en
  rå-SQL-backfill vil *ikke* kjøre der.

### 2.3 Mønstre som allerede finnes, og som dette designet bør gjenbruke

Dette er den viktigste delen av kartleggingen: prosjektet har allerede løst «delt mal vs. lokal
instans» tre ganger, på tre nivåer. Ingen av dem er en ny entitet.

| Nivå | Mekanisme | Sted |
|---|---|---|
| Rettskilde | `Guid? VirksomhetId` — `NULL` = delt/nasjonal (Lov/Forskrift fra Lovdata), satt = virksomhetens egen lokale kilde | `Entiteter.cs:65`, `docs/03` §0.1 (03:26) |
| Hendelse | `Guid? VirksomhetId` — `NULL` = nasjonal/delt hendelse | `Entiteter.cs:404` |
| Kodeliste | `Guid? VirksomhetId` — `NULL` kun for `Type='ekstern-referanse'` | `Entiteter.cs:520-521` |
| Parameterverdi | `Guid? VirksomhetId` — `NULL` = **den nasjonale standardverdien** for kommuner uten eget regelsett | `Entiteter.cs:586-598`, seedet i `KommunaleParametreSeed.cs:34-44` |
| Vilkår | `GeneriskMal` — fritekstkode, f.eks. `"GM-VANDEL-PERSON"`, «ingen egen registertabell i v1» | `Entiteter.cs:625` |
| Global konfigurasjon | `TaggKindKonfigurasjonEntitet` — egen liten tabell, ikke virksomhet-scopet, seedet i oppstart | `Program.cs:209-217` |

`KommunaleParametreSeed.cs:34-44` er verdt å se på i sin helhet, fordi det *er* master/instans-mønsteret
én etasje ned og allerede i drift: tre verdier for samme felt `klokkeslett.tidspunkt` — Tønsberg
(`08:00–02:00 …`), Bærum (`07:00–03:00`) og en rad med `VirksomhetId = null` kommentert som
«Standardregel (§8.4-mønsteret) — nasjonal norm for kommuner uten eget registrert regelsett».
`Entiteter.cs:590-592` beskriver samme sak i entitetens egen dokumentasjon.

Med andre ord: **`VirksomhetId IS NULL` betyr allerede «samlenivå/nasjonal» i denne kodebasen, fire
steder.** Det er ikke en ny konvensjon jeg foreslår, det er den etablerte.

To låste beslutninger fra `docs/15` §10.2 binder også §5:

- **[LÅST]** `TjenesteEntitet` får en diskriminator `Registertype` (`"tjeneste" | "forvaltningsoppgave"`),
  **non-nullable, ingen C#-default**, med backfill av eksisterende rader til `"tjeneste"`
  (15:843-857). Besluttet, aldri kodet. Enhver ny diskriminator jeg foreslår må ikke forveksles med
  denne, og bør følge samme form.
- **[LÅST]** «Slusen skal være strukturell, ikke basert på disiplin» (15:880-887): når flere
  logiske sett bor i samme tabell, skal filtreringen ligge i et dedikert repository-lag *pluss* en
  regresjonstest som beviser at den andre typen rader er fraværende i output. Dette gjelder direkte
  for master-rader i `tjenester`.
- **[LÅST]** En relasjon med annen semantikk skal **ikke** presses inn i
  `TjenesteavhengighetEntitet`s sykelsjekkede graf, selv når begge ender er rader i samme tabell
  (15:889-895, om `dekker`-relasjonen). Gjelder også «arver fra» — se §5.2.

---

## 3. Spørsmål 1 — er «Kommune»/«Tingrett»/… en utvidelse av `Forvaltningsniva`?

**[ANBEFALT] Nei. Nytt felt `Organisasjonstype` på `Virksomhet`, med `CHECK`-constraint.
`Forvaltningsniva` beholdes uendret som en egen, grovere akse.**

Begrunnelsen er at de to feltene svarer på forskjellige spørsmål — samme to-akse-resonnement som
`docs/15` §13 brukte for å skille `NormativVirkning` (rettslig kraft) fra `FunksjonellRolle`
(funksjon) på rettskilder:

| | `Forvaltningsniva` (finnes) | `Organisasjonstype` (ny) |
|---|---|---|
| Spørsmål | På hvilket forvaltningsnivå ligger organet? | Hvilken *slags* organ er det? |
| Verdisett | `stat` \| `fylke` \| `kommune` (3) | `kommune` \| `fylkeskommune` \| `statsforvalter` \| `tingrett` \| `lagmannsrett` \| `jordskifterett` (6, jf. §9 om flere) |
| Bruk | Hvem er vedtaksmyndighet (bystyre/kommunestyre/fylkesting) | Hvilke instanser er «like», og hvilken master gjelder |

Å presse taksonomien inn i `Forvaltningsniva` går ikke, og det er lett å vise: **Statsforvalteren,
tingretten, lagmannsretten og jordskifteretten er alle `"stat"`.** Ett felt med tre verdier kan ikke
skille fire typer som deler verdi. Motsatt er «tingrett vs. lagmannsrett» et nivåskille — men i
domstolshierarkiet, ikke i forvaltningen, og det er en annen akse igjen.

Det gir også et lite, ekte problem verdt å nevne: **domstolene er ikke forvaltning.** Å sette
`Forvaltningsniva = "stat"` på en tingrett er en unøyaktighet, og «ingen gjettet fallback» tilsier at
feltet da skal være `NULL` og `Organisasjonstype` bære sannheten. Om vokabularet i stedet bør utvides
med en verdi `domstol` er et spørsmål til Johann (§9).

**Kodeliste eller `CHECK`-constraint?** [ANBEFALT] `CHECK`-constraint på en streng, ikke
`KodelisteEntitet`:

- `KodelisteEntitet` (`Entiteter.cs:516-541`) er et *domene*-artefakt: den har status, versjonering,
  `JuridiskGrunnlagEid`, gyldighetsperioder og et eget publiseringsløp, fordi den modellerer
  verdidomener brukt i vilkår (`KL-VANDELSOMRADE-ALKOHOLLOV`). Organisasjonstype er derimot et
  strukturelt systemfelt uten juridisk hjemmel og uten redaksjonelt livsløp. Å blande lagene ville
  gjort typelisten til noe en jurist må publisere.
- Verdisettet er lite, stabilt og strukturelt. `CHECK`-constraint på streng er det etablerte mønsteret
  for nøyaktig det: `RegelIdeDbContext.cs:126-127` (`importrolle`), `:510` (kodelister `type`), `:656`
  (regelnoder), `:702` (unntak), `:732` (vilkårstre-kommentarer).
- Bonus: constraint-en er også en mulighet til å rette dagens mangel — `forvaltningsniva` har ingen
  `CHECK` i dag (§2.1), og bør få én i samme migrasjon.

**Fluktvei, hvis Johann vil kunne legge til typer uten migrasjon:** kopier
`TaggKindKonfigurasjonEntitet`-mønsteret (`Program.cs:209-217`) — en liten, global, ikke
virksomhet-scopet tabell (`kode`, `navn`, `sorteringsrekkefolge`), seedet i oppstart, med FK fra
`Virksomhet`. Det er ~30 linjer mer og gir en redigerbar liste. Jeg anbefaler det **ikke** nå, fordi
verdisettet endres omtrent når Norge omorganiserer domstoler eller kommunestruktur, dvs. sjelden nok
at en migrasjon er riktig kostnad. Men det er den rette utvidelsen den dagen listen skal kunne
redigeres i UI.

---

## 4. Spørsmål 2 — hvordan modelleres «357 kommuner er instanser av typen Kommune»?

**[ANBEFALT] Ingen ny entitet. `Virksomhet`-tabellen ER riktig form. Det som mangler er (a) ekte
seed-data og (b) nøyaktig ett nytt felt — `Organisasjonstype` fra §3.**

En egen `Organisasjonstype`-/`Forvaltningstype`-entitet ville blitt en oppslagstabell med seks rader
som ikke inneholder annet enn et navn, og hvis eneste funksjon er å være mål for en FK. Det er skjema
for skjemaets skyld, og bryter §0.1. Typen har ingen egne attributter, ingen livssyklus, ingen
proveniens og ingen redaktør — den *er* en enum.

Det gjør «Bergen kommune er én av 357 instanser av typen Kommune» til nøyaktig dette:

```
virksomheter
  navn                = "Bergen kommune"
  organisasjonsnummer = "964338531"       -- den stabile nøkkelen [LÅST, docs/15 §3.3]
  organisasjonstype   = "kommune"         -- NYTT: hvilken type-instans dette er
  forvaltningsniva    = "kommune"         -- uendret, grovere akse
  kommunenummer       = "4601"            -- geografisk attributt, aldri nøkkel [LÅST]
```

…ganget med 357 rader. «Å jobbe på samlenivået» blir da et helt vanlig oppslag
(`WHERE organisasjonstype = 'kommune'`), ikke en ny abstraksjon.

Fire konkrete konsekvenser som må håndteres i den runden seedingen faktisk bygges — alle verifisert,
ingen av dem blokkerende:

1. **Seed-guardene må byttes fra navn til organisasjonsnummer.** Dagens mønster er en global guard på
   navn: `BergenKorpusSeed.cs:72` (`AnyAsync(v => v.Navn == "Bergen kommune")`),
   `AgderFylkeskommuneSeed.cs:20`, `KommunaleParametreSeed.cs:22`. For 357 rader må guarden være
   **per rad** og på `Organisasjonsnummer` (som allerede har unik indeks,
   `RegelIdeDbContext.cs:102-103`), ellers blir seedingen enten alt-eller-intet eller ikke-idempotent.
2. **Eksisterende rader må ikke få gjettede numre.** Agder fylkeskommune er en ekte organisasjon og
   bør fylles fra seed-listen; Tønsberg og Bærum likeså. «Testkommunen» er *oppdiktet* og skal
   beholde `NULL` i både organisasjonsnummer og organisasjonstype — å gi den et konstruert
   organisasjonsnummer ville vært presis den gjettede fallbacken §0.1 forbyr. [PÅ AVKLARING i §9,
   fordi det angår data Johann eier.]
3. **Virksomhetsvelgerne blir uleselige ved 357 rader.** `/api/virksomheter` returnerer alle rader
   uten paginering eller filter (`Program.cs:309-313`), og to skjermer bruker den som nedtrekksliste
   (`DatasettDetalj.tsx:134-138`, `TjenesteVeiledning.tsx:158-161`). En 357-raders nedtrekksliste uten
   søk er i praksis ødelagt. Fiksen er liten (søkbart felt, evt. filter på organisasjonstype), men den
   må med i samme runde som seedingen — ellers gjør vi en fungerende skjerm dårligere.
4. **Globale seed-guarder på tittel misfyrer når flere kommuner har samme tjenestenavn.**
   `Byggesteg2InnholdSeed.cs:115` er `AnyAsync(t => t.Tittel == "Alminnelig skjenkebevilling")` — en
   *global* guard, kommentert «global guard». I det øyeblikket to kommuner har en tjeneste med samme
   tittel er den logikken feil. Må rettes til å være virksomhet-scopet før flere kommuner får
   tjenester.

Merk: **tallet 357 skal komme fra seed-listen, ikke fra oss.** Antall kommuner har endret seg flere
ganger det siste tiåret, og hverken jeg eller kodebasen er en autoritativ kilde på dagens tall. Det er
en av grunnene til at §7 spør etter en liste med proveniens og uttrekksdato.

---

## 5. Spørsmål 3 — master-tjeneste (hovedspørsmålet)

### 5.1 Først: hva skal «arver» bety?

**[ANBEFALT] (i) Ren kobling for sammenligning i første runde. Ekte feltarv med per-felt
overstyring er [UTENFOR SCOPE].**

Konkret betyr (i): instansraden peker på masterraden, og *ingenting* skjer automatisk med feltene.
Master-tittel og master-beskrivelse er kontekst og sammenligningsgrunnlag, ikke standardverdier som
skrives inn i instansen. Ved opprettelse *kan* UI-et tilby å forhåndsfylle skjemaet fra masteren, men
det er en kopi gjort av et menneske ved opprettelsen, ikke en levende arv.

Tre grunner, i stigende styrke:

1. **Prosjektets egen stil.** `docs/16` §9 formulerer normen: «Alle åtte er avgrensede, ikke
   big-bang». Samme mønster er brukt om og om igjen: `GeneriskMal` er «fritekst-kode … ingen egen
   registertabell i v1» (`Entiteter.cs:625`), `ErFormel` er «rent annoterende i v1»
   (`Entiteter.cs:644-651`), `Hendelse`/`Tjenesteavhengighet` ble spesifisert lenge før DTO/UI. En ren
   kobling er den samme avgrensningen.
2. **Ekte arv kolliderer med dagens skrivevei, målbart.** `OppdaterAsync`
   (`TjenesteregisterTjeneste.cs:129-140`) setter alle 13 CPSV-felt ubetinget fra `TjenesteRequest`
   (`Dtos.cs:143-146`). I en arvemodell må systemet kunne skille «dette feltet er tomt fordi det
   arves» fra «dette feltet er tomt fordi kommunen ikke har verdien» og fra «kommunen har bevisst
   overstyrt til noe annet». Det krever et per-felt overstyringsflagg — altså 13 ekstra kolonner eller
   en egen overstyringstabell — *pluss* at hele PUT-veien fra `TjenesteDetalj.tsx:198-217` gjennom
   `Program.cs:901-921` må lære forskjellen. Det er en betydelig ombygging av det mest brukte
   skjemaet i appen, til null gevinst for sammenligningsformålet.
3. **`docs/15` har allerede valgt propageringsmekanisme — og det er ikke overskriving.** 15:545-548
   om Finlands modell: «hent på nytt, diff per `TekstHash` …, **flagg** alle `Tjeneste`- og
   `Vilkår`-objekter hvis kildenoder endret seg». Flagging, ikke automatisk overskriving. En ren
   kobling er nøyaktig det diff-et trenger for å vite hvilke rader som skal flagges; feltarv er ikke
   nødvendig for det.

Det er også et rettslig moment: en kommunes tjenestebeskrivelse er kommunens eget ansvar. En
mekanisme som endrer 357 kommuners publiserte tjenestebeskrivelser fordi en nasjonal redaksjon
redigerte en mal, uten et menneske i loopen per kommune, er ikke opplagt forsvarlig — jf. RBAC-normen i
`docs/03` §2 (03:341) om at ingen kan endre en annen virksomhets entiteter. Dette er en av grunnene
til at (ii) fortjener en egen runde og ikke en fotnote i denne.

### 5.2 Alternativene, vurdert

| | Modell | Vurdering |
|---|---|---|
| **A** | Ny `MasterTjenesteEntitet` med eget delmengde-feltsett; `TjenesteEntitet.ArvetFraMasterTjenesteId` | Avvist. Dupliserer CPSV-feltsettet og alt maskineriet rundt: eget statusløp, egen versjonering, egen proveniens-entitetstype, egen CRUD-tjeneste, eget API, eget UI. Verst: `TjenesteRegelverksreferanseEntitet` har FK `TjenesteId` (`Entiteter.cs:385-391`) og kan ikke peke på en master — men det er nettopp masteren som skal bære de *felles* hjemlene (alkoholloven §4-1…§4-7), jf. 15:376-384 «rettskilden gir identitet». Man måtte da bygge en parallell referansetabell. Høy kostnad, ingen gevinst |
| **B** | Gjenbruk `TjenesteEntitet` for både master og instans, selvrefererende FK, master-rader eid av en syntetisk «samlenivå»-`Virksomhet` (én rad per type) | Nesten riktig, men den syntetiske virksomheten er en løgn i `virksomheter`-tabellen: «Kommune (samlenivå)» er ikke en organisasjon, har ikke organisasjonsnummer, og ville dukket opp i `/api/virksomheter` (`Program.cs:309-313`) og dermed i virksomhetsvelgerne som en falsk kommune. Den bryter også [LÅST] `docs/15` §3.3, der organisasjonsnummer er *den* identiteten en virksomhet har. Og ingen bruker tilhører den, så ingen kan redigere masteren under dagens RBAC |
| **C** | Gjenbruk `TjenesteEntitet`, men gjør master-radene **eierløse** (`VirksomhetId = NULL`) med en eksplisitt nivå-diskriminator og en type-referanse | **[ANBEFALT]** — se §5.3 |

Merk om selve koblingen: den skal være **et eget felt, ikke en `rel`-verdi i
`TjenesteavhengighetEntitet`**. Det følger direkte av [LÅST] 15:889-895, der `dekker`-relasjonen
ble holdt utenfor avhengighetsgrafen selv om begge ender er rader i samme tabell, fordi grafen har
bounded sykelsjekk (`TjenesteavhengighetregisterTjeneste.LukkerSykelAsync`) bygget for
«forutsetning for»-semantikk. «Arver fra» er en helt annen relasjon, og ville risikert falske
sykelavvisninger.

### 5.3 [ANBEFALT] Alternativ C i detalj

Master-tjenesten er en rad i `tjenester` uten eier, merket eksplisitt som samlenivå, og knyttet til
den organisasjonstypen den gjelder for:

- **`VirksomhetId` blir nullable**, og `NULL` betyr «samlenivå/felles» — presis samme semantikk som
  `NULL` allerede har på rettskilde, hendelse, kodeliste og parameterverdi (§2.3). Dette er ikke en ny
  konvensjon, det er den eksisterende, anvendt på én tabell mer.
- **Ny diskriminator `Tjenestenivaa`** (`"master" | "instans"`), non-nullable, ingen C#-default,
  backfill av alle eksisterende rader til `"instans"` — samme form som [LÅST] `Registertype`
  (15:850-857). Nivået leses altså *eksplisitt*, ikke utledet fra `VirksomhetId IS NULL`. Begrunnelse:
  en utledet sannhet er en gjettet sannhet, og en fremtidig `NULL`-eier av andre grunner (f.eks. en
  slettet virksomhet) ville ellers stille gjort en rad til master.
- **Ny `GjelderOrganisasjonstype`** — kun på master-rader, og det er *denne* som knytter masteren til
  taksonomien fra §3. En master for «Skjenkebevilling» gjelder `"kommune"`.
- **Ny `MasterTjenesteId`** — nullable selvrefererende FK, kun på instans-rader.

Det gir tre nye kolonner og én endret nullability på `tjenester`, ingen ny tabell, og null nye
entiteter. Merk hva den *ikke* trenger: eget statusløp (arvet), egen versjonering (arvet), egen
proveniens (`ProveniensHjelper.NyRad` tar allerede `Guid? virksomhetId`,
`ProveniensHjelper.cs:12-13`, og `EntitetType` er en fri streng — en master-rad får proveniens gratis
med `virksomhet_id = NULL`, akkurat som en nasjonal rettskilde), egne regelverksreferanser
(`TjenesteRegelverksreferanseEntitet` peker på `TjenesteId` og virker uendret — masteren kan bære
alkohollovens paragrafer, som er nettopp det 15:376-384 krever).

Tre akser som ikke må forveksles, siden `TjenesteEntitet` nå får sin tredje diskriminator:

| Felt | Svarer på | Status |
|---|---|---|
| `Tjenestetype` | Hva slags ting er dette? (Bevilling/Registrering/…) | Finnes, `Entiteter.cs:357` |
| `Registertype` | Hvilket register/regelsett hører raden til? (tjeneste/forvaltningsoppgave) | [LÅST], ikke kodet, `docs/15` §10.2 |
| `Tjenestenivaa` | På hvilket nivå er raden definert? (samlenivå/organisasjonsnivå) | Foreslått her |

### 5.4 Skjemaskisse

*Skisse i tekst, som grunnlag for beslutning. Ingen `.cs`-fil, ingen migrasjon er skrevet.*

```csharp
// Endring på TjenesteEntitet (Entiteter.cs:346-382):

// ENDRET: fra `required Guid` til nullable. NULL = master/samlenivå — samme betydning
// som på RettskildeEntitet (:65), HendelseEntitet (:404), KodelisteEntitet (:521) og
// DatasettVerdiEntitet (:598). Kommentaren på :350-351 må skrives om tilsvarende.
Guid?   VirksomhetId

// NYTT: eksplisitt nivå. Non-nullable, INGEN C#-default (nye rader må ta stilling) —
// samme form som [LÅST] Registertype, docs/15 §10.2. Migrering: backfill ALLE
// eksisterende rader til "instans" (korrekt per definisjon: enhver rad opprettet før
// dette feltet fantes er en konkret virksomhets tjeneste).
string  Tjenestenivaa            // "master" | "instans"

// NYTT: kun på master-rader — hvilken organisasjonstype masteren gjelder for.
// Verdisettet er det samme som Virksomhet.Organisasjonstype (§3).
string? GjelderOrganisasjonstype // "kommune" | "fylkeskommune" | ...

// NYTT: kun på instans-rader — ren kobling, ingen feltarv (§5.1).
Guid?   MasterTjenesteId         // FK -> tjenester.id
```

```sql
-- CHECK-constraints (mønster: RegelIdeDbContext.cs:126-129, :510, :656, :702)

ck_tjenester_tjenestenivaa
    tjenestenivaa IN ('master', 'instans')

ck_tjenester_nivaa_konsistens
    (tjenestenivaa = 'master'
        AND virksomhet_id IS NULL
        AND gjelder_organisasjonstype IS NOT NULL
        AND master_tjeneste_id IS NULL)
 OR (tjenestenivaa = 'instans'
        AND virksomhet_id IS NOT NULL
        AND gjelder_organisasjonstype IS NULL)

-- Indekser
ix_tjenester_master           ON tjenester (master_tjeneste_id) WHERE master_tjeneste_id IS NOT NULL
ix_tjenester_nivaa_type       ON tjenester (tjenestenivaa, gjelder_organisasjonstype)
-- Eksisterende ix_tjenester_virksomhet (RegelIdeDbContext.cs:392) beholdes uendret.
```

```csharp
// Endring på Virksomhet (Entiteter.cs:9-28):

string? Organisasjonstype   // "kommune" | "fylkeskommune" | "statsforvalter" |
                            // "tingrett" | "lagmannsrett" | "jordskifterett"
                            // Nullable: Testkommunen og andre ikke-ekte rader har ingen.
```

```sql
ck_virksomheter_organisasjonstype
    organisasjonstype IS NULL OR organisasjonstype IN
        ('kommune','fylkeskommune','statsforvalter','tingrett','lagmannsrett','jordskifterett')

-- Bør med i samme migrasjon, siden feltet i dag mangler constraint (§2.1):
ck_virksomheter_forvaltningsniva
    forvaltningsniva IS NULL OR forvaltningsniva IN ('stat','fylke','kommune')
```

**Bevisst utelatt: ingen unik indeks på `(master_tjeneste_id, virksomhet_id)`.** Én kommune skal kunne
ha *flere* tjenester knyttet til samme master. Det er ikke en hypotetisk mulighet — `docs/15` 15:378-380
dokumenterer den: «Oslo har **én** side som dekker minst ti rettslige bevillingsvarianter. Bergen har
**elleve** sider for det samme.» En unikhetsbeskrankning her ville gjort Bergens faktiske
tjenestestruktur ulovlig i databasen.

**To regler som ikke kan være `CHECK`-constraints** (de leser to rader) og derfor hører i
tjenestelaget, med `ArgumentException` som ellers i `TjenesteregisterTjeneste`:

- `MasterTjenesteId` må peke på en rad med `Tjenestenivaa = "master"` (ingen master av en master i
  denne runden — hierarkiske mastere er [UTENFOR SCOPE]).
- Masterens `GjelderOrganisasjonstype` må stemme med den eiende virksomhetens `Organisasjonstype` — en
  fylkeskommune skal ikke kunne knytte seg til en kommune-master. NB: dette forutsetter at
  virksomheten faktisk *har* en organisasjonstype, som i dag er `NULL` for alle
  (§2.1). Regelen kan derfor først håndheves etter seedingen; før det må den være en advarsel, ikke en
  avvisning. [PÅ AVKLARING i §9.]

### 5.5 Hvilke felt hører hjemme på masteren?

Feltdelingen er allerede besluttet i prinsipp, i `docs/15` 15:398-400:

> Skillet mellom lag 1 og 2 er ikke felttype, men **hvem som bestemmer verdien**: står den i lov eller
> forskrift, er den generell; bestemmer kommunen den, er den lokal.

Anvendt på det faktiske CPSV-feltsettet, med de virkelige verdiene fra
`Byggesteg2InnholdSeed.cs:117-130` («Alminnelig skjenkebevilling» for Testkommunen) som prøve:

| Felt | Nivå | Belegg fra den seedede raden |
|---|---|---|
| `Tittel` | Master | «Alminnelig skjenkebevilling» — samme i alle kommuner, følger av loven |
| `Beskrivelse` | Master | «…jf. alkoholloven kapittel 4» — ren lovgjengivelse |
| `Output` | Master | «Vedtak om skjenkebevilling» |
| `Tjenestetype` | Master | «Enkeltvedtak» |
| `Malgruppe` | Master | «Virksomheter som ønsker å skjenke alkoholholdig drikk» |
| `KonsekvensVedBrudd` | Master | «…jf. alkoholforskriften kapittel 10» |
| `Sprak` | Master | `["nb"]` (men kan overstyres lokalt) |
| Regelverksreferanser | Master | `§4-1`…`§4-7` på alkoholloven (`:132-134`) — identisk for alle |
| `KompetentMyndighet` | Instans | «Testkommunen» — per kommune, jf. `docs/15` 15:913 |
| `Kontaktpunkt` | Instans | «Testkommunens skjenkekontor» |
| `Kostnad` | Instans | «Bevillingsgebyr fastsatt av kommunestyret» — kommunen bestemmer |
| `Behandlingstid` | Instans | «Inntil 3 måneder» — kommunens eget servicenivå |
| `Kanaler` | Instans | «Digitalt søknadsskjema» — avhenger av kommunens systemer |
| Vilkårstre (`RotnodeId`) | Instans | Lokale parametre finnes allerede per kommune (`KommunaleParametreSeed.cs`) |

Poenget med tabellen er at feltsettet **ikke** trenger å deles i skjemaet. Alle 13 feltene finnes på
begge nivåer; delingen over er redaksjonell veiledning om hvor det er *meningsfullt* å fylle ut, og
den er samtidig det som gjør avviksvisningen i §6 mulig: for master-felt er avvik interessant, for
instans-felt er variasjon forventet. Det er også nøyaktig hvorfor alternativ A (eget delmengde-feltsett
på en egen entitet) er feil: delingen er en redaksjonell konvensjon, ikke en skjemaegenskap.

### 5.6 Konsekvenser som må håndteres i samme runde som skjemaet

Ikke valgfritt tilleggsarbeid — uten disse er master-rader en aktiv feilkilde.

1. **Strukturell sluse, ikke disiplin.** Direkte anvendelse av [LÅST] 15:880-887: `ListerForAsync`
   (`TjenesteregisterTjeneste.cs:19-23`) må aldri returnere master-rader i en virksomhets liste, og en
   fremtidig CPSV-eksport må ta eksplisitt stilling til dem. Krav: filteret ligger i tjenestelaget (én
   metode per sett — `ListerForAsync` for instanser, en ny `ListerMastereAsync` for samlenivå), pluss
   en regresjonstest som seeder en master-rad, kaller virksomhetens liste og asserterer at raden er
   **fraværende**. Merk at dagens predikat `t.VirksomhetId == virksomhetId` med `Guid?` faktisk
   utelukker `NULL` av seg selv — men det er en tilfeldighet, og en tilfeldighet er ikke en sluse.
2. **Den uscopede skrivetilgangen blir farligere.** `OppdaterAsync` og
   status-/rotnode-endepunktene filtrerer ikke på virksomhet (§2.2). I dag betyr det at bruker i
   kommune A kan endre kommune B sin tjeneste hvis hun har id-en — galt, men avgrenset. Med
   master-rader betyr det at hvem som helst kan endre malen 357 kommuner sammenlignes mot. Enten
   lukkes hullet i samme runde, eller master-rader gjøres read-only i API-et inntil det er lukket.
   [PÅ AVKLARING i §9: hvilken rolle *skal* eie mastere.]
3. **`TjenesteDto.VirksomhetId` blir nullable** (`Dtos.cs:130-140`), som slår gjennom i
   `types.ts` og i `TjenesterListe.tsx:154`s `visEier(t.virksomhetId)`
   (`useVirksomheter.ts:44-48`). Eier-kolonnen trenger en eksplisitt etikett for eierløse rader
   («Samlenivå» / «Felles mal»), ikke en tom celle — en tom celle leses som manglende data.
4. **`Registertype`-forholdet** ([LÅST], ikke kodet): når den bygges, må master-rader også ha en
   `Registertype`. En master for en forvaltningsoppgave er meningsfull, men den kombinasjonen bør
   avklares når begge feltene finnes, ikke antas nå.

---

## 6. Spørsmål 4 — hva betyr «konsolidering», og hva bygges nå?

**[ANBEFALT] Bare strukturen bygges nå: master-konseptet + koblingen + taksonomien. Alt
sammenlignings- og konsolideringsverktøy er en egen, senere runde.**

Johanns «etterhvert få en konsolidering» dekker minst tre ting med svært ulik kostnad. `docs/15`
§6.5 (15:532-541) er allerede konkret om hva premien er:

> - **Lovlighet.** Lokal forskrift som setter skjenketid til 03:30 er ulovlig. Flagges automatisk.
> - **Uteligger.** Behandlingstid 4 uker, 8 uker, 30 dager — reell variasjon eller datafeil?
> - **Hull.** 340 kommuner har side om ambulerende skjenkebevilling, én har ikke. […]

| Trinn | Hva | Anbefaling |
|---|---|---|
| 1 | **Struktur** — master-rad, kobling, organisasjonstype | Bygges nå (etter §9 og seed-listen). Alt annet forutsetter den |
| 2 | **Avviksvisning** — én master, N instanser side om side, felt for felt, ren lesevisning | Egen, senere runde. Billig når trinn 1 finnes: én spørring på `master_tjeneste_id` + en tabell. Ingen ny lagring |
| 3 | **Maskinelle kontroller** — lovlighet/uteligger/hull (15:532-541) | Senere. Krever at instansdataene faktisk er fylt ut for mange kommuner, og at feltene er sammenlignbare |
| 4 | **KI-forslag om at N kommuners tjenester bør slås sammen til én master** | **[UTENFOR SCOPE]** — byggesteg 5-aktig, og forutsetter trinn 2–3 som treningsgrunnlag |

To grunner til å være streng med avgrensningen, som ikke er stilistiske:

- **Det finnes ikke data å sammenligne ennå.** Bergen er den eneste virksomheten med reelt
  kildemateriale (`BergenKorpusSeed.cs`), og «Alminnelig skjenkebevilling» finnes som *én* rad, for
  Testkommunen (`Byggesteg2InnholdSeed.cs:114-130`). Et sammenligningsverktøy bygget nå ville hatt
  én kolonne. Verktøyet kan ikke testes meningsfullt før N ≥ 2 kommuner har ekte tjenesterader —
  altså etter fase 3 i `docs/15` §4.
- **Konsolidering er en redaksjonell handling, ikke en teknisk.** Å slå 357 varianter sammen til én
  mal er å ta stilling til hva som er lovpålagt likt og hva som er lovlig lokal variasjon. `docs/16`
  16:118-121 peker på nøyaktig det manglende leddet: det finnes ikke noe
  definisjonsmyndighet-felt, og «`VirksomhetId` sier hvem som *eier raden*, ikke hvem som er
  definisjonsmyndighet». Strukturen i §5 er en forutsetning for det arbeidet, ikke en erstatning for
  det.

Merk terminologi: «konsolidering» finnes ikke som begrep i dagens dokumentasjon i denne betydningen —
`docs/15` kaller det **normalisering** (15:571) og **kryssammenligning** (15:374, 15:579).
`Konsolidert`/`KonsolidertDato` er allerede et opptatt feltnavn på rettskilder (om konsolidert
lovtekst) og bør ikke gjenbrukes her. [PÅ AVKLARING i §9: hvilket ord vi lander på.]

---

## 7. Spørsmål 5 — seed-listen: eksakt format og felter

For at en fremtidig «seed alle organisasjoner»-jobb skal kunne kjøre uten å gjette, trengs én fil per
uttrekk. **CSV eller TSV med overskriftsrad, UTF-8** (JSON går like bra — CSV foreslås bare fordi den
kan komme rett ut av et register-uttrekk uten mellomledd).

| Kolonne | Påkrevd | Format | Merknad |
|---|---|---|---|
| `navn` | Ja | Fritekst | Offisielt navn, helst ordrett som i Enhetsregisteret, slik at raden kan verifiseres |
| `organisasjonsnummer` | **Ja** | 9 siffer, ingen mellomrom | Den stabile nøkkelen [LÅST, `docs/15` §3.3]. Har allerede unik indeks (`RegelIdeDbContext.cs:102-103`) |
| `organisasjonstype` | **Ja** | Én av `kommune`, `fylkeskommune`, `statsforvalter`, `tingrett`, `lagmannsrett`, `jordskifterett` | Verdisettet fra §3. Nye verdier krever migrasjon — meld dem i stedet for å improvisere |
| `kommunenummer` | Nei | 4 siffer | **Kun** for `organisasjonstype = kommune`. Rent geografisk/statistisk attributt, aldri nøkkel [LÅST]. Tomt for alle andre typer |
| `forvaltningsniva` | Nei | `stat` \| `fylke` \| `kommune` | Kan utledes av oss for kommune/fylkeskommune/statsforvalter. La den stå tom for domstolene — se §9 |

Regler jeg vil holde meg til under seeding, så det er sagt på forhånd:

- **Rader uten organisasjonsnummer avvises, ikke gjettes.** Mangler nummeret, hopper vi over raden og
  rapporterer den — samme mønster som `BergenKorpusSeed.cs:102`. Ingen konstruerte numre.
- **Ingen utledning av navn.** Vi slår ikke opp, korrigerer eller normaliserer navn; kommer det
  «Ålesund kommune» blir det «Ålesund kommune».
- **Idempotent per rad**, guardet på organisasjonsnummer (§4, punkt 1).
- **Ingen tjenester seedes** av denne jobben. Organisasjoner er organisasjoner; master-tjenester og
  instanser er separate, senere handlinger.

Tre ting jeg trenger *sammen med* filen, ikke i den:

1. **Kilde og uttrekksdato.** Bergens tall er dokumentert som «verifisert ekte tall, ikke gjettet:
   Brønnøysundregisterets Enhetsregister … (hentet 2026-08-14 via
   data.brreg.no/enhetsregisteret/api/enheter)» (`BergenKorpusSeed.cs:79-81`). Samme standard bør
   gjelde hele listen, og den bør stå som kommentar i seed-filen. Er domstolene hentet fra samme
   register eller fra Domstoladministrasjonen?
2. **Skal filen sjekkes inn i repoet?** Mønsteret for kildedata er `data/kilder/…` med en README som
   forklarer opphavet (jf. `data/kilder/raw-handbok/README.md`). Da blir seedingen reproduserbar for
   andre enn den som kjørte den. Alternativet er å holde filen utenfor og bare committe resultatet,
   som jeg vil unngå.
3. **Én rad for hver av de eksisterende, ekte virksomhetene** — Bergen kommune, Agder fylkeskommune,
   Tønsberg kommune, Bærum kommune — slik at de kan fylles ut i samme jobb i stedet for å stå igjen
   som halvferdige rader (§4, punkt 2). «Testkommunen» skal *ikke* være i listen.

---

## 8. [UTENFOR SCOPE] — bevisst utsatt

Ingen av punktene er avvist som ideer; de hører i egne runder, og nevnes her så avgrensningen er
eksplisitt fremfor underforstått.

- **Ekte feltarv med per-felt overstyring** (§5.1, alternativ (ii)). Krever per-felt
  overstyringsflagg og ombygging av hele PUT-veien.
- **Automatisk propagering av masterendringer til instansene.** `docs/15` 15:545-548 har allerede
  valgt «diff og flagg» fremfor overskriving; mekanismen bygges når endringsvarsling bygges, ikke nå.
- **KI-forslag om konsolidering** (§6, trinn 4).
- **Sammenlignings-/avviksskjerm** (§6, trinn 2) — billig, men egen runde, og trenger data først.
- **Hierarkiske mastere** (en nasjonal master over en fylkesvis master). `MasterTjenesteId` er
  begrenset til én nivåhopp av en tjenestelagsregel (§5.4). Behovet er ikke demonstrert.
- **Organisatorisk hierarki mellom virksomheter** (kommune → statsforvalter → departement).
  Johanns beskrivelse gjelder *typer* og *likhet*, ikke over-/underordning. Å legge til en
  `OverordnetVirksomhetId` nå ville vært skjema uten et navngitt bruksområde. Se §9.
- **Master for `Vilkår`/`Regelnode`/`Begrep`.** Samme resonnement gjelder åpenbart der (`GeneriskMal`,
  `Entiteter.cs:625`, er begynnelsen på det), men én entitet av gangen.
- **Å implementere `Forvaltningsniva`s dokumenterte hensikt** («styrer hvilket organ som er
  vedtaksmyndighet», `Entiteter.cs:25-26`). Feltet forblir uleste data etter denne runden også.
- **Admin-UI for å opprette/redigere virksomheter.** Finnes ikke i dag (§2.1); seeding dekker behovet
  her.

---

## 9. Åpne spørsmål — [PÅ AVKLARING], før koding starter

Analogt med ELI- og brukerveiledning-spørsmålene i forrige runde: dette er spørsmål jeg ikke kan
avgjøre alene, og som endrer skjemaet hvis svaret er et annet enn jeg antar.

- **Vil du at jeg begynner å bygge dette nå, eller venter på seed-listen?** Min anbefaling: bygg
  §3–§5 (skjema + constraints + tjenestelag) *før* listen kommer, siden de er uavhengige av
  innholdet, og hold seedingen som et eget, kort steg etterpå. Motargumentet er at et skjema uten
  data ikke kan verifiseres mot virkeligheten — 357 ekte rader kan avsløre en antakelse
  (f.eks. to organisasjoner med samme navn, eller en type som ikke passer). Din beslutning.
- **Er de seks typene fullstendige?** Nevnt: kommune, fylkeskommune, statsforvalter, tingrett,
  lagmannsrett, jordskifterett. Nærliggende kandidater vi kan trenge snart: direktorat, departement,
  helseforetak, interkommunalt samarbeid/vertskommune (som `docs/15` 15:292-296 alt peker på som
  kilden til `kompetentMyndighet`). Jeg vil helst ha listen komplett før constraint-en skrives, siden
  hver ny verdi ellers er en migrasjon.
- **Domstolene og `Forvaltningsniva`:** domstolene er ikke forvaltning. Skal `forvaltningsniva` være
  `NULL` for tingrett/lagmannsrett/jordskifterett (min anbefaling — «ingen gjettet fallback»), eller
  vil du utvide vokabularet med `domstol`?
- **Har en domstol «tjenester» i CPSV-forstand?** En jordskifterett har noe søkbart; en lagmannsretts
  ankebehandling er noe annet. Dette tangerer [LÅST] `Registertype` (`docs/15` §10.2, tjeneste vs.
  forvaltningsoppgave) — men en dømmende handling er kanskje ingen av de to. Verdt å vite før vi
  seeder domstoler og forventer tjenesterader under dem.
- **Hvem eier og kan endre en master-tjeneste?** `docs/03` §2 (03:341) sier at ingen kan endre en
  annen virksomhets entiteter, med delte/nasjonale rettskilder som unntak. En eierløs master har ingen
  virksomhet, så under dagens regler kan enten *ingen* eller *alle* redigere den. Mitt forslag som
  midlertidig løsning: samme regel som delte rettskilder (Jurist/Fagansvarlig, uansett virksomhet),
  og at en «nasjonal redaktør»-rolle er en egen, senere beslutning. Se også §5.6 punkt 2 — den
  uscopede skrivetilgangen bør lukkes i samme runde.
- **Skal Agder fylkeskommune, Tønsberg og Bærum fylles ut fra seed-listen?** De er ekte
  organisasjoner uten organisasjonsnummer i dag (§2.1). Min anbefaling: ja, i samme jobb.
  «Testkommunen» beholder `NULL` — den er oppdiktet og skal ikke få et konstruert nummer.
- **Ordvalg:** `Tjenestenivaa` med verdiene `master`/`instans`. Johann sa «master», og feltet bør
  bruke hans ord. Alternativer om vi vil ha norsk hele veien: `felles`/`lokal`, `mal`/`instans`,
  `samlenivaa`/`organisasjonsnivaa`. Samme spørsmål for §6: «konsolidering» eller `docs/15`s
  «normalisering»/«kryssammenligning»? Navnet blir stående i skjemaet, så det er verdt ett svar nå.
- **Trengs organisatorisk hierarki mellom virksomheter?** Jeg antar nei (§8). Hvis en kommune skal
  kunne knyttes til «sin» statsforvalter, sier det ifra nå — det er ett nullbart felt hvis det tas
  med fra starten, og en migrasjon pluss backfill hvis det kommer senere.

---

## 10. Kildegrunnlag

**Kode, verifisert mot `master` @ `a246f5e`:** `src/RegelIde.Data/Entiteter.cs` (`Virksomhet` 9-28,
`RettskildeEntitet.VirksomhetId` 65, `TjenesteEntitet` 346-382,
`TjenesteRegelverksreferanseEntitet` 385-391, `HendelseEntitet` 401-416, `KodelisteEntitet` 516-541,
`DatasettVerdiEntitet` 586-603, `VilkarEntitet.GeneriskMal` 625),
`src/RegelIde.Data/RegelIdeDbContext.cs` (`Virksomhet` 93-104, CHECK-mønstre 126-129/510/656/702/732,
`TjenesteEntitet` 360-393, datasett_verdier 593), `src/RegelIde.Data/TjenesteregisterTjeneste.cs`
(19-26, 31-66, 115-147), `src/RegelIde.Data/ProveniensHjelper.cs` (12-22),
`src/RegelIde.Data/Databaseoppsett.cs` (75-89), `src/RegelIde.Data/BergenKorpusSeed.cs` (72-91, 102),
`src/RegelIde.Data/AgderFylkeskommuneSeed.cs` (10-14, 20-22),
`src/RegelIde.Data/KommunaleParametreSeed.cs` (22, 29-44),
`src/RegelIde.Data/Byggesteg2InnholdSeed.cs` (111-134), `src/RegelIde.Api/Program.cs` (189-205,
209-217, 309-313, 858-921), `src/RegelIde.Api/Dtos.cs` (130-146),
`src/RegelIde.Api/GjeldendeBrukerTjeneste.cs` (43), `src/RegelIde.Web/src/pages/TjenesterListe.tsx`
(122-156), `src/RegelIde.Web/src/pages/TjenesteDetalj.tsx` (198-217, 405-429),
`src/RegelIde.Web/src/virksomhet/useVirksomheter.ts` (44-48).

**Dokumentasjon:** `docs/15-handbok-dokumentgraf-notat.md` §3.3 (298-315), §4 (353-374), §4s
identitetsregel (376-384), lag 1/lag 2-skillet (398-400), §6.5 (532-541), §6.6 (545-548), §7
(564-579), §10.2 (841-895). `docs/16-vurdering-rettskilde-til-tjenestebeskrivelse.md`
definisjonsmyndighet (118-121), parameterlaget (413), §9s prioriteringsnorm (761-762).
`docs/03-domenemodell.md` §0.1 (21-30), §1.5 (123-210), §1.11 (266-267), §2 (337-361).
`docs/02-produktkrav.md` §4.1 (238-241) — to-lags modell generisk vs. tjenestespesifikk, og
`digital-rettsstat` prinsipp 9 «modellér delte regler én gang», som er samme prinsipp dette notatet
anvender ett nivå opp (Tjeneste i stedet for Vilkår).

**Uverifiserte antakelser, for ordens skyld:**

- **Antallet 357.** Johanns tall, ikke sjekket mot noe register her, og antall kommuner har endret seg
  flere ganger. Skal komme fra seed-listen (§7). Samme for antall statsforvaltere, tingretter,
  lagmannsretter og jordskifteretter — jeg oppgir ingen tall for dem bevisst.
- **At CPSV-AP-NO ikke har et eget master-/malbegrep** som burde brukt et standardisert felt i stedet
  for `MasterTjenesteId`. Ikke sjekket mot spesifikasjonen. Bør sjekkes før feltet låses — hvis
  CPSV har et etablert begrep for «generell tjenestebeskrivelse», er dets navn å foretrekke.
- **At Finlands modell faktisk er master/instans i denne forstand.** Gjengitt fra `docs/15` §4/§7,
  ikke lest i primærkilden (lov 571/2016, DVVs FSC-veiledning).
- **Kostnadsanslaget for alternativ A vs. C.** Basert på lesing av kallkjedene, ikke på et forsøk.
- **At ingen av de tre parallelle branchene** under arbeid 2026-08-14 endrer feltene her. Sjekket på
  `master`, ikke i deres arbeidskopier.
