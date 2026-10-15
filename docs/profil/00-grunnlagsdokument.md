# Forslag til norsk profil for maskinlesbar rett – med Regel-IDE som utprøvingsarena

**Revidert arbeidsutkast · 9. oktober 2026**

**Status:** Arbeidsutkast. Dette er et forslag til norsk profil, ikke en vedtatt nasjonal standard. Regel-IDE er foreløpig en utprøvingsarena og kandidatreferanseimplementasjon; konform implementasjon må dokumenteres før den omtales som referanseimplementasjon. Mappingene til internasjonale modeller er ikke ferdig validert.

---

## 1. Sammendrag

Regel-IDE kan utvikles fra et system for strukturering og klassifisering av lovtekst til en **utprøvingsarena og senere mulig referanseimplementasjon for en åpen norsk profil for maskinlesbar rett**. Inntil en bestemt organisasjon og prosess har gitt profilen formell status, omtales den som «forslag til norsk profil». «Standard» brukes om målbildet eller standardiseringsarbeidet, ikke som om formell status allerede foreligger. Regel-IDE bør først kalles referanseimplementasjon når det er demonstrert at implementasjonen følger en publisert profil og består dens konformitetstester.

Regel-IDE bygger etter det opplyste allerede på ELI-identifikatorer fra Lovdata og Akoma Ntoso for dokumentstruktur. Dette er et viktig fundament. Norges ELI-implementasjon gir URI-er for konsolidert lovgivning og Lovtidend-versjoner, med strukturerte identifikatorer og metadata. Akoma Ntoso er en OASIS-standard for strukturering og utveksling av lovgivnings-, parlamentariske og rettslige dokumenter. Se [EUs ELI-register for Norge](https://eur-lex.europa.eu/eli-register/norway.html) og [OASIS Akoma Ntoso](https://www.oasis-open.org/standard/akn-v1-0/).

Disse standardene løser likevel ikke alene den juridiske modelleringen. Regel-IDE må fremdeles definere hvordan aktører, institusjoner, kompetanser, normative posisjoner, vilkår, rettsvirkninger, fortolkninger og bevisgrunnlag representeres.

### Anbefalt retning

1. **Bruk ELI og Akoma Ntoso for kilder og dokumentstruktur**, men verifiser særskilt identitet på bestemmelses- og tekstfragmentnivå, versjonskoblinger og gyldighetstid.
2. **Evaluer LKIF Core som referanse for generelle juridiske begreper**, ikke som en ferdig norsk modell.
3. **Evaluer UFO-L/Hohfeld for normative posisjoner**, samtidig som Hohfelds `power` holdes atskilt fra allmenn offentligrettslig kompetanse.
4. **Bruk LegalRuleML som mulig representasjons- og utvekslingsformat for formelle regler**, ikke som erstatning for en kjerneontologi.
5. **Vurder RDF/OWL for semantisk representasjon, SHACL for datavalidering og PROV-O for proveniens**. Disse tekniske byggesteinene er viktige, men de avgjør ikke den juridiske begrepslæren.
6. **Utvikle en norsk forvaltningsrettslig profil** med eksplisitte definisjoner, positive og negative eksempler, mappinger og valideringsregler.
7. **Skill mellom profil/spesifikasjon, implementasjon, korpus og testkorpus**. Et konkret modellutsagn er ikke automatisk en normativ regel i profilen.
8. **Prioriter en avgrenset release candidate** for dokumentkilder, aktører/institusjoner og kompetanse. Full regelstruktur og konkret etterlevelse bør utvikles gjennom avgrensede piloter.

Det mulige norske bidraget ligger ikke i å oppfinne maskinlesbar rett på nytt, men i å utvikle, teste og forvalte en gjenbrukbar norsk profil som bevarer viktige rettslige forskjeller og kan kobles til internasjonale modeller.

## 2. Endringer som bør gjøres i den opprinnelige planen

Følgende justeringer er viktige før dokumentet brukes som grunnlag for en offentlig release candidate av den foreslåtte profilen.

### 2.1 Ikke presenter en foreslått arkitektur som allerede interoperabel

ELI, Akoma Ntoso, LKIF Core, UFO-L og LegalRuleML er ikke fem ferdig sammenkoblede lag med automatisk semantisk kompatibilitet. De har ulike formål, representasjonsformer og overlappende begreper. Mappinger må spesifiseres, testes og versjoneres. LegalRuleML sier dessuten uttrykkelig at spesifikasjon av kjerne- og domenespesifikke juridiske ontologier ligger utenfor standardens hovedomfang.

### 2.2 Skill offentligrettslig kompetanse fra Hohfelds `power`

Hohfelds `power` er evnen til å endre rettslige posisjoner. Offentligrettslig kompetanse kan også omfatte myndighet til å utføre en oppgave, treffe en beslutning eller utøve offentlig funksjon uten at hver utøvelse uten videre beskrives som en Hohfeldiansk endring av rettsstillinger. Begrepene kan kobles, men må ikke behandles som synonymer.

### 2.3 Skill kompetanse fra handling og resultat

«Kompetanse til å oppnevne», «oppnevningen» og «personen som er oppnevnt» er tre forskjellige ting. Tilsvarende er adgang til å delegere, en gjennomført delegering og kompetansen hos mottakeren forskjellige utsagn. Datamodellen må holde disse fra hverandre.

### 2.4 Ikke gjør hvert verb til en egen kompetansetype

Verb som «velge», «oppnevne», «utpeke», «utnevne», «ansette», «avsette» og «avskjedige» er relevante, men de ligger ikke nødvendigvis på samme abstraksjonsnivå. Noen beskriver handlinger, andre kompetanser, og andre igjen rettsvirkninger. Begrepshierarkiet må være basert på juridisk funksjon, ikke bare ordlyd.

### 2.5 Gjør proveniens, tid og tolkningsstatus gjennomgående

Kilde, tidsdimensjon, jurisdiksjon, tolkningsstatus og versjonering kan ikke være et sent tillegg. De må kunne knyttes til alle sentrale modellutsagn og regelobjekter. Gyldigheten til en rettskilde må også skilles fra perioden hvor en bestemt analyse eller klassifikasjon anses gyldig.

### 2.6 Ikke likestill en offentlig plikt med en individuell kravsrett

En bestemmelse kan pålegge et organ en plikt uten at en bestemt privatperson eller et annet organ automatisk får en individuell, håndhevbar kravsrett. Hohfelds korrelativitet er nyttig når det er etablert at relasjonen er en kravsrett/plikt-relasjon; den må ikke brukes til å utlede kravsrett fra enhver normativ plikt.

### 2.7 Bruk etablerte tekniske standarder for kvalitetssikring

I tillegg til juridiske kjerneontologier bør prosjektet eksplisitt vurdere:

- [RDF og OWL 2](https://www.w3.org/TR/owl2-overview/) for semantisk representasjon og formelle begrepsforhold.
- [SHACL](https://www.w3.org/TR/shacl/) for validering av datagrafer mot uttrykkelige krav.
- [PROV-O](https://www.w3.org/TR/prov-o/) for å representere hvem eller hva som skapte, endret, avledet eller godkjente et modellutsagn.
- [SKOS](https://www.w3.org/TR/skos-reference/) for kontrollerte vokabularer, etiketter og begrepsmappinger der full ontologisk semantikk ikke er nødvendig.

OWL-semantikk og validering er ikke det samme. SHACL bør brukes til praktiske konformitetskrav, obligatoriske felt og datakvalitetskontroll; OWL bør ikke alene brukes som en generell skjema-validator.

### 2.8 Kall første versjon en profil eller spesifikasjon, ikke en vedtatt nasjonal profil

Inntil det foreligger en transparent styringsmodell, åpen konsultasjon, spesifikasjon, testpakke og dokumentert implementasjonsuavhengighet, er «forslag til norsk profil» eller «kandidat til nasjonal profil» mer presist enn «norsk standard».

### 2.9 Fastsett hva som er normativt

Før en release candidate publiseres, skal dokumentpakken angi hvilke artefakter som er normative og hvilke som er informative. Anbefalt prioritering er:

1. Den teknologinøytrale spesifikasjonen, inkludert definisjoner og uttrykkelige krav.
2. Maskinlesbare ontologier/skjemaer og SHACL-regler, i den utstrekning spesifikasjonen uttrykkelig gjør dem normative.
3. Eksempler og testtilfeller som uttrykkelig er merket normative for en bestemt profil.
4. Mappinger til eksterne modeller, som i første versjon bør være informative.
5. Regel-IDE som utprøvingsarena og ikke-normativ implementasjon.

Hvis normativ tekst, maskinlesbare artefakter og valideringsregler motsier hverandre, skal dette behandles som en feil i dokumentpakken. Det skal ikke overlates til hver implementasjon å velge hvilken versjon den foretrekker. Spesifikasjonen er den primære kilden til den tilsiktede semantikken; maskinlesbare artefakter og valideringsregler skal korrigeres eller spesifikasjonen endres gjennom en versjonert endringsprosess.

## 3. Formål og avgrensning

### 3.1 Formål

Formålet er å utvikle et felles, åpent og teknologinøytralt begrepsapparat for å representere rettskilder, norske institusjoner, rettslige kompetanser, normative relasjoner og regelstruktur på en sporbar og maskinlesbar måte.

Profilen skal støtte strukturert analyse og spørsmål som:

- Hvilke rettssubjekter og offentlige organer omtales eller etableres i rettskildene?
- Hvilke organer inngår i hvilke rettssubjekter eller organisatoriske strukturer?
- Hvem har hvilken kompetanse, over hvilket tema, i hvilket område og på hvilke vilkår?
- Hvem har en plikt overfor hvem, og hvem er eventuelt begunstiget eller håndhevingsberettiget?
- Hvem kan instruere, delegere, oppnevne, føre tilsyn med, behandle klage på eller overprøve hvem?
- Hvilke vilkår, unntak, frister og rettsvirkninger er uttrykt i en regel?
- Hvilken lovversjon og hvilket konkret tekstfragment støtter et modellutsagn?
- Er utsagnet tekstfunn, normalisering, juridisk fortolkning eller en slutning basert på flere kilder?
- Hva er uavklart eller omstridt?

### 3.2 Avgrensning

Første versjon skal først og fremst standardisere representasjonen av en juridisk analyse. Den skal ikke uten videre hevde å fastsette det materielt riktige svaret i enhver konkret sak eller automatisere full rettsanvendelse.

Følgende skilles uttrykkelig:

1. **Kildestrukturering:** hva dokumentet sier, hvor det står og hvilken versjon det tilhører.
2. **Semantisk normalisering:** hvordan uttrykk fra teksten knyttes til kontrollerte begreper.
3. **Juridisk analyse:** hvilken rettslig betydning som begrunnes ut fra relevante kilder.
4. **Avledet slutning:** en konklusjon som følger av flere modellutsagn eller regler.
5. **Konkret rettsanvendelse:** anvendelse på faktiske forhold, bevis og saksspesifikke omstendigheter.

Systemet skal kunne vise forskjellen mellom disse nivåene, og kunne la spørsmål stå åpne.

## 4. Grunnprinsipper

### 4.1 Kilden først

Alle materielle juridiske påstander skal ha referanse til én eller flere kilder. Henvisningen bør være så presis som kildesystemet tillater: rettskilde, versjon, bestemmelse, ledd, punktum, bokstav eller tekstfragment.

### 4.2 Ingen skjult slutning

Normaliseringer og juridiske slutninger skal ikke presenteres som om de står ordrett i lovteksten. Avledede relasjoner må kunne spores tilbake til premissene som støtter dem.

### 4.3 Ikke tvangsklassifiser

Modellen skal kunne uttrykke «uavklart», «ukjent», «flere mulige tolkninger» og «utenfor profilens omfang». Manglende data er ikke det samme som at noe ikke finnes eller ikke er tillatt.

### 4.4 Skille mellom aktør, rolle, stilling og handling

En person eller organisasjon er ikke det samme som rollen den innehar, stillingen den fyller, kompetansen den har eller handlingen den utfører.

### 4.5 Skille mellom tekstlig subjekt og juridisk ansvarsbærer

Det grammatiske subjektet i lovteksten er ikke nødvendigvis et rettssubjekt eller den endelige bæreren av den rettslige plikten. Dette må avgjøres fra kilder og juridisk analyse, ikke grammatikk alene.

### 4.6 Tids- og jurisdiksjonsbevissthet

Rettens gyldighet, aktørens eksistens, kompetanseforhold og modellanalysens gyldighet kan ha ulike tidsperioder. Disse må representeres særskilt.

### 4.7 Utvidbarhet uten vilkårlig typevekst

Ny type opprettes bare etter en dokumentert vurdering av juridisk betydning, gjenbruk, skille fra eksisterende typer og konsekvenser for søk og inferens.

### 4.8 Teknologinøytral standard

Standarden skal definere semantikk, identifikatorer, profiler og konformitetskrav. Den skal ikke kreve at alle implementasjoner bruker Regel-IDE eller én bestemt database eller brukerflate.

## 5. Eksterne standarder og ontologier

### 5.1 ELI – identifikasjon og metadata om rettskilder

ELI skal brukes der det finnes egnede ELI-identifikatorer og metadata. Norges ELI-implementasjon beskriver URI-mønstre for norsk lovgivning, inkludert nivåer, versjon, tidspunkt, språk og format. Det må likevel dokumenteres nøyaktig hvilke URI-er Regel-IDE bruker for dokument, bestemmelse og tekstfragment, og hvilke egenskaper som følger av ELI i seg selv kontra Lovdatas konkrete implementasjon.

### 5.2 Akoma Ntoso – dokumentstruktur

Akoma Ntoso brukes for strukturerte juridiske dokumenter og deres deler, metadata og henvisninger. Det er et dokumentformat og en standardisert dokumentmodell, ikke en komplett ontologi for rettslig kompetanse eller normative posisjoner. Regel-IDE må ha en eksplisitt kobling fra hvert modellutsagn til de relevante AKN-elementene eller tekstfragmentene.

### 5.3 LKIF Core – referanse for generelle juridiske begreper

LKIF Core er en modulær samling ontologier med begreper fra juridisk og allmenn kunnskapsrepresentasjon. Den bør vurderes som referanse for generelle begreper som aktør, organisasjon, rolle, handling, prosess, tid, sted og rettslig kilde.

Den bør ikke ukritisk importeres som den norske brukerrettede klassifikasjonen. For hvert relevant begrep skal profilen angi om det gjenbrukes, spesialiseres, mappes løst eller ikke brukes.

### 5.4 UFO-L og Hohfeld – referanse for normative posisjoner

UFO-L kan vurderes for deontiske og rettslige posisjoner, med særlig nytte for skillet mellom kravsrett/plikt, frihet/ingen-kravsrett, kompetanse/utsatthet og immunitet/manglende kompetanse.

Det må skilles mellom Hohfelds tekniske `power` og bredere offentligrettslig kompetanse. En offentlig myndighet som utfører en lovpålagt oppgave utøver ikke nødvendigvis en Hohfeldiansk `power` i alle henseender. Mappingen må begrunnes per relasjonstype.

### 5.5 LegalRuleML – representasjon av regler

LegalRuleML er en OASIS-standard for strukturert representasjon av juridiske normer og regler, herunder vilkår, deontiske modaliteter, tid, jurisdiksjon, negasjon og regelkonflikter. Den er særlig relevant for en senere regelprofil.

LegalRuleML skal ikke behandles som en erstatning for kjerneontologien. Første versjon kan definere en intern regelstruktur og deretter dokumentere hvilke deler som kan serialiseres til LegalRuleML.

### 5.6 RDF, OWL, SHACL, PROV-O og SKOS

Disse fyller andre roller enn de juridiske modellene:

- **RDF:** grafbasert datarepresentasjon.
- **OWL 2:** formell semantikk for klasser, egenskaper og relasjoner.
- **SHACL:** kontroll av data mot eksplisitte krav og former.
- **PROV-O:** proveniens for datakilder, aktiviteter, agenter, generering og avledning.
- **SKOS:** begrepsordbøker, etiketter, hierarkier og mappings der en full logisk ontologi ikke trengs.

Det må vedtas en teknisk arkitektur som sier hvilke av disse som er normativt krav, anbefalt representasjon eller valgfri eksportform.

### 5.7 Mappingprinsipper

Mappinger skal ikke automatisk bruke `owl:equivalentClass` eller tilsvarende sterke ekvivalenspåstander. Bruk en kontrollert mappingtype:

- ekvivalent, med dokumentert semantisk begrunnelse
- norsk begrep er snevrere enn internasjonalt begrep
- norsk begrep er bredere enn internasjonalt begrep
- nært beslektet, men ikke ekvivalent
- ingen egnet ekvivalent funnet
- mapping ikke vurdert

Hver mapping skal ha begrunnelse, ansvarlig versjon, status og eksempler.

## 6. Arkitektur: moduler, ikke en stiv trapp

De opprinnelige nivåene er nyttige som arbeidsplan, men de bør ikke fremstilles som en strikt semantisk kjede. Tid, kilde, proveniens, jurisdiksjon og tolkningsstatus går på tvers av alle moduler. En plikt kan for eksempel modelleres samtidig med aktørene den gjelder for og med vilkår fra en regel, uten at disse må fullføres i separate tidsfaser.

### Modul A – Kilder og dokumentuttrykk

- rettskildeidentitet
- kilde- og dokumenttype
- dokumentstruktur
- bestemmelse og tekstfragment
- versjon og språk
- endringshistorikk
- henvisninger
- publisering og kunngjøring
- tidsmessig gyldighet, når den er dokumentert

### Modul B – Entiteter og institusjoner

- personer og organisasjoner
- rettssubjekter
- offentlige organer
- organisatoriske enheter
- roller og stillinger
- kollegiale organer
- ordninger og abstrakte administrative innretninger
- geografiske og jurisdiksjonelle områder
- organisatoriske og geografiske relasjoner

### Modul C – Kompetanse og institusjonelle relasjoner

- kompetansehaver
- kompetansefamilie og handlingstype
- objekt eller sakstema
- adressat eller motpart, der relevant
- rettsvirkning
- vilkår og begrensninger
- delegering og annen kompetanseoverføring
- oppgaver og funksjoner som må skilles fra kompetanse
- instruksjon, samordning, tilsyn og overprøving

### Modul D – Normative posisjoner

- plikt
- kravsrett
- frihet/tillatelse
- forbud
- ingen-kravsrett
- kompetanse i Hohfelds tekniske forstand
- utsatthet for kompetanseutøvelse
- immunitet
- manglende kompetanse
- prosessuell adgang og relaterte posisjoner

### Modul E – Regelstruktur

- regelobjekt
- vilkår
- kumulative og alternative vilkår
- unntak
- negasjon
- frister og terskler
- rettsvirkning
- prioritet og konflikt
- opphør, brudd, reaksjon og reparasjon, der relevant

### Tverrgående dimensjoner

Alle moduler kan trenge:

- kilde og tekstfragment
- gjeldende rettskildeversjon
- tidsperiode for påstanden
- jurisdiksjon og saklig virkeområde
- proveniens
- tolkningsstatus
- sikkerhet/uenighet
- opphavsperson eller system
- standard- og datakorpusversjon

### Modul F – Bruk, spørring og etterlevelse

Denne modulen er brukstilfellebasert og omfatter spørringer, visualisering, dokumentasjon på oppfyllelse, avvik og beslutningsstøtte. Den er ikke et krav om at hele standarden skal modellere faktiske saker eller automatisere etterlevelse.

## 7. Entiteter og forvaltningsstruktur

Begrepene nedenfor er kandidater som må få presise definisjoner og avgrensninger. Listen skal ikke forstås som at alle nødvendigvis må være søskenklasser på samme ontologiske nivå.

### 7.1 Rettssubjekt

En enhet som etter relevant rett kan være selvstendig bærer av rettigheter og plikter.

Mulige eksempler: staten, en kommune, et fylke/fylkeskommune, et helseforetak, et aksjeselskap, en stiftelse eller en fysisk person. Hvert eksempel må vurderes etter den rettslige konteksten; betegnelsen alene avgjør ikke alltid status.

### 7.2 Offentlig organ

En institusjon eller enhet tillagt offentlig funksjon eller myndighet. Organstatus må skilles fra rettssubjektivitet: et organ er ikke nødvendigvis et selvstendig rettssubjekt.

Mulige eksempler: departement, direktorat, kommunestyre, nemnd og domstol. Profilen må beskrive hvordan organbegrepet avgrenses etter relevant norsk rett.

### 7.3 Organisatorisk enhet

En administrativ enhet som inngår i en organisasjon, men som ikke nødvendigvis er et selvstendig organ eller rettssubjekt. Mulige eksempler er avdeling, seksjon, klinikk, tjenesteområde og kontor.

### 7.4 Rolle

En funksjon eller posisjon som kan innehas av en person eller organisasjon i en viss kontekst. Eksempler kan være arbeidsgiver, behandlingsansvarlig, tilsynsmyndighet eller klageinstans. Rolle bør ikke forveksles med den faktiske aktøren eller stillingen.

### 7.5 Stilling eller embete

En institusjonalisert posisjon som kan innehas av en person. Det må skilles mellom stillingen, rettslig status som embete, rolleinnehaveren og handlinger vedkommende foretar.

### 7.6 Ordning

Begrepet «ordning» er potensielt svært bredt. Det bør bare brukes som en egen type når det gis en definisjon som skiller en rettslig eller administrativ innretning fra organisasjon, rettssubjekt, norm og finansieringsmekanisme. Det kan bli nødvendig å dele kategorien i underbegreper som ytelsesordning, finansieringsordning og administrativ ordning.

### 7.7 Område

Geografisk område, administrativt område, virkeområde og jurisdiksjon bør ikke slås sammen til én betydning. Profilen skal skille mellom faktisk geografi, administrativ inndeling, lovbestemt geografisk virkeområde og den jurisdiksjonen en myndighet har.

### 7.8 Oppgave eller funksjon

Profilen bør ha et eget begrep for en **oppgave eller funksjon** som er tillagt en aktør, et organ eller en organisatorisk enhet. Dette er nødvendig fordi lovtekst ofte sier at et organ skal samordne, føre oversikt, bistå, forvalte, sørge for noe eller utføre sekretariatsfunksjoner, uten at utsagnet uten videre uttrykker kompetanse til å endre rettsstillinger eller en plikt overfor en bestemt motpart.

Oppgave/funksjon skal ikke være en restkategori som skjuler juridiske forskjeller. Når relevant, må modellen skille mellom:

- oppgaven eller funksjonen som er tillagt aktøren
- en plikt til å utføre oppgaven
- kompetanse til å treffe beslutninger eller foreta handlinger med rettslig virkning
- en konkret handling som faktisk utføres
- en eventuell rettslig relasjon til en motpart eller begunstiget

Eksempel: «Myndigheten skal samordne beredskapsarbeidet» kan gi grunnlag for å registrere en oppgave/funksjon og en plikt til å utføre den. Det viser ikke uten videre at myndigheten har instruksjonskompetanse overfor alle som omfattes av samordningen. Om oppgaven også innebærer særskilt kompetanse, må vurderes ut fra hjemmel og sammenheng.

### 7.9 Person, klasse og instans

Standarden må eksplisitt skille mellom en klasse (for eksempel alle kommuner), en konkret instans (en bestemt kommune), en person, en rolle og en bestemt forekomst av en rolle. Regler for en klasse skal ikke uten videre kopieres til alle konkrete instanser uten en eksplisitt, kontrollert slutningsregel.

## 8. Organisatoriske og geografiske relasjoner

Relasjoner skal bare defineres når de har forskjellig juridisk eller praktisk betydning. Kandidater inkluderer:

### Organisasjon

- `del_av`
- `organ_for`
- `har_organ`
- `stilling_i`
- `rolle_i`
- `innehar_stilling`
- `innehar_rolle`
- `har_fast_medlem`
- `ledes_av`
- `har_sekretariat`
- `sekretariat_for`
- `rapporterer_til`
- `administrativt_underlagt`
- `faglig_underlagt`
- `instruksjonslinje_til`, dersom det rettslige grunnlaget er etablert

Disse relasjonene er ikke synonymer. Organisatorisk underordning skal ikke automatisk innebære instruksjonskompetanse; en rapporteringslinje skal ikke automatisk bety kontroll- eller klagekompetanse.

### Område

- `ligger_i`
- `består_av_område`
- `dekker_område`
- `har_sete_i`
- `har_virkeområde_i`
- `har_jurisdiksjon_i`
- `er_rettskrets_for`
- `er_valgkrets_for`

Det må skilles mellom en del av et område, et område som geografisk ligger i et annet, og et område en myndighet har kompetanse i. Setningen «valgkretsen ligger i fylket» beviser ikke at hele fylket inngår i valgkretsen.

### Fast og saksavhengig sammensetning

Følgende må modelleres forskjellig:

- fast medlemskap
- organisatorisk tilhørighet
- oppnevning til et verv
- konkret deltakelse i én sak
- saksavhengig sammensetning av et kollegialt organ

At Høyesterett settes med et bestemt antall dommere i en sak, er ikke det samme som at disse dommerne er faste medlemmer av en egen organisatorisk enhet.

## 9. Kompetansemodellen

### 9.1 Arbeidsdefinisjon av rettslig kompetanse

Som foreløpig arbeidsdefinisjon foreslås:

> **Rettslig kompetanse er en rettslig anerkjent myndighet eller rettslig evne for en aktør til å utføre en handling som rettsordenen knytter en bestemt rettslig eller institusjonell virkning til, herunder å etablere, endre, oppheve eller fastsette en rettslig eller institusjonell posisjon, ordning eller beslutning, eller å utøve offentlig myndighet.**

Definisjonen er et arbeidsforslag som må prøves mot norsk offentligrettslig teori og et representativt testkorpus før den blir normativ. Den er ikke synonym med Hohfelds tekniske `power`. Den omfatter heller ikke automatisk enhver oppgave, funksjon, faktisk handling, informasjonsinnhenting eller rådgivende aktivitet. Slike fenomener kan være oppgaver eller funksjoner uten at det er etablert en selvstendig kompetanse i denne betydningen.

For grenseområder som samordning, tilsyn, informasjonsinnhenting og rådgivning må modellen først registrere tekstgrunnlaget og funksjonen, og deretter vurdere særskilt om rettskildene også etablerer kompetanse, plikt eller en normativ posisjon.

### 9.2 Kompetanse som reifisert objekt

Kompetanse bør ikke begrenses til en enkel grafkant. Et kompetanseobjekt bør minst kunne uttrykke:

- kompetanse-ID
- kompetansehaver
- kompetansefamilie
- lovlig handlingstype
- objekt eller tema
- eventuell adressat/motpart
- område og jurisdiksjon
- vilkår og unntak
- rettsvirkning eller kompetanseformål
- hjemmel og tekstfragment
- gyldighetsperiode
- delegerings- eller overføringsstatus
- proveniens og tolkningsstatus

### 9.3 Skill tre ting

For eksempel «A kan delegere kompetanse til B»:

1. **Kompetanseregel:** A har adgang/kompetanse til å delegere innenfor angitte grenser.
2. **Disposisjon:** A har faktisk delegert en bestemt kompetanse til B ved et identifiserbart vedtak eller instrument.
3. **Resultat:** B har kompetansen i en definert periode, med det omfang og de vilkår som følger av delegeringen.

Tilbakekall er en egen handling med egen hjemmel og rettsvirkning. Samme skille bør brukes ved oppnevning, ansettelse, forskriftsfastsetting, godkjenning, samtykke, klage og anke.

### 9.4 Kompetansefamilier – foreløpig taksonomi

Familiene nedenfor er et arbeidsforslag. Før de blir normative i en standard, må hver familie få en definisjon, positive eksempler, moteksempler, grense mot nabofamilier og testspørringer.

**A. Organisasjonskompetanse**

- opprette
- organisere
- omorganisere
- slå sammen
- dele
- avvikle

Dette skal skilles fra å velge eller oppnevne medlemmer, fastsette en arbeidsordning, samordne virksomhet eller instruere en eksisterende enhet.

**B. Sammensetnings- og personalkompetanse**

Handlingstyper kan omfatte valg, oppnevning, utpeking, utnevning, ansettelse, konstituering, avsetting, oppsigelse og avskjed. Disse skal ikke automatisk defineres som sidestilte kompetansetyper. Profilen skal skille mellom handlingen, kompetansen til å utføre handlingen og den rettslige virkningen.

Eksempel: «Kommunestyret velger forliksrådet» uttrykker kompetanse til å velge. «Styremedlemmet er avsatt» beskriver resultatet av en handling. Avsetting fra et verv må skilles fra oppsigelse av et arbeidsforhold og avskjed fra stilling eller embete.

**C. Styringskompetanse**

- instruksjon
- samordning
- delegering
- godkjenning
- samtykke
- pålegg

At ett organ samordner et område, viser ikke uten videre at det kan instruere alle aktører på området. Kompetanseform og rekkevidde må dokumenteres hver for seg.

**D. Normgivningskompetanse**

Normgivning skal knyttes til normform, eksempelvis lov, forskrift, lokal forskrift, reglement, arbeidsordning, vedtekter, instruks eller forretningsorden. «Forskriftskompetanse» kan i mange tilfeller representeres som normgivningskompetanse med normform `forskrift`, fremfor som en separat hovedfamilie.

Eksempel: «Kongen kan gi forskrift om X» modelleres med kompetansehaver, normgivningshandling, normform, tema, modalitet, vilkår og kilde. At kompetansehaver er angitt som «Kongen» innebærer ikke i seg selv at bestemmelsen uttrykkelig avklarer delegerbarhet.

**E. Kontroll- og oppfølgingskompetanse**

Kandidater: tilsyn, revisjon, lovlighetskontroll, informasjonsinnhenting, undersøkelse, kritikk, krav om retting og reaksjon. Disse bør deles etter rettslig funksjon fremfor å samles under en bred kategori «kontroll».

**F. Klage og overprøving**

Kandidater: klagebehandling, anke, omgjøring, stadfesting, opphevelse og annen overprøving. Det må skilles mellom:

- instansrelasjon (hvem er klage- eller ankeinstans)
- kompetanse til å behandle/avgjøre
- parts eller aktørs adgang til å initiere prosessen
- den konkrete prosesshandlingen
- avgjørelsen som følger

**G. Enkeltvedtakskompetanse**

Denne kategorien skal brukes bare når kompetansen faktisk gjelder enkeltvedtak etter det relevante lovbegrepet. Ordene «vedtar», «avgjør» eller «beslutter» er ikke alene nok til å klassifisere avgjørelsen som enkeltvedtak. Klassifikasjonen må være knyttet til relevant lovversjon og rettslig definisjon.

### 9.5 Delegeringsstatus

Modellen bør skille mellom:

- uttrykkelig adgang til delegering
- uttrykkelig forbud mot delegering
- delegering ikke regulert i den aktuelle bestemmelsen
- adgang som eventuelt følger av andre rettskilder eller alminnelige regler
- konkret, dokumentert delegering
- konkret tilbakekall eller endring

«Ikke regulert her» er ikke det samme som «ikke delegerbar». En juridisk slutning om delegerbarhet må merkes som slutning, med egne kilder og begrunnelse. Faktisk delegering krever dokumentasjon fra et relevant vedtak, en instruks, forskrift, resolusjon eller annen kilde.

## 10. Normative posisjoner og rettsforhold

### 10.1 Hohfeld som analyseverktøy

Hohfelds åtte posisjoner organiseres vanligvis som fire korrelatpar:

| Posisjon hos A | Korrelativ posisjon hos B |
|---|---|
| Kravsrett (`claim-right`) | Plikt (`duty`) |
| Frihet (`liberty/privilege`) | Ingen-kravsrett (`no-right`) |
| Kompetanse i Hohfelds forstand (`power`) | Utsatthet for kompetanseutøvelse (`liability`) |
| Immunitet (`immunity`) | Manglende kompetanse (`disability/no-power`) |

Terminologien på norsk må fastsettes i en begrepsmanual, og de engelske originaltermene bør bevares som etiketter for mapping. `Liability` bør ikke oversettes mekanisk til «ansvar», fordi norsk rett bruker ansvar om flere forskjellige fenomener.

### 10.2 Ikke utled kravsrett automatisk

En norm kan pålegge et offentlig organ en plikt uten at en bestemt privatperson automatisk har en individuelt håndhevbar kravsrett. Derfor må modellen ha egne egenskaper for:

- normadressat
- pliktbærer
- motpart, hvis relevant
- begunstiget
- kravshaver, hvis etablert
- håndhevingsberettiget, hvis etablert
- rettslig interesse, der relevant

Disse rollene kan falle sammen, men må ikke antas å gjøre det.

### 10.3 Pliktobjekt

Et pliktobjekt kan omfatte:

- normadressat
- handling eller unnlatelse
- objekt/tema
- motpart eller mottaker
- begunstiget
- betalingsmottaker eller kravshaver, hvis relevant
- vilkår
- unntak
- frist og gyldighetsperiode
- rettskilde og tekstfragment
- tolkningsstatus

Eksempler som «A skal samarbeide med B», «A skal inngå avtale med B», «A skal informere B» eller «A skal dekke utgiftene til X» må ikke reduseres til én enkel relasjon dersom mottaker, formål, vilkår eller rettsvirkning er nødvendig for å forstå regelen.

### 10.4 Avtaleforhold

Skill mellom:

1. plikt til å inngå avtale
2. handlingen å inngå avtalen
3. avtalen som dokument/rettsakt
4. rettsforholdet avtalen etablerer
5. løpende plikter og rettigheter etter avtalen

### 10.5 Betalingsansvar

Skill minst mellom normadressat, juridisk betalingsansvarlig, finansieringskilde, betalingsmottaker, kravshaver, begunstiget og kostnadsobjekt. «A skal dekke utgiftene» gir ikke alene grunnlag for å anta at alle disse rollene sammenfaller.

## 11. Regelstruktur og rettsvirkninger

En modellert regel bør kunne representere følgende der det er relevant:

- identitet og type
- tekstlig uttrykk og kilde
- normadressat/bærer
- handling eller rettslig forhold
- modalitet
- personelt virkeområde
- saklig virkeområde
- geografisk virkeområde/jurisdiksjon
- tidsmessig virkeområde
- vilkår
- alternative og kumulative vilkår
- unntak og negasjon
- rettsvirkning
- prioritet/forrang, dersom hjemlet
- konsekvens ved brudd, dersom hjemlet
- reaksjon og eventuell reparasjon
- tolkninger og proveniens

Det må ikke forutsettes ett modellert regelobjekt per paragraf. En bestemmelse kan uttrykke flere regler, og én regel kan bero på flere bestemmelser, rettsakter eller andre rettskilder.

### 11.1 Konstitutive og preskriptive regler

Det er nyttig å skille mellom regler som etablerer eller definerer juridiske forhold, og regler som påbyr, tillater eller forbyr handlinger. Dette skillet bør likevel konkretiseres i den valgte LegalRuleML-mappingen og testes mot norsk lovtekst.

### 11.2 Vilkår og unntak

Vilkår må kunne merkes som alternative eller kumulative. Unntak må være knyttet til regelen de begrenser, og må ikke gå tapt når en enklere visning genereres. Negasjon («ikke», «med mindre», «bare dersom») må bevares eksplisitt.

### 11.3 Prioritet og regelkonflikt

Systemet skal ikke utlede regelprioritet utelukkende fra dokumentrekkefølge eller fra at én bestemmelse er nyere. Forrang, derogasjon og regelkonflikt krever rettslig grunnlag og bør merkes som direkte angitt eller juridisk utledet.

## 12. Eksempler på vanskelige referenter og relasjoner

### 12.1 Ordning som grammatisk subjekt: Folketrygden

Hvis lovteksten sier «Folketrygden skal dekke utgiftene», skal Folketrygden først registreres som tekstlig referent. Modellen må deretter undersøke om den aktuelle regelen peker på staten, et forvaltningsorgan, en finansieringsordning eller et annet juridisk ansvarssubjekt.

Ikke klassifiser Folketrygden som et selvstendig rettssubjekt, organ eller organisatorisk enhet uten rettslig grunnlag. Skill mellom:

- tekstlig subjekt
- ordning/finansieringsmekanisme
- rettslig pliktbærer
- administrerende organ
- betalingsmottaker
- begunstiget
- kostnadsobjekt

### 12.2 Sametingets administrasjon

Utsagnet «Sametinget skal ha en egen administrasjon» kan gi grunnlag for å modellere en administrativ enhet som organisatorisk del av Sametinget. Eventuelle bestemmelser om hvem som ansetter personellet, må modelleres separat. Det må ikke sluttes at administrasjonen er et selvstendig rettssubjekt eller organ uten ytterligere grunnlag.

### 12.3 Direktørstilling

Utsagnet «Høyesterett skal ha en direktør» gjelder først og fremst en stilling eller funksjon som Høyesterett skal ha. Den konkrete personen kobles til stillingen ved en separat opplysning om hvem som innehar den i en viss periode. Stillingen, organet og personen er forskjellige forekomster.

### 12.4 Klageinstans, ankeinstans og adgang til overprøving

Skill mellom:

- hvilken instans som er klage-/ankeinstans
- hvilken kompetanse instansen har
- hvem som har adgang til å klage/anke
- om adgangen er betinget av vilkår
- den konkrete klagen/anken
- instansens avgjørelse

«Avgjørelsen kan ankes» skal ikke automatisk tolkes som ubetinget rett til full behandling. Den konkrete prosessordningen, vilkår og eventuelle begrensninger må undersøkes.

### 12.5 Foreleggelse og rådgivende uttalelse

Foreleggelse skal ikke automatisk klassifiseres som kontroll. Modellen må identifisere formålet med foreleggelsen og hvilken rettslig funksjon mottakeren har. En anmodning om en rådgivende tolkningsuttalelse er noe annet enn bindende godkjenning, instruksjon, tilsyn eller overprøving.

### 12.6 «Skal ha» og klasseregler

En regel om at «hver kommune skal ha et kommunestyre» gjelder en klasse av kommuner og kan uttrykkes som en generell konstitutiv/organisatorisk regel. Den samme klasserepresentasjonen passer ikke automatisk for en unik institusjon som Høyesterett eller Sametinget. Profilen må skille mellom lovpålagt struktur på klassenivå og konkrete institusjoner.

### 12.7 Geografisk avgrensning

En bestemmelse som sier at en valgkrets består av navngitte kommuner «i fylket», skal ikke automatisk modelleres som om hele fylket inngår i valgkretsen. Skill mellom medlemskap i et område, geografisk plassering, områdeavgrensning og myndighetens virkeområde.

## 13. Proveniens og status for modellutsagn

Alle modellutsagn bør ha en status som minst kan uttrykkes slik:

1. **Tekstfunn:** opplysningen er eksplisitt i et angitt tekstfragment.
2. **Normalisering:** tekstens innhold er knyttet til et kontrollert begrep uten at en ny materiell rettslig påstand tilføyes.
3. **Juridisk fortolkning:** betydningen følger av en begrunnet tolkning av kilder.
4. **Avledet slutning:** utsagnet følger av flere identifiserte premisser.
5. **Eksternt dokumentert faktum:** eksempelvis et konkret delegeringsvedtak, en instruks eller en oppnevning som ikke følger av lovbestemmelsen alene.
6. **Ubekreftet eller uavklart:** opplysningen kan være relevant, men er ikke juridisk bekreftet.
7. **Omstridt:** det finnes flere dokumenterte, konkurrerende fortolkninger.
8. **Foreldet:** tidligere klassifikasjon er erstattet eller ikke lenger gyldig.

Hver påstand bør kunne ha flere kilder og flere fortolkninger. Når kilder trekker i ulike retninger, må modellen bevare uenigheten fremfor å skjule den bak én kanonisk verdi.

### 13.1 Egen identitet for analyseutsagn

En juridisk analyse eller et fortolkningsutsagn skal ha en egen stabil identifikator, adskilt fra:

- rettskildens identitet
- dokumentversjonens identitet
- identiteten til paragrafen eller tekstfragmentet
- identiteten til standard-/profilversjonen
- identiteten til korpusversjonen

Dette gjør det mulig at samme tekstfragment har flere parallelle analyser, at en analyse revideres uten at tekstfragmentets identitet endres, og at alternative flertalls- og mindretallsvurderinger eller faglige uenigheter bevares. En revidert analyse bør kunne lenkes til analysen den erstatter, uten at den eldre analysen slettes fra historikken.

Et analyseutsagn bør minst ha: `analysis_id`, analysetype/status, kilde- og tekstfragmentreferanser, profilversjon, korpusversjon, forfatter/opphav, opprettelses- og endringstid, begrunnelse, relasjoner til premisser eller andre analyser, og status for uenighet/godkjenning. Dette er et krav til sporbarhet, ikke en påstand om at hver analyse er en autoritativ uttalelse om gjeldende rett.

Proveniens bør kunne angi hvem eller hva som skapte og endret en påstand, hvilken aktivitet som lå bak, hvilke kilder som ble brukt, hvilken standard- og korpusversjon som gjaldt, og om en jurist eller faggruppe har vurdert den. PROV-O kan være en teknisk referanse for dette.

## 14. Standard, profil, implementasjon og korpus

Disse må publiseres og versjoneres separat.

### 14.1 Spesifikasjon for den foreslåtte profilen

Definerer begreper, relasjoner, identifikatorer, tillatte kombinasjoner, konformitetskrav og endringsregler. Inntil formell vedtakelse bør dokumentasjonen konsekvent omtale dette som en foreslått profil/spesifikasjon, ikke en vedtatt norsk standard.

### 14.2 Norsk forvaltningsrettslig profil

Definerer norske spesialiseringer og lokale konvensjoner, med presise definisjoner, rettslig begrunnelse, eksempler og moteksempler.

### 14.3 Regel-IDE som utprøvingsarena og kandidatreferanseimplementasjon

Regel-IDE leser og presenterer kilder, foreslår modellering, lar brukere korrigere, kjører validering, viser proveniens og eksporterer data. Inntil det er demonstrert at systemet konformt implementerer en publisert profil og består dens tester, bør det omtales som utprøvingsarena og kandidatreferanseimplementasjon. Andre implementasjoner skal kunne anvende profilen uten å bruke Regel-IDE.

### 14.4 Modellert korpus

Inneholder konkrete kilder, tekstfunn, klassifikasjoner, referentoppløsninger, juridiske analyser, uenigheter og redaksjonelle beslutninger.

### 14.5 Testkorpus

Et separat, versjonert datasett med juridisk kontrollerte eksempler, moteksempler og vanskelige grensetilfeller. Testdata som brukes til utvikling, skal skilles fra låste testdata som brukes til uavhengig evaluering.

### 14.6 Normativ rangordning av artefakter

Publiseringspakken skal merke hvert artefakt som normativt eller informativt. Anbefalt rekkefølge er:

1. Den teknologinøytrale spesifikasjonen med begrepsdefinisjoner og normative krav.
2. Den maskinlesbare ontologien/profilen og SHACL-valideringsreglene, i den utstrekning spesifikasjonen uttrykkelig betegner dem som normative.
3. Eksempler og testtilfeller som uttrykkelig er normative for en angitt profil.
4. Mappinger til LKIF Core, UFO-L, LegalRuleML og andre eksterne modeller; disse bør være informative i første versjon.
5. Regel-IDE som ikke-normativ utprøvingsarena og senere mulig referanseimplementasjon.

Ved motstrid skal ikke implementatører velge fritt mellom artefaktene. Motstriden skal registreres som en feil i publiseringspakken og løses gjennom en dokumentert og versjonert endring. Den teknologinøytrale spesifikasjonen uttrykker tilsiktet semantikk; maskinlesbare artefakter og valideringsregler skal gjenspeile den.
## 15. Første release candidate: konkret minimumsprodukt

RC1 bør være et avgrenset, testbart minimumsprodukt – ikke en generell fullmodell av norsk rett. Målet er å demonstrere at profilen kan identifisere kilder, representere analyser med sporbar proveniens, beskrive grunnleggende institusjoner og et lite sett kompetanser, og validere data uavhengig av Regel-IDE.

### Obligatorisk i RC1

- ELI-forankret kildeidentifikasjon og Akoma Ntoso-forankret dokument-/tekststruktur, med presise kilde- og tekstfragmentreferanser der kildene tillater det.
- Eget `analysis_id` for hvert juridisk analyseutsagn, adskilt fra rettskilde-, tekstfragment-, profil- og korpusidentitet.
- Grunnleggende begreper for rettssubjekt, organ, organisatorisk enhet, rolle, stilling/embete, ordning, område og oppgave/funksjon.
- Et avgrenset sett organisatoriske, medlemskaps- og område-relasjoner.
- Kompetanseobjekt med arbeidsdefinisjon og et lite antall stabile familier, for eksempel organisering, personell/oppnevning, styring/delegering, normgivning og klage/overprøving.
- Eksplisitt status for tekstfunn, normalisering, juridisk fortolkning, avledet slutning og uavklart/omstridt analyse.
- Tverrgående tid, jurisdiksjon, proveniens, profilversjon og analysehistorikk.
- SHACL-validering for obligatoriske identifikatorer, kildekrav, datatyper og tillatte relasjoner i RC1-profilen.
- En åpen kommenteringsprosess med strukturert feilkategorisering.
- Et låst testsett med uavhengig juridisk kontroll, dokumenterte kritiske feilklasser og reproducerbar konformitetstest.
- En teknologinøytral spesifikasjon, maskinlesbare artefakter, eksempeldata, kjente begrensninger og endringslogg.

### Eksperimentelt eller utenfor RC1

- Normativ avhengighet av LKIF Core eller UFO-L; mappinger skal være informative til de er særskilt evaluert.
- Full Hohfeld/UFO-L-representasjon av normative posisjoner.
- Full LegalRuleML-serialisering av alle regler.
- Full modellering av vilkår, prioritet, konflikt, reaksjon og reparasjon for hele lovkorpuset.
- Automatisk juridisk resonnering for enhver konkret sak.
- Automatisk konklusjon om regelbrudd eller om individuelle kravsretter.
- Full modellering av etterlevelse og virksomheters interne kontrollprosesser.

### Kriterier for å gå videre fra RC1

Før RC1 anses som klar for offentlig konsultasjon, skal profilen ha en publisert definisjonskatalog, valideringsregler, et låst testsett, en rapport om uavhengig juridisk annotering, kjente begrensninger og en transparent prosess for å melde og rette avvik. Før en senere versjon omtales som en referanseimplementasjon, skal Regel-IDE bestå konformitetstestene og helst prøves mot minst én uavhengig implementasjon eller en separat import-/eksportklient.

## 16. Konformitetsprofiler

Konformitetsprofiler bør beskrives som valgfrie moduler, ikke som en påstand om at implementasjoner må realisere en lineær modenhetstrapp. En implementasjon kan støtte kildeprofil og kompetanseprofil uten å støtte konkret etterlevelse.

### Profil A – Kilde og tekst

- kildeidentifikator
- dokumentstruktur
- tekstfragment
- versjon/språk/format der tilgjengelig
- henvisninger og kildeproveniens

### Profil B – Institusjon og struktur

- Profil A
- entiteter, roller, stillinger og oppgaver/funksjoner
- organisatoriske relasjoner
- geografisk og administrativt område
- tidsavhengig institusjonell status

### Profil C – Kompetanse

- Profil B
- kompetansehaver
- kompetansefamilie og handlingstype
- objekt/tema
- vilkår og avgrensning
- hjemmel og tolkningsstatus
- skille mellom kompetanseregel, faktisk disposisjon og rettsvirkning

### Profil D – Normative posisjoner

- Profil C
- plikt og eventuelt kravsrett
- adgang/tillatelse og forbud
- Hohfeld-posisjoner der de er relevant og begrunnet
- normadressat, motpart og begunstiget som separate roller

### Profil E – Regelstruktur

- Profil D
- vilkår, unntak og negasjon
- frister og terskler
- rettsvirkninger og prioritet
- tid og jurisdiksjon
- regelbrudd/reaksjon der det er hjemlet

Hver profil må ha maskinlesbare valideringsregler. Profil A innebærer ikke at profil B–E automatisk er støttet.

## 17. Testkorpus og juridisk validering

Dagens eksisterende eksempelmateriale kan være starten på et testkorpus, men det bør ikke behandles som en uavhengig fasit dersom det er generert eller kontrollert med de samme metodene som skal evalueres.

### 17.1 Testområder

Korpuset bør inneholde:

- klare og entydige eksempler
- vanskelige grenseeksempler
- moteksempler
- alternative tolkninger
- negative regler og unntak
- implisitte eller uklare referenter
- flere utsagn i samme bestemmelse
- henvisningskjeder
- kompetanse og plikt i samme bestemmelse
- ordninger som ikke er aktører
- tidsbegrensede regler og lovendringer
- geografiske virkeområder
- delegering og tilbakekall
- klage og overprøving
- skjønnsmessige vurderingstemaer
- forhold som krever andre kilder enn lovtekst

### 17.2 Juridisk uavhengighet

Minst to jurister bør klassifisere et utvalg uavhengig av hverandre og uten å se systemets forslag. Uenighet dokumenteres; ved behov avklares den av en tredje person eller gjennom en uttrykkelig begrunnet faglig avgjørelse. Det skal rapporteres både enighet og hvor ofte kategoriene viste seg uklare.

### 17.3 Målinger

Mål separat:

- presisjon og gjenfinning for tekstfunn
- korrekt entitets- og referentoppløsning
- klassifisering per relasjon og kompetansefamilie
- bevaring av adressat, vilkår, unntak og polaritet
- kildepresisjon
- korrekt skille mellom tekstfunn, normalisering og juridisk slutning
- antall kritiske feil
- interannotatørenighet og typevise uenigheter

En samlet treffprosent skal ikke brukes som erstatning for juridisk kvalitetsanalyse.

### 17.4 Kritiske feil

Stresstestsettet bør uttrykkelig teste om modellen:

- omdanner adgang til plikt
- plasserer plikten på feil aktør
- forveksler kompetanse med faktisk handling
- forveksler organisatorisk underordning med instruksjonskompetanse
- utleder kravsrett fra en generell plikt uten grunnlag
- mister vilkår, unntak eller negasjon
- antar at grammatisk subjekt er rettssubjekt
- forveksler klage-/ankeinstans med klage-/ankeadgang
- presenterer slutninger som tekstfunn
- bruker feil lovversjon

Et foreslått internt kvalitetskrav kan være null kritiske feil i en definert låst testpakke før release candidate publiseres. Det er ikke bevis for at modellen er feilfri utenfor testpakken.

## 18. Kontroll med typevekst

En ny type bør ikke opprettes bare fordi en bestemmelse bruker et nytt verb. Før opprettelse skal et kort beslutningsnotat besvare:

1. Hvilket konkret spørsmål kan ikke besvares med eksisterende typer?
2. Har skillet selvstendig rettslig eller operativ betydning?
3. Gir typen andre resultater eller slutninger enn nærliggende typer?
4. Forekommer mekanismen i flere rettskilder eller er den sektorspesifikk?
5. Finnes et egnet internasjonalt begrep?
6. Kan forskjellen uttrykkes som en egenskap, rolle, handlingstype eller rettsvirkning i stedet for en ny klasse?
7. Kan typen defineres med positive eksempler, moteksempler og avgrensning mot nabotyper?
8. Hvilke feilsvar eller uriktige slutninger kan oppstå dersom utsagnet plasseres i denne typen?

Klassifikasjonen bør testes gjennom konkrete spørringer, ikke bare vurderes estetisk.

## 19. Offentlig kommentering og faggruppe

### 19.1 Kommentering

Jurister, virksomheter, utviklere og forskere bør kunne kommentere både konkrete klassifikasjoner og selve standarden. Kommentarskjemaet bør skille mellom:

- feil aktør/subjekt
- feil motpart eller begunstiget
- feil entitetstype
- feil relasjon eller kompetansefamilie
- feil handlingstype eller rettsvirkning
- feil polaritet/modalitet
- manglende vilkår eller unntak
- feil eller manglende kilde
- feil referentoppløsning
- skjult juridisk slutning
- uenighet om rettslig tolkning
- svakhet ved definisjon eller ontologi
- forslag til alternativ klassifikasjon

Kommentaren kan inkludere juridisk begrunnelse, kilde, ønsket endring, standardversjon, korpusversjon, dato og eventuell faglig/organisatorisk tilknytning. Personvern og habilitet må håndteres gjennom en tydelig policy.

### 19.2 Faggruppe

Faggruppen bør ha et offentlig mandat og omfatte flere faglige perspektiver:

- alminnelig forvaltningsrett
- statsrett og kommunalrett
- prosessrett
- sektorrettslige fagmiljøer
- lovteknikk og rettskildearbeid
- rettsinformatikk
- ontologi og informasjonsmodellering
- standardisering og datainteroperabilitet
- brukere av standarden og offentlige virksomheter

Faggruppen bør skille mellom:

1. avgjørelser om klassifikasjon av en konkret bestemmelse
2. avgjørelser om generell begrepsmodell
3. mappingavgjørelser til internasjonale modeller
4. endringer i validerings- og konformitetsregler
5. versjons- og migreringsavgjørelser

Juridiske avgjørelser må begrunnes med rettskilder og metode; tekniske avgjørelser må dokumentere konsekvenser for datakvalitet, inferens og interoperabilitet. Uenighet og mindretallssyn bør kunne publiseres.

Faggruppens avgjørelser er **redaksjonelle og standardfaglige beslutninger** om hvordan rettskilder og juridiske analyser representeres i profilen og referansekorpuset. De er ikke rettslig bindende avgjørelser om gjeldende rett og skal ikke presenteres som autoritative avgjørelser på vegne av domstoler, lovgiver eller andre kompetente organer. Faggruppen kan vedta en anbefalt modellering og dokumentere alternative syn, men den kan ikke gjennom standardforvaltning gjøre en omstridt juridisk tolkning autoritativ.

## 20. Versjonering og status

### 20.1 Standardstatus

- arbeidsutkast
- offentlig høring
- release candidate
- godkjent profilversjon
- foreldet
- erstattet

### 20.2 Status for et modellutsagn

- maskinelt foreslått
- manuelt foreslått
- faglig vurdert
- godkjent
- omstridt
- endret etter kommentar
- avvist
- foreldet

### 20.3 Endringshistorikk

Hver ny profilversjon skal ha endringslogg, migreringsveiledning, kompatibilitetsvurdering og oppdaterte valideringstester. En endring i ontologien må utløse en vurdering av hvilke korpusutsagn som bør revideres.

## 21. Risikoområder og avbøtende tiltak

### For tidlig standardisering

**Risiko:** Dagens systemkategorier blir standard før de er prøvd på tvers av rettsområder.

**Tiltak:** Profilér og test et begrenset kjerneutvalg; hold eksperimentelle typer utenfor normativ kjerne.

### Sammenblanding av tekst og fortolkning

**Risiko:** Juridiske slutninger presenteres som direkte tekstfunn.

**Tiltak:** Obligatorisk proveniens- og epistemisk status.

### For sterk likestilling med internasjonale modeller

**Risiko:** Semantisk feil mapping til LKIF, UFO-L eller LegalRuleML.

**Tiltak:** Bruk eksplisitte mappingtyper og eksempler; unngå ekvivalenspåstander uten særskilt bevis.

### Feil automatiske slutninger

Eksempler:

- underordnet betyr ikke alltid instruerbar
- en offentlig plikt innebærer ikke alltid individuell kravsrett
- grammatisk subjekt er ikke nødvendigvis juridisk ansvarsbærer
- «kan ankes» betyr ikke alltid ubetinget rett til full behandling
- «vedtak» betyr ikke automatisk enkeltvedtak
- «forelegge» betyr ikke automatisk kontroll
- «Kongen» avgjør ikke i seg selv spørsmålet om delegerbarhet
- fravær av funnet hjemmel er ikke det samme som et rettslig forbud

### For komplisert førsteversjon

**Risiko:** Full formalisering gjør prosjektet langsomt, skjørt og vanskelig å ta i bruk.

**Tiltak:** Definer konformitetsprofiler og test ett begrenset bruksområde om gangen.

### Ugjennomsiktig faglig styring

**Risiko:** Én utvikler eller liten gruppe blir de facto rettslig autoritet.

**Tiltak:** Åpne beslutningslogger, høringer, begrunnelser, mulighet for revurdering og tydelig skille mellom standardforvaltning og juridisk myndighet.

## 22. Foreslått veikart

### Trinn 1 – Frys og dokumenter dagens modell

- dokumenter eksisterende typer, relasjoner og definisjoner
- registrer kjente feil og tvilstilfeller
- identifiser hvilke kategorier som allerede er blandet på tvers av nivåer

### Trinn 2 – Lag mappingstudie

Sammenlign et representativt utvalg av dagens modell med LKIF Core og UFO-L. Dokumenter også hvilke deler som eventuelt kan representeres i LegalRuleML og hvilke tekniske opplysninger som bør ligge i RDF/OWL, SHACL, PROV-O eller SKOS.

### Trinn 3 – Definer metamodel og profiler

Beskriv identitet, relasjon, tid, proveniens, status, kardinalitet, tillatte kilde-/måltyper og valideringsregler.

### Trinn 4 – Velg begrenset pilotkorpus

Start med et håndterbart utvalg av bestemmelser om organer, kompetanse, delegering, plikt og klage/overprøving fra flere rettsområder. Ikke bruk bare én lov eller bare lett klassifiserbare utsagn.

### Trinn 5 – Lag uavhengig juridisk fasit

La jurister klassifisere et utvalg uavhengig, dokumentere uenighet og avklare begreper før resultatene brukes til evaluering.

### Trinn 6 – Implementer validering

Bruk SHACL eller tilsvarende eksplisitte regler for obligatoriske felter, gyldige relasjoner, kildekrav og konformitet. Hold semantisk inferens atskilt fra validering.

### Trinn 7 – Publiser release candidate

Publiser spesifikasjon, maskinlesbar profil, testkorpus, valideringsregler, eksempler, moteksempler, mappinger, kjente begrensninger, lisens og endringslogg.

### Trinn 8 – Åpen konsultasjon og flere implementasjoner

Inviter jurister, forskere, offentlige virksomheter og utviklere til å prøve spesifikasjonen. Be om minst én uavhengig testimplementasjon eller eksport-/importtest for å kontrollere at standarden ikke bare beskriver Regel-IDE selv.

### Trinn 9 – Vedta profilversjon 1.0

Dette bør skje først når testene, mappingene, styringsmodellen og prosessen for endringshåndtering er tilstrekkelig dokumentert.

## 23. Foreslåtte pilotområder

Piloter bør velges både etter samfunnsmessig relevans og etter hvor godt de tester modellen.

Mulige piloter:

- instruksjonslinjer i statsforvaltningen
- delegeringskjeder og tilbakekall
- klage- og overprøvingsstruktur
- lovpålagt samarbeid mellom kommuner og helseforetak
- betalingsansvar og finansieringsordninger i helselovgivningen
- kraft- og beredskapsorganisering
- melde- og rapporteringsplikter
- organisatoriske krav til kommuner og statlige organer
- oppnevning, valg, ansettelse og avsetting
- geografisk virkeområde og jurisdiksjon

Hver pilot skal ha definerte spørsmål, uavhengig fasit, kritiske feilklasser og tydelige stopp-/godkjenningskriterier.

## 24. Hva som eventuelt er nytt ved en norsk profil

Maskinlesbar rett, juridiske ontologier, Rules as Code, ELI, Akoma Ntoso, LKIF Core, UFO-L og LegalRuleML finnes allerede. Prosjektet bør derfor ikke hevde at de grunnleggende idéene er nye.

Et mulig norsk bidrag kan ligge i en dokumentert kombinasjon av:

- ELI-identifiserte norske kilder
- Akoma Ntoso-struktur
- en kontrollert norsk forvaltningsrettslig profil
- mapping til juridiske kjerneontologier
- eksplisitte kompetanse- og institusjonsbegreper
- normative relasjoner med rettslig proveniens
- versjonering og sporing fra påstand til tekstfragment
- et uavhengig juridisk testkorpus
- åpen kommentering og transparent styring
- en implementasjon som demonstrerer reell interoperabilitet

Nyhetsverdien må dokumenteres gjennom sammenligning med eksisterende arbeid og implementasjoner, ikke bare gjennom at komponentene kombineres i ett prosjekt.

## 25. Samlet anbefaling

Regel-IDE bør i denne fasen beskrives som:

> **En åpen utprøvingsarena og kandidatreferanseimplementasjon for en foreslått norsk profil for maskinlesbar rett. Profilen bruker ELI og Akoma Ntoso for kilder og dokumentstruktur, evaluerer LKIF Core for generelle juridiske begreper, UFO-L/Hohfeld for normative posisjoner og LegalRuleML for formell regelrepresentasjon. Den norske profilen definerer og begrunner de spesialiseringene som trengs for norsk forvaltningsrett. Alle analyseutsagn skal ha egen identitet, kunne spores til kilder og merkes som tekstfunn, normalisering, fortolkning eller slutning.**

Den foreslåtte profilen bør særlig definere:

- norsk institusjonsmodell
- skille mellom rettssubjekt, organ, enhet, rolle, stilling, ordning og område
- kompetansefamilier og handlingstyper
- normative relasjoner mellom parter
- vilkår, unntak og rettsvirkninger
- referentoppløsning for lovtekst
- skillet mellom normadressat, motpart, begunstiget og juridisk ansvarsbærer
- tid, jurisdiksjon, proveniens og epistemisk status
- mappinger til internasjonale modeller
- test- og konformitetskrav

Første release candidate bør være sterk på kildeidentitet, dokumentstruktur, institusjoner og en begrenset kompetansemodell. Den bør etablere et kontrollert fundament for normative posisjoner og regelstruktur uten å kreve at hele lovkorpuset formaliseres i detalj.

Målet bør ikke være å gjøre all juss automatisk beregnbar fra første versjon. Målet bør være å utvikle en åpen, etterprøvbar og utvidbar modell som bevarer de juridiske forskjellene som er nødvendige for pålitelig søk, sammenstilling og videre automatisering.

---

## Referanser og normative ressurser

1. **ELI – Norway implementation.** EUR-Lex, ELI Register: https://eur-lex.europa.eu/eli-register/norway.html
2. **Akoma Ntoso Version 1.0.** OASIS Standard: https://www.oasis-open.org/standard/akn-v1-0/
3. **LKIF Core.** Repository and ontology modules: https://github.com/RinkeHoekstra/lkif-core
4. **UFO-L.** Project and research materials: https://nemo.inf.ufes.br/en/projetos/ufo-l/
5. **LegalRuleML Core Specification Version 1.0.** OASIS Standard: https://www.oasis-open.org/standard/legalruleml-core-specification-version-1-0-oasis-standard/
6. **OWL 2 Overview.** W3C: https://www.w3.org/TR/owl2-overview/
7. **SHACL.** W3C Recommendation: https://www.w3.org/TR/shacl/
8. **PROV-O: The PROV Ontology.** W3C Recommendation: https://www.w3.org/TR/prov-o/
9. **SKOS Reference.** W3C Recommendation: https://www.w3.org/TR/skos-reference/
10. **Forvaltningsloven, lov 20. juni 2025 nr. 81.** Ikrafttredelse må kontrolleres mot gjeldende opplysninger ved konkret anvendelse: https://lovdata.no/lov/2025-06-20-81/

**Kilde- og statusmerknad:** At en standard er nevnt her, betyr ikke at mappingen til Regel-IDE er ferdig eller at standarden er valgt endelig. Alle mappinger og profilvalg må dokumenteres og testes før de eventuelt gis normativ status.
