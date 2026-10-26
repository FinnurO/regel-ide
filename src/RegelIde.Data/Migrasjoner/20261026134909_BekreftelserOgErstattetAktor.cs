using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class BekreftelserOgErstattetAktor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // [Ny, issue #397] IF NOT EXISTS som i NedlagteAktorerOgEndringsmarkor: migrasjonstestene (HistoriskSkjema) legger kolonnen til.
            migrationBuilder.Sql("ALTER TABLE virksomheter ADD COLUMN IF NOT EXISTS erstattet_av_id uuid;");

            migrationBuilder.CreateTable(
                name: "strukturkant_bekreftelser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hjemmel_eid = table.Column<string>(type: "text", nullable: false),
                    dato = table.Column<DateOnly>(type: "date", nullable: true),
                    kommentar = table.Column<string>(type: "text", nullable: true),
                    oppdagelses_kilde = table.Column<string>(type: "text", nullable: false),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("strukturkant_bekreftelser_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_strukturkant_bekreftelser_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_strukturkant_bekreftelser_strukturkanter_kant_id",
                        column: x => x.kant_id,
                        principalTable: "strukturkanter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_virksomheter_erstattet_av_id",
                table: "virksomheter",
                column: "erstattet_av_id");

            migrationBuilder.CreateIndex(
                name: "IX_strukturkant_bekreftelser_hjemmel_rettskilde_id",
                table: "strukturkant_bekreftelser",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_strukturkant_bekreftelser",
                table: "strukturkant_bekreftelser",
                columns: new[] { "kant_id", "hjemmel_rettskilde_id", "hjemmel_eid" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_virksomheter_virksomheter_erstattet_av_id",
                table: "virksomheter",
                column: "erstattet_av_id",
                principalTable: "virksomheter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_virksomheter_virksomheter_erstattet_av_id",
                table: "virksomheter");

            migrationBuilder.DropTable(
                name: "strukturkant_bekreftelser");

            migrationBuilder.DropIndex(
                name: "IX_virksomheter_erstattet_av_id",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "erstattet_av_id",
                table: "virksomheter");
        }
    }
}
