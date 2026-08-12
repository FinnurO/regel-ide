# Rådata — testfixture for `HandbokTekstParser` (byggesteg-1-utvidelse, Trinn 1)

Ikke Lovdatas eksportformat (se `../raw-lovdata/README.md`) — dette er tekstlaget fra et **hentet
PDF-dokument** (kommunal retningslinje), rådata for den nye, ikke-Lovdata-spesifikke
segmenteringsparseren beskrevet i `../../docs/15-handbok-dokumentgraf-notat.md` §2/§8 (Trinn 1).

## Fil og proveniens

| Fil | Kilde | Hentet | Metode |
|---|---|---|---|
| `bergen-retningslinjer-SD-24-113.txt` | Bergen kommune, *Retningslinjer for tildeling av salgs- og skjenkebevillinger i Bergen kommune for perioden 2024–2028* (Dok.nr. SD-24-113, Rev.nr. 01, fastsatt av Bystyret 19.06.2024, gyldig 01.07.2024–01.07.2028). Offentlig, `https://www.bergen.kommune.no/api/rest/filer/V51903878` | 2026-08-12 | **Ekte dokument, ikke syntetisk.** Hentet via WebFetch, PDF-tekstlaget lest side for side (5 sider) via Claude Codes innebygde PDF-lesing. Sidene er konkatenert i lesevolgen med sidebrytnings-støylinjene (`Dok.nr.: SD-24-113 Side N av 5`) bevart i teksten, nettopp fordi disse skal filtreres bort AV parseren som testes — å fjerne dem her ville gjort filtreringslogikken utestet. |

Teksten er PDF-tekstlagets naturlige linjeoppdeling (ikke reflowet/redigert), inkludert der en
overskrift (f.eks. «3.2») står alene på slutten av en side og selve brødteksten fortsetter først
etter neste sides støylinje — en reell paginering-splitter-node-på-tvers-av-side-situasjon, bevisst
IKKE rettet opp manuelt, fordi det er nøyaktig den typen støy en ekte PDF-tekstutvinning (PdfPig)
ville produsert.

Ingen endringer i sak-/personinnhold — dokumentet inneholder ingen navngitte saksbehandlere eller
direktenumre.
