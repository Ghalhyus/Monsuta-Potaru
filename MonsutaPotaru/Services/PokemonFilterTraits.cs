using PKHeX.Core;
using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>
/// Ce que regardent la recherche et les filtres de « Ma collection » (<see cref="PokemonCriteria"/>), pour un Pokémon lu
/// dans une sauvegarde et non enregistré : Pokémon à choisir d'un échange ou d'une équipe de combat. Valeurs simples, pour
/// que deux candidats relus restent égaux (les pages retrouvent le Pokémon choisi par égalité).
/// </summary>
/// <param name="Nickname">Nom affiché (« Œuf » pour un œuf) ; <paramref name="SpeciesName"/> vide pour un œuf : la
/// recherche ne révèle pas l'espèce d'un œuf, même de l'ami.</param>
public sealed record PokemonFilterTraits(ushort Species, bool IsEgg, string Nickname, string SpeciesName, byte Level, byte Gender,
    bool IsShiny, byte Type1, byte Type2, byte Nature, int Ability, byte IvHp, byte IvAtk, byte IvDef, byte IvSpA, byte IvSpD,
    byte IvSpe, FormKind FormKind, bool MegaReady)
{
    /// <summary>Caractéristiques d'un Pokémon, calculées comme celles de « Ma collection » (<see cref="PokemonTraits.Fill"/>).</summary>
    public static PokemonFilterTraits Of(PKM pk, PokemonView view)
    {
        var row = new BankPokemon { Species = pk.Species };
        PokemonTraits.Fill(row, pk);
        return new PokemonFilterTraits(pk.Species, pk.IsEgg, pk.IsEgg ? "Œuf" : view.Nickname, pk.IsEgg ? "" : view.SpeciesName,
            (byte)pk.CurrentLevel, pk.Gender, pk.IsShiny && !pk.IsEgg, row.Type1, row.Type2, row.Nature ?? 0, row.Ability,
            row.IvHp, row.IvAtk, row.IvDef, row.IvSpA, row.IvSpD, row.IvSpe, row.FormKind, row.MegaReady);
    }

    /// <summary>Ligne de collection équivalente, pour appliquer les filtres en mémoire avec le code de la base.</summary>
    public BankPokemon ToRow() => new()
    {
        Species = Species, IsEgg = IsEgg, Nickname = Nickname, SpeciesName = SpeciesName, Level = Level, Gender = Gender,
        IsShiny = IsShiny, Type1 = Type1, Type2 = Type2, Nature = Nature, Ability = Ability, IvHp = IvHp, IvAtk = IvAtk,
        IvDef = IvDef, IvSpA = IvSpA, IvSpD = IvSpD, IvSpe = IvSpe, FormKind = FormKind, MegaReady = MegaReady,
    };
}

/// <summary>Recherche et filtres de « Ma collection » appliqués à une liste de Pokémon à choisir (échange, équipe de combat).</summary>
public static class PokemonSearch
{
    private static readonly System.Globalization.CompareInfo French = System.Globalization.CultureInfo.GetCultureInfo("fr-FR").CompareInfo;

    /// <summary>
    /// Pokémon dont l'espèce ou le surnom contient <paramref name="search"/> (casse et accents ignorés) et qui passent les
    /// filtres, dans leur ordre de départ.
    /// </summary>
    public static List<T> Filter<T>(IEnumerable<T> items, string? search, PokemonCriteria criteria, Func<T, PokemonFilterTraits> traits)
    {
        var text = search?.Trim() ?? "";
        var rows = items.Select(item => (Item: item, Row: traits(item).ToRow()))
            .Where(x => text.Length == 0 || Contains(x.Row.SpeciesName, text) || Contains(x.Row.Nickname, text))
            .ToList();
        if (criteria.IsEmpty)
            return rows.Select(x => x.Item).ToList();
        var kept = criteria.Apply(rows.Select(x => x.Row).AsQueryable()).ToHashSet(ReferenceEqualityComparer.Instance);
        return rows.Where(x => kept.Contains(x.Row)).Select(x => x.Item).ToList();
    }

    /// <summary>Talents présents dans la liste (hors œufs et 1re et 2e génération), proposés par le filtre « Talent ».</summary>
    public static List<int> Abilities<T>(IEnumerable<T> items, Func<T, PokemonFilterTraits> traits) =>
        items.Select(traits).Where(t => !t.IsEgg && t.Ability > 0).Select(t => t.Ability).Distinct().ToList();

    private static bool Contains(string value, string text) =>
        French.IndexOf(value, text, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
}
