/**
 * Tester for navneform-kjedens hovedledd — at «Karasjok» resolver til navneformen «Karasjok
 * kommune» og IKKE til virksomhetens tospråklige registernavn, som var Johanns andre innvending
 * (tagg-synlig-runden, 2026-09-08). Se navneformKjede.ts for hvorfor kjeden løses i visningen.
 */
import { describe, expect, it } from 'vitest';
import type { BegrepDto } from '../api/types';
import { finnHovedledd } from './navneformKjede';

const KARASJOK = 'v-karasjok';
const REGISTERNAVN = 'Karasjoga gielda / Karasjok kommune';

/** Bare feltene `finnHovedledd` faktisk leser — resten av BegrepDto er irrelevant her. */
function navneform(term: string, grunn: BegrepDto['navneformgrunn'], virksomhetId = KARASJOK): BegrepDto {
  return { term, navneformgrunn: grunn, virksomhetReferanseId: virksomhetId } as BegrepDto;
}

describe('finnHovedledd', () => {
  it('velger den GJELDENDE navneformen framfor registernavnet', () => {
    const kjede = finnHovedledd(
      KARASJOK,
      [navneform('Karasjok', 'kortform'), navneform('Karasjok kommune', 'gjeldende')],
      REGISTERNAVN,
    );
    expect(kjede?.hovedledd).toBe('Karasjok kommune');
    // Registernavnet er ikke borte — det er flyttet til hover, siden det ikke skal være hovedleddet.
    expect(kjede?.registernavn).toBe(REGISTERNAVN);
  });

  it('faller tilbake til registernavnet når ingen gjeldende navneform finnes', () => {
    // Ingen gjettet fallback: viser det som FAKTISK finnes, aldri «Karasjok» + « kommune».
    const kjede = finnHovedledd(KARASJOK, [navneform('Karasjok', 'kortform')], REGISTERNAVN);
    expect(kjede?.hovedledd).toBe(REGISTERNAVN);
  });

  it('regner ikke en kortform, utgått eller feilskrevet navneform som gjeldende', () => {
    for (const grunn of ['kortform', 'utgatt', 'feilskriving', null] as const) {
      const kjede = finnHovedledd(KARASJOK, [navneform('Karasjok kommune', grunn)], REGISTERNAVN);
      expect(kjede?.hovedledd).toBe(REGISTERNAVN);
    }
  });

  it('ser kun på navneformer som peker på SAMME virksomhet', () => {
    // En annen kommunes gjeldende navneform skal aldri kunne bli Karasjoks hovedledd.
    const kjede = finnHovedledd(
      KARASJOK,
      [navneform('Kautokeino kommune', 'gjeldende', 'v-kautokeino')],
      REGISTERNAVN,
    );
    expect(kjede?.hovedledd).toBe(REGISTERNAVN);
  });

  it('utelater registernavnet fra hover når det er identisk med hovedleddet', () => {
    const kjede = finnHovedledd(KARASJOK, [navneform('Karasjok kommune', 'gjeldende')], 'Karasjok kommune');
    expect(kjede?.hovedledd).toBe('Karasjok kommune');
    expect(kjede?.registernavn).toBeUndefined();
  });

  it('gir undefined når det ikke finnes noe navn å vise i det hele tatt', () => {
    // Virksomheten er ikke lastet OG det finnes ingen gjeldende navneform — da har kalleren
    // ingenting sant å vise, og skal ikke finne på noe.
    expect(finnHovedledd(KARASJOK, [navneform('Karasjok', 'kortform')], undefined)).toBeUndefined();
  });

  it('finner hovedleddet selv om virksomheten ikke er lastet', () => {
    const kjede = finnHovedledd(KARASJOK, [navneform('Karasjok kommune', 'gjeldende')], undefined);
    expect(kjede?.hovedledd).toBe('Karasjok kommune');
    expect(kjede?.registernavn).toBeUndefined();
  });
});
