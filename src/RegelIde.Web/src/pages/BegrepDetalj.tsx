import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Button, Field, Heading, Label, Link, Paragraph, Select, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import type { BegrepDto, RettskildeSammendrag, VilkarDto } from '../api/types';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

export default function BegrepDetalj() {
  const { id } = useParams<{ id: string }>();
  const [begrep, setBegrep] = useState<BegrepDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [bruktIVilkar, setBruktIVilkar] = useState<Array<{ vilkar: VilkarDto; rotnodeId: string | undefined }>>([]);
  const [eierNavn, setEierNavn] = useState<string | null>(null);

  const [term, setTerm] = useState('');
  const [definisjon, setDefinisjon] = useState('');
  const [lovreferanseEid, setLovreferanseEid] = useState('');
  const [begrepstype, setBegrepstype] = useState('faktabegrep');
  const [lagrer, setLagrer] = useState(false);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);
  const [statusEndres, setStatusEndres] = useState(false);

  function fyllSkjemaFra(b: BegrepDto) {
    setTerm(b.term);
    setDefinisjon(b.definisjon);
    setLovreferanseEid(b.lovreferanseEid ?? '');
    setBegrepstype(b.begrepstype);
  }

  useEffect(() => {
    if (!id) return;
    api.hentBegrep(id).then((b) => {
      setBegrep(b);
      fyllSkjemaFra(b);
      api.hentVirksomheter().then((liste) => setEierNavn(liste.find((v) => v.id === b.virksomhetId)?.navn ?? null)).catch(() => setEierNavn(null));
    }).catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av begrep.'));
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
    // «Brukt i vilkår» — bevisst forenkling (kun ett vilkårstre finnes i dag, se plan «Sammenhengende navigasjon»):
    // rotnodeId hentes fra første tjeneste som har en satt, i stedet for en generell reverse-oppslag.
    Promise.all([api.hentVilkarListe(), api.hentTjenester()])
      .then(([vilkarListe, tjenester]) => {
        const rotnodeId = tjenester.find((t) => t.rotnodeId)?.rotnodeId ?? undefined;
        setBruktIVilkar(
          vilkarListe
            .filter((v) => v.begrepId === id || v.skjonnsgrunnlagBegrepId === id)
            .map((v) => ({ vilkar: v, rotnodeId })),
        );
      })
      .catch(() => setBruktIVilkar([]));
  }, [id]);

  async function lagre(e: FormEvent) {
    e.preventDefault();
    if (!id || !begrep) return;
    setLagreFeil(null);
    setLagrer(true);
    try {
      const oppdatert = await api.oppdaterBegrep(id, {
        term: term.trim(), definisjon: definisjon.trim(), lovreferanseEid: lovreferanseEid.trim() || null,
        gjelderFor: begrep.gjelderFor, kodelisteReferanseId: begrep.kodelisteReferanseId,
        skosUrl: begrep.skosUrl, begrepstype,
      });
      setBegrep(oppdatert);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  async function endreStatus(nyStatus: string) {
    if (!id) return;
    setStatusEndres(true);
    setLagreFeil(null);
    try {
      const oppdatert = await api.settBegrepStatus(id, { status: nyStatus });
      setBegrep(oppdatert);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    } finally {
      setStatusEndres(false);
    }
  }

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!begrep) return <Paragraph>Laster …</Paragraph>;

  return (
    <>
      <Heading level={1} data-size="lg">
        «{begrep.term}»
      </Heading>
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '1.5rem' }}>
        <Tag data-color="info">{begrep.status}</Tag>
        <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Eier: {eierNavn ?? '—'}
        </span>
      </div>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Egenskaper
        </Heading>
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
          <Textfield label="Term" value={term} onChange={(e) => setTerm(e.target.value)} required />
          <Field>
            <Label>Definisjon</Label>
            <Textarea value={definisjon} onChange={(e) => setDefinisjon(e.target.value)} rows={3} required />
          </Field>
          <Textfield label="Lovreferanse (eId)" value={lovreferanseEid} onChange={(e) => setLovreferanseEid(e.target.value)}
            style={{ fontFamily: 'monospace' }} />
          {begrep.lovreferanseEid && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginTop: '-0.5rem' }}>
              {(() => {
                const href = rettskildeLenke(begrep.lovreferanseEid, rettskilder);
                return href ? (
                  <Link asChild><RouterLink to={href}>Åpne i rettskilden →</RouterLink></Link>
                ) : (
                  <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Fant ikke rettskilden for denne eId-en.</span>
                );
              })()}
            </Paragraph>
          )}
          <Field>
            <Label>Begrepstype</Label>
            <Select value={begrepstype} onChange={(e) => setBegrepstype(e.target.value)}>
              <Select.Option value="faktabegrep">Faktabegrep</Select.Option>
              <Select.Option value="handlingsbegrep">Handlingsbegrep</Select.Option>
            </Select>
          </Field>
          {lagreFeil && <div className="feilmelding">{lagreFeil}</div>}
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Status
        </Heading>
        <Select value={begrep.status} disabled={statusEndres} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
          {STATUSER.map((s) => (
            <Select.Option key={s} value={s}>{s}</Select.Option>
          ))}
        </Select>
      </section>

      {bruktIVilkar.length > 0 && (
        <section>
          <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
            Brukt i vilkår
          </Heading>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
            {bruktIVilkar.map(({ vilkar, rotnodeId }) =>
              rotnodeId ? (
                <Link asChild key={vilkar.id}>
                  <RouterLink to={`/vilkarstre/${rotnodeId}?fokusVilkar=${vilkar.id}`}>{vilkar.tittel}</RouterLink>
                </Link>
              ) : (
                <span key={vilkar.id}>{vilkar.tittel}</span>
              ),
            )}
          </div>
        </section>
      )}
    </>
  );
}
