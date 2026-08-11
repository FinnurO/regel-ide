using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegelIde.Data.Migrasjoner
{
    /// <inheritdoc />
    public partial class LeggTilRettskildeNodeEmbeddinger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rettskilde_node_embeddinger",
                columns: table => new
                {
                    node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    embedding = table.Column<List<double>>(type: "double precision[]", nullable: false),
                    modell = table.Column<string>(type: "text", nullable: false),
                    opprettet_tidspunkt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("rettskilde_node_embeddinger_pkey", x => x.node_id);
                    table.ForeignKey(
                        name: "FK_rettskilde_node_embeddinger_rettskilde_noder_node_id",
                        column: x => x.node_id,
                        principalTable: "rettskilde_noder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rettskilde_node_embeddinger");
        }
    }
}
