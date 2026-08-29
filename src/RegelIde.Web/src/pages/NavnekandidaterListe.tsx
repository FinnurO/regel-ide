import { useEffect, useMemo, useRef, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Button, Card, Field, Heading, Label, Link, Paragraph, Select, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import type { NavnekandidatDto, RettskildeSammendrag } from '../api/types';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';

type Sorteringskolonne = 'kategori' | 'rettskilde' | 'status' | 'opprettet';

const STATUS_FARGE: Record<string, 'neutral' | 'warning' | 'success' | 'danger'> = {
  Venter: 'warning',
  Godkjent: 'success',
  Avvist: 'danger',
};

const KATEGORI_FARGE: Record<string, 'info' | 'accent'> = {
  virksomhet: 'accent',
  rolle: 'info',
};

/**
 * Oppdagelseskø (docs/13-backlog.md §9) — komplementær til `VirksomhetKandidaterListe.tsx`, samme
 * mønster tett fulgt (sveip-panel + filtrerbar tabell + godkjenn/avvis per rad). Den avgjørende
 * forskjellen fra virksomhetskandidatene er hva "godkjenn" faktisk gjør, se `kandidatHandlingTekst`:
 * for `"rolle"` opprettes et EKTE rollebegrep direkte (serversiden har alt den trenger), for
 * `"virksomhet"` settes kun status — selve virksomhetskoblingen (ny ELLER eksisterende virksomhet)
 * krever et menneske og skjer via Brreg-søket/"opprett med bare navn"-skjemaet på `/virksomheter`
 * (lenken under sender med `?forslagNavn=` som forhåndsutfyller begge der).
 */
export default function NavnekandidaterListe() {
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);

  const [rettskildeFilter, setRettskildeFilter] = useState('');
  const [kategoriFilter, setKategoriFilter] = useState<'virksomhet' | 'rolle' | ''>('');
  const [statusFilter, setStatusFilter] = useState<'Venter' | 'Godkjent' | 'Avvist' | 'Alle'>('Venter');

  const [kandidater, setKandidater] = useState<NavnekandidatDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [laster, setLaster] = useState(false);

  const [sveipRettskildeId, setSveipRettskildeId] = useState('');
  const [sveiper, setSveiper] = useState(false);
  const [sveipFeil, setSveipFeil] = useState<string | null>(null);
  const [sveipResultat, setSveipResultat] = useState<{ funnet: number; nye: number } | null>(null);

  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('opprettet');
  const [sortStigende, setSortStigende] = useState(false);

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);

  // Samme "kun siste utstedte forespørsel får sette state"-vern som VirksomhetKandidaterListe.tsx —
  // se den filens kommentar for hele resonnementet (rask filterbytte kan ellers la et treg, eldre
  // svar overskrive et nyere).
  const sisteForesporsel = useRef(0);

  function lastKandidater() {
    const denneForesporselen = ++sisteForesporsel.current;
    setLaster(true);
    setFeil(null);
    api
      .hentNavnekandidater({
        rettskildeId: rettskildeFilter || undefined,
        kategori: kategoriFilter || undefined,
        status: statusFilter,
      })
      .then((liste) => {
        if (denneForesporselen !== sisteForesporsel.current) return;
        setKandidater(liste);
      })
      .catch((e) => {
        if (denneForesporselen !== sisteForesporsel.current) return;
        setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kandidater.');
      })
      .finally(() => {
        if (denneForesporselen === sisteForesporsel.current) setLaster(false);
      });
  }

  useEffect(lastKandidater, [rettskildeFilter, kategoriFilter, statusFilter]);

  const rettskilderPerId = useMemo(() => new Map(rettskilder.map((r) => [r.id, r] as const)), [rettskilder]);
  function visRettskilde(rettskildeId: string): string {
    return rettskilderPerId.get(rettskildeId)?.kortnavn ?? rettskilderPerId.get(rettskildeId)?.tittel ?? rettskildeId;
  }

  async function kjorSveip() {
    setSveiper(true);
    setSveipFeil(null);
    setSveipResultat(null);
    try {
      const resultat = await api.sveipNavnekandidater({ rettskildeId: sveipRettskildeId || null });
      setSveipResultat({ funnet: resultat.antallTreffFunnet, nye: resultat.antallNyeKandidater });
      lastKandidater();
    } catch (err) {
      setSveipFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sveip.');
    } finally {
      setSveiper(false);
    }
  }

  async function enkelthandling(id: string, handling: 'godkjenn' | 'avvis') {
    try {
      if (handling === 'godkjenn') await api.godkjennNavnekandidat(id);
      else await api.avvisNavnekandidat(id);
      lastKandidater();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved behandling av kandidat.');
    }
  }

  function bytteSortering(kolonne: Sorteringskolonne) {
    if (sortKolonne === kolonne) setSortStigende((s) => !s);
    else {
      setSortKolonne(kolonne);
      setSortStigende(true);
    }
  }
  function sorteringsindikator(kolonne: Sorteringskolonne) {
    if (sortKolonne !== kolonne) return '';
    return sortStigende ? ' ▲' : ' ▼';
  }

  const viste = useMemo(() => {
    if (!kandidater) return null;
    const sortnokkel = (k: NavnekandidatDto) =>
      sortKolonne === 'kategori'
        ? k.kategori
        : sortKolonne === 'rettskilde'
          ? visRettskilde(k.rettskildeId)
          : sortKolonne === 'status'
            ? k.status
            : k.opprettetTidspunkt;
    return [...kandidater].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kandidater, sortKolonne, sortStigende, rettskilderPerId]);

  const paginering = usePaginering(viste ?? []);

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Navnekandidater
      </Heading>
      <Paragraph style={{ marginBottom: '1.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Egennavn/juridiske aktører oppdaget ved regex-mønstergjenkjenning i allerede importert
        rettskildetekst (docs/13-backlog.md §9) — ren tekstanalyse, ikke KI. Komplementær til{' '}
        <Link asChild><RouterLink to="/virksomhet-kandidater">Virksomhetskandidater</RouterLink></Link>,
        som bekrefter FLERE forekomster av allerede kjente navn; dette er en oppdagelseskø for HELT NYE
        navn ingen registrert navneform/rollebegrep dekker ennå.
      </Paragraph>

      <Card style={{ padding: '1rem', marginBottom: '1.5rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Kjør sveip
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginBottom: '0.5rem' }}>
          Ingen rettskilde valgt = hele det importerte korpuset. Dekningen er begrenset til det som
          faktisk er importert, ikke alle norske lover/forskrifter.
        </Paragraph>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <Field style={{ minWidth: '20rem' }}>
            <Label>Rettskilde (valgfritt)</Label>
            <Select data-size="sm" value={sveipRettskildeId} onChange={(e) => setSveipRettskildeId(e.target.value)}>
              <Select.Option value="">Hele korpuset</Select.Option>
              {rettskilder.map((r) => (
                <Select.Option key={r.id} value={r.id}>{r.kortnavn ?? r.tittel}</Select.Option>
              ))}
            </Select>
          </Field>
          <Button onClick={kjorSveip} disabled={sveiper}>
            {sveiper ? 'Sveiper …' : 'Kjør sveip'}
          </Button>
        </div>
        {sveipFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{sveipFeil}</div>}
        {sveipResultat && (
          <div className="infomelding" style={{ marginTop: '0.5rem' }}>
            Fant {sveipResultat.funnet} treff totalt, {sveipResultat.nye} nye kandidater lagt i køen.
          </div>
        )}
      </Card>

      <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Field style={{ minWidth: '18rem' }}>
          <Label>Rettskilde</Label>
          <Select data-size="sm" value={rettskildeFilter} onChange={(e) => setRettskildeFilter(e.target.value)}>
            <Select.Option value="">Alle rettskilder</Select.Option>
            {rettskilder.map((r) => (
              <Select.Option key={r.id} value={r.id}>{r.kortnavn ?? r.tittel}</Select.Option>
            ))}
          </Select>
        </Field>
        <Field style={{ minWidth: '12rem' }}>
          <Label>Kategori</Label>
          <Select data-size="sm" value={kategoriFilter} onChange={(e) => setKategoriFilter(e.target.value as typeof kategoriFilter)}>
            <Select.Option value="">Alle kategorier</Select.Option>
            <Select.Option value="virksomhet">Virksomhet</Select.Option>
            <Select.Option value="rolle">Rolle</Select.Option>
          </Select>
        </Field>
        <Field style={{ minWidth: '10rem' }}>
          <Label>Status</Label>
          <Select data-size="sm" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}>
            <Select.Option value="Venter">Venter</Select.Option>
            <Select.Option value="Godkjent">Godkjent</Select.Option>
            <Select.Option value="Avvist">Avvist</Select.Option>
            <Select.Option value="Alle">Alle</Select.Option>
          </Select>
        </Field>
      </div>

      {feil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{feil}</div>}
      {laster && !kandidater && <Paragraph>Laster …</Paragraph>}
      {viste && viste.length === 0 && <Paragraph>Ingen kandidater matcher filteret.</Paragraph>}

      {viste && viste.length > 0 && (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <Table>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('kategori')}>
                      Kategori{sorteringsindikator('kategori')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Foreslått tekst</Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('rettskilde')}>
                      Lov/forskrift{sorteringsindikator('rettskilde')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Node</Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('status')}>
                      Status{sorteringsindikator('status')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Handling</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {paginering.visteRader.map((k) => (
                  <Table.Row key={k.id}>
                    <Table.Cell>
                      <Tag data-color={KATEGORI_FARGE[k.kategori] ?? 'neutral'} data-size="sm">{k.kategori}</Tag>
                    </Table.Cell>
                    <Table.Cell style={{ fontWeight: 500 }}>{k.foreslattTekst}</Table.Cell>
                    <Table.Cell>{visRettskilde(k.rettskildeId)}</Table.Cell>
                    <Table.Cell style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>
                      {(() => {
                        const href = rettskildeLenke(k.nodeEid, rettskilder);
                        return href ? <Link asChild><RouterLink to={href} target="_blank">{k.nodeEid} ↗</RouterLink></Link> : k.nodeEid;
                      })()}
                    </Table.Cell>
                    <Table.Cell>
                      <Tag data-color={STATUS_FARGE[k.status] ?? 'neutral'} data-size="sm">{k.status}</Tag>
                    </Table.Cell>
                    <Table.Cell>
                      {k.status === 'Venter' ? (
                        <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', alignItems: 'center' }}>
                          <Button data-size="sm" onClick={() => enkelthandling(k.id, 'godkjenn')}>Godkjenn</Button>
                          <Button data-size="sm" variant="tertiary" onClick={() => enkelthandling(k.id, 'avvis')}>Avvis</Button>
                          {k.kategori === 'virksomhet' && (
                            <Link asChild>
                              <RouterLink to={`/virksomheter?forslagNavn=${encodeURIComponent(k.foreslattTekst)}`} target="_blank">
                                Finn/opprett virksomhet ↗
                              </RouterLink>
                            </Link>
                          )}
                        </div>
                      ) : (
                        <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                          {k.behandletAv ? `Behandlet av ${k.behandletAv}` : '—'}
                        </span>
                      )}
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </div>
        </Card>
      )}

      {viste && viste.length > 0 && <Pagineringskontroll {...paginering} />}
    </>
  );
}
