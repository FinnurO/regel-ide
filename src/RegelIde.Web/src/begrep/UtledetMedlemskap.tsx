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
/** [Ny, issue #442] Overskrift, forklaring og kolonner per medlemsregel. Kommunens tekst er uendret. */
const REGELTEKST: Record<string, { tittel: string; forklaring: string; omrade: string | null }> = {
  omraderegister_kommune: {
    tittel: 'Medlemmer (utledet av områderegisteret)',
    forklaring: 'Medlemskapet registreres ikke for hånd. Det regnes ut hver gang: kommunene i Kartverkets inndeling, med virksomheten som har '
      + 'ansvarsområde til kommunen og samme kommunenummer. Premisset for hvert medlemskap er den territoriekanten.',
    omrade: 'kommuner',
  },
  omraderegister_fylkeskommune: {
    tittel: 'Medlemmer (utledet av territoriekantene)',
    forklaring: 'Medlemskapet registreres ikke for hånd. Det regnes ut hver gang: for hvert fylke unntatt Oslo, fylkeskommunen (rettssubjekt) med '
      + 'territoriekant til fylket etter kommuneloven § 3-1 første ledd. Statsforvalteren har også ansvarsområde til fylket, men er ikke medlem.',
    omrade: 'fylker',
  },
  organiseringsvedtak_departement: {
    tittel: 'Medlemmer (utledet av organiseringsvedtakene)',
    forklaring: 'Medlemskapet regnes ut hver gang: levende aktører som et organiseringsvedtak oppretter (R oppretter fra Kongen i statsråd), eller som '
      + 'har en medlemskant med navnevedtaket eller en kongelig resolusjon som premiss. Det siste er en fortolkning: vedtaket forutsetter '
      + 'departementet, men oppretter det ikke.',
    omrade: null,
  },
};

const PREMISSART_TEKST: Record<string, string> = {
  territoriekant: 'Territoriekant',
  oppretterkant: 'Opprettelsesvedtak',
  medlemskant: 'Medlemskant',
};

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
  const tekst = REGELTEKST[svar?.regel ?? ''] ?? REGELTEKST.omraderegister_kommune;
  const medOmrade = svar ? svar.antallOmrader !== null : true;

  return (
    <section style={{ marginBottom: '2rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
        {tekst.tittel}
      </Heading>
      <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '-0.5rem', marginBottom: '0.75rem' }}>
        {tekst.forklaring}
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
                {svar.antallOmrader !== null
                  ? `${svar.medlemmer.length} av ${svar.antallOmrader} ${tekst.omrade ?? 'områder'} har et medlem.`
                  : `${svar.medlemmer.length} medlemmer.`}
              </Paragraph>
              <span title="Klassen selv">Klassen:</span> <KunnskapsstatusTag status={svar.klassensKunnskapsstatus} />
              {/* [ENDRET, issue #442] Lenketeksten var fast «kommuneloven § 2-1 første ledd». Kommune og fylkeskommune har samme hjemmel; klassen
                * departement har ingen hjemmel, bare medlemsregel (grunnlaget står i Grunndata, KlasseGrunnlag). */}
              {svar.hjemmel && (hjemmelLenke
                ? <Link asChild><RouterLink to={hjemmelLenke}>{svar.hjemmel.endsWith('/2018/06/22/83/nor/§2-1/ledd-1') ? 'kommuneloven § 2-1 første ledd' : 'hjemmelen'}</RouterLink></Link>
                : <Metatekst as="span" style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>{svar.hjemmel}</Metatekst>)}
              <span title="Hvert medlemskap">Medlemskapet:</span> <KunnskapsstatusTag status={svar.kunnskapsstatus} />
            </div>
            {svar.unntatt.length > 0 && (
              <Metatekst style={{ margin: '0 1rem 0.75rem' }}>Unntatt i loven: {svar.unntatt.join(' ')}</Metatekst>
            )}
            {svar.hull.length > 0 && (
              <Alert data-color="warning" data-size="sm" style={{ margin: '0 1rem 0.75rem' }}>
                <Details>
                  <Details.Summary>
                    {svar.hull.length} {medOmrade ? `${tekst.omrade ?? 'områder'} uten medlem` : 'aktører er ikke medlem'} — ingen er valgt for dem
                  </Details.Summary>
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
                      {medOmrade && <Table.HeaderCell>{svar.omradetype === 'fylke' ? 'Fylkesnr.' : 'Kommunenr.'}</Table.HeaderCell>}
                      {medOmrade && <Table.HeaderCell>{svar.omradetype === 'fylke' ? 'Fylke (område)' : 'Kommune (område)'}</Table.HeaderCell>}
                      <Table.HeaderCell>{medOmrade ? 'Rettssubjekt' : 'Aktør'}</Table.HeaderCell>
                      <Table.HeaderCell>Premiss</Table.HeaderCell>
                      <Table.HeaderCell>Medlemskapet</Table.HeaderCell>
                    </Table.Row>
                  </Table.Head>
                  <Table.Body>
                    {paginering.visteRader.map((m) => (
                      <Table.Row key={m.virksomhetId}>
                        {medOmrade && <Table.Cell>{m.omradekode}</Table.Cell>}
                        {medOmrade && (
                          <Table.Cell>
                            {m.omradeId ? <Link asChild><RouterLink to={`/begreper/${m.omradeId}`}>{m.omradenavn}</RouterLink></Link> : m.omradenavn}
                          </Table.Cell>
                        )}
                        <Table.Cell>
                          <Link asChild><RouterLink to={`/virksomheter/${m.virksomhetId}`}>{m.navn}</RouterLink></Link>
                        </Table.Cell>
                        <Table.Cell>
                          <span style={{ display: 'inline-flex', gap: '0.3rem', flexWrap: 'wrap', alignItems: 'center' }}>
                            {PREMISSART_TEKST[m.premissart] ?? m.premissart}
                            {!m.validert && <Tag data-size="sm" data-color="info">Forslag</Tag>}
                          </span>
                        </Table.Cell>
                        <Table.Cell><KunnskapsstatusTag status={m.kunnskapsstatus} /></Table.Cell>
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
