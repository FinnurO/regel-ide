import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Details, Heading, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { Metatekst } from '../entitet/Metatekst';
import type { Navneformgrunn, TilVurderingDto } from '../api/types';
import { NavneformgrunnTag } from '../virksomhet/Navneformgrunn';
import {
  byggTilVurdering, startHerDepartementer, tidPerElement, tidTotalt, type Blokkering, type EnkelRad, type KoRad,
} from '../tilvurdering/tilVurdering';

/**
 * [Ny, issue #517 «Til vurdering», Johanns avgjørelser 2026-11-27] Startsiden: det som venter på en menneskelig avgjørelse, per kø, med
 * «Start her», hva én vurdering krever, hva den låser opp og hva som blokkerer. Siden er en guide, ikke en ny kø: lenkene går inn i køene
 * som finnes. Rekkefølgen og tekstene står i den rene modulen `tilvurdering/tilVurdering.ts`; tallene i ett kall, `GET /api/til-vurdering`.
 * Mønsteret er docs/09 §59.
 */

const tall = (n: number) => n.toLocaleString('nb-NO');

function KoLenke({ rad }: { rad: Pick<KoRad, 'tittel' | 'href' | 'ekstern'> }) {
  if (rad.href === null) return <>{rad.tittel}</>;
  if (rad.ekstern) return <Link href={rad.href} target="_blank" rel="noopener noreferrer">{rad.tittel} ↗</Link>;
  return <Link asChild><RouterLink to={rad.href}>{rad.tittel}</RouterLink></Link>;
}

function BlokkeringTag({ b }: { b: Blokkering | null }) {
  if (!b) return <>—</>;
  return (
    <>
      <Tag data-size="sm" data-color={b.farge}>{b.tekst}</Tag>
      {b.forklaring && <Metatekst>{b.forklaring}</Metatekst>}
    </>
  );
}

function Departementliste({ tv, antall }: { tv: TilVurderingDto; antall?: number }) {
  const aktorer = antall === undefined ? tv.departementer.aktorer : tv.departementer.aktorer.slice(0, antall);
  return (
    <Table data-size="sm" data-density="compact" style={{ width: '100%', background: 'var(--ds-color-neutral-background-default)' }}>
      <Table.Head>
        <Table.Row>
          <Table.HeaderCell>Aktør</Table.HeaderCell>
          <Table.HeaderCell>Navneform som venter</Table.HeaderCell>
          <Table.HeaderCell style={{ textAlign: 'right' }}>Funn</Table.HeaderCell>
        </Table.Row>
      </Table.Head>
      <Table.Body>
        {aktorer.map((a) => (
          <Table.Row key={a.virksomhetId}>
            <Table.Cell><Link asChild><RouterLink to={`/virksomheter/${a.virksomhetId}`}>{a.navn}</RouterLink></Link></Table.Cell>
            <Table.Cell>
              {a.navneformer.map((n) => (
                <span key={n.begrepId} style={{ display: 'inline-flex', gap: '0.25rem', alignItems: 'center', marginRight: '0.5rem' }}>
                  {n.term} <NavneformgrunnTag grunn={n.navneformgrunn as Navneformgrunn | null} />
                </span>
              ))}
            </Table.Cell>
            <Table.Cell style={{ textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{tall(a.funn)}</Table.Cell>
          </Table.Row>
        ))}
      </Table.Body>
    </Table>
  );
}

function StartHerKort({ tv, rad }: { tv: TilVurderingDto; rad: KoRad }) {
  if (rad.id === 'departementer') {
    const s = startHerDepartementer(tv);
    const forste = s.aktorer[0];
    return (
      <Card data-color="accent" variant="tinted" style={{ padding: '1rem', marginBottom: '1rem' }}>
        <Paragraph data-size="sm" style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <Tag data-size="sm" data-color="info">Start her</Tag>
          <Metatekst as="span">{s.minutter} (anslag)</Metatekst>
        </Paragraph>
        <Heading level={2} data-size="sm">Godkjenn navneformene til {s.aktorer.length} departementer</Heading>
        <Paragraph data-size="sm">
          Navneformene er foreslått av maskinen og ikke vurdert. De holder igjen {tall(s.funn)} av de {tall(s.iAlt)} funnene om utgåtte aktører.
          Navnet og hjemmelen står på aktørsiden, så du bekrefter et faktum om navnehistorikken, ikke en tolkning.
        </Paragraph>
        <Paragraph data-size="sm"><strong>Slik gjør du det:</strong> åpne aktøren, gå til fanen Navneformer og trykk «Godkjenn» på navneformen
          som er forslag.</Paragraph>
        <Paragraph data-size="sm"><strong>Låser opp:</strong> «Navneform ikke faglig vurdert» forsvinner fra {tall(s.funn)} funn.</Paragraph>
        <Departementliste tv={tv} antall={s.aktorer.length} />
        {forste && (
          <div style={{ marginTop: '0.75rem' }}>
            <Button data-size="sm" asChild>
              <RouterLink to={`/virksomheter/${forste.virksomhetId}`}>Åpne {forste.navn}</RouterLink>
            </Button>
          </div>
        )}
      </Card>
    );
  }
  return (
    <Card data-color="accent" variant="tinted" style={{ padding: '1rem', marginBottom: '1rem' }}>
      <Paragraph data-size="sm" style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <Tag data-size="sm" data-color="info">Start her</Tag>
        {rad.minutter && <Metatekst as="span">{tidTotalt(rad.venter, rad.minutter)} (anslag)</Metatekst>}
      </Paragraph>
      <Heading level={2} data-size="sm">{rad.tittel}: {tall(rad.venter)}</Heading>
      <Paragraph data-size="sm"><strong>Låser opp:</strong> {rad.laaserOpp}</Paragraph>
      <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>{rad.krever.map((k) => <li key={k}><Metatekst as="span">{k}</Metatekst></li>)}</ul>
      {rad.href && !rad.ekstern && (
        <div style={{ marginTop: '0.75rem' }}>
          <Button data-size="sm" asChild><RouterLink to={rad.href}>Åpne {rad.tittel.toLowerCase()}</RouterLink></Button>
        </div>
      )}
    </Card>
  );
}

function Kotabell({ rader, nummerert, tv }: { rader: KoRad[]; nummerert: boolean; tv: TilVurderingDto }) {
  return (
    <Table data-size="sm" data-density="compact" style={{ width: '100%' }}>
      <Table.Head>
        <Table.Row>
          {nummerert && <Table.HeaderCell style={{ width: '2rem' }} />}
          <Table.HeaderCell style={{ width: '30%' }}>Kø</Table.HeaderCell>
          <Table.HeaderCell style={{ textAlign: 'right' }}>Venter</Table.HeaderCell>
          {nummerert && <Table.HeaderCell>Tid (anslag)</Table.HeaderCell>}
          <Table.HeaderCell>Låser opp</Table.HeaderCell>
          <Table.HeaderCell style={{ width: '20%' }}>Blokkerer</Table.HeaderCell>
        </Table.Row>
      </Table.Head>
      <Table.Body>
        {rader.map((rad, i) => (
          <Table.Row key={rad.id} style={{ verticalAlign: 'top' }}>
            {nummerert && <Table.Cell><Metatekst as="span">{i + 1}</Metatekst></Table.Cell>}
            <Table.Cell>
              <KoLenke rad={rad} />
              <Metatekst>{rad.hvor}</Metatekst>
              {rad.krever.length > 0 && (
                <Details>
                  <Details.Summary>Hva én vurdering krever</Details.Summary>
                  <Details.Content>
                    <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>{rad.krever.map((k) => <li key={k}><Metatekst as="span">{k}</Metatekst></li>)}</ul>
                  </Details.Content>
                </Details>
              )}
              {rad.id === 'departementer' && tv.departementer.aktorer.length > 0 && (
                <Details>
                  <Details.Summary>Alle {tall(tv.departementer.aktorer.length)} aktørene</Details.Summary>
                  <Details.Content><Departementliste tv={tv} /></Details.Content>
                </Details>
              )}
            </Table.Cell>
            <Table.Cell style={{ textAlign: 'right', fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
              <strong>{tall(rad.venter)}</strong>
              {rad.venterTillegg && <Metatekst>{rad.venterTillegg}</Metatekst>}
            </Table.Cell>
            {nummerert && (
              <Table.Cell style={{ whiteSpace: 'nowrap' }}>
                {rad.minutter ? <>{tidPerElement(rad.minutter)}<Metatekst>{tidTotalt(rad.venter, rad.minutter)} i alt</Metatekst></> : '—'}
              </Table.Cell>
            )}
            <Table.Cell>{rad.laaserOpp}</Table.Cell>
            <Table.Cell><BlokkeringTag b={rad.blokkerer} /></Table.Cell>
          </Table.Row>
        ))}
      </Table.Body>
    </Table>
  );
}

function Enkeltabell({ rader }: { rader: EnkelRad[] }) {
  return (
    <Table data-size="sm" data-density="compact" style={{ width: '100%' }}>
      <Table.Body>
        {rader.map((r) => (
          <Table.Row key={r.tittel}>
            <Table.Cell><KoLenke rad={{ tittel: r.tittel, href: r.href, ekstern: r.ekstern }} /></Table.Cell>
            <Table.Cell style={{ textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{r.venter === null ? '—' : tall(r.venter)}</Table.Cell>
            <Table.Cell><Metatekst as="span">{r.merknad}</Metatekst></Table.Cell>
          </Table.Row>
        ))}
      </Table.Body>
    </Table>
  );
}

export default function TilVurdering() {
  const [tv, setTv] = useState<TilVurderingDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    api.hentTilVurdering()
      .then((r) => { setTv(r); setFeil(null); })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av tallene.'));
  }, []);

  const visning = useMemo(() => (tv ? byggTilVurdering(tv) : null), [tv]);
  const startRad = visning?.klare.find((k) => k.id === visning.startHer) ?? null;

  return (
    <>
      <Heading level={1} data-size="lg">Til vurdering</Heading>
      <Metatekst>
        Det som venter på en menneskelig avgjørelse, i den rekkefølgen som gir mest per time. Tid per element er et anslag, ikke målt.
        {tv && <> Tallene er hentet {new Date(tv.hentet).toLocaleString('nb-NO', { dateStyle: 'short', timeStyle: 'short' })}.</>}
      </Metatekst>

      {feil && (
        <Alert data-color="warning" style={{ marginTop: '1rem' }}>
          Tallene er ikke tilgjengelige: {feil}
        </Alert>
      )}
      {!feil && !visning && <div style={{ padding: '1rem' }}><Spinner aria-label="Laster tallene …" data-size="sm" /></div>}

      {tv && visning && (
        <>
          <div style={{ marginTop: '1rem' }}>
            {startRad ? <StartHerKort tv={tv} rad={startRad} /> : (
              <Card style={{ padding: '1rem', marginBottom: '1rem' }}><Paragraph data-size="sm">Ingen av køene i rekkefølgen har noe som venter.</Paragraph></Card>
            )}
          </div>

          <Card style={{ padding: 0, overflow: 'hidden', marginBottom: '1rem' }}>
            <div style={{ padding: '1rem 1rem 0.5rem' }}>
              <Heading level={2} data-size="sm">Klar til vurdering</Heading>
              <Metatekst>Rekkefølgen er satt etter hva vurderingen låser opp delt på tiden den tar. {tall(visning.klarTeller)} elementer i alt.</Metatekst>
            </div>
            <Kotabell rader={visning.klare} nummerert tv={tv} />
          </Card>

          <Card style={{ padding: 0, overflow: 'hidden', marginBottom: '1rem' }}>
            <div style={{ padding: '1rem 1rem 0.5rem' }}>
              <Heading level={2} data-size="sm">Venter på noe annet enn deg</Heading>
              <Metatekst>Disse køene har elementer, men det mangler en skjerm, en måling eller en arbeidsflyt. Lenken går til saken når køen mangler
                en skjerm.</Metatekst>
            </div>
            <Kotabell rader={visning.blokkerte} nummerert={false} tv={tv} />
          </Card>

          <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
            <Details>
              <Details.Summary>Eldre køer, ikke prioritert ({visning.eldre.length})</Details.Summary>
              <Details.Content><Enkeltabell rader={visning.eldre} /></Details.Content>
            </Details>
            <Details>
              <Details.Summary>Utenfor appen ({visning.utenforAppen.length})</Details.Summary>
              <Details.Content><Enkeltabell rader={visning.utenforAppen} /></Details.Content>
            </Details>
          </Card>

          <Metatekst>
            Tallene kommer fra ett kall, GET /api/til-vurdering. Forslagskøen har {tall(tv.forslagskoTotalt)} forslag i alt; tallene over er de som
            hører til hver kø.
          </Metatekst>
        </>
      )}
    </>
  );
}
