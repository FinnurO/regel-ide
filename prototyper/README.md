# Prototyper

Interaktive HTML-mockuper fra Claude Design — den visuelle/frontend-siden av regel-ide, holdt bevisst atskilt fra det tekniske designarbeidet i `docs/` (se `docs/08-byggesteg1-teknisk-design.md`s innledning for hvorfor frontend-komponentstruktur er overlatt til dette verktøyet i stedet for Claude Code).

| Fil | Dekker | Kilde |
|---|---|---|
| `Byggesteg1-Rettskilder.dc.html` | Rettskildebibliotek (bibliotek-oversikt, importveiviser Hent→Konverter→Metadata→Verifiser, rettskilde-detalj med Tekst/AKN-kilde/Identitet/Koblinger/Historikk-faner, versjonssammenligning med `tekst_hash`-diff) | [Claude Design-prosjekt "Regel-IDE prototype"](https://claude.ai/design/p/7785b113-6899-4faf-b6e0-90102db8e7a5?file=Byggesteg1-Rettskilder.dc.html) |
| `Regel-IDE.dc.html` | Den opprinnelige, fulle interaktive prototypen — alle 14 skjermer fra førsteutkastets navigasjon (kap. 2 i `historikk/Kravspesifikasjon-original-v1.0.md`), statiske data og simulerte interaksjoner. Kilden `historikk/Kravspesifikasjon-original-v1.0.md` ble utledet fra denne. | Samme Claude Design-prosjekt, fil `Regel-IDE.dc.html` |

Byggesteg1-mockupen implementerer det låste designet i `docs/08-byggesteg1-teknisk-design.md` presist — ELI-forankret identitet, FRBRauthor kildetype-avhengig, `<hcontainer>` for romertall, `end`-attributt for opphevede paragrafer, canonical_id/kilde_id/kildesystem-skillet, og `tekst_hash`-basert tag-overlevelse på tvers av versjoner. Verdt å sjekke igjen for avvik hvis `08-byggesteg1-teknisk-design.md` endres videre (f.eks. hvis §6-punktene om ELI-seksjonssyntaks eller opphevelses-attributter avklares annerledes enn antatt her).

`Regel-IDE.dc.html` er derimot uendret siden førsteutkastet — den viser fortsatt XOR/NAND-operatorer, den samlede Vilkår-noden osv., altså designet **før** de tre QA-rundene i `docs/`. Den er beholdt for historisk sporbarhet (se `../historikk/README.md`), ikke som et gjeldende referansepunkt for de andre byggestegene.
