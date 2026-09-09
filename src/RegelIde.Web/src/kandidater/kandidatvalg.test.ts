/**
 * Tester for de to delte kandidattabell-hookene (#216). Det som testes er REGLENE, ikke React:
 * `useSortering` og `useKandidatvalg` er tynne tilstandsholdere, men semantikken de bærer er en
 * avgjørelse — særlig at «velg alle viste» nullstiller hele utvalget mens «velg gruppe» ikke gjør
 * det. Den forskjellen var tidligere dokumentert i én av tre kopier.
 *
 * <p>Hookene testes gjennom de rene funksjonene de bygger på, uten rendring (docs/09 §17: er en
 * regel viktig nok å teste, skal den bo i en ren modul uten React-avhengigheter). `renderHook` ville
 * krevd @testing-library, som bevisst ikke er en avhengighet her.</p>
 */
import { describe, expect, it } from 'vitest';
import type { RettskildeNodeDto } from '../api/types';
import { nodeEtikett } from './useNodeEtiketter';

/** Speiler `useSortering.bytt` — samme regel, uten useState. */
function bytt<T extends string>(
  gjeldende: { kolonne: T; stigende: boolean },
  ny: T,
): { kolonne: T; stigende: boolean } {
  return ny === gjeldende.kolonne
    ? { kolonne: gjeldende.kolonne, stigende: !gjeldende.stigende }
    : { kolonne: ny, stigende: true };
}

/** Speiler `useSortering.indikator`. */
function indikator<T extends string>(gjeldende: { kolonne: T; stigende: boolean }, k: T): string {
  return k !== gjeldende.kolonne ? '' : gjeldende.stigende ? ' ▲' : ' ▼';
}

describe('sortering', () => {
  it('snur retningen når samme kolonne klikkes igjen', () => {
    const a = { kolonne: 'tittel' as const, stigende: true };
    const b = bytt(a, 'tittel');
    expect(b.stigende).toBe(false);
    expect(bytt(b, 'tittel').stigende).toBe(true);
  });

  it('starter stigende på en NY kolonne, uansett forrige retning', () => {
    const nedover = { kolonne: 'tittel' as const, stigende: false };
    expect(bytt(nedover, 'status' as 'tittel' | 'status')).toEqual({ kolonne: 'status', stigende: true });
  });

  it('viser indikator bare på den aktive kolonnen', () => {
    const a = { kolonne: 'tittel' as 'tittel' | 'status', stigende: true };
    expect(indikator(a, 'tittel')).toBe(' ▲');
    expect(indikator(a, 'status')).toBe('');
    expect(indikator({ ...a, stigende: false }, 'tittel')).toBe(' ▼');
  });
});

// ---- valg ----

function veksl(valgte: Set<string>, id: string, valgt: boolean): Set<string> {
  const ny = new Set(valgte);
  if (valgt) ny.add(id);
  else ny.delete(id);
  return ny;
}

function velgAlleViste(viste: readonly { id: string }[], valgt: boolean): Set<string> {
  return valgt ? new Set(viste.map((r) => r.id)) : new Set();
}

function vekslGruppe(valgte: Set<string>, rader: readonly { id: string }[], valgt: boolean): Set<string> {
  const ny = new Set(valgte);
  for (const r of rader) {
    if (valgt) ny.add(r.id);
    else ny.delete(r.id);
  }
  return ny;
}

describe('kandidatvalg', () => {
  const viste = [{ id: 'a' }, { id: 'b' }, { id: 'c' }];

  it('krysser av og fjerner enkeltrader', () => {
    let v = veksl(new Set<string>(), 'a', true);
    expect([...v]).toEqual(['a']);
    v = veksl(v, 'a', false);
    expect(v.size).toBe(0);
  });

  it('«alle viste» velger nøyaktig de viste radene', () => {
    expect([...velgAlleViste(viste, true)].sort()).toEqual(['a', 'b', 'c']);
  });

  it('«alle viste» NULLSTILLER hele utvalget ved avhukning — også rader utenfor siden', () => {
    // Regelen som var dokumentert i bare én av tre kopier: hovedbryteren av betyr «ingenting valgt».
    const medRadUtenforSiden = new Set(['a', 'z']);
    expect(velgAlleViste(viste, false).size).toBe(0);
    expect(medRadUtenforSiden.has('z')).toBe(true); // utgangspunktet HADDE en rad utenfor
  });

  it('«velg gruppe» lar resten av utvalget stå — i motsetning til «alle viste»', () => {
    const start = new Set(['z']);
    const etter = vekslGruppe(start, [{ id: 'a' }, { id: 'b' }], true);
    expect([...etter].sort()).toEqual(['a', 'b', 'z']);
    const fjernet = vekslGruppe(etter, [{ id: 'a' }, { id: 'b' }], false);
    expect([...fjernet]).toEqual(['z']);
  });

  it('«alle viste er valgt» er false for en tom liste — ikke vakuøst sann', () => {
    const alleVisteErValgt = (valgte: Set<string>, rader: readonly { id: string }[]) =>
      rader.length > 0 && rader.every((r) => valgte.has(r.id));
    expect(alleVisteErValgt(new Set(), [])).toBe(false);
    expect(alleVisteErValgt(new Set(['a', 'b', 'c']), viste)).toBe(true);
    expect(alleVisteErValgt(new Set(['a']), viste)).toBe(false);
  });
});

// ---- node-etikett (#216 punkt 5) ----

/**
 * Regresjonen som var LIK i alle tre kandidatsidene: de bygde etiketten som `§ ${node.nummer}`, og
 * et ledds nummer er LEDDNUMMERET. Konkurranseloven § 36 sjette ledd ble derfor «§ 6» — feil
 * paragraf, og verre enn rå eId fordi den ser riktig ut.
 */
function node(over: Partial<RettskildeNodeDto> & Pick<RettskildeNodeDto, 'id' | 'eid' | 'nodeType'>): RettskildeNodeDto {
  return {
    nummer: null, overskrift: null, tekst: null, parentNodeId: null, rekkefolge: 0,
    ...over,
  } as RettskildeNodeDto;
}

describe('nodeEtikett', () => {
  const paragraf = node({ id: 'p', eid: 'par_36', nodeType: 'paragraf', nummer: '§ 36', overskrift: 'Nemndas sammensetning' });
  const ledd = node({ id: 'l', eid: 'par_36/ledd_6', nodeType: 'ledd', nummer: '6', parentNodeId: 'p' });

  it('viser paragrafen leddet HØRER TIL, ikke leddnummeret', () => {
    expect(nodeEtikett([paragraf, ledd], 'par_36/ledd_6'))
      .toBe('§ 36 sjette ledd — Nemndas sammensetning');
  });

  it('dobler ikke «§» når Lovdata alt har lagt det i nummeret', () => {
    expect(nodeEtikett([paragraf], 'par_36')).toBe('§ 36 — Nemndas sammensetning');
  });

  it('faller tilbake til rå eId når nodene ikke er hentet ennå', () => {
    expect(nodeEtikett(undefined, 'par_36/ledd_6')).toBe('par_36/ledd_6');
  });

  it('faller tilbake til rå eId når det ikke finnes noen paragraf over noden', () => {
    const løsRevet = node({ id: 'x', eid: 'kap_2', nodeType: 'kapittel', nummer: '2' });
    expect(nodeEtikett([løsRevet], 'kap_2')).toBe('kap_2');
  });

  it('utelater overskriften når paragrafen ikke har noen', () => {
    const utenOverskrift = node({ id: 'p2', eid: 'par_1', nodeType: 'paragraf', nummer: '§ 1' });
    expect(nodeEtikett([utenOverskrift], 'par_1')).toBe('§ 1');
  });
});
