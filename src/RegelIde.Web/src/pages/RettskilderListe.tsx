import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Checkbox, Heading, Link, Paragraph, Spinner, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { LovdataImportstatusDto, RettskildeSammendrag } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

type Sorteringskolonne = 'tittel' | 'kildetype' | 'eier';
type ImportstatusSorteringskolonne = 'tittel' | 'datokode' | 'type';

export default function RettskilderListe() {
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kunMine, setKunMine] = useState(false);
  // Irrelevant-markering (2026-08-30) — default false ekskluderer ErIrrelevant-markerte rettskilder
  // stille fra standardvisningen, samme «ikke skjul stille, gi et eksplisitt valg»-prinsipp som
  // visIkkeImportert lenger ned bruker for Lovdata-importstatus.
  const [visIrrelevante, setVisIrrelevante] = useState(false);
  const [filterTekst, setFilterTekst] = useState('');
  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('tittel');
  const [sortStigende, setSortStigende] = useState(true);
  const { gjeldendeBruker } = useBruker();
  const { visEier } = useVirksomheter();

  // ---------- Ikke-importerte Lovdata-dokumenter (lovdata_importstatus, importert=false) ----------
  // Holdt bevisst unna som standard (Johanns ønske: "filtrere de bort") — lastes lazy først når
  // brukeren faktisk slår på checkboxen, slik at Rettskilder-siden ikke tvangs-henter 5000+ rader med
  // full payload på hver side-last som standardoppførsel.
  const [visIkkeImportert, setVisIkkeImportert] = useState(false);
  const [ikkeImportert, setIkkeImportert] = useState<LovdataImportstatusDto[] | null>(null);
  const [ikkeImportertLaster, setIkkeImportertLaster] = useState(false);
  const [ikkeImportertFeil, setIkkeImportertFeil] = useState<string | null>(null);
  const [importstatusFilterTekst, setImportstatusFilterTekst] = useState('');
  const [importstatusSortKolonne, setImportstatusSortKolonne] = useState<ImportstatusSorteringskolonne>('tittel');
  const [importstatusSortStigende, setImportstatusSortStigende] = useState(true);
  const [importerendeDatokode, setImporterendeDatokode] = useState<string | null>(null);

  function lastRettskilder() {
    setFeil(null);
    setRettskilder(null);
    const virksomhetId = kunMine && gjeldendeBruker ? gjeldendeBruker.virksomhetId : undefined;
    api
      .hentRettskilder(virksomhetId, visIrrelevante)
      .then(setRettskilder)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av rettskilder.'));
  }

  useEffect(() => {
    lastRettskilder();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kunMine, gjeldendeBruker, visIrrelevante]);

  useEffect(() => {
    if (!visIkkeImportert || ikkeImportert !== null) return;
    setIkkeImportertFeil(null);
    setIkkeImportertLaster(true);
    api
      .hentLovdataImportstatus(false)
      .then(setIkkeImportert)
      .catch((e) =>
        setIkkeImportertFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av Lovdata-importstatus.'),
      )
      .finally(() => setIkkeImportertLaster(false));
  }, [visIkkeImportert, ikkeImportert]);

  async function importerDokument(datokode: string) {
    setImporterendeDatokode(datokode);
    try {
      await api.importerFraLovdata(datokode);
      // Nå en ekte rettskilde — fjern fra "ikke importert"-lista og last hovedlista på nytt slik at
      // den dukker opp der.
      setIkkeImportert((liste) => liste?.filter((s) => s.datokode !== datokode) ?? liste);
      lastRettskilder();
    } catch (err) {
      const melding = err instanceof ApiError ? err.message : 'Ukjent feil ved import fra Lovdata.';
      // Feilmeldingen kan ha endret seg siden forrige fullimport-runde — oppdater raden i visningen.
      setIkkeImportert((liste) => liste?.map((s) => (s.datokode === datokode ? { ...s, feilmelding: melding } : s)) ?? liste);
    } finally {
      setImporterendeDatokode(null);
    }
  }

  function bytteSortering(kolonne: Sorteringskolonne) {
    if (sortKolonne === kolonne) setSortStigende((s) => !s);
    else {
      setSortKolonne(kolonne);
      setSortStigende(true);
    }
  }

  function bytteImportstatusSortering(kolonne: ImportstatusSorteringskolonne) {
    if (importstatusSortKolonne === kolonne) setImportstatusSortStigende((s) => !s);
    else {
      setImportstatusSortKolonne(kolonne);
      setImportstatusSortStigende(true);
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

  const visteIkkeImportert = useMemo(() => {
    if (!ikkeImportert) return null;
    const tekst = importstatusFilterTekst.trim().toLowerCase();
    const filtrert = tekst
      ? ikkeImportert.filter(
          (s) =>
            (s.tittel?.toLowerCase().includes(tekst) ?? false) ||
            s.datokode.toLowerCase().includes(tekst) ||
            s.type.toLowerCase().includes(tekst) ||
            (s.feilmelding?.toLowerCase().includes(tekst) ?? false),
        )
      : ikkeImportert;

    const sortnokkel = (s: LovdataImportstatusDto) =>
      importstatusSortKolonne === 'tittel'
        ? (s.tittel ?? s.datokode)
        : importstatusSortKolonne === 'datokode'
          ? s.datokode
          : s.type;

    return [...filtrert].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return importstatusSortStigende ? cmp : -cmp;
    });
  }, [ikkeImportert, importstatusFilterTekst, importstatusSortKolonne, importstatusSortStigende]);

  const paginering = usePaginering(viste ?? []);
  const importstatusPaginering = usePaginering(visteIkkeImportert ?? []);

  function sorteringsindikator(kolonne: Sorteringskolonne) {
    if (sortKolonne !== kolonne) return '';
    return sortStigende ? ' ▲' : ' ▼';
  }

  function importstatusSorteringsindikator(kolonne: ImportstatusSorteringskolonne) {
    if (importstatusSortKolonne !== kolonne) return '';
    return importstatusSortStigende ? ' ▲' : ' ▼';
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Rettskilder
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Åpne data — delte/nasjonale kilder (Lov/Forskrift fra Lovdata) og alle virksomheters
        publiserte lokale kilder. Kladder (status «Utkast») vises aldri her, og kilder markert som
        irrelevant for regel-ide er skjult som standard.
      </Paragraph>

      <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-end', marginBottom: '1rem', flexWrap: 'wrap' }}>
        {gjeldendeBruker && (
          <Checkbox
            label={`Vis kun ${gjeldendeBruker.virksomhetNavn} sine egne kilder`}
            checked={kunMine}
            onChange={(e) => setKunMine(e.target.checked)}
          />
        )}
        <Checkbox
          label="Vis også kilder markert som irrelevant"
          checked={visIrrelevante}
          onChange={(e) => setVisIrrelevante(e.target.checked)}
        />
        <Textfield
          label="Filtrer"
          placeholder="Tittel, kildetype eller eier"
          value={filterTekst}
          onChange={(e) => setFilterTekst(e.target.value)}
          style={{ maxWidth: '20rem' }}
        />
      </div>

      {feil && <Alert data-color="danger">{feil}</Alert>}

      {!rettskilder && !feil && <Spinner aria-label="Laster …" data-size="sm" />}

      {viste && viste.length === 0 && <Paragraph>Ingen rettskilder funnet.</Paragraph>}

      {viste && viste.length > 0 && (
        <Table className="rettskilde-tabell" border data-density="compact">
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
              <Table.HeaderCell></Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {paginering.visteRader.map((r) => (
              <Table.Row key={r.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/rettskilder/${r.id}`}>{r.kortnavn ?? r.tittel}</RouterLink>
                  </Link>
                </Table.Cell>
                <Table.Cell>{r.kildetype}</Table.Cell>
                <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{r.eli ?? '—'}</Table.Cell>
                <Table.Cell>{visEier(r.virksomhetId)}</Table.Cell>
                <Table.Cell>{r.erIrrelevant && <Tag data-color="warning" data-size="sm">Irrelevant</Tag>}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
      {viste && viste.length > 0 && <Pagineringskontroll {...paginering} />}

      <div style={{ marginTop: '2rem', paddingTop: '1.5rem', borderTop: '1px solid var(--ds-color-neutral-border-subtle)' }}>
        <Checkbox
          label={
            ikkeImportert
              ? `Vis også Lovdata-dokumenter som ikke er importert (${ikkeImportert.length})`
              : 'Vis også Lovdata-dokumenter som ikke er importert'
          }
          checked={visIkkeImportert}
          onChange={(e) => setVisIkkeImportert(e.target.checked)}
        />

        {visIkkeImportert && (
          <section style={{ marginTop: '1rem' }}>
            <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
              Lovdata-dokumenter uten import
            </Heading>
            <Paragraph style={{ marginBottom: '0.75rem' }}>
              Dokumenter fra Lovdatas bulk-arkiv som den automatiske synkroniseringen ikke klarte å tolke
              (siste kjente forsøk). Kan importeres enkeltvis her — lykkes det, dukker dokumentet opp i
              tabellen over.
            </Paragraph>

            {ikkeImportertLaster && <Spinner aria-label="Laster …" data-size="sm" />}
            {ikkeImportertFeil && <Alert data-color="danger">{ikkeImportertFeil}</Alert>}

            {ikkeImportert && (
              <>
                <Textfield
                  label="Filtrer"
                  placeholder="Tittel, datokode, type eller feilmelding"
                  value={importstatusFilterTekst}
                  onChange={(e) => setImportstatusFilterTekst(e.target.value)}
                  style={{ maxWidth: '20rem', marginBottom: '0.75rem' }}
                />

                {visteIkkeImportert && visteIkkeImportert.length === 0 && (
                  <Paragraph>Ingen ikke-importerte dokumenter funnet.</Paragraph>
                )}

                {visteIkkeImportert && visteIkkeImportert.length > 0 && (
                  <Table border data-density="compact">
                    <Table.Head>
                      <Table.Row>
                        <Table.HeaderCell>
                          <button
                            type="button"
                            className="tabell-sorter-knapp"
                            onClick={() => bytteImportstatusSortering('tittel')}
                          >
                            Tittel{importstatusSorteringsindikator('tittel')}
                          </button>
                        </Table.HeaderCell>
                        <Table.HeaderCell>
                          <button
                            type="button"
                            className="tabell-sorter-knapp"
                            onClick={() => bytteImportstatusSortering('datokode')}
                          >
                            Datokode{importstatusSorteringsindikator('datokode')}
                          </button>
                        </Table.HeaderCell>
                        <Table.HeaderCell>
                          <button
                            type="button"
                            className="tabell-sorter-knapp"
                            onClick={() => bytteImportstatusSortering('type')}
                          >
                            Type{importstatusSorteringsindikator('type')}
                          </button>
                        </Table.HeaderCell>
                        <Table.HeaderCell>ELI</Table.HeaderCell>
                        <Table.HeaderCell>Feilmelding</Table.HeaderCell>
                        <Table.HeaderCell>Handling</Table.HeaderCell>
                      </Table.Row>
                    </Table.Head>
                    <Table.Body>
                      {importstatusPaginering.visteRader.map((s) => (
                        <Table.Row key={s.datokode}>
                          <Table.Cell>{s.tittel ?? '—'}</Table.Cell>
                          <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{s.datokode}</Table.Cell>
                          <Table.Cell>{s.type}</Table.Cell>
                          <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>
                            <Link href={s.eli} target="_blank" rel="noopener noreferrer">
                              {s.eli}
                            </Link>
                          </Table.Cell>
                          <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)', maxWidth: '24rem' }}>
                            {s.feilmelding ?? '—'}
                          </Table.Cell>
                          <Table.Cell>
                            <Button
                              data-size="sm"
                              disabled={importerendeDatokode === s.datokode}
                              onClick={() => importerDokument(s.datokode)}
                            >
                              {importerendeDatokode === s.datokode ? 'Importerer …' : 'Importer'}
                            </Button>
                          </Table.Cell>
                        </Table.Row>
                      ))}
                    </Table.Body>
                  </Table>
                )}
                {visteIkkeImportert && visteIkkeImportert.length > 0 && (
                  <Pagineringskontroll {...importstatusPaginering} />
                )}
              </>
            )}
          </section>
        )}
      </div>
    </>
  );
}
