using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilGruppeMedlemskap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gruppe_medlemskap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    overordnet_gruppe_begrep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    underordnet_gruppe_begrep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paragrafspenn_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    gyldig_fra = table.Column<DateOnly>(type: "date", nullable: true),
                    gyldig_til = table.Column<DateOnly>(type: "date", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gruppe_medlemskap_pkey", x => x.Id);
                    table.CheckConstraint("ck_gruppe_medlemskap_ikke_selv", "overordnet_gruppe_begrep_id <> underordnet_gruppe_begrep_id");
                    table.ForeignKey(
                        name: "FK_gruppe_medlemskap_begreper_overordnet_gruppe_begrep_id",
                        column: x => x.overordnet_gruppe_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gruppe_medlemskap_begreper_underordnet_gruppe_begrep_id",
                        column: x => x.underordnet_gruppe_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gruppe_medlemskap_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gruppe_medlemskap_hjemmel",
                table: "gruppe_medlemskap",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_gruppe_medlemskap_underordnet",
                table: "gruppe_medlemskap",
                column: "underordnet_gruppe_begrep_id");

            migrationBuilder.CreateIndex(
                name: "ux_gruppe_medlemskap_par",
                table: "gruppe_medlemskap",
                columns: new[] { "overordnet_gruppe_begrep_id", "underordnet_gruppe_begrep_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gruppe_medlemskap");
        }
    }
}
