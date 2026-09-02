using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilRaaHtmlOgEndringsfelt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ikrafttredelse_raa",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "konsolidert_dato_raa",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sist_endret_ved",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rettskilde_endringer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endring_eid = table.Column<string>(type: "text", nullable: false),
                    endring_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sorteringsrekkefolge = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rettskilde_endringer_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rettskilde_endringer_rettskilder_endring_rettskilde_id",
                        column: x => x.endring_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rettskilde_endringer_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rettskilde_endringer_endring_rettskilde",
                table: "rettskilde_endringer",
                column: "endring_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_rettskilde_endringer_rettskilde_id_endring_eid",
                table: "rettskilde_endringer",
                columns: new[] { "rettskilde_id", "endring_eid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rettskilde_endringer");

            migrationBuilder.DropColumn(
                name: "ikrafttredelse_raa",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "konsolidert_dato_raa",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "sist_endret_ved",
                table: "rettskilder");
        }
    }
}
