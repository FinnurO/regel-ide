using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilVeiledningStotte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rekkefolge",
                table: "regelnode_barn",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "datasett_verdier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    datasett_id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verdi = table.Column<string>(type: "jsonb", nullable: false),
                    kilde = table.Column<string>(type: "text", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("datasett_verdier_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_datasett_verdier_datasett_datasett_id",
                        column: x => x.datasett_id,
                        principalTable: "datasett",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_datasett_verdier_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "vilkarstre_kommentarer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mal_type = table.Column<string>(type: "text", nullable: false),
                    mal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dokumenttype = table.Column<string>(type: "text", nullable: false),
                    tekst_html = table.Column<string>(type: "text", nullable: false),
                    rekkefolge = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vilkarstre_kommentarer_pkey", x => x.Id);
                    table.CheckConstraint("ck_vilkarstre_kommentarer_mal_type", "mal_type IN ('vilkar', 'regelnode', 'unntak')");
                    table.ForeignKey(
                        name: "FK_vilkarstre_kommentarer_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_datasett_verdier_virksomhet_id",
                table: "datasett_verdier",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ux_datasett_verdier_datasett_virksomhet",
                table: "datasett_verdier",
                columns: new[] { "datasett_id", "virksomhet_id" },
                unique: true,
                filter: "virksomhet_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_datasett_verdier_standardverdi",
                table: "datasett_verdier",
                column: "datasett_id",
                unique: true,
                filter: "virksomhet_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vilkarstre_kommentarer_mal",
                table: "vilkarstre_kommentarer",
                columns: new[] { "mal_type", "mal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_vilkarstre_kommentarer_virksomhet_id",
                table: "vilkarstre_kommentarer",
                column: "virksomhet_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "datasett_verdier");

            migrationBuilder.DropTable(
                name: "vilkarstre_kommentarer");

            migrationBuilder.DropColumn(
                name: "rekkefolge",
                table: "regelnode_barn");
        }
    }
}
