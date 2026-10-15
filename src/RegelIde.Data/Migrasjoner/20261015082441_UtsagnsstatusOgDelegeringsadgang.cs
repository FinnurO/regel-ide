using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #367 «utsagnsstatus» + #335 «delegeringsadgang», Johanns beslutning 2026-10-15: én migrasjon] Kunnskapsstatus og
    /// godkjenning, analysehistorikk (entitetsstatus, erstatter, begrunnelse, tolkningsargumenter, premisser, profilversjon), versjonert
    /// hjemmel (hjemmel_node_id + resynk-flagg), delegeringsadgang + slutning i stedet for delegerbar, navneformen «Kongen» og
    /// fvl-kategoriens gyldighetstid. Datadelen og tellingen står i <see cref="UtsagnsstatusMigrering"/>; <c>Down</c> snur nøyaktig og
    /// nekter når noe ikke kan uttrykkes før #367. Kjørt etter #355 (ingen stablede migrasjoner).
    /// </summary>
    public partial class UtsagnsstatusOgDelegeringsadgang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_delegerbar",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_status",
                table: "strukturkanter");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "strukturkanter",
                type: "text",
                nullable: false,
                defaultValue: "godkjent",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "validert");

            migrationBuilder.AddColumn<string>(
                name: "begrunnelse",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delegering_slutning",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delegeringsadgang",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entitetsstatus",
                table: "strukturkanter",
                type: "text",
                nullable: false,
                defaultValue: "gjeldende");

            migrationBuilder.AddColumn<Guid>(
                name: "erstatter_kant_id",
                table: "strukturkanter",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hjemmel_endret_tidspunkt",
                table: "strukturkanter",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "hjemmel_node_id",
                table: "strukturkanter",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kunnskapsstatus",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profilversjon",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "tolkningsargumenter",
                table: "strukturkanter",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fvl_kategori_gjelder_fra",
                table: "relasjonstype_konfigurasjon",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fvl_kategori_gjelder_til",
                table: "relasjonstype_konfigurasjon",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fvl_kategori_hjemmel",
                table: "relasjonstype_konfigurasjon",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "strukturkant_premisser",
                columns: table => new
                {
                    kant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    premiss_kant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("strukturkant_premisser_pkey", x => new { x.kant_id, x.premiss_kant_id });
                    table.CheckConstraint("ck_strukturkant_premisser_ikke_selv", "kant_id <> premiss_kant_id");
                    table.ForeignKey(
                        name: "FK_strukturkant_premisser_strukturkanter_kant_id",
                        column: x => x.kant_id,
                        principalTable: "strukturkanter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_strukturkant_premisser_strukturkanter_premiss_kant_id",
                        column: x => x.premiss_kant_id,
                        principalTable: "strukturkanter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_strukturkanter_erstatter",
                table: "strukturkanter",
                column: "erstatter_kant_id");

            migrationBuilder.CreateIndex(
                name: "ix_strukturkanter_hjemmel_node",
                table: "strukturkanter",
                column: "hjemmel_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_strukturkant_premisser_premiss",
                table: "strukturkant_premisser",
                column: "premiss_kant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_strukturkanter_rettskilde_noder_hjemmel_node_id",
                table: "strukturkanter",
                column: "hjemmel_node_id",
                principalTable: "rettskilde_noder",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_strukturkanter_strukturkanter_erstatter_kant_id",
                table: "strukturkanter",
                column: "erstatter_kant_id",
                principalTable: "strukturkanter",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Datadelen (UtsagnsstatusMigrering.UpSql) mens delegerbar ennå finnes: statusene, hjemmelnoden, delegeringsadgangen (med
            // proveniens), navneformen «Kongen», fvl-gyldigheten og constraint-triggeren for premisser. CHECK-ene legges på etterpå.
            migrationBuilder.Sql(UtsagnsstatusMigrering.UpSql);

            migrationBuilder.DropColumn(
                name: "delegerbar",
                table: "strukturkanter");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_delegering_slutning",
                table: "strukturkanter",
                sql: "delegering_slutning IS NULL OR (kategori = 'K' AND delegeringsadgang = 'ikke_regulert' AND delegering_slutning IN ('trolig_delegerbar', 'trolig_ikke_delegerbar'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_delegeringsadgang",
                table: "strukturkanter",
                sql: "delegeringsadgang IS NULL OR (kategori = 'K' AND delegeringsadgang IN ('uttrykkelig_tillatt', 'uttrykkelig_forbudt', 'ikke_regulert'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_entitetsstatus",
                table: "strukturkanter",
                sql: "entitetsstatus IN ('gjeldende', 'foreldet')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_fortolkning",
                table: "strukturkanter",
                sql: "kunnskapsstatus IS NULL OR kunnskapsstatus <> 'fortolkning' OR (begrunnelse IS NOT NULL AND cardinality(tolkningsargumenter) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_kunnskapsstatus",
                table: "strukturkanter",
                sql: "kunnskapsstatus IS NULL OR kunnskapsstatus IN ('tekstfunn', 'normalisering', 'fortolkning', 'avledet', 'eksternt_dokumentert', 'uavklart', 'omstridt', 'foreldet')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_kunnskapsstatus_grunnlag",
                table: "strukturkanter",
                sql: "(kunnskapsstatus IS NULL OR kunnskapsstatus <> 'eksternt_dokumentert' OR kilde_utenfor_korpus_tekst IS NOT NULL) AND (kunnskapsstatus IS NULL OR kunnskapsstatus NOT IN ('tekstfunn', 'normalisering') OR (hjemmel_rettskilde_id IS NOT NULL AND hjemmel_eid IS NOT NULL)) AND (kunnskapsstatus IS NULL OR kunnskapsstatus <> 'foreldet' OR entitetsstatus = 'foreldet')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_status",
                table: "strukturkanter",
                sql: "status IN ('maskinelt_foreslatt', 'manuelt_foreslatt', 'faglig_vurdert', 'godkjent', 'avvist')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_tolkningsargumenter",
                table: "strukturkanter",
                sql: "tolkningsargumenter IS NULL OR tolkningsargumenter <@ ARRAY['ordlyd', 'kontekst_system', 'formal', 'forarbeider', 'praksis', 'reelle_hensyn', 'antitetisk_analogisk']::text[]");

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_fvl_gyldighet",
                table: "relasjonstype_konfigurasjon",
                sql: "(fvl_kategori IS NOT NULL OR (fvl_kategori_hjemmel IS NULL AND fvl_kategori_gjelder_fra IS NULL AND fvl_kategori_gjelder_til IS NULL)) AND (fvl_kategori_gjelder_fra IS NULL OR fvl_kategori_gjelder_til IS NULL OR fvl_kategori_gjelder_fra <= fvl_kategori_gjelder_til)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_delegering_slutning",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_delegeringsadgang",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_entitetsstatus",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_fortolkning",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_kunnskapsstatus",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_kunnskapsstatus_grunnlag",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_status",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_tolkningsargumenter",
                table: "strukturkanter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_fvl_gyldighet",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.AddColumn<bool>(
                name: "delegerbar",
                table: "strukturkanter",
                type: "boolean",
                nullable: true);

            // Datadelen før kolonnene og tabellen forsvinner: avbryter hvis noe ikke kan uttrykkes før #367, ellers delegerbar fra
            // proveniensen, statusene tilbake, taggene tilbake til «Kongen i statsråd» og triggeren ut.
            migrationBuilder.Sql(UtsagnsstatusMigrering.DownSql);

            migrationBuilder.DropForeignKey(
                name: "FK_strukturkanter_rettskilde_noder_hjemmel_node_id",
                table: "strukturkanter");

            migrationBuilder.DropForeignKey(
                name: "FK_strukturkanter_strukturkanter_erstatter_kant_id",
                table: "strukturkanter");

            migrationBuilder.DropTable(
                name: "strukturkant_premisser");

            migrationBuilder.DropIndex(
                name: "ix_strukturkanter_erstatter",
                table: "strukturkanter");

            migrationBuilder.DropIndex(
                name: "ix_strukturkanter_hjemmel_node",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "begrunnelse",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "delegering_slutning",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "delegeringsadgang",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "entitetsstatus",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "erstatter_kant_id",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "hjemmel_endret_tidspunkt",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "hjemmel_node_id",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "kunnskapsstatus",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "profilversjon",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "tolkningsargumenter",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "fvl_kategori_gjelder_fra",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.DropColumn(
                name: "fvl_kategori_gjelder_til",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.DropColumn(
                name: "fvl_kategori_hjemmel",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "strukturkanter",
                type: "text",
                nullable: false,
                defaultValue: "validert",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "godkjent");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_delegerbar",
                table: "strukturkanter",
                sql: "delegerbar IS NULL OR kategori = 'K'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_status",
                table: "strukturkanter",
                sql: "status IN ('foreslatt_av_ai', 'validert')");
        }
    }
}
