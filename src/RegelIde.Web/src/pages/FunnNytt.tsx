import { useEffect, useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Alert, Button, Card, Field, Heading, Label, Link, Select, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { FunnKategori, FunnKoblingInn, FunnVurderingspunktInn, RettskildeSammendrag, VirksomhetDto } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { ALLE_KATEGORIER, KATEGORI_TEKST, UNDERTYPER, UNDERTYPE_TEKST } from '../funn/FunnVisning';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';

interface BestemmelseUtkast { rolle: 'gjelder_bestemmelse' | 'motstridende_bestemmelse'; rettskildeId: string; eid: string; etikett: string; merknad: string }
interface AktorUtkast { virksomhetId: string; etikett: string }
interface KildeUtkast { etikett: string; lenke: string }
interface BortforklaringUtkast { overskrift: string; tekst: string }

/**
 * [Ny, issue #429 «Funn til vurdering», 2026-10-30] Manuell registrering av et funn (status oppdaget). Ingenting er forhåndsvalgt
 * (CLAUDE.md §8): kategorien må velges. Bestemmelser lagres med ELI og eId og slås opp i korpuset på serveren; finnes de ikke,
 * står de som «ikke i korpuset». Aktører pekes på med etiketten slik teksten skriver navnet (CLAUDE.md §0).
 * Agenter registrerer gjennom det samme endepunktet (`POST /api/funn`, med `oppdagelsesKilde` og `eksternNokkel`).
 */
export default function FunnNytt() {
  const navigate = useNavigate();
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [virksomheter, setVirksomheter] = useState<VirksomhetDto[]>([]);
  const [kategori, setKategori] = useState<FunnKategori | ''>('');
  const [undertype, setUndertype] = useState('');
  const [tittel, setTittel] = useState('');
  const [beskrivelse, setBeskrivelse] = useState('');
  const [funnetI, setFunnetI] = useState('');
  const [internMerknad, setInternMerknad] = useState('');
  const [bestemmelser, setBestemmelser] = useState<BestemmelseUtkast[]>([]);
  const [aktorer, setAktorer] = useState<AktorUtkast[]>([]);
  const [kilder, setKilder] = useState<KildeUtkast[]>([]);
  const [bortforklaringer, setBortforklaringer] = useState<BortforklaringUtkast[]>([]);
  const [feil, setFeil] = useState<string | null>(null);
  const [lagrer, setLagrer] = useState(false);

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => {});
    api.hentVirksomheter().then(setVirksomheter).catch(() => {});
  }, []);

  function oppdater<T>(liste: T[], sett: (l: T[]) => void, i: number, endring: Partial<T>) {
    sett(liste.map((x, j) => (j === i ? { ...x, ...endring } : x)));
  }

  async function lagre() {
    setFeil(null);
    const koblinger: FunnKoblingInn[] = [];
    for (const b of bestemmelser) {
      const rk = rettskilder.find((r) => r.id === b.rettskildeId);
      if (!rk?.eli) { setFeil(`Rettskilden for «${b.etikett || 'bestemmelsen'}» har ingen ELI og kan ikke kobles som bestemmelse.`); return; }
      const eid = b.eid.trim() ? (b.eid.trim().startsWith('http') ? b.eid.trim() : `${rk.eli}/${b.eid.trim().replace(/^\//, '')}`) : null;
      koblinger.push({ rolle: b.rolle, etikett: b.etikett, merknad: b.merknad || null, rettskildeEli: rk.eli, eid });
    }
    for (const a of aktorer) koblinger.push({ rolle: 'berort_aktor', etikett: a.etikett, virksomhetId: a.virksomhetId });
    for (const k of kilder) koblinger.push({ rolle: 'kilde', etikett: k.etikett, eksternLenke: k.lenke });
    const vurderingspunkter: FunnVurderingspunktInn[] = bortforklaringer
      .filter((b) => b.tekst.trim())
      .map((b) => ({ type: 'bortforklaring_provd', tekst: b.tekst.trim(), overskrift: b.overskrift.trim() || null }));
    setLagrer(true);
    try {
      const f = await api.opprettFunn({
        kategori: kategori as FunnKategori, undertype: undertype || null, tittel, beskrivelse, funnetI: funnetI || null,
        internMerknad: internMerknad || null, koblinger, vurderingspunkter,
      });
      navigate(`/funn/${f.id}`);
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  return (
    <>
      <Metatekst as="nav" aria-label="Brødsmulesti" style={{ display: 'flex', gap: '0.4rem', marginBottom: '0.6rem' }}>
        <Link asChild><RouterLink to="/funn">Funn til vurdering</RouterLink></Link>
        <span>/</span>
        <span style={{ color: 'var(--ds-color-neutral-text-default)' }}>Registrer funn</span>
      </Metatekst>
      <Heading level={1} data-size="lg" style={{ marginBottom: '1rem' }}>Registrer funn</Heading>

      <div style={{ display: 'grid', gap: '1rem', maxWidth: '48rem' }}>
        <Card style={{ padding: '1rem', display: 'grid', gap: '0.75rem' }} data-size="sm">
          <Field>
            <Label>Kategori</Label>
            <Select data-size="sm" value={kategori} onChange={(e) => { setKategori(e.target.value as FunnKategori | ''); setUndertype(''); }}>
              <Select.Option value="">Velg …</Select.Option>
              {ALLE_KATEGORIER.map((k) => <Select.Option key={k} value={k}>{KATEGORI_TEKST[k]}</Select.Option>)}
            </Select>
          </Field>
          {kategori && UNDERTYPER[kategori].length > 0 && (
            <Field>
              <Label>Undertype (valgfri)</Label>
              <Select data-size="sm" value={undertype} onChange={(e) => setUndertype(e.target.value)}>
                <Select.Option value="">Ikke angitt</Select.Option>
                {UNDERTYPER[kategori].map((u) => <Select.Option key={u} value={u}>{UNDERTYPE_TEKST[u]}</Select.Option>)}
              </Select>
            </Field>
          )}
          <Textfield data-size="sm" label="Tittel" value={tittel} onChange={(e) => setTittel(e.target.value)} />
          <Field>
            <Label>Påstand</Label>
            <Metatekst style={{ margin: 0 }}>Hva modellen observerte. Linjer som begynner med «- » blir punkter. Teksten publiseres hvis funnet godkjennes.</Metatekst>
            <Textarea data-size="sm" rows={4} value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} />
          </Field>
          <Textfield data-size="sm" label="Funnet i (valgfri, for eksempel #375)" value={funnetI} onChange={(e) => setFunnetI(e.target.value)} />
          <Field>
            <Label>Intern merknad (valgfri, publiseres ikke)</Label>
            <Textarea data-size="sm" rows={2} value={internMerknad} onChange={(e) => setInternMerknad(e.target.value)} />
          </Field>
        </Card>

        <Card style={{ padding: '1rem', display: 'grid', gap: '0.75rem' }} data-size="sm">
          <Heading level={2} data-size="xs">Bestemmelser</Heading>
          {bestemmelser.map((b, i) => (
            <div key={i} style={{ display: 'grid', gap: '0.5rem', borderTop: i ? '1px solid var(--ds-color-neutral-border-subtle)' : undefined, paddingTop: i ? '0.75rem' : 0 }}>
              <Field>
                <Label>Rolle</Label>
                <Select data-size="sm" value={b.rolle} onChange={(e) => oppdater(bestemmelser, setBestemmelser, i, { rolle: e.target.value as BestemmelseUtkast['rolle'] })}>
                  <Select.Option value="gjelder_bestemmelse">Gjelder</Select.Option>
                  <Select.Option value="motstridende_bestemmelse">Mulig motstrid</Select.Option>
                </Select>
              </Field>
              <RettskildeVelger rettskilder={rettskilder} value={b.rettskildeId} onChange={(v) => oppdater(bestemmelser, setBestemmelser, i, { rettskildeId: v })} />
              <Textfield data-size="sm" label="eId under rettskilden (valgfri, for eksempel §11/ledd-3)" value={b.eid}
                onChange={(e) => oppdater(bestemmelser, setBestemmelser, i, { eid: e.target.value })} />
              <Textfield data-size="sm" label="Etikett (for eksempel FOR-2008-09-12-1071 § 11 tredje ledd)" value={b.etikett}
                onChange={(e) => oppdater(bestemmelser, setBestemmelser, i, { etikett: e.target.value })} />
              <Textfield data-size="sm" label="Hva den sier (valgfri)" value={b.merknad}
                onChange={(e) => oppdater(bestemmelser, setBestemmelser, i, { merknad: e.target.value })} />
              <div><Button data-size="sm" variant="tertiary" onClick={() => setBestemmelser(bestemmelser.filter((_, j) => j !== i))}>Fjern</Button></div>
            </div>
          ))}
          <div><Button data-size="sm" variant="secondary" onClick={() => setBestemmelser([...bestemmelser, { rolle: 'gjelder_bestemmelse', rettskildeId: '', eid: '', etikett: '', merknad: '' }])}>
            Legg til bestemmelse
          </Button></div>
        </Card>

        <Card style={{ padding: '1rem', display: 'grid', gap: '0.75rem' }} data-size="sm">
          <Heading level={2} data-size="xs">Berørte aktører</Heading>
          {aktorer.map((a, i) => (
            <div key={i} style={{ display: 'grid', gap: '0.5rem' }}>
              <VirksomhetVelger virksomheter={virksomheter} value={a.virksomhetId} label="Aktør" tomValgTekst="Velg aktør …"
                onChange={(v) => oppdater(aktorer, setAktorer, i, { virksomhetId: v })} />
              <Textfield data-size="sm" label="Navnet slik teksten skriver det" value={a.etikett}
                onChange={(e) => oppdater(aktorer, setAktorer, i, { etikett: e.target.value })} />
              <div><Button data-size="sm" variant="tertiary" onClick={() => setAktorer(aktorer.filter((_, j) => j !== i))}>Fjern</Button></div>
            </div>
          ))}
          <div><Button data-size="sm" variant="secondary" onClick={() => setAktorer([...aktorer, { virksomhetId: '', etikett: '' }])}>Legg til aktør</Button></div>
        </Card>

        <Card style={{ padding: '1rem', display: 'grid', gap: '0.75rem' }} data-size="sm">
          <Heading level={2} data-size="xs">Bortforklaringer som er prøvd</Heading>
          <Metatekst style={{ margin: 0 }}>Kan også legges til ved juristvurderingen. Et funn kan ikke juristvurderes uten minst én.</Metatekst>
          {bortforklaringer.map((b, i) => (
            <div key={i} style={{ display: 'grid', gap: '0.5rem' }}>
              <Textfield data-size="sm" label="Overskrift (for eksempel Lex posterior)" value={b.overskrift}
                onChange={(e) => oppdater(bortforklaringer, setBortforklaringer, i, { overskrift: e.target.value })} />
              <Field>
                <Label>Hva som ble prøvd, og resultatet</Label>
                <Textarea data-size="sm" rows={2} value={b.tekst} onChange={(e) => oppdater(bortforklaringer, setBortforklaringer, i, { tekst: e.target.value })} />
              </Field>
              <div><Button data-size="sm" variant="tertiary" onClick={() => setBortforklaringer(bortforklaringer.filter((_, j) => j !== i))}>Fjern</Button></div>
            </div>
          ))}
          <div><Button data-size="sm" variant="secondary" onClick={() => setBortforklaringer([...bortforklaringer, { overskrift: '', tekst: '' }])}>Legg til bortforklaring</Button></div>
        </Card>

        <Card style={{ padding: '1rem', display: 'grid', gap: '0.75rem' }} data-size="sm">
          <Heading level={2} data-size="xs">Kilder</Heading>
          {kilder.map((k, i) => (
            <div key={i} style={{ display: 'grid', gap: '0.5rem' }}>
              <Textfield data-size="sm" label="Etikett (for eksempel Prop. 79 L (2025–2026))" value={k.etikett}
                onChange={(e) => oppdater(kilder, setKilder, i, { etikett: e.target.value })} />
              <Textfield data-size="sm" label="Lenke" value={k.lenke} onChange={(e) => oppdater(kilder, setKilder, i, { lenke: e.target.value })} />
              <div><Button data-size="sm" variant="tertiary" onClick={() => setKilder(kilder.filter((_, j) => j !== i))}>Fjern</Button></div>
            </div>
          ))}
          <div><Button data-size="sm" variant="secondary" onClick={() => setKilder([...kilder, { etikett: '', lenke: '' }])}>Legg til kilde</Button></div>
        </Card>

        {feil && <Alert data-color="danger" data-size="sm">{feil}</Alert>}
        <div>
          <Button data-size="sm" disabled={lagrer || !kategori || !tittel.trim() || !beskrivelse.trim()} onClick={lagre}>
            {lagrer ? 'Lagrer …' : 'Registrer funn'}
          </Button>
        </div>
      </div>
    </>
  );
}
