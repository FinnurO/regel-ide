/**
 * [Ny, 2026-08-28, import-wizard-runden] TypeScript-speil av RAA modelleksport-JSON-en
 * (`RettighetModellEksportTjeneste`/`TjenesteModellSkjema` på serveren, docs/23-tjeneste-modell-
 * eksport-og-skjema.md) — snake_case, EKSAKT samme feltnavn som eksporten/skjemaet. Brukt KUN til å
 * parse en brukerlastet fil client-side i `ImportWizard.tsx` — ALDRI sendt til serveren i denne
 * formen (serveren ser kun allerede-resolvede `ImportRettighetRequest`, se `types.ts`).
 */

export interface RaaHjemmel {
  lov: string | null;
  henvisning: string | null;
}

export interface RaaEgetInnholdselement {
  id: string;
  tittel: string;
  tekst: string | null;
}

export interface RaaRegelverksreferanse {
  lov: string | null;
  henvisning: string | null;
  felt: string | null;
}

export interface RaaKanal {
  kanal: string;
  adresse?: string | null;
}

export interface RaaVedlegg {
  navn: string | null;
  kategori: string | null;
  hjemmel: RaaHjemmel | null;
}

export interface RaaVeiledningstekst {
  overskrift: string | null;
  innhold: string | null;
  hjemmel: RaaHjemmel | null;
}

export interface RaaArsak {
  arsak: string | null;
  hjemmel: RaaHjemmel | null;
}

export interface RaaBevisKanal {
  kanal: string | null;
}

export interface RaaResultat {
  hva: string | null;
  bevis_kanaler: RaaBevisKanal[];
}

export interface RaaBehandlingstid {
  frist: string | null;
  hjemmel: RaaHjemmel | null;
}

export interface RaaKostnad {
  belop: string | null;
  hjemmel: RaaHjemmel[];
}

export interface RaaHandling {
  navn: string;
  handlingstype: string | null;
  bruksomraade: string | null;
  utfort_av: string | null;
  merknad: string | null;
  eies_av_denne_tjenesten?: boolean;
  kanaler?: RaaKanal[];
  behandlingstid?: RaaBehandlingstid | null;
  kostnad?: RaaKostnad | null;
  vedlegg?: RaaVedlegg[];
  veiledningstekst?: RaaVeiledningstekst[];
  arsaker?: RaaArsak[];
  resultat?: RaaResultat | null;
}

export interface RaaAvhengighet {
  rel: string;
  retning: 'fra' | 'til';
  mal_type: 'tjeneste' | 'ekstern_referanse';
  mal_navn: string;
  mal_id?: string | null;
  organisasjonsnummer?: string | null;
  kildeurl?: string | null;
  merknad?: string | null;
}

export interface RaaInnsender {
  hvem_kan_sende: string[];
  innlogging: string | null;
}

export interface RaaInnsending {
  kanal: string | null;
  etter_mottak: string[];
  merknad?: string | null;
}

export interface RaaKontakt {
  generelt: string | null;
  kommunen_kan_veilede_om: string[];
}

export interface RaaEndringer {
  plikt: string | null;
  eksempler: string[];
}

export interface RaaHvaRettighetenInnebarer {
  innledning: string | null;
  varighet: string | null;
  plikter?: string[];
  kontroll_og_tilsyn?: string | null;
  konsekvenser_ved_brudd_pa_regelverket?: string | null;
  avgrensning_merknad?: string | null;
  krav_til_drift?: string | null;
  tommeavtale_og_kontroll?: string | null;
  rapportering?: string | null;
  endringer_i_virksomheten?: RaaEndringer | null;
}

export interface RaaInnhold {
  tidspunkt_og_frister?: string | null;
  vedlegg?: string[];
  vedlegg_merknad?: string | null;
  opplysninger_som_skal_sendes_inn?: string[];
  opplysninger_merknad?: string | null;
  veiledning_og_utfylling?: string[];
  veiledning_merknad?: string | null;
  innsender_og_tilgang?: RaaInnsender | null;
  innsending_og_oppfolging?: RaaInnsending | null;
  kontakt_og_hjelp?: RaaKontakt | null;
  hva_rettigheten_innebarer?: RaaHvaRettighetenInnebarer | null;
  egne_innholdselementer?: RaaEgetInnholdselement[];
}

export interface RaaRettighet {
  navn: string;
  tjenesteomrade: string | null;
  los_klassifisering: string | null;
  livshendelser: string[];
  type: string | null;
  kompetent_myndighet: string | null;
  status: string | null;
  malgruppe: string[];
  formal: string | null;
  innhold: RaaInnhold | null;
  regelverksreferanser: RaaRegelverksreferanse[];
  handlinger: RaaHandling[];
  avhengigheter: RaaAvhengighet[];
}

export interface RaaModell {
  rettigheter: RaaRettighet[];
}

/** Kaster med en lesbar feilmelding i stedet for å gjette — ingen delvis-gyldig import godtas stille. */
export function tolkModelleksportJson(raaTekst: string): RaaRettighet[] {
  let parset: unknown;
  try {
    parset = JSON.parse(raaTekst);
  } catch {
    throw new Error('Ikke gyldig JSON.');
  }
  if (
    typeof parset !== 'object' ||
    parset === null ||
    !('rettigheter' in parset) ||
    !Array.isArray((parset as RaaModell).rettigheter)
  ) {
    throw new Error('Forventet et objekt med et "rettigheter"-array på rotnivå (samme form som GET /api/tjenester/modelleksport).');
  }
  const rettigheter = (parset as RaaModell).rettigheter;
  rettigheter.forEach((r, i) => {
    if (!r.navn || typeof r.navn !== 'string') {
      throw new Error(`Rettighet nr. ${i + 1} mangler "navn".`);
    }
  });
  return rettigheter;
}
