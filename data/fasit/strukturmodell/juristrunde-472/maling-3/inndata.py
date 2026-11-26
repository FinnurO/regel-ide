"""Bygger vurderingskortene for #472 måling 3 fra kopien regelide_501 (bare lesing). Kjør: python inndata.py utvalg.json inndata.json

Kortet har samme form som i måling 2, med to tillegg:
- `for_pr5` (bare endrede kort, E01–E15): funnet slik det sto før PR 5 (tabellen snap_funn i kopien), og `etter_pr5`: status og begrunnelse.
- `kortnavn`: for hver lov som er lest som opphevet, kortnavnet regelen leser av tittelen (eller grunnen til at den ikke gir noe), titlene
  korpuset siterer med et kortnavn i parentes, lover med samme kortnavn, og nodene i den opphevende kilden som nevner kortnavnet.
Konteksten er et hjelpemiddel, ikke en vurdering. Modellererens vurdering legges inn for hånd.
"""
import json, os, re, subprocess, sys

DB = "regelide_501"
M = ["januar", "februar", "mars", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "desember"]
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


kortnavn = {x["eli"]: x for x in json.load(open(os.path.join(HER, "kortnavn.json"), encoding="utf-8"))}
PAREN = re.compile(r"\(([^)\s–,-]+)")


def sitater_av_lov(eli):
    """Hjemmelstekster i korpus som siterer loven med dato og nummer og et kortnavn i parentes rett etter."""
    m = re.search(r"/eli/lov/(\d{4})/(\d{2})/(\d{2})/(\d+)/nor", eli)
    if not m: return []
    y, mo, d, nr = m.groups()
    kjerne = rf"{int(d)}\. *{M[int(mo) - 1]} +{y} +nr\.? *{nr}[^0-9()]{{0,200}}\("
    rx = kjerne + r"[[:alnum:]æøåÆØÅ]{4,}(loven|lova)"
    sitat = r"lov[^.]{0,20}" + kjerne + r"[^)]*\)"
    return q(f"""select coalesce(dokument_id, eli) kilde, left(substring(annet_om_dokumentet from {lit(sitat)}), 300) sitat
                 from rettskilder where annet_om_dokumentet ~ {lit(rx)} order by 1 limit 5""")


def andre_lover_med_navn(k):
    stamme = k[:-5] if k.endswith("loven") else k[:-4]
    return q(f"""select coalesce(dokument_id, eli) kilde, tittel, kortnavn from rettskilder
                 where kildetype='Lov' and (lower(kortnavn) in ({lit(stamme + 'loven')}, {lit(stamme + 'lova')})
                   or tittel ~* {lit(chr(92) + '(' + stamme + '(loven|lova)')}) order by 1 limit 6""")


utvalg = json.load(open(sys.argv[1], encoding="utf-8"))["utvalg"]
kort = []
for u in utvalg:
    f = q(f'select "Id"::text id, ekstern_nokkel, undertype, tittel, beskrivelse, status, forklart_bort_begrunnelse from funn where "Id"={lit(u["id"])}')[0]
    rk = q(f"""select "Id"::text id, dokument_id, tittel, annet_om_dokumentet, ikrafttredelse, rent_endringsvedtak, kildeform, eli
               from rettskilder where dokument_id={lit(u["dokument_id"])} and entitetsstatus='gjeldende' and importrolle='primaer'""")[0]
    kob = q(f"""select k.rolle, k.etikett, k.merknad, k.eid, n.tekst nodetekst from funn_koblinger k left join rettskilde_noder n on n."Id"=k.node_id
                where k.funn_id={lit(u["id"])} order by k.sortering""")
    hs = q(f"""select h.hjemmel_eid hjemmel, h.hjemmel_eli, m.dokument_id i_korpus, h.rolle, h.rolle_regel, h.status, h.bevistrinn, h.ukjent_grunn,
                      o.dokument_id opphevet_av, h.opphevet_av_eid, h.opphevet_av_sitat, h.opphevet_fra_kunngjort lest_i_lovtidend,
                      v.dokument_id videreforing_kilde, h.videreforing_eid, h.videreforing_form, h.videreforing_forbehold, h.videreforing_tidsfrist::text,
                      h.videreforing_dekning, h.videreforing_sitat, h.premissmangel, h.kunnskapsstatus
               from hjemmelsstatus h left join rettskilder m on m."Id"=h.mal_rettskilde_id left join rettskilder o on o."Id"=h.opphevet_av_rettskilde_id
               left join rettskilder v on v."Id"=h.videreforing_rettskilde_id
               where h.rettskilde_id={lit(rk["id"])} order by h.hjemmel_eid""")
    hs = [{k: v for k, v in h.items() if v is not None} for h in hs]
    kn = []
    for h in hs:
        e = h.get("hjemmel_eli")
        if not e or not h.get("bevistrinn") or "/eli/lov/" not in e or any(x["eli"] == e for x in kn): continue
        les = kortnavn.get(e, {"eli": e, "kortnavn": None, "grunn": "ingen_tittel_med_kortnavn"})
        k = les.get("kortnavn")
        sitert = sitater_av_lov(e)
        navn = k
        if navn is None:
            for s in sitert:
                p = PAREN.search(s.get("sitat") or "")
                if p and re.search(r"(loven|lova)$", p.group(1).lower()):
                    navn = p.group(1).lower()
                    break
        noder = []
        if navn and h.get("opphevet_av"):
            stamme = navn[:-5] if navn.endswith("loven") else navn[:-4]
            noder = q(f"""select n.eid, left(regexp_replace(n.tekst, '\\s+', ' ', 'g'), 500) tekst from rettskilde_noder n join rettskilder r on r."Id"=n.rettskilde_id
                          where r.dokument_id={lit(h["opphevet_av"])} and r.entitetsstatus='gjeldende' and n.entitetsstatus='gjeldende'
                            and n.tekst ~* {lit(chr(92) + 'm' + stamme + '(loven|lova)' + chr(92) + 'M')} order by n.sorteringsrekkefolge limit 6""")
        kn.append({"eli": e, "kortnavn_lest": k, "grunn": les["grunn"], "titler_i_korpus": sitert,
                   "andre_lover_med_samme_navn": andre_lover_med_navn(navn) if navn else [], "opphevende_kilde_nevner_kortnavnet": noder})
    c = {"id": u["kort"], "utvalg": u["utvalg"], "funn_id": f["id"], "ekstern_nokkel": f["ekstern_nokkel"], "prefiks": u["prefiks"],
         "forskrift": {"dokument_id": rk["dokument_id"], "tittel": rk["tittel"], "lovdatas_hjemmelstekst": rk["annet_om_dokumentet"],
                       "ikrafttredelse": rk["ikrafttredelse"], "rent_endringsvedtak": rk["rent_endringsvedtak"], "kildeform": rk["kildeform"]},
         "hjemmelsstatus_etter_pr5": hs, "kortnavn": kn}
    if u["utvalg"] == "endret":
        s = q(f'select undertype, tittel, beskrivelse from snap_funn where "Id"={lit(u["id"])}')[0]
        c["for_pr5"] = {"status": "oppdaget", "undertype": s["undertype"], "tittel": s["tittel"], "pastand": s["beskrivelse"]}
        c["etter_pr5"] = {"status": f["status"], "begrunnelse": f["forklart_bort_begrunnelse"]}
    else:
        c["foreslatt"] = {"status": f["status"], "undertype": f["undertype"], "tittel": f["tittel"], "pastand": f["beskrivelse"], "koblinger": kob}
    kort.append(c)

json.dump({"sak": "#472 måling 3 (#501, PR 5: videreføring med kortnavnet fra tittelen til den opphevede loven)", "dato": "2026-11-25",
           "grunnlag": f"Kopien {DB} (bare SELECT) etter at PR 5 er kjørt: hjemmelsstatus 472-pr5-2026-11-25 skrevet, funnregelen kjørt med skriv og oppfrisk. "
                       "Før: 264 funn oppdaget. Etter: 241 oppdaget, 23 forklart bort (samlet status videreført). Utvalget står i utvalg.json.",
           "klasser": {
               "riktig": "Endret kort: utfallet etter PR 5 holder (hjemmelen er videreført av den koblede overgangsregelen, så funnet skal ikke stå). "
                         "Kontrollkort: funnet holder som i måling 2 (opphevet uten funnet videreføring, riktig undertype), og regelen har ikke oversett en "
                         "videreføring med kortnavnet.",
               "feil": "Endret kort: funnet burde ha stått (overgangsregelen dekker ikke forskriften, eller kortnavnet peker ikke på loven). "
                       "Kontrollkort: påstanden eller undertypen er feil.",
               "usikker": "kan ikke avgjøres fra kildene"},
           "feilarsaker": ["kortnavnet", "dekningen", "videreforingsformen", "populasjonen", "tekstlesingen", "rollen", "annet"],
           "kort": kort}, open(sys.argv[2], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(kort), "kort")
