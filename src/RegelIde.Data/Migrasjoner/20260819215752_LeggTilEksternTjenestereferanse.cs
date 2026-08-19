using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilEksternTjenestereferanse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "til_tjeneste_id",
                table: "tjenesteavhengigheter",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "til_ekstern_referanse_id",
                table: "tjenesteavhengigheter",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "eksterne_tjenestereferanser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisasjonsnummer = table.Column<string>(type: "text", nullable: false),
                    navn = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("eksterne_tjenestereferanser_pkey", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tjenesteavhengigheter_til_ekstern",
                table: "tjenesteavhengigheter",
                column: "til_ekstern_referanse_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tjenesteavhengigheter_ett_mal",
                table: "tjenesteavhengigheter",
                sql: "(til_tjeneste_id IS NOT NULL AND til_ekstern_referanse_id IS NULL) OR (til_tjeneste_id IS NULL AND til_ekstern_referanse_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_eksterne_tjenestereferanser_orgnr_navn",
                table: "eksterne_tjenestereferanser",
                columns: new[] { "organisasjonsnummer", "navn" });

            migrationBuilder.AddForeignKey(
                name: "FK_tjenesteavhengigheter_eksterne_tjenestereferanser_til_ekste~",
                table: "tjenesteavhengigheter",
                column: "til_ekstern_referanse_id",
                principalTable: "eksterne_tjenestereferanser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tjenesteavhengigheter_eksterne_tjenestereferanser_til_ekste~",
                table: "tjenesteavhengigheter");

            migrationBuilder.DropTable(
                name: "eksterne_tjenestereferanser");

            migrationBuilder.DropIndex(
                name: "ix_tjenesteavhengigheter_til_ekstern",
                table: "tjenesteavhengigheter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tjenesteavhengigheter_ett_mal",
                table: "tjenesteavhengigheter");

            migrationBuilder.DropColumn(
                name: "til_ekstern_referanse_id",
                table: "tjenesteavhengigheter");

            migrationBuilder.AlterColumn<Guid>(
                name: "til_tjeneste_id",
                table: "tjenesteavhengigheter",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
