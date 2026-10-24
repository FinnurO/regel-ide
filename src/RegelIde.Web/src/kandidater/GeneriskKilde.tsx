import { Tag } from '@digdir/designsystemet-react';
import type { GeneriskTaggOppslag, Godkjenning, StandardregelSvar } from '../api/types';
import { GodkjenningTag } from '../strukturkant/Utsagnsstatus';

/**
 * [Ny, issue #394 AK 2] Hvor svaret på en generisk omtale kommer fra, som tekst: «binding, faglig vurdert», «standardregel:
 * ressortfordelingen», «standardregel: Lovdata, sekundær kilde» eller «ikke avklart». Brukes i `title`-kjeden på markeringen i løpeteksten
 * (der en ReactNode ikke kan stå) og i forhåndsvisningen i veiviseren. Samme ord som profilsiden.
 */
export function generiskKildeTekst(kilde: 'binding' | 'regel' | null | undefined, bindingStatus?: Godkjenning | null,
  regel?: StandardregelSvar | null): string {
  if (kilde === 'binding') {
    return `binding, ${bindingStatus === 'godkjent' ? 'godkjent' : bindingStatus === 'faglig_vurdert' ? 'faglig vurdert' : (bindingStatus ?? 'ukjent status')}`;
  }
  if (kilde === 'regel' && regel) {
    if (regel.rute === 'ressortfordeling') return 'standardregel: ressortfordelingen';
    if (regel.rute === 'lovdata') return 'standardregel: Lovdata, sekundær kilde';
    return 'standardregel';
  }
  return 'ikke avklart';
}

/**
 * [Ny, issue #394 AK 2/3] Merkene for kilden til en generisk omtale, ved siden av «Generisk omtale» (`NavneformgrunnTag`). Fargerollene
 * (docs/09 §42): «Binding» og «Standardregel» er `neutral` (en klassifisering av hvor svaret kommer fra); godkjenningen følger §35
 * (faglig vurdert → `info`, godkjent → ingen merke); «Sekundær kilde» er `warning` — svaret er ikke sterkere enn Lovdatas felt; «Ikke
 * avklart» er `neutral` `outline`, som «Navneform mangler» (§40).
 */
export function GeneriskKildeTagger({ oppslag }: { oppslag: Pick<GeneriskTaggOppslag, 'kilde' | 'bindingStatus' | 'regel'> }) {
  const { kilde, bindingStatus, regel } = oppslag;
  if (kilde === 'binding') {
    return (
      <>
        <Tag data-size="sm" data-color="neutral" title="En manuell binding for denne rettskilden (eller paragrafen) går foran standardregelen.">Binding</Tag>
        {bindingStatus && <GodkjenningTag status={bindingStatus} oppdagelsesKilde="manuell" />}
      </>
    );
  }
  if (kilde === 'regel' && regel) {
    return (
      <>
        <Tag data-size="sm" data-color="neutral" title={`${regel.regelnavn}: ${regel.forklaring}${regel.merknader.length ? ` ${regel.merknader.join(' ')}` : ''}`}>
          Standardregel
        </Tag>
        {regel.dokumentasjon === 'sekundaer' && (
          <Tag data-size="sm" data-color="warning" title="Svaret bygger på Lovdatas felt «Ansvarlig departement», ikke på en vurdert ressortfordeling.">
            Sekundær kilde
          </Tag>
        )}
      </>
    );
  }
  return (
    <Tag data-size="sm" data-color="neutral" variant="outline"
      title={regel ? `Standardregelen ga ikke svar: ${regel.forklaring}` : 'Ingen vurdert binding dekker forekomsten, og ingen standardregel gjelder ordet.'}>
      Ikke avklart
    </Tag>
  );
}
