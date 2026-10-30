using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class FunnTilVurdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "funn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ekstern_nokkel = table.Column<string>(type: "text", nullable: true),
                    kategori = table.Column<string>(type: "text", nullable: false),
                    undertype = table.Column<string>(type: "text", nullable: true),
                    tittel = table.Column<string>(type: "text", nullable: false),
                    beskrivelse = table.Column<string>(type: "text", nullable: false),
                    vurdering = table.Column<string>(type: "text", nullable: true),
                    intern_merknad = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "oppdaget"),
                    oppdagelses_kilde = table.Column<string>(type: "text", nullable: false),
                    funnet_i = table.Column<string>(type: "text", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    juristvurdert_av = table.Column<string>(type: "text", nullable: true),
                    juristvurdert_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    vurdert_av_type = table.Column<string>(type: "text", nullable: true),
                    forklart_bort_begrunnelse = table.Column<string>(type: "text", nullable: true),
                    forklart_bort_av = table.Column<string>(type: "text", nullable: true),
                    forklart_bort_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    godkjent_av = table.Column<string>(type: "text", nullable: true),
                    godkjent_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    godkjent_innhold_json = table.Column<string>(type: "text", nullable: true),
                    godkjent_innhold_hash = table.Column<string>(type: "text", nullable: true),
                    kilde_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("funn_pkey", x => x.Id);
                    table.CheckConstraint("ck_funn_forklart_bort", "status <> 'forklart_bort' OR (forklart_bort_begrunnelse IS NOT NULL AND forklart_bort_av IS NOT NULL AND forklart_bort_tidspunkt IS NOT NULL)");
                    table.CheckConstraint("ck_funn_godkjent", "(status = 'godkjent_for_publisering' AND godkjent_av IS NOT NULL AND godkjent_tidspunkt IS NOT NULL AND godkjent_innhold_json IS NOT NULL AND godkjent_innhold_hash IS NOT NULL) OR (status <> 'godkjent_for_publisering' AND godkjent_innhold_json IS NULL AND godkjent_innhold_hash IS NULL)");
                    table.CheckConstraint("ck_funn_juristvurdert_hvem", "status NOT IN ('juristvurdert', 'godkjent_for_publisering') OR (juristvurdert_av IS NOT NULL AND juristvurdert_tidspunkt IS NOT NULL AND vurdert_av_type IS NOT NULL AND vurdering IS NOT NULL)");
                    table.CheckConstraint("ck_funn_kategori", "kategori IN ('utgatt_aktor', 'mulig_motstrid', 'uklarhet_hull')");
                    table.CheckConstraint("ck_funn_status", "status IN ('oppdaget', 'juristvurdert', 'godkjent_for_publisering', 'forklart_bort')");
                    table.CheckConstraint("ck_funn_undertype", "undertype IS NULL OR (kategori = 'utgatt_aktor' AND undertype IN ('nedlagt_aktor', 'utgatt_navn')) OR (kategori = 'uklarhet_hull' AND undertype IN ('hull', 'upresis_henvisning', 'publiseringsfeil_kilde'))");
                    table.CheckConstraint("ck_funn_vurdert_av_type", "vurdert_av_type IS NULL OR vurdert_av_type IN ('menneske', 'ki_agent')");
                });

            migrationBuilder.CreateTable(
                name: "funn_koblinger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    funn_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rolle = table.Column<string>(type: "text", nullable: false),
                    sortering = table.Column<int>(type: "integer", nullable: false),
                    etikett = table.Column<string>(type: "text", nullable: false),
                    merknad = table.Column<string>(type: "text", nullable: true),
                    rettskilde_eli = table.Column<string>(type: "text", nullable: true),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    eid = table.Column<string>(type: "text", nullable: true),
                    node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    begrep_id = table.Column<Guid>(type: "uuid", nullable: true),
                    strukturkant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kildefeil_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ekstern_lenke = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("funn_koblinger_pkey", x => x.Id);
                    table.CheckConstraint("ck_funn_koblinger_ett_mal", "(CASE WHEN rettskilde_eli IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN virksomhet_id IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN begrep_id IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN strukturkant_id IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN kildefeil_id IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN ekstern_lenke IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.CheckConstraint("ck_funn_koblinger_node", "node_id IS NULL OR eid IS NOT NULL");
                    table.CheckConstraint("ck_funn_koblinger_rettskilde", "(rettskilde_id IS NULL AND eid IS NULL AND node_id IS NULL) OR rettskilde_eli IS NOT NULL");
                    table.CheckConstraint("ck_funn_koblinger_rolle", "rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse', 'berort_aktor', 'navneform', 'etterfolger', 'kilde')");
                    table.CheckConstraint("ck_funn_koblinger_rolle_mal", "(rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse') AND rettskilde_eli IS NOT NULL) OR (rolle = 'berort_aktor' AND virksomhet_id IS NOT NULL) OR (rolle = 'navneform' AND begrep_id IS NOT NULL) OR (rolle = 'etterfolger' AND strukturkant_id IS NOT NULL) OR (rolle = 'kilde' AND (rettskilde_eli IS NOT NULL OR ekstern_lenke IS NOT NULL OR kildefeil_id IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_funn_koblinger_begreper_begrep_id",
                        column: x => x.begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_funn_koblinger_funn_funn_id",
                        column: x => x.funn_id,
                        principalTable: "funn",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_funn_koblinger_kildefeil_kildefeil_id",
                        column: x => x.kildefeil_id,
                        principalTable: "kildefeil",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_funn_koblinger_rettskilde_noder_node_id",
                        column: x => x.node_id,
                        principalTable: "rettskilde_noder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_funn_koblinger_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_funn_koblinger_strukturkanter_strukturkant_id",
                        column: x => x.strukturkant_id,
                        principalTable: "strukturkanter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_funn_koblinger_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "funn_vurderingspunkter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    funn_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    sortering = table.Column<int>(type: "integer", nullable: false),
                    overskrift = table.Column<string>(type: "text", nullable: true),
                    tekst = table.Column<string>(type: "text", nullable: false),
                    sikkerhet = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("funn_vurderingspunkter_pkey", x => x.Id);
                    table.CheckConstraint("ck_funn_vurderingspunkter_sikkerhet", "sikkerhet IS NULL OR (type = 'vurdering' AND sikkerhet IN ('hoy', 'middels_til_hoy', 'middels', 'lav_til_middels', 'lav'))");
                    table.CheckConstraint("ck_funn_vurderingspunkter_type", "type IN ('bortforklaring_provd', 'vurdering', 'ikke_avgjort')");
                    table.ForeignKey(
                        name: "FK_funn_vurderingspunkter_funn_funn_id",
                        column: x => x.funn_id,
                        principalTable: "funn",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_funn_status",
                table: "funn",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_funn_ekstern_nokkel",
                table: "funn",
                column: "ekstern_nokkel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_funn_koblinger_begrep_id",
                table: "funn_koblinger",
                column: "begrep_id");

            migrationBuilder.CreateIndex(
                name: "ix_funn_koblinger_funn",
                table: "funn_koblinger",
                column: "funn_id");

            migrationBuilder.CreateIndex(
                name: "IX_funn_koblinger_kildefeil_id",
                table: "funn_koblinger",
                column: "kildefeil_id");

            migrationBuilder.CreateIndex(
                name: "ix_funn_koblinger_node",
                table: "funn_koblinger",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "ix_funn_koblinger_rettskilde",
                table: "funn_koblinger",
                column: "rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_funn_koblinger_rettskilde_eli",
                table: "funn_koblinger",
                column: "rettskilde_eli");

            migrationBuilder.CreateIndex(
                name: "IX_funn_koblinger_strukturkant_id",
                table: "funn_koblinger",
                column: "strukturkant_id");

            migrationBuilder.CreateIndex(
                name: "ix_funn_koblinger_virksomhet",
                table: "funn_koblinger",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ix_funn_vurderingspunkter_funn",
                table: "funn_vurderingspunkter",
                column: "funn_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funn_koblinger");

            migrationBuilder.DropTable(
                name: "funn_vurderingspunkter");

            migrationBuilder.DropTable(
                name: "funn");
        }
    }
}
