import type { ComponentPropsWithoutRef, CSSProperties, ElementType, ReactNode } from 'react';
import { Paragraph } from '@digdir/designsystemet-react';

/**
 * [Ny, issue #266, 2026-09-11] Én kilde for "metatekst" (docs/09 §6): hjelpetekst/støttetekst som
 * verken er brødtekst eller en Designsystemet-komponent med egen `data-size`-skala.
 * `Paragraph`s `data-size`-skala når aldri ned til 12px (`xs` = 14px, `sm` = 16px) — §6s fasit er
 * derfor en RÅ token, `fontSize: 'var(--ds-font-size-1)'`, som MÅ kombineres med
 * `color: 'var(--ds-color-neutral-text-subtle)'` (§6s egen presisering — "begge, ikke bare fargen
 * alene").
 *
 * <p>
 * Før denne komponenten fantes 246 uavhengige kopier av denne stiloppskriften — 231 av dem med
 * riktig `fontSize`, men kun 111 (48 %) som faktisk parte den med riktig farge. Denne komponenten
 * låser begge, og lar `style` overstyre ETT AV DEM ved behov (f.eks. en bevisst annen farge på en
 * ellers metatekst-stor melding) — spredningsrekkefølgen under sikrer at en eksplisitt `color` i
 * `style` alltid vinner over defaulten, mens `fontSize` normalt IKKE bør overstyres (det er selve
 * poenget med komponenten).
 * </p>
 *
 * <p>
 * Polymorf via `as` — de 246 opprinnelige stedene var ikke alle `Paragraph` (mange var `span`,
 * `div`, `li`, `nav`, `td` osv., der en semantisk endring til `<p>` ville vært feil, f.eks. inni en
 * `<nav>`-brødsmulesti eller en tabellcelle). Default er `Paragraph` (issue #266s egen anbefaling —
 * "wrapper Paragraph"); `as="span"`/`as="div"`/… eller `as={NoenKomponent}` for de andre.
 * </p>
 */
export function Metatekst<T extends ElementType = typeof Paragraph>({
  as, style, children, ...rest
}: { as?: T; style?: CSSProperties; children?: ReactNode } & Omit<ComponentPropsWithoutRef<T>, 'as' | 'style' | 'children'>) {
  const As = (as ?? Paragraph) as ElementType;
  return (
    <As
      style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', ...style }}
      {...rest}
    >
      {children}
    </As>
  );
}
