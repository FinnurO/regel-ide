import { useCallback, useEffect, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { VisningsinnstillingInput } from '../api/types';
import { ACCORDION_NOKLER, SEKSJON_NOKLER } from '../api/tjenesteFelt';

/**
 * Innlogget brukers fanerekkefølge/-synlighet og accordion-rekkefølge/åpen-tilstand på
 * Tjeneste-siden (2026-08-27, Tjenestedetalj-redesignrunden) — PER BRUKER, lagret på serveren
 * (GET/PUT /api/brukere/meg/tjeneste-visning), IKKE localStorage og IKKE per tjeneste. Se
 * `BrukerVisningsinnstillingEntitet` på serveren for begrunnelsen.
 * <p>
 * Lagrer optimistisk — UI-en reflekterer endringen umiddelbart, lagre-kallet skjer i bakgrunnen.
 * En feilet lagring vises som en ikke-blokkerende feilmelding; selve UI-tilstanden ruller IKKE
 * tilbake (en mislykket nettverksrunde skal ikke rykke brukerens nettopp gjorte omrokkering
 * tilbake under henne).
 */
export function useVisningsinnstillinger() {
  const [innstilling, setInnstilling] = useState<VisningsinnstillingInput | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    api.hentTjenesteVisningsinnstillinger().then(setInnstilling).catch((e) => {
      setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av visningsinnstillinger.');
      // Faller tilbake til standard rekkefølge/synlighet lokalt, slik at siden fortsatt er
      // brukbar selv om lagringen av PREFERANSER er nede — dette er komfort, ikke kjernedata.
      setInnstilling({
        seksjonsrekkefolge: [...SEKSJON_NOKLER],
        skjulteSeksjoner: [],
        accordionRekkefolge: [...ACCORDION_NOKLER],
        accordionApne: { grunnleggende: true },
      });
    });
  }, []);

  const lagre = useCallback((ny: VisningsinnstillingInput) => {
    setInnstilling(ny);
    api.lagreTjenesteVisningsinnstillinger(ny).catch((e) => {
      setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved lagring av visningsinnstillinger.');
    });
  }, []);

  function flyttSeksjon(nokkel: string, retning: -1 | 1) {
    if (!innstilling) return;
    const rekkefolge = innstilling.seksjonsrekkefolge.slice();
    const i = rekkefolge.indexOf(nokkel);
    const j = i + retning;
    if (i < 0 || j < 0 || j >= rekkefolge.length) return;
    [rekkefolge[i], rekkefolge[j]] = [rekkefolge[j], rekkefolge[i]];
    lagre({ ...innstilling, seksjonsrekkefolge: rekkefolge });
  }

  function skjulSeksjon(nokkel: string) {
    if (!innstilling || innstilling.skjulteSeksjoner.includes(nokkel)) return;
    lagre({ ...innstilling, skjulteSeksjoner: [...innstilling.skjulteSeksjoner, nokkel] });
  }

  function visSeksjon(nokkel: string) {
    if (!innstilling) return;
    lagre({ ...innstilling, skjulteSeksjoner: innstilling.skjulteSeksjoner.filter((k) => k !== nokkel) });
  }

  function flyttAccordion(nokkel: string, retning: -1 | 1) {
    if (!innstilling) return;
    const rekkefolge = innstilling.accordionRekkefolge.slice();
    const i = rekkefolge.indexOf(nokkel);
    const j = i + retning;
    if (i < 0 || j < 0 || j >= rekkefolge.length) return;
    [rekkefolge[i], rekkefolge[j]] = [rekkefolge[j], rekkefolge[i]];
    lagre({ ...innstilling, accordionRekkefolge: rekkefolge });
  }

  function settAccordionApen(nokkel: string, apen: boolean) {
    if (!innstilling) return;
    lagre({ ...innstilling, accordionApne: { ...innstilling.accordionApne, [nokkel]: apen } });
  }

  function apneAlleAccordions(apen: boolean) {
    if (!innstilling) return;
    const alle: Record<string, boolean> = {};
    for (const k of innstilling.accordionRekkefolge) alle[k] = apen;
    lagre({ ...innstilling, accordionApne: alle });
  }

  return {
    innstilling, feil,
    flyttSeksjon, skjulSeksjon, visSeksjon,
    flyttAccordion, settAccordionApen, apneAlleAccordions,
  };
}
