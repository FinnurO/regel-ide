using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilVirksomhetskatalogOgRollemodell : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "organisasjonsform_kode",
                table: "virksomheter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "overordnet_enhet_id",
                table: "virksomheter",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sektorkode",
                table: "virksomheter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "sist_brreg_synkronisert",
                table: "virksomheter",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "virksomhet_id",
                table: "begreper",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "definisjon",
                table: "begreper",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "begrepstype",
                table: "begreper",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "begrepskategori",
                table: "begreper",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lovkilde_id",
                table: "begreper",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "virksomhet_referanse_id",
                table: "begreper",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "myndighetstildelinger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    rolle_begrep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paragrafspenn_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    vilkaar = table.Column<string>(type: "text", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("myndighetstildelinger_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_myndighetstildelinger_begreper_rolle_begrep_id",
                        column: x => x.rolle_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_myndighetstildelinger_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_myndighetstildelinger_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "virksomhet_kandidater",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_eid = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Venter"),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    behandlet_av = table.Column<string>(type: "text", nullable: true),
                    behandlet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("virksomhet_kandidater_pkey", x => x.Id);
                    table.CheckConstraint("ck_virksomhet_kandidater_status", "status IN ('Venter', 'Godkjent', 'Avvist')");
                    table.ForeignKey(
                        name: "FK_virksomhet_kandidater_rettskilder_rettskilde_id",
                        column: x => x.rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_virksomhet_kandidater_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "virksomhet_nettsider",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    merknad = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("virksomhet_nettsider_pkey", x => x.Id);
                    table.CheckConstraint("ck_virksomhet_nettsider_type", "type IN ('Hovedside', 'Ovrig')");
                    table.ForeignKey(
                        name: "FK_virksomhet_nettsider_virksomheter_virksomhet_id",
                        column: x => x.virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_virksomheter_overordnet_enhet_id",
                table: "virksomheter",
                column: "overordnet_enhet_id");

            migrationBuilder.CreateIndex(
                name: "IX_begreper_lovkilde_id",
                table: "begreper",
                column: "lovkilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_begreper_virksomhet_referanse",
                table: "begreper",
                column: "virksomhet_referanse_id");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_rollebegrep_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori = 'rolle' AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper",
                sql: "begrepskategori IS NULL OR begrepskategori IN ('virksomhet', 'rolle')");

            migrationBuilder.CreateIndex(
                name: "ix_myndighetstildelinger_hjemmel",
                table: "myndighetstildelinger",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_myndighetstildelinger_rolle_begrep",
                table: "myndighetstildelinger",
                column: "rolle_begrep_id");

            migrationBuilder.CreateIndex(
                name: "ix_myndighetstildelinger_virksomhet",
                table: "myndighetstildelinger",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ix_virksomhet_kandidater_rettskilde",
                table: "virksomhet_kandidater",
                column: "rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_virksomhet_kandidater_virksomhet",
                table: "virksomhet_kandidater",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ux_virksomhet_kandidater_virksomhet_node",
                table: "virksomhet_kandidater",
                columns: new[] { "virksomhet_id", "rettskilde_id", "node_eid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_virksomhet_nettsider_virksomhet",
                table: "virksomhet_nettsider",
                column: "virksomhet_id");

            migrationBuilder.AddForeignKey(
                name: "FK_begreper_rettskilder_lovkilde_id",
                table: "begreper",
                column: "lovkilde_id",
                principalTable: "rettskilder",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_begreper_virksomheter_virksomhet_referanse_id",
                table: "begreper",
                column: "virksomhet_referanse_id",
                principalTable: "virksomheter",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_virksomheter_virksomheter_overordnet_enhet_id",
                table: "virksomheter",
                column: "overordnet_enhet_id",
                principalTable: "virksomheter",
                principalColumn: "Id");

            // Datamigrasjon (docs/20 §2.1): "fylke" var den gamle, grove Forvaltningsniva-verdien for
            // fylkeskommuner — allerede seedede rader beholder den gamle verdien for alltid ellers,
            // siden OrganisasjonsregisterSeed kun fyller inn NULL-felter (??=), aldri overskriver et
            // allerede satt Forvaltningsniva. Én rad-for-rad-idempotent UPDATE her sikrer at
            // EKSISTERENDE seedede fylkeskommuner får riktig verdi uansett — trygt å kjøre flere ganger.
            migrationBuilder.Sql("UPDATE virksomheter SET forvaltningsniva = 'fylkeskommune' WHERE forvaltningsniva = 'fylke';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_begreper_rettskilder_lovkilde_id",
                table: "begreper");

            migrationBuilder.DropForeignKey(
                name: "FK_begreper_virksomheter_virksomhet_referanse_id",
                table: "begreper");

            migrationBuilder.DropForeignKey(
                name: "FK_virksomheter_virksomheter_overordnet_enhet_id",
                table: "virksomheter");

            migrationBuilder.DropTable(
                name: "myndighetstildelinger");

            migrationBuilder.DropTable(
                name: "virksomhet_kandidater");

            migrationBuilder.DropTable(
                name: "virksomhet_nettsider");

            migrationBuilder.DropIndex(
                name: "IX_virksomheter_overordnet_enhet_id",
                table: "virksomheter");

            migrationBuilder.DropIndex(
                name: "IX_begreper_lovkilde_id",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ix_begreper_virksomhet_referanse",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_rollebegrep_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "organisasjonsform_kode",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "overordnet_enhet_id",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "sektorkode",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "sist_brreg_synkronisert",
                table: "virksomheter");

            migrationBuilder.DropColumn(
                name: "begrepskategori",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "lovkilde_id",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "virksomhet_referanse_id",
                table: "begreper");

            migrationBuilder.AlterColumn<Guid>(
                name: "virksomhet_id",
                table: "begreper",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "definisjon",
                table: "begreper",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "begrepstype",
                table: "begreper",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
