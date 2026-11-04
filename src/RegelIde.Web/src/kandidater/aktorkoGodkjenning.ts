import type { AktorkoDetalj, AktorkoGodkjenningRequest, Aktorkotype } from '../api/types';

/**
 * [Ny, issue #440 PR 2] Reglene bak godkjenningsskjemaet i aktørkøen, delt mellom skjermen og testene. Speiler valideringen i
 * `AktorkoSkrivTjeneste.GodkjennAsync`: ingenting er forhåndsvalgt, og knappen er av til alt er valgt.
 */

export const AKTORKOTYPE_TEKST: Record<Aktorkotype, { tekst: string; bestemt: string; beskrivelse: string }> = {
  aktor: { tekst: 'Aktør', bestemt: 'Aktøren', beskrivelse: 'Ett bestemt organ eller én virksomhet («Utdanningsdirektoratet»).' },
  klasse: { tekst: 'Klasse', bestemt: 'Klassen', beskrivelse: 'Mange av samme slag, hver for seg («barnehagen», «foreldrerådet» i hver barnehage).' },
  rolle: { tekst: 'Rolle', bestemt: 'Rollen', beskrivelse: 'En funksjon noen har («styrer», «dommer»).' },
  omrade: { tekst: 'Område', bestemt: 'Området', beskrivelse: 'Et geografisk eller administrativt område.' },
};

/** Utkastet i skjemaet. `null` = ikke valgt. Hjemmelen er eId-en til et forslag, eller «mangler». */
export interface GodkjenningUtkast {
  type: Aktorkotype | null;
  navn: string | null;
  hjemmel: string | null; // forslagets `${rettskildeId}|${eid}`, eller 'mangler'
  gjelder: string | null; // rettskildeId, eller 'fast'
  godkjenning: 'faglig_vurdert' | 'godkjent' | null;
  begrunnelse: string;
}

export const TOMT_UTKAST: GodkjenningUtkast = { type: null, navn: null, hjemmel: null, gjelder: null, godkjenning: null, begrunnelse: '' };

export const forslagNokkel = (f: { rettskildeId: string; eid: string }) => `${f.rettskildeId}|${f.eid}`;

/** Hva som mangler før godkjenningen kan sendes, eller null når alt er valgt. */
export function godkjenningMangler(u: GodkjenningUtkast): string | null {
  if (!u.type) return 'Velg hva navnet er.';
  if (!u.navn) return 'Velg skrivemåten som blir navnet.';
  if (!u.hjemmel) return 'Velg hjemmel: et forslag, eller «hjemmel mangler».';
  if (u.type !== 'aktor' && u.hjemmel === 'mangler' && !u.gjelder) return `Velg hvor ${AKTORKOTYPE_TEKST[u.type].bestemt.toLowerCase()} gjelder.`;
  if (!u.godkjenning) return 'Velg hvor sikkert det er.';
  return null;
}

/** Forespørselen til `POST /api/aktorko/{nøkkel}/godkjenn`, eller null når noe mangler. */
export function tilForesporsel(u: GodkjenningUtkast, detalj: AktorkoDetalj): AktorkoGodkjenningRequest | null {
  if (godkjenningMangler(u) !== null) return null;
  const forslag = u.hjemmel === 'mangler' ? null : detalj.hjemmelforslag.find((f) => forslagNokkel(f) === u.hjemmel) ?? null;
  if (u.hjemmel !== 'mangler' && !forslag) return null;
  const utenHjemmel = u.type !== 'aktor' && forslag === null;
  return {
    type: u.type!,
    navn: u.navn!,
    hjemmel: forslag ? 'forslag' : 'mangler',
    hjemmelRettskildeId: forslag?.rettskildeId ?? null,
    hjemmelEid: forslag?.eid ?? null,
    godkjenning: u.godkjenning!,
    begrunnelse: u.begrunnelse.trim() || null,
    gjelderILovkildeId: utenHjemmel && u.gjelder !== 'fast' ? u.gjelder : null,
    gjelderFast: utenHjemmel ? u.gjelder === 'fast' : null,
    forventetForekomster: detalj.forekomster,
  };
}

/** Varselet ved kobling, etter navneformgrunnen (speiler `AktorkoSkrivTjeneste.Grunnvarsel`). */
export function grunnvarsel(grunn: string | null, navn: string, aktor: string): string | null {
  if (grunn === 'utgatt') return `«${navn}» registreres som UTGÅTT navn på ${aktor}. Forekomstene peker på aktøren, men navnet er ikke aktørens navn i dag.`;
  if (grunn === 'kortform') return `«${navn}» registreres som KORTFORM for ${aktor}. Kortformen er ikke aktørens fulle navn.`;
  return null;
}

/** Alle skrivemåtene i navnet, vanligste først, uten dubletter. */
export function skrivemater(detalj: AktorkoDetalj): string[] {
  return [...new Set(detalj.varianter.flatMap((v) => v.skrivemater.map((s) => s.tekst)))];
}
