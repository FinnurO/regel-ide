# Fasit-format: strukturelle utsagn om forvaltningen i en rettskilde

Formål: en NØYTRAL annotasjon av hva rettskilden faktisk sier om forvaltningens STRUKTUR — hvem som
finnes, hvordan de er organisert, hvilke relasjoner og hvilken myndighet de har, og hvilke områder de
virker i. Fasiten skal brukes til å MÅLE (1) om en datamodell kan uttrykke utsagnene, og (2) hvor godt en
automatisk konvertering (mønstre + KI) finner dem. Den skal derfor være modell-uavhengig: annoter det
teksten sier, ikke hvordan regel-ide i dag lagrer det.

Ikke annoter: plikter/rettigheter for private, saksbehandlingsregler, frister, materielle vilkår
(«normativt lag», kommer senere). MEN: kompetanse («X kan gi forskrift om …», «X treffer vedtak i saker
etter …», «klage over vedtak etter § 5 går til Y», «X fører tilsyn med at …») ER struktur og SKAL
annoteres — det sier hvem som har hvilken myndighet.

## Prinsipper

- **Ingen gjetting.** Sitatet skal være en eksakt delstreng av nodens tekst (kopier, ikke parafraser).
  Kan en referent ikke avgjøres fra teksten, sett `referent: null` og angi hvordan den MÅTTE løses
  (`oppløsning`).
- **Én rad per utsagn** (en setning kan gi flere utsagn).
- **Usikkerhet er data**: bruk `sikkerhet: "lav"` + `kommentar` heller enn å utelate.
- Passer ingen type: bruk `"annet:<kort_navn>"` og forklar i `kommentar`. Det er nettopp disse som
  avslører hull i modellen — de er verdifulle, ikke feil.

## Fil

Én JSON-fil per hovedrettskilde (inkludert ledsagende forskrifter), UTF-8:

{
  "rettskilde": "<tittel>",
  "eli": "<eli for hovedkilden>",
  "ledsagende": [{"tittel": "...", "eli": "..."}],
  "annotert_av": "KI-agent (Claude), <dato> — UTKAST, ikke menneskelig verifisert",
  "noder_lest": <antall>,
  "aktorer": [ ... ],
  "utsagn": [ ... ]
}

### `aktorer` — ett innslag per DISTINKT omtale-form i kilden (ikke per forekomst)

{
  "id": "a1",
  "tekstform": "Statsforvalteren",            // slik den står (første forekomst)
  "varianter": ["statsforvalteren", "statsforvaltaren"],
  "eid_eksempler": ["kapittel-2/paragraf-2-1/ledd-1", "..."],   // inntil 5
  "antall_forekomster": 12,                  // ca., i denne kilden
  "entitetstype": "rettssubjekt" | "organ" | "organisatorisk_enhet" | "rolle" | "person" | "omrade" | "klasse" | "annet:<x>",
  "navngitt": true | false,                  // true = peker på én bestemt (Sametinget, NVE); false = generisk (kommunen, statsforvalteren, departementet)
  "referent": "<konkret navn>" | null,       // kun hvis teksten ALENE avgjør det
  "oppløsning": "tekstlig" | "lovens_departement" | "foregaende_ledd" | "omrade" | "saksforhold" | "forskrift_utenfor" | "ukjent",
  "distributiv": true | false | null,        // gjelder regelen hvert medlem for seg («kommunen skal …» = true)
  "kommentar": "..."
}

Veiledning `entitetstype`:
- rettssubjekt: Staten, en kommune (som juridisk person), fylkeskommune, RHF/HF, stiftelse, AS.
- organ: Stortinget, Kongen (i statsråd), regjeringen, departement, direktorat/tilsyn (organ for staten),
  kommunestyre, styre, nemnd, domstol.
- organisatorisk_enhet: intern enhet uten selvstendig myndighet (avdeling, sekretariat som enhet).
- rolle: en funksjon som innehas av en aktør i en sammenheng (koordinator, kommuneoverlege,
  behandlingsansvarlig, «forvaltningsmyndigheten»).
- klasse: en kategori av subjekter («kommuner i forvaltningsområdet», «språkutviklingskommuner»).
- omrade: geografisk/jurisdiksjonelt område (domssogn, lagdømme, forvaltningsområdet, helseregion, fylke).
Merk dobbeltnatur eksplisitt i kommentar (f.eks. «kommune» både rettssubjekt og område).

### `utsagn` — ett innslag per strukturelt utsagn

{
  "id": "u1",
  "eid": "<node-eid>",
  "sitat": "<eksakt delstreng, maks ~30 ord>",
  "kategori": "relasjon" | "kompetanse" | "medlemskap" | "sammensetning_omrade" | "ansvarsomrade" | "konstituerende" | "organsammensetning" | "annet:<x>",
  "type": "<se lister under>",
  "fra": "a1",                // aktør-id (eller null hvis ikke i teksten)
  "til": "a2",                // aktør-id ELLER null. [ENDRET, #341] For kompetanse: MOTPARTEN («A har klagekompetanse
                              // overfor B»), null når kompetansen ikke har en motpart i teksten. til = fra på
                              // normgivning = selvregulering (avledet, ikke en egen type).
  "objekt": "<for kompetanse: bestemmelse/sakstype/regelverk, f.eks. 'vedtak etter § 3-1', 'forskrift om …'>",
  "normform": "forskrift" | "reglement" | "arbeidsordning" | "vedtekter" | "instruks",  // [Ny, #341] KUN på
                              // normgivningskompetanse; utelatt/null = ikke angitt
  "undertype": "valg" | "ansettelse" | "utpeking" | "oppnevning" | "anke",  // [Ny, #352] HVORDAN kompetansen utøves:
                              // KUN på oppnevningskompetanse (valg/ansettelse/utpeking/oppnevning — verbet «velger»,
                              // «ansetter», «utpeker», «oppnevner») og overprovingskompetanse (anke). Utelatt/null = ikke angitt.
                              // Verbene og ordstammene står i kompetanseleksikon.json («undertyper»)
  "grunnlag": "offentligrettslig" | "privatrettslig",  // [Ny, #341] KUN på kompetanse; privatrettslig = eierskap/
                              // selskapsrett. Utelatt/null = ikke angitt (settes av et menneske, aldri utledet)
  "delegerbar": true | false, // [Ny, #341/#335] KUN på kompetanse: «Kongen …» = true, «Kongen i statsråd …» og
                              // «X selv» = false — avgjort på sitatet. Utelatt/null = ikke angitt
  "polaritet": "positiv" | "negativ",           // «kan ikke instruere» = negativ
  "avgrensning": "<paragraf/sakstype/vilkår som begrenser utsagnet, ellers null>",
  "betinget": true | false,
  "kilde_utenfor_korpus": true | false,         // utsagnet viser til noe som fastsettes utenfor teksten (kgl.res., vedtekter, «Kongen bestemmer»)
  "sikkerhet": "hoy" | "middels" | "lav",
  "kommentar": "...",
  "oppdagelseskilde": "monster:<id>"          // [Ny, #307, 2026-10-07] KUN i maskinell konvertering: hvilket mønster/
                                              // hvilken modell som fant utsagnet («monster:forskrift-gi», senere «ki:<modell>»).
                                              // Fasiten (manuell) har ikke feltet. Blir OppdagelsesKilde ved lagring (#313).
  "verifisert_av": "<hvem, hvorfor, dato>"    // [Ny, #312, 2026-10-08] Valgfritt, på utsagn OG aktører: raden er rettet/
                                              // verifisert av et menneske (første bruk: Johanns systemiske rettelse av
                                              // domstollovens inndelingsdel via fasitkontrollen #309). Konverteringen setter det aldri.
}

**[ENDRET, issue #341, Johanns beslutninger 2026-10-08]** Grensen mellom relasjon og kompetanse:
- **Kompetanse** er myndighet: «A har kompetanse av typen X, eventuelt OVERFOR B (`til`), når det gjelder Y
  (`objekt`/`avgrensning`)». Klageinstans, instruksjon, omgjøring, oppnevning, tilsyn med en aktør, avsetting,
  sanksjon, samtykke, overprøving og forelegging er kompetanse med motpart — ikke relasjon.
- **Relasjon** er struktur UTEN myndighet: eierskap, ledelse, sekretariat, rapportering, etterfølger, representasjon —
  og den GJENNOMFØRTE delegeringen (`har_delegert_til`, fra et delegeringsvedtak; unntakene i vedtaket er egne rader
  med negativ polaritet = avgrensning av delegeringen, ikke negativ kompetanse). Kompetansen til å delegere («X kan
  delegere til Y») er `delegeringskompetanse`.
- [ENDRET, #352] `administrativt_underordnet`, `radgir`, og (under konstituerende) `oppretter` og `avvikler` er avgjort
  som struktur/hendelse, ikke myndighet (Johanns beslutning 4); kompetansen til å opprette eller avvikle er
  `opprettingskompetanse`/`avviklingskompetanse`. `bistar`, `samarbeider_med` og `del_av` står fortsatt under relasjon
  uten å være avgjort.
- Kompetansetypene har en **familie** (struktur, oppnevning, styring, normgivning, kontroll, klage_overproving, vedtak,
  sanksjon) og en **fvl-kategori**; begge er egenskaper ved TYPEN og står i `Strukturkanter.Kompetansetyper` i koden,
  ikke i fasiten. `beslutningskompetanse` står over alle familiene og brukes når teksten bare sier
  «beslutningsmyndighet». `vedtakskompetanse` betyr enkeltvedtak; forskrift er `normgivningskompetanse` med normform
  `forskrift`.
- Konverteringen av fasiten fra de gamle typene er deterministisk: `konvertering-341-kompetanse.py`.

**[ENDRET, issue #352, Johanns beslutninger 2026-10-08]** Restene etter #341:
- Familien `personell` heter **oppnevning**: «En aktør gis myndighet til å bestemme hvem som skal inneha en rolle, et
  verv eller en funksjon.» `velger` (relasjon), `utpekingskompetanse` og `ansettelseskompetanse` er
  `oppnevningskompetanse` med motpart og `undertype` (`valg`, `utpeking`, `ansettelse`; `oppnevning` når teksten sier
  «oppnevner»). `avsettingskompetanse` står i familien oppnevning som motsatsen (hovedøktens tolkning, Johann bekrefter).
- `ankeinstans_for` er `overprovingskompetanse` med motpart og `undertype` `anke` (familien klage og overprøving).
- `foreleggingskompetanse` er i familien kontroll. «… skal/må godkjennes av X» / «Godkjenning … gis av X» er
  `godkjenningskompetanse` (styring), ikke `vedtakskompetanse`.
- Konverteringen er deterministisk: `konvertering-352-oppnevning.py` (fasit 1797 → 1797 utsagn, KI-utdata 1043 → 1043).

Typer per kategori (bruk disse når de passer, ellers "annet:<x>"):
- relasjon (aktør→aktør): `eies_av`, `ledes_av`, `sekretariat_for`, `rapporterer_til`, `etterfolger`,
  `representerer`, `har_delegert_til` (gjennomført delegering, når BÅDE fra og til er gitt),
  `administrativt_underordnet`, `bistar`, `samarbeider_med`, `radgir`, `del_av`.
- kompetanse (aktør→motpart/bestemmelse/sakstype): `beslutningskompetanse`, `opprettingskompetanse`,
  `avviklingskompetanse`, `organisasjonskompetanse`, `oppnevningskompetanse` (med undertype: valg, ansettelse,
  utpeking — X bestemmer hvem som er myndighet —, oppnevning), `avsettingskompetanse`, `instruksjonskompetanse`
  (bruk polaritet), `samordningskompetanse`, `delegeringskompetanse` (X kan delegere), `godkjenningskompetanse`,
  `samtykkekompetanse`, `paleggskompetanse`, `normgivningskompetanse` (med normform), `tilsynskompetanse`,
  `revisjonskompetanse`, `klagekompetanse`, `omgjoringskompetanse`, `overprovingskompetanse` (undertype anke),
  `stadfestingskompetanse`, `vedtakskompetanse` (enkeltvedtak), `sanksjonskompetanse`, `foreleggingskompetanse`,
  `ukjent` (maskinell konvertering: et kompetanseuttrykk verken leksikonet eller KI kan typebestemme — gjettes ikke).
- medlemskap (aktør/klasse → klasse): `medlem_av`, `inngar_i`.
- sammensetning_omrade (område → område): `bestar_av`, `del_av`.
- ansvarsomrade (aktør → område): `har_ansvarsomrade`, `har_jurisdiksjon`, `har_sete_i`.
- konstituerende: `oppretter`, `avvikler`, `skal_finnes` («Hver kommune skal ha …»).
- organsammensetning: `har_medlemmer` (antall, hvem oppnevner), `har_organ` (rettssubjekt → organ, f.eks. «kommunestyret»).

## Lesing

Les ALLE noder (ikke stikkprøver). Spørring (bytt RID):

select nd.eid, nd.node_type, nd.overskrift, nd.tekst from rettskilde_noder nd
where nd.rettskilde_id='RID' and nd.entitetsstatus='gjeldende' and not nd.opphevet
order by nd.sorteringsrekkefolge;

Store kilder: les i bolker (offset/limit), men les alt.
