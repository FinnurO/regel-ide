import { useState } from 'react';
import { Button, Dialog, Paragraph, Tag } from '@digdir/designsystemet-react';

/**
 * Den delte 6-trinns statusmodellen brukt av Vilkår, Regelnode, Unntak, Begrep og Handling
 * (docs/30 §1.6/§3.3 — saksbehandlertilpasningen). Flyttet hit fra separate lokale
 * `const STATUSER = [...]`-konstanter i hver detaljside/panel — samme verdier, én kilde.
 */
export const STATUS_VERDIER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'] as const;
export type StatusVerdi = typeof STATUS_VERDIER[number];

const STATUS_LABELER: Record<StatusVerdi, string> = {
  utkast: 'Utkast',
  under_revisjon: 'Under revisjon',
  validert: 'Validert',
  publisert: 'Publisert',
  tilbaketrukket: 'Tilbaketrukket',
  arkivert: 'Arkivert',
};

/** Overganger som krever en Dialog-bekreftelse før de utføres (docs/30 §3.3) — nøkkelen er
 * `${fra}->${til}`. Kun de to eksplisitt nevnt i bestillingen; andre overganger (inkl. reversering
 * til et tidligere steg, som var mulig med den gamle <Select>-en) utføres direkte som før. */
const KREVER_BEKREFTELSE = new Set(['publisert->tilbaketrukket', 'publisert->arkivert']);

function erStatusVerdi(v: string): v is StatusVerdi {
  return (STATUS_VERDIER as readonly string[]).includes(v);
}

export interface StatusStepperProps {
  /** Gjeldende statusverdi — en ukjent/ugyldig streng vises uten noe steg fremhevet, ingen gjettet
   * fallback (samme "ikke funnet ≠ oppfunnet"-holdning som ellers i kodebasen). */
  status: string;
  /** Kalles med den NYE statusverdien — etter bekreftelse, for de to overgangene som krever det.
   * Samme rolle som `Select`s `onChange` hadde tidligere: kalleren selv gjør API-kallet. */
  onChange: (nyStatus: StatusVerdi) => void;
  disabled?: boolean;
}

/**
 * Statusflyt som en synlig Tag-rekke (docs/30 §3.3 — saksbehandlertilpasningen) i stedet for en bar
 * `<Select>`-dropdown: gjeldende steg er fremhevet (`data-color="accent"`), passerte steg er
 * `neutral`, fremtidige steg er `outline`. Et klikk på ETHVERT annet steg enn det gjeldende utfører
 * samme handling som `Select`s `onChange` gjorde før (inkludert å gå TILBAKE til et tidligere steg —
 * det var mulig med den gamle dropdownen, og denne komponenten fjerner ingen funksjonalitet, kun
 * legger til en visuell flyt + bekreftelse på de kritiske overgangene).
 * <p>
 * `publisert → tilbaketrukket`/`arkivert` krever en `Dialog`-bekreftelse (Designsystemets
 * komponent-vokabular, første bruk — se docs/09 §0) før handlingen faktisk utføres.
 */
export function StatusStepper({ status, onChange, disabled }: StatusStepperProps) {
  const [ventendeStatus, setVentendeStatus] = useState<StatusVerdi | null>(null);
  const aktivIndeks = erStatusVerdi(status) ? STATUS_VERDIER.indexOf(status) : -1;

  function klikkSteg(steg: StatusVerdi) {
    if (disabled || steg === status) return;
    if (KREVER_BEKREFTELSE.has(`${status}->${steg}`)) {
      setVentendeStatus(steg);
    } else {
      onChange(steg);
    }
  }

  function bekreft() {
    if (ventendeStatus) onChange(ventendeStatus);
    setVentendeStatus(null);
  }

  return (
    <>
      <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', alignItems: 'center' }}>
        {STATUS_VERDIER.map((steg, indeks) => (
          <Tag
            key={steg}
            data-size="sm"
            data-color={indeks === aktivIndeks ? 'accent' : 'neutral'}
            variant={indeks > aktivIndeks ? 'outline' : 'default'}
            style={{ cursor: disabled ? 'default' : 'pointer', opacity: disabled ? 0.6 : 1 }}
            onClick={() => klikkSteg(steg)}
            title={steg === status ? 'Gjeldende status' : `Sett status til «${STATUS_LABELER[steg]}»`}
          >
            {STATUS_LABELER[steg]}
          </Tag>
        ))}
      </div>

      <Dialog open={ventendeStatus !== null} onClose={() => setVentendeStatus(null)} closeButton="Avbryt" style={{ maxWidth: '28rem' }}>
        <Dialog.Block>
          <Paragraph style={{ margin: 0 }}>
            Endre status fra «{erStatusVerdi(status) ? STATUS_LABELER[status] : status}» til «{ventendeStatus ? STATUS_LABELER[ventendeStatus] : ''}»?
            {ventendeStatus === 'tilbaketrukket' && ' Dette markerer den som ikke lenger gjeldende.'}
            {ventendeStatus === 'arkivert' && ' Dette markerer den som avsluttet/historisk.'}
          </Paragraph>
        </Dialog.Block>
        <Dialog.Block style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
          <Button data-size="sm" variant="secondary" onClick={() => setVentendeStatus(null)}>Avbryt</Button>
          <Button data-size="sm" data-color="danger" onClick={bekreft}>Bekreft</Button>
        </Dialog.Block>
      </Dialog>
    </>
  );
}
