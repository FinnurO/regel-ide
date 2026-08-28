# 19. Notat: Virksomhetsbegrep og kobling til rettskilder

*Status: avklaringsrunde. Mottatt som oppdrag fra Johann 2026-08-20, sammen med oppdraget om å
importere statlige virksomheter fra Enhetsregisteret (§A i oppdraget, bygget parallelt — se
`BrregVirksomhetImportTjeneste`). Oppdraget ble gitt som en bakgrunnsjobb med eksplisitt instruks om
at jeg IKKE skal stoppe og spørre, men gjøre begrunnede, dokumenterte valg selv. Alt som er et ekte
valg jeg har tatt på Johanns vegne er derfor merket **[ANTATT]** og samlet i §7 — det er den listen
som skal leses først av et menneske.*

Verifisert mot `virksomhet-import` @ `f0293e6` (2026-08-20), som er siste mergede commit på `master`.
Alle linjenumre er fra det commit-et. En annen branch har kodeendringer under arbeid samtidig
(Lovdata-importstatus-UI); ingen av dem berører feltene dette notatet diskuterer.

---

## 0. Markører

| Markør | Betyr |
|---|---|
| **[LÅST]** | Besluttet i en tidligere runde, gjengitt her fordi det binder dette designet. Reåpnes ikke her |
| **[VALGT]** | Besluttet i denne runden, med begrunnelse. Kodet |
| **[ANTATT]** | Et ekte valg tatt på Johanns vegne fordi runden var en bakgrunnsjobb. Bør bekreftes. Samlet i §7 |
| **[UTENFOR SCOPE]** | Bevisst utsatt til en senere runde. Samlet i §8 |
| **[VERIFISERT]** | Påstand om dagens kode, sjekket mot fil og linje |

### 0.1 Bærende prinsipper, arvet

Tre prinsipper fra tidligere runder styrer hver konklusjon under:

- **Høst struktur, ikke generer den** (`docs/15` §3.4, `docs/17` §0.1). Der strukturen finnes i
  virkeligheten — Enhetsregisteret, loven — skal den leses inn, ikke modelleres på nytt. Dette er
  hovedargumentet i §3 under, og det er grunnen til at «kommune» *ikke* blir 357 koblingsrader.
- **Ingen gjettet fallback** (`docs/17` §0.1). Manglende data gir avvisning eller `NULL`, aldri en
  plausibel utfylling.
- **Ikke bland to ortogonale akser i ett felt** (`docs/17` §3, og §5.3s tre-akse-tabell for
  `TjenesteEntitet`). Dette avgjør §2 under.

---

## 1. Behovet, i Johanns egne ord

> «jeg vil knytte til et begrep, altså et navn, som benyttes i rettskildene for å si hvilke roller en
> virksomhet har. Jeg vil altså knytte en virksomhet til de lovene de er nevnt i (på rettskilde
> metadata, f.eks relevant for) samt alle steder i selve rettskilden. så på Mattilsynet skal begrepet
> være 'Mattilsynet'. da skal man f.eks tagge og linke denne loven:
> https://lovdata.no/dokument/NL/lov/2003-12-19-124. [...] på org.nummer for statsforvaltere så brukes
> både begrepene 'Fylkesmann' og 'Statsforvalter'. For kommuner så benyttes 'kommune'.»

Fire krav, som må skilles fordi de har svært ulik kostnad:

1. Et **begrep** kan betegne en virksomhets *rolle* i rettskildene («Mattilsynet», «kommune»,
   «Statsforvalter»).
2. Begrepet må kunne knyttes til **én** virksomhet (Mattilsynet) *eller* til **mange** (alle
   kommuner, alle statsforvaltere).
3. Ett og samme begrep kan ha **flere termer** («Fylkesmann» og «Statsforvalter» om samme organ).
4. Koblingen til rettskilden trengs på **to nivåer**: som metadata på dokumentet («matloven er
   relevant for Mattilsynet») *og* som posisjonert forekomst inne i teksten («her, i §23, står ordet
   Mattilsynet»).

---

## 2. Spørsmål 1 — skal et virksomhetsbegrep være virksomhetseid eller delt?

**[VALGT] Delt/nasjonalt. `BegrepEntitet.VirksomhetId` gjøres nullbar, og `NULL` betyr «delt/nasjonalt
begrep» — presis samme semantikk feltet allerede har fire andre steder i kodebasen.**

Dette er Johanns egen anbefaling i oppdraget, og den er riktig. Begrunnelsen, med belegg:

- **[VERIFISERT]** I dag er feltet påkrevd: `Entiteter.cs:645-646`, kommentert «Påkrevd (§0.1) — et
  begrep er alltid virksomhetens eget arbeidsprodukt.» Det er den låste regelen som må mykes opp, og
  det er en bevisst endring, ikke en omgåelse.
- **[VERIFISERT]** `NULL = delt/nasjonal` er allerede den etablerte konvensjonen i denne kodebasen,
  ikke en ny en: `RettskildeEntitet.VirksomhetId` (`Entiteter.cs:69-77`, «NULL = delt/nasjonal
  rettskilde … aldri duplisert per virksomhet»), `KodelisteEntitet.VirksomhetId`
  (`Entiteter.cs:677-678`), `HendelseEntitet` og `DatasettVerdiEntitet` (jf. `docs/17` §2.3s tabell,
  som teller fire slike steder).
- **Sakens kjerne:** «Mattilsynet» og «kommune» er ikke noen virksomhets private eiendom. De er
  fakta om forvaltningsstrukturen, brukt av alle. Å eie dem per virksomhet ville betydd at 357
  kommuner hver måtte opprette sin egen «kommune»-begrepsrad for å kunne tagge samme lovtekst — samme
  duplikasjonsargument `RettskildeEntitet`s kommentar (`Entiteter.cs:73-75`) alt bruker mot å
  duplisere nasjonale rettskilder per virksomhet: «duplisering … ville vært både kostbart og
  feilutsatt ved lovendringer, som da måtte vedlikeholdes N ganger i stedet for én».

**Hvordan holdes de to slagene begrep fra hverandre?** Ikke ved å utlede det fra
`VirksomhetId IS NULL`. Samme argument som `docs/17` §5.3 brukte for `Tjenestenivaa`: «en utledet
sannhet er en gjettet sannhet», og en fremtidig `NULL`-eier av andre grunner ville ellers stille gjort
en rad til et delt begrep. Derfor en eksplisitt diskriminator:

**[VALGT] Ny `Begrepskategori` på `BegrepEntitet`** — nullbar, `CHECK`-begrenset, med `'virksomhet'`
som eneste verdi i denne runden. `NULL` = et vanlig fagbegrep, som i dag.

**[VALGT] `Begrepskategori` blir et NYTT felt, ikke en ny verdi i `Begrepstype`.** Dette er §0.1s
tredje prinsipp anvendt direkte. **[VERIFISERT]** `Begrepstype` (`Entiteter.cs:654`) er
`'faktabegrep' | 'handlingsbegrep'` med eksplisitt kildehenvisning til Schartum 2025 7.3.3-7.3.4 —
det er en *erkjennelsesteoretisk* klassifikasjon av hva slags begrep dette er. «Mattilsynet» *er* et
faktabegrep i Schartums forstand, og skal fortsatt være det. Spørsmålet «hva betegner denne termen?»
er en annen akse, og å presse den inn i `Begrepstype` ville gjort de to verdiene ikke-uttømmende og
ødelagt en dokumentert, ekstern klassifikasjon.

| Felt | Svarer på | Status |
|---|---|---|
| `Begrepstype` | Hva slags begrep er dette? (fakta/handling — Schartum) | Finnes, `Entiteter.cs:654` |
| `Begrepskategori` | Hva betegner termen? (en virksomhet / et alminnelig fagbegrep) | Ny, dette notatet |

Konsekvens som må håndteres i samme runde: **[VERIFISERT]** dagens skrivevei antar en eier.
En `CHECK`-constraint må derfor binde de to feltene sammen, slik at ingen kan lage et eierløst
*fagbegrep* ved uhell:

```sql
ck_begreper_kategori
    begrepskategori IS NULL OR begrepskategori IN ('virksomhet')

ck_begreper_eierskap
    (begrepskategori = 'virksomhet')            -- delt: virksomhet_id KAN være NULL
 OR (begrepskategori IS NULL AND virksomhet_id IS NOT NULL)  -- fagbegrep: som før, alltid eid
```

Merk at et virksomhetsbegrep *får* ha en eier — en kommune skal kunne definere sitt eget lokale
virksomhetsbegrep uten at det blir nasjonalt. Det er bare de nasjonale som har `NULL`.

---

## 3. Spørsmål 2 — ett begrep, mange virksomheter

**[VALGT] Én koblingstabell, med et enten-eller: koblingen peker på ENTEN én konkret virksomhet,
ELLER på en hel organisasjonstype. Ikke 357 rader.**

Oppdraget skisserte to alternativer — en ren mange-til-mange `BegrepVirksomhet`, eller kobling til en
hel organisasjonstype — og ba om at begge vurderes. Konklusjonen er at **begge trengs**, fordi de sier
to forskjellige ting, men at de hører i **én** tabell.

### 3.1 Hvorfor ren mange-til-mange alene er feil

En ren `BegrepVirksomhet(BegrepId, VirksomhetId)` dekker Mattilsynet-tilfellet perfekt, og
kommune-tilfellet *tilsynelatende* også: 357 rader. Men den materialiserer noe som er avledbart, og
det har tre konkrete kostnader:

1. **Radene blir gale av seg selv.** Norge slår sammen og deler kommuner. Hver
   kommunesammenslåing gjør 357-radssettet stille feil, og ingenting i systemet oppdager det.
   `docs/17` §4 er eksplisitt om nettopp dette tallet: «tallet 357 skal komme fra seed-listen, ikke
   fra oss. Antall kommuner har endret seg flere ganger det siste tiåret.» En koblingstabell som
   hardkoder dagens 357 gjentar den feilen én etasje ned.
2. **Den svarer på feil spørsmål.** «Ordet *kommune* i matloven betegner alle kommuner» er en
   påstand om *typen*, ikke om 357 navngitte organ. Å skrive den ut som 357 rader mister
   informasjonen om at det var en typepåstand — og gjør det umulig å skille «alle kommuner» fra «disse
   357 kommunene, tilfeldigvis alle».
3. **Den bryter §0.1s første prinsipp.** Strukturen «hvilke virksomheter er kommuner» finnes
   allerede, i `Virksomhet`-tabellen, som FK til organisasjonstype-kodelisten (`docs/17` §11 [LÅST],
   bygget i §A av dette oppdraget). Å kopiere den inn i en koblingstabell er å generere struktur som
   allerede er høstet.

### 3.2 Hvorfor kobling til organisasjonstype alene også er feil

Den kan ikke uttrykke Mattilsynet. Det finnes ingen organisasjonstype med nøyaktig ett medlem, og å
lage en («type = mattilsynet») ville vært en oppdiktet type for å omgå en manglende kobling.

### 3.3 [VALGT] Løsningen — én tabell, XOR-constraint

```csharp
public sealed class BegrepVirksomhetEntitet
{
    public Guid Id { get; set; }
    public required Guid BegrepId { get; set; }

    /// Satt = begrepet betegner NØYAKTIG denne virksomheten ("Mattilsynet").
    public Guid? VirksomhetId { get; set; }

    /// Satt = begrepet betegner ENHVER virksomhet av denne typen ("kommune", "Statsforvalter").
    public Guid? OrganisasjonstypeId { get; set; }
}
```

```sql
-- Nøyaktig én av de to er satt. Samme form som docs/17 §5.4s ck_tjenester_nivaa_konsistens,
-- som er den etablerte presedensen for en flerfelts enten-eller-regel i dette skjemaet.
ck_begrep_virksomhet_maal
    (virksomhet_id IS NOT NULL AND organisasjonstype_id IS NULL)
 OR (virksomhet_id IS NULL AND organisasjonstype_id IS NOT NULL)
```

Én tabell fremfor to fordi de to radslagene er *samme relasjon* på to presisjonsnivåer — begge svarer
«hvilke virksomheter betegner dette begrepet?» — og fordi ethvert oppslag trenger begge samtidig.
To tabeller ville tvunget hver spørring til en union på kallstedet.

Oppslaget «hvilke virksomheter betegner begrep X» blir da: de eksplisitt koblede radene, unionert med
alle virksomheter hvis `OrganisasjonstypeId` er koblet. Det er **én** rad for «kommune» og forblir
korrekt den dagen kommune nummer 358 importeres. Det motsatte oppslaget («hvilke begrep gjelder for
virksomhet Y») er samme union lest andre vei, og det er *det* oppslaget som gjør at Bergen kommune
automatisk arver «kommune»-begrepet uten at noen har koblet Bergen til noe.

Merk hva denne tabellen bevisst **ikke** har: ingen `VirksomhetId` som *eier*-felt. Koblingen er et
faktum om forvaltningsstrukturen, ikke et arbeidsprodukt — samme resonnement som §2.

---

## 4. Spørsmål 3 — «Fylkesmann» og «Statsforvalter»

**[VALGT] Ett begrep, to termer. Nytt felt `AlternativeTermer` (skos:altLabel) på `BegrepEntitet`.**

**[VERIFISERT]** `BegrepEntitet` har ingen plass for synonymer i dag. `Term` er kommentert
`// skos:prefLabel` (`Entiteter.cs:648`) og er det eneste termfeltet; det finnes ingen `altLabel`,
og ingen egen synonymtabell.

At dette skal være **ett** begrep og ikke to følger av hva som faktisk skjedde: «Fylkesmannen» ble
omdøpt til «Statsforvalteren» i 2021. Det er samme organ, samme rolle, samme hjemler — ordet ble
byttet. To separate `Begrep`-rader ville påstått at det finnes to *begreper*, og ville tvunget hver
konsument (tagging, søk, oppslag «hvilke lover gjelder for statsforvalteren») til å huske å slå sammen
dem igjen. SKOS har `altLabel` for nøyaktig dette, og prosjektet har allerede valgt SKOS som
begrepsmodell (`docs/03` §1.3, gjengitt i `Entiteter.cs:637-638`).

Det er også det som gjør Johannns eksempel korrekt uten spesialtilfelle: **én** rad
`Term = "Statsforvalter"`, `AlternativeTermer = ["Fylkesmann"]`, koblet til organisasjonstypen
`statsforvalter` — altså **én** koblingsrad som dekker alle 10 statsforvaltere, og som treffer eldre
lovtekst der ordet «fylkesmannen» fortsatt står.

**Datatype:** `List<string>`, lagret som jsonb. Begrunnelse: **[VERIFISERT]** entiteten har
allerede nøyaktig dette mønsteret i `GjelderFor` (`Entiteter.cs:651`, `List<string> = []`), så det er
null nytt maskineri. En egen tabell er ikke berettiget — et synonym har ingen egne attributter, ingen
status og ingen livssyklus; det er en streng.

**[ANTATT]** At `prefLabel` skal være «Statsforvalter» og ikke «Fylkesmann». Gjeldende offisielle navn
er det åpenbare valget, men det betyr at et treff på historisk lovtekst vises under en term som ikke
står i teksten. Alternativet — å bygge gyldighetsperioder per term — er [UTENFOR SCOPE], se §8.

---

## 5. Spørsmål 4 — kobling på metadatanivå og i teksten

Johann ba om begge. De er to forskjellige mekanismer og bygges som to.

### 5.1 (a) Metadatanivå — er `GjelderFor` riktig felt?

**[VALGT] Nei. `GjelderFor` røres ikke. Ny tabell `RettskildeBegrepEntitet`.**

**[VERIFISERT]** `GjelderFor` er `List<string>` (`Entiteter.cs:651`), dokumentert som «Roller/tjenester»
i `docs/03-domenemodell.md`. Tre grunner til at den er feil bærer:

1. **Retningen er gal.** `GjelderFor` sitter på *begrepet* og peker (som fritekst) mot roller. Det
   Johann ber om er en kobling mellom en *rettskilde* og et begrep — «matloven er relevant for
   Mattilsynet». Å uttrykke det i `GjelderFor` ville betydd å skrive lovens navn inn i begrepets
   rollefelt, altså å snu relasjonen for å slippe å lage en tabell.
2. **Det er fritekst uten FK.** Ingen referanseintegritet, ingen mulighet for det omvendte oppslaget
   («hvilke rettskilder?») uten strengsammenligning.
3. **Kardinaliteten er feil.** Én lov nevner mange begrep, og ett begrep nevnes i mange lover. Det er
   en mange-til-mange-relasjon, og den hører i sin egen tabell.

```csharp
public sealed class RettskildeBegrepEntitet
{
    public Guid Id { get; set; }
    public required Guid RettskildeId { get; set; }
    public required Guid BegrepId { get; set; }

    /// 'relevant_for' i denne runden — Johanns eget ord. Egen kolonne, ikke antatt,
    /// fordi andre relasjonstyper ('hjemler', 'nevner') er nærliggende senere.
    public required string Relasjon { get; set; }

    public required string Opprinnelse { get; set; } // 'manuell' | 'import'
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}
```

**Ingen `VirksomhetId`.** Se §5.3 for hvorfor.

Merk at virksomheten *ikke* er med i tabellen. «Hvilke rettskilder er koblet til Mattilsynet»
besvares ved å gå via begrepet: rettskilde → begrep → (§3s koblingstabell) → virksomhet. Det er
bevisst, og det er Johanns egen modell — han ba om å «knytte til et begrep … for å si hvilke roller en
virksomhet har». Begrepet *er* leddet. En direkte `RettskildeVirksomhet`-tabell ved siden av ville
vært en andre, parallell sannhet om samme forhold, og de to ville kommet i utakt.

### 5.2 (b) Tekstnivå — kan `TekstTaggEntitet` brukes?

**[VALGT] Nei. Ny, ikke-virksomhet-scopet tabell `RettskildeBegrepForekomstEntitet`.**

Dette er notatets viktigste arkitektoniske valg, og oppdraget pekte riktig på spenningen.

**[VERIFISERT]** `TekstTaggEntitet.VirksomhetId` er `required` (`Entiteter.cs:323`), og
klassekommentaren over den (`:317-322`) er ikke en tilfeldighet men en uttalt beslutning:

> «Ikke nullable, i motsetning til RettskildeEntitet.VirksomhetId — en tagg er alltid en virksomhets
> eget arbeidsprodukt, selv når den peker på en delt/nasjonal rettskilde. To virksomheter kan tagge
> samme lovparagraf ulikt (forskjellige vilkår/begreper), så taggen arver ikke synlighet fra
> RettskildeId.»

Mekanismen er altså **bevisst subjektiv**. Men «ordet *Mattilsynet* står i matloven §23» er ikke en
tolkning — det er en observerbar egenskap ved teksten. Å tvinge den inn i `TekstTaggEntitet` gir tre
konkrete skader:

1. **Den må lyve om en eier.** Hvem eier taggen «Mattilsynet står her»? Ingen. Man måtte valgt en
   vilkårlig virksomhet, eller innført en syntetisk «nasjonal» virksomhetsrad — presis den løgnen i
   `virksomheter`-tabellen `docs/17` §5.2 avviste alternativ B for.
2. **Den ville krevd N duplikater.** Er taggen virksomhet-scopet, ser bare den ene virksomheten den.
   357 kommuner måtte hver tagge samme objektive forekomst i samme delte lovtekst — igjen
   duplikasjonsargumentet fra `Entiteter.cs:73-75`.
3. **Den ødelegger den eksisterende mekanismens mening.** Hvis noen tagger er objektive og andre
   subjektive, i samme tabell, uten diskriminator, kan ingen konsument lenger stole på at en tagg
   representerer eierens vurdering.

**Presedensen finnes allerede, og oppdraget pekte på den.** **[VERIFISERT]**
`RettskildeReferanseEntitet` (`Entiteter.cs:273-295`) kobler et *tekstspenn* til en annen rettskilde
**uten** noe `VirksomhetId` i det hele tatt — den har `FraNodeId` (`:276`), `TilRettskildeId` (`:277`),
og posisjonen som `TekstStart`/`TekstLengde` (`:293-294`), pluss `Opprinnelse` `'import' | 'manuell'`
(`:285`). Det er *nøyaktig* samme form som trengs her, én måltype unna: en objektiv, posisjonert
kobling fra et tekstspenn til noe annet. Den nye tabellen er derfor en søsken av den, ikke en ny idé:

```csharp
public sealed class RettskildeBegrepForekomstEntitet
{
    public Guid Id { get; set; }
    public required Guid RettskildeId { get; set; }
    public required string NodeEid { get; set; }
    public required Guid BegrepId { get; set; }

    /// Posisjon i nodens tekst — samme form som RettskildeReferanseEntitet.TekstStart/TekstLengde.
    public int? TekstStart { get; set; }
    public int? TekstLengde { get; set; }

    /// Sitatet, slik at forekomsten kan verifiseres og relokeres. Nullable av samme grunn
    /// som TekstStart: en forekomst kan registreres på node-nivå uten eksakt offset.
    public string? Sitat { get; set; }

    public required string Opprinnelse { get; set; } // 'manuell' | 'import'
    public required string OpprettetAv { get; set; }
    public DateTimeOffset OpprettetTidspunkt { get; set; }
}
```

### 5.3 Hva de to nye tabellene har til felles

Ingen av dem har `VirksomhetId`. Det er ikke en forglemmelse, det er poenget: begge registrerer
*objektive* forhold om en delt rettskilde, og begge er derfor lesbare for alle. `TekstTaggEntitet`
består uendret ved siden av, for det den faktisk er god til — en virksomhets egen, subjektive
kobling fra lovtekst til sine egne vilkår og fagbegreper.

**[ANTATT]** At skrivetilgang til disse to delte tabellene skal følge samme regel som delte
rettskilder (Jurist/Fagansvarlig, uansett virksomhet). Dette er den samme uavklarte RBAC-saken
`docs/17` §11 lot stå åpen for eierløse master-rader, og den bør avgjøres én gang for alle delte
tabeller, ikke per tabell. I denne runden er endepunktene bygget med samme rollekrav som resten av
skriveveiene, ikke strammere.

---

## 6. Hva som faktisk er bygget i denne runden

Skjemaet over, pluss end-til-ende-verifisering av Johanns eget eksempel:

- Begrepet «Mattilsynet» (`Begrepskategori = 'virksomhet'`, `VirksomhetId = NULL`), koblet via §3s
  tabell til den ekte `Virksomhet`-raden for Mattilsynet (orgnr `985399077`, importert fra
  Enhetsregisteret — den eneste «Stat»-virksomheten som importeres, se §7.1).
- Matloven (`LOV-2003-12-19-124`) importert via det eksisterende
  `POST /api/rettskilder/lovdata`-endepunktet, koblet til begrepet på metadatanivå (§5.1) og med
  registrerte forekomster i teksten (§5.2).
- Begrepene «kommune» og «Statsforvalter»/«Fylkesmann», koblet til organisasjonstype (§3.3) — altså
  én koblingsrad hver, ikke 357 og 10.
- Toveis oppslag via API: hvilke rettskilder gjelder for en virksomhet, og hvilke virksomheter
  gjelder for en rettskilde.

**Full bakoverfylling er IKKE gjort** — se §8.

---

## 7. [ANTATT] — valgene et menneske bør bekrefte

Samlet, fordi runden var en bakgrunnsjobb uten mulighet til å spørre. Ingen av dem blokkerer det som
er bygget; alle er billige å endre nå og dyrere senere.

1. **Feltnavnet `Begrepskategori` og verdien `'virksomhet'`** (§2). Alternativer:
   `Referansetype`, `Betegner`. Navnet blir stående i skjemaet.
2. **At `Begrepskategori` er et nytt felt og ikke en tredje `Begrepstype`-verdi** (§2). Jeg mener
   argumentet er sterkt (Schartum-aksen skal ikke forurenses), men det er et skjemavalg Johann eier.
3. **Én koblingstabell med XOR fremfor to tabeller** (§3.3). Funksjonelt likeverdige; dette er et
   smaksvalg om form.
4. **At «Statsforvalter» er prefLabel og «Fylkesmann» altLabel**, ikke omvendt, og at de ikke har
   gyldighetsperioder (§4).
5. **At `Relasjon` på `RettskildeBegrepEntitet` er en fri streng med `'relevant_for'` som eneste
   verdi nå**, i stedet for en kodeliste. Valgt fordi verdisettet ikke er kartlagt; en kodeliste
   uten kjente verdier er skjema for skjemaets skyld.
6. **At virksomhet↔rettskilde alltid går via et begrep** (§5.1), aldri direkte. Dette er den mest
   konsekvensrike antakelsen i notatet: hvis Johann vil kunne knytte en virksomhet til en lov *uten*
   at det finnes et begrep for den, må en direkte kobling til.
7. **RBAC for de to nye delte tabellene** (§5.3).
8. **At `stat`-kategorien fra oppdraget heter `statligforvaltningsorgan`** i
   organisasjonstype-kodelisten, ikke `stat`. Begrunnelse: `Forvaltningsniva` har allerede verdien
   `'stat'` på en annen akse, og `docs/17` §3 er eksplisitt om at statsforvalter, tingrett og
   lagmannsrett *også* er `forvaltningsniva = 'stat'`. To felt med samme verdi og ulik mening er
   nøyaktig den akseblandingen §0.1 forbyr.

---

## 7.1 Avgrensningen av «Stat» — korrigert underveis [LÅST i denne runden]

Verdt å skrive ned eksplisitt, fordi det endret koden midt i arbeidet.

Oppdraget ble opprinnelig formulert som «importer statlige forvaltningsorgan (departementer,
direktorater, tilsyn, ombud)», og en bred, sektorkodebasert harvest ble bygget for det: filteret
`institusjonellSektorkode = 6100` × `organisasjonsform ∈ {STAT, ORGL}` ga **320 enheter**, målt live.

Det var en **feiltolkning**, korrigert av brukeren før noe ble importert: han har konkret navngitt
**én** statlig virksomhet — Mattilsynet. De andre organene (Skatteetaten, Digdir,
Brønnøysundregistrene, Politidirektoratet) var *illustrasjoner* av hva uttrykket «statlig
forvaltningsorgan» betyr, ikke en importinstruks.

Den korrigerte modellen har derfor **to bevisst ulike mekanismer**, og forskjellen er ikke en
inkonsekvens:

| Kategori | Mekanisme | Begrunnelse |
|---|---|---|
| Kommune, fylkeskommune | Bred, automatisk (statisk fil, `OrganisasjonsregisterSeed`) | Etterspurt som hel kategori («For kommuner så benyttes 'kommune'») |
| Statsforvalter | Bred, automatisk harvest fra Enhetsregisteret | Etterspurt som hel kategori («på org.nummer for statsforvaltere …») |
| «Stat» | **Navngitt liste**, i dag kun Mattilsynet | Kun én virksomhet er faktisk navngitt |
| Tingrett, lagmannsrett, jordskifterett | **Ingen import** — kun koder i kodelisten | `docs/17` §11 navnga kodene, men ingen har bedt om domstolene som data |

Dette er samme linje `docs/17` §11 la for geografisk virkeområde: «ikke bygg en unntaksmekanisme før
et unntak faktisk må representeres». En bred harvest som trekker inn 320 organ ingen har spurt om er
data uten et navngitt bruksområde — og 320 sovende rader i virksomhetsvelgerne er en reell kostnad
(`docs/17` §4 punkt 3).

Kodelisten beholder likevel alle sju kodene, inkludert domstolene: en kode uten rader koster
ingenting, og `docs/17` §11s hele poeng med en redigerbar kodeliste var at nye typer ikke skal kreve
en migrasjon.

---

## 8. [UTENFOR SCOPE] — bevisst utsatt

- **Full bakoverfylling av forekomster** — å registrere alle virksomhetsbegrep i alle importerte
  rettskilder. Eksplisitt ikke forventet i denne runden. Skjemaet er bygget for at flere
  sammenhenger legges til case-by-case.
- **Automatisk gjenkjenning** av virksomhetsnavn i lovtekst (regel- eller KI-basert). Krever at
  §5.2s tabell finnes først, som den nå gjør.
- **Gyldighetsperioder per term** («Fylkesmann» til 2021, «Statsforvalter» etter). SKOS har ikke
  dette; det ville krevd en egen termtabell med datoer. Behovet er ikke demonstrert — begge termene
  skal treffe uansett periode, som er alt §4 trenger.
- **Relokering av forekomster ved reimport.** `TekstTaggEntitet` har et helt maskineri for dette
  (`RelokeringFeilet`, `NodeTekstHash`). Den nye forekomsttabellen har `Sitat` slik at samme
  mekanisme *kan* bygges, men den er ikke bygget.
- **Flere `Relasjon`-verdier** enn `'relevant_for'` (§7 punkt 5).
- **Master-tjeneste-delen av `docs/17`** (`Tjenestenivaa`, `MasterTjenesteId`,
  `GjelderOrganisasjonstype`). Eksplisitt utenfor dette oppdraget, fortsatt ubygget.
- **UI for å redigere virksomhetsbegrep og forekomster.** Skjema og API er bygget; en egen
  redigeringsflate er ikke.

---

## 9. Kildegrunnlag

**Kode, verifisert mot `virksomhet-import` @ `f0293e6`:** `src/RegelIde.Data/Entiteter.cs`
(`Virksomhet` 9-40, `RettskildeEntitet.VirksomhetId` 69-77, `RettskildeReferanseEntitet` 273-295,
`TaggKindKonfigurasjonEntitet` 297-311, `TekstTaggEntitet.VirksomhetId` 313-323, `BegrepEntitet`
637-665, `KodelisteEntitet.VirksomhetId` 673-678).

**Dokumentasjon:** `docs/17-forvaltningsstruktur-master-tjeneste.md` §0.1, §2.3 (delt/NULL-mønsteret),
§3 (to-akse-argumentet), §4 (357-tallet skal ikke hardkodes), §5.2 (avvisningen av en syntetisk
samlenivå-virksomhet), §5.4 (flerfelts `CHECK`-presedensen), §11 [LÅST]
(organisasjonstype-kodelisten). `docs/18-vurdering-rettighet-samhandling-modell.md` (samme «høst
struktur, ikke generer den»-prinsipp). `docs/03-domenemodell.md` §0.1, §1.3 (SKOS).

**Ekstern kilde, verifisert live 2026-08-20** mot `data.brreg.no/enhetsregisteret/api/enheter`: se
`BrregVirksomhetImportTjeneste`s klassekommentar for de faktiske kodene og de målte tallene.

**Uverifiserte antakelser, for ordens skyld:**

- **At SKOS `altLabel` er riktig konstruksjon for Fylkesmann/Statsforvalter.** Lest fra SKOS'
  alminnelige bruk, ikke mot en normativ SKOS-spesifikasjon eller Felles datakatalogs egen
  veiledning. Bør sjekkes mot data.norge.no før begrepene publiseres dit (`SkosUrl`,
  `Entiteter.cs:653`).
- **At omdøpingen Fylkesmann → Statsforvalter skjedde i 2021.** Alminnelig kjent, ikke sjekket mot
  primærkilde i denne runden.
- **At ingen konsument av `BegrepEntitet` bryter når `VirksomhetId` blir nullbar.** Sjekket ved
  kompilering og testkjøring, ikke ved uttømmende lesing av alle kallsteder.
