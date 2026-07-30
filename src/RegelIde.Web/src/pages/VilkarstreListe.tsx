import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Button, Heading, Link, Paragraph, Table, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { TjenesteDto } from '../api/types';

export default function VilkarstreListe() {
  const navigate = useNavigate();
  const [tjenester, setTjenester] = useState<TjenesteDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [oppretterForTjeneste, setOppretterForTjeneste] = useState<string | null>(null);
  const [rotnodeTittel, setRotnodeTittel] = useState('');
  const [oppretter, setOppretter] = useState(false);

  useEffect(() => {
    api.hentTjenester().then(setTjenester)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av tjenester.'));
  }, []);

  async function opprettRotnode(e: FormEvent, tjenesteId: string) {
    e.preventDefault();
    setFeil(null);
    setOppretter(true);
    try {
      const regelnode = await api.opprettRegelnode({
        tittel: rotnodeTittel.trim(), beskrivelse: null, generiskMal: null, barnOperator: 'OG',
        utdataNavn: 'Vedtak', utdataType: 'vedtak', erRotnode: true, juridiskGrunnlag: null,
        innvilgelseTekst: null, avslagTekst: null,
      });
      await api.settTjenesteRotnode(tjenesteId, { regelnodeId: regelnode.id });
      navigate(`/vilkarstre/${regelnode.id}`);
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av rotnode.');
    } finally {
      setOppretter(false);
    }
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Vilkårstre
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Grafeditor for Vilkår/Regel/Unntak (produktkrav kap. 3.4) — velg en tjeneste for å åpne dens vilkårstre.
      </Paragraph>

      {feil && <div className="feilmelding">{feil}</div>}
      {!tjenester && !feil && <Paragraph>Laster …</Paragraph>}
      {tjenester && tjenester.length === 0 && <Paragraph>Ingen tjenester funnet — opprett en under «Tjenester» først.</Paragraph>}

      {tjenester && tjenester.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Tjeneste</Table.HeaderCell>
              <Table.HeaderCell>Vilkårstre</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {tjenester.map((t) => (
              <Table.Row key={t.id}>
                <Table.Cell>{t.tittel}</Table.Cell>
                <Table.Cell>
                  {t.rotnodeId ? (
                    <Link asChild>
                      <RouterLink to={`/vilkarstre/${t.rotnodeId}`}>Åpne vilkårstre</RouterLink>
                    </Link>
                  ) : oppretterForTjeneste === t.id ? (
                    <form onSubmit={(e) => opprettRotnode(e, t.id)} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end' }}>
                      <Textfield data-size="sm" label="Rotnodens tittel" value={rotnodeTittel} onChange={(e) => setRotnodeTittel(e.target.value)} required />
                      <Button data-size="sm" type="submit" disabled={oppretter || !rotnodeTittel.trim()}>
                        {oppretter ? 'Oppretter …' : 'Opprett'}
                      </Button>
                    </form>
                  ) : (
                    <Button data-size="sm" variant="secondary" onClick={() => { setOppretterForTjeneste(t.id); setRotnodeTittel(`Vedtak om ${t.tittel.toLowerCase()}`); }}>
                      Opprett rotnode
                    </Button>
                  )}
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
