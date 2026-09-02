using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilGyldighetsperiodePaMyndighetstildeling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "gyldig_fra",
                table: "myndighetstildelinger",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "gyldig_til",
                table: "myndighetstildelinger",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gyldig_fra",
                table: "myndighetstildelinger");

            migrationBuilder.DropColumn(
                name: "gyldig_til",
                table: "myndighetstildelinger");
        }
    }
}
