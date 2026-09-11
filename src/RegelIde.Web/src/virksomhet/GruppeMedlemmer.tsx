import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Card, Heading, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import { useVirksomheter } from './useVirksomheter';
import { Metatekst } from '../entitet/Metatekst';
import type {
  GruppeMedlemskapDto, MyndighetstildelingDto, ParagrafspennParDto, RettskildeSammendrag,
  VirksomhetsbegrepDto,
} from '../api/types';

/**
 * [Ny, gruppemedlemskap-runden, 2026-09-08, issue #164] Drill-through fra et gruppebegrep til det
 * gruppen FAKTISK inneholder — begge nivåene i hierarkiet, og retningen oppover.
 *
 * <h3>Hvorfor tre lister og ikke én</h3>
 * De tre er ikke tre visninger av det samme, de er tre ULIKE påstander:
 * <ul>
 *   <li><b>Medlemsgrupper</b> (`GruppeMedlemskapEntitet`) — gruppe-av-gruppe. «forvaltningsområdet
 *       for samiske språk» inneholder ikke kommuner direkte; den inneholder de tre
 *       kommune-KATEGORIENE.</li>
 *   <li><b>Virksomheter</b> (`MyndighetstildelingEntitet`) — nivået under: de konkrete, navngitte
 *       organene. «språkutviklingskommuner» inneholder Karasjok, Kautokeino, Nesseby og Tana.</li>
 *   <li><b>Medlem av</b> — motsatt retning, slik at man kan navigere OPP igjen etter å ha gått ned.
 *       Uten den er drill-throughen en enveiskjørt gate.</li>
 * </ul>
 * En sammenslått «medlemmer»-liste ville skjult nettopp det Johann ville se: at Karasjok får sine
 * plikter etter sameloven INDIREKTE, gjennom en gruppe som selv er medlem av en gruppe.
 *
 * <h3>Hjemmelen står per rad, ikke per liste</h3>
 * Hvert medlemskap har sin EGEN hjemmel med sitt eget paragrafspenn — det er hele grunnen til at
 * medlemskapet er en egen entitet og ikke en kolonne (se `GruppeMedlemskapEntitet`). Derfor er
 * «Hjemmel»-kolonnen per rad, og lenker til NØYAKTIG paragrafen medlemskapet står i, ikke bare til
 * rettskilden. En felles «hjemlet i …»-setning over tabellen ville vært en påstand som ikke stemmer
 * så snart to medlemmer kommer fra to ulike forskrifter.
 *
 * <h3>Designmønster (docs/09)</h3>
 * §14: `Card` ALLTID rendret, tom-tilstand som `Paragraph` INNI kortet. §15: `null` = laster ⇒
 * `Spinner`; den negative påstanden («ingen medlemmer») vises aldri mens dataene fortsatt er på vei.
 * §5: `Link asChild` rundt react-router sin `Link`.
 *
 * <p><b>Bevisst IKKE gjort i denne runden</b> (docs/09 §14 sin migreringsplikt, eksplisitt flagget):
 * `BegrepDetalj` er ikke migrert til det delte `KontekstPanel`-mønsteret. Å migrere hele siden er et
 * større, selvstendig grep enn å lukke gruppe-drill-throughen, og ville blandet to ting i samme
 * endring. Seksjonene her følger derfor sidens EKSISTERENDE seksjonsmønster.</p>
 */
export interface GruppeMedlemmerProps {
  gruppeBegrepId: string;
  /** Allerede hentet av kalleren — brukes til å vise hjemmelens TITTEL i stedet for en rå id. */
  rettskilder: RettskildeSammendrag[];
}

/** Halen av en eId etter rettskildens ELI — «§1/ledd-1» i stedet for hele URL-en. Kjenner vi ikke
 * ELI-en, vises eId-en rå: en forkortet visning som gjetter er verre enn en lang som stemmer. */
function paragrafVisning(eid: string, eli: string | null | undefined): string {
  if (eli && eid.startsWith(eli)) return eid.slice(eli.length).replace(/^\//, '');
  return eid;
}

/** Hjemmelen som én celle: rettskildens tittel som lenke til nøyaktig paragrafen, med paragrafspennet
 * som liten metatekst under. Flere spenn listes hver for seg — de er hver sin påstand om HVOR. */
function HjemmelCelle({
  hjemmelRettskildeId, paragrafspenn, rettskilder,
}: {
  hjemmelRettskildeId: string;
  paragrafspenn: ParagrafspennParDto[];
  rettskilder: RettskildeSammendrag[];
}) {
  const hjemmel = rettskilder.find((r) => r.id === hjemmelRettskildeId);
  const forste = paragrafspenn[0];
  const href = forste
    ? rettskildeLenkeForId(hjemmelRettskildeId, forste.fraEid)
    : `/rettskilder/${hjemmelRettskildeId}`;
  return (
    <>
      <Link asChild>
        <RouterLink to={href}>{hjemmel?.tittel ?? 'Se hjemmelen'}</RouterLink>
      </Link>
      {paragrafspenn.length > 0 && (
        <Metatekst as="span" style={{
          display: 'block', fontFamily: 'var(--ds-font-family-mono, monospace)',
          color: 'var(--ds-color-neutral-text-subtle)',
        }}>
          {paragrafspenn
            .map((p) => [p.fraEid, p.tilEid]
              .filter((e): e is string => !!e)
              .map((e) => paragrafVisning(e, hjemmel?.eli))
              .join(' – '))
            .join(', ')}
        </Metatekst>
      )}
    </>
  );
}

/** Gyldighetsperioden, eller ingenting. Et medlemskap uten periode er det NORMALE (det varer så
 * lenge hjemmelen gjør), og «—» i hver rad ville vært støy uten innhold. */
function Gyldighet({ fra, til }: { fra: string | null; til: string | null }) {
  if (!fra && !til) return null;
  return (
    <Tag data-size="sm" data-color="info">
      {fra ?? '…'} – {til ?? '…'}
    </Tag>
  );
}

export function GruppeMedlemmer({ gruppeBegrepId, rettskilder }: GruppeMedlemmerProps) {
  // `null` = ikke hentet ennå (docs/09 §15) — skilt fra `[]` = hentet, og faktisk tomt.
  const [medlemsgrupper, setMedlemsgrupper] = useState<GruppeMedlemskapDto[] | null>(null);
  const [overordnede, setOverordnede] = useState<GruppeMedlemskapDto[] | null>(null);
  const [tildelinger, setTildelinger] = useState<MyndighetstildelingDto[] | null>(null);
  const [gruppebegrep, setGruppebegrep] = useState<VirksomhetsbegrepDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const { virksomheter, visEier } = useVirksomheter();

  useEffect(() => {
    let avbrutt = false;
    setFeil(null);
    setMedlemsgrupper(null);
    setOverordnede(null);
    setTildelinger(null);
    Promise.all([
      api.hentMedlemsgrupper(gruppeBegrepId),
      api.hentOverordnedeGrupper(gruppeBegrepId),
      api.hentMyndighetstildelingerForGruppebegrep(gruppeBegrepId),
      // Gruppebegrepene trengs for å vise TERMEN til en medlemsgruppe — medlemskapsraden bærer bare
      // id-er. Lista er kort (~7) og deles av begge retningene.
      api.hentGruppebegrep(),
    ])
      .then(([medlem, over, tildelt, alle]) => {
        if (avbrutt) return;
        setMedlemsgrupper(medlem);
        setOverordnede(over);
        setTildelinger(tildelt);
        setGruppebegrep(alle);
      })
      .catch((e) => {
        if (avbrutt) return;
        setFeil(e instanceof ApiError ? e.message : 'Kunne ikke hente gruppens medlemmer.');
        // Tom-tilstand er IKKE riktig svar når kallet feilet — da er svaret ukjent, og feilen vises.
        // Settes likevel til [] slik at spinneren ikke står og går for alltid.
        setMedlemsgrupper([]);
        setOverordnede([]);
        setTildelinger([]);
      });
    return () => { avbrutt = true; };
  }, [gruppeBegrepId]);

  /** Termen til et gruppebegrep, eller den rå id-en — aldri en oppfunnet term. */
  function gruppeTerm(id: string): string {
    return gruppebegrep?.find((g) => g.id === id)?.term ?? id;
  }

  return (
    <>
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}

      {/* ---------- Gruppe av gruppe ---------- */}
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Medlemsgrupper
        </Heading>
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '-0.5rem', marginBottom: '0.75rem' }}>
          Grupper som selv er medlem av denne gruppen — ett nivå ned. Hver rad er hjemlet der
          medlemskapet faktisk står, typisk en forskrift, ikke den loven som definerer gruppene.
        </Metatekst>
        <Card style={{ padding: medlemsgrupper && medlemsgrupper.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {medlemsgrupper === null ? (
            <Spinner aria-label="Laster medlemsgrupper …" data-size="sm" />
          ) : medlemsgrupper.length === 0 ? (
            <Paragraph style={{ margin: 0 }}>
              Ingen andre grupper er medlem av denne gruppen. Medlemmene er i så fall konkrete
              virksomheter — se listen under.
            </Paragraph>
          ) : (
            <Table data-size="sm" data-density="compact" style={{ width: '100%' }}>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Gruppe</Table.HeaderCell>
                  <Table.HeaderCell>Hjemmel</Table.HeaderCell>
                  <Table.HeaderCell>Gyldighet</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {medlemsgrupper.map((m) => (
                  <Table.Row key={m.id}>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/begreper/${m.underordnetGruppeBegrepId}`}>
                          «{gruppeTerm(m.underordnetGruppeBegrepId)}»
                        </RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>
                      <HjemmelCelle
                        hjemmelRettskildeId={m.hjemmelRettskildeId}
                        paragrafspenn={m.paragrafspenn}
                        rettskilder={rettskilder}
                      />
                    </Table.Cell>
                    <Table.Cell><Gyldighet fra={m.gyldigFra} til={m.gyldigTil} /></Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
      </section>

      {/* ---------- Nivået under: de konkrete virksomhetene ---------- */}
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Virksomheter i gruppen
        </Heading>
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '-0.5rem', marginBottom: '0.75rem' }}>
          Konkrete, navngitte virksomheter som er tildelt denne gruppen (myndighetstildelinger).
          Vilkår-kolonnen står bare når tildelingen er avgrenset til noe bestemt.
        </Metatekst>
        <Card style={{ padding: tildelinger && tildelinger.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {tildelinger === null ? (
            <Spinner aria-label="Laster virksomheter i gruppen …" data-size="sm" />
          ) : tildelinger.length === 0 ? (
            <Paragraph style={{ margin: 0 }}>
              Ingen virksomheter er tildelt denne gruppen ennå.
            </Paragraph>
          ) : (
            <Table data-size="sm" data-density="compact" style={{ width: '100%' }}>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Virksomhet</Table.HeaderCell>
                  <Table.HeaderCell>Hjemmel</Table.HeaderCell>
                  <Table.HeaderCell>Vilkår</Table.HeaderCell>
                  <Table.HeaderCell>Gyldighet</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {tildelinger.map((t) => (
                  <Table.Row key={t.id}>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/virksomheter/${t.virksomhetId}`}>
                          {virksomheter.find((v) => v.id === t.virksomhetId)?.visningsnavn ?? visEier(t.virksomhetId)}
                        </RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>
                      <HjemmelCelle
                        hjemmelRettskildeId={t.hjemmelRettskildeId}
                        paragrafspenn={t.paragrafspenn}
                        rettskilder={rettskilder}
                      />
                    </Table.Cell>
                    <Table.Cell>
                      {t.vilkaar ?? (
                        <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Ingen</span>
                      )}
                    </Table.Cell>
                    <Table.Cell><Gyldighet fra={t.gyldigFra} til={t.gyldigTil} /></Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
      </section>

      {/* ---------- Retningen oppover ---------- */}
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Medlem av
        </Heading>
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '-0.5rem', marginBottom: '0.75rem' }}>
          Grupper denne gruppen selv er medlem av. Det er denne veien plikter arves indirekte: en
          virksomhet i denne gruppen er også omfattet av det som gjelder for gruppene her.
        </Metatekst>
        <Card style={{ padding: overordnede && overordnede.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {overordnede === null ? (
            <Spinner aria-label="Laster overordnede grupper …" data-size="sm" />
          ) : overordnede.length === 0 ? (
            <Paragraph style={{ margin: 0 }}>
              Denne gruppen er ikke medlem av noen annen gruppe.
            </Paragraph>
          ) : (
            <Table data-size="sm" data-density="compact" style={{ width: '100%' }}>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Gruppe</Table.HeaderCell>
                  <Table.HeaderCell>Hjemmel</Table.HeaderCell>
                  <Table.HeaderCell>Gyldighet</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {overordnede.map((m) => (
                  <Table.Row key={m.id}>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/begreper/${m.overordnetGruppeBegrepId}`}>
                          «{gruppeTerm(m.overordnetGruppeBegrepId)}»
                        </RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>
                      <HjemmelCelle
                        hjemmelRettskildeId={m.hjemmelRettskildeId}
                        paragrafspenn={m.paragrafspenn}
                        rettskilder={rettskilder}
                      />
                    </Table.Cell>
                    <Table.Cell><Gyldighet fra={m.gyldigFra} til={m.gyldigTil} /></Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
      </section>
    </>
  );
}
