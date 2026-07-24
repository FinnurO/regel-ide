import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Heading, Paragraph, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RettskildeDetalj as RettskildeDetaljType, RettskildeNodeDto } from '../api/types';

interface TreNode extends RettskildeNodeDto {
  barn: TreNode[];
}

function byggTre(noder: RettskildeNodeDto[]): TreNode[] {
  const perId = new Map<string, TreNode>(noder.map((n) => [n.id, { ...n, barn: [] }]));
  const rotnoder: TreNode[] = [];
  for (const node of perId.values()) {
    if (node.parentNodeId && perId.has(node.parentNodeId)) {
      perId.get(node.parentNodeId)!.barn.push(node);
    } else {
      rotnoder.push(node);
    }
  }
  return rotnoder;
}

function TreVisning({ noder }: { noder: TreNode[] }) {
  return (
    <ul className="tre">
      {noder.map((n) => (
        <li key={n.id}>
          <div className="tre-node">
            <strong>{n.nummer ?? n.nodeType}</strong>
            {n.overskrift && ` — ${n.overskrift}`}
            {n.tekst && (n.nodeType === 'ledd' || n.nodeType === 'punkt') && (
              <Paragraph style={{ margin: '0.1rem 0 0.3rem', fontSize: 'var(--ds-font-size-2)' }}>{n.tekst}</Paragraph>
            )}
            <span className="eid">{n.eid}</span>
          </div>
          {n.barn.length > 0 && <TreVisning noder={n.barn} />}
        </li>
      ))}
    </ul>
  );
}

export default function RettskildeDetalj() {
  const { id } = useParams<{ id: string }>();
  const [detalj, setDetalj] = useState<RettskildeDetaljType | null>(null);
  const [tre, setTre] = useState<TreNode[] | null>(null);
  const [visAknXml, setVisAknXml] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    setFeil(null);
    setDetalj(null);
    setTre(null);
    Promise.all([api.hentRettskilde(id), api.hentNoder(id)])
      .then(([d, noder]) => {
        setDetalj(d);
        setTre(byggTre(noder));
      })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av rettskilden.'));
  }, [id]);

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!detalj) return <Paragraph>Laster …</Paragraph>;

  return (
    <>
      <Link to="/">← Tilbake til listen</Link>
      <Heading level={1} data-size="lg" style={{ marginTop: '0.5rem' }}>
        {detalj.tittel}
      </Heading>

      <div style={{ display: 'flex', gap: '0.5rem', margin: '0.5rem 0 1rem', flexWrap: 'wrap' }}>
        <Tag data-color="info">{detalj.kildetype}</Tag>
        <Tag data-color={detalj.status === 'Gjeldende' ? 'success' : 'warning'}>{detalj.status}</Tag>
        {detalj.virksomhetId ? (
          <span className="badge-virksomhet">Virksomhetseid</span>
        ) : (
          <span className="badge-delt">Delt / nasjonal</span>
        )}
      </div>

      <table style={{ marginBottom: '1.5rem' }}>
        <tbody>
          <tr>
            <td style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>ELI</td>
            <td>{detalj.eli ?? '—'}</td>
          </tr>
          <tr>
            <td style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Kortnavn</td>
            <td>{detalj.kortnavn ?? '—'}</td>
          </tr>
          <tr>
            <td style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Konsolidert dato</td>
            <td>{detalj.konsolidertDato ?? '—'}</td>
          </tr>
          <tr>
            <td style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Utgiver</td>
            <td>{detalj.utgiver ?? '—'}</td>
          </tr>
        </tbody>
      </table>

      <Heading level={2} data-size="sm">
        Innhold (tre-navigasjon)
      </Heading>
      {tre && tre.length > 0 ? <TreVisning noder={tre} /> : <Paragraph>Ingen noder.</Paragraph>}

      <div style={{ marginTop: '1.5rem' }}>
        <button type="button" onClick={() => setVisAknXml((v) => !v)}>
          {visAknXml ? 'Skjul' : 'Vis'} kanonisk AKN-XML
        </button>
        {visAknXml && (
          <pre style={{ overflow: 'auto', maxHeight: '400px', background: 'var(--ds-color-neutral-surface-default)', padding: '1rem', fontSize: '0.8rem' }}>
            {detalj.aknXml}
          </pre>
        )}
      </div>
    </>
  );
}
