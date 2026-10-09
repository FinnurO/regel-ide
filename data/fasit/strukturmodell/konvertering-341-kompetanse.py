#!/usr/bin/env python3
"""
Deterministisk konvertering av strukturfasiten til kompetansemodellen fra issue #341 (Johanns beslutninger
2026-10-08). Ingen rad endres for hånd; alt under er regler som kan leses og kjøres om igjen.

    python data/fasit/strukturmodell/konvertering-341-kompetanse.py            # fasitfilene
    python data/fasit/strukturmodell/konvertering-341-kompetanse.py ki-utdata  # de lagrede KI-utdataene (#308)

Idempotent: kjøres den på en allerede konvertert fil, finnes ingen gamle typer, og ingenting endres.

Reglene (FORMAT.md er oppdatert tilsvarende):

  P1 — myndighetsrelasjonene flyttes fra relasjon til kompetanse, med "til" = motparten (samme retning):
       klageinstans_for → klagekompetanse, instruksjon → instruksjonskompetanse, omgjoring → omgjoringskompetanse,
       oppnevner → oppnevningskompetanse, tilsyn_med_aktor → tilsynskompetanse, og de fasiten hadde som annet:*:
       annet:avsetter → avsettingskompetanse, annet:sanksjonsmyndighet_over → sanksjonskompetanse,
       annet:samtykker_til → samtykkekompetanse, annet:overproving og annet:domstolsoverproving →
       overprovingskompetanse, annet:foreleggelse_til → foreleggingskompetanse, annet:reviderer → revisjonskompetanse.
  Delegering (Johanns beslutning 1): delegerer_til i et LEDSAGENDE DELEGERINGSVEDTAK (noden ligger — slått opp i
       noder/<kilde>.json — i en ledsagende kilde med tittel «Delegering …») er en GJENNOMFØRT delegering → relasjon/har_delegert_til; ellers (loven sier
       at X KAN delegere) → kompetanse/delegeringskompetanse. Unntakene i et delegeringsvedtak (negativ polaritet)
       beholdes som egne har_delegert_til-rader: de er avgrensning av delegeringen, ikke negativ kompetanse.
       [ENDRET, #356, 2026-10-09] Rettet i konvertering-356-fasitrester.py (R6): unntakene står nå i avgrensningen på den
       positive kanten, og de negative radene er slettet. Dette skriptet røres ikke (det konverterer bare de gamle typene).
  P2 — typologi og normform:
       forskriftskompetanse → normgivningskompetanse + normform «forskrift» (alle 205),
       annet:vedtektskompetanse → normgivningskompetanse + normform «vedtekter»,
       annet:reglementskompetanse og annet:intern_regelgivning → normgivningskompetanse + normformen ordet i SITATET
       sier (forskrift|reglement|arbeidsordning|vedtekter|instruks, første treff) — ellers normform null,
       delegeringsfullmakt → delegeringskompetanse, annet:<x>kompetanse → <x>kompetanse der <x> er en type i
       typologien (godkjenning, samtykke, sanksjon, overprøving, beslutning), annet:organiseringskompetanse →
       organisasjonskompetanse, annet:ansettelseskompetanse og annet:tilsettingskompetanse → ansettelseskompetanse,
       annet:samordningsansvar («skal samordne») → samordningskompetanse.
  representerer (Johanns R-liste): annet:representerer → relasjon/representerer.
  Delegerbar (#335 AC2, avgjort på SITATET, ikke på aktøren — fasitens «Kongen»-aktør har «Kongen i statsråd» som
       variant): bare på kompetanse der fra-aktøren er en Kongen-aktør — «Kongen i statsråd» i sitatet → false,
       ellers «Kongen» i sitatet → true; og for alle kompetanser: «<fra-form> selv» i sitatet → false.
  Grunnlag settes aldri (NULL = ikke angitt; privatrettslig instruksjon avgjøres av et menneske).

Ikke konvertert, fordi beslutningen ikke er tatt (åpne spørsmål på #341): velger, radgir, bistar, samarbeider_med,
administrativt_underordnet, del_av (relasjon), annet:ankeinstans_for, annet:forelegges_for, annet:intern_forelegging,
konstituerende/oppretter og avvikler, og øvrige annet:*. Skriptet lister dem.
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

R_TIL_K = {
    "klageinstans_for": "klagekompetanse",
    "instruksjon": "instruksjonskompetanse",
    "omgjoring": "omgjoringskompetanse",
    "oppnevner": "oppnevningskompetanse",
    "tilsyn_med_aktor": "tilsynskompetanse",
    "annet:avsetter": "avsettingskompetanse",
    "annet:sanksjonsmyndighet_over": "sanksjonskompetanse",
    "annet:samtykker_til": "samtykkekompetanse",
    "annet:overproving": "overprovingskompetanse",
    "annet:domstolsoverproving": "overprovingskompetanse",
    "annet:foreleggelse_til": "foreleggingskompetanse",
    "annet:reviderer": "revisjonskompetanse",
}
K_TIL_K = {
    "delegeringsfullmakt": "delegeringskompetanse",
    "annet:godkjenningskompetanse": "godkjenningskompetanse",
    "annet:samtykkekompetanse": "samtykkekompetanse",
    "annet:sanksjonskompetanse": "sanksjonskompetanse",
    "annet:overprovingskompetanse": "overprovingskompetanse",
    "annet:beslutningskompetanse": "beslutningskompetanse",
    "annet:organiseringskompetanse": "organisasjonskompetanse",
    "annet:ansettelseskompetanse": "ansettelseskompetanse",
    "annet:tilsettingskompetanse": "ansettelseskompetanse",
    "annet:samordningsansvar": "samordningskompetanse",  # «X skal samordne …» — leksikonregelen samordne (#341)
}
NORMGIVNING = {  # gammel type → normform (None = les av sitatet)
    "forskriftskompetanse": "forskrift",
    "annet:vedtektskompetanse": "vedtekter",
    "annet:reglementskompetanse": None,
    "annet:intern_regelgivning": None,
}
NORMFORMER = ["forskrift", "reglement", "arbeidsordning", "vedtekter", "instruks"]


def normform_fra_sitat(sitat):
    treff = [(m.start(), nf) for nf in NORMFORMER for m in [re.search(r"\b" + nf, sitat, re.IGNORECASE)] if m]
    return min(treff)[1] if treff else None


def dokument_for(kilde):
    """eId → ELI-en til dokumentet noden står i, fra den eksporterte nodefila. Noen ledsagende kilder har relative eId-er
    («kap-I/ledd-2» i energilovens delegeringsvedtak), så prefikset alene avgjør ikke dokumentet."""
    noder = json.load(open(os.path.join(HER, "noder", kilde + ".json"), encoding="utf-8"))
    return {n["eid"]: n["eli"] for n in noder}


def konverter(d, eid_til_eli):
    endret = collections.Counter()
    kongen = {a["id"] for a in d["aktorer"] if a["tekstform"].lower() == "kongen" or a["tekstform"].lower().startswith("kongen i statsr")}
    former = {a["id"]: [a["tekstform"]] + list(a.get("varianter") or []) for a in d["aktorer"]}
    delegeringsdokumenter = [l["eli"] for l in d.get("ledsagende", []) if l["tittel"].lower().startswith("delegering")]
    for u in d["utsagn"]:
        gammel = (u["kategori"], u["type"])
        if u["kategori"] == "relasjon" and u["type"] in R_TIL_K:
            u["kategori"], u["type"] = "kompetanse", R_TIL_K[u["type"]]
        elif u["kategori"] == "relasjon" and u["type"] == "delegerer_til":
            if eid_til_eli.get(u["eid"]) in delegeringsdokumenter:
                u["type"] = "har_delegert_til"
            else:
                u["kategori"], u["type"] = "kompetanse", "delegeringskompetanse"
        elif u["kategori"] == "relasjon" and u["type"] == "annet:representerer":
            u["type"] = "representerer"
        elif u["kategori"] == "kompetanse" and u["type"] in K_TIL_K:
            u["type"] = K_TIL_K[u["type"]]
        elif u["kategori"] == "kompetanse" and u["type"] in NORMGIVNING:
            nf = NORMGIVNING[u["type"]] or normform_fra_sitat(u["sitat"])
            u["type"] = "normgivningskompetanse"
            if nf is not None:
                u["normform"] = nf
        if (u["kategori"], u["type"]) != gammel:
            endret[f"{gammel[0]}/{gammel[1]} → {u['kategori']}/{u['type']}"] += 1

        if u["kategori"] == "kompetanse" and "delegerbar" not in u:
            sitat = u["sitat"]
            verdi = None
            if u.get("fra") in kongen:
                if "Kongen i statsråd" in sitat:
                    verdi = False
                elif re.search(r"\bKongen", sitat):
                    verdi = True
            if u.get("fra") and any(re.search(re.escape(f) + r"\s+selv\b", sitat, re.IGNORECASE) for f in former.get(u["fra"], [])):
                verdi = False
            if verdi is not None:
                u["delegerbar"] = verdi
                endret[f"delegerbar = {str(verdi).lower()}"] += 1
    return endret


def tell(d):
    return collections.Counter((u["kategori"], u["type"]) for u in d["utsagn"])


totalt_for, totalt_etter, alle_endringer = collections.Counter(), collections.Counter(), collections.Counter()
for kilde in KILDER:
    sti = os.path.join(MAPPE, kilde + ".json")
    if not os.path.exists(sti):
        continue
    raw = open(sti, encoding="utf-8").read().replace("\r\n", "\n")
    innrykk = len(raw.split("\n")[1]) - len(raw.split("\n")[1].lstrip())
    d = json.loads(raw)
    antall_for = len(d["utsagn"])
    totalt_for.update(tell(d))
    endret = konverter(d, dokument_for(kilde))
    assert len(d["utsagn"]) == antall_for, "ingen rader skal forsvinne"
    totalt_etter.update(tell(d))
    alle_endringer.update(endret)
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
print("\nIkke konvertert (avventer Johanns beslutning, #341):")
for (k, t), v in sorted(totalt_etter.items()):
    if (k == "relasjon" and t in {"velger", "radgir", "bistar", "samarbeider_med", "administrativt_underordnet", "del_av",
                                  "annet:ankeinstans_for", "annet:forelegges_for", "annet:intern_forelegging"}) \
            or (k == "konstituerende" and t in {"oppretter", "avvikler"}):
        print(f"  {v:4}  {k}/{t}")
