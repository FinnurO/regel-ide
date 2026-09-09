import { Tag } from '@digdir/designsystemet-react';

/**
 * [Ny, konfidens-runden, 2026-09-09] Konfidensen på en navnekandidat, med grunnen som hover-tekst.
 *
 * <p>
 * Bakgrunn: et `virksomhet`-treff SNL/SSR ikke bekreftet ble AUTOMATISK AVVIST. Det skjulte reelle
 * organer — «Reguleringsmyndigheten» og «Energiklagenemndas» lå begge som automatisk avvist i
 * forskrift om Energiklagenemnda, fordi SNL ikke har artikler om dem. Johann 2026-09-09: «vi kan ikke
 * automatisk avvise disse p.g.a. manglende SNL/SSR. Kan vi innføre Høy/Lav konfidens fremfor å avvise
 * dem?»
 * </p>
 *
 * <p>
 * <b>Fargevalget er et krav, ikke pynt</b> — samme prinsipp som `NavneformgrunnTag` (docs/09 §15):
 * `success` for høy (en ekstern kilde bekreftet navnet), `neutral` for lav. Lav konfidens er
 * BEVISST ikke `warning`/`danger`: det er ingen feil og ingen advarsel, bare fravær av bekreftelse.
 * En rød merkelapp ville lest som «dette er galt» og gjenskapt nøyaktig den avvisningen vi fjernet.
 * </p>
 *
 * <p>
 * Ingen merkelapp for `null` (ikke klassifisert). Alle `gruppe`-kandidater er null — de sendes aldri
 * til SNL/SSR — og en «Ikke klassifisert»-lapp på hver av dem ville vært støy uten innhold.
 * </p>
 */
export function KonfidensTag({ konfidens, grunn }: { konfidens: string | null; grunn: string | null }) {
  if (!konfidens) return null;
  const hoy = konfidens === 'hoy';
  return (
    <Tag data-size="sm" data-color={hoy ? 'success' : 'neutral'} title={konfidensGrunnTekst(grunn)}>
      {hoy ? 'Høy konfidens' : 'Lav konfidens'}
    </Tag>
  );
}

/**
 * Grunnkodene som hel setning. Fritekst ville kommet i utakt med backend; kodene er et lukket
 * vokabular med CHECK-constraint, og oversettelsen hører i visningen.
 *
 * <p>
 * `null` er et ekte, dokumentert tilfelle og ikke en mangel: rader som ble migrert fra den gamle
 * auto-avvisningen har konfidens `'lav'` men ingen grunn, fordi grunnen aldri ble lagret og ikke kan
 * utledes i ettertid (se migrasjonen `LeggTilKonfidensPaNavnekandidat`). Teksten sier det, i stedet
 * for å gjette.
 * </p>
 */
export function konfidensGrunnTekst(grunn: string | null): string {
  switch (grunn) {
    case 'snl_treff':
      return 'Bekreftet som institusjon i Store norske leksikon.';
    case 'ssr_med_institusjonsord':
      return 'Bekreftet stedsnavn i Kartverkets SSR, med et institusjonsord rett etter i teksten.';
    case 'ssr_uten_institusjonsord':
      return 'Bekreftet stedsnavn i Kartverkets SSR, men UTEN institusjonsord etter — ofte en '
        + 'geografisk referanse i løpetekst, ikke et organ.';
    case 'ukjent_i_snl_og_ssr':
      return 'Ukjent i både Store norske leksikon og Kartverkets SSR. Det betyr ikke at navnet er '
        + 'feil — mange reelle forvaltningsorganer har ingen SNL-artikkel.';
    default:
      return 'Grunn ikke registrert (raden er fra før konfidens ble innført).';
  }
}
