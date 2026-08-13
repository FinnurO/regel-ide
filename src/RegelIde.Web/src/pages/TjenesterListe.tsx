import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Button, Heading, Link, Paragraph, Table, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { TjenesteDto } from '../api/types';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

type Sorteringskolonne = 'tittel' | 'tjenestetype' | 'status' | 'eier';

export default function TjenesterListe() {
  const navigate = useNavigate();
  const [tjenester, setTjenester] = useState<TjenesteDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [nyTittel, setNyTittel] = useState('');
  const [oppretterFeil, setOppretterFeil] = useState<string | null>(null);
  const [oppretter, setOppretter] = useState(false);
  const [filterTekst, setFilterTekst] = useState('');
  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('tittel');
  const [sortStigende, setSortStigende] = useState(true);
  const { visEier } = useVirksomheter();

  useEffect(() => {
    api
      .hentTjenester()
      .then(setTjenester)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av tjenester.'));
  }, []);

  async function opprett(e: FormEvent) {
    e.preventDefault();
    setOppretterFeil(null);
    setOppretter(true);
    try {
      const tjeneste = await api.opprettTjeneste({
        tittel: nyTittel.trim(), beskrivelse: null, kompetentMyndighet: null, output: null,
        tjenestetype: null, malgruppe: null, kanaler: null, kostnad: null, behandlingstid: null,
        kontaktpunkt: null, konsekvensVedBrudd: null, sprak: null,
      });
      navigate(`/tjenester/${tjeneste.id}`);
    } catch (err) {
      setOppretterFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av tjeneste.');
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
    if (!tjenester) return null;
    const tekst = filterTekst.trim().toLowerCase();
    const filtrert = tekst
      ? tjenester.filter(
          (t) =>
            t.tittel.toLowerCase().includes(tekst) ||
            (t.tjenestetype?.toLowerCase().includes(tekst) ?? false) ||
            t.status.toLowerCase().includes(tekst) ||
            visEier(t.virksomhetId).toLowerCase().includes(tekst),
        )
      : tjenester;

    const sortnokkel = (t: TjenesteDto) =>
      sortKolonne === 'tittel'
        ? t.tittel
        : sortKolonne === 'tjenestetype'
          ? (t.tjenestetype ?? '')
          : sortKolonne === 'status'
            ? t.status
            : visEier(t.virksomhetId);

    return [...filtrert].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
  }, [tjenester, filterTekst, sortKolonne, sortStigende, visEier]);

  function sorteringsindikator(kolonne: Sorteringskolonne) {
    if (sortKolonne !== kolonne) return '';
    return sortStigende ? ' ▲' : ' ▼';
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Tjenester
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Tjenestedefinisjoner (CPSV-AP-NO, produktkrav kap. 3.2) — virksomhetens eget arbeidsprodukt.
      </Paragraph>

      <form onSubmit={opprett} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '1.5rem' }}>
        <Textfield label="Ny tjeneste" placeholder="f.eks. Alminnelig skjenkebevilling" value={nyTittel}
          onChange={(e) => setNyTittel(e.target.value)} required />
        <Button type="submit" disabled={oppretter || !nyTittel.trim()}>
          {oppretter ? 'Oppretter …' : 'Opprett'}
        </Button>
      </form>
      {oppretterFeil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{oppretterFeil}</div>}

      <Textfield
        label="Filtrer"
        placeholder="Tittel, tjenestetype, status eller eier"
        value={filterTekst}
        onChange={(e) => setFilterTekst(e.target.value)}
        style={{ maxWidth: '20rem', marginBottom: '1rem' }}
      />

      {feil && <div className="feilmelding">{feil}</div>}
      {!tjenester && !feil && <Paragraph>Laster …</Paragraph>}
      {viste && viste.length === 0 && <Paragraph>Ingen tjenester funnet.</Paragraph>}

      {viste && viste.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('tittel')}>
                  Tittel{sorteringsindikator('tittel')}
                </button>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('tjenestetype')}>
                  Tjenestetype{sorteringsindikator('tjenestetype')}
                </button>
              </Table.HeaderCell>
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
            {viste.map((t) => (
              <Table.Row key={t.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/tjenester/${t.id}`}>{t.tittel}</RouterLink>
                  </Link>
                </Table.Cell>
                <Table.Cell>{t.tjenestetype ?? '—'}</Table.Cell>
                <Table.Cell>{t.status}</Table.Cell>
                <Table.Cell>{visEier(t.virksomhetId)}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
