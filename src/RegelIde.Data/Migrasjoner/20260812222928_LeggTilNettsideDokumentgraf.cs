using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilNettsideDokumentgraf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nettside_dokumenter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kanonisk_url = table.Column<string>(type: "text", nullable: false),
                    tittel = table.Column<string>(type: "text", nullable: true),
                    raa_tekst = table.Column<string>(type: "text", nullable: true),
                    innholds_hash = table.Column<string>(type: "text", nullable: true),
                    hentet = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("nettside_dokumenter_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nettside_dokumenter_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "nettside_lenker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    fra_nettside_dokument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    raa_href = table.Column<string>(type: "text", nullable: false),
                    anker_tekst = table.Column<string>(type: "text", nullable: true),
                    til_nettside_dokument_id = table.Column<Guid>(type: "uuid", nullable: true),
                    til_eid_kandidat = table.Column<string>(type: "text", nullable: true),
                    til_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("nettside_lenker_pkey", x => x.Id);
                    table.CheckConstraint("ck_nettside_lenker_type", "type IN ('lenker_til', 'lovdatalenke')");
                    table.ForeignKey(
                        name: "FK_nettside_lenker_nettside_dokumenter_fra_nettside_dokument_id",
                        column: x => x.fra_nettside_dokument_id,
                        principalTable: "nettside_dokumenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nettside_lenker_nettside_dokumenter_til_nettside_dokument_id",
                        column: x => x.til_nettside_dokument_id,
                        principalTable: "nettside_dokumenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_nettside_lenker_rettskilder_til_rettskilde_id",
                        column: x => x.til_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "nettside_stier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nettside_dokument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sti = table.Column<string>(type: "text", nullable: false),
                    sti_type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("nettside_stier_pkey", x => x.Id);
                    table.CheckConstraint("ck_nettside_stier_stitype", "sti_type IN ('tematisk', 'organisatorisk')");
                    table.ForeignKey(
                        name: "FK_nettside_stier_nettside_dokumenter_nettside_dokument_id",
                        column: x => x.nettside_dokument_id,
                        principalTable: "nettside_dokumenter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nettside_dokumenter_virksomhet_id",
                table: "nettside_dokumenter",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ux_nettside_dokumenter_kanonisk_url",
                table: "nettside_dokumenter",
                column: "kanonisk_url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nettside_lenker_fra_href",
                table: "nettside_lenker",
                columns: new[] { "fra_nettside_dokument_id", "raa_href" });

            migrationBuilder.CreateIndex(
                name: "IX_nettside_lenker_til_nettside_dokument_id",
                table: "nettside_lenker",
                column: "til_nettside_dokument_id");

            migrationBuilder.CreateIndex(
                name: "ix_nettside_lenker_til_rettskilde",
                table: "nettside_lenker",
                column: "til_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_nettside_stier_dokument_type_sti",
                table: "nettside_stier",
                columns: new[] { "nettside_dokument_id", "sti_type", "sti" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nettside_lenker");

            migrationBuilder.DropTable(
                name: "nettside_stier");

            migrationBuilder.DropTable(
                name: "nettside_dokumenter");
        }
    }
}
