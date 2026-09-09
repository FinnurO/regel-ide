/**
 * [Ny, punktliste-runden, 2026-09-09, issue #128] Fra Lovdatas rå «ministry»-streng
 * (`RettskildeSammendrag.ansvarligDepartement`) til departementets EGEN virksomhetsrad, slik at
 * Departement-kolonnen i rettskildelisten kan lenke dit i stedet for å være død tekst.
 *
 * <p><b>Regelen er speilet fra serveren, ikke oppfunnet her.</b> Autoritativ kilde er
 * `VirksomhetOppslagTjeneste.FinnVirksomhetIdForNavnAsync` (RegelIde.Data): EKSAKT,
 * case-insensitivt navnematch mot `Virksomhet.Navn` — registerets egen form, ikke `visningsnavn` —
 * og `null` uten treff. Denne modulen gjør nøyaktig det samme mot den virksomhetslisten
 * `useVirksomheter` alt har hentet på siden (den brukes der allerede for `visEier`).</p>
 *
 * <p><b>Avvist alternativ, med pris.</b> Det opplagt «riktigere» grepet er å legge
 * `AnsvarligDepartementLenker` på `RettskildeSammendrag` slik `RettskildeDetalj` alt har det, og la
 * serveren løse oppslaget. Prisen er en DTO-endring + en BULK-variant av oppslaget i
 * `RettskildeRepository.RettskilderAsync` (listen er 5899 rader — ett oppslag per rad er en N+1) +
 * endring i `Program.cs`' listeendepunkt, altså backend-tester som ikke kan kjøres i denne
 * worktreen (issue #10: Data.Tests bruker fast port 55432, og to andre agenter jobber samtidig).
 * Issue #128 antar for øvrig at feltet ALT finnes på sammendraget («AnsvarligDepartementVirksomhetId»)
 * — det gjør det ikke, se `Dtos.cs`. Valget her er derfor bevisst det billige, og at regelen finnes i
 * to språk er den kjente kostnaden: den er én strengsammenligning, og drifter den, drifter den mot en
 * server som fortsatt er autoritativ (en lenke som ikke lages er et fravær, aldri en gal lenke).</p>
 *
 * <p>Ren modul uten React-avhengigheter (`docs/09` §17) — regelen er verdt å teste, og
 * «ingen gjettet fallback» er nettopp den delen av den som må låses av en test.</p>
 */

/** Bare feltene regelen leser. `VirksomhetDto` tilfredsstiller den strukturelt. */
export interface VirksomhetNavnerad {
  id: string;
  /** REGISTERETS form av navnet — det er dette feltet serveren matcher mot. Se `VirksomhetDto.navn`. */
  navn: string;
}

/**
 * Oppslag fra navn (lowercased) til virksomhets-id, bygget én gang per virksomhetsliste.
 *
 * <p>Ved to rader med case-ufølsomt likt navn vinner den FØRSTE i listen — samme utfall som
 * serverens `FirstOrDefaultAsync()`. Situasjonen skal ikke kunne oppstå (`OrganisasjonsregisterSeed`
 * matcher selv case-ufølsomt ved bakfylling, se dens klassekommentar), og et vilkårlig valg mellom
 * to identiske navn er ikke en gjetning om HVILKET departement det er — det er samme departement.</p>
 */
export function byggDepartementOppslag(virksomheter: readonly VirksomhetNavnerad[]): Map<string, string> {
  const kart = new Map<string, string>();
  for (const v of virksomheter) {
    const nokkel = v.navn.toLowerCase();
    if (!kart.has(nokkel)) kart.set(nokkel, v.id);
  }
  return kart;
}

/**
 * Virksomhets-id-en for ett departementnavn, eller `undefined`.
 *
 * <p>`undefined` betyr «ingen lenke», og dekker BEGGE de to tilstandene som må se like ut for
 * kalleren og ulike ut for oss: (a) navnet finnes ikke eksakt i katalogen, og (b) `oppslag` er
 * `undefined` fordi virksomhetslisten ennå ikke er hentet. Kalleren skal derfor vise departementet
 * som ren TEKST i begge tilfeller — ikke en «ukjent departement»-påstand, som ville vært en negativ
 * påstand vist mens data lastes (`docs/09` §15).</p>
 *
 * <p>Målt mot dev-basen 2026-09-09: 17 av 20 distinkte departementstrenger i korpuset treffer en
 * virksomhet. De tre som ikke gjør det er «Stortinget» (ikke et departement, og ikke i katalogen) og
 * to rader der Lovdatas flerverdi-felt er kommet inn KONKATENERT
 * («FinansdepartementetJustis- og beredskapsdepartementet») — det siste er en importfeil, rapportert
 * som funn, og nettopp et navn som ALDRI skal lenkes til «nærmeste treff».</p>
 */
export function departementVirksomhetId(
  departement: string,
  oppslag: Map<string, string> | undefined,
): string | undefined {
  return oppslag?.get(departement.toLowerCase());
}
