using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilKildefeil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kildefeil",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_eid = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    beskrivelse = table.Column<string>(type: "text", nullable: false),
                    funnet_av_mekanisme = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Ny"),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("kildefeil_pkey", x => x.Id);
                    table.CheckConstraint("ck_kildefeil_status", "status IN ('Ny', 'Kjent', 'Rettet-hos-oss', 'Venter-på-Lovdata')");
                    table.ForeignKey(
                        name: "FK_kildefeil_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kildefeil_rettskilde",
                table: "kildefeil",
                column: "rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_kildefeil_status",
                table: "kildefeil",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_kildefeil_rettskilde_eid_type_mekanisme",
                table: "kildefeil",
                columns: new[] { "rettskilde_id", "rettskilde_eid", "type", "funnet_av_mekanisme" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kildefeil");
        }
    }
}
