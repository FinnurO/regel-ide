using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilParallellnavnOgSsrSted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ekstern_navneoppslag_cache_kilde",
                table: "ekstern_navneoppslag_cache");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper");

            migrationBuilder.AddColumn<string>(
                name: "skrivemate_json",
                table: "ekstern_navneoppslag_cache",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ekstern_navneoppslag_cache_kilde",
                table: "ekstern_navneoppslag_cache",
                sql: "kilde IN ('snl', 'ssr', 'ssr-sted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper",
                sql: "navneformgrunn IS NULL OR navneformgrunn IN ('gjeldende', 'utgatt', 'kortform', 'feilskriving', 'parallellnavn')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ekstern_navneoppslag_cache_kilde",
                table: "ekstern_navneoppslag_cache");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "skrivemate_json",
                table: "ekstern_navneoppslag_cache");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ekstern_navneoppslag_cache_kilde",
                table: "ekstern_navneoppslag_cache",
                sql: "kilde IN ('snl', 'ssr')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_navneformgrunn",
                table: "begreper",
                sql: "navneformgrunn IS NULL OR navneformgrunn IN ('gjeldende', 'utgatt', 'kortform', 'feilskriving')");
        }
    }
}
