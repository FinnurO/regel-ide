import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Card, Heading, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import { Metatekst } from '../entitet/Metatekst';
import type { KildeUtenforKorpusType, ParagrafspennParDto, RettskildeSammendrag, StrukturkantDto } from '../api/types';
import { KILDETYPE_VISNING } from '../strukturkant/KildeUtenforKorpus';
import { StrukturkantTabell } from '../strukturkant/StrukturkantTabell';
import { BegrepskategoriTag } from '../begrep/Nodetype';

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
 * <h3>[ENDRET, issue #311 «Strukturmodell 6»] Én kilde: strukturkantene</h3>
 * Alle tre listene leses nå fra ÉTT kall (`GET /api/strukturkanter?begrepId=…`) i stedet for tre endepunkter
 * mot to tabeller: medlemsgrupper = M-kanter INN fra et begrep, virksomheter = M-/I-kanter INN fra en
 * virksomhet (I for en rolle — «innehas av»), medlem av = M-kanter UT til et begrep. En fjerde seksjon viser
 * øvrige kanter begrepet står i (områdesammensetning, ansvarsområde, kompetanse, klassenivå). Hjemmelen kan
 * nå mangle — da finnes en kilde utenfor korpus, og den vises med samme «Ingen hjemmel»-merke som docs/09 §18.
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
  hjemmelRettskildeId, hjemmelEid, paragrafspenn, rettskilder, kilde, kildetype,
}: {
  hjemmelRettskildeId: string | null;
  /** [Ny, issue #341] HVOR tildelingen står. Før #341 lå det i paragrafspennet (som nå bare er avgrensningen). */
  hjemmelEid: string | null;
  paragrafspenn: ParagrafspennParDto[];
  rettskilder: RettskildeSammendrag[];
  /** [Ny, issue #311] Kilde utenfor korpus — vises når det ikke finnes hjemmel. */
  kilde: string | null;
  /** [Ny, issue #312] Kildetypen — vises i stedet for «Ingen hjemmel» når den finnes (docs/09 §31). */
  kildetype?: KildeUtenforKorpusType | null;
}) {
  if (!hjemmelRettskildeId) {
    return (
      <>
        <Tag data-size="sm" data-color="warning">{kildetype ? (KILDETYPE_VISNING[kildetype] ?? kildetype) : 'Ingen hjemmel'}</Tag>{' '}
        <Metatekst as="span">{kilde}</Metatekst>
      </>
    );
  }
  const hjemmel = rettskilder.find((r) => r.id === hjemmelRettskildeId);
  const forste = hjemmelEid ?? paragrafspenn[0]?.fraEid;
  const href = forste
    ? rettskildeLenkeForId(hjemmelRettskildeId, forste)
    : `/rettskilder/${hjemmelRettskildeId}`;
  return (
    <>
      <Link asChild>
        <RouterLink to={href}>{hjemmel?.tittel ?? 'Se hjemmelen'}</RouterLink>
      </Link>
      {hjemmelEid && (
        <Metatekst as="span" style={{
          display: 'block', fontFamily: 'var(--ds-font-family-mono, monospace)',
          color: 'var(--ds-color-neutral-text-subtle)',
        }}>
          {paragrafVisning(hjemmelEid, hjemmel?.eli)}
        </Metatekst>
      )}
      {paragrafspenn.length > 0 && (
        <Metatekst as="span" style={{
          display: 'block', fontFamily: 'var(--ds-font-family-mono, monospace)',
          color: 'var(--ds-color-neutral-text-subtle)',
        }}>
          {hjemmelEid ? 'gjelder for ' : ''}{paragrafspenn
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
  const [kanter, setKanter] = useState<StrukturkantDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    let avbrutt = false;
    setFeil(null);
    setKanter(null);
    api.hentStrukturkanter({ begrepId: gruppeBegrepId })
      .then((k) => { if (!avbrutt) setKanter(k); })
      .catch((e) => {
        if (avbrutt) return;
        setFeil(e instanceof ApiError ? e.message : 'Kunne ikke hente gruppens medlemmer.');
        // Tom-tilstand er IKKE riktig svar når kallet feilet — da er svaret ukjent, og feilen vises.
        // Settes likevel til [] slik at spinneren ikke står og går for alltid.
        setKanter([]);
      });
    return () => { avbrutt = true; };
  }, [gruppeBegrepId]);

  // [ENDRET, issue #311] De tre listene avledet fra kantene — se klassekommentaren.
  const medlemsgrupper = kanter && kanter.filter((k) => k.kategori === 'M' && k.retning === 'til' && k.fra.type === 'begrep');
  const tildelinger = kanter && kanter.filter((k) => (k.kategori === 'M' || k.kategori === 'I') && k.retning === 'til' && k.fra.type === 'virksomhet');
  const overordnede = kanter && kanter.filter((k) => k.kategori === 'M' && k.retning === 'fra' && k.til?.type === 'begrep');
  const ovrige = kanter && kanter.filter((k) => !medlemsgrupper!.includes(k) && !tildelinger!.includes(k) && !overordnede!.includes(k));

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
                      <span style={{ display: 'inline-flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                        <Link asChild>
                          <RouterLink to={`/begreper/${m.fra.id}`}>«{m.fra.navn}»</RouterLink>
                        </Link>
                        <BegrepskategoriTag kategori={m.fra.nodetype} />
                      </span>
                    </Table.Cell>
                    <Table.Cell>
                      <HjemmelCelle
                        hjemmelRettskildeId={m.hjemmelRettskildeId}
                        hjemmelEid={m.hjemmelEid}
                        paragrafspenn={m.paragrafspenn}
                        rettskilder={rettskilder}
                        kilde={m.kildeUtenforKorpusTekst}
                        kildetype={m.kildeUtenforKorpusType}
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
          Konkrete, navngitte virksomheter som er medlem av denne gruppen — eller, for en rolle, som
          innehar den. Vilkår-kolonnen står bare når tilhørigheten er avgrenset til noe bestemt.
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
                      <span style={{ display: 'inline-flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                        <Link asChild>
                          <RouterLink to={`/virksomheter/${t.fra.id}`}>{t.fra.navn}</RouterLink>
                        </Link>
                        {t.kategori === 'I' && <Tag data-size="sm" data-color="neutral">Innehar rollen</Tag>}
                        {t.polaritet === 'negativ' && <Tag data-size="sm" data-color="warning">Negativ</Tag>}
                        {t.status === 'foreslatt_av_ai' && <Tag data-size="sm" data-color="info">Forslag</Tag>}
                      </span>
                    </Table.Cell>
                    <Table.Cell>
                      <HjemmelCelle
                        hjemmelRettskildeId={t.hjemmelRettskildeId}
                        hjemmelEid={t.hjemmelEid}
                        paragrafspenn={t.paragrafspenn}
                        rettskilder={rettskilder}
                        kilde={t.kildeUtenforKorpusTekst}
                      />
                    </Table.Cell>
                    <Table.Cell>
                      {t.avgrensningTekst ?? (
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
                      <span style={{ display: 'inline-flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                        <Link asChild>
                          <RouterLink to={`/begreper/${m.til!.id}`}>«{m.til!.navn}»</RouterLink>
                        </Link>
                        <BegrepskategoriTag kategori={m.til!.nodetype} />
                      </span>
                    </Table.Cell>
                    <Table.Cell>
                      <HjemmelCelle
                        hjemmelRettskildeId={m.hjemmelRettskildeId}
                        hjemmelEid={m.hjemmelEid}
                        paragrafspenn={m.paragrafspenn}
                        rettskilder={rettskilder}
                        kilde={m.kildeUtenforKorpusTekst}
                        kildetype={m.kildeUtenforKorpusType}
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

      {/* ---------- [Ny, issue #311] Øvrige strukturkanter ---------- */}
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Andre strukturutsagn
        </Heading>
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '-0.5rem', marginBottom: '0.75rem' }}>
          Kompetanse, områdesammensetning, ansvarsområder og klassenivå (docs/33 §4.3) der dette begrepet er
          en av endene.
        </Metatekst>
        <StrukturkantTabell kanter={ovrige} tomTekst="Ingen andre strukturutsagn." visKategori />
      </section>
    </>
  );
}
