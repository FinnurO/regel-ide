# docs/profil · Forslag til norsk profil for maskinlesbar rett

Denne mappen holder **profilen**, altså det teknologinøytrale begrepsapparatet, adskilt fra **implementasjonen** (Regel-IDE, docs/33 og resten av docs/). Skillet kommer fra grunnlagsdokumentet §14.

**Terminologi** (besluttet av Johann 2026-10-15):
- Profilen heter **«forslag til norsk profil»**, ikke «standard».
- Regel-IDE er **«utprøvingsarena og kandidatreferanseimplementasjon»**.
- Dette gjelder til det finnes styringsmodell, åpen høring, spesifikasjon, testpakke og konformitetstester.

## Dokumentene

| Nr. | Dokument | Status | Normativt? |
|---|---|---|---|
| 00 | [Grunnlagsdokument](00-grunnlagsdokument.md): Johanns forslag til norsk profil, revidert 9.10.2026 | REFERAT (oppdateres ikke) | Nei. Grunnlag |
| 01 | Profilspesifikasjon: definisjoner, krav, status | Ikke skrevet | Ja, når den publiseres |
| 02 | Begrepsmanual: definisjon, eksempel, moteksempel og grense per begrep, fra fasitkontrollen | Ikke skrevet | Ja, sammen med 01 |
| 03 | [Metodenotat](03-metodenotat.md): rettslig metode og status for modellutsagn | Arbeidsutkast | Nei. Grunnlag for 01 |
| 04 | [Mappingnotat](04-mappingnotat.md): regel-ide mot ELI/AKN, LKIF Core, UFO-L, LegalRuleML (verifisert mot primærkildene 2026-10-09) | Arbeidsutkast | Nei. Informativt (§5.7) |
| 05 | Plan for testkorpus: låst utvalg, uavhengig menneskelig annotering | Ikke skrevet | – |
| 06 | Beslutningslogg for typevekst (§18): samlet fra beslutningene i sakene | Ikke skrevet | – |

Implementasjonens svar på profilen: [../34-arkitekturvurdering-norsk-profil.md](../34-arkitekturvurdering-norsk-profil.md).

## Rangordning ved motstrid (grunnlagsdokumentet §14.6)

1. Profilspesifikasjonen (01).
2. Maskinlesbar profil og SHACL-former, i den grad 01 gjør dem normative.
3. Normative eksempler og testtilfeller.
4. Mappinger (informative i første versjon).
5. Regel-IDE (ikke-normativ).

Motstrid mellom disse er en feil i dokumentpakken. Den rettes med en versjonert endring, ikke ved at implementasjonen velger.
