# Anbefalt behandling av pilotforslagene (#375)

Avledet av juristrunden (utfordringer.json, svar.json, replikk.json). Anbefalingen er ikke en godkjenning; Johann avgjør i forslagskøen (/ki-oppdagelse).
«godkjenn» = juristen sa holder, eller innvendingen gjelder noe utenfor raden. «rett, så godkjenn» = rettingen i utfordringer.json bør gjøres (revisjon) før godkjenning. «avvis» = modellereren og juristen er enige om at raden er feil.

Antall: {'avvis': 21, 'rett, så godkjenn': 23, 'godkjenn': 27}

| Kort | Rettskilde | eId | Kategori/type | Fra | Anbefaling | Kort grunn |
|---|---|---|---|---|---|---|
| f1 | Delegering av Kongens myndighet etter barneha | ledd-1 | K normgivning | Kunnskapsdepartementet | avvis | Delegeringen gjaldt bare fra 21 |
| f2 | Delegering av Kongens myndighet etter barneha | ledd-1 | R har_delegert_til | Kongen i statsråd | avvis | Typen (R har_delegert_til, Kongen → KD) er riktig, men kanten mangler tidsavgrensningen, som står i sitatet: «med virkning fra 21 |
| f3 | Forskrift om barnehagevirksomhet i Longyearby | §6/ledd-2 | K vedtak | Kunnskapsdepartementet | rett, så godkjenn | «avgjør» er ikke nok for vedtak (fvl |
| f4 | Forskrift om enkelte klagenemnder på Kunnskap | §6/ledd-1 | K klage | Klagenemnda for godkjenning av utenlandsk utdanning og yrkesgodkjenning | godkjenn |  |
| f5 | Forskrift om enkelte klagenemnder på Kunnskap | §6/ledd-2 | K klage | Klagenemnda for godkjenning av utenlandsk utdanning og yrkesgodkjenning | godkjenn |  |
| f6 | Forskrift om enkelte klagenemnder på Kunnskap | §7/ledd-1 | K oppnevning | Kunnskapsdepartementet | rett, så godkjenn | «oppnevnes» skal gi undertype oppnevning (#352) |
| f7 | Forskrift om finansiering av private barnehag | §7/ledd-2 | F forvaltning | STATISTISK SENTRALBYRÅ | avvis | «sentralitetsindeksen som utarbeides av Statistisk sentralbyrå» er en identifiserende beskrivelse i en definisjon av sentralitetssone |
| f8 | Forskrift om finansiering av private barnehag | §8/ledd-1 | F sorge_for | kommune | godkjenn |  |
| f9 | Forskrift om finansiering av private barnehag | §15/ledd-3 | P betaling | kommune | rett, så godkjenn | Leddet sier selv «Kommunens plikt til å dekke barnehagens pensjonsutgifter», så P betaling med til = null holder |
| f10 | Forskrift om finansiering av private barnehag | §18/ledd-3 | F forvaltning | kommune | avvis | «Kommunen beregner tilskudd … ut fra hvor mange barn …» er en beregningsregel (regellaget, L16), ikke et arbeidsfelt eller en forvaltningsop |
| f11 | Forskrift om finansiering av private barnehag | (merknad/veiledning) | K vedtak | Finansdepartementet | avvis | Noden ligger i «Veiledning til de enkelte bestemmelsene» og er departementets veiledning, ikke forskriftstekst (eId uten ELI) |
| f12 | Forskrift om finansiering av private barnehag | (merknad/veiledning) | K normgivning | Finansdepartementet | avvis | Samme som f11, og verre: anslaget for kommunal deflator er gjort til normgivning med normform forskrift |
| f13 | Forskrift om foreldrebetaling i barnehager | §1/ledd-2 | K beslutning | STORTINGET | rett, så godkjenn | K beslutning for Stortinget kan stå, men forskriften (fastsatt ved kgl.res.) hjemler ikke Stortingets kompetanse |
| f14 | Forskrift om foreldrebetaling i barnehager | §3e/ledd-1 | P informasjon | kommune | avvis | «Kommunen skal innhente de opplysninger den trenger for å behandle søknader» er utredningsplikt i saksbehandlingen (jf |
| f15 | Forskrift om foreldrebetaling i barnehager | §3e/ledd-4 | P informasjon | kommune | avvis | Som f14: kommunen skal be søkeren levere skattemelding |
| f16 | Forskrift om foreldrebetaling i barnehager | §6/ledd-1 | K vedtak | Kongen i statsråd | rett, så godkjenn | «For 2006 fastsettes maksimalgrensen av Kongen i Statsråd» er en overgangsregel som er uttømt |
| f17 | Forskrift om godkjenning av yrkeskvalifikasjo | §11/ledd-3 | K klage | Kunnskapsdepartementet | rett, så godkjenn | K klage skal ha motparten som til (#341) |
| f18 | Forskrift om overgangsregler til barnehagelov | §2/ledd-1 | F kontroll | kommune | rett, så godkjenn | «skal fullføre pågående tilsyn» er ikke F kontroll («påse at») |
| f19 | Forskrift om overgangsregler til barnehagelov | §2/ledd-1 | F kontroll | statsforvalter | rett, så godkjenn | Samme som f18, for statsforvalteren |
| f20 | Forskrift om overgangsregler til barnehagelov | §4/ledd-1 | P informasjon | kommune | rett, så godkjenn | P informasjon kommune → statsforvalter for et stengingsvedtak som sendes til orientering, holder som type |
| f21 | Forskrift om regnskapsplikt mv. for private b | §8/ledd-1 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | normform forskrift er KI-lagets egen slutning («kan anses som forskrift») |
| f22 | Forskrift om tildeling av tilskudd til privat | §2/ledd-1 | F sorge_for | kommune | avvis | «Kommunen skal sørge for at saken er så godt opplyst som mulig før det fattes vedtak» er utredningsplikten i saksbehandlingen (jf |
| f23 | Forskrift om tildeling av tilskudd til privat | §2/ledd-2 | F forvaltning | kommune | avvis | Begrunnelsesplikten (jf |
| f24 | Forskrift om tildeling av tilskudd til privat | §3/ledd-3 | F forvaltning | kommune | avvis | «Kommunen skal beregne satsene … i samsvar med forholdstall …» er en beregningsregel (regellaget) |
| f25 | Forskrift om tildeling av tilskudd til privat | §3/ledd-6 | F forvaltning | STATISTISK SENTRALBYRÅ | avvis | Samme som f7: en identifiserende beskrivelse («som utarbeides av Statistisk sentralbyrå (oppdatert juni 2023)») gir ingen F |
| f26 | Forskrift om tildeling av tilskudd til privat | §4a/ledd-2 | F forvaltning | kommune | avvis | «skal legge til grunn barnehagens pensjonsutgifter som fremgår av regnskapet» er en beregnings- og bevisregel (regellaget), ikke en oppgave |
| f27 | Forskrift om tildeling av tilskudd til privat | §4a/ledd-4 | F forvaltning | kommune | avvis | Indeksregulering er et beregningsskritt (regellaget), ikke F forvaltning |
| f28 | Forskrift om tildeling av tilskudd til privat | §15/ledd-2 | F forvaltning | kommune | avvis | «Kommunen skal gjøre fratrekk etter § 11 første ledd» er et beregningsskritt i refusjonskravet (regellaget) |
| f29 | Forskrift om tildeling av tilskudd til privat | (merknad/veiledning) | K vedtak | Kunnskapsdepartementet | avvis | Noden ligger i «Merknader til de enkelte bestemmelsene» og er departementets kommentar, ikke forskriftstekst (relativ eId uten ELI) |
| f30 | Forskrift om tildeling av tilskudd til privat | (merknad/veiledning) | K normgivning | Kunnskapsdepartementet | avvis | Som f29: merknad, ikke forskriftstekst |
| f31 | Forskrift om tildeling av tilskudd til privat | (merknad/veiledning) | P betaling | kommune | avvis | Merknad, ikke forskriftstekst |
| f32 | Forskrift om tildeling av tilskudd til privat | (merknad/veiledning) | P betaling | kommune | avvis | Duplikat av f31 i en annen merknadsnode (rom-andre-ledd) |
| f33 | barnehageloven | §2/ledd-7 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f34 | barnehageloven | §2b/ledd-2 | F samordning | kommune | godkjenn | F samordning for kommunen holder (begrepsmanualen §F: «skal samordne» uten bindende virkning) |
| f35 | barnehageloven | §7a/ledd-4 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f36 | barnehageloven | §10/ledd-1 | F veiledning | kommune | rett, så godkjenn | F veiledning holder, og §10 er lærebokeksempelet i begrepsmanualen |
| f37 | barnehageloven | §10/ledd-1 | F kontroll | kommune | rett, så godkjenn | F kontroll for «påse at» er riktig (begrepsmanualen: §10 er oppgaven, §53 er K tilsyn) |
| f38 | barnehageloven | §10/ledd-4 | K normgivning | Kunnskapsdepartementet | godkjenn | Normgivningsraden holder |
| f39 | barnehageloven | §13/ledd-1 | F veiledning | statsforvalter | rett, så godkjenn | F veiledning holder |
| f40 | barnehageloven | §14/ledd-4 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f41 | barnehageloven | §14b/ledd-1 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | Typen holder |
| f42 | barnehageloven | §14b/ledd-2 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f43 | barnehageloven | §15/ledd-3 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f44 | barnehageloven | §17/ledd-2 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f45 | barnehageloven | §19/ledd-4 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f46 | barnehageloven | §19a/ledd-8 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | Andre punktum, «Forskrift etter første punktum kan gjøre unntak fra reglene i første og andre ledd», gir forskriften adgang til å fravike lo |
| f47 | barnehageloven | §19b/ledd-3 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | «Departementet skal gi forskrift» er en plikt til å gi forskrift |
| f48 | barnehageloven | §19b/ledd-5 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | Som f46: «Forskrift gitt etter første punktum kan gjøre unntak fra reglene i første og andre ledd» mangler. |
| f49 | barnehageloven | §19c/ledd-1 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | Som f47: «Departementet skal gi forskrift» er pålagt normgivning, og «skal» forsvinner |
| f50 | barnehageloven | §20/ledd-1 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f51 | barnehageloven | §23/ledd-6 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f52 | barnehageloven | §23a/ledd-2 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f53 | barnehageloven | §23c/ledd-1 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f54 | barnehageloven | §24/ledd-3 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f55 | barnehageloven | §24/ledd-4 | K oppretting | Kunnskapsdepartementet | godkjenn | K oppretting holder («kan opprette en klagenemnd») |
| f56 | barnehageloven | §25/ledd-2 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | Objektet er kuttet («… arbeider i barnehagen på») |
| f57 | barnehageloven | §28/ledd-4 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f58 | barnehageloven | §30/ledd-4 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f59 | barnehageloven | §31/ledd-6 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | Objektet er kuttet midt i oppregningen |
| f60 | barnehageloven | §33/ledd-3 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f61 | barnehageloven | §34/ledd-3 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f62 | barnehageloven | §38/ledd-7 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f63 | barnehageloven | §47/ledd-1 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f64 | barnehageloven | §47a/ledd-3 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | Typen holder |
| f65 | barnehageloven | §48/ledd-1 | K normgivning | Kunnskapsdepartementet | godkjenn |  |
| f66 | barnehageloven | §48a/ledd-4 | K vedtak | STATISTISK SENTRALBYRÅ | avvis | «Personopplysninger fra registeret kan ikke utleveres til andre enn Statistisk sentralbyrå» er et utleveringsforbud med SSB som eneste tilla |
| f67 | barnehageloven | §48a/ledd-5 | K normgivning | Kunnskapsdepartementet | godkjenn | Raden holder for første punktum |
| f68 | barnehageloven | §52/ledd-1 | K normgivning | Kongen i statsråd | godkjenn |  |
| f69 | barnehageloven | §57/ledd-1 | K beslutning | Kongen i statsråd | rett, så godkjenn | K beslutning for ikrafttredelse holder som type, men kompetansen er brukt og uttømt (loven trådte i kraft 1 |
| f70 | barnehageloven | §57/ledd-4 | K normgivning | Kunnskapsdepartementet | rett, så godkjenn | Andre punktum utvider hjemmelen: «Det kan også gis bestemmelser som viderefører bestemmelser i loven her om tilsyn, veiledning og reaksjonsm |
| f71 | Midlertidig forskrift om midlertidig godkjenn | §3/ledd-2 | F forvaltning | kommune | avvis | «skal … beregne tilskuddet … på nytt» er et beregningsskritt (regellaget), ikke F forvaltning (som f24) |
