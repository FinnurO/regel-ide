import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Alert, Button, Card, Field, Heading, Label, Link, Paragraph, Select, Spinner, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import type { BegrepBruktIRettskildeDto, BegrepDto, RettskildeSammendrag, VilkarDto } from '../api/types';
import { StatusStepper } from '../entitet/StatusStepper';

export default function BegrepDetalj() {
  const { id } = useParams<{ id: string }>();
  const [begrep, setBegrep] = useState<BegrepDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [bruktIVilkar, setBruktIVilkar] = useState<Array<{ vilkar: VilkarDto; rotnodeId: string | undefined }>>([]);
  const [bruktIRettskilder, setBruktIRettskilder] = useState<BegrepBruktIRettskildeDto[]>([]);
  const { visEier } = useVirksomheter();

  const [term, setTerm] = useState('');
  const [definisjon, setDefinisjon] = useState('');
  const [lovreferanseEid, setLovreferanseEid] = useState('');
  const [begrepstype, setBegrepstype] = useState('faktabegrep');
  const [lagrer, setLagrer] = useState(false);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);
  const [statusEndres, setStatusEndres] = useState(false);

  function fyllSkjemaFra(b: BegrepDto) {
    setTerm(b.term);
    setDefinisjon(b.definisjon ?? '');
    setLovreferanseEid(b.lovreferanseEid ?? '');
    setBegrepstype(b.begrepstype ?? 'faktabegrep');
  }

  useEffect(() => {
    if (!id) return;
    api.hentBegrep(id).then((b) => {
      setBegrep(b);
      fyllSkjemaFra(b);
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
    api.hentBegrepBruktIRettskilder(id).then(setBruktIRettskilder).catch(() => setBruktIRettskilder([]));
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

  if (feil) return <Alert data-color="danger">{feil}</Alert>;
  if (!begrep) return <Spinner aria-label="Laster …" data-size="sm" />;

  return (
    <>
      <nav aria-label="Brødsmulesti" style={{ display: 'flex', gap: '0.4rem', fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.6rem', flexWrap: 'wrap' }}>
        <Link asChild><RouterLink to="/begreper">Begreper</RouterLink></Link>
        <span>/</span>
        <span style={{ color: 'var(--ds-color-neutral-text-default)' }}>«{begrep.term}»</span>
      </nav>

      <Heading level={1} data-size="lg">
        «{begrep.term}»
      </Heading>
      <Paragraph style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', margin: '0.5rem 0 1.5rem' }}>
        <Tag data-color="info" data-size="sm">{begrep.status}</Tag>
        {begrep.begrepskategori === 'virksomhet' && <Tag data-color="success" data-size="sm">Virksomhet-navneform</Tag>}
        {begrep.begrepskategori === 'gruppe' && <Tag data-color="success" data-size="sm">Gruppebegrep</Tag>}
        <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Eier: {visEier(begrep.virksomhetId)}
        </span>
      </Paragraph>

      {(begrep.begrepskategori === 'virksomhet' || begrep.begrepskategori === 'gruppe') && (
        <section style={{ marginBottom: '1.5rem' }}>
          <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
            Lenket til
          </Heading>
          {begrep.begrepskategori === 'virksomhet' && begrep.virksomhetReferanseId && (
            <Paragraph>
              Navneform for{' '}
              <Link asChild>
                <RouterLink to={`/virksomheter/${begrep.virksomhetReferanseId}`}>{visEier(begrep.virksomhetReferanseId)}</RouterLink>
              </Link>
            </Paragraph>
          )}
          {begrep.begrepskategori === 'gruppe' && begrep.lovkildeId && (
            <Paragraph>
              Gruppebegrep hjemlet i{' '}
              {(() => {
                const lov = rettskilder.find((r) => r.id === begrep.lovkildeId);
                if (!lov) return <span>{begrep.lovkildeId}</span>;
                // [Rettet, 2026-08-30] Lenk til NØYAKTIG paragrafen (via lovreferanseEid, satt
                // automatisk ved godkjenning fra en navnekandidat, se OpprettGruppebegrepAsync) når
                // den finnes — en bar /rettskilder/{id}-lenke uten eid velger ingen node og lander
                // på en tom side (Johann observerte nettopp dette for «Statsforvalteren»). Faller
                // tilbake til en lenke til hele loven (uten valgt node) for eldre/manuelt opprettede
                // gruppebegrep uten kjent opprinnelsesparagraf.
                const nodeHref = begrep.lovreferanseEid ? rettskildeLenke(begrep.lovreferanseEid, rettskilder) : null;
                return (
                  <Link asChild>
                    <RouterLink to={nodeHref ?? `/rettskilder/${lov.id}`}>{lov.tittel}</RouterLink>
                  </Link>
                );
              })()}
            </Paragraph>
          )}
        </section>
      )}

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Egenskaper
        </Heading>
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
          <Textfield label="Term" value={term} onChange={(e) => setTerm(e.target.value)} required />
          {begrep.begrepskategori !== 'virksomhet' && begrep.begrepskategori !== 'gruppe' && (
            <Field>
              <Label>Definisjon</Label>
              <Textarea value={definisjon} onChange={(e) => setDefinisjon(e.target.value)} rows={3} required />
            </Field>
          )}
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
          {begrep.begrepskategori !== 'virksomhet' && begrep.begrepskategori !== 'gruppe' && (
            <Field>
              <Label>Begrepstype</Label>
              <Select value={begrepstype} onChange={(e) => setBegrepstype(e.target.value)}>
                <Select.Option value="faktabegrep">Faktabegrep</Select.Option>
                <Select.Option value="handlingsbegrep">Handlingsbegrep</Select.Option>
              </Select>
            </Field>
          )}
          {lagreFeil && <Alert data-color="danger">{lagreFeil}</Alert>}
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Status
        </Heading>
        <StatusStepper status={begrep.status} onChange={endreStatus} disabled={statusEndres} />
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Brukt i vilkår
        </Heading>
        <Card style={{ padding: bruktIVilkar.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {bruktIVilkar.length === 0 ? (
            <Paragraph style={{ margin: 0 }}>Ikke brukt i noe vilkår ennå.</Paragraph>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', padding: '0.75rem' }}>
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
          )}
        </Card>
      </section>

      <section>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Brukt i rettskilder
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginTop: '-0.5rem', marginBottom: '0.75rem' }}>
          Steder i importert lovtekst der «{begrep.term}» faktisk forekommer — et ekte tekstsøk, ikke basert på lovreferansen over.
        </Paragraph>
        <Card style={{ padding: bruktIRettskilder.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {bruktIRettskilder.length === 0 ? (
            <Paragraph style={{ margin: 0 }}>Ingen forekomster funnet i importert lovtekst ennå.</Paragraph>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', padding: '0.75rem' }}>
              {bruktIRettskilder.map((r) => (
                <div key={`${r.rettskildeId}-${r.nodeEid}`}>
                  <Link asChild>
                    <RouterLink to={`/rettskilder/${r.rettskildeId}?eid=${encodeURIComponent(r.nodeEid)}`}>{r.rettskildeTittel}</RouterLink>
                  </Link>
                  <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', margin: 0 }}>
                    {r.snippet}
                  </Paragraph>
                </div>
              ))}
            </div>
          )}
        </Card>
      </section>
    </>
  );
}
