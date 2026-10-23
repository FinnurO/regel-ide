import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Heading, Link, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { AktortypeUtsagnDto, RettskildeSammendrag, VirksomhetDto, VirksomhetsbegrepDto } from '../api/types';
import { AKTORTYPE_VISNING, ORDNINGSTYPE_VISNING } from '../begrep/Nodetype';
import { Metatekst } from '../entitet/Metatekst';
import { KILDETYPE_VISNING } from '../strukturkant/KildeUtenforKorpus';
import { GodkjenningTag, KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { NavneformgrunnTag } from './Navneformgrunn';

/**
 * [Ny, issue #385 «aktøridentitet», Johann 2026-10-16] «Strengt tatt så er orgnummer og orgnavnet i Brreg kun attributter.» Aktørens
 * identitet og primærnavn kommer fra rettskildene: den gjeldende navneformen. Denne fila viser det på aktørsiden (docs/09 §40):
 * - `AktoridentitetKort`: lovens navn (gjeldende navneform) med grunnlag og status, forslagene som venter (navneform og aktørtype) med
 *   Godkjenn/Avvis, og «Navneform mangler» når det ikke finnes en godkjent gjeldende navneform.
 * - `Registerdata`: registernavn, orgnr, organisasjonsform, sektorkode, overordnet enhet, sist synkronisert — som attributter.
 * - `UtsagnKilde`: «Bygger på» for en navneform eller et aktørtype-utsagn (hjemmel, kilde utenfor korpus med lenker, eller hullet).
 * Et forslag vises som forslag (`info`), aldri som navnet (CLAUDE.md §9: systemtilstand er ikke påstand om verden).
 */

type Grunnlagsbaerer = Pick<VirksomhetsbegrepDto, 'lovkildeId' | 'lovreferanseEid' | 'kildeUtenforKorpusTekst' | 'kildeUtenforKorpusType'
  | 'kildeUtenforKorpusDokumentasjon' | 'kildeUtenforKorpusLenker' | 'kunnskapsstatus' | 'skosUrl'>;

export function UtsagnKilde({ b, rettskilder }: { b: Grunnlagsbaerer; rettskilder: RettskildeSammendrag[] }) {
  if (b.lovkildeId && b.lovreferanseEid) {
    const tittel = rettskilder.find((r) => r.id === b.lovkildeId)?.kortnavn ?? rettskilder.find((r) => r.id === b.lovkildeId)?.tittel ?? 'Rettskilden';
    const paragraf = eidKort(b.lovreferanseEid);
    return (
      <Link asChild>
        <RouterLink to={rettskildeLenkeForId(b.lovkildeId, b.lovreferanseEid)}>{tittel} {paragraf}</RouterLink>
      </Link>
    );
  }
  if (b.kildeUtenforKorpusTekst) {
    return (
      <span>
        {b.kildeUtenforKorpusTekst}
        {b.kildeUtenforKorpusType && (
          <Tag data-size="sm" data-color="neutral" style={{ marginLeft: '0.35rem' }}>
            {KILDETYPE_VISNING[b.kildeUtenforKorpusType]}{b.kildeUtenforKorpusDokumentasjon === 'sekundaer' ? ' (sekundær)' : ''}
          </Tag>
        )}
        {(b.kildeUtenforKorpusLenker ?? []).map((l) => (
          <Metatekst as="span" key={l} style={{ display: 'block' }}>
            <Link href={l} target="_blank" rel="noopener noreferrer">{l}</Link>
          </Metatekst>
        ))}
      </span>
    );
  }
  if (b.skosUrl) {
    return <Link href={b.skosUrl} target="_blank" rel="noopener noreferrer"><Tag data-color="success" data-size="sm">SNL ↗</Tag></Link>;
  }
  if (b.kunnskapsstatus === 'uavklart') return <Tag data-size="sm" data-color="warning">Hjemmel mangler</Tag>;
  return <Metatekst as="span" style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Ikke registrert</Metatekst>;
}

/** «…/nor/§2/ledd-1/punkt-1» → «§ 2 ledd 1 punkt 1». Rå eId når formen er ukjent (ingen gjettet etikett). */
export function eidKort(eid: string): string {
  const hale = eid.split('/nor/')[1];
  if (!hale) return eid;
  return hale.split('/').map((d) => d.replace(/^§/, '§ ').replace(/^(ledd|punkt|kap)-/, '$1 ').replace(/^avslutning/, 'tekst etter punktene')).join(' ');
}

export function erForslag(godkjenning: string | null | undefined): boolean {
  return godkjenning === 'maskinelt_foreslatt' || godkjenning === 'manuelt_foreslatt' || godkjenning === 'faglig_vurdert';
}

export function AktoridentitetKort({
  virksomhet, navneformer, rettskilder, onEndret,
}: {
  virksomhet: VirksomhetDto;
  navneformer: VirksomhetsbegrepDto[] | null;
  rettskilder: RettskildeSammendrag[];
  onEndret: () => void;
}) {
  const [utsagn, setUtsagn] = useState<AktortypeUtsagnDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kjorer, setKjorer] = useState<string | null>(null);

  const last = () => {
    api.hentAktortypeUtsagn(virksomhet.id).then(setUtsagn).catch(() => setUtsagn([]));
  };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(last, [virksomhet.id]);

  const gjeldende = (navneformer ?? []).filter((n) => n.navneformgrunn === 'gjeldende');
  const godkjentGjeldende = gjeldende.find((n) => !erForslag(n.godkjenning));
  const navneforslag = (navneformer ?? []).filter((n) => erForslag(n.godkjenning));
  const typeforslag = (utsagn ?? []).filter((u) => erForslag(u.status));

  async function handling(nokkel: string, f: () => Promise<unknown>) {
    setKjorer(nokkel);
    setFeil(null);
    try {
      await f();
      last();
      onEndret();
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Handlingen feilet.');
    } finally {
      setKjorer(null);
    }
  }

  return (
    <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>Identitet</Heading>
      <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
        Navnet og typen aktøren har etter rettskildene. Registernavn og organisasjonsnummer er attributter, og står under «Registerdata».
      </Metatekst>
      {navneformer === null ? <Spinner aria-label="Laster …" data-size="sm" /> : (
        <Table data-size="sm" style={{ width: '100%', marginBottom: '0.5rem' }}>
          <Table.Body>
            <Table.Row>
              <Table.HeaderCell scope="row">Gjeldende navneform</Table.HeaderCell>
              <Table.Cell>
                {godkjentGjeldende ? (
                  <span style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                    <strong>{godkjentGjeldende.term}</strong>
                    {godkjentGjeldende.kunnskapsstatus && <KunnskapsstatusTag status={godkjentGjeldende.kunnskapsstatus} />}
                    <Metatekst as="span">— <UtsagnKilde b={godkjentGjeldende} rettskilder={rettskilder} /></Metatekst>
                  </span>
                ) : (
                  <span style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                    <Tag data-size="sm" data-color="neutral" variant="outline">Navneform mangler</Tag>
                    <Metatekst as="span">Siden viser registernavnet til en navneform er godkjent.</Metatekst>
                  </span>
                )}
              </Table.Cell>
            </Table.Row>
            {navneforslag.map((n) => (
              <Table.Row key={n.id}>
                <Table.HeaderCell scope="row">Forslag til navneform</Table.HeaderCell>
                <Table.Cell>
                  <span style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                    «{n.term}» <NavneformgrunnTag grunn={n.navneformgrunn} />
                    <KunnskapsstatusTag status={n.kunnskapsstatus ?? null} />
                    <GodkjenningTag status={n.godkjenning!} oppdagelsesKilde={n.oppdagelsesKilde ?? 'ukjent'} />
                    <Button data-size="sm" variant="secondary" disabled={kjorer !== null}
                      onClick={() => handling(n.id, () => api.godkjennNavneform(n.id))}>Godkjenn</Button>
                    <Button data-size="sm" variant="tertiary" disabled={kjorer !== null}
                      onClick={() => handling(n.id, () => api.avvisNavneform(n.id, null))}>Avvis</Button>
                  </span>
                  <Metatekst style={{ margin: '0.2rem 0 0' }}>Bygger på: <UtsagnKilde b={n} rettskilder={rettskilder} /></Metatekst>
                  {n.begrunnelse && <Metatekst style={{ margin: 0, color: 'var(--ds-color-neutral-text-subtle)' }}>{n.begrunnelse}</Metatekst>}
                </Table.Cell>
              </Table.Row>
            ))}
            <Table.Row>
              <Table.HeaderCell scope="row">Aktørtype</Table.HeaderCell>
              <Table.Cell>
                {virksomhet.aktortype
                  ? <strong>{AKTORTYPE_VISNING[virksomhet.aktortype].tekst}{virksomhet.ordningstype ? ` (${ORDNINGSTYPE_VISNING[virksomhet.ordningstype]})` : ''}</strong>
                  : <Tag data-size="sm" data-color="neutral" variant="outline">Aktørtype uavklart</Tag>}
                {utsagn === null && <Spinner aria-label="Laster …" data-size="xs" />}
                {typeforslag.map((u) => (
                  <div key={u.id} style={{ marginTop: '0.35rem' }}>
                    <span style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                      Forslag: {AKTORTYPE_VISNING[u.aktortype].tekst}
                      <KunnskapsstatusTag status={u.kunnskapsstatus} />
                      <GodkjenningTag status={u.status} oppdagelsesKilde={u.oppdagelsesKilde} />
                      <Button data-size="sm" variant="secondary" disabled={kjorer !== null}
                        onClick={() => handling(u.id, () => api.godkjennAktortypeUtsagn(u.id))}>Godkjenn</Button>
                      <Button data-size="sm" variant="tertiary" disabled={kjorer !== null}
                        onClick={() => handling(u.id, () => api.avvisAktortypeUtsagn(u.id, null))}>Avvis</Button>
                    </span>
                    <Metatekst style={{ margin: '0.2rem 0 0' }}>
                      Bygger på: <UtsagnKilde b={{ lovkildeId: u.hjemmelRettskildeId, lovreferanseEid: u.hjemmelEid, kildeUtenforKorpusTekst: u.kildeUtenforKorpusTekst,
                        kildeUtenforKorpusType: u.kildeUtenforKorpusType, kildeUtenforKorpusDokumentasjon: u.kildeUtenforKorpusDokumentasjon,
                        kildeUtenforKorpusLenker: u.kildeUtenforKorpusLenker, kunnskapsstatus: u.kunnskapsstatus, skosUrl: null }} rettskilder={rettskilder} />
                    </Metatekst>
                    {u.begrunnelse && <Metatekst style={{ margin: 0, color: 'var(--ds-color-neutral-text-subtle)' }}>{u.begrunnelse}</Metatekst>}
                  </div>
                ))}
              </Table.Cell>
            </Table.Row>
          </Table.Body>
        </Table>
      )}
      {feil && <Alert data-color="danger" data-size="sm">{feil}</Alert>}
    </Card>
  );
}

/** Registerdata som attributter (AC2). Registernavnet vises alltid her — også når det er likt visningsnavnet. */
export function Registerdata({ virksomhet, overordnetNavn }: { virksomhet: VirksomhetDto; overordnetNavn: string | null }) {
  return (
    <Card style={{ padding: '1rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>Registerdata</Heading>
      <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
        Koblingen til Enhetsregisteret. Attributter, ikke identitet: navnet registeret bruker, beholdes i registerets egen form (issue #158).
      </Metatekst>
      <Table data-size="sm">
        <Table.Body>
          <Table.Row>
            <Table.HeaderCell>Registernavn</Table.HeaderCell>
            <Table.Cell>{virksomhet.organisasjonsnummer ? virksomhet.navn : <Metatekst as="span">Ikke registrert i Enhetsregisteret ({virksomhet.navn})</Metatekst>}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.HeaderCell>Organisasjonsnummer</Table.HeaderCell>
            <Table.Cell style={{ fontFamily: 'monospace' }}>{virksomhet.organisasjonsnummer ?? '—'}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.HeaderCell>Organisasjonsform</Table.HeaderCell>
            <Table.Cell>{virksomhet.organisasjonsformKode ?? '—'}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.HeaderCell>Sektorkode</Table.HeaderCell>
            <Table.Cell>{virksomhet.sektorkode ?? '—'}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.HeaderCell>Overordnet enhet</Table.HeaderCell>
            <Table.Cell>{overordnetNavn ?? '—'}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.HeaderCell>Sist synkronisert</Table.HeaderCell>
            <Table.Cell>{virksomhet.sistBrregSynkronisert ?? 'Aldri (kun seedet)'}</Table.Cell>
          </Table.Row>
        </Table.Body>
      </Table>
    </Card>
  );
}
