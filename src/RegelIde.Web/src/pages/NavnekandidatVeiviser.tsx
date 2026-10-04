import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router';
import {
  Alert, Breadcrumbs, Button, Card, Divider, Field, Heading, Label, Link, Paragraph, Radio, Search,
  Select, Spinner, Table, Tag, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type {
  BrregEnhetDto, NavnekandidatDto, Navneformgrunn, RelasjonsTypeKonfigurasjonDto,
  RettskildeDetalj as RettskildeDetaljDto, RettskildeNodeDto, VirksomhetsbegrepDto,
} from '../api/types';
import { BerikelseVisning } from '../virksomhet/BerikelseVisning';
import { GruppebegrepVelger } from '../virksomhet/GruppebegrepVelger';
import { NavneformgrunnVelger } from '../virksomhet/Navneformgrunn';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { KonfidensTag, konfidensGrunnTekst } from '../kandidater/KonfidensTag';
import { Metatekst } from '../entitet/Metatekst';

/**
 * [Ny, navnekandidat-wizard-runden, 2026-09-07] Behandling av ÉN navnekandidat, ende til ende.
 *
 * <h3>Hva den løser</h3>
 * «Godkjenn» på Navnekandidater-siden var én knapp som betydde ulike ting per kategori, og for
 * `virksomhet` endte den i en blindvei: det ble opprettet en `TekstTagg` med `RefId=null` som ALDRI
 * ble koblet, og «Finn/opprett virksomhet»-lenken forsvant så snart status ikke var «Venter» lenger.
 * Denne veiviseren tar saksbehandleren gjennom hele kjeden i stedet, og lukker den:
 * navneform (med begrunnelse) → godkjent kandidat → en tagg som faktisk PEKER på virksomheten.
 *
 * <h3>De to mekanismene som IKKE skal blandes</h3>
 * Steg 1 retter TEKSTEN, og finnes bare for regex-artefakter («Ø Suldal kommune», der en ledende Ø er
 * limt inn fra en koordinat). Steg 3 velger BEGRUNNELSEN, for legitime strenger som faktisk står slik
 * i lovteksten («Suldal», «Matilsynet», «Arkivverket») — de skal beholdes ordrett, ikke skrives om.
 * Se `Navneformgrunn.tsx` og `NavnekandidatOppdagelseTjeneste.OppdaterAsync`.
 *
 * <h3>[Utvidet, gruppemedlemskap-runden, 2026-09-08, issue #164] «Medlem av eksisterende gruppe»</h3>
 * Denne veien SA tidligere eksplisitt til saksbehandleren at den ikke var bygget. Den er nå bygget,
 * og er den fjerde `Slag`-verdien: kandidaten «Karasjok» i forskriften er ikke et nytt gruppebegrep
 * og ikke bare en virksomhet — den er «Karasjok kommune» I EGENSKAP AV å være navngitt medlem av
 * «språkutviklingskommuner». Veien gjenbruker HELE steg 4-maskineriet (katalogsøk/Brreg/kun navn +
 * navneformgrunn) og legger ett felt til: hvilken gruppe. Utfallet er alt virksomhet-veien gir,
 * pluss en `MyndighetstildelingEntitet` hjemlet i KANDIDATENS EGEN rettskilde — det er forskriften
 * der navnet står som navngir medlemskapet, ikke loven som definerte gruppen.
 *
 * <h3>[ENDRET, issue #203 pkt. 2] «Administrativ inndeling» er nå en ekte kategori — men IKKE i DENNE
 * veiviseren</h3>
 * Kategorien finnes nå (`NavnekandidatDto.kategori === 'administrativ_inndeling'`, satt av SSR-basert
 * klassifisering ved sveip, se `NavnekandidatOppdagelseTjeneste.KlassifiserAsync`), men godkjennes
 * IKKE via denne veiviseren — den følger nøyaktig samme mekanisme som «Gruppe som defineres her»
 * (oppretter begrepet direkte, ingen virksomhetskobling å velge), og har derfor sin egen
 * hurtig-«Godkjenn»-knapp på `NavnekandidaterListe.tsx` i stedet, akkurat som gruppe. Steg 2 sin
 * radio-liste under er derfor BEVISST uendret (ingen ny «administrativ inndeling»-radio her) — en
 * kandidat SSR alt har klassifisert dit trenger ingen ekstra menneskelig kategorivalg i en veiviser
 * bygget for den mer kompliserte virksomhets-koblingsflyten.
 *
 * <h3>[Utvidet, «alle mekanismer»-runden, 2026-09-21, issue #283] De fire resterende mekanismene</h3>
 * Denne runden dekker det filkommentaren lenge sa var bevisst utenfor: (1) en MANUELL inngangsdør
 * (`TagTekst`s «Behandle som organ/gruppe →», `RettskildeDetalj.tsx`) i tillegg til sveipet —
 * `Kandidat.oppdagelsesKilde==='manuell'`, men INGEN forgrening i veiviseren basert på det; (2) et
 * valgfritt STEG 4 «Utover navneform?» mellom virksomhetsvalget og bekreftelsen, for BÅDE
 * `virksomhet`- og `gruppemedlem`-sporet (se `Tillegg`-typen) — «Rolle tildelt her» (generell
 * `Myndighetstildeling`, IKKE bundet til gruppemedlemskapet) eller «Relasjon til annen virksomhet»
 * (`VirksomhetRelasjonEntitet`, løser issue #263 AC2/AC3 sitt «Minimalt»-nivå) registreres SAMTIDIG
 * som navneformen lukkes; (3) gruppe-sporet (`Slag==='gruppe'`) har fått et valgfritt tillegg — «er
 * denne gruppen selv medlem av en annen gruppe?» — som oppretter et `GruppeMedlemskapEntitet` mellom
 * det NYE gruppebegrepet og en allerede eksisterende, overordnet gruppe (ett nytt endepunkt,
 * `kobl-til-gruppe-av-gruppe`, siden klienten ikke kjenner det nye gruppebegrepets id på forhånd).
 * Korreksjonsregel-tabellen (issue #203 pkt. 4) er IKKE del av denne runden.
 *
 * <h3>Designmønster</h3>
 * Ny side, så den følger saksbehandler-mønsteret fra dag én (docs/09 §14): brødsmulesti,
 * `data-size="sm"` gjennomgående, `Card` ALLTID rendret med tom-tilstand som `Paragraph` inni, og
 * steg-indikatoren som en `Tag`-rekke (samme visuelle idiom som `StatusStepper`, men den er bundet
 * til den 6-trinns entitets-statusmodellen og passer ikke her).
 */

/** Steg 2 sitt valg. `null` = ikke valgt ennå. `gruppemedlem` er
 * [ny, gruppemedlemskap-runden, 2026-09-08] — se filkommentaren. */
type Slag = 'virksomhet' | 'gruppemedlem' | 'gruppe' | 'irrelevant';

/** De to `Slag`-verdiene som går videre til steg 3 (virksomhetsvalget) og steg 4 (tillegget under). Skilt
 * ut som en egen predikatfunksjon fordi den brukes på flere steder — steg-titlene, steg 3s
 * «Neste»-knapp, steg 4 (tillegget) og steg 5 (bekreft) sin synlighet — og en glemt oppdatering av ÉN
 * av dem gir en veiviser som halvveis åpner en vei. */
function harVirksomhetssteg(slag: Slag | null): boolean {
  return slag === 'virksomhet' || slag === 'gruppemedlem';
}

/**
 * [Ny, «alle mekanismer»-runden, 2026-09-21, issue #283 AC5] Steg 4 sitt valg — det NYE, valgfrie
 * steget mellom virksomhetsvalget og bekreftelsen, for BÅDE `virksomhet`- og `gruppemedlem`-sporet.
 * `'ingen'` er standarden (dagens oppførsel, uendret) — de aller fleste treff er bare en navneform.
 */
type Tillegg = 'ingen' | 'rolle' | 'relasjon';

/** Steg-titlene. Steg 3 sin tittel avhenger av `Slag`: gruppemedlem-veien velger BÅDE virksomhet og
 * gruppe i det steget, og en tittel som bare sa «Hvilken virksomhet?» ville underrapportert hva
 * steget faktisk krever av saksbehandleren.
 * <p>[ENDRET, «alle mekanismer»-runden, 2026-09-21, issue #283 AC5] Fem elementer (ikke fire) for
 * `virksomhet`/`gruppemedlem`-sporet — det nye steg 4 («Utover navneform?») er satt inn FØR «Bekreft»,
 * som dermed rykker fra indeks 3 til indeks 4. Andre `Slag`-verdier (`gruppe`/`irrelevant`/`null`)
 * bruker fortsatt bare fire — de når aldri forbi steg 2 i det hele tatt.</p>
 * <p>[Rettet samtidig] Indeksene er FAKTISK «steg-nummer minus 1» nå (kommentaren under sa alltid
 * det, men steg 3-kortet leste tidligere indeks 3 — «Bekreft» — i stedet for indeks 2. Usynlig fordi
 * teksten der aldri ble vist alene, kun sammen med et hardkodet «3. »-prefiks.)</p> */
function stegTitler(slag: Slag | null): readonly string[] {
  // [ENDRET 2026-09-09] «Kontekst» er ikke lenger et steg — se `steg`-tilstanden i komponenten.
  // Indeks i denne listen er derfor steg-nummer MINUS 1.
  const virksomhetTittel = slag === 'gruppemedlem' ? 'Hvilken virksomhet og gruppe?' : 'Hvilken virksomhet?';
  if (harVirksomhetssteg(slag)) {
    return ['Er teksten riktig?', 'Hva slags ting er dette?', virksomhetTittel, 'Utover navneform?', 'Bekreft'];
  }
  return ['Er teksten riktig?', 'Hva slags ting er dette?', virksomhetTittel, 'Bekreft'];
}

/** Steg 3 sin gren: koble til en som finnes, eller opprette en ny (fra Brreg, eller kun navn). */
type VirksomhetVei = 'eksisterende' | 'brreg' | 'kunNavn';

/**
 * Setningen fra rettskilden med treffet markert. Bygget på nøyaktig de samme offsetene og det samme
 * 30-tegns kontekstvinduet som backend bruker for `QuotePrefix`/`QuoteSuffix`
 * (`NavnekandidatOppdagelseTjeneste`) — men her vises HELE nodeteksten, med treffet uthevet, siden
 * poenget er at saksbehandleren skal kunne lese setningen og selv vurdere om treffet er riktig
 * avgrenset. Et for smalt vindu er nettopp hvordan «Ø Suldal kommune» slapp gjennom i utgangspunktet.
 */
function TreffIKontekst({ tekst, start, slutt }: { tekst: string; start: number; slutt: number }) {
  // Defensivt: rettskilden kan ha endret seg siden sveipet, så offsetene er ikke garantert gyldige.
  // Da vises teksten UTEN markering i stedet for å kaste eller markere feil sted.
  const gyldig = start >= 0 && slutt <= tekst.length && slutt > start;
  if (!gyldig) {
    return (
      <>
        <Paragraph data-size="sm" style={{ margin: 0, fontStyle: 'italic' }}>{tekst}</Paragraph>
        <Alert data-color="warning" data-size="sm" style={{ marginTop: '0.5rem' }}>
          Tegnposisjonene ({start}–{slutt}) stemmer ikke med nodeteksten ({tekst.length} tegn) —
          rettskilden er sannsynligvis endret siden sveipet. Treffet kan ikke markeres, og det blir
          ikke opprettet noen tagg i rettskilden ved fullføring.
        </Alert>
      </>
    );
  }
  return (
    <Paragraph data-size="sm" style={{ margin: 0 }}>
      {tekst.slice(0, start)}
      <mark
        style={{
          background: 'var(--ds-color-brand1-surface-tinted)',
          color: 'var(--ds-color-brand1-text-default)',
          borderBottom: '2px solid var(--ds-color-brand1-border-default)',
          fontWeight: 'var(--ds-font-weight-semibold)',
        }}
      >
        {tekst.slice(start, slutt)}
      </mark>
      {tekst.slice(slutt)}
    </Paragraph>
  );
}

export default function NavnekandidatVeiviser() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { virksomheter, oppdater: oppdaterVirksomheter } = useVirksomheter();

  const [kandidat, setKandidat] = useState<NavnekandidatDto | null>(null);
  const [rettskilde, setRettskilde] = useState<RettskildeDetaljDto | null>(null);
  const [noder, setNoder] = useState<RettskildeNodeDto[] | null>(null);
  const [lasteFeil, setLasteFeil] = useState<string | null>(null);

  // [ENDRET, nemnd/sekretariat-runden, 2026-09-09] Starter på steg 1, ikke 0. Steg 0 («Kontekst»)
  // inneholder ingen beslutning — bare setningen fra rettskilden og hvor den står — og et
  // «Neste»-klikk gjennom en ren leseskjerm er friksjon uten innhold. Kortet vises fortsatt, alltid,
  // øverst; det er bare ikke lenger et steg man må klikke seg gjennom. Johann 2026-09-09: «her må
  // wizarden være enklere».
  const [steg, setSteg] = useState(1);

  // Steg 1
  const [tekst, setTekst] = useState('');
  const [lagrerTekst, setLagrerTekst] = useState(false);

  // Steg 2
  const [slag, setSlag] = useState<Slag | null>(null);

  // Steg 3
  const [vei, setVei] = useState<VirksomhetVei>('eksisterende');
  const [valgtVirksomhetId, setValgtVirksomhetId] = useState('');
  const [navneformgrunn, setNavneformgrunn] = useState<Navneformgrunn | null>(null);
  const [brregSok, setBrregSok] = useState('');
  const [brregTreff, setBrregTreff] = useState<BrregEnhetDto[] | null>(null);
  const [brregSoker, setBrregSoker] = useState(false);
  const [nyttNavn, setNyttNavn] = useState('');
  const [oppretterVirksomhet, setOppretterVirksomhet] = useState(false);

  // Steg 3, gruppemedlem-veien (gruppemedlemskap-runden). `null` = ikke lastet ennå — brukes for å
  // skille «laster» fra «ingen gruppebegrep finnes» (docs/09 §15: den negative påstanden er
  // reservert for et faktisk tomt svar).
  const [gruppebegrep, setGruppebegrep] = useState<VirksomhetsbegrepDto[] | null>(null);
  const [valgtGruppeBegrepId, setValgtGruppeBegrepId] = useState('');
  /** Tittelen på loven det VALGTE gruppebegrepet er hjemlet i. Hentes for ÉN rettskilde etter at
   * valget er gjort — bevisst ikke ved å laste hele rettskildelista for å kunne merke lista på
   * forhånd: korpuset er ~5900 rettskilder, og docs/09 §10 er tydelig på at slike lister ikke skal
   * lastes for å pynte på et valg. `null` mens den lastes eller når loven ikke kunne hentes. */
  const [valgtGruppeLovTittel, setValgtGruppeLovTittel] = useState<string | null>(null);

  // [Ny, «alle mekanismer»-runden, 2026-09-21, issue #283 AC5] Steg 4 — valgfritt tillegg UTOVER
  // navneformen, for BÅDE virksomhet- og gruppemedlem-sporet. Default 'ingen' = dagens oppførsel.
  const [tillegg, setTillegg] = useState<Tillegg>('ingen');

  // Tillegg==='rolle' — AC6: rollebegrep (et FRITT valgt gruppebegrep, IKKE gruppemedlem-sporets egen
  // gruppe) + paragrafspenn (ETT par — se `LeggTilMyndighetstildelingForm.tsx` for den fulle
  // liste-byggeren; her holdt til ett par, se PR-beskrivelsen for begrunnelsen) + valgfritt vilkår.
  const [valgtRolleBegrepId, setValgtRolleBegrepId] = useState('');
  const [rolleFraEid, setRolleFraEid] = useState('');
  const [rolleTilEid, setRolleTilEid] = useState('');
  const [rolleVilkaar, setRolleVilkaar] = useState('');

  // Tillegg==='relasjon' — AC7/AC8, løser issue #263 AC2/AC3 sitt «Minimalt»-nivå.
  const [relasjonstyper, setRelasjonstyper] = useState<RelasjonsTypeKonfigurasjonDto[] | null>(null);
  const [motpartVirksomhetId, setMotpartVirksomhetId] = useState('');
  const [relasjonsType, setRelasjonsType] = useState('');
  /** `true` = hjemlet i kandidatens EGEN rettskilde/node (sendes ikke eksplisitt, settes server-side —
   * samme «hjemmelen er ikke et valg»-konvensjon som gruppemedlemskap). `false` = ingen formell
   * hjemmel, kun `relasjonKommentar` som fritekst. */
  const [relasjonHjemletHer, setRelasjonHjemletHer] = useState(true);
  const [relasjonKommentar, setRelasjonKommentar] = useState('');

  // Slag==='gruppe' — AC9: valgfritt tillegg «er denne gruppen selv medlem av en annen gruppe?».
  // Gjenbruker `gruppebegrep`-lista over (bredere lastebetingelse, se effekten under).
  const [erGruppeAvGruppe, setErGruppeAvGruppe] = useState(false);
  const [valgtOverordnetGruppeBegrepId, setValgtOverordnetGruppeBegrepId] = useState('');

  // [Ny, issue #298 AC3] Slag==='gruppe' — saksbehandlerens EKSPLISITTE valg mellom et gruppebegrep
  // hjemlet i DENNE loven (dagens oppførsel, default — AC5: «ingen regresjon») og et fast, nasjonalt
  // begrep uten lovscoping (Johanns «Kongen»-eksempel, issue #298). Gjensidig utelukkende med
  // gruppe-av-gruppe-tillegget over — begge er EKSTRA valg på gruppe-sporet, men gruppe-av-gruppe
  // gjelder uansett hvilken scope man velger her, så de to påvirker ikke hverandre i UI-en.
  const [gruppeScope, setGruppeScope] = useState<'lovspesifikt' | 'fast'>('lovspesifikt');

  // Steg 5 / avslutning
  const [fullfører, setFullfører] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [ferdig, setFerdig] = useState<{
    tittel: string;
    detaljer: string[];
    rettskildeLenke: string | null;
    /** Navnet på tagg-laget taggen faktisk havnet i — «Virksomhet» for virksomhet-veien,
     * «Begrep» for gruppe-veien (der taggen er en `begrep`-tagg mot gruppebegrepet). Må følge
     * utfallet, ikke være hardkodet: å sende saksbehandleren til feil lag er en påstand som
     * ikke stemmer, og laget er nettopp det man må velge for å SE markeringen. */
    taggLag: string | null;
    virksomhetLenke: string | null;
    /** [Ny, nemnd/sekretariat-runden, 2026-09-09] Virksomheten kjeden ble lukket mot, slik at
     * oppsummeringen kan tilby NESTE handling: et virksomhetssveip som finner de ØVRIGE
     * forekomstene av navnet i korpuset. Uten dette stoppet flyten etter én tagg, og
     * saksbehandleren måtte selv vite at det finnes en annen kø som gjør resten (Johann
     * 2026-09-09: «her flyter det litt sammen»). `null` på gruppe-/irrelevant-veiene, som ikke
     * ender i en virksomhet. */
    virksomhetForSveip: { id: string; navn: string } | null;
    /** [Ny, gruppemedlemskap-runden] Gruppebegrepets detaljside, satt på gruppemedlem-veien (den
     * EKSISTERENDE gruppen medlemskapet ble registrert for) og — [Ny, «alle mekanismer»-runden,
     * 2026-09-21, issue #283 AC9] — på gruppe-av-gruppe-tillegget (det NYE gruppebegrepets EGEN
     * side, der man ser at det er registrert som medlem av den overordnede gruppen). */
    gruppeLenke: string | null;
    advarsel: string | null;
  } | null>(null);

  // [Ny, nemnd/sekretariat-runden, 2026-09-09] Sveipet som tilbys i oppsummeringen — se
  // `virksomhetForSveip`. Egen tilstand, ikke gjenbruk av veiviser-stegene: dette skjer ETTER at
  // kandidaten er ferdig behandlet, og skal ikke kunne rulle veiviseren tilbake.
  const [sveiper, setSveiper] = useState(false);
  const [sveipResultat, setSveipResultat] = useState<{ funnet: number; nye: number } | null>(null);
  const [sveipFeil, setSveipFeil] = useState<string | null>(null);

  async function kjorVirksomhetssveip(virksomhetId: string) {
    setSveiper(true);
    setSveipFeil(null);
    try {
      const r = await api.sveipVirksomhetKandidater({ virksomhetId });
      setSveipResultat({ funnet: r.antallTreffFunnet, nye: r.antallNyeKandidater });
    } catch (e) {
      setSveipFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved sveip.');
    } finally {
      setSveiper(false);
    }
  }

  useEffect(() => {
    if (!id) return;
    setLasteFeil(null);
    api.hentNavnekandidat(id)
      .then((k) => {
        setKandidat(k);
        setTekst(k.foreslattTekst);
        // Forhåndsvelg det opplagte: en 'gruppe'-kandidat skal normalt bli et gruppebegrep, en
        // 'virksomhet'-kandidat en konkret virksomhet. Fortsatt et VALG brukeren ser og kan endre —
        // steg 2 hoppes aldri over automatisk.
        setSlag(k.kategori === 'gruppe' ? 'gruppe' : 'virksomhet');
        setNyttNavn(k.foreslattTekst);
        setBrregSok(k.foreslattTekst);
        return Promise.all([api.hentRettskilde(k.rettskildeId), api.hentNoder(k.rettskildeId)]);
      })
      .then(([r, n]) => {
        setRettskilde(r);
        setNoder(n);
      })
      .catch((e) => setLasteFeil(e instanceof ApiError ? e.message : 'Kunne ikke laste navnekandidaten.'));
  }, [id]);

  const node = useMemo(
    () => (noder && kandidat ? noder.find((n) => n.eid === kandidat.nodeEid) ?? null : null),
    [noder, kandidat],
  );

  /** Treffet flytter seg når teksten er rettet — vis markeringen der den NYE teksten faktisk står,
   * ikke på det gamle intervallet. Finner vi den ikke, faller vi tilbake til sveipets offsets. */
  const markering = useMemo(() => {
    if (!kandidat || !node?.tekst) return null;
    const funnet = node.tekst.indexOf(kandidat.foreslattTekst);
    return funnet >= 0
      ? { start: funnet, slutt: funnet + kandidat.foreslattTekst.length }
      : { start: kandidat.startOffset, slutt: kandidat.endOffset };
  }, [kandidat, node]);

  /** Gruppebegrepene lastes FØRST når NOEN av de tre stedene som trenger dem faktisk er valgt — ikke
   * ved sidelast. De aller fleste kandidatene trenger ingen av dem, og et kall ingen av dem trenger er
   * et kall som ikke skal gjøres.
   * <p>[UTVIDET, «alle mekanismer»-runden, 2026-09-21, issue #283] Var scopet KUN til
   * `slag === 'gruppemedlem'` — nå ogSÅ tillegg==='rolle' (steg 4, AC6: rollebegrepet er et FRITT
   * valgt gruppebegrep) og slag==='gruppe' med gruppe-av-gruppe-tillegget valgt (AC9: den OVERORDNEDE
   * gruppen er også et gruppebegrep). Samme lastede liste gjenbrukes for alle tre — de er samme
   * underliggende data (alle gruppebegrep), bare tre ulike BRUKssteder.</p> */
  const trengerGruppebegrepliste = slag === 'gruppemedlem' || tillegg === 'rolle' || (slag === 'gruppe' && erGruppeAvGruppe);
  useEffect(() => {
    if (!trengerGruppebegrepliste || gruppebegrep !== null) return;
    api.hentGruppebegrep()
      .then(setGruppebegrep)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Kunne ikke laste gruppebegrepene.'));
  }, [trengerGruppebegrepliste, gruppebegrep]);

  /** [Ny, «alle mekanismer»-runden, 2026-09-21, issue #283 AC7] Relasjonstypene lastes FØRST når
   * «Relasjon til annen virksomhet»-tillegget faktisk er valgt — samme lat-lasting-begrunnelse som
   * gruppebegrep over. */
  useEffect(() => {
    if (tillegg !== 'relasjon' || relasjonstyper !== null) return;
    api.hentRelasjonstyper()
      .then(setRelasjonstyper)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Kunne ikke laste relasjonstypene.'));
  }, [tillegg, relasjonstyper]);

  /** Loven det VALGTE gruppebegrepet er hjemlet i — ÉN rettskilde, hentet etter valget. Se
   * kommentaren på `valgtGruppeLovTittel` for hvorfor ikke hele rettskildelista lastes på forhånd. */
  useEffect(() => {
    setValgtGruppeLovTittel(null);
    if (!valgtGruppeBegrepId) return;
    const lovkildeId = gruppebegrep?.find((g) => g.id === valgtGruppeBegrepId)?.lovkildeId;
    if (!lovkildeId) return;
    let avbrutt = false;
    api.hentRettskilde(lovkildeId)
      .then((r) => { if (!avbrutt) setValgtGruppeLovTittel(r.tittel); })
      // Lovtittelen er PYNT på et valg som allerede er gjort — mangler den, vises termen alene
      // heller enn at hele steget feiler på den.
      .catch(() => { /* stille: se over */ });
    return () => { avbrutt = true; };
  }, [valgtGruppeBegrepId, gruppebegrep]);

  const valgtGruppe = gruppebegrep?.find((g) => g.id === valgtGruppeBegrepId) ?? null;

  // [Ny, «alle mekanismer»-runden, 2026-09-21, issue #283 AC6] Paragraf-kandidater for
  // rolletillegget — samme filter som `LeggTilMyndighetstildelingForm.tsx`, men scopet til DENNE
  // rettskildens noder (`noder` er alt lastet for kontekst-kortet) i stedet for en separat lastet
  // lov: kandidatens rettskilde ER hjemmelen her, det er ikke et valg.
  const paragrafKandidaterForRolle = (noder ?? []).filter(
    (n) => n.nodeType === 'side' || (n.nodeType !== 'kapittel' && n.nummer),
  );
  function visRolleNodeKort(eid: string): string {
    const funnetNode = (noder ?? []).find((n) => n.eid === eid);
    if (!funnetNode) return eid;
    if (funnetNode.nodeType === 'side') return 'Hele siden';
    return funnetNode.nummer ? `§ ${funnetNode.nummer}` : eid;
  }
  const valgtRolle = gruppebegrep?.find((g) => g.id === valgtRolleBegrepId) ?? null;
  const valgtMotpart = virksomheter.find((v) => v.id === motpartVirksomhetId) ?? null;

  async function lagreTekst() {
    if (!id || !kandidat) return;
    const ny = tekst.trim();
    if (ny === kandidat.foreslattTekst) { setSteg(2); return; } // ingen endring — ingen unødvendig skriving.
    setLagrerTekst(true);
    setFeil(null);
    try {
      const oppdatert = await api.oppdaterNavnekandidat(id, { foreslattTekst: ny });
      setKandidat(oppdatert);
      setTekst(oppdatert.foreslattTekst);
      setNyttNavn(oppdatert.foreslattTekst);
      setBrregSok(oppdatert.foreslattTekst);
      setSteg(2);
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke lagre den rettede teksten.');
    } finally {
      setLagrerTekst(false);
    }
  }

  async function søkBrreg() {
    if (!brregSok.trim()) return;
    setBrregSoker(true);
    setFeil(null);
    try {
      setBrregTreff(await api.sokBrreg(brregSok.trim()));
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Brreg-søket feilet.');
      setBrregTreff(null);
    } finally {
      setBrregSoker(false);
    }
  }

  /** Oppretter virksomheten (Brreg eller kun navn) og VELGER den — steg 3 er ikke ferdig før en
   * konkret virksomhet er valgt, uansett hvilken vei man kom dit. */
  async function opprettOgVelg(fra: 'brreg' | 'kunNavn', organisasjonsnummer?: string) {
    setOppretterVirksomhet(true);
    setFeil(null);
    try {
      const ny = fra === 'brreg'
        ? await api.opprettVirksomhetFraBrreg(organisasjonsnummer!)
        : await api.opprettVirksomhet({ navn: nyttNavn.trim(), overordnetEnhetId: null });
      await oppdaterVirksomheter();
      setValgtVirksomhetId(ny.id);
      setVei('eksisterende'); // den er nå nettopp det — en eksisterende, valgt virksomhet.
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke opprette virksomheten.');
    } finally {
      setOppretterVirksomhet(false);
    }
  }

  /** Steg 2-utfallene som avslutter veiviseren uten å gå via steg 3. */
  async function fullførIkkeVirksomhet(valg: 'gruppe' | 'irrelevant') {
    if (!id || !kandidat) return;
    setFullfører(true);
    setFeil(null);
    try {
      if (valg === 'gruppe') {
        // [Ny, «alle mekanismer»-runden, 2026-09-21, issue #283 AC9] Gruppe-av-gruppe-tillegget bruker
        // et ANNET endepunkt — klienten kjenner ikke det nye gruppebegrepets id på forhånd, se
        // KoblTilGruppeAvGruppeAsync. Den vanlige veien under (else-grenen) er BEVISST HELT UENDRET
        // (AC5: «ingen regresjon») for det store flertallet uten noe gruppe-av-gruppe-forhold.
        if (erGruppeAvGruppe && valgtOverordnetGruppeBegrepId) {
          const resultat = await api.koblNavnekandidatTilGruppeAvGruppe(id, {
            overordnetGruppeBegrepId: valgtOverordnetGruppeBegrepId,
          });
          const overordnetTerm = gruppebegrep?.find((g) => g.id === valgtOverordnetGruppeBegrepId)?.term
            ?? 'den valgte gruppen';
          setFerdig({
            tittel: `«${kandidat.foreslattTekst}» er opprettet som gruppebegrep.`,
            detaljer: [
              'Gruppebegrepet er hjemlet i denne rettskilden (navn + lov utgjør identiteten).',
              `Gruppen er registrert som medlem av «${overordnetTerm}», hjemlet i denne rettskilden — `
                + 'det er her medlemskapet står (issue #283 AC9).',
              'Tekst-taggen for forekomsten er koblet til det nye gruppebegrepet.',
              'Navnekandidaten er satt til «Godkjent».',
            ],
            rettskildeLenke: rettskildeLenkeForId(kandidat.rettskildeId, kandidat.nodeEid),
            taggLag: 'Begrep',
            virksomhetLenke: null,
            virksomhetForSveip: null,
            // [Ny] Til forskjell fra den uendrede grenen under (som aldri viste denne lenken for
            // gruppe-veien) peker denne på det NYE gruppebegrepets EGEN side — der ser man at det
            // faktisk er registrert som medlem av den overordnede gruppen.
            gruppeLenke: `/begreper/${resultat.gruppebegrep.id}`,
            advarsel: null,
          });
          return;
        }

        // [Ny, issue #298 AC3] Fast, nasjonalt begrep — ETT ANNET endepunkt, get-or-create server-side
        // (gjenbruker en eksisterende fast rad med samme Term i stedet for å opprette en dublett, se
        // VirksomhetsbegrepTjeneste.OpprettEllerGjenbrukFastGruppebegrepAsync). Samme "egen gren, ikke
        // en utvidelse av den vanlige veien"-begrunnelse som gruppe-av-gruppe-grenen over.
        if (gruppeScope === 'fast') {
          const resultat = await api.godkjennNavnekandidatSomFastGruppebegrep(id);
          setFerdig({
            tittel: resultat.varNyttBegrep
              ? `«${kandidat.foreslattTekst}» er opprettet som fast, nasjonalt gruppebegrep.`
              : `«${kandidat.foreslattTekst}» er koblet til et eksisterende fast, nasjonalt gruppebegrep.`,
            detaljer: [
              resultat.varNyttBegrep
                ? 'Gruppebegrepet er FAST og nasjonalt — det har ingen lovkilde, og gjenbrukes på tvers av alle lover.'
                : 'Et fast gruppebegrep med nøyaktig denne teksten fantes allerede — kandidaten er koblet '
                  + 'til DEN eksisterende raden i stedet for å opprette en ny (samme begrep, ikke en dublett).',
              'Tekst-taggen for forekomsten er koblet til gruppebegrepet.',
              'Navnekandidaten er satt til «Godkjent».',
            ],
            rettskildeLenke: rettskildeLenkeForId(kandidat.rettskildeId, kandidat.nodeEid),
            taggLag: 'Begrep',
            virksomhetLenke: null,
            virksomhetForSveip: null,
            gruppeLenke: `/begreper/${resultat.gruppebegrep.id}`,
            advarsel: null,
          });
          return;
        }

        // BEVISST helt uendret vei: samme GodkjennAsync som dagens fungerende hurtig-Godkjenn
        // (oppretter Begrep(gruppe) + tagg m/RefId). Ingen regresjon her (AK5).
        await api.godkjennNavnekandidat(id);
        setFerdig({
          tittel: `«${kandidat.foreslattTekst}» er opprettet som gruppebegrep.`,
          detaljer: [
            'Gruppebegrepet er hjemlet i denne rettskilden (navn + lov utgjør identiteten).',
            'Tekst-taggen for forekomsten er koblet til det nye gruppebegrepet.',
            'Navnekandidaten er satt til «Godkjent».',
          ],
          rettskildeLenke: rettskildeLenkeForId(kandidat.rettskildeId, kandidat.nodeEid),
          // Gruppe-veien lager en 'begrep'-tagg mot gruppebegrepet — altså laget «Begrep»,
          // IKKE «Virksomhet». Se GodkjennAsync.
          taggLag: 'Begrep',
          virksomhetLenke: null,
          virksomhetForSveip: null,
          gruppeLenke: null,
          advarsel: null,
        });
      } else {
        await api.avvisNavnekandidat(id);
        setFerdig({
          tittel: `«${kandidat.foreslattTekst}» er avvist.`,
          detaljer: ['Navnekandidaten er satt til «Avvist», og det er ikke opprettet noe innhold.'],
          rettskildeLenke: null,
          taggLag: null,
          virksomhetLenke: null,
          virksomhetForSveip: null,
          gruppeLenke: null,
          advarsel: null,
        });
      }
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Handlingen feilet.');
    } finally {
      setFullfører(false);
    }
  }

  /**
   * Steg 5: lukker kjeden.
   * <p>Gruppemedlem-sporet går ALLTID via `kobl-til-gruppemedlemskap` (det er sporets EGET
   * primærutfall). Det rene virksomhet-sporet går via `kobl-til-virksomhet` NÅR intet steg 4-tillegg
   * er valgt — ER et tillegg valgt der, gjør TILLEGGETS eget endepunkt HELE kjedelukkingen selv
   * (samme idempotente `LukkKjedenMotVirksomhetAsync` alle disse endepunktene deler server-side), så
   * et separat, rent redundant `kobl-til-virksomhet`-kall gjøres IKKE i tillegg.</p>
   * <p>[Ny, «alle mekanismer»-runden, 2026-09-21, issue #283 AC5/AC6/AC7/AC8] Steg 4-tillegget
   * (`tillegg`) er en UAVHENGIG tilleggsopplysning, ikke en erstatning for gruppemedlem-sporet — er
   * BÅDE `slag==='gruppemedlem'` OG et tillegg valgt, gjøres BEGGE kall.</p>
   */
  async function fullførVirksomhet() {
    if (!id || !kandidat || !valgtVirksomhetId) return;
    if (slag === 'gruppemedlem' && !valgtGruppeBegrepId) return;
    if (!tilleggKlart) return;
    setFullfører(true);
    setFeil(null);
    try {
      const primær = slag === 'gruppemedlem'
        ? await api.koblNavnekandidatTilGruppemedlemskap(id, {
          virksomhetId: valgtVirksomhetId,
          gruppeBegrepId: valgtGruppeBegrepId,
          navneformgrunn,
        })
        : tillegg === 'ingen'
          ? await api.koblNavnekandidatTilVirksomhet(id, {
            virksomhetId: valgtVirksomhetId,
            navneformgrunn,
          })
          : null;

      const rolleResultat = tillegg === 'rolle'
        ? await api.koblNavnekandidatTilMyndighetstildeling(id, {
          virksomhetId: valgtVirksomhetId,
          rolleBegrepId: valgtRolleBegrepId,
          paragrafspenn: [{ fraEid: rolleFraEid.trim(), tilEid: rolleTilEid.trim() || null }],
          vilkaar: rolleVilkaar.trim() || null,
          navneformgrunn,
        })
        : null;

      const relasjonResultat = tillegg === 'relasjon'
        ? await api.koblNavnekandidatTilRelasjon(id, {
          virksomhetId: valgtVirksomhetId,
          navneformgrunn,
          motpartVirksomhetId,
          relasjonsType,
          hjemletHer: relasjonHjemletHer,
          kommentar: relasjonHjemletHer ? null : (relasjonKommentar.trim() || null),
        })
        : null;

      // Alle fire mulige resultater deler samme fem grunnfelt (kandidat/navneform/taggId/
      // rettskildeId/nodeEid) — se DTO-ene i api/types.ts. Hvilket av dem som faktisk ble kalt
      // avgjøres av slag/tillegg over; nøyaktig ETT av dem er alltid satt (aldri alle null, sperret
      // av validerings-guardene øverst i funksjonen).
      const resultat = primær ?? rolleResultat ?? relasjonResultat;
      if (!resultat) return; // uoppnåelig gitt guardene over — TS krever likevel en eksplisitt sjekk.

      const virksomhetNavn = virksomheter.find((v) => v.id === valgtVirksomhetId)?.visningsnavn ?? 'virksomheten';
      setFerdig({
        tittel: `«${resultat.navneform.term}» er nå en navneform for ${virksomhetNavn}.`,
        detaljer: [
          navneformgrunn
            ? `Begrunnelsen er lagret som «${navneformgrunn}».`
            : 'Ingen begrunnelse er satt (uspesifisert) — den kan settes senere på virksomhetens side.',
          ...(slag === 'gruppemedlem' && valgtGruppe
            ? [
              `${virksomhetNavn} er registrert som medlem av «${valgtGruppe.term}», hjemlet i `
              + 'denne rettskilden — det er her navnet står.',
            ]
            : []),
          ...(rolleResultat && valgtRolle
            ? [
              `${virksomhetNavn} er tildelt rollen «${valgtRolle.term}» her, hjemlet i denne `
              + 'rettskilden (issue #283 AC6).',
            ]
            : []),
          ...(relasjonResultat && valgtMotpart
            ? [
              relasjonHjemletHer
                ? `Relasjonen «${relasjonsType}» til ${valgtMotpart.visningsnavn} er registrert, `
                  + 'hjemlet i denne rettskilden (issue #283 AC7/AC8).'
                : `Relasjonen «${relasjonsType}» til ${valgtMotpart.visningsnavn} er registrert, uten `
                  + 'formell hjemmel — kun kommentaren.',
            ]
            : []),
          'Navnekandidaten er satt til «Godkjent».',
          // [ENDRET, navneform-kjede-runden, 2026-09-08] Sa tidligere at taggen «peker nå på
          // virksomheten». Det er ikke lenger sant, og docs/09 §15 er eksplisitt om at påstandene i
          // denne oppsummeringen må FØLGE utfallet: taggen peker på NAVNEFORMEN, og virksomheten nås
          // gjennom den. Se TekstTaggEntitet.RefId.
          resultat.taggId
            ? `Tekst-taggen for forekomsten peker nå på navneformen «${resultat.navneform.term}», som `
              + `igjen peker på ${virksomhetNavn}. Hele kjeden er synlig i rettskilden under laget «Virksomhet».`
            : 'Ingen tekst-tagg ble opprettet — se advarselen under.',
        ],
        rettskildeLenke: resultat.taggId
          ? rettskildeLenkeForId(resultat.rettskildeId, resultat.nodeEid)
          : null,
        taggLag: resultat.taggId ? 'Virksomhet' : null,
        virksomhetLenke: `/virksomheter/${valgtVirksomhetId}`,
        virksomhetForSveip: { id: valgtVirksomhetId, navn: virksomhetNavn },
        gruppeLenke: slag === 'gruppemedlem' && valgtGruppeBegrepId ? `/begreper/${valgtGruppeBegrepId}` : null,
        // Den dokumenterte degraderingen skal VISES, ikke skjules.
        advarsel: resultat.taggId
          ? null
          : 'Navneformen er koblet, men det ble ikke opprettet noen tagg i rettskildeteksten. Det skjer '
            + 'når rettskilden ikke har et ansvarlig departement som finnes i virksomhetskatalogen (en tagg '
            + 'må eies av en virksomhet), eller når tegnposisjonene ikke lenger stemmer fordi rettskilden er '
            + 'endret siden sveipet.',
      });
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke fullføre koblingen.');
    } finally {
      setFullfører(false);
    }
  }

  if (lasteFeil) {
    return (
      <>
        <Heading level={1} data-size="lg" style={{ marginBottom: '1rem' }}>Behandle navnekandidat</Heading>
        <Alert data-color="danger">{lasteFeil}</Alert>
      </>
    );
  }
  if (!kandidat) return <Spinner aria-label="Laster navnekandidaten …" />;

  const valgtVirksomhet = virksomheter.find((v) => v.id === valgtVirksomhetId) ?? null;

  /** Steg 3 er ferdig når virksomheten er valgt — OG, på gruppemedlem-veien, gruppen også. Ett felt
   * som mangler skal stoppe «Neste», ikke bli en 400 fra endepunktet ved fullføring.
   * [ENDRET, «alle mekanismer»-runden, 2026-09-21, issue #283] Omdøpt fra `steg4Klart` — «steg 4» er
   * nå det NYE tillegg-steget (se `tilleggKlart` under), ikke virksomhetsvalget lenger. */
  const steg3Klart = valgtVirksomhetId !== ''
    && (slag !== 'gruppemedlem' || valgtGruppeBegrepId !== '');

  /** [Ny, «alle mekanismer»-runden, 2026-09-21, issue #283 AC5/AC6/AC7] Steg 4 er ferdig når det
   * valgte tillegget har det det trenger — eller når intet tillegg er valgt (default, alltid klart). */
  const tilleggKlart = tillegg === 'ingen'
    || (tillegg === 'rolle' && valgtRolleBegrepId !== '' && rolleFraEid.trim() !== '')
    || (tillegg === 'relasjon' && motpartVirksomhetId !== '' && relasjonsType !== ''
        && (relasjonHjemletHer || relasjonKommentar.trim() !== ''));

  return (
    <>
      <Breadcrumbs data-size="sm" style={{ marginBottom: '0.75rem' }}>
        <Breadcrumbs.List>
          <Breadcrumbs.Item>
            <Breadcrumbs.Link asChild><RouterLink to="/navnekandidater">Navnekandidater</RouterLink></Breadcrumbs.Link>
          </Breadcrumbs.Item>
          <Breadcrumbs.Item>
            <Breadcrumbs.Link aria-current="page">Behandle «{kandidat.foreslattTekst}»</Breadcrumbs.Link>
          </Breadcrumbs.Item>
        </Breadcrumbs.List>
      </Breadcrumbs>

      <Heading level={1} data-size="lg" style={{ marginBottom: '0.5rem' }}>
        Behandle «{kandidat.foreslattTekst}»
      </Heading>
      <Paragraph style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Tag data-size="sm" data-color="neutral">Kategori: {kandidat.kategori}</Tag>
        <Tag data-size="sm" data-color={kandidat.status === 'Godkjent' ? 'success' : kandidat.status === 'Avvist' ? 'danger' : 'info'}>
          {kandidat.status}
        </Tag>
      </Paragraph>

      {/* Steg-indikator — samme Tag-rekke-idiom som StatusStepper (docs/09 §14), men egen, siden
        * StatusStepper er bundet til den 6-trinns entitets-statusmodellen. */}
      <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', alignItems: 'center', marginBottom: '1rem' }}>
        {stegTitler(slag).map((tittel, i) => (
          <Tag
            key={tittel}
            data-size="sm"
            data-color={i + 1 === steg ? 'accent' : 'neutral'}
            variant={i + 1 > steg ? 'outline' : 'default'}
          >
            {i + 1}. {tittel}
          </Tag>
        ))}
      </div>

      {ferdig ? (
        <Card style={{ padding: '1rem' }}>
          <Alert data-color="success" style={{ marginBottom: '0.75rem' }}>{ferdig.tittel}</Alert>
          {ferdig.advarsel && (
            <Alert data-color="warning" style={{ marginBottom: '0.75rem' }}>{ferdig.advarsel}</Alert>
          )}
          <Heading level={2} data-size="xs" style={{ marginBottom: '0.35rem' }}>Dette skjedde</Heading>
          <ul style={{ margin: '0 0 1rem 1.1rem', padding: 0 }}>
            {ferdig.detaljer.map((d) => (
              <Metatekst as="li" key={d} style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>{d}</Metatekst>
            ))}
          </ul>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            {ferdig.rettskildeLenke && (
              <Button data-size="sm" asChild>
                <RouterLink to={ferdig.rettskildeLenke}>Se i rettskilden ↗</RouterLink>
              </Button>
            )}
            {ferdig.virksomhetLenke && (
              <Button data-size="sm" variant="secondary" asChild>
                <RouterLink to={ferdig.virksomhetLenke}>Se virksomheten ↗</RouterLink>
              </Button>
            )}
            {ferdig.gruppeLenke && (
              <Button data-size="sm" variant="secondary" asChild>
                <RouterLink to={ferdig.gruppeLenke}>Se gruppen og medlemmene ↗</RouterLink>
              </Button>
            )}
            <Button data-size="sm" variant="tertiary" onClick={() => navigate('/navnekandidater')}>
              Tilbake til navnekandidater
            </Button>
          </div>

          {/* [Ny, nemnd/sekretariat-runden, 2026-09-09] Neste handling, ikke bare en lenke: denne
            * veiviseren har tagget ÉN forekomst — den kandidaten ble funnet i. De øvrige
            * forekomstene av samme navn i korpuset er den ANDRE køens jobb, og det er ikke rimelig
            * å forvente at saksbehandleren vet det. Se KandidatflytForklaring for skillet. */}
          {ferdig.virksomhetForSveip && (
            <Card style={{ padding: '0.85rem', marginTop: '1rem' }}>
              <Heading level={2} data-size="xs" style={{ marginBottom: '0.35rem' }}>
                Neste: finn de øvrige forekomstene
              </Heading>
              <Paragraph data-size="sm" style={{ marginBottom: '0.6rem' }}>
                Nå er ÉN forekomst tagget — den kandidaten ble funnet i. Et virksomhetssveip leter
                gjennom hele korpuset etter alle navneformene til {ferdig.virksomhetForSveip.navn} og
                legger hvert treff i virksomhetskandidat-køen, der du kan massegodkjenne dem.
              </Paragraph>
              {sveipFeil && <Alert data-color="danger" style={{ marginBottom: '0.6rem' }}>{sveipFeil}</Alert>}
              {sveipResultat && (
                <Alert data-color="info" style={{ marginBottom: '0.6rem' }}>
                  {sveipResultat.funnet} treff funnet, {sveipResultat.nye} nye kandidater lagt i køen.
                  {sveipResultat.nye === 0 && ' Ingen nye betyr at treffene alt er behandlet tidligere.'}
                </Alert>
              )}
              <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                <Button
                  data-size="sm"
                  disabled={sveiper}
                  onClick={() => kjorVirksomhetssveip(ferdig.virksomhetForSveip!.id)}
                >
                  {sveiper ? 'Sveiper …' : 'Kjør virksomhetssveip'}
                </Button>
                <Button data-size="sm" variant="secondary" asChild>
                  <RouterLink to={`/virksomhet-kandidater?virksomhetId=${ferdig.virksomhetForSveip.id}`}>
                    Åpne virksomhetskandidater ↗
                  </RouterLink>
                </Button>
              </div>
            </Card>
          )}
          {ferdig.rettskildeLenke && ferdig.taggLag && (
            <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.75rem', marginBottom: 0 }}>
              Taggen ligger i laget «{ferdig.taggLag}» i tagg-velgeren over lovteksten — velg det
              laget for å se markeringen.
            </Metatekst>
          )}
        </Card>
      ) : (
        <>
          {/* ---------------- Steg 0: Kontekst ---------------- */}
          <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
            <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>Slik står treffet</Heading>
            <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
              Slik står treffet i rettskilden. Les setningen før du bestemmer deg — er treffet feil
              avgrenset, rettes teksten i neste steg.
            </Metatekst>

            <Table data-size="sm" style={{ marginBottom: '0.75rem', width: '100%' }}>
              <Table.Body>
                <Table.Row>
                  <Table.HeaderCell scope="row">Rettskilde</Table.HeaderCell>
                  <Table.Cell>
                    {rettskilde ? (
                      <Link asChild>
                        <RouterLink to={rettskildeLenkeForId(kandidat.rettskildeId, kandidat.nodeEid)}>
                          {rettskilde.tittel} ↗
                        </RouterLink>
                      </Link>
                    ) : <Spinner aria-label="Laster …" data-size="xs" />}
                  </Table.Cell>
                </Table.Row>
                <Table.Row>
                  <Table.HeaderCell scope="row">Node</Table.HeaderCell>
                  <Table.Cell>
                    <Metatekst as="span" style={{ fontFamily: 'var(--ds-font-family-mono, monospace)' }}>
                      {kandidat.nodeEid}
                    </Metatekst>
                  </Table.Cell>
                </Table.Row>
                <Table.Row>
                  <Table.HeaderCell scope="row">Ansvarlig departement</Table.HeaderCell>
                  <Table.Cell>
                    {/* «Ikke kjent» er en PÅSTAND om rettskilden, og skal derfor ikke vises mens
                      * rettskilden fortsatt lastes — da er svaret ikke «ukjent», bare ikke kommet
                      * ennå. Samme spinner-mens-laster som Rettskilde-raden over. */}
                    {rettskilde === null
                      ? <Spinner aria-label="Laster …" data-size="xs" />
                      : rettskilde.ansvarligDepartement?.length
                        ? rettskilde.ansvarligDepartement.join(', ')
                        : <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Ikke kjent</span>}
                  </Table.Cell>
                </Table.Row>
                <Table.Row>
                  <Table.HeaderCell scope="row">Ekstern berikelse</Table.HeaderCell>
                  <Table.Cell><BerikelseVisning k={kandidat} /></Table.Cell>
                </Table.Row>
                {/* [Ny, konfidens-runden, 2026-09-09] Konfidensen ER grunnen til at raden ligger
                  * her og ikke er avvist. Grunnen skrives ut i klartekst, ikke bare som en
                  * merkelapp med hover: «Lav» uten hvorfor er ikke handlingsrettet. */}
                <Table.Row>
                  <Table.HeaderCell scope="row">Konfidens</Table.HeaderCell>
                  <Table.Cell>
                    {kandidat.konfidens ? (
                      <span style={{ display: 'flex', gap: '0.5rem', alignItems: 'baseline', flexWrap: 'wrap' }}>
                        <KonfidensTag konfidens={kandidat.konfidens} grunn={kandidat.konfidensGrunn} />
                        <Metatekst as="span" style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
                          {konfidensGrunnTekst(kandidat.konfidensGrunn)}
                        </Metatekst>
                      </span>
                    ) : (
                      <Metatekst as="span" style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
                        Ikke klassifisert — gruppe-kandidater sendes aldri til SNL/SSR.
                      </Metatekst>
                    )}
                  </Table.Cell>
                </Table.Row>
              </Table.Body>
            </Table>

            <Heading level={3} data-size="xs" style={{ marginBottom: '0.35rem' }}>Setningen i rettskilden</Heading>
            <Card style={{ padding: '0.75rem', background: 'var(--ds-color-neutral-surface-tinted)' }}>
              {node?.tekst && markering
                ? <TreffIKontekst tekst={node.tekst} start={markering.start} slutt={markering.slutt} />
                : noder === null
                  ? <Spinner aria-label="Laster nodeteksten …" data-size="sm" />
                  : (
                    <Paragraph data-size="sm" style={{ margin: 0 }}>
                      Fant ikke noden «{kandidat.nodeEid}» i rettskilden. Den kan være fjernet ved en
                      reimport — teksten kan derfor ikke vises, og det blir ikke opprettet noen tagg.
                    </Paragraph>
                  )}
            </Card>

          </Card>

          {/* ---------------- Steg 1: Er teksten riktig? ---------------- */}
          {steg >= 1 && (
            <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>1. Er teksten riktig?</Heading>
              <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                Rett teksten når sveipet har tatt med tegn som ikke hører til navnet («Ø Suldal
                kommune», der Ø kommer fra en koordinat rett foran), eller når mønsteret har KUTTET
                navnet for kort — «Reguleringsmyndigheten» der loven skriver «Reguleringsmyndigheten
                for energi». Begge er artefakter fra mønsteret, ikke opplysninger om navnet.
              </Metatekst>
              {/* [Ny, konfidens-runden, 2026-09-09] Dette er ikke en detalj: uten at posisjonene
                * følger teksten ville taggen sitert de opprinnelige 22 tegnene mens navneformen
                * hadde 33. Se ReankreTilNyTekstAsync. */}
              <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                Skriver du en tekst som FINNES i setningen over, flyttes tegnposisjonene dit, slik at
                taggen dekker hele navnet. Retter du en skrivemåte som ikke står slik i loven
                («Matilsynet» → «Mattilsynet»), står posisjonene igjen på det som faktisk står — og da
                er en begrunnelse («feilskriving») den riktige mekanismen, ikke en rettet tekst.
              </Metatekst>
              <Textfield
                data-size="sm"
                label="Foreslått tekst"
                value={tekst}
                onChange={(e) => setTekst(e.target.value)}
                readOnly={steg !== 1}
                style={{ maxWidth: '28rem', marginBottom: '0.75rem' }}
              />
              {kandidat.status === 'Avvist' && steg === 1 && (
                <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.75rem' }}>
                  Denne raden er avvist. Lagrer du en rettet tekst, settes den tilbake til «Venter»
                  slik at den kan behandles på nytt.
                </Alert>
              )}
              {steg === 1 && (
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <Button data-size="sm" onClick={lagreTekst} disabled={lagrerTekst || !tekst.trim()}>
                    {lagrerTekst ? 'Lagrer …' : tekst.trim() === kandidat.foreslattTekst ? 'Teksten er riktig — neste' : 'Lagre rettet tekst'}
                  </Button>
                  <Button data-size="sm" variant="tertiary" onClick={() => navigate('/navnekandidater')}>Avbryt</Button>
                </div>
              )}
            </Card>
          )}

          {/* ---------------- Steg 2: Hva slags ting er dette? ---------------- */}
          {steg >= 2 && (
            <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>2. Hva slags ting er dette?</Heading>
              <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                «Administrativ inndeling» er bevisst ikke med i denne runden — velg «Ikke relevant»
                hvis treffet er det, og ta det opp separat.
              </Metatekst>
              <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
                <Radio
                  name="slag"
                  label="Konkret virksomhet"
                  description="Ett navngitt organ/aktør, f.eks. Arkivverket eller Suldal kommune."
                  value="virksomhet"
                  checked={slag === 'virksomhet'}
                  onChange={() => setSlag('virksomhet')}
                  disabled={steg !== 2}
                />
                {/* [Ny, gruppemedlemskap-runden, 2026-09-08, issue #164] Johanns eget eksempel står i
                  * beskrivelsen: det er nettopp «Karasjok i forskriften» som ikke lot seg behandle før
                  * denne veien fantes — den er en virksomhet OG et gruppemedlemskap, ikke ett av dem. */}
                <Radio
                  name="slag"
                  label="Konkret virksomhet, navngitt som medlem av en gruppe"
                  description="Teksten navngir en virksomhet i egenskap av å tilhøre en gruppe loven har definert — f.eks. «Karasjok» som språkutviklingskommune. Oppretter både navneformen og medlemskapet."
                  value="gruppemedlem"
                  checked={slag === 'gruppemedlem'}
                  onChange={() => setSlag('gruppemedlem')}
                  disabled={steg !== 2}
                />
                <Radio
                  name="slag"
                  label="Gruppe som defineres her"
                  description="En rolle/gruppe loven selv definerer, f.eks. «forurensningsmyndighet»."
                  value="gruppe"
                  checked={slag === 'gruppe'}
                  onChange={() => setSlag('gruppe')}
                  disabled={steg !== 2}
                />
                <Radio
                  name="slag"
                  label="Ikke relevant"
                  description="Ikke en aktør — eller noe denne runden ikke håndterer. Raden avvises."
                  value="irrelevant"
                  checked={slag === 'irrelevant'}
                  onChange={() => setSlag('irrelevant')}
                  disabled={steg !== 2}
                />
              </Field>

              {/* [Ny, issue #298 AC3] Saksbehandlerens eksplisitte valg: lovspesifikt (dagens
                * oppførsel, fortsatt default — AC5 «ingen regresjon») eller fast/nasjonalt gruppebegrep
                * uten lovscoping. Gruppe-av-gruppe-tillegget rett under gjelder KUN lovspesifikt
                * (KoblTilGruppeAvGruppeAsync oppretter alltid et lovspesifikt gruppebegrep server-side)
                * — vist/relevant bare når den scopen er valgt. */}
              {slag === 'gruppe' && steg === 2 && (
                <div style={{ marginBottom: '0.75rem' }}>
                  <Divider style={{ margin: '0.75rem 0' }} />
                  <Heading level={3} data-size="xs" style={{ marginBottom: '0.35rem' }}>
                    Lovspesifikt eller fast, nasjonalt begrep?
                  </Heading>
                  <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
                    Et lovspesifikt gruppebegrep hører til DENNE loven — samme navn i en annen lov blir
                    en egen rad. Et fast, nasjonalt begrep har ingen lov å høre til, f.eks. «Kongen»:
                    samme organ uansett hvilken lov som nevner det. Finnes det alt et fast begrep med
                    nøyaktig denne teksten, kobles kandidaten til DET i stedet for å opprette en ny rad.
                  </Metatekst>
                  <Field data-size="sm">
                    <Radio
                      name="gruppeScope" label="Lovspesifikt gruppebegrep for denne loven" value="lovspesifikt"
                      checked={gruppeScope === 'lovspesifikt'} onChange={() => setGruppeScope('lovspesifikt')}
                    />
                    <Radio
                      name="gruppeScope" label="Fast, nasjonalt begrep" value="fast"
                      checked={gruppeScope === 'fast'} onChange={() => setGruppeScope('fast')}
                    />
                  </Field>
                </div>
              )}

              {/* [Ny, «alle mekanismer»-runden, 2026-09-21, issue #283 AC9] Gruppe-av-gruppe-tillegget
                * — kun for gruppe-sporet, som ellers ikke har noe eget steg 3/4 å legge det i. KUN for
                * lovspesifikt (se kommentaren over). */}
              {slag === 'gruppe' && steg === 2 && gruppeScope === 'lovspesifikt' && (
                <div style={{ marginBottom: '0.75rem' }}>
                  <Divider style={{ margin: '0.75rem 0' }} />
                  <Heading level={3} data-size="xs" style={{ marginBottom: '0.35rem' }}>
                    Er denne gruppen selv medlem av en annen gruppe?
                  </Heading>
                  <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
                    Valgfritt — f.eks. at «språkutviklingskommuner» selv inngår i «forvaltningsområdet
                    for samiske språk». Den overordnede gruppen må finnes som gruppebegrep fra før.
                  </Metatekst>
                  <Field data-size="sm" style={{ marginBottom: '0.5rem' }}>
                    <Radio name="gruppeAvGruppe" label="Nei" value="nei"
                      checked={!erGruppeAvGruppe} onChange={() => setErGruppeAvGruppe(false)} />
                    <Radio name="gruppeAvGruppe" label="Ja" value="ja"
                      checked={erGruppeAvGruppe} onChange={() => setErGruppeAvGruppe(true)} />
                  </Field>
                  {erGruppeAvGruppe && (
                    gruppebegrep === null ? (
                      <Spinner aria-label="Laster gruppebegrepene …" data-size="sm" />
                    ) : gruppebegrep.length === 0 ? (
                      <Alert data-color="warning" data-size="sm">
                        Det finnes ingen andre gruppebegrep ennå — ingen overordnet gruppe å velge.
                      </Alert>
                    ) : (
                      <GruppebegrepVelger
                        gruppebegrep={gruppebegrep}
                        value={valgtOverordnetGruppeBegrepId}
                        onChange={setValgtOverordnetGruppeBegrepId}
                        label="Overordnet gruppe"
                        tomValgTekst="Velg overordnet gruppe …"
                        style={{ maxWidth: '28rem' }}
                      />
                    )
                  )}
                </div>
              )}

              {steg === 2 && (
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                  {harVirksomhetssteg(slag) && (
                    <Button data-size="sm" onClick={() => setSteg(3)}>Neste</Button>
                  )}
                  {slag === 'gruppe' && (
                    <Button
                      data-size="sm"
                      onClick={() => fullførIkkeVirksomhet('gruppe')}
                      disabled={fullfører || (gruppeScope === 'lovspesifikt' && erGruppeAvGruppe && !valgtOverordnetGruppeBegrepId)}
                    >
                      {fullfører
                        ? 'Oppretter …'
                        : gruppeScope === 'fast' ? 'Godkjenn som fast, nasjonalt begrep' : 'Opprett gruppebegrep og godkjenn'}
                    </Button>
                  )}
                  {slag === 'irrelevant' && (
                    <Button data-size="sm" data-color="danger" onClick={() => fullførIkkeVirksomhet('irrelevant')} disabled={fullfører}>
                      {fullfører ? 'Avviser …' : 'Avvis kandidaten'}
                    </Button>
                  )}
                  <Button data-size="sm" variant="tertiary" onClick={() => setSteg(1)}>Tilbake</Button>
                </div>
              )}
            </Card>
          )}

          {/* ---------------- Steg 3: Hvilken virksomhet? ---------------- */}
          {steg >= 3 && (
            <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
              {/* [Rettet, «alle mekanismer»-runden, 2026-09-21] Leste tidligere indeks [3] («Bekreft»)
                * i stedet for [2] (virksomhet-tittelen) — usynlig fordi teksten aldri ble vist alene,
                * kun sammen med det hardkodede «3. »-prefikset. Se `stegTitler`s kommentar. */}
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>
                3. {stegTitler(slag)[2]}
              </Heading>
              <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                Finn virksomheten i katalogen, eller opprett den — fra Brønnøysundregisteret hvis den
                er registrert der, ellers med bare navnet.
                {slag === 'gruppemedlem'
                  && ' Velg deretter hvilken gruppe teksten navngir den som medlem av.'}
              </Metatekst>

              <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
                <Radio name="vei" label="Velg fra katalogen" value="eksisterende"
                  checked={vei === 'eksisterende'} onChange={() => setVei('eksisterende')} disabled={steg !== 3} />
                <Radio name="vei" label="Søk i Brønnøysundregisteret og opprett" value="brreg"
                  checked={vei === 'brreg'} onChange={() => setVei('brreg')} disabled={steg !== 3} />
                <Radio name="vei" label="Opprett med bare navn" value="kunNavn"
                  description="For aktører uten egen Brreg-registrering, f.eks. Kystvakten som del av Forsvaret."
                  checked={vei === 'kunNavn'} onChange={() => setVei('kunNavn')} disabled={steg !== 3} />
              </Field>

              {vei === 'eksisterende' && (
                <VirksomhetVelger
                  virksomheter={virksomheter}
                  value={valgtVirksomhetId}
                  onChange={setValgtVirksomhetId}
                  label="Virksomhet"
                  tomValgTekst="Velg virksomhet …"
                  style={{ marginBottom: '0.75rem', maxWidth: '28rem' }}
                />
              )}

              {vei === 'brreg' && steg === 3 && (
                <div style={{ marginBottom: '0.75rem' }}>
                  <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '0.5rem', maxWidth: '28rem' }}>
                    <Search data-size="sm" style={{ flex: 1 }}>
                      <Search.Input
                        aria-label="Søk i Brønnøysundregisteret"
                        value={brregSok}
                        onChange={(e) => setBrregSok(e.target.value)}
                        onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void søkBrreg(); } }}
                      />
                      <Search.Clear />
                    </Search>
                    <Button data-size="sm" onClick={søkBrreg} disabled={brregSoker || !brregSok.trim()}>
                      {brregSoker ? 'Søker …' : 'Søk'}
                    </Button>
                  </div>
                  {/* Kortet rendres ALLTID når et søk er gjort — tom-tilstand som Paragraph inni (docs/09 §14). */}
                  {brregTreff !== null && (
                    <Card style={{ padding: brregTreff.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
                      {brregTreff.length === 0 ? (
                        <Paragraph data-size="sm" style={{ margin: 0 }}>Ingen treff i Brreg på «{brregSok}».</Paragraph>
                      ) : (
                        <Table data-size="sm" data-density="compact" style={{ width: '100%' }}>
                          <Table.Head>
                            <Table.Row>
                              <Table.HeaderCell>Navn</Table.HeaderCell>
                              <Table.HeaderCell>Org.nr.</Table.HeaderCell>
                              <Table.HeaderCell>Form</Table.HeaderCell>
                              <Table.HeaderCell />
                            </Table.Row>
                          </Table.Head>
                          <Table.Body>
                            {brregTreff.map((e) => (
                              <Table.Row key={e.organisasjonsnummer}>
                                <Table.Cell>{e.navn}</Table.Cell>
                                <Table.Cell>{e.organisasjonsnummer}</Table.Cell>
                                <Table.Cell>{e.organisasjonsformBeskrivelse ?? e.organisasjonsformKode ?? '—'}</Table.Cell>
                                <Table.Cell>
                                  <Button
                                    data-size="sm"
                                    variant="secondary"
                                    onClick={() => opprettOgVelg('brreg', e.organisasjonsnummer)}
                                    disabled={oppretterVirksomhet}
                                  >
                                    Opprett og velg
                                  </Button>
                                </Table.Cell>
                              </Table.Row>
                            ))}
                          </Table.Body>
                        </Table>
                      )}
                    </Card>
                  )}
                </div>
              )}

              {vei === 'kunNavn' && steg === 3 && (
                <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
                  <Textfield
                    data-size="sm"
                    label="Navn på virksomheten"
                    value={nyttNavn}
                    onChange={(e) => setNyttNavn(e.target.value)}
                  />
                  <Button data-size="sm" variant="secondary" onClick={() => opprettOgVelg('kunNavn')} disabled={oppretterVirksomhet || !nyttNavn.trim()}>
                    {oppretterVirksomhet ? 'Oppretter …' : 'Opprett og velg'}
                  </Button>
                </div>
              )}

              {valgtVirksomhet && (
                <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.75rem' }}>
                  Valgt virksomhet: <strong>{valgtVirksomhet.navn}</strong>
                  {valgtVirksomhet.organisasjonsnummer ? ` (org.nr. ${valgtVirksomhet.organisasjonsnummer})` : ''}
                </Alert>
              )}

              {/* ----- Gruppemedlem-veiens ENE ekstra felt (gruppemedlemskap-runden, issue #164) ----- */}
              {slag === 'gruppemedlem' && (
                <>
                  <Divider style={{ margin: '0.75rem 0' }} />
                  <Heading level={3} data-size="xs" style={{ marginBottom: '0.35rem' }}>
                    Hvilken gruppe navngir teksten den som medlem av?
                  </Heading>
                  <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
                    Gruppen må finnes som gruppebegrep fra før — den er definert i en LOV, mens denne
                    rettskilden bare navngir medlemmene. Mangler gruppen, må den opprettes fra
                    lovteksten som definerer den først.
                  </Metatekst>
                  {/* docs/09 §15: «ingen gruppebegrep finnes» er en påstand, og skal ikke vises mens
                    * lista fortsatt lastes. */}
                  {gruppebegrep === null ? (
                    <Spinner aria-label="Laster gruppebegrepene …" data-size="sm" />
                  ) : gruppebegrep.length === 0 ? (
                    <Alert data-color="warning" data-size="sm" style={{ marginBottom: '0.75rem' }}>
                      Det finnes ingen gruppebegrep ennå. Opprett gruppen fra den lovteksten som
                      definerer den — via «Gruppe som defineres her» på den kandidaten — og kom
                      tilbake hit etterpå.
                    </Alert>
                  ) : (
                    <GruppebegrepVelger
                      gruppebegrep={gruppebegrep}
                      value={valgtGruppeBegrepId}
                      onChange={setValgtGruppeBegrepId}
                      label="Gruppe"
                      tomValgTekst="Velg gruppe …"
                      style={{ marginBottom: '0.75rem', maxWidth: '28rem' }}
                    />
                  )}
                  {valgtGruppe && (
                    <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.75rem' }}>
                      Valgt gruppe: <strong>{valgtGruppe.term}</strong>
                      {valgtGruppeLovTittel ? <> — definert i {valgtGruppeLovTittel}</> : null}
                    </Alert>
                  )}
                </>
              )}

              <Divider style={{ margin: '0.75rem 0' }} />

              <Heading level={3} data-size="xs" style={{ marginBottom: '0.35rem' }}>
                Hvorfor peker «{kandidat.foreslattTekst}» på denne virksomheten?
              </Heading>
              <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
                Dette er stedet for legitime strenger som ikke er det offisielle navnet — et utgått
                navn, en kortform, eller en skrivefeil i kildeteksten. Kan stå tom.
              </Metatekst>
              <NavneformgrunnVelger
                value={navneformgrunn}
                onChange={setNavneformgrunn}
                label="Begrunnelse"
                style={{ marginBottom: '0.75rem', maxWidth: '28rem' }}
              />

              {steg === 3 && (
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <Button data-size="sm" onClick={() => setSteg(4)} disabled={!steg3Klart}>Neste</Button>
                  <Button data-size="sm" variant="tertiary" onClick={() => setSteg(2)}>Tilbake</Button>
                </div>
              )}
            </Card>
          )}

          {/* ---------------- Steg 4: Utover navneform? (Ny, issue #283 AC5/AC6/AC7) ---------------- */}
          {steg >= 4 && harVirksomhetssteg(slag) && (
            <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>
                4. {stegTitler(slag)[3]}
              </Heading>
              <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                Valgfritt — de aller fleste treff er BARE en navneform. Velg ett av de to under KUN
                når denne KONKRETE forekomsten selv sier noe mer: at stedet HAR en rolle her, eller at
                det står i et organisatorisk forhold til en annen, navngitt virksomhet.
              </Metatekst>

              <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
                <Radio name="tillegg" label="Ingen" value="ingen"
                  checked={tillegg === 'ingen'} onChange={() => setTillegg('ingen')} disabled={steg !== 4} />
                <Radio
                  name="tillegg"
                  label="Rolle tildelt her"
                  description="Teksten tildeler et rollebegrep (en myndighet) til virksomheten her — f.eks. «forurensningsmyndighet». Oppretter en generell myndighetstildeling, uavhengig av et evt. gruppemedlemskap."
                  value="rolle"
                  checked={tillegg === 'rolle'}
                  onChange={() => { setTillegg('rolle'); if (!rolleFraEid) setRolleFraEid(kandidat.nodeEid); }}
                  disabled={steg !== 4}
                />
                <Radio
                  name="tillegg"
                  label="Relasjon til annen virksomhet"
                  description="Teksten beskriver et organisatorisk forhold til en annen, navngitt virksomhet — f.eks. klageinstans, underlagt, sekretariat."
                  value="relasjon"
                  checked={tillegg === 'relasjon'}
                  onChange={() => setTillegg('relasjon')}
                  disabled={steg !== 4}
                />
              </Field>

              {tillegg === 'rolle' && (
                <>
                  {gruppebegrep === null ? (
                    <Spinner aria-label="Laster gruppebegrepene …" data-size="sm" />
                  ) : gruppebegrep.length === 0 ? (
                    <Alert data-color="warning" data-size="sm" style={{ marginBottom: '0.75rem' }}>
                      Det finnes ingen gruppebegrep ennå — ingen rolle å velge. Opprett rollebegrepet
                      fra lovteksten som definerer det først (via «Gruppe som defineres her» på den
                      kandidaten).
                    </Alert>
                  ) : (
                    <GruppebegrepVelger
                      gruppebegrep={gruppebegrep}
                      value={valgtRolleBegrepId}
                      onChange={setValgtRolleBegrepId}
                      label="Rollebegrep"
                      tomValgTekst="Velg rolle …"
                      style={{ marginBottom: '0.75rem', maxWidth: '28rem' }}
                    />
                  )}

                  <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
                    {paragrafKandidaterForRolle.length > 0 && (
                      <Field data-size="sm" style={{ maxWidth: '14rem' }}>
                        <Label>Paragraf</Label>
                        <Select data-size="sm" value={rolleFraEid} onChange={(e) => setRolleFraEid(e.target.value)}>
                          <Select.Option value="">Velg …</Select.Option>
                          {paragrafKandidaterForRolle.map((n) => (
                            <Select.Option key={n.id} value={n.eid}>
                              {n.nodeType === 'side' ? 'Hele siden' : n.nummer}{n.overskrift ? ` — ${n.overskrift}` : ''}
                            </Select.Option>
                          ))}
                        </Select>
                      </Field>
                    )}
                    <Textfield data-size="sm" label="Fra eId (avansert / manuell)" value={rolleFraEid}
                      onChange={(e) => setRolleFraEid(e.target.value)} style={{ minWidth: '16rem', fontFamily: 'monospace' }} />
                    {paragrafKandidaterForRolle.length > 0 && (
                      <Field data-size="sm" style={{ maxWidth: '14rem' }}>
                        <Label>Til paragraf (valgfritt)</Label>
                        <Select data-size="sm" value={rolleTilEid} onChange={(e) => setRolleTilEid(e.target.value)}>
                          <Select.Option value="">Enkeltpunkt, ikke spenn</Select.Option>
                          {paragrafKandidaterForRolle.map((n) => (
                            <Select.Option key={n.id} value={n.eid}>
                              {n.nodeType === 'side' ? 'Hele siden' : n.nummer}{n.overskrift ? ` — ${n.overskrift}` : ''}
                            </Select.Option>
                          ))}
                        </Select>
                      </Field>
                    )}
                    <Textfield data-size="sm" label="Til eId (valgfritt, avansert)" value={rolleTilEid}
                      onChange={(e) => setRolleTilEid(e.target.value)} style={{ minWidth: '16rem', fontFamily: 'monospace' }} />
                  </div>
                  {/* [Note] Kun ETT paragrafspenn-par her — se PR-beskrivelsen for begrunnelsen
                    * (`LeggTilMyndighetstildelingForm.tsx` har den fulle liste-byggeren for flere). */}
                  <Textfield data-size="sm" label="Vilkår (valgfritt)" value={rolleVilkaar}
                    onChange={(e) => setRolleVilkaar(e.target.value)} style={{ maxWidth: '28rem', marginBottom: '0.75rem' }} />
                  {valgtRolle && (
                    <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.75rem' }}>
                      Rolletildeling: <strong>{valgtRolle.term}</strong> ved {visRolleNodeKort(rolleFraEid || kandidat.nodeEid)}
                    </Alert>
                  )}
                </>
              )}

              {tillegg === 'relasjon' && (
                <>
                  <Field data-size="sm" style={{ maxWidth: '24rem', marginBottom: '0.75rem' }}>
                    <Label>Relasjonstype</Label>
                    <Select data-size="sm" value={relasjonsType} onChange={(e) => setRelasjonsType(e.target.value)} disabled={!relasjonstyper}>
                      <Select.Option value="">{relasjonstyper ? 'Velg relasjonstype …' : 'Laster …'}</Select.Option>
                      {relasjonstyper?.map((t) => (
                        <Select.Option key={t.kode} value={t.kode}>
                          {t.kode} — «{t.fraVisningsmal.replace('{0}', 'motparten')}»
                        </Select.Option>
                      ))}
                    </Select>
                  </Field>
                  <VirksomhetVelger
                    virksomheter={virksomheter.filter((v) => v.id !== valgtVirksomhetId)}
                    value={motpartVirksomhetId}
                    onChange={setMotpartVirksomhetId}
                    label="Motpart (annen virksomhet)"
                    tomValgTekst="Velg virksomhet …"
                    style={{ marginBottom: '0.75rem', maxWidth: '28rem' }}
                  />
                  <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
                    <Radio name="hjemletHer" label="Hjemlet i denne rettskilden"
                      description="Relasjonen fremgår faktisk av denne setningen."
                      checked={relasjonHjemletHer} onChange={() => setRelasjonHjemletHer(true)} />
                    <Radio name="hjemletHer" label="Ikke hjemlet her — bare en kommentar"
                      description="Relasjonen er kjent, men denne teksten er ikke den formelle hjemmelen."
                      checked={!relasjonHjemletHer} onChange={() => setRelasjonHjemletHer(false)} />
                  </Field>
                  {!relasjonHjemletHer && (
                    <Textfield data-size="sm" label="Kommentar" placeholder="f.eks. lenke til org-kart"
                      value={relasjonKommentar} onChange={(e) => setRelasjonKommentar(e.target.value)}
                      style={{ maxWidth: '28rem', marginBottom: '0.75rem' }} />
                  )}
                </>
              )}

              {steg === 4 && (
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <Button data-size="sm" onClick={() => setSteg(5)} disabled={!tilleggKlart}>Neste</Button>
                  <Button data-size="sm" variant="tertiary" onClick={() => setSteg(3)}>Tilbake</Button>
                </div>
              )}
            </Card>
          )}

          {/* ---------------- Steg 5: Bekreft ---------------- */}
          {steg >= 5 && (
            <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>5. Bekreft</Heading>
              <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                Dette blir opprettet eller endret når du fullfører:
              </Metatekst>
              <Table data-size="sm" style={{ marginBottom: '1rem', width: '100%' }}>
                <Table.Body>
                  <Table.Row>
                    <Table.HeaderCell scope="row">Navneform</Table.HeaderCell>
                    <Table.Cell>«{kandidat.foreslattTekst}»</Table.Cell>
                  </Table.Row>
                  <Table.Row>
                    <Table.HeaderCell scope="row">Peker på virksomhet</Table.HeaderCell>
                    <Table.Cell>{valgtVirksomhet?.navn ?? '—'}</Table.Cell>
                  </Table.Row>
                  {slag === 'gruppemedlem' && (
                    <Table.Row>
                      <Table.HeaderCell scope="row">Medlem av gruppe</Table.HeaderCell>
                      <Table.Cell>
                        {valgtGruppe ? `«${valgtGruppe.term}»` : '—'}
                        {/* Hjemmelen er ikke et valg, og skal derfor STÅ her, ikke velges: den er
                          * alltid kandidatens egen rettskilde. Se KoblTilGruppemedlemskapAsync. */}
                        <Metatekst as="span" style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>
                          Hjemlet i {rettskilde?.tittel ?? 'denne rettskilden'} — det er her navnet står.
                        </Metatekst>
                      </Table.Cell>
                    </Table.Row>
                  )}
                  {/* [Ny, «alle mekanismer»-runden, 2026-09-21, issue #283 AC5/AC6/AC7] Steg 4-tillegget. */}
                  {tillegg === 'rolle' && (
                    <Table.Row>
                      <Table.HeaderCell scope="row">Rolle tildelt her</Table.HeaderCell>
                      <Table.Cell>
                        {valgtRolle ? `«${valgtRolle.term}»` : '—'}
                        <Metatekst as="span" style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>
                          Hjemlet i {rettskilde?.tittel ?? 'denne rettskilden'}, ved {visRolleNodeKort(rolleFraEid || kandidat.nodeEid)}.
                        </Metatekst>
                      </Table.Cell>
                    </Table.Row>
                  )}
                  {tillegg === 'relasjon' && (
                    <Table.Row>
                      <Table.HeaderCell scope="row">Relasjon til annen virksomhet</Table.HeaderCell>
                      <Table.Cell>
                        {valgtMotpart && relasjonsType ? `${relasjonsType} — ${valgtMotpart.visningsnavn}` : '—'}
                        <Metatekst as="span" style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>
                          {relasjonHjemletHer
                            ? `Hjemlet i ${rettskilde?.tittel ?? 'denne rettskilden'}.`
                            : 'Ingen formell hjemmel — kun kommentaren registreres.'}
                        </Metatekst>
                      </Table.Cell>
                    </Table.Row>
                  )}
                  <Table.Row>
                    <Table.HeaderCell scope="row">Begrunnelse</Table.HeaderCell>
                    <Table.Cell>{navneformgrunn ?? 'Uspesifisert'}</Table.Cell>
                  </Table.Row>
                  <Table.Row>
                    <Table.HeaderCell scope="row">Navnekandidat</Table.HeaderCell>
                    <Table.Cell>Settes til «Godkjent»</Table.Cell>
                  </Table.Row>
                  <Table.Row>
                    <Table.HeaderCell scope="row">Tagg i rettskilden</Table.HeaderCell>
                    {/* [ENDRET, navneform-kjede-runden, 2026-09-08] Sa «Kobles til virksomheten».
                      * Det er ikke lenger sant — taggen kobles til NAVNEFORMEN (se
                      * TekstTaggEntitet.RefId), og docs/09 §15 krever at det veiviseren LOVER her
                      * stemmer med det den faktisk gjør. Oppsummeringen ETTER fullføring var alt
                      * rettet; denne forhåndsvisningen sto igjen med den gamle påstanden. */}
                    <Table.Cell>
                      Kobles til navneformen over, i laget «Virksomhet» — virksomheten nås gjennom
                      den. En eventuell ubundet tagg på samme sted gjenbrukes i stedet for at en ny
                      opprettes.
                    </Table.Cell>
                  </Table.Row>
                </Table.Body>
              </Table>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <Button data-size="sm" onClick={fullførVirksomhet} disabled={fullfører || !steg3Klart || !tilleggKlart}>
                  {fullfører ? 'Fullfører …' : 'Fullfør'}
                </Button>
                <Button data-size="sm" variant="tertiary" onClick={() => setSteg(harVirksomhetssteg(slag) ? 4 : 3)}>Tilbake</Button>
              </div>
            </Card>
          )}
        </>
      )}

      {feil && <Alert data-color="danger" style={{ marginTop: '0.75rem' }}>{feil}</Alert>}
    </>
  );
}
