import { useEffect, useState, type ReactNode } from 'react';
import { Link as RouterLink } from 'react-router';
import { Card, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { RettskildeNodeDto, StrukturkantDto, Strukturkantkategori, StrukturnodeDto } from '../api/types';
import { paragrafEtikett } from '../rettskilde/paragrafEtikett';
import { Metatekst } from '../entitet/Metatekst';
import { KILDETYPE_VISNING } from './KildeUtenforKorpus';

/**
 * [Ny, issue #311 «Strukturmodell 6», 2026-10-07] ÉN delt visning av strukturkanter (docs/33 §4.3) — brukt av
 * `VirksomhetDetalj`, `GruppeMedlemmer` (BegrepDetalj) og `RettskildeDetalj`. Før #311 hadde hver av de tre
 * kanttypene sin egen tabell på hver side; nå er det samme felt på alle kategorier (hjemmel ELLER kilde
 * utenfor korpus, avgrensning, polaritet, gyldighet, status), og de skal leses likt overalt.
 *
 * <h3>Fargerollene (docs/09 §15, oppdatert i samme runde)</h3>
 * <ul>
 *   <li>Kategorien er en KLASSIFISERING, ikke en status — `neutral`, aldri statusfarger.</li>
 *   <li>Negativ polaritet er `warning`: «kan IKKE instruere» skal ikke kunne leses som at relasjonen gjelder.</li>
 *   <li>Et forslag (`foreslatt_av_ai`) er `info` med oppdagelseskilden i teksten — det er en ubekreftet
 *       påstand, og skal ikke se ut som en validert.</li>
 *   <li>«Ingen hjemmel» er `warning` pluss kilden (docs/09 §18) — et forhold bekreftet mot et org-kart
 *       skal ikke kunne forveksles med et som står i en bestemmelse.</li>
 * </ul>
 */
export const STRUKTURKANT_KATEGORI_VISNING: Record<Strukturkantkategori, { tekst: string; forklaring: string }> = {
  // [ENDRET, #330] «underlagt» var en av de gamle kodene — nå administrativt underordnet.
  R: { tekst: 'Relasjon', forklaring: 'Aktør → aktør (klageinstans for, administrativt underordnet, sekretariat for …).' },
  K: { tekst: 'Kompetanse', forklaring: 'Aktør/rolle → bestemmelse eller sakstype (forskrift, vedtak, tilsyn …).' },
  M: { tekst: 'Medlemskap', forklaring: 'Aktør/klasse/område → klasse eller område. Det som gjelder klassen, gjelder medlemmet.' },
  O: { tekst: 'Områdesammensetning', forklaring: 'Område → område (består av).' },
  A: { tekst: 'Ansvarsområde', forklaring: 'Aktør → område (ansvarsområde, jurisdiksjon, sete).' },
  G: { tekst: 'Organtilhørighet', forklaring: 'Organ/enhet/rolle → rettssubjekt (organ for, del av).' },
  I: { tekst: 'Rolleinnehav', forklaring: 'Aktør → rolle (innehar). Arves ikke.' },
  T: { tekst: 'Klassenivå', forklaring: 'Klasse → rolle/organtype: hvert medlem av klassen skal ha …' },
};

export function StrukturkantKategoriTag({ kategori }: { kategori: Strukturkantkategori }) {
  const v = STRUKTURKANT_KATEGORI_VISNING[kategori];
  return <Tag data-size="sm" data-color="neutral" title={v?.forklaring}>{v?.tekst ?? kategori}</Tag>;
}

/** Lenken til en node: virksomhetssiden eller begrepssiden. */
export function nodeHref(node: StrukturnodeDto): string {
  return node.type === 'virksomhet' ? `/virksomheter/${node.id}` : `/begreper/${node.id}`;
}

/**
 * Visningsteksten med nodenavnene lenket der de ALT står (erstatter `RelasjonstekstMedLenke` — [FJERNET, #311] — og av samme grunn, docs/09
 * §18: malen inneholder navnet, så en påhengt lenke ga dobbelt navn). Hvert navn lenkes én gang, fra venstre;
 * finnes et navn ikke i teksten (en mal uten {0}), legges en egen lenke til etter — aldri en rad uten vei videre.
 */
export function KanttekstMedLenker({ tekst, noder }: { tekst: string; noder: StrukturnodeDto[] }) {
  const deler: ReactNode[] = [];
  const mangler: StrukturnodeDto[] = [];
  let rest = tekst;
  let nokkel = 0;
  for (const node of noder) {
    const i = rest.indexOf(node.navn);
    if (i < 0) { mangler.push(node); continue; }
    deler.push(rest.slice(0, i));
    deler.push(
      <Link asChild key={`l${nokkel++}`}>
        <RouterLink to={nodeHref(node)}>{node.navn}</RouterLink>
      </Link>,
    );
    rest = rest.slice(i + node.navn.length);
  }
  deler.push(rest);
  return (
    <>
      {deler}
      {mangler.map((node) => (
        <span key={node.id}>
          {' '}(<Link asChild><RouterLink to={nodeHref(node)}>{node.navn}</RouterLink></Link>)
        </span>
      ))}
    </>
  );
}

export interface StrukturkantTabellProps {
  /** `null` = laster (Spinner, docs/09 §15). */
  kanter: StrukturkantDto[] | null;
  tomTekst: string;
  /** Vis kategori-tag i hver rad — når tabellen blander kategorier. */
  visKategori?: boolean;
  /** Vis hjemmelens rettskildenavn — utelates på rettskildens egen side (docs/09 §18). */
  visHjemmelKilde?: boolean;
  /** Ekstra kolonne med handlinger per rad (godkjenn/avvis/slett). */
  handlinger?: (kant: StrukturkantDto) => ReactNode;
}

/**
 * Tabellen. Kolonner: utsagnet (med lenker og tagger), avgrensningen (paragrafspenn + tekst + egen gyldighet)
 * og hjemmelen/kilden. `Card` ALLTID rendret med tom-tilstand inni (docs/09 §14/§28).
 */
export function StrukturkantTabell({ kanter, tomTekst, visKategori = false, visHjemmelKilde = true, handlinger }: StrukturkantTabellProps) {
  // Nodene per rettskilde, for «§ 36 sjette ledd» i stedet for rå eId (docs/09 §18) — lazy, samme mønster
  // som VirksomhetDetalj hadde for tildelingene.
  const [noder, setNoder] = useState<Map<string, RettskildeNodeDto[]>>(new Map());
  useEffect(() => {
    const ider = new Set((kanter ?? []).map((k) => k.hjemmelRettskildeId).filter((x): x is string => !!x));
    for (const id of ider) {
      if (noder.has(id)) continue;
      api.hentNoder(id)
        .then((n) => setNoder((forrige) => new Map(forrige).set(id, n)))
        .catch(() => { /* ingen gjettet etikett — rå eId vises */ });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kanter]);

  function paragraf(rettskildeId: string | null, eid: string): string {
    const etikett = rettskildeId ? paragrafEtikett(noder.get(rettskildeId), eid) : undefined;
    if (etikett) return etikett.tekst;
    return eid.split('/nor/').pop() ?? eid;
  }

  return (
    <Card style={{ padding: kanter && kanter.length > 0 ? 0 : '1rem', overflow: 'hidden', marginBottom: '0.75rem' }}>
      {!kanter && <Spinner aria-label="Laster …" data-size="sm" />}
      {kanter && kanter.length === 0 && <Paragraph style={{ margin: 0 }}>{tomTekst}</Paragraph>}
      {kanter && kanter.length > 0 && (
        <Table data-density="compact">
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Utsagn</Table.HeaderCell>
              <Table.HeaderCell>Avgrensning</Table.HeaderCell>
              <Table.HeaderCell>Hjemmel / kilde</Table.HeaderCell>
              {handlinger && <Table.HeaderCell>Handlinger</Table.HeaderCell>}
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {kanter.map((k) => {
              // Motparten er den ANDRE noden sett fra listens node; uten node (per hjemmel) lenkes begge.
              const lenkenoder = k.retning === 'fra' ? (k.til ? [k.til] : [])
                : k.retning === 'til' ? [k.fra]
                : [k.fra, ...(k.til ? [k.til] : [])];
              return (
                <Table.Row key={k.id}>
                  <Table.Cell>
                    <span style={{ display: 'inline-flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                      {visKategori && <StrukturkantKategoriTag kategori={k.kategori} />}
                      <span><KanttekstMedLenker tekst={k.visningstekst} noder={lenkenoder} /></span>
                      {k.polaritet === 'negativ' && (
                        <Tag data-size="sm" data-color="warning" title="Teksten sier at dette IKKE gjelder.">Negativ</Tag>
                      )}
                      {k.status === 'foreslatt_av_ai' && (
                        <Tag data-size="sm" data-color="info" title="Ikke bekreftet av et menneske — regnes ikke som gjeldende.">
                          Forslag ({k.oppdagelsesKilde})
                        </Tag>
                      )}
                    </span>
                  </Table.Cell>
                  <Metatekst as={Table.Cell}>
                    {k.paragrafspenn.length > 0 && (
                      <span style={{ display: 'block' }}>
                        {k.paragrafspenn
                          .map((p) => (p.tilEid
                            ? `${paragraf(k.hjemmelRettskildeId, p.fraEid)} – ${paragraf(k.hjemmelRettskildeId, p.tilEid)}`
                            : paragraf(k.hjemmelRettskildeId, p.fraEid)))
                          .join(', ')}
                      </span>
                    )}
                    {k.avgrensningTekst && <span style={{ display: 'block' }}>{k.avgrensningTekst}</span>}
                    {(k.gyldigFra || k.gyldigTil) && (
                      <span style={{ display: 'block' }}>Gyldig {k.gyldigFra ?? '…'}–{k.gyldigTil ?? '…'}</span>
                    )}
                    {k.paragrafspenn.length === 0 && !k.avgrensningTekst && !k.gyldigFra && !k.gyldigTil && '—'}
                  </Metatekst>
                  <Metatekst as={Table.Cell}>
                    {k.hjemmelRettskildeId ? (
                      <Link asChild>
                        <RouterLink
                          to={(k.hjemmelEid ?? k.paragrafspenn[0]?.fraEid)
                            ? rettskildeLenkeForId(k.hjemmelRettskildeId, (k.hjemmelEid ?? k.paragrafspenn[0]?.fraEid)!)
                            : `/rettskilder/${k.hjemmelRettskildeId}`}
                        >
                          {[visHjemmelKilde ? (k.hjemmelRettskildeTittel ?? 'Rettskilde') : null,
                            k.hjemmelEid ? paragraf(k.hjemmelRettskildeId, k.hjemmelEid) : null]
                            .filter(Boolean).join(' ') || 'Se hjemmelen'}
                        </RouterLink>
                      </Link>
                    ) : (
                      <>
                        {/* [ENDRET, issue #312, Johanns beslutning 2026-10-08] «Ingen hjemmel» bare når kanten heller ikke
                          * har en kildetype. Med kildetype vises TYPEN i stedet (`warning` som før, docs/09 §31): en kant
                          * fra Kartverket eller fra vedtektene er ikke uten kilde, den har en kilde utenfor korpus. */}
                        {k.kildeUtenforKorpusType ? (
                          <Tag data-size="sm" data-color="warning" title="Kilde utenfor korpus — ikke en hjemmel i lovteksten.">
                            {KILDETYPE_VISNING[k.kildeUtenforKorpusType] ?? k.kildeUtenforKorpusType}
                            {k.kildeUtenforKorpusDokumentasjon === 'sekundaer' ? ' (sekundær)' : ''}
                          </Tag>
                        ) : (
                          <Tag data-size="sm" data-color="warning">Ingen hjemmel</Tag>
                        )}{' '}
                        {k.kildeUtenforKorpusLenke ? (
                          <Link href={k.kildeUtenforKorpusLenke} target="_blank" rel="noopener noreferrer">
                            {k.kildeUtenforKorpusTekst}
                          </Link>
                        ) : k.kildeUtenforKorpusTekst}
                      </>
                    )}
                    {k.kommentar && <span style={{ display: 'block' }}>{k.kommentar}</span>}
                  </Metatekst>
                  {handlinger && <Table.Cell>{handlinger(k)}</Table.Cell>}
                </Table.Row>
              );
            })}
          </Table.Body>
        </Table>
      )}
    </Card>
  );
}
