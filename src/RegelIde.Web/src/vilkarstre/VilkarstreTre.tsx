/**
 * VilkarstreTre
 * ------------------------------------------------------------------
 * Hierarkisk liste-visning av vilkårstreet (produktkrav kap. 3.4,
 * alternativ til VilkarstreGraf) — operator per Regelnode («barn: OG»),
 * Unntak vist som et eget, innrykket element under sin gjelder_regel.
 * Samme visuelle familie som RettskildeTre.tsx (indent + ledelinje), men
 * egen komponent siden node-/kantformen er en annen.
 */
import { Tag } from '@digdir/designsystemet-react';
import type { VilkarstreNode } from './bygging';

interface VilkarstreTreProps {
  root: VilkarstreNode;
  valgtId?: string;
  onSelect: (node: VilkarstreNode) => void;
}

export function VilkarstreTre({ root, valgtId, onSelect }: VilkarstreTreProps) {
  return (
    <div role="tree" aria-label="Vilkårstre (hierarkisk visning)" style={{ padding: 'var(--ds-size-2)' }}>
      <Rad node={root} dybde={0} valgtId={valgtId} onSelect={onSelect} />
    </div>
  );
}

function Rad({ node, dybde, valgtId, onSelect }: { node: VilkarstreNode; dybde: number; valgtId?: string; onSelect: (n: VilkarstreNode) => void }) {
  const valgt = node.id === valgtId;

  return (
    <>
      <div
        role="treeitem"
        aria-selected={valgt}
        tabIndex={0}
        onClick={() => onSelect(node)}
        onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSelect(node); } }}
        style={{
          display: 'flex', alignItems: 'center', gap: 'var(--ds-size-2)',
          paddingBlock: 'var(--ds-size-1)',
          paddingInlineStart: `calc(var(--ds-size-3) + ${dybde} * var(--ds-size-5))`,
          borderRadius: 'var(--ds-border-radius-default)',
          background: valgt ? 'var(--ds-color-accent-surface-tinted)' : 'transparent',
          cursor: 'pointer',
        }}
      >
        <Tag data-size="sm" data-color={node.kind === 'regelnode' ? 'accent' : 'info'}>
          {node.kind === 'regelnode' ? 'Regelnode' : 'Vilkår'}
        </Tag>
        <span style={{ fontWeight: node.erRotnode ? 700 : 500 }}>{node.tittel}</span>
        {node.kind === 'regelnode' && (
          <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
            barn: {node.barnOperator}{node.erRotnode ? ' · rotnode' : ''}
          </span>
        )}
        {node.kind === 'vilkar' && (
          <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
            {node.vilkarstype} · {node.vurderingstype}{node.erFormel ? ' · formel' : ''}
          </span>
        )}
      </div>

      {node.children.map((barn) => (
        <Rad key={barn.id} node={barn} dybde={dybde + 1} valgtId={valgtId} onSelect={onSelect} />
      ))}

      {node.unntak.map((u) => (
        <div key={u.id}>
          <div
            style={{
              paddingBlock: 'var(--ds-size-1)',
              paddingInlineStart: `calc(var(--ds-size-3) + ${dybde + 1} * var(--ds-size-5))`,
              fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-warning-text-default)',
            }}
          >
            <Tag data-size="sm" data-color="warning">Unntak</Tag>{' '}
            {u.tittel} — med mindre «{u.betingelse.tittel}»
          </div>
          <Rad node={u.betingelse} dybde={dybde + 2} valgtId={valgtId} onSelect={onSelect} />
        </div>
      ))}
    </>
  );
}
