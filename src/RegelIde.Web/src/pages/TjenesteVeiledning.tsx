/**
 * TjenesteVeiledning
 * ------------------------------------------------------------------
 * Tjenestesentrert veiledning fra vilkårstreet (2026-07-30,
 * docs/12-fasit-handbok-leveranse.md "Hovedfunn") — vilkårstreet rendret
 * som en lineær fortelling i beslutningsorden, med kommunale/nasjonale
 * datasett-verdier og veiledningskommentarer vevd inn per node, i stedet
 * for en paragraf-anchoret kommentarsamling. Live-rendret fra
 * GET /api/tjenester/{id}/veiledning — ingen egen persistert
 * dokument-entitet i denne runden.
 */
import { useEffect, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Alert, Heading, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import type { RettskildeSammendrag, VeiledningDto, VeiledningNodeDto, VirksomhetDto } from '../api/types';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { DokumenttypeTag } from '../handbok/DokumenttypeTag';
import { Metatekst } from '../entitet/Metatekst';

function VisVerdi({ verdiJson }: { verdiJson: string }) {
  try {
    const verdi = JSON.parse(verdiJson);
    return <>{typeof verdi === 'string' ? verdi : JSON.stringify(verdi)}</>;
  } catch {
    return <>{verdiJson}</>;
  }
}

function VeiledningNode({ node, dybde, rettskilder }: { node: VeiledningNodeDto; dybde: number; rettskilder: RettskildeSammendrag[] }) {
  return (
    <section style={{ marginLeft: `${dybde * 1.25}rem`, marginBottom: '1.5rem' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.3rem' }}>
        <Heading level={3} data-size="xs" style={{ margin: 0 }}>{node.tittel}</Heading>
        <Tag data-color={node.type === 'vilkar' ? 'info' : 'neutral'} data-size="sm">
          {node.type === 'vilkar' ? node.vilkarstype : `Regelnode · barn: ${node.barnOperator}`}
        </Tag>
        {node.vurderingstype === 'skjonnsbasert' && <Tag data-color="warning" data-size="sm">Skjønn</Tag>}
      </div>
      {node.beskrivelse && <Paragraph style={{ marginBottom: '0.3rem' }}>{node.beskrivelse}</Paragraph>}
      {node.juridiskGrunnlag.length > 0 && (
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.3rem' }}>
          Hjemmel:{' '}
          {node.juridiskGrunnlag.map((g, i) => {
            const href = rettskildeLenke(g.eId, rettskilder);
            return (
              <span key={`${g.kilde}-${g.eId}`}>
                {i > 0 && ', '}
                {href ? <Link asChild><RouterLink to={href}>{g.kilde}</RouterLink></Link> : g.kilde}
                {/* [Ny, 2026-09-02, issue #115] eId degradert til liten, sekundær metatekst — g.kilde er
                    allerede rettskildens tittel, se Egenskapspanel.tsx sin JuridiskGrunnlagRedigering
                    der feltet fylles ut fra rettskilde.tittel. */}
                {' '}<span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>({g.eId})</span>
              </span>
            );
          })}
        </Metatekst>
      )}
      {node.skjonnsmomenter.length > 0 && (
        <Metatekst style={{ marginBottom: '0.3rem' }}>
          Momenter i skjønnsvurderingen: {node.skjonnsmomenter.map((m) => m.navn).join(', ')}.
        </Metatekst>
      )}

      {node.inputDatasettVerdier.length > 0 && (
        <Table data-size="sm" border style={{ marginBottom: '0.5rem', maxWidth: '32rem' }}>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Parameter</Table.HeaderCell>
              <Table.HeaderCell>Verdi</Table.HeaderCell>
              <Table.HeaderCell>Kilde</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {node.inputDatasettVerdier.map((v) => (
              <Table.Row key={v.datasettId}>
                <Table.Cell>{v.felt}</Table.Cell>
                <Table.Cell>
                  <VisVerdi verdiJson={v.verdiJson} />
                  {v.erStandardverdi && <Tag data-color="neutral" data-size="sm" style={{ marginLeft: '0.4rem' }}>standard</Tag>}
                </Table.Cell>
                <Metatekst as={Table.Cell}>{v.kilde ?? '—'}</Metatekst>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}

      {node.kommentarer.map((k) => (
        <div key={k.id} style={{ marginBottom: '0.5rem' }}>
          <DokumenttypeTag dokumenttype={k.dokumenttype} style={{ marginBottom: '0.2rem' }} />
          <div style={{ fontSize: 'var(--ds-font-size-2)' }} dangerouslySetInnerHTML={{ __html: k.tekstHtml }} />
        </div>
      ))}

      {node.barn.map((b) => <VeiledningNode key={b.id} node={b} dybde={dybde + 1} rettskilder={rettskilder} />)}

      {node.unntak.map((u) => (
        <div key={u.id} style={{ marginLeft: '1.25rem', marginTop: '0.5rem', borderLeft: '3px solid var(--ds-color-warning-border-default)', paddingLeft: '0.75rem' }}>
          <Paragraph style={{ marginBottom: '0.3rem' }}>
            <strong>Unntak: {u.tittel}</strong> — med mindre «{u.betingelseTittel}»
          </Paragraph>
          {u.beskrivelse && <Metatekst style={{ marginBottom: '0.3rem' }}>{u.beskrivelse}</Metatekst>}
          {u.kommentarer.map((k) => (
            <div key={k.id} style={{ marginBottom: '0.3rem' }}>
              <DokumenttypeTag dokumenttype={k.dokumenttype} style={{ marginBottom: '0.2rem' }} />
              <div style={{ fontSize: 'var(--ds-font-size-2)' }} dangerouslySetInnerHTML={{ __html: k.tekstHtml }} />
            </div>
          ))}
        </div>
      ))}
    </section>
  );
}

export default function TjenesteVeiledning() {
  const { id } = useParams<{ id: string }>();
  const [virksomheter, setVirksomheter] = useState<VirksomhetDto[]>([]);
  const [virksomhetId, setVirksomhetId] = useState('');
  const [veiledning, setVeiledning] = useState<VeiledningDto | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => { api.hentVirksomheter().then(setVirksomheter).catch(() => setVirksomheter([])); }, []);
  useEffect(() => { api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([])); }, []);

  useEffect(() => {
    if (!id) return;
    setFeil(null);
    api.hentTjenesteVeiledning(id, virksomhetId || null).then(setVeiledning)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av veiledningen.'));
  }, [id, virksomhetId]);

  if (feil) return <Alert data-color="danger">{feil}</Alert>;
  if (!veiledning) return <Spinner aria-label="Laster …" data-size="sm" />;

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.5rem' }}>Veiledning: {veiledning.tjenesteTittel}</Heading>
      <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '1rem' }}>
        Vilkårstreet rendret i beslutningsorden, med kommunale/nasjonale parameterverdier og
        veiledningskommentarer vevd inn per node — ikke en persistert dokumentversjon, alltid
        gjeldende tilstand.
      </Metatekst>
      <VirksomhetVelger
        virksomheter={virksomheter}
        value={virksomhetId}
        onChange={setVirksomhetId}
        label="Virksomhet"
        tomValgTekst="(nasjonal standardverdi)"
        style={{ maxWidth: '20rem', marginBottom: '1.5rem' }}
      />

      <VeiledningNode node={veiledning.rot} dybde={0} rettskilder={rettskilder} />
    </>
  );
}
