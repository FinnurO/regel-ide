import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { Button, Checkbox, Heading, Paragraph, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RettskildeSammendrag } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';

/**
 * Oppretter en ny håndbok/rundskriv (AK-3.3.8) — alternativ til «Ny kilde»/«Importer AKN» siden en
 * håndbok forfattes direkte i verktøyet, uten ekstern kilde å importere fra (se
 * HandbokForfatterTjeneste.OpprettHandbokAsync i RegelIde.Data).
 */
export default function HandbokOpprett() {
  const navigate = useNavigate();
  const { gjeldendeBruker } = useBruker();

  const [tittel, setTittel] = useState('');
  const [feil, setFeil] = useState<string | null>(null);
  const [laster, setLaster] = useState(false);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [valgteRettskilder, setValgteRettskilder] = useState<Set<string>>(new Set());

  useEffect(() => { api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([])); }, []);

  function vekslRettskilde(id: string, valgt: boolean) {
    setValgteRettskilder((forrige) => {
      const ny = new Set(forrige);
      if (valgt) ny.add(id); else ny.delete(id);
      return ny;
    });
  }

  async function opprett(e: FormEvent) {
    e.preventDefault();
    setFeil(null);
    setLaster(true);
    try {
      const { id } = await api.opprettHandbok({ tittel: tittel.trim() });
      await Promise.all([...valgteRettskilder].map((rettskildeId) => api.leggTilHandbokRettskildeomfang(id, rettskildeId)));
      navigate(`/rettskilder/${id}`);
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av håndbok.');
    } finally {
      setLaster(false);
    }
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Ny håndbok
      </Heading>
      <Paragraph style={{ marginBottom: '1.5rem' }}>
        Innlogget som <strong>{gjeldendeBruker?.navn ?? '(ingen testbruker valgt)'}</strong>,{' '}
        {gjeldendeBruker?.virksomhetNavn}.
      </Paragraph>
      <Paragraph style={{ marginBottom: '1.5rem', maxWidth: '40rem' }}>
        En håndbok/rundskriv er virksomhetens egen forvaltningspraksis og regelverksforståelse, forfattet
        direkte i verktøyet — ikke importert fra en ekstern kilde. Etter opprettelse legger du til
        kapitler og kommentarseksjoner, og kobler dem til aktuelle lovparagrafer.
      </Paragraph>

      <form onSubmit={opprett} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
        <Textfield
          label="Tittel"
          placeholder="f.eks. Alkoholloven med kommentarer"
          value={tittel}
          onChange={(e) => setTittel(e.target.value)}
          required
        />

        {rettskilder.length > 0 && (
          <div>
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginBottom: '0.3rem' }}>
              Rettskilder håndboken omhandler (kan endres senere):
            </Paragraph>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.2rem' }}>
              {rettskilder.map((r) => (
                <Checkbox
                  key={r.id}
                  label={r.tittel}
                  checked={valgteRettskilder.has(r.id)}
                  onChange={(e) => vekslRettskilde(r.id, e.target.checked)}
                />
              ))}
            </div>
          </div>
        )}

        <div>
          <Button type="submit" disabled={laster || !tittel.trim()}>
            {laster ? 'Oppretter …' : 'Opprett'}
          </Button>
        </div>
      </form>
      {feil && <div className="feilmelding" style={{ marginTop: '0.75rem' }}>{feil}</div>}
    </>
  );
}
