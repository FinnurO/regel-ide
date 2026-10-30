# Juristrunde #375 (tredje lasting), del b: sammendrag av runde 1

Runden gjelder 25 kort, f1–f25. Det er de nye F- og P-forslagene KI-laget (DeepSeek-V4-Flash) ga ved den tredje lastingen av piloten 2026-10-30: 20 F (alle forvaltning) og 5 P. Alle har klassen kommune som fra, unntatt f15 (Direktoratet for høyere utdanning og kompetanse) og f17 (statsforvalter). Detaljene står i `utfordringer.json`.

Det er ikke prøvd på nytt at klasse som fra leses distributivt (#418), og heller ikke rollen lokal barnehagemyndighet. Lovteksten er lest i den lokale basen. Finansieringsforskriften § 12, §§ 17–18, § 20 og § 25 er i tillegg kontrollert mot Lovdata 2026-10-30.

## Tall

| Dom | Antall |
|---|---:|
| holder | 1 (f14) |
| innvending | 24 |
| kontroller | 0 |
| prinsipiell | 0 |

| Alvor | Antall | Kort |
|---|---:|---|
| høy | 2 | f11, f22 |
| middels | 20 | f1–f10, f12, f13, f15–f20, f23, f25 |
| lav | 2 | f21, f24 |

- **Retting** (kortet står, men skal endres): f3, f4, f7, f10 og f25 til P betaling, f18 til K beslutning, f20 til F sorge_for, og typen i f24 til sorge_for.
- **Ny rad:** P betaling etter barnehageloven § 19 a første ledd (f1) og § 19 b første ledd (f9), og driftstilskuddet etter tildelingsforskriften § 3 første ledd (f19).
- **Avvis:** de andre innvendingene, det vil si f1, f2, f5, f6, f8, f9, f11, f12, f13, f15, f16, f17, f19, f21, f22 og f23.

## De viktigste innvendingene

1. **«Kommunen skal + verb» blir F forvaltning (innvending på 19 av 20 F-kort).** KI-laget gjør fortsatt det samme som juristrunde 375 påpekte (f10, f24–f28, f71). Det er tre grupper:
   - *Betalingsplikt:* «skal gi [eiendoms-/grunn]tilskudd» og «skal tildele tilskuddet» (f3, f4, f7, f10, f25). Det er P betaling med til = null og modalitet skal, slik presedensen er fra f9 og f71 i juristrunde 375 og k69, k71 og k77 i juristrunde 418.
   - *Beregningsregler:* «beregne … på samme måte» og indeksregulering (f1, f9, f12) hører til regellaget.
   - *Henvisninger og saksbehandling:* «gi påslag, jf. § 12» (f2, f8), søknadsskjema (f15), «vurdere å følge opp» (f16) og innsyn (f18). Ingen av dem er en oppgave.
   Løsningen bør være systemisk, ikke kort for kort. KI-instruksen bør skille mellom å betale, å beregne og å ha ansvar for et felt før den bruker forvaltning. Begrepsmanualen §F reserverer forvaltning for regelverk, ordninger, fond og apparat.
2. **Et fradrag blir en betalingsplikt (f11, høy).** Finansieringsforskriften § 20 begrenser kommunens plikt; overskriften på Lovdata er «Begrensninger i kommunens plikt til å gi tilskudd». Som P betaling sier raden det motsatte, og den vises som en egen betalingsplikt i pliktoppslaget.
3. **P samarbeid uten motpart (f22, høy).** Plikten etter barnehageloven § 11 til å organisere myndighets- og eieroppgavene atskilt er hverken samarbeid eller struktur. Den er en nesten-dublett av k81, som juristrunde 418 anbefalte å avvise. Det strukturelle innholdet er rolleskillet, og det står alt (I innehar, 8a02a7d6).
4. **Feil P-type for kommunens plasstilbud (f20).** «Kommunen har plikt til å tilby plass i barnehage» er ført som P informasjon. Det er kommunens resultatansvar for befolkningen (F sorge_for, som hotl. u9), og barnets rett etter § 16 hører til regellaget. Som P ville barna fått en strukturell kravsrett (17.4).
5. **Dubletter av forslag som alt står i køen (f5, f6, f13, f17, f21, f22).**
   - f17 dubler bdd661d0, og 1c4bd084, som står i samme ledd, dubler a67f6b72 på samme måte (utenfor raden).
   - f21 dubler 1e81663e, som har riktig type (sorge_for).
   - f13 dubler avgrensningen juristrunde 418 la på K normgivning dac5ce22. Raden får også en adgang («kan gi retningslinjer») til å se ut som en oppgave.
   - f5 og f6 er de samme betalingspliktene som f4, bare med andre satser.

## Tverrgående mønstre

- **Betalingsplikten mangler der den står i loven.** Basen har ingen P betaling på barnehageloven § 19 a første ledd (grunntilskudd) eller § 19 b første ledd (pensjonstilskudd). Den har heller ingen på finansieringsforskriften § 6 og § 13 (ordinære barnehager) eller tildelingsforskriften § 3 første ledd. Forslagene fanger dermed undertilfellene (familie- og åpne barnehager, påslag), men ikke hovedpliktene. Ny rad er foreslått på f1, f9 og f19. § 6 og § 13 i finansieringsforskriften bør tas i neste KI-kjøring.
- **Påslaget er en del av tilskuddet, ikke en egen betaling.** Det gjelder f2, f8 og f19, og bygger på § 12 («4,7 prosent av det samlede grunntilskuddet») og satsene «inklusive administrasjonspåslag». Juristrunde 418 (k67) var tvetydig her. Lesningen i denne runden er én betalingsplikt per tilskuddstype, med påslaget i beregningen. Modellereren bør ta stilling til dette, slik at påslag behandles likt i alle forskriftene.
- **Tidsavgrensning.** Finansieringsforskriften gjelder fra tilskuddsåret 2027, og tildelingsforskriften oppheves 31.12.2026 (§ 25). Ingen av radene har dette i avgrensningen. Midlertidig forskrift § 3 (f25) viser til tildelingsforskriften, og hva henvisningen gjelder etter 2026, avgjøres ikke i teksten.
- **Mulig versjonsavvik (17.4: feil lovversjon).** I korpuset har tabellene i finansieringsforskriften §§ 17–18 plassholderen «[Satsene fastsettes årlig av KD]». Lovdata 2026-10-30 har tallsatser, og ifølge Lovdata har § 18 et femte ledd som korpuset ikke viser. Kontroller korpusversjonen før KI-raden «KD vedtakskompetanse» (ikke lastet, ukjent aktør) blir vurdert. Den bygger på plassholderen.
- **Modalitet.** Kortene viser ikke modalitet for F, men databaseradene har «skal» der teksten har modalverb og null ved «har ansvaret for». Det er riktig. Ved retting fra F til P beholdes «skal».
- **Kunnskapsstatus.** Alle forslagene har kunnskapsstatus null (ikke vurdert), og fra er løst med standardregelen. Det er registrert i #411 og er ikke gjentatt per kort.

## Prinsipielle innvendinger

Ingen. Ingen av innvendingene gjelder modellreglene. De gjelder hvordan KI-laget bruker F, og at hovedpliktene ikke er lastet.
