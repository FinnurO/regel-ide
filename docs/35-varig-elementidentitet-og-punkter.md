# Varig elementidentitet og punktbokstaver (undersøkelse for #495 og #498)

*Status: UTKAST, under arbeid 2026-11-25. Undersøkelse, ikke bygging. Prosjekteieren ga klarsignal til undersøkelse 2026-11-25.*

## Funn så langt (foreløpig)

- eId bygges i hver versjon fra §-nummer og ledd- og punktindeks (`src/RegelIde.Kildekonvertering/LovdataIdentifikatorer.cs`, `LeddEid`/`PunktEid`).
- Kilde-HTML-en for GDPR artikkel 6 har både punktbokstav (`<li data-name="a.">`) og underledd som eget `<article class="legalP">` etter lista. Feilen ligger i parseren, ikke i kilden.
- Målingen av omnummerering i korpuset pågår.
