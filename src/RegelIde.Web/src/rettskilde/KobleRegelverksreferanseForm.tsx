import { useState, type FormEvent } from 'react';
import { Button, Field, Label, Select, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RettskildeNodeDto, RettskildeSammendrag, TjenesteRegelverksreferanseDto } from '../api/types';
import { RettskildeVelger } from './RettskildeVelger';

export interface KobleRegelverksreferanseFormProps {
  tjenesteId: string;
  /** `undefined`/`null` = knytt til HELE tjenesten (den flate listen i Regelverksreferanser-fanen).
   * Satt = knytt til ETT bestemt felt (§-knappen på et Innhold-felt) — se feltnøkkel-konvensjonen
   * i api/tjenesteFelt.ts. */
  felt?: string | null;
  rettskilder: RettskildeSammendrag[];
  /** Delt cache fra siden (samme Map brukt av Regelverksreferanser-fanens grupperte visning) —
   * unngår at hver enkelt feltform henter nodene for samme rettskilde på nytt. */
  noderPerRettskilde: Map<string, RettskildeNodeDto[]>;
  sikreNoderFor: (rettskildeId: string) => void;
  onKoblet: (ny: TjenesteRegelverksreferanseDto) => void;
  /** Mindre, tettere layout for inline bruk under et enkelt Innhold-felt. */
  kompakt?: boolean;
}

/**
 * «Koble ny referanse»-formen — trukket ut fra `TjenesteDetalj.tsx` (Tjenestedetalj-redesignrunden
 * 2026-08-27) til en delt komponent, brukt BÅDE fra Regelverksreferanser-fanen (uten `felt`, dagens
 * opprinnelige oppførsel) og fra hvert Innhold-felts "§"-knapp (med `felt` satt). Rettskilde-delen
 * bruker `RettskildeVelger` (søkbar, docs/09 §10.1) i stedet for et rått `<Select>` over alle 5893
 * rettskilder — det SISTE gjenværende stedet med det mønsteret, se docs/22 §5.
 */
export function KobleRegelverksreferanseForm({
  tjenesteId, felt, rettskilder, noderPerRettskilde, sikreNoderFor, onKoblet, kompakt,
}: KobleRegelverksreferanseFormProps) {
  const [rettskildeId, setRettskildeId] = useState('');
  const [eid, setEid] = useState('');
  const [kobler, setKobler] = useState(false);
  const [feilmelding, setFeilmelding] = useState<string | null>(null);

  function velgRettskilde(id: string) {
    setRettskildeId(id);
    setEid('');
    if (id) sikreNoderFor(id);
  }

  // Samme filter som RettskildeDetalj/eidLenker-bruken andre steder: kun blad-noder med en faktisk
  // paragraf/nummer, PLUSS "side"-noder (en Brukerveiledning har ingen paragrafinndeling, §3.1, men
  // ER selv en reell, hel referanse — se docs/22s funn om dette).
  const paragrafKandidater = (noderPerRettskilde.get(rettskildeId) ?? [])
    .filter((n) => n.nodeType === 'side' || (n.nodeType !== 'kapittel' && n.nummer));

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!rettskildeId || !eid.trim()) return;
    setFeilmelding(null);
    setKobler(true);
    try {
      const ny = await api.kobleTjenesteRegelverksreferanse(tjenesteId, {
        tilRettskildeId: rettskildeId, tilEid: eid.trim(), felt: felt ?? null,
      });
      onKoblet(ny);
      setEid('');
    } catch (err) {
      setFeilmelding(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av referanse.');
    } finally {
      setKobler(false);
    }
  }

  const storrelse = kompakt ? 'sm' : undefined;

  return (
    <form onSubmit={submit} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
      <RettskildeVelger rettskilder={rettskilder} value={rettskildeId} onChange={velgRettskilde} label="Rettskilde" />
      {rettskildeId && paragrafKandidater.length > 0 && (
        <Field style={{ maxWidth: '16rem' }}>
          <Label>Paragraf</Label>
          <Select data-size={storrelse} value={eid} onChange={(ev) => setEid(ev.target.value)}>
            <Select.Option value="">Velg …</Select.Option>
            {paragrafKandidater.map((n) => (
              <Select.Option key={n.id} value={n.eid}>
                {n.nodeType === 'side' ? 'Hele siden' : n.nummer}{n.overskrift ? ` — ${n.overskrift}` : ''}
              </Select.Option>
            ))}
          </Select>
        </Field>
      )}
      <Textfield data-size={storrelse} label="Avansert / manuell eId" value={eid}
        onChange={(ev) => setEid(ev.target.value)} style={{ minWidth: kompakt ? '14rem' : '22rem', fontFamily: 'monospace' }} />
      <Button data-size={storrelse} type="submit" disabled={kobler || !rettskildeId || !eid.trim()}>
        {kobler ? 'Kobler …' : 'Koble referanse'}
      </Button>
      {feilmelding && <span style={{ color: 'var(--ds-color-danger-text-default)', fontSize: 'var(--ds-font-size-1)', width: '100%' }}>{feilmelding}</span>}
    </form>
  );
}
