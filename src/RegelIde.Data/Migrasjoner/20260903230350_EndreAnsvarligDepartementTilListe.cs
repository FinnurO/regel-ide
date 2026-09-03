using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Fler-verdi-departement, 2026-09-04] <c>ansvarlig_departement</c> går fra en enkelt <c>text</c>-
    /// kolonne (kommaseparert streng for flere departementer, issue #152) til en ekte Postgres
    /// <c>text[]</c> (samme mønster som <c>tjenester.malgruppe</c>/<c>tjenester.kanaler</c>) — se
    /// <see cref="RettskildeEntitet.AnsvarligDepartement"/> sin doc-kommentar.
    /// <para>
    /// Den auto-scaffoldede <c>AlterColumn</c> ble BEVISST erstattet med rå SQL her: Postgres kan ikke
    /// implisitt caste en eksisterende <c>text</c>-verdi til <c>text[]</c> (ville feilet mot enhver rad
    /// med en satt verdi — ~5900 rader i praksis, se AnsvarligDepartementBackfillTjeneste-kommentaren om
    /// dagens datavolum). <c>USING string_to_array(ansvarlig_departement, ', ')</c> konverterer den
    /// ALLEREDE korrekt kommaseparerte strengen (issue #152-fiksen, ", " som skilletegn — se
    /// LovdataHtmlParser.HentSammensattTekst) til en ekte liste i SAMME migrasjon, i stedet for å la
    /// eksisterende rader stå med en tom/null liste til neste resynk. <c>NULL</c> forblir <c>NULL</c>
    /// uendret (ingen Lovdata-importert Lov/Forskrift-rad skal få en gjettet, tom liste der den før
    /// bevisst manglet data helt).
    /// </para>
    /// </summary>
    public partial class EndreAnsvarligDepartementTilListe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE rettskilder
                ALTER COLUMN ansvarlig_departement TYPE text[]
                USING CASE
                    WHEN ansvarlig_departement IS NULL THEN NULL
                    ELSE string_to_array(ansvarlig_departement, ', ')
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Speilbilde av Up — samme ", "-skilletegn tilbake til én streng (tapsfri for enhver verdi
            // som faktisk kom derfra i Up, siden ingen departementnavn selv inneholder ", ").
            migrationBuilder.Sql(
                """
                ALTER TABLE rettskilder
                ALTER COLUMN ansvarlig_departement TYPE text
                USING CASE
                    WHEN ansvarlig_departement IS NULL THEN NULL
                    ELSE array_to_string(ansvarlig_departement, ', ')
                END;
                """);
        }
    }
}
