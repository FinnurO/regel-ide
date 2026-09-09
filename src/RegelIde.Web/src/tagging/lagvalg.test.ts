/**
 * Tester for lagvalget — den regelen som avgjør om det står NOE markert i lovteksten når en side
 * åpnes kaldt. Feilen som ble rettet i tagg-synlig-runden (2026-09-08) var nettopp at denne regelen
 * ikke fantes: aktivt lag var alltid `kinds[0]`.
 *
 * <p>Appens FØRSTE frontend-tester (det fantes ingen JS/TS-test-runner i repoet før dette). Kun rene
 * funksjoner testes — ingen React-rendring, ingen DOM — derfor er `vitest` alene nok, uten
 * @testing-library/jsdom. Se docs/09-design-konvensjoner.md §17.</p>
 */
import { describe, expect, it } from 'vitest';
import type { TagKind, TextTag } from './TagTekst';
import { finnStandardLag, velgAktivtLag } from './lagvalg';

/** Samme rekkefølge som taggkind-konfigurasjonen har i dag: «Begrep» først, «Virksomhet» sist. */
const kinds: TagKind[] = [
  { id: 'begrep', label: 'Begrep', color: 'accent' },
  { id: 'vilkar', label: 'Vilkår', color: 'warning' },
  { id: 'virksomhet', label: 'Virksomhet', color: 'info' },
];

function tagg(kind: string, id = `t-${kind}`): TextTag {
  return { id, start: 0, end: 5, kind, ref: null };
}

describe('finnStandardLag', () => {
  it('velger laget noden FAKTISK har tagger i, ikke det første konfigurerte', () => {
    // Forskrift 2005-06-17-657 § 1 ledd-1: kun virksomhet-tagger. Før fiksen ga dette 'begrep',
    // og teksten sto umarkert selv om tagg-listen viste 14 rader.
    expect(finnStandardLag([tagg('virksomhet')], kinds)).toBe('virksomhet');
  });

  it('virker begge veier — en node med kun begrep-tagger gir begrep', () => {
    // Sameloven § 3-1 ledd-1 punkt-1.
    expect(finnStandardLag([tagg('begrep')], kinds)).toBe('begrep');
  });

  it('bryter likhet på kinds-rekkefølgen når lagene har LIKE MANGE tagger', () => {
    expect(finnStandardLag([tagg('virksomhet'), tagg('vilkar')], kinds)).toBe('vilkar');
  });

  it('velger laget med FLEST tagger, ikke det første med minst én', () => {
    // Forskrift 2005-06-17-657 § 1 ledd-1 etter at gruppebegrepene ble tagget (2026-09-09): 7
    // begrep-tagger og 14 virksomhet-tagger på samme node. «Første med minst én» ga Begrep, som
    // skjulte de 14 kommunenavnene bak en fane.
    const tags = [
      ...Array.from({ length: 7 }, (_, i) => tagg('begrep', `b-${i}`)),
      ...Array.from({ length: 14 }, (_, i) => tagg('virksomhet', `v-${i}`)),
    ];
    expect(finnStandardLag(tags, kinds)).toBe('virksomhet');
  });

  it('velger fortsatt begrep når begrep har flest', () => {
    const tags = [
      ...Array.from({ length: 3 }, (_, i) => tagg('begrep', `b-${i}`)),
      tagg('virksomhet', 'v-0'),
    ];
    expect(finnStandardLag(tags, kinds)).toBe('begrep');
  });

  it('faller tilbake til første lag når noden ikke har tagger i det hele tatt', () => {
    expect(finnStandardLag([], kinds)).toBe('begrep');
  });

  it('ignorerer tagger i et lag som ikke er konfigurert', () => {
    // Et lag som er fjernet fra konfigurasjonen skal ikke kunne bli aktivt lag — det finnes ingen
    // knapp å bytte tilbake fra.
    expect(finnStandardLag([tagg('utgatt-lag')], kinds)).toBe('begrep');
  });

  it('gir undefined når det ikke finnes lag i det hele tatt', () => {
    expect(finnStandardLag([tagg('begrep')], [])).toBeUndefined();
  });
});

describe('velgAktivtLag', () => {
  it('lar brukerens eget valg overstyre defaulten', () => {
    // Noden har kun virksomhet-tagger, men brukeren har valgt Begrep selv: da skal han BLI der,
    // med tom markering — han spurte om det laget.
    expect(velgAktivtLag('begrep', [tagg('virksomhet')], kinds)).toBe('begrep');
  });

  it('holder brukerens valg også når det valgte laget ikke har tagger', () => {
    expect(velgAktivtLag('vilkar', [tagg('virksomhet'), tagg('begrep')], kinds)).toBe('vilkar');
  });

  it('deriverer når brukeren ikke har valgt (null, undefined og tom streng)', () => {
    // Tom streng er tilstanden en kontrollerende forelder starter i — se `activeKind`-propen.
    for (const ikkeValgt of [null, undefined, '']) {
      expect(velgAktivtLag(ikkeValgt, [tagg('virksomhet')], kinds)).toBe('virksomhet');
    }
  });
});
