# Juristutfordrer, aktørkøen del b (n97–n193): sammendrag av runde 1

Inndata: `inndata-b.json` (97 kort). Resultat: `utfordringer.json` (ett objekt per kort, i samme rekkefølge). Lovteksten er kontrollert med
SELECT mot `rettskilde_noder` og eksisterende noder mot `virksomheter` og `begreper` i den delte basen 2026-11-05. Ingenting er skrevet.

## Tall

| Dom | Antall |
|---|---:|
| holder | 58 |
| innvending | 37 |
| kontroller | 2 |
| prinsipiell | 0 |
| **sum** | **97** |

| Alvor (bare innvending) | Antall |
|---|---:|
| høy | 3 (n100, n103, n188) |
| middels | 13 (n101, n108, n116, n120, n129, n130, n143, n144, n145, n150, n154, n187, n190) |
| lav | 21 |

Av de 37 innvendingene gjelder 4 noe utenfor selve kortet (n97, n110, n142, n176). **Alle de sju pilotkortene holder** (n109, n127, n148,
n149, n157, n159, n193). Kontroller: n136 og n137 (sentralitetssone 5/6, se under).

## De viktigste innvendingene

1. **n100 «Felles klagenemnd» (høy): feil referent og en styringslinje som ikke finnes.** Kortet gjør nemnda til et organ i NTNU, men det
   er den nasjonale klagenemnda som Kunnskapsdepartementet oppnevner. Korpuset sier det selv: UiB-forskriftene § 17-2 («den nasjonale Felles
   klagenemnd») og Noroff-forskriften § 4-5 («oppnevnt av Kunnskapsdepartementet»). Hjemmelen er uhl. § 14-2 første ledd, med status
   fortolkning. Advarsel: «felles klagenemnd» i FOR-2020-02-13-688 er et annet organ.
2. **n103 «Forliksrådet» (høy): organ_i kommune er feil.** Forliksrådet er en domstol (dl. § 1 andre ledd, § 27 fjerde ledd), og namsfogden
   er sekretariat (§ 27 sjette ledd). At kommunestyret velger medlemmene, er oppnevningskompetanse. «I hver kommune» er territorielt, og
   felles forliksråd er mulig (§ 27 sjuende ledd). Typen og hjemmelen holder.
3. **n188 «tiltakssonen i Finnmark og Nord-Troms» (høy): bestar_av Troms gir feil svar.** Teksten nevner sju navngitte kommuner i Nord-Troms.
   O bestar_av arves geografisk, så Tromsø, Harstad og andre ville fått tiltakssonesatsene. Kantene må gå til de sju kommuneområdene, som
   alle finnes i registeret.
4. **Hjemmel «mangler» der korpuset har den (middels: n143, n144, n145, n150, n190; lav: n141, n151, n168, n180, n181).** Eksempler:
   - lagmann: dl. § 10 første ledd, samme ledd som modellereren selv bruker for førstelagmannen;
   - leder og nestleder i NOKUTs klagenemnd: FOR-2025-07-02-1462 § 3 andre ledd, «skal … være sammensatt slik: En leder og en nestleder»;
   - lokal redningssentral: definert i Organisasjonsplan for redningstjenesten pkt. 1.3;
   - Trollheimen: Trollheimenloven LOV-1984-12-21-101 er lastet og avgrenser området.

   Etter Johanns regel 2 skal hullet da ikke vises.
5. **Eksisterende eller ikke-foreslåtte noder:**
   - n108 «fylkesnemnda» lager en ny klasse, men virksomheten «BARNEVERNS- OG HELSENEMNDA» (d1aabe5f…, orgnr 995710757) finnes allerede
     (middels).
   - Fem omtaler og relasjoner peker på noder som ikke foreslås i noe kort, verken i a eller b, og som ikke finnes i basen:
     - «forvaltningsorgan» (n97, n176, og etter rettingen også n154);
     - «lagmannsrett» (n110, n143, n86);
     - «barnekoordinator» (n142);
     - «barnehagekommune …» (n123);
     - «domstol (den som behandler saken)» (n167).

Andre middels:
- n101 og n120 har ikke-merkede slutninger: organ_i kommune for felles samarbeidsutvalg, og eies_av kommune for kommunal barnehage.
  Teksten sier henholdsvis «kan bestemme» og «ansvaret for».
- n116: ankeutvalget er ikke en organisatorisk enhet uten myndighet. Det er Høyesterett satt med tre dommere og har egen
  avgjørelsesmyndighet etter tvl. §§ 30-1 til 30-3.
- n129 og n130: «kommuner i tiltakssonen …» er en klasse som forskriften definerer («menes»), ikke kommune med et vilkår. Det er også
  inkonsistent med n188.
- n154 og n187: ikke-aktør er feil når navnet er subjekt for en kompetanse (innhenting fra Folkeregisteret, krav om bevisopptak).

## Tverrgående mønstre

- **Hullet settes for raskt.** Modellereren har brukt regelens resultat (ingen forslag) som om det var juristens vurdering. Rundt ti kort har
  hjemmelen i samme paragraf eller i en lastet kilde, oftest i sammensetningsbestemmelser og definisjoner. Regelen gir ikke forslag ved
  oppnevning og sammensetning, men et menneske kan og bør registrere hjemmelen med status fortolkning (juristrunde-440 punkt 5).
- **Organtilhørighet brukes som standardrelasjon** (organ_i kommune: forliksråd, felles samarbeidsutvalg, PPT; organ_i NTNU: Felles
  klagenemnd). I tre av dem står ikke tilknytningen i teksten, og to gir en styringslinje som ikke finnes. Teksten sier oftere «i hver
  kommune» (territorium), «kan bestemme» (kompetanse) eller «ansvaret for» (ansvar).
- **Oppfølging mellom kortene mangler.** Ni relasjoner peker på noder ingen foreslår (se punkt 5). n97 og n176 er samme forekomst.
- **Områder:**
  - Utgåtte kommuner og fylker kobles med etterfulgt_av der arealet er slått sammen med andre (Hedmark → Innlandet, Meldal → Orkland). Det
    bør være inngår_i (lav).
  - Kanter til et helt fylke der teksten lister kommuner, gir feil arv (n188).
- **Definerte klasser i tilskuddsforskriften** («Med … menes …») er behandlet som vilkår. Tiltakssonen bør bli klasse eller område, fordi
  den er territoriell og medlemmene er offentlige pliktsubjekter. Sentralitetssone 5/6 (n136, n137) er satt til kontroller: definisjonen viser
  til SSBs indeks (utenfor korpus), og Johann må avgjøre om tariffkriterier skal bli klasser eller vilkår i regellaget. Private barnehager i
  vedlegg I (n162) holder som vilkår, fordi det er private parters rettigheter.
- **Funn til docs/33 §4.5:** Nav-loven § 4 andre ledd («Etaten skal forvalte … folketrygdloven, med unntak av kapittel 5») fyller det som der
  kalles et «synlig hull» for folketrygden utenom kapittel 5 (n102).
- **Grupperingen:** De samme ordene betyr ulike referenter. Det gjelder «nemndas leder», «leder» og «nestleder» i §§ 3, 7 og 11 i KD-
  forskriften (tre nemnder), «vertskommune» (spesialpedagogisk forskrift mot koml. kap. 20) og «felles klagenemnd» (nasjonal mot
  Skrivekunstakademiet). Det er ikke truffet ennå, fordi hvert navn har én forekomst. Det vil skje når flere kilder lastes.

## Venter-kortene

n107 «fremmed stat» og n148 «myndighetsnivåene» holder som venter. «fremmed stat» avhenger av hvordan stat modelleres (som Sverige og Finland
i a). «myndighetsnivåene» er forvaltningsnivåene. Leddet er ikke normativt, så ingenting går tapt om det blir ikke-aktør. Fylkeskommune-
delene i n124, n157 og n177 venter med rette.

## Prinsipielle innvendinger

Ingen.
