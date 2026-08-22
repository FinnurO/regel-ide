import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { Alert, Button, Checkbox, Heading, Paragraph, Spinner, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { LovdataKatalogTreffDto, RettskildeDetalj, RettskildeNodeDto } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';

export default function Importer() {
  const navigate = useNavigate();
  const { gjeldendeBruker } = useBruker();

  const [sokestreng, setSokestreng] = useState('');
  const [treff, setTreff] = useState<LovdataKatalogTreffDto[]>([]);
  const [sokerLaster, setSokerLaster] = useState(false);
  const [lovdataFeil, setLovdataFeil] = useState<string | null>(null);
  const [lovdataLaster, setLovdataLaster] = useState(false);

  useEffect(() => {
    if (!sokestreng.trim()) {
      setTreff([]);
      return;
    }
    setSokerLaster(true);
    const tidsavbrudd = setTimeout(() => {
      api.sokLovdataKatalog(sokestreng.trim())
        .then(setTreff)
        .catch(() => setTreff([]))
        .finally(() => setSokerLaster(false));
    }, 300);
    return () => clearTimeout(tidsavbrudd);
  }, [sokestreng]);

  const [fil, setFil] = useState<File | null>(null);
  const [erVirksomhetensEgen, setErVirksomhetensEgen] = useState(false);
  const [filFeil, setFilFeil] = useState<string | null>(null);
  const [filLaster, setFilLaster] = useState(false);

  // Importbekreftelse (AK-3.3.6) — vises i stedet for skjemaene etter en vellykket import.
  const [importertId, setImportertId] = useState<string | null>(null);
  const [kildetekst, setKildetekst] = useState<string | null>(null);
  const [detalj, setDetalj] = useState<RettskildeDetalj | null>(null);
  const [noder, setNoder] = useState<RettskildeNodeDto[] | null>(null);
  const [kortnavn, setKortnavn] = useState('');
  const [utgiver, setUtgiver] = useState('');
  const [lagrerMetadata, setLagrerMetadata] = useState(false);
  const [metadataFeil, setMetadataFeil] = useState<string | null>(null);

  useEffect(() => {
    if (!importertId) return;
    Promise.all([api.hentRettskilde(importertId), api.hentNoder(importertId)]).then(([d, n]) => {
      setDetalj(d);
      setKortnavn(d.kortnavn ?? '');
      setUtgiver(d.utgiver ?? '');
      setNoder(n);
    });
  }, [importertId]);

  async function importerFraLovdata(datokode: string) {
    setLovdataFeil(null);
    setLovdataLaster(true);
    try {
      const { id } = await api.importerFraLovdata(datokode);
      setKildetekst(null); // ikke tilgjengelig klientsidig for Lovdata-sporet (hentet server-side)
      setImportertId(id);
    } catch (err) {
      setLovdataFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved import fra Lovdata.');
    } finally {
      setLovdataLaster(false);
    }
  }

  async function importerFraFil(e: FormEvent) {
    e.preventDefault();
    if (!fil) return;
    setFilFeil(null);
    setFilLaster(true);
    try {
      const tekst = await fil.text();
      const virksomhetId = erVirksomhetensEgen ? gjeldendeBruker?.virksomhetId : undefined;
      const { id } = await api.importerFraFil(fil, virksomhetId);
      setKildetekst(tekst);
      setImportertId(id);
    } catch (err) {
      setFilFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved import fra fil.');
    } finally {
      setFilLaster(false);
    }
  }

  async function bekreftOgLagre() {
    if (!importertId) return;
    setMetadataFeil(null);
    setLagrerMetadata(true);
    try {
      await api.oppdaterRettskildeMetadata(importertId, { kortnavn: kortnavn.trim() || null, utgiver: utgiver.trim() || null });
      navigate(`/rettskilder/${importertId}`);
    } catch (err) {
      setMetadataFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av metadata.');
    } finally {
      setLagrerMetadata(false);
    }
  }

  if (importertId) {
    return (
      <>
        <Heading level={1} data-size="lg">
          Bekreft import
        </Heading>
        <Paragraph style={{ marginBottom: '1.5rem' }}>
          {detalj ? detalj.tittel : 'Laster …'} — sjekk at metadata er riktig tolket, og sammenlign kildeteksten mot
          strukturen regel-IDE har tolket ut av den (AK-3.3.6), før du fortsetter.
        </Paragraph>

        <div style={{ display: 'flex', gap: '1rem', marginBottom: '1.5rem', maxWidth: '40rem' }}>
          <Textfield label="Kortnavn" value={kortnavn} onChange={(e) => setKortnavn(e.target.value)} style={{ flex: 1 }} />
          <Textfield label="Utgiver" value={utgiver} onChange={(e) => setUtgiver(e.target.value)} style={{ flex: 1 }} />
        </div>

        <div style={{ display: 'flex', gap: '1.5rem', alignItems: 'flex-start', marginBottom: '1.5rem' }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
              Kildetekst
            </Heading>
            {kildetekst ? (
              <pre
                style={{
                  maxHeight: '50vh', overflow: 'auto', margin: 0, padding: '0.75rem',
                  background: 'var(--ds-color-neutral-surface-default)',
                  border: '1px solid var(--ds-color-neutral-border-subtle)',
                  borderRadius: 'var(--ds-border-radius-md)',
                  fontSize: '0.75rem', whiteSpace: 'pre-wrap', wordBreak: 'break-word',
                }}
              >
                {kildetekst}
              </pre>
            ) : (
              <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
                Kildetekst er ikke tilgjengelig her for Lovdata-import (hentet server-side) — kun for filopplasting.
              </Paragraph>
            )}
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
              Tolket struktur ({noder?.length ?? 0} noder)
            </Heading>
            <div
              style={{
                maxHeight: '50vh', overflow: 'auto', padding: '0.75rem',
                border: '1px solid var(--ds-color-neutral-border-subtle)',
                borderRadius: 'var(--ds-border-radius-md)',
              }}
            >
              {noder ? (
                noder.map((n) => (
                  <div key={n.id} style={{ fontSize: 'var(--ds-font-size-2)', marginBottom: '0.25rem' }}>
                    {n.nummer ?? n.nodeType}
                    {n.overskrift && ` — ${n.overskrift}`}
                    {n.opphevet && ' (Opphevet)'}
                  </div>
                ))
              ) : (
                <Spinner aria-label="Laster …" data-size="sm" />
              )}
            </div>
          </div>
        </div>

        {metadataFeil && <Alert data-color="danger">{metadataFeil}</Alert>}
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <Button onClick={bekreftOgLagre} disabled={lagrerMetadata}>
            {lagrerMetadata ? 'Lagrer …' : 'Bekreft og lagre'}
          </Button>
          <Button variant="tertiary" onClick={() => navigate(`/rettskilder/${importertId}`)}>
            Hopp over
          </Button>
        </div>
      </>
    );
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Importer rettskilde
      </Heading>
      <Paragraph style={{ marginBottom: '1.5rem' }}>
        Innlogget som <strong>{gjeldendeBruker?.navn ?? '(ingen testbruker valgt)'}</strong>,{' '}
        {gjeldendeBruker?.virksomhetNavn}.
      </Paragraph>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm">
          Fra Lovdata (søk)
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem' }}>
          Søk i en lokal, søkbar katalog over Lovdatas bulk-datasett (fornyes automatisk) — velg et treff
          for å hente og konvertere direkte fra Lovdatas offisielle datasett. Alltid en delt/nasjonal
          kilde (Lov eller Forskrift) — passer ikke for lokale forskrifter eller virksomhetsdokumenter.
        </Paragraph>
        <Textfield
          label="Søk etter tittel"
          placeholder="f.eks. alkoholloven"
          value={sokestreng}
          onChange={(e) => setSokestreng(e.target.value)}
          style={{ maxWidth: '30rem', marginBottom: '0.75rem' }}
        />
        {sokerLaster && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Søker …</Paragraph>}
        {!sokerLaster && sokestreng.trim() && treff.length === 0 && (
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Ingen treff.</Paragraph>
        )}
        {treff.length > 0 && (
          <ul style={{ maxHeight: '20rem', overflow: 'auto' }}>
            {treff.map((t) => (
              <li key={t.datokode} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.3rem' }}>
                <span style={{ flex: 1 }}>
                  {t.tittel} <span style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>({t.datokode}, {t.type})</span>
                </span>
                <Button data-size="sm" disabled={lovdataLaster} onClick={() => importerFraLovdata(t.datokode)}>
                  {lovdataLaster ? 'Importerer …' : 'Importer'}
                </Button>
              </li>
            ))}
          </ul>
        )}
        {lovdataFeil && <Alert data-color="danger" style={{ marginTop: '0.75rem' }}>{lovdataFeil}</Alert>}
      </section>

      <section>
        <Heading level={2} data-size="sm">
          Fra fil
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem' }}>
          Laster opp en HTML-fil i Lovdatas «XML-kompatible HTML»-format (samme struktur som bulk-
          datasettet). Nettsidens HTML-format for lokale forskrifter (lovdata.no/dokument/LF/…) er
          <strong> ikke</strong> støttet ennå — se src/README.md.
        </Paragraph>
        <form onSubmit={importerFraFil}>
          <Textfield
            type="file"
            label="Velg fil"
            accept=".html,text/html"
            onChange={(e) => setFil(e.target.files?.[0] ?? null)}
            required
          />
          <Checkbox
            label={`Dette er ${gjeldendeBruker?.virksomhetNavn ?? 'min virksomhet'} sin egen lokale kilde (ikke en delt/nasjonal kilde)`}
            checked={erVirksomhetensEgen}
            onChange={(e) => setErVirksomhetensEgen(e.target.checked)}
            style={{ margin: '0.75rem 0' }}
          />
          <Button type="submit" disabled={filLaster || !fil}>
            {filLaster ? 'Importerer …' : 'Last opp og importer'}
          </Button>
        </form>
        {filFeil && <Alert data-color="danger" style={{ marginTop: '0.75rem' }}>{filFeil}</Alert>}
      </section>
    </>
  );
}
