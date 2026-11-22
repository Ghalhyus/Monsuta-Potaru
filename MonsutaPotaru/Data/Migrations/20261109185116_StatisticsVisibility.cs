using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// Page « Statistiques » visible sur le profil (<c>PublicPages.Statistics</c> = 16) : cochée pour tous les comptes
    /// existants (choix de l'utilisateur), comme les nouveaux (<c>PublicPages.All</c>). Le modèle ne change pas.
    /// </summary>
    public partial class StatisticsVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE AspNetUsers SET PublicPages = PublicPages | 16;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE AspNetUsers SET PublicPages = PublicPages & 15;");
        }
    }
}
