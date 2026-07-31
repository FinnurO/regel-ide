# Kildegrunnlag for fasit-dokumenter

**Viktig, annen rolle enn `data/kilder/referanser/`:** filene her er ikke ekte, eksterne
kommune-/direktorat-dokumenter brukt som empirisk sammenligningsgrunnlag — de er Johanns egne,
opplastede **målbilder** ("fasiter") for hva en regel-ide-generert håndbok/veiledning skal kunne
levere på et gitt område. De lever ved siden av `docs/12-fasit-handbok-leveranse.md` (og senere
fasit-dokumenter for andre områder) som selve kildegrunnlaget den skårer output mot.

## Filer og proveniens

| Fil | Kilde | Status |
|---|---|---|
| `skjenkebevilling-rundskriv-fasit.md` | Johanns opplastede "målbilde"-dokument for skjenkebevilling-håndboken, versjon 4 (mottatt 2026-07-31, erstatter en tidligere versjon 3 som primærkilde for `docs/12-fasit-handbok-leveranse.md`). Ikke en reell kommunal/statlig kilde — et forfattet eksempel som viser strukturen/detaljnivået en ferdig håndbok bør ha. | Uendret original. Kjente, bevisste svakheter i dokumentet selv (ikke noe som skal "fikses" ved å redigere filen): to seksjoner nummerert "## 11." (duplikat, se `12-fasit-handbok-leveranse.md`), ingen inline rettskilde-lenker (bekreftet av Johann). |

## Hvorfor en egen mappe, ikke `data/kilder/referanser/`

`data/kilder/referanser/` er empirisk sammenligningsmateriale for å designe MOT ekte kommunal
praksis (Vennesla/Tønsberg/Helsedirektoratet) — ingen av dokumentene der er skrevet som et
sluttresultat regel-ide skal reprodusere. Filene her ER nettopp det: et definert sluttresultat en
test/skåring kan måles mot. Samme underliggende idé som `prototyper/*.dc.html` for skjermbilder,
bare for tekstinnhold.
