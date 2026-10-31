/**
 * [Ny, issue #429] Rene funksjoner for funn-teksten, delt av appen og speilet i nettside/funn/index.html. Holdt utenfor .tsx-filene
 * så de kan testes uten DOM.
 */

export interface Tekstblokk {
  type: 'avsnitt' | 'liste';
  linjer: string[];
}

/** Linjer som begynner med «- » er punkter; påfølgende punkter blir én liste. Tomme linjer skiller ingenting. */
export function delITekstblokker(tekst: string): Tekstblokk[] {
  const blokker: Tekstblokk[] = [];
  for (const linje of tekst.split('\n')) {
    if (!linje.trim()) continue;
    const erPunkt = linje.startsWith('- ');
    const siste = blokker[blokker.length - 1];
    if (erPunkt && siste?.type === 'liste') siste.linjer.push(linje.slice(2));
    else blokker.push({ type: erPunkt ? 'liste' : 'avsnitt', linjer: [erPunkt ? linje.slice(2) : linje] });
  }
  return blokker;
}

/** Lenke til Lovdata for en ELI/eId, samme omforming som nettsiden (nettside/assets/js/common.js, lovdataLenke). Ukjent format: uendret. */
export function lovdataLenke(eidEllerEli: string | null): string | null {
  if (!eidEllerEli) return null;
  const m = /^https:\/\/lovdata\.no\/eli\/(lov|forskrift)\/(\d{4})\/(\d{2})\/(\d{2})(?:\/(\d+))?\/nor(\/§[^/]+)?/.exec(eidEllerEli);
  if (!m) return eidEllerEli;
  const [, type, aar, mnd, dag, lopenr, paragraf] = m;
  return `https://lovdata.no/dokument/${type === 'lov' ? 'NL' : 'SF'}/${type}/${aar}-${mnd}-${dag}${lopenr ? `-${lopenr}` : ''}${paragraf ?? ''}`;
}
