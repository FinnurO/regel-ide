import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useSearchParams } from 'react-router';
import {
  Alert, Breadcrumbs, Card, Chip, Details, Field, Heading, Label, Link, Paragraph, Search, Spinner, Table, Tag,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type {
  AktorkoDetalj, AktorkoFordeling, AktorkoHjemmelforslag, AktorkoListe, AktorkoOrdtreff, AktorkoRad, AktorkoVariant, Aktorkorute,
  Kunnskapsstatus,
} from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { KunnskapsstatusTag } from '../strukturkant/Utsagnsstatus';
import { usePaginering } from '../tabell/usePaginering';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { AKTORKORUTE_TEKST } from '../kandidater/aktorkorute';

/**
 * [Ny, issue #440 PR 1, Johanns avgjørelser 2026-10-30] Aktørkøen: ett sted for ukjente aktører, gruppert per navn med alle forekomster.
 * Erstatter /navnekandidater og /virksomhet-kandidater som godkjenningssted (docs/09 §50). Johann 2026-10-30: «gjentagelsen skjer vel på
 * forslagsidene … altså før aktøren er opprettet» — det samme navnet sto én gang per forekomst, på fem ulike steder.
 *
 * <p><b>Bare lesing i PR 1.</b> Ingen knapper som skriver: godkjenningen (aktør, navneform, hjemmel og tagger i én transaksjon) kommer i
 * PR 2, etter juristrunden på hjemmelregelen. Hjemmelforslaget vises derfor bare som forslag (`info`), og ingenting er forhåndsvalgt.</p>
 *
 * <p>Oppsettet følger skissen Johann så 2026-10-30, visning (c): fordelingen øverst (hver kandidat havner på nøyaktig ett sted), regelen
 * for grupperingen, og køen som én lukket `Details` per navn (samme mønster som forslagskøen, docs/09 §36). Detaljen hentes først når
 * raden åpnes.</p>
 */

const RUTE_HAVNER: Record<Aktorkorute, string> = {
  generisk: 'Generiske omtaler, én rad per ord. Standardregelen eller en binding gir svaret per rettskilde (docs/33 §5.10).',
  kjent_aktor: 'Aktørsiden, som navnetreff på rettskildens rad. Samme behandling som virksomhetskandidatene i dag.',
  kjent_begrep: 'Begrepssiden eller områdesiden, som navnetreff.',
  boyningsform: 'Under ordet den ligner, som «annen form». Regelen og bindingen følger ikke med: «et departement» er ikke «departementet».',
  aktorko: 'Lista under, gruppert per navn.',
};

const RUTE_KORT = AKTORKORUTE_TEKST;

type Filter = 'alle' | 'flere' | 'hjemmel';

function tall(n: number) {
  return n.toLocaleString('nb-NO');
}

/** Teksten med ett spenn markert. Brukes for forekomsten i setningen og navnet i hjemmelforslaget. */
function Markert({ tekst, start, lengde }: { tekst: string; start: number; lengde: number }) {
  if (start < 0 || start + lengde > tekst.length) return <>{tekst}</>;
  return (
    <>
      {tekst.slice(0, start)}
      <mark>{tekst.slice(start, start + lengde)}</mark>
      {tekst.slice(start + lengde)}
    </>
  );
}

function Ordtreffbeskrivelse({ t }: { t: AktorkoOrdtreff }) {
  if (t.rute === 'boyningsform') {
    return <>bøyning av «{t.ligner}» ({t.suffiks}), {t.treff}. Regelen følger ikke med.</>;
  }
  return <>{t.treff ?? 'ingen regel traff'}</>;
}

function Fordeling({ fordeling }: { fordeling: AktorkoFordeling }) {
  return (
    <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.25rem' }}>
        {tall(fordeling.ventende)} ventende navnekandidater, fordelt
      </Heading>
      <Metatekst style={{ marginBottom: '0.75rem', maxWidth: '52rem' }}>
        Hver kandidat havner på nøyaktig ett sted. Reglene prøves i rekkefølge: likt ord (uten forskjell på store og små bokstaver) som en
        generisk navneform, en aktørs navneform eller registernavn, og en kjent klasse, rolle eller område. Deretter prøves bøyningsformer i
        samme rekkefølge. Treffene regnes ut når siden leses; ingenting flyttes, og ingenting skrives.
      </Metatekst>
      <Table data-size="sm" data-density="compact" border>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell>Rute</Table.HeaderCell>
            <Table.HeaderCell>De hyppigste ordene</Table.HeaderCell>
            <Table.HeaderCell style={{ textAlign: 'right' }}>Forekomster</Table.HeaderCell>
            <Table.HeaderCell style={{ textAlign: 'right' }}>Rettskilder</Table.HeaderCell>
            <Table.HeaderCell>Havner</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {fordeling.ruter.map((r) => (
            <Table.Row key={r.rute}>
              <Table.Cell>{r.rute === 'aktorko' ? <strong>{RUTE_KORT[r.rute]}</strong> : RUTE_KORT[r.rute]}</Table.Cell>
              <Table.Cell>
                <Metatekst as="span">
                  {r.rute === 'aktorko'
                    ? `Ingen av reglene over traff: ${tall(r.ulikeOrd)} ulike ord, som blir ${tall(fordeling.antallNavn)} navn.`
                    : r.topp.slice(0, 4).map((t) => `${t.ord} ${t.forekomster}`).join(', ') + (r.ulikeOrd > 4 ? ' …' : '')}
                </Metatekst>
              </Table.Cell>
              <Table.Cell style={{ textAlign: 'right' }}>{r.rute === 'aktorko' ? <strong>{tall(r.forekomster)}</strong> : tall(r.forekomster)}</Table.Cell>
              <Table.Cell style={{ textAlign: 'right' }}>{tall(r.rettskilder)}</Table.Cell>
              <Table.Cell><Metatekst as="span">{RUTE_HAVNER[r.rute]}</Metatekst></Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </Table>
      <Details style={{ marginTop: '0.75rem' }}>
        <Details.Summary>
          De {fordeling.hyppigste.length} hyppigste ordene ({tall(fordeling.hyppigste.reduce((s, t) => s + t.forekomster, 0))} forekomster):
          hvor hvert ord havner
        </Details.Summary>
        <Details.Content>
          <Table data-size="sm" data-density="compact">
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Ord</Table.HeaderCell>
                <Table.HeaderCell style={{ textAlign: 'right' }}>Forekomster</Table.HeaderCell>
                <Table.HeaderCell>Treffer</Table.HeaderCell>
                <Table.HeaderCell>Rute</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {fordeling.hyppigste.map((t) => (
                <Table.Row key={t.ord}>
                  <Table.Cell>{t.ord}</Table.Cell>
                  <Table.Cell style={{ textAlign: 'right' }}>{tall(t.forekomster)}</Table.Cell>
                  <Table.Cell><Metatekst as="span"><Ordtreffbeskrivelse t={t} /></Metatekst></Table.Cell>
                  <Table.Cell>{RUTE_KORT[t.rute]}</Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        </Details.Content>
      </Details>
    </Card>
  );
}

function Grupperingsregel({ fordeling }: { fordeling: AktorkoFordeling | null }) {
  return (
    <Details style={{ marginBottom: '1rem' }}>
      <Details.Summary>Slik blir forekomstene til navn</Details.Summary>
      <Details.Content>
        <ol style={{ margin: 0, paddingLeft: '1.25rem' }}>
          <li>
            <Paragraph data-size="sm"><strong>Samme ord:</strong> trimmet, mellomrom slått sammen og uten forskjell på store og små
              bokstaver. «Foreldrerådet» og «foreldrerådet» er samme ord. Skjer automatisk.</Paragraph>
          </li>
          <li>
            <Paragraph data-size="sm"><strong>Bøyning:</strong> like forledd, og det siste ordet skiller seg bare med ett suffiks fra en lukket
              liste (-ene, -ane, -ens, -ets, -en, -et, -er, -ar, -ne, -a, -e, -n, -r, -t, -s). Det korteste ordet må ha minst fire tegn.
              Skjer automatisk, og regelen står på varianten.
              {fordeling && ` Nå: ${tall(fordeling.ordIKoen)} ord blir ${tall(fordeling.antallNavn)} navn.`}</Paragraph>
          </li>
          <li>
            <Paragraph data-size="sm"><strong>Genitivforledd</strong> («barnehagens foreldreråd» mot «foreldrerådet»): bare et forslag, som
              står under navnet det ligner. Slås aldri sammen automatisk: «domstolens leder» er ikke «leder».
              {fordeling && ` Nå: ${tall(fordeling.genitivforslag)} forslag.`}</Paragraph>
          </li>
        </ol>
      </Details.Content>
    </Details>
  );
}

function Variantlinje({ v, navnetsOrd }: { v: AktorkoVariant; navnetsOrd: string }) {
  return (
    <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap', marginBottom: '0.25rem' }}>
      <span>{v.skrivemater.map((s) => `«${s.tekst}» ${s.forekomster}`).join(', ')}</span>
      {v.regel === 'samme_ord' ? (
        <Tag data-size="sm" data-color="neutral" title="Regel 1: samme ord uten forskjell på store og små bokstaver.">Samme ord</Tag>
      ) : (
        <Tag data-size="sm" data-color="neutral"
          title={`Regel 2: bøyningsform av «${v.boyningAv}». Slått sammen automatisk; kan skilles ut i neste runde.`}>
          Bøyning ({v.suffiks}){v.boyningAv !== navnetsOrd ? ` av «${v.boyningAv}»` : ''}
        </Tag>
      )}
    </div>
  );
}

function Hjemmelforslagvisning({ h }: { h: AktorkoHjemmelforslag }) {
  return (
    <div style={{ marginBottom: '0.75rem' }}>
      <Paragraph data-size="sm" style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap', marginBottom: '0.25rem' }}>
        <Link asChild><RouterLink to={`/rettskilder/${h.rettskildeId}`}>{h.kortnavn ?? h.rettskilde}</RouterLink></Link>
        <span>{h.bestemmelse ?? h.eid}</span>
        <KunnskapsstatusTag status={h.kunnskapsstatus as Kunnskapsstatus} />
        <Tag data-size="sm" data-color="info" title={`Forslag etter regelen: «${h.uttrykk}» og navnet i samme setning. Ikke vurdert av noen.`}>
          Forslag (regel)
        </Tag>
      </Paragraph>
      <Metatekst as="div" style={{ borderLeft: '3px solid var(--ds-color-accent-border-subtle)', paddingLeft: '0.6rem' }}>
        «<Markert tekst={h.setning} start={h.treffStart} lengde={h.treff.length} />»
      </Metatekst>
    </div>
  );
}

/** Detaljen for ett navn. Hentes først når raden åpnes. */
function Navnedetalj({ nokkel }: { nokkel: string }) {
  const [detalj, setDetalj] = useState<AktorkoDetalj | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    api.hentAktorkoNavn(nokkel)
      .then(setDetalj)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av navnet.'));
  }, [nokkel]);

  if (feil) return <Alert data-color="danger" data-size="sm">{feil}</Alert>;
  if (!detalj) return <Spinner aria-label="Laster navnet …" data-size="sm" />;

  const merknader = detalj.perRettskilde.flatMap((r) => r.forekomster).filter((f) => f.erMerknad).length;
  return (
    <div>
      <Heading level={4} data-size="2xs" style={{ marginBottom: '0.3rem' }}>Varianter</Heading>
      {detalj.varianter.map((v) => <Variantlinje key={v.normalisert} v={v} navnetsOrd={detalj.nokkel} />)}
      {detalj.genitivforslagInn.map((g) => (
        <div key={g.fraNokkel} style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap', marginBottom: '0.25rem' }}>
          <span>«{g.fraVariant}» {g.forekomster}</span>
          <Tag data-size="sm" data-color="info" title="Regel 3: genitivforledd. Bare et forslag; slås ikke sammen. Står som eget navn i køen.">
            Forslag: genitivforledd
          </Tag>
        </div>
      ))}
      {detalj.genitivforslagUt.length > 0 && (
        <Metatekst>
          Navnet har genitivforledd og kan være det samme som {detalj.genitivforslagUt.map((g) => `«${g.tilNokkel}»`).join(', ')}. Bare et
          forslag (regel 3).
        </Metatekst>
      )}

      <Heading level={4} data-size="2xs" style={{ margin: '0.75rem 0 0.3rem' }}>Forekomster per rettskilde</Heading>
      {detalj.perRettskilde.map((r) => (
        <div key={r.rettskildeId} style={{ marginBottom: '0.5rem' }}>
          <Paragraph data-size="sm" style={{ marginBottom: '0.15rem' }}>
            <Link asChild><RouterLink to={`/rettskilder/${r.rettskildeId}`}>{r.tittel}</RouterLink></Link>{' '}
            <Metatekst as="span">{r.forekomster.length}</Metatekst>
          </Paragraph>
          <ul style={{ margin: 0, paddingLeft: '1.1rem' }}>
            {r.forekomster.map((f) => (
              <li key={f.navnekandidatId}>
                <Metatekst as="span" title={f.eid}>{f.bestemmelse ?? f.eid}</Metatekst>{' '}
                {f.erMerknad && (
                  <Tag data-size="sm" data-color="neutral" title="Merknad eller veiledning (#419, docs/33 §5.14): kan tagges, men er aldri hjemmel.">
                    Veiledning, kan ikke være hjemmel
                  </Tag>
                )}{' '}
                {f.utdrag !== null ? (
                  <span>«<Markert tekst={f.utdrag} start={f.utdragStart} lengde={f.tekst.length} />»</span>
                ) : (
                  <Metatekst as="span">«{f.tekst}» (teksten er endret siden sveipet; utdraget vises ikke)</Metatekst>
                )}
              </li>
            ))}
          </ul>
        </div>
      ))}

      <Card style={{ padding: '0.75rem', marginTop: '0.75rem' }} data-color="neutral">
        <Heading level={4} data-size="2xs" style={{ marginBottom: '0.4rem' }}>
          Hjemmel for aktøren{detalj.hjemmelforslag.length > 0 ? ': forslag' : ''}
        </Heading>
        {detalj.hjemmelforslag.length > 0 ? (
          detalj.hjemmelforslag.map((h, i) => <Hjemmelforslagvisning key={`${h.eid}-${i}`} h={h} />)
        ) : (
          <Alert data-color="warning" data-size="sm" style={{ marginBottom: '0.4rem' }}>
            <strong>Hjemmel for aktøren mangler</strong> i rettskildene der navnet står. Ingen av de {tall(detalj.forekomster)} forekomstene står
            i en bestemmelse med et opprettelsesuttrykk og navnet{merknader > 0 ? `, og ${merknader} står i veiledning, som aldri er hjemmel` : ''}.
            Ingenting er gjettet inn i stedet.
          </Alert>
        )}
        <Metatekst>Regelen: {detalj.hjemmelregel}</Metatekst>
      </Card>
      <Metatekst style={{ marginTop: '0.5rem' }}>
        Godkjenning (aktør, navneform, hjemmel eller «hjemmel mangler», og tagger for alle forekomstene) kommer i neste runde (#440 PR 2).
      </Metatekst>
    </div>
  );
}

function Navnerad({ rad }: { rad: AktorkoRad }) {
  const [apnet, setApnet] = useState(false);
  const varianter = rad.varianter.length;
  return (
    <Details onToggle={() => setApnet(true)} style={{ borderBottom: '1px solid var(--ds-color-neutral-border-subtle)' }}>
      <Details.Summary>
        <span style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', flexWrap: 'wrap', width: '100%' }}>
          <strong style={{ minWidth: '14rem' }}>{rad.navn}</strong>
          <Metatekst as="span">
            {tall(rad.forekomster)} forekomst{rad.forekomster === 1 ? '' : 'er'} i {tall(rad.rettskilder)} rettskilde{rad.rettskilder === 1 ? '' : 'r'}
            {varianter > 1 ? ` · ${varianter} varianter` : ''}
            {rad.genitivforslagInn > 0 ? ` · ${rad.genitivforslagInn} genitivforslag` : ''}
          </Metatekst>
          {rad.forsteHjemmelforslag ? (
            <Tag data-size="sm" data-color="info" title={`Forslag etter regelen (${rad.hjemmelforslag} i alt). Ikke vurdert av noen.`}>
              Forslag: {rad.forsteHjemmelforslag.kortnavn ?? rad.forsteHjemmelforslag.rettskilde.slice(0, 40)}{' '}
              {rad.forsteHjemmelforslag.bestemmelse ?? ''}{rad.hjemmelforslag > 1 ? ` (+${rad.hjemmelforslag - 1})` : ''}
            </Tag>
          ) : (
            <Tag data-size="sm" data-color="warning"
              title="Ingen bestemmelse i rettskildene der navnet står har et opprettelsesuttrykk med navnet. Ingenting er gjettet.">
              Hjemmel for aktøren mangler
            </Tag>
          )}
          {rad.bareMerknader && <Tag data-size="sm" data-color="neutral">Bare i veiledning</Tag>}
        </span>
      </Details.Summary>
      <Details.Content>{apnet && <Navnedetalj nokkel={rad.nokkel} />}</Details.Content>
    </Details>
  );
}

export default function AktorKo() {
  const [sokeparametre, setSokeparametre] = useSearchParams();
  const rettskildeFilter = sokeparametre.get('rettskildeId');
  const [liste, setListe] = useState<AktorkoListe | null>(null);
  const [fordeling, setFordeling] = useState<AktorkoFordeling | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [sok, setSok] = useState('');
  const [filter, setFilter] = useState<Filter>('alle');

  useEffect(() => {
    const feilmelding = (e: unknown) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av aktørkøen.');
    api.hentAktorkoFordeling().then(setFordeling).catch(feilmelding);
    api.hentAktorko().then(setListe).catch(feilmelding);
  }, []);

  const iRettskilden = useMemo(
    () => (liste?.navn ?? []).filter((r) => !rettskildeFilter || r.rettskildeIder.includes(rettskildeFilter)),
    [liste, rettskildeFilter],
  );
  const synlige = useMemo(() => {
    const q = sok.trim().toLowerCase();
    return iRettskilden.filter((r) =>
      (filter === 'alle' || (filter === 'flere' && r.forekomster > 1) || (filter === 'hjemmel' && r.hjemmelforslag > 0))
      && (!q || r.navn.toLowerCase().includes(q) || r.varianter.some((v) => v.normalisert.includes(q))));
  }, [iRettskilden, sok, filter]);
  const paginering = usePaginering(synlige);

  const antall: Record<Filter, number> = {
    alle: iRettskilden.length,
    flere: iRettskilden.filter((r) => r.forekomster > 1).length,
    hjemmel: iRettskilden.filter((r) => r.hjemmelforslag > 0).length,
  };

  return (
    <>
      <Breadcrumbs data-size="sm" style={{ marginBottom: '0.75rem' }}>
        <Breadcrumbs.List>
          <Breadcrumbs.Item>
            <Breadcrumbs.Link asChild><RouterLink to="/virksomheter">Aktører</RouterLink></Breadcrumbs.Link>
          </Breadcrumbs.Item>
          <Breadcrumbs.Item>
            <Breadcrumbs.Link aria-current="page">Aktørkø</Breadcrumbs.Link>
          </Breadcrumbs.Item>
        </Breadcrumbs.List>
      </Breadcrumbs>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>Aktørkø</Heading>
      <Metatekst style={{ marginBottom: '1rem', maxWidth: '52rem' }}>
        Hvert navn i lovteksten som ikke kan kobles til en aktør, en klasse eller et generisk ord, står her én gang. Variantene og
        forekomstene står samlet under navnet. Hjemmelforslaget er bare et forslag: regelen vurderes av juristen før godkjenningen bygges.
        Den flate lista over navnekandidater står som{' '}
        <Link asChild><RouterLink to="/navnekandidater/historikk">historikk</RouterLink></Link>.
      </Metatekst>

      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}

      {fordeling ? <Fordeling fordeling={fordeling} /> : !feil && <Spinner aria-label="Laster fordelingen …" data-size="sm" />}
      <Grupperingsregel fordeling={fordeling} />

      <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', marginBottom: '0.5rem', flexWrap: 'wrap' }}>
        <Field style={{ minWidth: '16rem' }}>
          <Label>Søk</Label>
          <Search data-size="sm">
            <Search.Input aria-label="Søk i navn og varianter" value={sok} onChange={(e) => setSok(e.target.value)} placeholder="Navn eller variant …" />
            <Search.Clear onClick={() => setSok('')} />
          </Search>
        </Field>
        <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap' }} role="group" aria-label="Filter">
          <Chip.Radio data-size="sm" name="aktorko-filter" checked={filter === 'alle'} onChange={() => setFilter('alle')}>
            Alle {tall(antall.alle)}
          </Chip.Radio>
          <Chip.Radio data-size="sm" name="aktorko-filter" checked={filter === 'flere'} onChange={() => setFilter('flere')}>
            Flere forekomster {tall(antall.flere)}
          </Chip.Radio>
          <Chip.Radio data-size="sm" name="aktorko-filter" checked={filter === 'hjemmel'} onChange={() => setFilter('hjemmel')}>
            Har hjemmelforslag {tall(antall.hjemmel)}
          </Chip.Radio>
          {rettskildeFilter && (
            <Chip.Removable data-size="sm" aria-label="Fjern rettskildefilteret"
              onClick={() => { sokeparametre.delete('rettskildeId'); setSokeparametre(sokeparametre); }}>
              Bare én rettskilde
            </Chip.Removable>
          )}
        </div>
      </div>
      <Metatekst style={{ marginBottom: '0.5rem' }}>
        Sortert på antall forekomster. «Hjemmel» viser det første forslaget etter regelen, eller hullet.
      </Metatekst>

      <Card style={{ padding: 0, overflow: 'hidden' }} data-size="sm">
        {liste === null && !feil ? (
          <div style={{ padding: '1rem' }}><Spinner aria-label="Laster aktørkøen …" data-size="sm" /></div>
        ) : synlige.length === 0 ? (
          <Paragraph data-size="sm" style={{ padding: '1rem', margin: 0 }}>
            {liste && liste.navn.length > 0 ? 'Ingen navn passer søket eller filteret.' : 'Ingen ukjente navn venter.'}
          </Paragraph>
        ) : (
          paginering.visteRader.map((r) => <Navnerad key={r.nokkel} rad={r} />)
        )}
      </Card>
      <Pagineringskontroll {...paginering} />
    </>
  );
}
