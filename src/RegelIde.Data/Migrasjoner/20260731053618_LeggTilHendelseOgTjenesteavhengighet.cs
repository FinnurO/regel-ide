using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilHendelseOgTjenesteavhengighet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hendelser",
                table: "tjenester");

            migrationBuilder.DropColumn(
                name: "tjenesteavhengigheter",
                table: "tjenester");

            migrationBuilder.CreateTable(
                name: "hendelser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    navn = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    beskrivelse = table.Column<string>(type: "text", nullable: true),
                    versjon = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hendelser_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hendelser_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tjeneste_hendelser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tjeneste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hendelse_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tjeneste_hendelser_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tjeneste_hendelser_hendelser_hendelse_id",
                        column: x => x.hendelse_id,
                        principalTable: "hendelser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tjeneste_hendelser_tjenester_tjeneste_id",
                        column: x => x.tjeneste_id,
                        principalTable: "tjenester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tjenesteavhengigheter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fra_tjeneste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_tjeneste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rel = table.Column<string>(type: "text", nullable: false),
                    hendelse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beskrivelse = table.Column<string>(type: "text", nullable: true),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tjenesteavhengigheter_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tjenesteavhengigheter_hendelser_hendelse_id",
                        column: x => x.hendelse_id,
                        principalTable: "hendelser",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tjenesteavhengigheter_tjenester_fra_tjeneste_id",
                        column: x => x.fra_tjeneste_id,
                        principalTable: "tjenester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tjenesteavhengigheter_tjenester_til_tjeneste_id",
                        column: x => x.til_tjeneste_id,
                        principalTable: "tjenester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tjenesteavhengigheter_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hendelser_virksomhet",
                table: "hendelser",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "IX_tjeneste_hendelser_hendelse_id",
                table: "tjeneste_hendelser",
                column: "hendelse_id");

            migrationBuilder.CreateIndex(
                name: "ux_tjeneste_hendelser",
                table: "tjeneste_hendelser",
                columns: new[] { "tjeneste_id", "hendelse_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tjenesteavhengigheter_fra",
                table: "tjenesteavhengigheter",
                column: "fra_tjeneste_id");

            migrationBuilder.CreateIndex(
                name: "IX_tjenesteavhengigheter_hendelse_id",
                table: "tjenesteavhengigheter",
                column: "hendelse_id");

            migrationBuilder.CreateIndex(
                name: "ix_tjenesteavhengigheter_til",
                table: "tjenesteavhengigheter",
                column: "til_tjeneste_id");

            migrationBuilder.CreateIndex(
                name: "IX_tjenesteavhengigheter_virksomhet_id",
                table: "tjenesteavhengigheter",
                column: "virksomhet_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tjeneste_hendelser");

            migrationBuilder.DropTable(
                name: "tjenesteavhengigheter");

            migrationBuilder.DropTable(
                name: "hendelser");

            migrationBuilder.AddColumn<string>(
                name: "hendelser",
                table: "tjenester",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "tjenesteavhengigheter",
                table: "tjenester",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }
    }
}
