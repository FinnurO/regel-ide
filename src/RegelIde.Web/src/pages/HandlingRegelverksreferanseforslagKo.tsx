import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Heading, Link, Paragraph, Select, Spinner, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import { KandidatStatusTag } from '../kandidater/KandidatStatusTag';
import { RettskildeFlervalg } from '../rettskilde/RettskildeFlervalg';
import type { RettskildeSammendrag, HandlingRegelverksreferanseForslagDto } from '../api/types';

/**
 * [Ny, issue #290] Kandidatkø for KI-foreslåtte paragrafnivå-OPPGRADERINGER av Handling-regelverks-
 * referanser importert av OppgaveregisterHandlingSeed — dokumentnivå er allerede koblet der (rettskilden
 * er importert), men fritekst-henvisningen ("§§ 21-4, 22-3" e.l.) var for kompleks for seedens egen
 * enkle regex. Egen kø fra TjenesteRegelverksreferanseforslagKo.tsx (som gjelder en HELT NY kobling for
 * en Tjeneste) — se HandlingRegelverksreferanseforslagTjeneste (backend) for hele resonnementet, inkl.
 * hvorfor kandidat-innsnevringen her er deterministisk (regex+bekreftelse), ikke embedding-basert.
 * <para>docs/09 §14 (saksbehandlerverktøy-mønsteret): Card alltid rendret, data-size="sm" konsekvent —
 * samme mønster som TjenesteRegelverksreferanseforslagKo.tsx.</para>
 */
export default function HandlingRegelverksreferanseforslagKo() {
  const [statusFilter, setStatusFilter] = useState<'Venter' | 'Godkjent' | 'Avvist' | 'Alle'>('Venter');
  const [forslag, setForslag] = useState<HandlingRegelverksreferanseForslagDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [valgteRettskilder, setValgteRettskilder] = useState<Set<string>>(new Set());
  const [kjorer, setKjorer] = useState(false);
  const [sisteKjoring, setSisteKjoring] = useState<{ kandidater: number; vurdert: number; nye: number } | null>(null);

  const [behandlerId, setBehandlerId] = useState<string | null>(null);
  const [radFeil, setRadFeil] = useState<Record<string, string>>({});

  function lastForslag() {
    api.hentHandlingRegelverksreferanseForslag(statusFilter === 'Alle' ? 'Alle' : statusFilter)
      .then(setForslag)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kø.'));
  }

  useEffect(() => {
    lastForslag();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter]);

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);

  async function kjorForslag() {
    setFeil(null);
    setKjorer(true);
    try {
      const resultat = await api.kjorHandlingRegelverksreferanseforslag({ rettskildeIder: [...valgteRettskilder] });
      setSisteKjoring({
        kandidater: resultat.antallKandidatrader,
        vurdert: resultat.antallVurdert,
        nye: resultat.antallNyeForslag,
      });
      lastForslag();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kjøring av forslag.');
    } finally {
      setKjorer(false);
    }
  }

  async function godkjenn(id: string) {
    setBehandlerId(id);
    setRadFeil((forrige) => { const ny = { ...forrige }; delete ny[id]; return ny; });
    try {
      await api.godkjennHandlingRegelverksreferanseforslag(id);
      lastForslag();
    } catch (err) {
      setRadFeil((forrige) => ({ ...forrige, [id]: err instanceof ApiError ? err.message : 'Ukjent feil ved godkjenning.' }));
    } finally {
      setBehandlerId(null);
    }
  }

  async function avvis(id: string) {
    setBehandlerId(id);
    setRadFeil((forrige) => { const ny = { ...forrige }; delete ny[id]; return ny; });
    try {
      await api.avvisHandlingRegelverksreferanseforslag(id);
      lastForslag();
    } catch (err) {
      setRadFeil((forrige) => ({ ...forrige, [id]: err instanceof ApiError ? err.message : 'Ukjent feil ved avvisning.' }));
    } finally {
      setBehandlerId(null);
    }
  }

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.5rem' }}>
        Handling-regelverksreferanseforslag
      </Heading>
      <Paragraph style={{ marginBottom: '1rem', maxWidth: '48rem' }} data-size="sm">
        Foreslår paragrafnivå-oppgraderinger av Oppgaveregister-importerte handlingers regelverks-
        referanser (issue #290) — kun for handlinger der rettskilden allerede er koblet på dokumentnivå,
        men den opprinnelige fritekst-henvisningen var for kompleks for den automatiske importens egen
        enkle regex. Kandidatparagrafer trekkes ut deterministisk fra selve fritekst-henvisningen og
        bekreftes mot rettskildens ekte struktur FØR KI-en vurderer relevans. Ingen automatisk
        opprettelse uten godkjenning.
      </Paragraph>

      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '1rem', flexWrap: 'wrap' }}>
        {rettskilder.length > 0 && (
          <RettskildeFlervalg rettskilder={rettskilder} valgte={valgteRettskilder} onChange={setValgteRettskilder} />
        )}
        <Button data-size="sm" onClick={kjorForslag} disabled={kjorer || valgteRettskilder.size === 0} style={{ marginBottom: '0.75rem' }}>
          {kjorer ? 'Kjører …' : 'Kjør forslag'}
        </Button>
        <Select data-size="sm" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)} style={{ maxWidth: '12rem', marginBottom: '0.75rem' }}>
          <Select.Option value="Venter">Venter</Select.Option>
          <Select.Option value="Godkjent">Godkjent</Select.Option>
          <Select.Option value="Avvist">Avvist</Select.Option>
          <Select.Option value="Alle">Alle</Select.Option>
        </Select>
      </div>

      {sisteKjoring && (
        <Alert data-color="info" data-size="sm" style={{ marginBottom: '1rem' }}>
          {sisteKjoring.kandidater} kandidatrad(er) på dokumentnivå med fritekst-henvisning, {sisteKjoring.vurdert} vurdert
          {' '}mot valgte rettskilder, {sisteKjoring.nye} nytt/nye forslag lagt i køen.
        </Alert>
      )}
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '1rem' }}>{feil}</Alert>}

      <Card style={{ padding: forslag && forslag.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
        {!forslag && <Spinner aria-label="Laster …" data-size="sm" />}
        {forslag && forslag.length === 0 && <Paragraph data-size="sm" style={{ margin: 0 }}>Ingen forslag med denne statusen.</Paragraph>}
        {forslag && forslag.length > 0 && (
          <Table data-size="sm" border data-density="compact">
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Handling</Table.HeaderCell>
                <Table.HeaderCell>Tjeneste</Table.HeaderCell>
                <Table.HeaderCell>Foreslått paragraf</Table.HeaderCell>
                <Table.HeaderCell>Begrunnelse</Table.HeaderCell>
                <Table.HeaderCell>Status</Table.HeaderCell>
                <Table.HeaderCell>Handlinger</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {forslag.map((f) => (
                <Table.Row key={f.id}>
                  <Table.Cell>{f.handlingNavn}</Table.Cell>
                  <Table.Cell>{f.tjenesteTittel}</Table.Cell>
                  <Table.Cell style={{ maxWidth: '16rem' }}>
                    <Link asChild><RouterLink to={rettskildeLenkeForId(f.tilRettskildeId, f.tilEid)}>{f.tilEid}</RouterLink></Link>
                  </Table.Cell>
                  <Table.Cell style={{ maxWidth: '24rem' }}>
                    <Paragraph data-size="xs" style={{ margin: 0, color: 'var(--ds-color-neutral-text-subtle)' }}>{f.begrunnelse}</Paragraph>
                  </Table.Cell>
                  <Table.Cell>
                    <KandidatStatusTag status={f.status} />
                  </Table.Cell>
                  <Table.Cell>
                    {f.status === 'Venter' && (
                      <div style={{ display: 'flex', gap: '0.4rem', flexDirection: 'column', alignItems: 'flex-start' }}>
                        <div style={{ display: 'flex', gap: '0.4rem' }}>
                          <Button data-size="sm" onClick={() => godkjenn(f.id)} disabled={behandlerId === f.id}>Godkjenn</Button>
                          <Button data-size="sm" variant="secondary" onClick={() => avvis(f.id)} disabled={behandlerId === f.id}>Avvis</Button>
                        </div>
                        {radFeil[f.id] && <Alert data-color="danger" data-size="sm" style={{ margin: 0 }}>{radFeil[f.id]}</Alert>}
                      </div>
                    )}
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}
      </Card>
    </>
  );
}
