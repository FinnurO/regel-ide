import type {
  EgetInnholdselementInput, HandlingArsakInput, HandlingRequest, HandlingVedleggInput, HandlingVeiledningstekstInput,
  TjenesteInnholdInput, TjenesteRequest,
} from '../api/types';
import type { RaaHandling, RaaHjemmel, RaaInnhold, RaaRettighet } from './modelleksportTyper';

/**
 * [Ny, 2026-08-28, import-wizard-runden] Konverterer den rå, snake_case modelleksport-formen til
 * appens interne camelCase request-DTO-er — se docs/21-feltmapping-eksterne-kilder.md for reglene.
 * Ingen gjettet fallback: der en RAA-verdi mangler et felt appens skrive-API krever som ikke-nullbart
 * (f.eks. `HandlingArsakInput.Hjemmel`, `HandlingVedleggInput.Navn`), UTELATES den enkeltraden i
 * stedet for å fylles med en oppdiktet placeholder — samlet i `advarsler` slik at wizarden kan vise
 * dem til mennesket.
 */
export interface KonverteringResultat {
  request: TjenesteRequest;
  handlinger: HandlingRequest[];
  advarsler: string[];
}

function konverterHjemmel(raa: RaaHjemmel | null | undefined): { lov: string; henvisning: string | null } | null {
  if (!raa || !raa.lov) return null;
  return { lov: raa.lov, henvisning: raa.henvisning ?? null };
}

function konverterInnhold(raa: RaaInnhold | null | undefined): { innhold: TjenesteInnholdInput | null; konsekvensVedBrudd: string | null } {
  if (!raa) return { innhold: null, konsekvensVedBrudd: null };
  const hvi = raa.hva_rettigheten_innebarer;
  const innhold: TjenesteInnholdInput = {
    tidspunktOgFrister: raa.tidspunkt_og_frister ?? null,
    innsenderOgTilgang: raa.innsender_og_tilgang
      ? { hvemKanSende: raa.innsender_og_tilgang.hvem_kan_sende, innlogging: raa.innsender_og_tilgang.innlogging }
      : null,
    vedlegg: raa.vedlegg ?? [],
    vedleggMerknad: raa.vedlegg_merknad ?? null,
    opplysningerSomSkalSendesInn: raa.opplysninger_som_skal_sendes_inn ?? [],
    opplysningerMerknad: raa.opplysninger_merknad ?? null,
    veiledningOgUtfylling: raa.veiledning_og_utfylling ?? [],
    veiledningMerknad: raa.veiledning_merknad ?? null,
    innsendingOgOppfolging: raa.innsending_og_oppfolging
      ? { kanal: raa.innsending_og_oppfolging.kanal, etterMottak: raa.innsending_og_oppfolging.etter_mottak, merknad: raa.innsending_og_oppfolging.merknad ?? null }
      : null,
    kontaktOgHjelp: raa.kontakt_og_hjelp
      ? { generelt: raa.kontakt_og_hjelp.generelt, kommunenKanVeiledeOm: raa.kontakt_og_hjelp.kommunen_kan_veilede_om }
      : null,
    hvaRettighetenInnebarer: hvi
      ? {
          innledning: hvi.innledning ?? null,
          varighet: hvi.varighet ?? null,
          plikter: hvi.plikter ?? [],
          endringerIVirksomheten: hvi.endringer_i_virksomheten
            ? { plikt: hvi.endringer_i_virksomheten.plikt, eksempler: hvi.endringer_i_virksomheten.eksempler }
            : null,
          kontrollOgTilsyn: hvi.kontroll_og_tilsyn ?? null,
          avgrensningMerknad: hvi.avgrensning_merknad ?? null,
          kravTilDrift: hvi.krav_til_drift ?? null,
          tommeavtaleOgKontroll: hvi.tommeavtale_og_kontroll ?? null,
          rapportering: hvi.rapportering ?? null,
        }
      : null,
  };
  return { innhold, konsekvensVedBrudd: hvi?.konsekvenser_ved_brudd_pa_regelverket ?? null };
}

function konverterEgneInnholdselementer(raa: RaaInnhold | null | undefined): EgetInnholdselementInput[] {
  return (raa?.egne_innholdselementer ?? []).map((e) => ({ id: e.id, tittel: e.tittel, tekst: e.tekst }));
}

function konverterHandling(raa: RaaHandling, advarsler: string[], indeksPrefiks: string): HandlingRequest | null {
  if (!raa.handlingstype) {
    advarsler.push(`${indeksPrefiks}: handling «${raa.navn}» mangler handlingstype, ble utelatt.`);
    return null;
  }
  const vedlegg: HandlingVedleggInput[] = [];
  for (const v of raa.vedlegg ?? []) {
    if (!v.navn) {
      advarsler.push(`${indeksPrefiks}: et vedlegg på «${raa.navn}» mangler navn, ble utelatt.`);
      continue;
    }
    vedlegg.push({ navn: v.navn, kategori: v.kategori ?? null, hjemmel: konverterHjemmel(v.hjemmel) });
  }
  const veiledningstekst: HandlingVeiledningstekstInput[] = [];
  for (const v of raa.veiledningstekst ?? []) {
    if (!v.overskrift) {
      advarsler.push(`${indeksPrefiks}: en veiledningstekst på «${raa.navn}» mangler overskrift, ble utelatt.`);
      continue;
    }
    veiledningstekst.push({ overskrift: v.overskrift, innhold: v.innhold ?? null, hjemmel: konverterHjemmel(v.hjemmel) });
  }
  const arsaker: HandlingArsakInput[] = [];
  for (const a of raa.arsaker ?? []) {
    const hjemmel = konverterHjemmel(a.hjemmel);
    if (!a.arsak || !hjemmel) {
      advarsler.push(`${indeksPrefiks}: en årsak på «${raa.navn}» mangler årsakstekst eller hjemmel, ble utelatt.`);
      continue;
    }
    arsaker.push({ arsak: a.arsak, hjemmel });
  }
  return {
    navn: raa.navn,
    handlingstype: raa.handlingstype,
    bruksomraade: raa.bruksomraade ?? null,
    utfortAv: raa.utfort_av ?? null,
    kanaler: (raa.kanaler ?? []).map((k) => ({ kanal: k.kanal, adresse: k.adresse ?? null })),
    behandlingstid: raa.behandlingstid ? { frist: raa.behandlingstid.frist, hjemmel: konverterHjemmel(raa.behandlingstid.hjemmel) } : null,
    kostnad: raa.kostnad ? { belop: raa.kostnad.belop, hjemmel: (raa.kostnad.hjemmel ?? []).map((h) => konverterHjemmel(h)).filter((h): h is { lov: string; henvisning: string | null } => h !== null) } : null,
    vedlegg,
    veiledningstekst,
    arsaker,
    resultat: raa.resultat ? { hva: raa.resultat.hva, bevisKanaler: (raa.resultat.bevis_kanaler ?? []).filter((b) => b.kanal).map((b) => ({ kanal: b.kanal! })) } : null,
    merknad: raa.merknad ?? null,
  };
}

/** `regelverksreferanser`/avhengigheter tas IKKE med her — de krever FK-resolusjon fra wizarden (se ImportWizard.tsx). */
export function konverterRettighet(raa: RaaRettighet): KonverteringResultat {
  const advarsler: string[] = [];
  const { innhold, konsekvensVedBrudd } = konverterInnhold(raa.innhold);
  const request: TjenesteRequest = {
    tittel: raa.navn,
    beskrivelse: null,
    kompetentMyndighet: raa.kompetent_myndighet ?? null,
    output: null,
    tjenestetype: null,
    malgruppe: raa.malgruppe ?? [],
    kanaler: null,
    kostnad: null,
    behandlingstid: null,
    kontaktpunkt: null,
    konsekvensVedBrudd,
    sprak: null,
    livshendelser: raa.livshendelser ?? [],
    losKlassifisering: raa.los_klassifisering ?? null,
    tjenesteomrade: raa.tjenesteomrade ?? null,
    type: raa.type ?? null,
    formal: raa.formal ?? null,
    innhold,
    egneInnholdselementer: konverterEgneInnholdselementer(raa.innhold),
  };
  const handlinger = (raa.handlinger ?? [])
    .map((h, i) => konverterHandling(h, advarsler, `«${raa.navn}», handling ${i + 1}`))
    .filter((h): h is HandlingRequest => h !== null);
  return { request, handlinger, advarsler };
}

/**
 * Prøver å finne et gjenkjennelig rettskilde-søkeord fra `lov`-fritekstfeltet (f.eks.
 * "Ekteskapsloven – ekteskl (LOV-1991-07-04-47)" → "Ekteskapsloven") — et FORSØK til å forhåndsutfylle
 * søkefeltet, ALDRI en automatisk kobling. Mennesket bekrefter/søker selv videre.
 */
export function gjettRettskildeSokeord(lovTekst: string | null): string {
  if (!lovTekst) return '';
  return lovTekst.split(/[–(]/)[0]?.trim() ?? lovTekst;
}

/** Samme idé for virksomhet — tar første "ord-gruppe" før en parentes/komma i kompetent_myndighet. */
export function gjettVirksomhetSokeord(kompetentMyndighet: string | null): string {
  if (!kompetentMyndighet) return '';
  return kompetentMyndighet.split(/[–(,]/)[0]?.trim() ?? kompetentMyndighet;
}
