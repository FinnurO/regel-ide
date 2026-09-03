import { useMemo, useState } from 'react';
import { Link as RouterLink, useSearchParams } from 'react-router';
import { Alert, Button, Card, Checkbox, Heading, Link, Paragraph, Spinner, Table, Tag, Textfield } from '@digdir/designsystemet-react';
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
  // [Ny, 2026-08-30, docs/13-backlog.md §9 — "koble til eksisterende virksomhet"] Følger med samme
  // lenke som `forslagNavn` (NavnekandidaterListe.tsx) — lar tredje panelet under tilby å godkjenne
  // DENNE konkrete kandidatraden i samme handling som å koble navneformen. Kun til stede når
  // landingen faktisk kom fra en navnekandidatrad; `null` ellers (f.eks. direkte navigering hit).
  const navnekandidatId = søkeparametre.get('navnekandidatId');
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
        <KoblEksisterendeVirksomhetPanel
          virksomheter={virksomheter}
          forhaandsutfyltNavn={forhaandsutfyltNavn}
          navnekandidatId={navnekandidatId}
        />
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
  const [sistOpprettetId, setSistOpprettetId] = useState<string | null>(null);
  // [Ny, issue #194] SNL-lenken for navneformen som (eventuelt) ble auto-opprettet ved dette kallet —
  // `undefined` = ikke sjekket ennå, `null` = sjekket, ingen bekreftet SNL-treff.
  const [sistOpprettetSnlUrl, setSistOpprettetSnlUrl] = useState<string | null | undefined>(undefined);

  async function opprett(e: React.FormEvent) {
    e.preventDefault();
    if (!navn.trim()) return;
    setOppretter(true);
    setFeil(null);
    try {
      const opprettet = await api.opprettVirksomhet({ navn: navn.trim(), overordnetEnhetId: overordnetEnhetId || null });
      setSistOpprettetNavn(navn.trim());
      setSistOpprettetId(opprettet.id);
      setSistOpprettetSnlUrl(undefined);
      setNavn('');
      setOverordnetEnhetId('');
      onOpprettet();
      // Backend gjorde nettopp et synkront SNL-oppslag (#194, samme mekanisme som fra-brreg) — hent
      // navneformene på nytt for å vise en eventuell bekreftet SNL-lenke med én gang, til
      // verifisering, i stedet for å tvinge saksbehandler til å navigere til detaljsiden for å se den.
      try {
        const begrep = await api.hentVirksomhetsbegrep(opprettet.id);
        setSistOpprettetSnlUrl(begrep.find((b) => b.skosUrl)?.skosUrl ?? null);
      } catch {
        setSistOpprettetSnlUrl(null);
      }
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
        virksomhet» er valgfri. Navnet slås automatisk opp mot Store norske leksikon; en bekreftet
        artikkel gir en ferdig navneform du kan verifisere under.
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
          <Alert data-color="success" style={{ marginBottom: '0.5rem' }}>
            <span style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
              «{sistOpprettetNavn}» opprettet.
              {sistOpprettetId && (
                <Link asChild data-size="sm">
                  <RouterLink to={`/virksomheter/${sistOpprettetId}`}>Åpne virksomheten</RouterLink>
                </Link>
              )}
              {sistOpprettetSnlUrl && (
                <Link href={sistOpprettetSnlUrl} target="_blank" rel="noopener noreferrer" data-size="sm">
                  <Tag data-color="success" data-size="sm">SNL-bekreftet navneform opprettet ↗</Tag>
                </Link>
              )}
              {sistOpprettetSnlUrl === null && (
                <Tag data-color="neutral" data-size="sm">Ingen SNL-treff — ingen navneform auto-opprettet</Tag>
              )}
            </span>
          </Alert>
        )}
        <Button type="submit" disabled={oppretter || !navn.trim()}>
          {oppretter ? 'Oppretter …' : 'Opprett virksomhet'}
        </Button>
      </form>
    </Card>
  );
}

/**
 * [Ny, 2026-08-30, oppgavebeskrivelse "koble til eksisterende virksomhet"] Tredje vei inn i denne
 * landingen, VED SIDEN AV de to opprett-panelene over (ikke i stedet for — noen ganger ER det
 * faktisk en helt ny virksomhet). Dekker Johanns konkrete eksempel: "Kredittilsynet er nå
 * Finanstilsynet" — en navnekandidat av kategori `"virksomhet"` som IKKE er en ny aktør, men bare
 * et gammelt navn på en virksomhet som allerede finnes i katalogen. Før dette panelet fantes det
 * ingen vei hit fra godkjenningsflyten: brukeren måtte selv huske ekvivalensen, søke opp
 * Finanstilsynet i den generelle listen, åpne detaljsiden, og bruke "Legg til navneform"-skjemaet
 * der — helt frikoblet fra navnekandidat-raden som utløste det hele.
 *
 * Gjenbruker BEVISST eksisterende backend-kapasitet, ingen ny entitet/endepunkt:
 * - `VirksomhetVelger` (samme Combobox-mønster som "Del av virksomhet" i NavnKunPanel over — samme
 *   ~451-rader-"unngå render-alle-som-option"-begrunnelse gjelder identisk her, se den filens
 *   kommentar).
 * - `POST /api/virksomhetsbegrep` (samme endepunkt som "Legg til navneform"-skjemaet på
 *   VirksomhetDetalj.tsx bruker, `api.opprettVirksomhetsbegrep`) — INGEN egen "koble"-entitet, en
 *   navneform PEKENDE PÅ den valgte virksomheten er hele koblingen.
 *
 * Koblingen er ALDRI automatisk/gjettet — knappen er disabled til et menneske eksplisitt har valgt
 * én virksomhet i velgeren.
 *
 * Godkjenning av selve navnekandidat-raden (kun når landingen kom fra en kandidatrad, se
 * `navnekandidatId`) er en SEPARAT avkrysning, forhåndshuket men synlig og av-hukbar — bevisst ikke
 * stille/automatisk (oppgavebeskrivelsens eksplisitte krav): brukeren skal se at "koble navneform"
 * og "godkjenn kandidaten" er to ulike konsekvenser av samme trykk, ikke én skjult bivirkning.
 */
function KoblEksisterendeVirksomhetPanel({
  virksomheter, forhaandsutfyltNavn, navnekandidatId,
}: { virksomheter: VirksomhetDto[]; forhaandsutfyltNavn?: string; navnekandidatId?: string | null }) {
  const [navn, setNavn] = useState(forhaandsutfyltNavn ?? '');
  const [valgtVirksomhetId, setValgtVirksomhetId] = useState('');
  const [godkjennKandidatOgsa, setGodkjennKandidatOgsa] = useState(true);
  const [kobler, setKobler] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [kandidatFeil, setKandidatFeil] = useState<string | null>(null);
  const [suksess, setSuksess] = useState<{ navn: string; virksomhetId: string; virksomhetNavn: string; kandidatGodkjent: boolean } | null>(null);

  async function koble(e: React.FormEvent) {
    e.preventDefault();
    if (!navn.trim() || !valgtVirksomhetId) return;
    const virksomhet = virksomheter.find((v) => v.id === valgtVirksomhetId);
    if (!virksomhet) return; // Skal ikke kunne skje — velgeren viser kun rader fra `virksomheter`.

    setKobler(true);
    setFeil(null);
    setKandidatFeil(null);
    setSuksess(null);
    try {
      await api.opprettVirksomhetsbegrep({ virksomhetId: valgtVirksomhetId, term: navn.trim(), skosUrl: null });

      // Kandidatgodkjenningen er en SEPARAT handling mot et separat endepunkt — feiler den, skal
      // ikke navneform-koblingen (som allerede lyktes) fremstå som mislykket. Vises i stedet som en
      // egen feilmelding ved siden av suksessmeldingen for selve koblingen.
      let kandidatGodkjent = false;
      if (navnekandidatId && godkjennKandidatOgsa) {
        try {
          await api.godkjennNavnekandidat(navnekandidatId);
          kandidatGodkjent = true;
        } catch (err) {
          setKandidatFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved godkjenning av navnekandidaten.');
        }
      }

      setSuksess({ navn: navn.trim(), virksomhetId: valgtVirksomhetId, virksomhetNavn: virksomhet.navn, kandidatGodkjent });
      setValgtVirksomhetId('');
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av navneform.');
    } finally {
      setKobler(false);
    }
  }

  return (
    <Card style={{ padding: '1rem', maxWidth: '28rem', flex: '1 1 20rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.3rem' }}>
        Er dette et nytt navn for en virksomhet som allerede finnes?
      </Heading>
      <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
        For når det ikke er en ny virksomhet, men en ny navneform på én som allerede er i katalogen —
        f.eks. «Kredittilsynet» som en eldre betegnelse på Finanstilsynet. Velg virksomheten under;
        navnet legges til som navneform på DEN, ingen ny virksomhet opprettes.
      </Paragraph>
      <form onSubmit={koble}>
        <Textfield
          label="Navneform"
          value={navn}
          onChange={(e) => setNavn(e.target.value)}
          style={{ marginBottom: '0.5rem' }}
        />
        <VirksomhetVelger
          virksomheter={virksomheter}
          value={valgtVirksomhetId}
          onChange={setValgtVirksomhetId}
          label="Er egentlig virksomheten"
          tomValgTekst="Velg virksomhet …"
          style={{ marginBottom: '0.75rem' }}
        />
        {navnekandidatId && (
          <Checkbox
            label="Godkjenn også navnekandidaten (markeres som «Godkjent» i stedet for «Venter»)"
            checked={godkjennKandidatOgsa}
            onChange={(e) => setGodkjennKandidatOgsa(e.target.checked)}
            style={{ marginBottom: '0.75rem' }}
          />
        )}
        {feil && <Alert data-color="danger" style={{ marginBottom: '0.5rem' }}>{feil}</Alert>}
        {kandidatFeil && (
          <Alert data-color="warning" style={{ marginBottom: '0.5rem' }}>
            Navneformen ble koblet, men godkjenning av kandidaten feilet: {kandidatFeil}
          </Alert>
        )}
        {suksess && !feil && (
          <Alert data-color="success" style={{ marginBottom: '0.5rem' }}>
            «{suksess.navn}» er nå lagt til som navneform for {suksess.virksomhetNavn}.
            {suksess.kandidatGodkjent && ' Navnekandidaten er markert som godkjent.'}{' '}
            <Link asChild><RouterLink to={`/virksomheter/${suksess.virksomhetId}`}>Se {suksess.virksomhetNavn} ↗</RouterLink></Link>
          </Alert>
        )}
        <Button type="submit" disabled={kobler || !navn.trim() || !valgtVirksomhetId}>
          {kobler ? 'Kobler …' : 'Legg til som navneform'}
        </Button>
      </form>
    </Card>
  );
}
