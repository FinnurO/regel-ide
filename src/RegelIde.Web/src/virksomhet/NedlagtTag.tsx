import { Tag } from '@digdir/designsystemet-react';

/**
 * [Ny, issue #395, Johanns avgjørelse M5] Merket for en aktør med status `nedlagt` (et nedlagt departement): finnes ikke lenger, er
 * identifisert av vedtakene som opprettet og nedla den, og har ingen registerkobling. `warning`, samme rolle som en utgått navneform
 * (docs/09 §15): den skal aldri kunne leses som en aktør som finnes i dag. Datoen er dagen etter siste gyldighetsdag (nedleggelsen).
 * Vises ikke for andre statuser.
 */
export function NedlagtTag({ livsstatus, gyldigFra, gyldigTil }: { livsstatus?: string | null; gyldigFra?: string | null; gyldigTil?: string | null }) {
  if (livsstatus !== 'nedlagt') return null;
  const periode = `${gyldigFra ?? 'ukjent start'}–${gyldigTil ?? 'ukjent slutt'}`;
  return (
    <Tag data-size="sm" data-color="warning" title={`Nedlagt. Fantes ${periode} etter vedtakene. Ingen kobling til Enhetsregisteret.`}>
      Nedlagt{gyldigTil ? ` (til ${gyldigTil})` : ''}
    </Tag>
  );
}
