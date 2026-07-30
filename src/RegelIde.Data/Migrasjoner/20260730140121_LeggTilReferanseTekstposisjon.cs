using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilReferanseTekstposisjon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tekst_lengde",
                table: "rettskilde_referanser",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tekst_start",
                table: "rettskilde_referanser",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tekst_lengde",
                table: "rettskilde_referanser");

            migrationBuilder.DropColumn(
                name: "tekst_start",
                table: "rettskilde_referanser");
        }
    }
}
