namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #368 «oppgave/funksjon (kategori F)», Johanns beslutning 2026-10-15] Data-delen av migrasjonen <c>OppgaveFunksjon</c>,
/// skilt ut som konstanter slik at <c>OppgaveMigreringTests</c> kjører nøyaktig den SQL-en migrasjonen kjører — samme teknikk som
/// <see cref="PliktMigrering"/> (#353).
/// <para>
/// <b>Hva den gjør:</b>
/// <list type="number">
/// <item><b>Typekonfigurasjonen:</b> de ni F-typene (<see cref="Strukturkanter.Oppgavetyper"/>) med visningsmaler, ON CONFLICT DO
/// NOTHING. Malene er de samme som <see cref="Strukturkanter.Startsett"/>.</item>
/// <item><b>Bare det juristrunden avgjorde flyttes</b> (Johann: «ingenting flyttes automatisk før en juristrunde på kandidatene»). Lokalt
/// fantes fire kandidater (R <c>sekretariat_for</c> fra KLAGENEMNDSSEKRETARIATET); utfallet per kant står i <see cref="Beslutninger"/>
/// og som literal i SQL-en (frosset). En kant som ikke finnes, eller som ikke lenger er R <c>sekretariat_for</c> med samme ender,
/// røres ikke (et annet miljø, eller en rad som er endret siden runden) — og telles som «ikke funnet».
/// <list type="bullet">
/// <item><c>begge</c>: R-kanten står, og en NY F-kant legges til med samme ender, hjemmel og kilde, kunnskapsstatus fra runden
/// (<c>avledet</c>, med R-kanten som premiss i <c>strukturkant_premisser</c>: teksten sier hva sekretariatet gjør, men ikke at det er
/// KNS — det følger av R-kanten med kilde utenfor korpus), status <c>maskinelt_foreslatt</c> og oppdagelseskilde
/// <c>ki:juristrunde-368</c> — den er en KI-juristrundes forslag og skal godkjennes av et menneske i forslagskøen (#313), ikke
/// godkjent av migrasjonen.</item>
/// <item><c>behold</c>: ingenting endres.</item>
/// </list></item>
/// </list>
/// </para>
/// <para>
/// <b>Tellingen</b> (samme mønster som #311/#330/#341/#352/#353): kanter etter = før + nye F-kanter; ingen eksisterende kant endret;
/// én proveniensrad (<c>endret_av = 'migrasjon-368'</c>, handling <c>opprettet</c>) per ny kant med kildekanten; alle ni F-typer finnes.
/// Avviker noe: RAISE EXCEPTION.
/// </para>
/// <para>
/// <b>[LÅST] Frosset sammen med migrasjonen.</b> Trengs en endring: ny migrasjon, ikke rediger denne.
/// </para>
/// </summary>
public static class OppgaveMigrering
{
    /// <summary>Hvem migrasjonen skriver som i <c>opprettet_av</c> og proveniensens <c>endret_av</c>.</summary>
    public const string EndretAv = "migrasjon-368";

    /// <summary>Oppdagelseskilden på F-kantene migrasjonen legger til: et KI-juristrundeforslag, ikke en manuell registrering.</summary>
    public const string Oppdagelseskilde = "ki:juristrunde-368";

    /// <summary>Juristrundens utfall for de lokale kandidatene (kant-id → utfall). SQL-en har samme tabell som literal (frosset).</summary>
    public static readonly IReadOnlyList<(Guid KantId, string Utfall)> Beslutninger =
    [
        (Guid.Parse("12e28ad6-49cb-4eba-8408-93cf4b837e41"), "behold"),
        (Guid.Parse("6d39745e-85e9-47a4-aadf-6046210aa3c6"), "behold"), // «oppgavene som lederen pålegger» = henvisning, ingen F
        (Guid.Parse("6d5b8668-4a74-4bf1-b702-eaf0b332607d"), "begge"),  // «sekretariat som foretar saksforberedelse»
        (Guid.Parse("0f407efc-4da0-4218-9ba7-97ca64ca8634"), "behold"), // som Energiklagenemnda
    ];

    public const string UpSql = """
        DO $do$
        DECLARE
            v_totalt_for integer;
            v_totalt_etter integer;
            v_aktuelle integer;
            v_nye integer;
            v_proveniens integer;
            v_endret integer;
            v_typer integer;
            v_ikke_funnet text;
        BEGIN
            -- ---- Typekonfigurasjonen: de åtte F-typene (samme maler som Strukturkanter.Startsett) ----
            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv, familie, fvl_kategori, saksavhengig)
            SELECT gen_random_uuid(), 'F', t.kode, t.fra_mal, t.til_mal,
                   coalesce((SELECT max(sorteringsrekkefolge) FROM relasjonstype_konfigurasjon), 0) + t.nr, true, NULL, NULL, false
            FROM (VALUES
                (1, 'forvaltning', 'har forvaltningsoppgave {0}', '{0} har forvaltningsoppgave som gjelder denne'),
                (2, 'samordning', 'har samordningsoppgave {0}', '{0} har samordningsoppgave som gjelder denne'),
                (3, 'sorge_for', 'har sørge-for-ansvar {0}', '{0} har sørge-for-ansvar som gjelder denne'),
                (4, 'oversikt', 'har oversiktsoppgave {0}', '{0} har oversiktsoppgave som gjelder denne'),
                (5, 'bistand', 'har bistandsoppgave {0}', '{0} har bistandsoppgave som gjelder denne'),
                (6, 'sekretariatsfunksjon', 'har sekretariatsfunksjon {0}', '{0} har sekretariatsfunksjon som gjelder denne'),
                (7, 'radgivning', 'har rådgivningsoppgave {0}', '{0} har rådgivningsoppgave som gjelder denne'),
                (8, 'veiledning', 'har veiledningsoppgave {0}', '{0} har veiledningsoppgave som gjelder denne'),
                (9, 'kontroll', 'har kontrolloppgave {0}', '{0} har kontrolloppgave som gjelder denne')
            ) AS t(nr, kode, fra_mal, til_mal)
            ON CONFLICT (kategori, kode) DO NOTHING;

            -- ---- Juristrundens utfall (frosset): bare «begge» gir en ny F-kant; «behold» står bare som spor ----
            CREATE TEMP TABLE oppgave_368 (
                kant_id uuid PRIMARY KEY, utfall text NOT NULL, typekode text, objekt text, modalitet text,
                kunnskapsstatus text, begrunnelse text, tolkningsargumenter text[]) ON COMMIT DROP;
            INSERT INTO oppgave_368 VALUES
                ('12e28ad6-49cb-4eba-8408-93cf4b837e41', 'behold', NULL, NULL, NULL, NULL, NULL, NULL),
                ('6d39745e-85e9-47a4-aadf-6046210aa3c6', 'behold', NULL, NULL, NULL, NULL, NULL, NULL),
                ('6d5b8668-4a74-4bf1-b702-eaf0b332607d', 'begge', 'sekretariatsfunksjon', 'saksforberedelse', 'skal', 'avledet',
                 'Avledet (juristrunden i #368): stiftelsesloven § 7a tredje ledd sier at Stiftelsesklagenemndas sekretariat foretar saksforberedelse — en stående funksjon for organet, ikke ett trinn i én sak. At sekretariatet er KLAGENEMNDSSEKRETARIATET, følger av premisset (R sekretariat_for med kilde utenfor korpus); kunnskapsstatus kan ikke være sterkere enn premisset.',
                 NULL),
                ('0f407efc-4da0-4218-9ba7-97ca64ca8634', 'behold', NULL, NULL, NULL, NULL, NULL, NULL);

            -- ---- Før ----
            SELECT count(*) INTO v_totalt_for FROM strukturkanter;
            -- Bare kanter som fortsatt er det runden vurderte: R sekretariat_for, gjeldende.
            CREATE TEMP TABLE aktuelle_368 ON COMMIT DROP AS
            SELECT o.*, k.fra_virksomhet_id, k.fra_begrep_id, k.til_virksomhet_id, k.til_begrep_id, k.hjemmel_rettskilde_id, k.hjemmel_eid,
                   k.hjemmel_node_id, k.kilde_utenfor_korpus_tekst, k.kilde_utenfor_korpus_lenke, k.kilde_utenfor_korpus_type,
                   k.kilde_utenfor_korpus_dokumentasjon, k.avgrensning_tekst, k.avgrensning_paragrafspenn_json
            FROM oppgave_368 o JOIN strukturkanter k ON k."Id" = o.kant_id
            WHERE o.utfall = 'begge' AND k.kategori = 'R' AND k.typekode = 'sekretariat_for' AND k.entitetsstatus = 'gjeldende';
            SELECT count(*) INTO v_aktuelle FROM aktuelle_368;
            SELECT string_agg(o.kant_id::text, ', ') INTO v_ikke_funnet
            FROM oppgave_368 o WHERE o.utfall = 'begge' AND NOT EXISTS (SELECT 1 FROM aktuelle_368 a WHERE a.kant_id = o.kant_id);

            -- ---- Nye F-kanter: samme ender, hjemmel og kilde som R-kanten; et forslag, ikke godkjent ----
            CREATE TEMP TABLE nye_368 ON COMMIT DROP AS SELECT gen_random_uuid() AS ny_id, a.* FROM aktuelle_368 a;
            INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, fra_begrep_id, til_virksomhet_id, til_begrep_id, objekt,
                avgrensning_paragrafspenn_json, avgrensning_tekst, polaritet, hjemmel_rettskilde_id, hjemmel_eid, hjemmel_node_id,
                kilde_utenfor_korpus_tekst, kilde_utenfor_korpus_lenke, kilde_utenfor_korpus_type, kilde_utenfor_korpus_dokumentasjon,
                status, oppdagelses_kilde, kommentar, opprettet_av, opprettet_tidspunkt, modalitet, kunnskapsstatus, begrunnelse,
                tolkningsargumenter, profilversjon, entitetsstatus)
            SELECT n.ny_id, 'F', n.typekode, n.fra_virksomhet_id, n.fra_begrep_id, n.til_virksomhet_id, n.til_begrep_id, n.objekt,
                n.avgrensning_paragrafspenn_json, n.avgrensning_tekst, 'positiv', n.hjemmel_rettskilde_id, n.hjemmel_eid, n.hjemmel_node_id,
                n.kilde_utenfor_korpus_tekst, n.kilde_utenfor_korpus_lenke, n.kilde_utenfor_korpus_type, n.kilde_utenfor_korpus_dokumentasjon,
                'maskinelt_foreslatt', 'ki:juristrunde-368',
                format('[#368, juristrunden] Oppgaven til sekretariatet; R sekretariat_for %s står (utfall «begge»).', n.kant_id),
                'migrasjon-368', now(), n.modalitet, n.kunnskapsstatus, n.begrunnelse, n.tolkningsargumenter,
                'arbeidsutkast-2026-10-09', 'gjeldende'
            FROM nye_368 n;
            SELECT count(*) INTO v_nye FROM nye_368;
            -- Avledet krever premisset (constraint-triggeren tr_strukturkanter_avledet_premiss, #367): R-kanten selv.
            INSERT INTO strukturkant_premisser (kant_id, premiss_kant_id)
            SELECT n.ny_id, n.kant_id FROM nye_368 n WHERE n.kunnskapsstatus = 'avledet';

            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'strukturkant', n.ny_id, 'migrasjon-368', now(), 'opprettet',
                jsonb_build_object('issue', 368, 'endring', 'oppgave', 'utfall', n.utfall, 'kildekant', n.kant_id)
            FROM nye_368 n;

            -- ---- Etter ----
            SELECT count(*) INTO v_totalt_etter FROM strukturkanter;
            SELECT count(*) INTO v_proveniens FROM proveniens p
            WHERE p.endret_av = 'migrasjon-368' AND p.handling = 'opprettet' AND p.kilde_referanser->>'issue' = '368';
            SELECT count(*) INTO v_endret FROM strukturkanter k JOIN oppgave_368 o ON o.kant_id = k."Id"
            WHERE k.kategori <> 'R' OR k.typekode <> 'sekretariat_for';
            SELECT count(*) INTO v_typer FROM relasjonstype_konfigurasjon WHERE kategori = 'F'
              AND kode IN ('forvaltning', 'samordning', 'sorge_for', 'oversikt', 'bistand', 'sekretariatsfunksjon', 'radgivning', 'veiledning', 'kontroll');

            IF v_totalt_etter <> v_totalt_for + v_nye OR v_nye <> v_aktuelle OR v_proveniens <> v_nye OR v_endret <> 0 OR v_typer <> 9 THEN
                RAISE EXCEPTION 'Issue #368: avvik — % kanter før, % etter, % nye F-kanter av % aktuelle, % proveniensrader, % kandidater endret, % av 9 F-typer. Avbrutt.',
                    v_totalt_for, v_totalt_etter, v_nye, v_aktuelle, v_proveniens, v_endret, v_typer;
            END IF;

            RAISE NOTICE 'Issue #368: % kanter før, % etter; % nye F sekretariat (forslag) fra juristrundens «begge»; 0 kanter flyttet; 9 F-typer; ikke funnet (annet miljø): %.',
                v_totalt_for, v_totalt_etter, v_nye, coalesce(v_ikke_funnet, 'ingen');
        END
        $do$;
        """;

    /// <summary>
    /// Snur NØYAKTIG det <see cref="UpSql"/> gjorde: F-kantene med en <c>migrasjon-368</c>-proveniensrad slettes (med proveniensen),
    /// og F-typene tas ut av konfigurasjonen. Finnes det F-kanter registrert etter migrasjonen (uten slik proveniens), eller en
    /// modalitet på F, avbrytes <c>Down</c> i stedet for å kaste dem stille.
    /// </summary>
    public const string DownSql = """
        DO $do$
        BEGIN
            IF EXISTS (SELECT 1 FROM strukturkanter k WHERE k.kategori = 'F'
                       AND NOT EXISTS (SELECT 1 FROM proveniens p WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-368'
                                       AND p.kilde_referanser->>'issue' = '368' AND p.handling = 'opprettet')) THEN
                RAISE EXCEPTION 'Issue #368 Down: det finnes oppgavekanter (F) registrert etter migrasjonen, som ikke kan uttrykkes før #368. Slett eller konverter dem for hånd først.';
            END IF;
            DELETE FROM strukturkanter k USING proveniens p
            WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-368' AND p.handling = 'opprettet' AND p.kilde_referanser->>'issue' = '368'
              AND k.kategori = 'F';
            DELETE FROM proveniens p WHERE p.endret_av = 'migrasjon-368' AND p.kilde_referanser->>'issue' = '368';
            DELETE FROM relasjonstype_konfigurasjon WHERE kategori = 'F';
        END
        $do$;
        """;
}
