"""Måling 4, del #514: vurderingskort for enkeltvedtakene og et kontrollutvalg, fra kopien regelide_502 etter kaldkjøringen med #514 (bare lesing).
Kjør: python inndata.py inndata.json

- V01–V05: alle forskriftene regelen leser som enkeltvedtak (hele populasjonen av treff, 5). For V01–V04 endres dekningen fra «dokumenttype ikke
  nevnt» til «dekket», og funnet «Hjemmel opphevet» (uavklart) forklares bort. V05 har en aktiv hjemmel og endrer ingenting.
- K01–K12: kontroll for det regelen IKKE leser som enkeltvedtak. K01–K10 er de gjenværende funnene med undertype uavklart i INS og SF (utenom de
  fire sentralbankforskriftene om tilbaketrekking, der bare én er med), K11–K12 er de to vedtakene der prototypen fant en part som ikke står i tittelen.
"""
import json, os, subprocess, sys

DB = "regelide_502"


def q(sql):
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    wrapped = f"set default_transaction_read_only=on; begin read only; select coalesce(json_agg(t),'[]'::json) from ({sql}) t; commit;"
    r = subprocess.run(["podman", "exec", "-i", "regel-ide-postgres-1", "psql", "-U", "postgres", "-d", DB, "-At", "-v", "ON_ERROR_STOP=1"],
                       input=wrapped.encode("utf-8"), capture_output=True, env=env)
    if r.returncode: raise RuntimeError(r.stderr.decode("utf-8", "replace"))
    lines = [l for l in r.stdout.decode("utf-8").splitlines() if l.strip() not in ("SET", "BEGIN", "COMMIT")]
    return json.loads(chr(10).join(lines))


def lit(s): return "'" + s.replace("'", "''") + "'"


def kort_for(dok):
    rk = q(f"""select "Id"::text id, dokument_id, tittel, kortnavn, annet_om_dokumentet, ikrafttredelse, rent_endringsvedtak
               from rettskilder where dokument_id={lit(dok)} and entitetsstatus='gjeldende' and importrolle='primaer'""")[0]
    rk["innhold"] = [n["tekst"] for n in q(f"""select tekst from rettskilde_noder where rettskilde_id={lit(rk["id"])} and entitetsstatus='gjeldende'
                                                and not er_merknad and tekst is not null order by sorteringsrekkefolge""")]
    hs = q(f"""select h.hjemmel_eid hjemmel, h.rolle, h.status, h.bevistrinn, o.dokument_id opphevet_av, h.opphevet_av_sitat,
                      v.dokument_id videreforing_kilde, h.videreforing_eid, h.videreforing_form, h.videreforing_dekning, h.videreforing_sitat
               from hjemmelsstatus h left join rettskilder o on o."Id"=h.opphevet_av_rettskilde_id
               left join rettskilder v on v."Id"=h.videreforing_rettskilde_id where h.rettskilde_id={lit(rk["id"])} order by h.hjemmel_eid""")
    f = q(f"""select f.status, f.undertype, f.tittel, f.beskrivelse, f.forklart_bort_begrunnelse from funn f
              where f.ekstern_nokkel={lit('hjemmel-opphevet:' + rk["dokument_id"].split('/', 1)[1].replace('-', '/', 2).replace('forskrift/', 'forskrift/'))}""")
    return {"forskrift": rk, "hjemmelsstatus": [{k: v for k, v in h.items() if v is not None} for h in hs], "funn": f}


def nokkel(dok):
    _, typ, rest = dok.split("/")
    a, m, d, n = rest.split("-")
    return f"hjemmel-opphevet:{typ}/{a}/{m}/{d}/{n}/nor"


V = [("INS/forskrift/2011-06-24-650", "Lovisenberg diakonale høgskole"), ("SF/forskrift/2001-08-31-1074", "Posten Norge BA"),
     ("INS/forskrift/2011-06-24-649", "Ansgar Teologiske Høgskole"), ("INS/forskrift/2007-03-16-304", "Norsk Lærerakademi Bachelor- og masterstudier"),
     ("SF/forskrift/1971-06-25-3800", "TONO (Norsk Komponistforenings Internasjonale Musikkbyrå) fortsatt")]
K = ["INS/forskrift/1992-12-11-1266", "INS/forskrift/2000-03-21-285", "INS/forskrift/2001-02-01-1702", "INS/forskrift/2001-07-04-820",
     "INS/forskrift/2003-05-08-594", "INS/forskrift/2009-11-20-1428", "INS/forskrift/2010-11-19-1470", "INS/forskrift/2011-01-21-93",
     "INS/forskrift/2012-02-03-125", "SF/forskrift/2009-06-05-617", "INS/forskrift/1990-09-28-866", "SF/forskrift/1952-08-22-5"]
kort = []
for i, (dok, part) in enumerate(V, 1):
    c = kort_for(dok)
    c["funn"] = q(f"""select status, undertype, tittel, beskrivelse, forklart_bort_begrunnelse from funn where ekstern_nokkel={lit(nokkel(dok))}""")
    kort.append({"id": f"V{i:02d}", "gjelder": "Lest som enkeltvedtak", "lest_part": part} | c)
for i, dok in enumerate(K, 1):
    c = kort_for(dok)
    c["funn"] = q(f"""select status, undertype, tittel, beskrivelse from funn where ekstern_nokkel={lit(nokkel(dok))}""")
    kort.append({"id": f"K{i:02d}", "gjelder": "Ikke lest som enkeltvedtak (kontroll)"} | c)

json.dump({"sak": "#514 (enkeltvedtak som overgangsregelen dekker), måling 4 del 2", "dato": "2026-11-27",
           "grunnlag": f"Kopien {DB} etter kaldkjøringen med #514. Regelen (Enkeltvedtaklesing) leser 5 av de gjeldende primære forskriftene med "
                       "hjemmel (utenom DEL) som enkeltvedtak. 4 hjemmelsrader går fra «dokumenttype ikke nevnt» til «dekket», og 4 funn forklares bort.",
           "regelen": "Enkeltvedtak når alle vilkårene holder: (1) innholdet har en operativ setning som gir en navngitt part tillatelse, konsesjon, løyve, "
                      "dispensasjon, fritak, godkjenning, samtykke, bevilling, autorisasjon, akkreditering eller status som …; (2) partens første ord står i "
                      "tittelen eller kortnavnet; (3) vedtaket har høyst 8 noder med tekst og ingen generelle regler (Formål, Virkeområde, «Denne forskrift "
                      "gjelder»); (4) parten er ikke et statlig organ (Høgskolen i …, Universitetet i …, Statens …, …departementet, …direktoratet, …tilsynet). "
                      "Da er en videreføring dekket når overgangsregelen nevner enkeltvedtak eller vedtak.",
           "klasser": {"riktig": "V-kort: vedtaket er et enkeltvedtak rettet mot parten, og dekningen og utfallet (videreført, funnet forklart bort) er riktige. "
                                 "K-kort: vedtaket er ikke et enkeltvedtak som overgangsregelen dekker, så det er riktig at det ikke leses som det.",
                       "feil": "lesingen eller utfallet er feil", "usikker": "kan ikke avgjøres fra kildene"},
           "feilarsaker": ["vedtakstypen", "dekningen", "populasjonen", "tekstlesingen", "pastanden", "annet"],
           "kort": kort}, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
print(len(kort), "kort")
for c in kort: print(c["id"], c["forskrift"]["dokument_id"], [f["status"] for f in c["funn"]], [h.get("videreforing_dekning") for h in c["hjemmelsstatus"]])
