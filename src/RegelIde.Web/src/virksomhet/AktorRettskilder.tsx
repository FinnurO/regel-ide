import { Fragment, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Chip, Heading, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { AktorRettskilderadDto, AktorRettskilderDto, StrukturkantDto } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KILDETYPE_VISNING } from '../strukturkant/KildeUtenforKorpus';
import { StrukturkantTabell, nodeHref } from '../strukturkant/StrukturkantTabell';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { Treffliste } from './KompetanseViaKlasse';
import { filtrer, synligeFiltre, utsagnIRaden, ventendeUtsagn, type RettskildeFilter } from './rettskildeliste';

/**
 * [Ny, issue #441, Johanns avgjørelser 2026-10-30] «Andre rettskilder aktøren står i» på aktørsiden: én rad per rettskilde, sortert med utsagn
 * først (API-et sorterer). Kolonnene er forhold (merkene), kompetanse, plikt, oppgave, annet og venter. Raden foldes ut med Godkjenn/Avvis per
 * utsagn og «Godkjenn alle N». For en kommune står direkte utsagn og utsagn via klasse hver for seg, med svakeste premiss (docs/09 §48).
 * Lovdatas «Ansvarlig departement» er et merke for en sekundær kilde, aldri et utsagn. Rettskilden med hjemmelen for aktøren står i kortet over og
 * gjentas ikke (docs/09 §52).
 */

const VENTER = ['maskinelt_foreslatt', 'manuelt_foreslatt', 'faglig_vurdert'];

function Merker({ r }: { r: AktorRettskilderadDto }) {
  return (
    <span style={{ display: 'inline-flex', gap: '0.25rem', flexWrap: 'wrap' }}>
      {r.gruppe === 'via_klasse' && <Tag data-size="sm" data-color="neutral" title="Utsagnet står på en klasse aktøren er medlem av (avledet).">Via klasse</Tag>}
      {r.ansvarligDepartement && (
        <Tag data-size="sm" data-color="neutral" title="Lovdatas metadatafelt «Ansvarlig departement»: en sekundær kilde, ikke et utsagn i teksten.">
          Ansvarlig dep. (Lovdata, sekundær)
        </Tag>
      )}
      {r.fastsattAv && <Tag data-size="sm" data-color="neutral" title="«Fastsatt av» i hjemmelslinja peker på aktøren (#215).">Fastsatt av</Tag>}
      {r.navnetTagget > 0 && <Tag data-size="sm" data-color="neutral" title="Navneformene er tagget i teksten.">Navnet tagget {r.navnetTagget}</Tag>}
      {r.kildeUtenforKorpusType && (
        <Tag data-size="sm" data-color="warning" title="Kilde utenfor korpus — ikke en hjemmel i lovteksten.">
          {KILDETYPE_VISNING[r.kildeUtenforKorpusType as keyof typeof KILDETYPE_VISNING] ?? r.kildeUtenforKorpusType}
        </Tag>
      )}
    </span>
  );
}

function Utfoldet({ r, virksomhetId, premisser, onEndret }: {
  r: AktorRettskilderadDto; virksomhetId: string; premisser: Map<string, StrukturkantDto>; onEndret: () => void;
}) {
  const [kjorer, setKjorer] = useState<string | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [bekreft, setBekreft] = useState(false);
  const [melding, setMelding] = useState<string | null>(null);
  const ventende = ventendeUtsagn(r);

  async function handling(nokkel: string, f: () => Promise<unknown>, ok?: string) {
    setKjorer(nokkel);
    setFeil(null);
    try {
      await f();
      if (ok) setMelding(ok);
      setBekreft(false);
      onEndret();
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Handlingen feilet.');
    } finally {
      setKjorer(null);
    }
  }

  if (r.gruppe === 'via_klasse') {
    const klasser = new Map(r.viaKlasse.flatMap((t) => t.via).map((n) => [n.id, n]));
    return (
      <>
        <Treffliste treff={r.viaKlasse} premisser={premisser} tomTekst="Ingen treff via klasse." />
        <Metatekst style={{ marginBottom: '0.25rem' }}>
          Utsagnene står på klassen og gjelder aktøren som medlem. Forslagene godkjennes på klassen:{' '}
          {[...klasser.values()].map((n, i) => (
            <span key={n.id}>{i > 0 && ', '}<Link asChild><RouterLink to={nodeHref(n)}>{n.navn}</RouterLink></Link></span>
          ))}
          .
        </Metatekst>
      </>
    );
  }

  return (
    <>
      {r.utsagn.length > 0 ? (
        <StrukturkantTabell kanter={r.utsagn} tomTekst="" visKategori visHjemmelKilde={false}
          handlinger={(k) => VENTER.includes(k.status) ? (
            <span style={{ display: 'inline-flex', gap: '0.25rem' }}>
              <Button data-size="sm" variant="secondary" disabled={kjorer !== null}
                onClick={() => handling(k.id, () => api.godkjennStrukturkant(k.id))}>Godkjenn</Button>
              <Button data-size="sm" variant="tertiary" disabled={kjorer !== null}
                onClick={() => handling(k.id, () => api.avvisStrukturkant(k.id))}>Avvis</Button>
            </span>
          ) : null} />
      ) : (
        <Metatekst style={{ marginBottom: '0.5rem' }}>
          Ingen utsagn om aktøren er registrert i denne rettskilden. Forholdet kommer fra {[
            r.ansvarligDepartement ? 'Lovdatas metadata (ansvarlig departement)' : null,
            r.fastsattAv ? 'hjemmelslinja (fastsatt av)' : null,
            r.navnetTagget > 0 ? 'taggede forekomster av navnet' : null,
            r.navnetreffVenter > 0 ? 'navnetreff som venter' : null,
            r.navneformerVenter > 0 ? 'et navneformforslag' : null,
          ].filter(Boolean).join(', ')}.
        </Metatekst>
      )}
      <span style={{ display: 'inline-flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
        {ventende > 0 && r.rettskildeId && (bekreft ? (
          <>
            <Button data-size="sm" disabled={kjorer !== null}
              onClick={() => handling('alle', () => api.godkjennAlleAktorutsagn(virksomhetId, r.rettskildeId!), `${ventende} utsagn godkjent.`)}>
              Bekreft: godkjenn {ventende}
            </Button>
            <Button data-size="sm" variant="tertiary" onClick={() => setBekreft(false)}>Avbryt</Button>
          </>
        ) : (
          <Button data-size="sm" variant="secondary" onClick={() => setBekreft(true)}>Godkjenn alle {ventende}</Button>
        ))}
        {r.rettskildeId && <Link asChild><RouterLink to={`/rettskilder/${r.rettskildeId}`}>Åpne rettskilden</RouterLink></Link>}
      </span>
      {(r.navnetreffVenter > 0 || r.navneformerVenter > 0) && (
        <Metatekst style={{ marginTop: '0.35rem' }}>
          {r.navnetreffVenter > 0 && `${r.navnetreffVenter} navnetreff venter (fanen «Kandidater»). `}
          {r.navneformerVenter > 0 && `${r.navneformerVenter} navneformforslag venter (fanen «Navneformer»).`}
        </Metatekst>
      )}
      {melding && <Alert data-color="success" data-size="sm" style={{ marginTop: '0.35rem' }}>{melding}</Alert>}
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.35rem' }}>{feil}</Alert>}
    </>
  );
}

export function AktorRettskilder({ virksomhetId, svar, feil, onEndret }: {
  virksomhetId: string; svar: AktorRettskilderDto | null; feil: string | null; onEndret: () => void;
}) {
  const [filter, setFilter] = useState<RettskildeFilter>('alle');
  const [apne, setApne] = useState<Set<string>>(new Set());
  const rader = useMemo(() => filtrer(svar?.rader ?? [], filter), [svar, filter]);
  const paginering = usePaginering(rader);
  const premisser = useMemo(() => new Map((svar?.premisser ?? []).map((p) => [p.id, p])), [svar]);
  const nokkel = (r: AktorRettskilderadDto) => `${r.gruppe}-${r.rettskildeId ?? r.kildeUtenforKorpusType ?? 'uten'}`;
  const visGrupper = (svar?.tall.viaKlasse ?? 0) > 0;

  return (
    <section style={{ marginBottom: '2rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>Andre rettskilder aktøren står i</Heading>
      <Metatekst style={{ marginBottom: '0.75rem' }}>
        Én rad per rettskilde. Tallene teller utsagn der aktøren er part, i begge retninger, og hvert utsagn står i én rad. «Venter» er forslag som
        ikke er godkjent: utsagn, navnetreff og navneformer. Vedtaket med hjemmelen står i kortet over og gjentas ikke her.
        {visGrupper && ' Direkte utsagn og utsagn om en klasse aktøren er medlem av står hver for seg; det som gjelder via klassen, er avledet, og statusen følger det svakeste premisset.'}
      </Metatekst>
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}
      {!svar && !feil && <Spinner aria-label="Laster rettskildene …" data-size="sm" />}
      {svar && (
        <>
          <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
            {synligeFiltre(svar.tall).map((f) => (
              <Chip.Radio key={f.filter} data-size="sm" name="aktor-rettskildefilter" checked={filter === f.filter} onChange={() => setFilter(f.filter)}>
                {f.tekst} {f.antall}
              </Chip.Radio>
            ))}
          </div>
          <Card style={{ padding: rader.length > 0 ? 0 : '1rem', overflow: 'hidden', marginBottom: '0.75rem' }}>
            {rader.length === 0 ? (
              <Paragraph style={{ margin: 0 }}>
                {svar.rader.length === 0 ? 'Aktøren står ikke i noen andre rettskilder: ingen utsagn, merker eller taggede forekomster.' : 'Ingen rader med dette filteret.'}
              </Paragraph>
            ) : (
              <Table data-density="compact">
                <Table.Head>
                  <Table.Row>
                    <Table.HeaderCell>Rettskilde</Table.HeaderCell>
                    <Table.HeaderCell>Forhold</Table.HeaderCell>
                    <Table.HeaderCell style={{ textAlign: 'right' }} title="Kompetanse (K)">Komp.</Table.HeaderCell>
                    <Table.HeaderCell style={{ textAlign: 'right' }} title="Plikt (P)">Plikt</Table.HeaderCell>
                    <Table.HeaderCell style={{ textAlign: 'right' }} title="Oppgave (F)">Oppgave</Table.HeaderCell>
                    <Table.HeaderCell style={{ textAlign: 'right' }} title="Relasjoner, medlemskap, ansvarsområder og andre utsagn">Annet</Table.HeaderCell>
                    <Table.HeaderCell style={{ textAlign: 'right' }}>Venter</Table.HeaderCell>
                  </Table.Row>
                </Table.Head>
                <Table.Body>
                  {paginering.visteRader.map((r, i) => {
                    const n = nokkel(r);
                    const apen = apne.has(n);
                    const forrige = paginering.visteRader[i - 1];
                    const nyGruppe = visGrupper && (i === 0 || forrige.gruppe !== r.gruppe);
                    const kanFoldes = utsagnIRaden(r) > 0 || r.venter > 0 || r.ansvarligDepartement || r.fastsattAv || r.navnetTagget > 0;
                    return (
                      <Fragment key={n}>
                        {nyGruppe && (
                          <Table.Row>
                            <Table.HeaderCell colSpan={7} style={{ background: 'var(--ds-color-neutral-background-tinted)' }}>
                              {r.gruppe === 'direkte' ? 'Direkte' : 'Via klasse (avledet)'}
                            </Table.HeaderCell>
                          </Table.Row>
                        )}
                        <Table.Row>
                          <Table.Cell>
                            {kanFoldes ? (
                              <button type="button" className="tabell-sorter-knapp" aria-expanded={apen}
                                onClick={() => setApne((a) => { const ny = new Set(a); if (ny.has(n)) ny.delete(n); else ny.add(n); return ny; })}>
                                {apen ? '▾' : '▸'} {r.tittel}
                              </button>
                            ) : r.tittel}
                            {r.kildetype && <Metatekst as="span" style={{ marginLeft: '0.35rem' }}>{r.kildetype}</Metatekst>}
                          </Table.Cell>
                          <Table.Cell><Merker r={r} /></Table.Cell>
                          <Table.Cell style={{ textAlign: 'right' }}>{r.kompetanse}</Table.Cell>
                          <Table.Cell style={{ textAlign: 'right' }}>{r.plikt}</Table.Cell>
                          <Table.Cell style={{ textAlign: 'right' }}>{r.oppgave}</Table.Cell>
                          <Table.Cell style={{ textAlign: 'right' }}>{r.annet}</Table.Cell>
                          <Table.Cell style={{ textAlign: 'right' }}>
                            {r.venter}{r.gruppe === 'via_klasse' && r.venter > 0 && <Metatekst as="span"> på klassen</Metatekst>}
                          </Table.Cell>
                        </Table.Row>
                        {apen && (
                          <Table.Row>
                            <Table.Cell colSpan={7} style={{ background: 'var(--ds-color-neutral-background-default)' }}>
                              <Utfoldet r={r} virksomhetId={virksomhetId} premisser={premisser} onEndret={onEndret} />
                            </Table.Cell>
                          </Table.Row>
                        )}
                      </Fragment>
                    );
                  })}
                </Table.Body>
              </Table>
            )}
          </Card>
          {rader.length > 0 && <Pagineringskontroll {...paginering} />}
          {svar.hull.map((h) => <Metatekst key={h} style={{ display: 'block', marginTop: '0.5rem' }}>{h}</Metatekst>)}
          <Metatekst style={{ marginTop: '0.5rem' }}>
            {svar.tall.kanter} utsagn der aktøren er part: {svar.tall.kanterIRadene} i radene og {svar.tall.kanterIHjemmelen} i vedtaket med hjemmelen.
          </Metatekst>
        </>
      )}
    </section>
  );
}
