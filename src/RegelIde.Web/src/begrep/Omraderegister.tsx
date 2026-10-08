import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Card, Heading, Link, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { KommuneTilhorighetDto, Omradetype, StrukturkantDto, TilhorighetsrubrikkDto } from '../api/types';
import { StrukturkantTabell } from '../strukturkant/StrukturkantTabell';
import { Metatekst } from '../entitet/Metatekst';

/**
 * [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08] Det et REGISTRERT område (fylke, kommune,
 * tettsted, lagsogn, lagdømme, helseregion) skal svare på (docs/32 S8, docs/33 §6.1 tommelfingerregel 5 og 10):
 * hva består det av, hva inngår det i, hvem har ansvar der — og for en kommune: hvilket fylke, hvilken tingrett,
 * lagdømme, statsforvalter og RHF den hører til.
 *
 * Kantene vises med den delte `StrukturkantTabell` (docs/09 §31) — ingen lokal kanttabell. Tilhørigheten er en
 * egen, kompakt tabell fordi den er et BEREGNET svar (fra kantene), ikke kanter: hver rubrikk viser kandidatene
 * og, når det ikke er nøyaktig én, «Ikke entydig» (`warning`) eller «Mangler» (`neutral`) — ingenting velges
 * (CLAUDE.md §8, samme regel #314 skal bruke).
 *
 * Seksjonsmønster som resten av BegrepDetalj (docs/09 §16): `Card` alltid rendret med tom-tilstand inni,
 * `null` = laster ⇒ `Spinner`.
 */
export const OMRADETYPE_VISNING: Record<Omradetype, string> = {
  fylke: 'Fylke',
  kommune: 'Kommune (territorium)',
  tettsted: 'Tettsted',
  lagsogn: 'Lagsogn',
  lagdomme: 'Lagdømme',
  helseregion: 'Helseregion',
  annet: 'Annet område',
};

/** Rubrikknavn slik de vises — samme rekkefølge som tjenesten gir dem. */
const RUBRIKK_VISNING: Record<string, string> = {
  kommune: 'Kommunen (rettssubjekt)',
  fylke: 'Fylke',
  tingrett: 'Tingrett',
  lagsogn: 'Lagsogn',
  'lagdømme': 'Lagdømme',
  lagmannsrett: 'Lagmannsrett',
  statsforvalter: 'Statsforvalter',
  helseregion: 'Helseregion',
  RHF: 'Regionalt helseforetak',
};

/** Rubrikker der kandidatene er OMRÅDER (lenke til begrepet); de andre er virksomheter. */
const OMRADERUBRIKKER = new Set(['fylke', 'lagsogn', 'lagdømme', 'helseregion']);

export function OmradetypeTag({ omradetype, omradekode }: { omradetype: Omradetype; omradekode?: string | null }) {
  return (
    <Tag data-color="neutral" data-size="sm" title="Områdetype og kode i det autoritative registeret (issue #312).">
      {OMRADETYPE_VISNING[omradetype] ?? omradetype}{omradekode ? ` ${omradekode}` : ''}
    </Tag>
  );
}

function Rubrikkstatus({ r }: { r: TilhorighetsrubrikkDto }) {
  if (r.status === 'entydig') return null;
  return r.status === 'ikke_entydig'
    ? <Tag data-color="warning" data-size="sm" title="Flere kandidater — ingen er valgt.">Ikke entydig</Tag>
    : <Tag data-color="neutral" data-size="sm" title="Ingen kant i registeret gir et svar.">Mangler</Tag>;
}

export function Omraderegister({ omradeId, omradetype }: { omradeId: string; omradetype: Omradetype }) {
  const [kanter, setKanter] = useState<StrukturkantDto[] | null>(null);
  const [tilhorighet, setTilhorighet] = useState<KommuneTilhorighetDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    setKanter(null);
    setTilhorighet(null);
    api.hentStrukturkanter({ begrepId: omradeId }).then(setKanter).catch(() => setKanter([]));
    if (omradetype === 'kommune') {
      api.hentOmradeTilhorighet(omradeId).then(setTilhorighet)
        .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved oppslag av tilhørighet.'));
    }
  }, [omradeId, omradetype]);

  const sammensetning = kanter && kanter.filter((k) => k.kategori === 'O' && k.retning === 'fra');
  const inngarI = kanter && kanter.filter((k) => k.kategori === 'O' && k.retning === 'til');
  const ansvar = kanter && kanter.filter((k) => k.kategori === 'A' && k.retning === 'til');
  const ovrige = kanter && kanter.filter((k) => k.kategori !== 'O' && k.kategori !== 'A');

  return (
    <>
      {omradetype === 'kommune' && (
        <section style={{ marginBottom: '2rem' }}>
          <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>Tilhørighet</Heading>
          <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
            Beregnet fra kantene under og i de overordnede områdene: fylke og helseregion fra Kartverket og
            RHF-vedtektene, tingrett, lagsogn og lagdømme fra forskrift om inndelingen av rettskretser og lagdømmer,
            statsforvalteren fra embetsinndelingen. Er det ikke nøyaktig én kandidat, velges ingen.
          </Metatekst>
          {feil && <Alert data-color="danger" data-size="sm">{feil}</Alert>}
          <Card style={{ padding: tilhorighet ? 0 : '1rem', overflow: 'hidden', marginBottom: '0.75rem' }}>
            {!tilhorighet && !feil && <Spinner aria-label="Laster …" data-size="sm" />}
            {tilhorighet && (
              <Table data-density="compact">
                <Table.Body>
                  {tilhorighet.rubrikker.map((r) => (
                    <Table.Row key={r.rubrikk}>
                      <Table.HeaderCell style={{ width: '14rem' }}>{RUBRIKK_VISNING[r.rubrikk] ?? r.rubrikk}</Table.HeaderCell>
                      <Table.Cell>
                        <span style={{ display: 'inline-flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
                          {r.kandidater.map((k, i) => (
                            <span key={k.id}>
                              <Link asChild>
                                <RouterLink to={OMRADERUBRIKKER.has(r.rubrikk) ? `/begreper/${k.id}` : `/virksomheter/${k.id}`}>{k.navn}</RouterLink>
                              </Link>
                              {i < r.kandidater.length - 1 ? ',' : ''}
                            </span>
                          ))}
                          {r.kandidater.length === 0 && '—'}
                          <Rubrikkstatus r={r} />
                        </span>
                      </Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
            )}
          </Card>
        </section>
      )}

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>Hvem har ansvar her</Heading>
        <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Aktører med ansvarsområde i (eller sete i) nettopp dette området. Ansvar i et overordnet område (fylket,
          lagdømmet) står på det områdets side — og i tilhørigheten over for en kommune.
        </Metatekst>
        <StrukturkantTabell kanter={ansvar} tomTekst="Ingen aktør har ansvarsområde direkte i dette området." />
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>Består av</Heading>
        <StrukturkantTabell kanter={sammensetning} tomTekst="Området er ikke registrert med delområder." />
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>Inngår i</Heading>
        <StrukturkantTabell kanter={inngarI} tomTekst="Området er ikke registrert som del av et annet område." />
      </section>

      {ovrige && ovrige.length > 0 && (
        <section style={{ marginBottom: '2rem' }}>
          <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>Andre strukturutsagn</Heading>
          <StrukturkantTabell kanter={ovrige} tomTekst="" visKategori />
        </section>
      )}
    </>
  );
}
