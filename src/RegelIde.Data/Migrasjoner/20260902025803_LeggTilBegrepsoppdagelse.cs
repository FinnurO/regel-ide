using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilBegrepsoppdagelse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "begrepsforekomster",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_eid = table.Column<string>(type: "text", nullable: false),
                    start_offset = table.Column<int>(type: "integer", nullable: false),
                    end_offset = table.Column<int>(type: "integer", nullable: false),
                    begrep = table.Column<string>(type: "text", nullable: false),
                    begrep_original = table.Column<string>(type: "text", nullable: false),
                    definisjon = table.Column<string>(type: "text", nullable: true),
                    kildetype = table.Column<string>(type: "text", nullable: false),
                    monster_id = table.Column<string>(type: "text", nullable: false),
                    konfidens = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    scope_ref_eid = table.Column<string>(type: "text", nullable: true),
                    henvisnings_maal = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Venter"),
                    begrep_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    behandlet_av = table.Column<string>(type: "text", nullable: true),
                    behandlet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("begrepsforekomster_pkey", x => x.Id);
                    table.CheckConstraint("ck_begrepsforekomster_kildetype", "kildetype IN ('eksplisitt_liste', 'egen_paragraf', 'inline_menes', 'skal_forstas_som', 'copula', 'heretter_kalt', 'ekstern_referanse', 'eos_referanse', 'vedleggstabell', 'distribuert')");
                    table.CheckConstraint("ck_begrepsforekomster_konfidens", "konfidens IN ('hoy', 'middels', 'lav', 'krever_oppslag')");
                    table.CheckConstraint("ck_begrepsforekomster_monster_id", "monster_id IN ('M1', 'M2', 'M3', 'M4', 'M5', 'M6', 'M7', 'M8', 'M9', 'M10', 'M11', 'M12', 'M13', 'M14', 'M15', 'M16', 'M17')");
                    table.CheckConstraint("ck_begrepsforekomster_scope", "scope IN ('hele_dokumentet', 'kapittel', 'paragraf')");
                    table.CheckConstraint("ck_begrepsforekomster_status", "status IN ('Venter', 'Godkjent', 'Avvist')");
                    table.ForeignKey(
                        name: "FK_begrepsforekomster_begreper_begrep_id",
                        column: x => x.begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_begrepsforekomster_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "begrepsrelasjoner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    fra_forekomst_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_forekomst_id = table.Column<Guid>(type: "uuid", nullable: true),
                    til_term_fritekst = table.Column<string>(type: "text", nullable: true),
                    relasjonstype = table.Column<string>(type: "text", nullable: false),
                    til_referanse_eid = table.Column<string>(type: "text", nullable: false),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("begrepsrelasjoner_pkey", x => x.Id);
                    table.CheckConstraint("ck_begrepsrelasjoner_ett_mal", "(til_forekomst_id IS NOT NULL AND til_term_fritekst IS NULL) OR (til_forekomst_id IS NULL AND til_term_fritekst IS NOT NULL)");
                    table.CheckConstraint("ck_begrepsrelasjoner_type", "relasjonstype IN ('avhenger_av', 'utelukker', 'unntak_fra')");
                    table.ForeignKey(
                        name: "FK_begrepsrelasjoner_begrepsforekomster_fra_forekomst_id",
                        column: x => x.fra_forekomst_id,
                        principalTable: "begrepsforekomster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_begrepsrelasjoner_begrepsforekomster_til_forekomst_id",
                        column: x => x.til_forekomst_id,
                        principalTable: "begrepsforekomster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_begrepsforekomster_begrep",
                table: "begrepsforekomster",
                column: "begrep");

            migrationBuilder.CreateIndex(
                name: "IX_begrepsforekomster_begrep_id",
                table: "begrepsforekomster",
                column: "begrep_id");

            migrationBuilder.CreateIndex(
                name: "ix_begrepsforekomster_rettskilde",
                table: "begrepsforekomster",
                column: "rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_begrepsforekomster_rettskilde_node_start",
                table: "begrepsforekomster",
                columns: new[] { "rettskilde_id", "node_eid", "start_offset" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_begrepsrelasjoner_fra",
                table: "begrepsrelasjoner",
                column: "fra_forekomst_id");

            migrationBuilder.CreateIndex(
                name: "ix_begrepsrelasjoner_til",
                table: "begrepsrelasjoner",
                column: "til_forekomst_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "begrepsrelasjoner");

            migrationBuilder.DropTable(
                name: "begrepsforekomster");
        }
    }
}
