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
import { Button, Field, Label, Link, Paragraph, Select, Tabs, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import type {
  BegrepDto, DatasettDto, JuridiskGrunnlagInput, ProveniensDto, RegelnodeDto, RettskildeSammendrag, UnntakDto,
  VilkarDto, VilkarstreKommentarDto,
} from '../api/types';
import { MinimalEditor } from '../handbok/MinimalEditor';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

const VEILEDNINGSDOKUMENTTYPER = [
  { id: 'kommentar', label: 'Kommentar' },
  { id: 'hjemmel', label: 'Hjemmel' },
  { id: 'praktisk-rad', label: 'Praktisk råd' },
  { id: 'sjekkliste', label: 'Sjekkliste' },
];

const VEILEDNINGSDOKUMENTTYPE_FARGE: Record<string, 'info' | 'warning' | 'neutral' | 'success'> = {
  hjemmel: 'info',
  'praktisk-rad': 'warning',
  sjekkliste: 'success',
  kommentar: 'neutral',
};

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

/**
 * Juridisk grunnlag — redigerbar liste (2026-07-30, tidligere kun lese-visning). Hver oppføring lenkes
 * til rettskilden når den finnes (samme `rettskildeLenke`-mekanisme). Endringer holdes i lokal state til
 * brukeren trykker Lagre (samme mønster som resten av panelet) — ingen egen lagre-handling her.
 */
function JuridiskGrunnlagRedigering({ grunnlag, rettskilder, onEndre }: {
  grunnlag: JuridiskGrunnlagInput[]; rettskilder: RettskildeSammendrag[]; onEndre: (nyListe: JuridiskGrunnlagInput[]) => void;
}) {
  const [nyRettskildeId, setNyRettskildeId] = useState('');
  const [nyEid, setNyEid] = useState('');

  function leggTil() {
    const rettskilde = rettskilder.find((r) => r.id === nyRettskildeId);
    if (!rettskilde || !nyEid.trim()) return;
    onEndre([...grunnlag, { kilde: rettskilde.kortnavn ?? rettskilde.tittel, eId: nyEid.trim() }]);
    setNyRettskildeId('');
    setNyEid('');
  }

  return (
    <div>
      {grunnlag.length === 0 ? (
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Ingen juridisk grunnlag lagt til.
        </Paragraph>
      ) : (
        <ul style={{ margin: '0 0 0.5rem', padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '0.3rem' }}>
          {grunnlag.map((g, i) => {
            const href = rettskildeLenke(g.eId, rettskilder);
            return (
              <li key={i} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: 'var(--ds-font-size-1)' }}>
                {href ? (
                  <Link asChild><RouterLink to={href}>{g.kilde} {g.eId}</RouterLink></Link>
                ) : (
                  <span>{g.kilde} {g.eId}</span>
                )}
                <Button variant="tertiary" data-color="danger" data-size="sm" type="button"
                  onClick={() => onEndre(grunnlag.filter((_, j) => j !== i))}>
                  Fjern
                </Button>
              </li>
            );
          })}
        </ul>
      )}
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
        <Field>
          <Label>Rettskilde</Label>
          <Select data-size="sm" value={nyRettskildeId} onChange={(e) => setNyRettskildeId(e.target.value)}>
            <Select.Option value="">Velg …</Select.Option>
            {rettskilder.map((r) => <Select.Option key={r.id} value={r.id}>{r.tittel}</Select.Option>)}
          </Select>
        </Field>
        <Textfield data-size="sm" label="eId" value={nyEid} onChange={(e) => setNyEid(e.target.value)}
          style={{ minWidth: '16rem', fontFamily: 'monospace' }} />
        <Button data-size="sm" type="button" variant="secondary" onClick={leggTil} disabled={!nyRettskildeId || !nyEid.trim()}>
          Legg til
        </Button>
      </div>
    </div>
  );
}

/**
 * Input-datasett på et Vilkår (§1.6/§1.8) — egen join-tabell (`vilkar_input_datasett`), derfor egne
 * lagre/fjern-kall direkte mot API-et i stedet for å gå via panelets «Lagre»-knapp (2026-07-30).
 */
function InputDatasettAdministrasjon({ vilkarId }: { vilkarId: string }) {
  const [input, setInput] = useState<DatasettDto[] | null>(null);
  const [alleDatasett, setAlleDatasett] = useState<DatasettDto[]>([]);
  const [nyDatasettId, setNyDatasettId] = useState('');
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    setInput(null);
    api.hentVilkarInput(vilkarId).then(setInput).catch(() => setInput([]));
    api.hentDatasett().then(setAlleDatasett).catch(() => setAlleDatasett([]));
  }, [vilkarId]);

  async function leggTil() {
    if (!nyDatasettId) return;
    setFeil(null);
    try {
      const nytt = await api.leggTilVilkarInput(vilkarId, { datasettId: nyDatasettId });
      setInput((forrige) => [...(forrige ?? []), nytt]);
      setNyDatasettId('');
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av datasett.');
    }
  }

  async function fjern(datasettId: string) {
    setFeil(null);
    try {
      await api.fjernVilkarInput(vilkarId, datasettId);
      setInput((forrige) => (forrige ?? []).filter((d) => d.id !== datasettId));
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved fjerning av datasett.');
    }
  }

  if (input === null) return <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Laster …</Paragraph>;
  const ubrukte = alleDatasett.filter((d) => !input.some((i) => i.id === d.id));

  return (
    <div>
      {feil && <div className="feilmelding" style={{ marginBottom: '0.5rem' }}>{feil}</div>}
      {input.length === 0 ? (
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Ingen input-datasett koblet.
        </Paragraph>
      ) : (
        <ul style={{ margin: '0 0 0.5rem', padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '0.3rem' }}>
          {input.map((d) => (
            <li key={d.id} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: 'var(--ds-font-size-1)' }}>
              <Link asChild>
                <RouterLink to={`/datasett/${d.id}`} style={{ fontFamily: 'monospace' }}>{d.prop}</RouterLink>
              </Link>
              <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>({d.felt})</span>
              <Button variant="tertiary" data-color="danger" data-size="sm" type="button" onClick={() => fjern(d.id)}>Fjern</Button>
            </li>
          ))}
        </ul>
      )}
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end' }}>
        <Field>
          <Label>Datasett</Label>
          <Select data-size="sm" value={nyDatasettId} onChange={(e) => setNyDatasettId(e.target.value)}>
            <Select.Option value="">Velg …</Select.Option>
            {ubrukte.map((d) => <Select.Option key={d.id} value={d.id}>{d.felt}</Select.Option>)}
          </Select>
        </Field>
        <Button data-size="sm" type="button" variant="secondary" onClick={leggTil} disabled={!nyDatasettId}>
          Legg til
        </Button>
      </div>
    </div>
  );
}

/**
 * Veiledningskommentarer på en vilkårstre-node (2026-07-30, docs/12-fasit-handbok-leveranse.md
 * "Hovedfunn" + dimensjon A) — samme rolle som håndbok-kommentarer har på en rettskilde-node, men for
 * Vilkår/Regelnode/Unntak. `Dokumenttype` er selve proveniens-merkingen (dimensjon A): en «hjemmel»-
 * eller «praktisk-råd»-kommentar vises visuelt distinkt, ikke bare som en umerket fritekst.
 */
function VeiledningskommentarAdministrasjon({ malType, malId }: { malType: string; malId: string }) {
  const [kommentarer, setKommentarer] = useState<VilkarstreKommentarDto[] | null>(null);
  const [nyDokumenttype, setNyDokumenttype] = useState('kommentar');
  const [nyTekst, setNyTekst] = useState('');
  const [lagrer, setLagrer] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);

  function last() {
    api.hentVilkarstreKommentarer(malType, malId).then(setKommentarer).catch(() => setKommentarer([]));
  }

  useEffect(() => { setKommentarer(null); last(); }, [malType, malId]);

  async function leggTil() {
    if (!nyTekst.trim()) return;
    setFeil(null);
    setLagrer(true);
    try {
      await api.opprettVilkarstreKommentar({ malType, malId, dokumenttype: nyDokumenttype, tekstHtml: nyTekst });
      setNyTekst('');
      last();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av kommentar.');
    } finally {
      setLagrer(false);
    }
  }

  async function fjern(id: string) {
    await api.fjernVilkarstreKommentar(id);
    last();
  }

  if (kommentarer === null) return <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Laster …</Paragraph>;

  return (
    <div>
      {feil && <div className="feilmelding" style={{ marginBottom: '0.5rem' }}>{feil}</div>}
      {kommentarer.length === 0 ? (
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Ingen veiledningskommentarer lagt til.
        </Paragraph>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '1rem' }}>
          {kommentarer.map((k) => (
            <div key={k.id} style={{
              border: '1px solid var(--ds-color-neutral-border-subtle)', borderRadius: 'var(--ds-border-radius-md)', padding: '0.5rem',
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.3rem' }}>
                <Tag data-color={VEILEDNINGSDOKUMENTTYPE_FARGE[k.dokumenttype] ?? 'neutral'} data-size="sm">
                  {VEILEDNINGSDOKUMENTTYPER.find((d) => d.id === k.dokumenttype)?.label ?? k.dokumenttype}
                </Tag>
                <Button variant="tertiary" data-color="danger" data-size="sm" type="button" onClick={() => fjern(k.id)}>Fjern</Button>
              </div>
              <div style={{ fontSize: 'var(--ds-font-size-2)' }} dangerouslySetInnerHTML={{ __html: k.tekstHtml }} />
            </div>
          ))}
        </div>
      )}
      <Field>
        <Label>Dokumenttype</Label>
        <Select data-size="sm" value={nyDokumenttype} onChange={(e) => setNyDokumenttype(e.target.value)} style={{ maxWidth: '16rem', marginBottom: '0.5rem' }}>
          {VEILEDNINGSDOKUMENTTYPER.map((d) => <Select.Option key={d.id} value={d.id}>{d.label}</Select.Option>)}
        </Select>
      </Field>
      <MinimalEditor value={nyTekst} onChange={(html) => setNyTekst(html)} />
      <Button data-size="sm" type="button" onClick={leggTil} disabled={lagrer || !nyTekst.trim()} style={{ marginTop: '0.5rem' }}>
        {lagrer ? 'Lagrer …' : 'Legg til'}
      </Button>
    </div>
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
        <Tabs.Tab value="veiledning">Veiledning</Tabs.Tab>
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
  const [begrepId, setBegrepId] = useState('');
  const [skjonnsgrunnlagBegrepId, setSkjonnsgrunnlagBegrepId] = useState('');
  const [juridiskGrunnlag, setJuridiskGrunnlag] = useState<JuridiskGrunnlagInput[]>([]);
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
      setBegrepId(v.begrepId ?? '');
      setSkjonnsgrunnlagBegrepId(v.skjonnsgrunnlagBegrepId ?? '');
      setJuridiskGrunnlag(v.juridiskGrunnlag);
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
        vilkarstype, gjelderRolle: vilkar.gjelderRolle, juridiskGrunnlag,
        begrepId: begrepId || null, vurderingstype, parametreJson: vilkar.parametreJson,
        skjonnsgrunnlagBegrepId: skjonnsgrunnlagBegrepId || null, skjonnsmomenter: vilkar.skjonnsmomenter,
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
  const begrep = begreper.find((b) => b.id === begrepId);
  const skjonnsgrunnlag = begreper.find((b) => b.id === skjonnsgrunnlagBegrepId);

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
          <Field>
            <Label>Beskrivelse</Label>
            <Textarea value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={2} />
          </Field>
          <Field>
            <Label>Vilkårstype</Label>
            <Select value={vilkarstype} onChange={(e) => setVilkarstype(e.target.value)}>
              <Select.Option value="formell">Formell</Select.Option>
              <Select.Option value="materiell">Materiell</Select.Option>
            </Select>
          </Field>
          <Field>
            <Label>Vurderingstype</Label>
            <Select value={vurderingstype} onChange={(e) => setVurderingstype(e.target.value)}>
              <Select.Option value="regelbasert">Regelbasert</Select.Option>
              <Select.Option value="skjonnsbasert">Skjønnsbasert</Select.Option>
              <Select.Option value="hybrid">Hybrid</Select.Option>
            </Select>
          </Field>
          <Field>
            <Label>Begrep</Label>
            <Select value={begrepId} onChange={(e) => setBegrepId(e.target.value)}>
              <Select.Option value="">(ingen)</Select.Option>
              {begreper.map((b) => <Select.Option key={b.id} value={b.id}>{b.term}</Select.Option>)}
            </Select>
          </Field>
          {begrep && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginTop: '-0.5rem' }}>
              <Link asChild><RouterLink to={`/begreper/${begrep.id}`}>Åpne begrep →</RouterLink></Link>
            </Paragraph>
          )}
          {(vurderingstype === 'skjonnsbasert' || vurderingstype === 'hybrid') && (
            <>
              <Field>
                <Label>Skjønnsgrunnlag</Label>
                <Select value={skjonnsgrunnlagBegrepId} onChange={(e) => setSkjonnsgrunnlagBegrepId(e.target.value)}>
                  <Select.Option value="">(ikke satt)</Select.Option>
                  {begreper.map((b) => <Select.Option key={b.id} value={b.id}>{b.term}</Select.Option>)}
                </Select>
              </Field>
              {skjonnsgrunnlag && (
                <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginTop: '-0.5rem' }}>
                  <Link asChild><RouterLink to={`/begreper/${skjonnsgrunnlag.id}`}>Åpne begrep →</RouterLink></Link>
                </Paragraph>
              )}
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
          <Field>
            <Label>Veiledningstekst til bruker</Label>
            <Textarea value={veiledningBruker} onChange={(e) => setVeiledningBruker(e.target.value)} rows={3} />
          </Field>
          <Field>
            <Label>Veiledning til saksbehandler</Label>
            <Textarea value={veiledningSaksbehandler} onChange={(e) => setVeiledningSaksbehandler(e.target.value)} rows={3} />
          </Field>
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      )}

      {fane === 'metadata' && (
        <form onSubmit={lagre} style={{ maxWidth: '32rem' }}>
          <Paragraph>Versjon: {vilkar.versjon}</Paragraph>
          <Paragraph style={{ marginBottom: '0.25rem' }}>Juridisk grunnlag:</Paragraph>
          <JuridiskGrunnlagRedigering grunnlag={juridiskGrunnlag} rettskilder={rettskilder} onEndre={setJuridiskGrunnlag} />
          <div style={{ marginTop: '0.75rem' }}>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
          <Field style={{ maxWidth: '16rem', marginTop: '1rem' }}>
            <Label>Status</Label>
            <Select value={vilkar.status} onChange={(e) => endreStatus(e.target.value)}>
              {STATUSER.map((s) => <Select.Option key={s} value={s}>{s}</Select.Option>)}
            </Select>
          </Field>
          <Paragraph style={{ marginBottom: '0.25rem', marginTop: '1rem' }}>Input-datasett:</Paragraph>
          <InputDatasettAdministrasjon vilkarId={id} />
        </form>
      )}

      {fane === 'veiledning' && <VeiledningskommentarAdministrasjon malType="vilkar" malId={id} />}
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

  const [juridiskGrunnlag, setJuridiskGrunnlag] = useState<JuridiskGrunnlagInput[]>([]);

  useEffect(() => {
    setRegelnode(null);
    setHistorikk(null);
    api.hentRegelnode(id).then((r) => {
      setRegelnode(r);
      setTittel(r.tittel);
      setBeskrivelse(r.beskrivelse ?? '');
      setInnvilgelseTekst(r.innvilgelseTekst ?? '');
      setAvslagTekst(r.avslagTekst ?? '');
      setJuridiskGrunnlag(r.juridiskGrunnlag);
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
        erRotnode: regelnode.erRotnode, juridiskGrunnlag,
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
          <Field>
            <Label>Beskrivelse</Label>
            <Textarea value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={2} />
          </Field>
          <Field>
            <Label>Logisk operator (AK-3.4.2)</Label>
            <Select value={regelnode.barnOperator} onChange={(e) => endreOperator(e.target.value)}>
              <Select.Option value="OG">OG</Select.Option>
              <Select.Option value="ELLER">ELLER</Select.Option>
              <Select.Option value="IKKE">IKKE</Select.Option>
            </Select>
          </Field>
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
          <Field>
            <Label>Innvilgelsestekst</Label>
            <Textarea value={innvilgelseTekst} onChange={(e) => setInnvilgelseTekst(e.target.value)} rows={3} />
          </Field>
          <Field>
            <Label>Avslagstekst</Label>
            <Textarea value={avslagTekst} onChange={(e) => setAvslagTekst(e.target.value)} rows={3} />
          </Field>
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      )}

      {fane === 'metadata' && (
        <form onSubmit={lagre} style={{ maxWidth: '32rem' }}>
          <Paragraph>Versjon: {regelnode.versjon}</Paragraph>
          <Paragraph style={{ marginBottom: '0.25rem' }}>Juridisk grunnlag:</Paragraph>
          <JuridiskGrunnlagRedigering grunnlag={juridiskGrunnlag} rettskilder={rettskilder} onEndre={setJuridiskGrunnlag} />
          <div style={{ marginTop: '0.75rem' }}>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
          <Field style={{ maxWidth: '16rem', marginTop: '1rem' }}>
            <Label>Status</Label>
            <Select value={regelnode.status} onChange={(e) => endreStatus(e.target.value)}>
              {STATUSER.map((s) => <Select.Option key={s} value={s}>{s}</Select.Option>)}
            </Select>
          </Field>
        </form>
      )}

      {fane === 'veiledning' && <VeiledningskommentarAdministrasjon malType="regelnode" malId={id} />}
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
  const [juridiskGrunnlag, setJuridiskGrunnlag] = useState<JuridiskGrunnlagInput[]>([]);
  const [lagrer, setLagrer] = useState(false);

  useEffect(() => {
    setUnntak(null);
    setHistorikk(null);
    api.hentUnntak(id).then((u) => {
      setUnntak(u);
      setTittel(u.tittel);
      setBeskrivelse(u.beskrivelse ?? '');
      setJuridiskGrunnlag(u.juridiskGrunnlag);
    });
    api.hentUnntakHistorikk(id).then(setHistorikk);
  }, [id]);

  async function lagre(e: FormEvent) {
    e.preventDefault();
    if (!unntak) return;
    setFeil(null);
    setLagrer(true);
    try {
      const oppdatert = await api.oppdaterUnntak(id, { tittel: tittel.trim(), beskrivelse: beskrivelse.trim() || null, juridiskGrunnlag });
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
          <Field>
            <Label>Beskrivelse</Label>
            <Textarea value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={2} />
          </Field>
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
        <form onSubmit={lagre} style={{ maxWidth: '32rem' }}>
          <Paragraph>Versjon: {unntak.versjon}</Paragraph>
          <Paragraph style={{ marginBottom: '0.25rem' }}>Juridisk grunnlag:</Paragraph>
          <JuridiskGrunnlagRedigering grunnlag={juridiskGrunnlag} rettskilder={rettskilder} onEndre={setJuridiskGrunnlag} />
          <div style={{ marginTop: '0.75rem' }}>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
          <Field style={{ maxWidth: '16rem', marginTop: '1rem' }}>
            <Label>Status</Label>
            <Select value={unntak.status} onChange={(e) => endreStatus(e.target.value)}>
              {STATUSER.map((s) => <Select.Option key={s} value={s}>{s}</Select.Option>)}
            </Select>
          </Field>
        </form>
      )}

      {fane === 'veiledning' && <VeiledningskommentarAdministrasjon malType="unntak" malId={id} />}
      {fane === 'historikk' && <Historikk liste={historikk} />}
    </div>
  );
}
