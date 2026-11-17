import { useCallback, useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Details, Heading, Link, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { ForhandRelasjoner, ForhandRelasjonsforslag, Kunnskapsstatus, RelasjonNode } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { TOLKNINGSARGUMENTER } from './utsagnsgrunnlag';
import { relasjonstekst } from './aktorkoForhand';
import { SKRIVES_SOM_TEKST, avTekst, kantmerke, knappetilstand, nodelenke, storForbokstav, tellingstekst } from './forhandRelasjonsregler';

/**
 * [Ny, issue #469, Johanns klarsignal 2026-11-17] Forhåndsklassifiseringens relasjoner, én linje og én knapp per relasjon (docs/09 §54).
 *
 * <p>Henter `GET /api/aktorko/forhand/{nr}/relasjoner` selv, så den kan stå både i kortet før godkjenningen (knappene er av: «Opprett navnet
 * først») og under kvitteringen etter (navnet er opprettet, og knappene virker). «Skriv relasjonen …» viser først hva som skrives; «Bekreft:
 * skriv som forslag» skriver. Ingen fri inndata: alt kommer fra API-et, som regner det ut av fila og basen.</p>
 */

function Node({ n }: { n: RelasjonNode | null }) {
  if (!n) return <Metatekst as="span">ingen</Metatekst>;
  const til = nodelenke(n);
  return til
    ? <Link asChild><RouterLink to={til}>{n.navn}</RouterLink></Link>
    : <>«{n.navn}» <Metatekst as="span">({n.merknad ?? 'finnes ikke ennå'})</Metatekst></>;
}

function Bekreftelse({ f, onBekreft, onAvbryt, sender }: { f: ForhandRelasjonsforslag; onBekreft: () => void; onAvbryt: () => void; sender: boolean }) {
  const argumenter = (f.tolkningsargumenter ?? []).map((a) => TOLKNINGSARGUMENTER.find((x) => x.verdi === a)?.tekst ?? a);
  return (
    <div style={{ borderLeft: '3px solid var(--ds-color-accent-border-subtle)', paddingLeft: '0.6rem', margin: '0.3rem 0 0.5rem' }}>
      <Metatekst style={{ marginBottom: '0.25rem' }}>Dette skrives (kontrollert mot forhåndsklassifiseringen):</Metatekst>
      <Table data-size="sm" data-density="compact" style={{ marginBottom: '0.4rem' }}>
        <Table.Body>
          <Table.Row><Table.HeaderCell style={{ width: '9rem' }}>Utsagn</Table.HeaderCell><Table.Cell>{f.utsagn}</Table.Cell></Table.Row>
          <Table.Row><Table.HeaderCell>Fra</Table.HeaderCell><Table.Cell><Node n={f.fra} /></Table.Cell></Table.Row>
          <Table.Row><Table.HeaderCell>Til</Table.HeaderCell><Table.Cell><Node n={f.til} /></Table.Cell></Table.Row>
          <Table.Row>
            <Table.HeaderCell>Kategori</Table.HeaderCell>
            <Table.Cell>{f.kategori} {f.kategoritekst}</Table.Cell>
          </Table.Row>
          <Table.Row><Table.HeaderCell>Type</Table.HeaderCell><Table.Cell>{f.typekode}</Table.Cell></Table.Row>
          <Table.Row>
            <Table.HeaderCell style={{ verticalAlign: 'top' }}>Hjemmel</Table.HeaderCell>
            <Table.Cell>
              {f.hjemmel && (
                <>
                  <Link asChild><RouterLink to={`/rettskilder/${f.hjemmel.rettskildeId}`}>{f.hjemmel.rettskilde}</RouterLink></Link>{' '}
                  <span title={f.hjemmel.eid}>{f.hjemmel.bestemmelse ?? f.hjemmel.eid}</span>
                  {f.hjemmel.sitat && <Metatekst>«{f.hjemmel.sitat}»</Metatekst>}
                  <Metatekst>Navnets hjemmel i forhåndsklassifiseringen; fila har ikke egen hjemmel per relasjon.</Metatekst>
                </>
              )}
            </Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.HeaderCell style={{ verticalAlign: 'top' }}>Kunnskapsstatus</Table.HeaderCell>
            <Table.Cell>
              <KunnskapsstatusTag status={(f.kunnskapsstatus as Kunnskapsstatus | null) ?? null} />
              {argumenter.length > 0 && <Metatekst>Tolkningsargumenter: {argumenter.join(', ')}.</Metatekst>}
            </Table.Cell>
          </Table.Row>
          {f.merknad && (
            <Table.Row>
              <Table.HeaderCell style={{ verticalAlign: 'top' }}>Merknad i fila</Table.HeaderCell>
              <Table.Cell>
                {f.merknad}
                <Metatekst>Lagres i begrunnelsen. Viser merknaden til en annen bestemmelse enn hjemmelen, avbryt og registrer kanten på aktørsiden.</Metatekst>
              </Table.Cell>
            </Table.Row>
          )}
          <Table.Row>
            <Table.HeaderCell>Status</Table.HeaderCell>
            <Table.Cell><Tag data-size="sm" data-color="info">{SKRIVES_SOM_TEKST}</Tag></Table.Cell>
          </Table.Row>
        </Table.Body>
      </Table>
      <div style={{ display: 'flex', gap: '0.6rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <Button data-size="sm" disabled={sender} onClick={onBekreft}>{sender ? 'Skriver …' : 'Bekreft: skriv som forslag'}</Button>
        <Button data-size="sm" variant="tertiary" onClick={onAvbryt}>Avbryt</Button>
      </div>
    </div>
  );
}

function Relasjonsrad({ f, onSkrevet }: { f: ForhandRelasjonsforslag; onSkrevet: (ny: ForhandRelasjonsforslag) => void }) {
  const [apen, setApen] = useState(false);
  const [sender, setSender] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const tilstand = knappetilstand(f);
  const merke = kantmerke(f);
  const motpart = [f.fra, f.til].find((n) => n && !n.erNavnet) ?? f.til;

  async function skriv() {
    setSender(true);
    setFeil(null);
    try {
      const ny = await api.skrivForhandRelasjon(f.nr, f.indeks, { fraId: f.fra?.id ?? null, tilId: f.til?.id ?? null });
      setApen(false);
      onSkrevet(ny);
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Skrivingen feilet. Ingenting er lagret.');
    } finally {
      setSender(false);
    }
  }

  return (
    <li style={{ marginBottom: '0.4rem' }}>
      <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <span>{storForbokstav(relasjonstekst(f.relasjon)).replace(/\.$/, '')}</span>
        {motpart ? <Node n={motpart} /> : null}
        {f.kategori && <Tag data-size="sm" data-color="neutral" title={`Kategori ${f.kategori} (${f.kategoritekst}), type ${f.typekode}.`}>{f.kategori} {f.typekode}</Tag>}
        {merke && <Tag data-size="sm" data-color={merke.farge}>{merke.tekst}</Tag>}
        {f.status === 'skrives_av_koblingen' && (
          <Tag data-size="sm" data-color="neutral" title="Koblingen gjør navnet til navneform på aktøren.">Skrives av koblingen</Tag>
        )}
        {tilstand === 'skriv' && !apen && (
          <Button data-size="sm" variant="secondary" onClick={() => setApen(true)}>Skriv relasjonen …</Button>
        )}
        {tilstand === 'av' && <Button data-size="sm" variant="secondary" disabled>Skriv relasjonen …</Button>}
      </div>
      {tilstand === 'av' && <Metatekst>{avTekst(f)}</Metatekst>}
      {f.status === 'kan_ikke_skrives' && <Metatekst>Kan ikke skrives: {f.grunn}</Metatekst>}
      {f.status === 'avvist' && (
        <Metatekst>Avvist av {f.kantSistEndretAv ?? 'et menneske'}{f.kantAvvisningBegrunnelse ? `: ${f.kantAvvisningBegrunnelse}` : ''}. Den skrives ikke på nytt.</Metatekst>
      )}
      {f.status === 'finnes' && f.utsagn && <Metatekst>{f.utsagn}{f.grunn === 'fantes alt (identisk kant)' ? ' (fantes alt)' : ''}.</Metatekst>}
      {f.merknad && <Metatekst>Merknad i fila: {f.merknad}</Metatekst>}
      {apen && <Bekreftelse f={f} sender={sender} onBekreft={skriv} onAvbryt={() => setApen(false)} />}
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.25rem' }}>{feil}</Alert>}
    </li>
  );
}

/** Relasjonene for ett navn, med knappene. Holder svaret fra en skriving selv, så raden viser «Finnes som forslag» uten ny lesing. */
function Relasjonsliste({ data }: { data: ForhandRelasjoner }) {
  const [relasjoner, setRelasjoner] = useState(data.relasjoner);
  useEffect(() => setRelasjoner(data.relasjoner), [data]);
  if (relasjoner.length === 0) return <Metatekst as="span">Ingen</Metatekst>;
  const erstatt = (ny: ForhandRelasjonsforslag) => setRelasjoner((rs) => rs.map((r) => (r.indeks === ny.indeks ? ny : r)));
  return (
    <>
      <Metatekst style={{ marginBottom: '0.25rem' }}>{storForbokstav(tellingstekst(relasjoner))}</Metatekst>
      <ul style={{ margin: 0, paddingLeft: '1.1rem' }}>
        {relasjoner.map((r) => <Relasjonsrad key={r.indeks} f={r} onSkrevet={erstatt} />)}
      </ul>
    </>
  );
}

/** Lista over relasjonene for ett navn i forhåndsklassifiseringen. `oppdater` endres når navnet er godkjent, så lista leses på nytt. */
export function ForhandRelasjonsliste({ nr, oppdater }: { nr: number; oppdater?: unknown }) {
  const [data, setData] = useState<ForhandRelasjoner | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  const les = useCallback(() => {
    api.hentForhandRelasjoner(nr)
      .then(setData)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Relasjonene kunne ikke hentes.'));
  }, [nr]);
  useEffect(les, [les, oppdater]);

  if (feil) return <Alert data-color="danger" data-size="sm">{feil}</Alert>;
  if (!data) return <Spinner aria-label="Laster relasjonene …" data-size="sm" />;
  return <Relasjonsliste data={data} />;
}

/**
 * [Ny, issue #469] Navn som er godkjent, står ikke lenger i køen, men relasjonene deres kan fortsatt skrives. Lukket `Details` under køen; lista
 * hentes først når den åpnes (`GET /api/aktorko/forhand/relasjoner?bareOpprettede=true`). Bare navn med minst én relasjon som kan skrives, venter
 * på en node, finnes eller er avvist; navn der ingen relasjon kan skrives, tas ikke med.
 */
export function OpprettedeNavnsRelasjoner() {
  const [apnet, setApnet] = useState(false);
  const [data, setData] = useState<ForhandRelasjoner[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    if (!apnet || data) return;
    api.hentForhandRelasjonerOpprettede()
      .then((d) => setData(d.filter((n) => n.relasjoner.some((r) => r.kategori !== null))))
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Relasjonene kunne ikke hentes.'));
  }, [apnet, data]);

  return (
    <Details onToggle={() => setApnet(true)} style={{ marginTop: '1rem' }}>
      <Details.Summary>Relasjoner for navn som er opprettet{data ? ` (${data.length})` : ''}</Details.Summary>
      <Details.Content>
        <Metatekst style={{ marginBottom: '0.5rem' }}>
          Navnene her er godkjent og står ikke lenger i køen. Relasjonene fra forhåndsklassifiseringen skrives som strukturkanter med én knapp per
          relasjon, alltid som forslag.
        </Metatekst>
        {feil ? <Alert data-color="danger" data-size="sm">{feil}</Alert>
          : !data ? <Spinner aria-label="Laster relasjonene …" data-size="sm" />
            : data.length === 0 ? <Metatekst>Ingen opprettede navn har relasjoner som kan skrives.</Metatekst>
              : data.map((n) => (
                <div key={n.nr} style={{ marginBottom: '0.75rem' }}>
                  <Heading level={4} data-size="2xs" style={{ marginBottom: '0.2rem' }}>
                    {n.navnetsNode && nodelenke(n.navnetsNode)
                      ? <Link asChild><RouterLink to={nodelenke(n.navnetsNode)!}>{n.navnetsNode.navn}</RouterLink></Link>
                      : n.nyNode ?? n.navn}{' '}
                    <Metatekst as="span">(n{n.nr}, «{n.navn}» i køen)</Metatekst>
                  </Heading>
                  <Relasjonsliste data={n} />
                </div>
              ))}
      </Details.Content>
    </Details>
  );
}
