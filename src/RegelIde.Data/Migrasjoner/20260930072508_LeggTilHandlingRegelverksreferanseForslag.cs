using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilHandlingRegelverksreferanseForslag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "handling_regelverksreferanse_forslag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    handling_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_eid = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("handling_regelverksreferanse_forslag_pkey", x => x.Id);
                    table.CheckConstraint("ck_handling_regelverksreferanse_forslag_status", "status IN ('Venter', 'Godkjent', 'Avvist')");
                    table.ForeignKey(
                        name: "FK_handling_regelverksreferanse_forslag_handlinger_handling_id",
                        column: x => x.handling_id,
                        principalTable: "handlinger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_handling_regelverksreferanse_forslag_rettskilder_til_rettsk~",
                        column: x => x.til_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_handling_regelverksreferanse_forslag_til_rettskilde_id",
                table: "handling_regelverksreferanse_forslag",
                column: "til_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_handling_regelverksreferanse_forslag_par",
                table: "handling_regelverksreferanse_forslag",
                columns: new[] { "handling_id", "til_rettskilde_id", "til_eid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "handling_regelverksreferanse_forslag");
        }
    }
}
