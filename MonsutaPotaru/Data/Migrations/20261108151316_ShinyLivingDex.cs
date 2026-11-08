using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <inheritdoc />
    public partial class ShinyLivingDex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LivingDexes_OwnerId_Game",
                table: "LivingDexes");

            migrationBuilder.AddColumn<bool>(
                name: "Shiny",
                table: "LivingDexes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_LivingDexes_OwnerId_Game_Shiny",
                table: "LivingDexes",
                columns: new[] { "OwnerId", "Game", "Shiny" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sans la colonne, un living dex normal et un chromatique du même jeu violeraient l'index unique : les chromatiques partent.
            migrationBuilder.Sql("DELETE FROM LivingDexes WHERE Shiny = 1;");

            migrationBuilder.DropIndex(
                name: "IX_LivingDexes_OwnerId_Game_Shiny",
                table: "LivingDexes");

            migrationBuilder.DropColumn(
                name: "Shiny",
                table: "LivingDexes");

            migrationBuilder.CreateIndex(
                name: "IX_LivingDexes_OwnerId_Game",
                table: "LivingDexes",
                columns: new[] { "OwnerId", "Game" },
                unique: true);
        }
    }
}
