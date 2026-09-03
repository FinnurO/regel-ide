import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import {
  Alert, Button, Checkbox, Heading, Link, Paragraph, Spinner, Table, Tabs, Tag, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { LovdataImportstatusDto, RettskildeHjemmelRelasjonDto, RettskildeSammendrag } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';
import { RettskilderHierarki } from '../rettskilde/RettskilderHierarki';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

// 'departement' lagt til (issue #193) — se AnsvarligDepartement. Sorteringsnøkkelen for en rad med
// FLERE departementer (fler-verdi-departement, 2026-09-04) er den alfabetisk LAVESTE av dem, se
// departementSorteringsnokkel under.
type Sorteringskolonne = 'tittel' | 'kildetype' | 'departement' | 'eier';
type ImportstatusSorteringskolonne = 'tittel' | 'datokode' | 'type';
// Tre faner: to opprinnelige (2026-09-02, issue #114) — «Aktive rettskilder» var tidligere den eneste
// tabellen med to gjemte "vis også ..."-avkrysningsbokser (irrelevant-markerte / ikke-trådt-i-kraft).
// Begge kategoriene samles i «Utenfor korpuset» i stedet, se RettskilderListe-komponentens
// hoveddoc-kommentar under. Tredje fane 'hierarki' (issue #193) — departement→lov→forskrift, EGEN
// visningsmodus ved siden av den flate «Aktive rettskilder»-lista, ikke en erstatning for den.
type Fane = 'aktive' | 'utenfor-korpuset' | 'hierarki';

// Ikrafttredelse-status (2026-09-02, listevisning-fiks) — samme dato-mønster som
// LovdataHtmlParser.DatoMønster (FørsteDato) på serversiden: en gyldig åååå-MM-dd et sted i strengen.
// Kun meningsfullt for Lov/Forskrift (RettskildeSammendrag.ikrafttredelseRaa er KUN populert for disse
// to kildetypene — se Dtos.cs) — andre kildetyper skal ALDRI vises som "ikke i kraft" bare fordi feltet
// er null der (det er da forventet fravær av data, ikke et "Kongen bestemmer"-tilfelle).
const IKRAFTTREDELSE_DATO_MONSTER = /\d{4}-\d{2}-\d{2}/;
const KILDETYPER_MED_IKRAFTTREDELSESDATO = new Set(['Lov', 'Forskrift']);

// [Rettet, 2026-09-03, issue #126] `ikrafttredelseRaa == null` ble FØR behandlet identisk med "feltet
// finnes, men inneholder ingen gyldig dato" — begge ga `true` ("ikke i kraft"). Det er FEIL: null her
// betyr «ikke bakfylt ennå» (feltet populeres KUN ved (re)import — se RettskildeSammendrag sin
// doc-kommentar), ikke «Kongen bestemmer»/en reell "ikke trådt i kraft"-status. Koden hadde allerede en
// kommentar (se historikken på denne fila) som advarte MOT nøyaktig denne feilslutningen for andre
// kildetyper (håndbøker/rundskriv, der feltet aldri populeres) — men samme feilslutning rammet
// Lov/Forskrift like hardt når feltet rett og slett ikke var bakfylt ennå (bekreftet direkte mot
// kjørende dev-database: 5873 av 5873 Lov/Forskrift-rader hadde `ikrafttredelseRaa=null` FØR en full
// resynk hadde kjørt siden PR #84 — «Utenfor korpuset»-fanen viste da nesten HELE korpuset).
// Tre eksplisitte tilstander i stedet for to: 'ukjent' (null — ikke bakfylt, IKKE en ekskluderings-
// grunn) skilt fra 'ikke-i-kraft' (feltet ER satt, men inneholder ingen gyldig dato — en REELL "Kongen
// bestemmer uten dato ennå"-status). Kun 'ikke-i-kraft' skal telle som en "Utenfor korpuset"-grunn —
// 'ukjent' havner i «Aktive rettskilder» (samme sted den ville havnet om feltet var korrekt bakfylt til
// en gyldig dato, «gi tvilen fordelen» fremfor å feilklassifisere som ekskludert).
type Ikrafttredelsesstatus = 'ukjent' | 'ikke-i-kraft' | 'i-kraft';

function ikrafttredelsesstatus(r: RettskildeSammendrag): Ikrafttredelsesstatus {
  if (!KILDETYPER_MED_IKRAFTTREDELSESDATO.has(r.kildetype)) return 'i-kraft'; // ikke relevant for denne kildetypen — aldri en ekskluderingsgrunn.
  if (r.ikrafttredelseRaa == null) return 'ukjent';
  return IKRAFTTREDELSE_DATO_MONSTER.test(r.ikrafttredelseRaa) ? 'i-kraft' : 'ikke-i-kraft';
}

function erIkkeTraadtIKraft(r: RettskildeSammendrag): boolean {
  return ikrafttredelsesstatus(r) === 'ikke-i-kraft';
}

// [Ny, issue #193] Samme «aktiv»-avgrensning som 'aktive'-fanens grunnlag i `viste` under, flyttet ut
// til en delt funksjon slik at departement→lov→forskrift-hierarkiet (RettskilderHierarki) bruker
// NØYAKTIG samme korpus-avgrensning — ikke en egen, potensielt driftende definisjon av «aktiv».
function erAktivRettskilde(r: RettskildeSammendrag): boolean {
  return !r.erIrrelevant && !erIkkeTraadtIKraft(r);
}

// [ENDRET, fler-verdi-departement, 2026-09-04] ansvarligDepartement er nå en liste (en rad kan ha flere
// departementer ved delt ansvar) — sorteringsnøkkelen er den alfabetisk LAVESTE av dem, Johanns
// eksplisitte valg («sorter på f.eks. første/alfabetisk laveste departement i listen»). Tom streng (sorterer
// FØRST) for en rad uten noe kjent departement, samme «ukjent sorterer øverst»-idé som annen kode i denne
// fila (se ikrafttredelsesstatus).
function departementSorteringsnokkel(r: RettskildeSammendrag): string {
  if (!r.ansvarligDepartement || r.ansvarligDepartement.length === 0) return '';
  return [...r.ansvarligDepartement].sort((a, b) => a.localeCompare(b, 'nb'))[0];
}

export default function RettskilderListe() {
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kunMine, setKunMine] = useState(false);
  const [fane, setFane] = useState<Fane>('aktive');
  const [filterTekst, setFilterTekst] = useState('');
  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('tittel');
  const [sortStigende, setSortStigende] = useState(true);
  const { gjeldendeBruker } = useBruker();
  const { visEier } = useVirksomheter();

  // ---------- Hjemmelrelasjoner for departement→lov→forskrift-hierarkiet (issue #193) — lazy, samme
  // "ikke tving frem et ekstra kall på hver sidelast"-mønster som visIkkeImportert/ikkeImportert under:
  // lastes først når brukeren faktisk åpner 'hierarki'-fanen. ----------
  const [hjemmelrelasjoner, setHjemmelrelasjoner] = useState<RettskildeHjemmelRelasjonDto[] | null>(null);
  const [hjemmelrelasjonerLaster, setHjemmelrelasjonerLaster] = useState(false);
  const [hjemmelrelasjonerFeil, setHjemmelrelasjonerFeil] = useState<string | null>(null);

  useEffect(() => {
    if (fane !== 'hierarki' || hjemmelrelasjoner !== null) return;
    setHjemmelrelasjonerFeil(null);
    setHjemmelrelasjonerLaster(true);
    api
      .hentHjemmelrelasjoner()
      .then(setHjemmelrelasjoner)
      .catch((e) =>
        setHjemmelrelasjonerFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av hjemmelrelasjoner.'),
      )
      .finally(() => setHjemmelrelasjonerLaster(false));
  }, [fane, hjemmelrelasjoner]);

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
    // inkluderIrrelevante er alltid true nå (2026-09-02, issue #114) — begge fanene deler ett
    // datasett; «Aktive rettskilder» filtrerer irrelevante/ikke-i-kraft-rader bort selv (se `viste`
    // under), «Utenfor korpuset» trenger dem. Tidligere var dette et eget opt-in-toggle, se historikk.
    api
      .hentRettskilder(virksomhetId, true)
      .then(setRettskilder)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av rettskilder.'));
  }

  useEffect(() => {
    lastRettskilder();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kunMine, gjeldendeBruker]);

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
    // «Aktive rettskilder» = verken irrelevant-markert eller ikke-i-kraft (se erAktivRettskilde).
    // «Utenfor korpuset» = minst én av de to (en rad kan i prinsippet ha begge samtidig — se
    // Grunn-kolonnen i den fanen).
    const grunnlag =
      fane === 'aktive' ? rettskilder.filter(erAktivRettskilde) : rettskilder.filter((r) => !erAktivRettskilde(r));
    const filtrert = tekst
      ? grunnlag.filter(
          (r) =>
            r.tittel.toLowerCase().includes(tekst) ||
            (r.kortnavn?.toLowerCase().includes(tekst) ?? false) ||
            r.kildetype.toLowerCase().includes(tekst) ||
            visEier(r.virksomhetId).toLowerCase().includes(tekst),
        )
      : grunnlag;

    const sortnokkel = (r: RettskildeSammendrag) =>
      sortKolonne === 'tittel'
        ? r.tittel
        : sortKolonne === 'kildetype'
          ? r.kildetype
          : sortKolonne === 'departement'
            ? departementSorteringsnokkel(r)
            : visEier(r.virksomhetId);

    return [...filtrert].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
  }, [rettskilder, filterTekst, sortKolonne, sortStigende, visEier, fane]);

  // [Ny, issue #193] Grunnlaget for departement→lov→forskrift-hierarkiet — SAMME «aktiv»-avgrensning
  // som 'aktive'-fanens flate liste (erAktivRettskilde over), slik at hierarkiet og listevisningen alltid
  // viser samme korpus, bare gruppert ulikt. Fritekstfilteret (filterTekst) anvendes IKKE her —
  // RettskilderHierarki filtrerer selv på lov-/forskriftstittel, siden hierarkiet må beholde en lovs
  // grener selv om treffet kun er i en forskrift under den (se komponenten). Departement-filteret som
  // fantes her er FJERNET (fler-verdi-departement-runden, 2026-09-04, Johanns eksplisitte designvalg) —
  // hierarki-fanen ER navigasjonsmekanismen på departement-nivå.
  const hierarkiGrunnlag = useMemo(() => {
    if (!rettskilder) return null;
    return rettskilder.filter(erAktivRettskilde);
  }, [rettskilder]);

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
        publiserte lokale kilder. Kladder (status «Utkast») vises aldri her. Kilder markert som
        irrelevant for regel-ide og Lov/Forskrift som ennå ikke er trådt i kraft holdes utenfor
        «Aktive rettskilder» — se fanen «Utenfor korpuset» for begge kategoriene.
      </Paragraph>

      <Tabs value={fane} onChange={(v) => setFane(v as Fane)} style={{ marginBottom: '1rem' }}>
        <Tabs.List>
          <Tabs.Tab value="aktive">Aktive rettskilder</Tabs.Tab>
          <Tabs.Tab value="hierarki">Departement → lov → forskrift</Tabs.Tab>
          <Tabs.Tab value="utenfor-korpuset">Utenfor korpuset</Tabs.Tab>
        </Tabs.List>
      </Tabs>

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

      {feil && <Alert data-color="danger">{feil}</Alert>}

      {!rettskilder && !feil && <Spinner aria-label="Laster …" data-size="sm" />}

      {fane !== 'hierarki' && viste && viste.length === 0 && (
        <Paragraph>
          {fane === 'aktive' ? 'Ingen rettskilder funnet.' : 'Ingen rettskilder utenfor korpuset funnet.'}
        </Paragraph>
      )}

      {fane === 'hierarki' && (
        <>
          {hjemmelrelasjonerFeil && <Alert data-color="danger">{hjemmelrelasjonerFeil}</Alert>}
          {hjemmelrelasjonerLaster && <Spinner aria-label="Laster …" data-size="sm" />}
          {hierarkiGrunnlag && hjemmelrelasjoner && (
            <RettskilderHierarki
              rettskilder={hierarkiGrunnlag}
              hjemmelrelasjoner={hjemmelrelasjoner}
              filterTekst={filterTekst}
            />
          )}
        </>
      )}

      {viste && viste.length > 0 && fane === 'aktive' && (
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
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('departement')}>
                  Departement{sorteringsindikator('departement')}
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
            {paginering.visteRader.map((r) => (
              <Table.Row key={r.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/rettskilder/${r.id}`}>{r.tittel}</RouterLink>
                  </Link>
                  {r.kortnavn && (
                    <span
                      style={{
                        marginLeft: '0.5rem',
                        fontSize: 'var(--ds-font-size-1)',
                        color: 'var(--ds-color-neutral-text-subtle)',
                      }}
                    >
                      {r.kortnavn}
                    </span>
                  )}
                </Table.Cell>
                <Table.Cell>{r.kildetype}</Table.Cell>
                <Table.Cell>{r.ansvarligDepartement?.join(', ') || '—'}</Table.Cell>
                <Table.Cell>{visEier(r.virksomhetId)}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}

      {viste && viste.length > 0 && fane === 'utenfor-korpuset' && (
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
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('departement')}>
                  Departement{sorteringsindikator('departement')}
                </button>
              </Table.HeaderCell>
              <Table.HeaderCell>
                <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('eier')}>
                  Eier{sorteringsindikator('eier')}
                </button>
              </Table.HeaderCell>
              <Table.HeaderCell>Grunn</Table.HeaderCell>
              <Table.HeaderCell>Merknad</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {paginering.visteRader.map((r) => (
              <Table.Row key={r.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/rettskilder/${r.id}`}>{r.tittel}</RouterLink>
                  </Link>
                  {r.kortnavn && (
                    <span
                      style={{
                        marginLeft: '0.5rem',
                        fontSize: 'var(--ds-font-size-1)',
                        color: 'var(--ds-color-neutral-text-subtle)',
                      }}
                    >
                      {r.kortnavn}
                    </span>
                  )}
                </Table.Cell>
                <Table.Cell>{r.kildetype}</Table.Cell>
                <Table.Cell>{r.ansvarligDepartement?.join(', ') || '—'}</Table.Cell>
                <Table.Cell>{visEier(r.virksomhetId)}</Table.Cell>
                <Table.Cell style={{ display: 'flex', gap: '0.25rem', flexWrap: 'wrap' }}>
                  {r.erIrrelevant && <Tag data-color="warning" data-size="sm">Irrelevant</Tag>}
                  {erIkkeTraadtIKraft(r) && <Tag data-color="warning" data-size="sm">Ikke i kraft</Tag>}
                </Table.Cell>
                <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)', maxWidth: '24rem' }}>
                  {r.erIrrelevant && r.irrelevantKommentar ? r.irrelevantKommentar : '—'}
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
      {fane !== 'hierarki' && viste && viste.length > 0 && <Pagineringskontroll {...paginering} />}

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
