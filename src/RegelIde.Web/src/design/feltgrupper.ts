/**
 * [Ny, #440, Johanns avgjørelse 2026-11-05, docs/09 §51] Regelen «radiogrupper står i `Fieldset`, ikke i `Field`» som ren funksjon, så
 * den kan testes uten rendring (docs/09 §17).
 *
 * Hvorfor: `Radio` og `Checkbox` i `@digdir/designsystemet-react` er selv en `Field` (en `<ds-field>` med input og etikett). Står flere av
 * dem inni en ytre `Field`, kobler den ytre `<ds-field>` alle etikettene til den første knappen, og et klikk på en etikett velger den
 * første. Funnet i kaldsjekken av aktørkøen 2026-11-05 (#453): et klikk på «Klasse» valgte «Aktør».
 */

/** Én `Field`-blokk som inneholder knapper den ikke skal inneholde. */
export interface FeltgruppeBrudd {
  /** Linjen `<Field` står på (1-basert). */
  linje: number;
  radio: number;
  checkbox: number;
}

/**
 * Finner `<Field …>…</Field>`-blokker i JSX-kilden som inneholder `<Radio` eller `<Checkbox`. Blokkene pares som parenteser, så en
 * `Field` inni en `Fieldset` eller en annen `Field` telles for seg. Tekstbasert, ikke en parser: `<Field` i en kommentar eller streng
 * telles også, og det er bedre å få et falskt brudd å se på enn å overse et ekte.
 */
export function radiogrupperIField(kilde: string): FeltgruppeBrudd[] {
  const brudd: FeltgruppeBrudd[] = [];
  const apne: number[] = [];
  const re = /<Field\b|<\/Field>/g;
  for (let m = re.exec(kilde); m; m = re.exec(kilde)) {
    if (m[0] === '<Field') {
      apne.push(m.index);
      continue;
    }
    const start = apne.pop();
    if (start === undefined) continue;
    const inni = kilde.slice(start, m.index);
    const radio = (inni.match(/<Radio\b/g) ?? []).length;
    const checkbox = (inni.match(/<Checkbox\b/g) ?? []).length;
    if (radio + checkbox > 0) brudd.push({ linje: kilde.slice(0, start).split('\n').length, radio, checkbox });
  }
  return brudd.sort((a, b) => a.linje - b.linje);
}
