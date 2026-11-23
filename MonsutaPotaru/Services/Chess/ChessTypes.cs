namespace MonsutaPotaru.Services;

/// <summary>Les dix-huit types des Pokémon (Fée comprise : table des types depuis la 6e génération).</summary>
public enum PokeType
{
    Normal, Fire, Water, Grass, Electric, Ice, Fighting, Poison, Ground, Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark,
    Steel, Fairy,
}

/// <summary>
/// Efficacité d'un type contre un autre (table des jeux depuis la 6e génération), pour les échecs Pokémon : chaque pièce
/// n'a qu'un type, la prise dépend du type de la pièce qui mange contre celui de la pièce mangée.
/// </summary>
public static class PokeTypes
{
    /// <summary>Nom français du type (« Électrik »).</summary>
    public static string Name(PokeType type) => type switch
    {
        PokeType.Normal => "Normal",
        PokeType.Fire => "Feu",
        PokeType.Water => "Eau",
        PokeType.Grass => "Plante",
        PokeType.Electric => "Électrik",
        PokeType.Ice => "Glace",
        PokeType.Fighting => "Combat",
        PokeType.Poison => "Poison",
        PokeType.Ground => "Sol",
        PokeType.Flying => "Vol",
        PokeType.Psychic => "Psy",
        PokeType.Bug => "Insecte",
        PokeType.Rock => "Roche",
        PokeType.Ghost => "Spectre",
        PokeType.Dragon => "Dragon",
        PokeType.Dark => "Ténèbres",
        PokeType.Steel => "Acier",
        _ => "Fée",
    };

    /// <summary>Nom anglais en minuscules, pour les classes CSS (« type-electric »), comme les capacités des combats.</summary>
    public static string Key(PokeType type) => type.ToString().ToLowerInvariant();

    // Par type qui attaque : types contre lesquels il est super efficace, pas très efficace, sans effet.
    private static readonly Dictionary<PokeType, (PokeType[] Super, PokeType[] Weak, PokeType[] None)> Chart = new()
    {
        [PokeType.Normal] = ([], [PokeType.Rock, PokeType.Steel], [PokeType.Ghost]),
        [PokeType.Fire] = ([PokeType.Grass, PokeType.Ice, PokeType.Bug, PokeType.Steel],
            [PokeType.Fire, PokeType.Water, PokeType.Rock, PokeType.Dragon], []),
        [PokeType.Water] = ([PokeType.Fire, PokeType.Ground, PokeType.Rock], [PokeType.Water, PokeType.Grass, PokeType.Dragon], []),
        [PokeType.Electric] = ([PokeType.Water, PokeType.Flying], [PokeType.Electric, PokeType.Grass, PokeType.Dragon], [PokeType.Ground]),
        [PokeType.Grass] = ([PokeType.Water, PokeType.Ground, PokeType.Rock],
            [PokeType.Fire, PokeType.Grass, PokeType.Poison, PokeType.Flying, PokeType.Bug, PokeType.Dragon, PokeType.Steel], []),
        [PokeType.Ice] = ([PokeType.Grass, PokeType.Ground, PokeType.Flying, PokeType.Dragon],
            [PokeType.Fire, PokeType.Water, PokeType.Ice, PokeType.Steel], []),
        [PokeType.Fighting] = ([PokeType.Normal, PokeType.Ice, PokeType.Rock, PokeType.Dark, PokeType.Steel],
            [PokeType.Poison, PokeType.Flying, PokeType.Psychic, PokeType.Bug, PokeType.Fairy], [PokeType.Ghost]),
        [PokeType.Poison] = ([PokeType.Grass, PokeType.Fairy], [PokeType.Poison, PokeType.Ground, PokeType.Rock, PokeType.Ghost],
            [PokeType.Steel]),
        [PokeType.Ground] = ([PokeType.Fire, PokeType.Electric, PokeType.Poison, PokeType.Rock, PokeType.Steel],
            [PokeType.Grass, PokeType.Bug], [PokeType.Flying]),
        [PokeType.Flying] = ([PokeType.Grass, PokeType.Fighting, PokeType.Bug], [PokeType.Electric, PokeType.Rock, PokeType.Steel], []),
        [PokeType.Psychic] = ([PokeType.Fighting, PokeType.Poison], [PokeType.Psychic, PokeType.Steel], [PokeType.Dark]),
        [PokeType.Bug] = ([PokeType.Grass, PokeType.Psychic, PokeType.Dark],
            [PokeType.Fire, PokeType.Fighting, PokeType.Poison, PokeType.Flying, PokeType.Ghost, PokeType.Steel, PokeType.Fairy], []),
        [PokeType.Rock] = ([PokeType.Fire, PokeType.Ice, PokeType.Flying, PokeType.Bug], [PokeType.Fighting, PokeType.Ground, PokeType.Steel], []),
        [PokeType.Ghost] = ([PokeType.Psychic, PokeType.Ghost], [PokeType.Dark], [PokeType.Normal]),
        [PokeType.Dragon] = ([PokeType.Dragon], [PokeType.Steel], [PokeType.Fairy]),
        [PokeType.Dark] = ([PokeType.Psychic, PokeType.Ghost], [PokeType.Fighting, PokeType.Dark, PokeType.Fairy], []),
        [PokeType.Steel] = ([PokeType.Ice, PokeType.Rock, PokeType.Fairy],
            [PokeType.Fire, PokeType.Water, PokeType.Electric, PokeType.Steel], []),
        [PokeType.Fairy] = ([PokeType.Fighting, PokeType.Dragon, PokeType.Dark], [PokeType.Fire, PokeType.Poison, PokeType.Steel], []),
    };

    /// <summary>Multiplicateur des dégâts : 2, 1, 0,5 ou 0.</summary>
    public static double Effectiveness(PokeType attacker, PokeType defender)
    {
        var (super, weak, none) = Chart[attacker];
        if (super.Contains(defender))
            return 2;
        if (weak.Contains(defender))
            return 0.5;
        return none.Contains(defender) ? 0 : 1;
    }
}
