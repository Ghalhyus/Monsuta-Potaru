using MonsutaPotaru.Data;

namespace MonsutaPotaru.Services;

/// <summary>
/// Paysages des parcs : nom, carte (wwwroot/parc/{carte}-{taille}.json, écrite par outils/carte-parc.py) et textes de la
/// page « Parc ». Un nouveau paysage : une valeur de <see cref="ParkLandscape"/>, ses cartes, et une ligne dans chaque
/// méthode.
/// </summary>
public static class ParkLandscapes
{
    /// <summary>Les paysages, dans l'ordre de la fenêtre d'achat.</summary>
    public static IReadOnlyList<ParkLandscape> All { get; } = Enum.GetValues<ParkLandscape>();

    public static string Name(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "Plage",
        ParkLandscape.Volcano => "Volcan",
        ParkLandscape.Desert => "Désert",
        _ => "Forêt",
    };

    /// <summary>Où vit un Pokémon de ce parc, dans une phrase (« il est à la plage »).</summary>
    public static string Where(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "à la plage",
        ParkLandscape.Volcano => "au volcan",
        ParkLandscape.Desert => "dans le désert",
        _ => "dans la forêt",
    };

    /// <summary>Pictogramme d'un Pokémon de ce parc dans la fenêtre « Choisir les Pokémon » d'un autre parc.</summary>
    public static string Icon(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "🏝️",
        ParkLandscape.Volcano => "🌋",
        ParkLandscape.Desert => "🏜️",
        _ => "🌳",
    };

    /// <summary>Début du nom de ses cartes : « plage-1 » pour plage-1-1.json à plage-1-4.json.</summary>
    public static string MapName(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "plage-1",
        ParkLandscape.Volcano => "volcan-1",
        ParkLandscape.Desert => "desert-1",
        _ => "parc-1",
    };

    /// <summary>Présentation dans la fenêtre « Acheter un parc ».</summary>
    public static string Description(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "Une côte de sable bordée de cocotiers, et surtout la mer : les Pokémon qui nagent y sont à leur aise, " +
                               "ceux qui craignent l'eau beaucoup moins.",
        ParkLandscape.Volcano => "Un plateau de roche volcanique où la lave tombe en cascades et coule jusqu'à des mares brûlantes : " +
                                 "le paradis des types Feu, Roche et Dragon, beaucoup moins celui des autres.",
        ParkLandscape.Desert => "Des dunes à perte de vue, un plateau d'où tombe une cascade et une oasis bordée de palmiers : " +
                                "le royaume des types Sol, bien moins celui des Pokémon qui craignent le sable.",
        _ => "Un pré au milieu des bois, avec ses arbres, ses fleurs et ses plans d'eau.",
    };

    /// <summary>Ce qui se passe à un agrandissement (fenêtre « Agrandir le parc »).</summary>
    public static string Expansion(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "La cocoteraie et le récif reculent et dévoilent une nouvelle partie de la plage",
        ParkLandscape.Volcano => "Les rochers reculent et dévoilent une nouvelle partie du volcan",
        ParkLandscape.Desert => "Les cactus reculent et dévoilent une nouvelle partie du désert",
        _ => "La forêt recule et dévoile une nouvelle partie du parc",
    };
}
