import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Heading, Link, Paragraph, Spinner, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { KiForslagKoRadDto, KiOppdagelseKandidatUtfallDto, RettskildeSammendrag } from '../api/types';
import { RettskildeFlervalg } from '../rettskilde/RettskildeFlervalg';
import { Metatekst } from '../entitet/Metatekst';

/**
 * [Ny, issue #285] «KI-oppdagelse virksomhet/gruppe» — TILLEGG til det deterministiske
 * navnekandidat-sveipet (`NavnekandidaterListe.tsx`), IKKE en erstatning. Samme rettskilde-velger +
 * «kjør KI»-mønster som `BegrepsforslagKo.tsx`/`TjenesteforslagKo.tsx`, men denne siden viser i
 * tillegg en egen kø for de rolle-/relasjon-/gruppe-av-gruppe-forslagene agenten kan skrive direkte
 * (status `foreslatt_av_ai` på `MyndighetstildelingEntitet`/`VirksomhetRelasjonEntitet`/
 * `GruppeMedlemskapEntitet`, issue #285 AC5/AC6) — adskilt fra menneske-opprettede rader, som ALDRI
 * får denne statusen (se `MyndighetstildelingTjeneste.OpprettAsync` m.fl., default `'validert'`).
 *
 * Selve navneform-kandidatene (kategori virksomhet/gruppe) KI-en foreslår havner i den EKSISTERENDE
 * `/navnekandidater`-køen (samme `Status = "Venter"`-flyt som ethvert regex-treff) — denne siden
 * lenker dit i stedet for å duplisere den visningen, filtrert på `oppdagelsesKilde=ki-fri-sveip`.
 */
export default function KiOppdagelseKo() {
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [valgteRettskilder, setValgteRettskilder] = useState<Set<string>>(new Set());
  const [kjorer, setKjorer] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [sisteKjoring, setSisteKjoring] = useState<{
    kandidater: KiOppdagelseKandidatUtfallDto[]; inputTokens: number | null; outputTokens: number | null; meldinger: string[];
  } | null>(null);

  const [ko, setKo] = useState<KiForslagKoRadDto[] | null>(null);
  const [koFeil, setKoFeil] = useState<string | null>(null);
  const [handlingKjorer, setHandlingKjorer] = useState<string | null>(null);

  function lastKo() {
    api.hentKiForslagKo()
      .then(setKo)
      .catch((e) => setKoFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kø.'));
  }

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
    lastKo();
  }, []);

  async function kjorOppdagelse() {
    setFeil(null);
    setSisteKjoring(null);
    setKjorer(true);
    try {
      const respons = await api.kjorKiOppdagelse({ rettskildeIder: [...valgteRettskilder] });
      setSisteKjoring(respons);
      lastKo();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kjøring av KI-oppdagelse.');
    } finally {
      setKjorer(false);
    }
  }

  async function godkjenn(rad: KiForslagKoRadDto) {
    setHandlingKjorer(rad.id);
    try {
      if (rad.type === 'myndighetstildeling') await api.godkjennMyndighetstildeling(rad.id);
      else if (rad.type === 'virksomhet_relasjon') await api.godkjennVirksomhetRelasjon(rad.id);
      else await api.godkjennGruppeMedlemskap(rad.id);
      lastKo();
    } catch (err) {
      setKoFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved godkjenning.');
    } finally {
      setHandlingKjorer(null);
    }
  }

  async function avvis(rad: KiForslagKoRadDto) {
    setHandlingKjorer(rad.id);
    try {
      if (rad.type === 'myndighetstildeling') await api.avvisMyndighetstildeling(rad.id);
      else if (rad.type === 'gruppe_medlemskap') await api.avvisGruppeMedlemskap(rad.id);
      // 'virksomhet_relasjon' bruker det eksisterende, ubetingede slett-endepunktet — se
      // client.ts sin kommentar på slettVirksomhetRelasjon/godkjennVirksomhetRelasjon.
      else await api.slettVirksomhetRelasjon(rad.id);
      lastKo();
    } catch (err) {
      setKoFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved avvisning.');
    } finally {
      setHandlingKjorer(null);
    }
  }

  const typeEtikett: Record<KiForslagKoRadDto['type'], string> = {
    myndighetstildeling: 'Rolle',
    virksomhet_relasjon: 'Relasjon',
    gruppe_medlemskap: 'Gruppe av gruppe',
  };

  return (
    <>
      <Heading level={1} data-size="lg">
        KI-oppdagelse: virksomheter, grupper, roller og relasjoner
      </Heading>
      <Paragraph style={{ marginBottom: '1.5rem', maxWidth: '42rem' }}>
        Velg én eller flere rettskilder — agenten leser den faktiske, allerede importerte lovteksten
        FRITT (ikke bare kjente regex-mønstre) og foreslår virksomhet-/gruppekandidater, pluss rolle/
        relasjon/gruppe-av-gruppe der teksten eksplisitt sier det. Dette er et TILLEGG til det
        deterministiske <Link asChild><RouterLink to="/navnekandidater">navnekandidat-sveipet</RouterLink></Link>,
        ikke en erstatning. Ingenting publiseres uten at et menneske godkjenner det eksplisitt.
      </Paragraph>

      {rettskilder.length > 0 && (
        <div style={{ marginBottom: '1rem' }}>
          <RettskildeFlervalg rettskilder={rettskilder} valgte={valgteRettskilder} onChange={setValgteRettskilder} />
          <Button data-size="sm" onClick={kjorOppdagelse} disabled={kjorer || valgteRettskilder.size === 0}>
            {kjorer ? 'Kjører KI-oppdagelse …' : 'Kjør KI-oppdagelse'}
          </Button>
        </div>
      )}

      {feil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{feil}</Alert>}

      {sisteKjoring && (
        <div style={{ marginBottom: '1.5rem' }}>
          {sisteKjoring.meldinger.map((m, i) => (
            <Alert key={i} data-color="info" style={{ marginBottom: '0.3rem' }}>{m}</Alert>
          ))}
          <Paragraph>
            {sisteKjoring.kandidater.length} forslag behandlet —{' '}
            {sisteKjoring.kandidater.filter((k) => k.navnekandidatId).length} navnekandidat(er) opprettet/gjenbrukt,{' '}
            {sisteKjoring.kandidater.filter((k) => k.myndighetstildelingId).length} rolle(r),{' '}
            {sisteKjoring.kandidater.filter((k) => k.virksomhetRelasjonId).length} relasjon(er),{' '}
            {sisteKjoring.kandidater.filter((k) => k.gruppeMedlemskapId).length} gruppe-av-gruppe.
          </Paragraph>
          {(sisteKjoring.inputTokens !== null || sisteKjoring.outputTokens !== null) && (
            <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
              Siste KI-kall: {sisteKjoring.inputTokens ?? '—'} input-tokens, {sisteKjoring.outputTokens ?? '—'} output-tokens.
            </Metatekst>
          )}
          <Paragraph style={{ marginTop: '0.5rem' }}>
            Selve navneform-forslagene ligger nå i{' '}
            <Link asChild><RouterLink to="/navnekandidater?oppdagelsesKilde=ki-fri-sveip">navnekandidat-køen (filtrert på «ki-fri-sveip»)</RouterLink></Link>.
          </Paragraph>
        </div>
      )}

      <Heading level={2} data-size="sm" style={{ marginTop: '1rem' }}>
        Ventende rolle-/relasjon-/gruppe-av-gruppe-forslag
      </Heading>
      <Paragraph style={{ marginBottom: '1rem', maxWidth: '42rem' }}>
        Egen kø (issue #285 AC6) — disse radene er allerede skrevet til registeret med status
        «foreslått av AI», men regnes IKKE som gjeldende før de godkjennes her.
      </Paragraph>
      {koFeil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{koFeil}</Alert>}
      {!ko && <Spinner aria-label="Laster …" data-size="sm" />}
      {ko && ko.length === 0 && <Paragraph>Ingen ventende KI-forslag.</Paragraph>}
      {ko && ko.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Type</Table.HeaderCell>
              <Table.HeaderCell>Forslag</Table.HeaderCell>
              <Table.HeaderCell>KI-versjon</Table.HeaderCell>
              <Table.HeaderCell>Handlinger</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {ko.map((rad) => (
              <Table.Row key={`${rad.type}-${rad.id}`}>
                <Table.Cell>{typeEtikett[rad.type]}</Table.Cell>
                <Table.Cell>{rad.visningstekst}</Table.Cell>
                <Metatekst as={Table.Cell}>{rad.aiForslagVersjon ?? '—'}</Metatekst>
                <Table.Cell>
                  <div style={{ display: 'flex', gap: '0.4rem' }}>
                    <Button
                      variant="tertiary" data-size="sm" onClick={() => avvis(rad)} disabled={handlingKjorer === rad.id}
                    >
                      Avvis
                    </Button>
                    <Button data-size="sm" onClick={() => godkjenn(rad)} disabled={handlingKjorer === rad.id}>
                      Godkjenn
                    </Button>
                  </div>
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
