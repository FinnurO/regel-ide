namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #330 «harmoniser gamle og nye relasjonskoder», 2026-10-08] Data-delen av migrasjonen
/// <c>HarmoniserRelasjonskoder</c>, skilt ut som konstanter slik at <c>RelasjonskodeHarmoniseringTests</c> kan
/// kjøre nøyaktig den SQL-en migrasjonen kjører — samme teknikk som <see cref="StrukturkantMigrering"/> (#311).
/// <para>
/// <b>Hvorfor:</b> #311 flyttet de fem R-kodene fra <c>virksomhet_relasjoner</c> inn i <c>strukturkanter</c> med
/// SAMME kode, ved siden av docs/33 §4.3-startsettet. Tre av dem leses MOTSATT vei av sin nye tvilling —
/// «X har klageinstans hos Y» (<c>klageinstans</c>) betyr det samme som «Y er klageinstans for X»
/// (<c>klageinstans_for</c>). Med begge i bruk ga «hvem er klageinstans for hvem?» (docs/32 S1/S2) to svar
/// avhengig av hvordan kanten ble registrert. Johann besluttet 2026-10-08 å harmonisere (issue #330).
/// </para>
/// <para>
/// <b>[LÅST] Mappingen er sakens tabell (issue #330, målt lokalt 2026-10-08):</b>
/// <list type="table">
/// <item><c>klageinstans</c> → R <c>klageinstans_for</c>, fra/til BYTTES (3 rader lokalt)</item>
/// <item><c>sekretariat</c> → R <c>sekretariat_for</c>, fra/til BYTTES (4)</item>
/// <item><c>oppgaver_overfort_til</c> → R <c>etterfolger</c>, fra/til BYTTES (2) — «A fikk oppgavene overført til
/// B» = «B etterfølger A»</item>
/// <item><c>enhet_i</c> → <b>G</b> <c>del_av</c>, samme retning (1) — «er enhet i» er organtilhørighet, ikke en
/// relasjon mellom to selvstendige aktører (docs/33 §4.3: G = organ/enhet/rolle → rettssubjekt)</item>
/// <item><c>underlagt</c> → R <c>administrativt_underordnet</c>, samme retning (0)</item>
/// </list>
/// </para>
/// <para>
/// Hver konverterte kant beholder id, hjemmel, avgrensning, kilde og status; bare kategori, typekode og (der det
/// står «byttes») endene endres. Hver får en proveniensrad (<c>handling = 'endret'</c>, <c>endret_av =
/// 'migrasjon-330'</c>) med gammel kode og de OPPRINNELIGE endene i <c>kilde_referanser</c> — det er den
/// <c>Down</c> leser for å snu nøyaktig de radene tilbake. De gamle kodene slettes til slutt fra
/// <c>relasjonstype_konfigurasjon</c> (og er fjernet fra <see cref="Strukturkanter.Startsett"/>, så oppstarts-
/// seeden ikke legger dem inn igjen), slik at ingen skrivevei kan bruke dem.
/// </para>
/// <para>
/// <b>Tellingen</b> (samme mønster som #311): totalt antall kanter skal være likt før og etter, ingen kant skal
/// ha en gammel kode igjen, hver målkode skal ha nøyaktig (før + konverterte), og hver konverterte kant skal ha
/// riktige ender målt mot proveniensraden. Avviker noe, avbrytes migrasjonen (RAISE EXCEPTION) i stedet for å
/// fullføre med tap.
/// </para>
/// <para>
/// <b>Bevisst IKKE her:</b> avgrensningen på Energiklagenemnda-raden (issue #330, Johanns rettelse 2026-10-08).
/// Den legges til gjennom API-et (<c>PUT /api/strukturkanter/{id}/avgrensning</c>) etter at Johann har bekreftet
/// den — en migrasjon skal ikke gjette innhold i én bestemt rad.
/// </para>
/// <para>
/// <b>[LÅST] Frosset sammen med migrasjonen.</b> En historisk migrasjon skal gjøre det samme i dag som den dag
/// den ble kjørt. Trengs en endring: ny migrasjon, ikke rediger denne.
/// </para>
/// </summary>
public static class RelasjonskodeHarmonisering
{
    /// <summary>Hvem migrasjonen skriver som i <c>sist_endret_av</c> og proveniensens <c>endret_av</c>.</summary>
    public const string EndretAv = "migrasjon-330";

    /// <summary>Sakens mappingtabell — se klassekommentaren. Brukes av testene; SQL-en under har den samme
    /// tabellen som literal (frosset).</summary>
    public static readonly IReadOnlyList<(string GammelKode, string NyKategori, string NyKode, bool Byttes)> Mapping =
    [
        ("klageinstans", Strukturkanter.Relasjon, "klageinstans_for", true),
        ("sekretariat", Strukturkanter.Relasjon, "sekretariat_for", true),
        ("oppgaver_overfort_til", Strukturkanter.Relasjon, "etterfolger", true),
        ("enhet_i", Strukturkanter.Organtilhorighet, "del_av", false),
        ("underlagt", Strukturkanter.Relasjon, "administrativt_underordnet", false),
    ];

    /// <summary>De fem gamle R-kodene. Ingen av dem skal finnes i <see cref="Strukturkanter.Startsett"/> eller kunne
    /// brukes av en skrivevei etter #330 — testene sjekker begge deler mot denne lista.</summary>
    public static readonly IReadOnlyList<string> GamleKoder = Mapping.Select(m => m.GammelKode).ToList();

    public const string UpSql = """
        DO $do$
        DECLARE
            v_totalt_for integer;
            v_totalt_etter integer;
            v_gamle integer;
            v_gamle_igjen integer;
            v_proveniens integer;
            v_feil_ender integer;
            v_avvik text;
            v_oppsummering text;
        BEGIN
            CREATE TEMP TABLE harmonisering_330 (
                gammel_kode text PRIMARY KEY, ny_kategori text NOT NULL, ny_kode text NOT NULL, byttes boolean NOT NULL,
                antall_gamle integer, antall_nye_for integer) ON COMMIT DROP;
            INSERT INTO harmonisering_330 (gammel_kode, ny_kategori, ny_kode, byttes) VALUES
                ('klageinstans', 'R', 'klageinstans_for', true),
                ('sekretariat', 'R', 'sekretariat_for', true),
                ('oppgaver_overfort_til', 'R', 'etterfolger', true),
                ('enhet_i', 'G', 'del_av', false),
                ('underlagt', 'R', 'administrativt_underordnet', false);

            -- ---- Før ----
            UPDATE harmonisering_330 h SET
                antall_gamle = (SELECT count(*) FROM strukturkanter k WHERE k.kategori = 'R' AND k.typekode = h.gammel_kode),
                antall_nye_for = (SELECT count(*) FROM strukturkanter k WHERE k.kategori = h.ny_kategori AND k.typekode = h.ny_kode);
            SELECT count(*) INTO v_totalt_for FROM strukturkanter;
            SELECT sum(antall_gamle) INTO v_gamle FROM harmonisering_330;

            -- Målkodene skal finnes i konfigurasjonen i det øyeblikket kantene peker på dem (logisk FK; i en fersk
            -- base seedes startsettet først ved oppstart, etter migrasjonene). Samme maler som Strukturkanter.Startsett.
            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
            VALUES (gen_random_uuid(), 'R', 'klageinstans_for', 'er klageinstans for {0}', 'har klageinstans hos {0}', 1, true),
                   (gen_random_uuid(), 'R', 'administrativt_underordnet', 'er administrativt underordnet {0}', 'er administrativt overordnet {0}', 2, true),
                   (gen_random_uuid(), 'R', 'sekretariat_for', 'er sekretariat for {0}', 'har sekretariat hos {0}', 5, true),
                   (gen_random_uuid(), 'R', 'etterfolger', 'etterfølger {0}', 'etterfølges av {0}', 11, true),
                   (gen_random_uuid(), 'G', 'del_av', 'er del av {0}', 'har som del {0}', 37, true)
            ON CONFLICT (kategori, kode) DO NOTHING;

            -- ---- Proveniens FØR endringen: gammel kode og de opprinnelige endene (Down leser dem) ----
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'strukturkant', k."Id", 'migrasjon-330', now(), 'endret',
                jsonb_build_object('issue', 330, 'harmonisering', 'relasjonskode',
                    'fra_kategori', k.kategori, 'fra_typekode', k.typekode,
                    'til_kategori', h.ny_kategori, 'til_typekode', h.ny_kode, 'retning_byttet', h.byttes,
                    'opprinnelig_fra_virksomhet_id', k.fra_virksomhet_id, 'opprinnelig_til_virksomhet_id', k.til_virksomhet_id,
                    'opprinnelig_fra_begrep_id', k.fra_begrep_id, 'opprinnelig_til_begrep_id', k.til_begrep_id)
            FROM strukturkanter k JOIN harmonisering_330 h ON k.kategori = 'R' AND k.typekode = h.gammel_kode;

            -- ---- Konverteringen. Alle uttrykk på høyre side leser radens GAMLE verdier, så byttet er ett steg. ----
            UPDATE strukturkanter k SET
                kategori = h.ny_kategori,
                typekode = h.ny_kode,
                fra_virksomhet_id = CASE WHEN h.byttes THEN k.til_virksomhet_id ELSE k.fra_virksomhet_id END,
                til_virksomhet_id = CASE WHEN h.byttes THEN k.fra_virksomhet_id ELSE k.til_virksomhet_id END,
                fra_begrep_id = CASE WHEN h.byttes THEN k.til_begrep_id ELSE k.fra_begrep_id END,
                til_begrep_id = CASE WHEN h.byttes THEN k.fra_begrep_id ELSE k.til_begrep_id END,
                sist_endret_av = 'migrasjon-330',
                sist_endret_tidspunkt = now()
            FROM harmonisering_330 h
            WHERE k.kategori = 'R' AND k.typekode = h.gammel_kode;

            -- ---- Etter ----
            SELECT count(*) INTO v_totalt_etter FROM strukturkanter;
            SELECT count(*) INTO v_gamle_igjen
            FROM strukturkanter k JOIN harmonisering_330 h ON k.kategori = 'R' AND k.typekode = h.gammel_kode;
            SELECT string_agg(format('%s %s: %s etter, forventet %s (%s før + %s konvertert)', h.ny_kategori, h.ny_kode,
                       e.antall, h.antall_nye_for + h.antall_gamle, h.antall_nye_for, h.antall_gamle), '; ')
            INTO v_avvik
            FROM harmonisering_330 h
            CROSS JOIN LATERAL (SELECT count(*)::int AS antall FROM strukturkanter k
                                WHERE k.kategori = h.ny_kategori AND k.typekode = h.ny_kode) e
            WHERE e.antall <> h.antall_nye_for + h.antall_gamle;
            SELECT count(*) INTO v_proveniens FROM proveniens p
            WHERE p.endret_av = 'migrasjon-330' AND p.handling = 'endret' AND p.kilde_referanser->>'issue' = '330';
            -- Endene målt mot proveniensraden: byttet der det skulle byttes, uendret ellers.
            SELECT count(*) INTO v_feil_ender
            FROM proveniens p JOIN strukturkanter k ON k."Id" = p.entitet_id
            WHERE p.endret_av = 'migrasjon-330' AND p.handling = 'endret' AND p.kilde_referanser->>'issue' = '330'
              AND NOT (
                CASE WHEN (p.kilde_referanser->>'retning_byttet')::boolean
                     THEN k.fra_virksomhet_id IS NOT DISTINCT FROM (p.kilde_referanser->>'opprinnelig_til_virksomhet_id')::uuid
                      AND k.til_virksomhet_id IS NOT DISTINCT FROM (p.kilde_referanser->>'opprinnelig_fra_virksomhet_id')::uuid
                      AND k.fra_begrep_id IS NOT DISTINCT FROM (p.kilde_referanser->>'opprinnelig_til_begrep_id')::uuid
                      AND k.til_begrep_id IS NOT DISTINCT FROM (p.kilde_referanser->>'opprinnelig_fra_begrep_id')::uuid
                     ELSE k.fra_virksomhet_id IS NOT DISTINCT FROM (p.kilde_referanser->>'opprinnelig_fra_virksomhet_id')::uuid
                      AND k.til_virksomhet_id IS NOT DISTINCT FROM (p.kilde_referanser->>'opprinnelig_til_virksomhet_id')::uuid
                      AND k.fra_begrep_id IS NOT DISTINCT FROM (p.kilde_referanser->>'opprinnelig_fra_begrep_id')::uuid
                      AND k.til_begrep_id IS NOT DISTINCT FROM (p.kilde_referanser->>'opprinnelig_til_begrep_id')::uuid
                END);

            IF v_totalt_etter <> v_totalt_for OR v_gamle_igjen <> 0 OR v_avvik IS NOT NULL
               OR v_proveniens <> v_gamle OR v_feil_ender <> 0 THEN
                RAISE EXCEPTION 'Issue #330: avvik etter konvertering — % kanter før, % etter; % med gammel kode igjen; % proveniensrader for % konverterte; % med feil ender; per målkode: %. Avbrutt — ingen rader skal gå tapt.',
                    v_totalt_for, v_totalt_etter, v_gamle_igjen, v_proveniens, v_gamle, v_feil_ender, coalesce(v_avvik, 'ok');
            END IF;

            -- ---- De gamle kodene fjernes fra konfigurasjonen: ingen skrivevei kan bruke dem lenger ----
            DELETE FROM relasjonstype_konfigurasjon c USING harmonisering_330 h
            WHERE c.kategori = 'R' AND c.kode = h.gammel_kode;

            SELECT string_agg(format('%s → %s %s: %s%s', h.gammel_kode, h.ny_kategori, h.ny_kode, h.antall_gamle,
                       CASE WHEN h.byttes THEN ' (byttet)' ELSE '' END), ', ' ORDER BY h.gammel_kode)
            INTO v_oppsummering FROM harmonisering_330 h;
            RAISE NOTICE 'Issue #330: % kanter før og etter; % konvertert (%).', v_totalt_for, v_gamle, v_oppsummering;
        END
        $do$;
        """;

    /// <summary>
    /// Snur NØYAKTIG de kantene <see cref="UpSql"/> konverterte (de med en <c>migrasjon-330</c>-proveniensrad)
    /// tilbake til gammel kategori/kode og opprinnelige ender, og legger de gamle kodene inn igjen i
    /// konfigurasjonen med malene de hadde. Kanter registrert med de nye kodene ETTER migrasjonen røres ikke —
    /// de fantes ikke i den gamle verdenen, og å gjette en gammel kode for dem ville vært å finne opp data.
    /// </summary>
    public const string DownSql = """
        DO $do$
        BEGIN
            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
            VALUES (gen_random_uuid(), 'R', 'underlagt', 'er underlagt {0}', 'er eier/overordnet for {0}', 0, true),
                   (gen_random_uuid(), 'R', 'sekretariat', 'har sekretariat hos {0}', 'er sekretariat for {0}', 1, true),
                   (gen_random_uuid(), 'R', 'klageinstans', 'har klageinstans hos {0}', 'er klageinstans for {0}', 2, true),
                   (gen_random_uuid(), 'R', 'enhet_i', 'er enhet i {0}', 'har enhet {0}', 3, true),
                   (gen_random_uuid(), 'R', 'oppgaver_overfort_til', 'fikk oppgavene overført til {0}', 'overtok oppgavene til {0}', 4, true)
            ON CONFLICT (kategori, kode) DO NOTHING;

            UPDATE strukturkanter k SET
                kategori = p.kilde_referanser->>'fra_kategori',
                typekode = p.kilde_referanser->>'fra_typekode',
                fra_virksomhet_id = (p.kilde_referanser->>'opprinnelig_fra_virksomhet_id')::uuid,
                til_virksomhet_id = (p.kilde_referanser->>'opprinnelig_til_virksomhet_id')::uuid,
                fra_begrep_id = (p.kilde_referanser->>'opprinnelig_fra_begrep_id')::uuid,
                til_begrep_id = (p.kilde_referanser->>'opprinnelig_til_begrep_id')::uuid
            FROM proveniens p
            WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-330' AND p.handling = 'endret'
              AND p.kilde_referanser->>'issue' = '330';

            DELETE FROM proveniens p
            WHERE p.endret_av = 'migrasjon-330' AND p.handling = 'endret' AND p.kilde_referanser->>'issue' = '330';
        END
        $do$;
        """;
}
