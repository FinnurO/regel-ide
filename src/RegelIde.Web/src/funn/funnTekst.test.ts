import { describe, expect, it } from 'vitest';
import {
  delITekstblokker, delPremissmangler, etterfolgerlinje, kategoriMerketekst, lovdataLenke, utenEtterfolgerpunkt,
} from './funnTekst';

describe('delITekstblokker (#429)', () => {
  it('gjør «- »-linjer til én liste etter innledningen', () => {
    const blokker = delITekstblokker('To forskrifter gir ulikt svar:\n- A sier X.\n- B sier Y.\nAvslutning.');
    expect(blokker).toEqual([
      { type: 'avsnitt', linjer: ['To forskrifter gir ulikt svar:'] },
      { type: 'liste', linjer: ['A sier X.', 'B sier Y.'] },
      { type: 'avsnitt', linjer: ['Avslutning.'] },
    ]);
  });
});

describe('lovdataLenke (#429)', () => {
  it('lager Lovdata-lenken til paragrafen for en eId på ledd', () => {
    expect(lovdataLenke('https://lovdata.no/eli/forskrift/2008/09/12/1071/nor/§11/ledd-3'))
      .toBe('https://lovdata.no/dokument/SF/forskrift/2008-09-12-1071/§11');
  });
  it('lar et ukjent format stå uendret og gir null for null', () => {
    expect(lovdataLenke('https://www.regjeringen.no/x')).toBe('https://www.regjeringen.no/x');
    expect(lovdataLenke(null)).toBeNull();
  });
});

describe('delPremissmangler (#429 PR 3)', () => {
  it('skiller R9 (hjemmelsloven) fra navneform og etterfølger', () => {
    const r9 = 'Hjemmelsloven kan være opphevet: rettskilden bygger på lov 24. juni 1988 nr. 64 (Lovdatas hjemmelsfelt), og …';
    const etterfolger = 'Etterfølger ikke vurdert: «X» (maskinelt foreslått).';
    expect(delPremissmangler([etterfolger, r9])).toEqual({ hjemmel: [r9], nyHjemmel: [], uttomt: [], andre: [etterfolger] });
    expect(delPremissmangler([])).toEqual({ hjemmel: [], nyHjemmel: [], uttomt: [], andre: [] });
  });
});

describe('etterfolgerlinje (#429 PR 2)', () => {
  const punkt = { overskrift: 'Etterfølger', tekst: 'Digitaliserings- og forvaltningsdepartementet (ekom).' };
  it('viser kantene når funnet peker på noen', () => {
    expect(etterfolgerlinje([{ etikett: 'NFD' }], [punkt])).toBe('NFD');
  });
  it('viser juristens punkt «Etterfølger» når det ikke finnes noen kant, og tar det ut av lista', () => {
    expect(etterfolgerlinje([], [punkt])).toBe(punkt.tekst);
    expect(utenEtterfolgerpunkt([punkt, { overskrift: null, tekst: 'x' }], [])).toEqual([{ overskrift: null, tekst: 'x' }]);
    expect(utenEtterfolgerpunkt([punkt], [{ etikett: 'NFD' }])).toEqual([punkt]);
  });
  it('sier «ukjent» uten kant og punkt, og «ikke vurdert» når premissporten stopper', () => {
    expect(etterfolgerlinje([], [])).toBe('ukjent');
    expect(etterfolgerlinje([], [punkt], true)).toBe('ikke vurdert');
  });
});

describe('delPremissmangler og kategoriMerketekst (#472 PR 2)', () => {
  it('skiller «Ny hjemmel» og #446 fra «Hjemmel ikke vurdert», som er en vanlig premiss', () => {
    const ny = 'Ny hjemmel ikke vurdert: vurderingen har ikke noe punkt «Ny hjemmel» …';
    const uttomt = 'Uttømt virkning ikke klassifisert (#446): det er ikke avgjort …';
    const hjemmel = 'Hjemmel ikke vurdert: vurderingen har ikke noe punkt «Hjemmel» …';
    expect(delPremissmangler([ny, hjemmel, uttomt])).toEqual({ hjemmel: [], nyHjemmel: [ny], uttomt: [uttomt], andre: [hjemmel] });
  });
  it('gir «Hjemmel opphevet: videreføring ikke funnet», men gjenstandsløs alene', () => {
    expect(kategoriMerketekst('Hjemmel opphevet', 'hjemmel_opphevet', 'Videreføring ikke funnet')).toBe('Hjemmel opphevet: videreføring ikke funnet');
    expect(kategoriMerketekst('Hjemmel opphevet', 'gjenstandslos', 'Gjenstandsløs')).toBe('Gjenstandsløs');
    expect(kategoriMerketekst('Mulig motstrid', null, null)).toBe('Mulig motstrid');
  });
});
