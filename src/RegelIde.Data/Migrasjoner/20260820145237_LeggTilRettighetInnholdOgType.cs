using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilRettighetInnholdOgType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "formal",
                table: "tjenester",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "innhold",
                table: "tjenester",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "tjenester",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "formal",
                table: "tjenester");

            migrationBuilder.DropColumn(
                name: "innhold",
                table: "tjenester");

            migrationBuilder.DropColumn(
                name: "type",
                table: "tjenester");
        }
    }
}
