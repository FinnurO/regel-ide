# Endringslogg v0.2 — ontologi låst, Vedtak modellert, skjønn presisert

Denne runden svarer på en andre ekstern vurdering av v0.1 (se `00-endringslogg-v0.1.md` for den første). Vurderingen ga 8.5–9.5/10 på domene-/produktnivå og pekte på fem konkrete mangler; alle fem er adressert her, med to unntak som er bevisst avvist (begrunnet under).

## Adressert

| Funn i vurderingen | Adressert |
|---|---|
| Vilkår/Regel/Unntak er beskrevet som en åpen designbeslutning — domenemodellen er ikke ferdig så lenge den er åpen | **Låst.** Formell ontologi med kardinaliteter, invarianter og tillatte relasjoner i `01-referansemodell.md` §5, testet mot fire alkoholloven-eksempler. Feltnivå i `03-domenemodell.md` §1.8–1.10, API i `04-api-kontrakter.md` §7. |
| Vedtak eksisterer implisitt flere steder, ikke som sentral domenekonstruksjon | Lagt til som eksplisitt begrep (`01-referansemodell.md` §15.1: Vedtak/Vedtaksgrunnlag/Vedtaksvirkning) og feltskjema for testcase-simulering (`03-domenemodell.md` §1.15) — uten at regel-IDE selv blir eier av driftsdata (det er fortsatt `forklaringsmodell-api`s jobb). |
| Skjønn er nevnt mange steder, men skjønnsmoment/skjønnsgrunnlag/avklaringsbehov er uklare | Presisert som tre distinkte begrep med ulik natur (modelleringstid vs. sakstid) i `01-referansemodell.md` §6.1. |
| Primærbrukeren er uklar (jurist? fagansvarlig? utvikler? saksbehandler? alle samtidig?) | **Bevisst multi-rolle**, ikke en uklarhet — bekreftet av produkteier: tjenestedesignere, jurister, fagansvarlige/saksbehandlere og utviklere skal bruke verktøyet sammen, jf. `digital-rettsstat` prinsipp 7. Gjort eksplisitt i `02-produktkrav.md` innledningen. |
| Arkitekturen mangler systemkontekstdiagram | Lagt til i `05-arkitektur-og-nfk.md` §0. |
| MVP er for bredt (dashboard, kunnskapsgraf, saksbehandling, AI, eksport, begrepsforvaltning samtidig) | MVP-/konseptbevis-grensen er nå eksplisitt markert i `06-veikart.md` og `02-produktkrav.md` kap. 2 — dashboard og kunnskapsgraf er merket **utenfor**, ikke bare "sist". |

## Bevisst avvist (og hvorfor)

- **Publiseringsarkitektur (CQRS/event sourcing/transaksjonsgrenser) låst nå.** Vi har allerede en hendelsesmodell (`03-domenemodell.md` §5) som sier *hva* som skal skje ved publisering. *Hvordan* det realiseres teknisk er en implementasjonsbeslutning for når byggesteg 1 faktisk starter koding — å låse den nå ville bety å gjette uten last- eller konsistensdata. Se `05-arkitektur-og-nfk.md` §3.4 for begrunnelsen i sin helhet.
- **"Nasjonal standard"-kravene (institusjonell forankring, 5 pilotdomener, konformitet/sertifiserbarhet, uavhengighet fra implementasjon).** Dette er reelle spørsmål, men de hører til `digital-rettsstat`s Fase 2/3 (Institusjonalisering/Skalering, se `digital-rettsstat/docs/02-veikart.md`) — ikke til regel-IDEs jobb som Fase 1-referanseimplementasjon for ett pilotdomene. Å jage sertifiserbarhet nå ville motvirke den samme vurderingens eget råd om smalere scope.

## Ikke bekreftet ennå

- **Suksessmål/KPI.** Foreslått kandidat (lovspeil-latens: tid fra rettskildeendring til oppdatert/testet/publisert regel) står i `06-veikart.md`, markert eksplisitt som forslag til diskusjon — ikke bekreftet av produkteier ennå.
- Sekvensdiagrammer, fullstendige API request/response-skjemaer og kanonisk JSON er bevisst utsatt til etter at ontologien (byggesteg 0) er implementert og verifisert i byggesteg 4 — se `06-veikart.md`. Å spesifisere dem mot et nodeskjema som fortsatt kunne endre seg ville betydd å skrive dem to ganger.
