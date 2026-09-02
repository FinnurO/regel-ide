using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilLovdataResynkAdministrasjon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lovdata_resynk_innstilling",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    intervall_timer = table.Column<int>(type: "integer", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("lovdata_resynk_innstilling_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lovdata_resynk_kjoringer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    utlost = table.Column<string>(type: "text", nullable: false),
                    utlost_av_bruker = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    startet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fullfort_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    nye = table.Column<int>(type: "integer", nullable: true),
                    nye_versjoner = table.Column<int>(type: "integer", nullable: true),
                    uendret = table.Column<int>(type: "integer", nullable: true),
                    feilet = table.Column<int>(type: "integer", nullable: true),
                    totalt_behandlet = table.Column<int>(type: "integer", nullable: true),
                    feilmelding = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("lovdata_resynk_kjoringer_pkey", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lovdata_resynk_kjoringer_startet_tidspunkt",
                table: "lovdata_resynk_kjoringer",
                column: "startet_tidspunkt");

            migrationBuilder.CreateIndex(
                name: "ix_lovdata_resynk_kjoringer_status",
                table: "lovdata_resynk_kjoringer",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lovdata_resynk_innstilling");

            migrationBuilder.DropTable(
                name: "lovdata_resynk_kjoringer");
        }
    }
}
