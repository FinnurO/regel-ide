using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilAdministrativInndelingOgNavnekandidatKorreksjon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper");

            migrationBuilder.CreateTable(
                name: "navnekandidat_korreksjoner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opprinnelig_tekst = table.Column<string>(type: "text", nullable: false),
                    korrigert_tekst = table.Column<string>(type: "text", nullable: false),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("navnekandidat_korreksjoner_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_navnekandidat_korreksjoner_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater",
                sql: "kategori IN ('virksomhet', 'gruppe', 'administrativ_inndeling')");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_administrativ_inndeling_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori = 'administrativ_inndeling' AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper",
                sql: "begrepskategori IS NULL OR begrepskategori IN ('virksomhet', 'gruppe', 'administrativ_inndeling')");

            migrationBuilder.CreateIndex(
                name: "ux_navnekandidat_korreksjoner_rettskilde_opprinnelig",
                table: "navnekandidat_korreksjoner",
                columns: new[] { "rettskilde_id", "opprinnelig_tekst" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "navnekandidat_korreksjoner");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater");

            migrationBuilder.DropIndex(
                name: "ux_begreper_administrativ_inndeling_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater",
                sql: "kategori IN ('virksomhet', 'gruppe')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper",
                sql: "begrepskategori IS NULL OR begrepskategori IN ('virksomhet', 'gruppe')");
        }
    }
}
