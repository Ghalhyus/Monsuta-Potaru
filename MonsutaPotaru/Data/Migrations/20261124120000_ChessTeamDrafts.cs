using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// Administration → « Brouillons d'échecs » : équipes d'échecs proposées par les administrateurs pour essayer
    /// l'équilibrage (jamais jouées). Écrite à la main (<c>dotnet ef</c> absent), d'après la migration <c>HideSeekRecords</c>.
    /// </summary>
    public partial class ChessTeamDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChessTeamDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    BaseTeam = table.Column<string>(type: "TEXT", nullable: true),
                    Members = table.Column<string>(type: "TEXT", nullable: false),
                    Counted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChessTeamDrafts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChessTeamDrafts");
        }
    }
}
