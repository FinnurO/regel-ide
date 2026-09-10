using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilLovdataFeilloggOgAksjonskrok : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nye_kilder_sveip_utfort_av",
                table: "lovdata_resynk_kjoringer",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "nye_kilder_sveip_utfort_tidspunkt",
                table: "lovdata_resynk_kjoringer",
                type: "timestamp with time zone",
                nullable: true);

            // [Rettet, orkestrator-verifisering, 2026-09-10] defaultValueSql lagt til: uten den feiler
            // migrasjonen med "column contains null values" mot en database som ALT har rader i
            // lovdata_resynk_kjoringer (enhver ekte dev-/prod-database — embedded-test-fixturen starter
            // alltid fra tom base, så dette ble aldri fanget av testene). Tomt array er riktig verdi for
            // eksisterende kjøringer: de fant ingen "nye rettskilder" i DENNE forstanden (kolonnen
            // fantes ikke da de kjørte), ikke en gjettet verdi — se HjemmelValideringTjeneste-mønsteret
            // for samme "ingen gjettet fallback"-holdning.
            migrationBuilder.AddColumn<List<Guid>>(
                name: "nye_rettskilde_ider",
                table: "lovdata_resynk_kjoringer",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "lovdata_importstatus_historikk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kjoring_id = table.Column<Guid>(type: "uuid", nullable: false),
                    datokode = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    tittel = table.Column<string>(type: "text", nullable: true),
                    eli = table.Column<string>(type: "text", nullable: false),
                    feilmelding = table.Column<string>(type: "text", nullable: true),
                    forsokt_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("lovdata_importstatus_historikk_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lovdata_importstatus_historikk_lovdata_resynk_kjoringer_kjo~",
                        column: x => x.kjoring_id,
                        principalTable: "lovdata_resynk_kjoringer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lovdata_importstatus_historikk_kjoring_id",
                table: "lovdata_importstatus_historikk",
                column: "kjoring_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lovdata_importstatus_historikk");

            migrationBuilder.DropColumn(
                name: "nye_kilder_sveip_utfort_av",
                table: "lovdata_resynk_kjoringer");

            migrationBuilder.DropColumn(
                name: "nye_kilder_sveip_utfort_tidspunkt",
                table: "lovdata_resynk_kjoringer");

            migrationBuilder.DropColumn(
                name: "nye_rettskilde_ider",
                table: "lovdata_resynk_kjoringer");
        }
    }
}
