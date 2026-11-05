import { useEffect, useRef, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Details, Field, Fieldset, Heading, Label, Radio, Select, Textarea } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { AktorkoDetalj, AktorkoKvittering, Aktorkotype, GeneriskOmtaleRapport, VirksomhetDto } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { NAVNEFORMGRUNN_VALG } from '../virksomhet/Navneformgrunn';
import { KUNNSKAPSSTATUS_VISNING } from '../strukturkant/Utsagnsstatus';
import type { Kunnskapsstatus } from '../api/types';
import {
  AKTORKOTYPE_TEKST, TOMT_UTKAST, forslagNokkel, godkjenningMangler, grunnvarsel, skrivemater, tilForesporsel, type GodkjenningUtkast,
} from './aktorkoGodkjenning';
import { AKTORKORUTE_TEKST } from './aktorkorute';

/**
 * [Ny, issue #440 PR 2, Johanns avgjørelser 2026-10-30 og 2026-11-01] Skrivingen i aktørkøen (docs/09 §50): godkjenningen som ett skjema,
 * og de andre handlingene i en lukket `Details`. Hver handling er én transaksjon i API-et; kvitteringen er veiviserens «Dette skjedde»
 * (docs/09 §15).
 *
 * <p>Radioknappene står i `Fieldset` med `Fieldset.Legend`, ikke i `Field`: i en `Field` peker alle etikettene på den første knappen
 * (målt i kaldsjekken 2026-11-05: et klikk på «Klasse» valgte «Aktør»). Samme mønster som `GeneriskEnSkjerm` (docs/09 §50).</p>
 *
 * <p><b>Ingenting er forhåndsvalgt</b> (#440 AK5): typen, skrivemåten, hjemmelen og hvor sikkert det er, velges aktivt. Knappen er av med
 * `Metatekst` som sier hva som mangler. Reglene står i `aktorkoGodkjenning.ts` (med vitest).</p>
 *
 * <p>[ENDRET, #440, Johanns avgjørelse 2026-11-05] «Godta forslaget» i forhåndsklassifiseringen (`AktorkoForhandKort.tsx`) fyller ut skjemaene
 * her (`forslag`-propene), og et menneske trykker på skjemaets egen knapp. Det er fortsatt ingen stille godkjenning: ingenting er fylt ut før
 * noen har trykket «Godta forslaget», og ingenting lagres før skjemaets knapp. Det som ikke kunne fylles ut, står som merknad.</p>
 */

/** Utkastet «Godta forslaget» fyller godkjenningen med. `nr` øker ved hvert klikk, så samme forslag kan fylles ut på nytt. */
export interface ForslagTilGodkjenning { nr: number; utkast: GodkjenningUtkast; merknader: string[] }

/** Det «Godta forslaget» fyller de andre handlingene med. */
export interface ForslagTilAndre {
  nr: number; handling: 'koble' | 'ikke_aktor' | 'generisk_omtale';
  aktorId?: string; navn?: string | null; grunn?: string; begrunnelse?: string; ord?: string; merknader: string[];
}

/** Boksen som sier at skjemaet er fylt ut fra forslaget, og hva som ikke kunne fylles ut. `info`: et forslag ingen har vurdert (§50). */
function FyltUt({ merknader, hva }: { merknader: string[]; hva: string }) {
  return (
    <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.6rem' }}>
      Fylt ut fra forhåndsklassifiseringen: {hva}. Se over, og bekreft med knappen under. Ingenting er lagret ennå.
      {merknader.length > 0 && (
        <ul style={{ margin: '0.3rem 0 0 1.1rem', padding: 0 }}>{merknader.map((m) => <li key={m}>{m}</li>)}</ul>
      )}
    </Alert>
  );
}

function feiltekst(e: unknown, standard: string) {
  return e instanceof ApiError ? e.message : standard;
}

/** «Dette skjedde» etter en skrivende handling. */
export function Kvittering({ k }: { k: AktorkoKvittering }) {
  return (
    <Card style={{ padding: '0.75rem', marginTop: '0.5rem' }} data-color="neutral">
      <Alert data-color="success" data-size="sm" style={{ marginBottom: '0.5rem' }}>
        {k.handling === 'godkjent' ? 'Godkjent.' : k.handling === 'ikke_aktor' ? 'Avvist som «ikke en aktør».'
          : k.handling === 'koblet' ? 'Koblet til aktøren.' : 'Grupperingen er endret.'}
      </Alert>
      {k.varsler.map((v) => (
        <Alert key={v} data-color="warning" data-size="sm" style={{ marginBottom: '0.5rem' }}>{v}</Alert>
      ))}
      <Heading level={4} data-size="2xs" style={{ marginBottom: '0.3rem' }}>Dette skjedde</Heading>
      <ul style={{ margin: '0 0 0.5rem 1.1rem', padding: 0 }}>
        {k.linjer.map((l) => <Metatekst as="li" key={l}>{l}</Metatekst>)}
      </ul>
      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
        {k.virksomhetId && (
          <Button data-size="sm" variant="secondary" asChild>
            <RouterLink to={`/virksomheter/${k.virksomhetId}`}>Se aktøren ↗</RouterLink>
          </Button>
        )}
        {k.begrepId && (
          <Button data-size="sm" variant="secondary" asChild>
            <RouterLink to={`/begreper/${k.begrepId}`}>Se begrepet ↗</RouterLink>
          </Button>
        )}
      </div>
    </Card>
  );
}

/** Godkjenningsskjemaet: aktør eller klasse/rolle/område, navneform med hjemmel eller «hjemmel mangler», og tagger for alle forekomstene. */
export function Godkjenningsskjema({ detalj, onFerdig, forslag }: {
  detalj: AktorkoDetalj; onFerdig: (k: AktorkoKvittering) => void; forslag?: ForslagTilGodkjenning;
}) {
  const [u, setU] = useState<GodkjenningUtkast>(TOMT_UTKAST);
  const kort = useRef<HTMLDivElement>(null);
  // [Ny, #440] «Godta forslaget»: utkastet erstatter det som står, og skjemaet rulles fram. Ingenting sendes.
  useEffect(() => {
    if (!forslag) return;
    setU(forslag.utkast);
    kort.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [forslag?.nr]); // eslint-disable-line react-hooks/exhaustive-deps
  const [sender, setSender] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const sett = (endring: Partial<GodkjenningUtkast>) => setU((x) => ({ ...x, ...endring }));
  const n = detalj.nokkel;
  const mangler = godkjenningMangler(u);
  const valgtForslag = detalj.hjemmelforslag.find((f) => forslagNokkel(f) === u.hjemmel) ?? null;

  async function send() {
    const body = tilForesporsel(u, detalj);
    if (!body) return;
    setSender(true);
    setFeil(null);
    try {
      onFerdig(await api.godkjennAktorkoNavn(detalj.nokkel, body));
    } catch (e) {
      setFeil(feiltekst(e, 'Godkjenningen feilet. Ingenting er lagret.'));
    } finally {
      setSender(false);
    }
  }

  return (
    <Card ref={kort} style={{ padding: '0.75rem', marginTop: '0.75rem' }} data-color="neutral">
      <Heading level={4} data-size="2xs" style={{ marginBottom: '0.2rem' }}>Godkjenn</Heading>
      <Metatekst style={{ marginBottom: '0.6rem' }}>
        Én godkjenning oppretter {u.type ? AKTORKOTYPE_TEKST[u.type].bestemt.toLowerCase() : 'aktøren eller begrepet'}, navnet med hjemmel eller
        «hjemmel mangler», og tagger for alle {detalj.forekomster} forekomstene, i én transaksjon. Ingenting er forhåndsvalgt
        {detalj.forhandsklassifisering && detalj.forhandsklassifisering.handling === 'godkjenn' ? '; «Godta forslaget» over fyller ut skjemaet' : ''}.
      </Metatekst>
      {forslag && <FyltUt merknader={forslag.merknader} hva="typen, navnet, hjemmelen, hvor den gjelder, «faglig vurdert» og begrunnelsen" />}

      <Fieldset data-size="sm" style={{ marginBottom: '0.6rem' }}>
        <Fieldset.Legend>Hva er «{detalj.navn}»?</Fieldset.Legend>
        {(Object.keys(AKTORKOTYPE_TEKST) as Aktorkotype[]).map((t) => (
          <Radio key={t} name={`aktorko-type-${n}`} value={t} label={AKTORKOTYPE_TEKST[t].tekst} description={AKTORKOTYPE_TEKST[t].beskrivelse}
            checked={u.type === t} onChange={() => sett({ type: t, gjelder: null })} />
        ))}
      </Fieldset>

      <Fieldset data-size="sm" style={{ marginBottom: '0.6rem' }}>
        <Fieldset.Legend>Navnet, slik det står i teksten</Fieldset.Legend>
        {skrivemater(detalj).map((s) => (
          <Radio key={s} name={`aktorko-navn-${n}`} value={s} label={`«${s}»`} checked={u.navn === s} onChange={() => sett({ navn: s })} />
        ))}
      </Fieldset>

      <Fieldset data-size="sm" style={{ marginBottom: '0.6rem' }}>
        <Fieldset.Legend>Hjemmel for at {u.type ? AKTORKOTYPE_TEKST[u.type].bestemt.toLowerCase() : 'den'} finnes</Fieldset.Legend>
        {detalj.hjemmelforslag.map((f) => (
          <Radio key={forslagNokkel(f)} name={`aktorko-hjemmel-${n}`} value={forslagNokkel(f)}
            label={`${f.kortnavn ?? f.rettskilde} ${f.bestemmelse ?? f.eid}`}
            description={`Forslag etter regelen («${f.uttrykk}»), ${KUNNSKAPSSTATUS_VISNING[f.kunnskapsstatus as Kunnskapsstatus]?.tekst.toLowerCase() ?? f.kunnskapsstatus}. `
              + 'Hjemmelens art blir «oppretting».' + (f.annenParagraf ? ` ${f.annenParagraf.charAt(0).toUpperCase()}${f.annenParagraf.slice(1)}.` : '')}
            checked={u.hjemmel === forslagNokkel(f)} onChange={() => sett({ hjemmel: forslagNokkel(f), gjelder: null })} />
        ))}
        <Radio name={`aktorko-hjemmel-${n}`} value="mangler" label="Hjemmel mangler"
          description="Lagres som uavklart, med «ikke funnet av regelen; andre lover og vedtekter er ikke søkt». En annen hjemmel registreres på aktørsiden."
          checked={u.hjemmel === 'mangler'} onChange={() => sett({ hjemmel: 'mangler' })} />
      </Fieldset>

      {u.type && u.type !== 'aktor' && u.hjemmel === 'mangler' && (
        <Fieldset data-size="sm" style={{ marginBottom: '0.6rem' }}>
          <Fieldset.Legend>Hvor gjelder {AKTORKOTYPE_TEKST[u.type].bestemt.toLowerCase()}?</Fieldset.Legend>
          {detalj.perRettskilde.map((r) => (
            <Radio key={r.rettskildeId} name={`aktorko-gjelder-${n}`} value={r.rettskildeId} label={r.kortnavn ?? r.tittel}
              checked={u.gjelder === r.rettskildeId} onChange={() => sett({ gjelder: r.rettskildeId })} />
          ))}
          <Radio name={`aktorko-gjelder-${n}`} value="fast" label="Hele korpuset (fast begrep)"
            checked={u.gjelder === 'fast'} onChange={() => sett({ gjelder: 'fast' })} />
        </Fieldset>
      )}
      {u.type && u.type !== 'aktor' && valgtForslag && (
        <Metatekst style={{ marginBottom: '0.6rem' }}>
          {AKTORKOTYPE_TEKST[u.type].bestemt} gjelder i {valgtForslag.kortnavn ?? valgtForslag.rettskilde}, der hjemmelen står.
        </Metatekst>
      )}

      <Fieldset data-size="sm" style={{ marginBottom: '0.6rem' }}>
        <Fieldset.Legend>Hvor sikkert er det?</Fieldset.Legend>
        <Radio name={`aktorko-godkjenning-${n}`} value="faglig_vurdert" label="Faglig vurdert"
          description="Vurdert av deg, men ikke godkjent. Står som forslag til noen godkjenner det."
          checked={u.godkjenning === 'faglig_vurdert'} onChange={() => sett({ godkjenning: 'faglig_vurdert' })} />
        <Radio name={`aktorko-godkjenning-${n}`} value="godkjent" label="Godkjent" description="Gjelder fra nå."
          checked={u.godkjenning === 'godkjent'} onChange={() => sett({ godkjenning: 'godkjent' })} />
      </Fieldset>
      <Textarea data-size="sm" aria-label="Begrunnelse (valgfri)" placeholder="Begrunnelse (valgfri)" rows={2} value={u.begrunnelse}
        onChange={(e) => sett({ begrunnelse: e.target.value })} style={{ maxWidth: '36rem', marginBottom: '0.5rem' }} />

      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.5rem' }}>{feil}</Alert>}
      <div style={{ display: 'flex', gap: '0.6rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <Button data-size="sm" disabled={mangler !== null || sender} onClick={send}>
          {sender ? 'Lagrer …' : `Godkjenn «${u.navn ?? detalj.navn}»`}
        </Button>
        {mangler && <Metatekst as="span">{mangler}</Metatekst>}
      </div>
    </Card>
  );
}

/** «Ikke en aktør», «Koble til en aktør som finnes» og «Tagg alle som generisk omtale», i en lukket `Details`. */
export function AndreHandlinger({ detalj, onFerdig, forslag }: {
  detalj: AktorkoDetalj; onFerdig: (k: AktorkoKvittering) => void; forslag?: ForslagTilAndre;
}) {
  const n = detalj.nokkel;
  const [begrunnelse, setBegrunnelse] = useState('');
  const [virksomheter, setVirksomheter] = useState<VirksomhetDto[] | null>(null);
  const [aktorId, setAktorId] = useState('');
  const [navn, setNavn] = useState<string | null>(null);
  const [grunn, setGrunn] = useState('');
  const [feil, setFeil] = useState<string | null>(null);
  const [sender, setSender] = useState(false);
  const [apnet, setApnet] = useState(false);
  // [ENDRET, #440] Styrt åpen/lukket, så «Godta forslaget» kan åpne den.
  const [apen, setApen] = useState(false);
  const boks = useRef<HTMLDivElement>(null);

  // [Ny, #440] «Godta forslaget»: fyller ut handlingen forslaget gjelder, åpner boksen og ruller dit. Ingenting sendes.
  useEffect(() => {
    if (!forslag) return;
    setApen(true);
    setApnet(true);
    if (forslag.handling === 'koble') {
      setAktorId(forslag.aktorId ?? '');
      setNavn(forslag.navn ?? null);
      setGrunn(forslag.grunn ?? '');
    } else if (forslag.handling === 'ikke_aktor') {
      setBegrunnelse(forslag.begrunnelse ?? '');
    }
    boks.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [forslag?.nr]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (apnet && virksomheter === null) api.hentVirksomheter().then((v) => setVirksomheter(v.filter((x) => !x.erstattetAvId))).catch(() => setVirksomheter([]));
  }, [apnet, virksomheter]);

  async function kjor(handling: () => Promise<AktorkoKvittering>, standard: string) {
    setSender(true);
    setFeil(null);
    try {
      onFerdig(await handling());
    } catch (e) {
      setFeil(feiltekst(e, standard));
    } finally {
      setSender(false);
    }
  }

  const aktor = virksomheter?.find((v) => v.id === aktorId) ?? null;
  const varsel = navn && aktor ? grunnvarsel(grunn || null, navn, aktor.navn) : null;

  return (
    <div ref={boks}>
    <Details style={{ marginTop: '0.75rem' }} open={apen}
      onToggle={(e) => { const o = (e.target as HTMLDetailsElement).open; setApen(o); if (o) setApnet(true); }}>
      <Details.Summary>Andre handlinger: ikke en aktør, koble til en aktør som finnes, generisk omtale</Details.Summary>
      <Details.Content>
        {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.5rem' }}>{feil}</Alert>}
        {forslag && (
          <FyltUt merknader={forslag.merknader}
            hva={forslag.handling === 'koble' ? '«Koble til en aktør som finnes»' : forslag.handling === 'ikke_aktor' ? '«Ikke en aktør» med begrunnelsen'
              : `tørrkjøringen av «Tagg alle som generisk omtale» for «${forslag.ord}»`} />
        )}

        <Heading level={5} data-size="2xs" style={{ marginBottom: '0.3rem' }}>Ikke en aktør</Heading>
        <Metatekst style={{ marginBottom: '0.3rem' }}>Alle {detalj.forekomster} forekomstene avvises med begrunnelsen. Radene slettes ikke.</Metatekst>
        <Textarea data-size="sm" aria-label="Hvorfor er dette ikke en aktør?" placeholder="Hvorfor er dette ikke en aktør? (påkrevd)" rows={2}
          value={begrunnelse} onChange={(e) => setBegrunnelse(e.target.value)} style={{ maxWidth: '36rem', marginBottom: '0.4rem' }} />
        <Button data-size="sm" variant="secondary" disabled={!begrunnelse.trim() || sender}
          onClick={() => kjor(() => api.aktorkoIkkeAktor(n, begrunnelse.trim(), detalj.forekomster), 'Avvisningen feilet. Ingenting er lagret.')}>
          Avvis som «ikke en aktør»
        </Button>

        <Heading level={5} data-size="2xs" style={{ margin: '1rem 0 0.3rem' }}>Koble til en aktør som finnes</Heading>
        <Metatekst style={{ marginBottom: '0.3rem' }}>Navnet blir ny navneform på aktøren, og alle forekomstene tagges mot den.</Metatekst>
        {virksomheter === null ? (
          <Metatekst>Laster aktørene …</Metatekst>
        ) : (
          <VirksomhetVelger virksomheter={virksomheter} value={aktorId} onChange={setAktorId} label="Aktør" tomValgTekst="Velg aktør …"
            style={{ maxWidth: '28rem', marginBottom: '0.5rem' }} />
        )}
        <Fieldset data-size="sm" style={{ marginBottom: '0.5rem' }}>
          <Fieldset.Legend>Navnet, slik det står i teksten</Fieldset.Legend>
          {skrivemater(detalj).map((s) => (
            <Radio key={s} name={`aktorko-koble-navn-${n}`} value={s} label={`«${s}»`} checked={navn === s} onChange={() => setNavn(s)} />
          ))}
        </Fieldset>
        <Field data-size="sm" style={{ maxWidth: '22rem', marginBottom: '0.5rem' }}>
          <Label>Grunn til at navneformen peker dit</Label>
          <Select data-size="sm" value={grunn} onChange={(e) => setGrunn(e.target.value)}>
            <Select.Option value="">Velg grunn …</Select.Option>
            {NAVNEFORMGRUNN_VALG.map((v) => <Select.Option key={v.verdi} value={v.verdi}>{v.label}</Select.Option>)}
          </Select>
        </Field>
        {varsel && <Alert data-color="warning" data-size="sm" style={{ marginBottom: '0.5rem', maxWidth: '40rem' }}>{varsel}</Alert>}
        {aktor?.livsstatus === 'nedlagt' && (
          <Alert data-color="warning" data-size="sm" style={{ marginBottom: '0.5rem', maxWidth: '40rem' }}>
            {aktor.navn} er nedlagt. Forekomstene vil peke på en aktør som ikke finnes i dag.
          </Alert>
        )}
        <Button data-size="sm" variant="secondary" disabled={!aktorId || !navn || !grunn || sender}
          onClick={() => kjor(() => api.aktorkoKoble(n, { virksomhetId: aktorId, navn: navn!, navneformgrunn: grunn, forventetForekomster: detalj.forekomster }),
            'Koblingen feilet. Ingenting er lagret.')}>
          Koble {navn ? `«${navn}»` : 'navnet'} til {aktor?.navn ?? 'aktøren'}
        </Button>

        <Heading level={5} data-size="2xs" style={{ margin: '1rem 0 0.3rem' }}>Tagg alle som generisk omtale</Heading>
        <Metatekst style={{ marginBottom: '0.3rem' }}>
          Per ord, og bare nøyaktig ordet (ikke bøyningsformer). Tørrkjøringen viser hva som ville skjedd; «Lagre» er et eget klikk.
        </Metatekst>
        {detalj.varianter.map((v) => (
          <GeneriskOmtale key={v.normalisert} ord={v.normalisert} onFerdig={onFerdig}
            torrkjorVed={forslag?.handling === 'generisk_omtale' && forslag.ord === v.normalisert ? forslag.nr : undefined} />
        ))}
      </Details.Content>
    </Details>
    </div>
  );
}

/** Tørrkjøring og lagring av «Tagg alle som generisk omtale» for ett ord (Johanns avgjørelse 2026-10-30). Også brukt i fordelingen. */
export function GeneriskOmtale({ ord, onFerdig, onLagret, torrkjorVed }: {
  ord: string; onFerdig?: (k: AktorkoKvittering) => void; onLagret?: (r: GeneriskOmtaleRapport) => void;
  /** [Ny, #440] «Godta forslaget» tørrkjører (skriver ingenting). Lagringen er fortsatt et eget klikk. */
  torrkjorVed?: number;
}) {
  const [torr, setTorr] = useState<GeneriskOmtaleRapport | null>(null);
  const [lagret, setLagret] = useState<GeneriskOmtaleRapport | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [sender, setSender] = useState(false);

  async function kjor(skriv: boolean) {
    setSender(true);
    setFeil(null);
    try {
      const r = await api.aktorkoGeneriskOmtale(ord, skriv);
      if (skriv) {
        setLagret(r);
        onLagret?.(r);
        onFerdig?.({
          handling: 'godkjent', nokkel: ord, forekomster: r.forekomster, tagget: r.tagget, ikkeTagget: r.ikkeTagget, varsler: [],
          linjer: [`«${ord}»: ${r.tagget} av ${r.forekomster} forekomster er tagget som generisk omtale og godkjent.`],
          virksomhetId: null, virksomhetNavn: null, begrepId: null, navneformId: null, hjemmelArt: null,
        });
      } else {
        setTorr(r);
      }
    } catch (e) {
      setFeil(feiltekst(e, 'Kallet feilet. Ingenting er lagret.'));
    } finally {
      setSender(false);
    }
  }

  useEffect(() => {
    if (torrkjorVed !== undefined && !lagret) void kjor(false);
  }, [torrkjorVed]); // eslint-disable-line react-hooks/exhaustive-deps

  const utenfor = torr ? Object.entries(torr.utenforPerRute) : [];
  return (
    <div style={{ marginBottom: '0.5rem' }}>
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <span>«{ord}»</span>
        {!lagret && (
          <Button data-size="sm" variant="tertiary" disabled={sender} onClick={() => kjor(false)}>Tørrkjør</Button>
        )}
        {torr && !lagret && !torr.sperre && torr.forekomster > 0 && (
          <Button data-size="sm" variant="secondary" disabled={sender} onClick={() => kjor(true)}>
            Lagre: tagg {torr.forekomster} som generisk omtale
          </Button>
        )}
      </div>
      {feil && <Alert data-color="danger" data-size="sm">{feil}</Alert>}
      {torr && !lagret && (
        torr.sperre ? (
          <Alert data-color="warning" data-size="sm" style={{ maxWidth: '40rem' }}>{torr.sperre}</Alert>
        ) : (
          <Metatekst>
            Tørrkjøring: {torr.forekomster} forekomster i {torr.rettskilder} rettskilder ville blitt tagget mot den generiske navneformen
            «{torr.ord}» ({torr.navneformFinnes ? 'finnes' : 'opprettes'}): {torr.tagget} tagget, {torr.ikkeTagget} uten tagg (ingen eier eller
            endret tekst).{utenfor.length > 0 && ` Ikke med: ${utenfor.map(([r, a]) => `${a} på ruten «${AKTORKORUTE_TEKST[r as keyof typeof AKTORKORUTE_TEKST] ?? r}»`).join(', ')}.`}
            {' '}Ingen binding: svaret per rettskilde kommer fra standardregelen eller en binding.
          </Metatekst>
        )
      )}
      {lagret && <Metatekst>Lagret: {lagret.tagget} av {lagret.forekomster} forekomster er tagget og godkjent.</Metatekst>}
    </div>
  );
}
