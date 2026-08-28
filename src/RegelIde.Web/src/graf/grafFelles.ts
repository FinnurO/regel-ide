/**
 * [Ny, 2026-08-28] Delt mellom `Tjenestereise.tsx` (ekte, persisterte data fra
 * `GET /api/tjenester/{id}/avhengighetsgraf`) og `ImportWizard.tsx` sin in-memory forhåndsvisning
 * (rå, ikke-persistert modelleksport-JSON) — begge tegner "samme slags graf", bare med ulik
 * datakilde og id-rom (ekte GUID-er vs. synthetic navn-baserte id-er før noe er opprettet).
 */

export interface GrafNodeLik {
  id: string;
  navn: string;
  erHandling: boolean;
  type: string | null;
  kompetentMyndighet: string | null;
  livshendelser: string[];
  status: string | null;
}

export interface GrafKantLik {
  fraId: string;
  tilId: string;
  rel: string;
  erHandlingTilhorighet: boolean;
}

export interface FeltvisningValg {
  type: boolean;
  kompetentMyndighet: boolean;
  livshendelser: boolean;
  status: boolean;
}

export const FELTVISNING_DEFAULT: FeltvisningValg = {
  type: false, kompetentMyndighet: true, livshendelser: false, status: false,
};

/** De 8 EKTE `GyldigeRel`-verdiene (`TjenesteavhengighetregisterTjeneste.GyldigeRel` på serveren) —
 * IKKE bildets "Krever (alltid)/Kan kreve (betinget)"-legende (en annen, ikke-eksisterende taksonomi
 * — bildet er stilinspirasjon, ikke en spesifikasjon å kopiere). Farger valgt for å skille
 * relasjonene visuelt i grafen, ikke en betydningsbærende fargekonvensjon fra før. */
export const REL_FARGE: Record<string, string> = {
  forutsetning_for: '#0f6b3f',
  gir_mulighet_til: '#0f5b8f',
  utlost_av: '#8f5b0f',
  for: '#5b5b5b',
  avhengig_av: '#7a4fb0',
  input_til: '#1f7a7a',
  har_del: '#b04f8f',
  kan_miste: '#b03030',
};
export const REL_LABEL: Record<string, string> = {
  forutsetning_for: 'er forutsetning for',
  gir_mulighet_til: 'gir mulighet til',
  utlost_av: 'utløses av',
  for: 'kommer før',
  avhengig_av: 'er avhengig av',
  input_til: 'er input til',
  har_del: 'har del',
  kan_miste: 'kan miste',
};

export function nodeLabel(n: GrafNodeLik, felt: FeltvisningValg): string {
  const linjer = [n.navn];
  if (felt.type && n.type) linjer.push(`Type: ${n.type}`);
  if (felt.kompetentMyndighet && n.kompetentMyndighet) linjer.push(n.kompetentMyndighet);
  if (felt.livshendelser && n.livshendelser.length > 0) linjer.push(n.livshendelser.join(', '));
  if (felt.status && n.status) linjer.push(`Status: ${n.status}`);
  return linjer.join('\n');
}

/** Fallback-høyde for et kall uten `hoydePerNode` (f.eks. et fremtidig kall utenfor
 * `TjenesteGrafCanvas`) — matcher datastørrelsen `28 + 2*18` (navn + ett ekstra felt) der bruker der. */
const STANDARD_NODE_HOYDE = 64;
const RAD_MELLOMROM = 20;

/**
 * Generalisert lagdelt layout — BFS-dybde fra en (valgfri) foretrukket rot for x, indeks innad i
 * laget for y. Håndterer FLERE usammenhengende komponenter (vanlig i en stor, ikke-persistert
 * import-batch — ikke alt henger nødvendigvis sammen) ved å stable dem under hverandre; hver
 * komponent legges ut fra sin egen første-besøkte node. `forsteRotId` (typisk et graf-sentrum)
 * behandles først hvis oppgitt, slik at DEN komponenten alltid havner øverst. Egne handling-noder
 * plasseres rett under sin eiende tjeneste (ikke egen dybde/lag). Fritt drabart av React Flow selv
 * etterpå — ingen layoutbibliotek (dagre/elkjs).
 *
 * [Endret, 2026-08-29] `hoydePerNode` — oppdaget via kodegjennomgang: rad-avstanden innad i et lag var
 * tidligere en FAST `i * 100`, uavhengig av at nodehøyden selv er variabel (`28 + antallLinjer * 18`
 * i `TjenesteGrafCanvas.tsx`, styrt av `felt`-visningsvalgene) — med alle fire "Vis på hver
 * node"-valgene på og godt utfylt innhold ble noder opptil 118px høye, som overlappet den faste
 * 100px-avstanden. Rad-Y akkumuleres nå fra FAKTISK nodehøyde + `RAD_MELLOMROM` i stedet.
 */
export function beregnLagdeltLayout(
  noder: GrafNodeLik[], kanter: GrafKantLik[], forsteRotId?: string, hoydePerNode?: Map<string, number>,
): Map<string, { x: number; y: number }> {
  const hoydeFor = (id: string) => hoydePerNode?.get(id) ?? STANDARD_NODE_HOYDE;
  const naboer = new Map<string, string[]>();
  const alleTjenesteIder = noder.filter((n) => !n.erHandling).map((n) => n.id);
  alleTjenesteIder.forEach((id) => naboer.set(id, []));
  for (const k of kanter) {
    if (k.erHandlingTilhorighet) continue;
    if (!naboer.has(k.fraId) || !naboer.has(k.tilId)) continue;
    naboer.get(k.fraId)!.push(k.tilId);
    naboer.get(k.tilId)!.push(k.fraId);
  }

  const posisjon = new Map<string, { x: number; y: number }>();
  const besokt = new Set<string>();
  let yOffset = 0;
  const rotrekkefolge = forsteRotId && alleTjenesteIder.includes(forsteRotId)
    ? [forsteRotId, ...alleTjenesteIder.filter((id) => id !== forsteRotId)]
    : alleTjenesteIder;

  for (const start of rotrekkefolge) {
    if (besokt.has(start)) continue;
    const dybde = new Map<string, number>([[start, 0]]);
    const ko = [start];
    besokt.add(start);
    while (ko.length > 0) {
      const id = ko.shift()!;
      for (const nabo of naboer.get(id) ?? []) {
        if (!besokt.has(nabo)) {
          besokt.add(nabo);
          dybde.set(nabo, (dybde.get(id) ?? 0) + 1);
          ko.push(nabo);
        }
      }
    }
    const perLag = new Map<number, string[]>();
    for (const [id, d] of dybde) {
      if (!perLag.has(d)) perLag.set(d, []);
      perLag.get(d)!.push(id);
    }
    // Lagene i én komponent legges ut PARALLELT (samme startpunkt `yOffset`, ulik x per dybde `d`) —
    // neste komponent kan derfor først starte under den HØYESTE av dem, ikke bare det laget med flest
    // noder (et fåtall svært høye noder kan trenge mer plass enn mange lave).
    let hoyesteSluttY = yOffset;
    for (const [d, ider] of perLag) {
      let y = yOffset;
      ider.forEach((id) => {
        posisjon.set(id, { x: d * 260, y });
        y += hoydeFor(id) + RAD_MELLOMROM;
      });
      hoyesteSluttY = Math.max(hoyesteSluttY, y);
    }
    yOffset = hoyesteSluttY + RAD_MELLOMROM;
  }

  const handlingTeller = new Map<string, number>();
  kanter.filter((k) => k.erHandlingTilhorighet).forEach((k) => {
    const basis = posisjon.get(k.fraId) ?? { x: 0, y: 0 };
    const i = handlingTeller.get(k.fraId) ?? 0;
    posisjon.set(k.tilId, { x: basis.x, y: basis.y + 90 + i * 60 });
    handlingTeller.set(k.fraId, i + 1);
  });

  return posisjon;
}
