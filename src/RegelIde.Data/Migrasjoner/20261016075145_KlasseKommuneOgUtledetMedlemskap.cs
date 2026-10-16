using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #384, Johanns beslutning 2026-10-16] Den felles klassen «kommune» med utledet medlemskap fra områderegisteret.
    /// <list type="bullet">
    /// <item><c>begreper.medlemskapsregel</c> (CHECK <c>ck_begreper_medlemskapsregel</c>, unik blant gjeldende:
    /// <c>ux_begreper_medlemskapsregel</c>) — regelen medlemskapet utledes etter (<see cref="Klasseregler"/>).</item>
    /// <item><c>begreper.kunnskapsstatus</c> (CHECK <c>ck_begreper_kunnskapsstatus</c>) — kunnskapsstatus for begrepet selv. NULL for alle
    /// eksisterende rader (ingen gjettet verdi).</item>
    /// <item>Datadelen er <see cref="KlasseKommuneMigrering"/>: én ny klasse og én proveniensrad, eller ingenting ved navnekonflikt
    /// (WARNING). Antallene skrives som NOTICE.</item>
    /// </list>
    /// <c>Down</c> nekter når noe er registrert mot klassen etter migrasjonen; ellers fjerner den raden, proveniensraden, indeksen, CHECK-ene
    /// og kolonnene. Bindingene trengte ingen migrasjon: verket er (virksomhet_id, eli) på rettskilden bindingen ble registrert mot
    /// (<see cref="GeneriskNavneformTjeneste"/>). Bygget etter #378 (ikke stablet).
    /// </summary>
    public partial class KlasseKommuneOgUtledetMedlemskap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kunnskapsstatus",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "medlemskapsregel",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_begreper_medlemskapsregel",
                table: "begreper",
                column: "medlemskapsregel",
                unique: true,
                filter: "medlemskapsregel IS NOT NULL AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_kunnskapsstatus",
                table: "begreper",
                sql: "kunnskapsstatus IS NULL OR (kunnskapsstatus IN ('tekstfunn', 'normalisering') AND lovreferanse_eid IS NOT NULL) OR kunnskapsstatus IN ('uavklart', 'omstridt')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_medlemskapsregel",
                table: "begreper",
                sql: "medlemskapsregel IS NULL OR (begrepskategori = 'klasse' AND medlemskapsregel IN ('omraderegister_kommune'))");

            migrationBuilder.Sql(KlasseKommuneMigrering.UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Datadelen FØR kolonnene fjernes: Down finner klassen på id-en og nekter hvis noe peker på den.
            migrationBuilder.Sql(KlasseKommuneMigrering.DownSql);

            migrationBuilder.DropIndex(
                name: "ux_begreper_medlemskapsregel",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_kunnskapsstatus",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_medlemskapsregel",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "kunnskapsstatus",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "medlemskapsregel",
                table: "begreper");
        }
    }
}
