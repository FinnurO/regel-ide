#!/usr/bin/env python3
"""
Deterministisk konvertering av strukturfasiten etter issue #368 «oppgave/funksjon (kategori F)» (Johanns beslutning 2026-10-15,
docs/34 §6.1, docs/profil/02-begrepsmanual.md §F). Ingen rad endres for hånd og ingenting flyttes automatisk: hver endring står som et
avgjort kort i juristrunde-368-oppgave.json (utfallet av juristrunden, CLAUDE.md §23), og skriptet gjør nøyaktig det kortet sier.

    python data/fasit/strukturmodell/konvertering-368-oppgave.py            # fasitfilene
    python data/fasit/strukturmodell/konvertering-368-oppgave.py ki-utdata  # de lagrede KI-utdataene (#308)

Idempotent: en rad som alt er konvertert (kategori «oppgave»), og en F-rad som alt finnes (samme eId, type, fra og objekt), røres ikke.

REGLER

  O1. Utfall «F»: raden ER en oppgave/funksjon. Den beholder id, eId og sitat og får kategori «oppgave», typen, fra, til, objekt,
      modalitet, polaritet og avgrensning fra den første F-raden på kortet. Felt som bare hører til kompetanse (normform, undertype,
      grunnlag, delegeringsadgang, delegering_slutning) fjernes. Den gamle kategorien/typen står i kommentaren («[#368, juristrunden] var
      kompetanse/samordningskompetanse»). Flere F-rader på kortet blir nye rader med samme eId og sitat.
  O2. Utfall «begge»: raden står som den er (den sier noe annet og sant: A har_ansvarsomrade, R sekretariat_for/forvaltes_av …), og
      F-radene legges til som nye rader med samme eId og sitat. F gir aldri K eller P i seg selv; begge radene har hjemmel i teksten.
  O3. Utfall «behold»: ingenting endres. Kortet står i beslutningsfila som sporingsgrunnlag (også de som er søkestøy).
  O4. Kunnskapsstatus (bare fasiten — KI-utdataene får ingen, som i #367): F-radene får «fortolkning» med begrunnelse og
      tolkningsargumenter, eller «tekstfunn» når teksten selv bruker funksjonsordet («Forvaltningsansvaret for …»). Kunnskapsstatusen er
      juristrundens, ikke skriptets.
  O6. «raden» på et kort: kunnskapsstatus, begrunnelse og tolkningsargumenter settes på SELVE raden (bare fasiten). Brukt der en K-rad
      står ved siden av en F-rad fra samme ord (hotl. u91/u92, shl. u213: K oppnevning), så det synes at K-raden er en egen fortolkning
      og ikke sluttet fra oppgaven (juristrunde 2).
  O5. Rader kandidatsøket ikke fant (kortet viser til en id som ikke finnes, eller sitatet stemmer ikke): skriptet stopper og lister dem
      — en gjettet plassering er verre enn ingen.
"""
import json
import os
import sys

MAPPE = os.path.dirname(os.path.abspath(__file__))
BESLUTNINGER = os.path.join(MAPPE, "juristrunde-368-oppgave.json")
KOMPETANSEFELT = ["normform", "undertype", "grunnlag", "delegeringsadgang", "delegering_slutning"]
F_FELT = ["type", "fra", "til", "objekt", "modalitet", "polaritet", "avgrensning"]


def neste_id(d):
    return f"u{max(int(u['id'][1:]) for u in d['utsagn'] if u['id'][1:].isdigit()) + 1}"


def finnes(d, eid, fr):
    return any(u.get("kategori") == "oppgave" and u.get("eid") == eid and u.get("type") == fr["type"]
               and u.get("fra") == fr["fra"] and u.get("objekt") == fr["objekt"] for u in d["utsagn"])


def status_felt(fr, er_fasit):
    if not er_fasit:
        return {}
    ut = {"kunnskapsstatus": fr["kunnskapsstatus"]}
    if fr.get("begrunnelse"):
        ut["begrunnelse"] = fr["begrunnelse"]
    if fr["kunnskapsstatus"] == "fortolkning":
        ut["tolkningsargumenter"] = fr["tolkningsargumenter"]
    return ut


def ny_rad(d, kilde, fr, er_fasit, merknad):
    rad = {"id": neste_id(d), "eid": kilde["eid"], "sitat": kilde["sitat"], "kategori": "oppgave"}
    rad.update({k: fr.get(k) for k in F_FELT})
    rad["polaritet"] = fr.get("polaritet") or "positiv"
    rad["betinget"] = kilde.get("betinget", False)
    rad["kilde_utenfor_korpus"] = kilde.get("kilde_utenfor_korpus", False)
    rad["sikkerhet"] = kilde.get("sikkerhet", "middels")
    rad["kommentar"] = merknad
    rad.update(status_felt(fr, er_fasit))
    if kilde.get("oppdagelseskilde"):
        rad["oppdagelseskilde"] = kilde["oppdagelseskilde"]
    d["utsagn"].append(rad)
    return rad


def konverter(d, kort, er_fasit, logg):
    per_id = {u["id"]: u for u in d["utsagn"]}
    for k in kort:
        u = per_id.get(k["id"])
        if u is None or (k.get("sitat") and u.get("sitat") != k["sitat"]):
            raise SystemExit(f"O5: {k['nokkel']} finnes ikke (eller sitatet stemmer ikke) — avbrutt, ingenting skrevet.")
        if k.get("raden") and er_fasit and u.get("kunnskapsstatus") != k["raden"]["kunnskapsstatus"]:
            u.update(k["raden"])  # O6
            logg["raden"] += 1
        if k["utfall"] == "behold":
            logg["behold"] += 1
            continue
        rader = k["f_rader"]
        if k["utfall"] == "F":
            if u.get("kategori") == "oppgave":
                logg["alt_konvertert"] += 1
                continue
            gammel = f"{u.get('kategori')}/{u.get('type')}"
            forste, resten = rader[0], rader[1:]
            for f in KOMPETANSEFELT:
                u.pop(f, None)
            for f in ["kunnskapsstatus", "begrunnelse", "tolkningsargumenter"]:
                u.pop(f, None)
            u["kategori"] = "oppgave"
            u.update({f: forste.get(f) for f in F_FELT})
            u["polaritet"] = forste.get("polaritet") or "positiv"
            u.update(status_felt(forste, er_fasit))
            u["kommentar"] = f"[#368, juristrunden] var {gammel}." + (f" {u['kommentar']}" if u.get("kommentar") else "")
            logg["F"] += 1
            logg["f_rader"] += 1
        else:
            resten = rader
            logg["begge"] += 1
        for fr in resten:
            if finnes(d, u["eid"], fr):
                logg["alt_konvertert"] += 1
                continue
            ny_rad(d, u, fr, er_fasit, f"[#368, juristrunden] ny oppgaverad fra {u['id']} ({k['utfall']}).")
            logg["f_rader"] += 1


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    undermappe = sys.argv[1] if len(sys.argv) > 1 else None
    mappe = os.path.join(MAPPE, undermappe) if undermappe else MAPPE
    er_fasit = undermappe is None
    alle = json.load(open(BESLUTNINGER, encoding="utf-8"))["kort"]
    kilde = "fasit" if er_fasit else "ki"
    logg = {"F": 0, "begge": 0, "behold": 0, "f_rader": 0, "alt_konvertert": 0, "raden": 0}
    for lov in sorted({k["lov"] for k in alle if k["kilde"] == kilde}):
        sti = os.path.join(mappe, f"{lov}.json")
        raw = open(sti, encoding="utf-8").read()
        innrykk = len(raw.split("\n")[1]) - len(raw.split("\n")[1].lstrip())  # som #367: filens eget innrykk, ingen omformatering
        d = json.loads(raw)
        for_antall = len(d["utsagn"])
        konverter(d, [k for k in alle if k["kilde"] == kilde and k["lov"] == lov], er_fasit, logg)
        with open(sti, "w", encoding="utf-8", newline="\n") as f:
            f.write(json.dumps(d, ensure_ascii=False, indent=innrykk))
        print(f"{lov}: {for_antall} → {len(d['utsagn'])} utsagn")
    print(f"{kilde}: F {logg['F']}, begge {logg['begge']}, behold {logg['behold']}, F-rader skrevet {logg['f_rader']}, "
          f"alt konvertert {logg['alt_konvertert']}, kunnskapsstatus på raden {logg['raden']}")


if __name__ == "__main__":
    main()
