using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Complétion de Rubis, Saphir, Émeraude, Rouge Feu et Vert Feuille : les tables de <see cref="Gen3CompletionData"/>,
/// reprises des désassemblages pokeruby, pokeemerald et pokefirered et des check-lists de l'utilisateur, lues dans les
/// drapeaux de la sauvegarde (par PKHeX : toutes les langues). Six sections, comme en 2e génération ; Pokédex national
/// (386). Les concours, la Zone de Combat et les Maîtres des capacités sont dans les quêtes annexes.
/// </summary>
public static class Gen3Completion
{
    private static readonly string[] Items = CompletionLabels.French.GetItemStrings(EntityContext.Gen3, GameVersion.E);

    /// <summary>Étapes de la partie, dans l'ordre : histoire, Pokédex (les 386), dresseurs, objets, rencontres, quêtes.</summary>
    public static List<CompletionStep> Steps(SAV3 sav, LivingDexGame game) =>
        CompletionGames.Steps(game switch
        {
            LivingDexGame.Emerald => Gen3CompletionData.Emerald,
            LivingDexGame.FireRed or LivingDexGame.LeafGreen => Gen3CompletionData.FireRedLeafGreen,
            _ => Gen3CompletionData.RubySapphire,
        }, new CompletionFlags(sav), 386, Label);

    /// <summary>Texte d'une étape : le sien, sinon l'équipe du dresseur, l'objet (une CT avec sa capacité) ou le Pokémon.</summary>
    public static string Label(CompletionRow row) =>
        row.Tm != 0 && row.Label is null ? CompletionLabels.Machine(row.Tm, Gen3CompletionData.TmMoves) : CompletionLabels.Of(row, ItemName);

    /// <summary>Nom français d'un objet de 3e génération (numéro du jeu).</summary>
    public static string ItemName(int item) => item < Items.Length ? Items[item] : $"Objet {item}";
}
