using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class FunnVurdertAvMaskinregel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_vurdert_av_type",
                table: "funn");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_vurdert_av_type",
                table: "funn",
                sql: "vurdert_av_type IS NULL OR vurdert_av_type IN ('menneske', 'ki_agent', 'maskinregel')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_vurdert_av_type",
                table: "funn");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_vurdert_av_type",
                table: "funn",
                sql: "vurdert_av_type IS NULL OR vurdert_av_type IN ('menneske', 'ki_agent')");
        }
    }
}
