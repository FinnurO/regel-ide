using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class HjemmelOpphevetFunn : Migration
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

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_kategori",
                table: "funn");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_undertype",
                table: "funn");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_koblinger_rolle",
                table: "funn_koblinger",
                sql: "rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse', 'berort_aktor', 'navneform', 'etterfolger', 'kilde', 'malvedtak', 'opphevende_bestemmelse', 'videreforingsbestemmelse')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_koblinger_rolle_mal",
                table: "funn_koblinger",
                sql: "(rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse', 'opphevende_bestemmelse', 'videreforingsbestemmelse') AND rettskilde_eli IS NOT NULL) OR (rolle = 'berort_aktor' AND virksomhet_id IS NOT NULL) OR (rolle = 'navneform' AND begrep_id IS NOT NULL) OR (rolle = 'etterfolger' AND strukturkant_id IS NOT NULL) OR (rolle = 'kilde' AND (rettskilde_eli IS NOT NULL OR ekstern_lenke IS NOT NULL OR kildefeil_id IS NOT NULL)) OR (rolle = 'malvedtak' AND rettskilde_eli IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_kategori",
                table: "funn",
                sql: "kategori IN ('utgatt_aktor', 'mulig_motstrid', 'uklarhet_hull', 'hjemmel_opphevet')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_undertype",
                table: "funn",
                sql: "undertype IS NULL OR (kategori = 'utgatt_aktor' AND undertype IN ('nedlagt_aktor', 'utgatt_navn', 'avgiverformel')) OR (kategori = 'uklarhet_hull' AND undertype IN ('hull', 'upresis_henvisning', 'publiseringsfeil_kilde')) OR (kategori = 'hjemmel_opphevet' AND undertype IN ('hjemmel_opphevet', 'blandede_hjemler', 'uavklart', 'videreforing_utlopt', 'gjenstandslos'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_koblinger_rolle",
                table: "funn_koblinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_koblinger_rolle_mal",
                table: "funn_koblinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_kategori",
                table: "funn");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_undertype",
                table: "funn");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_koblinger_rolle",
                table: "funn_koblinger",
                sql: "rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse', 'berort_aktor', 'navneform', 'etterfolger', 'kilde', 'malvedtak')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_koblinger_rolle_mal",
                table: "funn_koblinger",
                sql: "(rolle IN ('gjelder_bestemmelse', 'motstridende_bestemmelse') AND rettskilde_eli IS NOT NULL) OR (rolle = 'berort_aktor' AND virksomhet_id IS NOT NULL) OR (rolle = 'navneform' AND begrep_id IS NOT NULL) OR (rolle = 'etterfolger' AND strukturkant_id IS NOT NULL) OR (rolle = 'kilde' AND (rettskilde_eli IS NOT NULL OR ekstern_lenke IS NOT NULL OR kildefeil_id IS NOT NULL)) OR (rolle = 'malvedtak' AND rettskilde_eli IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_kategori",
                table: "funn",
                sql: "kategori IN ('utgatt_aktor', 'mulig_motstrid', 'uklarhet_hull')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_undertype",
                table: "funn",
                sql: "undertype IS NULL OR (kategori = 'utgatt_aktor' AND undertype IN ('nedlagt_aktor', 'utgatt_navn', 'avgiverformel')) OR (kategori = 'uklarhet_hull' AND undertype IN ('hull', 'upresis_henvisning', 'publiseringsfeil_kilde'))");
        }
    }
}
