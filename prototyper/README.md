# Prototyper

Interaktive HTML-mockuper fra Claude Design — den visuelle/frontend-siden av regel-ide, holdt bevisst atskilt fra det tekniske designarbeidet i `docs/` (se `docs/08-byggesteg1-teknisk-design.md`s innledning for hvorfor frontend-komponentstruktur er overlatt til dette verktøyet i stedet for Claude Code).

| Fil | Dekker | Kilde |
|---|---|---|
| `Byggesteg1-Rettskilder.dc.html` | Rettskildebibliotek (bibliotek-oversikt, importveiviser Hent→Konverter→Metadata→Verifiser, rettskilde-detalj med Tekst/AKN-kilde/Identitet/Koblinger/Historikk-faner, versjonssammenligning med `tekst_hash`-diff) | [Claude Design-prosjekt "Regel-IDE prototype"](https://claude.ai/design/p/7785b113-6899-4faf-b6e0-90102db8e7a5?file=Byggesteg1-Rettskilder.dc.html) |

Denne mockupen implementerer det låste designet i `docs/08-byggesteg1-teknisk-design.md` presist — ELI-forankret identitet, FRBRauthor kildetype-avhengig, `<hcontainer>` for romertall, `end`-attributt for opphevede paragrafer, canonical_id/kilde_id/kildesystem-skillet, og `tekst_hash`-basert tag-overlevelse på tvers av versjoner. Verdt å sjekke igjen for avvik hvis `08-byggesteg1-teknisk-design.md` endres videre (f.eks. hvis §6-punktene om ELI-seksjonssyntaks eller opphevelses-attributter avklares annerledes enn antatt her).

Samme Claude Design-prosjekt inneholder også `Regel-IDE.dc.html` (den opprinnelige, fulle interaktive prototypen kravspesifikasjonen ble utledet fra) og `Kravspesifikasjon.md` (kildedokumentet). Disse er ikke hentet inn i dette repoet ennå.
