import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Alert, Button, Checkbox, Field, Heading, Label, Link, Paragraph, Select, Spinner, Table, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { KunnskapsbibliotekFilDto, KunnskapsbibliotekLenkeDto, MittForslagDto, RettskildeSammendrag, TjenesteforslagDto } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';
import { RettskildeFlervalg } from '../rettskilde/RettskildeFlervalg';

/**
 * «Identifiser tjenester» (byggesteg 5 runde 1, docs/06-veikart.md) — foreslår nye Tjeneste-objekter
 * fra valgte rettskilder pluss virksomhetens registrerte kunnskapsbibliotek-lenker (nettside o.l.).
 * Bevisst ikke avhengig av at noe Tjeneste-objekt finnes fra før. Kjører mot en STUB-KI
 * (KiAgentKlientStub i RegelIde.Data) — se merknaden under kø-listen.
 */
export default function TjenesteforslagKo() {
  const navigate = useNavigate();
  const { gjeldendeBruker } = useBruker();

  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [valgteRettskilder, setValgteRettskilder] = useState<Set<string>>(new Set());
  const [lenker, setLenker] = useState<KunnskapsbibliotekLenkeDto[]>([]);
  const [nyUrl, setNyUrl] = useState('');
  const [nyBeskrivelse, setNyBeskrivelse] = useState('');
  const [leggerTilLenke, setLeggerTilLenke] = useState(false);
  const [filer, setFiler] = useState<KunnskapsbibliotekFilDto[]>([]);
  const [nyFilTittel, setNyFilTittel] = useState('');
  const [lasterOppFil, setLasterOppFil] = useState(false);
  const filInputRef = useRef<HTMLInputElement>(null);
  const [ko, setKo] = useState<TjenesteforslagDto[] | null>(null);
  // [Ny, 2026-08-29] Motstykket til `ko` over — tjenester DENNE virksomheten selv har foreslått til
  // en annen, fortsatt ubehandlet der. Oppdaget som et reelt hull under opprydding etter en
  // test-import: `ko` viser kun det egen virksomhet EIER, så et forslag man selv har SENDT ut var
  // usynlig for importøren igjen — ingen UI-vei til å bruke SlettForslagAsync sin allerede
  // eksisterende "opprinnelig foreslagsstiller"-tilgang.
  const [mineForslag, setMineForslag] = useState<MittForslagDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kjorer, setKjorer] = useState(false);

  // Massehandling på "Ventende forslag" (2026-08-30) — store test-import-/-sveip-mengder gjør
  // enkeltrad-behandling upraktisk, samme UX/backend-mønster som VirksomhetKandidaterListe.tsx.
  const [valgte, setValgte] = useState<Set<string>>(new Set());
  const [massehandlingKjorer, setMassehandlingKjorer] = useState(false);
  const [massehandlingFeil, setMassehandlingFeil] = useState<string | null>(null);

  // [Ny, 2026-08-30] Samme massehandling-mønster for "Mine forslag til andre virksomheter" —
  // egen valgt-mengde/feilstate siden det er en helt separat tabell/handling (Slett, ikke
  // Godkjenn/Avvis) med egne id-er fra en annen liste.
  const [valgteMine, setValgteMine] = useState<Set<string>>(new Set());
  const [massesletterMine, setMassesletterMine] = useState(false);
  const [massesletterMineFeil, setMassesletterMineFeil] = useState<string | null>(null);
  const [omfang, setOmfang] = useState<'tjeneste' | 'full'>('tjeneste');
  const [sisteKjoring, setSisteKjoring] = useState<{
    melding: string | null; inputTokens: number | null; outputTokens: number | null; antallHandlinger: number | null;
  } | null>(null);

  function lastLenker() {
    api.hentKunnskapsbibliotekLenker().then(setLenker).catch(() => setLenker([]));
  }

  function lastFiler() {
    api.hentKunnskapsbibliotekFiler().then(setFiler).catch(() => setFiler([]));
  }

  function lastKo() {
    api.hentTjenesteforslagKo()
      .then((liste) => { setKo(liste); setValgte(new Set()); }) // Ny liste — forrige utvalg gjelder ikke lenger.
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kø.'));
  }

  function lastMineForslag() {
    api.hentMineForslagTilAndreVirksomheter()
      .then((liste) => { setMineForslag(liste); setValgteMine(new Set()); })
      .catch(() => setMineForslag([]));
  }

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
    lastLenker();
    lastFiler();
    lastKo();
    lastMineForslag();
  }, []);

  async function leggTilLenke(e: FormEvent) {
    e.preventDefault();
    setFeil(null);
    setLeggerTilLenke(true);
    try {
      await api.leggTilKunnskapsbibliotekLenke({ url: nyUrl.trim(), beskrivelse: nyBeskrivelse.trim() || null });
      setNyUrl('');
      setNyBeskrivelse('');
      lastLenker();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av lenke.');
    } finally {
      setLeggerTilLenke(false);
    }
  }

  async function slettLenke(id: string) {
    await api.slettKunnskapsbibliotekLenke(id);
    lastLenker();
  }

  async function lastOppFil(e: ChangeEvent<HTMLInputElement>) {
    const fil = e.target.files?.[0];
    if (!fil) return;
    setFeil(null);
    setLasterOppFil(true);
    try {
      await api.lastOppKunnskapsbibliotekFil(fil, nyFilTittel);
      setNyFilTittel('');
      lastFiler();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opplasting av fil.');
    } finally {
      setLasterOppFil(false);
      if (filInputRef.current) filInputRef.current.value = '';
    }
  }

  async function slettFil(id: string) {
    await api.slettKunnskapsbibliotekFil(id);
    lastFiler();
  }

  async function kjorForslag() {
    setFeil(null);
    setSisteKjoring(null);
    setKjorer(true);
    try {
      // Omfang "full" (handlingsforslag-ki-omfang-runden) ber KI-en fylle BÅDE Tjeneste-formen og
      // Handlinger under den i samme kall — samme "ventende forslag"-tabell under viser tjenesten
      // uansett omfang (den leser kun status="foreslatt_av_ai", ikke hvilket endepunkt som opprettet
      // den), så bare selve KI-kallet og antall-handlinger-meldingen skiller de to omfangene her.
      if (omfang === 'full') {
        const respons = await api.kjorFullTjenesteforslag({ rettskildeIder: [...valgteRettskilder], omfang: 'full' });
        const antallHandlinger = respons.forslag.reduce((sum, f) => sum + f.handlinger.length, 0);
        setSisteKjoring({ melding: respons.melding, inputTokens: respons.inputTokens, outputTokens: respons.outputTokens, antallHandlinger });
      } else {
        const respons = await api.kjorTjenesteforslag({ rettskildeIder: [...valgteRettskilder] });
        setSisteKjoring({ melding: respons.melding, inputTokens: respons.inputTokens, outputTokens: respons.outputTokens, antallHandlinger: null });
      }
      lastKo();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kjøring av KI-forslag.');
    } finally {
      setKjorer(false);
    }
  }

  async function avvis(id: string) {
    await api.settTjenesteStatus(id, { status: 'utkast' });
    lastKo();
  }

  /** [Ny, 2026-08-28] Hard-sletter forslaget — til forskjell fra `avvis` (tilbakestiller kun status,
   * beholder innholdet for senere vurdering) fjerner denne det helt. Til opprydding etter en
   * import-test (69+ rettigheter) eller et åpenbart søppel-KI-forslag ingen ønsker å beholde spor av. */
  async function slett(id: string) {
    await api.slettTjenesteforslag(id);
    lastKo();
  }

  async function rediger(id: string) {
    await api.settTjenesteStatus(id, { status: 'under_revisjon' });
    navigate(`/tjenester/${id}`);
  }

  async function godkjenn(id: string) {
    await api.settTjenesteStatus(id, { status: 'validert', godkjentAv: gjeldendeBruker?.navn });
    lastKo();
  }

  /** [Ny, 2026-08-29] Angre et EGET tverr-virksomhet-forslag som fortsatt står ubehandlet hos
   * mål-virksomheten — se `mineForslag`-kommentaren over. */
  async function slettMittForslag(id: string) {
    await api.slettTjenesteforslag(id);
    lastMineForslag();
  }

  function vekslValgt(id: string, valgt: boolean) {
    setValgte((forrige) => {
      const ny = new Set(forrige);
      if (valgt) ny.add(id); else ny.delete(id);
      return ny;
    });
  }

  // Ingen paginering på denne siden — "alle viste" er dermed hele "Ventende forslag"-tabellen.
  function vekslAlleViste(valgt: boolean) {
    setValgte(valgt ? new Set((ko ?? []).map((f) => f.tjeneste.id)) : new Set());
  }

  async function massehandling(handling: 'godkjenn' | 'avvis') {
    if (valgte.size === 0) return;
    setMassehandlingKjorer(true);
    setMassehandlingFeil(null);
    try {
      const request = { ider: [...valgte] };
      const resultat = handling === 'godkjenn'
        ? await api.godkjennTjenesteforslagBatch(request)
        : await api.avvisTjenesteforslagBatch(request);
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

  function vekslValgtMitt(id: string, valgt: boolean) {
    setValgteMine((forrige) => {
      const ny = new Set(forrige);
      if (valgt) ny.add(id); else ny.delete(id);
      return ny;
    });
  }

  function vekslAlleVisteMine(valgt: boolean) {
    setValgteMine(valgt ? new Set((mineForslag ?? []).map((f) => f.tjeneste.id)) : new Set());
  }

  /** Samme SlettForslagAsync-endepunkt som enkeltrad-`slettMittForslag`/`slett` over — se
   * TjenesteforslagSlettBatchResultatDto-kommentaren på backend for hvorfor de to seksjonene deler ett
   * batch-endepunkt selv om de viser to forskjellige lister. */
  async function massesletteMine() {
    if (valgteMine.size === 0) return;
    setMassesletterMine(true);
    setMassesletterMineFeil(null);
    try {
      const resultat = await api.slettTjenesteforslagBatch({ ider: [...valgteMine] });
      const feilede = resultat.rader.filter((r) => !r.ok);
      if (feilede.length > 0) {
        setMassesletterMineFeil(
          `${feilede.length} av ${resultat.rader.length} rad(er) feilet: ${feilede.map((r) => r.feil).join('; ')}`,
        );
      }
      lastMineForslag();
    } catch (err) {
      setMassesletterMineFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved massesletting.');
    } finally {
      setMassesletterMine(false);
    }
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Identifiser tjenester
      </Heading>
      <Paragraph style={{ marginBottom: '1.5rem', maxWidth: '40rem' }}>
        Velg rettskilder og/eller registrer lenker til virksomhetens nettside o.l. — agenten foreslår
        nye Tjeneste-objekter. Forslag lander alltid som «foreslått av KI», og må godkjennes eksplisitt
        før de blir gjeldende.
      </Paragraph>

      <Heading level={2} data-size="sm">Kunnskapsbibliotek (lenker og filer)</Heading>
      <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginBottom: '0.5rem' }}>
        Nettside, PDF eller Word-dokument som beskriver hva virksomheten leverer av tjenester.
      </Paragraph>
      {lenker.length > 0 && (
        <ul style={{ marginBottom: '0.75rem' }}>
          {lenker.map((l) => (
            <li key={l.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.2rem' }}>
              <Link href={l.url} target="_blank" rel="noreferrer">{l.beskrivelse ?? l.url}</Link>
              <Button variant="tertiary" data-size="sm" onClick={() => slettLenke(l.id)}>Fjern</Button>
            </li>
          ))}
        </ul>
      )}
      <form onSubmit={leggTilLenke} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '1rem' }}>
        <Textfield label="URL" placeholder="https://…" value={nyUrl} onChange={(e) => setNyUrl(e.target.value)} required />
        <Textfield label="Beskrivelse (valgfri)" value={nyBeskrivelse} onChange={(e) => setNyBeskrivelse(e.target.value)} />
        <Button type="submit" disabled={leggerTilLenke || !nyUrl.trim()}>
          {leggerTilLenke ? 'Legger til …' : 'Legg til lenke'}
        </Button>
      </form>

      {filer.length > 0 && (
        <ul style={{ marginBottom: '0.75rem' }}>
          {filer.map((f) => (
            <li key={f.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.2rem' }}>
              <span>{f.tittel ?? f.filnavn} ({f.filtype})</span>
              <Button variant="tertiary" data-size="sm" onClick={() => slettFil(f.id)}>Fjern</Button>
            </li>
          ))}
        </ul>
      )}
      <div style={{ marginBottom: '1.5rem' }}>
        <Textfield
          label="Tittel (valgfri)"
          value={nyFilTittel}
          onChange={(e) => setNyFilTittel(e.target.value)}
          style={{ maxWidth: '25rem', marginBottom: '0.5rem' }}
        />
        <Textfield
          type="file"
          ref={filInputRef}
          label="Last opp PDF eller Word (.docx) — avvises hvis filen mangler tekstlag (f.eks. et rent skann)"
          accept=".pdf,.docx"
          disabled={lasterOppFil}
          onChange={lastOppFil}
        />
        {lasterOppFil && <Spinner aria-label="Laster opp …" data-size="xs" />}
      </div>

      {rettskilder.length > 0 && (
        <div style={{ marginBottom: '1rem' }}>
          <RettskildeFlervalg rettskilder={rettskilder} valgte={valgteRettskilder} onChange={setValgteRettskilder} />
          <Field style={{ maxWidth: '20rem', marginBottom: '0.75rem' }}>
            <Label>Omfang</Label>
            <Select data-size="sm" value={omfang} onChange={(e) => setOmfang(e.target.value as 'tjeneste' | 'full')}>
              <Select.Option value="tjeneste">Bare tjeneste</Select.Option>
              <Select.Option value="full">Tjeneste + handlinger (ett kall)</Select.Option>
            </Select>
          </Field>
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
              {sisteKjoring.antallHandlinger !== null && ` ${sisteKjoring.antallHandlinger} handling(er) foreslått under tjenesten(e).`}
            </Paragraph>
          )}
        </div>
      )}

      <Heading level={2} data-size="sm" style={{ marginTop: '1.5rem' }}>
        Ventende forslag
      </Heading>
      {!ko && <Spinner aria-label="Laster …" data-size="sm" />}
      {ko && ko.length === 0 && <Paragraph>Ingen ventende tjenesteforslag.</Paragraph>}
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
                    checked={ko.length > 0 && ko.every((f) => valgte.has(f.tjeneste.id))}
                    onChange={(e) => vekslAlleViste(e.target.checked)}
                  />
                </Table.HeaderCell>
                <Table.HeaderCell>Tittel</Table.HeaderCell>
                <Table.HeaderCell>Beskrivelse</Table.HeaderCell>
                <Table.HeaderCell>KI-versjon</Table.HeaderCell>
                <Table.HeaderCell>Handlinger</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {ko.map((f) => (
                <Table.Row key={f.tjeneste.id}>
                  <Table.Cell>
                    <Checkbox
                      aria-label={`Velg forslag ${f.tjeneste.tittel}`}
                      checked={valgte.has(f.tjeneste.id)}
                      onChange={(e) => vekslValgt(f.tjeneste.id, e.target.checked)}
                    />
                  </Table.Cell>
                  <Table.Cell>
                    <Link asChild><RouterLink to={`/tjenester/${f.tjeneste.id}`}>{f.tjeneste.tittel}</RouterLink></Link>
                  </Table.Cell>
                  <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{f.tjeneste.beskrivelse ?? '—'}</Table.Cell>
                  <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{f.aiForslagVersjon ?? '—'}</Table.Cell>
                  <Table.Cell>
                    <div style={{ display: 'flex', gap: '0.4rem' }}>
                      <Button variant="tertiary" data-size="sm" onClick={() => avvis(f.tjeneste.id)}>Avvis</Button>
                      <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => slett(f.tjeneste.id)}>Slett</Button>
                      <Button variant="tertiary" data-size="sm" onClick={() => rediger(f.tjeneste.id)}>Rediger</Button>
                      <Button data-size="sm" onClick={() => godkjenn(f.tjeneste.id)}>Godkjenn og legg til</Button>
                    </div>
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        </>
      )}

      <Heading level={2} data-size="sm" style={{ marginTop: '1.5rem' }}>
        Mine forslag til andre virksomheter
      </Heading>
      <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
        Tjenester DENNE virksomheten selv har foreslått til en ANNEN virksomhet via import-wizarden
        («Importer rettighetsmodell»), som fortsatt står ubehandlet der. Bruk «Slett» for å angre —
        f.eks. for å rydde opp etter en test-import før en ny kjøring.
      </Paragraph>
      {!mineForslag && <Spinner aria-label="Laster …" data-size="sm" />}
      {mineForslag && mineForslag.length === 0 && <Paragraph>Ingen egne forslag venter hos andre virksomheter.</Paragraph>}
      {mineForslag && mineForslag.length > 0 && (
        <>
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', margin: 0 }}>
              {valgteMine.size} valgt{valgteMine.size === 1 ? '' : 'e'}
            </Paragraph>
            <Button
              data-size="sm"
              variant="secondary"
              data-color="danger"
              onClick={massesletteMine}
              disabled={valgteMine.size === 0 || massesletterMine}
            >
              {massesletterMine ? 'Sletter …' : 'Slett valgte'}
            </Button>
          </div>
          {massesletterMineFeil && <div className="feilmelding" style={{ marginBottom: '0.75rem' }}>{massesletterMineFeil}</div>}
          <Table border>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>
                  <Checkbox
                    aria-label="Velg alle viste"
                    checked={mineForslag.length > 0 && mineForslag.every((f) => valgteMine.has(f.tjeneste.id))}
                    onChange={(e) => vekslAlleVisteMine(e.target.checked)}
                  />
                </Table.HeaderCell>
                <Table.HeaderCell>Tittel</Table.HeaderCell>
                <Table.HeaderCell>Mål-virksomhet</Table.HeaderCell>
                <Table.HeaderCell>Handlinger</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {mineForslag.map((f) => (
                <Table.Row key={f.tjeneste.id}>
                  <Table.Cell>
                    <Checkbox
                      aria-label={`Velg forslag ${f.tjeneste.tittel}`}
                      checked={valgteMine.has(f.tjeneste.id)}
                      onChange={(e) => vekslValgtMitt(f.tjeneste.id, e.target.checked)}
                    />
                  </Table.Cell>
                  <Table.Cell>
                    <Link asChild><RouterLink to={`/tjenester/${f.tjeneste.id}`}>{f.tjeneste.tittel}</RouterLink></Link>
                  </Table.Cell>
                  <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{f.malVirksomhetNavn}</Table.Cell>
                  <Table.Cell>
                    <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => slettMittForslag(f.tjeneste.id)}>Slett</Button>
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
