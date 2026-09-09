import { Link as RouterLink } from 'react-router';
import { Card, Heading, Link, Paragraph } from '@digdir/designsystemet-react';

/**
 * [Ny, nemnd/sekretariat-runden, 2026-09-09] Forklaringen av de TO kandidatkøene, som deles av
 * `NavnekandidaterListe`, `VirksomhetKandidaterListe` og veiviseren.
 *
 * <p>
 * Johann 2026-09-09: «kanskje forklare forskjellen på første gangs oppdagelse i navnekandidater vs
 * et sveip i virksomhetskandidater. her flyter det litt sammen.» Det gjorde det med god grunn: begge
 * heter «kandidater», begge har en kø med Venter/Godkjent/Avvist, begge utløses av en knapp som
 * heter «sveip». Forskjellen er RETNINGEN, og den er ikke synlig noe sted i UI-et før dette.
 * </p>
 *
 * <p>
 * Én delt komponent, ikke tre lokale avsnitt: teksten er den samme påstanden om systemet, og tre
 * kopier ville kommet i utakt første gang flyten endres.
 * </p>
 */
export function KandidatflytForklaring({ aktiv }: { aktiv: 'navn' | 'virksomhet' }) {
  return (
    <Card style={{ padding: '0.85rem', marginBottom: '1rem' }}>
      <Heading level={2} data-size="xs" style={{ marginBottom: '0.4rem' }}>
        To køer, motsatt retning
      </Heading>
      <Paragraph
        data-size="sm"
        style={{ marginBottom: '0.4rem', fontWeight: aktiv === 'navn' ? 600 : 400 }}
      >
        <Link asChild>
          <RouterLink to="/navnekandidater">Navnekandidater</RouterLink>
        </Link>{' '}
        — <strong>fra tekst til nytt navn.</strong> Sveipet leser ÉN rettskilde og foreslår navn vi
        ikke kjenner ennå. Resultatet av å behandle en rad er en virksomhet eller et gruppebegrep,
        en navneform, og én tagg: den ene forekomsten kandidaten ble funnet i.
      </Paragraph>
      <Paragraph
        data-size="sm"
        style={{ marginBottom: 0, fontWeight: aktiv === 'virksomhet' ? 600 : 400 }}
      >
        <Link asChild>
          <RouterLink to="/virksomhet-kandidater">Virksomhetskandidater</RouterLink>
        </Link>{' '}
        — <strong>fra kjent navn til alle tekstene.</strong> Sveipet tar navneformene til én
        virksomhet vi ALLEREDE kjenner og leter gjennom hele korpuset. Å godkjenne en rad lager
        taggen for den forekomsten; massegodkjenning tagger alle på én gang.
      </Paragraph>
      <Paragraph
        data-size="sm"
        style={{ marginTop: '0.5rem', marginBottom: 0, color: 'var(--ds-color-neutral-text-subtle)' }}
      >
        Rekkefølgen er derfor: oppdag navnet én gang her, og finn deretter resten av forekomstene med
        et virksomhetssveip. Et navnekandidat-sveip på samme forskrift en gang til gir ingen nye
        rader for et navn som alt er behandlet — det er ikke feil, det er den andre køens jobb.
      </Paragraph>
    </Card>
  );
}
