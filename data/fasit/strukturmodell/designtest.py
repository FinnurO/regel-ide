"""Designtest: kan dagens modell / revidert modell uttrykke hvert utsagn i fasiten?

Klassifiserer hvert (kategori, type) i fasit/*.json mot to modeller:
  dagens   = regel-ide master 91bfff8 (Virksomhet, VirksomhetRelasjon, Begrep gruppe/virksomhet,
             Myndighetstildeling, GruppeMedlemskap)
  revidert = forslaget i designnotatet (nodetyper + R/K/M/O/A/G/I-kanter, polaritet, avgrensning)
Verdier: ja | delvis | nei | senere_lag (bevisst utenfor strukturlaget, jf. forslagets punkt 9)
"""
import json, glob, os, re, sys, collections

HER = os.path.dirname(os.path.abspath(__file__))
FILER = ["sameloven", "energiloven", "helse-og-omsorgstjenesteloven", "spesialisthelsetjenesteloven", "domstolloven"]

# --- Standardtyper ----------------------------------------------------------------------------------
# (dagens, revidert, revidert-element)
STD = {
    # relasjon aktør->aktør
    "klageinstans_for": ("ja", "ja", "R"), "administrativt_underordnet": ("ja", "ja", "R"),
    "sekretariat_for": ("ja", "ja", "R"), "etterfolger": ("ja", "ja", "R"), "del_av@relasjon": ("ja", "ja", "G"),
    "instruksjon": ("nei", "ja", "R"), "omgjoring": ("nei", "ja", "R"), "tilsyn_med_aktor": ("nei", "ja", "R"),
    "rapporterer_til": ("nei", "ja", "R"), "oppnevner": ("nei", "ja", "R"), "velger": ("nei", "ja", "R"),
    "ledes_av": ("nei", "ja", "R"), "eies_av": ("nei", "ja", "R"), "radgir": ("nei", "ja", "R"),
    "delegerer_til": ("nei", "ja", "R"),
    "bistar": ("nei", "senere_lag", "-"), "samarbeider_med": ("nei", "senere_lag", "-"),
    # kompetanse aktør->bestemmelse
    "forskriftskompetanse": ("delvis", "ja", "K"), "vedtakskompetanse": ("delvis", "ja", "K"),
    "klagekompetanse": ("delvis", "ja", "K"), "tilsynskompetanse": ("delvis", "ja", "K"),
    "delegeringsfullmakt": ("delvis", "ja", "K"), "oppnevningskompetanse": ("delvis", "ja", "K"),
    "instruksjonskompetanse": ("delvis", "ja", "K"), "utpekingskompetanse": ("delvis", "ja", "K"),
    # [Ny, issue #341, 2026-10-08] Typene etter konverteringen (konvertering-341-kompetanse.py): myndighetsrelasjonene er
    # kompetanse med motpart, og K har hele typologien. Samme vurdering som de gamle kompetansetypene over (dagens:
    # delvis via myndighetstildeling; revidert: ja, K). har_delegert_til/representerer er R som delegerer_til var.
    **{t: ("delvis", "ja", "K") for t in [
        "normgivningskompetanse", "delegeringskompetanse", "omgjoringskompetanse", "avsettingskompetanse",
        "sanksjonskompetanse", "samtykkekompetanse", "overprovingskompetanse", "foreleggingskompetanse",
        "revisjonskompetanse", "godkjenningskompetanse", "organisasjonskompetanse", "ansettelseskompetanse",
        "beslutningskompetanse", "samordningskompetanse", "opprettingskompetanse", "avviklingskompetanse",
        "paleggskompetanse", "stadfestingskompetanse"]},
    "har_delegert_til": ("nei", "ja", "R"), "representerer": ("nei", "ja", "R"),
    # medlemskap
    "medlem_av": ("ja", "ja", "M"), "inngar_i": ("ja", "ja", "M"),
    # område
    "bestar_av": ("nei", "ja", "O"), "del_av@sammensetning_omrade": ("nei", "ja", "O"),
    "har_ansvarsomrade": ("nei", "ja", "A"), "har_jurisdiksjon": ("nei", "ja", "A"), "har_sete_i": ("nei", "ja", "A"),
    # konstituerende
    "oppretter": ("nei", "ja", "R"), "avvikler": ("delvis", "ja", "R"), "skal_finnes": ("nei", "ja", "T"),
    # organsammensetning
    "har_medlemmer": ("nei", "ja", "G"), "har_organ": ("nei", "ja", "G"),
}

# --- annet:* — nøkkelordregler (første treff vinner). Hver regel er begrunnet i designnotatet §5. ----
REGLER = [
    # restliste etter første kjøring (manuelt vurdert 2026-10-07)
    (r"virksomhetsoverforing|reviderer|innstiller_for|stedfortreder_for|overordnet_domstol|sanksjonsmyndighet_over|administrerer", ("nei", "ja", "R")),
    (r"fastsette_rettssted|delegert_myndighet", ("delvis", "ja", "K")),
    (r"kanal_via|ressursplikt|kan_kreve_bevisopptak|varsler", ("nei", "senere_lag", "-")),
    (r"valgmate|funksjonsperiode|felles_organ|daglig_ledelse|ledelsesstruktur", ("nei", "ja", "G")),
    (r"registerforing|mottaker_av_soknader|myndighet_etter_forskrift|koordineringsmyndighet|fastsette_medlemskap|samordningsansvar", ("delvis", "ja", "K")),
    (r"underlagt_statlig_personalregelverk", ("nei", "ja", "M")),
    (r"initiativ_og_uttalerett|tilgangsrett|saksforberedelse|kan_initiere|kan_kreve_innkalling|avtalebasert_tjenesteyting|avleverer_til|oversender_sak", ("nei", "senere_lag", "-")),
    # kompetansevarianter -> K (kompetansetype er konfigurerbar, ikke lukket liste)
    (r"kompetanse$|^vedtekts|^reglements|^ansettelses|^innsigelses|normering|samtykke|^begjaer", ("delvis", "ja", "K")),
    (r"overprov|godkjenn|iverksett|ikraftsett|intern_regelgiv|medlemskapsfastsett|omradeinndeling|omradefastsett", ("delvis", "ja", "K")),
    (r"forvaltningsansvar|ressort", ("nei", "ja", "K")),
    # delegering/overføring med unntak og historikk
    (r"delegering|ansvarsoverforing|oppgave_overfort|overtar_rettigheter|tjenesteutsetting", ("delvis", "ja", "R")),
    # rolleinnehav / utpeking
    (r"utpekt_som|innehar_rolle|lovtildelt_oppgave|oppgaveansvar", ("delvis", "ja", "I")),
    # representasjon / på vegne av / eierstyring
    (r"representer|pa_vegne_av|utover_myndighet|eierstyring|utover_myndighet_gjennom|forvalter_av", ("nei", "ja", "R")),
    (r"avsetter|innkaller|forutgaende_instans|ankeinstans|domstolsoverprov|vedtak_bindende", ("nei", "ja", "R")),
    (r"uavhengig|uforenlig", ("nei", "ja", "R")),
    # status = medlemskap i en rettslig klasse («forvaltningsorgan», «selvstendig rettssubjekt»)
    (r"status|rettssubjekt|forvaltningsorgan|statsforvaltning|enkeltvedtak|underlagt_regelverk|regelverk_gjelder|tilskriv", ("nei", "ja", "M")),
    # klasser definert ved kriterium, komplement, partisjon
    (r"intensjonal|komplement|partisjon|klasse_definert|tjenestekrets|lokalisert", ("nei", "ja", "M")),
    # område
    (r"virkeomrade|stedlig|omrade|valgkrets|mandatfordeling|bostedsregion|inneholder|region|fastfrys|avledet_tilknytning|valgbarhet", ("nei", "ja", "A")),
    # organsammensetning / selskapsorganer
    (r"funksjonstid|moteplikt|moterett|stiftelse|organisasjonsform|tidsbegrenset_rolle|deltar_i|velgerkorps", ("nei", "ja", "G")),
    (r"felles_opprettelse|opprett", ("nei", "ja", "R")),
    # bevisst senere lag: prosess, informasjon, finansiering, saksforberedelse
    (r"informasjon|konsultasjon|anmod|fremmer|forbereder|horing|opplysning|veileder|medvirk|finansier|kompenser|dekker_utgifter|oppgjor|bevilgning|hefter|ramme|adgang|tvist|part_i_sak|forelegg|klagerett|fastsetter_vilkar|avtalt|ansvarsdeling|sorge_for|saklig_arbeidsomrade|definisjon|avgrenset_mot", ("nei", "senere_lag", "-")),
]

def klassifiser(kat, typ):
    if typ in STD: return STD[typ]
    if f"{typ}@{kat}" in STD: return STD[f"{typ}@{kat}"]
    navn = (kat + " " + typ).replace("annet:", "")
    for rx, v in REGLER:
        if re.search(rx, navn): return v
    return None

def main():
    rader, uklass = [], collections.Counter()
    akt = collections.Counter(); oppl = collections.Counter(); negativ = avgr = utenfor = 0
    for n in FILER:
        p = os.path.join(HER, n + ".json")
        if not os.path.exists(p): print("mangler", n); continue
        d = json.load(open(p, encoding="utf-8"))
        for a in d["aktorer"]:
            akt[(n, a["entitetstype"])] += 1
            oppl[a.get("oppløsning") or a.get("opplosning")] += 1
        for u in d["utsagn"]:
            v = klassifiser(u["kategori"], u["type"])
            if v is None: uklass[(u["kategori"], u["type"])] += 1; v = ("?", "?", "?")
            dag, rev, el = v
            # Egenskaper dagens modell mangler uansett type: negativ polaritet, avgrensning på VirksomhetRelasjon
            if u.get("polaritet") == "negativ":
                negativ += 1
                if dag == "ja": dag = "delvis"
            if u.get("avgrensning") and el == "R" and dag == "ja":
                avgr += 1; dag = "delvis"
            if u.get("kilde_utenfor_korpus"): utenfor += 1
            if rev == "ja" and re.search(r"intensjonal|komplement|partisjon|klasse_definert|tjenestekrets|mandatfordeling|bostedsregion|fastfrys|avledet|valgbarhet|subsidi", u["type"]):
                rev = "delvis"   # modellen kan lagre utsagnet, men ikke avgjøre medlemskap uten regelevaluering
            rader.append((n, u["kategori"], u["type"], dag, rev, el))
    tot = len(rader)
    print(f"Utsagn: {tot}  (negative: {negativ}, kilde utenfor korpus: {utenfor})")
    for modell, i in (("dagens", 3), ("revidert", 4)):
        c = collections.Counter(r[i] for r in rader)
        print(modell, {k: f"{v} ({100*v/tot:.0f} %)" for k, v in c.most_common()})
    print("\nPer rettskilde (dagens ja+delvis / revidert ja, av strukturutsagn = ikke senere_lag):")
    for n in FILER:
        rs = [r for r in rader if r[0] == n and r[4] != "senere_lag"]
        if not rs: continue
        dj = sum(r[3] == "ja" for r in rs); dd = sum(r[3] == "delvis" for r in rs); rj = sum(r[4] == "ja" for r in rs)
        print(f"  {n:32s} n={len(rs):4d}  dagens ja {100*dj/len(rs):3.0f} %  delvis {100*dd/len(rs):3.0f} %  revidert ja {100*rj/len(rs):3.0f} %")
    print("\nRevidert-element (struktur):", dict(collections.Counter(r[5] for r in rader if r[4] == "ja")))
    print("\nAktører per entitetstype:", dict(collections.Counter(k[1] for k in akt.elements())))
    print("Oppløsning av referent:", dict(oppl))
    if uklass:
        print("\nUKLASSIFISERT:", file=sys.stderr)
        for k, v in uklass.most_common(): print(f"  {v} {k}", file=sys.stderr)
    json.dump([dict(zip(["kilde", "kategori", "type", "dagens", "revidert", "element"], r)) for r in rader],
              open(os.path.join(HER, "designtest-resultat.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

if __name__ == "__main__":
    main()
