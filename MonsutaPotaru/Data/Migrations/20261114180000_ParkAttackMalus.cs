using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// Malus « attaqué » des Pokémon du parc (ParkService.RecordAttackAsync) : fin du malus et niveaux de bonheur perdus.
    /// </summary>
    public partial class ParkAttackMalus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MalusLevels",
                table: "ParkMembers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "MalusUntil",
                table: "ParkMembers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MalusLevels",
                table: "ParkMembers");

            migrationBuilder.DropColumn(
                name: "MalusUntil",
                table: "ParkMembers");
        }
    }
}
