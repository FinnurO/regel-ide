"""Måling 4, del #502: vurderingskort for de endrede funnene og et kontrollutvalg, fra kopien regelide_502_jurist (bare lesing).
Kjør: python inndata.py inndata.json

Kopien er en klone av regelide_502 etter kaldkjøringen med #502 (hjemmelsstatus, «Hjemmel opphevet» og «Utgått aktør» skrevet med
?skriv=true&oppfrisk=true). Endringene er lest av en øyeblikkskopi av tabellene før og etter:
- E01–E08: #472-funn som går fra oppdaget til forklart bort, fordi forskriften selv er opphevet av en annen kilde (R8, nye former);
- E09: et nytt #472-funn, fordi hjemmelsloven (privatskulelova 1985) nå leses som opphevet (E2);
- E10: et #429-funn som forklares bort, og kilden går til kildefeilkøen (R8);
- E11: en hjemmelsrad som går fra ukjent til erstattet (skiltforskriftene 1980, E1).
Kontrollutvalget er setninger fra sveipet (r8-setninger.tsv) som ikke endret noe funn: K01–K06 nye hele opphevinger (frø 20261127, trukket
blant dem som ikke er i E-kortene og har nummer), K07–K10 setninger som var godtatt og nå avvises (fire av de ni med nummer), og K11 setningen
som avgrenses av neste setning (LTI/forskrift/2016-12-13-1596).
"""
import json, os, random, subprocess, sys

DB = "regelide_502_jurist"
FRO = 20261127
HER = os.path.dirname(os.path.abspath(__file__))


def q(sql):
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    wrapped = f"set default_transaction_read_only=on; begin read only; select coalesce(json_agg(t),'[]'::json) from ({sql}) t; commit;"
    r = subprocess.run(["podman", "exec", "-i", "regel-ide-postgres-1", "psql", "-U", "postgres", "-d", DB, "-At", "-v", "ON_ERROR_STOP=1"],
                       input=wrapped.encode("utf-8"), capture_output=True, env=env)
    if r.returncode: raise RuntimeError(r.stderr.decode("utf-8", "replace"))
    lines = [l for l in r.stdout.decode("utf-8").splitlines() if l.strip() not in ("SET", "BEGIN", "COMMIT")]
    return json.loads(chr(10).join(lines))


def lit(s): return "'" + s.replace("'", "''") + "'"


def kilde(dok):
    return q(f"""select "Id"::text id, dokument_id, tittel, annet_om_dokumentet, ikrafttredelse, rent_endringsvedtak, kildeform
                 from rettskilder where dokument_id={lit(dok)} and entitetsstatus='gjeldende' and importrolle='primaer'""")[0]


def node(dok, rel):
    return q(f"""select n.eid, n.tekst, p.tekst forelder from rettskilde_noder n join rettskilder r on r."Id"=n.rettskilde_id
                 left join rettskilde_noder p on p."Id"=n.parent_node_id
                 where r.dokument_id={lit(dok)} and r.entitetsstatus='gjeldende' and n.entitetsstatus='gjeldende' and (n.eid={lit(rel)} or n.eid like {lit('%/nor/' + rel)})""")


def funn(nokkel):
    f = q(f"""select "Id"::text id, ekstern_nokkel, kategori, status, undertype, tittel, beskrivelse, forklart_bort_begrunnelse from funn
              where ekstern_nokkel={lit(nokkel)}""")[0]
    f["koblinger"] = q(f"""select rolle, etikett, merknad, eid from funn_koblinger where funn_id={lit(f["id"])} order by sortering""")
    return f


def hjemmelsstatus(rk_id):
    hs = q(f"""select h.hjemmel_eid hjemmel, h.rolle, h.status, h.bevistrinn, o.dokument_id opphevet_av, h.opphevet_av_eid, h.opphevet_av_sitat,
                      v.dokument_id videreforing_kilde, h.videreforing_form, h.videreforing_dekning, h.videreforing_sitat, h.kunnskapsstatus
               from hjemmelsstatus h left join rettskilder o on o."Id"=h.opphevet_av_rettskilde_id
               left join rettskilder v on v."Id"=h.videreforing_rettskilde_id where h.rettskilde_id={lit(rk_id)} order by h.hjemmel_eid""")
    return [{k: v for k, v in h.items() if v is not None} for h in hs]


def gjelder(f):
    return q(f"""select r.dokument_id from funn_koblinger k join rettskilder r on r."Id"=k.rettskilde_id where k.funn_id={lit(f["id"])}
                 and k.rolle='gjelder_bestemmelse' limit 1""")[0]["dokument_id"]


E = [  # (nøkkel, opphevende kilde, relativ eId)
    ("hjemmel-opphevet:forskrift/2001/06/29/721/nor", "DEL/forskrift/2018-12-20-2052", "punkt-5/punkt-2"),
    ("hjemmel-opphevet:forskrift/2003/06/27/802/nor", "DEL/forskrift/2018-12-20-2052", "punkt-5/punkt-3"),
    ("hjemmel-opphevet:forskrift/2004/02/12/1221/nor", "DEL/forskrift/2018-12-20-2052", "punkt-5/punkt-4"),
    ("hjemmel-opphevet:forskrift/2003/07/04/900/nor", "DEL/forskrift/2018-12-20-2052", "punkt-5/punkt-1"),
    ("hjemmel-opphevet:forskrift/2005/09/26/1093/nor", "DEL/forskrift/2008-12-19-1538", "kap-II/ledd-1"),
    ("hjemmel-opphevet:forskrift/2014/09/11/1192/nor", "LTI/forskrift/2018-12-14-1920", "kap-II/ledd-1/punkt-1"),
    ("hjemmel-opphevet:forskrift/2017/03/18/357/nor", "LTI/forskrift/2019-07-02-961", "kap-II/ledd-1"),
    ("hjemmel-opphevet:forskrift/2018/04/16/852/nor", "LTI/forskrift/2021-11-14-3414", "kap-I/ledd-1"),
]
kort = []
for i, (nokkel, opph, rel) in enumerate(E, 1):
    f = funn(nokkel)
    k = kilde(gjelder(f))
    kort.append({"id": f"E{i:02d}", "gjelder": "#472: forklart bort fordi forskriften selv er opphevet (R8)", "funn": f,
                 "forskrift": k, "hjemmelsstatus": hjemmelsstatus(k["id"]),
                 "opphevende_kilde": kilde(opph) | {"node": node(opph, rel)}})
f = funn("hjemmel-opphevet:forskrift/2003/02/13/224/nor")
k = kilde("SF/forskrift/2003-02-13-224")
kort.append({"id": "E09", "gjelder": "#472: nytt funn, hjemmelsloven privatskulelova 1985 leses nå som opphevet (E2, uten «Endrer»)", "funn": f,
             "forskrift": k, "hjemmelsstatus": hjemmelsstatus(k["id"]),
             "opphevende_kilde": kilde("DEL/forskrift/2003-07-04-896") | {"node": node("DEL/forskrift/2003-07-04-896", "punkt-3")}})
f = funn(q("""select ekstern_nokkel from funn where ekstern_nokkel like 'utgatt-aktor:forskrift/2014/09/11/1192/nor:%'""")[0]["ekstern_nokkel"])
k = kilde("LTI/forskrift/2014-09-11-1192")
kf = q(f"""select status, type, beskrivelse from kildefeil where rettskilde_id={lit(k["id"])}""")
kort.append({"id": "E10", "gjelder": "#429: forklart bort, kilden går til kildefeilkøen (R8)", "funn": f, "forskrift": k, "kildefeil": kf,
             "opphevende_kilde": kilde("LTI/forskrift/2018-12-14-1920") | {"node": node("LTI/forskrift/2018-12-14-1920", "kap-II/ledd-1/punkt-1")}})
k = kilde("LTI/forskrift/2005-11-09-1278")
kort.append({"id": "E11", "gjelder": "Hjemmelsstatus: skiltforskriftene 1980 som hjemmel (kompetanseledd) går fra ukjent til erstattet (E1). Ingen funn endres.",
             "forskrift": k, "hjemmelsstatus": hjemmelsstatus(k["id"]),
             "opphevende_kilde": kilde("SF/forskrift/2005-10-07-1219") | {"node": node("SF/forskrift/2005-10-07-1219", "§47/ledd-2")}})

# Kontroll fra sveipet: (før, etter, kilde, eId, mål, sitat, hele teksten).
rader = [l.rstrip("\n").split("\t") for l in open(os.path.join(HER, "r8-setninger.tsv"), encoding="utf-8")]
e_maal = {"2001-6-29-721", "2003-6-27-802", "2004-2-12-1221", "2003-7-4-900", "2005-9-26-1093", "2014-9-11-1192", "2017-3-18-357", "2018-4-16-852",
          "1985-6-14-73", "1980-10-10-1"}
nye = sorted([r for r in rader if r[1] == "godtatt" and r[4] not in e_maal and not r[4].endswith("-")], key=lambda r: (r[2], r[3], r[4]))
rng = random.Random(FRO)
rng.shuffle(nye)
for i, r in enumerate(nye[:6], 1):
    kort.append({"id": f"K{i:02d}", "gjelder": "Kontroll: setning som før ble lest som delvis og nå som hel oppheving", "kilde": r[2], "eid": r[3],
                 "maal": r[4], "sitat": r[5], "hele_teksten": r[6]})
onsket = {("DEL/forskrift/2002-12-20-1627", "1985-6-14-62"), ("DEL/forskrift/2003-12-19-1948", "1925-7-17-11"),
          ("SF/forskrift/2012-11-22-1088", "1971-5-21-47"), ("SF/forskrift/2022-12-20-2384", "2002-6-21-45")}
valgt = sorted([r for r in rader if r[0] == "godtatt" and (r[2], r[4]) in onsket], key=lambda r: (r[2], r[4]))
for i, r in enumerate(valgt, 7):
    kort.append({"id": f"K{i:02d}", "gjelder": "Kontroll: setning som før ble godtatt som hel oppheving og nå avvises av vakten", "kilde": r[2], "eid": r[3],
                 "maal": r[4], "hele_teksten": r[6]})
n = node("LTI/forskrift/2016-12-13-1596", "kap-II/ledd-2")
kort.append({"id": "K11", "gjelder": "Kontroll: «av myndighet» ville gjort setningen hel, men neste setning avgrenser opphevingen, så den leses som delvis",
             "kilde": "LTI/forskrift/2016-12-13-1596", "eid": "kap-II/ledd-2", "maal": "2010-3-18-425", "hele_teksten": n[0]["tekst"]})

json.dump({"sak": "#502 (R8: flere former for hel oppheving), måling 4 del 1", "dato": "2026-11-27",
           "grunnlag": f"Kopien {DB} etter kaldkjøringen med #502. Sveipet over alle (node, mål) i gjeldende primærkilder står i r8-setninger.tsv: "
                       "56 går fra avvist til godtatt, 9 fra godtatt til avvist av vakten, og 11 fra avvist til avvist av vakten.",
           "klasser": {"riktig": "E-kort: utfallet etter #502 er riktig (forskriften er opphevet i sin helhet av setningen, så funnet skal ikke stå; det nye "
                                 "funnet holder; hjemmelen er erstattet). K-kort: lesingen etter #502 er riktig (hel oppheving av målet, eller ikke en hel "
                                 "oppheving av målet).",
                       "feil": "utfallet eller lesingen etter #502 er feil", "usikker": "kan ikke avgjøres fra kildene"},
           "feilarsaker": ["tekstlesingen", "populasjonen", "dekningen", "rollen", "videreforingsformen", "pastanden", "annet"],
           "kort": kort}, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
print(len(kort), "kort")
for c in kort: print(c["id"], (c.get("forskrift") or {}).get("dokument_id") or c.get("kilde"), c.get("maal", ""))
