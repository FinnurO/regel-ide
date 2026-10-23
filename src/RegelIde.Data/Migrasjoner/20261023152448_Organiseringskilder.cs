using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class Organiseringskilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "objekt_eli",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kildeform",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rettskilde_disposisjonstyper",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disposisjonstype = table.Column<string>(type: "text", nullable: false),
                    kilde = table.Column<string>(type: "text", nullable: false),
                    regel = table.Column<string>(type: "text", nullable: false),
                    treff = table.Column<string>(type: "text", nullable: true),
                    node_eid = table.Column<string>(type: "text", nullable: true),
                    kunnskapsstatus = table.Column<string>(type: "text", nullable: false),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("rettskilde_disposisjonstyper_pkey", x => x.Id);
                    table.CheckConstraint("ck_rettskilde_disposisjonstyper_kilde", "kilde IN ('tittel', 'monster', 'ki', 'manuell')");
                    table.CheckConstraint("ck_rettskilde_disposisjonstyper_kunnskapsstatus", "kunnskapsstatus IN ('tekstfunn', 'normalisering', 'fortolkning', 'uavklart', 'omstridt')");
                    table.CheckConstraint("ck_rettskilde_disposisjonstyper_type", "disposisjonstype IN ('oppretting', 'navneendring', 'nedleggelse', 'omorganisering', 'overforing_ressortfordeling', 'delegering', 'instruks', 'ikrafttredelse', 'regulering')");
                    table.ForeignKey(
                        name: "FK_rettskilde_disposisjonstyper_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // [Ny, issue #395] Kildeformen fylles bare der Lovdatas dokid-prefiks er kjent (RettskildeMetadata.AvledKildeform). Rader uten
            // dokid (lokalt 2026-10-23: eldre, erstattede versjoner og seedfiler) blir stående NULL til neste resynk — ingen gjettet form.
            migrationBuilder.Sql("""
                UPDATE rettskilder SET kildeform = 'konsolidert'
                WHERE kildeform IS NULL AND split_part(dokument_id, '/', 1) IN ('NL', 'SF', 'DEL', 'INS', 'STV');
                UPDATE rettskilder SET kildeform = 'kunngjort'
                WHERE kildeform IS NULL AND split_part(dokument_id, '/', 1) = 'LTI';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_rettskilder_kildeform",
                table: "rettskilder",
                sql: "kildeform IS NULL OR kildeform IN ('konsolidert', 'kunngjort')");

            migrationBuilder.CreateIndex(
                name: "ix_rettskilde_disposisjonstyper_type",
                table: "rettskilde_disposisjonstyper",
                column: "disposisjonstype");

            migrationBuilder.CreateIndex(
                name: "ux_rettskilde_disposisjonstyper_rettskilde_type_kilde",
                table: "rettskilde_disposisjonstyper",
                columns: new[] { "rettskilde_id", "disposisjonstype", "kilde" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rettskilde_disposisjonstyper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rettskilder_kildeform",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "objekt_eli",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "kildeform",
                table: "rettskilder");
        }
    }
}
