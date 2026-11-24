"""Bygger vurderingskortene for #472 måling 2 fra kopien regelide_472pr4 (bare lesing). Kjør: python inndata.py utvalg.json kort.json

Kortet har samme form som i måling 1 (../inndata.json), uten modellererens vurdering (den legges inn for hånd i inndata.json). I tillegg står
`kontekst`: tekst i korpus som nevner forskriften med dato og nummer (kan være en oppheving av den), og nodene i den opphevende kilden som kan
være overgangsregler. Konteksten er et hjelpemiddel, ikke en vurdering.
"""
import json, os, re, subprocess, sys

DB = "regelide_472pr4"
M = ["januar", "februar", "mars", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "desember"]


def q(sql):
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    wrapped = f"set default_transaction_read_only=on; begin read only; select coalesce(json_agg(t),'[]'::json) from ({sql}) t; commit;"
    r = subprocess.run(["podman", "exec", "-i", "regel-ide-postgres-1", "psql", "-U", "postgres", "-d", DB, "-At", "-v", "ON_ERROR_STOP=1"],
                       input=wrapped.encode("utf-8"), capture_output=True, env=env)
    if r.returncode: raise RuntimeError(r.stderr.decode("utf-8", "replace"))
    lines = [l for l in r.stdout.decode("utf-8").splitlines() if l.strip() not in ("SET", "BEGIN", "COMMIT")]
    return json.loads(chr(10).join(lines))


def lit(s): return "'" + s.replace("'", "''") + "'"


def dokid(rid):
    if rid is None: return None
    r = q(f'select dokument_id from rettskilder where "Id"={lit(rid)}')
    return r[0]["dokument_id"] if r else None


utvalg = json.load(open(sys.argv[1], encoding="utf-8"))["utvalg"]
kort = []
for u in utvalg:
    f = q(f'select "Id"::text id, ekstern_nokkel, undertype, tittel, beskrivelse from funn where "Id"={lit(u["id"])}')[0]
    rk = q(f"""select "Id"::text id, dokument_id, tittel, annet_om_dokumentet, ikrafttredelse, rent_endringsvedtak, kildeform, eli
               from rettskilder where dokument_id={lit(u["dokument_id"])} and entitetsstatus='gjeldende' and importrolle='primaer'""")[0]
    kob = q(f"""select k.rolle, k.etikett, k.merknad, k.eid, n.tekst nodetekst from funn_koblinger k left join rettskilde_noder n on n."Id"=k.node_id
                where k.funn_id={lit(u["id"])} order by k.sortering""")
    hs = q(f"""select h.hjemmel_eid hjemmel, m.dokument_id i_korpus, h.rolle, h.rolle_regel, h.status, h.bevistrinn, h.ukjent_grunn, h.fulgt_eli,
                      o.dokument_id opphevet_av, h.opphevet_av_eid, h.opphevet_av_sitat, h.opphevet_dato::text, h.opphevet_fra_kunngjort lest_i_lovtidend,
                      v.dokument_id videreforing_kilde, h.videreforing_eid, h.videreforing_form, h.videreforing_forbehold, h.videreforing_tidsfrist::text,
                      h.videreforing_dekning, h.videreforing_sitat, e.dokument_id erstattet_av, h.erstattet_av_eid, h.premissmangel, h.kunnskapsstatus
               from hjemmelsstatus h left join rettskilder m on m."Id"=h.mal_rettskilde_id left join rettskilder o on o."Id"=h.opphevet_av_rettskilde_id
               left join rettskilder v on v."Id"=h.videreforing_rettskilde_id left join rettskilder e on e."Id"=h.erstattet_av_rettskilde_id
               where h.rettskilde_id={lit(rk["id"])} order by h.hjemmel_eid""")
    hs = [{k: v for k, v in h.items() if v is not None} for h in hs]
    # Konteksten: tekst i andre gjeldende kilder som nevner forskriften med dato og nummer.
    m = re.match(r"\w+/forskrift/(\d{4})-(\d{2})-(\d{2})-(\d+)", rk["dokument_id"])
    nevnt = []
    if m:
        y, mo, d, nr = m.groups()
        rx = rf"{int(d)}\. *{M[int(mo) - 1]} +{y}.{{0,15}}nr\.? *{nr}([^0-9]|$)"
        nevnt = q(f"""select r.dokument_id, n.eid, left(regexp_replace(n.tekst, '\\s+', ' ', 'g'), 600) tekst
                      from rettskilde_noder n join rettskilder r on r."Id"=n.rettskilde_id
                      where r.entitetsstatus='gjeldende' and r.importrolle='primaer' and n.entitetsstatus='gjeldende' and r."Id"<>{lit(rk["id"])}
                        and n.tekst ~ {lit(rx)} order by 1, n.sorteringsrekkefolge limit 10""")
    # Overgangskandidater i de opphevende kildene: noder med «forskrift»/«vedtak» og et gjelde-ord.
    opphevende = sorted({h["opphevet_av"] for h in hs if h.get("opphevet_av")})
    overgang = []
    for o in opphevende:
        overgang += q(f"""select r.dokument_id, n.eid, left(regexp_replace(n.tekst, '\\s+', ' ', 'g'), 500) tekst
                          from rettskilde_noder n join rettskilder r on r."Id"=n.rettskilde_id
                          where r.dokument_id={lit(o)} and r.entitetsstatus='gjeldende' and n.entitetsstatus='gjeldende' and not n.opphevet
                            and n.tekst ~* '(forskrift|vedtak|delegering|instruks|bestemmelse)' and n.tekst ~* '(gjeld|fortsatt|framleis|stå ved lag|opprettholdes|overgang)'
                          order by n.sorteringsrekkefolge limit 8""")
    kort.append({
        "id": u["kort"], "funn_id": f["id"], "ekstern_nokkel": f["ekstern_nokkel"], "undertype": f["undertype"], "prefiks": u["prefiks"],
        "forskrift": {"dokument_id": rk["dokument_id"], "tittel": rk["tittel"], "lovdatas_hjemmelstekst": rk["annet_om_dokumentet"],
                      "ikrafttredelse": rk["ikrafttredelse"], "rent_endringsvedtak": rk["rent_endringsvedtak"], "kildeform": rk["kildeform"]},
        "foreslatt": {"tittel": f["tittel"], "pastand": f["beskrivelse"], "koblinger": kob, "hjemmelsstatus": hs},
        "kontekst": {"forskriften_nevnt_i_annen_kilde": nevnt, "mulige_overgangsregler_i_opphevende_kilde": overgang},
    })

json.dump({"sak": "#472 måling 2 (presisjon for funnkategorien hjemmel_opphevet etter rettingene i PR 4)", "dato": "2026-11-24",
           "grunnlag": f"Kopien {DB} (bare SELECT) etter at PR 4 er kjørt: hjemmelsstatus og funnregelen 472-pr4-2026-11-24, 264 funn med status oppdaget. "
                       "Utvalget står i utvalg.json. Måling 2 er gjort i kopien, før noe er skrevet mot den delte basen.",
           "kort": kort}, open(sys.argv[2], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(kort), "kort")
