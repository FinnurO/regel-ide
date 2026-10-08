using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #341 «kompetanse med motpart og typologi», 2026-10-08] Nye K-felt på kanten (<c>normform</c>,
    /// <c>grunnlag</c>, <c>delegerbar</c>, alle NULL = ikke angitt), <c>familie</c> og <c>fvl_kategori</c> på typen
    /// (Johanns hierarkibeslutning), selvregulering tillatt som selvkant på K normgivning, og datadelen i
    /// <see cref="KompetanseMigrering"/>: R-myndighetsrelasjonene til K med motpart, K forskrift → normgivning/forskrift,
    /// hjemmelssted ut av avgrensningen (feilen fra #311) og typekonfigurasjonen. Teller før/etter og avbryter ved avvik.
    /// </summary>
    public partial class KompetanseMedMotpart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_ikke_selv",
                table: "strukturkanter");

            migrationBuilder.AddColumn<bool>(
                name: "delegerbar",
                table: "strukturkanter",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grunnlag",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normform",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "familie",
                table: "relasjonstype_konfigurasjon",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fvl_kategori",
                table: "relasjonstype_konfigurasjon",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_delegerbar",
                table: "strukturkanter",
                sql: "delegerbar IS NULL OR kategori = 'K'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_grunnlag",
                table: "strukturkanter",
                sql: "grunnlag IS NULL OR (kategori = 'K' AND grunnlag IN ('offentligrettslig', 'privatrettslig'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_ikke_selv",
                table: "strukturkanter",
                sql: "(kategori = 'K' AND typekode = 'normgivning') OR ((fra_virksomhet_id IS NULL OR til_virksomhet_id IS NULL OR fra_virksomhet_id <> til_virksomhet_id) AND (fra_begrep_id IS NULL OR til_begrep_id IS NULL OR fra_begrep_id <> til_begrep_id))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_normform",
                table: "strukturkanter",
                sql: "normform IS NULL OR (kategori = 'K' AND typekode = 'normgivning' AND normform IN ('forskrift', 'reglement', 'arbeidsordning', 'vedtekter', 'instruks'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_familie",
                table: "relasjonstype_konfigurasjon",
                sql: "familie IS NULL OR (kategori = 'K' AND familie IN ('struktur', 'personell', 'styring', 'normgivning', 'kontroll', 'klage_overproving', 'vedtak', 'sanksjon'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_fvl_kategori",
                table: "relasjonstype_konfigurasjon",
                sql: "fvl_kategori IS NULL OR (kategori = 'K' AND fvl_kategori IN ('forskrift', 'enkeltvedtak', 'ikke_vedtak'))");

            // Datadelen etter kolonnene og CHECK-ene: konverteringen setter typekode og normform i samme UPDATE.
            migrationBuilder.Sql(KompetanseMigrering.UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Datadelen FØR kolonnene fjernes — Down leser proveniensradene og skriver de gamle verdiene tilbake.
            migrationBuilder.Sql(KompetanseMigrering.DownSql);

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_delegerbar",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_grunnlag",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_ikke_selv",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_normform",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_familie",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_fvl_kategori",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.DropColumn(
                name: "delegerbar",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "grunnlag",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "normform",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "familie",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.DropColumn(
                name: "fvl_kategori",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_ikke_selv",
                table: "strukturkanter",
                sql: "(fra_virksomhet_id IS NULL OR til_virksomhet_id IS NULL OR fra_virksomhet_id <> til_virksomhet_id) AND (fra_begrep_id IS NULL OR til_begrep_id IS NULL OR fra_begrep_id <> til_begrep_id)");
        }
    }
}
