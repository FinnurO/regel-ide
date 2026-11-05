import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Card, Heading, Link, Paragraph, Spinner, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { KlassegrunnlagDto, KlassegrunnlagKildeDto } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { GodkjenningTag, KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { eidKort } from '../virksomhet/Aktoridentitet';

/**
 * [Ny, issue #442, Johanns avgjørelse 2026-11-05] «Grunnlag for klassen» på begrepssiden (fanen Grunndata, docs/09 §53). Regelen er «Klasser må ha
 * minst hjemmel eller medlemsregel, og kan ha begge»: hjemmelen og medlemsregelen står hver på sin rad, og hullet (`warning`-`Alert`) vises bare
 * når begge mangler. Kommune og fylkeskommune har begge (kommuneloven § 2-1 første ledd og en medlemsregel); departement har bare
 * medlemsregelen. Forankringen (Grunnloven) står i begrunnelsen, aldri som hjemmel. Følger §16: `Card` er alltid rendret, `null` gir `Spinner`.
 */
export function KlasseGrunnlag({ klasseId }: { klasseId: string }) {
  const [svar, setSvar] = useState<KlassegrunnlagDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    let avbrutt = false;
    setSvar(null);
    setFeil(null);
    api.hentKlassegrunnlag(klasseId)
      .then((s) => { if (!avbrutt) setSvar(s); })
      .catch((e) => { if (!avbrutt) setFeil(e instanceof ApiError ? e.message : 'Kunne ikke hente grunnlaget for klassen.'); });
    return () => { avbrutt = true; };
  }, [klasseId]);

  return (
    <section style={{ marginBottom: '1.5rem' }}>
      <Heading level={3} data-size="xs" style={{ marginBottom: '0.5rem' }}>Grunnlag for klassen</Heading>
      <Metatekst style={{ marginBottom: '0.5rem' }}>En klasse skal ha minst hjemmel eller medlemsregel, og kan ha begge.</Metatekst>
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.5rem' }}>{feil}</Alert>}
      <Card style={{ padding: '0.75rem 1rem' }}>
        {svar === null ? (
          feil ? <Paragraph style={{ margin: 0 }}>Grunnlaget er ukjent (se feilen over).</Paragraph>
            : <Spinner aria-label="Henter grunnlaget …" data-size="sm" />
        ) : (
          <>
            {svar.status === 'mangler' && (
              <Alert data-color="warning" data-size="sm" style={{ marginBottom: '0.5rem' }}>Klassen mangler hjemmel og medlemsregel</Alert>
            )}
            <Table data-size="sm">
              <Table.Body>
                <Table.Row>
                  <Table.HeaderCell scope="row" style={{ width: '9rem' }}>Hjemmel</Table.HeaderCell>
                  <Table.Cell>
                    <span style={{ display: 'inline-flex', gap: '0.3rem', flexWrap: 'wrap', alignItems: 'center' }}>
                      {svar.hjemmel ? <Kilde kilde={svar.hjemmel} /> : svar.kildeUtenforKorpus
                        ? <span>{svar.kildeUtenforKorpus}</span>
                        : <Metatekst as="span">Ingen{svar.medlemsregel ? ' (klassen har medlemsregel)' : ''}</Metatekst>}
                      {(svar.hjemmel || svar.kildeUtenforKorpus) && <KunnskapsstatusTag status={svar.kunnskapsstatus} />}
                      {svar.godkjenning && <GodkjenningTag status={svar.godkjenning} oppdagelsesKilde="manuell" />}
                    </span>
                  </Table.Cell>
                </Table.Row>
                <Table.Row>
                  <Table.HeaderCell scope="row">Medlemsregel</Table.HeaderCell>
                  <Table.Cell>
                    {svar.medlemsregel ? (
                      <>
                        <Paragraph data-size="sm" style={{ margin: 0 }}>{svar.medlemsregel.tekst}</Paragraph>
                        <Metatekst as="span" style={{ display: 'inline-flex', gap: '0.3rem', flexWrap: 'wrap' }}>
                          Kilde: {svar.regelKilder.length > 0
                            ? svar.regelKilder.map((k) => <Kilde key={k.eid} kilde={k} />)
                            : svar.medlemsregel.kilde}
                        </Metatekst>
                      </>
                    ) : <Metatekst as="span">Ingen. Medlemskapet er registrert med medlemskanter.</Metatekst>}
                  </Table.Cell>
                </Table.Row>
              </Table.Body>
            </Table>
            {svar.begrunnelse && <Metatekst style={{ marginTop: '0.5rem' }}>{svar.begrunnelse}</Metatekst>}
          </>
        )}
      </Card>
    </section>
  );
}

function Kilde({ kilde }: { kilde: KlassegrunnlagKildeDto }) {
  const tekst = `${kilde.tittel ?? 'Bestemmelsen'} ${eidKort(kilde.eid)}`;
  return kilde.rettskildeId
    ? <Link asChild><RouterLink to={rettskildeLenkeForId(kilde.rettskildeId, kilde.eid)}>{tekst}</RouterLink></Link>
    : <span>{tekst}</span>;
}
