using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilHandlingEksternKildeOgRegelverksreferanser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ekstern_kilde_id",
                table: "handlinger",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "handling_regelverksreferanser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    handling_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_eid = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("handling_regelverksreferanser_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_handling_regelverksreferanser_handlinger_handling_id",
                        column: x => x.handling_id,
                        principalTable: "handlinger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_handling_regelverksreferanser_rettskilder_til_rettskilde_id",
                        column: x => x.til_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_handlinger_ekstern_kilde",
                table: "handlinger",
                column: "ekstern_kilde_id",
                unique: true,
                filter: "ekstern_kilde_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_handling_regelverksreferanser_til_rettskilde_id",
                table: "handling_regelverksreferanser",
                column: "til_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_handling_regelverksreferanser",
                table: "handling_regelverksreferanser",
                columns: new[] { "handling_id", "til_rettskilde_id", "til_eid" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_handlinger_eksterne_kilder_ekstern_kilde_id",
                table: "handlinger",
                column: "ekstern_kilde_id",
                principalTable: "eksterne_kilder",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_handlinger_eksterne_kilder_ekstern_kilde_id",
                table: "handlinger");

            migrationBuilder.DropTable(
                name: "handling_regelverksreferanser");

            migrationBuilder.DropIndex(
                name: "ux_handlinger_ekstern_kilde",
                table: "handlinger");

            migrationBuilder.DropColumn(
                name: "ekstern_kilde_id",
                table: "handlinger");
        }
    }
}
