/**
 * [Ny, issue #440 PR 1] Setningen sveipene viser om treffene aktørkøens ruting sendte et annet sted. Ingen setning når ingenting ble sendt
 * et annet sted (ikke «0 treff …»), og rutene står i samme rekkefølge som reglene prøves.
 */
import { describe, expect, it } from 'vitest';
import { blirNavnekandidatIAktorsveipet, erGeneriskOrdUtenBinding, utenforAktorkoenTekst } from './aktorkorute';

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

/**
 * [Ny, issue #440, Johanns avgjørelse 2026-10-31] Aktørsveipet: generisk ord uten binding blir fortsatt navnekandidat («Behandle» til
 * én-skjermen, docs/09 §42), alle andre kjente ord holdes utenfor. Samme tabell som AktorkoreglerTests i Data-testene.
 */
describe('blirNavnekandidatIAktorsveipet', () => {
  it('gjør unntak for generisk ord uten binding', () => {
    expect(blirNavnekandidatIAktorsveipet('generisk', 'generisk')).toBe(true);
    expect(erGeneriskOrdUtenBinding('generisk', 'generisk')).toBe(true);
  });

  it('legger ukjente navn i køen, som før', () => {
    expect(blirNavnekandidatIAktorsveipet('ny_aktor', 'aktorko')).toBe(true);
    expect(blirNavnekandidatIAktorsveipet('ny_navneform_for_kjent_aktor', 'aktorko')).toBe(true);
    expect(blirNavnekandidatIAktorsveipet('ny_aktor', null)).toBe(true); // eldre svar uten rute: regelen fra før #440
  });

  it('holder alle andre kjente ord utenfor', () => {
    expect(blirNavnekandidatIAktorsveipet('generisk', 'boyningsform')).toBe(false); // bøyningsform arver aldri unntaket
    expect(blirNavnekandidatIAktorsveipet('ny_aktor', 'boyningsform')).toBe(false);
    expect(blirNavnekandidatIAktorsveipet('ny_aktor', 'generisk')).toBe(false);     // status og rute må begge si generisk
    expect(blirNavnekandidatIAktorsveipet('ny_aktor', 'kjent_aktor')).toBe(false);
    expect(blirNavnekandidatIAktorsveipet('ny_aktor', 'kjent_begrep')).toBe(false);
    expect(erGeneriskOrdUtenBinding('ny_aktor', 'generisk')).toBe(false);
  });

  it('gir aldri kandidat for kjente eller flertydige', () => {
    expect(blirNavnekandidatIAktorsveipet('kjent', 'generisk')).toBe(false);
    expect(blirNavnekandidatIAktorsveipet('kjent', 'aktorko')).toBe(false);
    expect(blirNavnekandidatIAktorsveipet('flertydig', 'generisk')).toBe(false);
  });
});
