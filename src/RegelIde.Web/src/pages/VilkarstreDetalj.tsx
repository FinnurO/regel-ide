import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams, useSearchParams } from 'react-router';
import { Button, Field, Heading, Label, Link, Paragraph, Select, Textfield, ToggleGroup } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { BegrepDto, RegelnodeBarnDto, RegelnodeDto, RettskildeSammendrag, UnntakDto, VilkarDto } from '../api/types';
import { byggVilkarstre, flatNodeliste, type VilkarstreNode } from '../vilkarstre/bygging';
import { VilkarstreGraf } from '../vilkarstre/VilkarstreGraf';
import { VilkarstreTre } from '../vilkarstre/VilkarstreTre';
import { Egenskapspanel, type EgenskapspanelNode } from '../vilkarstre/Egenskapspanel';

/**
 * Samme reachability-sjekk som VilkarstreGrafHjelper.KanNaAsync på backend (INV-7), kjørt client-side
 * mot allerede innlastede data — brukes til å filtrere bort barn-kandidater som ville skapt en sykel,
 * i stedet for å la brukeren velge en ugyldig kombinasjon og først få vite det etter innsending.
 */
function kanNaKlient(
  fraType: 'vilkar' | 'regelnode', fraId: string, tilType: 'vilkar' | 'regelnode', tilId: string,
  barnPerRegelnode: Map<string, RegelnodeBarnDto[]>, unntakListe: UnntakDto[],
): boolean {
  if (fraType === 'vilkar') return false; // Vilkår er alltid blad (INV-1) — har ingen utgående kanter
  const unntakPerGjelderRegel = new Map<string, UnntakDto[]>();
  for (const u of unntakListe) {
    const liste = unntakPerGjelderRegel.get(u.gjelderRegelId) ?? [];
    liste.push(u);
    unntakPerGjelderRegel.set(u.gjelderRegelId, liste);
  }
  const besokt = new Set<string>([`regelnode:${fraId}`]);
  const koe: string[] = [`regelnode:${fraId}`];
  while (koe.length > 0) {
    const nokkel = koe.shift()!;
    const skilletIndeks = nokkel.indexOf(':');
    const type = nokkel.slice(0, skilletIndeks) as 'vilkar' | 'regelnode';
    const id = nokkel.slice(skilletIndeks + 1);
    if (type === tilType && id === tilId) return true;
    if (type !== 'regelnode') continue;
    for (const b of barnPerRegelnode.get(id) ?? []) {
      const barnNokkel = `${b.barnType}:${b.barnId}`;
      if (!besokt.has(barnNokkel)) { besokt.add(barnNokkel); koe.push(barnNokkel); }
    }
    for (const u of unntakPerGjelderRegel.get(id) ?? []) {
      const betNokkel = `${u.betingelseType}:${u.betingelseId}`;
      if (!besokt.has(betNokkel)) { besokt.add(betNokkel); koe.push(betNokkel); }
    }
  }
  return false;
}

export default function VilkarstreDetalj() {
  const { rotnodeId } = useParams<{ rotnodeId: string }>();
  const [searchParams] = useSearchParams();
  const [regelnoder, setRegelnoder] = useState<RegelnodeDto[] | null>(null);
  const [vilkarListe, setVilkarListe] = useState<VilkarDto[] | null>(null);
  const [unntakListe, setUnntakListe] = useState<UnntakDto[] | null>(null);
  const [begreper, setBegreper] = useState<BegrepDto[]>([]);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [barnPerRegelnode, setBarnPerRegelnode] = useState<Map<string, RegelnodeBarnDto[]>>(new Map());
  const [feil, setFeil] = useState<string | null>(null);
  const [visning, setVisning] = useState<'graf' | 'tre'>('graf');
  const [valgt, setValgt] = useState<EgenskapspanelNode | null>(null);

  const [visLeggTil, setVisLeggTil] = useState(false);
  const [nyType, setNyType] = useState<'vilkar' | 'regelnode'>('vilkar');
  const [nyTittel, setNyTittel] = useState('');
  const [leggerTil, setLeggerTil] = useState(false);

  const [visKoble, setVisKoble] = useState(false);
  const [kobleForelder, setKobleForelder] = useState('');
  const [kobleBarnType, setKobleBarnType] = useState<'vilkar' | 'regelnode'>('vilkar');
  const [kobleBarnId, setKobleBarnId] = useState('');
  const [kobler, setKobler] = useState(false);
  const [kobleFeil, setKobleFeil] = useState<string | null>(null);

  const [visNyttUnntak, setVisNyttUnntak] = useState(false);
  const [nyUnntakTittel, setNyUnntakTittel] = useState('');
  const [nyUnntakGjelderRegel, setNyUnntakGjelderRegel] = useState('');
  const [nyUnntakBetingelseType, setNyUnntakBetingelseType] = useState<'vilkar' | 'regelnode'>('vilkar');
  const [nyUnntakBetingelseId, setNyUnntakBetingelseId] = useState('');
  const [oppretterUnntak, setOppretterUnntak] = useState(false);
  const [unntakFeil, setUnntakFeil] = useState<string | null>(null);

  const lastAlt = useCallback(async () => {
    if (!rotnodeId) return;
    try {
      const [rn, vk, un, bg, rk] = await Promise.all([
        api.hentRegelnodeListe(), api.hentVilkarListe(), api.hentUnntakListe(), api.hentBegreper(), api.hentRettskilder(),
      ]);
      const barnPar = await Promise.all(rn.map(async (r) => [r.id, await api.hentRegelnodeBarn(r.id)] as const));
      setRegelnoder(rn);
      setVilkarListe(vk);
      setUnntakListe(un);
      setBegreper(bg);
      setRettskilder(rk);
      setBarnPerRegelnode(new Map(barnPar));
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av vilkårstreet.');
    }
  }, [rotnodeId]);

  useEffect(() => { lastAlt(); }, [lastAlt]);

  const tre = useMemo<VilkarstreNode | null>(() => {
    if (!rotnodeId || !regelnoder || !vilkarListe || !unntakListe) return null;
    return byggVilkarstre(rotnodeId, regelnoder, vilkarListe, unntakListe, barnPerRegelnode);
  }, [rotnodeId, regelnoder, vilkarListe, unntakListe, barnPerRegelnode]);

  const alleNoder = useMemo(() => (tre ? flatNodeliste(tre) : []), [tre]);

  useEffect(() => {
    if (valgt || alleNoder.length === 0) return;
    const fokusId = searchParams.get('fokusVilkar');
    if (!fokusId) return;
    const funnet = alleNoder.find((n) => n.id === fokusId);
    if (funnet) setValgt({ kind: funnet.kind, id: funnet.id });
  }, [alleNoder, searchParams, valgt]);

  function onSelect(node: VilkarstreNode) {
    setValgt({ kind: node.kind, id: node.id });
  }

  async function leggTil(e: FormEvent) {
    e.preventDefault();
    setFeil(null);
    setLeggerTil(true);
    try {
      if (nyType === 'vilkar') {
        await api.opprettVilkar({
          tittel: nyTittel.trim(), beskrivelse: null, generiskMal: null, vilkarstype: 'formell', gjelderRolle: null,
          juridiskGrunnlag: null, begrepId: null, vurderingstype: 'regelbasert', parametreJson: null,
          skjonnsgrunnlagBegrepId: null, skjonnsmomenter: null, kreverDokumentasjon: false, eskaleringsrolle: null,
          veiledningTilBruker: null, veiledningTilSaksbehandler: null, erFormel: false, formelBeskrivelse: null,
        });
      } else {
        await api.opprettRegelnode({
          tittel: nyTittel.trim(), beskrivelse: null, generiskMal: null, barnOperator: 'OG', utdataNavn: 'Utfall',
          utdataType: 'boolean', erRotnode: false, juridiskGrunnlag: null, innvilgelseTekst: null, avslagTekst: null,
        });
      }
      setNyTittel('');
      setVisLeggTil(false);
      await lastAlt();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse.');
    } finally {
      setLeggerTil(false);
    }
  }

  async function kobleBarn(e: FormEvent) {
    e.preventDefault();
    setKobleFeil(null);
    setKobler(true);
    try {
      await api.kobleRegelnodeBarn(kobleForelder, { barnType: kobleBarnType, barnId: kobleBarnId });
      setVisKoble(false);
      setKobleBarnId('');
      await lastAlt();
    } catch (err) {
      setKobleFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling.');
    } finally {
      setKobler(false);
    }
  }

  async function opprettUnntak(e: FormEvent) {
    e.preventDefault();
    setUnntakFeil(null);
    setOppretterUnntak(true);
    try {
      await api.opprettUnntak({
        tittel: nyUnntakTittel.trim(), beskrivelse: null, gjelderRegelId: nyUnntakGjelderRegel,
        betingelseType: nyUnntakBetingelseType, betingelseId: nyUnntakBetingelseId, juridiskGrunnlag: null,
      });
      setNyUnntakTittel('');
      setNyUnntakBetingelseId('');
      setVisNyttUnntak(false);
      await lastAlt();
    } catch (err) {
      setUnntakFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av unntak.');
    } finally {
      setOppretterUnntak(false);
    }
  }

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!tre || !vilkarListe || !regelnoder) return <Paragraph>Laster …</Paragraph>;

  const ubrukteVilkar = vilkarListe.filter((v) => !alleNoder.some((n) => n.kind === 'vilkar' && n.id === v.id));
  const ubrukteRegelnoder = regelnoder.filter((r) => !alleNoder.some((n) => n.kind === 'regelnode' && n.id === r.id));

  return (
    <>
      <Link asChild>
        <RouterLink to="/vilkarstre">← Tilbake til listen</RouterLink>
      </Link>
      <Heading level={1} data-size="lg" style={{ marginTop: '0.5rem', marginBottom: '1rem' }}>
        {tre.tittel}
      </Heading>

      <div style={{ display: 'flex', gap: '1.5rem', alignItems: 'flex-start' }}>
        <div style={{ flex: 2, minWidth: 0 }}>
          <ToggleGroup value={visning} onChange={(v) => setVisning(v as 'graf' | 'tre')} data-size="sm" style={{ marginBottom: '0.75rem' }}>
            <ToggleGroup.Item value="graf">Graf</ToggleGroup.Item>
            <ToggleGroup.Item value="tre">Tre</ToggleGroup.Item>
          </ToggleGroup>

          <div style={{ border: '1px solid var(--ds-color-neutral-border-subtle)', borderRadius: 'var(--ds-border-radius-lg)', padding: '0.5rem', overflow: 'auto' }}>
            {visning === 'graf'
              ? <VilkarstreGraf root={tre} valgtId={valgt?.id} onSelect={onSelect} />
              : <VilkarstreTre root={tre} valgtId={valgt?.id} onSelect={onSelect} />}
          </div>

          <div style={{ marginTop: '1rem', display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <Button data-size="sm" variant="secondary" onClick={() => setVisLeggTil((v) => !v)}>
              {visLeggTil ? 'Avbryt' : 'Nytt vilkår/regelnode'}
            </Button>
            <Button data-size="sm" variant="secondary" onClick={() => setVisKoble((v) => !v)}>
              {visKoble ? 'Avbryt' : 'Koble barn til regelnode'}
            </Button>
            <Button data-size="sm" variant="secondary" onClick={() => setVisNyttUnntak((v) => !v)}>
              {visNyttUnntak ? 'Avbryt' : 'Nytt unntak'}
            </Button>
          </div>

          {visLeggTil && (
            <form onSubmit={leggTil} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginTop: '0.75rem' }}>
              <Field>
                <Label>Type</Label>
                <Select data-size="sm" value={nyType} onChange={(e) => setNyType(e.target.value as 'vilkar' | 'regelnode')}>
                  <Select.Option value="vilkar">Vilkår</Select.Option>
                  <Select.Option value="regelnode">Regelnode</Select.Option>
                </Select>
              </Field>
              <Textfield data-size="sm" label="Tittel" value={nyTittel} onChange={(e) => setNyTittel(e.target.value)} required />
              <Button data-size="sm" type="submit" disabled={leggerTil || !nyTittel.trim()}>
                {leggerTil ? 'Oppretter …' : 'Opprett'}
              </Button>
            </form>
          )}

          {(ubrukteVilkar.length > 0 || ubrukteRegelnoder.length > 0) && (
            <div style={{ marginTop: '0.75rem' }}>
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.25rem' }}>
                Løse noder — opprettet, men ikke koblet inn i treet ennå (bruk «Koble barn til regelnode» for å plassere dem):
              </Paragraph>
              <ul style={{ margin: 0, fontSize: 'var(--ds-font-size-1)' }}>
                {ubrukteVilkar.map((v) => <li key={v.id}>{v.tittel} (Vilkår)</li>)}
                {ubrukteRegelnoder.map((r) => <li key={r.id}>{r.tittel} (Regelnode)</li>)}
              </ul>
            </div>
          )}

          {visKoble && (
            <form onSubmit={kobleBarn} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginTop: '0.75rem', flexWrap: 'wrap' }}>
              <Field>
                <Label>Forelder (regelnode)</Label>
                <Select data-size="sm" value={kobleForelder} onChange={(e) => setKobleForelder(e.target.value)}>
                  <Select.Option value="">Velg …</Select.Option>
                  {regelnoder.map((r) => <Select.Option key={r.id} value={r.id}>{r.tittel}</Select.Option>)}
                </Select>
              </Field>
              <Field>
                <Label>Barn-type</Label>
                <Select data-size="sm" value={kobleBarnType} onChange={(e) => { setKobleBarnType(e.target.value as 'vilkar' | 'regelnode'); setKobleBarnId(''); }}>
                  <Select.Option value="vilkar">Vilkår</Select.Option>
                  <Select.Option value="regelnode">Regelnode</Select.Option>
                </Select>
              </Field>
              <Field>
                <Label>Barn</Label>
                <Select data-size="sm" value={kobleBarnId} onChange={(e) => setKobleBarnId(e.target.value)}>
                  <Select.Option value="">Velg …</Select.Option>
                  {(kobleBarnType === 'vilkar' ? vilkarListe : regelnoder)
                    .filter((n) => !kobleForelder || kobleBarnType !== 'regelnode' || !kanNaKlient('regelnode', n.id, 'regelnode', kobleForelder, barnPerRegelnode, unntakListe ?? []))
                    .map((n) => (
                      <Select.Option key={n.id} value={n.id}>{n.tittel}</Select.Option>
                    ))}
                </Select>
              </Field>
              <Button data-size="sm" type="submit" disabled={kobler || !kobleForelder || !kobleBarnId}>
                {kobler ? 'Kobler …' : 'Koble'}
              </Button>
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', width: '100%', margin: 0 }}>
                «Barn»-listen viser kun kandidater som ikke ville skapt en sykel (INV-7) med valgt forelder.
              </Paragraph>
              {kobleFeil && <div className="feilmelding" style={{ width: '100%' }}>{kobleFeil}</div>}
            </form>
          )}

          {visNyttUnntak && (
            <form onSubmit={opprettUnntak} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginTop: '0.75rem', flexWrap: 'wrap' }}>
              <Textfield data-size="sm" label="Tittel" value={nyUnntakTittel} onChange={(e) => setNyUnntakTittel(e.target.value)} required />
              <Field>
                <Label>Gjelder regel</Label>
                <Select data-size="sm" value={nyUnntakGjelderRegel} onChange={(e) => { setNyUnntakGjelderRegel(e.target.value); setNyUnntakBetingelseId(''); }}>
                  <Select.Option value="">Velg …</Select.Option>
                  {regelnoder.map((r) => <Select.Option key={r.id} value={r.id}>{r.tittel}</Select.Option>)}
                </Select>
              </Field>
              <Field>
                <Label>Betingelse-type</Label>
                <Select data-size="sm" value={nyUnntakBetingelseType} onChange={(e) => { setNyUnntakBetingelseType(e.target.value as 'vilkar' | 'regelnode'); setNyUnntakBetingelseId(''); }}>
                  <Select.Option value="vilkar">Vilkår</Select.Option>
                  <Select.Option value="regelnode">Regelnode</Select.Option>
                </Select>
              </Field>
              <Field>
                <Label>Betingelse</Label>
                <Select data-size="sm" value={nyUnntakBetingelseId} onChange={(e) => setNyUnntakBetingelseId(e.target.value)}>
                  <Select.Option value="">Velg …</Select.Option>
                  {(nyUnntakBetingelseType === 'vilkar' ? vilkarListe : regelnoder)
                    .filter((n) => !nyUnntakGjelderRegel || nyUnntakBetingelseType !== 'regelnode' || !kanNaKlient('regelnode', n.id, 'regelnode', nyUnntakGjelderRegel, barnPerRegelnode, unntakListe ?? []))
                    .map((n) => (
                      <Select.Option key={n.id} value={n.id}>{n.tittel}</Select.Option>
                    ))}
                </Select>
              </Field>
              <Button data-size="sm" type="submit" disabled={oppretterUnntak || !nyUnntakTittel.trim() || !nyUnntakGjelderRegel || !nyUnntakBetingelseId}>
                {oppretterUnntak ? 'Oppretter …' : 'Opprett unntak'}
              </Button>
              {unntakFeil && <div className="feilmelding" style={{ width: '100%' }}>{unntakFeil}</div>}
            </form>
          )}
        </div>

        <div style={{ flex: 3, minWidth: 0, borderLeft: '1px solid var(--ds-color-neutral-border-subtle)', paddingLeft: '1.5rem' }}>
          {valgt ? (
            <Egenskapspanel node={valgt} begreper={begreper} rettskilder={rettskilder} onEndret={lastAlt} />
          ) : (
            <Paragraph>Velg en node i grafen/treet for å se og redigere egenskapene.</Paragraph>
          )}
        </div>
      </div>
    </>
  );
}
