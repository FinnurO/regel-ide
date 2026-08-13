import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Checkbox, Heading, Link, Paragraph, Table, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RettskildeSammendrag } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

type Sorteringskolonne = 'tittel' | 'kildetype' | 'eier';

export default function RettskilderListe() {
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kunMine, setKunMine] = useState(false);
  const [filterTekst, setFilterTekst] = useState('');
  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('tittel');
  const [sortStigende, setSortStigende] = useState(true);
  const { gjeldendeBruker } = useBruker();
  const { visEier } = useVirksomheter();

  useEffect(() => {
    setFeil(null);
    setRettskilder(null);
    const virksomhetId = kunMine && gjeldendeBruker ? gjeldendeBruker.virksomhetId : undefined;
    api
      .hentRettskilder(virksomhetId)
      .then(setRettskilder)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av rettskilder.'));
  }, [kunMine, gjeldendeBruker]);

  function bytteSortering(kolonne: Sorteringskolonne) {
    if (sortKolonne === kolonne) setSortStigende((s) => !s);
    else {
      setSortKolonne(kolonne);
      setSortStigende(true);
    }
  }

  const viste = useMemo(() => {
    if (!rettskilder) return null;
    const tekst = filterTekst.trim().toLowerCase();
    const filtrert = tekst
      ? rettskilder.filter(
          (r) =>
            r.tittel.toLowerCase().includes(tekst) ||
            (r.kortnavn?.toLowerCase().includes(tekst) ?? false) ||
            r.kildetype.toLowerCase().includes(tekst) ||
            visEier(r.virksomhetId).toLowerCase().includes(tekst),
        )
      : rettskilder;

    const sortnokkel = (r: RettskildeSammendrag) =>
      sortKolonne === 'tittel'
        ? (r.kortnavn ?? r.tittel)
        : sortKolonne === 'kildetype'
          ? r.kildetype
          : visEier(r.virksomhetId);

    return [...filtrert].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
  }, [rettskilder, filterTekst, sortKolonne, sortStigende, visEier]);

  function sorteringsindikator(kolonne: Sorteringskolonne) {
    if (sortKolonne !== kolonne) return '';
    return sortStigende ? ' ▲' : ' ▼';
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Rettskilder
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Åpne data — delte/nasjonale kilder (Lov/Forskrift fra Lovdata) og alle virksomheters
        publiserte lokale kilder. Kladder (status «Utkast») vises aldri her.
      </Paragraph>

      <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-end', marginBottom: '1rem', flexWrap: 'wrap' }}>
        {gjeldendeBruker && (
          <Checkbox
            label={`Vis kun ${gjeldendeBruker.virksomhetNavn} sine egne kilder`}
            checked={kunMine}
            onChange={(e) => setKunMine(e.target.checked)}
          />
        )}
        <Textfield
          label="Filtrer"
          placeholder="Tittel, kildetype eller eier"
          value={filterTekst}
          onChange={(e) => setFilterTekst(e.target.value)}
          style={{ maxWidth: '20rem' }}
        />
      </div>

      {feil && <div className="feilmelding">{feil}</div>}

      {!rettskilder && !feil && <Paragraph>Laster …</Paragraph>}

      {viste && viste.length === 0 && <Paragraph>Ingen rettskilder funnet.</Paragraph>}

      {viste && viste.length > 0 && (
        <Table className="rettskilde-tabell" border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('tittel')}>
                  Tittel{sorteringsindikator('tittel')}
                </button>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('kildetype')}>
                  Kildetype{sorteringsindikator('kildetype')}
                </button>
              </Table.HeaderCell>
              <Table.HeaderCell>ELI</Table.HeaderCell>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('eier')}>
                  Eier{sorteringsindikator('eier')}
                </button>
              </Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {viste.map((r) => (
              <Table.Row key={r.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/rettskilder/${r.id}`}>{r.kortnavn ?? r.tittel}</RouterLink>
                  </Link>
                </Table.Cell>
                <Table.Cell>{r.kildetype}</Table.Cell>
                <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{r.eli ?? '—'}</Table.Cell>
                <Table.Cell>{visEier(r.virksomhetId)}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
