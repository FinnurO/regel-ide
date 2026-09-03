# 31. Navneform-berikelse via SNL/SSR — spesifikasjon og byggeplan

Spesifikasjon for et NYTT oppdagelsesmønster i navnekandidat-sveipet: «stor bokstav midt i en
setning» som rå utløser, klassifisert mot to eksterne, levende API-er (Store norske leksikon og
Kartverkets Sentralt stadnamnregister) for å skille genuine institusjonsnavn fra rene stedsnavn og
personnavn. Bakgrunn/kilde-research finnes i chat-historikken denne planrunden kom fra (samme
transkripsjonskonvensjon som docs/24) — konklusjonene under er verifisert direkte mot de faktiske
API-ene og sidene (WebFetch/WebSearch), ikke antatt fra hukommelse.

**Hvorfor dette mønsteret ikke fantes før**: dagens `NavnekandidatOppdagelseTjeneste` er UTELUKKENDE
presise, kjente mønstre (`SuffiksMønster`, `FasteRollerMønster`, flerords-institusjonsord-mønsteret)
— den finner ALDRI en institusjon den ikke allerede har et ord/suffiks-signal for. «Stor bokstav
midt i en setning» er det motsatte: et bredt, uspesifikt fangenett (norsk kapitaliserer aldri
vanlige substantiv, så ETHVERT slikt treff er et ekte egennavn) som uten videre klassifisering
druknet ville produsert enorme mengder personnavn/stedsnavn/utenlandske ord — ubrukelig alene.

## 1. De tre kildene — verifisert, ikke antatt

### 1.1 Store norske leksikon (SNL) — positiv institusjonskilde, FØRST i kjeden

- **Søk-API**: `https://[subdomain].snl.no/api/v1/search?query=X&limit=&offset=` — levende, per-term
  søk. Ingen dokumentert API-nøkkel eller ratelimit. Svar: artikkel-id/type/tittel/snippet/
  **taksonomikategori**/rank/URL (standard + `.json`-variant).
- **Taksonomi**: `.taxonomy/3103` («Myndigheter i Norge») dekker eksplisitt embetsmenn, Kongehuset,
  lokalforvaltning, offentlige direktorater, **offentlige utvalg**, regjering/departement, Stortinget
  — nøyaktig kategorien `docs/28`s §5 identifiserte som verst dekket (utvalg/nemnder uten egen
  juridisk enhet i Brreg, f.eks. EOS-utvalget).
- **Faktaboks-felt** (verifisert mot `snl.no/Den_Norske_Advokatforening`): offisielt navn, **«også
  kjent som»** (eksplisitt alias-felt — løser synonym-problemet i docs/28 §3 direkte), organisasjons-
  type, **organisasjonsnummer** når relevant, sektor/næringskode.
- **Lisens**: metadata/faktaboks «alltid fritt lisensiert» — trygt å lagre alias/orgnr. Løpetekst
  varierer, oppgitt per artikkel i JSON-svaret — IKKE lagre løpetekst uten å sjekke denne per treff.
- **Ingen dokumentert bulk-nedlasting** av en hel taksonomikategori — må gå via søk, ikke en
  engangs-import av «alle offentlige utvalg».

### 1.2 Kartverkets Sentralt stadnamnregister (SSR) — negativt geografifilter, ANDRE i kjeden

- Over 1 million stadnamn, objekttype + kommune/fylke-tilhørighet + koordinater.
- **Tilgang**: bulk GML/SOSI via [Geonorge](https://register.geonorge.no/geodatalov-statusregister/stedsnavn-komplett-ssr/e1c50348-962d-4047-8325-bdc265c853ed),
  PLUSS et nøkkelfritt REST-søk (indeksert, JSON/XML) — samme per-term-oppslags-mulighet som SNL,
  ikke bare bulk.
- **Lisens**: **NLOD 2.0** — samme lisensfamilie som Lovdata-bulkdataen appen allerede bruker
  (`LovdataBulkHenter`), ingen ny juridisk avklaring nødvendig.

### 1.3 Ordbokene (Bokmålsordboka/Nynorskordboka) — lav prioritet, IKKE i første versjon

- Full nedlastbar dump hos Språkbanken (NB), CC-BY 4.0, ordklasse + bøying fra Norsk ordbank.
- **Konklusjon fra diskusjonen (verifisert resonnement, ikke bare antatt)**: siden norsk ALDRI
  kapitaliserer vanlige substantiv, er et treff fra «stor bokstav midt i setning»-mønsteret per
  definisjon allerede filtrert av selve ortografien — restmengden etter SNL+SSR er i praksis
  personnavn/stedsnavn/institusjonsnavn, ikke fellesord. Ordboka ville derfor fanget lite som ikke
  allerede er utelukket av mønsteret selv. **Bygges IKKE i første versjon** — vurderes kun senere hvis
  reell restmengde etter SNL+SSR viser seg fortsatt for støyende i praksis.

## 2. Klassifiseringskjede

For hvert rå treff («stor bokstav, ikke ved forrige punktum/setningsstart»):

1. **SNL-søk** (`query=treffet`, `limit=3`). Eksakt eller nær-eksakt tittelmatch, MED taksonomi under
   en myndighets-/institusjonskategori → **høy starttillit, positiv institusjonskandidat**. Lagre
   SNL-URL + «også kjent som»-alias (fra artikkelens faktaboks, hentet i et andre kall når treff
   finnes) som berikelse på kandidaten.
2. **Ingen SNL-treff (eller treff uten institusjonstaksonomi)** → **SSR-oppslag**. Treff (stedsnavn
   funnet) OG **ingen** institusjonsord (kommune/fylkeskommune/direktorat/etc., gjenbruk eksisterende
   `Institusjonsord`-liste) rett etter i løpeteksten → **forkast som geografisk løpetekst-referanse**,
   ingen navnekandidat opprettes. Treff MED institusjonsord etter → behold som kandidat, men merk at
   forleddet er et bekreftet, ekte stadnamn (samme positive bekreftelsesrolle som når `Institusjonsord`-
   mønsteret allerede finner "X kommune" — SSR gir en EKSTRA bekreftelse på at "X" er reelt).
3. **Ingen treff i NOEN av dem** → behold som lav-tillit kandidat (ukjent egennavn — kan være en ny,
   ikke-katalogisert institusjon, et personnavn, eller et utenlandsk ord) — vises i køen, men uten
   noen berikelse/tillitsheving. IKKE forkastet automatisk — «ingen gjettet fallback», samme filosofi
   som resten av navnekandidat-mekanismen.

## 3. Arkitektur

**Live per-term-oppslag med lokal cache — IKKE en full bulk-import av SSRs 1+ million rader eller et
forsøk på å skrape hele SNL-taksonomien.** Begrunnelse: begge API-ene støtter presist per-term-søk,
og et fullkorpussveip kan produsere svært mange rå treff (jf. begrepsoppdagelsens 6018 treff på et
mye snevrere mønster, docs/24) — å slå opp samme term om og om igjen ved hvert sveip er unødvendig
netttrafikk uten cache, og INGEN av API-ene har dokumentert ratelimit å navigere trygt uten en.

- Ny tabell `EksternNavneoppslagCacheEntitet`: `Term` (normalisert, lowercase), `Kilde`
  (`'snl'`\|`'ssr'`), `Treff` (bool), `TaksonomiKategori`/`ObjektType` (nullable, kun ved treff),
  `AliasJson`/`OrganisasjonsnummerFunnet` (nullable, kun SNL), `EksternUrl` (nullable), `SlaOppTidspunkt`.
  Unik nøkkel `(Term, Kilde)`. Ingen TTL-utløp i første versjon (institusjons-/stedsnavn endres sjelden
  — samme oppdateringskadens som Brreg-katalogen forøvrig) — en fremtidig "tøm cache og slå opp på
  nytt"-mekanisme kan legges til senere, ikke nå.
- Ny tjeneste (foreslått `EksternNavneoppslagTjeneste`): `SlaOppSnlAsync(term)`/`SlaOppSsrAsync(term)`
  — cache-oppslag først, live HTTP-kall kun ved cache-miss, ALDRI kast videre en nettverksfeil til
  selve sveipet (fanges, logges, behandles som «ukjent» — et utilgjengelig eksternt API skal aldri
  stoppe eller krasje et helt korpussveip, samme «§3.3 ingen gjettet fallback, men også ingen skjørhet
  mot eksterne feil»-holdning som resten av appens integrasjoner).

## 4. Hvor dette kobles inn — gjenbruk `NavnekandidatEntitet`, ikke en ny tabell

Til forskjell fra `Begrepsforekomst` (docs/24), som fikk en EGEN tabell fordi
`BegrepEntitet`/dagens KI-forslagsmekanisme ikke passet formen — her passer det EKSISTERENDE
`NavnekandidatEntitet`-arbeidskø-mønsteret (`Venter`/`Godkjent`/`Avvist`, samme
`GodkjennAsync`-revalideringsflyt) strukturelt uendret. Dette er samme TYPE kandidat
(navneform for en virksomhet), bare oppdaget via en NY metode. Verifiser selv (les
`NavnekandidatOppdagelseTjeneste.cs`/`Entiteter.cs` FØR du bygger) om det allerede finnes et
diskriminator-felt for HVILKET mønster som produserte en rad — hvis ikke, legg til et nytt, nullbart
felt (f.eks. `OppdagelsesKilde`/`MonsterId`, samme idé som `BegrepsforekomstEntitet.MonsterId`) slik
at UI-et kan vise/filtrere på "funnet via stor-bokstav+SNL/SSR" separat fra de etablerte
suffiks-/rolleordsmønstrene — ikke bland dem sammen usynlig.

## 5. Byggerekkefølge (første versjon)

1. **`EksternNavneoppslagCacheEntitet` + `EksternNavneoppslagTjeneste`** (§3) — de to live-oppslagene
   med cache, testet isolert (mock/stub HTTP for enhetstester, ett faktisk live-kall mot hver API i en
   egen, tydelig merket integrasjonstest — samme mønster som andre eksterne kall i kodebasen bør ha).
2. **«Stor bokstav midt i setning»-mønsteret** i (eller ved siden av)
   `NavnekandidatOppdagelseTjeneste` — ren struktur-/tegnsetting-basert utløser (ikke ordliste-basert),
   over `RettskildeNode.Tekst`.
3. **Klassifiseringskjeden** (§2) kobler utløseren til `EksternNavneoppslagTjeneste`, produserer
   `NavnekandidatEntitet`-rader (kategori `virksomhet`) for SNL-bekreftede treff og lav-tillit-rader
   for ukjente treff, forkaster SSR-bekreftet-geografi-treff.
4. **Ordbokene bygges IKKE** denne runden (§1.3).
5. **UI**: gjenbruk eksisterende `NavnekandidaterListe.tsx` (nylig oppgradert til kompakt tetthet,
   PR #93) — vis den nye kilde-/berikelsesinformasjonen (SNL-alias, SSR-bekreftelse) som en ekstra
   kolonne/detalj, ikke en helt ny side, siden dette er samme kø som allerede finnes.

## 6. Åpne spørsmål / risikoer

- Ingen av API-ene har dokumentert ratelimit — cache (§3) reduserer risikoen kraftig, men et FØRSTE
  fullkorpussveip vil likevel gjøre mange tusen unike oppslag. Vurder å kjøre første sveip mot et
  avgrenset delsett av korpuset (samme forsiktighetsprinsipp som ble brukt for begrepsoppdagelsens
  to referansedokumenter) før et fullt sveip, for å observere faktisk API-oppførsel under last.
  Bør IKKE avgjøres stille av byggeagenten — flagg til Johann hvis restriksjoner observeres.
- SNLs søk-API returnerer «nær-eksakt tittelmatch» — eksakt matchelogikk (streng likhet vs.
  fuzzy/delvis) må bygges bevisst, ikke gjettes, siden en for løs match ville gitt falske positive
  institusjonsbekreftelser.
- Ordbokene kan bli aktuell igjen senere hvis SNL+SSR-restmengden i praksis viser seg større enn
  forventet — ikke steng den muligheten permanent, bare utsett den.

## 7. [Ny, issue #117] Utvidet til de eldre, etablerte mønstrene også

§5s byggerekkefølge over beskriver KUN det opprinnelige, NYE "stor bokstav midt i setning"-mønsteret
(`SveipStorBokstavAsync`). Issue #117 (Johann, 2026-09-03) bekreftet at de eldre, presise mønstrene —
suffiksmønsteret og flerords-institusjonsord-mønsteret (`SveipAsync`) — aldri ble koblet til denne
klassifiseringskjeden, slik at kjente falske positiver derfra (typeeksempelet "regelverket"/
"avtaleverket") kun ble luket ut av den manuelle `VerketDenyliste`, uten noen SNL/SSR-validering i det
hele tatt.

**Løsning**: `SveipAsync` kaller nå samme `BeholdSomKandidatAsync`-kjede (§2) og samme cache
(`EksternNavneoppslagCacheEntitet`) som `SveipStorBokstavAsync` — ingen ny, parallell mekanisme. Se
`NavnekandidatOppdagelseTjeneste.SveipAsync`s metodekommentar for full begrunnelse, inkludert:

- **Scopet til KUN `"virksomhet"`** — `"gruppe"`-kandidater (fast rollesubstantiv-liste + suffiksmønsterets
  liten-forbokstav-gren) sendes ALDRI til SNL/SSR: de er en lukket liste generiske rollesubstantiv, ikke
  institusjons-EGENNAVN, og spørsmålet SNL/SSR svarer på ("er dette en kjent institusjon/et kjent
  stedsnavn") gir ikke mening for dem.
- **Gjenbruk, ikke en ny, skreddersydd variant**: klassifiseringen kalles med HELE den fangede
  kandidatteksten (f.eks. "Miljødirektoratet", "Statens vegvesen") som term — til forskjell fra
  "stor bokstav"-mønsteret, der termen er ETT bart, ukvalifisert ord. SSR-forkastingsgrenen (§2 punkt 2)
  trigges derfor i praksis sjelden for disse to mønstrene (institusjonsordet/-suffikset er allerede en
  del av den fangede teksten), så nettoeffekten her er hovedsakelig SNL-bekreftelse/berikelse — ikke en
  ny presisjonsmekanisme som erstatter `VerketDenyliste` (den forblir uendret).
- **"Ingen gjettet fallback" gjelder fortsatt**: en term SNL/SSR ikke kjenner igjen ("ukjent i begge",
  §2 punkt 3) forkastes IKKE automatisk — den beholdes som lav-tillit-kandidat, akkurat som før denne
  utvidelsen og akkurat som `SveipStorBokstavAsync` allerede gjør. Dette er en BEVISST, IKKE endret
  presisjon/recall-avveining: Johanns kommentar på issue #117 antydet at "For tilsyn"/"Konkret tilsyn"
  (issue #149, et separat root cause — manglende `ErSetningsstart`-sjekk, fikset uavhengig) "også ville
  vært luket bort" av SNL/SSR, men en generell "forkast alt SNL/SSR ikke kjenner igjen"-regel for disse
  mønstrene ville også forkastet ekte, men ennå ikke SNL/SSR-katalogiserte institusjoner — en vesentlig
  presisjon/recall-produktbeslutning som IKKE er tatt stilltiende her. Flagget eksplisitt til Johann i
  PR-en for issue #117 som en mulig, separat oppfølging dersom han faktisk vil ha strengere forkasting.
- **Berikelse ved lesetidspunkt** (`RegelIde.Api`s `BerikNavnekandidaterAsync`) er utvidet FRA "kun rader
  med `OppdagelsesKilde == StorBokstavOppdagelsesKilde`" TIL "alle `Kategori == 'virksomhet'`-rader" —
  samme cache-tabell kan nå ha treff for en kandidat uansett hvilket mønster som oppdaget den.
