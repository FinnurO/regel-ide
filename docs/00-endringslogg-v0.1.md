# Endringslogg v0.1 — fra førsteutkastet

Dette restrukturerer kravspesifikasjonen "Forvaltningsverktøy for digitale tjenester («Regel-IDE»)" v1.0 (2026-07-23), på grunnlag av en ekstern vurdering av det dokumentet og begrepsmodellen "Referansemodell for regelverk, beslutninger og digitalisering". Ingen funksjonalitet er fjernet — strukturen og noen begreper er endret.

## Hva den eksterne vurderingen pekte på (og hva vi har gjort med det)

| Funn | Vurdering | Adressert i |
|---|---|---|
| Dokumentet blander produktkrav, domenemodell, implementasjonsvalg og arkitektur | 9/10 domenemodell, men trenger oppdeling | Splittet i 7 dokumenter, se README |
| Uklart skille krav vs. anbefaling | — | Konsekvent "skal" (bindende) / "bør" (anbefalt) i `02-produktkrav.md` og `05-arkitektur-og-nfk.md` |
| Rolle- og autorisasjonsmodell for svak (6/10) — ingen RBAC-matrise | — | RBAC-matrise i `03-domenemodell.md` §2 |
| Livssykluser ufullstendige | — | Tilstandsdiagrammer for vilkår, rettskilde, tjeneste, kodeliste i `03-domenemodell.md` §3 |
| Publiseringsmodell mangler (hva publiseres, atomisk?, rollback?) | — | `03-domenemodell.md` §4 |
| API-kontrakter mangler (3/10) | — | `04-api-kontrakter.md` |
| Relasjoner mellom entiteter ikke egen del | — | `03-domenemodell.md` §1, med ER-diagram |
| Tagging som tegnintervaller er sårbar ved konsoliderte lovendringer/korrektur | Teknisk risiko | Flagget i `05-arkitektur-og-nfk.md` §3 — anbefaler `eId` + quote selector (W3C Web Annotation-mønster) i tillegg til offset |
| Vilkårsmodellen bør kreve DAG eksplisitt | Teknisk risiko | Eksplisitt krav i `03-domenemodell.md` §1 og `05-arkitektur-og-nfk.md` §1 |
| XOR/NAND sjelden i juridiske regelsett | Vurder å fjerne | **Fjernet.** Se begrunnelse i `01-referansemodell.md` §3 — verken Schartum (2025) eller `digital-rettsstat` bruker disse; kun OG/ELLER/IKKE + sammenligningsoperatorer |
| Hendelsesmodell (domenehendelser) ikke formalisert | — | `03-domenemodell.md` §5 |
| Nummerering (kap. 5 → 7.1) | Kosmetisk | Rettet i `02-produktkrav.md` |
| Ingen skille MVP vs. senere faser | — | Faser markert per skjerm/entitet i `02-produktkrav.md`, utdypet i `06-veikart.md` |

## Den viktigste endringen: "Vilkår" i kap. 3.8 er delt i to

Førsteutkastets `Vilkår`-entitet (regelnode med `barn[]`, `barn_operator`, `utdata_parameter`) konfliderte tre ting som både begrepsmodellen og `digital-rettsstat/docs/06-regellaget.md` (*"Regel betyr tre ulike ting"*) advarer eksplisitt mot å blande: en **rettsregel** (vilkår + rettsfølge + unntak), en **beslutningsregel** (komposisjonslogikken/operatoren) og et **atomært vilkår** (en enkelt testbar betingelse). Dette er markert som en **åpen designbeslutning som trenger flere iterasjoner** — ikke låst i v0.1 — se `01-referansemodell.md` §4 for detaljer og alternativer.

## Hva som er uendret

Alle skjermer, akseptkriterier (AK-*), datamodellens innhold (feltnivå) og designsystem-kravene fra førsteutkastet er videreført i `02-produktkrav.md`. Testcase (skjenkebevilling) er uendret.
