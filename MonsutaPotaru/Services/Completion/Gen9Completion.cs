using System.Text.RegularExpressions;
using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Complétion d'Écarlate et de Violet, DLC compris (Le Masque Turquoise, Le Disque Indigo : toujours comptés, à leur
/// lieu) : les tables de <see cref="Gen9CompletionData"/>, reprises de la liste de drapeaux de FlagsEditorEX et de la
/// check-list de l'utilisateur (pas de désassemblage pour la Switch), lues dans la sauvegarde par PKHeX (toutes les
/// langues). Pokédex : les 692 espèces présentes dans le jeu ; CT : faite dès que sa recette est débloquée.
/// </summary>
public static partial class Gen9Completion
{
    private static readonly string[] Items = CompletionLabels.French.GetItemStrings(EntityContext.Gen9, GameVersion.SV);

    /// <summary>Étapes de la partie, dans l'ordre : histoire, Pokédex, dresseurs, objets, rencontres, quêtes.</summary>
    public static List<CompletionStep> Steps(SaveFile sav, LivingDexGame game) =>
        CompletionGames.Steps(game == LivingDexGame.Violet ? Gen9CompletionData.Violet : Gen9CompletionData.Scarlet,
            new CompletionFlags(sav), Gen9CompletionData.Pokedex, Label);

    /// <summary>Texte d'une étape : le sien, sinon la CT (« CT001 Bélier ») ou l'objet, ou le Pokémon.</summary>
    public static string Label(CompletionRow row) =>
        row.Tm != 0 && row.Label is null
            ? $"CT{row.Tm:000} {CompletionLabels.French.movelist[PersonalInfo9SV.MachineMoves[row.Tm]]}"
            : CompletionLabels.Of(row, ItemName);

    /// <summary>Nom français d'un objet, sans les précisions de PKHeX.</summary>
    public static string ItemName(int item) => item < Items.Length ? Disambiguation().Replace(Items[item], "") : $"Objet {item}";

    /// <summary>Précisions ajoutées par PKHeX aux noms en double : « (G6-8) », « (EV) ».</summary>
    [GeneratedRegex(@"(?: \([^)]*\))+$")]
    private static partial Regex Disambiguation();
}
