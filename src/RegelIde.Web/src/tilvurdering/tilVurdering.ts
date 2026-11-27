import type { TilVurderingDto } from '../api/types';

/**
 * [Ny, issue #517 «Til vurdering», Johanns avgjørelser 2026-11-27] Rekkefølgen og tekstene på startsiden, som en ren modul (docs/09 §17):
 * tallene kommer fra `GET /api/til-vurdering`, alt annet står her og er låst med vitest (`tilVurdering.test.ts`).
 *
 * [LÅST, #517] Rekkefølgen er fast og begrunnet, ikke regnet ut: hva vurderingen låser opp delt på tiden den tar, slik kartleggingen
 * begrunnet den. «Start her» er departementenes navneformer (de sju som holder igjen flest funn). Når den køen er tom, er «Start her» neste
 * kø i rekkefølgen som har noe som venter.
 *
 * Tidene er anslag, ikke målt (AK 5). De står som minutter per element; totalen regnes av antallet.
 */

/** [LÅST, #517] Antall aktører i «Start her». */
export const START_HER_ANTALL = 7;

/** Anslag per aktør: åpne siden, lese navnet og hjemmelen, ett eller to klikk. */
const DEPARTEMENT_MINUTTER: [number, number] = [2, 3];

/** [Ny, #517 PR 3] Aktørsiden åpnet på fanen Navneformer, der navneformene som er forslag har «Godkjenn». */
export const aktorLenke = (virksomhetId: string) => `/virksomheter/${virksomhetId}?fane=navneformer`;

export const GITHUB_SAK = (nr: number) => `https://github.com/FinnurO/regel-ide/issues/${nr}`;

/** Det som holder en kø igjen. `warning` er et synlig hull (docs/09 §15/§25); `neutral` er en opplysning om rekkefølge. */
export interface Blokkering {
  tekst: string;
  farge: 'warning' | 'neutral';
  forklaring?: string;
}

export interface KoRad {
  id: string;
  tittel: string;
  /** Lenken inn i køen, eller til saken når køen mangler en skjerm. `null`: lista står på siden selv. */
  href: string | null;
  ekstern: boolean;
  hvor: string;
  venter: number;
  venterTillegg?: string;
  /** Minutter per element, [fra, til]. Utelatt når tiden ikke er poenget (blokkerte køer). */
  minutter?: [number, number];
  krever: string[];
  laaserOpp: string;
  blokkerer: Blokkering | null;
}

export interface EnkelRad {
  tittel: string;
  href: string;
  ekstern: boolean;
  venter: number | null;
  merknad: string;
}

export interface TilVurderingVisning {
  /** Køene som er klare, i fast rekkefølge, nummerert fra 1. */
  klare: KoRad[];
  /** Køene som har elementer, men venter på en skjerm, en måling eller en arbeidsflyt. */
  blokkerte: KoRad[];
  eldre: EnkelRad[];
  utenforAppen: EnkelRad[];
  /** Id-en til køen «Start her» peker på (første klare kø med noe som venter), eller null når alt er tomt. */
  startHer: string | null;
  /** Telleren på menylenken: elementene i de klare køene. */
  klarTeller: number;
}

const tall = (n: number) => n.toLocaleString('nb-NO');
const desimal = (n: number) => (Number.isInteger(n) ? String(n) : n.toLocaleString('nb-NO', { maximumFractionDigits: 1 }));

/** «ca. 1 min», «1–2 min», «0,5–1 min». */
export function tidPerElement([fra, til]: [number, number]): string {
  return fra === til ? `ca. ${desimal(fra)} min` : `${desimal(fra)}–${desimal(til)} min`;
}

/** Tiden i alt for `antall` elementer: «ca. 20 min», «ca. 1 t», «1–2 t». Timer avrundes til nærmeste halve. */
export function tidTotalt(antall: number, [fra, til]: [number, number]): string {
  if (antall <= 0) return '—';
  const lo = antall * fra;
  const hi = antall * til;
  if (hi < 60) {
    const midt = Math.max(5, Math.round((lo + hi) / 2 / 5) * 5);
    return `ca. ${midt} min`;
  }
  const halv = (m: number) => Math.max(0.5, Math.round(m / 30) / 2);
  const a = halv(lo);
  const b = halv(hi);
  return a === b ? `ca. ${desimal(a)} t` : `${desimal(a)}–${desimal(b)} t`;
}

export function byggTilVurdering(tv: TilVurderingDto): TilVurderingVisning {
  const dep = tv.departementer;
  const ak = tv.aktorko;
  const venterModellvalg = ak.perHandling['venter'] ?? 0;
  const tilGodkjenning = ak.perHandling['godkjenn'] ?? 0;
  // [ENDRET, #517 PR 3] Radene følger filteret på handling i aktørkøen (`?handling=godkjenn|andre`), så tallet er det samme som køen viser
  // med lenkens filter (AK 1 og 6). De som trenger «Hvor gjelder den?», står derfor under godkjenning, ikke under resten.
  const andre = ['generisk_omtale', 'ikke_aktor', 'koble', 'ta_med'].reduce((sum, h) => sum + (ak.perHandling[h] ?? 0), 0);
  const funnAlleAktorer = dep.funnKumulativt.length > 0 ? dep.funnKumulativt[dep.funnKumulativt.length - 1] : 0;

  const klare: KoRad[] = [
    {
      id: 'departementer',
      tittel: 'Departementenes navneformer',
      href: null,
      ekstern: false,
      hvor: 'Aktørsiden, fanen Navneformer',
      venter: dep.aktorer.length,
      venterTillegg: 'aktører',
      minutter: DEPARTEMENT_MINUTTER,
      krever: [
        'Lenken åpner aktøren på fanen Navneformer.',
        '«Godkjenn» på navneformen som er forslag. Ett klikk per navneform.',
        'Navnet og hjemmelen står på siden, så du bekrefter et faktum om navnehistorikken, ikke en tolkning.',
      ],
      laaserOpp: `«Navneform ikke faglig vurdert» forsvinner fra ${tall(funnAlleAktorer)} funn om utgåtte aktører. `
        + `De ${Math.min(START_HER_ANTALL, dep.aktorer.length)} første er «Start her».`,
      blokkerer: null,
    },
    {
      id: 'aktorko-klare',
      tittel: 'Aktørkø: godkjenn forhåndsklassifiserte navn',
      href: '/aktorko?handling=godkjenn',
      ekstern: false,
      hvor: 'Aktørkøen, filtrert på forslag til godkjenning',
      venter: tilGodkjenning,
      venterTillegg: `${tall(ak.klare)} klare`,
      minutter: [1, 1],
      krever: [
        '«Godta forslaget» fyller ut skjemaet. Ingenting lagres ennå.',
        'Velg «Hvor sikkert er det?» (Faglig vurdert eller Godkjent).',
        '«Godkjenn «navn»». Tre klikk.',
        `${tall(ak.trengerSted)} er en klasse, rolle eller et område uten hjemmel: velg i tillegg «Hvor gjelder den?».`,
      ],
      laaserOpp: `${tall(tilGodkjenning)} nye aktører, klasser, roller og områder. Av relasjonene i forhåndsklassifiseringen står `
        + `${tall(ak.relasjonerVenterPaNavn)} som «Opprett navnet først»; de kan skrives når navnet er godkjent.`,
      blokkerer: null,
    },
    {
      id: 'etterfolgere',
      tittel: 'Etterfølgerforslag',
      href: '/ki-oppdagelse?type=etterfolger',
      ekstern: false,
      hvor: 'Forslagskøen, filtrert på typen etterfølger',
      venter: tv.etterfolgere.forslag,
      venterTillegg: `${tall(tv.etterfolgere.manuelle)} manuelle`,
      minutter: [1, 2],
      krever: [
        'Les utsagnet («X etterfølger Y») og hjemmelen eller kilden utenfor korpus.',
        '«Godkjenn», eller «Avvis» med begrunnelse. Med filteret står ikke «Godkjenn alle» per hjemmel; kryss av og bruk «Godkjenn valgte».',
        `Ta de ${tall(tv.etterfolgere.manuelle)} manuelt foreslåtte først: de holder igjen ${tall(tv.etterfolgere.funnManuelle)} funn.`,
      ],
      laaserOpp: `«Etterfølger ikke vurdert» forsvinner fra ${tall(tv.etterfolgere.funnMedEtterfolgermangel)} funn om utgåtte aktører.`,
      blokkerer: null,
    },
    {
      id: 'aktorko-resten',
      tittel: 'Aktørkø: resten',
      href: '/aktorko?handling=andre',
      ekstern: false,
      hvor: 'Aktørkøen, filtrert på omtale, ikke en aktør, koble og ta med',
      venter: andre,
      minutter: [0.5, 1],
      krever: [
        `Generisk omtale (${tall(ak.perHandling['generisk_omtale'] ?? 0)}): «Godta forslaget» → «Tørrkjør» → «Lagre: tagg N som generisk omtale».`,
        `Ikke en aktør (${tall(ak.perHandling['ikke_aktor'] ?? 0)}): «Godta forslaget» fyller ut begrunnelsen → lagre.`,
        `Koble (${tall(ak.perHandling['koble'] ?? 0)}) og ta med (${tall(ak.perHandling['ta_med'] ?? 0)}): «Godta forslaget» → skjemaets knapp.`,
      ],
      laaserOpp: `Køen blir tom bortsett fra navnene som venter på modellvalg. ${tall(ak.pilotNavn)} av de ${tall(ak.antallNavn)} navnene står i `
        + 'barnehageregelverket, så piloten kan lastes uten at utsagn faller bort for en ukjent aktør.',
      blokkerer: venterModellvalg > 0 ? { tekst: `${tall(venterModellvalg)} venter på modellvalg (#463)`, farge: 'warning' } : null,
    },
    {
      id: 'pilot',
      tittel: 'Piloten: barnehageloven',
      href: tv.pilot.rettskilder.length > 0 ? `/ki-oppdagelse?hjemmel=${tv.pilot.rettskilder.join(',')}` : '/ki-oppdagelse',
      ekstern: false,
      hvor: 'Forslagskøen, filtrert på hjemler i barnehageregelverket (#375)',
      venter: tv.pilot.forslag,
      minutter: [1, 2],
      krever: [
        'Åpne gruppen for hjemmelen og les forhåndsvisningen.',
        '«Godkjenn» eller «Avvis» per forslag, eller «Godkjenn alle N» med to klikk.',
        `${tall(tv.pilot.ki)} er KI-forslag og ${tall(tv.pilot.monster)} mønsterforslag.`,
      ],
      laaserOpp: 'Pilotens sammenligning med BU-kartleggingen kan gjøres.',
      blokkerer: { tekst: 'Etter punkt 2 og 4', farge: 'neutral', forklaring: 'Aktørkøen først, så forslagene lastes med alle aktørene.' },
    },
    {
      id: 'kunngjort',
      tittel: 'Hjemmel opphevet: gjeldende status',
      href: '/funn?kategori=hjemmel_opphevet&status=oppdaget&mangel=kunngjort',
      ekstern: false,
      hvor: 'Funn til vurdering, filtrert på «Hjemmel opphevet», oppdaget og «Kunngjort versjon»',
      venter: tv.hjemmelOpphevet.kunngjortVersjon,
      minutter: [2, 3],
      krever: [
        'Slå opp vedtaket på Lovdata.',
        'Fyll ut «Gjeldende status: hva viser Lovdata for vedtaket?» i juristvurderingen og lagre.',
      ],
      laaserOpp: `Mangelen «Kunngjort versjon, status ikke kontrollert» forsvinner fra ${tall(tv.hjemmelOpphevet.kunngjortVersjon)} funn.`,
      blokkerer: null,
    },
  ];

  const forekomsterForslag = tv.forekomster.reduce((s, f) => s + f.forslag, 0);
  const slutninger = tv.forekomster.filter((f) => f.slutninger > 0).map((f) => `«${f.term}» ${tall(f.slutninger)}`).join(', ');
  const kildefeilOpphevet = tv.kildefeil.perType['opphevet_av_annen_kilde'] ?? 0;
  const kildefeilNode = tv.kildefeil.perType['hjemmel_node_mangler'] ?? 0;

  const blokkerte: KoRad[] = [
    {
      id: 'hjemmel-opphevet', tittel: 'Hjemmel opphevet', href: '/funn?kategori=hjemmel_opphevet&status=oppdaget', ekstern: false, hvor: 'Funn til vurdering (#472)',
      venter: tv.hjemmelOpphevet.oppdaget, krever: [],
      laaserOpp: `Juristvurdering per funn. ${tall(tv.hjemmelOpphevet.holdesAvNyHjemmel)} holdes av «Ny hjemmel»: først da kan «mulig bortfalt» `
        + 'stå og funnet publiseres.',
      blokkerer: { tekst: '10–15 min per funn', farge: 'warning', forklaring: 'Forslag: juristagenten fyller ut, og du ser bare uenighet.' },
    },
    {
      id: 'utgatt-aktor', tittel: 'Utgått aktør', href: GITHUB_SAK(429), ekstern: true, hvor: 'Funn til vurdering (#429)',
      venter: tv.utgattAktor.oppdaget, krever: [],
      laaserOpp: 'Juristvurdering og publisering.',
      blokkerer: { tekst: 'Ny presisjonsmåling først', farge: 'warning', forklaring: 'Navneformene og etterfølgerne i punkt 1 og 3 kan tas nå.' },
    },
    {
      id: 'forekomster', tittel: 'Forekomster av «regjeringen» og «statsråd»', href: GITHUB_SAK(486), ekstern: true, hvor: 'Taggene i lovteksten (#486)',
      venter: forekomsterForslag, krever: [],
      laaserOpp: `Slutninger et menneske må avgjøre: ${slutninger || 'ingen'}. Resten er regeltagger.`,
      blokkerer: { tekst: 'Ingen skjerm for å godkjenne', farge: 'warning' },
    },
    {
      id: 'klasseforslag', tittel: 'Klasser, roller og generiske navneformer', href: GITHUB_SAK(482), ekstern: true, hvor: '(#482)',
      venter: tv.klasseforslag, krever: [],
      laaserOpp: '«Forslag» forsvinner fra medlemskap og tilhørighet, for eksempel for fylkeskommune, departement og Norge.',
      blokkerer: { tekst: 'Ingen skjerm (#482)', farge: 'warning' },
    },
    {
      id: 'navneformforslag', tittel: 'Navneformforslag på tvers av aktører', href: GITHUB_SAK(400), ekstern: true, hvor: '(#400)',
      venter: tv.navneformforslag, krever: [],
      laaserOpp: 'Kortformer, parallellnavn og gjeldende navn med hjemmel. Godkjennes i dag én aktør om gangen; navneformene i punkt 1 er med i tallet.',
      blokkerer: { tekst: 'Ingen samlet liste (#400)', farge: 'warning' },
    },
    {
      id: 'ressortkanter', tittel: 'Ressortkanter fra livsløpet', href: GITHUB_SAK(403), ekstern: true, hvor: 'Forslagskøen, F forvaltning (#403)',
      venter: tv.ressortkanter, krever: [],
      laaserOpp: '«Departementet» besvares fra vedtaket, ikke fra Lovdatas felt.',
      blokkerer: { tekst: 'Venter på arbeidsflyt (#403)', farge: 'warning' },
    },
    {
      id: 'kildefeil', tittel: 'Kildefeil', href: GITHUB_SAK(504), ekstern: true, hvor: 'Kildefeil (#504)',
      venter: tv.kildefeil.ny, krever: [],
      laaserOpp: `${tall(kildefeilOpphevet)} «opphevet av annen kilde»: en avvisning lar maskinregelen lage funn i kilden igjen. `
        + `${tall(kildefeilNode)} mangler hjemmelnode (teknisk).`,
      blokkerer: { tekst: 'Ingen knapp for å avvise (#504)', farge: 'warning' },
    },
  ];

  const eldre: EnkelRad[] = [
    { tittel: 'Begrepskandidater', href: '/begrepskandidater', ekstern: false, venter: tv.eldre.begrepskandidater, merknad: 'Fra før strukturmodellen.' },
    { tittel: 'Relaterte definisjoner', href: '/begrep-definisjon-relasjoner', ekstern: false, venter: tv.eldre.relaterteDefinisjoner,
      merknad: 'Fra før strukturmodellen.' },
    { tittel: 'Virksomhetskandidater', href: '/virksomhet-kandidater', ekstern: false, venter: tv.eldre.virksomhetskandidater,
      merknad: 'Tatt ut av menyen med #440. Ruten står.' },
  ];

  // Tallene her finnes ikke i appen; de står i saken og på GitHub. De vises derfor uten tall (ingen gjettet verdi, CLAUDE.md §8).
  const utenforAppen: EnkelRad[] = [
    { tittel: 'Fasitgjennomgangen (#309)', href: GITHUB_SAK(309), ekstern: true, venter: null, merknad: 'Arket fra oktober. Antallet står i saken.' },
    { tittel: 'Åpne pull requests', href: 'https://github.com/FinnurO/regel-ide/pulls', ekstern: true, venter: null,
      merknad: 'Blant annet fra Dependabot.' },
  ];

  const startHer = klare.find((k) => k.venter > 0)?.id ?? null;
  return {
    klare, blokkerte, eldre, utenforAppen, startHer,
    klarTeller: klare.reduce((s, k) => s + k.venter, 0),
  };
}

/** «Start her» når det er departementene: de første aktørene og hvor mange ulike funn de holder igjen. */
export function startHerDepartementer(tv: TilVurderingDto) {
  const aktorer = tv.departementer.aktorer.slice(0, START_HER_ANTALL);
  const funn = aktorer.length === 0 ? 0 : tv.departementer.funnKumulativt[aktorer.length - 1] ?? 0;
  return { aktorer, funn, iAlt: tv.utgattAktor.oppdaget, minutter: tidTotalt(aktorer.length, DEPARTEMENT_MINUTTER) };
}
