#!/usr/bin/env python3
"""
Systemisk rettelse av domstolloven-fasitens INNDELINGSDEL (issue #312, Johanns funn fra fasitkontrollen #309,
2026-10-07/08). Kjøres én gang; idempotent (gjør ingenting hvis det ikke finnes rk_*-aktører igjen).

    python data/fasit/strukturmodell/rettelse-312-domstolinndeling.py

Hva som endres, og hvorfor (kommentarene på #312):
  1. De oppfunne rettskrets-aktørene (rk_*) fjernes — rettskretsen har ikke eget navn i forskriften.
     «tingrett har_ansvarsomrade rk» + «rk bestar_av kommune» blir «tingrett har_ansvarsomrade kommune»
     (sitat og eId fra bestar_av-raden).
  2. «lagsogn bestar_av rk» (sitat = tingrettens navn i «Til lagsognet X sogner …») blir det teksten sier:
     «tingrett annet:sogner_til lagsogn» (kategori ansvarsomrade — aktør → område; ingen FORMAT-type passer).
     Samme id, eId og sitat. At KOMMUNENE inngår i lagsognet via tingrettens kommuner (Johanns kommentar (c) på
     #312) er en avledning modellen gjør (DomstolinndelingTolker/OmraderegisterSeed), og annoteres ikke som 357
     egne rader — av samme grunn som de AVLEDEDE «rk del_av lagdømme»-radene fjernes under.
  3. Rader som bare beskrev den oppfunne rettskretsen og ikke har noe motstykke uten den, fjernes og listes:
     «rk del_av lagdømme» (allerede merket AVLEDET; følger transitivt av lagsogn → lagdømme),
     «rk annet:gruppert_under fylkesgruppe» og «rk del_av fylke» («X fylke har rettskretsene …»). De
     fylkesgruppe-aktørene som da står uten utsagn, fjernes.
  4. Rettssteder (sted_*, annet:sted) slås sammen med kommune-aktøren med samme navn (en felles skråstrek-form).
     Et rettssted som ikke er en kommune, blir område + «del_av» kommunen; kommunen er Kartverkets SSR-treff i en
     av tingrettens egne kommuner (samme regel og samme øyeblikksbilde som DomstolinndelingTolker i koden), og
     raden har kilde_utenfor_korpus = true fordi teksten ikke sier hvilken kommune stedet ligger i.
     Uløste steder skrives ut og blir stående urørt.
Hver endret eller ny rad får "verifisert_av".
"""
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
HER = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HER, "..", "..", ".."))
FASIT = os.path.join(HER, "domstolloven.json")
SEED = os.path.join(REPO, "src", "RegelIde.Data", "Seed")
VERIFISERT = "Johann (systemisk rettelse via fasitkontroll #309, 2026-10-08)"
BEBYGGELSE = ["By", "Tettsted", "Tettbebyggelse", "Bygdelag (bygd)"]

d = json.load(open(FASIT, encoding="utf-8"))
aktorer = {a["id"]: a for a in d["aktorer"]}
if not any(i.startswith("rk_") for i in aktorer):
    print("Ingen rk_*-aktører — rettelsen er alt gjort.")
    sys.exit(0)

utsagn = d["utsagn"]
neste = max(int(u["id"][1:]) for u in utsagn if u["id"][1:].isdigit()) + 1


def ny_id():
    global neste
    i = f"u{neste}"
    neste += 1
    return i


def merk(u, kommentar):
    u["verifisert_av"] = VERIFISERT
    u["kommentar"] = (kommentar + (" " + u["kommentar"] if u.get("kommentar") else "")).strip()
    return u


rk_til_tr = {u["til"]: u["fra"] for u in utsagn
             if u["type"] == "har_ansvarsomrade" and (u["til"] or "").startswith("rk_") and (u["fra"] or "").startswith("tr_")}
assert len(rk_til_tr) == 28, len(rk_til_tr)

ut, fjernet = [], {"tr har_ansvarsomrade rk": 0, "rk del_av lagdømme": 0, "rk gruppert_under fylkesgruppe": 0, "rk del_av fylke": 0}
tr_kommuner = {}
# 1. rk bestar_av k → tr har_ansvarsomrade k
for u in utsagn:
    f, t = u["fra"] or "", u["til"] or ""
    if f.startswith("tr_") and t.startswith("rk_"):
        fjernet["tr har_ansvarsomrade rk"] += 1
        continue
    if f.startswith("rk_") and t.startswith("k_") and u["type"] == "bestar_av":
        tr = rk_til_tr[f]
        tr_kommuner.setdefault(tr, []).append(t)
        u.update(kategori="ansvarsomrade", type="har_ansvarsomrade", fra=tr)
        ut.append(merk(u, "Rettet #312: var «rettskrets består av kommune» med en oppfunnet rettskrets-aktør; tingretten har ansvarsområde direkte til kommunen."))
        continue
    ut.append(u)
utsagn = ut

# 2. ls bestar_av rk → ls bestar_av k (per kommune), 3. fjern rk-rader uten motstykke
ut = []
for u in utsagn:
    f, t = u["fra"] or "", u["til"] or ""
    if f.startswith("ls_") and t.startswith("rk_"):
        u.update(kategori="ansvarsomrade", type="annet:sogner_til", fra=rk_til_tr[t], til=f)
        ut.append(merk(u, "Rettet #312: var «lagsogn består av rettskrets» med en oppfunnet rettskrets-aktør; teksten sier at tingretten sogner til lagsognet. Kommunene inngår i lagsognet via tingrettens kommuner (avledet i modellen, ikke annotert)."))
        continue
    if f.startswith("rk_"):
        if t.startswith("ld_"): fjernet["rk del_av lagdømme"] += 1
        elif t.startswith("fgruppe_"): fjernet["rk gruppert_under fylkesgruppe"] += 1
        elif t.startswith("fylke_"): fjernet["rk del_av fylke"] += 1
        else: raise SystemExit(f"Uventet rk-rad: {u}")
        continue
    ut.append(u)
utsagn = ut

# 4. Rettssteder
kv = json.load(open(os.path.join(SEED, "kartverket-fylker-kommuner.json"), encoding="utf-8"))["svar"]
ssr = {s["sok"]: s["treff"] for s in json.load(open(os.path.join(SEED, "kartverket-ssr-rettssteder.json"), encoding="utf-8"))["navn"]}
navn_til_nr = {}
for f in kv:
    for k in f["kommuner"]:
        navn_til_nr.setdefault(k["kommunenavnNorsk"], []).append(k["kommunenummer"])


def former(s):
    return [p.strip() for p in s.split("/") if p.strip()]


def kommunenumre(kid):
    ns = {n for p in former(aktorer[kid]["tekstform"]) for n in navn_til_nr.get(p, [])}
    return ns


kommuneaktorer = [i for i in aktorer if i.startswith("k_")]
sete = [u for u in utsagn if u["type"] == "har_sete_i" and (u["til"] or "").startswith("sted_")]
uloste, sammenslatt, tettsteder = [], 0, 0
erstatt = {}
for u in sete:
    sid, tr = u["til"], u["fra"]
    sted = aktorer[sid]
    egne = tr_kommuner.get(tr, [])
    kand = [k for k in kommuneaktorer if set(former(aktorer[k]["tekstform"])) & set(former(sted["tekstform"]))]
    if len(kand) > 1:
        kand = [k for k in kand if k in egne]
    if len(kand) == 1:
        k = kand[0]
        erstatt[sid] = k
        u["til"] = k
        merk(u, "Rettet #312: rettsstedet er samme område som kommunen tingretten har ansvar for (to kanter til samme node).")
        if sted["tekstform"] != aktorer[k]["tekstform"] and sted["tekstform"] not in aktorer[k]["varianter"]:
            aktorer[k]["varianter"].append(sted["tekstform"])
        aktorer[k]["antall_forekomster"] += sted["antall_forekomster"]
        sammenslatt += 1
        continue
    if kand:
        uloste.append(f"{sted['tekstform']} ({sid}): {len(kand)} kommuner passer")
        continue
    # Tettsted: SSR-treff i en av tingrettens egne kommuner.
    egne_nr = {n: k for k in egne for n in kommunenumre(k)}
    treff = [(BEBYGGELSE.index(t["navneobjekttype"]), t, km["kommunenummer"])
             for p in former(sted["tekstform"]) for t in ssr.get(p, []) if t["navneobjekttype"] in BEBYGGELSE
             for km in t["kommuner"] if km["kommunenummer"] in egne_nr]
    if not treff:
        uloste.append(f"{sted['tekstform']} ({sid}): ikke kommune, og ingen SSR-bebyggelse i tingrettens kommuner")
        continue
    beste = min(r for r, _, _ in treff)
    treff = [x for x in treff if x[0] == beste]
    if len(treff) != 1:
        uloste.append(f"{sted['tekstform']} ({sid}): {len(treff)} SSR-treff av beste type")
        continue
    _, t, nr = treff[0]
    sted.update(entitetstype="omrade", referent=f"{sted['tekstform']} ({t['navneobjekttype']}, SSR-stedsnummer {t['stedsnummer']})",
                kommentar=f"Rettet #312: rettssted som ikke er en kommune — tettsted som område i kommunen (Johanns funn 1 på #312). Var: {sted.get('kommentar') or ''}".strip())
    sted["verifisert_av"] = VERIFISERT
    merk(u, "Rettet #312: rettsstedet er et tettsted, registrert som område.")
    utsagn.append(merk({
        "id": ny_id(), "eid": u["eid"], "sitat": u["sitat"], "kategori": "sammensetning_omrade", "type": "del_av",
        "fra": sid, "til": egne_nr[nr], "objekt": None, "polaritet": "positiv", "avgrensning": None, "betinget": False,
        "kilde_utenfor_korpus": True, "sikkerhet": "hoy", "kommentar": "",
    }, f"Ny #312: hvilken kommune tettstedet ligger i, står ikke i teksten — Kartverkets SSR (stedsnummer {t['stedsnummer']}, kommune {nr}), avgrenset til tingrettens egne kommuner."))
    tettsteder += 1

# Aktørlista: fjern rk_*, sammenslåtte sted_* og fylkesgrupper uten utsagn.
brukt = {x for u in utsagn for x in (u["fra"], u["til"]) if x}
for i in list(aktorer):
    if i.startswith("rk_") or i in erstatt or (i.startswith("fgruppe_") and i not in brukt):
        del aktorer[i]
for k in set(erstatt.values()):
    aktorer[k]["verifisert_av"] = VERIFISERT
d["aktorer"] = [a for a in d["aktorer"] if a["id"] in aktorer]
d["utsagn"] = utsagn

with open(FASIT, "w", encoding="utf-8", newline="\n") as f:
    json.dump(d, f, ensure_ascii=False, indent=1)
    f.write("\n")

print("Fjernet:", fjernet)
print(f"tingrett → kommune: {sum(len(v) for v in tr_kommuner.values())}; tingrett sogner til lagsogn: "
      f"{sum(1 for u in utsagn if u['type'] == 'annet:sogner_til')}")
print(f"Rettssteder slått sammen med kommune: {sammenslatt}; tettsteder som område + del_av: {tettsteder}")
print("Uløste steder:", uloste or "ingen")
print(f"Aktører {len(d['aktorer'])}, utsagn {len(d['utsagn'])}")
