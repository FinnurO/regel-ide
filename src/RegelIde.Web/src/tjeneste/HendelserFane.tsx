import { useState, type Dispatch, type FormEvent, type SetStateAction } from 'react';
import { Alert, Button, Field, Heading, Label, Paragraph, Select, Spinner, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { HendelseDto } from '../api/types';
import type { DetaljVisning } from './detaljVisning';

export interface HendelserFaneProps {
  tjenesteId: string;
  hendelser: HendelseDto[] | null;
  setHendelser: Dispatch<SetStateAction<HendelseDto[] | null>>;
  alleHendelser: HendelseDto[];
  setAlleHendelser: Dispatch<SetStateAction<HendelseDto[]>>;
  onSelectDetail: (v: DetaljVisning) => void;
}

/** Uendret funksjon fra tidligere Hendelser-seksjonen, kun flyttet ut i egen fil. */
export function HendelserFane({ tjenesteId, hendelser, setHendelser, alleHendelser, setAlleHendelser, onSelectDetail }: HendelserFaneProps) {
  const [nyHendelseId, setNyHendelseId] = useState('');
  const [leggerTilHendelse, setLeggerTilHendelse] = useState(false);
  const [visNyHendelse, setVisNyHendelse] = useState(false);
  const [nyHendelseNavn, setNyHendelseNavn] = useState('');
  const [nyHendelseType, setNyHendelseType] = useState('virksomhetshendelse');
  const [hendelseFeil, setHendelseFeil] = useState<string | null>(null);

  async function kobleHendelse(e: FormEvent) {
    e.preventDefault();
    if (!nyHendelseId) return;
    setHendelseFeil(null);
    setLeggerTilHendelse(true);
    try {
      const oppdatert = await api.kobleTjenesteHendelse(tjenesteId, { hendelseId: nyHendelseId });
      setHendelser(oppdatert);
      setNyHendelseId('');
    } catch (err) {
      setHendelseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av hendelse.');
    } finally {
      setLeggerTilHendelse(false);
    }
  }

  async function opprettOgKobleHendelse(e: FormEvent) {
    e.preventDefault();
    if (!nyHendelseNavn.trim()) return;
    setHendelseFeil(null);
    setLeggerTilHendelse(true);
    try {
      const hendelse = await api.opprettHendelse({ navn: nyHendelseNavn.trim(), type: nyHendelseType, beskrivelse: null });
      setAlleHendelser((forrige) => [...forrige, hendelse]);
      const oppdatert = await api.kobleTjenesteHendelse(tjenesteId, { hendelseId: hendelse.id });
      setHendelser(oppdatert);
      setNyHendelseNavn('');
      setVisNyHendelse(false);
    } catch (err) {
      setHendelseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av hendelse.');
    } finally {
      setLeggerTilHendelse(false);
    }
  }

  async function fjernHendelse(hendelseId: string) {
    await api.fjernTjenesteHendelse(tjenesteId, hendelseId);
    setHendelser((forrige) => (forrige ?? []).filter((h) => h.id !== hendelseId));
  }

  return (
    <div style={{ maxWidth: '760px' }}>
      <Heading level={2} data-size="xs" style={{ marginBottom: '0.75rem' }}>Hendelser</Heading>
      <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
        Ren, symmetrisk klassifisering (docs/03-domenemodell.md §1.5) — ingen retning. To tjenester som
        deler samme hendelse blir relaterte uten at én forårsaker den andre.
      </Paragraph>
      {hendelser === null && <Spinner aria-label="Laster …" data-size="sm" />}
      {hendelser && hendelser.length === 0 && <Paragraph>Ingen hendelser koblet ennå.</Paragraph>}
      {hendelser && hendelser.length > 0 && (
        <ul style={{ marginBottom: '1rem' }}>
          {hendelser.map((h) => (
            <li key={h.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <button type="button" onClick={() => onSelectDetail({ title: h.navn, meta: `Hendelse · ${h.type}`, body: h.beskrivelse })}
                style={{ background: 'none', border: 'none', padding: 0, font: 'inherit', color: 'inherit', cursor: 'pointer' }}>
                {h.navn}
              </button>
              <Tag data-color="neutral" data-size="sm">{h.type}</Tag>
              <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernHendelse(h.id)}>Fjern</Button>
            </li>
          ))}
        </ul>
      )}

      <form onSubmit={kobleHendelse} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '0.5rem' }}>
        <Field>
          <Label>Eksisterende hendelse</Label>
          <Select data-size="sm" value={nyHendelseId} onChange={(e) => setNyHendelseId(e.target.value)}>
            <Select.Option value="">Velg …</Select.Option>
            {alleHendelser
              .filter((h) => !(hendelser ?? []).some((koblet) => koblet.id === h.id))
              .map((h) => <Select.Option key={h.id} value={h.id}>{h.navn} ({h.type})</Select.Option>)}
          </Select>
        </Field>
        <Button data-size="sm" type="submit" disabled={leggerTilHendelse || !nyHendelseId}>
          {leggerTilHendelse ? 'Kobler …' : 'Koble hendelse'}
        </Button>
        <Button data-size="sm" variant="tertiary" onClick={() => setVisNyHendelse((v) => !v)}>
          {visNyHendelse ? 'Avbryt' : '+ Ny hendelse'}
        </Button>
      </form>
      {visNyHendelse && (
        <form onSubmit={opprettOgKobleHendelse} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '0.5rem' }}>
          <Textfield data-size="sm" label="Navn på ny hendelse" value={nyHendelseNavn} onChange={(e) => setNyHendelseNavn(e.target.value)} required />
          <Field>
            <Label>Type</Label>
            <Select data-size="sm" value={nyHendelseType} onChange={(e) => setNyHendelseType(e.target.value)}>
              <Select.Option value="generell">Generell (cv:Event)</Select.Option>
              <Select.Option value="livshendelse">Livshendelse</Select.Option>
              <Select.Option value="virksomhetshendelse">Virksomhetshendelse</Select.Option>
            </Select>
          </Field>
          <Button data-size="sm" type="submit" disabled={leggerTilHendelse || !nyHendelseNavn.trim()}>
            {leggerTilHendelse ? 'Oppretter …' : 'Opprett og koble'}
          </Button>
        </form>
      )}
      {hendelseFeil && <Alert data-color="danger">{hendelseFeil}</Alert>}
    </div>
  );
}
