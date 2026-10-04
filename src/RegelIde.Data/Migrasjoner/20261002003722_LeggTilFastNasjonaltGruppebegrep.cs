using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilFastNasjonaltGruppebegrep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_begreper_gruppebegrep_fast_term",
                table: "begreper",
                column: "term",
                unique: true,
                filter: "begrepskategori = 'gruppe' AND entitetsstatus = 'gjeldende' AND lovkilde_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_begreper_gruppebegrep_fast_term",
                table: "begreper");
        }
    }
}
