using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilRettskildeHjemmel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rettskilde_hjemler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hjemmel_eid = table.Column<string>(type: "text", nullable: false),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sorteringsrekkefolge = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rettskilde_hjemler_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rettskilde_hjemler_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rettskilde_hjemler_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rettskilde_hjemler_hjemmel_rettskilde",
                table: "rettskilde_hjemler",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_rettskilde_hjemler_rettskilde_id_hjemmel_eid",
                table: "rettskilde_hjemler",
                columns: new[] { "rettskilde_id", "hjemmel_eid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rettskilde_hjemler");
        }
    }
}
