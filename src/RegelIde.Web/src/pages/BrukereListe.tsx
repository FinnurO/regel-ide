import { useEffect, useState, type FormEvent } from 'react';
import { Alert, Button, Field, Heading, Label, Paragraph, Select, Spinner, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { BrukerDto, BrukerRolle } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

/** RBAC-matrisen, docs/03-domenemodell.md §2 — speiler BrukerregisterTjeneste.GyldigeRoller på serveren. */
const GYLDIGE_ROLLER: BrukerRolle[] = ['Fagansvarlig', 'Jurist', 'Systemforvalter', 'Saksbehandler'];

/**
 * Brukerhåndtering — opprett testbrukere og tilordne/endre rolle+virksomhet for eksisterende
 * brukere (test- eller ekte Altinn-brukere). Se docs/13-backlog.md.
 * <para>
 * Sletting er BEVISST utelatt denne runden — det reiser spørsmål om hva som skjer med data en
 * slettet bruker "eier" (opprettetAv/sistEndretAv-felter, proveniens), som er utenfor scope.
 * </para>
 */
export default function BrukereListe() {
  const { virksomheter, laster: lasterVirksomheter } = useVirksomheter();
  // Kun aktive virksomheter i VELGERNE under (tilordne til ny/eksisterende bruker) — de ~370 sovende
  // kommunene/fylkeskommunene fra organisasjonsregister-seedingen (2026-08-14) skal ikke flomme over
  // en velger ment for reelt dagligarbeid. Berører KUN disse velgerne, ikke selve bruker-tabellen
  // (som fortsatt viser virksomhetNavn for eksisterende brukere uansett Aktiv-status).
  const aktiveVirksomheter = virksomheter.filter((v) => v.aktiv);
  const { lastBrukerePaNytt } = useBruker();

  const [brukere, setBrukere] = useState<BrukerDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  const [nyttNavn, setNyttNavn] = useState('');
  const [nyRolle, setNyRolle] = useState<BrukerRolle>('Saksbehandler');
  const [nyVirksomhetId, setNyVirksomhetId] = useState('');
  const [oppretterFeil, setOppretterFeil] = useState<string | null>(null);
  const [oppretter, setOppretter] = useState(false);

  const [redigererId, setRedigererId] = useState<string | null>(null);
  const [redigerRolle, setRedigerRolle] = useState<BrukerRolle>('Saksbehandler');
  const [redigerVirksomhetId, setRedigerVirksomhetId] = useState('');
  const [redigererFeil, setRedigererFeil] = useState<string | null>(null);
  const [lagrer, setLagrer] = useState(false);

  function hentBrukereListe() {
    return api
      .hentBrukere()
      .then(setBrukere)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av brukere.'));
  }

  useEffect(() => {
    hentBrukereListe();
  }, []);

  useEffect(() => {
    if (!nyVirksomhetId && aktiveVirksomheter.length > 0) setNyVirksomhetId(aktiveVirksomheter[0].id);
  }, [aktiveVirksomheter, nyVirksomhetId]);

  async function opprett(e: FormEvent) {
    e.preventDefault();
    setOppretterFeil(null);
    setOppretter(true);
    try {
      await api.opprettBruker({ navn: nyttNavn.trim(), rolle: nyRolle, virksomhetId: nyVirksomhetId });
      setNyttNavn('');
      await Promise.all([hentBrukereListe(), lastBrukerePaNytt()]);
    } catch (err) {
      setOppretterFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av bruker.');
    } finally {
      setOppretter(false);
    }
  }

  function startRedigering(b: BrukerDto) {
    setRedigererId(b.id);
    setRedigerRolle(b.rolle as BrukerRolle);
    setRedigerVirksomhetId(b.virksomhetId);
    setRedigererFeil(null);
  }

  function avbrytRedigering() {
    setRedigererId(null);
    setRedigererFeil(null);
  }

  async function lagreRedigering(id: string) {
    setRedigererFeil(null);
    setLagrer(true);
    try {
      await api.oppdaterBruker(id, { rolle: redigerRolle, virksomhetId: redigerVirksomhetId });
      setRedigererId(null);
      await Promise.all([hentBrukereListe(), lastBrukerePaNytt()]);
    } catch (err) {
      setRedigererFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved oppdatering av bruker.');
    } finally {
      setLagrer(false);
    }
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Brukere
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Opprett testbrukere og tilordne/endre rolle og virksomhet. Ekte Altinn-brukere (opprettet av
        selve innloggingen) kan også få endret rolle og virksomhet her, men opprettes ikke fra dette
        skjemaet.
      </Paragraph>

      <form onSubmit={opprett} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '1.5rem', flexWrap: 'wrap' }}>
        <Textfield
          label="Navn"
          placeholder="f.eks. Kari Testbruker"
          value={nyttNavn}
          onChange={(e) => setNyttNavn(e.target.value)}
          required
        />
        <Field>
          <Label>Rolle</Label>
          <Select value={nyRolle} onChange={(e) => setNyRolle(e.target.value as BrukerRolle)}>
            {GYLDIGE_ROLLER.map((rolle) => (
              <Select.Option key={rolle} value={rolle}>
                {rolle}
              </Select.Option>
            ))}
          </Select>
        </Field>
        <Field>
          <Label>Virksomhet</Label>
          <Select value={nyVirksomhetId} onChange={(e) => setNyVirksomhetId(e.target.value)} disabled={lasterVirksomheter}>
            {aktiveVirksomheter.map((v) => (
              <Select.Option key={v.id} value={v.id}>
                {v.navn}
              </Select.Option>
            ))}
          </Select>
        </Field>
        <Button type="submit" disabled={oppretter || !nyttNavn.trim() || !nyVirksomhetId}>
          {oppretter ? 'Oppretter …' : 'Opprett bruker'}
        </Button>
      </form>
      {oppretterFeil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{oppretterFeil}</Alert>}

      {feil && <Alert data-color="danger">{feil}</Alert>}
      {!brukere && !feil && <Spinner aria-label="Laster …" data-size="sm" />}
      {brukere && brukere.length === 0 && <Paragraph>Ingen brukere funnet.</Paragraph>}

      {redigererFeil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{redigererFeil}</Alert>}

      {brukere && brukere.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Navn</Table.HeaderCell>
              <Table.HeaderCell>Virksomhet</Table.HeaderCell>
              <Table.HeaderCell>Rolle</Table.HeaderCell>
              <Table.HeaderCell>Type</Table.HeaderCell>
              <Table.HeaderCell></Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {brukere.map((b) => {
              const redigererDenne = redigererId === b.id;
              return (
                <Table.Row key={b.id}>
                  <Table.Cell>{b.navn}</Table.Cell>
                  <Table.Cell>
                    {redigererDenne ? (
                      <Select
                        value={redigerVirksomhetId}
                        onChange={(e) => setRedigerVirksomhetId(e.target.value)}
                        aria-label={`Virksomhet for ${b.navn}`}
                      >
                        {aktiveVirksomheter.map((v) => (
                          <Select.Option key={v.id} value={v.id}>
                            {v.navn}
                          </Select.Option>
                        ))}
                      </Select>
                    ) : (
                      b.virksomhetNavn
                    )}
                  </Table.Cell>
                  <Table.Cell>
                    {redigererDenne ? (
                      <Select
                        value={redigerRolle}
                        onChange={(e) => setRedigerRolle(e.target.value as BrukerRolle)}
                        aria-label={`Rolle for ${b.navn}`}
                      >
                        {GYLDIGE_ROLLER.map((rolle) => (
                          <Select.Option key={rolle} value={rolle}>
                            {rolle}
                          </Select.Option>
                        ))}
                      </Select>
                    ) : (
                      b.rolle
                    )}
                  </Table.Cell>
                  <Table.Cell>
                    <Tag data-color={b.erAltinnBruker ? 'info' : 'neutral'} variant="outline">
                      {b.erAltinnBruker ? 'Ekte Altinn-bruker' : 'Testbruker'}
                    </Tag>
                  </Table.Cell>
                  <Table.Cell>
                    {redigererDenne ? (
                      <div style={{ display: 'flex', gap: '0.5rem' }}>
                        <Button variant="primary" data-size="sm" disabled={lagrer} onClick={() => lagreRedigering(b.id)}>
                          {lagrer ? 'Lagrer …' : 'Lagre'}
                        </Button>
                        <Button variant="tertiary" data-size="sm" disabled={lagrer} onClick={avbrytRedigering}>
                          Avbryt
                        </Button>
                      </div>
                    ) : (
                      <Button variant="tertiary" data-size="sm" onClick={() => startRedigering(b)}>
                        Rediger
                      </Button>
                    )}
                  </Table.Cell>
                </Table.Row>
              );
            })}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
