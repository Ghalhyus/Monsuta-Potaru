using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// Statistiques des mini-jeux : pièces mangées par chaque camp d'une partie d'échecs (0 pour les parties déjà
    /// enregistrées, dont les coups ne sont pas gardés).
    /// </summary>
    public partial class ChessCaptures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BlackCaptures",
                table: "ChessRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WhiteCaptures",
                table: "ChessRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlackCaptures",
                table: "ChessRecords");

            migrationBuilder.DropColumn(
                name: "WhiteCaptures",
                table: "ChessRecords");
        }
    }
}
