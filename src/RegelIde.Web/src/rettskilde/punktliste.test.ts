/**
 * Tester for punktliste-reglene (issue #213). Feilen som motiverte dem: åpner man
 * merverdiavgiftsforskriften § 1-3-1 ledd-1 sto det «(1) Med personkjøretøy menes» og så ingenting —
 * punkt 1–8 lå i basen, men ble aldri rendret. Den verre varianten (§ 1-3-2 ledd-2) har lista MIDT i
 * setningen, og leddets egen tekst sier da noe ANNET enn loven.
 *
 * Tekstene under er ekte, hentet fra `GET /api/rettskilder/df5e47ef-.../noder` 2026-09-09 — ikke
 * konstruerte eksempler, siden det nettopp var virkelighetens sammenskjøtinger regelen må tåle.
 */
import { describe, expect, it } from 'vitest';
import { PUNKTMERKE_FORKLARING, harTekstEtterListen, underordnedePunkter, underordnetInnhold, type PunktNode } from './punktliste';

const BASE = 'https://lovdata.no/eli/forskrift/2009/12/15/1540/nor';

function node(p: Partial<PunktNode>): PunktNode {
  return {
    eid: p.eid ?? `${BASE}/§1-3-1`,
    nodeType: p.nodeType ?? 'ledd',
    nummer: p.nummer ?? null,
    tekst: p.tekst ?? null,
    barn: p.barn ?? [],
  };
}

/** § 1-3-1 ledd-1 — innledning + åtte punkter, lista står PÅ SLUTTEN av leddet. */
const ledd1 = node({
  eid: `${BASE}/§1-3-1/ledd-1`,
  nummer: '1',
  tekst: '(1) Med personkjøretøy menes',
  barn: [
    node({ eid: `${BASE}/§1-3-1/ledd-1/punkt-1`, nodeType: 'punkt', nummer: '1', tekst: 'motorvogn registrert som personbil' }),
    node({ eid: `${BASE}/§1-3-1/ledd-1/punkt-2`, nodeType: 'punkt', nummer: '2', tekst: 'motorvogn registrert som varebil klasse 1' }),
    node({ eid: `${BASE}/§1-3-1/ledd-1/punkt-8`, nodeType: 'punkt', nummer: '8', tekst: 'kjøretøy som ikke benytter motor til framdrift og som hovedsakelig er innrettet for persontransport.' }),
  ],
});

describe('underordnedePunkter', () => {
  it('tar med punktbarna i den rekkefølgen de kommer, med sitt eget nummer og sin egen eId', () => {
    const punkter = underordnedePunkter(ledd1);
    expect(punkter.map((p) => p.merke)).toEqual(['1', '2', '8']);
    expect(punkter[0].eid).toBe(`${BASE}/§1-3-1/ledd-1/punkt-1`);
    expect(punkter[0].tekst).toBe('motorvogn registrert som personbil');
  });

  it('er tom for et ledd uten punktbarn — ingen tom liste vises da', () => {
    expect(underordnedePunkter(node({ tekst: '(4) Med registrering menes i denne paragrafen …' }))).toEqual([]);
  });

  it('tar bare punkt-noder, ikke andre nodetyper under samme forelder', () => {
    const paragraf = node({
      nodeType: 'paragraf',
      barn: [
        node({ eid: 'a', nodeType: 'ledd', nummer: '1', tekst: 'et ledd' }),
        node({ eid: 'b', nodeType: 'punkt', nummer: '1', tekst: 'et punkt' }),
      ],
    });
    expect(underordnedePunkter(paragraf).map((p) => p.eid)).toEqual(['b']);
  });

  it('tar punkt-i-punkt med, rekursivt (alkoholforskriften § 6-2 / § 14-3 punkt 14 er ekte tilfeller)', () => {
    const ledd = node({
      barn: [
        node({
          eid: 'p1', nodeType: 'punkt', nummer: '1', tekst: 'gebyrsatser',
          barn: [node({ eid: 'p1-1', nodeType: 'punkt', nummer: '1', tekst: 'sats A' })],
        }),
      ],
    });
    const punkter = underordnedePunkter(ledd);
    expect(punkter).toHaveLength(1);
    expect(punkter[0].punkter.map((p) => p.eid)).toEqual(['p1-1']);
  });

  it('hopper over et punkt uten egen tekst — et nummerert, tomt listeelement ville påstått noe som ikke står der', () => {
    const ledd = node({
      barn: [
        node({ eid: 'tom', nodeType: 'punkt', nummer: '1', tekst: null }),
        node({ eid: 'blank', nodeType: 'punkt', nummer: '2', tekst: '   ' }),
        node({ eid: 'ekte', nodeType: 'punkt', nummer: '3', tekst: 'campingbil' }),
      ],
    });
    expect(underordnedePunkter(ledd).map((p) => p.eid)).toEqual(['ekte']);
  });

  it('faller tilbake til eId-ens siste segment når punktnoden mangler nummer — aldri et oppfunnet nummer', () => {
    const ledd = node({ barn: [node({ eid: `${BASE}/§1/ledd-1/punkt-4`, nodeType: 'punkt', nummer: null, tekst: 'noe' })] });
    expect(underordnedePunkter(ledd)[0].merke).toBe('punkt-4');
  });
});

describe('harTekstEtterListen', () => {
  it('er false for innledningen til § 1-3-1 ledd-1 — «… menes» er ingen avsluttet setning', () => {
    expect(harTekstEtterListen(ledd1.tekst, 8)).toBe(false);
  });

  it('er false når innledningen ender på kolon («Med fartøygrupper menes:»)', () => {
    expect(harTekstEtterListen('Med fartøygrupper menes:', 3)).toBe(false);
  });

  it('er true for § 1-3-2 ledd-2 — lista står midt i setningen, og teksten etter er skjøtet på', () => {
    const ledd2 =
      '(2) Ved vurderingen av om et fotografi skal anses som kunstnerisk skal det blant annet legges ' +
      'vekt på om fotografiet har interesse for en videre krets av personer, om det er ment for ' +
      'offentlig visning i gallerier e.l. og på om prisen reflekterer en kunstnerisk verdi. I tillegg ' +
      'må fotografiet være Bilder fra ordinær fotografvirksomhet anses ikke som kunstneriske fotografier.';
    expect(harTekstEtterListen(ledd2, 4)).toBe(true);
  });

  it('er true for de andre ekte sammenskjøtingene som ble målt 2026-09-09', () => {
    expect(harTekstEtterListen(
      'En formidler … dersom følgende vilkår er oppfylt Formidleren er likevel ansvarlig dersom ' +
      'formidleren visste eller burde ha visst at informasjonen var feilaktig.',
      2,
    )).toBe(true);
    expect(harTekstEtterListen(
      '(2) Tilbyder … Oversikten skal minst inneholde følgende opplysninger Returer av varer som ' +
      'nevnt i bokstav l, må dokumenteres.',
      13,
    )).toBe(true);
  });

  it('ser gjennom avsluttende sitattegn og parenteser — «persontransport.»» er fortsatt avsluttet', () => {
    expect(harTekstEtterListen('… og teksten etter lista slutter slik.»', 3)).toBe(true);
    expect(harTekstEtterListen('… og teksten etter lista slutter slik.)', 3)).toBe(true);
  });

  it('er false uten punkter — påstanden gjelder bare noder som FAKTISK har en liste under seg', () => {
    expect(harTekstEtterListen('En helt vanlig avsluttet setning.', 0)).toBe(false);
  });

  it('er false for tom/manglende tekst — ingen påstand om en tekst som ikke finnes', () => {
    expect(harTekstEtterListen(null, 4)).toBe(false);
    expect(harTekstEtterListen('', 4)).toBe(false);
    expect(harTekstEtterListen(undefined, 4)).toBe(false);
  });
});

describe('PUNKTMERKE_FORKLARING', () => {
  it('sier at merket er eId-ens punktnummer og ikke Lovdatas egen merking — hele poenget med å vise den', () => {
    expect(PUNKTMERKE_FORKLARING).toContain('punkt-1');
    expect(PUNKTMERKE_FORKLARING).toContain('«a.»');
  });
});

/**
 * [Ny, avslutningsnode-runden, 2026-10-09, issue #361] Teksten etter en punktliste er en egen
 * `avslutning`-node og skal vises ETTER punktene, i lovens rekkefølge. Tekstene er ekte (energiloven
 * § 10-2 annet ledd), forkortet.
 */
describe('underordnetInnhold', () => {
  const E = 'https://lovdata.no/eli/lov/1990/06/29/50/nor/§10-2/ledd-2';
  const ledd = node({
    eid: E, nummer: '2', tekst: 'Reguleringsmyndigheten kan … samt i',
    barn: [
      node({ eid: `${E}/punkt-1`, nodeType: 'punkt', nummer: '1', tekst: 'avtaleloven § 38 b' }),
      node({ eid: `${E}/punkt-2`, nodeType: 'punkt', nummer: '2', tekst: 'markedsføringsloven § 6 …' }),
      node({ eid: `${E}/avslutning`, nodeType: 'avslutning', tekst: 'Første punktum gjelder likevel bare når …' }),
    ],
  });

  it('gir punktene som én liste og avslutningen etter dem', () => {
    const blokker = underordnetInnhold(ledd);
    expect(blokker.map((b) => b.type)).toEqual(['punkter', 'avslutning']);
    expect(blokker[0].type === 'punkter' && blokker[0].punkter.map((p) => p.merke)).toEqual(['1', '2']);
    expect(blokker[1]).toEqual({ type: 'avslutning', eid: `${E}/avslutning`, tekst: 'Første punktum gjelder likevel bare når …' });
  });

  it('holder flere lister og avslutninger i rekkefølge (avslutning, avslutning-2)', () => {
    const blokker = underordnetInnhold(node({
      barn: [
        node({ eid: 'p1', nodeType: 'punkt', nummer: '1', tekst: 'a' }),
        node({ eid: 'a1', nodeType: 'avslutning', tekst: 'Mellom:' }),
        node({ eid: 'p2', nodeType: 'punkt', nummer: '2', tekst: 'b' }),
        node({ eid: 'a2', nodeType: 'avslutning', tekst: 'Slutt.' }),
      ],
    }));
    expect(blokker.map((b) => (b.type === 'punkter' ? b.punkter.map((p) => p.eid).join() : b.eid))).toEqual(['p1', 'a1', 'p2', 'a2']);
  });

  it('tar med avslutningen under et punkt i punktets eget innhold (alkoholforskriften § 14-3 punkt 14)', () => {
    const blokker = underordnetInnhold(node({
      barn: [node({
        eid: 'p14', nodeType: 'punkt', nummer: '14', tekst: '… herunder at følgende vilkår:',
        barn: [
          node({ eid: 'p14-1', nodeType: 'punkt', nummer: '1', tekst: 'første vilkår' }),
          node({ eid: 'p14-a', nodeType: 'avslutning', tekst: 'Nærmere krav … kan fastsettes av Helsedirektoratet.' }),
        ],
      })],
    }));
    const punkt14 = blokker[0].type === 'punkter' ? blokker[0].punkter[0] : undefined;
    expect(punkt14?.innhold.map((b) => b.type)).toEqual(['punkter', 'avslutning']);
    expect(punkt14?.punkter.map((p) => p.eid)).toEqual(['p14-1']);
  });

  it('hopper over en avslutning uten tekst, og ledd under en paragraf', () => {
    expect(underordnetInnhold(node({
      nodeType: 'paragraf',
      barn: [
        node({ eid: 'l', nodeType: 'ledd', nummer: '1', tekst: 'et ledd' }),
        node({ eid: 'tom', nodeType: 'avslutning', tekst: '  ' }),
      ],
    }))).toEqual([]);
  });

  it('underordnedePunkter er uendret: bare punktene, ikke avslutningen', () => {
    expect(underordnedePunkter(ledd).map((p) => p.eid)).toEqual([`${E}/punkt-1`, `${E}/punkt-2`]);
  });
});
