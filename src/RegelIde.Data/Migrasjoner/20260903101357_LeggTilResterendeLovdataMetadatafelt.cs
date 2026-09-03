using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilResterendeLovdataMetadatafelt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "annet_om_dokumentet",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dokument_id",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "etat",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eu_eos_henvisning",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gjelder_for",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kunngjort",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "publisert_i",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ref_id",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rettsomrade",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "siste_rettelse",
                table: "rettskilder",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "annet_om_dokumentet",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "dokument_id",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "etat",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "eu_eos_henvisning",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "gjelder_for",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "kunngjort",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "publisert_i",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "ref_id",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "rettsomrade",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "siste_rettelse",
                table: "rettskilder");
        }
    }
}
