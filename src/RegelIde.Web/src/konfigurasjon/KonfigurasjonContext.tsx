import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { api } from '../api/client';
import type { TagKind } from '../tagging/TagTekst';

interface KonfigurasjonContextVerdi {
  taggKinds: TagKind[];
  laster: boolean;
}

const KonfigurasjonContext = createContext<KonfigurasjonContextVerdi | null>(null);

/**
 * Henter global konfigurasjon (i dag: kun tag-kinds) én gang ved oppstart — erstatter en tidligere
 * hardkodet liste i RettskildeDetalj.tsx (2026-07-25). Se TaggKindKonfigurasjonEntitet-kommentaren i
 * RegelIde.Data/Entiteter.cs for hvorfor dette ikke er en generisk Settings-ramme ennå.
 */
export function KonfigurasjonProvider({ children }: { children: ReactNode }) {
  const [taggKinds, setTaggKinds] = useState<TagKind[]>([]);
  const [laster, setLaster] = useState(true);

  useEffect(() => {
    api
      .hentTaggKinds()
      .then((liste) => setTaggKinds(liste.map((k) => ({ id: k.kode, label: k.navn, color: k.farge as TagKind['color'] }))))
      .finally(() => setLaster(false));
  }, []);

  return <KonfigurasjonContext.Provider value={{ taggKinds, laster }}>{children}</KonfigurasjonContext.Provider>;
}

export function useKonfigurasjon() {
  const ctx = useContext(KonfigurasjonContext);
  if (!ctx) throw new Error('useKonfigurasjon må brukes innenfor en KonfigurasjonProvider');
  return ctx;
}
