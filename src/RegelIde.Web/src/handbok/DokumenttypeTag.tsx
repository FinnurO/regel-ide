import type { CSSProperties } from 'react';
import { Tag } from '@digdir/designsystemet-react';

/**
 * [Ny, issue #267, 2026-09-11] Veiledningsdokumenttypen (hjemmel/praktisk-råd/sjekkliste/kommentar)
 * på et rendret veiledningsutdrag — funnet duplisert IDENTISK (samme fire nøkler, samme farger, samme
 * norske labels) i `TjenesteVeiledning.tsx` (`DOKUMENTTYPE_FARGE`/`DOKUMENTTYPE_LABEL`) og
 * `vilkarstre/Egenskapspanel.tsx` (`VEILEDNINGSDOKUMENTTYPE_FARGE`/`VEILEDNINGSDOKUMENTTYPER`).
 * Konsolidert til én kilde her.
 *
 * <p>
 * <b>Ikke å forveksle med</b> `handbok/KommentarRedigering.tsx` sin `DOKUMENTTYPER`
 * (kommentar/retningslinje/instruks/håndbok) — det er håndbok-SEKSJONENS egen type (avgjør
 * bindende/ikke bindende), et reelt annet domene som deler ordet "dokumenttype" uten å dele
 * betydning. Ikke slått sammen (docs/09 §25-prinsippet: samme navn er ikke bevis på duplisering).
 * </p>
 */
export const VEILEDNINGSDOKUMENTTYPER = [
  { id: 'kommentar', label: 'Kommentar' },
  { id: 'hjemmel', label: 'Hjemmel' },
  { id: 'praktisk-rad', label: 'Praktisk råd' },
  { id: 'sjekkliste', label: 'Sjekkliste' },
] as const;

const VEILEDNINGSDOKUMENTTYPE_FARGE: Record<string, 'info' | 'warning' | 'neutral' | 'success'> = {
  hjemmel: 'info',
  'praktisk-rad': 'warning',
  sjekkliste: 'success',
  kommentar: 'neutral',
};

export function DokumenttypeTag({ dokumenttype, style }: { dokumenttype: string; style?: CSSProperties }) {
  const label = VEILEDNINGSDOKUMENTTYPER.find((d) => d.id === dokumenttype)?.label ?? dokumenttype;
  return (
    <Tag data-color={VEILEDNINGSDOKUMENTTYPE_FARGE[dokumenttype] ?? 'neutral'} data-size="sm" style={style}>
      {label}
    </Tag>
  );
}
