using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilBegrepDefinisjonRelasjon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "begrep_definisjon_relasjon_kandidater",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    fra_forekomst_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_forekomst_id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalisert_definisjon = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Venter"),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    behandlet_av = table.Column<string>(type: "text", nullable: true),
                    behandlet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("begrep_definisjon_relasjon_kandidater_pkey", x => x.Id);
                    table.CheckConstraint("ck_begrep_def_rel_kandidater_ikke_selv", "fra_forekomst_id <> til_forekomst_id");
                    table.CheckConstraint("ck_begrep_def_rel_kandidater_status", "status IN ('Venter', 'Godkjent', 'Avvist')");
                    table.ForeignKey(
                        name: "FK_begrep_definisjon_relasjon_kandidater_begrepsforekomster_fr~",
                        column: x => x.fra_forekomst_id,
                        principalTable: "begrepsforekomster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_begrep_definisjon_relasjon_kandidater_begrepsforekomster_ti~",
                        column: x => x.til_forekomst_id,
                        principalTable: "begrepsforekomster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "begrep_definisjon_relasjoner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    fra_begrep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_begrep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kilde = table.Column<string>(type: "text", nullable: false),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("begrep_definisjon_relasjoner_pkey", x => x.Id);
                    table.CheckConstraint("ck_begrep_def_relasjoner_ikke_selv", "fra_begrep_id <> til_begrep_id");
                    table.CheckConstraint("ck_begrep_def_relasjoner_kilde", "kilde IN ('sveip', 'manuell')");
                    table.ForeignKey(
                        name: "FK_begrep_definisjon_relasjoner_begreper_fra_begrep_id",
                        column: x => x.fra_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_begrep_definisjon_relasjoner_begreper_til_begrep_id",
                        column: x => x.til_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_begrep_def_rel_kandidater_til",
                table: "begrep_definisjon_relasjon_kandidater",
                column: "til_forekomst_id");

            migrationBuilder.CreateIndex(
                name: "ux_begrep_def_rel_kandidater_par",
                table: "begrep_definisjon_relasjon_kandidater",
                columns: new[] { "fra_forekomst_id", "til_forekomst_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_begrep_definisjon_relasjoner_til_begrep_id",
                table: "begrep_definisjon_relasjoner",
                column: "til_begrep_id");

            migrationBuilder.CreateIndex(
                name: "ux_begrep_def_relasjoner_fra_til",
                table: "begrep_definisjon_relasjoner",
                columns: new[] { "fra_begrep_id", "til_begrep_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "begrep_definisjon_relasjon_kandidater");

            migrationBuilder.DropTable(
                name: "begrep_definisjon_relasjoner");
        }
    }
}
