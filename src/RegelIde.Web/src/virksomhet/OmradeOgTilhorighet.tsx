import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Card, Details, Heading, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { StrukturkantDto, StrukturnodeDto, TilhorighetKandidatDto, TilhorighetSvarDto, VirksomhetTilhorighetDto } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { OmradetypeTag } from '../begrep/Omraderegister';
import { StrukturkantTabell, nodeHref } from '../strukturkant/StrukturkantTabell';
import { KUNNSKAPSSTATUS_VISNING, KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';

/**
 * [Ny, issue #390 AK 1, Johann 2026-10-16 (Alta-eksempelet)] «Område og tilhørighet» på siden til en aktør med eget territorium (kommunen):
 * fylke, tingrett og rettssted, lagsogn, lagdømme, lagmannsrett, statsforvalter, helseregion, regionalt helseforetak, valgkrets og klasser.
 * Før #390 sto dette bare på områdesiden — tingretten har domsmyndighet over TERRITORIET, ikke over kommunen som juridisk person (docs/33
 * §4.1), og den som åpnet Alta kommune, så ikke hvilken tingrett kommunen hører til.
 *
 * Svaret er beregnet (`GET /api/virksomheter/{id}/omrade-tilhorighet`), ikke lagret (#345). Hvert svar viser (docs/09 §47):
 * <ul>
 *   <li>noden med lenke, og <b>kunnskapsstatusen</b> med `KunnskapsstatusTag` (§35). Den er aldri sterkere enn det svakeste premisset
 *       (docs/33 §4.6): et svar som hviler på en kant som ikke er vurdert, er «Uavklart», ikke «Avledet».</li>
 *   <li>«Svakeste premiss:» med kanten og dens status, som `Metatekst` — leseren skal se HVA som gjør svaret svakt.</li>
 *   <li>«Kjeden» i en lukket `Details`: premissene i rekkefølge, i den delte `StrukturkantTabell` (§31) — ingen egen kanttabell.</li>
 *   <li>«Forslag» (`info`) når et premiss ikke er godkjent; «Ikke entydig» (`warning`) og «Mangler» (`neutral`) som tilhørigheten på
 *       områdesiden (§32). Ingen kandidat velges.</li>
 * </ul>
 */
const RUBRIKK_VISNING: Record<string, string> = {
  fylke: 'Ligger i fylke',
  tingrett: 'Tingrett (domssogn)',
  rettssted: 'Tingrettens rettssted',
  lagsogn: 'Lagsogn',
  'lagdømme': 'Lagdømme',
  lagmannsrett: 'Lagmannsrett',
  statsforvalter: 'Statsforvalter',
  helseregion: 'Helseregion',
  RHF: 'Regionalt helseforetak',
  valgkrets: 'Valgkrets',
};

function Rubrikkstatus({ s }: { s: TilhorighetSvarDto }) {
  if (s.status === 'ikke_entydig') return <Tag data-color="warning" data-size="sm" title="Flere kandidater — ingen er valgt.">Ikke entydig</Tag>;
  if (s.status === 'mangler') return <Tag data-color="neutral" data-size="sm" title="Ingen kant i registeret gir et svar.">Mangler</Tag>;
  return null;
}

function Kandidat({ k, premisser }: { k: TilhorighetKandidatDto; premisser: Map<string, StrukturkantDto> }) {
  const kjede = k.premisser.map((id) => premisser.get(id)).filter((p): p is StrukturkantDto => !!p);
  const svakeste = k.svakestePremissId ? premisser.get(k.svakestePremissId) : undefined;
  return (
    <div style={{ marginBottom: '0.35rem' }}>
      <span style={{ display: 'inline-flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <NodeLenke node={k.node} />
        <KunnskapsstatusTag status={k.kunnskapsstatus} premisser={k.avledet ? k.premisser : null} />
        {k.forslag && (
          <Tag data-color="info" data-size="sm" title="Minst ett premiss er ikke godkjent ennå (forslag eller faglig vurdert).">Forslag</Tag>
        )}
      </span>
      {k.avledet && svakeste && (
        <Metatekst style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Svakeste premiss: «{svakeste.visningstekst}» ({k.svakestePremissStatus ? KUNNSKAPSSTATUS_VISNING[k.svakestePremissStatus].tekst : 'Ikke vurdert'})
        </Metatekst>
      )}
      {!k.avledet && (
        <Metatekst style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Registrert direkte på aktøren (lagret utsagn, ikke avledet).
        </Metatekst>
      )}
      <Details data-size="sm">
        <Details.Summary>Kjeden ({kjede.length} {kjede.length === 1 ? 'premiss' : 'premisser'})</Details.Summary>
        <Details.Content>
          <StrukturkantTabell kanter={kjede} tomTekst="Ingen premisser." />
        </Details.Content>
      </Details>
    </div>
  );
}

function NodeLenke({ node }: { node: StrukturnodeDto }) {
  return <Link asChild><RouterLink to={nodeHref(node)}>{node.navn}</RouterLink></Link>;
}

export function OmradeOgTilhorighet({ virksomhetId }: { virksomhetId: string }) {
  const [svar, setSvar] = useState<VirksomhetTilhorighetDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  useEffect(() => {
    let avbrutt = false;
    setSvar(null);
    setFeil(null);
    api.hentVirksomhetTilhorighet(virksomhetId)
      .then((s) => { if (!avbrutt) setSvar(s); })
      .catch((e) => { if (!avbrutt) setFeil(e instanceof ApiError ? e.message : 'Kunne ikke regne ut tilhørigheten.'); });
    return () => { avbrutt = true; };
  }, [virksomhetId]);

  const premisser = new Map((svar?.premisser ?? []).map((p) => [p.id, p]));

  return (
    <section style={{ marginBottom: '2rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>Område og tilhørighet</Heading>
      <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Det aktøren hører til gjennom territoriet sitt. Tingretten har domsmyndighet over området, ikke over kommunen som juridisk person,
        så svaret går via territoriet. Ingenting her er lagret: hvert svar regnes ut fra kantene, og det får aldri sterkere status enn
        det svakeste premisset i kjeden.
      </Metatekst>
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}
      {!svar && !feil && <Spinner aria-label="Regner ut tilhørigheten …" data-size="sm" />}
      {svar && !svar.territorium && (
        <Card style={{ padding: '1rem' }}>
          <Paragraph style={{ margin: 0 }}>{svar.hull ?? 'Aktøren har ikke eget territorium i områderegisteret.'}</Paragraph>
        </Card>
      )}
      {svar?.territorium && (
        <>
          <Paragraph data-size="sm" style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
            Territoriet:{' '}
            <Link asChild><RouterLink to={`/begreper/${svar.territorium.id}`}>{svar.territorium.navn}</RouterLink></Link>
            {svar.territorium.omradetype && <OmradetypeTag omradetype={svar.territorium.omradetype} omradekode={svar.territorium.omradekode} />}
            {svar.territoriekantId && premisser.get(svar.territoriekantId) && (
              <KunnskapsstatusTag status={premisser.get(svar.territoriekantId)!.kunnskapsstatus} />
            )}
          </Paragraph>
          <Card style={{ padding: 0, overflow: 'hidden', marginBottom: '1.5rem' }}>
            <Table data-density="compact">
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell style={{ width: '13rem' }}>Hører til</Table.HeaderCell>
                  <Table.HeaderCell>Svar, status og kjede</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {svar.svar.map((s) => (
                  <Table.Row key={s.rubrikk}>
                    <Table.HeaderCell>{RUBRIKK_VISNING[s.rubrikk] ?? s.rubrikk}</Table.HeaderCell>
                    <Table.Cell>
                      {s.kandidater.map((k) => <Kandidat key={k.node.id} k={k} premisser={premisser} />)}
                      {s.kandidater.length === 0 && <span style={{ marginRight: '0.4rem' }}>—</span>}
                      <Rubrikkstatus s={s} />
                      {s.hull && (
                        <Metatekst style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>{s.hull}</Metatekst>
                      )}
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </Card>

          <Heading level={3} data-size="xs" style={{ marginBottom: '0.5rem' }}>Medlem av klasser</Heading>
          <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
            Klassen «kommune» er utledet av områderegisteret med territoriekanten som premiss (#384). Et medlemskap registrert direkte på
            aktøren er et lagret utsagn med sin egen status.
          </Metatekst>
          <Card style={{ padding: svar.klasser.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
            {svar.klasser.length === 0 ? (
              <Paragraph style={{ margin: 0 }}>Ingen klassemedlemskap er registrert eller utledet.</Paragraph>
            ) : (
              <Table data-density="compact">
                <Table.Body>
                  {svar.klasser.map((k) => (
                    <Table.Row key={k.node.id}>
                      <Table.Cell><Kandidat k={k} premisser={premisser} /></Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
            )}
          </Card>
        </>
      )}
    </section>
  );
}
