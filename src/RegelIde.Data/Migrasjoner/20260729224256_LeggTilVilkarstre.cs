using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilVilkarstre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "rotnode_id",
                table: "tjenester",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "datasett",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    felt = table.Column<string>(type: "text", nullable: false),
                    prop = table.Column<string>(type: "text", nullable: false),
                    dtype = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    kilde = table.Column<string>(type: "text", nullable: true),
                    kodeliste_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grunnlag = table.Column<string>(type: "text", nullable: true),
                    lagring = table.Column<string>(type: "text", nullable: true),
                    mottakere = table.Column<List<string>>(type: "text[]", nullable: false),
                    bruk = table.Column<string>(type: "text", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("datasett_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_datasett_kodelister_kodeliste_id",
                        column: x => x.kodeliste_id,
                        principalTable: "kodelister",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_datasett_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regelnoder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tittel = table.Column<string>(type: "text", nullable: false),
                    beskrivelse = table.Column<string>(type: "text", nullable: true),
                    generisk_mal = table.Column<string>(type: "text", nullable: true),
                    barn_operator = table.Column<string>(type: "text", nullable: false),
                    utdata_navn = table.Column<string>(type: "text", nullable: false),
                    utdata_type = table.Column<string>(type: "text", nullable: false),
                    er_rotnode = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    juridisk_grunnlag = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    innvilgelse_tekst = table.Column<string>(type: "text", nullable: true),
                    avslag_tekst = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("regelnoder_pkey", x => x.Id);
                    table.CheckConstraint("ck_regelnoder_barn_operator", "barn_operator IN ('OG', 'ELLER', 'IKKE')");
                    table.ForeignKey(
                        name: "FK_regelnoder_regelnoder_erstatter_id",
                        column: x => x.erstatter_id,
                        principalTable: "regelnoder",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_regelnoder_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vilkar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tittel = table.Column<string>(type: "text", nullable: false),
                    beskrivelse = table.Column<string>(type: "text", nullable: true),
                    generisk_mal = table.Column<string>(type: "text", nullable: true),
                    vilkarstype = table.Column<string>(type: "text", nullable: false),
                    gjelder_rolle = table.Column<string>(type: "text", nullable: true),
                    juridisk_grunnlag = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    begrep_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vurderingstype = table.Column<string>(type: "text", nullable: false),
                    parametre = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    skjonnsgrunnlag_begrep_id = table.Column<Guid>(type: "uuid", nullable: true),
                    skjonnsmomenter = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    krever_dokumentasjon = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    eskaleringsrolle = table.Column<string>(type: "text", nullable: true),
                    veiledning_til_bruker = table.Column<string>(type: "text", nullable: true),
                    veiledning_til_saksbehandler = table.Column<string>(type: "text", nullable: true),
                    er_formel = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    formel_beskrivelse = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("vilkar_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vilkar_begreper_begrep_id",
                        column: x => x.begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_vilkar_begreper_skjonnsgrunnlag_begrep_id",
                        column: x => x.skjonnsgrunnlag_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_vilkar_vilkar_erstatter_id",
                        column: x => x.erstatter_id,
                        principalTable: "vilkar",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_vilkar_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regelnode_barn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    regelnode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    barn_type = table.Column<string>(type: "text", nullable: false),
                    barn_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("regelnode_barn_pkey", x => x.Id);
                    table.CheckConstraint("ck_regelnode_barn_type", "barn_type IN ('vilkar', 'regelnode')");
                    table.ForeignKey(
                        name: "FK_regelnode_barn_regelnoder_regelnode_id",
                        column: x => x.regelnode_id,
                        principalTable: "regelnoder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unntak",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tittel = table.Column<string>(type: "text", nullable: false),
                    beskrivelse = table.Column<string>(type: "text", nullable: true),
                    gjelder_regel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    betingelse_type = table.Column<string>(type: "text", nullable: false),
                    betingelse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    juridisk_grunnlag = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
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
                    table.PrimaryKey("unntak_pkey", x => x.Id);
                    table.CheckConstraint("ck_unntak_betingelse_type", "betingelse_type IN ('vilkar', 'regelnode')");
                    table.ForeignKey(
                        name: "FK_unntak_regelnoder_gjelder_regel_id",
                        column: x => x.gjelder_regel_id,
                        principalTable: "regelnoder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_unntak_unntak_erstatter_id",
                        column: x => x.erstatter_id,
                        principalTable: "unntak",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_unntak_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vilkar_input_datasett",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    vilkar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    datasett_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("vilkar_input_datasett_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vilkar_input_datasett_datasett_datasett_id",
                        column: x => x.datasett_id,
                        principalTable: "datasett",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vilkar_input_datasett_vilkar_vilkar_id",
                        column: x => x.vilkar_id,
                        principalTable: "vilkar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tjenester_rotnode_id",
                table: "tjenester",
                column: "rotnode_id");

            migrationBuilder.CreateIndex(
                name: "IX_datasett_kodeliste_id",
                table: "datasett",
                column: "kodeliste_id");

            migrationBuilder.CreateIndex(
                name: "ix_datasett_prop",
                table: "datasett",
                column: "prop");

            migrationBuilder.CreateIndex(
                name: "IX_datasett_virksomhet_id",
                table: "datasett",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ux_regelnode_barn",
                table: "regelnode_barn",
                columns: new[] { "regelnode_id", "barn_type", "barn_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regelnoder_erstatter_id",
                table: "regelnoder",
                column: "erstatter_id");

            migrationBuilder.CreateIndex(
                name: "ix_regelnoder_virksomhet",
                table: "regelnoder",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "IX_unntak_erstatter_id",
                table: "unntak",
                column: "erstatter_id");

            migrationBuilder.CreateIndex(
                name: "ix_unntak_gjelder_regel",
                table: "unntak",
                column: "gjelder_regel_id");

            migrationBuilder.CreateIndex(
                name: "ix_unntak_virksomhet",
                table: "unntak",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "IX_vilkar_begrep_id",
                table: "vilkar",
                column: "begrep_id");

            migrationBuilder.CreateIndex(
                name: "IX_vilkar_erstatter_id",
                table: "vilkar",
                column: "erstatter_id");

            migrationBuilder.CreateIndex(
                name: "IX_vilkar_skjonnsgrunnlag_begrep_id",
                table: "vilkar",
                column: "skjonnsgrunnlag_begrep_id");

            migrationBuilder.CreateIndex(
                name: "ix_vilkar_virksomhet",
                table: "vilkar",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "IX_vilkar_input_datasett_datasett_id",
                table: "vilkar_input_datasett",
                column: "datasett_id");

            migrationBuilder.CreateIndex(
                name: "ux_vilkar_input_datasett",
                table: "vilkar_input_datasett",
                columns: new[] { "vilkar_id", "datasett_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tjenester_regelnoder_rotnode_id",
                table: "tjenester",
                column: "rotnode_id",
                principalTable: "regelnoder",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tjenester_regelnoder_rotnode_id",
                table: "tjenester");

            migrationBuilder.DropTable(
                name: "regelnode_barn");

            migrationBuilder.DropTable(
                name: "unntak");

            migrationBuilder.DropTable(
                name: "vilkar_input_datasett");

            migrationBuilder.DropTable(
                name: "regelnoder");

            migrationBuilder.DropTable(
                name: "datasett");

            migrationBuilder.DropTable(
                name: "vilkar");

            migrationBuilder.DropIndex(
                name: "IX_tjenester_rotnode_id",
                table: "tjenester");

            migrationBuilder.DropColumn(
                name: "rotnode_id",
                table: "tjenester");
        }
    }
}
