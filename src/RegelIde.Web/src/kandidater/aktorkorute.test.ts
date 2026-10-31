/**
 * [Ny, issue #440 PR 1] Setningen sveipene viser om treffene aktørkøens ruting sendte et annet sted. Ingen setning når ingenting ble sendt
 * et annet sted (ikke «0 treff …»), og rutene står i samme rekkefølge som reglene prøves.
 */
import { describe, expect, it } from 'vitest';
import { utenforAktorkoenTekst } from './aktorkorute';

describe('utenforAktorkoenTekst', () => {
  it('gir null når ingenting ble sendt et annet sted', () => {
    expect(utenforAktorkoenTekst(undefined)).toBeNull();
    expect(utenforAktorkoenTekst({})).toBeNull();
    expect(utenforAktorkoenTekst({ generisk: 0 })).toBeNull();
  });

  it('summerer og lister rutene i regelrekkefølge', () => {
    expect(utenforAktorkoenTekst({ boyningsform: 2, generisk: 8, kjent_aktor: 4 }))
      .toBe('14 treff er kjente ord og ble ikke lagt i køen: generisk ord 8, kjent aktør 4, bøyningsform av et kjent ord 2.');
    expect(utenforAktorkoenTekst({ kjent_begrep: 1 }))
      .toBe('1 treff er et kjent ord og ble ikke lagt i køen: kjent klasse, rolle eller område 1.');
  });
});
