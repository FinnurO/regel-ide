using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilEksternNavneoppslagCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "oppdagelses_kilde",
                table: "navnekandidater",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ekstern_navneoppslag_cache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    term = table.Column<string>(type: "text", nullable: false),
                    kilde = table.Column<string>(type: "text", nullable: false),
                    treff = table.Column<bool>(type: "boolean", nullable: false),
                    taksonomi_kategori = table.Column<string>(type: "text", nullable: true),
                    alias_json = table.Column<string>(type: "text", nullable: true),
                    organisasjonsnummer_funnet = table.Column<string>(type: "text", nullable: true),
                    ekstern_url = table.Column<string>(type: "text", nullable: true),
                    slaopp_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ekstern_navneoppslag_cache_pkey", x => x.Id);
                    table.CheckConstraint("ck_ekstern_navneoppslag_cache_kilde", "kilde IN ('snl', 'ssr')");
                });

            migrationBuilder.CreateIndex(
                name: "ux_ekstern_navneoppslag_cache_term_kilde",
                table: "ekstern_navneoppslag_cache",
                columns: new[] { "term", "kilde" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ekstern_navneoppslag_cache");

            migrationBuilder.DropColumn(
                name: "oppdagelses_kilde",
                table: "navnekandidater");
        }
    }
}
