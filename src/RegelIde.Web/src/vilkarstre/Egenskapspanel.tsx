/**
 * Egenskapspanel
 * ------------------------------------------------------------------
 * Faner for valgt node i vilkårstreet (produktkrav kap. 3.4): Generelt,
 * Tekster, Metadata, Historikk. Feltene varierer per nodetype
 * (§1.8-1.10 i domenemodellen). «Standardref» foldes inn i Generelt
 * (samme felt, ingen reell duplisering) — «Output» (eksportformater)
 * er utsatt til byggesteg 6s eksportmotor, ingen stub-knapper her.
 *
 * BegrepId/SkjonnsgrunnlagBegrepId vises read-only (kun term-oppslag) i
 * denne runden — en full søk-og-velg-UI for begreper er ikke bygget ennå,
 * siden seed-dataene allerede dekker det som trengs for å bevise flyten.
 */
import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink } from 'react-router';
import { Button, Link, Paragraph, Select, Tabs, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import type { BegrepDto, JuridiskGrunnlagInput, ProveniensDto, RegelnodeDto, RettskildeSammendrag, UnntakDto, VilkarDto } from '../api/types';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

export type EgenskapspanelNode = { kind: 'vilkar' | 'regelnode' | 'unntak'; id: string };

interface EgenskapspanelProps {
  node: EgenskapspanelNode;
  begreper: BegrepDto[];
  rettskilder: RettskildeSammendrag[];
  onEndret: () => void;
}

export function Egenskapspanel({ node, begreper, rettskilder, onEndret }: EgenskapspanelProps) {
  const [fane, setFane] = useState('generelt');
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => setFane('generelt'), [node.id]);

  if (node.kind === 'vilkar') return <VilkarPanel id={node.id} fane={fane} setFane={setFane} begreper={begreper} rettskilder={rettskilder} feil={feil} setFeil={setFeil} onEndret={onEndret} />;
  if (node.kind === 'regelnode') return <RegelnodePanel id={node.id} fane={fane} setFane={setFane} rettskilder={rettskilder} feil={feil} setFeil={setFeil} onEndret={onEndret} />;
  return <UnntakPanel id={node.id} fane={fane} setFane={setFane} rettskilder={rettskilder} feil={feil} setFeil={setFeil} onEndret={onEndret} />;
}

/** Juridisk grunnlag-listen — hver oppføring blir en lenke til rettskilden (§X.eId matches) når den finnes. */
function JuridiskGrunnlagListe({ grunnlag, rettskilder }: { grunnlag: JuridiskGrunnlagInput[]; rettskilder: RettskildeSammendrag[] }) {
  if (grunnlag.length === 0) return <>—</>;
  return (
    <>
      {grunnlag.map((g, i) => {
        const href = rettskildeLenke(g.eId, rettskilder);
        return (
          <span key={i}>
            {i > 0 && ', '}
            {href ? (
              <Link asChild>
                <RouterLink to={href}>{g.kilde} {g.eId}</RouterLink>
              </Link>
            ) : (
              `${g.kilde} ${g.eId}`
            )}
          </span>
        );
      })}
    </>
  );
}

function Historikk({ liste }: { liste: ProveniensDto[] | null }) {
  if (liste === null) return <Paragraph>Laster …</Paragraph>;
  if (liste.length === 0) return <Paragraph>Ingen historikk ennå.</Paragraph>;
  return (
    <ul>
      {liste.map((p) => (
        <li key={p.id} style={{ fontSize: 'var(--ds-font-size-1)' }}>
          {new Date(p.dato).toLocaleString('nb-NO')} — {p.handling} ({p.endretAv})
        </li>
      ))}
    </ul>
  );
}

function FelleFaner({ fane, setFane }: { fane: string; setFane: (f: string) => void }) {
  return (
    <Tabs value={fane} onChange={setFane} style={{ marginBottom: '1rem' }}>
      <Tabs.List>
        <Tabs.Tab value="generelt">Generelt</Tabs.Tab>
        <Tabs.Tab value="tekster">Tekster</Tabs.Tab>
        <Tabs.Tab value="metadata">Metadata</Tabs.Tab>
        <Tabs.Tab value="historikk">Historikk</Tabs.Tab>
      </Tabs.List>
    </Tabs>
  );
}

function VilkarPanel({ id, fane, setFane, begreper, rettskilder, feil, setFeil, onEndret }: {
  id: string; fane: string; setFane: (f: string) => void; begreper: BegrepDto[]; rettskilder: RettskildeSammendrag[];
  feil: string | null; setFeil: (f: string | null) => void; onEndret: () => void;
}) {
  const [vilkar, setVilkar] = useState<VilkarDto | null>(null);
  const [historikk, setHistorikk] = useState<ProveniensDto[] | null>(null);
  const [tittel, setTittel] = useState('');
  const [beskrivelse, setBeskrivelse] = useState('');
  const [vilkarstype, setVilkarstype] = useState('formell');
  const [vurderingstype, setVurderingstype] = useState('regelbasert');
  const [veiledningBruker, setVeiledningBruker] = useState('');
  const [veiledningSaksbehandler, setVeiledningSaksbehandler] = useState('');
  const [lagrer, setLagrer] = useState(false);

  useEffect(() => {
    setVilkar(null);
    setHistorikk(null);
    api.hentVilkar(id).then((v) => {
      setVilkar(v);
      setTittel(v.tittel);
      setBeskrivelse(v.beskrivelse ?? '');
      setVilkarstype(v.vilkarstype);
      setVurderingstype(v.vurderingstype);
      setVeiledningBruker(v.veiledningTilBruker ?? '');
      setVeiledningSaksbehandler(v.veiledningTilSaksbehandler ?? '');
    });
    api.hentVilkarHistorikk(id).then(setHistorikk);
  }, [id]);

  async function lagre(e: FormEvent) {
    e.preventDefault();
    if (!vilkar) return;
    setFeil(null);
    setLagrer(true);
    try {
      const oppdatert = await api.oppdaterVilkar(id, {
        tittel: tittel.trim(), beskrivelse: beskrivelse.trim() || null, generiskMal: vilkar.generiskMal,
        vilkarstype, gjelderRolle: vilkar.gjelderRolle, juridiskGrunnlag: vilkar.juridiskGrunnlag,
        begrepId: vilkar.begrepId, vurderingstype, parametreJson: vilkar.parametreJson,
        skjonnsgrunnlagBegrepId: vilkar.skjonnsgrunnlagBegrepId, skjonnsmomenter: vilkar.skjonnsmomenter,
        kreverDokumentasjon: vilkar.kreverDokumentasjon, eskaleringsrolle: vilkar.eskaleringsrolle,
        veiledningTilBruker: veiledningBruker.trim() || null, veiledningTilSaksbehandler: veiledningSaksbehandler.trim() || null,
        erFormel: vilkar.erFormel, formelBeskrivelse: vilkar.formelBeskrivelse,
      });
      setVilkar(oppdatert);
      onEndret();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  async function endreStatus(nyStatus: string) {
    setFeil(null);
    try {
      const oppdatert = await api.settVilkarStatus(id, { status: nyStatus });
      setVilkar(oppdatert);
      onEndret();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    }
  }

  if (!vilkar) return <Paragraph>Laster …</Paragraph>;
  const begrep = begreper.find((b) => b.id === vilkar.begrepId);
  const skjonnsgrunnlag = begreper.find((b) => b.id === vilkar.skjonnsgrunnlagBegrepId);

  return (
    <div>
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.75rem' }}>
        <Tag data-color="info">Vilkår</Tag>
        <Tag data-color="neutral">{vilkar.status}</Tag>
        {vilkar.erFormel && <Tag data-color="warning">Formel</Tag>}
      </div>
      <FelleFaner fane={fane} setFane={setFane} />
      {feil && <div className="feilmelding" style={{ marginBottom: '0.75rem' }}>{feil}</div>}

      {fane === 'generelt' && (
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '32rem' }}>
          <Textfield label="Tittel" value={tittel} onChange={(e) => setTittel(e.target.value)} required />
          <Textarea label="Beskrivelse" value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={2} />
          <Select label="Vilkårstype" value={vilkarstype} onChange={(e) => setVilkarstype(e.target.value)}>
            <Select.Option value="formell">Formell</Select.Option>
            <Select.Option value="materiell">Materiell</Select.Option>
          </Select>
          <Select label="Vurderingstype" value={vurderingstype} onChange={(e) => setVurderingstype(e.target.value)}>
            <Select.Option value="regelbasert">Regelbasert</Select.Option>
            <Select.Option value="skjonnsbasert">Skjønnsbasert</Select.Option>
            <Select.Option value="hybrid">Hybrid</Select.Option>
          </Select>
          {begrep && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>
              Begrep: <Link asChild><RouterLink to={`/begreper/${begrep.id}`}>«{begrep.term}»</RouterLink></Link>
            </Paragraph>
          )}
          {(vurderingstype === 'skjonnsbasert' || vurderingstype === 'hybrid') && (
            <>
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                Skjønnsgrunnlag:{' '}
                {skjonnsgrunnlag ? (
                  <Link asChild><RouterLink to={`/begreper/${skjonnsgrunnlag.id}`}>«{skjonnsgrunnlag.term}»</RouterLink></Link>
                ) : (
                  '(ikke satt)'
                )}
              </Paragraph>
              {vilkar.skjonnsmomenter.length > 0 && (
                <ul style={{ margin: 0 }}>
                  {vilkar.skjonnsmomenter.map((m, i) => <li key={i} style={{ fontSize: 'var(--ds-font-size-1)' }}>{m.navn}</li>)}
                </ul>
              )}
            </>
          )}
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      )}

      {fane === 'tekster' && (
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '32rem' }}>
          <Textarea label="Veiledningstekst til bruker" value={veiledningBruker} onChange={(e) => setVeiledningBruker(e.target.value)} rows={3} />
          <Textarea label="Veiledning til saksbehandler" value={veiledningSaksbehandler} onChange={(e) => setVeiledningSaksbehandler(e.target.value)} rows={3} />
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      )}

      {fane === 'metadata' && (
        <div style={{ maxWidth: '32rem' }}>
          <Paragraph>Versjon: {vilkar.versjon}</Paragraph>
          <Paragraph>Juridisk grunnlag: <JuridiskGrunnlagListe grunnlag={vilkar.juridiskGrunnlag} rettskilder={rettskilder} /></Paragraph>
          <Select label="Status" value={vilkar.status} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
            {STATUSER.map((s) => <Select.Option key={s} value={s}>{s}</Select.Option>)}
          </Select>
        </div>
      )}

      {fane === 'historikk' && <Historikk liste={historikk} />}
    </div>
  );
}

function RegelnodePanel({ id, fane, setFane, rettskilder, feil, setFeil, onEndret }: {
  id: string; fane: string; setFane: (f: string) => void; rettskilder: RettskildeSammendrag[];
  feil: string | null; setFeil: (f: string | null) => void; onEndret: () => void;
}) {
  const [regelnode, setRegelnode] = useState<RegelnodeDto | null>(null);
  const [historikk, setHistorikk] = useState<ProveniensDto[] | null>(null);
  const [tittel, setTittel] = useState('');
  const [beskrivelse, setBeskrivelse] = useState('');
  const [innvilgelseTekst, setInnvilgelseTekst] = useState('');
  const [avslagTekst, setAvslagTekst] = useState('');
  const [lagrer, setLagrer] = useState(false);

  useEffect(() => {
    setRegelnode(null);
    setHistorikk(null);
    api.hentRegelnode(id).then((r) => {
      setRegelnode(r);
      setTittel(r.tittel);
      setBeskrivelse(r.beskrivelse ?? '');
      setInnvilgelseTekst(r.innvilgelseTekst ?? '');
      setAvslagTekst(r.avslagTekst ?? '');
    });
    api.hentRegelnodeHistorikk(id).then(setHistorikk);
  }, [id]);

  async function lagre(e: FormEvent) {
    e.preventDefault();
    if (!regelnode) return;
    setFeil(null);
    setLagrer(true);
    try {
      const oppdatert = await api.oppdaterRegelnode(id, {
        tittel: tittel.trim(), beskrivelse: beskrivelse.trim() || null, generiskMal: regelnode.generiskMal,
        barnOperator: regelnode.barnOperator, utdataNavn: regelnode.utdataNavn, utdataType: regelnode.utdataType,
        erRotnode: regelnode.erRotnode, juridiskGrunnlag: regelnode.juridiskGrunnlag,
        innvilgelseTekst: innvilgelseTekst.trim() || null, avslagTekst: avslagTekst.trim() || null,
      });
      setRegelnode(oppdatert);
      onEndret();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  async function endreOperator(operator: string) {
    setFeil(null);
    try {
      const oppdatert = await api.settRegelnodeOperator(id, { barnOperator: operator });
      setRegelnode(oppdatert);
      onEndret();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved endring av operator.');
    }
  }

  async function endreStatus(nyStatus: string) {
    setFeil(null);
    try {
      const oppdatert = await api.settRegelnodeStatus(id, { status: nyStatus });
      setRegelnode(oppdatert);
      onEndret();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    }
  }

  if (!regelnode) return <Paragraph>Laster …</Paragraph>;

  return (
    <div>
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.75rem' }}>
        <Tag data-color="accent">Regelnode</Tag>
        <Tag data-color="neutral">{regelnode.status}</Tag>
        {regelnode.erRotnode && <Tag data-color="success">Rotnode</Tag>}
      </div>
      <FelleFaner fane={fane} setFane={setFane} />
      {feil && <div className="feilmelding" style={{ marginBottom: '0.75rem' }}>{feil}</div>}

      {fane === 'generelt' && (
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '32rem' }}>
          <Textfield label="Tittel" value={tittel} onChange={(e) => setTittel(e.target.value)} required />
          <Textarea label="Beskrivelse" value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={2} />
          <Select label="Logisk operator (AK-3.4.2)" value={regelnode.barnOperator} onChange={(e) => endreOperator(e.target.value)}>
            <Select.Option value="OG">OG</Select.Option>
            <Select.Option value="ELLER">ELLER</Select.Option>
            <Select.Option value="IKKE">IKKE</Select.Option>
          </Select>
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>
            Utdata: {regelnode.utdataNavn} ({regelnode.utdataType})
          </Paragraph>
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      )}

      {fane === 'tekster' && (
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '32rem' }}>
          <Textarea label="Innvilgelsestekst" value={innvilgelseTekst} onChange={(e) => setInnvilgelseTekst(e.target.value)} rows={3} />
          <Textarea label="Avslagstekst" value={avslagTekst} onChange={(e) => setAvslagTekst(e.target.value)} rows={3} />
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      )}

      {fane === 'metadata' && (
        <div style={{ maxWidth: '32rem' }}>
          <Paragraph>Versjon: {regelnode.versjon}</Paragraph>
          <Paragraph>Juridisk grunnlag: <JuridiskGrunnlagListe grunnlag={regelnode.juridiskGrunnlag} rettskilder={rettskilder} /></Paragraph>
          <Select label="Status" value={regelnode.status} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
            {STATUSER.map((s) => <Select.Option key={s} value={s}>{s}</Select.Option>)}
          </Select>
        </div>
      )}

      {fane === 'historikk' && <Historikk liste={historikk} />}
    </div>
  );
}

function UnntakPanel({ id, fane, setFane, rettskilder, feil, setFeil, onEndret }: {
  id: string; fane: string; setFane: (f: string) => void; rettskilder: RettskildeSammendrag[];
  feil: string | null; setFeil: (f: string | null) => void; onEndret: () => void;
}) {
  const [unntak, setUnntak] = useState<UnntakDto | null>(null);
  const [historikk, setHistorikk] = useState<ProveniensDto[] | null>(null);
  const [tittel, setTittel] = useState('');
  const [beskrivelse, setBeskrivelse] = useState('');
  const [lagrer, setLagrer] = useState(false);

  useEffect(() => {
    setUnntak(null);
    setHistorikk(null);
    api.hentUnntak(id).then((u) => {
      setUnntak(u);
      setTittel(u.tittel);
      setBeskrivelse(u.beskrivelse ?? '');
    });
    api.hentUnntakHistorikk(id).then(setHistorikk);
  }, [id]);

  async function lagre(e: FormEvent) {
    e.preventDefault();
    if (!unntak) return;
    setFeil(null);
    setLagrer(true);
    try {
      const oppdatert = await api.oppdaterUnntak(id, { tittel: tittel.trim(), beskrivelse: beskrivelse.trim() || null, juridiskGrunnlag: unntak.juridiskGrunnlag });
      setUnntak(oppdatert);
      onEndret();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  async function endreStatus(nyStatus: string) {
    setFeil(null);
    try {
      const oppdatert = await api.settUnntakStatus(id, { status: nyStatus });
      setUnntak(oppdatert);
      onEndret();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    }
  }

  if (!unntak) return <Paragraph>Laster …</Paragraph>;

  return (
    <div>
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.75rem' }}>
        <Tag data-color="warning">Unntak</Tag>
        <Tag data-color="neutral">{unntak.status}</Tag>
      </div>
      <FelleFaner fane={fane} setFane={setFane} />
      {feil && <div className="feilmelding" style={{ marginBottom: '0.75rem' }}>{feil}</div>}

      {fane === 'generelt' && (
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '32rem' }}>
          <Textfield label="Tittel" value={tittel} onChange={(e) => setTittel(e.target.value)} required />
          <Textarea label="Beskrivelse" value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={2} />
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
            gjelder_regel og betingelse settes ved opprettelse (INV-3/INV-4) og endres ikke her.
          </Paragraph>
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      )}

      {fane === 'tekster' && (
        <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Unntak har ingen egne tekstfelt (§1.10).</Paragraph>
      )}

      {fane === 'metadata' && (
        <div style={{ maxWidth: '32rem' }}>
          <Paragraph>Versjon: {unntak.versjon}</Paragraph>
          <Paragraph>Juridisk grunnlag: <JuridiskGrunnlagListe grunnlag={unntak.juridiskGrunnlag} rettskilder={rettskilder} /></Paragraph>
          <Select label="Status" value={unntak.status} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
            {STATUSER.map((s) => <Select.Option key={s} value={s}>{s}</Select.Option>)}
          </Select>
        </div>
      )}

      {fane === 'historikk' && <Historikk liste={historikk} />}
    </div>
  );
}
