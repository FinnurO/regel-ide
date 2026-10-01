using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #285 AC5, KI-oppdagelse-runden] Lukker et bekreftet gap: <c>virksomhet_relasjoner</c>,
    /// <c>myndighetstildelinger</c> og <c>gruppe_medlemskap</c> hadde INGEN statusfelt i det hele tatt før
    /// denne migrasjonen — en KI-opprettet rad (se
    /// <see cref="RegelIde.Data.VirksomhetOgGruppeKiOppdagelseTjeneste"/>) kunne ikke skilles fra en
    /// menneske-opprettet rad, og hadde ingen «venter på revisjon»-tilstand.
    /// <para>
    /// <c>NOT NULL DEFAULT 'validert'</c> — bevisst, ikke bare en EF-konvensjon: den fyller ALLE
    /// eksisterende rader (menneske-drevet flyt fra PR #284/issue #164/#283) med <c>'validert'</c> i
    /// samme migrasjon, uten en egen UPDATE-setning. Ingen atferdsendring for eksisterende bruk — disse
    /// radene var alltid ment å være gjeldende med det samme, og skal ALDRI havne i en revisjonskø de
    /// aldri ba om (Johanns eksplisitte krav, issue #285 AC5).
    /// </para>
    /// </summary>
    public partial class LeggTilStatusPaRelasjonerOgTildelinger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "virksomhet_relasjoner",
                type: "text",
                nullable: false,
                defaultValue: "validert");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "myndighetstildelinger",
                type: "text",
                nullable: false,
                defaultValue: "validert");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "gruppe_medlemskap",
                type: "text",
                nullable: false,
                defaultValue: "validert");

            migrationBuilder.AddCheckConstraint(
                name: "ck_virksomhet_relasjoner_status",
                table: "virksomhet_relasjoner",
                sql: "status IN ('foreslatt_av_ai', 'validert')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_myndighetstildelinger_status",
                table: "myndighetstildelinger",
                sql: "status IN ('foreslatt_av_ai', 'validert')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_gruppe_medlemskap_status",
                table: "gruppe_medlemskap",
                sql: "status IN ('foreslatt_av_ai', 'validert')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_virksomhet_relasjoner_status",
                table: "virksomhet_relasjoner");

            migrationBuilder.DropCheckConstraint(
                name: "ck_myndighetstildelinger_status",
                table: "myndighetstildelinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gruppe_medlemskap_status",
                table: "gruppe_medlemskap");

            migrationBuilder.DropColumn(
                name: "status",
                table: "virksomhet_relasjoner");

            migrationBuilder.DropColumn(
                name: "status",
                table: "myndighetstildelinger");

            migrationBuilder.DropColumn(
                name: "status",
                table: "gruppe_medlemskap");
        }
    }
}
