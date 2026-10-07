import { useEffect, useState } from 'react';
import { Alert, Button, Card, Field, Label, Radio, Select, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { KildeUtenforKorpusDokumentasjon, KildeUtenforKorpusType, RelasjonsTypeKonfigurasjonDto, RettskildeSammendrag, StrukturkantDto, VirksomhetDto } from '../api/types';
import { KildeUtenforKorpusVelger } from '../strukturkant/KildeUtenforKorpus';
import { VirksomhetVelger } from './VirksomhetVelger';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';
import { Metatekst } from '../entitet/Metatekst';

export interface LeggTilVirksomhetRelasjonFormProps {
  virksomhetId: string;
  virksomheter: VirksomhetDto[];
  rettskilder: RettskildeSammendrag[];
  onOpprettet: (ny: StrukturkantDto) => void;
}

/**
 * «Legg til relasjon»-skjema (docs/28, docs/29 §Del C) — kobler DENNE virksomheten (alltid fra-siden) til en
 * annen konkret virksomhet, med en typekode hentet fra `GET /api/konfigurasjon/relasjonstyper?kategori=R`.
 * Samme fil-per-skjema-konvensjon som `LeggTilMyndighetstildelingForm.tsx`, samme `VirksomhetVelger`/
 * `RettskildeVelger`-gjenbruk som resten av UI-et.
 * <p>
 * [ENDRET, issue #311 «Strukturmodell 6»] Lagrer en R-strukturkant (`POST /api/strukturkanter`), og har fått
 * de felles egenskapene fra docs/33 §4.3 som `VirksomhetRelasjon` manglet (#134):
 * </p>
 * <ul>
 *   <li><b>Polaritet</b> — må VELGES (ingen forhåndsvalgt verdi, samme «ingen standardverdi»-regel som
 *       KI-konverteringen, docs/33 §5.2). «Kan ikke instruere» er et utsagn, ikke fraværet av et.</li>
 *   <li><b>Avgrensning</b> — sakstypen relasjonen gjelder («bare klagesaker»). Var før skrevet inn i
 *       kommentaren fordi tabellen ikke hadde et felt for det.</li>
 *   <li><b>Kilde utenfor korpus</b> — påkrevd når det ikke er valgt hjemmel (org-kart, vedtekter, kgl.res.).
 *       Det var dette den gamle «Kommentar»-feltet ble brukt til uten hjemmel.</li>
 * </ul>
 */
export function LeggTilVirksomhetRelasjonForm({ virksomhetId, virksomheter, rettskilder, onOpprettet }: LeggTilVirksomhetRelasjonFormProps) {
  const [typer, setTyper] = useState<RelasjonsTypeKonfigurasjonDto[] | null>(null);
  const [tilVirksomhetId, setTilVirksomhetId] = useState('');
  const [relasjonsType, setRelasjonsType] = useState('');
  const [polaritet, setPolaritet] = useState<'positiv' | 'negativ' | null>(null);
  const [hjemmelRettskildeId, setHjemmelRettskildeId] = useState('');
  const [hjemmelEid, setHjemmelEid] = useState('');
  const [kildeTekst, setKildeTekst] = useState('');
  const [kildeLenke, setKildeLenke] = useState('');
  // [Ny, Johanns beslutning 2026-10-07] Type og dokumentasjon er påkrevd sammen med kilden — ingen forhåndsvalg.
  const [kildeType, setKildeType] = useState<KildeUtenforKorpusType | ''>('');
  const [kildeDok, setKildeDok] = useState<KildeUtenforKorpusDokumentasjon | ''>('');
  const [avgrensning, setAvgrensning] = useState('');

  const [oppretter, setOppretter] = useState(false);
  const [feilmelding, setFeilmelding] = useState<string | null>(null);

  useEffect(() => {
    api.hentRelasjonstyper('R').then(setTyper).catch(() => setTyper([]));
  }, []);

  // Andre virksomheter enn denne selv — en relasjon til seg selv avvises uansett server-side, men
  // ingen grunn til å tilby det som et valg i det hele tatt.
  const andreVirksomheter = virksomheter.filter((v) => v.id !== virksomhetId);
  // Hjemmel ELLER kilde utenfor korpus (med type og dokumentasjon) — aldri begge (docs/33 §4.3).
  const harKilde = hjemmelRettskildeId
    ? !kildeTekst.trim()
    : !!kildeTekst.trim() && !!kildeType && !!kildeDok;

  async function opprett() {
    if (!tilVirksomhetId || !relasjonsType || !polaritet || !harKilde) return;
    setFeilmelding(null);
    setOppretter(true);
    try {
      const ny = await api.opprettStrukturkant({
        kategori: 'R', typekode: relasjonsType, fraVirksomhetId: virksomhetId, tilVirksomhetId, polaritet,
        hjemmelRettskildeId: hjemmelRettskildeId || null, hjemmelEid: hjemmelEid.trim() || null,
        kildeUtenforKorpusTekst: kildeTekst.trim() || null, kildeUtenforKorpusLenke: kildeLenke.trim() || null,
        kildeUtenforKorpusType: kildeType || null, kildeUtenforKorpusDokumentasjon: kildeDok || null,
        avgrensningTekst: avgrensning.trim() || null,
      });
      onOpprettet(ny);
      setTilVirksomhetId('');
      setRelasjonsType('');
      setPolaritet(null);
      setHjemmelRettskildeId('');
      setHjemmelEid('');
      setKildeTekst('');
      setKildeLenke('');
      setKildeType('');
      setKildeDok('');
      setAvgrensning('');
    } catch (err) {
      setFeilmelding(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av relasjon.');
    } finally {
      setOppretter(false);
    }
  }

  return (
    <Card style={{ padding: '1rem', marginTop: '0.75rem' }}>
      <div style={{ marginBottom: '0.75rem' }}>
        <VirksomhetVelger virksomheter={andreVirksomheter} value={tilVirksomhetId} onChange={setTilVirksomhetId}
          label="Motpart (annen virksomhet)" tomValgTekst="Velg virksomhet …" />
      </div>

      <Field data-size="sm" style={{ maxWidth: '24rem', marginBottom: '0.75rem' }}>
        <Label>Relasjonstype</Label>
        <Select data-size="sm" value={relasjonsType} onChange={(e) => setRelasjonsType(e.target.value)} disabled={!typer}>
          <Select.Option value="">{typer ? 'Velg relasjonstype …' : 'Laster …'}</Select.Option>
          {typer?.map((t) => (
            <Select.Option key={t.kode} value={t.kode}>
              {t.kode} — «{t.fraVisningsmal.replace('{0}', 'motparten')}»
            </Select.Option>
          ))}
        </Select>
      </Field>

      <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
        <Label>Polaritet</Label>
        <Radio name="relasjon-polaritet" value="positiv" label="Positiv — relasjonen gjelder"
          checked={polaritet === 'positiv'} onChange={() => setPolaritet('positiv')} />
        <Radio name="relasjon-polaritet" value="negativ" label="Negativ — teksten sier at relasjonen IKKE gjelder (f.eks. «kan ikke instruere»)"
          checked={polaritet === 'negativ'} onChange={() => setPolaritet('negativ')} />
      </Field>

      <div style={{ marginBottom: '0.75rem' }}>
        <RettskildeVelger rettskilder={rettskilder} value={hjemmelRettskildeId} onChange={setHjemmelRettskildeId}
          label="Hjemmel (rettskilde)" />
      </div>

      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
        <Textfield data-size="sm" label="Hjemmel-eId (valgfritt, avansert)" value={hjemmelEid}
          onChange={(e) => setHjemmelEid(e.target.value)} style={{ flex: 1, minWidth: '14rem', fontFamily: 'monospace' }} />
        <Textfield data-size="sm" label="Avgrensning (valgfritt)" placeholder="f.eks. bare klagesaker"
          value={avgrensning} onChange={(e) => setAvgrensning(e.target.value)} style={{ flex: 1, minWidth: '14rem' }} />
      </div>

      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.25rem' }}>
        <Textfield data-size="sm" label="Kilde utenfor korpus (bare uten hjemmel)"
          placeholder="f.eks. organisasjonskartet, vedtekter, kgl.res."
          value={kildeTekst} onChange={(e) => setKildeTekst(e.target.value)} style={{ flex: 2, minWidth: '16rem' }} />
        <Textfield data-size="sm" label="Lenke til kilden (valgfritt)" value={kildeLenke}
          onChange={(e) => setKildeLenke(e.target.value)} style={{ flex: 1, minWidth: '14rem' }} />
      </div>
      {!hjemmelRettskildeId && (
        <div style={{ marginBottom: '0.25rem' }}>
          <KildeUtenforKorpusVelger type={kildeType} dokumentasjon={kildeDok} onType={setKildeType} onDokumentasjon={setKildeDok} />
        </div>
      )}
      <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
        En relasjon må ha en hjemmel ELLER en kilde utenfor korpus med type og dokumentasjon (docs/33 §4.3) — et
        forhold som bare er bekreftet mot et organisasjonskart skal ikke kunne forveksles med et som står i en
        bestemmelse. Med hjemmel skal kildefeltene stå tomme.
      </Metatekst>

      <Button data-size="sm" type="button" onClick={opprett}
        disabled={oppretter || !tilVirksomhetId || !relasjonsType || !polaritet || !harKilde}>
        {oppretter ? 'Oppretter …' : 'Opprett relasjon'}
      </Button>
      {feilmelding && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{feilmelding}</Alert>}
    </Card>
  );
}
