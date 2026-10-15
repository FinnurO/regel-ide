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
   et regelverk, er det ikke P.
3. **Legger teksten et arbeidsfelt eller et ansvar til aktøren?** Da er det F.

Svaret kan være mer enn én rad. «Departementet skal samordne arbeidet og kan gi pålegg om …» er F samordning **og** K pålegg.
Hver rad har sin egen hjemmel i teksten.

### Regelen som binder: F gir aldri K eller P i seg selv

En oppgave gir ikke instruksjonsmyndighet, ikke vedtakskompetanse og ikke en kravsrett for noen. Systemet slutter aldri en
K- eller P-kant fra en F-kant, og ingen spørring om kompetanse eller plikt returnerer F. Sier teksten at oppgaven også gir
myndighet eller en plikt overfor noen, registreres det som egne rader med egen hjemmel.

Begrunnelse: grunnlagsdokumentet §7.8 («Myndigheten skal samordne beredskapsarbeidet» viser ikke uten videre at myndigheten har
instruksjonskompetanse overfor alle som omfattes av samordningen). Legalitetsprinsippet krever hjemmel for inngrep; en
oppgavebestemmelse er normalt ikke en slik hjemmel.

### Felt

| Felt | Innhold | Regel |
|---|---|---|
| `fra` | Aktøren oppgaven er lagt til: aktør, organ, organisatorisk enhet, rolle eller klasse | Påkrevd. En ordning har ingen oppgaver; den forvaltes (R `forvaltes_av`) |
| `objekt` | Oppgaven, med lovens ord så langt det går: «beredskapsarbeid», «momentan balanse i kraftsystemet», «nasjonale faglige retningslinjer» | **Påkrevd** (CHECK). Oppgaven er hele innholdet i en F-kant |
| `til` | Valgfri: aktøren, klassen eller området oppgaven gjelder | Bare når teksten sier det. «Sekretariat for forliksrådene» har `til`; «skal samordne beredskapsarbeid» har ikke |
| `modalitet` | `skal` / `kan` / `bor` | Som på P. «Skal» er plikten til å utføre oppgaven, uten motpart. Normativ presens («Fortegnelse … føres av kommunen») = `skal`. «Kan» = en oppgave aktøren har adgang til å påta seg |
| `polaritet` | `positiv` / `negativ` | «Skal ikke ha oppgaver som …» er negativ |
| hjemmel, avgrensning, kunnskapsstatus | Som alle kanter | Klassifisering til F er en tolkning: `fortolkning` med begrunnelse og tolkningsargumenter, eller `tekstfunn` når teksten selv bruker funksjonsordet («er sekretariat for», «forvaltningsansvaret for») |

### Typer

Et lite, lukket startsett. Nye typer er rader i typekonfigurasjonen, ikke kode, men skal begrunnes (grunnlagsdokumentet §4.7).

| Kode | Fasittype | Når | Ordlyd som typisk viser den |
|---|---|---|---|
| `forvaltning` | `forvaltningsoppgave` | Ansvaret for å forvalte et regelverk, en ordning, et register eller et fond | «skal forvalte», «forvaltningsansvaret for», «forvaltningen av … hører under» |
| `samordning` | `samordningsoppgave` | Å samordne eller koordinere andres arbeid, uten myndighet til å binde dem | «skal samordne», «sørge for samordning av», «koordinere» |
| `sorge_for` | `sorge_for_ansvar` | Et resultatansvar: aktøren skal sørge for at noe skjer eller finnes, for et område eller en befolkning | «skal sørge for at», «har det overordnede ansvar for at», «skal ha ansvaret for» |
| `oversikt` | `oversiktsoppgave` | Å føre oversikt, register, fortegnelse eller følge med på en tilstand | «skal føre oversikt over», «registeret føres av», «skal følge med på» |
| `bistand` | `bistandsoppgave` | En generell oppgave å bidra til andres arbeid, uten en bestemt motpart som kan kreve det | «skal bidra i», «medvirke til» (uten motpart) |
| `sekretariat` | `sekretariatsfunksjon` | Å være sekretariat for et organ eller utføre sekretariatsoppgaver | «er sekretariat for», «sekretariatet skal utføre de oppgavene som» |
| `radgivning` | `radgivningsoppgave` | Å være rådgiver eller gi råd som fast funksjon | «skal være medisinskfaglig rådgiver for», «skal gi råd» |
| `veiledning` | `veiledningsoppgave` | Å veilede publikum, tjenesteytere eller andre organer om regelverk eller fag | «skal veilede om», «skal gi veiledning», «opplæring og veiledning av» |

`veiledning` står ikke i docs/34 §6.1-forslaget. Den er lagt til i #368 fordi barnehageloven (piloten, #375) har «veilede og
påse» i nesten hver kapittel, og veiledning er et eget forvaltningsrettslig begrep (fvl. § 11). Den er ikke det samme som
rådgivning: rådgivning går til den som skal avgjøre noe, veiledning til den som skal etterleve eller bruke regelverket.
Avvik fra docs/34; juristrunden i #368 vurderte det.

### Positive eksempler (fra fasiten)

| Sitat | Kilde | F-rad |
|---|---|---|
| «Beredskapsmyndigheten skal samordne beredskapsarbeid» | energiloven u160 | `samordning`, fra = Beredskapsmyndigheten, objekt = «beredskapsarbeid», skal |
| «Den systemansvarlige skal sørge for at det til enhver tid er momentan balanse» | energiloven u131 | `sorge_for`, objekt = «momentan balanse mellom produksjon og forbruk», skal |
| «Kommunen skal sørge for at personer som oppholder seg i kommunen, tilbys nødvendige helse- og omsorgstjenester» | hotl. u9 | `sorge_for`, til = kommunen (området), skal. A `har_ansvarsomrade` står i tillegg |
| «Fortegnelse over de valgte meddommere føres av kommunen» | domstolloven u171 | `oversikt`, objekt = «fortegnelse over valgte meddommere», skal (normativ presens) |
| «Namsfogden er sekretariat for forliksrådene i sitt distrikt» | domstolloven u70 | `sekretariat`, til = forliksrådene, avgrensning «i sitt distrikt» |
| «Kommunelegen skal være medisinskfaglig rådgiver for kommunen» | hotl. u52 | `radgivning`, til = kommunen, skal |
| «Helsedirektoratet skal utvikle, formidle og vedlikeholde nasjonale faglige retningslinjer og veiledere» | hotl. u180 | `forvaltning` eller `veiledning` (juristrunden avgjør) |

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

### Grenser mot naboene

**F samordning mot K samordning.** K `samordning` (familien styring) er *myndighet* til å samordne andre, slik at de er bundet
av samordningen (for eksempel en hjemmel til å fastsette felles planer eller treffe bindende samordningsvedtak). «Skal samordne»
alene er en oppgave, ikke myndighet (grunnlagsdokumentet §7.8). Uten tekst som gir bindende virkning er det F. Er begge deler
der, er det to rader.

**F bistand mot P bistand.** P `bistand` er en plikt overfor en motpart som kan kreve ytelsen («skal yte bistand til andre
kommuner ved ulykker», «etter anmodning fra en domstol skal politiet …»). F `bistand` er et arbeidsfelt uten en slik motpart
(«skal bidra i kommunens folkehelsearbeid»). Prøven er om noen kan si «dette skylder du meg».

**F sekretariat mot R `sekretariat_for`.** R `sekretariat_for` sier hvem som er sekretariatet for hvem: en organisatorisk
relasjon mellom to aktører (docs/33 §4.3, #330). F `sekretariat` sier hva sekretariatet skal gjøre. Et sitat som bare
sier «A er sekretariat for B», kan være én av delene eller begge; juristrunden i #368 avgjør regelen (se utfallet i docs/33 §4.7).

**F radgivning mot R `radgir` og P konsultasjon.** R `radgir` er avgjort som struktur i #352 (en fast rådgiverrelasjon). P
`konsultasjon` er plikten til å *innhente* råd eller uttalelse og ligger hos den som spør (#355). F `radgivning` er oppgaven å gi
råd. «Skal gis anledning til å uttale seg» er derfor P konsultasjon hos den som skal spørre, ikke F hos den som svarer.

**F forvaltning mot R `forvaltes_av`.** `forvaltes_av` går fra en ordning (folketrygden, et fond) til organet, fordi ordningen
ikke er en aktør (#353). F `forvaltning` går fra aktøren og har regelverket eller registeret som objekt
(«Forvaltningsansvaret for energiloven § 4-4»).

**F sørge for mot A ansvarsområde.** A `har_ansvarsomrade` sier *hvor* aktøren virker. F `sorge_for` sier *hva* den har
resultatansvar for. «Kommunen skal sørge for at personer som oppholder seg i kommunen, tilbys …» gir begge.

**F mot regellaget.** Vilkår, frister og kvalitetskrav til oppgaven («forsvarlige», «innen rimelig tid») hører til regellaget
(rettsfølge, L16). F-kanten er den aktørnære projeksjonen: hvem som har oppgaven, hva den er, og hvor det står.

### Hvordan F tegnes i modellen

- Databasen: kategori `F` i `strukturkanter`, typekodene over i `relasjonstype_konfigurasjon` med visningsmaler
  («har samordningsoppgaven {0}»). CHECK-ene: `objekt` påkrevd, `modalitet` lov på P og F, `til` valgfri.
- Fasiten: `kategori: "oppgave"`, `type` = fasittypen i tabellen over, `objekt` påkrevd, `modalitet`.
- Appen: egen seksjon «Oppgaver» på aktørsiden og rettskildesiden, ved siden av kompetanse og plikt, aldri inni dem.
- Standardene: LKIF `Function` (role.owl) er nært beslektet (docs/profil/04). UFO-L og LegalRuleML har ingen motpart.
