using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #508, prosjekteierens avgjørelse 2026-11-25 og 2026-11-27] Kolonnen nummer_kilde: hvor nummer kommer fra (trykt etikett
    /// eller løpenummer). Alle eksisterende ledd og punkt fra Lovdata får <c>lopenummer</c>, fordi det er det nummer er i dag. Da vises et
    /// løpenummer aldri som en trykt bokstav (juristrunden J5). Den trykte etiketten fylles inn av samlekjøringen i #508 PR 3, med logg per
    /// node, eller kommer med en ny versjon. Underledd (node_type 'underledd') trenger ingen skjemaendring: node_type har ingen CHECK.
    /// </summary>
    public partial class NummerkildeOgUnderledd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS: HistoriskSkjema i Data.Tests legger kolonnen til på eldre skjema før senere migrasjoner kjøres (samme mønster
            // som er_merknad og endringsrolle).
            migrationBuilder.Sql("ALTER TABLE rettskilde_noder ADD COLUMN IF NOT EXISTS nummer_kilde text;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rettskilde_noder_nummer_kilde",
                table: "rettskilde_noder",
                sql: "nummer_kilde IS NULL OR nummer_kilde IN ('bokstav', 'tall', 'romertall', 'trykt', 'strek', 'lopenummer')");

            migrationBuilder.Sql(
                "UPDATE rettskilde_noder SET nummer_kilde = 'lopenummer' " +
                "WHERE kildesystem = 'lovdata' AND node_type IN ('ledd', 'punkt') AND nummer IS NOT NULL AND nummer_kilde IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rettskilde_noder_nummer_kilde",
                table: "rettskilde_noder");

            migrationBuilder.DropColumn(
                name: "nummer_kilde",
                table: "rettskilde_noder");
        }
    }
}
