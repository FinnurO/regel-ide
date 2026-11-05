import { useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Details, Fieldset, Heading, Link, Paragraph, Radio, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { AktorHjemmelDto, AktorHjemmelNavneformDto, HjemmelArt } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { StrukturkantTabell } from '../strukturkant/StrukturkantTabell';
import { GodkjenningTag, KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { DisposisjonstypeTagger } from '../rettskilde/Disposisjonstype';
import { eidKort } from './Aktoridentitet';
import { NavneformgrunnTag } from './Navneformgrunn';

/**
 * [Ny, issue #441, Johanns avgjørelser 2026-10-30 og 2026-11-01] Kortet «Hjemmel for aktøren», øverst og uthevet på aktørsiden (docs/09 §52).
 * Svarer på «hva er hjemmelen for at aktøren finnes?» (S1). Regelen står i `AktorsideTjeneste.HjemmelAsync`: R oppretter først, så en
 * navneform med `hjemmel_art`, så den gjeldende navneformen i en organiseringskilde. Ellers hullet — aldri en gjetning.
 */

const KILDE_TEKST: Record<string, string> = {
  oppretter_kant: 'Opprettelsesvedtak',
  hjemmel_art: 'Navneform med hjemmel for aktøren',
  disposisjonstype: 'Navneformens hjemmel er et organiseringsvedtak',
};

const HJEMMEL_ART_TEKST: Record<HjemmelArt, string> = {
  oppretting: 'Oppretting',
  navnevedtak: 'Navnevedtak',
};

function Navneformrad({ n, etikett }: { n: AktorHjemmelNavneformDto; etikett: string }) {
  return (
    <Table.Row>
      <Table.Cell>
        {etikett} «{n.term}» <NavneformgrunnTag grunn={n.navneformgrunn} />
        {n.rettskildeId && n.eid && (
          <Metatekst style={{ margin: 0 }}>
            <Link asChild><RouterLink to={rettskildeLenkeForId(n.rettskildeId, n.eid)}>{n.rettskildeTittel ?? 'Rettskilden'} {eidKort(n.eid)}</RouterLink></Link>
          </Metatekst>
        )}
      </Table.Cell>
      <Table.Cell>
        <span style={{ display: 'inline-flex', gap: '0.3rem', flexWrap: 'wrap' }}>
          <KunnskapsstatusTag status={n.kunnskapsstatus} />
          {n.godkjenning && <GodkjenningTag status={n.godkjenning} oppdagelsesKilde={n.oppdagelsesKilde ?? 'ukjent'} />}
          {n.hjemmelArt && <Tag data-size="sm" data-color="neutral" title="begreper.hjemmel_art (#441)">{HJEMMEL_ART_TEKST[n.hjemmelArt]}</Tag>}
        </span>
      </Table.Cell>
    </Table.Row>
  );
}

export function AktorHjemmelKort({ virksomhetId, hjemmel, onEndret }: { virksomhetId: string; hjemmel: AktorHjemmelDto | null; onEndret: () => void }) {
  const [valgtArt, setValgtArt] = useState<HjemmelArt | null>(null);
  const [kjorer, setKjorer] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [kvittering, setKvittering] = useState<string | null>(null);
  const [bekreftAlle, setBekreftAlle] = useState(false);

  async function godkjenn() {
    setKjorer(true);
    setFeil(null);
    try {
      const k = await api.godkjennAktorHjemmel(virksomhetId, valgtArt);
      setKvittering([k.navneformGodkjent ? 'Navneformen er godkjent' : null, k.oppretterkantGodkjent ? 'opprettelsen (R oppretter) er godkjent' : null,
        k.hjemmelArt ? `hjemmel_art: ${HJEMMEL_ART_TEKST[k.hjemmelArt].toLowerCase()}` : null].filter(Boolean).join(', ') + '.');
      onEndret();
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke godkjenne hjemmelen.');
    } finally {
      setKjorer(false);
    }
  }

  async function godkjennAlle(rettskildeId: string) {
    setKjorer(true);
    setFeil(null);
    try {
      const r = await api.godkjennAlleAktorutsagn(virksomhetId, rettskildeId);
      setKvittering(`${r.godkjent} utsagn godkjent${r.hoppetOver ? `, ${r.hoppetOver} hoppet over (hjemmel i en merknad)` : ''}.`);
      setBekreftAlle(false);
      onEndret();
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke godkjenne utsagnene.');
    } finally {
      setKjorer(false);
    }
  }

  return (
    <Card data-color="accent" variant="tinted" style={{ padding: '1rem', marginBottom: '1rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>Hjemmel for aktøren</Heading>
      {!hjemmel && <Spinner aria-label="Laster hjemmelen …" data-size="sm" />}
      {hjemmel?.status === 'mangler' && (
        <>
          <Alert data-color="warning" data-size="sm" style={{ marginBottom: '0.25rem' }}>Hjemmel for aktøren mangler</Alert>
          {/* Samme metatekst som i aktørkøen (Johann 2026-11-01, docs/09 §50). */}
          {hjemmel.hullmerknad && <Metatekst style={{ marginBottom: '0.75rem' }}>{hjemmel.hullmerknad}</Metatekst>}
          {hjemmel.mestOmtalt.length > 0 && (
            <div style={{ marginBottom: '0.75rem' }}>
              <Paragraph data-size="sm" style={{ margin: 0 }}><strong>Mest omtalt i (ikke hjemmel):</strong></Paragraph>
              {hjemmel.mestOmtalt.map((m) => (
                <div key={m.rettskildeId} style={{ marginTop: '0.25rem' }}>
                  <Paragraph data-size="sm" style={{ margin: 0 }}>
                    <Link asChild>
                      <RouterLink to={m.eid ? rettskildeLenkeForId(m.rettskildeId, m.eid) : `/rettskilder/${m.rettskildeId}`}>
                        {m.kortnavn ?? m.tittel}{m.eid ? `, ${eidKort(m.eid)}` : ''}
                      </RouterLink>
                    </Link>
                    {': '}{m.utsagn} {m.utsagn === 1 ? 'utsagn' : 'utsagn'} og {m.taggedeForekomster} {m.taggedeForekomster === 1 ? 'tagget forekomst' : 'taggede forekomster'}
                    {m.bestemmelser.length > 1 && ` i ${m.bestemmelser.length} bestemmelser`}.
                  </Paragraph>
                  {m.sitat && <Metatekst style={{ margin: 0 }}>«{m.sitat}»</Metatekst>}
                </div>
              ))}
              <Metatekst style={{ marginTop: '0.25rem' }}>
                Regnes bare av utsagn der aktøren er part og taggede forekomster av navneformene, aldri av fritekst eller treff via klasse. Et
                stedsnavn i en annen forskrift teller ikke.
              </Metatekst>
            </div>
          )}
          {hjemmel.detSomFinnes.length > 0 && (
            <>
              <Heading level={3} data-size="2xs" style={{ marginBottom: '0.25rem' }}>Det som finnes</Heading>
              <Table data-size="sm" style={{ marginBottom: '0.25rem' }}>
                <Table.Body>
                  {hjemmel.detSomFinnes.map((d) => (
                    <Table.Row key={`${d.art}-${d.tekst}`}>
                      <Table.Cell>
                        {d.art === 'klasse' && d.nodeId ? <Link asChild><RouterLink to={`/begreper/${d.nodeId}`}>{d.tekst}</RouterLink></Link> : d.tekst}
                      </Table.Cell>
                      <Table.Cell>
                        <span style={{ display: 'inline-flex', gap: '0.3rem', flexWrap: 'wrap', alignItems: 'center' }}>
                          {d.art === 'klasse' && (
                            <Metatekst as="span">
                              Klassen:{' '}
                              {d.hjemmelRettskildeId && d.hjemmelEid
                                ? <Link asChild><RouterLink to={rettskildeLenkeForId(d.hjemmelRettskildeId, d.hjemmelEid)}>{d.hjemmelTittel ?? 'hjemmelen'} {eidKort(d.hjemmelEid)}</RouterLink></Link>
                                : d.hjemmelEid ? eidKort(d.hjemmelEid) : 'hjemmel ikke registrert'}
                            </Metatekst>
                          )}
                          {d.art === 'klasse' && d.klassensKunnskapsstatus && <KunnskapsstatusTag status={d.klassensKunnskapsstatus} />}
                          {d.art === 'klasse' && <Metatekst as="span">Medlemskapet:</Metatekst>}
                          {d.kunnskapsstatus && <KunnskapsstatusTag status={d.kunnskapsstatus} />}
                          {d.art === 'register' && !d.kunnskapsstatus && <Tag data-size="sm" data-color="neutral">Registerdata</Tag>}
                          {d.forslag && <Tag data-size="sm" data-color="info">Forslag</Tag>}
                        </span>
                      </Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
              <Metatekst>Ingen av disse er hjemmel for at aktøren finnes.</Metatekst>
            </>
          )}
        </>
      )}
      {hjemmel?.status === 'hjemlet' && (
        <>
          <Paragraph data-size="sm" style={{ margin: '0 0 0.25rem', display: 'flex', gap: '0.4rem', flexWrap: 'wrap', alignItems: 'center' }}>
            <Tag data-size="sm" data-color="neutral" title="Hvor regelen fant hjemmelen (docs/33 §5.17).">{KILDE_TEKST[hjemmel.kilde ?? ''] ?? hjemmel.kilde}</Tag>
            {hjemmel.rettskildeId && hjemmel.eid ? (
              <Link asChild>
                <RouterLink to={rettskildeLenkeForId(hjemmel.rettskildeId, hjemmel.eid)}>
                  {hjemmel.rettskildeTittel}{hjemmel.dokumentId ? ` (${hjemmel.dokumentId.replace(/^[A-Z]+\//, '')})` : ''}, {eidKort(hjemmel.eid)}
                </RouterLink>
              </Link>
            ) : <span>{hjemmel.oppretterkant?.kildeUtenforKorpusTekst ?? 'Kilde utenfor korpus'}</span>}
            {hjemmel.gjelderFra && <Metatekst as="span">gjelder fra {hjemmel.gjelderFra}</Metatekst>}
          </Paragraph>
          {hjemmel.disposisjonstyper.length > 0 && (
            <Paragraph data-size="sm" style={{ margin: '0 0 0.35rem', display: 'flex', gap: '0.3rem', flexWrap: 'wrap', alignItems: 'center' }}>
              <Metatekst as="span">Vedtaket er klassifisert som</Metatekst> <DisposisjonstypeTagger typer={hjemmel.disposisjonstyper} />
            </Paragraph>
          )}
          {hjemmel.sitat && <Paragraph data-size="sm" style={{ margin: '0 0 0.5rem', fontStyle: 'italic' }}>«{hjemmel.sitat}»</Paragraph>}
          <Table data-size="sm" style={{ marginBottom: '0.5rem' }}>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Utsagn</Table.HeaderCell>
                <Table.HeaderCell>Kunnskapsstatus og godkjenning</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {hjemmel.navneform && <Navneformrad n={hjemmel.navneform} etikett={hjemmel.navnevedtak ? 'Navnet:' : 'Navneform'} />}
              {hjemmel.oppretterkant && (
                <Table.Row>
                  <Table.Cell>{hjemmel.oppretterkant.visningstekst}</Table.Cell>
                  <Table.Cell>
                    <span style={{ display: 'inline-flex', gap: '0.3rem', flexWrap: 'wrap' }}>
                      <KunnskapsstatusTag status={hjemmel.oppretterkant.kunnskapsstatus} />
                      <GodkjenningTag status={hjemmel.oppretterkant.status} oppdagelsesKilde={hjemmel.oppretterkant.oppdagelsesKilde} />
                    </span>
                  </Table.Cell>
                </Table.Row>
              )}
            </Table.Body>
          </Table>
          {hjemmel.kilde === 'oppretter_kant' && !hjemmel.navneform && (
            <Metatekst style={{ marginBottom: '0.5rem' }}>
              Ingen gjeldende navneform har hjemmel i dette vedtaket. «Godkjenn hjemmelen» godkjenner bare opprettelsen.
            </Metatekst>
          )}
          {hjemmel.venter && hjemmel.maVelgeHjemmelArt && (
            <Fieldset data-size="sm" style={{ marginBottom: '0.5rem' }}>
              <Fieldset.Legend>Hva slags hjemmel er det?</Fieldset.Legend>
              <Fieldset.Description>
                Vedtaket er både {hjemmel.disposisjonstyper.includes('oppretting') ? 'oppretting' : 'omorganisering'} og
                {hjemmel.disposisjonstyper.includes('navneendring') ? ' navneendring' : ' noe annet'}, så regelen avgjør ikke om bestemmelsen oppretter
                aktøren eller gir den navnet. Ingenting er forhåndsvalgt.
              </Fieldset.Description>
              <Radio name="hjemmel-art" value="oppretting" label="Oppretting — bestemmelsen oppretter aktøren" checked={valgtArt === 'oppretting'}
                onChange={() => setValgtArt('oppretting')} />
              <Radio name="hjemmel-art" value="navnevedtak" label="Navnevedtak — bestemmelsen gir aktøren navnet" checked={valgtArt === 'navnevedtak'}
                onChange={() => setValgtArt('navnevedtak')} />
            </Fieldset>
          )}
          {hjemmel.venter ? (
            <span style={{ display: 'inline-flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
              <Button data-size="sm" onClick={godkjenn} disabled={kjorer || (hjemmel.maVelgeHjemmelArt && valgtArt === null)}>
                {kjorer ? 'Godkjenner …' : 'Godkjenn hjemmelen'}
              </Button>
              <Metatekst as="span">
                {hjemmel.maVelgeHjemmelArt && valgtArt === null ? 'Velg hva slags hjemmel det er. ' : ''}
                Godkjenner {[hjemmel.navneform ? 'navneformen' : null, hjemmel.oppretterkant ? 'opprettelsen (R oppretter)' : null].filter(Boolean).join(' og ')} i
                samme handling{hjemmel.navneform && !hjemmel.navneform.hjemmelArt && (valgtArt ?? hjemmel.hjemmelArtForslag)
                  ? `, og merker hjemmelen som ${HJEMMEL_ART_TEKST[(valgtArt ?? hjemmel.hjemmelArtForslag)!].toLowerCase()}` : ''}.
              </Metatekst>
            </span>
          ) : (
            <Metatekst>Hjemmelen er godkjent.</Metatekst>
          )}
          {hjemmel.utsagnIKilden.length > 0 && hjemmel.rettskildeId && (
            <Details data-size="sm" style={{ marginTop: '0.75rem' }}>
              <Details.Summary>Alle {hjemmel.utsagnIKilden.length} utsagn om aktøren i vedtaket</Details.Summary>
              <Details.Content>
                <Metatekst style={{ marginBottom: '0.5rem' }}>Vedtaket står ikke på nytt i lista over de andre rettskildene.</Metatekst>
                <StrukturkantTabell kanter={hjemmel.utsagnIKilden} tomTekst="Ingen utsagn." visKategori visHjemmelKilde={false} />
                {(() => {
                  const ventende = hjemmel.utsagnIKilden.filter((u) => u.status !== 'godkjent').length;
                  if (ventende === 0) return null;
                  return bekreftAlle ? (
                    <span style={{ display: 'inline-flex', gap: '0.5rem' }}>
                      <Button data-size="sm" onClick={() => godkjennAlle(hjemmel.rettskildeId!)} disabled={kjorer}>Bekreft: godkjenn {ventende}</Button>
                      <Button data-size="sm" variant="tertiary" onClick={() => setBekreftAlle(false)}>Avbryt</Button>
                    </span>
                  ) : (
                    <Button data-size="sm" variant="secondary" onClick={() => setBekreftAlle(true)}>Godkjenn alle {ventende}</Button>
                  );
                })()}
              </Details.Content>
            </Details>
          )}
        </>
      )}
      {kvittering && <Alert data-color="success" data-size="sm" style={{ marginTop: '0.5rem' }}>{kvittering}</Alert>}
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.5rem' }}>{feil}</Alert>}
    </Card>
  );
}
