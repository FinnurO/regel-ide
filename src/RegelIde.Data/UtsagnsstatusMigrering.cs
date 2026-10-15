namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #367 «utsagnsstatus» + #335 «delegeringsadgang», Johanns beslutninger 2026-10-09 og 2026-10-15, docs/34 §3–5] Data-delen av
/// migrasjonen <c>UtsagnsstatusOgDelegeringsadgang</c>, skilt ut som konstanter slik at <c>UtsagnsstatusMigreringTests</c> kjører nøyaktig
/// den SQL-en migrasjonen kjører — samme teknikk som <see cref="AvslutningMigrering"/> (#355), <see cref="PliktMigrering"/> (#353),
/// <see cref="OppnevningMigrering"/> (#352) og <see cref="KompetanseMigrering"/> (#341).
/// <para>
/// <b>Hva den gjør</b> (skjemaendringene står i selve migrasjonen):
/// <list type="number">
/// <item><b>Godkjenning</b> (AC2): <c>foreslatt_av_ai</c> → <c>maskinelt_foreslatt</c>, <c>validert</c> → <c>godkjent</c>. Én-til-én, så
/// <c>Down</c> snur den uten proveniens.</item>
/// <item><b>Kunnskapsstatus</b> (AC1): ingen eksisterende rad får en verdi (CLAUDE.md §8). Constraint-triggeren
/// <c>tr_strukturkanter_avledet_premiss</c> (utsatt til commit) krever minst én premiss for <c>avledet</c>.</item>
/// <item><b>Versjonert hjemmel</b> (AC4): <c>hjemmel_node_id</c> = noden med kantens <c>hjemmel_eid</c> i kantens egen hjemmel-rettskilde
/// (den versjonen kanten ble laget mot), bare når nøyaktig én gjeldende node treffer. Ellers NULL — visningen sier «hjemmelversjon ukjent».</item>
/// <item><b>Delegeringsadgang</b> (AC5, #335): <c>delegerbar</c> (bool) mappes på HJEMMELNODENS TEKST (noden og undernodene), ikke på
/// verdien alene: false + «kan ikke delegeres» eller «X selv» → <c>uttrykkelig_forbudt</c>; false + «Kongen i statsråd» →
/// <c>ikke_regulert</c> + <c>trolig_ikke_delegerbar</c>; true + «kan delegere» → <c>uttrykkelig_tillatt</c>; true + «Kongen» (uten
/// «i statsråd») → <c>ikke_regulert</c> + <c>trolig_delegerbar</c>. Alt annet avgjøres ikke: NULL, og id-ene listes i NOTICE. Den gamle
/// verdien står i proveniensen for hver rad (<c>Down</c> leser den). Målt lokalt 2026-10-15: 0 rader med delegerbar.</item>
/// <item><b>«Kongen» som egen navneform</b> (#335 AC1): finnes navneformen «Kongen i statsråd» (nøyaktig én gjeldende), får virksomheten den
/// en ny navneform «Kongen», og taggene med <c>quote_exact = 'Kongen'</c> på «Kongen i statsråd» flyttes dit — avgjort på teksten, uten
/// gjetting. (Den arkiverte «kongen»-raden fra #310 røres ikke.)</item>
/// <item><b>fvl-kategoriens gyldighet</b> (AC6): typene med fvl-kategori får hjemmelen «forvaltningsloven (LOV-1967-02-10) § 2» og
/// gjelder_fra = lovens ikrafttredelse slik korpuset har den (nøyaktig én gjeldende primærkilde, ellers NULL). gjelder_til står tom: den
/// nye loven er ikke i kraft.</item>
/// </list>
/// </para>
/// <para>
/// <b>Tellingen:</b> antall kanter likt før og etter; statusene summerer likt (foreslatt_av_ai = maskinelt_foreslatt, validert = godkjent);
/// én proveniensrad per kant med delegerbar; taggene på de to navneformene summerer likt. Avviker noe: RAISE EXCEPTION.
/// </para>
/// <para><b>[LÅST] Frosset sammen med migrasjonen.</b> Trengs en endring: ny migrasjon.</para>
/// </summary>
public static class UtsagnsstatusMigrering
{
    public const string EndretAv = "migrasjon-367";

    /// <summary>CHECK-ene før #367 (Down legger dem tilbake).</summary>
    public const string StatusCheckFor = "status IN ('foreslatt_av_ai', 'validert')";
    public const string DelegerbarCheckFor = "delegerbar IS NULL OR kategori = 'K'";

    public const string UpSql = """
        DO $do$
        DECLARE
            v_totalt_for integer; v_totalt_etter integer;
            v_ai_for integer; v_val_for integer; v_maskinelt integer; v_godkjent integer; v_ukjent integer;
            v_deleg_for integer; v_deleg_prov integer; v_deleg_uavklart text;
            v_med_eid integer; v_node_satt integer;
            v_kis uuid; v_kis_antall integer; v_virksomhet uuid; v_kongen uuid;
            v_tagger_for integer; v_tagger_etter integer; v_flyttet integer := 0;
            v_fvl_fra date; v_fvl_antall integer; v_fvl_typer integer;
        BEGIN
            -- ---- Før ----
            SELECT count(*) INTO v_totalt_for FROM strukturkanter;
            SELECT count(*) FILTER (WHERE status = 'foreslatt_av_ai'), count(*) FILTER (WHERE status = 'validert'),
                   count(*) FILTER (WHERE status NOT IN ('foreslatt_av_ai', 'validert'))
              INTO v_ai_for, v_val_for, v_ukjent FROM strukturkanter;
            IF v_ukjent <> 0 THEN
                RAISE EXCEPTION 'Issue #367: % kanter har en status utenfor (foreslatt_av_ai, validert). Ingenting er endret.', v_ukjent;
            END IF;
            SELECT count(*) INTO v_deleg_for FROM strukturkanter WHERE delegerbar IS NOT NULL;
            SELECT count(*) INTO v_med_eid FROM strukturkanter WHERE hjemmel_eid IS NOT NULL;

            -- ---- 1. Godkjenning ----
            UPDATE strukturkanter SET status = CASE status WHEN 'foreslatt_av_ai' THEN 'maskinelt_foreslatt' ELSE 'godkjent' END;

            -- ---- 2. Versjonert hjemmel: noden for eId-en i kantens egen hjemmel-rettskilde, bare ved nøyaktig ett treff ----
            UPDATE strukturkanter k SET hjemmel_node_id = n."Id"
            FROM rettskilde_noder n
            WHERE k.hjemmel_eid IS NOT NULL AND n.rettskilde_id = k.hjemmel_rettskilde_id AND n.eid = k.hjemmel_eid
              AND n.entitetsstatus = 'gjeldende'
              AND (SELECT count(*) FROM rettskilde_noder m WHERE m.rettskilde_id = k.hjemmel_rettskilde_id AND m.eid = k.hjemmel_eid
                     AND m.entitetsstatus = 'gjeldende') = 1;
            SELECT count(*) INTO v_node_satt FROM strukturkanter WHERE hjemmel_node_id IS NOT NULL;

            -- ---- 3. Delegeringsadgang fra delegerbar, avgjort på hjemmelnodens tekst (noden + undernodene) ----
            CREATE TEMP TABLE t367_deleg ON COMMIT DROP AS
            SELECT k."Id" AS id, k.delegerbar,
                   (SELECT string_agg(coalesce(n.tekst, ''), ' ' ORDER BY n.sorteringsrekkefolge) FROM rettskilde_noder n
                     WHERE n.rettskilde_id = k.hjemmel_rettskilde_id AND n.entitetsstatus = 'gjeldende'
                       AND (n.eid = k.hjemmel_eid OR n.eid LIKE k.hjemmel_eid || '/%')) AS tekst,
                   NULL::text AS adgang, NULL::text AS slutning
            FROM strukturkanter k WHERE k.delegerbar IS NOT NULL;
            UPDATE t367_deleg SET adgang = CASE
                    WHEN delegerbar = false AND (tekst ~* 'kan ikke delegeres' OR tekst ~ '\m\w+ selv\M(?!\s+om\M)') THEN 'uttrykkelig_forbudt'
                    WHEN delegerbar = false AND tekst LIKE '%Kongen i statsråd%' THEN 'ikke_regulert'
                    WHEN delegerbar = true AND tekst ~* 'kan delegere' THEN 'uttrykkelig_tillatt'
                    WHEN delegerbar = true AND tekst ~ '\mKongen\M' AND tekst NOT LIKE '%Kongen i statsråd%' THEN 'ikke_regulert'
                END;
            UPDATE t367_deleg SET slutning = CASE WHEN delegerbar THEN 'trolig_delegerbar' ELSE 'trolig_ikke_delegerbar' END
            WHERE adgang = 'ikke_regulert';
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'strukturkant', d.id, 'migrasjon-367', now(), 'endret',
                   jsonb_build_object('issue', 367, 'felt', 'delegerbar', 'delegerbar', d.delegerbar,
                                      'delegeringsadgang', d.adgang, 'delegering_slutning', d.slutning)
            FROM t367_deleg d;
            UPDATE strukturkanter k SET delegeringsadgang = d.adgang, delegering_slutning = d.slutning
            FROM t367_deleg d WHERE k."Id" = d.id;
            SELECT count(*) INTO v_deleg_prov FROM proveniens p
            WHERE p.endret_av = 'migrasjon-367' AND p.entitet_type = 'strukturkant' AND p.kilde_referanser->>'felt' = 'delegerbar';
            SELECT string_agg(d.id::text, ', ' ORDER BY d.id) INTO v_deleg_uavklart FROM t367_deleg d WHERE d.adgang IS NULL;

            -- ---- 4. «Kongen» som egen navneform for virksomheten Kongen i statsråd (#335 AC1) ----
            SELECT count(*), min(b."Id"::text)::uuid INTO v_kis_antall, v_kis FROM begreper b
            WHERE b.begrepskategori = 'virksomhet' AND b.term = 'Kongen i statsråd' AND b.entitetsstatus = 'gjeldende';
            IF v_kis_antall = 1 THEN
                SELECT virksomhet_referanse_id INTO v_virksomhet FROM begreper WHERE "Id" = v_kis;
                SELECT count(*) INTO v_tagger_for FROM tekst_tagger WHERE kind = 'virksomhet' AND ref_id = v_kis;
                IF v_virksomhet IS NOT NULL AND NOT EXISTS (SELECT 1 FROM begreper b WHERE b.begrepskategori = 'virksomhet'
                        AND b.virksomhet_referanse_id = v_virksomhet AND b.entitetsstatus = 'gjeldende' AND lower(b.term) = 'kongen') THEN
                    v_kongen := gen_random_uuid();
                    INSERT INTO begreper ("Id", term, gjelder_for, status, versjon, entitetsstatus, opprettet_av, opprettet_tidspunkt,
                                          begrepskategori, virksomhet_referanse_id)
                    VALUES (v_kongen, 'Kongen', '{}', 'publisert', 1, 'gjeldende', 'migrasjon-367', now(), 'virksomhet', v_virksomhet);
                    INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
                    VALUES (gen_random_uuid(), 'begrep', v_kongen, 'migrasjon-367', now(), 'opprettet',
                            jsonb_build_object('issue', 367, 'merknad', 'navneform «Kongen» skilt ut fra «Kongen i statsråd» (#335 AC1)',
                                               'fra_navneform', v_kis));
                    UPDATE tekst_tagger SET ref_id = v_kongen WHERE kind = 'virksomhet' AND ref_id = v_kis AND quote_exact = 'Kongen';
                    GET DIAGNOSTICS v_flyttet = ROW_COUNT;
                    SELECT count(*) INTO v_tagger_etter FROM tekst_tagger WHERE kind = 'virksomhet' AND ref_id IN (v_kis, v_kongen);
                    IF v_tagger_etter <> v_tagger_for THEN
                        RAISE EXCEPTION 'Issue #367: taggene på navneformene summerer ikke (% før, % etter). Avbrutt.', v_tagger_for, v_tagger_etter;
                    END IF;
                END IF;
            END IF;

            -- ---- 5. fvl-kategoriens gyldighet (AC6) ----
            SELECT count(*), min(r.ikrafttredelse) INTO v_fvl_antall, v_fvl_fra FROM rettskilder r
            WHERE r.eli = 'https://lovdata.no/eli/lov/1967/02/10/nor' AND r.entitetsstatus = 'gjeldende' AND r.importrolle = 'primaer';
            UPDATE relasjonstype_konfigurasjon SET fvl_kategori_hjemmel = 'forvaltningsloven (LOV-1967-02-10) § 2',
                   fvl_kategori_gjelder_fra = CASE WHEN v_fvl_antall = 1 THEN v_fvl_fra END
            WHERE fvl_kategori IS NOT NULL;
            GET DIAGNOSTICS v_fvl_typer = ROW_COUNT;

            -- ---- Etter ----
            SELECT count(*) INTO v_totalt_etter FROM strukturkanter;
            SELECT count(*) FILTER (WHERE status = 'maskinelt_foreslatt'), count(*) FILTER (WHERE status = 'godkjent')
              INTO v_maskinelt, v_godkjent FROM strukturkanter;
            IF v_totalt_etter <> v_totalt_for OR v_maskinelt <> v_ai_for OR v_godkjent <> v_val_for OR v_deleg_prov <> v_deleg_for THEN
                RAISE EXCEPTION 'Issue #367: avvik — % kanter før, % etter; foreslatt_av_ai % → maskinelt_foreslatt %; validert % → godkjent %; % proveniensrader for % kanter med delegerbar. Avbrutt.',
                    v_totalt_for, v_totalt_etter, v_ai_for, v_maskinelt, v_val_for, v_godkjent, v_deleg_prov, v_deleg_for;
            END IF;

            RAISE NOTICE 'Issue #367: % kanter før og etter. Godkjenning: foreslatt_av_ai % → maskinelt_foreslatt %, validert % → godkjent %. Kunnskapsstatus: ingen satt (ikke vurdert).',
                v_totalt_for, v_ai_for, v_maskinelt, v_val_for, v_godkjent;
            RAISE NOTICE 'Issue #367: hjemmel_node_id satt på % av % kanter med hjemmel-eId (% uten: hjemmelversjon ukjent); % kanter uten hjemmel-eId (kilde utenfor korpus).',
                v_node_satt, v_med_eid, v_med_eid - v_node_satt, v_totalt_for - v_med_eid;
            RAISE NOTICE 'Issue #367: delegerbar på % kanter → delegeringsadgang (proveniens per rad). Ikke avgjort uten å gjette: %.',
                v_deleg_for, coalesce(v_deleg_uavklart, 'ingen');
            RAISE NOTICE 'Issue #367: navneform «Kongen»: % (flyttet % tagger fra «Kongen i statsråd»). fvl-kategori: % typer, gjelder_fra %.',
                coalesce(v_kongen::text, 'ikke opprettet'), v_flyttet, v_fvl_typer, coalesce(v_fvl_fra::text, 'ukjent');
        END
        $do$;

        -- ---- Kunnskapsstatus avledet krever minst én premiss: constraint-trigger, utsatt til commit ----
        CREATE FUNCTION fn_strukturkanter_avledet_premiss() RETURNS trigger LANGUAGE plpgsql AS $fn$
        DECLARE v_kant uuid;
        BEGIN
            -- IF, ikke CASE: plpgsql slår opp feltene i begge grenene, og NEW/OLD har ulike felt i de to tabellene.
            IF TG_TABLE_NAME = 'strukturkanter' THEN
                v_kant := NEW."Id";
            ELSE
                v_kant := OLD.kant_id;
            END IF;
            IF EXISTS (SELECT 1 FROM strukturkanter k WHERE k."Id" = v_kant AND k.kunnskapsstatus = 'avledet')
               AND NOT EXISTS (SELECT 1 FROM strukturkant_premisser p WHERE p.kant_id = v_kant) THEN
                RAISE EXCEPTION 'Kant % har kunnskapsstatus avledet uten premisser (issue #367: avledet krever minst én premiss).', v_kant
                    USING ERRCODE = '23514';
            END IF;
            RETURN NULL;
        END
        $fn$;
        CREATE CONSTRAINT TRIGGER tr_strukturkanter_avledet_premiss AFTER INSERT OR UPDATE OF kunnskapsstatus ON strukturkanter
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fn_strukturkanter_avledet_premiss();
        CREATE CONSTRAINT TRIGGER tr_strukturkant_premisser_avledet AFTER DELETE ON strukturkant_premisser
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fn_strukturkanter_avledet_premiss();
        """;

    /// <summary>
    /// Snur NØYAKTIG det <see cref="UpSql"/> gjorde: delegerbar fra proveniensen, statusene tilbake, taggene tilbake til «Kongen i statsråd»
    /// og navneformen «Kongen» ut. Finnes det opplysninger som ikke kan uttrykkes før #367 (nye godkjenningsverdier, kunnskapsstatus,
    /// begrunnelse, tolkningsargumenter, foreldede rader, premisser, profilversjon, delegeringsadgang satt etter migrasjonen), avbrytes
    /// <c>Down</c> i stedet for å kaste dem stille. (Hjemmel-endret-flagget og hjemmelnoden kan regnes ut igjen og droppes.)
    /// Forutsetter at kolonnen <c>delegerbar</c> er lagt tilbake og den nye status-CHECK-en er droppet.
    /// </summary>
    public const string DownSql = """
        DO $do$
        BEGIN
            IF EXISTS (SELECT 1 FROM strukturkanter WHERE status IN ('manuelt_foreslatt', 'faglig_vurdert', 'avvist')
                        OR kunnskapsstatus IS NOT NULL OR begrunnelse IS NOT NULL OR tolkningsargumenter IS NOT NULL
                        OR entitetsstatus <> 'gjeldende' OR erstatter_kant_id IS NOT NULL OR profilversjon IS NOT NULL)
               OR EXISTS (SELECT 1 FROM strukturkant_premisser) THEN
                RAISE EXCEPTION 'Issue #367 Down: det finnes kanter med godkjenning, kunnskapsstatus, begrunnelse, tolkningsargumenter, revisjoner, premisser eller profilversjon som ikke kan uttrykkes før #367. Rett dem for hånd først.';
            END IF;
            IF EXISTS (SELECT 1 FROM strukturkanter k WHERE (k.delegeringsadgang IS NOT NULL OR k.delegering_slutning IS NOT NULL)
                        AND NOT EXISTS (SELECT 1 FROM proveniens p WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-367'
                                          AND p.kilde_referanser->>'felt' = 'delegerbar')) THEN
                RAISE EXCEPTION 'Issue #367 Down: det finnes kanter med delegeringsadgang satt etter migrasjonen. Den kan ikke uttrykkes som delegerbar uten å gjette. Rett dem for hånd først.';
            END IF;
        END
        $do$;

        DROP TRIGGER tr_strukturkant_premisser_avledet ON strukturkant_premisser;
        DROP TRIGGER tr_strukturkanter_avledet_premiss ON strukturkanter;
        DROP FUNCTION fn_strukturkanter_avledet_premiss();

        UPDATE strukturkanter k SET delegerbar = (p.kilde_referanser->>'delegerbar')::boolean
        FROM proveniens p
        WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-367' AND p.entitet_type = 'strukturkant'
          AND p.kilde_referanser->>'felt' = 'delegerbar';
        DELETE FROM proveniens p WHERE p.endret_av = 'migrasjon-367' AND p.entitet_type = 'strukturkant'
          AND p.kilde_referanser->>'felt' = 'delegerbar';

        UPDATE strukturkanter SET status = CASE status WHEN 'maskinelt_foreslatt' THEN 'foreslatt_av_ai' ELSE 'validert' END;

        UPDATE tekst_tagger t SET ref_id = (p.kilde_referanser->>'fra_navneform')::uuid
        FROM proveniens p
        WHERE p.endret_av = 'migrasjon-367' AND p.entitet_type = 'begrep' AND p.handling = 'opprettet' AND t.ref_id = p.entitet_id
          AND t.kind = 'virksomhet';
        DELETE FROM begreper b USING proveniens p
        WHERE p.endret_av = 'migrasjon-367' AND p.entitet_type = 'begrep' AND p.handling = 'opprettet' AND b."Id" = p.entitet_id;
        DELETE FROM proveniens p WHERE p.endret_av = 'migrasjon-367' AND p.entitet_type = 'begrep';
        """;
}
