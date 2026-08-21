using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilLovdataImportstatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lovdata_importstatus",
                columns: table => new
                {
                    datokode = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    tittel = table.Column<string>(type: "text", nullable: true),
                    eli = table.Column<string>(type: "text", nullable: false),
                    importert = table.Column<bool>(type: "boolean", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    feilmelding = table.Column<string>(type: "text", nullable: true),
                    sist_forsokt_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("lovdata_importstatus_pkey", x => x.datokode);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lovdata_importstatus_importert",
                table: "lovdata_importstatus",
                column: "importert");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lovdata_importstatus");
        }
    }
}
