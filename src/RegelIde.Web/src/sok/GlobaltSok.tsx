import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { Button, Dialog, EXPERIMENTAL_Suggestion as Suggestion, type SuggestionItem } from '@digdir/designsystemet-react';
import { api } from '../api/client';
import type { BegrepDto, RettskildeSammendrag, TjenesteDto, VilkarDto } from '../api/types';

/** Øvre grense på antall KOMBINERTE treff mount'et som `<Suggestion.Option>` samtidig — samme
 * "søk-før-mount"-teknikk og grense som `useRettskildeSok` (docs/09 §10), her på tvers av fire
 * entitetstyper i stedet for én. */
const MAKS_TREFF = 50;

type SokType = 'rettskilde' | 'tjeneste' | 'begrep' | 'vilkar';
const TYPE_LABEL: Record<SokType, string> = {
  rettskilde: 'Rettskilde', tjeneste: 'Tjeneste', begrep: 'Begrep', vilkar: 'Vilkår',
};

interface SokTreff {
  type: SokType;
  id: string;
  label: string;
  meta: string;
  href: string;
}

/** Suggestion sin `value` må være én streng — id-rommene til de fire entitetstypene er separate
 * GUID-serier (kollisjon usannsynlig), men vi prefikser med typen likevel for å være eksplisitt
 * korrekt i stedet for å stole på det. */
function nokkel(t: Pick<SokTreff, 'type' | 'id'>): string {
  return `${t.type}:${t.id}`;
}

/**
 * Globalt søk på tvers av rettskilder/tjenester/begrep/vilkår (docs/30 §3.4/§4 punkt 7 —
 * saksbehandlertilpasningen) — en Ctrl/Cmd+K-triggered `Dialog` med samme `Suggestion`-baserte
 * "søk-før-mount"-teknikk som `RettskildeFlervalg`/`useRettskildeSok` (docs/09 §10): kun de første
 * `MAKS_TREFF` kombinerte treffene mountes som `<Suggestion.Option>`, uansett hvor mange kandidater
 * (5893 rettskilder + tjenester + begrep + vilkår) som finnes totalt.
 * <p>
 * Renderer BÅDE en synlig "Søk overalt"-knapp (for oppdagbarhet — et rent tastatursnarveis-only
 * søk ville vært usynlig for noen som ikke visste det fantes) OG lytter globalt etter Ctrl/Cmd+K.
 * Dataene (alle fire lister) hentes FØRST når dialogen åpnes for aller første gang i denne
 * sesjonen — ingen ekstra nettverkslast for en bruker som aldri åpner søket.
 * <p>
 * Vilkår har ingen egen detaljside — samme bevisste forenkling som resten av kodebasen («kun ett
 * vilkårstre finnes i dag») brukes her: lenken går til `/vilkarstre/{rotnodeId}?fokusVilkar={id}`.
 */
export function GlobaltSok() {
  const navigate = useNavigate();
  const [apen, setApen] = useState(false);
  const [sok, setSok] = useState('');
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[] | null>(null);
  const [tjenester, setTjenester] = useState<TjenesteDto[] | null>(null);
  const [begreper, setBegreper] = useState<BegrepDto[] | null>(null);
  const [vilkarListe, setVilkarListe] = useState<VilkarDto[] | null>(null);
  const [rotnodeId, setRotnodeId] = useState<string | undefined>(undefined);

  useEffect(() => {
    function handleKeydown(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setApen(true);
      }
    }
    window.addEventListener('keydown', handleKeydown);
    return () => window.removeEventListener('keydown', handleKeydown);
  }, []);

  useEffect(() => {
    if (!apen || rettskilder !== null) return;
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
    api.hentTjenester().then((t) => {
      setTjenester(t);
      setRotnodeId((forrige) => forrige ?? t.find((x) => x.rotnodeId)?.rotnodeId ?? undefined);
    }).catch(() => setTjenester([]));
    api.hentBegreper().then(setBegreper).catch(() => setBegreper([]));
    api.hentVilkarListe().then(setVilkarListe).catch(() => setVilkarListe([]));
  }, [apen, rettskilder]);

  const alleTreff = useMemo<SokTreff[]>(() => {
    const items: SokTreff[] = [];
    for (const r of rettskilder ?? []) {
      items.push({ type: 'rettskilde', id: r.id, label: r.kortnavn ?? r.tittel, meta: r.kildetype, href: `/rettskilder/${r.id}` });
    }
    for (const t of tjenester ?? []) {
      items.push({ type: 'tjeneste', id: t.id, label: t.tittel, meta: t.status, href: `/tjenester/${t.id}` });
    }
    for (const b of begreper ?? []) {
      items.push({ type: 'begrep', id: b.id, label: b.term, meta: b.status, href: `/begreper/${b.id}` });
    }
    for (const v of vilkarListe ?? []) {
      items.push({
        type: 'vilkar', id: v.id, label: v.tittel, meta: v.status,
        href: rotnodeId ? `/vilkarstre/${rotnodeId}?fokusVilkar=${v.id}` : '/vilkarstre',
      });
    }
    return items;
  }, [rettskilder, tjenester, begreper, vilkarListe, rotnodeId]);

  const treff = useMemo(() => {
    const s = sok.trim().toLowerCase();
    if (!s) return [];
    return alleTreff.filter((t) => t.label.toLowerCase().includes(s)).slice(0, MAKS_TREFF);
  }, [alleTreff, sok]);

  function velg(item: SuggestionItem | null) {
    lukk();
    if (!item) return;
    const funnet = alleTreff.find((t) => nokkel(t) === item.value);
    if (funnet) navigate(funnet.href);
  }

  function lukk() {
    setApen(false);
    setSok('');
  }

  return (
    <>
      <Button
        variant="tertiary" data-size="sm" onClick={() => setApen(true)}
        style={{ width: '100%', justifyContent: 'space-between', display: 'flex' }}
      >
        Søk overalt
        <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>Ctrl/⌘+K</span>
      </Button>

      <Dialog open={apen} onClose={lukk} closeButton="Lukk søk" style={{ maxWidth: '36rem', width: '100%' }}>
        <Dialog.Block>
          <Suggestion multiple={false} filter={false} selected={null} onSelectedChange={velg}>
            <Suggestion.Input
              placeholder="Søk i rettskilder, tjenester, begrep og vilkår …"
              onInput={(e) => setSok(e.currentTarget.value)}
            />
            <Suggestion.Clear onClick={() => setSok('')} />
            <Suggestion.List>
              <Suggestion.Empty>
                {sok.trim() ? 'Ingen treff.' : 'Skriv for å søke på tvers av rettskilder, tjenester, begrep og vilkår …'}
              </Suggestion.Empty>
              {treff.map((t) => (
                <Suggestion.Option key={nokkel(t)} value={nokkel(t)}>
                  {TYPE_LABEL[t.type]} · {t.label}
                </Suggestion.Option>
              ))}
            </Suggestion.List>
          </Suggestion>
        </Dialog.Block>
      </Dialog>
    </>
  );
}
