import { Heading, Link, Paragraph, Tag } from '@digdir/designsystemet-react';
import type { FunnDto, FunnKategori, FunnOffentlig, FunnStatus } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { delITekstblokker, delPremissmangler, etterfolgerlinje, lovdataLenke, utenEtterfolgerpunkt } from './funnTekst';

export { lovdataLenke };

/**
 * [Ny, issue #429 «Funn til vurdering», 2026-10-30] ÉN delt kilde for visningen av funn i appen (docs/09 §49): tekstene, merkene og
 * forhåndsvisningen av det som publiseres. Ordbruken er låst: «funn til vurdering», aldri «feil» eller «avvik» (#429 AK5).
 *
 * Fargerollene er låst per betydning (docs/09 §15/§25/§35):
 * - kategori og undertype er en klassifisering → `neutral`;
 * - oppdaget og juristvurdert er ventende → `info` (som forslag);
 * - godkjent for publisering → `success` (som «Godkjent» i KandidatStatusTag);
 * - forklart bort → `neutral`: det er en avklaring, ikke en feil, og `danger` er forbeholdt avvist;
 * - «Kilde endret» og premissporten → `warning`;
 * - [Ny, #429 PR 3] forklart bort av en maskinregel → eget merke i `neutral`, med hele teksten synlig (som KI-merket).
 */

export const KATEGORI_TEKST: Record<FunnKategori, string> = {
  utgatt_aktor: 'Utgått aktør',
  mulig_motstrid: 'Mulig motstrid',
  uklarhet_hull: 'Uklarhet eller hull',
};

export const UNDERTYPE_TEKST: Record<string, string> = {
  nedlagt_aktor: 'Nedlagt aktør',
  utgatt_navn: 'Utgått navn',
  // [Ny, #429 PR 4, Johanns avgjørelse 2026-11-04 (2)] Navnet står bare i avgiverformelen. Sorteres sist i lista (API-et). Ikke i UNDERTYPER:
  // det er maskinregelens klassifisering, ikke noe som velges ved registrering.
  avgiverformel: 'Bare i avgiverformelen',
  hull: 'Hull',
  upresis_henvisning: 'Upresis henvisning',
  publiseringsfeil_kilde: 'Publiseringsfeil i kilden',
};

export const UNDERTYPER: Record<FunnKategori, string[]> = {
  utgatt_aktor: ['nedlagt_aktor', 'utgatt_navn'],
  mulig_motstrid: [],
  uklarhet_hull: ['hull', 'upresis_henvisning', 'publiseringsfeil_kilde'],
};

export const STATUS_VISNING: Record<FunnStatus, { tekst: string; farge: 'info' | 'success' | 'neutral'; forklaring: string }> = {
  oppdaget: { tekst: 'Oppdaget', farge: 'info', forklaring: 'Registrert, ikke juristvurdert.' },
  juristvurdert: { tekst: 'Juristvurdert', farge: 'info', forklaring: 'Juristen har prøvd å forklare funnet bort. Ikke godkjent for publisering.' },
  godkjent_for_publisering: {
    tekst: 'Godkjent for publisering', farge: 'success',
    forklaring: 'Godkjent av en publiseringsansvarlig. Det frosne innholdet kommer med i neste eksport til nettsiden.',
  },
  // [ENDRET, #429 PR 3] Ikke «Juristen har …»: en maskinregel kan også forklare bort, og det vises med eget merke (MaskinregelTag).
  forklart_bort: { tekst: 'Forklart bort', farge: 'neutral', forklaring: 'Forklart bort med begrunnelse. Publiseres ikke.' },
};

export const SIKKERHET_TEKST: Record<string, string> = {
  hoy: 'høy',
  middels_til_hoy: 'middels til høy',
  middels: 'middels',
  lav_til_middels: 'lav til middels',
  lav: 'lav',
};

export const ALLE_STATUSER: FunnStatus[] = ['oppdaget', 'juristvurdert', 'godkjent_for_publisering', 'forklart_bort'];
export const ALLE_KATEGORIER: FunnKategori[] = ['utgatt_aktor', 'mulig_motstrid', 'uklarhet_hull'];

export function FunnStatusTag({ status }: { status: FunnStatus }) {
  const v = STATUS_VISNING[status];
  return <Tag data-size="sm" data-color={v.farge} title={v.forklaring}>{v.tekst}</Tag>;
}

export function FunnKategoriTag({ kategori, undertype }: { kategori: FunnKategori; undertype: string | null }) {
  const tekst = KATEGORI_TEKST[kategori] ?? kategori;
  const under = undertype ? UNDERTYPE_TEKST[undertype] ?? undertype : null;
  return <Tag data-size="sm" data-color="neutral">{under ? `${tekst}: ${under.toLowerCase()}` : tekst}</Tag>;
}

/** Hele teksten står synlig, ikke bare i `title` — som fasitmerket i docs/09 §46 (Johann 2026-10-30, anbefaling 7). */
export function KiVurdertTag() {
  return (
    <Tag data-size="sm" data-color="neutral" title="Juristvurderingen er gjort av juristagenten (CLAUDE.md §23), ikke av en menneskelig jurist.">
      Juristvurdert av KI-agent, ikke kontrollert av menneskelig jurist
    </Tag>
  );
}

/**
 * [Ny, #429 PR 3, Johanns avgjørelse 2026-11-02 (5)] Funnet er forklart bort av en maskinregel, ikke av en jurist. Hele teksten står synlig,
 * som KI-merket: maskin eller menneske skal synes (CLAUDE.md §9). Begrunnelsen med regelen og beviset står på detaljsiden.
 */
export function MaskinregelTag() {
  return (
    <Tag data-size="sm" data-color="neutral" title="En maskinregel holdt funnet tilbake etter at det var lastet. Begrunnelsen viser regelen og beviset.">
      Forklart bort av maskinregel, ikke av jurist
    </Tag>
  );
}

/** De merkene som hører til et funn, i fast rekkefølge: status, kategori, KI-vurdering eller maskinregel, kilde endret, premissport. */
export function FunnMerker({ funn }: { funn: FunnDto }) {
  const { hjemmel, andre } = delPremissmangler(funn.premissmangler);
  return (
    <>
      <FunnStatusTag status={funn.status} />
      <FunnKategoriTag kategori={funn.kategori} undertype={funn.undertype} />
      {funn.vurdertAvType === 'ki_agent' && funn.status !== 'oppdaget' && <KiVurdertTag />}
      {funn.vurdertAvType === 'maskinregel' && <MaskinregelTag />}
      {funn.kildeEndretTidspunkt && (
        <Tag data-size="sm" data-color="warning" title="En bestemmelse funnet peker på, har fått ny tekst i en ny versjon fra Lovdata.">
          Kilde endret – vurder på nytt
        </Tag>
      )}
      {andre.length > 0 && funn.status === 'juristvurdert' && (
        <Tag data-size="sm" data-color="warning" title={andre.join(' ')}>
          {andre.some((m) => m.startsWith('Etterfølger ikke vurdert')) ? 'Etterfølger ikke vurdert' : 'Premiss ikke vurdert'}
        </Tag>
      )}
      {/* [Ny, #429 PR 3, R9] Også på et funn som bare er oppdaget: mangelen skal synes før juristen tar det (Johann 2026-11-02 (5)). */}
      {hjemmel.length > 0 && (funn.status === 'oppdaget' || funn.status === 'juristvurdert') && (
        <Tag data-size="sm" data-color="warning" title={hjemmel.join(' ')}>Hjemmelsloven kan være opphevet</Tag>
      )}
    </>
  );
}

/** Tekst der linjer som begynner med «- » er punkter — samme regel som nettsiden bruker (nettside/funn/index.html). */
export function FunnTekst({ tekst }: { tekst: string }) {
  const blokker = delITekstblokker(tekst);
  return (
    <>
      {blokker.map((b, i) =>
        b.type === 'liste' ? (
          <ul key={i} style={{ margin: '0 0 0.75rem', paddingLeft: '1.25rem' }}>
            {b.linjer.map((l, j) => <li key={j}><Paragraph data-size="sm" style={{ margin: 0 }}>{l}</Paragraph></li>)}
          </ul>
        ) : (
          <Paragraph key={i} data-size="sm" style={{ margin: '0 0 0.5rem' }}>{b.linjer.join(' ')}</Paragraph>
        ),
      )}
    </>
  );
}

/**
 * Forhåndsvisning av det som publiseres — bygget av nøyaktig det objektet eksporten skriver (`forhandsvisning`, det frosne
 * øyeblikksbildet når funnet er godkjent). Rekkefølgen og ordene følger nettsiden (nettside/funn/index.html), så det Johann godkjenner
 * her, er det leseren ser der.
 */
/**
 * `modus` styrer bare metalinjen: et funn som ikke er godkjent, skal aldri se godkjent ut i forhåndsvisningen (rettet i kaldsjekken
 * 2026-10-30 — `godkjentDato` i forhåndsvisningen er dagens dato, ikke en godkjenning). `etterfolgerIkkeVurdert` gir «Etterfølger ikke
 * vurdert» i stedet for navnet når premissporten stopper funnet på etterfølgerkanten (Johanns avgjørelse 2026-10-30).
 */
export function OffentligFunn({ funn, modus, etterfolgerIkkeVurdert = false }: {
  funn: FunnOffentlig;
  modus: 'godkjent' | 'forhandsvisning' | 'til-godkjenning';
  etterfolgerIkkeVurdert?: boolean;
}) {
  const statuslinje = modus === 'godkjent'
    ? `Godkjent for publisering ${funn.godkjentDato}`
    : modus === 'til-godkjenning' ? `Godkjennes for publisering ${funn.godkjentDato}` : 'Ikke godkjent for publisering';
  // [ENDRET, #429 PR 2] Juristens punkt «Etterfølger» står i etterfølgerlinjen når funnet ikke peker på noen etterfølgerkant.
  const vurderingspunkter = funn.kategori === 'utgatt_aktor' ? utenEtterfolgerpunkt(funn.vurderingspunkter, funn.etterfolgere) : funn.vurderingspunkter;
  return (
    <div>
      <Heading level={3} data-size="xs" style={{ marginBottom: '0.4rem' }}>{funn.tittel}</Heading>
      <Metatekst style={{ margin: '0 0 0.6rem' }}>
        {KATEGORI_TEKST[funn.kategori]}
        {funn.undertype ? ` (${(UNDERTYPE_TEKST[funn.undertype] ?? funn.undertype).toLowerCase()})` : ''}
        {' · '}{statuslinje}
        {funn.juristvurdertAvKi ? ' · Juristvurdert av KI-agent, ikke kontrollert av menneskelig jurist' : ''}
      </Metatekst>
      <FunnTekst tekst={funn.beskrivelse} />
      {funn.kategori === 'utgatt_aktor' && (
        <Paragraph data-size="sm">
          <strong>Etterfølger:</strong>{' '}
          {etterfolgerlinje(funn.etterfolgere, funn.vurderingspunkter, etterfolgerIkkeVurdert)}
        </Paragraph>
      )}
      {funn.bortforklaringer.length > 0 && (
        <>
          <Paragraph data-size="sm" style={{ margin: '0.5rem 0 0.25rem' }}><strong>Bortforklaringer som er prøvd:</strong></Paragraph>
          <ul style={{ margin: '0 0 0.75rem', paddingLeft: '1.25rem' }}>
            {funn.bortforklaringer.map((b, i) => (
              <li key={i}><Paragraph data-size="sm" style={{ margin: 0 }}>{b.overskrift && <em>{b.overskrift}: </em>}{b.tekst}</Paragraph></li>
            ))}
          </ul>
        </>
      )}
      {funn.vurdering && <Paragraph data-size="sm"><strong>Vurdering:</strong> {funn.vurdering}</Paragraph>}
      {vurderingspunkter.length > 0 && (
        <ul style={{ margin: '0 0 0.75rem', paddingLeft: '1.25rem' }}>
          {vurderingspunkter.map((p, i) => (
            <li key={i}><Paragraph data-size="sm" style={{ margin: 0 }}>
              {p.sikkerhet ? `Sikkerhet: ${SIKKERHET_TEKST[p.sikkerhet] ?? p.sikkerhet} ${p.tekst}.` : p.tekst}
            </Paragraph></li>
          ))}
        </ul>
      )}
      {funn.ikkeAvgjort.length > 0 && (
        <>
          <Paragraph data-size="sm" style={{ margin: '0.5rem 0 0.25rem' }}>Ikke avgjort:</Paragraph>
          <ul style={{ margin: '0 0 0.75rem', paddingLeft: '1.25rem' }}>
            {funn.ikkeAvgjort.map((p, i) => <li key={i}><Paragraph data-size="sm" style={{ margin: 0 }}>{p.tekst}</Paragraph></li>)}
          </ul>
        </>
      )}
      {funn.kilder.length > 0 && (
        <>
          <Paragraph data-size="sm" style={{ margin: '0.5rem 0 0.25rem' }}><strong>Kilder:</strong></Paragraph>
          <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
            {funn.kilder.map((k, i) => {
              const lenke = k.lenke ?? lovdataLenke(k.eid ?? k.eli);
              return (
                <li key={i}><Paragraph data-size="sm" style={{ margin: 0 }}>
                  {lenke ? <Link href={lenke} target="_blank" rel="noopener noreferrer">{k.etikett}</Link> : k.etikett}
                </Paragraph></li>
              );
            })}
          </ul>
        </>
      )}
    </div>
  );
}
