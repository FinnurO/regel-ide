using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilHandlingOgRettighetUtvidelser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rå SQL i stedet for AlterColumn — EF genererer ikke en gyldig USING-klausul for
            // text -> text[], og en direkte cast kastes av Postgres ("cannot cast type text to
            // text[]"). Eksisterende enkeltverdi blir ett-elements liste; NULL blir tom liste
            // (matcher entitetens `= []`-default, ikke NULL).
            migrationBuilder.Sql(
                "ALTER TABLE tjenester ALTER COLUMN malgruppe TYPE text[] " +
                "USING CASE WHEN malgruppe IS NULL THEN ARRAY[]::text[] ELSE ARRAY[malgruppe] END;");
            migrationBuilder.Sql("ALTER TABLE tjenester ALTER COLUMN malgruppe SET NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE tjenester ALTER COLUMN malgruppe SET DEFAULT '{}';");

            // defaultValueSql er nødvendig her (ikke bare på entitetens C#-side `= []`) — uten den
            // feiler ADD COLUMN NOT NULL på en tabell som allerede har rader (bekreftet 2026-08-20:
            // fungerte på en helt fersk/tom database, men slo feil mot den ekte, langvarige
            // utviklings-databasen som allerede har seedede Tjeneste-rader).
            migrationBuilder.AddColumn<List<string>>(
                name: "livshendelser",
                table: "tjenester",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "los_klassifisering",
                table: "tjenester",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tjenesteomrade",
                table: "tjenester",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "handlinger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tjeneste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    navn = table.Column<string>(type: "text", nullable: false),
                    handlingstype = table.Column<string>(type: "text", nullable: false),
                    bruksomraade = table.Column<string>(type: "text", nullable: true),
                    utfort_av = table.Column<string>(type: "text", nullable: true),
                    rotnode_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kanaler = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    behandlingstid = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    kostnad = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    vedlegg = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    veiledningstekst = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    arsaker = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    resultat = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    merknad = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "utkast"),
                    versjon = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("handlinger_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_handlinger_regelnoder_rotnode_id",
                        column: x => x.rotnode_id,
                        principalTable: "regelnoder",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_handlinger_tjenester_tjeneste_id",
                        column: x => x.tjeneste_id,
                        principalTable: "tjenester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_handlinger_rotnode_id",
                table: "handlinger",
                column: "rotnode_id");

            migrationBuilder.CreateIndex(
                name: "ix_handlinger_tjeneste",
                table: "handlinger",
                column: "tjeneste_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "handlinger");

            migrationBuilder.DropColumn(
                name: "livshendelser",
                table: "tjenester");

            migrationBuilder.DropColumn(
                name: "los_klassifisering",
                table: "tjenester");

            migrationBuilder.DropColumn(
                name: "tjenesteomrade",
                table: "tjenester");

            migrationBuilder.Sql(
                "ALTER TABLE tjenester ALTER COLUMN malgruppe TYPE text " +
                "USING CASE WHEN array_length(malgruppe, 1) IS NULL THEN NULL ELSE malgruppe[1] END;");
            migrationBuilder.Sql("ALTER TABLE tjenester ALTER COLUMN malgruppe DROP NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE tjenester ALTER COLUMN malgruppe DROP DEFAULT;");
        }
    }
}
