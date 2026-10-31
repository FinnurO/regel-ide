import { useCallback, useEffect, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import {
  Alert, Button, Card, Dialog, Field, Heading, Label, Link, Paragraph, Select, Spinner, Table, Textarea, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { FunnDto, FunnKoblingDto, FunnTilgangDto, FunnVurderingspunktInn } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { FunnMerker, FunnTekst, OffentligFunn, SIKKERHET_TEKST, lovdataLenke } from '../funn/FunnVisning';

const ROLLE_TEKST: Record<string, string> = {
  motstridende_bestemmelse: 'Mulig motstrid',
  gjelder_bestemmelse: 'Gjelder',
  berort_aktor: 'Berørt aktør',
  navneform: 'Navneform',
  etterfolger: 'Etterfølger',
  kilde: 'Kilde',
};

function tid(iso: string | null): string {
  return iso ? new Date(iso).toLocaleDateString('nb-NO', { dateStyle: 'medium' }) : '';
}

/**
 * [Ny, issue #429 «Funn til vurdering», 2026-10-30] Ett funn: påstanden, bestemmelsene, vurderingen med prøvde bortforklaringer,
 * kildene, forhåndsvisningen av det som publiseres, og behandlingen (docs/09 §49). Detaljmalen i §6/§14: brødsmulesti, H1 og merkene
 * på linje 2, seksjoner som `Card` som alltid vises.
 * <para>
 * Godkjenning for publisering og tilbaketrekking bekreftes i en `Dialog` som viser nøyaktig teksten som blir publisert (§14: kritiske
 * overganger). Knappen vises bare for publiseringsansvarlige (`/api/funn/tilgang`); serveren sjekker uansett (403).
 * </para>
 */
export default function FunnDetalj() {
  const { id } = useParams<{ id: string }>();
  const [funn, setFunn] = useState<FunnDto | null>(null);
  const [tilgang, setTilgang] = useState<FunnTilgangDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [handlingFeil, setHandlingFeil] = useState<string | null>(null);
  const [arbeider, setArbeider] = useState(false);
  const [bekreft, setBekreft] = useState<'godkjenn' | 'trekk' | null>(null);

  // Juristvurdering: ingenting forhåndsvalgt (CLAUDE.md §8).
  const [vurdering, setVurdering] = useState('');
  const [vurdertAvType, setVurdertAvType] = useState<'' | 'menneske' | 'ki_agent'>('');
  const [nyBortforklaringOverskrift, setNyBortforklaringOverskrift] = useState('');
  const [nyBortforklaring, setNyBortforklaring] = useState('');
  const [begrunnelse, setBegrunnelse] = useState('');

  const last = useCallback(() => {
    if (!id) return;
    api.hentEttFunn(id).then((f) => { setFunn(f); setVurdering(f.vurdering ?? ''); setFeil(null); })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av funnet.'));
    api.hentFunnTilgang().then(setTilgang).catch(() => setTilgang(null));
  }, [id]);

  useEffect(() => { last(); }, [last]);

  async function utfor(handling: () => Promise<FunnDto>) {
    setArbeider(true);
    setHandlingFeil(null);
    try {
      const f = await handling();
      setFunn(f);
      setVurdering(f.vurdering ?? '');
      setBekreft(null);
    } catch (e) {
      setHandlingFeil(e instanceof ApiError ? e.message : 'Ukjent feil.');
      setBekreft(null);
    } finally {
      setArbeider(false);
    }
  }

  if (feil) return <Alert data-color="danger" data-size="sm">{feil}</Alert>;
  if (!funn) return <Spinner aria-label="Laster funnet …" data-size="sm" />;

  const bestemmelser = funn.koblinger.filter((k) => k.rolle === 'motstridende_bestemmelse' || k.rolle === 'gjelder_bestemmelse');
  const aktorer = funn.koblinger.filter((k) => k.rolle === 'berort_aktor' || k.rolle === 'navneform' || k.rolle === 'etterfolger');
  const kilder = funn.koblinger.filter((k) => k.rolle === 'kilde');
  const punkter = (type: string) => funn.vurderingspunkter.filter((p) => p.type === type);
  const kanBehandles = funn.status === 'oppdaget' || funn.status === 'juristvurdert';
  const etterfolgerIkkeVurdert = funn.premissmangler.some((m) => m.startsWith('Etterfølger ikke vurdert'));

  function juristvurder() {
    const eksisterende: FunnVurderingspunktInn[] = funn!.vurderingspunkter.map((p) => ({
      type: p.type, tekst: p.tekst, overskrift: p.overskrift, sikkerhet: p.sikkerhet,
    }));
    const nye: FunnVurderingspunktInn[] = nyBortforklaring.trim()
      ? [{ type: 'bortforklaring_provd', tekst: nyBortforklaring.trim(), overskrift: nyBortforklaringOverskrift.trim() || null }]
      : [];
    return utfor(async () => {
      const f = await api.juristvurderFunn(funn!.id, {
        vurdering, vurdertAvType: vurdertAvType as 'menneske' | 'ki_agent', vurderingspunkter: [...eksisterende, ...nye],
      });
      setNyBortforklaring('');
      setNyBortforklaringOverskrift('');
      return f;
    });
  }

  return (
    <>
      <Metatekst as="nav" aria-label="Brødsmulesti" style={{ display: 'flex', gap: '0.4rem', marginBottom: '0.6rem', flexWrap: 'wrap' }}>
        <Link asChild><RouterLink to="/funn">Funn til vurdering</RouterLink></Link>
        <span>/</span>
        <span style={{ color: 'var(--ds-color-neutral-text-default)' }}>{funn.eksternNokkel ?? 'Funn'}</span>
      </Metatekst>
      <Heading level={1} data-size="lg">{funn.tittel}</Heading>
      <Paragraph style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', margin: '0.5rem 0 1.5rem' }}>
        <FunnMerker funn={funn} />
      </Paragraph>

      {handlingFeil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '1rem' }}>{handlingFeil}</Alert>}

      <div style={{ display: 'grid', gap: '1rem' }}>
        <Card style={{ padding: '1rem' }} data-size="sm">
          <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>Påstand</Heading>
          <FunnTekst tekst={funn.beskrivelse} />
          {funn.internMerknad && <Metatekst style={{ marginTop: '0.5rem' }}>Intern merknad (publiseres ikke): {funn.internMerknad}</Metatekst>}
        </Card>

        <Card style={{ padding: 0, overflow: 'hidden' }} data-size="sm">
          <Heading level={2} data-size="sm" style={{ padding: '1rem 1rem 0.5rem' }}>Bestemmelser</Heading>
          {bestemmelser.length === 0 ? (
            <Paragraph data-size="sm" style={{ padding: '0 1rem 1rem', margin: 0 }}>Funnet peker ikke på noen bestemmelse.</Paragraph>
          ) : (
            <Table data-size="sm">
              <Table.Head><Table.Row>
                <Table.HeaderCell>Rolle</Table.HeaderCell><Table.HeaderCell>Bestemmelse</Table.HeaderCell><Table.HeaderCell>I korpuset</Table.HeaderCell>
              </Table.Row></Table.Head>
              <Table.Body>
                {bestemmelser.map((k) => <BestemmelseRad key={k.id} k={k} />)}
              </Table.Body>
            </Table>
          )}
        </Card>

        {aktorer.length > 0 && (
          <Card style={{ padding: '1rem' }} data-size="sm">
            <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>Aktører</Heading>
            <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
              {aktorer.map((k) => (
                <li key={k.id}><Paragraph data-size="sm" style={{ margin: 0 }}>
                  <Metatekst as="span">{ROLLE_TEKST[k.rolle]}: </Metatekst>
                  {k.virksomhetId ? <Link asChild><RouterLink to={`/virksomheter/${k.virksomhetId}`}>{k.etikett}</RouterLink></Link> : k.etikett}
                  {k.merknad ? ` — ${k.merknad}` : ''}
                </Paragraph></li>
              ))}
            </ul>
          </Card>
        )}

        <Card style={{ padding: '1rem' }} data-size="sm">
          <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>Vurdering</Heading>
          <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Bortforklaringer som er prøvd</Heading>
          {punkter('bortforklaring_provd').length === 0 ? (
            <Paragraph data-size="sm">Ingen ennå. Juristen prøver først å forklare funnet bort (#429).</Paragraph>
          ) : (
            <ul style={{ margin: '0 0 0.75rem', paddingLeft: '1.25rem' }}>
              {punkter('bortforklaring_provd').map((p) => (
                <li key={p.id}><Paragraph data-size="sm" style={{ margin: 0 }}>{p.overskrift && <em>{p.overskrift}: </em>}{p.tekst}</Paragraph></li>
              ))}
            </ul>
          )}
          {funn.vurdering && <Paragraph data-size="sm"><strong>Vurdering:</strong> {funn.vurdering}</Paragraph>}
          {punkter('vurdering').length > 0 && (
            <ul style={{ margin: '0 0 0.75rem', paddingLeft: '1.25rem' }}>
              {punkter('vurdering').map((p) => (
                <li key={p.id}><Paragraph data-size="sm" style={{ margin: 0 }}>
                  {p.sikkerhet ? `Sikkerhet: ${SIKKERHET_TEKST[p.sikkerhet] ?? p.sikkerhet} ${p.tekst}.` : p.tekst}
                </Paragraph></li>
              ))}
            </ul>
          )}
          {punkter('ikke_avgjort').length > 0 && (
            <>
              <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Ikke avgjort</Heading>
              <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
                {punkter('ikke_avgjort').map((p) => <li key={p.id}><Paragraph data-size="sm" style={{ margin: 0 }}>{p.tekst}</Paragraph></li>)}
              </ul>
            </>
          )}
          {funn.status === 'forklart_bort' && (
            <Paragraph data-size="sm" style={{ marginTop: '0.75rem' }}><strong>Forklart bort:</strong> {funn.forklartBortBegrunnelse}</Paragraph>
          )}
          <Metatekst style={{ marginTop: '0.75rem' }}>
            Registrert av {funn.opprettetAv} {tid(funn.opprettetTidspunkt)} ({funn.oppdagelsesKilde}{funn.funnetI ? `, funnet i ${funn.funnetI}` : ''}).
            {funn.juristvurdertAv && ` Juristvurdert av ${funn.juristvurdertAv} ${tid(funn.juristvurdertTidspunkt)}.`}
            {funn.forklartBortAv && ` Forklart bort av ${funn.forklartBortAv} ${tid(funn.forklartBortTidspunkt)}.`}
            {funn.godkjentAv && ` Godkjent for publisering av ${funn.godkjentAv} ${tid(funn.godkjentTidspunkt)}.`}
          </Metatekst>
        </Card>

        {kilder.length > 0 && (
          <Card style={{ padding: '1rem' }} data-size="sm">
            <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>Kilder</Heading>
            <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
              {kilder.map((k) => {
                const lenke = k.eksternLenke ?? lovdataLenke(k.eid ?? k.rettskildeEli);
                return (
                  <li key={k.id}><Paragraph data-size="sm" style={{ margin: 0 }}>
                    {lenke ? <Link href={lenke} target="_blank" rel="noopener noreferrer">{k.etikett}</Link> : k.etikett}
                  </Paragraph></li>
                );
              })}
            </ul>
          </Card>
        )}

        <Card style={{ padding: '1rem' }} data-size="sm">
          <Heading level={2} data-size="sm" style={{ marginBottom: '0.25rem' }}>Behandling</Heading>
          {funn.premissmangler.length > 0 && (
            <Alert data-color="warning" data-size="sm" style={{ margin: '0.5rem 0' }}>
              <Paragraph data-size="sm" style={{ margin: 0 }}>Premissporten stopper publisering (docs/33 §4.6):</Paragraph>
              <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
                {funn.premissmangler.map((m) => <li key={m}><Paragraph data-size="sm" style={{ margin: 0 }}>{m}</Paragraph></li>)}
              </ul>
            </Alert>
          )}

          {kanBehandles && (
            <div style={{ display: 'grid', gap: '0.75rem', maxWidth: '44rem', marginTop: '0.5rem' }}>
              <Heading level={3} data-size="xs">Juristvurdering</Heading>
              {/* [ENDRET, kaldsjekken 2026-10-30] To felt, ikke ett Field med to input: Designsystemet advarte i konsollen. */}
              <Textfield data-size="sm" label="Ny prøvd bortforklaring: overskrift (valgfri, for eksempel «Lex posterior»)"
                value={nyBortforklaringOverskrift} onChange={(e) => setNyBortforklaringOverskrift(e.target.value)} />
              <Field>
                <Label>Ny prøvd bortforklaring: hva som ble prøvd, og resultatet (valgfri, legges til de over)</Label>
                <Textarea data-size="sm" rows={2} value={nyBortforklaring} onChange={(e) => setNyBortforklaring(e.target.value)} />
              </Field>
              <Field>
                <Label>Vurdering</Label>
                <Textarea data-size="sm" rows={3} value={vurdering} onChange={(e) => setVurdering(e.target.value)} />
              </Field>
              <Field>
                <Label>Vurdert av</Label>
                <Select data-size="sm" value={vurdertAvType} onChange={(e) => setVurdertAvType(e.target.value as '' | 'menneske' | 'ki_agent')}>
                  <Select.Option value="">Velg …</Select.Option>
                  <Select.Option value="menneske" disabled={!tilgang?.kanJuristvurdere}>Meg, som jurist</Select.Option>
                  <Select.Option value="ki_agent">Juristagenten (KI), ført inn av meg</Select.Option>
                </Select>
              </Field>
              <div>
                <Button data-size="sm" disabled={arbeider || !vurdering.trim() || !vurdertAvType} onClick={juristvurder}>
                  Lagre juristvurdering
                </Button>
              </div>

              {(tilgang?.kanJuristvurdere || tilgang?.kanPublisere) && (
                <>
                  <Heading level={3} data-size="xs">Forklar bort</Heading>
                  <Field>
                    <Label>Begrunnelse</Label>
                    <Textarea data-size="sm" rows={2} value={begrunnelse} onChange={(e) => setBegrunnelse(e.target.value)} />
                  </Field>
                  <div>
                    <Button data-size="sm" variant="secondary" disabled={arbeider || !begrunnelse.trim()}
                      onClick={() => utfor(() => api.forklarBortFunn(funn.id, begrunnelse))}>
                      Forklar bort
                    </Button>
                  </div>
                </>
              )}
            </div>
          )}

          <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', flexWrap: 'wrap', marginTop: '1rem' }}>
            {funn.status === 'juristvurdert' && tilgang?.kanPublisere && (
              <Button data-size="sm" disabled={arbeider || funn.premissmangler.length > 0} onClick={() => setBekreft('godkjenn')}>
                Godkjenn for publisering …
              </Button>
            )}
            {funn.status === 'godkjent_for_publisering' && tilgang?.kanPublisere && (
              <Button data-size="sm" variant="secondary" disabled={arbeider} onClick={() => setBekreft('trekk')}>Trekk tilbake …</Button>
            )}
            {!tilgang?.kanPublisere && funn.status !== 'forklart_bort' && (
              <Metatekst style={{ margin: 0 }}>
                Bare publiseringsansvarlige kan godkjenne for publisering
                {tilgang && !tilgang.publiseringsansvarligeKonfigurert ? '. Ingen er satt opp ennå (RegelIde:Funn:Publiseringsansvarlige).' : '.'}
              </Metatekst>
            )}
          </div>
        </Card>

        {funn.forhandsvisning && (
          <Card style={{ padding: '1rem' }} data-size="sm">
            <Heading level={2} data-size="sm" style={{ marginBottom: '0.25rem' }}>
              {funn.status === 'godkjent_for_publisering' ? 'Slik står det på nettsiden' : 'Slik blir det på nettsiden'}
            </Heading>
            <Metatekst style={{ marginBottom: '0.75rem' }}>
              {funn.status === 'godkjent_for_publisering'
                ? 'Det frosne innholdet som ble godkjent. Endres det offentlige innholdet, går funnet tilbake til juristvurdert.'
                : 'Forhåndsvisning. Ingenting publiseres før funnet er godkjent for publisering.'}
            </Metatekst>
            <OffentligFunn funn={funn.forhandsvisning} modus={funn.status === 'godkjent_for_publisering' ? 'godkjent' : 'forhandsvisning'}
              etterfolgerIkkeVurdert={etterfolgerIkkeVurdert} />
          </Card>
        )}
      </div>

      <Dialog open={bekreft !== null} onClose={() => setBekreft(null)} closeButton="Avbryt" style={{ maxWidth: '44rem' }}>
        <Dialog.Block>
          <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
            {bekreft === 'godkjenn' ? 'Godkjenne for publisering?' : 'Trekke tilbake fra publisering?'}
          </Heading>
          <Paragraph data-size="sm">
            {bekreft === 'godkjenn'
              ? 'Teksten under fryses og kommer med i neste eksport til nettsiden (nettside/data/funn/register.json). Nettsiden endres først når den PR-en flettes.'
              : 'Funnet går tilbake til juristvurdert og kommer ikke med i neste eksport.'}
          </Paragraph>
          {bekreft === 'godkjenn' && funn.forhandsvisning && (
            <div style={{ border: '1px solid var(--ds-color-neutral-border-subtle)', borderRadius: 'var(--ds-border-radius-md)', padding: '0.75rem' }}>
              <OffentligFunn funn={funn.forhandsvisning} modus="til-godkjenning" />
            </div>
          )}
        </Dialog.Block>
        <Dialog.Block style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
          <Button data-size="sm" variant="secondary" onClick={() => setBekreft(null)}>Avbryt</Button>
          <Button data-size="sm" disabled={arbeider} onClick={() => utfor(() => bekreft === 'godkjenn'
            ? api.godkjennFunnForPublisering(funn.id) : api.trekkFunnTilbake(funn.id))}>
            {bekreft === 'godkjenn' ? 'Godkjenn for publisering' : 'Trekk tilbake'}
          </Button>
        </Dialog.Block>
      </Dialog>
    </>
  );
}

function BestemmelseRad({ k }: { k: FunnKoblingDto }) {
  const lovdata = lovdataLenke(k.eid ?? k.rettskildeEli);
  return (
    <Table.Row>
      <Table.Cell><Metatekst as="span">{ROLLE_TEKST[k.rolle]}</Metatekst></Table.Cell>
      <Table.Cell>
        {k.etikett}{k.merknad ? ` — ${k.merknad}` : ''}
        {lovdata && <> <Link href={lovdata} target="_blank" rel="noopener noreferrer" data-size="sm">(Lovdata)</Link></>}
      </Table.Cell>
      <Table.Cell>
        {k.rettskildeId ? (
          <Link asChild><RouterLink to={k.eid ? rettskildeLenkeForId(k.rettskildeId, k.eid) : `/rettskilder/${k.rettskildeId}`}>
            {k.nodeId ? 'Åpne bestemmelsen' : 'Åpne rettskilden (bestemmelsen ikke funnet)'}
          </RouterLink></Link>
        ) : (
          <Metatekst as="span">Ikke i korpuset</Metatekst>
        )}
      </Table.Cell>
    </Table.Row>
  );
}
