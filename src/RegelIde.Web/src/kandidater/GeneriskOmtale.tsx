import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import {
  Alert, Button, Divider, Field, Heading, Label, Link, Radio, Select, Spinner, Table, Tag, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type {
  GeneriskNavneformOversikt, NavneformBindingDto, NavneformBindingRequest, NavnekandidatDto, RettskildeNodeDto,
  UtsagnsgrunnlagRequest, VirksomhetDto, VirksomhetsbegrepDto,
} from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { GodkjenningTag, KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { GruppebegrepVelger } from '../virksomhet/GruppebegrepVelger';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { nodeEtikett } from './Utsagnssteg';

/** Perioden en binding gjelder, som tekst. Åpen periode i begge ender = «gjelder uten tidsavgrensning». */
export function periodeTekst(fra: string | null, til: string | null): string {
  const d = (s: string) => new Date(`${s}T00:00:00`).toLocaleDateString('nb-NO');
  if (!fra && !til) return 'gjelder uten tidsavgrensning';
  if (fra && til) return `gjelder ${d(fra)}–${d(til)}`;
  return fra ? `gjelder fra ${d(fra)}` : `gjelder til ${d(til!)}`;
}

/** Det veiviseren trenger fra «Hva betyr den her?»-steget. `bind` null = ikke valgt. */
export interface GeneriskValg {
  bind: boolean | null;
  referentType: 'virksomhet' | 'begrep' | null;
  virksomhetId: string;
  begrepId: string;
  fraEid: string;
  tilEid: string;
  gyldigFra: string;
  gyldigTil: string;
}

export const TOMT_GENERISK_VALG: GeneriskValg = {
  bind: null, referentType: null, virksomhetId: '', begrepId: '', fraEid: '', tilEid: '', gyldigFra: '', gyldigTil: '',
};

/** Null når valget er fullstendig; ellers hva som mangler. */
export function generiskFeil(v: GeneriskValg): string | null {
  if (v.bind === null) return 'Velg om omtalen bare skal markeres, eller også bindes.';
  if (!v.bind) return null;
  if (!((v.referentType === 'virksomhet' && v.virksomhetId) || (v.referentType === 'begrep' && v.begrepId))) return 'Velg hva den betyr her.';
  if (v.gyldigFra && v.gyldigTil && v.gyldigFra > v.gyldigTil) return '«Gjelder fra» kan ikke være etter «Gjelder til».';
  return null;
}

/**
 * Bindingen veiviseren sender: referent, spenn og periode herfra, grunnlag og status fra de to felles stegene (issue #385 AC8–AC10).
 * «Godkjent» gjør bindingen gjeldende; «faglig vurdert» lar den stå som forslag.
 */
export function tilBinding(v: GeneriskValg, g: UtsagnsgrunnlagRequest): NavneformBindingRequest {
  return {
    tilVirksomhetId: v.referentType === 'virksomhet' ? v.virksomhetId : null,
    tilBegrepId: v.referentType === 'begrep' ? v.begrepId : null,
    paragrafspenn: v.fraEid ? [{ fraEid: v.fraEid, tilEid: v.tilEid || null }] : [],
    hjemmelRettskildeId: g.hjemmelRettskildeId,
    hjemmelEid: g.hjemmelEid,
    kildeUtenforKorpusTekst: g.kildeTekst,
    kildeUtenforKorpusLenke: g.kildeLenker?.[0] ?? null,
    kildeUtenforKorpusLenker: g.kildeLenker,
    kildeUtenforKorpusType: g.kildeType,
    kildeUtenforKorpusDokumentasjon: g.kildeDokumentasjon,
    kunnskapsstatus: g.kunnskapsstatus,
    begrunnelse: g.begrunnelse,
    tolkningsargumenter: g.tolkningsargumenter,
    status: g.godkjenning,
    kommentar: null,
    gyldigFra: v.gyldigFra || null,
    gyldigTil: v.gyldigTil || null,
  };
}

/**
 * [Ny, issue #378 A2/B5/AC7, Johanns godkjenning 2026-10-15] Veiviserens «Generisk omtale»-spor. Viser først hvor termen alt brukes og
 * hva den betyr i hver rettskilde (AC7), med Godkjenn/Avvis på foreslåtte bindinger i kandidatens rettskilde.
 *
 * [ENDRET, issue #385 AC8–AC10] Kontrollert komponent for steget «Hva betyr den her?»: bare markere, eller binde — referent, spenn og
 * egen gyldighet. HVA bindingen bygger på og HVOR SIKKER den er, velges i veiviserens to felles steg, som for navneform, aktør og
 * medlemskap; før #385 hadde dette sporet sine egne felt for det, og en binding herfra ble alltid lagret som godkjent.
 *
 * Ingenting forhåndsvelges (CLAUDE.md §8, docs/09 §31).
 */
export function GeneriskOmtale({
  kandidat, noder, virksomheter, verdi, onEndre, disabled,
}: {
  kandidat: NavnekandidatDto;
  noder: RettskildeNodeDto[] | null;
  virksomheter: VirksomhetDto[];
  verdi: GeneriskValg;
  onEndre: (v: GeneriskValg) => void;
  disabled: boolean;
}) {
  const term = kandidat.foreslattTekst.trim().toLowerCase();
  const [oversikt, setOversikt] = useState<GeneriskNavneformOversikt | null | undefined>(undefined);
  const [begreper, setBegreper] = useState<VirksomhetsbegrepDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const sett = (endring: Partial<GeneriskValg>) => onEndre({ ...verdi, ...endring });

  function lastOversikt() {
    api.hentGeneriskeNavneformer(term).then((r) => setOversikt(r[0] ?? null)).catch(() => setOversikt(null));
  }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(lastOversikt, [term]);
  useEffect(() => {
    if (verdi.referentType !== 'begrep' || begreper !== null) return;
    api.hentGruppebegrep().then((b) => setBegreper(b.filter((x) => x.begrepskategori === 'klasse' || x.begrepskategori === 'rolle')))
      .catch(() => setBegreper([]));
  }, [verdi.referentType, begreper]);

  const perId = new Map((noder ?? []).map((n) => [n.id, n]));
  const paragrafer = (noder ?? []).filter((n) => !n.opphevet && (n.nodeType === 'side' || n.nodeType === 'kapittel' || n.nodeType === 'paragraf'));
  const herBindinger = oversikt?.rettskilder.find((r) => r.rettskildeId === kandidat.rettskildeId)?.bindinger ?? [];

  async function behandleBinding(b: NavneformBindingDto, handling: 'godkjenn' | 'avvis') {
    setFeil(null);
    try {
      if (handling === 'godkjenn') await api.godkjennNavneformBinding(b.id);
      else await api.avvisNavneformBinding(b.id);
      lastOversikt();
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Handlingen feilet.');
    }
  }

  return (
    <div style={{ marginBottom: '0.75rem' }}>
      <Heading level={3} data-size="xs" style={{ marginBottom: '0.35rem' }}>«{term}» i korpuset</Heading>
      {oversikt === undefined ? <Spinner aria-label="Laster …" data-size="sm" /> : oversikt === null ? (
        <Metatekst>«{term}» er ikke registrert som generisk omtale ennå — den blir det når du fullfører.</Metatekst>
      ) : (
        <>
          <Metatekst>
            Generisk omtale, tagget {oversikt.antallTagger} ganger i {oversikt.rettskilder.length} rettskilder. Hva den betyr, står per
            rettskilde — der det ikke er en godkjent binding, er omtalen ukoblet.
          </Metatekst>
          <Table data-size="sm" data-density="compact" border style={{ marginBottom: '0.5rem' }}>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Rettskilde</Table.HeaderCell>
                <Table.HeaderCell>Tagger</Table.HeaderCell>
                <Table.HeaderCell>Betyr</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {oversikt.rettskilder.map((r) => (
                <Table.Row key={r.rettskildeId}>
                  <Table.Cell>
                    <Link asChild><RouterLink to={`/rettskilder/${r.rettskildeId}`}>{r.rettskildeTittel}</RouterLink></Link>
                  </Table.Cell>
                  <Table.Cell>{r.antallTagger}</Table.Cell>
                  <Table.Cell>
                    {r.bindinger.length === 0 ? <Metatekst as="span">Ikke bundet</Metatekst> : r.bindinger.map((b) => (
                      <div key={b.id} style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
                        <Link asChild>
                          <RouterLink to={b.referent.type === 'virksomhet' ? `/virksomheter/${b.referent.id}` : `/begreper/${b.referent.id}`}>
                            {b.referent.navn}
                          </RouterLink>
                        </Link>
                        {b.paragrafspenn.length > 0 && <Metatekst as="span">({b.paragrafspenn.length} spenn)</Metatekst>}
                        <Metatekst as="span">{periodeTekst(b.gyldigFra, b.gyldigTil)}</Metatekst>
                        <KunnskapsstatusTag status={b.kunnskapsstatus} />
                        <GodkjenningTag status={b.status} oppdagelsesKilde={b.oppdagelsesKilde} />
                        {/* [Ny, issue #385 AC12] Som strukturkantene (docs/09 §35). */}
                        {b.hjemmelEndretTidspunkt && <Tag data-size="sm" data-color="warning">Hjemmel endret – vurder på nytt</Tag>}
                        {r.rettskildeId === kandidat.rettskildeId && (b.status !== 'godkjent' || b.hjemmelEndretTidspunkt) && !disabled && (
                          <>
                            <Button data-size="sm" variant="secondary" onClick={() => behandleBinding(b, 'godkjenn')}>Godkjenn</Button>
                            <Button data-size="sm" variant="tertiary" onClick={() => behandleBinding(b, 'avvis')}>Avvis</Button>
                          </>
                        )}
                      </div>
                    ))}
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        </>
      )}

      <Divider style={{ margin: '0.75rem 0' }} />
      <Field data-size="sm" style={{ marginBottom: '0.5rem' }}>
        <Radio name="generiskBind" label="Bare marker som generisk" value="nei" disabled={disabled}
          description="Omtalen kobles til den generiske navneformen, uten betydning for denne rettskilden ennå. Den står da som «generisk» i aktørsveipet."
          checked={verdi.bind === false} onChange={() => sett({ bind: false })} />
        <Radio name="generiskBind" label="Marker og bind for denne rettskilden" value="ja" disabled={disabled}
          description={herBindinger.length > 0 ? 'Det finnes alt en binding her — en ny må ha et annet paragrafspenn eller en annen periode.' : `Hva «${term}» betyr her.`}
          checked={verdi.bind === true} onChange={() => sett({ bind: true })} />
      </Field>

      {verdi.bind && (
        <div style={{ display: 'grid', gap: '0.6rem', marginBottom: '0.6rem' }}>
          <Field data-size="sm">
            <Radio name="referentType" label="En aktør" value="virksomhet" checked={verdi.referentType === 'virksomhet'} disabled={disabled}
              onChange={() => sett({ referentType: 'virksomhet' })} />
            <Radio name="referentType" label="En klasse eller rolle («kommunen» som barnehagemyndighet)" value="begrep" disabled={disabled}
              checked={verdi.referentType === 'begrep'} onChange={() => sett({ referentType: 'begrep' })} />
          </Field>
          {verdi.referentType === 'virksomhet' && (
            <VirksomhetVelger virksomheter={virksomheter} value={verdi.virksomhetId} onChange={(virksomhetId) => sett({ virksomhetId })}
              label="Betyr aktøren" tomValgTekst="Velg aktør …" style={{ maxWidth: '28rem' }} />
          )}
          {verdi.referentType === 'begrep' && (begreper === null ? <Spinner aria-label="Laster …" data-size="sm" /> : begreper.length === 0 ? (
            <Alert data-color="warning" data-size="sm">Det finnes ingen klasse- eller rollebegrep. Opprett begrepet først.</Alert>
          ) : (
            <GruppebegrepVelger gruppebegrep={begreper} value={verdi.begrepId} onChange={(begrepId) => sett({ begrepId })} label="Betyr klassen/rollen"
              tomValgTekst="Velg klasse eller rolle …" style={{ maxWidth: '28rem' }} />
          ))}

          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <Field data-size="sm" style={{ maxWidth: '16rem' }}>
              <Label>Gjelder i (valgfritt — tomt = hele rettskilden)</Label>
              <Select data-size="sm" value={verdi.fraEid} disabled={disabled} onChange={(e) => sett({ fraEid: e.target.value })}>
                <Select.Option value="">Hele rettskilden</Select.Option>
                {paragrafer.map((n) => <Select.Option key={n.id} value={n.eid}>{nodeEtikett(n, perId)}</Select.Option>)}
              </Select>
            </Field>
            {verdi.fraEid && (
              <Field data-size="sm" style={{ maxWidth: '16rem' }}>
                <Label>Til og med (valgfritt)</Label>
                <Select data-size="sm" value={verdi.tilEid} disabled={disabled} onChange={(e) => sett({ tilEid: e.target.value })}>
                  <Select.Option value="">Bare denne</Select.Option>
                  {paragrafer.map((n) => <Select.Option key={n.id} value={n.eid}>{nodeEtikett(n, perId)}</Select.Option>)}
                </Select>
              </Field>
            )}
          </div>

          {/* [Ny, issue #378, Johanns beslutning 2026-10-16] Bindingens egen gyldighet — f.eks. til en ressortendring. Tomt = åpen ende. */}
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <Textfield data-size="sm" type="date" label="Gjelder fra (valgfritt)" value={verdi.gyldigFra} readOnly={disabled}
              onChange={(e) => sett({ gyldigFra: e.target.value })} />
            <Textfield data-size="sm" type="date" label="Gjelder til (valgfritt)" value={verdi.gyldigTil} readOnly={disabled}
              onChange={(e) => sett({ gyldigTil: e.target.value })} />
          </div>
          {verdi.gyldigFra && verdi.gyldigTil && verdi.gyldigFra > verdi.gyldigTil && (
            <Alert data-color="warning" data-size="sm">«Gjelder fra» kan ikke være etter «Gjelder til».</Alert>
          )}
        </div>
      )}

      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.5rem' }}>{feil}</Alert>}
    </div>
  );
}
