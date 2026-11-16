using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Complétion de Diamant, Perle, Platine, HeartGold et SoulSilver : les tables de <see cref="Gen4CompletionData"/>, reprises
/// des désassemblages pokediamond (fichiers binaires du jeu), pokeplatinum et pokeheartgold et des check-lists de
/// l'utilisateur, lues dans la sauvegarde par PKHeX (toutes les langues). Six sections, comme en 3e génération ; Pokédex
/// national (493). Les concours, les Zarbi, les formes de Motisma et les Ruines d'Alpha sont dans les quêtes annexes.
/// </summary>
public static class Gen4Completion
{
    private static readonly string[] Items = CompletionLabels.French.GetItemStrings(EntityContext.Gen4, GameVersion.HGSS);

    /// <summary>
    /// Étapes de la partie, dans l'ordre : histoire, Pokédex (les 493), dresseurs, objets, rencontres, quêtes (à la fin,
    /// les rubans : <see cref="CompletionRibbons"/> ; en HG / SS, puis le PokéAthlon : <see cref="CompletionPokeathlon"/>).
    /// </summary>
    public static List<CompletionStep> Steps(SAV4 sav, LivingDexGame game) =>
        CompletionGames.Steps(Rows(game), new CompletionFlags(sav), 493, Label);

    /// <summary>Lignes de la partie : la table du jeu, puis ses rubans.</summary>
    public static IReadOnlyList<CompletionRow> Rows(LivingDexGame game) => game switch
    {
        LivingDexGame.Platinum => [.. Gen4CompletionData.Platinum, .. CompletionRibbons.Sinnoh(platinum: true, heartGold: false)],
        LivingDexGame.HeartGold or LivingDexGame.SoulSilver =>
            [.. Gen4CompletionData.HeartGoldSoulSilver, .. CompletionRibbons.Sinnoh(platinum: false, heartGold: true),
                .. CompletionPokeathlon.Rows()],
        _ => [.. Gen4CompletionData.DiamondPearl, .. CompletionRibbons.Sinnoh(platinum: false, heartGold: false)],
    };

    /// <summary>Texte d'une étape : le sien, sinon l'équipe du dresseur, l'objet (une CT avec sa capacité) ou le Pokémon.</summary>
    public static string Label(CompletionRow row) =>
        row.Tm != 0 && row.Label is null ? CompletionLabels.Machine(row.Tm, Gen4CompletionData.TmMoves, 92) : CompletionLabels.Of(row, ItemName);

    /// <summary>Nom français d'un objet de 4e génération (numéro du jeu).</summary>
    public static string ItemName(int item) => item < Items.Length ? Items[item] : $"Objet {item}";
}
