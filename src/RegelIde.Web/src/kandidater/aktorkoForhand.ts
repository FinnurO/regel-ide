import type {
  AktorkoDetalj, AktorkoForhandKort, AktorkoForhandsklassifisering, Aktorkotype, Forhandssikkerhet, Forhandstype,
} from '../api/types';
import { AKTORKOTYPE_TEKST, forslagNokkel, skrivemater, type GodkjenningUtkast } from './aktorkoGodkjenning';

/**
 * [Ny, issue #440, Johanns avgjørelse 2026-11-05] Forhåndsklassifiseringen i aktørkøen (docs/09 §53, docs/33 §5.16.2): tekstene, filtrene og
 * hvordan «Godta forslaget» fyller skjemaene. Rene funksjoner med vitest.
 *
 * <p><b>Ingen stille godkjenning.</b> «Godta forslaget» fyller bare ut et eksisterende skjema; et menneske ser det og trykker på skjemaets egen
 * knapp. Det som ikke kan fylles ut av forslaget (et navn som ikke står i teksten, hvor en klasse uten hjemmel gjelder), står tomt med en
 * merknad, og knappen er av til det er valgt.</p>
 */

/** Merket, låst tekst (#440). Samme stil som fasitmerket i docs/09 §46: `info`, hele teksten synlig. */
export const FORHAND_MERKE = 'Forhåndsklassifisert (KI-agent og juristrunde), ikke kontrollert av menneske';

export const FORHANDSTYPE_TEKST: Record<Forhandstype, string> = {
  aktor: 'Aktør',
  klasse: 'Klasse',
  rolle: 'Rolle',
  omrade: 'Område',
  omtale: 'Omtale av en node',
  ikke_aktor: 'Ikke aktør',
  venter: 'Venter på modellvalg',
};

export const SIKKERHET_TEKST: Record<Forhandssikkerhet, string> = { hoy: 'høy', middels: 'middels', lav: 'lav' };

/** Modellvalget navnene venter på (Norge og nivåene). */
export const MODELLVALG_SAK = '#463';

const RELASJON_TEKST: Record<string, string> = {
  rolle_for: 'rolle for',
  rolle_innehas_av: 'innehas av',
  medlem: 'medlem',
  medlemsregel: 'medlemsregel',
  underklasse_av: 'underklasse av',
  organ_i: 'organ i',
  organ_for: 'organ for',
  samme_som: 'samme som',
  omtale_av: 'omtale av',
  utgatt_navn: 'utgått navn for',
  kortform_for: 'kortform for',
  bestar_av: 'består av',
  har_ansvarsomrade: 'har ansvarsområde',
  personkrets_i: 'personkrets i',
  fores_av: 'føres av',
  fores_for: 'føres for',
  klageinstans_for: 'klageinstans for',
  forvaltes_av: 'forvaltes av',
  inngar_i: 'inngår i',
  eies_av: 'eies av',
  ledes_av: 'ledes av',
  opprettet_av: 'opprettet av',
  kan_bestemmes_av: 'kan bestemmes av',
  sekretariat: 'sekretariat for',
  ansvarlig: 'ansvarlig',
  underlagt: 'underlagt',
  administrativt_underlagt: 'administrativt underlagt',
  generisk_omtale: 'generisk omtale',
  venter: 'venter',
};

/** Relasjonskoden i klart språk. Ukjente koder vises med mellomrom i stedet for understrek, ikke gjettet om. */
export function relasjonstekst(kode: string): string {
  return RELASJON_TEKST[kode] ?? kode.replace(/_/g, ' ');
}

/** Juristens dom i runde 1, modellererens svar og replikken i runde 2, som én linje. */
export function juristlinje(f: Pick<AktorkoForhandsklassifisering, 'juristdom' | 'juristAlvor' | 'modellererSvar' | 'replikk'>): string {
  if (!f.juristdom) return 'Ikke vurdert av juristen.';
  if (f.juristdom.startsWith('ikke_vurdert')) return 'Ikke vurdert av juristen: navnet venter på modellvalg (Johanns avgjørelse 2026-11-05).';
  const dom = f.juristdom === 'holder' ? 'Holder' : f.juristdom === 'innvending' ? 'Innvending' : f.juristdom === 'kontroller' ? 'Kontroller' : f.juristdom;
  const deler = [dom + (f.juristAlvor ? ` (alvor: ${f.juristAlvor})` : '')];
  if (f.modellererSvar) deler.push(`modellereren: ${f.modellererSvar}`);
  if (f.replikk) deler.push('replikk i runde 2');
  return deler.join(' → ') + '.';
}

/** Teksten på knappen og hva den gjør, per handling. Null når det ikke er noen knapp (venter). */
export function handlingstekst(f: AktorkoForhandsklassifisering): string | null {
  switch (f.handling) {
    case 'godkjenn': return `Fyller ut «Godkjenn» som ${FORHANDSTYPE_TEKST[f.type].toLowerCase()}.`;
    case 'koble': return `Fyller ut «Koble til en aktør som finnes» med ${f.kobling?.virksomhetNavn ?? 'aktøren'}.`;
    case 'ikke_aktor': return 'Fyller ut «Ikke en aktør» med begrunnelsen.';
    case 'generisk_omtale': return 'Tørrkjører «Tagg alle som generisk omtale» for ordet. Lagringen er et eget klikk.';
    case 'ta_med': return `Viser hva som skjer når ordet tas med i «${f.taMedINavn ?? f.taMedINokkel}». Du bekrefter før noe lagres.`;
    case 'venter': return null;
  }
}

/** Begrunnelsen som følger med i det som lagres, så det står hvor forslaget kom fra. */
export function forhandBegrunnelse(f: AktorkoForhandsklassifisering): string {
  return `Forhåndsklassifisering (#440, ${f.kilde}; KI-agent og juristrunde): ${f.begrunnelse}`;
}

/**
 * Skrivemåten forslagets navn har i teksten: nøyaktig lik først, ellers den ene som er lik uten forskjell på store og små bokstaver. Null når
 * ingen (eller flere) passer: da velger mennesket. Navnet skrives aldri om (CLAUDE.md §8).
 */
export function skrivemateFor(navn: string | null, kandidater: string[]): string | null {
  if (!navn) return null;
  if (kandidater.includes(navn)) return navn;
  const like = kandidater.filter((s) => s.toLowerCase() === navn.toLowerCase());
  return like.length === 1 ? like[0] : null;
}

const GODKJENNBARE: Forhandstype[] = ['aktor', 'klasse', 'rolle', 'omrade'];

/**
 * «Godta forslaget» for godkjenningen: utkastet skjemaet fylles med, og merknadene om det som ikke kunne fylles ut. Hjemmelen er forslagets
 * når den er et av regelens forslag, ellers «hjemmel mangler» (godkjenningen kan ikke lagre en annen hjemmel; den står i «eget steg»). Hvor en
 * klasse uten hjemmel gjelder, er rettskilden hjemmelen står i når navnet står der; ellers tom. «Faglig vurdert» er fylt ut: et forslag fra
 * en KI-agent er ikke godkjent før et menneske sier det.
 */
export function godkjenningFraForslag(f: AktorkoForhandsklassifisering, detalj: AktorkoDetalj): { utkast: GodkjenningUtkast; merknader: string[] } {
  const merknader: string[] = [];
  const type = GODKJENNBARE.includes(f.typeforslag) ? (f.typeforslag as Aktorkotype) : null;
  const navn = skrivemateFor(f.nyNode, skrivemater(detalj));
  if (!navn) {
    merknader.push(`Navnet i forslaget, «${f.nyNode ?? detalj.navn}», står ikke i teksten. Velg skrivemåten selv, eller vent med navnet (se «Eget steg»).`);
  }
  const h = f.hjemmel;
  const forslag = h?.erRegelensForslag && h.rettskildeId
    ? detalj.hjemmelforslag.find((x) => forslagNokkel(x) === forslagNokkel({ rettskildeId: h.rettskildeId!, eid: h.eid })) ?? null
    : null;
  let gjelder: string | null = null;
  if (h && !forslag) {
    merknader.push(`Forslagets hjemmel (${h.rettskilde ?? 'rettskilden'} ${h.bestemmelse ?? h.eid}) er ikke et av regelens forslag. `
      + 'Godkjenningen lagrer «hjemmel mangler»; hjemmelen registreres i eget steg.');
  }
  if (type && type !== 'aktor' && !forslag) {
    if (h?.rettskildeId && detalj.perRettskilde.some((r) => r.rettskildeId === h.rettskildeId)) {
      gjelder = h.rettskildeId;
    } else {
      merknader.push(`Forslaget sier ikke hvor ${AKTORKOTYPE_TEKST[type].bestemt.toLowerCase()} gjelder. Velg rettskilden, eller hele korpuset.`);
    }
  }
  return {
    utkast: {
      type, navn, hjemmel: forslag ? forslagNokkel(forslag) : 'mangler', gjelder, godkjenning: 'faglig_vurdert', begrunnelse: forhandBegrunnelse(f),
    },
    merknader,
  };
}

/** «Godta forslaget» for koblingen: aktøren, skrivemåten (den ene, eller den vanligste) og grunnen når forslaget sier den. */
export function koblingFraForslag(f: AktorkoForhandsklassifisering, detalj: AktorkoDetalj): { aktorId: string; navn: string | null; grunn: string; merknader: string[] } {
  const merknader: string[] = [];
  const s = skrivemater(detalj);
  if (!f.kobling?.navneformgrunn) merknader.push('Forslaget sier ikke hvilken grunn navneformen har. Velg grunnen selv.');
  if (f.kobling && !f.kobling.finnes) merknader.push(`Aktøren (${f.kobling.virksomhetNavn}) finnes ikke i basen nå. Kontroller forslaget.`);
  return { aktorId: f.kobling?.finnes ? f.kobling.virksomhetId : '', navn: s[0] ?? null, grunn: f.kobling?.navneformgrunn ?? '', merknader };
}

// ------------------------------------------------------------------------------------------------ Filtrene i køen

export type Typefilter = 'alle' | Forhandstype | 'uten';
export type Sikkerhetsfilter = 'alle' | Forhandssikkerhet;

export function passerForhandsfilter(f: AktorkoForhandKort | null | undefined, type: Typefilter, sikkerhet: Sikkerhetsfilter): boolean {
  if (type === 'uten') return !f && sikkerhet === 'alle';
  if (type !== 'alle' && f?.type !== type) return false;
  if (sikkerhet !== 'alle' && f?.sikkerhet !== sikkerhet) return false;
  return true;
}

/** «Bare aktører med høy sikkerhet» er de to filtrene satt til aktør og høy. */
export const erBareSikreAktorer = (type: Typefilter, sikkerhet: Sikkerhetsfilter) => type === 'aktor' && sikkerhet === 'hoy';
