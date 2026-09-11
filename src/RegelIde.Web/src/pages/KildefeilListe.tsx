import { useCallback, useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Field, Heading, Label, Link, Paragraph, Select, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { KildefeilDto, KildefeilStatus, RettskildeSammendrag } from '../api/types';
import { useRettskildeoppslag } from '../kandidater/useNodeEtiketter';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { Metatekst } from '../entitet/Metatekst';

const STATUS_FARGE: Record<KildefeilStatus, 'neutral' | 'warning' | 'success' | 'info'> = {
  Ny: 'warning',
  Kjent: 'neutral',
  'Rettet-hos-oss': 'success',
  'Venter-på-Lovdata': 'info',
};

const ALLE_STATUSER: KildefeilStatus[] = ['Ny', 'Kjent', 'Rettet-hos-oss', 'Venter-på-Lovdata'];

function formaterTidspunkt(iso: string): string {
  return new Date(iso).toLocaleString('nb-NO', { dateStyle: 'short', timeStyle: 'short' });
}

/**
 * [Ny, issue #249, 2026-09-10] Synlig, VARIG liste over feil funnet i selve KILDEN (typisk Lovdata) —
 * ikke vår parsing, ikke vår import. Alternativ B (besluttet med Johann 2026-09-10): egen tabell, én
 * samlet søkeside, i stedet for en ren visning av ett enkelt endepunkts spørreresultat — se
 * `KildefeilEntitet` på serveren for hele resonnementet.
 * <para>
 * Denne siden er bevisst IKKE en kandidat-godkjenningskø (ingen godkjenn/avvis-widget som
 * NavnekandidaterListe/VirksomhetKandidaterListe) — en kildefeil forblir en feil uansett hva vi synes
 * om den. Statusfeltet (Ny/Kjent/Rettet-hos-oss/Venter-på-Lovdata) er OPPFØLGING, kun filtrerbart her i
 * denne runden (issue #249s akseptansekriterier krever filtrering, ikke en egen status-endre-UI —
 * lagt til som en naturlig utvidelse siden hvis/når det trengs).
 * </para>
 * <para>
 * «Kjør hjemmel-validering»-knappen kaller det GENERISKE registreringsendepunktet
 * (POST .../hjemmel-validering/registrer-kildefeil) — i dag den ENESTE mekanismen som skriver hit,
 * men UI-et her viser HELE registeret uansett hvilken `funnetAvMekanisme` en fremtidig rad har
 * (akseptansekriterium 3 — ingen ny side trengs for et nytt sveip/en ny validering).
 * </para>
 */
export default function KildefeilListe() {
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [statusFilter, setStatusFilter] = useState<KildefeilStatus | ''>('');

  const [kildefeil, setKildefeil] = useState<KildefeilDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [laster, setLaster] = useState(false);

  const [registrerer, setRegistrerer] = useState(false);
  const [registrerFeil, setRegistrerFeil] = useState<string | null>(null);
  const [sisteRegistrering, setSisteRegistrering] = useState<{ nye: number; totalt: number } | null>(null);

  const hentListe = useCallback(() => {
    setLaster(true);
    return api
      .hentKildefeil({ status: statusFilter || undefined })
      .then((rader) => {
        setKildefeil(rader);
        setFeil(null);
      })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kildefeil.'))
      .finally(() => setLaster(false));
  }, [statusFilter]);

  useEffect(() => {
    hentListe();
  }, [hentListe]);

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => {}); // ingen gjettet fallback — lenkene viser rå id hvis dette feiler
  }, []);

  const { tittel } = useRettskildeoppslag(rettskilder);

  async function kjorHjemmelValidering() {
    setRegistrerFeil(null);
    setRegistrerer(true);
    try {
      const resultat = await api.registrerKildefeilFraHjemmelValidering();
      setSisteRegistrering({ nye: resultat.nyeRegistrert, totalt: resultat.totaltRegistrertForMekanismen });
      await hentListe();
    } catch (err) {
      setRegistrerFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kjøring av hjemmel-validering.');
    } finally {
      setRegistrerer(false);
    }
  }

  const paginering = usePaginering(kildefeil ?? []);

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Kildefeil
      </Heading>
      <Paragraph style={{ marginBottom: '1.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Varig, samlet register over feil vi finner i selve KILDEN (typisk Lovdata) — ikke vår parsing,
        ikke vår import (issue #249). Ethvert sveip/enhver validering som oppdager en kildefeil skriver
        hit; denne siden viser hele registeret uansett hvilken mekanisme som fant funnet.
      </Paragraph>

      <Card style={{ padding: '1rem', marginBottom: '1rem' }} data-size="sm">
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Hjemmel-validering (issue #233)
        </Heading>
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
          Sjekker om hjemmelrelasjonene faktisk peker på bestemmelser som finnes, og registrerer hvert
          «node finnes ikke»-funn i lista under. Idempotent — kjør på nytt uten å duplisere kjente funn.
        </Metatekst>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <Button data-size="sm" onClick={kjorHjemmelValidering} disabled={registrerer}>
            {registrerer ? 'Kjører …' : 'Kjør hjemmel-validering'}
          </Button>
          {sisteRegistrering && !registrerer && (
            <Metatekst style={{ margin: 0, color: 'var(--ds-color-neutral-text-subtle)' }}>
              {sisteRegistrering.nye} nye registrert (totalt {sisteRegistrering.totalt} for denne mekanismen).
            </Metatekst>
          )}
        </div>
        {registrerFeil && (
          <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.75rem' }}>
            {registrerFeil}
          </Alert>
        )}
      </Card>

      <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
        <Field style={{ minWidth: '14rem' }}>
          <Label>Status</Label>
          <Select
            data-size="sm"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as KildefeilStatus | '')}
          >
            <Select.Option value="">Alle</Select.Option>
            {ALLE_STATUSER.map((s) => (
              <Select.Option key={s} value={s}>
                {s}
              </Select.Option>
            ))}
          </Select>
        </Field>
      </div>

      {feil && (
        <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.75rem' }}>
          {feil}
        </Alert>
      )}

      <Card style={{ padding: kildefeil && kildefeil.length > 0 ? 0 : '1rem', overflow: 'hidden' }} data-size="sm">
        {laster && !kildefeil && <Paragraph style={{ padding: '1rem', margin: 0 }}>Laster …</Paragraph>}
        {kildefeil && kildefeil.length === 0 && (
          <Paragraph style={{ margin: 0 }}>Ingen kildefeil registrert{statusFilter ? ` med status «${statusFilter}»` : ''} ennå.</Paragraph>
        )}
        {kildefeil && kildefeil.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <Table data-density="compact">
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Status</Table.HeaderCell>
                  <Table.HeaderCell>Rettskilde / node</Table.HeaderCell>
                  <Table.HeaderCell>Type</Table.HeaderCell>
                  <Table.HeaderCell>Beskrivelse</Table.HeaderCell>
                  <Table.HeaderCell>Funnet av</Table.HeaderCell>
                  <Table.HeaderCell>Tidspunkt</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {paginering.visteRader.map((k) => (
                  <Table.Row key={k.id}>
                    <Table.Cell>
                      <Tag data-color={STATUS_FARGE[k.status]} data-size="sm">
                        {k.status}
                      </Tag>
                    </Table.Cell>
                    <Table.Cell style={{ maxWidth: '18rem' }}>
                      <Link asChild>
                        <RouterLink
                          to={k.rettskildeEid ? rettskildeLenkeForId(k.rettskildeId, k.rettskildeEid) : `/rettskilder/${k.rettskildeId}`}
                        >
                          {tittel(k.rettskildeId)}
                          {k.rettskildeEid ? ` — ${k.rettskildeEid}` : ''}
                        </RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>{k.type}</Table.Cell>
                    <Table.Cell style={{ maxWidth: '28rem', whiteSpace: 'normal' }}>{k.beskrivelse}</Table.Cell>
                    <Table.Cell>{k.funnetAvMekanisme}</Table.Cell>
                    <Table.Cell>{formaterTidspunkt(k.opprettetTidspunkt)}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </div>
        )}
      </Card>
      {kildefeil && kildefeil.length > 0 && (
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
