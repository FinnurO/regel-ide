using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilTaggKindKonfigurasjon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "taggkind_konfigurasjon",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kode = table.Column<string>(type: "text", nullable: false),
                    navn = table.Column<string>(type: "text", nullable: false),
                    farge = table.Column<string>(type: "text", nullable: false),
                    sorteringsrekkefolge = table.Column<int>(type: "integer", nullable: false),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("taggkind_konfigurasjon_pkey", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_taggkind_konfigurasjon_kode",
                table: "taggkind_konfigurasjon",
                column: "kode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "taggkind_konfigurasjon");
        }
    }
}
