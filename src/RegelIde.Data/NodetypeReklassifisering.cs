namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #310 «Strukturmodell 5: nodetype-akse», 2026-10-07] Data-delen av migrasjonen
/// <c>InnforNodetypeakse</c>, skilt ut som konstanter slik at testene (<c>NodetypeReklassifiseringTests</c>)
/// kan kjøre NØYAKTIG den SQL-en migrasjonen kjører mot rader de selv har lagt inn — en migrasjon kjøres
/// ellers bare én gang, mot en tom testbase, og logikken ville vært utestet.
/// <para>
/// <b>[LÅST] Frosset sammen med migrasjonen.</b> En historisk migrasjon skal gjøre det samme i dag som
/// den dag den ble kjørt. Trengs en endring, lag en NY migrasjon med egen SQL — ikke rediger denne.
/// </para>
/// <para>
/// <b>Reklassifiseringslista er Johanns godkjente liste</b> (issue #310, kommentar 2026-10-07: «godkjent
/// som foreslått»). Radene identifiseres på (Term case-insensitivt, lovkilde via rettskildens ELI eller
/// tittel) — ALDRI på Guid, fordi andre miljøer har andre Guid-er for samme begrep. En rad som ikke
/// finnes er en no-op, ikke en feil. Bare rader som fortsatt har <c>begrepskategori = 'gruppe'</c> og
/// <c>entitetsstatus = 'gjeldende'</c> berøres, så skriptet er idempotent (andre kjøring endrer ingenting).
/// </para>
/// <para>
/// <b>Sammenslåinger</b> («kongen» → «Kongen i statsråd», og de to statsforvalter-radene → én fast,
/// nasjonal klasse): ALLE koblinger flyttes til den overlevende raden FØR den andre arkiveres
/// (<c>entitetsstatus = 'arkivert'</c>, aldri DELETE) — tekst-tagger (<c>ref_id</c>),
/// myndighetstildelinger, gruppemedlemskap (begge retninger), definisjonsrelasjoner, vilkår og
/// begrepsforekomster. Unntaket er EKSAKTE dubletter, der den overlevende raden alt har samme kobling:
/// en dublett-tagg arkiveres (den unike indeksen <c>tekst_tagger_unik_tagg</c> tillater ikke to like),
/// og en dublett-tildeling/-medlemskap/-relasjon slettes — den bærer ingen opplysning den overlevende
/// raden ikke alt har. Et medlemskap som ville blitt selv-medlemskap (A medlem av A) slettes av samme
/// grunn (<c>ck_gruppe_medlemskap_ikke_selv</c>).
/// </para>
/// <para>
/// <b>«stortinget» (reindriftsloven) → organ:</b> finnes Stortinget som <see cref="Virksomhet"/>
/// (orgnr 971524960), blir begrepet en navneform (<c>'virksomhet'</c>) for den — eller slås sammen med en
/// eksisterende navneform «Stortinget» — og taggene flyttes til virksomhet-laget (samme regel som
/// migrasjonen <c>OmklassifiserNavneformTaggerTilVirksomhetslaget</c>). Finnes den ikke, blir begrepet et
/// <c>'organ'</c>-begrep: en virksomhetsrad med gjettede data opprettes IKKE (CLAUDE.md §8). Målt lokalt
/// 2026-10-07: Stortinget finnes ikke som virksomhet. Har begrepet tildelinger/medlemskap, gjøres det
/// heller ikke om til navneform (en navneform kan ikke bære dem) — da blir det organ.
/// </para>
/// </summary>
public static class NodetypeReklassifisering
{
    /// <summary>Hjelpefunksjoner i <c>pg_temp</c> (forsvinner med sesjonen, ryddes eksplisitt til slutt
    /// av <see cref="Sql"/>). Egen konstant fordi de MÅ finnes før DO-blokken kompileres.</summary>
    private const string Hjelpefunksjoner = """
        CREATE OR REPLACE FUNCTION pg_temp.nt310_lov(p_eli text, p_tittel text) RETURNS SETOF uuid
        LANGUAGE sql AS $f$
            SELECT r."Id" FROM rettskilder r WHERE r.eli = p_eli OR r.tittel = p_tittel
        $f$;

        CREATE OR REPLACE FUNCTION pg_temp.nt310_finn(p_term text, p_eli text, p_tittel text) RETURNS uuid
        LANGUAGE sql AS $f$
            SELECT b."Id" FROM begreper b
            WHERE b.begrepskategori = 'gruppe' AND b.entitetsstatus = 'gjeldende'
              AND lower(b.term) = lower(p_term)
              AND ((p_eli IS NULL AND p_tittel IS NULL AND b.lovkilde_id IS NULL)
                   OR b.lovkilde_id IN (SELECT pg_temp.nt310_lov(p_eli, p_tittel)))
            ORDER BY b.opprettet_tidspunkt, b."Id"
            LIMIT 1
        $f$;

        CREATE OR REPLACE FUNCTION pg_temp.nt310_proveniens(p_id uuid, p_handling text, p_merknad text) RETURNS void
        LANGUAGE sql AS $f$
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            VALUES (gen_random_uuid(), 'begrep', p_id, 'migrasjon-310', now(), p_handling,
                    jsonb_build_object('issue', 310, 'merknad', p_merknad))
        $f$;

        CREATE OR REPLACE FUNCTION pg_temp.nt310_sett(p_term text, p_eli text, p_tittel text, p_ny text) RETURNS integer
        LANGUAGE plpgsql AS $f$
        DECLARE
            v_id uuid;
            v_antall integer := 0;
        BEGIN
            FOR v_id IN
                SELECT b."Id" FROM begreper b
                WHERE b.begrepskategori = 'gruppe' AND b.entitetsstatus = 'gjeldende'
                  AND lower(b.term) = lower(p_term)
                  AND ((p_eli IS NULL AND p_tittel IS NULL AND b.lovkilde_id IS NULL)
                       OR b.lovkilde_id IN (SELECT pg_temp.nt310_lov(p_eli, p_tittel)))
            LOOP
                UPDATE begreper SET begrepskategori = p_ny, sist_endret_av = 'migrasjon-310',
                    sist_endret_tidspunkt = now()
                WHERE "Id" = v_id;
                PERFORM pg_temp.nt310_proveniens(v_id, 'endret', 'gruppe -> ' || p_ny);
                v_antall := v_antall + 1;
            END LOOP;
            RETURN v_antall;
        END
        $f$;

        CREATE OR REPLACE FUNCTION pg_temp.nt310_slaa_sammen(p_overlevende uuid, p_arkivert uuid) RETURNS void
        LANGUAGE plpgsql AS $f$
        BEGIN
            IF p_overlevende IS NULL OR p_arkivert IS NULL OR p_overlevende = p_arkivert THEN
                RETURN;
            END IF;

            -- Tekst-tagger: en eksakt dublett (samme eier/spenn/lag peker alt på den overlevende) kan ikke
            -- flyttes (tekst_tagger_unik_tagg) — den arkiveres og blir stående mot den arkiverte raden.
            UPDATE tekst_tagger t SET entitetsstatus = 'arkivert'
            WHERE t.ref_id = p_arkivert AND EXISTS (
                SELECT 1 FROM tekst_tagger u
                WHERE u.ref_id = p_overlevende AND u.virksomhet_id = t.virksomhet_id
                  AND u.rettskilde_id = t.rettskilde_id AND u.node_eid = t.node_eid
                  AND u.start_offset = t.start_offset AND u.end_offset = t.end_offset AND u.kind = t.kind);
            UPDATE tekst_tagger t SET ref_id = p_overlevende
            WHERE t.ref_id = p_arkivert AND NOT EXISTS (
                SELECT 1 FROM tekst_tagger u
                WHERE u.ref_id = p_overlevende AND u.virksomhet_id = t.virksomhet_id
                  AND u.rettskilde_id = t.rettskilde_id AND u.node_eid = t.node_eid
                  AND u.start_offset = t.start_offset AND u.end_offset = t.end_offset AND u.kind = t.kind);

            -- Myndighetstildelinger: eksakte dubletter slettes, resten flyttes.
            DELETE FROM myndighetstildelinger m
            WHERE m.gruppe_begrep_id = p_arkivert AND EXISTS (
                SELECT 1 FROM myndighetstildelinger n
                WHERE n.gruppe_begrep_id = p_overlevende AND n.virksomhet_id = m.virksomhet_id
                  AND n.hjemmel_rettskilde_id = m.hjemmel_rettskilde_id
                  AND n.paragrafspenn_json = m.paragrafspenn_json
                  AND n.vilkaar IS NOT DISTINCT FROM m.vilkaar
                  AND n.gyldig_fra IS NOT DISTINCT FROM m.gyldig_fra
                  AND n.gyldig_til IS NOT DISTINCT FROM m.gyldig_til);
            UPDATE myndighetstildelinger SET gruppe_begrep_id = p_overlevende WHERE gruppe_begrep_id = p_arkivert;

            -- Gruppemedlemskap, begge retninger: selv-medlemskap og par som alt finnes slettes (ux_gruppe_medlemskap_par).
            DELETE FROM gruppe_medlemskap g
            WHERE g.overordnet_gruppe_begrep_id = p_arkivert
              AND (g.underordnet_gruppe_begrep_id = p_overlevende OR EXISTS (
                  SELECT 1 FROM gruppe_medlemskap h
                  WHERE h.overordnet_gruppe_begrep_id = p_overlevende
                    AND h.underordnet_gruppe_begrep_id = g.underordnet_gruppe_begrep_id));
            UPDATE gruppe_medlemskap SET overordnet_gruppe_begrep_id = p_overlevende
            WHERE overordnet_gruppe_begrep_id = p_arkivert;
            DELETE FROM gruppe_medlemskap g
            WHERE g.underordnet_gruppe_begrep_id = p_arkivert
              AND (g.overordnet_gruppe_begrep_id = p_overlevende OR EXISTS (
                  SELECT 1 FROM gruppe_medlemskap h
                  WHERE h.underordnet_gruppe_begrep_id = p_overlevende
                    AND h.overordnet_gruppe_begrep_id = g.overordnet_gruppe_begrep_id));
            UPDATE gruppe_medlemskap SET underordnet_gruppe_begrep_id = p_overlevende
            WHERE underordnet_gruppe_begrep_id = p_arkivert;

            -- Definisjonsrelasjoner: samme regel (ux_begrep_def_relasjoner_fra_til, ck_..._ikke_selv).
            DELETE FROM begrep_definisjon_relasjoner d
            WHERE d.fra_begrep_id = p_arkivert
              AND (d.til_begrep_id = p_overlevende OR EXISTS (
                  SELECT 1 FROM begrep_definisjon_relasjoner e
                  WHERE e.fra_begrep_id = p_overlevende AND e.til_begrep_id = d.til_begrep_id));
            UPDATE begrep_definisjon_relasjoner SET fra_begrep_id = p_overlevende WHERE fra_begrep_id = p_arkivert;
            DELETE FROM begrep_definisjon_relasjoner d
            WHERE d.til_begrep_id = p_arkivert
              AND (d.fra_begrep_id = p_overlevende OR EXISTS (
                  SELECT 1 FROM begrep_definisjon_relasjoner e
                  WHERE e.til_begrep_id = p_overlevende AND e.fra_begrep_id = d.fra_begrep_id));
            UPDATE begrep_definisjon_relasjoner SET til_begrep_id = p_overlevende WHERE til_begrep_id = p_arkivert;

            -- Ingen unike skranker på disse — flyttes rett.
            UPDATE vilkar SET begrep_id = p_overlevende WHERE begrep_id = p_arkivert;
            UPDATE vilkar SET skjonnsgrunnlag_begrep_id = p_overlevende WHERE skjonnsgrunnlag_begrep_id = p_arkivert;
            UPDATE begrepsforekomster SET begrep_id = p_overlevende WHERE begrep_id = p_arkivert;

            UPDATE begreper SET entitetsstatus = 'arkivert', status = 'arkivert',
                sist_endret_av = 'migrasjon-310', sist_endret_tidspunkt = now()
            WHERE "Id" = p_arkivert;
            PERFORM pg_temp.nt310_proveniens(p_arkivert, 'arkivert', 'slatt sammen med ' || p_overlevende::text);
        END
        $f$;
        """;

    /// <summary>Selve reklassifiseringen — de 13 radene i issue #310 + <c>administrativ_inndeling</c> → <c>omrade</c>.</summary>
    private const string Reklassifisering = """
        DO $do$
        DECLARE
            hol_eli text := 'https://lovdata.no/eli/lov/2011/06/24/30/nor';
            hol_tittel text := 'Lov om kommunale helse- og omsorgstjenester m.m. (helse- og omsorgstjenesteloven)';
            samel_eli text := 'https://lovdata.no/eli/lov/1987/06/12/56/nor';
            samel_tittel text := 'Lov om Sametinget og andre samiske rettsforhold (sameloven)';
            reindrift_eli text := 'https://lovdata.no/eli/lov/2007/06/15/40/nor';
            reindrift_tittel text := 'Lov om reindrift (reindriftsloven)';
            enl_eli text := 'https://lovdata.no/eli/lov/1990/06/29/50/nor';
            enl_tittel text := 'Lov om produksjon, omforming, overføring, omsetning, fordeling og bruk av energi m.m. (energiloven)';
            ngl_eli text := 'https://lovdata.no/eli/lov/2002/06/28/61/nor';
            ngl_tittel text := 'Lov om felles regler for det indre marked for naturgass (naturgassloven)';
            vgf_eli text := 'https://lovdata.no/eli/forskrift/2013/02/15/201/nor';
            vgf_tittel text := 'Forskrift til vergemålsloven (vergemålsforskriften)';
            v_overlevende uuid;
            v_arkivert uuid;
            v_fast uuid;
            v_fast_bestemt uuid;
            v_reindrift uuid;
            v_vergemal uuid;
            v_stortinget uuid;
            v_virksomhet uuid;
            v_navneform uuid;
            v_id uuid;
        BEGIN
            -- Enkle reklassifiseringer (Johanns godkjente liste, issue #310).
            PERFORM pg_temp.nt310_sett('departementet', hol_eli, hol_tittel, 'rolle');
            PERFORM pg_temp.nt310_sett('forvaltningsområdet for samiske språk', samel_eli, samel_tittel, 'omrade');
            PERFORM pg_temp.nt310_sett('kommunene', reindrift_eli, reindrift_tittel, 'klasse');
            PERFORM pg_temp.nt310_sett('reguleringsmyndighet', enl_eli, enl_tittel, 'rolle');
            PERFORM pg_temp.nt310_sett('reguleringsmyndighet', ngl_eli, ngl_tittel, 'rolle');
            PERFORM pg_temp.nt310_sett('språkstimuleringskommuner', samel_eli, samel_tittel, 'klasse');
            PERFORM pg_temp.nt310_sett('språkutviklingskommuner', samel_eli, samel_tittel, 'klasse');
            PERFORM pg_temp.nt310_sett('språkvitaliseringskommuner', samel_eli, samel_tittel, 'klasse');

            -- «kongen» + «Kongen i statsråd» → ett fast organ-begrep. Overlevende: «Kongen i statsråd».
            -- Mangler den, blir «kongen» alene organ (godkjent som organ uansett).
            v_overlevende := pg_temp.nt310_finn('Kongen i statsråd', NULL, NULL);
            v_arkivert := pg_temp.nt310_finn('kongen', NULL, NULL);
            PERFORM pg_temp.nt310_slaa_sammen(v_overlevende, v_arkivert);
            PERFORM pg_temp.nt310_sett('Kongen i statsråd', NULL, NULL, 'organ');
            PERFORM pg_temp.nt310_sett('kongen', NULL, NULL, 'organ');

            -- «statsforvalter» (reindriftsloven) + «Statsforvalteren» (vergemålsforskriften) → én FAST,
            -- nasjonal klasse (docs/33 §4.2: de ti statsforvalterne). Overlevende: en fast rad hvis et
            -- miljø alt har den, ellers reindriftslovens «statsforvalter», ellers vergemålsforskriftens.
            v_fast := pg_temp.nt310_finn('statsforvalter', NULL, NULL);
            v_fast_bestemt := pg_temp.nt310_finn('statsforvalteren', NULL, NULL);
            v_reindrift := pg_temp.nt310_finn('statsforvalter', reindrift_eli, reindrift_tittel);
            v_vergemal := pg_temp.nt310_finn('statsforvalteren', vgf_eli, vgf_tittel);
            v_overlevende := COALESCE(v_fast, v_fast_bestemt, v_reindrift, v_vergemal);
            IF v_overlevende IS NOT NULL THEN
                PERFORM pg_temp.nt310_slaa_sammen(v_overlevende, v_fast_bestemt);
                PERFORM pg_temp.nt310_slaa_sammen(v_overlevende, v_reindrift);
                PERFORM pg_temp.nt310_slaa_sammen(v_overlevende, v_vergemal);
                UPDATE begreper SET begrepskategori = 'klasse', lovkilde_id = NULL,
                    sist_endret_av = 'migrasjon-310', sist_endret_tidspunkt = now()
                WHERE "Id" = v_overlevende;
                PERFORM pg_temp.nt310_proveniens(v_overlevende, 'endret', 'gruppe -> klasse (fast, nasjonal)');
            END IF;

            -- «stortinget» (reindriftsloven) → organ. Navneform for Stortinget-virksomheten hvis den finnes.
            v_stortinget := pg_temp.nt310_finn('stortinget', reindrift_eli, reindrift_tittel);
            IF v_stortinget IS NOT NULL THEN
                SELECT v."Id" INTO v_virksomhet FROM virksomheter v WHERE v.organisasjonsnummer = '971524960';
                IF v_virksomhet IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM myndighetstildelinger m WHERE m.gruppe_begrep_id = v_stortinget)
                   AND NOT EXISTS (SELECT 1 FROM gruppe_medlemskap g
                                   WHERE g.overordnet_gruppe_begrep_id = v_stortinget
                                      OR g.underordnet_gruppe_begrep_id = v_stortinget) THEN
                    SELECT b."Id" INTO v_navneform FROM begreper b
                    WHERE b.begrepskategori = 'virksomhet' AND b.virksomhet_referanse_id = v_virksomhet
                      AND b.entitetsstatus = 'gjeldende' AND lower(b.term) = 'stortinget'
                    ORDER BY b.opprettet_tidspunkt LIMIT 1;
                    IF v_navneform IS NOT NULL THEN
                        PERFORM pg_temp.nt310_slaa_sammen(v_navneform, v_stortinget);
                        v_id := v_navneform;
                    ELSE
                        UPDATE begreper SET begrepskategori = 'virksomhet', virksomhet_referanse_id = v_virksomhet,
                            lovkilde_id = NULL, sist_endret_av = 'migrasjon-310', sist_endret_tidspunkt = now()
                        WHERE "Id" = v_stortinget;
                        PERFORM pg_temp.nt310_proveniens(v_stortinget, 'endret', 'gruppe -> virksomhet (navneform for 971524960)');
                        v_id := v_stortinget;
                    END IF;
                    UPDATE tekst_tagger t SET kind = 'virksomhet'
                    WHERE t.ref_id = v_id AND t.kind = 'begrep' AND NOT EXISTS (
                        SELECT 1 FROM tekst_tagger u
                        WHERE u.ref_id = v_id AND u.kind = 'virksomhet' AND u.virksomhet_id = t.virksomhet_id
                          AND u.rettskilde_id = t.rettskilde_id AND u.node_eid = t.node_eid
                          AND u.start_offset = t.start_offset AND u.end_offset = t.end_offset);
                ELSE
                    PERFORM pg_temp.nt310_sett('stortinget', reindrift_eli, reindrift_tittel, 'organ');
                END IF;
            END IF;

            -- 'administrativ_inndeling' → 'omrade' (issue #310: «går inn i omrade»; 0 rader lokalt).
            FOR v_id IN SELECT b."Id" FROM begreper b WHERE b.begrepskategori = 'administrativ_inndeling' LOOP
                UPDATE begreper SET begrepskategori = 'omrade', sist_endret_av = 'migrasjon-310',
                    sist_endret_tidspunkt = now()
                WHERE "Id" = v_id;
                PERFORM pg_temp.nt310_proveniens(v_id, 'endret', 'administrativ_inndeling -> omrade');
            END LOOP;
            UPDATE navnekandidater SET kategori = 'omrade' WHERE kategori = 'administrativ_inndeling';
        END
        $do$;

        DROP FUNCTION IF EXISTS pg_temp.nt310_slaa_sammen(uuid, uuid);
        DROP FUNCTION IF EXISTS pg_temp.nt310_sett(text, text, text, text);
        DROP FUNCTION IF EXISTS pg_temp.nt310_proveniens(uuid, text, text);
        DROP FUNCTION IF EXISTS pg_temp.nt310_finn(text, text, text);
        DROP FUNCTION IF EXISTS pg_temp.nt310_lov(text, text);
        """;

    /// <summary>Hele data-delen for begrep/navnekandidater. Kjøres av migrasjonen mellom «dropp gamle
    /// skranker/indekser» og «legg til nye» — se migrasjonens kommentar for rekkefølgen.</summary>
    public const string Sql = Hjelpefunksjoner + "\n" + Reklassifisering;

    /// <summary>
    /// Automatisk utfylling av <see cref="Virksomhet.Aktortype"/> — speiler
    /// <see cref="Nodetyper.UtledAktortypeAutomatisk"/> (KOMM/FYLK, og forvaltningsnivået som seeden
    /// utleder fra nettopp KOMM/FYLK — se <see cref="Virksomhet.Aktortype"/> for målingen bak). Rører
    /// aldri en rad som alt har en verdi.
    /// </summary>
    public const string AktortypeSql = """
        UPDATE virksomheter SET aktortype = 'rettssubjekt'
        WHERE aktortype IS NULL
          AND (organisasjonsform_kode IN ('KOMM', 'FYLK') OR forvaltningsniva IN ('kommune', 'fylkeskommune'));
        """;
}
