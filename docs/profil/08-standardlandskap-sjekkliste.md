# Sjekkliste: standardlandskapet

[Ny, #473, 2026-11-12] Johann avgjorde 2026-11-06 at kildene under skal gås gjennom **for hver nye profilversjon**. Det er ingen automatikk. Bakgrunnen er at LOMO ikke kom med i profilarbeidet, fordi vi bare vurderte standardene Johann sendte (docs/profil/04 §7).

## Slik brukes lista

1. Gå gjennom hver rad før en ny profilversjon publiseres.
2. Åpne primærkilden selv. En nyhetsside eller et søketreff er ikke nok. Merk det som ikke kan åpnes som «ikke verifisert» (CLAUDE.md §8).
3. Skriv datoen i kolonnen «Sist sjekket», og kort hva som er nytt. Nytt innhold føres inn i mappingnotatet (04 §7, med verifiseringsloggen) og i standardseksjonen på nettsiden (`nettside/profil/`, #standardene).
4. En ny standard eller en ny versjon med nye begreper skal ha koblingstabell og juristrunde, som i 04 §7.7.
5. Legg til nye steder å lete når du finner dem.

## Kildene

| Kilde | Hva som sjekkes | Hvor | Sist sjekket | Status ved siste sjekk |
|---|---|---|---|---|
| **LOMO** | Ny versjon, artikkelen, endringer i roller og tid | https://drpm.pages.code.europa.eu/lomo/latest/ · https://code.europa.eu/drpm/lomo (CHANGELOG, tagger) | 2026-11-12 | 1.1.0 (2026-10-21), CC BY 4.0. Artikkelen kommer med JURIX-boka innen utgangen av 2026. Merket «foreløpig» til den er publisert |
| **Interoperable Europe** | Nyheter om ontologier for regelverk og om digitalklar lovgivning | https://interoperable-europe.ec.europa.eu/collection/digital-ready-policymaking · https://interoperable-europe.ec.europa.eu/topic/digital-government/digital-ready-policymaking | 2026-11-12 | LOMO kunngjort 2026-11-04 |
| **EU Vocabularies** (Publikasjonskontoret) | Nye ontologier og autoritetstabeller (role, administrative obligation) | https://op.europa.eu/en/web/eu-vocabularies/ontologies | 2026-11-12 (bare gjennom søketreff) | Ikke gjennomgått side for side |
| **SEMIC Core Vocabularies** | Nye versjoner av CPOV, CPSV-AP, Core Location, Person, Business og CCCEV | https://github.com/SEMICeu (utgivelser) og https://semiceu.github.io/<vokabular>/releases/ | 2026-11-12 | CPOV 2.1.2, CPSV-AP 3.2.0, Core Location 2.1.1, Person 2.1.2, Business 2.3.0, CCCEV 2.2.0. Interoperable Europe viser eldre versjoner |
| **ELI-ontologien** | Ny versjon (etter 1.5), lisens | http://data.europa.eu/eli/ontology (les `owl:versionInfo`) · https://op.europa.eu/en/web/eu-vocabularies/eli | 2026-11-12 | 1.5. Fila på op.europa.eu er 1.4. Lisens ikke oppgitt |
| **ELI-DL og ELI-registeret** | Versjon og status; hva registeret sier om Norge | http://data.europa.eu/eli/eli-draft-legislation-ontology · https://eur-lex.europa.eu/eli-register/norway.html | 2026-11-12 | ELI-DL V3.0. Norge: pillar 3, RDFa |
| **Lovdata** | Om ELI fortsatt er beta, om nivå (paragraf) har fått funksjon, RDFa, åpne data og lisens | https://lovdata.no/eli/ · https://api.lovdata.no/publicData · https://api.lovdata.no/xmldocs | 2026-11-12 | Fortsatt «BETA». RDFa uten `based_on`. XML har `ministry` og `basedOn` |
| **OASIS LegalRuleML og Akoma Ntoso** | Nye versjoner, OWL-ontologi | https://www.oasis-open.org/committees/legalruleml/ · https://www.oasis-open.org/committees/legaldocml/ | 2026-11-06 (LegalRuleML) | LegalRuleML 1.0 (2021), ingen ny. Akoma Ntoso ikke sjekket i #473 |
| **W3C ODRL** | ODRL 3.0-arbeidsgruppen og charteret | https://www.w3.org/community/odrl/ · https://www.w3.org/TR/odrl-model/ | 2026-11-06 | 2.2 (2018). Workshop juli 2026, charterutkast for 2027–2028 |
| **FLINT og eFLINT** | Nye versjoner og publikasjoner | https://gitlab.com/eflint · https://gitlab.com/normativesystems/knowledge-modeling/flint-ontology | 2026-11-12 | eFLINT 4.1.0.1, FLINT-ontologien 1.0.0 |
| **UFO-L og LKIF Core** | Nye artikler eller versjoner | https://nemo.inf.ufes.br/ (publikasjoner) · https://github.com/RinkeHoekstra/lkif-core | 2026-10-09 (04 §1–§6) | Ikke sjekket på nytt i #473 |
| **JURIX** | Aksepterte artikler om plikter, kompetanse, frister og offentlig forvaltning | https://jurix.nl/conferences/ · årets konferanseside (2026: https://www.irit.fr/jurix2026/program/) | 2026-11-06 | 79 aksepterte artikler i 2026, se 04 §7.4 |
| **ICAIL og AI and Law** | Samme tema | https://iaail.org/ · https://link.springer.com/journal/10506 | – | Ikke gjennomgått i #473 (forlagssidene var stengt for verktøyene) |
| **Digdir og Felles datakatalog** | Nye eller endrede spesifikasjoner, særlig en norsk organisasjonsprofil, og status i referansekatalogen | https://data.norge.no/specification · https://www.digdir.no/standarder/referansekatalogen-it-standarder/1480 | 2026-11-12 | CPSV-AP-NO 1.2.0, SKOS-AP-NO-Begrep 2.0.15, DCAT-AP-NO 3.0.7. Ingen CPOV-AP-NO |
| **Begrepskatalogen** | Om lovdefinisjoner publiseres med kilde, og verdiene for forhold til kilde | https://data.norge.no/vocabulary/relationship-with-source-type | 2026-11-12 | Tre verdier |
| **Registre** | Endringer i organisasjonsformer og inndeling | https://data.brreg.no/enhetsregisteret/api/organisasjonsformer · https://data.ssb.no/api/klass/v1/classifications/131 og /104 | 2026-11-12 | Brukes allerede i områderegisteret og virksomhetskatalogen |
| **Danmark** | Digitaliseringsklar lovgivning og felles organisasjonsmodell | https://digst.dk/digital-transformation/digitaliseringsklar-lovgivning/ · https://arkitektur.digst.dk/specifikationer/organisation | 2026-11-12 | OIO-modellen 2.0. Status for anvendelsesprofilen ikke verifisert |
| **Sverige** | Rättsinformation, ELI, myndighetsregister | https://lagrummet.se/ · https://myndighetsregistret.scb.se/ | 2026-11-12 | ELI og åpne data «behöver utredas vidare» (2025) |
| **Finland** | Finlex åpne data, tjenestekatalogen | https://www.finlex.fi/en/open-data/integration-quick-guide · https://api.palvelutietovaranto.suomi.fi/ | 2026-11-12 | Akoma Ntoso og ELI-alias i Finlex |

## Søk som kjøres i tillegg

Lista over fanger ikke alt. Kjør disse søkene og vurder treffene:

- «legal obligation ontology», «competence ontology public administration», «legal power ontology»
- «rules as code ontology», «machine-readable legislation ontology» med årstallet
- «site:interoperable-europe.ec.europa.eu ontology» og «site:op.europa.eu ontology»
- «regelverk maskinlesbart», «digitaliseringsvennlig regelverk» og «regelverk som kode» (Norge og Norden)
