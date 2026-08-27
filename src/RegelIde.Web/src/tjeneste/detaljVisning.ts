/** Innholdet i høyre kontekstpanels "Detaljer"-fane — se `KontekstPanel.tsx`. `body` er `null` når
 * ingen ytterligere tekst finnes å vise (aldri en oppfunnet tekst — "ingen gjettet fallback"). */
export interface DetaljVisning {
  meta: string;
  title: string;
  body: string | null;
}
