using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #355 «avslutning speiler innsetting», Johanns beslutninger 2026-10-09] Nye undertyper i CHECK
    /// <c>ck_strukturkanter_undertype</c> (oppnevning + utnevning/konstitusjon; avsetting: avsetting/oppsigelse/avskjed; vedtak:
    /// tilbakekall — <see cref="Strukturkanter.Undertyper"/>) og datadelen i <see cref="AvslutningMigrering"/>: K <c>forelegging</c> ut
    /// av typekonfigurasjonen (med proveniens), avbrudd hvis det finnes K forelegging-kanter (rettsvirkningen kan ikke avgjøres uten
    /// teksten). Teller før/etter; <c>Down</c> snur nøyaktig og nekter når det finnes en undertype som ikke kan uttrykkes før #355.
    /// Kjørt etter #353 (ingen stablede migrasjoner, sakens AC1).
    /// </summary>
    public partial class AvslutningSpeilerInnsetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_undertype",
                table: "strukturkanter");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_undertype",
                table: "strukturkanter",
                sql: AvslutningMigrering.UndertypeCheckEtter);

            migrationBuilder.Sql(AvslutningMigrering.UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Datadelen FØR den gamle CHECK-en: Down avbryter med en forklaring hvis en undertype ikke kan uttrykkes før #355, og
            // legger forelegging-koden inn igjen fra proveniensen.
            migrationBuilder.Sql(AvslutningMigrering.DownSql);

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_undertype",
                table: "strukturkanter");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_undertype",
                table: "strukturkanter",
                sql: AvslutningMigrering.UndertypeCheckFor);
        }
    }
}
