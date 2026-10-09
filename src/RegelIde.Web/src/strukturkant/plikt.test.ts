/**
 * [Ny, issue #353, 2026-10-09] Tester for P-hjelperne (plikt.ts). Det som må låses: modaliteten forhåndsvelges aldri og «bor»
 * leses «bør»; skjemaet tilbyr ikke typer serveren alltid avviser for en ordning; og S6-statusene har fargerollene fra
 * tilhørigheten (docs/09 §32/§34) — løst = ingen tag, «Ikke entydig» = warning, «Mangler»/«Ikke angitt» = neutral.
 * Typekodene og malene under er de faktiske fra Strukturkanter.cs (#353), ikke oppdiktede.
 */
import { describe, expect, it } from 'vitest';
import {
  MODALITETER, modalitetFraValg, modalitetsord, motpartStatusVisning, ordningKanVaereMotpart, pliktGrunnlagTekst,
  pliktValgtekst, typerForAktortype,
} from './plikt';

describe('modalitetsord', () => {
  it('leser «bor» som «bør» — lagret uten ø, vist med (samme som StrukturkantTjeneste.Modalitetsord)', () => {
    expect(modalitetsord('bor')).toBe('bør');
  });
  it('lar skal og kan stå', () => {
    expect(modalitetsord('skal')).toBe('skal');
    expect(modalitetsord('kan')).toBe('kan');
  });
  it('gir null for ikke angitt — ingen gjettet «skal» for presens', () => {
    expect(modalitetsord(null)).toBeNull();
    expect(modalitetsord(undefined)).toBeNull();
    expect(modalitetsord('')).toBeNull();
  });
});

describe('modalitetFraValg', () => {
  it('«Ikke angitt» (tom streng) blir null, ikke en standardverdi', () => {
    expect(modalitetFraValg('')).toBeNull();
  });
  it('de tre lukkede verdiene går gjennom uendret', () => {
    expect(modalitetFraValg('skal')).toBe('skal');
    expect(modalitetFraValg('kan')).toBe('kan');
    expect(modalitetFraValg('bor')).toBe('bor');
  });
  it('en verdi utenfor lista blir null — «bør» med ø er visningsformen, ikke lagringsverdien', () => {
    expect(modalitetFraValg('bør')).toBeNull();
    expect(modalitetFraValg('må')).toBeNull();
  });
  it('velgeren har nøyaktig skal/kan/bør i den rekkefølgen', () => {
    expect(MODALITETER.map((m) => m.tekst)).toEqual(['Skal', 'Kan', 'Bør']);
  });
});

describe('pliktValgtekst', () => {
  it('bygger valgteksten av malen, med motparten i stedet for {0}', () => {
    expect(pliktValgtekst({ fraVisningsmal: 'har samarbeidsplikt {0}' }))
      .toBe('Plikt — «har samarbeidsplikt overfor motparten»');
  });
});

describe('typerForAktortype', () => {
  const typer = [
    { kategori: 'R' as const, kode: 'eies_av' },
    { kategori: 'R' as const, kode: 'forvaltes_av' },
    { kategori: 'K' as const, kode: 'klage' },
    { kategori: 'P' as const, kode: 'samarbeid' },
    { kategori: 'P' as const, kode: 'betaling' },
  ];
  it('en ordning får bare P og R forvaltes_av — den er ikke en aktør (Strukturkanter.OrdningLovSomFra)', () => {
    expect(typerForAktortype(typer, 'ordning').map((t) => `${t.kategori}:${t.kode}`))
      .toEqual(['R:forvaltes_av', 'P:samarbeid', 'P:betaling']);
  });
  it('en aktør får alt unntatt forvaltes_av, som krever en ordning som fra-node', () => {
    expect(typerForAktortype(typer, 'organ').map((t) => t.kode))
      .toEqual(['eies_av', 'klage', 'samarbeid', 'betaling']);
  });
  it('uavklart aktørtype behandles ikke som ordning — typen gjettes ikke', () => {
    expect(typerForAktortype(typer, null).some((t) => t.kode === 'forvaltes_av')).toBe(false);
  });
});

describe('ordningKanVaereMotpart', () => {
  it('bare i P (Strukturkanter.OrdningLovSomTil)', () => {
    expect(ordningKanVaereMotpart('P')).toBe(true);
    expect(ordningKanVaereMotpart('R')).toBe(false);
    expect(ordningKanVaereMotpart('K')).toBe(false);
    expect(ordningKanVaereMotpart(null)).toBe(false);
  });
});

describe('motpartStatusVisning', () => {
  it('løst motpart (konkret, entydig) gir ingen tag — samme som entydig tilhørighet', () => {
    expect(motpartStatusVisning('konkret')).toBeNull();
    expect(motpartStatusVisning('entydig')).toBeNull();
  });
  it('flere kandidater er warning «Ikke entydig» — ingen velges', () => {
    expect(motpartStatusVisning('ikke_entydig')).toMatchObject({ tekst: 'Ikke entydig', farge: 'warning' });
  });
  it('mangler og ikke angitt er neutral', () => {
    expect(motpartStatusVisning('mangler')).toMatchObject({ tekst: 'Mangler', farge: 'neutral' });
    expect(motpartStatusVisning('ikke_angitt')).toMatchObject({ tekst: 'Ikke angitt', farge: 'neutral' });
  });
  it('en ukjent status vises rå, ikke skjult og ikke tolket', () => {
    expect(motpartStatusVisning('noe_nytt')).toMatchObject({ tekst: 'noe_nytt', farge: 'neutral' });
  });
});

describe('pliktGrunnlagTekst', () => {
  it('sier hvorfor plikten gjelder kommunen', () => {
    expect(pliktGrunnlagTekst('direkte')).toBe('Direkte (kommunen selv)');
    expect(pliktGrunnlagTekst('medlem_av', 'språkutviklingskommuner')).toBe('Via medlemskap i «språkutviklingskommuner»');
    expect(pliktGrunnlagTekst('klasse_uten_registrert_medlemskap', 'kommunen'))
      .toBe('«kommunen» — klasse uten registrert medlemskap');
  });
  it('uten klassenavn faller teksten tilbake på grunnlaget alene, ikke et gjettet navn', () => {
    expect(pliktGrunnlagTekst('medlem_av')).toBe('Via registrert medlemskap');
    expect(pliktGrunnlagTekst('klasse_uten_registrert_medlemskap')).toBe('Klasse uten registrert medlemskap');
  });
  it('ukjent grunnlag vises rått', () => {
    expect(pliktGrunnlagTekst('noe_annet')).toBe('noe_annet');
  });
});
