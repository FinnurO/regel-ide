#!/usr/bin/env python3
"""
Deterministisk konvertering av strukturfasiten etter Johanns beslutninger på issue #352 (2026-10-08), «rester etter #341».
Ingen rad endres for hånd; alt under er regler som kan leses og kjøres om igjen.

    python data/fasit/strukturmodell/konvertering-352-oppnevning.py            # fasitfilene
    python data/fasit/strukturmodell/konvertering-352-oppnevning.py ki-utdata  # de lagrede KI-utdataene (#308)

Idempotent: kjøres det på en allerede konvertert fil, finnes ingen gamle typer, undertypene står alt, og ingenting endres.

Reglene (FORMAT.md og docs/33 §4.3 er oppdatert tilsvarende):

  Beslutning 1 — oppnevningsfamilien. «Oppnevningskompetanse … er den mest generelle kategorien. […] Fellesnevner: En aktør
       gis myndighet til å bestemme hvem som skal inneha en rolle, et verv eller en funksjon.» Verbet beholdes som UNDERTYPE:
       relasjon/velger                     → kompetanse/oppnevningskompetanse, undertype «valg»
       kompetanse/utpekingskompetanse      → kompetanse/oppnevningskompetanse, undertype «utpeking»
       kompetanse/ansettelseskompetanse    → kompetanse/oppnevningskompetanse, undertype «ansettelse»
       Samme retning: fra = den som velger/utpeker/ansetter, til = motparten. Undertypen fra den GAMLE typen vinner over
       sitatet (det er det annotatøren sa; vi bevarer, vi tolker ikke på nytt).
       avsettingskompetanse står som egen type i familien oppnevning (hovedøktens tolkning, Johann bekrefter) — ingen
       dataendring her.
  Beslutning 2 — relasjon/annet:ankeinstans_for → kompetanse/overprovingskompetanse, undertype «anke» (fra = ankeinstansen,
       til = den hvis avgjørelser ankes). Undertypen «anke» er hovedøktens tolkning (Johann bekrefter).
  Beslutning 3 — leksikonregelen vedtak-godkjennes-av gir godkjenningskompetanse: kompetanse/vedtakskompetanse der SITATET
       treffer regelens to uttrykk (samme regex som Monsterkatalog.cs: «skal/må/kan (være) godkjennes/godkjent av X» og
       «Godkjenning … gis av X») → godkjenningskompetanse. Andre godkjenningslignende vedtaksutsagn («er godkjent av X»,
       «godkjennes … av X» uten modalverb, «til godkjenning») røres IKKE — de er ikke regelens uttrykk, og å utvide den
       ville vært å gjette. Skriptet lister dem som kandidater for fasitgjennomgangen (#309).
       forelegging → familien kontroll er en egenskap ved TYPEN (Strukturkanter.Kompetansetyper), ikke ved fasiten.
  Undertype fra sitatet — for oppnevnings- og overprøvingskompetanse som ikke har en undertype etter reglene over: den ENE
       undertypen hvis ordstammer (kompetanseleksikon.json, «undertyper» — samme fil mønsterlaget leser) står ved ordstart i
       sitatet. Ingen treff eller treff for flere undertyper → ingen undertype (gjettes ikke).
  Delegerbar (#335-regelen fra konvertering-341-kompetanse.py, kopiert uendret): på rader som BLIR kompetanse her
       (velger, ankeinstans_for), så de behandles som alle andre kompetanser.
  Beslutning 4 — administrativt_underordnet, radgir, oppretter og avvikler blir stående (struktur/hendelse). Ingen endring.

Fortsatt ikke avgjort (listes): bistar, samarbeider_med og del_av under relasjon, annet:forelegges_for og
annet:intern_forelegging (plikt til å forelegge — ikke det samme som foreleggingskompetanse), annet:ankekompetanse (en PARTS
rett til å anke, ikke ankeinstans) og annet:overordnet_domstol.
"""
import collections
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
HER = os.path.dirname(os.path.abspath(__file__))
MAPPE = os.path.join(HER, "ki-utdata") if len(sys.argv) > 1 and sys.argv[1] == "ki-utdata" else HER
KILDER = ["domstolloven", "energiloven", "helse-og-omsorgstjenesteloven", "sameloven", "spesialisthelsetjenesteloven"]
LEKSIKON = os.path.join(HER, "..", "..", "..", "src", "RegelIde.Data", "Strukturkonvertering", "kompetanseleksikon.json")

# (gammel kategori, gammel type) → (ny type, undertype); ny kategori er alltid kompetanse.
TIL_OPPNEVNING = {
    ("relasjon", "velger"): ("oppnevningskompetanse", "valg"),
    ("kompetanse", "utpekingskompetanse"): ("oppnevningskompetanse", "utpeking"),
    ("kompetanse", "ansettelseskompetanse"): ("oppnevningskompetanse", "ansettelse"),
    ("relasjon", "annet:ankeinstans_for"): ("overprovingskompetanse", "anke"),
}

# Monsterkatalog.cs, «vedtak-godkjennes-av» (Etter("fra") forenklet til «et ord følger», nok til å kjenne igjen uttrykket).
ETTER = r"(?:[^\W\d_]+s\s+)?(?:[^\W\d_]+-\s+og\s+)?[^\W\d_]"
FRITT = r"(?:[^.;]|\.(?=\s*\(?[a-zæøå\d]))"
GODKJENNES_AV = [
    re.compile(r"\b(?:skal|må|kan)\s+(?:være\s+)?(?:godkjennes|godkjent)\s+av\s+" + ETTER),
    re.compile(r"\bGodkjenning\b" + FRITT + r"{0,80}?\bgis\s+av\s+" + ETTER),
]

UAVGJORT = {("relasjon", t) for t in ["bistar", "samarbeider_med", "del_av", "annet:forelegges_for", "annet:intern_forelegging",
                                      "annet:overordnet_domstol"]} | {("kompetanse", "annet:ankekompetanse")}

UNDERTYPER = json.load(open(LEKSIKON, encoding="utf-8"))["undertyper"]


def undertype_fra_sitat(fasittype, sitat):
    treff = {u["undertype"] for u in UNDERTYPER if u["type"] == fasittype
             and any(re.search(r"\b" + s, sitat, re.IGNORECASE) for s in u["stammer"])}
    return treff.pop() if len(treff) == 1 else None


def delegerbar(u, kongen, former):
    """#335-regelen, kopiert fra konvertering-341-kompetanse.py (avgjort på SITATET)."""
    sitat, verdi = u["sitat"], None
    if u.get("fra") in kongen:
        if "Kongen i statsråd" in sitat:
            verdi = False
        elif re.search(r"\bKongen", sitat):
            verdi = True
    if u.get("fra") and any(re.search(re.escape(f) + r"\s+selv\b", sitat, re.IGNORECASE) for f in former.get(u["fra"], [])):
        verdi = False
    return verdi


def konverter(d):
    endret = collections.Counter()
    kandidater = []
    kongen = {a["id"] for a in d["aktorer"] if a["tekstform"].lower() == "kongen" or a["tekstform"].lower().startswith("kongen i statsr")}
    former = {a["id"]: [a["tekstform"]] + list(a.get("varianter") or []) for a in d["aktorer"]}
    for u in d["utsagn"]:
        gammel = (u["kategori"], u["type"])
        if gammel in TIL_OPPNEVNING:
            ny_type, undertype = TIL_OPPNEVNING[gammel]
            ble_kompetanse = u["kategori"] != "kompetanse"
            u["kategori"], u["type"], u["undertype"] = "kompetanse", ny_type, undertype
            if ble_kompetanse and "delegerbar" not in u:
                verdi = delegerbar(u, kongen, former)
                if verdi is not None:
                    u["delegerbar"] = verdi
                    endret[f"delegerbar = {str(verdi).lower()}"] += 1
        elif gammel == ("kompetanse", "vedtakskompetanse"):
            if any(r.search(u["sitat"]) for r in GODKJENNES_AV):
                u["type"] = "godkjenningskompetanse"
            elif re.search(r"godkjen", u["sitat"], re.IGNORECASE):
                kandidater.append(u)
        if u["kategori"] == "kompetanse" and u["type"] in ("oppnevningskompetanse", "overprovingskompetanse") and not u.get("undertype"):
            undertype = undertype_fra_sitat(u["type"], u["sitat"])
            if undertype is not None:
                u["undertype"] = undertype
                endret[f"undertype fra sitatet: {u['type']}/{undertype}"] += 1
        if (u["kategori"], u["type"]) != gammel:
            ny = f"{u['kategori']}/{u['type']}" + (f" ({u['undertype']})" if u.get("undertype") else "")
            endret[f"{gammel[0]}/{gammel[1]} → {ny}"] += 1
    return endret, kandidater


def tell(d):
    return collections.Counter((u["kategori"], u["type"]) for u in d["utsagn"])


totalt_for, totalt_etter, alle_endringer, alle_kandidater = collections.Counter(), collections.Counter(), collections.Counter(), []
for kilde in KILDER:
    sti = os.path.join(MAPPE, kilde + ".json")
    if not os.path.exists(sti):
        continue
    raw = open(sti, encoding="utf-8").read().replace("\r\n", "\n")
    innrykk = len(raw.split("\n")[1]) - len(raw.split("\n")[1].lstrip())
    d = json.loads(raw)
    antall_for = len(d["utsagn"])
    totalt_for.update(tell(d))
    endret, kandidater = konverter(d)
    assert len(d["utsagn"]) == antall_for, "ingen rader skal forsvinne"
    totalt_etter.update(tell(d))
    alle_endringer.update(endret)
    alle_kandidater += [(kilde, u) for u in kandidater]
    with open(sti, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(d, ensure_ascii=False, indent=innrykk))
    print(f"{kilde}: {antall_for} utsagn, {sum(v for k, v in endret.items() if '→' in k)} endret type")

print(f"\nMappe: {os.path.relpath(MAPPE, HER) or '.'}")
print("Endringer:")
for k, v in sorted(alle_endringer.items(), key=lambda kv: (-kv[1], kv[0])):
    print(f"  {v:4}  {k}")
kat = lambda c, k: sum(v for (kk, _), v in c.items() if kk == k)
print(f"\nFør:   {sum(totalt_for.values())} utsagn, relasjon {kat(totalt_for, 'relasjon')}, kompetanse {kat(totalt_for, 'kompetanse')}")
print(f"Etter: {sum(totalt_etter.values())} utsagn, relasjon {kat(totalt_etter, 'relasjon')}, kompetanse {kat(totalt_etter, 'kompetanse')}")
print("\nPer type (før → etter), de som er berørt:")
for (k, t) in sorted(set(totalt_for) | set(totalt_etter)):
    if t in {"velger", "utpekingskompetanse", "ansettelseskompetanse", "oppnevningskompetanse", "avsettingskompetanse",
             "annet:ankeinstans_for", "overprovingskompetanse", "vedtakskompetanse", "godkjenningskompetanse", "foreleggingskompetanse"}:
        print(f"  {k}/{t}: {totalt_for[(k, t)]} → {totalt_etter[(k, t)]}")
print("\nIkke avgjort (står uendret):")
for (k, t), v in sorted(totalt_etter.items()):
    if (k, t) in UAVGJORT:
        print(f"  {v:4}  {k}/{t}")
print(f"\nGodkjenningslignende vedtakskompetanse som IKKE er regelens uttrykk (kandidater for #309, ikke konvertert): {len(alle_kandidater)}")
for kilde, u in alle_kandidater:
    print(f"  {kilde} {u['id']}: {u['sitat'][:110]}")
