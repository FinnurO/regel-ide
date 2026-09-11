import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Heading, Link, Paragraph, Select, Spinner, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import { KandidatStatusTag } from '../kandidater/KandidatStatusTag';
import type { BegrepDefinisjonRelasjonKandidatDto } from '../api/types';

/**
 * [Ny, #212, 2026-09-10] Kandidatkø for «definert likt som»-forslag mellom to begreps-FOREKOMSTER
 * (BegrepsforekomstEntitet), egen kø fra Begrepskandidater.tsx (M1/M11-godkjenning) av samme grunn
 * som den siden selv er egen fra BegrepsforslagKo.tsx — se BegrepDefinisjonRelasjonTjeneste (backend)
 * for hele resonnementet.
 * <para>
 * Deteksjonsregelen (Johann, 2026-09-10): eksakt lik definisjonstekst etter normalisering (mellomrom/
 * store-små/tegnsetting bort) — INGEN score, INGEN diff å vise (en match ER null forskjell). Denne
 * siden viser derfor ordlyden fra begge sider RÅTT, side om side, slik saksbehandleren selv kan
 * bekrefte likheten — «ingen ugjennomsiktig score alene» (AC3).
 * </para>
 * <para>
 * Bekreftelse krever at BEGGE underliggende forekomster allerede er godkjent til et Begrep i
 * Begrepskandidater.tsx (kjernebeslutningen er om Begrep-rader, ikke rå forekomster) — feiler kallet
 * med den forklaringen, vises serverens feilmelding uendret i stedet for å late som noe annet gikk galt.
 * </para>
 * <para>
 * docs/09 §14 (saksbehandlerverktøy-mønsteret): Card alltid rendret (tom-tilstand som Paragraph inni),
 * data-size="sm" konsekvent — bygget fra dag én med dette mønsteret, ikke det eldre skjema-mønsteret.
 * </para>
 */
export default function BegrepDefinisjonRelasjonKo() {
  const [statusFilter, setStatusFilter] = useState<'Venter' | 'Godkjent' | 'Avvist' | 'Alle'>('Venter');
  const [kandidater, setKandidater] = useState<BegrepDefinisjonRelasjonKandidatDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [sveiper, setSveiper] = useState(false);
  const [sisteSveip, setSisteSveip] = useState<{ grupper: number; nye: number } | null>(null);
  const [behandlerId, setBehandlerId] = useState<string | null>(null);
  const [radFeil, setRadFeil] = useState<Record<string, string>>({});

  function lastKandidater() {
    api.hentBegrepDefinisjonRelasjonKandidater(statusFilter === 'Alle' ? 'Alle' : statusFilter)
      .then(setKandidater)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kø.'));
  }

  useEffect(() => {
    lastKandidater();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter]);

  async function kjorSveip() {
    setFeil(null);
    setSveiper(true);
    try {
      const resultat = await api.sveipBegrepDefinisjonRelasjoner();
      setSisteSveip({ grupper: resultat.antallGrupperFunnet, nye: resultat.antallNyeKandidater });
      lastKandidater();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sveip.');
    } finally {
      setSveiper(false);
    }
  }

  async function godkjenn(id: string) {
    setBehandlerId(id);
    setRadFeil((forrige) => { const ny = { ...forrige }; delete ny[id]; return ny; });
    try {
      await api.godkjennBegrepDefinisjonRelasjon(id);
      lastKandidater();
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
      await api.avvisBegrepDefinisjonRelasjon(id);
      lastKandidater();
    } catch (err) {
      setRadFeil((forrige) => ({ ...forrige, [id]: err instanceof ApiError ? err.message : 'Ukjent feil ved avvisning.' }));
    } finally {
      setBehandlerId(null);
    }
  }

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.5rem' }}>
        Relaterte definisjoner
      </Heading>
      <Paragraph style={{ marginBottom: '1rem', maxWidth: '48rem' }} data-size="sm">
        Foreslår par av begreps-forekomster (M1/M11-sveipet) med EKSAKT lik definisjonstekst etter
        normalisering (mellomrom/store-små bokstaver/tegnsetting bort) i to ULIKE rettskilder — issue
        #212. Bøyningsvarianter av samme stamme («fellesgrad»/«fellesgrader») og reelle ordlyd-avvik
        regnes bevisst som ULIKE og foreslås ikke automatisk. Bekreftelse krever at begge forekomster
        allerede er godkjent til et Begrep (Begrepskandidater-siden) — relasjonen er mellom to
        register-rader, ikke rå forekomster.
      </Paragraph>

      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap' }}>
        <Button data-size="sm" onClick={kjorSveip} disabled={sveiper}>
          {sveiper ? 'Sveiper …' : 'Sveip hele korpuset'}
        </Button>
        <Select data-size="sm" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)} style={{ maxWidth: '12rem' }}>
          <Select.Option value="Venter">Venter</Select.Option>
          <Select.Option value="Godkjent">Godkjent</Select.Option>
          <Select.Option value="Avvist">Avvist</Select.Option>
          <Select.Option value="Alle">Alle</Select.Option>
        </Select>
      </div>

      {sisteSveip && (
        <Alert data-color="info" data-size="sm" style={{ marginBottom: '1rem' }}>
          Fant {sisteSveip.grupper} gruppe(r) med eksakt lik definisjonstekst på tvers av rettskilder,
          {' '}{sisteSveip.nye} nytt/nye forslag lagt i køen.
        </Alert>
      )}
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '1rem' }}>{feil}</Alert>}

      <Card style={{ padding: kandidater && kandidater.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
        {!kandidater && <Spinner aria-label="Laster …" data-size="sm" />}
        {kandidater && kandidater.length === 0 && <Paragraph data-size="sm" style={{ margin: 0 }}>Ingen kandidater med denne statusen.</Paragraph>}
        {kandidater && kandidater.length > 0 && (
          <Table data-size="sm" border data-density="compact">
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Term</Table.HeaderCell>
                <Table.HeaderCell>Fra (rettskilde/ordlyd)</Table.HeaderCell>
                <Table.HeaderCell>Til (rettskilde/ordlyd)</Table.HeaderCell>
                <Table.HeaderCell>Status</Table.HeaderCell>
                <Table.HeaderCell>Handlinger</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {kandidater.map((k) => (
                <Table.Row key={k.id}>
                  <Table.Cell>{k.fra.begrepOriginal}</Table.Cell>
                  <Table.Cell style={{ maxWidth: '20rem' }}>
                    <Link asChild><RouterLink to={rettskildeLenkeForId(k.fra.rettskildeId, k.fra.nodeEid)}>Paragraf</RouterLink></Link>
                    <Paragraph data-size="xs" style={{ margin: 0, color: 'var(--ds-color-neutral-text-subtle)' }}>{k.fra.definisjon}</Paragraph>
                  </Table.Cell>
                  <Table.Cell style={{ maxWidth: '20rem' }}>
                    <Link asChild><RouterLink to={rettskildeLenkeForId(k.til.rettskildeId, k.til.nodeEid)}>Paragraf</RouterLink></Link>
                    <Paragraph data-size="xs" style={{ margin: 0, color: 'var(--ds-color-neutral-text-subtle)' }}>{k.til.definisjon}</Paragraph>
                  </Table.Cell>
                  <Table.Cell>
                    <KandidatStatusTag status={k.status} />
                  </Table.Cell>
                  <Table.Cell>
                    {k.status === 'Venter' && (
                      <div style={{ display: 'flex', gap: '0.4rem', flexDirection: 'column', alignItems: 'flex-start' }}>
                        <div style={{ display: 'flex', gap: '0.4rem' }}>
                          <Button data-size="sm" onClick={() => godkjenn(k.id)} disabled={behandlerId === k.id}>Godkjenn</Button>
                          <Button data-size="sm" variant="secondary" onClick={() => avvis(k.id)} disabled={behandlerId === k.id}>Avvis</Button>
                        </div>
                        {radFeil[k.id] && <Alert data-color="danger" data-size="sm" style={{ margin: 0 }}>{radFeil[k.id]}</Alert>}
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
