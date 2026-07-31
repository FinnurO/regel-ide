using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilAltinnBrukerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "altinn_bruker_id",
                table: "brukere",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_brukere_altinn_bruker_id",
                table: "brukere",
                column: "altinn_bruker_id",
                unique: true,
                filter: "altinn_bruker_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_brukere_altinn_bruker_id",
                table: "brukere");

            migrationBuilder.DropColumn(
                name: "altinn_bruker_id",
                table: "brukere");
        }
    }
}
