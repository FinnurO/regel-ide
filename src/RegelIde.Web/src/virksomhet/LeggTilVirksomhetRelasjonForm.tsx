import { useEffect, useState } from 'react';
import { Alert, Button, Card, Field, Label, Select, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RelasjonsTypeKonfigurasjonDto, RettskildeSammendrag, VirksomhetDto, VirksomhetRelasjonDto } from '../api/types';
import { VirksomhetVelger } from './VirksomhetVelger';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';

export interface LeggTilVirksomhetRelasjonFormProps {
  virksomhetId: string;
  virksomheter: VirksomhetDto[];
  rettskilder: RettskildeSammendrag[];
  onOpprettet: (nye: VirksomhetRelasjonDto[]) => void;
}

/**
 * «Legg til relasjon»-skjema for VirksomhetRelasjon (docs/28, docs/29 §Del C) — kobler DENNE
 * virksomheten (alltid Fra-siden, se `POST /api/virksomheter/{id}/relasjoner`) til en annen konkret
 * virksomhet, med en typet relasjonstype hentet fra `GET /api/konfigurasjon/relasjonstyper`. Samme
 * fil-per-skjema-konvensjon som `LeggTilMyndighetstildelingForm.tsx`, samme `VirksomhetVelger`/
 * `RettskildeVelger`-gjenbruk som resten av UI-et.
 */
export function LeggTilVirksomhetRelasjonForm({ virksomhetId, virksomheter, rettskilder, onOpprettet }: LeggTilVirksomhetRelasjonFormProps) {
  const [typer, setTyper] = useState<RelasjonsTypeKonfigurasjonDto[] | null>(null);
  const [tilVirksomhetId, setTilVirksomhetId] = useState('');
  const [relasjonsType, setRelasjonsType] = useState('');
  const [hjemmelRettskildeId, setHjemmelRettskildeId] = useState('');
  const [hjemmelEid, setHjemmelEid] = useState('');
  const [kommentar, setKommentar] = useState('');

  const [oppretter, setOppretter] = useState(false);
  const [feilmelding, setFeilmelding] = useState<string | null>(null);

  useEffect(() => {
    api.hentRelasjonstyper().then(setTyper).catch(() => setTyper([]));
  }, []);

  // Andre virksomheter enn denne selv — en relasjon til seg selv avvises uansett server-side, men
  // ingen grunn til å tilby det som et valg i det hele tatt.
  const andreVirksomheter = virksomheter.filter((v) => v.id !== virksomhetId);

  async function opprett() {
    if (!tilVirksomhetId || !relasjonsType) return;
    setFeilmelding(null);
    setOppretter(true);
    try {
      const nye = await api.opprettVirksomhetRelasjon(virksomhetId, {
        tilVirksomhetId, relasjonsType, hjemmelRettskildeId: hjemmelRettskildeId || null,
        hjemmelEid: hjemmelEid.trim() || null, kommentar: kommentar.trim() || null,
      });
      onOpprettet(nye);
      setTilVirksomhetId('');
      setRelasjonsType('');
      setHjemmelRettskildeId('');
      setHjemmelEid('');
      setKommentar('');
    } catch (err) {
      setFeilmelding(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av relasjon.');
    } finally {
      setOppretter(false);
    }
  }

  return (
    <Card style={{ padding: '1rem', marginTop: '0.75rem' }}>
      <div style={{ marginBottom: '0.75rem' }}>
        <VirksomhetVelger virksomheter={andreVirksomheter} value={tilVirksomhetId} onChange={setTilVirksomhetId}
          label="Motpart (annen virksomhet)" tomValgTekst="Velg virksomhet …" />
      </div>

      <Field data-size="sm" style={{ maxWidth: '24rem', marginBottom: '0.75rem' }}>
        <Label>Relasjonstype</Label>
        <Select data-size="sm" value={relasjonsType} onChange={(e) => setRelasjonsType(e.target.value)} disabled={!typer}>
          <Select.Option value="">{typer ? 'Velg relasjonstype …' : 'Laster …'}</Select.Option>
          {typer?.map((t) => (
            <Select.Option key={t.kode} value={t.kode}>
              {t.kode} — «{t.fraVisningsmal.replace('{0}', 'motparten')}»
            </Select.Option>
          ))}
        </Select>
      </Field>

      <div style={{ marginBottom: '0.75rem' }}>
        <RettskildeVelger rettskilder={rettskilder} value={hjemmelRettskildeId} onChange={setHjemmelRettskildeId}
          label="Hjemmel (valgfritt)" />
      </div>

      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
        <Textfield data-size="sm" label="Hjemmel-eId (valgfritt, avansert)" value={hjemmelEid}
          onChange={(e) => setHjemmelEid(e.target.value)} style={{ flex: 1, minWidth: '14rem', fontFamily: 'monospace' }} />
        <Textfield data-size="sm" label="Kommentar (valgfritt)" placeholder="f.eks. lenke til org-kart når det ikke finnes en formell hjemmel"
          value={kommentar} onChange={(e) => setKommentar(e.target.value)} style={{ flex: 2, minWidth: '16rem' }} />
      </div>

      <Button type="button" onClick={opprett} disabled={oppretter || !tilVirksomhetId || !relasjonsType}>
        {oppretter ? 'Oppretter …' : 'Opprett relasjon'}
      </Button>
      {feilmelding && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{feilmelding}</Alert>}
    </Card>
  );
}
