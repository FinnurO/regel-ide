using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, Refs #486 PR 2, prosjekteierens avgjørelse 2026-11-25] To nye lesninger for en forekomstbinding (tagg per forekomst av «statsråd»,
    /// docs/33 §5.19.3): <c>kongen_i_statsrad</c> (beslutningsformen, referent Kongen i statsråd) og <c>statsradet</c> (organet, referent
    /// statsrådet). Begge har referent, så <c>ck_navneform_bindinger_referent_en</c> er uendret. Bare CHECK; ingen data flyttes.
    /// <c>Down</c> nekter når en binding har en av de nye lesningene (de kan ikke uttrykkes uten dem).
    /// </summary>
    public partial class StatsradLesninger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_navneform_bindinger_lesning",
                table: "navneform_bindinger");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navneform_bindinger_lesning",
                table: "navneform_bindinger",
                sql: "lesning IS NULL OR (tekst_tagg_id IS NOT NULL AND lesning IN ('fremmed_stat', 'generisk_konvensjonsstat', 'ikke_aktor', 'rolle_statsrad', 'funksjon', 'regjeringens_resolusjon', 'slutning', 'kongen_i_statsrad', 'statsradet'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE v_antall int;
                BEGIN
                  SELECT count(*) INTO v_antall FROM navneform_bindinger WHERE lesning IN ('kongen_i_statsrad', 'statsradet');
                  IF v_antall > 0 THEN
                    RAISE EXCEPTION 'Refs #486 Down: % forekomstbinding(er) har lesningen kongen_i_statsrad eller statsradet. Avvis eller slett dem først.', v_antall;
                  END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_navneform_bindinger_lesning",
                table: "navneform_bindinger");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navneform_bindinger_lesning",
                table: "navneform_bindinger",
                sql: "lesning IS NULL OR (tekst_tagg_id IS NOT NULL AND lesning IN ('fremmed_stat', 'generisk_konvensjonsstat', 'ikke_aktor', 'rolle_statsrad', 'funksjon', 'regjeringens_resolusjon', 'slutning'))");
        }
    }
}
