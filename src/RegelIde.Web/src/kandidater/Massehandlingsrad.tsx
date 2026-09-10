import type { ReactNode } from 'react';
import { Button, Paragraph } from '@digdir/designsystemet-react';

/**
 * [Ny, kandidatside-runden, 2026-09-09, issue #216] Massehandlingsraden over en kandidattabell:
 * telleren, Godkjenn/Avvis/Slett valgte, og feilmeldingen under.
 *
 * <p>
 * Navnekandidat- og virksomhetskandidatsiden hadde hver sin kopi. Begrepskandidatsiden hadde INGEN —
 * der måtte hver rad behandles for seg, og et M1-sveip på én definisjonsparagraf gir tjue rader i
 * samme paragraf. Det er ikke en arbeidsflate, og det var den reelle mangelen bak issuet.
 * </p>
 *
 * <p>
 * Én tilstand for hele raden (`kjorer`/`feil`), ikke én per knapp: knappene sitter i samme rad, deler
 * det samme utvalget, og bare én av dem kan være i gang. Det var slik begge de eksisterende kopiene
 * oppførte seg, og det er bevisst beholdt.
 * </p>
 *
 * <p>
 * Feilmeldingen er ETT felt fordi backend-batchene svarer per rad: kalleren setter
 * «3 av 20 rad(er) feilet: …» og beholder de radene som gikk gjennom. En batch som ruller tilbake alt
 * fordi én rad var ugyldig ville vært verre for arbeidskøen — se batch-endepunktene.
 * </p>
 */
export interface MassehandlingsradProps {
  antallValgte: number;
  kjorer: boolean;
  feil: string | null;
  /** Tillegg til telleren, f.eks. « — filtrert til én lov/forskrift». */
  merknad?: string;
  onGodkjenn: () => void;
  onAvvis: () => void;
  /**
   * Presis sletting av de avkryssede radene. Utelates der køen ikke har noe å slette presist —
   * knappen skal da IKKE vises, i stedet for å vises deaktivert uten forklaring.
   */
  onSlett?: () => void;
  /**
   * Kontroll som må stå FØR knappene for at handlingen skal være mulig — begrepskandidatsiden
   * plasserer virksomhetsvelgeren her, siden godkjenning der ikke kan utledes uten den.
   */
  children?: ReactNode;
}

export function Massehandlingsrad({
  antallValgte, kjorer, feil, merknad, onGodkjenn, onAvvis, onSlett, children,
}: MassehandlingsradProps) {
  const ingenting = antallValgte === 0;
  return (
    <>
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap' }}>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', margin: 0 }}>
          {antallValgte} valgt{antallValgte === 1 ? '' : 'e'}
          {merknad ?? ''}
        </Paragraph>
        {children}
        <Button data-size="sm" onClick={onGodkjenn} disabled={ingenting || kjorer}>
          {kjorer ? 'Godkjenner …' : 'Godkjenn valgte'}
        </Button>
        <Button data-size="sm" variant="secondary" onClick={onAvvis} disabled={ingenting || kjorer}>
          {kjorer ? 'Avviser …' : 'Avvis valgte'}
        </Button>
        {onSlett && (
          <Button data-size="sm" data-color="danger" onClick={onSlett} disabled={ingenting || kjorer}>
            {kjorer ? 'Sletter …' : 'Slett valgte'}
          </Button>
        )}
      </div>
      {feil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{feil}</div>}
    </>
  );
}
