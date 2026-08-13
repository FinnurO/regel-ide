import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Button, Field, Heading, Label, Link, Paragraph, Select, Table, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { BegrepDto } from '../api/types';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

type Sorteringskolonne = 'term' | 'begrepstype' | 'status' | 'eier';

export default function BegreperListe() {
  const navigate = useNavigate();
  const [begreper, setBegreper] = useState<BegrepDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [nyTerm, setNyTerm] = useState('');
  const [nyBegrepstype, setNyBegrepstype] = useState('faktabegrep');
  const [oppretterFeil, setOppretterFeil] = useState<string | null>(null);
  const [oppretter, setOppretter] = useState(false);
  const [filterTekst, setFilterTekst] = useState('');
  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('term');
  const [sortStigende, setSortStigende] = useState(true);
  const { visEier } = useVirksomheter();

  useEffect(() => {
    api
      .hentBegreper()
      .then(setBegreper)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av begreper.'));
  }, []);

  async function opprett(e: FormEvent) {
    e.preventDefault();
    setOppretterFeil(null);
    setOppretter(true);
    try {
      const begrep = await api.opprettBegrep({
        term: nyTerm.trim(), definisjon: '(fyll ut)', lovreferanseEid: null, gjelderFor: null,
        kodelisteReferanseId: null, skosUrl: null, begrepstype: nyBegrepstype,
      });
      navigate(`/begreper/${begrep.id}`);
    } catch (err) {
      setOppretterFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av begrep.');
    } finally {
      setOppretter(false);
    }
  }

  function bytteSortering(kolonne: Sorteringskolonne) {
    if (sortKolonne === kolonne) setSortStigende((s) => !s);
    else {
      setSortKolonne(kolonne);
      setSortStigende(true);
    }
  }

  const viste = useMemo(() => {
    if (!begreper) return null;
    const tekst = filterTekst.trim().toLowerCase();
    const filtrert = tekst
      ? begreper.filter(
          (b) =>
            b.term.toLowerCase().includes(tekst) ||
            b.begrepstype.toLowerCase().includes(tekst) ||
            b.status.toLowerCase().includes(tekst) ||
            visEier(b.virksomhetId).toLowerCase().includes(tekst),
        )
      : begreper;

    const sortnokkel = (b: BegrepDto) =>
      sortKolonne === 'term'
        ? b.term
        : sortKolonne === 'begrepstype'
          ? b.begrepstype
          : sortKolonne === 'status'
            ? b.status
            : visEier(b.virksomhetId);

    return [...filtrert].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
  }, [begreper, filterTekst, sortKolonne, sortStigende, visEier]);

  function sorteringsindikator(kolonne: Sorteringskolonne) {
    if (sortKolonne !== kolonne) return '';
    return sortStigende ? ' ▲' : ' ▼';
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Begreper
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Begrepsregister (SKOS, produktkrav kap. 3.8) — begrepskartlegging tar utgangspunkt i eksisterende rettskilder.
      </Paragraph>

      <form onSubmit={opprett} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '1.5rem' }}>
        <Textfield label="Ny term" placeholder="f.eks. uklanderlig vandel" value={nyTerm}
          onChange={(e) => setNyTerm(e.target.value)} required />
        <Field>
          <Label>Begrepstype</Label>
          <Select value={nyBegrepstype} onChange={(e) => setNyBegrepstype(e.target.value)}>
            <Select.Option value="faktabegrep">Faktabegrep</Select.Option>
            <Select.Option value="handlingsbegrep">Handlingsbegrep</Select.Option>
          </Select>
        </Field>
        <Button type="submit" disabled={oppretter || !nyTerm.trim()}>
          {oppretter ? 'Oppretter …' : 'Opprett'}
        </Button>
      </form>
      {oppretterFeil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{oppretterFeil}</div>}

      <Textfield
        label="Filtrer"
        placeholder="Term, begrepstype, status eller eier"
        value={filterTekst}
        onChange={(e) => setFilterTekst(e.target.value)}
        style={{ maxWidth: '20rem', marginBottom: '1rem' }}
      />

      {feil && <div className="feilmelding">{feil}</div>}
      {!begreper && !feil && <Paragraph>Laster …</Paragraph>}
      {viste && viste.length === 0 && <Paragraph>Ingen begreper funnet.</Paragraph>}

      {viste && viste.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('term')}>
                  Term{sorteringsindikator('term')}
                </button>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('begrepstype')}>
                  Begrepstype{sorteringsindikator('begrepstype')}
                </button>
              </Table.HeaderCell>
              <Table.HeaderCell>Lovreferanse</Table.HeaderCell>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('status')}>
                  Status{sorteringsindikator('status')}
                </button>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('eier')}>
                  Eier{sorteringsindikator('eier')}
                </button>
              </Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {viste.map((b) => (
              <Table.Row key={b.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/begreper/${b.id}`}>{b.term}</RouterLink>
                  </Link>
                </Table.Cell>
                <Table.Cell>{b.begrepstype}</Table.Cell>
                <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{b.lovreferanseEid ?? '—'}</Table.Cell>
                <Table.Cell>{b.status}</Table.Cell>
                <Table.Cell>{visEier(b.virksomhetId)}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
