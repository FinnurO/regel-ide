import { Link as RouterLink } from 'react-router';
import { Card, Heading, Link, Paragraph } from '@digdir/designsystemet-react';
import type { RegelnodeDto, TjenesteDto } from '../api/types';
import type { SeksjonNokkel } from '../api/tjenesteFelt';

function StatKort({ etikett, verdi, onClick }: { etikett: string; verdi: number; onClick: () => void }) {
  return (
    <Card style={{ padding: '0.9rem 1rem', cursor: 'pointer' }} onClick={onClick}>
      <div style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>{etikett}</div>
      <div style={{ fontSize: 'var(--ds-font-size-5)', fontWeight: 600, marginTop: '0.1rem' }}>{verdi}</div>
    </Card>
  );
}

export interface OversiktFaneProps {
  tjeneste: TjenesteDto;
  rotnode: RegelnodeDto | null;
  antallReferanser: number;
  antallHendelser: number;
  antallHandlinger: number;
  antallAvhengigheter: number;
  onGaTilFane: (seksjon: SeksjonNokkel) => void;
}

/**
 * Oversikt-fanen (ny, Tjenestedetalj-redesignrunden 2026-08-27) — landingsvisning: metadata,
 * vilkårstre-kobling, fire klikkbare statistikk-kort (→ hopper til riktig fane), beskrivelse. Ren
 * lesevisning — alt data er allerede hentet av siden, ingen egne API-kall her.
 */
export function OversiktFane({
  tjeneste, rotnode, antallReferanser, antallHendelser, antallHandlinger, antallAvhengigheter, onGaTilFane,
}: OversiktFaneProps) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', maxWidth: '900px' }}>
      <Card style={{ padding: '1rem 1.25rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.6rem' }}>Metadata</Heading>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '0.75rem 1.5rem', fontSize: 'var(--ds-font-size-1)' }}>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Kompetent myndighet</div><div>{tjeneste.kompetentMyndighet ?? '—'}</div></div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Rettighetstype</div><div>{tjeneste.type ?? '—'}</div></div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Tjenesteområde</div><div>{tjeneste.tjenesteomrade ?? '—'}</div></div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Behandlingstid</div><div>{tjeneste.behandlingstid ?? '—'}</div></div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.1rem' }}>Kostnad</div><div>{tjeneste.kostnad ?? '—'}</div></div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Output</div><div>{tjeneste.output ?? '—'}</div></div>
        </div>
      </Card>

      <Card style={{ padding: '1rem 1.25rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.4rem' }}>Vilkårstre</Heading>
        {tjeneste.rotnodeId ? (
          <>
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
              Rotnode: {rotnode?.tittel ?? '…'}
            </Paragraph>
            <div style={{ display: 'flex', gap: '0.5rem', fontSize: 'var(--ds-font-size-1)' }}>
              <Link asChild><RouterLink to={`/vilkarstre/${tjeneste.rotnodeId}`}>Åpne vilkårstre →</RouterLink></Link>
              <Link asChild><RouterLink to={`/tjenester/${tjeneste.id}/veiledning`}>Åpne veiledning →</RouterLink></Link>
            </div>
          </>
        ) : (
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>
            Ingen rotnode koblet ennå. <Link asChild><button type="button" onClick={() => onGaTilFane('vilkarstre')} style={{ background: 'none', border: 'none', padding: 0, font: 'inherit', color: 'inherit', textDecoration: 'underline', cursor: 'pointer' }}>Gå til Vilkårstre →</button></Link>
          </Paragraph>
        )}
      </Card>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '0.75rem' }}>
        <StatKort etikett="Regelverksreferanser" verdi={antallReferanser} onClick={() => onGaTilFane('regelverk')} />
        <StatKort etikett="Hendelser" verdi={antallHendelser} onClick={() => onGaTilFane('hendelser')} />
        <StatKort etikett="Handlinger" verdi={antallHandlinger} onClick={() => onGaTilFane('handlinger')} />
        <StatKort etikett="Avhengigheter" verdi={antallAvhengigheter} onClick={() => onGaTilFane('avhengigheter')} />
      </div>

      <Card style={{ padding: '1rem 1.25rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.4rem' }}>Beskrivelse</Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', margin: 0 }}>{tjeneste.beskrivelse ?? 'Ingen beskrivelse registrert ennå.'}</Paragraph>
      </Card>
    </div>
  );
}
