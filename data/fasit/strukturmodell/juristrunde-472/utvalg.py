"""Trekker utvalget for #472 måling 1 (bare lesing mot den delte basen). Kjør: python utvalg.py utvalg.json"""
import json, os, subprocess
def q(sql):
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    wrapped = f"set default_transaction_read_only=on; begin read only; select coalesce(json_agg(t),'[]'::json) from ({sql}) t; commit;"
    r = subprocess.run(["podman","exec","-i","regel-ide-postgres-1","psql","-U","postgres","-d","regelide","-At","-v","ON_ERROR_STOP=1"],
                       input=wrapped.encode("utf-8"), capture_output=True, env=env)
    if r.returncode: raise RuntimeError(r.stderr.decode("utf-8","replace"))
    lines=[l for l in r.stdout.decode("utf-8").splitlines() if l.strip() not in ("SET","BEGIN","COMMIT")]
    return json.loads(chr(10).join(lines))
import json, random, sys
FRO = 20261124
rows = q("""select f."Id"::text id, f.ekstern_nokkel, f.undertype, r.dokument_id, split_part(r.dokument_id,'/',1) prefiks
 from funn f join funn_koblinger k on k.funn_id=f."Id" and k.rolle='gjelder_bestemmelse' join rettskilder r on r."Id"=k.rettskilde_id
 where f.kategori='hjemmel_opphevet' and f.status='oppdaget' order by f.ekstern_nokkel""")
assert len(rows)==319, len(rows)
KVOTE = {
 "hjemmel_opphevet": {"DEL":6,"LTI":3,"SF":1},
 "blandede_hjemler": {"SF":2,"LTI":1,"DEL":1},
 "uavklart": {"SF":1,"LTI":1},
 "videreforing_utlopt": {"SF":1,"DEL":1},
 "gjenstandslos": {"DEL":2},
}
rng = random.Random(FRO)
valgt=[]; sett=set()
for ut in KVOTE:
    for pref,n in KVOTE[ut].items():
        lag=[r for r in rows if r["undertype"]==ut and r["prefiks"]==pref]
        lag.sort(key=lambda r:r["ekstern_nokkel"])
        rng.shuffle(lag)
        for r in lag[:n]:
            assert r["id"] not in sett; sett.add(r["id"]); valgt.append(r)
for i,r in enumerate(valgt,1): r["kort"]=f"H{i:02d}"
json.dump({"frø":FRO,"metode":"Lag = (undertype, prefiks). Innen hvert lag sorteres funnene på ekstern_nokkel og stokkes med random.Random(frø) i fast lagrekkefølge (KVOTE under); de n første trekkes. Uten tilbakelegging.",
           "populasjon":{"funn":len(rows),"kategori":"hjemmel_opphevet","status":"oppdaget","regelversjon":"472-pr2-2026-11-24"},
           "kvote":KVOTE,"utvalg":valgt}, open(sys.argv[1],"w",encoding="utf-8"), ensure_ascii=False, indent=1)
for r in valgt: print(r["kort"], r["undertype"], r["dokument_id"])
