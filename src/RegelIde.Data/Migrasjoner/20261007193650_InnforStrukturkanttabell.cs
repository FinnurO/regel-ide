using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <summary>
    /// [Ny, issue #311 «Strukturmodell 6: én typestyrt kanttabell», 2026-10-07] Skjema + data i ÉN migrasjon
    /// (Johanns valg A: full konsolidering):
    /// <list type="number">
    /// <item><c>relasjonstype_konfigurasjon.kategori</c> (eksisterende rader → <c>'R'</c>); unik på (kategori, kode).
    /// Startsettet fra docs/33 §4.3 seedes ved oppstart (<see cref="Strukturkanter.SeedStartsettAsync"/>), ikke her.</item>
    /// <item>Tabellen <c>strukturkanter</c> (<see cref="StrukturkantEntitet"/>).</item>
    /// <item>Alle rader i <c>virksomhet_relasjoner</c>, <c>gruppe_medlemskap</c> og <c>myndighetstildelinger</c>
    /// flyttes 1:1 med samme id (<see cref="StrukturkantMigrering.DataSql"/>) — antallet kontrolleres, og
    /// migrasjonen avbrytes ved avvik.</item>
    /// <item>Organene som virksomheter, organ-begrepene som navneformer (<see cref="StrukturkantMigrering.OrganSql"/>).</item>
    /// <item><c>'organ'</c> fjernes fra <c>ck_begreper_begrepskategori</c> og nodebegrep-indeksene.</item>
    /// <item>De tre gamle tabellene DROPPES.</item>
    /// </list>
    /// <para>
    /// <b>Hvorfor droppe, ikke la dem stå tomme med et spor</b> (issue #311 lot valget stå åpent, CLAUDE.md §7
    /// krever spor): en tom tabell som fortsatt finnes i skjemaet inviterer til nettopp den parallelle
    /// skriveveien saken skal fjerne, og EF-entitetene er borte, så ingen kode kan bruke den. Sporet står
    /// der en leser leter: <c>[FJERNET]</c>-kommentarer i <c>Entiteter.cs</c> og <c>RegelIdeDbContext.cs</c>,
    /// proveniensraden per flyttet kant (<c>handling = 'migrert'</c>, <c>kilde_referanser.fra_tabell</c>),
    /// og denne migrasjonens <c>Down</c>, som gjenskaper tabellene.
    /// </para>
    /// <para>
    /// <b>Hvorfor Stortinget lages her og ikke i en oppstartsseed:</b> <c>'organ'</c> kan bare fjernes fra
    /// CHECK-en etter at organ-begrepene er navneformer, og det krever at virksomhetene finnes — i samme
    /// migrasjon. En migrasjon kan ikke kalle Brreg; verdiene er derfor lest fra et committet øyeblikksbilde
    /// (<c>Seed/brreg-971524960-stortinget.json</c>, hentet 2026-10-07), og en test holder SQL-en og fila like.
    /// En gated oppstartsseed (CLAUDE.md §4) ville kommet ETTER migrasjonen og kunne ikke løst rekkefølgen.
    /// </para>
    /// <para>
    /// <b><c>Down</c> er bevisst upresis</b> (samme linje som <c>InnforNodetypeakse</c>): R/M/I-kanter som
    /// lar seg uttrykke i de gamle tabellene flyttes tilbake; K/O/A/G/T, negative kanter og kanter med bare
    /// kilde utenfor korpus (som ikke kan bli myndighetstildeling) går tapt. Organ-virksomhetene og
    /// navneformene blir stående.
    /// </para>
    /// </summary>
    public partial class InnforStrukturkanttabell : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rekkefølgen er håndredigert etter `dotnet ef migrations add` — se klassekommentaren.
            // 1. Typekonfigurasjonen får kategori; identiteten blir (kategori, kode).
            migrationBuilder.DropIndex(
                name: "ux_relasjonstype_konfigurasjon_kode",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.AddColumn<string>(
                name: "kategori",
                table: "relasjonstype_konfigurasjon",
                type: "text",
                nullable: false,
                defaultValue: "R");

            migrationBuilder.CreateIndex(
                name: "ux_relasjonstype_konfigurasjon_kategori_kode",
                table: "relasjonstype_konfigurasjon",
                columns: new[] { "kategori", "kode" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon",
                sql: "kategori IN ('R', 'K', 'M', 'O', 'A', 'G', 'I', 'T')");

            // 2. Den nye tabellen.
            migrationBuilder.CreateTable(
                name: "strukturkanter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kategori = table.Column<string>(type: "text", nullable: false),
                    typekode = table.Column<string>(type: "text", nullable: false),
                    fra_virksomhet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fra_begrep_id = table.Column<Guid>(type: "uuid", nullable: true),
                    til_virksomhet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    til_begrep_id = table.Column<Guid>(type: "uuid", nullable: true),
                    objekt = table.Column<string>(type: "text", nullable: true),
                    avgrensning_paragrafspenn_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    avgrensning_tekst = table.Column<string>(type: "text", nullable: true),
                    polaritet = table.Column<string>(type: "text", nullable: false, defaultValue: "positiv"),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hjemmel_eid = table.Column<string>(type: "text", nullable: true),
                    kilde_utenfor_korpus_tekst = table.Column<string>(type: "text", nullable: true),
                    kilde_utenfor_korpus_lenke = table.Column<string>(type: "text", nullable: true),
                    gyldig_fra = table.Column<DateOnly>(type: "date", nullable: true),
                    gyldig_til = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "validert"),
                    oppdagelses_kilde = table.Column<string>(type: "text", nullable: false, defaultValue: "manuell"),
                    kommentar = table.Column<string>(type: "text", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("strukturkanter_pkey", x => x.Id);
                    table.CheckConstraint("ck_strukturkanter_fra_en", "(fra_virksomhet_id IS NULL) <> (fra_begrep_id IS NULL)");
                    table.CheckConstraint("ck_strukturkanter_ikke_selv", "(fra_virksomhet_id IS NULL OR til_virksomhet_id IS NULL OR fra_virksomhet_id <> til_virksomhet_id) AND (fra_begrep_id IS NULL OR til_begrep_id IS NULL OR fra_begrep_id <> til_begrep_id)");
                    table.CheckConstraint("ck_strukturkanter_kategori", "kategori IN ('R', 'K', 'M', 'O', 'A', 'G', 'I', 'T')");
                    table.CheckConstraint("ck_strukturkanter_kilde", "hjemmel_rettskilde_id IS NOT NULL OR kilde_utenfor_korpus_tekst IS NOT NULL");
                    table.CheckConstraint("ck_strukturkanter_polaritet", "polaritet IN ('positiv', 'negativ')");
                    table.CheckConstraint("ck_strukturkanter_status", "status IN ('foreslatt_av_ai', 'validert')");
                    table.CheckConstraint("ck_strukturkanter_til_hoyst_en", "til_virksomhet_id IS NULL OR til_begrep_id IS NULL");
                    table.CheckConstraint("ck_strukturkanter_til_pakrevd", "kategori IN ('K', 'T') OR til_virksomhet_id IS NOT NULL OR til_begrep_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_strukturkanter_begreper_fra_begrep_id",
                        column: x => x.fra_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_strukturkanter_begreper_til_begrep_id",
                        column: x => x.til_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_strukturkanter_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_strukturkanter_virksomheter_fra_virksomhet_id",
                        column: x => x.fra_virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_strukturkanter_virksomheter_til_virksomhet_id",
                        column: x => x.til_virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_strukturkanter_fra_begrep",
                table: "strukturkanter",
                column: "fra_begrep_id");

            migrationBuilder.CreateIndex(
                name: "ix_strukturkanter_fra_virksomhet",
                table: "strukturkanter",
                column: "fra_virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ix_strukturkanter_hjemmel",
                table: "strukturkanter",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_strukturkanter_kategori_type",
                table: "strukturkanter",
                columns: new[] { "kategori", "typekode" });

            migrationBuilder.CreateIndex(
                name: "ix_strukturkanter_status",
                table: "strukturkanter",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_strukturkanter_til_begrep",
                table: "strukturkanter",
                column: "til_begrep_id");

            migrationBuilder.CreateIndex(
                name: "ix_strukturkanter_til_virksomhet",
                table: "strukturkanter",
                column: "til_virksomhet_id");

            // 3. Data: de tre gamle tabellene → strukturkanter (1:1, samme id, tellingen kontrolleres).
            migrationBuilder.Sql(StrukturkantMigrering.DataSql);

            // 4. Organene (Johanns beslutning på #311): Stortinget + «Kongen i statsråd» som virksomheter,
            //    organ-begrepene → navneformer. MÅ stå før 'organ' fjernes fra CHECK-en under.
            migrationBuilder.Sql(StrukturkantMigrering.OrganSql);

            // 5. 'organ' ut av begrepskategoriene og nodebegrep-indeksene.
            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper",
                column: "term",
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade') AND entitetsstatus = 'gjeldende' AND lovkilde_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade') AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper",
                sql: "begrepskategori IS NULL OR begrepskategori IN ('virksomhet', 'gruppe', 'klasse', 'rolle', 'omrade')");

            // 6. De gamle tabellene — se klassekommentaren for hvorfor de droppes og ikke står tomme.
            migrationBuilder.DropTable(
                name: "gruppe_medlemskap");

            migrationBuilder.DropTable(
                name: "myndighetstildelinger");

            migrationBuilder.DropTable(
                name: "virksomhet_relasjoner");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Typer utenfor R kan ikke stå i en tabell som er unik på kode alene.
            migrationBuilder.Sql("DELETE FROM relasjonstype_konfigurasjon WHERE kategori <> 'R';");

            migrationBuilder.DropIndex(
                name: "ux_relasjonstype_konfigurasjon_kategori_kode",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.DropCheckConstraint(
                name: "ck_relasjonstype_konfigurasjon_kategori",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper");

            migrationBuilder.DropIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper");

            migrationBuilder.DropCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper");

            migrationBuilder.DropColumn(
                name: "kategori",
                table: "relasjonstype_konfigurasjon");

            migrationBuilder.CreateTable(
                name: "gruppe_medlemskap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    gyldig_fra = table.Column<DateOnly>(type: "date", nullable: true),
                    gyldig_til = table.Column<DateOnly>(type: "date", nullable: true),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    overordnet_gruppe_begrep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paragrafspenn_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "validert"),
                    underordnet_gruppe_begrep_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gruppe_medlemskap_pkey", x => x.Id);
                    table.CheckConstraint("ck_gruppe_medlemskap_ikke_selv", "overordnet_gruppe_begrep_id <> underordnet_gruppe_begrep_id");
                    table.CheckConstraint("ck_gruppe_medlemskap_status", "status IN ('foreslatt_av_ai', 'validert')");
                    table.ForeignKey(
                        name: "FK_gruppe_medlemskap_begreper_overordnet_gruppe_begrep_id",
                        column: x => x.overordnet_gruppe_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gruppe_medlemskap_begreper_underordnet_gruppe_begrep_id",
                        column: x => x.underordnet_gruppe_begrep_id,
                        principalTable: "begreper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gruppe_medlemskap_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "myndighetstildelinger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    gruppe_begrep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gyldig_fra = table.Column<DateOnly>(type: "date", nullable: true),
                    gyldig_til = table.Column<DateOnly>(type: "date", nullable: true),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    paragrafspenn_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    sist_endret_av = table.Column<string>(type: "text", nullable: true),
                    sist_endret_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "validert"),
                    vilkaar = table.Column<string>(type: "text", nullable: true),
                    virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("myndighetstildelinger_pkey", x => x.Id);
                    table.CheckConstraint("ck_myndighetstildelinger_status", "status IN ('foreslatt_av_ai', 'validert')");
                    table.ForeignKey(
                        name: "FK_myndighetstildelinger_begreper_gruppe_begrep_id",
                        column: x => x.gruppe_begrep_id,
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
                name: "virksomhet_relasjoner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    entitetsstatus = table.Column<string>(type: "text", nullable: false, defaultValue: "gjeldende"),
                    fra_virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hjemmel_eid = table.Column<string>(type: "text", nullable: true),
                    hjemmel_rettskilde_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kommentar = table.Column<string>(type: "text", nullable: true),
                    opprettet_av = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    relasjons_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "validert"),
                    til_virksomhet_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("virksomhet_relasjoner_pkey", x => x.Id);
                    table.CheckConstraint("ck_virksomhet_relasjoner_status", "status IN ('foreslatt_av_ai', 'validert')");
                    table.ForeignKey(
                        name: "FK_virksomhet_relasjoner_rettskilder_hjemmel_rettskilde_id",
                        column: x => x.hjemmel_rettskilde_id,
                        principalTable: "rettskilder",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_virksomhet_relasjoner_virksomheter_fra_virksomhet_id",
                        column: x => x.fra_virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_virksomhet_relasjoner_virksomheter_til_virksomhet_id",
                        column: x => x.til_virksomhet_id,
                        principalTable: "virksomheter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_relasjonstype_konfigurasjon_kode",
                table: "relasjonstype_konfigurasjon",
                column: "kode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_fast_term",
                table: "begreper",
                column: "term",
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade', 'organ') AND entitetsstatus = 'gjeldende' AND lovkilde_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_begreper_nodebegrep_term_lovkilde",
                table: "begreper",
                columns: new[] { "term", "lovkilde_id" },
                unique: true,
                filter: "begrepskategori IN ('gruppe', 'klasse', 'rolle', 'omrade', 'organ') AND entitetsstatus = 'gjeldende'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_begreper_begrepskategori",
                table: "begreper",
                sql: "begrepskategori IS NULL OR begrepskategori IN ('virksomhet', 'gruppe', 'klasse', 'rolle', 'omrade', 'organ')");

            migrationBuilder.CreateIndex(
                name: "ix_gruppe_medlemskap_hjemmel",
                table: "gruppe_medlemskap",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_gruppe_medlemskap_underordnet",
                table: "gruppe_medlemskap",
                column: "underordnet_gruppe_begrep_id");

            migrationBuilder.CreateIndex(
                name: "ux_gruppe_medlemskap_par",
                table: "gruppe_medlemskap",
                columns: new[] { "overordnet_gruppe_begrep_id", "underordnet_gruppe_begrep_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_myndighetstildelinger_gruppe_begrep",
                table: "myndighetstildelinger",
                column: "gruppe_begrep_id");

            migrationBuilder.CreateIndex(
                name: "ix_myndighetstildelinger_hjemmel",
                table: "myndighetstildelinger",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_myndighetstildelinger_virksomhet",
                table: "myndighetstildelinger",
                column: "virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ix_virksomhet_relasjoner_fra",
                table: "virksomhet_relasjoner",
                column: "fra_virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "IX_virksomhet_relasjoner_hjemmel_rettskilde_id",
                table: "virksomhet_relasjoner",
                column: "hjemmel_rettskilde_id");

            migrationBuilder.CreateIndex(
                name: "ix_virksomhet_relasjoner_til",
                table: "virksomhet_relasjoner",
                column: "til_virksomhet_id");

            migrationBuilder.CreateIndex(
                name: "ux_virksomhet_relasjoner_fra_til_type",
                table: "virksomhet_relasjoner",
                columns: new[] { "fra_virksomhet_id", "til_virksomhet_id", "relasjons_type" },
                unique: true,
                filter: "entitetsstatus = 'gjeldende'");

            // Flytt det som lar seg uttrykke tilbake (se klassekommentaren), FØR strukturkanter droppes.
            migrationBuilder.Sql("""
                INSERT INTO virksomhet_relasjoner ("Id", fra_virksomhet_id, til_virksomhet_id, relasjons_type,
                    hjemmel_rettskilde_id, hjemmel_eid, kommentar, entitetsstatus, status, opprettet_av, opprettet_tidspunkt)
                SELECT k."Id", k.fra_virksomhet_id, k.til_virksomhet_id, k.typekode, k.hjemmel_rettskilde_id, k.hjemmel_eid,
                    COALESCE(k.kommentar, k.kilde_utenfor_korpus_tekst), 'gjeldende', k.status, k.opprettet_av, k.opprettet_tidspunkt
                FROM strukturkanter k
                WHERE k.kategori = 'R' AND k.polaritet = 'positiv'
                  AND k.fra_virksomhet_id IS NOT NULL AND k.til_virksomhet_id IS NOT NULL
                ON CONFLICT DO NOTHING;
                INSERT INTO gruppe_medlemskap ("Id", overordnet_gruppe_begrep_id, underordnet_gruppe_begrep_id, hjemmel_rettskilde_id,
                    paragrafspenn_json, gyldig_fra, gyldig_til, status, opprettet_av, opprettet_tidspunkt, sist_endret_av, sist_endret_tidspunkt)
                SELECT k."Id", k.til_begrep_id, k.fra_begrep_id, k.hjemmel_rettskilde_id, k.avgrensning_paragrafspenn_json,
                    k.gyldig_fra, k.gyldig_til, k.status, k.opprettet_av, k.opprettet_tidspunkt, k.sist_endret_av, k.sist_endret_tidspunkt
                FROM strukturkanter k
                WHERE k.kategori = 'M' AND k.polaritet = 'positiv' AND k.fra_begrep_id IS NOT NULL AND k.til_begrep_id IS NOT NULL
                  AND k.hjemmel_rettskilde_id IS NOT NULL
                ON CONFLICT DO NOTHING;
                INSERT INTO myndighetstildelinger ("Id", gruppe_begrep_id, virksomhet_id, hjemmel_rettskilde_id, paragrafspenn_json,
                    vilkaar, gyldig_fra, gyldig_til, status, opprettet_av, opprettet_tidspunkt, sist_endret_av, sist_endret_tidspunkt)
                SELECT k."Id", k.til_begrep_id, k.fra_virksomhet_id, k.hjemmel_rettskilde_id, k.avgrensning_paragrafspenn_json,
                    k.avgrensning_tekst, k.gyldig_fra, k.gyldig_til, k.status, k.opprettet_av, k.opprettet_tidspunkt,
                    k.sist_endret_av, k.sist_endret_tidspunkt
                FROM strukturkanter k
                WHERE k.kategori IN ('M', 'I') AND k.polaritet = 'positiv' AND k.fra_virksomhet_id IS NOT NULL
                  AND k.til_begrep_id IS NOT NULL AND k.hjemmel_rettskilde_id IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "strukturkanter");
        }
    }
}
