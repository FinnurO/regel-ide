import type {
  Godkjenning, Grunnlagstype, KildeUtenforKorpusDokumentasjon, KildeUtenforKorpusType, Kunnskapsstatus, Tolkningsargument,
  UtsagnsgrunnlagRequest,
} from '../api/types';

/**
 * [Ny, issue #385 AC8–AC10] Reglene for veiviserens to nye steg, som en ren modul (docs/09 §17: en regel viktig nok å teste bor uten
 * React). Speiler `Utsagnsgrunnlag.Valider` i RegelIde.Data — serveren er portvakten, dette er for at knappen er av og meldingen står
 * der FØR et kall feiler.
 *
 * - «Hva bygger dette på?»: hjemmel i korpus (bestemmelse), kilde utenfor korpus (beskrivelse, type, dokumentasjonsgrad og ÉN ELLER FLERE
 *   lenker) eller «hjemmel mangler» (et synlig hull).
 * - «Hvor sikkert er det?»: kunnskapsstatus (docs/profil/03 §5), godkjenning (faglig vurdert / godkjent) og begrunnelse.
 *
 * Ingenting forhåndsvelges (CLAUDE.md §8): tom streng / null betyr «ikke valgt».
 */

export interface GrunnlagUtkast {
  grunnlag: Grunnlagstype | null;
  hjemmelEid: string;
  kildeTekst: string;
  kildeType: KildeUtenforKorpusType | '';
  kildeDokumentasjon: KildeUtenforKorpusDokumentasjon | '';
  lenker: string[];
}

export interface SikkerhetUtkast {
  kunnskapsstatus: Kunnskapsstatus | '';
  godkjenning: Extract<Godkjenning, 'faglig_vurdert' | 'godkjent'> | '';
  begrunnelse: string;
  argumenter: Tolkningsargument[];
}

export const TOMT_GRUNNLAG: GrunnlagUtkast = { grunnlag: null, hjemmelEid: '', kildeTekst: '', kildeType: '', kildeDokumentasjon: '', lenker: [''] };
export const TOM_SIKKERHET: SikkerhetUtkast = { kunnskapsstatus: '', godkjenning: '', begrunnelse: '', argumenter: [] };

/** Kunnskapsstatusene veiviseren tilbyr. Avledet (krever premisser) og foreldet (settes av en revisjon) settes aldri her. */
export const VEIVISER_KUNNSKAPSSTATUSER: Kunnskapsstatus[] = [
  'tekstfunn', 'normalisering', 'fortolkning', 'eksternt_dokumentert', 'uavklart', 'omstridt',
];

/** Tolkningsargumentene (docs/profil/03 §6) — speilet av `Strukturkanter.Tolkningsargumenter`. */
export const TOLKNINGSARGUMENTER: { verdi: Tolkningsargument; tekst: string }[] = [
  { verdi: 'ordlyd', tekst: 'Ordlyd' },
  { verdi: 'kontekst_system', tekst: 'Kontekst og system' },
  { verdi: 'formal', tekst: 'Formål' },
  { verdi: 'forarbeider', tekst: 'Forarbeider' },
  { verdi: 'praksis', tekst: 'Praksis' },
  { verdi: 'reelle_hensyn', tekst: 'Reelle hensyn' },
  { verdi: 'antitetisk_analogisk', tekst: 'Antitetisk eller analogisk' },
];

export function erLenke(s: string): boolean {
  try {
    const u = new URL(s.trim());
    return u.protocol === 'http:' || u.protocol === 'https:';
  } catch {
    return false;
  }
}

export function rensedeLenker(g: GrunnlagUtkast): string[] {
  return [...new Set(g.lenker.map((l) => l.trim()).filter((l) => l !== ''))];
}

/** Null når steget «Hva bygger dette på?» er fullstendig; ellers hva som mangler. */
export function grunnlagFeil(g: GrunnlagUtkast): string | null {
  switch (g.grunnlag) {
    case null:
      return 'Velg hva det bygger på.';
    case 'hjemmel':
      return g.hjemmelEid ? null : 'Velg bestemmelsen.';
    case 'kilde': {
      if (!g.kildeTekst.trim()) return 'Beskriv kilden.';
      if (!g.kildeType || !g.kildeDokumentasjon) return 'Velg kildetype og dokumentasjon.';
      const lenker = rensedeLenker(g);
      if (lenker.length === 0) return 'Oppgi minst én lenke til dokumentasjonen.';
      const ugyldig = lenker.find((l) => !erLenke(l));
      return ugyldig ? `«${ugyldig}» er ikke en gyldig lenke (http eller https).` : null;
    }
    case 'mangler':
      return null;
  }
}

/** Null når «Hvor sikkert er det?» er fullstendig og passer grunnlaget; ellers hva som mangler eller ikke henger sammen. */
export function sikkerhetFeil(g: GrunnlagUtkast, s: SikkerhetUtkast): string | null {
  if (!s.godkjenning) return 'Velg godkjenning: faglig vurdert eller godkjent.';
  if (g.grunnlag === 'mangler') {
    if (s.kunnskapsstatus !== 'uavklart') return '«Hjemmel mangler» lagres som uavklart.';
    if (!s.begrunnelse.trim()) return 'Skriv hva som mangler i begrunnelsen.';
  }
  switch (s.kunnskapsstatus) {
    case 'tekstfunn':
    case 'normalisering':
      return g.grunnlag === 'hjemmel' ? null : `${s.kunnskapsstatus === 'tekstfunn' ? 'Tekstfunn' : 'Normalisering'} krever en bestemmelse i korpus.`;
    case 'eksternt_dokumentert':
      return g.grunnlag === 'kilde' ? null : 'Eksternt dokumentert krever kilde utenfor korpus.';
    case 'fortolkning':
      if (!s.begrunnelse.trim()) return 'Fortolkning krever en begrunnelse.';
      return s.argumenter.length > 0 ? null : 'Fortolkning krever minst ett tolkningsargument.';
    default:
      return null;
  }
}

/** Forespørselen til API-et. Hjemmelen er i kandidatens rettskilde. Kalleren har sjekket at begge stegene er fullstendige. */
export function tilRequest(g: GrunnlagUtkast, s: SikkerhetUtkast, rettskildeId: string): UtsagnsgrunnlagRequest {
  return {
    grunnlag: g.grunnlag!,
    hjemmelRettskildeId: g.grunnlag === 'hjemmel' ? rettskildeId : null,
    hjemmelEid: g.grunnlag === 'hjemmel' ? g.hjemmelEid : null,
    kildeTekst: g.grunnlag === 'kilde' ? g.kildeTekst.trim() : null,
    kildeType: g.grunnlag === 'kilde' && g.kildeType ? g.kildeType : null,
    kildeDokumentasjon: g.grunnlag === 'kilde' && g.kildeDokumentasjon ? g.kildeDokumentasjon : null,
    kildeLenker: g.grunnlag === 'kilde' ? rensedeLenker(g) : null,
    kunnskapsstatus: s.kunnskapsstatus || null,
    godkjenning: s.godkjenning as Godkjenning,
    begrunnelse: s.begrunnelse.trim() || null,
    tolkningsargumenter: s.argumenter.length ? s.argumenter : null,
  };
}
