import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Alert, Button, Card, Heading, Link, Paragraph, Spinner, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { TjenesteDto } from '../api/types';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

type Sorteringskolonne = 'tittel' | 'tjenestetype' | 'status' | 'eier';

/** Samme 6 statusverdier som backend (TjenesteregisterTjeneste), farge+visningstekst for Tag. */
const STATUS_VISNING: Record<string, { farge: 'neutral' | 'warning' | 'info' | 'success' | 'danger'; tekst: string }> = {
  utkast: { farge: 'neutral', tekst: 'Utkast' },
  under_revisjon: { farge: 'warning', tekst: 'Under revisjon' },
  validert: { farge: 'info', tekst: 'Validert' },
  publisert: { farge: 'success', tekst: 'Publisert' },
  tilbaketrukket: { farge: 'danger', tekst: 'Tilbaketrukket' },
  arkivert: { farge: 'neutral', tekst: 'Arkivert' },
};

/**
 * KPI-rad (2026-08-20, "Startside Alternativ 1c") — kun tall vi faktisk kan bekrefte er riktige.
 * Mock-en viste også «Til godkjenning»/«Håndbøker i arbeid», men ingen av dem har en reell
 * datakilde ennå (ingen "til_godkjenning"-status finnes, håndbøker har ikke en egen fremdrifts-
 * status i DTO-en) — utelatt heller enn å vise et tall vi ikke kan stå for. De to KI-forslagskøene
 * er derimot ekte, allerede eksisterende endepunkter.
 */
function KpiKort({ etikett, verdi }: { etikett: string; verdi: number | null }) {
  return (
    <Card style={{ flex: 1, minWidth: '160px', padding: '0.75rem 1rem' }}>
      <div style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>{etikett}</div>
      <div style={{ fontSize: 'var(--ds-font-size-5)', fontWeight: 600, marginTop: '0.1rem' }}>{verdi ?? '…'}</div>
    </Card>
  );
}

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

  const [tjenesteforslagAntall, setTjenesteforslagAntall] = useState<number | null>(null);
  const [begrepsforslagAntall, setBegrepsforslagAntall] = useState<number | null>(null);

  useEffect(() => {
    api
      .hentTjenester()
      .then(setTjenester)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av tjenester.'));
    api.hentTjenesteforslagKo().then((liste) => setTjenesteforslagAntall(liste.length)).catch(() => setTjenesteforslagAntall(null));
    api.hentBegrepsforslagKo().then((liste) => setBegrepsforslagAntall(liste.length)).catch(() => setBegrepsforslagAntall(null));
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

  const paginering = usePaginering(viste ?? []);

  function sorteringsindikator(kolonne: Sorteringskolonne) {
    if (sortKolonne !== kolonne) return '';
    return sortStigende ? ' ▲' : ' ▼';
  }

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Tjenester
      </Heading>
      <Paragraph style={{ marginBottom: '1.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Virksomhetens tjeneste- og rettighetsdefinisjoner.
      </Paragraph>

      <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
        <KpiKort etikett="KI-forslag tjenester ubehandlet" verdi={tjenesteforslagAntall} />
        <KpiKort etikett="KI-forslag begrep ubehandlet" verdi={begrepsforslagAntall} />
      </div>

      <form onSubmit={opprett} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '1.5rem' }}>
        <Textfield label="Ny tjeneste" placeholder="f.eks. Alminnelig skjenkebevilling" value={nyTittel}
          onChange={(e) => setNyTittel(e.target.value)} required />
        <Button type="submit" disabled={oppretter || !nyTittel.trim()}>
          {oppretter ? 'Oppretter …' : 'Opprett'}
        </Button>
      </form>
      {oppretterFeil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{oppretterFeil}</Alert>}

      <Textfield
        label="Filtrer"
        placeholder="Tittel, tjenestetype, status eller eier"
        value={filterTekst}
        onChange={(e) => setFilterTekst(e.target.value)}
        style={{ maxWidth: '20rem', marginBottom: '1rem' }}
      />

      {feil && <Alert data-color="danger">{feil}</Alert>}
      {!tjenester && !feil && <Spinner aria-label="Laster …" data-size="sm" />}
      {viste && viste.length === 0 && <Paragraph>Ingen tjenester funnet.</Paragraph>}

      {viste && viste.length > 0 && (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <Table>
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
              {paginering.visteRader.map((t) => {
                const status = STATUS_VISNING[t.status];
                return (
                  <Table.Row key={t.id}>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/tjenester/${t.id}`}>{t.tittel}</RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>{t.tjenestetype ?? '—'}</Table.Cell>
                    <Table.Cell>
                      <Tag data-color={status?.farge ?? 'neutral'} data-size="sm">{status?.tekst ?? t.status}</Tag>
                    </Table.Cell>
                    <Table.Cell>{visEier(t.virksomhetId)}</Table.Cell>
                  </Table.Row>
                );
              })}
            </Table.Body>
          </Table>
        </Card>
      )}
      {viste && viste.length > 0 && <Pagineringskontroll {...paginering} />}
    </>
  );
}
