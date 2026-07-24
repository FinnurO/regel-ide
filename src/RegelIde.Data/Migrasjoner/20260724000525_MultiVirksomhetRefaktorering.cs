using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class MultiVirksomhetRefaktorering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "tekst_tagger_unik_tagg",
                table: "tekst_tagger");

            migrationBuilder.DropIndex(
                name: "ux_rettskilder_eli_gjeldende",
                table: "rettskilder");

            migrationBuilder.AddColumn<Guid>(
                name: "virksomhet_id",
                table: "tekst_tagger",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "virksomhet_id",
                table: "rettskilder",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "virksomhet_id",
                table: "proveniens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "virksomheter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    navn = table.Column<string>(type: "text", nullable: false),
                    organisasjonsnummer = table.Column<string>(type: "text", nullable: true),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("virksomheter_pkey", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tekst_tagger_virksomhet",
                table: "tekst_tagger",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "tekst_tagger_unik_tagg",
                table: "tekst_tagger",
                columns: new[] { "virksomhet_id", "rettskilde_id", "node_eid", "start_offset", "end_offset", "kind", "ref_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rettskilder_eli_gjeldende_delt",
                table: "rettskilder",
                column: "eli",
                unique: true,
                filter: "entitetsstatus = 'gjeldende' AND virksomhet_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_rettskilder_eli_gjeldende_per_virksomhet",
                table: "rettskilder",
                columns: new[] { "virksomhet_id", "eli" },
                unique: true,
                filter: "entitetsstatus = 'gjeldende' AND virksomhet_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_proveniens_virksomhet_id",
                table: "proveniens",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ux_virksomheter_organisasjonsnummer",
                table: "virksomheter",
                column: "organisasjonsnummer",
                unique: true,
                filter: "organisasjonsnummer IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_proveniens_virksomheter_virksomhet_id",
                table: "proveniens",
                column: "virksomhet_id",
                principalTable: "virksomheter",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_rettskilder_virksomheter_virksomhet_id",
                table: "rettskilder",
                column: "virksomhet_id",
                principalTable: "virksomheter",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_tekst_tagger_virksomheter_virksomhet_id",
                table: "tekst_tagger",
                column: "virksomhet_id",
                principalTable: "virksomheter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_proveniens_virksomheter_virksomhet_id",
                table: "proveniens");

            migrationBuilder.DropForeignKey(
                name: "FK_rettskilder_virksomheter_virksomhet_id",
                table: "rettskilder");

            migrationBuilder.DropForeignKey(
                name: "FK_tekst_tagger_virksomheter_virksomhet_id",
                table: "tekst_tagger");

            migrationBuilder.DropTable(
                name: "virksomheter");

            migrationBuilder.DropIndex(
                name: "ix_tekst_tagger_virksomhet",
                table: "tekst_tagger");

            migrationBuilder.DropIndex(
                name: "tekst_tagger_unik_tagg",
                table: "tekst_tagger");

            migrationBuilder.DropIndex(
                name: "ux_rettskilder_eli_gjeldende_delt",
                table: "rettskilder");

            migrationBuilder.DropIndex(
                name: "ux_rettskilder_eli_gjeldende_per_virksomhet",
                table: "rettskilder");

            migrationBuilder.DropIndex(
                name: "IX_proveniens_virksomhet_id",
                table: "proveniens");

            migrationBuilder.DropColumn(
                name: "virksomhet_id",
                table: "tekst_tagger");

            migrationBuilder.DropColumn(
                name: "virksomhet_id",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "virksomhet_id",
                table: "proveniens");

            migrationBuilder.CreateIndex(
                name: "tekst_tagger_unik_tagg",
                table: "tekst_tagger",
                columns: new[] { "rettskilde_id", "node_eid", "start_offset", "end_offset", "kind", "ref_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rettskilder_eli_gjeldende",
                table: "rettskilder",
                column: "eli",
                unique: true,
                filter: "entitetsstatus = 'gjeldende'");
        }
    }
}
