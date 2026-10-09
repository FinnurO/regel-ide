namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #355 «avslutning speiler innsetting», Johanns beslutninger 2026-10-09] Data-delen av migrasjonen
/// <c>AvslutningSpeilerInnsetting</c>, skilt ut som konstanter slik at <c>AvslutningMigreringTests</c> kjører nøyaktig den SQL-en
/// migrasjonen kjører — samme teknikk som <see cref="PliktMigrering"/> (#353), <see cref="OppnevningMigrering"/> (#352) og
/// <see cref="KompetanseMigrering"/> (#341).
/// <para>
/// <b>Hva den gjør:</b>
/// <list type="number">
/// <item><b>Undertypene</b> (beslutning 1 og 2): CHECK <c>ck_strukturkanter_undertype</c> byttes i migrasjonen selv (oppnevning +
/// utnevning/konstitusjon, avsetting: avsetting/oppsigelse/avskjed, vedtak: tilbakekall). Ingen eksisterende kant får en undertype:
/// basen har ikke sitatet, og verbet gjettes ikke (CLAUDE.md §8) — samme valg som #352. Fasiten, som har sitatet, får undertypene av
/// <c>konvertering-355-avslutning.py</c>.</item>
/// <item><b>Forelegging ut av typekonfigurasjonen</b> (kommentaren 2026-10-09, beslutning 3): K <c>forelegging</c> slettes, med alle
/// verdiene i proveniensen (så <c>Down</c> legger inn nøyaktig den raden). En K forelegging-KANT kan ikke konverteres her: om svaret
/// er bindende (mottakerens kompetanse) eller rådgivende (P konsultasjon) avgjøres av teksten, og basen har den ikke. Finnes det
/// slike kanter, avbryter migrasjonen og lister id-ene — de må klassifiseres av et menneske først (samme regel som #341 for
/// <c>delegerer_til</c>). Målt lokalt 2026-10-09: 0 slike kanter.</item>
/// </list>
/// Ingenting annet røres. Ankeinstansen (beslutning 4) og ankeadgangen (beslutning 5) er fasit- og oppslagsendringer, ikke
/// dataendringer: lokalt finnes ingen K overprøving-kanter og ingen ankeadgang-kanter (målt 2026-10-09).
/// </para>
/// <para>
/// <b>Tellingen</b> (samme mønster som #311/#330/#341/#352/#353): totalt antall kanter likt før og etter; ingen K forelegging-kant
/// (før og etter); én proveniensrad (<c>endret_av = 'migrasjon-355'</c>, <c>handling = 'slettet'</c>) per slettet konfigurasjonsrad;
/// koden finnes ikke etterpå. Avviker noe: RAISE EXCEPTION.
/// </para>
/// <para>
/// <b>[LÅST] Frosset sammen med migrasjonen.</b> En historisk migrasjon skal gjøre det samme i dag som den dag den ble kjørt.
/// Trengs en endring: ny migrasjon, ikke rediger denne.
/// </para>
/// </summary>
public static class AvslutningMigrering
{
    /// <summary>Hvem migrasjonen skriver som i proveniensens <c>endret_av</c>.</summary>
    public const string EndretAv = "migrasjon-355";

    /// <summary>CHECK-en før (#352) og etter (#355). Migrasjonen bruker disse literalene (frosset).</summary>
    public const string UndertypeCheckFor =
        "undertype IS NULL OR (kategori = 'K' AND ((typekode = 'oppnevning' AND undertype IN ('valg', 'ansettelse', 'utpeking', 'oppnevning')) OR (typekode = 'overproving' AND undertype IN ('anke'))))";

    public const string UndertypeCheckEtter =
        "undertype IS NULL OR (kategori = 'K' AND ((typekode = 'oppnevning' AND undertype IN ('valg', 'ansettelse', 'utpeking', 'oppnevning', 'utnevning', 'konstitusjon')) OR (typekode = 'avsetting' AND undertype IN ('avsetting', 'oppsigelse', 'avskjed')) OR (typekode = 'overproving' AND undertype IN ('anke')) OR (typekode = 'vedtak' AND undertype IN ('tilbakekall'))))";

    public const string UpSql = """
        DO $do$
        DECLARE
            v_totalt_for integer;
            v_totalt_etter integer;
            v_forelegging text;
            v_konfig_for integer;
            v_proveniens integer;
            v_igjen integer;
        BEGIN
            -- ---- Før ----
            SELECT count(*) INTO v_totalt_for FROM strukturkanter;
            SELECT string_agg(k."Id"::text, ', ' ORDER BY k."Id") INTO v_forelegging
            FROM strukturkanter k WHERE k.kategori = 'K' AND k.typekode = 'forelegging';
            IF v_forelegging IS NOT NULL THEN
                RAISE EXCEPTION 'Issue #355: det finnes K forelegging-kanter (%). Forelegging er ikke lenger en kompetansetype, og om svaret er bindende (mottakerens kompetanse) eller rådgivende (P konsultasjon) avgjøres av teksten, som basen ikke har. Klassifiser dem for hånd først — ingenting er endret.', v_forelegging;
            END IF;
            SELECT count(*) INTO v_konfig_for FROM relasjonstype_konfigurasjon WHERE kategori = 'K' AND kode = 'forelegging';

            -- ---- Forelegging ut av konfigurasjonen, med alle verdiene i proveniensen (Down leser dem) ----
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'relasjonstype_konfigurasjon', c."Id", 'migrasjon-355', now(), 'slettet',
                jsonb_build_object('issue', 355, 'endring', 'fjernet_kode', 'kategori', c.kategori, 'kode', c.kode,
                    'fra_visningsmal', c.fra_visningsmal, 'til_visningsmal', c.til_visningsmal,
                    'sorteringsrekkefolge', c.sorteringsrekkefolge, 'aktiv', c.aktiv, 'familie', c.familie,
                    'fvl_kategori', c.fvl_kategori, 'saksavhengig', c.saksavhengig)
            FROM relasjonstype_konfigurasjon c
            WHERE c.kategori = 'K' AND c.kode = 'forelegging';
            DELETE FROM relasjonstype_konfigurasjon c WHERE c.kategori = 'K' AND c.kode = 'forelegging';

            -- ---- Etter ----
            SELECT count(*) INTO v_totalt_etter FROM strukturkanter;
            SELECT count(*) INTO v_proveniens FROM proveniens p
            WHERE p.endret_av = 'migrasjon-355' AND p.handling = 'slettet' AND p.kilde_referanser->>'issue' = '355';
            SELECT count(*) INTO v_igjen FROM relasjonstype_konfigurasjon WHERE kategori = 'K' AND kode = 'forelegging';

            IF v_totalt_etter <> v_totalt_for OR v_proveniens <> v_konfig_for OR v_igjen <> 0 THEN
                RAISE EXCEPTION 'Issue #355: avvik — % kanter før, % etter; % proveniensrader for % slettede konfigurasjonsrader; % forelegging-koder igjen. Avbrutt.',
                    v_totalt_for, v_totalt_etter, v_proveniens, v_konfig_for, v_igjen;
            END IF;

            RAISE NOTICE 'Issue #355: % kanter før og etter; 0 K forelegging-kanter; % konfigurasjonsrad(er) for K forelegging slettet (med proveniens).',
                v_totalt_for, v_konfig_for;
        END
        $do$;
        """;

    /// <summary>
    /// Snur NØYAKTIG det <see cref="UpSql"/> gjorde: K forelegging tilbake i konfigurasjonen med sin egen Id og sine egne verdier
    /// (fra proveniensen), og proveniensradene ut. Finnes det en undertype som ikke kan uttrykkes før #355 (utnevning, konstitusjon,
    /// en undertype på avsetting eller vedtak), avbrytes <c>Down</c> i stedet for å kaste opplysningen stille — CHECK-en fra #352
    /// ville ellers velte uten å si hvorfor.
    /// </summary>
    public const string DownSql = """
        DO $do$
        BEGIN
            IF EXISTS (SELECT 1 FROM strukturkanter k WHERE k.undertype IS NOT NULL AND (
                    (k.typekode = 'oppnevning' AND k.undertype IN ('utnevning', 'konstitusjon'))
                    OR k.typekode IN ('avsetting', 'vedtak'))) THEN
                RAISE EXCEPTION 'Issue #355 Down: det finnes kanter med undertype utnevning/konstitusjon, eller undertype på avsetting/vedtak, som ikke kan uttrykkes før #355. Rett dem for hånd først.';
            END IF;

            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv, familie, fvl_kategori, saksavhengig)
            SELECT p.entitet_id, r->>'kategori', r->>'kode', r->>'fra_visningsmal', r->>'til_visningsmal',
                   (r->>'sorteringsrekkefolge')::integer, (r->>'aktiv')::boolean, r->>'familie', r->>'fvl_kategori',
                   coalesce((r->>'saksavhengig')::boolean, false)
            FROM proveniens p CROSS JOIN LATERAL (SELECT p.kilde_referanser AS r) x
            WHERE p.endret_av = 'migrasjon-355' AND p.handling = 'slettet' AND p.entitet_type = 'relasjonstype_konfigurasjon'
              AND p.kilde_referanser->>'issue' = '355'
            ON CONFLICT (kategori, kode) DO NOTHING;

            DELETE FROM proveniens p
            WHERE p.endret_av = 'migrasjon-355' AND p.handling = 'slettet' AND p.kilde_referanser->>'issue' = '355';
        END
        $do$;
        """;
}
