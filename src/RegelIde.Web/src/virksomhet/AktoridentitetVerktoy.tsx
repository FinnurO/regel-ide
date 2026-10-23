import { useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Details, Link, Paragraph, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { EtterfyllingRapport, NavneformforslagRapport, Navneformforslagutfall, Treffidentitet } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KUNNSKAPSSTATUS_VISNING } from '../strukturkant/Utsagnsstatus';

/**
 * [Ny, issue #385 AC1/AC3, Johanns presisering 2026-10-16] To kjøringer på katalogen, hver med tørrkjøring først og et eget klikk for å
 * lagre (samme mønster som aktørsveipet, docs/09 §38):
 * 1. Forslag til gjeldende navneform fra lovteksten for aktørene som mangler en. Lagrer FORSLAG; ingenting blir aktørens navn før et
 *    menneske godkjenner det på aktørsiden.
 * 2. Kilde og tagg for treffene som alt finnes: navneformen får stedet navnet står, og spennet tagges der teksten er nøyaktig lik.
 */
const UTFALL_TEKST: Record<Navneformforslagutfall, string> = {
  foreslatt: 'Foreslått',
  forslag_finnes: 'Forslag venter alt',
  tidligere_avvist: 'Tidligere avvist',
  navneform_finnes: 'Navneformen finnes (uten grunn «gjeldende»)',
  ingen_treff: 'Ikke funnet i lovteksten',
  ingen_egennavnform: 'Bare små bokstaver eller VERSALER i teksten',
  flere_former: 'Flere skrivemåter — ingen valgt',
};

export function TreffidentitetTekst({ t }: { t: Treffidentitet }) {
  return (
    <>
      {t.navneformForslag} navneformforslag, {t.kilderSatt} kilder satt, {t.taggerOpprettet} tagger opprettet ({t.taggerFantes} fantes),
      {' '}{t.aktortypeForslag} aktørtypeforslag. Ikke tagget: {t.ulikSkrivemaate} med annen skrivemåte, {t.taggerKolliderer} der en annen
      tagg står, {t.taggerUtenEier} uten ansvarlig departement som eier.
    </>
  );
}

export function AktoridentitetVerktoy({ onEndret }: { onEndret: () => void }) {
  const [forslag, setForslag] = useState<NavneformforslagRapport | null>(null);
  const [etterfylling, setEtterfylling] = useState<EtterfyllingRapport | null>(null);
  const [kjorer, setKjorer] = useState<string | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  async function kjor<T>(nokkel: string, f: () => Promise<T>, sett: (r: T) => void) {
    setKjorer(nokkel);
    setFeil(null);
    try {
      sett(await f());
      if (nokkel.endsWith('skriv')) onEndret();
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kjøringen feilet.');
    } finally {
      setKjorer(null);
    }
  }

  return (
    <Details>
      <Details.Summary>Aktøridentitet: navneform og kilde fra lovteksten</Details.Summary>
      <Details.Content>
        <Metatekst style={{ marginBottom: '0.5rem' }}>
          Aktørens navn kommer fra rettskildene; registernavnet og orgnr er attributter. Begge kjøringene vises først som tørrkjøring.
          «Lagre» skriver forslag (godkjennes på aktørsiden) og kilder/tagger med proveniens. Ingenting gjettes: står navnet med flere
          skrivemåter, foreslås ingen.
        </Metatekst>
        <Paragraph data-size="sm" style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <Button data-size="sm" variant="secondary" disabled={kjorer !== null}
            onClick={() => kjor('forslag', () => api.foreslaNavneformer(false), setForslag)}>
            {kjorer === 'forslag' ? 'Leter i lovteksten …' : '1. Finn forslag til gjeldende navneform'}
          </Button>
          {forslag && !forslag.skrevet && forslag.foreslatt > 0 && (
            <Button data-size="sm" disabled={kjorer !== null} onClick={() => kjor('forslag-skriv', () => api.foreslaNavneformer(true), setForslag)}>
              Lagre {forslag.foreslatt} forslag
            </Button>
          )}
          <Button data-size="sm" variant="secondary" disabled={kjorer !== null}
            onClick={() => kjor('etterfyll', () => api.etterfyllKildeOgTagger(false), setEtterfylling)}>
            {kjorer === 'etterfyll' ? 'Går gjennom treffene …' : '2. Finn kilde og tagger for treff som finnes'}
          </Button>
          {etterfylling && !etterfylling.skrevet && (
            <Button data-size="sm" disabled={kjorer !== null}
              onClick={() => kjor('etterfyll-skriv', () => api.etterfyllKildeOgTagger(true), setEtterfylling)}>
              Lagre kilder og tagger
            </Button>
          )}
        </Paragraph>
        {feil && <Alert data-color="danger" data-size="sm">{feil}</Alert>}

        {forslag && (
          <>
            <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.5rem' }}>
              {forslag.skrevet ? 'Lagret: ' : 'Tørrkjøring: '}{forslag.foreslatt} av {forslag.aktorerUtenGjeldendeNavneform} aktører uten godkjent
              gjeldende navneform får et forslag.
            </Alert>
            <Table data-size="sm" data-density="compact" style={{ marginBottom: '0.75rem' }}>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Aktør (registernavn)</Table.HeaderCell>
                  <Table.HeaderCell>Utfall</Table.HeaderCell>
                  <Table.HeaderCell>Lovens skrivemåte</Table.HeaderCell>
                  <Table.HeaderCell>Hvor</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {forslag.rader.map((r) => (
                  <Table.Row key={r.virksomhetId}>
                    <Table.Cell><Link asChild><RouterLink to={`/virksomheter/${r.virksomhetId}`}>{r.registernavn}</RouterLink></Link></Table.Cell>
                    <Table.Cell>
                      <Tag data-size="sm" data-color={r.utfall === 'foreslatt' ? 'info' : 'neutral'}>{UTFALL_TEKST[r.utfall]}</Tag>
                    </Table.Cell>
                    <Table.Cell>
                      {r.form ?? (r.former.length ? r.former.join(' / ') : '—')}
                      {r.kunnskapsstatus && <Metatekst as="span"> ({KUNNSKAPSSTATUS_VISNING[r.kunnskapsstatus].tekst.toLowerCase()})</Metatekst>}
                    </Table.Cell>
                    <Table.Cell>
                      {r.rettskildeTittel ? <Metatekst as="span">{r.rettskildeTittel} — {r.forekomster} forekomster i {r.rettskilder} rettskilder</Metatekst> : '—'}
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </>
        )}

        {etterfylling && (
          <Alert data-color="info" data-size="sm">
            {etterfylling.skrevet ? 'Lagret: ' : 'Tørrkjøring: '}{etterfylling.kantTreff} treff for {etterfylling.aktorer} aktører.{' '}
            <TreffidentitetTekst t={etterfylling.tall} />
          </Alert>
        )}
      </Details.Content>
    </Details>
  );
}
