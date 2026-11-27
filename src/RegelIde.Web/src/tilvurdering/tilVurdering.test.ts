import { describe, expect, it } from 'vitest';
import type { TilVurderingDto } from '../api/types';
import { START_HER_ANTALL, aktorLenke, byggTilVurdering, startHerDepartementer, tidPerElement, tidTotalt } from './tilVurdering';

/** [Ny, issue #517] Tallene fra kartleggingen 2026-11-27 (kopi av den delte basen). */
function dto(endre: (d: TilVurderingDto) => void = () => {}): TilVurderingDto {
  const aktorer = Array.from({ length: 25 }, (_, i) => ({
    virksomhetId: `id${i}`, navn: `Departement ${i}`, funn: 80 - i,
    navneformer: [{ begrepId: `b${i}`, term: `Gammelt navn ${i}`, navneformgrunn: 'utgatt', godkjenning: 'maskinelt_foreslatt' }],
  }));
  const d: TilVurderingDto = {
    hentet: '2026-11-27T10:00:00Z',
    departementer: { aktorer, funnMedNavneformmangel: 582, funnKumulativt: aktorer.map((_, i) => Math.min(581, 73 * (i + 1))) },
    aktorko: {
      antallNavn: 188, forekomster: 285, klare: 57, trengerSted: 5, relasjonerVenterPaNavn: 53, pilotNavn: 122,
      perHandling: { godkjenn: 62, generisk_omtale: 73, ikke_aktor: 39, venter: 5, koble: 7, ta_med: 2 },
    },
    etterfolgere: { forslag: 52, manuelle: 15, funnMedEtterfolgermangel: 312, funnManuelle: 204 },
    pilot: { forslag: 215, ki: 135, monster: 79, rettskilder: [] },
    hjemmelOpphevet: { oppdaget: 404, holdesAvNyHjemmel: 176, kunngjortVersjon: 46, andreFunnOppdaget: 3 },
    utgattAktor: { oppdaget: 592 },
    forekomster: [{ term: 'regjeringen', forslag: 254, slutninger: 156 }, { term: 'statsråd', forslag: 278, slutninger: 24 }],
    klasseforslag: 8, navneformforslag: 322, ressortkanter: 1125,
    kildefeil: { ny: 187, perType: { opphevet_av_annen_kilde: 48, hjemmel_node_mangler: 139 } },
    forslagskoTotalt: 1902,
    eldre: { begrepskandidater: 6012, relaterteDefinisjoner: 2541, virksomhetskandidater: 229 },
  };
  endre(d);
  return d;
}

describe('rekkefølgen er fast (Johann 2026-11-27)', () => {
  it('de klare køene står i den begrunnede rekkefølgen, og departementene er «Start her»', () => {
    const v = byggTilVurdering(dto());
    expect(v.klare.map((k) => k.id)).toEqual(['departementer', 'aktorko-klare', 'etterfolgere', 'aktorko-resten', 'pilot', 'kunngjort']);
    expect(v.startHer).toBe('departementer');
  });

  it('«Start her» går videre til neste kø når departementene er tomme, og er null når alt er tomt', () => {
    expect(byggTilVurdering(dto((d) => { d.departementer.aktorer = []; d.departementer.funnKumulativt = []; })).startHer).toBe('aktorko-klare');
    const tom = byggTilVurdering(dto((d) => {
      d.departementer.aktorer = []; d.departementer.funnKumulativt = [];
      d.aktorko = { ...d.aktorko, antallNavn: 0, klare: 0, perHandling: {} };
      d.etterfolgere.forslag = 0; d.pilot.forslag = 0; d.hjemmelOpphevet.kunngjortVersjon = 0;
    }));
    expect(tom.startHer).toBeNull();
    expect(tom.klarTeller).toBe(0);
  });
});

describe('tallene (AK 1 og 3)', () => {
  it('telleren på menylenken er summen av de klare køene', () => {
    const v = byggTilVurdering(dto());
    // 25 aktører + 62 til godkjenning + 52 etterfølgere + 121 andre (73 + 39 + 7 + 2) + 215 pilot + 46 kunngjort. De 5 som venter på
    // modellvalg er ikke med.
    expect(v.klare.map((k) => k.venter)).toEqual([25, 62, 52, 121, 215, 46]);
    expect(v.klarTeller).toBe(521);
  });

  it('det som låses opp, har tallet fra API-et', () => {
    const v = byggTilVurdering(dto());
    const rad = (id: string) => v.klare.find((k) => k.id === id)!;
    expect(rad('departementer').laaserOpp).toContain('581 funn');
    expect(rad('etterfolgere').laaserOpp).toContain('312 funn');
    expect(rad('etterfolgere').krever.join(' ')).toContain('holder igjen 204 funn');
    expect(rad('aktorko-klare').laaserOpp).toContain('53');
    expect(rad('aktorko-resten').blokkerer?.tekst).toBe('5 venter på modellvalg (#463)');
  });

  it('ingen blokkering når ingen venter på modellvalg', () => {
    const v = byggTilVurdering(dto((d) => { d.aktorko.perHandling = { godkjenn: 62 }; }));
    expect(v.klare.find((k) => k.id === 'aktorko-resten')!.blokkerer).toBeNull();
  });
});

describe('lenkene har filteret køen viser tallet med (AK 6)', () => {
  it('aktørkøen, forslagskøen og funnene', () => {
    const v = byggTilVurdering(dto((d) => { d.pilot.rettskilder = ['r1', 'r2']; }));
    const href = (id: string) => v.klare.find((k) => k.id === id)!.href;
    expect(href('aktorko-klare')).toBe('/aktorko?handling=godkjenn');
    expect(href('aktorko-resten')).toBe('/aktorko?handling=andre');
    expect(href('etterfolgere')).toBe('/ki-oppdagelse?type=etterfolger');
    expect(href('pilot')).toBe('/ki-oppdagelse?hjemmel=r1,r2');
    expect(href('kunngjort')).toBe('/funn?kategori=hjemmel_opphevet&status=oppdaget&mangel=kunngjort');
    expect(v.blokkerte.find((k) => k.id === 'hjemmel-opphevet')!.href).toBe('/funn?kategori=hjemmel_opphevet&status=oppdaget');
    expect(aktorLenke('x')).toBe('/virksomheter/x?fane=navneformer');
  });
});

describe('blokkerte køer (AK 4)', () => {
  it('køer uten skjerm lenker til saken, ikke til en side uten knapp', () => {
    const v = byggTilVurdering(dto());
    for (const id of ['utgatt-aktor', 'forekomster', 'klasseforslag', 'navneformforslag', 'ressortkanter', 'kildefeil']) {
      const rad = v.blokkerte.find((k) => k.id === id)!;
      expect(rad.ekstern).toBe(true);
      expect(rad.href).toMatch(/^https:\/\/github\.com\/FinnurO\/regel-ide\/issues\/\d+$/);
      expect(rad.blokkerer?.farge).toBe('warning');
    }
    expect(v.blokkerte.find((k) => k.id === 'forekomster')!.laaserOpp).toContain('«regjeringen» 156, «statsråd» 24');
  });

  it('det som ikke finnes i appen, vises uten tall', () => {
    expect(byggTilVurdering(dto()).utenforAppen.every((r) => r.venter === null)).toBe(true);
  });
});

describe('Start her', () => {
  it('de sju første, med antall ulike funn fra den kumulative lista', () => {
    const s = startHerDepartementer(dto());
    expect(s.aktorer).toHaveLength(START_HER_ANTALL);
    expect(s.funn).toBe(511);
    expect(s.iAlt).toBe(592);
  });

  it('færre enn sju aktører gir dem som finnes', () => {
    const s = startHerDepartementer(dto((d) => { d.departementer.aktorer = d.departementer.aktorer.slice(0, 2); }));
    expect(s.aktorer).toHaveLength(2);
    expect(s.funn).toBe(146);
  });
});

describe('tid er et anslag (AK 5)', () => {
  it('per element', () => {
    expect(tidPerElement([1, 1])).toBe('ca. 1 min');
    expect(tidPerElement([0.5, 1])).toBe('0,5–1 min');
    expect(tidPerElement([2, 3])).toBe('2–3 min');
  });

  it('i alt', () => {
    expect(tidTotalt(7, [2, 3])).toBe('ca. 20 min');
    expect(tidTotalt(57, [1, 1])).toBe('ca. 55 min');
    expect(tidTotalt(126, [0.5, 1])).toBe('1–2 t');
    expect(tidTotalt(52, [1, 2])).toBe('1–1,5 t');
    expect(tidTotalt(215, [1, 2])).toBe('3,5–7 t');
    expect(tidTotalt(0, [1, 2])).toBe('—');
  });
});
