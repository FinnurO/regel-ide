/**
 * Klientside pretty-printer for rå Lovdata-HTML («Vis kilde», issue #131) — samme "vis som strukturerte
 * data i stedet for én ubrutt blob"-behov som formaterXml.ts allerede løser for AKN-XML (issue #130),
 * bare for RettskildeEntitet.Innhold (Lovdata-originalen, ekte HTML) i stedet for den lagrede akn_xml-
 * strengen. Uten dette viser "Vis kilde" (RettskildeDetalj.tsx) rå, uformatert markup i én lang linje —
 * nøyaktig samme problem #130 allerede fikset for AKN-XML-fanen, bare ufikset her.
 *
 * `DOMParser(..., 'text/html')` (IKKE 'application/xml' som formaterXml.ts bruker) — ekte Lovdata-HTML
 * er ikke nødvendigvis velformet XML (uparede `<br>`/`<img>`, HTML-entiteter som `&nbsp;`), så en streng
 * XML-parse ville feilet på gyldig HTML. HTML-modus er alltid lenient og feiler aldri (browserens egen
 * HTML5-parser reparerer i stedet for å kaste), så — i motsetning til formaterXml.ts — er det ikke
 * behov for en egen "ugyldig markup"-sjekk her.
 */
export function forsokFormaterHtml(raaHtml: string): string {
  try {
    const parser = new DOMParser();
    const dokument = parser.parseFromString(raaHtml, 'text/html');
    return formaterNode(dokument.body, 0).trimStart();
  } catch {
    // Samme "ingen gjettet fallback"-holdning som formaterXml.ts: en formateringsfeil skal aldri skjule
    // innholdet — vis den rå strengen (samme oppførsel som FØR denne fiksen) i stedet for å kaste.
    return raaHtml;
  }
}

const INNRYKK = '  ';

// HTML-elementer uten lukke-tag — samme liste som HTML5-spesifikasjonens "void elements". Uten dette
// ville f.eks. et enslig <br> blitt tolket som å "åpne" et element uten barn og aldri lukkes riktig i
// den formaterte visningen.
const TOMME_ELEMENTER = new Set([
  'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'param', 'source', 'track', 'wbr',
]);

function formaterNode(node: Element, dybde: number): string {
  const innrykk = INNRYKK.repeat(dybde);
  const tag = node.tagName.toLowerCase();
  const attributter = Array.from(node.attributes)
    .map((a) => ` ${a.name}="${a.value}"`)
    .join('');

  if (TOMME_ELEMENTER.has(tag)) {
    return `${innrykk}<${tag}${attributter}>\n`;
  }

  const barn = Array.from(node.childNodes);
  const elementBarn = barn.filter((n): n is Element => n.nodeType === Node.ELEMENT_NODE);
  const tekstinnhold = barn
    .filter((n) => n.nodeType === Node.TEXT_NODE)
    .map((n) => n.textContent ?? '')
    .join('')
    .trim();

  if (barn.length === 0) {
    return `${innrykk}<${tag}${attributter}></${tag}>\n`;
  }

  // Rent tekstinnhold (ingen element-barn, f.eks. <p>Ren tekst</p>) — behold på ÉN linje, samme
  // begrunnelse som formaterXml.ts.
  if (elementBarn.length === 0) {
    return `${innrykk}<${tag}${attributter}>${tekstinnhold}</${tag}>\n`;
  }

  // Blandet innhold (tekst + inline-elementer om hverandre, f.eks. <p>tekst<a>lenke</a>mer tekst</p>) —
  // ikke bryt opp tekstflyten med kunstige linjeskift/innrykk mellom tekst og inline-elementer.
  if (tekstinnhold.length > 0) {
    const serialisert = new XMLSerializer();
    const innhold = barn.map((n) => serialisert.serializeToString(n)).join('');
    return `${innrykk}<${tag}${attributter}>${innhold}</${tag}>\n`;
  }

  const indreLinjer = elementBarn.map((b) => formaterNode(b, dybde + 1)).join('');
  return `${innrykk}<${tag}${attributter}>\n${indreLinjer}${innrykk}</${tag}>\n`;
}
