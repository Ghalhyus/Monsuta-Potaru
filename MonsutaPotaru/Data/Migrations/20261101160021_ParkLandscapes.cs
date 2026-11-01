using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// Plusieurs parcs par joueur, un par paysage (table Parks) : le parc de chacun devient sa Forêt, avec ses
    /// agrandissements (colonne AspNetUsers.ParkExpansions, supprimée) et ses Pokémon (ParkMembers.ParkId). Retour : la
    /// Forêt redevient le parc unique, les Pokémon des autres parcs le quittent (ils restent dans la collection).
    /// </summary>
    public partial class ParkLandscapes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Landscape = table.Column<int>(type: "INTEGER", nullable: false),
                    Expansions = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parks_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parks_OwnerId_Landscape",
                table: "Parks",
                columns: new[] { "OwnerId", "Landscape" },
                unique: true);

            // La Forêt (Landscape = 1) de chaque compte, avec les agrandissements de son parc
            migrationBuilder.Sql("INSERT INTO Parks (OwnerId, Landscape, Expansions) SELECT Id, 1, ParkExpansions FROM AspNetUsers;");

            migrationBuilder.AddColumn<int>(
                name: "ParkId",
                table: "ParkMembers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Ses Pokémon y vivent (un Pokémon sans compte, s'il y en avait, quitte le parc)
            migrationBuilder.Sql("""
                UPDATE ParkMembers SET ParkId = COALESCE((SELECT p.Id FROM Parks p WHERE p.OwnerId = ParkMembers.OwnerId AND p.Landscape = 1), 0);
                DELETE FROM ParkMembers WHERE ParkId = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ParkMembers_ParkId",
                table: "ParkMembers",
                column: "ParkId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParkMembers_Parks_ParkId",
                table: "ParkMembers",
                column: "ParkId",
                principalTable: "Parks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "ParkExpansions",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParkExpansions",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE AspNetUsers SET ParkExpansions = COALESCE((SELECT p.Expansions FROM Parks p WHERE p.OwnerId = AspNetUsers.Id AND p.Landscape = 1), 0);
                DELETE FROM ParkMembers WHERE ParkId NOT IN (SELECT Id FROM Parks WHERE Landscape = 1);
                """);

            // ParkMembers sans ParkId, reconstruite ici : EF la reconstruirait à la fin de la migration, après la suppression
            // de Parks, qui viderait ParkMembers (suppression en cascade) tant qu'elle la référence
            migrationBuilder.Sql("""
                CREATE TABLE "ef_temp_ParkMembers" (
                    "BankPokemonId" INTEGER NOT NULL CONSTRAINT "PK_ParkMembers" PRIMARY KEY,
                    "AddedAt" TEXT NOT NULL,
                    "OwnerId" TEXT NOT NULL,
                    CONSTRAINT "FK_ParkMembers_BankPokemon_BankPokemonId" FOREIGN KEY ("BankPokemonId") REFERENCES "BankPokemon" ("Id") ON DELETE CASCADE
                );
                INSERT INTO "ef_temp_ParkMembers" ("BankPokemonId", "AddedAt", "OwnerId") SELECT "BankPokemonId", "AddedAt", "OwnerId" FROM "ParkMembers";
                DROP TABLE "ParkMembers";
                ALTER TABLE "ef_temp_ParkMembers" RENAME TO "ParkMembers";
                CREATE INDEX "IX_ParkMembers_OwnerId" ON "ParkMembers" ("OwnerId");
                """);

            migrationBuilder.DropTable(
                name: "Parks");
        }
    }
}
