using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Complétion de Rouge, Bleu et Jaune (versions internationales) : les tables de <see cref="Gen1CompletionData"/>, reprises
/// des désassemblages pokered et pokeyellow, lues dans les drapeaux de la sauvegarde.
/// </summary>
public static class Gen1Completion
{
    private static readonly string[] Items = CompletionLabels.French.GetItemStrings(EntityContext.Gen1, GameVersion.RD);

    /// <summary>Noms d'objets de PKHeX corrigés (fautes de la liste française de 1re génération).</summary>
    private static readonly Dictionary<int, string> ItemFixes = new()
    {
        [36] = "Protéine", [69] = "Boîte Jetons", [76] = "Canne", [80] = "Huile", [82] = "Élixir",
    };

    private const int FirstHm = 196, FirstTm = 201;

    /// <summary>Étapes de la partie, dans l'ordre : histoire, Pokédex (les 151), dresseurs, objets, rencontres.</summary>
    public static List<CompletionStep> Steps(SAV1 sav, LivingDexGame game) =>
        CompletionGames.Steps(game == LivingDexGame.Yellow ? Gen1CompletionData.Yellow : Gen1CompletionData.RedBlue,
            new CompletionFlags(sav), 151, Label);

    /// <summary>Texte d'une étape : le sien, sinon l'équipe du dresseur, l'objet, les jetons ou le Pokémon.</summary>
    public static string Label(CompletionRow row) => CompletionLabels.Of(row, ItemName);

    /// <summary>Nom français d'un objet de Rouge / Bleu / Jaune ; une CT ou une CS avec sa capacité (« CT08 Plaquage »).</summary>
    public static string ItemName(int item) => item switch
    {
        >= FirstTm => CompletionLabels.Machine(item - FirstTm + 1, Gen1CompletionData.TmMoves),
        >= FirstHm => CompletionLabels.Machine(50 + item - FirstHm + 1, Gen1CompletionData.TmMoves),
        _ => ItemFixes.GetValueOrDefault(item) ?? Items[item],
    };
}
