using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonsutaPotaru.Migrations
{
    /// <summary>
    /// Le mode hybride devient la disposition par défaut des écrans de combat, pour les nouveaux comptes seulement : les
    /// comptes existants sans disposition enregistrée (donc « Empilés », l'ancien défaut, jamais écrit) reçoivent
    /// « "combat":"empile" » dans leurs préférences. Pas de changement de modèle.
    /// </summary>
    public partial class HybridBattleDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE AspNetUsers SET SortPreferences = '{"combat":"empile"}'
                WHERE SortPreferences IS NULL OR trim(SortPreferences) = '' OR NOT json_valid(SortPreferences);
                """);
            migrationBuilder.Sql("""
                UPDATE AspNetUsers SET SortPreferences = json_set(SortPreferences, '$.combat', 'empile')
                WHERE json_valid(SortPreferences) AND json_extract(SortPreferences, '$.combat') IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retour à l'ancien défaut (« Empilés ») : l'hybride implicite s'écrit, puis « empile » redevient implicite.
            migrationBuilder.Sql("""
                UPDATE AspNetUsers SET SortPreferences = '{"combat":"hybride"}'
                WHERE SortPreferences IS NULL OR trim(SortPreferences) = '';
                """);
            migrationBuilder.Sql("""
                UPDATE AspNetUsers SET SortPreferences = json_set(SortPreferences, '$.combat', 'hybride')
                WHERE json_valid(SortPreferences) AND json_extract(SortPreferences, '$.combat') IS NULL;
                """);
            migrationBuilder.Sql("""
                UPDATE AspNetUsers SET SortPreferences = json_remove(SortPreferences, '$.combat')
                WHERE json_valid(SortPreferences) AND json_extract(SortPreferences, '$.combat') = 'empile';
                """);
            migrationBuilder.Sql("UPDATE AspNetUsers SET SortPreferences = NULL WHERE SortPreferences = '{}';");
        }
    }
}
