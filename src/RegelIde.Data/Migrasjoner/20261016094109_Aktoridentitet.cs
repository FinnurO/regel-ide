using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #385 «aktøridentitet», Johann 2026-10-16] Aktørens identitet kommer fra rettskildene; orgnr og registernavn er attributter.
    /// <list type="bullet">
    /// <item><c>begreper</c>: godkjenning (NULL for alle eksisterende rader = registrert før #385, ingen gjettet status), kilde utenfor korpus
    /// med én eller flere lenker, begrunnelse, tolkningsargumenter, oppdagelseskilde og avvisningsbegrunnelse. Gjør en navneform til et
    /// modellutsagn som kan foreslås og godkjennes. <c>ck_begreper_kunnskapsstatus</c> utvides med fortolkning og eksternt dokumentert.</item>
    /// <item><c>aktortype_utsagn</c> (ny): aktørtypen som modellutsagn. Tom etter migrasjonen; forslagene lages av tjenestene.</item>
    /// <item><c>navneform_bindinger</c>: «hjemmel endret» (AC12), lenkeliste, og «hjemmel mangler» som uavklart (AC8).</item>
    /// <item><c>strukturkanter</c>: lenkeliste. Datadelen kopierer den ene lenken inn i lista (samme for bindingene).</item>
    /// <item><c>navnekandidater</c>: avvisningsbegrunnelse (AC7).</item>
    /// </list>
    /// Ingen data foreslås av migrasjonen (CLAUDE.md §4): forslag til gjeldende navneform lages av
    /// <c>POST /api/aktorer/navneformforslag</c>, og forslag til aktørtype av aktørsveipet og lasteren. <c>Down</c> fjerner kolonnene og
    /// tabellen; lenkene står fortsatt i <c>kilde_utenfor_korpus_lenke</c> (den første).
    /// </summary>
    public partial class Aktoridentitet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_navneform_bindinger_kilde",
                table: "navneform_bindinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_kunnskapsstatus",
                table: "begreper");

            migrationBuilder.AddColumn<List<string>>(
                name: "kilde_utenfor_korpus_lenker",
                table: "strukturkanter",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "avvisning_begrunnelse",
                table: "navnekandidater",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hjemmel_endret_tidspunkt",
                table: "navneform_bindinger",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "kilde_utenfor_korpus_lenker",
                table: "navneform_bindinger",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "avvisning_begrunnelse",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "begrunnelse",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "godkjenning",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kilde_utenfor_korpus_dokumentasjon",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "kilde_utenfor_korpus_lenker",
                table: "begreper",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kilde_utenfor_korpus_tekst",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kilde_utenfor_korpus_type",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "oppdagelses_kilde",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "tolkningsargumenter",
                table: "begreper",
                type: "text[]",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aktortype_utsagn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aktortype = table.Column<string>(type: "text", nullable: false),
                    ordningstype = table.Column<string>(type: "text", nullable: true),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hjemmel_eid = table.Column<string>(type: "text", nullable: true),
                    kilde_utenfor_korpus_tekst = table.Column<string>(type: "text", nullable: true),
                    kilde_utenfor_korpus_type = table.Column<string>(type: "text", nullable: true),
                    kilde_utenfor_korpus_dokumentasjon = table.Column<string>(type: "text", nullable: true),
                    kilde_utenfor_korpus_lenker = table.Column<List<string>>(type: "text[]", nullable: true),
                    kunnskapsstatus = table.Column<string>(type: "text", nullable: true),
                    begrunnelse = table.Column<string>(type: "text", nullable: true),
                    tolkningsargumenter = table.Column<List<string>>(type: "text[]", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "manuelt_foreslatt"),
                    oppdagelses_kilde = table.Column<string>(type: "text", nullable: false, defaultValue: "manuell"),
                    avvisning_begrunnelse = table.Column<string>(type: "text", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("aktortype_utsagn_pkey", x => x.Id);
                    table.CheckConstraint("ck_aktortype_utsagn_aktortype", "aktortype IN ('rettssubjekt', 'organ', 'organisatorisk_enhet', 'ordning')");
                    table.CheckConstraint("ck_aktortype_utsagn_avvisning_begrunnelse", "avvisning_begrunnelse IS NULL OR status = 'avvist'");
                    table.CheckConstraint("ck_aktortype_utsagn_avvist_hvem", "status <> 'avvist' OR (sist_endret_av IS NOT NULL AND sist_endret_tidspunkt IS NOT NULL)");
                    table.CheckConstraint("ck_aktortype_utsagn_kilde", "(hjemmel_rettskilde_id IS NOT NULL AND hjemmel_eid IS NOT NULL AND kilde_utenfor_korpus_tekst IS NULL AND kilde_utenfor_korpus_type IS NULL AND kilde_utenfor_korpus_dokumentasjon IS NULL) OR (hjemmel_rettskilde_id IS NULL AND hjemmel_eid IS NULL AND kilde_utenfor_korpus_tekst IS NOT NULL AND kilde_utenfor_korpus_type IN ('kgl_res', 'instruks', 'tildelingsbrev', 'vedtekter', 'styrevedtak', 'forarbeider', 'nettside_annet', 'register') AND kilde_utenfor_korpus_dokumentasjon IN ('primaer', 'sekundaer')) OR (hjemmel_rettskilde_id IS NULL AND hjemmel_eid IS NULL AND kilde_utenfor_korpus_tekst IS NULL AND kilde_utenfor_korpus_type IS NULL AND kilde_utenfor_korpus_dokumentasjon IS NULL AND (kunnskapsstatus IS NULL OR (kunnskapsstatus = 'uavklart' AND begrunnelse IS NOT NULL)))");
                    table.CheckConstraint("ck_aktortype_utsagn_kunnskapsstatus", "kunnskapsstatus IS NULL OR (kunnskapsstatus IN ('tekstfunn', 'normalisering') AND hjemmel_rettskilde_id IS NOT NULL AND hjemmel_eid IS NOT NULL) OR (kunnskapsstatus = 'eksternt_dokumentert' AND kilde_utenfor_korpus_tekst IS NOT NULL) OR (kunnskapsstatus = 'fortolkning' AND begrunnelse IS NOT NULL AND cardinality(tolkningsargumenter) > 0) OR kunnskapsstatus IN ('uavklart', 'omstridt')");
                    table.CheckConstraint("ck_aktortype_utsagn_ordningstype", "ordningstype IS NULL OR (aktortype = 'ordning' AND ordningstype IN ('trygdeordning', 'fond', 'tilskuddsordning'))");
                    table.CheckConstraint("ck_aktortype_utsagn_status", "status IN ('maskinelt_foreslatt', 'manuelt_foreslatt', 'faglig_vurdert', 'godkjent', 'avvist')");
                    table.CheckConstraint("ck_aktortype_utsagn_tolkningsargumenter", "tolkningsargumenter IS NULL OR tolkningsargumenter <@ ARRAY['ordlyd', 'kontekst_system', 'formal', 'forarbeider', 'praksis', 'reelle_hensyn', 'antitetisk_analogisk']::text[]");
                    table.ForeignKey(
                        name: "FK_aktortype_utsagn_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_aktortype_utsagn_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_navneform_bindinger_kilde",
                table: "navneform_bindinger",
                sql: "(hjemmel_rettskilde_id IS NOT NULL AND hjemmel_eid IS NOT NULL AND kilde_utenfor_korpus_tekst IS NULL AND kilde_utenfor_korpus_lenke IS NULL AND kilde_utenfor_korpus_type IS NULL AND kilde_utenfor_korpus_dokumentasjon IS NULL) OR (hjemmel_rettskilde_id IS NULL AND hjemmel_eid IS NULL AND kilde_utenfor_korpus_tekst IS NOT NULL AND kilde_utenfor_korpus_type IS NOT NULL AND kilde_utenfor_korpus_dokumentasjon IS NOT NULL) OR (hjemmel_rettskilde_id IS NULL AND hjemmel_eid IS NULL AND kilde_utenfor_korpus_tekst IS NULL AND kilde_utenfor_korpus_lenke IS NULL AND kilde_utenfor_korpus_type IS NULL AND kilde_utenfor_korpus_dokumentasjon IS NULL AND kunnskapsstatus = 'uavklart' AND begrunnelse IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_avvisning_begrunnelse",
                table: "begreper",
                sql: "avvisning_begrunnelse IS NULL OR godkjenning = 'avvist'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_godkjenning",
                table: "begreper",
                sql: "godkjenning IS NULL OR godkjenning IN ('maskinelt_foreslatt', 'manuelt_foreslatt', 'faglig_vurdert', 'godkjent', 'avvist')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_kilde",
                table: "begreper",
                sql: "kilde_utenfor_korpus_type IS NULL OR (lovreferanse_eid IS NULL AND kilde_utenfor_korpus_tekst IS NOT NULL AND kilde_utenfor_korpus_dokumentasjon IN ('primaer', 'sekundaer') AND kilde_utenfor_korpus_type IN ('kgl_res', 'instruks', 'tildelingsbrev', 'vedtekter', 'styrevedtak', 'forarbeider', 'nettside_annet', 'register'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_kunnskapsstatus",
                table: "begreper",
                sql: "kunnskapsstatus IS NULL OR (kunnskapsstatus IN ('tekstfunn', 'normalisering') AND lovreferanse_eid IS NOT NULL) OR (kunnskapsstatus = 'eksternt_dokumentert' AND kilde_utenfor_korpus_tekst IS NOT NULL) OR (kunnskapsstatus = 'fortolkning' AND begrunnelse IS NOT NULL AND cardinality(tolkningsargumenter) > 0) OR kunnskapsstatus IN ('uavklart', 'omstridt')");

            migrationBuilder.CreateIndex(
                name: "ix_aktortype_utsagn_hjemmel",
                table: "aktortype_utsagn",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_aktortype_utsagn_virksomhet",
                table: "aktortype_utsagn",
                column: "virksomhet_id");

            // Datadelen: den ene lenken blir første (og eneste) element i lista.
            migrationBuilder.Sql(
                "UPDATE strukturkanter SET kilde_utenfor_korpus_lenker = ARRAY[kilde_utenfor_korpus_lenke] WHERE kilde_utenfor_korpus_lenke IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE navneform_bindinger SET kilde_utenfor_korpus_lenker = ARRAY[kilde_utenfor_korpus_lenke] WHERE kilde_utenfor_korpus_lenke IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aktortype_utsagn");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navneform_bindinger_kilde",
                table: "navneform_bindinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_avvisning_begrunnelse",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_godkjenning",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_kilde",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_kunnskapsstatus",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "kilde_utenfor_korpus_lenker",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "avvisning_begrunnelse",
                table: "navnekandidater");

            migrationBuilder.DropColumn(
                name: "hjemmel_endret_tidspunkt",
                table: "navneform_bindinger");

            migrationBuilder.DropColumn(
                name: "kilde_utenfor_korpus_lenker",
                table: "navneform_bindinger");

            migrationBuilder.DropColumn(
                name: "avvisning_begrunnelse",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "begrunnelse",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "godkjenning",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "kilde_utenfor_korpus_dokumentasjon",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "kilde_utenfor_korpus_lenker",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "kilde_utenfor_korpus_tekst",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "kilde_utenfor_korpus_type",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "oppdagelses_kilde",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "tolkningsargumenter",
                table: "begreper");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navneform_bindinger_kilde",
                table: "navneform_bindinger",
                sql: "(hjemmel_rettskilde_id IS NOT NULL AND hjemmel_eid IS NOT NULL AND kilde_utenfor_korpus_tekst IS NULL AND kilde_utenfor_korpus_lenke IS NULL AND kilde_utenfor_korpus_type IS NULL AND kilde_utenfor_korpus_dokumentasjon IS NULL) OR (hjemmel_rettskilde_id IS NULL AND hjemmel_eid IS NULL AND kilde_utenfor_korpus_tekst IS NOT NULL AND kilde_utenfor_korpus_type IS NOT NULL AND kilde_utenfor_korpus_dokumentasjon IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_kunnskapsstatus",
                table: "begreper",
                sql: "kunnskapsstatus IS NULL OR (kunnskapsstatus IN ('tekstfunn', 'normalisering') AND lovreferanse_eid IS NOT NULL) OR kunnskapsstatus IN ('uavklart', 'omstridt')");
        }
    }
}
