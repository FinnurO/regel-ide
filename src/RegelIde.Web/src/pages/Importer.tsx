import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Checkbox, Heading, Paragraph, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { useBruker } from '../bruker/BrukerContext';

export default function Importer() {
  const navigate = useNavigate();
  const { gjeldendeBruker } = useBruker();

  const [datokode, setDatokode] = useState('');
  const [lovdataFeil, setLovdataFeil] = useState<string | null>(null);
  const [lovdataLaster, setLovdataLaster] = useState(false);

  const [fil, setFil] = useState<File | null>(null);
  const [erVirksomhetensEgen, setErVirksomhetensEgen] = useState(false);
  const [filFeil, setFilFeil] = useState<string | null>(null);
  const [filLaster, setFilLaster] = useState(false);

  async function importerFraLovdata(e: FormEvent) {
    e.preventDefault();
    setLovdataFeil(null);
    setLovdataLaster(true);
    try {
      const { id } = await api.importerFraLovdata(datokode.trim());
      navigate(`/rettskilder/${id}`);
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
      const virksomhetId = erVirksomhetensEgen ? gjeldendeBruker?.virksomhetId : undefined;
      const { id } = await api.importerFraFil(fil, virksomhetId);
      navigate(`/rettskilder/${id}`);
    } catch (err) {
      setFilFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved import fra fil.');
    } finally {
      setFilLaster(false);
    }
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
          Fra Lovdata (datokode)
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem' }}>
          Henter og konverterer direkte fra Lovdatas offisielle bulk-datasett. Alltid en delt/nasjonal
          kilde (Lov eller Forskrift) — passer ikke for lokale forskrifter eller virksomhetsdokumenter.
        </Paragraph>
        <form onSubmit={importerFraLovdata} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end' }}>
          <Textfield
            label="Datokode"
            placeholder="f.eks. LOV-1989-06-02-27"
            value={datokode}
            onChange={(e) => setDatokode(e.target.value)}
            required
          />
          <Button type="submit" disabled={lovdataLaster || !datokode.trim()}>
            {lovdataLaster ? 'Importerer …' : 'Importer'}
          </Button>
        </form>
        {lovdataFeil && <div className="feilmelding" style={{ marginTop: '0.75rem' }}>{lovdataFeil}</div>}
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
        {filFeil && <div className="feilmelding" style={{ marginTop: '0.75rem' }}>{filFeil}</div>}
      </section>
    </>
  );
}
