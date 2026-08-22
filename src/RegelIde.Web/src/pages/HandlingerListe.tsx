/**
 * HandlingerListe (2026-08-22) — egen toppnivå-inngang for Handlinger, se Sidebar.tsx-gruppen
 * "Arbeidsprodukt". Lister Handling-rader TVERS AV ALLE Tjenester (til forskjell fra
 * TjenesteDetalj.tsx sin nøstede "Handlinger"-seksjon, som viser kun ÉN tjenestes egne) — samme
 * sorterings-/filter-/tabellmønster som TjenesterListe.tsx, men uten opprett-formen (en handling
 * opprettes alltid UNDER en konkret tjeneste, ikke fritt fra denne siden).
 *
 * Henter via GET /api/handlinger — ETT kall (HandlingregisterTjeneste.ListerAlleAsync joiner inn
 * tjenestens tittel+virksomhetId server-side), ikke N kall (ett per tjeneste) fra klienten.
 */
import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Card, Heading, Link, Paragraph, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { HandlingMedTjenesteDto } from '../api/types';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

type Sorteringskolonne = 'navn' | 'handlingstype' | 'status' | 'tjeneste' | 'eier';

/** Samme 6 statusverdier/farger som TjenesterListe.tsx (HandlingEntitet.Status er samme verdisett). */
const STATUS_VISNING: Record<string, { farge: 'neutral' | 'warning' | 'info' | 'success' | 'danger'; tekst: string }> = {
  utkast: { farge: 'neutral', tekst: 'Utkast' },
  under_revisjon: { farge: 'warning', tekst: 'Under revisjon' },
  validert: { farge: 'info', tekst: 'Validert' },
  publisert: { farge: 'success', tekst: 'Publisert' },
  tilbaketrukket: { farge: 'danger', tekst: 'Tilbaketrukket' },
  arkivert: { farge: 'neutral', tekst: 'Arkivert' },
};

export default function HandlingerListe() {
  const [rader, setRader] = useState<HandlingMedTjenesteDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [filterTekst, setFilterTekst] = useState('');
  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('navn');
  const [sortStigende, setSortStigende] = useState(true);
  const { visEier } = useVirksomheter();

  useEffect(() => {
    api
      .hentAlleHandlinger()
      .then(setRader)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av handlinger.'));
  }, []);

  function bytteSortering(kolonne: Sorteringskolonne) {
    if (sortKolonne === kolonne) setSortStigende((s) => !s);
    else {
      setSortKolonne(kolonne);
      setSortStigende(true);
    }
  }

  const viste = useMemo(() => {
    if (!rader) return null;
    const tekst = filterTekst.trim().toLowerCase();
    const filtrert = tekst
      ? rader.filter(
          (r) =>
            r.handling.navn.toLowerCase().includes(tekst) ||
            r.handling.handlingstype.toLowerCase().includes(tekst) ||
            r.handling.status.toLowerCase().includes(tekst) ||
            r.tjenesteTittel.toLowerCase().includes(tekst) ||
            visEier(r.virksomhetId).toLowerCase().includes(tekst),
        )
      : rader;

    const sortnokkel = (r: HandlingMedTjenesteDto) =>
      sortKolonne === 'navn'
        ? r.handling.navn
        : sortKolonne === 'handlingstype'
          ? r.handling.handlingstype
          : sortKolonne === 'status'
            ? r.handling.status
            : sortKolonne === 'tjeneste'
              ? r.tjenesteTittel
              : visEier(r.virksomhetId);

    return [...filtrert].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
  }, [rader, filterTekst, sortKolonne, sortStigende, visEier]);

  const paginering = usePaginering(viste ?? []);

  function sorteringsindikator(kolonne: Sorteringskolonne) {
    if (sortKolonne !== kolonne) return '';
    return sortStigende ? ' ▲' : ' ▼';
  }

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Handlinger
      </Heading>
      <Paragraph style={{ marginBottom: '1.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Konkrete handlinger (søke, melde, klage, kontrolleres, …) tvers av alle rettigheter (tjenester).
      </Paragraph>

      <Textfield
        label="Filtrer"
        placeholder="Navn, handlingstype, status, rettighet eller eier"
        value={filterTekst}
        onChange={(e) => setFilterTekst(e.target.value)}
        style={{ maxWidth: '20rem', marginBottom: '1rem' }}
      />

      {feil && <div className="feilmelding">{feil}</div>}
      {!rader && !feil && <Paragraph>Laster …</Paragraph>}
      {viste && viste.length === 0 && <Paragraph>Ingen handlinger funnet.</Paragraph>}

      {viste && viste.length > 0 && (
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
                  <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('handlingstype')}>
                    Handlingstype{sorteringsindikator('handlingstype')}
                  </button>
                </Table.HeaderCell>
                <Table.HeaderCell>
                  <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('status')}>
                    Status{sorteringsindikator('status')}
                  </button>
                </Table.HeaderCell>
                <Table.HeaderCell>
                  <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('tjeneste')}>
                    Rettighet{sorteringsindikator('tjeneste')}
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
              {paginering.visteRader.map((r) => {
                const status = STATUS_VISNING[r.handling.status];
                return (
                  <Table.Row key={r.handling.id}>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/tjenester/${r.handling.tjenesteId}/handlinger/${r.handling.id}`}>{r.handling.navn}</RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>{r.handling.handlingstype}</Table.Cell>
                    <Table.Cell>
                      <Tag data-color={status?.farge ?? 'neutral'} data-size="sm">{status?.tekst ?? r.handling.status}</Tag>
                    </Table.Cell>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/tjenester/${r.handling.tjenesteId}`}>{r.tjenesteTittel}</RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>{visEier(r.virksomhetId)}</Table.Cell>
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
