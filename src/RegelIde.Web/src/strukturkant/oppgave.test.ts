import { describe, expect, it } from 'vitest';
import { harModalitet, harMotpartEllerHva, oppgaveValgtekst } from './oppgave';

// [Ny, issue #368] Kategorien F (oppgave/funksjon) i skjemaet.
describe('oppgave (F)', () => {
  it('valgteksten har ingen «overfor» — en oppgave har ingen motpart', () => {
    expect(oppgaveValgtekst({ fraVisningsmal: 'har samordningsoppgave {0}' })).toBe('Oppgave — «har samordningsoppgave …»');
  });

  it('modalitet hører til P og F, ikke K eller R', () => {
    expect(harModalitet('F')).toBe(true);
    expect(harModalitet('P')).toBe(true);
    expect(harModalitet('K')).toBe(false);
    expect(harModalitet('R')).toBe(false);
  });

  it('F krever oppgaven (objektet) også med en motpart; det oppgaven gjelder er valgfritt', () => {
    expect(harMotpartEllerHva('F', 'v1', '', '')).toBe(false);
    expect(harMotpartEllerHva('F', '', 'beredskapsarbeid', '')).toBe(true);
    expect(harMotpartEllerHva('F', '', '', 'https://lovdata.no/eli/x/nor/§1')).toBe(false);
  });

  it('P og R/K er uendret', () => {
    expect(harMotpartEllerHva('P', '', '', 'eid')).toBe(true);
    expect(harMotpartEllerHva('P', '', '', '')).toBe(false);
    expect(harMotpartEllerHva('K', '', 'x', '')).toBe(false);
    expect(harMotpartEllerHva('R', 'v1', '', '')).toBe(true);
  });
});
