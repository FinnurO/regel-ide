# 18. Vurdering: Rettighet/Samhandling-modellen mot faktisk kode og låste beslutninger

*Mottatt notat: «Revisjon av tjenestemodellen (CPSV-tilnærmingen erstattes)», fra analysearbeid på
tvers av Altinn, Oppgaveregisteret, Statsforvalteren og fylkeskommuner. Denne vurderingen tar
**ikke** stilling til om selve ideen — retten fremfor samhandlingen som modellenhet — er riktig i
sak. Det er en ekstern analytisk konklusjon, og den overprøves ikke her. Vurderingen gjelder
utelukkende tre ting: **henger notatet sammen med det som faktisk er bygget**, **hva koster en
migrering**, og **hvordan står notatet mot beslutninger som allerede er låst eller parkert**.*

Verifisert mot `docs/vurdering-rettighet-samhandling-modell` @ `34f4d85` (2026-08-14), som er
`master` uendret. Alle linjenumre er fra det commit-et.

---

## 0. Markører

| Markør | Betyr |
|---|---|
| **STEMMER** | Notatets påstand om dagens kode er korrekt, verifisert mot fil og linje |
| **STEMMER DELVIS** | Noe reelt finnes, men ikke i den formen notatet beskriver |
| **STEMMER IKKE** | Bekreftet med negativt søk — det notatet kaller eksisterende finnes ikke |
| **[LÅST]** | Besluttet i en tidligere runde. Binder denne vurderingen, reåpnes ikke her |
| **[PARKERT]** | Foreslått i en tidligere runde, aldri besluttet, aldri kodet |
| **[HULL]** | Notatet har ikke selv flagget dette, og det endrer kostnaden |
| **[PÅ AVKLARING]** | Kan ikke avgjøres uten Johann |

---

## 1. Sammendrag

**Notatet er ikke bygningsklart, og hovedgrunnen er ikke migreringskost — den er overraskende lav.
Hovedgrunnen er at notatet løser samme strukturproblem som `docs/17` allerede har levert en
gjennomarbeidet løsning på, uten å vite at `docs/17` finnes.** Det er nå to konkurrerende forslag
liggende samtidig for «samlenivå vs. lokal instans» og for «hvilken slags organisasjon er dette»:
ett fra en avklaringsrunde i dette prosjektet 2026-08-13/14 (`docs/17`, merget men parkert, med 9
ubesvarte spørsmål), og ett nyimportert utenfra (dette notatet). De to legger til **nesten likelydende
felt på samme tabell** — `Virksomhet.Organisasjonstype` (docs/17) og
`Virksomhet.Organiseringstype` (notatet) — med disjunkte verdisett og ulik betydning. Godkjennes
begge som skrevet, får `Virksomhet` to felt ingen leser kan holde fra hverandre.

Verre: notatets eget åpne spørsmål 2 («bør det innføres et eget `Tjenestemal`-konsept … for å unngå
at samme tjenestetekst dupliseres 8 ganger?») er **ordrett det spørsmålet `docs/17` §5 er skrevet for
å besvare**, i sin lengste og mest gjennomarbeidede seksjon — der det ble utredet som alternativ A,
B og C, og der A (et eget malkonsept) ble **eksplisitt avvist med konkret begrunnelse**. Svarer
Johann «ja, bygg `Tjenestemal`» på notatets spørsmål 2, omgjør han `docs/17`s anbefaling uten å bli
gjort oppmerksom på at han gjør det.

Utover dette: fire av notatets seks påstander om «eksisterende, uendret» infrastruktur holder ikke
mot koden (§B). Migreringskosten i *innhold* er liten — **14 seedede `Tjeneste`-rader**, ikke
hundrevis (§C). Men notatet spesifiserer ikke hvor `Vilkår` og vilkårstreet skal feste seg i den nye
modellen, og det er prosjektets mest utviklede innhold (§D).

**Anbefaling: én kort avklaringsrunde om forholdet til `docs/17` FØR noe annet vurderes.** Se §F.

---

## 2. Hva notatet faktisk foreslår, kort

Slik at resten av vurderingen har et fast referansepunkt:

| # | Forslag | Berører |
|---|---|---|
| §1 | `RettighetEntitet` + `SamhandlingEntitet` + `RettighetForutsetning` + `SamhandlingTilbyder` erstatter `TjenesteEntitet` som kjernemodell | Alt |
| §2 | Empirisk grunnlag: 1485 Altinn-tjenester + 903 skjema klassifisert etter navnemønster | — (begrunnelse) |
| §3 | `ForvaltningsomraadeEntitet` (geografisk master) + `Virksomhet.Organiseringstype` + `forvaltningsomrade_id` | `Virksomhet` |
| §4 | `FeltLovreferanse` — lovreferanse per felt, ikke per entitet | Nytt |
| §5 | `Versjonsmetadata` på alle konsolideringslagets entiteter | Nytt |
| §6 | Innhøstings-/kvalitetssikringslag (`Innhostingskjoring`, `Kildekvalitetsflagg`, `KvalitetssikretBeskrivelse`) | Nytt |
| §7 | SKOS-basert klassifikasjon, flere sektor-ontologier, språk som egen akse | Nytt |

---

## A. Forholdet til `docs/17` — det viktigste funnet

`docs/17-forvaltningsstruktur-master-tjeneste.md` ble skrevet 2026-08-13/14 som svar på et tidligere
ønske fra Johann om «en struktur som reflekterer strukturene i offentlig forvaltning» pluss et
«master tjeneste»-konsept. Den er **merget** (`c099e14`, PR #34) men **parkert**: den venter på svar
på 9 `[PÅ AVKLARING]`-spørsmål i sin §9, og på en seed-liste over organisasjoner Johann har lovet.
Ingen kode er skrevet mot den — bekreftet med negativt søk: `Organisasjonstype`, `Tjenestenivaa`,
`MasterTjenesteId` og `GjelderOrganisasjonstype` gir **null treff** i `src/`.

Notatets §3 og `docs/17` §3–§5 adresserer i stor grad samme problem. Punkt for punkt:

### A.1 Navnekollisjonen — konkret og alvorlig

| | `docs/17` §3 | Notatet §3 |
|---|---|---|
| Feltnavn | `Virksomhet.Organisasjonstype` | `Virksomhet.Organiseringstype` |
| Verdisett | `kommune` \| `fylkeskommune` \| `statsforvalter` \| `tingrett` \| `lagmannsrett` \| `jordskifterett` (6) | `geografisk_forvaltningsledd` \| `sektormyndighet` (2) |
| Svarer på | Hvilken *slags* organ er det — og dermed hvilke instanser er «like» | Sitter organet på den delte geografiske masteren, eller ikke |
| Driver | Master-tjeneste-matching (`GjelderOrganisasjonstype`) | `forvaltningsomrade_id` skal/skal ikke være satt |

Navnene skiller seg med tre bokstaver («Organisasjons-» vs. «Organiserings-»), verdisettene er
disjunkte, og betydningene er forskjellige. Dette er ikke en stilistisk innvending: to felt med
nesten samme navn på samme tabell, der ingen av verdiene overlapper, er en varig kilde til feil i
både kode og redaksjonelt arbeid. **Uansett hvilket forslag som vinner, kan ikke begge navnene
brukes.**

### A.2 Er forslagene forenlige? — I sak ja, i form nei

Substansielt er de i stor grad **komplementære, og hver av dem er ufullstendig der den andre er
komplett**:

- **`docs/17`s 6 verdier har ingen plass til sektormyndigheter.** NAV, Skatteetaten, Tolletaten,
  Digdir og Lånekassen passer ikke inn i noen av de seks. `docs/17` §9 flagger dette selv som sitt
  andre åpne spørsmål («Er de seks typene fullstendige? Nærliggende kandidater: direktorat,
  departement, helseforetak, interkommunalt samarbeid»). Notatets `sektormyndighet` er nettopp den
  manglende oppsamlingskategorien — **notatet besvarer altså delvis `docs/17`s åpne spørsmål 2.**
- **Notatets 2 verdier har ingen plass til domstolene.** En tingrett er ikke et
  `geografisk_forvaltningsledd` i notatets forstand (notatets geografiske master har nøyaktig tre
  nivåer — `land` \| `fylke` \| `kommune` — og en rettskrets sammenfaller ikke med noen av dem), og
  den er ikke naturlig en `sektormyndighet` heller. `docs/17` §3 gjør et poeng av nettopp dette:
  «domstolene er ikke forvaltning». Notatets todeling har ingen boks for dem.
- De to aksene er dessuten **logisk ortogonale**: «hvilken slags organ» og «hviler organet på delt
  geografisk master» er to spørsmål, ikke ett. `docs/17` §3 argumenterer selv for at
  `Forvaltningsniva` og `Organisasjonstype` er to akser fordi de svarer på ulike spørsmål — samme
  resonnement gjelder en tredje akse.

Den naturlige syntesen er derfor: **behold `docs/17`s finmaskede `Organisasjonstype` (den driver
master-matchingen), legg til notatets `ForvaltningsomraadeEntitet` (den løser noe `docs/17` ikke
løser), og utvid verdisettet med sektormyndighet-kategorier.** Om notatets binære `Organiseringstype`
i tillegg skal lagres eksplisitt, eller utledes av `forvaltningsomrade_id IS NOT NULL`, er en reell
avveining: `docs/17` §5.3 etablerer prosjektets egen norm i motsatt retning av utledning — «en
utledet sannhet er en gjettet sannhet» — så et eksplisitt felt er faktisk i tråd med presedensen.
Men da må navnet endres.

### A.3 `docs/17` §4s «ingen ny entitet»-argument rammer IKKE `ForvaltningsomraadeEntitet`

Dette er verdt å si tydelig, fordi det er lett å lese `docs/17` som et generelt forbud mot nye
tabeller. `docs/17` §4 avviste en egen `Organisasjonstype`-entitet med denne begrunnelsen: den
«ville blitt en oppslagstabell med seks rader som ikke inneholder annet enn et navn, og hvis eneste
funksjon er å være mål for en FK … Typen har ingen egne attributter, ingen livssyklus, ingen
proveniens og ingen redaktør — den *er* en enum.»

Ingen del av det treffer `ForvaltningsomraadeEntitet`. Den har flere hundre rader, et
selvrefererende hierarki (kommune → fylke → land), og egne attributter. Notatets §3 er derfor
**ikke** i konflikt med `docs/17` §4 — de to avviser og foreslår helt forskjellige slags tabeller.
Dette er det klareste tilfellet av reell komplementaritet mellom de to dokumentene.

### A.4 Den ekte motsetningen: `SamhandlingTilbyder` vs. master/instans

Her er forslagene **gjensidig utelukkende**, og dette er den viktigste substansielle konflikten.

Begge dokumentene svarer på samme empiriske observasjon — én nasjonal tjenestebeskrivelse, flere
geografiske tilbydere. Notatets §2 tallfester den: 131 av 288 Statsforvalter-tjenester (46 %) har
flere tilbydere med samme navn/URL/beskrivelse og ulikt orgnummer. `docs/15` §4 og `docs/17` §1
beskriver samme fenomen for 357 kommuner.

Men løsningene er ikke bare forskjellige, de er uforenlige:

| | `docs/17` §5.3 (alternativ C) | Notatet §1 (`SamhandlingTilbyder`) |
|---|---|---|
| Form | Én master-rad + N instans-rader, koblet med `MasterTjenesteId` | Én `Samhandling`-rad + N rader i en kobletabell |
| Antall innholdsrader | 1 + N | **1** |
| Hvor bor lokal variasjon | På instans-raden | **Ingensteds** |

Det siste er avgjørende. `docs/17` §5.5 tabellerer eksplisitt hvilke CPSV-felt som varierer per
organisasjon, med belegg i den faktisk seedede raden: `KompetentMyndighet`, `Kontaktpunkt`,
`Kostnad` («Bevillingsgebyr fastsatt av kommunestyret»), `Behandlingstid` («Inntil 3 måneder»),
`Kanaler` og vilkårstreets rotnode er **instansnivå** — kommunen bestemmer verdien. Under
`SamhandlingTilbyder` finnes det bare én innholdsrad, så disse feltene har ingen plass å variere.
Kobletabellen bærer kun `organisasjonsnummer`.

Det er sannsynligvis også grunnen til at notatet **umiddelbart etter** å ha foreslått
`SamhandlingTilbyder` må stille sitt eget åpne spørsmål 2 om `Tjenestemal`: modellen har kollapset
mal/instans-skillet, og notatet merker at det trengs tilbake. `docs/17` §5 er den runden som allerede
gjorde det arbeidet.

### A.5 Notatets åpne spørsmål 2 er `docs/17` §5, gjenåpnet

Notatets §3 avslutter: *«Åpent spørsmål, ikke avgjort her: bør det innføres et eget
`Tjenestemal`-konsept (den nasjonale beskrivelsen) atskilt fra hver geografiske instans, for å unngå
at samme tjenestetekst dupliseres 8 ganger i `SamhandlingEntitet`? Foreslås som egen
beslutningsrunde.»*

Den beslutningsrunden er kjørt. `docs/17` §5.2 utredet tre alternativer, og et eget malkonsept er
alternativ A, som ble **avvist** med tre konkrete argumenter:

1. Det dupliserer CPSV-feltsettet og alt maskineriet rundt: eget statusløp, egen versjonering, egen
   proveniens, egen CRUD-tjeneste, eget API, eget UI.
2. `TjenesteRegelverksreferanseEntitet` har FK `TjenesteId` (`Entiteter.cs:385-391`) og kan ikke
   peke på en mal — men det er nettopp malen som skal bære de *felles* hjemlene (alkoholloven
   §4-1…§4-7). Man måtte bygget en parallell referansetabell.
3. `docs/17` §5.3s alternativ C oppnår det samme med tre nye kolonner, ingen ny tabell og ingen ny
   entitet, ved å gjenbruke `VirksomhetId IS NULL`-konvensjonen som **allerede** betyr
   «samlenivå/nasjonal» fire steder i kodebasen (rettskilde, hendelse, kodeliste, parameterverdi —
   `docs/17` §2.3).

Argument 2 er det som endrer seg mest under den nye modellen, og verdt å merke: hvis
`RettighetEntitet` er den forfattede, nasjonale enheten — notatet sier selv at `Navn` er «forfattet
i Regellaget, ikke hentet 1:1 fra kilde» — mens `SamhandlingEntitet` bærer `kilde_id` og er per
kilderad, så **inneholder notatets modell allerede mal/instans-skillet**, og `Tjenestemal` er
overflødig. Det er en god mulig utgang på spørsmålet. Men den forutsetter at
`SamhandlingTilbyder` fjernes eller omformes (§A.4), og at det avklares om `Rettighet` er
nasjonal-per-definisjon eller kan finnes per kommune.

### A.6 Hva skjer med `docs/17`s 9 åpne spørsmål?

Min vurdering, spørsmål for spørsmål:

| `docs/17` §9-spørsmål | Status hvis notatet godkjennes |
|---|---|
| 1. Bygge nå, eller vente på seed-listen? | **Endres.** Svaret blir «vent» nesten automatisk: innføres `ForvaltningsomraadeEntitet`, må seed-listen (§7) ha en ny kolonne eller en andre fil, siden geografisk område og organisasjon nå er to rader |
| 2. Er de seks organisasjonstypene fullstendige? | **Delvis besvart av notatet** — `sektormyndighet` er den manglende kategorien. Men domstolene mangler fortsatt i notatets todeling (§A.2) |
| 3. Domstolene og `Forvaltningsniva` | **Uendret.** Ingen av modellene løser den; notatet nevner ikke domstoler |
| 4. Har en domstol «tjenester» i CPSV-forstand? | **Uendret, men lettere.** «Samhandling» er et mer romslig ord enn «tjeneste» for en dømmende handling |
| 5. Hvem eier og kan endre en master? | **Uendret og fortsatt kritisk.** Gjelder like fullt en delt `Rettighet` |
| 6. Skal Agder/Tønsberg/Bærum fylles ut? | **Uendret** |
| 7. Ordvalg (`Tjenestenivaa`, master/instans) | **Utvides.** Nå også `Rettighet`/`Samhandling` vs. `Tjeneste`, og `Organisasjonstype` vs. `Organiseringstype` |
| 8. Trengs organisatorisk hierarki mellom virksomheter? | **Delvis besvart — og bedre.** Notatets §3 skiller geografisk hierarki fra organisatorisk, som er et skarpere svar enn `docs/17`s «antar nei» |
| 9. (§5.6) Skal den uscopede skrivetilgangen lukkes? | **Uendret og mer alvorlig.** Se §D.5 |

**Konklusjon på §A:** `docs/17` bør **ikke skrotes**, og den står ikke i veien. Den bør
**oppdateres**, fordi to av dens fem hovedspørsmål (§3 organisasjonstype, §5 master-tjeneste) nå har
et konkurrerende svar liggende, og fordi dens §4 «ingen ny entitet»-konklusjon er riktig for det den
avviste men ikke dekker notatets geografiske master. Det som **ikke** kan skje er at Johann besvarer
notatets åpne spørsmål 2 uten å se `docs/17` §5 først.

---

## B. Verifisering av notatets faktapåstander mot koden

Notatet beskriver flere ting som «eksisterende, uendret» eller som «samme mønster som». Hver av dem
er sjekket. Fire av seks holder ikke.

### B.1 `Registertype: 'tjeneste' | 'forvaltningsoppgave'` — «eksisterende diskriminator, uendret betydning»

**STEMMER IKKE.** Feltet finnes ikke i koden. Negativt søk: `Registertype`, `registertype` og
`forvaltningsoppgave` gir **null treff** i `src/` — verken i `Entiteter.cs`, `RegelIdeDbContext.cs`
eller i noen av de 26 migrasjonene.

Det er **[LÅST]** som *design* i `docs/15` §10.2 (avklaringsrunde 1, 2026-08-12), men aldri kodet.
`docs/16` §1 lister nettopp «ingen `Registertype`» blant det som mangler, og `docs/17` §2.2 kaller
det «[LÅST], ikke kodet».

Dette er mer enn en presisjonsfeil, fordi notatet arver feltet inn i `RettighetEntitet` som om
betydningen var etablert i kode. Og betydningen er smalere enn notatet antyder: `Registertype`s
låste jobb er **eksportslusen**, ikke semantisk klassifisering. `docs/15` §10.2, ordrett:

> Bare `Registertype = tjeneste` emitteres som `cpsv:PublicService` i ekstern eksport.

…og slusen er låst som **strukturell**: «ingen kode utenfor et dedikert repository-lag får røre
`TjenesteEntitet` direkte for eksportformål … pluss én regresjonstest som seeder en
`Registertype="forvaltningsoppgave"`-rad, kjører CPSV-eksporten, og asserterer at raden er FRAVÆRENDE
i output.» Notatets forslag om å gate slusen på `Rettighet` er forenlig med det. Notatets åpne
spørsmål 1 er det ikke — se §E.1.

### B.2 «Kontroll ortogonal i stedet for særtilfelle» via `utfort_av`

**STEMMER DELVIS — og notatets §1 er internt inkonsistent her.** Som tegnet plasserer notatets §1
`Registertype: 'tjeneste' | 'forvaltningsoppgave'` på `RettighetEntitet` **og** `'Kontroll'` som
`Samhandlingstype`-verdi **og** `utfort_av: 'virksomhet' | 'forvaltning'` på `SamhandlingEntitet`.
Kontroll/forvaltningsoppgave er altså kodet tre steder samtidig i samme skisse. Notatets åpne
spørsmål 1 spør om å fjerne én av dem — men modellen som tegnet må velge før noe kan kostnadsberegnes.

`utfort_av`-aksen i seg selv er additiv og besvarer noe ingenting i dag besvarer (retningen på
samhandlingen). Det er den minst kontroversielle delen av forslaget.

### B.3 `foreslatt_av_ai: bool` — «samme kontrakt som sweep-arkitekturen for rettskilder»

**STEMMER IKKE — og navnet notatet velger finnes faktisk allerede, i en annen rolle.** Dagens
KI-forslagsarkitektur er ikke et boolsk flagg. Et forslag representeres som en **statusverdi i
entitetens eget livsløp** pluss en **proveniensrad** som bærer sporbarheten.
`ProveniensHjelper.NyForslagRad` (`ProveniensHjelper.cs:29-42`) er den delte inngangen, og den setter:

```csharp
Handling = "foreslatt_av_ai",
AiForslagVersjon = aiForslagVersjon,
KildeReferanserJson = kildeReferanserJson,
```

`foreslatt_av_ai` er altså i dag en **`Handling`-verdi på en proveniensrad**, ikke et felt på
entiteten — og den raden bærer i tillegg hvilken agent/modellversjon som foreslo, og hvilke
kildereferanser forslaget hviler på. `TjenesteforslagTjeneste.cs` (310 linjer) og
`BegrepsforslagTjeneste.cs` produserer forslag gjennom den veien.

Forskjellen er ikke kosmetisk. Et `bool` kan ikke uttrykke overgangen
foreslått → under_revisjon → validert, kan ikke bære hvilken agent/modellversjon som foreslo raden,
og kan ikke bære kildereferansene forslaget hviler på. Skal `SamhandlingEntitet` faktisk ha «samme
kontrakt», trenger den statusløpet og proveniensraden — ikke ett felt. Det er en større, men også
mer verdifull, endring enn notatet beskriver. Verre: innføres `foreslatt_av_ai` *også* som boolsk
felt, finnes samme begrep to steder med ulik form, og den ene kan være usann mens den andre er sann.

### B.4 `Versjonsmetadata` — «gjenbruk av samme temporale mønster som AKN/`temporalGroup`»

**STEMMER IKKE, på to måter.**

For det første: `temporalGroup` er ikke et implementert mønster i denne kodebasen — det er eksplisitt
**forkastet** i den ene koden som kunne brukt det. `AknXmlSkriver.cs:166-172`, ordrett:

> AKNs offisielle temporal-mekanisme (attributtet "period" pekende til en `<temporalGroup>` i
> `<lifecycle>`) er ikke implementert her — det tidligere "end"-attributtet fantes ikke i noe
> attributeGroup skjemaet definerer for hierarkiske elementer og var derfor rett og slett ugyldig
> (bekreftet ved skjemavalidering …). `regelIde:`-attributter er derimot skjemalovlige … og brukes i
> stedet.

Ellers forekommer ordet bare to steder i dokumentasjonen (`docs/15:690` som en AKN-XML-mappingtabell,
`docs/15:1002` som noe som skal *verifiseres* mot skjemaet). `docs/08` Vedlegg A.9 omtaler AKNs
`start`/`end`-attributter som riktig idiomatikk for opphevelser — men det er et **XML-utdataformat**,
ikke et persisteringsmønster, og koden over viser at selv der vant `regelIde:opphevet`.

Å begrunne `Versjonsmetadata` med «gjenbruk av AKN/`temporalGroup`, IKKE en ny ad hoc-løsning» peker
altså på det ene mønsteret prosjektet har prøvd og forlatt.

For det andre — og viktigere — **det finnes et ekte, etablert versjoneringsmønster, og
`TjenesteEntitet` har det allerede i sin helhet** (`Entiteter.cs:367-375`):

```csharp
public int Versjon { get; set; } = 1;
public string Entitetsstatus { get; set; } = "gjeldende";
public Guid? ErstatterId { get; set; }
public DateOnly? GyldigFra { get; set; }
public DateOnly? GyldigTil { get; set; }
public required string OpprettetAv { get; set; }
public DateTimeOffset OpprettetTidspunkt { get; set; }
public string? SistEndretAv { get; set; }
public DateTimeOffset? SistEndretTidspunkt { get; set; }
```

Samme blokk finnes på `BegrepEntitet` (`:499-507`) og `KodelisteEntitet` (`:530-538`), og
`ErstatterId` brukes på **7 entiteter** (`Entiteter.cs:80, 369, 501, 532, 657, 701, 749` — én per versjonert register).

Notatets forslag er altså i praksis oppfylt for det som allerede finnes — men det peker på feil
presedens, **og det peker `erstattet_av_id` motsatt vei av den etablerte `ErstatterId`** (som peker
bakover, på raden denne raden erstatter). Å innføre en fremoverpekende variant ved siden av en
bakoverpekende brukt 7 steder er en varig inkonsistens. Notatet mangler også `Versjon` og
`Entitetsstatus`, som er de to feltene resten av kodebasen faktisk filtrerer på.

`docs/08` §2.1 er dessuten eksplisitt om at versjonering skjer på **dokumentnivå, ikke nodenivå**,
og at «dette krever ingen egen nodeversjonstabell» — verdt å ha med før `Versjonsmetadata` gjøres til
egen tabell.

### B.5 `FeltLovreferanse` — «samme granularitet som vilkårsanalysen, utvidet»

**STEMMER DELVIS.** Kobling mellom entitet og rettskildeparagraf finnes flere steder allerede:
`TjenesteRegelverksreferanseEntitet` (`Entiteter.cs:385-391`, FK `TjenesteId` +
`TilRettskildeId` + påkrevd `TilEid`), `RettskildeReferanseEntitet`, og
`BegrepEntitet.LovreferanseEid` (`:493`, «validert mot RettskildeNoder ved lagring»).
`LokalEid`/`eId`-mønsteret notatet vil gjenbruke er ekte og låst i `docs/08` §1.2.

Det som er **genuint nytt** er `feltnavn`-aksen — å si at *dette bestemte feltet* har *denne*
hjemmelen. Ingenting i dag gjør det.

Men notatet bør møte en presedens som taler imot en generisk tabell. `Entiteter.cs:892-905`
dokumenterer en avgjørelse fra en tidligere runde om **ikke** å konvergere to referansetabeller,
nettopp fordi feltene ikke passer uten friksjon og fordi det ville gitt «alltid-NULL-kolonner … samme
antipattern `HandbokKommentarMetadataEntitet` og `RettskildeNodeEmbeddingEntitet` allerede unngår ved
å være egne tabeller». En `FeltLovreferanse` med `entitet_type ∈ {Rettighet, Samhandling,
RettighetForutsetning, Vilkaar}` er nøyaktig den generiske formen den avgjørelsen valgte bort. Det
betyr ikke at forslaget er galt — men det må begrunnes mot den presedensen, ikke rundt den.

### B.6 «Tønsberg/Bærum-retningslinjer 2024–2028» som begrunnelse for versjonering

**STEMMER DELVIS.** `KommunaleParametreSeed.cs` seeder ekte, ulike parameterverdier for Tønsberg og
Bærum, pluss en `VirksomhetId = null`-rad som nasjonal standardverdi — mønsteret `docs/17` §2.3
bygger på. Årstallene er der, men som **fritekst i et kildefelt**, ikke som strukturert
gyldighetsperiode: `DatasettVerdiEntitet` har ingen `GyldigFra`/`GyldigTil`.

Notatets poeng — at et tidsavgrenset regelsett ikke skal fremstå som evigvarende sannhet — er altså
**riktig, og peker på et ekte hull**. Men hullet er i `DatasettVerdiEntitet`, en entitet notatet ikke
nevner, ikke i den nye kjernemodellen.

### B.7 `Organisasjonsnummer`-i-URI-beslutningen

**STEMMER.** Notatets §3 gjengir `docs/15` §3.3 korrekt: kommunenummer er ikke stabilt over tid
(Bergen 1201 før 2020, 4601 etter), `Organisasjonsnummer` bærer URI-nøkkelen, kommunenummer er et
attributt og aldri en nøkkel. Dette er den ene sentrale påstanden om låst beslutning som notatet
gjengir helt presist. Men se §D.1 for konsekvensen notatet ikke trekker.

### B.8 `Virksomhet` som den ser ut i dag

**STEMMER.** `Entiteter.cs:9-28` har `Id`, `Navn`, `Organisasjonsnummer`, `OpprettetTidspunkt`,
`Kommunenummer` (`:23`) og `Forvaltningsniva` (`:27`). Ingen `Organiseringstype`, ingen
`forvaltningsomrade_id`. Verdt å legge til, som `docs/17` §2.1 fant: **ingen kode leser
`Forvaltningsniva` eller `Kommunenummer` for logikk** — eneste skriving er
`BergenKorpusSeed.cs:83`, eneste lesing er en testassert (`BergenKorpusSeedTests.cs:46`). Det finnes
heller ingen `CHECK`-constraint på `forvaltningsniva` (`RegelIdeDbContext.cs:101`). Notatets nye
felt vil altså legges ved siden av to felt som er uleste data i dag.

### B.9 Er §2-tabellen internt konsistent?

**STEMMER — regnestykket er riktig.** Begge kolonnene summerer eksakt til sitt n, og prosentene til
100:

| | Altinn | Oppgaveregisteret |
|---|---|---|
| Sum rader | 286+62+20+2+10+129+312+664 = **1485** ✓ | 148+84+6+1+5+93+208+358 = **903** ✓ |
| Sum prosent | 19+4+1+0+1+9+21+45 = **100** ✓ | 16+9+1+0+1+10+23+40 = **100** ✓ |

Én liten unøyaktighet: notatet skriver «navnemønster alene dekker under 60 %». For Altinn er
klassifisert andel 821/1485 = 55,3 %, som stemmer. For Oppgaveregisteret er den 545/903 = **60,4 %**,
altså marginalt *over*. Ubetydelig for konklusjonen, men verdt å rette.

### B.10 Følger konklusjonen «feil abstraksjonsnivå» av tallene? — Delvis. Det er et sprang.

Notatets §0 hevder: «en enkelt rad fra Altinn/Oppgaveregisteret beskriver **nesten alltid** en
samhandling om en rett, ikke retten selv». Tabellen i §2 viser ikke det:

| Kategori | Altinn | Oppgaveregisteret |
|---|---|---|
| Klassifisert som samhandling (Etablering + Periodisk + Endring + Utvidelse + Kontroll + Annen) | 509 = **34 %** | 337 = **37 %** |
| Klassifisert som Rettighet (kort tittel, intet verb) | 312 = **21 %** | 208 = **23 %** |
| Ikke klassifisert | 664 = **45 %** | 358 = **40 %** |

Det tabellen faktisk viser er at **omtrent en tredjedel** er påvisbart samhandlinger, at **omtrent en
femtedel er påvisbart *ikke* samhandlinger** (de er retten selv — notatet sier dette selv, og at de
ikke skal tvinges inn i mønsteret), og at **40–45 % er ukjent**. «Nesten alltid» krever at nesten
hele den uklassifiserte resten faller på samhandlingssiden. Det kan godt være riktig, men det er en
antakelse tabellen ikke belegger.

Det svakere utsagnet — *«en betydelig andel av kilderadene er samhandlinger om en rett, og
minst en femtedel er retten selv; ingen av de to får en riktig plass i en 1:1-modell mot kilderaden»*
— følger derimot av tallene, og er tilstrekkelig til å begrunne at abstraksjonsnivået bør vurderes.
Konklusjonens retning holder; styrken i formuleringen gjør det ikke. Verdt å justere, siden notatet
ellers er nøye med å skille «empirisk» fra «tolkning» og selv merker §2 som empirisk.

`tilbys_av`-funnet (131 av 288 = 46 %) er derimot både empirisk og direkte
strukturbegrunnende — det er notatets sterkeste tall.

---

## C. Bloss-radius og migreringskost — konkrete tall

Alle tall er telt, ikke anslått.

### C.1 Kode

| Mål | Tall |
|---|---|
| `.cs`-filer som refererer `TjenesteEntitet` eller `TjenesteId` | **36** (18 utenfor `Migrasjoner/`, 18 i) |
| Migrasjoner totalt / som berører `tjenest`-tabeller | **26 / 22** |
| Entiteter med FK inn i `Tjeneste` | **4** — `TjenesteRegelverksreferanseEntitet` (`:388`), `TjenesteHendelseEntitet` (`:427`), `TjenesteavhengighetEntitet` (`:443-444`, to FK-er), `VilkarEntitet.TjenesteId` (`:621`) |
| API-endepunkter i `/api/tjenester`-gruppen | **20** (+ `GET /api/rettskilder/{id}/referert-av-tjenester` og `tjenesteId`-filteret på `/api/vilkar`) |
| Sentrale backend-tjenester, linjer | **1095** — `TjenesteregisterTjeneste` 240, `TjenesteforslagTjeneste` 310, `VilkarregisterTjeneste` 196, `RegelnoderegisterTjeneste` 190, `TjenesteavhengighetregisterTjeneste` 159 |
| Frontend `Tjeneste*`-sider, linjer | **4 filer / 1237 linjer** — `TjenesteDetalj.tsx` 635, `TjenesteforslagKo.tsx` 272, `TjenesteVeiledning.tsx` 168, `TjenesterListe.tsx` 162 |
| `.ts`/`.tsx`-filer som nevner `tjeneste` | **19** |
| Testfiler som nevner `Tjeneste` | **39** (27 testmetoder i de fire mest Tjeneste-sentrale) |

### C.2 Innhold — og her er den gode nyheten

**Det seedes bare 14 `Tjeneste`-rader i hele kodebasen.** Ikke hundrevis.

| Kilde | Rader | Hva |
|---|---|---|
| `Byggesteg2InnholdSeed.cs:114-135` | **1** | «Alminnelig skjenkebevilling» — full CPSV-utfylling + 7 regelverksreferanser (§4-1…§4-7) |
| `FasitRunde4Seed.cs:184-193` | **13** | `RelevanteTjenester`-listen, ordrett fra rundskriv-fasitens §12 |
| `BergenKorpusSeed.cs`, `AgderFylkeskommuneSeed.cs`, `KommunaleParametreSeed.cs`, `TestkommuneInnholdSeed.cs` | **0** | Seeder virksomheter, rettskilder og parametre — ingen tjenester |

I tillegg henger **5 `Vilkår` + 1 rotnode** på den ene skjenkebevillingsraden
(`Byggesteg4VilkarstreSeed.cs:59-112`).

Migreringen av *innhold* er altså liten nok å gjøre manuelt og med omhu. Det er den ene tydelige
grunnen til at dette forslaget kommer på et gunstig tidspunkt: **kostnaden er i kode og i låste
beslutninger, ikke i data.**

### C.3 De 14 radene er tilfeldigvis en nesten perfekt testfixture for notatets tese

Dette er verdt egen plass, fordi det både styrker notatet og avdekker et hull. `RelevanteTjenester`
er 13 titler hentet ordrett fra rundskriv-fasiten, og de faller nesten mistenkelig pent inn i
notatets vokabular:

| Seedet tittel | Notatets kategori |
|---|---|
| Alminnelig skjenkebevilling, Serveringsbevilling, Salgsbevilling | Rettighet (kort tittel, intet verb) |
| Etablererprøven, Kunnskapsprøvene | Rettighet/kvalifikasjon — og opplagte `forutsetter_rettighet_id` |
| Omsetningsoppgave og bevillingsgebyr | `PeriodiskRapportering` |
| Utvidelse av skjenkebevilling for en enkelt anledning | `Utvidelse` |
| Endringer i driften …, Endring av eiere …, Eierskifte og drift i overgangsperioden … | `Endringsmelding` |
| Kontroller av salgs- og skjenkesteder | `Kontroll` (`utfort_av='forvaltning'`) |
| Skjenkebevilling for et arrangement | Rettighet *eller* `Etablering` — uklart |
| **Oppsigelse av bevilling** | **Ingen** av de fem verdiene passer |
| **Konsekvenser ved brudd på regelverket** | **Ingen** — og hører nærmere `HendelseEntitet` |

Prøvd mot prosjektets egne data har `Samhandlingstype` altså **to hull av fjorten**: opphør/avvikling
av en rett, og sanksjon/konsekvens. Begge er ekte livsløpshendelser for en bevilling. Anbefaling:
utvid vokabularet med minst `Opphor` før det låses — testet mot fjorten rader, ikke mot 2388.

---

## D. Hull notatet ikke selv har flagget

### D.1 [HULL] `ForvaltningsomraadeEntitet` har ingen stabil ekstern nøkkel

Notatets §3 anvender den låste `docs/15` §3.3-begrunnelsen korrekt — kommunenummer er attributt,
aldri nøkkel — men trekker ikke konsekvensen. Beslutningen fungerer for `Virksomhet` **fordi
`Organisasjonsnummer` finnes** som en stabil, ekstern nøkkel å falle tilbake på. Et geografisk
forvaltningsområde er ikke en organisasjon og har **ingen** slik nøkkel: kommunenummer er det eneste
eksterne håndtaket, og det er eksplisitt forkastet.

`ForvaltningsomraadeEntitet` ville dermed hatt kun en intern surrogat-`Guid` som identitet. Det
kolliderer direkte med notatets **eget** §6: `Innhostingskjoring` skal beregne «diff mot forrige
kjøring: nye / endrede / fjernede rader», og `Kildekvalitetsflagg` og `kilde_versjon` forutsetter at
en rad kan gjenkjennes på tvers av kjøringer. Uten stabil nøkkel har forsoningen ingenting å matche
på. Dette må løses før §3 og §6 kan bygges sammen — enten med en dokumentert, intern
identitetskonvensjon (og da eksplisitt, jf. `docs/08` §1.2.1s `canonical_id`/`source_id`-prinsipp,
som er laget for nettopp dette), eller ved å akseptere kommunenummer som nøkkel *for denne tabellen*
med en skrevet begrunnelse for hvorfor det er forsvarlig her.

### D.2 [HULL] Hvor fester `Vilkår` og vilkårstreet seg? — Modellens største uspesifiserte punkt

`VilkarEntitet.TjenesteId` (`Entiteter.cs:621`) er nullbar FK inn i `Tjeneste`, og
`TjenesteEntitet.RotnodeId` (`:381`) peker til vilkårstreets rotnode. Notatet nevner `Vilkaar` **kun**
som en mulig `entitet_type`-verdi i `FeltLovreferanse` (§4) — og sier ingenting om hvor et vilkår
hører i `Rettighet`/`Samhandling`-delingen.

Spørsmålet er ikke akademisk, og det har to plausible og uforenlige svar:

- **På `Rettighet`**: vilkårene for å *ha* retten (vandel, kunnskapsprøve) er egenskaper ved retten,
  uavhengig av hvilken samhandling som utløser vurderingen.
- **På `Samhandling`**: vilkårene vurderes i en konkret søknadsbehandling, og en `Utvidelse` har
  andre vilkår enn en `Etablering`.

Begge er forsvarlige. Men dette er prosjektets **mest utviklede innhold**: skjenkebevillingens
vilkårstre er fasit-leveransen (`docs/12`), med 5 vilkår, rotnode, kommentarer og vilkårstre-graf, og
det er det byggesteg 4 finnes for. At notatet ikke nevner det er det største enkeltstående hullet —
og det bør besvares før noe bygges, ikke underveis.

### D.3 [HULL] `RettighetForutsetning` finnes nesten allerede — og en låst beslutning gjelder

`TjenesteavhengighetEntitet` (`Entiteter.cs:432-459`) er allerede en rettet M:N-graf mellom
tjeneste-rader, med `Rel`-verdier som inkluderer **`'forutsetning_for'`**, og med bounded sykelsjekk
i `TjenesteavhengighetregisterTjeneste.LukkerSykelAsync`. Notatets `RettighetForutsetning` beskrives
som «selvrefererende DAG, IKKE tre» — som er nøyaktig det som er bygget, inkludert asyklisitetsvernet.

Notatet nevner ikke entiteten. Det eneste genuint nye er `obligatorisk: bool` og
`FeltLovreferanse`-koblingen (en avhengighetsrad har ingen hjemmelsreferanse i dag).

Merk samtidig at `docs/15` §10.2 **[LÅST]** at «en relasjon med annen semantikk skal ikke presses inn
i `TjenesteavhengighetEntitet`s sykelsjekkede graf». Her er semantikken den *samme*
(«forutsetning for»), så gjenbruk er sannsynligvis riktig og i tråd med beslutningen — men det skal
avgjøres eksplisitt, ikke ved at en ny tabell innføres uten at den eksisterende nevnes.

### D.4 [HULL] `Kontroll` kolliderer med `HendelseEntitet`, som allerede finnes

`HendelseEntitet` (`Entiteter.cs:395-416`) er et bygget, delt register (byggesteg 2, ferdig
2026-07-31 per `docs/13` §1) med `VirksomhetId = null`-mønsteret for nasjonale hendelser. Dens
dokumentasjonskommentar (`:398-399`) er direkte relevant:

> En Hendelse er alltid et EKTE, eksternt fenomen som skjer MED en virksomhet (eierskifte,
> **kontroll/tilsyn**, brudd, avvikling) — aldri en tjenestes eget resultat/utfall.

Kontroll/tilsyn, brudd og avvikling er altså allerede modellert som hendelser, koblet til tjenester
via `TjenesteHendelseEntitet` og via `TjenesteavhengighetEntitet.Rel='utlost_av'` + `HendelseId`.
Notatets `Samhandlingstype='Kontroll'` og de to vokabularhullene fra §C.3 (`Oppsigelse av bevilling`,
`Konsekvenser ved brudd`) treffer **nøyaktig** de tre ordene i den kommentaren. Forholdet mellom
`Samhandling` og `Hendelse` må avklares, ellers får prosjektet to registre for samme fenomen.

### D.5 [HULL] KI-forslagspipelinen må skrives om, og notatet nevner det ikke

`TjenesteforslagTjeneste.cs` (310 linjer) produserer i dag `TjenesteEntitet`-forslag, konsumert av
`TjenesteforslagKo.tsx` (272 linjer) via tre endepunkter (`/forslag`, `/forslag/kjor`,
`/forslag/kjor-rag`). Byggesteg 5 er `docs/13` §1s mest utbygde steg — fire runder, med ekte
KI-leverandør, RAG-spike, retry-hjelper og målinger (89,7 % feltfullstendighet, `docs/13` §4 punkt 7).

Under `Rettighet`+`Samhandling` må agenten ikke bare fylle andre felt — den må **ta en ny beslutning
den ikke tar i dag**: er dette funnet en Rettighet eller en Samhandling, og hvilken Rettighet hører
Samhandlingen til? Notatets §2 sier selv at navnemønster bare klassifiserer 55–60 %, og at resten
«krever manuell klassifisering av en jurist/informasjonsforvalter». Det er altså kjent at oppgaven er
vanskelig — men konsekvensen for den eksisterende agentpipelinen er ikke nevnt noe sted i notatet.
Dette er reelt arbeid, og det gjør byggesteg 5s målinger ugyldige som sammenligningsgrunnlag.

### D.6 [HULL] Klassifikasjonslaget forholder seg ikke til `Begrep` og `Kodeliste`

Notatets §7 innfører `Klassifikasjonssystem`, `KlassifikasjonsBegrep` og `SpraakVariant`. To
eksisterende entiteter dekker nærliggende behov:

- `BegrepEntitet` (`:484-508`) **er allerede SKOS**: `Term` er kommentert `skos:prefLabel`,
  `Definisjon` er `skos:definition`, og `SkosUrl` er «publisert URI i Felles datakatalog».
- `KodelisteEntitet` med `Type='ekstern-referanse'` (`:516-541`) har `EksternKildeUri` og
  `EksternKildeVersjon`, og er dokumentert som formen for «refererer en autoritativ kilde,
  dupliserer ikke» — som er nesten presis definisjonen på notatets `Klassifikasjonssystem`.

Notatets `SpraakVariant`-akse (Vergemål/Verjemål som samme begrep) er et ekte og godt funn, men den
hører logisk også på `BegrepEntitet`, som i dag har én `Term` og ingen språkvarianter. Å innføre
språkvarianter kun i et nytt klassifikasjonslag gir prosjektet to halve løsninger.

Verdt å merke som ren informasjon: `Tjeneste` har **ingen** tema-/sektor-/klassifiseringsfelt i dag —
negativt søk på `Tema`, `Klassifis`, `Sektor` i `Entiteter.cs` gir bare
`TjenesteHendelseEntitet`-kommentaren. §7 er altså helt grønn mark; ingenting skal migreres, men
ingenting finnes å henge det på heller.

### D.7 [HULL] Delte `Rettighet`-rader gjør et kjent sikkerhetshull verre

`docs/17` §2.2 verifiserte at bare listeendepunktet er virksomhet-scopet:
`TjenesteregisterTjeneste.ListerForAsync` filtrerer på `VirksomhetId`, men `FinnAsync`,
`OppdaterAsync`, status-, rotnode- og regelverksreferanse-veiene filtrerer **kun** på
`Entitetsstatus`. Enhver innlogget bruker kan i dag lese og skrive enhver tjeneste hvis hun har
id-en.

Er `Rettighet` en nasjonal, delt rad (som notatets «forfattet i Regellaget» antyder), betyr det
samme hullet at hvem som helst kan endre en rett som gjelder alle. `docs/17` §5.6 punkt 2 og §9
flagget dette; notatet gjentar ikke problemet, men arver det i forsterket form.

### D.8 [HULL] Eksportslusens regresjonstest må omskrives

`docs/15` §10.2 krever **[LÅST]** en regresjonstest som seeder en
`Registertype='forvaltningsoppgave'`-rad, kjører CPSV-eksporten og asserterer at raden er fraværende.
Flyttes eksportenheten til `Rettighet`, endres testens form: predikatet går fra ett kolonnefilter på
den eksporterte tabellen til en vurdering over `Samhandling → Rettighet`. Slusen skal fortsatt være
«strukturell, ikke basert på disiplin» — det er den låste delen — og det blir vanskeligere, ikke
lettere, å garantere. Notatet påstår at slusen «bør gate på `Rettighet`» uten å behandle hva det gjør
med kravet.

---

## E. Vurdering av notatets fire åpne spørsmål

### E.1 Bør `Kontroll`/forvaltningsoppgave bli en `Samhandlingstype` med `utfort_av`-akse, i stedet for en `Registertype`-verdi?

**Min vurdering: nei, ikke som stilt — men ja til `utfort_av` som tillegg. De to utelukker ikke
hverandre, og notatet overdriver konflikten med den låste beslutningen.**

Tre grunner:

1. **`Registertype` er en eksportsluse, ikke en klassifisering.** Dens låste jobb (§B.1) er å avgjøre
   hva som emitteres som `cpsv:PublicService`. Blir «forvaltningsoppgave» i stedet en
   `Samhandlingstype`-verdi, går slusepredikatet fra ett kolonnefilter til en join-og-aggregering, og
   den låste «strukturell, ikke disiplin»-egenskapen blir vanskeligere å garantere (§D.8). Formålet
   `Registertype` finnes for, forsvinner ikke fordi modellenheten skifter.
2. **Modellen som tegnet dobbeltkoder allerede** (§B.2) — `Registertype` på Rettighet *og* `Kontroll`
   som Samhandlingstype *og* `utfort_av`. Spørsmålet burde derfor være «hvilken av de tre fjernes»,
   og svaret er sannsynligvis `Kontroll`-verdien: den er utledbar fra `utfort_av='forvaltning'`.
3. **Det finnes et tredje, bygget hjem for tilsyn** som verken spørsmålet eller notatet nevner:
   `HendelseEntitet` (§D.4). Før en tredje koding innføres, bør forholdet til den avklares.

`utfort_av` er derimot additiv, billig og besvarer noe ingenting besvarer i dag. Den ville jeg
anbefalt uansett utfallet på `Registertype`. Merk at siden `Registertype` **aldri er kodet**, er
dette ikke å bryte en implementert beslutning — det er å justere en beslutning før den bygges, som er
en billigere og mindre dramatisk handling enn notatet fremstiller. Men det krever fortsatt Johanns ja,
siden `docs/15` §10.2 er merket [LÅST].

### E.2 `Tjenestemal` som eget konsept?

**Min vurdering: sannsynligvis nei — men dette spørsmålet må ikke besvares uten `docs/17` §5 på
bordet.** Se §A.5. `docs/17` utredet dette som alternativ A og avviste det med tre konkrete
argumenter, og anbefalte i stedet alternativ C (tre nye kolonner, ingen ny tabell, gjenbruk av den
etablerte `VirksomhetId IS NULL`-konvensjonen).

Det som er nytt under notatets modell, og som kan gjøre spørsmålet overflødig: hvis `Rettighet` er
den forfattede, nasjonale enheten og `Samhandling` er den kildeforankrede, per-instans-enheten, så
**er** mal/instans-skillet allerede i modellen. Da er `Tjenestemal` en tredje etasje ingen har bedt
om. Men det forutsetter to avklaringer: at `SamhandlingTilbyder` omformes slik at lokal variasjon har
et sted å bo (§A.4), og at det avgjøres om en `Rettighet` er nasjonal per definisjon eller kan
finnes per kommune.

### E.3 Statsforvalteren som frittstående statlig linje?

**Det juridisk-administrative spørsmålet er utenfor min kompetanse. Men strukturelt kan jeg si to
nyttige ting, og det ene er potensielt blokkerende.**

For det første, det ikke-blokkerende: modellen trenger ikke svaret for å kunne bygges. Notatets
`Overordnet_id` er eksplisitt geografisk (kommune → fylke → land), og notatet sier selv at den
antyder ingen kommandolinje. Risikoen er **presentasjonell**, ikke strukturell — et UI som tegner
hierarkiet vil *se ut* som et organisasjonskart. Den avvæpnes billig: navngi feltet så det ikke kan
leses som kommando (`geografisk_overordnet_id`), og legg ikke til noe organisatorisk hierarki-felt.
`docs/17` §8 har allerede satt organisatorisk hierarki utenfor scope, og §9 spør om det trengs i det
hele tatt — de to dokumentene peker samme vei her.

For det andre, det som **bør sjekkes før `forvaltningsomrade_id` låses**: notatets modell gir hver
`geografisk_forvaltningsledd` **nøyaktig én** `ForvaltningsomraadeEntitet`-node («Hver peker til
nøyaktig én»). Det holder for en kommune. Det er **ikke verifisert** at det holder for et
statsforvalterembete — dekker ett embete mer enn ett fylke, bryter en enkelt FK sammen, og
statsforvaltere trenger en mange-til-mange-kobling mens kommuner ikke gjør det. Jeg oppgir bevisst
ingen tall for antall embeter eller fylker (samme grunn som `docs/17` §10 ikke oppgir tall for
domstoler: kodebasen er ingen autoritativ kilde, og «ingen gjettet fallback» gjelder også en
vurdering). Men kardinaliteten er et ja/nei-spørsmål som avgjør skjemaformen, og det er billig å
sjekke nå og dyrt å oppdage etter en migrasjon.

### E.4 Full liste over `RettighetForutsetning`-kandidater er ikke kartlagt

**Enig i at den ikke er kartlagt — men det er ikke det som bør bekymre, og oppgaven kan ikke starte
først.**

To mer nyttige observasjoner:

1. **Strukturen finnes sannsynligvis allerede** (§D.3): `TjenesteavhengighetEntitet` med
   `Rel='forutsetning_for'` er en rettet, sykelsjekket DAG mellom rader i samme tabell. Kartleggingen
   trenger derfor kanskje ingen ny tabell i det hele tatt — kun `obligatorisk` og en
   hjemmelsreferanse.
2. **Prosjektet har allerede et lite, ekte testsett** (§C.3): «Etablererprøven» og «Kunnskapsprøvene»
   er seedet som egne rader ved siden av «Alminnelig skjenkebevilling», og kunnskapsprøve →
   skjenkebevilling er nettopp notatets eget eksempel. Kartleggingsmetoden kan altså prøves på
   fjorten rader der svaret er kjent, før den slippes løs på 520 kandidater.

Rekkefølgemessig hører dette **sist**: det er manuelt juristarbeid som forutsetter at
`Rettighet`-radene finnes. Det bør ikke stå i veien for noen av de andre beslutningene, og det bør
heller ikke brukes som argument for å utsette dem.

---

## F. Anbefalt rekkefølge for en avklaringsrunde

Rangert etter hva som blokkerer mest. Punkt 1 er det eneste som må skje før noe annet kan
kostnadsberegnes meningsfullt.

**1. FØRST: `docs/17` vs. notatets §3/§5 — hvilket strukturforslag står?** [PÅ AVKLARING]

Dette blokkerer alt. Begge forslagene endrer `Virksomhet`, begge hevder å løse multi-tilbyder/
samlenivå, og feltnavnene kolliderer nesten (§A.1). Konkret trenger Johann å se `docs/17` §3 og §5
ved siden av notatets §3 og svare på tre ting:

- Beholdes `docs/17`s `Organisasjonstype` (6 finmaskede verdier), notatets `Organiseringstype` (2
  grove), eller begge under nye navn? Min anbefaling: begge akser beholdes fordi de svarer på ulike
  spørsmål, men navnene må skilles og verdisettene utvides — `sektormyndighet` mangler i den ene,
  domstolene i den andre.
- Innføres `ForvaltningsomraadeEntitet`? Min anbefaling: ja — `docs/17` §4s «ingen ny entitet»-
  argument treffer den ikke (§A.3), og den løser noe `docs/17` ikke løser. Men §D.1 (ingen stabil
  nøkkel) må besvares i samme åndedrag.
- Er `docs/17` §5s master/instans-modell erstattet av `SamhandlingTilbyder`, eller består den? Dette
  er den ekte motsetningen (§A.4), og svaret avgjør punkt 2.

**2. Er `Rettighet` malen?** [PÅ AVKLARING]

Ett svar lukker både notatets åpne spørsmål 2 og `docs/17` §5 (§A.5, §E.2). Hvis ja — `Rettighet` er
nasjonal og forfattet, `Samhandling` er kildeforankret og per instans — er både `Tjenestemal` og
`docs/17`s master-tjeneste overflødige, og `docs/17` §5 kan lukkes som besvart i stedet for å stå
åpen. Forutsetter at `SamhandlingTilbyder` omformes slik at lokal variasjon har et sted å bo.

**3. Hvor fester `Vilkår` seg?** [PÅ AVKLARING]

§D.2. Billig å svare på, dyrt å utsette: det er prosjektets mest utviklede innhold og fasitens
leveranse. Bør besvares før migreringsplanen skrives, ikke under.

**4. `Registertype`/eksportslusen, og forholdet til `HendelseEntitet`.** [PÅ AVKLARING]

Notatets åpne spørsmål 1 (§E.1) pluss §D.4. Blir enklere når punkt 2 har avgjort hvilken tabell som
er eksportenheten. Min anbefaling er kjent: behold `Registertype` som sluse, adopter `utfort_av`,
fjern `Kontroll` som egen `Samhandlingstype`-verdi, og avklar `Samhandling` vs. `Hendelse`.

**5. Seed-listespesifikasjonen (`docs/17` §7) revideres.**

Endres kun hvis punkt 1 innfører `ForvaltningsomraadeEntitet` — da trengs geografiske områder som
egne rader, altså en kolonne mer eller en fil mer. Bør ikke sendes til Johann før punkt 1 er avgjort,
ellers ber vi om en liste vi må be om igjen.

**6. Migreringsplan for de 14 seedede radene, og for KI-pipelinen.**

§C.2, §C.3 og §D.5. Innholdsmigreringen er liten nok å gjøre manuelt; pipeline-omskrivingen er det
ikke. Bør ha en skrevet plan før første migrasjon kjøres, men trenger ikke egen avklaringsrunde.

**7. Kan vente — genuint additivt, ingen konflikt med noe låst:**

- §6 innhøstings-/kvalitetssikringslaget. Ingen kollisjon med noe eksisterende; men §D.1 må løses
  først hvis det skal forsone geografiske rader.
- §7 klassifikasjon/SKOS. Helt grønn mark (§D.6), men bør avklares mot `BegrepEntitet` og
  `KodelisteEntitet` så prosjektet ikke får to halve SKOS-løsninger.
- §4 `FeltLovreferanse`. Nytt kun i `feltnavn`-aksen (§B.5), og må begrunnes mot
  «alltid-NULL-kolonner»-presedensen i `Entiteter.cs:892-905`.
- §5 `Versjonsmetadata`. Stort sett allerede oppfylt (§B.4). Det ekte hullet er
  `DatasettVerdiEntitet`s manglende gyldighetsperiode (§B.6) — en liten, avgrenset oppgave som kan
  gjøres uavhengig av hele denne runden.

**8. SIST: kartlegging av `RettighetForutsetning`-kandidater.**

§E.4. Manuelt juristarbeid, forutsetter at radene finnes. Skal ikke blokkere noe over.

---

## G. Kildegrunnlag

**Kode, verifisert mot `34f4d85` (2026-08-14):** `src/RegelIde.Data/Entiteter.cs` (`Virksomhet` 9-28,
`BegrepEntitet` 484-508, `KodelisteEntitet` 516-541, `TjenesteEntitet` 346-382,
`TjenesteRegelverksreferanseEntitet` 385-391, `HendelseEntitet` 395-416, `TjenesteHendelseEntitet`
419-430, `TjenesteavhengighetEntitet` 432-459, `VilkarEntitet.TjenesteId` 621,
`NettsideLenkeEntitet`-begrunnelsen 892-905, `ErstatterId` på 80/369/501/532/657/701/749),
`src/RegelIde.Data/RegelIdeDbContext.cs` (100-101), `src/RegelIde.Data/TjenesteregisterTjeneste.cs`
(240 linjer), `src/RegelIde.Data/TjenesteforslagTjeneste.cs` (310),
`src/RegelIde.Data/VilkarregisterTjeneste.cs` (196), `src/RegelIde.Data/RegelnoderegisterTjeneste.cs`
(190), `src/RegelIde.Data/TjenesteavhengighetregisterTjeneste.cs` (159),
`src/RegelIde.Data/Byggesteg2InnholdSeed.cs` (112-135), `src/RegelIde.Data/FasitRunde4Seed.cs`
(184-210), `src/RegelIde.Data/Byggesteg4VilkarstreSeed.cs` (33-112),
`src/RegelIde.Data/KommunaleParametreSeed.cs`, `src/RegelIde.Data/BergenKorpusSeed.cs` (77-83),
`src/RegelIde.Data/ProveniensHjelper.cs` (`NyForslagRad` 29-42),
`src/RegelIde.Kildekonvertering/AknXmlSkriver.cs` (164-178, den forkastede
`temporalGroup`-mekanismen), `src/RegelIde.Api/Program.cs` (`/api/tjenester`-gruppen 896-1082,
1857-1994),
`src/RegelIde.Web/src/pages/` (`TjenesteDetalj.tsx` 635 linjer, `TjenesteforslagKo.tsx` 272,
`TjenesteVeiledning.tsx` 168, `TjenesterListe.tsx` 162).

**Negative søk (bekrefter fravær):** `Registertype`/`registertype`/`forvaltningsoppgave`,
`Organisasjonstype`, `Tjenestenivaa`, `MasterTjenesteId`, `GjelderOrganisasjonstype`,
`Organiseringstype`, `Forvaltningsomraade`, `Tema`/`Klassifis`/`Sektor` — alle null treff i `src/`.

**Dokumentasjon:** `docs/17-forvaltningsstruktur-master-tjeneste.md` i sin helhet (særlig §2.1-2.3,
§3, §4, §5.1-5.6, §7, §8, §9). `docs/15-handbok-dokumentgraf-notat.md` §3.3 (244-315, den låste
`Organisasjonsnummer`-beslutningen), §10.1-10.2 (811-900, `Registertype` og eksportslusen), §11.
`docs/08-byggesteg1-teknisk-design.md` §1.1-1.2.1 (11-98, FRBR/eId/`canonical_id`), §2.1 (275-287,
versjonering på dokumentnivå), Vedlegg A.9 (409). `docs/13-backlog.md` §1 (byggesteg-status), §2.7
(478-500, supersedt av `docs/15` §10.2), §4, §6. `docs/16-vurdering-rettskilde-til-tjenestebeskrivelse.md`
§0 (markørformen denne vurderingen låner), §1 («ingen `Registertype`»). `docs/06-veikart.md`.

**Uverifiserte antakelser, for ordens skyld:**

- **Notatets egne kildetall** (1485 Altinn-tjenester, 903 skjema, 288 Statsforvalter-tjenester, 792
  fylkeskommunale skjema, 131/288-funnet). Ikke etterprøvbart herfra — ingen av disse datasettene
  finnes i repoet. Regnestykkene *innad* i §2-tabellen er derimot kontrollert og stemmer (§B.9).
- **Kardinaliteten statsforvalterembete ↔ fylke** (§E.3). Bevisst ikke tallfestet; flagget som noe
  som må sjekkes, ikke antas.
- **At CPSV-AP-NO ikke har et etablert begrep for «rettighet» atskilt fra «tjeneste»** som burde
  brukes fremfor `RettighetEntitet`. Ikke sjekket mot spesifikasjonen. Samme forbehold `docs/17` §10
  tok om et standardisert master-/malbegrep, og verdt å sjekke før navnet låses.
- **Kostnadsvurderingen av å omskrive KI-pipelinen** (§D.5). Basert på lesing av kallkjedene og
  linjetall, ikke på et forsøk.
- **At de syv navngitte branchene som tilhører en annen utvikler** ikke endrer feltene denne
  vurderingen omtaler. Sjekket på `master` (`34f4d85`), ikke i deres arbeidskopier.
