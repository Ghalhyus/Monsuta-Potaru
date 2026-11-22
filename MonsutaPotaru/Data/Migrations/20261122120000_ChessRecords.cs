using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// Mini-jeux → Échecs : parties terminées (vainqueur, équipes, fin), pour les statistiques des joueurs.
    /// </summary>
    public partial class ChessRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChessRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WhiteId = table.Column<string>(type: "TEXT", nullable: false),
                    BlackId = table.Column<string>(type: "TEXT", nullable: false),
                    WhiteTeam = table.Column<string>(type: "TEXT", nullable: false),
                    BlackTeam = table.Column<string>(type: "TEXT", nullable: false),
                    Moves = table.Column<int>(type: "INTEGER", nullable: false),
                    End = table.Column<string>(type: "TEXT", nullable: false),
                    WinnerId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChessRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChessRecords_BlackId",
                table: "ChessRecords",
                column: "BlackId");

            migrationBuilder.CreateIndex(
                name: "IX_ChessRecords_WhiteId",
                table: "ChessRecords",
                column: "WhiteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChessRecords");
        }
    }
}
