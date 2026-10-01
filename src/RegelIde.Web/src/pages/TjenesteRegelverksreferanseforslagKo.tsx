import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Heading, Link, Paragraph, Select, Spinner, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import { KandidatStatusTag } from '../kandidater/KandidatStatusTag';
import { RettskildeFlervalg } from '../rettskilde/RettskildeFlervalg';
import type { RettskildeSammendrag, TjenesteRegelverksreferanseForslagDto } from '../api/types';

/**
 * [Ny, issue #286] Kandidatkø for KI-foreslåtte regelverksreferanser på EGEN virksomhets EKSISTERENDE
 * tjenester som i dag mangler koblingen — egen kø fra TjenesteforslagKo.tsx (som gjelder HELT NYE
 * tjenester) av samme grunn som BegrepDefinisjonRelasjonKo.tsx er egen fra Begrepskandidater.tsx: her
 * er tjenesten allerede en ekte, gjeldende rad — det som vurderes er BARE selve koblingen. Se
 * TjenesteRegelverksreferanseforslagTjeneste (backend) for hele resonnementet, inkl. hvorfor
 * kandidat-innsnevringen er embedding-basert.
 * <para>
 * Godkjenning krever ingen forutgående steg (til forskjell fra BegrepDefinisjonRelasjonKo, som krever
 * at begge forekomster først er godkjent til et Begrep) — tjenesten her er allerede en ekte rad, og
 * godkjenning oppretter direkte den ekte TjenesteRegelverksreferanseEntitet-koblingen.
 * </para>
 * <para>docs/09 §14 (saksbehandlerverktøy-mønsteret): Card alltid rendret, data-size="sm" konsekvent —
 * samme mønster som BegrepDefinisjonRelasjonKo.tsx.</para>
 */
export default function TjenesteRegelverksreferanseforslagKo() {
  const [statusFilter, setStatusFilter] = useState<'Venter' | 'Godkjent' | 'Avvist' | 'Alle'>('Venter');
  const [forslag, setForslag] = useState<TjenesteRegelverksreferanseForslagDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [valgteRettskilder, setValgteRettskilder] = useState<Set<string>>(new Set());
  const [kjorer, setKjorer] = useState(false);
  const [sisteKjoring, setSisteKjoring] = useState<{ utenReferanse: number; vurdert: number; nye: number } | null>(null);

  const [behandlerId, setBehandlerId] = useState<string | null>(null);
  const [radFeil, setRadFeil] = useState<Record<string, string>>({});

  function lastForslag() {
    api.hentTjenesteRegelverksreferanseForslag(statusFilter === 'Alle' ? 'Alle' : statusFilter)
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
      const resultat = await api.kjorTjenesteRegelverksreferanseforslag({ rettskildeIder: [...valgteRettskilder] });
      setSisteKjoring({
        utenReferanse: resultat.antallTjenesterUtenReferanse,
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
      await api.godkjennTjenesteRegelverksreferanseforslag(id);
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
      await api.avvisTjenesteRegelverksreferanseforslag(id);
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
        Regelverksreferanseforslag
      </Heading>
      <Paragraph style={{ marginBottom: '1rem', maxWidth: '48rem' }} data-size="sm">
        Foreslår koblinger fra egen virksomhets EKSISTERENDE tjenester (typisk importerte tjenester)
        uten regelverksreferanser til en paragraf i valgte rettskilder — issue #286. Kandidatparagrafer
        snevres inn med embedding-likhet mot tjenestens tittel/beskrivelse FØR selve KI-vurderingen.
        Ingen automatisk opprettelse uten godkjenning.
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
          {sisteKjoring.utenReferanse} tjeneste(r) uten regelverksreferanse i dag, {sisteKjoring.vurdert} vurdert
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
                  <Table.Cell>
                    <Link asChild><RouterLink to={`/tjenester/${f.tjenesteId}`}>{f.tjenesteTittel}</RouterLink></Link>
                  </Table.Cell>
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
