import { useState } from 'react';
import {
  Alert, Button, Card, Dialog, Divider, Field, Heading, Label, Radio, Search, Table, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { BrregEnhetDto, NavnekandidatBatchResultatDto, NavnekandidatDto, Navneformgrunn } from '../api/types';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { NavneformgrunnVelger } from '../virksomhet/Navneformgrunn';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { Metatekst } from '../entitet/Metatekst';

type VirksomhetVei = 'eksisterende' | 'brreg' | 'kunNavn';

/**
 * [Ny, issue #299 AC3/AC4] «Behandle gruppen» — ÉN beslutningsflyt for en HEL gruppe navnekandidater
 * (samme `ForeslattTekst`, se `NavnekandidaterListe.tsx` sin `Gruppering==='foreslattTekst'`-visning),
 * i stedet for å åpne enkeltrad-veiviseren (`NavnekandidatVeiviser.tsx`) N ganger for et utfall som
 * uansett blir identisk hver gang — Johanns «Kongen»/«Departementet»/«kommunen»-eksempel (issue #299).
 *
 * <p>
 * <b>Samme steg-FORM som enkeltrad-veiviseren, MYE kortere</b>: «hva slags ting er dette» er IKKE et
 * steg her — kategorien er allerede kjent og UNIFORM for alle radene i gruppen (kalleren sikrer dette
 * FØR dialogen i det hele tatt åpnes, se `kanBehandlesSamlet` i `NavnekandidaterListe.tsx` — en gruppe
 * med blandet kategori må behandles rad for rad i stedet). Det som gjenstår er nøyaktig det
 * enkeltrad-veiviseren kaller «hvilken virksomhet?»/«lovspesifikt eller fast?», pluss én
 * bekreft-handling som appliserer SAMME svar på ALLE radene via de nye batch-endepunktene
 * (`/kobl-til-virksomhet-batch`, `/godkjenn-gruppe-batch`) — se de endepunktenes kommentarer i
 * `Program.cs` for hvorfor «ett begrep, N tagger» IKKE er det samme som å kalle enkeltrad-endepunktet
 * N ganger.
 * </p>
 *
 * <p>
 * «Avvis gruppen» er BEVISST ikke en del av DENNE dialogen — avvisning trenger ingen beslutningsflyt
 * (ingen begrep/virksomhet å velge), og gjenbruker i stedet det EKSISTERENDE `/avvis-batch`-endepunktet
 * direkte fra listesiden (se `avvisGruppe` der) — en egen, enklere bekreftelse er riktigere enn å
 * presse en no-op-flyt inn i denne komponenten.
 * </p>
 */
export interface BehandleGruppeDialogProps {
  /** Gruppetittelen (hyppigste skrivemåte, se `vanligsteSkrivemaate` i NavnekandidaterListe.tsx) —
   * radene kan ha ULIK case, så `rader[0].foreslattTekst` er ikke representativ. */
  visningsnavn: string;
  /** Radene i gruppen som FAKTISK skal behandles — kun status='Venter'. Kategorien kan være BLANDET
   * (KI-klassifiseringen er ustabil på samme tekst): dialogen lar saksbehandleren velge eksplisitt.
   * Garantert ikke-tom av kalleren. */
  rader: NavnekandidatDto[];
  /** «§ nummer — overskrift» (eller rå eId) for DEN REPRESENTATIVE raden — se AC2: den første etter
   * paragraf-/lovreferanse-rekkefølge, ikke bare den første i et vilkårlig array. */
  representantKontekst: string;
  /** Lovens tittel for den representative raden, eller `null` når rettskildelisten ikke er lastet ennå. */
  representantRettskildeTittel: string | null;
  /** `true` når gruppen FAKTISK spenner over flere ulike rettskilder — se AC1s parentes om at
   * identiteten for gruppe/fast begrep er lov- eller term-skalert (issue #298): et lovspesifikt valg på
   * en slik gruppe oppretter/gjenbruker ETT begrep PER lov, ikke ett delt begrep for hele gruppen. */
  flereRettskilder: boolean;
  onLukk: () => void;
  /** Kalt etter en fullført (helt eller delvis) batch — kalleren laster listen på nytt og lukker. */
  onFerdig: () => void;
}

const KATEGORI_TEKST: Record<NavnekandidatDto['kategori'], string> = {
  gruppe: 'Gruppe',
  virksomhet: 'Virksomhet',
  administrativ_inndeling: 'Administrativ inndeling',
};

export function BehandleGruppeDialog({
  visningsnavn, rader, representantKontekst, representantRettskildeTittel, flereRettskilder, onLukk, onFerdig,
}: BehandleGruppeDialogProps) {
  const foreslattTekst = visningsnavn;

  // Antall rader per kategori, mest vanlige først. Blandet kategori er normalen for de største gruppene
  // («kommunen», «statsforvalteren»): saksbehandleren avgjør EN gang, og utfallet appliseres på alle.
  // `administrativ_inndeling` tilbys kun når den FAKTISK finnes blant radene — aldri som stille
  // sammenslåingsmål.
  const kategoriAntall = [...rader.reduce((m, r) => m.set(r.kategori, (m.get(r.kategori) ?? 0) + 1),
    new Map<NavnekandidatDto['kategori'], number>())].sort((a, b) => b[1] - a[1]);
  const erBlandet = kategoriAntall.length > 1;
  const [kategori, setKategori] = useState<NavnekandidatDto['kategori']>(kategoriAntall[0][0]);
  const antallSomEndrerKategori = rader.filter((r) => r.kategori !== kategori).length;
  const { virksomheter, oppdater: oppdaterVirksomheter } = useVirksomheter();

  // ---------- Virksomhet-sporet (kategori==='virksomhet') ----------
  const [vei, setVei] = useState<VirksomhetVei>('eksisterende');
  const [valgtVirksomhetId, setValgtVirksomhetId] = useState('');
  const [navneformgrunn, setNavneformgrunn] = useState<Navneformgrunn | null>(null);
  const [brregSok, setBrregSok] = useState(foreslattTekst);
  const [brregTreff, setBrregTreff] = useState<BrregEnhetDto[] | null>(null);
  const [brregSoker, setBrregSoker] = useState(false);
  const [nyttNavn, setNyttNavn] = useState(foreslattTekst);
  const [oppretterVirksomhet, setOppretterVirksomhet] = useState(false);

  // ---------- Gruppe-sporet (kategori==='gruppe') — issue #298 AC3, speil av veiviserens steg 2. ----------
  const [gruppeScope, setGruppeScope] = useState<'lovspesifikt' | 'fast'>('lovspesifikt');

  // ---------- Felles ----------
  const [kjorer, setKjorer] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [resultat, setResultat] = useState<{ ok: number; feilet: number; feilmeldinger: string[] } | null>(null);

  async function søkBrreg() {
    if (!brregSok.trim()) return;
    setBrregSoker(true);
    setFeil(null);
    try {
      setBrregTreff(await api.sokBrreg(brregSok.trim()));
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Brreg-søket feilet.');
      setBrregTreff(null);
    } finally {
      setBrregSoker(false);
    }
  }

  async function opprettOgVelg(fra: 'brreg' | 'kunNavn', organisasjonsnummer?: string) {
    setOppretterVirksomhet(true);
    setFeil(null);
    try {
      const ny = fra === 'brreg'
        ? await api.opprettVirksomhetFraBrreg(organisasjonsnummer!)
        : await api.opprettVirksomhet({ navn: nyttNavn.trim(), overordnetEnhetId: null });
      await oppdaterVirksomheter();
      setValgtVirksomhetId(ny.id);
      setVei('eksisterende');
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke opprette virksomheten.');
    } finally {
      setOppretterVirksomhet(false);
    }
  }

  function settResultatFraSvar(svar: NavnekandidatBatchResultatDto) {
    const feilede = svar.rader.filter((r) => !r.ok);
    setResultat({
      ok: svar.rader.length - feilede.length,
      feilet: feilede.length,
      feilmeldinger: feilede.map((r) => `${r.id}: ${r.feil ?? 'Ukjent feil'}`),
    });
  }

  async function bekreftVirksomhet() {
    if (!valgtVirksomhetId) return;
    setKjorer(true);
    setFeil(null);
    try {
      settResultatFraSvar(await api.koblNavnekandidaterTilVirksomhetBatch({
        ider: rader.map((r) => r.id), virksomhetId: valgtVirksomhetId, navneformgrunn,
        omkategoriser: antallSomEndrerKategori > 0,
      }));
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved behandling av gruppen.');
    } finally {
      setKjorer(false);
    }
  }

  async function bekreftGruppe() {
    setKjorer(true);
    setFeil(null);
    try {
      settResultatFraSvar(await api.godkjennNavnekandidaterGruppeBatch({
        ider: rader.map((r) => r.id), fast: gruppeScope === 'fast',
        tilKategori: antallSomEndrerKategori > 0 ? 'gruppe' : undefined,
      }));
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved behandling av gruppen.');
    } finally {
      setKjorer(false);
    }
  }

  async function bekreftAdministrativInndeling() {
    setKjorer(true);
    setFeil(null);
    try {
      settResultatFraSvar(await api.godkjennNavnekandidaterGruppeBatch({
        ider: rader.map((r) => r.id), fast: false,
        tilKategori: antallSomEndrerKategori > 0 ? 'administrativ_inndeling' : undefined,
      }));
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved behandling av gruppen.');
    } finally {
      setKjorer(false);
    }
  }

  const valgtVirksomhet = virksomheter.find((v) => v.id === valgtVirksomhetId) ?? null;
  const klarTilBekreft = kategori !== 'virksomhet' || valgtVirksomhetId !== '';

  return (
    <Dialog open onClose={onLukk} closeButton="Lukk" style={{ maxWidth: '38rem' }}>
      <Dialog.Block>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>
          Behandle gruppen «{foreslattTekst}»
        </Heading>
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
          {rader.length} kandidat{rader.length === 1 ? '' : 'er'} med nøyaktig denne teksten
          {flereRettskilder
            ? ', på tvers av flere lover/forskrifter'
            : representantRettskildeTittel ? ` i ${representantRettskildeTittel}` : ''}.
          {' '}Alle appliseres SAMME utfall ved bekreftelse — ett begrep-/virksomhetsvalg, ikke {rader.length}.
        </Metatekst>
        <Card style={{ padding: '0.6rem 0.75rem', marginBottom: '1rem', background: 'var(--ds-color-neutral-surface-tinted)' }}>
          <Metatekst as="span" style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
            Representativ kontekst (første etter lovreferanse): {representantKontekst}
          </Metatekst>
        </Card>
      </Dialog.Block>

      {!resultat && (
        <Dialog.Block>
          {erBlandet && (
            <div style={{ marginBottom: '0.75rem' }}>
              <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.5rem' }}>
                Gruppen har blandet kategori ({kategoriAntall.map(([k, n]) => `${n} ${k.replace('_', ' ')}`).join(', ')}) —
                KI-klassifiseringen er ustabil på samme tekst. Velg ÉN kategori for hele gruppen.
              </Alert>
              <Field data-size="sm">
                <Label>Behandle alle som:</Label>
                {kategoriAntall.map(([k, n]) => (
                  <Radio key={k} name="behandleSom" value={k}
                    label={`${KATEGORI_TEKST[k]} (${n} av ${rader.length} er det i dag)`}
                    checked={kategori === k} onChange={() => setKategori(k)} />
                ))}
              </Field>
              {antallSomEndrerKategori > 0 && (
                <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.4rem' }}>
                  {antallSomEndrerKategori} rad{antallSomEndrerKategori === 1 ? '' : 'er'} endrer kategori til
                  {' '}«{KATEGORI_TEKST[kategori]}» ved bekreftelse (de står som «Venter», teksten røres ikke).
                </Metatekst>
              )}
              <Divider style={{ margin: '0.75rem 0' }} />
            </div>
          )}
          {kategori === 'virksomhet' && (
            <>
              <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
                <Radio name="vei" label="Velg fra katalogen" value="eksisterende"
                  checked={vei === 'eksisterende'} onChange={() => setVei('eksisterende')} />
                <Radio name="vei" label="Søk i Brønnøysundregisteret og opprett" value="brreg"
                  checked={vei === 'brreg'} onChange={() => setVei('brreg')} />
                <Radio name="vei" label="Opprett med bare navn" value="kunNavn"
                  checked={vei === 'kunNavn'} onChange={() => setVei('kunNavn')} />
              </Field>

              {vei === 'eksisterende' && (
                <VirksomhetVelger
                  virksomheter={virksomheter}
                  value={valgtVirksomhetId}
                  onChange={setValgtVirksomhetId}
                  label="Virksomhet"
                  tomValgTekst="Velg virksomhet …"
                  style={{ marginBottom: '0.75rem', maxWidth: '28rem' }}
                />
              )}

              {vei === 'brreg' && (
                <div style={{ marginBottom: '0.75rem' }}>
                  <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '0.5rem', maxWidth: '28rem' }}>
                    <Search data-size="sm" style={{ flex: 1 }}>
                      <Search.Input
                        aria-label="Søk i Brønnøysundregisteret"
                        value={brregSok}
                        onChange={(e) => setBrregSok(e.target.value)}
                        onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void søkBrreg(); } }}
                      />
                      <Search.Clear />
                    </Search>
                    <Button data-size="sm" onClick={søkBrreg} disabled={brregSoker || !brregSok.trim()}>
                      {brregSoker ? 'Søker …' : 'Søk'}
                    </Button>
                  </div>
                  {brregTreff !== null && (
                    <Card style={{ padding: brregTreff.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
                      {brregTreff.length === 0 ? (
                        <Metatekst style={{ margin: 0, padding: '0.5rem' }}>Ingen treff i Brreg på «{brregSok}».</Metatekst>
                      ) : (
                        <Table data-size="sm" data-density="compact" style={{ width: '100%' }}>
                          <Table.Head>
                            <Table.Row>
                              <Table.HeaderCell>Navn</Table.HeaderCell>
                              <Table.HeaderCell>Org.nr.</Table.HeaderCell>
                              <Table.HeaderCell />
                            </Table.Row>
                          </Table.Head>
                          <Table.Body>
                            {brregTreff.map((e) => (
                              <Table.Row key={e.organisasjonsnummer}>
                                <Table.Cell>{e.navn}</Table.Cell>
                                <Table.Cell>{e.organisasjonsnummer}</Table.Cell>
                                <Table.Cell>
                                  <Button data-size="sm" variant="secondary"
                                    onClick={() => opprettOgVelg('brreg', e.organisasjonsnummer)}
                                    disabled={oppretterVirksomhet}>
                                    Opprett og velg
                                  </Button>
                                </Table.Cell>
                              </Table.Row>
                            ))}
                          </Table.Body>
                        </Table>
                      )}
                    </Card>
                  )}
                </div>
              )}

              {vei === 'kunNavn' && (
                <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
                  <Textfield data-size="sm" label="Navn på virksomheten" value={nyttNavn}
                    onChange={(e) => setNyttNavn(e.target.value)} />
                  <Button data-size="sm" variant="secondary" onClick={() => opprettOgVelg('kunNavn')}
                    disabled={oppretterVirksomhet || !nyttNavn.trim()}>
                    {oppretterVirksomhet ? 'Oppretter …' : 'Opprett og velg'}
                  </Button>
                </div>
              )}

              {valgtVirksomhet && (
                <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.75rem' }}>
                  Valgt virksomhet: <strong>{valgtVirksomhet.visningsnavn}</strong> — kobles til ALLE
                  {' '}{rader.length} forekomstene.
                </Alert>
              )}

              <Divider style={{ margin: '0.75rem 0' }} />
              <NavneformgrunnVelger
                value={navneformgrunn}
                onChange={setNavneformgrunn}
                label="Begrunnelse (felles for alle forekomstene)"
                style={{ marginBottom: '0.5rem', maxWidth: '28rem' }}
              />
            </>
          )}

          {kategori === 'gruppe' && (
            <>
              <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
                Et lovspesifikt gruppebegrep hører til denne/disse loven(e) — samme navn i en annen lov
                blir en egen rad. Et fast, nasjonalt begrep har ingen lov å høre til, f.eks. «Kongen»
                (issue #298).
                {flereRettskilder && ' Lovspesifikt her oppretter/gjenbruker ÉN rad PER lov gruppen spenner over, ikke én delt rad for hele gruppen.'}
              </Metatekst>
              <Field data-size="sm">
                <Radio name="gruppeScope" label="Lovspesifikt gruppebegrep" value="lovspesifikt"
                  checked={gruppeScope === 'lovspesifikt'} onChange={() => setGruppeScope('lovspesifikt')} />
                <Radio name="gruppeScope" label="Fast, nasjonalt begrep" value="fast"
                  checked={gruppeScope === 'fast'} onChange={() => setGruppeScope('fast')} />
              </Field>
            </>
          )}

          {kategori === 'administrativ_inndeling' && (
            <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
              Administrativ inndeling er alltid lovspesifikt (ingen fast/nasjonal variant, issue #298) —
              ett begrep opprettes/gjenbrukes per lov gruppen spenner over, og kobles til alle
              {' '}{rader.length} forekomstene.
            </Metatekst>
          )}

          {feil && <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.75rem' }}>{feil}</Alert>}
        </Dialog.Block>
      )}

      {resultat && (
        <Dialog.Block>
          <Alert data-color={resultat.feilet === 0 ? 'success' : 'warning'} style={{ marginBottom: '0.5rem' }}>
            {resultat.ok} av {resultat.ok + resultat.feilet} kandidat{resultat.ok + resultat.feilet === 1 ? '' : 'er'} godkjent.
            {resultat.feilet > 0 && ` ${resultat.feilet} feilet — de står fortsatt som «Venter» og kan behandles på nytt (enkeltvis eller i en ny gruppehandling).`}
          </Alert>
          {resultat.feilmeldinger.length > 0 && (
            <ul style={{ margin: '0 0 0 1.1rem', padding: 0 }}>
              {resultat.feilmeldinger.map((m) => (
                <Metatekst as="li" key={m} style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>{m}</Metatekst>
              ))}
            </ul>
          )}
        </Dialog.Block>
      )}

      <Dialog.Block style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
        {resultat ? (
          <Button data-size="sm" onClick={onFerdig}>Lukk og oppdater listen</Button>
        ) : (
          <>
            <Button data-size="sm" variant="tertiary" onClick={onLukk} disabled={kjorer}>Avbryt</Button>
            <Button
              data-size="sm"
              disabled={kjorer || !klarTilBekreft}
              onClick={
                kategori === 'virksomhet' ? bekreftVirksomhet
                  : kategori === 'gruppe' ? bekreftGruppe
                    : bekreftAdministrativInndeling
              }
            >
              {kjorer ? 'Behandler …' : `Bekreft for alle ${rader.length}`}
            </Button>
          </>
        )}
      </Dialog.Block>
    </Dialog>
  );
}
