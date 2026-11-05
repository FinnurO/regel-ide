/**
 * [Ny, #440, 2026-11-05, docs/09 §51] «Kan et klikk på en etikett i en radiogruppe velge en annen knapp enn den brukeren klikket på?» Ikke
 * så lenge ingen `Radio` eller `Checkbox` står i en `Field`. Testen leser kilden til alle komponentene og feiler på hvert sted mønsteret
 * kommer tilbake, med fil og linje. Før rettingen fant den 17 steder i 6 filer.
 */
import { describe, expect, it } from 'vitest';
import { radiogrupperIField } from './feltgrupper';

describe('radiogrupperIField', () => {
  it('finner en radiogruppe i Field, med linjen', () => {
    const kilde = [
      '<Field data-size="sm">',
      '  <Label>Godkjenning</Label>',
      '  <Radio name="g" value="a" label="A" />',
      '  <Radio name="g" value="b" label="B" />',
      '</Field>',
    ].join('\n');
    expect(radiogrupperIField(kilde)).toEqual([{ linje: 1, radio: 2, checkbox: 0 }]);
  });

  it('finner også avkrysningsbokser i Field', () => {
    expect(radiogrupperIField('<Field>\n<Label>X</Label>\n<Checkbox label="a" />\n</Field>')).toEqual([{ linje: 1, radio: 0, checkbox: 1 }]);
  });

  it('godtar Fieldset, og Field uten knapper', () => {
    const kilde = [
      '<Fieldset data-size="sm">',
      '  <Fieldset.Legend>Hva er det?</Fieldset.Legend>',
      '  <Radio name="t" value="a" label="A" />',
      '  <Field><Label>Fra</Label><Select /></Field>',
      '</Fieldset>',
      '<Field><Label>Grunn</Label><Textfield /></Field>',
    ].join('\n');
    expect(radiogrupperIField(kilde)).toEqual([]);
  });

  it('teller en Field inni en annen Field for seg', () => {
    const kilde = '<Field>\n<Field>\n<Radio />\n</Field>\n<Select />\n</Field>';
    expect(radiogrupperIField(kilde)).toEqual([
      { linje: 1, radio: 1, checkbox: 0 },
      { linje: 2, radio: 1, checkbox: 0 },
    ]);
  });
});

describe('frontenden', () => {
  // Vite leser kildene som tekst; ingen rendring og ingen node-typer trengs.
  const kilder = import.meta.glob<string>('../**/*.tsx', { query: '?raw', import: 'default', eager: true });

  it('leser komponentene (vakt mot en tom glob som ville gjort testen virkningsløs)', () => {
    expect(Object.keys(kilder).length).toBeGreaterThan(50);
    expect(Object.keys(kilder)).toContain('../kandidater/Utsagnssteg.tsx');
  });

  it('har ingen Radio eller Checkbox i en Field', () => {
    const funn = Object.entries(kilder).flatMap(([fil, kilde]) =>
      radiogrupperIField(kilde).map((b) => `${fil}:${b.linje} (${b.radio} Radio, ${b.checkbox} Checkbox)`));
    expect(funn).toEqual([]);
  });
});
