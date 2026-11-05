# Aktørkøen: forhåndsklassifisering av de 193 navnene

Gjelder aktørkøen (`/aktorko`, docs/33 §5.16) slik den sto i den delte basen 2026-11-05: 193 navn fra 393 ventende forekomster i 80 rettskilder. Hentet med `GET /api/aktorko` og `/api/aktorko/{nøkkel}`, nodeteksten med SELECT. Ingenting er skrevet til basen. Modellereren klassifiserte alle navnene (`klassifisering-runde0.json`), og juristutfordreren vurderte dem i to runder (`a/` n2–n96, `b/` n97–n193; inndata i `inndata-a.json` og `inndata-b.json`). Det endelige resultatet med relasjoner, sitater og id-er står i `klassifisering.json`. Id-ene til eksisterende noder er kontrollert mot basen (39 id-er).

Johanns avgjørelser 2026-11-05 er lagt til grunn. En klasse har hjemmel eller medlemsregel med kilde. «bostedskommune» og lignende er roller. «den kommunen» og «En kommune» er generiske omtaler. Norge, staten, fylkeskommunen og departementene venter på beslutningsnotatet og er ikke vurdert av juristen.

**Juristrunden:** runde 1 ga 121 holder, 65 innvendinger og 3 kontroller. Modellereren aksepterte 65, svarte delvis på 1 (n40 regjeringen, satt til «venter») og avviste 2 (n136 og n137, sentralitetssone: vilkår i regellaget i stedet for klasse). Juristen godtok alle tre i runde 2. **Ingen uenighet står igjen.**

## Fordeling

| Type | Antall |
|---|---:|
| aktør | 13 |
| klasse | 23 |
| rolle | 22 |
| område | 6 |
| omtale | 82 |
| ikke_aktør | 39 |
| venter | 8 |

| Handling i køen | Antall |
|---|---:|
| tagg som omtale | 73 |
| ikke aktør | 39 |
| opprett klasse | 23 |
| opprett rolle | 22 |
| opprett aktør | 13 |
| vent (modellvalg) | 8 |
| koble til kjent aktør | 7 |
| opprett område | 6 |
| ta med i n70 | 1 |
| ta med i n97 | 1 |

**Nye aktører (13):** Utdanningsdirektoratet (organ, sikkerhet hoy); NOKUTs klagenemnd (organ, sikkerhet hoy); Longyearbyen lokalstyre (rettssubjekt, sikkerhet hoy); Barne-, ungdoms- og familieetaten (organ, sikkerhet middels); Felles klagenemnd (organ, sikkerhet middels); Folketrygden (ordning, sikkerhet middels); Høyesteretts ankeutvalg (organ, sikkerhet middels); Innstillingsrådet for dommere (organ, sikkerhet hoy); Lånekassens klagenemnd (organ, sikkerhet hoy); NOKUT (organ, sikkerhet hoy); Statens undersøkelseskommisjon for helse- og omsorgstjenesten (organ, sikkerhet hoy); Styret for domstoladministrasjonen (organ, sikkerhet hoy); Sysselmesteren på Svalbard (organ, sikkerhet hoy).

**Tilleggsnoder** som relasjonene viser til, men som ikke er navn i køen: forvaltningsorgan (klasse); lagmannsrett (klasse); barnekoordinator (rolle); Longyearbyen arealplanområde (område).

## Anbefaling per navn

P = pilotnavn (#375). «Jurist» er dom i runde 1 → modellererens svar → replikk i runde 2. Hjemmel er eId under `https://lovdata.no/eli/` med kunnskapsstatus; sitatene står i `klassifisering.json`.

| Nr | Navn | Forek. | Anbefaling | Type | Ny node | Relasjoner | Hjemmel | Sikkerhet | Jurist | Begrunnelse (forkortet) |
|---|---|---:|---|---|---|---|---|---|---|---|
| n1 P | fylkeskommunen | 94 | vent (modellvalg) | venter |  |  | ikke relevant | - | ikke_vurdert | venter på modellvalg (Norge/nivåene). Johanns avgjørelse 2026-11-05: ingen forslag før beslutningsnotatet. |
| n2 P | barnehagen | 21 | opprett klasse | klasse | «barnehage» |  | lov/2005/06/17/64/nor/§6/ledd-1 (fortolkning) | hoy | holder | «barnehagen» brukes distributivt om enhver barnehage (generisk omtale av klassen), aldri om én navngitt virksomhet. Ikke en aktør. Rammeplan- og merknadsforekomstene (4 av 21) er ikke hjemmel. |
| n3 | barnehageeier | 9 | opprett rolle | rolle | «barnehageeier» | rolle_for «barnehage» (ny); rolle_innehas_av kommune (`eb25cd79`); rolle_innehas_av «privat barnehage» (ny) | lov/2005/06/17/64/nor/§7 (fortolkning) | hoy | innvending middels → aksepter | En rolle et rettssubjekt har overfor en barnehage, ikke en aktør. Rammeplanen sier selv «jf. barnehageloven § 7 første ledd». |
| n4 | private barnehager | 9 | opprett klasse | klasse | «privat barnehage» | underklasse_av «barnehage» (ny) | lov/2005/06/17/64/nor/§7a/ledd-1 (fortolkning) | hoy | holder | Flertallsform uten artikkel om en avgrenset mengde barnehager. Underklasse av barnehage. |
| n5 P | Utdanningsdirektoratet | 9 | opprett aktør | aktør | «Utdanningsdirektoratet» (organ) |  | mangler | hoy | holder | Egennavn på én statlig virksomhet. Ikke i basen (søkt på «utdanning» blant 567 virksomheter). PILOT. |
| n6 | departementene | 6 | vent (modellvalg) | venter |  |  | ikke relevant | - | ikke_vurdert | venter på modellvalg (Norge/nivåene). |
| n7 | spesialisthelsetjenesten | 5 | opprett klasse | klasse | «virksomheter som yter spesialisthelsetjenester» | omtale_av «regionalt helseforetak» (ny) | lov/1999/07/02/61/nor/§1-2/ledd-1 (fortolkning) | lav | innvending middels → aksepter | Et tjenestenivå, ikke en virksomhet. I hol. kap. 9 betyr det de som yter spesialisthelsetjenester i saken. Kan også leses som generisk omtale av klassen helseforetak. |
| n8 | foreldrerådet | 4 | opprett klasse | klasse | «foreldreråd» | organ_i «barnehage» (ny) | lov/2005/06/17/64/nor/§4/ledd-1 (normalisering) | hoy | holder | Hver barnehage har sitt eget foreldreråd, så ordet er en klasse av organer (juristrunde-440: «klasser eller roller»), ikke én aktør. PILOT-nabo (genitivforslag fra «barnehagens foreldreråd»). |
| n9 | Norge | 4 | vent (modellvalg) | venter |  |  | ikke relevant | - | ikke_vurdert | venter på modellvalg (Norge/nivåene). (Johann: område med hjemmel i Grl. § 1.) |
| n10 | regionalt helseforetak | 4 | opprett klasse | klasse | «regionalt helseforetak» | medlem HELSE NORD RHF (`f72fa629`); medlem HELSE VEST RHF (`448071cb`); medlem HELSE SØR-ØST RHF (`44ce9fbc`); medlem HELSE MIDT-NORGE RHF (`11fe43ce`) | lov/2001/06/15/93/nor/§4/ledd-1 (tekstfunn) | hoy | innvending middels → aksepter | Ubestemt form («regionalt helseforetak plikter …») om hvert av de fire RHF-ene. Medlemmene finnes alt som virksomheter. |
| n11 | styrer | 4 | opprett rolle | rolle | «styrer» | rolle_for «barnehage» (ny) | forskrift/2017/06/22/1049/nor/§4/ledd-1 (tekstfunn) | hoy | holder | En stilling i barnehagen (person), ikke et organ. «styrer»/«styreren» er rollen. |
| n12 | andre kommuner | 3 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | Omtale av klassen kommune. Ingen ny node. |
| n13 P | medlemmer | 3 | opprett rolle | rolle | «medlem av forliksrådet» | rolle_for «forliksråd» (ny) | lov/1915/08/13/5/nor/§27/ledd-2 (fortolkning) | hoy | innvending middels → aksepter | Medlemmer av et organ er personer med verv, ikke aktører. Grupperingen slår sammen to organer (NOKUTs klagenemnd og forliksrådet). PILOT. |
| n14 P | skolen | 3 | opprett klasse | klasse | «skole» |  | mangler | middels | holder | Generisk omtale av skolen barnet går over til (bhl. § 2 a). En klasse (juristrunde-440), ikke én aktør. PILOT. |
| n15 | tilsynsmyndigheten | 3 | opprett rolle | rolle | «tilsynsmyndighet» | rolle_innehas_av STATENS HELSETILSYN (`96d9bb64`); rolle_innehas_av statsforvalter (`99f62b57`); rolle_innehas_av kommune (`eb25cd79`) | lov/2017/12/15/107/nor/§3/ledd-1 (tekstfunn) | middels | innvending lav → aksepter | En funksjon som ulike organer har etter hver lov. Grupperingen slår sammen hol. (Helsetilsynet/statsforvalteren) og forskriften om miljø i barnehager (kommunen). |
| n16 | Virksomheten | 3 | ikke aktør | ikke_aktør |  | omtale_av «virksomheter som er omfattet av helse- og omsorgstjenesteloven» (ny) | ikke relevant | hoy | innvending lav → aksepter | Henviser tilbake til virksomheten som er nevnt før (barnehage/skole i forskriften, helse- og omsorgsvirksomhet i hol.). Ikke en aktør. To ulike referenter i samme navn. |
| n17 | åpne barnehager | 3 | opprett klasse | klasse | «åpen barnehage» | underklasse_av «barnehage» (ny) | lov/2005/06/17/64/nor/§14/ledd-1 (fortolkning) | middels | innvending lav → aksepter | Barnehageform, underklasse av barnehage. |
| n18 | alle som arbeider i barnehagen | 2 | ikke aktør | ikke_aktør |  | personkrets_i «barnehage» (ny) | ikke relevant | hoy | holder | Personkrets (alle ansatte) med meldeplikt. Personer er utsatt som nodetype (docs/33 §4.1); plikten hører til regellaget. |
| n19 | barna | 2 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Barna er private personer (rammeplanen). Juristrunde-440: «barna … er ikke aktører». |
| n20 | barnehagelærer | 2 | ikke aktør | ikke_aktør |  |  | ikke relevant | middels | holder | Profesjon og kvalifikasjonskrav («utdannet barnehagelærer»), ikke en innehaver av myndighet eller plikt. Hører til regellaget. |
| n21 | barnehagens samarbeidsutvalg | 2 | tagg som omtale | omtale |  | omtale_av «samarbeidsutvalg» (ny) | ikke relevant | hoy | holder | «X-ens Y»: organet Y i X. Godta genitivforslaget mot «samarbeidsutvalget». |
| n22 P | barnehagens styrer | 2 | tagg som omtale | omtale |  | omtale_av «styrer» (ny) | ikke relevant | hoy | holder | «X-ens Y» der Y er en stilling: rollen styrer. Godta genitivforslaget. PILOT. |
| n23 | barnevernstjenesten | 2 | opprett klasse | klasse | «barnevernstjeneste» | organ_i kommune (`eb25cd79`) | lov/2021/06/18/97/nor/§15-3/ledd-1 (normalisering) | hoy | holder | Kommunal tjeneste i hver kommune, altså en klasse av organisatoriske enheter, ikke én aktør. |
| n24 P | bostedskommune | 2 | opprett rolle | rolle | «bostedskommune» | rolle_innehas_av kommune (`eb25cd79`) | forskrift/2017/06/22/1050/nor/§2/ledd-1 (fortolkning) | hoy | innvending lav → aksepter | Johanns avgjørelse 2026-11-05: en rolle en kommune har overfor noen. PILOT. |
| n25 | den kommunen | 2 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | Johanns avgjørelse: «den kommunen» er omtale av klassen, merket generisk. Her med relativsetning («som har bistandsbehovet»), som er en rolle i saken. |
| n26 | den nye kommunen | 2 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | middels | innvending lav → aksepter | En kommune i en bestemt situasjon (etter sammenslåing). Ikke en ny aktør; alternativt omtale av klassen kommune med vilkår. |
| n27 | det regionale helseforetaket | 2 | tagg som omtale | omtale |  | omtale_av «regionalt helseforetak» (ny) | ikke relevant | hoy | holder | Bestemt form om det RHF-et som hører til kommunens helseregion (paring via område, docs/33 §4.2). |
| n28 | Finland | 2 | opprett område | område | «Finland» |  | ikke relevant | - | innvending lav → aksepter | venter på modellvalg (Norge/nivåene). Stat som område (§ 39) og som traktatpart (§ 81) er samme valg som for Norge. |
| n29 | Folkeregisteret | 2 | ikke aktør | ikke_aktør |  | fores_av SKATTEETATEN (`5e69bca7`) | lov/2016/12/09/88/nor/§1-1/ledd-1 (tekstfunn) | hoy | innvending lav → aksepter | Et register (gjenstand), ikke en aktør. |
| n30 | foreldre | 2 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Foreldre er private parter. |
| n31 | institusjonen | 2 | opprett klasse | klasse | «institusjon utpekt av regionalt helseforetak» |  | lov/2011/06/24/30/nor/§10-4/ledd-1 (fortolkning) | middels | innvending middels → aksepter | Henvisning til en institusjon som er nevnt før (barnevernsinstitusjon / rusinstitusjon). To referenter i samme navn. |
| n32 | KD | 2 | koble til kjent aktør | omtale |  | samme_som Kunnskapsdepartementet (`61b9a4f8`) | ikke relevant | hoy | holder | Kortform for Kunnskapsdepartementet i satstabellene i finansieringsforskriften. Koble som kortform, ikke ny aktør. |
| n33 P | Klagenemnda | 2 | opprett aktør | aktør | «NOKUTs klagenemnd» (organ) | kortform_for «NOKUTs klagenemnd» (ny); klageinstans_for «NOKUT» (ny) | forskrift/2025/07/02/1462/nor/§1/ledd-1/punkt-1 (fortolkning) | hoy | holder | «Klagenemnda» i §§ 2–3 er kortform for NOKUTs klagenemnd (kap. 2). Opprett aktøren med det fulle navnet, og bind kortformen til kapitlet: forskriften har tre klagenemnder. PILOT. |
| n34 | kommunen der barnet er folkeregistrert | 2 | tagg som omtale | omtale |  | omtale_av «bostedskommune» (ny) | ikke relevant | hoy | holder | «X der …»: rollen bostedskommune (folkeregistrert = bosatt). Ikke ny aktør. |
| n35 | Longyearbyen lokalstyre | 2 | opprett aktør | aktør | «Longyearbyen lokalstyre» (rettssubjekt) | har_ansvarsomrade «Longyearbyen arealplanområde» (ny) | lov/1925/07/17/11/nor/§30/ledd-2 (tekstfunn) | hoy | innvending hoy → aksepter | Navngitt rettssubjekt. Ikke i basen. |
| n36 P | omsorgskommune | 2 | opprett rolle | rolle | «omsorgskommune» | rolle_innehas_av kommune (`eb25cd79`) | forskrift/2017/06/22/1050/nor/§3/ledd-1 (fortolkning) | hoy | innvending lav → aksepter | Johanns avgjørelse: rolle en kommune har overfor noen. PILOT. |
| n37 | personalet | 2 | ikke aktør | ikke_aktør |  | personkrets_i «barnehage» (ny) | ikke relevant | hoy | holder | De ansatte i barnehagen (rammeplanen). Personkrets. |
| n38 | private familiebarnehager | 2 | opprett klasse | klasse | «familiebarnehage» | underklasse_av «barnehage» (ny) | forskrift/2005/12/16/1555/nor/§1/ledd-1 (tekstfunn) | middels | innvending middels → aksepter | «private familiebarnehager» = klassen familiebarnehage avgrenset til private. Ny node bare for familiebarnehage; «private» er avgrensning. |
| n39 | private ordinære barnehager | 2 | tagg som omtale | omtale |  | omtale_av «privat barnehage» (ny) | ikke relevant | hoy | holder | Privat barnehage avgrenset til den ordinære barnehageformen. Ingen egen node. |
| n40 | regjeringen | 2 | vent (modellvalg) | venter |  |  | ikke relevant | - | kontroller → delvis → godtar | venter på modellvalg (Norge/nivåene). Om regjeringen er statsrådet (eget organ) eller navneform for Kongen i statsråd, avgjøres i notatet. |
| n41 | samarbeidsutvalget | 2 | opprett klasse | klasse | «samarbeidsutvalg» | organ_i «barnehage» (ny) | lov/2005/06/17/64/nor/§4/ledd-1 (normalisering) | hoy | holder | Ett per barnehage: klasse av organer, som foreldrerådet. |
| n42 | samiske barnehager | 2 | opprett klasse | klasse | «samisk barnehage» | underklasse_av «barnehage» (ny) | kap-3/rom-samiske-barnehager/ledd-1 (fortolkning) | middels | innvending lav → aksepter | Barnehager med samisk formål. Underklasse, ikke aktør. |
| n43 | staten | 2 | vent (modellvalg) | venter |  |  | ikke relevant | - | ikke_vurdert | venter på modellvalg (Norge/nivåene). |
| n44 | styret | 2 | tagg som omtale | omtale |  | omtale_av «barnehageeier» (ny); omtale_av «Styret for domstoladministrasjonen» (ny) | ikke relevant | hoy | innvending lav → aksepter | To ulike referenter i samme navn: styret i rettssubjektet som driver en privat barnehage (organ i hvert rettssubjekt), og styret for Domstoladministrasjonen. Skill ut før behandling. |
| n45 | Sverige | 2 | opprett område | område | «Sverige» |  | ikke relevant | - | innvending lav → aksepter | venter på modellvalg (Norge/nivåene). Samme valg som for Finland og Norge. |
| n46 | søker | 2 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Søkeren er en privat part (FORMAT.md: private parter annoteres ikke). |
| n47 | Virksomheter | 2 | ikke aktør | ikke_aktør |  | omtale_av «virksomheter som er omfattet av helse- og omsorgstjenesteloven» (ny) | ikke relevant | hoy | innvending middels → aksepter | Ubestemt krets avgrenset med vilkår (bhl. § 6 første ledd, hol. § 12-3 a). Vilkåret hører til regellaget. To ulike kretser i samme navn. |
| n48 | Administrasjonspåslaget | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Et tilskuddselement (beløp), ikke en aktør. Står bare i en merknad. |
| n49 | Alle barnehager | 1 | tagg som omtale | omtale |  | omtale_av «barnehage» (ny) | ikke relevant | hoy | holder | «alle X»: omtale av klassen barnehage. |
| n50 | Alle godkjente barnehager i kommunen | 1 | tagg som omtale | omtale |  | omtale_av «barnehage» (ny) | ikke relevant | hoy | holder | Klassen barnehage avgrenset til de godkjente i kommunen. Avgrensningen hører til regellaget. |
| n51 | andre tilskudd enn rammetilskuddet | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Tilskudd (penger), ikke en aktør. |
| n52 P | andre tjenesteytere | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Ubestemt krets («andre tjenesteytere» der samarbeid er nødvendig). Ingen medlemsregel. Ikke en aktør. PILOT. |
| n53 | ankeutvalget | 1 | tagg som omtale | omtale |  | omtale_av «Høyesteretts ankeutvalg» (ny) | ikke relevant | middels | holder | Kortform for Høyesteretts ankeutvalg (dl. § 6). Godta genitivforslaget. |
| n54 | annet folkevalgt organ | 1 | tagg som omtale | omtale |  | omtale_av «folkevalgt organ» (ny) | ikke relevant | middels | holder | Omtale av klassen folkevalgt organ (kommuneloven § 5-1), ikke en ny aktør. |
| n55 | Arbeids- og velferdsetatens register | 1 | ikke aktør | ikke_aktør |  | fores_for ARBEIDS- OG VELFERDSETATEN (`b3bdec90`) | ikke relevant | hoy | holder | «X-ens Y» der Y er et register: gjenstand, ikke organ. Kommunen fører registeret (bhl. § 10 fjerde ledd). |
| n56 | Arbeidsgiver | 1 | ikke aktør | ikke_aktør |  | omtale_av «barnehageeier» (ny) | ikke relevant | middels | innvending lav → aksepter | En rolle (den som ansetter), ikke en aktør. |
| n57 | avdelingsledere | 1 | opprett rolle | rolle | «avdelingsleder (domstol)» | rolle_for «tingrett» (ny) | lov/1915/08/13/5/nor/§19/ledd-4 (fortolkning) | hoy | innvending lav → aksepter | Stilling i domstolen (dommer som leder en avdeling). |
| n58 | Avtalepartene | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`); omtale_av «regionalt helseforetak» (ny); omtale_av «helseforetak» (ny) | ikke relevant | hoy | innvending lav → aksepter | Henviser til partene i avtalen etter hol. § 6-1 (kommunen og RHF/HF). |
| n59 | bank | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Barnehagens bank: privat part som skal gi opplysninger. |
| n60 | barn, elever og foreldre | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Tre private persongrupper i en oppramsing. |
| n61 | barne-, ungdoms- og familieetaten | 1 | opprett aktør | aktør | «Barne-, ungdoms- og familieetaten» (organ) | ledes_av BARNE-, UNGDOMS- OG FAMILIEDIREKTORATET (`aaf4157f`) | lov/2021/06/18/97/nor/§16-1/ledd-2 (fortolkning) | middels | holder | Etaten er et eget statlig organ; direktoratet (finnes i basen) er sentralt nivå. Om etaten skal være egen aktør eller navneform for direktoratet, må kontrolleres. |
| n62 | barnehagen eller kommunen | 1 | tagg som omtale | omtale |  | omtale_av «barnehage» (ny); omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | To omtaler i én frase. Del i «barnehagen» og «kommunen». |
| n63 | barnehagens foreldreråd | 1 | tagg som omtale | omtale |  | omtale_av «foreldreråd» (ny) | ikke relevant | hoy | holder | «X-ens Y»: organet foreldreråd i barnehagen. Godta genitivforslaget. |
| n64 | barnehagens personale | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Personkrets (barnehagens ansatte). |
| n65 | barnehagens regnskapsfører | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Privat oppdragstaker for barnehagen; skal gi opplysninger. |
| n66 | Barnehagens vedtekter | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Et dokument. «X-ens Y» der Y er en gjenstand. |
| n67 | Barnehagepersonalet | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Personkrets (de ansatte). |
| n68 | barnets bostedskommune | 1 | tagg som omtale | omtale |  | omtale_av «bostedskommune» (ny) | ikke relevant | hoy | holder | Johanns eksempel: rolle, ikke aktør. Godta genitivforslaget. |
| n69 | barnevernet | 1 | tagg som omtale | omtale |  | omtale_av «barnevernstjeneste» (ny) | ikke relevant | middels | holder | Dagligtale for barnevernstjenesten (rammeplanen). |
| n70 | bestemmende organ | 1 | tagg som omtale | omtale |  | omtale_av «barnehageeier» (ny) | ikke relevant | middels | innvending lav → aksepter | Organet som har ansvaret når rettssubjektet ikke har styre. Bestemmes av organisasjonsformen, ikke en egen aktør. Hører sammen med «det bestemmende organ». |
| n71 | de nye kommunene | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | middels | innvending lav → aksepter | Samme som «den nye kommunen», flertall. |
| n72 | de private barnehagene | 1 | tagg som omtale | omtale |  | omtale_av «privat barnehage» (ny) | ikke relevant | hoy | holder | «de X»: omtale av klassen. |
| n73 | De regionale helseforetakene | 1 | tagg som omtale | omtale |  | omtale_av «regionalt helseforetak» (ny) | ikke relevant | hoy | holder | «de X»: alle fire RHF. |
| n74 | den fysiske personen | 1 | tagg som omtale | omtale |  | omtale_av «barnehageeier» (ny) | ikke relevant | middels | holder | Den fysiske personen som eier barnehagen: innehaver av rollen barnehageeier. |
| n75 | Den pedagogisk-psykologiske tjenesten | 1 | tagg som omtale | omtale |  | omtale_av «pedagogisk-psykologisk tjeneste» (ny) | ikke relevant | hoy | holder | «den X»: omtale av klassen. |
| n76 P | Den pedagogiske lederen | 1 | tagg som omtale | omtale |  | omtale_av «pedagogisk leder» (ny) | ikke relevant | hoy | holder | «den X»: omtale av rollen pedagogisk leder. PILOT. |
| n77 | den private ordinære barnehagen | 1 | tagg som omtale | omtale |  | omtale_av «privat barnehage» (ny) | ikke relevant | hoy | holder | «den X»: omtale av klassen. |
| n78 | den som er ansvarlig for barnehagen | 1 | tagg som omtale | omtale |  | omtale_av «barnehageeier» (ny) | ikke relevant | hoy | holder | «den som er ansvarlig for barnehagen etter § 7» = barnehageeieren. |
| n79 | den som er ansvarlig for barnehagen etter barnehageloven § 7 | 1 | tagg som omtale | omtale |  | omtale_av «barnehageeier» (ny) | ikke relevant | hoy | holder | Som over. Slå sammen med de to andre variantene. |
| n80 | den som er ansvarlig for barnehagen etter § 7 | 1 | tagg som omtale | omtale |  | omtale_av «barnehageeier» (ny) | ikke relevant | hoy | holder | Som over. |
| n81 | det bestemmende organ | 1 | ta med i n70 | omtale |  | omtale_av «barnehageeier» (ny) | ikke relevant | middels | innvending lav → aksepter | Samme som «bestemmende organ». |
| n82 | det offentlige | 1 | vent (modellvalg) | venter |  |  | ikke relevant | - | holder | venter på modellvalg (Norge/nivåene). «det offentlige» betaler (dl. § 105 a); hvilket nivå, avhenger av notatet. |
| n83 | det samiske samfunnet | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Et samfunn (befolkningsgruppe), ikke en aktør (rammeplanen). |
| n84 P | Direktoratet | 1 | koble til kjent aktør | omtale |  | samme_som DIREKTORATET FOR HØYERE UTDANNING OG KOMPETANSE (`daf7c915`) | ikke relevant | hoy | holder | Feiltreff: sveipet fanget bare første ord i et navn som finnes i basen. Navnet står fullt i samme setning og er alt navneform. PILOT. |
| n85 | direktøren | 1 | opprett rolle | rolle | «direktør for Domstoladministrasjonen» | rolle_for DOMSTOLADMINISTRASJONEN (`34e89712`) | mangler | hoy | holder | En stilling, ikke en aktør. Forslaget fra regelen gjelder en annen direktør. |
| n86 | Dommere | 1 | opprett rolle | rolle | «dommer» | rolle_for NORGES HØYESTERETT (`da59aa06`); rolle_for «tingrett» (ny); rolle_for «lagmannsrett» (ny); rolle_for «jordskifterett» (ny); rolle_for «forliksråd» (ny) | lov/1915/08/13/5/nor/§52/ledd-1 (tekstfunn) | hoy | innvending middels → aksepter | Embete (person), ikke en aktør. |
| n87 | Domstolens leder | 1 | tagg som omtale | omtale |  | omtale_av «domstolleder» (ny) | ikke relevant | hoy | holder | «X-ens Y»: rollen leder av domstolen. |
| n88 | domstollederen | 1 | opprett rolle | rolle | «domstolleder» | rolle_for NORGES HØYESTERETT (`da59aa06`); rolle_for «lagmannsrett» (ny); rolle_for «tingrett» (ny) | mangler | hoy | innvending lav → aksepter | Stilling. |
| n89 | Eier av familiebarnehage | 1 | tagg som omtale | omtale |  | omtale_av «barnehageeier» (ny) | ikke relevant | hoy | holder | «Eier av X» = rollen barnehageeier for en familiebarnehage. |
| n90 | Eier av private barnehager | 1 | tagg som omtale | omtale |  | omtale_av «barnehageeier» (ny) | ikke relevant | hoy | holder | Som over. |
| n91 | eieren | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | innvending middels → aksepter | «eieren» i bhl. § 56 = barnehagens eier. |
| n92 | eierens nærstående | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Personkrets knyttet til eieren (nærstående). Private parter. |
| n93 | En kommune | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | Johanns avgjørelse: «En kommune» er omtale av klassen, merket generisk. |
| n94 | En kommune eller fylkeskommune | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | Sammensatt: kommune-delen er omtale av klassen; fylkeskommune-delen venter på notatet. |
| n95 | Enhetsregisteret | 1 | ikke aktør | ikke_aktør |  | fores_av REGISTERENHETEN I BRØNNØYSUND (`e8d3c02c`) | lov/2025/06/20/105/nor/§1-2/ledd-1 (tekstfunn) | hoy | holder | Et register, ikke en aktør. |
| n96 | Enhver som utfører tjeneste eller arbeid etter denne loven | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Ubestemt personkrets avgrenset med vilkår (opplysningsplikt). Regellaget. |
| n97 | et statlig forvaltningsorgan | 1 | tagg som omtale | omtale |  | omtale_av «forvaltningsorgan» (ny) | ikke relevant | middels | innvending lav → aksepter | Omtale av klassen forvaltningsorgan (docs/33 §4.1 bruker den som klasseeksempel). «statlig» er avgrensning. |
| n98 | familievernkontor | 1 | opprett klasse | klasse | «familievernkontor» |  | lov/1997/06/19/62/nor/§2/ledd-1 (fortolkning) | middels | innvending lav → aksepter | Ubestemt form om et hvilket som helst familievernkontor. Klasse, ikke aktør. |
| n99 | Felles | 1 | tagg som omtale | omtale |  | omtale_av «Felles klagenemnd» (ny) | ikke relevant | hoy | holder | Feiltreff: første ord i «Felles klagenemnd». |
| n100 | Felles klagenemnd | 1 | opprett aktør | aktør | «Felles klagenemnd» (organ) | opprettet_av Kunnskapsdepartementet (`61b9a4f8`) | lov/2024/03/08/9/nor/§14-2/ledd-1 (fortolkning) | middels | innvending hoy → aksepter | Navngitt organ ved NTNU. Navnet er ikke unikt i landet, så det trenger referanse til institusjonen. |
| n101 | felles samarbeidsutvalg | 1 | opprett klasse | klasse | «felles samarbeidsutvalg (barnehage og grunnskole)» | underklasse_av «samarbeidsutvalg» (ny); organ_for «barnehage» (ny); kan_bestemmes_av kommune (`eb25cd79`) | lov/2005/06/17/64/nor/§5/ledd-1 (tekstfunn) | middels | innvending middels → aksepter | Et organ kommunen kan opprette, ett per barnehage/skole-par. Klasse av organer (forleddssperren: et annet organ enn samarbeidsutvalget). |
| n102 | Folketrygden | 1 | opprett aktør | aktør | «Folketrygden» (ordning) | forvaltes_av HELSEDIREKTORATET (`bbabbed0`); forvaltes_av ARBEIDS- OG VELFERDSETATEN (`b3bdec90`) | lov/1997/02/28/19/nor/§1-1/ledd-1 (fortolkning) | middels | innvending lav → aksepter | Ikke-aktør med funksjon i loven: nodetypen ordning (aktørtype ordning, ordningstype trygdeordning, docs/33 §4.5). «Folketrygden yter stønad». |
| n103 | Forliksrådet | 1 | opprett klasse | klasse | «forliksråd» | har_ansvarsomrade kommune (`eb25cd79`); sekretariat namsfogden | lov/1915/08/13/5/nor/§27/ledd-1 (normalisering) | hoy | innvending hoy → aksepter | Ett forliksråd per kommune: klasse (juristrunde-440), ikke én aktør. |
| n104 | forskrift 16. desember 2005 nr. 1508 om midlertidig dispensasjon og unntak fra utdanningskravet for styrer og pedagogisk leder gitt med hjemmel i lov 17. juni 2005 nr. 64 om barnehager (barnehageloven) § 17 fjerde ledd og § 18 sjette ledd | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | En rettskilde (forskriftstittel). |
| n105 | Forskrift om rammeplan for barnehagens innhold og oppgaver (rammeplanen) | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | En rettskilde (forskriftstittel). |
| n106 | fosterforeldrene eller institusjonen der barnet bor | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Sammensatt: private personer og en institusjon der barnet bor. Regellaget. |
| n107 | fremmed stat | 1 | vent (modellvalg) | venter |  |  | ikke relevant | - | holder | venter på modellvalg (Norge/nivåene). Stat som avtalepart, samme valg som for Norge. |
| n108 | fylkesnemnda | 1 | koble til kjent aktør | omtale |  | utgatt_navn BARNEVERNS- OG HELSENEMNDA (`d1aabe5f`) | lov/2021/06/18/97/nor/§14-1/ledd-1 (fortolkning) | middels | innvending middels → aksepter | Flere regionale nemnder: klasse. «fylkesnemnda» er utgått navn i hol. § 10-6. |
| n109 P | Før kommunen | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | Sveipet tok med første ord i setningen. Det er «kommunen» (generisk, standardregelen gjelder). PILOT. |
| n110 | Førstelagmannen | 1 | opprett rolle | rolle | «førstelagmann» | rolle_for «lagmannsrett» (ny) | lov/1915/08/13/5/nor/§10/ledd-1 (normalisering) | hoy | innvending lav → aksepter | Stilling (leder av lagmannsretten), ikke en aktør. Hjemmelen er riktig, men den gjelder en rolle. |
| n111 | Godkjente barnehager som mottar tilskudd | 1 | tagg som omtale | omtale |  | omtale_av «barnehage» (ny) | ikke relevant | hoy | holder | «X som …»: klassen barnehage med vilkår. |
| n112 | Hedmark | 1 | opprett område | område | «Hedmark (fylke, utgått)» (fylke (utgått)) | inngar_i Innlandet (`6ab58594`) | mangler | middels | innvending lav → aksepter | Et tidligere fylke som område (reindriftsloven § 4). Ikke fylkeskommunen. Registeret har ikke utgåtte fylker. |
| n113 | helseforetak | 1 | opprett klasse | klasse | «helseforetak» | eies_av «regionalt helseforetak» (ny); eies_av «helseforetak» (ny) | lov/2001/06/15/93/nor/§2/ledd-3 (tekstfunn) | hoy | innvending lav → aksepter | Ubestemt form. Klasse, ikke aktør. |
| n114 | Hver private barnehage | 1 | tagg som omtale | omtale |  | omtale_av «privat barnehage» (ny) | ikke relevant | hoy | holder | «hver X»: omtale av klassen. |
| n115 | Høyesterett | 1 | koble til kjent aktør | omtale |  | samme_som NORGES HØYESTERETT (`da59aa06`) | lov/1814/05/17/nor/§88/ledd-1 (tekstfunn) | hoy | innvending lav → aksepter | Kjent aktør med annen skrivemåte. Koble som navneform (gjeldende), ikke ny aktør. |
| n116 | Høyesteretts ankeutvalg | 1 | opprett aktør | aktør | «Høyesteretts ankeutvalg» (organ) | organ_i NORGES HØYESTERETT (`da59aa06`) | lov/1915/08/13/5/nor/§5/ledd-1 (fortolkning) | middels | innvending middels → aksepter | En avdeling av Høyesterett med egen kompetanse. Om det er en organisatorisk enhet eller bare en sammensetning av retten, må vurderes. |
| n117 | ideelle organisasjoner | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Private parter (mulige drivere av brukerrom). |
| n118 | Innstillingsrådet | 1 | opprett aktør | aktør | «Innstillingsrådet for dommere» (organ) |  | lov/1915/08/13/5/nor/§55a/ledd-1 (fortolkning) | hoy | holder | Navngitt organ. «Innstillingsrådet» er kortform; registrer det fulle navnet som gjeldende. |
| n119 | klageinstansen | 1 | tagg som omtale | omtale |  | omtale_av «klageinstans (kommunal)» (ny) | ikke relevant | hoy | holder | Henviser til «Kommunens klageorgan» i første ledd. |
| n120 | kommunale barnehager | 1 | opprett klasse | klasse | «kommunal barnehage» | underklasse_av «barnehage» (ny); ansvarlig kommune (`eb25cd79`) | lov/2005/06/17/64/nor/§7/ledd-1 (fortolkning) | middels | innvending middels → aksepter | Underklasse av barnehage. |
| n121 | Kommunale og private barnehager | 1 | tagg som omtale | omtale |  | omtale_av «kommunal barnehage» (ny); omtale_av «privat barnehage» (ny) | ikke relevant | hoy | holder | Sammensatt: to klasser. |
| n122 | Kommunelegen | 1 | opprett rolle | rolle | «kommunelege» | rolle_for kommune (`eb25cd79`) | lov/2011/06/24/30/nor/§5-5/ledd-1 (normalisering) | hoy | holder | docs/33 §4.1 bruker kommunelege som eksempel på rolle. |
| n123 | kommunen der barnet faktisk går i barnehage | 1 | tagg som omtale | omtale |  | omtale_av «vertskommune» (ny) | ikke relevant | middels | innvending lav → aksepter | «X der …» = rolle. Står bare i en merknad, så den gir ingen hjemmel. |
| n124 | Kommunen eller fylkeskommunen | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | Sammensatt; fylkeskommune-delen venter. |
| n125 | kommunen som barnehagemyndighet | 1 | tagg som omtale | omtale |  | omtale_av lokal barnehagemyndighet (`0ab29a09`) | ikke relevant | hoy | holder | «kommunen som barnehagemyndighet» = rollen lokal barnehagemyndighet, som finnes. |
| n126 | kommunens eget klageorgan | 1 | tagg som omtale | omtale |  | omtale_av «klageinstans (kommunal)» (ny) | ikke relevant | hoy | holder | Samme som «Kommunens klageorgan». Står bare i en merknad. |
| n127 P | Kommunens klageorgan | 1 | opprett rolle | rolle | «klageinstans (kommunal)» | rolle_for kommune (`eb25cd79`) | lov/1967/02/10/nor/§28/ledd-2 (fortolkning) | hoy | holder | Ikke et eget organ: rollen som klageinstans, og den innehas av det kommunale organet kommunestyret peker ut. PILOT. |
| n128 | Kommunens pedagogisk-psykologiske tjeneste | 1 | tagg som omtale | omtale |  | omtale_av «pedagogisk-psykologisk tjeneste» (ny) | ikke relevant | hoy | holder | «X-ens Y»: organ i kommunen. Godta genitivforslaget. |
| n129 | Kommuner i tiltakssonen i Finnmark og Nord-Troms | 1 | opprett klasse | klasse | «kommuner i tiltakssonen i Finnmark og Nord-Troms» | underklasse_av kommune (`eb25cd79`); medlemsregel «tiltakssonen i Finnmark og Nord-Troms» (ny) | forskrift/2015/10/09/1166/nor/§3/ledd-7 (tekstfunn) | hoy | innvending middels → aksepter | «Kommuner som/i …»: klassen kommune med et vilkår. Vilkåret hører til regellaget; ikke en ny node. |
| n130 | Kommuner i tiltakssonen i Finnmark og Nord-Troms som ikke har kommunale ordinære barnehager | 1 | tagg som omtale | omtale |  | omtale_av «kommuner i tiltakssonen i Finnmark og Nord-Troms» (ny) | ikke relevant | hoy | innvending middels → aksepter | «Kommuner som/i …»: klassen kommune med et vilkår. Vilkåret hører til regellaget; ikke en ny node. |
| n131 | Kommuner som fastsetter egne satser | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | «Kommuner som/i …»: klassen kommune med et vilkår. Vilkåret hører til regellaget; ikke en ny node. |
| n132 | Kommuner som har private barnehager med barn som er folkeregistrert i en annen kommune | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | «Kommuner som/i …»: klassen kommune med et vilkår. Vilkåret hører til regellaget; ikke en ny node. |
| n133 | Kommuner som ikke har kommunale familiebarnehager | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | «Kommuner som/i …»: klassen kommune med et vilkår. Vilkåret hører til regellaget; ikke en ny node. |
| n134 | Kommuner som ikke har kommunale ordinære barnehager | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | «Kommuner som/i …»: klassen kommune med et vilkår. Vilkåret hører til regellaget; ikke en ny node. |
| n135 | Kommuner som ikke har kommunale åpne barnehager | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | «Kommuner som/i …»: klassen kommune med et vilkår. Vilkåret hører til regellaget; ikke en ny node. |
| n136 | Kommuner som ligger i sentralitetssone 5 eller 6 | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | kontroller → avvis → godtar | «Kommuner som/i …»: klassen kommune med et vilkår. Vilkåret hører til regellaget; ikke en ny node. |
| n137 | Kommuner som ligger i sentralitetssone 5 eller sentralitetssone 6 som ikke har kommunale ordinære barnehager | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | kontroller → avvis → godtar | «Kommuner som/i …»: klassen kommune med et vilkår. Vilkåret hører til regellaget; ikke en ny node. |
| n138 | kommuner som slår seg sammen | 1 | tagg som omtale | omtale |  | omtale_av kommune (`eb25cd79`) | ikke relevant | hoy | holder | Som over; står bare i en merknad. |
| n139 | kommunestyret | 1 | opprett klasse | klasse | «kommunestyre» | organ_i kommune (`eb25cd79`) | lov/2018/06/22/83/nor/§5-3/ledd-1 (tekstfunn) | hoy | holder | Hver kommune har ett: klasse av organer. Det enkelte kommunestyret (i Oslo) er en aktør, men det generiske ordet er ikke det. |
| n140 | Kommunestyret selv | 1 | tagg som omtale | omtale |  | omtale_av «kommunestyre» (ny) | ikke relevant | hoy | holder | Omtale av kommunestyret; «selv» er en delegeringssperre (avgrensning på kanten). |
| n141 | kompetent myndighet | 1 | opprett rolle | rolle | «kompetent myndighet (annen stat)» |  | forskrift/2008/09/12/1071/nor/§8/ledd-1 (fortolkning) | middels | innvending lav → aksepter | Myndigheten i en annen stat som har utstedt kvalifikasjonen. Rolle i saken, ikke en aktør i korpuset. |
| n142 | koordinatoren | 1 | tagg som omtale | omtale |  | omtale_av «barnekoordinator» (ny) | ikke relevant | hoy | innvending lav → aksepter | Henviser til barnekoordinatoren i samme ledd: en rolle kommunen fyller. |
| n143 | lagmennene | 1 | opprett rolle | rolle | «lagmann» | rolle_for «lagmannsrett» (ny) | lov/1915/08/13/5/nor/§10/ledd-1 (normalisering) | hoy | innvending middels → aksepter | Embete (person). |
| n144 | leder | 1 | opprett rolle | rolle | «leder (av nemnd)» | rolle_for «NOKUTs klagenemnd» (ny) | forskrift/2025/07/02/1462/nor/§3/ledd-2 (tekstfunn) | hoy | innvending middels → aksepter | Verv i nemnda. Godta ikke genitivforslagene som slår sammen «domstolens leder» og «nemndas leder». |
| n145 | lokal redningssentral | 1 | opprett klasse | klasse | «lokal redningssentral» | har_ansvarsomrade hvert politidistrikt og Svalbard; underlagt Hovedredningssentralen | kap-1/rom-1-3/ledd-3 (tekstfunn) | hoy | innvending middels → aksepter | Flere lokale redningssentraler: klasse, ikke én aktør. |
| n146 | Lånekassens klagenemnd | 1 | opprett aktør | aktør | «Lånekassens klagenemnd» (organ) | klageinstans_for STATENS LÅNEKASSE FOR UTDANNING (`c169972e`) | lov/2005/06/03/37/nor/§18/ledd-1 (fortolkning) | hoy | holder | Navngitt organ. «X-ens Y» betyr her ikke organ i Lånekassen: nemnda er klageinstans for Lånekassen. |
| n147 | Meldal | 1 | opprett område | område | «Meldal (kommune, utgått)» (kommune (utgått)) | inngar_i Orkland (`b026a774`) | mangler | middels | innvending lav → aksepter | Et kommuneområde i en lovtittel. Registeret har bare dagens kommuner. |
| n148 P | myndighetsnivåene | 1 | vent (modellvalg) | venter |  |  | ikke relevant | - | holder | venter på modellvalg (Norge/nivåene). «myndighetsnivåene» er forvaltningsnivåene i seg selv (rammeplanen, ikke hjemmel). |
| n149 P | nemndas leder | 1 | tagg som omtale | omtale |  | omtale_av «leder (av nemnd)» (ny) | ikke relevant | hoy | holder | «X-ens Y»: rollen leder i nemnda. PILOT. |
| n150 | nestleder | 1 | opprett rolle | rolle | «nestleder (av nemnd)» | rolle_for «NOKUTs klagenemnd» (ny) | forskrift/2025/07/02/1462/nor/§3/ledd-2 (tekstfunn) | hoy | innvending middels → aksepter | Verv i nemnda. |
| n151 | NOKUT | 1 | opprett aktør | aktør | «NOKUT» (organ) |  | lov/2024/03/08/9/nor/§16-1/ledd-1 (fortolkning) | hoy | innvending lav → aksepter | Navngitt statlig organ, ikke i basen. Gjeldende navneform bør være det fulle navnet med forkortelsen, slik loven skriver det. |
| n152 | Norges teknisk–naturvitenskapelig universitet (NTNU) | 1 | koble til kjent aktør | omtale |  | samme_som NORGES TEKNISK-NATURVITENSKAPELIGE UNIVERSITET NTNU (`72dc2d24`) | ikke relevant | hoy | holder | Kjent aktør. Koble som navneform (skrivemåten i forskriften), ikke ny aktør. |
| n153 | Norgespris | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Del av en lovtittel («lov om Norgespris og strømstønad»). |
| n154 | offentlige myndigheter | 1 | tagg som omtale | omtale |  | omtale_av «forvaltningsorgan» (ny) | ikke relevant | middels | innvending middels → aksepter | Ubestemt krets avgrenset med formål («når det er nødvendig for å utføre oppgaver etter barnehageloven»). |
| n155 | Pasient- og brukerorganisasjoner | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Private organisasjoner. |
| n156 | Pedagogisk leder | 1 | opprett rolle | rolle | «pedagogisk leder» | rolle_for «barnehage» (ny) | forskrift/2017/06/22/1049/nor/§1/ledd-2 (tekstfunn) | hoy | innvending lav → aksepter | Stilling i barnehagen. |
| n157 P | pedagogisk-psykologisk tjeneste | 1 | opprett klasse | klasse | «pedagogisk-psykologisk tjeneste» | organ_i kommune (`eb25cd79`) | lov/2023/06/09/30/nor/§11-13/ledd-1 (normalisering) | hoy | holder | Kommunal tjeneste i hver kommune: klasse, ikke én aktør. PILOT. |
| n158 | pedagogisk-psykologiske tjenesten | 1 | tagg som omtale | omtale |  | omtale_av «pedagogisk-psykologisk tjeneste» (ny) | ikke relevant | hoy | holder | Bøyningsform; slå sammen med «pedagogisk-psykologisk tjeneste». |
| n159 P | pedagogiske ledere | 1 | tagg som omtale | omtale |  | omtale_av «pedagogisk leder» (ny) | ikke relevant | hoy | holder | Flertall av rollen. PILOT. |
| n160 | private barnehageeiere | 1 | tagg som omtale | omtale |  | omtale_av «barnehageeier» (ny) | ikke relevant | hoy | holder | Rollen barnehageeier for private barnehager. |
| n161 | private barnehagen | 1 | tagg som omtale | omtale |  | omtale_av «privat barnehage» (ny) | ikke relevant | hoy | holder | Omtale av klassen. |
| n162 | Private barnehager opplistet i vedlegg I | 1 | tagg som omtale | omtale |  | omtale_av «privat barnehage» (ny) | ikke relevant | hoy | holder | Ekstensjonal avgrensning (vedlegget) av klassen. Regellaget, ikke ny node. |
| n163 | Private barnehager som samlet over de tre siste årene har mer enn fem prosent høyere utgifter til pensjon | 1 | tagg som omtale | omtale |  | omtale_av «privat barnehage» (ny) | ikke relevant | hoy | holder | «X som …»: klassen med vilkår. |
| n164 | Private barnehager som samlet over de tre siste årene har mer enn fem prosent høyere utgifter til pensjon enn det de får dekket gjennom pensjonspåslaget etter § 4 | 1 | tagg som omtale | omtale |  | omtale_av «privat barnehage» (ny) | ikke relevant | hoy | holder | Som over (lengre variant av samme vilkår i den eldre forskriften). |
| n165 | private grunnskoler godkjent etter opplæringslova 2023 § 22-1 | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Kategori private skoler i en unntaksliste (økonomiforskriften § 3 a). Avgrensning i regellaget. |
| n166 | regionale helseforetak | 1 | tagg som omtale | omtale |  | omtale_av «regionalt helseforetak» (ny) | ikke relevant | hoy | holder | Flertall av klassen. |
| n167 | retten | 1 | tagg som omtale | omtale |  |  | ikke relevant | middels | innvending lav → aksepter | «retten» = den domstolen som har saken. Generisk omtale, ingen ny aktør. |
| n168 | Rettsvitner | 1 | opprett rolle | rolle | «rettsvitne» | rolle_for retten / den som styrer forretningen | lov/1915/08/13/5/nor/§101/ledd-1 (fortolkning) | hoy | innvending lav → aksepter | Personer oppnevnt til et verv. |
| n169 | revisor | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Barnehagens revisor: privat oppdragstaker. |
| n170 | rikets hovedstad | 1 | tagg som omtale | omtale |  | samme_som Oslo (`c119f08f`) | mangler | lav | innvending lav → aksepter | En beskrivelse av et sted (setet for Høyesterett), ikke en aktør. Hvilken node det er, avhenger av hvordan Oslo som hovedstad skal modelleres. |
| n171 | Sametinget | 1 | koble til kjent aktør | omtale |  | samme_som SAMEDIGGI / SAMETINGET (`e0a41e43`) | lov/1987/06/12/56/nor/§1-2/ledd-1 (normalisering) | hoy | holder | Kjent aktør (rutingen kjenner ikke registernavnet med skråstrek, juristrunde-440). Koble som gjeldende navneform. |
| n172 | sentralt register | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Et register. |
| n173 | skattemyndighetene | 1 | tagg som omtale | omtale |  | omtale_av SKATTEETATEN (`5e69bca7`) | ikke relevant | middels | holder | Står bare i en merknad. Skattemyndighetene er Skatteetaten (finnes). |
| n174 | sokn i Den norske kirke | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | middels | holder | Kategori barnehageeiere i en unntaksliste (økonomiforskriften § 3 a). Avgrensning, regellaget. |
| n175 | Statens undersøkelseskommisjon for helse- og omsorgstjenesten | 1 | opprett aktør | aktør | «Statens undersøkelseskommisjon for helse- og omsorgstjenesten» (organ) | administrativt_underlagt departementet | lov/2017/06/16/56/nor/§4/ledd-1 (tekstfunn) | hoy | holder | Navngitt organ med egen lov. Ikke i basen. |
| n176 | statlig forvaltningsorgan | 1 | ta med i n97 | omtale |  | omtale_av «forvaltningsorgan» (ny) | ikke relevant | middels | innvending lav → aksepter | Som «et statlig forvaltningsorgan» (samme forekomst, delt treff). |
| n177 | statlige og fylkeskommunale barnehager | 1 | tagg som omtale | omtale |  | omtale_av «barnehage» (ny) | ikke relevant | middels | holder | Sammensatt; står bare i en merknad. |
| n178 | statsautorisert revisor | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Kvalifikasjonskrav til revisor (privat). |
| n179 | stiftelse | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Organisasjonsform i en unntaksliste. |
| n180 | student- og elevrepresentantene | 1 | opprett rolle | rolle | «student- og elevrepresentant» | rolle_for «Lånekassens klagenemnd» (ny) | forskrift/2025/07/02/1462/nor/§11/ledd-2/punkt-2 (fortolkning) | hoy | innvending lav → aksepter | Verv i nemnda. |
| n181 | studentrepresentantene | 1 | opprett rolle | rolle | «studentrepresentant» | rolle_for «NOKUTs klagenemnd» (ny) | forskrift/2025/07/02/1462/nor/§3/ledd-2/punkt-4 (fortolkning) | hoy | innvending lav → aksepter | Verv i nemnda. |
| n182 | studentsamskipnadene | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | middels | holder | Kategori barnehageeiere (studentsamskipnader har egen lov). Avgrensning i regellaget. |
| n183 | styreren i barnehagen | 1 | tagg som omtale | omtale |  | omtale_av «styrer» (ny) | ikke relevant | hoy | holder | «X i Y»: rollen styrer i barnehagen. |
| n184 | Styret for domstoladministrasjonen | 1 | opprett aktør | aktør | «Styret for domstoladministrasjonen» (organ) | organ_i DOMSTOLADMINISTRASJONEN (`34e89712`) | lov/1915/08/13/5/nor/§33/ledd-1 (normalisering) | hoy | holder | Navngitt organ i en kjent aktør. |
| n185 | Sysselmesteren | 1 | tagg som omtale | omtale |  | omtale_av «Sysselmesteren på Svalbard» (ny) | ikke relevant | hoy | holder | Kortform. Slå sammen med «Sysselmesteren på Svalbard». |
| n186 | Sysselmesteren på Svalbard | 1 | opprett aktør | aktør | «Sysselmesteren på Svalbard» (organ) | har_ansvarsomrade Svalbard (`3c1da8b6`) | lov/1925/07/17/11/nor/§5/ledd-1 (normalisering) | hoy | holder | Navngitt embete. Ikke i basen. Ikke medlem av klassen statsforvalter (har samme myndighet, men er ikke en statsforvalter). |
| n187 | særskilt organ | 1 | opprett klasse | klasse | «særskilt organ (granskingsorgan etter dl. § 43 andre ledd)» |  | lov/1915/08/13/5/nor/§43/ledd-2 (fortolkning) | middels | innvending middels → aksepter | Ubestemt krets («et annet særskilt organ som er oppnevnt …»). Vilkår, regellaget. |
| n188 | tiltakssonen i Finnmark og Nord-Troms | 1 | opprett område | område | «tiltakssonen i Finnmark og Nord-Troms» | bestar_av Finnmark (`a4f24173`); bestar_av Kåfjord (`23b16bcb`); bestar_av Skjervøy (`2c693abe`); bestar_av Nordreisa (`f636b659`); bestar_av Kvænangen (`5ab3fa7b`); bestar_av Karlsøy (`10f3e35b`); b… | forskrift/2015/10/09/1166/nor/§3/ledd-7 (tekstfunn) | middels | innvending hoy → aksepter | Et område forskriften definerer. Navnet står bare i en merknad her, men definisjonen står i § 3 sjuende ledd. |
| n189 | tingretten | 1 | opprett klasse | klasse | «tingrett» |  | lov/1915/08/13/5/nor/§1/ledd-1/punkt-3 (normalisering) | hoy | innvending lav → aksepter | «tingretten» = den tingretten som er stedlig kompetent. Klasse; medlemmene finnes. |
| n190 | Trollheimen | 1 | opprett område | område | «Trollheimen og omegn» (annet) | bestar_av «Meldal (kommune, utgått)» (ny); bestar_av Midtre Gauldal (`e1c180b5`); bestar_av Oppdal (`95ab8adf`); bestar_av Rennebu (`63566b24`); bestar_av Rindal (`513efd19`); bestar_av Sunndal (`7cd… | lov/1984/12/21/101/nor/§1/ledd-1 (fortolkning) | middels | innvending middels → aksepter | Geografisk område uten kode (reindriftsloven § 2 andre ledd). Ikke en aktør. |
| n191 | trygdekontorene | 1 | koble til kjent aktør | omtale |  | utgatt_navn ARBEIDS- OG VELFERDSETATEN (`b3bdec90`) | ikke relevant | middels | innvending lav → aksepter | Utgått betegnelse i forskriften fra 2005. |
| n192 | vertskommune | 1 | opprett rolle | rolle | «vertskommune» | rolle_for kommune (`eb25cd79`) | forskrift/2017/06/22/1050/nor/§2/ledd-1 (fortolkning) | hoy | holder | Rolle en kommune har overfor barnet. |
| n193 P | Årsplanen | 1 | ikke aktør | ikke_aktør |  |  | ikke relevant | hoy | holder | Et dokument (årsplan). PILOT. |

## Mønstre som bør bli regler i køen (telt, ikke bygget)

Antall er navn der mønsteret er brukt i klassifiseringen. Et navn kan ha flere mønstre.

| Mønster | Navn | Forslag til regel |
|---|---:|---|
| «X-ens Y» | 17 | Y knyttes til X; typen bestemmes av Y (organ, stilling, rolle eller gjenstand). Aldri ny aktør av genitiven alene. Unntak: egennavn (Lånekassens klagenemnd, Høyesteretts ankeutvalg) og gjenstander (register, vedtekter). |
| «den/en/de/det/hver/alle/andre X» og ubestemt flertall | 55 | Omtale av klassen X, merket generisk. Ny node bare for X selv. |
| «X der …», «X som …», «den som …» | 32 | Rolle når relativsetningen knytter X til en person eller sak (bostedskommune); ellers vilkår på klassen X i regellaget. |
| «X og/eller Y» | 8 | Del i to omtaler; ingen node for frasen. |
| rolleord + «kommune», «eier», «myndighet» | 12 | Rolle (bostedskommune, vertskommune, barnehageeier). |
| innledende ord eller første ord i et lengre navn | 4 | Feiltreff («Før kommunen», «Direktoratet», «Felles», «Norgespris»): utvid til hele navnet eller fjern ordet foran. |
| register, plan, vedtekter, forskriftstittel, beløp | 11 | Ikke aktør. |
| private personer og personkretser | 20 | Ikke aktør (FORMAT.md; person er utsatt nodetype). |
| stilling eller verv (styrer, leder, medlem, dommer) | 22 | Rolle i organet eller virksomheten, aldri aktør. |
| kjent aktør med annen skrivemåte eller kortform | 6 | Koble som navneform (Høyesterett, Sametinget, NTNU, KD). |
| bare i merknader | 7 | Ingen hjemmel; lav prioritet. |
| utgått navn eller område | 4 | Utgått navneform, eller område med «inngår i». |
| egennavn som ikke finnes | 12 | Kandidat for ny aktør. |

Funn om grupperingen (egne saker, ikke rettet her): 7 navn slår sammen ulike referenter (blant annet styret, virksomheten, institusjonen, tilsynsmyndigheten, medlemmer og Finland). To navn er samme forekomst som et annet navn: «bestemmende organ» / «det bestemmende organ» og «et statlig forvaltningsorgan» / «statlig forvaltningsorgan».

