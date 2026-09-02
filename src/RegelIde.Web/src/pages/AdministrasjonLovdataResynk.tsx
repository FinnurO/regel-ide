import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Button, Card, Field, Heading, Label, Paragraph, Select, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { LovdataResynkKjoringDto, LovdataResynkUtlost } from '../api/types';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';

const UTLOST_TEKST: Record<LovdataResynkUtlost, string> = {
  Oppstart: 'Ved oppstart',
  Manuell: 'Manuell',
  Planlagt: 'Planlagt',
};

const STATUS_FARGE: Record<string, 'neutral' | 'warning' | 'success' | 'danger'> = {
  Pågår: 'warning',
  Fullført: 'success',
  Feilet: 'danger',
};

type FrekvensPreset = 'aldri' | 'daglig' | 'ukentlig' | 'egendefinert';

/** Reverserer OppdaterLovdataResynkInnstillingRequest.intervallTimer til et av de tre faste presettene
 * pluss et fritt timeantall — se LovdataResynkPlanlegging.SkalKjoreNaa på serveren for selve modellen
 * (null/0 = aldri, ellers et fritt antall timer). */
function presetForIntervall(intervallTimer: number | null): { preset: FrekvensPreset; egendefinert: string } {
  if (intervallTimer === null || intervallTimer <= 0) return { preset: 'aldri', egendefinert: '' };
  if (intervallTimer === 24) return { preset: 'daglig', egendefinert: '' };
  if (intervallTimer === 168) return { preset: 'ukentlig', egendefinert: '' };
  return { preset: 'egendefinert', egendefinert: String(intervallTimer) };
}

function formaterTidspunkt(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('nb-NO', { dateStyle: 'short', timeStyle: 'medium' });
}

function formaterVarighet(startIso: string, sluttIso: string | null): string | null {
  if (!sluttIso) return null;
  const sekunder = Math.round((new Date(sluttIso).getTime() - new Date(startIso).getTime()) / 1000);
  if (sekunder < 60) return `${sekunder} sek`;
  const minutter = Math.floor(sekunder / 60);
  return `${minutter} min ${sekunder % 60} sek`;
}

/**
 * Administrasjon → Lovdata full-resynk (GitHub-issue #104): manuell trigger med status for pågående/
 * siste kjøring, database-lagret frekvensstyring for automatisk kjøring (LovdataResynkPlanleggerTjeneste
 * på serveren sjekker denne hver time), og en synlig kjøre-historikk med resultat-counts per kjøring.
 * <para>
 * `nyeVersjoner` vises som en EGEN, fremhevet kolonne (issue #104s eksplisitte ønske: gjør
 * endringsomfanget synlig) — det finnes IKKE noen godkjenningskø for disse treffene i dag (dagens
 * automatiske-aksept-oppførsel er beholdt uendret); det er en bevisst IKKE besluttet avveining, flagget
 * til Johann i stedet for overreach, se PR-beskrivelsen for #104.
 * </para>
 * <para>
 * Poller historikken hvert 3. sekund KUN mens siste kjente kjøring har status "Pågår" — se
 * `pollerRef`/effekten under. docs/09-design-konvensjoner.md §14: Card alltid rendret (tom-tilstand
 * inni), kompakt tetthet, egen historikktabell med usePaginering/Pagineringskontroll (§9.1).
 * </para>
 */
export default function AdministrasjonLovdataResynk() {
  const [historikk, setHistorikk] = useState<LovdataResynkKjoringDto[] | null>(null);
  const [hentFeil, setHentFeil] = useState<string | null>(null);

  const [starter, setStarter] = useState(false);
  const [startFeil, setStartFeil] = useState<string | null>(null);

  const [preset, setPreset] = useState<FrekvensPreset>('aldri');
  const [egendefinertTimer, setEgendefinertTimer] = useState('');
  const [sistEndretAv, setSistEndretAv] = useState<string | null>(null);
  const [innstillingLastet, setInnstillingLastet] = useState(false);
  const [lagrer, setLagrer] = useState(false);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);
  const [lagreOk, setLagreOk] = useState(false);

  const pollerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const hentHistorikk = useCallback(() => {
    return api
      .hentLovdataResynkHistorikk()
      .then((rader) => {
        setHistorikk(rader);
        setHentFeil(null);
      })
      .catch((e) => setHentFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kjøre-historikk.'));
  }, []);

  useEffect(() => {
    hentHistorikk();
    api
      .hentLovdataResynkInnstilling()
      .then((i) => {
        const { preset: p, egendefinert } = presetForIntervall(i.intervallTimer);
        setPreset(p);
        setEgendefinertTimer(egendefinert);
        setSistEndretAv(i.sistEndretAv);
        setInnstillingLastet(true);
      })
      .catch(() => {}); // ingen gjettet fallback — skjemaet viser bare standardverdien "Aldri" hvis dette feiler
  }, [hentHistorikk]);

  // Poll KUN mens den nyeste kjøringen fortsatt pågår (manuelt trigget her, ELLER trigget av oppstart/
  // den planlagte bakgrunnsjobben et annet sted — historikken skal uansett oppdatere seg selv da).
  useEffect(() => {
    const pagar = historikk && historikk.length > 0 && historikk[0].status === 'Pågår';
    if (pagar && !pollerRef.current) {
      pollerRef.current = setInterval(hentHistorikk, 3000);
    } else if (!pagar && pollerRef.current) {
      clearInterval(pollerRef.current);
      pollerRef.current = null;
    }
    return () => {
      if (pollerRef.current) {
        clearInterval(pollerRef.current);
        pollerRef.current = null;
      }
    };
  }, [historikk, hentHistorikk]);

  async function start() {
    setStartFeil(null);
    setStarter(true);
    try {
      const ny = await api.startLovdataResynk();
      setHistorikk((forrige) => [ny, ...(forrige ?? [])]);
    } catch (err) {
      setStartFeil(
        err instanceof ApiError
          ? err.status === 409
            ? 'En kjøring pågår allerede — vent til den er ferdig.'
            : err.message
          : 'Ukjent feil ved start av resynk.',
      );
    } finally {
      setStarter(false);
    }
  }

  async function lagreFrekvens() {
    setLagreFeil(null);
    setLagreOk(false);
    setLagrer(true);
    try {
      const intervallTimer =
        preset === 'aldri' ? null : preset === 'daglig' ? 24 : preset === 'ukentlig' ? 168 : Number(egendefinertTimer);
      const oppdatert = await api.oppdaterLovdataResynkInnstilling({ intervallTimer });
      setSistEndretAv(oppdatert.sistEndretAv);
      setLagreOk(true);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av frekvens.');
    } finally {
      setLagrer(false);
    }
  }

  const egendefinertGyldig = preset !== 'egendefinert' || (/^\d+$/.test(egendefinertTimer) && Number(egendefinertTimer) > 0);

  const siste = historikk && historikk.length > 0 ? historikk[0] : null;
  const pagaende = siste?.status === 'Pågår' ? siste : null;

  const paginering = usePaginering(historikk ?? []);

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Lovdata full-resynk
      </Heading>
      <Paragraph style={{ marginBottom: '1.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Full synkronisering av alle lover og sentrale forskrifter fra Lovdatas bulk-arkiv (GitHub-issue #104)
        — manuell trigger, automatisk frekvens, og historikk over tidligere kjøringer.
      </Paragraph>

      <Card style={{ padding: '1rem', marginBottom: '1rem' }} data-size="sm">
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Kjør nå
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
          Starter en full runde i bakgrunnen — kan ta flere minutter over hele korpuset. Siden venter ikke
          på at kjøringen er ferdig; status oppdateres automatisk under mens den pågår.
        </Paragraph>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <Button data-size="sm" onClick={start} disabled={starter || pagaende !== null}>
            {starter ? 'Starter …' : 'Kjør full resynk'}
          </Button>
          {pagaende && (
            <Tag data-color={STATUS_FARGE[pagaende.status]} data-size="sm">
              Pågår — startet {formaterTidspunkt(pagaende.startetTidspunkt)}
            </Tag>
          )}
          {!pagaende && siste && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', margin: 0, color: 'var(--ds-color-neutral-text-subtle)' }}>
              Siste kjøring: {formaterTidspunkt(siste.startetTidspunkt)} ({UTLOST_TEKST[siste.utlost]}) —{' '}
              <Tag data-color={STATUS_FARGE[siste.status]} data-size="sm">
                {siste.status}
              </Tag>
            </Paragraph>
          )}
          {!pagaende && !siste && historikk && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', margin: 0, color: 'var(--ds-color-neutral-text-subtle)' }}>
              Ingen kjøring registrert ennå.
            </Paragraph>
          )}
        </div>
        {startFeil && (
          <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.75rem' }}>
            {startFeil}
          </Alert>
        )}
      </Card>

      <Card style={{ padding: '1rem', marginBottom: '1rem' }} data-size="sm">
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Automatisk frekvens
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
          Hvor ofte resynk skal kjøre automatisk, i tillegg til den manuelle knappen over og kjøringen ved
          hver app-oppstart. Sjekkes hver time — en endring her tar altså inntil en time å tre i kraft.
        </Paragraph>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <Field style={{ minWidth: '12rem' }}>
            <Label>Frekvens</Label>
            <Select data-size="sm" value={preset} onChange={(e) => setPreset(e.target.value as FrekvensPreset)} disabled={!innstillingLastet}>
              <Select.Option value="aldri">Aldri (kun manuell/oppstart)</Select.Option>
              <Select.Option value="daglig">Daglig</Select.Option>
              <Select.Option value="ukentlig">Ukentlig</Select.Option>
              <Select.Option value="egendefinert">Egendefinert (timer)</Select.Option>
            </Select>
          </Field>
          {preset === 'egendefinert' && (
            <Textfield
              data-size="sm"
              label="Antall timer"
              inputMode="numeric"
              style={{ maxWidth: '8rem' }}
              value={egendefinertTimer}
              onChange={(e) => setEgendefinertTimer(e.target.value.replace(/[^\d]/g, ''))}
              error={!egendefinertGyldig ? 'Må være et positivt heltall.' : undefined}
            />
          )}
          <Button data-size="sm" onClick={lagreFrekvens} disabled={lagrer || !innstillingLastet || !egendefinertGyldig}>
            {lagrer ? 'Lagrer …' : 'Lagre frekvens'}
          </Button>
          {lagreOk && !lagrer && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', margin: 0, color: 'var(--ds-color-success-text-default)' }}>
              Lagret.
            </Paragraph>
          )}
        </div>
        {sistEndretAv && (
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.5rem', marginBottom: 0 }}>
            Sist endret av {sistEndretAv}.
          </Paragraph>
        )}
        {lagreFeil && (
          <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.75rem' }}>
            {lagreFeil}
          </Alert>
        )}
      </Card>

      <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
        Kjøre-historikk
      </Heading>
      {hentFeil && (
        <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.75rem' }}>
          {hentFeil}
        </Alert>
      )}
      <Card style={{ padding: historikk && historikk.length > 0 ? 0 : '1rem', overflow: 'hidden' }} data-size="sm">
        {!historikk && !hentFeil && <Paragraph style={{ padding: '1rem', margin: 0 }}>Laster …</Paragraph>}
        {historikk && historikk.length === 0 && <Paragraph style={{ margin: 0 }}>Ingen kjøringer registrert ennå.</Paragraph>}
        {historikk && historikk.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <Table data-density="compact">
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Startet</Table.HeaderCell>
                  <Table.HeaderCell>Utløst</Table.HeaderCell>
                  <Table.HeaderCell>Status</Table.HeaderCell>
                  <Table.HeaderCell>Varighet</Table.HeaderCell>
                  <Table.HeaderCell>Nye</Table.HeaderCell>
                  <Table.HeaderCell>Nye versjoner</Table.HeaderCell>
                  <Table.HeaderCell>Uendret</Table.HeaderCell>
                  <Table.HeaderCell>Feilet (dok.)</Table.HeaderCell>
                  <Table.HeaderCell>Totalt</Table.HeaderCell>
                  <Table.HeaderCell>Feilmelding</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {paginering.visteRader.map((k) => (
                  <Table.Row key={k.id}>
                    <Table.Cell>{formaterTidspunkt(k.startetTidspunkt)}</Table.Cell>
                    <Table.Cell>
                      {UTLOST_TEKST[k.utlost]}
                      {k.utlostAvBruker ? ` (${k.utlostAvBruker})` : ''}
                    </Table.Cell>
                    <Table.Cell>
                      <Tag data-color={STATUS_FARGE[k.status]} data-size="sm">
                        {k.status}
                      </Tag>
                    </Table.Cell>
                    <Table.Cell>{formaterVarighet(k.startetTidspunkt, k.fullfortTidspunkt) ?? '—'}</Table.Cell>
                    <Table.Cell>{k.nye ?? '—'}</Table.Cell>
                    <Table.Cell>
                      {/* Fremhevet med vilje (issue #104: "vise TYDELIG i historikken hvor mange dokumenter
                          som faktisk fikk en NyVersjon") — det er den ene telleren som betyr et dokument
                          faktisk ENDRET INNHOLD, ikke bare ble oppdaget/re-sett uendret. */}
                      {k.nyeVersjoner !== null && k.nyeVersjoner > 0 ? (
                        <Tag data-color="info" data-size="sm">
                          {k.nyeVersjoner}
                        </Tag>
                      ) : (
                        (k.nyeVersjoner ?? '—')
                      )}
                    </Table.Cell>
                    <Table.Cell>{k.uendret ?? '—'}</Table.Cell>
                    <Table.Cell>{k.feilet ?? '—'}</Table.Cell>
                    <Table.Cell>{k.totaltBehandlet ?? '—'}</Table.Cell>
                    <Table.Cell style={{ maxWidth: '20rem', whiteSpace: 'normal' }}>{k.feilmelding ?? '—'}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </div>
        )}
      </Card>
      {historikk && historikk.length > 0 && (
        <Pagineringskontroll
          side={paginering.side}
          settSide={paginering.settSide}
          sidestorrelse={paginering.sidestorrelse}
          settSidestorrelse={paginering.settSidestorrelse}
          totaltAntallSider={paginering.totaltAntallSider}
          totaltAntallRader={paginering.totaltAntallRader}
        />
      )}
    </>
  );
}
