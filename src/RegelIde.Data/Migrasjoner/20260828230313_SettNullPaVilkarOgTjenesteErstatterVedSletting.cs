using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class SettNullPaVilkarOgTjenesteErstatterVedSletting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tjenester_tjenester_erstatter_id",
                table: "tjenester");

            migrationBuilder.DropForeignKey(
                name: "FK_vilkar_tjenester_tjeneste_id",
                table: "vilkar");

            migrationBuilder.AddForeignKey(
                name: "FK_tjenester_tjenester_erstatter_id",
                table: "tjenester",
                column: "erstatter_id",
                principalTable: "tjenester",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_vilkar_tjenester_tjeneste_id",
                table: "vilkar",
                column: "tjeneste_id",
                principalTable: "tjenester",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tjenester_tjenester_erstatter_id",
                table: "tjenester");

            migrationBuilder.DropForeignKey(
                name: "FK_vilkar_tjenester_tjeneste_id",
                table: "vilkar");

            migrationBuilder.AddForeignKey(
                name: "FK_tjenester_tjenester_erstatter_id",
                table: "tjenester",
                column: "erstatter_id",
                principalTable: "tjenester",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_vilkar_tjenester_tjeneste_id",
                table: "vilkar",
                column: "tjeneste_id",
                principalTable: "tjenester",
                principalColumn: "Id");
        }
    }
}
