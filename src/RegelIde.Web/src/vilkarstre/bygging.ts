/**
 * Bygger et enkelt, rekursivt tre-objekt (VilkarstreNode) fra de flate DTO-listene API-et returnerer
 * (Vilkår/Regelnode/Unntak/RegelnodeBarn) — brukt av både VilkarstreGraf og VilkarstreTre, slik at
 * begge visningene alltid viser nøyaktig samme struktur.
 */
import type { RegelnodeBarnDto, RegelnodeDto, UnntakDto, VilkarDto } from '../api/types';

export interface VilkarstreUnntak {
  id: string;
  tittel: string;
  betingelse: VilkarstreNode;
}

export interface VilkarstreNode {
  id: string;
  kind: 'vilkar' | 'regelnode';
  tittel: string;
  erRotnode?: boolean;
  barnOperator?: string; // kun regelnode
  vilkarstype?: string; // kun vilkar
  vurderingstype?: string; // kun vilkar
  erFormel?: boolean; // kun vilkar
  children: VilkarstreNode[];
  unntak: VilkarstreUnntak[];
}

export function byggVilkarstre(
  rotnodeId: string,
  regelnoder: RegelnodeDto[],
  vilkar: VilkarDto[],
  unntakListe: UnntakDto[],
  barnPerRegelnode: Map<string, RegelnodeBarnDto[]>,
): VilkarstreNode | null {
  const regelnodeById = new Map(regelnoder.map((r) => [r.id, r]));
  const vilkarById = new Map(vilkar.map((v) => [v.id, v]));
  const unntakPerGjelderRegel = new Map<string, UnntakDto[]>();
  for (const u of unntakListe) {
    const liste = unntakPerGjelderRegel.get(u.gjelderRegelId) ?? [];
    liste.push(u);
    unntakPerGjelderRegel.set(u.gjelderRegelId, liste);
  }

  const besokt = new Set<string>();

  function byggFra(id: string, kind: 'vilkar' | 'regelnode'): VilkarstreNode | null {
    const noekkel = `${kind}:${id}`;
    if (besokt.has(noekkel)) return null; // vern mot uventet sykel i visning — bør aldri skje, DAG håndheves server-side
    besokt.add(noekkel);

    if (kind === 'vilkar') {
      const v = vilkarById.get(id);
      if (!v) return null;
      return {
        id: v.id, kind: 'vilkar', tittel: v.tittel, vilkarstype: v.vilkarstype,
        vurderingstype: v.vurderingstype, erFormel: v.erFormel, children: [], unntak: [],
      };
    }

    const r = regelnodeById.get(id);
    if (!r) return null;
    const barn = barnPerRegelnode.get(id) ?? [];
    const children = barn
      .map((b) => byggFra(b.barnId, b.barnType as 'vilkar' | 'regelnode'))
      .filter((n): n is VilkarstreNode => n !== null);
    const unntak = (unntakPerGjelderRegel.get(id) ?? [])
      .map((u) => {
        const betingelse = byggFra(u.betingelseId, u.betingelseType as 'vilkar' | 'regelnode');
        return betingelse ? { id: u.id, tittel: u.tittel, betingelse } : null;
      })
      .filter((u): u is VilkarstreUnntak => u !== null);

    return {
      id: r.id, kind: 'regelnode', tittel: r.tittel, erRotnode: r.erRotnode,
      barnOperator: r.barnOperator, children, unntak,
    };
  }

  return byggFra(rotnodeId, 'regelnode');
}

/** Flat liste av alle noder i treet (for enkel oppslag ved klikk), inkl. unntak-betingelser. */
export function flatNodeliste(node: VilkarstreNode): VilkarstreNode[] {
  const ut = [node];
  for (const c of node.children) ut.push(...flatNodeliste(c));
  for (const u of node.unntak) ut.push(...flatNodeliste(u.betingelse));
  return ut;
}
