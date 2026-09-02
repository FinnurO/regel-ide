import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Alert, Button, Card, Field, Heading, Label, Link, Paragraph, Select, Spinner, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { KodelisteDto, MyndighetstildelingDto, RettskildeSammendrag, VirksomhetKandidatDto, VirksomhetsbegrepDto } from '../api/types';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { LeggTilMyndighetstildelingForm } from '../virksomhet/LeggTilMyndighetstildelingForm';

export default function VirksomhetDetalj() {
  const { id } = useParams<{ id: string }>();
  const { virksomheterPerId, laster: virksomheterLaster } = useVirksomheter();

  const [begrep, setBegrep] = useState<VirksomhetsbegrepDto[] | null>(null);
  const [tildelinger, setTildelinger] = useState<MyndighetstildelingDto[] | null>(null);
  const [kandidater, setKandidater] = useState<VirksomhetKandidatDto[] | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [visLeggTilTildeling, setVisLeggTilTildeling] = useState(false);
  // Departement-virksomhet-lenke (2026-08-30) — ikke betinget på noen egen "er departement"-boolsk,
  // se oppgavebeskrivelsen: lastes for ENHVER virksomhet, seksjonen skjules bare når listen er tom.
  const [rettskilderAnsvarligFor, setRettskilderAnsvarligFor] = useState<RettskildeSammendrag[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  const [nyTerm, setNyTerm] = useState('');
  const [leggerTil, setLeggerTil] = useState(false);
  const [leggTilFeil, setLeggTilFeil] = useState<string | null>(null);

  const [sveiper, setSveiper] = useState(false);
  const [sveipFeil, setSveipFeil] = useState<string | null>(null);
  const [sveipResultat, setSveipResultat] = useState<{ funnet: number; nye: number } | null>(null);

  const [forvaltningsnivaKodeliste, setForvaltningsnivaKodeliste] = useState<KodelisteDto | null>(null);
  const [forvaltningsnivaLagres, setForvaltningsnivaLagres] = useState(false);
  const [forvaltningsnivaFeil, setForvaltningsnivaFeil] = useState<string | null>(null);
  // useVirksomheter() henter og cacher ÉN gang per bruk — den har ingen "hent på nytt"-funksjon
  // (ville krevd å endre en delt hook brukt mange steder). Lagrer derfor den nyeste verdien lokalt her
  // og lar den overstyre hook-verdien i visningen under, i stedet for å endre den delte hooken.
  const [forvaltningsnivaOverstyrt, setForvaltningsnivaOverstyrt] = useState<string | null | undefined>(undefined);

  function lastAlt() {
    if (!id) return;
    api.hentVirksomhetsbegrep(id).then(setBegrep)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av begrep.'));
    api.hentMyndighetstildelingerForVirksomhet(id).then(setTildelinger).catch(() => setTildelinger([]));
    api.hentVentendeKandidater(id).then(setKandidater).catch(() => setKandidater([]));
    api.hentRettskilderAnsvarligFor(id).then(setRettskilderAnsvarligFor).catch(() => setRettskilderAnsvarligFor([]));
  }

  useEffect(lastAlt, [id]);
  useEffect(() => {
    api.hentKodelister()
      .then((liste) => setForvaltningsnivaKodeliste(liste.find((k) => k.kode === 'KL-FORVALTNINGSNIVA') ?? null))
      .catch(() => setForvaltningsnivaKodeliste(null));
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);

  async function endreForvaltningsniva(verdi: string) {
    if (!id) return;
    setForvaltningsnivaFeil(null);
    setForvaltningsnivaLagres(true);
    try {
      const oppdatert = await api.settVirksomhetForvaltningsniva(id, verdi === '' ? null : verdi);
      setForvaltningsnivaOverstyrt(oppdatert.forvaltningsniva);
    } catch (err) {
      setForvaltningsnivaFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved endring av forvaltningsnivå.');
    } finally {
      setForvaltningsnivaLagres(false);
    }
  }

  async function leggTilBegrep(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyTerm.trim()) return;
    setLeggTilFeil(null);
    setLeggerTil(true);
    try {
      await api.opprettVirksomhetsbegrep({ virksomhetId: id, term: nyTerm.trim(), skosUrl: null });
      setNyTerm('');
      lastAlt();
    } catch (err) {
      setLeggTilFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av navneform.');
    } finally {
      setLeggerTil(false);
    }
  }

  async function kjorSveip() {
    if (!id) return;
    setSveiper(true);
    setSveipFeil(null);
    setSveipResultat(null);
    try {
      const resultat = await api.sveipVirksomhetKandidater({ virksomhetId: id });
      setSveipResultat({ funnet: resultat.antallTreffFunnet, nye: resultat.antallNyeKandidater });
      lastAlt();
    } catch (err) {
      setSveipFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sveip.');
    } finally {
      setSveiper(false);
    }
  }

  if (virksomheterLaster) return <Spinner aria-label="Laster …" data-size="sm" />;
  const virksomhet = id ? virksomheterPerId.get(id) : undefined;
  if (!virksomhet) return <Alert data-color="danger">Fant ingen virksomhet med id «{id}».</Alert>;

  const forvaltningsniva = forvaltningsnivaOverstyrt === undefined ? virksomhet.forvaltningsniva : forvaltningsnivaOverstyrt;

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        {virksomhet.navn}
      </Heading>
      <Paragraph style={{ marginBottom: '0.75rem', display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <Tag data-color={forvaltningsniva ? 'info' : 'neutral'} data-size="sm">
          {forvaltningsniva ?? 'Forvaltningsnivå ikke satt'}
        </Tag>
        <Tag data-color={virksomhet.aktiv ? 'success' : 'neutral'} data-size="sm">
          {virksomhet.aktiv ? 'Aktiv' : 'Sovende'}
        </Tag>
      </Paragraph>

      {feil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{feil}</Alert>}

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Grunndata
        </Heading>
        <Card style={{ padding: '1rem' }}>
          <Table>
            <Table.Body>
              <Table.Row>
                <Table.HeaderCell>Organisasjonsnummer</Table.HeaderCell>
                <Table.Cell style={{ fontFamily: 'monospace' }}>{virksomhet.organisasjonsnummer ?? '—'}</Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Forvaltningsnivå</Table.HeaderCell>
                <Table.Cell>
                  <Field style={{ maxWidth: '16rem' }}>
                    <Label style={{ display: 'none' }}>Forvaltningsnivå</Label>
                    <Select data-size="sm" value={forvaltningsniva ?? ''} disabled={forvaltningsnivaLagres}
                      onChange={(e) => endreForvaltningsniva(e.target.value)}>
                      <Select.Option value="">Ikke satt</Select.Option>
                      {forvaltningsnivaKodeliste?.koder.map((k) => (
                        <Select.Option key={k.kode} value={k.kode}>{k.term}</Select.Option>
                      ))}
                    </Select>
                  </Field>
                  {forvaltningsnivaFeil && <Alert data-color="danger" style={{ marginTop: '0.25rem' }}>{forvaltningsnivaFeil}</Alert>}
                </Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Organisasjonsform (Brreg)</Table.HeaderCell>
                <Table.Cell>{virksomhet.organisasjonsformKode ?? '—'}</Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Sektorkode (Brreg)</Table.HeaderCell>
                <Table.Cell>{virksomhet.sektorkode ?? '—'}</Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Overordnet enhet</Table.HeaderCell>
                <Table.Cell>
                  {virksomhet.overordnetEnhetId
                    ? virksomheterPerId.get(virksomhet.overordnetEnhetId)?.navn ?? virksomhet.overordnetEnhetId
                    : '—'}
                </Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Sist synkronisert mot Brreg</Table.HeaderCell>
                <Table.Cell>{virksomhet.sistBrregSynkronisert ?? 'Aldri (kun seedet)'}</Table.Cell>
              </Table.Row>
            </Table.Body>
          </Table>
        </Card>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Navneformer i rettskildetekst
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
          Alle navneformer under peker på samme virksomhet — synonymer (f.eks. «Fylkesmann»/«Statsforvalter») er bare flere rader, ingen egen mekanisme.
        </Paragraph>
        {!begrep && <Spinner aria-label="Laster …" data-size="sm" />}
        {begrep && begrep.length === 0 && <Paragraph>Ingen navneformer registrert ennå.</Paragraph>}
        {begrep && begrep.length > 0 && (
          <Card style={{ padding: 0, overflow: 'hidden', marginBottom: '0.75rem' }}>
            <Table>
              <Table.Body>
                {begrep.map((b) => (
                  <Table.Row key={b.id}>
                    <Table.Cell>{b.term}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </Card>
        )}
        <form onSubmit={leggTilBegrep} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end' }}>
          <Textfield label="Ny navneform" placeholder="f.eks. Statsforvalter" value={nyTerm}
            onChange={(e) => setNyTerm(e.target.value)} required />
          <Button type="submit" disabled={leggerTil || !nyTerm.trim()}>
            {leggerTil ? 'Legger til …' : 'Legg til'}
          </Button>
        </form>
        {leggTilFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{leggTilFeil}</Alert>}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Myndighetstildelinger
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
          Gruppebegrep (f.eks. «forurensningsmyndighet») tildelt denne virksomheten gjennom en forskrift.
          Gyldighet arves fra hjemmelen, ingen egne datoer her.
        </Paragraph>
        {!tildelinger && <Spinner aria-label="Laster …" data-size="sm" />}
        {tildelinger && tildelinger.length === 0 && <Paragraph>Ingen myndighetstildelinger registrert.</Paragraph>}
        {tildelinger && tildelinger.length > 0 && (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <Table>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Paragrafspenn</Table.HeaderCell>
                  <Table.HeaderCell>Vilkår</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {tildelinger.map((t) => (
                  <Table.Row key={t.id}>
                    <Table.Cell style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>
                      {t.paragrafspenn.map((p) => (p.tilEid ? `${p.fraEid}–${p.tilEid}` : p.fraEid)).join(', ')}
                    </Table.Cell>
                    <Table.Cell>{t.vilkaar ?? '—'}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </Card>
        )}
        <Button data-size="sm" variant="secondary" onClick={() => setVisLeggTilTildeling((v) => !v)}>
          {visLeggTilTildeling ? 'Skjul skjema' : 'Legg til myndighetstildeling'}
        </Button>
        {visLeggTilTildeling && id && (
          <LeggTilMyndighetstildelingForm
            virksomhetId={id}
            rettskilder={rettskilder}
            onOpprettet={(ny) => {
              setTildelinger((forrige) => [...(forrige ?? []), ny]);
              setVisLeggTilTildeling(false);
            }}
          />
        )}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Ansvarlig for
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
          Gjeldende lover/forskrifter der Lovdata oppgir denne virksomheten som ansvarlig departement
          (eksakt navnetreff, ingen fuzzy-matching — se rettskildens egen "Ansvarlig departement"-felt).
        </Paragraph>
        {!rettskilderAnsvarligFor && <Spinner aria-label="Laster …" data-size="sm" />}
        {rettskilderAnsvarligFor && rettskilderAnsvarligFor.length === 0 && (
          <Paragraph>Ingen rettskilder registrert med denne virksomheten som ansvarlig departement.</Paragraph>
        )}
        {rettskilderAnsvarligFor && rettskilderAnsvarligFor.length > 0 && (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <Table>
              <Table.Body>
                {rettskilderAnsvarligFor.map((r) => (
                  <Table.Row key={r.id}>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/rettskilder/${r.id}`}>{r.kortnavn ?? r.tittel}</RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>{r.kildetype}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </Card>
        )}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Ventende kandidater
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
          Funn fra tekstsøk som ikke er godkjent eller avvist ennå.{' '}
          <Link asChild><RouterLink to={`/virksomhet-kandidater?virksomhetId=${id}`}>Se full kandidatliste (alle statuser, filtrerbar)</RouterLink></Link>
        </Paragraph>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.75rem' }}>
          <Button data-size="sm" variant="secondary" onClick={kjorSveip} disabled={sveiper}>
            {sveiper ? 'Sveiper …' : 'Kjør sveip for denne virksomheten'}
          </Button>
        </div>
        {sveipFeil && <Alert data-color="danger" style={{ marginBottom: '0.75rem' }}>{sveipFeil}</Alert>}
        {sveipResultat && (
          <Alert data-color="info" style={{ marginBottom: '0.75rem' }}>
            Fant {sveipResultat.funnet} treff totalt, {sveipResultat.nye} nye kandidater lagt i køen.
          </Alert>
        )}
        {!kandidater && <Spinner aria-label="Laster …" data-size="sm" />}
        {kandidater && kandidater.length === 0 && <Paragraph>Ingen ventende kandidater.</Paragraph>}
        {kandidater && kandidater.length > 0 && (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <Table>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Node</Table.HeaderCell>
                  <Table.HeaderCell>Handling</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {kandidater.map((k) => (
                  <Table.Row key={k.id}>
                    <Table.Cell style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>{k.nodeEid}</Table.Cell>
                    <Table.Cell style={{ display: 'flex', gap: '0.5rem' }}>
                      <Button
                        data-size="sm"
                        variant="secondary"
                        onClick={() => api.godkjennVirksomhetKandidat(k.id).then(lastAlt)}
                      >
                        Godkjenn
                      </Button>
                      <Button
                        data-size="sm"
                        variant="tertiary"
                        onClick={() => api.avvisVirksomhetKandidat(k.id).then(lastAlt)}
                      >
                        Avvis
                      </Button>
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </Card>
        )}
      </section>
    </>
  );
}
