import { describe, expect, it } from 'vitest';
import type { ForhandRelasjonsforslag, RelasjonNode } from '../api/types';
import { avTekst, kantmerke, knappetilstand, nodelenke, telling, tellingstekst } from './forhandRelasjonsregler';

const node = (navn: string, erNavnet: boolean, id: string | null = null): RelasjonNode =>
  ({ id, type: id ? 'begrep' : null, navn, art: 'klasse', erNavnet, merknad: null });

function forslag(over: Partial<ForhandRelasjonsforslag>): ForhandRelasjonsforslag {
  return {
    nr: 24, indeks: 0, relasjon: 'rolle_innehas_av', relasjonstekst: 'innehas av', merknad: null, status: 'kan_skrives', grunn: null,
    kategori: 'I', kategoritekst: 'rolleinnehav', typekode: 'innehar', utsagn: 'kommune innehar rollen bostedskommune',
    fra: node('kommune', false, 'k'), til: node('bostedskommune', true, 'b'), hjemmel: null, kunnskapsstatus: 'tekstfunn',
    tolkningsargumenter: null, tolkningsbegrunnelse: null, skrivesSom: 'manuelt_foreslatt', kantId: null, kantStatus: null,
    kantSistEndretAv: null, kantAvvisningBegrunnelse: null, ...over,
  };
}

describe('knappen per relasjon (#469)', () => {
  it('skriver bare når API-et sier kan_skrives', () => {
    expect(knappetilstand(forslag({}))).toBe('skriv');
    expect(knappetilstand(forslag({ status: 'opprett_navnet_forst' }))).toBe('av');
    expect(knappetilstand(forslag({ status: 'mangler_forutsetning' }))).toBe('av');
    expect(knappetilstand(forslag({ status: 'finnes' }))).toBe('ingen');
    expect(knappetilstand(forslag({ status: 'avvist' }))).toBe('ingen');
    expect(knappetilstand(forslag({ status: 'kan_ikke_skrives' }))).toBe('ingen');
  });

  it('sier «Opprett navnet først» når navnet ikke er opprettet, og navnet på motparten ellers', () => {
    expect(avTekst(forslag({ status: 'opprett_navnet_forst' }))).toBe('Opprett navnet først.');
    expect(avTekst(forslag({ status: 'opprett_motparten_forst', fra: node('barnehage', false), til: node('styrer', true, 's') })))
      .toBe('Opprett «barnehage» først.');
    expect(avTekst(forslag({ status: 'mangler_forutsetning', grunn: 'sett aktørtypen «ordning» på «Folketrygden» først' })))
      .toBe('Sett aktørtypen «ordning» på «Folketrygden» først.');
  });

  it('viser en kant som finnes etter godkjenningen, og en avvist som avvist', () => {
    expect(kantmerke(forslag({ status: 'finnes', kantStatus: 'manuelt_foreslatt' }))).toEqual({ tekst: 'Finnes som forslag', farge: 'info' });
    expect(kantmerke(forslag({ status: 'finnes', kantStatus: 'godkjent' }))).toEqual({ tekst: 'Finnes, godkjent', farge: 'success' });
    expect(kantmerke(forslag({ status: 'avvist', kantStatus: 'avvist' }))).toEqual({ tekst: 'Avvist', farge: 'danger' });
    expect(kantmerke(forslag({}))).toBeNull();
  });

  it('teller per tilstand', () => {
    const r = [forslag({}), forslag({ status: 'opprett_navnet_forst' }), forslag({ status: 'finnes' }), forslag({ status: 'kan_ikke_skrives' }),
      forslag({ status: 'skrives_av_koblingen' })];
    expect(telling(r)).toEqual({ skriv: 1, av: 1, finnes: 1, ingen: 2 });
    expect(tellingstekst(r)).toBe('1 kan skrives, 1 venter på at en node opprettes, 1 finnes eller er avvist, 2 kan ikke skrives');
  });

  it('lenker til aktøren eller begrepet, og ikke til en node som ikke finnes', () => {
    expect(nodelenke({ id: 'a', type: 'virksomhet' })).toBe('/virksomheter/a');
    expect(nodelenke({ id: 'b', type: 'begrep' })).toBe('/begreper/b');
    expect(nodelenke({ id: null, type: null })).toBeNull();
  });
});
