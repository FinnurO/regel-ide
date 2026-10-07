namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #311 «Strukturmodell 6: én typestyrt kanttabell», 2026-10-07] Data-delen av migrasjonen
/// <c>InnforStrukturkanttabell</c>, skilt ut som konstanter slik at <c>StrukturkantMigreringTests</c> kan
/// verifisere nøyaktig den SQL-en migrasjonen kjører — samme teknikk som <see cref="NodetypeReklassifisering"/>.
/// <para>
/// <b>[LÅST] Frosset sammen med migrasjonen.</b> En historisk migrasjon skal gjøre det samme i dag som den dag
/// den ble kjørt. Trengs en endring: ny migrasjon, ikke rediger denne.
/// </para>
/// </summary>
public static class StrukturkantMigrering
{
    /// <summary>
    /// Flytter ALLE rader fra de tre gamle tabellene 1:1 inn i <c>strukturkanter</c>, med SAMME id (issue
    /// #311 AC2: «migrert uten tap»):
    /// <list type="bullet">
    /// <item><c>virksomhet_relasjoner</c> → R med samme typekode. Kommentar UTEN hjemmel → kilde utenfor korpus
    /// (det var bruken: «bekreftet mot organisasjonskartet …»); kommentar MED hjemmel → kommentar.</item>
    /// <item><c>gruppe_medlemskap</c> → M <c>medlem_av</c>, fra = underordnet, til = overordnet.</item>
    /// <item><c>myndighetstildelinger</c> → I <c>innehar</c> når målet er en rolle (nodetypen fra #310), ellers
    /// M <c>medlem_av</c>; <c>vilkaar</c> → <c>avgrensning_tekst</c>.</item>
    /// </list>
    /// <c>oppdagelses_kilde</c> = <c>'ki:&lt;versjon&gt;'</c> der proveniensen har en AI-versjon for raden, ellers
    /// <c>'manuell'</c>. Typekodene <c>M medlem_av</c> og <c>I innehar</c> legges inn i
    /// <c>relasjonstype_konfigurasjon</c> (R-kodene fantes fra før). Hver flyttet rad får en proveniensrad (<c>handling = 'migrert'</c>) som navngir
    /// tabellen den kom fra. Til slutt sammenlignes antallet: avviker det, avbrytes migrasjonen
    /// (RAISE EXCEPTION) i stedet for å fullføre med tap.
    /// <para>
    /// Rader i <c>virksomhet_relasjoner</c> med <c>entitetsstatus &lt;&gt; 'gjeldende'</c> kan ikke uttrykkes (kanten
    /// har ingen entitetsstatus, og ingen kodevei skrev noe annet enn 'gjeldende' — slettingen var ekte
    /// <c>Remove</c>). Finnes slike, avbrytes migrasjonen med en melding, i stedet for å miste dem stille.
    /// En relasjon uten både hjemmel og kommentar får en eksplisitt markør som kilde («ingen kilde oppgitt
    /// …») — ikke en oppfunnet kilde, men et synlig hull (ck_strukturkanter_kilde krever en av dem).
    /// </para>
    /// </summary>
    public const string DataSql = """
        DO $do$
        DECLARE
            v_vr integer;
            v_gm integer;
            v_mt integer;
            v_kanter integer;
        BEGIN
            IF EXISTS (SELECT 1 FROM virksomhet_relasjoner WHERE entitetsstatus <> 'gjeldende') THEN
                RAISE EXCEPTION 'Issue #311: virksomhet_relasjoner har rader med entitetsstatus <> gjeldende — kan ikke flyttes uten tap. Rydd dem for hånd og kjør migrasjonen på nytt.';
            END IF;

            SELECT count(*) INTO v_vr FROM virksomhet_relasjoner;
            SELECT count(*) INTO v_gm FROM gruppe_medlemskap;
            SELECT count(*) INTO v_mt FROM myndighetstildelinger;

            INSERT INTO strukturkanter (
                "Id", kategori, typekode, fra_virksomhet_id, til_virksomhet_id,
                hjemmel_rettskilde_id, hjemmel_eid, kilde_utenfor_korpus_tekst, kommentar,
                avgrensning_paragrafspenn_json, polaritet, status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
            SELECT r."Id", 'R', r.relasjons_type, r.fra_virksomhet_id, r.til_virksomhet_id,
                r.hjemmel_rettskilde_id, r.hjemmel_eid,
                CASE WHEN r.hjemmel_rettskilde_id IS NULL
                     THEN COALESCE(NULLIF(btrim(r.kommentar), ''),
                                   '(ingen kilde oppgitt — migrert fra virksomhet_relasjoner uten hjemmel og uten kommentar, issue #311)')
                END,
                CASE WHEN r.hjemmel_rettskilde_id IS NOT NULL THEN r.kommentar END,
                '[]', 'positiv', r.status,
                COALESCE((SELECT 'ki:' || p.ai_forslag_versjon FROM proveniens p
                          WHERE p.entitet_id = r."Id" AND p.ai_forslag_versjon IS NOT NULL
                          ORDER BY p.dato LIMIT 1), 'manuell'),
                r.opprettet_av, r.opprettet_tidspunkt
            FROM virksomhet_relasjoner r;

            INSERT INTO strukturkanter (
                "Id", kategori, typekode, fra_begrep_id, til_begrep_id, hjemmel_rettskilde_id,
                avgrensning_paragrafspenn_json, polaritet, gyldig_fra, gyldig_til, status, oppdagelses_kilde,
                opprettet_av, opprettet_tidspunkt, sist_endret_av, sist_endret_tidspunkt)
            SELECT g."Id", 'M', 'medlem_av', g.underordnet_gruppe_begrep_id, g.overordnet_gruppe_begrep_id,
                g.hjemmel_rettskilde_id, g.paragrafspenn_json, 'positiv', g.gyldig_fra, g.gyldig_til, g.status,
                COALESCE((SELECT 'ki:' || p.ai_forslag_versjon FROM proveniens p
                          WHERE p.entitet_id = g."Id" AND p.ai_forslag_versjon IS NOT NULL
                          ORDER BY p.dato LIMIT 1), 'manuell'),
                g.opprettet_av, g.opprettet_tidspunkt, g.sist_endret_av, g.sist_endret_tidspunkt
            FROM gruppe_medlemskap g;

            INSERT INTO strukturkanter (
                "Id", kategori, typekode, fra_virksomhet_id, til_begrep_id, hjemmel_rettskilde_id,
                avgrensning_paragrafspenn_json, avgrensning_tekst, polaritet, gyldig_fra, gyldig_til, status,
                oppdagelses_kilde, opprettet_av, opprettet_tidspunkt, sist_endret_av, sist_endret_tidspunkt)
            SELECT m."Id",
                CASE WHEN b.begrepskategori = 'rolle' THEN 'I' ELSE 'M' END,
                CASE WHEN b.begrepskategori = 'rolle' THEN 'innehar' ELSE 'medlem_av' END,
                m.virksomhet_id, m.gruppe_begrep_id, m.hjemmel_rettskilde_id,
                m.paragrafspenn_json, m.vilkaar, 'positiv', m.gyldig_fra, m.gyldig_til, m.status,
                COALESCE((SELECT 'ki:' || p.ai_forslag_versjon FROM proveniens p
                          WHERE p.entitet_id = m."Id" AND p.ai_forslag_versjon IS NOT NULL
                          ORDER BY p.dato LIMIT 1), 'manuell'),
                m.opprettet_av, m.opprettet_tidspunkt, m.sist_endret_av, m.sist_endret_tidspunkt
            FROM myndighetstildelinger m
            JOIN begreper b ON b."Id" = m.gruppe_begrep_id;

            -- Typekodene de flyttede kantene bruker, slik at den logiske FK-en holder i det øyeblikket migrasjonen er
            -- ferdig (resten av startsettet seedes ved oppstart, Strukturkanter.SeedStartsettAsync). Samme maler som der.
            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv)
            VALUES (gen_random_uuid(), 'M', 'medlem_av', 'er medlem av {0}', 'har medlem {0}', 34, true),
                   (gen_random_uuid(), 'I', 'innehar', 'innehar rollen {0}', 'innehas av {0}', 43, true)
            ON CONFLICT (kategori, kode) DO NOTHING;

            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'strukturkant', k."Id", 'migrasjon-311', now(), 'migrert',
                jsonb_build_object('issue', 311, 'fra_tabell',
                    CASE WHEN EXISTS (SELECT 1 FROM virksomhet_relasjoner r WHERE r."Id" = k."Id") THEN 'virksomhet_relasjoner'
                         WHEN EXISTS (SELECT 1 FROM gruppe_medlemskap g WHERE g."Id" = k."Id") THEN 'gruppe_medlemskap'
                         ELSE 'myndighetstildelinger' END)
            FROM strukturkanter k;

            SELECT count(*) INTO v_kanter FROM strukturkanter;
            IF v_kanter <> v_vr + v_gm + v_mt THEN
                RAISE EXCEPTION 'Issue #311: % kanter etter flytting, forventet % (% relasjoner + % medlemskap + % tildelinger). Avbrutt — ingen rader skal gå tapt.',
                    v_kanter, v_vr + v_gm + v_mt, v_vr, v_gm, v_mt;
            END IF;
            RAISE NOTICE 'Issue #311: flyttet % relasjoner, % gruppemedlemskap og % myndighetstildelinger til % strukturkanter.',
                v_vr, v_gm, v_mt, v_kanter;
        END
        $do$;
        """;

    // ---------------- Organene (Johanns beslutning på #311, 2026-10-07) ----------------

    /// <summary>Organisasjonsnummeret Johann oppga for Stortinget — og den STABILE seed-nøkkelen (CLAUDE.md §4).</summary>
    public const string StortingetOrgnr = "971524960";

    /// <summary>Verdiene fra Brreg-øyeblikksbildet <c>Seed/brreg-971524960-stortinget.json</c> (hentet
    /// 2026-10-07). Navnet er registerets rå form (#158), ikke normalisert.</summary>
    public const string StortingetNavn = "STORTINGET";
    public const string StortingetOrganisasjonsform = "STAT";
    public const string StortingetSektorkode = "6100";
    public const string StortingetHjemmeside = "www.stortinget.no/";
    public const string StortingetHentet = "2026-10-07";

    /// <summary>Navnet på organet uten orgnr. Ordrett slik det står i loven (helse- og omsorgstjenesteloven
    /// m.fl.) og slik #310-begrepet het.</summary>
    public const string KongenIStatsradNavn = "Kongen i statsråd";

    /// <summary>ELI for Grunnloven — hjemmelen Johann oppga for «Kongen i statsråd».</summary>
    public const string GrunnlovenEli = "https://lovdata.no/eli/lov/1814/05/17/nor";

    /// <summary>
    /// Johanns beslutning på #311: organer bor i <see cref="Virksomhet"/>, og <c>'organ'</c> fjernes som
    /// begrepskategori.
    /// <list type="number">
    /// <item><b>Stortinget</b> opprettes fra Brreg-øyeblikksbildet hvis ingen virksomhet har orgnr 971524960
    /// (seed-vakt på den STABILE nøkkelen, CLAUDE.md §4). Felt som fra-brreg-endepunktet: navn rått,
    /// orgform, sektorkode, hovedside, <c>sist_brreg_synkronisert</c> = hentedatoen, forvaltningsnivå NULL
    /// (docs/20 §7.2: aldri utledet fra Brreg). Aktørtype <c>'organ'</c> — docs/33 §4.1 nevner Stortinget
    /// eksplisitt som organ. Finnes raden alt, settes bare en NULL-aktørtype til organ.</item>
    /// <item><b>«Kongen i statsråd»</b> opprettes uten orgnr, aktørtype <c>'organ'</c>. Det finnes ingen stabil
    /// registernøkkel, så vakten er (orgnr NULL, aktørtype organ, navn) — svakere enn §4 krever for seeds,
    /// men dette er en migrasjon som kjører én gang per base, ikke en oppstartsseed. Hjemmelen (Grunnloven)
    /// står i proveniensraden for opprettelsen (<c>kilde_referanser.hjemmel</c>): <see cref="Virksomhet"/> har
    /// ikke noe hjemmelfelt, og en strukturkant krever en motpart (Staten finnes ikke som virksomhet).</item>
    /// <item><b>Organ-begrepene blir navneformer</b> (<c>'virksomhet'</c>, <c>virksomhet_referanse_id</c> satt,
    /// <c>lovkilde_id</c> NULL — samme regel som #310 brukte for «stortinget»): «Kongen i statsråd» og den
    /// arkiverte «kongen» → Kongen i statsråd, «stortinget» → Stortinget. Taggene som pekte på dem flyttes fra
    /// begrep- til virksomhet-laget (samme regel som migrasjonen OmklassifiserNavneformTaggerTilVirksomhetslaget).
    /// Finnes en identisk navneform alt, slås begrepet sammen med den. Et organ-begrep med ANNEN term (satt
    /// for hånd i et annet miljø via nodetype-endepunktet) kan ikke knyttes til en virksomhet uten å gjette —
    /// det settes tilbake til <c>'gruppe'</c> (uavklart) med en proveniensrad, ikke kastet.</item>
    /// </list>
    /// Står etter <see cref="DataSql"/>: en kant som peker på et organ-begrep kan ikke bli en navneform-kant —
    /// finnes en slik, avbrytes migrasjonen (målt 2026-10-07: 0 lokalt).
    /// </summary>
    public const string OrganSql = $$"""
        DO $do$
        DECLARE
            v_storting uuid;
            v_kongen uuid;
            v_grunnlov uuid;
            v_mal uuid;
            v_navneform uuid;
            r record;
        BEGIN
            IF EXISTS (
                SELECT 1 FROM strukturkanter k JOIN begreper b ON b."Id" IN (k.fra_begrep_id, k.til_begrep_id)
                WHERE b.begrepskategori = 'organ') THEN
                RAISE EXCEPTION 'Issue #311: en strukturkant peker på et organ-begrep. Organ-begrepene skal bli navneformer for organ-virksomheter, og en navneform kan ikke bære en kant — koble kanten om for hånd og kjør migrasjonen på nytt.';
            END IF;

            -- 1. Stortinget (Brreg-øyeblikksbilde {{StortingetHentet}}).
            SELECT v."Id" INTO v_storting FROM virksomheter v WHERE v.organisasjonsnummer = '{{StortingetOrgnr}}';
            IF v_storting IS NULL THEN
                v_storting := gen_random_uuid();
                INSERT INTO virksomheter ("Id", navn, organisasjonsnummer, opprettet_tidspunkt, forvaltningsniva,
                    aktiv, organisasjonsform_kode, overordnet_enhet_id, sektorkode, sist_brreg_synkronisert, aktortype)
                VALUES (v_storting, '{{StortingetNavn}}', '{{StortingetOrgnr}}', now(), NULL,
                    true, '{{StortingetOrganisasjonsform}}', NULL, '{{StortingetSektorkode}}', DATE '{{StortingetHentet}}', 'organ');
                INSERT INTO virksomhet_nettsider ("Id", virksomhet_id, url, type)
                VALUES (gen_random_uuid(), v_storting, '{{StortingetHjemmeside}}', 'Hovedside');
                INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
                VALUES (gen_random_uuid(), 'virksomhet', v_storting, 'migrasjon-311', now(), 'opprettet',
                    jsonb_build_object('issue', 311, 'kilde', 'Brreg Enhetsregisteret, øyeblikksbilde',
                        'fil', 'src/RegelIde.Data/Seed/brreg-971524960-stortinget.json', 'hentet', '{{StortingetHentet}}'));
            ELSE
                UPDATE virksomheter SET aktortype = 'organ' WHERE "Id" = v_storting AND aktortype IS NULL;
            END IF;

            -- 2. Kongen i statsråd — uten orgnr, hjemmel Grunnloven (i proveniensen, se klassekommentaren).
            SELECT v."Id" INTO v_kongen FROM virksomheter v
            WHERE v.organisasjonsnummer IS NULL AND v.aktortype = 'organ' AND lower(v.navn) = lower('{{KongenIStatsradNavn}}')
            ORDER BY v.opprettet_tidspunkt LIMIT 1;
            IF v_kongen IS NULL THEN
                v_kongen := gen_random_uuid();
                SELECT rk."Id" INTO v_grunnlov FROM rettskilder rk
                WHERE rk.eli = '{{GrunnlovenEli}}' AND rk.entitetsstatus = 'gjeldende'
                ORDER BY rk.opprettet_tidspunkt DESC LIMIT 1;
                INSERT INTO virksomheter ("Id", navn, organisasjonsnummer, opprettet_tidspunkt, aktiv, aktortype)
                VALUES (v_kongen, '{{KongenIStatsradNavn}}', NULL, now(), true, 'organ');
                INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
                VALUES (gen_random_uuid(), 'virksomhet', v_kongen, 'migrasjon-311', now(), 'opprettet',
                    jsonb_build_object('issue', 311, 'beslutning', 'Johann 2026-10-07: organ uten orgnr',
                        'hjemmel', jsonb_build_object('eli', '{{GrunnlovenEli}}', 'rettskilde_id', v_grunnlov)));
            END IF;

            -- 3. Organ-begrepene → navneformer.
            FOR r IN SELECT b."Id", b.term, b.entitetsstatus FROM begreper b WHERE b.begrepskategori = 'organ' LOOP
                IF lower(r.term) IN (lower('{{KongenIStatsradNavn}}'), 'kongen') THEN
                    v_mal := v_kongen;
                ELSIF lower(r.term) = 'stortinget' THEN
                    v_mal := v_storting;
                ELSE
                    UPDATE begreper SET begrepskategori = 'gruppe', sist_endret_av = 'migrasjon-311', sist_endret_tidspunkt = now()
                    WHERE "Id" = r."Id";
                    INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
                    VALUES (gen_random_uuid(), 'begrep', r."Id", 'migrasjon-311', now(), 'endret',
                        jsonb_build_object('issue', 311, 'merknad', 'organ -> gruppe (uavklart): ingen kjent organ-virksomhet for termen'));
                    CONTINUE;
                END IF;

                -- Finnes en gjeldende navneform med samme term for virksomheten alt? Da slås begrepet sammen med den.
                v_navneform := NULL;
                IF r.entitetsstatus = 'gjeldende' THEN
                    SELECT b."Id" INTO v_navneform FROM begreper b
                    WHERE b.begrepskategori = 'virksomhet' AND b.virksomhet_referanse_id = v_mal
                      AND b.entitetsstatus = 'gjeldende' AND lower(b.term) = lower(r.term)
                    ORDER BY b.opprettet_tidspunkt LIMIT 1;
                END IF;

                IF v_navneform IS NOT NULL THEN
                    UPDATE tekst_tagger t SET entitetsstatus = 'arkivert'
                    WHERE t.ref_id = r."Id" AND EXISTS (
                        SELECT 1 FROM tekst_tagger u WHERE u.ref_id = v_navneform AND u.kind = 'virksomhet'
                          AND u.virksomhet_id = t.virksomhet_id AND u.rettskilde_id = t.rettskilde_id AND u.node_eid = t.node_eid
                          AND u.start_offset = t.start_offset AND u.end_offset = t.end_offset);
                    UPDATE tekst_tagger t SET ref_id = v_navneform, kind = 'virksomhet'
                    WHERE t.ref_id = r."Id" AND t.entitetsstatus = 'gjeldende';
                    UPDATE begrepsforekomster SET begrep_id = v_navneform WHERE begrep_id = r."Id";
                    UPDATE begreper SET begrepskategori = 'virksomhet', virksomhet_referanse_id = v_mal, lovkilde_id = NULL,
                        entitetsstatus = 'arkivert', status = 'arkivert', sist_endret_av = 'migrasjon-311', sist_endret_tidspunkt = now()
                    WHERE "Id" = r."Id";
                    INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
                    VALUES (gen_random_uuid(), 'begrep', r."Id", 'migrasjon-311', now(), 'arkivert',
                        jsonb_build_object('issue', 311, 'merknad', 'organ slått sammen med navneform ' || v_navneform::text));
                ELSE
                    UPDATE begreper SET begrepskategori = 'virksomhet', virksomhet_referanse_id = v_mal, lovkilde_id = NULL,
                        sist_endret_av = 'migrasjon-311', sist_endret_tidspunkt = now()
                    WHERE "Id" = r."Id";
                    -- Tagger fra begrep- til virksomhet-laget; en eksakt dublett i virksomhet-laget arkiveres i stedet.
                    UPDATE tekst_tagger t SET entitetsstatus = 'arkivert'
                    WHERE t.ref_id = r."Id" AND t.kind = 'begrep' AND EXISTS (
                        SELECT 1 FROM tekst_tagger u WHERE u.ref_id = r."Id" AND u.kind = 'virksomhet'
                          AND u.virksomhet_id = t.virksomhet_id AND u.rettskilde_id = t.rettskilde_id AND u.node_eid = t.node_eid
                          AND u.start_offset = t.start_offset AND u.end_offset = t.end_offset);
                    UPDATE tekst_tagger t SET kind = 'virksomhet'
                    WHERE t.ref_id = r."Id" AND t.kind = 'begrep' AND t.entitetsstatus = 'gjeldende';
                    INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
                    VALUES (gen_random_uuid(), 'begrep', r."Id", 'migrasjon-311', now(), 'endret',
                        jsonb_build_object('issue', 311, 'merknad', 'organ -> virksomhet (navneform for ' || v_mal::text || ')'));
                END IF;
            END LOOP;
        END
        $do$;
        """;
}
