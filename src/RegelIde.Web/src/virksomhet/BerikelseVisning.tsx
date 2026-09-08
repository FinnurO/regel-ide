import { Link, Tag } from '@digdir/designsystemet-react';
import type { NavnekandidatDto } from '../api/types';

/**
 * SNL/SSR-berikelsen for en navnekandidat (docs/31-navneform-berikelse-snl-ssr-spesifikasjon.md) —
 * slås opp server-side fra en ekstern-oppslag-cache på LESETIDSPUNKTET, ikke lagret på kandidatraden.
 *
 * [FLYTTET hit, navnekandidat-wizard-runden, 2026-09-07] Bodde tidligere som en lokal funksjon i
 * `pages/NavnekandidaterListe.tsx`. Wizarden (`pages/NavnekandidatVeiviser.tsx`) trenger nøyaktig
 * samme visning i sitt kontekst-steg, og en side skal ikke importere en komponent fra en annen SIDE
 * — flyttet til delt mappe i stedet for duplisert, samme mønster som `VirksomhetVelger`.
 * Innholdet er UENDRET fra originalen.
 */
export function BerikelseVisning({ k }: { k: NavnekandidatDto }) {
  return (
    <div style={{ marginTop: '0.3rem', display: 'flex', gap: '0.35rem', flexWrap: 'wrap', alignItems: 'center' }}>
      {k.snlUrl ? (
        <>
          <Link href={k.snlUrl} target="_blank" rel="noreferrer" data-size="sm">
            <Tag data-color="success" data-size="sm">
              SNL{k.snlOrganisasjonsnummer ? ` · org.nr. ${k.snlOrganisasjonsnummer}` : ''} ↗
            </Tag>
          </Link>
          {k.snlAlias && k.snlAlias.length > 0 && (
            <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
              også kjent som: {k.snlAlias.join(', ')}
            </span>
          )}
        </>
      ) : k.ssrBekreftetStedsnavn ? (
        <Tag data-color="info" data-size="sm">
          SSR-bekreftet stedsnavn{k.ssrObjektType ? ` (${k.ssrObjektType})` : ''}
        </Tag>
      ) : (
        <Tag data-color="neutral" data-size="sm">Ukjent i SNL/SSR — lav tillit</Tag>
      )}
    </div>
  );
}
