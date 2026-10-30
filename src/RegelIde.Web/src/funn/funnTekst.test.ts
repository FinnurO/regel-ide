import { describe, expect, it } from 'vitest';
import { delITekstblokker, lovdataLenke } from './funnTekst';

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
