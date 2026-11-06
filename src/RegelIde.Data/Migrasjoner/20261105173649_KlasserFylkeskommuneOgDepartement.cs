using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #442, Johanns avgjørelse 2026-11-05] Utvider CHECK <c>ck_begreper_medlemskapsregel</c> med regelen for klassen «fylkeskommune»
    /// (<c>omraderegister_fylkeskommune</c>, kommuneloven § 3-1 første ledd) og for klassen «departement» (<c>organiseringsvedtak_departement</c>).
    /// Speilet er <see cref="Klasseregler.Alle"/>. Migrasjonen skriver ingen data: klassene, territoriekantene og medlemskantene er forslag og
    /// skrives av <see cref="KlasseoppsettTjeneste"/> (<c>POST /api/klasser/fylkeskommune-departement?skriv=true</c>), med tørrkjøring.
    /// <c>Down</c> nekter når en klasse bruker en av de nye reglene, i stedet for å la CHECK-en feile uten forklaring.
    /// </summary>
    public partial class KlasserFylkeskommuneOgDepartement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_medlemskapsregel",
                table: "begreper");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_medlemskapsregel",
                table: "begreper",
                sql: "medlemskapsregel IS NULL OR (begrepskategori = 'klasse' AND medlemskapsregel IN ('omraderegister_kommune', 'omraderegister_fylkeskommune', 'organiseringsvedtak_departement'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE v_antall int;
                BEGIN
                  SELECT count(*) INTO v_antall FROM begreper
                  WHERE medlemskapsregel IN ('omraderegister_fylkeskommune', 'organiseringsvedtak_departement');
                  IF v_antall > 0 THEN
                    RAISE EXCEPTION 'Issue #442 Down: % klasse(r) bruker en medlemskapsregel fra #442. Arkiver dem eller fjern regelen først.', v_antall;
                  END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_medlemskapsregel",
                table: "begreper");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_medlemskapsregel",
                table: "begreper",
                sql: "medlemskapsregel IS NULL OR (begrepskategori = 'klasse' AND medlemskapsregel IN ('omraderegister_kommune'))");
        }
    }
}
