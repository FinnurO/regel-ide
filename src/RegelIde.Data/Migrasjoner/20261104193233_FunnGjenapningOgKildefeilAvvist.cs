using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class FunnGjenapningOgKildefeilAvvist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_kildefeil_status",
                table: "kildefeil");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_undertype",
                table: "funn");

            migrationBuilder.AddColumn<string>(
                name: "status_endret_av",
                table: "kildefeil",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "status_endret_tidspunkt",
                table: "kildefeil",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "statusbegrunnelse",
                table: "kildefeil",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gjenapnet_av",
                table: "funn",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gjenapnet_begrunnelse",
                table: "funn",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "gjenapnet_tidspunkt",
                table: "funn",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_kildefeil_avvist",
                table: "kildefeil",
                sql: "status <> 'Avvist' OR (status_endret_av IS NOT NULL AND status_endret_tidspunkt IS NOT NULL AND statusbegrunnelse IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_kildefeil_status",
                table: "kildefeil",
                sql: "status IN ('Ny', 'Kjent', 'Rettet-hos-oss', 'Venter-på-Lovdata', 'Avvist')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_gjenapnet",
                table: "funn",
                sql: "(gjenapnet_tidspunkt IS NULL AND gjenapnet_av IS NULL AND gjenapnet_begrunnelse IS NULL) OR (gjenapnet_tidspunkt IS NOT NULL AND gjenapnet_av IS NOT NULL AND gjenapnet_begrunnelse IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_undertype",
                table: "funn",
                sql: "undertype IS NULL OR (kategori = 'utgatt_aktor' AND undertype IN ('nedlagt_aktor', 'utgatt_navn', 'avgiverformel')) OR (kategori = 'uklarhet_hull' AND undertype IN ('hull', 'upresis_henvisning', 'publiseringsfeil_kilde'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_kildefeil_avvist",
                table: "kildefeil");

            migrationBuilder.DropCheckConstraint(
                name: "ck_kildefeil_status",
                table: "kildefeil");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_gjenapnet",
                table: "funn");

            migrationBuilder.DropCheckConstraint(
                name: "ck_funn_undertype",
                table: "funn");

            migrationBuilder.DropColumn(
                name: "status_endret_av",
                table: "kildefeil");

            migrationBuilder.DropColumn(
                name: "status_endret_tidspunkt",
                table: "kildefeil");

            migrationBuilder.DropColumn(
                name: "statusbegrunnelse",
                table: "kildefeil");

            migrationBuilder.DropColumn(
                name: "gjenapnet_av",
                table: "funn");

            migrationBuilder.DropColumn(
                name: "gjenapnet_begrunnelse",
                table: "funn");

            migrationBuilder.DropColumn(
                name: "gjenapnet_tidspunkt",
                table: "funn");

            migrationBuilder.AddCheckConstraint(
                name: "ck_kildefeil_status",
                table: "kildefeil",
                sql: "status IN ('Ny', 'Kjent', 'Rettet-hos-oss', 'Venter-på-Lovdata')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_funn_undertype",
                table: "funn",
                sql: "undertype IS NULL OR (kategori = 'utgatt_aktor' AND undertype IN ('nedlagt_aktor', 'utgatt_navn')) OR (kategori = 'uklarhet_hull' AND undertype IN ('hull', 'upresis_henvisning', 'publiseringsfeil_kilde'))");
        }
    }
}
