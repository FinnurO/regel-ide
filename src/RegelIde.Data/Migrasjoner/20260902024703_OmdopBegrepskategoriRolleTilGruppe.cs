using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class OmdopBegrepskategoriRolleTilGruppe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_myndighetstildelinger_begreper_rolle_begrep_id",
                table: "myndighetstildelinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater");

            migrationBuilder.DropIndex(
                name: "ux_begreper_rollebegrep_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper");

            // Data-del (docs/29 §A.2 punkt 2) — konverter eksisterende rader FØR CHECK-constraint-ene
            // legges til igjen under (den nye 'gruppe'-varianten ville ellers avvist de fortsatt
            // 'rolle'-merkede radene). Verifisert lave radantall 2026-09-02: 1 BegrepEntitet-rad,
            // 2486 NavnekandidatEntitet-rader — ingen ytelsesbekymring.
            migrationBuilder.Sql("UPDATE begreper SET begrepskategori = 'gruppe' WHERE begrepskategori = 'rolle';");
            migrationBuilder.Sql("UPDATE navnekandidater SET kategori = 'gruppe' WHERE kategori = 'rolle';");

            migrationBuilder.RenameColumn(
                name: "rolle_begrep_id",
                table: "myndighetstildelinger",
                newName: "gruppe_begrep_id");

            migrationBuilder.RenameIndex(
                name: "ix_myndighetstildelinger_rolle_begrep",
                table: "myndighetstildelinger",
                newName: "ix_myndighetstildelinger_gruppe_begrep");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater",
                sql: "kategori IN ('virksomhet', 'gruppe')");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_gruppebegrep_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori = 'gruppe' AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper",
                sql: "begrepskategori IS NULL OR begrepskategori IN ('virksomhet', 'gruppe')");

            migrationBuilder.AddForeignKey(
                name: "FK_myndighetstildelinger_begreper_gruppe_begrep_id",
                table: "myndighetstildelinger",
                column: "gruppe_begrep_id",
                principalTable: "begreper",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_myndighetstildelinger_begreper_gruppe_begrep_id",
                table: "myndighetstildelinger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater");

            migrationBuilder.DropIndex(
                name: "ux_begreper_gruppebegrep_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper");

            // Reverser data-delen (motstykke til Up() — se kommentaren der) FØR CHECK-constraint-ene
            // for 'rolle' legges til igjen under.
            migrationBuilder.Sql("UPDATE begreper SET begrepskategori = 'rolle' WHERE begrepskategori = 'gruppe';");
            migrationBuilder.Sql("UPDATE navnekandidater SET kategori = 'rolle' WHERE kategori = 'gruppe';");

            migrationBuilder.RenameColumn(
                name: "gruppe_begrep_id",
                table: "myndighetstildelinger",
                newName: "rolle_begrep_id");

            migrationBuilder.RenameIndex(
                name: "ix_myndighetstildelinger_gruppe_begrep",
                table: "myndighetstildelinger",
                newName: "ix_myndighetstildelinger_rolle_begrep");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navnekandidater_kategori",
                table: "navnekandidater",
                sql: "kategori IN ('virksomhet', 'rolle')");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_rollebegrep_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori = 'rolle' AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper",
                sql: "begrepskategori IS NULL OR begrepskategori IN ('virksomhet', 'rolle')");

            migrationBuilder.AddForeignKey(
                name: "FK_myndighetstildelinger_begreper_rolle_begrep_id",
                table: "myndighetstildelinger",
                column: "rolle_begrep_id",
                principalTable: "begreper",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
