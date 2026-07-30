import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useParams, useSearchParams } from 'react-router';
import { Button, Heading, Link, Paragraph, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type {
  RettskildeDetalj as RettskildeDetaljType,
  RettskildeNodeDto,
  RettskildeSammendrag,
  TekstTaggDto,
  TjenesteReferanseDto,
} from '../api/types';
import { TagTekst, type Registry, type TagKindId, type TextTag } from '../tagging/TagTekst';
import { RettskildeTre, type RettskildeNode as TreNodeVm } from '../tre/RettskildeTre';
import { KommentarRedigering } from '../handbok/KommentarRedigering';
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
  const [searchParams] = useSearchParams();
  const { taggKinds } = useKonfigurasjon();
  const [detalj, setDetalj] = useState<RettskildeDetaljType | null>(null);
  const [tre, setTre] = useState<TreNode[] | null>(null);
  const [tagger, setTagger] = useState<TekstTaggDto[]>([]);
  const [activeKind, setActiveKind] = useState<string>('');
  const [selectedEid, setSelectedEid] = useState<string | undefined>(undefined);
  const [referertAvTjenester, setReferertAvTjenester] = useState<TjenesteReferanseDto[]>([]);
  const [filter, setFilter] = useState('');
  const [visAknXml, setVisAknXml] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [taggFeil, setTaggFeil] = useState<string | null>(null);

  // Håndbok-forfatterflyt (2026-07-26) — kun relevant når kildetype='Rundskriv', se render under.
  const [alleRettskilder, setAlleRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [visOpprettKapittel, setVisOpprettKapittel] = useState(false);
  const [nyKapittelNummer, setNyKapittelNummer] = useState('');
  const [nyKapittelOverskrift, setNyKapittelOverskrift] = useState('');
  const [kapittelFeil, setKapittelFeil] = useState<string | null>(null);
  const [kapittelLagrer, setKapittelLagrer] = useState(false);
  const [visOpprettKommentar, setVisOpprettKommentar] = useState(false);

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
        const eidFraLenke = searchParams.get('eid');
        if (eidFraLenke) setSelectedEid(eidFraLenke);
      })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av rettskilden.'));
    api.hentReferertAvTjenester(id).then(setReferertAvTjenester).catch(() => setReferertAvTjenester([]));
  }, [id]);

  useEffect(() => {
    if (detalj?.kildetype === 'Rundskriv') api.hentRettskilder().then(setAlleRettskilder);
  }, [detalj?.kildetype]);

  // Registry for «Koble til …»-handlingen i tagg-listen (byggesteg 2) — kandidater for kind='begrep'/'tjeneste'.
  const [registry, setRegistry] = useState<Registry>({});
  // Kobler-visning for allerede-koblede tagger («resolveRef») — 2026-07-30, se TagTekst.tsx.
  const [begrepPerId, setBegrepPerId] = useState<Map<string, string>>(new Map());
  const [tjenestePerId, setTjenestePerId] = useState<Map<string, string>>(new Map());
  const [vilkarPerId, setVilkarPerId] = useState<Map<string, string>>(new Map());
  const [regelnodePerId, setRegelnodePerId] = useState<Map<string, string>>(new Map());
  const [rotnodeId, setRotnodeId] = useState<string | undefined>(undefined);
  useEffect(() => {
    Promise.all([api.hentBegreper(), api.hentTjenester(), api.hentVilkarListe(), api.hentRegelnodeListe()])
      .then(([begreper, tjenester, vilkarListe, regelnoder]) => {
        setRegistry({
          begrep: begreper.map((b) => ({ ref: b.id, label: b.term })),
          tjeneste: tjenester.map((t) => ({ ref: t.id, label: t.tittel })),
        });
        setBegrepPerId(new Map(begreper.map((b) => [b.id, b.term])));
        setTjenestePerId(new Map(tjenester.map((t) => [t.id, t.tittel])));
        setVilkarPerId(new Map(vilkarListe.map((v) => [v.id, v.tittel])));
        setRegelnodePerId(new Map(regelnoder.map((r) => [r.id, r.tittel])));
        // Bevisst forenkling (kun ett vilkårstre finnes i dag) — se plan «Sammenhengende navigasjon».
        setRotnodeId(tjenester.find((t) => t.rotnodeId)?.rotnodeId ?? undefined);
      })
      .catch(() => setRegistry({})); // ingen bruker valgt ennå e.l. — «Koble til» skjules bare
  }, []);

  function resolveRef(kind: TagKindId, ref: string): { label: string; href: string } | undefined {
    if (kind === 'begrep' && begrepPerId.has(ref)) return { label: begrepPerId.get(ref)!, href: `/begreper/${ref}` };
    if (kind === 'tjeneste' && tjenestePerId.has(ref)) return { label: tjenestePerId.get(ref)!, href: `/tjenester/${ref}` };
    if (kind === 'vilkar' && vilkarPerId.has(ref) && rotnodeId) {
      return { label: vilkarPerId.get(ref)!, href: `/vilkarstre/${rotnodeId}?fokusVilkar=${ref}` };
    }
    if (kind === 'regel' && regelnodePerId.has(ref) && rotnodeId) {
      return { label: regelnodePerId.get(ref)!, href: `/vilkarstre/${rotnodeId}?fokusVilkar=${ref}` };
    }
    return undefined;
  }

  async function handleKobleTag(taggId: string, ref: string) {
    if (!id) return;
    setTaggFeil(null);
    try {
      const oppdatert = await api.kobleTaggTilEntitet(id, taggId, { refId: ref });
      setTagger((forrige) => forrige.map((t) => (t.id === taggId ? oppdatert : t)));
    } catch (e) {
      setTaggFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved kobling av tagg.');
    }
  }

  async function refetchNoder(velgEid?: string) {
    if (!id) return;
    const noder = await api.hentNoder(id);
    setTre(byggTre(noder));
    if (velgEid) setSelectedEid(velgEid);
  }

  async function opprettKapittel(parentNodeId: string | null) {
    if (!id || !nyKapittelNummer.trim()) return;
    setKapittelFeil(null);
    setKapittelLagrer(true);
    try {
      const kapittel = await api.opprettKapittelNode(id, {
        parentNodeId,
        nummer: nyKapittelNummer.trim(),
        overskrift: nyKapittelOverskrift.trim() || null,
      });
      setNyKapittelNummer('');
      setNyKapittelOverskrift('');
      setVisOpprettKapittel(false);
      await refetchNoder(kapittel.eid);
    } catch (err) {
      setKapittelFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av kapittel.');
    } finally {
      setKapittelLagrer(false);
    }
  }

  const taggerPerNode = useMemo(() => {
    const kart = new Map<string, TextTag[]>();
    for (const t of tagger) {
      const liste = kart.get(t.nodeEid) ?? [];
      liste.push({ id: t.id, start: t.startOffset, end: t.endOffset, kind: t.kind, ref: t.refId, kreverGjennomgang: t.kreverGjennomgang });
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

      {referertAvTjenester.length > 0 && (
        <div style={{ marginBottom: '1.5rem' }}>
          <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
            Brukt i tjenester
          </Heading>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
            {referertAvTjenester.map((r) => (
              <Link asChild key={`${r.tjenesteId}-${r.tilEid}`}>
                <RouterLink to={`/tjenester/${r.tjenesteId}`}>
                  {r.tjenesteTittel} ({r.tilEid})
                </RouterLink>
              </Link>
            ))}
          </div>
        </div>
      )}

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
          {detalj.kildetype === 'Rundskriv' && (
            <div style={{ marginBottom: '0.75rem' }}>
              {!visOpprettKapittel ? (
                <Button data-size="sm" variant="secondary" onClick={() => setVisOpprettKapittel(true)}>
                  Nytt kapittel
                </Button>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                  <Textfield label="Nummer" data-size="sm" value={nyKapittelNummer} onChange={(e) => setNyKapittelNummer(e.target.value)} />
                  <Textfield label="Overskrift" data-size="sm" value={nyKapittelOverskrift} onChange={(e) => setNyKapittelOverskrift(e.target.value)} />
                  <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <Button data-size="sm" disabled={kapittelLagrer || !nyKapittelNummer.trim()} onClick={() => opprettKapittel(null)}>
                      {kapittelLagrer ? 'Oppretter …' : 'Opprett'}
                    </Button>
                    <Button data-size="sm" variant="tertiary" onClick={() => setVisOpprettKapittel(false)}>
                      Avbryt
                    </Button>
                  </div>
                  {kapittelFeil && <div className="feilmelding">{kapittelFeil}</div>}
                </div>
              )}
            </div>
          )}
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

              {detalj.kildetype === 'Rundskriv' ? (
                valgtNode.handbokMetadata ? (
                  <KommentarRedigering
                    handbokId={id!}
                    mode="rediger"
                    node={valgtNode}
                    alleRettskilder={alleRettskilder}
                    onLagret={(oppdatert) => refetchNoder(oppdatert.eid)}
                  />
                ) : !visOpprettKommentar ? (
                  <Button data-size="sm" variant="secondary" onClick={() => setVisOpprettKommentar(true)}>
                    Ny kommentarseksjon her
                  </Button>
                ) : (
                  <KommentarRedigering
                    handbokId={id!}
                    mode="ny"
                    parentNodeId={valgtNode.id}
                    alleRettskilder={alleRettskilder}
                    onLagret={(opprettet) => {
                      setVisOpprettKommentar(false);
                      refetchNoder(opprettet.eid);
                    }}
                    onAvbryt={() => setVisOpprettKommentar(false)}
                  />
                )
              ) : kanTagges ? (
                <TagTekst
                  text={valgtNode.tekst!}
                  tags={taggerPerNode.get(valgtNode.eid) ?? []}
                  kinds={taggKinds}
                  activeKind={activeKind}
                  onActiveKindChange={setActiveKind}
                  onTag={(t) => handleTag(valgtNode.eid, valgtNode.tekst!, t)}
                  onRemoveTag={handleSlett}
                  registry={registry}
                  onLinkTag={handleKobleTag}
                  resolveRef={resolveRef}
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
