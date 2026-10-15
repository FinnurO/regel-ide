import { describe, expect, it } from 'vitest';
import type { StrukturkantDto } from '../api/types';
import { avvisningstekst, grupperPerHjemmel, tillitFor } from './forslagsko';

const kant = (id: string, hjemmel: string | null, tittel: string | null) =>
  ({ id, hjemmelRettskildeId: hjemmel, hjemmelRettskildeTittel: tittel }) as StrukturkantDto;

describe('forslagskøen for strukturkanter (#313)', () => {
  it('gir tillit etter docs/33 §5.5: mønster normal, KI lav unntatt forskrift og tilsyn', () => {
    expect(tillitFor({ oppdagelsesKilde: 'monster:forskrift-gi', kategori: 'K', typekode: 'vedtak', normform: null })).toBeNull();
    expect(tillitFor({ oppdagelsesKilde: 'ki:m', kategori: 'K', typekode: 'normgivning', normform: 'forskrift' })).toBeNull();
    expect(tillitFor({ oppdagelsesKilde: 'ki:m', kategori: 'K', typekode: 'normgivning', normform: 'instruks' })).toBe('lav');
    expect(tillitFor({ oppdagelsesKilde: 'ki:m', kategori: 'K', typekode: 'tilsyn', normform: null })).toBe('middels');
    expect(tillitFor({ oppdagelsesKilde: 'ki:m', kategori: 'R', typekode: 'rapporterer_til', normform: null })).toBe('lav');
  });

  it('grupperer per hjemmel, flest først, uten hjemmel sist', () => {
    const g = grupperPerHjemmel([kant('1', 'a', 'Lov A'), kant('2', null, null), kant('3', 'b', 'Lov B'), kant('4', 'b', 'Lov B')]);
    expect(g.map((x) => [x.tittel, x.kanter.length])).toEqual([['Lov B', 2], ['Lov A', 1], ['Uten hjemmel i korpus', 1]]);
  });

  it('sier hvem som avviste og hvorfor', () => {
    expect(avvisningstekst({ sistEndretAv: 'Kari', sistEndretTidspunkt: null, avvisningBegrunnelse: 'Feil retning.' }))
      .toBe('Avvist av Kari (menneske) ukjent tidspunkt: Feil retning.');
  });
});
