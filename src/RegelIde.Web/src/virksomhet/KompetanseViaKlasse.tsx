import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Card, Details, Heading, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { KompetanseViaKlasseDto, KompetanseViaKlasseTreffDto, StrukturkantDto } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KanttekstMedLenker, StrukturkantTabell, nodeHref } from '../strukturkant/StrukturkantTabell';
import { KUNNSKAPSSTATUS_VISNING, KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';

/**
 * [Ny, issue #418, Johanns avgjørelser (a) og (b) 2026-10-29] «Kompetanse via klasse eller rolle» på VirksomhetDetalj, under den direkte
 * kompetansen. K kan ha en klasse som innehaver, lest distributivt («Kommunen kan gi lokal forskrift» gjelder hver kommune), og klassen kan ha
 * en rolle («lokal barnehagemyndighet»). Svaret er beregnet (`GET /api/virksomheter/{id}/kompetanse-via-klasse`), ikke lagret (#345).
 *
 * Samme mønster som «Område og tilhørighet» (docs/09 §47): hvert treff har `KunnskapsstatusTag` som aldri er sterkere enn det svakeste
 * premisset (docs/33 §4.6), «Forslag» (`info`) når et premiss ikke er godkjent, «Svakeste premiss:» som `Metatekst`, og kjeden i en lukket
 * `Details` med den delte `StrukturkantTabell` (§31). Ingen egen kanttabell.
 */
function Treff({ t, premisser }: { t: KompetanseViaKlasseTreffDto; premisser: Map<string, StrukturkantDto> }) {
  const kjede = t.premisser.map((id) => premisser.get(id)).filter((p): p is StrukturkantDto => !!p);
  const svakeste = t.svakestePremissId ? premisser.get(t.svakestePremissId) : undefined;
  const noder = [t.kant.fra, ...(t.kant.til ? [t.kant.til] : [])];
  return (
    <Table.Row>
      <Table.Cell>
        <KanttekstMedLenker tekst={t.kant.visningstekst} noder={noder} />
      </Table.Cell>
      <Table.Cell>
        {t.via.map((n, i) => (
          <span key={n.id}>
            {i > 0 && ' → '}
            <Link asChild><RouterLink to={nodeHref(n)}>{n.navn}</RouterLink></Link>
            {n.nodetype && <Metatekst style={{ marginLeft: '0.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>({n.nodetype})</Metatekst>}
          </span>
        ))}
      </Table.Cell>
      <Table.Cell>
        <span style={{ display: 'inline-flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <KunnskapsstatusTag status={t.kunnskapsstatus} premisser={t.premisser} />
          {t.forslag && (
            <Tag data-color="info" data-size="sm" title="Minst ett premiss er ikke godkjent ennå (forslag eller faglig vurdert).">Forslag</Tag>
          )}
        </span>
        {svakeste && (
          <Metatekst style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>
            Svakeste premiss: «{svakeste.visningstekst}» ({t.svakestePremissStatus ? KUNNSKAPSSTATUS_VISNING[t.svakestePremissStatus].tekst : 'Ikke vurdert'})
          </Metatekst>
        )}
        <Details data-size="sm">
          <Details.Summary>Kjeden ({kjede.length} {kjede.length === 1 ? 'premiss' : 'premisser'})</Details.Summary>
          <Details.Content>
            <StrukturkantTabell kanter={kjede} tomTekst="Ingen premisser." />
          </Details.Content>
        </Details>
      </Table.Cell>
    </Table.Row>
  );
}

function Treffliste({ treff, premisser, tomTekst }: { treff: KompetanseViaKlasseTreffDto[]; premisser: Map<string, StrukturkantDto>; tomTekst: string }) {
  return (
    <Card style={{ padding: treff.length > 0 ? 0 : '1rem', overflow: 'hidden', marginBottom: '1rem' }}>
      {treff.length === 0 ? (
        <Paragraph style={{ margin: 0 }}>{tomTekst}</Paragraph>
      ) : (
        <Table data-density="compact">
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Utsagn</Table.HeaderCell>
              <Table.HeaderCell style={{ width: '14rem' }}>Via</Table.HeaderCell>
              <Table.HeaderCell style={{ width: '22rem' }}>Status og kjede</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {treff.map((t) => <Treff key={`${t.kant.id}-${t.retning}`} t={t} premisser={premisser} />)}
          </Table.Body>
        </Table>
      )}
    </Card>
  );
}

export function KompetanseViaKlasse({ virksomhetId }: { virksomhetId: string }) {
  const [svar, setSvar] = useState<KompetanseViaKlasseDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  useEffect(() => {
    let avbrutt = false;
    setSvar(null);
    setFeil(null);
    api.hentKompetanseViaKlasse(virksomhetId)
      .then((s) => { if (!avbrutt) setSvar(s); })
      .catch((e) => { if (!avbrutt) setFeil(e instanceof ApiError ? e.message : 'Kunne ikke regne ut kompetansen via klasse eller rolle.'); });
    return () => { avbrutt = true; };
  }, [virksomhetId]);

  const premisser = new Map((svar?.premisser ?? []).map((p) => [p.id, p]));
  return (
    <>
      <Heading level={4} data-size="2xs" style={{ marginBottom: '0.35rem' }}>Via klasse eller rolle (avledet)</Heading>
      <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Kompetanse som står på en klasse virksomheten er medlem av, eller på en rolle den eller klassen har. «Kommunen kan …» i en lov som
        gjelder alle kommuner, gir hver kommune kompetansen (#418). Ingenting her er lagret, og statusen er aldri sterkere enn det svakeste
        premisset: medlemskapet, rolleinnehavet eller kompetansen selv.
      </Metatekst>
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}
      {!svar && !feil && <Spinner aria-label="Regner ut kompetansen via klasse eller rolle …" data-size="sm" />}
      {svar && (
        <>
          <Treffliste treff={svar.treff.filter((t) => t.retning === 'har')} premisser={premisser}
            tomTekst="Ingen kompetanse via klasse eller rolle." />
          {svar.treff.some((t) => t.retning === 'overfor') && (
            <>
              <Heading level={4} data-size="2xs" style={{ marginBottom: '0.35rem' }}>Andres kompetanse overfor klassen eller rollen</Heading>
              <Treffliste treff={svar.treff.filter((t) => t.retning === 'overfor')} premisser={premisser} tomTekst="" />
            </>
          )}
          {svar.hull.map((h) => (
            <Metatekst key={h} style={{ display: 'block', marginBottom: '0.5rem', color: 'var(--ds-color-neutral-text-subtle)' }}>{h}</Metatekst>
          ))}
        </>
      )}
    </>
  );
}
