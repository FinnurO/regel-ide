import { useMemo, useState } from 'react';
import { Link as RouterLink, useSearchParams } from 'react-router';
import { Alert, Button, Card, Heading, Link, Paragraph, Spinner, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { BrregEnhetDto, VirksomhetDto } from '../api/types';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

type Sorteringskolonne = 'navn' | 'organisasjonsnummer' | 'forvaltningsniva' | 'aktiv';

/** Samme "ikke gjett, vis tomt tydelig"-holdning som resten av appen (docs/20 §4/§7.2) — de fleste
 * radene har ingen Forvaltningsniva satt ennå, og det skal se annerledes ut enn en reell verdi. */
function forvaltningsnivaVisning(verdi: string | null): { farge: 'neutral' | 'info'; tekst: string } {
  return verdi ? { farge: 'info', tekst: verdi } : { farge: 'neutral', tekst: 'Ikke satt' };
}

export default function VirksomheterListe() {
  const { virksomheter, laster, oppdater } = useVirksomheter();
  // [Ny, docs/13-backlog.md §9] ?forslagNavn=… (fra Navnekandidater-siden sin "virksomhet"-kategori,
  // "Finn/opprett virksomhet"-lenken) forhåndsutfyller BEGGE opprett-panelene under — et helt nytt
  // egennavn kan jo enten allerede finnes i Brreg (søk finner det) eller trenge "bare navn"-flyten
  // (aktør uten egen Brreg-registrering), og vi vet ikke hvilket UTEN at brukeren faktisk ser etter.
  const [søkeparametre] = useSearchParams();
  const forhaandsutfyltNavn = søkeparametre.get('forslagNavn') ?? '';
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

  const paginering = usePaginering(viste);

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

      <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', marginBottom: '1.25rem' }}>
        <BrregSokPanel
          eksisterendeOrgnr={new Set(virksomheter.map((v) => v.organisasjonsnummer).filter((n): n is string => !!n))}
          onOpprettet={oppdater}
          forhaandsutfyltSok={forhaandsutfyltNavn}
        />
        <NavnKunPanel virksomheter={virksomheter} onOpprettet={oppdater} forhaandsutfyltNavn={forhaandsutfyltNavn} />
      </div>

      <Textfield
        label="Filtrer"
        placeholder="Navn, organisasjonsnummer eller forvaltningsnivå"
        value={filterTekst}
        onChange={(e) => setFilterTekst(e.target.value)}
        style={{ maxWidth: '20rem', marginBottom: '1rem' }}
      />

      {laster && <Spinner aria-label="Laster …" data-size="sm" />}
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
              {paginering.visteRader.map((v) => {
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
      {!laster && viste.length > 0 && <Pagineringskontroll {...paginering} />}
    </>
  );
}

/**
 * [Ny, 2026-08-29, docs/13-backlog.md §9] Søk-og-opprett mot Brreg — for å tette reelle hull i
 * katalogen (Johann fant flere navngitte myndigheter/institusjoner i lovtekst som mangler helt,
 * f.eks. Bufetat/Statped/NPE) uten å måtte taste inn orgnr/navn manuelt. Portert fra
 * `github.com/FinnurO/kontaktlisteregisteret`s `BrregService`-mønster, se `BrregKlient.cs`.
 */
function BrregSokPanel({
  eksisterendeOrgnr, onOpprettet, forhaandsutfyltSok,
}: { eksisterendeOrgnr: Set<string>; onOpprettet: () => void; forhaandsutfyltSok?: string }) {
  const [sokTekst, setSokTekst] = useState(forhaandsutfyltSok ?? '');
  const [soker, setSoker] = useState(false);
  const [treff, setTreff] = useState<BrregEnhetDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [oppretterOrgnr, setOppretterOrgnr] = useState<string | null>(null);
  const [nettoppOpprettet, setNettoppOpprettet] = useState<Set<string>>(new Set());

  async function sok(e: React.FormEvent) {
    e.preventDefault();
    if (!sokTekst.trim()) return;
    setSoker(true);
    setFeil(null);
    try {
      setTreff(await api.sokBrreg(sokTekst.trim()));
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved søk mot Brreg.');
      setTreff(null);
    } finally {
      setSoker(false);
    }
  }

  async function opprett(orgnr: string) {
    setOppretterOrgnr(orgnr);
    setFeil(null);
    try {
      await api.opprettVirksomhetFraBrreg(orgnr);
      setNettoppOpprettet((s) => new Set(s).add(orgnr));
      onOpprettet();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse.');
    } finally {
      setOppretterOrgnr(null);
    }
  }

  return (
    <Card style={{ padding: '1rem', marginBottom: '1.25rem', maxWidth: '40rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.3rem' }}>
        Søk i Brreg og opprett virksomhet
      </Heading>
      <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
        For virksomheter som mangler i katalogen over — søk på navn eller organisasjonsnummer i
        Brønnøysundregisterets Enhetsregister, og opprett den direkte herfra.
      </Paragraph>
      <form onSubmit={sok} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '0.75rem' }}>
        <Textfield
          label="Navn eller organisasjonsnummer"
          placeholder="f.eks. Statped, eller 974761084"
          value={sokTekst}
          onChange={(e) => setSokTekst(e.target.value)}
          style={{ maxWidth: '20rem' }}
        />
        <Button type="submit" disabled={soker || !sokTekst.trim()}>
          {soker ? 'Søker …' : 'Søk i Brreg'}
        </Button>
      </form>

      {feil && <Alert data-color="danger" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}

      {treff && treff.length === 0 && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Ingen treff i Brreg.</Paragraph>}

      {treff && treff.length > 0 && (
        <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
          {treff.map((t) => {
            const alleredeICatalogen = eksisterendeOrgnr.has(t.organisasjonsnummer) || nettoppOpprettet.has(t.organisasjonsnummer);
            return (
              <li
                key={t.organisasjonsnummer}
                style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', padding: '0.4rem 0', borderTop: '1px solid var(--ds-color-neutral-border-subtle)' }}
              >
                <span style={{ flex: 1 }}>
                  {t.navn}{' '}
                  <span style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                    ({t.organisasjonsnummer})
                  </span>
                  {t.organisasjonsformBeskrivelse && (
                    <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}> — {t.organisasjonsformBeskrivelse}</span>
                  )}
                  {t.poststed && <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>, {t.poststed}</span>}
                  {!t.erAktiv && <Tag data-color="warning" data-size="sm" style={{ marginLeft: '0.4rem' }}>Slettet i Brreg</Tag>}
                </span>
                {alleredeICatalogen ? (
                  <Tag data-color="success" data-size="sm">Allerede i katalogen</Tag>
                ) : (
                  <Button data-size="sm" onClick={() => opprett(t.organisasjonsnummer)} disabled={oppretterOrgnr === t.organisasjonsnummer}>
                    {oppretterOrgnr === t.organisasjonsnummer ? 'Oppretter …' : 'Opprett virksomhet'}
                  </Button>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
}

/**
 * [Ny, 2026-08-30, brukertilbakemelding] Opprett en virksomhet med KUN navn — for aktører uten egen
 * Brreg-registrering, f.eks. Kystvakten (del av Forsvaret). «Del av virksomhet» er valgfri og setter
 * OverordnetEnhetId — samme felt Brreg-berikelse ellers fyller automatisk (docs/20 §2.1), her manuelt
 * siden Brreg ikke har denne relasjonen for aktører uten egen registrering.
 */
function NavnKunPanel({
  virksomheter, onOpprettet, forhaandsutfyltNavn,
}: { virksomheter: VirksomhetDto[]; onOpprettet: () => void; forhaandsutfyltNavn?: string }) {
  const [navn, setNavn] = useState(forhaandsutfyltNavn ?? '');
  const [overordnetEnhetId, setOverordnetEnhetId] = useState('');
  const [oppretter, setOppretter] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [sistOpprettetNavn, setSistOpprettetNavn] = useState<string | null>(null);

  async function opprett(e: React.FormEvent) {
    e.preventDefault();
    if (!navn.trim()) return;
    setOppretter(true);
    setFeil(null);
    try {
      await api.opprettVirksomhet({ navn: navn.trim(), overordnetEnhetId: overordnetEnhetId || null });
      setSistOpprettetNavn(navn.trim());
      setNavn('');
      setOverordnetEnhetId('');
      onOpprettet();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse.');
    } finally {
      setOppretter(false);
    }
  }

  return (
    <Card style={{ padding: '1rem', maxWidth: '28rem', flex: '1 1 20rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.3rem' }}>
        Opprett virksomhet med bare navn
      </Heading>
      <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
        For aktører uten egen Brreg-registrering, f.eks. Kystvakten (del av Forsvaret) — «del av
        virksomhet» er valgfri.
      </Paragraph>
      <form onSubmit={opprett}>
        <Textfield
          label="Navn"
          value={navn}
          onChange={(e) => setNavn(e.target.value)}
          style={{ marginBottom: '0.5rem' }}
        />
        <VirksomhetVelger
          virksomheter={virksomheter}
          value={overordnetEnhetId}
          onChange={setOverordnetEnhetId}
          label="Del av virksomhet (valgfri)"
          tomValgTekst="Ingen — selvstendig virksomhet"
          style={{ marginBottom: '0.75rem' }}
        />
        {feil && <Alert data-color="danger" style={{ marginBottom: '0.5rem' }}>{feil}</Alert>}
        {sistOpprettetNavn && !feil && (
          <Alert data-color="success" style={{ marginBottom: '0.5rem' }}>«{sistOpprettetNavn}» opprettet.</Alert>
        )}
        <Button type="submit" disabled={oppretter || !navn.trim()}>
          {oppretter ? 'Oppretter …' : 'Opprett virksomhet'}
        </Button>
      </form>
    </Card>
  );
}
