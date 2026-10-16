import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Card, Details, Heading, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import type { RettskildeSammendrag, UtledetMedlemskapDto } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';

/**
 * [Ny, issue #384, Johanns beslutning 2026-10-16] Medlemmene av en klasse med UTLEDET medlemskap — klassen «kommune»: kommunene i
 * områderegisteret med sitt rettssubjekt. Uten denne seksjonen viste siden «Ingen virksomheter er tildelt denne gruppen ennå» for en klasse
 * med 357 medlemmer (CLAUDE.md §9: systemtilstand er ikke en påstand om verden).
 *
 * Følger GruppeMedlemmer (docs/09 §16): `Card` alltid rendret, `null` = laster ⇒ `Spinner`, kompakt tabell, paginert (§9). Kunnskapsstatus
 * vises med `KunnskapsstatusTag` (§35): klassen selv (tekstfunn, kommuneloven § 2-1 første ledd) og hvert medlemskap (avledet). Kommuner som ikke
 * kunne gjøres til medlem, står i en `warning`-`Alert` — de er ikke med i tabellen, og ingen er valgt for dem.
 */
export function UtledetMedlemskap({ begrepId, rettskilder }: { begrepId: string; rettskilder: RettskildeSammendrag[] }) {
  const [svar, setSvar] = useState<UtledetMedlemskapDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    let avbrutt = false;
    setSvar(null);
    setFeil(null);
    api.hentUtledetMedlemskap(begrepId)
      .then((s) => { if (!avbrutt) setSvar(s); })
      .catch((e) => { if (!avbrutt) setFeil(e instanceof ApiError ? e.message : 'Kunne ikke regne ut medlemskapet.'); });
    return () => { avbrutt = true; };
  }, [begrepId]);

  const paginering = usePaginering(svar?.medlemmer ?? []);
  const hjemmelLenke = svar?.hjemmel ? rettskildeLenke(svar.hjemmel, rettskilder) : undefined;

  return (
    <section style={{ marginBottom: '2rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
        Medlemmer (utledet av områderegisteret)
      </Heading>
      <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '-0.5rem', marginBottom: '0.75rem' }}>
        Medlemskapet registreres ikke for hånd. Det regnes ut hver gang: kommunene i Kartverkets inndeling, med virksomheten som har
        ansvarsområde til kommunen og samme kommunenummer. Premisset for hvert medlemskap er den territoriekanten.
      </Metatekst>
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}
      <Card style={{ padding: svar && svar.medlemmer.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
        {svar === null ? (
          feil ? <Paragraph style={{ margin: 0 }}>Medlemskapet er ukjent (se feilen over).</Paragraph>
            : <Spinner aria-label="Regner ut medlemskapet …" data-size="sm" />
        ) : (
          <>
            <div style={{ padding: svar.medlemmer.length > 0 ? '0.75rem 1rem' : 0, display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
              <Paragraph data-size="sm" style={{ margin: 0 }}>
                {svar.medlemmer.length} av {svar.antallOmrader} kommuner er medlem.
              </Paragraph>
              <span title="Klassen selv">Klassen:</span> <KunnskapsstatusTag status={svar.klassensKunnskapsstatus} />
              {svar.hjemmel && (hjemmelLenke
                ? <Link asChild><RouterLink to={hjemmelLenke}>kommuneloven § 2-1 første ledd</RouterLink></Link>
                : <Metatekst as="span" style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>{svar.hjemmel}</Metatekst>)}
              <span title="Hvert medlemskap">Medlemskapet:</span> <KunnskapsstatusTag status={svar.kunnskapsstatus} />
            </div>
            {svar.hull.length > 0 && (
              <Alert data-color="warning" data-size="sm" style={{ margin: '0 1rem 0.75rem' }}>
                <Details>
                  <Details.Summary>{svar.hull.length} kommuner er ikke medlem — ingen er valgt for dem</Details.Summary>
                  <Details.Content>
                    <ul style={{ margin: 0 }}>{svar.hull.map((h) => <li key={h}>{h}</li>)}</ul>
                  </Details.Content>
                </Details>
              </Alert>
            )}
            {svar.medlemmer.length > 0 && (
              <>
                <Table data-size="sm" data-density="compact" style={{ width: '100%' }}>
                  <Table.Head>
                    <Table.Row>
                      <Table.HeaderCell>Kommunenr.</Table.HeaderCell>
                      <Table.HeaderCell>Kommune (område)</Table.HeaderCell>
                      <Table.HeaderCell>Rettssubjekt</Table.HeaderCell>
                      <Table.HeaderCell>Premiss</Table.HeaderCell>
                    </Table.Row>
                  </Table.Head>
                  <Table.Body>
                    {paginering.visteRader.map((m) => (
                      <Table.Row key={m.virksomhetId}>
                        <Table.Cell>{m.kommunenummer}</Table.Cell>
                        <Table.Cell>
                          <Link asChild><RouterLink to={`/begreper/${m.omradeId}`}>{m.omradenavn}</RouterLink></Link>
                        </Table.Cell>
                        <Table.Cell>
                          <Link asChild><RouterLink to={`/virksomheter/${m.virksomhetId}`}>{m.navn}</RouterLink></Link>
                        </Table.Cell>
                        <Table.Cell>
                          {m.validert ? 'Territoriekant' : <Tag data-size="sm" data-color="info">Forslag</Tag>}
                        </Table.Cell>
                      </Table.Row>
                    ))}
                  </Table.Body>
                </Table>
                <Pagineringskontroll {...paginering} />
              </>
            )}
          </>
        )}
      </Card>
    </section>
  );
}
