import { Alert, Button, Checkbox, Field, Label, Radio, Select, Textarea, Textfield } from '@digdir/designsystemet-react';
import type { Kunnskapsstatus, RettskildeNodeDto } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KildeUtenforKorpusVelger } from '../strukturkant/KildeUtenforKorpus';
import { KUNNSKAPSSTATUS_VISNING } from '../strukturkant/Utsagnsstatus';
import {
  grunnlagFeil, sikkerhetFeil, TOLKNINGSARGUMENTER, VEIVISER_KUNNSKAPSSTATUSER,
  type GrunnlagUtkast, type SikkerhetUtkast,
} from './utsagnsgrunnlag';

/**
 * [Ny, issue #385 AC8–AC10, Johann 2026-10-16] Veiviserens to nye steg som delte, kontrollerte komponenter — det samme grunnlaget og den
 * samme statusen for alt veiviseren oppretter (navneform, aktør, medlemskap, binding). Reglene står i `utsagnsgrunnlag.ts` og speiler
 * serveren. Ingenting forhåndsvelges (CLAUDE.md §8, docs/09 §31).
 */

/** Etikett for en node i velgeren: paragrafen foran leddet («§ 10 ledd 1»), som docs/09 §38. */
export function nodeEtikett(n: RettskildeNodeDto, perId: Map<string, RettskildeNodeDto>): string {
  const tittel = n.overskrift ? ` — ${n.overskrift}` : '';
  if (n.nodeType === 'side') return 'Hele siden';
  if (n.nodeType === 'kapittel') return `Kapittel ${n.nummer ?? n.eid}${tittel}`;
  if (n.nodeType === 'paragraf') return `${n.nummer ?? n.eid}${tittel}`;
  const forelder = n.parentNodeId ? perId.get(n.parentNodeId) : undefined;
  return `${forelder ? `${nodeEtikett(forelder, perId)} ` : ''}${n.nodeType} ${n.nummer ?? ''}`.trim();
}

export function GrunnlagSteg({
  verdi, onEndre, noder, kandidatEid, disabled,
}: {
  verdi: GrunnlagUtkast;
  onEndre: (v: GrunnlagUtkast) => void;
  noder: RettskildeNodeDto[] | null;
  /** Noden navnet står i — vises først i lista, men velges ikke for saksbehandleren. */
  kandidatEid: string;
  disabled: boolean;
}) {
  const perId = new Map((noder ?? []).map((n) => [n.id, n]));
  const hjemmelnoder = (noder ?? []).filter((n) => !n.opphevet && ['side', 'paragraf', 'ledd', 'punkt', 'avslutning'].includes(n.nodeType));
  const her = hjemmelnoder.find((n) => n.eid === kandidatEid);
  const sortert = her ? [her, ...hjemmelnoder.filter((n) => n !== her)] : hjemmelnoder;
  const feil = grunnlagFeil(verdi);
  const sett = (endring: Partial<GrunnlagUtkast>) => onEndre({ ...verdi, ...endring });

  return (
    <>
      <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
        <Radio name="grunnlag" label="Hjemmel i korpus" value="hjemmel" disabled={disabled}
          description="En bestemmelse i denne rettskilden sier det — f.eks. leddet der navnet står."
          checked={verdi.grunnlag === 'hjemmel'} onChange={() => sett({ grunnlag: 'hjemmel' })} />
        <Radio name="grunnlag" label="Kilde utenfor korpus" value="kilde" disabled={disabled}
          description="Kgl.res., instruks, vedtekter, forarbeider eller en nettside — med én eller flere lenker til dokumentasjonen."
          checked={verdi.grunnlag === 'kilde'} onChange={() => sett({ grunnlag: 'kilde' })} />
        <Radio name="grunnlag" label="Hjemmel mangler" value="mangler" disabled={disabled}
          description="Ingen kilde er funnet ennå. Lagres som et synlig hull (uavklart), ikke som et tomt felt."
          checked={verdi.grunnlag === 'mangler'} onChange={() => sett({ grunnlag: 'mangler' })} />
      </Field>

      {verdi.grunnlag === 'hjemmel' && (
        <Field data-size="sm" style={{ maxWidth: '36rem', marginBottom: '0.75rem' }}>
          <Label>Bestemmelsen</Label>
          <Select data-size="sm" value={verdi.hjemmelEid} disabled={disabled} onChange={(e) => sett({ hjemmelEid: e.target.value })}>
            <Select.Option value="">Velg …</Select.Option>
            {sortert.map((n) => (
              <Select.Option key={n.id} value={n.eid}>
                {nodeEtikett(n, perId)}{n.eid === kandidatEid ? ' (her står navnet)' : ''}
              </Select.Option>
            ))}
          </Select>
        </Field>
      )}

      {verdi.grunnlag === 'kilde' && (
        <div style={{ display: 'grid', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <Textfield data-size="sm" label="Kilden" description="Hva dokumentet er, f.eks. «Om Tilsynsutvalget for dommere» eller «Prop. 1 S (2025–2026)»."
            value={verdi.kildeTekst} readOnly={disabled} onChange={(e) => sett({ kildeTekst: e.target.value })} style={{ maxWidth: '36rem' }} />
          <KildeUtenforKorpusVelger type={verdi.kildeType} dokumentasjon={verdi.kildeDokumentasjon} disabled={disabled}
            onType={(kildeType) => sett({ kildeType })} onDokumentasjon={(kildeDokumentasjon) => sett({ kildeDokumentasjon })} />
          {verdi.lenker.map((l, i) => (
            <div key={i} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end' }}>
              <Textfield data-size="sm" label={i === 0 ? 'Lenke til dokumentasjonen' : `Lenke ${i + 1}`} value={l} readOnly={disabled}
                placeholder="https://" style={{ minWidth: '28rem' }}
                onChange={(e) => sett({ lenker: verdi.lenker.map((x, j) => (j === i ? e.target.value : x)) })} />
              {verdi.lenker.length > 1 && !disabled && (
                <Button data-size="sm" variant="tertiary" onClick={() => sett({ lenker: verdi.lenker.filter((_, j) => j !== i) })}>Fjern</Button>
              )}
            </div>
          ))}
          {!disabled && (
            <div><Button data-size="sm" variant="secondary" onClick={() => sett({ lenker: [...verdi.lenker, ''] })}>+ Legg til lenke</Button></div>
          )}
        </div>
      )}

      {verdi.grunnlag === 'mangler' && (
        <Alert data-color="warning" data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Lagres med synlig hull: kunnskapsstatus «Uavklart», og begrunnelsen i neste steg skal si hva som mangler.
        </Alert>
      )}
      {!disabled && feil && verdi.grunnlag !== null && <Metatekst>{feil}</Metatekst>}
    </>
  );
}

export function SikkerhetSteg({
  grunnlag, verdi, onEndre, disabled,
}: {
  grunnlag: GrunnlagUtkast;
  verdi: SikkerhetUtkast;
  onEndre: (v: SikkerhetUtkast) => void;
  disabled: boolean;
}) {
  const sett = (endring: Partial<SikkerhetUtkast>) => onEndre({ ...verdi, ...endring });
  const feil = sikkerhetFeil(grunnlag, verdi);
  return (
    <>
      <Field data-size="sm" style={{ maxWidth: '22rem', marginBottom: '0.5rem' }}>
        <Label>Kunnskapsstatus</Label>
        <Select data-size="sm" value={verdi.kunnskapsstatus} disabled={disabled}
          onChange={(e) => sett({ kunnskapsstatus: e.target.value as Kunnskapsstatus | '' })}>
          <Select.Option value="">Ikke vurdert</Select.Option>
          {VEIVISER_KUNNSKAPSSTATUSER.map((k) => <Select.Option key={k} value={k}>{KUNNSKAPSSTATUS_VISNING[k].tekst}</Select.Option>)}
        </Select>
      </Field>
      {verdi.kunnskapsstatus && <Metatekst style={{ marginBottom: '0.5rem' }}>{KUNNSKAPSSTATUS_VISNING[verdi.kunnskapsstatus].forklaring}</Metatekst>}

      {verdi.kunnskapsstatus === 'fortolkning' && (
        <Field data-size="sm" style={{ marginBottom: '0.5rem' }}>
          <Label>Tolkningsargumenter (minst ett)</Label>
          <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
            {TOLKNINGSARGUMENTER.map((a) => (
              <Checkbox key={a.verdi} data-size="sm" label={a.tekst} checked={verdi.argumenter.includes(a.verdi)} disabled={disabled}
                onChange={(e) => sett({ argumenter: e.target.checked ? [...verdi.argumenter, a.verdi] : verdi.argumenter.filter((x) => x !== a.verdi) })} />
            ))}
          </div>
        </Field>
      )}

      <Field data-size="sm" style={{ marginBottom: '0.5rem' }}>
        <Label>Godkjenning</Label>
        <Radio name="godkjenning" label="Faglig vurdert" value="faglig_vurdert" disabled={disabled}
          description="Vurdert av deg, men ikke godkjent. Står som forslag til noen godkjenner det."
          checked={verdi.godkjenning === 'faglig_vurdert'} onChange={() => sett({ godkjenning: 'faglig_vurdert' })} />
        <Radio name="godkjenning" label="Godkjent" value="godkjent" disabled={disabled}
          description="Gjelder fra nå. En aktørtype settes på aktøren først når den er godkjent."
          checked={verdi.godkjenning === 'godkjent'} onChange={() => sett({ godkjenning: 'godkjent' })} />
      </Field>

      <Textarea data-size="sm" aria-label="Begrunnelse" placeholder={grunnlag.grunnlag === 'mangler' ? 'Hva mangler?' : 'Begrunnelse (fritekst)'}
        value={verdi.begrunnelse} readOnly={disabled} rows={2} onChange={(e) => sett({ begrunnelse: e.target.value })}
        style={{ maxWidth: '36rem', marginBottom: '0.5rem' }} />
      {!disabled && feil && <Metatekst>{feil}</Metatekst>}
    </>
  );
}
