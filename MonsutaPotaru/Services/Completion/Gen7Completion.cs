using System.Text.RegularExpressions;
using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Complétion de Soleil, Lune, Ultra-Soleil, Ultra-Lune et Let's Go, Pikachu / Évoli : les tables de
/// <see cref="Gen7CompletionData"/>, reprises des listes de drapeaux de FlagsEditorEX et des check-lists de l'utilisateur
/// (pas de désassemblage pour la 3DS ni la Switch), lues dans la sauvegarde par PKHeX (toutes les langues). Pokédex
/// national (802, 807) ; Let's Go : les 151 de Kanto, Meltan et Melmetal. Arbre de Combat, Zygarde, Scanner des Îles et
/// Experts de Let's Go dans les quêtes annexes. Pas encore de dresseurs : il faut les ROM des jeux.
/// </summary>
public static partial class Gen7Completion
{
    private static readonly string[] Items = CompletionLabels.French.GetItemStrings(EntityContext.Gen7, GameVersion.USUM);
    private static readonly string[] LetsGoItems = CompletionLabels.French.GetItemStrings(EntityContext.Gen7b, GameVersion.GG);
    private static readonly ushort[] AlolaMachines = PersonalInfo7.MachineMoves.ToArray();
    private static readonly ushort[] LetsGoMachines = LearnSource7GG.MachineMoves.ToArray();

    /// <summary>Pokédex de Let's Go : les 151 de Kanto, puis Meltan et Melmetal.</summary>
    public static readonly IReadOnlyList<int> LetsGoPokedex = [.. Enumerable.Range(1, 151), 808, 809];

    /// <summary>Étapes de la partie, dans l'ordre : histoire, Pokédex, objets, rencontres, quêtes.</summary>
    public static List<CompletionStep> Steps(SaveFile sav, LivingDexGame game)
    {
        var flags = new CompletionFlags(sav);
        return game switch
        {
            LivingDexGame.LetsGoPikachu => CompletionGames.Steps(Gen7CompletionData.LetsGoPikachu, flags, LetsGoPokedex, LetsGoLabel),
            LivingDexGame.LetsGoEevee => CompletionGames.Steps(Gen7CompletionData.LetsGoEevee, flags, LetsGoPokedex, LetsGoLabel),
            LivingDexGame.Moon => CompletionGames.Steps(Gen7CompletionData.Moon, flags, 802, AlolaLabel),
            LivingDexGame.UltraSun => CompletionGames.Steps(Gen7CompletionData.UltraSun, flags, 807, AlolaLabel),
            LivingDexGame.UltraMoon => CompletionGames.Steps(Gen7CompletionData.UltraMoon, flags, 807, AlolaLabel),
            _ => CompletionGames.Steps(Gen7CompletionData.Sun, flags, 802, AlolaLabel),
        };
    }

    /// <summary>Texte d'une étape d'Alola : le sien, sinon l'objet (une CT avec sa capacité) ou le Pokémon.</summary>
    public static string AlolaLabel(CompletionRow row) =>
        row.Tm != 0 && row.Label is null ? CompletionLabels.Machine(row.Tm, AlolaMachines, 100) : CompletionLabels.Of(row, ItemName);

    /// <summary>Texte d'une étape de Let's Go (60 CT).</summary>
    public static string LetsGoLabel(CompletionRow row) =>
        row.Tm != 0 && row.Label is null ? CompletionLabels.Machine(row.Tm, LetsGoMachines, 60) : CompletionLabels.Of(row, LetsGoItemName);

    /// <summary>Nom français d'un objet de Soleil à Ultra-Lune (numéro du jeu), sans les précisions de PKHeX (« [Z] »).</summary>
    public static string ItemName(int item) => item < Items.Length ? Clean(Items[item]) : $"Objet {item}";

    /// <summary>Nom français d'un objet de Let's Go.</summary>
    public static string LetsGoItemName(int item) => item < LetsGoItems.Length ? Clean(LetsGoItems[item]) : $"Objet {item}";

    private static string Clean(string name) => Disambiguation().Replace(name, "");

    /// <summary>Précisions ajoutées par PKHeX aux noms en double : « Normazélite [Z] », « Passe Bateau (GP/GE) ».</summary>
    [GeneratedRegex(@" (?:\[Z\]|\((?:SM|GP/GE|GP|GE)\))$")]
    private static partial Regex Disambiguation();
}
