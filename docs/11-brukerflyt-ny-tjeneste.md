# 11. Brukerflyt: Få inn en ny tjeneste med håndbok og vilkår

**Status: kommentert og rettet (2026-07-30).** Johann sin første kommentarrunde er innarbeidet
under (se punktene merket **Rettet**), inkl. ledd/punkt-nummerering (som opprinnelig sto i denne
lista som en større, utsatt sak, men ble løst i samme runde som referanse-arbeidet). Samme runde la
også til inline klikkbare kryssreferanser i selve løpeteksten (§ 1-5 vises nå som en ekte lenke inni
paragrafteksten, ikke bare i den separate "Referanser"-lista — se Fase 5/6). Flere av kommentarene
er større funksjonsønsker (daglig Lovdata-synkronisering, PDF/Word-import, data.norge.no-oppslag,
valg av grafeditor-bibliotek, håndbok-liste-side, mulighet til å velge underliggende rettskilde ved
opprettelse av ny håndbok, Forskrift→Lov-kobling utover den generelle kryssreferanse-mekanismen) som
fortsatt IKKE er løst — notert som separate saker og ikke gjentatt her.

Denne beskriver en foreslått ende-til-ende-flyt for
hvordan en fagperson/jurist går fra «vi har en tjeneste som trenger regelverksstøtte» til et
sammenhengende resultat i Regel-IDE: rettskilde → tjeneste → begreper → vilkårstre → håndbok/rundskriv →
tekst-tagging tilbake til lovteksten. Den er skrevet for to bruksområder:

1. **Brukertesting** — en konkret sti å følge for å gi tilbakemelding på hva som fungerer og ikke.
2. **Fremtidig brukerveiledning** — når flyten er kommentert og justert, kan den formaliseres.

Flyten er verifisert mot faktisk UI-kode (byggesteg 1, 2 og 4 runde 1) per 2026-07-30 — ikke bare
mot spesifikasjonen. Punkter merket ⚠️ har ingen UI-støtte ennå, kun API/seed-støtte.

## Kjente begrensninger denne flyten vil avdekke

| # | Begrensning | Hvor |
|---|---|---|
| ~~1~~ | ~~Ingen skjema for å legge til regelverksreferanser på en Tjeneste.~~ **Rettet 2026-07-30** — `TjenesteDetalj.tsx` har nå et skjema for å koble/fjerne regelverksreferanser. | `TjenesteDetalj.tsx` |
| ~~2~~ | ~~Ingen skjema for å sette juridisk grunnlag eller begrep på et Vilkår/Regelnode etter opprettelse.~~ **Rettet 2026-07-30** — juridisk grunnlag er nå en redigerbar liste, og Vilkår har Select-pickere for begrep/skjønnsgrunnlag. | `Egenskapspanel.tsx` |
| ~~3~~ | ~~Ingen skjema for å opprette et Unntak, eller for å koble et Datasett som input til et Vilkår.~~ **Rettet 2026-07-30** — «Nytt unntak»-skjema (med sykel-filter på betingelsen) og input-datasett-administrasjon på Vilkår. | `VilkarstreDetalj.tsx` / `Egenskapspanel.tsx` |
| ~~4~~ | ~~Nyopprettede, ikke-tilkoblede Vilkår/Regelnoder var usynlige i grafen/treet, uten forklaring.~~ **Rettet 2026-07-30** — en «Løse noder»-liste viser dem nå. | `VilkarstreDetalj.tsx` |
| ~~5~~ | ~~«Koble barn til regelnode»s tre nedtrekksmenyer manglet synlig ledetekst (Designsystemet-komponentfeil: `label`-prop på bare `Select`/`Textarea` blir en dødt HTML-attributt, ikke en synlig `<label>`).~~ **Rettet 2026-07-30**, systemisk over 6 filer — samtidig lagt til klientside sykel-filtrering på «Barn»-listen. | `Select`/`Textarea`-bruk i 6 filer |
| 6 | Ingen mulighet til å velge underliggende rettskilde/kildetype når man oppretter en ny håndbok — `HandbokOpprett.tsx` tar kun en tittel. | `HandbokOpprett.tsx` |
| 7 | Ingen side som lister eksisterende håndbøker — de er kun synlige/nåbare via den generelle rettskilde-listen. | *(ingen fil finnes ennå)* |

## Fase 0 — Forutsetning

Rettskilden (lov/forskrift) tjenesten bygger på må finnes i Rettskildebiblioteket.

- **Hvis den ikke finnes:** *Importer* → importer fra Lovdata (ELI + dato) eller last opp fil.
- **Forventet resultat:** kilden dukker opp i *Rettskilder*-listen med full paragraf/ledd-struktur.

## Fase 1 — Opprett tjenesten

1. *Tjenester* → skriv tittel → *Opprett*. Du kommer rett til tjeneste-detaljsiden.
2. Fyll ut CPSV-AP-NO-feltene (beskrivelse, kompetent myndighet, tjenestetype, målgruppe, kanaler,
   kostnad, behandlingstid, kontaktpunkt, konsekvens ved brudd, språk) → *Lagre*.
3. Under **Regelverksreferanser**: velg rettskilde, skriv inn eId (f.eks.
   `https://lovdata.no/eli/lov/.../§4-1`) → *Koble referanse*. Kan fjernes igjen med *Fjern*.

## Fase 2 — Kartlegg begreper (om tjenesten bruker skjønnsbaserte vilkår)

1. *Begreper* → skriv term (f.eks. «uklanderlig vandel») → velg type → *Opprett*.
2. På begrep-detaljsiden: fyll ut definisjon, sett *Lovreferanse (eId)* hvis det finnes en konkret
   paragraf begrepet stammer fra.
3. **Forventet resultat:** lenken «Åpne i rettskilden →» dukker opp under feltet.

## Fase 3 — Bygg vilkårstreet

1. *Vilkårstre* → finn tjenesten din i listen → *Opprett rotnode* → gi den en tittel
   (f.eks. «Vedtak om …») → *Opprett*. Du havner nå i grafeditoren.
2. *Nytt vilkår/regelnode* → velg type (Vilkår eller Regelnode) → gi tittel → *Opprett*. Gjenta for
   hvert vilkår tjenesten faktisk krever. Merk: en nyopprettet node vises ikke i selve grafen/treet
   før den er koblet inn — se den nye **«Løse noder»**-listen som dukker opp under knappene.
3. *Koble barn til regelnode* → velg forelder (rotnoden eller en undernode) → velg barn-type og
   barn → *Koble*. Bygg opp treet slik den reelle logikken (OG/ELLER) krever. «Barn»-listen filtrerer
   nå bort kandidater som ville skapt en sykel med valgt forelder.
4. Klikk på et Vilkår i grafen → Egenskapspanel → fyll ut vilkårstype/vurderingstype → *Lagre*.
   - ⚠️ Hvis vilkåret er skjønnsbasert og skal peke på et begrep, eller skal ha et juridisk
     grunnlag: dette **må settes utenfor UI** i denne runden. Noter dette som et testfunn.
5. Prøv å koble et barn som skaper en sykel (f.eks. koble rotnoden som barn av sitt eget barn) —
   **forventet resultat:** avvises med en tydelig feilmelding som viser sykelen.

## Fase 4 — Skriv håndbok/rundskriv-kommentar

Kan gjøres når som helst, uavhengig av fase 1–3.

1. *Ny håndbok* → gi tittel (f.eks. «Rundskriv til [tjenesten]») → *Opprett*. Du havner på
   kilde-detaljsiden.
2. *Nytt kapittel* → nummer + overskrift → *Opprett*.
3. Velg kapittelet i treet → *Ny kommentarseksjon her* → skriv tekst, sett dokumenttype/festenivå
   → koble til en lovparagraf i den opprinnelige rettskilden → *Lagre*.
4. **Forventet resultat:** kommentaren vises under kapittelet, og lovreferansen er søkbar/synlig.

## Fase 5 — Koble lovteksten til vilkårene (tekst-tagging)

1. Gå til den opprinnelige rettskilden → finn paragrafen/leddet et vilkår stammer fra.
2. Marker teksten som er selve vilkårsbetingelsen → velg laget **Vilkår** i tag-linjen → *Ny tagg*.
3. I tagg-listen under: bruk *«Koble til …»* for å knytte taggen til det riktige Vilkåret (krever
   at Vilkåret er opprettet i fase 3 først).
4. **Forventet resultat:** tagg-linjen viser nå en ekte lenke til vilkårets tittel i stedet for en
   GUID.

## Fase 6 — Verifiser at alt henger sammen (kryssnavigasjon)

- Fra rettskilden: klikk deg til vilkåret via tag-lenken → sjekk at Egenskapspanelet viser riktig
  juridisk grunnlag tilbake til samme paragraf.
- Fra begrepet: sjekk at *«Brukt i vilkår»* peker til riktig vilkår, med riktig node forhåndsvalgt
  i treet.
- Fra tjenesten: sjekk at *«Åpne vilkårstre»* virker, og at regelverksreferanser (om noen finnes
  fra seed) er klikkbare.
