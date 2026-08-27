/**
 * Feltnøkkel-konvensjonen for `TjenesteRegelverksreferanseDto.felt` (2026-08-27, Tjenestedetalj-
 * redesignrunden) — ÉN kilde til sannhet, samme nøkler frontend og backend bruker, ALLTID de ekte
 * DTO-feltnavnene (aldri en forkortelse/oversettelsestabell). Se `TjenesteFeltnokler`-kommentaren i
 * `RegelIde.Data/TjenesteregisterTjeneste.cs` for den autoritative, fullstendige listen dette speiler.
 * <p>
 * Også hjem for de faste fane- og accordion-nøklene (`SEKSJONER`/`ACCORDIONER`) — samme nøkler som
 * `BrukerVisningsinnstillingTjeneste` på serveren forventer i et `VisningsinnstillingInput`.
 */

/** De 7 faste fane-nøklene (utenom "oversikt", som alltid er først og aldri skjulbar), i
 * SERVERENS standardrekkefølge — se BrukerVisningsinnstillingTjeneste.StandardSeksjonsrekkefolge. */
export const SEKSJON_NOKLER = [
  'vilkarstre', 'innhold', 'status', 'regelverk', 'hendelser', 'handlinger', 'avhengigheter',
] as const;
export type SeksjonNokkel = (typeof SEKSJON_NOKLER)[number];

export const SEKSJON_LABELER: Record<SeksjonNokkel, string> = {
  vilkarstre: 'Vilkårstre',
  innhold: 'Innhold',
  status: 'Status',
  regelverk: 'Regelverksreferanser',
  hendelser: 'Hendelser',
  handlinger: 'Handlinger',
  avhengigheter: 'Avhengigheter',
};

/** De 9 faste accordion-nøklene i Innhold-fanen, i SERVERENS standardrekkefølge — se
 * BrukerVisningsinnstillingTjeneste.StandardAccordionRekkefolge. */
export const ACCORDION_NOKLER = [
  'grunnleggende', 'tidspunkt', 'innsender', 'vedlegg', 'opplysninger', 'veiledning', 'innsending', 'kontakt', 'innebaerer',
] as const;
export type AccordionNokkel = (typeof ACCORDION_NOKLER)[number];

export const ACCORDION_LABELER: Record<AccordionNokkel, string> = {
  grunnleggende: 'Grunnleggende',
  tidspunkt: 'Tidspunkt og frister',
  innsender: 'Innsender og tilgang',
  vedlegg: 'Vedlegg',
  opplysninger: 'Opplysninger',
  veiledning: 'Veiledning og utfylling',
  innsending: 'Innsending og oppfølging',
  kontakt: 'Kontakt og hjelp',
  innebaerer: 'Hva rettigheten innebærer',
};

/** Feltnøkkel for et fritt innholdselement — se TjenesteregisterTjeneste.cs sin kommentar. */
export function feltnokkelForEgetInnhold(id: string): string {
  return `egneInnholdselementer.${id}`;
}
