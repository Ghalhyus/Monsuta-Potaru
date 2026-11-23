using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// Mini-jeux → Cache-cache : une ligne par joueur d'une partie terminée (cibles trouvées, résultat), pour les
    /// statistiques. Écrite à la main (<c>dotnet ef</c> absent), d'après la migration <c>ChessRecords</c>.
    /// </summary>
    public partial class HideSeekRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HideSeekRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Found = table.Column<int>(type: "INTEGER", nullable: false),
                    Rounds = table.Column<int>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HideSeekRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HideSeekRecords_GameId",
                table: "HideSeekRecords",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_HideSeekRecords_UserId",
                table: "HideSeekRecords",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HideSeekRecords");
        }
    }
}
