import { useEffect, useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Alert, Button, Checkbox, Heading, Link, Paragraph, Spinner, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { BegrepsforslagDto, RettskildeSammendrag } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';
import { RettskildeFlervalg } from '../rettskilde/RettskildeFlervalg';

/**
 * «Identifiser begrep» (byggesteg 5 runde 1, docs/06-veikart.md) — rent rettskilde-drevet, ingen
 * kobling til Tjeneste. Kjører mot en STUB-KI (KiAgentKlientStub i RegelIde.Data) — se merknaden
 * under kø-listen.
 *
 * Massehandling (avkrysningsbokser + «Godkjenn valgte»/«Avvis valgte», 2026-08-30) — store
 * test-kjøringer kan legge mange forslag i køen samtidig. Samme UX/backend-mønster som
 * VirksomhetKandidaterListe.tsx (se den filens kommentarer for hele resonnementet).
 */
export default function BegrepsforslagKo() {
  const navigate = useNavigate();
  const { gjeldendeBruker } = useBruker();

  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [valgteRettskilder, setValgteRettskilder] = useState<Set<string>>(new Set());
  const [ko, setKo] = useState<BegrepsforslagDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kjorer, setKjorer] = useState(false);
  const [sisteKjoring, setSisteKjoring] = useState<{ melding: string | null; inputTokens: number | null; outputTokens: number | null } | null>(null);

  const [valgte, setValgte] = useState<Set<string>>(new Set());
  const [massehandlingKjorer, setMassehandlingKjorer] = useState(false);
  const [massehandlingFeil, setMassehandlingFeil] = useState<string | null>(null);

  function lastKo() {
    api.hentBegrepsforslagKo()
      .then((liste) => {
        setKo(liste);
        setValgte(new Set()); // Køen er lastet på nytt — forrige utvalg gjelder ikke lenger.
      })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kø.'));
  }

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
    lastKo();
  }, []);

  async function kjorForslag() {
    setFeil(null);
    setSisteKjoring(null);
    setKjorer(true);
    try {
      const respons = await api.kjorBegrepsforslag({ rettskildeIder: [...valgteRettskilder] });
      setSisteKjoring({ melding: respons.melding, inputTokens: respons.inputTokens, outputTokens: respons.outputTokens });
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

  function vekslValgt(id: string, valgt: boolean) {
    setValgte((forrige) => {
      const ny = new Set(forrige);
      if (valgt) ny.add(id); else ny.delete(id);
      return ny;
    });
  }

  // Ingen paginering på denne siden ennå — "alle viste" er dermed hele køen, ikke bare én side.
  function vekslAlleViste(valgt: boolean) {
    setValgte(valgt ? new Set((ko ?? []).map((f) => f.begrep.id)) : new Set());
  }

  async function massehandling(handling: 'godkjenn' | 'avvis') {
    if (valgte.size === 0) return;
    setMassehandlingKjorer(true);
    setMassehandlingFeil(null);
    try {
      const request = { ider: [...valgte] };
      const resultat = handling === 'godkjenn'
        ? await api.godkjennBegrepsforslagBatch(request)
        : await api.avvisBegrepsforslagBatch(request);
      const feilede = resultat.rader.filter((r) => !r.ok);
      if (feilede.length > 0) {
        setMassehandlingFeil(
          `${feilede.length} av ${resultat.rader.length} rad(er) feilet: ${feilede.map((r) => r.feil).join('; ')}`,
        );
      }
      lastKo();
    } catch (err) {
      setMassehandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved massehandling.');
    } finally {
      setMassehandlingKjorer(false);
    }
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
          <RettskildeFlervalg rettskilder={rettskilder} valgte={valgteRettskilder} onChange={setValgteRettskilder} />
          <Button onClick={kjorForslag} disabled={kjorer || valgteRettskilder.size === 0}>
            {kjorer ? 'Kjører KI-forslag …' : 'Kjør KI-forslag'}
          </Button>
        </div>
      )}

      {feil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{feil}</Alert>}

      {sisteKjoring && (
        <div style={{ marginBottom: '1rem' }}>
          {sisteKjoring.melding && (
            <Alert data-color="info" style={{ marginBottom: '0.3rem' }}>{sisteKjoring.melding}</Alert>
          )}
          {(sisteKjoring.inputTokens !== null || sisteKjoring.outputTokens !== null) && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
              Siste KI-kall: {sisteKjoring.inputTokens ?? '—'} input-tokens, {sisteKjoring.outputTokens ?? '—'} output-tokens.
            </Paragraph>
          )}
        </div>
      )}

      <Heading level={2} data-size="sm" style={{ marginTop: '1.5rem' }}>
        Ventende forslag
      </Heading>
      {!ko && <Spinner aria-label="Laster …" data-size="sm" />}
      {ko && ko.length === 0 && <Paragraph>Ingen ventende begrepsforslag.</Paragraph>}
      {ko && ko.length > 0 && (
        <>
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', margin: 0 }}>
              {valgte.size} valgt{valgte.size === 1 ? '' : 'e'}
            </Paragraph>
            <Button data-size="sm" onClick={() => massehandling('godkjenn')} disabled={valgte.size === 0 || massehandlingKjorer}>
              {massehandlingKjorer ? 'Godkjenner …' : 'Godkjenn valgte'}
            </Button>
            <Button data-size="sm" variant="secondary" onClick={() => massehandling('avvis')} disabled={valgte.size === 0 || massehandlingKjorer}>
              {massehandlingKjorer ? 'Avviser …' : 'Avvis valgte'}
            </Button>
          </div>
          {massehandlingFeil && <div className="feilmelding" style={{ marginBottom: '0.75rem' }}>{massehandlingFeil}</div>}
          <Table border>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>
                  <Checkbox
                    aria-label="Velg alle viste"
                    checked={ko.length > 0 && ko.every((f) => valgte.has(f.begrep.id))}
                    onChange={(e) => vekslAlleViste(e.target.checked)}
                  />
                </Table.HeaderCell>
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
                    <Checkbox
                      aria-label={`Velg forslag ${f.begrep.term}`}
                      checked={valgte.has(f.begrep.id)}
                      onChange={(e) => vekslValgt(f.begrep.id, e.target.checked)}
                    />
                  </Table.Cell>
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
        </>
      )}

      <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginTop: '1.5rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Byggesteg 5 runde 1: KI-klienten er en stub (KiAgentKlientStub) — den returnerer ett fast
        eksempelforslag for å bevise kø-/godkjenningsmekanismen, ikke ekte språkmodell-resonnering.
        Ekte leverandørvalg er en egen, senere beslutning.
      </Paragraph>
    </>
  );
}
