namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #352 «Kompetansemodellen: rester etter #341», 2026-10-08] Data-delen av migrasjonen
/// <c>OppnevningsfamilienOgRester</c>, skilt ut som konstanter slik at <c>OppnevningMigreringTests</c> kjører nøyaktig den
/// SQL-en migrasjonen kjører — samme teknikk som <see cref="KompetanseMigrering"/> (#341) og
/// <see cref="RelasjonskodeHarmonisering"/> (#330).
/// <para>
/// <b>Hva den gjør (Johanns beslutninger på #352, 2026-10-08):</b>
/// <list type="number">
/// <item><b>Oppnevningsfamilien</b> (beslutning 1): R <c>velger</c>, K <c>utpeking</c> og K <c>ansettelse</c> blir K
/// <c>oppnevning</c> med <see cref="StrukturkantEntitet.Undertype">undertype</see> <c>valg</c>/<c>utpeking</c>/
/// <c>ansettelse</c> — verbet går ikke tapt. SAMME retning: fra = den som velger/utpeker/ansetter, til = den/det som
/// velges (motparten). Familien <c>personell</c> heter <c>oppnevning</c> i typekonfigurasjonen.</item>
/// <item><b>Anke</b> (beslutning 2): R <c>ankeinstans_for</c> blir K <c>overproving</c> med undertype <c>anke</c> (fra =
/// ankeinstansen, til = den hvis avgjørelser ankes — samme retning som <c>klageinstans_for</c> → <c>klage</c> i #341).
/// Undertypen <c>anke</c> er hovedøktens tolkning (Johann bekrefter i PR-en).</item>
/// <item><b>Forelegging</b> (beslutning 3) får familien <c>kontroll</c>.</item>
/// <item><b>Typekonfigurasjonen</b>: de flyttede kodene (R <c>velger</c>, R <c>ankeinstans_for</c>, K <c>utpeking</c>,
/// K <c>ansettelse</c>) slettes, så ingen skrivevei kan bruke dem.</item>
/// </list>
/// Hjemmel, avgrensning, polaritet, objekt, kilde og status røres ikke. Beslutning 4 (administrativt_underordnet, radgir,
/// oppretter og avvikler blir R) krever ingen dataendring.
/// </para>
/// <para>
/// <b>Tellingen</b> (samme mønster som #311/#330/#341): totalt antall likt før og etter; ingen kant med en flyttet kode
/// igjen; hver målkode = før + konverterte; hver flyttet kant har undertypen den gamle koden sa; ingen familie
/// <c>personell</c> igjen; én proveniensrad (<c>endret_av = 'migrasjon-352'</c>) per endret kant med de gamle verdiene —
/// det <c>Down</c> leser. Avviker noe: RAISE EXCEPTION. Målt lokalt 2026-10-08: 0 kanter med de fire kodene (R-kantene i
/// den lokale basen er sekretariat_for og etterfolger), så lokalt endrer migrasjonen bare konfigurasjonen.
/// </para>
/// <para>
/// <b>Bevisst IKKE her:</b> undertypen utledes ikke for kanter som ALT var K <c>oppnevning</c> før migrasjonen — basen har
/// ikke sitatet, og å gjette verbet ville vært å finne opp data (CLAUDE.md §8). Fasiten, som har sitatet, får undertypen av
/// skriptet <c>konvertering-352-oppnevning.py</c>. Leksikonregelen <c>vedtak-godkjennes-av</c> → godkjenning er et
/// leksikonspørsmål (mønsterlaget), ikke en dataendring her.
/// </para>
/// <para>
/// <b>[LÅST] Frosset sammen med migrasjonen.</b> En historisk migrasjon skal gjøre det samme i dag som den dag den ble
/// kjørt. Trengs en endring: ny migrasjon, ikke rediger denne.
/// </para>
/// </summary>
public static class OppnevningMigrering
{
    /// <summary>Hvem migrasjonen skriver som i <c>sist_endret_av</c> og proveniensens <c>endret_av</c>.</summary>
    public const string EndretAv = "migrasjon-352";

    /// <summary>Kodene som flyttes: (gammel kategori, gammel kode) → (ny kode, undertype); ny kategori er alltid K. SQL-en
    /// under har samme tabell som literal (frosset); testene bruker denne.</summary>
    public static readonly IReadOnlyList<(string GammelKategori, string GammelKode, string NyKode, string Undertype)> Flytting =
    [
        (Strukturkanter.Relasjon, "velger", Strukturkanter.Oppnevning, "valg"),
        (Strukturkanter.Kompetanse, "utpeking", Strukturkanter.Oppnevning, "utpeking"),
        (Strukturkanter.Kompetanse, "ansettelse", Strukturkanter.Oppnevning, "ansettelse"),
        (Strukturkanter.Relasjon, "ankeinstans_for", Strukturkanter.Overproving, "anke"),
    ];

    public const string UpSql = """
        DO $do$
        DECLARE
            v_totalt_for integer;
            v_totalt_etter integer;
            v_flyttet integer;
            v_igjen integer;
            v_avvik text;
            v_proveniens integer;
            v_feil_undertype integer;
            v_personell integer;
        BEGIN
            CREATE TEMP TABLE flytting_352 (
                gammel_kategori text NOT NULL, gammel_kode text NOT NULL, ny_kode text NOT NULL, undertype text NOT NULL,
                antall integer, antall_nye_for integer, PRIMARY KEY (gammel_kategori, gammel_kode)) ON COMMIT DROP;
            INSERT INTO flytting_352 (gammel_kategori, gammel_kode, ny_kode, undertype) VALUES
                ('R', 'velger', 'oppnevning', 'valg'),
                ('K', 'utpeking', 'oppnevning', 'utpeking'),
                ('K', 'ansettelse', 'oppnevning', 'ansettelse'),
                ('R', 'ankeinstans_for', 'overproving', 'anke');

            -- ---- Før ----
            UPDATE flytting_352 f SET
                antall = (SELECT count(*) FROM strukturkanter k WHERE k.kategori = f.gammel_kategori AND k.typekode = f.gammel_kode),
                antall_nye_for = (SELECT count(*) FROM strukturkanter k WHERE k.kategori = 'K' AND k.typekode = f.ny_kode);
            SELECT count(*) INTO v_totalt_for FROM strukturkanter;
            SELECT sum(antall) INTO v_flyttet FROM flytting_352;

            -- ---- Typekonfigurasjonen: familien personell heter oppnevning; forelegging er kontroll ----
            -- (CHECK-en på familie er droppet før denne SQL-en og legges inn igjen med 'oppnevning' etterpå.)
            UPDATE relasjonstype_konfigurasjon SET familie = 'oppnevning' WHERE kategori = 'K' AND familie = 'personell';
            UPDATE relasjonstype_konfigurasjon SET familie = 'kontroll' WHERE kategori = 'K' AND kode = 'forelegging';
            -- Målkodene må finnes før kantene peker på dem (logisk FK). De ble lagt inn av #341; ON CONFLICT for en base der
            -- noen er slettet for hånd.
            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv, familie, fvl_kategori)
            VALUES (gen_random_uuid(), 'K', 'oppnevning', 'har oppnevningskompetanse {0}', '{0} har oppnevningskompetanse overfor denne', 18, true, 'oppnevning', NULL),
                   (gen_random_uuid(), 'K', 'overproving', 'har overprøvingskompetanse {0}', '{0} har overprøvingskompetanse overfor denne', 33, true, 'klage_overproving', NULL)
            ON CONFLICT (kategori, kode) DO NOTHING;

            -- ---- Proveniens FØR endringen: de gamle verdiene (Down leser dem) ----
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'strukturkant', k."Id", 'migrasjon-352', now(), 'endret',
                jsonb_build_object('issue', 352, 'endring', 'oppnevningsfamilien',
                    'fra_kategori', k.kategori, 'fra_typekode', k.typekode, 'fra_undertype', k.undertype,
                    'til_kategori', 'K', 'til_typekode', f.ny_kode, 'til_undertype', f.undertype)
            FROM strukturkanter k JOIN flytting_352 f ON k.kategori = f.gammel_kategori AND k.typekode = f.gammel_kode;

            -- ---- Konverteringen ----
            UPDATE strukturkanter k SET
                kategori = 'K',
                typekode = f.ny_kode,
                undertype = f.undertype,
                sist_endret_av = 'migrasjon-352',
                sist_endret_tidspunkt = now()
            FROM flytting_352 f
            WHERE k.kategori = f.gammel_kategori AND k.typekode = f.gammel_kode;

            -- ---- Etter ----
            SELECT count(*) INTO v_totalt_etter FROM strukturkanter;
            SELECT count(*) INTO v_igjen
            FROM strukturkanter k JOIN flytting_352 f ON k.kategori = f.gammel_kategori AND k.typekode = f.gammel_kode;
            SELECT string_agg(format('K %s: %s etter, forventet %s', f.ny_kode, e.antall, f.forventet), '; ')
            INTO v_avvik
            FROM (SELECT ny_kode, min(antall_nye_for) + sum(antall) AS forventet FROM flytting_352 GROUP BY ny_kode) f
            CROSS JOIN LATERAL (SELECT count(*)::int AS antall FROM strukturkanter k
                                WHERE k.kategori = 'K' AND k.typekode = f.ny_kode) e
            WHERE e.antall <> f.forventet;
            SELECT count(*) INTO v_proveniens FROM proveniens p
            WHERE p.endret_av = 'migrasjon-352' AND p.handling = 'endret' AND p.kilde_referanser->>'issue' = '352';
            SELECT count(*) INTO v_feil_undertype
            FROM proveniens p JOIN strukturkanter k ON k."Id" = p.entitet_id
            WHERE p.endret_av = 'migrasjon-352' AND p.kilde_referanser->>'issue' = '352'
              AND (k.kategori <> 'K' OR k.typekode <> p.kilde_referanser->>'til_typekode'
                   OR k.undertype IS DISTINCT FROM p.kilde_referanser->>'til_undertype');
            SELECT count(*) INTO v_personell FROM relasjonstype_konfigurasjon WHERE familie = 'personell';

            IF v_totalt_etter <> v_totalt_for OR v_igjen <> 0 OR v_avvik IS NOT NULL OR v_proveniens <> v_flyttet
               OR v_feil_undertype <> 0 OR v_personell <> 0 THEN
                RAISE EXCEPTION 'Issue #352: avvik etter konvertering — % kanter før, % etter; % med flyttet kode igjen; % proveniensrader for % flyttet; % med feil type/undertype; % typer med familie personell; per målkode: %. Avbrutt — ingen rader skal gå tapt.',
                    v_totalt_for, v_totalt_etter, v_igjen, v_proveniens, v_flyttet, v_feil_undertype, v_personell, coalesce(v_avvik, 'ok');
            END IF;

            -- ---- De flyttede kodene ut av konfigurasjonen: ingen skrivevei kan bruke dem lenger ----
            -- Radene skrives til proveniens først, med alle verdiene, så Down legger inn igjen NØYAKTIG de som fantes (og
            -- ingen som ikke fantes) — i motsetning til #341 sin Down, som la inn faste maler.
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'relasjonstype_konfigurasjon', c."Id", 'migrasjon-352', now(), 'slettet',
                jsonb_build_object('issue', 352, 'endring', 'fjernet_kode', 'kategori', c.kategori, 'kode', c.kode,
                    'fra_visningsmal', c.fra_visningsmal, 'til_visningsmal', c.til_visningsmal,
                    'sorteringsrekkefolge', c.sorteringsrekkefolge, 'aktiv', c.aktiv, 'familie', c.familie, 'fvl_kategori', c.fvl_kategori)
            FROM relasjonstype_konfigurasjon c
            WHERE (c.kategori, c.kode) IN (('R', 'velger'), ('R', 'ankeinstans_for'), ('K', 'utpeking'), ('K', 'ansettelse'));
            DELETE FROM relasjonstype_konfigurasjon c
            WHERE (c.kategori, c.kode) IN (('R', 'velger'), ('R', 'ankeinstans_for'), ('K', 'utpeking'), ('K', 'ansettelse'));

            RAISE NOTICE 'Issue #352: % kanter før og etter; % flyttet til K (%); familien personell heter oppnevning; forelegging er kontroll.',
                v_totalt_for, v_flyttet,
                (SELECT string_agg(format('%s %s → K %s/%s: %s', gammel_kategori, gammel_kode, ny_kode, undertype, antall), ', ') FROM flytting_352);
        END
        $do$;
        """;

    /// <summary>
    /// Snur NØYAKTIG de kantene <see cref="UpSql"/> endret (de med en <c>migrasjon-352</c>-proveniensrad), setter familien
    /// tilbake til <c>personell</c> og forelegging til NULL, og legger inn igjen NØYAKTIG de konfigurasjonsradene Up slettet
    /// (samme Id, maler, rekkefølge og fvl-kategori — lest fra proveniensen), ingen andre.
    /// En undertype registrert ETTER migrasjonen kan ikke uttrykkes før #352 (kolonnen fjernes); finnes en, avbrytes
    /// <c>Down</c> i stedet for å kaste opplysningen stille.
    /// </summary>
    public const string DownSql = """
        DO $do$
        BEGIN
            IF EXISTS (SELECT 1 FROM strukturkanter k WHERE k.undertype IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM proveniens p WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-352'
                                       AND p.kilde_referanser->>'issue' = '352')) THEN
                RAISE EXCEPTION 'Issue #352 Down: det finnes kanter med undertype registrert etter migrasjonen, som ikke kan uttrykkes før #352. Rett dem for hånd først.';
            END IF;

            -- De slettede kodene tilbake med sin egen Id og sine egne verdier (fra proveniensen Up skrev).
            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv, familie, fvl_kategori)
            SELECT p.entitet_id, r->>'kategori', r->>'kode', r->>'fra_visningsmal', r->>'til_visningsmal',
                   (r->>'sorteringsrekkefolge')::integer, (r->>'aktiv')::boolean, r->>'familie', r->>'fvl_kategori'
            FROM proveniens p CROSS JOIN LATERAL (SELECT p.kilde_referanser AS r) x
            WHERE p.endret_av = 'migrasjon-352' AND p.handling = 'slettet' AND p.entitet_type = 'relasjonstype_konfigurasjon'
              AND p.kilde_referanser->>'issue' = '352'
            ON CONFLICT (kategori, kode) DO NOTHING;

            UPDATE strukturkanter k SET
                kategori = p.kilde_referanser->>'fra_kategori',
                typekode = p.kilde_referanser->>'fra_typekode',
                undertype = NULL
            FROM proveniens p
            WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-352' AND p.handling = 'endret'
              AND p.kilde_referanser->>'issue' = '352' AND p.kilde_referanser->>'endring' = 'oppnevningsfamilien';

            DELETE FROM proveniens p
            WHERE p.endret_av = 'migrasjon-352' AND p.handling IN ('endret', 'slettet') AND p.kilde_referanser->>'issue' = '352';

            UPDATE relasjonstype_konfigurasjon SET familie = 'personell' WHERE kategori = 'K' AND familie = 'oppnevning';
            UPDATE relasjonstype_konfigurasjon SET familie = NULL WHERE kategori = 'K' AND kode = 'forelegging';
        END
        $do$;
        """;
}
