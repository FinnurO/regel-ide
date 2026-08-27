import { useState, type Dispatch, type FormEvent, type SetStateAction } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Field, Heading, Label, Link, Paragraph, Select, Spinner, Tabs, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { GYLDIGE_HANDLINGSTYPER, GYLDIGE_UTFORT_AV } from '../api/types';
import type { HandlingDto, TjenesteRegelverksreferanseDto } from '../api/types';

export interface HandlingerFaneProps {
  tjenesteId: string;
  handlinger: HandlingDto[] | null;
  setHandlinger: Dispatch<SetStateAction<HandlingDto[] | null>>;
  /** ALLE referanser (flate + felt-koblede) — KI-forslaget bruker settet av unike rettskilder herfra som kontekst. */
  referanser: TjenesteRegelverksreferanseDto[] | null;
}

/**
 * Handlinger-fanen — nå unionen av EIDE + sekundært KOBLEDE handlinger (2026-08-27, se
 * `HandlingTjenesteEntitet`). Ny "Koble eksisterende"/"Opprett ny"-fanevalg der "Opprett ny" er
 * uendret fra tidligere, og "Koble eksisterende" søker EGEN virksomhets handlinger og kobler en
 * allerede eksisterende inn i tillegg til sin egentlige eier.
 */
export function HandlingerFane({ tjenesteId, handlinger, setHandlinger, referanser }: HandlingerFaneProps) {
  const [modus, setModus] = useState<'koble' | 'opprett'>('koble');

  const [nyHandlingNavn, setNyHandlingNavn] = useState('');
  const [nyHandlingType, setNyHandlingType] = useState<string>(GYLDIGE_HANDLINGSTYPER[0]);
  const [nyHandlingUtfortAv, setNyHandlingUtfortAv] = useState('');
  const [leggerTilHandling, setLeggerTilHandling] = useState(false);
  const [handlingFeil, setHandlingFeil] = useState<string | null>(null);

  const [sok, setSok] = useState('');
  const [sokTreff, setSokTreff] = useState<HandlingDto[]>([]);
  const [sokerLaster, setSokerLaster] = useState(false);

  const [handlingsforslagKjorer, setHandlingsforslagKjorer] = useState(false);
  const [handlingsforslagFeil, setHandlingsforslagFeil] = useState<string | null>(null);
  const [handlingsforslagMelding, setHandlingsforslagMelding] = useState<string | null>(null);

  async function lastPaNytt() {
    setHandlinger(await api.hentHandlinger(tjenesteId));
  }

  function sokEtterHandling(verdi: string) {
    setSok(verdi);
    if (!verdi.trim()) { setSokTreff([]); return; }
    setSokerLaster(true);
    api.sokHandlingRegister(verdi.trim())
      .then(setSokTreff)
      .catch(() => setSokTreff([]))
      .finally(() => setSokerLaster(false));
  }

  async function koble(handlingId: string) {
    setHandlingFeil(null);
    try {
      await api.kobleHandlingTilTjeneste(tjenesteId, { handlingId });
      await lastPaNytt();
      setSok('');
      setSokTreff([]);
    } catch (err) {
      setHandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av handling.');
    }
  }

  async function opprettHandling(e: FormEvent) {
    e.preventDefault();
    if (!nyHandlingNavn.trim()) return;
    setHandlingFeil(null);
    setLeggerTilHandling(true);
    try {
      await api.opprettHandling(tjenesteId, {
        navn: nyHandlingNavn.trim(), handlingstype: nyHandlingType, bruksomraade: null,
        utfortAv: nyHandlingUtfortAv || null, kanaler: null, behandlingstid: null, kostnad: null,
        vedlegg: null, veiledningstekst: null, arsaker: null, resultat: null, merknad: null,
      });
      await lastPaNytt();
      setNyHandlingNavn('');
      setNyHandlingUtfortAv('');
    } catch (err) {
      setHandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av handling.');
    } finally {
      setLeggerTilHandling(false);
    }
  }

  /** Bruker tjenestens EGNE koblede regelverksreferansers rettskilder som KI-kontekst — se
   * tidligere begrunnelse i den opprinnelige TjenesteDetalj.tsx (handlingsforslag-ki-omfang-runden). */
  async function foreslaHandlinger() {
    const rettskildeIder = [...new Set((referanser ?? []).map((r) => r.tilRettskildeId))];
    if (rettskildeIder.length === 0) {
      setHandlingsforslagFeil('Ingen regelverksreferanser koblet til denne tjenesten ennå — koble minst én under «Regelverksreferanser» først.');
      return;
    }
    setHandlingsforslagFeil(null);
    setHandlingsforslagMelding(null);
    setHandlingsforslagKjorer(true);
    try {
      const respons = await api.kjorHandlingsforslag(tjenesteId, { rettskildeIder });
      await lastPaNytt();
      setHandlingsforslagMelding(
        respons.melding ?? `${respons.forslag.length} handling(er) foreslått av KI-en (status "foreslått av KI").`,
      );
    } catch (err) {
      setHandlingsforslagFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kjøring av handlingsforslag.');
    } finally {
      setHandlingsforslagKjorer(false);
    }
  }

  return (
    <div style={{ maxWidth: '900px' }}>
      <Heading level={2} data-size="xs" style={{ marginBottom: '0.75rem' }}>Handlinger</Heading>
      <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
        Konkrete, tidsavgrensede interaksjoner knyttet til denne rettigheten (søknad, melding, klage …) —
        de den EIER pluss de den er sekundært koblet til (en handling kan gjenbrukes fra en annen tjeneste).
      </Paragraph>
      <div style={{ marginBottom: '0.75rem' }}>
        <Button variant="secondary" data-size="sm" onClick={foreslaHandlinger} disabled={handlingsforslagKjorer}
          title="Bruker tjenestens koblede regelverksreferanser som KI-kontekst">
          {handlingsforslagKjorer ? 'Foreslår handlinger …' : 'Foreslå handlinger (KI)'}
        </Button>
        {handlingsforslagFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{handlingsforslagFeil}</Alert>}
        {handlingsforslagMelding && <Alert data-color="info" style={{ marginTop: '0.5rem' }}>{handlingsforslagMelding}</Alert>}
      </div>

      {handlinger === null && <Spinner aria-label="Laster …" data-size="sm" />}
      {handlinger && handlinger.length === 0 && <Paragraph>Ingen handlinger registrert ennå.</Paragraph>}
      {handlinger && handlinger.length > 0 && (
        <div style={{ border: '1px solid var(--ds-color-neutral-border-subtle)', borderRadius: 'var(--ds-border-radius-md)', overflow: 'hidden', marginBottom: '1.25rem' }}>
          {handlinger.map((h) => (
            <div key={h.id} style={{
              display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 1fr', gap: '0.5rem', alignItems: 'center',
              padding: '0.6rem 0.9rem', borderBottom: '1px solid var(--ds-color-neutral-border-subtle)', fontSize: 'var(--ds-font-size-1)',
            }}>
              {/* h.tjenesteId er ALLTID handlingens EIENDE tjeneste (uendret av en sekundær kobling), se HandlingTjenesteEntitet. */}
              <Link asChild><RouterLink to={`/tjenester/${h.tjenesteId}/handlinger/${h.id}`}>{h.navn}</RouterLink></Link>
              <Tag data-color="info" data-size="sm">{h.handlingstype}</Tag>
              <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>{h.utfortAv ?? '—'}</span>
              <Tag data-color="neutral" data-size="sm">{h.status}</Tag>
            </div>
          ))}
        </div>
      )}

      <Card style={{ maxWidth: '640px', padding: '1rem 1.25rem' }}>
        <Tabs value={modus} onChange={(v) => setModus(v as 'koble' | 'opprett')} style={{ marginBottom: '0.75rem' }}>
          <Tabs.List>
            <Tabs.Tab value="koble">Koble eksisterende</Tabs.Tab>
            <Tabs.Tab value="opprett">Opprett ny</Tabs.Tab>
          </Tabs.List>
        </Tabs>

        {modus === 'koble' ? (
          <>
            <Textfield data-size="sm" label="Søk blant egen virksomhets handlinger" value={sok}
              onChange={(e) => sokEtterHandling(e.target.value)} style={{ marginBottom: '0.5rem' }} />
            {sokerLaster && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Søker …</Paragraph>}
            {!sokerLaster && sok.trim() && sokTreff.length === 0 && (
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Ingen treff.</Paragraph>
            )}
            {sokTreff.length > 0 && (
              <ul style={{ maxHeight: '12rem', overflow: 'auto', marginBottom: '0.5rem' }}>
                {sokTreff.map((h) => (
                  <li key={h.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.2rem' }}>
                    <span style={{ flex: 1, fontSize: 'var(--ds-font-size-1)' }}>
                      {h.navn} <Tag data-color="info" data-size="sm">{h.handlingstype}</Tag>
                    </span>
                    <Button data-size="sm" variant="tertiary" onClick={() => koble(h.id)}>Koble</Button>
                  </li>
                ))}
              </ul>
            )}
          </>
        ) : (
          <form onSubmit={opprettHandling} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <Textfield data-size="sm" label="Navn på ny handling" value={nyHandlingNavn} onChange={(e) => setNyHandlingNavn(e.target.value)} required />
            <Field>
              <Label>Handlingstype</Label>
              <Select data-size="sm" value={nyHandlingType} onChange={(e) => setNyHandlingType(e.target.value)}>
                {GYLDIGE_HANDLINGSTYPER.map((t) => <Select.Option key={t} value={t}>{t}</Select.Option>)}
              </Select>
            </Field>
            <Field>
              <Label>Utført av</Label>
              <Select data-size="sm" value={nyHandlingUtfortAv} onChange={(e) => setNyHandlingUtfortAv(e.target.value)}>
                <Select.Option value="">Ikke satt</Select.Option>
                {GYLDIGE_UTFORT_AV.map((u) => <Select.Option key={u} value={u}>{u}</Select.Option>)}
              </Select>
            </Field>
            <Button data-size="sm" type="submit" disabled={leggerTilHandling || !nyHandlingNavn.trim()}>
              {leggerTilHandling ? 'Oppretter …' : 'Opprett handling'}
            </Button>
          </form>
        )}
        {handlingFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{handlingFeil}</Alert>}
      </Card>
    </div>
  );
}
