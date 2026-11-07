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
        ParkLandscape.Ocean => "Océan",
        ParkLandscape.Cave => "Grotte",
        ParkLandscape.Mountain => "Montagne",
        ParkLandscape.Dungeon => "Donjon",
        _ => "Forêt",
    };

    /// <summary>Où vit un Pokémon de ce parc, dans une phrase (« il est à la plage »).</summary>
    public static string Where(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "à la plage",
        ParkLandscape.Volcano => "au volcan",
        ParkLandscape.Desert => "dans le désert",
        ParkLandscape.Ocean => "dans l'océan",
        ParkLandscape.Cave => "dans la grotte",
        ParkLandscape.Mountain => "à la montagne",
        ParkLandscape.Dungeon => "dans le donjon",
        _ => "dans la forêt",
    };

    /// <summary>Pictogramme d'un Pokémon de ce parc dans la fenêtre « Choisir les Pokémon » d'un autre parc.</summary>
    public static string Icon(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "🏝️",
        ParkLandscape.Volcano => "🌋",
        ParkLandscape.Desert => "🏜️",
        ParkLandscape.Ocean => "🌊",
        ParkLandscape.Cave => "🦇",
        ParkLandscape.Mountain => "⛰️",
        ParkLandscape.Dungeon => "🏚️",
        _ => "🌳",
    };

    /// <summary>Début du nom de ses cartes : « plage-1 » pour plage-1-1.json à plage-1-4.json.</summary>
    public static string MapName(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "plage-1",
        ParkLandscape.Volcano => "volcan-1",
        ParkLandscape.Desert => "desert-1",
        ParkLandscape.Ocean => "ocean-1",
        ParkLandscape.Cave => "grotte-1",
        ParkLandscape.Mountain => "montagne-1",
        ParkLandscape.Dungeon => "donjon-1",
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
        ParkLandscape.Ocean => "La pleine mer à perte de vue, quelques rochers et trois îlots au loin : seuls les Pokémon qui nagent (type Eau) " +
                               "peuvent y vivre, et ils y sont comme chez eux.",
        ParkLandscape.Cave => "Une grande caverne dans la pénombre, éclairée par des torches et des cristaux, avec ses stalagmites et un lac " +
                              "souterrain : le refuge des types Sol, Roche, Ténèbres, Spectre et Dragon, bien moins celui des Pokémon " +
                              "qui aiment le grand air.",
        ParkLandscape.Mountain => "Une vallée traversée par une rivière, entre des massifs aux flancs de pierre et leurs sommets : tous " +
                                  "les Pokémon s'y promènent, mais seuls ceux qui volent (type Vol) montent se poser tout là-haut.",
        ParkLandscape.Dungeon => "Un souterrain abandonné, ses salles et ses couloirs de pierre, ses tombes, ses bassins d'égout et l'or " +
                                 "oublié, dans la pénombre des torches : le repaire des types Spectre, Ténèbres, Poison, Psy, Acier et " +
                                 "Dragon, bien moins celui des Pokémon qui aiment le grand air.",
        _ => "Un pré au milieu des bois, avec ses arbres, ses fleurs et ses plans d'eau.",
    };

    /// <summary>Ce qui se passe à un agrandissement (fenêtre « Agrandir le parc »).</summary>
    public static string Expansion(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Beach => "La cocoteraie et le récif reculent et dévoilent une nouvelle partie de la plage",
        ParkLandscape.Volcano => "Les rochers reculent et dévoilent une nouvelle partie du volcan",
        ParkLandscape.Desert => "Les cactus reculent et dévoilent une nouvelle partie du désert",
        ParkLandscape.Ocean => "Le récif recule et dévoile une nouvelle partie de l'océan",
        ParkLandscape.Cave => "La roche recule et dévoile une nouvelle partie de la grotte",
        ParkLandscape.Mountain => "La forêt de sapins recule et dévoile une nouvelle partie de la montagne",
        ParkLandscape.Dungeon => "Des éboulis sont dégagés et ouvrent de nouvelles salles du donjon",
        _ => "La forêt recule et dévoile une nouvelle partie du parc",
    };

    /// <summary>
    /// Ce qui empêche un Pokémon de ces types (index PKHeX, comme <c>BankPokemon.Type1</c>) de vivre dans ce paysage, en
    /// quelques mots (« Ne nage pas »), ou null s'il le peut. L'océan n'a que de l'eau et des îlots : seuls les nageurs y
    /// vont (<see cref="ParkHabitats.MovementOf(byte, byte)"/> : type Eau, choix de l'utilisateur, 2026-11-03). La montagne
    /// accueille tout le monde ; seuls les volants y montent sur les sommets (choix de l'utilisateur, 2026-11-07).
    /// </summary>
    public static string? Refusal(ParkLandscape landscape, byte type1, byte type2) => landscape switch
    {
        ParkLandscape.Ocean when ParkHabitats.MovementOf(type1, type2) is not ("swim" or "swim-lava") => "Ne nage pas",
        _ => null,
    };

    /// <summary>
    /// Remarque de la fenêtre « Choisir les Pokémon » pour un paysage où tous ne font pas la même chose (à l'océan, les
    /// autres sont grisés ; à la montagne, seuls les volants montent sur les sommets), ou null.
    /// </summary>
    public static string? ChooserNote(ParkLandscape landscape) => landscape switch
    {
        ParkLandscape.Ocean => "Dans l'océan, seuls ceux qui nagent (type Eau) peuvent vivre : les autres sont grisés.",
        ParkLandscape.Mountain => "À la montagne, tous se promènent dans la vallée, mais seuls ceux qui volent (type Vol) montent sur les sommets.",
        _ => null,
    };
}
