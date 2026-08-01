import { useEffect, useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Button, Checkbox, Heading, Link, Paragraph, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { BegrepsforslagDto, RettskildeSammendrag } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';

/**
 * «Identifiser begrep» (byggesteg 5 runde 1, docs/06-veikart.md) — rent rettskilde-drevet, ingen
 * kobling til Tjeneste. Kjører mot en STUB-KI (KiAgentKlientStub i RegelIde.Data) — se merknaden
 * under kø-listen.
 */
export default function BegrepsforslagKo() {
  const navigate = useNavigate();
  const { gjeldendeBruker } = useBruker();

  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [valgteRettskilder, setValgteRettskilder] = useState<Set<string>>(new Set());
  const [ko, setKo] = useState<BegrepsforslagDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kjorer, setKjorer] = useState(false);

  function lastKo() {
    api.hentBegrepsforslagKo().then(setKo).catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kø.'));
  }

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
    lastKo();
  }, []);

  function vekslRettskilde(id: string, valgt: boolean) {
    setValgteRettskilder((forrige) => {
      const ny = new Set(forrige);
      if (valgt) ny.add(id); else ny.delete(id);
      return ny;
    });
  }

  async function kjorForslag() {
    setFeil(null);
    setKjorer(true);
    try {
      await api.kjorBegrepsforslag({ rettskildeIder: [...valgteRettskilder] });
      lastKo();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kjøring av KI-forslag.');
    } finally {
      setKjorer(false);
    }
  }

  async function avvis(id: string) {
    await api.settBegrepStatus(id, { status: 'utkast' });
    lastKo();
  }

  async function rediger(id: string) {
    await api.settBegrepStatus(id, { status: 'under_revisjon' });
    navigate(`/begreper/${id}`);
  }

  async function godkjenn(id: string) {
    await api.settBegrepStatus(id, { status: 'validert', godkjentAv: gjeldendeBruker?.navn });
    lastKo();
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Identifiser begrep
      </Heading>
      <Paragraph style={{ marginBottom: '1.5rem', maxWidth: '40rem' }}>
        Velg én eller flere rettskilder — agenten leser den faktiske, allerede importerte lovteksten og
        foreslår begrep. Ingen kobling til Tjeneste. Forslag lander alltid som «foreslått av KI», og må
        godkjennes eksplisitt før de blir gjeldende.
      </Paragraph>

      {rettskilder.length > 0 && (
        <div style={{ marginBottom: '1rem' }}>
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginBottom: '0.3rem' }}>Rettskilder:</Paragraph>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.2rem', marginBottom: '0.75rem' }}>
            {rettskilder.map((r) => (
              <Checkbox key={r.id} label={r.tittel} checked={valgteRettskilder.has(r.id)}
                onChange={(e) => vekslRettskilde(r.id, e.target.checked)} />
            ))}
          </div>
          <Button onClick={kjorForslag} disabled={kjorer || valgteRettskilder.size === 0}>
            {kjorer ? 'Kjører KI-forslag …' : 'Kjør KI-forslag'}
          </Button>
        </div>
      )}

      {feil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{feil}</div>}

      <Heading level={2} data-size="sm" style={{ marginTop: '1.5rem' }}>
        Ventende forslag
      </Heading>
      {!ko && <Paragraph>Laster …</Paragraph>}
      {ko && ko.length === 0 && <Paragraph>Ingen ventende begrepsforslag.</Paragraph>}
      {ko && ko.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Term</Table.HeaderCell>
              <Table.HeaderCell>Begrepstype</Table.HeaderCell>
              <Table.HeaderCell>Definisjon</Table.HeaderCell>
              <Table.HeaderCell>KI-versjon</Table.HeaderCell>
              <Table.HeaderCell>Handlinger</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {ko.map((f) => (
              <Table.Row key={f.begrep.id}>
                <Table.Cell>
                  <Link asChild><RouterLink to={`/begreper/${f.begrep.id}`}>{f.begrep.term}</RouterLink></Link>
                </Table.Cell>
                <Table.Cell>{f.begrep.begrepstype}</Table.Cell>
                <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{f.begrep.definisjon}</Table.Cell>
                <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{f.aiForslagVersjon ?? '—'}</Table.Cell>
                <Table.Cell>
                  <div style={{ display: 'flex', gap: '0.4rem' }}>
                    <Button variant="tertiary" data-size="sm" onClick={() => avvis(f.begrep.id)}>Avvis</Button>
                    <Button variant="tertiary" data-size="sm" onClick={() => rediger(f.begrep.id)}>Rediger</Button>
                    <Button data-size="sm" onClick={() => godkjenn(f.begrep.id)}>Godkjenn og legg til</Button>
                  </div>
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}

      <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginTop: '1.5rem', opacity: 0.7 }}>
        Byggesteg 5 runde 1: KI-klienten er en stub (KiAgentKlientStub) — den returnerer ett fast
        eksempelforslag for å bevise kø-/godkjenningsmekanismen, ikke ekte språkmodell-resonnering.
        Ekte leverandørvalg er en egen, senere beslutning.
      </Paragraph>
    </>
  );
}
