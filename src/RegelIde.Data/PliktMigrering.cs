namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #353 «pliktrelasjoner mellom parter (kategori P) og nodetypen ordning», Johanns godkjenning 2026-10-08] Data-delen
/// av migrasjonen <c>PliktOgOrdning</c>, skilt ut som konstanter slik at <c>PliktMigreringTests</c> kjører nøyaktig den SQL-en
/// migrasjonen kjører — samme teknikk som <see cref="OppnevningMigrering"/> (#352) og <see cref="KompetanseMigrering"/> (#341).
/// <para>
/// <b>Hva den gjør:</b>
/// <list type="number">
/// <item><b>Typekonfigurasjonen:</b> de seks P-typene (<see cref="Strukturkanter.Plikttyper"/>: samarbeid, avtale, betaling,
/// bistand, informasjon, konsultasjon), R <c>forvaltes_av</c> og G <c>tilhorer</c> legges inn (ON CONFLICT: finnes koden alt, røres
/// den ikke). Malene er de samme som <see cref="Strukturkanter.Startsett"/>.</item>
/// <item><b>Kanter med de to udefinerte R-kodene fra fasiten</b> (<c>samarbeider_med</c>, <c>bistar</c>) — som aldri har stått i
/// startsettet, men kan finnes i et miljø der noen har lagt dem inn for hånd — blir P <c>samarbeid</c>/<c>bistand</c> i SAMME
/// retning (fra = pliktsubjektet), med modalitet NULL: basen har ikke sitatet, og modaliteten gjettes ikke (CLAUDE.md §8). Kodene
/// slettes deretter fra konfigurasjonen (med proveniens, så <c>Down</c> legger inn nøyaktig de radene som fantes). Målt lokalt
/// 2026-10-09: 0 slike kanter og 0 slike koder — lokalt endrer migrasjonen bare konfigurasjonen og skjemaet.</item>
/// </list>
/// Ingen kant får modalitet, og ingen virksomhet blir ordning: det er opplysninger et menneske (eller konverteringen, som
/// forslag) registrerer — «Folketrygden» finnes ikke som virksomhet lokalt, og en rad med gjettede data opprettes ikke.
/// </para>
/// <para>
/// <b>Tellingen</b> (samme mønster som #311/#330/#341/#352): totalt antall kanter likt før og etter; ingen kant med en flyttet kode
/// igjen; hver P-målkode = før + konverterte; én proveniensrad (<c>endret_av = 'migrasjon-353'</c>) per endret kant med de gamle
/// verdiene; alle åtte nye typer finnes etterpå. Avviker noe: RAISE EXCEPTION.
/// </para>
/// <para>
/// <b>[LÅST] Frosset sammen med migrasjonen.</b> En historisk migrasjon skal gjøre det samme i dag som den dag den ble kjørt.
/// Trengs en endring: ny migrasjon, ikke rediger denne.
/// </para>
/// </summary>
public static class PliktMigrering
{
    /// <summary>Hvem migrasjonen skriver som i <c>sist_endret_av</c> og proveniensens <c>endret_av</c>.</summary>
    public const string EndretAv = "migrasjon-353";

    /// <summary>R-kodene som flyttes til P: (gammel R-kode, ny P-kode). SQL-en under har samme tabell som literal (frosset).</summary>
    public static readonly IReadOnlyList<(string GammelKode, string NyKode)> Flytting =
    [
        ("samarbeider_med", "samarbeid"),
        ("bistar", "bistand"),
    ];

    /// <summary>Typene migrasjonen legger inn: (kategori, kode).</summary>
    public static readonly IReadOnlyList<(string Kategori, string Kode)> NyeTyper =
    [
        .. Strukturkanter.Plikttyper.Select(t => (Strukturkanter.Plikt, t.Kode)),
        (Strukturkanter.Relasjon, Strukturkanter.ForvaltesAv),
        (Strukturkanter.Organtilhorighet, Strukturkanter.Tilhorer),
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
            v_feil integer;
            v_nye_typer integer;
        BEGIN
            CREATE TEMP TABLE flytting_353 (
                gammel_kode text PRIMARY KEY, ny_kode text NOT NULL, antall integer, antall_nye_for integer) ON COMMIT DROP;
            INSERT INTO flytting_353 (gammel_kode, ny_kode) VALUES ('samarbeider_med', 'samarbeid'), ('bistar', 'bistand');

            -- ---- Før ----
            UPDATE flytting_353 f SET
                antall = (SELECT count(*) FROM strukturkanter k WHERE k.kategori = 'R' AND k.typekode = f.gammel_kode),
                antall_nye_for = (SELECT count(*) FROM strukturkanter k WHERE k.kategori = 'P' AND k.typekode = f.ny_kode);
            SELECT count(*) INTO v_totalt_for FROM strukturkanter;
            SELECT sum(antall) INTO v_flyttet FROM flytting_353;

            -- ---- Typekonfigurasjonen: P-typene, R forvaltes_av, G tilhorer (samme maler som Strukturkanter.Startsett) ----
            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv, familie, fvl_kategori, saksavhengig)
            SELECT gen_random_uuid(), t.kategori, t.kode, t.fra_mal, t.til_mal,
                   coalesce((SELECT max(sorteringsrekkefolge) FROM relasjonstype_konfigurasjon), 0) + t.nr, true, NULL, NULL, false
            FROM (VALUES
                (1, 'P', 'samarbeid', 'har samarbeidsplikt {0}', '{0} har samarbeidsplikt overfor denne'),
                (2, 'P', 'avtale', 'har avtaleplikt {0}', '{0} har avtaleplikt overfor denne'),
                (3, 'P', 'betaling', 'har betalingsplikt {0}', '{0} har betalingsplikt overfor denne'),
                (4, 'P', 'bistand', 'har bistandsplikt {0}', '{0} har bistandsplikt overfor denne'),
                (5, 'P', 'informasjon', 'har informasjonsplikt {0}', '{0} har informasjonsplikt overfor denne'),
                (6, 'P', 'konsultasjon', 'har konsultasjonsplikt {0}', '{0} har konsultasjonsplikt overfor denne'),
                (7, 'R', 'forvaltes_av', 'forvaltes av {0}', 'forvalter {0}'),
                (8, 'G', 'tilhorer', 'tilhører {0}', 'har ordningen {0}')
            ) AS t(nr, kategori, kode, fra_mal, til_mal)
            ON CONFLICT (kategori, kode) DO NOTHING;

            -- ---- Proveniens FØR endringen: de gamle verdiene (Down leser dem) ----
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'strukturkant', k."Id", 'migrasjon-353', now(), 'endret',
                jsonb_build_object('issue', 353, 'endring', 'plikt',
                    'fra_kategori', k.kategori, 'fra_typekode', k.typekode, 'til_kategori', 'P', 'til_typekode', f.ny_kode)
            FROM strukturkanter k JOIN flytting_353 f ON k.kategori = 'R' AND k.typekode = f.gammel_kode;

            -- ---- Konverteringen: samme retning, modalitet NULL (basen har ikke sitatet) ----
            UPDATE strukturkanter k SET
                kategori = 'P',
                typekode = f.ny_kode,
                sist_endret_av = 'migrasjon-353',
                sist_endret_tidspunkt = now()
            FROM flytting_353 f
            WHERE k.kategori = 'R' AND k.typekode = f.gammel_kode;

            -- ---- Etter ----
            SELECT count(*) INTO v_totalt_etter FROM strukturkanter;
            SELECT count(*) INTO v_igjen
            FROM strukturkanter k JOIN flytting_353 f ON k.kategori = 'R' AND k.typekode = f.gammel_kode;
            SELECT string_agg(format('P %s: %s etter, forventet %s', f.ny_kode, e.antall, f.antall_nye_for + f.antall), '; ')
            INTO v_avvik
            FROM flytting_353 f
            CROSS JOIN LATERAL (SELECT count(*)::int AS antall FROM strukturkanter k WHERE k.kategori = 'P' AND k.typekode = f.ny_kode) e
            WHERE e.antall <> f.antall_nye_for + f.antall;
            SELECT count(*) INTO v_proveniens FROM proveniens p
            WHERE p.endret_av = 'migrasjon-353' AND p.handling = 'endret' AND p.kilde_referanser->>'issue' = '353';
            SELECT count(*) INTO v_feil
            FROM proveniens p JOIN strukturkanter k ON k."Id" = p.entitet_id
            WHERE p.endret_av = 'migrasjon-353' AND p.kilde_referanser->>'issue' = '353' AND p.handling = 'endret'
              AND (k.kategori <> 'P' OR k.typekode <> p.kilde_referanser->>'til_typekode' OR k.modalitet IS NOT NULL);
            SELECT count(*) INTO v_nye_typer FROM relasjonstype_konfigurasjon
            WHERE (kategori, kode) IN (('P', 'samarbeid'), ('P', 'avtale'), ('P', 'betaling'), ('P', 'bistand'), ('P', 'informasjon'),
                                       ('P', 'konsultasjon'), ('R', 'forvaltes_av'), ('G', 'tilhorer'));

            IF v_totalt_etter <> v_totalt_for OR v_igjen <> 0 OR v_avvik IS NOT NULL OR v_proveniens <> v_flyttet
               OR v_feil <> 0 OR v_nye_typer <> 8 THEN
                RAISE EXCEPTION 'Issue #353: avvik etter konvertering — % kanter før, % etter; % med flyttet kode igjen; % proveniensrader for % flyttet; % med feil type/modalitet; % av 8 nye typer; per målkode: %. Avbrutt — ingen rader skal gå tapt.',
                    v_totalt_for, v_totalt_etter, v_igjen, v_proveniens, v_flyttet, v_feil, v_nye_typer, coalesce(v_avvik, 'ok');
            END IF;

            -- ---- De flyttede R-kodene ut av konfigurasjonen (om de finnes), med alle verdiene i proveniensen ----
            INSERT INTO proveniens ("Id", entitet_type, entitet_id, endret_av, dato, handling, kilde_referanser)
            SELECT gen_random_uuid(), 'relasjonstype_konfigurasjon', c."Id", 'migrasjon-353', now(), 'slettet',
                jsonb_build_object('issue', 353, 'endring', 'fjernet_kode', 'kategori', c.kategori, 'kode', c.kode,
                    'fra_visningsmal', c.fra_visningsmal, 'til_visningsmal', c.til_visningsmal,
                    'sorteringsrekkefolge', c.sorteringsrekkefolge, 'aktiv', c.aktiv, 'familie', c.familie,
                    'fvl_kategori', c.fvl_kategori, 'saksavhengig', c.saksavhengig)
            FROM relasjonstype_konfigurasjon c
            WHERE (c.kategori, c.kode) IN (('R', 'samarbeider_med'), ('R', 'bistar'));
            DELETE FROM relasjonstype_konfigurasjon c WHERE (c.kategori, c.kode) IN (('R', 'samarbeider_med'), ('R', 'bistar'));

            RAISE NOTICE 'Issue #353: % kanter før og etter; % flyttet til P (%); 8 nye typer (P ×6, R forvaltes_av, G tilhorer).',
                v_totalt_for, v_flyttet,
                (SELECT string_agg(format('R %s → P %s: %s', gammel_kode, ny_kode, antall), ', ') FROM flytting_353);
        END
        $do$;
        """;

    /// <summary>
    /// Snur NØYAKTIG det <see cref="UpSql"/> gjorde: kantene med en <c>migrasjon-353</c>-proveniensrad tilbake til R med sin gamle
    /// kode, de slettede R-kodene inn igjen (fra proveniensen), og de åtte nye typene ut. Finnes det opplysninger som ikke kan
    /// uttrykkes før #353 — P-kanter registrert etter migrasjonen, R forvaltes_av/G tilhorer-kanter, en modalitet, en ordning eller
    /// en ordningstype — avbrytes <c>Down</c> i stedet for å kaste dem stille.
    /// </summary>
    public const string DownSql = """
        DO $do$
        BEGIN
            IF EXISTS (SELECT 1 FROM strukturkanter k WHERE k.kategori = 'P'
                       AND NOT EXISTS (SELECT 1 FROM proveniens p WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-353'
                                       AND p.kilde_referanser->>'issue' = '353' AND p.handling = 'endret'))
               OR EXISTS (SELECT 1 FROM strukturkanter k WHERE (k.kategori, k.typekode) IN (('R', 'forvaltes_av'), ('G', 'tilhorer')))
               OR EXISTS (SELECT 1 FROM strukturkanter k WHERE k.modalitet IS NOT NULL) THEN
                RAISE EXCEPTION 'Issue #353 Down: det finnes plikt-, forvaltes_av- eller tilhorer-kanter (eller en modalitet) registrert etter migrasjonen, som ikke kan uttrykkes før #353. Slett eller konverter dem for hånd først.';
            END IF;
            IF EXISTS (SELECT 1 FROM virksomheter v WHERE v.aktortype = 'ordning' OR v.ordningstype IS NOT NULL) THEN
                RAISE EXCEPTION 'Issue #353 Down: det finnes virksomheter med aktørtype ordning (eller en ordningstype), som ikke kan uttrykkes før #353. Rett dem for hånd først.';
            END IF;

            -- De slettede R-kodene tilbake med sin egen Id og sine egne verdier (fra proveniensen Up skrev).
            INSERT INTO relasjonstype_konfigurasjon ("Id", kategori, kode, fra_visningsmal, til_visningsmal, sorteringsrekkefolge, aktiv, familie, fvl_kategori, saksavhengig)
            SELECT p.entitet_id, r->>'kategori', r->>'kode', r->>'fra_visningsmal', r->>'til_visningsmal',
                   (r->>'sorteringsrekkefolge')::integer, (r->>'aktiv')::boolean, r->>'familie', r->>'fvl_kategori',
                   coalesce((r->>'saksavhengig')::boolean, false)
            FROM proveniens p CROSS JOIN LATERAL (SELECT p.kilde_referanser AS r) x
            WHERE p.endret_av = 'migrasjon-353' AND p.handling = 'slettet' AND p.entitet_type = 'relasjonstype_konfigurasjon'
              AND p.kilde_referanser->>'issue' = '353'
            ON CONFLICT (kategori, kode) DO NOTHING;

            UPDATE strukturkanter k SET
                kategori = p.kilde_referanser->>'fra_kategori',
                typekode = p.kilde_referanser->>'fra_typekode'
            FROM proveniens p
            WHERE p.entitet_id = k."Id" AND p.endret_av = 'migrasjon-353' AND p.handling = 'endret'
              AND p.kilde_referanser->>'issue' = '353' AND p.kilde_referanser->>'endring' = 'plikt';

            DELETE FROM proveniens p
            WHERE p.endret_av = 'migrasjon-353' AND p.handling IN ('endret', 'slettet') AND p.kilde_referanser->>'issue' = '353';

            DELETE FROM relasjonstype_konfigurasjon
            WHERE kategori = 'P' OR (kategori, kode) IN (('R', 'forvaltes_av'), ('G', 'tilhorer'));
        END
        $do$;
        """;
}
