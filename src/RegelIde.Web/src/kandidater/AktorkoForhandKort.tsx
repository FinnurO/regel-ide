import { useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Heading, Link, Paragraph, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { AktorkoDetalj, AktorkoForhandKort, AktorkoForhandsklassifisering, ForhandNode, ForhandRelasjon, Kunnskapsstatus } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { FORHAND_MERKE, FORHANDSTYPE_TEKST, MODELLVALG_SAK, SIKKERHET_TEKST, handlingstekst, juristlinje, relasjonstekst } from './aktorkoForhand';

/**
 * [Ny, issue #440, Johanns avgjørelse 2026-11-05] Forhåndsklassifiseringen for ett navn i aktørkøen (docs/09 §53): forslaget (type, relasjoner
 * med lenker, hjemmel eller medlemsregel med sitat, sikkerhet, juristens vurdering), det som må gjøres i eget steg, og «Godta forslaget».
 *
 * <p>Knappen skriver ingenting. Den fyller ut skjemaet som hører til handlingen (godkjenn, koble, ikke en aktør, generisk omtale); et menneske
 * ser det og trykker på skjemaets egen knapp. «Ta med» bekreftes her, med teksten om hva som skjer. Navn som venter på modellvalg har ingen
 * knapp.</p>
 */

/** Merket, med hele teksten synlig (som fasitmerket i docs/09 §46). */
export function ForhandMerke({ kilde }: { kilde: string }) {
  return (
    <Tag data-size="sm" data-color="info"
      title={`Fra ${kilde} (PR #462). Modellereren (KI-agent) klassifiserte, og juristutfordreren (KI-agent) vurderte i to runder. Ingen menneske har kontrollert forslaget.`}>
      {FORHAND_MERKE}
    </Tag>
  );
}

/** Merkene i køens rad: forslaget (`info`, ikke vurdert av noen), sikkerheten (`neutral`), og venter/utdatert (`warning`). */
export function ForhandRadmerker({ f }: { f: AktorkoForhandKort }) {
  return (
    <>
      {f.type === 'venter' ? (
        <Tag data-size="sm" data-color="warning" title="Navnet venter på modellvalget (Norge og nivåene). Ingen forslag før beslutningsnotatet.">
          Venter på {MODELLVALG_SAK}
        </Tag>
      ) : (
        <Tag data-size="sm" data-color="info" title={`${FORHAND_MERKE}.`}>Forhåndsklassifisert: {FORHANDSTYPE_TEKST[f.type].toLowerCase()}</Tag>
      )}
      {f.sikkerhet && <Tag data-size="sm" data-color="neutral">Sikkerhet {SIKKERHET_TEKST[f.sikkerhet]}</Tag>}
      {f.utdatert && (
        <Tag data-size="sm" data-color="warning" title={`Endret siden klassifiseringen: ${f.utdatert}.`}>Klassifisering kan være utdatert</Tag>
      )}
    </>
  );
}

function Nodelenke({ n }: { n: ForhandNode }) {
  if (n.ny) {
    return <>«{n.navn}» <Tag data-size="sm" data-color="neutral" title="Noden finnes ikke i basen ennå.">Ny node</Tag></>;
  }
  if (n.id && n.finnes) {
    const til = n.art === 'virksomhet' ? `/virksomheter/${n.id}` : `/begreper/${n.id}`;
    return <Link asChild><RouterLink to={til}>{n.navn}</RouterLink></Link>;
  }
  if (n.id) return <>{n.navn} <Metatekst as="span">(finnes ikke i basen nå)</Metatekst></>;
  return <>«{n.navn}»</>;
}

function Relasjonslinje({ r }: { r: ForhandRelasjon }) {
  return (
    <li style={{ marginBottom: '0.25rem' }}>
      <span>{relasjonstekst(r.relasjon)}</span>{' '}
      {r.til ? <Nodelenke n={r.til} /> : null}{' '}
      {r.skrivesAvHandlingen ? (
        <Tag data-size="sm" data-color="neutral" title="Koblingen gjør navnet til navneform på aktøren.">Skrives av koblingen</Tag>
      ) : (
        <Tag data-size="sm" data-color="neutral" title={r.egetSteg ?? undefined}>Eget steg</Tag>
      )}
      {r.merknad && <Metatekst>{r.merknad}</Metatekst>}
    </li>
  );
}

function Hjemmelcelle({ f }: { f: AktorkoForhandsklassifisering }) {
  const h = f.hjemmel;
  if (!h) {
    return (
      <>
        <span>{f.hjemmelStatus === 'mangler' ? 'Mangler' : 'Ikke relevant'}</span>
        {f.hjemmelMerknad && f.hjemmelMerknad !== 'ikke relevant' && <Metatekst>{f.hjemmelMerknad}</Metatekst>}
      </>
    );
  }
  return (
    <>
      <Paragraph data-size="sm" style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap', margin: 0 }}>
        {h.rettskildeId
          ? <Link asChild><RouterLink to={`/rettskilder/${h.rettskildeId}`}>{h.rettskilde ?? 'Rettskilden'}</RouterLink></Link>
          : <span>{h.rettskilde ?? 'Rettskilden er ikke funnet i basen'}</span>}
        <span title={h.eid}>{h.bestemmelse ?? h.eid}</span>
        <KunnskapsstatusTag status={(h.kunnskapsstatus as Kunnskapsstatus | null) ?? null} />
        <Tag data-size="sm" data-color="neutral"
          title={h.erRegelensForslag ? 'Samme bestemmelse som køens hjemmelforslag: godkjenningen kan lagre den.'
            : 'Ikke et av køens hjemmelforslag: godkjenningen kan ikke lagre den (eget steg).'}>
          {h.erRegelensForslag ? 'Regelens forslag' : 'Ikke regelens forslag'}
        </Tag>
      </Paragraph>
      {h.sitat ? (
        <Metatekst as="div" style={{ borderLeft: '3px solid var(--ds-color-accent-border-subtle)', paddingLeft: '0.6rem', marginTop: '0.25rem' }}>
          «{h.sitat}»{h.sitatKilde === 'basen' ? ' (teksten i basen; klassifiseringen har ikke sitatet)' : ''}
        </Metatekst>
      ) : (
        <Metatekst>Sitatet er ikke funnet: bestemmelsen står ikke i basen.</Metatekst>
      )}
      {f.hjemmelMerknad && <Metatekst style={{ marginTop: '0.2rem' }}>{f.hjemmelMerknad}</Metatekst>}
    </>
  );
}

export function ForhandKort({ f, detalj, onGodta, onGrupperingEndret }: {
  f: AktorkoForhandsklassifisering; detalj: AktorkoDetalj; onGodta: () => void; onGrupperingEndret: () => void;
}) {
  const [taMedApen, setTaMedApen] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [sender, setSender] = useState(false);
  const knapp = handlingstekst(f);
  const medlemsregel = f.relasjoner.some((r) => r.relasjon === 'medlemsregel');

  async function taMed() {
    if (!f.taMedINokkel) return;
    setSender(true);
    setFeil(null);
    try {
      await api.aktorkoTaMed(f.taMedINokkel, detalj.nokkel);
      onGrupperingEndret();
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : '«Ta med» feilet. Ingenting er lagret.');
    } finally {
      setSender(false);
    }
  }

  return (
    <Card style={{ padding: '0.75rem', marginBottom: '0.75rem' }} data-color="neutral">
      <Heading level={4} data-size="2xs" style={{ marginBottom: '0.3rem' }}>
        Forhåndsklassifisering: {FORHANDSTYPE_TEKST[f.typeforslag].toLowerCase()}
        {f.undertype ? ` (${f.undertype})` : ''}{f.nyNode ? ` «${f.nyNode}»` : ''}
      </Heading>
      <Paragraph data-size="sm" style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap', marginBottom: '0.4rem' }}>
        <ForhandMerke kilde={f.kilde} />
        {f.sikkerhet && <Tag data-size="sm" data-color="neutral">Sikkerhet {SIKKERHET_TEKST[f.sikkerhet]}</Tag>}
        {f.venterPaModellvalg && <Tag data-size="sm" data-color="warning">Venter på {MODELLVALG_SAK}</Tag>}
        {f.utdatert && <Tag data-size="sm" data-color="warning">Klassifisering kan være utdatert</Tag>}
        {f.pilot && <Tag data-size="sm" data-color="neutral" title="Et av pilotnavnene (#375).">Pilotnavn</Tag>}
      </Paragraph>
      {f.utdatert && <Metatekst style={{ marginBottom: '0.4rem' }}>Endret siden klassifiseringen: {f.utdatert}. Se over forekomstene før du godtar.</Metatekst>}

      <Table data-size="sm" data-density="compact" style={{ marginBottom: '0.5rem' }}>
        <Table.Body>
          <Table.Row>
            <Table.HeaderCell style={{ width: '11rem', verticalAlign: 'top' }}>Relasjoner</Table.HeaderCell>
            <Table.Cell>
              {f.relasjoner.length === 0 ? <Metatekst as="span">Ingen</Metatekst> : (
                <ul style={{ margin: 0, paddingLeft: '1.1rem' }}>{f.relasjoner.map((r, i) => <Relasjonslinje key={i} r={r} />)}</ul>
              )}
            </Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.HeaderCell style={{ verticalAlign: 'top' }}>{medlemsregel ? 'Hjemmel / medlemsregel' : 'Hjemmel'}</Table.HeaderCell>
            <Table.Cell><Hjemmelcelle f={f} /></Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.HeaderCell style={{ verticalAlign: 'top' }}>Begrunnelse</Table.HeaderCell>
            <Table.Cell>{f.begrunnelse}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.HeaderCell style={{ verticalAlign: 'top' }}>Juristens vurdering</Table.HeaderCell>
            <Table.Cell>
              <span>{juristlinje(f)}</span>
              {f.juristInnvending && <Metatekst>Innvendingen: {f.juristInnvending}</Metatekst>}
              {f.replikk && <Metatekst>Replikken: {f.replikk}</Metatekst>}
              {f.rettetEtterJuristen && <Metatekst>Forslaget er rettet etter innvendingen.</Metatekst>}
            </Table.Cell>
          </Table.Row>
        </Table.Body>
      </Table>

      {f.egneSteg.length > 0 && (
        <>
          <Heading level={5} data-size="2xs" style={{ marginBottom: '0.2rem' }}>Må gjøres i eget steg</Heading>
          <ul style={{ margin: '0 0 0.5rem 1.1rem', padding: 0 }}>
            {f.egneSteg.map((s) => <Metatekst as="li" key={s}>{s}</Metatekst>)}
          </ul>
        </>
      )}

      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.5rem' }}>{feil}</Alert>}
      {knapp === null ? (
        <Metatekst>Venter på {MODELLVALG_SAK}. Ingen knapp før modellvalget er tatt.</Metatekst>
      ) : f.handling === 'ta_med' && taMedApen ? (
        <div style={{ display: 'flex', gap: '0.6rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <Metatekst as="span">
            Dette skjer: «{detalj.navn}» ({detalj.forekomster} forekomster) tas med i navnet «{f.taMedINavn ?? f.taMedINokkel}», og behandles der.
          </Metatekst>
          <Button data-size="sm" disabled={sender} onClick={taMed}>{sender ? 'Lagrer …' : `Ta med i «${f.taMedINavn ?? f.taMedINokkel}»`}</Button>
          <Button data-size="sm" variant="tertiary" onClick={() => setTaMedApen(false)}>Avbryt</Button>
        </div>
      ) : (
        <div style={{ display: 'flex', gap: '0.6rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <Button data-size="sm" variant="secondary" onClick={() => (f.handling === 'ta_med' ? setTaMedApen(true) : onGodta())}>Godta forslaget</Button>
          <Metatekst as="span">{knapp} Ingenting lagres før du bekrefter.</Metatekst>
        </div>
      )}
    </Card>
  );
}
