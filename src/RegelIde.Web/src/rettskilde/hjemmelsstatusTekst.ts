// [Ny, issue #472 PR 1, Johanns avgjørelser 2026-11-18] Tekstene for hjemmelsstatusen på rettskildesiden (docs/09 §56). Ren modul uten React,
// så reglene kan testes med vitest (docs/09 §17).

/** Samlet status. Ordet for den som sier at hjemlene er borte, er Johanns: «Hjemmel opphevet, videreføring ikke funnet» (mulig bortfalt).
 * Lovdata viser forskriften som gjeldende, så ingen av tekstene sier at den ER bortfalt (CLAUDE.md §9). */
export const SAMLET_STATUS: Record<string, { tekst: string; forklaring: string }> = {
  aktiv: { tekst: 'Hjemlene er aktive', forklaring: 'Alle hjemlene som teller, er gjeldende i korpuset.' },
  viderefort: {
    tekst: 'Videreført etter overgangsregel',
    forklaring: 'Minst én hjemmel er opphevet, men en overgangsregel viderefører vedtak gitt i medhold av den.',
  },
  videreforing_utlopt: {
    tekst: 'Videreføringen er utløpt',
    forklaring: 'Hjemmelen er opphevet, og overgangsregelen viderefører bare fram til en dato som er passert.',
  },
  hjemmel_opphevet: {
    tekst: 'Hjemmel opphevet, videreføring ikke funnet (mulig bortfalt)',
    forklaring:
      'Alle hjemlene som teller, er opphevet, og ingen videreføring er funnet. Ikke funnet betyr ikke at den ikke finnes, og forskriften kan '
      + 'gjelde ved tolkning så langt den kunne vært gitt etter den nye loven. Lovdata viser forskriften som gjeldende.',
  },
  blandede_hjemler: {
    tekst: 'Blandede hjemler',
    forklaring: 'Noen hjemler er opphevet uten funnet videreføring, andre bærer. Hvilken del av forskriften som står uten hjemmel, er ikke avgjort.',
  },
  uavklart: {
    tekst: 'Uavklart',
    forklaring: 'Noen hjemler er opphevet uten funnet videreføring, og det eneste som bærer, er hjemler der rollen ikke er vurdert.',
  },
  ukjent: { tekst: 'Ukjent', forklaring: 'Statusen til minst én hjemmel kan ikke avgjøres med kildene i korpuset.' },
};

export const STATUS: Record<string, string> = {
  aktiv: 'Aktiv',
  opphevet: 'Opphevet, videreføring ikke funnet',
  opphevet_bestemmelse: 'Bestemmelsen er opphevet',
  viderefort: 'Opphevet, videreført',
  videreforing_utlopt: 'Opphevet, videreføringen er utløpt',
  erstattet: 'Opphevet og erstattet',
  gjenstandslos: 'Gjenstandsløs',
  ukjent: 'Ukjent',
};

export const ROLLE: Record<string, { tekst: string; forklaring: string }> = {
  materiell: { tekst: 'Materiell hjemmel', forklaring: 'Rettsgrunnlaget for innholdet.' },
  kompetanseledd: {
    tekst: 'Kompetanseledd',
    forklaring: 'Viser hvem som hadde myndigheten (et delegeringsvedtak, «jf. kgl.res.», Grl. § 12). At det oppheves, gjør ikke vedtaket ugyldig.',
  },
  overgangsgrunnlag: { tekst: 'Overgangsgrunnlag', forklaring: 'En overgangsbestemmelse som bærer forskriften videre.' },
  gjenstand: { tekst: 'Gjenstand', forklaring: 'Loven vedtaket handler om, ikke hjemmelen for det.' },
};

export const ROLLEREGEL: Record<string, string> = {
  med_hjemmel_i: 'hjemmelsteksten nevner loven etter «med hjemmel i»',
  jf_vedtak: 'hjemmelsteksten nevner vedtaket etter «jf.»',
  delegeringsvedtak: 'hjemmelen er selv et delegeringsvedtak',
  overgangsparagraf: 'bestemmelsen står i en overgangsparagraf',
  grl_12: 'Grunnloven § 12 er ressortfordeling',
  ressortvedtak_gjenstand: 'ressortvedtak etter Grl. § 12, og hjemmelsteksten nevner ikke loven',
};

export const BEVISTRINN: Record<string, string> = {
  E1: 'opphevingssetning i en kilde som har hjemmelen i «Endrer»',
  E1u: 'opphevingssetning i utvidet form (passiv, «til», eller punktum i tittelen), i en kilde som har hjemmelen i «Endrer»',
  E2: 'opphevingssetning uten «Endrer»-kobling',
  EN_LTI: 'opphevingssetningen står bare i den kunngjorte teksten (Norsk Lovtidend)',
};

export const UKJENT_GRUNN: Record<string, string> = {
  EN: 'en gjeldende lov har hjemmelen i «Endrer», men opphevingssetningen er ikke funnet',
  END: 'hjemmelen er en endringslov som ikke kan følges til én lov',
  E3: 'hjemmelen nevnes bare sammen med et oppheve-ord',
  INGEN: 'ingen spor i korpuset',
  eid_ikke_funnet: 'bestemmelsen finnes ikke i loven',
  bestemmelse_opphevet: 'bestemmelsen er merket opphevet; videreføringen står i endringsloven og er ikke undersøkt',
  oppheving_ikke_i_kraft: 'opphevingen er ikke i kraft ennå',
  mangler_i_korpus: 'hjemmelsdokumentet mangler i korpuset',
};

export const DEKNING: Record<string, string> = {
  dekket: 'Overgangsregelen nevner dokumenttypen.',
  dokumenttype_ikke_nevnt: 'Overgangsregelen nevner ikke denne typen vedtak; at den dekkes, er fortolkning.',
  avgrensning_ikke_kontrollert: 'Overgangsregelen gjelder bare noen kapitler eller paragrafer, og hjemmelen er hele loven.',
};

/** «Opphevet av LOV-2004-03-05-12 § 48 andre ledd» — referansen og bestemmelsen, uten tomrom når en av dem mangler. */
export function kildetekst(k: { referanse: string | null; bestemmelse: string | null; tittel?: string | null } | null): string {
  if (!k) return '';
  return [k.referanse ?? k.tittel, k.bestemmelse].filter(Boolean).join(' ');
}

/** «31.12.2019» fra «2019-12-31». */
export function norskDato(iso: string | null): string | null {
  if (!iso) return null;
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  return m ? `${m[3]}.${m[2]}.${m[1]}` : iso;
}
