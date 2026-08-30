import { NavLink } from 'react-router';
import { Dropdown, Label } from '@digdir/designsystemet-react';
import { useBruker } from '../bruker/BrukerContext';

/**
 * Gruppert sidemeny (2026-08-20, "Startside Alternativ 1c"; regruppert til seks seksjoner
 * 2026-08-30 — menyen vokste til 17 lenker under de opprinnelige tre gruppene, som ikke lenger
 * ga et tydelig nok informasjonsarkitektur-hierarki). De seks gruppene (Kilder / Registre /
 * Tjenestemodellering / Forslag og kandidater / Virksomhetskatalog / Administrasjon) speiler den
 * faktiske hierarkiske strukturen i domenet, i stedet for en alfabetisk/kronologisk flat liste.
 *
 * "Kommende" rader (i dag: kun Importer katalog) har ingen side bygget ennå — vist som en
 * ikke-klikkbar rad med et "Kommer"-merke, samme "ingen gjettet fallback"-holdning som resten av
 * appen: vi later ikke som funksjonaliteten finnes før den faktisk gjør det. Handlinger fikk sin
 * egen toppnivå-side 2026-08-22 (HandlingerListe.tsx) og er derfor en ekte lenke igjen.
 */
interface NavRad {
  kind: 'lenke';
  to: string;
  label: string;
}
interface KommendeRad {
  kind: 'kommende';
  label: string;
}
type GruppeRad = NavRad | KommendeRad;

interface Gruppe {
  heading: string;
  rader: GruppeRad[];
}

const GRUPPER: Gruppe[] = [
  {
    heading: 'Kilder',
    rader: [
      { kind: 'lenke', to: '/rettskilder', label: 'Rettskilder' },
      { kind: 'lenke', to: '/importer', label: 'Importer rettskilder' },
      { kind: 'kommende', label: 'Importer katalog' },
    ],
  },
  {
    heading: 'Registre',
    rader: [
      { kind: 'lenke', to: '/begreper', label: 'Begreper' },
      { kind: 'lenke', to: '/kodelister', label: 'Kodelister' },
      { kind: 'lenke', to: '/datasett', label: 'Datasett' },
    ],
  },
  {
    heading: 'Tjenestemodellering',
    rader: [
      { kind: 'lenke', to: '/tjenester', label: 'Tjenester' },
      { kind: 'lenke', to: '/handlinger', label: 'Handlinger' },
      { kind: 'lenke', to: '/vilkarstre', label: 'Vilkårstre' },
      { kind: 'lenke', to: '/tjenestereise', label: 'Tjenestereise (graf)' },
      { kind: 'lenke', to: '/handboker/ny', label: 'Håndbøker' },
      { kind: 'lenke', to: '/importer/rettighetsmodell', label: 'Importer rettighetsmodell' },
    ],
  },
  {
    heading: 'Forslag og kandidater',
    rader: [
      { kind: 'lenke', to: '/tjenester/forslag', label: 'KI-forslag tjenester' },
      { kind: 'lenke', to: '/begreper/forslag', label: 'KI-forslag begrep' },
      { kind: 'lenke', to: '/virksomhet-kandidater', label: 'Virksomhetskandidater' },
      { kind: 'lenke', to: '/navnekandidater', label: 'Navnekandidater' },
    ],
  },
  {
    heading: 'Virksomhetskatalog',
    rader: [{ kind: 'lenke', to: '/virksomheter', label: 'Virksomheter' }],
  },
  {
    heading: 'Administrasjon',
    rader: [{ kind: 'lenke', to: '/brukere', label: 'Brukere' }],
  },
];

/** Forbokstav fra første og siste ord — «Tone Karlsen» → «TK». Ingen fallback-gjetting: ett ord gir kun én bokstav. */
function initialer(navn: string): string {
  const deler = navn.trim().split(/\s+/).filter(Boolean);
  if (deler.length === 0) return '';
  const forste = deler[0][0];
  const siste = deler.length > 1 ? deler[deler.length - 1][0] : '';
  return (forste + siste).toUpperCase();
}

function BrukerChip({ navn, meta }: { navn: string; meta: string }) {
  return (
    <span className="sidebar__bruker-chip">
      <span className="sidebar__bruker-avatar" aria-hidden="true">
        {initialer(navn)}
      </span>
      <span className="identitet-brikke__tekst">
        <span className="identitet-brikke__navn">{navn}</span>
        <span className="identitet-brikke__meta">{meta}</span>
      </span>
    </span>
  );
}

export function Sidebar() {
  const { brukere, gjeldendeBruker, velgBruker, laster, ekteInnlogging, innloggingsfeil } = useBruker();

  return (
    <aside className="sidebar">
      <div className="sidebar__brand">
        <span className="sidebar__brand-merke" aria-hidden="true">R</span>
        <span className="sidebar__brand-tekst">
          <span className="sidebar__brand-navn">Forvaltningsverktøy</span>
          <span className="sidebar__brand-undertekst">for digitale tjenester</span>
        </span>
      </div>

      <nav className="sidebar__nav">
        {GRUPPER.map((gruppe) => (
          <div key={gruppe.heading}>
            <div className="sidebar__gruppe-heading">{gruppe.heading}</div>
            {gruppe.rader.map((rad) =>
              rad.kind === 'kommende' ? (
                <span key={rad.label} className="sidebar__item sidebar__item--kommende">
                  {rad.label}
                  <span className="sidebar__badge sidebar__badge--kommer">Kommer</span>
                </span>
              ) : (
                <NavLink key={rad.to} to={rad.to} className={({ isActive }) => `sidebar__item${isActive ? ' aktiv' : ''}`}>
                  {rad.label}
                </NavLink>
              ),
            )}
          </div>
        ))}
      </nav>

      <div className="sidebar__bruker">
        {laster ? null : innloggingsfeil ? (
          <div className="identitet-brikke identitet-brikke--feil" role="status">
            <Label data-size="sm">{innloggingsfeil}</Label>
          </div>
        ) : gjeldendeBruker ? (
          ekteInnlogging ? (
            <BrukerChip navn={gjeldendeBruker.navn} meta={`${gjeldendeBruker.virksomhetNavn} · ${gjeldendeBruker.rolle}`} />
          ) : (
            <Dropdown.TriggerContext>
              <Dropdown.Trigger variant="tertiary" className="sidebar__bruker-knapp">
                <BrukerChip navn={gjeldendeBruker.navn} meta={`${gjeldendeBruker.virksomhetNavn} · ${gjeldendeBruker.rolle}`} />
              </Dropdown.Trigger>
              <Dropdown placement="top-start">
                <Dropdown.Heading>Bytt testbruker</Dropdown.Heading>
                <Dropdown.List>
                  {brukere.map((b) => (
                    <Dropdown.Item key={b.id}>
                      <Dropdown.Button
                        aria-current={b.id === gjeldendeBruker.id}
                        data-valgt={b.id === gjeldendeBruker.id || undefined}
                        onClick={() => velgBruker(b.id)}
                      >
                        {b.navn} ({b.rolle}) — {b.virksomhetNavn}
                      </Dropdown.Button>
                    </Dropdown.Item>
                  ))}
                </Dropdown.List>
              </Dropdown>
            </Dropdown.TriggerContext>
          )
        ) : null}
      </div>
    </aside>
  );
}
