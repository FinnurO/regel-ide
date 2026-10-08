namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #341 «kompetanse med motpart og typologi», 2026-10-08] Data-delen av migrasjonen
/// <c>KompetanseMedMotpart</c>, skilt ut som konstanter slik at <c>KompetanseMigreringTests</c> kjører nøyaktig den
/// SQL-en migrasjonen kjører — samme teknikk som <see cref="RelasjonskodeHarmonisering"/> (#330).
/// <para>
/// <b>Hva den gjør (Johanns beslutninger på #341, 2026-10-08):</b>
/// <list type="number">
/// <item><b>Myndighetsrelasjonene flyttes fra R til K</b> med til = motparten (P1) — SAMME retning, bare kategori og
/// kode endres: <c>klageinstans_for</c> → K <c>klage</c> («A er klageinstans for B» = «A har klagekompetanse overfor
/// B»; fra = klageinstansen, til = den hvis vedtak påklages — kontrollert mot visningsmalen fra #330 og mot hjemmelen
/// for alle tre lokale rader), <c>instruksjon</c> → K <c>instruksjon</c>, <c>omgjoring</c> → K <c>omgjoring</c>,
/// <c>oppnevner</c> → K <c>oppnevning</c> (0 lokale rader for de tre siste). Hjemmel, avgrensning (også
/// Energiklagenemnda-radens), polaritet, kilde og status røres ikke.</item>
/// <item><b>K <c>forskrift</c> → K <c>normgivning</c> med normform <c>forskrift</c></b> (P2; 0 lokale rader).</item>
/// <item><b>Hjemmelssted ut av avgrensningen</b> (feilen fra #311): en M-/I-kant med hjemmel i korpus, tom
/// <c>hjemmel_eid</c> og et avgrensningsspenn som er ETT punkt i kantens EGEN hjemmel, får punktet som
/// <c>hjemmel_eid</c> og tomt spenn. Regelen gjetter ikke: et punkt i hjemmelen er hvor tildelingen står; et spenn
/// som peker inn i en ANNEN rettskilde (gruppebegrepets lov — slik «Legg til tilhørighet»-skjemaet og docs/20 §2.5
/// brukte feltet) er en ekte avgrensning og blir stående. Målt lokalt 2026-10-08: 19 rader (18 M + 1 I), alle ett
/// punkt i egen hjemmel.</item>
/// <item><b>Typekonfigurasjonen</b>: nye K-typer og R <c>har_delegert_til</c> legges inn, alle K-typer får familie og
/// fvl-kategori (Johanns hierarkibeslutning, <see cref="Strukturkanter.Kompetansetyper"/>), K-malene får «overfor»-formen
/// der de fortsatt har teksten fra startsettet (en mal endret bevisst i drift røres ikke), og de flyttede/fjernede
/// kodene (R <c>klageinstans_for</c>/<c>instruksjon</c>/<c>omgjoring</c>/<c>oppnevner</c>/<c>delegerer_til</c>,
/// K <c>forskrift</c>/<c>iverksetting</c>) slettes, så ingen skrivevei kan bruke dem.</item>
/// </list>
/// </para>
/// <para>
/// <b>Avbrytes i stedet for å gjette:</b> R <c>delegerer_til</c>-rader (kompetanse eller gjennomført delegering? kan
/// ikke avgjøres uten kilden), K <c>iverksetting</c>-rader (ingen plass i typologien), og M-/I-rader med et spenn i
/// egen hjemmel som ikke er ett punkt (et spenn eller flere punkter kan ikke bli én <c>hjemmel_eid</c>). Målt lokalt:
/// 0 av hver.
/// </para>
/// <para>
/// <b>Tellingen</b> (samme mønster som #311/#330): totalt antall likt før og etter; ingen kant med en flyttet kode
/// igjen; hver målkode = før + konverterte; hver hjemmelsstedrad har punktet som <c>hjemmel_eid</c> og tomt spenn;
/// én proveniensrad (<c>endret_av = 'migrasjon-341'</c>) per endret kant med de gamle verdiene — det <c>Down</c> leser.
/// Avviker noe: RAISE EXCEPTION.
/// </para>
/// <para>
/// <b>Bevisst IKKE her:</b> <c>grunnlag</c> og <c>delegerbar</c> settes ikke på noen rad (NULL = ikke angitt; å utlede
/// dem ville vært å gjette). Kongen-taggene fra #335 hører til #335.
/// </para>
/// <para>
/// <b>[LÅST] Frosset sammen med migrasjonen.</b> En historisk migrasjon skal gjøre det samme i dag som den dag den ble
/// kjørt. Trengs en endring: ny migrasjon, ikke rediger denne.
/// </para>
/// </summary>
public static class KompetanseMigrering
{
    /// <summary>Hvem migrasjonen skriver som i <c>sist_endret_av</c> og proveniensens <c>endret_av</c>.</summary>
    public const string EndretAv = "migrasjon-341";

    /// <summary>Kodene som flyttes: (gammel kategori, gammel kode) → (ny kategori, ny kode, normform). SQL-en under har
    /// samme tabell som literal (frosset); testene bruker denne.</summary>
    public static readonly IReadOnlyList<(string GammelKategori, string GammelKode, string NyKategori, string NyKode, string? Normform)> Flytting =
    [
        (Strukturkanter.Relasjon, "klageinstans_for", Strukturkanter.Kompetanse, "klage", null),
        (Strukturkanter.Relasjon, "instruksjon", Strukturkanter.Kompetanse, "instruksjon", null),
        (Strukturkanter.Relasjon, "omgjoring", Strukturkanter.Kompetanse, "omgjoring", null),
        (Strukturkanter.Relasjon, "oppnevner", Strukturkanter.Kompetanse, "oppnevning", null),
        (Strukturkanter.Kompetanse, "forskrift", Strukturkanter.Kompetanse, Strukturkanter.Normgivning, "forskrift"),
    ];

    /// <summary>Kodene som er borte fra konfigurasjonen etter migrasjonen (de flyttede + de to som avbryter).</summary>
    public static readonly IReadOnlyList<(string Kategori, string Kode)> FjernedeKoder =
    [
        .. Flytting.Select(f => (f.GammelKategori, f.GammelKode)),
        (Strukturkanter.Relasjon, "delegerer_til"),
        (Strukturkanter.Kompetanse, "iverksetting"),
    ];

    public const string UpSql = """
        DO $do$
        DECLARE
            v_totalt_for integer;
            v_totalt_etter integer;
            v_flyttet integer;
            v_hjemmelsted integer;
            v_ukonverterbare integer;
            v_utenfor_hjemmel integer;
            v_uten_sted integer;
            v_igjen integer;
            v_avvik text;
            v_proveniens integer;
            v_feil_sted integer;
        BEGIN
            CREATE TEMP TABLE flytting_341 (
                gammel_kategori text NOT NULL, gammel_kode text NOT NULL, ny_kategori text NOT NULL, ny_kode text NOT NULL,
                ny_normform text, antall integer, antall_nye_for integer, PRIMARY KEY (gammel_kategori, gammel_kode)) ON COMMIT DROP;
            INSERT INTO flytting_341 (gammel_kategori, gammel_kode, ny_kategori, ny_kode, ny_normform) VALUES
                ('R', 'klageinstans_for', 'K', 'klage', NULL),
                ('R', 'instruksjon', 'K', 'instruksjon', NULL),
                ('R', 'omgjoring', 'K', 'omgjoring', NULL),
                ('R', 'oppnevner', 'K', 'oppnevning', NULL),
                ('K', 'forskrift', 'K', 'normgivning', 'forskrift');

            -- ---- Det som ikke kan avgjøres uten å gjette: avbryt ----
            IF EXISTS (SELECT 1 FROM strukturkanter WHERE kategori = 'R' AND typekode = 'delegerer_til') THEN
                RAISE EXCEPTION 'Issue #341: det finnes R delegerer_til-kanter. Johann (2026-10-08): kompetansen til å delegere er K delegering, en GJENNOMFØRT delegering er R har_delegert_til — hvilken en rad er, avgjøres ved å lese kilden. Konverter dem for hånd og kjør migrasjonen på nytt.';
            END IF;
            IF EXISTS (SELECT 1 FROM strukturkanter WHERE kategori = 'K' AND typekode = 'iverksetting') THEN
                RAISE EXCEPTION 'Issue #341: det finnes K iverksetting-kanter, og iverksetting er ikke i Johanns typologi (P2). Avklar typen for hånd og kjør migrasjonen på nytt.';
            END IF;

            -- ---- Før ----
            UPDATE flytting_341 f SET
                antall = (SELECT count(*) FROM strukturkanter k WHERE k.kategori = f.gammel_kategori AND k.typekode = f.gammel_kode),
                antall_nye_for = (SELECT count(*) FROM strukturkanter k WHERE k.kategori = f.ny_kategori AND k.typekode = f.ny_kode);
            SELECT count(*) INTO v_totalt_for FROM strukturkanter;
            SELECT sum(antall) INTO v_flyttet FROM flytting_341;

            -- Hjemmelssted: ett punkt i kantens EGEN hjemmel (se klassekommentaren).
            CREATE TEMP TABLE hjemmelsted_341 ON COMMIT DROP AS
            SELECT k."Id" AS id, k.avgrensning_paragrafspenn_json AS gammelt_spenn,
                   (k.avgrensning_paragrafspenn_json::jsonb -> 0 ->> 'FraEid') AS eid
            FROM strukturkanter k
            WHERE k.kategori IN ('M', 'I') AND k.hjemmel_rettskilde_id IS NOT NULL AND k.hjemmel_eid IS NULL
              AND jsonb_array_length(k.avgrensning_paragrafspenn_json::jsonb) = 1
              AND (k.avgrensning_paragrafspenn_json::jsonb -> 0 ->> 'TilEid') IS NULL
              AND EXISTS (SELECT 1 FROM rettskilde_noder n
                          WHERE n.rettskilde_id = k.hjemmel_rettskilde_id
                            AND n.eid = (k.avgrensning_paragrafspenn_json::jsonb -> 0 ->> 'FraEid'));
            SELECT count(*) INTO v_hjemmelsted FROM hjemmelsted_341;

            -- Et spenn i egen hjemmel som IKKE er ett punkt, kan ikke bli én hjemmel-eId uten å gjette.
            SELECT count(*) INTO v_ukonverterbare
            FROM strukturkanter k
            WHERE k.kategori IN ('M', 'I') AND k.hjemmel_rettskilde_id IS NOT NULL AND k.hjemmel_eid IS NULL
              AND k."Id" NOT IN (SELECT id FROM hjemmelsted_341)
              AND EXISTS (SELECT 1 FROM jsonb_array_elements(k.avgrensning_paragrafspenn_json::jsonb) e
                          JOIN rettskilde_noder n ON n.rettskilde_id = k.hjemmel_rettskilde_id
                           AND n.eid IN (e ->> 'FraEid', e ->> 'TilEid'));
            IF v_ukonverterbare > 0 THEN
                RAISE EXCEPTION 'Issue #341: % M-/I-kanter har et avgrensningsspenn i egen hjemmel som ikke er ett punkt — det kan ikke flyttes til hjemmel_eid uten å gjette. Rett dem for hånd og kjør migrasjonen på nytt.', v_ukonverterbare;
            END IF;
            -- Rapporteres, røres ikke: spenn i en annen rettskilde (ekte avgrensning) og M/I uten noe hjemmelssted.
            SELECT count(*) INTO v_utenfor_hjemmel FROM strukturkanter k
            WHERE k.kategori IN ('M', 'I') AND k.avgrensning_paragrafspenn_json <> '[]' AND k."Id" NOT IN (SELECT id FROM hjemmelsted_341);
            SELECT count(*) INTO v_uten_sted FROM strukturkanter k
            WHERE k.kategori IN ('M', 'I') AND k.hjemmel_rettskilde_id IS NOT NULL AND k.hjemmel_eid IS NULL
              AND k.avgrensning_paragrafspenn_json = '[]';

            -- ---- Typekonfigurasjonen: nye koder (logisk FK i det øyeblikket kantene peker på dem) ----
            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv, familie, fvl_kategori)
            VALUES (gen_random_uuid(), 'R', 'har_delegert_til', 'har delegert myndighet til {0}', 'har fått delegert myndighet fra {0}', 7, true, NULL, NULL),
                   (gen_random_uuid(), 'K', 'beslutning', 'har beslutningskompetanse {0}', '{0} har beslutningskompetanse overfor denne', 14, true, NULL, NULL),
                   (gen_random_uuid(), 'K', 'oppretting', 'har opprettingskompetanse {0}', '{0} har opprettingskompetanse overfor denne', 15, true, 'struktur', NULL),
                   (gen_random_uuid(), 'K', 'avvikling', 'har avviklingskompetanse {0}', '{0} har avviklingskompetanse overfor denne', 16, true, 'struktur', NULL),
                   (gen_random_uuid(), 'K', 'organisasjon', 'har organisasjonskompetanse {0}', '{0} har organisasjonskompetanse overfor denne', 17, true, 'struktur', NULL),
                   (gen_random_uuid(), 'K', 'oppnevning', 'har oppnevningskompetanse {0}', '{0} har oppnevningskompetanse overfor denne', 18, true, 'personell', NULL),
                   (gen_random_uuid(), 'K', 'utpeking', 'har utpekingskompetanse {0}', '{0} har utpekingskompetanse overfor denne', 19, true, 'personell', NULL),
                   (gen_random_uuid(), 'K', 'ansettelse', 'har ansettelseskompetanse {0}', '{0} har ansettelseskompetanse overfor denne', 20, true, 'personell', 'enkeltvedtak'),
                   (gen_random_uuid(), 'K', 'avsetting', 'har avsettingskompetanse {0}', '{0} har avsettingskompetanse overfor denne', 21, true, 'personell', NULL),
                   (gen_random_uuid(), 'K', 'instruksjon', 'har instruksjonskompetanse {0}', '{0} har instruksjonskompetanse overfor denne', 22, true, 'styring', 'ikke_vedtak'),
                   (gen_random_uuid(), 'K', 'samordning', 'har samordningskompetanse {0}', '{0} har samordningskompetanse overfor denne', 23, true, 'styring', 'ikke_vedtak'),
                   (gen_random_uuid(), 'K', 'delegering', 'har delegeringskompetanse {0}', '{0} har delegeringskompetanse overfor denne', 24, true, 'styring', NULL),
                   (gen_random_uuid(), 'K', 'godkjenning', 'har godkjenningskompetanse {0}', '{0} har godkjenningskompetanse overfor denne', 25, true, 'styring', NULL),
                   (gen_random_uuid(), 'K', 'samtykke', 'har samtykkekompetanse {0}', '{0} har samtykkekompetanse overfor denne', 26, true, 'styring', NULL),
                   (gen_random_uuid(), 'K', 'palegg', 'har påleggskompetanse {0}', '{0} har påleggskompetanse overfor denne', 27, true, 'styring', 'enkeltvedtak'),
                   (gen_random_uuid(), 'K', 'normgivning', 'har normgivningskompetanse {0}', '{0} har normgivningskompetanse overfor denne', 28, true, 'normgivning', NULL),
                   (gen_random_uuid(), 'K', 'tilsyn', 'har tilsynskompetanse {0}', '{0} har tilsynskompetanse overfor denne', 29, true, 'kontroll', 'ikke_vedtak'),
                   (gen_random_uuid(), 'K', 'revisjon', 'har revisjonskompetanse {0}', '{0} har revisjonskompetanse overfor denne', 30, true, 'kontroll', 'ikke_vedtak'),
                   (gen_random_uuid(), 'K', 'klage', 'har klagekompetanse {0}', '{0} har klagekompetanse overfor denne', 31, true, 'klage_overproving', NULL),
                   (gen_random_uuid(), 'K', 'omgjoring', 'har omgjøringskompetanse {0}', '{0} har omgjøringskompetanse overfor denne', 32, true, 'klage_overproving', NULL),
                   (gen_random_uuid(), 'K', 'overproving', 'har overprøvingskompetanse {0}', '{0} har overprøvingskompetanse overfor denne', 33, true, 'klage_overproving', NULL),
                   (gen_random_uuid(), 'K', 'stadfesting', 'har stadfestingskompetanse {0}', '{0} har stadfestingskompetanse overfor denne', 34, true, 'klage_overproving', NULL),
                   (gen_random_uuid(), 'K', 'vedtak', 'har vedtakskompetanse {0}', '{0} har vedtakskompetanse overfor denne', 35, true, 'vedtak', 'enkeltvedtak'),
                   (gen_random_uuid(), 'K', 'sanksjon', 'har sanksjonskompetanse {0}', '{0} har sanksjonskompetanse overfor denne', 36, true, 'sanksjon', NULL),
                   (gen_random_uuid(), 'K', 'forelegging', 'har foreleggingskompetanse {0}', '{0} har foreleggingskompetanse overfor denne', 37, true, NULL, NULL)
            ON CONFLICT (kategori, kode) DO NOTHING;

            -- Familie og fvl-kategori på K-typene som fantes fra før (Johanns hierarkibeslutning; Strukturkanter.Kompetansetyper).
            UPDATE relasjonstype_konfigurasjon c SET familie = n.familie, fvl_kategori = n.fvl
            FROM (VALUES ('beslutning', NULL, NULL),
                ('oppretting', 'struktur', NULL),
                ('avvikling', 'struktur', NULL),
                ('organisasjon', 'struktur', NULL),
                ('oppnevning', 'personell', NULL),
                ('utpeking', 'personell', NULL),
                ('ansettelse', 'personell', 'enkeltvedtak'),
                ('avsetting', 'personell', NULL),
                ('instruksjon', 'styring', 'ikke_vedtak'),
                ('samordning', 'styring', 'ikke_vedtak'),
                ('delegering', 'styring', NULL),
                ('godkjenning', 'styring', NULL),
                ('samtykke', 'styring', NULL),
                ('palegg', 'styring', 'enkeltvedtak'),
                ('normgivning', 'normgivning', NULL),
                ('tilsyn', 'kontroll', 'ikke_vedtak'),
                ('revisjon', 'kontroll', 'ikke_vedtak'),
                ('klage', 'klage_overproving', NULL),
                ('omgjoring', 'klage_overproving', NULL),
                ('overproving', 'klage_overproving', NULL),
                ('stadfesting', 'klage_overproving', NULL),
                ('vedtak', 'vedtak', 'enkeltvedtak'),
                ('sanksjon', 'sanksjon', NULL),
                ('forelegging', NULL, NULL))
                AS n(kode, familie, fvl)
            WHERE c.kategori = 'K' AND c.kode = n.kode;

            -- K-malene som fortsatt har startsettets tekst fra #311 («har klagekompetanse: {0}»), får «overfor»-formen.
            UPDATE relasjonstype_konfigurasjon c SET fra_visningsmal = n.fra, til_visningsmal = n.til
            FROM (VALUES
                ('vedtak', 'har vedtakskompetanse: {0}', 'har vedtakskompetanse {0}', '{0} har vedtakskompetanse overfor denne'),
                ('klage', 'har klagekompetanse: {0}', 'har klagekompetanse {0}', '{0} har klagekompetanse overfor denne'),
                ('tilsyn', 'har tilsynskompetanse: {0}', 'har tilsynskompetanse {0}', '{0} har tilsynskompetanse overfor denne'),
                ('delegering', 'kan delegere: {0}', 'har delegeringskompetanse {0}', '{0} har delegeringskompetanse overfor denne'),
                ('oppnevning', 'har oppnevningskompetanse: {0}', 'har oppnevningskompetanse {0}', '{0} har oppnevningskompetanse overfor denne'),
                ('instruksjon', 'har instruksjonskompetanse: {0}', 'har instruksjonskompetanse {0}', '{0} har instruksjonskompetanse overfor denne'),
                ('utpeking', 'har utpekingskompetanse: {0}', 'har utpekingskompetanse {0}', '{0} har utpekingskompetanse overfor denne'),
                ('godkjenning', 'har godkjenningskompetanse: {0}', 'har godkjenningskompetanse {0}', '{0} har godkjenningskompetanse overfor denne'),
                ('overproving', 'har overprøvingskompetanse: {0}', 'har overprøvingskompetanse {0}', '{0} har overprøvingskompetanse overfor denne'))
                AS n(kode, gammel_fra, fra, til)
            WHERE c.kategori = 'K' AND c.kode = n.kode AND c.fra_visningsmal = n.gammel_fra;

            -- ---- Proveniens FØR endringen: de gamle verdiene (Down leser dem) ----
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'strukturkant', k."Id", 'migrasjon-341', now(), 'endret',
                jsonb_build_object('issue', 341, 'endring', 'kompetanse_med_motpart',
                    'fra_kategori', k.kategori, 'fra_typekode', k.typekode, 'fra_normform', k.normform,
                    'til_kategori', f.ny_kategori, 'til_typekode', f.ny_kode, 'til_normform', f.ny_normform)
            FROM strukturkanter k JOIN flytting_341 f ON k.kategori = f.gammel_kategori AND k.typekode = f.gammel_kode;

            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'strukturkant', h.id, 'migrasjon-341', now(), 'endret',
                jsonb_build_object('issue', 341, 'endring', 'hjemmelsted',
                    -- Som TEKST, ikke jsonb: Down skal skrive tilbake nøyaktig samme streng (idempotensen sammenligner den).
                    'gammelt_avgrensningsspenn', h.gammelt_spenn, 'ny_hjemmel_eid', h.eid)
            FROM hjemmelsted_341 h;

            -- ---- Konverteringen ----
            UPDATE strukturkanter k SET
                kategori = f.ny_kategori,
                typekode = f.ny_kode,
                normform = COALESCE(f.ny_normform, k.normform),
                sist_endret_av = 'migrasjon-341',
                sist_endret_tidspunkt = now()
            FROM flytting_341 f
            WHERE k.kategori = f.gammel_kategori AND k.typekode = f.gammel_kode;

            UPDATE strukturkanter k SET
                hjemmel_eid = h.eid,
                avgrensning_paragrafspenn_json = '[]',
                sist_endret_av = 'migrasjon-341',
                sist_endret_tidspunkt = now()
            FROM hjemmelsted_341 h
            WHERE k."Id" = h.id;

            -- ---- Etter ----
            SELECT count(*) INTO v_totalt_etter FROM strukturkanter;
            SELECT count(*) INTO v_igjen
            FROM strukturkanter k JOIN flytting_341 f ON k.kategori = f.gammel_kategori AND k.typekode = f.gammel_kode;
            SELECT string_agg(format('%s %s: %s etter, forventet %s', f.ny_kategori, f.ny_kode, e.antall, f.forventet), '; ')
            INTO v_avvik
            FROM (SELECT ny_kategori, ny_kode, min(antall_nye_for) + sum(antall) AS forventet
                  FROM flytting_341 GROUP BY ny_kategori, ny_kode) f
            CROSS JOIN LATERAL (SELECT count(*)::int AS antall FROM strukturkanter k
                                WHERE k.kategori = f.ny_kategori AND k.typekode = f.ny_kode) e
            WHERE e.antall <> f.forventet;
            SELECT count(*) INTO v_proveniens FROM proveniens p
            WHERE p.endret_av = 'migrasjon-341' AND p.handling = 'endret' AND p.kilde_referanser->>'issue' = '341';
            SELECT count(*) INTO v_feil_sted
            FROM hjemmelsted_341 h JOIN strukturkanter k ON k."Id" = h.id
            WHERE k.hjemmel_eid IS DISTINCT FROM h.eid OR k.avgrensning_paragrafspenn_json <> '[]';

            IF v_totalt_etter <> v_totalt_for OR v_igjen <> 0 OR v_avvik IS NOT NULL
               OR v_proveniens <> v_flyttet + v_hjemmelsted OR v_feil_sted <> 0 THEN
                RAISE EXCEPTION 'Issue #341: avvik etter konvertering — % kanter før, % etter; % med flyttet kode igjen; % proveniensrader for % flyttet + % hjemmelssted; % med feil hjemmelssted; per målkode: %. Avbrutt — ingen rader skal gå tapt.',
                    v_totalt_for, v_totalt_etter, v_igjen, v_proveniens, v_flyttet, v_hjemmelsted, v_feil_sted, coalesce(v_avvik, 'ok');
            END IF;

            -- ---- De flyttede og fjernede kodene ut av konfigurasjonen: ingen skrivevei kan bruke dem lenger ----
            DELETE FROM relasjonstype_konfigurasjon c
            WHERE (c.kategori, c.kode) IN (('R', 'klageinstans_for'), ('R', 'instruksjon'), ('R', 'omgjoring'), ('R', 'oppnevner'),
                                           ('R', 'delegerer_til'), ('K', 'forskrift'), ('K', 'iverksetting'));

            RAISE NOTICE 'Issue #341: % kanter før og etter; % flyttet til K (%); % M/I-kanter fikk hjemmelsstedet som hjemmel_eid; % M/I-kanter har avgrensningsspenn utenfor egen hjemmel (urørt); % M/I-kanter med hjemmel mangler hjemmelssted (urørt, synlig hull).',
                v_totalt_for, v_flyttet,
                (SELECT string_agg(format('%s %s → %s %s: %s', gammel_kategori, gammel_kode, ny_kategori, ny_kode, antall), ', ') FROM flytting_341),
                v_hjemmelsted, v_utenfor_hjemmel, v_uten_sted;
        END
        $do$;
        """;

    /// <summary>
    /// Snur NØYAKTIG de kantene <see cref="UpSql"/> endret (de med en <c>migrasjon-341</c>-proveniensrad) og legger de
    /// fjernede kodene inn igjen med malene fra før. Kanter registrert med de nye kodene ETTER migrasjonen røres ikke —
    /// å gjette en gammel kode for dem ville vært å finne opp data. En selvreguleringskant (normgivning der til = fra)
    /// kan ikke uttrykkes før #341; finnes en, avbrytes <c>Down</c> (CHECK-en som gjeninnføres ville veltet den).
    /// </summary>
    public const string DownSql = """
        DO $do$
        BEGIN
            IF EXISTS (SELECT 1 FROM strukturkanter k WHERE k.kategori = 'K' AND k.typekode = 'normgivning'
                       AND (k.fra_virksomhet_id = k.til_virksomhet_id OR k.fra_begrep_id = k.til_begrep_id)) THEN
                RAISE EXCEPTION 'Issue #341 Down: det finnes selvreguleringskanter (normgivning der til = fra), som ikke kan uttrykkes før #341. Slett dem for hånd først.';
            END IF;

            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
            VALUES (gen_random_uuid(), 'R', 'klageinstans_for', 'er klageinstans for {0}', 'har klageinstans hos {0}', 6, true),
                   (gen_random_uuid(), 'R', 'instruksjon', 'kan instruere {0}', 'kan instrueres av {0}', 8, true),
                   (gen_random_uuid(), 'R', 'omgjoring', 'kan omgjøre vedtak fra {0}', 'kan få vedtak omgjort av {0}', 9, true),
                   (gen_random_uuid(), 'R', 'oppnevner', 'oppnevner {0}', 'oppnevnes av {0}', 12, true),
                   (gen_random_uuid(), 'R', 'delegerer_til', 'delegerer myndighet til {0}', 'har fått delegert myndighet fra {0}', 18, true),
                   (gen_random_uuid(), 'K', 'forskrift', 'har forskriftskompetanse: {0}', 'forskriftskompetanse ligger hos {0}', 23, true),
                   (gen_random_uuid(), 'K', 'iverksetting', 'har iverksettingskompetanse: {0}', 'iverksettingskompetanse ligger hos {0}', 32, true)
            ON CONFLICT (kategori, kode) DO NOTHING;

            UPDATE relasjonstype_konfigurasjon c SET fra_visningsmal = n.gammel_fra, til_visningsmal = n.gammel_til
            FROM (VALUES
                ('vedtak', 'har vedtakskompetanse: {0}', 'vedtakskompetanse ligger hos {0}', 'har vedtakskompetanse {0}'),
                ('klage', 'har klagekompetanse: {0}', 'klagekompetanse ligger hos {0}', 'har klagekompetanse {0}'),
                ('tilsyn', 'har tilsynskompetanse: {0}', 'tilsynskompetanse ligger hos {0}', 'har tilsynskompetanse {0}'),
                ('delegering', 'kan delegere: {0}', 'delegeringsfullmakt ligger hos {0}', 'har delegeringskompetanse {0}'),
                ('oppnevning', 'har oppnevningskompetanse: {0}', 'oppnevningskompetanse ligger hos {0}', 'har oppnevningskompetanse {0}'),
                ('instruksjon', 'har instruksjonskompetanse: {0}', 'instruksjonskompetanse ligger hos {0}', 'har instruksjonskompetanse {0}'),
                ('utpeking', 'har utpekingskompetanse: {0}', 'utpekingskompetanse ligger hos {0}', 'har utpekingskompetanse {0}'),
                ('godkjenning', 'har godkjenningskompetanse: {0}', 'godkjenningskompetanse ligger hos {0}', 'har godkjenningskompetanse {0}'),
                ('overproving', 'har overprøvingskompetanse: {0}', 'overprøvingskompetanse ligger hos {0}', 'har overprøvingskompetanse {0}'))
                AS n(kode, gammel_fra, gammel_til, ny_fra)
            WHERE c.kategori = 'K' AND c.kode = n.kode AND c.fra_visningsmal = n.ny_fra;

            UPDATE strukturkanter k SET
                kategori = p.kilde_referanser->>'fra_kategori',
                typekode = p.kilde_referanser->>'fra_typekode',
                normform = p.kilde_referanser->>'fra_normform'
            FROM proveniens p
            WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-341' AND p.handling = 'endret'
              AND p.kilde_referanser->>'issue' = '341' AND p.kilde_referanser->>'endring' = 'kompetanse_med_motpart';

            UPDATE strukturkanter k SET
                hjemmel_eid = NULL,
                avgrensning_paragrafspenn_json = p.kilde_referanser->>'gammelt_avgrensningsspenn'
            FROM proveniens p
            WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-341' AND p.handling = 'endret'
              AND p.kilde_referanser->>'issue' = '341' AND p.kilde_referanser->>'endring' = 'hjemmelsted';

            DELETE FROM proveniens p
            WHERE p.endret_av = 'migrasjon-341' AND p.handling = 'endret' AND p.kilde_referanser->>'issue' = '341';

            -- De nye kodene ut igjen, men bare der ingen kant bruker dem (en kant registrert etter migrasjonen beholder sin).
            DELETE FROM relasjonstype_konfigurasjon c
            WHERE (c.kategori, c.kode) IN (('R', 'har_delegert_til'), ('K', 'omgjoring'), ('K', 'samtykke'), ('K', 'palegg'),
                                           ('K', 'normgivning'), ('K', 'organisasjon'), ('K', 'avsetting'), ('K', 'sanksjon'),
                                           ('K', 'forelegging'), ('K', 'beslutning'), ('K', 'oppretting'), ('K', 'avvikling'),
                                           ('K', 'ansettelse'), ('K', 'samordning'), ('K', 'revisjon'), ('K', 'stadfesting'))
              AND NOT EXISTS (SELECT 1 FROM strukturkanter k WHERE k.kategori = c.kategori AND k.typekode = c.kode);
        END
        $do$;
        """;
}
