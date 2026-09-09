import { Link as RouterLink } from 'react-router';
import { Link } from '@digdir/designsystemet-react';

/**
 * [Ny, nemnd/sekretariat-runden, 2026-09-09] Én relasjonsrads tekst, med motpartens navn lenket
 * INNE i setningen.
 *
 * <p>
 * Bakgrunn: `visningstekst` kommer ferdig utfylt fra
 * `RelasjonsTypeKonfigurasjonEntitet.FraVisningsmal`/`TilVisningsmal` («er sekretariat for {0}»),
 * altså MED motpartens navn i seg. `VirksomhetDetalj` la likevel på «({motpartNavn})» etterpå for å
 * få en lenke, og resultatet leste «er sekretariat for Konkurranseklagenemnda
 * (Konkurranseklagenemnda)». Å fjerne dublisen ved å droppe parentesen ville tatt bort lenken —
 * derfor lenkes navnet der det ALT står.
 * </p>
 *
 * <p>
 * Finner vi ikke navnet i teksten (en mal som er endret i databasen til å utelate {0}), faller vi
 * tilbake til teksten pluss en egen lenke — ingen gjettet plassering, og aldri en rad uten vei
 * videre til motparten.
 * </p>
 */
export function RelasjonstekstMedLenke({
  visningstekst,
  motpartNavn,
  motpartVirksomhetId,
}: {
  visningstekst: string;
  motpartNavn: string;
  motpartVirksomhetId: string;
}) {
  const lenke = (
    <Link asChild>
      <RouterLink to={`/virksomheter/${motpartVirksomhetId}`}>{motpartNavn}</RouterLink>
    </Link>
  );
  const i = visningstekst.indexOf(motpartNavn);
  if (i < 0) {
    return (
      <>
        {visningstekst} ({lenke})
      </>
    );
  }
  return (
    <>
      {visningstekst.slice(0, i)}
      {lenke}
      {visningstekst.slice(i + motpartNavn.length)}
    </>
  );
}
