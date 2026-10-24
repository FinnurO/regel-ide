import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import {
  Alert, Button, Checkbox, Details, Divider, Field, Fieldset, Heading, Label, Link, Radio, Select, Spinner, Tag, Textarea, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type {
  GeneriskKandidatkontekst, GeneriskReferentforslag, Godkjenning, Kunnskapsstatus, NavneformBindingRequest, NavnekandidatDto,
  RettskildeNodeDto, StrukturnodeDto, Tolkningsargument, VirksomhetDto, VirksomhetsbegrepDto,
} from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KUNNSKAPSSTATUS_VISNING, KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { NavneformgrunnTag } from '../virksomhet/Navneformgrunn';
import { GeneriskKildeTagger, generiskKildeTekst } from './GeneriskKilde';
import { periodeTekst } from './GeneriskOmtale';
import { nodeEtikett } from './Utsagnssteg';

/** Kvitteringen veiviseren viser etter lagring («Dette skjedde», docs/09 §15). */
export interface GeneriskKvittering {
  tittel: string;
  detaljer: string[];
  rettskildeLenke: string | null;
  referentLenke: string | null;
  referentErBegrep: boolean;
}

type Valg =
  | { type: 'regel' }
  | { type: 'marker' }
  | { type: 'referent'; referent: StrukturnodeDto; forslag: GeneriskReferentforslag | null };

const ARGUMENTER: { verdi: Tolkningsargument; tekst: string }[] = [
  { verdi: 'ordlyd', tekst: 'Ordlyden' }, { verdi: 'kontekst_system', tekst: 'Sammenhengen i loven' }, { verdi: 'formal', tekst: 'Formålet' },
  { verdi: 'forarbeider', tekst: 'Forarbeidene' }, { verdi: 'praksis', tekst: 'Praksis' }, { verdi: 'reelle_hensyn', tekst: 'Reelle hensyn' },
  { verdi: 'antitetisk_analogisk', tekst: 'Antitetisk eller analogisk' },
];

const FORSLAG_ETIKETT: Record<GeneriskReferentforslag['kilde'], string> = {
  standardregel: 'Standardregel',
  ansvarlig_departement: 'Lovens departement',
  samme_rettskilde: 'Nevnt i samme rettskilde',
  navneform: 'Navneform',
  tidligere_binding: 'Bundet før',
};

function lenkeFor(r: StrukturnodeDto): string {
  return r.type === 'virksomhet' ? `/virksomheter/${r.id}` : `/begreper/${r.id}`;
}

/** Ordet uthevet i nodeteksten, til forhåndsvisningen. */
function MedOrdet({ tekst, term, etikett }: { tekst: string; term: string; etikett: string }) {
  const m = new RegExp(`(?<![\\p{L}\\p{N}])${term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?![\\p{L}\\p{N}])`, 'iu').exec(tekst);
  if (!m) return <>{tekst}</>;
  return (
    <>
      {tekst.slice(0, m.index)}
      <mark title={etikett} style={{
        background: 'var(--ds-color-brand1-surface-tinted)', borderBottom: '2px solid var(--ds-color-brand1-border-default)',
        borderRadius: 'var(--ds-border-radius-sm)', padding: '0 2px',
      }}>{m[0]}</mark>
      {tekst.slice(m.index + m[0].length)}
    </>
  );
}

/**
 * [Ny, issue #394 AK 4, Johann 2026-10-23: «tungvint», «for mange steg», «resultatet vises ikke»] Veiviserens ÉN skjerm for en generisk
 * omtale. Erstatter stegene «Hva betyr den her?», «Hva bygger dette på?», «Hvor sikkert er det?» og «Bekreft» for dette sporet.
 * <ul>
 * <li><b>Forslag fra konteksten</b>, mest sannsynlige øverst (`GET /api/navnekandidater/{id}/generisk-kontekst`): standardregelen (når den
 * løser trengs ingen binding), aktører nevnt i samme rettskilde, navneformer som inneholder ordet, tidligere bindinger og lovens
 * departement. Hvert forslag sier hvorfor det er foreslått.</li>
 * <li><b>Søk</b> på tvers av aktører, klasser og roller, med treff mens man skriver.</li>
 * <li><b>Grunnlag og sikkerhet er ferdig utfylt</b> etter hvordan referenten ble funnet (hjemmel = der ordet står; kunnskapsstatus
 * fortolkning med sammenhengen i loven; begrunnelsen sier hvor), og kan endres under «Detaljer». Godkjenningen står som «Faglig vurdert»
 * — en binding blir aldri godkjent uten at noen velger det (docs/09 §42 om hvorfor dette avviker fra «ingenting forhåndsvelges»).</li>
 * <li><b>Slik blir det</b>: kjeden i lovteksten og hvor bindingen gjelder, før lagring. Etter lagring en kvittering med lenker.</li>
 * </ul>
 */
export function GeneriskEnSkjerm({
  kandidat, noder, virksomheter, autoApnet, onAnnet, onFerdig,
}: {
  kandidat: NavnekandidatDto;
  noder: RettskildeNodeDto[] | null;
  virksomheter: VirksomhetDto[];
  /** Veiviseren åpnet rett her fordi ordet er en kjent generisk omtale (standardregel eller registrert). */
  autoApnet: boolean;
  onAnnet: () => void;
  onFerdig: (k: GeneriskKvittering) => void;
}) {
  const [kontekst, setKontekst] = useState<GeneriskKandidatkontekst | null | undefined>(undefined);
  const [begreper, setBegreper] = useState<VirksomhetsbegrepDto[] | null>(null);
  const [valg, setValg] = useState<Valg | null>(null);
  const [sok, setSok] = useState('');
  const [omfang, setOmfang] = useState<'rettskilde' | 'paragraf' | 'spenn'>('rettskilde');
  const [fraEid, setFraEid] = useState('');
  const [tilEid, setTilEid] = useState('');
  const [kunnskapsstatus, setKunnskapsstatus] = useState<Kunnskapsstatus>('fortolkning');
  const [argumenter, setArgumenter] = useState<Tolkningsargument[]>(['kontekst_system']);
  const [begrunnelse, setBegrunnelse] = useState('');
  const [godkjenning, setGodkjenning] = useState<Godkjenning>('faglig_vurdert');
  const [gyldigFra, setGyldigFra] = useState('');
  const [gyldigTil, setGyldigTil] = useState('');
  const [lagrer, setLagrer] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    api.hentGeneriskKandidatkontekst(kandidat.id).then((k) => {
      setKontekst(k);
      // Forhåndsvalg: standardregelen når den løser (da trengs ingen binding). Ellers ingenting — et forslag velges av et menneske.
      if (k.regelsvar?.utfall === 'lost') setValg({ type: 'regel' });
    }).catch(() => setKontekst(null));
    api.hentGruppebegrep().then((b) => setBegreper(b.filter((x) => x.begrepskategori === 'klasse' || x.begrepskategori === 'rolle')))
      .catch(() => setBegreper([]));
  }, [kandidat.id]);

  const perId = useMemo(() => new Map((noder ?? []).map((n) => [n.id, n])), [noder]);
  const node = (noder ?? []).find((n) => n.eid === kandidat.nodeEid) ?? null;
  const paragraf = useMemo(() => {
    let n = node;
    while (n && n.nodeType !== 'paragraf') n = n.parentNodeId ? perId.get(n.parentNodeId) ?? null : null;
    return n;
  }, [node, perId]);
  const paragrafer = (noder ?? []).filter((n) => !n.opphevet && (n.nodeType === 'kapittel' || n.nodeType === 'paragraf'));
  const term = kontekst?.term ?? kandidat.foreslattTekst.trim().toLowerCase();
  const sted = node ? nodeEtikett(node, perId) : kandidat.nodeEid;

  function velgReferent(referent: StrukturnodeDto, forslag: GeneriskReferentforslag | null) {
    setValg({ type: 'referent', referent, forslag });
    setKunnskapsstatus(forslag?.kunnskapsstatus ?? 'fortolkning');
    setArgumenter(forslag?.tolkningsargumenter.length ? forslag.tolkningsargumenter : ['kontekst_system']);
    setBegrunnelse(forslag?.begrunnelse ?? `«${term}» i ${sted} i ${kontekst?.rettskildeTittel ?? 'rettskilden'} viser til ${referent.navn} ut fra sammenhengen.`);
    setSok('');
  }

  const sokTreff = useMemo(() => {
    const q = sok.trim().toLowerCase();
    if (q.length < 2) return [];
    const akt: StrukturnodeDto[] = virksomheter.filter((v) => !v.livsstatus && v.visningsnavn.toLowerCase().includes(q))
      .map((v) => ({ type: 'virksomhet', id: v.id, navn: v.visningsnavn, nodetype: null }));
    const beg: StrukturnodeDto[] = (begreper ?? []).filter((b) => b.term.toLowerCase().includes(q))
      .map((b) => ({ type: 'begrep', id: b.id, navn: b.term, nodetype: b.begrepskategori }));
    // De som begynner med søket først, så kortest navn.
    return [...akt, ...beg].sort((a, b) => Number(!a.navn.toLowerCase().startsWith(q)) - Number(!b.navn.toLowerCase().startsWith(q))
      || a.navn.length - b.navn.length).slice(0, 8);
  }, [sok, virksomheter, begreper]);

  const spenn = omfang === 'rettskilde' ? [] : omfang === 'paragraf' ? (paragraf ? [{ fraEid: paragraf.eid, tilEid: null }] : [])
    : fraEid ? [{ fraEid, tilEid: tilEid || null }] : [];
  const omfangTekst = omfang === 'rettskilde' ? `hele ${kontekst?.rettskildeTittel ?? 'rettskilden'} (også nye versjoner av loven)`
    : omfang === 'paragraf' ? (paragraf ? nodeEtikett(paragraf, perId) : 'paragrafen') : fraEid ? 'det valgte spennet' : '—';

  const binder = valg?.type === 'referent';
  const detaljFeil = !binder ? null
    : omfang === 'spenn' && !fraEid ? 'Velg hvor spennet starter.'
    : omfang === 'paragraf' && !paragraf ? 'Fant ikke paragrafen ordet står i — velg et spenn.'
    : kunnskapsstatus === 'fortolkning' && (!begrunnelse.trim() || argumenter.length === 0) ? 'Fortolkning krever en begrunnelse og minst ett argument (under «Detaljer»).'
    : gyldigFra && gyldigTil && gyldigFra > gyldigTil ? '«Gjelder fra» kan ikke være etter «Gjelder til».'
    : null;

  async function lagre() {
    if (!valg || detaljFeil) return;
    setLagrer(true);
    setFeil(null);
    const binding: NavneformBindingRequest | null = valg.type !== 'referent' ? null : {
      tilVirksomhetId: valg.referent.type === 'virksomhet' ? valg.referent.id : null,
      tilBegrepId: valg.referent.type === 'begrep' ? valg.referent.id : null,
      paragrafspenn: spenn,
      hjemmelRettskildeId: kandidat.rettskildeId,
      hjemmelEid: kandidat.nodeEid,
      kildeUtenforKorpusTekst: null, kildeUtenforKorpusLenke: null, kildeUtenforKorpusLenker: null,
      kildeUtenforKorpusType: null, kildeUtenforKorpusDokumentasjon: null,
      kunnskapsstatus,
      begrunnelse: begrunnelse.trim() || null,
      tolkningsargumenter: kunnskapsstatus === 'fortolkning' ? argumenter : null,
      status: godkjenning,
      kommentar: null,
      gyldigFra: gyldigFra || null,
      gyldigTil: gyldigTil || null,
    };
    try {
      const r = await api.markerNavnekandidatSomGenerisk(kandidat.id, binding);
      const referent = valg.type === 'referent' ? valg.referent : valg.type === 'regel' ? kontekst?.regelsvar?.referentVisning ?? null : null;
      onFerdig({
        tittel: valg.type === 'referent'
          ? `«${r.navneform.term}» betyr ${valg.referent.navn} i ${omfangTekst}.`
          : valg.type === 'regel'
            ? `«${r.navneform.term}» er markert som generisk omtale. Standardregelen gir ${referent?.navn ?? 'svaret'}.`
            : `«${r.navneform.term}» er markert som generisk omtale, ikke avklart.`,
        detaljer: [
          r.varNyNavneform ? 'Den generiske navneformen er opprettet — én for hele korpuset.' : 'Den generiske navneformen fantes alt og er gjenbrukt.',
          valg.type === 'referent'
            ? `Bindingen er lagret som ${godkjenning === 'godkjent' ? 'godkjent' : 'faglig vurdert'} og brukes i oppslaget med en gang. `
              + `Grunnlag: ${sted} (der ordet står), ${KUNNSKAPSSTATUS_VISNING[kunnskapsstatus].tekst.toLowerCase()}.`
            : valg.type === 'regel' ? 'Ingen binding er lagret: standardregelen gir svaret, og en vurdert ressortfordeling slår igjennom når den godkjennes.'
              : 'Ingen binding: omtalen vises som «generisk omtale, ikke avklart» til noen binder den.',
          r.taggId ? 'Taggen på forekomsten peker på den generiske navneformen og viser kjeden.' : 'Ingen tagg kunne opprettes.',
          'Navnekandidaten er satt til «Godkjent» (behandlet).',
        ],
        rettskildeLenke: rettskildeLenkeForId(kandidat.rettskildeId, kandidat.nodeEid),
        referentLenke: referent ? lenkeFor(referent) : null,
        referentErBegrep: referent?.type === 'begrep',
      });
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke lagre.');
    } finally {
      setLagrer(false);
    }
  }

  if (kontekst === undefined) return <Spinner aria-label="Henter forslag …" data-size="sm" />;
  if (kontekst === null) return <Alert data-color="danger" data-size="sm">Kunne ikke hente forslagene for kandidaten.</Alert>;
  const regel = kontekst.regelsvar;
  const forslag = kontekst.forslag.filter((f) => f.kilde !== 'standardregel');
  const kjedeKilde = valg?.type === 'regel' ? generiskKildeTekst('regel', null, regel)
    : valg?.type === 'referent' ? generiskKildeTekst('binding', godkjenning) : 'ikke avklart';
  const kjedeReferent = valg?.type === 'regel' ? regel?.referentVisning?.navn : valg?.type === 'referent' ? valg.referent.navn : null;

  return (
    <div style={{ display: 'grid', gap: '0.9rem' }}>
      {autoApnet && (
        <Alert data-color="info" data-size="sm">
          «{term}» er {kontekst.standardregel ? 'et ord med standardregel' : 'registrert som generisk omtale'}, så veiviseren åpnet rett her.{' '}
          <Button data-size="sm" variant="tertiary" onClick={onAnnet}>Behandle som noe annet</Button>
        </Alert>
      )}
      <Metatekst>
        En generisk omtale er et fellesnavn — «styret», «departementet» — som betyr noe bestemt i hver lov. Eksempel: «departementet» i
        reindriftsloven er Landbruks- og matdepartementet. Velg hva «{term}» betyr i {kontekst.rettskildeTittel}.
      </Metatekst>

      {kontekst.bindingerHer.length > 0 && (
        <Alert data-color="info" data-size="sm">
          Det finnes alt {kontekst.bindingerHer.length === 1 ? 'en binding' : `${kontekst.bindingerHer.length} bindinger`} her:{' '}
          {kontekst.bindingerHer.map((b) => `${b.referent.navn} (${b.paragrafspenn.length ? 'paragrafspenn' : 'hele rettskilden'}, ${periodeTekst(b.gyldigFra, b.gyldigTil)}, ${b.status === 'godkjent' ? 'godkjent' : b.status === 'faglig_vurdert' ? 'faglig vurdert' : 'forslag'})`).join('; ')}.
          En ny binding må gjelde et annet spenn eller en annen periode.
        </Alert>
      )}

      <Fieldset data-size="sm">
        <Fieldset.Legend>Hva betyr «{term}» her?</Fieldset.Legend>
        {kontekst.standardregel && (
          regel?.utfall === 'lost' && regel.referentVisning ? (
            <Radio name="generiskValg" value="regel" checked={valg?.type === 'regel'} onChange={() => setValg({ type: 'regel' })}
              label={<>Standardregelen: {regel.referentVisning.navn} <GeneriskKildeTagger oppslag={{ kilde: 'regel', bindingStatus: null, regel }} /></>}
              description={`${regel.forklaring}${regel.merknader.length ? ` ${regel.merknader.join(' ')}` : ''} Ingen binding trengs.`} />
          ) : (
            <Alert data-color="warning" data-size="sm">
              Standardregelen «{kontekst.standardregel.navn}» gir ikke svar her: {regel?.forklaring}
              {regel?.merknader.length ? ` ${regel.merknader.join(' ')}` : ''}
            </Alert>
          )
        )}
        {forslag.map((f) => (
          <Radio key={`${f.referent.type}-${f.referent.id}`} name="generiskValg" value={f.referent.id}
            checked={valg?.type === 'referent' && valg.referent.id === f.referent.id} onChange={() => velgReferent(f.referent, f)}
            label={<>{f.referent.navn} <Tag data-size="sm" data-color="neutral">{FORSLAG_ETIKETT[f.kilde]}</Tag></>}
            description={f.grunn} />
        ))}
        {valg?.type === 'referent' && !valg.forslag && (
          <Radio name="generiskValg" value={valg.referent.id} checked onChange={() => undefined}
            label={<>{valg.referent.navn} <Tag data-size="sm" data-color="neutral">Valgt i søket</Tag></>} />
        )}
        <Radio name="generiskValg" value="marker" checked={valg?.type === 'marker'} onChange={() => setValg({ type: 'marker' })}
          label="Ingen av dem — bare marker som generisk"
          description="Omtalen kobles til den generiske navneformen og vises som «generisk omtale, ikke avklart» til noen binder den." />
      </Fieldset>

      <div>
        <Textfield data-size="sm" label="Finner du det ikke? Søk etter aktør, klasse eller rolle" value={sok}
          onChange={(e) => setSok(e.target.value)} style={{ maxWidth: '28rem' }} />
        {sok.trim().length >= 2 && (
          sokTreff.length === 0 ? <Metatekst>Ingen treff på «{sok}».</Metatekst> : (
            <ul style={{ listStyle: 'none', margin: '0.35rem 0 0', padding: 0, display: 'grid', gap: '0.2rem', maxWidth: '28rem' }}>
              {sokTreff.map((t) => (
                <li key={`${t.type}-${t.id}`}>
                  <Button data-size="sm" variant="tertiary" onClick={() => velgReferent(t, null)}>
                    {t.navn}
                  </Button>{' '}
                  <Tag data-size="sm" data-color="neutral">{t.type === 'virksomhet' ? 'Aktør' : t.nodetype === 'rolle' ? 'Rolle' : 'Klasse'}</Tag>
                </li>
              ))}
            </ul>
          )
        )}
      </div>

      {binder && (
        <Fieldset data-size="sm">
          <Fieldset.Legend>Hvor gjelder det?</Fieldset.Legend>
          <Radio name="omfang" value="rettskilde" checked={omfang === 'rettskilde'} onChange={() => setOmfang('rettskilde')}
            label={`Hele ${kontekst.rettskildeTittel}`}
            description={`Alle ${kontekst.forekomsterIRettskilden} forekomster av «${term}» i rettskilden, også i nye versjoner av loven. En smalere binding går foran.`} />
          {paragraf && (
            <Radio name="omfang" value="paragraf" checked={omfang === 'paragraf'} onChange={() => setOmfang('paragraf')}
              label={`Bare ${nodeEtikett(paragraf, perId)}`} description="Paragrafen ordet står i. Resten av rettskilden følger standardregelen eller andre bindinger." />
          )}
          <Radio name="omfang" value="spenn" checked={omfang === 'spenn'} onChange={() => setOmfang('spenn')}
            label="Et annet kapittel eller paragrafspenn" />
          {omfang === 'spenn' && (
            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
              <Field data-size="sm" style={{ maxWidth: '16rem' }}>
                <Label>Fra</Label>
                <Select data-size="sm" value={fraEid} onChange={(e) => setFraEid(e.target.value)}>
                  <Select.Option value="">Velg …</Select.Option>
                  {paragrafer.map((n) => <Select.Option key={n.id} value={n.eid}>{nodeEtikett(n, perId)}</Select.Option>)}
                </Select>
              </Field>
              <Field data-size="sm" style={{ maxWidth: '16rem' }}>
                <Label>Til og med (valgfritt)</Label>
                <Select data-size="sm" value={tilEid} onChange={(e) => setTilEid(e.target.value)}>
                  <Select.Option value="">Bare denne</Select.Option>
                  {paragrafer.map((n) => <Select.Option key={n.id} value={n.eid}>{nodeEtikett(n, perId)}</Select.Option>)}
                </Select>
              </Field>
            </div>
          )}
        </Fieldset>
      )}

      {binder && (
        <Details>
          <Details.Summary>Detaljer: grunnlag og sikkerhet ({KUNNSKAPSSTATUS_VISNING[kunnskapsstatus].tekst.toLowerCase()}, {godkjenning === 'godkjent' ? 'godkjent' : 'faglig vurdert'})</Details.Summary>
          <Details.Content>
            <div style={{ display: 'grid', gap: '0.6rem' }}>
              <Metatekst>
                Grunnlaget er bestemmelsen der ordet står: {sted} i {kontekst.rettskildeTittel}. Kunnskapsstatusen sier hvordan svaret er funnet —
                «Fortolkning» når det følger av sammenhengen, «Tekstfunn» når loven selv sier det (for eksempel en definisjon).
              </Metatekst>
              <Field data-size="sm" style={{ maxWidth: '20rem' }}>
                <Label>Kunnskapsstatus</Label>
                <Select data-size="sm" value={kunnskapsstatus} onChange={(e) => setKunnskapsstatus(e.target.value as Kunnskapsstatus)}>
                  {(['fortolkning', 'tekstfunn', 'normalisering', 'uavklart', 'omstridt'] as Kunnskapsstatus[]).map((k) => (
                    <Select.Option key={k} value={k}>{KUNNSKAPSSTATUS_VISNING[k].tekst} — {KUNNSKAPSSTATUS_VISNING[k].forklaring}</Select.Option>
                  ))}
                </Select>
              </Field>
              {kunnskapsstatus === 'fortolkning' && (
                <Fieldset data-size="sm">
                  <Fieldset.Legend>Tolkningsargumenter</Fieldset.Legend>
                  {ARGUMENTER.map((a) => (
                    <Checkbox key={a.verdi} label={a.tekst} value={a.verdi} checked={argumenter.includes(a.verdi)}
                      onChange={(e) => setArgumenter(e.target.checked ? [...argumenter, a.verdi] : argumenter.filter((x) => x !== a.verdi))} />
                  ))}
                </Fieldset>
              )}
              <Field data-size="sm">
                <Label>Begrunnelse</Label>
                <Textarea data-size="sm" rows={2} value={begrunnelse} onChange={(e) => setBegrunnelse(e.target.value)} />
              </Field>
              <Fieldset data-size="sm">
                <Fieldset.Legend>Hvor sikkert er det?</Fieldset.Legend>
                <Radio name="godkjenning" value="faglig_vurdert" checked={godkjenning === 'faglig_vurdert'} onChange={() => setGodkjenning('faglig_vurdert')}
                  label="Faglig vurdert" description="Du har vurdert det. Bindingen brukes i oppslaget og står med statusen synlig." />
                <Radio name="godkjenning" value="godkjent" checked={godkjenning === 'godkjent'} onChange={() => setGodkjenning('godkjent')}
                  label="Godkjent" description="Endelig: du har myndighet til å godkjenne modellutsagn." />
              </Fieldset>
              <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                <Textfield data-size="sm" type="date" label="Gjelder fra (valgfritt)" value={gyldigFra} onChange={(e) => setGyldigFra(e.target.value)} />
                <Textfield data-size="sm" type="date" label="Gjelder til (valgfritt)" value={gyldigTil} onChange={(e) => setGyldigTil(e.target.value)} />
              </div>
            </div>
          </Details.Content>
        </Details>
      )}

      <Divider />
      <Heading level={3} data-size="xs">Slik blir det</Heading>
      {!valg ? <Metatekst>Velg hva «{term}» betyr, så vises resultatet her før du lagrer.</Metatekst> : (
        <div style={{ display: 'grid', gap: '0.4rem' }}>
          {node?.tekst && (
            <blockquote style={{ margin: 0, paddingLeft: '0.75rem', borderLeft: '3px solid var(--ds-color-neutral-border-subtle)' }}>
              <Metatekst as="span">{sted}: </Metatekst>
              <MedOrdet tekst={node.tekst} term={term}
                etikett={`${term} (generisk; ${kjedeKilde}) → ${kjedeReferent ?? 'generisk omtale, ikke avklart'}`} />
            </blockquote>
          )}
          <div style={{ display: 'flex', gap: '0.35rem', alignItems: 'center', flexWrap: 'wrap' }}>
            «{term}» → <NavneformgrunnTag grunn="generisk" />
            {valg.type === 'regel' && <GeneriskKildeTagger oppslag={{ kilde: 'regel', bindingStatus: null, regel }} />}
            {valg.type === 'referent' && <GeneriskKildeTagger oppslag={{ kilde: 'binding', bindingStatus: godkjenning, regel: null }} />}
            {valg.type === 'marker' && <GeneriskKildeTagger oppslag={{ kilde: null, bindingStatus: null, regel: null }} />}
            → {kjedeReferent
              ? (valg.type === 'referent' ? <Link asChild><RouterLink to={lenkeFor(valg.referent)}>{kjedeReferent}</RouterLink></Link> : <strong>{kjedeReferent}</strong>)
              : <Metatekst as="span">generisk omtale, ikke avklart</Metatekst>}
            {valg.type === 'referent' && <KunnskapsstatusTag status={kunnskapsstatus} />}
          </div>
          <Metatekst>
            {valg.type === 'referent'
              ? `Gjelder i ${omfangTekst}${gyldigFra || gyldigTil ? `, ${periodeTekst(gyldigFra || null, gyldigTil || null)}` : ''}. `
                + (omfang === 'rettskilde' && kontekst.standardregel ? 'Bindingen går foran standardregelen i hele rettskilden.' : '')
              : valg.type === 'regel' ? `Ingen binding lagres. Standardregelen gir svaret for alle ${kontekst.forekomsterIRettskilden} forekomster i rettskilden, så lenge ingen binding går foran.`
                : 'Ingen binding lagres.'}
          </Metatekst>
        </div>
      )}

      {feil && <Alert data-color="danger" data-size="sm">{feil}</Alert>}
      {detaljFeil && <Metatekst>{detaljFeil}</Metatekst>}
      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
        <Button data-size="sm" onClick={lagre} disabled={!valg || lagrer || detaljFeil !== null}>
          {lagrer ? 'Lagrer …' : valg?.type === 'referent' ? 'Lagre bindingen' : 'Marker som generisk'}
        </Button>
        {!autoApnet && <Button data-size="sm" variant="tertiary" onClick={onAnnet}>Tilbake</Button>}
      </div>
    </div>
  );
}
