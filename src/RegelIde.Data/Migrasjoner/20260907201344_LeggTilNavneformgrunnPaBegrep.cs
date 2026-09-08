using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilNavneformgrunnPaBegrep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "navneformgrunn",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper",
                sql: "navneformgrunn IS NULL OR navneformgrunn IN ('gjeldende', 'utgatt', 'kortform', 'feilskriving')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "navneformgrunn",
                table: "begreper");
        }
    }
}
