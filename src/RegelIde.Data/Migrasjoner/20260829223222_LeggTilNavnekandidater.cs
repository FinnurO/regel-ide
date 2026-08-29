using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilNavnekandidater : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "navnekandidater",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    foreslatt_tekst = table.Column<string>(type: "text", nullable: false),
                    kategori = table.Column<string>(type: "text", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_eid = table.Column<string>(type: "text", nullable: false),
                    start_offset = table.Column<int>(type: "integer", nullable: false),
                    end_offset = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Venter"),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    behandlet_av = table.Column<string>(type: "text", nullable: true),
                    behandlet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("navnekandidater_pkey", x => x.Id);
                    table.CheckConstraint("ck_navnekandidater_kategori", "kategori IN ('virksomhet', 'rolle')");
                    table.CheckConstraint("ck_navnekandidater_status", "status IN ('Venter', 'Godkjent', 'Avvist')");
                    table.ForeignKey(
                        name: "FK_navnekandidater_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_navnekandidater_rettskilde",
                table: "navnekandidater",
                column: "rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_navnekandidater_rettskilde_node_start",
                table: "navnekandidater",
                columns: new[] { "rettskilde_id", "node_eid", "start_offset" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "navnekandidater");
        }
    }
}
