import type { AktorRettskilderadDto, AktorRettskildeTallDto } from '../api/types';

/**
 * [Ny, issue #441] Reglene for lista «Andre rettskilder aktøren står i» på aktørsiden, som rene funksjoner (docs/09 §17: en regel som er viktig
 * nok å teste, bor i en ren modul). Sorteringen kommer fra API-et (rader med utsagn først); her filtreres og telles det bare.
 */

export type RettskildeFilter = 'alle' | 'med_utsagn' | 'venter' | 'ansvarlig' | 'fastsatt' | 'tagget' | 'direkte' | 'via_klasse';

/** Utsagnene i raden: kompetanse, plikt, oppgave og annet. Hvert utsagn står i én rad (AK 3: ingen dobbelttelling). */
export function utsagnIRaden(r: AktorRettskilderadDto): number {
  return r.kompetanse + r.plikt + r.oppgave + r.annet;
}

export function filtrer(rader: AktorRettskilderadDto[], filter: RettskildeFilter): AktorRettskilderadDto[] {
  switch (filter) {
    case 'alle': return rader;
    case 'med_utsagn': return rader.filter((r) => utsagnIRaden(r) > 0);
    case 'venter': return rader.filter((r) => r.venter > 0);
    case 'ansvarlig': return rader.filter((r) => r.ansvarligDepartement);
    case 'fastsatt': return rader.filter((r) => r.fastsattAv);
    case 'tagget': return rader.filter((r) => r.navnetTagget > 0);
    case 'direkte': return rader.filter((r) => r.gruppe === 'direkte');
    case 'via_klasse': return rader.filter((r) => r.gruppe === 'via_klasse');
  }
}

/** Filterknappene som vises: «Direkte» og «Via klasse» bare når aktøren har treff via klasse (en kommune); de andre bare når de har rader. */
export function synligeFiltre(tall: AktorRettskildeTallDto): { filter: RettskildeFilter; tekst: string; antall: number }[] {
  const alle: { filter: RettskildeFilter; tekst: string; antall: number }[] = [
    { filter: 'alle', tekst: 'Alle', antall: tall.rettskilder },
    { filter: 'med_utsagn', tekst: 'Med utsagn', antall: tall.medUtsagn },
    { filter: 'venter', tekst: 'Venter', antall: tall.venter },
  ];
  if (tall.viaKlasse > 0) {
    alle.push({ filter: 'direkte', tekst: 'Direkte', antall: tall.direkte }, { filter: 'via_klasse', tekst: 'Via klasse', antall: tall.viaKlasse });
  }
  alle.push(
    { filter: 'ansvarlig', tekst: 'Ansvarlig departement', antall: tall.ansvarligDepartement },
    { filter: 'fastsatt', tekst: 'Fastsatt av', antall: tall.fastsattAv },
    { filter: 'tagget', tekst: 'Navnet tagget', antall: tall.navnetTagget },
  );
  return alle.filter((f) => f.filter === 'alle' || f.antall > 0);
}

/** AK 3: tellerne i radene og utsagnene i hjemmelkortet summerer til alle kantene aktøren er part i, uten dobbelttelling. */
export function kontrollsumStemmer(rader: AktorRettskilderadDto[], tall: AktorRettskildeTallDto): boolean {
  const iRadene = rader.filter((r) => r.gruppe === 'direkte').reduce((s, r) => s + utsagnIRaden(r), 0);
  return iRadene === tall.kanterIRadene && tall.kanterIRadene + tall.kanterIHjemmelen === tall.kanter;
}

/** Ventende utsagn i raden som «Godkjenn alle N» gjelder (bare direkte utsagn; forslag via klasse godkjennes på klassen). */
export function ventendeUtsagn(r: AktorRettskilderadDto): number {
  return r.utsagn.filter((u) => u.status === 'maskinelt_foreslatt' || u.status === 'manuelt_foreslatt' || u.status === 'faglig_vurdert').length;
}
