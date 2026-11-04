using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class AktorkoSkriving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "aktorko_gruppe",
                table: "navnekandidater",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hjemmel_art",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_hjemmel_art",
                table: "begreper",
                sql: "hjemmel_art IS NULL OR (hjemmel_art IN ('oppretting', 'navnevedtak') AND lovreferanse_eid IS NOT NULL)");

            // [Ny, issue #441, Johanns avgjørelse 2026-10-30] Tilbakefyll: navneformene livsløpet skrev med hjemmel i NAVNEVEDTAKET
            // (DepartementNavneformHjemmelTjeneste, oppdagelses_kilde 'monster:livslop-navnevedtak', docs/33 §5.10). Bare de radene: der vet vi
            // av konstruksjon at hjemmelen er navnevedtaket. Avviste rader får ingen art. Målt mot en kopi av den delte basen 2026-11-04:
            // 13 rader (1 godkjent, 12 forslag). #441 sa 14 aktører; forskjellen står i PR-en. Andre rader gjettes ikke (CLAUDE.md §8).
            migrationBuilder.Sql(
                "UPDATE begreper SET hjemmel_art = 'navnevedtak' WHERE oppdagelses_kilde = 'monster:livslop-navnevedtak' "
                + "AND lovreferanse_eid IS NOT NULL AND godkjenning IS DISTINCT FROM 'avvist' AND hjemmel_art IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_hjemmel_art",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "aktorko_gruppe",
                table: "navnekandidater");

            migrationBuilder.DropColumn(
                name: "hjemmel_art",
                table: "begreper");
        }
    }
}
