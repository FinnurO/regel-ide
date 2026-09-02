import { useEffect, useState, type Dispatch, type FormEvent, type SetStateAction } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Field, Heading, Label, Link, Paragraph, Select, Spinner, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { HendelseDto, TjenesteDto, TjenesteavhengighetDto, TjenesteTverrTenantTreffDto } from '../api/types';
import type { DetaljVisning } from '../entitet/detaljVisning';

/** 'for'/'avhengig_av'/'input_til' er de generelle relasjonene; de tre første har en presis
 * betydning (docs/03-domenemodell.md §1.5). Uendret fra tidligere TjenesteDetalj.tsx. */
const TJENESTEAVHENGIGHET_REL = [
  { id: 'forutsetning_for', label: 'er forutsetning for' },
  { id: 'gir_mulighet_til', label: 'gir mulighet til' },
  { id: 'utlost_av', label: 'utløses av en hendelse' },
  { id: 'for', label: 'kommer før (generelt)' },
  { id: 'avhengig_av', label: 'er avhengig av (generelt)' },
  { id: 'input_til', label: 'er input til (generelt)' },
];

export interface AvhengigheterFaneProps {
  tjenesteId: string;
  avhengigheter: TjenesteavhengighetDto[] | null;
  setAvhengigheter: Dispatch<SetStateAction<TjenesteavhengighetDto[] | null>>;
  alleTjenester: TjenesteDto[];
  alleHendelser: HendelseDto[];
  onSelectDetail: (v: DetaljVisning) => void;
}

/** Uendret funksjon fra tidligere Tjenesteavhengigheter-seksjonen, kun flyttet ut i egen fil. */
export function AvhengigheterFane({ tjenesteId, avhengigheter, setAvhengigheter, alleTjenester, alleHendelser, onSelectDetail }: AvhengigheterFaneProps) {
  const [nyAvhengighetTilId, setNyAvhengighetTilId] = useState('');
  const [nyAvhengighetRel, setNyAvhengighetRel] = useState('forutsetning_for');
  const [nyAvhengighetHendelseId, setNyAvhengighetHendelseId] = useState('');
  const [nyAvhengighetBeskrivelse, setNyAvhengighetBeskrivelse] = useState('');
  const [leggerTilAvhengighet, setLeggerTilAvhengighet] = useState(false);
  const [avhengighetFeil, setAvhengighetFeil] = useState<string | null>(null);

  const [nyAvhengighetTilOrgnr, setNyAvhengighetTilOrgnr] = useState('');
  const [nyAvhengighetTilNavn, setNyAvhengighetTilNavn] = useState('');
  const [nyAvhengighetTilUrl, setNyAvhengighetTilUrl] = useState('');
  const [tverrTenantSok, setTverrTenantSok] = useState('');
  const [tverrTenantTreff, setTverrTenantTreff] = useState<TjenesteTverrTenantTreffDto[]>([]);
  const [tverrTenantSokerLaster, setTverrTenantSokerLaster] = useState(false);
  const [valgtTverrTenantTreff, setValgtTverrTenantTreff] = useState<TjenesteTverrTenantTreffDto | null>(null);

  function velgTilTjeneste(tjId: string, treff: TjenesteTverrTenantTreffDto | null = null) {
    setNyAvhengighetTilId(tjId);
    setValgtTverrTenantTreff(treff);
    setNyAvhengighetTilOrgnr('');
    setNyAvhengighetTilNavn('');
    setNyAvhengighetTilUrl('');
  }

  function endreEkstern(felt: 'orgnr' | 'navn' | 'url', verdi: string) {
    if (felt === 'orgnr') setNyAvhengighetTilOrgnr(verdi);
    else if (felt === 'navn') setNyAvhengighetTilNavn(verdi);
    else setNyAvhengighetTilUrl(verdi);
    if (felt !== 'url') {
      setNyAvhengighetTilId('');
      setValgtTverrTenantTreff(null);
    }
  }

  useEffect(() => {
    if (!tverrTenantSok.trim()) { setTverrTenantTreff([]); return; }
    setTverrTenantSokerLaster(true);
    const tidsavbrudd = setTimeout(() => {
      api.sokTjenesterTverrTenant(tverrTenantSok.trim())
        .then(setTverrTenantTreff)
        .catch(() => setTverrTenantTreff([]))
        .finally(() => setTverrTenantSokerLaster(false));
    }, 300);
    return () => clearTimeout(tidsavbrudd);
  }, [tverrTenantSok]);

  async function leggTilAvhengighet(e: FormEvent) {
    e.preventDefault();
    // [Endret, 2026-08-29] Navn alene er nok — organisasjonsnummer er valgfritt på serveren
    // (se TjenesteavhengighetregisterTjeneste.OpprettAsync), for konseptuelle eksterne motparter uten
    // et ekte norsk orgnummer («en utenlandsk vigselsmyndighet»). Denne siden krevde tidligere BEGGE
    // felt og kunne dermed aldri sende inn nettopp den kombinasjonen backend-endringen muliggjorde.
    const harEksternMal = !nyAvhengighetTilId && nyAvhengighetTilNavn.trim();
    if (!nyAvhengighetTilId && !harEksternMal) return;
    setAvhengighetFeil(null);
    setLeggerTilAvhengighet(true);
    try {
      const oppdatert = await api.opprettTjenesteavhengighet(tjenesteId, {
        tilTjenesteId: nyAvhengighetTilId || null,
        rel: nyAvhengighetRel,
        hendelseId: nyAvhengighetRel === 'utlost_av' ? nyAvhengighetHendelseId || null : null,
        beskrivelse: nyAvhengighetBeskrivelse.trim() || null,
        tilOrganisasjonsnummer: nyAvhengighetTilId ? null : nyAvhengighetTilOrgnr.trim() || null,
        tilNavn: nyAvhengighetTilId ? null : nyAvhengighetTilNavn.trim() || null,
        tilUrl: nyAvhengighetTilId ? null : nyAvhengighetTilUrl.trim() || null,
      });
      setAvhengigheter(oppdatert);
      velgTilTjeneste('');
      setTverrTenantSok('');
      setTverrTenantTreff([]);
      setNyAvhengighetHendelseId('');
      setNyAvhengighetBeskrivelse('');
    } catch (err) {
      setAvhengighetFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av avhengighet.');
    } finally {
      setLeggerTilAvhengighet(false);
    }
  }

  async function fjernAvhengighet(avhengighetId: string) {
    await api.slettTjenesteavhengighet(avhengighetId);
    setAvhengigheter((forrige) => (forrige ?? []).filter((a) => a.id !== avhengighetId));
  }

  return (
    <div style={{ maxWidth: '800px' }}>
      <Heading level={2} data-size="xs" style={{ marginBottom: '0.75rem' }}>Tjenesteavhengigheter</Heading>
      <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
        Rettede, årsaksforklarte koblinger mellom to tjenester (docs/03-domenemodell.md §1.5) — ett
        rettet kant per relasjon, vist med riktig tekst uansett hvilken side du ser fra.
      </Paragraph>
      {avhengigheter === null && <Spinner aria-label="Laster …" data-size="sm" />}
      {avhengigheter && avhengigheter.length === 0 && <Paragraph>Ingen tjenesteavhengigheter registrert ennå.</Paragraph>}
      {avhengigheter && avhengigheter.length > 0 && (
        <ul style={{ marginBottom: '1rem' }}>
          {avhengigheter.map((a) => (
            <li key={a.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <button type="button"
                onClick={() => onSelectDetail({ title: a.visningstekst, meta: `Avhengighet · ${a.rel}`, body: a.beskrivelse })}
                style={{ background: 'none', border: 'none', padding: 0, font: 'inherit', color: 'inherit', cursor: 'pointer' }}>
                {a.visningstekst}
              </button>
              {a.motpartTjenesteId && (
                <Link asChild style={{ fontSize: 'var(--ds-font-size-1)' }}><RouterLink to={`/tjenester/${a.motpartTjenesteId}`}>↗</RouterLink></Link>
              )}
              {a.motpartOrganisasjonsnummer && <Tag data-color="info" data-size="sm">org.nr {a.motpartOrganisasjonsnummer}</Tag>}
              {a.motpartUrl && <Link href={a.motpartUrl} target="_blank" rel="noreferrer" style={{ fontSize: 'var(--ds-font-size-1)' }}>↗</Link>}
              {a.beskrivelse && <Tag data-color="neutral" data-size="sm">{a.beskrivelse}</Tag>}
              <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernAvhengighet(a.id)}>Fjern</Button>
            </li>
          ))}
        </ul>
      )}

      <form onSubmit={leggTilAvhengighet} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
        <Field>
          <Label>Relasjon (denne tjenesten …)</Label>
          <Select data-size="sm" value={nyAvhengighetRel} onChange={(e) => setNyAvhengighetRel(e.target.value)}>
            {TJENESTEAVHENGIGHET_REL.map((r) => <Select.Option key={r.id} value={r.id}>{r.label}</Select.Option>)}
          </Select>
        </Field>
        <Field>
          <Label>Til tjeneste (egen virksomhet)</Label>
          <Select data-size="sm" value={nyAvhengighetTilId} onChange={(e) => velgTilTjeneste(e.target.value)}>
            <Select.Option value="">Velg …</Select.Option>
            {alleTjenester.filter((t) => t.id !== tjenesteId).map((t) => <Select.Option key={t.id} value={t.id}>{t.tittel}</Select.Option>)}
          </Select>
        </Field>
        {nyAvhengighetRel === 'utlost_av' && (
          <Field>
            <Label>Hendelse</Label>
            <Select data-size="sm" value={nyAvhengighetHendelseId} onChange={(e) => setNyAvhengighetHendelseId(e.target.value)}>
              <Select.Option value="">Velg …</Select.Option>
              {alleHendelser.map((h) => <Select.Option key={h.id} value={h.id}>{h.navn}</Select.Option>)}
            </Select>
          </Field>
        )}
        <Textfield data-size="sm" label="Nyanse/unntak (valgfritt)" value={nyAvhengighetBeskrivelse}
          onChange={(e) => setNyAvhengighetBeskrivelse(e.target.value)} style={{ minWidth: '16rem' }} />
        <Button data-size="sm" type="submit"
          disabled={leggerTilAvhengighet || (!nyAvhengighetTilId && !nyAvhengighetTilNavn.trim())}>
          {leggerTilAvhengighet ? 'Oppretter …' : 'Opprett avhengighet'}
        </Button>
      </form>

      <div style={{ paddingTop: '0.75rem', borderTop: '1px solid var(--ds-color-neutral-border-subtle)' }}>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
          Eller finn en ANNEN virksomhets publiserte tjeneste, eller — hvis den ikke finnes som en ekte
          tjeneste i Regel-IDE i det hele tatt — oppgi den som en ekstern referanse manuelt nedenfor.
          Navn er det eneste påkrevde feltet; organisasjonsnummer brukes som bindingsnøkkel når
          motparten faktisk har et, men en konseptuell motpart uten et ekte norsk orgnummer (f.eks. en
          utenlandsk myndighet) kan opprettes med navn alene.
        </Paragraph>
        <Textfield data-size="sm" label="Søk i andre virksomheters publiserte tjenester" value={tverrTenantSok}
          onChange={(e) => setTverrTenantSok(e.target.value)} style={{ maxWidth: '24rem', marginBottom: '0.5rem' }} />
        {tverrTenantSokerLaster && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Søker …</Paragraph>}
        {!tverrTenantSokerLaster && tverrTenantSok.trim() && tverrTenantTreff.length === 0 && (
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Ingen treff.</Paragraph>
        )}
        {tverrTenantTreff.length > 0 && (
          <ul style={{ maxHeight: '12rem', overflow: 'auto', marginBottom: '0.5rem' }}>
            {tverrTenantTreff.map((t) => (
              <li key={t.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.2rem' }}>
                <span style={{ flex: 1, fontSize: 'var(--ds-font-size-1)' }}>
                  {t.tittel} <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>({t.virksomhetNavn})</span>
                </span>
                <Button data-size="sm" variant="tertiary" onClick={() => velgTilTjeneste(t.id, t)}>Velg</Button>
              </li>
            ))}
          </ul>
        )}
        {valgtTverrTenantTreff && (
          <Tag data-color="success" data-size="sm" style={{ marginBottom: '0.5rem' }}>
            Valgt: {valgtTverrTenantTreff.tittel} ({valgtTverrTenantTreff.virksomhetNavn})
          </Tag>
        )}
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <Textfield data-size="sm" label="Avansert / manuell — organisasjonsnummer (valgfritt)" value={nyAvhengighetTilOrgnr}
            onChange={(e) => endreEkstern('orgnr', e.target.value)} style={{ minWidth: '12rem' }} />
          <Textfield data-size="sm" label="Navn på tjenesten" value={nyAvhengighetTilNavn}
            onChange={(e) => endreEkstern('navn', e.target.value)} style={{ minWidth: '16rem' }} />
          <Textfield data-size="sm" label="URL (valgfritt)" value={nyAvhengighetTilUrl}
            onChange={(e) => endreEkstern('url', e.target.value)} style={{ minWidth: '14rem' }} />
        </div>
      </div>
      {avhengighetFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{avhengighetFeil}</Alert>}
    </div>
  );
}
