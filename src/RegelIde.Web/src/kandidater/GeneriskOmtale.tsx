import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import {
  Alert, Button, Checkbox, Divider, Field, Heading, Label, Link, Paragraph, Radio, Select, Spinner, Table, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type {
  GeneriskNavneformOversikt, KildeUtenforKorpusDokumentasjon, KildeUtenforKorpusType, Kunnskapsstatus, NavneformBindingDto,
  NavnekandidatDto, RettskildeNodeDto, Tolkningsargument, VirksomhetDto, VirksomhetsbegrepDto,
} from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KildeUtenforKorpusVelger } from '../strukturkant/KildeUtenforKorpus';
import { GodkjenningTag, KunnskapsstatusTag, KUNNSKAPSSTATUS_VISNING } from '../strukturkant/Utsagnsstatus';
import { GruppebegrepVelger } from '../virksomhet/GruppebegrepVelger';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';

/** Tolkningsargumentene (docs/profil/03 §6) — speilet av `Strukturkanter.Tolkningsargumenter`. */
const TOLKNINGSARGUMENTER: { verdi: Tolkningsargument; tekst: string }[] = [
  { verdi: 'ordlyd', tekst: 'Ordlyd' },
  { verdi: 'kontekst_system', tekst: 'Kontekst og system' },
  { verdi: 'formal', tekst: 'Formål' },
  { verdi: 'forarbeider', tekst: 'Forarbeider' },
  { verdi: 'praksis', tekst: 'Praksis' },
  { verdi: 'reelle_hensyn', tekst: 'Reelle hensyn' },
  { verdi: 'antitetisk_analogisk', tekst: 'Antitetisk eller analogisk' },
];

/** Statusene en binding kan få her. Avledet krever premisser (finnes ikke for bindinger), foreldet settes bare av en revisjon. */
const BINDING_KUNNSKAPSSTATUSER: Kunnskapsstatus[] = ['tekstfunn', 'fortolkning', 'eksternt_dokumentert', 'uavklart', 'omstridt'];

export interface GeneriskFerdig {
  term: string;
  varNyNavneform: boolean;
  bundet: boolean;
  taggOpprettet: boolean;
}

/**
 * [Ny, issue #378 A2/B5/AC7, Johanns godkjenning 2026-10-15] Veiviserens «Generisk omtale»-spor. Ingen ny side (#264): steg 2 i den
 * eksisterende navnekandidatveiviseren. Viser først hvor termen alt brukes og hva den betyr i hver rettskilde (AC7), og lar
 * saksbehandleren:
 * - **markere som generisk** — den generiske navneformen (én per term for hele korpuset) og taggen, ingen binding; eller
 * - **binde for denne rettskilden** — referent (virksomhet, klasse eller rolle), valgfritt paragrafspenn, hjemmel i rettskilden ELLER kilde
 *   utenfor korpus, og kunnskapsstatus (docs/profil/03 §5: `tekstfunn` når loven definerer ordet, ellers `fortolkning` med argumenter).
 *   En binding registrert her er en menneskelig avgjørelse og lagres som godkjent.
 *
 * Ingenting forhåndsvelges (CLAUDE.md §8, docs/09 §31): referent, grunnlag og kunnskapsstatus er tomme til saksbehandleren velger.
 */
export function GeneriskOmtale({
  kandidat, noder, virksomheter, onFerdig,
}: {
  kandidat: NavnekandidatDto;
  noder: RettskildeNodeDto[] | null;
  virksomheter: VirksomhetDto[];
  onFerdig: (f: GeneriskFerdig) => void;
}) {
  const term = kandidat.foreslattTekst.trim().toLowerCase();
  const [oversikt, setOversikt] = useState<GeneriskNavneformOversikt | null | undefined>(undefined);
  const [bind, setBind] = useState<boolean | null>(null);
  const [referentType, setReferentType] = useState<'virksomhet' | 'begrep' | null>(null);
  const [virksomhetId, setVirksomhetId] = useState('');
  const [begreper, setBegreper] = useState<VirksomhetsbegrepDto[] | null>(null);
  const [begrepId, setBegrepId] = useState('');
  const [fraEid, setFraEid] = useState('');
  const [tilEid, setTilEid] = useState('');
  const [grunnlag, setGrunnlag] = useState<'hjemmel' | 'kilde' | null>(null);
  const [hjemmelEid, setHjemmelEid] = useState('');
  const [kildeTekst, setKildeTekst] = useState('');
  const [kildeLenke, setKildeLenke] = useState('');
  const [kildeType, setKildeType] = useState<KildeUtenforKorpusType | ''>('');
  const [kildeDok, setKildeDok] = useState<KildeUtenforKorpusDokumentasjon | ''>('');
  const [kunnskapsstatus, setKunnskapsstatus] = useState<Kunnskapsstatus | ''>('');
  const [begrunnelse, setBegrunnelse] = useState('');
  const [argumenter, setArgumenter] = useState<Tolkningsargument[]>([]);
  const [kjorer, setKjorer] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);

  function lastOversikt() {
    api.hentGeneriskeNavneformer(term).then((r) => setOversikt(r[0] ?? null)).catch(() => setOversikt(null));
  }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(lastOversikt, [term]);
  useEffect(() => {
    if (referentType !== 'begrep' || begreper !== null) return;
    api.hentGruppebegrep().then((b) => setBegreper(b.filter((x) => x.begrepskategori === 'klasse' || x.begrepskategori === 'rolle')))
      .catch(() => setBegreper([]));
  }, [referentType, begreper]);

  // Spennet velges på kapittel- eller paragrafnivå (et kapittel dekker alt under det); hjemmelen kan også være et ledd. Ledd vises med
  // paragrafen foran («§ 10 ledd 1»), ellers er «1» og «2» uleselige i en lang liste.
  const perId = new Map((noder ?? []).map((n) => [n.id, n]));
  const nodeTekst = (n: RettskildeNodeDto): string => {
    const tittel = n.overskrift ? ` — ${n.overskrift}` : '';
    if (n.nodeType === 'side') return 'Hele siden';
    if (n.nodeType === 'kapittel') return `Kapittel ${n.nummer ?? n.eid}${tittel}`;
    if (n.nodeType === 'paragraf') return `${n.nummer ?? n.eid}${tittel}`; // nummer er alt «§ 10»
    const forelder = n.parentNodeId ? perId.get(n.parentNodeId) : undefined;
    return `${forelder ? `${nodeTekst(forelder)} ` : ''}${n.nodeType} ${n.nummer ?? ''}`.trim();
  };
  const paragrafer = (noder ?? []).filter((n) => !n.opphevet && (n.nodeType === 'side' || n.nodeType === 'kapittel' || n.nodeType === 'paragraf'));
  const hjemmelnoder = (noder ?? []).filter((n) => !n.opphevet && (n.nodeType === 'side' || n.nodeType === 'paragraf' || n.nodeType === 'ledd'));
  const herBindinger = oversikt?.rettskilder.find((r) => r.rettskildeId === kandidat.rettskildeId)?.bindinger ?? [];

  const referentKlar = (referentType === 'virksomhet' && virksomhetId !== '') || (referentType === 'begrep' && begrepId !== '');
  const grunnlagKlart = (grunnlag === 'hjemmel' && hjemmelEid !== '')
    || (grunnlag === 'kilde' && kildeTekst.trim() !== '' && !!kildeType && !!kildeDok);
  const statusKlar = kunnskapsstatus === ''
    || (kunnskapsstatus === 'tekstfunn' && grunnlag === 'hjemmel')
    || (kunnskapsstatus === 'eksternt_dokumentert' && grunnlag === 'kilde')
    || (kunnskapsstatus === 'fortolkning' && begrunnelse.trim() !== '' && argumenter.length > 0)
    || kunnskapsstatus === 'uavklart' || kunnskapsstatus === 'omstridt';
  const klar = bind === false || (bind === true && referentKlar && grunnlagKlart && statusKlar);

  async function fullfor() {
    setKjorer(true);
    setFeil(null);
    try {
      const r = await api.markerNavnekandidatSomGenerisk(kandidat.id, bind ? {
        tilVirksomhetId: referentType === 'virksomhet' ? virksomhetId : null,
        tilBegrepId: referentType === 'begrep' ? begrepId : null,
        paragrafspenn: fraEid ? [{ fraEid, tilEid: tilEid || null }] : [],
        hjemmelRettskildeId: grunnlag === 'hjemmel' ? kandidat.rettskildeId : null,
        hjemmelEid: grunnlag === 'hjemmel' ? hjemmelEid : null,
        kildeUtenforKorpusTekst: grunnlag === 'kilde' ? kildeTekst.trim() : null,
        kildeUtenforKorpusLenke: grunnlag === 'kilde' && kildeLenke.trim() ? kildeLenke.trim() : null,
        kildeUtenforKorpusType: grunnlag === 'kilde' && kildeType ? kildeType : null,
        kildeUtenforKorpusDokumentasjon: grunnlag === 'kilde' && kildeDok ? kildeDok : null,
        kunnskapsstatus: kunnskapsstatus || null,
        begrunnelse: begrunnelse.trim() || null,
        tolkningsargumenter: argumenter.length ? argumenter : null,
        status: 'godkjent',
        kommentar: null,
      } : null);
      onFerdig({ term: r.navneform.term, varNyNavneform: r.varNyNavneform, bundet: r.binding !== null, taggOpprettet: r.taggId !== null });
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke markere som generisk.');
    } finally {
      setKjorer(false);
    }
  }

  async function behandleBinding(b: NavneformBindingDto, handling: 'godkjenn' | 'avvis') {
    setFeil(null);
    try {
      if (handling === 'godkjenn') await api.godkjennNavneformBinding(b.id);
      else await api.avvisNavneformBinding(b.id);
      lastOversikt();
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Handlingen feilet.');
    }
  }

  return (
    <div style={{ marginBottom: '0.75rem' }}>
      <Divider style={{ margin: '0.75rem 0' }} />
      <Heading level={3} data-size="xs" style={{ marginBottom: '0.35rem' }}>«{term}» i korpuset</Heading>
      {oversikt === undefined ? <Spinner aria-label="Laster …" data-size="sm" /> : oversikt === null ? (
        <Metatekst>«{term}» er ikke registrert som generisk omtale ennå — den blir det når du fullfører.</Metatekst>
      ) : (
        <>
          <Metatekst>
            Generisk omtale, tagget {oversikt.antallTagger} ganger i {oversikt.rettskilder.length} rettskilder. Hva den betyr, står per
            rettskilde — der det ikke er en godkjent binding, er omtalen ukoblet.
          </Metatekst>
          <Table data-size="sm" data-density="compact" border style={{ marginBottom: '0.5rem' }}>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Rettskilde</Table.HeaderCell>
                <Table.HeaderCell>Tagger</Table.HeaderCell>
                <Table.HeaderCell>Betyr</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {oversikt.rettskilder.map((r) => (
                <Table.Row key={r.rettskildeId}>
                  <Table.Cell>
                    <Link asChild><RouterLink to={`/rettskilder/${r.rettskildeId}`}>{r.rettskildeTittel}</RouterLink></Link>
                  </Table.Cell>
                  <Table.Cell>{r.antallTagger}</Table.Cell>
                  <Table.Cell>
                    {r.bindinger.length === 0 ? <Metatekst as="span">Ikke bundet</Metatekst> : r.bindinger.map((b) => (
                      <div key={b.id} style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                        <Link asChild>
                          <RouterLink to={b.referent.type === 'virksomhet' ? `/virksomheter/${b.referent.id}` : `/begreper/${b.referent.id}`}>
                            {b.referent.navn}
                          </RouterLink>
                        </Link>
                        {b.paragrafspenn.length > 0 && <Metatekst as="span">({b.paragrafspenn.length} spenn)</Metatekst>}
                        <KunnskapsstatusTag status={b.kunnskapsstatus} />
                        <GodkjenningTag status={b.status} oppdagelsesKilde={b.oppdagelsesKilde} />
                        {r.rettskildeId === kandidat.rettskildeId && b.status !== 'godkjent' && (
                          <>
                            <Button data-size="sm" variant="secondary" onClick={() => behandleBinding(b, 'godkjenn')}>Godkjenn</Button>
                            <Button data-size="sm" variant="tertiary" onClick={() => behandleBinding(b, 'avvis')}>Avvis</Button>
                          </>
                        )}
                      </div>
                    ))}
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        </>
      )}

      <Field data-size="sm" style={{ marginBottom: '0.5rem' }}>
        <Radio name="generiskBind" label="Bare marker som generisk" value="nei"
          description="Omtalen kobles til den generiske navneformen, uten betydning for denne rettskilden ennå. Den står da som «generisk» i aktørsveipet."
          checked={bind === false} onChange={() => setBind(false)} />
        <Radio name="generiskBind" label="Marker og bind for denne rettskilden"
          description={herBindinger.length > 0 ? 'Det finnes alt en binding her — en ny må ha et annet paragrafspenn.' : 'Hva «' + term + '» betyr her.'}
          value="ja" checked={bind === true} onChange={() => setBind(true)} />
      </Field>

      {bind && (
        <div style={{ display: 'grid', gap: '0.6rem', marginBottom: '0.6rem' }}>
          <Field data-size="sm">
            <Radio name="referentType" label="En virksomhet" value="virksomhet" checked={referentType === 'virksomhet'}
              onChange={() => setReferentType('virksomhet')} />
            <Radio name="referentType" label="En klasse eller rolle («kommunen» som barnehagemyndighet)" value="begrep"
              checked={referentType === 'begrep'} onChange={() => setReferentType('begrep')} />
          </Field>
          {referentType === 'virksomhet' && (
            <VirksomhetVelger virksomheter={virksomheter} value={virksomhetId} onChange={setVirksomhetId} label="Betyr virksomheten"
              tomValgTekst="Velg virksomhet …" style={{ maxWidth: '28rem' }} />
          )}
          {referentType === 'begrep' && (begreper === null ? <Spinner aria-label="Laster …" data-size="sm" /> : begreper.length === 0 ? (
            <Alert data-color="warning" data-size="sm">Det finnes ingen klasse- eller rollebegrep. Opprett begrepet først.</Alert>
          ) : (
            <GruppebegrepVelger gruppebegrep={begreper} value={begrepId} onChange={setBegrepId} label="Betyr klassen/rollen"
              tomValgTekst="Velg klasse eller rolle …" style={{ maxWidth: '28rem' }} />
          ))}

          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <Field data-size="sm" style={{ maxWidth: '16rem' }}>
              <Label>Gjelder fra (valgfritt — tomt = hele rettskilden)</Label>
              <Select data-size="sm" value={fraEid} onChange={(e) => setFraEid(e.target.value)}>
                <Select.Option value="">Hele rettskilden</Select.Option>
                {paragrafer.map((n) => <Select.Option key={n.id} value={n.eid}>{nodeTekst(n)}</Select.Option>)}
              </Select>
            </Field>
            {fraEid && (
              <Field data-size="sm" style={{ maxWidth: '16rem' }}>
                <Label>Til og med (valgfritt)</Label>
                <Select data-size="sm" value={tilEid} onChange={(e) => setTilEid(e.target.value)}>
                  <Select.Option value="">Bare denne</Select.Option>
                  {paragrafer.map((n) => <Select.Option key={n.id} value={n.eid}>{nodeTekst(n)}</Select.Option>)}
                </Select>
              </Field>
            )}
          </div>

          <Field data-size="sm">
            <Label>Hvor betydningen kommer fra</Label>
            <Radio name="grunnlag" label="Bestemmelse i denne rettskilden (loven definerer eller forutsetter ordet)" value="hjemmel"
              checked={grunnlag === 'hjemmel'} onChange={() => setGrunnlag('hjemmel')} />
            <Radio name="grunnlag" label="Kilde utenfor korpus (f.eks. kgl.res. om ansvarsfordeling)" value="kilde"
              checked={grunnlag === 'kilde'} onChange={() => setGrunnlag('kilde')} />
          </Field>
          {grunnlag === 'hjemmel' && (
            <Field data-size="sm" style={{ maxWidth: '28rem' }}>
              <Label>Bestemmelsen</Label>
              <Select data-size="sm" value={hjemmelEid} onChange={(e) => setHjemmelEid(e.target.value)}>
                <Select.Option value="">Velg …</Select.Option>
                {hjemmelnoder.map((n) => <Select.Option key={n.id} value={n.eid}>{nodeTekst(n)}</Select.Option>)}
              </Select>
            </Field>
          )}
          {grunnlag === 'kilde' && (
            <>
              <Textfield data-size="sm" label="Kilden" value={kildeTekst} onChange={(e) => setKildeTekst(e.target.value)} style={{ maxWidth: '36rem' }} />
              <Textfield data-size="sm" label="Lenke (valgfri)" value={kildeLenke} onChange={(e) => setKildeLenke(e.target.value)} style={{ maxWidth: '36rem' }} />
              <KildeUtenforKorpusVelger type={kildeType} dokumentasjon={kildeDok} onType={setKildeType} onDokumentasjon={setKildeDok} />
            </>
          )}

          <Field data-size="sm" style={{ maxWidth: '22rem' }}>
            <Label>Kunnskapsstatus</Label>
            <Select data-size="sm" value={kunnskapsstatus} onChange={(e) => setKunnskapsstatus(e.target.value as Kunnskapsstatus | '')}>
              <Select.Option value="">Ikke vurdert</Select.Option>
              {BINDING_KUNNSKAPSSTATUSER.map((k) => <Select.Option key={k} value={k}>{KUNNSKAPSSTATUS_VISNING[k].tekst}</Select.Option>)}
            </Select>
          </Field>
          {kunnskapsstatus && <Metatekst>{KUNNSKAPSSTATUS_VISNING[kunnskapsstatus].forklaring}</Metatekst>}
          {kunnskapsstatus === 'tekstfunn' && grunnlag !== 'hjemmel' && (
            <Alert data-color="warning" data-size="sm">Tekstfunn krever en bestemmelse i rettskilden.</Alert>
          )}
          {kunnskapsstatus === 'eksternt_dokumentert' && grunnlag !== 'kilde' && (
            <Alert data-color="warning" data-size="sm">Eksternt dokumentert krever kilde utenfor korpus.</Alert>
          )}
          {(kunnskapsstatus === 'fortolkning' || kunnskapsstatus === 'omstridt' || kunnskapsstatus === 'uavklart') && (
            <Textfield data-size="sm" label={kunnskapsstatus === 'fortolkning' ? 'Begrunnelse' : 'Begrunnelse (valgfri)'} value={begrunnelse}
              onChange={(e) => setBegrunnelse(e.target.value)} style={{ maxWidth: '36rem' }} />
          )}
          {kunnskapsstatus === 'fortolkning' && (
            <Field data-size="sm">
              <Label>Tolkningsargumenter (minst ett)</Label>
              <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                {TOLKNINGSARGUMENTER.map((a) => (
                  <Checkbox key={a.verdi} data-size="sm" label={a.tekst} checked={argumenter.includes(a.verdi)}
                    onChange={(e) => setArgumenter((x) => (e.target.checked ? [...x, a.verdi] : x.filter((y) => y !== a.verdi)))} />
                ))}
              </div>
            </Field>
          )}
        </div>
      )}

      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.5rem' }}>{feil}</Alert>}
      <Paragraph data-size="sm" style={{ margin: 0 }}>
        <Button data-size="sm" onClick={fullfor} disabled={!klar || kjorer}>
          {kjorer ? 'Lagrer …' : bind ? 'Marker som generisk og bind' : 'Marker som generisk'}
        </Button>
      </Paragraph>
    </div>
  );
}
