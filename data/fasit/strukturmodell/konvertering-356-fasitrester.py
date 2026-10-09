#!/usr/bin/env python3
"""
Deterministisk konvertering av strukturfasiten etter issue #356, «konverteringsrester etter #341/#352» (fasitkontrollen #309,
gjennomgang v5 2026-10-09), med kommentarene samme dag: tillegget om de negative har_delegert_til-radene i energiloven, tillegget om
domstolloven:u199, og utfallet av juristdebatten om fasitkortene (17 kort i #356 og 18 kort i #309, «Utfall av juristdebatten om
fasitkortene (2026-10-09)»). Ingen rad endres for hånd; alt under er regler som kan leses og kjøres om igjen.

    python data/fasit/strukturmodell/konvertering-356-fasitrester.py            # fasitfilene
    python data/fasit/strukturmodell/konvertering-356-fasitrester.py ki-utdata  # de lagrede KI-utdataene (#308)

Idempotent: kjøres det på en allerede konvertert fil, finnes ingen gamle typer eller negative unntaksrader, de nye radene og
aktørene finnes, og avgrensningene inneholder alt tilleggene — ingenting endres.

GENERELLE REGLER (fasit OG ki-utdata — det er reglene, ikke radene, som rettes; FORMAT.md og docs/33 §4.3 er oppdatert):

  R0. #341-regelen for delegering, fullført: kompetanse/delegeringskompetanse (positiv) i et LEDSAGENDE DELEGERINGSVEDTAK (noden
      ligger i en ledsagende kilde med tittel «Delegering …», slått opp i noder/<kilde>.json) der sitatet sier at delegeringen ER
      gjort («delegerer», «delegeres», «Delegeringen omfatter …») → relasjon/har_delegert_til (Johanns beslutning 1 på #341: en
      gjennomført delegering). #341-skriptet konverterte bare relasjon/delegerer_til; KI-utdataene hadde typen delegeringskompetanse
      i vedtakene og ble aldri truffet. «endres departementets delegeringsvedtak» (en endring, ikke en delegering) røres ikke.
  R1. Kompetanse til å opprette eller avvikle (#352 beslutning 4): konstituerende/oppretter eller avvikler der sitatet har
      KOMPETANSEORDLYD — «kan … opprette/nedsette/etablere/beslutte å ha», «maa/må ikke nedsættes» (forbud, negativ), «treffer vedtak
      om (å) opprette/oppløsning/nedleggelse», «avgjør om … etablere» — → kompetanse/opprettingskompetanse eller
      avviklingskompetanse (samme fra/til/polaritet). Hendelsen («som er opprettet i medhold av», «organ nedsatt av»), plikten
      («skal opprette») og «Vedtak om nedleggelse» uten subjekt røres ikke. Delegerbar-regelen fra #341 (kopiert) settes på de nye
      kompetansene: u27 «Kongen i statsråd» = false, u33 «Styret selv» = false.
  R2. «Det kan (også) oppnevnes …» (passiv, L14) som organsammensetning/har_medlemmer eller konstituerende/oppretter →
      kompetanse/oppnevningskompetanse, undertype oppnevning, fra = null (teksten sier ikke hvem), til = organet som oppnevnes;
      avgrensningen er «saker …»-leddet i sitatet når den mangler (#356 punkt 4, helse-og-omsorgstjenesteloven u116).
  R3. «Beslutningsmyndighet i henhold til §§ X, Y og Z kan ikke delegeres» (negativ delegeringskompetanse som viser til
      paragrafer i samme lov) → delegerbar = false på de positive kompetansene med samme fra i de paragrafene (#356 punkt 2, sameloven
      § 2-12 fjerde ledd → u160, u163, u176). Det er et TEKSTFUNN (lovens egne ord), ikke en slutning fra ordet «Kongen». Den negative
      delegeringsraden beholdes som sporingsgrunnlag (kort nr. 32). Tekstfunn/slutning-skillet på feltet hører til #335 og er ikke
      gjort her.
  R4. En negativ delegeringskompetanse som bare er «X selv» og dobler en kompetanse i samme ledd som alt har delegerbar = false,
      slettes (#356 punkt 5, helse-og-omsorgstjenesteloven u62 dobler u61).
  R5. kompetanse/vedtakskompetanse der sitatet sier «beslutningsmyndighet» → beslutningskompetanse (#341: brukes når teksten bare
      sier «beslutningsmyndighet»; vedtakskompetanse betyr nå enkeltvedtak) (#356 punkt 3, sameloven u14).
  R6. Unntak i et delegeringsvedtak er AVGRENSNING på den positive kanten (#356-tillegget 2026-10-09, #341 beslutning 1): en negativ
      har_delegert_til eller delegeringskompetanse i et ledsagende delegeringsvedtak slettes, og unntaket legges til avgrensningen på
      den positive har_delegert_til-kanten med samme ender (eller null-ender) — (a) de positive kantene i SAMME ledd, ellers (b) den
      ene positive kanten i samme dokument der noden innleder unntakslisten («med følgende unntak»). En positiv rad som selv er et
      unntak fra unntaket («med unntak av …», u231) er ikke et mål. Finnes ingen positiv kant, blir den negative raden stående og
      listes for Johann (gjettes ikke). Tillegget er «ikke myndighet etter <paragraf>» for tabellradene i femte ledd, ellers teksten
      fra juristkortene (nr. 11, 40–49), og hvert tillegg får hjemmelsstedet i parentes («FOR-2025-06-26-1340 kap. I femte ledd»),
      så eId-en til den slettede raden ikke går tapt.
  R7. «Delegeringen omfatter myndigheten til å endre og oppheve forskrift …» i et delegeringsvedtak (fasiten: normgivningskompetanse
      fra departementet) angir hvor langt delegeringen i samme punkt rekker (kort nr. 8): → relasjon/har_delegert_til med endene til
      den positive delegeringen i samme node; objekt «myndighet til å endre og oppheve forskrift <dato> nr. <n> fastsatt av Kongen»
      (tittelen etter «nr. <n> om …» tas ut, som i kortet); normform og delegerbar fjernes (bare på K).

FASITREGLER (bare fasiten — rettinger av enkeltrader, slått opp på (kilde, id, sitat) så en omnummerert fasit ikke treffer feil rad):

  R8. Lærdom 1 i #309 (kort nr. 5): kilde_utenfor_korpus gjelder HJEMMELEN, ikke den utfyllende forskriften. Spesialisthelsetjeneste-
      loven-fasiten hadde true på alle 47 forskriftshjemlene (konvensjonsfeil i den fila) → false. De andre filene har ikke feilen
      (energiloven 2 og helse- og omsorgstjenesteloven 2 rader med true er ikke vurdert her; se PR-en).
  R9. Juristkortene i #356 (nr. 9, 11, 28, 31, 32, 40–49, 50, 62) og #309 (nr. 2, 5, 6, 8, 14, 16, 18, 19, 21, 22, 27, 30, 35, 39, 54,
      55, 57, 61), v5-punktene som ennå ikke var rettet (spesialisthelsetjenesteloven u122, u203; helse- og omsorgstjenesteloven u13,
      u55) og tillegget om domstolloven:u199. Tabellen RETTING under; nye rader, nye aktører og slettinger i regel_9.
      Spesialisthelsetjenesteloven u94 (v5 punkt 8) var alt rettet i #355 (fra = null, ikke foretaksmøtet); den får bare en kommentar.
  R10. Juristrunden for denne saken (CLAUDE.md §23) — se regel_10.

Står utenfor (listes, røres ikke): domstolloven:u263 («gis myndighet» til å oversende — ingen type for oversendelse, Johann avgjør),
og delegerbar tekstfunn/slutning (#335, krever migrasjon).
"""
import collections
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
HER = os.path.dirname(os.path.abspath(__file__))
KI = len(sys.argv) > 1 and sys.argv[1] == "ki-utdata"
MAPPE = os.path.join(HER, "ki-utdata") if KI else HER
KILDER = ["domstolloven", "energiloven", "helse-og-omsorgstjenesteloven", "sameloven", "spesialisthelsetjenesteloven"]
ORDENSTALL = {1: "første", 2: "annet", 3: "tredje", 4: "fjerde", 5: "femte", 6: "sjette", 7: "sjuende", 8: "åttende", 9: "niende"}

DL = "https://lovdata.no/eli/lov/1915/08/13/5/nor/"
HFL = "https://lovdata.no/eli/lov/2001/06/15/93/nor/"
SHTL = "https://lovdata.no/eli/lov/1999/07/02/61/nor/"
HOTL = "https://lovdata.no/eli/lov/2011/06/24/30/nor/"
SAME = "https://lovdata.no/eli/lov/1987/06/12/56/nor/"


def noder_for(kilde):
    sti = os.path.join(HER, "noder", kilde + ".json")
    return json.load(open(sti, encoding="utf-8")) if os.path.exists(sti) else []


def tilfoy(u, tekst):
    if tekst not in (u.get("kommentar") or ""):
        u["kommentar"] = ((u.get("kommentar") or "") + " " + tekst).strip()


def kommentar_foran(u, tekst):
    """Kortets kommentar først; den gamle beholdes etter («Tidligere: …»), så ingenting går tapt (CLAUDE.md §7)."""
    gammel = u.get("kommentar") or ""
    if gammel.startswith(tekst):
        return False
    u["kommentar"] = tekst + (f" Tidligere kommentar: «{gammel}»." if gammel else "")
    return True


def neste_id(d, prefiks="u"):
    liste = d["utsagn"] if prefiks == "u" else d["aktorer"]
    tall = [int(x["id"][1:]) for x in liste if re.fullmatch(prefiks + r"\d+", x["id"])]
    return f"{prefiks}{max(tall) + 1}"


def finn(d, uid, sitat):
    return next((u for u in d["utsagn"] if u["id"] == uid and u["sitat"] == sitat), None)


def finnes(d, eid, kategori, type_, sitat, **felt):
    return any(u["eid"] == eid and u["kategori"] == kategori and u["type"] == type_ and u["sitat"] == sitat
               and all(u.get(k) == v for k, v in felt.items()) for u in d["utsagn"])


def ny_rad(d, mal, **felt):
    """En ny rad bygget på en eksisterende (samme eId, sikkerhet …), med feltene overstyrt."""
    rad = {k: v for k, v in mal.items() if k not in ("delegerbar", "undertype", "modalitet", "normform", "verifisert_av")}
    rad.update(felt)
    rad["id"] = neste_id(d)
    d["utsagn"].append(rad)
    return rad


def ny_aktor(d, mal_id, tekstform, **felt):
    a = next((a for a in d["aktorer"] if a["tekstform"] == tekstform), None)
    if a is not None:
        return a["id"], False
    mal = next(a for a in d["aktorer"] if a["id"] == mal_id)
    a = {**{k: mal.get(k) for k in mal}, "id": neste_id(d, "a"), "tekstform": tekstform, "varianter": [], **felt}
    d["aktorer"].append(a)
    return a["id"], True


def slett(d, u, logg, kilde, grunn):
    d["utsagn"].remove(u)
    logg.append((kilde, u["id"], f"SLETTET ({u['kategori']}/{u['type']}, {u['polaritet']}): {grunn}"))


def delegerbar(u, kongen, former):
    """#335-regelen, kopiert uendret fra konvertering-341-kompetanse.py (avgjort på SITATET)."""
    sitat, verdi = u["sitat"], None
    if u.get("fra") in kongen:
        if "Kongen i statsråd" in sitat:
            verdi = False
        elif re.search(r"\bKongen", sitat):
            verdi = True
    if u.get("fra") and any(re.search(re.escape(f) + r"\s+selv\b", sitat, re.IGNORECASE) for f in former.get(u["fra"], [])):
        verdi = False
    return verdi


class Kontekst:
    def __init__(self, kilde, d):
        self.kilde, self.d = kilde, d
        noder = noder_for(kilde)
        self.tekst = {n["eid"]: n.get("tekst") or "" for n in noder}
        self.eli = {n["eid"]: n["eli"] for n in noder}
        self.delegeringsvedtak = {l["eli"] for l in d.get("ledsagende", []) if l["tittel"].lower().startswith("delegering")}
        self.kongen = {a["id"] for a in d["aktorer"]
                       if a["tekstform"].lower() == "kongen" or a["tekstform"].lower().startswith("kongen i statsr")}
        self.former = {a["id"]: [a["tekstform"]] + list(a.get("varianter") or []) for a in d["aktorer"]}

    def i_vedtak(self, u):
        return self.eli.get(u["eid"]) in self.delegeringsvedtak

    def hjemmelssted(self, u):
        """«FOR-2025-06-26-1340 kap. I femte ledd» for en relativ eId i et ledsagende dokument; ellers eId-en."""
        m = re.search(r"forskrift/(\d{4})/(\d{2})/(\d{2})/(\d+)/", self.eli.get(u["eid"], ""))
        k = re.fullmatch(r"kap-([IVX]+)/ledd-(\d+)", u["eid"])
        if m and k and int(k.group(2)) in ORDENSTALL:
            return f"FOR-{m.group(1)}-{m.group(2)}-{m.group(3)}-{m.group(4)} kap. {k.group(1)} {ORDENSTALL[int(k.group(2))]} ledd"
        return u["eid"]


# ---- R0 --------------------------------------------------------------------------------------------------------------------------
GJENNOMFORT = re.compile(r"\bdelegerer\b|\bdelegeres\b|^Delegeringen omfatter\b")


def regel_0(k, logg):
    for u in k.d["utsagn"]:
        if (u["kategori"], u["type"], u["polaritet"]) == ("kompetanse", "delegeringskompetanse", "positiv") and k.i_vedtak(u) \
                and GJENNOMFORT.search(u["sitat"]):
            u["kategori"], u["type"] = "relasjon", "har_delegert_til"
            for felt in ("delegerbar", "normform", "undertype", "grunnlag"):
                u.pop(felt, None)
            tilfoy(u, "[#356, R0] Gjennomført delegering i et delegeringsvedtak (#341 beslutning 1). Var kompetanse/delegeringskompetanse.")
            logg.append((k.kilde, u["id"], "R0 kompetanse/delegeringskompetanse → relasjon/har_delegert_til (gjennomført delegering)"))


# ---- R7 --------------------------------------------------------------------------------------------------------------------------
def regel_7(k, logg):
    for u in k.d["utsagn"]:
        if not (u["sitat"].startswith("Delegeringen omfatter myndigheten til") and k.i_vedtak(u) and u["type"] != "har_delegert_til"):
            continue
        hode = [p for p in k.d["utsagn"] if p["eid"] == u["eid"] and p["type"] == "har_delegert_til" and p["polaritet"] == "positiv"
                and p is not u]
        if len(hode) != 1:
            continue  # gjettes ikke
        m = re.search(r"Delegeringen omfatter (myndigheten til .*?fastsatt av \w+)\.", k.tekst.get(u["eid"], ""))
        gammel = (u["kategori"], u["type"], u.get("fra"), u.get("objekt"))
        u["kategori"], u["type"], u["fra"], u["til"] = "relasjon", "har_delegert_til", hode[0]["fra"], hode[0]["til"]
        if m and not KI:
            u["objekt"] = re.sub(r"^myndigheten", "myndighet", re.sub(r"(nr\. \d+) om .*? (fastsatt av)", r"\1 \2", m.group(1)))
        for felt in ("delegerbar", "normform", "undertype", "grunnlag"):
            u.pop(felt, None)
        tilfoy(u, f"[#356, R7, kort nr. 8] Setningen angir hvor langt delegeringen i samme punkt ({hode[0]['id']}) rekker, ikke egen "
                  f"lovhjemmel for departementet (#341 beslutning 1). Var {gammel[0]}/{gammel[1]} fra «{gammel[2]}», objekt «{gammel[3]}».")
        logg.append((k.kilde, u["id"], f"R7 {gammel[0]}/{gammel[1]} → relasjon/har_delegert_til ({hode[0]['fra']} → {hode[0]['til']})"))


# ---- R1 --------------------------------------------------------------------------------------------------------------------------
VERB = r"(?:opprett\w*|nedsett\w*|nedsætt\w*|etabler\w*|beslutte\s+å\s+ha|oppløs\w*|nedlegg\w*|avvikl\w*)"
KOMPETANSEORDLYD = [
    re.compile(r"\bkan\b[^.;]{0,120}?\b" + VERB, re.IGNORECASE),
    re.compile(r"\b(?:maa|må)\s+ikke\s+" + VERB, re.IGNORECASE),
    re.compile(r"\btreffer\s+vedtak\s+om\s+(?:å\s+)?" + VERB, re.IGNORECASE),
    re.compile(r"\bavgjør\s+om\b[^.;]{0,60}?\b" + VERB, re.IGNORECASE),
]
TIL_KOMPETANSE = {"oppretter": "opprettingskompetanse", "avvikler": "avviklingskompetanse"}


def regel_1(k, logg):
    for u in k.d["utsagn"]:
        if u["kategori"] != "konstituerende" or u["type"] not in TIL_KOMPETANSE:
            continue
        if not any(r.search(u["sitat"]) for r in KOMPETANSEORDLYD):
            continue
        gammel = u["type"]
        u["kategori"], u["type"] = "kompetanse", TIL_KOMPETANSE[gammel]
        tilfoy(u, f"[#356, R1] Kompetanseordlyd i sitatet: kompetansen til å {'opprette' if gammel == 'oppretter' else 'avvikle'} "
                  f"(#352 beslutning 4). Var konstituerende/{gammel}.")
        logg.append((k.kilde, u["id"], f"R1 konstituerende/{gammel} → kompetanse/{u['type']}" + (" (negativ)" if u["polaritet"] == "negativ" else "")))
        verdi = delegerbar(u, k.kongen, k.former)
        if verdi is not None and "delegerbar" not in u:
            u["delegerbar"] = verdi
            logg.append((k.kilde, u["id"], f"R1 delegerbar = {str(verdi).lower()} (#341-regelen på sitatet)"))


# ---- R2 --------------------------------------------------------------------------------------------------------------------------
OPPNEVNES = re.compile(r"\bDet\s+kan\s+(?:også\s+)?oppnevnes\b")


def regel_2(k, logg):
    for u in k.d["utsagn"]:
        if (u["kategori"], u["type"]) not in (("organsammensetning", "har_medlemmer"), ("konstituerende", "oppretter")):
            continue
        if not OPPNEVNES.search(u["sitat"]):
            continue
        gammel = (u["kategori"], u["type"], u.get("fra"), u.get("til"))
        organ = u.get("fra") if u["type"] == "har_medlemmer" else u.get("til")
        u.update({"kategori": "kompetanse", "type": "oppnevningskompetanse", "undertype": "oppnevning", "fra": None, "til": organ})
        m = re.search(r"\bfor\s+(saker\b[^.;]*)", u["sitat"])
        if not u.get("avgrensning") and m:
            u["avgrensning"] = m.group(1)
        tilfoy(u, f"[#356, R2] «Det kan oppnevnes» er en adgang til å oppnevne (L14), ikke organets faste medlemmer: "
                  f"oppnevningskompetanse, fra = null (passiv — teksten sier ikke hvem). Var {gammel[0]}/{gammel[1]} "
                  f"fra «{gammel[2]}» til «{gammel[3]}».")
        logg.append((k.kilde, u["id"], f"R2 {gammel[0]}/{gammel[1]} → kompetanse/oppnevningskompetanse/oppnevning, fra = null, til = {organ}"))


# ---- R3 --------------------------------------------------------------------------------------------------------------------------
def regel_3(k, logg, utenfor):
    for n in k.d["utsagn"]:
        if (n["kategori"], n["type"], n["polaritet"]) != ("kompetanse", "delegeringskompetanse", "negativ"):
            continue
        if not re.search(r"\bkan\s+ikke\s+delegeres\b", n["sitat"]):
            continue
        m = re.search(r"§§?\s*([0-9a-z\-–, ]+(?:og\s+[0-9a-z\-–]+)?)", n["sitat"])
        refs = re.findall(r"\d+-\d+\s?[a-z]?\b", m.group(1)) if m else []
        if not refs or not n.get("fra"):
            utenfor.append((k.kilde, n, "«kan ikke delegeres» uten paragrafhenvisning eller uten fra — ikke brukt"))
            continue
        lov = n["eid"].split("§")[0]
        for u in k.d["utsagn"]:
            if u is n or u["kategori"] != "kompetanse" or u["polaritet"] != "positiv" or u.get("fra") != n["fra"]:
                continue
            if not any(u["eid"].startswith(f"{lov}§{r.replace(' ', '')}/") for r in refs):
                continue
            tilfoy(u, f"[#356, R3] delegerbar = false: TEKSTFUNN — {n['sitat']!r} ({n['id']}). Ikke en slutning fra ordet «Kongen».")
            if u.get("delegerbar") is not False:
                logg.append((k.kilde, u["id"], f"R3 delegerbar {u.get('delegerbar')} → false (tekstfunn, {n['id']})"))
                u["delegerbar"] = False


# ---- R4 --------------------------------------------------------------------------------------------------------------------------
def regel_4(k, logg):
    for n in list(k.d["utsagn"]):
        if (n["kategori"], n["type"], n["polaritet"]) != ("kompetanse", "delegeringskompetanse", "negativ"):
            continue
        if not re.fullmatch(r"\S+(?:\s+\S+)?\s+selv", n["sitat"].strip()):
            continue
        m = next((u for u in k.d["utsagn"] if u is not n and u["eid"] == n["eid"] and u["kategori"] == "kompetanse"
                  and u.get("fra") == n.get("fra") and u.get("delegerbar") is False and n["sitat"] in u["sitat"]), None)
        if m is None:
            continue
        tilfoy(m, f"[#356, R4] Delegeringsforbudet («{n['sitat']}») står som delegerbar = false på denne raden; den doble negative "
                  f"delegeringsraden ({n['id']}) er slettet.")
        slett(k.d, n, logg, k.kilde, f"R4 dobler {m['id']} (delegerbar = false står der)")


# ---- R5 --------------------------------------------------------------------------------------------------------------------------
def regel_5(k, logg):
    for u in k.d["utsagn"]:
        if (u["kategori"], u["type"]) == ("kompetanse", "vedtakskompetanse") and re.search(r"beslutningsmyndighet", u["sitat"], re.I):
            u["type"] = "beslutningskompetanse"
            tilfoy(u, "[#356, R5] Teksten sier «beslutningsmyndighet» (#341: beslutningskompetanse); vedtakskompetanse betyr enkeltvedtak. "
                      "Var vedtakskompetanse.")
            logg.append((k.kilde, u["id"], "R5 vedtakskompetanse → beslutningskompetanse («beslutningsmyndighet»)"))


# ---- R6 --------------------------------------------------------------------------------------------------------------------------
# Kortenes tekst for unntaket (juristdebatten 2026-10-09). Resten får «ikke myndighet etter <paragraf>» fra tabellraden.
UNNTAK_TEKST = {
    ("energiloven", "u230", "Delegeringsvedtaket omfatter ikke departementets forskriftskompetanse etter energiloven"):
        "ikke departementets forskriftskompetanse, unntatt §§ 9-1, 9-2, 9-3 og 9-5 (som er delegert, u231)",  # nr. 40
    ("energiloven", "u233", "Myndighet tillagt reguleringsmyndigheten og klagenemnden fremgår av bestemmelser i energiloven med "
                            "forskrifter, og omfattes ikke av delegeringsvedtaket her."):
        "ikke myndighet tillagt reguleringsmyndigheten og klagenemnda (deklaratorisk; følger av § 2-5 med forskrifter)",  # nr. 41
    ("energiloven", "u234", "§ 2-5 første ledd | Utpeking av reguleringsmyndighet og klagenemnd"): "ikke myndighet etter § 2-5 første ledd",
    ("energiloven", "u235", "§ 3-1 hvor det i forskrift er fastsatt at vedtak fattes av Kongen i statsråd"):
        "ikke myndighet etter § 3-1 der forskrift legger vedtaket til Kongen i statsråd (deklaratorisk, jf. u227)",  # nr. 11
    ("energiloven", "u236", "§ 3-3 annet og fjerde ledd | Områdekonsesjonærens leveringsplikt"): "ikke myndighet etter § 3-3 annet og fjerde ledd",
    ("energiloven", "u237", "§ 3-4 annet og tredje ledd | Anleggskonsesjonærens plikt til å tilknytte uttakskunder"):
        "ikke myndighet etter § 3-4 annet og tredje ledd",
    ("energiloven", "u238", "§ 3-4a fjerde ledd | Plikt til å tilknytte anlegg for produksjon av elektrisk energi"):
        "ikke myndighet etter § 3-4 a fjerde ledd",
    ("energiloven", "u239", "§ 3-6 | Ekspropriasjon av elektriske anlegg"): "ikke myndighet etter § 3-6",
    ("energiloven", "u241", "§ 6-1 annet ledd | Tildeling av myndighet til å utøve systemansvaret"): "ikke myndighet etter § 6-1 annet ledd",
    ("energiloven", "u243", "§ 9-1 siste ledd | Beslutning om å underlegge kraftforsyningen KBO"): "ikke myndighet etter § 9-1 siste ledd",
    ("energiloven", "u249", "Delegeringen gjelder ikke i de tilfellene der det er presisert at myndigheten til å fatte vedtak ikke ligger "
                            "til Norges vassdrags- og energidirektorat."):
        "ikke der det er presisert at myndigheten til å fatte vedtak ikke ligger til NVE (presiseringen står i den enkelte konsesjonen "
        "eller kgl.res.)",  # nr. 49
}


def unntakstekst(k, n):
    if not KI and (k.kilde, n["id"], n["sitat"]) in UNNTAK_TEKST:
        return UNNTAK_TEKST[(k.kilde, n["id"], n["sitat"])]
    m = re.match(r"\s*(§[^|]+?|[Kk]apittel\s+\d+\s?[a-z]?)\s*\|", n["sitat"])
    if m:
        return "ikke myndighet etter " + re.sub(r"^Kapittel", "kapittel", m.group(1).strip())
    return f"ikke: «{n['sitat']}»"


def regel_6(k, logg, utenfor):
    negative = [n for n in k.d["utsagn"] if n["type"] in ("har_delegert_til", "delegeringskompetanse") and n["polaritet"] == "negativ"
                and k.i_vedtak(n)]
    for n in negative:
        def passer(p):
            return (p["type"] == "har_delegert_til" and p["polaritet"] == "positiv" and k.eli.get(p["eid"]) == k.eli.get(n["eid"])
                    and n.get("fra") in (None, p.get("fra")) and n.get("til") in (None, p.get("til"))
                    and not p["sitat"].lower().startswith("med unntak"))
        mal = [p for p in k.d["utsagn"] if passer(p) and p["eid"] == n["eid"]]
        if not mal:
            mal = [p for p in k.d["utsagn"] if passer(p) and "med følgende unntak" in k.tekst.get(p["eid"], "")]
            if len(mal) != 1:
                utenfor.append((k.kilde, n, f"negativt unntak i delegeringsvedtak uten entydig positiv kant ({len(mal)} kandidater) — står"))
                continue
        tillegg = f"{unntakstekst(k, n)} ({k.hjemmelssted(n)})"
        for p in mal:
            if tillegg not in (p.get("avgrensning") or ""):
                p["avgrensning"] = (p["avgrensning"] + "; " if p.get("avgrensning") else "") + tillegg
            tilfoy(p, f"[#356, R6] Unntak i delegeringsvedtaket står i avgrensningen (var egen negativ rad {n['id']}).")
            logg.append((k.kilde, p["id"], f"R6 avgrensning + «{tillegg}» (fra {n['id']})"))
        slett(k.d, n, logg, k.kilde, f"R6 unntaket er avgrensning på {', '.join(p['id'] for p in mal)}")


# ---- R8 --------------------------------------------------------------------------------------------------------------------------
def regel_8(k, logg):
    if KI or k.kilde != "spesialisthelsetjenesteloven":
        return
    for u in k.d["utsagn"]:
        if u["type"] == "normgivningskompetanse" and u.get("normform") == "forskrift" and u.get("kilde_utenfor_korpus") is True:
            u["kilde_utenfor_korpus"] = False
            tilfoy(u, "[#356, R8, lærdom 1 i #309] kilde_utenfor_korpus gjelder hjemmelen (her loven), ikke den utfyllende forskriften. Var true.")
            logg.append((k.kilde, u["id"], "R8 kilde_utenfor_korpus true → false (forskriftshjemmel i loven)"))


# ---- R9: kortene ------------------------------------------------------------------------------------------------------------------
# (kilde, id, sitat) → felt. «+kommentar» legges til; «kommentar» settes først og den gamle beholdes etter; «-felt» fjernes.
RETTING = {
    # #356 nr. 9
    ("spesialisthelsetjenesteloven", "u267", "Departementet avgjør i tvilstilfeller hvor en pasient har bostedsregion"): {
        "type": "beslutningskompetanse", "til": None,
        "+kommentar": "[#356, kort nr. 9] Beslutningskompetanse, ikke enkeltvedtak: avgjørelsen styrer hvilket regionalt helseforetak som har "
                      "betalingsansvaret etter § 5-2 første ledd (fordeling mellom RHF-er). til = null betyr ikke at pasienten er motpart."},
    # #356 nr. 28
    ("sameloven", "u152", "Denne registrering skal være tilgjengelig kun for den myndighet som har ansvaret for gjennomføring av valg til "
                          "Sameting, eller etter samtykke fra Sametinget."): {
        "kategori": "annet:informasjonsdeling",
        "avgrensning": "eksklusiv («kun»): bare valgmyndigheten; andre bare etter samtykke fra Sametinget (egen rad)",
        "+kommentar": "[#356, kort nr. 28] Tilgang er ikke myndighet (K); informasjonstilgang står utenfor P (docs/33 §4.4). Var kategori kompetanse."},
    # #356 nr. 31
    ("sameloven", "u155", "Vedtak om registrering kan ikke påklages."): {
        "til": "a1", "+kommentar": "[#356, kort nr. 31] Motparten står i samme ledd («søke Sametinget om å bli registrert»): til = Sametinget (P1)."},
    # #356 nr. 32 — delegerbar settes av R3; kortet sier i tillegg at u170 beholdes som sporingsgrunnlag.
    ("sameloven", "u170", "Beslutningsmyndighet i henhold til §§ 2-9, 2-10 og 2-14 kan ikke delegeres."): {
        "+kommentar": "[#356, kort nr. 32] Beholdes som sporingsgrunnlag for delegerbar = false på u160, u163 og u176 (R3, tekstfunn). "
                      "Merkingen tekstfunn/slutning («uttrykkelig_forbudt») hører til #335."},
    # #356 nr. 49 (avgrensningen settes av R6)
    ("energiloven", "u247", "Norges vassdrags- og energidirektorat gis myndighet til å behandle søknader om endringer i konsesjoner etter "
                            "energiloven som er endelig avgjort av departementet."): {
        "objekt": "behandling av søknader om endringer i konsesjoner etter energiloven",
        "avgrensning_foran": "konsesjoner som er endelig avgjort av departementet",
        "+kommentar": "[#356, kort nr. 49] Objektet presisert; hvilke konsesjoner står nå i avgrensningen."},
    ("energiloven", "u248", "Norges vassdrags- og energidirektorat delegeres også myndighet til å behandle søknader om endringer i konsesjoner "
                            "etter energiloven gitt av Kongen i statsråd"): {
        "objekt": "behandling av søknader om endringer i konsesjoner etter energiloven",
        "avgrensning_foran": "konsesjoner gitt av Kongen i statsråd ved kongelig resolusjon",
        "+kommentar": "[#356, kort nr. 49] Objektet presisert; hvilke konsesjoner står nå i avgrensningen."},
    # #356 nr. 50 (u53 slettes i regel_9)
    ("spesialisthelsetjenesteloven", "u51", "Eiere utøver den øverste myndighet i foretak i foretaksmøte"): {
        "kategori": "kompetanse", "type": "beslutningskompetanse", "fra": "a24", "til": "a5",
        "avgrensning": "i foretaksmøte — eier kan ikke utøve eierstyring utenom foretaksmøte (§ 16 første ledd annet punktum); unntatt "
                       "bevilgning og vilkår for tildelingen, som kan gis utenfor foretaksmøte (§ 16 tredje ledd, u55)",
        "+kommentar": "[#356, kort nr. 50] u53 («Eier kan ikke utøve eierstyring i foretak utenom foretaksmøte») gjentok denne raden som negasjon "
                      "og er slått inn i avgrensningen. Var relasjon/annet:utover_myndighet_gjennom eier → foretaksmøtet; foretaksmøtet som "
                      "organ står i u52 (har_organ)."},
    # #356 nr. 62 (u248 slettes og uttalelsesraden legges til i regel_9)
    ("domstolloven", "u247", "Tilsynsutvalget kan treffe vedtak om disiplinærtiltak"): {
        "type": "sanksjonskompetanse", "til": "dommer",
        "avgrensning": "ikke forhold som kan overprøves etter reglene i rettspleielovgivningen for øvrig (§ 236 fjerde ledd)",
        "+kommentar": "[#356, kort nr. 62] Disiplinærtiltak (kritikk, advarsel) er selv reaksjoner (#355 beslutning 3): sanksjonskompetanse "
                      "overfor dommere. u248 («kan ikke vurdere …») er avgrensning her, ikke en egen negativ tilsynskant. Var vedtakskompetanse."},
    # Tillegget 2026-10-09
    ("domstolloven", "u199", "Kongen kan gi nærmere forskrifter om dommeres sidegjøremål."): {
        "objekt": "dommeres sidegjøremål", "+kommentar": "[#356, tillegg 2026-10-09] Objektet var «sidegjøremål»."},
    # v5 punkt 3
    ("spesialisthelsetjenesteloven", "u122", "Den daglige ledelsen omfatter ikke saker som etter foretakets forhold er av uvanlig art eller "
                                             "av stor betydning"): {
        "type": "beslutningskompetanse",
        "avgrensning": "unntatt når styret i den enkelte sak har gitt daglig leder myndighet, eller når styrets beslutning ikke kan avventes "
                       "uten vesentlig ulempe for foretakets virksomhet (§ 37 tredje ledd annet punktum; delegeringen står i u123)",
        "+kommentar": "[#356, v5 punkt 3] Intern foretaksbeslutning, ikke enkeltvedtak: beslutningskompetanse. Unntaket i samme ledd manglet. "
                      "Var vedtakskompetanse."},
    # v5 punkt 8 — alt rettet i #355 (fra = null); bare kommentar
    ("spesialisthelsetjenesteloven", "u94", "Dette gjelder ikke et styremedlem som er valgt etter §§ 22 eller 23"): {
        "+kommentar": "[#356, v5 punkt 8] [slutning] Styremedlemmer etter §§ 22 og 23 velges av og blant de ansatte, så det er ikke "
                      "foretaksmøtet som er avskåret; fra står som null fordi setningen ikke nevner de ansatte."},
    # v5 punkt 9 (lærdom 5)
    ("spesialisthelsetjenesteloven", "u203", "Den kliniske etikkomiteen skal utføre sine oppgaver uavhengig og selvstendig"): {
        "fra": None, "+kommentar": "[#356, v5 punkt 9, lærdom 5] Uavhengighetsutsagn: fra = null (enhver). Helseforetaket var tolket inn."},
    # v5 punkt 10
    ("helse-og-omsorgstjenesteloven", "u13", "Avtalene kan ikke overdras."): {
        "type": "annet:overdragelse_av_avtale", "fra": "a22", "til": None,
        "objekt": "avtaler om tjenesteyting med andre offentlige eller private tjenesteytere (første ledd)",
        "avgrensning": None,
        "+kommentar": "[#356, v5 punkt 10] Teksten sier at avtalene ikke kan overdras — ikke at kommunen ikke kan sette ut tjenester. "
                      "[slutning] fra = avtaleparten (tjenesteyteren i første ledd); setningen er passiv. Var annet:tjenesteutsetting "
                      "kommunen → tjenesteytere."},
    # #309 nr. 2
    ("spesialisthelsetjenesteloven", "u249", "Departementet kan gi forskrift om krav til godkjenning av virksomheter og helsetjenester"): {
        "objekt": "krav til godkjenning av virksomheter og helsetjenester",
        "avgrensning": "når hensyn til tjenestetilbudets kvalitet, pasientsikkerhet, samfunnssikkerhet eller beredskap tilsier det",
        "+kommentar": "[#309, kort nr. 2] Universitetssykehus-setningen er egen rad (u250)."},
    # #309 nr. 5
    ("spesialisthelsetjenesteloven", "u252", "Departementet kan gi forskrift med nærmere bestemmelser om vilkår for tildeling av godkjenning"): {
        "avgrensning": "godkjenning som kreves i medhold av § 4-1 første og andre ledd", "+kommentar": "[#309, kort nr. 5]"},
    # #309 nr. 6
    ("helse-og-omsorgstjenesteloven", "u20", "Kommunen skal treffe vedtak om kriteriene etter andre ledd er oppfylt."): {
        "objekt": "vedtak om at kriteriene for langtidsopphold i sykehjem eller tilsvarende bolig særskilt tilrettelagt for heldøgns "
                  "tjenester (§ 3-2 a annet ledd) er oppfylt",
        "+kommentar": "[#309, kort nr. 6] Skal-plikten og ventelisteplikten hører til regellaget (docs/33 §4.4)."},
    # #309 nr. 8 — typen og endene settes av R7; objektet her (kortets tekst)
    ("domstolloven", "u267", "Delegeringen omfatter myndigheten til å endre og oppheve forskrift 25. juni 2010 nr. 977"): {
        "objekt": "myndighet til å endre og oppheve forskrift 25. juni 2010 nr. 977 fastsatt av Kongen"},
    # #309 nr. 14 (til = ny aktør, regel_9)
    ("helse-og-omsorgstjenesteloven", "u147", "Ved avtale mellom de berørte kommunene kan ansvaret overføres til en annen kommune som den "
                                              "rusmiddelavhengige har tilknytning til."): {
        "kategori": "kompetanse", "type": "annet:avtalebasert_ansvarsoverforing", "fra": "a5",
        "avgrensning": "ansvaret for å reise sak etter §§ 10-2 og 10-3; krever avtale mellom de berørte kommunene",
        "+kommentar": "[#309, kort nr. 14] «Kan … ved avtale overføres» er en adgang, ikke en gjennomført overføring (L14). Var "
                      "relasjon/annet:ansvarsoverforing til «andre kommuner»."},
    # #309 nr. 16
    ("spesialisthelsetjenesteloven", "u170", "Staten har det overordnede ansvar for at befolkningen gis nødvendig spesialisthelsetjeneste"): {
        "til": None, "+kommentar": "[#309, kort nr. 16] Teksten sier «befolkningen», ikke «riket» (L1); området gjettes ikke. Var til = riket."},
    # #309 nr. 18 (u42 slettes i regel_9)
    ("spesialisthelsetjenesteloven", "u41", "Kongen i statsråd bestemmer hvilken arbeidsgivertilknytning foretakene skal ha"): {
        "+kommentar": "[#309, kort nr. 18] delegerbar = false er lest av «Kongen i statsråd» (#335-regelen; slutning). Den faktiske "
                      "arbeidsgivertilknytningen er ukjent og må slås opp (kgl.res. eller vedtekter, kilde utenfor korpus). M-raden u42 "
                      "(medlem_av arbeidsgiverorganisasjon) er fjernet (L12)."},
    # #309 nr. 19
    ("helse-og-omsorgstjenesteloven", "u153", "For hjelp fra kommunens helse- og omsorgstjeneste, herunder privat virksomhet som driver etter "
                                              "avtale med kommunen"): {
        "+kommentar": "[#309, kort nr. 19] «Herunder» i § 11-2 avgrenser bare vederlagsregelen; leses raden generelt, er hjemmelen også "
                      "helse- og omsorgstjenesteloven § 3-1 fjerde ledd."},
    # #309 nr. 21
    ("sameloven", "u16", "Valg til Sametinget holdes som direkte valg."): {
        "kategori": "kompetanse", "type": "oppnevningskompetanse", "undertype": "valg", "fra": None, "til": "a138",
        "avgrensning": "direkte valg",
        "kommentar": "velgerne er de som står i Sametingets valgmanntall (§ 2-6)",
        "+kommentar": "[#309, kort nr. 21] Valg er oppnevning med undertypen valg (#352 beslutning 1); fra = null (passiv), til = medlemmene "
                      "av Sametinget (samme aktør som i u18). Var organsammensetning/annet:valgmate fra Sametinget."},
    # #309 nr. 22
    ("helse-og-omsorgstjenesteloven", "u14", "skal kommunen ha knyttet til seg lege, sykepleier, fysioterapeut, jordmor, helsesykepleier, "
                                             "ergoterapeut og psykolog"): {
        "avgrensning": "for å oppfylle ansvaret etter § 3-1",
        "kommentar": "motparten er faggrupper/roller; tilknytningen kan være ansettelse eller avtale (f.eks. næringsdrivende fastlege)",
        "+kommentar": "[#309, kort nr. 22] Forskriftshjemmelen i annen setning står som u15."},
    # #309 nr. 27
    ("helse-og-omsorgstjenesteloven", "u135", "skal forslag til endelig vedtak sendes barneverns- og helsenemnda innen to uker"): {
        "kategori": "annet:saksforberedelse", "fra": None, "betinget": True,
        "avgrensning": "når det er truffet midlertidig vedtak; innen to uker, ellers faller vedtaket bort",
        "kommentar": "avsenderen er kommunen, jf. § 10-8 første ledd (slutning)",
        "+kommentar": "[#309, kort nr. 27] Oversendelse til nemnda er saksforberedelse (senere lag, docs/33 §4.4), verken R eller P. Var "
                      "relasjon fra kommunen."},
    # #309 nr. 30
    ("sameloven", "u147", "Sør-Norge valgkrets skal likevel ikke tildeles flere mandater enn valgkretsen ville fått dersom alle 39 mandater "
                          "var fordelt forholdsmessig mellom valgkretsene"): {
        "polaritet": "positiv", "objekt": "mandater i Sametinget",
        "avgrensning": "ikke flere enn valgkretsen ville fått om alle 39 mandater var fordelt forholdsmessig",
        "+kommentar": "[#309, kort nr. 30] Taket er avgrensning på en positiv kant (lærdom 4). Fordelingsregelen er valgloven § 11-3 tredje "
                      "og fjerde ledd (i korpuset: https://lovdata.no/eli/lov/2023/06/16/62/nor/§11-3/ledd-3 og ledd-4, kontrollert mot "
                      "lokal base 2026-10-09) — vanlig henvisning, ikke kilde utenfor korpus. Var negativ, objekt «mandattak for Sør-Norge "
                      "valgkrets»."},
    # #309 nr. 35
    ("energiloven", "u23", "Enkeltvedtak fattet av reguleringsmyndigheten kan bare påklages til klagenemnda."): {
        "kommentar": "[slutning] utledet av «bare» i u22: uten særregelen ville departementet vært klageinstans etter fvl. § 28 første ledd",
        "+kommentar": "[#309, kort nr. 35]"},
    # #309 nr. 39
    ("energiloven", "u141", "Kapittel VI om klage og omgjøring"): {"objekt": "omgjøring etter forvaltningsloven kapittel VI (§ 35)",
                                    "+kommentar": "[#309, kort nr. 39] ulovfestet omgjøring er ikke regulert"},
    ("energiloven", "u142", "Kapittel VI om klage og omgjøring"): {"objekt": "omgjøring etter forvaltningsloven kapittel VI (§ 35)",
                                    "+kommentar": "[#309, kort nr. 39] ulovfestet omgjøring er ikke regulert"},
    # #309 nr. 54
    ("spesialisthelsetjenesteloven", "u133", "Foretak kan ikke eie hele eller deler av virksomhet som yter spesialisthelsetjenester og som er "
                                             "organisert med begrenset ansvar"): {
        "fra": "a52", "avgrensning": "organisert med begrenset ansvar; hel eller delvis eierandel",
        "+kommentar": "[#309, kort nr. 54] Den eide er «virksomhet som yter spesialisthelsetjenester» (a52), ikke a64 (heleide datterselskaper, "
                      "hentet fra § 45)."},
    # #309 nr. 55
    ("spesialisthelsetjenesteloven", "u135", "Foretak kan ikke eie virksomhet som yter spesialisthelsetjenester sammen med andre enn foretak"): {
        "avgrensning": "sammen med andre enn foretak (sameie)", "+kommentar": "[#309, kort nr. 55] Organisasjonsformkravet i annet punktum er egen rad."},
    # #309 nr. 57
    ("domstolloven", "u121", "En dommer er uavhengig i sin dømmende virksomhet."): {
        "kommentar": "[slutning] instruksjonsforbudet er utledet av «uavhengig»; jf. Grunnloven § 95 annet ledd",
        "+kommentar": "[#309, kort nr. 57]"},
    # #309 nr. 61 (erstatningsraden legges til i regel_9)
    ("domstolloven", "u236", "Forliksrådet kan ikke ilegge straff eller erstatning etter dette kapitlet."): {
        "objekt": "rettergangsstraff", "avgrensning": "etter domstolloven kapittel 10",
        "+kommentar": "[#309, kort nr. 61] Erstatning er kompensasjon, ikke reaksjon (#355 beslutning 3): egen rad (beslutningskompetanse). "
                      "Tingrettens kompetanse står i u237. Objektet var «rettergangsstraff og erstatning»."},
}


def sett_felt(u, r, k, logg):
    for felt, verdi in r.items():
        if felt == "+kommentar":
            tilfoy(u, verdi)
        elif felt == "kommentar":
            if kommentar_foran(u, verdi):
                logg.append((k.kilde, u["id"], "kommentar"))
        elif felt == "avgrensning_foran":
            if verdi not in (u.get("avgrensning") or ""):
                u["avgrensning"] = verdi + ("; " + u["avgrensning"] if u.get("avgrensning") else "")
                logg.append((k.kilde, u["id"], f"avgrensning «{verdi}» foran"))
        elif u.get(felt, None) != verdi or felt not in u:
            if felt not in u and verdi is None:
                continue
            logg.append((k.kilde, u["id"], f"{felt}: {u.get(felt)!r} → {verdi!r}"))
            u[felt] = verdi


def regel_9(k, logg):
    d, kilde = k.d, k.kilde
    for u in d["utsagn"]:
        r = RETTING.get((kilde, u["id"], u["sitat"]))
        if r is not None:
            sett_felt(u, r, k, logg)

    if kilde == "domstolloven":
        # nr. 62: u248 slettes (avgrensningen står nå på u247); ny rad for uttalelseskompetansen etter § 236 tredje ledd.
        u248 = finn(d, "u248", "Tilsynsutvalget kan ikke vurdere forhold som kan overprøves etter reglene i rettspleielovgivningen for øvrig.")
        if u248:
            slett(d, u248, logg, kilde, "kort nr. 62 — avgrensning på u247 og uttalelsesraden")
        u247 = finn(d, "u247", "Tilsynsutvalget kan treffe vedtak om disiplinærtiltak")
        s = "Tilsynsutvalget kan gi en uttalelse om hva som er god dommerskikk, uten at dommeren ilegges disiplinærtiltak."
        if u247 and not finnes(d, DL + "§236/ledd-3", "kompetanse", "annet:uttalelse", s):
            r = ny_rad(d, u247, eid=DL + "§236/ledd-3", sitat=s, type="annet:uttalelse", til="dommer",
                       objekt="uttalelse om hva som er god dommerskikk", avgrensning=u247["avgrensning"], sikkerhet="hoy",
                       kommentar="[#356, kort nr. 62] Uttalelseskompetansen etter § 236 tredje ledd: ikke sanksjon (ingen disiplinærtiltak) og "
                                 "ikke bindende — annet:uttalelse, med samme avgrensning som u247 (§ 236 fjerde ledd gjelder hele utvalgets "
                                 "virksomhet).")
            logg.append((kilde, r["id"], "NY RAD kompetanse/annet:uttalelse (Tilsynsutvalget → dommere, § 236 tredje ledd)"))
        # nr. 61: erstatning som egen negativ rad
        u236 = finn(d, "u236", "Forliksrådet kan ikke ilegge straff eller erstatning etter dette kapitlet.")
        if u236 and not finnes(d, u236["eid"], "kompetanse", "beslutningskompetanse", u236["sitat"]):
            r = ny_rad(d, u236, type="beslutningskompetanse", objekt="erstatning",
                       kommentar="[#309, kort nr. 61] Skilt ut fra u236 (samme sitat, L13): erstatning er kompensasjon, ikke reaksjon.")
            logg.append((kilde, r["id"], "NY RAD kompetanse/beslutningskompetanse negativ, objekt erstatning (forliksrådet, § 213)"))

    if kilde == "sameloven":
        # nr. 28: samtykkekompetansen etter § 2-6 fjerde ledd
        u152 = next((u for u in d["utsagn"] if u["id"] == "u152"), None)
        s = "eller etter samtykke fra Sametinget"
        if u152 and not finnes(d, u152["eid"], "kompetanse", "samtykkekompetanse", s):
            r = ny_rad(d, u152, sitat=s, kategori="kompetanse", type="samtykkekompetanse", fra="a1", til=None,
                       objekt="tilgang for andre enn valgmyndigheten til registreringen av innføring i valgmanntallet i folkeregisteret",
                       avgrensning=None, sikkerhet="hoy",
                       kommentar="[#356, kort nr. 28 og punkt 7] Sametinget kan samtykke til at andre får tilgang (§ 2-6 fjerde ledd).")
            logg.append((kilde, r["id"], "NY RAD kompetanse/samtykkekompetanse (Sametinget, § 2-6 fjerde ledd)"))
        a141 = next((a for a in d["aktorer"] if a["id"] == "a141"), None)
        tekst = ("[#356, kort nr. 28] Oppløses av forskrift om valg til Sametinget (FOR-2008-12-19-1480, i korpuset), jf. § 2-10 "
                 "(Sametinget er øverste valgmyndighet). «forskrift_utenfor» betyr her utenfor lovteksten, ikke utenfor korpuset (lærdom 2).")
        if a141 and tekst not in (a141.get("kommentar") or ""):
            a141["kommentar"] = ((a141.get("kommentar") or "") + " " + tekst).strip()
            logg.append((kilde, "a141", "aktør: oppløsning presisert i kommentaren (forskrift om valg til Sametinget, i korpus)"))

    if kilde == "spesialisthelsetjenesteloven":
        # nr. 18: M-raden fjernes (L12)
        u42 = finn(d, "u42", "Alle foretakene skal ha en og samme arbeidsgivertilknytning")
        if u42:
            slett(d, u42, logg, kilde, "kort nr. 18 — medlemskapet var en slutning (L12); u41 beholdes")
        # nr. 50: u53 slås inn i u51
        u53 = finn(d, "u53", "Eier kan ikke utøve eierstyring i foretak utenom foretaksmøte")
        if u53:
            slett(d, u53, logg, kilde, "kort nr. 50 — avgrensning på u51")
        # nr. 54: u136 brukte samme feilaktør (a64). Den eide er «virksomhet som ikke yter spesialisthelsetjenester» (ny aktør).
        u136 = finn(d, "u136", "kan foretak eie virksomhet som ikke yter spesialisthelsetjenester alene eller sammen med andre")
        if u136 and u136["fra"] == "a64":
            aid, ny = ny_aktor(d, "a52", "virksomhet som ikke yter spesialisthelsetjenester",
                               eid_eksempler=[HFL + "§42/ledd-3"], antall_forekomster=1, referent=None,
                               kommentar="[#356, kort nr. 54] Annen virksomhet foretak kan eie (hfl § 42 tredje ledd); skal organiseres som "
                                         "selskap med begrenset ansvar.")
            if ny:
                logg.append((kilde, aid, "NY AKTØR «virksomhet som ikke yter spesialisthelsetjenester»"))
            u136["fra"] = aid
            tilfoy(u136, f"[#356, kort nr. 54] fra = den eide virksomheten ({aid}); a64 (heleide datterselskaper) var hentet fra § 45.")
            logg.append((kilde, "u136", f"fra: 'a64' → '{aid}'"))
        # nr. 55: organisasjonsformkravet i § 42 annet ledd annet punktum
        u135 = finn(d, "u135", "Foretak kan ikke eie virksomhet som yter spesialisthelsetjenester sammen med andre enn foretak")
        s = ("Dersom flere foretak eier virksomhet som yter spesialisthelsetjenester sammen, skal virksomheten organiseres som helseforetak "
             "eller som ansvarlig selskap i medhold av selskapsloven")
        if u135 and not finnes(d, u135["eid"], "konstituerende", "annet:organisasjonsformkrav", s):
            r = ny_rad(d, u135, sitat=s, kategori="konstituerende", type="annet:organisasjonsformkrav", fra="a52", til=None,
                       objekt="organisasjonsform: helseforetak eller ansvarlig selskap etter selskapsloven", polaritet="positiv",
                       avgrensning="når flere foretak eier virksomheten sammen", betinget=True, sikkerhet="hoy",
                       kommentar="[#309, kort nr. 55] § 42 annet ledd annet punktum.")
            logg.append((kilde, r["id"], "NY RAD konstituerende/annet:organisasjonsformkrav (§ 42 annet ledd annet punktum)"))
        a52 = next((a for a in d["aktorer"] if a["id"] == "a52"), None)
        tekst = ("[#309, kort nr. 55] Varianten «Virksomheter som er omfattet av loven her» hører til spesialisthelsetjenesteloven (§ 1-2). "
                 "Samme ordlyd i helseforetaksloven betyr foretakene (hfl § 2) — knytt varianten til rettskilden, ellers gjenkjennes feil aktør.")
        if a52 and tekst not in (a52.get("kommentar") or ""):
            a52["kommentar"] = ((a52.get("kommentar") or "") + " " + tekst).strip()
            logg.append((kilde, "a52", "aktør: varianten knyttet til spesialisthelsetjenesteloven i kommentaren"))

    if kilde == "helse-og-omsorgstjenesteloven":
        # nr. 14: motparten er en annen kommune den rusmiddelavhengige har tilknytning til (ny aktør, tekstform fra sitatet)
        u147 = next((u for u in d["utsagn"] if u["id"] == "u147"), None)
        if u147:
            aid, ny = ny_aktor(d, "a3", "en annen kommune som den rusmiddelavhengige har tilknytning til",
                               eid_eksempler=[HOTL + "§10-8/ledd-1"], antall_forekomster=1, entitetstype="rolle", distributiv=False,
                               kommentar="[#309, kort nr. 14] Kommunen ansvaret for å reise sak kan overføres til ved avtale (§ 10-8 første ledd).")
            if ny:
                logg.append((kilde, aid, "NY AKTØR «en annen kommune som den rusmiddelavhengige har tilknytning til»"))
            if u147.get("til") != aid:
                logg.append((kilde, "u147", f"til: {u147.get('til')!r} → {aid!r}"))
                u147["til"] = aid
        # v5 punkt 11: forbudet gjelder «andre private enn ideelle organisasjoner» (komplementklassen), ikke de ideelle
        u55 = finn(d, "u55", "Kommunen kan ikke inngå avtale med andre private enn ideelle organisasjoner om drift av brukerromsordning.")
        if u55:
            aid, ny = ny_aktor(d, "a25", "andre private enn ideelle organisasjoner", eid_eksempler=[HOTL + "§5-6/ledd-1"], antall_forekomster=1,
                               oppløsning="tekstlig",
                               kommentar="[#356, v5 punkt 11] Komplementklasse (jf. #349): private som ikke er ideelle organisasjoner. "
                                         "Kommunen kan ikke avtale drift av brukerromsordning med dem (§ 5-6).")
            if ny:
                logg.append((kilde, aid, "NY AKTØR «andre private enn ideelle organisasjoner»"))
            if u55["til"] != aid:
                tilfoy(u55, f"[#356, v5 punkt 11, lærdom 4] Negasjonen gikk feil vei: forbudet gjelder «andre private enn ideelle "
                            f"organisasjoner» ({aid}), ikke de ideelle (a25). Var til = a25, avgrensning «{u55.get('avgrensning')}».")
                logg.append((kilde, "u55", f"til: {u55['til']!r} → {aid!r}; avgrensning → «drift av brukerromsordning»"))
                u55["til"], u55["avgrensning"] = aid, "drift av brukerromsordning"


# ---- R10: juristrunden for denne saken (CLAUDE.md §23) — fylles etter runde 1 og 2 ----------------------------------------------------
JURIST_356 = {}


def regel_10(k, logg):
    for u in k.d["utsagn"]:
        r = JURIST_356.get((k.kilde, u["id"], u["sitat"]))
        if r is not None:
            sett_felt(u, r, k, logg)


def tell(d):
    return collections.Counter((u["kategori"], u["type"]) for u in d["utsagn"])


totalt_for, totalt_etter, logg, utenfor = collections.Counter(), collections.Counter(), [], []
for kilde in KILDER:
    sti = os.path.join(MAPPE, kilde + ".json")
    if not os.path.exists(sti):
        continue
    raw = open(sti, encoding="utf-8").read().replace("\r\n", "\n")
    innrykk = len(raw.split("\n")[1]) - len(raw.split("\n")[1].lstrip())
    d = json.loads(raw)
    antall_for = len(d["utsagn"])
    totalt_for.update(tell(d))
    k = Kontekst(kilde, d)
    regel_0(k, logg)
    regel_7(k, logg)
    regel_1(k, logg)
    regel_2(k, logg)
    regel_3(k, logg, utenfor)
    regel_4(k, logg)
    regel_5(k, logg)
    regel_6(k, logg, utenfor)
    if not KI:
        regel_8(k, logg)
        regel_9(k, logg)
        regel_10(k, logg)
    for u in d["utsagn"]:
        if k.tekst and u["sitat"] not in k.tekst.get(u["eid"], u["sitat"]):
            raise AssertionError(f"{kilde} {u['id']}: sitatet er ikke en eksakt delstreng av noden")
        if u.get("delegerbar") is not None:
            assert u["kategori"] == "kompetanse", f"{kilde} {u['id']}: delegerbar bare på kompetanse"
        assert u.get("fra") is None or any(a["id"] == u["fra"] for a in d["aktorer"]), f"{kilde} {u['id']}: ukjent fra"
        assert u.get("til") is None or any(a["id"] == u["til"] for a in d["aktorer"]), f"{kilde} {u['id']}: ukjent til"
    assert len({u["id"] for u in d["utsagn"]}) == len(d["utsagn"]), "dupliserte id-er"
    totalt_etter.update(tell(d))
    with open(sti, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(d, ensure_ascii=False, indent=innrykk))
    print(f"{kilde}: {antall_for} → {len(d['utsagn'])} utsagn")

print(f"\nMappe: {os.path.relpath(MAPPE, HER) or '.'}")
print(f"Endringer ({len(logg)}):")
for kilde, uid, hva in logg:
    print(f"  {kilde} {uid}: {hva}")
kat = lambda c, k: sum(v for (kk, _), v in c.items() if kk == k)
print(f"\nFør:   {sum(totalt_for.values())} utsagn, kompetanse {kat(totalt_for, 'kompetanse')}, relasjon {kat(totalt_for, 'relasjon')}, "
      f"konstituerende {kat(totalt_for, 'konstituerende')}")
print(f"Etter: {sum(totalt_etter.values())} utsagn, kompetanse {kat(totalt_etter, 'kompetanse')}, relasjon {kat(totalt_etter, 'relasjon')}, "
      f"konstituerende {kat(totalt_etter, 'konstituerende')}")
print("Per type (før → etter), de som er berørt:")
for (kk, t) in sorted(set(totalt_for) | set(totalt_etter)):
    if totalt_for[(kk, t)] != totalt_etter[(kk, t)]:
        print(f"  {kk}/{t}: {totalt_for[(kk, t)]} → {totalt_etter[(kk, t)]}")
print(f"\nStår utenfor / ikke konvertert: {len(utenfor)}")
for kilde, u, grunn in utenfor:
    print(f"  {kilde} {u['id']} ({u['kategori']}/{u['type']}, {grunn}): {u['sitat'][:100]}")
if not KI:
    print("  domstolloven u263 (relasjon/annet:kanal_via): «gis myndighet» til å oversende — ingen type for oversendelse, Johann avgjør")
