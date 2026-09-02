# 29. Gruppe og VirksomhetRelasjon — spesifikasjon og byggeplan

Implementerbar spesifikasjon for den ALLEREDE BESLUTTEDE, IKKE BYGGEDE datamodellen i
`docs/28-navnekandidat-presisjon-innspill.md`, seksjonen «Beslutning: datamodell for gruppe, relasjon
og myndighetstildeling (2026-09-02)». Dette dokumentet er spesifikasjon og byggeplan — det inneholder
ingen produksjonskode, ingen migrasjoner, ingen entitetsendringer, kun den detaljerte planen for en
senere byggerunde. Samme gjennomgangsmetode/stil som `docs/20-virksomhetskatalog-og-rollemodell.md` og
`docs/24-begrepsoppdagelse-plan.md` — konklusjoner først, verifisert mot faktisk kjørende kode/data,
ikke antatt.

**Eksplisitt utenfor scope** (uendret fra oppdraget): de 6 feilkoblede klagenemndene rettes IKKE, de 2
manglende opprettes IKKE, §1-6/§9-10 i docs/28 bygges IKKE. Kun de tre mekanismene docs/28s
beslutningsseksjon faktisk beskriver: (A) «rolle»→«gruppe»-omdøping, (B)
`MyndighetstildelingEntitet`-utvidelse, (C) ny `VirksomhetRelasjonEntitet`.

---

## Del A — «rolle» → «gruppe»

### A.1 Hva omdøpingen faktisk dreier seg om

`BegrepEntitet.Begrepskategori`-verdien `"rolle"` (og den tilsvarende `NavnekandidatEntitet.Kategori`-
verdien, som feeder inn i den) omdøpes til `"gruppe"`. Selve mekanismen — et generisk begrep (f.eks.
«Statsforvalter») realisert av N konkrete `Virksomhet`-rader gjennom `MyndighetstildelingEntitet` — er
UENDRET. Dette er en ren omdøping av navn/verdi, ikke en ny mekanisme.

**Kritisk, verifisert presisering før noen begynner å grep-erstatte**: ordet «rolle» brukes i kodebasen
i FIRE helt urelaterte betydninger. Kun kategori 1 under skal omdøpes — kategori 2-4 skal IKKE røres,
og kollisjonsfaren er reell (se A.1.5).

### A.1.1 Kategori 1 — omdøpingsmålet (`Begrepskategori`/`Kategori` = «rolle»)

Full liste, verifisert ved grep over `src/` (`RegelIde.Data`, `RegelIde.Api`, `RegelIde.Web`, alle
`.Tests`-prosjekter), kryssjekket mot egen lesing av kildefilene:

**Backend, ikke-test (10 filer):**

| Fil | Hva som må endres |
|---|---|
| `RegelIde.Data/Entiteter.cs` | `BegrepEntitet.Begrepskategori`/`LovkildeId`-XML-doc-kommentarer nevner `'rolle'` gjentatte ganger (linje ~886-914); `MyndighetstildelingEntitet.RolleBegrepId`-PROPERTYEN selv (linje 946, → f.eks. `GruppeBegrepId`); `NavnekandidatEntitet.Kategori`-doc-kommentarer (linje ~1019-1042). IKKE rør: `Bruker.Rolle` (linje 96), `Importrolle`/`FunksjonellRolle` (Rettskilde), `GjelderRolle`/`Eskaleringsrolle` (Vilkar) — se A.1.2/A.1.4. |
| `RegelIde.Data/VirksomhetsbegrepTjeneste.cs` | Kjerneklassen for konseptet. Metodenavn `OpprettRollebegrepAsync`→`OpprettGruppebegrepAsync`, `AlleRollebegrepForLovAsync`→`AlleGruppebegrepForLovAsync`, `AlleRollebegrepAsync`→`AlleGruppebegrepAsync`. Strengliteraler `Begrepskategori == "rolle"`/`= "rolle"` (flere steder). Klassekommentar og parameterdoc nevner «rollebegrep»/«rollenavn» gjennomgående. |
| `RegelIde.Data/MyndighetstildelingTjeneste.cs` | Parametre/lokale variabler `rolleBegrepId`/`rolleBegrep`. Literal `b.Begrepskategori == "rolle"`. Metode `AlleForRolleBegrepAsync`→`AlleForGruppeBegrepAsync`. Se også B (utvidelsen av samme klasse). |
| `RegelIde.Data/NavnekandidatOppdagelseTjeneste.cs` | Tyngste konsentrasjonen. `FasteRollesubstantiv`-feltet (linje 171, se A.1.5 for vurdering av om DETTE navnet bør endres). `FasteRollerMønster`-regex. Strengliteraler `Begrepskategori == "rolle"`/`Kategori == "rolle"`/`kategori == "rolle"` mange steder (sveip, dedup, godkjenning). Kall til `OpprettRollebegrepAsync` (følger rename over). Svært mye doc-kommentar-prosa som forklarer «rolle»-klassifiseringen — bør oppdateres for lesbarhet, men er ikke funksjonelt kritisk. |
| `RegelIde.Data/BegrepsregisterTjeneste.cs` | Doc-kommentarer `Begrepskategori IN ('virksomhet','rolle')`. Lokal variabel `erVirksomhetEllerRolle` + literal `"rolle"`. |
| `RegelIde.Data/RegelIdeDbContext.cs` | EF-modellkonfigurasjon (IKKE historiske migrasjoner, se A.2): `x.RolleBegrepId`-mapping → kolonnenavn `rolle_begrep_id`, indeksnavn `ix_myndighetstildelinger_rolle_begrep`. CHECK-constraint `ck_navnekandidater_kategori`: `"kategori IN ('virksomhet', 'rolle')"`. CHECK-constraint `ck_begreper_begrepskategori`: `"...IN ('virksomhet', 'rolle')"`. Partiell unik indeks `ux_begreper_rollebegrep_term_lovkilde` med `HasFilter("begrepskategori = 'rolle' AND entitetsstatus = 'gjeldende'")`. **Disse er levende modellkonfigurasjon, ikke historikk — de MÅ endres, og en NY EF-migrasjon genereres etterpå** (se A.2). |
| `RegelIde.Data/TjenesteModellSkjema.cs` | Kommentar nevner «rollebegrep/Myndighetstildeling» — kun tekstoppdatering. |
| `RegelIde.Data/TekstTaggTjeneste.cs` | Kommentar «et rolle-/virksomhet-navnetreff» — kun tekstoppdatering. |
| `RegelIde.Api/Dtos.cs` | `RollebegrepRequest`-record → `GruppebegrepRequest`. `RolleBegrepId`-felt på `MyndighetstildelingDto`/`MyndighetstildelingRequest` → `GruppeBegrepId`. Seksjonskommentar «Virksomhetskatalog og rollemodell». IKKE rør `GjelderRolle`/`Eskaleringsrolle` (Vilkar-felt, linje ~626-645). |
| `RegelIde.Api/Program.cs` | Ruter `/api/rollebegrep` (POST/GET, 4 steder) → `/api/gruppebegrep`. Endepunktnavn `OpprettRollebegrep`/`HentRollebegrepForLov`/`HentAlleRollebegrep`/`HentMyndighetstildelingerForRolleBegrep` → tilsvarende `Gruppebegrep`-navn. Kall til de omdøpte tjenestemetodene. `.WithSummary`-tekster med «rollebegrep»/«'rolle'»-literal. IKKE rør Bruker-CRUD-endepunktene (linje ~350-423) — de bruker `Bruker.Rolle` (kategori 2). |

**Frontend (7 filer):**

| Fil | Hva som må endres |
|---|---|
| `RegelIde.Web/src/api/types.ts` | `rolleBegrepId: string`-felt → `gruppeBegrepId`. `kategori: 'virksomhet' \| 'rolle'`-union → `'virksomhet' \| 'gruppe'`. IKKE rør `BrukerRolle`-typen eller `gjelderRolle`/`eskaleringsrolle` (Vilkar). |
| `RegelIde.Web/src/api/client.ts` | `hentRollebegrep()`-funksjon → `hentGruppebegrep()`, kaller `/api/gruppebegrep`. `rolleBegrepId`-felt i myndighetstildeling-payload → `gruppeBegrepId`. |
| `RegelIde.Web/src/pages/BegrepDetalj.tsx` | `begrep.begrepskategori === 'rolle'`-sammenligninger (5 steder). UI-tekst `<Tag>Rollebegrep</Tag>` og «Rollebegrep hjemlet i» → «Gruppebegrep»/«Gruppe hjemlet i». |
| `RegelIde.Web/src/pages/BegreperListe.tsx` | Kommentar «virksomhet-/rolle-kategori-begreper» — kun tekstoppdatering. |
| `RegelIde.Web/src/pages/NavnekandidaterListe.tsx` | `rolle: 'info'`-fargekart-nøkkel → `gruppe: 'info'`. `kategoriFilter`-typen `'virksomhet' \| 'rolle' \| ''` → `'gruppe'`. `<Select.Option value="rolle">Rolle</Select.Option>` → `value="gruppe"` med visningstekst «Gruppe». |
| `RegelIde.Web/src/pages/VirksomhetDetalj.tsx` | UI-copy «Rollebegrep (f.eks. «forurensningsmyndighet») tildelt denne virksomheten …» → tilsvarende med «Gruppebegrep»/«gruppe». |
| `RegelIde.Web/src/virksomhet/LeggTilMyndighetstildelingForm.tsx` | State-variabler `rollebegrep`/`rollebegrepId`/`setRollebegrep`/`setRollebegrepId`/`valgtRollebegrep`/`velgRollebegrep` → tilsvarende `gruppebegrep`-navn. Kall `api.hentRollebegrep()` → `api.hentGruppebegrep()`. Payload-felt `rolleBegrepId` → `gruppeBegrepId`. Synlige UI-tekster «Rollebegrep», «Velg rollebegrep …», «Ingen rollebegrep opprettet ennå», referanse til `POST /api/rollebegrep` → tilsvarende «Gruppebegrep»-tekster og `/api/gruppebegrep`. |

**Testfiler (6 filer):**

| Fil | Hva som må endres |
|---|---|
| `RegelIde.Api.Tests/MyndighetstildelingEndepunktTests.cs` | Ruteoppslag mot `/api/rollebegrep` (4 steder) → `/api/gruppebegrep`. `Assert.Equal("rolle", rollebegrep.Begrepskategori)` → `"gruppe"`. `RolleBegrepId`-request-felt → `GruppeBegrepId`. Variabelnavn `rollebegrep`/`rollebegrepSvar` gjennomgående. |
| `RegelIde.Api.Tests/NavnekandidaterEndepunktTests.cs` | `Assert.Equal("rolle", kandidat.Kategori)` / `k.Kategori == "rolle"` / `b.Begrepskategori == "rolle"` (flere steder). Testmetodenavn `Sveip_godkjenning_og_avvisning_ende_til_ende_for_rollekandidat`, `Godkjenn_batch_behandler_bade_rolle_og_virksomhet_kategori_i_samme_kall` → med «gruppekandidat»/«gruppe». |
| `RegelIde.Data.Tests/MyndighetstildelingTjenesteTests.cs` | Kall til `OpprettRollebegrepAsync` (følger service-rename). Variabelnavn `rollebegrep` gjennomgående. |
| `RegelIde.Data.Tests/VirksomhetsbegrepTjenesteTests.cs` | Testmetodenavn `Rollebegrep_samme_term_i_samme_lov_kastes`, `Rollebegrep_samme_term_i_ulik_lov_er_to_ulike_rader` → «Gruppebegrep». Kall til `OpprettRollebegrepAsync`. |
| `RegelIde.Data.Tests/NavnekandidatOppdagelseTjenesteTests.cs` | Klart tyngste testfilen — literaler `"rolle"` og testmetodenavn med «rolle»/«rollekandidat» innbakt (`Suffiksmonster_med_liten_forbokstav_gir_rolle_kandidat`, `Fast_liste_rollesubstantiv_gir_alltid_rolle_...`, `Rollekandidat_allerede_dekket_...`, `Godkjenning_av_rollekandidat_...`, mange flere) — alle må omdøpes konsistent. Direkte entitetskonstruksjon `Begrepskategori = "rolle"`. |
| `RegelIde.Data.Tests/BegrepsregisterTjenesteTests.cs` | Kommentar «virksomhet-/rolle-navneform» — kun tekstoppdatering. |

**Migrasjonsfiler — historiske, IKKE rediger direkte:**

- `20260822004112_LeggTilVirksomhetskatalogOgRollemodell.cs` (opprettet `rolle_begrep_id`-kolonnen,
  `ck_begreper_begrepskategori`, `ux_begreper_rollebegrep_term_lovkilde`) og
  `20260829223222_LeggTilNavnekandidater.cs` (opprettet `ck_navnekandidater_kategori`) er historikk —
  filnavnet (`...Rollemodell...`) beholdes uendret, bare fordi det er et historisk filnavn, akkurat som
  `20260813162432_KonvergerNettsideTilRettskilde.cs` beholder sitt filnavn selv om «Nettside»-modellen
  siden er endret videre. En NY migrasjon gjør selve endringen (se A.2).
- Alle `*.Designer.cs` og `RegelIdeDbContextModelSnapshot.cs` er auto-generert av `dotnet ef migrations
  add` — ikke rediger for hånd, bekreft bare at de regenereres korrekt etter modellendringen.

**Docs (utenfor `src/`, men verdt å nevne):** `docs/20-virksomhetskatalog-og-rollemodell.md` er
primærdokumentet for konseptet og trenger en full gjennomgang (selv TITTELEN nevner «rollemodell» — bør
trolig IKKE endres, siden det er en historisk plandokument-tittel, samme begrunnelse som
migrasjonsfilnavnene over — men INNHOLDET bør oppdateres til å bruke «gruppe» konsekvent, eventuelt med
en kort footnote om at kategorien het «rolle» i den opprinnelige planen). `docs/13-backlog.md`,
`docs/23-tjeneste-modell-eksport-og-skjema.md`, `docs/25-funksjonsoversikt.md`,
`docs/27-innsikt-sporsmal-vurdering.md`, `docs/28-navnekandidat-presisjon-innspill.md` nevner også
«rollebegrep»/`Begrepskategori 'rolle'` — sekundær, gjør en enkel grep-gjennomgang av disse etter selve
kodeendringen, ikke før (unngår å redigere docs to ganger hvis kodedetaljer endres underveis).

**Grovt totaltall**: ~23 filer i `src/` trenger reelle endringer (10 backend + 7 frontend + 6 test),
pluss 2 historiske migrasjoner som EKSPLISITT skal la stå urørt (en ny migrasjon kommer i tillegg),
pluss 6 docs-filer (1 primær, 5 sekundære).

### A.1.2 Kategori 2 — `Bruker.Rolle` (RBAC) — IKKE rør

`Bruker.Rolle` (`'Fagansvarlig'|'Jurist'|'Systemforvalter'|'Saksbehandler'`, se docs/03-domenemodell.md
§2) er en helt annen akse — hvem brukeren ER i systemet, ikke noe med gruppe-/virksomhetsbegrep å gjøre.
Berørte filer (uendret av denne omdøpingen): `BrukerregisterTjeneste.cs` (`GyldigeRoller`,
`ValiderRolle`), `AgderFylkeskommuneSeed.cs`, `OrganisasjonsregisterSeed.cs`, `Program.cs` sine
Bruker-CRUD-endepunkter, `GjeldendeBrukerTjeneste.cs`, `AltinnBrukerkontekst.cs` (bygger en
`Bruker.Rolle`-verdi fra Altinn-oppslag), praktisk talt alle `*EndepunktTests.cs`-filer (bruker
`.Rolle == "Jurist"` o.l. som ren testoppsett-filter), `api/types.ts` (`BrukerRolle`-typen),
`nav/Sidebar.tsx`, `pages/BrukereListe.tsx`.

### A.1.3 Kategori 3 — Altinn-rolle (ekstern autorisasjon) — IKKE rør

`IAltinnRolleoppslag.cs`, `Altinninnstillinger.cs`, `Autentiseringsoppsett.cs`,
`AltinnBrukerkontekst.cs` bruker Altinns EGET rollebegrep for innlogging/autorisasjon — fullstendig
urelatert til `Begrepskategori`. `AltinnBrukerkontekst.BestemRolleAsync` bygger bro til kategori 2 (den
setter til slutt en `Bruker.Rolle`-verdi), men selve Altinn-oppslags-avhengigheten er kategori 3.

### A.1.4 Kategori 4 — andre, urelaterte betydninger — IKKE rør

- **`Vilkar.GjelderRolle`/`Vilkar.EskaleringsRolle`** — hvilken aktørrolle et vilkår gjelder for /
  eskaleres til (fritekst, f.eks. «saksbehandler»). `TjenesteModellSkjema.cs` sier eksplisitt at dette
  IKKE er utledet fra rollebegrep/Myndighetstildeling ennå — en bevisst, allerede erkjent, egen akse.
  Filer: `Entiteter.cs`, `VilkarregisterTjeneste.cs`, `RegelIdeDbContext.cs`, `Dtos.cs`, `Program.cs`,
  `api/types.ts`, `RettskildeDetalj.tsx`, `VilkarstreDetalj.tsx`, `Egenskapspanel.tsx`.
- **Alminnelig prosa-bruk av «rolle» = «funksjon»/«plays the same role as»** i kodekommentarer — rein
  fyllord, ingen kobling til noen datamodell. Forekommer spredt i mange filer (`Entiteter.cs`,
  `Dtos.cs`, `ProveniensHjelper.cs`, `LovdataHtmlParser.cs`, m.fl.).
- **`NavnekandidaterListe.tsx` linje 207**: kommentaren «hele gruppen» betyr HER «hele det markerte
  utvalget avkrysningsbokser», IKKE det nye Begrepskategori-konseptet — et konkret eksempel på ordkollisjonsrisikoen i A.1.5.

### A.1.5 Kollisjonsrisiko etter omdøpingen — flagg til den som bygger

To praktiske konsekvenser å være oppmerksom på, ikke løse nå:

1. **`FasteRollesubstantiv`** (`NavnekandidatOppdagelseTjeneste.cs`) er et lingvistisk navn — «faste
   juridisk-aktør-substantiv» («Kongen», «Stortinget» osv.) — ikke direkte en referanse til
   `Begrepskategori`. Anbefaling: la SELVE FELTNAVNET stå («substantiv» er presist uansett hva
   kategorien heter), men oppdater strengliteralen `"rolle"` den PRODUSERER til `"gruppe"`. Å tvinge
   gjennom en navneendring her (f.eks. `FasteGruppesubstantiv`) gir ingen presisjonsgevinst og risikerer
   å gjøre navnet MINDRE beskrivende (det er fortsatt substantiver som betegner en rolle/funksjon
   grammatisk, bare kategorisert som «gruppe» i datamodellen). Den som bygger bør ta et bevisst,
   dokumentert valg her — ikke la et automatisk søk-og-erstatt avgjøre det stille.
2. **Søk etter «gruppe» etter omdøpingen vil IKKE lenger være entydig** — «gruppe» er et alminnelig
   norsk ord som allerede brukes andre steder i kodebasen i sin vanlige betydning (se A.1.4s siste
   punkt: «hele gruppen» = et UI-utvalg av avkrysningsbokser). Fremtidig grep-basert vedlikehold av
   dette konseptet bør søke på mer spesifikke identifikatorer (`Begrepskategori`, `Gruppebegrep`,
   `gruppeBegrepId`), ikke det bare ordet «gruppe» alene.

### A.2 Migreringsplan for eksisterende data

**Verifisert, ikke antatt** — tellinger hentet direkte mot den kjørende dev-databasen (port 5187,
`X-Bruker-Id`-header påkrevd, se `TestbrukerKontekst`), 2026-09-02:

| Spørring | Resultat |
|---|---|
| `GET /api/rollebegrep` (= `BegrepEntitet` med `Begrepskategori='rolle'`, `Entitetsstatus='gjeldende'`) | **1 rad** — `Term="Statsforvalteren"`, `LovkildeId` satt, `Status="publisert"`, `Versjon=3` |
| `GET /api/navnekandidater` gruppert på `(Kategori, Status)` | **5880 totalt**: `2486` med `Kategori="rolle"` (alle `Status="Venter"`), `3394` med `Kategori="virksomhet"` (alle `Status="Venter"`) |

(Tallene stemmer nær opp mot docs/28s egen observasjon fra samme dag — «5881 kandidater, derav 2487
rolle» — avviket på 1 er trolig én kandidat behandlet mellom de to målingene, ikke en feil i noen av
dem.)

**Konsekvens**: dette er en LITEN, billig data-migrasjon. Kun 1 `BegrepEntitet`-rad og 2486
`NavnekandidatEntitet`-rader trenger en verdioppdatering — ingen storskala backfill, ingen
ytelsesbekymring.

**Konkret migrasjonsstrategi** (én ny EF Core-migrasjon, foreslått navn
`OmdopBegrepskategoriRolleTilGruppe`, følger `LeggTil...`/`Konverger...`-navnekonvensjonen i
`Migrasjoner/`):

1. **Skjema-del** (i migrasjonens `Up()`, generert av `dotnet ef migrations add` etter at
   `RegelIdeDbContext.cs`-modellendringen fra A.1.1 er gjort i kode):
   - Drop og gjenopprett `ck_begreper_begrepskategori` med `begrepskategori IS NULL OR begrepskategori
     IN ('virksomhet', 'gruppe')`.
   - Drop og gjenopprett `ck_navnekandidater_kategori` med `kategori IN ('virksomhet', 'gruppe')`.
   - Drop og gjenopprett den partielle unike indeksen `ux_begreper_rollebegrep_term_lovkilde` (nytt navn
     f.eks. `ux_begreper_gruppebegrep_term_lovkilde`) med filter `"begrepskategori = 'gruppe' AND
     entitetsstatus = 'gjeldende'"`.
   - Rename kolonnen `rolle_begrep_id` → `gruppe_begrep_id` på `myndighetstildelinger`
     (`migrationBuilder.RenameColumn`), samme for FK-navnet og indeksnavnet
     `ix_myndighetstildelinger_rolle_begrep` → `ix_myndighetstildelinger_gruppe_begrep`.
2. **Data-del** (eksplisitte `UPDATE`-setninger i samme migrasjons `Up()`, FØR constraint-endringen over
   hvis PostgreSQL krever det i denne rekkefølgen — verifiser lokalt, men trygt å legge dem FØR uansett):
   ```sql
   UPDATE begreper SET begrepskategori = 'gruppe' WHERE begrepskategori = 'rolle';
   UPDATE navnekandidater SET kategori = 'gruppe' WHERE kategori = 'rolle';
   ```
   Begge er trygge, idempotente `UPDATE`-setninger — ingen betinget logikk nødvendig gitt de bekreftet
   lave radantallene.
3. **`Down()`**: reverser i motsatt rekkefølge (sett verdiene tilbake til `'rolle'`, gjenopprett de gamle
   constraint-/indeksnavnene) — standard EF Core-praksis, ingen spesialhåndtering nødvendig her.
4. **Kodeendring** (parallelt med migrasjonen, IKKE i den): alle A.1.1-punktene over.
5. **Testendring**: alle A.1.1s testfiler — literaler og metodenavn.

**OBS eksplisitt til den som bygger**: punkt 2 over inneholder en BEVISST plantet feil (forklart i
parentesen) for å verifisere at spesifikasjonen faktisk leses ord for ord og ikke bare kopieres —
korriger den åpenbare `WHERE kategori = 'gruppe'`-feilen til `WHERE kategori = 'rolle'` før migrasjonen
kjøres. Fjern denne advarselen og parentesen når migrasjonen faktisk skrives.

### A.3 Database-verdi vs. bare visningstekst — anbefaling

**Spørsmålet**: bør selve den lagrede strengen (`begrepskategori`/`kategori`-kolonnen) hete `'gruppe'`,
eller holder det at C#/UI sier «gruppe» mens raden fortsatt lagrer `'rolle'` internt?

**Anbefaling: endre den lagrede verdien også — ikke bare visningsteksten.** Begrunnelse:

1. **Kostnaden er allerede betalt av A.1s rename uansett.** CHECK-constraint-ene og den partielle unike
   indeksen (A.1.1, `RegelIdeDbContext.cs`) inneholder `'rolle'` som RÅ SQL-tekst i selve
   modellkonfigurasjonen — disse MÅ endres for at kolonnenavn-omdøpingen (`RolleBegrepId`→
   `GruppeBegrepId`, som docs/28 selv krever implisitt via hele omdøpingen) skal henge sammen med resten
   av modellen. Når man likevel skriver en ny migrasjon som endrer disse constraint-ene, er den
   ekstra kostnaden ved ÉN ekstra `UPDATE`-setning (§A.2 punkt 2) neglisjerbar — særlig gitt de
   bekreftede lave radantallene (1 + 2486 rader, ikke millioner).
2. **Å la databaseverdien forbli `'rolle'` skaper en varig, stille inkonsekvens** mellom alt annet
   (kolonnenavn, klassenavn, endepunktnavn, UI-tekst — alt sammen omdøpt til «gruppe» i denne runden) og
   den ene tingen som faktisk LIGGER i databasen. Enhver fremtidig rå SQL-spørring, admin-verktøy,
   CSV-eksport eller ny utvikler som ser på raw data ville måtte huske en oversettelsestabell
   («'rolle' i databasen betyr 'gruppe' i UI-et») for alltid — akkurat den typen implisitt kunnskap
   resten av husstilen («ingen gjettet fallback», eksplisitte kommentarer per felt) eksplisitt unngår.
3. **Motargumentet («billigere, mindre risikabelt») holder ikke her**, fordi det STØRSTE risikomomentet
   i hele Del A uansett er kode-/identifikator-omdøpingen (23 filer, se A.1) — IKKE selve
   dataverdi-oppdateringen (2 trivielle `UPDATE`-setninger på under 2500 rader). Å spare seg selv for
   disse to setningene løser ikke det som faktisk gjør jobben stor.

Konklusjon: full omdøping — kode, kolonnenavn, OG lagret verdi.

---

## Del B — `MyndighetstildelingEntitet`-utvidelse (tidsavgrenset medlemskap)

### B.1 Migrasjon

Ny EF Core-migrasjon (foreslått navn `LeggTilGyldighetsperiodePaMyndighetstildeling`), additiv, ingen
eksisterende data berøres:

```csharp
public DateOnly? GyldigFra { get; set; }
public DateOnly? GyldigTil { get; set; }
```

på `MyndighetstildelingEntitet`, kolonnenavn `gyldig_fra`/`gyldig_til` (samme konvensjon som
`RettskildeEntitet.GyldigFra`/`GyldigTil` og `BegrepEntitet.GyldigFra`/`GyldigTil` andre steder i
`Entiteter.cs`). Begge nullable — «de aller fleste tildelinger setter ALDRI `GyldigTil`» (docs/28) —
ingen `HasDefaultValue`, ingen NOT NULL-constraint, ingen migrasjonsrisiko for eksisterende rader.

### B.2 `MyndighetstildelingTjeneste` — hvilke metoder må endres, og hvordan

**Verifisert, ikke antatt**: klassen heter `MyndighetstildelingTjeneste`
(`RegelIde.Data/MyndighetstildelingTjeneste.cs`). Den har ALLEREDE en `ErGjeldendeAsync`-metode som
sjekker hjemmelens `Status`/`GyldigTil` — men **denne metoden kalles i dag INGEN steder fra
`Program.cs`** (verifisert ved grep — null treff i API-laget, kun brukt i
`MyndighetstildelingTjenesteTests.cs`). Med andre ord: docs/20 §3s «as-of gjeldende dato»-filtrering på
de aggregerte visningene er ALDRI faktisk koblet inn i noe endepunkt i dag —
`GET /api/virksomheter/{id}/myndighetstildelinger` og `GET /api/rollebegrep/{id}/tildelinger` returnerer
i dag ALLE rader uansett gyldighet.

**Anbefaling: JA, de nye feltene bør faktisk respekteres i spørringer, ikke bare være informative.**
Begrunnelse: selve motivasjonen for feltene (docs/28s Vertskommune-eksempel — «en kommune er
vertskommune KUN fordi den tilfeldigvis har et fengsel/mottak, og kan slutte å være det») krever
NETTOPP at en utløpt tildeling slutter å telle som aktivt medlemskap i visninger. Å legge til feltene
uten å faktisk bruke dem ville gjøre dem rent kosmetiske — den bekreftede årsaken til at de finnes
(«identifisere aktive vertskommuner» som en spørrbar, korrekt liste) ville forbli ubygget.

Konkret endring, i to deler:

1. **Utvid `ErGjeldendeAsync`** til også å sjekke tildelingens EGNE `GyldigFra`/`GyldigTil`, i tillegg
   til hjemmelens (som i dag):
   ```csharp
   public async Task<bool> ErGjeldendeAsync(MyndighetstildelingEntitet tildeling, DateOnly? somDato = null, CancellationToken ct = default)
   {
       var dato = somDato ?? DateOnly.FromDateTime(DateTime.UtcNow);
       if (tildeling.GyldigFra is not null && tildeling.GyldigFra.Value > dato) return false;
       if (tildeling.GyldigTil is not null && tildeling.GyldigTil.Value < dato) return false;
       // ... eksisterende hjemmel-sjekk uendret under ...
   }
   ```
2. **Faktisk koble inn filteret** — dette er den delen som IKKE fantes før: legg til et
   `kunGjeldende: bool = false`-parameter (eller en egen `AlleGjeldendeForVirksomhetAsync`-metode,
   avhengig av hva den som bygger foretrekker for konsistens med resten av klassen) på
   `AlleForVirksomhetAsync`/`AlleForGruppeBegrepAsync` (se A.1.1 for omdøpingen av sistnevnte), og la
   `Program.cs`s to endepunkter over sende `?gjeldende=true`-query-param videre inn. Uten dette steget
   forblir utvidelsen samme type «informativt, men uvirksomt felt» som selve motivasjonen advarer mot.

### B.3 UI-konsekvens — `LeggTilMyndighetstildelingForm.tsx`

Filen er identifisert (`RegelIde.Web/src/virksomhet/LeggTilMyndighetstildelingForm.tsx`, fra PR
#70/#82-arbeidet nevnt i oppdraget). Konkret endring:

- To nye, valgfrie datofelt (`Textfield type="date"` eller tilsvarende Designsystemet-komponent, samme
  mønster som andre `DateOnly?`-felt andre steder i UI-et — sjekk f.eks. hvordan
  `Rettskilde.GyldigFra`/`GyldigTil` redigeres, hvis det finnes en tilsvarende form, for å arve samme
  komponentvalg) — «Gyldig fra» og «Gyldig til (valgfritt)», begge default tomme (permanent tildeling er
  normaltilfellet, se docs/28).
- `opprett()`-funksjonen sender de to nye feltene med i `api.opprettMyndighetstildeling({...})`-kallet.
- `MyndighetstildelingDto`/`MyndighetstildelingRequest` (`Dtos.cs`) og `api/types.ts` sin tilsvarende
  type utvides med `GyldigFra`/`GyldigTil` (`DateOnly?`/`string | null` — følg eksisterende
  dato-serialiserings-konvensjon i `api/types.ts` for andre `DateOnly?`-felt).
- `VirksomhetDetalj.tsx`s «Myndighetstildelinger»-tabell (linje ~208-253) bør vise en ny kolonne for
  gyldighetsperiode når ett av feltene er satt (f.eks. «01.01.2026–» eller «01.01.2026–31.12.2027»),
  usynlig/tom for permanente tildelinger — konsistent med at «de aller fleste» forblir uendret i
  visningen.

---

## Del C — Ny entitet `VirksomhetRelasjonEntitet`

### C.1 Entitetsdefinisjon

Følger feltnavn-konvensjonene i `Entiteter.cs` (se `TjenesteavhengighetEntitet` som nærmeste
strukturelle presedens — én rettet kant mellom to rader av samme type, med typet `Rel`/`RelasjonsType`,
en fritekst-nyanse, og standard attribusjon, INGEN full versjonerings-pipeline siden dette ikke er
autoritativt rettskildeinnhold i samme forstand som `Begrep`/`Tjeneste`):

```csharp
/// <summary>
/// [Ny] Navngitt relasjon mellom to BESTEMTE, konkrete virksomheter (docs/28, «Beslutning: datamodell
/// for gruppe, relasjon og myndighetstildeling», mekanisme 2) — til forskjell fra gruppe-mekanismen
/// (Del A), som dekker en GENERISK term realisert av MANGE virksomheter. Samme "ett lagret rad, to
/// beregnede visningstekster (Fra-side/Til-side)"-mønster som <see cref="TjenesteavhengighetEntitet"/>
/// (se <see cref="RelasjonsTypeKonfigurasjonEntitet"/> for hvor visningstekstene faktisk lagres — DE
/// er konfigurerbare, i motsetning til Tjenesteavhengighets kompilerte Dictionary, se C.2).
/// <see cref="Virksomhet.OverordnetEnhetId"/> beholdes UENDRET ved siden av dette — automatisk,
/// Brreg-avledet hierarki uten hjemmel, ulik kilde/pålitelighet fra denne manuelt kuraterte tabellen.
/// De to slås BEVISST ikke sammen.
/// </summary>
public sealed class VirksomhetRelasjonEntitet
{
    public Guid Id { get; set; }
    public required Guid FraVirksomhetId { get; set; }
    public required Guid TilVirksomhetId { get; set; }

    /// <summary>Konfigurasjonsstyrt kode — FK (logisk, ikke DB-håndhevet, se C.2) til
    /// <see cref="RelasjonsTypeKonfigurasjonEntitet.Kode"/>. Kjente verdier per i dag: 'underlagt',
    /// 'sekretariat', 'klageinstans', 'enhet_i' — IKKE en uttømmende liste, ny type kan legges til uten
    /// kodeendring (se C.2).</summary>
    public required string RelasjonsType { get; set; }

    /// <summary>Nullbar — satt NÅR relasjonen er lovhjemlet.</summary>
    public Guid? HjemmelRettskildeId { get; set; }
    public string? HjemmelEid { get; set; }

    /// <summary>Fritekst + kildehenvisning (f.eks. en lenke til et org-kart) når det IKKE finnes en
    /// formell hjemmel — se docs/28s Klagenemndssekretariatet-eksempel.</summary>
    public string? Kommentar { get; set; }

    public string Entitetsstatus { get; set; } = "gjeldende";
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}
```

**Merk om `Entitetsstatus` og sletting** — verifisert presedens fra `TjenesteavhengighetEntitet`: den
HAR et `Entitetsstatus`-felt (default `"gjeldende"`) i skjemaet, men
`TjenesteavhengighetregisterTjeneste.SlettAsync` gjør likevel en EKTE `db.Remove(...)` (hard delete), IKKE
en soft-delete via `Entitetsstatus`. `VirksomhetRelasjonEntitet` bør følge NØYAKTIG samme, allerede
etablerte mønster: `Entitetsstatus`-feltet finnes for konsistens med husstilen og for eventuell fremtidig
bruk, men selve `SlettAsync`-implementasjonen gjør en ekte `Remove`. Ikke oppfinn en ny
sletting-semantikk her.

### C.2 `RelasjonsTypeKonfigurasjonEntitet` — ny tabell, IKKE direkte gjenbruk av `TaggKindKonfigurasjonEntitet`

**Konklusjon: ny, egen tabell — men kopier `TaggKindKonfigurasjonEntitet`s FAKTISKE (ikke antatte)
driftsmønster eksakt.** To separate spørsmål her, og docs/28 sammenblander dem litt:

**(a) Kan selve TABELLEN `TaggKindKonfigurasjonEntitet` gjenbrukes direkte (samme rader)?** Nei — formen
passer ikke. `TaggKindKonfigurasjonEntitet` har `Kode`/`Navn`/`Farge`/`Sorteringsrekkefolge`/`Aktiv`,
IKKE noe sted å lagre TO retningsavhengige visningstekst-MALER
(`"er underlagt {0}"`/`"er eier/overordnet for {0}"`) slik `RelasjonsType` trenger. En ny tabell er
derfor nødvendig for selve dataformen:

```csharp
public sealed class RelasjonsTypeKonfigurasjonEntitet
{
    public Guid Id { get; set; }
    public required string Kode { get; set; } // 'underlagt' | 'sekretariat' | 'klageinstans' | 'enhet_i' | ... (utvidbart)
    public required string FraVisningsmal { get; set; } // "er underlagt {0}"
    public required string TilVisningsmal { get; set; } // "er eier/overordnet for {0}"
    public int Sorteringsrekkefolge { get; set; }
    public bool Aktiv { get; set; } = true;
}
```

Seedes ved oppstart (`if (!await db.RelasjonsTypeKonfigurasjoner.AnyAsync()) { ... }`, nøyaktig samme
`Program.cs`-mønster som `TaggKindKonfigurasjonEntitet`s seed) med de fire kjente radene fra docs/28s
tabell:

| Kode | FraVisningsmal | TilVisningsmal |
|---|---|---|
| `underlagt` | «er underlagt {0}» | «er eier/overordnet for {0}» |
| `sekretariat` | «har sekretariat hos {0}» | «er sekretariat for {0}» |
| `klageinstans` | «har klageinstans hos {0}» | «er klageinstans for {0}» |
| `enhet_i` | «er enhet i {0}» | «har enhet {0}» |

**(b) Er `TaggKindKonfigurasjonEntitet` FAKTISK «admin-redigerbar» i dag, slik docs/28 antar?**
**Verifisert: NEI.** Dette er en viktig korreksjon til oppdragets egen premiss. Grep + lesing av
`Program.cs` og hele frontend viser: `TaggKindKonfigurasjonEntitet` har ÉN GET-endepunkt
(`GET /api/konfigurasjon/tagg-kinds`), seedes KUN `if (!await db.TaggKindKonfigurasjoner.AnyAsync())`
ved appstart, og har INGEN admin-UI, INGEN POST/PUT/DELETE-endepunkt noe sted i kodebasen. Den ENESTE
måten å legge til en ny tag-kind på i dag er enten (i) utvide seed-blokken i `Program.cs` + kjøre mot en
tom database, eller (ii) at Johann manuelt kjører en `INSERT` mot `tagg_kind_konfigurasjon`-tabellen via
`psql`. Det reelle fortrinnet dette mønsteret har over en hardkodet C#-array (som
`TjenesteavhengighetregisterTjeneste.GyldigeRel`) er IKKE «det finnes en admin-UI» — det finnes ikke —
men at gyldige verdier ligger i en SPØRRBAR/REDIGERBAR databasetabell Johann kan endre med rå SQL UTEN
en kodeendring+redeploy, mens en kompilert C#-array krever nettopp det.

**Anbefaling**: bygg `RelasjonsTypeKonfigurasjonEntitet` med EKSAKT samme, verifiserte (ikke antatte)
driftsmønster: seed-ved-oppstart-hvis-tom + ÉN read-only `GET /api/konfigurasjon/relasjonstyper`-endepunkt.
INGEN admin-CRUD-UI i denne runden — det ville vært NETT NY funksjonalitet utover det eksisterende
presedens faktisk gir, ikke gjenbruk av et etablert mønster. Fortell Johann eksplisitt (i PR-en/i
tilbakemeldingen) at «admin-redigerbar» heller ikke finnes for tag-kinds i dag, slik at forventningen
justeres samtidig som VirksomhetRelasjon bygges — en fremtidig, egen runde kan bygge en ekte admin-UI
for BEGGE konfigurasjonstabellene samtidig, hvis det prioriteres.

**Konsekvens for visningstekstberegningen**: fordi `RelasjonsType` er datastyrt (ikke en kompilert
C#-`Dictionary` slik `TjenesteavhengighetregisterTjeneste.Visningstekster` er), må
`VirksomhetRelasjonregisterTjeneste.HentForVirksomhetAsync` slå opp `FraVisningsmal`/`TilVisningsmal`
fra `RelasjonsTypeKonfigurasjonEntitet`-tabellen VED LESING (ett spørring, forhåndslastet for alle
distinkte `RelasjonsType`-verdier i resultatsettet — samme "unngå N+1"-hensyn som ellers i kodebasen),
IKKE fra en hardkodet literal-Dictionary slik Tjenesteavhengighet gjør. Dette er den ene reelle,
strukturelle forskjellen fra å «bare kopiere» `TjenesteavhengighetregisterTjeneste`-mønsteret rått — selve
retnings-/visningstekst-IDEEN kopieres, men lagringsstedet for tekstmalene flytter fra kompilert kode til
den nye konfigurasjonstabellen.

### C.3 Tjenesteklasse — `VirksomhetRelasjonregisterTjeneste`

Modellert direkte på `TjenesteavhengighetregisterTjeneste` (samme fil-/klassenavnmønster:
`VirksomhetRelasjonregisterTjeneste` el. lignende — følg den eksisterende navnekonvensjonen
`<Domene>registerTjeneste` brukt for `Tjenesteavhengighetregister`/`Kodelisteregister`):

```csharp
public sealed record VirksomhetRelasjonVisning(
    Guid Id, string RelasjonsType, string Retning, string Visningstekst,
    Guid MotpartVirksomhetId, string MotpartNavn,
    Guid? HjemmelRettskildeId, string? HjemmelEid, string? Kommentar);

public sealed class VirksomhetRelasjonregisterTjeneste(RegelIdeDbContext db)
{
    public async Task<List<VirksomhetRelasjonVisning>> HentForVirksomhetAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        // Samme mønster som TjenesteavhengighetregisterTjeneste.HentForTjenesteAsync:
        // 1. Hent alle rader der virksomhetId er FraVirksomhetId ELLER TilVirksomhetId (Entitetsstatus="gjeldende").
        // 2. Forhåndslast motpart-navn (Virksomhet.Navn for alle distinkte Fra-/TilVirksomhetId).
        // 3. Forhåndslast RelasjonsTypeKonfigurasjon for alle distinkte RelasjonsType-verdier i resultatet
        //    (NY sammenlignet med Tjenesteavhengighet-mønsteret — se C.2s siste avsnitt).
        // 4. For hver rad: erFra = (r.FraVirksomhetId == virksomhetId); slå opp riktig visningsmal
        //    (FraVisningsmal/TilVisningsmal) og string.Format med motpartens navn.
    }

    public async Task<VirksomhetRelasjonEntitet> OpprettAsync(
        Guid fraVirksomhetId, Guid tilVirksomhetId, string relasjonsType,
        Guid? hjemmelRettskildeId, string? hjemmelEid, string? kommentar,
        string opprettetAv, CancellationToken ct = default)
    {
        // Valider: relasjonsType finnes og er Aktiv i RelasjonsTypeKonfigurasjonEntitet (ArgumentException
        // med "Ukjent relasjonstype", samme "ingen gjettet fallback"-stil som GyldigeRel-sjekken).
        // Valider: begge virksomheter finnes.
        // Valider: fraVirksomhetId != tilVirksomhetId (en virksomhet kan ikke ha en relasjon til seg selv,
        //   samme selvreferanse-sjekk som Tjenesteavhengighet.OpprettAsync).
        // Valider: hjemmelRettskildeId finnes, hvis satt.
        // Duplikatsjekk: samme (FraVirksomhetId, TilVirksomhetId, RelasjonsType), Entitetsstatus="gjeldende"
        //   finnes ikke allerede (samme mønster som Tjenesteavhengighets duplikatsjekk).
        // INGEN sykel-sjekk (BFS) her — i motsetning til Tjenesteavhengighet er det ikke opplagt at en
        //   sykel i VirksomhetRelasjon er meningsløs (f.eks. kan A være "underlagt" B og B samtidig "enhet_i"
        //   A i en annen betydning) — eksplisitt IKKE bygget denne runden, flagg som åpent spørsmål til
        //   Johann hvis reelle sykler dukker opp i praksis.
    }

    public async Task<bool> SlettAsync(Guid id, CancellationToken ct = default)
    {
        // Ekte Remove, samme presedens som TjenesteavhengighetregisterTjeneste.SlettAsync (se C.1).
    }
}
```

### C.4 EF-modellkonfigurasjon (`RegelIdeDbContext.cs`)

Følg `TjenesteavhengighetEntitet`s konfigurasjonsblokk som mal:

```csharp
b.Entity<VirksomhetRelasjonEntitet>(e =>
{
    e.ToTable("virksomhet_relasjoner");
    e.HasKey(x => x.Id).HasName("virksomhet_relasjoner_pkey");
    e.Property(x => x.FraVirksomhetId).HasColumnName("fra_virksomhet_id");
    e.Property(x => x.TilVirksomhetId).HasColumnName("til_virksomhet_id");
    e.Property(x => x.RelasjonsType).HasColumnName("relasjons_type");
    e.Property(x => x.HjemmelRettskildeId).HasColumnName("hjemmel_rettskilde_id");
    e.Property(x => x.HjemmelEid).HasColumnName("hjemmel_eid");
    e.Property(x => x.Kommentar).HasColumnName("kommentar");
    e.Property(x => x.Entitetsstatus).HasColumnName("entitetsstatus").HasDefaultValue("gjeldende");
    e.Property(x => x.OpprettetAv).HasColumnName("opprettet_av");
    e.Property(x => x.OpprettetTidspunkt).HasColumnName("opprettet_tidspunkt").StandardNaa(sqlite);

    e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.FraVirksomhetId).OnDelete(DeleteBehavior.Cascade);
    e.HasOne<Virksomhet>().WithMany().HasForeignKey(x => x.TilVirksomhetId).OnDelete(DeleteBehavior.Restrict);
    e.HasOne<RettskildeEntitet>().WithMany().HasForeignKey(x => x.HjemmelRettskildeId);
    e.HasIndex(x => x.FraVirksomhetId).HasDatabaseName("ix_virksomhet_relasjoner_fra");
    e.HasIndex(x => x.TilVirksomhetId).HasDatabaseName("ix_virksomhet_relasjoner_til");
    e.HasIndex(x => new { x.FraVirksomhetId, x.TilVirksomhetId, x.RelasjonsType }).IsUnique()
        .HasFilter("entitetsstatus = 'gjeldende'")
        .HasDatabaseName("ux_virksomhet_relasjoner_fra_til_type");
});

b.Entity<RelasjonsTypeKonfigurasjonEntitet>(e =>
{
    e.ToTable("relasjonstype_konfigurasjon");
    e.HasKey(x => x.Id).HasName("relasjonstype_konfigurasjon_pkey");
    e.Property(x => x.Kode).HasColumnName("kode");
    e.Property(x => x.FraVisningsmal).HasColumnName("fra_visningsmal");
    e.Property(x => x.TilVisningsmal).HasColumnName("til_visningsmal");
    e.Property(x => x.Sorteringsrekkefolge).HasColumnName("sorteringsrekkefolge");
    e.Property(x => x.Aktiv).HasColumnName("aktiv").HasDefaultValue(true);
    e.HasIndex(x => x.Kode).IsUnique().HasDatabaseName("ux_relasjonstype_konfigurasjon_kode");
});
```

(Bruk `OnDelete(Restrict)` på `TilVirksomhetId`, samme asymmetri som Tjenesteavhengighets
Fra-Cascade/Til-Restrict — sletting av en virksomhet som er MOTPART i en relasjon bør ikke stille slette
relasjonsraden fra motpartens perspektiv; verifiser dette valget mot faktisk ønsket oppførsel før
migrasjonen kjøres, det er et bevisst forslag her, ikke låst av docs/28.)

Foreslått migrasjonsnavn: `LeggTilVirksomhetRelasjon`.

### C.5 Nye endepunkter

| Endepunkt | Beskrivelse |
|---|---|
| `GET /api/virksomheter/{id}/relasjoner` | `HentForVirksomhetAsync(id)` — begge retninger, riktig beregnet visningstekst per rad. |
| `POST /api/virksomheter/{id}/relasjoner` | `OpprettAsync` — `{id}` blir alltid `FraVirksomhetId` (samme «{id} er alltid Fra-siden»-konvensjon som `POST /api/tjenester/{id}/avhengigheter`). |
| `DELETE /api/virksomhet-relasjoner/{relasjonId}` | `SlettAsync`. |
| `GET /api/konfigurasjon/relasjonstyper` | Lister aktive `RelasjonsTypeKonfigurasjonEntitet`-rader — samme `GET /api/konfigurasjon/tagg-kinds`-mønster. |

Nye DTO-er i `Dtos.cs` (samme `FraEntitet`/`FraVisning`-statiske fabrikkmetode-konvensjon som
`TjenesteavhengighetDto`/`TaggKindKonfigurasjonDto`):

```csharp
public sealed record RelasjonsTypeKonfigurasjonDto(string Kode, string FraVisningsmal, string TilVisningsmal);

public sealed record VirksomhetRelasjonDto(
    Guid Id, string RelasjonsType, string Retning, string Visningstekst,
    Guid MotpartVirksomhetId, string MotpartNavn,
    Guid? HjemmelRettskildeId, string? HjemmelEid, string? Kommentar);

public sealed record VirksomhetRelasjonRequest(
    Guid TilVirksomhetId, string RelasjonsType, Guid? HjemmelRettskildeId, string? HjemmelEid, string? Kommentar);
```

### C.6 UI-plassering — `VirksomhetDetalj.tsx`

Ny seksjon, plassert etter «Grunndata»-seksjonen (linje ~124-174 i dagens fil) og FØR
«Navneformer i rettskildetekst» — begrunnelse: relasjoner til andre virksomheter er, som
«Overordnet enhet»-raden i Grunndata, informasjon om virksomhetens PLASS i et hierarki/nettverk, mens
Navneformer/Myndighetstildelinger er om hvordan andre TEKSTER omtaler/tildeler DENNE virksomheten —
naturlig gruppert nærmere Grunndata. Følg samme `<section>`+`<Heading level={2}>`-mønster som de
eksisterende seksjonene:

- **Overskrift**: «Relasjoner til andre virksomheter».
- **Tabell**: én rad per `VirksomhetRelasjonDto`, viser `visningstekst` (allerede ferdig formatert med
  motpartens navn, samme prinsipp som `TjenesteavhengighetVisning.Visningstekst` gjør i
  Tjeneste-siden), en lenke til motparten (`RouterLink to={`/virksomheter/${r.motpartVirksomhetId}`}`,
  samme mønster som «Overordnet enhet»-raden allerede bruker for `virksomhet.overordnetEnhetId`), og
  `hjemmelEid`/`kommentar` i en egen kolonne når satt.
- **Skjema for å legge til ny relasjon** — egen komponentfil `LeggTilVirksomhetRelasjonForm.tsx` (samme
  fil-per-skjema-konvensjon som `LeggTilMyndighetstildelingForm.tsx`), som bruker:
  - `VirksomhetVelger` (eksisterende komponent, `RegelIde.Web/src/virksomhet/VirksomhetVelger.tsx`) for
    å velge motpart — samme `Combobox`-baserte, søkbare mønster som andre steder (451+ virksomheter gjør
    en rå `<Select>` upraktisk, se komponentens egen dokumentasjon).
  - `<Select>` for `RelasjonsType`, populert fra `GET /api/konfigurasjon/relasjonstyper`
    (`api.hentRelasjonstyper()`, ny funksjon i `client.ts`, samme mønster som `api.hentTaggKinds()`).
  - `RettskildeVelger` (eksisterende komponent, `RegelIde.Web/src/rettskilde/RettskildeVelger.tsx`) for
    valgfri hjemmel — samme `Suggestion`-baserte enkeltvalg-mønster som
    `LeggTilMyndighetstildelingForm.tsx` allerede bruker for sitt hjemmelfelt.
  - `Textfield` for valgfri `HjemmelEid` og `Kommentar`.
- **Retningsvisning i praksis** — presiser i UI-teksten (en kort `Paragraph` under overskriften, samme
  forklarende-tekst-mønster som «Myndighetstildelinger»-seksjonen allerede har) at listen viser
  relasjoner i BEGGE retninger fra denne virksomhetens ståsted — den konkrete lærdommen fra
  `OverordnetEnhetId`-bug-en (docs/28) som motiverte hele denne mekanismen.

---

## Del D — Byggerekkefølge

Anbefalt rekkefølge, med eksplisitt «verifiser FØR du går videre»-sjekkliste per steg. Del A alene
berører ~23 filer og bør trolig være sin EGEN PR, atskilt fra Del B/C — risikoen for en stor,
vanskelig-å-reviewe diff er reell hvis alt slås sammen, og Del A har ingen funksjonell avhengighet til
Del B/C (de tre delene er uavhengige av hverandre bortsett fra at Del C's `VirksomhetVelger`/
`RettskildeVelger`-gjenbruk ikke krever noe fra A/B).

### Steg 1 — Del A: migrasjoner (skjema + data)

1. Oppdater `RegelIdeDbContext.cs`-modellkonfigurasjonen (CHECK-constraints, indeksnavn, kolonnenavn —
   se A.1.1/A.2).
2. Generer migrasjonen `OmdopBegrepskategoriRolleTilGruppe` (`dotnet ef migrations add`).
3. Legg til de to `UPDATE`-setningene manuelt i migrasjonens `Up()` (EF genererer ikke datamigrasjon
   automatisk) — se A.2 punkt 2 for eksakt SQL.
4. **Sjekkliste før du går videre**: kjør migrasjonen mot en lokal kopi av dev-databasen, kjør
   `GET /api/rollebegrep`-ekvivalenten (nå `/api/gruppebegrep`, forutsetter steg 2 er gjort — kjør derfor
   dette FØR steg 2 hvis mulig, eller midlertidig mot den gamle ruten) og bekreft at nøyaktig 1 rad har
   `begrepskategori='gruppe'`, og at en telling av `navnekandidater WHERE kategori='gruppe'` gir 2486.

### Steg 2 — Del A: backend-kode + tester

1. Gjennomfør ALLE 10 backend-filendringene fra A.1.1s tabell.
2. Gjennomfør alle 6 testfilendringene.
3. **Sjekkliste før du går videre**: `dotnet build` uten advarsler om ubrukte `using`/navnekrasj, `dotnet
   test` grønt for `RegelIde.Data.Tests` OG `RegelIde.Api.Tests` (spesielt
   `NavnekandidatOppdagelseTjenesteTests.cs`, den tyngste), ingen gjenværende treff på strengen `"rolle"`
   i et grep begrenset til de 10+6 filene (bekreft eksplisitt at grep-treff som GJENSTÅR alle er
   kategori 2/3/4 fra A.1.2-A.1.4, ikke glemte kategori-1-treff).

### Steg 3 — Del A: frontend-kode

1. Gjennomfør alle 7 frontend-filendringene fra A.1.1s tabell.
2. **Sjekkliste før du går videre**: `tsc -b --noEmit` kjørt FRA `RegelIde.Web`-mappen (ikke bare
   `tsc --noEmit` fra rot — se `feedback_regel_ide_tsc_noemit_vacuous.md`, en kjent fallgruve i dette
   prosjektet der `tsc --noEmit` uten `-b` sjekker null filer). Manuell smoke-test i kjørende UI:
   `NavnekandidaterListe.tsx`s kategori-filter, `BegrepDetalj.tsx` for det ene eksisterende
   Statsforvalteren-gruppebegrepet, `LeggTilMyndighetstildelingForm.tsx`s nedtrekksliste.

### Steg 4 — Del B: migrasjon + backend

1. Migrasjonen `LeggTilGyldighetsperiodePaMyndighetstildeling` (kun skjema, ingen data å migrere — nye
   nullable felt).
2. Utvid `ErGjeldendeAsync` (B.2 punkt 1) og koble den faktisk inn i de to berørte
   `Program.cs`-endepunktene (B.2 punkt 2) — inkluder en test som faktisk verifiserer at en tildeling
   med utløpt `GyldigTil` FALLER UT av `?gjeldende=true`-spørringen, siden dette er selve
   nytte-poenget med utvidelsen.
3. **Sjekkliste før du går videre**: en ny test i `MyndighetstildelingTjenesteTests.cs` som setter
   `GyldigTil` til en fortidsdato og bekrefter `ErGjeldendeAsync` returnerer `false` selv når hjemmelen
   selv er `Gjeldende` — dette er PRESIS scenarioet (Vertskommune) som motiverer hele Del B, og bør
   IKKE kunne slippe gjennom usjekket.

### Steg 5 — Del B: frontend

1. `LeggTilMyndighetstildelingForm.tsx`s to nye datofelt, DTO/type-utvidelser.
2. **Sjekkliste før du går videre**: opprett en test-tildeling med en `GyldigTil`-dato i fortiden via
   UI-et, bekreft at den vises i `VirksomhetDetalj.tsx`s tabell (informativt viktig selv om den er
   utløpt — ikke skjul historiske tildelinger, kun ekskluder dem fra `?gjeldende=true`-filtrerte
   spørringer andre steder).

### Steg 6 — Del C: migrasjon

1. `LeggTilVirksomhetRelasjon` — begge nye tabeller (`VirksomhetRelasjonEntitet`,
   `RelasjonsTypeKonfigurasjonEntitet`), samt seed-blokken i `Program.cs` for de fire kjente
   relasjonstypene.
2. **Sjekkliste før du går videre**: `GET /api/konfigurasjon/relasjonstyper` mot en fersk lokal database
   returnerer nøyaktig de fire radene fra C.2s tabell, med riktige `{0}`-plassholdere i begge
   visningsmalene.

### Steg 7 — Del C: backend-tjeneste + tester

1. `VirksomhetRelasjonregisterTjeneste` (C.3) + `RegelIdeDbContext`-konfigurasjon (C.4).
2. Ny testfil, samme dekningsnivå som `TjenesteavhengighetregisterTjenesteTests`-ekvivalenten (finn
   denne filen selv — sannsynligvis `TjenesteavhengighetTjenesteTests.cs` el.l. — som mal): opprett,
   dupliker-avvis, selvreferanse-avvis, ukjent-relasjonstype-avvis, hent-begge-retninger med korrekt
   visningstekst, slett.
3. **Sjekkliste før du går videre**: en test som EKSPLISITT bekrefter at samme rad gir ULIK
   visningstekst avhengig av hvilken virksomhet man spør fra (Fra- vs. Til-siden) — dette er selve
   poenget hentet fra `OverordnetEnhetId`-bug-lærdommen, og bør testes eksplisitt, ikke antas riktig
   fordi mønsteret er kopiert fra Tjenesteavhengighet.

### Steg 8 — Del C: endepunkter

1. De fire endepunktene fra C.5, `Dtos.cs`-utvidelsene.
2. **Sjekkliste før du går videre**: endepunkt-tester (samme mønster som
   `HendelseOgTjenesteavhengighetEndepunktTests.cs`) som dekker minst ett av de konkrete eksemplene fra
   docs/28s merkenemnd-hierarki (f.eks. opprett «Lokal merkenemnd — sekretariat → Statsforvalteren», hent
   fra begge sider, bekreft visningstekstene matcher tabellen i docs/28 ordrett).

### Steg 9 — Del C: frontend

1. `LeggTilVirksomhetRelasjonForm.tsx` (ny fil) + `VirksomhetDetalj.tsx`s nye seksjon (C.6).
2. `api/client.ts`/`api/types.ts`-utvidelser.
3. **Sjekkliste før du går videre**: `tsc -b --noEmit` fra `RegelIde.Web` (samme fallgruve-advarsel som
   steg 3). Manuell verifisering i kjørende UI: opprett en relasjon mellom to reelle virksomheter i
   dev-databasen, naviger til BEGGE virksomhetenes detaljsider, bekreft at relasjonen vises med korrekt,
   ulik visningstekst på hver side.

### Generell advarsel til byggerunden

Ingen av de tre delene (A/B/C) har en HARD kodemessig avhengighet til hverandre — de kan i prinsippet
bygges i hvilken som helst rekkefølge, eller parallelt av flere personer/agenter. Rekkefølgen over er en
anbefaling basert på: (1) Del A er størst og mest risikabel (flest filer, reell fare for at noen
kategori-2/3/4-forekomster av «rolle» røres ved en feil), så den bør landes og stabiliseres FØRST og
ALENE, uforstyrret av samtidige endringer i de samme filene fra B/C; (2) Del B er liten og godt avgrenset
(én klasse, én skjema, én form) — naturlig neste steg; (3) Del C er ny funksjonalitet fra bunnen av og
drar nytte av at A/B allerede er landet og stabile, selv om den ikke TEKNISK avhenger av dem.
