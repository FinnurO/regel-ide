#!/usr/bin/env python3
"""
Fornyer øyeblikksbildene områderegisteret seedes fra (issue #312 «Strukturmodell 7: områderegister»).

Oppstarten gjør ALDRI nettverkskall (CLAUDE.md §4) — OmraderegisterSeed leser bare filene i denne mappa.
Dette skriptet er den eneste veien til nye data: kjør det, se på diffen, commit filene. Det er bevisst et
manuelt steg: en ny kommuneinndeling skal leses av et menneske før den blir seedet.

    python src/RegelIde.Data/Seed/fornye-omraderegister.py

Bare standardbiblioteket. Skriver (overskriver) disse filene i samme mappe som skriptet:

  kartverket-fylker-kommuner.json  Kartverket kommuneinfo, fylker med kommuner (rått svar)
  brreg-kommuner.json              Enhetsregisteret, alle enheter med organisasjonsform KOMM (uttrekk)
  brreg-domstoler.json             Enhetsregisteret, tingretter og lagmannsretter under Domstolene i Norge (uttrekk)
  kartverket-ssr-rettssteder.json  Kartverkets SSR, eksakte navnetreff for rettssteder som ikke er kommunenavn
  ssb-klass-104-fylkesendringer-2024.json  SSB KLASS 104, fylkesendringene 1.1.2024 (rått svar)

Filene statsforvalter-embetsomrader.json og helseregioner-rhf.json fornyes IKKE her: de er lest for hånd fra
kilder uten API (vedtekter i PDF, kgl.res.) og skal oppdateres av et menneske som har lest kilden.
"""
import datetime
import json
import os
import urllib.parse
import urllib.request

MAPPE = os.path.dirname(os.path.abspath(__file__))
IDAG = datetime.date.today().isoformat()

KARTVERKET_URL = (
    "https://ws.geonorge.no/kommuneinfo/v1/fylkerkommuner"
    "?filtrer=fylkesnummer,fylkesnavn,kommuner.kommunenummer,kommuner.kommunenavnNorsk"
)
BRREG_KOMM_URL = "https://data.brreg.no/enhetsregisteret/api/enheter?organisasjonsform=KOMM&size=1000"
BRREG_SOK_URL = "https://data.brreg.no/enhetsregisteret/api/enheter?navn={navn}&size=200"
DOMSTOLENE_I_NORGE = "984195796"  # overordnet enhet for alle 28 tingretter og 6 lagmannsretter (lest 2026-10-08)
SSR_URL = "https://ws.geonorge.no/stedsnavn/v1/navn?{q}"
# Navneobjekttypene som er et bebygd sted et rettssted kan ligge i. «Bygdelag (bygd)» er med fordi Lofthus
# (Hardanger og Voss tingrett) bare finnes slik i SSR — målt 2026-10-08. Gard, nes, bydel osv. er ikke med.
BEBYGGELSESTYPER = {"By", "Tettsted", "Tettbebyggelse", "Bygdelag (bygd)"}
SSB_URL = "https://data.ssb.no/api/klass/v1/classifications/104/changes?from=2023-12-31&to=2024-01-02"

# Rettsstedene i forskrift om inndelingen av rettskretser og lagdømmer (2021-01-22-163) som IKKE er et
# kommunenavn i Kartverket-fila. OmraderegisterSeed rapporterer et rettssted den ikke finner her eller som
# kommune — kommer det et nytt, legges det til i lista og skriptet kjøres på nytt.
RETTSSTEDER_UTEN_KOMMUNENAVN = [
    "Brekstad", "Brønnøysund", "Egersund", "Fagernes", "Finnsnes", "Førde", "Hokksund", "Hønefoss",
    "Lofthus", "Mo i Rana", "Mysen", "Sandnessjøen", "Sandvika", "Ski", "Svolvær", "Vågåmo",
]


def hent(url):
    req = urllib.request.Request(url, headers={"Accept": "application/json", "User-Agent": "regel-ide-fornyelse/1.0"})
    with urllib.request.urlopen(req, timeout=60) as svar:
        return json.load(svar)


def skriv(filnavn, innhold):
    sti = os.path.join(MAPPE, filnavn)
    with open(sti, "w", encoding="utf-8", newline="\n") as f:
        json.dump(innhold, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("skrev", sti)


def kartverket():
    svar = hent(KARTVERKET_URL)
    for fylke in svar:
        fylke["kommuner"].sort(key=lambda k: k["kommunenummer"])
    svar.sort(key=lambda f: f["fylkesnummer"])
    antall = sum(len(f["kommuner"]) for f in svar)
    skriv("kartverket-fylker-kommuner.json", {
        "_kilde": "Kartverket, kommuneinfo-API (fylker med kommuner). Rått svar, bare sortert på nummer.",
        "_url": KARTVERKET_URL,
        "_hentet": IDAG,
        "_antall": {"fylker": len(svar), "kommuner": antall},
        "_bruk": "Issue #312: OmraderegisterSeed oppretter fylke- og kommuneområdene og O-kantene fylke -> kommune herfra. "
                 "Fornyes med fornye-omraderegister.py.",
        "svar": svar,
    })


def brreg_kommuner():
    svar = hent(BRREG_KOMM_URL)
    enheter = svar.get("_embedded", {}).get("enheter", [])
    if svar["page"]["totalElements"] != len(enheter):
        raise SystemExit(f"Brreg ga {len(enheter)} av {svar['page']['totalElements']} KOMM-enheter — øk size.")
    ut = sorted(({
        "organisasjonsnummer": e["organisasjonsnummer"],
        "navn": e["navn"],
        "kommunenummer": (e.get("forretningsadresse") or {}).get("kommunenummer"),
    } for e in enheter), key=lambda e: e["organisasjonsnummer"])
    skriv("brreg-kommuner.json", {
        "_kilde": "Enhetsregisteret (Brønnøysundregistrene), alle enheter med organisasjonsform KOMM. UTTREKK: "
                  "organisasjonsnummer, navn og forretningsadresse.kommunenummer.",
        "_url": BRREG_KOMM_URL,
        "_hentet": IDAG,
        "_antall": len(ut),
        "_bruk": "Issue #312: kobler kommunen som rettssubjekt (Virksomhet, nøkkel = organisasjonsnummer) til sitt "
                 "kommunenummer. For organisasjonsform KOMM er forretningsadressens kommunenummer kommunens eget "
                 "(BrregKlient.Kommunenummer, verifisert live 2026-09-08); seeden sjekker i tillegg at nummeret er "
                 "entydig og finnes hos Kartverket.",
        "enheter": ut,
    })


def brreg_domstoler():
    funnet = {}
    sok = []
    for navn in ("tingrett", "lagmannsrett"):
        url = BRREG_SOK_URL.format(navn=navn)
        sok.append(url)
        for e in hent(url).get("_embedded", {}).get("enheter", []):
            if e.get("overordnetEnhet") != DOMSTOLENE_I_NORGE:
                continue
            if e["organisasjonsform"]["kode"] != "ORGL":
                continue
            funnet[e["organisasjonsnummer"]] = {
                "organisasjonsnummer": e["organisasjonsnummer"],
                "navn": e["navn"],
                "organisasjonsform": e["organisasjonsform"]["kode"],
                "overordnetEnhet": e["overordnetEnhet"],
            }
    ut = sorted(funnet.values(), key=lambda e: e["navn"])
    skriv("brreg-domstoler.json", {
        "_kilde": "Enhetsregisteret (Brønnøysundregistrene), navnesøk. UTTREKK: enheter med organisasjonsform ORGL "
                  f"og overordnet enhet {DOMSTOLENE_I_NORGE} (DOMSTOLENE I NORGE).",
        "_url": sok,
        "_hentet": IDAG,
        "_antall": len(ut),
        "_bruk": "Issue #312: tingrettene og lagmannsrettene finnes ikke i virksomhetskatalogen. OmraderegisterSeed "
                 "oppretter dem herfra (nøkkel = organisasjonsnummer) når navnet i inndelingsforskriften treffer "
                 "registernavnet eksakt (en av skråstrek-formene, uten skille på store/små bokstaver).",
        "enheter": ut,
    })


def ssr_rettssteder():
    ut = []
    for navn in RETTSSTEDER_UTEN_KOMMUNENAVN:
        treff = []
        side = 1
        while True:
            q = urllib.parse.urlencode({"sok": navn, "fuzzy": "false", "treffPerSide": 500, "side": side})
            svar = hent(SSR_URL.format(q=q))
            for h in svar.get("navn", []):
                if h.get("skrivemåte") != navn or h.get("navnestatus") != "hovednavn":
                    continue
                if h.get("navneobjekttype") not in BEBYGGELSESTYPER:
                    continue
                treff.append({
                    "skrivemåte": h["skrivemåte"],
                    "navneobjekttype": h["navneobjekttype"],
                    "navnestatus": h["navnestatus"],
                    "stedsnummer": h["stedsnummer"],
                    "kommuner": [{"kommunenummer": k["kommunenummer"], "kommunenavn": k["kommunenavn"]}
                                 for k in h.get("kommuner", [])],
                })
            meta = svar["metadata"]
            if meta["viserTil"] >= meta["totaltAntallTreff"]:
                break
            side += 1
        treff.sort(key=lambda t: t["stedsnummer"])
        ut.append({"sok": navn, "treff": treff})
    skriv("kartverket-ssr-rettssteder.json", {
        "_kilde": "Kartverket, Sentralt stedsnavnregister (SSR), /stedsnavn/v1/navn. UTTREKK: treff med eksakt samme "
                  "skrivemåte, navnestatus hovednavn og navneobjekttype i " + ", ".join(sorted(BEBYGGELSESTYPER)) + ".",
        "_url": SSR_URL.format(q="sok=<navn>&fuzzy=false&treffPerSide=500&side=<n>"),
        "_hentet": IDAG,
        "_bruk": "Issue #312: rettssteder som ikke er en kommune (Johanns funn 1 på #312) legges inn som tettsted-område "
                 "i kommunen SSR oppgir. Seeden godtar bare et treff i en av kommunene tingretten selv dekker, og bare "
                 "når det er nøyaktig ett slikt; ellers listes rettsstedet som uløst.",
        "navn": ut,
    })


def ssb_fylkesendringer():
    svar = hent(SSB_URL)
    skriv("ssb-klass-104-fylkesendringer-2024.json", {
        "_kilde": "SSB KLASS, klassifikasjon 104 (fylkesinndeling), endringer rundt 1.1.2024. Rått svar.",
        "_url": SSB_URL,
        "_hentet": IDAG,
        "_bruk": "Issue #312: RHF-vedtektene (sist endret 11.06.2024) navngir fylker fra før 1.1.2024 (Viken, Vestfold og "
                 "Telemark, Troms og Finnmark). Seeden avbilder dem til dagens fylker med denne endringslista — ikke ved "
                 "å gjette ut fra navn.",
        "svar": svar,
    })


if __name__ == "__main__":
    kartverket()
    brreg_kommuner()
    brreg_domstoler()
    ssr_rettssteder()
    ssb_fylkesendringer()
