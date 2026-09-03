import { useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Details, Heading, Link, List, Paragraph } from '@digdir/designsystemet-react';
import type { RettskildeHjemmelRelasjonDto, RettskildeSammendrag } from '../api/types';

interface RettskilderHierarkiProps {
  /** Korpuset hierarkiet bygges fra — kalleren har allerede anvendt «aktiv»/departement-filteret
   * (se RettskilderListe.tsx sin `hierarkiGrunnlag`), denne komponenten filtrerer IKKE selv på det. */
  rettskilder: RettskildeSammendrag[];
  /** Bulk-hjemmelrelasjoner for HELE korpuset (GET /api/rettskilder/hjemmelrelasjoner) — ufiltrert er
   * greit, komponenten slår kun opp relasjoner der BEGGE sider finnes i `rettskilder`. */
  hjemmelrelasjoner: RettskildeHjemmelRelasjonDto[];
  /** Fritekstfilter (samme felt som de flate tabellene) — matcher lovens ELLER en av dens forskrifters
   * tittel. En lov beholdes (med KUN de matchende forskriftene) når enten loven selv eller minst én
   * forskrift under den matcher, slik at man kan søke rett på en forskrift og fortsatt se hvilken lov
   * den hører til. */
  filterTekst: string;
}

interface LovMedForskrifter {
  lov: RettskildeSammendrag;
  forskrifter: RettskildeSammendrag[];
}

interface DepartementGruppe {
  departement: string;
  lover: LovMedForskrifter[];
}

const UKJENT_DEPARTEMENT = 'Ukjent departement';

/**
 * Departement → lov → forskrift-hierarkiet (issue #193) — tredje visningsmodus i RettskilderListe.tsx,
 * ved siden av (ikke i stedet for) den flate «Aktive rettskilder»-lista. Gjenbruker
 * RettskildeHjemmelEntitet-relasjonen som allerede vises PER RETTSKILDE på RettskildeDetalj.tsx
 * («Hjemmel for»-seksjonen) — INGEN ny relasjonsmodell, kun en klientside gruppering av samme data
 * hentet i bulk (`/api/rettskilder/hjemmelrelasjoner`, se RettskildeRepository.AlleHjemmelrelasjonerAsync).
 *
 * Ytelse (docs/09 §9/§10s "ikke mount alt samtidig"-lærdom fra Combobox/Suggestion): selve lov-nivået
 * (opptil noen hundre `<Details>`, gruppert under ~15-20 departement-overskrifter) mountes direkte —
 * langt under de dokumenterte render-timeout-terskelverdiene (451/5893 FLATE options i ÉN liste). De
 * langt tallrikere FORSKRIFTENE under hver lov mountes derimot IKKE før brukeren faktisk åpner den
 * aktuelle lovens `<Details>` (kontrollert `open`-state per lov, se `LovRad` under).
 *
 * [ENDRET, fler-verdi-departement, 2026-09-04] En lov med FLERE ansvarlige departementer (delt ansvar)
 * vises nå under HVERT departement den tilhører, ikke bare det første — se grupperingen under.
 *
 * [Rettet, 2026-09-04] Departement-NIVÅET manglet et kollaps-nivå — med ~20 departementer og opptil
 * ~70 lover under ett enkelt departement rendret siden en uendelig, uoversiktlig liste av lov-rader
 * for ALLE departementer samtidig (kun selve loven kunne kollapses, ikke gruppen den sto i). Johann:
 * "det holder ikke å fjerne dropdown. departementet må være mulig å kollapse, nå blir det en uendelig
 * lang liste som er umulig å forholde seg til." Departement-gruppen er nå SELV en `<Details>` (samme
 * kollapset-som-standard-mønster som `LovRad`), se `DepartementGruppeRad` under — siden med ~20
 * departementer viser nå kun 20 rader ved åpning, ikke flere hundre.
 */
export function RettskilderHierarki({ rettskilder, hjemmelrelasjoner, filterTekst }: RettskilderHierarkiProps) {
  const grupper = useMemo<DepartementGruppe[]>(() => {
    const rettskilderById = new Map(rettskilder.map((r) => [r.id, r]));

    // Forskrifter gruppert på lovId — kun relasjoner der BEGGE sider finnes i det (allerede filtrerte)
    // `rettskilder`-korpuset regnes med, slik at en forskrift som selv er filtrert bort (f.eks.
    // irrelevant-markert) ikke likevel dukker opp som en gren.
    const forskrifterPerLov = new Map<string, RettskildeSammendrag[]>();
    for (const rel of hjemmelrelasjoner) {
      if (!rettskilderById.has(rel.lovId)) continue;
      const forskrift = rettskilderById.get(rel.forskriftId);
      if (!forskrift) continue;
      const liste = forskrifterPerLov.get(rel.lovId) ?? [];
      if (!liste.some((f) => f.id === forskrift.id)) liste.push(forskrift);
      forskrifterPerLov.set(rel.lovId, liste);
    }

    const perDepartement = new Map<string, LovMedForskrifter[]>();
    for (const r of rettskilder) {
      if (r.kildetype !== 'Lov') continue;
      // [ENDRET, fler-verdi-departement, 2026-09-04] En lov med FLERE ansvarlige departementer (delt
      // ansvar) skal havne under HVERT av dem, ikke bare det første — samme lov/forskrifter-objekt
      // (samme referanse) i flere grener av treet, ikke en kopi.
      const departementer =
        r.ansvarligDepartement && r.ansvarligDepartement.length > 0 ? r.ansvarligDepartement : [UKJENT_DEPARTEMENT];
      const forskrifter = (forskrifterPerLov.get(r.id) ?? []).sort((a, b) => a.tittel.localeCompare(b.tittel, 'nb'));
      for (const departement of departementer) {
        const liste = perDepartement.get(departement) ?? [];
        liste.push({ lov: r, forskrifter });
        perDepartement.set(departement, liste);
      }
    }

    let resultat: DepartementGruppe[] = [...perDepartement.entries()].map(([departement, lover]) => ({
      departement,
      lover: lover.sort((a, b) => a.lov.tittel.localeCompare(b.lov.tittel, 'nb')),
    }));

    const tekst = filterTekst.trim().toLowerCase();
    if (tekst) {
      resultat = resultat
        .map((gruppe) => ({
          ...gruppe,
          lover: gruppe.lover
            .filter(
              ({ lov, forskrifter }) =>
                lov.tittel.toLowerCase().includes(tekst) || forskrifter.some((f) => f.tittel.toLowerCase().includes(tekst)),
            )
            .map(({ lov, forskrifter }) => ({
              lov,
              // Matcher loven selv (direkte treff) → behold alle forskriftene under den. Matcher kun
              // via forskrift(er) → vis kun de(n) matchende forskriften(e), ikke resten av lovens grener.
              forskrifter: lov.tittel.toLowerCase().includes(tekst)
                ? forskrifter
                : forskrifter.filter((f) => f.tittel.toLowerCase().includes(tekst)),
            })),
        }))
        .filter((gruppe) => gruppe.lover.length > 0);
    }

    return resultat.sort((a, b) => a.departement.localeCompare(b.departement, 'nb'));
  }, [rettskilder, hjemmelrelasjoner, filterTekst]);

  if (grupper.length === 0) {
    return <Paragraph>Ingen lover funnet{filterTekst.trim() ? ' for gjeldende filter' : ''}.</Paragraph>;
  }

  const filterAktiv = filterTekst.trim().length > 0;

  return (
    <div>
      {grupper.map((gruppe) => (
        <DepartementGruppeRad key={gruppe.departement} gruppe={gruppe} filterAktiv={filterAktiv} />
      ))}
    </div>
  );
}

/** Ett departement, kollapset som standard — samme «ikke mount alt samtidig»-begrunnelse som `LovRad`,
 * bare ett nivå opp: lov-RADENE under et departement rendres kun når brukeren faktisk åpner DET
 * departementet (i tillegg til at hver enkelt lovs forskrifter fortsatt krever et eget klikk). Uten
 * dette viste siden alle lover for alle ~20 departementer samtidig — «en uendelig lang liste som er
 * umulig å forholde seg til» (Johann).
 * `filterAktiv` (fritekstfilteret over har et treff) tvinger gruppen åpen uansett manuell
 * kollaps-state — grupper som allerede har blitt filtrert ned til KUN matchende lover (se `grupper`
 * sin useMemo) skal vises umiddelbart, ikke gjemmes bak enda et klikk oppå selve filtreringen. */
function DepartementGruppeRad({ gruppe, filterAktiv }: { gruppe: DepartementGruppe; filterAktiv: boolean }) {
  const [manueltApen, setManueltApen] = useState(false);
  const apen = filterAktiv || manueltApen;

  return (
    <Details open={apen} onToggle={(e) => setManueltApen((e.target as HTMLDetailsElement).open)} style={{ marginBottom: '0.75rem' }}>
      <Details.Summary>
        <Heading level={2} data-size="sm" style={{ display: 'inline', margin: 0 }}>
          {gruppe.departement}{' '}
          <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', fontWeight: 400 }}>
            ({gruppe.lover.length} {gruppe.lover.length === 1 ? 'lov' : 'lover'})
          </span>
        </Heading>
      </Details.Summary>
      <Details.Content>
        {apen && gruppe.lover.map(({ lov, forskrifter }) => <LovRad key={lov.id} lov={lov} forskrifter={forskrifter} />)}
      </Details.Content>
    </Details>
  );
}

/** Én lov, kollapset som standard — forskriftene under den (Details.Content) rendres kun når brukeren
 * faktisk åpner den, se komponentens hoveddoc-kommentar (ytelse). */
function LovRad({ lov, forskrifter }: { lov: RettskildeSammendrag; forskrifter: RettskildeSammendrag[] }) {
  const [apen, setApen] = useState(false);

  return (
    <Details open={apen} onToggle={(e) => setApen((e.target as HTMLDetailsElement).open)} style={{ marginBottom: '0.4rem' }}>
      <Details.Summary>
        {/* stopPropagation (samme mønster som entitet/Accordion.tsx) — klikk på selve lenken skal
            navigere, ikke (bare) veksle accordion-en. */}
        <span onClick={(e) => e.stopPropagation()}>
          <Link asChild>
            <RouterLink to={`/rettskilder/${lov.id}`}>{lov.tittel}</RouterLink>
          </Link>
        </span>{' '}
        <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
          ({forskrifter.length} {forskrifter.length === 1 ? 'forskrift' : 'forskrifter'})
        </span>
      </Details.Summary>
      <Details.Content>
        {apen &&
          (forskrifter.length === 0 ? (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
              Ingen forskrifter hjemlet i denne loven.
            </Paragraph>
          ) : (
            <List.Unordered>
              {forskrifter.map((f) => (
                <List.Item key={f.id}>
                  <Link asChild>
                    <RouterLink to={`/rettskilder/${f.id}`}>{f.tittel}</RouterLink>
                  </Link>
                </List.Item>
              ))}
            </List.Unordered>
          ))}
      </Details.Content>
    </Details>
  );
}
