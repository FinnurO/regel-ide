import { Field, Label, Pagination, Paragraph, Select, usePagination as useDsPaginering } from '@digdir/designsystemet-react';
import { SIDESTORRELSER, type Sidestorrelse } from './usePaginering';

export interface PagineringskontrollProps {
  side: number;
  settSide: (side: number) => void;
  sidestorrelse: Sidestorrelse;
  settSidestorrelse: (sidestorrelse: Sidestorrelse) => void;
  totaltAntallSider: number;
  totaltAntallRader: number;
}

/**
 * Sidevelger (antall pr. side) + selve sidenavigasjonen (Designsystemets `Pagination`), rendret
 * sammen som ÉN kontroll under en tabell. Se docs/09-design-konvensjoner.md §9 for mønsteret dette
 * brukes i (fem liste-sider) og `usePaginering.ts` for state-hooken den er bygget på.
 *
 * Skjuler seg selv (returnerer `null`) når det ikke er noe å paginere — én tom side er ikke en
 * paginering.
 */
export function Pagineringskontroll({
  side,
  settSide,
  sidestorrelse,
  settSidestorrelse,
  totaltAntallSider,
  totaltAntallRader,
}: PagineringskontrollProps) {
  const { pages, prevButtonProps, nextButtonProps } = useDsPaginering({
    currentPage: side,
    totalPages: totaltAntallSider,
    setCurrentPage: settSide,
  });

  if (totaltAntallRader === 0) return null;

  const fra = sidestorrelse === 'alle' ? 1 : (side - 1) * sidestorrelse + 1;
  const til = sidestorrelse === 'alle' ? totaltAntallRader : Math.min(side * sidestorrelse, totaltAntallRader);

  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: '1rem',
        marginTop: '1rem',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', flexWrap: 'wrap' }}>
        <Field style={{ minWidth: '9rem' }}>
          <Label>Vis pr. side</Label>
          <Select
            data-size="sm"
            value={String(sidestorrelse)}
            onChange={(e) => settSidestorrelse((e.target.value === 'alle' ? 'alle' : Number(e.target.value)) as Sidestorrelse)}
          >
            {SIDESTORRELSER.map((s) => (
              <Select.Option key={s} value={String(s)}>
                {s === 'alle' ? 'Alle' : s}
              </Select.Option>
            ))}
          </Select>
        </Field>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', margin: 0 }}>
          Viser {fra}–{til} av {totaltAntallRader}
        </Paragraph>
      </div>

      {sidestorrelse !== 'alle' && totaltAntallSider > 1 && (
        <Pagination data-current={String(side)} data-total={String(totaltAntallSider)} aria-label="Bla i sider" data-size="sm">
          <Pagination.List>
            <Pagination.Item>
              <Pagination.Button {...prevButtonProps} aria-label="Forrige side">
                Forrige
              </Pagination.Button>
            </Pagination.Item>
            {pages.map(({ page, itemKey, buttonProps }) => (
              <Pagination.Item key={itemKey}>
                {typeof page === 'number' && (
                  <Pagination.Button {...buttonProps} aria-label={`Side ${page}`}>
                    {page}
                  </Pagination.Button>
                )}
              </Pagination.Item>
            ))}
            <Pagination.Item>
              <Pagination.Button {...nextButtonProps} aria-label="Neste side">
                Neste
              </Pagination.Button>
            </Pagination.Item>
          </Pagination.List>
        </Pagination>
      )}
    </div>
  );
}
