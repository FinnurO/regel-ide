using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
