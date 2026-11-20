# Måling av ordlydsreglene for «regjeringen» (#486), runde 1: sammendrag

Inndata: `inndata.json`, 20 forekomstkort (m1–m20), trukket stratifisert per regel (seed 486). Dommene står i `utfordringer.json`. Jeg har vurdert bare om lesningen, regelen, kunnskapsstatusen og hjemmelen holder for hvert kort, ikke om reglene burde vært flere eller om slutningene burde vært regler.

## Tall

| Dom | Antall | Kort |
|---|---:|---|
| holder | 19 | m2–m20 |
| innvending | 1 | m1 (lav) |
| kontroller | 0 | |
| prinsipiell | 0 | |
| **Sum** | **20** | |

Etter lesning:

| Lesning | Kort | Holder | Innvending |
|---|---:|---:|---:|
| fremmed_stat | m1–m3 | 2 | 1 (lav) |
| generisk_konvensjonsstat | m4–m7 | 4 | 0 |
| ikke_aktor | m8–m11 | 4 | 0 |
| rolle_statsrad | m12–m15 | 4 | 0 |
| funksjon | m16–m17 | 2 | 0 |
| regjeringens_resolusjon | m18–m20 | 3 | 0 |

Ingen av de 20 taggene gir feil referent eller en kant som ikke finnes.

## Innvendingen

**m1, lav: kunnskapsstatusen er for svak.** Syria-forskriften art. 27 nr. 1 bokstav b sier selv «den syriske regjeringen». Det er et tekstfunn, akkurat som «regjeringen i Australia» (m2) og «regjeringen for Det Forente Kongerike» (m3). Tittelregelen slår til først og gir bare fortolkning, fordi tittelen avgrenser kilden og ikke forekomsten. Lesningen er riktig. Rettingen er regel `fremmed:nasjonalitet` og kunnskapsstatus `tekstfunn`. Hvordan rekkefølgen skal være, er et regelspørsmål. Det kan også løses ved at tittelregelen beholdes, men at kunnskapsstatusen heves når ordlyden i forekomsten selv treffer en fremmed-regel.

## Merknader til kort som holder

- **m4 holder, men bare som fortolkning.** Den norske ordlyden i SOLAS regel IV/2 nr. 1.15 er «regjeringen i en annen stat som er part i konvensjonen». Lest isolert peker den mot fremmed stat, fordi definisjonen ikke har noen handlende stat som «annen» kan vise til, og forskriften gjelder norske skip. Den generiske lesningen bygger på den engelske teksten («a Contracting Government») og på at Norge selv definerer radiodekningsområder. Kunnskapsstatusen må derfor bli stående som fortolkning. [slutning] «annen stat» er trolig en variant i oversettelsen. Jeg har ikke kontrollert det mot Lovdata eller IMO.
- **m5:** Formuleringen «regjeringen i den staten hvis flagg fartøyet fører» er ordrett definisjonen av «Administrasjon» i 1969-konvensjonen art. 2 nr. 2. Forskriften gjelder norske skip (§ 1), og det støtter at Norge er omfattet.
- **m11:** Taggen gjelder ordet «Regjeringens», ikke trykningskontoret. «etter samråd med Regjeringens trykningskontor» kan selv gi trykningskontoret en aktør og en P-kant (konsultasjon), og ikke-aktør-taggen må ikke sperre den.
- **m12–m15, hjemmelen for rollen:** Grl. § 15 første ledd («Enhver som er medlem av statsrådet») gir rollenavnet ordrett, og den holder. At «medlem av regjeringen» betyr nettopp denne rollen, er bedre støttet av andre bestemmelser:
  - Grl. § 72 tredje ledd nr. 1 og 3, lest med § 62 annet ledd;
  - § 82 første ledd, der «Regjeringen» og «medlem av statsrådet» står i samme ledd.

  De kan tas med i begrunnelsen. Kunnskapsstatusen er fortsatt fortolkning.
- **m12:** Leddet har tre forekomster: «oversendt fra regjeringen», «medlem av regjeringen» og «innhentet fra regjeringen». Bare den midterste er tagget, så noden er flertydig etter vilkår 2 fra #463. Det er slik det skal være.
- **m16–m17:** Funksjonslesningen bæres av Grl. § 41 («forestå regjeringen som kongemaktens midlertidige utøver») og § 13 første ledd («styret av riket» = «føre regjeringen»). Hjemmelen er nå forekomstens egen eId, og § 41 og § 13 første ledd kan tas med i begrunnelsen.
- **m18–m20:** Grl. § 32 lyder: «De beslutninger som fattes av regjeringen under kongens fravær, utferdiges i kongens navn». Korpuset støtter lesningen: kystvaktinstruksen (FOR-1999-11-05-1145) og delegeringsvedtaket (FOR-1996-11-07-1016) har begge `fastsatt_av` = «Regjeringens res.». § 32 bruker selv «regjeringen» om den som beslutter, så Kongen i statsråd som referent er en fortolkning, og det er riktig merket. I m19 er oppramsingen en liste over vedtaksformer, ikke over organer.

## Tverrgående mønstre

- **Kunnskapsstatus følger regelen, ikke forekomsten.** Det gir utslag bare i m1, der en svakere regel (tittel) skygger for en sterkere (ordlyd).
- **Den generiske lesningen i konvensjonstekst holder i alle fire kortene.** I m4 og m5 er det likevel ordlyden lest med konvensjonens system som avgjør, ikke ordlyden alene. Fortolkning er riktig status for hele gruppen.
- **Avtaletitler:** I samme type tittel blir den norske forekomsten ikke aktør (m9) og den fremmede forekomsten fremmed stat (m2). Begge lesningene gir ingen kant, så det er ikke en feil. Det følger av den vedtatte rekkefølgen.

## Prinsipielle innvendinger

Ingen.

## Runde 2

Modellereren aksepterte m1 og m12 (`svar.json`). Replikken min er **godtar** på begge (`replikk.json`):

- **m1:** Ordlydsreglene for fremmed stat prøves nå før sanksjonstittelen. Jeg har kontrollert alle 14 forekomstene i gjeldende sanksjonsforskrifter i korpuset. Alle er fortsatt fremmed stat. 13 får nå en ordlydsregel og tekstfunn. Den russiske «regjeringens resolusjon» i Sanksjonsforskrift Ukraina § 19m treffer ingen ordlydsregel (genitiv, «ved» foran), og tittelregelen fanger den fortsatt før regel 6. Den blir altså ikke Kongen i statsråd.
- **m12:** Begrunnelsen for regel 4 viser nå til Grl. § 72 tredje ledd, § 62 annet ledd og § 82 første ledd ved siden av hjemmelen § 15 første ledd. Sitatene stemmer med Grunnloven i korpuset.

**Til Johann:** ingenting står igjen. Jurist og modellerer er enige om alle 20 kortene.

## Kilder

- Korpuset (delt `regelide`, bare SELECT, 2026-11-20):
  - Grl. §§ 12, 13, 15, 32, 41, 44, 62, 72 og 82 (gjeldende versjon);
  - Syria-forskriften art. 27;
  - FOR-2014-07-01-955 §§ 1–2 og SOLAS IV regel 2;
  - FOR-2015-06-30-823 § 1 og 1969-konvensjonen art. 2 og 12;
  - FOR-1977-02-15-2, vedlegg til bilag 3, regel 5;
  - sjøloven vedlegg 2 (Athen-konvensjonen);
  - strl. § 112;
  - `fastsatt_av` for FOR-1999-11-05-1145, FOR-1996-11-07-1016, FOR-2003-02-28-220 og FOR-1983-07-26-1385;
  - søk etter «trykningskontor».
- `src/RegelIde.Data/RegjeringenOrdlydsregler.cs` (regelnavn og rekkefølge).
- `data/fasit/strukturmodell/regjeringen/sammendrag.md` og `a/sammendrag.md` (#463), og docs/33 §5.19.
- SOLAS 1974 regel IV/2.1.12, engelsk tekst. Ikke kontrollert mot kilde i denne runden.
