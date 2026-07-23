# Rådata — kilder til byggesteg 1 (Rettskildebibliotek)

Dette er **ikke** produksjonsdata og **ikke** AKN — det er rådata hentet fra Lovdata, lagt inn som konkret utgangspunkt for byggesteg 1 (`../../docs/06-veikart.md`). AKN-konverteringen er selve byggesteg-1-arbeidet og er ikke gjort her.

## Proveniens

| Felt | Verdi |
|---|---|
| Kilde | Lovdatas offisielle, gratis bulk-datasett: `https://api.lovdata.no/v1/publicData/get/gjeldende-lover.tar.bz2` og `.../gjeldende-sentrale-forskrifter.tar.bz2` |
| Lisens | NLOD 2.0 (Norsk lisens for offentlige data) — kildeangivelse: **Lovdata** |
| Hentet | 2026-07-23 (alkoholloven/-forskriften); 2026-07-24 (forvaltningsloven) |
| Filer | `nl/nl-19890602-027.xml` → `alkoholloven-LOV-1989-06-02-27.html`; `sf/sf-20050608-0538.xml` → `alkoholforskriften-FOR-2005-06-08-538.html`; `nl/nl-19670210-000.xml` → `forvaltningsloven-LOV-1967-02-10.html` (løpenummer "000" — loven har ikke noe reelt løpenummer, samme mønster som andre eldre lover) |
| Original koding | **cp1252** i Lovdatas bulk-datasett. Alkoholloven/-forskriften ble ved en feil i innhentingen 2026-07-23 dobbelt feilkodet (mojibake) og er siden rettet til korrekt UTF-8 (samme innhold). Forvaltningsloven ble hentet direkte fra bulk-datasettet 2026-07-24 og var allerede korrekt UTF-8 uten noen mellomsteg. |

`alkoholloven-LOV-1989-06-02-27.html` = "Lov om omsetning av alkoholholdig drikk m.v. (alkoholloven)", alle 11 kapitler. `alkoholforskriften-FOR-2005-06-08-538.html` = "Forskrift om omsetning av alkoholholdig drikk mv. (alkoholforskriften)". Se `06-veikart.md` byggesteg 1 for hvorfor **hele** loven/forskriften er hentet, ikke bare kapittel 4. `forvaltningsloven-LOV-1967-02-10.html` = "Lov om behandlingsmåten i forvaltningssaker (forvaltningsloven)", hele loven — nevnt i `06-veikart.md` §3.1.1 som kandidat "infrastrukturlov" å forhåndsimportere uansett testcase.

Den andre forskriften fra samme dato i bulk-arkivet (`sf-20050608-0539.xml`, "Forskrift om engrossalg og tilvirkning av alkoholholdig drikk mv.") er **ikke** tatt med — den gjelder engrossalg/tilvirkning, ikke skjenkebevilling, og er utenfor testcaset.

## Lokale forskrifter er ikke i bulk-datasettet

Undersøkt 2026-07-24 (se `../../src/README.md`): Lovdatas gratis bulk-datasett dekker kun **sentrale** (statlige) rettskilder. Lokale forskrifter (kunngjort i Norsk Lovtidend **Del II**, bekreftet av Johann) er ikke tilgjengelig der, og heller ikke i samme "XML-kompatible HTML"-format på selve nettsiden (`lovdata.no/dokument/LF/...` bruker en helt annen, inkompatibel HTML-struktur — `morTag_p`/`paragrafHeader` i stedet for `legalArticle`/`documentHeader`). Konsekvens: lokale forskrifter må kommunen selv levere til regel-IDE (fil/opplasting), og systemet trenger en egen parser for det formatet kommunen faktisk leverer — det er ikke bare en ny kilde-URL å peke det eksisterende Lovdata-importet mot.

**Idé til fremtidig scope (ikke besluttet, ikke bygget):** siden Lovdata selv ikke tilbyr noe API for lokale forskrifter, kan regel-IDE bli det de facto API-et for dette — kommunen leverer filen én gang, regel-IDE konverterer og gjør den tilgjengelig via `RegelIde.Api` (se `../../src/README.md`) på samme måte som sentrale rettskilder. Krever en egen parser for kommunens leveranseformat (ikke Lovdatas), og er ikke spec'et videre ennå.

## Format og hva det betyr for AKN-konverteringen

Filene er Lovdatas "XML-kompatible HTML" (bekreftet struktur, ikke antatt):

```html
<section class="section" data-name="kap1" id="kapittel-1" data-lovdata-URL="NL/lov/1989-06-02-27/KAPITTEL_1">
  <h2>Kapittel 1. Alminnelige bestemmelser.</h2>
  <article class="legalArticle" data-lovdata-URL="NL/lov/1989-06-02-27/§1-1" data-name="§1-1" id="kapittel-1-paragraf-1">
    <h3 class="legalArticleHeader"><span class="legalArticleValue">§ 1-1</span>. <span class="legalArticleTitle">Lovens formål.</span></h3>
    <article class="legalP" id="kapittel-1-paragraf-1-ledd-1">Reguleringen av innførsel og omsetning …</article>
  </article>
  ...
```

Strukturell mapping til AKN (for byggesteg 1s implementasjon):

| Lovdata-HTML | AKN |
|---|---|
| `<section class="section" data-name="kapN">` | `<chapter>` |
| `<article class="legalArticle" data-lovdata-URL="…/§X-Y">` | `<article>`, `eId` avledet direkte fra `data-lovdata-URL` |
| `<article class="legalP">` | `<paragraph>` (ledd) |
| `<li><article class="listArticle">` | bokstav-/nummerpunkt |
| `<a href="lov/…/§X">` inni løpetekst | intern kryssreferanse — bevar som lenke i AKN, ikke bare fjern taggen |
| `changesToParent` | endringshistorikk — mates inn i proveniensen (`03-domenemodell.md` §1.14), ikke i selve AKN-teksten |

`data-lovdata-URL` er praktisk talt en ferdig hierarkisk identifikator — regel-IDEs `eId` (`03-domenemodell.md` §1.1) kan avledes direkte derfra (f.eks. `NL/lov/1989-06-02-27/§4-3` → `par_4-3`), ikke konstrueres på nytt.

## Relevante paragrafer for testcaset (skjenkebevilling)

Identifisert ved gjennomgang av innholdsfortegnelsen — se `01-referansemodell.md` §5.5 for hvordan disse brukes i det låste vilkårstre-eksemplet:

- **§ 1-1** Lovens formål — tjenestebeskrivelsens grunnlag (`02-produktkrav.md` kap. 3.2)
- **§ 1-3, § 1-4** Definisjoner — begrepsgrunnlag (kap. 3.8)
- **§ 1-5** Aldersgrenser — aldersvilkåret
- **§ 1-7, § 1-7a** Bevilling for salg og skjenking; kommunens skjønnsutøvelse — grensen regel/skjønn
- **§ 1-7b** Krav til vandel — skjønnsgrunnlaget i vandelsvilkåret (`01-referansemodell.md` §6.1)
- **§ 1-7c** Styrer og stedfortreder
- **Kapittel 4** (§ 4-1 til § 4-7) Kommunale skjenkebevillinger — § 4-3 (vilkår), § 4-4 (tidsinnskrenkninger/skjenketid)

Resten av loven/forskriften er med for navigasjon og kryssreferanser, ikke fordi alt skal modelleres som vilkår i byggesteg 4.

## `referanser/` — ekte kommunal praksis, ikke Testkommunens egne rettskilder

Se [`referanser/README.md`](referanser/README.md). Inneholder Helsedirektoratets veileder i salgs- og skjenkekontroll (IS-2038) og ekte, vedtatte alkoholpolitiske retningslinjer fra Vennesla og Tønsberg kommuner — brukt som empirisk sammenligningsgrunnlag for byggesteg 2/4/6/7 (`06-veikart.md`, "Funn fra reelle virksomhetsdokumenter"), ikke som noe som importeres som Testkommunens egne rettskilder.
