import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams, useSearchParams } from 'react-router-dom';
import { Button, Heading, Link, Paragraph, Select, Textfield, ToggleGroup } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { BegrepDto, RegelnodeBarnDto, RegelnodeDto, RettskildeSammendrag, UnntakDto, VilkarDto } from '../api/types';
import { byggVilkarstre, flatNodeliste, type VilkarstreNode } from '../vilkarstre/bygging';
import { VilkarstreGraf } from '../vilkarstre/VilkarstreGraf';
import { VilkarstreTre } from '../vilkarstre/VilkarstreTre';
import { Egenskapspanel, type EgenskapspanelNode } from '../vilkarstre/Egenskapspanel';

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

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!tre || !vilkarListe || !regelnoder) return <Paragraph>Laster …</Paragraph>;

  const ubrukteNoder = nyType === 'vilkar'
    ? vilkarListe.filter((v) => !alleNoder.some((n) => n.kind === 'vilkar' && n.id === v.id))
    : regelnoder.filter((r) => !alleNoder.some((n) => n.kind === 'regelnode' && n.id === r.id));

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
          </div>

          {visLeggTil && (
            <form onSubmit={leggTil} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginTop: '0.75rem' }}>
              <Select data-size="sm" label="Type" value={nyType} onChange={(e) => setNyType(e.target.value as 'vilkar' | 'regelnode')}>
                <Select.Option value="vilkar">Vilkår</Select.Option>
                <Select.Option value="regelnode">Regelnode</Select.Option>
              </Select>
              <Textfield data-size="sm" label="Tittel" value={nyTittel} onChange={(e) => setNyTittel(e.target.value)} required />
              <Button data-size="sm" type="submit" disabled={leggerTil || !nyTittel.trim()}>
                {leggerTil ? 'Oppretter …' : 'Opprett'}
              </Button>
            </form>
          )}

          {visKoble && (
            <form onSubmit={kobleBarn} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginTop: '0.75rem', flexWrap: 'wrap' }}>
              <Select data-size="sm" label="Forelder (regelnode)" value={kobleForelder} onChange={(e) => setKobleForelder(e.target.value)}>
                <Select.Option value="">Velg …</Select.Option>
                {regelnoder.map((r) => <Select.Option key={r.id} value={r.id}>{r.tittel}</Select.Option>)}
              </Select>
              <Select data-size="sm" label="Barn-type" value={kobleBarnType} onChange={(e) => { setKobleBarnType(e.target.value as 'vilkar' | 'regelnode'); setKobleBarnId(''); }}>
                <Select.Option value="vilkar">Vilkår</Select.Option>
                <Select.Option value="regelnode">Regelnode</Select.Option>
              </Select>
              <Select data-size="sm" label="Barn" value={kobleBarnId} onChange={(e) => setKobleBarnId(e.target.value)}>
                <Select.Option value="">Velg …</Select.Option>
                {(kobleBarnType === 'vilkar' ? vilkarListe : regelnoder).map((n) => (
                  <Select.Option key={n.id} value={n.id}>{n.tittel}</Select.Option>
                ))}
              </Select>
              <Button data-size="sm" type="submit" disabled={kobler || !kobleForelder || !kobleBarnId}>
                {kobler ? 'Kobler …' : 'Koble'}
              </Button>
              {kobleFeil && <div className="feilmelding" style={{ width: '100%' }}>{kobleFeil}</div>}
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
