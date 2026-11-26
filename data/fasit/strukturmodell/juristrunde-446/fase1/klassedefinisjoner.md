# #446 – forslag til klasser for «uttømt virkning» (fase 1, til juristrunde)

Formål: avgjøre, uten gjetting (CLAUDE.md §8), om en gjeldende lov eller forskrift i korpuset er et vedtak som bare endrer, setter i kraft eller opphever andre dokumenter. Slike vedtak har uttømt virkningen sin når de er i kraft: innholdet står i dokumentet de endret. Brukes av to maskinregler:
- #472 «Hjemmel opphevet»: en forskrift som er et rent vedtak, skal ikke gi funn om at hjemmelen er opphevet.
- #429 «Utgått aktør»: et utgått navn i et rent endringsvedtak er historisk tekst, ikke en feil.

Kilden er Lovdatas rå HTML for dokumentet (den som ble lastet ned), ikke bare nodene i korpuset. Den rå teksten har endringsinstruksene («I lov … gjøres følgende endringer:», «§ 3 skal lyde:») og Lovdatas markering av foreslått ny paragraf.

## Lesing ledd for ledd
Hvert ledd (og hver setning i leddet) får én rolle:
- E – endringsinstruks: «I [dokument] gjøres følgende endring(er)», «§ X skal lyde», «Ny § X skal lyde», «… erstattes med …», «… oppheves» inne i en endringsdel, «I følgende lover endres X til Y».
- Q – sitert ny tekst: alt som står etter en endringsinstruks i samme romertallsdel eller paragraf, og alt i Lovdatas markering for foreslått/endret tekst.
- I – ikrafttredelse: «trer i kraft», «gjelder fra», «tek til å gjelde», «settes i kraft», «Kongen kan sette i kraft de enkelte bestemmelsene til forskjellig tid», «gis virkning fra [dato]». Ikke når setningen også har eget innhold (for eksempel «gjelder ikke for», «fortsatt», «for saker/søknader/personer …», «dersom», «kan gi forskrift», «overgangsregler»).
- O – oppheving av et annet dokument: «[dokument] oppheves», «Fra samme tid oppheves forskrift …».
- S – eget innhold: alt annet (overgangsregler, delegering, materielle regler, hjemler for forskrift).
Overskrifter, ingresser («Fastsatt av …»), lister over endrede lover og Lovdatas merknader telles ikke.

## Klassene
| Klasse | Regel | Kunnskapsstatus (forslag) |
|---|---|---|
| rent_endringsvedtak | Ingen S. Minst én E/Q (eller: endringsdelen er innarbeidet og bare I/O står igjen), og minst ett signal til fra kilden: tittel med endringsform, tittel med ikrafttredelsesform, eller hodet sier at kilden endrer andre dokumenter (changesToDocuments). | normalisering (formen leses av teksten etter navngitt regel) |
| rent_ikrafttredelsesvedtak | Ingen S, ingen E/Q. Bare I (og eventuelt O). Tittelen har ikrafttredelsesform. | normalisering |
| rent_opphevingsvedtak | Ingen S, ingen E/Q. Minst én O (og eventuelt I). | normalisering |
| blandet | Tittelen har endrings-, opphevings- eller ikrafttredelsesform, men kilden har minst ett S-ledd. Grunnlaget peker på første S-ledd (eId og sitat). Eksempel: DEL/forskrift/1999-06-25-711 «ikrafttredelse av og overgangsregler til revisorloven». | normalisering |
| selvstendig | Minst ett S-ledd, og tittelen har ikke slik form. | normalisering |
| ukjent | Ingen operativ tekst, rå Lovdata-tekst mangler, eller formen i teksten og signalene i tittel/hode spriker. | uavklart |

Rent betyr «formen er bare endring/ikrafttredelse/oppheving». Om virkningen faktisk er uttømt, avhenger i tillegg av at vedtaket er i kraft. Forslaget har derfor en egen opplysning: i kraft (ja / delvis / Kongen bestemmer / framtidig / ukjent), lest fra Lovdatas «I kraft fra».
