using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "proveniens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    entitet_type = table.Column<string>(type: "text", nullable: false),
                    entitet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endret_av = table.Column<string>(type: "text", nullable: false),
                    dato = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    handling = table.Column<string>(type: "text", nullable: false),
                    kilde_referanser = table.Column<string>(type: "jsonb", nullable: true),
                    ai_forslag_versjon = table.Column<string>(type: "text", nullable: true),
                    godkjent_av = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("proveniens_pkey", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rettskilder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctype = table.Column<string>(type: "text", nullable: false),
                    kildetype = table.Column<string>(type: "text", nullable: false),
                    importrolle = table.Column<string>(type: "text", nullable: false, defaultValue: "primaer"),
                    tittel = table.Column<string>(type: "text", nullable: false),
                    kortnavn = table.Column<string>(type: "text", nullable: true),
                    eli = table.Column<string>(type: "text", nullable: true),
                    akn_xml = table.Column<string>(type: "text", nullable: true),
                    ikrafttredelse = table.Column<DateOnly>(type: "date", nullable: true),
                    konsolidert_dato = table.Column<DateOnly>(type: "date", nullable: true),
                    utgiver = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("rettskilder_pkey", x => x.Id);
                    table.CheckConstraint("ck_rettskilder_akn_xml", "importrolle = 'referanse' OR akn_xml IS NOT NULL");
                    table.CheckConstraint("ck_rettskilder_importrolle", "importrolle IN ('primaer', 'referanse')");
                    table.ForeignKey(
                        name: "FK_rettskilder_rettskilder_erstatter_id",
                        column: x => x.erstatter_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "rettskilde_noder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eid = table.Column<string>(type: "text", nullable: false),
                    kildesystem = table.Column<string>(type: "text", nullable: false, defaultValue: "lovdata"),
                    kilde_id = table.Column<string>(type: "text", nullable: false),
                    offisiell_eli = table.Column<string>(type: "text", nullable: true),
                    parent_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    node_type = table.Column<string>(type: "text", nullable: false),
                    nummer = table.Column<string>(type: "text", nullable: true),
                    overskrift = table.Column<string>(type: "text", nullable: true),
                    tekst = table.Column<string>(type: "text", nullable: true),
                    tekst_hash = table.Column<string>(type: "text", nullable: true),
                    sorteringsrekkefolge = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rettskilde_noder_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rettskilde_noder_rettskilde_noder_parent_node_id",
                        column: x => x.parent_node_id,
                        principalTable: "rettskilde_noder",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_rettskilde_noder_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tekst_tagger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_eid = table.Column<string>(type: "text", nullable: false),
                    start_offset = table.Column<int>(type: "integer", nullable: false),
                    end_offset = table.Column<int>(type: "integer", nullable: false),
                    quote_prefix = table.Column<string>(type: "text", nullable: false),
                    quote_exact = table.Column<string>(type: "text", nullable: false),
                    quote_suffix = table.Column<string>(type: "text", nullable: false),
                    node_tekst_hash = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    ref_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tekst_tagger_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tekst_tagger_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rettskilde_referanser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    fra_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    til_eid = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rettskilde_referanser_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rettskilde_referanser_rettskilde_noder_fra_node_id",
                        column: x => x.fra_node_id,
                        principalTable: "rettskilde_noder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rettskilde_referanser_rettskilder_til_rettskilde_id",
                        column: x => x.til_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_proveniens_entitet",
                table: "proveniens",
                columns: new[] { "entitet_type", "entitet_id" });

            migrationBuilder.CreateIndex(
                name: "ix_rettskilde_noder_eid_hash",
                table: "rettskilde_noder",
                columns: new[] { "eid", "tekst_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_rettskilde_noder_parent",
                table: "rettskilde_noder",
                column: "parent_node_id");

            migrationBuilder.CreateIndex(
                name: "rettskilde_noder_rettskilde_id_eid_key",
                table: "rettskilde_noder",
                columns: new[] { "rettskilde_id", "eid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rettskilde_referanser_til_rettskilde_id",
                table: "rettskilde_referanser",
                column: "til_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "rettskilde_referanser_fra_node_id_til_rettskilde_id_til_ei_key",
                table: "rettskilde_referanser",
                columns: new[] { "fra_node_id", "til_rettskilde_id", "til_eid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rettskilder_erstatter_id",
                table: "rettskilder",
                column: "erstatter_id");

            migrationBuilder.CreateIndex(
                name: "ux_rettskilder_eli_gjeldende",
                table: "rettskilder",
                column: "eli",
                unique: true,
                filter: "entitetsstatus = 'gjeldende'");

            migrationBuilder.CreateIndex(
                name: "ix_tekst_tagger_node",
                table: "tekst_tagger",
                columns: new[] { "rettskilde_id", "node_eid" });

            migrationBuilder.CreateIndex(
                name: "tekst_tagger_unik_tagg",
                table: "tekst_tagger",
                columns: new[] { "rettskilde_id", "node_eid", "start_offset", "end_offset", "kind", "ref_id" },
                unique: true);

            // Uttrykksindeks (GIN + to_tsvector) — ikke uttrykkelig av EF Cores fluent API,
            // derfor rå SQL. §2 i teknisk design: fulltekstsøk på norsk.
            migrationBuilder.Sql(
                "CREATE INDEX ix_rettskilde_noder_tekst_fts ON rettskilde_noder USING gin(to_tsvector('norwegian', tekst));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_rettskilde_noder_tekst_fts;");

            migrationBuilder.DropTable(
                name: "proveniens");

            migrationBuilder.DropTable(
                name: "rettskilde_referanser");

            migrationBuilder.DropTable(
                name: "tekst_tagger");

            migrationBuilder.DropTable(
                name: "rettskilde_noder");

            migrationBuilder.DropTable(
                name: "rettskilder");
        }
    }
}
