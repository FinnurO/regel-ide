#!/usr/bin/env python3
"""
Deterministisk konvertering av strukturfasiten etter issue #367 «utsagnsstatus» og #335 «delegeringsadgang» (Johanns beslutninger
2026-10-09 og 2026-10-15, docs/34 §3–5, docs/profil/03 §5). Ingen rad endres for hånd; alt under er regler som kan leses og kjøres om igjen.

    python data/fasit/strukturmodell/konvertering-367-utsagnsstatus.py            # fasitfilene
    python data/fasit/strukturmodell/konvertering-367-utsagnsstatus.py ki-utdata  # de lagrede KI-utdataene (#308)

Idempotent: kjøres det på en allerede konvertert fil, finnes ingen «delegerbar», og reglene gir samme verdier — ingenting endres.

REGLER

  D. delegerbar (bool, #341) → delegeringsadgang (etter TEKSTEN) + delegering_slutning (alltid en slutning). Bare på kompetanse,
     i denne rekkefølgen, og den første som slår til gjelder:
     D0. delegeringskompetanse (positiv eller negativ) får ingen verdi: raden ER delegeringsregelen («X kan delegere …», «… kan ikke
         delegeres»), og adgangen er en egenskap ved kompetansen som delegeres (#335), ikke ved delegeringsregelen selv. Den gamle
         verdien står i kommentaren.
     D1. Sitatet sier «kan ikke delegeres», eller fra-aktørens tekstform/variant står rett foran «selv» i sitatet eller i samme ledd
         («kommunestyret selv», ikke «selv om») → uttrykkelig_forbudt (tekstfunn, sameloven § 2-12 fjerde ledd, #356).
     D2. En negativ delegeringskompetanse med «kan ikke delegeres» og paragrafhenvisning gjelder raden (samme fra, eId i en av
         paragrafene — #356 R3) → uttrykkelig_forbudt.
     D3. Sitatet eller samme ledd sier at fra-aktøren «kan delegere» («Innstillingsrådet kan delegere til domstoladministrasjonen å
         foreta konstitusjonen») → uttrykkelig_tillatt.
     D4. Ellers: nevner bestemmelsen (alle noder i samme paragraf, fra noder/<kilde>.json) delegering («deleger»), kan det ikke
         avgjøres uten å lese den → INGEN verdi, raden listes for Johann.
     D5. Ellers → ikke_regulert («bestemmelsen sier ingenting»), og for en «Kongen»-aktør en SLUTNING på sitatet (#335, juristrunden
         2026-10-09 kort nr. 10): «Kongen i statsråd» eller «Grunnloven § 21» (embetsutnevnelse i statsråd, jf. Grl. § 28 — juristrunden
         i #355, domstolloven u120) → trolig_ikke_delegerbar, ellers «Kongen» → trolig_delegerbar. Aldri et tekstfunn.
     Raden får en kommentar «[#367, D<n>] …» når verdien er satt eller avvik fra den gamle delegerbar-verdien må forklares.
     Den gamle verdien sjekkes: true må bli tillatt eller trolig_delegerbar, false må bli forbudt eller trolig_ikke_delegerbar. Avviker
     den, listes raden (ingen gammel verdi overstyres i stillhet).

  K. kunnskapsstatus (bare fasiten — KI-utdataene får ingen: metoden er ikke dokumentert per rad, og godkjenningen er maskinell):
     K1. normalisering når typen er avgjort av en regel i kompetanseleksikonet (src/RegelIde.Data/Strukturkonvertering/
         kompetanseleksikon.json): et av regelens uttrykk står i sitatet (mønsteret «X …» med valgfrie ledd i parentes og
         alternativer med «/»), og raden har regelens kategori og type. Begrunnelsen er regelen: «kompetanseleksikon <versjon>,
         regel <id>: «<uttrykk>» → <type>». Fra/til er ikke vurdert av regelen.
     Alt annet: ingen verdi (ikke vurdert). Tekstfunn, fortolkning og avledet settes i juristrunder, ikke av et skript (docs/34 §3).
"""
import collections
import json
import os
import re
import sys

HER = os.path.dirname(os.path.abspath(__file__))
KI = len(sys.argv) > 1 and sys.argv[1] == "ki-utdata"
MAPPE = os.path.join(HER, "ki-utdata") if KI else HER
KILDER = ["domstolloven", "energiloven", "helse-og-omsorgstjenesteloven", "sameloven", "spesialisthelsetjenesteloven"]
LEKSIKON = json.load(open(os.path.join(HER, "..", "..", "..", "src", "RegelIde.Data", "Strukturkonvertering", "kompetanseleksikon.json"),
                          encoding="utf-8"))


def mal_til_regex(uttrykk):
    """«X kan gi (nærmere) forskrift(er) om» → regex. X = aktøren (hvilke som helst ord), «(…)» valgfritt, «a/b» alternativer."""
    deler = []
    for ord_ in uttrykk.split():
        if ord_ == "X":
            deler.append(r"(?:\S+\s+){0,6}?")
            continue
        if ord_.startswith("(") and ord_.endswith(")"):
            deler.append(r"(?:" + re.escape(ord_[1:-1]) + r"\s+)?")
            continue
        alternativer = [re.escape(a).replace(r"\(", "(?:").replace(r"\)", ")?") for a in ord_.split("/")]
        deler.append("(?:" + "|".join(alternativer) + r")\s+")
    return re.compile(r"\b" + "".join(deler).rstrip(r"\s+").removesuffix(r"\s+"), re.I)


REGLER = [(r, u, mal_til_regex(u)) for r in LEKSIKON["regler"] for u in r["uttrykk"]]


def noder_for(kilde):
    sti = os.path.join(HER, "noder", kilde + ".json")
    return json.load(open(sti, encoding="utf-8")) if os.path.exists(sti) else []


def paragraf(eid):
    m = re.match(r"(.*?§[^/]+)", eid)
    return m.group(1) if m else eid


def tilfoy(u, tekst):
    k = u.get("kommentar") or ""
    if tekst not in k:
        u["kommentar"] = (k + " " + tekst).strip()


def delegering(u, d, former, kongen, ptekst, negative_refs, ledd):
    """(adgang, slutning, regel) for én kompetanserad — se D1–D5."""
    sitat = u["sitat"]
    if u["type"] == "delegeringskompetanse":
        return None, None, "D0"
    tekster = (sitat, ledd.get(u["eid"], ""))
    fra = u.get("fra")
    navn = former.get(fra, [])
    if re.search(r"\bkan\s+ikke\s+delegeres\b", sitat, re.I) or any(
            re.search(re.escape(f) + r"\s+selv\b(?!\s+om\b)", t, re.I) for f in navn for t in tekster):
        return "uttrykkelig_forbudt", None, "D1"
    lov_par = paragraf(u["eid"])
    if fra and any(fra == nfra and lov_par in refs for nfra, refs in negative_refs):
        return "uttrykkelig_forbudt", None, "D2"
    if any(re.search(re.escape(f) + r"\s+kan\s+delegere\b", t, re.I) for f in navn for t in tekster):
        return "uttrykkelig_tillatt", None, "D3"
    if re.search(r"deleger", ptekst.get(lov_par, ""), re.I):
        return None, None, "D4"
    slutning = None
    if fra in kongen:
        if "Kongen i statsråd" in sitat or re.search(r"Grunnloven\s+§\s*21\b", sitat):
            slutning = "trolig_ikke_delegerbar"
        elif "Kongen" in sitat:
            slutning = "trolig_delegerbar"
    return "ikke_regulert", slutning, "D5"


def kunnskapsstatus(u):
    for r, uttrykk, rx in REGLER:
        if (u["kategori"], u["type"]) == (r["kategori"], r["type"]) and rx.search(u["sitat"]):
            return "normalisering", f"kompetanseleksikon {LEKSIKON['versjon']}, regel {r['id']}: «{uttrykk}» → {r['type']}"
    return None, None


logg, uavklart, avvik = [], [], []
teller = collections.Counter()
for kilde in KILDER:
    sti = os.path.join(MAPPE, kilde + ".json")
    if not os.path.exists(sti):
        continue
    raw = open(sti, encoding="utf-8").read().replace("\r\n", "\n")
    innrykk = len(raw.split("\n")[1]) - len(raw.split("\n")[1].lstrip())
    d = json.loads(raw)
    noder = noder_for(kilde)
    ledd = {n["eid"]: n.get("tekst") or "" for n in noder}
    ptekst = collections.defaultdict(str)
    for n in noder:
        ptekst[paragraf(n["eid"])] += " " + (n.get("tekst") or "") + " " + (n.get("overskrift") or "")
    former = {a["id"]: [a["tekstform"]] + list(a.get("varianter") or []) for a in d["aktorer"]}
    kongen = {a["id"] for a in d["aktorer"] if a["tekstform"].lower() == "kongen" or a["tekstform"].lower().startswith("kongen i statsr")}
    negative_refs = []
    for n in d["utsagn"]:
        if (n["kategori"], n["type"], n["polaritet"]) == ("kompetanse", "delegeringskompetanse", "negativ") \
                and re.search(r"\bkan\s+ikke\s+delegeres\b", n["sitat"]) and n.get("fra"):
            m = re.search(r"§§?\s*([0-9a-z\-–, ]+(?:og\s+[0-9a-z\-–]+)?)", n["sitat"])
            refs = re.findall(r"\d+-\d+\s?[a-z]?\b", m.group(1)) if m else []
            lov = n["eid"].split("§")[0]
            negative_refs.append((n["fra"], {f"{lov}§{r.replace(' ', '')}" for r in refs}))

    for u in d["utsagn"]:
        gammel = u.pop("delegerbar", None)
        if u["kategori"] == "kompetanse":
            adgang, slutning, regel = delegering(u, d, former, kongen, ptekst, negative_refs, ledd)
            ok = gammel is None or regel == "D0" or (gammel is True and (adgang == "uttrykkelig_tillatt" or slutning == "trolig_delegerbar")) \
                or (gammel is False and (adgang == "uttrykkelig_forbudt" or slutning == "trolig_ikke_delegerbar"))
            if not ok:
                avvik.append((kilde, u["id"], gammel, adgang, slutning, regel, u["sitat"][:90]))
            if regel == "D0":
                u.pop("delegeringsadgang", None)
                u.pop("delegering_slutning", None)
                if gammel is not None:
                    tilfoy(u, f"[#367, D0] Delegeringsadgang gjelder kompetansen som delegeres, ikke delegeringsregelen selv. Før #367: "
                              f"delegerbar = {str(gammel).lower()}.")
            elif adgang is None:
                uavklart.append((kilde, u["id"], gammel, u["sitat"][:110]))
                tilfoy(u, "[#367, D4] Bestemmelsen nevner delegering; delegeringsadgangen er ikke avgjort uten å lese den (listet for Johann)."
                          + (f" Før #367: delegerbar = {str(gammel).lower()}." if gammel is not None else ""))
                u.pop("delegeringsadgang", None)
                u.pop("delegering_slutning", None)
            else:
                if u.get("delegeringsadgang") != adgang or u.get("delegering_slutning") != slutning:
                    logg.append((kilde, u["id"], f"delegerbar {gammel} → {adgang}{' + ' + slutning if slutning else ''} ({regel})"))
                u["delegeringsadgang"] = adgang
                if slutning:
                    u["delegering_slutning"] = slutning
                else:
                    u.pop("delegering_slutning", None)
                if regel != "D5" or slutning:
                    tilfoy(u, f"[#367, {regel}] " + {
                        "D1": "Delegering uttrykkelig forbudt: TEKSTFUNN i sitatet.",
                        "D2": "Delegering uttrykkelig forbudt: TEKSTFUNN i en annen bestemmelse («kan ikke delegeres» med paragrafhenvisning).",
                        "D3": "Delegering uttrykkelig tillatt: TEKSTFUNN i sitatet.",
                        "D5": "Delegering ikke regulert i teksten; «Kongen»/«Kongen i statsråd» gir bare en SLUTNING etter alminnelige regler (#335).",
                    }[regel])
            teller[(regel, adgang, slutning)] += 1
        elif gammel is not None:
            raise AssertionError(f"{kilde} {u['id']}: delegerbar utenfor kompetanse")
        if not KI:
            status, begrunnelse = kunnskapsstatus(u)
            if status:
                u["kunnskapsstatus"], u["begrunnelse"] = status, begrunnelse
                teller[("kunnskapsstatus", status)] += 1
            else:
                teller[("kunnskapsstatus", None)] += 1
    assert not any("delegerbar" in u for u in d["utsagn"])
    with open(sti, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(d, ensure_ascii=False, indent=innrykk))

print(f"Mappe: {os.path.relpath(MAPPE, HER) or '.'}")
print("Telling (regel, adgang, slutning):")
for k, v in sorted(teller.items(), key=lambda kv: str(kv[0])):
    print(f"  {k}: {v}")
print(f"\nEndringer: {len(logg)}")
print(f"\nIkke avgjort uten å lese bestemmelsen (D4, {len(uavklart)}):")
for r in uavklart:
    print("  ", *r)
print(f"\nAvvik fra gammel delegerbar ({len(avvik)}):")
for r in avvik:
    print("  ", *r)
