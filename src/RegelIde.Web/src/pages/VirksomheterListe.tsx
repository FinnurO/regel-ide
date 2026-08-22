import { useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Card, Heading, Link, Paragraph, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

type Sorteringskolonne = 'navn' | 'organisasjonsnummer' | 'forvaltningsniva' | 'aktiv';

/** Samme "ikke gjett, vis tomt tydelig"-holdning som resten av appen (docs/20 §4/§7.2) — de fleste
 * radene har ingen Forvaltningsniva satt ennå, og det skal se annerledes ut enn en reell verdi. */
function forvaltningsnivaVisning(verdi: string | null): { farge: 'neutral' | 'info'; tekst: string } {
  return verdi ? { farge: 'info', tekst: verdi } : { farge: 'neutral', tekst: 'Ikke satt' };
}

export default function VirksomheterListe() {
  const { virksomheter, laster } = useVirksomheter();
  const [filterTekst, setFilterTekst] = useState('');
  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('navn');
  const [sortStigende, setSortStigende] = useState(true);

  function bytteSortering(kolonne: Sorteringskolonne) {
    if (sortKolonne === kolonne) setSortStigende((s) => !s);
    else {
      setSortKolonne(kolonne);
      setSortStigende(true);
    }
  }

  const viste = useMemo(() => {
    const tekst = filterTekst.trim().toLowerCase();
    const filtrert = tekst
      ? virksomheter.filter(
          (v) =>
            v.navn.toLowerCase().includes(tekst) ||
            (v.organisasjonsnummer?.includes(tekst) ?? false) ||
            (v.forvaltningsniva?.toLowerCase().includes(tekst) ?? false),
        )
      : virksomheter;

    const sortnokkel = (v: (typeof virksomheter)[number]) =>
      sortKolonne === 'navn'
        ? v.navn
        : sortKolonne === 'organisasjonsnummer'
          ? (v.organisasjonsnummer ?? '')
          : sortKolonne === 'forvaltningsniva'
            ? (v.forvaltningsniva ?? '')
            : String(v.aktiv);

    return [...filtrert].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
  }, [virksomheter, filterTekst, sortKolonne, sortStigende]);

  function sorteringsindikator(kolonne: Sorteringskolonne) {
    if (sortKolonne !== kolonne) return '';
    return sortStigende ? ' ▲' : ' ▼';
  }

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Virksomheter
      </Heading>
      <Paragraph style={{ marginBottom: '1.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Katalog over virksomheter identifisert ved organisasjonsnummer (docs/20) — både aktive tenanter
        i Regel-IDE og virksomheter som bare forekommer i rettskildetekst. En virksomhet trenger ikke
        ha brukere for å stå her.
      </Paragraph>

      <Textfield
        label="Filtrer"
        placeholder="Navn, organisasjonsnummer eller forvaltningsnivå"
        value={filterTekst}
        onChange={(e) => setFilterTekst(e.target.value)}
        style={{ maxWidth: '20rem', marginBottom: '1rem' }}
      />

      {laster && <Paragraph>Laster …</Paragraph>}
      {!laster && viste.length === 0 && <Paragraph>Ingen virksomheter funnet.</Paragraph>}

      {!laster && viste.length > 0 && (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <Table>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>
                  <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('navn')}>
                    Navn{sorteringsindikator('navn')}
                  </button>
                </Table.HeaderCell>
                <Table.HeaderCell>
                  <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('organisasjonsnummer')}>
                    Organisasjonsnummer{sorteringsindikator('organisasjonsnummer')}
                  </button>
                </Table.HeaderCell>
                <Table.HeaderCell>
                  <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('forvaltningsniva')}>
                    Forvaltningsnivå{sorteringsindikator('forvaltningsniva')}
                  </button>
                </Table.HeaderCell>
                <Table.HeaderCell>
                  <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('aktiv')}>
                    Aktiv{sorteringsindikator('aktiv')}
                  </button>
                </Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {viste.map((v) => {
                const forvaltningsniva = forvaltningsnivaVisning(v.forvaltningsniva);
                return (
                  <Table.Row key={v.id}>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/virksomheter/${v.id}`}>{v.navn}</RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell style={{ fontFamily: 'monospace' }}>{v.organisasjonsnummer ?? '—'}</Table.Cell>
                    <Table.Cell>
                      <Tag data-color={forvaltningsniva.farge} data-size="sm">{forvaltningsniva.tekst}</Tag>
                    </Table.Cell>
                    <Table.Cell>
                      <Tag data-color={v.aktiv ? 'success' : 'neutral'} data-size="sm">{v.aktiv ? 'Aktiv' : 'Sovende'}</Tag>
                    </Table.Cell>
                  </Table.Row>
                );
              })}
            </Table.Body>
          </Table>
        </Card>
      )}
    </>
  );
}
