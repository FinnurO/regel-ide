/**
 * Tester for departement→virksomhet-oppslaget (issue #128). Det som må låses er «ingen gjettet
 * fallback»: et departementnavn som ikke finnes eksakt i katalogen skal bli TEKST, aldri en lenke til
 * nærmeste treff. Navnene under er ekte, hentet fra dev-basen 2026-09-09.
 */
import { describe, expect, it } from 'vitest';
import { byggDepartementOppslag, departementVirksomhetId, type VirksomhetNavnerad } from './departementLenke';

const katalog: VirksomhetNavnerad[] = [
  { id: 'fin', navn: 'Finansdepartementet' },
  { id: 'jd', navn: 'Justis- og beredskapsdepartementet' },
  // Registerets form er VERSALER for Brreg-synkroniserte rader (issue #158) — nettopp derfor er
  // matchen case-insensitiv på serveren, og må være det her.
  { id: 'lmd', navn: 'LANDBRUKS- OG MATDEPARTEMENTET' },
];
const oppslag = byggDepartementOppslag(katalog);

describe('departementVirksomhetId', () => {
  it('finner departementet ved eksakt navn', () => {
    expect(departementVirksomhetId('Finansdepartementet', oppslag)).toBe('fin');
  });

  it('matcher case-insensitivt i begge retninger — registerets VERSALER mot Lovdatas skrivemåte', () => {
    expect(departementVirksomhetId('Landbruks- og matdepartementet', oppslag)).toBe('lmd');
    expect(departementVirksomhetId('FINANSDEPARTEMENTET', oppslag)).toBe('fin');
  });

  it('gir ingen lenke for et navn som ikke finnes i katalogen («Stortinget»)', () => {
    expect(departementVirksomhetId('Stortinget', oppslag)).toBeUndefined();
  });

  it('gir ingen lenke for Lovdatas konkatenerte flerverdi-streng — aldri nærmeste treff', () => {
    // Ekte rad i korpuset 2026-09-09. Den INNEHOLDER «Finansdepartementet», og en delvis match ville
    // gitt en lenke til Finansdepartementet alene — som er en gal påstand om hvem som forvalter loven.
    expect(departementVirksomhetId('FinansdepartementetJustis- og beredskapsdepartementet', oppslag)).toBeUndefined();
  });

  it('gir ingen lenke før virksomhetslisten er hentet — ikke en «ukjent»-påstand mens data lastes', () => {
    expect(departementVirksomhetId('Finansdepartementet', undefined)).toBeUndefined();
  });
});

describe('byggDepartementOppslag', () => {
  it('lar den første raden vinne ved case-ufølsomt likt navn — samme utfall som serverens FirstOrDefault', () => {
    const kart = byggDepartementOppslag([
      { id: 'forst', navn: 'Kunnskapsdepartementet' },
      { id: 'sist', navn: 'KUNNSKAPSDEPARTEMENTET' },
    ]);
    expect(kart.get('kunnskapsdepartementet')).toBe('forst');
  });

  it('er tomt for en tom liste', () => {
    expect(byggDepartementOppslag([]).size).toBe(0);
  });
});
