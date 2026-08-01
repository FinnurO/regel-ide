# 14. Byggesteg 5, teknisk design — Kunnskapsbibliotek + KI-agenter

*Kort mal for rundene som bygger de gjenstående KI-agentene (Tjenestebeskrivelse/Vilkår-og-
Vilkårstre/Håndbok), skrevet etter at runde 1 («Identifiser tjenester» + «Identifiser begrep») var
verifisert. Se `06-veikart.md` for retningsbeslutningen og `02-produktkrav.md` kap. 3.10 for
brukerflaten. Mye kortere enn `08-byggesteg1-teknisk-design.md` — byggesteg 5 introduserer ett nytt
arkitektonisk mønster (ekstern KI-klient-abstraksjon), ikke et helt nytt delsystem.*

## 1. `IKiAgentKlient` — kontrakten fremtidige agenter skal bruke

```csharp
public interface IKiAgentKlient
{
    Task<string> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default);
}
```

- **Én metode, rå streng inn og ut.** Vendor-spesifikk request/response-oversettelse hører hjemme i
  den konkrete implementasjonen (`KiAgentKlientStub` i dag), IKKE i grensesnittet — det holder
  interfacet stabilt uansett hvilken leverandør som velges senere.
- **Parsing av svaret er agentens ansvar**, ikke klientens. Hver agent-service (`BegrepsforslagTjeneste`,
  `TjenesteforslagTjeneste`, og fremtidige) definerer sin egen private JSON-kontrakt for hva den ber
  om og forventer tilbake — samme mønster som `KiAgentKlientStub`s to faste svar viser.
- **Registrer i `Program.cs`**: `builder.Services.AddScoped<IKiAgentKlient, KiAgentKlientStub>();`.
  Et fremtidig ekte leverandørvalg bytter kun denne ene linjen (+ en ny klasse, + evt.
  `AddHttpClient` i stedet for `AddScoped` hvis leverandøren kalles over HTTP) — ingen endring i noen
  agent-service.
- **Leverandørvalg er fortsatt ikke tatt.** Når det tas: legg konfigurasjon under en ny
  `RegelIde:KiAgent:*`-seksjon i `appsettings.json` (samme `IConfiguration`-mønster som
  `RegelIde:Database`/`RegelIde:Kildemappe`), og vurder om trege, ekte KI-kall trenger en
  bakgrunnsjobb-mekanisme (ingen finnes i dag — se `05-arkitektur-og-nfk.md`).

## 2. Kunnskapsbibliotek — skjema og hva det IKKE dekker ennå

`KunnskapsbibliotekLenkeEntitet` (`src/RegelIde.Data/Entiteter.cs`) er i dag **kun lenker**, scopet
til `VirksomhetId` (ikke `TjenesteId` — se §3 for hvorfor):

| Felt | Type | Beskrivelse |
|---|---|---|
| `VirksomhetId` | `Guid`, påkrevd | Alltid virksomhetens eget arbeidsprodukt |
| `Url` | string, påkrevd | Validert med `Uri.TryCreate(..., UriKind.Absolute)` + http/https-sjekk |
| `Beskrivelse` | string?, valgfri | |

**Bevisst utelatt i runde 1**: fil-opplasting (PDF/Word/skannet) og notater. Hvis en fremtidig agent
faktisk trenger dette (f.eks. en Håndbok-agent som skal lese et faktisk rundskriv-utkast), er
naturlig utvidelse en `Type`-diskriminator ('fil'|'lenke'|'notat') på samme entitet — polymorf, samme
mønster som `TekstTaggEntitet.Kind`/`RegelnodeBarnEntitet.BarnType` — pluss en `byte[]?`-kolonne for
`fil` (Npgsql mapper `byte[]` til `bytea` automatisk, ingen `ValueConverter` nødvendig — det ville
vært **første** BLOB-kolonne i denne kodebasen). Dokumentinnholds-uttrekk (PDF-tekst/OCR) er en helt
egen, ikke-trivial oppgave — utsett til den faktisk trengs, ikke bygg forskuddsvis.

## 3. Hvorfor rettskilde (ikke Tjeneste, ikke dokument-opplasting) er kontekst-kilden i runde 1

Veikartets opprinnelige forslag («kunnskapsbibliotek sentrert rundt Tjeneste») forutsetter implisitt
at Tjenesten allerede finnes. Det holder for agenter som *anriker* en eksisterende Tjeneste
(Tjenestebeskrivelse, Håndbok), men **ikke** for en agent som skal identifisere *hvilke* Tjenester som
finnes i utgangspunktet. Runde 1s to agenter er derfor begge rettskilde-drevne:

- Rettskilder er allerede ekte, strukturert, importert tekst (`RettskildeNodeEntitet.Tekst` på
  ledd/punkt-nivå) — ingen parsing-/OCR-arbeid, i motsetning til et opplastet dokument.
- «Identifiser tjenester» supplerer med kunnskapsbibliotek-**lenker** (virksomhets-scopet, ikke
  Tjeneste-scopet) fordi en lov ikke sier noe om hvilke tjenester en spesifikk kommune organiserer —
  det gjør derimot virksomhetens egen nettside.
- «Identifiser begrep» bruker ikke kunnskapsbiblioteket i det hele tatt — SKOS-begreper hentes
  fullstendig fra rettskilde-tekst.

**Konsekvens for fremtidige agenter**: spør alltid "hva er den mest naturlige, allerede-strukturerte
kontekstkilden for DENNE spesifikke agenten" før du antar kunnskapsbiblioteket er riktig input — det
var det ikke for noen av de to agentene i runde 1.

## 4. Generalisering av `foreslatt_av_ai` — mønsteret å kopiere

For hver ny entitetstype en agent skal foreslå (Vilkår, Regelnode, …):

1. Legg `"foreslatt_av_ai"` til entitetens `GyldigeStatuser`-array i dens registertjeneste.
2. Legg til en `OpprettForslagFraKiAsync(...)` — kopi av den eksisterende `OpprettAsync`, men
   `Status = "foreslatt_av_ai"` og `ProveniensHjelper.NyForslagRad(...)` i stedet for `NyRad(...,
   "opprettet", ...)`.
3. Utvid `SettStatusAsync` med en valgfri `godkjentAv`-parameter (bakoverkompatibel) som setter
   `ProveniensEntitet.GodkjentAv` — allerede gjort for Begrep/Tjeneste, kopier mønsteret.
4. **Ikke** bygg en ny, generalisert kø-endepunkt/UI per type — hold ett kø-endepunkt + én side per
   artefakttype til minst tre-fire typer finnes og et reelt gjenbruksmønster er synlig i praksis
   (samme "ikke abstraher for tidlig"-prinsipp som resten av kodebasen).

## 5. Kjent, ikke-fikset kvirk (arvet, ikke innført av byggesteg 5)

`GET /api/rettskilder` uten `virksomhetId`-param returnerer delte/nasjonale kilder pluss **alle**
virksomheters publiserte lokale kilder, ikke strengt "egne + delte" for den innloggede virksomheten.
Presist nok for et system med i praksis én test-virksomhet, men bør rettes (endre
`RettskildeRepository.AlleRettskilderAsync`s filter til `r.VirksomhetId == null || r.VirksomhetId ==
virksomhetId`) før en fremtidig agent-runde med faktisk flere virksomheter i samme miljø.
