/** Innholdet i høyre kontekstpanels "Detaljer"-fane — se `KontekstPanel.tsx`. `body` er `null` når
 * ingen ytterligere tekst finnes å vise (aldri en oppfunnet tekst — "ingen gjettet fallback").
 * <p>
 * Flyttet ut av `tjeneste/` (2026-09-02, docs/30 §4 punkt 1 — saksbehandlertilpasningen) sammen med
 * `KontekstPanel`/`Accordion` til denne entitetsuavhengige plasseringen — typen var allerede
 * entitetsuavhengig, kun plasseringen var Tjeneste-spesifikk. */
export interface DetaljVisning {
  meta: string;
  title: string;
  body: string | null;
}
