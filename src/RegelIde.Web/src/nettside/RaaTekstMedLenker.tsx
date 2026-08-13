import type { ReactNode } from 'react';
import { Link as RouterLink } from 'react-router';
import { Link } from '@digdir/designsystemet-react';
import type { NettsideLenkeDto } from '../api/types';

const MARKDOWN_LENKE_MØNSTER = /\[([^\]]*)\]\((\S+?)\)/g;

/**
 * Konverterer Markdown-lenker `[tekst](href)` i NettsideDokument.RaaTekst til ekte `<a>`-elementer
 * (se Entiteter.cs/NettsideTekstParser-kommentarene for HVORFOR raaTekst bærer lenker i denne
 * notasjonen i stedet for `<a href>` direkte — RaaTekst er tekstlaget, ikke rå HTML). Løst INTERN
 * lenke (til en annen NettsideDokument eller en importert RettskildeEntitet) peker til den faktiske
 * detaljsiden i appen; uløst/ekstern lenke vises som en vanlig ekstern lenke i ny fane, akkurat som
 * oppgaven ba om.
 */
export function RaaTekstMedLenker({ raaTekst, lenker }: { raaTekst: string; lenker: NettsideLenkeDto[] }) {
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

function gjengiAvsnitt(avsnitt: string, lenkePerHref: Map<string, NettsideLenkeDto>): ReactNode[] {
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

function LenkeElement({ ankerTekst, href, lenke }: { ankerTekst: string; href: string; lenke?: NettsideLenkeDto }) {
  if (lenke?.tilNettsideDokumentId) {
    return (
      <Link asChild>
        <RouterLink to={`/nettsider/${lenke.tilNettsideDokumentId}`}>{ankerTekst}</RouterLink>
      </Link>
    );
  }
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
