using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilTjenesteBegrepKodeliste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kodelister",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kode = table.Column<string>(type: "text", nullable: false),
                    navn = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    juridisk_grunnlag_eid = table.Column<string>(type: "text", nullable: true),
                    ekstern_kilde_uri = table.Column<string>(type: "text", nullable: true),
                    ekstern_kilde_versjon = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "utkast"),
                    versjon = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    erstatter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gyldig_fra = table.Column<DateOnly>(type: "date", nullable: true),
                    gyldig_til = table.Column<DateOnly>(type: "date", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("kodelister_pkey", x => x.Id);
                    table.CheckConstraint("ck_kodelister_type", "type IN ('juridisk', 'teknisk', 'ekstern-referanse')");
                    table.ForeignKey(
                        name: "FK_kodelister_kodelister_erstatter_id",
                        column: x => x.erstatter_id,
                        principalTable: "kodelister",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_kodelister_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tjenester",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tittel = table.Column<string>(type: "text", nullable: false),
                    beskrivelse = table.Column<string>(type: "text", nullable: true),
                    kompetent_myndighet = table.Column<string>(type: "text", nullable: true),
                    output = table.Column<string>(type: "text", nullable: true),
                    tjenestetype = table.Column<string>(type: "text", nullable: true),
                    malgruppe = table.Column<string>(type: "text", nullable: true),
                    kanaler = table.Column<List<string>>(type: "text[]", nullable: false),
                    kostnad = table.Column<string>(type: "text", nullable: true),
                    behandlingstid = table.Column<string>(type: "text", nullable: true),
                    kontaktpunkt = table.Column<string>(type: "text", nullable: true),
                    konsekvens_ved_brudd = table.Column<string>(type: "text", nullable: true),
                    sprak = table.Column<List<string>>(type: "text[]", nullable: false),
                    hendelser = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    tjenesteavhengigheter = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "utkast"),
                    versjon = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    erstatter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gyldig_fra = table.Column<DateOnly>(type: "date", nullable: true),
                    gyldig_til = table.Column<DateOnly>(type: "date", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tjenester_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tjenester_tjenester_erstatter_id",
                        column: x => x.erstatter_id,
                        principalTable: "tjenester",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tjenester_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "begreper",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term = table.Column<string>(type: "text", nullable: false),
                    definisjon = table.Column<string>(type: "text", nullable: false),
                    lovreferanse_eid = table.Column<string>(type: "text", nullable: true),
                    gjelder_for = table.Column<List<string>>(type: "text[]", nullable: false),
                    kodeliste_referanse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    skos_url = table.Column<string>(type: "text", nullable: true),
                    begrepstype = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "utkast"),
                    versjon = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    erstatter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gyldig_fra = table.Column<DateOnly>(type: "date", nullable: true),
                    gyldig_til = table.Column<DateOnly>(type: "date", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("begreper_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_begreper_begreper_erstatter_id",
                        column: x => x.erstatter_id,
                        principalTable: "begreper",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_begreper_kodelister_kodeliste_referanse_id",
                        column: x => x.kodeliste_referanse_id,
                        principalTable: "kodelister",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_begreper_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kodeliste_koder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kodeliste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kode = table.Column<string>(type: "text", nullable: false),
                    term = table.Column<string>(type: "text", nullable: false),
                    definisjon = table.Column<string>(type: "text", nullable: true),
                    gyldig_fra = table.Column<DateOnly>(type: "date", nullable: true),
                    gyldig_til = table.Column<DateOnly>(type: "date", nullable: true),
                    erstattes_av_kode_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("kodeliste_koder_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kodeliste_koder_kodeliste_koder_erstattes_av_kode_id",
                        column: x => x.erstattes_av_kode_id,
                        principalTable: "kodeliste_koder",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_kodeliste_koder_kodelister_kodeliste_id",
                        column: x => x.kodeliste_id,
                        principalTable: "kodelister",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tjeneste_regelverksreferanser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tjeneste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_eid = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tjeneste_regelverksreferanser_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tjeneste_regelverksreferanser_rettskilder_til_rettskilde_id",
                        column: x => x.til_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tjeneste_regelverksreferanser_tjenester_tjeneste_id",
                        column: x => x.tjeneste_id,
                        principalTable: "tjenester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_begreper_erstatter_id",
                table: "begreper",
                column: "erstatter_id");

            migrationBuilder.CreateIndex(
                name: "IX_begreper_kodeliste_referanse_id",
                table: "begreper",
                column: "kodeliste_referanse_id");

            migrationBuilder.CreateIndex(
                name: "ix_begreper_virksomhet",
                table: "begreper",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "IX_kodeliste_koder_erstattes_av_kode_id",
                table: "kodeliste_koder",
                column: "erstattes_av_kode_id");

            migrationBuilder.CreateIndex(
                name: "ux_kodeliste_koder_kode",
                table: "kodeliste_koder",
                columns: new[] { "kodeliste_id", "kode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kodelister_erstatter_id",
                table: "kodelister",
                column: "erstatter_id");

            migrationBuilder.CreateIndex(
                name: "IX_kodelister_virksomhet_id",
                table: "kodelister",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ux_kodelister_kode",
                table: "kodelister",
                column: "kode",
                unique: true,
                filter: "entitetsstatus = 'gjeldende'");

            migrationBuilder.CreateIndex(
                name: "IX_tjeneste_regelverksreferanser_til_rettskilde_id",
                table: "tjeneste_regelverksreferanser",
                column: "til_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_tjeneste_regelverksreferanser",
                table: "tjeneste_regelverksreferanser",
                columns: new[] { "tjeneste_id", "til_rettskilde_id", "til_eid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tjenester_erstatter_id",
                table: "tjenester",
                column: "erstatter_id");

            migrationBuilder.CreateIndex(
                name: "ix_tjenester_virksomhet",
                table: "tjenester",
                column: "virksomhet_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "begreper");

            migrationBuilder.DropTable(
                name: "kodeliste_koder");

            migrationBuilder.DropTable(
                name: "tjeneste_regelverksreferanser");

            migrationBuilder.DropTable(
                name: "kodelister");

            migrationBuilder.DropTable(
                name: "tjenester");
        }
    }
}
