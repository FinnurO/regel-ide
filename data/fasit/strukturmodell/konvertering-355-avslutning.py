#!/usr/bin/env python3
"""
Deterministisk konvertering av strukturfasiten etter Johanns beslutninger på issue #355 (godkjent 2026-10-09), «avslutning speiler
innsetting», med kommentarene samme dag: beslutningene om fasitkortene domstolloven:u22, sameloven:u165 og domstolloven:u118, og
utfallet av juristdebatten om fasitkortene (kort nr. 29 og nr. 58). Ingen rad endres for hånd; alt under er regler som kan leses og
kjøres om igjen.

    python data/fasit/strukturmodell/konvertering-355-avslutning.py            # fasitfilene
    python data/fasit/strukturmodell/konvertering-355-avslutning.py ki-utdata  # de lagrede KI-utdataene (#308)

Idempotent: kjøres det på en allerede konvertert fil, står undertypene alt, de delte radene finnes, de nye radene finnes, og ingen
foreleggingskompetanse er igjen — ingenting endres.

Reglene (FORMAT.md og docs/33 §4.3 er oppdatert tilsvarende):

  1. Undertype fra sitatet (sakens beslutning 1 og 2) — for kompetanse av typene oppnevning, avsetting, overprøving og vedtak som
     ikke har en undertype: den ENE undertypen hvis ordstammer (kompetanseleksikon.json, «undertyper» — samme fil mønsterlaget leser)
     står ved ordstart i sitatet. Ingen treff eller treff for flere undertyper → ingen undertype (gjettes ikke). Nye undertyper:
       oppnevningskompetanse: utnevning («utnevnes»), konstitusjon («konstitueres», «konstitusjon»)
       avsettingskompetanse:  avsetting («avsettes»), oppsigelse («sies opp»), avskjed («avskjediges»)
       vedtakskompetanse:     tilbakekall («tilbakekalles», «trekke tilbake»)
     En undertype som alt står, røres ikke (det annotatøren eller #352 sa, vinner).
  2. Én rad per utsagn (L13) der sitatet har flere undertyper og saken har avgjort dem (kildene står i tabellen i saken):
       spesialisthelsetjenesteloven u118 «Styret treffer vedtak om å si opp eller avskjedige daglig leder» → to rader, undertype
       oppsigelse og avskjed (samme sitat, samme ender).
  3. Kort nr. 58 (domstolloven u122, § 55 femte ledd; Grunnloven § 22 annet ledd) — setningen har tre regler, og forflytning er ikke
     avsetting (L13, beslutning 1): (1) avsetting/oppsigelse, negativ, fra null, til dommere; (2) avsetting/avskjed, negativ, fra null,
     til dommere, avgrensning «unntatt etter rettergang og dom»; (3) kompetanse/annet:forflytning, negativ, fra null, til dommere,
     avgrensning «mot sin vilje». Kommentar «jf. Grunnloven § 22 annet ledd» på alle tre.
  4. Kort nr. 29 (helse-og-omsorgstjenesteloven u145, § 10-7 første ledd «Kommunen er part i saken») — partsposisjon hører til
     regellaget (beslutning 5): kategori annet:partsposisjon, til = null (tingretten er ikke motpart; overprøvingen står alt som u144).
  5. Beslutning 5 — ankeadgang er en partsposisjon i regellaget, utenfor strukturlaget (som møteplikt, docs/33 §4.4): domstolloven
     u102 (§ 37) og u108 (§ 46 annet ledd) kompetanse/annet:ankekompetanse → annet:partsposisjon/annet:ankeadgang. Den strukturelle
     delen av § 37, «paa det offentliges vegne», er en ny rad relasjon/representerer fra departementet til staten.
  6. Beslutning 4 — ankeinstans: domstolloven u275 (inndelingsforskriften § 10 første ledd) har til = tingrettene (organet), ikke
     rettskretsene (området). Rettskretsen er avgrensning (L1). Paret lagmannsrett ↔ tingrett regnes ut via tingrett → lagsogn →
     lagdømme (#345) og lagres ikke dobbelt.
  7. Forelegging tas ut som kompetansetype (kommentaren 2026-10-09, beslutning 3). Hver kompetanse/foreleggingskompetanse
     klassifiseres etter RETTSVIRKNINGEN, slått opp i tabellen FORELEGGING under (kilde, id, sitat). Domstolloven § 51 a (beslutning 4):
     EFTA-domstolens svar er en rådgivende tolkningsuttalelse (ODA artikkel 34, kilde utenfor korpus) → plikt/konsultasjonsplikt fra
     avsenderen med modalitet «kan», objekt «rådgivende tolkningsuttalelse»; forliksrådene (u118) samme kant med negativ polaritet.
     En foreleggingskompetanse som IKKE står i tabellen, røres ikke og listes — rettsvirkningen gjettes ikke.
  8. Beslutning 4, første ledd siste punktum: «Rettens beslutning om at et tolkningsspørsmål skal eller ikke skal forelegges for
     EFTA-domstolen, kan ikke angripes ved anke» blir en ny negativ anke-rad (overprøving/anke, fra null, til domstolene), hvis den
     ikke finnes.
  9. Beslutningen om u22/u165 («X skal ha en Y» for ett navngitt organ er ikke T, som bare er klassenivå): G del_av, fra = enheten
     eller STILLINGEN, til = organet (snudd i forhold til skal_finnes). Fasiten uttrykker G del_av som relasjon/del_av (designtest.py:
     «del_av@relasjon» → G, som u14 og u158). Stillingen «direktør» (hr_dir) er rollen knyttet til nettopp Høyesterett; referenten
     settes til «Høyesteretts direktør».

  10. Juristrunden (CLAUDE.md §23, 2026-10-09): agenten jurist-utfordrer vurderte de 29 radene regel 1–9 endret eller la til.
     13 holdt, 16 fikk innvending, alle akseptert (ingen uenighet til Johann): konstitusjonsradene får motparten dommere og
     varigheten/vilkårene i avgrensningen; Innstillingsrådets konstitusjon etter § 55 f annet ledd (egen rad, L13); u120 delegerbar
     = false (Grl. § 21: Kongen i statsråd); u216 «kontaktpsykolog utnevnes» = oppnevning, ikke utnevning (ikke embete); u94 er
     unntaket fra avsettingen (undertype avsetting, fra = null som u93); vilkårene i avgrensningen på tilbakekallene (u186, u205–u207)
     og på § 51 a (u117); nye rader: protokollsekretærer og utredere del_av Høyesterett (§ 9), Høyesteretts anke etter § 37, og
     § 55 h første ledd (kort nr. 58 for midlertidige dommere, ny aktør «midlertidig dommer»).

Regel 2–10 er slått opp på (kilde, id, sitat), så en omnummerert fasit ikke treffer feil rad; de gjelder bare fasiten. Regel 1 og 7
gjelder også KI-utdataene.

Står utenfor (listes, røres ikke): relasjon/annet:forelegges_for (domstolloven u52, u56: «… skal forelegges for Stortinget») og
relasjon/annet:intern_forelegging (helse- og omsorgstjenesteloven u141, u142) er ikke foreleggingskompetanse; de er saksgang
(«selve foreleggelsen er saksgang og hører til regellaget», beslutning 3) og står alt som senere lag.
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
LEKSIKON = os.path.join(HER, "..", "..", "..", "src", "RegelIde.Data", "Strukturkonvertering", "kompetanseleksikon.json")
UNDERTYPER = json.load(open(LEKSIKON, encoding="utf-8"))["undertyper"]
TYPER_MED_UNDERTYPE = {u["type"] for u in UNDERTYPER}

DL = "https://lovdata.no/eli/lov/1915/08/13/5/nor/"

# Regel 7: foreleggingskompetanse → rettsvirkningen. (kilde, id, sitat) → endringene.
KONSULTASJON = {"kategori": "plikt", "type": "konsultasjonsplikt", "modalitet": "kan", "objekt": "rådgivende tolkningsuttalelse",
                "kilde_utenfor_korpus": True}
FORELEGGING = {
    ("domstolloven", "u117", "forelegge tolkningsspørsmålet for EFTA-domstolen"): KONSULTASJON,
    ("domstolloven", "u118", "Forliksrådene har ikke adgang til å forelegge tolkningsspørsmål for EFTA-domstolen."): KONSULTASJON,
}
FORELEGGING_KOMMENTAR = (
    "[#355, beslutning 3 og 4] Forelegging er ikke lenger en kompetansetype. Rettsvirkningen er rådgivende: EFTA-domstolen gir en "
    "rådgivende tolkningsuttalelse (ODA artikkel 34, kilde utenfor korpus) → plikt/konsultasjon fra avsenderen, modalitet kan. "
    "Var kompetanse/foreleggingskompetanse.")

ANKE_51A_SITAT = ("Rettens beslutning om at et tolkningsspørsmål skal eller ikke skal forelegges for EFTA-domstolen, kan ikke angripes "
                  "ved anke")
REPRESENTERER_SITAT = "paa det offentliges vegne"
GRL22 = "jf. Grunnloven § 22 annet ledd"


def nodetekster(kilde):
    sti = os.path.join(HER, "noder", kilde + ".json")
    return {n["eid"]: n.get("tekst") or "" for n in json.load(open(sti, encoding="utf-8"))} if os.path.exists(sti) else {}


def undertype_fra_sitat(fasittype, sitat):
    treff = {u["undertype"] for u in UNDERTYPER if u["type"] == fasittype
             and any(re.search(r"\b" + s, sitat, re.IGNORECASE) for s in u["stammer"])}
    return treff.pop() if len(treff) == 1 else None


def tilfoy(u, tekst):
    if tekst not in (u.get("kommentar") or ""):
        u["kommentar"] = ((u.get("kommentar") or "") + " " + tekst).strip()


def neste_id(d):
    return f"u{max(int(u['id'][1:]) for u in d['utsagn']) + 1}"


def finn(d, uid, sitat):
    return next((u for u in d["utsagn"] if u["id"] == uid and u["sitat"] == sitat), None)


def finnes(d, eid, kategori, type_, sitat, **felt):
    return any(u["eid"] == eid and u["kategori"] == kategori and u["type"] == type_ and u["sitat"] == sitat
               and all(u.get(k) == v for k, v in felt.items()) for u in d["utsagn"])


def ny_rad(d, mal, **felt):
    """En ny rad bygget på en eksisterende (samme eId, sikkerhet …), med feltene overstyrt. Feltrekkefølgen følger malen."""
    rad = {k: v for k, v in mal.items() if k not in ("delegerbar", "undertype", "modalitet", "verifisert_av")}
    rad.update(felt)
    rad["id"] = neste_id(d)
    d["utsagn"].append(rad)
    return rad


def regel_1(d, tekster, endret):
    for u in d["utsagn"]:
        if u["kategori"] == "kompetanse" and u["type"] in TYPER_MED_UNDERTYPE and not u.get("undertype"):
            ut = undertype_fra_sitat(u["type"], u["sitat"])
            if ut is not None:
                u["undertype"] = ut
                endret[f"undertype fra sitatet: {u['type']}/{ut}"] += 1


def regel_2(d, kilde, endret):
    if kilde != "spesialisthelsetjenesteloven":
        return
    sitat = "Styret treffer vedtak om å si opp eller avskjedige daglig leder"
    u = finn(d, "u118", sitat)
    if u is None:
        return
    if u.get("undertype") != "oppsigelse":
        u["undertype"] = "oppsigelse"
        tilfoy(u, "[#355, L13] Sitatet har to regler: oppsigelse (denne raden) og avskjed (egen rad).")
        endret["spesialisthelsetjenesteloven u118 → avsetting/oppsigelse"] += 1
    if not finnes(d, u["eid"], "kompetanse", "avsettingskompetanse", sitat, undertype="avskjed"):
        r = ny_rad(d, u, undertype="avskjed",
                   kommentar="[#355, L13] Skilt ut fra u118 (samme sitat): avskjed er en egen regel ved siden av oppsigelse.")
        endret[f"ny rad {r['id']}: spesialisthelsetjenesteloven § 36 første ledd avsetting/avskjed"] += 1


def regel_3(d, kilde, endret):
    if kilde != "domstolloven":
        return
    eid = DL + "§55/ledd-5"
    u = next((x for x in d["utsagn"] if x["id"] == "u122" and x["eid"] == eid), None)
    if u is None:
        return
    if u["sitat"] == "Dommere kan ikke sies opp eller forflyttes mot sin vilje og kan bare avskjediges etter rettergang og dom.":
        gammel = dict(u)
        u.update({"sitat": "Dommere kan ikke sies opp", "kategori": "kompetanse", "type": "avsettingskompetanse",
                  "undertype": "oppsigelse", "polaritet": "negativ", "fra": None, "avgrensning": None})
        u["kommentar"] = (f"{GRL22}. [#355, kort nr. 58] Setningen har tre regler (L13): oppsigelse (denne raden), avskjed bare etter "
                          f"dom og forflytning mot sin vilje (egne rader). Var én avsettingskompetanse med avgrensning "
                          f"«{gammel['avgrensning']}»; kommentaren var «{gammel.get('kommentar') or ''}».")
        endret["domstolloven u122 → avsetting/oppsigelse (kort nr. 58)"] += 1
    if not finnes(d, eid, "kompetanse", "avsettingskompetanse", "kan bare avskjediges etter rettergang og dom"):
        r = ny_rad(d, u, sitat="kan bare avskjediges etter rettergang og dom", undertype="avskjed",
                   avgrensning="unntatt etter rettergang og dom", kommentar=f"{GRL22}. [#355, kort nr. 58] Skilt ut fra u122.")
        endret[f"ny rad {r['id']}: domstolloven § 55 femte ledd avsetting/avskjed (kort nr. 58)"] += 1
    if not finnes(d, eid, "kompetanse", "annet:forflytning", "forflyttes mot sin vilje"):
        r = ny_rad(d, u, sitat="forflyttes mot sin vilje", type="annet:forflytning", avgrensning="mot sin vilje",
                   kommentar=f"{GRL22}. [#355, kort nr. 58] Skilt ut fra u122: forflytning er ikke avsetting (L13, beslutning 1).")
        r.pop("undertype", None)
        endret[f"ny rad {r['id']}: domstolloven § 55 femte ledd annet:forflytning (kort nr. 58)"] += 1


def regel_4(d, kilde, endret):
    if kilde != "helse-og-omsorgstjenesteloven":
        return
    u = finn(d, "u145", "Kommunen er part i saken.")
    if u is None or u["kategori"] == "annet:partsposisjon":
        return
    tilfoy(u, f"[#355, kort nr. 29] Partsposisjon hører til regellaget (beslutning 5). Var kategori «{u['kategori']}» med til = "
              f"tingretten; tingretten er ikke motpart (overprøvingen står som u144).")
    u["kategori"], u["til"] = "annet:partsposisjon", None
    endret["helse-og-omsorgstjenesteloven u145 → annet:partsposisjon, til = null (kort nr. 29)"] += 1


def regel_5(d, kilde, endret):
    if kilde != "domstolloven":
        return
    for uid, sitat in [("u102", "kan vedkommende regjeringsdepartement paa det offentliges vegne fremsætte indsigelse og erklære anke til Høiesteret"),
                       ("u108", "Rettens avgjørelse kan ankes av vedkommende regjeringsdepartement.")]:
        u = finn(d, uid, sitat)
        if u is None or u["type"] == "annet:ankeadgang":
            continue
        tilfoy(u, f"[#355, beslutning 5] En parts adgang til å anke er en partsposisjon i regellaget, utenfor strukturlaget (som "
                  f"møteplikt). Var {u['kategori']}/{u['type']}.")
        u["kategori"], u["type"] = "annet:partsposisjon", "annet:ankeadgang"
        u.pop("delegerbar", None)
        endret[f"domstolloven {uid} kompetanse/annet:ankekompetanse → annet:partsposisjon/annet:ankeadgang"] += 1
    eid = DL + "§37/ledd-1"
    if not finnes(d, eid, "relasjon", "representerer", REPRESENTERER_SITAT):
        mal = finn(d, "u102", "kan vedkommende regjeringsdepartement paa det offentliges vegne fremsætte indsigelse og erklære anke til Høiesteret")
        assert mal is not None and any(a["id"] == "staten" for a in d["aktorer"])
        r = ny_rad(d, mal, sitat=REPRESENTERER_SITAT, kategori="relasjon", type="representerer", fra="dep", til="staten",
                   objekt=None, avgrensning="saker som ikke hører under norsk domsmyndighet (§ 37)", sikkerhet="hoy",
                   kommentar="[#355, beslutning 5] Den strukturelle delen av § 37: departementet opptrer «paa det offentliges vegne» "
                             "— R representerer departement → staten (Johanns beslutning). Selve ankeadgangen er u102 (regellaget).")
        endret[f"ny rad {r['id']}: domstolloven § 37 relasjon/representerer (departementet → staten)"] += 1


def regel_6(d, kilde, endret):
    if kilde != "domstolloven":
        return
    u = finn(d, "u275", "som er ankeinstans for flere rettskretser")
    if u is None or u.get("til") == "tr":
        return
    tilfoy(u, f"[#355, beslutning 4] Målet er organet: til = tingrettene (var «{u['til']}», rettskretsene). Rettskretsen er "
              f"avgrensning, ikke motpart (L1). Paret lagmannsrett ↔ tingrett regnes ut via tingrett → lagsogn → lagdømme (#345) og "
              f"lagres ikke dobbelt.")
    u["til"] = "tr"
    u["avgrensning"] = "tingrettene i eget lagdømme (rettskretsene som sogner til lagsognene lagdømmet består av)"
    endret["domstolloven u275 til = tingrettene, avgrensning eget lagdømme"] += 1


def regel_7(d, kilde, endret, utenfor):
    for u in d["utsagn"]:
        if (u["kategori"], u["type"]) != ("kompetanse", "foreleggingskompetanse"):
            continue
        ny = FORELEGGING.get((kilde, u["id"], u["sitat"])) if not KI else None
        if ny is None:
            utenfor.append((kilde, u, "foreleggingskompetanse uten avgjort rettsvirkning — ikke konvertert"))
            continue
        u.update(ny)
        u.pop("delegerbar", None)
        u.pop("undertype", None)
        tilfoy(u, FORELEGGING_KOMMENTAR)
        endret[f"kompetanse/foreleggingskompetanse → plikt/konsultasjonsplikt (kan{', negativ' if u['polaritet'] == 'negativ' else ''})"] += 1


def regel_8(d, kilde, tekster, endret):
    if kilde != "domstolloven":
        return
    eid = DL + "§51a/ledd-1"
    assert ANKE_51A_SITAT in tekster.get(eid, ""), "fant ikke § 51 a første ledd siste punktum i nodeteksten"
    if any(u["eid"] == eid and u["type"] == "overprovingskompetanse" and u["polaritet"] == "negativ" for u in d["utsagn"]):
        return
    mal = finn(d, "u117", "forelegge tolkningsspørsmålet for EFTA-domstolen")
    r = ny_rad(d, mal, sitat=ANKE_51A_SITAT, kategori="kompetanse", type="overprovingskompetanse", undertype="anke", fra=None,
               til="domstolene", objekt="beslutning om å forelegge eller ikke forelegge et tolkningsspørsmål for EFTA-domstolen",
               polaritet="negativ", avgrensning=None, betinget=False, kilde_utenfor_korpus=False, sikkerhet="hoy",
               kommentar="[#355, beslutning 4] § 51 a første ledd siste punktum: beslutningen om å forelegge (eller ikke) kan ikke "
                         "ankes — egen negativ anke-rad.")
    endret[f"ny rad {r['id']}: domstolloven § 51 a første ledd siste punktum overprøving/anke, negativ"] += 1


def regel_9(d, kilde, endret):
    regler = {
        "domstolloven": ("u22", "Høyesterett skal ha en direktør", "hr", "hr_dir", "domstolloven § 9"),
        "sameloven": ("u165", "Sametinget skal ha en egen administrasjon.", "a1", "a132", "sameloven § 2-12 første ledd"),
    }
    if kilde not in regler:
        return
    uid, sitat, organ, del_, hjemmel = regler[kilde]
    u = finn(d, uid, sitat)
    if u is None or u["type"] == "del_av":
        return
    assert (u["fra"], u["til"]) == (organ, del_), f"{kilde} {uid}: uventede ender {u['fra']} → {u['til']}"
    tilfoy(u, f"[#355, Johanns beslutning 1 på u22/u165] «X skal ha en Y» for ett navngitt organ er ikke T (T er bare klassenivå), "
              f"men G del_av ({hjemmel}). Var {u['kategori']}/{u['type']} {organ} → {del_}; snudd: fra = delen, til = organet.")
    u["kategori"], u["type"], u["fra"], u["til"] = "relasjon", "del_av", del_, organ
    endret[f"{kilde} {uid} konstituerende/skal_finnes → relasjon/del_av (G)"] += 1
    if kilde == "domstolloven":
        a = next(a for a in d["aktorer"] if a["id"] == "hr_dir")
        if a.get("referent") != "Høyesteretts direktør":
            a["kommentar"] = ((a.get("kommentar") or "") + f" [#355] Stillingen knyttet til nettopp Høyesterett, ikke en felles "
                              f"«direktør»-rolle: G del_av Høyesterett (u22). En person kobles til stillingen med I innehar. "
                              f"Referenten var «{a.get('referent')}».").strip()
            a["referent"] = "Høyesteretts direktør"
            endret["aktør hr_dir: referent «Høyesteretts direktør»"] += 1

# Regel 10 — juristrunden 2026-10-09 (CLAUDE.md §23, agenten jurist-utfordrer, runde 1 på de 29 radene #355 endret eller la til).
# Alle 16 innvendinger er akseptert av modellereren (ingen uenighet igjen, ingen runde 2). Rettingene er tekstlesninger av samme
# ledd/paragraf; slutninger er merket [slutning]. (kilde, id, sitat) → felt som settes.
DL55E1 = ("ved siden av fast stilling, ved en sideordnet domstol, inntil to år (jordskifterett inntil fire år), ved ledighet eller fravær, "
          "inhabilitet, behov for ekstra dommere eller omorganisering (§ 55 e første ledd)")
JURIST_RETTING = {
    ("domstolloven", "u117", "forelegge tolkningsspørsmålet for EFTA-domstolen"): {
        "avgrensning": "når domstolen i en sak må ta stilling til tolkningen av EØS-avtalen med protokoller, vedlegg og rettsakter"},
    ("domstolloven", "u120", "Dommere til Høyesterett, lagmannsrettene, tingrettene og jordskifterettene utnevnes som embetsmenn av Kongen etter Grunnloven § 21."): {
        "delegerbar": False,
        "+kommentar": "[#355, juristrunden] delegerbar = false: sitatet viser til Grunnloven § 21 (Kongen utnevner embetsmenn «etter å ha hørt "
                      "sitt statsråd», jf. § 28) — Kongen i statsråd, som #335-regelen gir false for. [slutning fra Grl. § 21, i korpus]"},
    ("domstolloven", "u124", "foreta enkelte konstitusjoner etter nærmere bestemmelser i §§ 55 e og 55 f"): {
        "til": "dommer", "avgrensning": "enkelte konstitusjoner etter §§ 55 e og 55 f"},
    ("domstolloven", "u133", "Slik konstitusjon gjøres av domstollederen der vigselen skal foretas."): {
        "til": "dommer", "avgrensning": "ved en annen domstol, for å foreta vigsler; også pensjonerte dommere; ikke jordskiftelagdommere og "
                                         "dommere i jordskifterettene (§ 55 e annet ledd)"},
    ("domstolloven", "u134", "Beslutning om konstitusjon som nevnt i første ledd, gjøres av Innstillingsrådet for dommere."): {
        "til": "dommer", "avgrensning": DL55E1},
    ("domstolloven", "u136", "Konstitusjoner med varighet inntil tre måneder kan foretas av domstollederen."): {"til": "dommer"},
    ("domstolloven", "u137", "men av Kongen dersom konstitusjonen har lengre varighet enn ett år eller konstitusjonen gjelder Høyesterett"): {
        "til": "dommer", "avgrensning": "ny dommer (§ 55 f første ledd, inntil to år): lengre varighet enn ett år, eller konstitusjonen gjelder Høyesterett"},
    ("energiloven", "u205", "Departementet eller reguleringsmyndigheten kan trekke tilbake en konsesjon eller annen tillatelse"): {
        "avgrensning": "uriktige eller ufullstendige opplysninger av vesentlig betydning; ikke lenger skikket etter grovt eller gjentatte brudd "
                       "på loven eller pålegg; for konsesjon også oversittelse av frister for fremdriften"},
    ("energiloven", "u206", "Departementet eller reguleringsmyndigheten kan trekke tilbake en konsesjon eller annen tillatelse"): {
        "avgrensning": "uriktige eller ufullstendige opplysninger av vesentlig betydning; ikke lenger skikket etter grovt eller gjentatte brudd "
                       "på loven eller pålegg; for konsesjon også oversittelse av frister for fremdriften"},
    ("energiloven", "u207", "Reguleringsmyndigheten kan, uavhengig av første ledd, helt eller delvis trekke tilbake en omsetningskonsesjon etter § 4-1"): {
        "avgrensning": "grovt eller gjentatte brudd på loven eller på avtaleloven § 38 b, markedsføringsloven og angrerettloven (opplistet i "
                       "punktene); bare når lovbruddet gir rimelig grunn til å tro at fortsatt virksomhet kan skade sluttbrukere eller tilliten "
                       "til strømmarkedet"},
    ("helse-og-omsorgstjenesteloven", "u186", "Departementet kan tilbakekalle godkjenning"): {
        "avgrensning": "når virksomheten som er tildelt godkjenningen, ikke fyller kravene som er satt i forskrift",
        "kommentar": "[slutning] Implisitt også kompetanse til å gi godkjenning."},
    ("spesialisthelsetjenesteloven", "u94", "Dette gjelder ikke et styremedlem som er valgt etter §§ 22 eller 23"): {
        "undertype": "avsetting", "fra": None,
        "+kommentar": "[#355, juristrunden] Unntaket fra avsettingen i forrige punktum («Dette» = avsettingen): undertype avsetting; fra = null "
                      "som u93 («den som har valgt», saksforhold). Var fra = foretaksmøtet."},
    ("spesialisthelsetjenesteloven", "u216", "kontaktpsykolog utnevnes i stedet for kontaktlege"): {
        "undertype": "oppnevning",
        "+kommentar": "[#355, juristrunden] Undertype oppnevning, ikke utnevning: verbet «utnevnes» står, men utnevning er embete etter Grl. "
                      "§ 21 (beslutning 1); kontaktpsykologen er en funksjon institusjonen gir, som kontaktlegen i u215."},
}


def regel_10(d, kilde, endret):
    for u in d["utsagn"]:
        r = JURIST_RETTING.get((kilde, u["id"], u["sitat"]))
        if r is None:
            continue
        for felt, verdi in r.items():
            if felt == "+kommentar":
                tilfoy(u, verdi)
            elif u.get(felt) != verdi:
                u[felt] = verdi
                endret[f"juristrunden: {kilde} {u['id']} {felt}"] += 1
    if kilde == "domstolloven":
        _ny = lambda eid, sitat, **f: (None if finnes(d, eid, f["kategori"], f["type"], sitat) else f)
        hr_mal = finn(d, "u22", "Høyesterett skal ha en direktør")
        # u22-setningen: protokollsekretærer og utredere er samme konstruksjon (beslutning 1), jf. § 61 annet ledd.
        s9 = "så mange protokollsekretærer og utredere som saksmengden krever"
        if hr_mal and not finnes(d, hr_mal["eid"], "relasjon", "del_av", s9):
            r = ny_rad(d, hr_mal, sitat=s9, kategori="relasjon", type="del_av", fra="protsekr", til="hr", avgrensning="så mange som saksmengden krever",
                       kommentar="[#355, juristrunden] Samme «Høyesterett skal ha …»-setning som u22: G del_av (beslutning 1), jf. § 61 annet ledd. "
                                 "Aktøren samler protokollsekretærer og utredere.")
            endret[f"ny rad {r['id']}: domstolloven § 9 del_av (protokollsekretærer og utredere)"] += 1
        # § 37: Høyesterett er ankeinstans for anken departementet erklærer (beslutning 4: K overprøving/anke, motpart domstolene).
        s37 = "erklære anke til Høiesteret for at faa saksbehandlingen og avgjørelsen kjendt ugyldig"
        mal37 = finn(d, "u102", "kan vedkommende regjeringsdepartement paa det offentliges vegne fremsætte indsigelse og erklære anke til Høiesteret")
        if mal37 and not finnes(d, mal37["eid"], "kompetanse", "overprovingskompetanse", s37):
            r = ny_rad(d, mal37, sitat=s37, kategori="kompetanse", type="overprovingskompetanse", undertype="anke", fra="hr", til="domstolene",
                       objekt="saksbehandlingen og avgjørelsen i en sak som ikke hører under norsk domsmyndighet",
                       avgrensning="anke fra departementet etter § 37; ingen tidsfrist eller ankesum", sikkerhet="hoy",
                       kommentar="[#355, juristrunden] Høyesteretts ankekompetanse etter § 37, adskilt fra departementets ankeadgang (u102, regellaget).")
            endret[f"ny rad {r['id']}: domstolloven § 37 overprøving/anke (Høyesterett)"] += 1
        # § 55 f annet ledd første del: Innstillingsrådets konstitusjonskompetanse (u137 er bare Kongens del av punktumet).
        s55f = "Beslutning om konstitusjon som nevnt i første ledd, gjøres av Innstillingsrådet for dommere"
        mal55f = finn(d, "u137", "men av Kongen dersom konstitusjonen har lengre varighet enn ett år eller konstitusjonen gjelder Høyesterett")
        if mal55f and not finnes(d, mal55f["eid"], "kompetanse", "oppnevningskompetanse", s55f):
            r = ny_rad(d, mal55f, sitat=s55f, fra="innst", til="dommer", undertype="konstitusjon", objekt="konstitusjon av ny dommer (§ 55 f)",
                       avgrensning="ny dommer (§ 55 f første ledd): ikke lengre varighet enn ett år, og ikke Høyesterett",
                       kommentar="[#355, juristrunden] Punktumet har to regler (L13): Innstillingsrådet (denne raden) og Kongen (u137).")
            endret[f"ny rad {r['id']}: domstolloven § 55 f annet ledd oppnevning/konstitusjon (Innstillingsrådet)"] += 1
        # § 55 h første ledd: kort nr. 58 for midlertidige dommere (samme tre regler, avgrenset til perioden).
        eid55h = DL + "§55h/ledd-1"
        if not any(a["id"] == "middommer" for a in d["aktorer"]):
            dommer = next(a for a in d["aktorer"] if a["id"] == "dommer")
            d["aktorer"].append({**{k: dommer.get(k) for k in dommer}, "id": "middommer", "tekstform": "midlertidig dommer",
                                 "varianter": ["midlertidige dommere", "midlertidige dommeren"], "eid_eksempler": [eid55h], "antall_forekomster": 3,
                                 "kommentar": "[#355, juristrunden] Midlertidige dommere, «herunder dommerfullmektig og jordskiftedommerfullmektig» (§ 55 h)."})
            endret["aktør middommer (midlertidig dommer)"] += 1
        u122 = next(x for x in d["utsagn"] if x["id"] == "u122")
        periode = "i den perioden konstitusjonen eller tilsettingen gjelder for"
        for sitat, type_, undertype, avgr in [
                ("En midlertidig dommer, herunder dommerfullmektig og jordskiftedommerfullmektig, kan ikke sies opp", "avsettingskompetanse",
                 "oppsigelse", periode),
                ("kan bare avskjediges etter rettergang og dom i den perioden konstitusjonen eller tilsettingen gjelder for", "avsettingskompetanse",
                 "avskjed", "unntatt etter rettergang og dom; " + periode),
                ("forflyttes mot sin vilje", "annet:forflytning", None, "mot sin vilje; " + periode)]:
            if finnes(d, eid55h, "kompetanse", type_, sitat):
                continue
            r = ny_rad(d, u122, eid=eid55h, sitat=sitat, type=type_, til="middommer", avgrensning=avgr, sikkerhet="hoy",
                       kommentar="[#355, juristrunden] § 55 h første ledd: samme tre regler som kort nr. 58 (§ 55 femte ledd), for midlertidige "
                                 "dommere. Siste punktum («fratrer … uten oppsigelse eller avskjed») er at konstitusjonen opphører — ingen kant.")
            if undertype:
                r["undertype"] = undertype
            endret[f"ny rad {r['id']}: domstolloven § 55 h første ledd {type_}" + (f"/{undertype}" if undertype else "")] += 1


def tell(d):
    return collections.Counter((u["kategori"], u["type"]) for u in d["utsagn"])


totalt_for, totalt_etter, alle_endringer, utenfor = collections.Counter(), collections.Counter(), collections.Counter(), []
undertyper_etter = collections.Counter()
for kilde in KILDER:
    sti = os.path.join(MAPPE, kilde + ".json")
    if not os.path.exists(sti):
        continue
    raw = open(sti, encoding="utf-8").read().replace("\r\n", "\n")
    innrykk = len(raw.split("\n")[1]) - len(raw.split("\n")[1].lstrip())
    d = json.loads(raw)
    antall_for = len(d["utsagn"])
    totalt_for.update(tell(d))
    tekster = nodetekster(kilde)
    endret = collections.Counter()
    if not KI:
        regel_2(d, kilde, endret)
        regel_3(d, kilde, endret)
        regel_4(d, kilde, endret)
        regel_5(d, kilde, endret)
        regel_6(d, kilde, endret)
    regel_7(d, kilde, endret, utenfor)
    if not KI:
        regel_8(d, kilde, tekster, endret)
        regel_9(d, kilde, endret)
    regel_1(d, tekster, endret)  # også de nye radene får undertype (de har den alt satt)
    if not KI:
        regel_10(d, kilde, endret)  # juristrunden etter regel 1, så en overstyrt undertype (u216) vinner
    assert len(d["utsagn"]) >= antall_for, "ingen rader skal forsvinne"
    for u in d["utsagn"]:
        if tekster and u["sitat"] not in tekster.get(u["eid"], u["sitat"]):
            raise AssertionError(f"{kilde} {u['id']}: sitatet er ikke en eksakt delstreng av noden")
        if u.get("undertype") and u["kategori"] == "kompetanse":
            undertyper_etter[f"{u['type']}/{u['undertype']}"] += 1
    totalt_etter.update(tell(d))
    alle_endringer.update(endret)
    for u in d["utsagn"]:
        if re.search(r"forelegg", u["type"]) and (u["kategori"], u["type"]) != ("kompetanse", "foreleggingskompetanse"):
            utenfor.append((kilde, u, "saksgang (regellaget), ikke foreleggingskompetanse — står"))
    with open(sti, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(d, ensure_ascii=False, indent=innrykk))
    print(f"{kilde}: {antall_for} → {len(d['utsagn'])} utsagn")

print(f"\nMappe: {os.path.relpath(MAPPE, HER) or '.'}")
print("Endringer:")
for k, v in sorted(alle_endringer.items(), key=lambda kv: (-kv[1], kv[0])):
    print(f"  {v:4}  {k}")
kat = lambda c, k: sum(v for (kk, _), v in c.items() if kk == k)
print(f"\nFør:   {sum(totalt_for.values())} utsagn, kompetanse {kat(totalt_for, 'kompetanse')}, plikt {kat(totalt_for, 'plikt')}, "
      f"relasjon {kat(totalt_for, 'relasjon')}")
print(f"Etter: {sum(totalt_etter.values())} utsagn, kompetanse {kat(totalt_etter, 'kompetanse')}, plikt {kat(totalt_etter, 'plikt')}, "
      f"relasjon {kat(totalt_etter, 'relasjon')}")
print("Undertyper på kompetanse (etter):")
for k, v in sorted(undertyper_etter.items()):
    print(f"  {v:4}  {k}")
print(f"\nStår utenfor / ikke konvertert: {len(utenfor)}")
for kilde, u, grunn in utenfor:
    print(f"  {kilde} {u['id']} ({u['kategori']}/{u['type']}, {grunn}): {u['sitat'][:100]}")
