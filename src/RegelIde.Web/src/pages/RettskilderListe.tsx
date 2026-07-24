import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Heading, Paragraph } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RettskildeSammendrag } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';

export default function RettskilderListe() {
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kunMine, setKunMine] = useState(false);
  const { gjeldendeBruker } = useBruker();

  useEffect(() => {
    setFeil(null);
    setRettskilder(null);
    const virksomhetId = kunMine && gjeldendeBruker ? gjeldendeBruker.virksomhetId : undefined;
    api
      .hentRettskilder(virksomhetId)
      .then(setRettskilder)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av rettskilder.'));
  }, [kunMine, gjeldendeBruker]);

  return (
    <>
      <Heading level={1} data-size="lg">
        Rettskilder
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Åpne data — delte/nasjonale kilder (Lov/Forskrift fra Lovdata) og alle virksomheters
        publiserte lokale kilder. Kladder (status «Utkast») vises aldri her.
      </Paragraph>

      {gjeldendeBruker && (
        <label style={{ display: 'block', marginBottom: '1rem' }}>
          <input type="checkbox" checked={kunMine} onChange={(e) => setKunMine(e.target.checked)} />{' '}
          Vis kun {gjeldendeBruker.virksomhetNavn} sine egne kilder
        </label>
      )}

      {feil && <div className="feilmelding">{feil}</div>}

      {!rettskilder && !feil && <Paragraph>Laster …</Paragraph>}

      {rettskilder && rettskilder.length === 0 && <Paragraph>Ingen rettskilder funnet.</Paragraph>}

      {rettskilder && rettskilder.length > 0 && (
        <table className="rettskilde-tabell">
          <thead>
            <tr>
              <th>Tittel</th>
              <th>Kildetype</th>
              <th>ELI</th>
              <th>Eierskap</th>
            </tr>
          </thead>
          <tbody>
            {rettskilder.map((r) => (
              <tr key={r.id}>
                <td>
                  <Link to={`/rettskilder/${r.id}`}>{r.kortnavn ?? r.tittel}</Link>
                </td>
                <td>{r.kildetype}</td>
                <td style={{ fontSize: 'var(--ds-font-size-1)' }}>{r.eli ?? '—'}</td>
                <td>
                  {r.virksomhetId ? (
                    <span className="badge-virksomhet">Virksomhetseid</span>
                  ) : (
                    <span className="badge-delt">Delt / nasjonal</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
