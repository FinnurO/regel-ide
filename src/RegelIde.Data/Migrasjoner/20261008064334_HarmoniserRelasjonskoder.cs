using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #330 «harmoniser gamle og nye relasjonskoder», 2026-10-08] Ren datamigrasjon (ingen
    /// skjemaendring — modellsnapshotet er uendret): de fem R-kodene fra før #311 konverteres til docs/33
    /// §4.3-kodene, med fra/til byttet der den gamle koden ble lest motsatt vei, og fjernes fra
    /// <c>relasjonstype_konfigurasjon</c>. Mapping, tellinger og avbruddsregler står i
    /// <see cref="RelasjonskodeHarmonisering"/>.
    /// <para>
    /// <c>Down</c> er presis for de radene denne migrasjonen rørte (den leser proveniensradene <c>Up</c> skrev),
    /// og lar kanter som er registrert med de nye kodene senere stå.
    /// </para>
    /// </summary>
    public partial class HarmoniserRelasjonskoder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RelasjonskodeHarmonisering.UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RelasjonskodeHarmonisering.DownSql);
        }
    }
}
