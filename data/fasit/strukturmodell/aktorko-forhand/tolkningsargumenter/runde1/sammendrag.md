# Tolkningsargumenter for fortolkningshjemlene i aktørkøen, juristutfordreren runde 1

Inndata: `../inndata.json` (29 navn med kunnskapsstatus `fortolkning` i `klassifisering.json`). Vurderingen er gjort uten modellererens
begrunnelser (CLAUDE.md §23). Kildene er den delte basen (bare SELECT) og Lovdata for shl. § 2-1 a og FOR-2017-06-22-1050 § 1.

## Tall

| Dom | Antall |
|---|---|
| holder | 11 (n2, n3, n4, n33, n102, n116, n118, n120, n127, n146, n187) |
| innvending | 17 |
| kontroller | 1 (n61) |
| prinsipiell | 0 |

Innvendingene fordelt:

| Gjelder / alvor | lav | middels | hoy |
|---|---|---|---|
| raden (argumentene eller statusen) | 4 (n13, n168, n180, n181) | 3 (n24, n36, n192) | 0 |
| utenfor_raden (hjemmelmerknad, relasjon) | 9 (n17, n31, n42, n57, n98, n100, n141, n151, n190) | 1 (n7) | 0 |

**Argumentene holder for 22 av 29 navn.** Rettingen gjelder 7 navn: én gang argumentlista (n168) og seks ganger statusen (n13, n24, n36, n180, n181,
n192).

## De viktigste innvendingene

1. **n24, n36, n192: forskriften definerer rollene selv (middels).** FOR-2017-06-22-1050 § 1 første ledd nr. 1–3 sier «I denne forskriften
   menes med bostedskommune / vertskommune / omsorgskommune: den kommunen …» (basen, og Lovdata kontrollert). Hjemmelen bør være definisjonen,
   med status **tekstfunn**, slik n86 (dl. § 52) og n145 er. Hjemmelmerknaden «forskriften bruker rollen uten å definere den» er feil.
   Skjemaet har ikke eId i `foreslatt`, så rettingen forutsetter at eId-en endres. I n192 er relasjonen også snudd (rolle_for kommune): den
   bør være rolle_innehas_av kommune, som forrige runde rettet i n24 og n36.
2. **n7: relasjonen «omtale_av regionalt helseforetak» bæres ikke av noen av argumentene (middels, utenfor raden).** Den strider mot hjemmelen
   (shl. § 1-2 første ledd: «staten og private»). Den gjeninnfører også RHF-koblingen som forrige runde avviste og modellereren aksepterte. Som
   navneformbinding (docs/33 §5.16.2) ville den gjort ordet til en navneform for RHF.
3. **n13: tekstfunn, ikke normalisering (lav).** «Forliksrådet skal ha tre medlemmer»: kjernen «medlemmer» står ordrett i køens form, og «tre»
   er et tillatt tallord foran. Kalibreringen er n144/n150 (tekstfunn på «En leder og en nestleder» i en «skal ha … være sammensatt
   slik»-setning). N143 og n110 fikk normalisering bare fordi ordformen i teksten er en annen enn køens.
4. **n180, n181: normalisering, ikke fortolkning (lav).** Plassene opprettes i § 11 andre ledd nr. 2 og § 3 andre ledd nr. 4 («medlem … som
   skal representere elever og studenter»). Forskriften kaller dem selv «student- og elevrepresentantene» og «studentrepresentantene» i neste
   ledd. Sammentrekningen gir ingen ny materiell påstand, slik n143 (lagmenn → lagmann) og n184 («ledes av et styre») heller ikke gjør. Dette
   avviker fra forrige runde, som valgte fortolkning bare fordi ordet ikke står i punktet som oppretter plassen.
5. **n168: kontekst og system bærer ikke (lav).** Alt slutningen trenger, står i dl. § 101 (rollen og rolle_for i første ledd, medlemsregelen i
   andre ledd). Riktig liste er bare ordlyd.
6. **n31: henvisningen i medlemsregelen er foreldet (lav, utenfor raden).** Utpekingen av institusjoner står i shl. § 2-1 a **femte** ledd. Fjerde
   ledd er plikten til å planlegge og evaluere. Hotl. §§ 10-3 og 10-4 viser fortsatt til «fjerde ledd», mens § 10-2 viser til «femte».
   Argumentene holder.
7. **n61 (kontroller):** ledes_av Barne-, ungdoms- og familiedirektoratet [slutning: sentralt nivå = direktoratet] har ingen støtte i korpuset.
   Slutningen bæres verken av ordlyd eller kontekst og system. Den krever forarbeider eller eksternt dokumentert grunnlag, og det er ikke
   kontrollert. Argumentene for hjemmelen holder.

## Tverrgående mønstre

- **Hjemmelmerknaden skrives inn i begrunnelsen, og 16 av 29 er utdaterte eller feil.** `AktorkoSkrivTjeneste.cs` (linje 169) setter begrunnelsen til
  «… «sitat». {hjemmel_merknad}.». Bare den eksakte verdien «ikke relevant» filtreres bort. For fortolkning er begrunnelsen kravet i
  docs/33 §4.6.
  - Ti merknader sier fortsatt «mangler» eller «mangler: …» fra før juristrunden fant hjemmelen: n17, n42, n57, n100, n141, n151, n168, n180,
    n181, n190.
  - n13 har «ikke relevant: …», som ikke filtreres.
  - n98 viser til § 1, men eId-en er § 2.
  - n7 gjelder den gamle hjemmelen.
  - n192 har «-».
  - n24 og n36 sier at rollen ikke er definert, og det er feil.

  Det er ikke en feil i argumentene, men det som lagres, blir selvmotsigende («hjemmel … mangler»). **Forslag:** skriv om merknadene før
  «Godta forslaget» brukes, eller filtrer verdier som begynner med «mangler» eller «ikke relevant».
- **Kontekst og system er standardvalget (25 av 29 forslag har ordlyd + kontekst_system).** Det bærer i de fleste tilfellene, fordi
  hjemmelen forutsetter noden uten å opprette den, og klassen eller rollen må bekreftes av andre bestemmelser. Det bærer ikke der alt står i
  samme paragraf (n168). Der modellereren har valgt ordlyd alene (n118, n187), er det riktig.
- **Statusgrensen.** Praksisen i fila er:
  - **tekstfunn:** ordet står ordrett i en setning som oppretter eller definerer noden;
  - **normalisering:** en språklig omforming uten ny påstand;
  - **fortolkning:** bestemmelsen forutsetter noden, eller må leses sammen med andre kilder.

  Seks forslag ligger for lavt på denne skalaen (n13, n24, n36, n180, n181, n192; n13 er normalisering som bør være tekstfunn). Modellererens
  egne forslag om normalisering for n13 og n57 er riktige i retning. N57 holder, n13 bør gå ett trinn videre.
- **Formål, forarbeider, praksis og reelle hensyn bærer ikke i noen av de 29.** At hjemmelen er en formålsbestemmelse (n102, n151, n190),
  gjør ikke formål til et tolkningsargument: teksten leses for hva den sier om noden. Antitetisk slutning finnes bare i en relasjonsmerknad
  (n120, «ikke eget rettssubjekt, jf. § 7a»). Den hører til relasjonen, ikke hjemmelen.
- **Tolkningsargumenter med normalisering eller tekstfunn avvises av godkjenningen** (linje 159). Reservelistene for n13 og n57 må derfor
  ikke sendes.

## Prinsipielle innvendinger

Ingen. Én mangel i formatet: `foreslatt` har ikke eId, så rettingen for n24, n36 og n192 står i innvendingsteksten.
