import { createContext, useContext, useState, type Dispatch, type ReactNode, type SetStateAction } from 'react';
import {
  Alert, Button, Label, Paragraph, Select, Tag, Textarea, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { GYLDIGE_RETTIGHETSTYPER } from '../api/types';
import type {
  EgetInnholdselementInput, RettskildeNodeDto, RettskildeSammendrag, TjenesteDto, TjenesteInnholdInput,
  TjenesteRegelverksreferanseDto, TjenesteRequest,
} from '../api/types';
import { ACCORDION_LABELER, feltnokkelForEgetInnhold } from '../api/tjenesteFelt';
import { KobleRegelverksreferanseForm } from '../rettskilde/KobleRegelverksreferanseForm';
import { Accordion } from '../entitet/Accordion';
import type { DetaljVisning } from '../entitet/detaljVisning';

// ---------- Felt-nivå regelverksreferanser (§-tagger) — delt av alle felt i denne fanen ----------

interface FeltReferanserCtx {
  referanser: TjenesteRegelverksreferanseDto[];
  tjenesteId: string;
  rettskilder: RettskildeSammendrag[];
  noderPerRettskilde: Map<string, RettskildeNodeDto[]>;
  sikreNoderFor: (id: string) => void;
  onReferanseLagtTil: (r: TjenesteRegelverksreferanseDto) => void;
  onReferanseFjernet: (id: string) => void;
  onSelectDetail: (v: DetaljVisning) => void;
}
const Ctx = createContext<FeltReferanserCtx | null>(null);

function FeltReferanser({ feltKey }: { feltKey: string }) {
  const ctx = useContext(Ctx);
  const [visForm, setVisForm] = useState(false);
  if (!ctx) return null;
  const koblede = ctx.referanser.filter((r) => r.felt === feltKey);

  async function fjern(referanseId: string) {
    await api.fjernTjenesteRegelverksreferanse(referanseId);
    ctx!.onReferanseFjernet(referanseId);
  }

  function visDetalj(r: TjenesteRegelverksreferanseDto) {
    const rettskilde = ctx!.rettskilder.find((rk) => rk.id === r.tilRettskildeId);
    const node = ctx!.noderPerRettskilde.get(r.tilRettskildeId)?.find((n) => n.eid === r.tilEid);
    ctx!.onSelectDetail({
      title: node?.overskrift ?? node?.nummer ?? r.tilEid,
      meta: rettskilde ? rettskilde.tittel : 'Regelverksreferanse',
      body: node?.tekst ?? null,
    });
  }

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: '0.3rem', flexWrap: 'wrap' }}>
      {koblede.map((r) => {
        const rettskilde = ctx.rettskilder.find((rk) => rk.id === r.tilRettskildeId);
        const node = ctx.noderPerRettskilde.get(r.tilRettskildeId)?.find((n) => n.eid === r.tilEid);
        // Full tittel (issue #151) i stedet for kortnavn — kan bli lang når noden mangler et §-nummer
        // (hele-dokument-referanse); trunkeres visuelt med ellipsis i stedet for å falle tilbake til
        // kortnavn, full tittel er fortsatt tilgjengelig i title-attributten (hover) og i detaljpanelet.
        const merkelapp = node?.nummer ? `§ ${node.nummer}` : (rettskilde?.tittel ?? r.tilEid);
        return (
          <Tag key={r.id} data-size="sm" variant="outline" style={{ cursor: 'pointer', maxWidth: '16rem' }} onClick={() => visDetalj(r)}
            title={node?.nummer ? 'Vis detaljer' : `${merkelapp} — Vis detaljer`}>
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '13rem' }}>
              {merkelapp}
            </span>
            <button type="button" onClick={(e) => { e.stopPropagation(); fjern(r.id); }}
              aria-label="Fjern referanse"
              style={{ marginLeft: '0.3rem', background: 'none', border: 'none', padding: 0, cursor: 'pointer', font: 'inherit' }}>
              ✕
            </button>
          </Tag>
        );
      })}
      <Button variant="tertiary" data-size="sm" onClick={() => setVisForm((v) => !v)} title="Legg til lovreferanse"
        style={{ minWidth: 0, padding: '0 0.4rem' }}>
        §
      </Button>
      {visForm && (
        <div style={{ flexBasis: '100%', marginTop: '0.3rem' }}>
          <KobleRegelverksreferanseForm
            tjenesteId={ctx.tjenesteId} felt={feltKey} rettskilder={ctx.rettskilder}
            noderPerRettskilde={ctx.noderPerRettskilde} sikreNoderFor={ctx.sikreNoderFor}
            kompakt
            onKoblet={(r) => { ctx.onReferanseLagtTil(r); setVisForm(false); }}
          />
        </div>
      )}
    </div>
  );
}

/** Feltet rendrer sin egen synlige `Label` (for å få plass til §-taggraden ved siden av den) i
 * stedet for å la `Field` wire den til kontrollen automatisk (docs/09 §5-mønsteret) — kontrollen
 * må derfor selv få `aria-label={"samme tekst som label"}` på kall-stedet for tilgjengelig navn
 * (Designsystemets Textfield/Textarea krever `label`/`aria-label`/`aria-labelledby`). */
function Felt({ feltKey, label, spanFull, help, children }: { feltKey: string; label: string; spanFull?: boolean; help?: string; children: ReactNode }) {
  return (
    <div style={spanFull ? { gridColumn: 'span 2' } : undefined}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '0.5rem', marginBottom: '0.25rem' }}>
        <Label style={{ margin: 0 }}>{label}</Label>
        <FeltReferanser feltKey={feltKey} />
      </div>
      {children}
      {help && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.2rem' }}>{help}</Paragraph>}
    </div>
  );
}

// ---------- Selve fanen ----------
// Accordion-skallet (Details, opp/ned/åpen) er flyttet til `../entitet/Accordion` (docs/30 §4 punkt 1).

export interface InnholdFaneProps {
  tjeneste: TjenesteDto;
  onTjenesteOppdatert: (t: TjenesteDto) => void;
  referanser: TjenesteRegelverksreferanseDto[] | null;
  setReferanser: Dispatch<SetStateAction<TjenesteRegelverksreferanseDto[] | null>>;
  rettskilder: RettskildeSammendrag[];
  noderPerRettskilde: Map<string, RettskildeNodeDto[]>;
  sikreNoderFor: (id: string) => void;
  onSelectDetail: (v: DetaljVisning) => void;
  accordionRekkefolge: string[];
  accordionApne: Record<string, boolean>;
  flyttAccordion: (nokkel: string, retning: -1 | 1) => void;
  settAccordionApen: (nokkel: string, apen: boolean) => void;
  apneAlleAccordions: (apen: boolean) => void;
}

/** Én rad tekst per linje ↔ liste, samme hjelpefunksjoner som opprinnelige TjenesteDetalj.tsx. */
function tilListeNL(tekst: string): string[] {
  return tekst.split('\n').map((s) => s.trim()).filter(Boolean);
}
function fraListeNL(liste: string[]): string {
  return liste.join('\n');
}
function tilListe(kommaseparert: string): string[] {
  return kommaseparert.split(',').map((s) => s.trim()).filter(Boolean);
}

export function InnholdFane({
  tjeneste, onTjenesteOppdatert, referanser, setReferanser, rettskilder, noderPerRettskilde, sikreNoderFor,
  onSelectDetail, accordionRekkefolge, accordionApne, flyttAccordion, settAccordionApen, apneAlleAccordions,
}: InnholdFaneProps) {
  // ---- Grunnleggende (Egenskaper) ----
  const [tittel, setTittel] = useState(tjeneste.tittel);
  const [beskrivelse, setBeskrivelse] = useState(tjeneste.beskrivelse ?? '');
  const [formal, setFormal] = useState(tjeneste.formal ?? '');
  const [kompetentMyndighet, setKompetentMyndighet] = useState(tjeneste.kompetentMyndighet ?? '');
  const [tjenestetype, setTjenestetype] = useState(tjeneste.tjenestetype ?? '');
  const [type, setType] = useState(tjeneste.type ?? '');
  const [malgruppe, setMalgruppe] = useState(tjeneste.malgruppe.join(', '));
  const [kanaler, setKanaler] = useState(tjeneste.kanaler.join(', '));
  const [kostnad, setKostnad] = useState(tjeneste.kostnad ?? '');
  const [behandlingstid, setBehandlingstid] = useState(tjeneste.behandlingstid ?? '');
  const [kontaktpunkt, setKontaktpunkt] = useState(tjeneste.kontaktpunkt ?? '');
  const [konsekvensVedBrudd, setKonsekvensVedBrudd] = useState(tjeneste.konsekvensVedBrudd ?? '');
  const [sprak, setSprak] = useState(tjeneste.sprak.join(', '));
  const [livshendelser, setLivshendelser] = useState(tjeneste.livshendelser.join(', '));
  const [losKlassifisering, setLosKlassifisering] = useState(tjeneste.losKlassifisering ?? '');
  const [tjenesteomrade, setTjenesteomrade] = useState(tjeneste.tjenesteomrade ?? '');
  const [output, setOutput] = useState(tjeneste.output ?? '');

  // ---- Innhold-underfelt ----
  const i = tjeneste.innhold;
  const [iTidspunktOgFrister, setITidspunktOgFrister] = useState(i?.tidspunktOgFrister ?? '');
  const [iInnsenderHvemKanSende, setIInnsenderHvemKanSende] = useState(fraListeNL(i?.innsenderOgTilgang?.hvemKanSende ?? []));
  const [iInnsenderInnlogging, setIInnsenderInnlogging] = useState(i?.innsenderOgTilgang?.innlogging ?? '');
  const [iVedlegg, setIVedlegg] = useState(fraListeNL(i?.vedlegg ?? []));
  const [iVedleggMerknad, setIVedleggMerknad] = useState(i?.vedleggMerknad ?? '');
  const [iOpplysninger, setIOpplysninger] = useState(fraListeNL(i?.opplysningerSomSkalSendesInn ?? []));
  const [iOpplysningerMerknad, setIOpplysningerMerknad] = useState(i?.opplysningerMerknad ?? '');
  const [iVeiledning, setIVeiledning] = useState(fraListeNL(i?.veiledningOgUtfylling ?? []));
  const [iVeiledningMerknad, setIVeiledningMerknad] = useState(i?.veiledningMerknad ?? '');
  const [iInnsendingKanal, setIInnsendingKanal] = useState(i?.innsendingOgOppfolging?.kanal ?? '');
  const [iInnsendingEtterMottak, setIInnsendingEtterMottak] = useState(fraListeNL(i?.innsendingOgOppfolging?.etterMottak ?? []));
  const [iInnsendingMerknad, setIInnsendingMerknad] = useState(i?.innsendingOgOppfolging?.merknad ?? '');
  const [iKontaktGenerelt, setIKontaktGenerelt] = useState(i?.kontaktOgHjelp?.generelt ?? '');
  const [iKontaktKommunenKanVeiledeOm, setIKontaktKommunenKanVeiledeOm] = useState(fraListeNL(i?.kontaktOgHjelp?.kommunenKanVeiledeOm ?? []));
  const [iHviInnledning, setIHviInnledning] = useState(i?.hvaRettighetenInnebarer?.innledning ?? '');
  const [iHviVarighet, setIHviVarighet] = useState(i?.hvaRettighetenInnebarer?.varighet ?? '');
  const [iHviPlikter, setIHviPlikter] = useState(fraListeNL(i?.hvaRettighetenInnebarer?.plikter ?? []));
  const [iHviEndringerPlikt, setIHviEndringerPlikt] = useState(i?.hvaRettighetenInnebarer?.endringerIVirksomheten?.plikt ?? '');
  const [iHviEndringerEksempler, setIHviEndringerEksempler] = useState(fraListeNL(i?.hvaRettighetenInnebarer?.endringerIVirksomheten?.eksempler ?? []));
  const [iHviKravTilDrift, setIHviKravTilDrift] = useState(i?.hvaRettighetenInnebarer?.kravTilDrift ?? '');
  const [iHviTommeavtaleOgKontroll, setIHviTommeavtaleOgKontroll] = useState(i?.hvaRettighetenInnebarer?.tommeavtaleOgKontroll ?? '');
  const [iHviRapportering, setIHviRapportering] = useState(i?.hvaRettighetenInnebarer?.rapportering ?? '');
  const [iHviKontrollOgTilsyn, setIHviKontrollOgTilsyn] = useState(i?.hvaRettighetenInnebarer?.kontrollOgTilsyn ?? '');
  const [iHviAvgrensningMerknad, setIHviAvgrensningMerknad] = useState(i?.hvaRettighetenInnebarer?.avgrensningMerknad ?? '');

  // ---- Egne innholdselementer — rekkefølge/åpen-tilstand er LOKAL/PER TJENESTE, ikke del av den
  // delte per-bruker useVisningsinnstillinger-tilstanden (se klassekommentaren i
  // BrukerVisningsinnstillingEntitet for begrunnelsen: en custom-nøkkel gir bare mening for DEN
  // ene tjenesten den ble skrevet på). Rendres ETTER de faste accordionene, i egen-arrayets
  // rekkefølge — reorder av `egne` ER selve rekkefølgen, ingen egen indeksliste. ----
  const [egne, setEgne] = useState<EgetInnholdselementInput[]>(tjeneste.egneInnholdselementer);
  const [egneApne, setEgneApne] = useState<Record<string, boolean>>(
    () => Object.fromEntries(tjeneste.egneInnholdselementer.map((el) => [el.id, true])),
  );

  const [lagrer, setLagrer] = useState(false);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);

  function byggInnhold(): TjenesteInnholdInput {
    const harEndringer = iHviEndringerPlikt.trim() || iHviEndringerEksempler.trim();
    const harInnsender = iInnsenderHvemKanSende.trim() || iInnsenderInnlogging.trim();
    const harInnsending = iInnsendingKanal.trim() || iInnsendingEtterMottak.trim() || iInnsendingMerknad.trim();
    const harKontakt = iKontaktGenerelt.trim() || iKontaktKommunenKanVeiledeOm.trim();
    return {
      tidspunktOgFrister: iTidspunktOgFrister.trim() || null,
      innsenderOgTilgang: harInnsender
        ? { hvemKanSende: tilListeNL(iInnsenderHvemKanSende), innlogging: iInnsenderInnlogging.trim() || null } : null,
      vedlegg: tilListeNL(iVedlegg),
      vedleggMerknad: iVedleggMerknad.trim() || null,
      opplysningerSomSkalSendesInn: tilListeNL(iOpplysninger),
      opplysningerMerknad: iOpplysningerMerknad.trim() || null,
      veiledningOgUtfylling: tilListeNL(iVeiledning),
      veiledningMerknad: iVeiledningMerknad.trim() || null,
      innsendingOgOppfolging: harInnsending
        ? { kanal: iInnsendingKanal.trim() || null, etterMottak: tilListeNL(iInnsendingEtterMottak), merknad: iInnsendingMerknad.trim() || null }
        : null,
      kontaktOgHjelp: harKontakt
        ? { generelt: iKontaktGenerelt.trim() || null, kommunenKanVeiledeOm: tilListeNL(iKontaktKommunenKanVeiledeOm) } : null,
      hvaRettighetenInnebarer: {
        innledning: iHviInnledning.trim() || null,
        varighet: iHviVarighet.trim() || null,
        plikter: tilListeNL(iHviPlikter),
        endringerIVirksomheten: harEndringer
          ? { plikt: iHviEndringerPlikt.trim() || null, eksempler: tilListeNL(iHviEndringerEksempler) } : null,
        kontrollOgTilsyn: iHviKontrollOgTilsyn.trim() || null,
        avgrensningMerknad: iHviAvgrensningMerknad.trim() || null,
        kravTilDrift: iHviKravTilDrift.trim() || null,
        tommeavtaleOgKontroll: iHviTommeavtaleOgKontroll.trim() || null,
        rapportering: iHviRapportering.trim() || null,
      },
    };
  }

  /** ÉN lagre-handling for HELE fanen (Grunnleggende + alle Innhold-underseksjoner + egne
   * innholdselementer) — samme "ett skjema"-tanke som selve mockup-canvasen hadde (kun ETT
   * Lagre-knapp der òg, i Grunnleggende-accordionen), unngår risikoen ved delvise lagringer som
   * ville krevd at HVER accordion kjente hele det andre skjemaets nåværende tilstand for ikke å
   * overskrive den ved en PUT.
   * <p>
   * Bevisst en VANLIG knapp-`onClick`, IKKE en `<form onSubmit>` — hvert felts §-knapp åpner sin
   * egen `KobleRegelverksreferanseForm`, som ER et `<form>`. HTML tillater ikke nestede
   * `&lt;form&gt;`-elementer (og en nestet form-submit her viste seg i praksis å utløse en FULL
   * side-omlasting i stedet for React-handleren — reelt funn, ikke en teoretisk bekymring), så
   * denne fanen har bevisst INGEN omsluttende form-element. */
  async function lagre() {
    setLagreFeil(null);
    setLagrer(true);
    try {
      const request: TjenesteRequest = {
        tittel: tittel.trim(), beskrivelse: beskrivelse.trim() || null,
        kompetentMyndighet: kompetentMyndighet.trim() || null, output: output.trim() || null,
        tjenestetype: tjenestetype.trim() || null, malgruppe: tilListe(malgruppe), kanaler: tilListe(kanaler),
        kostnad: kostnad.trim() || null, behandlingstid: behandlingstid.trim() || null, kontaktpunkt: kontaktpunkt.trim() || null,
        konsekvensVedBrudd: konsekvensVedBrudd.trim() || null, sprak: tilListe(sprak),
        livshendelser: tilListe(livshendelser), losKlassifisering: losKlassifisering.trim() || null,
        tjenesteomrade: tjenesteomrade.trim() || null,
        type: type || null, formal: formal.trim() || null, innhold: byggInnhold(),
        egneInnholdselementer: egne,
      };
      onTjenesteOppdatert(await api.oppdaterTjeneste(tjeneste.id, request));
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  function leggTilEgetInnholdselement() {
    const nytt: EgetInnholdselementInput = { id: crypto.randomUUID(), tittel: 'Nytt innholdselement', tekst: '' };
    setEgne((forrige) => [...forrige, nytt]);
    setEgneApne((forrige) => ({ ...forrige, [nytt.id]: true }));
  }
  function fjernEgetInnholdselement(elementId: string) {
    setEgne((forrige) => forrige.filter((el) => el.id !== elementId));
    setEgneApne((forrige) => { const { [elementId]: _fjernet, ...rest } = forrige; return rest; });
  }
  function oppdaterEgetInnholdselement(elementId: string, patch: Partial<EgetInnholdselementInput>) {
    setEgne((forrige) => forrige.map((el) => (el.id === elementId ? { ...el, ...patch } : el)));
  }
  function flyttEgetInnholdselement(elementId: string, retning: -1 | 1) {
    setEgne((forrige) => {
      const liste = forrige.slice();
      const i = liste.findIndex((el) => el.id === elementId);
      const j = i + retning;
      if (i < 0 || j < 0 || j >= liste.length) return forrige;
      [liste[i], liste[j]] = [liste[j], liste[i]];
      return liste;
    });
  }

  const ctxValue: FeltReferanserCtx = {
    referanser: referanser ?? [], tjenesteId: tjeneste.id, rettskilder, noderPerRettskilde, sikreNoderFor,
    onReferanseLagtTil: (r) => setReferanser((forrige) => [...(forrige ?? []), r]),
    onReferanseFjernet: (id) => setReferanser((forrige) => (forrige ?? []).filter((r) => r.id !== id)),
    onSelectDetail,
  };

  const gridStil = { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem 1.5rem' } as const;

  return (
    <Ctx.Provider value={ctxValue}>
      <div style={{ maxWidth: '780px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', margin: 0 }}>
            Alle tjenester har de faste seksjonene. Legg til egne innholdselementer nederst ved behov.
          </Paragraph>
          <div style={{ display: 'flex', gap: '0.5rem', flex: '0 0 auto' }}>
            <Button type="button" variant="secondary" data-size="sm" onClick={() => apneAlleAccordions(true)}>Åpne alle</Button>
            <Button type="button" variant="secondary" data-size="sm" onClick={() => apneAlleAccordions(false)}>Lukk alle</Button>
            <Button type="button" data-size="sm" disabled={lagrer} onClick={lagre}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </div>
        {lagreFeil && <Alert data-color="danger" style={{ marginBottom: '0.75rem' }}>{lagreFeil}</Alert>}

        {accordionRekkefolge.map((nokkel, idx) => {
          const flytt = (retning: -1 | 1) => flyttAccordion(nokkel, retning);
          const kanFlyttes = { opp: idx > 0, ned: idx < accordionRekkefolge.length - 1 };
          const apen = !!accordionApne[nokkel];

          if (nokkel === 'grunnleggende') {
            return (
              <Accordion key={nokkel} apen={apen} kanFlyttes={kanFlyttes} onToggle={(v) => settAccordionApen(nokkel, v)}
                onFlytt={flytt} tittel={ACCORDION_LABELER.grunnleggende} tittelSuffiks="— fast">
                <div style={gridStil}>
                  <Felt feltKey="tittel" label="Tittel">
                    <Textfield aria-label="Tittel" value={tittel} onChange={(e) => setTittel(e.target.value)} style={{ width: '100%' }} required />
                  </Felt>
                  <Felt feltKey="tjenestetype" label="Tjenestetype">
                    <Textfield aria-label="Tjenestetype" value={tjenestetype} onChange={(e) => setTjenestetype(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="beskrivelse" label="Beskrivelse" spanFull help="Kort, notatpreget — se Formål for det fulle lovformålet.">
                    <Textarea aria-label="Beskrivelse" value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={3} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="formal" label="Formål" spanFull>
                    <Textarea aria-label="Formål" value={formal} onChange={(e) => setFormal(e.target.value)} rows={3} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="kompetentMyndighet" label="Kompetent myndighet">
                    <Textfield aria-label="Kompetent myndighet" value={kompetentMyndighet} onChange={(e) => setKompetentMyndighet(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="type" label="Rettighetstype">
                    <Select value={type} onChange={(e) => setType(e.target.value)} style={{ width: '100%' }}>
                      <Select.Option value="">Ikke satt</Select.Option>
                      {GYLDIGE_RETTIGHETSTYPER.map((t) => <Select.Option key={t} value={t}>{t}</Select.Option>)}
                    </Select>
                  </Felt>
                  <Felt feltKey="malgruppe" label="Målgruppe (kommaseparert)" spanFull>
                    <Textfield aria-label="Målgruppe (kommaseparert)" value={malgruppe} onChange={(e) => setMalgruppe(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="kanaler" label="Kanaler (kommaseparert)" spanFull>
                    <Textfield aria-label="Kanaler (kommaseparert)" value={kanaler} onChange={(e) => setKanaler(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="kostnad" label="Kostnad" spanFull>
                    <Textfield aria-label="Kostnad" value={kostnad} onChange={(e) => setKostnad(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="behandlingstid" label="Behandlingstid">
                    <Textfield aria-label="Behandlingstid" value={behandlingstid} onChange={(e) => setBehandlingstid(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="kontaktpunkt" label="Kontaktpunkt">
                    <Textfield aria-label="Kontaktpunkt" value={kontaktpunkt} onChange={(e) => setKontaktpunkt(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="konsekvensVedBrudd" label="Konsekvens ved brudd" spanFull>
                    <Textfield aria-label="Konsekvens ved brudd" value={konsekvensVedBrudd} onChange={(e) => setKonsekvensVedBrudd(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="sprak" label="Språk (kommaseparert)">
                    <Textfield aria-label="Språk (kommaseparert)" value={sprak} onChange={(e) => setSprak(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="livshendelser" label="Livshendelser (kommaseparert)">
                    <Textfield aria-label="Livshendelser (kommaseparert)" value={livshendelser} onChange={(e) => setLivshendelser(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="losKlassifisering" label="LOS-klassifisering">
                    <Textfield aria-label="LOS-klassifisering" value={losKlassifisering} onChange={(e) => setLosKlassifisering(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="tjenesteomrade" label="Tjenesteområde">
                    <Textfield aria-label="Tjenesteområde" value={tjenesteomrade} onChange={(e) => setTjenesteomrade(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="output" label="Output — hva tjenesten produserer (cv:produces)" spanFull>
                    <Textfield aria-label="Output — hva tjenesten produserer (cv:produces)" value={output} onChange={(e) => setOutput(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                </div>
              </Accordion>
            );
          }

          if (nokkel === 'tidspunkt') {
            return (
              <Accordion key={nokkel} apen={apen} kanFlyttes={kanFlyttes} onToggle={(v) => settAccordionApen(nokkel, v)}
                onFlytt={flytt} tittel={ACCORDION_LABELER.tidspunkt} tittelSuffiks="— fast">
                <Felt feltKey="innhold.tidspunktOgFrister" label="Innhold">
                  <Textarea aria-label="Innhold" value={iTidspunktOgFrister} onChange={(e) => setITidspunktOgFrister(e.target.value)} rows={3} style={{ width: '100%' }} />
                </Felt>
              </Accordion>
            );
          }

          if (nokkel === 'innsender') {
            return (
              <Accordion key={nokkel} apen={apen} kanFlyttes={kanFlyttes} onToggle={(v) => settAccordionApen(nokkel, v)}
                onFlytt={flytt} tittel={ACCORDION_LABELER.innsender} tittelSuffiks="— fast">
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
                  <Felt feltKey="innhold.innsenderOgTilgang.hvemKanSende" label="Hvem kan sende (én pr. linje)">
                    <Textarea aria-label="Hvem kan sende (én pr. linje)" value={iInnsenderHvemKanSende} onChange={(e) => setIInnsenderHvemKanSende(e.target.value)} rows={3} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.innsenderOgTilgang.innlogging" label="Innlogging">
                    <Textfield aria-label="Innlogging" value={iInnsenderInnlogging} onChange={(e) => setIInnsenderInnlogging(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                </div>
              </Accordion>
            );
          }

          if (nokkel === 'vedlegg') {
            return (
              <Accordion key={nokkel} apen={apen} kanFlyttes={kanFlyttes} onToggle={(v) => settAccordionApen(nokkel, v)}
                onFlytt={flytt} tittel={ACCORDION_LABELER.vedlegg} tittelSuffiks="— fast">
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
                  <Felt feltKey="innhold.vedlegg" label="Vedlegg (én pr. linje)">
                    <Textarea aria-label="Vedlegg (én pr. linje)" value={iVedlegg} onChange={(e) => setIVedlegg(e.target.value)} rows={3} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.vedleggMerknad" label="Merknad">
                    <Textfield aria-label="Merknad" value={iVedleggMerknad} onChange={(e) => setIVedleggMerknad(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                </div>
              </Accordion>
            );
          }

          if (nokkel === 'opplysninger') {
            return (
              <Accordion key={nokkel} apen={apen} kanFlyttes={kanFlyttes} onToggle={(v) => settAccordionApen(nokkel, v)}
                onFlytt={flytt} tittel={ACCORDION_LABELER.opplysninger} tittelSuffiks="— fast">
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
                  <Felt feltKey="innhold.opplysningerSomSkalSendesInn" label="Opplysninger som skal sendes inn (én pr. linje)">
                    <Textarea aria-label="Opplysninger som skal sendes inn (én pr. linje)" value={iOpplysninger} onChange={(e) => setIOpplysninger(e.target.value)} rows={3} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.opplysningerMerknad" label="Merknad">
                    <Textfield aria-label="Merknad" value={iOpplysningerMerknad} onChange={(e) => setIOpplysningerMerknad(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                </div>
              </Accordion>
            );
          }

          if (nokkel === 'veiledning') {
            return (
              <Accordion key={nokkel} apen={apen} kanFlyttes={kanFlyttes} onToggle={(v) => settAccordionApen(nokkel, v)}
                onFlytt={flytt} tittel={ACCORDION_LABELER.veiledning} tittelSuffiks="— fast">
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
                  <Felt feltKey="innhold.veiledningOgUtfylling" label="Veiledningspunkter (én pr. linje)">
                    <Textarea aria-label="Veiledningspunkter (én pr. linje)" value={iVeiledning} onChange={(e) => setIVeiledning(e.target.value)} rows={3} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.veiledningMerknad" label="Merknad">
                    <Textfield aria-label="Merknad" value={iVeiledningMerknad} onChange={(e) => setIVeiledningMerknad(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                </div>
              </Accordion>
            );
          }

          if (nokkel === 'innsending') {
            return (
              <Accordion key={nokkel} apen={apen} kanFlyttes={kanFlyttes} onToggle={(v) => settAccordionApen(nokkel, v)}
                onFlytt={flytt} tittel={ACCORDION_LABELER.innsending} tittelSuffiks="— fast">
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
                  <Felt feltKey="innhold.innsendingOgOppfolging.kanal" label="Kanal">
                    <Textfield aria-label="Kanal" value={iInnsendingKanal} onChange={(e) => setIInnsendingKanal(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.innsendingOgOppfolging.etterMottak" label="Etter mottak (én pr. linje)">
                    <Textarea aria-label="Etter mottak (én pr. linje)" value={iInnsendingEtterMottak} onChange={(e) => setIInnsendingEtterMottak(e.target.value)} rows={3} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.innsendingOgOppfolging.merknad" label="Merknad">
                    <Textfield aria-label="Merknad" value={iInnsendingMerknad} onChange={(e) => setIInnsendingMerknad(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                </div>
              </Accordion>
            );
          }

          if (nokkel === 'kontakt') {
            return (
              <Accordion key={nokkel} apen={apen} kanFlyttes={kanFlyttes} onToggle={(v) => settAccordionApen(nokkel, v)}
                onFlytt={flytt} tittel={ACCORDION_LABELER.kontakt} tittelSuffiks="— fast">
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
                  <Felt feltKey="innhold.kontaktOgHjelp.generelt" label="Generelt">
                    <Textfield aria-label="Generelt" value={iKontaktGenerelt} onChange={(e) => setIKontaktGenerelt(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.kontaktOgHjelp.kommunenKanVeiledeOm" label="Kommunen kan veilede om (én pr. linje)">
                    <Textarea aria-label="Kommunen kan veilede om (én pr. linje)" value={iKontaktKommunenKanVeiledeOm} onChange={(e) => setIKontaktKommunenKanVeiledeOm(e.target.value)} rows={3} style={{ width: '100%' }} />
                  </Felt>
                </div>
              </Accordion>
            );
          }

          if (nokkel === 'innebaerer') {
            return (
              <Accordion key={nokkel} apen={apen} kanFlyttes={kanFlyttes} onToggle={(v) => settAccordionApen(nokkel, v)}
                onFlytt={flytt} tittel={ACCORDION_LABELER.innebaerer} tittelSuffiks="— fast">
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.innledning" label="Innledning">
                    <Textarea aria-label="Innledning" value={iHviInnledning} onChange={(e) => setIHviInnledning(e.target.value)} rows={2} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.varighet" label="Varighet">
                    <Textfield aria-label="Varighet" value={iHviVarighet} onChange={(e) => setIHviVarighet(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.plikter" label="Plikter (én pr. linje)">
                    <Textarea aria-label="Plikter (én pr. linje)" value={iHviPlikter} onChange={(e) => setIHviPlikter(e.target.value)} rows={2} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.endringerIVirksomheten.plikt" label="Endringer i virksomheten — plikt">
                    <Textfield aria-label="Endringer i virksomheten — plikt" value={iHviEndringerPlikt} onChange={(e) => setIHviEndringerPlikt(e.target.value)} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.endringerIVirksomheten.eksempler" label="Endringer i virksomheten — eksempler (én pr. linje)">
                    <Textarea aria-label="Endringer i virksomheten — eksempler (én pr. linje)" value={iHviEndringerEksempler} onChange={(e) => setIHviEndringerEksempler(e.target.value)} rows={2} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.kravTilDrift" label="Krav til drift">
                    <Textarea aria-label="Krav til drift" value={iHviKravTilDrift} onChange={(e) => setIHviKravTilDrift(e.target.value)} rows={2} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.tommeavtaleOgKontroll" label="Tømmeavtale og kontroll">
                    <Textarea aria-label="Tømmeavtale og kontroll" value={iHviTommeavtaleOgKontroll} onChange={(e) => setIHviTommeavtaleOgKontroll(e.target.value)} rows={2} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.rapportering" label="Rapportering">
                    <Textarea aria-label="Rapportering" value={iHviRapportering} onChange={(e) => setIHviRapportering(e.target.value)} rows={2} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.kontrollOgTilsyn" label="Kontroll og tilsyn">
                    <Textarea aria-label="Kontroll og tilsyn" value={iHviKontrollOgTilsyn} onChange={(e) => setIHviKontrollOgTilsyn(e.target.value)} rows={2} style={{ width: '100%' }} />
                  </Felt>
                  <Felt feltKey="innhold.hvaRettighetenInnebarer.avgrensningMerknad" label="Avgrensning/merknad">
                    <Textarea aria-label="Avgrensning/merknad" value={iHviAvgrensningMerknad} onChange={(e) => setIHviAvgrensningMerknad(e.target.value)} rows={2} style={{ width: '100%' }} />
                  </Felt>
                </div>
              </Accordion>
            );
          }

          return null;
        })}

        {egne.map((element, idx) => (
          <Accordion key={element.id} apen={!!egneApne[element.id]}
            kanFlyttes={{ opp: idx > 0, ned: idx < egne.length - 1 }}
            onToggle={(v) => setEgneApne((forrige) => ({ ...forrige, [element.id]: v }))}
            onFlytt={(retning) => flyttEgetInnholdselement(element.id, retning)}
            tittel={element.tittel} onTittelChange={(t) => oppdaterEgetInnholdselement(element.id, { tittel: t })}
            onFjern={() => fjernEgetInnholdselement(element.id)}>
            <Felt feltKey={feltnokkelForEgetInnhold(element.id)} label="Fritekst">
              <Textarea aria-label="Fritekst" value={element.tekst ?? ''} onChange={(e) => oppdaterEgetInnholdselement(element.id, { tekst: e.target.value })}
                rows={3} style={{ width: '100%' }} placeholder="Skriv innhold …" />
            </Felt>
          </Accordion>
        ))}

        <Button type="button" variant="secondary" data-size="sm" onClick={leggTilEgetInnholdselement} style={{ marginTop: '0.3rem' }}>
          + Legg til eget innholdselement
        </Button>
      </div>
    </Ctx.Provider>
  );
}
