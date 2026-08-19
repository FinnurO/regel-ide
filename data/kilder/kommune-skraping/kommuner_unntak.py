#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""Skrap manuelle kommunale skjemaoversikter og flett inn i hovedfilene.

Krever at kommuner_hovedscript.py ligger i samme mappe.
Kjør fra Python 3.14 CLI:
    exec(open("kommuner_unntak.py", encoding="utf-8").read())
"""

import csv
import importlib.util
import json
import re
import html as html_lib
from pathlib import Path
from types import SimpleNamespace
from urllib.parse import parse_qsl, urlencode, urljoin, urlparse

from bs4 import BeautifulSoup

BASE_DIR = Path.cwd()
MAIN_SCRIPT = BASE_DIR / "kommuner_hovedscript.py"
if not MAIN_SCRIPT.exists():
    MAIN_SCRIPT = BASE_DIR / "kommuner.py"
if not MAIN_SCRIPT.exists():
    raise FileNotFoundError("Mangler kommuner_hovedscript.py eller kommuner.py")

spec = importlib.util.spec_from_file_location("kommuner_felles", MAIN_SCRIPT)
felles = importlib.util.module_from_spec(spec)
spec.loader.exec_module(felles)

# Filkonstanter i eldre hovedskript kan ha andre navn.
if not hasattr(felles, "OUTPUT_JSON"):
    felles.OUTPUT_JSON = Path("treff.json")
if not hasattr(felles, "OUTPUT_RESULT_CSV"):
    felles.OUTPUT_RESULT_CSV = Path("resultat.csv")
if not hasattr(felles, "OUTPUT_MISSING_CSV"):
    felles.OUTPUT_MISSING_CSV = Path("ingen_treff.csv")
if not hasattr(felles, "OUTPUT_CANDIDATES_CSV"):
    felles.OUTPUT_CANDIDATES_CSV = Path("kandidater.csv")

def canonical_url(url):
    """Normaliser URL uten å miste parametere som identifiserer skjema."""
    parsed = urlparse(str(url or "").strip())
    scheme = (parsed.scheme or "https").lower()
    host = (parsed.hostname or "").lower()
    port = f":{parsed.port}" if parsed.port and parsed.port not in (80, 443) else ""
    path = re.sub(r"/+", "/", parsed.path or "/").rstrip("/") or "/"
    ignored = {"utm_source", "utm_medium", "utm_campaign", "utm_term",
               "utm_content", "fbclid", "gclid"}
    query = urlencode(sorted(
        (key.lower(), value)
        for key, value in parse_qsl(parsed.query, keep_blank_values=True)
        if key.lower() not in ignored
    ))
    return f"{scheme}://{host}{port}{path}" + (f"?{query}" if query else "")


def merge_records(records):
    """Dedupliser tjenester og slå sammen kildehenvisninger."""
    unique = {}
    for record in records:
        if not isinstance(record, dict):
            continue
        url = str(record.get("url") or "").strip()
        name = str(record.get("tjenestenavn") or "").strip()
        if not url or not name:
            continue
        key = canonical_url(url).lower()
        if key not in unique:
            item = dict(record)
            item["kilder"] = []
            unique[key] = item
        target = unique[key]
        seen = {
            (canonical_url(src.get("url")).lower(), str(src.get("metode") or ""))
            for src in target.get("kilder", []) if isinstance(src, dict)
        }
        for src in record.get("kilder", []):
            if not isinstance(src, dict):
                continue
            src_key = (canonical_url(src.get("url")).lower(), str(src.get("metode") or ""))
            if src_key not in seen:
                target["kilder"].append({
                    "url": canonical_url(src.get("url")),
                    "metode": str(src.get("metode") or ""),
                })
                seen.add(src_key)
    return list(unique.values())


def make_record(name, url, category, kommune, orgnr):
    return {
        "tjenestenavn": str(name).strip(),
        "url": str(url).strip(),
        "kategori": str(category or "Skjema").strip(),
        "beskrivelse": "",
        "tilbys_av": [{
            "organisasjon": kommune,
            "organisasjonsnummer": orgnr,
        }],
    }


SOURCES = {
    'ALSTAHAUG KOMMUNE': [
        'https://www.alstahaug.kommune.no/selvbetjening---min-side/skjema-a-a',
    ],
    'ALVER KOMMUNE': [
        'https://alver.kommune.no/artikkel/skjema-i-alver-kommune',
    ],
    'ASKER KOMMUNE': [
        'https://www.asker.kommune.no/soknadsskjemaer/',
    ],
    'AURE KOMMUNE': [
        'https://www.aure.kommune.no/skjema/',
    ],
    'AURLAND KOMMUNE': [
        'https://aurland.kommune.no/artikkel/skjema-i-aurland-kommune',
    ],
    'AURSKOG-HØLAND KOMMUNE': [
        'https://aurskog-holand.kommune.no/innhold/skjemaer-gebyrer-og-priser/',
    ],
    'AVERØY KOMMUNE': [
        'https://www.averoy.kommune.no/skjema/',
    ],
    'BALSFJORD KOMMUNE': [
        'https://www.balsfjord.kommune.no/skjulte-sider/skjema-a-a',
    ],
    'BARDU KOMMUNE': [
        'https://www.bardu.kommune.no/skjema-a-aa.526083.no.html',
    ],
    'BERLEVÅG KOMMUNE': [
        'https://www.berlevag.kommune.no/elektroniske-skjema.4877299-177056.html',
    ],
    'BØ KOMMUNE': [
        'https://www.boe.kommune.no/snarveier/skjema',
    ],
    'DIELDDANUORI SUOHKAN - TJELDSUND KOMMUNE': [
        'https://www.tjeldsund.kommune.no/toppmeny/selvbetjening/skjema-a-a',
    ],
    'DRAMMEN KOMMUNE': [
        'https://www.drammen.kommune.no/skjemaer-og-selvbetjening/',
    ],
    'DYRØY KOMMUNE': [
        'https://www.dyroy.kommune.no/selvbetjening/skjema-a-a',
    ],
    'DØNNA KOMMUNE': [
        'https://www.donna.kommune.no/skjulte-sider/skjema-a-a',
    ],
    'EIGERSUND KOMMUNE': [
        'https://www.eigersund.kommune.no/artikler/2010/q1/2010-02-04-skjema-og-dokumenter',
    ],
    'EVENES KOMMUNE / EVENÁSSI SUOHKAN': [
        'https://www.evenes.kommune.no/skjemaarkiv/',
    ],
    'EVJE OG HORNNES KOMMUNE': [
        'https://www.e-h.kommune.no/alle-skjema/',
    ],
    'FAUSKE KOMMUNE': [
        'https://www.fauske.kommune.no/skjema',
    ],
    'FLATANGER KOMMUNE': [
        'https://www.flatanger.kommune.no/snarveier/skjema-a-a',
    ],
    'GILDESKÅL KOMMUNE': [
        'https://www.gildeskal.kommune.no/selvbetjening/skjema-a-a',
    ],
    'GJEMNES KOMMUNE': [
        'https://www.gjemnes.kommune.no/tjenester/servicekontoret/skjema/',
    ],
    'GRATANGEN KOMMUNE': [
        'https://www.gratangen.kommune.no/selvbetjening/skjema-a-a',
    ],
    'HADSEL KOMMUNE': [
        'https://www.hadsel.kommune.no/skjema/',
    ],
    'HAMARØY KOMMUNE': [
        'https://www.hamaroy.kommune.no/selvbetjening/skjema-a-a',
    ],
    'HEIM KOMMUNE': [
        'https://www.heim.kommune.no/selvbetjening/skjema-a-a',
    ],
    'HEMNES KOMMUNE': [
        'https://www.hemnes.kommune.no/',
    ],
    'HJELMELAND KOMMUNE': [
        'https://www.hjelmeland.kommune.no/soknadssenter/',
    ],
    'HOLE KOMMUNE': [
        'https://www.hole.kommune.no/selvbetjening/skjema-a-a',
    ],
    'HOLTÅLEN KOMMUNE': [
        'https://holtalen.kommune.no/om-kommunen/selvbetjening-skjema/',
    ],
    'IBESTAD KOMMUNE': [
        'https://www.ibestad.kommune.no/skjema-a-a',
    ],
    'AUSTEVOLL KOMMUNE': [
        'https://austevoll.kommune.no/artikkel/skjema-i-austevoll-kommune',
    ],
    'KRISTIANSUND KOMMUNE': [
        'https://www.kristiansund.kommune.no/skjema/',
    ],
    'GAIVUONA SUOHKAN KÅFJORD KOMMUNE KAIVUONON KOMUUNI': [
        'https://www.kafjord.kommune.no/toppmeny/innsyn-og-selvbetjening/skjema-a-a',
    ],
    'FLAKSTAD KOMMUNE': [
        'https://flakstad.kommune.no/skjema/',
    ],
    'BRØNNØY KOMMUNE': [
        'https://www.bronnoy.kommune.no/skjema?pagesize=100',
    ],
    'INDRE FOSEN KOMMUNE': [
        'https://www.indrefosen.kommune.no/tjenester/alle-skjema/',
    ],
    'KARLSØY KOMMUNE': [
        'https://www.karlsoy.kommune.no/skjemaer/skjema-a-a',
    ],
    'DEANU GIELDA / TANA KOMMUNE': [
        'https://www.tana.kommune.no/selvbetjening/skjema-a-a',
    ],
    'ETNEDAL KOMMUNE': [
        'https://www.etnedal.kommune.no/soknadssenter?pagesize=100',
    ],
    'FLÅ KOMMUNE': [
        'https://www.flaa.kommune.no/meny/teknisk-eiendom-og-naring/skjema/',
    ],
    'FYRESDAL KOMMUNE': [
        'https://www.fyresdal.kommune.no/skjema-a-a',
    ],
    'KVINNHERAD KOMMUNE': [
        'https://www.kvinnherad.kommune.no/populaere-lenker/skjema-a-a',
    ],
    'KVÆFJORD KOMMUNE': [
        'https://www.kvafjord.kommune.no/selvbetjening/skjema',
    ],
    'KVÆNANGEN KOMMUNE': [
        'https://www.kvanangen.kommune.no/skjema-a-a',
    ],
    'LAVANGEN KOMMUNE LOABÁGA SUOHKAN': [
        'https://www.lavangen.kommune.no/toppmeny/soknadsskjema',
    ],
    'LEIRFJORD KOMMUNE': [
        'https://www.leirfjord.kommune.no/selvbetjening/skjema-a---a',
    ],
    'LURØY KOMMUNE': [
        'https://www.luroy.kommune.no/om-kommunen/skjema',
    ],
    'LYNGEN KOMMUNE IVGU SUOHKAN YYKEÄN KUNTA': [
        'https://www.lyngen.kommune.no/selvbetjening/skjemaoversikt',
    ],
    'LØTEN KOMMUNE': [
        'https://www.loten.kommune.no/politikk-og-organisasjon/digitale-skjemaer/',
    ],
    'MASFJORDEN KOMMUNE': [
        'https://masfjorden.kommune.no/artikkel/skjema',
    ],
    'MELHUS KOMMUNE': [
        'https://www.melhus.kommune.no/tjenester/skjema-a-a',
    ],
    'MODALEN KOMMUNE': [
        'https://modalen.kommune.no/artikkel/skjemaer-i-kommunen',
    ],
    'MOSKENES KOMMUNE': [
        'https://moskenes.kommune.no/skjema/',
    ],
    'NESNA KOMMUNE': [
        'https://www.nesna.kommune.no/alle-soknadskjema/',
    ],
    'NISSEDAL KOMMUNE': [
        'https://www.nissedal.kommune.no/skjema-a-a',
    ],
    'NORD-AURDAL KOMMUNE': [
        'https://www.nord-aurdal.kommune.no/ofte-brukt-lenker/skjema',
    ],
    'NÆRØYSUND KOMMUNE': [
        'https://www.naroysund.kommune.no/soknadssenter',
    ],
    'ORKLAND KOMMUNE': [
        'https://www.orkland.kommune.no/selvbetjening/skjema-a-a',
    ],
    'OVERHALLA KOMMUNE': [
        'https://www.overhalla.kommune.no/selvbetjening/skjema-a-a',
    ],
    'PORSANGER KOMMUNE': [
        'https://www.porsanger.kommune.no/snarveier/skjema-og-digitale-tjenester/skjema-a-a',
    ],
    'RANDABERG KOMMUNE': [
        'https://randaberg.kommune.no/innhold/skjema/',
    ],
    'RINDAL KOMMUNE': [
        'https://www.rindal.kommune.no/skjema/',
    ],
    'RINGSAKER KOMMUNE': [
        'https://www.ringsaker.kommune.no/selvbetjening/skjema-a-a',
    ],
    'RØDØY KOMMUNE': [
        'https://www.rodoy.kommune.no/skjema/',
    ],
    'SALTDAL KOMMUNE': [
        'https://www.saltdal.kommune.no/selvbetjening/skjema-a-a',
    ],
    'SAUDA KOMMUNE': [
        'https://www.sauda.kommune.no/soknadssenter/',
    ],
    'SELJORD KOMMUNE': [
        'https://www.seljord.kommune.no/snarveier/sok-tenester/soknadsskjema-a-a',
    ],
    'SKAUN KOMMUNE': [
        'https://www.skaun.kommune.no/skjema-fra-a-til-a',
    ],
    'SKJERVØY KOMMUNE': [
        'https://www.skjervoy.kommune.no/selvbetjening/skjema-a-a',
    ],
    'SMØLA KOMMUNE': [
        'http://www.smola.kommune.no/toppmeny/skjema/',
    ],
    'SNÅSA KOMMUNE': [
        'https://www.snasa.kommune.no/tjenester/skjemabank/',
    ],
    'SOGNDAL KOMMUNE': [
        'https://www.sogndal.kommune.no/sjolvbetening/skjema-a-a',
    ],
    'STANGE KOMMUNE': [
        'https://www.stange.kommune.no/skjema/',
    ],
    'STORFJORD KOMMUNE': [
        'https://www.storfjord.kommune.no/skjema-a-aa.177516.no.html',
    ],
    'STRAND KOMMUNE': [
        'https://www.strand.kommune.no/kontakt/liste-over-alle-soknadsskjema?pagesize=100',
    ],
    'SULDAL KOMMUNE': [
        'https://www.suldal.kommune.no/om-kommunen/kontakt-oss/alle-skjema',
    ],
    'SUNNDAL KOMMUNE': [
        'https://www.sunndal.kommune.no/toppmeny/skjema/',
    ],
    'SURNADAL KOMMUNE': [
        'https://www.surnadal.kommune.no/skjema/',
    ],
    'SØR-AURDAL KOMMUNE': [
        'https://www.sor-aurdal.kommune.no/soknadssenter',
    ],
    'SØRFOLD KOMMUNE': [
        'https://www.sorfold.kommune.no/selvbetjening/skjemaoversikt',
    ],
    'SØRREISA KOMMUNE': [
        'https://www.sorreisa.kommune.no/selvbetjening/skjema',
    ],
    'TINGVOLL KOMMUNE': [
        'https://www.tingvoll.kommune.no/tjenester/skjema/',
    ],
    'TOKKE KOMMUNE': [
        'https://www.tokke.kommune.no/snarvegar/skjema-a-a',
    ],
    'TRÆNA KOMMUNE': [
        'https://www.trana.kommune.no/selvbetjening/skjema-a-a',
    ],
    'TYSNES KOMMUNE': [
        'https://tysnes.kommune.no/artikkel/skjema-i-tysnes-kommune-',
    ],
    'TYSVÆR KOMMUNE': [
        'https://www.tysver.kommune.no/organisasjon/skjema',
    ],
    'UNJARGGA GIELDA / NESSEBY KOMMUNE': [
        'https://www.nesseby.kommune.no/artikler/2024/q2/2024-04-11-skjemaer',
    ],
    'UTSIRA KOMMUNE': [
        'https://utsira.kommune.no/skjema-fra-a-a/',
    ],
    'VANG KOMMUNE': [
        'https://www.vang.kommune.no/soknadssenter',
    ],
    'VARDØ KOMMUNE': [
        'https://www.vardo.kommune.no/selvbetjening/skjema-a-a',
    ],
    'VEGA KOMMUNE': [
        'https://www.vega.kommune.no/skjema?pagesize=100',
    ],
    'VESTBY KOMMUNE': [
        'https://www.vestby.kommune.no/selvbetjening/skjema-a-a',
    ],
    'VESTRE SLIDRE KOMMUNE': [
        'https://www.vestre-slidre.kommune.no/soknadssenter',
    ],
    'VESTRE TOTEN KOMMUNE': [
        'https://vestre-toten.kommune.no/oversikt-over-digitale-soknader-og-skjema/',
    ],
    'VEVELSTAD KOMMUNE': [
        'https://www.vevelstad.kommune.no/skjemaer/skjema.1252.aspx',
    ],
    'VINJE KOMMUNE': [
        'https://www.vinje.kommune.no/skjema-a-a',
    ],
    'ÅS KOMMUNE': [
        'https://www.as.kommune.no/sider-utenom-meny/skjema-a-a',
    ],
    'ØSTRE TOTEN KOMMUNE': [
        'https://www.ototen.no/soknadssenter/skjemaoversikt',
    ],
    'ØYSTRE SLIDRE KOMMUNE': [
        'https://www.oystre-slidre.kommune.no/soknadssenter',
    ],
    'NORDRE FOLLO KOMMUNE': [
        'https://skjema.no/nordrefollo',
    ],
    'NORE OG UVDAL KOMMUNE': [
        'https://skjema.no/noreuvdal',
    ],
    'SANDE KOMMUNE': [
        'https://skjema.sande.kommune.no/',
    ],
    'SKJÅK KOMMUNE': [
        'https://skjema.no/skjaak',
    ],
    'ÅL KOMMUNE': [
        'https://skjema.no/aal',
    ],
}

STOPP_UTEN_SKJEMAKILDE = {
    "AUSTRHEIM KOMMUNE": "https://www.austrheim.kommune.no/",
    "BERGEN KOMMUNE": "https://www.bergen.kommune.no/",
    "FEDJE KOMMUNE": "https://www.fedje.kommune.no/",
    "FRØYA KOMMUNE": "https://www.froya.kommune.no/",
    "GULEN KOMMUNE": "https://www.gulen.kommune.no/",
    "HAMAR KOMMUNE": "https://www.hamar.kommune.no/",
    "INDRE ØSTFOLD KOMMUNE": "https://www.io.kommune.no/",
    "KARASJOGA GIELDA / KARASJOK KOMMUNE": "https://www.karasjok.kommune.no/",
    "KRISTIANSAND KOMMUNE": "https://www.kristiansand.kommune.no/",
    "KVINESDAL KOMMUNE": "https://www.kvinesdal.kommune.no/",
    "KVITESEID KOMMUNE": "https://www.kviteseid.kommune.no/",
    "LEKA KOMMUNE": "https://www.leka.kommune.no/",
    "LØRENSKOG KOMMUNE": "https://www.lorenskog.kommune.no/",
    "MÅLSELV KOMMUNE": "https://www.malselv.kommune.no/",
    "NOME KOMMUNE": "https://www.nome.kommune.no/",
    "OSLO KOMMUNE": "https://www.oslo.kommune.no/",
    "OSTERØY KOMMUNE": "https://www.osteroy.kommune.no/",
    "RÆLINGEN KOMMUNE": "https://www.ralingen.kommune.no/",
    "RØST KOMMUNE": "https://www.rost.kommune.no/",
    "SALANGEN KOMMUNE": "https://www.salangen.kommune.no/",
    "SAMNANGER KOMMUNE": "https://www.samnanger.kommune.no/",
    "STEIGEN KOMMUNE": "https://www.steigen.kommune.no/",
    "SVEIO KOMMUNE": "https://www.sveio.kommune.no/",
    "SØMNA KOMMUNE": "https://www.somna.kommune.no/",
    "TRONDHEIM KOMMUNE": "https://www.trondheim.kommune.no/",
    "VAKSDAL KOMMUNE": "https://www.vaksdal.kommune.no/",
    "VÅLER KOMMUNE": "https://www.valer.kommune.no/",
}



STOPP_UTEN_SKJEMAKILDE_ORGNR = {
    "964994307": {
        "kommune": "VÆRØY KOMMUNE",
        "nettsted": "https://varoy.kommune.no/",
    },
    "871034222": {
        "kommune": "VÅLER KOMMUNE",
        "nettsted": "https://www.valer.kommune.no/",
    },
}

# Disse kommunene har samme temabaserte artikkelstruktur. Kildene er nøyaktig
# de brukeren har oppgitt og blir aldri erstattet av automatisk kildeleting.
TEMA_ARTIKKEL_ORGNR = {
    "920290922",  # Alver
    "964968063",  # Aurland
    "941139787",  # Austevoll
    "945627913",  # Masfjorden
    "959412340",  # Tysnes
}

ALFABETISK_ARTIKKEL_ORGNR = {
    "964969302",  # Modalen
}

PAGESIZE_KATALOG_ORGNR = {
    "964978751",  # Strand
}

EXCLUDED_TEXT = (
    "hjem", "forside", "kontakt", "personvern", "informasjonskaps",
    "tilgjengelighet", "min side", "logg inn for å se", "sist endret",
    "skriv ut", "del på", "facebook", "instagram", "linkedin",
    "fant du det du lette etter", "ofte stilte spørsmål", "se adresser",
)
EXCLUDED_PATH = (
    "/personvern", "/cookie", "/kontakt", "/om-kommunen/",
    "/search", "/sok?", "/login", "/minside",
)
STRONG_HOSTS = (
    "skjema.kf.no", "multiform.kf.no", "bekymringsmelding.fiks.ks.no",
    "svarut.ks.no", "foresatt.visma.no", "barnehage.visma.no",
    "skole.visma.com", "husbanken.no", "nav.no", "altinn.no",
    "dibk.no", "kartverket.no", "landbruksdirektoratet.no",
)


def load_orgs():
    # Bruk hovedskriptets egen CSV-leser og feltnavn. Dette unngår at
    # unntaksskriptet er avhengig av et konstantnavn som kan endres.
    return {
        str(item.get("name") or "").strip(): str(item.get("orgnr") or "").strip()
        for item in felles.load_kommuner()
        if str(item.get("name") or "").strip()
    }


def clean_label(value):
    return re.sub(r"\s+", " ", str(value or "")).strip()


def nearest_category_for_profile(link, root, orgnr):
    """Finn kategori for AIM-temasider og alfabetiske sider."""
    excluded = {
        "om kommunen", "innsyn i saker", "framside", "kontakt oss",
        "skjema i kommunen", "skjema i alver kommune",
        "skjema i aurland kommune", "skjema i austevoll kommune",
        "skjema i tysnes kommune", "skjema", "om chat",
    }

    if orgnr in ALFABETISK_ARTIKKEL_ORGNR:
        # Modalen: nærmeste foregående enkeltbokstav er kategori.
        for marker in link.find_all_previous(["h2", "h3", "h4", "strong", "b"]):
            label = clean_label(marker.get_text(" ", strip=True)).upper()
            if len(label) == 1 and label in "ABCDEFGHIJKLMNOPQRSTUVWXYZÆØÅ":
                return label

    if orgnr in TEMA_ARTIKKEL_ORGNR:
        # AIM rendrer temaene som knapper/overskrifter før lenkene. Velg den
        # nærmeste foregående korte etiketten i dokumentrekkefølge.
        candidates = link.find_all_previous(
            ["button", "summary", "h2", "h3", "h4", "h5", "h6"]
        )
        for candidate in candidates:
            label = clean_label(candidate.get_text(" ", strip=True))
            lower = label.lower()
            if not label or len(label) > 100 or lower in excluded:
                continue
            if any(noise in lower for noise in (
                "hopp til", "meny", "søk", "spør meg", "start på nytt",
                "last ned samtalen", "slett samtalen", "personvern",
            )):
                continue
            return label

    heading = link.find_previous(["h2", "h3", "h4", "h5", "h6"])
    if heading:
        label = clean_label(heading.get_text(" ", strip=True))
        if label and label.lower() not in excluded:
            return label
    return "Skjema"


def nearest_heading(link, root, orgnr=""):
    return nearest_category_for_profile(link, root, orgnr)


def is_service_link(link, base_url):
    href = str(link.get("href") or "").strip()
    text = re.sub(r"\s+", " ", link.get_text(" ", strip=True)).strip()
    if not href or not text or len(text) < 3:
        return False
    if href.startswith(("#", "javascript:", "mailto:", "tel:")):
        return False
    if len(text) == 1 and text.upper() in "ABCDEFGHIJKLMNOPQRSTUVWXYZÆØÅ":
        return False
    lower = f"{text} {href}".lower()
    if any(value in lower for value in EXCLUDED_TEXT):
        return False
    absolute = urljoin(base_url, href)
    parsed = urlparse(absolute)
    if any(value in (parsed.path + "?" + parsed.query).lower() for value in EXCLUDED_PATH):
        return False
    if parsed.fragment and canonical_url(absolute) == canonical_url(base_url):
        return False

    path = parsed.path.lower()
    host = (parsed.hostname or "").lower()
    if (
        host in STRONG_HOSTS
        or host.endswith(".visma.no")
        or host == "multiform.kf.no"
        or "/cng/" in path
        or "/dialogue/" in path
        or "/handlers/skjema.ashx" in path
        or "skjemaid=" in parsed.query.lower()
        or "wizardid=" in parsed.query.lower()
    ):
        return True
    if path.endswith((".pdf", ".doc", ".docx", ".xls", ".xlsx", ".odt")):
        return True
    strong = (
        "skjema", "søknad", "soknad", "melding", "registrering",
        "påmelding", "pamelding", "bestilling", "rekvisisjon", "klage",
        "rapport", "tilskudd", "refusjon", "samtykke", "erklæring",
        "tillatelse", "innmelding", "utmelding", "plass", "bevilling",
    )
    return any(word in lower for word in strong)


def response_quality(response):
    """Gi en enkel kvalitetsscore for katalog-HTML."""
    if response is None:
        return -1
    text = str(getattr(response, "text", "") or "")
    lower = text.lower()
    form_markers = (
        "handlers/skjema.ashx", "formsengine", "multiform.kf.no",
        "/cng/", "/dialogue/", "skjemaid=", "wizardid=",
        "skjemaliste", "skjema a-", "søknadssenter", "soknadssenter",
    )
    marker_hits = sum(marker in lower for marker in form_markers)
    href_count = len(re.findall(r"href\s*=", text, flags=re.IGNORECASE))
    return marker_hits * 1000 + href_count * 10 + min(len(text), 9999) // 1000


def fetch_with_playwright(url, timeout):
    """Render katalogen ferdig i Chromium før HTML hentes.

    Temasidene viser navigasjonslenker tidlig, mens skjemalenkene kommer senere.
    Derfor er det ikke nok å vente på en vilkårlig a[href].
    """
    from playwright.sync_api import sync_playwright

    form_selector = ", ".join((
        'a[href*="multiform.kf.no"]',
        'a[href*="/cng/"]',
        'a[href*="/dialogue/"]',
        'a[href*="/handlers/skjema.ashx"]',
        'a[href*="skjemaid="]',
        'a[href*="wizardId="]',
        'a[href$=".pdf"]',
        'a[href$=".doc"]',
        'a[href$=".docx"]',
    ))

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True)
        page = browser.new_page(locale="nb-NO", viewport={"width": 1440, "height": 1200})
        navigation = page.goto(
            url, wait_until="domcontentloaded", timeout=timeout * 1000
        )
        status = navigation.status if navigation is not None else 200

        try:
            page.wait_for_load_state("networkidle", timeout=min(timeout, 20) * 1000)
        except Exception:
            pass

        # Åpne lukkede tema-/accordionseksjoner. Kildesiden endres ikke.
        for selector in (
            'button[aria-expanded="false"]',
            'summary',
            '[role="button"][aria-expanded="false"]',
        ):
            elements = page.locator(selector)
            for index in range(min(elements.count(), 100)):
                try:
                    element = elements.nth(index)
                    text = (element.inner_text(timeout=1000) or "").strip()
                    if text and len(text) <= 180:
                        element.click(timeout=1500, force=True)
                except Exception:
                    pass

        # Vent på en faktisk skjemalenke, ikke bare navigasjonslenkene.
        try:
            page.wait_for_selector(
                form_selector, state="attached", timeout=min(timeout, 25) * 1000
            )
        except Exception:
            # Sider kan også ha relevante eksterne lenker uten kjente mønstre.
            page.wait_for_timeout(3000)

        html = page.content()
        final_url = page.url
        browser.close()

    return SimpleNamespace(
        status_code=status,
        text=html,
        content=html.encode("utf-8"),
        url=final_url,
    )


def request_catalog(url):
    """Hent katalog med requests, curl_cffi og til slutt Chromium.

    HTTP 200 er ikke nok. Flere CMS/WAF-løsninger returnerer et tomt skall med
    status 200 til vanlige HTTP-klienter. Den responsen byttes ut når en
    nettlesertransport gir mer komplett HTML.
    """
    timeout = getattr(felles, "TIMEOUT", 30)
    headers = dict(getattr(felles, "HEADERS", {}))
    headers.update({
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
        "Accept-Language": "nb-NO,nb;q=0.9,no;q=0.8,en-US;q=0.7,en;q=0.6",
        "Cache-Control": "no-cache",
        "Pragma": "no-cache",
        "Sec-Fetch-Dest": "document",
        "Sec-Fetch-Mode": "navigate",
        "Sec-Fetch-Site": "none",
        "Sec-Fetch-User": "?1",
        "Upgrade-Insecure-Requests": "1",
    })
    response = felles.get_session().get(
        url, headers=headers, timeout=timeout, allow_redirects=True,
        verify=getattr(felles, "VERIFY_TLS", True),
    )
    best_response = response
    best_transport = "REQUESTS"
    best_quality = response_quality(response)
    attempts = [f"REQUESTS:q={best_quality}:bytes={len(getattr(response, 'content', b'') or b'')}"]

    # Prøv curl_cffi både ved 403 og ved mistenkelig tom HTTP 200.
    if response.status_code == 403 or best_quality < 1000:
        try:
            from curl_cffi import requests as curl_requests
            candidate = curl_requests.get(
                url, headers=headers, timeout=timeout,
                allow_redirects=True, impersonate="chrome",
            )
            quality = response_quality(candidate)
            attempts.append(
                f"CURL_CFFI:q={quality}:bytes={len(getattr(candidate, 'content', b'') or b'')}"
            )
            if quality > best_quality:
                best_response = candidate
                best_transport = "CURL_CFFI_CHROME"
                best_quality = quality
        except ImportError:
            attempts.append("CURL_CFFI:MANGLER")
        except Exception as exc:
            attempts.append(f"CURL_CFFI:FEIL:{type(exc).__name__}")

    # Dersom HTTP-klientene fortsatt bare gir et tomt skall, render siden.
    if best_quality < 1000:
        try:
            candidate = fetch_with_playwright(url, timeout)
            quality = response_quality(candidate)
            attempts.append(
                f"PLAYWRIGHT:q={quality}:bytes={len(candidate.content)}"
            )
            if quality > best_quality:
                best_response = candidate
                best_transport = "PLAYWRIGHT_CHROMIUM"
                best_quality = quality
        except ImportError:
            attempts.append("PLAYWRIGHT:MANGLER")
        except Exception as exc:
            attempts.append(f"PLAYWRIGHT:FEIL:{type(exc).__name__}")

    return best_response, best_transport, ";".join(attempts)


def scrape_catalog(kommune, orgnr, source_url):
    response, transport, transport_log = request_catalog(source_url)

    aim_profile = (
        orgnr in TEMA_ARTIKKEL_ORGNR
        or orgnr in ALFABETISK_ARTIKKEL_ORGNR
    )
    if aim_profile and transport != "PLAYWRIGHT_CHROMIUM":
        try:
            rendered = fetch_with_playwright(
                source_url, getattr(felles, "TIMEOUT", 30)
            )
            rendered_quality = response_quality(rendered)
            transport_log += (
                f";PLAYWRIGHT_TVUNGET:q={rendered_quality}:"
                f"bytes={len(rendered.content)}"
            )
            response = rendered
            transport = "PLAYWRIGHT_CHROMIUM"
        except Exception as exc:
            transport_log += f";PLAYWRIGHT_TVUNGET:FEIL:{type(exc).__name__}"

    if not felles.usable_response(response):
        note = "Ingen brukbar HTTP-respons."
        if response is not None:
            note = f"HTTP {response.status_code}; transport={transport}; forsøk={transport_log}."
        return [], source_url, note

    host = (urlparse(response.url).hostname or "").lower()
    if host == "skjema.no" and hasattr(felles, "scrape_skjema_no_api"):
        slug = urlparse(response.url).path.strip("/")
        api_records, api_url, api_note = felles.scrape_skjema_no_api(slug, kommune, orgnr)
        if api_records:
            records = []
            for record in api_records:
                item = dict(record)
                item["kilder"] = [{"url": canonical_url(response.url), "metode": "SKJEMA_NO_API"}]
                records.append(item)
            return merge_records(records), canonical_url(source_url), api_note

    # Klassiske ACOS-kataloger kan ha skjemaene som col1/col2/col3-JSON.
    if hasattr(felles, "scrape_embedded_skjemaliste"):
        embedded = felles.scrape_embedded_skjemaliste(
            response.text, response.url, kommune, orgnr
        )
        if embedded:
            records = []
            for record in embedded:
                item = dict(record)
                item["kilder"] = [{"url": canonical_url(response.url), "metode": "HTML_INNEBYGD_JSON"}]
                records.append(item)
            return merge_records(records), canonical_url(source_url), f"HTTP {response.status_code}; transport={transport}; forsøk={transport_log}; innebygd JSON."

    soup = BeautifulSoup(response.text, "html.parser")
    # Flere CMS-maler legger katalogen utenfor det første main-elementet.
    # Søk i hele dokumentet og filtrer navigasjon etterpå.
    root = soup
    records = []
    source = canonical_url(source_url)
    links = root.find_all("a", href=True)

    for link in links:
        href = str(link.get("href") or "").strip()
        text = re.sub(r"\s+", " ", link.get_text(" ", strip=True)).strip()
        if not href or not text:
            continue
        absolute = urljoin(response.url, href)
        parsed_absolute = urlparse(absolute)
        same_page = (
            canonical_url(absolute).split("#", 1)[0]
            == canonical_url(response.url).split("#", 1)[0]
        )
        lower_text = text.lower()
        if (
            href.startswith(("#", "javascript:", "mailto:", "tel:"))
            or same_page
            or lower_text in {"om chat", "start på nytt", "last ned samtalen", "slett samtalen"}
        ):
            continue
        if is_service_link(link, response.url):
            record = make_record(
                name=text,
                url=absolute,
                category=nearest_heading(link, root, orgnr),
                kommune=kommune,
                orgnr=orgnr,
            )
            record["kilder"] = [{"url": source, "metode": "UNNTAK_HTML_KATALOG"}]
            records.append(record)


    # Reserve for CMS-er som bygger lenker fra HTML-fragmenter eller JSON.
    # Brukes bare når vanlig DOM-parsing ga null tjenester.
    if not records:
        raw_html = html_lib.unescape(response.text).replace("\\/", "/")
        patterns = [
            r"href\s*=\s*[\"']([^\"']+)[\"']",
            r"[\"'](?:url|href)[\"']\s*:\s*[\"']([^\"']+)[\"']",
        ]
        raw_urls = []
        for pattern in patterns:
            raw_urls.extend(re.findall(pattern, raw_html, flags=re.IGNORECASE))

        for raw_url in raw_urls:
            absolute = urljoin(response.url, raw_url)
            parsed = urlparse(absolute)
            lower = absolute.lower()
            path = parsed.path.lower()
            is_form = (
                "/handlers/skjema.ashx" in path
                or "/formsengine/" in path
                or "/cng/" in path
                or "/dialogue/" in path
                or "skjemaid=" in lower
                or "wizardid=" in lower
                or path.endswith((".pdf", ".doc", ".docx", ".xls", ".xlsx", ".odt"))
            )
            if not is_form:
                continue
            anchor = soup.find("a", href=raw_url)
            text = re.sub(r"\s+", " ", anchor.get_text(" ", strip=True)).strip() if anchor else ""
            if not text:
                text = parsed.path.rstrip("/").split("/")[-1] or "Skjema"
                text = re.sub(r"[_-]+", " ", text).strip()
            record = make_record(
                name=text,
                url=absolute,
                category=nearest_heading(anchor, root, orgnr) if anchor else "Skjema",
                kommune=kommune,
                orgnr=orgnr,
            )
            record["kilder"] = [{"url": source, "metode": "UNNTAK_HTML_RAAURL"}]
            records.append(record)

    records = merge_records(records)
    return records, source, (
        f"HTTP {response.status_code}; transport={transport}; "
        f"forsøk={transport_log}; DOM-lenker={len(links)}; "
        f"skjemalenker={sum(1 for link in links if is_service_link(link, response.url))}; "
        f"bytes={len(getattr(response, 'content', b'') or b'')}; "
        f"tjenester={len(records)}."
    )


def load_json(path, default):
    if not path.exists():
        return default
    try:
        with path.open("r", encoding="utf-8-sig") as file:
            return json.load(file)
    except (OSError, ValueError, json.JSONDecodeError):
        return default


def load_csv(path):
    if not path.exists():
        return []
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as file:
            return list(csv.DictReader(file))
    except OSError:
        return []


def build_missing_rows(kommuner, treff_by_orgnr, result_rows):
    if hasattr(felles, "build_missing_rows"):
        return felles.build_missing_rows(kommuner, treff_by_orgnr, result_rows)
    statuses = {}
    for row in result_rows:
        kommune = str(row.get("kommune") or "").strip()
        status = str(row.get("status") or "").strip()
        if kommune:
            statuses.setdefault(kommune, set()).add(status)
    missing = []
    for item in kommuner:
        kommune = item["name"]
        if item["orgnr"] in treff_by_orgnr:
            continue
        missing.append({
            "kommune": kommune,
            "organisasjonsnummer": item["orgnr"],
            "slug": felles.normalize_name(kommune),
        })
    return missing


def write_outputs_extended(treff, result_rows, missing_rows):
    if hasattr(felles, "migrate_count_fields_in_treff"):
        treff = felles.migrate_count_fields_in_treff(treff)
    if hasattr(felles, "migrate_count_fields_in_rows"):
        result_rows = felles.migrate_count_fields_in_rows(result_rows)
    if hasattr(felles, "dedupe_result_rows"):
        result_rows = felles.dedupe_result_rows(result_rows)

    slug_map = {
        str(item.get("kommune") or "").strip(): str(item.get("slug") or "").strip()
        for item in treff if isinstance(item, dict)
    }
    if hasattr(felles, "enrich_result_rows"):
        result_rows = felles.enrich_result_rows(result_rows, slug_map)

    with felles.OUTPUT_JSON.open("w", encoding="utf-8") as file:
        json.dump(treff, file, ensure_ascii=False, indent=2)
        file.write("\n")

    result_fields = [
        "organisasjonsnummer", "kommune", "status", "nettsted", "skjemaside", "kilde_url",
        "antall_unikt_bidrag", "antall_i_kilden", "antall_overlapp",
        "antall_kandidater", "metode", "merknad",
    ]
    with felles.OUTPUT_RESULT_CSV.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=result_fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(result_rows)

    missing_fields = ["kommune", "organisasjonsnummer", "slug"]
    with felles.OUTPUT_MISSING_CSV.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=missing_fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(missing_rows)



def run():
    # Kandidater brukes ikke. En gammel kandidater.csv fra tidligere versjoner
    # fjernes for å unngå at den oppfattes som et aktivt resultat.
    candidate_path = Path("kandidater.csv")
    if candidate_path.exists():
        candidate_path.unlink()

    orgs = load_orgs()
    treff = load_json(felles.OUTPUT_JSON, [])
    treff_by_orgnr = {
        str(item.get("organisasjonsnummer") or "").strip(): item
        for item in treff
        if isinstance(item, dict) and str(item.get("organisasjonsnummer") or "").strip()
    }
    result_rows = load_csv(felles.OUTPUT_RESULT_CSV)

    # FUNNET er ferdig og skal ikke skrapes på nytt. TOM_KILDE og FEIL
    # prøves på nytt ved hver kjøring. Hvis resultat.csv er slettet, kjøres
    # alle eksplisitte unntakskilder på nytt.
    # Migrer gamle resultatrader uten org.nr. bare når kommunenavnet er unikt.
    orgs_by_name = {}
    for name, orgnr in orgs.items():
        orgs_by_name.setdefault(name, []).append(orgnr)
    migrated_rows = []
    for row in result_rows:
        orgnr = str(row.get("organisasjonsnummer") or "").strip()
        if not orgnr:
            matches = orgs_by_name.get(str(row.get("kommune") or "").strip(), [])
            if len(matches) == 1:
                row["organisasjonsnummer"] = matches[0]
            elif len(matches) > 1:
                continue
        migrated_rows.append(row)
    result_rows = migrated_rows

    found_orgnr = {
        str(row.get("organisasjonsnummer") or "").strip()
        for row in result_rows
        if str(row.get("status") or "").strip() == "FUNNET"
    }
    pending_sources = {
        kommune: urls
        for kommune, urls in SOURCES.items()
        if orgs.get(kommune, "") not in found_orgnr
    }
    skipped_found = set(SOURCES) - set(pending_sources)

    retry_orgnr = {orgs.get(name, "") for name in pending_sources}
    stop_orgnr = {
        orgs.get(name, "") for name in STOPP_UTEN_SKJEMAKILDE
    } | set(STOPP_UTEN_SKJEMAKILDE_ORGNR)
    replaced_orgnr = retry_orgnr | stop_orgnr
    result_rows = [
        row for row in result_rows
        if str(row.get("organisasjonsnummer") or "").strip() not in replaced_orgnr
    ]

    # Kommuner brukeren har avsluttet kildesøket for. Eventuelle gamle treff
    # fjernes og resultatet viser bare kommunens nettsted og tom skjemaside.
    for kommune, nettsted in STOPP_UTEN_SKJEMAKILDE.items():
        treff_by_orgnr.pop(orgs.get(kommune, ""), None)
        result_rows.append({
            "organisasjonsnummer": orgs.get(kommune, ""),
            "kommune": kommune,
            "status": "INGEN_SKJEMAKILDE",
            "nettsted": nettsted,
            "skjemaside": "",
            "kilde_url": "",
            "antall_tjenester": 0,
            "antall_i_kilden": 0,
            "antall_overlapp": 0,
            "antall_kandidater": 0,
            "metode": "MANUELT_STOPP",
            "merknad": "Kildesøk avsluttet etter brukerens instruksjon.",
        })


    for orgnr, info in STOPP_UTEN_SKJEMAKILDE_ORGNR.items():
        treff_by_orgnr.pop(orgnr, None)
        result_rows.append({
            "organisasjonsnummer": orgnr,
            "kommune": info["kommune"],
            "status": "INGEN_SKJEMAKILDE",
            "nettsted": info["nettsted"],
            "skjemaside": "",
            "kilde_url": "",
            "antall_unikt_bidrag": 0,
            "antall_i_kilden": 0,
            "antall_overlapp": 0,
            "antall_kandidater": 0,
            "metode": "MANUELT_STOPP",
            "merknad": "Kildesøk avsluttet etter brukerens instruksjon.",
        })

    print(
        f"Unntak: skal behandle {len(pending_sources)} kommuner; "
        f"hopper over {len(skipped_found)} med FUNNET."
    )

    for kommune, urls in pending_sources.items():
        orgnr = orgs.get(kommune, "")
        if not orgnr:
            print(f"ADVARSEL: Fant ikke organisasjonsnummer for {kommune}")
        new_records = []
        source_summaries = []

        for source_url in urls:
            records, final_url, note = scrape_catalog(kommune, orgnr, source_url)
            new_records.extend(records)
            source_summaries.append({
                "url": final_url,
                "metode": "UNNTAK_HTML_KATALOG",
                "antall_unikt_bidrag": len(records),
                "antall_i_kilden": len(records),
                "antall_overlapp": 0,
                "antall_kandidater": 0,
            })
            result_rows.append({
                "organisasjonsnummer": orgnr,
                "kommune": kommune,
                "status": "FUNNET" if records else "TOM_KILDE",
                "nettsted": f"{urlparse(final_url).scheme}://{urlparse(final_url).netloc}/" if urlparse(final_url).netloc else "",
                "skjemaside": final_url,
                "kilde_url": final_url,
                "antall_unikt_bidrag": len(records),
                "antall_i_kilden": len(records),
                "antall_overlapp": 0,
                "antall_kandidater": 0,
                "metode": "UNNTAK_HTML_KATALOG",
                "merknad": note,
            })

        existing = treff_by_orgnr.get(orgnr, {})
        combined_records = merge_records((existing.get("records") or []) + new_records)
        old_sources = existing.get("sources") or []
        source_keys = {canonical_url(s.get("url")).lower() for s in source_summaries}
        combined_sources = [s for s in old_sources if canonical_url(s.get("url")).lower() not in source_keys]
        combined_sources.extend(source_summaries)

        if combined_records:
            treff_by_orgnr[orgnr] = {
                "kommune": kommune,
                "organisasjonsnummer": orgnr or existing.get("organisasjonsnummer", ""),
                "slug": felles.normalize_name(kommune),
                "sources": combined_sources,
                "antall_tjenester": len(combined_records),
                "records": combined_records,
            }
        print(f"{kommune}: {len(new_records)} fra unntakskatalog, {len(combined_records)} samlet")

    kommuner = felles.load_kommuner()
    missing = build_missing_rows(kommuner, treff_by_orgnr, result_rows)
    write_outputs_extended(
        list(treff_by_orgnr.values()),
        result_rows,
        missing,
    )
    print("Ferdig. Oppdaterte treff.json, resultat.csv og ingen_treff.csv.")


UNNTAK_RESULTAT = run()
