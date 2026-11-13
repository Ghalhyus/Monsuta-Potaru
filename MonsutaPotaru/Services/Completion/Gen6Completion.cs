using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Complétion de X, Y, Rubis Oméga et Saphir Alpha : les tables de <see cref="Gen6CompletionData"/>, reprises des listes
/// de drapeaux de FlagsEditorEX et des check-lists de l'utilisateur (pas de désassemblage pour la 3DS), lues dans la
/// sauvegarde par PKHeX (toutes les langues). Pokédex national (721). Maison de Combat, Super Training et (Rubis Oméga /
/// Saphir Alpha) concours dans les quêtes annexes. Pas encore de dresseurs : il faut les ROM des jeux.
/// </summary>
public static class Gen6Completion
{
    private static readonly string[] Items = CompletionLabels.French.GetItemStrings(EntityContext.Gen6, GameVersion.ORAS);
    private static readonly ushort[] XyMachines = LearnSource6XY.MachineMoves.ToArray();
    private static readonly ushort[] OrasMachines = LearnSource6AO.MachineMoves.ToArray();

    /// <summary>Étapes de la partie, dans l'ordre : histoire, Pokédex (les 721), objets, rencontres, quêtes.</summary>
    public static List<CompletionStep> Steps(SAV6 sav, LivingDexGame game)
    {
        var (rows, machines) = game switch
        {
            LivingDexGame.Y => (Gen6CompletionData.Y, XyMachines),
            LivingDexGame.OmegaRuby => (Gen6CompletionData.OmegaRuby, OrasMachines),
            LivingDexGame.AlphaSapphire => (Gen6CompletionData.AlphaSapphire, OrasMachines),
            _ => (Gen6CompletionData.X, XyMachines),
        };
        return CompletionGames.Steps(rows, new CompletionFlags(sav), 721, row => Label(row, machines));
    }

    /// <summary>Texte d'une étape : le sien, sinon l'objet (une CT ou une CS avec sa capacité) ou le Pokémon.</summary>
    public static string Label(CompletionRow row, IReadOnlyList<ushort> machines) =>
        row.Tm != 0 && row.Label is null ? CompletionLabels.Machine(row.Tm, machines, 100) : CompletionLabels.Of(row, ItemName);

    /// <summary>Nom français d'un objet de 6e génération (numéro du jeu).</summary>
    public static string ItemName(int item) => item < Items.Length ? Items[item] : $"Objet {item}";
}
