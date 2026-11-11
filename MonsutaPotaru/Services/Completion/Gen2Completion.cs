using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Complétion d'Or, Argent et Cristal (versions internationales) : les tables de <see cref="Gen2CompletionData"/>, reprises
/// des désassemblages pokegold et pokecrystal et des check-lists de l'utilisateur, lues dans les drapeaux de la sauvegarde.
/// Six sections : celles de la 1re génération et les quêtes annexes (Ruines d'Alpha, Fratrie des sept jours, Balls de
/// Fargas…).
/// </summary>
public static class Gen2Completion
{
    private static readonly string[] Items = CompletionLabels.French.GetItemStrings(EntityContext.Gen2, GameVersion.C);

    /// <summary>Étapes de la partie, dans l'ordre : histoire, Pokédex (les 251), dresseurs, objets, rencontres, quêtes.</summary>
    public static List<CompletionStep> Steps(SAV2 sav, LivingDexGame game) =>
        CompletionGames.Steps(game == LivingDexGame.Crystal ? Gen2CompletionData.Crystal : Gen2CompletionData.GoldSilver,
            new CompletionFlags(sav), 251, Label);

    /// <summary>Texte d'une étape : le sien, sinon l'équipe du dresseur, l'objet (une CT avec sa capacité) ou le Pokémon.</summary>
    public static string Label(CompletionRow row) =>
        row.Tm != 0 && row.Label is null ? CompletionLabels.Machine(row.Tm, Gen2CompletionData.TmMoves) : CompletionLabels.Of(row, ItemName);

    /// <summary>Nom français d'un objet d'Or / Argent / Cristal.</summary>
    public static string ItemName(int item) => Items[item];
}
