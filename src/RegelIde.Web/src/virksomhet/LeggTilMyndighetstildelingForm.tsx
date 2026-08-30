import { useEffect, useState } from 'react';
import { Alert, Button, Card, Field, Label, Paragraph, Select, Table, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { MyndighetstildelingDto, ParagrafspennParDto, RettskildeNodeDto, RettskildeSammendrag, VirksomhetsbegrepDto } from '../api/types';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';

export interface LeggTilMyndighetstildelingFormProps {
  virksomhetId: string;
  rettskilder: RettskildeSammendrag[];
  onOpprettet: (ny: MyndighetstildelingDto) => void;
}

/**
 * «Legg til myndighetstildeling»-skjema (docs/13-backlog.md §8.1 punkt 1 — det manglende
 * frontend-skjemaet for `POST /api/myndighetstildelinger`, som tidligere kun kunne kalles via
 * HTTP/Swagger). Kobler ETT eksisterende rollebegrep til DENNE virksomheten, hjemlet i en
 * rettskilde brukeren velger — mennesket velger alltid eksplisitt, ingen gjettet/automatisk kobling.
 *
 * Paragrafspenn-byggeren gjenbruker MØNSTERET fra `KobleRegelverksreferanseForm` (paragraf-`Select`
 * sourced fra rollebegrepets EGEN lov + en «avansert/manuell» eId-`Textfield` som fallback), men
 * bygger en LISTE av `{ FraEid, TilEid? }`-par (docs/20 §7.1) i stedet for én enkelt referanse —
 * `ParagrafspennJson` krever minst ett par, se `MyndighetstildelingTjeneste.OpprettAsync`.
 */
export function LeggTilMyndighetstildelingForm({ virksomhetId, rettskilder, onOpprettet }: LeggTilMyndighetstildelingFormProps) {
  const [rollebegrep, setRollebegrep] = useState<VirksomhetsbegrepDto[] | null>(null);
  const [rollebegrepId, setRollebegrepId] = useState('');
  const [hjemmelRettskildeId, setHjemmelRettskildeId] = useState('');
  const [vilkaar, setVilkaar] = useState('');

  const [noderPerLov, setNoderPerLov] = useState<Map<string, RettskildeNodeDto[]>>(new Map());
  const [paragrafspenn, setParagrafspenn] = useState<ParagrafspennParDto[]>([]);
  const [fraEid, setFraEid] = useState('');
  const [tilEid, setTilEid] = useState('');

  const [oppretter, setOppretter] = useState(false);
  const [feilmelding, setFeilmelding] = useState<string | null>(null);

  useEffect(() => {
    api.hentRollebegrep().then(setRollebegrep).catch(() => setRollebegrep([]));
  }, []);

  const valgtRollebegrep = rollebegrep?.find((r) => r.id === rollebegrepId);
  const lovkildeId = valgtRollebegrep?.lovkildeId ?? null;
  const lovForRollebegrep = lovkildeId ? rettskilder.find((r) => r.id === lovkildeId) : undefined;

  useEffect(() => {
    if (!lovkildeId || noderPerLov.has(lovkildeId)) return;
    api.hentNoder(lovkildeId)
      .then((noder) => setNoderPerLov((forrige) => new Map(forrige).set(lovkildeId, noder)))
      .catch(() => { /* Ingen gjettet fallback — brukeren faller tilbake til manuell eId under. */ });
  }, [lovkildeId, noderPerLov]);

  // Samme filter som KobleRegelverksreferanseForm: kun blad-noder med en faktisk paragraf/nummer,
  // pluss "side"-noder (Brukerveiledning har ingen paragrafinndeling, men er selv en hel referanse).
  const paragrafKandidater = (lovkildeId ? noderPerLov.get(lovkildeId) : undefined)?.filter(
    (n) => n.nodeType === 'side' || (n.nodeType !== 'kapittel' && n.nummer),
  ) ?? [];

  function velgRollebegrep(id: string) {
    setRollebegrepId(id);
    setParagrafspenn([]);
    setFraEid('');
    setTilEid('');
  }

  function leggTilSpenn() {
    if (!fraEid.trim()) return;
    setParagrafspenn((forrige) => [...forrige, { fraEid: fraEid.trim(), tilEid: tilEid.trim() || null }]);
    setFraEid('');
    setTilEid('');
  }

  function fjernSpenn(indeks: number) {
    setParagrafspenn((forrige) => forrige.filter((_, i) => i !== indeks));
  }

  async function opprett() {
    if (!rollebegrepId || !hjemmelRettskildeId || paragrafspenn.length === 0) return;
    setFeilmelding(null);
    setOppretter(true);
    try {
      const ny = await api.opprettMyndighetstildeling({
        rolleBegrepId: rollebegrepId, virksomhetId, hjemmelRettskildeId, paragrafspenn, vilkaar: vilkaar.trim() || null,
      });
      onOpprettet(ny);
      setRollebegrepId('');
      setHjemmelRettskildeId('');
      setVilkaar('');
      setParagrafspenn([]);
    } catch (err) {
      setFeilmelding(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av myndighetstildeling.');
    } finally {
      setOppretter(false);
    }
  }

  return (
    <Card style={{ padding: '1rem', marginTop: '0.75rem' }}>
      <Field style={{ maxWidth: '30rem', marginBottom: '0.75rem' }}>
        <Label>Rollebegrep</Label>
        <Select data-size="sm" value={rollebegrepId} onChange={(e) => velgRollebegrep(e.target.value)} disabled={!rollebegrep}>
          <Select.Option value="">{rollebegrep ? 'Velg rollebegrep …' : 'Laster …'}</Select.Option>
          {rollebegrep?.map((r) => {
            const lov = r.lovkildeId ? rettskilder.find((rk) => rk.id === r.lovkildeId) : undefined;
            return (
              <Select.Option key={r.id} value={r.id}>
                {r.term}{lov ? ` — ${lov.tittel}` : ''}
              </Select.Option>
            );
          })}
        </Select>
        {rollebegrep && rollebegrep.length === 0 && (
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.2rem' }}>
            Ingen rollebegrep opprettet ennå (opprettes via «Koble til …» i lovtekst-visningen, eller <code>POST /api/rollebegrep</code>).
          </Paragraph>
        )}
      </Field>

      <div style={{ marginBottom: '0.75rem' }}>
        <RettskildeVelger rettskilder={rettskilder} value={hjemmelRettskildeId} onChange={setHjemmelRettskildeId}
          label="Hjemmel (forskrift/delegeringsvedtak)" />
      </div>

      {rollebegrepId && (
        <div style={{ marginBottom: '0.75rem' }}>
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', fontWeight: 500, marginBottom: '0.4rem' }}>
            Paragrafspenn i {lovForRollebegrep?.tittel ?? 'rollebegrepets lov'} — minst ett kreves
          </Paragraph>
          {paragrafspenn.length > 0 && (
            <Card style={{ padding: 0, overflow: 'hidden', marginBottom: '0.5rem' }}>
              <Table>
                <Table.Body>
                  {paragrafspenn.map((p, i) => (
                    <Table.Row key={i}>
                      <Table.Cell style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>
                        {p.tilEid ? `${p.fraEid} – ${p.tilEid}` : p.fraEid}
                      </Table.Cell>
                      <Table.Cell>
                        <Button data-size="sm" variant="tertiary" onClick={() => fjernSpenn(i)}>Fjern</Button>
                      </Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
            </Card>
          )}
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
            {paragrafKandidater.length > 0 && (
              <Field style={{ maxWidth: '14rem' }}>
                <Label>Fra paragraf</Label>
                <Select data-size="sm" value={fraEid} onChange={(e) => setFraEid(e.target.value)}>
                  <Select.Option value="">Velg …</Select.Option>
                  {paragrafKandidater.map((n) => (
                    <Select.Option key={n.id} value={n.eid}>
                      {n.nodeType === 'side' ? 'Hele siden' : n.nummer}{n.overskrift ? ` — ${n.overskrift}` : ''}
                    </Select.Option>
                  ))}
                </Select>
              </Field>
            )}
            <Textfield data-size="sm" label="Fra eId (avansert / manuell)" value={fraEid}
              onChange={(e) => setFraEid(e.target.value)} style={{ minWidth: '16rem', fontFamily: 'monospace' }} />
            {paragrafKandidater.length > 0 && (
              <Field style={{ maxWidth: '14rem' }}>
                <Label>Til paragraf (valgfritt)</Label>
                <Select data-size="sm" value={tilEid} onChange={(e) => setTilEid(e.target.value)}>
                  <Select.Option value="">Enkeltpunkt, ikke spenn</Select.Option>
                  {paragrafKandidater.map((n) => (
                    <Select.Option key={n.id} value={n.eid}>
                      {n.nodeType === 'side' ? 'Hele siden' : n.nummer}{n.overskrift ? ` — ${n.overskrift}` : ''}
                    </Select.Option>
                  ))}
                </Select>
              </Field>
            )}
            <Textfield data-size="sm" label="Til eId (valgfritt, avansert / manuell)" value={tilEid}
              onChange={(e) => setTilEid(e.target.value)} style={{ minWidth: '16rem', fontFamily: 'monospace' }} />
            <Button data-size="sm" variant="secondary" type="button" onClick={leggTilSpenn} disabled={!fraEid.trim()}>
              Legg til spenn
            </Button>
          </div>
        </div>
      )}

      <Textfield label="Vilkår (valgfritt)" placeholder="f.eks. kommunale avløpsanlegg" value={vilkaar}
        onChange={(e) => setVilkaar(e.target.value)} style={{ maxWidth: '30rem', marginBottom: '0.75rem' }} />

      <Button type="button" onClick={opprett}
        disabled={oppretter || !rollebegrepId || !hjemmelRettskildeId || paragrafspenn.length === 0}>
        {oppretter ? 'Oppretter …' : 'Opprett myndighetstildeling'}
      </Button>
      {feilmelding && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{feilmelding}</Alert>}
    </Card>
  );
}
