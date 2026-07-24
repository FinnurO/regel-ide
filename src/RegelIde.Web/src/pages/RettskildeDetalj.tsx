import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
import { Heading, Link, Paragraph, Table, Tag, ToggleGroup } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RettskildeDetalj as RettskildeDetaljType, RettskildeNodeDto, TekstTaggDto } from '../api/types';
import { TagTekst, type TagKind, type TextTag } from '../tagging/TagTekst';

// Designsystemet har ingen lilla-familie (docs/09-design-konvensjoner.md) — begrep/tjeneste deler
// derfor blåtoner (accent/info) fremfor en oppdiktet farge.
const KINDS: TagKind[] = [
  { id: 'begrep', label: 'Begrep', color: 'accent' },
  { id: 'tjeneste', label: 'Tjeneste', color: 'info' },
  { id: 'vilkar', label: 'Vilkår', color: 'warning' },
  { id: 'regel', label: 'Regel', color: 'success' },
];

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

interface TreVisningProps {
  noder: TreNode[];
  taggerPerNode: Map<string, TextTag[]>;
  activeKind: string;
  onActiveKindChange: (id: string) => void;
  onTag: (nodeEid: string, nodeTekst: string, t: { start: number; end: number; kind: string; ref: string | null }) => void;
  onRemoveTag: (id: string) => void;
}

function TreVisning({ noder, taggerPerNode, activeKind, onActiveKindChange, onTag, onRemoveTag }: TreVisningProps) {
  return (
    <ul className="tre">
      {noder.map((n) => (
        <li key={n.id}>
          <div className="tre-node">
            <strong>{n.nummer ?? n.nodeType}</strong>
            {n.overskrift && ` — ${n.overskrift}`}
            {n.tekst && (n.nodeType === 'ledd' || n.nodeType === 'punkt') && (
              <div style={{ margin: '0.1rem 0 0.3rem', fontSize: 'var(--ds-font-size-2)' }}>
                <TagTekst
                  text={n.tekst}
                  tags={taggerPerNode.get(n.eid) ?? []}
                  kinds={KINDS}
                  activeKind={activeKind}
                  onActiveKindChange={onActiveKindChange}
                  showLayerSwitch={false}
                  showTagList={false}
                  onTag={(t) => onTag(n.eid, n.tekst!, t)}
                  onRemoveTag={onRemoveTag}
                />
              </div>
            )}
            <span className="eid">{n.eid}</span>
          </div>
          {n.barn.length > 0 && (
            <TreVisning
              noder={n.barn}
              taggerPerNode={taggerPerNode}
              activeKind={activeKind}
              onActiveKindChange={onActiveKindChange}
              onTag={onTag}
              onRemoveTag={onRemoveTag}
            />
          )}
        </li>
      ))}
    </ul>
  );
}

export default function RettskildeDetalj() {
  const { id } = useParams<{ id: string }>();
  const [detalj, setDetalj] = useState<RettskildeDetaljType | null>(null);
  const [tre, setTre] = useState<TreNode[] | null>(null);
  const [tagger, setTagger] = useState<TekstTaggDto[]>([]);
  const [activeKind, setActiveKind] = useState<string>(KINDS[0].id);
  const [visAknXml, setVisAknXml] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [taggFeil, setTaggFeil] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    setFeil(null);
    setDetalj(null);
    setTre(null);
    Promise.all([api.hentRettskilde(id), api.hentNoder(id), api.hentTagger(id)])
      .then(([d, noder, egneTagger]) => {
        setDetalj(d);
        setTre(byggTre(noder));
        setTagger(egneTagger);
      })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av rettskilden.'));
  }, [id]);

  const taggerPerNode = useMemo(() => {
    const kart = new Map<string, TextTag[]>();
    for (const t of tagger) {
      const liste = kart.get(t.nodeEid) ?? [];
      liste.push({ id: t.id, start: t.startOffset, end: t.endOffset, kind: t.kind, ref: t.refId });
      kart.set(t.nodeEid, liste);
    }
    return kart;
  }, [tagger]);

  async function handleTag(
    nodeEid: string,
    nodeTekst: string,
    t: { start: number; end: number; kind: string; ref: string | null },
  ) {
    if (!id) return;
    setTaggFeil(null);
    try {
      const nyTagg = await api.opprettTagg(id, {
        nodeEid,
        startOffset: t.start,
        endOffset: t.end,
        quotePrefix: nodeTekst.slice(Math.max(0, t.start - 30), t.start),
        quoteExact: nodeTekst.slice(t.start, t.end),
        quoteSuffix: nodeTekst.slice(t.end, t.end + 30),
        kind: t.kind as TekstTaggDto['kind'],
      });
      setTagger((forrige) => [...forrige, nyTagg]);
    } catch (e) {
      setTaggFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved opprettelse av tagg.');
    }
  }

  async function handleSlett(taggId: string) {
    if (!id) return;
    setTaggFeil(null);
    try {
      await api.slettTagg(id, taggId);
      setTagger((forrige) => forrige.filter((t) => t.id !== taggId));
    } catch (e) {
      setTaggFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved fjerning av tagg.');
    }
  }

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!detalj) return <Paragraph>Laster …</Paragraph>;

  return (
    <>
      <Link asChild>
        <RouterLink to="/">← Tilbake til listen</RouterLink>
      </Link>
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

      <Table style={{ marginBottom: '1.5rem' }}>
        <Table.Body>
          <Table.Row>
            <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>ELI</Table.Cell>
            <Table.Cell>{detalj.eli ?? '—'}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Kortnavn</Table.Cell>
            <Table.Cell>{detalj.kortnavn ?? '—'}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Konsolidert dato</Table.Cell>
            <Table.Cell>{detalj.konsolidertDato ?? '—'}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Utgiver</Table.Cell>
            <Table.Cell>{detalj.utgiver ?? '—'}</Table.Cell>
          </Table.Row>
        </Table.Body>
      </Table>

      <Heading level={2} data-size="sm">
        Innhold (tre-navigasjon)
      </Heading>
      <Paragraph style={{ marginBottom: '0.5rem' }}>
        Marker tekst i et ledd/punkt for å tagge den som begrep, tjeneste, vilkår eller regel (AK-3.3.1). Ett lag vises
        av gangen — bytt lag under.
      </Paragraph>
      <ToggleGroup
        value={activeKind}
        onChange={setActiveKind}
        data-size="sm"
        data-toggle-group="Vis tagger"
        style={{ marginBottom: '0.75rem' }}
      >
        {KINDS.map((k) => (
          <ToggleGroup.Item key={k.id} value={k.id}>
            {k.label}
          </ToggleGroup.Item>
        ))}
      </ToggleGroup>
      {taggFeil && <div className="feilmelding">{taggFeil}</div>}
      {tre && tre.length > 0 ? (
        <TreVisning
          noder={tre}
          taggerPerNode={taggerPerNode}
          activeKind={activeKind}
          onActiveKindChange={setActiveKind}
          onTag={handleTag}
          onRemoveTag={handleSlett}
        />
      ) : (
        <Paragraph>Ingen noder.</Paragraph>
      )}

      {tagger.length > 0 && (
        <div style={{ marginTop: '1.5rem' }}>
          <Heading level={2} data-size="sm">
            Egne tagger
          </Heading>
          <ul className="egne-tagger-liste">
            {tagger.map((t) => (
              <li key={t.id}>
                <Tag data-color={KINDS.find((k) => k.id === t.kind)?.color} data-size="sm">
                  {KINDS.find((k) => k.id === t.kind)?.label ?? t.kind}
                </Tag>
                <span className="sitat">«{t.quoteExact}»</span>
                <button type="button" onClick={() => handleSlett(t.id)}>
                  Fjern
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

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
