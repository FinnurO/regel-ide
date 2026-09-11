import { Link as RouterLink } from 'react-router';
import { Card, Heading, Link } from '@digdir/designsystemet-react';
import type { RegelnodeDto, TjenesteDto } from '../api/types';
import type { SeksjonNokkel } from '../api/tjenesteFelt';
import { Metatekst } from '../entitet/Metatekst';

function StatKort({ etikett, verdi, onClick }: { etikett: string; verdi: number; onClick: () => void }) {
  return (
    <Card style={{ padding: '0.9rem 1rem', cursor: 'pointer' }} onClick={onClick}>
      <Metatekst as="div" style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>{etikett}</Metatekst>
      <div style={{ fontSize: 'var(--ds-font-size-5)', fontWeight: 600, marginTop: '0.1rem' }}>{verdi}</div>
    </Card>
  );
}

export interface OversiktFaneProps {
  tjeneste: TjenesteDto;
  /**
   * [Ny, 2026-09-09, issue #138] Visningsnavnet til virksomheten som EIER tjenesten
   * (`tjeneste.virksomhetId`), hentet fra `useVirksomheter().visEier` — som ALDRI finner opp et
   * navn: er iden ukjent, får vi den rå GUID-en tilbake. `null` betyr «lister fortsatt», og skal
   * ikke vises som et svar (docs/09 §15: en påstand skal ikke vises mens data lastes).
   *
   * <p>Johann: «hvor er koblingen til en virksomhet (med eller uten org.nummer)? kompetent
   * myndighet er fritekst.» Koblingen FINNES — `TjenesteEntitet.VirksomhetId` er ikke-nullbar og
   * peker på eier-virksomheten — den ble bare ikke vist noe sted. Det er «Kompetent myndighet»
   * som er fritekst, og den skal FORTSATT være det: en FK-konvertering av det feltet er bevisst
   * utsatt (docs/13-backlog.md §8, docs/23 §6), fordi kompetent myndighet i en importert
   * rettighetsmodell ofte er et organ vi ikke har i katalogen. To ulike ting, som issuet selv
   * påpeker — eieren er en FK, myndigheten er tekst.</p>
   */
  eierNavn: string | null;
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
  tjeneste, eierNavn, rotnode, antallReferanser, antallHendelser, antallHandlinger, antallAvhengigheter, onGaTilFane,
}: OversiktFaneProps) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', maxWidth: '900px' }}>
      <Card style={{ padding: '1rem 1.25rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.6rem' }}>Metadata</Heading>
        <Metatekst as="div" style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '0.75rem 1.5rem' }}>
          {/* [Ny, 2026-09-09, issue #138] Eier-virksomheten, ØVERST og med lenke: det er den
            * faktiske koblingen til katalogen, og den avgjør hvem som ser og kan endre tjenesten.
            * Står før «Kompetent myndighet» nettopp fordi de to blandes — se `eierNavn`. */}
          <div>
            <div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Eier (virksomhet)</div>
            <div>
              {eierNavn === null
                ? <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Laster …</span>
                : (
                  <Link asChild>
                    <RouterLink to={`/virksomheter/${tjeneste.virksomhetId}`}>
                      {eierNavn} ↗
                    </RouterLink>
                  </Link>
                )}
            </div>
          </div>
          <div>
            <div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Kompetent myndighet</div>
            {/* Fritekst, bevisst — se `eierNavn`-kommentaren. Skal ikke forveksles med eieren over. */}
            <div>{tjeneste.kompetentMyndighet ?? '—'}</div>
          </div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Rettighetstype</div><div>{tjeneste.type ?? '—'}</div></div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Tjenesteområde</div><div>{tjeneste.tjenesteomrade ?? '—'}</div></div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Behandlingstid</div><div>{tjeneste.behandlingstid ?? '—'}</div></div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.1rem' }}>Kostnad</div><div>{tjeneste.kostnad ?? '—'}</div></div>
          <div><div style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Output</div><div>{tjeneste.output ?? '—'}</div></div>
        </Metatekst>
      </Card>

      <Card style={{ padding: '1rem 1.25rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.4rem' }}>Vilkårstre</Heading>
        {tjeneste.rotnodeId ? (
          <>
            <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
              Rotnode: {rotnode?.tittel ?? '…'}
            </Metatekst>
            <Metatekst as="div" style={{ display: 'flex', gap: '0.5rem' }}>
              <Link asChild><RouterLink to={`/vilkarstre/${tjeneste.rotnodeId}`}>Åpne vilkårstre →</RouterLink></Link>
              <Link asChild><RouterLink to={`/tjenester/${tjeneste.id}/veiledning`}>Åpne veiledning →</RouterLink></Link>
            </Metatekst>
          </>
        ) : (
          <Metatekst>
            Ingen rotnode koblet ennå. <Link asChild><button type="button" onClick={() => onGaTilFane('vilkarstre')} style={{ background: 'none', border: 'none', padding: 0, font: 'inherit', color: 'inherit', textDecoration: 'underline', cursor: 'pointer' }}>Gå til Vilkårstre →</button></Link>
          </Metatekst>
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
        <Metatekst style={{ margin: 0 }}>{tjeneste.beskrivelse ?? 'Ingen beskrivelse registrert ennå.'}</Metatekst>
      </Card>
    </div>
  );
}
