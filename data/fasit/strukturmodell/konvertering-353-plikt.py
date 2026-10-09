#!/usr/bin/env python3
"""
Deterministisk konvertering av strukturfasiten etter Johanns godkjenning av issue #353 (2026-10-08): pliktrelasjoner mellom
parter (kategori P, «plikt overfor motpart») og nodetypen «ordning». Ingen rad endres for hånd; alt under er regler som kan leses
og kjøres om igjen.

    python data/fasit/strukturmodell/konvertering-353-plikt.py            # fasitfilene
    python data/fasit/strukturmodell/konvertering-353-plikt.py ki-utdata  # de lagrede KI-utdataene (#308)

Idempotent: kjøres det på en allerede konvertert fil, finnes ingen av de gamle typene, folketrygden er alt en ordning, og
forvaltes_av-utsagnet finnes alt — ingenting endres.

Reglene (FORMAT.md og docs/33 §4.4/§4.5 er oppdatert tilsvarende):

  1. Typene som docs/33 §4.4 holdt utenfor strukturlaget («samarbeid, bistand, informasjonsdeling, konsultasjon,
     finansiering/betalingsansvar») blir kategorien «plikt» — (gammel kategori, gammel type) → plikttype:
       relasjon/samarbeider_med                      → samarbeidsplikt (avtaleplikt når SITATET sier «inngå (…)avtale med»)
       relasjon/bistar                               → bistandsplikt
       relasjon/annet:konsultasjonsplikt_overfor     → konsultasjonsplikt
       relasjon/annet:informasjonsdeling, annet:varsler → informasjonsplikt
       annet:finansieringsansvar/annet:dekker_utgifter_for, relasjon/annet:finansieringsansvar,
       ansvarsomrade/annet:finansieringsansvar       → betalingsplikt, TIL = NULL (se 3)
       relasjon/annet:finansierer, relasjon/annet:kompenserer → betalingsplikt, til beholdt («yter tilskudd TIL X», «yte X
                                                        kompensasjon», «mottar tilskudd fra» — teksten sier hvem som får pengene)
     KI-utdataene har egne annet-navn for det samme: relasjon/annet:dekker_utgifter → betalingsplikt (til = null),
     annet:plikt/annet:bistandsplikt → bistandsplikt, annet:plikttilstruktur/annet:opplysningsplikt og
     relasjon/annet:meldingsplikt_til → informasjonsplikt.
     Retningen er den annotatøren satte: fra = pliktsubjektet, til = motparten. Ingen kant snus, ingen dupliseres
     (gjensidighet sluttes aldri, L13: én kant per pliktsubjekt — fasiten har det alt).
  2. Modalitet (L14). [ENDRET, juristgjennomgangen 2026-10-09, godtatt av koordinatoren] I denne rekkefølgen:
     a. SITATET: «skal/skulle», «plikter/plikt/pålagt» → skal; «bør» → bor; «kan» → kan. Ett distinkt modalverb → det; flere → null.
     b. «A kan pålegge B …» (i sitatet eller setningen): «kan» tilhører A sin påleggskompetanse (K paleggskompetanse, styring —
        vedtakskompetanse med samme sitat gjøres om). B sin plikt er P med modalitet SKAL, avgrensning «når pålegg er gitt»,
        betinget, og det konkrete pålegget er en kilde utenfor korpus (helse- og omsorgstjenesteloven § 6-6).
     c. «A kan be B om å …» er en adgang til å be om bistand, ingen plikt for B: relasjon/bistar → relasjon/annet:anmodning_om_bistand,
        ikke P (domstolloven § 19 annet ledd, u43).
     d. «Det samme gjelder …» arver modalverbet i setningen før, når den har nøyaktig ett (spesialisthelsetjenesteloven § 5-2
        første ledd annet punktum → skal; hjemmelen er begge punktum, som står i samme ledd-node).
     e. SETNINGEN i nodeteksten som inneholder sitatet; for et punkt i en liste INNLEDNINGEN («Plikten til å konsultere … gjelder
        for» → skal).
     f. Normativ presens uten modalverb («dekkes av staten», «Staten dekker», «Staten yter») → skal, notert som presens.
     Ellers null, og raden listes.
  3. Betalingsmottakeren. [ENDRET, juristgjennomgangen] Sier teksten HVEM utgiftene er sine — genitiv foran «utgift» («Det
     regionale helseforetakets behandlings- … utgifter») eller «utgifter som påføres X» — er til = X (den annoterte til-aktøren;
     ingen ny aktør utledes, og det kodes ikke at det må være et annet RHF). Sier teksten bare hva utgiftene går TIL (formål:
     «utgifter til behandling …», «utgiftene til kontrollkommisjonenes virksomhet»), er til = null; den gamle til-aktøren står i
     kommentaren. Spesialisthelsetjenesteloven § 5-3 første ledd siste punktum («Dersom pasienten ikke kan dekke utgiftene selv,
     skal de dekkes av vedkommende helseinstitusjon»): tapsfordeling, ikke plikt overfor en motpart → annet:tapsfordeling.
  4. Ordning: aktøren «Folketrygden» (spesialisthelsetjenesteloven a39, entitetstype annet:trygdeordning) blir entitetstype
     «ordning» med undertype «trygdeordning». Forvaltningen er slått opp i folketrygdloven (lokal base, ELI lov/1997/02/28/19,
     gjeldende): § 21-11 a første ledd «Helsedirektoratet skal forvalte kapittel 5 …» gir relasjon/forvaltes_av fra
     Folketrygden til Helsedirektoratet (a35), avgrenset til kapittel 5. Folketrygdloven sier ikke «forvalte» om noe annet
     kapittel (§ 21-11 første ledd gir Arbeids- og velferdsdirektoratet VEDTAKSKOMPETANSE, ikke forvaltning) — de står som
     synlig hull i kommentaren. Ingen tilhorer-kant: ingen bestemmelse sier at folketrygden tilhører staten (§8).
     Noden § 21-11 a første ledd er lagt i noder/spesialisthelsetjenesteloven.json og folketrygdloven som ledsagende kilde
     (bare den ene noden — loven er ikke lest i sin helhet).
  4b. De andre aktørene annotatøren alt merket som ordning — «Folketrygden» i helse- og omsorgstjenesteloven (annet:trygdeordning)
     og «energifond» i energiloven (annet:fond) — blir entitetstype «ordning» med undertypen navnet sa (helse- og
     omsorgstjenestelovens folketrygd har ingen utsagn, så ingen forvaltes_av der).
  4c. relasjon/annet:forvalter_av («forvalteren av Energifondet» → energifond) blir relasjon/forvaltes_av med fra/til byttet
     (ordningen → forvalteren): samme forhold, navngitt fra ordningens side, som de snudde kodene i #330.

Står UTENFOR (listes, røres ikke): møteplikt og saksforberedelse (Johanns beslutning), rettigheter som er motstykket til en
plikt (konsultasjonsrett, høringsrett, informasjonstilgang — å lage plikten av retten ville vært å slutte gjensidighet), plikter
for private («Enhver plikter …»), hefte/garanti, bevilgning og avtalebasert oppgjør/tjenesteyting (avtalens innhold, ikke en
plikt overfor en motpart), ressursplikt («skaffe rettslokale» — ikke en av de seks typene).
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

# (gammel kategori, gammel type) → (plikttype, til beholdes?)
TIL_PLIKT = {
    ("relasjon", "samarbeider_med"): ("samarbeidsplikt", True),
    ("relasjon", "bistar"): ("bistandsplikt", True),
    ("relasjon", "annet:konsultasjonsplikt_overfor"): ("konsultasjonsplikt", True),
    ("relasjon", "annet:informasjonsdeling"): ("informasjonsplikt", True),
    ("relasjon", "annet:varsler"): ("informasjonsplikt", True),
    ("annet:finansieringsansvar", "annet:dekker_utgifter_for"): ("betalingsplikt", False),
    ("relasjon", "annet:finansieringsansvar"): ("betalingsplikt", False),
    ("ansvarsomrade", "annet:finansieringsansvar"): ("betalingsplikt", False),
    ("relasjon", "annet:finansierer"): ("betalingsplikt", True),
    ("relasjon", "annet:kompenserer"): ("betalingsplikt", True),
    # KI-utdataenes egne navn for det samme (#308-kjøringen).
    ("relasjon", "annet:dekker_utgifter"): ("betalingsplikt", False),
    ("annet:plikt", "annet:bistandsplikt"): ("bistandsplikt", True),
    ("annet:plikttilstruktur", "annet:opplysningsplikt"): ("informasjonsplikt", True),
    ("relasjon", "annet:meldingsplikt_til"): ("informasjonsplikt", True),
}


# Folketrygden (regel 4). Slått opp i lokal base 2026-10-09 (rettskilder.eli lov/1997/02/28/19, status Gjeldende).
FTRL = {"tittel": "Lov om folketrygd (folketrygdloven)", "eli": "https://lovdata.no/eli/lov/1997/02/28/19/nor"}
FTRL_NODE = {
    "rettskilde": FTRL["tittel"], "eli": FTRL["eli"], "eid": "https://lovdata.no/eli/lov/1997/02/28/19/nor/§21-11a/ledd-1",
    "nodeType": "ledd", "overskrift": None,
    "tekst": "Helsedirektoratet skal forvalte kapittel 5, sikre rett ytelse til den enkelte og ha ansvaret for å følge opp og "
             "kontrollere tjenester, ytelser og utbetalinger.",
}
FORVALTES_AV_SITAT = "Helsedirektoratet skal forvalte kapittel 5"


AVTALE = re.compile(r"\binngå\s+(?:en\s+)?\w*avtaler?\s+med\b", re.IGNORECASE)

MODAL = [
    (re.compile(r"\b(?:skal|skulle)\b", re.IGNORECASE), "skal"),
    (re.compile(r"\b(?:plikter|plikt|plikten|pålagt|pligter|pligt)\b", re.IGNORECASE), "skal"),  # pligt: eldre rettskrivning (domstolloven)
    (re.compile(r"\bbør\b", re.IGNORECASE), "bor"),
    (re.compile(r"\bkan\b", re.IGNORECASE), "kan"),
]
# [ENDRET, juristgjennomgangen 2026-10-09] To former der modalverbet tilhører en ANNEN aktør:
#   «A kan pålegge B …»        → B har plikten (skal) NÅR pålegg er gitt; A har påleggskompetanse (regel 2b).
#   «A kan be B om å …»        → en adgang til å be om bistand, ingen plikt for B (regel 2c, ikke P).
PALEGG = re.compile(r"\bkan\s+(?:\w+\s+){0,2}?pålegge\b", re.IGNORECASE)
ANMODNING = re.compile(r"\bkan\s+(?:\w+\s+){0,2}?be\s+\w+.*?\bom\b", re.IGNORECASE)
ANNEN_AKTORS_KOMPETANSE = re.compile(r"\b(?:skal|kan|bør)\s+(?:\w+\s+){0,2}?(?:kreve|anmode)\b", re.IGNORECASE)
DET_SAMME = re.compile(r"^\W*Det samme gjelder\b", re.IGNORECASE)
NORMATIV_PRESENS = re.compile(r"\b(?:dekkes|dekker|yter|ytes|gjelder|betales|betaler)\b", re.IGNORECASE)

# [Ny, juristgjennomgangen 2026-10-09] Navngitte tilfeller (kilde, id, sitatets begynnelse) — sitatet er med, så en omnummerert
# fasit ikke treffer feil rad (samme teknikk som SETTES_MED i konvertering-352-oppnevning.py).
TAPSFORDELING = {("spesialisthelsetjenesteloven", "u276", "Dersom pasienten ikke kan dekke utgiftene selv")}


def modaler(tekst):
    funnet = {m for rx, m in MODAL if rx.search(tekst)}
    return funnet.pop() if len(funnet) == 1 else ("flere" if funnet else None)


def setning_med(nodetekst, sitat):
    """(setningen som inneholder sitatet, setningen før). Setningsgrense = punktum/semikolon/kolon fulgt av mellomrom og stor
    bokstav — samme regel som MonsterStrukturkonverterer.Setningsgrense, så «jf. § 5-1» ikke deler en setning."""
    i = nodetekst.find(sitat)
    if i < 0:
        return None, None
    grense = re.compile(r"(?<=[.;:!?])\s+(?=[\[«(]?[A-ZÆØÅ])")
    grenser = [0] + [m.end() for m in grense.finditer(nodetekst)]
    start = max(g for g in grenser if g <= i)
    slutt = min([m.start() for m in grense.finditer(nodetekst) if m.start() >= i + len(sitat) - 1] or [len(nodetekst)])
    tidligere = [g for g in grenser if g < start]
    forrige = nodetekst[max(tidligere):start] if tidligere else None
    return nodetekst[start:slutt], forrige



def modalitet(u, tekster):
    """Regel 2 — (modalitet, kilde, kandidatgrunn). Kilde: sitat | setning | forrige setning («Det samme gjelder») |
    innledningen (punkt i en liste) | presens | pålegg."""
    sitat = u["sitat"]
    nodetekst = tekster.get(u["eid"], "")
    setning, forrige = setning_med(nodetekst, sitat) if nodetekst else (None, None)
    helhet = (setning or "") + " " + sitat
    if PALEGG.search(helhet):
        return "skal", "pålegg", None
    if ANNEN_AKTORS_KOMPETANSE.search(helhet):
        return None, None, "modalverbet hører til en annen aktørs kompetanse"
    m = modaler(sitat)
    if m == "flere":
        return None, None, "flere modalverb i sitatet"
    if m:
        return m, "sitat", None
    if DET_SAMME.search(sitat) or (setning and DET_SAMME.search(setning)):
        fm = modaler(forrige or "")
        if fm and fm != "flere":
            return fm, "forrige setning («Det samme gjelder»)", None
    if setning:
        m = modaler(setning)
        if m == "flere":
            return None, None, "flere modalverb i setningen"
        if m:
            return m, "setning", None
    if "/punkt-" in u["eid"]:
        forelder = tekster.get(u["eid"].rsplit("/punkt-", 1)[0], "")
        m = modaler(forelder)
        if m and m != "flere":
            return m, "innledningen til lista", None
    if NORMATIV_PRESENS.search(setning or sitat):
        return "skal", "presens", None
    return None, None, "ingen modalverb og ingen normativ presens"


def betalingsmottaker_i_teksten(u, former):
    """Regel 3 — står det HVEM utgiftene er sine? Genitiv foran «utgift» («Det regionale helseforetakets … utgifter») eller
    «som påføres X». Bare den annoterte til-aktørens egne former telles (ingen ny aktør utledes). «Utgiftene til X» er formål."""
    if not u.get("til"):
        return False
    sitat = u["sitat"]
    if re.search(r"\bsom\s+påføres\b", sitat, re.IGNORECASE):
        return True
    utgift = re.search(r"utgift", sitat, re.IGNORECASE)
    if not utgift:
        return False
    foran = sitat[:utgift.start()].lower()
    return any(re.search(r"\b" + re.escape(f.lower()) + r"s\b", foran) for f in former.get(u["til"], []))


def nodetekster(kilde):
    sti = os.path.join(HER, "noder", kilde + ".json")
    return {n["eid"]: n.get("tekst") or "" for n in json.load(open(sti, encoding="utf-8"))} if os.path.exists(sti) else {}


def tilfoy(u, felt, tekst):
    u[felt] = ((u.get(felt) or "") + (" " if u.get(felt) else "") + tekst).strip()


def konverter(d, kilde, tekster):
    endret, kandidater, modalkilde = collections.Counter(), [], collections.Counter()
    navn = {a["id"]: a["tekstform"] for a in d["aktorer"]}
    former = {a["id"]: [a["tekstform"]] + list(a.get("varianter") or []) for a in d["aktorer"]}
    pliktsubjekt_ved_palegg = {}
    for u in d["utsagn"]:
        gammel = (u["kategori"], u["type"])
        if gammel not in TIL_PLIKT:
            continue
        if (kilde, u["id"]) in {(k, i) for k, i, _ in TAPSFORDELING} and any(
                u["sitat"].startswith(s) for k, i, s in TAPSFORDELING if (k, i) == (kilde, u["id"])):
            u["kategori"], u["type"] = "annet:tapsfordeling", "annet:tapsfordeling"
            tilfoy(u, "kommentar", "[#353, juristgjennomgangen] Tapsfordeling, ikke plikt overfor en motpart: institusjonen bærer "
                                   "selv tapet når pasienten ikke kan betale.")
            endret[f"{gammel[0]}/{gammel[1]} → annet:tapsfordeling"] += 1
            continue
        if gammel == ("relasjon", "bistar") and ANMODNING.search(u["sitat"]) and not PALEGG.search(u["sitat"]):
            u["type"] = "annet:anmodning_om_bistand"
            tilfoy(u, "kommentar", "[#353, juristgjennomgangen] «A kan be B om å …» er en adgang til å be om bistand; teksten sier "
                                   "ikke at B skal — ingen plikt.")
            endret["relasjon/bistar → relasjon/annet:anmodning_om_bistand («kan be … om»)"] += 1
            continue
        ny_type, behold_til = TIL_PLIKT[gammel]
        if ny_type == "samarbeidsplikt" and AVTALE.search(u["sitat"]):
            ny_type = "avtaleplikt"
        u["kategori"], u["type"] = "plikt", ny_type
        if not behold_til and u.get("til"):
            if betalingsmottaker_i_teksten(u, former):
                tilfoy(u, "kommentar", f"[#353] Til = «{navn.get(u['til'], u['til'])}»: teksten sier hvem utgiftene er sine "
                                       "(genitiv / «som påføres»).")
                endret["betaling: til beholdt (genitiv / «som påføres»)"] += 1
            else:
                tilfoy(u, "kommentar", f"[#353] Til var «{navn.get(u['til'], u['til'])}», men teksten sier bare hva utgiftene går "
                                       "til (formål), ikke hvem som får pengene — til = null.")
                u["til"] = None
                endret["betaling: til satt til null (formål)"] += 1
        m, kilde_m, grunn = modalitet(u, tekster)
        if m is not None:
            u["modalitet"] = m
            modalkilde[f"modalitet fra {kilde_m}"] += 1
            if kilde_m == "pålegg":
                tilfoy(u, "avgrensning", "når pålegg er gitt")
                u["betinget"], u["kilde_utenfor_korpus"] = True, True
                tilfoy(u, "kommentar", "[#353, juristgjennomgangen] «kan» hører til påleggskompetansen; plikten er «skal» når pålegg er "
                                       "gitt, og det konkrete pålegget er en kilde utenfor korpus.")
                pliktsubjekt_ved_palegg[u["eid"]] = u.get("fra")
            elif kilde_m == "presens":
                tilfoy(u, "kommentar", "[#353] Modalitet skal: normativ presens.")
            elif kilde_m.startswith("forrige"):
                tilfoy(u, "kommentar", "[#353] Modaliteten arves fra setningen «Det samme gjelder» viser til; hjemmelen er begge punktum.")
        else:
            modalkilde["modalitet null"] += 1
            if grunn:
                kandidater.append((u, grunn))
        endret[f"{gammel[0]}/{gammel[1]} → plikt/{ny_type}"] += 1
    # Regel 2b, kompetansesiden: «A kan pålegge …» er påleggskompetanse (styring), ikke vedtakskompetanse.
    for u in d["utsagn"]:
        if (u["kategori"], u["type"]) == ("kompetanse", "vedtakskompetanse") and PALEGG.search(u["sitat"]):
            u["type"] = "paleggskompetanse"
            if not u.get("til") and pliktsubjekt_ved_palegg.get(u["eid"]):
                u["til"] = pliktsubjekt_ved_palegg[u["eid"]]
            tilfoy(u, "kommentar", "[#353, juristgjennomgangen] Påleggskompetanse (styring): «kan» hører hit; pliktsubjektets plikt er en "
                                   "egen P-kant med modalitet skal når pålegg er gitt.")
            endret["kompetanse/vedtakskompetanse → paleggskompetanse («kan pålegge»)"] += 1
    return endret, kandidater, modalkilde


def ordning(d, noder_sti):
    """Regel 4 — bare spesialisthelsetjenesteloven i fasiten (KI-utdataene har ikke aktøren)."""
    endret = collections.Counter()
    folketrygden = next((a for a in d["aktorer"] if a["tekstform"] == "Folketrygden"), None)
    hdir = next((a for a in d["aktorer"] if a["tekstform"] == "Helsedirektoratet"), None)
    if folketrygden is None or hdir is None:
        return endret
    if folketrygden["entitetstype"] != "ordning":
        folketrygden["entitetstype"] = "ordning"
        folketrygden["undertype"] = "trygdeordning"
        folketrygden["kommentar"] = (
            "Ordning (ikke aktør, #353): en trygdeordning loven gir en funksjon — pliktsubjekt for betaling (§ 5-3 annet ledd). "
            "Forvaltes av Helsedirektoratet for kapittel 5 (folketrygdloven § 21-11 a første ledd, eget utsagn). Andre kapitler: "
            "folketrygdloven sier ikke «forvalte» om dem (§ 21-11 første ledd gir Arbeids- og velferdsdirektoratet "
            "vedtakskompetanse, ikke forvaltning) — synlig hull, ikke gjettet. Hvilket kapittel betalingsplikten i § 5-3 annet "
            "ledd hører under, sier spesialisthelsetjenesteloven ikke. Tilhører staten? Ingen bestemmelse sier det — ingen "
            "tilhorer-kant (synlig hull). [Før #353: entitetstype annet:trygdeordning; «forvaltes av Nav (utenfor korpus)» var "
            "en antakelse uten kilde.]")
        endret["Folketrygden → ordning/trygdeordning"] += 1
    if not any(x["eli"] == FTRL["eli"] for x in d["ledsagende"]):
        d["ledsagende"].append(dict(FTRL))
        endret["folketrygdloven som ledsagende kilde"] += 1
    if not any(u["type"] == "forvaltes_av" and u["fra"] == folketrygden["id"] for u in d["utsagn"]):
        nr = max(int(u["id"][1:]) for u in d["utsagn"]) + 1
        d["utsagn"].append({
            "id": f"u{nr}", "eid": FTRL_NODE["eid"], "sitat": FORVALTES_AV_SITAT, "kategori": "relasjon", "type": "forvaltes_av",
            "fra": folketrygden["id"], "til": hdir["id"], "objekt": None, "polaritet": "positiv",
            "avgrensning": "folketrygdloven kapittel 5 (stønad ved helsetjenester)", "betinget": False,
            "kilde_utenfor_korpus": False, "sikkerhet": "hoy",
            "kommentar": "#353: slått opp i folketrygdloven (lokal base), ikke gjettet. Gjelder bare kapittel 5; andre kapitler er "
                         "et synlig hull (se aktøren).",
        })
        d["noder_lest"] = d.get("noder_lest", 0) + 1
        endret["relasjon/forvaltes_av (Folketrygden → Helsedirektoratet, ftrl § 21-11 a første ledd)"] += 1
        hdir.setdefault("eid_eksempler", [])
        if FTRL_NODE["eid"] not in hdir["eid_eksempler"] and len(hdir["eid_eksempler"]) < 5:
            hdir["eid_eksempler"].append(FTRL_NODE["eid"])
    raw = open(noder_sti, encoding="utf-8", newline="").read()
    if not any(n["eid"] == FTRL_NODE["eid"] for n in json.loads(raw)):
        # Fila er psql-eksportens ene linje («"felt" : verdi»); noden føyes til i samme form, så diffen er bare den.
        slutt = raw.rstrip().rfind("]")
        ny = "{" + ", ".join(f"{json.dumps(k)} : {json.dumps(v, ensure_ascii=False)}" for k, v in FTRL_NODE.items()) + "}"
        with open(noder_sti, "w", encoding="utf-8", newline="") as f:
            f.write(raw[:slutt] + ", " + ny + raw[slutt:])
        assert any(n["eid"] == FTRL_NODE["eid"] for n in json.load(open(noder_sti, encoding="utf-8")))
        endret["noden § 21-11 a første ledd lagt i noder/"] += 1
    return endret


ORDNINGSTYPER = {"annet:trygdeordning": "trygdeordning", "annet:fond": "fond", "annet:tilskuddsordning": "tilskuddsordning"}


def andre_ordninger(d):
    """Regel 4b/4c — de andre aktørene annotatøren alt merket som en ordning (annet:trygdeordning, annet:fond) blir entitetstype
    «ordning» med undertypen navnet sa; relasjon/annet:forvalter_av («forvalteren av Energifondet») blir relasjon/forvaltes_av
    med fra = ordningen og til = forvalteren — samme forhold, bare navngitt fra den andre siden (som #330)."""
    endret = collections.Counter()
    for a in d["aktorer"]:
        if a.get("entitetstype") in ORDNINGSTYPER:
            gammel = a["entitetstype"]
            a["entitetstype"], a["undertype"] = "ordning", ORDNINGSTYPER[gammel]
            a["kommentar"] = ((a.get("kommentar") or "") + f" [#353] Entitetstype var «{gammel}».").strip()
            endret[f"{gammel} → ordning/{a['undertype']}"] += 1
    ordninger = {a["id"] for a in d["aktorer"] if a.get("entitetstype") == "ordning"}
    for u in d["utsagn"]:
        if (u["kategori"], u["type"]) == ("relasjon", "annet:forvalter_av") and u.get("til") in ordninger:
            u["type"], u["fra"], u["til"] = "forvaltes_av", u["til"], u["fra"]
            u["kommentar"] = ((u.get("kommentar") or "") + " [#353] Var annet:forvalter_av (forvalter → ordning); snudd til "
                              "forvaltes_av (ordning → forvalter), samme forhold.").strip()
            endret["relasjon/annet:forvalter_av → relasjon/forvaltes_av (snudd)"] += 1
    return endret


def a44(d):
    """Johanns kort helse-og-omsorgstjenesteloven:a44 — beskrivelsen av hvordan paret avgjøres (tilpasning punkt 5)."""
    a = next((a for a in d["aktorer"] if a["tekstform"] == "det regionale helseforetaket i helseregionen"), None)
    if a is None or "[#353]" in (a.get("kommentar") or ""):
        return 0
    a["kommentar"] = (a.get("kommentar") or "") + (
        " [#353] Avtaleplikten (§ 6-1, § 11-4) er én P-kant mellom klassene «kommunen» og «det regionale helseforetaket i "
        "helseregionen». Paret kommune X ↔ RHF Y regnes ut via område (kommunen ligger i fylket, fylket i helseregionen, RHF-et har "
        "ansvarsområde i helseregionen); helseregionenes inndeling står ikke i lov, den kommer fra vedtektene (ekstern kilde, #340).")
    return 1


def tell(d):
    return collections.Counter((u["kategori"], u["type"]) for u in d["utsagn"])


totalt_for, totalt_etter, alle_endringer, alle_kandidater, alle_modal = (collections.Counter(), collections.Counter(),
                                                                         collections.Counter(), [], collections.Counter())
for kilde in KILDER:
    sti = os.path.join(MAPPE, kilde + ".json")
    if not os.path.exists(sti):
        continue
    raw = open(sti, encoding="utf-8").read().replace("\r\n", "\n")
    innrykk = len(raw.split("\n")[1]) - len(raw.split("\n")[1].lstrip())
    d = json.loads(raw)
    antall_for = len(d["utsagn"])
    totalt_for.update(tell(d))
    endret, kandidater, modal = konverter(d, kilde, nodetekster(kilde))
    if not KI and kilde == "spesialisthelsetjenesteloven":
        endret.update(ordning(d, os.path.join(HER, "noder", kilde + ".json")))
    endret.update(andre_ordninger(d))
    if not KI and kilde == "helse-og-omsorgstjenesteloven" and a44(d):
        endret["a44: kommentar om paret via område"] += 1
    assert len(d["utsagn"]) >= antall_for, "ingen rader skal forsvinne"
    totalt_etter.update(tell(d))
    alle_endringer.update(endret)
    alle_modal.update(modal)
    alle_kandidater += [(kilde, u, g) for u, g in kandidater]
    with open(sti, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(d, ensure_ascii=False, indent=innrykk))
    print(f"{kilde}: {antall_for} → {len(d['utsagn'])} utsagn, {sum(v for k, v in endret.items() if '→ plikt' in k)} til plikt")

print(f"\nMappe: {os.path.relpath(MAPPE, HER) or '.'}")
print("Endringer:")
for k, v in sorted(alle_endringer.items(), key=lambda kv: (-kv[1], kv[0])):
    print(f"  {v:4}  {k}")
print("Modalitet:")
for k, v in sorted(alle_modal.items()):
    print(f"  {v:4}  {k}")
kat = lambda c, k: sum(v for (kk, _), v in c.items() if kk == k)
print(f"\nFør:   {sum(totalt_for.values())} utsagn, relasjon {kat(totalt_for, 'relasjon')}, plikt {kat(totalt_for, 'plikt')}")
print(f"Etter: {sum(totalt_etter.values())} utsagn, relasjon {kat(totalt_etter, 'relasjon')}, plikt {kat(totalt_etter, 'plikt')}")
print("Plikt per type (etter):")
for (k, t), v in sorted(totalt_etter.items()):
    if k == "plikt":
        print(f"  {v:4}  {t}")
print(f"\nPlikter uten modalitet med en grunn som bør leses (for Johann/#309): {len(alle_kandidater)}")
for kilde, u, grunn in alle_kandidater:
    print(f"  {kilde} {u['id']} ({u['type']}, {grunn}): {u['sitat'][:110]}")
print("\nStår utenfor (ikke konvertert):")
UTENFOR = re.compile(r"moteplikt|saksforberedelse|forbereder_sak|fremmer_sak|fremmer_budsjett|oversender_sak|forelegges|intern_forelegging|"
                     r"konsultasjonsrett|horingsrett|informasjonstilgang|informasjonsinnhenting|hefter_for|bevilgning|avtalebasert|"
                     r"ressursplikt|medvirker|anmod|opplysningspalegg|avleverer_til|kanal_via")
for (k, t), v in sorted(totalt_etter.items()):
    if UTENFOR.search(t):
        print(f"  {v:4}  {k}/{t}")
