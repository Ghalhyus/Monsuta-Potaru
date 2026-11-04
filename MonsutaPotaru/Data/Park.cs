namespace MonsutaPotaru.Data;

/// <summary>Paysage d'un parc : sa carte, son nom et ses textes (voir <see cref="Services.ParkLandscapes"/>).</summary>
public enum ParkLandscape
{
    /// <summary>Le premier parc de chacun, créé à sa première visite : un pré dans les bois (cartes parc-1-{taille}).</summary>
    Forest = 1,
    /// <summary>Un tiers de sable, deux tiers de mer (cartes plage-1-{taille}).</summary>
    Beach = 2,
    /// <summary>Un plateau de roche volcanique, des coulées et des mares de lave (cartes volcan-1-{taille}).</summary>
    Volcano = 3,
    /// <summary>Des dunes (~70 % de sable), un plateau d'où une cascade tombe dans une oasis (cartes desert-1-{taille}).</summary>
    Desert = 4,
    /// <summary>La pleine mer et trois îlots, seulement pour les Pokémon qui nagent (cartes ocean-1-{taille}).</summary>
    Ocean = 5,
    /// <summary>Une caverne dans la pénombre, ses piliers de roche, ses cristaux et un lac souterrain (cartes grotte-1-{taille}).</summary>
    Cave = 6,
}

/// <summary>
/// Parc d'un joueur : un par paysage au plus (index unique), la Forêt d'abord, les autres achetés avec le porte-monnaie
/// (voir <see cref="Services.ParkService"/>). Chacun a sa taille et ses Pokémon (<see cref="ParkMember"/>).
/// </summary>
public class Park
{
    public int Id { get; set; }

    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }

    public ParkLandscape Landscape { get; set; }

    /// <summary>Agrandissements achetés (0 à <see cref="Services.ParkService.MaxExpansions"/>) : la taille du parc, sa carte et sa capacité.</summary>
    public int Expansions { get; set; }
}
