using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #368 «oppgave/funksjon (kategori F)», Johanns beslutning 2026-10-15] Ny kategori F i strukturkanter og
    /// typekonfigurasjonen; modalitet lov på F; F uten til-node; objekt påkrevd på F (ck_strukturkanter_oppgave_objekt). Datadelen i
    /// <see cref="OppgaveMigrering"/>: de åtte F-typene og BARE det juristrunden avgjorde for de lokale kandidatene. Teller før/etter og
    /// avbryter ved avvik; <c>Down</c> kjører datadelen FØR de gamle CHECK-ene legges tilbake. Kjørt etter #367/#313 (ikke stablet).
    /// </summary>
    public partial class OppgaveFunksjon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_kategori",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_modalitet",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_til_pakrevd",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_kategori",
                table: "strukturkanter",
                sql: "kategori IN ('R', 'K', 'P', 'F', 'M', 'O', 'A', 'G', 'I', 'T')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_modalitet",
                table: "strukturkanter",
                sql: "modalitet IS NULL OR (kategori IN ('P', 'F') AND modalitet IN ('skal', 'kan', 'bor'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_oppgave_objekt",
                table: "strukturkanter",
                sql: "kategori <> 'F' OR objekt IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_til_pakrevd",
                table: "strukturkanter",
                sql: "kategori IN ('K', 'P', 'F', 'T') OR til_virksomhet_id IS NOT NULL OR til_begrep_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon",
                sql: "kategori IN ('R', 'K', 'P', 'F', 'M', 'O', 'A', 'G', 'I', 'T')");

            migrationBuilder.Sql(OppgaveMigrering.UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(OppgaveMigrering.DownSql);

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_kategori",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_modalitet",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_oppgave_objekt",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_til_pakrevd",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_kategori",
                table: "strukturkanter",
                sql: "kategori IN ('R', 'K', 'P', 'M', 'O', 'A', 'G', 'I', 'T')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_modalitet",
                table: "strukturkanter",
                sql: "modalitet IS NULL OR (kategori = 'P' AND modalitet IN ('skal', 'kan', 'bor'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_til_pakrevd",
                table: "strukturkanter",
                sql: "kategori IN ('K', 'P', 'T') OR til_virksomhet_id IS NOT NULL OR til_begrep_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon",
                sql: "kategori IN ('R', 'K', 'P', 'M', 'O', 'A', 'G', 'I', 'T')");
        }
    }
}
