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

/** [Ny, #429 PR 2] Overskriften på vurderingspunktet der juristen sier hvem som har oppgaven nå (Funn.EtterfolgerOverskrift i API-et). */
export const ETTERFOLGER_OVERSKRIFT = 'Etterfølger';

/** [Ny, #429 PR 3, R9] Overskriften på vurderingspunktet der juristen sier om vedtaket fortsatt har virkning når hjemmelsloven er opphevet
 * (Funn.HjemmelOverskrift i API-et). */
export const HJEMMEL_OVERSKRIFT = 'Hjemmel';

/** [Ny, #429 PR 3, R9] Premissmanglene fra R9 begynner alltid slik (Opphevingskontroll.Premissmangeltekst i API-et). */
export const HJEMMEL_MANGEL_START = 'Hjemmelsloven kan være opphevet';

/** [Ny, #472 PR 2] Overskriften på vurderingspunktet der juristen sier om forskriften kan stå på en ny hjemmel (Funn.NyHjemmelOverskrift). */
export const NY_HJEMMEL_OVERSKRIFT = 'Ny hjemmel';

/** [Ny, #472 PR 2] Premissmanglene for «Ny hjemmel» og #446 begynner alltid slik (FunnTjeneste.NyHjemmelMangelStart og UttomtVirkningMangelStart). */
export const NY_HJEMMEL_MANGEL_START = 'Ny hjemmel ikke vurdert';
export const UTTOMT_VIRKNING_MANGEL_START = 'Uttømt virkning ikke klassifisert';

/** [Ny, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1] Premissmangelen når korpuset bare har forskriften som kunngjort versjon
 * (FunnTjeneste.KunngjortMangelStart), og vurderingspunktet som fjerner den (Funn.GjeldendeStatusOverskrift). */
export const KUNNGJORT_MANGEL_START = 'Kunngjort versjon, gjeldende status ikke kontrollert';
export const GJELDENDE_STATUS_OVERSKRIFT = 'Gjeldende status';

/** [Ny, #429 PR 3] Deler premissmanglene i R9 (hjemmelsloven) og resten (navneform og etterfølger), så merkene kan vises hver for seg.
 * [ENDRET, #472 PR 2] Også «Ny hjemmel» og #446, som vises på et funn som bare er oppdaget, slik som R9. */
export function delPremissmangler(mangler: string[]): {
  hjemmel: string[]; nyHjemmel: string[]; uttomt: string[]; kunngjort: string[]; andre: string[];
} {
  // [ENDRET, #472 PR 4] Også «Kunngjort versjon», som vises på et funn som bare er oppdaget, slik som #446.
  const egne = [HJEMMEL_MANGEL_START, NY_HJEMMEL_MANGEL_START, UTTOMT_VIRKNING_MANGEL_START, KUNNGJORT_MANGEL_START];
  return {
    hjemmel: mangler.filter((m) => m.startsWith(HJEMMEL_MANGEL_START)),
    nyHjemmel: mangler.filter((m) => m.startsWith(NY_HJEMMEL_MANGEL_START)),
    uttomt: mangler.filter((m) => m.startsWith(UTTOMT_VIRKNING_MANGEL_START)),
    kunngjort: mangler.filter((m) => m.startsWith(KUNNGJORT_MANGEL_START)),
    andre: mangler.filter((m) => !egne.some((e) => m.startsWith(e))),
  };
}

/**
 * [Ny, #472 PR 2] Kategorimerket i klartekst: «Kategori: undertype». Gjenstandsløs står alene, fordi det er gjenstanden og ikke hjemmelen som er
 * opphevet (Johanns avgjørelse 2026-11-18 (5): eget merke).
 */
export function kategoriMerketekst(kategoritekst: string, undertype: string | null, undertypetekst: string | null): string {
  if (undertype === 'gjenstandslos' && undertypetekst) return undertypetekst;
  return undertypetekst ? `${kategoritekst}: ${undertypetekst.toLowerCase()}` : kategoritekst;
}

/**
 * [Ny, #429 PR 2, Johanns avgjørelse 2026-10-31 (3)] Etterfølgerlinjen for et funn om en utgått aktør, speilet i nettside/funn/index.html.
 * Etterfølgerkantene når funnet peker på noen; ellers juristens vurderingspunkt «Etterfølger»; ellers «ukjent». `ikkeVurdert` (premissporten
 * stopper på etterfølgeren) gir «ikke vurdert». Vurderingspunktet vises bare her, ikke også i lista over vurderingspunkter
 * (`utenEtterfolgerpunkt`).
 */
export function etterfolgerlinje(
  etterfolgere: { etikett: string }[], vurderingspunkter: { overskrift: string | null; tekst: string }[], ikkeVurdert = false,
): string {
  if (ikkeVurdert) return 'ikke vurdert';
  if (etterfolgere.length > 0) return etterfolgere.map((e) => e.etikett).join(', ');
  const punkt = vurderingspunkter.find((p) => p.overskrift?.trim().toLowerCase() === ETTERFOLGER_OVERSKRIFT.toLowerCase());
  return punkt ? punkt.tekst : 'ukjent';
}

/** Vurderingspunktene uten punktet «Etterfølger» når det er brukt som etterfølgerlinje (ingen etterfølgerkant). */
export function utenEtterfolgerpunkt<T extends { overskrift: string | null }>(punkter: T[], etterfolgere: unknown[]): T[] {
  if (etterfolgere.length > 0) return punkter;
  return punkter.filter((p) => p.overskrift?.trim().toLowerCase() !== ETTERFOLGER_OVERSKRIFT.toLowerCase());
}

/** [Ny, issue #517 PR 3] Filteret på premissmangel fra lenkene på «Til vurdering» (`/funn?mangel=kunngjort`). Nøkkelen i lenken peker på
 *  starten av mangelteksten API-et skriver; en ukjent nøkkel gir ingen filter. */
export const MANGELFILTER: Record<string, { start: string; tekst: string }> = {
  kunngjort: { start: KUNNGJORT_MANGEL_START, tekst: 'Kunngjort versjon, status ikke kontrollert' },
};

export function passerMangelfilter(mangler: string[] | null | undefined, filter: string | null): boolean {
  if (!filter || !MANGELFILTER[filter]) return true;
  return (mangler ?? []).some((m) => m.startsWith(MANGELFILTER[filter].start));
}
