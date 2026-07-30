/**
 * MinimalEditor
 * ------------------------------------------------------------------
 * Bevisst minimal rik-tekst-editor for håndbok-kommentarseksjoner
 * (docs/03-domenemodell.md §1.1.1 "Redigeringsflate"). Tillatt markup:
 * avsnitt, overskrift (h3), fet, kursiv, understreking, lenke og
 * INTERN REFERANSE. Ingen tabeller, bilder, farger eller fonter.
 *
 * Interne referanser lagres som typede pekere, ikke URL-er, slik at en
 * fremtidig påvirkningsanalyse kan følge dem:
 *   <a data-ref-kind="rettskilde" data-ref-id="...eId...">§ 1-7c</a>
 * (attributtnavn valgt for å matche KommentarTekstSanering.cs sin
 * allow-list i RegelIde.Data nøyaktig — se den for den autoritative
 * server-side saneringen; klienten er ikke tiltrodd uansett.)
 *
 * Opprinnelig utkast fra Claude Design (2026-07-26); tilpasset her:
 *   - data-kind/data-ref (klientens opprinnelige navn) -> data-ref-kind/
 *     data-ref-id, for å matche den allerede committede backend-spesifikasjonen.
 *   - RefKind er en fri streng, ikke en lukket union — kun 'rettskilde' har
 *     reelle kandidater i byggesteg 1 (Begrep/Vilkår finnes først i byggesteg 2/4).
 *
 * DESIGNSYSTEMET-KOMPONENTER SOM BRUKES
 *   - ToggleGroup → blokktype (Avsnitt / Overskrift)
 *   - Button      → B / I / U og panelhandlinger
 *   - Textfield   → URL-feltet i lenkepanelet
 *   - Tag         → «ingen kandidater»-tomtilstand
 * Selve redigeringsflaten er contentEditable — finnes ikke i DS.
 *
 * TOKENS: kun --ds-*.
 */
import { useCallback, useEffect, useRef, useState } from 'react';
import { ToggleGroup, Button, Textfield, Tag } from '@digdir/designsystemet-react';

/* ------------------------------ typer ------------------------------ */

export type RefKind = string;

export interface RefOption {
  kind: RefKind;
  /** Maskin-id — det som lagres, aldri visningsetiketten. F.eks. en eId. */
  id: string;
  label: string;
}

export interface InternReferanse {
  kind: RefKind;
  ref: string;
}

export interface MinimalEditorProps {
  /** HTML-innhold (kontrollert ved montering, deretter ukontrollert av ytelseshensyn). */
  value: string;
  onChange: (html: string, referanser: InternReferanse[]) => void;
  /** Kandidater for «sett inn referanse». */
  referanser?: RefOption[];
  placeholder?: string;
  readOnly?: boolean;
  /** Overstyr tillatt markup. Default: p, h3, b, i, u, a. */
  allow?: string[];
}

const DEFAULT_ALLOW = ['p', 'h3', 'b', 'i', 'u', 'a'];

/** Skjemaer en href får ha. Alt annet — særlig javascript: og data: — fjernes. */
const TILLATTE_URL_SKJEMA = ['http:', 'https:', 'mailto:'];

/* --------------------------- hjelpere --------------------------- */

/**
 * Er dette en href vi tør sette på en lenke? Relative URL-er er trygge (de arver
 * sidens eget skjema); absolutte må ha et skjema fra allow-listen. Speiler
 * AllowedSchemes i KommentarTekstSanering.cs, som fortsatt er den autoritative
 * grensen — denne finnes for at en javascript:-lenke ikke engang skal kunne
 * bli stående i redigeringsflaten mens man jobber.
 */
export function erTryggUrl(verdi: string): boolean {
  try {
    return TILLATTE_URL_SKJEMA.includes(new URL(verdi, window.location.origin).protocol);
  } catch {
    return false;
  }
}

/** Sanering til tillatt markup (klientside — kun for UX, ikke sikkerhetsgrense). Kjøres ved lagring. */
export function saniter(html: string, allow: string[] = DEFAULT_ALLOW): string {
  const doc = new DOMParser().parseFromString(`<div>${html}</div>`, 'text/html');
  const root = doc.body.firstElementChild!;
  const walk = (el: Element) => {
    [...el.children].forEach((child) => {
      walk(child);
      const tag = child.tagName.toLowerCase();
      if (!allow.includes(tag)) {
        child.replaceWith(...Array.from(child.childNodes)); // behold tekst, fjern tagg
        return;
      }
      [...child.attributes].forEach((a) => {
        const behold = tag === 'a' && ['href', 'data-ref-kind', 'data-ref-id'].includes(a.name);
        if (!behold || (a.name === 'href' && !erTryggUrl(a.value))) child.removeAttribute(a.name);
      });
    });
  };
  walk(root);
  return root.innerHTML;
}

/**
 * Bygger noden som settes inn i flaten for en typet referanse, som DOM — aldri som
 * en HTML-streng. `label` er lagret, brukerstyrt data (kortnavn/tittel på en
 * rettskilde), og settes derfor med textContent: markup i den skal bli synlig tekst,
 * ikke tolkes. Etterfølges av et mellomrom slik at markøren får et sted å stå.
 */
export function byggReferanseNode(o: RefOption): DocumentFragment {
  const lenke = document.createElement('a');
  lenke.setAttribute('data-ref-kind', o.kind);
  lenke.setAttribute('data-ref-id', o.id);
  lenke.textContent = o.label;
  const bit = document.createDocumentFragment();
  bit.append(lenke, document.createTextNode(' ')); // nbsp, som før — vanlig mellomrom kollapser
  return bit;
}

/** Leser ut alle typede pekere fra innholdet. */
export function lesReferanser(html: string): InternReferanse[] {
  const doc = new DOMParser().parseFromString(`<div>${html}</div>`, 'text/html');
  return [...doc.querySelectorAll('a[data-ref-kind]')]
    .map((a) => ({ kind: a.getAttribute('data-ref-kind') ?? '', ref: a.getAttribute('data-ref-id') ?? '' }))
    .filter((r) => r.kind && r.ref);
}

/* --------------------------- komponent --------------------------- */

export function MinimalEditor({
  value,
  onChange,
  referanser = [],
  placeholder = 'Skriv kommentaren …',
  readOnly = false,
  allow = DEFAULT_ALLOW,
}: MinimalEditorProps) {
  const ref = useRef<HTMLDivElement>(null);
  const [fmt, setFmt] = useState({ bold: false, italic: false, underline: false, block: 'p' });
  const [panel, setPanel] = useState<null | 'lenke' | 'referanse'>(null);
  const [url, setUrl] = useState('');
  const [urlFeil, setUrlFeil] = useState<string | null>(null);
  /** Siste markørposisjon inne i flaten. Å klikke en knapp i panelet flytter fokus ut,
   *  så vi må ha tatt vare på hvor innsettingen skal skje. */
  const sisteRange = useRef<Range | null>(null);

  // Seed én gang — React skal ikke eie contentEditable-barna.
  useEffect(() => {
    if (ref.current && !ref.current.innerHTML) ref.current.innerHTML = value;
  }, [value]);

  const emit = useCallback(() => {
    if (!ref.current) return;
    const html = saniter(ref.current.innerHTML, allow);
    onChange(html, lesReferanser(html));
  }, [onChange, allow]);

  const huskUtvalg = useCallback(() => {
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return;
    const r = sel.getRangeAt(0);
    if (ref.current?.contains(r.commonAncestorContainer)) sisteRange.current = r.cloneRange();
  }, []);

  const sync = useCallback(() => {
    huskUtvalg();
    try {
      let block = 'p';
      const b = document.queryCommandValue('formatBlock');
      if (typeof b === 'string' && b.toLowerCase().includes('h3')) block = 'h3';
      setFmt({
        bold: document.queryCommandState('bold'),
        italic: document.queryCommandState('italic'),
        underline: document.queryCommandState('underline'),
        block,
      });
    } catch {
      /* noop */
    }
  }, [huskUtvalg]);

  const exec = useCallback(
    (cmd: string, val?: string) => {
      ref.current?.focus();
      try {
        document.execCommand(cmd, false, val);
      } catch {
        /* noop */
      }
      sync();
      emit();
    },
    [sync, emit],
  );

  /**
   * Setter inn en typet referanse som DOM-noder, ikke som en HTML-streng.
   *
   * Tidligere ble `<a ...>${o.label}</a>` bygget som tekst og sendt gjennom
   * execCommand('insertHTML'). o.label er lagret, brukerstyrt data (kortnavn/tittel
   * på en rettskilde), så markup i den ble tolket som markup ved innsetting og kjørte
   * umiddelbart i redaktørens nettleser — server-saneringen kjører først ved lagring
   * og rakk aldri å stoppe det. textContent gjør at label alltid blir tekst.
   */
  const settInnReferanse = useCallback(
    (o: RefOption) => {
      const vert = ref.current;
      if (!vert) return;
      vert.focus();

      let range = sisteRange.current;
      if (!range || !vert.contains(range.commonAncestorContainer)) {
        range = document.createRange();
        range.selectNodeContents(vert);
        range.collapse(false); // ingen kjent markør — legg bakerst
      }
      range.deleteContents();

      const bit = byggReferanseNode(o);
      const mellomrom = bit.lastChild!;
      range.insertNode(bit);

      range.setStartAfter(mellomrom);
      range.collapse(true);
      const sel = window.getSelection();
      sel?.removeAllRanges();
      sel?.addRange(range);
      sisteRange.current = range.cloneRange();

      setPanel(null);
      emit();
    },
    [emit],
  );

  const bar: React.CSSProperties = {
    display: 'flex',
    alignItems: 'center',
    gap: 'var(--ds-size-1)',
    flexWrap: 'wrap',
    padding: 'var(--ds-size-2) var(--ds-size-3)',
    borderBottom: '1px solid var(--ds-color-neutral-border-subtle)',
    background: 'var(--ds-color-neutral-surface-tinted)',
  };

  return (
    <div
      style={{
        border: '1px solid var(--ds-color-neutral-border-subtle)',
        borderRadius: 'var(--ds-border-radius-lg)',
        overflow: 'hidden',
        background: 'var(--ds-color-neutral-surface-default)',
      }}
    >
      {!readOnly && (
        <div style={bar} role="toolbar" aria-label="Formatering">
          <ToggleGroup value={fmt.block} onChange={(v) => exec('formatBlock', v as string)} data-size="sm">
            <ToggleGroup.Item value="p">Avsnitt</ToggleGroup.Item>
            <ToggleGroup.Item value="h3">Overskrift</ToggleGroup.Item>
          </ToggleGroup>

          <span
            aria-hidden
            style={{ width: 1, height: 22, background: 'var(--ds-color-neutral-border-default)', margin: '0 var(--ds-size-1)' }}
          />

          <Button
            variant={fmt.bold ? 'primary' : 'tertiary'}
            data-size="sm"
            aria-pressed={fmt.bold}
            onClick={() => exec('bold')}
            style={{ fontWeight: 700, minWidth: 32 }}
          >
            B
          </Button>
          <Button
            variant={fmt.italic ? 'primary' : 'tertiary'}
            data-size="sm"
            aria-pressed={fmt.italic}
            onClick={() => exec('italic')}
            style={{ fontStyle: 'italic', minWidth: 32 }}
          >
            I
          </Button>
          <Button
            variant={fmt.underline ? 'primary' : 'tertiary'}
            data-size="sm"
            aria-pressed={fmt.underline}
            onClick={() => exec('underline')}
            style={{ textDecoration: 'underline', minWidth: 32 }}
          >
            U
          </Button>

          <span
            aria-hidden
            style={{ width: 1, height: 22, background: 'var(--ds-color-neutral-border-default)', margin: '0 var(--ds-size-1)' }}
          />

          <Button variant={panel === 'lenke' ? 'primary' : 'tertiary'} data-size="sm" onClick={() => setPanel(panel === 'lenke' ? null : 'lenke')}>
            Lenke
          </Button>
          <Button
            variant={panel === 'referanse' ? 'primary' : 'tertiary'}
            data-size="sm"
            onClick={() => setPanel(panel === 'referanse' ? null : 'referanse')}
          >
            Referanse
          </Button>
        </div>
      )}

      {panel === 'lenke' && (
        <div
          style={{
            display: 'flex',
            alignItems: 'flex-end',
            gap: 'var(--ds-size-2)',
            padding: 'var(--ds-size-3)',
            background: 'var(--ds-color-accent-surface-tinted)',
            borderBottom: '1px solid var(--ds-color-accent-border-subtle)',
          }}
        >
          <Textfield
            label="URL"
            data-size="sm"
            value={url}
            error={urlFeil}
            onChange={(e) => {
              setUrl(e.target.value);
              setUrlFeil(null);
            }}
            placeholder="https://lovdata.no/…"
            style={{ flex: 1 }}
          />
          <Button
            data-size="sm"
            onClick={() => {
              if (!url) return;
              // Uten denne kan man lage en javascript:-lenke i sitt eget dokument. Den
              // strippes riktignok av saniter() ved lagring, men skal ikke kunne oppstå.
              if (!erTryggUrl(url)) {
                setUrlFeil('Bare http-, https- og mailto-adresser er tillatt.');
                return;
              }
              exec('createLink', url);
              setUrl('');
              setUrlFeil(null);
              setPanel(null);
            }}
          >
            Sett inn
          </Button>
          <Button
            variant="tertiary"
            data-size="sm"
            onClick={() => {
              setUrl('');
              setUrlFeil(null);
              setPanel(null);
            }}
          >
            Avbryt
          </Button>
        </div>
      )}

      {panel === 'referanse' && (
        <div
          style={{
            padding: 'var(--ds-size-3)',
            background: 'var(--ds-color-brand1-surface-tinted)',
            borderBottom: '1px solid var(--ds-color-brand1-border-subtle)',
          }}
        >
          <div style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: 'var(--ds-size-2)' }}>
            Sett inn referanse — lagres som typet peker, ikke som URL:
          </div>
          <div style={{ display: 'flex', gap: 'var(--ds-size-2)', flexWrap: 'wrap' }}>
            {referanser.map((o) => (
              <Button key={`${o.kind}:${o.id}`} variant="secondary" data-size="sm" onClick={() => settInnReferanse(o)}>
                {o.label}
              </Button>
            ))}
            {referanser.length === 0 && <Tag data-color="neutral" data-size="sm">Ingen kandidater</Tag>}
          </div>
        </div>
      )}

      <div
        ref={ref}
        contentEditable={!readOnly}
        suppressContentEditableWarning
        role="textbox"
        aria-multiline="true"
        aria-label="Kommentartekst"
        data-placeholder={placeholder}
        onKeyUp={() => {
          sync();
          emit();
        }}
        onMouseUp={sync}
        onBlur={() => {
          huskUtvalg();
          emit();
        }}
        style={{
          minHeight: 300,
          padding: 'var(--ds-size-5) var(--ds-size-6)',
          fontSize: 'var(--ds-font-size-3)',
          lineHeight: 'var(--ds-line-height-lg)',
          outlineOffset: 3,
        }}
      />
    </div>
  );
}

/* ------------------------------------------------------------------
 * MERK for produksjon:
 *  - `saniter()` her er kun for UX (unngå at åpenbart feil markup blir
 *    værende i DOM-en) — KommentarTekstSanering.cs i RegelIde.Data er
 *    den autoritative saneringen, kjørt server-side ved hver lagring.
 *  - `document.execCommand` er deprecated men fungerer universelt for
 *    dette begrensede settet. Trengs mer (lister, angre-stack), bytt
 *    til en editor-kjerne (Lexical/TipTap) og behold samme props-API.
 *  - Plassholder krever én CSS-regel som ikke kan være inline:
 *      [data-placeholder]:empty::before { content: attr(data-placeholder);
 *        color: var(--ds-color-neutral-text-subtle); }
 * ------------------------------------------------------------------ */
