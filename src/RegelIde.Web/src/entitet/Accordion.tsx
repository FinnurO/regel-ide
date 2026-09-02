import type { ReactNode } from 'react';
import { Button, Details, Textfield } from '@digdir/designsystemet-react';

export interface AccordionProps {
  apen: boolean;
  kanFlyttes: { opp: boolean; ned: boolean };
  onToggle: (apen: boolean) => void;
  onFlytt: (retning: -1 | 1) => void;
  tittelSuffiks?: string;
  onFjern?: () => void;
  tittel: string;
  onTittelChange?: (t: string) => void;
  children: ReactNode;
}

/**
 * Delt `Details`-accordion-skall — flyttet ut av `tjeneste/InnholdFane.tsx` (2026-09-02, docs/30 §4
 * punkt 1 — saksbehandlertilpasningen) til denne entitetsuavhengige plasseringen. Komponenten var
 * allerede fullstendig entitetsuavhengig (kun opp/ned/åpen/fjern/tittel-props), kun plasseringen var
 * Tjeneste-spesifikk — INGEN atferdsendring i denne flyttingen.
 * <p>
 * Kontrollert `open`+`onToggle` (verifisert mot installert `1.18.0` sine `.d.ts`-filer — `onToggle`
 * er en native `toggle`-hendelse, `(e.target as HTMLDetailsElement).open` gir den NYE tilstanden).
 * Egne opp/ned/fjern-knapper i `Summary` MÅ `e.stopPropagation()` — hele `Summary` toggler ellers
 * accordion-en når man klikker en knapp inni den.
 */
export function Accordion({
  apen, kanFlyttes, onToggle, onFlytt, tittelSuffiks, onFjern, tittel, onTittelChange, children,
}: AccordionProps) {
  return (
    <Details open={apen} onToggle={(e) => onToggle((e.target as HTMLDetailsElement).open)} style={{ marginBottom: '0.6rem' }}>
      <Details.Summary>
        <span style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', width: '100%', gap: '0.5rem' }}>
          {onTittelChange ? (
            <Textfield
              data-size="sm" value={tittel} onChange={(e) => onTittelChange(e.target.value)}
              onClick={(e) => e.stopPropagation()} aria-label="Tittel på innholdselement"
              style={{ maxWidth: '18rem', border: 'none', background: 'transparent', fontWeight: 600 }}
            />
          ) : (
            <span style={{ fontWeight: 600 }}>
              {tittel}{tittelSuffiks && <span style={{ fontWeight: 400, fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginLeft: '0.4rem' }}>{tittelSuffiks}</span>}
            </span>
          )}
          <span style={{ display: 'flex', gap: '0.2rem' }} onClick={(e) => e.stopPropagation()}>
            <Button variant="tertiary" data-size="sm" disabled={!kanFlyttes.opp} onClick={() => onFlytt(-1)} title="Flytt opp" style={{ minWidth: 0, padding: '0 0.3rem' }}>↑</Button>
            <Button variant="tertiary" data-size="sm" disabled={!kanFlyttes.ned} onClick={() => onFlytt(1)} title="Flytt ned" style={{ minWidth: 0, padding: '0 0.3rem' }}>↓</Button>
            {onFjern && <Button variant="tertiary" data-size="sm" data-color="danger" onClick={onFjern} title="Fjern innholdselement" style={{ minWidth: 0, padding: '0 0.3rem' }}>✕</Button>}
          </span>
        </span>
      </Details.Summary>
      <Details.Content>{children}</Details.Content>
    </Details>
  );
}
