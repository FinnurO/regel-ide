import type { CSSProperties } from 'react';
import { Button, Heading, Paragraph, Tabs } from '@digdir/designsystemet-react';
import { eidVisningstekst } from '../api/eidLenker';
import type { HendelseDto, RettskildeNodeDto, RettskildeSammendrag, TjenesteavhengighetDto, TjenesteRegelverksreferanseDto } from '../api/types';
import type { DetaljVisning } from './detaljVisning';

export interface KontekstPanelProps {
  collapsed: boolean;
  onToggleCollapsed: () => void;
  rightTab: 'relasjoner' | 'detaljer';
  setRightTab: (t: 'relasjoner' | 'detaljer') => void;
  /** Flate (felt === null) regelverksreferanser — samme utvalg som Regelverksreferanser-fanen. */
  flateReferanser: TjenesteRegelverksreferanseDto[];
  rettskilder: RettskildeSammendrag[];
  noderPerRettskilde: Map<string, RettskildeNodeDto[]>;
  hendelser: HendelseDto[];
  avhengigheter: TjenesteavhengighetDto[];
  selectedDetail: DetaljVisning | null;
  onSelectDetail: (v: DetaljVisning) => void;
  onClearDetail: () => void;
}

const RAD_STIL: CSSProperties = {
  display: 'block', width: '100%', textAlign: 'left', background: 'none', border: 'none',
  padding: '0.4rem 0.3rem', font: 'inherit', fontSize: 'var(--ds-font-size-1)', cursor: 'pointer',
  borderRadius: 'var(--ds-border-radius-sm)',
};

/**
 * Høyre kontekstpanel (nytt, Tjenestedetalj-redesignrunden 2026-08-27) — «Relasjoner»-fanen samler
 * Regelverksreferanser+Hendelser+Avhengigheter (IKKE Handlinger, som allerede har egne detaljsider)
 * i én alltid-synlig liste uansett hvilken hovedfane man står i; «Detaljer»-fanen viser det sist
 * klikkede (en relasjonsrad her, ELLER en §-feltreferanse i Innhold-fanen). Kollapsbart (fast
 * bredde, IKKE dra-i-bredde — se plan-notatets scope-avgrensning).
 */
export function KontekstPanel({
  collapsed, onToggleCollapsed, rightTab, setRightTab, flateReferanser, rettskilder, noderPerRettskilde,
  hendelser, avhengigheter, selectedDetail, onSelectDetail, onClearDetail,
}: KontekstPanelProps) {
  if (collapsed) {
    return (
      <div style={{ flex: '0 0 40px', width: '40px', borderLeft: '1px solid var(--ds-color-neutral-border-subtle)', display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: '10px' }}>
        <Button variant="tertiary" data-size="sm" onClick={onToggleCollapsed} title="Vis kontekstpanel">«</Button>
      </div>
    );
  }

  function visReferanse(r: TjenesteRegelverksreferanseDto) {
    const rettskilde = rettskilder.find((rk) => rk.id === r.tilRettskildeId);
    const node = noderPerRettskilde.get(r.tilRettskildeId)?.find((n) => n.eid === r.tilEid);
    onSelectDetail({
      title: eidVisningstekst(r.tilEid, rettskilder, noderPerRettskilde) ?? r.tilEid,
      meta: rettskilde ? (rettskilde.kortnavn ?? rettskilde.tittel) : 'Regelverksreferanse',
      body: node?.tekst ?? null,
    });
  }

  return (
    <div style={{ flex: '0 0 320px', width: '320px', display: 'flex', flexDirection: 'column', overflow: 'hidden', borderLeft: '1px solid var(--ds-color-neutral-border-subtle)' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0.6rem 0.7rem', borderBottom: '1px solid var(--ds-color-neutral-border-subtle)' }}>
        <Tabs value={rightTab} onChange={(v) => setRightTab(v as 'relasjoner' | 'detaljer')}>
          <Tabs.List>
            <Tabs.Tab value="relasjoner">Relasjoner</Tabs.Tab>
            <Tabs.Tab value="detaljer">Detaljer</Tabs.Tab>
          </Tabs.List>
        </Tabs>
        <Button variant="tertiary" data-size="sm" onClick={onToggleCollapsed} title="Skjul kontekstpanel">«</Button>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: '0.75rem' }}>
        {rightTab === 'relasjoner' ? (
          <>
            <Heading level={3} data-size="2xs" style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.3rem' }}>Regelverksreferanser</Heading>
            {flateReferanser.length === 0 && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>Ingen ennå.</Paragraph>}
            {flateReferanser.map((r) => (
              <button key={r.id} type="button" style={RAD_STIL} onClick={() => visReferanse(r)}>
                {eidVisningstekst(r.tilEid, rettskilder, noderPerRettskilde) ?? r.tilEid}
              </button>
            ))}

            <Heading level={3} data-size="2xs" style={{ color: 'var(--ds-color-neutral-text-subtle)', margin: '1rem 0 0.3rem' }}>Hendelser</Heading>
            {hendelser.length === 0 && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>Ingen ennå.</Paragraph>}
            {hendelser.map((h) => (
              <button key={h.id} type="button" style={RAD_STIL}
                onClick={() => onSelectDetail({ title: h.navn, meta: `Hendelse · ${h.type}`, body: h.beskrivelse })}>
                {h.navn}
              </button>
            ))}

            <Heading level={3} data-size="2xs" style={{ color: 'var(--ds-color-neutral-text-subtle)', margin: '1rem 0 0.3rem' }}>Avhengigheter</Heading>
            {avhengigheter.length === 0 && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>Ingen ennå.</Paragraph>}
            {avhengigheter.map((a) => (
              <button key={a.id} type="button" style={RAD_STIL}
                onClick={() => onSelectDetail({ title: a.visningstekst, meta: `Avhengighet · ${a.rel}`, body: a.beskrivelse })}>
                {a.visningstekst}
              </button>
            ))}
          </>
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
            Klikk en rad i Regelverk, Hendelser eller Avhengigheter — eller en §-referanse på et felt — for å se detaljer her.
          </Paragraph>
        )}
      </div>
    </div>
  );
}
