using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #463, Johanns avgjørelse 2026-11-05 (modell B), klarsignal 2026-11-17] Utvider CHECK <c>ck_begreper_omradetype</c> med
    /// <c>rike</c> (<see cref="Omradetyper.Rike"/>): området Norge, hjemlet i Grunnloven § 1 første ledd. Migrasjonen skriver ingen data: Norge,
    /// staten og kantene er forslag og skrives av <see cref="NorgeStatenTjeneste"/> (<c>POST /api/norge-staten?skriv=true</c>), med tørrkjøring.
    /// <c>Down</c> nekter når et område har typen, i stedet for å la CHECK-en feile uten forklaring (samme mønster som #442).
    /// </summary>
    public partial class NorgeRike : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_omradetype",
                table: "begreper");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_omradetype",
                table: "begreper",
                sql: "omradetype IS NULL OR (begrepskategori = 'omrade' AND omradetype IN ('fylke', 'kommune', 'tettsted', 'lagsogn', 'lagdomme', 'helseregion', 'annet', 'rike'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE v_antall int;
                BEGIN
                  SELECT count(*) INTO v_antall FROM begreper WHERE omradetype = 'rike';
                  IF v_antall > 0 THEN
                    RAISE EXCEPTION 'Issue #463 Down: % område(r) har typen rike. Arkiver dem eller endre typen først.', v_antall;
                  END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_omradetype",
                table: "begreper");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_omradetype",
                table: "begreper",
                sql: "omradetype IS NULL OR (begrepskategori = 'omrade' AND omradetype IN ('fylke', 'kommune', 'tettsted', 'lagsogn', 'lagdomme', 'helseregion', 'annet'))");
        }
    }
}
