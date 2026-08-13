using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class KonvergerNettsideTilRettskilde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_nettside_lenker_nettside_dokumenter_fra_nettside_dokument_id",
                table: "nettside_lenker");

            migrationBuilder.DropForeignKey(
                name: "FK_nettside_lenker_nettside_dokumenter_til_nettside_dokument_id",
                table: "nettside_lenker");

            migrationBuilder.DropForeignKey(
                name: "FK_nettside_stier_nettside_dokumenter_nettside_dokument_id",
                table: "nettside_stier");

            migrationBuilder.DropTable(
                name: "nettside_dokumenter");

            migrationBuilder.DropIndex(
                name: "IX_nettside_lenker_til_nettside_dokument_id",
                table: "nettside_lenker");

            migrationBuilder.DropColumn(
                name: "til_nettside_dokument_id",
                table: "nettside_lenker");

            migrationBuilder.RenameColumn(
                name: "nettside_dokument_id",
                table: "nettside_stier",
                newName: "rettskilde_id");

            migrationBuilder.RenameIndex(
                name: "ux_nettside_stier_dokument_type_sti",
                table: "nettside_stier",
                newName: "ux_nettside_stier_rettskilde_type_sti");

            migrationBuilder.RenameColumn(
                name: "fra_nettside_dokument_id",
                table: "nettside_lenker",
                newName: "fra_node_id");

            migrationBuilder.AddForeignKey(
                name: "FK_nettside_lenker_rettskilde_noder_fra_node_id",
                table: "nettside_lenker",
                column: "fra_node_id",
                principalTable: "rettskilde_noder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_nettside_stier_rettskilder_rettskilde_id",
                table: "nettside_stier",
                column: "rettskilde_id",
                principalTable: "rettskilder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_nettside_lenker_rettskilde_noder_fra_node_id",
                table: "nettside_lenker");

            migrationBuilder.DropForeignKey(
                name: "FK_nettside_stier_rettskilder_rettskilde_id",
                table: "nettside_stier");

            migrationBuilder.RenameColumn(
                name: "rettskilde_id",
                table: "nettside_stier",
                newName: "nettside_dokument_id");

            migrationBuilder.RenameIndex(
                name: "ux_nettside_stier_rettskilde_type_sti",
                table: "nettside_stier",
                newName: "ux_nettside_stier_dokument_type_sti");

            migrationBuilder.RenameColumn(
                name: "fra_node_id",
                table: "nettside_lenker",
                newName: "fra_nettside_dokument_id");

            migrationBuilder.AddColumn<Guid>(
                name: "til_nettside_dokument_id",
                table: "nettside_lenker",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "nettside_dokumenter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    hentet = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    innholds_hash = table.Column<string>(type: "text", nullable: true),
                    kanonisk_url = table.Column<string>(type: "text", nullable: false),
                    raa_tekst = table.Column<string>(type: "text", nullable: true),
                    tittel = table.Column<string>(type: "text", nullable: true),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("nettside_dokumenter_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nettside_dokumenter_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_nettside_lenker_til_nettside_dokument_id",
                table: "nettside_lenker",
                column: "til_nettside_dokument_id");

            migrationBuilder.CreateIndex(
                name: "IX_nettside_dokumenter_virksomhet_id",
                table: "nettside_dokumenter",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ux_nettside_dokumenter_kanonisk_url",
                table: "nettside_dokumenter",
                column: "kanonisk_url",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_nettside_lenker_nettside_dokumenter_fra_nettside_dokument_id",
                table: "nettside_lenker",
                column: "fra_nettside_dokument_id",
                principalTable: "nettside_dokumenter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_nettside_lenker_nettside_dokumenter_til_nettside_dokument_id",
                table: "nettside_lenker",
                column: "til_nettside_dokument_id",
                principalTable: "nettside_dokumenter",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_nettside_stier_nettside_dokumenter_nettside_dokument_id",
                table: "nettside_stier",
                column: "nettside_dokument_id",
                principalTable: "nettside_dokumenter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
