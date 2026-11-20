# Statsrådet og rollen statsråd (#486, PR #489), runde 1: sammendrag

Inndata: fire kort (j1–j4) som beskriver det modellereren bygget i `src/RegelIde.Data/RegjeringenTjeneste.cs` etter Johanns avgjørelse 2026-11-20 på PR #489. Dommene står i `utfordringer.json`.

## Tall

| Dom | Antall | Kort |
|---|---:|---|
| holder | 2 | j1, j4 |
| innvending | 1 | j3 (middels) |
| kontroller | 1 | j2 (gjelder noe utenfor raden) |
| prinsipiell | 0 | |
| **Sum** | **4** | |

## Innvendingen

**j3, middels: rollen skal knyttes til organet med G har_medlemmer, ikke del_av.**

Grl. § 12 første ledd sier at «Dette råd skal bestå av en statsminister og i det minste syv andre medlemmer». Domstolloven § 3 første ledd har samme form: «Retten skal ha en justitiarius og nitten andre dommere». Den bestemmelsen står i fasiten som G har_medlemmer fra Høyesterett til rollen dommer, med antallet i objekt (domstolloven u11, og likeså u19, u25 og u39).

Mønsteret med del_av i docs/33 §4.3 punkt 7 gjelder en stilling ved organet, som Høyesteretts direktør. Det gjelder ikke medlemmene. Modellereren skriver selv i begrunnelsen at ordlyden sier «medlem av», og at del_av er modellens form.

Jeg er enig i at I innehar og T skal_ha ikke passer.

Rettingen er:
- G har_medlemmer, fra statsrådet til rollen statsråd;
- objekt «en statsminister og minst syv andre medlemmer»;
- hjemmel Grl. § 12 første ledd;
- kunnskapsstatus fortolkning.

Typen er konfigurert og aktiv i den delte basen, men ingen kant av typen er lastet. Kontroller derfor at kanttjenesten godtar et rollebegrep på til-siden. Begrunnelsen for regel 4 (j4) må følge rettingen.

## Kontroller

**j2: termen «statsråd» må ikke brukes til tagging på term.**

Rollebegrepet selv holder. Grl. § 15 første ledd bruker «vedkommende statsråd» om medlemmet, så navnet er et tekstfunn, og statsministeren er med.

I korpuset betyr den ubestemte formen likevel nesten alltid organet eller beslutningsformen: 254 av 364 forekomster har «i» rett foran («Kongen i statsråd»). Det samme leddet som er hjemmelen, sier dessuten «det samlede statsråd» om organet.

Forekomstene må derfor bindes til rollen bare gjennom regel 4 eller av et menneske. Ellers trengs en ordsperre som for «staten». Jeg har ikke kontrollert om noe sveip tagger rollebegreper på term.

## Holder

- **j1:** Grl. § 12 første ledd skaper rådet, så hjemmelens art er oppretting. Navnet står i annet ledd og i § 15, så kunnskapsstatusen er riktig satt til fortolkning.
- **j4:** Forekomstene peker nå på rollebegrepet, med § 15 første ledd og fortolkning. Det er mer presist enn organet var i runde 1.

## Til Johann

Ingenting ennå. j3 er et typevalg som modellereren kan svare på. Det går til Johann bare hvis hun avviser innvendingen.

## Kilder

- Grunnloven i korpuset, gjeldende versjon: §§ 12, 15, 28, 30, 62, 72 og 82.
- `data/fasit/strukturmodell/domstolloven.json`: u11, u19, u25 og u39.
- `data/fasit/strukturmodell/FORMAT.md` (organsammensetning).
- docs/33 §4.3 (tabellen og punkt 7), §5.16 og §5.17.
- Delt base, bare SELECT 2026-11-20:
  - `relasjonstype_konfigurasjon`: har_medlemmer er aktiv;
  - `strukturkanter`: 0 kanter av typen har_medlemmer;
  - `begreper`: ingen begreper med termene «statsråd» eller «statsrådet»;
  - telling av «statsråd» i gjeldende noder.
