/**
 * Tester for referanse-prefill-logikken (issue #143). Fixturene under er hentet fra Johanns
 * 03.09.2026-rapport i saken: importen av opplaringslova.modelleksport.json droppet alle 4
 * regelverksreferanser på «Godkjenning av privat skole etter opplæringslova» stille, fordi
 * `rettskildeId`/`eid` aldri ble forsøkt fylt. Den ekte rettskilden (id, tittel, eli) og den ekte
 * `henvisning`-URL-en (`https://lovdata.no/lov/2023-06-09-30/§22-1`) i testene under er kopiert fra
 * den saken, ikke konstruert.
 */
import { describe, expect, it } from 'vitest';
import type { RettskildeNodeDto, RettskildeSammendrag } from '../api/types';
import { finnEntydigRettskilde, finnParagrafNode, trekkUtParagrafnummer } from './konverterModelleksport';

function rettskilde(p: Partial<RettskildeSammendrag>): RettskildeSammendrag {
  return {
    id: p.id ?? 'r1',
    virksomhetId: p.virksomhetId ?? null,
    eli: p.eli ?? null,
    tittel: p.tittel ?? '',
    kortnavn: p.kortnavn ?? null,
    kildetype: p.kildetype ?? 'Lov',
    ansvarligDepartement: p.ansvarligDepartement ?? null,
    erIrrelevant: p.erIrrelevant ?? false,
    irrelevantKommentar: p.irrelevantKommentar ?? null,
    ikrafttredelseRaa: p.ikrafttredelseRaa ?? null,
  };
}

function node(p: Partial<RettskildeNodeDto>): RettskildeNodeDto {
  return {
    id: p.id!,
    eid: p.eid!,
    parentNodeId: p.parentNodeId ?? null,
    nodeType: p.nodeType ?? 'paragraf',
    nummer: p.nummer ?? null,
    overskrift: p.overskrift ?? null,
    tekst: p.tekst ?? null,
    opphevet: p.opphevet ?? false,
    opphevetDato: p.opphevetDato ?? null,
    versjon: p.versjon ?? 1,
    handbokMetadata: p.handbokMetadata ?? null,
  };
}

const OPPLARINGSLOVA = rettskilde({
  id: '0332d4be-0f0f-4a33-b26c-4bef0dcad5e0',
  tittel: 'Lov om grunnskoleopplæringa og den vidaregåande opplæringa (opplæringslova)',
  kortnavn: 'Opplæringslova',
  eli: 'https://lovdata.no/eli/lov/2023/06/09/30/nor',
});

describe('finnEntydigRettskilde', () => {
  it('forhåndsvelger ved nøyaktig ett substrengtreff mot tittel (opplæringslova-eksempelet fra #143)', () => {
    const treff = finnEntydigRettskilde('Opplæringslova – oppll', [
      OPPLARINGSLOVA,
      rettskilde({ id: 'annen', tittel: 'Lov om barnehager (barnehageloven)' }),
    ]);
    expect(treff?.id).toBe(OPPLARINGSLOVA.id);
  });

  it('gjetter IKKE ved null treff', () => {
    const treff = finnEntydigRettskilde('Ekteskapsloven – ekteskl', [OPPLARINGSLOVA]);
    expect(treff).toBeNull();
  });

  it('gjetter IKKE ved flere treff', () => {
    const dublett = rettskilde({ id: 'dublett', tittel: 'Forskrift om opplæringslova sitt verkeområde' });
    const treff = finnEntydigRettskilde('Opplæringslova', [OPPLARINGSLOVA, dublett]);
    expect(treff).toBeNull();
  });

  it('returnerer null for tom/manglende lov-tekst', () => {
    expect(finnEntydigRettskilde(null, [OPPLARINGSLOVA])).toBeNull();
    expect(finnEntydigRettskilde('', [OPPLARINGSLOVA])).toBeNull();
  });
});

describe('trekkUtParagrafnummer', () => {
  it('trekker ut paragrafnummeret fra kildens henvisning-URL (ULIKT appens eId-format, se #143)', () => {
    expect(trekkUtParagrafnummer('https://lovdata.no/lov/2023-06-09-30/§22-1')).toBe('22-1');
  });

  it('takler et paragrafnummer med bokstavsuffiks', () => {
    expect(trekkUtParagrafnummer('https://lovdata.no/lov/2005-06-17-64/§8a')).toBe('8a');
  });

  it('returnerer null når henvisningen ikke inneholder en §-del', () => {
    expect(trekkUtParagrafnummer('https://lovdata.no/lov/2023-06-09-30')).toBeNull();
    expect(trekkUtParagrafnummer(null)).toBeNull();
  });
});

describe('finnParagrafNode', () => {
  const paragraf221 = node({
    id: 'p22-1',
    eid: 'https://lovdata.no/eli/lov/2023/06/09/30/nor/§22-1',
    nodeType: 'paragraf',
    nummer: '§ 22-1',
    overskrift: 'Godkjenning',
  });
  const kapittel = node({ id: 'k22', eid: 'https://lovdata.no/eli/lov/2023/06/09/30/nor/kap22', nodeType: 'kapittel', nummer: '22' });
  const noder = [kapittel, paragraf221];

  it('finner den ekte paragraf-noden når nummeret trukket ut av henvisning matcher (kildens §22-1-eksempel)', () => {
    const funnet = finnParagrafNode('https://lovdata.no/lov/2023-06-09-30/§22-1', noder);
    expect(funnet?.eid).toBe(paragraf221.eid);
  });

  it('lar paragraf-valget stå tomt (null) når ingen node matcher — IKKE et gjettet nærmeste treff', () => {
    expect(finnParagrafNode('https://lovdata.no/lov/2023-06-09-30/§99-9', noder)).toBeNull();
  });

  it('lar paragraf-valget stå tomt når nodene ikke er hentet ennå', () => {
    expect(finnParagrafNode('https://lovdata.no/lov/2023-06-09-30/§22-1', undefined)).toBeNull();
  });

  it('matcher aldri en kapittel-node selv om nummeret er likt', () => {
    expect(finnParagrafNode('https://lovdata.no/lov/2023-06-09-30/§22', noder)).toBeNull();
  });
});

/**
 * [Ny, #143, 2026-09-10] Regresjonstest mot issue #143s DOKUMENTERTE eksempel: rettigheten
 * «Godkjenning av privat skole etter opplæringslova» hadde 4 regelverksreferanser i kildefilen — alle
 * mot Opplæringslova § 22-1, med ulik `felt`-kobling (null/formal/kompetentMyndighet/
 * innhold.hvaRettighetenInnebarer.innledning) — og landet FØR denne saken som 0 av 4 importert. Målt
 * her (§16): begge nivåene av prefillen kjørt på nøyaktig de 4 kildereferansene fra rapporten.
 */
describe('issue #143 — dokumentert eksempel (opplæringslova § 22-1 × 4 felt)', () => {
  const paragraf221 = node({
    id: 'p22-1',
    eid: 'https://lovdata.no/eli/lov/2023/06/09/30/nor/§22-1',
    nodeType: 'paragraf',
    nummer: '§ 22-1',
  });
  const felter = [null, 'formal', 'kompetentMyndighet', 'innhold.hvaRettighetenInnebarer.innledning'];
  const kildeReferanser = felter.map((felt) => ({
    lov: 'Opplæringslova – oppll', henvisning: 'https://lovdata.no/lov/2023-06-09-30/§22-1', felt,
  }));

  it('forhåndsvelger rettskilde OG paragraf for alle 4 av 4 referansene', () => {
    let antallRettskildeFunnet = 0;
    let antallParagrafFunnet = 0;
    for (const r of kildeReferanser) {
      const rettskilde = finnEntydigRettskilde(r.lov, [OPPLARINGSLOVA]);
      if (rettskilde) antallRettskildeFunnet += 1;
      const node = rettskilde ? finnParagrafNode(r.henvisning, [paragraf221]) : null;
      if (node) antallParagrafFunnet += 1;
    }
    expect(antallRettskildeFunnet).toBe(4);
    expect(antallParagrafFunnet).toBe(4);
  });
});
