import { useState } from 'react';
import { Card, Field, Heading, Label, Paragraph, Select, Spinner } from '@digdir/designsystemet-react';
import type { Kompetansefamilie, StrukturkantDto } from '../api/types';
import { FAMILIE_REKKEFOLGE, FAMILIE_VISNING, StrukturkantTabell } from './StrukturkantTabell';

/** Gruppen for kompetanser uten familie: «beslutning» (står over alle familiene) og typer som ikke er plassert ennå. */
const UTEN_FAMILIE = 'uten_familie';
type Gruppe = Kompetansefamilie | typeof UTEN_FAMILIE;

/**
 * [Ny, issue #341, Johanns hierarkibeslutning 2026-10-08] Kompetansekantene for én node, gruppert og filtrerbare på
 * kompetansefamilie (struktur, personell, styring, normgivning, kontroll, klage og overprøving, vedtak, sanksjon). Svarer
 * på S9: «hvilken kompetanse har A, overfor hvem, etter hvilken paragraf?» — og omvendt, fra motpartens side, «hvem har
 * kompetanse overfor B?» (samme kant, til-malen).
 * <p>
 * Hver gruppe er en vanlig `StrukturkantTabell` (docs/09 §31: ingen egen kanttabell). Familien er en klassifisering og
 * vises som overskrift, ikke som statusfarge. Filteret er lokalt (alle kantene er alt hentet for noden); API-et har det
 * samme filteret som `?familie=` for andre klienter.
 * </p>
 */
export function KompetanseTabell({ kanter, tomTekst }: { kanter: StrukturkantDto[] | null; tomTekst: string }) {
  const [familie, setFamilie] = useState<Gruppe | ''>('');

  if (!kanter) {
    return <Card style={{ padding: '1rem', marginBottom: '0.75rem' }}><Spinner aria-label="Laster …" data-size="sm" /></Card>;
  }
  if (kanter.length === 0) {
    return <Card style={{ padding: '1rem', marginBottom: '0.75rem' }}><Paragraph style={{ margin: 0 }}>{tomTekst}</Paragraph></Card>;
  }

  const gruppeFor = (k: StrukturkantDto): Gruppe => k.familie ?? UTEN_FAMILIE;
  const grupper = ([...FAMILIE_REKKEFOLGE, UTEN_FAMILIE] as Gruppe[]).filter((g) => kanter.some((k) => gruppeFor(k) === g));
  const vist = familie ? grupper.filter((g) => g === familie) : grupper;
  const navn = (g: Gruppe) => (g === UTEN_FAMILIE ? 'Beslutning / uten familie' : FAMILIE_VISNING[g]);

  return (
    <>
      {grupper.length > 1 && (
        <Field data-size="sm" style={{ maxWidth: '18rem', marginBottom: '0.75rem' }}>
          <Label>Kompetansefamilie</Label>
          <Select data-size="sm" value={familie} onChange={(e) => setFamilie(e.target.value as Gruppe | '')}>
            <Select.Option value="">Alle ({kanter.length})</Select.Option>
            {grupper.map((g) => (
              <Select.Option key={g} value={g}>
                {navn(g)} ({kanter.filter((k) => gruppeFor(k) === g).length})
              </Select.Option>
            ))}
          </Select>
        </Field>
      )}
      {vist.map((g) => (
        <div key={g}>
          <Heading level={4} data-size="2xs" style={{ marginBottom: '0.4rem' }}>{navn(g)}</Heading>
          <StrukturkantTabell kanter={kanter.filter((k) => gruppeFor(k) === g)} tomTekst={tomTekst} />
        </div>
      ))}
    </>
  );
}
