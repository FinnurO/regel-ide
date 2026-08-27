import { useMemo, type Dispatch, type SetStateAction } from 'react';
import { Link as RouterLink } from 'react-router';
import { Button, Heading, Link, Paragraph, Spinner } from '@digdir/designsystemet-react';
import { api } from '../api/client';
import { eidVisningstekst, rettskildeLenke } from '../api/eidLenker';
import type { RettskildeNodeDto, RettskildeSammendrag, TjenesteRegelverksreferanseDto } from '../api/types';
import { KobleRegelverksreferanseForm } from '../rettskilde/KobleRegelverksreferanseForm';
import type { DetaljVisning } from './detaljVisning';

export interface RegelverkFaneProps {
  tjenesteId: string;
  /** ALLE referanser (både flate og felt-koblede) — denne fanen filtrerer selv til `felt === null`. */
  referanser: TjenesteRegelverksreferanseDto[] | null;
  setReferanser: Dispatch<SetStateAction<TjenesteRegelverksreferanseDto[] | null>>;
  rettskilder: RettskildeSammendrag[];
  noderPerRettskilde: Map<string, RettskildeNodeDto[]>;
  sikreNoderFor: (rettskildeId: string) => void;
  onSelectDetail: (v: DetaljVisning) => void;
}

/** Den flate, hele-tjenesten-regelverksreferanselisten (felt === null) — feltnivå-referanser vises
 * i stedet inline på sine respektive felt i Innhold-fanen. Samme "Koble ny referanse"-form som
 * feltene bruker, delt via `KobleRegelverksreferanseForm`. */
export function RegelverkFane({
  tjenesteId, referanser, setReferanser, rettskilder, noderPerRettskilde, sikreNoderFor, onSelectDetail,
}: RegelverkFaneProps) {
  const flate = useMemo(() => (referanser ?? []).filter((r) => r.felt === null), [referanser]);

  const gruppert = useMemo(() => {
    const kart = new Map<string, TjenesteRegelverksreferanseDto[]>();
    for (const r of flate) {
      const liste = kart.get(r.tilRettskildeId) ?? [];
      liste.push(r);
      kart.set(r.tilRettskildeId, liste);
    }
    return [...kart.entries()];
  }, [flate]);

  async function fjern(referanseId: string) {
    await api.fjernTjenesteRegelverksreferanse(referanseId);
    setReferanser((forrige) => (forrige ?? []).filter((r) => r.id !== referanseId));
  }

  function visDetalj(r: TjenesteRegelverksreferanseDto, rettskilde: RettskildeSammendrag | undefined) {
    const node = noderPerRettskilde.get(r.tilRettskildeId)?.find((n) => n.eid === r.tilEid);
    onSelectDetail({
      title: eidVisningstekst(r.tilEid, rettskilder, noderPerRettskilde) ?? r.tilEid,
      meta: rettskilde ? (rettskilde.kortnavn ?? rettskilde.tittel) : 'Regelverksreferanse',
      body: node?.tekst ?? null,
    });
  }

  return (
    <div style={{ maxWidth: '760px' }}>
      <Heading level={2} data-size="xs" style={{ marginBottom: '0.75rem' }}>Regelverksreferanser</Heading>
      {referanser === null && <Spinner aria-label="Laster …" data-size="sm" />}
      {referanser && flate.length === 0 && <Paragraph>Ingen regelverksreferanser koblet ennå.</Paragraph>}
      {referanser && flate.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', marginBottom: '1.25rem' }}>
          {gruppert.map(([tilRettskildeId, rader]) => {
            const rettskilde = rettskilder.find((r) => r.id === tilRettskildeId);
            return (
              <div key={tilRettskildeId}>
                <Heading level={3} data-size="xs" style={{ marginBottom: '0.3rem' }}>
                  {rettskilde ? (rettskilde.kortnavn ?? rettskilde.tittel) : tilRettskildeId}
                </Heading>
                <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
                  {rader.map((r) => {
                    const visningstekst = eidVisningstekst(r.tilEid, rettskilder, noderPerRettskilde);
                    const href = rettskildeLenke(r.tilEid, rettskilder);
                    return (
                      <li key={r.id} style={{ fontSize: 'var(--ds-font-size-1)', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                        <button type="button" onClick={() => visDetalj(r, rettskilde)}
                          style={{ background: 'none', border: 'none', padding: 0, font: 'inherit', color: 'var(--ds-color-accent-text-default)', cursor: 'pointer', textAlign: 'left' }}>
                          {visningstekst ?? r.tilEid}
                        </button>
                        {href && <Link asChild style={{ fontSize: 'var(--ds-font-size-1)' }}><RouterLink to={href}>↗</RouterLink></Link>}
                        <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjern(r.id)}>Fjern</Button>
                      </li>
                    );
                  })}
                </ul>
              </div>
            );
          })}
        </div>
      )}

      <KobleRegelverksreferanseForm
        tjenesteId={tjenesteId}
        rettskilder={rettskilder}
        noderPerRettskilde={noderPerRettskilde}
        sikreNoderFor={sikreNoderFor}
        onKoblet={(ny) => setReferanser((forrige) => [...(forrige ?? []), ny])}
      />
    </div>
  );
}
