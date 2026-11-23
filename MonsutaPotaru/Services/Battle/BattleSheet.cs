using PKHeX.Core;

namespace MonsutaPotaru.Services;

/// <summary>Catégorie d'une capacité (en 3e génération, elle dépend du type : <see cref="BattleSheet"/>).</summary>
public enum BattleMoveCategory { Status, Physical, Special }

/// <summary>Capacité de la table générée (<see cref="BattleDexData"/>) : précision 0 quand elle n'échoue jamais.</summary>
public readonly record struct BattleMoveData(BattleMoveCategory Category, int Power, int Accuracy, string Text);

/// <summary>Type affiché sur la fiche : nom français, et nom anglais en minuscules (classe CSS de sa couleur).</summary>
public sealed record BattleSheetType(string Name, string Key);

/// <summary>Capacité d'un Pokémon sur sa fiche (PP inconnus : null).</summary>
public sealed record BattleSheetMove(string Name, BattleSheetType Type, int? Pp, int? MaxPp, BattleMoveCategory Category,
    int Power, int Accuracy, string Text)
{
    public string CategoryName => Category switch
    {
        BattleMoveCategory.Physical => "Physique",
        BattleMoveCategory.Special => "Spéciale",
        _ => "Statut",
    };

    /// <summary>Puissance, ou un tiret (capacité de statut, K.O. en un coup, puissance variable).</summary>
    public string PowerText => Power > 0 ? Power.ToString() : "—";

    /// <summary>Précision, ou un tiret (n'échoue jamais).</summary>
    public string AccuracyText => Accuracy > 0 ? Accuracy.ToString() : "—";
}

/// <summary>
/// Fiche d'un Pokémon de l'équipe pendant un combat (page « Combats » : écran de sélection, résumé, capacités, détail
/// d'une capacité), lue dans la requête du simulateur : tout y est à jour (PV, statut, PP, objet perdu ou consommé). Les
/// statistiques sont celles du Pokémon hors modificateurs de combat, comme sur le résumé du jeu.
/// </summary>
public sealed record BattleSheet(string Name, string SpeciesName, string IconUrl, string Gender, int Level,
    IReadOnlyList<BattleSheetType> Types, int Hp, int MaxHp, string? Status, bool Active, string Ability, string AbilityText,
    string? Item, string? ItemUrl, BattleMemberStats? Stats, IReadOnlyList<BattleSheetMove> Moves)
{
    public bool Fainted => Status == "fnt";

    public int Percent => MaxHp > 0 ? (int)Math.Ceiling(Hp * 100.0 / MaxHp) : 0;

    /// <summary>Types physiques de la 3e génération (les autres sont spéciaux) : pour Puissance Cachée, dont le type varie.</summary>
    private static readonly HashSet<string> PhysicalTypes = ["normal", "fighting", "flying", "poison", "ground", "rock", "bug", "ghost", "steel"];

    public static BattleSheet Of(BattleTeamMember member)
    {
        var (species, forme, speciesName) = BattleNames.SpeciesOf(member.Details);
        var (level, gender, shiny) = BattleProtocol.Details(member.Details);
        var (hp, max, status) = BattleProtocol.Condition(member.Condition);
        var ability = BattleNames.AbilityNumber(member.Ability);
        var item = BattleNames.ItemNumber(member.Item);
        var name = BattleProtocol.Who(member.Ident).Name;
        return new BattleSheet(
            name.Length > 0 ? name : speciesName,
            speciesName,
            PokemonSprites.Url(species, BattleNames.FormIndex(species, forme), EntityContext.Gen3, false, 0, shiny),
            gender,
            level,
            [.. (member.Types ?? []).Select(t => new BattleSheetType(BattleNames.Type(t), BattleNames.Key(t)))],
            hp,
            member.MaxHp > 0 ? member.MaxHp : max,
            status,
            member.Active,
            BattleNames.Ability(member.Ability),
            ability > 0 && ability < BattleDexData.Abilities.Length ? BattleDexData.Abilities[ability] : "",
            string.IsNullOrEmpty(member.Item) ? null : BattleNames.Item(member.Item),
            item > 0 ? PokemonInfo.ItemSpriteUrl(item) : null,
            member.Stats,
            [.. member.Moves.Select((id, i) => Move(id, member.Pp is { } pp && i < pp.Count ? pp[i] : null))]);
    }

    private static BattleSheetMove Move(string id, IReadOnlyList<int>? pp)
    {
        var (number, hidden, written) = BattleNames.ParseMove(id);
        var data = number > 0 && number < BattleDexData.Moves.Length ? BattleDexData.Moves[number] : default;
        var type = new BattleSheetType(BattleNames.MoveType(id), BattleNames.MoveTypeKey(id));
        var category = data.Category;
        if (hidden is not null)
            category = PhysicalTypes.Contains(hidden) ? BattleMoveCategory.Physical : BattleMoveCategory.Special;
        return new BattleSheetMove(BattleNames.Move(id), type, pp is [var left, ..] ? left : null, pp is [_, var total, ..] ? total : null,
            category, written > 0 ? written : data.Power, data.Accuracy, data.Text ?? ""); // capacité inconnue : pas de texte
    }
}
