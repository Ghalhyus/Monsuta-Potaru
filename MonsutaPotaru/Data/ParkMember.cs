namespace MonsutaPotaru.Data;

/// <summary>
/// Pokémon de la collection installé dans l'un des parcs de son dresseur (un seul à la fois ; 20 à 80 par parc selon ses
/// agrandissements, voir <see cref="Services.ParkService"/>).
/// Supprimé avec le Pokémon : retiré de la collection, ou recréé par la mise à jour de sa sauvegarde stockée, il quitte le parc.
/// </summary>
public class ParkMember
{
    public int BankPokemonId { get; set; }
    public BankPokemon? BankPokemon { get; set; }

    /// <summary>Dresseur du Pokémon (celui de <see cref="BankPokemon"/>), pour compter et lister sans jointure.</summary>
    public string OwnerId { get; set; } = "";

    /// <summary>Parc où il vit (<see cref="Park.OwnerId"/> : le même dresseur).</summary>
    public int ParkId { get; set; }
    public Park? Park { get; set; }

    public DateTime AddedAt { get; set; }

    /// <summary>
    /// Fin du malus « attaqué » (UTC) : tant qu'elle n'est pas passée, il a <see cref="MalusLevels"/> niveaux de bonheur
    /// en moins (<see cref="Services.ParkService.RecordAttackAsync"/>) ; null ou passée : aucun malus.
    /// </summary>
    public DateTime? MalusUntil { get; set; }

    /// <summary>Niveaux de bonheur perdus par les attaques (1 ou 2, voir <see cref="Services.ParkService.MaxMalusLevels"/>).</summary>
    public int MalusLevels { get; set; }
}
