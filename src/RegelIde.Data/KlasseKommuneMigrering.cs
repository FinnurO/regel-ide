namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #384, Johanns beslutning 2026-10-16] Datadelen av migrasjonen <c>KlasseKommuneOgUtledetMedlemskap</c>: den felles klassen
/// «kommune» (kommuneloven § 2-1) med utledet medlemskap fra områderegisteret. Skrevet her og ikke inline i migrasjonen, slik at testen
/// (<c>KlasseKommuneMigreringTests</c>) og migrasjonen bruker nøyaktig samme SQL (samme mønster som <see cref="OppgaveMigrering"/>).
/// <para>
/// <b>Up</b> oppretter ÉN rad i <c>begreper</c> (kategori klasse, fast nasjonal — ingen lovkilde, #298) og én proveniensrad. Ingen
/// medlemmer legges inn: medlemskapet regnes ut av <see cref="KlassemedlemskapTjeneste"/>. Finnes det alt et fast nasjonalt nodebegrep
/// med termen «kommune», opprettes ingenting: det gjettes ikke at det er samme klasse (CLAUDE.md §8). Det skrives som WARNING, og
/// spørsmålet går til et menneske. Antallet før og etter skrives som NOTICE, og migrasjonen avbryter hvis noe annet enn den ene raden
/// er endret.
/// </para>
/// <para>
/// <b>Down</b> fjerner raden og proveniensraden Up la inn — og NEKTER (exception) når noe er registrert mot klassen etter migrasjonen
/// (bindinger, strukturkanter, tagger, eller en ny versjon av begrepet): da ville Down slettet data Up ikke la inn. Samme regel som
/// <c>OppgaveFunksjon</c> (#368). Det som peker på klassen, må fjernes først.
/// </para>
/// </summary>
public static class KlasseKommuneMigrering
{
    public const string EndretAv = "migrasjon-384";

    public const string Definisjon =
        "Kommunene i Norge, hver et eget rettssubjekt (kommuneloven § 2-1). Felles klasse for hele landet: medlemmene er kommunene i "
        + "områderegisteret (Kartverkets inndeling) med sitt rettssubjekt, og utledes — de registreres ikke for hånd.";

    public static readonly string UpSql = $$"""
        DO $$
        DECLARE v_for int; v_etter int; v_konflikt int; v_prov int;
        BEGIN
          SELECT count(*) INTO v_for FROM begreper;
          SELECT count(*) INTO v_konflikt FROM begreper
          WHERE term = 'kommune' AND entitetsstatus = 'gjeldende' AND lovkilde_id IS NULL AND omradetype IS NULL
            AND begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade');
          IF v_konflikt > 0 THEN
            RAISE WARNING '#384: det finnes alt et fast nasjonalt nodebegrep «kommune» (% rader). Klassen med utledet medlemskap opprettes ikke — det gjettes ikke at det er samme klasse. Avgjøres av et menneske.', v_konflikt;
          ELSE
            INSERT INTO begreper ("Id", virksomhet_id, term, definisjon, lovreferanse_eid, gjelder_for, status, versjon, entitetsstatus,
                opprettet_av, opprettet_tidspunkt, begrepskategori, lovkilde_id, medlemskapsregel, kunnskapsstatus)
            VALUES ('{{Klasseregler.KommuneKlasseId}}', NULL, 'kommune', '{{Definisjon}}', '{{Klasseregler.KommunelovenParagraf21Eid}}', '{}',
                'publisert', 1, 'gjeldende', '{{EndretAv}}', now(), 'klasse', NULL, '{{Klasseregler.OmraderegisterKommune}}', 'tekstfunn');
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            VALUES (gen_random_uuid(), 'begrep', '{{Klasseregler.KommuneKlasseId}}', '{{EndretAv}}', now(), 'opprettet',
                jsonb_build_object('issue', 384, 'beslutning', 'Johann 2026-10-16', 'hjemmel', '{{Klasseregler.KommunelovenParagraf21Eid}}',
                    'kunnskapsstatus', 'tekstfunn', 'medlemskapsregel', '{{Klasseregler.OmraderegisterKommune}}'));
          END IF;
          SELECT count(*) INTO v_etter FROM begreper;
          SELECT count(*) INTO v_prov FROM proveniens WHERE endret_av = '{{EndretAv}}';
          RAISE NOTICE '#384 begreper: % før, % etter; klassen «kommune» med utledet medlemskap: % rad(er); proveniens: %',
            v_for, v_etter, v_etter - v_for, v_prov;
          IF v_etter - v_for <> (CASE WHEN v_konflikt > 0 THEN 0 ELSE 1 END) OR v_prov <> v_etter - v_for THEN
            RAISE EXCEPTION 'Issue #384 Up: antallet begreper eller proveniensrader stemmer ikke (% før, % etter, % proveniens).', v_for, v_etter, v_prov;
          END IF;
        END $$;
        """;

    public static readonly string DownSql = $$"""
        DO $$
        DECLARE v_bindinger int; v_kanter int; v_tagger int; v_versjoner int; v_for int; v_etter int;
        BEGIN
          SELECT count(*) INTO v_bindinger FROM navneform_bindinger
          WHERE til_begrep_id = '{{Klasseregler.KommuneKlasseId}}' OR navneform_id = '{{Klasseregler.KommuneKlasseId}}';
          SELECT count(*) INTO v_kanter FROM strukturkanter
          WHERE fra_begrep_id = '{{Klasseregler.KommuneKlasseId}}' OR til_begrep_id = '{{Klasseregler.KommuneKlasseId}}';
          SELECT count(*) INTO v_tagger FROM tekst_tagger WHERE ref_id = '{{Klasseregler.KommuneKlasseId}}';
          SELECT count(*) INTO v_versjoner FROM begreper WHERE erstatter_id = '{{Klasseregler.KommuneKlasseId}}';
          IF v_bindinger + v_kanter + v_tagger + v_versjoner > 0 THEN
            RAISE EXCEPTION 'Issue #384 Down: klassen «kommune» har % bindinger, % strukturkanter, % tagger og % nyere versjoner registrert etter migrasjonen. Down ville slettet dem; fjern dem først.',
              v_bindinger, v_kanter, v_tagger, v_versjoner;
          END IF;
          SELECT count(*) INTO v_for FROM begreper;
          DELETE FROM proveniens WHERE endret_av = '{{EndretAv}}' AND entitet_id = '{{Klasseregler.KommuneKlasseId}}';
          DELETE FROM begreper WHERE "Id" = '{{Klasseregler.KommuneKlasseId}}';
          SELECT count(*) INTO v_etter FROM begreper;
          RAISE NOTICE '#384 Down begreper: % før, % etter', v_for, v_etter;
        END $$;
        """;
}
