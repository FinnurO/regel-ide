import { useEffect, useState } from 'react';
import { Button, Field, Heading, Label, Link, Paragraph, Select, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RettskildeNodeDto, RettskildeReferanseDto, RettskildeSammendrag } from '../api/types';
import { MinimalEditor } from './MinimalEditor';

const DOKUMENTTYPER = [
  { id: 'kommentar', label: 'Kommentar' },
  { id: 'retningslinje', label: 'Retningslinje' },
  { id: 'instruks', label: 'Instruks' },
  { id: 'handbok', label: 'Håndbok' },
];

const FESTE_NIVAER = [
  { id: 'kapittel', label: 'Kapittel' },
  { id: 'bestemmelse', label: 'Bestemmelse' },
  { id: 'ledd', label: 'Ledd' },
  { id: 'bokstav', label: 'Bokstav' },
];

/** Utleder bindende lokalt, kun for visning før lagring — tjenesten (RegelIde.Data) er alltid autoritativ. */
function utledBindende(dokumenttype: string): boolean {
  return dokumenttype !== 'kommentar';
}

interface KommentarRedigeringProps {
  handbokId: string;
  /** 'ny': oppretter en kommentarseksjon under parentNodeId. 'rediger': redigerer et eksisterende `node` (oppretter ny versjon). */
  mode: 'ny' | 'rediger';
  parentNodeId?: string;
  node?: RettskildeNodeDto;
  alleRettskilder: RettskildeSammendrag[];
  onLagret: (node: RettskildeNodeDto) => void;
  onAvbryt?: () => void;
}

/**
 * Forfatterflate for én håndbok-kommentarseksjon (AK-3.3.8–3.3.12). Dokumenttype/bindende, feste_niva,
 * marginord og selve rik-teksten (MinimalEditor) her; lovreferanse-kobling, versjonshistorikk,
 * revisjonsmerking og publisering er kun tilgjengelig i 'rediger'-modus (krever en eksisterende node).
 */
export function KommentarRedigering({ handbokId, mode, parentNodeId, node, alleRettskilder, onLagret, onAvbryt }: KommentarRedigeringProps) {
  const metadata = node?.handbokMetadata ?? null;

  const [nummer, setNummer] = useState(node?.nummer ?? '');
  const [overskrift, setOverskrift] = useState(node?.overskrift ?? '');
  const [tekstHtml, setTekstHtml] = useState(node?.tekst ?? '');
  const [dokumenttype, setDokumenttype] = useState(metadata?.dokumenttype ?? 'kommentar');
  const [festeNiva, setFesteNiva] = useState(metadata?.festeNiva ?? 'ledd');
  const [marginord, setMarginord] = useState<string[]>(metadata?.marginord ?? []);
  const [nyttMarginord, setNyttMarginord] = useState('');

  const [lagrer, setLagrer] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);

  const [referanser, setReferanser] = useState<RettskildeReferanseDto[]>([]);
  const [tilRettskildeId, setTilRettskildeId] = useState('');
  const [tilEid, setTilEid] = useState('');
  const [referanseFeil, setReferanseFeil] = useState<string | null>(null);

  const [revisjonsgrunn, setRevisjonsgrunn] = useState('');
  const [godkjentAv, setGodkjentAv] = useState('');
  const [handlingFeil, setHandlingFeil] = useState<string | null>(null);

  const [versjoner, setVersjoner] = useState<RettskildeNodeDto[] | null>(null);

  useEffect(() => {
    if (mode !== 'rediger' || !node) return;
    api.hentReferanser(handbokId).then((alle) => setReferanser(alle.filter((r) => r.fraNodeId === node.id)));
  }, [mode, node, handbokId]);

  async function lagre() {
    if (!nummer.trim()) {
      setFeil('Nummer kan ikke være tomt.');
      return;
    }
    setFeil(null);
    setLagrer(true);
    try {
      if (mode === 'ny') {
        if (!parentNodeId) throw new Error('Mangler foreldrenode.');
        const opprettet = await api.opprettKommentarNode(handbokId, {
          parentNodeId,
          nummer: nummer.trim(),
          overskrift: overskrift.trim() || null,
          tekstHtml,
          dokumenttype,
          festeNiva,
          marginord,
        });
        onLagret(opprettet);
      } else if (node) {
        const redigert = await api.redigerKommentarNode(handbokId, node.id, {
          tekstHtml,
          overskrift: overskrift.trim() || null,
          dokumenttype,
          festeNiva,
          marginord,
        });
        onLagret(redigert);
      }
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  async function kobleLovreferanse() {
    if (!node || !tilRettskildeId || !tilEid.trim()) return;
    setReferanseFeil(null);
    try {
      const referanse = await api.kobleLovreferanse(handbokId, node.id, { tilRettskildeId, tilEid: tilEid.trim() });
      setReferanser((forrige) => [...forrige, referanse]);
      setTilEid('');
    } catch (err) {
      setReferanseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling.');
    }
  }

  async function fjernLovreferanse(referanseId: string) {
    if (!node) return;
    try {
      await api.fjernLovreferanse(handbokId, node.id, referanseId);
      setReferanser((forrige) => forrige.filter((r) => r.id !== referanseId));
    } catch (err) {
      setReferanseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved fjerning.');
    }
  }

  async function settRevisjonsmerke() {
    if (!node || !revisjonsgrunn.trim()) return;
    setHandlingFeil(null);
    try {
      await api.settRevisjonsmerke(handbokId, node.id, { revisjonsgrunn: revisjonsgrunn.trim() });
      onLagret({ ...node, handbokMetadata: { ...node.handbokMetadata!, status: 'ma_revideres', revisjonsgrunn: revisjonsgrunn.trim() } });
      setRevisjonsgrunn('');
    } catch (err) {
      setHandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved revisjonsmerking.');
    }
  }

  async function publiser() {
    if (!node) return;
    setHandlingFeil(null);
    try {
      await api.publiserKommentar(handbokId, node.id, { godkjentAv: godkjentAv.trim() || null });
      onLagret({ ...node, handbokMetadata: { ...node.handbokMetadata!, status: 'publisert' } });
    } catch (err) {
      setHandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved publisering.');
    }
  }

  async function visVersjonshistorikk() {
    if (!node) return;
    const liste = await api.hentVersjonshistorikk(handbokId, node.eid);
    setVersjoner(liste);
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--ds-size-4)' }}>
      <div style={{ display: 'flex', gap: 'var(--ds-size-2)', flexWrap: 'wrap', alignItems: 'center' }}>
        <Textfield label="Nummer" value={nummer} onChange={(e) => setNummer(e.target.value)} style={{ width: '8rem' }} disabled={mode === 'rediger'} />
        <Textfield label="Overskrift" value={overskrift} onChange={(e) => setOverskrift(e.target.value)} style={{ flex: 1, minWidth: '12rem' }} />
      </div>

      <div style={{ display: 'flex', gap: 'var(--ds-size-3)', flexWrap: 'wrap', alignItems: 'flex-end' }}>
        <Field style={{ width: '12rem' }}>
          <Label data-size="sm">Dokumenttype</Label>
          <Select value={dokumenttype} onChange={(e) => setDokumenttype(e.target.value)}>
            {DOKUMENTTYPER.map((d) => (
              <Select.Option key={d.id} value={d.id}>
                {d.label}
              </Select.Option>
            ))}
          </Select>
        </Field>
        <Tag data-color={utledBindende(dokumenttype) ? 'warning' : 'neutral'} data-size="sm">
          {utledBindende(dokumenttype) ? 'Bindende' : 'Ikke bindende'} (utledet)
        </Tag>
        <Field style={{ width: '10rem' }}>
          <Label data-size="sm">Festenivå</Label>
          <Select value={festeNiva} onChange={(e) => setFesteNiva(e.target.value)}>
            {FESTE_NIVAER.map((n) => (
              <Select.Option key={n.id} value={n.id}>
                {n.label}
              </Select.Option>
            ))}
          </Select>
        </Field>
        {metadata && <Tag data-color={metadata.status === 'publisert' ? 'success' : metadata.status === 'ma_revideres' ? 'danger' : 'neutral'} data-size="sm">{metadata.status}</Tag>}
      </div>

      <div>
        <Label data-size="sm" style={{ display: 'block', marginBottom: 'var(--ds-size-1)' }}>
          Marginord
        </Label>
        <div style={{ display: 'flex', gap: 'var(--ds-size-2)', flexWrap: 'wrap', alignItems: 'center', marginBottom: 'var(--ds-size-2)' }}>
          {marginord.map((ord) => (
            <Tag key={ord} data-color="neutral" data-size="sm">
              {ord}{' '}
              <button
                type="button"
                aria-label={`Fjern ${ord}`}
                onClick={() => setMarginord((forrige) => forrige.filter((o) => o !== ord))}
                style={{ border: 'none', background: 'none', cursor: 'pointer', font: 'inherit', padding: 0, marginInlineStart: 4 }}
              >
                ×
              </button>
            </Tag>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 'var(--ds-size-2)' }}>
          <Textfield
            aria-label="Nytt marginord"
            data-size="sm"
            value={nyttMarginord}
            onChange={(e) => setNyttMarginord(e.target.value)}
            onKeyDown={(e) => {
              if (e.key !== 'Enter' || !nyttMarginord.trim()) return;
              e.preventDefault();
              setMarginord((forrige) => [...forrige, nyttMarginord.trim()]);
              setNyttMarginord('');
            }}
            placeholder="Skriv og trykk Enter"
            style={{ maxWidth: '16rem' }}
          />
        </div>
      </div>

      <div>
        <Label data-size="sm" style={{ display: 'block', marginBottom: 'var(--ds-size-1)' }}>
          Kommentartekst
        </Label>
        <MinimalEditor
          value={tekstHtml}
          onChange={(html) => setTekstHtml(html)}
          referanser={alleRettskilder.map((r) => ({ kind: 'rettskilde', id: r.id, label: r.kortnavn ?? r.tittel }))}
        />
      </div>

      {feil && <div className="feilmelding">{feil}</div>}
      <div style={{ display: 'flex', gap: 'var(--ds-size-2)' }}>
        <Button onClick={lagre} disabled={lagrer}>
          {lagrer ? 'Lagrer …' : mode === 'ny' ? 'Opprett' : 'Lagre (ny versjon)'}
        </Button>
        {onAvbryt && (
          <Button variant="tertiary" onClick={onAvbryt}>
            Avbryt
          </Button>
        )}
      </div>

      {mode === 'rediger' && node && (
        <>
          <section style={{ borderTop: '1px solid var(--ds-color-neutral-border-subtle)', paddingTop: 'var(--ds-size-4)' }}>
            <Heading level={3} data-size="2xs" style={{ marginBottom: 'var(--ds-size-2)' }}>
              Lovreferanser
            </Heading>
            {referanser.length > 0 && (
              <ul style={{ listStyle: 'none', padding: 0, margin: '0 0 var(--ds-size-2)' }}>
                {referanser.map((r) => (
                  <li key={r.id} style={{ display: 'flex', alignItems: 'center', gap: 'var(--ds-size-2)' }}>
                    <span style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>{r.tilEid}</span>
                    <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernLovreferanse(r.id)}>
                      Fjern
                    </Button>
                  </li>
                ))}
              </ul>
            )}
            <div style={{ display: 'flex', gap: 'var(--ds-size-2)', alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <Field style={{ width: '14rem' }}>
                <Label data-size="sm">Rettskilde</Label>
                <Select value={tilRettskildeId} onChange={(e) => setTilRettskildeId(e.target.value)}>
                  <Select.Option value="">Velg …</Select.Option>
                  {alleRettskilder.map((r) => (
                    <Select.Option key={r.id} value={r.id}>
                      {r.kortnavn ?? r.tittel}
                    </Select.Option>
                  ))}
                </Select>
              </Field>
              <Textfield label="eId" data-size="sm" value={tilEid} onChange={(e) => setTilEid(e.target.value)} style={{ flex: 1, minWidth: '14rem' }} />
              <Button data-size="sm" onClick={kobleLovreferanse} disabled={!tilRettskildeId || !tilEid.trim()}>
                Koble
              </Button>
            </div>
            {referanseFeil && <div className="feilmelding" style={{ marginTop: 'var(--ds-size-2)' }}>{referanseFeil}</div>}
          </section>

          <section style={{ borderTop: '1px solid var(--ds-color-neutral-border-subtle)', paddingTop: 'var(--ds-size-4)' }}>
            <Heading level={3} data-size="2xs" style={{ marginBottom: 'var(--ds-size-2)' }}>
              Status og publisering
            </Heading>
            <div style={{ display: 'flex', gap: 'var(--ds-size-2)', flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: 'var(--ds-size-2)' }}>
              <Textfield
                label="Godkjent av (kun bindende seksjoner)"
                data-size="sm"
                value={godkjentAv}
                onChange={(e) => setGodkjentAv(e.target.value)}
                style={{ minWidth: '14rem' }}
              />
              <Button data-size="sm" onClick={publiser}>
                Publiser
              </Button>
            </div>
            <div style={{ display: 'flex', gap: 'var(--ds-size-2)', flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <Textfield
                label="Revisjonsgrunn"
                data-size="sm"
                value={revisjonsgrunn}
                onChange={(e) => setRevisjonsgrunn(e.target.value)}
                style={{ minWidth: '14rem' }}
              />
              <Button data-size="sm" variant="secondary" onClick={settRevisjonsmerke} disabled={!revisjonsgrunn.trim()}>
                Merk «Må revideres»
              </Button>
            </div>
            {handlingFeil && <div className="feilmelding" style={{ marginTop: 'var(--ds-size-2)' }}>{handlingFeil}</div>}
            {metadata?.status === 'ma_revideres' && metadata.revisjonsgrunn && (
              <Paragraph style={{ color: 'var(--ds-color-danger-text-default)', marginTop: 'var(--ds-size-2)' }}>
                Må revideres: {metadata.revisjonsgrunn}
              </Paragraph>
            )}
          </section>

          <section style={{ borderTop: '1px solid var(--ds-color-neutral-border-subtle)', paddingTop: 'var(--ds-size-4)' }}>
            <Link href="#" data-size="sm" onClick={(e) => { e.preventDefault(); visVersjonshistorikk(); }}>
              Se tidligere versjoner
            </Link>
            {versjoner && (
              <ul style={{ listStyle: 'none', padding: 0, margin: 'var(--ds-size-2) 0 0' }}>
                {versjoner.map((v) => (
                  <li key={v.id} style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                    Versjon {v.versjon} — {v.overskrift ?? '(uten tittel)'}
                    {v.id === node.id && ' (gjeldende)'}
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  );
}
