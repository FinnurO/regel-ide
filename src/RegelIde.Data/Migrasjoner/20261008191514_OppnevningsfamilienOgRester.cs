using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #352 «Kompetansemodellen: rester etter #341», 2026-10-08] Ny K-egenskap <c>undertype</c> på kanten
    /// (NULL = ikke angitt; lukket liste per type, <see cref="Strukturkanter.Undertyper"/>), familien <c>personell</c> →
    /// <c>oppnevning</c> i CHECK-en, og datadelen i <see cref="OppnevningMigrering"/>: R velger, K utpeking og K ansettelse
    /// blir K oppnevning med undertype; R ankeinstans_for blir K overproving med undertype anke; forelegging får familien
    /// kontroll. Teller før/etter og avbryter ved avvik.
    /// </summary>
    public partial class OppnevningsfamilienOgRester : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Den gamle familie-CHECK-en (med 'personell') må ut før datadelen bytter verdien, og den nye inn etterpå.
            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_familie",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.AddColumn<string>(
                name: "undertype",
                table: "strukturkanter",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_strukturkanter_undertype",
                table: "strukturkanter",
                sql: "undertype IS NULL OR (kategori = 'K' AND ((typekode = 'oppnevning' AND undertype IN ('valg', 'ansettelse', 'utpeking', 'oppnevning')) OR (typekode = 'overproving' AND undertype IN ('anke'))))");

            // Datadelen etter kolonnen og undertype-CHECK-en: konverteringen setter kategori, typekode og undertype i samme UPDATE.
            migrationBuilder.Sql(OppnevningMigrering.UpSql);

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_familie",
                table: "relasjonstype_konfigurasjon",
                sql: "familie IS NULL OR (kategori = 'K' AND familie IN ('struktur', 'oppnevning', 'styring', 'normgivning', 'kontroll', 'klage_overproving', 'vedtak', 'sanksjon'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_familie",
                table: "relasjonstype_konfigurasjon");

            // Datadelen FØR kolonnen fjernes — Down leser proveniensradene og skriver de gamle verdiene tilbake.
            migrationBuilder.Sql(OppnevningMigrering.DownSql);

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_familie",
                table: "relasjonstype_konfigurasjon",
                sql: "familie IS NULL OR (kategori = 'K' AND familie IN ('struktur', 'personell', 'styring', 'normgivning', 'kontroll', 'klage_overproving', 'vedtak', 'sanksjon'))");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strukturkanter_undertype",
                table: "strukturkanter");

            migrationBuilder.DropColumn(
                name: "undertype",
                table: "strukturkanter");
        }
    }
}
