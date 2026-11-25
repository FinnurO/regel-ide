"""Trekker utvalget for #472 måling 3 fra KOPIEN regelide_501 etter #501 (PR 5), bare lesing. Kjør: python utvalg.py utvalg.json

To utvalg:
- Endret (E01–E15): funn som sto som oppdaget før PR 5 (tabellen snap_funn, tatt i kopien før kjøringen) og som PR 5 forklarte bort, fordi
  viltloven nå er videreført av viltressursloven § 61 andre ledd. M13 fra måling 2 er utelatt.
- Kontroll (K01–K08): funn som fortsatt står som oppdaget, der en hjemmel som faller, er en lov med kortnavn i tittelen. Fire der kortnavnet er
  entydig (men ingen V4-setning ble funnet), og fire der kortnavnet er tvetydig (regelen gir ikke svar). Det er der PR 5 kunne ha oversett
  noe.
"""
import json, os, random, subprocess, sys
DB = "regelide_501"
def q(sql):
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    wrapped = f"set default_transaction_read_only=on; begin read only; select coalesce(json_agg(t),'[]'::json) from ({sql}) t; commit;"
    r = subprocess.run(["podman","exec","-i","regel-ide-postgres-1","psql","-U","postgres","-d",DB,"-At","-v","ON_ERROR_STOP=1"],
                       input=wrapped.encode("utf-8"), capture_output=True, env=env)
    if r.returncode: raise RuntimeError(r.stderr.decode("utf-8","replace"))
    lines=[l for l in r.stdout.decode("utf-8").splitlines() if l.strip() not in ("SET","BEGIN","COMMIT")]
    return json.loads(chr(10).join(lines))
FRO = 20261126
HER = os.path.dirname(os.path.abspath(__file__))
# Kortnavnet per opphevet lov, fra rapporten til hjemmelsstatusen (?medRader=true) i kopien: kortnavn.json.
kortnavn = {x["eli"]: x for x in json.load(open(os.path.join(HER, "kortnavn.json"), encoding="utf-8"))}

endret = q("""select s."Id"::text id, s.ekstern_nokkel, s.undertype, r.dokument_id, split_part(r.dokument_id,'/',1) prefiks
 from snap_funn s join funn f on f."Id"=s."Id" join funn_koblinger k on k.funn_id=f."Id" and k.rolle='gjelder_bestemmelse'
 join rettskilder r on r."Id"=k.rettskilde_id
 where s.status='oppdaget' and f.status='forklart_bort' order by s.ekstern_nokkel""")
assert len(endret)==23, len(endret)
endret = [r for r in endret if r["dokument_id"] != "SF/forskrift/2019-12-17-1878"]  # M13
KVOTE_E = {"blandede_hjemler": {"SF":7,"DEL":1}, "hjemmel_opphevet": {"DEL":3,"LTI":2,"SF":1}, "uavklart": {"DEL":1}}

kontroll = q("""select distinct f."Id"::text id, f.ekstern_nokkel, f.undertype, r.dokument_id, split_part(r.dokument_id,'/',1) prefiks, h.hjemmel_eli
 from funn f join funn_koblinger k on k.funn_id=f."Id" and k.rolle='gjelder_bestemmelse' join rettskilder r on r."Id"=k.rettskilde_id
 join hjemmelsstatus h on h.rettskilde_id=r."Id" and h.status in ('opphevet','videreforing_utlopt') and h.hjemmel_eli like '%/eli/lov/%'
 where f.kategori='hjemmel_opphevet' and f.status='oppdaget' order by f.ekstern_nokkel""")
for r in kontroll: r["kortnavn"] = kortnavn.get(r["hjemmel_eli"], {"grunn":"ingen_tittel_med_kortnavn"})
lagK = {"entydig": [], "kortnavnet_brukes_om_flere_lover": []}
sett_k = {}
for r in kontroll:
    g = r["kortnavn"]["grunn"]
    if g in lagK and r["id"] not in sett_k: sett_k[r["id"]] = r; lagK[g].append(r)
KVOTE_K = {"entydig": 4, "kortnavnet_brukes_om_flere_lover": 4}

rng = random.Random(FRO)
valgt = []
for ut in KVOTE_E:
    for pref, n in KVOTE_E[ut].items():
        lag = sorted([r for r in endret if r["undertype"]==ut and r["prefiks"]==pref], key=lambda r: r["ekstern_nokkel"])
        rng.shuffle(lag)
        valgt += [dict(r, utvalg="endret") for r in lag[:n]]
for i, r in enumerate(valgt, 1): r["kort"] = f"E{i:02d}"
kvalgt = []
for g, n in KVOTE_K.items():
    lag = sorted(lagK[g], key=lambda r: r["ekstern_nokkel"])
    rng.shuffle(lag)
    kvalgt += [dict(r, utvalg="kontroll") for r in lag[:n]]
for i, r in enumerate(kvalgt, 1): r["kort"] = f"K{i:02d}"
json.dump({"frø": FRO,
           "metode": "Endret: lag = (undertype før PR 5, prefiks), sortert på ekstern_nokkel og stokket med random.Random(frø) i fast lagrekkefølge; de n første. "
                     "Kontroll: lag = kortnavnets lesing for en lov som faller (entydig / brukes om flere lover), samme metode. Uten tilbakelegging.",
           "populasjon": {"endret": 23, "endret_uten_M13": len(endret), "kontroll_entydig": len(lagK["entydig"]),
                          "kontroll_tvetydig": len(lagK["kortnavnet_brukes_om_flere_lover"]), "oppdaget_etter": 241, "database": DB,
                          "regelversjon": "hjemmelsstatus 472-pr5-2026-11-25, funnregel 472-pr4-2026-11-24"},
           "kvote": {"endret": KVOTE_E, "kontroll": KVOTE_K}, "utvalg": valgt + kvalgt},
          open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
for r in valgt + kvalgt: print(r["kort"], r["undertype"], r["dokument_id"], r.get("hjemmel_eli", ""), r.get("kortnavn", {}).get("kortnavn", ""))
print({g: len(v) for g, v in lagK.items()})
