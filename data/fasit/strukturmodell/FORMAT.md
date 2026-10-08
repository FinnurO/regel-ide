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
  "til": "a2",                // aktør-id ELLER null (for kompetanse: null)
  "objekt": "<for kompetanse: bestemmelse/sakstype/regelverk, f.eks. 'vedtak etter § 3-1', 'forskrift om …'>",
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

Typer per kategori (bruk disse når de passer, ellers "annet:<x>"):
- relasjon (aktør→aktør): `klageinstans_for`, `administrativt_underordnet`, `instruksjon` (bruk polaritet),
  `omgjoring` (bruk polaritet), `tilsyn_med_aktor`, `sekretariat_for`, `rapporterer_til`, `oppnevner`,
  `velger`, `ledes_av`, `eies_av`, `etterfolger`, `bistar`, `samarbeider_med`, `radgir`, `del_av`,
  `delegerer_til` (når BÅDE fra og til er gitt).
- kompetanse (aktør→bestemmelse/sakstype): `forskriftskompetanse`, `vedtakskompetanse`,
  `klagekompetanse`, `tilsynskompetanse` (tilsyn med at regelverk følges), `delegeringsfullmakt`
  (X kan delegere), `oppnevningskompetanse`, `instruksjonskompetanse`, `utpekingskompetanse`
  (X bestemmer hvem som er myndighet).
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
