import { useEffect, useState } from 'react';
import { Alert, Button, Card, Field, Label, Radio, Select, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { Aktortype, KildeUtenforKorpusDokumentasjon, KildeUtenforKorpusType, Kompetansegrunnlag, Kompetanseundertype, Normform, RelasjonsTypeKonfigurasjonDto, RettskildeSammendrag, StrukturkantDto, VirksomhetDto } from '../api/types';
import { FAMILIE_VISNING } from '../strukturkant/StrukturkantTabell';
import { MODALITETER, modalitetFraValg, ordningKanVaereMotpart, pliktValgtekst, typerForAktortype } from '../strukturkant/plikt';
import { KildeUtenforKorpusVelger } from '../strukturkant/KildeUtenforKorpus';
import { VirksomhetVelger } from './VirksomhetVelger';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';
import { Metatekst } from '../entitet/Metatekst';

/**
 * [Ny, issue #352, Johanns beslutning 1 2026-10-08] Undertypene per K-type — speilet av `Strukturkanter.Undertyper`, med
 * verbet fra leksikonet i teksten. Oppnevning er den generelle formen («hvem skal inneha en rolle, et verv eller en
 * funksjon»); verbet sier hvordan.
 */
const UNDERTYPER: Record<string, { verdi: Kompetanseundertype; tekst: string }[]> = {
  oppnevning: [
    { verdi: 'valg', tekst: 'Valg («velger»)' },
    { verdi: 'ansettelse', tekst: 'Ansettelse («ansetter»)' },
    { verdi: 'utpeking', tekst: 'Utpeking («utpeker»)' },
    { verdi: 'oppnevning', tekst: 'Oppnevning («oppnevner»)' },
  ],
  overproving: [{ verdi: 'anke', tekst: 'Anke («er ankeinstans for»)' }],
};

export interface LeggTilVirksomhetRelasjonFormProps {
  virksomhetId: string;
  virksomheter: VirksomhetDto[];
  rettskilder: RettskildeSammendrag[];
  onOpprettet: (ny: StrukturkantDto) => void;
  /** [Ny, issue #353] Aktørtypen til virksomheten skjemaet står på (fra-noden) — styrer hvilke typer som tilbys (en ordning
   * kan bare ha P og R forvaltes_av, se `typerForAktortype`). null = uavklart. */
  aktortype: Aktortype | null;
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
 * <p>
 * [ENDRET, issue #341 «kompetanse med motpart», 2026-10-08] Typelista har både R (struktur uten myndighet) og K
 * (kompetanse OVERFOR motparten — klage, instruksjon, oppnevning …), fordi myndighetsrelasjonene ble flyttet fra R til K
 * (Johanns beslutning P1). Velges en K-type, lagres en K-kant med motparten som til-node, og skjemaet tilbyr normform (bare
 * normgivning), grunnlag og «kan delegeres» — alle med «Ikke angitt» som utgangspunkt (CLAUDE.md §8: ingen forhåndsvalg).
 * </p>
 * <p>
 * [ENDRET, issue #352] Velges oppnevning eller overprøving, tilbyr skjemaet også undertypen (valg/ansettelse/utpeking/
 * oppnevning, anke) — også den med «Ikke angitt» som utgangspunkt. R «velger» og «er ankeinstans for» finnes ikke lenger
 * i lista; de er oppnevning (valg) og overprøving (anke).
 * </p>
 * <p>
 * [ENDRET, issue #353 «plikt overfor motpart», 2026-10-09] «Legg til relasjon, kompetanse eller plikt»: lista har også P-typene
 * («Plikt — «har samarbeidsplikt overfor motparten»», docs/09 §34). Velges en P-type, tilbyr skjemaet «Modalitet» (Ikke angitt /
 * Skal / Kan / Bør — aldri forhåndsvalgt, CLAUDE.md §8) og «Hva plikten gjelder» (objektet). Motparten er valgfri for P, som i
 * datamodellen («Folketrygden skal dekke behandlingsutgifter …» har ingen mottaker i teksten) — men da må objektet eller
 * hjemmel-eId-en si hva plikten gjelder (samme krav som serveren, StrukturkantTjeneste.OpprettAsync). For R og K er motparten
 * fortsatt påkrevd i dette skjemaet (K uten motpart registreres av konverteringen eller over API-et). Typene filtreres på
 * aktørtypen (en ordning er ikke en aktør, `typerForAktortype`), og en ordning tilbys som motpart bare for P.
 * </p>
 */
export function LeggTilVirksomhetRelasjonForm({ virksomhetId, virksomheter, rettskilder, onOpprettet, aktortype }: LeggTilVirksomhetRelasjonFormProps) {
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
  // [Ny, issue #341] Bare for K-typer.
  const [normform, setNormform] = useState<Normform | ''>('');
  const [grunnlag, setGrunnlag] = useState<Kompetansegrunnlag | ''>('');
  const [delegerbar, setDelegerbar] = useState<'' | 'ja' | 'nei'>('');
  // [Ny, issue #352] Bare for K-typer som har undertyper (UNDERTYPER).
  const [undertype, setUndertype] = useState<Kompetanseundertype | ''>('');
  // [Ny, issue #353] Bare for P-typer. '' = ikke angitt (null).
  const [modalitet, setModalitet] = useState('');
  const [objekt, setObjekt] = useState('');

  const [oppretter, setOppretter] = useState(false);
  const [feilmelding, setFeilmelding] = useState<string | null>(null);

  useEffect(() => {
    // [ENDRET, issue #341] R og K: «er klageinstans for» heter nå «har klagekompetanse overfor» (K).
    // [ENDRET, issue #353] + P (plikt overfor motpart).
    Promise.all([api.hentRelasjonstyper('R'), api.hentRelasjonstyper('K'), api.hentRelasjonstyper('P')])
      .then(([r, k, p]) => setTyper([...r, ...k, ...p]))
      .catch(() => setTyper([]));
  }, []);
  // [Ny, issue #353] Bare typene serveren godtar fra denne aktørtypen (en ordning: P og R forvaltes_av).
  const tilbudteTyper = typer && typerForAktortype(typer, aktortype);
  // Verdien i nedtrekkslista er «kategori:kode» — samme kode kan i prinsippet finnes i to kategorier.
  const [valgtKategori, valgtKode] = relasjonsType ? relasjonsType.split(':') as ['R' | 'K' | 'P', string] : [null, ''];
  const erKompetanse = valgtKategori === 'K';
  const erPlikt = valgtKategori === 'P';
  const undertyper = erKompetanse ? UNDERTYPER[valgtKode] : undefined;

  // Andre virksomheter enn denne selv — en relasjon til seg selv avvises uansett server-side, men
  // ingen grunn til å tilby det som et valg i det hele tatt.
  // [ENDRET, issue #353] En ordning er motpart bare i P (Strukturkanter.OrdningLovSomTil).
  const andreVirksomheter = virksomheter.filter((v) => v.id !== virksomhetId
    && (v.aktortype !== 'ordning' || ordningKanVaereMotpart(valgtKategori)));
  // Hjemmel ELLER kilde utenfor korpus (med type og dokumentasjon) — aldri begge (docs/33 §4.3).
  const harKilde = hjemmelRettskildeId
    ? !kildeTekst.trim()
    : !!kildeTekst.trim() && !!kildeType && !!kildeDok;
  // [Ny, issue #353] Motparten er valgfri bare for P — og da må objektet eller hjemmel-eId-en si hva plikten gjelder.
  const harMotpartEllerHva = tilVirksomhetId ? true : erPlikt && (!!objekt.trim() || !!hjemmelEid.trim());
  // En motpart som ikke lenger tilbys (en ordning, etter bytte fra en P-type til en R/K-type) skal ikke sendes skjult.
  const motpartGyldig = !tilVirksomhetId || andreVirksomheter.some((v) => v.id === tilVirksomhetId);

  async function opprett() {
    if (!harMotpartEllerHva || !motpartGyldig || !relasjonsType || !polaritet || !harKilde) return;
    setFeilmelding(null);
    setOppretter(true);
    try {
      const ny = await api.opprettStrukturkant({
        kategori: valgtKategori ?? 'R', typekode: valgtKode, fraVirksomhetId: virksomhetId, tilVirksomhetId: tilVirksomhetId || null, polaritet,
        normform: erKompetanse && valgtKode === 'normgivning' && normform ? normform : null,
        grunnlag: erKompetanse && grunnlag ? grunnlag : null,
        delegerbar: erKompetanse && delegerbar ? delegerbar === 'ja' : null,
        undertype: undertyper && undertyper.some((u) => u.verdi === undertype) ? (undertype as Kompetanseundertype) : null,
        // [Ny, issue #353] Bare på P — serveren avviser modalitet på andre kategorier.
        modalitet: erPlikt ? modalitetFraValg(modalitet) : null,
        objekt: erPlikt ? objekt.trim() || null : null,
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
      setNormform('');
      setGrunnlag('');
      setDelegerbar('');
      setUndertype('');
      setModalitet('');
      setObjekt('');
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
          label={erPlikt ? 'Motpart (annen virksomhet, valgfri for plikt)' : 'Motpart (annen virksomhet)'}
          tomValgTekst={erPlikt ? 'Ingen motpart i teksten' : 'Velg virksomhet …'} />
        {!motpartGyldig && (
          <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.25rem' }}>
            Den valgte motparten er en ordning, og en ordning kan bare være motpart i en plikt. Velg en annen motpart.
          </Metatekst>
        )}
      </div>

      <Field data-size="sm" style={{ maxWidth: '24rem', marginBottom: '0.75rem' }}>
        <Label>Relasjon, kompetanse eller plikt</Label>
        <Select data-size="sm" value={relasjonsType} onChange={(e) => setRelasjonsType(e.target.value)} disabled={!tilbudteTyper}>
          <Select.Option value="">{tilbudteTyper ? 'Velg type …' : 'Laster …'}</Select.Option>
          {tilbudteTyper?.map((t) => (
            <Select.Option key={`${t.kategori}:${t.kode}`} value={`${t.kategori}:${t.kode}`}>
              {t.kategori === 'K'
                ? `Kompetanse${t.familie ? ` (${FAMILIE_VISNING[t.familie].toLowerCase()})` : ''} — «${t.fraVisningsmal.replace('{0}', 'overfor motparten')}»`
                : t.kategori === 'P'
                  ? pliktValgtekst(t)
                  : `Relasjon — «${t.fraVisningsmal.replace('{0}', 'motparten')}»`}
            </Select.Option>
          ))}
        </Select>
      </Field>

      {/* [Ny, issue #353] Modaliteten står i utsagnsteksten i parentes («har samarbeidsplikt (skal) overfor …»), som normformen
        * på K — ingen egen tag (docs/09 §34). Objektet er det plikten gjelder når teksten ikke sier hvem den gjelder overfor. */}
      {erPlikt && (
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
          <Field data-size="sm" style={{ minWidth: '12rem' }}>
            <Label>Modalitet</Label>
            <Select data-size="sm" value={modalitet} onChange={(e) => setModalitet(e.target.value)}>
              <Select.Option value="">Ikke angitt</Select.Option>
              {MODALITETER.map((m) => (
                <Select.Option key={m.verdi} value={m.verdi}>{m.tekst}</Select.Option>
              ))}
            </Select>
          </Field>
          <Textfield data-size="sm" label="Hva plikten gjelder (valgfritt)" placeholder="f.eks. behandlings- og forpleiningsutgifter"
            value={objekt} onChange={(e) => setObjekt(e.target.value)} style={{ flex: 1, minWidth: '16rem' }} />
        </div>
      )}
      {erPlikt && !harMotpartEllerHva && (
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
          Uten motpart må plikten si hva den gjelder: fyll ut «Hva plikten gjelder» eller hjemmel-eId-en.
        </Metatekst>
      )}

      {erKompetanse && (
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
          {valgtKode === 'normgivning' && (
            <Field data-size="sm" style={{ minWidth: '12rem' }}>
              <Label>Normform</Label>
              <Select data-size="sm" value={normform} onChange={(e) => setNormform(e.target.value as Normform | '')}>
                <Select.Option value="">Ikke angitt</Select.Option>
                {(['forskrift', 'reglement', 'arbeidsordning', 'vedtekter', 'instruks'] as Normform[]).map((n) => (
                  <Select.Option key={n} value={n}>{n}</Select.Option>
                ))}
              </Select>
            </Field>
          )}
          {undertyper && (
            <Field data-size="sm" style={{ minWidth: '12rem' }}>
              <Label>Undertype</Label>
              <Select data-size="sm" value={undertype} onChange={(e) => setUndertype(e.target.value as Kompetanseundertype | '')}>
                <Select.Option value="">Ikke angitt</Select.Option>
                {undertyper.map((u) => (
                  <Select.Option key={u.verdi} value={u.verdi}>{u.tekst}</Select.Option>
                ))}
              </Select>
            </Field>
          )}
          <Field data-size="sm" style={{ minWidth: '12rem' }}>
            <Label>Grunnlag</Label>
            <Select data-size="sm" value={grunnlag} onChange={(e) => setGrunnlag(e.target.value as Kompetansegrunnlag | '')}>
              <Select.Option value="">Ikke angitt</Select.Option>
              <Select.Option value="offentligrettslig">Offentligrettslig</Select.Option>
              <Select.Option value="privatrettslig">Privatrettslig (eierskap/selskapsrett)</Select.Option>
            </Select>
          </Field>
          <Field data-size="sm" style={{ minWidth: '12rem' }}>
            <Label>Kan delegeres?</Label>
            <Select data-size="sm" value={delegerbar} onChange={(e) => setDelegerbar(e.target.value as '' | 'ja' | 'nei')}>
              <Select.Option value="">Ikke angitt</Select.Option>
              <Select.Option value="ja">Ja («Kongen …»)</Select.Option>
              <Select.Option value="nei">Nei («Kongen i statsråd …», «… selv»)</Select.Option>
            </Select>
          </Field>
        </div>
      )}

      <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
        <Label>Polaritet</Label>
        <Radio name="relasjon-polaritet" value="positiv" label="Positiv — relasjonen gjelder"
          checked={polaritet === 'positiv'} onChange={() => setPolaritet('positiv')} />
        <Radio name="relasjon-polaritet" value="negativ" label="Negativ — teksten sier at det IKKE gjelder (f.eks. «kan ikke instruere»)"
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
        disabled={oppretter || !harMotpartEllerHva || !motpartGyldig || !relasjonsType || !polaritet || !harKilde}>
        {oppretter ? 'Oppretter …' : erKompetanse ? 'Opprett kompetanse' : erPlikt ? 'Opprett plikt' : 'Opprett relasjon'}
      </Button>
      {feilmelding && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{feilmelding}</Alert>}
    </Card>
  );
}
