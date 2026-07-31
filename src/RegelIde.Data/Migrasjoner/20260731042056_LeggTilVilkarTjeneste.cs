using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilVilkarTjeneste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tjeneste_id",
                table: "vilkar",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_vilkar_tjeneste",
                table: "vilkar",
                column: "tjeneste_id");

            migrationBuilder.AddForeignKey(
                name: "FK_vilkar_tjenester_tjeneste_id",
                table: "vilkar",
                column: "tjeneste_id",
                principalTable: "tjenester",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vilkar_tjenester_tjeneste_id",
                table: "vilkar");

            migrationBuilder.DropIndex(
                name: "ix_vilkar_tjeneste",
                table: "vilkar");

            migrationBuilder.DropColumn(
                name: "tjeneste_id",
                table: "vilkar");
        }
    }
}
