using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilTjenesteRedesignUtvidelser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_tjeneste_regelverksreferanser",
                table: "tjeneste_regelverksreferanser");

            migrationBuilder.AddColumn<string>(
                name: "egne_innholdselementer",
                table: "tjenester",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "felt",
                table: "tjeneste_regelverksreferanser",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bruker_visningsinnstillinger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    bruker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seksjonsrekkefolge = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    skjulte_seksjoner = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    accordion_rekkefolge = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    accordion_apne = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("bruker_visningsinnstillinger_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bruker_visningsinnstillinger_brukere_bruker_id",
                        column: x => x.bruker_id,
                        principalTable: "brukere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "handling_tjenester",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    handling_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tjeneste_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("handling_tjenester_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_handling_tjenester_handlinger_handling_id",
                        column: x => x.handling_id,
                        principalTable: "handlinger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_handling_tjenester_tjenester_tjeneste_id",
                        column: x => x.tjeneste_id,
                        principalTable: "tjenester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tjeneste_regelverksreferanser",
                table: "tjeneste_regelverksreferanser",
                columns: new[] { "tjeneste_id", "til_rettskilde_id", "til_eid" },
                unique: true,
                filter: "felt IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_tjeneste_regelverksreferanser_felt",
                table: "tjeneste_regelverksreferanser",
                columns: new[] { "tjeneste_id", "til_rettskilde_id", "til_eid", "felt" },
                unique: true,
                filter: "felt IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_bruker_visningsinnstillinger_bruker",
                table: "bruker_visningsinnstillinger",
                column: "bruker_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_handling_tjenester_tjeneste_id",
                table: "handling_tjenester",
                column: "tjeneste_id");

            migrationBuilder.CreateIndex(
                name: "ux_handling_tjenester",
                table: "handling_tjenester",
                columns: new[] { "handling_id", "tjeneste_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bruker_visningsinnstillinger");

            migrationBuilder.DropTable(
                name: "handling_tjenester");

            migrationBuilder.DropIndex(
                name: "ux_tjeneste_regelverksreferanser",
                table: "tjeneste_regelverksreferanser");

            migrationBuilder.DropIndex(
                name: "ux_tjeneste_regelverksreferanser_felt",
                table: "tjeneste_regelverksreferanser");

            migrationBuilder.DropColumn(
                name: "egne_innholdselementer",
                table: "tjenester");

            migrationBuilder.DropColumn(
                name: "felt",
                table: "tjeneste_regelverksreferanser");

            migrationBuilder.CreateIndex(
                name: "ux_tjeneste_regelverksreferanser",
                table: "tjeneste_regelverksreferanser",
                columns: new[] { "tjeneste_id", "til_rettskilde_id", "til_eid" },
                unique: true);
        }
    }
}
