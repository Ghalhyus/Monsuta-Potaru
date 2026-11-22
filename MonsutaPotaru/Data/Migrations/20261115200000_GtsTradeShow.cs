using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// GTS : animation d'échange que le déposant verra en récupérant le Pokémon reçu contre son offre (TradeShow en JSON).
    /// </summary>
    public partial class GtsTradeShow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TradeShow",
                table: "GtsPokemon",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TradeShow",
                table: "GtsPokemon");
        }
    }
}
