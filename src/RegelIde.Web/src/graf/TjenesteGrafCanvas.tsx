import { useEffect, useMemo, useState } from 'react';
import {
  ReactFlow, Background, Controls, MiniMap, Position, useNodesState, useEdgesState, type Edge, type Node,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { Checkbox, Paragraph } from '@digdir/designsystemet-react';
import {
  REL_FARGE, REL_LABEL, beregnLagdeltLayout, nodeLabel,
  type FeltvisningValg, type GrafKantLik, type GrafNodeLik,
} from './grafFelles';

export interface TjenesteGrafCanvasProps {
  noder: GrafNodeLik[];
  kanter: GrafKantLik[];
  felt: FeltvisningValg;
  onFeltChange: (felt: FeltvisningValg) => void;
  /** Fremhevet node (typisk et valgt sentrum) — valgfri, ren visuell fremheving + layout-rot. */
  fremhevetId?: string;
  hoyde?: string;
}

/**
 * [Ny, 2026-08-28] Delt graf-lerret — brukt av BÅDE `Tjenestereise.tsx` (ekte, persisterte data) og
 * `ImportWizard.tsx` sin in-memory forhåndsvisning (rå, ikke-persistert modelleksport-JSON, synthetic
 * navn-baserte id-er). All felles tegne-/layout-logikk bor her, IKKE duplisert i de to sidene.
 *
 * Bruker `useNodesState`/`useEdgesState` (React Flow sitt eget, offisielt anbefalte mønster for en
 * graf drevet av EKSTERNE data) og synkroniserer dem via `useEffect` når `noder`/`kanter`-props
 * endres — IKKE bare rene `nodes`/`edges`-props direkte til `<ReactFlow>` uten change-handlere.
 *
 * Hver node får eksplisitt `width`/`height` OG `handles` satt DIREKTE på node-objektet (ikke bare
 * via CSS-style). Rotårsak til at kanter opprinnelig ikke ble tegnet (2026-08-28, grundig
 * kildekode-sporet i `node_modules/@xyflow/{react,system}`): React Flow avhenger NORMALT av en
 * `ResizeObserver` den selv registrerer på hver node-DOM-node for å (a) sette `visibility: visible`
 * (`nodeHasDimensions` i `@xyflow/system`: `measured?.width ?? width ?? initialWidth` — width/height
 * alene dekker DENNE delen), OG (b) måle de faktiske `<Handle>`-elementenes posisjon til
 * `handleBounds` (`adoptUserNodes`/`parseHandles` i `@xyflow/system` — DENNE delen har INGEN
 * width/height-fallback, kun `userNode.measured` eller en EKTE DOM-måling). Uten (b) har ingen kant
 * et gyldig sluttpunkt uansett hvor "synlig" noden er — bekreftet ved at et Claude Browser-
 * forhåndsvisningsvindu har en ekte, native `ResizeObserver`-konstruktør, men aldri faktisk kaller
 * tilbake. Eksplisitt `handles` (et offisielt, dokumentert Node-felt — for SSR/miljøer uten måling)
 * omgår HELE dette ved å oppgi handle-posisjonene analytisk.
 *
 * [Utvidet, 2026-08-28, lesbarhetsrunden — se docs/13-backlog.md for bakgrunn/skjermbilde] Fire
 * navngitte handles per tjeneste-node i stedet for bare to (`inn`/`ut` for vanlige
 * tjeneste-til-tjeneste-kanter til venstre/høyre, `handling-mal`/`handling-kilde` for
 * handling-tilhørighet til topp/bunn) — retter "alt presses gjennom samme koblingspunkt"-symptomet:
 * før delte vanlige kanter OG handling-kanter samme topp/bunn-par, nå har de hver sin korridor.
 * Handling-noder selv har bare `handling-mal` (topp) — de er aldri kilde/mål for en vanlig kant.
 * Kantetiketter er IKKE lenger alltid synlige inline (kolliderte med bokser/andre kanter i tette
 * grafer) — vises i stedet i et lite detaljpanel under lerretet når kanten holdes over eller velges
 * (`onEdgeMouseEnter`/`onEdgeMouseLeave`/`onEdgeClick`), og kanttypen er `smoothstep` (rette
 * linjesegmenter) i stedet for konkurrerende buede bezier-kurver for vanlige kanter — begge uten ny
 * avhengighet (innebygde React Flow-typer). Layout-stabilitet: `useEffect`-en under beholder
 * EKSISTERENDE nodeposisjoner (inkl. en brukers manuelle drag) for noder som allerede fantes, og
 * regner kun ut posisjon for id-er som er nye — uten dette kjørte HELE layouten på nytt (og
 * overskrev en drag) selv når bare en visningscheckbox (`felt`) ble endret, som ikke påvirker
 * topologien i det hele tatt.
 */
export function TjenesteGrafCanvas({ noder, kanter, felt, onFeltChange, fremhevetId, hoyde = '65vh' }: TjenesteGrafCanvasProps) {
  const [fremhevetKantId, setFremhevetKantId] = useState<string | null>(null);
  const [valgtKantId, setValgtKantId] = useState<string | null>(null);

  const { nodes: beregnedeNoder, edges: beregnedeKanter } = useMemo<{ nodes: Node[]; edges: Edge[] }>(() => {
    const posisjoner = beregnLagdeltLayout(noder, kanter, fremhevetId);
    const eierAvHandling = new Set(kanter.filter((k) => k.erHandlingTilhorighet).map((k) => k.fraId));
    const nodes: Node[] = noder.map((n) => {
      const antallLinjer = nodeLabel(n, felt).split('\n').length;
      const width = n.erHandling ? 170 : 190;
      const height = 28 + antallLinjer * 18;
      // Vanlige tjeneste-til-tjeneste-kanter kobler til venstre (mål/inn)/høyre (kilde/ut) —
      // handling-tilhørighet kobler til topp (mål, på selve handling-noden)/bunn (kilde, på
      // tjenesten som eier handlingen) — to atskilte korridorer, se doc-kommentar over.
      const handles: Node['handles'] = [
        { type: 'target' as const, id: 'inn', position: Position.Left, x: -3, y: height / 2 - 3, width: 6, height: 6 },
        { type: 'source' as const, id: 'ut', position: Position.Right, x: width - 3, y: height / 2 - 3, width: 6, height: 6 },
      ];
      if (n.erHandling) {
        handles.push({ type: 'target' as const, id: 'handling-mal', position: Position.Top, x: width / 2 - 3, y: -3, width: 6, height: 6 });
      } else if (eierAvHandling.has(n.id)) {
        handles.push({ type: 'source' as const, id: 'handling-kilde', position: Position.Bottom, x: width / 2 - 3, y: height - 3, width: 6, height: 6 });
      }
      return {
        id: n.id,
        position: posisjoner.get(n.id) ?? { x: 0, y: 0 },
        width,
        height,
        handles,
        data: { label: nodeLabel(n, felt) },
        style: {
          width,
          height,
          whiteSpace: 'pre-line',
          fontSize: n.erHandling ? '0.75rem' : '0.85rem',
          background: n.id === fremhevetId
            ? 'var(--ds-color-accent-surface-tinted)'
            : n.erHandling ? 'var(--ds-color-neutral-surface-tinted)' : 'var(--ds-color-neutral-background-default)',
          border: n.id === fremhevetId ? '2px solid var(--ds-color-accent-border-strong)' : '1px solid var(--ds-color-neutral-border-default)',
          borderRadius: 'var(--ds-border-radius-md)',
          padding: '0.5rem 0.75rem',
        },
      };
    });
    const edges: Edge[] = kanter.map((k, i) => ({
      id: `${k.fraId}-${k.tilId}-${k.rel}-${i}`,
      source: k.fraId,
      target: k.tilId,
      sourceHandle: k.erHandlingTilhorighet ? 'handling-kilde' : 'ut',
      targetHandle: k.erHandlingTilhorighet ? 'handling-mal' : 'inn',
      // Ingen inline `label` lenger — vises i stedet i detaljpanelet under ved hover/valg (se
      // doc-kommentar over). Handling-tilhørighet har uansett aldri en meningsfull relasjonstekst.
      style: { stroke: k.erHandlingTilhorighet ? 'var(--ds-color-neutral-border-subtle)' : REL_FARGE[k.rel] ?? '#888' },
      animated: false,
      type: k.erHandlingTilhorighet ? 'straight' : 'smoothstep',
    }));
    return { nodes, edges };
  }, [noder, kanter, felt, fremhevetId]);

  const [nodes, setNodes, onNodesChange] = useNodesState(beregnedeNoder);
  const [edges, setEdges, onEdgesChange] = useEdgesState(beregnedeKanter);

  useEffect(() => {
    // Behold posisjonen til noder som allerede fantes (inkl. en brukers manuelle drag) — regn kun ut
    // posisjon for id-er som er NYE i denne oppdateringen. Uten dette hoppet HELE grafen til en fersk
    // layout selv når bare `felt` (rene visnings-checkbokser, ingen topologiendring) endret seg.
    setNodes((forrige) => {
      const forrigePosisjon = new Map(forrige.map((n) => [n.id, n.position]));
      return beregnedeNoder.map((n) => (forrigePosisjon.has(n.id) ? { ...n, position: forrigePosisjon.get(n.id)! } : n));
    });
    setEdges(beregnedeKanter);
  }, [beregnedeNoder, beregnedeKanter, setNodes, setEdges]);

  const kantIMarkering = valgtKantId ?? fremhevetKantId;
  const kantDetalj = useMemo(() => {
    if (!kantIMarkering) return null;
    const k = kanter.find((kant, i) => `${kant.fraId}-${kant.tilId}-${kant.rel}-${i}` === kantIMarkering);
    return k ?? null;
  }, [kantIMarkering, kanter]);

  return (
    <>
      <div style={{ display: 'flex', gap: '1rem', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', fontWeight: 'var(--ds-font-weight-medium)', margin: 0 }}>
          Vis på hver node:
        </Paragraph>
        <Checkbox label="Type" checked={felt.type} onChange={(e) => onFeltChange({ ...felt, type: e.target.checked })} />
        <Checkbox label="Kompetent myndighet" checked={felt.kompetentMyndighet} onChange={(e) => onFeltChange({ ...felt, kompetentMyndighet: e.target.checked })} />
        <Checkbox label="Livshendelser" checked={felt.livshendelser} onChange={(e) => onFeltChange({ ...felt, livshendelser: e.target.checked })} />
        <Checkbox label="Status" checked={felt.status} onChange={(e) => onFeltChange({ ...felt, status: e.target.checked })} />
      </div>

      {/* Den innebygde `'default'`-node-typen tegner ALLTID sine egne to Handle-prikker fysisk
        * øverst/nederst på boksen (hardkodet i @xyflow/react sin DefaultNode), UANSETT hva
        * `node.handles`-metadataen sier — den brukes kun til selve kant-ruting-matematikken (derfor
        * går kantene riktig inn/ut til venstre/høyre, se `sourceHandle`/`targetHandle` over), ikke
        * til hvilke prikker som faktisk TEGNES. Uten en egen node-komponent (større endring, ikke
        * gjort her) ville disse to prikkene stå igjen synlige på feil sted (topp/bunn) mens linjene
        * faktisk går til venstre/høyre — forvirrende, ikke bare kosmetisk feil. Appen har uansett
        * ingen funksjon for å dra ut en NY kant manuelt (ingen `onConnect` noe sted i koden), så
        * prikkene har ingen interaktiv funksjon å miste ved å skjules. */}
      <style>{'.regelide-graf .react-flow__handle { opacity: 0; pointer-events: none; }'}</style>
      <div className="regelide-graf" style={{ height: hoyde, border: '1px solid var(--ds-color-neutral-border-subtle)', borderRadius: 'var(--ds-border-radius-md)' }}>
        <ReactFlow
          nodes={nodes}
          edges={edges}
          onNodesChange={onNodesChange}
          onEdgesChange={onEdgesChange}
          onEdgeMouseEnter={(_, edge) => setFremhevetKantId(edge.id)}
          onEdgeMouseLeave={() => setFremhevetKantId(null)}
          onEdgeClick={(_, edge) => setValgtKantId((forrige) => (forrige === edge.id ? null : edge.id))}
          onPaneClick={() => setValgtKantId(null)}
          fitView
        >
          <Background />
          <Controls />
          <MiniMap />
        </ReactFlow>
      </div>

      <div style={{ minHeight: '2.5rem', marginTop: '0.75rem' }}>
        {kantDetalj ? (
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>
            <span style={{ display: 'inline-block', width: '0.75rem', height: '0.75rem', marginRight: '0.4rem', verticalAlign: 'middle', background: kantDetalj.erHandlingTilhorighet ? 'var(--ds-color-neutral-border-subtle)' : REL_FARGE[kantDetalj.rel] ?? '#888', borderRadius: '50%' }} />
            {kantDetalj.erHandlingTilhorighet ? 'har handling' : REL_LABEL[kantDetalj.rel] ?? kantDetalj.rel}
            {valgtKantId && (
              <button
                type="button"
                onClick={() => setValgtKantId(null)}
                style={{ marginLeft: '0.75rem', fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', background: 'none', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}
              >
                Lukk
              </button>
            )}
          </Paragraph>
        ) : (
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
            Hold musepekeren over (eller klikk på) en kant for å se relasjonen. Fargeforklaring: {Object.entries(REL_LABEL).map(([rel, label]) => (
              <span key={rel} style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem', marginRight: '0.75rem' }}>
                <span style={{ display: 'inline-block', width: '0.75rem', height: '0.75rem', background: REL_FARGE[rel], borderRadius: '50%' }} />
                {label}
              </span>
            ))}
          </Paragraph>
        )}
      </div>
    </>
  );
}
