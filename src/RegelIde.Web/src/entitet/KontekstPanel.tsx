import type { CSSProperties } from 'react';
import { Button, Heading, Paragraph, Tabs } from '@digdir/designsystemet-react';
import type { DetaljVisning } from './detaljVisning';

export interface KontekstPanelItem {
  key: string;
  label: string;
  onClick: () => void;
}

export interface KontekstPanelGruppe {
  heading: string;
  items: KontekstPanelItem[];
}

export interface KontekstPanelProps {
  /** 'sidebar' (standard): fast 320px bredde + kollaps-knapp + intern Relasjoner/Detaljer-veksling —
   * Tjenestedetalj-mønsteret, uendret. 'full': fyller tilgjengelig bredde, ingen kollaps, ingen intern
   * Detaljer-fane — for bruk som INNHOLD i en egen fane på sider uten et permanent sidepanel (f.eks.
   * Rettskildedetaljs "Relasjoner"-fane, docs/30 §4 punkt 2), der et klikk på en rad navigerer direkte
   * i stedet for å vise en forhåndsvisning i samme panel. */
  variant?: 'sidebar' | 'full';
  collapsed?: boolean;
  onToggleCollapsed?: () => void;
  rightTab?: 'relasjoner' | 'detaljer';
  setRightTab?: (t: 'relasjoner' | 'detaljer') => void;
  /** Én gruppe per relasjonskategori (f.eks. "Regelverksreferanser", "Hjemmel", "Brukt i tjenester")
   * — entitetsuavhengig, kalleren avgjør hvilke grupper og hva hvert klikk skal gjøre. */
  grupper: KontekstPanelGruppe[];
  selectedDetail?: DetaljVisning | null;
  onClearDetail?: () => void;
}

const RAD_STIL: CSSProperties = {
  display: 'block', width: '100%', textAlign: 'left', background: 'none', border: 'none',
  padding: '0.4rem 0.3rem', font: 'inherit', fontSize: 'var(--ds-font-size-1)', cursor: 'pointer',
  borderRadius: 'var(--ds-border-radius-sm)',
};

function GruppeListe({ grupper }: { grupper: KontekstPanelGruppe[] }) {
  return (
    <>
      {grupper.map((gruppe, idx) => (
        <div key={gruppe.heading}>
          <Heading level={3} data-size="2xs" style={{ color: 'var(--ds-color-neutral-text-subtle)', margin: idx === 0 ? '0 0 0.3rem' : '1rem 0 0.3rem' }}>
            {gruppe.heading}
          </Heading>
          {gruppe.items.length === 0 && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>Ingen ennå.</Paragraph>
          )}
          {gruppe.items.map((item) => (
            <button key={item.key} type="button" style={RAD_STIL} onClick={item.onClick}>
              {item.label}
            </button>
          ))}
        </div>
      ))}
    </>
  );
}

/**
 * Høyre kontekstpanel — generalisert (2026-09-02, docs/30 §4 punkt 1 — saksbehandlertilpasningen) fra
 * Tjeneste-spesifikke felt (`flateReferanser`/`rettskilder`/`hendelser`/`avhengigheter`) til en delt,
 * entitetsuavhengig `grupper: { heading, items: {label, onClick} }[]`-form — samme visuelle mønster
 * og atferd som Tjenestedetalj-redesignrunden (2026-08-27) innførte, INGEN visuell endring på
 * Tjeneste-siden av denne generaliseringen; kalleren (`TjenesteDetalj.tsx`, `RettskildeDetalj.tsx`,
 * flere entitetssider etter hvert per docs/30 §4) bygger selv opp gruppene og hva hvert klikk skal gjøre.
 * <p>
 * I `variant="sidebar"` (standard) viser «Relasjoner»-fanen alle grupper i én alltid-synlig liste
 * uansett hvilken hovedfane man står i; «Detaljer»-fanen viser det sist klikkede. Kollapsbart (fast
 * bredde, IKKE dra-i-bredde). I `variant="full"` fylles tilgjengelig bredde og ingen kollaps/Detaljer-
 * fane vises — kalleren har allerede plassert panelet i sin egen fane, og radenes `onClick` navigerer
 * direkte i stedet for å bruke `selectedDetail`.
 */
export function KontekstPanel({
  variant = 'sidebar', collapsed, onToggleCollapsed, rightTab, setRightTab, grupper, selectedDetail, onClearDetail,
}: KontekstPanelProps) {
  if (variant === 'full') {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', width: '100%' }}>
        <GruppeListe grupper={grupper} />
      </div>
    );
  }

  if (collapsed) {
    return (
      <div style={{ flex: '0 0 40px', width: '40px', borderLeft: '1px solid var(--ds-color-neutral-border-subtle)', display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: '10px' }}>
        <Button variant="tertiary" data-size="sm" onClick={onToggleCollapsed} title="Vis kontekstpanel">«</Button>
      </div>
    );
  }

  const aktivFane = rightTab ?? 'relasjoner';

  return (
    <div style={{ flex: '0 0 320px', width: '320px', display: 'flex', flexDirection: 'column', overflow: 'hidden', borderLeft: '1px solid var(--ds-color-neutral-border-subtle)' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0.6rem 0.7rem', borderBottom: '1px solid var(--ds-color-neutral-border-subtle)' }}>
        <Tabs value={aktivFane} onChange={(v) => setRightTab?.(v as 'relasjoner' | 'detaljer')}>
          <Tabs.List>
            <Tabs.Tab value="relasjoner">Relasjoner</Tabs.Tab>
            <Tabs.Tab value="detaljer">Detaljer</Tabs.Tab>
          </Tabs.List>
        </Tabs>
        <Button variant="tertiary" data-size="sm" onClick={onToggleCollapsed} title="Skjul kontekstpanel">«</Button>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: '0.75rem' }}>
        {aktivFane === 'relasjoner' ? (
          <GruppeListe grupper={grupper} />
        ) : selectedDetail ? (
          <>
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.2rem' }}>{selectedDetail.meta}</Paragraph>
            <Heading level={3} data-size="xs" style={{ marginBottom: '0.5rem' }}>{selectedDetail.title}</Heading>
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', lineHeight: 1.6, marginBottom: '0.75rem' }}>
              {selectedDetail.body ?? 'Ingen ytterligere tekst å vise.'}
            </Paragraph>
            <Button variant="secondary" data-size="sm" onClick={onClearDetail}>Lukk</Button>
          </>
        ) : (
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
            Klikk en rad i listen over — eller en §-referanse på et felt — for å se detaljer her.
          </Paragraph>
        )}
      </div>
    </div>
  );
}
