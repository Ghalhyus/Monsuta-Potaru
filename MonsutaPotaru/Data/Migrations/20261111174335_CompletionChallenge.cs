using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// « Défis » → « Complétion (100 %) » : table <c>Completions</c> (une par jeu et par compte, une sauvegarde pour une
    /// seule, supprimée avec elle) ; la page publique <c>PublicPages.Completion</c> (= 32) est cochée pour tous les comptes
    /// existants, comme pour les nouveaux (<c>PublicPages.All</c>).
    /// </summary>
    public partial class CompletionChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Completions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Game = table.Column<int>(type: "INTEGER", nullable: false),
                    StoredSaveId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Completions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Completions_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Completions_StoredSaves_StoredSaveId",
                        column: x => x.StoredSaveId,
                        principalTable: "StoredSaves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Completions_OwnerId_Game",
                table: "Completions",
                columns: new[] { "OwnerId", "Game" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Completions_StoredSaveId",
                table: "Completions",
                column: "StoredSaveId",
                unique: true);

            migrationBuilder.Sql("UPDATE AspNetUsers SET PublicPages = PublicPages | 32;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE AspNetUsers SET PublicPages = PublicPages & ~32;");

            migrationBuilder.DropTable(
                name: "Completions");
        }
    }
}
