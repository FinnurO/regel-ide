import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
import { Heading, Link, Paragraph, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RettskildeDetalj as RettskildeDetaljType, RettskildeNodeDto, TekstTaggDto } from '../api/types';
import { TagTekst, type TextTag } from '../tagging/TagTekst';
import { RettskildeTre, type RettskildeNode as TreNodeVm } from '../tre/RettskildeTre';
import { useKonfigurasjon } from '../konfigurasjon/KonfigurasjonContext';

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

/**
 * Bygger om vårt flate/nøstede nodetre til RettskildeTre sin form.
 * `antallKommentarer` gjenbrukes her som antall EGNE tekst-tagger på noden
 * (ikke håndbok-kommentarer, som ikke finnes ennå) — komponentens generiske
 * "tellemerke uten status"-visning passer fint til det inntil håndboken
 * finnes, siden `kommentarStatus` da alltid er undefined for oss.
 */
function tilTreVm(noder: TreNode[], taggAntallPerNode: Map<string, number>): TreNodeVm[] {
  return noder.map((n) => ({
    eId: n.eid,
    nodeType: n.nodeType as TreNodeVm['nodeType'],
    merke: n.nummer ?? '',
    tittel: n.overskrift ?? undefined,
    tekst: n.tekst ?? undefined,
    opphevet: n.opphevet,
    antallKommentarer: taggAntallPerNode.get(n.eid),
    children: n.barn.length > 0 ? tilTreVm(n.barn, taggAntallPerNode) : undefined,
  }));
}

function finnNode(noder: TreNode[], eid: string): TreNode | null {
  for (const n of noder) {
    if (n.eid === eid) return n;
    const funnet = finnNode(n.barn, eid);
    if (funnet) return funnet;
  }
  return null;
}

export default function RettskildeDetalj() {
  const { id } = useParams<{ id: string }>();
  const { taggKinds } = useKonfigurasjon();
  const [detalj, setDetalj] = useState<RettskildeDetaljType | null>(null);
  const [tre, setTre] = useState<TreNode[] | null>(null);
  const [tagger, setTagger] = useState<TekstTaggDto[]>([]);
  const [activeKind, setActiveKind] = useState<string>('');
  const [selectedEid, setSelectedEid] = useState<string | undefined>(undefined);
  const [filter, setFilter] = useState('');
  const [visAknXml, setVisAknXml] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [taggFeil, setTaggFeil] = useState<string | null>(null);

  useEffect(() => {
    if (!activeKind && taggKinds.length > 0) setActiveKind(taggKinds[0].id);
  }, [taggKinds, activeKind]);

  useEffect(() => {
    if (!id) return;
    setFeil(null);
    setDetalj(null);
    setTre(null);
    setSelectedEid(undefined);
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

  const taggAntallPerNode = useMemo(() => {
    const kart = new Map<string, number>();
    for (const t of tagger) kart.set(t.nodeEid, (kart.get(t.nodeEid) ?? 0) + 1);
    return kart;
  }, [tagger]);

  const treVm = useMemo(() => (tre ? tilTreVm(tre, taggAntallPerNode) : []), [tre, taggAntallPerNode]);
  const valgtNode = useMemo(() => (tre && selectedEid ? finnNode(tre, selectedEid) : null), [tre, selectedEid]);

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

  const kanTagges = valgtNode && valgtNode.tekst && (valgtNode.nodeType === 'ledd' || valgtNode.nodeType === 'punkt');

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
        Innhold
      </Heading>
      {taggFeil && <div className="feilmelding">{taggFeil}</div>}

      <div style={{ display: 'flex', gap: '1.5rem', alignItems: 'flex-start', marginTop: '0.75rem' }}>
        <div style={{ width: '360px', flexShrink: 0 }}>
          <Textfield
            label="Søk i strukturen"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            style={{ marginBottom: '0.75rem' }}
          />
          {treVm.length > 0 ? (
            <div style={{ border: '1px solid var(--ds-color-neutral-border-subtle)', borderRadius: 'var(--ds-border-radius-lg)', maxHeight: '70vh', overflowY: 'auto' }}>
              <RettskildeTre
                nodes={treVm}
                selectedEId={selectedEid}
                onSelect={(eid) => setSelectedEid(eid)}
                filter={filter}
              />
            </div>
          ) : (
            <Paragraph>Ingen noder.</Paragraph>
          )}
        </div>

        <div style={{ flex: 1, minWidth: 0 }}>
          {!valgtNode && <Paragraph>Velg en node i treet for å se innholdet.</Paragraph>}

          {valgtNode && (
            <>
              <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>
                {valgtNode.nummer ?? valgtNode.nodeType}
                {valgtNode.overskrift && ` — ${valgtNode.overskrift}`}
              </Heading>
              <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '1rem' }}>
                {valgtNode.eid}
              </Paragraph>

              {kanTagges ? (
                <TagTekst
                  text={valgtNode.tekst!}
                  tags={taggerPerNode.get(valgtNode.eid) ?? []}
                  kinds={taggKinds}
                  activeKind={activeKind}
                  onActiveKindChange={setActiveKind}
                  onTag={(t) => handleTag(valgtNode.eid, valgtNode.tekst!, t)}
                  onRemoveTag={handleSlett}
                />
              ) : (
                <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
                  Denne noden har ingen egen løpetekst — velg et ledd eller punkt under den for å tagge.
                </Paragraph>
              )}
            </>
          )}
        </div>
      </div>

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
