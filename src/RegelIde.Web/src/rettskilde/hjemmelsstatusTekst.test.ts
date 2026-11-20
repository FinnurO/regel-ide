import { describe, expect, it } from 'vitest';
import { kildetekst, norskDato, SAMLET_STATUS, STATUS } from './hjemmelsstatusTekst';

describe('hjemmelsstatus (#472)', () => {
  it('har tekst for alle statusene API-et kan gi', () => {
    for (const s of ['aktiv', 'opphevet', 'opphevet_bestemmelse', 'viderefort', 'videreforing_utlopt', 'erstattet', 'gjenstandslos', 'ukjent']) {
      expect(STATUS[s]).toBeTruthy();
    }
    for (const s of ['aktiv', 'viderefort', 'videreforing_utlopt', 'hjemmel_opphevet', 'blandede_hjemler', 'uavklart', 'ukjent']) {
      expect(SAMLET_STATUS[s]?.tekst).toBeTruthy();
    }
  });

  it('sier aldri at forskriften er bortfalt, bare at den mulig er det (Johann 2026-11-18)', () => {
    expect(SAMLET_STATUS.hjemmel_opphevet.tekst).toBe('Hjemmel opphevet, videreføring ikke funnet (mulig bortfalt)');
    for (const v of Object.values(SAMLET_STATUS)) expect(v.tekst.toLowerCase()).not.toMatch(/sovende|ugyldig|er bortfalt/);
  });

  it('setter sammen kilden uten tomrom', () => {
    expect(kildetekst({ referanse: 'LOV-2004-03-05-12', bestemmelse: '§ 48 andre ledd' })).toBe('LOV-2004-03-05-12 § 48 andre ledd');
    expect(kildetekst({ referanse: null, bestemmelse: '§ 1', tittel: 'Lov om noe' })).toBe('Lov om noe § 1');
    expect(kildetekst(null)).toBe('');
  });

  it('viser datoen på norsk', () => {
    expect(norskDato('2019-12-31')).toBe('31.12.2019');
    expect(norskDato(null)).toBeNull();
  });
});
