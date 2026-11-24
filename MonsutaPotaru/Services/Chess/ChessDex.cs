using System.Globalization;
using System.Text;
using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>
/// Un Pokémon que l'éditeur des brouillons d'échecs propose : espèce et forme, nom français (« Raichu d'Alola »), ses vrais
/// types (un ou deux : la pièce en garde un, choisi dans l'éditeur).
/// </summary>
public sealed record ChessDexEntry(ushort Species, byte Form, string Name, IReadOnlyList<PokeType> Types)
{
    /// <summary>Image fixe de la forme (celle de l'espèce si elle n'en a pas).</summary>
    public string SpriteUrl => PokemonSprites.Url(Species, Form, EntityContext.Gen9, false, 0, false);

    /// <summary>Nom sans accents ni majuscules, pour la recherche (« raichu d'alola »).</summary>
    internal string SearchKey { get; } = ChessDex.Simplify(Name);
}

/// <summary>
/// Tous les Pokémon et leurs formes, pour l'éditeur des brouillons d'échecs (administration) : les 1 025 espèces, chaque
/// forme que PKHeX connaît (formes régionales, Méga-Évolutions de Légendes Z-A, formes de Motisma…). Types d'après la table
/// d'Écarlate et Violet, qui les donne pour toutes les espèces (même absentes du jeu), sinon de Légendes Z-A (Méga-Raichu).
/// </summary>
public static class ChessDex
{
    public const int MaxSpecies = 1025;

    private static readonly GameStrings French = GameInfo.GetStrings("fr");

    private static readonly Lazy<IReadOnlyList<ChessDexEntry>> Entries = new(Build);

    private static readonly Lazy<Dictionary<(ushort, byte), ChessDexEntry>> ByKey =
        new(() => All.ToDictionary(e => (e.Species, e.Form)));

    /// <summary>Tous les Pokémon proposés, par numéro puis par forme.</summary>
    public static IReadOnlyList<ChessDexEntry> All => Entries.Value;

    /// <summary>Ce Pokémon (valeur venue du navigateur : null s'il n'existe pas).</summary>
    public static ChessDexEntry? Find(ushort species, byte form) => ByKey.Value.GetValueOrDefault((species, form));

    /// <summary>Nom français d'une espèce et d'une forme (« Raichu d'Alola ») ; le numéro si elle est inconnue.</summary>
    public static string Name(ushort species, byte form) => Find(species, form)?.Name ?? $"n° {species} (forme {form})";

    /// <summary>
    /// Recherche par nom (sans tenir compte des accents ni des majuscules) ou par numéro (« 26 ») : ceux dont le nom
    /// commence par le texte d'abord, puis ceux qui le contiennent ; <paramref name="max"/> au plus.
    /// </summary>
    public static IReadOnlyList<ChessDexEntry> Search(string? text, int max = 30)
    {
        var query = Simplify(text ?? "").Trim().TrimStart('#');
        if (query.Length == 0)
            return [];
        if (ushort.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return [.. All.Where(e => e.Species == number).Take(max)];
        return [.. All.Where(e => e.SearchKey.Contains(query, StringComparison.Ordinal))
            .OrderBy(e => e.SearchKey.StartsWith(query, StringComparison.Ordinal) ? 0 : 1)
            .Take(max)];
    }

    /// <summary>Minuscules sans accents, apostrophe droite (« Lippouti » → « lippouti », « d’Effroi » → « d'effroi »).</summary>
    internal static string Simplify(string text)
    {
        var decomposed = text.Replace('’', '\'').ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string([.. decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
    }

    private static List<ChessDexEntry> Build()
    {
        var list = new List<ChessDexEntry>();
        for (ushort species = 1; species <= MaxSpecies; species++)
        {
            var names = FormConverter.GetFormList(species, French.types, French.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen9a);
            var sv = PersonalTable.SV[species].FormCount;
            var count = Math.Max(Math.Max(sv, PersonalTable.ZA[species].FormCount), (byte)1);
            for (byte form = 0; form < count; form++)
            {
                IPersonalTable table = form < sv ? PersonalTable.SV : PersonalTable.ZA;
                var info = table.GetFormEntry(species, form);
                PokeType[] types = info.Type1 == info.Type2 ? [TypeOf(info.Type1)] : [TypeOf(info.Type1), TypeOf(info.Type2)];
                list.Add(new(species, form, FormName(French.specieslist[species], form, form < names.Length ? names[form] : null), types));
            }
        }
        return list;
    }

    /// <summary>Type de PKHeX (dans l'ordre des jeux : Normal, Combat, Vol…) vers celui des échecs, par son nom anglais.</summary>
    private static PokeType TypeOf(byte type) => Enum.Parse<PokeType>(((MoveType)type).ToString());

    /// <summary>
    /// Nom affiché d'une forme, comme dans les équipes du jeu : « Raichu d'Alola », « Caninos d'Hisui », « Motisma Lavage »,
    /// « Méga-Dracaufeu X », « Primo-Kyogre » ; la forme de base garde le nom de l'espèce.
    /// </summary>
    internal static string FormName(string species, byte form, string? formName)
    {
        if (formName is null)
            return form == 0 ? species : $"{species} (forme {form})";
        if (formName.Length == 0 || formName == "Normal")
            return species;
        if (formName.StartsWith("Méga", StringComparison.Ordinal))
            return $"Méga-{species}{formName[4..]}"; // « Méga X » → « Méga-Dracaufeu X »
        if (formName.EndsWith("-Méga", StringComparison.Ordinal))
            return $"Méga-{species} {formName[..^5]}"; // Mistigrix « ♂-Méga »
        if (formName == "Primo")
            return $"Primo-{species}";
        foreach (var (region, article) in Regions)
            if (formName == region || formName.StartsWith(region + " ", StringComparison.Ordinal))
                return $"{species} {article}{formName}"; // « Tauros de Paldea Combative »
        return $"{species} {formName}";
    }

    private static readonly (string Region, string Article)[] Regions =
        [("Alola", "d'"), ("Hisui", "d'"), ("Galar", "de "), ("Paldea", "de ")];
}
