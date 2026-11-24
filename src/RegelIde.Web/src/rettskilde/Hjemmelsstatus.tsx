import { Link as RouterLink } from 'react-router';
import { Card, Heading, Link, Paragraph, Table, Tag } from '@digdir/designsystemet-react';
import type { HjemmelsstatusDto, HjemmelsstatusKildeDto, HjemmelsstatusRadDto } from '../api/types';
import { rettskildeLenkeForId } from '../api/eidLenker';
import { Metatekst } from '../entitet/Metatekst';
import { KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { BEVISTRINN, DEKNING, ROLLE, ROLLEREGEL, SAMLET_STATUS, STATUS, UKJENT_GRUNN, kildetekst, norskDato, samletTekst } from './hjemmelsstatusTekst';

/**
 * [Ny, issue #472 PR 1, Johanns avgjørelser 2026-11-18] Hjemmelsstatusen på rettskildesiden (docs/09 §56): status, rolle og bevis per hjemmel,
 * og den samlede statusen etter svakeste premiss. Den samlede statusen regnes ut av API-et og lagres ikke.
 * <p>
 * Fargerollene følger docs/09 §15/§35: statusene og rollene er klassifiseringer → `neutral`; kunnskapsstatus med sitt eget merke (uavklart er
 * `warning`). Ingen `danger` og ingen merkelapp på linje 2 under tittelen: Lovdata viser forskriften som gjeldende, og en opphevet hjemmel er en
 * lesning av premissene, ikke en statusendring (CLAUDE.md §9). [ENDRET, #472 PR 2] Funnet «Hjemmel opphevet» lenkes fra den samlede statusen,
 * og «(mulig bortfalt)» står først når funnet har «Ny hjemmel» vurdert (premissporten).
 */
export function Hjemmelsstatus({ data }: { data: HjemmelsstatusDto | null }) {
  if (!data) return null;
  if (!data.beregnet) {
    return (
      <section style={{ marginBottom: '1.5rem' }}>
        <Heading level={2} data-size="sm">Hjemmelsstatus</Heading>
        <Metatekst>Ikke beregnet: maskinregelen for hjemmelsstatus (#472) er ikke kjørt med skriving for denne rettskilden.</Metatekst>
      </section>
    );
  }
  const samlet = samletTekst(data.samletStatus, data.muligBortfaltVises);
  const svakest = data.hjemler.find((h) => h.rettskildeHjemmelId === data.svakestePremissId);
  return (
    <section style={{ marginBottom: '1.5rem' }}>
      <Heading level={2} data-size="sm">Hjemmelsstatus</Heading>
      <Metatekst>
        Lest av maskinregelen (#472, {data.regelversjon}
        {data.beregnetTidspunkt ? `, ${norskDato(data.beregnetTidspunkt)}` : ''}). Lovdata viser forskriften som gjeldende; dette er en lesning av
        hjemlene, ikke en statusendring.
      </Metatekst>
      <Paragraph style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', marginTop: '0.5rem' }}>
        <span>Samlet:</span>
        <Tag data-size="sm" data-color="neutral" title={samlet.forklaring}>{samlet.tekst}</Tag>
        <KunnskapsstatusTag status={data.samletKunnskapsstatus} />
        {data.gjenstandslos && (
          <Tag data-size="sm" data-color="neutral" title="Gjenstanden for vedtaket (loven det handler om) er opphevet.">Gjenstandsløs</Tag>
        )}
        {data.funnId && (
          <Link asChild data-size="sm"><RouterLink to={`/funn/${data.funnId}`}>Funn til vurdering</RouterLink></Link>
        )}
      </Paragraph>
      <Metatekst>{samlet.forklaring}</Metatekst>
      {svakest && (
        <Metatekst>
          Svakeste premiss: {hjemmeltekst(svakest)} ({STATUS[svakest.effektivStatus] ?? svakest.effektivStatus})
        </Metatekst>
      )}
      {data.rentEndringsvedtak && (
        <Metatekst>Rent endringsvedtak (#446): virkningen er uttømt, og statusen telles for seg.</Metatekst>
      )}
      <Card style={{ padding: 0, overflow: 'hidden', marginTop: '0.75rem' }}>
        <Table>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Hjemmel</Table.HeaderCell>
              <Table.HeaderCell>Rolle</Table.HeaderCell>
              <Table.HeaderCell>Status og bevis</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {data.hjemler.map((h) => <Rad key={h.rettskildeHjemmelId} h={h} />)}
          </Table.Body>
        </Table>
      </Card>
    </section>
  );
}

function hjemmeltekst(h: HjemmelsstatusRadDto): string {
  return [h.malTittel && h.malRettskildeId ? h.malTittel : h.malReferanse, h.bestemmelse].filter(Boolean).join(' ');
}

function Kildelenke({ k }: { k: HjemmelsstatusKildeDto }) {
  const tekst = kildetekst(k);
  if (k.rettskildeId && k.eid && !k.fraKunngjort) {
    return <Link asChild><RouterLink to={rettskildeLenkeForId(k.rettskildeId, k.eid)}>{tekst}</RouterLink></Link>;
  }
  if (k.rettskildeId) return <Link asChild><RouterLink to={`/rettskilder/${k.rettskildeId}`}>{tekst}</RouterLink></Link>;
  return <>{tekst}</>;
}

function Rad({ h }: { h: HjemmelsstatusRadDto }) {
  const rolle = h.rolle ? ROLLE[h.rolle] : null;
  const arvet = h.arvetSamletStatus && h.effektivStatus !== h.status;
  return (
    <Table.Row>
      <Table.Cell style={{ verticalAlign: 'top' }}>
        {h.malRettskildeId
          ? <Link asChild><RouterLink to={`/rettskilder/${h.malRettskildeId}`}>{hjemmeltekst(h)}</RouterLink></Link>
          : <span>{hjemmeltekst(h)}</span>}
        {!h.malRettskildeId && <Metatekst>Bare referanse i korpuset (ikke importert).</Metatekst>}
        {!h.teller && <Metatekst>Teller ikke i den samlede statusen.</Metatekst>}
      </Table.Cell>
      <Table.Cell style={{ verticalAlign: 'top' }}>
        {rolle
          ? <Tag data-size="sm" data-color="neutral" title={`${rolle.forklaring} Regel: ${ROLLEREGEL[h.rolleRegel ?? ''] ?? h.rolleRegel}.`}>{rolle.tekst}</Tag>
          : <Tag data-size="sm" data-color="neutral" variant="outline" title="Rollen er ikke vurdert: ingen navngitt regel ga den.">Ikke vurdert</Tag>}
      </Table.Cell>
      <Table.Cell style={{ verticalAlign: 'top' }}>
        <Paragraph style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <Tag data-size="sm" data-color="neutral">{STATUS[h.status] ?? h.status}</Tag>
          <KunnskapsstatusTag status={h.kunnskapsstatus} />
        </Paragraph>
        {arvet && (
          <Metatekst>
            Arvet fra hjemmelsforskriften: {SAMLET_STATUS[h.arvetSamletStatus!]?.tekst ?? h.arvetSamletStatus} (regnes som «{STATUS[h.effektivStatus] ?? h.effektivStatus}»).
          </Metatekst>
        )}
        {h.merknad && <Metatekst>{h.merknad}</Metatekst>}
        {h.ukjentGrunn && <Metatekst>Ukjent fordi {UKJENT_GRUNN[h.ukjentGrunn] ?? h.ukjentGrunn}.</Metatekst>}
        {h.fulgtEli && <Metatekst>Endringslov: statusen gjelder loven den endrer ({h.fulgtEli.replace('https://lovdata.no/eli/', '')}).</Metatekst>}
        {h.opphevetAv && (
          <Metatekst>
            Opphevet av <Kildelenke k={h.opphevetAv} />{h.opphevetDato ? ` (i kraft ${norskDato(h.opphevetDato)})` : ''}: «{h.opphevetAv.sitat}»
            {' '}— {BEVISTRINN[h.bevistrinn ?? ''] ?? h.bevistrinn}.
          </Metatekst>
        )}
        {h.videreforing && (
          <Metatekst>
            Videreført av <Kildelenke k={h.videreforing.kilde} /> ({h.videreforing.form}): «{h.videreforing.kilde.sitat}»
            {h.videreforing.forbehold ? ` Forbehold: «${h.videreforing.forbehold}».` : ''}
            {h.videreforing.tidsfrist ? ` Frist: ${norskDato(h.videreforing.tidsfrist)}.` : ''}
            {h.videreforing.dekning ? ` ${DEKNING[h.videreforing.dekning] ?? h.videreforing.dekning}` : ''}
          </Metatekst>
        )}
        {h.erstattetAv && <Metatekst>Erstattet av <Kildelenke k={h.erstattetAv} /> (tildeler kompetanse i samme vedtak).</Metatekst>}
        {h.premissmangel && (
          <Paragraph style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
            <Tag data-size="sm" data-color="warning" title={h.premissmangel}>Ikraftsetting ikke kontrollert</Tag>
            <Metatekst as="span">{h.premissmangel}</Metatekst>
          </Paragraph>
        )}
      </Table.Cell>
    </Table.Row>
  );
}
