/**
 * Tester for paragrafetiketten. Feilen som motiverte den (2026-09-09): en hjemmel som peker på
 * konkurranseloven § 36 SJETTE LEDD ble vist som «§ 6», fordi leddnodens eget `nummer` er
 * leddnummeret. En etikett som viser feil paragraf er verre enn en rå eId — den ser riktig ut.
 */
import { describe, expect, it } from 'vitest';
import type { RettskildeNodeDto } from '../api/types';
import { paragrafEtikett } from './paragrafEtikett';

const BASE = 'https://lovdata.no/eli/lov/2004/03/05/12/nor';

/** Bare feltene funksjonen leser — resten av DTO-en er irrelevant her. */
function node(p: Partial<RettskildeNodeDto>): RettskildeNodeDto {
  return {
    id: p.id!,
    eid: p.eid!,
    nodeType: p.nodeType!,
    nummer: p.nummer ?? null,
    overskrift: p.overskrift ?? null,
    parentNodeId: p.parentNodeId ?? null,
  } as RettskildeNodeDto;
}

const paragraf36 = node({
  id: 'p36', eid: `${BASE}/§36`, nodeType: 'paragraf', nummer: '§ 36',
  overskrift: 'Konkurranseklagenemndas organisasjon, avgjørelser og vedtak',
});
const ledd6 = node({ id: 'l6', eid: `${BASE}/§36/ledd-6`, nodeType: 'ledd', nummer: '6', parentNodeId: 'p36' });
const punkt2 = node({ id: 'pt2', eid: `${BASE}/§36/ledd-6/punkt-2`, nodeType: 'punkt', nummer: '2', parentNodeId: 'l6' });
const noder = [paragraf36, ledd6, punkt2];

describe('paragrafEtikett', () => {
  it('klatrer opp til paragrafen for et ledd, ikke leddets eget nummer', () => {
    expect(paragrafEtikett(noder, `${BASE}/§36/ledd-6`)?.tekst).toBe('§ 36 sjette ledd');
  });

  it('tar med paragrafens overskrift separat', () => {
    expect(paragrafEtikett(noder, `${BASE}/§36/ledd-6`)?.overskrift)
      .toBe('Konkurranseklagenemndas organisasjon, avgjørelser og vedtak');
  });

  it('klatrer to nivåer for et punkt', () => {
    expect(paragrafEtikett(noder, `${BASE}/§36/ledd-6/punkt-2`)?.tekst).toBe('§ 36 punkt 2');
  });

  it('viser paragrafen alene når eId-en peker på paragrafnoden selv', () => {
    expect(paragrafEtikett(noder, `${BASE}/§36`)?.tekst).toBe('§ 36');
  });

  it('legger på § når importen ikke har gjort det', () => {
    const uten = [node({ id: 'p7', eid: `${BASE}/§7a`, nodeType: 'paragraf', nummer: '7a' })];
    expect(paragrafEtikett(uten, `${BASE}/§7a`)?.tekst).toBe('§ 7a');
  });

  it('skriver «ledd 11» i stedet for et ordenstall over tiende', () => {
    const l11 = node({ id: 'l11', eid: `${BASE}/§36/ledd-11`, nodeType: 'ledd', nummer: '11', parentNodeId: 'p36' });
    expect(paragrafEtikett([paragraf36, l11], `${BASE}/§36/ledd-11`)?.tekst).toBe('§ 36 ledd 11');
  });

  it('gir undefined når nodene ikke er hentet — ingen gjettet etikett', () => {
    expect(paragrafEtikett(undefined, `${BASE}/§36/ledd-6`)).toBeUndefined();
  });

  it('gir undefined for en ukjent eId', () => {
    expect(paragrafEtikett(noder, `${BASE}/§99/ledd-1`)).toBeUndefined();
  });

  it('gir undefined når det ikke finnes en paragrafnode over noden', () => {
    const løs = [node({ id: 'l1', eid: `${BASE}/§1/ledd-1`, nodeType: 'ledd', nummer: '1' })];
    expect(paragrafEtikett(løs, `${BASE}/§1/ledd-1`)).toBeUndefined();
  });

  it('stopper på en sirkulær forelderkjede i stedet for å henge', () => {
    const a = node({ id: 'a', eid: `${BASE}/§2/ledd-1`, nodeType: 'ledd', nummer: '1', parentNodeId: 'b' });
    const b = node({ id: 'b', eid: `${BASE}/§2/ledd-2`, nodeType: 'ledd', nummer: '2', parentNodeId: 'a' });
    expect(paragrafEtikett([a, b], `${BASE}/§2/ledd-1`)).toBeUndefined();
  });
});
