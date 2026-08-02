using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilLovdataKatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lovdata_katalog_oppforinger",
                columns: table => new
                {
                    datokode = table.Column<string>(type: "text", nullable: false),
                    tittel = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    sist_oppdatert = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("lovdata_katalog_oppforinger_pkey", x => x.datokode);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lovdata_katalog_oppforinger_sist_oppdatert",
                table: "lovdata_katalog_oppforinger",
                column: "sist_oppdatert");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lovdata_katalog_oppforinger");
        }
    }
}
