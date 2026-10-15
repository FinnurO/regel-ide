# Rettslig metode og status for modellutsagn

**Forslag til norsk profil for maskinlesbar rett · metodenotat · arbeidsutkast 15. oktober 2026**

**Status:** Arbeidsutkast, ikke normativt. Notatet beskriver hvordan profilen skal *operasjonalisere* rettslig metode når lovtekst gjøres maskinlesbar. Det er ikke en lærebok i rettskildelære og tar ikke stilling til hva som er gjeldende rett i konkrete spørsmål. Regel-IDE er utprøvingsarena og kandidatreferanseimplementasjon. Eksemplene er hentet fra fasitkontrollen i Regel-IDE (#309) og er merket med saksnummer.

**Bakgrunnslitteratur** (oppgitt uten sidetall, og bare der påstanden er generell):
- T. Eckhoff, *Rettskildelære*, 5. utg. ved J.E. Helgesen, Universitetsforlaget 2001
- E. Boe, *Grunnleggende juridisk metode*, 5. utg., Universitetsforlaget 2025
- T. Eckhoff og E. Smith, *Forvaltningsrett* (om personell, materiell og prosessuell kompetanse)

---

## 1. Hvorfor profilen trenger et metodenotat

En maskinlesbar modell av retten gjør tre ting juristen ellers gjør i hodet:
1. finner kildene,
2. bestemmer hva de betyr,
3. sier hvor sikkert resultatet er.

Gjør modellen dette uten å vise hvordan, blir slutninger presentert som om de sto i loven. Det er hovedrisikoen standardforslaget peker på (§2.5, §4.2, §21).

Notatet gir derfor tre ting:
- **en operativ rettskildelære:** hvilke kilder modellen kan bygge på, og i hvilken rekkefølge de undersøkes (§3–4),
- **en statusskala for modellutsagn**, som knytter hver påstand til den metoden som ligger bak (§5),
- **regler for tolkning, motstrid, tid og skjønn** som modellen må følge når den går ut over ordlyden (§6–9).

Grunnprinsippet er det samme som i Regel-IDE i dag: **ikke gjett**. Mangler det grunnlag, skal modellen si det. Den skal ikke fylle hullet.

## 2. Tre nivåer som må holdes fra hverandre

| Nivå | Spørsmål | Eksempel |
|---|---|---|
| **Kilden** | Hva står det, hvor, og i hvilken versjon? | Domstolloven § 55 første ledd, gjeldende versjon på dato D |
| **Analysen** | Hva betyr det rettslig, og på hvilket grunnlag? | «Kongen i statsråd har oppnevningskompetanse (utnevning) overfor faste dommere» |
| **Anvendelsen** | Hva følger for et konkret faktum? | Ikke i profilens første versjon (standardforslaget §3.2) |

Profilen standardiserer **analysen**, med sporbar kobling til **kilden**. En analyse er en påstand om retten med grunnlag og status. Den er ikke en avgjørelse om gjeldende rett.

## 3. Rettskildefaktorene i modellen

Norsk rettskildelære regner med flere faktorer, ikke bare lovtekst (Eckhoff/Helgesen, Boe). Tabellen viser hvordan hver faktor kan brukes i profilen nå.

| Rettskildefaktor | Kan modellen bygge på den nå? | Hvordan |
|---|---|---|
| Grunnloven, lov | Ja | Korpus (Lovdata/ELI), presis eId |
| Forskrift, inkludert delegeringsvedtak og inndelingsforskrifter | Ja | Korpus. Kobling til hjemmelsloven bør uttrykkes (ELI `based_on`) |
| Kgl.res., instruks, tildelingsbrev, vedtekter, styrevedtak | Delvis | Kilde utenfor korpus med type og dokumentasjonsgrad (primær/sekundær) |
| Forarbeider | Delvis | Kilde utenfor korpus. Brukes som tolkningsstøtte, ikke som hjemmel |
| Rettspraksis | Ikke ennå | Bare som kilde utenfor korpus. Vekt (bindende eller veiledende) er spesifisert, men ikke bygget |
| Forvaltningspraksis | Ikke ennå | Kilde utenfor korpus |
| Juridisk teori | Ikke som grunnlag | Bare som begrunnelse i analysen, aldri som hjemmel |
| EØS-rett og folkerett | Delvis | Kilde utenfor korpus (eksempel: ODA art. 34, Haag-konvensjonen 1970) |
| Reelle hensyn | Ikke som grunnlag | Kan inngå i begrunnelsen for en fortolkning. Utsagnet får da status *fortolkning*, ikke *tekstfunn* |

**Regel:** Bare lov og forskrift i korpus, og dokumenterte kilder utenfor korpus, kan være **hjemmel** for et modellutsagn. De andre faktorene kan **begrunne** en fortolkning. Utsagnet får da en status som viser det (§5).

**Vekt:** Profilen tar foreløpig ikke stilling til hvor tungt hver faktor veier i en konkret avveining. Den skal kunne registrere *at* en fortolkning bygger på for eksempel forarbeider, slik at en jurist kan vurdere vekten. En egen vektskala for rettspraksis finnes i spesifikasjonen (bindende, tungtveiende, veiledende, illustrerende, historisk), men er ikke bygget.

## 4. Rekkefølgen kildene undersøkes i

Modellering av struktur avgjøres nesten alltid i lovteksten selv. Kildene undersøkes derfor i denne rekkefølgen, og den nærmeste kilden med mest autoritet brukes først:

1. sitatet,
2. hele leddet,
3. bestemmelsen,
4. interne henvisninger («etter første ledd», «nevnt i»),
5. definisjoner i samme rettskilde,
6. andre bestemmelser og modellutsagn i samme rettskilde,
7. annen lov eller forskrift i korpus,
8. forarbeider,
9. rettspraksis,
10. alminnelige prinsipper og teori.

**Kilden skal åpnes** når:
- sitatet er ufullstendig eller avhenger av forrige punktum,
- teksten har henvisninger («det samme gjelder», «dersom», «med mindre»),
- forslaget inneholder noe som ikke står i leddet,
- det er tvil om versjonen,
- utsagnet skal bygge på en kilde utenfor korpus.

Dette er ikke en lære om hvilken kilde som veier tyngst ved motstrid (se §7). Det er en arbeidsrekkefølge. I Regel-IDE er den fastsatt i instruksen til juristagenten (`.claude/agents/jurist-utfordrer.md`, #359).

## 5. Status for et modellutsagn

Hvert modellutsagn skal ha **én** status for hvordan det er kommet fram. Statusen sier noe om metoden, ikke om hvem som har godkjent utsagnet. Godkjenning registreres for seg (§10).

| Status | Definisjon | Krav til grunnlag | Eksempel fra fasitkontrollen |
|---|---|---|---|
| **Tekstfunn** | Opplysningen står uttrykkelig i et angitt tekstfragment | eId til fragmentet | «kan ikke delegeres» (sameloven § 2-12 fjerde ledd) gir delegering uttrykkelig forbudt (#356) |
| **Normalisering** | Teksten knyttes til et kontrollert begrep uten ny materiell påstand | eId + begrepet + regelen for normaliseringen | «Det regionale helseforetakets … utgifter skal dekkes av …» gir betalingsmottaker = det RHF-et utgiften gjelder. Vanlig språklig lesning av genitiv (#353) |
| **Fortolkning** | Betydningen følger av en begrunnet tolkning av en eller flere kilder | eId + tolkningsargumentene (§6) + eventuelle andre kilder | Eierstyring i foretaksmøtet er beslutning, ikke instruksjon (helseforetaksloven § 16, sett mot § 29) (#365) |
| **Avledet slutning** | Følger av flere identifiserte modellutsagn etter en angitt regel | Premissene (utsagns-id) + slutningsregelen | Agder lagmannsrett er ankeinstans for Agder tingrett, avledet via tingrett → lagsogn → lagdømme (inndelingsforskriften, #345, #355) |
| **Eksternt dokumentert** | Følger av et konkret dokument som ikke er lovtekst | Kilde utenfor korpus med type, lenke og dokumentasjonsgrad | Helseregion Sør-Øst som ansvarsområde, fra vedtektene til Helse Sør-Øst RHF § 3 (#312) |
| **Uavklart** | Kan være relevant, men er ikke avgjort | Hva som mangler | Hvilket organ som forvalter folketrygden utenom kapittel 5: synlig hull (#353) |
| **Omstridt** | Det finnes flere dokumenterte, konkurrerende fortolkninger | Hver fortolkning med eget grunnlag | (Ingen i fasiten ennå. Uenighet er så langt løst i debattrundene.) |
| **Foreldet** | Tidligere klassifisering som er erstattet | Henvisning til utsagnet som erstatter den | «Forskriftskompetanse» som egen type, erstattet av normgivning med normform forskrift (#341) |

**Viktige konsekvenser:**
- **En slutning kan aldri vises som tekstfunn.** Eksempel: at «Kongen» uten «i statsråd» gir delegerbar kompetanse, er en slutning etter alminnelige regler. Den skal vises som «trolig delegerbar (slutning)» (#335).
- **Statusen følger utsagnet.** Når en slutning bygger på premisser, kan den ikke ha sterkere status enn det svakeste premisset.
- **Uenighet bevares.** Når kildene trekker i ulike retninger, får utsagnet status *omstridt* med alle fortolkningene. Det skal ikke velges én av dem i stillhet.
- **«Ikke funnet» betyr ikke «finnes ikke».** At modellen ikke finner hjemmel, er ikke det samme som at handlingen er forbudt eller kompetansen mangler (standardforslaget §21).

## 6. Tolkning

Når et utsagn får status *fortolkning*, skal begrunnelsen angi hvilke tolkningsargumenter den bygger på. Profilen bruker en enkel, kontrollert liste:

| Argument | Beskrivelse |
|---|---|
| Ordlyd | Naturlig språklig forståelse av teksten |
| Kontekst og system | Sammenhengen med andre bestemmelser i samme eller annen lov (f.eks. at loven bruker «instruks» bare der det er hierarki) |
| Formål | Bestemmelsens eller lovens formål |
| Forarbeider | Lovgivers uttalte forutsetninger |
| Praksis | Rettspraksis eller forvaltningspraksis |
| Reelle hensyn | Vurderinger av hva som er en god regel |
| Antitetisk eller analogisk | Slutning fra det som ikke er sagt, eller fra lignende regler |

Profilen avgjør ikke hvordan argumentene skal veies. Den sikrer at det går fram *hvilke* argumenter som er brukt, slik at en jurist kan etterprøve vurderingen.

**Klassifikasjonsreglene i Regel-IDE er også tolkningsregler** og må dokumenteres som det:
- **Disposisjon først, funksjon for seg.** Hovedtypen bestemmes av hva disposisjonen *er*. Formål og reaksjonskarakter registreres for seg. En avsetting som reaksjon er fortsatt en avsetting (#355).
- **Bindende svar, partsadgang eller råd.** Gir mottakeren et bindende svar, er det mottakerens kompetanse. Er det en part som setter i gang, er det prosessuell adgang (regellaget). Ber organet om et råd, er det konsultasjon (#355).
- **Vedtak** er enkeltvedtak bare når den forvaltningsrettslige definisjonen er oppfylt. Ordene «vedtar», «avgjør» og «beslutter» er ikke nok alene (#356).
- **Kompetanse, oppgave, plikt og kravsrett er fire ting.** «Skal samordne» viser en oppgave og en plikt, men ikke uten videre instruksjonskompetanse. En offentlig plikt gir ikke uten videre noen en individuell kravsrett (standardforslaget §2.6, §7.8). ~~*Oppgave/funksjon er ennå ikke et begrep i modellen. Det er det viktigste hullet (se §11).*~~ [ENDRET, #368, 2026-10-15] Oppgave/funksjon er kategorien F, definert i begrepsmanualen (02 §F). Klassifiseringen til F er en tolkning og registreres som `fortolkning` med tolkningsargumenter, eller `tekstfunn` når teksten selv bruker funksjonsordet.
- **Tekstens subjekt er ikke nødvendigvis rettssubjektet.** «Folketrygden skal dekke» peker på en ordning, ikke på et rettssubjekt (#353).

## 7. Motstrid og forrang

Når regler trekker i ulike retninger, løser norsk rett det med prinsipper om forrang. Høyere rang går foran lavere (lex superior). Særregel går foran generell regel (lex specialis). Senere regel går foran tidligere (lex posterior). I tillegg gjelder uttrykkelige forrangsbestemmelser.

**Regler for profilen:**
1. **Forrang skal aldri utledes av rekkefølge, dato eller plassering alene** (standardforslaget §11.3). At en forskrift er nyere, betyr ikke automatisk at den går foran.
2. Et forrangsforhold skal merkes som **direkte angitt** eller **juridisk utledet**:
   - Direkte angitt: teksten sier det, for eksempel «uavhengig av første ledd» i energiloven § 10-2 annet ledd.
   - Juridisk utledet: forrangen følger av lex superior, specialis eller posterior. Det får status *fortolkning*, med prinsippet som argument.
3. **Trinnhøyde:** en forskrift gitt i medhold av en lov skal kobles til hjemmelen (ELI `based_on`). Da kan modellen vise at forskriften må holde seg innenfor lovens rammer, men den skal ikke selv avgjøre om den gjør det.
4. **Unntak** innenfor samme regel (for eksempel «med mindre») hører til regellaget som eget unntak. Unntak i et delegeringsvedtak er avgrensning på delegeringen, ikke en negativ kobling (#341, #356).

## 8. Tid

Tre tidsdimensjoner må holdes fra hverandre (standardforslaget §4.6):

| Tid | Spørsmål | Hvor i modellen |
|---|---|---|
| Kildens gyldighet | Når gjelder bestemmelsen? Ikrafttredelse, endring, opphevelse, overgangsregler | Versjon av rettskilden (ELI/AKN-uttrykket) |
| Forholdets gyldighet | Når eksisterer aktøren eller kompetansen? | Gyldig fra/til på modellutsagnet |
| Analysens gyldighet | Når ble klassifiseringen gjort, og under hvilken profilversjon? | Opphav og versjon på analysen |

**Konsekvenser:**
- **Hjemmelen må peke på en versjon**, ikke bare på bestemmelsen. I dag peker eId-en på gjeldende tekst uten dato. Endres teksten, mister analysen grunnlaget sitt uten at det synes.
- **Klassifiseringer som bygger på en definisjon i en lov, er tidsavhengige.** Eksempel: kategorien enkeltvedtak følger i dag forvaltningsloven 1967 § 2. Den nye forvaltningsloven (LOV-2025-06-20-81) er vedtatt, men ikke i kraft (Kongen bestemmer). Den flytter definisjonene til § 7 og lovfester delegering (§§ 22–24). Når den trer i kraft, må analyser som bygger på den gamle § 2, vurderes på nytt, og delegeringsadgang som i dag er «ikke regulert», kan få et lovgrunnlag.
- **En analyse slettes ikke når den erstattes.** Den blir *foreldet* og lenkes til den som erstatter den.

## 9. Skjønn

Lovtekst gir ofte organet et valg («kan»). Metodisk er det forskjell på:
- **fritt skjønn (forvaltningsskjønn):** organet avgjør selv om og hvordan det vil bruke kompetansen, innenfor rammer som saklighet og forholdsmessighet,
- **rettsanvendelsesskjønn:** vilkåret er en rettslig standard som kan prøves fullt av domstolene, for eksempel «uklanderlig vandel».

**For profilen:**
- I regellaget er skjønn spesifisert for vilkår (skjønnsgrunnlag, skjønnsmoment).
- I strukturlaget har en kompetanse med «kan» i dag ingen markør for skjønn. Profilen bør skille mellom «kan» som *adgang* (organet har kompetansen og velger om det vil bruke den) og «kan» som *skjønnsmessig vilkår*. Det er en fortolkning og må merkes slik.
- Profilen skal ikke modellere *hvordan* skjønnet skal utøves i en konkret sak.

## 10. Kvalitetssikring og autoritet

**Hvem kontrollerer.** Regel-IDE bruker i dag:
1. **modellering:** maskinelt forslag, og etter hvert mønster- og KI-lag,
2. **utfordring:** en KI-agent instruert som forvaltningsjurist, i to debattrunder (#359, CLAUDE.md §23),
3. **avgjørelse:** fagansvarlig avgjør der det fortsatt er uenighet.

Dette er godt nok for utviklingsarbeid, men det er **ikke uavhengig juridisk kontroll**. Juristagenten er KI, og fasiten er laget med de samme metodene som skal evalueres. Skal fasiten brukes som bevis, må et låst utvalg klassifiseres av minst to menneskelige jurister, uavhengig av hverandre og uten å se systemets forslag. Uenigheten mellom dem dokumenteres (standardforslaget §17.2).

**Hva en avgjørelse er.** Når fagansvarlig eller en faggruppe avgjør en klassifisering, er det en **redaksjonell beslutning om hvordan en analyse representeres**. Det er ikke en avgjørelse om gjeldende rett (standardforslaget §19.2). Omstridte spørsmål skal kunne stå som omstridte.

**Godkjenning er ikke status.** Om et utsagn er godkjent (maskinelt foreslått, faglig vurdert, godkjent, avvist), registreres for seg. Et godkjent utsagn kan ha status *fortolkning*. Et tekstfunn kan være ikke vurdert.

## 11. Konsekvenser for modellen (til arkitekturvurderingen)

Notatet forutsetter endringer i modellen. De vurderes i en egen arkitekturvurdering, ikke her:

1. **Status for alle modellutsagn** (§5), ikke bare for delegering. Godkjenning er et eget felt.
2. **Analysen får egen identitet og historikk.** Et tekstfragment kan ha flere analyser, og en erstattet analyse blir *foreldet*, ikke slettet.
3. **Hjemmelen peker på en versjon** (§8), og kategorier som er bundet til en lov, får tidsangivelse.
4. **Oppgave/funksjon blir et nytt begrep** (§6), atskilt fra kompetanse og plikt.
5. **Plikt er ikke kravsrett.** Rollene normadressat, pliktbærer, motpart, begunstiget og kravshaver holdes atskilt.
6. **Tolkningsargumentene registreres** når status er fortolkning (§6).
7. **Forrang** merkes som direkte angitt eller juridisk utledet (§7).
8. **Skjønnsmarkør** på «kan»-kompetanse (§9).

## 12. Åpne spørsmål

1. Skal profilen ha en egen vektskala for andre faktorer enn rettspraksis, eller bare registrere hvilke faktorer som er brukt?
2. Hvem skal utføre den uavhengige menneskelige kontrollen av testkorpuset, og hvor stort skal utvalget være?
3. Skal *normalisering* kreve en dokumentert normaliseringsregel (for eksempel genitivregelen for betalingsmottaker), slik at den kan etterprøves?
4. Hvor langt skal profilen gå i å registrere tolkningsargumenter? Kontrollert liste som i §6, eller fri tekst?
5. Når den nye forvaltningsloven trer i kraft: skal eldre analyser bli stående med gammel lovversjon som grunnlag, eller vurderes på nytt automatisk?
