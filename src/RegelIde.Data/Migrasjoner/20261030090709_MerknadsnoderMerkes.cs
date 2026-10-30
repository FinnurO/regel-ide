using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #419, Johanns avgjørelser 2026-10-29/30] Kolonnen <c>rettskilde_noder.er_merknad</c> (merknader og veiledning i
    /// forskriftsteksten, <see cref="Merknadsnoder"/>) og tilbakefyllingen av eksisterende noder (<see cref="MerknadsnodeMigrering.TilbakefyllSql"/>:
    /// regel 1 + unntakslisten, frosset her). Idempotent. Målt i den delte basen 2026-10-29: 100 rettskilder, 6 991 noder (gjeldende versjoner).
    /// <c>Down</c> fjerner kolonnen (merkingen er avledet av eId-ene og kan alltid fylles på nytt).
    /// </summary>
    public partial class MerknadsnoderMerkes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "er_merknad",
                table: "rettskilde_noder",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(MerknadsnodeMigrering.TilbakefyllSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "er_merknad",
                table: "rettskilde_noder");
        }
    }
}
