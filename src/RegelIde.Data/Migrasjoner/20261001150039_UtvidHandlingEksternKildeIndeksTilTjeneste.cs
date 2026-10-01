using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class UtvidHandlingEksternKildeIndeksTilTjeneste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_handlinger_ekstern_kilde",
                table: "handlinger");

            migrationBuilder.CreateIndex(
                name: "ux_handlinger_ekstern_kilde",
                table: "handlinger",
                columns: new[] { "ekstern_kilde_id", "tjeneste_id" },
                unique: true,
                filter: "ekstern_kilde_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_handlinger_ekstern_kilde",
                table: "handlinger");

            migrationBuilder.CreateIndex(
                name: "ux_handlinger_ekstern_kilde",
                table: "handlinger",
                column: "ekstern_kilde_id",
                unique: true,
                filter: "ekstern_kilde_id IS NOT NULL");
        }
    }
}
