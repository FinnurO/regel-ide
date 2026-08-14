using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilEksterneKilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eksterne_kilder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kildetype = table.Column<string>(type: "text", nullable: false),
                    ekstern_id = table.Column<string>(type: "text", nullable: false),
                    raa_json = table.Column<string>(type: "jsonb", nullable: false),
                    innholds_hash = table.Column<string>(type: "text", nullable: false),
                    hentet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("eksterne_kilder_pkey", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_eksterne_kilder_kildetype",
                table: "eksterne_kilder",
                column: "kildetype");

            migrationBuilder.CreateIndex(
                name: "ux_eksterne_kilder_kildetype_ekstern_id",
                table: "eksterne_kilder",
                columns: new[] { "kildetype", "ekstern_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eksterne_kilder");
        }
    }
}
