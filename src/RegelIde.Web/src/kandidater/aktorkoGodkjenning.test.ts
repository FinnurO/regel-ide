import { describe, expect, it } from 'vitest';
import type { AktorkoDetalj } from '../api/types';
import { TOMT_UTKAST, godkjenningMangler, grunnvarsel, skrivemater, tilForesporsel } from './aktorkoGodkjenning';

const forslag = {
  rettskildeId: 'bhl', rettskilde: 'Barnehageloven', kortnavn: 'bhl', eid: 'https://lovdata.no/eli/lov/2005/06/17/64/nor/§4/ledd-1',
  bestemmelse: '§ 4 første ledd', uttrykk: 'skal … ha et', setning: 'skal hver barnehage ha et foreldreråd', treff: 'foreldreråd', treffStart: 0,
  kunnskapsstatus: 'normalisering',
};
const detalj: AktorkoDetalj = {
  nokkel: 'foreldrerådet', navn: 'foreldrerådet', forekomster: 3,
  varianter: [{ normalisert: 'foreldrerådet', regel: 'samme_ord', boyningAv: null, suffiks: null, forekomster: 3,
    skrivemater: [{ tekst: 'foreldrerådet', forekomster: 2 }, { tekst: 'Foreldrerådet', forekomster: 1 }] }],
  genitivforslagInn: [], genitivforslagUt: [], perRettskilde: [], hjemmelforslag: [forslag], hjemmelregel: '',
};

describe('godkjenningen i aktørkøen (#440 PR 2)', () => {
  it('ingenting er forhåndsvalgt: det tomme utkastet kan ikke sendes', () => {
    expect(godkjenningMangler(TOMT_UTKAST)).toBe('Velg hva navnet er.');
    expect(tilForesporsel(TOMT_UTKAST, detalj)).toBeNull();
  });

  it('klasse uten hjemmel krever at man velger hvor den gjelder', () => {
    const u = { ...TOMT_UTKAST, type: 'klasse' as const, navn: 'foreldrerådet', hjemmel: 'mangler', godkjenning: 'godkjent' as const };
    expect(godkjenningMangler(u)).toBe('Velg hvor klassen gjelder.');
    const fast = tilForesporsel({ ...u, gjelder: 'fast' }, detalj);
    expect(fast).toMatchObject({ hjemmel: 'mangler', gjelderFast: true, gjelderILovkildeId: null });
  });

  it('et valgt forslag blir hjemmel med rettskilde og eId, og antall forekomster følger med', () => {
    const r = tilForesporsel({ ...TOMT_UTKAST, type: 'aktor', navn: 'Foreldrerådet', hjemmel: `bhl|${forslag.eid}`, godkjenning: 'faglig_vurdert' }, detalj);
    expect(r).toMatchObject({ hjemmel: 'forslag', hjemmelRettskildeId: 'bhl', hjemmelEid: forslag.eid, forventetForekomster: 3, gjelderFast: null });
  });

  it('varsler bare ved utgått navn og kortform', () => {
    expect(grunnvarsel('utgatt', 'X', 'Y')).toContain('UTGÅTT');
    expect(grunnvarsel('kortform', 'X', 'Y')).toContain('KORTFORM');
    expect(grunnvarsel('gjeldende', 'X', 'Y')).toBeNull();
  });

  it('skrivemåtene er alle formene fra teksten, uten dubletter', () => {
    expect(skrivemater(detalj)).toEqual(['foreldrerådet', 'Foreldrerådet']);
  });
});
