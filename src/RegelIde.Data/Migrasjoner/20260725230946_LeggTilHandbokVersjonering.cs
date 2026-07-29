using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilHandbokVersjonering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "rettskilde_noder_rettskilde_id_eid_key",
                table: "rettskilde_noder");

            migrationBuilder.AddColumn<string>(
                name: "entitetsstatus",
                table: "rettskilde_noder",
                type: "text",
                nullable: false,
                defaultValue: "gjeldende");

            migrationBuilder.AddColumn<Guid>(
                name: "erstatter_node_id",
                table: "rettskilde_noder",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "versjon",
                table: "rettskilde_noder",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "handbok_kommentar_metadata",
                columns: table => new
                {
                    node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dokumenttype = table.Column<string>(type: "text", nullable: false),
                    bindende = table.Column<bool>(type: "boolean", nullable: false),
                    feste_niva = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    revisjonsgrunn = table.Column<string>(type: "text", nullable: true),
                    publisert = table.Column<DateOnly>(type: "date", nullable: true),
                    sist_faglig_endret = table.Column<DateOnly>(type: "date", nullable: true),
                    underoverskrifter = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    marginord = table.Column<List<string>>(type: "text[]", nullable: false),
                    praksis = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("handbok_kommentar_metadata_pkey", x => x.node_id);
                    table.ForeignKey(
                        name: "FK_handbok_kommentar_metadata_rettskilde_noder_node_id",
                        column: x => x.node_id,
                        principalTable: "rettskilde_noder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rettskilde_noder_erstatter_node_id",
                table: "rettskilde_noder",
                column: "erstatter_node_id");

            migrationBuilder.CreateIndex(
                name: "ux_rettskilde_noder_eid_gjeldende",
                table: "rettskilde_noder",
                columns: new[] { "rettskilde_id", "eid" },
                unique: true,
                filter: "entitetsstatus = 'gjeldende'");

            migrationBuilder.AddForeignKey(
                name: "FK_rettskilde_noder_rettskilde_noder_erstatter_node_id",
                table: "rettskilde_noder",
                column: "erstatter_node_id",
                principalTable: "rettskilde_noder",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rettskilde_noder_rettskilde_noder_erstatter_node_id",
                table: "rettskilde_noder");

            migrationBuilder.DropTable(
                name: "handbok_kommentar_metadata");

            migrationBuilder.DropIndex(
                name: "IX_rettskilde_noder_erstatter_node_id",
                table: "rettskilde_noder");

            migrationBuilder.DropIndex(
                name: "ux_rettskilde_noder_eid_gjeldende",
                table: "rettskilde_noder");

            migrationBuilder.DropColumn(
                name: "entitetsstatus",
                table: "rettskilde_noder");

            migrationBuilder.DropColumn(
                name: "erstatter_node_id",
                table: "rettskilde_noder");

            migrationBuilder.DropColumn(
                name: "versjon",
                table: "rettskilde_noder");

            migrationBuilder.CreateIndex(
                name: "rettskilde_noder_rettskilde_id_eid_key",
                table: "rettskilde_noder",
                columns: new[] { "rettskilde_id", "eid" },
                unique: true);
        }
    }
}
