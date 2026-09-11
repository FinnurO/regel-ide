# Design-canvas — kildeøyeblikksbilde (2026-09-11)

Dette er kildefilene til en hel-applikasjons design-gjennomgang gjort med `design`-skillet i
Claude Code, sjekket inn her som et varig øyeblikksbilde — den midlertidige arbeidsmappen verktøyet
bygger i, og selve web-lenken under, er begge økt-lokale og kan gå tapt.

**Interaktiv, redigerbar visning**: https://claude.ai/code/artifact/cff0d4c6-dcb9-4a1c-af4d-ae4da15da850
(privat Artifact — del fra artifactets eget delingsmeny om flere skal ha tilgang).

## Innhold

16 `.dc.html`-artboards + `canvas.json` (layout-manifest), fordelt på 7 sider:

| Fil | Dekker |
|---|---|
| `Main.dc.html` | Fundament — farger, typografiskala, mellomrom (målt mot installert `@digdir/designsystemet`) |
| `Komponenter.dc.html` | Nye delte primitiver (Metatekst, KandidatStatusTag, DokumenttypeTag), StatusStepper-dokumentasjon, de 6 funnede statusfarge-duplikatene + konsolideringsbeslutning |
| `ListePagemal.dc.html` | Generisk listemal + status for alle 14 listesider |
| `Detaljsidemal.dc.html` | Generisk detaljmal + status for alle 8 detaljsider |
| `VirksomheterListe.dc.html` / `VirksomhetDetalj.dc.html` | Konkret før/etter-forslag (issue #268, IKKE besluttet) |
| `TjenesteOgHandling.dc.html` / `TjenesteFaner.dc.html` | Tjeneste/Handling innholdsverifisert i dybden |
| `Veivisermal.dc.html` | De tre reelt ulike veiviser-mønstrene (NavnekandidatVeiviser/ImportWizard/Importer) |
| `RettskildeLesing.dc.html` | RettskildeDetalj + TagTekst — appens mest brukte flate |
| `KIForslagsko.dc.html` | BegrepsforslagKo vs. TjenesteforslagKo |
| `VisualiseringOgSkall.dc.html` | VilkarstreGraf/RettskildeTre + appskallet |
| `Dokumentvisning.dc.html` | TjenesteVeiledning, HandbokOpprett, RaaTekstMedLenker |
| `SkjemaOgMerkelapper.dc.html` | «Legg til X»-skjemaer, Navneformgrunn |
| `TjenestereiseOgSok.dc.html` | Tjenestereise-graf (React Flow), GlobaltSok |
| `Handboksforfatting.dc.html` | KommentarRedigering + MinimalEditor |

## Hva som faktisk er bindende

Canvasen er referanse — den viser hvordan og hvorfor, med ekte målte tall og kodesitater. Det som
faktisk er besluttet er destillert til **docs/09-design-konvensjoner.md §23–29**; det er de
seksjonene som gjelder, ikke denne mappen direkte. §29 er eksplisitt merket som forslag, ikke
vedtak — VirksomhetDetalj-omstruktureringen bygges kun hvis issue #268 tas videre.

## Hvordan det brukes videre

- **Kode**: byttes ut mot faktiske komponenter/regler etter hvert som issue #266 (Metatekst),
  #267 (statusfarge/tetthet/knapp-`data-size`) og evt. #268 (VirksomheterListe/VirksomhetDetalj)
  bygges.
- **Canvasen selv**: republiseres (samme URL) om innholdet endres vesentlig — se `design`-skillet.
  Filene her i repoet oppdateres da i samme commit, så de to aldri driver fra hverandre over tid.
