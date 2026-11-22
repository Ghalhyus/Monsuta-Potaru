using System.Text.RegularExpressions;
using MonsutaPotaru.Data;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Complétion d'Épée, Bouclier, Diamant Étincelant, Perle Scintillante et Légendes Pokémon : Arceus : les tables de
/// <see cref="Gen8CompletionData"/>, reprises des listes de drapeaux de FlagsEditorEX et des check-lists de l'utilisateur
/// (pas de désassemblage pour la Switch), lues dans la sauvegarde par PKHeX (toutes les langues). Pas de dresseurs
/// (FlagsEditorEX ne donne pas leur lieu). Pokédex : Épée / Bouclier, les Pokémon des trois Pokédex régionaux (Galar,
/// Isolarmure, Couronneige : les DLC comptent toujours) ; DÉ/PS, le national de 493 ; Légendes Arceus, les 242 de Hisui,
/// chacun en trois étapes (capturé, recherche au niveau 10, Parfait).
/// </summary>
public static partial class Gen8Completion
{
    private static readonly string[] SwordShieldItems = CompletionLabels.French.GetItemStrings(EntityContext.Gen8, GameVersion.SWSH);
    private static readonly string[] BrilliantDiamondItems = CompletionLabels.French.GetItemStrings(EntityContext.Gen8b, GameVersion.BDSP);
    private static readonly ushort[] TechnicalMachines = PersonalInfo8SWSH.MachineMovesTechnical.ToArray();
    private static readonly ushort[] TechnicalRecords = PersonalInfo8SWSH.MachineMovesRecord.ToArray();

    /// <summary>Pokédex d'Épée / Bouclier : les espèces des Pokédex de Galar, de l'Isolarmure et de Couronneige (une forme suffit).</summary>
    public static readonly IReadOnlyList<int> SwordShieldPokedex = [.. Enumerable.Range(1, 898).Where(species =>
        Enumerable.Range(0, PersonalTable.SWSH[species].FormCount).Select(form => PersonalTable.SWSH.GetFormEntry((ushort)species, (byte)form))
            .Any(p => p.PokeDexIndex > 0 || p.ArmorDexIndex > 0 || p.CrownDexIndex > 0))];

    /// <summary>Pokédex de Hisui (Légendes Arceus), dans l'ordre national.</summary>
    public static readonly IReadOnlyList<int> HisuiPokedex = [.. Enumerable.Range(1, 905).Where(species =>
        Enumerable.Range(0, PersonalTable.LA[species].FormCount).Any(form => PersonalTable.LA.GetFormEntry((ushort)species, (byte)form).DexIndexHisui > 0))];

    /// <summary>Étapes de la partie, dans l'ordre : histoire, Pokédex, objets, rencontres, quêtes.</summary>
    public static List<CompletionStep> Steps(SaveFile sav, LivingDexGame game)
    {
        var flags = new CompletionFlags(sav);
        return game switch
        {
            LivingDexGame.Shield => CompletionGames.Steps(Gen8CompletionData.Shield, flags, SwordShieldPokedex, SwordShieldLabel),
            LivingDexGame.BrilliantDiamond => CompletionGames.Steps(Gen8CompletionData.BrilliantDiamond, flags, 493, SinnohLabel),
            LivingDexGame.ShiningPearl => CompletionGames.Steps(Gen8CompletionData.ShiningPearl, flags, 493, SinnohLabel),
            LivingDexGame.LegendsArceus => HisuiSteps(flags),
            _ => CompletionGames.Steps(Gen8CompletionData.Sword, flags, SwordShieldPokedex, SwordShieldLabel),
        };
    }

    /// <summary>Légendes Arceus : histoire, puis trois étapes par espèce de Hisui, puis le reste de la table.</summary>
    private static List<CompletionStep> HisuiSteps(CompletionFlags flags)
    {
        var steps = Gen8CompletionData.LegendsArceus.Select(r =>
            new CompletionStep(r.Section, r.Zone, r.Detail, CompletionLabels.Of(r, _ => ""), flags.State(r), r.Species, r.Form)).ToList();
        var dex = HisuiPokedex.SelectMany(n => new[] { ("D", "capturé"), ("LC", "recherche au niveau 10"), ("LP", "Parfait") }
            .Select(task => new CompletionStep(CompletionSection.Pokedex, "Pokédex", null,
                $"#{n:000} {CompletionLabels.French.specieslist[n]} : {task.Item2}",
                flags.Test($"{task.Item1}:{n}") ? CompletionState.Done : CompletionState.Missing, (ushort)n)));
        return [.. steps.Where(s => s.Section == CompletionSection.Story), .. dex, .. steps.Where(s => s.Section != CompletionSection.Story)];
    }

    /// <summary>Texte d'une étape d'Épée / Bouclier : le sien, sinon l'objet (« CT00 Ultimapoing », « DT00 Danse Lames ») ou le Pokémon.</summary>
    public static string SwordShieldLabel(CompletionRow row)
    {
        if (row.Tm == 0 || row.Label is not null)
            return CompletionLabels.Of(row, SwordShieldItemName);
        return row.Tm <= 100
            ? $"CT{row.Tm - 1:00} {CompletionLabels.French.movelist[TechnicalMachines[row.Tm - 1]]}"
            : $"DT{row.Tm - 101:00} {CompletionLabels.French.movelist[TechnicalRecords[row.Tm - 101]]}";
    }

    /// <summary>Texte d'une étape de DÉ/PS : CT01 à CT100 (les CS de Diamant / Perle devenues CT93 à CT100, dans le même ordre).</summary>
    public static string SinnohLabel(CompletionRow row) =>
        row.Tm != 0 && row.Label is null ? CompletionLabels.Machine(row.Tm, Gen4CompletionData.TmMoves, 100) : CompletionLabels.Of(row, SinnohItemName);

    /// <summary>Nom français d'un objet d'Épée / Bouclier, sans les précisions de PKHeX.</summary>
    public static string SwordShieldItemName(int item) => item < SwordShieldItems.Length ? Clean(SwordShieldItems[item]) : $"Objet {item}";

    /// <summary>Nom français d'un objet de DÉ/PS, sans les précisions de PKHeX.</summary>
    public static string SinnohItemName(int item) => item < BrilliantDiamondItems.Length ? Clean(BrilliantDiamondItems[item]) : $"Objet {item}";

    private static string Clean(string name) => Disambiguation().Replace(name, "");

    /// <summary>Précisions ajoutées par PKHeX aux noms en double : « (G6-8) », « (-) », « (DEPS) ».</summary>
    [GeneratedRegex(@"(?: \([^)]*\))+$")]
    private static partial Regex Disambiguation();
}
