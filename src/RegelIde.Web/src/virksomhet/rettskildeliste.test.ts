/**
 * [Ny, issue #441] Tester for rettskildelista på aktørsiden: filtrene, filterknappene og kontrollsummen i AK 3 (tellerne summerer til alle
 * kantene, uten dobbelttelling, og hjemmelsvedtaket gjentas ikke). Tallene i den siste testen er KD i kopien av den delte basen 2026-11-05.
 */
import { describe, expect, it } from 'vitest';
import type { AktorRettskilderadDto, AktorRettskildeTallDto } from '../api/types';
import { filtrer, kontrollsumStemmer, synligeFiltre, utsagnIRaden } from './rettskildeliste';

function rad(over: Partial<AktorRettskilderadDto>): AktorRettskilderadDto {
  return {
    gruppe: 'direkte', rettskildeId: 'r', tittel: 'T', kortnavn: null, kildetype: 'Forskrift', dokumentId: null, kildeUtenforKorpusType: null,
    ansvarligDepartement: false, fastsattAv: false, navnetTagget: 0, navnetreffVenter: 0, navneformerVenter: 0,
    kompetanse: 0, plikt: 0, oppgave: 0, annet: 0, venter: 0, utsagn: [], viaKlasse: [], ...over,
  };
}

const tall = (over: Partial<AktorRettskildeTallDto>): AktorRettskildeTallDto => ({
  rettskilder: 0, medUtsagn: 0, venter: 0, ansvarligDepartement: 0, fastsattAv: 0, navnetTagget: 0, direkte: 0, viaKlasse: 0,
  kanter: 0, kanterIRadene: 0, kanterIHjemmelen: 0, ...over,
});

describe('filtrer', () => {
  const rader = [
    rad({ rettskildeId: 'bhl', kompetanse: 33, venter: 33, ansvarligDepartement: true }),
    rad({ rettskildeId: 'dep', ansvarligDepartement: true }),
    rad({ rettskildeId: 'fast', fastsattAv: true, navnetTagget: 1 }),
    rad({ rettskildeId: 'via', gruppe: 'via_klasse', kompetanse: 2, venter: 2 }),
  ];
  it('«Med utsagn» tar bare rader med utsagn, ikke rader med bare merker', () => {
    expect(filtrer(rader, 'med_utsagn').map((r) => r.rettskildeId)).toEqual(['bhl', 'via']);
  });
  it('«Ansvarlig departement» er et merke, ikke et utsagn: raden teller ikke som utsagn', () => {
    const dep = filtrer(rader, 'ansvarlig').find((r) => r.rettskildeId === 'dep')!;
    expect(utsagnIRaden(dep)).toBe(0);
  });
  it('direkte og via klasse står hver for seg', () => {
    expect(filtrer(rader, 'direkte')).toHaveLength(3);
    expect(filtrer(rader, 'via_klasse').map((r) => r.rettskildeId)).toEqual(['via']);
  });
});

describe('synligeFiltre', () => {
  it('viser «Direkte» og «Via klasse» bare når det finnes treff via klasse, og skjuler tomme filtre', () => {
    const kd = synligeFiltre(tall({ rettskilder: 391, medUtsagn: 8, venter: 8, ansvarligDepartement: 384, fastsattAv: 93, navnetTagget: 3, direkte: 391 }));
    expect(kd.map((f) => f.tekst)).toEqual(['Alle', 'Med utsagn', 'Venter', 'Ansvarlig departement', 'Fastsatt av', 'Navnet tagget']);
    const kommune = synligeFiltre(tall({ rettskilder: 13, medUtsagn: 13, direkte: 2, viaKlasse: 11, navnetTagget: 1 }));
    expect(kommune.map((f) => f.tekst)).toEqual(['Alle', 'Med utsagn', 'Direkte', 'Via klasse', 'Navnet tagget']);
  });
});

describe('kontrollsumStemmer (AK 3)', () => {
  it('KD: 50 utsagn i radene + 15 i hjemmelsvedtaket = 65 kanter', () => {
    const rader = [rad({ kompetanse: 33 }), rad({ oppgave: 6 }), rad({ oppgave: 3 }), rad({ kompetanse: 1, annet: 1 }), rad({ kompetanse: 2 }),
      rad({ kompetanse: 2 }), rad({ kompetanse: 1 }), rad({ kompetanse: 1 }), rad({ gruppe: 'via_klasse', kompetanse: 5 })];
    expect(kontrollsumStemmer(rader, tall({ kanter: 65, kanterIRadene: 50, kanterIHjemmelen: 15 }))).toBe(true);
  });
  it('avslører dobbelttelling (hjemmelsvedtaket også i en rad)', () => {
    const rader = [rad({ kompetanse: 50 }), rad({ oppgave: 13, annet: 2 })];
    expect(kontrollsumStemmer(rader, tall({ kanter: 65, kanterIRadene: 50, kanterIHjemmelen: 15 }))).toBe(false);
  });
});
