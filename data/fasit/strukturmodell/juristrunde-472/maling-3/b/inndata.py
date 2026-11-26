"""Måling 3b: utvalg og vurderingskort for de NYE funnene etter prosjekteierens avgjørelse 2026-11-25 (alternativ (a)), fra kopien regelide_501b
(bare lesing). Kjør: python inndata.py utvalg.json inndata.json

Populasjonen er funnene «Hjemmel opphevet» som står som oppdaget etter kjøringen, men som ikke fantes før (ikke i tabellen snap_funn, tatt i
kopien før kjøringen). Alle er undertype uavklart: en hjemmel er opphevet og videreført av en overgangsregel som ikke nevner vedtakstypen, og
ingen annen premiss som bærer, er vurdert. Lagene er prefikset, med frø 20261127.
"""
import json, os, random, subprocess, sys

DB = "regelide_501b"
FRO = 20261127
KVOTE = {"DEL": 12, "INS": 2, "LTI": 1, "SF": 1}


def q(sql):
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    wrapped = f"set default_transaction_read_only=on; begin read only; select coalesce(json_agg(t),'[]'::json) from ({sql}) t; commit;"
    r = subprocess.run(["podman", "exec", "-i", "regel-ide-postgres-1", "psql", "-U", "postgres", "-d", DB, "-At", "-v", "ON_ERROR_STOP=1"],
                       input=wrapped.encode("utf-8"), capture_output=True, env=env)
    if r.returncode: raise RuntimeError(r.stderr.decode("utf-8", "replace"))
    lines = [l for l in r.stdout.decode("utf-8").splitlines() if l.strip() not in ("SET", "BEGIN", "COMMIT")]
    return json.loads(chr(10).join(lines))


def lit(s): return "'" + s.replace("'", "''") + "'"


nye = q("""select f."Id"::text id, f.ekstern_nokkel, f.undertype, r.dokument_id, split_part(r.dokument_id,'/',1) prefiks
 from funn f join funn_koblinger k on k.funn_id=f."Id" and k.rolle='gjelder_bestemmelse' join rettskilder r on r."Id"=k.rettskilde_id
 where f.kategori='hjemmel_opphevet' and f.status='oppdaget' and f."Id" not in (select "Id" from snap_funn) order by f.ekstern_nokkel""")
lag = {}
for r in nye: lag.setdefault(r["prefiks"], []).append(r)
rng = random.Random(FRO)
valgt = []
for p, n in KVOTE.items():
    l = sorted(lag.get(p, []), key=lambda r: r["ekstern_nokkel"])
    rng.shuffle(l)
    valgt += l[:n]
for i, r in enumerate(valgt, 1): r["kort"] = f"N{i:02d}"
json.dump({"frø": FRO, "metode": "Lag = prefiks. Sortert på ekstern_nokkel og stokket med random.Random(frø) i fast lagrekkefølge (KVOTE); de n første.",
           "populasjon": {"nye_funn": len(nye), "per_prefiks": {p: len(v) for p, v in lag.items()},
                          "per_undertype": {u: sum(1 for r in nye if r["undertype"] == u) for u in {r["undertype"] for r in nye}}, "database": DB},
           "kvote": KVOTE, "utvalg": valgt}, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=1)

kort = []
for u in valgt:
    f = q(f'select "Id"::text id, ekstern_nokkel, undertype, tittel, beskrivelse from funn where "Id"={lit(u["id"])}')[0]
    rk = q(f"""select "Id"::text id, dokument_id, tittel, annet_om_dokumentet, ikrafttredelse, rent_endringsvedtak, kildeform
               from rettskilder where dokument_id={lit(u["dokument_id"])} and entitetsstatus='gjeldende' and importrolle='primaer'""")[0]
    kob = q(f"""select k.rolle, k.etikett, k.merknad, k.eid from funn_koblinger k where k.funn_id={lit(u["id"])} order by k.sortering""")
    hs = q(f"""select h.hjemmel_eid hjemmel, m.dokument_id i_korpus, h.rolle, h.rolle_regel, h.status, h.bevistrinn, o.dokument_id opphevet_av,
                      h.opphevet_av_eid, h.opphevet_av_sitat, v.dokument_id videreforing_kilde, h.videreforing_eid, h.videreforing_form,
                      h.videreforing_forbehold, h.videreforing_dekning, h.videreforing_sitat, h.kunnskapsstatus
               from hjemmelsstatus h left join rettskilder m on m."Id"=h.mal_rettskilde_id left join rettskilder o on o."Id"=h.opphevet_av_rettskilde_id
               left join rettskilder v on v."Id"=h.videreforing_rettskilde_id where h.rettskilde_id={lit(rk["id"])} order by h.hjemmel_eid""")
    hs = [{k: v for k, v in h.items() if v is not None} for h in hs]
    kort.append({"id": u["kort"], "funn_id": f["id"], "ekstern_nokkel": f["ekstern_nokkel"], "undertype": f["undertype"], "prefiks": u["prefiks"],
                 "forskrift": {"dokument_id": rk["dokument_id"], "tittel": rk["tittel"], "lovdatas_hjemmelstekst": rk["annet_om_dokumentet"],
                               "ikrafttredelse": rk["ikrafttredelse"], "rent_endringsvedtak": rk["rent_endringsvedtak"], "kildeform": rk["kildeform"]},
                 "foreslatt": {"tittel": f["tittel"], "pastand": f["beskrivelse"], "koblinger": kob, "hjemmelsstatus": hs}})

json.dump({"sak": "#472 måling 3b (#501: nye funn etter prosjekteierens avgjørelse 2026-11-25, alternativ (a))", "dato": "2026-11-25",
           "grunnlag": f"Kopien {DB} (bare SELECT) etter PR 5 med alternativ (a): {len(nye)} nye funn, alle undertype uavklart. Utvalget står i utvalg.json.",
           "klasser": {"riktig": "«Uavklart» er et riktig svar: hjemmelen er opphevet, overgangsregelen som er koblet, nevner ikke vedtakstypen, og om den "
                                 "dekker vedtaket, er et reelt spørsmål. Påstanden er riktig.",
                       "feil": "Vedtaket er klart dekket av overgangsregelen (videreført), klart ikke dekket (hjemmel opphevet), eller påstanden er feil "
                               "på et annet punkt (for eksempel at vedtaket selv er opphevet).",
                       "usikker": "kan ikke avgjøres fra kildene"},
           "feilarsaker": ["dekningen", "populasjonen", "tekstlesingen", "rollen", "videreforingsformen", "pastanden", "annet"],
           "kort": kort}, open(sys.argv[2], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(nye), "nye;", len(kort), "kort")
for c in kort: print(c["id"], c["forskrift"]["dokument_id"], c["forskrift"]["tittel"][:80])
