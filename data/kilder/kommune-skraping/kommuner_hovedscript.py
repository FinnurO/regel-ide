#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""Kartlegg og skrap kommunale skjemakataloger.

Input:
    Kommuner.csv

Utdata:
    treff.json       Alle tjenester som ble funnet
    resultat.csv     En rad per kommune og kilde/kandidat
    ingen_treff.csv  Kommuner uten tjenester og uten manuelt unntak

Avhengigheter:
    pip install requests beautifulsoup4
"""

import csv
import json
import re
import sys
import threading
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from urllib.parse import urljoin, urlparse

import requests
from bs4 import BeautifulSoup


INPUT_FILE = Path("Kommuner.csv")
OUTPUT_JSON = Path("treff.json")
OUTPUT_RESULT_CSV = Path("resultat.csv")
OUTPUT_MISSING_CSV = Path("ingen_treff.csv")
OUTPUT_CANDIDATES_CSV = Path("kandidater.csv")

TIMEOUT = 30
MAX_WORKERS = 20
RESUME_EXISTING = True
VERIFY_TLS = True

HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/126.0.0.0 Safari/537.36"
    ),
    "Accept": "text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8",
    "Accept-Language": "nb-NO,nb;q=0.9,no;q=0.8,en;q=0.5",
}

API_HEADERS = {
    **HEADERS,
    "Accept": "application/json, text/plain, */*",
    "Content-Type": "application/json",
    "x-csrf": "1",
}

# Disse kildene skal ikke skrapes av dette skriptet. De skrives som UNNTAK,
# med en egen rad per kommune/kandidat i resultat.csv.
MANUAL_SOURCES = {}

# Hoveddomener som ikke følger <normalisert-navn>.kommune.no.
MAIN_DOMAIN_OVERRIDES = {
    "bo": ["https://www.boe.kommune.no/"],
    "evje-og-hornnes": ["https://www.e-h.kommune.no/"],
    "fla": ["https://www.flaa.kommune.no/"],
    "indre-fosen": ["https://www.indrefosen.kommune.no/"],
    "indre-ostfold": ["https://www.io.kommune.no/"],
    "kvaefjord": ["https://www.kvafjord.kommune.no/"],
    "kvaenangen": ["https://www.kvanangen.kommune.no/"],
    "naeroysund": ["https://www.naroysund.kommune.no/"],
    "raelingen": ["https://www.ralingen.kommune.no/"],
    "skjak": ["https://www.skjaak.kommune.no/"],
    "traena": ["https://www.trana.kommune.no/"],
    "tysvaer": ["https://www.tysver.kommune.no/"],
    "vaeroy": ["https://varoy.kommune.no/"],
    "al": ["https://www.aal.kommune.no/"],
}

# Norske kortnavn for organisasjonsnavn som også inneholder samiske/finske navn.
SLUG_OVERRIDES = {
    "DEANU GIELDA / TANA KOMMUNE": "tana",
    "DIELDDANUORI SUOHKAN - TJELDSUND KOMMUNE": "tjeldsund",
    "EVENES KOMMUNE / EVENASSI SUOHKAN": "evenes",
    "GAIVUONA SUOHKAN KAFJORD KOMMUNE KAIVUONON KOMUUNI": "kafjord",
    "GUOVDAGEAINNU SUOHKAN / KAUTOKEINO KOMMUNE": "kautokeino",
    "HARSTAD KOMMUNE / HARSTTAID SUOHKAN": "harstad",
    "KARASJOGA GIELDA / KARASJOK KOMMUNE": "karasjok",
    "LAVANGEN KOMMUNE LOABAGA SUOHKAN": "lavangen",
    "LYNGEN KOMMUNE IVGU SUOHKAN YYKEAN KUNTA": "lyngen",
    "SORTLAND KOMMUNE / SUORTTA SOUHKAN": "sortland",
    "UNJARGGA GIELDA / NESSEBY KOMMUNE": "nesseby",
}

_thread_local = threading.local()


def get_session():
    session = getattr(_thread_local, "session", None)
    if session is None:
        session = requests.Session()
        session.headers.update(HEADERS)
        _thread_local.session = session
    return session


def ascii_name(value):
    value = str(value or "").upper()
    replacements = {
        "Á": "A", "À": "A", "Â": "A", "Ä": "A",
        "É": "E", "È": "E", "Ê": "E", "Ë": "E",
        "Í": "I", "Ì": "I", "Î": "I", "Ï": "I",
        "Ó": "O", "Ò": "O", "Ô": "O", "Ö": "O",
        "Ú": "U", "Ù": "U", "Û": "U", "Ü": "U",
        "Æ": "AE", "Ø": "O", "Å": "A",
        "Č": "C", "Š": "S", "Ž": "Z", "Đ": "D",
    }
    for old, new in replacements.items():
        value = value.replace(old, new)
    return value


def normalize_name(name):
    original = str(name or "").strip()
    override_key = ascii_name(original)
    override_key = re.sub(r"\s+", " ", override_key).strip()
    if override_key in SLUG_OVERRIDES:
        return SLUG_OVERRIDES[override_key]

    # Finn delen som faktisk ender på KOMMUNE eller HERAD.
    parts = [part.strip() for part in original.split("/")]
    selected = ""
    for part in parts:
        if re.search(r"\b(?:KOMMUNE|HERAD)\b", part, flags=re.I):
            selected = part
            break
    if not selected:
        selected = original

    selected = re.sub(r"\s+(?:KOMMUNE|HERAD)\b.*$", "", selected, flags=re.I)
    if " - " in selected:
        selected = selected.split(" - ")[-1].strip()

    selected = selected.lower()
    selected = (
        selected.replace("æ", "ae")
        .replace("ø", "o")
        .replace("å", "a")
    )
    selected = re.sub(r"[^a-z0-9-]+", "-", selected)
    selected = re.sub(r"-+", "-", selected)
    return selected.strip("-")


def clean_header(value):
    return str(value or "").replace("\ufeff", "").strip()


def read_csv_with_fallback(path):
    """Les CSV robust med både semikolon og komma som skilletegn."""
    import io

    raw = path.read_bytes()
    attempts = (
        ("utf-8-sig", "strict"),
        ("utf-8", "strict"),
        ("cp1252", "strict"),
        ("latin-1", "strict"),
    )
    errors = []

    for encoding, error_mode in attempts:
        try:
            text = raw.decode(encoding, errors=error_mode)
        except UnicodeDecodeError as exc:
            errors.append(f"{encoding}: {exc}")
            continue

        if encoding == "latin-1":
            text = "".join(
                char for char in text
                if not (0x80 <= ord(char) <= 0x9F)
            )

        # Fjern tomme linjer foran overskriften. Filen kan komme eksportert
        # med en innledende blank linje.
        text = text.lstrip("\r\n\ufeff")

        # Kommuner.csv forekommer både som semikolonseparert og kommaseparert.
        first_line = text.splitlines()[0] if text.splitlines() else ""
        delimiters = []
        if ";" in first_line:
            delimiters.append(";")
        if "," in first_line:
            delimiters.append(",")
        for delimiter in (";", ","):
            if delimiter not in delimiters:
                delimiters.append(delimiter)

        for delimiter in delimiters:
            stream = io.StringIO(text, newline="")
            reader = csv.DictReader(stream, delimiter=delimiter)
            raw_fields = reader.fieldnames or []
            normalized_fields = [clean_header(value) for value in raw_fields]
            if "Name" not in normalized_fields:
                errors.append(
                    f"{encoding}/{repr(delimiter)}: fant ikke kolonnen Name"
                )
                continue

            # DictReader bruker de originale feltnavnene. Bytt dem til rensede
            # navn slik at BOM og mellomrom ikke følger med inn i radene.
            reader.fieldnames = normalized_fields
            return stream, reader, f"{encoding}, skilletegn {repr(delimiter)}"

    # Siste reserve med synlig erstatning av ugyldige enkeltbytes.
    text = raw.decode("utf-8-sig", errors="replace").lstrip("\r\n\ufeff")
    for delimiter in (";", ","):
        stream = io.StringIO(text, newline="")
        reader = csv.DictReader(stream, delimiter=delimiter)
        normalized_fields = [clean_header(value) for value in (reader.fieldnames or [])]
        if "Name" in normalized_fields:
            reader.fieldnames = normalized_fields
            print(
                "ADVARSEL: Kommuner.csv inneholdt ugyldige bytes. "
                "Ugyldige enkelttegn er erstattet med U+FFFD."
            )
            return stream, reader, (
                f"utf-8-sig med erstatning, skilletegn {repr(delimiter)}"
            )

    raise RuntimeError(
        f"Kunne ikke lese {path}. Forsøk: " + " | ".join(errors)
    )


def load_kommuner():
    if not INPUT_FILE.exists():
        raise FileNotFoundError(
            f"Fant ikke {INPUT_FILE.resolve()}. Legg Kommuner.csv i samme mappe som skriptet."
        )

    kommuner = []
    handle, reader, encoding = read_csv_with_fallback(INPUT_FILE)
    try:
        for raw_row in reader:
            row = {
                clean_header(key): value.strip() if isinstance(value, str) else value
                for key, value in raw_row.items()
                if key is not None
            }
            navn = str(row.get("Name") or "").strip()
            orgnr = str(row.get("OrganizationId") or "").strip()
            if navn:
                kommuner.append({"name": navn, "orgnr": orgnr})
    finally:
        handle.close()

    print(f"Leste {len(kommuner)} kommuner fra {INPUT_FILE} ({encoding}).")
    return kommuner


def request_url(url, headers=None):
    try:
        return get_session().get(
            url,
            headers=headers,
            timeout=TIMEOUT,
            allow_redirects=True,
            verify=VERIFY_TLS,
        )
    except requests.RequestException:
        return None


def usable_response(response):
    if response is None or response.status_code >= 400:
        return False
    final_host = (urlparse(response.url).hostname or "").lower()
    # skjema.no kan returnere en generell side for ukjente stier. Den blir derfor
    # ikke godkjent som kilde før skrapingen faktisk finner tjenester.
    return bool(final_host)


def candidate_sources(slug):
    return [
        f"https://skjema.{slug}.kommune.no",
        f"https://dialog.{slug}.kommune.no",
        f"https://skjema.no/{slug}",
    ]


def make_record(name, url, category, description, kommune_navn, orgnr):
    return {
        "tjenestenavn": str(name or "").strip(),
        "url": str(url or "").strip(),
        "kategori": str(category or "").strip(),
        "beskrivelse": str(description or "").strip(),
        "tilbys_av": [
            {
                "organisasjon": kommune_navn,
                "organisasjonsnummer": orgnr,
            }
        ],
    }


def normalize_tags(tags):
    if not tags:
        return ""
    if isinstance(tags, str):
        return tags.strip()
    result = []
    for tag in tags:
        if isinstance(tag, str):
            text = tag.strip()
        elif isinstance(tag, dict):
            text = str(tag.get("name") or tag.get("title") or tag.get("value") or "").strip()
        else:
            text = str(tag).strip()
        if text:
            result.append(text)
    return ", ".join(result)


def scrape_acos(base_url, kommune_navn, orgnr):
    api_urls = [
        f"{base_url.rstrip('/')}/dialogue/api/dialogues?language=nb-NO&filterLanguage=false",
        f"{base_url.rstrip('/')}/api/dialogues?language=nb-NO&filterLanguage=false",
    ]

    for api_url in api_urls:
        response = request_url(api_url, headers=API_HEADERS)
        if response is None or response.status_code >= 400:
            continue
        try:
            data = response.json()
        except (ValueError, json.JSONDecodeError):
            continue

        if isinstance(data, dict):
            dialogues = data.get("dialogues") or data.get("items") or data.get("results") or []
        elif isinstance(data, list):
            dialogues = data
        else:
            dialogues = []

        if not isinstance(dialogues, list) or not dialogues:
            continue

        records = []
        for item in dialogues:
            if not isinstance(item, dict):
                continue
            dialog_id = str(item.get("id") or item.get("dialogueId") or "").strip()
            name = str(item.get("name") or item.get("title") or "").strip()
            if not dialog_id or not name:
                continue
            public_url = urljoin(
                f"{base_url.rstrip('/')}/dialogue/",
                dialog_id,
            )
            records.append(
                make_record(
                    name=name,
                    url=public_url,
                    category=normalize_tags(item.get("tags") or item.get("categories")),
                    description=item.get("description") or "",
                    kommune_navn=kommune_navn,
                    orgnr=orgnr,
                )
            )

        if records:
            return dedupe_records(records), api_url

    return [], ""


def scrape_skjema_no_api(slug, kommune_navn, orgnr):
    """Hent komplett katalog fra api.skjema.no.

    Returnerer standardiserte tjenester med kun feltene som brukes av resten
    av skriptet: tjenestenavn, url, kategori, beskrivelse og tilbys_av.
    """
    api_url = f"https://api.skjema.no/{slug}/categories"
    headers = {
        **API_HEADERS,
        "Origin": "https://skjema.no",
        "Referer": f"https://skjema.no/{slug}",
    }

    response = request_url(
        f"{api_url}?culture=nb&fill=true",
        headers=headers,
    )
    if response is None or response.status_code >= 400:
        return [], api_url, (
            "Ingen respons fra API-et."
            if response is None
            else f"API-et svarte HTTP {response.status_code}."
        )

    try:
        payload = response.json()
    except (ValueError, json.JSONDecodeError):
        return [], response.url, "API-et returnerte ikke gyldig JSON."

    if isinstance(payload, list):
        categories = payload
    elif isinstance(payload, dict):
        categories = payload.get("categories")
        if not isinstance(categories, list):
            nested = payload.get("data")
            categories = nested.get("categories") if isinstance(nested, dict) else []
    else:
        categories = []

    if not isinstance(categories, list):
        categories = []

    records = []
    for category in categories:
        if not isinstance(category, dict):
            continue

        category_name = str(
            category.get("caption")
            or category.get("name")
            or category.get("title")
            or category.get("code")
            or ""
        ).strip()

        templates = category.get("templates") or category.get("forms") or []
        if not isinstance(templates, list):
            continue

        for template in templates:
            if not isinstance(template, dict):
                continue

            name = str(
                template.get("name")
                or template.get("caption")
                or template.get("title")
                or ""
            ).strip()
            form_id = str(
                template.get("formId")
                or template.get("formID")
                or template.get("formid")
                or template.get("code")
                or ""
            ).strip()
            template_id = str(template.get("id") or "").strip()
            public_id = form_id or template_id

            if not name or not public_id:
                continue

            records.append(
                make_record(
                    name=name,
                    url=f"https://skjema.no/{slug}/{public_id}",
                    category=category_name,
                    description="",
                    kommune_navn=kommune_navn,
                    orgnr=orgnr,
                )
            )

    records = dedupe_records(records)
    note = (
        f"API: {response.url}"
        if records
        else "API-et svarte, men inneholdt ingen tjenester."
    )
    return records, response.url, note


def nearest_category(link):
    heading = link.find_previous(["h1", "h2", "h3", "h4", "h5", "h6"])
    if heading is None:
        return ""
    return heading.get_text(" ", strip=True)


def is_in_navigation(link):
    return link.find_parent(["nav", "header", "footer"]) is not None


def excluded_link(href, text):
    value = f"{href} {text}".lower().strip()
    if not value:
        return True
    prefixes = ("#", "javascript:", "mailto:", "tel:")
    if href.lower().startswith(prefixes):
        return True
    excluded = (
        "/login", "loginvalg", "logg inn", "/personvern", "personvern",
        "cookie", "informasjonskaps", "tilgjengelighetserkl",
        "uustatus.no", "glemt passord", "mine saker", "min side",
        "facebook.com", "instagram.com", "linkedin.com", "youtube.com",
    )
    return any(item in value for item in excluded)


def classify_html_link(link, base_url):
    raw_href = str(link.get("href") or "").strip()
    text = link.get_text(" ", strip=True)
    if not raw_href or not text or excluded_link(raw_href, text):
        return "AVVIST", "teknisk eller navigasjon"
    if is_in_navigation(link):
        return "AVVIST", "header/nav/footer"

    absolute = urljoin(base_url, raw_href)
    current = urlparse(base_url)
    target = urlparse(absolute)
    host = (target.hostname or "").lower()
    current_host = (current.hostname or "").lower()
    path = target.path.rstrip("/")
    current_path = current.path.rstrip("/")

    # skjema.no bruker normalt /kommuneslug/tjeneste-id. Alt som ligger under
    # kommuneinngangen beholdes som tjeneste, uten krav om nøkkelord.
    if current_host == "skjema.no" or current_host.endswith(".skjema.no"):
        if host == current_host and current_path and path.startswith(current_path + "/"):
            return "TJENESTE", "underliggende skjema.no-lenke"
        if host != current_host:
            return "KANDIDAT", "ekstern lenke fra skjema.no-katalog"
        return "AVVIST", "ikke under kommuneinngangen"

    # ACOS/andre dedikerte skjema- og dialogdomener er allerede kataloger.
    # Alle innholdslenker beholdes, bortsett fra klare tekniske lenker ovenfor.
    dedicated = current_host.startswith("skjema.") or current_host.startswith("dialog.")
    if dedicated:
        if absolute.rstrip("/") == base_url.rstrip("/"):
            return "AVVIST", "lenke til katalogens forside"
        return "TJENESTE", "innholdslenke på dedikert katalogdomene"

    # På andre HTML-sider brukes brede signaler, men usikre lenker forkastes
    # ikke. De legges i kandidater.csv for etterkontroll.
    value = f"{absolute} {text}".lower()
    strong = (
        "/skjema/", "/dialogue/", "/forms/", "/form/", "/soknad/",
        "/søknad/", "skjema", "søknad", "soknad", "selvbetjening"
    )
    if any(item in value for item in strong):
        return "TJENESTE", "skjemasignal i URL eller lenketekst"
    return "KANDIDAT", "innholdslenke uten sikkert skjemasignal"


def canonical_url(url):
    parsed = urlparse(str(url or "").strip())
    scheme = (parsed.scheme or "https").lower()
    host = (parsed.hostname or "").lower()
    port = f":{parsed.port}" if parsed.port and parsed.port not in (80, 443) else ""
    path = re.sub(r"/+", "/", parsed.path or "/").rstrip("/") or "/"
    # Query-parametre som wizardId og externalId identifiserer selve skjemaet
    # og må derfor bevares. Bare vanlige sporingsparametre fjernes.
    from urllib.parse import parse_qsl, urlencode
    ignored = {"utm_source", "utm_medium", "utm_campaign", "utm_term",
               "utm_content", "fbclid", "gclid"}
    query = urlencode(sorted(
        (key.lower(), value)
        for key, value in parse_qsl(parsed.query, keep_blank_values=True)
        if key.lower() not in ignored
    ))
    return f"{scheme}://{host}{port}{path}" + (f"?{query}" if query else "")


def add_source_to_records(records, source_url, method):
    source = {"url": canonical_url(source_url), "metode": method}
    for record in records:
        record["kilder"] = [source.copy()]
    return records


def merge_records(records):
    """Dedupliser tjenester og bevar alle kildene på hver tjeneste."""
    unique = {}
    for record in records:
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
        seen_sources = {
            (canonical_url(source.get("url")).lower(), str(source.get("metode") or ""))
            for source in target.get("kilder", [])
            if isinstance(source, dict)
        }
        for source in record.get("kilder", []):
            if not isinstance(source, dict):
                continue
            source_key = (
                canonical_url(source.get("url")).lower(),
                str(source.get("metode") or ""),
            )
            if source_key not in seen_sources:
                target["kilder"].append({
                    "url": canonical_url(source.get("url")),
                    "metode": str(source.get("metode") or ""),
                })
                seen_sources.add(source_key)
    return list(unique.values())


def extract_json_assignment(html, variable_name):
    """Dekod en JSON-array tildelt en JavaScript-variabel."""
    match = re.search(
        rf"\bvar\s+{re.escape(variable_name)}\s*=\s*",
        html,
        flags=re.IGNORECASE,
    )
    if not match:
        return []

    decoder = json.JSONDecoder()
    start = match.end()
    while start < len(html) and html[start].isspace():
        start += 1
    try:
        value, _ = decoder.raw_decode(html[start:])
    except (ValueError, json.JSONDecodeError):
        return []
    return value if isinstance(value, list) else []


def scrape_embedded_skjemaliste(html, base_url, kommune_navn, orgnr):
    """Hent skjema fra ACOS WebForms-variablene col1, col2 og col3."""
    records = []
    for variable_name in ("col1", "col2", "col3"):
        categories = extract_json_assignment(html, variable_name)
        for category in categories:
            if not isinstance(category, dict):
                continue
            category_name = str(
                category.get("Navn")
                or category.get("navn")
                or category.get("Name")
                or ""
            ).strip()
            forms = (
                category.get("SkjemaListe")
                or category.get("skjemaListe")
                or category.get("Forms")
                or []
            )
            if not isinstance(forms, list):
                continue
            for form in forms:
                if not isinstance(form, dict):
                    continue
                name = str(
                    form.get("Navn")
                    or form.get("navn")
                    or form.get("Name")
                    or ""
                ).strip()
                relative_url = str(
                    form.get("Url")
                    or form.get("URL")
                    or form.get("url")
                    or ""
                ).strip()
                if not name or not relative_url:
                    continue
                records.append(
                    make_record(
                        name=name,
                        url=urljoin(base_url, relative_url),
                        category=category_name,
                        description="",
                        kommune_navn=kommune_navn,
                        orgnr=orgnr,
                    )
                )
    return dedupe_records(records)


def scrape_html_catalog(base_url, kommune_navn, orgnr, response=None):
    if response is None:
        response = request_url(base_url)
    if not usable_response(response):
        return [], [], "", "HTML"

    html = response.text
    soup = BeautifulSoup(html, "html.parser")
    source_host = (urlparse(response.url).hostname or "").lower()
    dedicated_catalog = source_host.startswith("skjema.") or source_host.startswith("dialog.")

    content = soup if dedicated_catalog else (
        soup.find("main") or soup.find(attrs={"role": "main"}) or soup.body or soup
    )
    records = []
    candidates = []

    for link in content.find_all("a", href=True):
        raw_href = str(link.get("href") or "").strip()
        text = link.get_text(" ", strip=True)
        if not raw_href or not text or excluded_link(raw_href, text):
            continue
        absolute_url = urljoin(response.url, raw_href)
        path_lower = urlparse(absolute_url).path.lower()

        if dedicated_catalog and ("/skjema/" in path_lower or "/dialogue/" in path_lower):
            classification, reason = "TJENESTE", "klassisk skjemakataloglenke"
        else:
            classification, reason = classify_html_link(link, response.url)
        if classification == "AVVIST":
            continue

        category = nearest_category(link)
        if classification == "TJENESTE":
            records.append(make_record(
                name=text,
                url=absolute_url,
                category=category,
                description="",
                kommune_navn=kommune_navn,
                orgnr=orgnr,
            ))
        else:
            candidates.append({
                "kommune": kommune_navn,
                "organisasjonsnummer": orgnr,
                "kilde_url": response.url,
                "tjenestenavn": text,
                "url": absolute_url,
                "kategori": category,
                "begrunnelse": reason,
            })

    embedded = scrape_embedded_skjemaliste(
        html, response.url, kommune_navn, orgnr
    )
    records.extend(embedded)
    records = dedupe_records(records)

    if embedded and records:
        method = "HTML_INNEBYGD_JSON"
    else:
        method = "HTML"
    return records, dedupe_candidates(candidates), response.url, method

def dedupe_candidates(candidates):
    unique = {}
    for row in candidates:
        url = str(row.get("url") or "").strip()
        text = str(row.get("tjenestenavn") or "").strip()
        if not url or not text:
            continue
        unique.setdefault(url.rstrip("/").lower(), row)
    return list(unique.values())

def dedupe_records(records):
    unique = {}
    for record in records:
        url = str(record.get("url") or "").strip()
        name = str(record.get("tjenestenavn") or "").strip()
        if not url or not name:
            continue
        key = url.rstrip("/").lower()
        if key not in unique:
            unique[key] = record
    return list(unique.values())


def kommune_nettsted(slug):
    """Returner kommunens ordinære nettsted uten nettverksoppslag."""
    overrides = MAIN_DOMAIN_OVERRIDES.get(slug) or []
    if overrides:
        return canonical_url(overrides[0])
    return canonical_url(f"https://www.{slug}.kommune.no/")


def enrich_result_rows(result_rows, kommune_slug_map=None):
    """Fyll nettsted, skjemaside og kilde_url konsekvent.

    nettsted: kommunens ordinære nettsted
    skjemaside: siden/API-inngangen der skjemaene ble funnet
    kilde_url: teknisk kilde, beholdes for bakoverkompatibilitet
    """
    kommune_slug_map = kommune_slug_map or {}
    enriched = []
    for original in result_rows:
        row = dict(original)
        kommune = str(row.get("kommune") or "").strip()
        slug = kommune_slug_map.get(kommune) or normalize_name(kommune)
        status = str(row.get("status") or "").strip()
        source = str(row.get("kilde_url") or row.get("skjemaside") or "").strip()

        # Nettsted skal alltid være kommunens ordinære nettsted, aldri
        # skjemaleverandørens domene. MANUELT_STOPP har en eksplisitt,
        # kontrollert nettadresse som skal bevares.
        if str(row.get("metode") or "") != "MANUELT_STOPP":
            row["nettsted"] = kommune_nettsted(slug)
        elif not str(row.get("nettsted") or "").strip():
            row["nettsted"] = kommune_nettsted(slug)

        if status in {"FUNNET", "TOM_KILDE"}:
            if not str(row.get("skjemaside") or "").strip():
                row["skjemaside"] = source
        elif status in {"IKKE_FUNNET", "INGEN_SKJEMAKILDE"}:
            row["skjemaside"] = ""

        # Ved IKKE_FUNNET var eldre kilde_url ofte kommunens hovedside. Flytt
        # den til nettsted og tøm teknisk kilde, siden ingen skjemakilde finnes.
        if status == "IKKE_FUNNET" and source:
            if not str(original.get("nettsted") or "").strip():
                row["nettsted"] = canonical_url(source)
            row["kilde_url"] = ""
        elif source:
            row["kilde_url"] = source

        enriched.append(row)
    return enriched


def check_main_domain(slug):
    urls = MAIN_DOMAIN_OVERRIDES.get(slug) or [
        f"https://{slug}.kommune.no",
        f"https://www.{slug}.kommune.no",
    ]
    for url in urls:
        response = request_url(url)
        if usable_response(response):
            return True, response.url
    return False, ""


def process_commune(kommune_navn, orgnr):
    slug = normalize_name(kommune_navn)
    result_rows = []


    source_results = []
    all_candidates = []
    processed_sources = set()

    # ACOS/HTML-kandidater. Redirect til samme endelige kilde behandles én gang.
    for candidate_url in candidate_sources(slug)[:2]:
        response = request_url(candidate_url)
        if not usable_response(response):
            continue
        resolved_url = canonical_url(response.url)
        resolved_key = resolved_url.lower()
        if resolved_key in processed_sources:
            continue
        processed_sources.add(resolved_key)

        acos_records, api_url = scrape_acos(resolved_url, kommune_navn, orgnr)
        if acos_records:
            method = "ACOS_API"
            records = add_source_to_records(
                dedupe_records(acos_records), resolved_url, method
            )
            candidates = []
            note = f"API: {api_url}"
        else:
            records, candidates, final_url, method = scrape_html_catalog(
                resolved_url, kommune_navn, orgnr, response=response
            )
            resolved_url = canonical_url(final_url or resolved_url)
            records = add_source_to_records(records, resolved_url, method)
            note = (
                "Skjema hentet fra innebygd JavaScript-JSON og/eller HTML."
                if method == "HTML_INNEBYGD_JSON"
                else "Skjema hentet fra HTML."
            )
        all_candidates.extend(candidates)
        source_results.append({
            "url": resolved_url,
            "metode": method,
            "records": records,
            "candidates": candidates,
            "merknad": note,
        })

    # skjema.no API er en uavhengig tredje kilde.
    skjema_records, skjema_api_url, skjema_note = scrape_skjema_no_api(
        slug, kommune_navn, orgnr
    )
    if skjema_records:
        source_url = canonical_url(f"https://skjema.no/{slug}")
        if source_url.lower() not in processed_sources:
            processed_sources.add(source_url.lower())
            method = "SKJEMA_NO_API"
            source_results.append({
                "url": source_url,
                "metode": method,
                "records": add_source_to_records(
                    dedupe_records(skjema_records), source_url, method
                ),
                "candidates": [],
                "merknad": skjema_note,
            })

    # Beregn deterministisk unikt bidrag per kilde. Da summerer
    # sources[*].antall_tjenester alltid til kommunens antall_tjenester.
    claimed_urls = set()
    sources = []
    all_records = []
    for source in source_results:
        source_urls = {
            canonical_url(record.get("url")).lower()
            for record in source["records"]
            if record.get("url")
        }
        unique_contribution = source_urls - claimed_urls
        overlap = source_urls & claimed_urls
        claimed_urls.update(source_urls)
        all_records.extend(source["records"])

        source_summary = {
            "url": source["url"],
            "metode": source["metode"],
            "antall_unikt_bidrag": len(unique_contribution),
            "antall_i_kilden": len(source_urls),
            "antall_overlapp": len(overlap),
            "antall_kandidater": len(source["candidates"]),
        }
        sources.append(source_summary)
        status = "FUNNET" if source_urls else "TOM_KILDE"
        result_rows.append({
            "organisasjonsnummer": orgnr,
            "kommune": kommune_navn,
            "status": status,
            "kilde_url": source["url"],
            "antall_unikt_bidrag": len(unique_contribution),
            "antall_i_kilden": len(source_urls),
            "antall_overlapp": len(overlap),
            "antall_kandidater": len(source["candidates"]),
            "metode": source["metode"],
            "merknad": source["merknad"],
        })

    all_records = merge_records(all_records)
    all_candidates = dedupe_candidates(all_candidates)
    service_urls = {canonical_url(record["url"]).lower() for record in all_records}
    all_candidates = [
        row for row in all_candidates
        if canonical_url(row.get("url")).lower() not in service_urls
    ]

    if not all_records and not all_candidates and not source_results:
        main_exists, main_url = check_main_domain(slug)
        result_rows.append({
            "organisasjonsnummer": orgnr,
            "kommune": kommune_navn,
            "status": "IKKE_FUNNET",
            "kilde_url": main_url,
            "antall_unikt_bidrag": 0,
            "antall_i_kilden": 0,
            "antall_overlapp": 0,
            "antall_kandidater": 0,
            "metode": "HOVEDDOMENE" if main_exists else "",
            "merknad": "Ingen skjemakandidat svarte.",
        })

    return {
        "kommune": kommune_navn,
        "orgnr": orgnr,
        "slug": slug,
        "sources": sources,
        "records": all_records,
        "candidates": all_candidates,
        "result_rows": result_rows,
    }

def load_existing_outputs(kommuner=None):
    """Les tidligere resultater med organisasjonsnummer som identitetsnøkkel."""
    existing_treff = {}
    existing_rows = []

    if not RESUME_EXISTING:
        return existing_treff, existing_rows

    kommuner = kommuner or []
    orgs_by_name = {}
    for item in kommuner:
        orgs_by_name.setdefault(item["name"], []).append(item["orgnr"])

    if OUTPUT_JSON.exists():
        try:
            with OUTPUT_JSON.open("r", encoding="utf-8-sig") as file:
                data = json.load(file)
            if isinstance(data, list):
                for item in data:
                    if not isinstance(item, dict):
                        continue
                    orgnr = str(item.get("organisasjonsnummer") or "").strip()
                    if orgnr:
                        existing_treff[orgnr] = item
        except (OSError, ValueError, json.JSONDecodeError) as exc:
            print(f"ADVARSEL: Kunne ikke lese {OUTPUT_JSON}: {exc}")

    if OUTPUT_RESULT_CSV.exists():
        try:
            with OUTPUT_RESULT_CSV.open("r", encoding="utf-8-sig", newline="") as file:
                for row in csv.DictReader(file):
                    kommune = str(row.get("kommune") or "").strip()
                    if not kommune:
                        continue
                    orgnr = str(row.get("organisasjonsnummer") or "").strip()
                    if not orgnr:
                        matches = orgs_by_name.get(kommune, [])
                        if len(matches) == 1:
                            orgnr = matches[0]
                            row["organisasjonsnummer"] = orgnr
                        elif len(matches) > 1:
                            print(
                                f"ADVARSEL: Gammel resultatrad for {kommune} mangler "
                                "organisasjonsnummer og kan ikke knyttes entydig. "
                                "Raden ignoreres."
                            )
                            continue
                    if orgnr:
                        existing_rows.append(row)
        except OSError as exc:
            print(f"ADVARSEL: Kunne ikke lese {OUTPUT_RESULT_CSV}: {exc}")

    orgs_in_rows = {
        str(row.get("organisasjonsnummer") or "").strip()
        for row in existing_rows
        if str(row.get("organisasjonsnummer") or "").strip()
    }
    for orgnr, item in existing_treff.items():
        if orgnr in orgs_in_rows:
            continue
        kommune = str(item.get("kommune") or "").strip()
        records = item.get("records") or []
        sources = item.get("sources") or []
        total = int(item.get("antall_tjenester") or len(records))
        if sources:
            for src in sources:
                existing_rows.append({
                    "organisasjonsnummer": orgnr,
                    "kommune": kommune,
                    "status": "FUNNET",
                    "kilde_url": str(src.get("url") or ""),
                    "antall_unikt_bidrag": src.get(
                        "antall_unikt_bidrag", src.get("antall_tjenester", total)
                    ),
                    "antall_i_kilden": src.get(
                        "antall_i_kilden", src.get("antall_tjenester", total)
                    ),
                    "antall_overlapp": src.get("antall_overlapp", 0),
                    "antall_kandidater": 0,
                    "metode": str(src.get("metode") or "GJENOPPBYGD_FRA_TREFF_JSON"),
                    "merknad": "Resultatrad gjenoppbygd fra eksisterende treff.json.",
                })
        else:
            existing_rows.append({
                "organisasjonsnummer": orgnr,
                "kommune": kommune,
                "status": "FUNNET",
                "kilde_url": "",
                "antall_unikt_bidrag": total,
                "antall_i_kilden": total,
                "antall_overlapp": 0,
                "antall_kandidater": 0,
                "metode": "GJENOPPBYGD_FRA_TREFF_JSON",
                "merknad": "Resultatrad gjenoppbygd fra eksisterende treff.json.",
            })

    return existing_treff, existing_rows


def resume_plan(existing_treff, existing_rows):
    """Finn ferdige og problematiske organisasjonsnumre."""
    if not existing_rows:
        return set(), set(existing_treff)

    by_orgnr = {}
    for row in existing_rows:
        orgnr = str(row.get("organisasjonsnummer") or "").strip()
        if not orgnr:
            continue
        entry = by_orgnr.setdefault(orgnr, {"statuses": set(), "methods": set()})
        entry["statuses"].add(str(row.get("status") or "").strip())
        entry["methods"].add(str(row.get("metode") or "").strip())

    retry_statuses = {"TOM_KILDE", "IKKE_FUNNET", "FEIL"}
    completed = set()
    retry = set()
    for orgnr, entry in by_orgnr.items():
        if any(method.startswith("UNNTAK_") for method in entry["methods"]):
            completed.add(orgnr)
        elif "FUNNET" in entry["statuses"]:
            completed.add(orgnr)
        elif entry["statuses"].intersection(retry_statuses):
            retry.add(orgnr)
    return completed, retry


def build_missing_rows(kommuner, treff_by_orgnr, result_rows):
    missing = []
    for item in kommuner:
        if item["orgnr"] in treff_by_orgnr:
            continue
        missing.append({
            "organisasjonsnummer": item["orgnr"],
            "kommune": item["name"],
            "slug": normalize_name(item["name"]),
        })
    return missing


def migrate_count_fields_in_treff(treff):
    """Migrer kildetelling til tydelige, ikke-misvisende feltnavn.

    Kommunens antall_tjenester beholdes som totalt antall unike tjenester.
    På kildenivå brukes antall_unikt_bidrag i stedet for antall_tjenester.
    """
    migrated = []
    for item in treff:
        if not isinstance(item, dict):
            continue
        commune = dict(item)
        new_sources = []
        for source in commune.get("sources") or []:
            if not isinstance(source, dict):
                continue
            src = dict(source)
            if "antall_unikt_bidrag" not in src:
                src["antall_unikt_bidrag"] = src.get("antall_tjenester", 0)
            src.pop("antall_tjenester", None)
            new_sources.append(src)
        commune["sources"] = new_sources
        migrated.append(commune)
    return migrated


def migrate_count_fields_in_rows(rows):
    """Migrer eldre resultat-rader til antall_unikt_bidrag."""
    migrated = []
    for original in rows:
        row = dict(original)
        if not str(row.get("antall_unikt_bidrag") or "").strip():
            row["antall_unikt_bidrag"] = row.get("antall_tjenester", "")
        row.pop("antall_tjenester", None)
        migrated.append(row)
    return migrated


def dedupe_result_rows(rows):
    """Fjern identiske resultat-rader fra tidligere eller parallelle kjøringer."""
    unique = {}
    for row in rows:
        key = (
            str(row.get("organisasjonsnummer") or "").strip(),
            str(row.get("kommune") or "").strip(),
            str(row.get("status") or "").strip(),
            canonical_url(row.get("nettsted")) if row.get("nettsted") else "",
            canonical_url(row.get("skjemaside")) if row.get("skjemaside") else "",
            canonical_url(row.get("kilde_url")) if row.get("kilde_url") else "",
            str(row.get("metode") or "").strip(),
            str(row.get("antall_unikt_bidrag") or "").strip(),
            str(row.get("antall_i_kilden") or "").strip(),
            str(row.get("antall_overlapp") or "").strip(),
        )
        unique.setdefault(key, row)
    return list(unique.values())


def write_outputs(treff, result_rows, missing, candidates):
    treff = migrate_count_fields_in_treff(treff)
    result_rows = migrate_count_fields_in_rows(result_rows)
    result_rows = dedupe_result_rows(result_rows)
    treff_sorted = sorted(treff, key=lambda item: item["kommune"])
    slug_map = {
        str(item.get("kommune") or "").strip(): str(item.get("slug") or "").strip()
        for item in treff if isinstance(item, dict)
    }
    result_rows = enrich_result_rows(result_rows, slug_map)
    result_sorted = sorted(result_rows, key=lambda row: (
        str(row.get("organisasjonsnummer") or ""),
        str(row.get("kommune") or ""), str(row.get("status") or ""),
        str(row.get("kilde_url") or "")
    ))

    with OUTPUT_JSON.open("w", encoding="utf-8") as file:
        json.dump(treff_sorted, file, ensure_ascii=False, indent=2)

    result_fields = [
        "organisasjonsnummer", "kommune", "status", "nettsted", "skjemaside", "kilde_url", "antall_unikt_bidrag",
        "antall_i_kilden", "antall_overlapp",
        "antall_kandidater", "metode", "merknad"
    ]
    with OUTPUT_RESULT_CSV.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=result_fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(result_sorted)

    with OUTPUT_MISSING_CSV.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=["kommune", "organisasjonsnummer", "slug"])
        writer.writeheader()
        writer.writerows(sorted(missing, key=lambda row: row["kommune"]))

    candidate_fields = [
        "kommune", "organisasjonsnummer", "kilde_url", "tjenestenavn",
        "url", "kategori", "begrunnelse"
    ]
    with OUTPUT_CANDIDATES_CSV.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=candidate_fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(sorted(candidates, key=lambda row: (
            str(row.get("kommune") or ""), str(row.get("url") or "")
        )))

def main():
    kommuner = load_kommuner()
    existing_treff, existing_rows = load_existing_outputs(kommuner)
    completed, retry = resume_plan(existing_treff, existing_rows)

    # Gamle rader og gamle treff for kommuner som skal prøves på nytt fjernes
    # før nye resultater flettes inn. Øvrige resultater bevares.
    treff_by_orgnr = {
        orgnr: item
        for orgnr, item in existing_treff.items()
        if orgnr not in retry
    }
    result_rows = [
        row for row in existing_rows
        if str(row.get("organisasjonsnummer") or "").strip() not in retry
    ]
    missing_by_kommune = {}
    all_candidates = []
    if OUTPUT_CANDIDATES_CSV.exists():
        try:
            with OUTPUT_CANDIDATES_CSV.open("r", encoding="utf-8-sig", newline="") as file:
                all_candidates = list(csv.DictReader(file))
        except OSError:
            all_candidates = []
    # Kandidater for kommuner som kjøres på nytt fjernes før nye legges inn.
    all_candidates = [
        row for row in all_candidates
        if str(row.get("organisasjonsnummer") or "").strip() not in retry
    ]

    pending = [item for item in kommuner if item["orgnr"] not in completed]
    print(
        f"Skal behandle {len(pending)} kommuner. "
        f"Hopper over {len(kommuner) - len(pending)} ferdigbehandlede kommuner. "
        f"Prøver {len(retry)} problemkommuner på nytt."
    )

    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as executor:
        futures = {
            executor.submit(process_commune, item["name"], item["orgnr"]): item
            for item in pending
        }

        for future in as_completed(futures):
            item = futures[future]
            try:
                result = future.result()
            except Exception as exc:
                print(f"FEIL {item['name']}: {exc}")
                result_rows.append(
                    {
                        "organisasjonsnummer": item["orgnr"],
                        "kommune": item["name"],
                        "status": "FEIL",
                        "kilde_url": "",
                        "antall_unikt_bidrag": 0,
                        "metode": "",
                        "merknad": str(exc),
                    }
                )
                missing_by_kommune[item["name"]] = {
                    "kommune": item["name"],
                    "organisasjonsnummer": item["orgnr"],
                    "slug": normalize_name(item["name"]),
                }
                continue

            result_rows.extend(result["result_rows"])
            all_candidates.extend(result.get("candidates", []))
            statuses = {row["status"] for row in result["result_rows"]}

            if result["records"]:
                treff_by_orgnr[result["orgnr"]] = {
                    "kommune": result["kommune"],
                    "organisasjonsnummer": result["orgnr"],
                    "slug": result["slug"],
                    "sources": result["sources"],
                    "antall_tjenester": len(result["records"]),
                    "records": result["records"],
                }
                print(
                    f"FUNNET {result['kommune']}: "
                    f"{len(result['records'])} unike tjenester"
                )
            elif "UNNTAK" not in statuses and not result.get("candidates"):
                missing_by_kommune[result["kommune"]] = {
                    "kommune": result["kommune"],
                    "organisasjonsnummer": result["orgnr"],
                    "slug": result["slug"],
                }
                print(f"IKKE FUNNET {result['kommune']}")
            else:
                print(f"UNNTAK {result['kommune']}")

    final_missing = build_missing_rows(kommuner, treff_by_orgnr, result_rows)

    write_outputs(
        list(treff_by_orgnr.values()),
        result_rows,
        final_missing,
        dedupe_candidates(all_candidates),
    )

    total_services = sum(
        int(item.get("antall_tjenester") or len(item.get("records") or []))
        for item in treff_by_orgnr.values()
    )
    manual_count = len(
        {
            row.get("kommune")
            for row in result_rows
            if row.get("status") == "UNNTAK"
        }
    )

    print()
    print("=" * 72)
    print(f"Kommuner i input: {len(kommuner)}")
    print(f"Kommuner med tjenester: {len(treff_by_orgnr)}")
    print(f"Unntakskommuner: {manual_count}")
    print(f"Kommuner uten tjenester: {len(final_missing)}")
    print(f"Totalt antall unike tjenester: {total_services}")
    print(f"Lagret: {OUTPUT_JSON}")
    print(f"Lagret: {OUTPUT_RESULT_CSV}")
    print(f"Lagret: {OUTPUT_MISSING_CSV}")
    


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("Avbrutt av bruker.")
        print("Skriptet ble avbrutt.")
    except Exception as exc:
        print(f"KRITISK FEIL: {exc}")
        print("Skriptet avsluttet med feil.")
