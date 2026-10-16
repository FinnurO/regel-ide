using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #378 B4/B5, Johanns godkjenning 2026-10-15] Generiske navneformer og bindingen av dem per rettskilde.
    /// <list type="bullet">
    /// <item><c>ck_begreper_navneformgrunn</c> får <c>'generisk'</c>; ny <c>ck_begreper_generisk_uten_referanse</c> (en generisk navneform er
    /// kategori virksomhet uten virksomhet) og <c>ux_begreper_generisk_term</c> (én per term for hele korpuset).</item>
    /// <item>Ny tabell <c>navneform_bindinger</c> med skrankene strukturkanter har etter #367 (se <see cref="NavneformBindingEntitet"/>).</item>
    /// <item>[ENDRET før merge, Johanns beslutning 2026-10-16 etter juristrunden B1] <c>gyldig_fra</c>/<c>gyldig_til</c> på bindingen
    /// (CHECK <c>ck_navneform_bindinger_gyldighet</c>), og ingen unik indeks på virkeområdet — overlappende gyldighet avvises i tjenesten.
    /// Migrasjonen ble rullet tilbake lokalt (0 bindinger, 0 generiske navneformer) og generert på nytt, i stedet for en ekstra migrasjon.</item>
    /// </list>
    /// Ingen datamigrering i <c>Up</c>: ingen eksisterende rad får <c>'generisk'</c> (det gjettes ikke hvilke navneformer som er generiske —
    /// det avgjøres i aktørsveipet, #378 A). <c>Up</c> skriver antallet navneformer per grunn før og etter (NOTICE) og avbryter hvis det endres.
    /// <c>Down</c> er eksakt motsatt: bindingene droppes med tabellen, og generiske navneformer og taggene som peker på dem slettes FØR den gamle
    /// CHECK-en legges tilbake (den ville ellers feile). Navnekandidater som ble godkjent som generiske, står igjen som godkjent.
    /// Bygget etter #368 (ikke stablet).
    /// </summary>
    public partial class GeneriskNavneformOgBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE TEMP TABLE _navneformgrunn_for_378 AS
                SELECT coalesce(navneformgrunn, '(null)') AS grunn, count(*) AS antall FROM begreper GROUP BY 1;");
            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper");

            migrationBuilder.CreateTable(
                name: "navneform_bindinger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    navneform_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paragrafspenn_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    til_virksomhet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    til_begrep_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hjemmel_eid = table.Column<string>(type: "text", nullable: true),
                    hjemmel_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kilde_utenfor_korpus_tekst = table.Column<string>(type: "text", nullable: true),
                    kilde_utenfor_korpus_lenke = table.Column<string>(type: "text", nullable: true),
                    kilde_utenfor_korpus_type = table.Column<string>(type: "text", nullable: true),
                    kilde_utenfor_korpus_dokumentasjon = table.Column<string>(type: "text", nullable: true),
                    kunnskapsstatus = table.Column<string>(type: "text", nullable: true),
                    begrunnelse = table.Column<string>(type: "text", nullable: true),
                    tolkningsargumenter = table.Column<List<string>>(type: "text[]", nullable: true),
                    profilversjon = table.Column<string>(type: "text", nullable: true),
                    gyldig_fra = table.Column<DateOnly>(type: "date", nullable: true),
                    gyldig_til = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "manuelt_foreslatt"),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    erstatter_binding_id = table.Column<Guid>(type: "uuid", nullable: true),
                    oppdagelses_kilde = table.Column<string>(type: "text", nullable: false, defaultValue: "manuell"),
                    kommentar = table.Column<string>(type: "text", nullable: true),
                    avvisning_begrunnelse = table.Column<string>(type: "text", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("navneform_bindinger_pkey", x => x.Id);
                    table.CheckConstraint("ck_navneform_bindinger_avvisning_begrunnelse", "avvisning_begrunnelse IS NULL OR status = 'avvist'");
                    table.CheckConstraint("ck_navneform_bindinger_avvist_hvem", "status <> 'avvist' OR (sist_endret_av IS NOT NULL AND sist_endret_tidspunkt IS NOT NULL)");
                    table.CheckConstraint("ck_navneform_bindinger_entitetsstatus", "entitetsstatus IN ('gjeldende', 'foreldet')");
                    table.CheckConstraint("ck_navneform_bindinger_fortolkning", "kunnskapsstatus IS NULL OR kunnskapsstatus <> 'fortolkning' OR (begrunnelse IS NOT NULL AND cardinality(tolkningsargumenter) > 0)");
                    table.CheckConstraint("ck_navneform_bindinger_gyldighet", "gyldig_fra IS NULL OR gyldig_til IS NULL OR gyldig_fra <= gyldig_til");
                    table.CheckConstraint("ck_navneform_bindinger_kilde", "(hjemmel_rettskilde_id IS NOT NULL AND hjemmel_eid IS NOT NULL AND kilde_utenfor_korpus_tekst IS NULL AND kilde_utenfor_korpus_lenke IS NULL AND kilde_utenfor_korpus_type IS NULL AND kilde_utenfor_korpus_dokumentasjon IS NULL) OR (hjemmel_rettskilde_id IS NULL AND hjemmel_eid IS NULL AND kilde_utenfor_korpus_tekst IS NOT NULL AND kilde_utenfor_korpus_type IS NOT NULL AND kilde_utenfor_korpus_dokumentasjon IS NOT NULL)");
                    table.CheckConstraint("ck_navneform_bindinger_kilde_dokumentasjon", "kilde_utenfor_korpus_dokumentasjon IS NULL OR kilde_utenfor_korpus_dokumentasjon IN ('primaer', 'sekundaer')");
                    table.CheckConstraint("ck_navneform_bindinger_kilde_type", "kilde_utenfor_korpus_type IS NULL OR kilde_utenfor_korpus_type IN ('kgl_res', 'instruks', 'tildelingsbrev', 'vedtekter', 'styrevedtak', 'forarbeider', 'nettside_annet', 'register')");
                    table.CheckConstraint("ck_navneform_bindinger_kunnskapsstatus", "kunnskapsstatus IS NULL OR kunnskapsstatus IN ('tekstfunn', 'normalisering', 'fortolkning', 'avledet', 'eksternt_dokumentert', 'uavklart', 'omstridt', 'foreldet')");
                    table.CheckConstraint("ck_navneform_bindinger_kunnskapsstatus_grunnlag", "(kunnskapsstatus IS NULL OR kunnskapsstatus <> 'eksternt_dokumentert' OR kilde_utenfor_korpus_tekst IS NOT NULL) AND (kunnskapsstatus IS NULL OR kunnskapsstatus NOT IN ('tekstfunn', 'normalisering') OR (hjemmel_rettskilde_id IS NOT NULL AND hjemmel_eid IS NOT NULL)) AND (kunnskapsstatus IS NULL OR kunnskapsstatus <> 'foreldet' OR entitetsstatus = 'foreldet')");
                    table.CheckConstraint("ck_navneform_bindinger_referent_en", "(til_virksomhet_id IS NULL) <> (til_begrep_id IS NULL)");
                    table.CheckConstraint("ck_navneform_bindinger_status", "status IN ('maskinelt_foreslatt', 'manuelt_foreslatt', 'faglig_vurdert', 'godkjent', 'avvist')");
                    table.CheckConstraint("ck_navneform_bindinger_tolkningsargumenter", "tolkningsargumenter IS NULL OR tolkningsargumenter <@ ARRAY['ordlyd', 'kontekst_system', 'formal', 'forarbeider', 'praksis', 'reelle_hensyn', 'antitetisk_analogisk']::text[]");
                    table.ForeignKey(
                        name: "FK_navneform_bindinger_begreper_navneform_id",
                        column: x => x.navneform_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_navneform_bindinger_begreper_til_begrep_id",
                        column: x => x.til_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_navneform_bindinger_navneform_bindinger_erstatter_binding_id",
                        column: x => x.erstatter_binding_id,
                        principalTable: "navneform_bindinger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_navneform_bindinger_rettskilde_noder_hjemmel_node_id",
                        column: x => x.hjemmel_node_id,
                        principalTable: "rettskilde_noder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_navneform_bindinger_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_navneform_bindinger_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_navneform_bindinger_virksomheter_til_virksomhet_id",
                        column: x => x.til_virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_begreper_generisk_term",
                table: "begreper",
                column: "term",
                unique: true,
                filter: "navneformgrunn = 'generisk' AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_generisk_uten_referanse",
                table: "begreper",
                sql: "navneformgrunn IS DISTINCT FROM 'generisk' OR (begrepskategori = 'virksomhet' AND virksomhet_referanse_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper",
                sql: "navneformgrunn IS NULL OR navneformgrunn IN ('gjeldende', 'utgatt', 'kortform', 'feilskriving', 'parallellnavn', 'generisk')");

            migrationBuilder.CreateIndex(
                name: "ix_navneform_bindinger_erstatter",
                table: "navneform_bindinger",
                column: "erstatter_binding_id");

            migrationBuilder.CreateIndex(
                name: "ix_navneform_bindinger_hjemmel",
                table: "navneform_bindinger",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_navneform_bindinger_hjemmel_node",
                table: "navneform_bindinger",
                column: "hjemmel_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_navneform_bindinger_navneform_rettskilde",
                table: "navneform_bindinger",
                columns: new[] { "navneform_id", "rettskilde_id" });

            migrationBuilder.CreateIndex(
                name: "ix_navneform_bindinger_rettskilde",
                table: "navneform_bindinger",
                column: "rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_navneform_bindinger_til_begrep",
                table: "navneform_bindinger",
                column: "til_begrep_id");

            migrationBuilder.CreateIndex(
                name: "ix_navneform_bindinger_til_virksomhet",
                table: "navneform_bindinger",
                column: "til_virksomhet_id");

            migrationBuilder.Sql(@"DO $$
DECLARE r record; avvik int;
BEGIN
  FOR r IN SELECT grunn, antall FROM _navneformgrunn_for_378 ORDER BY grunn LOOP
    RAISE NOTICE '#378 navneformgrunn %: % rader (uendret)', r.grunn, r.antall;
  END LOOP;
  SELECT count(*) INTO avvik FROM (
    SELECT coalesce(navneformgrunn, '(null)') AS grunn, count(*) AS antall FROM begreper GROUP BY 1
    EXCEPT SELECT grunn, antall FROM _navneformgrunn_for_378) x;
  IF avvik > 0 THEN RAISE EXCEPTION '#378: antallet navneformer per grunn endret seg i migrasjonen'; END IF;
  RAISE NOTICE '#378 navneform_bindinger: % rader', (SELECT count(*) FROM navneform_bindinger);
END $$;
DROP TABLE _navneformgrunn_for_378;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "navneform_bindinger");

            // Generiske navneformer finnes ikke før #378: taggene som peker på dem og radene selv fjernes før den gamle CHECK-en.
            migrationBuilder.Sql(@"DELETE FROM tekst_tagger WHERE kind = 'virksomhet'
                AND ref_id IN (SELECT ""Id"" FROM begreper WHERE navneformgrunn = 'generisk');
DELETE FROM begreper WHERE navneformgrunn = 'generisk';");

            migrationBuilder.DropIndex(
                name: "ux_begreper_generisk_term",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_generisk_uten_referanse",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper",
                sql: "navneformgrunn IS NULL OR navneformgrunn IN ('gjeldende', 'utgatt', 'kortform', 'feilskriving', 'parallellnavn')");
        }
    }
}
