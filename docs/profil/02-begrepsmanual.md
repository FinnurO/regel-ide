# Begrepsmanual: definisjon, eksempel, moteksempel og grense per begrep

*Arbeidsutkast, påbegynt 2026-10-15 (issue #368). Normativt sammen med profilspesifikasjonen (01) når den publiseres
(docs/profil/README.md). Inntil da er dette det bindende grunnlaget for fasit, konvertering og juristrunder for de begrepene
som står her.*

Manualen bygges opp ett begrep om gangen, fra fasitkontrollen og juristrundene. Hvert begrep har samme oppsett:
**definisjon, felt, typer, positive eksempler, moteksempler, grenser mot naboene, og hvordan det tegnes i modellen**.
Eksemplene er sitater fra fasiten (`data/fasit/strukturmodell/`), med lov og utsagns-id, så de kan etterprøves.

Begrepene som alt er i modellen (R, K, P, M, O, A, G, I, T) er beskrevet i docs/33 §4.3–§4.6 og FORMAT.md. De flyttes hit
etter hvert. Det første som står her, er det nye.

---

## F · Oppgave/funksjon

**Status:** besluttet som eget begrep av Johann 2026-10-15 (issue #368). Grunnlag: grunnlagsdokumentet §7.8, metodenotatet §6
(«Kompetanse, oppgave, plikt og kravsrett er fire ting»), docs/34 §6.1.

### Definisjon

En **oppgave/funksjon** er en virksomhet, et arbeidsfelt eller et ansvar som en rettskilde legger til en aktør, uten at utsagnet
i seg selv gir aktøren myndighet til å endre andres rettsstilling (kompetanse, K) eller en plikt overfor en bestemt motpart (P).

Testen er tre spørsmål, i denne rekkefølgen:

1. **Kan aktøren med dette utsagnet binde noen?** Gir teksten myndighet til å avgjøre, pålegge, instruere, godkjenne eller
   gi regler, er det K. Står det bare *hva aktøren skal arbeide med*, er det ikke K.
2. **Skylder aktøren en bestemt motpart en ytelse?** Kan en navngitt eller klassebestemt motpart si «du skal gjøre dette for
   meg» (samarbeide, betale, bistå, informere, konsultere), er det P. Er mottakeren et område, en befolkning, et arbeidsfelt eller
   et regelverk, er det ikke P. [Presisert i juristrunden, #368] Motparten må være en aktør i strukturlaget (et offentlig organ, en
   rolle eller en organisert representasjon), og ytelsen må skyldes i et konkret forhold («etter anmodning», «på forespørsel», en
   sak eller en avtale). Private begunstigede (pasienten, borgeren) hører til regellaget; F-raden er projeksjonen med `til` = null.
   En enhet, tjeneste eller rolle inne i samme rettssubjekt er ikke en motpart.
3. **Legger teksten et arbeidsfelt eller et ansvar til aktøren?** Da er det F.

Svaret kan være mer enn én rad. «Departementet skal samordne arbeidet og kan gi pålegg om …» er F samordning **og** K pålegg.
Hver rad har sin egen hjemmel i teksten.

### Regelen som binder: F gir aldri K eller P i seg selv

En oppgave gir ikke instruksjonsmyndighet, ikke vedtakskompetanse og ikke en kravsrett for noen. Systemet slutter aldri en
K- eller P-kant fra en F-kant, og ingen spørring om kompetanse eller plikt returnerer F. Sier teksten at oppgaven også gir
myndighet eller en plikt overfor noen, registreres det som egne rader med egen hjemmel.

**Også overfor tredjepart.** «A skal sørge for at B gjør X» gir F for A. Det gir ingen plikt for B, og ingen kompetanse for A
overfor B (juristrunden: «Det regionale helseforetaket skal sørge for at virksomheter … samarbeider» gir ikke P samarbeid for
virksomhetene).

Begrunnelse: grunnlagsdokumentet §7.8 («Myndigheten skal samordne beredskapsarbeidet» viser ikke uten videre at myndigheten har
instruksjonskompetanse overfor alle som omfattes av samordningen). Legalitetsprinsippet krever hjemmel for inngrep; en
oppgavebestemmelse er normalt ikke en slik hjemmel.

### Felt

| Felt | Innhold | Regel |
|---|---|---|
| `fra` | Aktøren oppgaven er lagt til: aktør, organ, organisatorisk enhet, rolle eller klasse | Påkrevd. En ordning har ingen oppgaver; den forvaltes (R `forvaltes_av`) |
| `objekt` | Oppgaven, med lovens ord så langt det går: «beredskapsarbeid», «momentan balanse i kraftsystemet», «nasjonale faglige retningslinjer» | **Påkrevd** (CHECK). Oppgaven er hele innholdet i en F-kant |
| `til` | Valgfri: aktøren, klassen eller området oppgaven gjelder | Bare når teksten sier det, og aldri samme node som `fra` (CHECK `ck_strukturkanter_ikke_selv`). Et område bare når det ikke alt står en A-rad for det. «skal samordne beredskapsarbeid» har ingen `til` |
| `modalitet` | `skal` / `kan` / `bor` | Som på P. «Skal» er plikten til å utføre oppgaven, uten motpart. Bare når teksten har et modalverb eller normativ presens av en handling («Fortegnelse … føres av kommunen», «Styret sørger for») = `skal`. [Juristrunden] Attributiv form uten modalverb («har ansvaret for», «Forvaltningsansvaret for», «hører under») gir `null` — å sette «skal» der er en slutning (jf. #355 pkt. 8) |
| `polaritet` | `positiv` / `negativ` | «Skal ikke ha oppgaver som …» er negativ |
| hjemmel, avgrensning, kunnskapsstatus | Som alle kanter | `fortolkning` (med begrunnelse og tolkningsargumenter) når teksten har et K- eller P-signal i samme ledd som måtte vurderes — slik var det for alle kandidatene i #368. `normalisering` når typen følger av ordformen i typetabellen og leddet ikke har noe K- eller P-signal (mønsterlaget setter den på egne forslag, som i #367). `tekstfunn` når teksten selv bruker funksjonsordet («Forvaltningsansvaret for …»). `avledet` når en del av utsagnet hviler på en annen kant (KNS som sekretariat, se under) |

### Typer

Et lite, lukket startsett. Nye typer er rader i typekonfigurasjonen, ikke kode, men skal begrunnes (grunnlagsdokumentet §4.7).

| Kode | Fasittype | Når | Ordlyd som typisk viser den |
|---|---|---|---|
| `forvaltning` | `forvaltningsoppgave` | Ansvaret for et regelverk, en ordning, et fond eller et forvaltningsapparat («den sentrale administrasjonen av domstolene») | «skal forvalte», «forvaltningsansvaret for». Ikke «forvaltningen av foretaket hører under styret»: det er selskapsrettslig kompetanse (K) |
| `samordning` | `samordningsoppgave` | Å samordne eller koordinere andres arbeid, uten myndighet til å binde dem | «skal samordne», «sørge for samordning av», «koordinere» |
| `sorge_for` | `sorge_for_ansvar` | Et resultatansvar: aktøren skal sørge for at noe skjer eller finnes, for et område eller en befolkning | «skal sørge for at», «har det overordnede ansvar for at», «skal ha ansvaret for» |
| `oversikt` | `oversiktsoppgave` | Å føre registeret, fortegnelsen eller oversikten, eller følge med på en tilstand (ansvaret for registeret som ordning er forvaltning) | «skal føre oversikt over», «registeret føres av», «skal følge med på» |
| `bistand` | `bistandsoppgave` | En generell oppgave å bidra til andres arbeid, uten en bestemt motpart som kan kreve det | «skal bidra i», «medvirke til» (uten motpart) |
| `sekretariatsfunksjon` | `sekretariatsfunksjon` | Å være sekretariat for et organ eller utføre sekretariatsoppgaver | «er sekretariat for», «sekretariatet skal utføre de oppgavene som» |
| `radgivning` | `radgivningsoppgave` | Å være rådgiver eller gi råd som fast funksjon | «skal være medisinskfaglig rådgiver for», «skal gi råd» |
| `veiledning` | `veiledningsoppgave` | En særskilt lovbestemt veiledningsfunksjon overfor dem som skal etterleve eller bruke regelverket eller faget | «skal veilede om», «skal gi veiledning», «utvikle … retningslinjer og veiledere», «opplæring og veiledning av» |
| `kontroll` | `kontrolloppgave` | Å påse at andre etterlever regelverket, uten egen hjemmel til å undersøke eller reagere | «skal påse at», «følge opp og kontrollere» |

[Ny, #395, juristrunden 2026-10-23] **Ressortfordeling** er F `forvaltning` med loven som objekt (og lovens ELI i `objekt_eli`):
«Oversikt over lover X skal ha forvaltningsansvaret for» og «Forvaltningsansvaret for lov … overføres fra A til B». `tekstfunn` når teksten
bruker «forvaltningsansvaret», ellers `normalisering` begrunnet med at objektet er et regelverk. Modalitet settes ikke: «skal ha» er
tildelingens form, ikke en plikt (unntak fra modalitetsregelen over).

`veiledning` og `kontroll` står ikke i docs/34 §6.1-forslaget; begge er lagt til i #368. Barnehageloven (piloten, #375) har «veilede
og påse» i nesten hver kapittel. Veiledning er ikke rådgivning: rådgivning går til den som skal avgjøre noe, veiledning til den som
skal etterleve eller bruke regelverket. [ENDRET, juristrunden] Den alminnelige veiledningsplikten i fvl. § 11 gjelder alle
forvaltningsorganer og hører til regellaget; den gir ingen F. Veiledning til en bestemt motpart som kan kreve den («skal gi
spesialisthelsetjenesten råd, veiledning og opplysninger … som er påkrevd for at [den] skal kunne løse sine oppgaver»), er P.

`kontroll` (juristrunden, prinsipp c): «skal påse at» er aldri K tilsyn i seg selv. Barnehageloven skiller: § 10 («påse at») er
oppgaven, § 53 («fører tilsyn med», pålegg om retting, stenging) er K tilsyn og K pålegg med egen hjemmel — to rader.

### Åpne spørsmål — avgjøres i barnehageloven-piloten (#375)

Johann besluttet på PR #381 (2026-10-16): **ingen ny type for noen av dem nå.** De står åpne med eksemplene, og avgjøres når
barnehageloven er modellert (#375), der begge formene er vanlige.

1. **Innsats eller arbeidsfelt** («arbeide for», «fremme», «legge til rette for»). Ingen av de ni typene passer: `sorge_for` er et
   resultatansvar («sørge for at noe skjer eller finnes»), og innsatsen er mindre enn det. Eksempler fra fasiten:
   - sameloven u224: «skal arbeide for vern og videre utvikling» (ført som `sorge_for` inntil videre, juristrunde 1 kort #81);
   - juristens kort #80 og #167 i runde 1 («fremme», «legge til rette for»).
   Inntil videre: `sorge_for` med objektet ordrett, slik at radene kan finnes og flyttes når typen er avgjort.
2. **En tjenestesektor som bærer av oppgaven.** Tekstens subjekt er ofte sektoren, ikke rettssubjektet som har ansvaret
   (grunnlagsdokumentet §4.5: tekstens subjekt er ikke nødvendigvis den juridiske ansvarsbæreren). Eksempler fra fasiten:
   - helse- og omsorgstjenesteloven u21 (aktør a20, `annet:tjenestesektor`): «Helse- og omsorgstjenestene skal bidra i kommunens
     folkehelsearbeid» — F bistand med sektoren som `fra`;
   - spesialisthelsetjenesteloven (aktør a47, «spesialisthelsetjenesten»): «Spesialisthelsetjenesten skal bistå ved utformingen av
     tiltakene» (hotl. u103/u109, P bistand overfor kommunen).
   Spørsmålet er om `fra` skal være sektoren (tekstformen) eller ansvarsbæreren (kommunen, RHF-et) løst opp. Inntil videre: tekstens
   subjekt, som fasiten gjør i dag.

### Positive eksempler (fra fasiten)

| Sitat | Kilde | F-rad |
|---|---|---|
| «Beredskapsmyndigheten skal samordne beredskapsarbeid» | energiloven u160 | `samordning`, fra = Beredskapsmyndigheten, objekt = «beredskapsarbeid», skal |
| «Den systemansvarlige skal sørge for at det til enhver tid er momentan balanse» | energiloven u131 | `sorge_for`, objekt = «momentan balanse mellom produksjon og forbruk», skal |
| «Kommunen skal sørge for at personer som oppholder seg i kommunen, tilbys nødvendige helse- og omsorgstjenester» | hotl. u9 | `sorge_for`, til = kommunen (området), skal. A `har_ansvarsomrade` står i tillegg |
| «Fortegnelse over de valgte meddommere føres av kommunen» | domstolloven u171 | `oversikt`, objekt = «fortegnelse over valgte meddommere», skal (normativ presens) |
| «Stiftelsesklagenemnda skal ha et sekretariat som foretar saksforberedelse» | stiftelsesloven § 7a (lokal base) | `sekretariatsfunksjon`, objekt «saksforberedelse» — teksten sier hva sekretariatet gjør. Ved siden av R `sekretariat_for` (begge); kunnskapsstatus `avledet` fordi at sekretariatet er KNS, kommer fra R-kanten |
| «Helsedirektoratet skal utvikle, formidle og vedlikeholde nasjonale faglige retningslinjer og veiledere» | hotl. u180 | `veiledning` (retningslinjene binder ikke) |
| «Fortegnelse … føres av kommunen» / «Registeret over sidegjøremål føres av domstoladministrasjonen» | domstolloven u171, u198 | `oversikt`, skal (normativ presens) |

### Moteksempler (ikke F)

| Sitat | Hvorfor ikke | Riktig |
|---|---|---|
| «Departementet kan gi forskrift om …» | Myndighet til å gi regler | K `normgivning` |
| «Departementet kan pålegge samarbeid mellom kommuner» | Myndighet til å binde kommunene | K `palegg` (og P samarbeid for kommunene) |
| «Kommunen skal inngå samarbeidsavtale med det regionale helseforetaket» | Plikt overfor en bestemt motpart | P `avtale` |
| «Spesialisthelsetjenesten skal bistå ved utformingen av tiltakene» (overfor kommunen) | Kommunen kan kreve bistanden | P `bistand` |
| «skal departementet innhente rådgivende uttalelser fra andre relevante departementer» | Plikten ligger hos den som spør | P `konsultasjon` fra departementet (#355) |
| «Folketrygden … forvaltes av Helsedirektoratet» | Struktur fra en ordning til organet | R `forvaltes_av` (#353); se grensen under |
| «Hver kommune skal ha en koordinerende enhet» | Krav om at et organ finnes | T `skal_ha` / G |
| «Namsfogden er sekretariat for forliksrådene i sitt distrikt» (domstolloven u70) | Teksten sier bare relasjonen | R `sekretariat_for` alene (juristrunden, prinsipp b) |
| «Kommunelegen skal være medisinskfaglig rådgiver for kommunen» (hotl. u52) | Bare relasjonen; «medisinskfaglig» er avgrensning | R `radgir` alene |
| «Energiklagenemndas sekretariat skal utføre de oppgavene som lederen pålegger» | En henvisning uten eget oppgaveinnhold; det leddet sier, er at lederen kan instruere sekretariatet | R `sekretariat_for` + K instruksjon, ingen F |
| «Kommunen skal sørge for at representanter for pasienter og brukere blir hørt ved utformingen …» (hotl. u34) | Plikt til å høre en klassebestemt motpart. «sørge for at» avgjør ikke typen alene | P konsultasjon |
| «skal sørge for at det inngås samarbeidsavtaler som nevnt i helse- og omsorgstjenesteloven § 6-1» | Kommunen er motpart og selv avtalepliktig | P avtale |
| «Klagesaken skal forberedes av statsforvalteren» | Et trinn i én sak, ikke en stående funksjon | Saksforberedelse, regellaget (#353) |

### Grenser mot naboene

**F samordning mot K samordning.** K `samordning` (familien styring) er *myndighet* til å samordne andre, slik at de er bundet
av samordningen (for eksempel en hjemmel til å fastsette felles planer eller treffe bindende samordningsvedtak). «Skal samordne»
alene er en oppgave, ikke myndighet (grunnlagsdokumentet §7.8). Uten tekst som gir bindende virkning er det F. Er begge deler
der, er det to rader.

**F bistand mot P bistand.** P `bistand` er en plikt overfor en motpart som kan kreve ytelsen («skal yte bistand til andre
kommuner ved ulykker», «etter anmodning fra en domstol skal politiet …»). F `bistand` er et arbeidsfelt uten en slik motpart
(«skal bidra i kommunens folkehelsearbeid»). Prøven er om noen kan si «dette skylder du meg».

**F sekretariat mot R `sekretariat_for` (og F rådgivning mot R `radgir`).** [Avgjort i juristrunden, #368] R alene når teksten
bare sier relasjonen («A er sekretariat for B», «A skal være rådgiver for B»). F i tillegg bare når teksten sier hva funksjonen går
ut på («sekretariat som foretar saksforberedelse»). F aldri i stedet for R: R bærer svaret på «hvem er sekretariat for hvem»
(#330, #352), og en F som bare gjentar relasjonen, er samme faktum lagret to ganger. En ren henvisning («de oppgavene som lederen
pålegger») er ikke innhold. R `radgir` er for faste rådgiverrelasjoner; uttalelser i én sak som den som avgjør innhenter, er P
konsultasjon (#355).

**F mot saksforberedelse.** En stående funksjon for et organ («sekretariat som foretar saksforberedelse») er F. Et trinn i én sak
(«klagesaken skal forberedes av», «avgir forslag eller innstilling») hører til regellaget (#353).

**F radgivning mot R `radgir` og P konsultasjon.** R `radgir` er avgjort som struktur i #352 (en fast rådgiverrelasjon). P
`konsultasjon` er plikten til å *innhente* råd eller uttalelse og ligger hos den som spør (#355). F `radgivning` er oppgaven å gi
råd. «Skal gis anledning til å uttale seg» er derfor P konsultasjon hos den som skal spørre, ikke F hos den som svarer.

**F forvaltning mot R `forvaltes_av`.** `forvaltes_av` går fra en ordning (folketrygden, et fond) til organet, fordi ordningen
ikke er en aktør (#353). Når det som forvaltes, er en ordning med egen node, står R `forvaltes_av` alene (juristrunden: «Helsedirektoratet
skal forvalte kapittel 5»). F `forvaltning` går fra aktøren og har regelverket eller apparatet som objekt («Forvaltningsansvaret for
energiloven § 4-4»). En identifiserende beskrivelse («det departementet som har ansvaret for loven») tildeler ingenting og gir ingen F.

**F sørge for mot A ansvarsområde.** A `har_ansvarsomrade` sier *hvor* aktøren virker. F `sorge_for` sier *hva* den har
resultatansvar for. «Kommunen skal sørge for at personer som oppholder seg i kommunen, tilbys …» gir begge.

**F mot regellaget.** Vilkår, frister og kvalitetskrav til oppgaven («forsvarlige», «innen rimelig tid») hører til regellaget
(rettsfølge, L16). F-kanten er den aktørnære projeksjonen: hvem som har oppgaven, hva den er, og hvor det står.

### Juristrunden i #368

Kandidatene (K samordning, R sekretariat_for/radgir, P bistand og `annet:*` med oppgaveordlyd i fasit, KI-utdata og lokal base)
ble vurdert i to runder (CLAUDE.md §23). Utfallet per rad står i `data/fasit/strukturmodell/juristrunde-368-oppgave.json`, tallene
og målingen i docs/33 §4.7. Ingen reell uenighet gikk til Johann; de åpne spørsmålene over er typevekst, ikke uenighet.

### Hvordan F tegnes i modellen

- Databasen: kategori `F` i `strukturkanter`, typekodene over i `relasjonstype_konfigurasjon` med visningsmaler
  («har samordningsoppgaven {0}»). CHECK-ene: `objekt` påkrevd, `modalitet` lov på P og F, `til` valgfri.
- Fasiten: `kategori: "oppgave"`, `type` = fasittypen i tabellen over, `objekt` påkrevd, `modalitet`.
- Appen: egen seksjon «Oppgaver» på aktørsiden og rettskildesiden, ved siden av kompetanse og plikt, aldri inni dem.
- Standardene: LKIF `Function` (role.owl) er nært beslektet (docs/profil/04). UFO-L og LegalRuleML har ingen motpart.

---

## Klasse med utledet medlemskap: «kommune» (issue #384)

**Status:** besluttet av Johann 2026-10-16 (issue #384). Bygget i `KlasseKommuneOgUtledetMedlemskap` og `KlassemedlemskapTjeneste`.

### Definisjon

En klasse er **ekstensjonal** når medlemmene er listet med hjemmel, som M-kanter (docs/33 §4.2). Den er **utledet** når en kilde utenfor
modellen alt sier hvem medlemmene er. «kommune» er den første utledede klassen. Kommunene i Norge er hver et eget rettssubjekt etter
kommuneloven § 2-1 annet ledd. Landet er inndelt i kommuner etter første ledd, og det er hjemmelen for klassen. Hvilke kommuner som finnes, står i områderegisteret (Kartverkets inndeling, #312). Medlemskapet lagres derfor ikke en
gang til. Det regnes ut, slik den avledede domstolkjeden i #345 gjør.

### Felt

| Felt | Verdi | Merknad |
|---|---|---|
| `begrepskategori` | `klasse` | Fast, nasjonal (ingen lovkilde, #298) |
| `lovreferanse_eid` | kommuneloven § 2-1 første ledd | Hjemmelen for klassen: «Norge er inndelt i kommuner og fylkeskommuner …» (Johann, PR #388) |
| `kunnskapsstatus` | `tekstfunn` | Gjelder klassen selv. NULL på alle andre begreper (ikke vurdert) |
| `medlemskapsregel` | `omraderegister_kommune` | Lukket vokabular. Én gjeldende klasse per regel |
| medlemskapet | beregnet | Kunnskapsstatus `avledet`. Premisset er territoriekanten (A `har_ansvarsomrade` fra kommunen til eget område) |

### Eksempler og grenser

- **Medlem:** Alta kommune (5601). Den har ansvarsområde til området Alta og samme kommunenummer.
- **Ikke medlem:** en tingrett. Den har ansvarsområde til kommunen, men et annet kommunenummer.
- **Hull, ikke medlem:** et kommuneområde uten aktør med samme kommunenummer, eller med flere. Det listes, og ingen velges (CLAUDE.md §8).
- **Lovspesifikk klasse:** «kommunene» i reindriftsloven § 2 annet ledd er ikke den felles klassen. Den gjelder sju navngitte kommuner.
  Den beholdes, og henviser til den felles klassen med `M medlem_av` «kommune»: hvert medlem er en kommune.
- **Ikke gjettet:** en lovspesifikk «kommunene» kan være alle kommuner eller et utvalg. Det avgjøres for hver lov.

### Hvordan det tegnes i modellen

- Den generiske navneformen «kommunen» bindes til klassen for hver rettskilde (docs/33 §5.7).
- Hvilken kommune omtalen gjelder i den enkelte saken, avgjøres ikke av klassen. Det er #314.
- **Appen:** begrepssiden viser seksjonen «Medlemmer (utledet av områderegisteret)». Den har antallet, kunnskapsstatus for klassen og for
  medlemskapet, og hullene.

## Binding av en generisk navneform følger verket (issue #384)

En binding sier hva en generisk omtale betyr i en **rettskilde som verk**: en lov eller forskrift, identifisert ved ELI. Den gjelder ikke
bare én konsolidert versjon. `rettskilde_id` er versjonen bindingen ble registrert og vurdert mot.

- **Følger verket:** en binding for hele rettskilden. En binding med paragrafspenn følger når spennet dekker de samme eId-ene i den nye
  versjonen.
- **Følger ikke, og listes:** et paragrafspenn som er borte, eller som dekker andre noder. Bindingen brukes ikke i den nye versjonen, og et
  menneske registrerer den på nytt.
- **Blokkerer bare seg selv** (Johann, PR #388): de andre gyldige bindingene for navneformen brukes som før. Oppslaget gir `spenn_endret`,
  uten referent, bare for en node ingen av dem dekker.
- **Grense mot hjemmelen:** hjemmelen peker fortsatt på versjonen bindingen ble vurdert mot. Strukturkantene flagges med «hjemmel endret»
  (#367), men bindingene gjør ikke det ennå. Det hører ikke til #384.
