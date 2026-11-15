using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <inheritdoc />
    public partial class Gts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GtsChanged",
                table: "SaveModifications",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GtsPokemon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<byte>(type: "INTEGER", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Context = table.Column<byte>(type: "INTEGER", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", nullable: false),
                    Species = table.Column<ushort>(type: "INTEGER", nullable: false),
                    Form = table.Column<byte>(type: "INTEGER", nullable: false),
                    SpeciesName = table.Column<string>(type: "TEXT", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<byte>(type: "INTEGER", nullable: false),
                    Gender = table.Column<byte>(type: "INTEGER", nullable: false),
                    HeldItem = table.Column<int>(type: "INTEGER", nullable: false),
                    IsShiny = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLegal = table.Column<bool>(type: "INTEGER", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    FamilyContext = table.Column<byte>(type: "INTEGER", nullable: false),
                    FamilyRegion = table.Column<byte>(type: "INTEGER", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    StoredSaveId = table.Column<int>(type: "INTEGER", nullable: true),
                    WantedSpecies = table.Column<ushort>(type: "INTEGER", nullable: false),
                    WantedMinLevel = table.Column<byte>(type: "INTEGER", nullable: true),
                    WantedMaxLevel = table.Column<byte>(type: "INTEGER", nullable: true),
                    WantedGender = table.Column<byte>(type: "INTEGER", nullable: true),
                    WantedShiny = table.Column<bool>(type: "INTEGER", nullable: true),
                    WantedLegalOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArrivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GtsPokemon", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GtsPokemon_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GtsPokemon_StoredSaves_StoredSaveId",
                        column: x => x.StoredSaveId,
                        principalTable: "StoredSaves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GtsPokemon_OwnerId",
                table: "GtsPokemon",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_GtsPokemon_Status_Species",
                table: "GtsPokemon",
                columns: new[] { "Status", "Species" });

            migrationBuilder.CreateIndex(
                name: "IX_GtsPokemon_StoredSaveId",
                table: "GtsPokemon",
                column: "StoredSaveId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GtsPokemon");

            migrationBuilder.DropColumn(
                name: "GtsChanged",
                table: "SaveModifications");
        }
    }
}
