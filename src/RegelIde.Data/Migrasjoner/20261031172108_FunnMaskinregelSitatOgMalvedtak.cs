using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>[Ny, #429 PR 2] Maskinregelen for utgåtte aktører: <c>funn_koblinger.quote_exact</c> (forekomsten forankres som en tagg:
    /// ELI + eId + ordrett sitat) og rollen <c>malvedtak</c> (vedtaket et endringsvedtak endrer, Johanns avgjørelse 2026-10-31 (2)).
    /// Bare tillegg: ingen eksisterende rad endres, og de gamle CHECK-ene er delmengder av de nye.</summary>
    public partial class FunnMaskinregelSitatOgMalvedtak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_koblinger_rolle",
                table: "funn_koblinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_koblinger_rolle_mal",
                table: "funn_koblinger");

            migrationBuilder.AddColumn<string>(
                name: "quote_exact",
                table: "funn_koblinger",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_koblinger_quote",
                table: "funn_koblinger",
                sql: "quote_exact IS NULL OR eid IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_koblinger_rolle",
                table: "funn_koblinger",
                sql: "rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse', 'berort_aktor', 'navneform', 'etterfolger', 'kilde', 'malvedtak')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_koblinger_rolle_mal",
                table: "funn_koblinger",
                sql: "(rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse') AND rettskilde_eli IS NOT NULL) OR (rolle = 'berort_aktor' AND virksomhet_id IS NOT NULL) OR (rolle = 'navneform' AND begrep_id IS NOT NULL) OR (rolle = 'etterfolger' AND strukturkant_id IS NOT NULL) OR (rolle = 'kilde' AND (rettskilde_eli IS NOT NULL OR ekstern_lenke IS NOT NULL OR kildefeil_id IS NOT NULL)) OR (rolle = 'malvedtak' AND rettskilde_eli IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_koblinger_quote",
                table: "funn_koblinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_koblinger_rolle",
                table: "funn_koblinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_koblinger_rolle_mal",
                table: "funn_koblinger");

            migrationBuilder.DropColumn(
                name: "quote_exact",
                table: "funn_koblinger");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_koblinger_rolle",
                table: "funn_koblinger",
                sql: "rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse', 'berort_aktor', 'navneform', 'etterfolger', 'kilde')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_koblinger_rolle_mal",
                table: "funn_koblinger",
                sql: "(rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse') AND rettskilde_eli IS NOT NULL) OR (rolle = 'berort_aktor' AND virksomhet_id IS NOT NULL) OR (rolle = 'navneform' AND begrep_id IS NOT NULL) OR (rolle = 'etterfolger' AND strukturkant_id IS NOT NULL) OR (rolle = 'kilde' AND (rettskilde_eli IS NOT NULL OR ekstern_lenke IS NOT NULL OR kildefeil_id IS NOT NULL))");
        }
    }
}
