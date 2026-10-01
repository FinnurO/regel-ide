using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilTjenesteRegelverksreferanseforslag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tjeneste_regelverksreferanse_forslag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tjeneste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_eid = table.Column<string>(type: "text", nullable: false),
                    felt = table.Column<string>(type: "text", nullable: true),
                    begrunnelse = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Venter"),
                    ai_forslag_versjon = table.Column<string>(type: "text", nullable: true),
                    kilde_referanser_json = table.Column<string>(type: "jsonb", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    behandlet_av = table.Column<string>(type: "text", nullable: true),
                    behandlet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tjeneste_regelverksreferanse_forslag_pkey", x => x.Id);
                    table.CheckConstraint("ck_tjeneste_regelverksreferanse_forslag_status", "status IN ('Venter', 'Godkjent', 'Avvist')");
                    table.ForeignKey(
                        name: "FK_tjeneste_regelverksreferanse_forslag_rettskilder_til_rettsk~",
                        column: x => x.til_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tjeneste_regelverksreferanse_forslag_tjenester_tjeneste_id",
                        column: x => x.tjeneste_id,
                        principalTable: "tjenester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tjeneste_regelverksreferanse_forslag_til_rettskilde_id",
                table: "tjeneste_regelverksreferanse_forslag",
                column: "til_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_tjeneste_regelverksreferanse_forslag_par",
                table: "tjeneste_regelverksreferanse_forslag",
                columns: new[] { "tjeneste_id", "til_rettskilde_id", "til_eid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tjeneste_regelverksreferanse_forslag");
        }
    }
}
