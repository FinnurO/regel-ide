import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Heading, Link, Paragraph } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { Strukturforslagsko } from '../strukturkant/Strukturforslagsko';
import type { KiOppdagelseKandidatUtfallDto, RettskildeSammendrag } from '../api/types';
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

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);

  async function kjorOppdagelse() {
    setFeil(null);
    setSisteKjoring(null);
    setKjorer(true);
    try {
      const respons = await api.kjorKiOppdagelse({ rettskildeIder: [...valgteRettskilder] });
      setSisteKjoring(respons);
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kjøring av KI-oppdagelse.');
    } finally {
      setKjorer(false);
    }
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        KI-oppdagelse og forslagskø for strukturkanter
      </Heading>
      <Paragraph style={{ marginBottom: '1.5rem', maxWidth: '42rem' }}>
        Velg én eller flere rettskilder — agenten leser den faktiske, allerede importerte lovteksten
        FRITT (ikke bare kjente regex-mønstre) og foreslår virksomheter og klasser, roller og områder (er typen usikker, velger du den), pluss rolle/
        relasjon/gruppe-av-gruppe der teksten eksplisitt sier det. Dette er et TILLEGG til det
        deterministiske <Link asChild><RouterLink to="/navnekandidater/historikk">navnekandidat-sveipet</RouterLink></Link>,
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
            {sisteKjoring.kandidater.filter((k) => k.rolleKantId).length} rolle(r)/tilhørighet(er),{' '}
            {sisteKjoring.kandidater.filter((k) => k.relasjonKantId).length} relasjon(er),{' '}
            {sisteKjoring.kandidater.filter((k) => k.gruppeAvGruppeKantId).length} gruppe-av-gruppe.
            {/* [Ny, issue #440] Kjente ord blir ikke navnekandidater (aktørkøens ruting); grunnen står per forslag i navnekandidatFeil. */}
            {(() => {
              const kjente = sisteKjoring.kandidater.filter((k) => k.aktorkoRute && k.aktorkoRute !== 'aktorko').length;
              return kjente > 0 ? ` ${kjente} var kjente ord og ble ikke lagt i køen.` : '';
            })()}
          </Paragraph>
          {(sisteKjoring.inputTokens !== null || sisteKjoring.outputTokens !== null) && (
            <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
              Siste KI-kall: {sisteKjoring.inputTokens ?? '—'} input-tokens, {sisteKjoring.outputTokens ?? '—'} output-tokens.
            </Metatekst>
          )}
          <Paragraph style={{ marginTop: '0.5rem' }}>
            Selve navneform-forslagene ligger nå i{' '}
            <Link asChild><RouterLink to="/aktorko">aktørkøen</RouterLink></Link>, og i den flate{' '}
            <Link asChild><RouterLink to="/navnekandidater/historikk?oppdagelsesKilde=ki-fri-sveip">navnekandidat-lista (filtrert på «ki-fri-sveip»)</RouterLink></Link>.
          </Paragraph>
        </div>
      )}

      {/* [ENDRET, issue #313] Den gamle lista over ventende KI-forslag er erstattet av den felles forslagskøen for strukturkanter —
        * samme kø for mønster- og KI-forslag, med lasting per rettskilde, forhåndsvisning og avvisning som status (#374). */}
      <Strukturforslagsko />
    </>
  );
}
