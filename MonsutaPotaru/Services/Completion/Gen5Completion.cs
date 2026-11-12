using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Complétion de Noir, Blanc, Noir 2 et Blanc 2 : les tables de <see cref="Gen5CompletionData"/>, reprises du désassemblage
/// pokebw2 (Noir 2 / Blanc 2), de la ROM de Pokémon Version Noire de l'utilisateur (Noir / Blanc), des listes de drapeaux de
/// FlagsEditorEX et des check-lists de l'utilisateur, lues dans la sauvegarde par PKHeX (toutes les langues). Six sections ;
/// Pokédex national (649). Métro de Combat, PWT et médailles dans les quêtes annexes.
/// </summary>
public static class Gen5Completion
{
    private static readonly string[] Items = CompletionLabels.French.GetItemStrings(EntityContext.Gen5, GameVersion.B2W2);
    private static readonly ushort[] TmMoves = PersonalInfo5BW.MachineMoves.ToArray();

    /// <summary>Étapes de la partie, dans l'ordre : histoire, Pokédex (les 649), dresseurs, objets, rencontres, quêtes.</summary>
    public static List<CompletionStep> Steps(SAV5 sav, LivingDexGame game) =>
        CompletionGames.Steps(game switch
        {
            LivingDexGame.White => Gen5CompletionData.White,
            LivingDexGame.Black2 => Gen5CompletionData.Black2,
            LivingDexGame.White2 => Gen5CompletionData.White2,
            _ => Gen5CompletionData.Black,
        }, new CompletionFlags(sav), 649, Label);

    /// <summary>Texte d'une étape : le sien, sinon l'équipe du dresseur, l'objet (une CT avec sa capacité) ou le Pokémon.</summary>
    public static string Label(CompletionRow row) =>
        row.Tm != 0 && row.Label is null ? CompletionLabels.Machine(row.Tm, TmMoves, 95) : CompletionLabels.Of(row, ItemName);

    /// <summary>Nom français d'un objet de 5e génération (numéro du jeu).</summary>
    public static string ItemName(int item) => item < Items.Length ? Items[item] : $"Objet {item}";
}
