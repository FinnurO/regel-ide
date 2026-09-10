using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilFastsattAvPaRettskilde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fastsatt_av",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fastsatt_av_organnavn",
                table: "rettskilder",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fastsatt_av",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "fastsatt_av_organnavn",
                table: "rettskilder");
        }
    }
}
