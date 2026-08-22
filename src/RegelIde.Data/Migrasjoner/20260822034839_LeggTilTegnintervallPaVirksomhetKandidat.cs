using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilTegnintervallPaVirksomhetKandidat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_virksomhet_kandidater_virksomhet_node",
                table: "virksomhet_kandidater");

            migrationBuilder.AddColumn<int>(
                name: "end_offset",
                table: "virksomhet_kandidater",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "start_offset",
                table: "virksomhet_kandidater",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ux_virksomhet_kandidater_virksomhet_node_start",
                table: "virksomhet_kandidater",
                columns: new[] { "virksomhet_id", "rettskilde_id", "node_eid", "start_offset" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_virksomhet_kandidater_virksomhet_node_start",
                table: "virksomhet_kandidater");

            migrationBuilder.DropColumn(
                name: "end_offset",
                table: "virksomhet_kandidater");

            migrationBuilder.DropColumn(
                name: "start_offset",
                table: "virksomhet_kandidater");

            migrationBuilder.CreateIndex(
                name: "ux_virksomhet_kandidater_virksomhet_node",
                table: "virksomhet_kandidater",
                columns: new[] { "virksomhet_id", "rettskilde_id", "node_eid" },
                unique: true);
        }
    }
}
