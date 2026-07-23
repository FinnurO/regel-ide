# Rådata — kilder til byggesteg 1 (Rettskildebibliotek)

Dette er **ikke** produksjonsdata og **ikke** AKN — det er rådata hentet fra Lovdata, lagt inn som konkret utgangspunkt for byggesteg 1 (`../../docs/06-veikart.md`). AKN-konverteringen er selve byggesteg-1-arbeidet og er ikke gjort her.

## Proveniens

| Felt | Verdi |
|---|---|
| Kilde | Lovdatas offisielle, gratis bulk-datasett: `https://api.lovdata.no/v1/publicData/get/gjeldende-lover.tar.bz2` og `.../gjeldende-sentrale-forskrifter.tar.bz2` |
| Lisens | NLOD 2.0 (Norsk lisens for offentlige data) — kildeangivelse: **Lovdata** |
| Hentet | 2026-07-23 |
| Filer | `nl/nl-19890602-027.xml` → `alkoholloven-LOV-1989-06-02-27.html`; `sf/sf-20050608-0538.xml` → `alkoholforskriften-FOR-2005-06-08-538.html` |
| Original koding | **cp1252** (ikke UTF-8/latin-1 — filene i dette repoet er konvertert til UTF-8) |

`alkoholloven-LOV-1989-06-02-27.html` = "Lov om omsetning av alkoholholdig drikk m.v. (alkoholloven)", alle 11 kapitler. `alkoholforskriften-FOR-2005-06-08-538.html` = "Forskrift om omsetning av alkoholholdig drikk mv. (alkoholforskriften)". Se `06-veikart.md` byggesteg 1 for hvorfor **hele** loven/forskriften er hentet, ikke bare kapittel 4.

Den andre forskriften fra samme dato i bulk-arkivet (`sf-20050608-0539.xml`, "Forskrift om engrossalg og tilvirkning av alkoholholdig drikk mv.") er **ikke** tatt med — den gjelder engrossalg/tilvirkning, ikke skjenkebevilling, og er utenfor testcaset.

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
