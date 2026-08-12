using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilHandbokFeltPaRettskildeOgVirksomhet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "forvaltningsniva",
                table: "virksomheter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kommunenummer",
                table: "virksomheter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "funksjonell_rolle",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hentet",
                table: "rettskilder",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hjemmel_eid",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "http_etag",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "http_last_modified",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "innhold",
                table: "rettskilder",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "innholds_hash",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "internt_dok_nr",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normativ_virkning",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revisjonsnr",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "saksnummer",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "url",
                table: "rettskilder",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "vedtaksdato",
                table: "rettskilder",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vedtatt_av",
                table: "rettskilder",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "forvaltningsniva",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "kommunenummer",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "funksjonell_rolle",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "hentet",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "hjemmel_eid",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "http_etag",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "http_last_modified",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "innhold",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "innholds_hash",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "internt_dok_nr",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "normativ_virkning",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "revisjonsnr",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "saksnummer",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "url",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "vedtaksdato",
                table: "rettskilder");

            migrationBuilder.DropColumn(
                name: "vedtatt_av",
                table: "rettskilder");
        }
    }
}
