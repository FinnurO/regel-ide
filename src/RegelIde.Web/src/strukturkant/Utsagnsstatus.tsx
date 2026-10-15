import { Tag } from '@digdir/designsystemet-react';
import type { Delegeringsadgang, DelegeringSlutning, Godkjenning, Kunnskapsstatus } from '../api/types';

/**
 * [Ny, issue #367 AC7, docs/34 §3, docs/profil/03 §5] Utsagnsstatus som EGNE merker (docs/09 §35):
 * <ul>
 *   <li><b>Kunnskapsstatus</b> — HVORDAN utsagnet er kommet fram. En klassifisering av metoden → `neutral`; uavklart og omstridt er
 *       `warning` (ikke avgjort). Null vises som «Ikke vurdert» (docs/34 §3: eksisterende rader har ingen gjettet status).</li>
 *   <li><b>Godkjenning</b> — HVEM som har godkjent det. Forslag er `info` (ubekreftet, som før #367), avvist er `danger`; godkjent
 *       vises ikke (det er normaltilstanden, som «validert» før).</li>
 *   <li><b>Delegering</b> (#335) — tekstfunn og slutning ser aldri like ut: «Kan ikke delegeres (teksten)» mot «Kan trolig delegeres
 *       (slutning)».</li>
 * </ul>
 */
export const KUNNSKAPSSTATUS_VISNING: Record<Kunnskapsstatus, { tekst: string; forklaring: string }> = {
  tekstfunn: { tekst: 'Tekstfunn', forklaring: 'Står uttrykkelig i det angitte tekstfragmentet.' },
  normalisering: { tekst: 'Normalisering', forklaring: 'Teksten er knyttet til et kontrollert begrep etter en dokumentert regel, uten ny materiell påstand.' },
  fortolkning: { tekst: 'Fortolkning', forklaring: 'Følger av en begrunnet tolkning (tolkningsargumentene står på kanten).' },
  avledet: { tekst: 'Avledet', forklaring: 'Slutning fra andre modellutsagn (premissene) etter en angitt regel.' },
  eksternt_dokumentert: { tekst: 'Eksternt dokumentert', forklaring: 'Følger av et dokument som ikke er lovtekst.' },
  uavklart: { tekst: 'Uavklart', forklaring: 'Kan være relevant, men er ikke avgjort.' },
  omstridt: { tekst: 'Omstridt', forklaring: 'Det finnes flere dokumenterte, konkurrerende fortolkninger.' },
  foreldet: { tekst: 'Foreldet', forklaring: 'Tidligere klassifisering, erstattet av en revisjon.' },
};

export function KunnskapsstatusTag({ status, premisser }: { status: Kunnskapsstatus | null; premisser?: string[] | null }) {
  if (status === null) {
    return (
      <Tag data-size="sm" data-color="neutral" title="Kunnskapsstatus er ikke vurdert ennå (issue #367: ingen gjettet verdi).">
        Ikke vurdert
      </Tag>
    );
  }
  const v = KUNNSKAPSSTATUS_VISNING[status];
  const tillegg = status === 'avledet' && premisser?.length ? ` Bygger på ${premisser.length} premiss${premisser.length === 1 ? '' : 'er'}.` : '';
  return (
    <Tag data-size="sm" data-color={status === 'uavklart' || status === 'omstridt' ? 'warning' : 'neutral'} title={`Kunnskapsstatus: ${v.forklaring}${tillegg}`}>
      {v.tekst}
    </Tag>
  );
}

export function GodkjenningTag({ status, oppdagelsesKilde }: { status: Godkjenning; oppdagelsesKilde: string }) {
  switch (status) {
    case 'godkjent':
      return null;
    case 'avvist':
      return <Tag data-size="sm" data-color="danger" title="Avvist av et menneske.">Avvist</Tag>;
    case 'maskinelt_foreslatt':
      return (
        <Tag data-size="sm" data-color="info" title="Maskinelt forslag, ikke bekreftet av et menneske — regnes ikke som gjeldende.">
          Forslag ({oppdagelsesKilde})
        </Tag>
      );
    case 'manuelt_foreslatt':
      return <Tag data-size="sm" data-color="info" title="Foreslått av et menneske, ikke godkjent ennå.">Manuelt forslag</Tag>;
    case 'faglig_vurdert':
      return <Tag data-size="sm" data-color="info" title="Faglig vurdert, ikke godkjent ennå.">Faglig vurdert</Tag>;
  }
}

/** [Ny, issue #367 AC5 / #335] Delegeringsadgang etter teksten, og slutningen etter alminnelige regler — aldri forvekslbare. */
export function DelegeringTag({ adgang, slutning }: { adgang: Delegeringsadgang | null; slutning: DelegeringSlutning | null }) {
  if (slutning) {
    return (
      <Tag data-size="sm" data-color="neutral"
        title={`Slutning etter alminnelige regler, ikke tekstfunn: teksten regulerer ikke delegering, men ${slutning === 'trolig_delegerbar'
          ? '«Kongen» uten «i statsråd» kan normalt delegeres (#335).'
          : '«Kongen i statsråd» er en sterk lovteknisk konvensjon for at kompetansen ikke delegeres (#335).'}`}>
        {slutning === 'trolig_delegerbar' ? 'Kan trolig delegeres (slutning)' : 'Kan trolig ikke delegeres (slutning)'}
      </Tag>
    );
  }
  if (adgang === null) return null;
  const tekst = adgang === 'uttrykkelig_tillatt' ? 'Kan delegeres (teksten)'
    : adgang === 'uttrykkelig_forbudt' ? 'Kan ikke delegeres (teksten)' : 'Delegering ikke regulert';
  const forklaring = adgang === 'uttrykkelig_tillatt' ? 'Teksten sier uttrykkelig at kompetansen kan delegeres.'
    : adgang === 'uttrykkelig_forbudt' ? 'Teksten sier uttrykkelig at kompetansen ikke kan delegeres («kan ikke delegeres», «… selv»).'
    : 'Bestemmelsen sier ingenting om delegering.';
  return <Tag data-size="sm" data-color="neutral" title={forklaring}>{tekst}</Tag>;
}
