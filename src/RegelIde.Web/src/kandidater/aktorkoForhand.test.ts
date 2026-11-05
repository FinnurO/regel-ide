import { describe, expect, it } from 'vitest';
import type { AktorkoDetalj, AktorkoForhandsklassifisering } from '../api/types';
import { godkjenningMangler, tilForesporsel } from './aktorkoGodkjenning';
import {
  erBareSikreAktorer, godkjenningFraForslag, handlingstekst, juristlinje, koblingFraForslag, passerForhandsfilter, relasjonstekst, skrivemateFor,
} from './aktorkoForhand';

const eid = 'https://lovdata.no/eli/forskrift/2017/06/22/1050/nor/§2/ledd-1';
const detalj: AktorkoDetalj = {
  nokkel: 'bostedskommune', navn: 'bostedskommune', forekomster: 2,
  varianter: [
    { normalisert: 'bostedskommune', regel: 'samme_ord', boyningAv: null, suffiks: null, forekomster: 1, skrivemater: [{ tekst: 'bostedskommune', forekomster: 1 }] },
    { normalisert: 'bostedskommunen', regel: 'boyning', boyningAv: 'bostedskommune', suffiks: '+n', forekomster: 1, skrivemater: [{ tekst: 'bostedskommunen', forekomster: 1 }] },
  ],
  genitivforslagInn: [], genitivforslagUt: [], hjemmelregel: '', hjemmelforslag: [],
  perRettskilde: [{ rettskildeId: 'f1050', tittel: 'Forskrift om spesialpedagogisk hjelp', kortnavn: null, forekomster: [] }],
};
const rolle: AktorkoForhandsklassifisering = {
  nr: 24, type: 'rolle', typeforslag: 'rolle', undertype: null, nyNode: 'bostedskommune', handling: 'godkjenn', sikkerhet: 'hoy',
  begrunnelse: 'En rolle en kommune har overfor noen.', relasjoner: [],
  hjemmel: { rettskildeId: 'f1050', rettskilde: 'Forskrift om spesialpedagogisk hjelp', eid, bestemmelse: '§ 2 første ledd', sitat: '…',
    sitatKilde: 'klassifiseringen', kunnskapsstatus: 'fortolkning', erRegelensForslag: false, kanLagres: true },
  hjemmelStatus: 'fortolkning', hjemmelMerknad: null, juristdom: 'innvending', juristAlvor: 'lav', juristInnvending: 'snudd', modellererSvar: 'aksepter',
  replikk: null, rettetEtterJuristen: true, venterPaModellvalg: false, pilot: true, utdatert: null, kobling: null, taMedINokkel: null, taMedINavn: null,
  egneSteg: [], kilde: 'klassifisering.json, n24',
};

describe('forhåndsklassifiseringen i aktørkøen (#440)', () => {
  it('«Godta forslaget» fyller godkjenningen med forhåndsklassifiseringens hjemmel, uten godkjenningsnivå og tolkningsargumenter', () => {
    const { utkast, merknader } = godkjenningFraForslag(rolle, detalj);
    // Johanns avgjørelse 2026-11-05: godkjenningsnivået står tomt.
    expect(utkast).toMatchObject({ type: 'rolle', navn: 'bostedskommune', hjemmel: 'forhand', gjelder: null, godkjenning: null, tolkningsargumenter: [] });
    expect(utkast.begrunnelse).toContain('Forhåndsklassifisering (#440');
    expect(merknader).toEqual(['Hjemmelen er fortolkning. Velg tolkningsargumentene; forhåndsklassifiseringen har dem ikke.', 'Velg hvor sikkert det er. Det fylles ikke ut.']);
    const medF = { ...detalj, forhandsklassifisering: rolle };
    expect(godkjenningMangler({ ...utkast, godkjenning: 'faglig_vurdert' }, medF)).toBe('Velg minst ett tolkningsargument: hjemmelen er fortolkning.');
    const klar = { ...utkast, godkjenning: 'godkjent' as const, tolkningsargumenter: ['ordlyd' as const] };
    expect(godkjenningMangler(klar, medF)).toBeNull();
    expect(tilForesporsel(klar, medF)).toMatchObject({
      type: 'rolle', hjemmel: 'forhandsklassifisering', hjemmelRettskildeId: 'f1050', hjemmelEid: eid, gjelderILovkildeId: null, gjelderFast: null,
      tolkningsargumenter: ['ordlyd'], godkjenning: 'godkjent',
    });
  });

  it('en hjemmel som ikke står i korpus gir «hjemmel mangler» med merknad', () => {
    const f = { ...rolle, hjemmel: { ...rolle.hjemmel!, kanLagres: false } };
    const { utkast, merknader } = godkjenningFraForslag(f, detalj);
    expect(utkast.hjemmel).toBe('mangler');
    expect(merknader[0]).toContain('står ikke entydig i korpus');
  });

  it('regelens forslag blir valgt som hjemmel', () => {
    const forslag = { rettskildeId: 'f1050', rettskilde: 'F', kortnavn: null, eid, bestemmelse: null, uttrykk: '', setning: '', treff: '', treffStart: 0, kunnskapsstatus: 'tekstfunn' };
    const f = { ...rolle, hjemmel: { ...rolle.hjemmel!, erRegelensForslag: true } };
    const { utkast, merknader } = godkjenningFraForslag(f, { ...detalj, hjemmelforslag: [forslag] });
    expect(utkast.hjemmel).toBe(`f1050|${eid}`);
    expect(utkast.gjelder).toBeNull();
    expect(merknader).toEqual(['Velg hvor sikkert det er. Det fylles ikke ut.']);
  });

  it('et navn som ikke står i teksten fylles ut med forslagets navn (grunnformen)', () => {
    const { utkast, merknader } = godkjenningFraForslag({ ...rolle, nyNode: 'omsorgskommune' }, detalj);
    expect(utkast.navn).toBe('omsorgskommune');
    expect(merknader[0]).toContain('står ikke i teksten');
  });

  it('en klasse uten hjemmel og uten rettskilde lar «hvor den gjelder» stå tomt', () => {
    const { utkast, merknader } = godkjenningFraForslag({ ...rolle, type: 'klasse', typeforslag: 'klasse', hjemmel: null }, detalj);
    expect(utkast.gjelder).toBeNull();
    expect(merknader).toContain('Forslaget sier ikke hvor klassen gjelder. Velg rettskilden, eller hele korpuset.');
  });

  it('skrivemåten: nøyaktig først, så den ene uten forskjell på store og små bokstaver, aldri omskrevet', () => {
    expect(skrivemateFor('pedagogisk leder', ['Pedagogisk leder', 'pedagogisk leder'])).toBe('pedagogisk leder');
    expect(skrivemateFor('kommuner i tiltakssonen', ['Kommuner i tiltakssonen'])).toBe('Kommuner i tiltakssonen');
    expect(skrivemateFor('foreldreråd', ['foreldrerådet'])).toBeNull();
  });

  it('koblingen tar grunnen fra forslaget, ellers velger mennesket', () => {
    const kd = { ...rolle, handling: 'koble' as const, kobling: { virksomhetId: 'kd', virksomhetNavn: 'Kunnskapsdepartementet', finnes: true, relasjon: 'samme_som', navneformgrunn: 'kortform' } };
    expect(koblingFraForslag(kd, detalj)).toMatchObject({ aktorId: 'kd', navn: 'bostedskommune', grunn: 'kortform', merknader: [] });
    const uten = { ...kd, kobling: { ...kd.kobling, navneformgrunn: null } };
    expect(koblingFraForslag(uten, detalj).grunn).toBe('');
  });

  it('navn som venter på modellvalg har ingen knapp', () => {
    expect(handlingstekst({ ...rolle, handling: 'venter' })).toBeNull();
    expect(handlingstekst(rolle)).toContain('Godkjenn');
  });

  it('filtrene: type, sikkerhet, «bare aktører med høy sikkerhet» og navn uten klassifisering', () => {
    const aktor = { nr: 5, type: 'aktor' as const, sikkerhet: 'hoy' as const, handling: 'godkjenn' as const, utdatert: null };
    expect(passerForhandsfilter(aktor, 'aktor', 'hoy')).toBe(true);
    expect(passerForhandsfilter({ ...aktor, sikkerhet: 'middels' }, 'aktor', 'hoy')).toBe(false);
    expect(passerForhandsfilter(aktor, 'klasse', 'alle')).toBe(false);
    expect(passerForhandsfilter(null, 'uten', 'alle')).toBe(true);
    expect(passerForhandsfilter(aktor, 'uten', 'alle')).toBe(false);
    expect(passerForhandsfilter(null, 'alle', 'alle')).toBe(true);
    expect(erBareSikreAktorer('aktor', 'hoy')).toBe(true);
  });

  it('tekstene for relasjoner og juristen', () => {
    expect(relasjonstekst('rolle_innehas_av')).toBe('innehas av');
    expect(relasjonstekst('ukjent_kode')).toBe('ukjent kode');
    expect(juristlinje(rolle)).toBe('Innvending (alvor: lav) → modellereren: aksepter.');
    expect(juristlinje({ ...rolle, juristdom: 'ikke_vurdert (Johanns avgjørelse: venter)' })).toContain('venter på modellvalg');
  });
});
