using System.Text.Json.Nodes;

namespace RegelIde.Data;

/// <summary>
/// [Ny, 2026-08-28] Hand-bygget JSON Schema (draft 2020-12) for nøyaktig den formen
/// <see cref="RettighetModellEksportTjeneste"/> produserer — for både mennesker OG KI-agenter som skal
/// forstå/konsumere modelleksporten uten å lese C#-koden. Servert live via
/// <c>GET /api/tjenester/modelleksport/schema</c> (<c>application/schema+json</c>), se docs/23-tjeneste-
/// modell-eksport-og-skjema.md.
/// <para>
/// Kodefeltenes <c>enum</c>-lister REFERERER de faktiske <c>GyldigeX</c>-arrayene (aldri retypede
/// literaler) — samme "ingen gjettet fallback"/ingen-duplisert-sannhet-holdning som resten av huset.
/// <c>retning</c> (avhengigheter) og <c>mal_type</c> har ingen egen <c>GyldigeX</c>-array siden de
/// beregnes (ikke lagres direkte, se <see cref="TjenesteavhengighetregisterTjeneste"/>/
/// <see cref="RettighetModellEksportTjeneste.AvhengighetJson"/>) — literal-enum for disse to, forklart
/// i deres egen <c>description</c>.
/// </para>
/// <para>
/// Dette skjemaet beskriver EKSPORTFORMATET — det er IKKE (ennå) en importkontrakt. Det finnes ingen
/// importmotpart til <see cref="RettighetModellEksportTjeneste"/> i dag (se docs/13-backlog.md og
/// docs/23-tjeneste-modell-eksport-og-skjema.md).
/// </para>
/// </summary>
public static class TjenesteModellSkjema
{
    public static JsonObject Bygg()
    {
        return new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = "tjeneste-modell.schema.json",
            ["title"] = "Rettighet (Tjeneste) — modelleksport",
            ["description"] =
                "Beskriver JSON-formen fra GET /api/tjenester/{id}/modelleksport (én rettighet, bart " +
                "objekt) OG GET /api/tjenester/modelleksport (flere rettigheter, { rettigheter: [...] }). " +
                "Brukes til tre formål: (1) intern verifisering av at det bygde stemmer med det " +
                "modellerte, (2) ekstern deling av en tjenestes fulle modell ut av applikasjonen, og " +
                "(3) et fremtidig importmål. VIKTIG: dette er per i dag KUN eksportformatet — det finnes " +
                "ingen importmotpart i applikasjonen ennå.",
            ["$defs"] = Definisjoner(),
            ["oneOf"] = new JsonArray
            {
                new JsonObject { ["$ref"] = "#/$defs/Rettighet" },
                new JsonObject
                {
                    ["type"] = "object",
                    ["description"] = "Flertalls-eksport (GET /api/tjenester/modelleksport).",
                    ["properties"] = new JsonObject
                    {
                        ["rettigheter"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject { ["$ref"] = "#/$defs/Rettighet" },
                        },
                    },
                    ["required"] = new JsonArray("rettigheter"),
                    ["additionalProperties"] = false,
                },
            },
        };
    }

    // ---------- små JSON Schema-byggeklosser ----------

    private static JsonArray TypeArr(params string[] typer) => new(typer.Select(t => (JsonNode)t).ToArray());

    private static JsonArray EnumArr(IEnumerable<string> verdier, bool inkluderNull = false)
    {
        var arr = new JsonArray(verdier.Select(v => (JsonNode)v).ToArray());
        if (inkluderNull) arr.Add(null);
        return arr;
    }

    private static JsonObject Str(string beskrivelse) => new() { ["type"] = "string", ["description"] = beskrivelse };

    private static JsonObject NullableStr(string beskrivelse) =>
        new() { ["type"] = TypeArr("string", "null"), ["description"] = beskrivelse };

    private static JsonObject Bool(string beskrivelse) => new() { ["type"] = "boolean", ["description"] = beskrivelse };

    private static JsonObject EnumStr(IEnumerable<string> verdier, string beskrivelse) =>
        new() { ["type"] = "string", ["enum"] = EnumArr(verdier), ["description"] = beskrivelse };

    private static JsonObject NullableEnumStr(IEnumerable<string> verdier, string beskrivelse) =>
        new() { ["type"] = TypeArr("string", "null"), ["enum"] = EnumArr(verdier, inkluderNull: true), ["description"] = beskrivelse };

    private static JsonObject StrArr(string beskrivelse) =>
        new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = beskrivelse };

    private static JsonObject Ref(string navn) => new() { ["$ref"] = $"#/$defs/{navn}" };

    private static JsonObject NullableRef(string navn, string? beskrivelse = null)
    {
        var o = new JsonObject { ["anyOf"] = new JsonArray(Ref(navn), new JsonObject { ["type"] = "null" }) };
        if (beskrivelse is not null) o["description"] = beskrivelse;
        return o;
    }

    private static JsonObject ArrRef(string navn, string beskrivelse) =>
        new() { ["type"] = "array", ["items"] = Ref(navn), ["description"] = beskrivelse };

    private static JsonObject Objekt(JsonObject properties, string? beskrivelse = null)
    {
        var o = new JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
        if (beskrivelse is not null) o["description"] = beskrivelse;
        return o;
    }

    // ---------- $defs ----------

    private static JsonObject Definisjoner() => new()
    {
        ["Hjemmel"] = Objekt(new JsonObject
        {
            ["lov"] = NullableStr("Rettskildens korte navn (f.eks. \"Alkoholloven – alkhl\")."),
            ["henvisning"] = NullableStr("Presis paragraf-/leddreferanse, typisk en lovdata.no eId-URI."),
        }, "Én hjemmelsreferanse (lov + presis henvisning)."),

        ["EgetInnholdselement"] = Objekt(new JsonObject
        {
            ["id"] = Str("Klientgenerert, stabil id (crypto.randomUUID()) — kan være mål for en " +
                         "felt-nivå regelverksreferanse via feltnøkkelen \"egneInnholdselementer.{id}\"."),
            ["tittel"] = Str("Overskriften brukeren selv har gitt elementet."),
            ["tekst"] = NullableStr("Fritekstinnholdet."),
        }, "Et fritt, egendefinert innholdselement utover de faste Innhold-feltene."),

        ["Regelverksreferanse"] = Objekt(new JsonObject
        {
            ["lov"] = NullableStr("Rettskildens korte navn."),
            ["henvisning"] = NullableStr("Presis paragraf-/leddreferanse (eId-URI)."),
            ["felt"] = NullableStr(
                "Null = referansen gjelder HELE tjenesten (den flate listen i Regelverk-fanen). Satt = " +
                "knyttet til ETT bestemt felt — verdien er en feltnøkkel fra den dokumenterte " +
                "konvensjonen i TjenesteregisterTjeneste.cs (TjenesteFeltnokler): flate feltnavn for " +
                "Grunnleggende-seksjonen (f.eks. \"tittel\", \"kompetentMyndighet\"), punktum-adskilte " +
                "nøkler for Innhold-underfelt (f.eks. \"innhold.hvaRettighetenInnebarer.varighet\"), og " +
                "\"egneInnholdselementer.{id}\" for frie elementer. Ingen fast enum her — egendefinerte " +
                "elementer har dynamiske id-er en fast liste ikke kan romme."),
        }, "Én regelverksreferanse, ev. knyttet til ett bestemt felt."),

        ["Kanal"] = Objekt(new JsonObject
        {
            ["kanal"] = Str("Kanalnavn (fri tekst, f.eks. \"Altinn\", \"papirskjema\")."),
            ["adresse"] = NullableStr("URL/adresse for kanalen, om aktuelt."),
        }),

        ["Vedlegg"] = Objekt(new JsonObject
        {
            ["navn"] = NullableStr("Navnet på vedlegget som kreves."),
            ["kategori"] = NullableStr("Fri kategorisering av vedlegget."),
            ["hjemmel"] = NullableRef("Hjemmel"),
        }),

        ["Veiledningstekst"] = Objekt(new JsonObject
        {
            ["overskrift"] = NullableStr("Overskrift på veiledningsavsnittet."),
            ["innhold"] = NullableStr("Selve veiledningsteksten."),
            ["hjemmel"] = NullableRef("Hjemmel"),
        }),

        ["Arsak"] = Objekt(new JsonObject
        {
            ["arsak"] = NullableStr("Årsak til handlingen (fri tekst)."),
            ["hjemmel"] = NullableRef("Hjemmel"),
        }),

        ["Handling"] = Objekt(new JsonObject
        {
            ["navn"] = Str("Handlingens navn."),
            ["handlingstype"] = NullableEnumStr(HandlingregisterTjeneste.GyldigeHandlingstyper,
                "Handlingens type."),
            ["bruksomraade"] = NullableStr("Fritekst-beskrivelse av når handlingen brukes."),
            ["utfort_av"] = NullableEnumStr(HandlingregisterTjeneste.GyldigeUtfortAv,
                "Hvem som utfører handlingen."),
            ["merknad"] = NullableStr("Fri merknad."),
            ["eies_av_denne_tjenesten"] = Bool(
                "true = denne tjenesten er handlingens EIER (HandlingEntitet.TjenesteId). false = " +
                "handlingen er kun sekundært KOBLET inn i denne tjenestens Handlinger-fane (delt " +
                "mange-til-mange, HandlingTjenesteEntitet) — den eies og forfattes av en annen tjeneste. " +
                "Kun til stede i flertalls- eller enkelt-eksport avhengig av hvilken tjeneste som er " +
                "eksportert; feltet finnes alltid, verdien avgjør eierskap."),
            ["kanaler"] = ArrRef("Kanal", "Kanaler handlingen kan utføres gjennom. Utelatt (ikke tom liste) når ingen finnes."),
            ["behandlingstid"] = Objekt(new JsonObject
            {
                ["frist"] = NullableStr("Behandlingsfrist, fri tekst (f.eks. \"4 uker\")."),
                ["hjemmel"] = NullableRef("Hjemmel"),
            }, "Utelatt når verken frist eller hjemmel er satt."),
            ["kostnad"] = Objekt(new JsonObject
            {
                ["belop"] = NullableStr("Beløp/gebyr, fri tekst."),
                ["hjemmel"] = ArrRef("Hjemmel", "Én eller flere hjemler for kostnaden."),
            }, "Utelatt når verken beløp eller hjemmel er satt."),
            ["vedlegg"] = ArrRef("Vedlegg", "Vedlegg som kreves for handlingen. Utelatt når ingen finnes."),
            ["veiledningstekst"] = ArrRef("Veiledningstekst", "Veiledningstekster knyttet til handlingen. Utelatt når ingen finnes."),
            ["arsaker"] = ArrRef("Arsak", "Årsaker til handlingen. Utelatt når ingen finnes."),
            ["resultat"] = Objekt(new JsonObject
            {
                ["hva"] = NullableStr("Hva resultatet av handlingen er."),
                ["bevis_kanaler"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject { ["kanal"] = NullableStr("Kanal beviset leveres gjennom.") },
                        ["additionalProperties"] = false,
                    },
                },
            }, "Utelatt når verken \"hva\" eller bevis-kanaler er satt."),
        }, "Én handling knyttet til rettigheten — enten eid av den, eller kun koblet inn (se eies_av_denne_tjenesten)."),

        ["Avhengighet"] = Objekt(new JsonObject
        {
            ["rel"] = EnumStr(TjenesteavhengighetregisterTjeneste.GyldigeRel,
                "Relasjonstypen mellom denne rettigheten og motparten."),
            ["retning"] = EnumStr(["fra", "til"],
                "\"fra\" = denne rettigheten er kilden til den rettede kanten, \"til\" = denne " +
                "rettigheten er målet. Beregnet fra hvilken side av lagringsraden rettigheten står på " +
                "(FraTjenesteId/TilTjenesteId) — ingen egen kodeliste-array i domenemodellen."),
            ["mal_type"] = EnumStr(["tjeneste", "ekstern_referanse"],
                "\"tjeneste\" = motparten er en annen ekte tjeneste i systemet (se mal_id). " +
                "\"ekstern_referanse\" = motparten er en ekstern plassholder (se organisasjonsnummer/" +
                "kildeurl). Beregnet ut fra om motparten har en ekte Tjeneste-rad — ingen egen kodeliste-" +
                "array i domenemodellen."),
            ["mal_navn"] = Str("Motpartens visningsnavn, alltid populert uansett mal_type."),
            ["mal_id"] = NullableStr("Id-en til motpart-tjenesten. Kun til stede når mal_type er \"tjeneste\"."),
            ["organisasjonsnummer"] = NullableStr("Motpartens organisasjonsnummer. Kun til stede når mal_type er \"ekstern_referanse\"."),
            ["kildeurl"] = NullableStr("Kildeurl for den eksterne referansen, om oppgitt."),
            ["merknad"] = NullableStr("Fri merknad/beskrivelse av avhengigheten."),
        }, "Én tjenesteavhengighet sett fra denne rettighetens ståsted."),

        ["Innhold"] = Objekt(new JsonObject
        {
            ["tidspunkt_og_frister"] = NullableStr("Tidspunkt/frister knyttet til søknaden/tjenesten."),
            ["vedlegg"] = StrArr("Navn på vedlegg som kreves (fri tekstliste, ikke samme som Handling.vedlegg)."),
            ["vedlegg_merknad"] = NullableStr("Merknad til vedleggslisten."),
            ["opplysninger_som_skal_sendes_inn"] = StrArr("Hvilke opplysninger søkeren skal sende inn."),
            ["opplysninger_merknad"] = NullableStr("Merknad til opplysningslisten."),
            ["veiledning_og_utfylling"] = StrArr("Veiledningspunkter for utfylling."),
            ["veiledning_merknad"] = NullableStr("Merknad til veiledningslisten."),
            ["innsender_og_tilgang"] = Objekt(new JsonObject
            {
                ["hvem_kan_sende"] = StrArr("Hvem som kan sende inn (roller/aktørtyper)."),
                ["innlogging"] = NullableStr("Innloggingskrav, fri tekst."),
            }, "Utelatt når ikke relevant for tjenesten."),
            ["innsending_og_oppfolging"] = Objekt(new JsonObject
            {
                ["kanal"] = NullableStr("Innsendingskanal."),
                ["etter_mottak"] = StrArr("Hva som skjer etter mottak."),
                ["merknad"] = NullableStr("Fri merknad."),
            }, "Utelatt når ikke relevant for tjenesten."),
            ["kontakt_og_hjelp"] = Objekt(new JsonObject
            {
                ["generelt"] = NullableStr("Generell kontaktinformasjon."),
                ["kommunen_kan_veilede_om"] = StrArr("Hva forvaltningen kan veilede om."),
            }, "Utelatt når ikke relevant for tjenesten."),
            ["hva_rettigheten_innebarer"] = Objekt(new JsonObject
            {
                ["innledning"] = NullableStr("Innledende beskrivelse av hva rettigheten innebærer."),
                ["varighet"] = NullableStr("Rettighetens varighet."),
                ["plikter"] = StrArr("Plikter rettighetshaveren har."),
                ["kontroll_og_tilsyn"] = NullableStr("Beskrivelse av kontroll/tilsyn."),
                ["konsekvenser_ved_brudd_pa_regelverket"] =
                    NullableStr("ALLTID hentet fra TjenesteEntitet.KonsekvensVedBrudd (det ekte, " +
                                "eksisterende feltet) — ikke duplisert lagring i selve Innhold-blobben."),
                ["avgrensning_merknad"] = NullableStr("Avgrensning av rettighetens omfang."),
                ["krav_til_drift"] = NullableStr("Krav til drift (Fettutskiller-typen rettigheter)."),
                ["tommeavtale_og_kontroll"] = NullableStr("Tømmeavtale/kontrollordning (Fettutskiller-typen rettigheter)."),
                ["rapportering"] = NullableStr("Rapporteringsplikt (Fettutskiller-typen rettigheter)."),
                ["endringer_i_virksomheten"] = Objekt(new JsonObject
                {
                    ["plikt"] = NullableStr("Plikt ved endringer i virksomheten."),
                    ["eksempler"] = StrArr("Eksempler på relevante endringer."),
                }, "Utelatt når ikke relevant."),
            }, "Utelatt når ikke relevant for tjenesten."),
            ["egne_innholdselementer"] = ArrRef("EgetInnholdselement",
                "Frie, brukerdefinerte innholdsseksjoner utover feltene over. Utelatt (ikke tom liste) " +
                "når tjenesten ikke har noen."),
        }, "Rettighetens \"Innhold\"-fane. HELE objektet er utelatt (null) kun når verken de faste " +
           "feltene eller egne_innholdselementer er satt."),

        ["Rettighet"] = Objekt(new JsonObject
        {
            ["navn"] = Str("Tjenestens/rettighetens tittel."),
            ["tjenesteomrade"] = NullableStr("Innbyggervennlig tema/kategori (egen akse fra los_klassifisering)."),
            ["los_klassifisering"] = NullableStr("Fri tekst — ikke koblet mot det faktiske LOS-vokabularet ennå (LOS 4 er varslet, ikke lansert)."),
            ["livshendelser"] = StrArr("Livshendelser tjenesten er relevant for. Fri tekst, ikke koblet mot et eksternt vokabular ennå."),
            ["type"] = NullableEnumStr(TjenesteregisterTjeneste.GyldigeRettighetstyper, "Rettighetstype (myndighetsutøvelse/ytelse/...)."),
            ["kompetent_myndighet"] = NullableStr(
                "Fri tekst i dag — IKKE utledet fra gruppebegrep/Myndighetstildeling ennå, se " +
                "docs/13-backlog.md §8 (kjent, uløst gap: samme gruppenavn kan i praksis være ulike " +
                "virksomheter i ulike deler av samme rettskilde)."),
            ["status"] = EnumStr(TjenesteregisterTjeneste.GyldigeStatuser, "Rettighetens status i forfatterløpet."),
            ["malgruppe"] = StrArr("Hvem tjenesten retter seg mot."),
            ["formal"] = NullableStr("Tjenestens formål."),
            ["innhold"] = NullableRef("Innhold"),
            ["regelverksreferanser"] = ArrRef("Regelverksreferanse", "Regelverksforankringen — hele tjenesten og/eller enkeltfelt."),
            ["handlinger"] = ArrRef("Handling", "Handlinger knyttet til rettigheten (eide og/eller koblede, se Handling.eies_av_denne_tjenesten)."),
            ["avhengigheter"] = ArrRef("Avhengighet", "Andre rettigheter/eksterne referanser denne rettigheten er avhengig av eller relatert til."),
        }, "Én komplett rettighet (tjeneste)."),
    };
}
