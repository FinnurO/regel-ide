import type { ReactNode } from 'react';
import { Link as RouterLink } from 'react-router';
import { Link } from '@digdir/designsystemet-react';
import type { NettsideLenkeMedMalDto } from '../api/types';

const MARKDOWN_LENKE_MØNSTER = /\[([^\]]*)\]\((\S+?)\)/g;

/**
 * Konverterer Markdown-lenker `[tekst](href)` i en Brukerveilednings side-node-tekst til ekte
 * `<a>`-elementer (se NettsideTekstParser-kommentaren for HVORFOR teksten bærer lenker i denne
 * notasjonen i stedet for `<a href>` direkte — det er tekstlaget, ikke rå HTML). Løst lenke (til en
 * importert RettskildeEntitet — en annen Brukerveiledning, en håndbok, en lov) peker til den faktiske
 * detaljsiden i appen; uløst/ekstern lenke vises som en vanlig ekstern lenke i ny fane.
 *
 * Punkt 8 (avklaringsrunde 2026-08-13) — flyttet fra `src/nettside/` til dette mer generelle stedet:
 * en nettside ER nå en ordinær RettskildeEntitet, og denne komponenten er derfor ikke lenger
 * nettside-spesifikk i noen arkitektonisk forstand, kun i hvilken NodeType-tekst den typisk brukes på
 * ("side"). Samme komponent, ingen funksjonell endring bortsett fra ÉN målfamilie i stedet for to
 * (`tilNettsideDokumentId` finnes ikke lenger — se NettsideLenkeMedMalDto).
 */
export function RaaTekstMedLenker({ raaTekst, lenker }: { raaTekst: string; lenker: NettsideLenkeMedMalDto[] }) {
  const lenkePerHref = new Map(lenker.map((l) => [l.raaHref, l]));

  return (
    <>
      {raaTekst.split(/\n{2,}/).map((avsnitt, i) => (
        <p key={i} style={{ whiteSpace: 'pre-wrap', marginBottom: '1rem' }}>
          {gjengiAvsnitt(avsnitt, lenkePerHref)}
        </p>
      ))}
    </>
  );
}

function gjengiAvsnitt(avsnitt: string, lenkePerHref: Map<string, NettsideLenkeMedMalDto>): ReactNode[] {
  const deler: ReactNode[] = [];
  let sistIndeks = 0;
  let nokkel = 0;

  for (const treff of avsnitt.matchAll(MARKDOWN_LENKE_MØNSTER)) {
    const [helMatch, ankerTekst, href] = treff;
    const start = treff.index ?? 0;
    if (start > sistIndeks) deler.push(avsnitt.slice(sistIndeks, start));

    const lenke = lenkePerHref.get(href);
    deler.push(<LenkeElement key={`lenke-${nokkel++}`} ankerTekst={ankerTekst || href} href={href} lenke={lenke} />);
    sistIndeks = start + helMatch.length;
  }
  if (sistIndeks < avsnitt.length) deler.push(avsnitt.slice(sistIndeks));

  return deler;
}

function LenkeElement({ ankerTekst, href, lenke }: { ankerTekst: string; href: string; lenke?: NettsideLenkeMedMalDto }) {
  if (lenke?.tilRettskildeId) {
    return (
      <Link asChild>
        <RouterLink to={`/rettskilder/${lenke.tilRettskildeId}`}>{ankerTekst}</RouterLink>
      </Link>
    );
  }
  // Uløst/ekstern — vanlig ekstern lenke, ny fane (samme "vis kilden" prinsipp som resten av appen).
  return (
    <Link href={href} target="_blank" rel="noopener noreferrer">
      {ankerTekst}
    </Link>
  );
}
