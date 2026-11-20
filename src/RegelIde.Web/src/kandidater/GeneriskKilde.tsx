import { Tag } from '@digdir/designsystemet-react';
import type { Forekomstforslag, Forekomstlesning, GeneriskTaggOppslag, Godkjenning, StandardregelSvar } from '../api/types';
import { GodkjenningTag, KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';

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
    if (regel.rute === 'lovdata_utgatt_navn') {
      return `standardregel: Lovdata, sekundær kilde, utgått navn${regel.valgtEtterDato ? ', valgt etter dato' : ''}`;
    }
    return 'standardregel';
  }
  return 'ikke avklart';
}

/** [Ny, issue #486] Lesningene i klartekst (samme ord som `Forekomstlesninger.Tekst` i Data). */
export const FOREKOMSTLESNING_TEKST: Record<Forekomstlesning, string> = {
  fremmed_stat: 'fremmed stat',
  generisk_konvensjonsstat: 'generisk konvensjonsstat',
  ikke_aktor: 'ikke aktør',
  rolle_statsrad: 'rollen statsråd',
  funksjon: 'funksjonen (styret av riket)',
  regjeringens_resolusjon: '«Regjeringens res.» (Kongen i statsråd)',
  slutning: 'slutning',
};

/**
 * [Ny, issue #486] Forslaget for én forekomst (tagg per forekomst), slik det står ved taggen før et menneske har vurdert det. Fargerollene følger
 * docs/09 §57: «Forekomst» og lesningen er `neutral` (en klassifisering); godkjenningen og kunnskapsstatusen bruker de delte merkene (§35:
 * forslag → `info`, uavklart → `warning`). En slutning sier «slutning» i klartekst, så den aldri ser ut som en regel (§35, delegering).
 */
export function ForekomstforslagTagger({ forslag }: { forslag: Forekomstforslag }) {
  const slutning = forslag.lesning === 'slutning';
  return (
    <>
      <Tag data-size="sm" data-color="neutral" title={forslag.kommentar ?? undefined}>
        {slutning ? 'Forekomst: slutning' : `Forekomst: ${forslag.lesningTekst}`}
      </Tag>
      <GodkjenningTag status={forslag.status} oppdagelsesKilde={forslag.oppdagelsesKilde} />
      <KunnskapsstatusTag status={forslag.kunnskapsstatus} />
    </>
  );
}

/** [Ny, issue #486] Teksten for forslaget i `title`-kjeden («forslag: rollen statsråd → statsrådet», «forslag: slutning, ikke avklart»). */
export function forekomstforslagTekst(forslag: Forekomstforslag): string {
  if (forslag.lesning === 'slutning') return 'forslag per forekomst: slutning, ikke avklart';
  return `forslag per forekomst: ${forslag.lesningTekst}${forslag.referent ? ` → ${forslag.referent.navn}` : ''}`;
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
        {/* [ENDRET, Johanns beslutning på PR #398] Et utgått navn i Lovdatas felt, ført til aktøren via navnehistorikken, sier det i merket. */}
        {regel.dokumentasjon === 'sekundaer' && (
          <Tag data-size="sm" data-color="warning" title={regel.rute === 'lovdata_utgatt_navn'
            ? (regel.valgtEtterDato
              ? 'Lovdatas felt bruker et navn flere departementer har hatt; svaret er det som hadde navnet sist (gjeldende versjon) eller på versjonens dato (Johanns avgjørelse på #397).'
              : 'Lovdatas felt «Ansvarlig departement» bruker et utgått navn; navnehistorikken fra organiseringsvedtakene fører det til dette departementet.')
            : 'Svaret bygger på Lovdatas felt «Ansvarlig departement», ikke på en vurdert ressortfordeling.'}>
            {regel.rute === 'lovdata_utgatt_navn'
              ? (regel.valgtEtterDato ? 'Sekundær kilde, utgått navn, valgt etter dato' : 'Sekundær kilde, utgått navn')
              : 'Sekundær kilde'}
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
