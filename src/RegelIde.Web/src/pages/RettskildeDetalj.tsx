import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate, useParams, useSearchParams } from 'react-router';
import { Alert, Button, Field, Heading, Label, Link, Paragraph, Select, Spinner, Switch, Table, Tabs, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type {
  DokumentReferanseDto,
  HandbokRettskildeomfangDto,
  NettsideLenkeMedMalDto,
  NettsideStiDto,
  RettskildeDetalj as RettskildeDetaljType,
  RettskildeEndringDto,
  RettskildeHjemletForDto,
  RettskildeHjemmelDto,
  RettskildeNodeDto,
  RettskildeReferanseDto,
  RettskildeSammendrag,
  TekstTaggDto,
  TjenesteReferanseDto,
} from '../api/types';
import { TagTekst, type Registry, type TagKindId, type TextTag } from '../tagging/TagTekst';
import { RettskildeTre, type RettskildeNode as TreNodeVm } from '../tre/RettskildeTre';
import { KommentarRedigering } from '../handbok/KommentarRedigering';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';
import { useKonfigurasjon } from '../konfigurasjon/KonfigurasjonContext';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { RaaTekstMedLenker } from '../rettskilde/RaaTekstMedLenker';
import { eidVisningstekst, finnRettskildeForEid, rettskildeLenke } from '../api/eidLenker';
import { KontekstPanel, type KontekstPanelGruppe } from '../entitet/KontekstPanel';

const STITYPE_FARGE: Record<string, 'info' | 'success'> = { tematisk: 'info', organisatorisk: 'success' };

type Fane = 'metadata' | 'innhold' | 'relasjoner' | 'handbok';
const FANE_LABELER: Record<Fane, string> = {
  metadata: 'Metadata', innhold: 'Innhold & tagging', relasjoner: 'Relasjoner', handbok: 'Håndbok',
};

interface TreNode extends RettskildeNodeDto {
  barn: TreNode[];
}

function byggTre(noder: RettskildeNodeDto[]): TreNode[] {
  const perId = new Map<string, TreNode>(noder.map((n) => [n.id, { ...n, barn: [] }]));
  const rotnoder: TreNode[] = [];
  for (const node of perId.values()) {
    if (node.parentNodeId && perId.has(node.parentNodeId)) {
      perId.get(node.parentNodeId)!.barn.push(node);
    } else {
      rotnoder.push(node);
    }
  }
  return rotnoder;
}

/**
 * Bygger om vårt flate/nøstede nodetre til RettskildeTre sin form.
 * `antallKommentarer` gjenbrukes her som antall EGNE tekst-tagger på noden
 * (ikke håndbok-kommentarer, som ikke finnes ennå) — komponentens generiske
 * "tellemerke uten status"-visning passer fint til det inntil håndboken
 * finnes, siden `kommentarStatus` da alltid er undefined for oss.
 */
function tilTreVm(noder: TreNode[], taggAntallPerNode: Map<string, number>): TreNodeVm[] {
  return noder.map((n) => ({
    eId: n.eid,
    nodeType: n.nodeType as TreNodeVm['nodeType'],
    merke: n.nummer ?? '',
    tittel: n.overskrift ?? undefined,
    tekst: n.tekst ?? undefined,
    opphevet: n.opphevet,
    antallKommentarer: taggAntallPerNode.get(n.eid),
    children: n.barn.length > 0 ? tilTreVm(n.barn, taggAntallPerNode) : undefined,
  }));
}

function finnNode(noder: TreNode[], eid: string): TreNode | null {
  for (const n of noder) {
    if (n.eid === eid) return n;
    const funnet = finnNode(n.barn, eid);
    if (funnet) return funnet;
  }
  return null;
}

/**
 * Finner eId-ene til alle FORFEDRE av `eid` i treet (kapittel/underinndeling/paragraf-nivåene som må
 * være åpne for at `eid` skal være synlig) — punkt 2, rettskildedetalj-fikser 2026-09-02. Ekskluderer
 * selve `eid` (den trenger ikke være "åpen" for å være synlig, kun sine forfedre). Returnerer `null`
 * når `eid` ikke finnes i treet ennå (data ikke lastet, eller ukjent eId) — kalleren faller da tilbake
 * til en tom liste, «ingen gjettet fallback».
 */
function finnForfedrePath(noder: TreNode[], eid: string, sti: string[] = []): string[] | null {
  for (const n of noder) {
    if (n.eid === eid) return sti;
    const funnet = finnForfedrePath(n.barn, eid, [...sti, n.eid]);
    if (funnet) return funnet;
  }
  return null;
}

export default function RettskildeDetalj() {
  const { id } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { taggKinds } = useKonfigurasjon();
  const [detalj, setDetalj] = useState<RettskildeDetaljType | null>(null);
  const [tre, setTre] = useState<TreNode[] | null>(null);
  const [tagger, setTagger] = useState<TekstTaggDto[]>([]);
  const [activeKind, setActiveKind] = useState<string>('');
  const [selectedEid, setSelectedEid] = useState<string | undefined>(undefined);
  const [referertAvTjenester, setReferertAvTjenester] = useState<TjenesteReferanseDto[]>([]);
  // Punkt 6/9 (avklaringsrunde 2026-08-13) — motsatt retning av Referanser under, men fra ANDRE
  // dokumenters (håndbok/rundskriv) noder, ikke fra en Tjeneste.
  const [referertAvDokumenter, setReferertAvDokumenter] = useState<DokumentReferanseDto[]>([]);

  // Hjemmel (2026-08-30) — header-metadatafeltet <dt class="basedOn">, DOKUMENTNIVÅ og bevisst
  // atskilt fra Referanser under (som er per-node løpetekst). Trygt å hente for ALLE doctyper/
  // kildetyper (serveren returnerer tom liste når feltet mangler, bekreftet KUN forskrifter har det i
  // dag) — samme "safe empty"-mønster som referertAvTjenester/-Dokumenter.
  const [hjemler, setHjemler] = useState<RettskildeHjemmelDto[]>([]);
  // Motsatt retning — kun ikke-tom for en LOV noe faktisk er hjemlet i.
  const [hjemletFor, setHjemletFor] = useState<RettskildeHjemletForDto[]>([]);

  // «Endrer» (rettskildedetalj-fikser, 2026-09-02, punkt 5) — header-metadatafeltet <dt
  // class="changesToDocuments">Endrer</dt>, hvilke(t) andre dokument(er) DENNE rettskilden endrer.
  // DOKUMENTNIVÅ, samme "safe empty"-mønster som hjemler/hjemletFor over.
  const [endringer, setEndringer] = useState<RettskildeEndringDto[]>([]);

  // Punkt 8 — kun ikke-tomme for kildetype='Brukerveiledning' (§3.4/§3.2). Trygt å hente for ALLE
  // doctyper (serveren returnerer tom liste for enhver annen), samme "safe empty"-mønster som
  // referertAvTjenester/-Dokumenter over.
  const [nettsideStier, setNettsideStier] = useState<NettsideStiDto[]>([]);
  const [nettsideLenker, setNettsideLenker] = useState<NettsideLenkeMedMalDto[]>([]);
  const [filter, setFilter] = useState('');
  const [visAknXml, setVisAknXml] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [taggFeil, setTaggFeil] = useState<string | null>(null);

  // Fanevalg (docs/30 §4 punkt 2 — saksbehandlertilpasningen 2026-09-02): Metadata / Innhold & tagging
  // / Relasjoner / Håndbok (sistnevnte kun for doctype='doc') — erstatter den tidligere endimensjonale
  // stabelen av <section>-er. INGEN endring i selve datainnhentingen/-logikken under, kun i hvordan den
  // vises.
  // Standard-fane «innhold» (rettskildedetalj-fikser, 2026-09-02, punkt 1) — de aller fleste brukere
  // vil se selve lovteksten først, ikke Metadata-fanen (Johanns live-testfunn 1).
  const [fane, setFane] = useState<Fane>('innhold');

  // Håndbok-forfatterflyt (2026-07-26) — kun relevant når kildetype='Rundskriv', se render under.
  const [alleRettskilder, setAlleRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [visOpprettKapittel, setVisOpprettKapittel] = useState(false);
  const [nyKapittelNummer, setNyKapittelNummer] = useState('');
  const [nyKapittelOverskrift, setNyKapittelOverskrift] = useState('');
  const [kapittelFeil, setKapittelFeil] = useState<string | null>(null);
  const [kapittelLagrer, setKapittelLagrer] = useState(false);
  const [visOpprettKommentar, setVisOpprettKommentar] = useState(false);

  // «Opprett vilkår fra dette utdraget» (2026-07-31, docs/13-backlog.md §2.5) — tekst-først-
  // forfatterflyt: identifiser vilkåret i lovteksten via en umerket kind='vilkar'-tagg, opprett
  // vilkåret derfra (juridisk grunnlag forhåndsutfylt fra taggens node), koble taggen — UTEN å
  // samtidig plassere det i regelgrafen (det er et eget, senere steg, jf. Johanns egen presisering).
  const [opprettVilkarFraTaggId, setOpprettVilkarFraTaggId] = useState<string | null>(null);
  const [nyVilkarTittel, setNyVilkarTittel] = useState('');
  const [nyVilkarTjenesteId, setNyVilkarTjenesteId] = useState('');
  const [oppretterVilkarFraTag, setOppretterVilkarFraTag] = useState(false);

  // Referanser (2026-07-30) — kryssreferanser fra og til denne rettskilden, per node. Kilde-referanser
  // (Opprinnelse='import') er skrivebeskyttet; manuelle kan legges til/fjernes for enhver node.
  const [referanser, setReferanser] = useState<RettskildeReferanseDto[]>([]);
  const [nyReferanseRettskildeId, setNyReferanseRettskildeId] = useState('');
  const [nyReferanseEid, setNyReferanseEid] = useState('');
  const [leggerTilReferanse, setLeggerTilReferanse] = useState(false);
  const [referanseFeil, setReferanseFeil] = useState<string | null>(null);

  // Håndbok-nivå rettskildeomfang (2026-07-31) — hvilke rettskilder håndboken som HELHET omhandler,
  // distinkt fra Referanser over (som er per tekstavsnitt). Kun relevant når detalj.doctype === 'doc'.
  const [rettskildeomfang, setRettskildeomfang] = useState<HandbokRettskildeomfangDto[]>([]);
  const [nyOmfangRettskildeId, setNyOmfangRettskildeId] = useState('');
  const [leggerTilOmfang, setLeggerTilOmfang] = useState(false);
  const [omfangFeil, setOmfangFeil] = useState<string | null>(null);

  const { visEier } = useVirksomheter();

  // Metadata-redigering (2026-08-13, avklaringsrunde punkt 3) — Eli forblir ALLTID skrivebeskyttet
  // (kun vist, aldri i dette skjemaet); de seks andre feltene fantes allerede på entiteten, men var
  // ikke skrivbare noe sted i UI-et før nå.
  const [redigererMetadata, setRedigererMetadata] = useState(false);
  const [metaKortnavn, setMetaKortnavn] = useState('');
  const [metaUtgiver, setMetaUtgiver] = useState('');
  const [metaInterntDokNr, setMetaInterntDokNr] = useState('');
  const [metaRevisjonsnr, setMetaRevisjonsnr] = useState('');
  const [metaVedtattAv, setMetaVedtattAv] = useState('');
  const [metaVedtaksdato, setMetaVedtaksdato] = useState('');
  const [metaGyldigTil, setMetaGyldigTil] = useState('');
  const [metaKonsolidertDato, setMetaKonsolidertDato] = useState('');
  const [lagrerMetadata, setLagrerMetadata] = useState(false);
  const [metadataFeil, setMetadataFeil] = useState<string | null>(null);

  // Irrelevant-markering (2026-08-30) — header-nivå «marker som irrelevant for regel-ide» +
  // fritekstkommentar. Toggelen er ALLTID synlig (ikke bak en «rediger»-knapp slik Metadata-
  // seksjonen over er) — dette er én enkeltstående handling (sett/fjern markeringen), ikke en
  // flerfelts skjemaredigering som trenger et eget redigeringsmodus.
  const [irrelevantMarkert, setIrrelevantMarkert] = useState(false);
  const [irrelevantKommentar, setIrrelevantKommentar] = useState('');
  const [lagrerIrrelevant, setLagrerIrrelevant] = useState(false);
  const [irrelevantFeil, setIrrelevantFeil] = useState<string | null>(null);

  async function lagreIrrelevant() {
    if (!id) return;
    setIrrelevantFeil(null);
    setLagrerIrrelevant(true);
    try {
      const oppdatert = await api.oppdaterRettskildeIrrelevant(id, {
        erIrrelevant: irrelevantMarkert,
        irrelevantKommentar: irrelevantKommentar.trim() || null,
      });
      setDetalj(oppdatert);
    } catch (err) {
      setIrrelevantFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av irrelevant-markering.');
    } finally {
      setLagrerIrrelevant(false);
    }
  }

  // Navnekandidat-fiks 3, del 2 (2026-08-30, docs/13-backlog.md §9) — manuell sveip-trigger for NØYAKTIG
  // denne rettskilden. Fantes ikke noe sted i UI-et før nå (kun via NavnekandidaterListe.tsx sitt
  // korpusomfattende/rettskilde-filtrerte sveip-panel, ikke herfra) — brukere som nettopp har
  // (re)importert ELLER lagt merke til at en kjent lov mangler kandidater hadde ingen synlig måte å
  // trigge det på uten å bytte side og lete opp riktig rettskilde i en nedtrekksliste der.
  const [sveiper, setSveiper] = useState(false);
  const [sveipFeil, setSveipFeil] = useState<string | null>(null);
  const [sveipResultat, setSveipResultat] = useState<{ funnet: number; nye: number } | null>(null);

  async function kjorSveip() {
    if (!id) return;
    setSveiper(true);
    setSveipFeil(null);
    setSveipResultat(null);
    try {
      const resultat = await api.sveipNavnekandidater({ rettskildeId: id });
      setSveipResultat({ funnet: resultat.antallTreffFunnet, nye: resultat.antallNyeKandidater });
    } catch (err) {
      setSveipFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sveip.');
    } finally {
      setSveiper(false);
    }
  }

  function startRedigerMetadata() {
    if (!detalj) return;
    setMetaKortnavn(detalj.kortnavn ?? '');
    setMetaUtgiver(detalj.utgiver ?? '');
    setMetaInterntDokNr(detalj.interntDokNr ?? '');
    setMetaRevisjonsnr(detalj.revisjonsnr ?? '');
    setMetaVedtattAv(detalj.vedtattAv ?? '');
    setMetaVedtaksdato(detalj.vedtaksdato ?? '');
    setMetaGyldigTil(detalj.gyldigTil ?? '');
    setMetaKonsolidertDato(detalj.konsolidertDato ?? '');
    setMetadataFeil(null);
    setRedigererMetadata(true);
  }

  async function lagreMetadata(e: FormEvent) {
    e.preventDefault();
    if (!id) return;
    setMetadataFeil(null);
    setLagrerMetadata(true);
    try {
      const oppdatert = await api.oppdaterRettskildeMetadata(id, {
        kortnavn: metaKortnavn.trim() || null,
        utgiver: metaUtgiver.trim() || null,
        interntDokNr: metaInterntDokNr.trim() || null,
        revisjonsnr: metaRevisjonsnr.trim() || null,
        vedtattAv: metaVedtattAv.trim() || null,
        vedtaksdato: metaVedtaksdato || null,
        gyldigTil: metaGyldigTil || null,
        konsolidertDato: metaKonsolidertDato || null,
      });
      setDetalj(oppdatert);
      setRedigererMetadata(false);
    } catch (err) {
      setMetadataFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av metadata.');
    } finally {
      setLagrerMetadata(false);
    }
  }

  useEffect(() => {
    if (!activeKind && taggKinds.length > 0) setActiveKind(taggKinds[0].id);
  }, [taggKinds, activeKind]);

  useEffect(() => {
    if (!id) return;
    setFeil(null);
    setDetalj(null);
    setTre(null);
    setSelectedEid(undefined);
    Promise.all([api.hentRettskilde(id), api.hentNoder(id), api.hentTagger(id)])
      .then(([d, noder, egneTagger]) => {
        setDetalj(d);
        setTre(byggTre(noder));
        setTagger(egneTagger);
        setIrrelevantMarkert(d.erIrrelevant);
        setIrrelevantKommentar(d.irrelevantKommentar ?? '');
      })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av rettskilden.'));
    api.hentReferertAvTjenester(id).then(setReferertAvTjenester).catch(() => setReferertAvTjenester([]));
    api.hentReferertAvDokumenter(id).then(setReferertAvDokumenter).catch(() => setReferertAvDokumenter([]));
    api.hentReferanser(id).then(setReferanser).catch(() => setReferanser([]));
    api.hentHjemmel(id).then(setHjemler).catch(() => setHjemler([]));
    api.hentHjemmelFor(id).then(setHjemletFor).catch(() => setHjemletFor([]));
    api.hentRettskildeEndringer(id).then(setEndringer).catch(() => setEndringer([]));
    api.hentRettskildeStier(id).then(setNettsideStier).catch(() => setNettsideStier([]));
    api.hentRettskildeNettsideLenker(id).then(setNettsideLenker).catch(() => setNettsideLenker([]));
    api.hentHandbokRettskildeomfang(id).then(setRettskildeomfang).catch(() => setRettskildeomfang([]));
  }, [id]);

  async function leggTilOmfang() {
    if (!id || !nyOmfangRettskildeId) return;
    setOmfangFeil(null);
    setLeggerTilOmfang(true);
    try {
      const nytt = await api.leggTilHandbokRettskildeomfang(id, nyOmfangRettskildeId);
      setRettskildeomfang((forrige) => [...forrige, nytt]);
      setNyOmfangRettskildeId('');
    } catch (err) {
      setOmfangFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av rettskildeomfang.');
    } finally {
      setLeggerTilOmfang(false);
    }
  }

  async function fjernOmfang(omfangId: string) {
    if (!id) return;
    await api.fjernHandbokRettskildeomfang(id, omfangId);
    setRettskildeomfang((forrige) => forrige.filter((o) => o.id !== omfangId));
  }

  // Egen effekt for ?eid= (i stedet for kun å lese den én gang inni data-lastingen over) — en lenke
  // KAN peke til en annen node i SAMME rettskilde (typisk: en intern kryssreferanse i løpeteksten,
  // 2026-07-30), og da endres kun søkeparameteren, ikke `id` — uten denne egne effekten ville
  // navigasjonen "henge fast" på forrige valgte node siden data-lastingseffekten over ikke kjører på nytt.
  useEffect(() => {
    const eidFraLenke = searchParams.get('eid');
    if (eidFraLenke) setSelectedEid(eidFraLenke);
  }, [searchParams]);

  // «alleRettskilder» dekker nå både håndbok-lovreferanser og den generelle Referanser-seksjonen —
  // derfor hentet uavhengig av kildetype (var tidligere kun for Rundskriv).
  useEffect(() => {
    api.hentRettskilder().then(setAlleRettskilder).catch(() => setAlleRettskilder([]));
  }, []);

  // Registry for «Koble til …»-handlingen i tagg-listen (byggesteg 2) — kandidater for kind='begrep'/'tjeneste'.
  const [registry, setRegistry] = useState<Registry>({});
  // Kobler-visning for allerede-koblede tagger («resolveRef») — 2026-07-30, se TagTekst.tsx.
  const [begrepPerId, setBegrepPerId] = useState<Map<string, string>>(new Map());
  const [tjenestePerId, setTjenestePerId] = useState<Map<string, string>>(new Map());
  const [vilkarPerId, setVilkarPerId] = useState<Map<string, string>>(new Map());
  const [regelnodePerId, setRegelnodePerId] = useState<Map<string, string>>(new Map());
  const [rotnodeId, setRotnodeId] = useState<string | undefined>(undefined);
  useEffect(() => {
    Promise.all([api.hentBegreper(), api.hentTjenester(), api.hentVilkarListe(), api.hentRegelnodeListe()])
      .then(([begreper, tjenester, vilkarListe, regelnoder]) => {
        setRegistry({
          begrep: begreper.map((b) => ({ ref: b.id, label: b.term })),
          tjeneste: tjenester.map((t) => ({ ref: t.id, label: t.tittel })),
        });
        setBegrepPerId(new Map(begreper.map((b) => [b.id, b.term])));
        setTjenestePerId(new Map(tjenester.map((t) => [t.id, t.tittel])));
        setVilkarPerId(new Map(vilkarListe.map((v) => [v.id, v.tittel])));
        setRegelnodePerId(new Map(regelnoder.map((r) => [r.id, r.tittel])));
        // Bevisst forenkling (kun ett vilkårstre finnes i dag) — se plan «Sammenhengende navigasjon».
        setRotnodeId(tjenester.find((t) => t.rotnodeId)?.rotnodeId ?? undefined);
      })
      .catch(() => setRegistry({})); // ingen bruker valgt ennå e.l. — «Koble til» skjules bare
  }, []);

  // Referanse-kandidater for håndbok-kommentarers MinimalEditor, ut over rettskilder (2026-07-31,
  // docs/13-backlog.md §2.4) — samme Vilkår/Tjeneste-registre som resolveRef under bruker, bare snudd
  // om til {id,label}-listeform. Kun en typet peker (kind+id), ingen tekst-fletting.
  const alleVilkarForReferanse = useMemo(
    () => [...vilkarPerId.entries()].map(([id, label]) => ({ id, label })),
    [vilkarPerId],
  );
  const alleTjenesterForReferanse = useMemo(
    () => [...tjenestePerId.entries()].map(([id, label]) => ({ id, label })),
    [tjenestePerId],
  );

  function resolveRef(kind: TagKindId, ref: string): { label: string; href: string } | undefined {
    if (kind === 'begrep' && begrepPerId.has(ref)) return { label: begrepPerId.get(ref)!, href: `/begreper/${ref}` };
    if (kind === 'tjeneste' && tjenestePerId.has(ref)) return { label: tjenestePerId.get(ref)!, href: `/tjenester/${ref}` };
    if (kind === 'vilkar' && vilkarPerId.has(ref) && rotnodeId) {
      return { label: vilkarPerId.get(ref)!, href: `/vilkarstre/${rotnodeId}?fokusVilkar=${ref}` };
    }
    if (kind === 'regel' && regelnodePerId.has(ref) && rotnodeId) {
      return { label: regelnodePerId.get(ref)!, href: `/vilkarstre/${rotnodeId}?fokusVilkar=${ref}` };
    }
    return undefined;
  }

  async function handleKobleTag(taggId: string, ref: string) {
    if (!id) return;
    setTaggFeil(null);
    try {
      const oppdatert = await api.kobleTaggTilEntitet(id, taggId, { refId: ref });
      setTagger((forrige) => forrige.map((t) => (t.id === taggId ? oppdatert : t)));
    } catch (e) {
      setTaggFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved kobling av tagg.');
    }
  }

  /** Åpner skjemaet for «opprett vilkår fra dette utdraget» — tittel forhåndsutfylt fra selve sitatet. */
  function startOpprettVilkarFraTag(taggId: string) {
    const tagg = tagger.find((t) => t.id === taggId);
    setOpprettVilkarFraTaggId(taggId);
    setNyVilkarTittel(tagg?.quoteExact.slice(0, 80) ?? '');
    setNyVilkarTjenesteId('');
    setTaggFeil(null);
  }

  /**
   * Oppretter Vilkåret (juridisk grunnlag forhåndsutfylt fra rettskilden + taggens node) og kobler
   * umiddelbart den umerkede taggen til det — bevisst UTEN å plassere det i regelgrafen (Johanns egen
   * presisering, docs/13-backlog.md §2.5: identifisere og bygge treet er to separate, senere steg).
   */
  async function opprettVilkarFraTag(e: FormEvent) {
    e.preventDefault();
    if (!id || !opprettVilkarFraTaggId || !nyVilkarTittel.trim() || !nyVilkarTjenesteId) return;
    const tagg = tagger.find((t) => t.id === opprettVilkarFraTaggId);
    if (!tagg) return;
    setTaggFeil(null);
    setOppretterVilkarFraTag(true);
    try {
      const nyttVilkar = await api.opprettVilkar({
        tittel: nyVilkarTittel.trim(), beskrivelse: null, generiskMal: null, vilkarstype: 'formell',
        gjelderRolle: null,
        juridiskGrunnlag: detalj ? [{ kilde: detalj.kortnavn ?? detalj.tittel, eId: tagg.nodeEid }] : null,
        begrepId: null, vurderingstype: 'regelbasert', parametreJson: null, skjonnsgrunnlagBegrepId: null,
        skjonnsmomenter: null, kreverDokumentasjon: false, eskaleringsrolle: null, veiledningTilBruker: null,
        veiledningTilSaksbehandler: null, erFormel: false, formelBeskrivelse: null,
        tjenesteId: nyVilkarTjenesteId,
      });
      const oppdatertTagg = await api.kobleTaggTilEntitet(id, opprettVilkarFraTaggId, { refId: nyttVilkar.id });
      setTagger((forrige) => forrige.map((t) => (t.id === oppdatertTagg.id ? oppdatertTagg : t)));
      setVilkarPerId((forrige) => new Map(forrige).set(nyttVilkar.id, nyttVilkar.tittel));
      setOpprettVilkarFraTaggId(null);
      setNyVilkarTittel('');
      setNyVilkarTjenesteId('');
    } catch (err) {
      setTaggFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av vilkår fra tagg.');
    } finally {
      setOppretterVilkarFraTag(false);
    }
  }

  async function refetchNoder(velgEid?: string) {
    if (!id) return;
    const noder = await api.hentNoder(id);
    setTre(byggTre(noder));
    if (velgEid) setSelectedEid(velgEid);
  }

  async function opprettKapittel(parentNodeId: string | null) {
    if (!id || !nyKapittelNummer.trim()) return;
    setKapittelFeil(null);
    setKapittelLagrer(true);
    try {
      const kapittel = await api.opprettKapittelNode(id, {
        parentNodeId,
        nummer: nyKapittelNummer.trim(),
        overskrift: nyKapittelOverskrift.trim() || null,
      });
      setNyKapittelNummer('');
      setNyKapittelOverskrift('');
      setVisOpprettKapittel(false);
      await refetchNoder(kapittel.eid);
    } catch (err) {
      setKapittelFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av kapittel.');
    } finally {
      setKapittelLagrer(false);
    }
  }

  const taggerPerNode = useMemo(() => {
    const kart = new Map<string, TextTag[]>();
    for (const t of tagger) {
      const liste = kart.get(t.nodeEid) ?? [];
      liste.push({ id: t.id, start: t.startOffset, end: t.endOffset, kind: t.kind, ref: t.refId, kreverGjennomgang: t.kreverGjennomgang });
      kart.set(t.nodeEid, liste);
    }
    return kart;
  }, [tagger]);

  const taggAntallPerNode = useMemo(() => {
    const kart = new Map<string, number>();
    for (const t of tagger) kart.set(t.nodeEid, (kart.get(t.nodeEid) ?? 0) + 1);
    return kart;
  }, [tagger]);

  const treVm = useMemo(() => (tre ? tilTreVm(tre, taggAntallPerNode) : []), [tre, taggAntallPerNode]);
  const valgtNode = useMemo(() => (tre && selectedEid ? finnNode(tre, selectedEid) : null), [tre, selectedEid]);

  // Punkt 2 (rettskildedetalj-fikser, 2026-09-02) — dyplenke via ?eid= skal åpne strukturen rundt
  // treffet, ikke bare velge det. `eidFraUrl` (i motsetning til `selectedEid`) endres KUN ved reell
  // navigasjon (URL-en, ikke brukerens manuelle klikk i treet — onSelect kaller kun setSelectedEid,
  // aldri setSearchParams) — brukt som `key` på RettskildeTre under slik at treet kun remountes (og
  // dermed re-initialiserer sin expanded-tilstand fra defaultExpanded) ved en faktisk dyplenke-
  // navigasjon, ikke ved vanlig browsing i treet.
  const eidFraUrl = searchParams.get('eid') ?? undefined;
  const forfedrePath = useMemo(
    () => (tre && eidFraUrl ? (finnForfedrePath(tre, eidFraUrl) ?? []) : []),
    [tre, eidFraUrl],
  );

  // Lesbar visning av "Referert fra håndbøker/andre dokumenter"s TilEid (funn 2, avklaringsrunde
  // 2026-08-13) — TilEid peker alltid på en node i DENNE rettskilden (det er nettopp derfor raden
  // dukker opp her), så vi slår den opp mot vår egen, allerede lastede nodeliste (flatet ut fra
  // treet) og gjenbruker eidVisningstekst-mønsteret fra TjenesteDetalj.tsx i stedet for å vise den
  // rå eId-kjeden. Faller ALLTID tilbake til rå eId når noden ikke er funnet ennå — «ingen gjettet
  // fallback».
  const detaljNoderPerRettskilde = useMemo(() => {
    const kart = new Map<string, RettskildeNodeDto[]>();
    if (!detalj || !tre) return kart;
    const flate: RettskildeNodeDto[] = [];
    function samle(noder: TreNode[]) {
      for (const n of noder) {
        flate.push(n);
        samle(n.barn);
      }
    }
    samle(tre);
    kart.set(detalj.id, flate);
    return kart;
  }, [detalj, tre]);
  const detaljSomSammendrag = useMemo(
    () => (detalj ? [{
      id: detalj.id, virksomhetId: detalj.virksomhetId, eli: detalj.eli, tittel: detalj.tittel,
      kortnavn: detalj.kortnavn, kildetype: detalj.kildetype, ansvarligDepartement: detalj.ansvarligDepartement,
      erIrrelevant: detalj.erIrrelevant, irrelevantKommentar: detalj.irrelevantKommentar, ikrafttredelseRaa: null,
    }] : []),
    [detalj],
  );
  function tilEidVisning(tilEid: string): string {
    return eidVisningstekst(tilEid, detaljSomSammendrag, detaljNoderPerRettskilde) ?? tilEid;
  }

  // Innebygde referanse-lenker i selve løpeteksten (2026-07-30) — samme kilde som "Referanser"-
  // seksjonen under, men kun de med en kjent tekstposisjon (import-referanser der parseren fant et
  // entydig treff; manuelle referanser har ingen posisjon og vises kun i lista under).
  const inlineReferanser = useMemo(
    () =>
      valgtNode
        ? referanser
            .filter((r) => r.fraNodeId === valgtNode.id && r.tekstStart != null && r.tekstLengde != null)
            .map((r) => ({
              start: r.tekstStart!,
              end: r.tekstStart! + r.tekstLengde!,
              href: `/rettskilder/${r.tilRettskildeId}?eid=${encodeURIComponent(r.tilEid)}`,
            }))
        : [],
    [referanser, valgtNode],
  );

  /** eId-prefiksmatch — en referanse til f.eks. "…/§4-1/ledd-2" skal fortsatt vises på §4-1-noden. */
  function eidMatcherNode(tilEid: string, nodeEid: string): boolean {
    return tilEid === nodeEid || tilEid.startsWith(`${nodeEid}/`);
  }

  // Punkt 6/9 — samme "referert av"-koblinger som de globale listene lenger opp, men filtrert til
  // NØYAKTIG den valgte noden, slik at de vises rett ved siden av teksten de faktisk peker på.
  const referertAvTjenesterForNode = useMemo(
    () => (valgtNode ? referertAvTjenester.filter((r) => eidMatcherNode(r.tilEid, valgtNode.eid)) : []),
    [referertAvTjenester, valgtNode],
  );
  const referertAvDokumenterForNode = useMemo(
    () => (valgtNode ? referertAvDokumenter.filter((r) => eidMatcherNode(r.tilEid, valgtNode.eid)) : []),
    [referertAvDokumenter, valgtNode],
  );

  async function handleTag(
    nodeEid: string,
    nodeTekst: string,
    t: { start: number; end: number; kind: string; ref: string | null },
  ) {
    if (!id) return;
    setTaggFeil(null);
    try {
      const nyTagg = await api.opprettTagg(id, {
        nodeEid,
        startOffset: t.start,
        endOffset: t.end,
        quotePrefix: nodeTekst.slice(Math.max(0, t.start - 30), t.start),
        quoteExact: nodeTekst.slice(t.start, t.end),
        quoteSuffix: nodeTekst.slice(t.end, t.end + 30),
        kind: t.kind as TekstTaggDto['kind'],
      });
      setTagger((forrige) => [...forrige, nyTagg]);
    } catch (e) {
      setTaggFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved opprettelse av tagg.');
    }
  }

  async function leggTilReferanse(e: FormEvent) {
    e.preventDefault();
    if (!id || !valgtNode || !nyReferanseRettskildeId || !nyReferanseEid.trim()) return;
    setReferanseFeil(null);
    setLeggerTilReferanse(true);
    try {
      const ny = await api.opprettNodeReferanse(id, valgtNode.id, {
        tilRettskildeId: nyReferanseRettskildeId, tilEid: nyReferanseEid.trim(),
      });
      setReferanser((forrige) => [...forrige, ny]);
      setNyReferanseEid('');
    } catch (err) {
      setReferanseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av referanse.');
    } finally {
      setLeggerTilReferanse(false);
    }
  }

  async function fjernReferanse(referanseId: string) {
    if (!id) return;
    setReferanseFeil(null);
    try {
      await api.fjernNodeReferanse(id, referanseId);
      setReferanser((forrige) => forrige.filter((r) => r.id !== referanseId));
    } catch (err) {
      setReferanseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved fjerning av referanse.');
    }
  }

  async function handleSlett(taggId: string) {
    if (!id) return;
    setTaggFeil(null);
    try {
      await api.slettTagg(id, taggId);
      setTagger((forrige) => forrige.filter((t) => t.id !== taggId));
    } catch (e) {
      setTaggFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved fjerning av tagg.');
    }
  }

  // Konsolidert kontekstpanel (docs/30 §4 punkt 2) — Hjemmel/Hjemmel for/Brukt i tjenester/Referert fra
  // håndbøker var tidligere fem separate <section>-er nedover siden; samles nå i ÉN "Relasjoner"-fane
  // via samme delte KontekstPanel-komponent Tjeneste-siden bruker (her i variant="full", siden denne
  // siden ikke har et permanent sidepanel — se KontekstPanel.tsx). Et klikk på en rad NAVIGERER direkte
  // (samme mål som de opprinnelige <Link>-radene pekte på), det finnes ingen "Detaljer"-forhåndsvisning
  // å bytte til her.
  const kontekstGrupper: KontekstPanelGruppe[] = [
    {
      heading: 'Hjemmel',
      items: hjemler.map((h) => {
        const lov = finnRettskildeForEid(h.hjemmelEid, alleRettskilder);
        const paragraf = h.hjemmelEid.slice(h.hjemmelEid.lastIndexOf('/') + 1);
        const tekst = lov ? `${lov.kortnavn ?? lov.tittel} ${paragraf}` : h.hjemmelEid;
        const lenke = rettskildeLenke(h.hjemmelEid, alleRettskilder);
        return { key: h.id, label: tekst, onClick: () => { if (lenke) navigate(lenke); } };
      }),
    },
    {
      heading: 'Hjemmel for',
      items: hjemletFor.map((h) => ({
        key: `${h.forskriftId}-${h.hjemmelEid}`,
        label: `${h.forskriftTittel} — ${h.hjemmelEid.slice(h.hjemmelEid.lastIndexOf('/') + 1)}`,
        onClick: () => navigate(`/rettskilder/${h.forskriftId}`),
      })),
    },
    {
      heading: 'Brukt i tjenester',
      items: referertAvTjenester.map((r) => ({
        key: `${r.tjenesteId}-${r.tilEid}`,
        label: `${r.tjenesteTittel} (${r.tilEid.slice(r.tilEid.lastIndexOf('/') + 1)})`,
        onClick: () => navigate(`/tjenester/${r.tjenesteId}`),
      })),
    },
    {
      heading: 'Referert fra håndbøker/andre dokumenter',
      items: referertAvDokumenter.map((r) => ({
        key: `${r.dokumentId}-${r.fraNodeEid}-${r.tilEid}`,
        label: `${r.dokumentTittel}${r.fraNodeOverskrift ? ` — ${r.fraNodeOverskrift}` : ''} → ${tilEidVisning(r.tilEid)}`,
        onClick: () => navigate(`/rettskilder/${r.dokumentId}?eid=${encodeURIComponent(r.fraNodeEid)}`),
      })),
    },
  ];

  if (feil) return <Alert data-color="danger">{feil}</Alert>;
  if (!detalj) return <Spinner aria-label="Laster …" data-size="sm" />;

  const kanTagges = valgtNode && valgtNode.tekst && (valgtNode.nodeType === 'ledd' || valgtNode.nodeType === 'punkt');
  // Håndbok-/nettside-importerte "kapittel"-noder (HandbokImportTjeneste) kan ha EGEN løpetekst
  // direkte på kapittel-nivå (se HandbokNode.Tekst-kommentaren: "Kapittel 6/7/9/10 har HELE sin
  // tekst direkte på kapittel-nivå") — uten dette ville teksten vært usynlig i UI-et, ikke bare
  // ikke-taggbar, siden `kanTagges` bevisst er begrenset til ledd/punkt (den eneste tidligere
  // observerte bladnode-formen). Vises derfor som ren, ikke-taggbar tekst i stedet for feilaktig
  // "ingen egen løpetekst".
  const harIkkeTaggbarTekst = valgtNode && valgtNode.tekst && !kanTagges;
  const visHandbokFane = detalj.doctype === 'doc';

  // Punkt 6 (rettskildedetalj-fikser, 2026-09-02) — ikke bland Lovdata- og egendefinerte metadata uten
  // skille. Gjenbruker RettskildeEntitet.VirksomhetIds eget nasjonal/lokal-skille (se doc-kommentaren
  // der): NULL = delt/nasjonal (Lovdata-importert Lov/Forskrift), satt = virksomhetens egen lokale kilde.
  // `Eli` satt tas med som et ekstra, uavhengig signal på Lovdata-opprinnelse (samme «to tegn på samme
  // ting»-mønster instruksjonen selv nevner) — ingen ny distinksjon oppfunnet.
  const erLovdataImportert = detalj.eli != null || detalj.virksomhetId == null;
  const erVirksomhetsEgen = detalj.virksomhetId != null;

  return (
    <>
      <nav aria-label="Brødsmulesti" style={{ display: 'flex', gap: '0.4rem', fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.6rem', flexWrap: 'wrap' }}>
        <Link asChild><RouterLink to="/rettskilder">Rettskilder</RouterLink></Link>
        <span>/</span>
        <span style={{ color: 'var(--ds-color-neutral-text-default)' }}>{detalj.tittel}</span>
      </nav>

      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '1rem', marginBottom: '0.5rem', flexWrap: 'wrap' }}>
        <div>
          <Heading level={1} data-size="lg" style={{ margin: 0 }}>{detalj.tittel}</Heading>
          <Paragraph style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', margin: '0.5rem 0 0' }}>
            <Tag data-color="info" data-size="sm">{detalj.kildetype}</Tag>
            <Tag data-color={detalj.status === 'Gjeldende' ? 'success' : 'warning'} data-size="sm">{detalj.status}</Tag>
            <Tag data-color={detalj.virksomhetId ? 'success' : 'info'} data-size="sm">{visEier(detalj.virksomhetId)}</Tag>
            {detalj.erIrrelevant && <Tag data-color="warning" data-size="sm">Irrelevant for regel-ide</Tag>}
          </Paragraph>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', flex: '0 0 auto' }}>
          <Button data-size="sm" variant="secondary" onClick={kjorSveip} disabled={sveiper}>
            {sveiper ? 'Sveiper …' : 'Sveip etter navnekandidater'}
          </Button>
          <Button data-size="sm" variant="secondary" onClick={() => setVisAknXml((v) => !v)}>
            {visAknXml ? 'Skjul' : 'Vis'} AKN-XML
          </Button>
        </div>
      </div>

      {sveipFeil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{sveipFeil}</Alert>}
      {sveipResultat && (
        <Alert data-color="info" style={{ marginBottom: '1rem' }}>
          Fant {sveipResultat.funnet} treff, {sveipResultat.nye} nye kandidater lagt i køen.{' '}
          <Link asChild><RouterLink to={`/navnekandidater?rettskildeId=${id}`}>Se navnekandidater ↗</RouterLink></Link>
        </Alert>
      )}
      {visAknXml && (
        <pre style={{ overflow: 'auto', maxHeight: '400px', background: 'var(--ds-color-neutral-surface-tinted)', padding: '1rem', fontSize: 'var(--ds-font-size-1)', marginBottom: '1rem' }}>
          {detalj.aknXml}
        </pre>
      )}

      <Tabs value={fane} onChange={(v) => setFane(v as Fane)} style={{ marginBottom: '1rem' }}>
        <Tabs.List>
          <Tabs.Tab value="metadata">{FANE_LABELER.metadata}</Tabs.Tab>
          <Tabs.Tab value="innhold">{FANE_LABELER.innhold}</Tabs.Tab>
          <Tabs.Tab value="relasjoner">{FANE_LABELER.relasjoner}</Tabs.Tab>
          {visHandbokFane && <Tabs.Tab value="handbok">{FANE_LABELER.handbok}</Tabs.Tab>}
        </Tabs.List>
      </Tabs>

      {fane === 'metadata' && (
        <>
          <div
            style={{
              marginBottom: '1.5rem', padding: '0.75rem', borderRadius: 'var(--ds-border-radius-default)',
              border: '1px solid var(--ds-color-neutral-border-subtle)',
            }}
          >
            {detalj.erIrrelevant && (
              <Alert data-color="warning" style={{ marginBottom: '0.75rem' }}>
                Markert som irrelevant for regel-ide.{detalj.irrelevantKommentar ? ` ${detalj.irrelevantKommentar}` : ''}
              </Alert>
            )}
            <Switch
              label="Marker som irrelevant for regel-ide"
              checked={irrelevantMarkert}
              onChange={(e) => setIrrelevantMarkert(e.target.checked)}
            />
            {irrelevantMarkert && (
              <Field style={{ marginTop: '0.5rem', maxWidth: '40rem' }}>
                <Label>Kommentar (hvorfor er denne irrelevant?)</Label>
                <Textarea value={irrelevantKommentar} onChange={(e) => setIrrelevantKommentar(e.target.value)} rows={2} />
              </Field>
            )}
            <div style={{ marginTop: '0.5rem' }}>
              <Button data-size="sm" onClick={lagreIrrelevant} disabled={lagrerIrrelevant}>
                {lagrerIrrelevant ? 'Lagrer …' : 'Lagre'}
              </Button>
            </div>
            {irrelevantFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{irrelevantFeil}</Alert>}
          </div>

          {detalj.kildetype === 'Brukerveiledning' && (
            <div style={{ marginBottom: '1.5rem' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
                Navigasjonsstier
              </Heading>
              {detalj.url && (
                <Paragraph style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.5rem' }}>
                  <Link href={detalj.url} target="_blank" rel="noopener noreferrer">{detalj.url}</Link>
                </Paragraph>
              )}
              {nettsideStier.length === 0 ? (
                <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
                  Ingen kjent navigasjonssti for denne siden.
                </Paragraph>
              ) : (
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                  {nettsideStier.map((s, i) => (
                    <span key={i} style={{ display: 'flex', gap: '0.35rem', alignItems: 'center' }}>
                      <Tag data-color={STITYPE_FARGE[s.stiType] ?? 'neutral'} data-size="sm">{s.stiType}</Tag>
                      <span style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>{s.sti}</span>
                    </span>
                  ))}
                </div>
              )}
            </div>
          )}

          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
                Metadata
              </Heading>
              {!redigererMetadata && (
                <Button data-size="sm" variant="tertiary" onClick={startRedigerMetadata}>Rediger</Button>
              )}
            </div>

            {!redigererMetadata ? (
              <>
                {/* Funn 1 (avklaringsrunde 2026-08-13) — splittet i to grupper: «Fra Lovdata» (skrive-
                    beskyttet, kun populert for importerte Lov/Forskrift) og «Lokalt forvaltet» (redigerbar
                    via «Rediger», populert for lokalt forfattede/kunngjorte kilder som håndbøker). Punkt 6
                    (rettskildedetalj-fikser, 2026-09-02) — hver gruppe vises nå KUN når den faktisk er
                    relevant for DENNE rettskilden (erLovdataImportert/erVirksomhetsEgen), ikke begge
                    alltid — ikke bland Lovdata- og egendefinerte metadata uten skille. */}
                {erLovdataImportert && (
                  <>
                    <Heading level={3} data-size="2xs" style={{ margin: '0 0 0.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
                      Fra Lovdata
                    </Heading>
                    <Table style={{ marginBottom: '1rem' }}>
                      <Table.Body>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>ELI</Table.Cell>
                          <Table.Cell>{detalj.eli ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Kortnavn</Table.Cell>
                          <Table.Cell>{detalj.kortnavn ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Ikrafttredelse (rå)</Table.Cell>
                          <Table.Cell>{detalj.ikrafttredelseRaa ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Konsolidert dato</Table.Cell>
                          <Table.Cell>{detalj.konsolidertDato ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Konsolidert dato (rå)</Table.Cell>
                          <Table.Cell>{detalj.konsolidertDatoRaa ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Sist endret ved</Table.Cell>
                          <Table.Cell>{detalj.sistEndretVed ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Utgiver</Table.Cell>
                          <Table.Cell>{detalj.utgiver ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Ansvarlig departement</Table.Cell>
                          <Table.Cell>
                            {detalj.ansvarligDepartement ? (
                              detalj.ansvarligDepartementVirksomhetId ? (
                                <Link asChild>
                                  <RouterLink to={`/virksomheter/${detalj.ansvarligDepartementVirksomhetId}`}>{detalj.ansvarligDepartement}</RouterLink>
                                </Link>
                              ) : (
                                detalj.ansvarligDepartement
                              )
                            ) : (
                              '—'
                            )}
                          </Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Endrer</Table.Cell>
                          <Table.Cell>
                            {endringer.length === 0 ? (
                              '—'
                            ) : (
                              <ul style={{ margin: 0, padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
                                {endringer.map((e) => {
                                  const mal = alleRettskilder.find((r) => r.id === e.endringRettskildeId);
                                  return (
                                    <li key={e.id}>
                                      {mal ? (
                                        <Link asChild>
                                          <RouterLink to={`/rettskilder/${mal.id}`}>{mal.kortnavn ?? mal.tittel}</RouterLink>
                                        </Link>
                                      ) : (
                                        e.endringEid
                                      )}
                                    </li>
                                  );
                                })}
                              </ul>
                            )}
                          </Table.Cell>
                        </Table.Row>
                      </Table.Body>
                    </Table>
                  </>
                )}

                {erVirksomhetsEgen && (
                  <>
                    <Heading level={3} data-size="2xs" style={{ margin: '0 0 0.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
                      Lokalt forvaltet
                    </Heading>
                    <Table>
                      <Table.Body>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Internt dok.nr</Table.Cell>
                          <Table.Cell>{detalj.interntDokNr ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Revisjonsnr</Table.Cell>
                          <Table.Cell>{detalj.revisjonsnr ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Vedtatt av</Table.Cell>
                          <Table.Cell>{detalj.vedtattAv ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Vedtaksdato</Table.Cell>
                          <Table.Cell>{detalj.vedtaksdato ?? '—'}</Table.Cell>
                        </Table.Row>
                        <Table.Row>
                          <Table.Cell style={{ paddingRight: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>Gyldig til</Table.Cell>
                          <Table.Cell>{detalj.gyldigTil ?? '—'}</Table.Cell>
                        </Table.Row>
                      </Table.Body>
                    </Table>
                  </>
                )}
              </>
            ) : (
              <form onSubmit={lagreMetadata} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
                <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', margin: 0 }}>
                  ELI ({detalj.eli ?? '—'}) er permanent skrivebeskyttet og kan ikke redigeres her.
                </Paragraph>
                <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                  <Textfield data-size="sm" label="Kortnavn" value={metaKortnavn} onChange={(e) => setMetaKortnavn(e.target.value)} style={{ flex: 1, minWidth: '12rem' }} />
                  <Textfield data-size="sm" label="Utgiver" value={metaUtgiver} onChange={(e) => setMetaUtgiver(e.target.value)} style={{ flex: 1, minWidth: '12rem' }} />
                </div>
                <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                  <Textfield data-size="sm" label="Internt dok.nr" value={metaInterntDokNr} onChange={(e) => setMetaInterntDokNr(e.target.value)} style={{ flex: 1, minWidth: '12rem' }} />
                  <Textfield data-size="sm" label="Revisjonsnr" value={metaRevisjonsnr} onChange={(e) => setMetaRevisjonsnr(e.target.value)} style={{ flex: 1, minWidth: '12rem' }} />
                </div>
                <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                  <Textfield data-size="sm" label="Vedtatt av" value={metaVedtattAv} onChange={(e) => setMetaVedtattAv(e.target.value)} style={{ flex: 1, minWidth: '12rem' }} />
                  <Textfield data-size="sm" type="date" label="Vedtaksdato" value={metaVedtaksdato} onChange={(e) => setMetaVedtaksdato(e.target.value)} style={{ flex: 1, minWidth: '12rem' }} />
                </div>
                <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                  <Textfield data-size="sm" type="date" label="Gyldig til" value={metaGyldigTil} onChange={(e) => setMetaGyldigTil(e.target.value)} style={{ flex: 1, minWidth: '12rem' }} />
                  <Textfield data-size="sm" type="date" label="Konsolidert dato" value={metaKonsolidertDato} onChange={(e) => setMetaKonsolidertDato(e.target.value)} style={{ flex: 1, minWidth: '12rem' }} />
                </div>
                {metadataFeil && <Alert data-color="danger">{metadataFeil}</Alert>}
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <Button data-size="sm" type="submit" disabled={lagrerMetadata}>{lagrerMetadata ? 'Lagrer …' : 'Lagre'}</Button>
                  <Button data-size="sm" variant="tertiary" onClick={() => setRedigererMetadata(false)}>Avbryt</Button>
                </div>
              </form>
            )}
          </div>
        </>
      )}

      {fane === 'innhold' && (
        <>
          {taggFeil && <Alert data-color="danger">{taggFeil}</Alert>}

          <div style={{ display: 'flex', gap: '1.5rem', alignItems: 'flex-start', marginTop: '0.25rem' }}>
            <div style={{ width: '360px', flexShrink: 0 }}>
              <Textfield
                label="Søk i strukturen"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
                style={{ marginBottom: '0.75rem' }}
              />
              {detalj.kildetype === 'Rundskriv' && (
                <div style={{ marginBottom: '0.75rem' }}>
                  {!visOpprettKapittel ? (
                    <Button data-size="sm" variant="secondary" onClick={() => setVisOpprettKapittel(true)}>
                      Nytt kapittel
                    </Button>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                      <Textfield label="Nummer" data-size="sm" value={nyKapittelNummer} onChange={(e) => setNyKapittelNummer(e.target.value)} />
                      <Textfield label="Overskrift" data-size="sm" value={nyKapittelOverskrift} onChange={(e) => setNyKapittelOverskrift(e.target.value)} />
                      <div style={{ display: 'flex', gap: '0.5rem' }}>
                        <Button data-size="sm" disabled={kapittelLagrer || !nyKapittelNummer.trim()} onClick={() => opprettKapittel(null)}>
                          {kapittelLagrer ? 'Oppretter …' : 'Opprett'}
                        </Button>
                        <Button data-size="sm" variant="tertiary" onClick={() => setVisOpprettKapittel(false)}>
                          Avbryt
                        </Button>
                      </div>
                      {kapittelFeil && <Alert data-color="danger">{kapittelFeil}</Alert>}
                    </div>
                  )}
                </div>
              )}
              {treVm.length > 0 ? (
                <div style={{ border: '1px solid var(--ds-color-neutral-border-subtle)', borderRadius: 'var(--ds-border-radius-lg)', maxHeight: '70vh', overflowY: 'auto' }}>
                  <RettskildeTre
                    key={`${id ?? ''}:${eidFraUrl ?? ''}`}
                    nodes={treVm}
                    selectedEId={selectedEid}
                    onSelect={(eid) => setSelectedEid(eid)}
                    filter={filter}
                    defaultExpanded={forfedrePath}
                  />
                </div>
              ) : (
                <Paragraph>Ingen noder.</Paragraph>
              )}
            </div>

            <div style={{ flex: 1, minWidth: 0 }}>
              {!valgtNode && <Paragraph>Velg en node i treet for å se innholdet.</Paragraph>}

              {valgtNode && (
                <>
                  <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>
                    {valgtNode.nummer ?? valgtNode.nodeType}
                    {valgtNode.overskrift && ` — ${valgtNode.overskrift}`}
                  </Heading>
                  <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '1rem' }}>
                    {valgtNode.eid}
                  </Paragraph>

                  {detalj.kildetype === 'Rundskriv' ? (
                    valgtNode.handbokMetadata ? (
                      <KommentarRedigering
                        handbokId={id!}
                        mode="rediger"
                        node={valgtNode}
                        alleRettskilder={alleRettskilder}
                        alleVilkar={alleVilkarForReferanse}
                        alleTjenester={alleTjenesterForReferanse}
                        onLagret={(oppdatert) => refetchNoder(oppdatert.eid)}
                      />
                    ) : !visOpprettKommentar ? (
                      <Button data-size="sm" variant="secondary" onClick={() => setVisOpprettKommentar(true)}>
                        Ny kommentarseksjon her
                      </Button>
                    ) : (
                      <KommentarRedigering
                        handbokId={id!}
                        mode="ny"
                        parentNodeId={valgtNode.id}
                        alleRettskilder={alleRettskilder}
                        alleVilkar={alleVilkarForReferanse}
                        alleTjenester={alleTjenesterForReferanse}
                        onLagret={(opprettet) => {
                          setVisOpprettKommentar(false);
                          refetchNoder(opprettet.eid);
                        }}
                        onAvbryt={() => setVisOpprettKommentar(false)}
                      />
                    )
                  ) : detalj.kildetype === 'Brukerveiledning' ? (
                    valgtNode.tekst ? (
                      <div style={{ maxWidth: '60rem' }}>
                        <RaaTekstMedLenker raaTekst={valgtNode.tekst} lenker={nettsideLenker} />
                      </div>
                    ) : (
                      <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Ingen tekst hentet for denne siden.</Paragraph>
                    )
                  ) : kanTagges ? (
                    <>
                      <TagTekst
                        text={valgtNode.tekst!}
                        tags={taggerPerNode.get(valgtNode.eid) ?? []}
                        kinds={taggKinds}
                        activeKind={activeKind}
                        onActiveKindChange={setActiveKind}
                        onTag={(t) => handleTag(valgtNode.eid, valgtNode.tekst!, t)}
                        onRemoveTag={handleSlett}
                        registry={registry}
                        onLinkTag={handleKobleTag}
                        onOpprettFraTag={(taggId) => startOpprettVilkarFraTag(taggId)}
                        opprettFraTagKinds={['vilkar']}
                        resolveRef={resolveRef}
                        references={inlineReferanser}
                      />
                      {opprettVilkarFraTaggId && (
                        <form
                          onSubmit={opprettVilkarFraTag}
                          style={{
                            display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap',
                            marginTop: '0.75rem', padding: '0.75rem', borderRadius: 'var(--ds-border-radius-default)',
                            background: 'var(--ds-color-warning-surface-tinted)',
                            border: '1px solid var(--ds-color-warning-border-subtle)',
                          }}
                        >
                          <Paragraph style={{ width: '100%', margin: 0, fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                            Opprett vilkår fra utdraget «{tagger.find((t) => t.id === opprettVilkarFraTaggId)?.quoteExact}» —
                            juridisk grunnlag fylles automatisk ut fra denne rettskilden og noden. Vilkåret plasseres IKKE
                            automatisk i noe vilkårstre — det gjøres som et eget steg senere.
                          </Paragraph>
                          <Textfield
                            data-size="sm"
                            label="Tittel på vilkåret"
                            value={nyVilkarTittel}
                            onChange={(e) => setNyVilkarTittel(e.target.value)}
                            style={{ minWidth: '16rem' }}
                            required
                          />
                          <Field>
                            <Label>Tjeneste</Label>
                            <Select data-size="sm" value={nyVilkarTjenesteId} onChange={(e) => setNyVilkarTjenesteId(e.target.value)}>
                              <Select.Option value="">Velg …</Select.Option>
                              {alleTjenesterForReferanse.map((t) => (
                                <Select.Option key={t.id} value={t.id}>{t.label}</Select.Option>
                              ))}
                            </Select>
                          </Field>
                          <Button data-size="sm" type="submit" disabled={oppretterVilkarFraTag || !nyVilkarTittel.trim() || !nyVilkarTjenesteId}>
                            {oppretterVilkarFraTag ? 'Oppretter …' : 'Opprett vilkår'}
                          </Button>
                          <Button data-size="sm" variant="tertiary" onClick={() => setOpprettVilkarFraTaggId(null)}>
                            Avbryt
                          </Button>
                        </form>
                      )}
                    </>
                  ) : harIkkeTaggbarTekst ? (
                    <Paragraph style={{ whiteSpace: 'pre-wrap' }}>{valgtNode.tekst}</Paragraph>
                  ) : (
                    <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
                      Denne noden har ingen egen løpetekst — velg et ledd eller punkt under den for å tagge.
                    </Paragraph>
                  )}

                  {(referertAvTjenesterForNode.length > 0 || referertAvDokumenterForNode.length > 0) && (
                    <div style={{ marginTop: '1rem', padding: '0.75rem', borderRadius: 'var(--ds-border-radius-default)', background: 'var(--ds-color-info-surface-tinted)' }}>
                      <Heading level={4} data-size="2xs" style={{ marginBottom: '0.4rem' }}>
                        Referert fra (punkt 6/9 — koblingen til denne noden, sett fra den andre siden)
                      </Heading>
                      <ul style={{ margin: 0, padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '0.3rem', fontSize: 'var(--ds-font-size-1)' }}>
                        {referertAvTjenesterForNode.map((r, i) => (
                          <li key={`t-${r.tjenesteId}-${i}`}>
                            <Link asChild><RouterLink to={`/tjenester/${r.tjenesteId}`}>{r.tjenesteTittel}</RouterLink></Link>
                            <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}> (tjeneste, {r.tilEid})</span>
                          </li>
                        ))}
                        {referertAvDokumenterForNode.map((r, i) => (
                          <li key={`d-${r.dokumentId}-${i}`}>
                            <Link asChild>
                              <RouterLink to={`/rettskilder/${r.dokumentId}?eid=${encodeURIComponent(r.fraNodeEid)}`}>
                                {r.dokumentTittel}{r.fraNodeOverskrift ? ` — ${r.fraNodeOverskrift}` : ''}
                              </RouterLink>
                            </Link>
                            <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}> ({tilEidVisning(r.tilEid)})</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}

                  <div style={{ marginTop: '1.5rem', paddingTop: '1rem', borderTop: '1px solid var(--ds-color-neutral-border-subtle)' }}>
                    <Heading level={3} data-size="xs" style={{ marginBottom: '0.5rem' }}>
                      Referanser
                    </Heading>
                    {referanseFeil && <Alert data-color="danger" style={{ marginBottom: '0.5rem' }}>{referanseFeil}</Alert>}
                    {(() => {
                      const nodeReferanser = referanser.filter((r) => r.fraNodeId === valgtNode.id);
                      // Punkt 9/10 — for Brukerveiledning vises de AUTOMATISK utledede §3.2-lenkene i
                      // SAMME liste som de generelle referansene, i stedet for en egen, duplikat
                      // strukturert lenke-tabell ved siden av (NettsideDetalj.tsx sin gamle "LENKER:"-
                      // blokk finnes ikke mer — dette ER den ene erstatningsvisningen for ALLE doctyper).
                      const nettsideLenkerForNode = detalj.kildetype === 'Brukerveiledning' ? nettsideLenker : [];
                      if (nodeReferanser.length === 0 && nettsideLenkerForNode.length === 0) {
                        return <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>Ingen referanser fra denne noden.</Paragraph>;
                      }
                      return (
                        <ul style={{ margin: '0 0 0.75rem', padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
                          {nodeReferanser.map((r) => (
                            <li key={r.id} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: 'var(--ds-font-size-1)' }}>
                              <Link asChild>
                                <RouterLink to={`/rettskilder/${r.tilRettskildeId}?eid=${encodeURIComponent(r.tilEid)}`}>{r.tilEid}</RouterLink>
                              </Link>
                              {r.opprinnelse === 'import' ? (
                                <Tag data-color="neutral" data-size="sm">fra kilden</Tag>
                              ) : (
                                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernReferanse(r.id)}>Fjern</Button>
                              )}
                            </li>
                          ))}
                          {nettsideLenkerForNode.map((l) => (
                            <li key={l.id} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: 'var(--ds-font-size-1)' }}>
                              <Tag data-color={l.type === 'lovdatalenke' ? 'warning' : 'neutral'} data-size="sm">{l.type}</Tag>
                              {l.tilRettskildeId ? (
                                <Link asChild>
                                  <RouterLink to={`/rettskilder/${l.tilRettskildeId}`}>{l.tilRettskildeTittel ?? l.tilRettskildeEli}</RouterLink>
                                </Link>
                              ) : (
                                <Link href={l.raaHref} target="_blank" rel="noopener noreferrer">{l.ankerTekst ?? l.raaHref}</Link>
                              )}
                              <Tag data-color="neutral" data-size="sm">fra kilden</Tag>
                            </li>
                          ))}
                        </ul>
                      );
                    })()}
                    <form onSubmit={leggTilReferanse} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
                      <RettskildeVelger rettskilder={alleRettskilder} value={nyReferanseRettskildeId} onChange={setNyReferanseRettskildeId} />
                      <Textfield data-size="sm" label="eId" value={nyReferanseEid} onChange={(e) => setNyReferanseEid(e.target.value)}
                        style={{ minWidth: '20rem', fontFamily: 'monospace' }} />
                      <Button data-size="sm" type="submit" disabled={leggerTilReferanse || !nyReferanseRettskildeId || !nyReferanseEid.trim()}>
                        {leggerTilReferanse ? 'Kobler …' : 'Koble referanse'}
                      </Button>
                    </form>
                  </div>
                </>
              )}
            </div>
          </div>
        </>
      )}

      {fane === 'relasjoner' && (
        <KontekstPanel variant="full" grupper={kontekstGrupper} />
      )}

      {fane === 'handbok' && visHandbokFane && (
        <div>
          <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
            Denne håndboken omhandler
          </Heading>
          {rettskildeomfang.length === 0 ? (
            <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
              Ingen rettskilder deklarert ennå.
            </Paragraph>
          ) : (
            <Table border style={{ marginBottom: '0.75rem' }}>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Tittel</Table.HeaderCell>
                  <Table.HeaderCell>Kildetype</Table.HeaderCell>
                  <Table.HeaderCell></Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {rettskildeomfang.map((o) => {
                  const rk = alleRettskilder.find((r) => r.id === o.tilRettskildeId);
                  return (
                    <Table.Row key={o.id}>
                      <Table.Cell>
                        <Link asChild>
                          <RouterLink to={`/rettskilder/${o.tilRettskildeId}`}>{rk?.kortnavn ?? rk?.tittel ?? o.tilRettskildeId}</RouterLink>
                        </Link>
                      </Table.Cell>
                      <Table.Cell>{rk?.kildetype ?? '—'}</Table.Cell>
                      <Table.Cell>
                        <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernOmfang(o.id)}>Fjern</Button>
                      </Table.Cell>
                    </Table.Row>
                  );
                })}
              </Table.Body>
            </Table>
          )}
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <RettskildeVelger
              rettskilder={alleRettskilder.filter((r) => r.id !== id && !rettskildeomfang.some((o) => o.tilRettskildeId === r.id))}
              value={nyOmfangRettskildeId}
              onChange={setNyOmfangRettskildeId}
              label="Legg til rettskilde"
            />
            <Button data-size="sm" onClick={leggTilOmfang} disabled={leggerTilOmfang || !nyOmfangRettskildeId}>
              {leggerTilOmfang ? 'Legger til …' : 'Legg til'}
            </Button>
          </div>
          {omfangFeil && <Alert data-color="danger" style={{ marginTop: '0.3rem' }}>{omfangFeil}</Alert>}
        </div>
      )}
    </>
  );
}
