using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilHandbokRettskildeomfang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "handbok_rettskildeomfang",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    handbok_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("handbok_rettskildeomfang_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_handbok_rettskildeomfang_rettskilder_handbok_id",
                        column: x => x.handbok_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_handbok_rettskildeomfang_rettskilder_til_rettskilde_id",
                        column: x => x.til_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_handbok_rettskildeomfang_til_rettskilde_id",
                table: "handbok_rettskildeomfang",
                column: "til_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_handbok_rettskildeomfang",
                table: "handbok_rettskildeomfang",
                columns: new[] { "handbok_id", "til_rettskilde_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "handbok_rettskildeomfang");
        }
    }
}
