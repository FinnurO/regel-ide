using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class Hjemmelsstatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hjemmelsstatus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_hjemmel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hjemmel_eid = table.Column<string>(type: "text", nullable: false),
                    hjemmel_eli = table.Column<string>(type: "text", nullable: true),
                    mal_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rolle = table.Column<string>(type: "text", nullable: true),
                    rolle_regel = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    bevistrinn = table.Column<string>(type: "text", nullable: true),
                    ukjent_grunn = table.Column<string>(type: "text", nullable: true),
                    fulgt_eli = table.Column<string>(type: "text", nullable: true),
                    opphevet_av_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opphevet_av_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opphevet_av_eid = table.Column<string>(type: "text", nullable: true),
                    opphevet_av_sitat = table.Column<string>(type: "text", nullable: true),
                    opphevet_dato = table.Column<DateOnly>(type: "date", nullable: true),
                    opphevet_fra_kunngjort = table.Column<bool>(type: "boolean", nullable: false),
                    videreforing_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    videreforing_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    videreforing_eid = table.Column<string>(type: "text", nullable: true),
                    videreforing_sitat = table.Column<string>(type: "text", nullable: true),
                    videreforing_form = table.Column<string>(type: "text", nullable: true),
                    videreforing_forbehold = table.Column<string>(type: "text", nullable: true),
                    videreforing_tidsfrist = table.Column<DateOnly>(type: "date", nullable: true),
                    videreforing_dekning = table.Column<string>(type: "text", nullable: true),
                    videreforing_fra_kunngjort = table.Column<bool>(type: "boolean", nullable: false),
                    erstattet_av_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    erstattet_av_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    erstattet_av_eid = table.Column<string>(type: "text", nullable: true),
                    premissmangel = table.Column<string>(type: "text", nullable: true),
                    kunnskapsstatus = table.Column<string>(type: "text", nullable: false),
                    regel = table.Column<string>(type: "text", nullable: false),
                    regelversjon = table.Column<string>(type: "text", nullable: false),
                    beregnet_av = table.Column<string>(type: "text", nullable: false),
                    beregnet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hjemmelsstatus_pkey", x => x.Id);
                    table.CheckConstraint("ck_hjemmelsstatus_bevis_sitat", "bevistrinn IS NULL OR opphevet_av_sitat IS NOT NULL");
                    table.CheckConstraint("ck_hjemmelsstatus_bevistrinn", "bevistrinn IS NULL OR bevistrinn IN ('E1', 'E1u', 'E2', 'EN_LTI')");
                    table.CheckConstraint("ck_hjemmelsstatus_dekning", "videreforing_dekning IS NULL OR videreforing_dekning IN ('dekket', 'dokumenttype_ikke_nevnt', 'avgrensning_ikke_kontrollert')");
                    table.CheckConstraint("ck_hjemmelsstatus_erstattet", "status <> 'erstattet' OR erstattet_av_rettskilde_id IS NOT NULL");
                    table.CheckConstraint("ck_hjemmelsstatus_form", "videreforing_form IS NULL OR videreforing_form IN ('V1', 'V2', 'V2a', 'V3', 'V4')");
                    table.CheckConstraint("ck_hjemmelsstatus_kunngjort", "NOT opphevet_fra_kunngjort OR opphevet_av_node_id IS NULL");
                    table.CheckConstraint("ck_hjemmelsstatus_kunnskapsstatus", "kunnskapsstatus IN ('tekstfunn', 'normalisering', 'fortolkning', 'avledet', 'eksternt_dokumentert', 'uavklart', 'omstridt')");
                    table.CheckConstraint("ck_hjemmelsstatus_oppheving", "status NOT IN ('opphevet', 'viderefort', 'videreforing_utlopt', 'erstattet', 'gjenstandslos') OR (bevistrinn IS NOT NULL AND opphevet_av_sitat IS NOT NULL AND opphevet_av_eid IS NOT NULL)");
                    table.CheckConstraint("ck_hjemmelsstatus_rolle", "rolle IS NULL OR rolle IN ('materiell', 'kompetanseledd', 'overgangsgrunnlag', 'gjenstand')");
                    table.CheckConstraint("ck_hjemmelsstatus_status", "status IN ('aktiv', 'opphevet', 'opphevet_bestemmelse', 'viderefort', 'videreforing_utlopt', 'erstattet', 'gjenstandslos', 'ukjent')");
                    table.CheckConstraint("ck_hjemmelsstatus_utlopt", "status <> 'videreforing_utlopt' OR videreforing_tidsfrist IS NOT NULL");
                    table.CheckConstraint("ck_hjemmelsstatus_videreforing", "status NOT IN ('viderefort', 'videreforing_utlopt') OR (videreforing_sitat IS NOT NULL AND videreforing_form IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_hjemmelsstatus_rettskilde_hjemler_rettskilde_hjemmel_id",
                        column: x => x.rettskilde_hjemmel_id,
                        principalTable: "rettskilde_hjemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hjemmelsstatus_rettskilde_noder_erstattet_av_node_id",
                        column: x => x.erstattet_av_node_id,
                        principalTable: "rettskilde_noder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hjemmelsstatus_rettskilde_noder_opphevet_av_node_id",
                        column: x => x.opphevet_av_node_id,
                        principalTable: "rettskilde_noder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hjemmelsstatus_rettskilde_noder_videreforing_node_id",
                        column: x => x.videreforing_node_id,
                        principalTable: "rettskilde_noder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hjemmelsstatus_rettskilder_erstattet_av_rettskilde_id",
                        column: x => x.erstattet_av_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hjemmelsstatus_rettskilder_mal_rettskilde_id",
                        column: x => x.mal_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hjemmelsstatus_rettskilder_opphevet_av_rettskilde_id",
                        column: x => x.opphevet_av_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hjemmelsstatus_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hjemmelsstatus_rettskilder_videreforing_rettskilde_id",
                        column: x => x.videreforing_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hjemmelsstatus_erstattet_av_node_id",
                table: "hjemmelsstatus",
                column: "erstattet_av_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_hjemmelsstatus_erstattet_av_rettskilde_id",
                table: "hjemmelsstatus",
                column: "erstattet_av_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_hjemmelsstatus_mal_rettskilde",
                table: "hjemmelsstatus",
                column: "mal_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "IX_hjemmelsstatus_opphevet_av_node_id",
                table: "hjemmelsstatus",
                column: "opphevet_av_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_hjemmelsstatus_opphevet_av_rettskilde_id",
                table: "hjemmelsstatus",
                column: "opphevet_av_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_hjemmelsstatus_rettskilde",
                table: "hjemmelsstatus",
                column: "rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_hjemmelsstatus_status",
                table: "hjemmelsstatus",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_hjemmelsstatus_videreforing_node_id",
                table: "hjemmelsstatus",
                column: "videreforing_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_hjemmelsstatus_videreforing_rettskilde_id",
                table: "hjemmelsstatus",
                column: "videreforing_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ux_hjemmelsstatus_rettskilde_hjemmel",
                table: "hjemmelsstatus",
                column: "rettskilde_hjemmel_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hjemmelsstatus");
        }
    }
}
