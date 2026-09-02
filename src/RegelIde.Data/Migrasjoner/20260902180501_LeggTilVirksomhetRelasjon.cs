using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilVirksomhetRelasjon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "relasjonstype_konfigurasjon",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kode = table.Column<string>(type: "text", nullable: false),
                    fra_visningsmal = table.Column<string>(type: "text", nullable: false),
                    til_visningsmal = table.Column<string>(type: "text", nullable: false),
                    sorteringsrekkefolge = table.Column<int>(type: "integer", nullable: false),
                    aktiv = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("relasjonstype_konfigurasjon_pkey", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "virksomhet_relasjoner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    fra_virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relasjons_type = table.Column<string>(type: "text", nullable: false),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hjemmel_eid = table.Column<string>(type: "text", nullable: true),
                    kommentar = table.Column<string>(type: "text", nullable: true),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("virksomhet_relasjoner_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_virksomhet_relasjoner_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_virksomhet_relasjoner_virksomheter_fra_virksomhet_id",
                        column: x => x.fra_virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_virksomhet_relasjoner_virksomheter_til_virksomhet_id",
                        column: x => x.til_virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_relasjonstype_konfigurasjon_kode",
                table: "relasjonstype_konfigurasjon",
                column: "kode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_virksomhet_relasjoner_fra",
                table: "virksomhet_relasjoner",
                column: "fra_virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "IX_virksomhet_relasjoner_hjemmel_rettskilde_id",
                table: "virksomhet_relasjoner",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_virksomhet_relasjoner_til",
                table: "virksomhet_relasjoner",
                column: "til_virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ux_virksomhet_relasjoner_fra_til_type",
                table: "virksomhet_relasjoner",
                columns: new[] { "fra_virksomhet_id", "til_virksomhet_id", "relasjons_type" },
                unique: true,
                filter: "entitetsstatus = 'gjeldende'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "relasjonstype_konfigurasjon");

            migrationBuilder.DropTable(
                name: "virksomhet_relasjoner");
        }
    }
}
