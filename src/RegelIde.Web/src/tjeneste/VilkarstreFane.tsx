import { useState, type FormEvent } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Heading, Link, Paragraph, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RegelnodeDto, TjenesteDto } from '../api/types';
import { RegelnodeVelger } from '../vilkarstre/RegelnodeVelger';

export interface VilkarstreFaneProps {
  tjeneste: TjenesteDto;
  rotnode: RegelnodeDto | null;
  regelnoder: RegelnodeDto[];
  onTjenesteOppdatert: (t: TjenesteDto) => void;
}

/** Samme funksjon som tidligere Vilkårstre-seksjonen i TjenesteDetalj.tsx — kun "bytt til
 * eksisterende regelnode" byttet fra et rått `<Select>` til den søkbare `RegelnodeVelger`en. */
export function VilkarstreFane({ tjeneste, rotnode, regelnoder, onTjenesteOppdatert }: VilkarstreFaneProps) {
  const [visOpprettRotnode, setVisOpprettRotnode] = useState(false);
  const [nyRotnodeTittel, setNyRotnodeTittel] = useState('');
  const [visByttRotnode, setVisByttRotnode] = useState(false);
  const [valgtRotnodeId, setValgtRotnodeId] = useState('');
  const [rotnodeEndres, setRotnodeEndres] = useState(false);
  const [rotnodeFeil, setRotnodeFeil] = useState<string | null>(null);

  async function opprettRotnode(e: FormEvent) {
    e.preventDefault();
    if (!nyRotnodeTittel.trim()) return;
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const nyRegelnode = await api.opprettRegelnode({
        tittel: nyRotnodeTittel.trim(), beskrivelse: null, generiskMal: null, barnOperator: 'OG',
        utdataNavn: 'Vedtak', utdataType: 'vedtak', erRotnode: true, juridiskGrunnlag: null,
        innvilgelseTekst: null, avslagTekst: null,
      });
      const oppdatert = await api.settTjenesteRotnode(tjeneste.id, { regelnodeId: nyRegelnode.id });
      onTjenesteOppdatert(oppdatert);
      setVisOpprettRotnode(false);
      setNyRotnodeTittel('');
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

  async function byttRotnode(e: FormEvent) {
    e.preventDefault();
    if (!valgtRotnodeId) return;
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const oppdatert = await api.settTjenesteRotnode(tjeneste.id, { regelnodeId: valgtRotnodeId });
      onTjenesteOppdatert(oppdatert);
      setVisByttRotnode(false);
      setValgtRotnodeId('');
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved bytte av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

  async function fjernRotnode() {
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const oppdatert = await api.fjernTjenesteRotnode(tjeneste.id);
      onTjenesteOppdatert(oppdatert);
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved fjerning av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

  return (
    <Card style={{ maxWidth: '640px', padding: '1rem 1.25rem' }}>
      <Heading level={2} data-size="xs" style={{ marginBottom: '0.75rem' }}>Vilkårstre</Heading>
      {tjeneste.rotnodeId ? (
        <>
          <Paragraph style={{ marginBottom: '0.75rem', display: 'flex', gap: '1rem', alignItems: 'center', flexWrap: 'wrap' }}>
            <span>Rotnode: <strong>{rotnode?.tittel ?? '…'}</strong></span>
            <Link asChild><RouterLink to={`/vilkarstre/${tjeneste.rotnodeId}`}>Åpne vilkårstre →</RouterLink></Link>
            <Link asChild><RouterLink to={`/tjenester/${tjeneste.id}/veiledning`}>Åpne veiledning →</RouterLink></Link>
          </Paragraph>
          <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
            <Button data-size="sm" variant="secondary" onClick={() => setVisByttRotnode((v) => !v)}>
              {visByttRotnode ? 'Avbryt' : 'Bytt rotnode'}
            </Button>
            <Button data-size="sm" variant="tertiary" data-color="danger" disabled={rotnodeEndres} onClick={fjernRotnode}>
              Fjern rotnode
            </Button>
          </div>
          {visByttRotnode && (
            <form onSubmit={byttRotnode} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <RegelnodeVelger regelnoder={regelnoder} value={valgtRotnodeId} onChange={setValgtRotnodeId} label="Ny rotnode (regelnode)" />
              <Button data-size="sm" type="submit" disabled={rotnodeEndres || !valgtRotnodeId}>
                {rotnodeEndres ? 'Setter …' : 'Sett som rotnode'}
              </Button>
            </form>
          )}
        </>
      ) : visOpprettRotnode ? (
        <form onSubmit={opprettRotnode} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <Textfield data-size="sm" label="Rotnodens tittel" value={nyRotnodeTittel} onChange={(e) => setNyRotnodeTittel(e.target.value)} required />
          <Button data-size="sm" type="submit" disabled={rotnodeEndres || !nyRotnodeTittel.trim()}>
            {rotnodeEndres ? 'Oppretter …' : 'Opprett'}
          </Button>
          <Button data-size="sm" variant="tertiary" onClick={() => setVisOpprettRotnode(false)}>Avbryt</Button>
        </form>
      ) : (
        <Button data-size="sm" variant="secondary" onClick={() => { setVisOpprettRotnode(true); setNyRotnodeTittel(`Vedtak om ${tjeneste.tittel.toLowerCase()}`); }}>
          Opprett rotnode
        </Button>
      )}
      {rotnodeFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{rotnodeFeil}</Alert>}
    </Card>
  );
}
