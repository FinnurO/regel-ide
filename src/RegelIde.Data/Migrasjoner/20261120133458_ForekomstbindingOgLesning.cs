using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #486, juristens vilkår 1 fra runde 2 om «regjeringen», Johanns avgjørelse 2026-11-20] <b>Tagg per forekomst med samme status
    /// som en binding.</b> <c>navneform_bindinger</c> får <c>tekst_tagg_id</c> (taggen er spennet; CASCADE fra taggen) og <c>lesning</c> (lukket
    /// liste, <see cref="Forekomstlesninger"/>). En forekomstbinding med lesning uten referent (fremmed stat, generisk konvensjonsstat, ikke aktør,
    /// funksjon, slutning) har ingen referent; alle andre bindinger har nøyaktig én som før. En slutning er alltid et forslag. Migrasjonen skriver
    /// ingen data: statsrådet, «regjeringen» og forekomstbindingene er forslag og skrives av <see cref="RegjeringenTjeneste"/>
    /// (<c>POST /api/regjeringen?skriv=true</c>), med tørrkjøring. <c>Down</c> nekter når forekomstbindinger finnes, i stedet for å slette dem
    /// stille (samme mønster som #442/#463).
    /// </summary>
    public partial class ForekomstbindingOgLesning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_navneform_bindinger_referent_en",
                table: "navneform_bindinger");

            migrationBuilder.AddColumn<string>(
                name: "lesning",
                table: "navneform_bindinger",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tekst_tagg_id",
                table: "navneform_bindinger",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_navneform_bindinger_tekst_tagg",
                table: "navneform_bindinger",
                column: "tekst_tagg_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navneform_bindinger_forekomst_spenn",
                table: "navneform_bindinger",
                sql: "tekst_tagg_id IS NULL OR paragrafspenn_json = '[]'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navneform_bindinger_lesning",
                table: "navneform_bindinger",
                sql: "lesning IS NULL OR (tekst_tagg_id IS NOT NULL AND lesning IN ('fremmed_stat', 'generisk_konvensjonsstat', 'ikke_aktor', 'rolle_statsrad', 'funksjon', 'regjeringens_resolusjon', 'slutning'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navneform_bindinger_referent_en",
                table: "navneform_bindinger",
                sql: "((til_virksomhet_id IS NULL) <> (til_begrep_id IS NULL)) OR (til_virksomhet_id IS NULL AND til_begrep_id IS NULL AND tekst_tagg_id IS NOT NULL AND lesning IN ('fremmed_stat', 'generisk_konvensjonsstat', 'ikke_aktor', 'funksjon', 'slutning'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navneform_bindinger_slutning_forslag",
                table: "navneform_bindinger",
                sql: "lesning IS NULL OR lesning <> 'slutning' OR status IN ('maskinelt_foreslatt', 'manuelt_foreslatt', 'avvist')");

            migrationBuilder.AddForeignKey(
                name: "FK_navneform_bindinger_tekst_tagger_tekst_tagg_id",
                table: "navneform_bindinger",
                column: "tekst_tagg_id",
                principalTable: "tekst_tagger",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE v_antall int;
                BEGIN
                  SELECT count(*) INTO v_antall FROM navneform_bindinger WHERE tekst_tagg_id IS NOT NULL OR lesning IS NOT NULL;
                  IF v_antall > 0 THEN
                    RAISE EXCEPTION 'Issue #486 Down: % forekomstbinding(er) finnes. Slett eller avvis dem først; de kan ikke uttrykkes uten kolonnene.', v_antall;
                  END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_navneform_bindinger_tekst_tagger_tekst_tagg_id",
                table: "navneform_bindinger");

            migrationBuilder.DropIndex(
                name: "ix_navneform_bindinger_tekst_tagg",
                table: "navneform_bindinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navneform_bindinger_forekomst_spenn",
                table: "navneform_bindinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navneform_bindinger_lesning",
                table: "navneform_bindinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navneform_bindinger_referent_en",
                table: "navneform_bindinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navneform_bindinger_slutning_forslag",
                table: "navneform_bindinger");

            migrationBuilder.DropColumn(
                name: "lesning",
                table: "navneform_bindinger");

            migrationBuilder.DropColumn(
                name: "tekst_tagg_id",
                table: "navneform_bindinger");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navneform_bindinger_referent_en",
                table: "navneform_bindinger",
                sql: "(til_virksomhet_id IS NULL) <> (til_begrep_id IS NULL)");
        }
    }
}
