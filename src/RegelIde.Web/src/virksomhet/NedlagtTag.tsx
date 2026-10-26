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

/**
 * [Ny, Johanns avgjørelse S3 på #397, docs/09 §45] «Hovedetterfølger ukjent» for et nedlagt departement uten en etterfølger som tok over
 * hovedansvaret (bare delvise eller ingen). Et synlig hull, `warning`; ingen etterfølger gjettes. Fylles av en avledet etterfølger, en som
 * overtok alle (de resterende) oppgavene, eller en faglig vurdert/godkjent etterfølger registrert manuelt.
 */
export function HovedetterfolgerUkjentTag({ ukjent }: { ukjent?: boolean | null }) {
  if (!ukjent) return null;
  return (
    <Tag data-size="sm" data-color="warning"
      title="Vedtakene sier ikke hvem som overtok hovedansvaret (bare delvise etterfølgere, eller ingen). Hullet kan fylles med en faglig vurdert etterfølger.">
      Hovedetterfølger ukjent
    </Tag>
  );
}

/** [Ny, issue #397, docs/09 §45] En duplikat som er erstattet av en annen aktør (reversibelt; ingenting er slettet). `neutral`. */
export function ErstattetTag({ erstattetAvId }: { erstattetAvId?: string | null }) {
  if (!erstattetAvId) return null;
  return (
    <Tag data-size="sm" data-color="neutral" title="Duplikat: aktøren er erstattet av en annen. Ingenting er slettet, og det kan angres.">
      Erstattet (duplikat)
    </Tag>
  );
}
