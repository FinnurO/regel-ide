import { Tag } from '@digdir/designsystemet-react';

/**
 * [Ny, issue #395] Disposisjonstypene (den lukkede listen i src/RegelIde.Data/Disposisjonstyper.cs) og kildeformen, ÉN delt kilde for
 * visning og filter (samme regel som NavneformgrunnTag, docs/09 §15). Fargerollene er låst (docs/09 §16):
 * - disposisjonstypene er `neutral`: de sier hva slags vedtak kilden er, ikke om noe er riktig eller galt;
 * - kildeformen vises bare når den er `kunngjort`, som `info`: teksten er vedtaket slik det ble kunngjort i Lovtidend, ikke en konsolidert
 *   tekst. Konsolidert er normaltilfellet og får ingen merkelapp (ville vært støy på hver rad).
 * En ukjent verdi vises rå i stedet for å skjules.
 */
export const DISPOSISJONSTYPER: { kode: string; navn: string }[] = [
  { kode: 'oppretting', navn: 'Oppretting' },
  { kode: 'navneendring', navn: 'Navneendring' },
  { kode: 'nedleggelse', navn: 'Nedleggelse' },
  { kode: 'omorganisering', navn: 'Omorganisering' },
  { kode: 'overforing_ressortfordeling', navn: 'Overføring / ressortfordeling' },
  { kode: 'delegering', navn: 'Delegering' },
  { kode: 'instruks', navn: 'Instruks' },
  { kode: 'ikrafttredelse', navn: 'Ikrafttredelse' },
  { kode: 'regulering', navn: 'Regulering' },
];

const navnFor = (kode: string) => DISPOSISJONSTYPER.find((t) => t.kode === kode)?.navn ?? kode;

export function DisposisjonstypeTagger({ typer }: { typer: string[] | null | undefined }) {
  if (!typer || typer.length === 0) return null;
  return (
    <>
      {typer.map((t) => (
        <Tag key={t} data-color="neutral" data-size="sm">{navnFor(t)}</Tag>
      ))}
    </>
  );
}

export function KildeformTag({ kildeform }: { kildeform: string | null | undefined }) {
  if (kildeform !== 'kunngjort') return null;
  return <Tag data-color="info" data-size="sm">Kunngjort</Tag>;
}
