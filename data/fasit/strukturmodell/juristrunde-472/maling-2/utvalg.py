"""Trekker utvalget for #472 måling 2 fra KOPIEN regelide_472pr4 etter rettingene i PR 4 (bare lesing). Kjør: python utvalg.py utvalg.json"""
import json, os, random, subprocess, sys
DB = "regelide_472pr4"
def q(sql):
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    wrapped = f"set default_transaction_read_only=on; begin read only; select coalesce(json_agg(t),'[]'::json) from ({sql}) t; commit;"
    r = subprocess.run(["podman","exec","-i","regel-ide-postgres-1","psql","-U","postgres","-d",DB,"-At","-v","ON_ERROR_STOP=1"],
                       input=wrapped.encode("utf-8"), capture_output=True, env=env)
    if r.returncode: raise RuntimeError(r.stderr.decode("utf-8","replace"))
    lines=[l for l in r.stdout.decode("utf-8").splitlines() if l.strip() not in ("SET","BEGIN","COMMIT")]
    return json.loads(chr(10).join(lines))
FRO = 20261125
rows = q("""select f."Id"::text id, f.ekstern_nokkel, f.undertype, r.dokument_id, split_part(r.dokument_id,'/',1) prefiks
 from funn f join funn_koblinger k on k.funn_id=f."Id" and k.rolle='gjelder_bestemmelse' join rettskilder r on r."Id"=k.rettskilde_id
 where f.kategori='hjemmel_opphevet' and f.status='oppdaget' order by f.ekstern_nokkel""")
assert len(rows)==264, len(rows)
# Ingen av H01–H20 fra måling 1.
maling1 = {f["forskrift"] for f in json.load(open(os.path.join(os.path.dirname(__file__), "..", "resultat.json"), encoding="utf-8"))["funn"]}
rows = [r for r in rows if r["dokument_id"] not in maling1]
assert len(rows)==250, len(rows)
# Om lag proporsjonalt med populasjonen (250), med minst to i hver undertype som finnes. Gjenstandsløs har bare H19, som er utelatt.
KVOTE = {
 "hjemmel_opphevet": {"DEL":7,"LTI":3,"SF":2},
 "blandede_hjemler": {"SF":3,"LTI":1},
 "uavklart": {"SF":1,"DEL":1},
 "videreforing_utlopt": {"SF":2},
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
for i,r in enumerate(valgt,1): r["kort"]=f"M{i:02d}"
lagstr = {}
for r in rows: lagstr[f'{r["undertype"]}/{r["prefiks"]}'] = lagstr.get(f'{r["undertype"]}/{r["prefiks"]}',0)+1
json.dump({"frø":FRO,"metode":"Lag = (undertype, prefiks). Innen hvert lag sorteres funnene på ekstern_nokkel og stokkes med random.Random(frø) i fast lagrekkefølge (KVOTE under); de n første trekkes. Uten tilbakelegging. H01–H20 er utelatt.",
           "populasjon":{"funn":264,"uten_maling1":len(rows),"kategori":"hjemmel_opphevet","status":"oppdaget","database":DB,"regelversjon":"472-pr4-2026-11-24","lag":lagstr},
           "kvote":KVOTE,"utvalg":valgt}, open(sys.argv[1],"w",encoding="utf-8"), ensure_ascii=False, indent=1)
for r in valgt: print(r["kort"], r["undertype"], r["dokument_id"])
