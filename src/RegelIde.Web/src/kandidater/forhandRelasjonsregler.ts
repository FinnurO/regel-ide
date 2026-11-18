import type { ForhandRelasjonsforslag, RelasjonNode } from '../api/types';

/**
 * [Ny, issue #469, Johanns klarsignal 2026-11-17] Forhåndsklassifiseringens relasjoner som strukturkanter, én knapp per relasjon (docs/09 §54,
 * docs/33 §5.16.3). Rene funksjoner med vitest; visningen står i `ForhandRelasjoner.tsx`.
 *
 * <p>Knappen skriver aldri direkte. Første klikk viser hva som skrives (fra, til, kategori, type, hjemmel, kunnskapsstatus og status «forslag»),
 * og først «Bekreft» skriver. Verdiene kommer fra API-et, som regner dem ut av fila og basen; ingenting her kan endres.</p>
 */

/** Hva raden har: en knapp som skriver, en knapp som er av (med grunnen), eller ingen knapp. */
export type Knappetilstand = 'skriv' | 'av' | 'ingen';

export function knappetilstand(f: Pick<ForhandRelasjonsforslag, 'status'>): Knappetilstand {
  switch (f.status) {
    case 'kan_skrives': return 'skriv';
    case 'opprett_navnet_forst':
    case 'opprett_motparten_forst':
    case 'mangler_forutsetning': return 'av';
    default: return 'ingen';
  }
}

/** Teksten ved en knapp som er av. «Opprett navnet først» er låst tekst (#469). */
export function avTekst(f: Pick<ForhandRelasjonsforslag, 'status' | 'grunn' | 'fra' | 'til'>): string {
  if (f.status === 'opprett_navnet_forst') return 'Opprett navnet først.';
  if (f.status === 'opprett_motparten_forst') {
    const mot = [f.fra, f.til].find((n) => n && !n.erNavnet);
    return `Opprett «${mot?.navn ?? 'motparten'}» først.`;
  }
  return storForbokstav(f.grunn ?? 'En forutsetning mangler.');
}

/** Merket for en kant som finnes eller er avvist. Fargene er låst per betydning (docs/09 §35): ventende `info`, godkjent `success`, avvist `danger`. */
export function kantmerke(f: Pick<ForhandRelasjonsforslag, 'status' | 'kantStatus'>): { tekst: string; farge: 'info' | 'success' | 'danger' } | null {
  if (f.status === 'avvist') return { tekst: 'Avvist', farge: 'danger' };
  if (f.status !== 'finnes') return null;
  switch (f.kantStatus) {
    case 'godkjent': return { tekst: 'Finnes, godkjent', farge: 'success' };
    case 'faglig_vurdert': return { tekst: 'Finnes, faglig vurdert', farge: 'info' };
    default: return { tekst: 'Finnes som forslag', farge: 'info' };
  }
}

/** Tallene i overskriften: hvor mange som kan skrives nå, hvor mange som venter på at en node opprettes, og hvor mange som ikke kan skrives. */
export function telling(relasjoner: Pick<ForhandRelasjonsforslag, 'status'>[]): { skriv: number; av: number; finnes: number; ingen: number } {
  const t = { skriv: 0, av: 0, finnes: 0, ingen: 0 };
  for (const r of relasjoner) {
    if (r.status === 'finnes' || r.status === 'avvist') t.finnes++;
    else t[knappetilstand(r)]++;
  }
  return t;
}

export function tellingstekst(relasjoner: Pick<ForhandRelasjonsforslag, 'status'>[]): string {
  const t = telling(relasjoner);
  const deler = [
    t.skriv > 0 ? `${t.skriv} kan skrives` : null,
    t.av > 0 ? `${t.av} venter på at en node opprettes` : null,
    t.finnes > 0 ? `${t.finnes} finnes eller er avvist` : null,
    t.ingen > 0 ? `${t.ingen} kan ikke skrives` : null,
  ].filter((x): x is string => x !== null);
  return deler.join(', ');
}

/** Lenken til noden: aktøren eller begrepet. Null når den ikke finnes ennå. */
export function nodelenke(n: Pick<RelasjonNode, 'id' | 'type'> | null): string | null {
  if (!n?.id) return null;
  return n.type === 'virksomhet' ? `/virksomheter/${n.id}` : `/begreper/${n.id}`;
}

/**
 * [Ny, #469, Johanns avgjørelse 2026-11-17] Hvor hjemmelen kommer fra: relasjonens egen (juristrunden fant en annen bestemmelse enn navnets), eller
 * navnets, bekreftet for relasjonen. Navnets hjemmel arves aldri uten juristrundens bekreftelse; uten hjemmel per relasjon er det ingen knapp.
 */
export function hjemmelkilde(kilde: 'navnets' | 'egen' | null): string {
  return kilde === 'egen'
    ? 'Relasjonens egen hjemmel, satt i juristrunden (aktorko-forhand/relasjonshjemler).'
    : 'Navnets hjemmel, bekreftet for relasjonen i juristrunden (aktorko-forhand/relasjonshjemler).';
}

/** Statusen kanten skrives med, i klart språk. Alltid forslag (#469). */
export const SKRIVES_SOM_TEKST = 'Forslag (manuelt foreslått), ikke godkjent';

function storForbokstav(s: string): string {
  const t = s.charAt(0).toUpperCase() + s.slice(1);
  return t.endsWith('.') ? t : `${t}.`;
}

export { storForbokstav };
