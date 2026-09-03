/**
 * Klientside pretty-printer for AKN-XML (issue #130) — «vis akn-xml på 1 linje er meningsløst. vis som
 * strukturerte data.» Rotårsak: `AknXmlSkriver.cs` bruker ren strengsammenslåing uten
 * `XmlWriterSettings.Indent`, så `RettskildeDetalj.aknXml` er én ubrutt linje for ALLE allerede
 * lagrede rettskilder (ikke bare eldre rader) — en ren visningsfiks her, uten noen endring av lagret
 * data eller noen ny import/migrasjon, løser derfor problemet for hele korpuset umiddelbart (i
 * motsetning til f.eks. #126/#127/#131, som krever en resynk for eksisterende rader).
 *
 * Bevisst valgt fremfor å gjøre AknXmlSkriver selv innrykket (issue #130s alternativ (b)): (a) krever
 * INGEN endring av den lagrede `akn_xml`-strengen (som selve skriverklassen dokumenterer skal være
 * referensielt transparent for (metadata, noder) — innrykk ville vært en ufarlig, men unødvendig
 * risiko å legge til der), og (b) virker likt for BÅDE gamle og nye rader uten en resynk.
 *
 * `DOMParser` (ikke en håndskrevet strengbasert innrykker) — AKN-XML-en er allerede en fullverdig,
 * velformet XML-dokument (validert mot akomantoso30.xsd, se AknXmlSkriver sin klassekommentar), så en
 * ekte DOM-parse er både enklere og tryggere enn regex-basert linjeskift/innrykk.
 */
export function forsokFormaterXml(raaXml: string): string {
  try {
    const parser = new DOMParser();
    const dokument = parser.parseFromString(raaXml, 'application/xml');
    if (dokument.querySelector('parsererror')) return raaXml; // ugyldig XML — vis rått i stedet for å late som formatering lyktes.
    return formaterNode(dokument.documentElement, 0).trimStart();
  } catch {
    // «ingen gjettet fallback» (§3.3) i ånd, oversatt til klientsiden: en formateringsfeil skal aldri
    // skjule innholdet — vis den rå strengen (samme oppførsel som FØR denne fiksen) i stedet for å kaste.
    return raaXml;
  }
}

const INNRYKK = '  ';

function formaterNode(node: Element, dybde: number): string {
  const innrykk = INNRYKK.repeat(dybde);
  const attributter = Array.from(node.attributes)
    .map((a) => ` ${a.name}="${a.value}"`)
    .join('');

  const barn = Array.from(node.childNodes);
  const elementBarn = barn.filter((n): n is Element => n.nodeType === Node.ELEMENT_NODE);
  const tekstinnhold = barn
    .filter((n) => n.nodeType === Node.TEXT_NODE)
    .map((n) => n.textContent ?? '')
    .join('')
    .trim();

  if (barn.length === 0) {
    return `${innrykk}<${node.tagName}${attributter}/>\n`;
  }

  // Rent tekstinnhold (ingen element-barn, f.eks. <num>1</num>) — behold på ÉN linje, ikke tving et
  // linjeskift mellom åpne-/lukke-tag rundt et enkeltord/-setning (ville sett like unaturlig ut som
  // problemet dette fikser).
  if (elementBarn.length === 0) {
    return `${innrykk}<${node.tagName}${attributter}>${tekstinnhold}</${node.tagName}>\n`;
  }

  // Blandet innhold (mixed content — f.eks. <p>tekst<ref>...</ref>mer tekst</p>, subFlow-elementer som
  // <ref>/<authorialNote>) — AKNs egen tekstflyt skal IKKE brytes opp med kunstige linjeskift/innrykk
  // mellom tekst og inline-elementer, det ville endret den synlige teksten. Serialiseres derfor via
  // XMLSerializer for akkurat DETTE elementets INNHOLD (ikke re-parset/indentert rekursivt), på égn linje.
  if (tekstinnhold.length > 0) {
    const serialisert = new XMLSerializer();
    const innhold = barn.map((n) => serialisert.serializeToString(n)).join('');
    return `${innrykk}<${node.tagName}${attributter}>${innhold}</${node.tagName}>\n`;
  }

  const indreLinjer = elementBarn.map((barn) => formaterNode(barn, dybde + 1)).join('');
  return `${innrykk}<${node.tagName}${attributter}>\n${indreLinjer}${innrykk}</${node.tagName}>\n`;
}
